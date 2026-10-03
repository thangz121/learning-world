"""Phase 1.9.18 STEP 3 — speaker-disjoint child-focused SO762 split.

Streams every SO762 row (annotations only) and builds a deterministic
speaker-disjoint split over the 122 child speakers (seed 1515, inherited).
Adults are kept separately. Writes child_split.json + summary doc data.
"""
from __future__ import annotations

import json
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from so762_common import (CHILD_AGE_MAX, SEED, iter_rows,  # noqa: E402
                          make_child_split)

OUT = Path(__file__).resolve().parents[1]


def summarize(rows, adapter):
    ages = Counter()
    phones = Counter()
    err = Counter()
    n_utt = len(rows)
    n_phone = 0
    n_bad = 0
    dur_s = 0.0
    for r in rows:
        ages[r["age"]] += 1
        for w in r["words"]:
            for p, a in zip(w["phones"], w["phones-accuracy"]):
                phones[p.rstrip("012")] += 1
                n_phone += 1
                if float(a) < 0.5:
                    n_bad += 1
            for mp in (w.get("mispronunciations") or []):
                pron = mp["pronounced-phone"]
                err["deletion" if pron == "<DEL>" else
                    "unknown" if pron == "<unk>" else "substitution"] += 1
        dur_s += float(r.get("dur_s", 0.0))
    return {
        "n_utterances": n_utt,
        "n_phone_tokens": n_phone,
        "n_bad_tokens": n_bad,
        "bad_rate": round(n_bad / n_phone, 6) if n_phone else 0.0,
        "age_distribution": dict(sorted(ages.items())),
        "error_type_distribution": dict(err),
        "top_phones": dict(phones.most_common(20)),
        "hours": round(dur_s / 3600.0, 3),
        "duration_source": "NOT_MEASURED_annotation_only",
    }


def main() -> dict:
    from Research.Speech.Phase1_2.Adapters.target_cmudict import (
        CmuDictTargetAdapter)
    adapter = CmuDictTargetAdapter()
    rows = list(iter_rows(splits=("train", "test"), with_audio=False))
    splits, assigned, adult_spk = make_child_split(rows)

    tr = set(assigned["train"])
    va = set(assigned["validation"])
    te = set(assigned["test"])
    overlap = sorted((tr & va) | (tr & te) | (va & te))
    # test leakage: no test speaker appears in train/val
    child_id = {}
    for r in rows:
        if r["speaker"] in tr:
            child_id[r["speaker"]] = "train"
        elif r["speaker"] in va:
            child_id[r["speaker"]] = "validation"
        elif r["speaker"] in te:
            child_id[r["speaker"]] = "test"

    out = {
        "phase": "1.9.18",
        "kind": "speaker-disjoint child-focused SO762 split",
        "seed": SEED,
        "child_age_max": CHILD_AGE_MAX,
        "source_revision": "06385584fad212b26134c656fdd3ccf9f093f33e",
        "total_rows": len(rows),
        "child_speakers": len(assigned["train"]) + len(assigned["validation"])
        + len(assigned["test"]),
        "adult_speakers_excluded": len(adult_spk),
        "speaker_overlap": overlap,
        "test_speaker_leakage": sorted(te & (tr | va)),
        "splits": {
            "train": {"n_speakers": len(tr),
                      "speakers": sorted(tr),
                      **summarize(splits["train"], adapter)},
            "validation": {"n_speakers": len(va),
                           "speakers": sorted(va),
                           **summarize(splits["validation"], adapter)},
            "test": {"n_speakers": len(te),
                     "speakers": sorted(te),
                     **summarize(splits["test"], adapter)},
            "adult_excluded": {"n_speakers": len(adult_spk),
                               "speakers": adult_spk,
                               **summarize(splits["adult"], adapter)},
        },
    }
    out["status"] = ("VERIFIED_COMPLETE"
                     if not overlap and not (te & (tr | va)) else "FAILED")
    (OUT / "child_split.json").write_text(
        json.dumps(out, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({k: v for k, v in out.items() if k != "splits"}, indent=2))
    for k in ("train", "validation", "test", "adult_excluded"):
        s = out["splits"][k]
        print(f"{k}: {s['n_speakers']} spk / {s['n_utterances']} utt / "
              f"{s['n_phone_tokens']} phones / {s['n_bad_tokens']} bad")
    return out


if __name__ == "__main__":
    r = main()
    sys.exit(0 if r["status"] == "VERIFIED_COMPLETE" else 1)
