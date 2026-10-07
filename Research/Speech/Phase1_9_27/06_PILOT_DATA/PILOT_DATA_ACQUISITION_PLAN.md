# PILOT DATA ACQUISITION PLAN — WP-1.9.27

> **Tóm tắt (VI):** Pilot local không cần license mới và không cần thu thập mới: dùng 10 speaker
> SO762 sẵn có + 546 token + 2 pack review. Việc còn lại là hành động người (nhãn) và (sau pilot)
> quyết định route dữ liệu lớn: OCSC/JIBO research, MyST thương mại, SIAK sau pháp lý, PERCEPT-R
> cho /r/, thu thập Việt-L1 dài hạn.

## What the pilot needs (all already available)

| need | status | artifact |
|---|---|---|
| audio (10 speakers, 200 utts) | ready | SO762 local subset; manifest frozen |
| machine features (546 tokens) | ready | `artifacts/pilot_features.csv` |
| blind review tooling | ready | `serve_review_1927.py` (Pack P supported) |
| label analysis | ready | `label_analysis.py --pack 06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv` |
| leakage checks | PASS | `pilot_checks.json` |
| success criteria / kill switch | frozen | `09_SUCCESS_CRITERIA/` |
| B2-D / B2-E designs | frozen | `07_B2_D/`, `08_B2_E/` |

## Not needed for the pilot

- No licence decision, no payment, no GPU, no new recordings, no production change.
- SIAK is not used for pilot modeling (CC-BY-ND unresolved); it appears only as review pool O.

## After the pilot (data routes, ranked — details in `11_DATA_ROADMAP/`)

1. Label round on local data (this WP's primary action; no licence).
2. If pilot is positive: OCSC/JIBO research registration (age 4–7; research-only) and/or the MyST
   commercial license (470 h, lexicon; phone labels generated + human-verified).
3. SIAK legal review and PERCEPT-R research benchmark run in parallel; Vietnamese-L1 collection is
   the only route that solves all blockers simultaneously (long-term partnership).

Do not purchase or download anything restricted before the pilot reads out; no license-unclear data
has been downloaded to date.
