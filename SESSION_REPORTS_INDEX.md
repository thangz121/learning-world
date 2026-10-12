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
| 1.9.27 | 2026-10-07 | Human review execution pipeline + local child-speech pilot + B2-D/B2-E readiness | HUMAN_REVIEW_PENDING_PILOT_READY; next gate HUMAN_REVIEW_ROUND_AND_PILOT_EVALUATION; B2 TRAINING NO | `dfdb82d` |
| 1.9.28 | 2026-10-07 | Human review gate + frozen pilot execution attempt (PATH A: no review data) | HUMAN_REVIEW_PENDING_PILOT_READY; next gate HUMAN_REVIEW_ROUND_AND_PILOT_EVALUATION; B2 TRAINING NO | `e53aa30` |
| 1.9.29 | 2026-10-07 | Human review execution -> frozen pilot -> B2 GO/NO-GO (PATH B: still no genuine labels) | HUMAN_REVIEW_PENDING_PILOT_READY; next gate HUMAN_REVIEW_ROUND_AND_PILOT_EVALUATION; B2 TRAINING NO | `3c0d811` |
| 1.9.30 | 2026-10-07 | Human label import -> agreement -> freeze -> frozen pilot -> B2 decision (PATH C: no genuine labels) | HUMAN_REVIEW_PENDING_PILOT_READY; next gate HUMAN_REVIEW_ROUND_AND_PILOT_EVALUATION; B2 TRAINING NO | `c8ad68a` |
| 1.9.31 | 2026-10-12 | External child standard (SO762 5-expert panel + Zenodo native kids, CC BY 4.0) replaces REV-B; BENCH 8,149 tokens; B2-D readout | B2_PILOT_PROMISING_DATA_REQUIRED (D5_or passes frozen c1–c4); B2 TRAINING NO | `fcd4725` |
| 1.9.32 | 2026-10-12 | Free TTS pipeline (Piper + LibriTTS-R, self-checked 16/16) + English→Listening game foundation "Vườn Tai Thính" (`listen_garden`) | IMPLEMENTED (not accepted); suite 739/734/0/5; build Succeeded errors=0; offline audio pinned | this commit |

## Current speech-research status

```
Speech Research: EXTERNAL-STANDARD PILOT READOUT COMPLETE (WP-1.9.31)
Last gate:       B2_PILOT_PROMISING_DATA_REQUIRED (WP-1.9.31)
Label source:    EXTERNAL CHILD STANDARD per PO directive 2026-10-12 — SO762 five-expert panel
                 (per-phone 2/{ }/( ), CC BY 4.0; children 6-15) + Zenodo 200495 native children
                 (~age 4.9, CC BY 4.0), all local, zero downloads. REV-B round superseded;
                 REV-A sessions + frozen 1.9.27-1.9.30 artifacts archived unchanged.
BENCH:           test 4,326 tokens / 64 children (4,232P / 94A); train 3,823 (3,789P / 34A);
                 mapping 0/8,695 malformed; agreement raw 88.9-89.9% (binary 96.3-96.6%),
                 unanimous 5/5 76-78%.
Baseline:        production decision FRR 8.65% · missing-evidence 16.54% · FAR 54.26% ·
                 false-evidence 13.83% (BENCH test) — over-acceptance is the central issue.
Best variant:    D5_or_max (prod/alt span-max OR): FRR 7.68% · missing 12.19% (-26.3% rel) ·
                 FAR 42.55% · AUC 0.8546; frozen criteria c1-c4 PASS (both pilot test speakers
                 improve: 15.7->7.8%, 29.8->23.4%). Thresholds hit grid floor 0.001 (baseline FAR
                 too high) — disclosed; full-train calibration sensitivity identical.
Native ref:      6 native children ~4.9y: 94/107 accepted (FRR-directional 12.15%); /v/ 3/6.
Pilot:           546 continuity readout (541P/5A): test 98P/1A D0 FRR 22.45% -> D5or 15.31%.
Type-B:          3/4 SO762 cases expert-labeled (014180143_15 ABSENT, 014190172_7 ABSENT vs REV-A
                 PRESENT divergence, 014350146_16 ABSENT); child_07_seven (Zenodo) label-limited.
/r/:             produced 220P/4A/91spk (absent-side gate unreachable); strict rhotic 218/6.
Next:            (1) data/license decision for full B2 — the ONLY thing this gate authorizes;
                 (2) acceptance-rule / operating-point redesign (lock target FAR first, minimize FRR;
                 D5 OR-evidence family is the best candidate); (3) PERCEPT-R route for /r/ absent side.
Production:      untouched (production_vad=false, router_locked=false, unity_integrated=false,
                 scorer_modified=false, production_window_locked=false)
Training:        NO (kill switch intact; positive result authorizes data/license decision only)
Product (English→Listening): FOUNDATION IMPLEMENTED (WP-1.9.32) — game `listen_garden`
                 "Vườn Tai Thính": LV1→LV4 (12 rounds/visit), 8 starter words, voice-first,
                 bell replay, wrong→slow replay; audio offline 100% (16 local Piper masters,
                 voice listen_v1, keys pinned by CT-S21F); suite 739/734/0/5; build errors=0.
                 Next: human/visual QA + journey driver + content phase 2 (add words via
                 Content/listening/listen_garden.json + tools/pregen_listen.py).
```
