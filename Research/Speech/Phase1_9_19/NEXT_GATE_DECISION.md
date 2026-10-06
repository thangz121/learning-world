# NEXT GATE DECISION — WP-1.9.19

**Final gate:** **MIXED_WINDOW_AND_ACOUSTIC**

After controlling the audio boundary/context across a pre-declared family of 11 reasonable
windows, both effects are material and separable:

- **Window/context effect:** 3/16 human-present final consonants go from <0.1 evidence at
  full to ≥0.3 under reasonable padding/raw windows (`child_01_seven`, `child_02_ten`,
  `child_04_four`); 6/21 of the 1.9.17 score inversions are explained by final-phone
  evidence changes; one human-absent token (`child_03_six`) also gains (0.001 → 0.919),
  so context recovery is not absent-specific.
- **Acoustic/encoder limitation survives:** 3/16 human-present finals have no evidence in
  any of the 11 windows (`child_01_ten`, `child_06_six`, `child_07_one`). In particular,
  the raw-window score recovery of `child_07_one` did **not** recover the final /n/
  evidence (max E 0.091) — the score change came from other phones.
- **Alignment dominates the present-token failures:** 9/16 present tokens have full-context
  evidence (E ≥0.3) that the production monotone span misses (span posterior <0.05;
  `child_01_nine`: E 0.979 vs span 0.0011).

## B2 justification

**B2 scientifically justified: NOT YET.**

Rationale:
1. The largest present-token failure class is ALIGNMENT (9/16); an encoder change now
   would be confounded by a known span-allocation defect.
2. The encoder gap is real but small (3/16, 3 children) and rests on single-reviewer
   labels; it cannot yet be distinguished from label error or from a boundary-controlled
   preprocessing issue without more confidently-labeled present finals.
3. Window selection does **not** provide a safe fix by itself: the dev-selected window
   (`pad100`) does not transfer (LWE recall 0.688/absent-FP 0.167 vs full 0.625/0.083),
   the best-separating LWE window (`raw`, AUC 0.880) still has recall 0.563, and no window
   preserves production recall (0.938). A future window policy must also pass the
   absent false-gain check.

## Recommended next steps (in order)

1. **Alignment repair (research-only):** use deletion-aware span placement as the
   evidence anchor (the 1.9.18 layer already detects the miss), and re-measure the 9
   ALIGNMENT cases under FRR-first. No production change.
2. **Boundary-controlled evaluation protocol:** for any future evidence comparison, fix
   the window family and require present/absent separation plus the false-gain check
   (this WP provides the harness).
3. **Labels:** collect 10–20 confidently-labeled human-present final consonants,
   especially /r/ (currently n=1, LOW) and the ENCODER-gap cases; if those tokens still
   show zero evidence under all reasonable windows with clean audio, the encoder gap
   becomes defensible.
4. **Only then** design a B2 experiment (encoder-level adaptation) with the alignment
   defect removed and the boundary protocol fixed.

No B2 training, no production change, no Unity integration in this work package.

```
Speech Research: NOT COMPLETE
Next gate:       MIXED_WINDOW_AND_ACOUSTIC
B2:              NOT YET (alignment repair + labels first)
```
