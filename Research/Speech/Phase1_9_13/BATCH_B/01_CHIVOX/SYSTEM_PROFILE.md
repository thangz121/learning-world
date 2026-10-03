# CHIVOX — System Profile

| Field | Value | Evidence |
|---|---|---|
| Vendor | Chivox AI (China; exam-grade speech assessment >10 years) | E0/E1 |
| Target | EdTech; explicit young-learner product line | E1 |
| Child engine | Dedicated young-learner models; age/level calibration | E1 (claims, no paper) |
| Age handling type | **Threshold + calibration + feedback-language + retry-length settings** (documented); dedicated models claimed | E1 |
| Diagnostics | phoneme/word evidence, accuracy, fluency, integrity/completeness, stress, intonation, pauses, insertions, omissions, repetitions | E1 |
| Correction kernels | missed reading / misreading / superfluous reading per phoneme/word | E1 |
| Audio quality | clipping / background speech / missing audio / incomplete response checks before feedback | E1 |
| API | HTTP (eval.cloud.chivox.com), WebSocket streaming, native SDKs, MCP (16 tools) | E1 |
| Auth | appKey + secretKey signature (sha256) — no anonymous access | E1 |
| Free path | pilot credits via contact form ("reply within one business day") | E1 |
| Open source | no (MCP local proxy is a client, not the engine) | E1 |
| Child pitch/pauses | explicitly named as child patterns to handle | E1 |
| Failure signal | `post proc failed` → re-record prompt (no valid voice / bad recording) | E1 |
| Model architecture | UNKNOWN (proprietary) | — |
| Papers | none found with implementation detail | E1 (search) |
