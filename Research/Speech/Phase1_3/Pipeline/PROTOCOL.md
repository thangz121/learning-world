# LWE Speaking Protocol 1.3.0

Offline process for later Unity integration. No Unity code in this phase.

## CLI
```
D:\speech-lab\venvs\p0\Scripts\python Research\Speech\Phase1_3\Pipeline\speaking_protocol.py \
  --audio path\to\file.wav --target red --out out.json
```

## Request
```json
{
  "audio": "C:/path/file.wav",
  "target": "red",
  "target_version": "cmudict.dict@2026-10-02",
  "pipeline_version": "phase1.2.0-2026-10-02",
  "enable_openpronounce": false,
  "population_label": "unspecified"
}
```

## Response (success)
```json
{
  "ok": true,
  "protocol_version": "lwe-speaking-protocol-1.3.0",
  "recognized_text": "red",
  "score": 91.1,
  "confidence": 0.911,
  "phonemes": [{"expected":"...","observed":"...","status":"ok","score":90.7,"confidence":0.907}],
  "warnings": [],
  "population_label": "..."
}
```

SCORE and CONFIDENCE are separate. Canonical target is CMUdict-based, never a child waveform.
