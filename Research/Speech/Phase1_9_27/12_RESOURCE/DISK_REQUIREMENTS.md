# DISK REQUIREMENTS — WP-1.9.27 Part 19

> **Tóm tắt (VI):** Ước lượng đĩa cho pilot và route tương lai: pilot gần như không tốn đĩa
> (features 118 KB; toàn bộ Phase 1.9.27 627 KB). Tài sản local hiện có: venv 1,94 GB, HF cache
> 10,5 GB, SO762 0,67 GB, frame cache 16,6 MB. Dữ liệu tương lai ước lượng theo range, chưa tải.

## Measured today (ASUS)

| item | size |
|---|---|
| Phase1_9_27 (docs + scripts + artifacts + features) | 627 KB |
| pilot feature tables (3 CSVs) | ~240 KB |
| frame cache (reusable) | 16.6 MB |
| speechocean762 local data | 0.67 GB |
| venv p0 (Python + torch + transformers) | 1.94 GB |
| HuggingFace cache (local encoders) | 10.50 GB |
| Research/Speech total | 1.90 GB |

## Estimates for future routes (not downloaded)

| route | estimated disk | basis |
|---|---|---|
| OCSC audio+transcripts | 1–5 GB | 303 speakers, task battery (estimate) |
| JIBO Kids | 2–4 GB | 21 h wav + metadata (estimate) |
| AusKidTalk annotated | 5–15 GB | 136.6 h (estimate, 16 kHz) |
| MyST FLAC | 30–50 GB | ~470 h FLAC 16 kHz (estimate) |
| PERCEPT-R | 2–5 GB | 32.5 h + labels (estimate) |
| Vietnamese-L1 30–50 children protocol | 5–15 GB | 2–4 h per child worst case (estimate) |
| Generated alignments (10–30 h subset) | < 1 GB | text/grid outputs |

## Rules

- Estimates are labeled estimates; actual sizes verified at download time only after rights.
- Pilot work (labels + features + B2-D/E readout) requires no new disk beyond measured values.
- Do not download restricted corpora before licence clearance (see `11_DATA_ROADMAP/`).
