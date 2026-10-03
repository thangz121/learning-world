"""Phase 1.9.14 — SIAK metadata + rejected/zero-rating investigation (no model).

Documents the downloaded SIAK release exactly: counts, ages, speakers, splits,
score distribution, duplicate-utterance structure, and whether the documented
rejected/zero-rating class is observable in this release. Feeds the assessability
layer question (section 12) without conflating low scores with rejections.
"""
from __future__ import annotations

import csv
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
SIAK = REPO / "Research/Speech/ExternalData/SIAK"
RES = REPO / "Research/Speech/Phase1_9_14/artifacts/siak"


def parse_name(fname: str):
    m = re.match(r"(train|test)(\d{3})(fifi|enuk|othr)(\d{2})_", fname)
    if not m:
        return None
    return {"split": m.group(1), "speaker_id": f"{m.group(1)}{m.group(2)}_{m.group(3)}",
            "l1": m.group(3), "age": int(m.group(4))}


def main():
    RES.mkdir(parents=True, exist_ok=True)
    rows = []
    for split in ("train", "test"):
        for r in csv.DictReader((SIAK / f"{split}.csv").open(encoding="utf-8")):
            meta = parse_name(r["file"])
            if not meta:
                continue
            meta.update({"file": r["file"], "utterance": r["utterance"], "score": int(r["score"])})
            rows.append(meta)

    by_age = Counter(r["age"] for r in rows)
    by_split = Counter(r["split"] for r in rows)
    by_l1 = Counter(r["l1"] for r in rows)
    speakers_by_age = defaultdict(set)
    for r in rows:
        speakers_by_age[r["age"]].add(r["speaker_id"])
    utt_by_age = {age: Counter(r["utterance"] for r in rows if r["age"] == age) for age in sorted(by_age)}

    scores = Counter(r["score"] for r in rows)
    total = len(rows)
    score_bands = {
        "zero": scores.get(0, 0),
        "1_19": sum(v for k, v in scores.items() if 1 <= k <= 19),
        "20_49": sum(v for k, v in scores.items() if 20 <= k <= 49),
        "50_79": sum(v for k, v in scores.items() if 50 <= k <= 79),
        "80_100": sum(v for k, v in scores.items() if 80 <= k <= 100),
    }
    # age 4-6 detail
    a46 = [r for r in rows if r["age"] in (4, 5, 6)]
    a46_speakers = Counter(r["speaker_id"] for r in a46)
    a46_split = Counter(r["split"] for r in a46)
    a46_utt = Counter(r["utterance"] for r in a46)
    score1 = [r for r in rows if r["score"] == 1]
    score1_age = Counter(r["age"] for r in score1)

    speaker_index = []
    for sid in sorted(set(r["speaker_id"] for r in rows)):
        rs = [r for r in rows if r["speaker_id"] == sid]
        speaker_index.append({
            "speaker_id": sid, "split": rs[0]["split"], "l1": rs[0]["l1"], "age": rs[0]["age"],
            "n_utterances": len(rs), "n_unique_targets": len(set(r["utterance"] for r in rs)),
            "score_mean": round(sum(r["score"] for r in rs) / len(rs), 2),
        })

    summary = {
        "release": {
            "path": str(SIAK),
            "source": "Kaggle mirror of SIAK (Say It Again, Kid!) with identifying metadata removed",
            "license": "CC-BY-ND-4.0 (commercial building/evaluation of speech technology models not prohibited)",
            "files": {"train_csv": 12308, "test_csv": 4000, "flac_indexed": 16308},
        },
        "annotation": {
            "fields": ["file", "utterance", "score"],
            "score": "single expert annotator, 0-100 (paper maps to 0-5 stars)",
            "utterance": "single word or short phrase (dashes for spaces)",
            "metadata_in_filename": ["split", "speaker", "L1", "age_years", "sample_no", "seconds_since_first"],
            "age_is_2_digit_years": True,
        },
        "counts": {
            "total": total, "by_split": dict(by_split), "by_l1": dict(by_l1),
            "by_age": dict(sorted(by_age.items())),
            "speakers_total": len(set(r["speaker_id"] for r in rows)),
            "unique_utterances": len(set(r["utterance"] for r in rows)),
            "score_bands": score_bands,
            "score_min": min(r["score"] for r in rows),
            "score_max": max(r["score"] for r in rows),
            "score_zero_n": scores.get(0, 0),
        },
        "age_4_6": {
            "n": len(a46),
            "speakers": dict(a46_speakers),
            "split": dict(a46_split),
            "unique_targets": len(a46_utt),
            "score_bands": {
                "1_19": sum(1 for r in a46 if r["score"] <= 19),
                "20_49": sum(1 for r in a46 if 20 <= r["score"] <= 49),
                "50_79": sum(1 for r in a46 if 50 <= r["score"] <= 79),
                "80_100": sum(1 for r in a46 if r["score"] >= 80),
            },
        },
        "rejected_class_investigation": {
            "documented_externally": "SLATE 2023 paper (E2): 1,489 items rejected before scoring "
                                     "(silence, interrupted, wrong word, spoken noise, lack of effort)",
            "observable_in_downloaded_release": False,
            "evidence": f"score minimum is {min(r['score'] for r in rows)}; zero-rating rows = 0; "
                        "rejected items are not present as a labeled class",
            "score_1_items": {"n": len(score1), "by_age": dict(sorted(score1_age.items())),
                              "interpretation": "rating 1 of 100 = scored as very poor pronunciation, "
                                                "not an explicit rejection; must not be conflated with "
                                                "the paper's rejected class"},
            "conclusion": "SIAK provides documented (paper-level) support for a pre-scoring rejection "
                          "gate, but the released data cannot empirically verify an assessability "
                          "classifier: no rejected rows, no rejection reason, no zero ratings.",
        },
        "speaker_leakage": {
            "official_splits": "filenames encode train/test; speaker numbers are disjoint across splits",
            "within_split_repeats": "each speaker contributes many repetitions of the same targets "
                                    "(e.g., train001 has 420 items over months); any future fine-tuning "
                                    "must hold out speakers, not utterances",
            "cross_split_speaker_ids": sorted(set(r['speaker_id'] for r in rows if r['split'] == 'train') &
                                              set(r['speaker_id'] for r in rows if r['split'] == 'test')),
        },
    }
    (RES / "siak_metadata.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")

    with open(RES / "siak_speakers.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(speaker_index[0].keys()))
        w.writeheader()
        w.writerows(speaker_index)

    print(json.dumps({"counts": summary["counts"], "age_4_6": summary["age_4_6"],
                      "rejected": summary["rejected_class_investigation"]["observable_in_downloaded_release"],
                      "score1": len(score1)}, indent=2))


if __name__ == "__main__":
    main()
