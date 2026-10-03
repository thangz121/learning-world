"""Phase 1.9.17 SpeechOcean762 phone-tier inspection (NO TRAINING, NO audio decode).

Streams the HF mirror, audits annotation reality (canonical phones, per-phone
scores, mispronunciation records), child-speaker/age coverage, and saves
<=25 representative annotation examples. Audio bytes are never decoded here;
format is verified separately against 2 original OpenSLR wav files.
"""
from __future__ import annotations

import json
from collections import Counter
from pathlib import Path

OUT = Path(__file__).resolve().parents[1]
CHILD_AGE_MAX = 15


def main() -> dict:
    from datasets import load_dataset
    per_split = {}
    examples = []
    ages = Counter()
    speakers: dict = {}
    phone_counter = Counter()
    scored_phones = 0
    mispro_cases = 0
    total = 0
    for split in ("train", "test"):
        ds = load_dataset("mispeech/speechocean762", split=split,
                          streaming=True)
        # drop the embedded-audio column BEFORE iteration: decoding needs
        # torchcodec (unavailable); annotation audit needs no audio bytes.
        keep = [c for c in ds.column_names if c != "audio"]
        ds = ds.select_columns(keep)
        n = 0
        for ex in ds:
            n += 1
            total += 1
            age = ex["age"]
            ages[age] += 1
            sp = speakers.setdefault(ex["speaker"],
                                     {"age": age, "gender": ex["gender"],
                                      "n": 0, "split": split})
            sp["n"] += 1
            for w in ex["words"]:
                for p, s in zip(w["phones"], w["phones-accuracy"]):
                    phone_counter[p] += 1
                    scored_phones += 1
                if w.get("mispronunciations"):
                    mispro_cases += 1
            if len(examples) < 25 and (age <= CHILD_AGE_MAX or len(examples) < 5):
                examples.append({
                    "split": split, "speaker": ex["speaker"], "age": age,
                    "gender": ex["gender"], "text": ex["text"],
                    "words": [{"text": w["text"], "phones": w["phones"],
                               "phones-accuracy": w["phones-accuracy"],
                               "mispronunciations": w.get("mispronunciations", [])}
                              for w in ex["words"]],
                })
        per_split[split] = n

    child_spk = {k: v for k, v in speakers.items() if v["age"] <= CHILD_AGE_MAX}
    report = {
        "source": "mispeech/speechocean762 (mirror of OpenSLR SLR101, CC BY 4.0)",
        "rows_streamed": per_split,
        "total_rows": total,
        "speakers_total": len(speakers),
        "child_speakers_le15": len(child_spk),
        "child_utterances_le15": sum(v["n"] for v in child_spk.values()),
        "age_values": sorted(ages),
        "age_histogram": {str(k): v for k, v in sorted(ages.items())},
        "distinct_canonical_phones": len(phone_counter),
        "scored_phone_tokens": scored_phones,
        "words_with_mispronunciations": mispro_cases,
        "phone_tier_type": "MANUAL-EXPERT-SCORED canonical phones "
                           "(5 experts; per-phone accuracy 0/1/2; NOT transcribed observed strings)",
        "examples": examples,
    }
    (OUT / "phone_tier_examples_so762.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "examples"},
                     indent=2))
    return report


if __name__ == "__main__":
    main()
