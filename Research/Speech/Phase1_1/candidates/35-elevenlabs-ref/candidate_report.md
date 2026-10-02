# Candidate 35-elevenlabs-ref

## Source
ElevenLabs TTS API (reference-audio generator ONLY — never a scorer).

## Version
NOT EXECUTED 2026-10-02.

## License
Commercial SaaS (ToS). RESEARCH_ONLY as cache-once reference generator.

## Model License
N/A (API).

## Dependency License
HTTPS client only.

## Intended Role
Canonical teaching / reference audio for target words. NOT waveform-similarity scoring.

## Installation
Env-only: `ELEVENLABS_API_KEY` or `ELEVEN_API_KEY`. Never hardcode in Unity/git/assets.

## Runtime
Network. Cache once per (text, voice, rate, format) → local L2 like existing AudioCache.

## Tests
Env scan on ASUS 2026-10-02: **NO ELEVEN* variable present** → API call skipped (no key fabrication).

## Results
NOT TESTED (no key in environment).

Planned verification when key available:
1. TTS "apple" twice, same voice/settings.
2. Record format (mp3/pcm), sample rate, latency, bytes.
3. Compare SHA of two runs (reproducibility).
4. Never use as GOP/DTW reference waveform for child scoring.

## Baseline Comparison
Existing production TTS = Cloudflare Worker (`CloudflareTranslateTtsProvider`). ElevenLabs would be an alternative reference generator only.

## Improvements
None measured (not run).

## Problems
- Key absent on research machine.
- Must not become "correct = sounds like ElevenLabs".

## Unique Capability
High-quality adult/child-like teaching audio with possible IPA/pronunciation controls (to verify when key available).

## Retention Decision
HOLD — procedure documented; execute when key provided. RESEARCH_ONLY role if retained.

## Future Combination
SpeakingTarget.canonicalReferenceAudio = cached ElevenLabs OR Cloudflare pregen; scoring still uses CMUdict + phoneme evidence, never waveform distance to TTS.
