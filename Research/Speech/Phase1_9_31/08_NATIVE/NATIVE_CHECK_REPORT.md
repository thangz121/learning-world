# 08 — NATIVE-CHILD REFERENCE (Zenodo 200495, ~4–5 tuổi) — WP-1.9.31

> **Tóm tắt (VI):** 6 trẻ BẢN NGỮ Anh (~4.9 tuổi, CC BY 4.0) nói số 1–10 và 5 câu cố định:
> 94/107 token được production chấp nhận (FRR hướng ~12.2%); thiếu bằng chứng 21.5%. /v/ yếu nhất
> (3/6); /d/ có bằng chứng thấp bất thường nhưng vẫn được nhận nhờ nhánh similarity. Đây là tham
> chiếu cận tuổi mục tiêu (4–6), KHÔNG phải gate; /r/ bị loại (non-rhotic).

- **Script:** `experiments/p31_native.py`;
  **artifacts:** `artifacts/native/native_check.csv`, `native_check_summary.json`.
- **Nguồn:** `english_children` (Zenodo 200495, CC BY 4.0) — 6 speaker `*_native`, mic studio (fallback port/nao);
  44.1k → 16k qua `load_mono16` (cache ngoài git).

## Summary

| metric | value |
|---|---:|
| tokens | 107 |
| speakers | 6 (age ~4–5; dataset M=4.9) |
| accepted | 94 (FRR hướng 12.15%) |
| missing-evidence (max<0.02) | 21.5% |
| median target_max | 0.519 |

| phone | n | accepted | median target_max | ghi chú |
|---|---:|---:|---:|---|
| n | 22 | 21 | 0.675 | rất ổn |
| s | 23 | 22 | 0.604 | ổn (có thể đang phát triển ở 4–5t) |
| t | 13 | 11 | 0.700 | 2 bị từ chối |
| ɡ | 13 | 11 | 0.631 | 2 bị từ chối dù evidence cao (nhánh quyết định) |
| d | 12 | 11 | **0.041** | evidence rất thấp nhưng vẫn nhận (similarity path) |
| v | 6 | **3** | 0.227 | yếu nhất |
| p | 6 | 5 | 0.880 | |
| ʃ | 6 | 5 | 0.419 | |
| l | 6 | 5 | 0.158 | thấp |

## Đọc số liệu (trung thực)

- Với trẻ bản ngữ ~5 tuổi, quyết định production vẫn từ chối ~12% phụ âm cuối "đúng chuẩn" —
  nhất quán hướng với bench (FRR 8.65%) và pilot test (22.5%, khó hơn).
- Cảnh báo phát triển ngôn ngữ: /v/, /l/, /s/ còn đang hoàn thiện ở tuổi này; một phần "từ chối"
  có thể hợp lệ — vì vậy đây là tham chiếu hướng, không phải gate.
- 4 file số bị thiếu mic trong bản gốc (six×2, one×2) — dataset missing combos, không phải lỗi pipeline.
- Toàn bộ /r/ bị loại trước khi đo (UK non-rhotic: không được giả định "four/car" có /r/ cuối).
