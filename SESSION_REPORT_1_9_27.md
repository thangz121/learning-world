# SESSION REPORT — WP-1.9.27 (HUMAN REVIEW EXECUTION PIPELINE + LOCAL PILOT + B2-D/B2-E READINESS)

> **Tóm tắt (VI):** WP chuẩn bị thi hành review ngoài và pilot local: QA lại server mù (21/21), đóng
> gói reviewer ngoài, pipeline nhãn (test PASS), hợp đồng dữ liệu pilot 10 speaker/200 utt/546 token
> (leakage PASS), trích features 2 encoder (200 s + 169 s), Pack P 546 ca phục vụ mù, runner khóa
> training, thiết kế B2-D/B2-E + tiêu chí + kill switch khóa trước kết quả, audit resource (CPU đủ).
> **0 nhãn mới** (không reviewer; không tạo nhãn giả). Gate: **HUMAN_REVIEW_PENDING_PILOT_READY**;
> B2 TRAINING = NO.

- **Date:** 2026-10-07 · **Machine:** ASUS · **Branch:** `ux/math-arenas-hotfix-20260930` (= `main`)
- **Base commit:** `5dfdc6c` · **Scope:** research-only; no production/model/Unity change
- **Flags:** production_vad=false, router_locked=false, unity_integrated=false,
  scorer_modified=false, production_window_locked=false; B2 training = NO.
- **Continuity note:** the session was interrupted by a power loss during `features_prod`; work was
  resumed, a latent numpy bug (`np.delete` on 2D without axis) was fixed, and all stages completed.

## 1. Objective

Move from `LABEL_COLLECTION_READY_DATA_BLOCKED` (WP-1.9.26) toward a defensible answer on whether
encoder adaptation / hybrid modeling is worth pursuing: make the review package externally
executable, prepare every analysis that consumes reviewer results, prepare a legally clean
speaker-disjoint local pilot, and freeze B2-D/B2-E designs before any pilot result exists.

## 2. Work performed (Parts 1–38)

- **Part 1** — `00_STATE_RECONCILIATION.md`: frozen / unresolved / autonomous / human / data /
  legal / GPU classification.
- **Part 2** — review-system QA re-run: **21/21 PASS**; server improved (duplicate 409,
  assessability field); QA submissions deleted.
- **Parts 3–6** — `02_EXTERNAL_REVIEW/` (9 files): reviewer README/protocol/form spec, import/export
  formats, A/B instructions, agreement + adjudication protocols.
- **Parts 7–9** — `label_analysis.py` (+ synthetic test PASS), empty outputs with schema,
  `LABEL_SUFFICIENCY_GATE.md`; consensus/kappa/weighted-kappa/Fleiss implemented.
- **Parts 10–13** — pilot data contract: frozen split (6/2/2, seed 1927), 200 utterances,
  **546 final-consonant tokens**, 0.196 h, QC PASS 200/200, 0 exclusions; leakage PASS
  (train/dev/test overlap 0; duplicate audio 0; no label/machine fields in manifest).
- **Pilot labels support (gap fix)** — the 276 pack contains none of the 10 pilot speakers;
  `build_pilot_review_pack.py` generated **Pack P** (546 candidates `P27-###`, token ids match the
  features 546/546); server + label pipeline parameterized (`--pack`, `--reviews`); Pack P served
  blind (546, payload = blind_id/word/target_phone; submit 200; QA submission deleted).
- **Part 14/15** — B2-D (alternative encoder) and B2-E (hybrid) designs frozen (`07_B2_D/`,
  `08_B2_E/`).
- **Part 16** — ablation sets frozen (D0–D6; BASE/E_ENC/E_ACO/E_TMP/E_ENC_ACO primary).
- **Parts 17–18** — success criteria (FRR-first, numeric thresholds justified for n~100 test),
  failure criteria, kill switch frozen before results (`09_SUCCESS_CRITERIA/`).
- **Parts 19–22, 32** — resource audit (measured), MyST decision (DO_NOT_BUY now /
  REQUIRES_ANNOTATION), public child-data audit (OCSC/JIBO/AusKidTalk/CAPIL), full data roadmap +
  route ranking.
- **Parts 23–24** — /r/ pilot design + feature schema; TYPE-B pilot design + analysis schema.
- **Parts 28–31** — preregistration, experiment config schema, leakage checks, split validator,
  runner design (`10_REPRODUCIBILITY/`).
- **Parts 29 (execution)** — `pilot_runner.py` stages: validate PASS; `features_prod` 546 tokens in
  200 s; `features_alt` 546 tokens in 169 s; merged `pilot_features.csv` (30 fields); eval/report
  correctly STOP without labels; `--allow-training` hard-stops.
- **Parts 35–38** — required output tree completed; final gate + session report; index updated.

## 3. Exact metrics

- Review QA: 21/21 PASS (Pack R 276 / 49 speakers / 15 pools; hashes recorded).
- Pack P: 546 candidates (train 337 / dev 110 / test 99); 10 speakers; 8 /r/; audio resolve
  546/546; token match with features 546/546.
- Pilot data: 200 utts; 0.196 h; QC PASS 200/200; clipping max 0.0; min SNR proxy 25.5 dB;
  ages 6 (1 spk) / 7 (9 spk); leakage pass true.
- Features: prod 546 tokens 200 s (~0.37 s/token incl. formants); alt 546 tokens 169 s
  (~0.31 s/token); combined process previously crashed 0xC0000005 (both models in one process) ->
  stages split; no combined mode.
- Labels: NEW_LABELS_COLLECTED = 0; human_labels_present=false; TYPE-B gate 0/4; /r/ gate
  0 PRESENT / 0 ABSENT / 0 speakers.
- Power audit: n=60@0.8 CI [0.682, 0.882]; n=15@0.8 CI [0.548, 0.930].
- Resource: venv 1.94 GB; HF cache 10.50 GB; SO762 0.67 GB; frame cache 16.6 MB; Phase1_9_27
  627 KB; Research/Speech 1.90 GB.

## 4. Final gate / decisions

- Final gate: **HUMAN_REVIEW_PENDING_PILOT_READY**.
- Next gate: `HUMAN_REVIEW_ROUND_AND_PILOT_EVALUATION`.
- B2 TRAINING: NO; B2 DESIGN: pilot-ready (frozen); production untouched; MyST: DO_NOT_BUY now.

## 5. Files

```
Research/Speech/Phase1_9_27/ (00_STATE_RECONCILIATION.md, 01..13 trees, experiments/, artifacts/)
SESSION_REPORT_1_9_27.md (this file, root; copy in 13_FINAL/)
```

## 6. Open items

1. Run the human review round: Pack R (276) and Pack P (546) with 2 reviewers; import + agreement +
   sufficiency.
2. Execute the pilot evaluation (B2-D/B2-E, frozen configs, FRR-first, speaker-disjoint) after
   labels; record the outcome mechanically.
3. Parallel no-cost: TalkBank registration, PERCEPT-R request, MyST quote, counsel questions, JIBO
   license confirmation, AusKidTalk terms.
4. Do not train B2; do not modify production; do not download license-unclear data.
