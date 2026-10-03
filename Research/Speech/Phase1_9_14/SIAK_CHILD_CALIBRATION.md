# SIAK CHILD CALIBRATION

**Scripts:** `experiments/siak_metadata.py`, `experiments/siak_calibration.py`
**Artifacts:** `artifacts/siak/siak_metadata.json`, `siak_speakers.csv`, `siak_scored.csv`
(1,074 rows), `siak_summary_ages4-5-6-7-8-9-10.json`
**Release:** `Research/Speech/ExternalData/SIAK/` (gitignored; license CC-BY-ND-4.0)

---

## 1. What the release is (inspected, not assumed)

| property | value |
|---|---|
| utterances | 16,308 (train.csv 12,308 + test.csv 4,000; 16,308 flac files indexed) |
| annotation fields | `file`, `utterance`, `score` only |
| score | single expert annotator, 0–100 (paper maps to 0–5 stars); **min 1, max 100, no zeros** |
| utterances | single word or short phrase, dashes for spaces; 320 unique targets |
| speakers | 172; `train`/`test` numbers disjoint across splits |
| L1 | fifi (Finnish) 13,161 · enuk (UK English) 2,434 · othr 713 |
| ages (filename, years) | 4:44 · 5:420 · 6:130 · 7:1,198 · 8:3,973 · 9:6,360 · 10:2,647 · 11:1,284 · 12:252 |
| age 4–6 subset | **594 utt / 5 speakers** (train001_fifi 420, train070_othr 44, train110_fifi 19, test143_enuk 89, test160_fifi 22); 222 unique targets |
| license | CC-BY-ND-4.0; README: commercial use of speech samples for building/evaluation of speech technology models is *not* prohibited; no unrelated derivatives; legal review still listed as an open blocker |

“Age 4–6 = 594” is exactly the value carried in the 1.9.13 handoff; it is now re-derived from
the filenames, and the ages are **actual years** (2-digit), not assumed.

## 2. The rejected / zero-rating question (Phase spec §12)

What 1.9.13 reported from the SLATE 2023 paper (E2, external documented): 1,489 items were
**rejected** before scoring — silence, interrupted, wrong word, spoken noise, lack of effort.

What this release contains: **no score-0 rows, no rejection class, no rejection reason**.
The minimum score is 1 (198 items, spread across ages); those are *rated* items (score 1/100
= very poor pronunciation), **not** rejections. The rejected items are absent from the
release.

Conclusion: SIAK provides **documented (paper-level) support** for a pre-scoring
assessability/rejection gate — the closest thing to an external child-specific confirmation
that such a gate is needed — but the released data **cannot empirically validate** an
assessability classifier. Treating score-1 items as “rejected/unusable” would be
retrofitting and is explicitly rejected here.

## 3. Calibration run (frozen soft-v2, no training, no calibration)

1,074 utterances scored: all age 4–6 (594) + 120 sampled per age 7–10 (480). Same model and
code as P2 (`PhoneEvidenceV2@1.4.0`, `wav2vec2-xlsr-53-espeak-cv-ft`, Apache-2.0).
Runtime 335 s CPU.

| group | n | Pearson | Spearman | mean soft | mean SIAK | FRR-proxy (soft<50 on SIAK≥80) |
|---|---:|---:|---:|---:|---:|---:|
| all | 1,074 | 0.226 | 0.222 | 60.8 | 56.7 | 24.6% |
| age 4 | 44 | 0.140 | 0.151 | 55.1 | 36.3 | 40.0% |
| age 5 | 420 | 0.145 | 0.141 | 56.1 | 52.0 | 33.3% |
| age 6 | 130 | 0.295 | 0.298 | 54.2 | 58.6 | 34.0% |
| age 7 | 120 | 0.193 | 0.182 | 63.1 | 58.7 | 21.4% |
| age 8 | 120 | 0.340 | 0.323 | 65.2 | 61.7 | 19.5% |
| age 9 | 120 | 0.178 | 0.188 | 68.9 | 65.1 | 11.3% |
| age 10 | 120 | 0.210 | 0.207 | 71.3 | 62.9 | 15.2% |
| test split (speaker-disjoint) | 207 | 0.263 | 0.236 | 59.6 | 59.9 | 26.7% |

Speaker-level (blocks with n ≥10, `siak_summary_...json`): Pearson ranges 0.10–0.66 across
speakers; age 4–6 speakers: test143_enuk 0.34 (n=89), test160_fifi 0.24 (n=22),
train001_fifi 0.15 (n=420), train070_othr 0.14 (n=44), train110_fifi 0.23 (n=19).

Interpretation:
- Zero-shot correlation **0.22–0.26** is far below SIAK's own trained systems (0.59–0.61,
  E2). The frozen adult-trained pipeline is **not** a child assessor.
- The age gradient (means rising 55→71 with age; FRR-proxy falling 40%→11%) shows the
  system's behavior tracks adult-likeness of the speech, not just pronunciation quality.
- Speaker-disjoint (SIAK test) numbers are consistent with the overall result
  (0.263/0.236), so this is not a same-speaker artifact.
- No calibration was applied; the numbers are the honest zero-shot state.

## 4. Speaker leakage risks

- SIAK's train/test split is speaker-disjoint by filename ID; cross-split speaker-ID
  intersection = ∅.
- Within a split, each speaker repeats targets many times over months (train001: 420 items).
  Any future fine-tuning or calibration **must hold out speakers**, never utterances.
- Physical-identity leakage across IDs cannot be excluded (anonymised release), so
  speaker-disjoint results are the strongest claim available.

## 5. What SIAK does / does not provide

Provides:
- a child-specific, human-scored benchmark with real age metadata (calibration target);
- documented evidence that a mature child pipeline rejected 1,489 samples pre-scoring;
- speaker-disjoint evaluation design.

Does not provide:
- the rejected samples or their reasons (cannot validate the assessability layer);
- more than 5 speakers at ages 4–6 (all age-4 data is one speaker);
- LI-Finnish/UK-English only; no Vietnamese-L1 children (LWE population unsolved);
- multi-annotator reliability (single annotator).

## 6. speechocean762

Not downloaded in this phase (open blocker from 1.9.13). No complementary benchmark result is
reported, and none is claimed.
