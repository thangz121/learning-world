# 02 — SPEAKER-DISJOINT CHILD-FOCUSED SPLIT (STEP 3)

**Script:** `experiments/build_child_split.py` · **Manifest:** `child_split.json`
**Seed:** 1515 (inherited from 1.9.15) · **Source rev:** `06385584…f093f33e`

---

## Split (child speakers only)

| split | speakers | utterances | phone tokens | bad (<0.5) tokens |
|---|---|---|---|---|
| train | 80 | 1,600 | 26,739 | 434 |
| validation | 18 | 360 | 6,045 | 32 |
| test | 24 | 480 | 7,853 | 48 |
| adult (excluded) | 128 | 2,560 | 53,808 | 2,889 |

- **Speaker overlap:** `[]`. **Test-speaker leakage into train/val:** `[]`.
- Adults are kept in a clearly-labeled separate set and **never** enter the child
  adaptation split (no adult mixing, no ablation mixing claimed here).
- Child bad tokens total 434+32+48 = 514 — matches Phase 1.9.17 exactly.

## Distributions

Age distribution, error-type (sub/del/unk) distribution, and top-phone
histograms per split are recorded in `child_split.json`. Test age bands present:
6, 7, 9, 10, 11, 12, 13, 15.

## Hours

Duration is **NOT_MEASURED** here (annotation-only pass). Utterance counts and
token counts are exact; audio duration is available from the zero-shot pass
(mean 0.94 s/utt processing; SO762 clips are 1–3 s each).

## Leakage discipline

- Test speakers are held out entirely; B1 thresholds are never tuned on test.
- No utterance-level random split that would leak speakers.
