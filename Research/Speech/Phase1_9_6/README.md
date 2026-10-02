# Phase 1.9.6 — Hybrid Rescue Verification

**Status:** `HUMAN_REVIEW_PENDING`  
**Decision:** `C. HUMAN_REVIEW_INCONCLUSIVE`  
**Why:** Human listening of 28 hybrid candidates not yet performed. Labels not fabricated.

## Quick start (human reviewer)

1. Open `HumanReview/review.html` (MODE A — bias-controlled).  
2. Listen to **all 28** clips.  
3. Export CSV → `Results/Human_Review_Labels_Filled.csv`.  
4. Run `python scripts/analyze_human_labels.py`.

## Key paths

| Path | Purpose |
|---|---|
| `HumanReview/review.html` | Primary review UI |
| `HumanReview/clips_hybrid/` | Preview WAVs (±50ms pad; gitignored) |
| `Results/clip_audit.json` | Integrity audit (28 OK) |
| `Results/human_review_results.json` | Metrics + decision |
| `Protocol/HUMAN_REVIEW_PROTOCOL.md` | Rules |
| `PHASE_1_9_6_REPORT.md` | Full report |

## Not done in this phase

- No Unity integration  
- No production VAD / router lock  
- No Silero thr change  
- No scorer change  
- No ASR auto-labels  
