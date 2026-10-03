"""Phase 1.9.15 — expand SIAK scoring to a speaker-balanced sample (RESEARCH ONLY).

Samples up to --cap utterances per speaker (deterministic), reuses Phase 1.9.14
scores where the same file was already scored, and scores the rest with the
frozen soft-v2 pipeline (PhoneEvidenceV2.soft_match).

Output: artifacts/siak/siak_expanded_scored.csv (one row per file, resume-safe).
"""
from __future__ import annotations

import argparse
import csv
import re
import sys
import time
from collections import Counter, defaultdict
from pathlib import Path

import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402

SIAK = REPO / "Research/Speech/ExternalData/SIAK"
OUT = REPO / "Research/Speech/Phase1_9_15"
RES = OUT / "artifacts" / "siak"
P1914_SCORED = REPO / "Research/Speech/Phase1_9_14/artifacts/siak/siak_scored.csv"

FIELDS = ["file", "utterance", "siak_score", "age", "speaker_id", "l1", "split", "duration_s",
          "soft_full", "confidence_0_1", "n_hits", "n_exact", "n_soft", "n_miss",
          "mean_sim", "mean_posterior", "processing_s", "error"]


def parse_name(fname: str):
    m = re.match(r"(train|test)(\d{3})(fifi|enuk|othr)(\d{2})_", fname)
    if not m:
        return None
    return {"split": m.group(1), "speaker_id": f"{m.group(1)}{m.group(2)}_{m.group(3)}",
            "l1": m.group(3), "age": int(m.group(4))}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cap", type=int, default=20)
    ap.add_argument("--limit-new", type=int, default=0)
    args = ap.parse_args()

    RES.mkdir(parents=True, exist_ok=True)
    rows = []
    for split in ("train", "test"):
        for r in csv.DictReader((SIAK / f"{split}.csv").open(encoding="utf-8")):
            meta = parse_name(r["file"])
            if not meta:
                continue
            meta.update({"file": r["file"], "utterance": r["utterance"], "score": int(r["score"])})
            rows.append(meta)

    by_spk = defaultdict(list)
    for r in rows:
        by_spk[r["speaker_id"]].append(r)

    selected = []
    for sid, items in sorted(by_spk.items()):
        items.sort(key=lambda r: r["file"])
        if len(items) > args.cap:
            step = len(items) / args.cap
            items = [items[int(i * step)] for i in range(args.cap)]
        selected.extend(items)
    print(f"selected {len(selected)} of {len(rows)} utterances across {len(by_spk)} speakers", flush=True)

    out_csv = RES / "siak_expanded_scored.csv"
    done = {}
    if out_csv.exists():
        for r in csv.DictReader(out_csv.open(encoding="utf-8")):
            if not r.get("error"):
                done[r["file"]] = r
    # seed from 1.9.14 for identical files
    if P1914_SCORED.exists():
        for r in csv.DictReader(P1914_SCORED.open(encoding="utf-8")):
            if not r.get("error") and r["file"] not in done:
                done[r["file"]] = r
    print(f"{len(done)} rows already available (1.9.15 + reused 1.9.14)", flush=True)

    idx = {}
    for p in (SIAK / "flac").rglob("*.flac"):
        idx[p.name] = p
    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()

    write_header = not out_csv.exists()
    t0 = time.perf_counter()
    n_new = 0
    with out_csv.open("a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=FIELDS)
        if write_header:
            w.writeheader()
        for r in selected:
            if r["file"] in done:
                continue
            if args.limit_new and n_new >= args.limit_new:
                break
            path = idx.get(r["file"])
            row = {k: "" for k in FIELDS}
            row.update({k: r[k] for k in ("file", "utterance", "speaker_id", "l1", "split")})
            row["siak_score"] = r["score"]
            row["age"] = r["age"]
            try:
                dur = sf.info(str(path)).duration if path else 0.0
                sm = pev.soft_match(str(path), tgt.build(r["utterance"]).arpabet)
                mt = Counter(h.match_type for h in sm.hits)
                row.update({
                    "duration_s": round(dur, 3), "soft_full": sm.soft_score_0_100,
                    "confidence_0_1": sm.confidence_0_1, "n_hits": len(sm.hits),
                    "n_exact": mt.get("exact", 0), "n_soft": mt.get("soft", 0),
                    "n_miss": mt.get("miss", 0), "mean_sim": round(sm.mean_sim, 4),
                    "mean_posterior": round(sm.mean_posterior, 6),
                    "processing_s": round(sm.processing_s, 2), "error": "",
                })
            except Exception as exc:  # noqa: BLE001
                row["error"] = type(exc).__name__ + ":" + str(exc)[:120]
            w.writerow(row)
            f.flush()
            n_new += 1
            if n_new % 100 == 0:
                print(f"  new {n_new} ({time.perf_counter()-t0:.0f}s) last={r['file']}", flush=True)
    print(f"scored {n_new} new rows in {time.perf_counter()-t0:.0f}s", flush=True)


if __name__ == "__main__":
    main()
