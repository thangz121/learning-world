# Candidate 36-score-confidence-v1

## Source
In-lab composition: CMUdict target + wav2vec2-xlsr-53-espeak-cv-ft CTC + Levenshtein phone ops + per-phone conf aggregation.
Script: `D:\speech-lab\bench_score_v1.py`
Artifact: `Research/Speech/Phase1_1/Experiments/score_vs_confidence_v1.json`

## Version
2026-10-02, ASUS CPU.

## License
Composition of CLEAR/Apache components (CMUdict BSD, facebook wav2vec2 Apache-2.0).

## Model License
facebook/wav2vec2-xlsr-53-espeak-cv-ft Apache-2.0.

## Dependency License
torch/transformers/soundfile — as prior audits.

## Intended Role
Prototype SCORE 0–100 + CONFIDENCE separation; phoneme diagnostics (sub/del/ins).

## Installation
Uses already-cached phone CTC model from Phase 1.1.

## Runtime
CPU ~0.4s/file after 3.2s load.

## Tests
1. Baseline clean SAPI/pregen words.
2. Speaker-variation corruptions of `sapi_red.wav` (pitch/amp/noise) — NOT phoneme errors.
3. Wrong-target: red audio scored as "blue".
4. Silence vs "red".

Canonical target = CMUdict only. No child voice as ground truth.

## Results (measured)

### Baseline
| file | target | word_score | confidence | PER | hyp_n |
|------|--------|------------|------------|-----|-------|
| sapi_red | red | **91.1** | **0.911** | 0.0 | 3 |
| sapi_cat | cat | 68.9 | 0.518 | 0.333 | 3 |
| pregen_apple | apple | 29.6 | 0.121 | 0.75 | 3 |
| sapi_red_apple | apple | 9.9 | 0.0 | 1.5 | 6 |
| sapi_blue | blue | 11.8 | 0.0 | 1.333 | 4 |
| stress_silence | red | **0.0** | **0.0** | 1.0 | 0 |

### Speaker-variation corruptions of red (same phoneme target)
| corruption | word_score | confidence | PER |
|------------|------------|------------|-----|
| pitch_up_1.3 (crude) | 59.8 | 0.504 | 0.333 |
| pitch_down_0.75 | **73.4** | 0.734 | **0.0** |
| soft_0.2 | **79.1** | 0.791 | **0.0** |
| loud_2.0 | **90.7** | 0.907 | **0.0** |
| noise_snr5 | 55.6 | 0.437 | 0.333 |
| noise_snr0 | 33.7 | 0.165 | 0.667 |

### Wrong target
red audio vs target "blue": word_score **1.8**, conf **0.0**, PER 1.0.

## Baseline Comparison
OpenPronounce earlier: red_apple correct 98.86 / wrong-word 6.79 — same discrimination direction.
This v1 adds PER + per-phone ops + explicit confidence field separate from score.

## Improvements (measured)
1. SCORE ≠ CONFIDENCE demonstrated (cat: score 68.9 but conf only 0.518).
2. Silence → (0, 0) not a false "pass".
3. Wrong target → near-zero score.
4. Soft/loud/pitch-down keep PER=0 (speaker variation partially tolerated).
5. Canonical target external to any child waveform (§22 held).

## Problems
1. Crude pitch_up damages signal (not true formant-preserving shift) → confounds "pitch invariance" claim.
2. `apple` / `blue` poor hyp quality on this phone model (known from candidate 32: apple → aː p o).
3. Phrase vs single-word target mismatch (red_apple scored as apple only).
4. No human calibration yet (no speechocean762 downloaded).
5. No real child audio — all SAPI/pregen = adult/synthetic labels only.

## Unique Capability
First end-to-end lab pipeline producing {word_score 0–100, confidence, PER, phone ops, phone_scores[]} on LWE audio with CMUdict targets.

## Retention Decision
RETAIN as research scorer v1 (not production). Seed for Phase 1.2 multi-signal ensemble.

## Future Combination
+ silero-VAD gate
+ moonshine ASR (text gate, not score)
+ parselmouth F1/F2 on vowel phones only
+ OpenPronounce as second scorer vote
+ human calibration on speechocean762 when dataset available
