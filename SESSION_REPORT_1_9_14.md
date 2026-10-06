# SESSION REPORT — Phase 1.9.14 (FIDELITY / ASSESSABILITY + DELETION-AWARE PRONUNCIATION)

> **Tóm tắt (VI):** Nghiên cứu research-only: xây lớp assessability v1/v2 trên 49 token có nhãn người,
> chẩn đoán deletion-aware Viterbi + GOP trên 65 token phụ âm cuối, kiểm kê/score SIAK. Kết quả:
> v2 không false-gate (0/30), deletion-aware/GOP không thắng baseline dưới FRR-first; SIAK zero-shot
> yếu (Pearson 0.226). Quyết định **B = RESEARCH_SUPPORTS_PARTIAL_CHANGE**. Commit `d594821`.

- **Date:** 2026-10-03
- **Machine:** ASUS (Windows, CPU-only)
- **Base:** Phase 1.9.13 `3ab6c01` (docs handoff `b4a140e`)
- **Branch:** `ux/math-arenas-hotfix-20260930`
- **Scope:** research-only; no production/Unity change; no training
- **Flags:** `production_vad=false · router_locked=false · unity_integrated=false · scorer_modified=false · production_window_locked=false`

## 1. Mission
Determine whether LWE asks the pronunciation scorer a question it should not be asked yet
(fidelity/assessability first), and whether deletion-aware evidence fixes final-consonant
false rejection / invisibility.

## 2. Read before doing anything
- `Research/Speech/Phase1_9_13/` (consolidated forensics, assessability matrix, proposal P1–P7)
- `Research/Speech/HANDOFF_1_9_13.md`
- Frozen 1.9.8–1.9.12 human-review artifacts
- `PhoneEvidenceV2` (`Phase1_4/SoftMatching/phone_evidence_v2.py`), `PhoneInventory` v1.4.0

## 3. Work performed
### P1 — Fidelity / assessability rule layer
- Script: `Research/Speech/Phase1_9_14/experiments/p1_fidelity_assessability.py`
- Data: 49 human-labeled tokens (1.9.9/1.9.10/1.9.11/1.9.12) + IMG stress case.
- Signals: duration, Silero segments/speech_ratio, RMS, ZCR, soft score, confidence, ASR status.
- v1 (literal): ASR status treated as validity evidence.
- v2 (child-safe): ASR supporting-only; refusal requires converging acoustic evidence.
- **Metrics:** v1 false-gate on human-correct **8/30 (26.7%)**; v2 **0/30**; ASR-empty human-correct
  16/24 routed to VALID/POSSIBLE (v1 put 4 UNINTELLIGIBLE + 4 FREE_SPEAK); scorer-miss deletion cases
  valid/possible attempt; IMG (Silero=0, human 28/28 SPEECH) → `POSSIBLE_ATTEMPT` with
  `ENERGY_PRESENT_DESPITE_VAD`, never NO_SPEECH.
- States `INCOMPLETE`/`FREE_SPEAK` defined but unused (no data).

### P2 — Deletion-aware final-consonant evidence
- Scripts: `experiments/p2_deletion_aware.py`, `p2_compare.py`
- Data: 65 final-consonant tokens (Zenodo), 28 blind human labels (16 present / 12 absent).
- Methods: A archived soft-v2 (reproduction 0/65 mismatches), B blank-interleaved (2L+1) Viterbi with
  skip transitions, C GOP-ratio over forced-present span; margin LLR per frame.
- **Metrics:** baseline FRR 1/16, FAR 5/12, AUC 0.760; B free presence FRR 7/16, FAR 2/12, AUC 0.698;
  margin best AUC 0.839 but no threshold dominance (FRR=0 → FAR 6/12); GOP AUC 0.740, FAR 11/12 at
  FRR≤1. Deleted /r/ still unsolved; child_04_four (present /r/, LOW) worst GOP.
- Runtime: 39.5 s CPU for 65 tokens (incl. baseline reproduction).

### P3 — SIAK calibration audit
- Scripts: `experiments/siak_metadata.py`, `siak_calibration.py`
- Release: 16,308 utt / 172 speakers / ages 4–12; license CC-BY-ND-4.0; age 4–6 = 594 utt / 5 speakers.
- Scored 1,074 utt (all 4–6 + 120/age 7–10) with frozen soft-v2: Pearson **0.226** (speaker-disjoint
  test 0.263); FRR-proxy on 4–6 human-good **33–40%**; age gradient 55→71 mean.
- **Rejected/zero-rating class absent from the release** (min score 1, no zeros) → documented but not
  empirically validatable.

## 4. Decision
**B = RESEARCH_SUPPORTS_PARTIAL_CHANGE**
- Supported: explicit fidelity/assessability layer; ASR supporting-only; FRR-first; NO EVIDENCE ≠ BAD.
- Not supported: deletion-aware/GOP as a scorer fix; any production change.

## 5. Artifacts (committed)
```
Research/Speech/Phase1_9_14/
  PHASE_1_9_14_REPORT.md, FIDELITY_ASSESSABILITY_RESULTS.md, DELETION_AWARE_RESULTS.md,
  SIAK_CHILD_CALIBRATION.md, FRR_ANALYSIS.md, ARCHITECTURE_DELTA.md,
  LICENSE_AND_DATA_NOTES.md, CURRICULUM_AND_UX_RESEARCH.md
  experiments/ (5 scripts), artifacts/p1|p2|siak, manifests/
```

## 6. Git
- Commit `d594821` — "phase1.9.14: validate assessability and deletion-aware evidence" — pushed
  to `origin/ux/math-arenas-hotfix-20260930`.

## 7. Environment / reproducibility
- Frozen model `facebook/wav2vec2-xlsr-53-espeak-cv-ft` via `PhoneEvidenceV2@1.4.0` (Apache-2.0).
- Python env `D:\speech-lab\venvs\p0` (torch 2.14.1+cpu, transformers 5.17.0, sklearn 1.9.1).
- Raw child audio gitignored (`ExternalData/zenodo_200495`, `SIAK`); derived CSVs only committed.

## 8. Open items handed to 1.9.15
- Independent validation of v2 needed (new blind set, multi-reviewer if possible).
- Population calibration with speaker-disjoint splits.
- Child phone model direction flagged (P4).
