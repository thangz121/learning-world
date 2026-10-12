# 06 — B2-D REPORT (encoder/aggregation comparison, external standard)

> **Tóm tắt (VI):** Trên BENCH test (4.232P/94A, trẻ 6–15, 64 speaker): baseline production quyết
> định ĐÚNG 7.68–8.65% là sai (FRR) nhưng NHẬN SAI 54.3% token bị chuyên gia chấm không đạt (FAR).
> Biến thể D5 (OR bằng chứng max của 2 encoder) tốt nhất: AUC 0.855; thiếu-bằng-chứng giảm
> 16.54%→12.19% (giảm tương đối 26.3%) — đạt ngưỡng 25%; FRR 8.65%→7.68%; FAR 54.3%→42.6%.
> Bốn tiêu chí số học đóng băng (c1–c4) đều đạt cho D5 ⇒ gate kết quả: `B2_PILOT_PROMISING_DATA_REQUIRED`.

- **Script:** `experiments/p31_eval.py`; **artifact:** `artifacts/eval/eval_readout.json`.
- **Định nghĩa (đóng băng):** FRR = tỉ lệ PRESENT bị quyết định từ chối; missing-evidence =
  PRESENT có score < 0.02; FAR = tỉ lệ ABSENT được nhận; false-evidence = ABSENT có score >= 0.30.

## Main table — BENCH test (readout, một lần)

| variant | thr (dev-matched) | FRR | missing-evidence | FAR | false-evidence | AUC |
|---|---|---:|---:|---:|---:|---:|
| **D0 production decision (baseline)** | — | 8.65% | 16.54% | 54.26% | 13.83% | — |
| D1 prod span-max | 0.001 | 10.54% | 16.54% | 37.23% | 13.83% | 0.8412 |
| D3 alt span-max | 0.001 | 16.56% | 22.35% | 30.85% | 13.83% | 0.8209 |
| D4 alt span-mean | 0.015 | 32.61% | 36.39% | 12.77% | 0.00% | 0.8230 |
| **D5 OR= max(prod_max, alt_max)** | 0.001 | **7.68%** | **12.19%** | 42.55% | 18.09% | **0.8546** |
| D5 AND= min(prod_max, alt_max) | 0.001 | 19.42% | 26.70% | 25.53% | 9.57% | 0.8187 |

## Criteria evaluation (frozen numbers, unchanged)

| criterion | D5_or | D1 | D3 | D4 | D5_and |
|---|---|---|---|---|---|
| c1 FRR <= baseline+0.05 | PASS | pass | fail | fail | fail |
| c2 missing-evidence giảm >=25% tương đối | **PASS (−26.3%)** | fail (0%) | fail | fail | fail |
| c3 FAR <= baseline+0.05 | PASS | pass | pass | pass | pass |
| c4 đúng dấu ở cả 2 speaker pilot test | **PASS** (cả 2 tốt hơn) | fail | fail | pass | n/a |
| c6 assessability | N/A (nhãn ngoài không có lớp NOT_ASSESSABLE — đã khai báo) | | | | |
| c7 random control | N/A (không có biến thể fit nào chạy được: train+dev ABSENT=34<60) | | | | |

## Chi tiết quan trọng (không tô hồng)

- **D5or là quy tắc rất "dễ tính"** (nhận nếu bất kỳ encoder có bằng chứng ≥0.001): nó tốt hơn
  baseline ở CẢ HAI phía (FRR 7.68<8.65 và FAR 42.6<54.3) trên bench — nhưng FAR tuyệt đối vẫn cao;
  kết quả này nói "encoder OR bù bằng chứng (missing-evidence giảm thật)", KHÔNG nói "đem D5 vào sản phẩm".
- **Ngưỡng matched-FAR chạm sàn lưới 0.001** vì FAR của baseline trên dev quá cao (0.559) — thiết kế
  FRR-first "khớp FAR baseline" trở nên lỏng. Đã chạy sensitivity hiệu chuẩn trên toàn train (post-hoc,
  ghi rõ trong JSON): kết quả y hệt (sàn 0.001). Không tuning trên test.
- **Per-speaker (64 speaker test):** D5or tốt hơn 31, xấu hơn 18, hòa 15 — net tốt nhưng phân bố hai
  chiều. Per-speaker table: `artifacts/eval/per_speaker.csv`.
- **Theo nhóm tuổi (missing/FAR D0→D5or):** 6–7t FRR 13.08→10.91, FAR 51.9→35.2 (n=1246P/54A);
  8–10t 6.79→5.77 (n=1178P/5A); 11–15t 6.80→6.69 (n=1808P/35A).
- **Theo lớp (missing D0→D5or):** stop 12.42→8.26; fricative 20.13→15.66; nasal 16.20→11.39;
  liquid 20.73→16.81; affricate 41.51→39.60 — cải thiện rải khắp nhóm, có cơ chế (bù bằng chứng
  chéo encoder), không phải một phone đơn lẻ (thỏa tinh thần tiêu chí 8).
- **/r/ (produced subset test, 119P/1A):** D0 FRR 3.36% / missing 4.2% → D5or 0.84% / 1.68%;
  phía ABSENT 1 token không kết luận được. Criterion 5 (/r/ không xấu đi): PASS (thực tế tốt hơn).

## Kết luận B2-D

- Giả thuyết "representation khác bù missing-evidence" được **hỗ trợ một phần có ý nghĩa**: D5 (OR)
  là biến thể duy nhất đạt ngưỡng giảm 25% và cũng là AUC cao nhất; hai encoder bù nhau.
- Baseline production thực sự yếu ở phía FAR (nhận 54% token chuyên gia chấm sai/mất) — đây là
  phát hiện định lượng mới ở quy mô 4.326 token, độc lập với REV-A.
- Không có thay đổi production/threshold nào được viết ngược.
