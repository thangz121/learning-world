"""WP-1.9.28 Part 30 — priority batch for the next human review round.

Reads `02_REVIEW_IMPORT/PACK_COMBINED.csv` (Pack R + Pack P) and writes
`14_DECISION/NEXT_REVIEW_BATCH.csv` with an explicit priority and rationale per
candidate. No labels, no fabrication — this is the exact candidate list reviewers
must process, in priority order.

Priority logic (frozen before any label exists):
  P0  Pack R pool F (TYPE-B decisive) and Pack P test split (pilot evaluation set)
  P1  Pack R /r/ pools A/B/C and weak/P0-support pools D/L; Pack P dev split
  P2  Pack R balanced/isolated/encoder-disagreement pools E/G/H/I/J/K; Pack P train split
  P3  Pack R random control M, consistency N, SIAK O
"""
from __future__ import annotations

import csv
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE.parent
PACK = OUT / "02_REVIEW_IMPORT" / "PACK_COMBINED.csv"
TARGET = OUT / "14_DECISION" / "NEXT_REVIEW_BATCH.csv"
FIELDS = ["priority", "pack", "group", "blind_id", "token_id", "word", "target_phone",
          "speaker_id", "rationale"]

R_P0 = {"F_typeb_strong_false"}
R_P1 = {"A_r_present", "B_r_absent", "C_r_uncertain", "D_weak_present",
        "L_low_score_true_present"}
R_P2 = {"E_weak_absent", "G_isolated_peak", "H_encoder_disagreement",
        "I_final_consonant_failure", "J_high_score_false_accept", "K_balanced_review"}
P_P0 = {"test"}
P_P1 = {"dev"}

RATIONALE = {
    "F_typeb_strong_false": "TYPE-B decisive: 4 unresolved encoder-vs-label cases",
    "A_r_present": "/r/ gate: PRESENT side (15 required)",
    "B_r_absent": "/r/ gate: ABSENT side (15 required)",
    "C_r_uncertain": "/r/ gate: unlabelled /r/ cases",
    "D_weak_present": "weak-PRESENT separation (weak true vs weak false)",
    "L_low_score_true_present": "missing-evidence mechanism (low evidence, true present)",
    "E_weak_absent": "weak-ABSENT control",
    "G_isolated_peak": "isolated-peak mechanism",
    "H_encoder_disagreement": "representation-dependence check",
    "I_final_consonant_failure": "final-consonant failure replay",
    "J_high_score_false_accept": "false-accept mechanism",
    "K_balanced_review": "balanced phone-class x truth baseline",
    "M_random_control": "random control: representativeness vs diagnostics",
    "N_consistency_control": "reliability control (historical labels re-inserted)",
    "O_siak_age46_broad": "SIAK age 4-6 broad pool (score-only data)",
    "test": "pilot evaluation set (frozen test split; 99 tokens)",
    "dev": "pilot sufficiency/fitting set (frozen dev split; 110 tokens)",
    "train": "pilot sufficiency/fitting set (frozen train split; 337 tokens)",
}


def priority(pack, group):
    if pack.startswith("REVIEW_CANDIDATES"):
        if group in R_P0:
            return "P0"
        if group in R_P1:
            return "P1"
        if group in R_P2:
            return "P2"
        return "P3"
    if group in P_P0:
        return "P0"
    if group in P_P1:
        return "P1"
    return "P2"


def main():
    rows = list(csv.DictReader(open(PACK, encoding="utf-8")))
    out = []
    for r in rows:
        p = priority(r["pack"], r["group"])
        out.append({"priority": p, "pack": r["pack"], "group": r["group"],
                    "blind_id": r["blind_id"], "token_id": r["token_id"],
                    "word": r["word"], "target_phone": r["target_phone"],
                    "speaker_id": r["speaker_id"],
                    "rationale": RATIONALE.get(r["group"], "scientific control")})
    out.sort(key=lambda x: (x["priority"], x["pack"], x["blind_id"]))
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    with open(TARGET, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=FIELDS)
        w.writeheader()
        w.writerows(out)
    from collections import Counter
    print("rows:", len(out), "by priority:", dict(Counter(x["priority"] for x in out)))
    print(f"wrote {TARGET}")
    print("DONE build_next_review_batch")


if __name__ == "__main__":
    main()
