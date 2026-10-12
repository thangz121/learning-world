# 01 — EXPERT LABELS (WP-1.9.31)

> **Tóm tắt (VI):** Nhãn ngoài được sinh máy móc từ 5 chuyên gia độc lập của speechocean762 cho
> 3 tập: pilot 546 token (541P/5A), bench train 3.823 token (3.789P/34A), bench test 4.326 token
> (4.232P/94A). KHÔNG token nào lỗi ánh xạ (0/8.695). Nhãn phụ: strict (P2>=3), any_zero (>=1
> chuyên gia chấm 0). /r/ cuối từ: 224 token toàn bench (220P/4A; strict 218/6). Không có /r/ trong
> pilot 546.

- **Nguồn:** `D:\speech-lab\data\speechocean762\resource\scores-detail.json`
  sha256 `8A90FF40558FE8032D9BFA919429DCAE267C0EC3FBDEDF1DCA30C0515EA989E1`
- **Quy tắc (đã khóa trước readout, xem `00_PREREGISTRATION.md` §3):** bỏ `[insertions]`; mỗi vị trí
  ref đọc dấu của 5 chuyên gia (không dấu = 2; `{}`=1; `()`=0); PRESENT iff P2+P1>=3; ABSENT iff A0>=3.
- **Script:** `experiments/p31_labels.py` (chạy lại được, tất định).
- **Artifacts:** `artifacts/labels/expert_labels_pilot.csv`, `_bench_train.csv`, `_bench_test.csv`,
  `label_coverage.json`.

## Counts

| set | speakers | tokens | PRESENT | ABSENT | any_zero | strict_correct | malformed |
|---|---|---:|---:|---:|---:|---:|---:|
| PILOT (frozen 546) | 10 | 546 | 541 | 5 | 46 | 535 | 0 |
| PILOT train | 6 | 337 | 333 | 4 | 23 | — | 0 |
| PILOT dev | 2 | 110 | 110 | 0 | 6 | — | 0 |
| PILOT test | 2 | 99 | 98 | 1 | 17 | — | 0 |
| BENCH train | 58 | 3,823 | 3,789 | 34 | 301 | 3,741 | 0 |
| BENCH test | 64 | 4,326 | 4,232 | 94 | 403 | 4,200 | 0 |

- `any_zero` = có ít nhất 1 chuyên gia chấm 0/không phát âm (nguồn ABSENT mềm, chỉ báo cáo).
- `strict_correct` = P2>=3 (phát âm đúng hoàn toàn theo đa số); phần còn lại gồm MIXED/ABSENT0.
- /r/ cuối từ (bench, cả hai split): 224 token — produced 220P/4A trên 91 speakers; strict 218/6.
- Pilot 546: **không có token /r/** (điều này khớp thiết kế cũ: /r/ nằm ở Pack R, không ở pilot).

## Ánh xạ (kiểm chứng)

- `p31_probe` giai đoạn khảo sát + lần chạy thật: chuỗi ghi chú của 5 chuyên gia sau khi bỏ `[]`
  khớp 100% thứ tự ref-phones (0 mismatch trên 8.695 token).
- Bẫy đã xử lý: câu nguồn `ref-phones` giữ nguyên cả stress digits ("AE1"); canonical hóa qua
  production inventory (`arpa_seq_to_canon`) giống pipeline đóng băng.
- Giới hạn đã ghi trong prereg: điểm 0 gộp "sai" và "mất" (không tách deletion thuần), nên nhãn
  ABSENT = "not acceptably produced" chứ không phải "deleted".

## Ghi chú phương pháp

- 5 chuyên gia chấm **độc lập** theo README của corpus — đây là nguồn nhãn người thật thay thế
  vòng review REV-B theo chỉ đạo PO; các phiên REV-A vẫn lưu trữ nguyên trạng.
- Nhãn thô per-expert nằm trong các CSV (cột `m1..m5`) để tái phân tích.
