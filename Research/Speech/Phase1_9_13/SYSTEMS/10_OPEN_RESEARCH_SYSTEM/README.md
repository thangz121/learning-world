# SYSTEM 10 — slip (RemiKG) — in-browser GOP-CTC pronunciation coach

STATUS: **CLONED + SOURCE-AUDITED (E3)**; browser app (no headless run). **License: none found**
→ treat as all-rights-reserved (no reuse of code).

Directly relevant to our Phase 1.9.12 failures:
- **CTC forced alignment with `present:false`** when a phone had to be synthesized (audio too
  short) → explicit representation of missing/short phones (our deleted final consonants).
- **GOP (Goodness of Pronunciation)** over aligned posteriors: GOP-Avg = mean log P(canonical |
  frame); GOP-ratio = mean(logP_canon − best non-blank); heard phone = argmax non-blank over
  span; severity buckets; `lowConfidence` when span < 3 frames.
- Honest calibration note in code: raw GOP-CTC ≈ 0.44–0.46 phone-level correlation vs human.
- Stack: wav2vec2-lv-60-espeak-cv-ft via transformers.js/ONNX (int8 ~318 MB), espeak-ng WASM
  G2P, CMUdict package, optional Python server seam (`services/infer/app.py`).
