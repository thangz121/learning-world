# FRR/FAR ANALYSIS — Phase 1.9.16 (baseline only, FRR-first)

**Artifacts:** `frr_far.csv` (baseline columns filled, child columns
NOT_AVAILABLE), `speaker_metrics.csv` (27 test speakers), `age_metrics.csv`.
LWE numbers from committed human labels (RECOVERED_FROM_GIT); SIAK proxy from
fresh 1.9.16 reproduction (RERUN).

---

## 1. Baseline safety numbers (frozen model)

| population | n | baseline | child model |
|---|---|---|---|
| LWE human-correct → FRR | 29 | 31.0% (9/29, soft<50) | NOT_AVAILABLE |
| LWE human-incorrect → FAR | 13 | 38.5% (5/13, soft≥50) | NOT_AVAILABLE |
| SIAK test rating≥80 → FRR-proxy | 196 | 12.24% (RERUN, matches 1.9.15) | NOT_AVAILABLE |

No composite score is constructed. The FRR-first rule stands: any future child
model must lower (or hold) FRR on human-correct speech before its PER or
correlation gains count for anything.

## 2. Speaker generalization (baseline, unseen speakers)

`speaker_metrics.csv` reports all 27 test speakers separately (n, mean soft,
mean confidence, mean rating). No speaker collapses to zero evidence; confidence
is uniformly low (the evidence weakness, not a speaker failure). Child-model
columns are present and NOT_AVAILABLE — the per-speaker table is ready for the
A/B the moment a child model legitimately exists. No catastrophic speaker
failure is averaged away: the worst speakers are visible in the CSV.

## 3. Age analysis (baseline)

`age_metrics.csv`: test 7–12 rescored on MAYNODE (means by split available in
`speaker_metrics.csv` + 1.9.15 per-age table, RECOVERED_FROM_GIT). Ages 4–6
(5 speakers, 99 utt) were NOT rescored in 1.9.16 — raw audio is present in the
verified SIAK copy, but re-scoring adds no decision-relevant information while
the adaptation itself is blocked; the 1.9.15 ages-46 analysis stands.
Age 4 has 20 utterances / effectively 1–2 speakers: **insufficient for any
age-4 claim**, stated explicitly per spec.

## 4. Recording robustness

Stress evidence (silence/noise controls, loud/soft tokens) is committed from
1.9.2/1.9.5/1.9.8 work (RECOVERED_FROM_GIT). No new synthetic perturbations
were generated: they are stress tests, not substitutes for the missing phone
labels, and cannot unblock the gate.
