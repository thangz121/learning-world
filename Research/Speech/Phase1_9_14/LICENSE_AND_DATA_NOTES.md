# LICENSE AND DATA NOTES — Phase 1.9.14

No secrets, API keys, or child-identifying information are committed. Raw child audio
(`Research/Speech/ExternalData/`) remains gitignored.

## 1. Data used

| data | location | license | committed? | notes |
|---|---|---|---|---|
| Zenodo 200495 English children (11 children, ~4.9y mean) | `ExternalData/zenodo_200495` | CC-BY-4.0 (in 1.9.8 inventory) | no (derived CSVs only) | P1/P2 tokens; anonymised `child_01..11`; human labels already committed in 1.9.9–1.9.12 |
| SIAK (Say It Again, Kid!) 16,308 utt | `ExternalData/SIAK` | **CC-BY-ND-4.0** | no (derived stats + scores only) | README: commercial use of samples for building/evaluation of speech technology models not prohibited; no unrelated derivatives; **legal review still an open blocker**; single annotator |
| IMG/NEW reference audio | 1.9.5 artifacts (clips committed by that phase) | per 1.9.5 registry | already committed | used only as stress evidence (28/28 human SPEECH) |
| speechocean762 | not downloaded | free commercial (per 1.9.13) | no | still an open blocker; not claimed in this phase |
| Human review labels 1.9.9–1.9.12 | `Research/Speech/Phase1_9_*` | project-internal | already committed | single reviewer (`human_mobile` / `human_maynode`), documented per phase |

## 2. Models used

| model | license | evidence | use in 1.9.14 |
|---|---|---|---|
| `facebook/wav2vec2-xlsr-53-espeak-cv-ft` | **Apache-2.0** | verified via `huggingface_hub.model_info` 2026-10-02 (Phase 1.1 candidate 32, snapshot `2c73378…`) | frozen soft-v2 scorer for P2/P3; local CPU inference |
| `facebook/wav2vec2-lv-60-espeak-cv-ft` | model card **still unverified** (1.9.13 blocker) | E3 (used by OpenPronounce/speak-better) | **not used** in this phase |
| CMUdict | BSD (per Phase 1.1 registry) | local model dir `D:\speech-lab\models\cmudict.dict` | target pronunciations |
| Silero VAD | per 1.9.x baseline (MIT, already established) | frozen | baseline VAD signals (not modified) |

## 3. Code / derived artifacts committed

- `experiments/*.py` — research scripts (P1, P2, P2 comparison, SIAK metadata/calibration).
- `artifacts/**` — derived tables/summaries (no raw audio).
- `manifests/*.json` — SHA-256 provenance of inputs.
- No Unity production code, no scorer code, no VAD/router/window code was modified.

## 4. Compliance statements

- No authentication/paywall/CAPTCHA was bypassed; no external system was scraped.
- Human labels are real submissions from the project's review servers; none fabricated or
  auto-filled. Uncertainty preserved (`AMBIGUOUS`/`UNCERTAIN` never silently resolved).
- SIAK commercial-use nuance documented; any future production/model-training use requires
  the legal review already listed as an open blocker.
- Derived artifacts contain no child voice and no identifying metadata beyond the already
  anonymised speaker codes.
