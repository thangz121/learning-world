# ASSESSABILITY INTERACTION (H) + RECORDING ROBUSTNESS (J)

**Script:** `experiments/assessability_stress.py`
**Artifacts:** `artifacts/ab/assessability_ab.csv|json`, `artifacts/stress/stress_tests.csv|json`

---

## 1. Experiment H — does the child head become a hidden assessability classifier?

Data: the 115 independent fidelity-review clips (1.9.15). 94 items have a canonical target
(numbers/sentences) and were runnable; 21 free-speech items have no target and are excluded.
Human labels: 87 valid attempts, 5 not-assessable among runnable items.

| group | baseline mean soft | adapted mean soft | confident (conf ≥ 0.05) |
|---|---:|---:|---:|
| human valid attempts (n=87) | 68.1 | 71.8 | 29.9% / 29.9% |
| human not-assessable (n=5) | 57.2 | **77.3** | 40.0% / 40.0% |

Reading:
- The head raises scores for valid speech (+3.6 mean) and does **not** change the confidence
  flag rate.
- On the 5 runnable not-assessable items it raises the mean score by +20 (57.2→77.3) —
  i.e. the adapted head produces *stronger* evidence on some unintelligible recordings.
  The confident-count stays 2/5, but the direction is the wrong one for assessability.
- n=5 is tiny; this is a **risk signal**, not a measured global regression. It reinforces the
  architecture rule: assessability must stay a separate layer and must never be inferred from
  phone-evidence strength.

## 2. Experiment J — recording-condition stress tests (synthetic only)

24 LWE tokens × 6 conditions; synthetic perturbations are stress tests, **not** real-child
validation.

| condition | baseline mean | adapted mean | baseline final recall | adapted final recall |
|---|---:|---:|---:|---:|
| clean | 67.1 | 76.2 | 75.0% | 66.7% |
| gain −12 dB | 67.1 | 76.2 | 75.0% | 66.7% |
| gain +6 dB (clipping) | 61.2 | 71.0 | 62.5% | 66.7% |
| noise SNR 20 dB | 61.3 | 68.6 | 62.5% | 58.3% |
| noise SNR 10 dB | 50.5 | 57.8 | 70.8% | 75.0% |
| telephone band 300–3400 Hz | 53.5 | 61.4 | 54.2% | 50.0% |

Findings:
- Both models are **gain-invariant** at −12 dB; +6 dB clips (clipping) degrade both.
- Noise and band-limiting degrade both models; the adapted head keeps a ~7–9 point higher
  mean but does not preserve final-consonant recall (clean: 66.7% vs 75%, telephone: 50% vs
  54%).
- No condition makes the adapted head safer than the baseline on finals.

## 3. Conclusion for the layers

| layer | status after 1.9.16 |
|---|---|
| assessability | must remain separate and rule/evidence-based; phone-evidence strength is not an assessability signal (H) |
| phone evidence | adapted head changes error distribution, not uniformly for the better; not adopted |
| pronunciation | unchanged (soft-v2 formula untouched) |
