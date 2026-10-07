# B2 EXPERIMENT PLAN (DESIGN ONLY — NO TRAINING) — WP-1.9.26 Part 18

> **Tóm tắt (VI):** Kế hoạch 5 thí nghiệm B2 (A–E) với giả thuyết, biến, dữ liệu, split, control,
> ablation, baseline, tiêu chí FRR-first/FAR/final-consonant//r//assessability/generalization.
> Không huấn luyện trong WP này.

Common protocol for all experiments: speaker-disjoint train/dev/test; FRR-first evaluation; the
production frozen encoder + its acceptance baseline as the comparator; the position-masked frame
cache + new listening labels as the evaluation target; no production change; no Unity.

## B2-A — same encoder + child-specific head

- Hypothesis: a child-adapted decision head on frozen features improves final-consonant decisions.
- IV: head architecture (linear / small MLP), training size. DV: FRR, FAR, final-consonant recall.
- Data: 60–200 labelled local tokens. Controls: random head; production soft_match baseline.
- Ablation: without blank/margin features; without rank. **Prior evidence: WP-1.9.16 B1 failed
  (final-consonant recall 93.8→75%) — expect a negative result unless labels fix the target.**
- Cannot solve: missing evidence (no new acoustic information).

## B2-B — phone-discrimination calibration

- Hypothesis: per-phone calibration (temperature/threshold) improves the s↔z, m↔n, v↔f separation.
- IV: calibration method (per-phone temperature / isotonic / threshold); DV: AUC per phone, FRR/FAR.
- Data: existing scores + labels. Controls: global temperature; production thresholds.
- Ablation: per-phone vs global; with/without rank conditioning.
- Cannot solve: missing evidence; /r/ (n too small).

## B2-C — encoder adaptation with child speech

- Hypothesis: fine-tuning the espeak encoder on child speech improves weak-final evidence.
- IV: data size (pilot 2–4 h vs robust 20–40 h), adaptation type (full / LoRA / last layers),
  domain mix. DV: missing-evidence rate, /r/, FRR/FAR, speaker generalization.
- Data: OCSC/JIBO (research) or MyST (commercial license) + local. Controls: frozen encoder;
  adult-speech fine-tune. Ablations: age band, corpus mix.
- Requires: GPU (none on ASUS), licensed data. Expected benefit: largest for missing evidence.
- Cannot solve: strong false peaks (they may persist), label ambiguity.

## B2-D — alternative encoder

- Hypothesis: a different encoder reduces representation-dependent failures.
- IV: encoder (xlsr-53-espeak vs lv-60-espeak vs future candidate); DV: missing/false evidence,
  /r/, agreement, isolated peaks.
- Data: 609-token local diagnostic set (already run in WP-1.9.26). Controls: same measurement code.
- Result so far: no aggregate winner (missing 16.5% vs 18.8%; false 17.2% vs 13.8%; 82.8% agreement).
- Cannot solve: label ambiguity; acceptance overlap.

## B2-E — hybrid acoustic + phonetic evidence

- Hypothesis: landmark/phonetic features (energy, voicing, frication, F3 for /r/) recover weak
  finals that CTC posteriors miss.
- IV: feature set (energy, voicing, formants, duration); DV: FRR/FAR, /r/ F3 detection.
- Data: local acoustic features (WP-1.9.12 + WP-1.9.26 /r/ audit) + new labels. Controls: CTC-only.
- Ablations: formants only; energy only; combined with CTC.
- Cannot solve: acoustically ambiguous/absent tokens; s↔z if acoustically identical.
