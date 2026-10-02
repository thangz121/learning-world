# Real-child pilot — infrastructure ready, execution status

## Status: PENDING_EXECUTION

No real-child audio is present on this machine (`D:\speech-lab` has no `real-child` dataset).

## Ready artifacts
- Protocol: `Research/Speech/Phase1_3/Reports/REAL_CHILD_VALIDATION_PROTOCOL.md` (still authoritative)
- Scoring instrument: Phase 1.5 protocol v3 (`Pipeline/speaking_protocol_v3.py`)
- Manifest template below

## Manifest template (JSONL or CSV)
```
subject_id,age_months,device,mic,environment,distance_cm,condition,target_id,target_text,take,wav_path,consent_version
child_001,52,phone,builtin,home_quiet,30,imitation,sheep,sheep,1,/data/real-child/.../sheep_t1.wav,v1
```

## Conditions
1. imitation (listen→repeat)
2. elicitation (picture→produce)
3. spontaneous (context)

## Analysis plan when data arrives
- Run protocol v3 per WAV
- Aggregate by child/mode/target without ranking children as “more correct”
- Report FP/FN vs human review subset
- **Never** redefine CMUdict targets from child averages

## Storage
Outside git. No PII in filenames.
