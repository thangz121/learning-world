# 07 — B2-E REPORT (hybrid, fixed thresholds, no fitting) — WP-1.9.31

> **Tóm tắt (VI):** Các biến thể B2-E có FIT **không chạy được** theo luật đóng băng (train+dev chỉ
> có 34 ABSENT < 60). Thay vào đó báo cáo đường cong ngưỡng CỐ ĐỊNH (không fit) cho các nhóm đặc
> trưng chính. Không nhóm đặc trưng đơn lẻ nào đạt FRR-first tốt hơn D5: encoder posterior (E_ENC)
> là tín hiệu chính; rms/temporal một mình quá yếu (FAR ~75–94% ở ngưỡng thấp).

- **Script:** `experiments/p31_eval.py` (mục `test_fixed_grid`); artifact cùng file JSON.
- **Lý do không fit (ghi trước readout):** `B2_E_DESIGN.md` yêu cầu >=60P+60A trong train+dev để fit;
  thực đo: 34 A ⇒ fitted variants = NOT EVALUATED/INCONCLUSIVE by construction. Random-control N/A.

## Fixed-threshold curves — BENCH test (FRR | FAR), τ giống nhau cho mọi nhóm

| τ | E_ENC target_max | E_ACO rms_ratio | E_TMP temporal_support | E_ENC∧ACO |
|---:|---|---|---|---|
| 0.02 | 16.54% \| 22.34% | 2.27% \| 93.62% | 18.01% \| 20.21% | 16.54% \| 22.34% |
| 0.05 | 19.80% \| 19.15% | 5.86% \| 81.91% | 21.60% \| 18.09% | 20.16% \| 19.15% |
| 0.10 | 22.57% \| 18.09% | 8.88% \| 76.60% | 24.93% \| 15.96% | 24.43% \| 18.09% |
| 0.15 | 24.72% \| 18.09% | 11.37% \| 68.09% | 27.55% \| 14.89% | 28.36% \| 18.09% |
| 0.30 | 28.88% \| 13.83% | 20.23% \| 56.38% | 35.11% \| 12.77% | 39.04% \| 13.83% |

- (missing-evidence/false-evidence theo định nghĩa đóng băng là HẰNG SỐ trên ngưỡng quyết định —
  xem JSON: E_ENC 16.54%/13.83%; E_ACO 2.27%/56.38%; E_TMP 18.01%/12.77%.)
- Đọc FRR-first: để giữ FRR <= baseline+5pp (13.65%), E_ENC phải ở τ≈0.02; ở đó FAR 22.3% —
  tốt hơn hẳn FAR baseline 54.3%, nhưng vẫn chưa đủ "đúng" cho sản phẩm; E_TMP tương tự.
- **So sánh chuẩn với B2-D:** không biến thể nhóm đơn nào vượt D5or (missing 12.19%). Cơ chế nổi
  trội = posterior (giữ), acoustic/temporal chỉ hỗ trợ.
- Pilot test fixed grid (tham khảo, n=98P/1A): E_ENC τ=0.02 → FRR 34.7% (cao — pilot khó hơn);
  xu hướng giống hệt bench.

## Kết luận B2-E

- Không thể kết luận gì về model fit (đúng luật: dữ liệu ABSENT không đủ). Đường cong cố định xác
  nhận: tín hiệu posterior chiếm ưu thế; hybrid không thể hiện lợi thế rõ trong chế độ no-fit.
- Không huấn luyện, không xuất model, không viết ngưỡng về production.
