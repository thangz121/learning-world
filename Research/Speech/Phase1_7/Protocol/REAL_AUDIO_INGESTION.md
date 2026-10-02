# Real-audio ingestion protocol v1.7

## Preserve original
1. Hash SHA-256 before any processing
2. Never overwrite original
3. Store external copy under `D:\speech-lab\data\real-audio\`
4. Do not commit real audio to git

## Manifest fields (only known values)
```json
{
  "audio_id": "IMG_0639",
  "source_file": "IMG_0639.mp3",
  "sha256": "...",
  "duration_s": 219.284,
  "language": "UNKNOWN",
  "speaker_age_months": null,
  "speaker_age_status": "unknown",
  "consent_status": "unknown",
  "target_known": false,
  "research_role": ["REAL_SPEAKER_UNKNOWN_AGE", "VAD_STRESS_CASE"],
  "privacy_sensitive": true
}
```

## Processing
1. ffprobe original
2. derive 16k mono WAV
3. quality metrics
4. VAD (document failures)
5. ASR/LID probes with model limitations labeled
6. pronunciation scoring **only** if authorized known target exists
