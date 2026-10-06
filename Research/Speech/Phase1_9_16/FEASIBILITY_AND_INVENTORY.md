# FEASIBILITY, DATASETS, INVENTORY, QUALITY, LEAKAGE

**Scripts:** `experiments/feasibility_audit.py`, `so762_manifest.py`, `inventory_audit.py`,
`quality_audit.py`
**Artifacts:** `artifacts/audit/`, `artifacts/inventory/`, `artifacts/quality/`

---

## 1. Data + model feasibility (spec §2, 12 questions)

| # | question | answer |
|---|---|---|
| 1 | child datasets available locally | speechocean762 (SLR101), SIAK, Zenodo 200495 (LWE corpus) |
| 2 | phoneme labels | so762: per-phone human accuracy 0/1/2 (5 experts) + canonical ARPAbet. SIAK/Zenodo: none |
| 3 | language/accent | so762 non-native English (Mandarin L1); SIAK Finnish/UK/other; LWE target Vietnamese-L1 children (no matching corpus) |
| 4 | age metadata | so762 6–15 children, 19–43 adults; SIAK 4–12; Zenodo ~4–5 |
| 5 | speaker counts | so762 child train 58 / child test 64 (disjoint); SIAK 172; Zenodo 11 |
| 6 | target vocabulary | so762 read sentences; SIAK 320 single words; LWE numbers/words/sentences |
| 7 | label reliability | so762: 94,445 phone judgments (score 2: 87,215; 1: 3,827; 0: 3,403), notation explicit incl. insertions; reliable for **quality-filtered targets**, not for phone transcriptions |
| 8 | label levels | so762 phone + word + sentence; SIAK utterance rating; Zenodo word |
| 9 | training license | so762 CC BY 4.0 (training + commercial allowed); SIAK README allows model building/evaluation (CC-BY-ND nuance, legal review open); Zenodo unused for training |
| 10 | enough for fine-tune | enough for **head-only adaptation** (1,160 so762 child train utt + SIAK auxiliary) on CPU; **not** enough for encoder fine-tuning (CPU-only, 317M params) |
| 11 | speaker-disjoint test | yes: so762 child test (64 spk) + SIAK test (27 spk) + LWE external (never trained) |
| 12 | local runtime | frozen model 1.18 GB, head 13.8 MB; cold load 13.6 s; warm 0.585 s/word; peak 1.75 GB RAM; CPU-only |

**Conclusion:** supervised phone-transcription training is impossible with local data (no
human phone transcriptions anywhere). Quality-filtered frozen-encoder head adaptation (B1)
is supported and was executed. A genuine child phone model needs encoder-level training with
more data and GPU (conditions in the main report §8).

## 2. speechocean762 manifest (parsed)

- 5,000 utterances / 250 speakers; train 2,500 (125 spk) / test 2,500 (125 spk), **speaker
  overlap 0**.
- Children (<18): train 1,160 utt / 58 spk; test 1,280 utt / 64 spk; 122 child speakers total;
  ages 6–15 (age 6 = 260 train / 240 test utt).
- Phones per utterance: train children mean 16.4; test children mean 16.9.
- Expert notation decoded with 0 alignment mismatches: `()`=0, `{}`=1, bare=2, `[]`=insertion.
- Word/sentence scores also available (not used for phone targets).

## 3. Phone inventory (versioned, no silent mapping)

`artifacts/inventory/phone_mapping.csv` — 41 ARPAbet entries all map OK in
PhoneInventory v1.4.0 (canon + model vocab id). CMUdict parses to 39 phones; so762 child
train uses 38; SIAK targets use 39. No so762 phone lacks a canonical mapping; no model
vocab token is unmapped. `SIL`/`SPN` exist in the vocab but never appear in targets.
Final-consonant inventory for SIAK targets: T 41, N 26, K 25, R 20, ER 19, S 18, L 14, M 14,
Z 11, P 11, NG 10, D 21, plus low counts — all mapped.

## 4. Data quality (spec §6)

| dataset | n | sr | dur med | rms med | clipping >1% | silence med | speakers | top-share |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| so762 all | 5,000 | 16 k | 3.53 s | 0.054 | 0 | 36.6% | 250 | 0.4% |
| so762 children | 2,440 | 16 k | 3.34 s | 0.058 | 0 | 36.4% | 122 | 0.8% |
| SIAK sample | 3,168 | 16 k | 0.85 s | 0.043 | 94 files | 32.8% | 172 | 0.6% |
| Zenodo 200495 | 671 | 44.1 k | 2.38 s | 0.127 | 0 | 0.0% | 11 | 13.1% |

Domination checks: so762 is 51.2% adults overall — training uses **children only**; age-6 is
20.5% of children; SIAK sample has 27.4% ratings <50 and only 3.1% age ≤6; Zenodo is
dominated by one speaker at 13.1% and is test-only.

## 5. Leakage audit (spec §7/§10)

| check | result |
|---|---|
| LWE audio/labels used in training | **no** (80-token corpus, 28 final-consonant labels and the 12 PME / 6 SCORER_MISS cases all held out) |
| so762 train/test speaker overlap | ∅ (official split) |
| internal val speakers (10 held-out so762 train children) | disjoint from fit speakers |
| SIAK training | train speakers only; SIAK test + ages 4–6 external never trained |
| duplicate audio | none observed (unique utterance IDs; no cross-split file reuse) |
| target overlap | so762 sentences are shared across speakers by design; speakers disjoint |
| model selection | chosen by validation CTC loss only; LWE external never inspected during training |
