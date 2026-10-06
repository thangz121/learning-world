# A/B ADAPTATION RESULTS (Experiments B, D, F, G)

**Scripts:** `experiments/train_head.py`, `eval_ab.py`, `ab_summary.py`
**Artifacts:** `artifacts/ab/` (`ab_metrics.json`, `lwe_ab_tokens.csv`, `siak_test_ab.csv`,
`siak_46_ab.csv`, `so762_phone_ab.csv`, `head.pt`, `training_log.json`)

---

## 1. Required pipeline (spec §23)

```
SAME AUDIO (16 kHz)
   ├── CURRENT PHONE: frozen wav2vec2 encoder + original lm_head  → PhoneEvidenceV2.soft_match
   └── CHILD PHONE:   frozen wav2vec2 encoder + trained CTC head  → PhoneEvidenceV2.soft_match
                                   ↓
                 COMPARE (same alignment + soft matching code)
                                   ↓
                 HUMAN VALIDATION (LWE labels, SIAK ratings, so762 phone scores)
```

Only `logits()` is replaced (`AdaptedPEV` subclass); the soft-v2 formula, confidence and
alignment are untouched. No production code changed.

## 2. Training summary (B1)

| item | value |
|---|---|
| encoder | frozen (317M params) |
| head | 2×Conv1d 1024→512→392, k=5, ~3.6M params, 13.8 MB |
| fit data | so762 train children 1,160 utt (58 spk) + SIAK train ratings ≥60 (~1,300 utt) |
| targets | canonical phones; so762 weight = mean human phone score/2; SIAK weight = rating/100 |
| validation | 10 held-out so762 train-child speakers; best epoch 2 (CTC 16.37), then overfit |
| LWE usage | none (external only) |

## 3. External LWE validation (spec §11, FRR-first §12)

n = 48 human verdicts (29 correct / 13 incorrect), threshold 50.

| metric | baseline | adapted | direction |
|---|---:|---:|---|
| FRR (human-correct → <50) | 31.0% | 20.7% | better |
| FAR (human-incorrect → ≥50) | 38.5% | 53.9% | **worse** |
| AUC correct vs incorrect | 0.659 | 0.625 | **worse** |
| final-consonant present recall (n=16) | 93.8% | 75.0% | **worse** |
| final-consonant absent detection (n=12) | 58.3% | 33.3% | **worse** |
| final-consonant FRR | 6.3% | 25.0% | **worse** |

The overall FRR improvement is real but comes with lower discrimination and a clear
regression on the final-consonant class — the exact class the phase targets. Under the
program's non-negotiable FRR-first rule this is **not an improvement**.

## 4. SIAK speaker-disjoint validation

| set | metric | baseline | adapted |
|---|---|---:|---:|
| test speakers (482 utt, 27 spk, ages 7–12) | Pearson | 0.307 | **0.355** |
| | Spearman | 0.272 | **0.333** |
| | MAE | 27.27 | 27.53 |
| | FRR-proxy (≥80 → <50) | 12.2% | **6.6%** |
| | FAR-proxy (≥80 on <50) | 26.5% | 29.3% |
| ages 4–6 external (99 utt, 5 spk) | Pearson | 0.184 | **0.373** |
| | FRR-proxy | 42.9% | **10.7%** |

Directionally positive, but the 4–6 result is 5 speakers only and the gains are not
reproduced on the LWE task.

## 5. so762 test children (in-domain held-out speakers)

| band | baseline exact | adapted exact | baseline miss | adapted miss |
|---|---:|---:|---:|---:|
| score2 (correct) | 77.7% | 78.5% | 16.3% | 16.7% |
| score1 (accented) | 41.9% | **51.6%** | 43.4% | 38.0% |
| score0 (wrong) | 23.9% | **32.2%** | 61.1% | 55.4% |
| word-final consonant score2 | **87.3%** | 78.6% | 8.2% | 17.2% |
| word-final consonant score0 | 33.3% | 25.8% | 50.0% | 54.5% |
| AUC posterior score2 vs score0 | **0.825** | 0.808 | | |

The head improves accented/wrong phones on average but **degrades final consonants** — a
category trade visible in-domain and externally.

## 6. Per-phone deltas (so762, n ≥ 30)

Improved: **ə +0.498**, ð +0.151, θ +0.137, ʊ +0.131, aɪ +0.120, ɪ +0.115, uː +0.099,
ɹ +0.060.
Degraded: **ɑ −0.260**, w −0.259, p −0.233, h −0.159, z −0.138, k −0.128, t −0.126,
iː −0.113.

The adapted head overfits to schwa (the most frequent phone in the training mix) and loses
several frequent consonants, including t/k/z/p — exactly the phones that matter for LWE
final-consonant words (eight/five/six).

## 7. Runtime

Adapted adds ~0.25–0.35 s/utt on CPU (so762 0.75→1.01 s, SIAK 0.32→0.65 s); head adds
13.8 MB. See `RUNTIME_AND_LICENSE.md`.
