# ZERO-SHOT CHILD BASELINE (Experiments A + C)

**Script:** `experiments/zeroshot_phone_benchmark.py` · **Artifacts:** `artifacts/zeroshot/`
**Model:** frozen `facebook/wav2vec2-xlsr-53-espeak-cv-ft` (snapshot `2c733782…`) via
`PhoneEvidenceV2@1.4.0`, alignment `ctc_forced_v1`. No training, no scorer change.

---

## 1. Experiment A — zero-shot on speaker-disjoint child test data

| corpus | utt | phones | exact | exact+soft | miss | runtime |
|---|---:|---:|---:|---:|---:|---:|
| so762 test children (64 spk) | 1,280 | 21,617 | 75.4% | 81.9% | 18.1% | ~0.75 s/utt (3.3 s median audio) |
| SIAK test speakers (27 spk) | 482 | 1,758 | 69.1% | 76.0% | 24.0% | 0.32 s/utt |

Important: neither corpus provides phone **transcriptions**, so these are *canonical
agreement* figures (what the model hears vs what the word should be), not field-standard
phone accuracy/PER. This limitation is structural for LWE (spec §4).

## 2. Evidence vs human phone scores (so762, 5 experts)

| human phone score | n | exact | exact+soft | miss | mean posterior | mean sim |
|---|---:|---:|---:|---:|---:|---:|
| 2 (correct) | 20,423 | 77.7% | 83.7% | 16.3% | 0.106 | 0.789 |
| 1 (accented) | 793 | 41.9% | 56.6% | 43.4% | 0.032 | 0.448 |
| 0 (incorrect/missed) | 401 | 23.9% | 38.9% | 61.1% | 0.012 | 0.269 |

- AUC (posterior, score2 vs score0) = **0.825**; AUC (sim) = 0.775. The frozen evidence is
  informative about human phone correctness, far from perfect.
- Deletion-like rate (match_type miss): score0 → 61.1%; score2 → 16.3% — the model does
  show weak evidence for missing phones, but 1 in 6 correct phones is also "miss"-classified.

## 3. Word-final consonants (so762)

| band | n | exact | exact+soft | miss |
|---|---:|---:|---:|---:|
| all finals (non-vowel) | 4,326 | 85.6% | 90.5% | 9.5% |
| score2 (correct) | 4,115 | 87.3% | 91.8% | 8.2% |
| score1 | 145 | 62.8% | 72.4% | 27.6% |
| score0 | 66 | 33.3% | 50.0% | 50.0% |

Finals are the model's strongest category overall, but the human-wrong (score0) cases are
exactly where it is weakest — the same pattern that produced the LWE deletion misses.

## 4. Experiment C — confusion forensics (model-inferred)

`artifacts/zeroshot/zeroshot_summary.json` → `top_confusions_expected_observed`.
Most frequent correct matches: t (1,559), n (1,405), ɪ (913), s (910), l (817), d (769),
ə (764). The largest actual confusion is **ə → ɑ (414)** — consistent with the adapted
head's biggest gain being schwa (see `AB_ADAPTATION_RESULTS.md`). Vowel confusions dominate
the count; the consonant errors relevant to LWE (final t/d/k/s/z, θ/ð, ɹ) are much rarer in
so762 sentences than in LWE's single-word targets.

## 5. SIAK cross-check

- Canonical agreement 69.1% (miss 24.0%), word-final exact 65.1% (miss 27.7%).
- Pearson(soft score, SIAK expert rating) = **0.312** on these 482 utterances — consistent
  with the 0.307 measured in 1.9.15, confirming the benchmark reproduces the frozen pipeline.

## 6. Runsheet for the A/B

The same script (with `AdaptedPEV`) is reused verbatim by `eval_ab.py`, so baseline and
adapted numbers come from identical audio, alignment and soft-matching code paths; only the
frame posteriors differ.
