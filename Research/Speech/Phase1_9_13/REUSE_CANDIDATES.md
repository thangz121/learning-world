# REUSE_CANDIDATES

| item | type | source | license | reuse_cost | expected_value | next step |
|---|---|---|---|---|---|---|
| OpenPronounce pipeline | DIRECT_CODE | Halleck45 (MIT) | MIT (deps GPL: phonemizer, Levenshtein) | LOW-MEDIUM | MEDIUM-HIGH | vendor review; use as cross-check tool |
| speak-better-than-ai | DIRECT_CODE | ldenoue (MIT) | MIT (espeak-ng GPL) | LOW | MEDIUM | extract EOS + deletion scoring ideas |
| VoxTutor harness | DIRECT_CODE | ranafaraz (MIT) | MIT | LOW | MEDIUM | regression test for alignment/GOP changes |
| wav2vec2-lv-60-espeak-cv-ft | DIRECT_MODEL | HF (verify card) | pending | MEDIUM | HIGH | check card; benchmark vs our phone model |
| SIAK dataset | DIRECT_DATASET | HF rkarhila/SIAK | CC-BY-ND (+commercial model use allowed) | HIGH (legal) | HIGH | legal review; child calibration set (age 4–6: 594 utt.) |
| GOP-AF algorithm | ALGORITHM | arXiv 2507.16838 | paper | MEDIUM | HIGH | reimplement; targets our CTC-span failure |
| Fidelity taxonomy | ARCHITECTURE_PATTERN | Speechace docs | n/a | LOW | HIGH | design attempt-state layer |
| Omission/ErrorType schema | ARCHITECTURE_PATTERN | Microsoft docs | n/a | LOW | HIGH | model our error taxonomy on it |
| NBestPhonemes concept | ARCHITECTURE_PATTERN | Microsoft docs | n/a | MEDIUM | MEDIUM | ranked phone candidates instead of best_obs |
| EOS constants | ALGORITHM | speak-better-than-ai | MIT | LOW | MEDIUM | benchmark vs our VAD |
| Star/attempt UX | UX_PATTERN | Speech Blubs / M-Speak / SIAK | n/a | LOW | MEDIUM | child feedback design |
| "tap to scrub to the slip" UX | UX_PATTERN | slip | n/a | LOW | MEDIUM | feedback design idea |
| ELSA streaming + endpointing | ARCHITECTURE_PATTERN | ELSA papers | n/a | LOW | MEDIUM | reference only |
| On-device no-retention privacy stance | ARCHITECTURE_PATTERN | Speech Blubs policy | n/a | LOW | HIGH | align LWE privacy messaging |
