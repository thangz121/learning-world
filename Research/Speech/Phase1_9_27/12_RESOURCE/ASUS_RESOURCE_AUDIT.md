# ASUS RESOURCE AUDIT — WP-1.9.27 Part 19

> **Tóm tắt (VI):** ASUS chỉ CPU, 16 GB RAM. Đo thực trong WP này: trích features production 546
> token = 200 s; encoder thay thế 546 token = 169 s; chạy chung 2 model trong 1 tiến trình bị lỗi
> bộ nhớ (0xC0000005) → phải chạy tuần tự. Đĩa: venv 1,94 GB; HF cache 10,5 GB; SO762 0,67 GB;
> frame cache 16,6 MB. Kết luận: mọi thí nghiệm pilot hiện tại chạy được trên CPU local.

## Measured (2026-10-07, this machine)

| item | value |
|---|---|
| CPU-only | no CUDA device used by any stage |
| RAM | 16 GB (combined two-encoder process crashed with 0xC0000005 = memory pressure) |
| prod features (546 tokens) | 200 s (~0.37 s/token, includes parselmouth formants) |
| alt features (546 tokens) | 169 s (~0.31 s/token) |
| pilot feature table | 118 KB CSV (546 rows x 30 columns) |
| venv p0 | 1.94 GB |
| HuggingFace cache (local models) | 10.50 GB |
| speechocean762 local data | 0.67 GB |
| frame cache (WP-1.9.21) | 16.6 MB (2,436 token-windows / 117,128 frames) |
| Phase1_9_27 artifacts | 627 KB |
| Research/Speech total | 1.90 GB |

## Operational constraints

- Never load both encoders in one process (memory fault). The runner already splits
  `features_prod` / `features_alt`; keep that discipline.
- parselmouth formant extraction contributes a material part of the prod runtime; it is optional and
  guarded (empty values when span too short).
- Everything above runs locally today: no GPU, no cloud, no new downloads.

## What this audit proves

The pilot's CPU-only steps (features, gate sweeps, low-capacity B2-E models) are feasible on ASUS.
Only encoder fine-tuning (B2-C) and large-corpus adaptation require a future GPU.
