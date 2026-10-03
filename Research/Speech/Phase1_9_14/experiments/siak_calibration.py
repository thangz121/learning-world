"""Phase 1.9.14 — P3 SIAK child calibration (RESEARCH ONLY).

Scores SIAK utterances with the FROZEN soft-v2 pipeline (PhoneEvidenceV2.soft_match)
and compares against SIAK's human 0-100 scores. No training, no calibration, no
production change. Speaker labels come from SIAK's own train/test split (each
speaker appears in exactly one split) so train/test reporting is speaker-disjoint.

Also documents the SIAK annotation structure and the rejected/zero-rating question
for the proposed assessability layer.

Usage:
  python siak_calibration.py --smoke
  python siak_calibration.py --ages 4 5 6 --max-per-age 0
  python siak_calibration.py --ages 7 8 9 10 --max-per-age 120
Resume-safe: existing rows in the output CSV are skipped.
"""
from __future__ import annotations

import argparse
import csv
import json
import math
import re
import sys
import time
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402

SIAK = REPO / "Research/Speech/ExternalData/SIAK"
OUT = REPO / "Research/Speech/Phase1_9_14"
RES = OUT / "artifacts" / "siak"


def parse_name(fname: str):
    m = re.match(r"(train|test)(\d{3})(fifi|enuk|othr)(\d{2})_", fname)
    if not m:
        return None
    return {"split": m.group(1), "speaker_num": m.group(2), "l1": m.group(3),
            "age": int(m.group(4)), "speaker_id": f"{m.group(1)}{m.group(2)}_{m.group(3)}"}


def build_index():
    idx = {}
    for p in (SIAK / "flac").rglob("*.flac"):
        idx[p.name] = p
    return idx


def load_metadata():
    rows = []
    for split in ("train", "test"):
        for r in csv.DictReader((SIAK / f"{split}.csv").open(encoding="utf-8")):
            meta = parse_name(r["file"])
            if not meta:
                continue
            meta["file"] = r["file"]
            meta["utterance"] = r["utterance"]
            meta["siak_score"] = int(r["score"])
            meta["split_csv"] = split
            rows.append(meta)
    return rows


def select(meta_rows, ages, max_per_age):
    picked = []
    by_age = defaultdict(list)
    for r in meta_rows:
        if r["age"] in ages:
            by_age[r["age"]].append(r)
    for age in ages:
        pool = by_age.get(age, [])
        pool = sorted(pool, key=lambda r: (r["speaker_id"], r["file"]))
        if max_per_age and len(pool) > max_per_age:
            step = len(pool) / max_per_age
            pool = [pool[int(i * step)] for i in range(max_per_age)]
        picked.extend(pool)
    return picked


def summarize(rows, out_prefix):
    def pearson(xs, ys):
        if len(xs) < 3:
            return None
        mx, my = sum(xs) / len(xs), sum(ys) / len(ys)
        num = sum((a - mx) * (b - my) for a, b in zip(xs, ys))
        dx = math.sqrt(sum((a - mx) ** 2 for a in xs))
        dy = math.sqrt(sum((b - my) ** 2 for b in ys))
        return num / (dx * dy) if dx and dy else None

    def spearman(xs, ys):
        if len(xs) < 3:
            return None
        def rank(v):
            order = sorted(range(len(v)), key=lambda i: v[i])
            r = [0.0] * len(v)
            i = 0
            while i < len(order):
                j = i
                while j + 1 < len(order) and v[order[j + 1]] == v[order[i]]:
                    j += 1
                avg = (i + j) / 2 + 1
                for k in range(i, j + 1):
                    r[order[k]] = avg
                i = j + 1
            return r
        return pearson(rank(xs), rank(ys))

    def block(rs):
        xs = [r["soft_full"] for r in rs]
        ys = [r["siak_score"] for r in rs]
        return {
            "n": len(rs),
            "pearson": pearson(xs, ys),
            "spearman": spearman(xs, ys),
            "mean_soft": float(np.mean(xs)) if xs else None,
            "mean_siak": float(np.mean(ys)) if ys else None,
            "frac_soft_lt50": float(np.mean([x < 50 for x in xs])) if xs else None,
            "frac_siak_ge80": float(np.mean([y >= 80 for y in ys])) if ys else None,
            "frr_proxy_soft_lt50_on_siak_ge80": (
                float(np.mean([r["soft_full"] < 50 for r in rs if r["siak_score"] >= 80]))
                if any(r["siak_score"] >= 80 for r in rs) else None),
            "frr_proxy_soft_lt20_on_siak_ge80": (
                float(np.mean([r["soft_full"] < 20 for r in rs if r["siak_score"] >= 80]))
                if any(r["siak_score"] >= 80 for r in rs) else None),
        }

    out = {"overall": block(rows)}
    for age in sorted(set(r["age"] for r in rows)):
        out[f"age_{age}"] = block([r for r in rows if r["age"] == age])
    for split in ("train", "test"):
        out[f"split_{split}"] = block([r for r in rows if r["split"] == split])
    spk = {}
    for sid in sorted(set(r["speaker_id"] for r in rows)):
        rs = [r for r in rows if r["speaker_id"] == sid]
        if len(rs) >= 10:
            spk[sid] = block(rs)
    out["speaker_disjoint_blocks"] = spk
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ages", type=int, nargs="*", default=[4, 5, 6, 7, 8, 9, 10])
    ap.add_argument("--max-per-age", type=int, default=120)
    ap.add_argument("--full-4-6", action="store_true", help="no cap for ages 4-6")
    ap.add_argument("--smoke", action="store_true")
    args = ap.parse_args()

    RES.mkdir(parents=True, exist_ok=True)
    out_csv = RES / "siak_scored.csv"

    meta = load_metadata()
    ages = args.ages
    picked = []
    for age in ages:
        pool = [r for r in meta if r["age"] == age]
        pool.sort(key=lambda r: (r["speaker_id"], r["file"]))
        cap = args.max_per_age
        if args.full_4_6 and age in (4, 5, 6):
            cap = 0
        if args.smoke:
            cap = 3
        if cap and len(pool) > cap:
            step = len(pool) / cap
            pool = [pool[int(i * step)] for i in range(cap)]
        picked.extend(pool)
    print(f"selected {len(picked)} utterances (ages={ages}, cap={args.max_per_age}, full46={args.full_4_6})", flush=True)

    done = set()
    if out_csv.exists():
        for r in csv.DictReader(out_csv.open(encoding="utf-8")):
            done.add(r["file"])
    print(f"{len(done)} already scored", flush=True)

    idx = build_index()
    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()
    fields = ["file", "utterance", "siak_score", "age", "speaker_id", "l1", "split", "duration_s",
              "soft_full", "confidence_0_1", "n_hits", "n_exact", "n_miss", "n_soft",
              "mean_sim", "mean_posterior", "processing_s", "error"]
    write_header = not out_csv.exists()
    t0 = time.perf_counter()
    n_new = 0
    with out_csv.open("a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        if write_header:
            w.writeheader()
        for i, r in enumerate(picked):
            if r["file"] in done:
                continue
            path = idx.get(r["file"])
            row = {k: "" for k in fields}
            row.update({k: r[k] for k in ("file", "utterance", "siak_score", "age", "speaker_id", "l1", "split")})
            try:
                dur = sf.info(str(path)).duration if path else 0.0
                sm = pev.soft_match(str(path), tgt.build(r["utterance"]).arpabet)
                mt = Counter(h.match_type for h in sm.hits)
                row.update({
                    "duration_s": round(dur, 3),
                    "soft_full": sm.soft_score_0_100,
                    "confidence_0_1": sm.confidence_0_1,
                    "n_hits": len(sm.hits),
                    "n_exact": mt.get("exact", 0), "n_miss": mt.get("miss", 0), "n_soft": mt.get("soft", 0),
                    "mean_sim": round(sm.mean_sim, 4),
                    "mean_posterior": round(sm.mean_posterior, 6),
                    "processing_s": round(sm.processing_s, 2),
                    "error": "",
                })
            except Exception as exc:  # noqa: BLE001
                row["error"] = type(exc).__name__ + ":" + str(exc)[:120]
            w.writerow(row)
            f.flush()
            n_new += 1
            if i % 10 == 0:
                print(f"[{i+1}/{len(picked)}] {r['file']} age={r['age']} siak={r['siak_score']} "
                      f"soft={row['soft_full']} ({time.perf_counter()-t0:.0f}s)", flush=True)

    rows = []
    for r in csv.DictReader(out_csv.open(encoding="utf-8")):
        if r.get("error"):
            continue
        for k in ("siak_score", "age", "soft_full", "confidence_0_1", "duration_s"):
            r[k] = float(r[k])
        r["age"] = int(r["age"])
        r["siak_score"] = int(r["siak_score"])
        rows.append(r)
    summary = {
        "phase": "1.9.14",
        "experiment": "P3 SIAK calibration (soft-v2, no training, no calibration)",
        "model": "facebook/wav2vec2-xlsr-53-espeak-cv-ft via frozen PhoneEvidenceV2@1.4.0",
        "n_scored": len(rows),
        "n_new_this_run": n_new,
        "results": summarize(rows, args),
        "production_vad": False, "router_locked": False, "unity_integrated": False,
        "scorer_modified": False, "production_window_locked": False,
    }
    (RES / f"siak_summary_ages{'-'.join(map(str, ages))}.json").write_text(
        json.dumps(summary, indent=2), encoding="utf-8")
    print("DONE", json.dumps(summary["results"]["overall"], indent=1))


if __name__ == "__main__":
    main()
