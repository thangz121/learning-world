# CHILD-SPEECH REPRESENTATION AUDIT — WP-1.9.24

Evidence base: WP-1.9.23 feature matrix (609 primary-window tokens; 557 labeled), WP-1.9.21 frame
cache, WP-1.9.24 alternative-encoder counterfactual. Every claim below is marked **OBSERVED**
(measured in the frame evidence) or **HYPOTHESIZED** (a plausible mechanism not yet demonstrated).

## 1. Observed: evidence strength by phone class (labeled tokens)

| class | n | median max_A | one-frame rate | median longest run | median blank | window-sensitive | production recall |
|---|---:|---:|---:|---:|---:|---:|---:|
| stop (t,k,p,d,b,ɡ) | 130 | 0.920 | 80.0% | 1 | 0.933 | 10.0% | 0.950 |
| fricative (s,z,f,v,θ,ð,ʃ,ʒ) | 198 | 0.753 | 64.1% | 1 | 0.898 | 14.7% | 0.891 |
| nasal (n,m,ŋ) | 197 | 0.681 | 43.2% | 2 | 0.923 | 10.2% | 0.958 |
| liquid (ɹ,l) | 28 | **0.109** | 57.1% | 1 | 0.960 | 3.6% | 0.857 |
| affricate (tʃ,dʒ) | 4 | 0.001 | 100% | 0 | 0.813 | 25.0% | 0.500 |

## 2. Observed: the weak-evidence tail is large

- 138 labeled PRESENT tokens have max_A < 0.15; their production recall is 0.630 and their median
  max_A is 0.0069 (essentially no evidence); 94.9% are one-frame events at the 0.05 support level.
- **87 of 528 labeled PRESENT tokens (16.5%) have max_A < 0.02** — i.e. the frozen encoder finds
  effectively no target evidence in the production span. Three are human listening labels:
  `child_01_nine` (PROBABLY_PRESENT, MEDIUM), `child_06_six` (PROBABLY_PRESENT, HIGH),
  `child_07_one` (CLEARLY_PRESENT, HIGH).
- The strongest single false accept (`child_07_seven`) is also a one-frame event (neighbors 0.024 /
  0.005 around a 0.63 peak).

## 3. Observed: systematic phone confusions

`s`↔`z` (62 of 93 /s/ tokens have z as top competitor; 41 of 58 /z/ have s), `m`↔`n` (17 of 20 /m/),
`v`↔`f`/`b`, `t`↔`d`. Liquids are the weakest class (median max 0.005 for /l/, 0.135 for /ɹ/), and
the /r/ subgroup has production FAR 0.833 (n=14 labeled). These are systematic, expected
articulatory confusions — not isolated noise.

## 4. Observed: blank domination and window sensitivity

- Median span-mean blank is 0.87–0.96 across classes; 204/330 labeled final-consonant tokens have
  blank mean ≥ 0.90.
- 10–15% of tokens are window-sensitive/unstable in identity (WP-1.9.22); one-frame support is the
  norm (203/330 finals).
- The second-encoder counterfactual shows the evidence is representation-dependent: 3/10 no-evidence
  so762 presents and 2 LWE weak presents gain strong evidence under `wav2vec2-lv-60-espeak-cv-ft`,
  while some primary-strong cases lose evidence (0.76 → 0.09, 0.59 → 0.14).

## 5. Hypotheses (NOT established)

| hypothesis | status | what would test it |
|---|---|---|
| Short (20 ms) final releases are genuinely one-frame events that the 20 ms CTC frame rate under-resolves | HYPOTHESIZED | frame-level alignment against human-annotated consonant boundaries on a labelled subset |
| Weak frication / reduced /r/ in child speech falls below the model's effective evidence floor | HYPOTHESIZED | alternative encoder or spectral landmark measurement on the same tokens |
| Low-energy endings + high blank posterior reflect soft/devoiced child productions rather than deletion | HYPOTHESIZED | energy/voicing landmark analysis (WP-1.9.12 acoustic features) with new labels |
| Microphone/environment differences (studio vs port mic) explain part of the window sensitivity | HYPOTHESIZED | per-mic comparison on the same speaker/word (LWE has 3 mic tracks) |
| Vietnamese-L1 coarticulation and tone influence final consonant realization | HYPOTHESIZED | Vietnamese-L1 child corpus (does not exist publicly) |

## 6. What is NOT claimed

- Not claimed: "the model is bad for children" (no experimental evidence for that global statement).
- Not claimed: any specific phonetic mechanism (deletion, coarticulation) without frame evidence.
- Not claimed: the encoder is the only remaining error source — the acceptance-layer overlap
  (WP-1.9.23) and label limits (WP-1.9.24 Part A) remain.

## 7. Consequence for B2 design

The B2 target supported by evidence is **missing evidence for weak final consonants (especially
liquids /r/, /l/) and representation instability**, not "strong false evidence on confidently absent
phones" (0/4 confirmed). See `B2_DESIGN_OPTIONS.md` and `B2_DATA_REQUIREMENTS.csv`.
