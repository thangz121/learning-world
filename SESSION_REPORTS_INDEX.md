# SESSION REPORTS — INDEX & CONVENTION

> **Quy ước (VI):** Mỗi lần làm việc (phase / work package) tạo **đúng 1 file báo cáo** dạng
> Markdown ở **thư mục gốc repo**, tên `SESSION_REPORT_<ID>.md`, chứa toàn bộ thông tin kỹ thuật
> của lần làm việc đó. File phase-report trong `Research/Speech/...` vẫn là nguồn chi tiết; file
> session report là bản tổng hợp cấp phiên (mission → thực thi → metrics → quyết định → git → artifacts).

## Convention

- Naming: `SESSION_REPORT_<ID>.md` (ví dụ `SESSION_REPORT_1_9_19.md`).
- Each report MUST contain:
  1. Header: date, machine, base commit, branch, scope, production flags.
  2. Mission / objectives.
  3. Work performed per sub-task (scripts, data, commands).
  4. Exact metrics / measured results (numbers, not adjectives).
  5. Decisions / gates.
  6. Files created/modified + artifact paths.
  7. Git actions (commit hashes, pushes) or explicit "no commit".
  8. Open items / next steps.
- Never include secrets (HF tokens, keys) or raw child audio in these files.
- Update this index when a new session report is added.

## Reports

| session | date | title | decision / gate | commit |
|---|---|---|---|---|
| 1.9.14 | 2026-10-03 | Fidelity/assessability + deletion-aware pronunciation | B = RESEARCH_SUPPORTS_PARTIAL_CHANGE | `d594821` |
| 1.9.15 | 2026-10-03 | Population calibration + independent child validation | B = CALIBRATION_PROMISING_BUT_INSUFFICIENT | `4412c2f`, `c362558`, `019249f`, `3f62ef4` |
| 1.9.16 | 2026-10-03 | Child phone model adaptation research (B1) | B = ADAPTATION_PROMISING_BUT_INSUFFICIENT | `e8d96da` |
| 1.9.17 | 2026-10-03 | Deletion / alignment diagnostic | GATE A (alignment/deletion primary); `SPEECH_RESEARCH_REQUIRES_ALIGNMENT_WORK` | `c611724` |
| 1.9.18 | 2026-10-03 | Deletion-aware evidence & decision layer | ALIGNMENT_DELETION_GATE_FAIL | `c611724` |
| 1.9.19 | 2026-10-03 | Boundary / window causality audit | MIXED_WINDOW_AND_ACOUSTIC; B2 NOT YET | (uncommitted) |

## Current speech-research status

```
Speech Research: NOT COMPLETE
Last gate:       MIXED_WINDOW_AND_ACOUSTIC (WP-1.9.19)
Next:            alignment/span repair + confidently-labeled present finals
                 (esp. /r/) under a fixed boundary protocol, then re-assess B2
Production:      untouched (production_vad=false, router_locked=false,
                 unity_integrated=false, scorer_modified=false,
                 production_window_locked=false)
```
