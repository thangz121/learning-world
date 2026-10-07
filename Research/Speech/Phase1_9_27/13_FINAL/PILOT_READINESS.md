# PILOT READINESS — WP-1.9.27

> **Tóm tắt (VI):** Pilot READY về mọi mặt tự động: dữ liệu đóng băng 10 speaker/200 utt/546 token;
> features 2 encoder đã trích; Pack P 546 ca; runner 5 stage + training hard-off; leakage PASS;
> thiết kế B2-D/B2-E + tiêu chí + kill switch khóa trước kết quả; resource CPU đo thực. Chỉ còn
> nhãn người (Pack P, tối thiểu 60P+60A) là điều kiện mở khóa đánh giá.

## Readiness matrix

| element | artifact | status |
|---|---|---|
| frozen data contract | `06_PILOT_DATA/PILOT_DATA_CONTRACT.md` + manifest | READY (200/200 PASS, 0 exclusion) |
| frozen split | `artifacts/pilot_split.json` (6/2/2, seed 1927) | READY; leakage PASS |
| features | `artifacts/pilot_features.csv` (546 x 30) | READY (prod 200 s + alt 169 s) |
| pilot review pack | `06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv` (546) | READY; served blind; 546/546 token match |
| label pipeline | `label_analysis.py` (+ test PASS) | READY (0 labels) |
| runner | `pilot_runner.py` validate/features/eval/report | features DONE; eval/report await labels |
| B2-D design | `07_B2_D/` | FROZEN |
| B2-E design | `08_B2_E/` | FROZEN |
| success/failure/kill | `09_SUCCESS_CRITERIA/` | FROZEN before results |
| reproduction | `10_REPRODUCIBILITY/` | READY |
| resource | `12_RESOURCE/` | measured; CPU sufficient |
| license | local-only pilot | READY (no licence needed) |

## Blocking dependencies for eval (exact)

1. Pack P labels: minimum 60 PRESENT + 60 ABSENT across the 546 pilot tokens (>=2 reviewers, or an
   explicit single-reviewer limitation).
2. /r/ coverage: Pack R 15+15 + Pack P 8 tokens; /r/ claims stay directional until the Pack R gate
   clears.
3. Nothing else. No training, no licence, no GPU, no production change.

## What "starting the pilot" means after labels arrive

1. `label_analysis.py --reviews artifacts/reviews --pack 06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv`
2. Implement/run the eval stage using the frozen configs (D0–D5, BASE/E_*) — the runner prints a
   STOP today because labels are absent, by design.
3. Run report (per-speaker, /r/, assessability, failure replay); apply the frozen criteria; record
   SUCCESS / PARTIAL / FAILURE / INCONCLUSIVE.

## Explicit non-claims

The pilot cannot establish production readiness, cannot cover Vietnamese-L1, and cannot justify
encoder fine-tuning by itself.
