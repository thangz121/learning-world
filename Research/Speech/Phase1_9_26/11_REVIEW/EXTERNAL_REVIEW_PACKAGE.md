# EXTERNAL REVIEW PACKAGE — WP-1.9.26 Part 26

> **Tóm tắt (VI):** Gói bàn giao cho reviewer ngoài: server mù + pack 276 ứng viên (diagnostic /
> balanced / random control / SIAK 4–6), README riêng, protocol, xuất JSON/CSV, không lộ machine
> prediction, không thấy nhau. Mục tiêu: 60P+60A nhãn tối thiểu, ưu tiên /r/ và TYPE-B.

## What to hand over

1. `01_HUMAN_LABEL_ACQUISITION/REVIEWER_README.md` — reviewer instructions.
2. `01_HUMAN_LABEL_ACQUISITION/LABELING_PROTOCOL.md` — full definitions.
3. `experiments/serve_review_1926.py` — blind server (run on the lab machine).
4. `01_HUMAN_LABEL_ACQUISITION/REVIEW_CANDIDATES.csv` — coordinator copy (contains machine fields;
   keep away from reviewers).
5. The audio files referenced in the CSV (local; do not redistribute).

## Reviewer setup

- 1–3 reviewers (A, B, optional C), each with a distinct reviewer ID.
- Each reviewer opens `http://<lab-ip>:8791`, labels independently; progress auto-saves; export
  JSON/CSV at the end.
- Reviewers never see machine predictions, pools, or each other's labels.
- Priority order for effort allocation: P0 TYPE-B (pool F), P1 /r/ (A–C) and weak present (D),
  P2 balanced/random control.

## Minimum outcome

- 60 PRESENT + 60 ABSENT (≥20 speakers) for the minimum scientific set.
- 15 PRESENT + 15 ABSENT /r/.
- 4 TYPE-B labels.
- ≥2 reviewers → Cohen's kappa; otherwise SINGLE_REVIEWER_LIMITATION.

## Analysis after return

- `experiments/reviewer_agreement.py` (to be run on the returned JSONL files) computes raw
  agreement, Cohen's/Fleiss' kappa, confidence-stratified and /r/-specific agreement.
- Labels populate `HUMAN_LABEL_RESULTS.csv`; disagreements are preserved, not majority-voted.
