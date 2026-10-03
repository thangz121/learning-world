# 01 — MATERIAL / PROVENANCE AUDIT (STEP 1)

**Script:** `experiments/audit_materials.py` · **Manifest:** `provenance.json`
**Status:** VERIFIED_COMPLETE (no blocking issues).

---

## Verified

| artifact | expected | observed | verdict |
|---|---|---|---|
| SO762 mirror revision | `06385584fad212b26134c656fdd3ccf9f093f33e` | same | MATCH (HF mirror of OpenSLR SLR101) |
| wav2vec2 revision | `2c733782…fe9398` | same | MATCH |
| wav2vec2 `config.json` sha256 | — | `4609fb49…bbfb13` | recorded |
| wav2vec2 `vocab.json` sha256 | — | `d732ab24…7d87d0` | recorded |
| CMUdict | local | sha256 `81917843…c3d22`, 3,618,488 B | MATCH |
| phone inventory | `phone-inventory-v1.4.0` | import OK, 41 ARPA symbols, sha256 `97d07c12…d0915` | MATCH |
| Phase 1.9.15 split seed | 1515 | 1515 (`Phase1_9_16/split_manifest.json`, VERIFIED_COMPLETE) | MATCH |
| Phase 1.9.16 frozen baseline | reproduced `482/482` | confirmed | MATCH |
| Phase 1.9.17 phone tier | child 122 spk / 2440 utt / 67 mapped / 0 unmapped | confirmed | MATCH |
| scripts present | phone_evidence_v2, target_cmudict, run_baseline | all present | MATCH |

## No blind redownload

The wav2vec2 snapshot, SIAK copy, and SO762 mirror were reused from local
caches; only cheap revision refs were re-read. Nothing verified-complete was
deleted, duplicated, or overwritten.

## Blocking issues

None.
