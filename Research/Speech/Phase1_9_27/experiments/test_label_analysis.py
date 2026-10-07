"""Tooling validation for label_analysis.py (SYNTHETIC QA ONLY — deleted after).

Creates two synthetic reviewer files over 6 pack candidates, runs the pipeline,
verifies agreement/consensus/adjudication outputs, then deletes everything.
No real labels are created; nothing is committed.
"""
from __future__ import annotations

import csv
import json
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
PHASE = HERE.parent
LAB26 = PHASE.parent / "Phase1_9_26" / "01_HUMAN_LABEL_ACQUISITION"
PY = r"D:\speech-lab\venvs\p0\Scripts\python.exe"
TMP = PHASE / "artifacts" / "_qa_synth"
OUT = TMP / "out"


def main():
    pack = list(csv.DictReader(open(LAB26 / "REVIEW_CANDIDATES.csv", encoding="utf-8")))[:6]
    TMP.mkdir(parents=True, exist_ok=True)
    for rev, labels in (("QA-SYN-A", ["PRESENT", "ABSENT", "PRESENT", "UNCERTAIN",
                                      "PRESENT", "ABSENT"]),
                        ("QA-SYN-B", ["PRESENT", "PRESENT", "PRESENT", "UNCERTAIN",
                                      "ABSENT", "ABSENT"])):
        with open(TMP / f"{rev}.jsonl", "w", encoding="utf-8") as f:
            for r, lab in zip(pack, labels):
                f.write(json.dumps({"reviewer_id": rev, "blind_id": r["blind_id"],
                                    "label": lab, "confidence": "HIGH",
                                    "assessable": "ASSESSABLE",
                                    "note": "SYNTHETIC QA TEST", "ts": "qa"}) + "\n")
    subprocess.run([PY, str(HERE / "label_analysis.py"), "--reviews", str(TMP),
                    "--out", str(OUT)], check=True)
    res = list(csv.DictReader(open(OUT / "HUMAN_LABEL_RESULTS.csv", encoding="utf-8")))
    agr = list(csv.DictReader(open(OUT / "REVIEWER_AGREEMENT.csv", encoding="utf-8")))
    adj = list(csv.DictReader(open(OUT / "ADJUDICATION_RESULTS.csv", encoding="utf-8")))
    gates = json.loads((OUT / "label_sufficiency.json").read_text(encoding="utf-8"))
    ok = (len(res) == 6 and len(agr) >= 2 and len(adj) >= 1
          and gates["n_reviewers"] == 2
          and gates["LABELS_SUFFICIENT_FOR_TYPE_B"] is False
          and gates["R_LABELS_SUFFICIENT"] is False)
    print(f"cases={len(res)} agreement_rows={len(agr)} adjudication={len(adj)} "
          f"typeb_gate={gates['LABELS_SUFFICIENT_FOR_TYPE_B']} "
          f"r_gate={gates['R_LABELS_SUFFICIENT']}")
    print("LABEL_PIPELINE_TEST", "PASS" if ok else "FAIL")
    shutil.rmtree(TMP)
    print("synthetic QA files deleted; no labels fabricated")
    print("DONE test_label_analysis")


if __name__ == "__main__":
    main()
