# PILOT RECORDING QC — WP-1.9.27 Part 13

> **Tóm tắt (VI):** QC khách quan từng utterance: duration, sample rate, clipping, RMS, silence
> ratio, SNR proxy, voiced fraction; luật loại trừ rõ ràng, có lý do — không xóa âm thầm.
> Kết quả hiện tại: 200/200 PASS, 0 loại trừ.

## Measured quantities (per utterance, in `PILOT_DATA_MANIFEST.csv`)

| field | definition |
|---|---|
| duration_s | length in seconds |
| sample_rate | Hz as stored |
| clipping_frac | fraction of samples with |x| > 0.99 |
| rms | RMS over the file |
| silence_ratio | fraction of 20 ms frames below 1% of peak frame RMS |
| snr_proxy | 20*log10(p90 frame RMS / p10 frame RMS) |
| voiced_fraction | fraction of pitch frames voiced (parselmouth, dur >= 0.2 s) |
| n_final_consonants / final_phones | target final-consonant inventory from reference phones |

## Exclusion rules (frozen)

| rule | threshold | reason code |
|---|---|---|
| too short | duration < 0.4 s | TOO_SHORT |
| clipping | clipping_frac > 0.02 | CLIPPING |
| low SNR | snr_proxy < 5 dB | LOW_SNR |

Exclusions are kept in the manifest with `qc_status=EXCLUDE`, `exclude_reason`, and are never
silently deleted. Speaker, utterance and timestamp remain attached for audit.

## Current results (2026-10-07)

- 200 utterances, PASS 200, EXCLUDE 0.
- clipping max 0.0 (no clipping detected); min SNR proxy 25.5 dB.
- hours PASS 0.196 h; all 10 speakers represented (20 utts each).
- QC was computed by `experiments/pilot_data_prep.py`; no audio was modified.

## Notes

- SNR proxy is a relative frame-energy statistic, not calibrated dB SNR; it is used only to catch
  gross noise problems.
- Voiced fraction is descriptive; it is not an exclusion criterion.
- If future label review marks a token NOT_ASSESSABLE, that is a label, not a QC exclusion; both are
  reported separately.
