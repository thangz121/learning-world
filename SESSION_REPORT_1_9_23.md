# SESSION REPORT — WP-1.9.23 (ACCEPTANCE RULE REDESIGN + FALSIFICATION + LABEL PACK)

> **Tóm tắt (VI):** Thiết kế research-only lớp acceptance với PRESENT/ABSENT/UNCERTAIN: 68 luật /
> 8 họ trên feature matrix dựng từ frame cache (không rerun encoder). Chọn trên SO762 dev, LWE
> external, test held-out. **0/68 luật FRR-first safe**. Luật tốt nhất `D2_identity_blank_occ1.0`
> (bỏ nhánh similarity) bỏ 3/10 false accept TYPE A nhưng mất 4,6 điểm recall dev; các luật
> rank/margin/blank/temporal bỏ được 8–9/10 TYPE A phải trả 21,8–51,6 điểm. 4/14 false accept là
> TYPE B (encoder, 0,63–0,96) không luật nào sửa được. Falsification 10 test: 6 FAIL, 1 PASS hạn chế,
> 3 cảnh báo. Pack nhãn 23 ứng viên READY_FOR_HUMAN_REVIEW, NEW_LABELS_COLLECTED = 0. Final:
> **ACCEPTANCE_RULE_AND_ENCODER_BOTH_LIMITING**; B2 NOT READY; gate kế:
> LABEL_EXPANSION_AND_ENCODER_DESIGN_REQUIRED.

- **Date:** 2026-10-07
- **Machine:** ASUS · **Branch:** `ux/math-arenas-hotfix-20260930` (= `main`) · **Base commit:** `d7ef0ff`
- **Scope:** research-only; no production/model/Unity change. **Flags:** all five false.
- **Git:** automatic commit/push per the standing instruction (branch + `main`, fast-forward).

## 1. Mission
Determine whether a research-only acceptance rule can preserve identity recall for weak true-present
final consonants while rejecting the weak / blank-dominated / competitor-conflict false accepts
(WP-1.9.22); explicit PRESENT/ABSENT/UNCERTAIN; FRR-first; no B2; no production change.

## 2. Work performed
- `experiments/build_feature_matrix.py` — 609-token feature matrix (identity, blank, temporal,
  competitor, window stability, phone) from existing WP-1.9.21/1.9.22 artifacts only.
- `experiments/run_rule_search.py` — 68 interpretable rules in 8 families (A strict identity,
  B rank-aware, C margin, D blank, E identity+support+margin, F identity+support+margin+blank with
  UNCERTAIN, G temporal, H phone-class exploratory); dev selection, external LWE, held-out test,
  Pareto frontier, TYPE A/B false-accept accounting, subgroup analysis, 10 falsification tests,
  per-token counterfactual table.
- `experiments/build_label_pack.py` — 23-candidate reviewer-ready pack + instructions.
- `experiments/inspect_rules.py` — audit helper.
- Reports: `ACCEPTANCE_RULE_REDESIGN.md`, `FALSIFICATION_RESULTS.md`, `B2_READINESS.md`,
  `NEXT_GATE_DECISION.md`, `HUMAN_LABEL_REVIEW_INSTRUCTIONS.md`.

## 3. Exact metrics / measured results
- **Baseline (full window):** dev 0.9088/0.5294; LWE 0.9375/0.4167; test 0.8943.
- **Rules:** 68 evaluated; **0 FRR-first safe**; 8 Pareto-optimal dev points.
- **Best rule `D2_identity_blank_occ1.0`:** dev 0.8632/0.4118 (loss 4.6 pts), LWE 0.9375/0.3333,
  test 0.859; removes 3/10 TYPE A (all similarity-path), leaves 7/10 TYPE A + 4/4 TYPE B.
- **Alternative rules:** A1 rank-1 0.6912/0.1176 (LWE 0.50/0.1667); B rank-aware 0.7404/0.1765;
  C1 margin≥0 0.6912/0.1176 (LWE 0.4375); D1 blank≤0.90 0.3930/0.0588; E 0.6772/0.1176
  (LWE 0.375); F main 0.6244/0.0714 with 22.2% UNCERTAIN (LWE 0.4444/0.0, 35.7% UNCERTAIN);
  G1 run≥2 0.4140/0.1765 (LWE 0.1875); H1 exploratory 0.7579/0.1176 (LWE 0.5625/0.1667).
- **False accepts:** 14 = 10 TYPE A + 4 TYPE B; best rule removes 3 TYPE A, TYPE B 4/4 remain.
- **Subgroups:** weak present 0.6582→0.4937; strong present 1.0→1.0; weak absent FAR 0.3636→0.2273;
  strong absent FAR 0.8→0.8; rank-1 FAR 1.0→1.0; rank-2..5 FAR 1.0→1.0; not-top5 recall 0.325→0.0;
  blank≥0.90 recall 0.8659→0.8156; one-frame 0.8539→0.7865; multi-frame 0.9919 (FAR 1.0); /r/ n=14
  recall 1.0 FAR 0.8333→0.5.
- **Falsification:** 6 FAIL (blank-dominated 262 remain; rank2-5 low-support 34; competitor 104;
  strong encoder 4; weak true presents 72 rejected; no safe point), 1 PASS (/r/ recall preserved,
  data-limited), 3 warnings (one-frame accepted by design; LWE/dev asymmetry; F 22–36% UNCERTAIN).
- **Label pack:** 23 candidates (P1 /r/ present 3, P2 weak present 2, P3 rank2-5 5, P4 strong
  false-accept 5, P5 /r/ absent 4, P6 strong /r/ present 4); NEW_LABELS_COLLECTED = 0; no
  fabrication; no audio in repo.
- **Human vs machine (existing 28):** production 15/16 present accepted, 5/12 absent accepted;
  best rule 15/16, 4/12; F rule 7/16, 0/12.

## 4. Gate / decisions
- Final status: **ACCEPTANCE_RULE_AND_ENCODER_BOTH_LIMITING**.
- Next gate: **LABEL_EXPANSION_AND_ENCODER_DESIGN_REQUIRED** (research-only).
- B2: **NOT READY** (no training). Production: untouched.

## 5. Artifacts
```
Research/Speech/Phase1_9_23/
  ACCEPTANCE_RULE_REDESIGN.md, ACCEPTANCE_RULE_VARIANTS.csv, ACCEPTANCE_RULE_PER_TOKEN.csv,
  ACCEPTANCE_PARETO_FRONTIER.csv, FALSE_ACCEPT_RECLASSIFICATION.csv,
  HUMAN_LABEL_REVIEW_PACK.csv, HUMAN_LABEL_REVIEW_INSTRUCTIONS.md, NEW_HUMAN_LABELS.csv,
  PHONE_SUBGROUP_ANALYSIS.csv, FALSIFICATION_RESULTS.md, EXPERIMENT_RESULTS.json,
  B2_READINESS.md, NEXT_GATE_DECISION.md,
  artifacts/feature_matrix.csv,
  experiments/ (build_feature_matrix.py, run_rule_search.py, build_label_pack.py,
                inspect_rules.py)
```

## 6. Open items / next steps
1. Human review round on the 23-candidate pack (10–20 confident PRESENT finals, /r/ first).
2. Design-only encoder-evidence study (weak finals, strong false peaks) — no training.
3. Reconsider B2 only after 1–2.

## 7. Git
Commit + push this package to the working branch and `main` (fast-forward), including this report and
the index update, per the standing instruction; verify `main == origin/main` and a clean tree.
