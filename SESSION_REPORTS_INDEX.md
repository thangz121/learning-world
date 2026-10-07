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
- **Standing instruction (from WP-1.9.20):** on completing a work package, commit and push
  automatically to the working branch and `main` (fast-forward), including the new
  `SESSION_REPORT_<ID>.md` and this index update — do not ask first.

## Reports

| session | date | title | decision / gate | commit |
|---|---|---|---|---|
| 1.9.14 | 2026-10-03 | Fidelity/assessability + deletion-aware pronunciation | B = RESEARCH_SUPPORTS_PARTIAL_CHANGE | `d594821` |
| 1.9.15 | 2026-10-03 | Population calibration + independent child validation | B = CALIBRATION_PROMISING_BUT_INSUFFICIENT | `4412c2f`, `c362558`, `019249f`, `3f62ef4` |
| 1.9.16 | 2026-10-03 | Child phone model adaptation research (B1) | B = ADAPTATION_PROMISING_BUT_INSUFFICIENT | `e8d96da` |
| 1.9.17 | 2026-10-03 | Deletion / alignment diagnostic | GATE A (alignment/deletion primary); `SPEECH_RESEARCH_REQUIRES_ALIGNMENT_WORK` | `c611724` |
| 1.9.18 | 2026-10-03 | Deletion-aware evidence & decision layer | ALIGNMENT_DELETION_GATE_FAIL | `c611724` |
| 1.9.19 | 2026-10-03 | Boundary / window causality audit | MIXED_WINDOW_AND_ACOUSTIC; B2 NOT YET | `6fc7158` |
| 1.9.20 | 2026-10-03 | Alignment representation audit & counterfactual realignment | ALIGNMENT_REPRESENTATION_FAIL; B2 NOT YET | `39df310` |
| 1.9.21 | 2026-10-07 | Temporally plausible support aggregation + label sufficiency audit | SUPPORT_AGGREGATION_FAIL; B2 NOT YET | `60a55ab` |
| 1.9.22 | 2026-10-07 | Acceptance / identity decision audit | ACCEPTANCE_LOGIC_AND_ENCODER_BOTH_REQUIRED; next gate ACCEPTANCE_RULE_REDESIGN_REQUIRED; B2 NOT READY | `d7ef0ff` |
| 1.9.23 | 2026-10-07 | Acceptance rule redesign + falsification + label pack | ACCEPTANCE_RULE_AND_ENCODER_BOTH_LIMITING; next gate LABEL_EXPANSION_AND_ENCODER_DESIGN_REQUIRED; B2 NOT READY | (this commit) |

## Current speech-research status

```
Speech Research: NOT COMPLETE
Last gate:       LABEL_EXPANSION_AND_ENCODER_DESIGN_REQUIRED (WP-1.9.23)
Final status:    ACCEPTANCE_RULE_AND_ENCODER_BOTH_LIMITING
Next:            human review of the 23-candidate label pack (10-20 confident
                 PRESENT finals, /r/ first) + design-only encoder-evidence study
                 (weak finals, strong false peaks); no B2 training
Production:      untouched (production_vad=false, router_locked=false,
                 unity_integrated=false, scorer_modified=false,
                 production_window_locked=false)
Frame cache:     CREATED — Research/Speech/Phase1_9_21/artifacts/frame_cache
                 (2,436 token-windows / 117,128 frames, reusable)
B2:              NOT READY (0/68 acceptance rules FRR-first safe; weak false accepts
                 not materially reduced; labels insufficient; no training)
Labels:          NEW_LABELS_COLLECTED = 0; 23-candidate pack READY_FOR_HUMAN_REVIEW
```
