# Candidate 19-openpronounce

## Source
Halleck45/OpenPronounce

## Version
0.3.0 (wav2vec2-large-960h + lv-60-espeak-cv-ft + espeak-ng + DTW + pYIN)

## License
MIT

## Model License
facebook checkpoints Apache (verify at ship); speechocean calib Apache

## Dependency License
torch/transformers/espeak-ng/ffmpeg/fastdtw; note: install downgraded hub to 1.16 + transformers to 5.17 in p0

## Intended Role
pronunciation assessment

## Installation
pip install openpronounce; --json (console IPA print crashes cp1258, JSON path clean)

## Runtime
CPU ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
red apple correct -> 98.86/100; same audio vs 'blue' -> 6.79 (distance 842 vs 500). Discrimination proven.

## Baseline Comparison
First end-to-end pronunciation SCORE on LWE audio (fills PRONUNCIATION_ASSESSMENT gap).

## Improvements
Self-hosted Azure-PA alternative: score 0-100 + phones + confidence + prosody.

## Problems
Console (non-JSON) output crashes on Windows codepage; large dl (2x wav2vec2-large).

## Unique Capability
RETAIN

## Retention Decision
Scoring service prototype; recalibrate thresholds on LWE vocab.

## Future Combination
CLEAR (code; verify checkpoint terms at ship)
