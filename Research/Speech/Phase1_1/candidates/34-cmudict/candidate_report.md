# Candidate 34-cmudict

## Source
https://github.com/cmusphinx/cmudict (raw cmudict.dict)

## Version
Fetched 2026-10-02 from master; 3,618,488 bytes.

## License
2-clause BSD-style (CMU Sphinx). CLEAR for research + commercial with notice.

## Model License
N/A (lexicon, not ML).

## Dependency License
None.

## Intended Role
Canonical text → ARPAbet target generation (SpeakingTarget.phonemes[]).

## Installation
`urllib` download to `D:\speech-lab\models\cmudict.dict`.

## Runtime
Lookup only, <1ms.

## Tests
LWE vocab probe + variants.

## Results (exact ARPAbet)
| word | variants | ARPAbet |
|------|----------|---------|
| red | 1 | R EH1 D |
| apple | 1 | AE1 P AH0 L |
| cat | 1 | K AE1 T |
| blue | 1 | B L UW1 |
| please | 1 | P L IY1 Z |
| teddy | 1 | T EH1 D IY0 |
| ball | 1 | B AO1 L |
| one | 1 | W AH1 N |
| book | 1 | B UH1 K |
| big | 1 | B IH1 G |
| small | 1 | S M AO1 L |
| open | 1 | OW1 P AH0 N |
| close | 2 | K L OW1 S / K L OW1 Z |
| dog | 1 | D AO1 G |

Matches espnet-g2p-en / g2p_en exactly on all single-variant words.

## Baseline Comparison
LWE `PhonemeTable` V1 covers only 6 words; CMUdict covers full Active 15 + more. Deterministic, versionable.

## Improvements
Stable canonical target independent of any child voice (principle §22).

## Problems
- No child-pronunciation variants (immature /r/, th-fronting).
- British/US dialect not dual-listed for most words.
- Stress digits must be stripped or kept consistently in scorer.

## Unique Capability
Deterministic, offline, zero-ML SpeakingTarget source.

## Retention Decision
RETAIN — production-path lexicon for SpeakingTarget v1.

## Future Combination
SpeakingTarget { text, arpa[], ipa[] via phonemizer/espeak RESEARCH_ONLY, variants[], dict_version="cmudict.dict@2026-10-02" }.
