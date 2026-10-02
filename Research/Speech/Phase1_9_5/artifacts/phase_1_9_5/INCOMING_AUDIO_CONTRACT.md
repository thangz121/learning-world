# Incoming Audio Contract — Phase 1.9.5

## Directory

`Research/Speech/Phase1_9_5/incoming-audio/`

Do **not** populate with short LWE benchmark WAVs or concatenations.

## Required files

### 1. IMG_0639 source

| Field | Requirement |
|---|---|
| Expected filename | `IMG_0639.mp3` |
| Authoritative SHA-256 | `E4CDF24D2B5140B91EF637676CD5FA1B9A7F6B6A629F62AE9AC2657CB171A6F7` |
| Outcomes | `EXACT_SOURCE_MATCH` / `FILE_EXISTS_BUT_HASH_MISMATCH` / `SOURCE_NOT_FOUND` |
| On mismatch | **stop** — do not substitute derived/clips |

### 2. Long recordings (≥2)

| Field | Requirement |
|---|---|
| Duration | ≥10 s each (prefer ≥20 s) |
| Identity | unique SHA-256; not crops/duplicates of each other |
| Content | real recording; not TTS/synthetic/noise-only fake |
| Provenance | source + license/permission known |

## Per-file metadata (mandatory)

- original_filename, SHA256, byte_size, duration  
- sample_rate, channels, codec/container  
- provenance, source, license/permission  
- recording origin; original vs derived; derivation chain if derived  
- speaker_count, language, recording_condition if known  

## Reference classification (never collapse)

`REAL_SOURCE` | `DERIVED_ANALYSIS_COPY` | `WEAK_REFERENCE` | `HUMAN_REFERENCE`
