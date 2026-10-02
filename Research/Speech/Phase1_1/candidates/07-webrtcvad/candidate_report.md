# Candidate 07-webrtcvad

## Source
wiseman/py-webrtcvad (via webrtcvad-wheels 2.0.14 fork, cp314)

## Version
2.0.14.post1

## License
MIT + WebRTC BSD-style (cbits)

## Model License
none (DSP, no model)

## Dependency License
none

## Intended Role
VAD baseline

## Installation
py-webrtcvad has NO cp314 wheel -> fork webrtcvad-wheels works; 30ms frames, aggressiveness 3

## Runtime
CPU ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
Speech files detected; silence 0/66; BUT noise 66/66 + tone 33/33 false-positive. ~0ms.

## Baseline Comparison
Zero-cost baseline that CONTRASTS silero: proves ML-VAD value with numbers.

## Improvements
Cheapest possible speech gate; useful where noise is controlled (quiet room).

## Problems
Total false-positive on noise/tone -> never use alone for endpointing.

## Unique Capability
RETAIN

## Retention Decision
Baseline comparator; ultra-low-cost gate in quiet conditions.

## Future Combination
CLEAR
