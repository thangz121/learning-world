# Child Speech Benchmarks — System Profile

| Dataset | Language | Ages | Size | Labels | License/Access | Evidence |
|---|---|---|---|---|---|---|
| NOCASA / TeflonNorL2 | Norwegian | 5–12 | 7,857 train + 1,460 test; 205 words | expert 1–5 stars; 0 excluded | Zenodo 14018511, EULA signed (human) | E2 |
| speechocean762 | English | children + adults (250 spk, half children) | 5,000 utt, ~6 h | 5 experts; sentence/word/phoneme (accuracy/stress/completeness/fluency/prosody) | **free commercial + non-commercial**, OpenSLR 101 | E2 |
| SIAK (Batch B #09 / Batch A #09) | English | 4–12 (6–12 paper) | 16,308 utt | single expert 0–100 | CC-BY-ND (+commercial model use allowed) | E4 (local) |
| CMU Kids / CSLU Kids / MyST | English (native) | children | — | mostly ASR | various (research) | E2 (mentioned) |

## Methodology harvest (NOCASA)
- Prediction as **unbalanced classification** (stars) or regression.
- Primary metric **UAR** (+95% CI via bootstrap), plus ACC and MAE.
- Zero-rating (unintelligible/noisy/silent) removed → the **assessability** problem is real and
  handled at the data level.
- Speaker-disjoint train/test; controlled distribution across score/gender/age/background.
- Baseline ceiling only 36.37% UAR → child APA is genuinely hard.
