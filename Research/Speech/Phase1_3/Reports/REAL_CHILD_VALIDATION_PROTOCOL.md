# Real-child validation protocol (later phase — NOT Phase 1.3 execution)

## Principles
- Child audio = **validation / robustness only**
- Canonical correctness remains CMUdict/IPA — **never one child's voice**
- No asking a preschooler to *read* English text

## Consent & privacy
- Written guardian consent before any recording
- Anonymized IDs only (no full name in filenames)
- Store age in months, not birthday
- Restricted access store outside git
- Right to withdraw → delete audio + manifest row

## Metadata (manifest CSV/JSON)
- subject_id (anonymous)
- age_months
- sex (optional)
- L1 / home language
- device (phone/headset/laptop)
- mic_distance_cm (approx)
- environment (quiet_room / home / classroom)
- condition: imitation | elicitation | spontaneous
- target_text / target_id
- take_index
- wav_path
- sample_rate (prefer 16000 mono PCM)
- consent_version
- notes

## Conditions
1. **Imitation:** play reference (TTS cache) → child repeats (1–3 takes)
2. **Elicitation:** picture/object/question → child produces target (no orthography)
3. **Spontaneous:** game-like context → free speech (label targets post-hoc if clear)

## Target list
Start from LWE Active words with phoneme coverage gaps filled later.  
Prefer single words then short phrases already in CMUdict.

## Format
- WAV 16 kHz mono PCM16
- Filename: `{subject_id}_{condition}_{target_id}_t{take}.wav`
- No video required for core validation

## Analysis uses
- False positive / false negative vs adult-calibrated scorer
- Speaker-variation stability across children
- Confidence calibration under child acoustics
- **Does not redefine** phone targets from child averages

## Out of scope for this protocol doc
- Actual recruitment
- Gameplay design
