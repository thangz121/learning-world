# 05 — PILOT READOUT (frozen 546, continuity) — WP-1.9.31

> **Tóm tắt (VI):** Đọc lại pilot đóng băng bằng chuẩn ngoài (541P/5A): baseline production bỏ sót
> bằng chứng 34.7% ở test split; quy tắc OR-hai-encoder (D5) giảm còn 24.5% và FRR 22.4%→15.3%;
> cả 2 speaker test đều tốt lên. FAR phía ABSENT vô nghĩa vì test chỉ có 1 token ABSENT — số liệu
> phụ lục, không dùng để chốt gate (bench mới là tập chính).

- **Script:** `experiments/p31_eval.py`; **artifact:** `artifacts/eval/eval_readout.json` (mục `pilot`).

## Frozen split readout (external labels)

| split | n | D0 FRR | D0 missing | D0 FAR | D5or FRR | D5or missing | D5or FAR |
|---|---:|---:|---:|---:|---:|---:|---:|
| train | 333P/4A | 5.71% | 12.61% | 75.0% | 6.61% | 9.31% | 50.0% |
| dev | 110P/0A | 19.09% | 34.55% | — | 14.55% | 24.55% | — |
| **test** | **98P/1A** | **22.45%** | **34.69%** | 100% (1/1) | **15.31%** | **24.49%** | 0% (0/1) |

- Cả hai speaker test (1075, 1076) đều cải thiện FRR: 15.7%→7.8% và 29.8%→23.4%.
- Per-class test (missing D0→D5or): stop 16.7→10.0; fricative 57.7→46.2; nasal 30.3→15.2;
  liquid 37.5→37.5; affricate (n rất nhỏ) 100→100.
- Ngưỡng matched-FAR chọn trên train (D5or = 0.001 — sàn lưới, xem cảnh báo trong B2-D report).

## So sánh với lịch sử

- Con số pilot "presents thiếu bằng chứng" (~30%+) khớp với phát hiện WP-1.9.24 (87/528 max_A<0.02)
  và xấu hơn bench tổng thể — pilot được chọn từ các speaker/nhóm khó hơn.
- REV-A/S2 (nhãn người 1 reviewer) vẫn lưu trữ; phiên bản nhãn ngoài này **không** dùng để đánh giá
  lại REV-A, chỉ thay nguồn thước đo theo chỉ đạo PO.

## Giới hạn

- 1 token ABSENT ở test ⇒ mọi chỉ số FAR của pilot là minh họa.
- Đây là sub-readout kế thừa; tập đọc chính của WP-1.9.31 là BENCH (§06).
