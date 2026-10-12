# 09 — FINAL GO/NO-GO (WP-1.9.31)

**FINAL GATE: `B2_PILOT_PROMISING_DATA_REQUIRED`**

> **Tóm tắt (VI):** Lần đầu tiên toàn bộ pipeline đóng băng chạy được hết trên thước đo ngoài
> (5 chuyên gia + trẻ bản ngữ). D5 (OR bằng chứng hai encoder) đạt cả 4 tiêu chí số học đóng băng
> ⇒ gate "promising": chỉ mở bước QUYẾT ĐỊNH DỮ LIỆU/LICENSE cho B2 đầy đủ — KHÔNG huấn luyện,
> KHÔNG đổi production, KHÔNG Unity. Các mặt còn thiếu được ghi nguyên trạng.

## Evidence summary

| item | result |
|---|---|
| Label source | SO762 5-expert panel (CC BY 4.0) + Zenodo native kids (CC BY 4.0), local, 0 downloads |
| BENCH test | 4,326 tokens / 64 trẻ (4,232P / 94A) — đủ ngưỡng >=30/side |
| Baseline (production decision) | FRR 8.65% · missing-evidence 16.54% · FAR **54.26%** · false-evidence 13.83% |
| Best variant **D5_or_max** | FRR **7.68%** · missing **12.19%** (−26.3% tương đối) · FAR 42.55% · AUC **0.8546** |
| Frozen criteria c1–c4 (D5or) | PASS tất cả (c4: cả 2 speaker pilot test tốt hơn) |
| c5 /r/ (produced subset) | PASS hướng tốt (FRR 3.36%→0.84%); phía ABSENT n=1 (không kết luận) |
| c6 assessability | N/A — nhãn ngoài không có lớp NOT_ASSESSABLE (khai báo trước) |
| c7 random control | N/A — không có biến thể fit chạy được (train+dev ABSENT=34<60, luật đóng băng) |
| Pilot 546 (continuity) | test 98P/1A: D0 FRR 22.45% → D5or 15.31%; cả 2 speaker tốt lên |
| Native ~4–5t (107 tokens) | 94/107 accepted (FRR hướng 12.15%); /v/ yếu nhất (3/6) |
| TYPE-B | 3/4 ca SO762 có hội đồng chuyên gia (2 khớp REV-A, 1 ngược); 1 ca Zenodo còn label-limited |
| Agreement (5 experts) | raw 88.9–89.9% / binary 96.3–96.6%; unanimous 5/5 76–78% |
| Locks | B2 training NO · production UNCHANGED · Unity NONE · REV-B not needed (PO directive) |

## Diễn giải được phép / không được phép

- **Được phép:** bước tiếp theo là một quyết định phạm vi dữ liệu/license cho full B2 (theo kill
  switch: "a positive result authorizes only a data/licence decision for full B2"), và thiết kế lại
  acceptance-rule/điểm vận hành (vì FAR baseline 54% là vấn đề trung tâm còn nguyên).
- **Không được phép:** huấn luyện, thay model, thay scorer/VAD/router, tích hợp Unity, viết ngưỡng
  vào sản phẩm, tự chốt "đạt sản phẩm".

## Open items (nguyên trạng, không tô hồng)

1. FAR tuyệt đối còn cao (D5or 42.6%): cần bài toán "điểm vận hành" có kiểm soát FAR; hiện ngưỡng
   matched-FAR chạm sàn 0.001 do FAR baseline quá lớn — thiết kế tiếp theo nên khóa FAR mục tiêu
   TRƯỚC, rồi tối thiểu FRR (FRR-first đúng nghĩa hơn).
2. /r/ phía ABSENT không với tới được bằng corpus hiện có (4/224); cần nguồn lâm sàng (PERCEPT-R,
   non-commercial) nếu muốn gate /r/ đầy đủ.
3. TYPE-B ca Zenodo `child_07_seven` không có nhãn chuyên gia (corpus không có phone labels).
4. assessability (c6) không kiểm chứng được với nguồn nhãn ngoài — đã khai báo trước readout.
5. Kết quả "promising" này KHÔNG áp dụng cho tiếng Việt-L1 (không có corpus công khai) — giới hạn
   kế thừa nguyên vẹn.
