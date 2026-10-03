# ELSA — System Profile

| Field | Value | Evidence |
|---|---|---|
| Vendor | ELSA Corp (elsaspeak.com / elsanow.io) | E1 |
| Product | ELSA Speak app (iOS/Android), Speech Analyzer (browser) | E2 |
| Target | Adult L2 English learners (no child-specific product found) | E2/E1 |
| Feedback | Phoneme-level color-coded feedback + hints; word/sentence scores; intonation; fluency; grammar; vocabulary; IELTS/TOEFL placement | E2 |
| Scripted mode | Read given text; server scores vs expected script | E2/E1 |
| Unscripted mode | ASR transcript + multidimensional analysis | E2 |
| API | Partner-gated; POST /api/v2/score_audio (scripted), /api/v1/score_audio_plus (unscripted), WebSocket streaming | E1 |
| Free access | App free tier exists; API requires partner token | E1 |
| Open source | None | E1 |
| Speech engine | Kaldi + custom DNN (2016) → end-to-end ASR fine-tuned on 100h non-native (2023) | E2 |
| Endpointing | Server-side EOS after beep prompt (2016) | E2 |
| L1 models | L1-specific acoustic models for targeted feedback | E2 |
| Speaker ID | Voice embedding + diarization to filter other speakers (2023) | E2 |

## Child relevance
None documented. ELSA is adult-L2 oriented; not evaluated on children. For LWE this is a gap,
not a solution.
