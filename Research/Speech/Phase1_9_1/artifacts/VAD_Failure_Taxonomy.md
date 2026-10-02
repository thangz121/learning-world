# VAD Failure Taxonomy — Phase 1.9.1 Gate 3

Classes **observed** in current real + controlled data (not a priori):

| Class | Evidence |
|---|---|
| **A. Clean / strong contrast speech** | NEW clean: cont_energy≈0.23, contrast≈0.88, Silero 38 segs |
| **B. Continuous-energy difficult recording** | IMG: cont≈0.97, contrast≈0.31, Silero 0 segs |
| **C. Continuous-energy non-speech vs speech** | **Unresolved without human labels** on IMG hybrid clips |
| **D. Low-SNR speech (synthetic white)** | NEW+white noise SNR curve; Silero F1 drops 1.0→0.4 |
| **G. Short fragmented candidates** | IMG hybrid: mean dur≈0.25s, all <0.5s, 9/28 <0.2s |
| **H. Short LWE utterances** | red/cat/apple etc. (benchmark) |

Not yet evidenced as separate labeled classes in this corpus: music-only, multi-speaker BG speech, explicit hum (would need more data).

## Implication for hybrid
hybrid_score fires on class **B** (IMG) where Silero fails; quality of those segments is **Gate 2 human-review dependent**.
