# /R/ REPORT - WP-1.9.28 Part 17

> **Tóm tắt (VI):** Không có nhãn /r/ mới → `R_INCONCLUSIVE`. Bảng kết quả chỉ có schema. Cảnh báo
> nguồn cung cục bộ (32 ứng viên cho 30 nhãn cần) đã ghi ở `04_LABEL_GATE/R_LABEL_GATE.md`.

## Status

`R_INCONCLUSIVE` - 0 PRESENT / 0 ABSENT consensus labels; no /r/ claim is permitted.

## What will be reported when labels exist (`R_RESULTS.csv`)

Per /r/ case: token, speaker, word, human label + confidence, production max_A, alt max_A, temporal
support, isolated-peak flag, F1/F2/F3 + F3/F2, model decision, gain/loss.

Summary metrics: PRESENT recall, ABSENT rejection, FPR, FNR, human confidence distribution,
isolated-peak rate, alternative-encoder agreement.

## Constraints

- 8 pilot /r/ tokens + 24 Pack R /r/ candidates = 32 total local supply vs 30 required labels; a
  skewed consensus can make the gate unreachable locally (documented).
- PERCEPT-R (research-only, non-commercial) is the external option; negotiate before use.
- No /r/ detector may be built; this remains diagnostic.
