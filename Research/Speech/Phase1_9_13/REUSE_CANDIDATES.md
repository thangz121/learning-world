# REUSE_CANDIDATES

| item | type | source | license | reuse_cost | expected_value | next step |
|---|---|---|---|---|---|---|
| OpenPronounce pipeline | DIRECT_CODE | Halleck45 (MIT) | MIT (deps GPL: phonemizer, Levenshtein) | LOW-MEDIUM | MEDIUM-HIGH | vendor review; use as cross-check tool |
| speak-better-than-ai | DIRECT_CODE | ldenoue (MIT) | MIT (espeak-ng GPL) | LOW | MEDIUM | extract EOS + deletion scoring ideas |
| VoxTutor harness | DIRECT_CODE | ranafaraz (MIT) | MIT | LOW | MEDIUM | regression test for alignment/GOP changes |
| wav2vec2-lv-60-espeak-cv-ft | DIRECT_MODEL | HF (verify card) | pending | MEDIUM | HIGH | check card; benchmark vs our phone model |
| SIAK dataset | DIRECT_DATASET | HF rkarhila/SIAK | CC-BY-ND (+commercial model use allowed) | HIGH (legal) | HIGH | legal review; child calibration set (age 4â€“6: 594 utt.) |
| GOP-AF algorithm | ALGORITHM | arXiv 2507.16838 | paper | MEDIUM | HIGH | reimplement; targets our CTC-span failure |
| Fidelity taxonomy | ARCHITECTURE_PATTERN | Speechace docs | n/a | LOW | HIGH | design attempt-state layer |
| Omission/ErrorType schema | ARCHITECTURE_PATTERN | Microsoft docs | n/a | LOW | HIGH | model our error taxonomy on it |
| NBestPhonemes concept | ARCHITECTURE_PATTERN | Microsoft docs | n/a | MEDIUM | MEDIUM | ranked phone candidates instead of best_obs |
| EOS constants | ALGORITHM | speak-better-than-ai | MIT | LOW | MEDIUM | benchmark vs our VAD |
| Star/attempt UX | UX_PATTERN | Speech Blubs / M-Speak / SIAK | n/a | LOW | MEDIUM | child feedback design |
| "tap to scrub to the slip" UX | UX_PATTERN | slip | n/a | LOW | MEDIUM | feedback design idea |
| ELSA streaming + endpointing | ARCHITECTURE_PATTERN | ELSA papers | n/a | LOW | MEDIUM | reference only |
| On-device no-retention privacy stance | ARCHITECTURE_PATTERN | Speech Blubs policy | n/a | LOW | HIGH | align LWE privacy messaging |

---

# REUSE_CANDIDATES — Batch B additions

| item | type | source | license | reuse_cost | expected_value | next step |
|---|---|---|---|---|---|---|
| Fidelity/assessability taxonomy (Chivox/Speechace/SpeechStep/SIAK) | ARCHITECTURE_PATTERN | multiple | n/a | LOW | HIGH | design attempt-state layer (P1) |
| Audio-quality gate before feedback (Chivox) | ARCHITECTURE_PATTERN | Chivox (E1) | n/a | LOW | HIGH | implement gate research |
| Omission/insertion diagnostics (SpeechSuper UI semantics) | ARCHITECTURE_PATTERN | SpeechSuper (E1) | n/a | LOW | HIGH | align our taxonomy |
| Age-group + strict/lenient as explicit settings | CALIBRATION_METHOD | SpeechSuper (E1) / Chivox (E1) | n/a | LOW | MEDIUM | expose in research pipeline |
| Crowe & McLeod 2020 acquisition norms + latest-consonant rule + FCD filter | ALGORITHM | SpeechLP (E1) | cite paper | LOW | HIGH | implement target ordering |
| Age-band child feedback policy (no numbers ? mastery) | UX_PATTERN | SpeechStep (E1) | n/a | LOW | HIGH | child decision layer (P7) |
| Tri-state word output + stars + offline/no-collection | UX/ARCHITECTURE_PATTERN | SpeakStar (E1) | n/a | LOW | HIGH | child output design |
| Template matching (correct/incorrect templates) | ALGORITHM | SayBananas (E2) | n/a | MEDIUM | MEDIUM | low-resource evidence option |
| High-dose trial model (~100/session; dose-response) | PRACTICE_PATTERN | SayBananas (E2) | n/a | LOW | MEDIUM | practice design |
| Dual-engine scoring (child-specialist + industry-standard) | ARCHITECTURE_PATTERN | SpeechTherapyMagic (E0/E1) | n/a | MEDIUM | MEDIUM | cross-check design |
| "Slipped ? sounded like ? tip" output | UX_PATTERN | SpeechTherapyMagic (E0/E1) | n/a | LOW | HIGH | feedback text template |
| speechocean762 dataset (English, half children, phoneme labels, commercial-free) | DIRECT_DATASET | OpenSLR 101 (E2) | free commercial | MEDIUM | HIGH | download + calibrate |
| NOCASA evaluation methodology (UAR, zero-rating removal) | EVALUATION_METHOD | arXiv 2504.20678 (E2) | n/a | LOW | HIGH | adopt for child eval |
| Phonological attributes (35 or 6-category) for diagnosis | ALGORITHM | arXiv 2311.07037 / SLATE 2025 (E2) | reimplement | MEDIUM | HIGH | diagnosis layer (P5) |
| Training-free retrieval evidence (PER-MDD) | ALGORITHM | arXiv 2511.20107 (E2) | reimplement | MEDIUM | HIGH | no-training evidence (P4) |
| FRR-first evaluation | EVALUATION_METHOD | PER-MDD (E2) | n/a | LOW | HIGH | track FRR on human-correct child tokens |
