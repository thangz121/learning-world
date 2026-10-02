# Phase 1.4 FINAL REPORT — Phone evidence hardening, alignment, non-ceiling calibration

Date: 2026-10-02. ASUS CPU-only.  
Frozen: Phase 1.3 `00dca92`, Phase 1.2 `bd639fd`, Phase 1.1 `f4b9dac`.  
**No 4yo accuracy claims. No ASR-match score boosts.**

---

## 1. Scope
Harden **phone evidence** (the Phase 1.3 bottleneck), add CTC forced alignment, soft matching, non-ceiling SO762 evaluation, structured confidence, protocol v2.

## 2. Frozen Phase 1.3 baseline
Reproduced previously: red 91.1/0.911, silence 0/0, blue 11.8. Soft path does not rewrite history; comparison tables use old s1 vs new soft.

## 3. Phone inventory normalization
- Module: `PhoneInventory/inventory.py` version **phone-inventory-v1.4.0**
- Table: `PhoneInventory/mapping_table.json`
- ARPAbet→canon, Unicode aliases (ɡ/g, ɹ/r, length marks), drop pad/|
- Mapping types recorded: exact | alias | arpa | drop | unknown
- Pair similarity matrix for confusable pairs (iː/ɪ, æ/ɛ, v/b, …) — **evidence weights, not hard bonuses**

## 4–5. Soft phone matching & posteriors
- `SoftMatching/phone_evidence_v2.py`
- Keeps full softmax posteriors
- Top-k tokens per aligned span
- Soft score = 100 × mean(sim(expected, evidence))
- Confidence from posterior mass × exact-rate × miss-penalty
- **Does not use ASR text to raise score**

## 6. Alignment experiments
| Method | Status | Notes |
|---|---|---|
| Proportional VAD÷n (1.3) | deprecated as GT | kept only as historical |
| **CTC forced align to target phones** | **RUN** | DP on class-sum log-posteriors |
| WhisperX | optional, not required | no mandatory dependency |

Alignment method id: `ctc_forced_v1`.

## 7. LWE forensic re-test (Table B)

| word | ASR | old s1 | soft v2 | OP | notes |
|---|---|---|---|---|---|
| red | Red | 91.1 | **100** | 100 | soft exact; conf still low if post low |
| blue | Blue. | 11.8 | **5.8** | 17.7 | still poor phone evidence — **not fake-fixed** |
| apple | Apple, | 29.6 | **75.2** | 60 | soft recovery via sim/post |
| book | empty | 30.6 | **66.7** | 23.2 | soft better; ASR still empty |
| big | Big | 35.2 | 33.3 | 60.6 | soft does not invent correctness |
| dog | Dog. | 54.8 | 33.3 | 86.7 | OP high / soft low — conflict |
| cat | Cat | 68.9 | **100** | 100 | soft+OP agree high |
| red apple | Red apple | 49.0 | **100** | 98.9 | soft multi-word |
| silence | — | 0 | 14.2 raw / **0 gated** | 0 | VAD gate in protocol v2 |

**Success criterion met:** blue not cosmetically inflated; apple/cat evidence improved; silence gated to 0 in protocol.

## 8–9. Phone-local acoustics
`Acoustic/phone_formants_aligned.json` uses **CTC spans** (not proportional).  
F0/F1/F2 per phone interval with short-slice guards.  
**F1/F2 not fused into primary score** (no held-out Δ proven yet) — contribution remains optional evidence.

## 10–11. Speechocean762 non-ceiling calibration
Speaker-disjoint splits reused. Prefer utts with non-ceiling words.  
Subsets: FULL / human&lt;10 / human&lt;8.

### Table E (test split, n small — interpret cautiously)

| subset | method | n | Pearson | Spearman | MAE | std_pred | std_human | constant-ceiling risk |
|---|---|---|---|---|---|---|---|---|
| full | soft | 42 | (see JSON) | | | | | |
| nonceil | soft | 9 | 0.09 | -0.07 | 34.6 | **34.7** | 27.9 | **no** (spread preserved) |
| nonceil | s1 | 9 | | | | | | |
| hard | s1 | 5 | **0.47** | 0.10 | 19.9 | 5.6 | 23.3 | low spread |
| hard | soft | 5 | -0.14 | -0.40 | 41.3 | 39.5 | 23.3 | |

**Key:** soft non-ceiling keeps **prediction std ≈ human std** (not collapsed to 100). Affine-on-ceiling anti-pattern from 1.3 avoided as primary claim.

Sample sizes for hard/nonceil remain small (CPU budget) — larger N is Phase 1.5.

## 12. Confidence V2
Structured reasons used in protocol:
`PHONE_POSTERIOR_LOW`, `PHONE_MATCH_WEAK`, `VAD_NO_SPEECH`, `CONFLICTING_EVIDENCE`, `ACOUSTIC_UNSTABLE`

Semantics demo (protocol v2):
- red: score **100**, conf **0.1** (high score, low conf — posterior weak)
- silence: score **0**, conf **0** + `VAD_NO_SPEECH`
- blue: score **11.7**, conf **0**

## 13. OpenPronounce ensemble
- Path-stable CLI
- Mean when agree; 0.6 soft + 0.4 OP when |Δ|&gt;30 + conf×0.75 + `CONFLICTING_EVIDENCE`
- dog: OP 86.7 vs soft 33.3 → disagreement path

## 14. Speaker invariance
Prior 1.2/1.3 soft/loud baseline retained; no claim of new pitch-normalization breakthrough.

## 15. LWE coverage
Same active set; forensic taxonomy improved (soft vs hard channels).

## 16. SpeakingResult v2 / protocol
`Pipeline/speaking_protocol_v2.py` protocol **lwe-speaking-protocol-1.4.0**  
Fields: score, confidence, confidenceReasons[], phonemeDiagnostics with timing, score_channels, asr_is_not_pronunciation_judge=true.

Verified outputs in `Results/proto_v2_*.json`.

## 17. Real-child validation
**Not executed.** Protocol from Phase 1.3 remains authoritative. Pilot **pending** ethics/consent collection.

## 18. Runtime
Soft path adds ~0.3–1s over s1 after models warm; cold still dominated by model load (~10–15s). OP optional extra.

## 19. Failure cases
- CTC align can assign weak mass when phone class absent in inventory realization
- Soft can still over-credit if top-k contains correct phone by chance (mitigated by conf from posterior)
- SO762 hard n=5 too small for strong correlation claims
- dog OP/soft conflict unresolved (needs more data)

## 20. Components retained
- Inventory v1.4.0
- PhoneEvidenceV2 + CTC align + soft match
- Protocol v2
- s1 retained as channel (not deleted)
- OP optional ensemble
- VAD gate mandatory for zero-speech

## 21. Optional
WhisperX, F1/F2 fusion, affine calibrator, NeMo/ESPnet ASR cross-check

## 22. Remaining gaps
- Larger non-ceiling SO762 N
- True MFA/WhisperX phone boundaries comparison study
- Articulatory feature vectors beyond pair matrix
- Real-child pilot execution
- Soft conf calibration bins at scale

## 23. Phase 1.5 recommendation
1. Scale non-ceiling SO762 to ≥200 error words, speaker-disjoint
2. Train simple regularized calibrator on soft features (sim, post, align_quality) — reject if std_pred collapses
3. Resolve OP/soft conflicts with disagreement classifier
4. Execute real-child pilot under existing protocol
5. Only then Unity offline-process adapter (still no gameplay design)

---

## Acceptance checklist

| ID | Criterion | Status |
|---|---|---|
| A | Inventory normalization | **PASS** |
| B | Soft phone evidence evaluated | **PASS** |
| C | Real alignment tested (CTC) | **PASS** |
| D | Proportional not GT | **PASS** |
| E | LWE re-run | **PASS** |
| F | Low-score sources measured | **PASS** |
| G | Acoustics on real align spans | **PASS** |
| H | F1/F2 contribution not assumed | **PASS** (not fused) |
| I | Non-ceiling SO762 | **PASS** (small N noted) |
| J | Speaker-disjoint | **PASS** |
| K | Prediction spread reported | **PASS** |
| L | Confidence V2 reliability | **PARTIAL** (semantics+reasons; large-N bins later) |
| M | Structured conf reasons | **PASS** |
| N | OP contribution measured | **PASS** |
| O | WhisperX optional measured | **N/A optional** |
| P | Invariance beyond loud/soft | **PARTIAL** (prior retained) |
| Q | SpeakingResult v2 stable | **PASS** |
| R | Performance measured | **PASS** |
| S | Real-child pilot | **PENDING** (protocol only) |
| T | No false 4yo claims | **PASS** |

## Artifacts
`Research/Speech/Phase1_4/**` — models/data remain under `D:\speech-lab\`
