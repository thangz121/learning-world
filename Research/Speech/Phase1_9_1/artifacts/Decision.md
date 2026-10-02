# Decision — Phase 1.9.1 Gate 8

## Final decision: **B. HYBRID PROMISING BUT INSUFFICIENT**

### Why not A (validated for integration)
1. **No filled human labels** yet on 28 IMG hybrid clips (review pack exists; labels empty).
2. Weak automated probe: **0/28** Moonshine nonempty — cannot claim hybrid recovers speech.
3. IMG F1 vs energy pseudo-ref is **not** human accuracy.
4. Router separates IMG vs NEW on a **2-file** grid only → overfit risk.
5. Explicit ban: do not lock production VAD/router this phase.

### Why not C (rejected)
1. Baseline still shows Silero **fails** IMG (0 segs) while NEW works — real failure mode exists.
2. hybrid_score still **unique** path producing selective IMG segments when Silero empty.
3. On NEW, hybrid_score F1≈0.97 vs clean Silero weak-ref; tracks Silero under white noise.
4. Research value remains for continuous-energy investigation.

### Keep in research path
- Default research baseline: Silero 0.5  
- Difficult continuous-energy candidate: hybrid_score  
- **Not** production Unity integration  

### Exact next blocker
**Human single-reviewer (or multi-rater) labels on `HumanReview/clips_hybrid` + filled `Human_Review_Template.csv`**, then recompute SPEECH vs NON_SPEECH rates for hybrid_score on IMG_0639.
