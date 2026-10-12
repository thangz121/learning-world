# 04 — BENCH INTEGRITY (WP-1.9.31)

> **Tóm tắt (VI):** Trích xuất features cho 2.440 utterance (bench test 1.280 + bench train 1.160)
> + 200 utterance pilot decisions bằng đúng code trích xuất của pipeline đóng băng. Số token ra
> khớp 100% số nhãn (4.326 / 3.823 / 546). Utterance lỗi chỉ là `no_focus` (0 token mất).
> Tái chạy pilot trên máy mới lệch tối đa 3.1e-5 (sai số float32 khác CPU) — các trường dùng cho
> quyết định (top1_phone, target_rank, cluster_width, longest_run_05, rms_ratio) trùng khít.

- **Script:** `experiments/p31_features.py` (checkpoint từng utterance; chạy lại bỏ qua utterance đã xong).
- **Artifacts:** `artifacts/features/bench_{train,test}_features.csv`, `pilot_decisions_features.csv`.

## Coverage

| set | utts | tokens extracted | label rows | token loss | error utts |
|---|---:|---:|---:|---:|---:|
| bench_test | 1,280 | 4,326 | 4,326 | 0 | 10 (`no_focus`) |
| bench_train | 1,160 | 3,823 | 3,823 | 0 | 6 (`no_focus`) |
| pilot_decisions | 200 | 546 | 546 | 0 | 3 (`no_focus`) |

`no_focus` = utterance có 0 phụ âm cuối theo luật token (không phải lỗi dữ liệu).

## Determinism vs the frozen ASUS extraction (pilot 546)

| field | max abs diff |
|---|---:|
| target_mean | 1.1e-05 |
| target_max | 3.1e-05 |
| alt_max | 5.0e-06 |
| alt_mean | 1.0e-06 |

- `top1_phone`, `target_rank`, `cluster_width`, `longest_run_05`, `rms_ratio`: 0 khác biệt.
- Nguyên nhân: đường tính float32 khác CPU giữa máy ASUS (đóng băng) và máy mới; không ảnh hưởng
  kết luận (ngưỡng 0.02/0.30 cách xa sai số). Ghi nhận trung thực trong báo cáo B2-D.

## Decision addendum

- `pilot_decisions_features.csv` chứa quyết định production dựng lại chính xác
  (`L.baseline_final_decision`: top-5 span-mean + similarity + posterior), 546/546 token có quyết định.
- Bench features mang sẵn trường quyết định (cùng code) — D0 baseline của bench đọc trực tiếp.

## Provenance hashes (recorded in `artifacts/eval/eval_readout.json`)

| input | sha256 |
|---|---|
| scores-detail.json | `8A90FF40…E1` |
| pilot manifest | `0A0D9B1F…5C` |
| pilot_features.csv (frozen) | `5F96EC60…BE` |
| expert_labels_pilot | `B7670234…4F` |
| expert_labels_bench_train | `2606F272…C8` |
| expert_labels_bench_test | `810148C8…52` |
| bench_train_features | `A03EB91E…B0` |
| bench_test_features | `E85676B9…A7` |

## Runtime (CPU-only, machine TECHVKITCHEN01)

- bench_test ~1,036 s; bench_train ~1,015 s; pilot_decisions ~145 s (xlsr-53 + lv-60 + formants).
- Không huấn luyện; không thay đổi production; không Unity.
