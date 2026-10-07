# HUMAN REVIEW STATUS — WP-1.9.27

> **Tóm tắt (VI):** 0 nhãn mới (không reviewer trong phiên; không tạo nhãn giả). Hai pack đã sẵn
> sàng mù: Pack R 276 ca / 49 speaker / 15 pool (QA 21/21 PASS) và Pack P 546 token pilot / 10
> speaker (server đã phục vụ, submit 200, QA submission đã xoá). Hành động người là bước duy nhất
> còn lại.

## Packs

| pack | candidates | speakers | status |
|---|---|---|---|
| Pack R (276) | 276 (15 pools A–O) | 49 | server + payload + isolation + resume + export QA: 21/21 PASS; hashes recorded |
| Pack P (546) | 546 (train 337 / dev 110 / test 99) | 10 frozen pilot speakers | generated; served blind (546 candidates, blind payload verified); token ids match features 546/546 |

## Labels

- **NEW_LABELS_COLLECTED = 0.** No reviewer was available in-session; no labels fabricated; no
  machine prediction promoted to a label.
- `HUMAN_LABEL_RESULTS.csv` = empty schema; agreement/adjudication files = empty; sufficiency gates
  false (0/4 TYPE-B; 0/15 PRESENT and 0/15 ABSENT /r/).
- Historical labels unchanged: 28 LWE listening labels (16 PRESENT / 12 ABSENT; 23 HIGH, 3 MEDIUM,
  2 LOW; single reviewer).

## Exact human action (the only remaining blocker for the label gate)

1. Start the server: `python serve_review_1927.py 8791` (Pack R).
2. Two reviewers (REV-A, REV-B) label independently; minimum 60 PRESENT + 60 ABSENT, 15+15 /r/,
   all 4 TYPE-B; the pack is deliberately larger (276) so the minimum is reachable.
3. Export CSV per reviewer; import:
   `python label_analysis.py --csv REV-A=revA.csv --csv REV-B=revB.csv`.
4. For the pilot: same server with
   `--pack 06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv` (Pack P, 546 tokens) -> pilot labels.
5. Adjudicate DISAGREEMENT/UNCERTAIN only after raw labels are frozen.

## Limitations to keep reporting

- If only one reviewer completes: `SINGLE_REVIEWER_LIMITATION` (no kappa; no TYPE-B decisive claim).
- Score-derived labels (SO762/SIAK) never count as listening labels.
- NOT_ASSESSABLE is not ABSENT and is reported separately.
