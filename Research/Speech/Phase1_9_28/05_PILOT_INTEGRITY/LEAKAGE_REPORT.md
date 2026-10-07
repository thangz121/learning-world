# LEAKAGE REPORT - WP-1.9.28 Part 10

> **Tóm tắt (VI):** `pilot_checks.py` chạy lại: toàn bộ kiểm tra rò rỉ PASS. Không có speaker trùng
> split, audio trùng, utt trùng, hay trường label/machine trong manifest. Nguồn: pilot_checks.json.

## Results (machine-readable source: `Phase1_9_27/artifacts/pilot_checks.json`)

| check | value | pass |
|---|---:|---|
| train_dev_overlap | 0 | yes |
| train_test_overlap | 0 | yes |
| dev_test_overlap | 0 | yes |
| duplicate_audio_paths | 0 | yes |
| duplicate_speaker_text_pairs | 0 | yes |
| utt_overlap_train_test | 0 | yes |
| label_fields_in_manifest | [] | yes |
| machine_score_fields_in_manifest | [] | yes |
| manifest_rows | 200 | yes |
| manifest_pass | 200 | yes |
| **leakage_pass** | **true** | yes |

## Coverage of the required leak classes (Part 10)

- speaker overlap: covered (0).
- duplicate audio: covered (0).
- session overlap: SO762 recording is one session per speaker; no session column exists, and the
  speaker-disjoint split therefore covers it (documented limitation).
- target-word leakage: duplicate speaker+text pairs 0.
- label leakage: no label columns in the manifest; features contain no labels.
- machine-score leakage: no machine fields in the manifest; the review payload is blind.
- alignment leakage: no alignment outputs are used as labels; alignment is AUTOMATIC evidence only.

## Pool statistics (context)

122 child SO762 speakers; 45 used in WP-1.9.x; 86 clean unused (1.712 h); the pilot subset is 10
speakers / 0.196 h / 546 tokens / 8 /r/.

## Verdict

No leakage condition; `PILOT_BLOCKED_DATA_INTEGRITY` not triggered.
