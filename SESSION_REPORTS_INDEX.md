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
| 1.9.23 | 2026-10-07 | Acceptance rule redesign + falsification + label pack | ACCEPTANCE_RULE_AND_ENCODER_BOTH_LIMITING; next gate LABEL_EXPANSION_AND_ENCODER_DESIGN_REQUIRED; B2 NOT READY | `6770de9` |
| 1.9.24 | 2026-10-07 | Human label expansion + encoder evidence design audit | LABELS_INSUFFICIENT_ENCODER_HYPOTHESIS_SUPPORTED; next gate LABEL_COLLECTION_AND_DATA_LICENSE_GATE; B2 NO TRAINING / DESIGN NOT READY | `59b6afe` |
| 1.9.25 | 2026-10-07 | Label collection + data/license gate | LABELS_INSUFFICIENT_DATA_LICENSE_BLOCKED; B2 TRAINING NO; next gate LABEL_COLLECTION_AND_DATA_ACQUISITION | `8ba65d2` |
| 1.9.26 | 2026-10-07 | Research blocker breakout: label pipeline, encoder audit, data/license discovery | LABEL_COLLECTION_READY_DATA_BLOCKED; next gate HUMAN_REVIEW_ROUND_AND_LOCAL_PILOT; B2 TRAINING NO | `5dfdc6c` |
| 1.9.27 | 2026-10-07 | Human review execution pipeline + local child-speech pilot + B2-D/B2-E readiness | HUMAN_REVIEW_PENDING_PILOT_READY; next gate HUMAN_REVIEW_ROUND_AND_PILOT_EVALUATION; B2 TRAINING NO | (this commit) |

## Current speech-research status

```
Speech Research: NOT COMPLETE
Last gate:       HUMAN_REVIEW_ROUND_AND_PILOT_EVALUATION (WP-1.9.27)
Final gate:      HUMAN_REVIEW_PENDING_PILOT_READY
Next:            2 reviewers on Pack R (276 candidates: 60P+60A minimum, 15+15 /r/,
                 4 TYPE-B) AND Pack P (546 pilot tokens, 60P+60A) -> import CSV ->
                 pilot evaluation (B2-D/B2-E, FRR-first, frozen criteria);
                 parallel: TalkBank registration, PERCEPT-R request, MyST quote,
                 counsel questions, JIBO license confirmation
Production:      untouched (production_vad=false, router_locked=false,
                 unity_integrated=false, scorer_modified=false,
                 production_window_locked=false)
Frame cache:     CREATED — Research/Speech/Phase1_9_21/artifacts/frame_cache
                 (2,436 token-windows / 117,128 frames, reusable)
Pilot:           DATA+FEATURES READY — 10 frozen speaker-disjoint speakers (6/2/2,
                 seed 1927), 200 utts, 546 final-consonant tokens, 0.196 h,
                 QC 200/200 PASS, leakage PASS; features extracted for production
                 (200 s) and alternative (169 s) encoders; runner hard-disables
                 training; eval awaits Pack P labels
B2:              TRAINING NO; B2-D/B2-E DESIGNS + SUCCESS/FAILURE/KILL CRITERIA
                 FROZEN before results. Dominant blocker remains human labels;
                 data blocker unchanged (phone labels at scale).
Labels:          NEW_LABELS_COLLECTED = 0; two blind packs ready (R 276 / P 546);
                 pipeline validated (test PASS); 28 historical LWE listening labels
                 (single reviewer); /r/ = 1 PRESENT LOW
Data:            28 datasets audited; age-4-7 research routes OCSC 303/JIBO 110/
                 AusKidTalk 620/CAPIL 30; MyST paid commercial route (DO_NOT_BUY
                 until pilot positive + quote); speechocean762 CC BY 4.0; SIAK ND
                 legal review open; no public Vietnamese-L1 child corpus
Encoder:         609-token comparison: missing 16.5% vs 18.8%; false 17.2% vs
                 13.8%; agreement 82.8% (no aggregate winner; diagnostic only)
Resource:        CPU-only ASUS sufficient for pilot (venv 1.94 GB; HF cache
                 10.50 GB; SO762 0.67 GB; frame cache 16.6 MB)
```
