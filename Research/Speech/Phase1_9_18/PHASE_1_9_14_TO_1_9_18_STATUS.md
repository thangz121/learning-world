# PHASE 1.9.14 → 1.9.18 — STATUS (EVIDENCE-BASED RECONSTRUCTION)

**Branch:** `phase1-9-16-recovery` · **HEAD:** `584eb2d` (== `origin`, 0/0)
**Baseline for this reconstruction:** verified 1.9.15 checkpoint `3f62ef4` and later commits.
**Status vocabulary:** PASS · FAIL · PARTIAL · BLOCKED · NOT RUN · NOT FOUND.
Every claim cites an artifact path and a commit.

---

## 0. Commit index

| Phase | Commit(s) | Date | Message |
|---|---|---|---|
| 1.9.14 | `d594821` | 2026-10-03 | validate assessability and deletion-aware evidence |
| 1.9.15 | `4412c2f` | 2026-10-03 | calibrate child evidence and validate generalization |
| 1.9.15 | `019249f` | 2026-10-03 | add independent fidelity validation analyzer |
| 1.9.15 | `c362558` | 2026-10-03 | add unlabeled system-behavior audit for new blind pack |
| 1.9.15 | `3f62ef4` | 2026-10-03 | independent fidelity validation and final calibration decision |
| 1.9.16 | `1a8cafd` | 2026-10-03 | maynode recovery audit (RECOVERY_STATE + ASUS_RECOVERY_STATUS) |
| 1.9.16 | `6eb0788` | 2026-10-03 | recover maynode training materials |
| 1.9.16 | `a88a198` | 2026-10-03 | evaluate child phone adaptation |
| 1.9.17 | `d45d3ed` | 2026-10-04 | audit speechocean762 child phone tier |
| 1.9.18 | `2bb9707` | 2026-10-04 | zero-shot and B1 child phone adaptation |
| 1.9.17 (evidence) | `584eb2d` | 2026-10-04 | commit SO762 audit evidence (wav, csv, logs, card) |

---

## 1. PHASE 1.9.14 — Fidelity/assessability + deletion-aware + SIAK

**Commit:** `d594821`. **Decision:** B = RESEARCH_SUPPORTS_PARTIAL_CHANGE.
**Artifacts:** `Phase1_9_14/PHASE_1_9_14_REPORT.md`, `ARCHITECTURE_DELTA.md`,
`artifacts/p1/`, `artifacts/p2/`, `artifacts/siak/`.

| Experiment | Result | Status | What it proves | What it does NOT prove |
|---|---|---|---|---|
| P1 fidelity v1 vs v2 | v1 false-gate 8/30 (26.7%); v2 0/30 | **PASS** (for v2 no-false-gate) | ASR must not decide validity; no-evidence ≠ wrong-pronunciation | Refusal states (NO_SPEECH/UNINTELLIGIBLE/FREE_SPEAK) validated — none exist |
| P2 deletion-aware Viterbi (zero penalty) | FRR 1/16 → 7/16 | **FAIL** | Deletion is representable but FRR-unsafe | That it helps children |
| P2 deletion margin | AUC 0.839 vs 0.760 baseline; no improving threshold | **PARTIAL** | Margin ranks present/absent better | A usable operating point |
| P2 GOP-ratio | AUC 0.740; FAR 91.7% at FRR-matched | **FAIL** | GOP-ratio not better than CTC-span here | Any child benefit |
| P3 SIAK calibration | Pearson 0.226 overall / 0.263 speaker-disjoint; 4–6 FRR-proxy 33–40% | **PARTIAL** | Adult pipeline weak on child speech | Any calibration adoption |
| SIAK rejected class | present in paper (1,489) but absent from release | **NOT RUN / NOT FOUND** | Nothing measurable | Nothing (empirically) |

---

## 2. PHASE 1.9.15 — Calibration + independent validation

**Commits:** `4412c2f`, `019249f`, `c362558`, `3f62ef4`.
**Decision:** B = CALIBRATION_PROMISING_BUT_INSUFFICIENT.
**Key artifact:** `artifacts/metrics.json`, `artifacts/calibration/metrics.json`,
`artifacts/external/external_lwe_metrics.json`, `artifacts/fidelity/independent_metrics.json`.

| Experiment | Result | Status | Proves | Does NOT prove |
|---|---|---|---|---|
| P1 independent fidelity (115 clips) | v2 false-gate **0/103**; refusal **5/8 dangerous** | **PASS** (no-false-gate) / **FAIL** (refusal) | 1.9.14 no-false-gate replicates independently | Refusal states validated |
| P2 SIAK calibration | Pearson 0.307→**0.454**, FRR 12.2%→2.0%, MAE 27.3→19.8 | **PARTIAL** | Speaker-disjoint calibration works in-population | External generalization; gain partly variance compression (pred_std ~8–10 vs true ~27) |
| P2b age conditioning | neutral/slightly worse; tree ignores age | **FAIL** | Age is not a usable feature | — |
| P3 external LWE | AUC 0.659→**0.706**; FAR 77–100% at FRR≤0.10 | **FAIL** | Calibration is not an LWE error detector | — |
| P4 bottleneck | phone evidence/alignment principal bottleneck | **PASS** (diagnosis) | Justifies a child phone model experiment | That any such model works |
| Leakage audit | 0 overlap, 0 duplicate, 0 missing | **PASS** | Splits are sound | — |

---

## 3. PHASE 1.9.16 — Child phone adaptation attempt (recovery)

**Commits:** `1a8cafd`, `6eb0788`, `a88a198`.
**Decision:** D = DATA_INSUFFICIENT_FOR_CHILD_ADAPTATION.
**Artifacts:** `RECOVERY_STATE.md`, `ASUS_RECOVERY_STATUS.md`, `DATA_AUDIT.md`,
`PHASE_1_9_16_REPORT.md`, `baseline_metrics.json`, `child_model_metrics.json`,
`MAYNODE_MATERIAL_AUDIT.md`.

| Item | Result | Status |
|---|---|---|
| ASUS 1.9.16 work recovery | zero verifiable progress; treated LOST_FROM_ASUS | **NOT FOUND** |
| Material recovery (SIAK/model/CMUdict) | SIAK byte-identical (3/3 hashes); model pinned | **PASS** |
| Phone-label determination | **no gold phone labels** in SIAK/LWE/CMU-Kids; MyST/PF-STAR blocked | **BLOCKED** |
| Baseline reproduction (482 SIAK) | 482/482 exact; FRR-proxy 0.1224 | **PASS** |
| Speaker-disjoint splits | 2094/493/482/99, overlap [] | **PASS** |
| B1 training | NOT RUN (no targets) | **NOT RUN** |
| `child_model_metrics.json` | NOT_AVAILABLE | **NOT RUN** |
| `confusion_matrix.csv` | declared absent (needs phone labels both axes) | **NOT FOUND** |

This is the phase that converted "we cannot test the remedy" into a precise shopping list
(a child corpus with a genuine phone tier + speaker IDs + training rights).

---

## 4. PHASE 1.9.17 — SO762 phone-tier audit

**Commits:** `d45d3ed`, `584eb2d`. **Decision:** A = SO762_PROVIDES_A_GENUINE_CHILD_PHONE_TIER.
**Artifacts:** `PHASE_1_9_17_REPORT.md`, `so762_phone_tier_audit.json`,
`so762_format_probe.json`, `evidence/` (2 wav, CSVs, logs, dataset card).

| Item | Result | Status | Proves | Does NOT prove |
|---|---|---|---|---|
| Phone tier exists | 5-expert per-phone 0/1/2; 3,403 bad tokens all with observed error records | **PASS** | A genuine child phone tier exists | Dense observed transcript (it is not) |
| Population | 122 child spk / 2,440 utt; ages 6–15; Mandarin-L1 | **PASS** | Child population exists | Any Vietnamese age-4 match |
| Inventory mapping | 67/67 phones map to `phone-inventory-v1.4.0` | **PASS** | Pipeline-compatible targets | — |
| Audio format | 40/40 mono PCM 16k/16-bit | **PASS** | Native pipeline input | — |
| License | OpenSLR CC BY 4.0 / HF card apache-2.0 | **PARTIAL** | Permissive both ways | Which declaration governs (unresolved) |
| Age 4–5 | absent (youngest 6) | **NOT FOUND** | — | — |

**Label discipline:** `<DEL>` and `<unk>` are carried as error categories, never as phones;
observed substitutions are diagnostic only.

---

## 5. PHASE 1.9.18 — Zero-shot baseline + B1 head-only adaptation

**Commit:** `2bb9707`. **Decision:** E = B1_REGRESSES_HUMAN_CORRECT_CHILD_SPEECH.
**Artifacts:** `00`–`13` docs, `zero_shot_baseline.json`, `b1_config.json`,
`b1_test_metrics.json`, `decision.json`, `phone_results.json`, `deletion_results.json`,
`lwe_transfer.json`, `siak_transfer.json`, `generalization.json`, `checkpoints/b1_head.pt`.

| Step | Result | Status |
|---|---|---|
| Material/provenance audit | SO762/model/inventory/seed all match | **PASS** |
| Target formulation | canonical target + expert weight; no pseudo-labels; ambiguous excluded | **PASS** (as method) |
| Speaker-disjoint child split | 80/18/24 spk, overlap [], leakage [] | **PASS** |
| Zero-shot baseline (SO762 child test) | FRR 0.2272 / FAR 0.2708 (n 7,728/48) | **PASS** (measurement) |
| B1 training | valid AUC 0.7999; 46 epochs; 35.4 s | **PASS** (training ran) |
| B1 test | FRR 0.2988 (**+0.0716 worse**) / FAR 0.1042 | **FAIL** |
| Phone-level | FRR up on every sufficient-n watch-list phone | **FAIL** |
| Deletion/final-consonant | reject rate up on all classes except F | **FAIL** (no fix) |
| Historical replay | audio gitignored | **BLOCKED / NOT RUN** |
| LWE transfer | raw audio gitignored | **BLOCKED** |
| SIAK transfer | ND review open | **BLOCKED** |
| B2 | not justified, not started | **NOT RUN** |

---

## 6. Cross-phase metric trajectory

| metric | 1.9.14 | 1.9.15 | 1.9.16 | 1.9.17 | 1.9.18 |
|---|---|---|---|---|---|
| Fidelity false-gate (v2) | 0/30 | **0/103** | — | — | — |
| SIAK Pearson (frozen→best) | 0.226 | 0.307→0.454 | 0.307 (repro) | — | — |
| LWE AUC | — | 0.659→0.706 | — | — | — |
| Child phone model | not addressed | justified | BLOCKED | tier found | B1 FAIL |

---

## 7. Architecture status roll-up

| Layer | 1.9.14 | 1.9.15 | 1.9.16 | 1.9.17 | 1.9.18 | **Current** |
|---|---|---|---|---|---|---|
| AUDIO QUALITY | UNPROVEN | UNPROVEN | UNPROVEN | UNPROVEN | UNPROVEN | **UNPROVEN** |
| VAD / EOS | frozen | frozen | frozen | frozen | frozen | **BLOCKED** |
| ASSESSABILITY | PARTIAL | PARTIAL | — | — | — | **JUSTIFIED (gate) / PARTIAL (refusal)** |
| MULTI-EVIDENCE | frozen | calibration | BLOCKED | tier found | B1 FAIL | **UNPROVEN** |
| DELETION-AWARE | FAIL | FAIL | — | — | FAIL | **REJECTED** |
| DIAGNOSTIC FUSION | UNPROVEN | UNPROVEN | — | — | — | **UNPROVEN** |
| CONFIDENCE | frozen | frozen | frozen | — | — | **UNPROVEN** |
| CHILD DECISION | design | design | — | — | — | **UNPROVEN** |
| PARENT MODE | design | design | — | — | — | **UNPROVEN** |

---

## 8. Production freeze status

Unchanged in every phase artifact and in both 1.9.18 decision files
(`decision.json`, `13_DECISION.md`):

```
production_vad=false
router_locked=false
unity_integrated=false
scorer_modified=false
production_window_locked=false
```

1.9.18 verified via git that no file outside `Research/Speech/Phase1_9_18/` (plus the
1.9.17 evidence) is dirty: `non-phase1.9.18 dirty paths: []`.

---

## 9. NOT FOUND summary (explicit)

- Human-labeled **invalid** recordings (to validate refusal states): **NOT FOUND**.
- SIAK rejected/zero-rating rows in the release: **NOT FOUND**.
- Dense observed-phone transcripts for SIAK/LWE/SO762: **NOT FOUND** (so PER/confusion
  are NOT_COMPUTABLE — declared, not faked).
- Any verifiable ASUS-local 1.9.16 work: **NOT FOUND**.
- Vietnamese-L1 age-4 child phone-tier corpus: **NOT FOUND**.
- Raw LWE child audio inside the repo (by policy): **NOT PRESENT** (gitignored).

---

## 10. Bottom line

1.9.14–1.9.15 produced one durable PASS (assessability separation / no-false-gate) and a
set of FAILs for the specific scorer fixes. 1.9.16 blocked adaptation for lack of data.
1.9.17 found the data. 1.9.18 used it and the adaptation **failed its own FRR-first
criterion**, with cross-domain transfer BLOCKED. The research line is honest, reproducible,
and **not production-ready**.
