# HUMAN REVIEW PENDING - WP-1.9.28 Part 3

> **Tóm tắt (VI):** Hành động người duy nhất còn thiếu: 2 reviewer dán nhãn Pack R (276) và
> Pack P (546) bằng server mù đã QA. Tối thiểu: 60P+60A; 15+15 /r/; 4 TYPE-B; 60P+60A pilot.
> Không có nhãn nào được tạo tự động.

## Exact reviewer action required

1. On the lab machine (ASUS):
   ```
   python Research/Speech/Phase1_9_27/experiments/serve_review_1927.py 8791
   ```
   Reviewer A and B open `http://<lab-machine>:8791` and use IDs `REV-A` / `REV-B`.
2. Label Pack R (276) independently. Priority (P0 -> P3) is in
   `14_DECISION/NEXT_REVIEW_BATCH.csv`; P0 = TYPE-B cases + pilot test tokens.
3. For the pilot labels run the same server with Pack P:
   ```
   python Research/Speech/Phase1_9_27/experiments/serve_review_1927.py 8791 \
     --pack Research/Speech/Phase1_9_27/06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv
   ```
   (Use a different `--reviews` dir if you want to keep rounds separate, e.g.
   `--reviews artifacts/reviews_pilot`.)
4. Export CSV per reviewer and send to the coordinator.
5. Coordinator imports exactly as in `02_REVIEW_IMPORT/REVIEW_IMPORT_REPORT.md`.

## Minimum scientific sets (frozen)

| set | requirement |
|---|---|
| overall | 60 PRESENT + 60 ABSENT, >=20 speakers |
| /r/ | 15 PRESENT + 15 ABSENT consensus, >=10 speakers (local supply: 32 candidates — see risk note) |
| TYPE-B | all 4 decisive cases with >=2 independent non-UNCERTAIN consensus |
| pilot | 60 PRESENT + 60 ABSENT among the 546 Pack P tokens; valid labels for the 99-token test split |

## Rules

- Do not reveal machine predictions, pool, or other reviewers' labels.
- UNCERTAIN is legitimate; do not force ABSENT.
- No majority vote; disagreements preserved; adjudication only after raw freeze.
- `/r/` local supply risk: if consensus splits unfavorably, the 15+15 target may require PERCEPT-R
  (research-only) — report, do not lower the gate.

## After import

`label_analysis.py` (combined pack) recomputes consensus/agreement/gates; if the pilot minimum is
met, execute the frozen evaluation and apply the kill switch mechanically.
