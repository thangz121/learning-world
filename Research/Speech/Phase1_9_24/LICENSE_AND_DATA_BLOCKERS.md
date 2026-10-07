# LICENSE AND DATA BLOCKERS — WP-1.9.24

No dataset was downloaded in this work package. Statuses below come from local research records
(WP-1.9.20 HANDOFF §4, WP-1.1 candidate audits) and the locally cached model metadata.

## Models

| model | local | license | status |
|---|---|---|---|
| facebook/wav2vec2-xlsr-53-espeak-cv-ft (production frozen) | yes (D:\speech-lab\models, snapshot 2c733782…) | apache-2.0 (verified via HF model_info 2026-10-02, local record) | **CLEAR** |
| facebook/wav2vec2-lv-60-espeak-cv-ft (used for the WP-1.9.24 counterfactual) | yes (home HF cache, 2.4 GB) | apache-2.0 | **CLEAR** |
| kgnlp/allophant | refs only, weights missing | not verified | **LICENSE_BLOCKED / NOT_RUNNABLE** |
| Tabahi/CUPE-2i | custom code snapshot (mapper/model2i/windowing/ckpt), not standard transformers | not verified | **LICENSE_BLOCKED / NOT_RUNNABLE** |
| SatwikDutta/kid-whisper-tiny-en-myst | refs only, weights missing | unverified (MyST-related) | **LICENSE_BLOCKED** |

## Datasets

| dataset | local | license / terms | use | status |
|---|---|---|---|---|
| speechocean762 (SLR101) | yes D:\speech-lab\data\speechocean762 | CC BY 4.0 (free commercial, per WP-1.9.20 HANDOFF) | dev/test phone scores | **CLEAR** |
| LWE corpus (Zenodo 200495) | yes Research/Speech/ExternalData/zenodo_200495 | local record: test-only | external blind labels (28) | **CLEAR (test-only)** |
| SIAK | yes Research/Speech/ExternalData/SIAK | CC-BY-ND; **legal review open** for model-weight/derived use | calibration (not used in 1.9.24) | **LICENSE_BLOCKED** for training/derived models |
| MyST (Boulder Learning child corpus) | no | license/fee unverified (candidate 31: separate audit required) | candidate child data | **LICENSE_BLOCKED** (audit first, do not download) |
| OGI/CSLU Kids (LDC2007S18) | no | restricted: non-commercial research only, signed agreement + LDC fee | candidate child data | **LICENSE_BLOCKED** for commercial product |
| CMU Kids (LDC97S63) | no | restricted: 2 agreements + fee; ages 6–11 | candidate child data | **LICENSE_BLOCKED** |
| NOCASA | no | research EULA | not needed now | not pursued |
| Vietnamese-L1 child corpus (age 4–6, gold phone labels) | no | **does not exist publicly** | the ideal B2-C domain | **DATA_BLOCKED** |

## Consequences

1. B2-C (encoder adaptation on child speech) is blocked on three independent axes: no licensed
   child corpus with phone labels, no Vietnamese-L1 child corpus at all, and no GPU on ASUS
   (16 GB RAM, CPU-only).
2. B2-D can proceed only as a small, licensed, local comparison (lv-60) — already done in this WP
   at diagnostic scale; a full replacement decision would need more labels and a broader comparison.
3. B2-E (hybrid acoustic/phonetic) and B2-B (calibration) are the only options not blocked by data
   licensing; both need the new human labels for validation.
4. The label gap (10–20 confident PRESENT finals, /r/ first) is independent of licensing: the
   recordings already exist locally; only a human reviewer is missing.

No raw audio was copied into the repository; no secrets or tokens are stored.
