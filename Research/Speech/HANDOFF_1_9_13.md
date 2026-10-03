# HANDOFF — Speech/VAD Research (through Phase 1.9.13)

**Repo:** `thangz121/learning-world` · **Branch:** `ux/math-arenas-hotfix-20260930`  
**HEAD at handoff:** `3ab6c01` (phase1.9.13: consolidate external system forensics)  
**Date:** 2026-10-03

> Read this file first. Do not restart completed phases. Do not modify frozen artifacts.

---

## 1. Where we are (phase timeline)

| Phase | Commit | Result |
|---|---|---|
| 1.8 | `9749b92` | Real-audio A/B: IMG (Silero=0, difficult) vs NEW (normal) |
| 1.9 | `fd0c5f4` | Hybrid VAD eval; decision: Silero default + hybrid research rescue |
| 1.9.1 | `1aef969` | Hybrid rescue human-review pack; decision B |
| 1.9.5 | `ad50950` | Real audio ingestion + evidence handoff (IMG + NEW verified) |
| 1.9.6 | `dabe5a7` | Human 28/28 SPEECH (min2s); decision A promising |
| 1.9.7 | `a81b3ff` | NEW=normal, IMG=stress; boundary/merge; router not calibrated |
| 1.9.8 | `3ca915b` | Zenodo 200495 child corpus (11 children, ~4.9y mean); decision A |
| 1.9.9 | `8479d53`/`e2449c5` | Child phone audit; Stage A labels; decision B (scorer needs child research) |
| 1.9.10 | `3dbb71b`/`cec525e` | 12 PME deep-dive; second-pass human; ending-sound class found |
| 1.9.11 | `a80dc43` | Repair states + SCORER_MISS + 80-token window A/B; decisions A(acoustic line)/B(window) |
| 1.9.12 | `d081973` | Final-consonant acoustic (redundant as implemented) + expanded window review; decisions B/C |
| **1.9.13** | `3ab6c01` | **External system forensics: 20 systems (Batch A+B) + consolidated report; decision A** |

## 2. Current architecture status (frozen)

- Silero 0.5 = default VAD (research baseline). Hybrid = research rescue for difficult audio only.
- Pronunciation scorer (soft-v2) **unchanged**.
- `production_vad=false · router_locked=false · unity_integrated=false · scorer_modified=false ·
  production_window_locked=false` — keep all false unless a separate production gate proves otherwise.

## 3. Key human evidence (authoritative)

- IMG hybrid candidates: Round1 short = 28/28 UNCERTAIN; Round2 ≥2s = **28/28 SPEECH**
  (boundaries truncated, ~70% word intelligibility).
- 1.9.10 second pass (12 PME): raw Stage B = 5 TRUE_ERROR + 7 UNCERTAIN; normalized =
  4 TRUE + 7 UNCERTAIN + 1 CONFLICTED (pme_01); 0 explicit ACCEPTABLE.
- 1.9.11 SCORER_MISS: 3 confirmed TRUE_SCORER_MISS — all "four" with **deleted final /r/**;
  rule: "missing ending sound = fail".
- 1.9.12 final consonants (28 reviewed): present 16 / absent 12 → /r/ 5/6, /t/ 3/8,
  fricatives 3/5, /n/ 1/9. Window review: 35 direct + 65 imputed (same-token rule);
  **no window shows perceptual advantage** (decision C).

## 4. Phase 1.9.13 harvest (what to reuse)

**Decision: A = CLEAR_ARCHITECTURE_INSIGHTS.** Full report:
`Research/Speech/Phase1_9_13/PHASE_1_9_13_FINAL_REPORT.md`
(+ `STOP_REINVENTING.md`, `IMPORTANT_UNKNOWN.md`, `PHASE_1_9_14_PROPOSAL.md`,
`EXTERNAL_ARCHITECTURE_PATTERNS.md`, `LWE_VS_EXTERNAL_ARCHITECTURES.md`,
`ASSESSABILITY_MATRIX.md`, `BATCH_B/age_conditioning_results.csv`).

Top reusable assets:
- **Fidelity/attempt taxonomy**: Speechace `fidelity_class` (CORRECT/NO_SPEECH/INCOMPLETE/
  FREE_SPEAK); Chivox quality gate + `post proc failed → re-record`; SpeechStep "declined
  rather than guessed"; SIAK rejected class; NOCASA zero-rating removal.
- **Deletion representation**: Microsoft `ErrorType=Omission`; SpeechSuper `/x/ omitted`;
  slip `present=false` + GOP-ratio; speak-better deletion sim=0; GOP-AF (arXiv 2507.16838).
- **MIT code (run)**: OpenPronounce (venv `D:\speech-lab\venvs\op13`; ran 16 cases E4/E6 —
  child human-correct token scored 2.46); speak-better-than-ai (E3); VoxTutor (E4 reproduced).
- **Datasets**: SIAK downloaded to `Research/Speech/ExternalData/SIAK` (16,308 utt; ages 4–6:
  594; CC-BY-ND, commercial model use allowed — legal review); speechocean762 (English, half
  children, phoneme labels, **free commercial**, OpenSLR 101 — NOT yet downloaded).
- **Target selection**: Crowe & McLeod 2020 norms + latest-consonant rule + FCD filter
  (SpeechLP, E1).
- **Evaluation**: UAR + zero-rating removal (NOCASA); **FRR-first** on human-correct child
  tokens (PER-MDD, E2).
- **No-training evidence**: PER-MDD retrieval (E2, FRR 4.43%); phonological attributes (E2).

## 5. Exact next steps (Phase 1.9.14 — proposal P1–P7)

1. **P1 Fidelity/assessability layer** (research): states NO_SPEECH / UNINTELLIGIBLE /
   INCOMPLETE / FREE_SPEAK / CORRECT; validate on our human-labeled sets.
2. **P2 Deletion-aware evidence**: blank-interleaved (2L+1) Viterbi + GOP-ratio on the 65
   final-consonant tokens vs the 28 human labels + the "four" class.
3. **P3 Child calibration**: score SIAK (4–6y) + speechocean762 (child subset), speaker-disjoint,
   UAR/FRR (download speechocean762 first; legal review for SIAK).
4. **P4 No-training retrieval (PER-MDD)** + GOP-AF reimplementation; compare with GOP-ratio.
5. **P5 Phonological-attribute diagnosis** (35 or compact 6-category head on wav2vec2).
6. **P6 Target-selection adoption** (curriculum ordering; not scoring).
7. **P7 Child-facing decision layer** (GREAT/ALMOST/TRY_AGAIN/CANNOT_ASSESS + stars; numbers
   in parent mode) with human spot-check.

Do not implement production changes; each proposal needs human validation first.

## 6. Environment & how to run

- **Frozen pipeline venv:** `D:\speech-lab\venvs\p0\Scripts\python` (torch/transformers,
  Silero, soft-v2). Do not install new packages here.
- **OpenPronounce venv (isolated):** `D:\speech-lab\venvs\op13\Scripts\python`
  (torch 2.14.1+cpu, transformers 5.17.0, openpronounce 0.3.0, espeakng-loader).
- **Real child audio (gitignored):** `Research/Speech/ExternalData/zenodo_200495/`,
  `Research/Speech/ExternalData/SIAK/`, `.../phase1_9_13_repos/` (cloned OSS repos).
- **Run OpenPronounce corpus:** `D:\speech-lab\venvs\op13\Scripts\python
  Research\Speech\Phase1_9_13\SYSTEMS\06_OPENPRONOUNCE\RUN_LOGS\run_openpronounce.py`
  (uses local SAPI references in `RAW_OUTPUT/openpronounce/ref/`; gTTS monkeypatched).
- **VoxTutor harness:** `cd Research/Speech/ExternalData/phase1_9_13_repos/VoxTutor` →
  `D:\speech-lab\venvs\op13\Scripts\python -m evals.harness`.
- **Human review servers (LAN):** per-phase `serve_review.py` (1.9.10: 8766, 1.9.11: 8767,
  1.9.12: 8768). QR helper: `Research/Speech/Phase1_9_11/Scripts/show_qr.py <url> <name.png>`.

## 7. Rules (non-negotiable)

- Human listening is authority; ASR is not a pronunciation judge; energy is not GT.
- Never fabricate human labels; never auto-fill.
- Do not modify frozen phases (1.9.x) artifacts or the scorer.
- Raw child audio never committed (gitignore `Research/Speech/ExternalData/`).
- No secrets in repo; API keys via env vars only.
- One system/experiment at a time; commit per completed unit; push after each.

## 8. Open blockers

- SIAK legal review (CC-BY-ND nuance) before calibration use.
- speechocean762 not yet downloaded.
- wav2vec2-lv-60-espeak-cv-ft and hubert-large-ls960-ft model cards not verified.
- No Vietnamese-L1 child dataset exists publicly.
- CPU latency of GOP-AF/attribute/retrieval methods unmeasured for offline Windows target.

## 9. START PROMPT for a new session (copy-paste)

```
Continue the Little World English Speech Research Lab.
Repo: D:\Vscode\little-world-english (branch ux/math-arenas-hotfix-20260930).
Read: Research/Speech/HANDOFF_1_9_13.md first, then
Research/Speech/Phase1_9_13/PHASE_1_9_13_FINAL_REPORT.md and
Research/Speech/Phase1_9_13/PHASE_1_9_14_PROPOSAL.md.
Do not restart or modify completed phases (1.8–1.9.13).
Production flags stay false (no Unity, no scorer changes, no router lock).
Next task: implement PHASE 1.9.14 P1 (fidelity/assessability layer research) and
P2 (blank-interleaved Viterbi + GOP-ratio on the 65 final-consonant tokens vs the
28 human labels in Research/Speech/Phase1_9_12). Use venv D:\speech-lab\venvs\p0 for
the frozen pipeline and D:\speech-lab\venvs\op13 for OpenPronounce cross-checks.
Commit per completed unit and push.
```
