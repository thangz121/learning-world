# Round 2 human outcome — min2s windows

**Reviewer:** human_maynode  
**Date:** 2026-10-02  
**UI:** 
eview_min2s.html (>=2.0s listen windows)  
**Reference:** NEW mp3 as SPEECH_REFERENCE_STANDARD

## Counts

| Label | n |
|---|---:|
| SPEECH | **28** |
| NON_SPEECH | 0 |
| MIXED | 0 |
| UNCERTAIN | 0 |

## Qualitative notes (reviewer)

1. All segments contain speech.
2. Hard to tell exactly what the child is saying; ~70% of words can be guessed.
3. Cuts may not land on complete word boundaries (truncated / cut-off feel).
4. Source is hosting-program audio → inherently difficult.
5. NEW reference is clearer/standard; its cuts also sometimes feel mid-word (incomplete ends).

## Interpretation

- **Speech presence:** hybrid_score candidates on IMG (Silero=0) are human-verified SPEECH at candidate level.
- **Not proven:** full-file recall, production readiness, clean word-aligned crops for scorer.
- **Decision:** A. HUMAN_VERIFIED_PROMISING (research path only).

## Next

Phase 1.9.7: multi-recording validation + boundary/merge research. No Unity / no router lock.
