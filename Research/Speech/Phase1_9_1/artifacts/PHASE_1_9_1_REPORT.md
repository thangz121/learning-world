# Phase 1.9.1 Report — VAD human-validation pack + robustness

## 1. WHAT WAS REPRODUCED
Silero/energy/hybrid/adaptive counts on IMG+NEW: **ALL_MATCH** vs Phase 1.9 report (see `Baseline_Reproduction.md`).

## 2. WHAT HUMAN REVIEW SHOWED
**Pending human fill.**  
Artifacts ready: 28 hybrid WAV clips, CSV template, `review.html`, guide.  
Labels empty by design (no auto human labels).

## 3. WHAT HYBRID ACTUALLY RECOVERS
- Count-level: 28 segments on IMG where Silero=0 (reproducible).  
- Content-level: **unknown** until human listen.  
- Weak ASR probe: 0/28 EN ASR nonempty → weak evidence only.

## 4. WHAT IT STILL MISSES
- Human SPEECH rate  
- Boundary quality  
- False positives among 28  
- Whether continuous-energy speech is present at all on IMG  

## 5. CONTROLLED ROBUSTNESS
hybrid_score ≈ silero under white noise on NEW; adaptive_then_silero worse at low SNR; IMG still unique failure.

## 6. ROUTER STABILITY
Grid 25 thr pairs: IMG→hybrid ~80%, NEW→hybrid 0%. Clean separation but **2-file overfit risk**.

## 7. DOWNSTREAM PRONUNCIATION IMPACT
Tight crops move blue/big scores; prefer full/padded. No scorer policy change.

## 8. LIMITATIONS
No human GT; EN-only ASR probe; 2 real files; pseudo-ref on IMG.

## 9. FINAL DECISION
**B. HYBRID PROMISING BUT INSUFFICIENT**

## 10. EXACT NEXT BLOCKER
Fill human labels on hybrid clips → quantify true SPEECH recovery rate.
