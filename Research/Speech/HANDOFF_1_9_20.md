# HANDOFF — Speech/VAD Research (through WP-1.9.20)

**Repo:** `thangz121/learning-world` · **Branch:** `main` (working branch `ux/math-arenas-hotfix-20260930`, same tip)
**HEAD at handoff:** `39df310` (wp-1.9.20 alignment counterfactual audit) · **Date:** 2026-10-03
**Speech Research status:** **NOT COMPLETE** · **Last gate:** `ALIGNMENT_REPRESENTATION_FAIL`

> Read this file first, then `SESSION_REPORTS_INDEX.md` (root) and the per-phase reports.
> Do not restart completed work packages. Do not modify frozen phases or production code.

## 1. Where we are (1.9.14 → 1.9.20)

| WP | Result | Gate / decision |
|---|---|---|
| 1.9.14 | Fidelity v1/v2; deletion-aware + GOP | B (partial change); v2 false-gate 0/30 |
| 1.9.15 | SIAK speaker-disjoint calibration; 115-clip blind validation | B; v2 0/103 false-gate but refusal miss 5/8 |
| 1.9.16 | B1 child head adaptation (frozen encoder) | B; final-consonant recall 93.8→75% ⇒ not adopted |
| 1.9.17 | Deletion/alignment diagnostic | GATE A (alignment/deletion) ⇒ `REQUIRES_ALIGNMENT_WORK` |
| 1.9.18 | Deletion-aware PRESENT/ABSENT/UNCERTAIN layer | `ALIGNMENT_DELETION_GATE_FAIL`; no FRR-first-safe point |
| 1.9.19 | Window/boundary causality (11 windows) | `MIXED_WINDOW_AND_ACOUSTIC`; ALIGNMENT 9 / WINDOW 3 / ENCODER 3 / MIXED 1 |
| 1.9.20 | Alignment counterfactual (A/B/C/D/E) | `ALIGNMENT_REPRESENTATION_FAIL`; **9 “ALIGNMENT” → 8 NOT_ALIGNMENT + 1 INCONCLUSIVE**, 0 recovered; negative control FAIL |

## 2. What is now established (do not re-litigate)

- **Alignment is NOT the binding constraint** for the previously claimed failures: 8/9 have the
  evidence already inside the production span (spiky evidence diluted by the **mean**-based
  aggregation); 1/9 (`child_01_nine`) has its 0.979 frame *before* the span (earlier occurrence
  of the same class). Counterfactual realignment (blank/skip/evidence-weighted/conservative)
  recovers 0/9 and increases false evidence on absent tokens (11 vs 8 on 29 absent).
- **Production forced alignment is frozen and adequate for its purpose**, but the real failure
  mechanisms now are: (a) **scoring aggregation** (span mean vs spiky evidence; acceptance on
  top-1 identity with posterior down to 0.0007), (b) **window** (3/16 present recovered by
  context; one absent token falsely gains), (c) **encoder no-evidence** minority (3/16 present
  in all 11 windows), (d) **label limits** (single reviewer; /r/ n=1 LOW).
- Frozen production behavior unchanged: `production_vad=false · router_locked=false ·
  unity_integrated=false · scorer_modified=false · production_window_locked=false`.
- **No B2** until the aggregation rule is re-measured FRR-first and the needed labels exist.

## 3. Next work package (recommended WP-1.9.21)

**Research-only aggregation/support rule** (no training, no production change):
1. Replace span-**mean** acceptance with span **max/support** + explicit deletion/UNCERTAIN
   (reuse `E = da_span_max` from 1.9.18), evaluated FRR-first on the 28 LWE blind labels.
2. Fix the 1.9.19/1.9.20 negative-control flaw: mask repeated phone classes by **word position**
   before measuring “evidence outside span” (initial /n/ of `nine` contaminated the metric).
3. Re-run speaker-disjoint so762 dev/test with the frozen rule; confirm no absent false-gain.
4. Then decide: if present finals with strong in-span support can be kept while absent
   false-PRESENT falls → scoring fix is the path; remaining no-evidence cases feed a later B2
   design.

## 4. What to download (and what NOT to)

| asset | status | action |
|---|---|---|
| speechocean762 (SLR101) | **already local** `D:\speech-lab\data\speechocean762` | no download; if missing → `https://www.openslr.org/101/` (CC BY 4.0, free commercial) |
| SIAK | already local `Research/Speech/ExternalData/SIAK` (CC-BY-ND; legal review open) | no download |
| Zenodo 200495 (LWE corpus) | already local; **test-only** | no download |
| **MyST (Boulder Learning child corpus)** | NOT local; license/fee unverified (candidate 31: “separate audit required”) | audit license first via `Research/Speech/Phase1_1/candidates/18-*` + `31-*`; do NOT fetch before license check |
| **OGI/CSLU Kids (LDC2007S18)** | NOT local; **restricted: non-commercial research only**, signed CSLU agreement + LDC fee | cannot be used for a commercial product; only if a research-only calibration is explicitly approved |
| **CMU Kids (LDC97S63)** | NOT local; restricted (2 agreements + fee; ages 6–11) | same restriction; not needed for the next WP |
| NOCASA / speechocean762-child subsets | NOCASA EULA (research) | not needed now; speechocean762 child subset already covers validation |
| **Vietnamese-L1 child corpus (age 4)** | **does not exist publicly** | not downloadable; only option is recording/annotation or partnerships |
| New labels needed (no download) | 10–20 confidently-labeled human-present final consonants, esp. **/r/** (currently n=1, LOW confidence) | collect from existing local corpora + human review (review tooling exists: `Phase1_9_15/experiments/serve_review.py`) |

No secrets/audio in git. HF token lives only in env (`HF_TOKEN`), never in repo.

## 5. Environment / how to run

- ASUS only; no MAYNODE. Python: `D:\speech-lab\venvs\p0\Scripts\python.exe`
  (torch 2.14.1+cpu, transformers 5.17.0, sklearn 1.9.1).
- Frozen model: `facebook/wav2vec2-xlsr-53-espeak-cv-ft`, snapshot `2c733782…`
  (`PhoneEvidenceV2@1.4.0`, `87254B7B…`; inventory `97D07C12…`).
- Key commands (research-only):
  - alignment rows: `python Research\Speech\Phase1_9_20\experiments\alignment_counterfactual.py`
  - variant analysis: `...\analyze_alignment.py`
  - deletion-aware evidence: `Research\Speech\Phase1_9_18\experiments\deletion_aware_decision.py`
  - window audit: `Research\Speech\Phase1_9_19\experiments\window_causality.py`
  - HF auth (rate-limit only): set `HF_TOKEN` env; never commit.

## 6. Rules (non-negotiable)

- Human listening is authority; ASR is not a pronunciation judge; FRR-first is mandatory.
- No B2 / encoder fine-tuning / head training; no production scorer/VAD/router/Unity change.
- Do not modify frozen 1.9.x artifacts; raw child audio never committed.
- **Per work package: create `SESSION_REPORT_<ID>.md` in the repo root and update
  `SESSION_REPORTS_INDEX.md`; then commit + push automatically** (branch + `main`, fast-forward)
  — standing user instruction since WP-1.9.20.
- If a conclusion is contradicted by new evidence, report the contradiction explicitly.

## 7. Open blockers

1. Aggregation rule not yet re-measured FRR-first (WP-1.9.21).
2. Confidently-labeled human-present final consonants (esp. /r/) still missing.
3. No Vietnamese-L1 child corpus; MyST/OGI/CMU Kids license paths unresolved.
4. SIAK CC-BY-ND legal review still open for any model-weight use.

## 8. START PROMPT for a new session

```
Continue the LWE Speech Research Lab.
Repo: D:\Vscode\little-world-english (main tip 39df310).
Read: Research/Speech/HANDOFF_1_9_20.md, SESSION_REPORTS_INDEX.md, then
Research/Speech/Phase1_9_20/ALIGNMENT_COUNTERFACTUAL_REPORT.md.
Do not restart or modify completed work packages (1.8–1.9.20).
Production flags stay false. No B2. Research-only.
Next task: WP-1.9.21 — research-only aggregation/support rule (span max/support
instead of mean, FRR-first) with position-masked negative control, using existing
evidence (1.9.18 E, 1.9.19 windows, 1.9.20 frame rows); collect/annotate 10–20
confidently-labeled present finals (esp. /r/). Then assess B2.
Commit + push per work package (with SESSION_REPORT file), no need to ask.
```
