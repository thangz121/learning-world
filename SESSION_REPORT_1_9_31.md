# SESSION REPORT - WP-1.9.31

> **Tóm tắt (VI):** Theo chỉ đạo PO, thay thước đo review người thứ hai (REV-B) bằng CHUẨN TRẺ NƯỚC NGOÀI:
> 5 chuyên gia SO762 + trẻ bản ngữ Zenodo (~4.9y), cả hai CC BY 4.0, local, 0 download. Đã khóa
> preregistration trước readout (commit `ddeb700`), sinh nhãn máy móc cho 8.695 token (0 lỗi), đo
> agreement 5 chuyên gia, chạy lại toàn bộ pilot/B2-D/B2-E bằng nhãn ngoài + bench mới 8.149 token.
> D5 (OR hai encoder) đạt cả 4 tiêu chí số học đóng băng ⇒ gate **B2_PILOT_PROMISING_DATA_REQUIRED**
> (chỉ mở quyết định dữ liệu/license; KHÔNG huấn luyện/không production/không Unity). Baseline
> production nhận sai 54.3% token chuyên gia chấm không đạt — phát hiện định lượng trung tâm.

- **DATE:** 2026-10-12
- **MACHINE:** new machine TECHVKITCHEN01 (fresh clone 2026-10-10; venv `D:\speech-lab\venvs\p0`, Python 3.14.8)
- **BRANCH:** `main` (= `ux/math-arenas-hotfix-20260930`)
- **BASE COMMIT:** `0e43eae` (WP-1.9.30 migration package, clone-first)
- **PRE-REG COMMIT:** `ddeb700` (before any readout)
- **FINAL COMMIT:** this commit
- **CURRENT GATE:** `B2_PILOT_PROMISING_DATA_REQUIRED` (new, external-standard readout)
- **PRODUCTION FLAGS:** untouched (production_vad=false · router_locked=false · unity_integrated=false ·
  scorer_modified=false · production_window_locked=false) · **TRAINING: NO**

## Mission

Per the product owner directive (2026-10-12): complete the speech track using foreign children's
standard productions as the measuring stick, replacing the owner-as-reviewer / second-reviewer round.
No thresholds weakened; new experiment with its own pre-registration (WP-1.9.27 §8 semantics).

## Work performed

1. Migration re-verified on the new machine: `pilot_runner --stage validate` PASS (200/200, leakage 0);
   `infra_health_1929` PASS (19/19 hashes, server, mapping). Unity EditMode suite on this machine
   (10-10 evidence) = 725/0/5.
2. Pre-registration written + committed BEFORE readout (`00_PREREGISTRATION.md`, `ddeb700`).
3. Label derivation (`p31_labels.py`): 5-expert marks → frozen binary rule; pilot 546 (541P/5A),
   bench train 3,823 (3,789/34), bench test 4,326 (4,232/94); 0 malformed; /r/ 224 (220/4; strict 218/6).
4. Agreement (`p31_agreement.py`): raw 3-way 88.9–89.9%, binary 96.3–96.6%; Fleiss 0.16–0.45 (prevalence);
   unanimous 76–78%. Replaces SINGLE_REVIEWER_LIMITATION.
5. Sufficiency (`p31_sufficiency.py`): BENCH meets >=60P/>=60A/>=30-side; /r/ absent-side unreachable;
   TYPE-B 3/4 expert-labeled (2 agree REV-A, 1 diverges), 1 Zenodo case label-limited.
6. Feature extraction (`p31_features.py`, CPU ~37 min): bench test 1,280 utts/4,326 tokens;
   bench train 1,160/3,823; pilot decisions 546/546 — token coverage exact, no_focus utts only;
   determinism vs frozen ASUS extraction: max abs diff 3.1e-5 (float32 CPU path), classification identical.
7. Readout (`p31_eval.py`, one pass): baseline/D1/D3/D4/D5_and/D5_or at dev-matched FAR; per-speaker,
   per-class, per-phone, age bands, /r/, failure replay, fixed-grid B2-E curves, criteria mapping.
8. Native check (`p31_native.py`): 6 native children (~4.9y), 107 tokens, 94 accepted (FRR 12.15%),
   /v/ weakest; /r/ excluded (non-rhotic).
9. Reports 01–09 + handoff + index update; commit + push.

## Metrics (key numbers)

- BENCH test baseline: FRR 8.65% · missing-evidence 16.54% · FAR 54.26% · false-evidence 13.83%.
- **D5_or_max: FRR 7.68% · missing 12.19% (−26.3% rel) · FAR 42.55% · AUC 0.8546** — frozen c1–c4 PASS.
- Pilot test (98P/1A): FRR 22.45%→15.31% (both speakers improve). Native ~4–5y: 94/107 accepted.
- Per-speaker bench (D5or vs D0): better 31 / worse 18 / tie 15 (of 64). Age 6–7 band: FRR 13.08→10.91.
- Matched-FAR thresholds hit the grid floor 0.001 (baseline FAR too high) — disclosed; full-train
  calibration sensitivity identical; no test tuning.

## Decisions / gates

- **`B2_PILOT_PROMISING_DATA_REQUIRED`** — authorizes only the next data/license decision for full B2
  (kill-switch boundary). Not production, not training.
- REV-B round superseded by PO directive; REV-A artifacts archived unchanged.
- /r/ absent-side gate documented as unreachable with available corpora (no reclassification).
- assessability criterion N/A (external labels have no NOT_ASSESSABLE class) — pre-declared.

## Files created (speech)

- `Research/Speech/Phase1_9_31/00_PREREGISTRATION.md`
- `Research/Speech/Phase1_9_31/01_EXPERT_LABELS/EXPERT_LABELS_REPORT.md`
- `Research/Speech/Phase1_9_31/02_AGREEMENT/AGREEMENT_REPORT.md`
- `Research/Speech/Phase1_9_31/03_SUFFICIENCY/LABEL_SUFFICIENCY_REPORT.md`
- `Research/Speech/Phase1_9_31/04_BENCH/BENCH_INTEGRITY.md`
- `Research/Speech/Phase1_9_31/05_PILOT/PILOT_READOUT.md`
- `Research/Speech/Phase1_9_31/06_B2_D/B2_D_REPORT.md`
- `Research/Speech/Phase1_9_31/07_B2_E/B2_E_REPORT.md`
- `Research/Speech/Phase1_9_31/08_NATIVE/NATIVE_CHECK_REPORT.md`
- `Research/Speech/Phase1_9_31/09_FINAL/FINAL_GO_NO_GO.md`
- `Research/Speech/Phase1_9_31/experiments/p31_{common,labels,agreement,sufficiency,features,eval,native}.py`
- `Research/Speech/Phase1_9_31/artifacts/**` (labels 8,695; features 8,695+546; eval JSON+CSV; native)
- `Research/Speech/HANDOFF_1_9_31.md`

## Git actions

- `ddeb700` pre-registration (pushed before readout).
- This commit: all WP-1.9.31 code, artifacts, reports, handoff, index update — pushed to `main`
  (fast-forward) per standing instruction.
- Untouched (by design): all frozen 1.9.x artifacts; production code; the 3 pre-existing Unity-side
  working-tree modifications (not part of this WP — left as-is).

## Open items / next steps

1. Data/license decision for full B2 (MyST paid vs research registrations vs local-only) — the only
   action this gate authorizes.
2. Acceptance-rule / operating-point redesign (lock target FAR first, then minimize FRR); D5 OR-evidence
   family is the current best candidate on the external standard.
3. /r/ absent-side via clinical corpora (PERCEPT-R, non-commercial) — future decision.
4. TYPE-B Zenodo case label-limited; Vietnamese-L1 generalization still out of scope (no public corpus).
