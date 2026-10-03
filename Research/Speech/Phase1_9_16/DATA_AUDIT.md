# DATA AUDIT — Phase 1.9.16

**Script:** `experiments/run_data_audit.py` · **Manifest:** `dataset_manifest.json`
All numbers below are RERUN ON MAYNODE (2026-10-03) unless marked otherwise.

---

## 1. SIAK selection (3,168 utt / 172 speakers, seed-1515 partition)

| property | value |
|---|---|
| utterances (train/valid/test/ages46-ext) | 2094 / 493 / 482 / 99 |
| total duration | 2,837.2 s (~47 min) |
| sample rate / channels | 16000 Hz (all) / mono (all) |
| duration range / mean | 0.257–3.86 s / 0.896 s |
| clipped files (peak ≥ 0.999) | 210 / 3168 (6.6%) |
| mean near-silence ratio (|x|<0.01) | 0.554 (short isolated words — expected, not a defect) |
| missing / undecodable | 0 |
| duplicate audio (1.9.15 SHA-1 audit) | 0 groups (RECOVERED_FROM_GIT) |
| dominant content | single English words / short phrases, ages 7–12 (+5 speakers ages 4–6) |

Corpus is child speech throughout (no adult dominance). Concentration risks:
train001 alone contributes 420 release utterances (sampling caps at 20/speaker);
Finnish-L1 majority (13,161/16,308 release). Both documented, not disqualifying
for validation use.

## 2. Phone-label determination (the load-bearing result of this audit)

| corpus | columns / tiers present | phone labels? |
|---|---|---|
| SIAK train.csv / test.csv | `file, utterance, score` (+ filename age/speaker/L1) | **NONE — RATINGS_ONLY** (verified by inspection + `verify_siak.py`) |
| LWE reviews (1.9.9–1.9.12) | human verdicts + model `phone_evidence` strings | **NONE — VERDICTS_ONLY** (model outputs are not labels) |
| CMU Kids mirrors (HF) | word transcripts (not downloaded) | WORDS_ONLY, mirror licenses unverified |
| MyST / PF-STAR | — | BLOCKED_ACCESS |

**Conclusion:** no corpus with defensible gold phone labels is available.
Supervised phone-head training (B1) is UNSUPPORTABLE on current materials.
SIAK is therefore used for validation/calibration only — never as phone
supervision. This single fact, combined with the license block, determines the
phase decision (see `PHASE_1_9_16_REPORT.md`, D).

## 3. What was NOT downloaded (and why)

- CMU Kids mirrors: word transcripts cannot train/test a phone head; downloading
  ~hours of audio to re-prove "no phone tier" is wasteful. Recorded NOT_REQUIRED
  with the license caveat (mirror MIT claim unverified, 1 like).
- MyST / PF-STAR: no public HF mirror; institutional access required.
- Zenodo-200495 raw: committed derivatives cover every replay/validation need.
