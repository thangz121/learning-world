# PILOT DATA CONTRACT — WP-1.9.27 Parts 10–13

> **Tóm tắt (VI):** Hợp đồng dữ liệu pilot đã đóng băng: 10 speaker SO762 CHƯA từng dùng trong
> WP-1.9.x, tuổi 6–7, 200 utterance (120/40/40), 546 token phụ âm cuối, 0,196 h, QC PASS 200/200,
> leakage PASS. Có 2 pack nhãn: Pack R (276 ca đánh giá cũ) và Pack P (546 token pilot) — cùng
> server mù. Không huấn luyện, không đổi production.

## Frozen contract

| element | value | source |
|---|---|---|
| speakers | 10, speaker-disjoint from all WP-1.9.x sets | `artifacts/pilot_split.json` |
| utterances | 200 (train 120 / dev 40 / test 40), 20 per speaker | `pilot_data_summary.json` |
| age | 6 (1 speaker) / 7 (9 speakers) — youngest clean SO762 subset | manifest |
| audio | SO762 child subset, local, 16 kHz WAV; no modification | manifest |
| hours (PASS) | 0.196 h | summary |
| final-consonant tokens | 546 across 20 phone classes (top: T 95, N 90, Z 81, S 64) | summary |
| /r/ tokens | 8 | summary |
| QC | PASS 200/200; excluded 0; clipping max 0.0; min SNR proxy 25.5 | manifest |
| leakage | train/dev/test overlap 0; duplicate audio 0; utt overlap 0 | `pilot_checks.json` |
| machine features | `artifacts/pilot_features.csv` (546 tokens, production + alt evidence) | extracted this WP |

## Rules

- The frozen split (`pilot_split.json`, seed 1927) must never be re-derived after seeing results.
- Test speakers (1075, 1076) never enter any adaptation/training/feature-fitting step.
- No utterance-level random split; no cross-split speaker.
- Audio QC exclusions are recorded with reason; 0 exclusions here.
- Natural distribution is kept; diagnostics (/r/, weak finals) are oversampled only via candidate
  selection, never by duplicating or synthesizing audio.

## Label sets (two packs, one validated server)

| pack | file | candidates | speaker base | purpose |
|---|---|---|---|---|
| Pack R | `Phase1_9_26/01_HUMAN_LABEL_ACQUISITION/REVIEW_CANDIDATES.csv` | 276 | 49 (pre-1.9.27 evaluation set) | TYPE-B, /r/, weak-present/false diagnostics |
| Pack P | `06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv` | 546 (`P27-###`) | the 10 frozen pilot speakers | B2-D/B2-E pilot labels |

Pack P was generated with the same focus-token logic as `pilot_runner.py`; token ids match
`pilot_features.csv` 546/546 and all audio references resolve. It is served by the same server:

```
python serve_review_1927.py 8791 --pack 06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv
```

Minimum pilot label set: 60 PRESENT + 60 ABSENT; ideal: all 546 attempted, giving per-split coverage
(train 337 / dev 110 / test 99 tokens). Reviewers stay blind (blind_id, word, target_phone only).

## Consumption path (ready)

1. Reviewer exports CSV (or server JSONL store).
2. `label_analysis.py --reviews <dir> --pack 06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv` produces
   consensus + agreement + adjudication + sufficiency.
3. `pilot_runner.py --stage eval` (labels required) then `--stage report` (future): per-speaker,
   /r/, assessability and failure-replay summaries join labels to `pilot_features.csv` by `token_id`.

## Prohibited in this WP

Encoder fine-tuning, LoRA, head training, B2-D/B2-E training, model replacement, production edits,
Unity work. `pilot_runner.py --allow-training` prints a hard stop by design.
