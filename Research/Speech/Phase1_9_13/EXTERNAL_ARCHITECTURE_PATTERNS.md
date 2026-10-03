# EXTERNAL_ARCHITECTURE_PATTERNS (evidence-supported layers only)

Recurring layers across independent systems (Batch A + Batch B):

```
1. AUDIO QUALITY GATE                      Chivox (E1), SayBananas attrition (E2),
                                           NOCASA zero-rating removal (E2)
2. VAD / EOS                               speak-better (RMS .025×3, 1300ms) (E3),
                                           SIAK server VAD (E2), ELSA endpointing (E2)
3. FIDELITY / ATTEMPT                      Speechace fidelity_class (E1),
                                           Chivox post-proc-failed (E1),
                                           SpeechStep refusal (E0/E1),
                                           SpeechLP voice detection (E1),
                                           SIAK/NOCASA rejected class (E2)
4. TARGET                                  CMUdict (ours), TTS/phonetician reference
                                           (OpenPronounce/speak-better/SpeakStar),
                                           Text2DUnit (surprisal paper, E2)
5. PHONEME / ACOUSTIC EVIDENCE             wav2vec2-lv-60-espeak-cv-ft (3 OSS systems, E3/E4);
                                           HuBERT (PER-MDD, E2); phonological attributes (E2)
6. ALIGNMENT OR ALIGNMENT-FREE             Viterbi 2L+1 (slip/speak-better, E3);
                                           DTW (OpenPronounce, E4); GOP-AF (E2);
                                           retrieval (PER-MDD, E2)
7. DIAGNOSTICS                             omission/insertion/substitution (MS/SpeechSuper,
                                           E1); attribute deviation (E2); heard-phone (E3)
8. SCORING                                 weighted components (0.3/0.4/0.3 OSS, E3);
                                           accuracy/fluency/completeness/prosody (MS/ELSA, E1/E2);
                                           stars (SIAK/M-Speak, E2/E1)
9. CONFIDENCE                              NBestPhonemes scores (MS, E1); lowConfidence flags
                                           (slip, E3); uncalibrated token conf (OSS, E3)
10. AGE / TOLERANCE CONTROLS               SpeechSuper age groups + slack slider (E1);
                                           Chivox age/level + gop_adjust (E1);
                                           SpeechStep age-band feedback policy (E1)
11. CHILD-FACING DECISION                  heard/nearly heard/not heard + stars (SpeakStar, E1);
                                           no numbers on kids (SpeechStep, E1);
                                           attempt reward (Speech Blubs, E1)
12. PARENT / TEACHER MODE                  progress by sound + reports (SpeechLP,
                                           SpeechTherapyMagic, SpeechStep; E1);
                                           SLP dashboards (E1)
```

Not universally present: cloud vs local (both exist), 0–100 (only some), phoneme-level
(most but not child consumer apps), forced alignment (older commercial; newer OSS uses
DTW/GOP/retrieval).
