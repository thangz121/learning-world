# BATCH B · SYSTEM 01 — CHIVOX (Chivox AI)

STATUS: **BLOCKED at execution** (appKey + secretKey signature required; pilot credits via sales
contact). Documentation is unusually rich for child handling (E1).

Key E1 findings:
- Dedicated **young-learner engine**: "calibrated on young learners—not adult speech patterns";
  higher pitch, longer pauses, developing articulation, repetitions/omissions treated as child
  patterns rather than automatic failure.
- **Age/level calibration**: thresholds, feedback language, retry length set per learner stage.
- **Audio quality gate before pronunciation feedback**: check clipping, background speech,
  missing audio, incomplete responses first; "separate low-quality audio retries from
  pronunciation coaching".
- Kernels: `en.nsp.score` (phonics), `en.word.score` (word + per-phoneme; **"adaptive to
  children and adults"**), `en.word.pron` (missed/misread/superfluous), `en.sent.score`
  (overall/fluency/accuracy/integrity + stress/pauses/loss-of-plosion/liaison),
  `en.sent.pron` (per-word correction).
- `gop_adjust` parameter [-1,1] exposed (score adjustment), `voiced`, `accent` (UK/US), rank 4/100.
- Failure mode documented: `post proc failed` → "audio was not successfully recorded or the
  engine did not detect the user's valid voice" → prompt re-record (assessability behavior).
- HTTP: eval.cloud.chivox.com (init/feed/fetch/release); MCP: mcp.cloud.chivox.com (16 tools).
