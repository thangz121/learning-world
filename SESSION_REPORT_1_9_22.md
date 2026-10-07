# SESSION REPORT — WP-1.9.22 (ACCEPTANCE / IDENTITY DECISION AUDIT)

> **Tóm tắt (VI):** Audit vì sao production PhoneEvidenceV2 ACCEPT target phone khi posterior/support
> rất thấp. Tái lập quyết định production chính xác 609/609 (đối chiếu bản lưu 1.9.12: 65/65).
> Census: 527/609 accept — 501 qua identity credit (104 = rank 2–5, tức 20,8% không phải top-1),
> 26 qua similarity, **0 qua posterior path** (post ≥ 0,25 không bao giờ kích hoạt; variant E ≡ A).
> 14/14 false accept là identity/similarity (7 WEAK, 4 STRONG encoder, 3 similarity); 12/14 blank
> ≥0,9; 10/14 competitor mạnh hơn target. Gỡ identity credit (B) loại 446 true present để bỏ 11 false
> accept → FRR-first bất khả thi. Identity margin AUC chỉ 0,68–0,77; 32% true present có margin âm.
> Kết luận **ACCEPTANCE_LOGIC_AND_ENCODER_BOTH_REQUIRED**; B2 NOT READY; gate kế:
> ACCEPTANCE_RULE_REDESIGN_REQUIRED.

- **Date:** 2026-10-07
- **Machine:** ASUS · **Branch:** `ux/math-arenas-hotfix-20260930` (= `main`) · **Base commit:** `60a55ab`
- **Scope:** research-only; no production/model/Unity change. **Flags:** all five false.
- **Git:** automatic commit/push per the standing instruction (branch + `main`, fast-forward).

## 1. Mission
Determine whether the TOP-1 identity acceptance mechanism is responsible for most false acceptance
when target acoustic support is weak (H1), or whether another component of the frozen decision path
is primary (H0); prove or falsify with frame-level evidence already in WP-1.9.21.

## 2. Work performed
- `experiments/recon_reproduce.py` — verified cached decisions vs archived WP-1.9.12 (65/65 exact).
- `experiments/rebuild_spanmean_decision.py` — targeted primary-window recompute (289 encoder runs)
  restoring the span-mean top-5, target rank, competitor, blank, identity margin; 609/609 cache
  reproduction, 0 mismatches.
- `experiments/analyze_acceptance.py` — Experiments 2–6 + all required CSV/JSON outputs.
- `experiments/inspect_spanmean.py`, `inspect_acceptance.py` — audit helpers.
- Reports: `ACCEPTANCE_IDENTITY_AUDIT.md`, `B2_READINESS.md`, `NEXT_GATE_DECISION.md`.

## 3. Exact metrics / measured results
- **Reproduction:** 65/65 archived LWE; 609/609 cache primary-window tokens.
- **Acceptance census (609):** 527 accepted; identity 501 (rank-1 397, rank>0 104 = 20.8%),
  similarity 26, posterior path 0; E ≡ A (0 changed).
- **Experiment 2 (FRR-first):** B removes identity → LWE recall 0.9375→0.0 (15/16 true rejected),
  dev 0.9088→0.0632 (241/285), test 0.8943→0.0573 (190/227). D identity+support τ=0.05 →
  LWE 0.75/0.25 (3 true rejected); τ=0.2 → 0.5625/0.0833 (6 true rejected). G posterior-mean-only
  useless (τ=0.05 → recall 0.0625). No variant improves the FRR-first trade-off.
- **Experiment 3 (14 false accepts):** IDENTITY_WEAK 7, IDENTITY_STRONG 4, SOFT_SIMILARITY 3;
  competitor_conflict 10/14, blank_dominated 12/14, window_dependent 1/14.
- **Experiment 4:** removing identity rejects **446 true presents** vs 11 false accepts removed;
  `child_07_seven` (strong false peak 0.63) survives all support gates.
- **Experiment 5 (margins):** true present (n=528) median +0.0225, 169 negative, 136 post<0.01,
  314 blank≥0.90, 456 identity (97 rank>0); true absent (n=29) median −0.0059, 25 negative;
  human uncertain (n=6; 2 final-consonant) median ≈0, 6/6 blank≥0.90. AUC margin LWE 0.682 /
  dev 0.770; span-max 0.781 / 0.792.
- **Experiment 6 (windows):** stable present 447, stable absent 46, window-sensitive 42,
  unstable 14, appears-only-outside-core-span 7, neighbor-phone 1 (11.5% not stable).
- **Experiment 7:** 28 LWE blind labels (16P/12A), /r/ 1P/5A; candidate pack 20; 0 new labels
  (no reviewer in-session; none fabricated).
- **Experiment 8:** B2 NOT READY (see `B2_READINESS.md`).

## 4. Gate / decisions
- Final status: **ACCEPTANCE_LOGIC_AND_ENCODER_BOTH_REQUIRED**.
- Next gate: **ACCEPTANCE_RULE_REDESIGN_REQUIRED** (research-only).
- B2: **NOT READY**. Production: untouched.

## 5. Artifacts
```
Research/Speech/Phase1_9_22/
  ACCEPTANCE_IDENTITY_AUDIT.md, ACCEPTANCE_VARIANT_RESULTS.csv,
  FALSE_ACCEPTANCE_DECOMPOSITION.csv, IDENTITY_MARGIN_ANALYSIS.csv,
  WINDOW_IDENTITY_ANALYSIS.csv, B2_READINESS.md, EXPERIMENT_RESULTS.json,
  NEXT_GATE_DECISION.md,
  artifacts/ (experiment1_reproduction.json, spanmean_decision_*.csv),
  experiments/ (recon_reproduce.py, rebuild_spanmean_decision.py, analyze_acceptance.py,
                inspect_spanmean.py, inspect_acceptance.py)
```

## 6. Open items / next steps
1. Acceptance-rule redesign (research-only, FRR-first) on the existing cache and labels.
2. Run the WP-1.9.21 label candidate pack (weak present finals, /r/ priority, rank-2–5 identity).
3. Reconsider B2 only after 1–2.

## 7. Git
Commit + push this package to the working branch and `main` (fast-forward), including this report and
the index update, per the standing instruction; verify `main == origin/main` and a clean tree.
