# slip — Architecture (E3)

```
TEXT ──espeak-ng WASM / CMUdict──▶ expected IPA tokens (392-vocab match)
MIC ──16k resample──▶ wav2vec2-lv-60-espeak-cv-ft (int8, worker/ONNX)
   → frame log-posteriors (50 fps, 20 ms)
        ↓
(2L+1) Viterbi CTC forced align → spans per expected token (present flag)
        ↓
GOP per phone: GOP-Avg, GOP-ratio, heard phone, severity, lowConfidence
        ↓
word-level highlight + "tap to scrub to the slipped sound" UX
```

No VAD/EOS documented in the repo (mic capture handled by the app); optional server seam
mirrors the same dsp/align/gop code.
