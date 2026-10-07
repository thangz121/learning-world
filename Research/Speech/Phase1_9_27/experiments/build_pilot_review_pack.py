"""WP-1.9.27 Part 10 (support) — build the pilot review pack (Pack P).

The 276-candidate pack (Pack R) covers the pre-1.9.27 evaluation speakers; the frozen
pilot uses 10 NEW speaker-disjoint speakers, so their labels must come from their own
blind pack. This script expands the frozen pilot manifest into one candidate per
final-consonant token using the SAME token identity and focus-token logic as
`pilot_runner.py`, so labels join `artifacts/pilot_features.csv` by `token_id`.

No audio is modified, no labels are created. Reviewer payload stays blind
(blind_id, word, target_phone); coordinator columns (split, age, corpus) are ignored
by the server.
"""
from __future__ import annotations

import csv
import json
import sys
from collections import defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402
from pilot_runner import _focus_tokens, _pilot_items  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_27"
MANIFEST = OUT / "06_PILOT_DATA" / "PILOT_DATA_MANIFEST.csv"
PACK_OUT = OUT / "06_PILOT_DATA" / "PILOT_REVIEW_CANDIDATES.csv"

PHONE_CLASS = {"stop": {"t", "k", "p", "d", "b", "ɡ"},
               "fricative": {"s", "z", "f", "v", "θ", "ð", "ʃ", "ʒ"},
               "nasal": {"n", "m", "ŋ"},
               "liquid": {"ɹ", "l"},
               "affricate": {"tʃ", "dʒ"}}
FIELDS = ["blind_id", "token_id", "corpus", "speaker_id", "split", "age", "word",
          "target_phone", "phone_class", "utt_id", "audio_reference", "reviewer_visible"]


def main():
    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
    inv = PhoneEvidenceV2().inv  # inventory only; production model NOT loaded
    detail, by_utt = _pilot_items()
    rows = []
    for utt_id in sorted(by_utt):
        m = by_utt[utt_id][0]
        parsed = _focus_tokens(detail, inv, utt_id, None, None)
        if parsed is None:
            raise RuntimeError(f"focus parse failed for {utt_id}")
        target, focus, wtext = parsed
        for k in focus:
            canon = target[k]
            cls = next((c for c, s2 in PHONE_CLASS.items() if canon in s2), "other")
            rows.append({
                "blind_id": f"P27-{len(rows)+1:03d}", "token_id": f"{utt_id}_{k}",
                "corpus": "so762_pilot", "speaker_id": m["speaker_id"],
                "split": m["split"], "age": m["age"], "word": wtext[k],
                "target_phone": canon, "phone_class": cls, "utt_id": utt_id,
                "audio_reference": m["audio_path"],
                "reviewer_visible": "blind_id,word,target_phone",
            })
    L.write_rows(PACK_OUT, rows, FIELDS)
    by_split = defaultdict(int)
    by_class = defaultdict(int)
    for r in rows:
        by_split[r["split"]] += 1
        by_class[r["phone_class"]] += 1
    summary = {"candidates": len(rows), "speakers": len({r["speaker_id"] for r in rows}),
               "by_split": dict(by_split), "by_class": dict(by_class),
               "r_tokens": sum(1 for r in rows if r["target_phone"] == "ɹ")}
    (OUT / "artifacts" / "pilot_review_pack_summary.json").write_text(
        json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=1))
    print(f"wrote {PACK_OUT}")
    print("DONE build_pilot_review_pack")


if __name__ == "__main__":
    main()
