# CPU / GPU REQUIREMENTS — WP-1.9.27 Part 19

> **Tóm tắt (VI):** Phân loại từng thí nghiệm: LOCAL_CPU (chạy ngay trên ASUS), LOCAL_GPU_REQUIRED
> (không có), FUTURE_GPU_REQUIRED (B2-C adaptation). Không cài model lớn không cần thiết.

| experiment | class | hardware | notes |
|---|---|---|---|
| Review server + label analysis | LOCAL_CPU | ASUS | HTTP server + CSV/JSONL; trivial |
| Pilot feature extraction (prod + alt) | LOCAL_CPU | ASUS | measured 200 s + 169 s for 546 tokens; run sequentially |
| B2-D encoder comparison readout | LOCAL_CPU | ASUS | features already extracted; pure table analysis |
| B2-E hybrid (logistic / shallow GBM) | LOCAL_CPU | ASUS | <= 546 x ~25 features; seconds |
| Random-feature control | LOCAL_CPU | ASUS | same |
| Calibration / gate sweeps | LOCAL_CPU | ASUS | seconds |
| Per-speaker/failure-replay reports | LOCAL_CPU | ASUS | reporting only |
| Larger-corpus diagnostics (OCSC/JIBO after rights) | LOCAL_CPU | ASUS | hours; batch inference; disk-bound |
| Encoder fine-tuning B2-C (2–4 h pilot) | FUTURE_GPU_REQUIRED | GPU (cloud) | not approved; needs licensed/cleared data |
| Full adaptation (20–40 h) | FUTURE_GPU_REQUIRED | GPU (cloud) | after a positive pilot + signed route |
| Vietnamese-L1 model work | FUTURE_GPU_REQUIRED | GPU (cloud) | requires collection first |

## Rules

- Do not install large models or corpora for experiments not yet approved.
- Do not assume GPU availability anywhere in the pilot design.
- Any future GPU work is a separate, explicitly-approved work package with a data/licence decision
  attached (`09_SUCCESS_CRITERIA/KILL_SWITCH.md`).
