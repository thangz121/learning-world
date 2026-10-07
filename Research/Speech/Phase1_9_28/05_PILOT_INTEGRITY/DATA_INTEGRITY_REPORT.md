# DATA INTEGRITY REPORT - WP-1.9.28 Part 10

> **Tóm tắt (VI):** Tất cả kiểm tra toàn vẹn pilot PASS: split speaker-disjoint 0 overlap; manifest
> 200/200 PASS; 546/546 token khớp features; audio 546/546 tồn tại; không có trường label/machine
> trong manifest. Kết luận: PILOT_INTEGRITY = PASS (dữ liệu sẵn sàng; chỉ chờ nhãn).

## Executed checks (2026-10-07)

| check | method | result |
|---|---|---|
| frozen split validation | `pilot_runner.py --stage validate` | rows 200, PASS 200; overlaps 0/0/0 |
| leakage suite | `pilot_checks.py` | `leakage_pass = true` (see `LEAKAGE_REPORT.md`) |
| token/feature correspondence | Pack P vs `pilot_features.csv` token_id sets | **546/546 match** |
| pack audio resolve | exists() over `PILOT_REVIEW_CANDIDATES.csv` references | 546/546 present, 0 missing |
| manifest integrity | row count + QC status + columns | 200 rows, 200 PASS, no label/machine fields |
| duplicate audio paths | `pilot_checks.py` | 0 |
| duplicate speaker+text | `pilot_checks.py` | 0 |
| utterance overlap train/test | `pilot_checks.py` | 0 |
| session overlap | SO762 subset is single-session per speaker; session key absent; covered by speaker disjointness (documented) | PASS (by speaker rule) |
| label leakage into features | feature columns are machine evidence only; labels absent | PASS |
| machine-score leakage into manifest | no max_a/evidence fields in manifest | PASS |
| alignment leakage | no alignment-truth columns enter labels or features; alignment is AUTOMATIC evidence only | PASS |

## Frozen asset hashes

All inputs hashed in `01_FREEZE_CHECK/FROZEN_CONFIG_HASHES.txt`; notably
`pilot_features.csv` 118094 bytes and Pack P 89644 bytes.

## Verdict

`PILOT_INTEGRITY = PASS`. No `PILOT_BLOCKED_DATA_INTEGRITY` condition. The pilot is blocked only by
the absence of human labels, which is a human dependency, not an integrity failure.
