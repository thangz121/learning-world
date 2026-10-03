# Common Test Corpus (Phase 1.9.13)

Existing LWE research assets (do not modify):

## Adult control words (Phase1_1/audio)
- sapi_red.wav, sapi_blue.wav, sapi_cat.wav, pregen_apple_normal.wav, sapi_big.wav,
  sapi_book.wav, sapi_dog.wav, sapi_red_apple.wav

## Cases (where a system accepts upload)
01_CORRECT (word), 02_WRONG_PHONEME, 03_MISSING_FINAL_CONSONANT, 04_WRONG_WORD,
05_PARTIAL, 06_SILENCE, 07_UNRELATED_ENGLISH, 08_VIETNAMESE, 09_VERY_SOFT,
10_NORMAL, 11_LOUD, 12_PITCH_VARIATION, 13_NOISE, 14_REPEATED_WORD

## Child data (validation only, NOT ground truth)
Zenodo 200495 (Phase 1.9.8) under `Research/Speech/ExternalData/` (gitignored).
Human labels from Phases 1.9.9–1.9.12 remain the reference, separate from audio.

## VAD stress
NEW_1790 (normal), IMG_0639 (high-complexity stress). Never average.

## Constraint
Commercial web/mobile systems cannot receive these files unless the product exposes an
upload/API path. Where no path exists, the case is documented as BLOCKED, not simulated.
