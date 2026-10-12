# 02 — EXPERT AGREEMENT (WP-1.9.31)

> **Tóm tắt (VI):** Đồng thuận 5 chuyên gia (đo thật thay cho dòng `SINGLE_REVIEWER_LIMITATION`):
> raw agreement 3 mức ~88.9–89.9%, nhị phân ({2,1} vs 0) ~96.3–96.6%; Fleiss kappa 3 mức 0.16–0.25,
> nhị phân 0.21–0.45; tỉ lệ nhất trí toàn bộ 5/5 là 76–78%. Kappa thấp do trội "đúng" (prevalence
> paradox) — raw agreement mới là số quan trọng ở đây.

- **Script:** `experiments/p31_agreement.py`; **artifact:** `artifacts/agreement/expert_agreement.json`.

## Results

| set | n | unanimous 5/5 | 3-way raw | 3-way Fleiss | binary raw | binary Fleiss |
|---|---:|---:|---:|---:|---:|---:|
| pilot | 546 | 76.2% | 0.8886 | 0.1585 | 0.9634 | 0.2120 |
| bench train | 3,823 | 78.0% | 0.8959 | 0.1585 | 0.9660 | 0.2451 |
| bench test | 4,326 | 78.2% | 0.8991 | 0.2549 | 0.9627 | 0.4469 |

- 3-way = 3 mức {2,1,0}; binary = {produced (2 hoặc 1) vs 0}.
- Pairwise Cohen kappa trung bình (3-way/binary) nằm trong cùng khoảng Fleiss (chi tiết trong JSON).
- Diễn giải: các bất đồng chủ yếu là "2 vs {1}" (đúng vs đúng-nhưng-nặng-giọng) — ảnh hưởng nhỏ
  đến nhãn nhị phân vì cả hai đều là PRESENT; nhãn ABSENT gần như luôn đạt 4–5/5.

## Ý nghĩa cho gate agreement cũ

- Gate cũ (inter-rater kappa giữa 2 reviewer) giờ có thể ước lượng từ 5 chuyên gia trên chính dữ
  liệu SO762. Không còn `SINGLE_REVIEWER_LIMITATION` cho tập đánh giá ngoài.
