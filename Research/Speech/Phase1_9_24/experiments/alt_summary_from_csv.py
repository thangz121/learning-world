"""Summarize the alternative-encoder counterfactual from its CSV (no rerun)."""
from __future__ import annotations

import csv
import json
import statistics as st
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

ART = L.REPO / "Research/Speech/Phase1_9_24/artifacts"


def main():
    rows = list(csv.DictReader(open(ART / "alt_encoder_counterfactual.csv", encoding="utf-8")))
    for r in rows:
        r["pm"] = float(r["primary_max_A"])
        r["am"] = float(r["alt_max_A"])
    summary = {}
    for g in ("TYPE_B", "lwe_labeled", "so762_no_evidence_present", "so762_strong_present",
              "so762_absent"):
        rs = [r for r in rows if r["group"] == g]
        if not rs:
            continue
        summary[g] = {
            "n": len(rs),
            "primary_max_median": round(st.median([r["pm"] for r in rs]), 5),
            "alt_max_median": round(st.median([r["am"] for r in rs]), 5),
            "primary_finds_evidence_rate": round(sum(1 for r in rs if r["pm"] >= 0.10) / len(rs), 4),
            "alt_finds_evidence_rate": round(sum(1 for r in rs if r["am"] >= 0.10) / len(rs), 4),
            "persists_rate": round(sum(1 for r in rs if r["am"] >= 0.10 and r["pm"] >= 0.10)
                                  / len(rs), 4),
            "lost_rate": round(sum(1 for r in rs if r["am"] < 0.10 <= r["pm"]) / len(rs), 4),
            "gained_rate": round(sum(1 for r in rs if r["am"] >= 0.10 > r["pm"]) / len(rs), 4),
        }
    out = {
        "model": "facebook/wav2vec2-lv-60-espeak-cv-ft",
        "local_files_only": True,
        "note": "second espeak-phoneme encoder; small diagnostic subset; "
                "evidence = max target-class posterior in the production span",
        "summary": summary,
        "type_b_rows": [{"token_id": r["token_id"], "primary": r["pm"], "alt": r["am"]}
                        for r in rows if r["group"] == "TYPE_B"],
    }
    (ART / "alt_encoder_summary.json").write_text(
        json.dumps(out, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(summary, indent=1))
    print("DONE alt_summary_from_csv")


if __name__ == "__main__":
    main()
