# PHASE 1.9.18 — FINAL REPORT

**Zero-shot frozen baseline + B1 head-only child phone adaptation (research only)**
Date 2026-10-04 · Machine ASUS · Branch `phase1-9-16-recovery` · Base `d45d3ed`

---

1. **Exact dataset revision:** `mispeech/speechocean762` mirror of OpenSLR SLR101,
   HF revision `06385584fad212b26134c656fdd3ccf9f093f33e`.
2. **Child speaker split:** seed 1515, speaker-disjoint child-only —
   train 80 spk/1,600 utt/26,739 tok; valid 18 spk/360 utt/6,045 tok;
   test 24 spk/480 utt/7,853 tok; adults (128 spk) excluded. Overlap `[]`,
   leakage `[]`.
3. **Zero-shot baseline:** frozen `wav2vec2-xlsr-53-espeak-cv-ft@2c73378` +
   `PhoneEvidenceV2@1.4.0`, 480/480 scored, 0 errors, mean soft 75.78,
   mean conf 0.0763; **FRR 0.2272, FAR 0.2708**.
4. **B1 architecture:** frozen encoder → frozen CTC alignment → 8-dim
   encoder-only per-phone features → trainable MLP `8-32-32-1`, BCE-with-logits.
5. **B1 training:** seed 1515, AdamW lr 1e-3, wd 1e-4, bs 64, ≤60 epochs,
   best-valid-AUC checkpoint (epoch 37, AUC 0.7999), 26,510 train / 5,999 valid
   tokens, pos_weight 0.01664.
6. **B1 test results:** 480/480 scored; **FRR 0.2988, FAR 0.1042**.
7. **SO762 improvement/regression:** **REGRESSION** — FRR +0.0716.
8. **LWE transfer result:** `LWE_TRANSFER_BLOCKED_RAW_AUDIO_GITIGNORED`
   (raw child audio not committed); baseline reference human-correct low-score
   rate 0.3103 (n=29); B1 `NOT_COMPUTABLE`.
9. **SIAK result:** `SIAK_TRANSFER_BLOCKED_PENDING_ND_REVIEW`; B1
   `NOT_COMPUTABLE`.
10. **FRR delta:** **+0.0716 (worse)** — baseline 0.2272 → B1 0.2988.
11. **FAR delta:** −0.1667 — baseline 0.2708 → B1 0.1042.
12. **Deletion / final-consonant:** deletion-reject 0.40 → 0.90 (n=10, small);
    final-consonant reject rate rises on every class except F — no fix.
13. **Historical failure replay:** 23 forensic cases `NOT_COMPUTABLE` (audio
    gitignored); controls (adult-TTS) show B1 lowers confidence on all,
    including baseline-perfect `sapi_red`/`cat`.
14. **License status:** SO762 permissive (CC BY 4.0 / apache-2.0 card
    discrepancy recorded); checkpoint `CHECKPOINT_RETAINED_RESEARCH_ONLY`;
    SIAK ND open.
15. **Exact decision:** **E = B1_REGRESSES_HUMAN_CORRECT_CHILD_SPEECH**.
16. **Whether B2 is justified:** **NO** (`b2_justified = false`).
17. **Checkpoint path/hash:** `checkpoints/b1_head.pt`
    sha256 `44d86379…da3e6`.
18. **Git commit:** *(filled at commit time)* — see `decision.json`.
19. **Branch:** `phase1-9-16-recovery`.
20. **Production lock status:** `production_vad=false · router_locked=false ·
    unity_integrated=false · scorer_modified=false ·
    production_window_locked=false` — all unchanged; production files untouched.

---

## Honesty rule honored

Training completion (AUC 0.7999) is **not** success. SO762 improvement is **not**
LWE improvement. A child model is **not** a solved 4-year-old Vietnamese task.
B1 produced no evidence beyond the frozen baseline; it regressed human-correct
child-speech rejection.
