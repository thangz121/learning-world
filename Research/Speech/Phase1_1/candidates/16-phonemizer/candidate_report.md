# Candidate 16-phonemizer

## Source
bootphon/phonemizer

## Version
3.4.0 + espeak-ng 1.52.0 (winget, needs VCRedist)

## License
GPL-3.0-or-later

## Model License
no ML checkpoint (rule-based + espeak data)

## Dependency License
espeak-ng GPL-3.0 (getopt BSD-2); festival/MBROLA not used

## Intended Role
text->phoneme

## Installation
pip install; winget espeak-ng; phonemize en-us backend espeak

## Runtime
Windows ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
IPA for all 7 LWE probes (red apple/cat/this is a cat/please/teddy/ball/one).

## Baseline Comparison
Proves G2P coverage of LWE vocab today; complements espnet-g2p-en (ARPAbet) with IPA.

## Improvements
Design-time phoneme target generation for vocab pipeline.

## Problems
GPL-3.0 copyleft on both layers -> research/design-time only unless product open-sources.

## Unique Capability
RETAIN as RESEARCH_ONLY

## Retention Decision
Offline vocab authoring tool, never bundled closed-source.

## Future Combination
RESTRICTED (GPL-3.0 both layers)
