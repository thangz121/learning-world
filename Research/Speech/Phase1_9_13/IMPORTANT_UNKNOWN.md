# IMPORTANT_UNKNOWN — what we still cannot know

1. **Exact child models**: Chivox "dedicated young-learner models", SpeechSuper age groups,
   SpeechStep child engine, SpeechTherapyMagic child engine, SayBananas template matching —
   none disclose architecture, training data, or thresholds.
2. **What age settings actually change**: SpeechSuper (3~6/6~12/>12) and Chivox age/level
   calibration expose controls, but the mechanism (model/threshold/calibration) is unknown.
3. **What leniency changes**: Chivox `gop_adjust` and SpeechSuper `slack` semantics are
   undocumented internally.
4. **Proprietary calibration**: human-calibration datasets and score mappings of all commercial
   systems are unpublished.
5. **Internal confidence models**: none of the commercial systems expose calibrated confidence
   (Microsoft's NBestPhonemes is the closest public evidence).
6. **Child behavior of commercial APIs**: none is verified on children; all child claims are
   marketing-level (E0/E1).
7. **SayBananas template matching implementation**: study describes behavior, not algorithm
   details (similarity metric, thresholds).
8. **SpeechStep/Chivox assessability internals**: "declined rather than guessed" and
   "post proc failed" are documented outcomes, not decision rules.
9. **NOCASA access**: EULA-gated; per-speaker metadata released only after the challenge.
10. **Vietnamese-L1 child data**: no public corpus; L2-ARCTIC includes adult Vietnamese speakers
    only. SIAK/speechocean762 are Finnish/Mandarin L1.
11. **Latency of research methods on CPU**: GOP-AF, PER-MDD, phonological-feature MDD do not
    report CPU latency for offline Windows targets.
12. **Model licenses**: wav2vec2-lv-60-espeak-cv-ft and hubert-large-ls960-ft model cards not
    yet verified for commercial use.
