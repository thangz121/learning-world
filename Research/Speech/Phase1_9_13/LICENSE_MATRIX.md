# LICENSE_MATRIX

| system | code_license | model_license | dataset_license | commercial_use | redistribution | modification | usable_in_LWE | reason | source |
|---|---|---|---|---|---|---|---|---|---|
| Speech Blubs | proprietary | proprietary | n/a | no | no | no | No | nothing extractable | E1 |
| AI Speak / M-Speak | proprietary | proprietary | n/a | no | no | no | No | nothing extractable | E1 |
| ELSA | proprietary | proprietary | proprietary | via API contract | no | no | API only | adult models; cloud | E1 |
| Microsoft PA | SDK samples MIT; service proprietary | service | n/a | via Azure | no | no | API possible; schema reusable | cloud + cost + child privacy review | E1 |
| Speechace | proprietary | proprietary | unknown | via paid plan | no | no | API possible | adult; key by request | E1 |
| OpenPronounce | **MIT** | wav2vec2 (verify card) | n/a | yes (MIT) | yes | yes | **Code YES (dependency review)** | phonemizer GPL-3.0; Levenshtein GPL-2.0 | E3 |
| speak-better-than-ai | **MIT** | wav2vec2 ONNX (verify) | n/a | yes (MIT) | yes | yes | **Code YES (espeak-ng GPL review)** | espeak-ng WASM GPL-3.0 | E3 |
| Alignment-free / VoxTutor | **MIT** | n/a (synthetic) | n/a | yes | yes | yes | **YES (research)** | numpy-only | E3/E4 |
| GOP-AF (paper) | no code | n/a | CMU Kids (research) | reimplement | — | — | Reimplement from paper | standard math | E2 |
| SIAK | no scoring code | not released | **CC-BY-ND-4.0** | **commercial model use not prohibited** (card) | no derivatives | no derivatives | **Dataset needs legal review** | ND limits; child privacy statement | E3 |
| slip | **none found** | wav2vec2 (verify) | n/a | no | no | no | **No code reuse** | no license | E3 |
| wav2vec2-lv-60-espeak-cv-ft (model) | — | **VERIFY HF model card** (commonly Apache-2.0; not assumed) | — | pending | pending | pending | Pending card check | used by 3 open systems | E1 |

Local child data: `Research/Speech/ExternalData/SIAK/` (gitignored, 279 MB).
Local external repos: `Research/Speech/ExternalData/phase1_9_13_repos/` (gitignored).
