# Downstream Safety Report — Gates 6–7

## IMG hybrid segment quality (no human GT yet)
- n=28, mean dur≈0.25s, **all <0.5s**, 9 <0.2s → high fragmentation risk
- speech_ratio≈0.032 (selective vs energy)
- Weak automated Moonshine probe: **0/28 nonempty ASR** → does **not** prove SPEECH; may be non-EN / noise / too short

## LWE soft-score vs crop/pad (delta vs full)

Summary from `downstream_results.json` (soft-v2):

| word | full | notes on crop sensitivity |
|---|---:|---|
| red | 100 | stable with silero/hybrid pads |
| apple | 75.2 | ~stable |
| cat | 100 | stable |
| blue | 5.8 | **highly crop-sensitive** (can jump) |
| big | 33.3 | **highly crop-sensitive** (can jump to 100) |
| book | 66.7 | check pad variants in JSON |
| dog | 33.3 | |
| red apple | 100 | |
| silence | 0 or low | VAD may emit 0 segs → score 0 |

## Conclusion for VAD→pronunciation handoff
- Prefer **full utterance** or **padded crop (≥250ms)** over tight crops for pronunciation.
- VAD front-end must not force tight crops into scorer without pad.
- Hybrid VAD on clean LWE short files: silero usually detects; hybrid crop deltas match silero crop deltas when both detect (see Phase 1.9) → damage is crop/alignment, not unique to hybrid.
