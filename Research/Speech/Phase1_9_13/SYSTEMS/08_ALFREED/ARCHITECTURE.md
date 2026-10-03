# Alignment-Free — Architecture Patterns (E2/E3)

## GOP-AF
```
LEARNER AUDIO → CTC acoustic model → frame posteriors (no segmentation)
TARGET PHONEME + CONTEXT → log posterior over FULL sequence (all alignments)
 → GOP-AF score per phoneme (deletion/insertion representable)
```

## VoxTutor benchmark
```
CANONICAL PHONEMES → per-phoneme templates → synthesized frames
 (+ injected mispronunciation at known positions; rate warp; channel noise)
SCORER: segmentation(fixed|DTW) × normalization(raw|GOP)
 → AUROC vs known truth
```

## Discrete-token surprisal
```
LEARNER AUDIO → SSL encoder → k-means tokens → n-gram LM surprisal
TEXT → Text2DUnit → canonical tokens → DTW (centroid L2) → alignment features
 → ridge regression → score
```

## WavLM-DTW templates
```
LEARNER AUDIO ─┐
NATIVE TEMPLATE ─┴→ WavLM frames → DTW (normalized distance)
 → phonetic score; warp-path statistics → rhythm; prosodic residuals → intonation
```
