# slip — System Profile

| Field | Value | Evidence |
|---|---|---|
| Repo | github.com/RemiKG/slip | E3 |
| License | **none found** (no LICENSE file; no license field) → all rights reserved | E3 |
| Type | Next.js browser app (on-device inference; optional server seam) | E3 |
| Model | facebook/wav2vec2-lv-60-espeak-cv-ft (int8 ~318 MB, cached) | E3/E1 |
| G2P | espeak-ng (WASM) + cmu-pronouncing-dictionary + grapheme anchoring | E3 |
| Alignment | (2L+1) blank-interleaved Viterbi CTC forced align (torchaudio equivalent, TS) | E3 |
| Scoring | GOP-Avg + GOP-ratio; heard phone; severity; lowConfidence flag | E3 |
| Reported accuracy | GOP-CTC ≈ 0.44–0.46 phone-level corr vs human (author's note) | E3 |
| Frame rate | ~50 fps (20 ms hop) | E1/E3 |
| Child-specific | none documented | E3 |
| Offline | yes after model download | E1 |
| Privacy | on-device; audio never leaves machine (optional server seam) | E1/E3 |
