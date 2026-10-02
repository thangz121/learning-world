# Candidate: 33-parselmouth

## Source
- PyPI: praat-parselmouth (Python bindings for Praat)
- Upstream Praat: https://github.com/praat/praat / https://www.fon.hum.uva.nl/praat/

## Version
- Date tested: 2026-10-02
- praat-parselmouth 0.4.7, bundled Praat 6.1.38 (`parselmouth.PRAAT_VERSION`), numpy 2.5.3, Python 3.14.7
- Install: `D:\speech-lab\venvs\p0\Scripts\python -m pip install praat-parselmouth` (9.1 MB wheel, win_amd64, no espeak/system deps)

## License
- praat-parselmouth 0.4.7: GPLv3 (per pip metadata).
- Praat itself is GPL — copyleft applies to the tool, not to measured numbers. Any redistribution/bundling of the binary needs GPL compliance review.

## Model License
NOT_AVAILABLE (no model/checkpoint — signal-processing primitives only).

## Dependency License
- numpy 2.5.3: BSD-3-Clause (+0BSD/MIT/Zlib/CC0-1.0 bundle). No torch/transformers needed for this candidate.

## Intended Role
Toolkit-primitive (acoustic measurement: F0 pitch + F1/F2/F3 formants for vowel-contrast evidence). Not STT/VAD/Alignment.

## Installation
```
D:\speech-lab\venvs\p0\Scripts\python -m pip install praat-parselmouth
D:\speech-lab\venvs\p0\Scripts\python -c "import parselmouth; print(parselmouth.__version__)"
# 0.4.7
```
Smallest reproducible env: venv p0 + `praat-parselmouth==0.4.7` + `numpy`. No compiler, no GPU, no model download.

## Runtime
- OS: Microsoft Windows 11 Pro 10.0.26200, CPU AMD Ryzen 5 150 (6C/12T), RAM 16 GB, CPU-only.
- Per-file measurement (pitch + Burg, 0.8–1.3 s audio) is effectively instant (< 1 s, not formally timed). No GPU/RAM pressure.

## Tests
Corpus (16 kHz mono, same files as other candidates):
- sapi_red.wav (1.2194 s, 19511 samples), sapi_cat.wav (1.2594 s, 20151), pregen_apple_normal.wav (0.8400 s, 13440), stress_tone.wav (1.0000 s, 16000; 440 Hz check tone)
Exact settings (do not change without re-measuring):
- Pitch: `Sound.to_pitch(time_step=0.01, pitch_floor=75.0, pitch_ceiling=600.0)`; stats over voiced frames only (freq > 0). Note: `time_step=0.0` is rejected by this parselmouth build (Positive[float] required), so 0.01 was used.
- Formants: `Sound.to_formant_burg(time_step=0.01, max_number_of_formants=5.0, maximum_formant=5500.0, window_length=0.025, pre_emphasis_from=50.0)`; values read at 25/50/75% duration + mean/std/min/max over 10 evenly spaced points (10–90% duration). F1/F2/F3 reported; F4/F5 not used.

## Results
All numbers measured 2026-10-02, Praat 6.1.38 via parselmouth 0.4.7.

### F0 (pitch, voiced frames only)
| file | dur (s) | frames total / voiced | F0 mean (Hz) | F0 std | F0 min | F0 max |
|---|---|---|---|---|---|---|
| sapi_red.wav | 1.2194 | 118 / 30 | 164.50 | 15.03 | 137.86 | 183.83 |
| sapi_cat.wav | 1.2594 | 122 / 18 | 166.10 | 10.30 | 148.96 | 178.43 |
| pregen_apple_normal.wav | 0.8400 | 80 / 40 | 186.82 | 44.41 | 136.38 | 248.58 |
| stress_tone.wav | 1.0000 | 97 / 97 | 440.0004 | 0.00002 | 440.0004 | 440.0004 |

440 Hz verification: PASS — measured 440.0004 ± 0.00002 Hz (expected ~440 Hz). Pitch pipeline is trustworthy within < 0.01 Hz on a pure tone.

### Formants Burg (F1/F2/F3, Hz)
Quartile snapshots (t = 25/50/75% duration):
| file | t25 F1/F2/F3 | t50 F1/F2/F3 | t75 F1/F2/F3 |
|---|---|---|---|
| sapi_red.wav | 0.305 s: 766.4 / 1997.7 / 3061.8 | 0.610 s: 523.6 / 1883.6 / 3455.7 | 0.915 s: 521.3 / 1877.5 / 3471.1 |
| sapi_cat.wav | 0.315 s: 925.3 / 1866.2 / 3086.4 | 0.630 s: 500.5 / 1542.3 / 4165.7 | 0.945 s: 522.3 / 1878.9 / 3467.3 |
| pregen_apple_normal.wav | 0.210 s: 928.9 / 1689.6 / 3028.5 | 0.420 s: 504.0 / 927.8 / 3451.4 | 0.630 s: 713.2 / 1253.0 / 2434.4 |
| stress_tone.wav (non-speech) | 0.250 s: 419.5 / 463.5 / 2639.4 | 0.500 s: 419.5 / 463.5 / 2635.6 | 0.750 s: 419.5 / 463.5 / 2634.3 |

10-point means (10–90% duration, n=10 each):
| file | F1 mean ± std (min–max) | F2 mean ± std (min–max) | F3 mean ± std |
|---|---|---|---|
| sapi_red.wav | 548.66 ± 117.52 (296.14–744.65) | 1811.42 ± 251.50 (1171.95–2078.22) | 3250.61 ± 564.06 |
| sapi_cat.wav | 675.41 ± 164.09 (501.89–888.57) | 1954.19 ± 264.66 (1601.84–2668.72) | 3366.16 ± 291.33 |
| pregen_apple_normal.wav | 808.15 ± 258.59 (494.60–1313.67) | 1614.51 ± 571.56 (893.06–2409.27) | 3321.51 ± 309.21 |
| stress_tone.wav | 419.53 ± 0.004 | 463.49 ± 0.004 | 2633.15 ± 3.66 |

Caveats (do not overclaim): file-level means include silence + consonants + vowels, so they are NOT vowel-only targets. They are coarse acoustic evidence that the pipeline resolves differences (e.g. apple shows wider F1/F2 spread consistent with a two-syllable word vs single-syllable red/cat), but true vowel-contrast claims (ɛ vs æ vs ɑ) require vowel-segmented Burg measurement (forced alignment + window on vowel nucleus). Tone formants are meaningless for vocal-tract inference (pure sine has no formants; Burg just fits poles near harmonics) — reported only to show the method does not hallucinate speech formants on tones.

## Baseline Comparison
Same audio as candidate 32. CTC phoneme output (32: sapi_red ɹɛd, sapi_cat kɛt, apple aːpo) aligns with acoustic differences here (red vs cat share ɛ per CTC but differ in quartile formant trajectories — e.g. cat t50 F2 1542 vs red t50 F2 1884 — reflecting k/t vs ɹ/d context, not a vowel change). No prior acoustic baseline exists; tone file serves as calibration baseline (440 Hz PASS).

## Improvements
- First calibrated F0/formant numbers on LWE Phase 1.1 corpus with exact reproducible Praat settings; tone check proves < 0.01 Hz pitch error.
- Per-file F0 + F1/F2/F3 tables give Phase 1.2 scorer concrete acoustic targets to compare against CTC phone confidences (candidate 32), e.g. low-conf ɛ in cat (0.3874) can be cross-checked against formant trajectory.

## Problems
- Whole-file formant means are coarse; SAPI TTS has long silences (only 18–40 voiced pitch frames of 80–122) so silence biases formant means. Vowel segmentation not done in this candidate.
- Burg with maximum_formant 5500 Hz is an adult-speech default; SAPI voice + child-target speech may need 5500 vs 5000 sensitivity analysis (not run — would be fabrication to claim).
- GPLv3 (parselmouth/Praat) needs compliance review before bundling into a shipped product; fine for lab use.

## Unique Capability
Only candidate providing calibrated acoustic-phonetic evidence (F0 + formant tracks) rather than labels — the ground truth against which pronunciation claims (vowel contrasts, stress tone) can be checked, including a verified 440 Hz calibration.

## Retention Decision
RETAIN (license status REVIEW_REQUIRED for product bundling due to GPLv3; CLEAR for internal lab use). No model license involved.

## Future Combination
Parselmouth (this candidate) as acoustic verifier: vowel-nucleus F1/F2 targets + F0 contour, combined with VAD (cand. 06/07) to isolate speech frames, Wav2Vec2-phoneme CTC conf (cand. 32) for phone posteriors, and Phase 1.2 GOP-style scorer that fuses both on the same audio.
