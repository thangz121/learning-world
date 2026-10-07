"""WP-1.9.24 Part A — final blinded review pack + empty label results.

Reads the WP-1.9.23 23-candidate pack, classifies candidates, adds review order
and blinding metadata. No labels are fabricated; NEW_LABELS_COLLECTED = 0.
"""
from __future__ import annotations

import csv
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_24"
PACK = L.REPO / "Research/Speech/Phase1_9_23/HUMAN_LABEL_REVIEW_PACK.csv"

FIELDS = [
    "blind_id", "review_order", "priority", "candidate_id", "speaker_id", "word",
    "target_phone", "source_corpus", "audio_reference", "candidate_class",
    "tag_r_priority", "tag_weak_present", "tag_rank25_identity",
    "tag_strong_false_accept", "reference_note", "machine_rank", "machine_max_A",
    "machine_blank_mean", "machine_margin", "production_decision",
    "best_rule_decision", "why_needed", "reviewer_visible_fields",
    "hidden_until_label", "review_status",
]

RESULTS_FIELDS = [
    "label_id", "token_id", "speaker_id", "word", "target_phone", "human_label",
    "confidence", "reviewer_id", "reviewer_count", "review_note", "source_corpus",
    "blinding_ok", "collected_at",
]


def main():
    rows = list(csv.DictReader(open(PACK, encoding="utf-8")))
    order = 0
    out = []
    for r in rows:
        order += 1
        pri = int(r["priority"])
        if pri == 4:
            cls = "ABSENT_CANDIDATE"
        elif pri == 5:
            cls = "ABSENT_CANDIDATE"
        elif pri == 1:
            cls = "UNCERTAIN_CANDIDATE"  # /r/ LWE four: word verdict negative, label needed
        else:
            cls = "PRESENT_CANDIDATE"
        out.append({
            "blind_id": f"L{order:02d}", "review_order": order,
            "priority": pri, "candidate_id": r["candidate_id"],
            "speaker_id": r["speaker_id"], "word": r["word"],
            "target_phone": r["target_phone"], "source_corpus": r["source_corpus"],
            "audio_reference": r["audio_reference"], "candidate_class": cls,
            "tag_r_priority": int(r["target_phone"] in ("ɹ", "?")),
            "tag_weak_present": int(pri == 2),
            "tag_rank25_identity": int(pri == 3),
            "tag_strong_false_accept": int(pri == 4),
            "reference_note": r["reference_note"],
            "machine_rank": r["machine_rank"], "machine_max_A": r["machine_max_A"],
            "machine_blank_mean": r["machine_blank_mean"],
            "machine_margin": r["machine_margin"],
            "production_decision": r["production_decision"],
            "best_rule_decision": r["best_rule_decision"],
            "why_needed": r["why_needed"],
            "reviewer_visible_fields": "blind_id; word; target_phone; audio_reference",
            "hidden_until_label": "machine_rank; machine_max_A; machine_blank_mean; "
                                  "machine_margin; production_decision; best_rule_decision; "
                                  "reference_note",
            "review_status": "READY_FOR_HUMAN_REVIEW",
        })
    L.write_rows(OUT / "HUMAN_LABEL_REVIEW_PACK_FINAL.csv", out, FIELDS)
    L.write_rows(OUT / "HUMAN_LABEL_RESULTS.csv", [], RESULTS_FIELDS)
    print("pack rows:", len(out),
          "| classes:", {c: sum(1 for r in out if r["candidate_class"] == c)
                         for c in ("PRESENT_CANDIDATE", "ABSENT_CANDIDATE",
                                   "UNCERTAIN_CANDIDATE")})
    print("NEW_LABELS_COLLECTED = 0")
    print("DONE build_review_pack_final")


if __name__ == "__main__":
    main()
