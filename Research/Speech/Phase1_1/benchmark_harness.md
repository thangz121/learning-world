# Standard benchmark harness (spec — implement once, reuse per candidate)

One harness, same evaluation structure for every applicable candidate.

## Input
WAV (16kHz mono PCM16 canonical, same as `CapturedSpeech`) + expected phrase +
expected language + candidate ID.

## Output (normalized JSON per run, missing = NOT_AVAILABLE)
- recognized_text, normalized_text, speech_detected
- start_time_s, end_time_s, latency_s, processing_time_s, real_time_factor
- confidence, word_timestamps, phoneme_data, alignment, pronunciation_score
- error_type / failure_type, cpu_pct, peak_ram_mb, gpu_used
- ASR_RECOGNITION fields and PRONUNCIATION_ASSESSMENT fields recorded SEPARATELY

## Metrics (computed, never invented)
- WER / CER where meaningful; for short phrases: exact match, normalized match,
  keyword match, partial match, false accept, false reject
- Timing: audio duration, processing time, RTF, end-to-result latency
- VAD: speech-start/end latency, false speech, missed speech
- Pronunciation (if exposed): word score, phoneme data, stability, error localization

## Notes
- LWE mic path must be represented: reuse `AcousticFixtures` style PCM + real
  `phone_mic_gateway --inject-wav` captures where useful.
- Short-utterance set is mandatory (apple/red/blue/cat/dog/book/big/small/open/close
  + short phrases). Long-form-only tuning is rejected as evidence.
- Child audio: real child only if legal, else SURROGATE-labeled stress set.
