# LWE_VS_EXTERNAL_ARCHITECTURES

| Layer | LWE_CURRENT | EXTERNAL_PATTERNS | KNOWN_GAP | EVIDENCE | POTENTIAL_UPGRADE | DO_NOT_CHANGE_YET |
|---|---|---|---|---|---|---|
| Audio quality | none | quality gate before feedback (Chivox); ~50% attrition without it (SayBananas) | no gate; IMG confusion | E1/E2 | add quality/assessability gate | ✔ (research first) |
| VAD/EOS | Silero 0.5 + window research | energy hangover (1300ms); server VAD; endpointing | window policy unresolved | E2/E3 | benchmark simple EOS baselines | ✔ |
| Fidelity/attempt | none | fidelity_class; rejected class; refusal | major gap | E1/E2 | design attempt state | ✔ |
| Target | CMUdict | CMUdict + TTS/phonetician refs; developmental norms (SpeechLP) | no developmental ordering | E1/E2 | adopt acquisition norms | ✔ |
| Phone evidence | soft-v2 (CTC forced) | wav2vec2-lv-60 (OSS); HuBERT retrieval; attributes | span anchoring contaminates | E2/E3/E4 | GOP-ratio / GOP-AF | ✔ |
| Alignment | CTC spans (forced) | 2L+1 Viterbi with present flag; DTW; retrieval | deletions not representable | E3/E4 | deletion-aware alignment | ✔ |
| Acoustic | span features (1.9.12) | GOP; DTW distance; attribute posteriors | not independent | E2/E3 | replace with GOP | ✔ |
| Diagnostics | phone misses (best_obs) | omission/insertion/substitution; attributes; heard-phone | no omission state | E1/E2/E3 | error taxonomy | ✔ |
| Scoring | soft-v2 0–100 | weighted components; stars; rubrics | FRR not tracked | E1/E2/E3 | FRR-first eval | ✔ |
| Confidence | separate confidence | NBestPhonemes; lowConfidence flags | uncalibrated | E1/E3 | keep separate, gate by it | ✔ |
| Child output | 0–100 | stars; tri-state; no numbers | not child-appropriate | E1 | child decision layer | ✔ |
| Parent mode | minimal | progress by sound; SLP dashboards | no reporting layer | E1 | parent/SLP diagnostics | ✔ |
| Age/tolerance | hidden constants | exposed age groups + leniency sliders | hidden calibration | E1 | explicit settings | ✔ |
