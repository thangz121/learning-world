"""Phase 1.9.15 — SIAK leakage audit + strict speaker-disjoint 3-way split.

Merges the expanded scoring (siak_expand.py) with the reused 1.9.14 scores for
the same deterministic 20/speaker selection, audits duplicates/overlap, and
assigns speakers to TRAIN / VALIDATION / TEST (speaker-disjoint, age-stratified).

Primary population: ages 7-12. Ages 4-6 are held out as a limited external
analysis (5 speakers) and are NOT used for fitting.

Outputs:
  artifacts/siak/siak_expanded_merged.csv
  artifacts/siak/calibration_train.csv | calibration_valid.csv | calibration_test.csv
  artifacts/siak/calibration_ages46.csv
  artifacts/siak/split_manifest.json
"""
from __future__ import annotations

import csv
import hashlib
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
SIAK = REPO / "Research/Speech/ExternalData/SIAK"
OUT = REPO / "Research/Speech/Phase1_9_15"
RES = OUT / "artifacts" / "siak"
NEW_SCORED = RES / "siak_expanded_scored.csv"
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


def sha1_file(p: Path) -> str:
    h = hashlib.sha1()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def selection(cap=20):
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
    selected = {}
    for sid, items in sorted(by_spk.items()):
        items.sort(key=lambda r: r["file"])
        if len(items) > cap:
            step = len(items) / cap
            items = [items[int(i * step)] for i in range(cap)]
        for it in items:
            selected[it["file"]] = it
    return selected


def assign_splits(speaker_meta: dict, seed=1515):
    """Age-stratified speaker-disjoint 70/15/15 assignment (deterministic).

    Only primary-population speakers (age >= 7) are assigned. Ages 4-6 stay out
    of fitting and are marked external.
    """
    by_age = defaultdict(list)
    for sid, meta in speaker_meta.items():
        if meta["age"] >= 7:
            by_age[meta["age"]].append(sid)
    assign = {}
    for age, sids in sorted(by_age.items()):
        sids = sorted(sids, key=lambda s: hashlib.sha1(f"{seed}:{s}".encode()).hexdigest())
        n = len(sids)
        n_val = max(1, round(n * 0.15)) if n >= 4 else (1 if n >= 2 else 0)
        n_test = max(1, round(n * 0.15)) if n >= 4 else (0 if n < 2 else 1)
        if n_val + n_test >= n:
            n_val, n_test = max(0, n - 1), min(1, n - 1)
        for i, sid in enumerate(sids):
            if i < n - n_val - n_test:
                assign[sid] = "train"
            elif i < n - n_test:
                assign[sid] = "validation"
            else:
                assign[sid] = "test"
    return assign


def main():
    RES.mkdir(parents=True, exist_ok=True)
    sel = selection(20)

    merged = {}
    for src in (P1914_SCORED, NEW_SCORED):
        if not src.exists():
            continue
        for r in csv.DictReader(src.open(encoding="utf-8")):
            if r.get("error") or r["file"] not in sel:
                continue
            if r["file"] not in merged:
                merged[r["file"]] = r
    print(f"merged {len(merged)} rows for {len(sel)} selected files", flush=True)
    missing = sorted(set(sel) - set(merged))
    print(f"missing scores: {len(missing)}", flush=True)

    # fill any missing metadata from selection (without scores)
    rows = []
    for fname, r in sorted(merged.items()):
        row = {k: r.get(k, "") for k in FIELDS}
        for k in ("file", "utterance", "age", "speaker_id", "l1", "split"):
            row[k] = r.get(k, sel[fname].get(k, sel[fname].get("score", "")))
        row["siak_score"] = r.get("siak_score", sel[fname]["score"])
        row["age"] = int(float(r.get("age", sel[fname]["age"])))
        rows.append(row)
    with open(RES / "siak_expanded_merged.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=FIELDS)
        w.writeheader()
        w.writerows(rows)

    # duplicate audio audit (hash the files actually used)
    idx = {p.name: p for p in (SIAK / "flac").rglob("*.flac")}
    hashes = defaultdict(list)
    for i, r in enumerate(rows):
        p = idx.get(r["file"])
        if p:
            hashes[sha1_file(p)].append(r["file"])
    dup = {h: fs for h, fs in hashes.items() if len(fs) > 1}
    print(f"duplicate audio groups: {len(dup)}", flush=True)

    speaker_meta = {}
    for r in rows:
        speaker_meta[r["speaker_id"]] = {"age": r["age"], "split_csv": r["split"], "l1": r["l1"]}
    assign = assign_splits(speaker_meta)

    by_split = defaultdict(list)
    for r in rows:
        sid = r["speaker_id"]
        if r["age"] < 7:
            r["population"] = "ages_4_6_external"
            r["split_assignment"] = "external"
        else:
            r["population"] = "primary"
            r["split_assignment"] = assign.get(sid, "train")
        by_split[r["split_assignment"]].append(r)

    with open(RES / "siak_expanded_merged.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)

    for name, fname in (("train", "calibration_train.csv"),
                        ("validation", "calibration_valid.csv"),
                        ("test", "calibration_test.csv")):
        with open(RES / fname, "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
            w.writeheader()
            w.writerows(by_split.get(name, []))
    with open(RES / "calibration_ages46.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(by_split.get("external", []))

    def block(rs):
        return {"n_utterances": len(rs), "n_speakers": len(set(r["speaker_id"] for r in rs)),
                "speakers": sorted(set(r["speaker_id"] for r in rs)),
                "ages": dict(sorted(Counter(r["age"] for r in rs).items())),
                "n_targets": len(set(r["utterance"] for r in rs))}

    overlap_tv = set(r["speaker_id"] for r in by_split["train"]) & set(r["speaker_id"] for r in by_split["test"])
    overlap_vv = set(r["speaker_id"] for r in by_split["train"]) & set(r["speaker_id"] for r in by_split["validation"])
    overlap_vt = set(r["speaker_id"] for r in by_split["validation"]) & set(r["speaker_id"] for r in by_split["test"])
    targets_t = set(r["utterance"] for r in by_split["train"])
    targets_v = set(r["utterance"] for r in by_split["test"])

    manifest = {
        "phase": "1.9.15",
        "source": "SIAK (CC-BY-ND-4.0), 20 utterances/speaker deterministic sample",
        "n_rows_merged": len(rows),
        "n_selected_missing_scores": len(missing),
        "duplicate_audio_groups": {h: fs for h, fs in list(dup.items())[:50]},
        "n_duplicate_audio_groups": len(dup),
        "splits": {name: block(rs) for name, rs in by_split.items()},
        "leakage_check": {
            "speaker_overlap_train_test": sorted(overlap_tv),
            "speaker_overlap_train_validation": sorted(overlap_vv),
            "speaker_overlap_validation_test": sorted(overlap_vt),
            "file_overlap": [],
            "target_overlap_train_test_n": len(targets_t & targets_v),
            "target_overlap_examples": sorted(targets_t & targets_v)[:20],
        },
        "age_4_6_note": "5 speakers held out of fitting; reported separately as limited external analysis",
    }
    (RES / "split_manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in manifest["splits"].items()}, indent=2))
    print("overlaps tt/tv/vt:", len(overlap_tv), len(overlap_vv), len(overlap_vt),
          "| dup groups:", len(dup), "| missing:", len(missing))


if __name__ == "__main__":
    main()
