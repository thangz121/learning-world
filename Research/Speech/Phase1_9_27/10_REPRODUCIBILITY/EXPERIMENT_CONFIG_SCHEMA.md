# EXPERIMENT CONFIG SCHEMA — WP-1.9.27 Part 29

> **Tóm tắt (VI):** Schema cấu hình thí nghiệm để mọi lần chạy tái lập được: định danh, dữ liệu,
> split, features, model, metrics, gates, môi trường. Không có trường ẩn; mọi artifact ghi kèm
> config + hash.

```yaml
experiment_id: pilot_d1_20261007        # unique, immutable
wp: WP-1.9.27
kind: B2_D | B2_E | diagnostic
data:
  manifest: Research/Speech/Phase1_9_27/06_PILOT_DATA/PILOT_DATA_MANIFEST.csv
  manifest_sha256: <recorded at run>
  features: Research/Speech/Phase1_9_27/artifacts/pilot_features.csv
  features_sha256: <recorded at run>
  split_file: Research/Speech/Phase1_9_27/artifacts/pilot_split.json
  labels: Research/Speech/Phase1_9_27/03_LABEL_ANALYSIS/HUMAN_LABEL_RESULTS.csv
  labels_sha256: <recorded at run>
  pack: Research/Speech/Phase1_9_27/06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv
features:
  set: E_ENC | E_ACO | E_TMP | E_ENC_ACO | FULL | D0..D6
  columns: [target_mean, target_max, ...]   # exact list
  normalization: [none | per_speaker_z]
model:
  family: none | logistic | gbm_shallow
  hyperparameters: {C: 1.0}                 # frozen before test
  random_feature_control: true|false
metrics: [frr, far, missing_evidence, auc, per_speaker, assessability, calibration]
gates: 09_SUCCESS_CRITERIA/SUCCESS_CRITERIA.md   # path, not a copy
environment:
  machine: ASUS
  python: D:\speech-lab\venvs\p0\Scripts\python.exe
  torch: <recorded>
  transformers: <recorded>
  seed: 1927
outputs:
  dir: Research/Speech/Phase1_9_27/artifacts/<experiment_id>/
  files: [metrics.json, per_speaker.csv, failure_replay.md, config.yaml]
```

## Rules

- The config is written before the run and never edited afterwards.
- Any artifact without its config + hashes is not citable.
- Test-split readout happens once per config; repeated reads require a new config and are reported
  as such.
- No field may contain prose decisions; thresholds live in the frozen criteria files.
