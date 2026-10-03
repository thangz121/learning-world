# PHASE 1.9.17 — SPEECHOCEAN762 PHONE-TIER INSPECTION

**Date:** 2026-10-04 · **Machine:** ASUS · **Branch:** `phase1-9-16-recovery`
**Base:** `a88a198` (Phase 1.9.16, decision D) · **Type:** data audit only — NO training,
NO audio decoding, NO production/scorer/VAD/router change.
**Flags (unchanged):** `production_vad=false · router_locked=false ·
unity_integrated=false · scorer_modified=false · production_window_locked=false`

## Decision

### A = SO762_PROVIDES_A_GENUINE_CHILD_PHONE_TIER

The 1.9.16 material gate ("no child corpus with a genuine phone tier, speaker IDs,
and clear training rights") is **LIFTED for phone-tier supervision**, subject to the
transfer caveats in §6. This is a *data* finding, not a model result: no training was
run and no checkpoint exists.

Decision legend for this phase: **A** genuine phone tier found → gate lifted ·
**B** partial tier only · **C** insufficient · **D** not accessible ·
**E** format mismatch · **F** inconclusive. Chosen: **A**.

---

## 1. Mission

Phase 1.9.16 ended in **D = DATA_INSUFFICIENT_FOR_CHILD_ADAPTATION** and left a precise
shopping list (its §4): a child corpus with (1) a genuine phone tier, (2) speaker IDs,
(3) clear training rights. Phase 1.9.17 audits **SpeechOcean762** against that list —
annotation reality, population, audio format, and inventory compatibility — before any
adaptation is authorized.

## 2. Corpus identity and license

| property | value |
|---|---|
| corpus | `mispeech/speechocean762` (HF mirror of **OpenSLR SLR101**) |
| paper | Zhang et al., Interspeech 2021 |
| size | 5,000 utterances, 250 speakers, 31,816 words, 94,445 phone tokens |
| language | English, **Mandarin-L1 non-native** (all speakers) |
| license | OpenSLR page: **CC BY 4.0, free commercial + non-commercial**. HF card front-matter instead declares `apache-2.0`. **Discrepancy recorded, not resolved**; both are permissive and neither is ND. |
| contrast | unlike SIAK (CC-BY-ND-4.0, training BLOCKED_PENDING_LICENSE_REVIEW) |

## 3. Population (STREAMED from HF, RERUN twice — identical totals)

| group | speakers | utterances | phone tokens |
|---|---|---|---|
| children ≤ 15 | **122** | **2,440** | 40,637 |
| adults > 15 | 128 | 2,560 | 53,808 |
| **total** | 250 | 5,000 | 94,445 |

Age range is **6–15 for children** (histogram: 6y=500, 7y=440, 9y=380, 11y=260,
15y=260, …) plus adults 19–43. **There are no ages 4–5.**

## 4. Annotation reality — the load-bearing result

The phone tier is **MANUAL, 5 independent experts**, per-canonical-phone score 0/1/2
(stored as the experts' average, hence floats like 1.4). For phones scored **< 0.5** an
extra record gives the **observed substituted phone**; for ≥ 0.5 no observed form is
given. It is therefore a *scoring tier with sparse observed substitutions*, **not a
dense observed-phone transcription**.

| phone-accuracy bucket (all) | tokens |
|---|---|
| perfect 2.0 | 76,202 (80.7%) |
| high [1.5, 2.0) | 11,013 |
| mid [0.5, 1.5) | 3,827 |
| zero (incorrect/missed) | 2,287 |
| below 0.5 (0, 0.5) | 1,116 |
| **bad (< 0.5) = zero + below** | **3,403 (3.60%)** |

**Observed-substitution tier (quality check):** 3,403 records — **exactly one per bad
token (1:1)**; 472 distinct canonical→pronounced pairs. Split by kind:
**substitution 1,737 · deletion `<DEL>` 846 · unknown `<unk>` 820**. The explicit
deletion tier is directly relevant to the final-consonant/deletion bottleneck found in
1.9.14–1.9.15.

**Child subset (≤ 15):** 40,637 tokens; bad **514 (1.26%)**; substitutions 514.
Child bad-token rate (1.26%) is **lower** than the adult rate (~5.4%).

## 5. Inventory and audio-format compatibility

- **Inventory:** 39 distinct base ARPAbet phones (67 with stress variants). All **67/67
  map** into the frozen LWE inventory `phone-inventory-v1.4.0`; **0 unmapped**
  (`so762_phone_tier_audit.json`). Targets for the 7 LWE control words already resolve
  via CMUdict (1.9.16 `PHONE_INVENTORY_AUDIT.md`).
- **Audio format:** 40 files sampled across both splits are **uniform PCM,
  mono, 16,000 Hz, 16-bit, RIFF/WAVE** — identical to the expected pipeline input.
  Two original OpenSLR wav examples kept in `so762_format_probe.json`:
  child (age 6, speaker 0001, `000010011.wav`) and adult (age 21, speaker 0036,
  `000360013.wav`). Bytes read straight from the parquet with `Audio(decode=False)`;
  **no torchcodec / no sample decoding**.

## 6. What this changes (shopping-list mapping) and caveats

| 1.9.16 §4 requirement | verdict |
|---|---|
| genuine phone tier (gold or independently validated) | **MET** — 5-expert per-phone scores + observed substitutions incl. deletions |
| speaker IDs | **MET** — 250 speakers, speaker-disjoint splits constructible |
| clear training rights | **MET** — permissive (CC BY 4.0 / apache-2.0 card; neither ND) |

Mandatory caveats (do not skip):

1. **Transfer domain:** so762 children are **Mandarin-L1, ages 6–15**; LWE children are
   **Vietnamese-L1, ~age 4**. No age-4 match, no shared L1. Any gain must be measured on
   LWE-derived child evidence (FRR-first), never assumed.
2. **Sparse labels:** observed realizations exist only for scores < 0.5. so762 can
   supervise/validate a phone *goodness/adaptation* head and phone-error classes
   (sub/del/unk), but it is **not** a dense observed-phone reference for a CTC phone
   error rate. Training targets remain the canonical sequence (text → ARPAbet); so762
   supplies per-phone expert weights and the sparse error supervision.
3. **License discrepancy** (§2) is recorded, not resolved; no production recommendation
   is implied by this phase.

## 7. Artifacts

| file | content | provenance |
|---|---|---|
| `experiments/inspect_so762.py` | streaming annotation/coverage inspection; ≤25 examples | RERUN (inherited) |
| `phone_tier_examples_so762.json` | 25 representative examples + totals | STREAMED_FROM_HF |
| `experiments/verify_so762_format.py` | RIFF header probe, 40 files, no decode | RERUN |
| `so762_format_probe.json` | uniform-format proof + child/adult wav examples | VERIFIED |
| `experiments/audit_so762_phone_tier.py` | full phone-tier aggregation + inventory mapping | RERUN |
| `so762_phone_tier_audit.json` | accuracy buckets, substitution kinds, base-phone histogram | VERIFIED |

No audio corpus was committed (HF mirror `/ remote parquet only); no production, scorer,
VAD, router, assessability, or Unity code was touched.

## 8. Next steps (executable, in order)

1. Build **speaker-disjoint so762 splits** (child-focused test) in the frozen 1.9.16 A/B
   harness; seed inherited from 1.9.15.
2. Fix the **B1 target/eval policy** for so762: canonical targets + per-phone expert
   weights for training; the 514 child substitution records as error supervision
   (sub/del/unk); **never** treat the tier as a dense transcript (§6.2).
3. **Zero-shot frozen baseline** (`PhoneEvidenceV2@1.4.0`) on the so762 child test set —
   the number B1 must beat (phone-level correlation to expert scores; FRR-first on
   expert-correct phones).
4. Measure **cross-corpus transfer** (so762 supervision → SIAK/LWE child validation) to
   quantify caveat §6.1 before any LWE claim.
5. Only then re-open the 1.9.16 adaptation decision (B1 head-only first).

## 9. Honest limitations

- No 4–5-year-old speakers; the youngest is 6 (500 utt).
- Phone realizations are sparse (< 0.5 threshold); `<unk>` (820) is not a phone label.
- HF mirror license metadata conflicts with OpenSLR; not legally resolved here.
- Counts are from two independent streaming passes (inspect + audit) that agreed
  exactly (5,000 rows; 250/122 speakers; 2,440 child utt; 94,445 tokens); no third
  frozen copy is committed.
- Single-pass streaming cannot guarantee the upstream parquet revision is immutable;
  the mirror commit seen was `06385584fad212b26134c656fdd3ccf9f093f33e`.
