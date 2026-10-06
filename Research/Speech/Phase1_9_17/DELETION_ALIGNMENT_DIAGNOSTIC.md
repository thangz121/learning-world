# DELETION / ALIGNMENT DIAGNOSTIC — Phase 1.9.17

**Machine:** ASUS · **Repository:** `thangz121/learning-world` · **Branch tip used:** `e8d96da`
(= main tip; branch not switched per git rule) · **Scope:** research-only diagnosis; no
production change, no training, no commit.

**Flags:** `production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false`

**Final status:** **SPEECH_RESEARCH_REQUIRES_ALIGNMENT_WORK**

---

## 0. Mission and corpus

Determine where word-final consonant failures actually occur, decomposing
ACOUSTIC / ALIGNMENT / DELETION / SCORING / THRESHOLD, using the existing failure corpus
(no new model):

- 12 PHONE_MODEL_ERROR cases (phase 1.9.10) and 6 SCORER_MISS cases (phase 1.9.11);
- 28 blind human final-consonant labels (phase 1.9.12): 16 present / 12 absent;
- all 80 studio number tokens (phase 1.9.8) reconstructed at frame level;
- window A/B scores from phase 1.9.11 (full / raw VAD / pad250) for mechanism checks.

Artifacts: `FAILURE_CASE_ANALYSIS.csv` (80 rows), `EXPERIMENT_RESULTS.json`,
`artifacts/case_evidence.csv`.

---

## 1. PHASE A — reconstructed current pipeline (frozen)

Source: `Research/Speech/Phase1_4/SoftMatching/phone_evidence_v2.py`
(SHA-256 `87254B7B…`, `PhoneEvidenceV2@1.4.0`, alignment `ctc_forced_v1`) and
`Phase1_4/PhoneInventory/inventory.py` (`phone-inventory-v1.4.0`).

| # | question | answer |
|---|---|---|
| 1 | logits | `logits()` L83–95: soundfile → require 16 kHz → `Wav2Vec2FeatureExtractor` → frozen `wav2vec2-xlsr-53-espeak-cv-ft` CTC → softmax `probs [T,392]` (≈50 frames/s) |
| 2 | phone sequence | greedy `greedy_phones()` L97–121 (argmax, blank/`\|` reset, canonicalized) — used for evidence not scoring |
| 3 | alignment | `ctc_align()` L123–200: monotone DP `dp[t][j]`, only STAY/ADVANCE transitions (no blank state, no skips); every one of the n target phones is forced to own ≥1 frame; spans merged/split to exactly n; emission = summed posterior of the canonical class ids |
| 4 | deletion | **not representable**: the DP cannot skip an expected phone; a deleted phone still receives a span |
| 5 | insertion | not represented in the alignment and not penalized; greedy extras are ignored by scoring |
| 6 | substitution | soft matching in `soft_match()` L202–258: best observation from top-k by `phone_similarity`, aggregate class posterior over the span |
| 7 | score | `soft_score = 100 × mean(sim)` L270; per-phone `match_type`: `exact` if best_obs==expected or post≥0.35; `soft` if post≥0.25 or best_sim≥0.35; else `miss`; `sim` per L242–250; confidence L272–279 |
| 8 | final-consonant score | depends on the **last ctc_align span**, class posterior over it, top-1 identity, the match rule, and the mean over all target phones (early vowel errors dilute the word score) |
| 9 | thresholds | post 0.25 / 0.35, best_sim 0.35, low-posterior flag 0.15, research pass/fail band 50 |
| 10 | where a final can be lost | (a) encoder has no evidence; (b) alignment places the span in a low-evidence region (measured: `child_01_nine` baseline span posterior max 0.0155 vs deletion-aware span 0.9793); (c) match rule accepts on top-1 identity with negligible posterior (`child_06_six`: span max 0.0007 → `exact`); (d) mean-over-phones dilution; (e) 50-threshold; (f) window/boundary instability (21/80 tokens flip between full and raw windows) |

## 2. PHASE B — failure corpus replay (frame-level)

Per-case rows: `FAILURE_CASE_ANALYSIS.csv`. Primary human-labeled decision failures (28
blind labels; 6 disagreements):

| case | human final | baseline | evidence | span_max | da_max | margin/frame | window full→raw | class |
|---|---|---|---|---|---|---|---|---|
| child_01_four | CLEARLY_ABSENT /r/ | soft (score 72.5) | NONE | 0.036 | 0.022 | −0.039 | 72.5→66.7 | **DELETION** |
| child_03_four | CLEARLY_ABSENT /r/ | exact (100.0) | NONE | 0.056 | 0.056 | −0.062 | 100→100 | **DELETION** |
| child_06_four | CLEARLY_ABSENT /r/ | exact (100.0) | NONE | 0.041 | 0.041 | −0.101 | 100→66.7 | **DELETION** |
| child_02_four | CLEARLY_ABSENT /r/ | exact (73.5) | WEAK | 0.154 | 0.154 | −0.025 | 73.5→40.1 | **MIXED** |
| child_07_seven | PROBABLY_ABSENT /n/ | exact (20.0) | STRONG | 0.629 | 0.629 | +0.039 | 20→43.5 | **MIXED** (acoustic/human conflict) |
| child_07_one | CLEARLY_PRESENT /n/ | miss (0.0) | NONE | 0.013 | 0.001 | −0.066 | **0→72.5** | **ALIGNMENT** (window/boundary) |

Correctly decided: 22/28 (baseline FRR 1/16 = 6.3%, FAR 5/12 = 41.7%).

Replay of the 12 PME + 6 SCORER_MISS (word-verdict inferred, marked `inferred` in the CSV):
consonant-final cases classify as DELETION 2 (`child_07_four`, `child_08_four`), ALIGNMENT 1
(`child_04_seven`), ACOUSTIC 1 (`child_08_five`, human CLEAR_CORRECT but no evidence for final
/v/ in either alignment); vowel-final PME/SM cases (`two`, `three`) are marked
`NON_FINAL_CONSONANT_CASE` (they are PME failures outside this diagnostic's target).

## 3. PHASE C — deletion-aware alignment counterfactual

Research-only blank-interleaved (2L+1) Viterbi with explicit skip transitions and a
present/absent log-likelihood-ratio margin, run on the **same frozen logits and targets**
(the 1.9.14 prototype; no architecture change was needed — see
`experiments/del_alignment_diagnostic.py`, imports `p2_deletion_aware`).

| rule on 28 labeled consonant finals (16 present / 12 absent) | present recall | absent detection |
|---|---:|---:|
| baseline (`match_type ∈ {exact, soft}`) | 0.9375 (15/16) | 0.5833 (7/12) |
| deletion-aware free presence (zero skip penalty) | 0.5625 (9/16) | 0.8333 (10/12) |
| margin/frame ≥ −0.02 | 0.75 | 0.8333 |
| margin/frame ≥ −0.04 | 0.75 | 0.6667 |
| margin/frame ≥ −0.06 (baseline-recall region) | 0.875 | 0.5833 |

No threshold dominates the baseline: the deletion-aware representation **can** reject absent
finals (including all five absent /r/: free-presence rate 0.0) but it also rejects
weak-but-present finals. The five absent /r/ have no /r/ evidence (span max 0.02–0.15); the
single human-present /r/ (`child_04_four`, PROBABLY_PRESENT, LOW reviewer confidence) also has
none (da max 0.003) — /r/ present-vs-absent is not separable with this evidence (n=1 present).

## 4. PHASE D — counterfactual scoring (same logits, same target, different rule)

Evidence-floor counterfactual on the baseline span (`span_max_post`):

| floor τ | present recall | absent detection |
|---:|---:|---:|
| 0.00 (baseline agnostic) | 0.9375 | 0.5833 |
| ≥0.01 | 0.9375 | 0.25 |
| ≥0.05 | 0.75 | 0.75 |
| ≥0.08 | 0.625 | 0.8333 |
| ≥0.20 | 0.5625 | 0.9167 |

At the baseline's recall level a posterior floor makes absent detection **worse** (0.25), and
improving absent detection requires dropping present recall to 0.56–0.75. The deletion-aware
margin shows the same frontier (phase 1.9.14). **No scoring-only rule dominates**: the failure
is not a mis-set threshold.

Root of the FAR pathway, exposed by the cases: the acceptance logic can return `exact` on
top-1 identity alone. `child_06_six` (human PROBABLY_PRESENT) has span posterior max
**0.0007** and still gets `exact`; 14/80 tokens are accepted with `evidence_class = NONE`
(5 of them human-labeled present → `OK_UNSUPPORTED`, i.e. agreement without support).

## 5. PHASE E — final-consonant aggregation (28 labeled)

| group | n | present | absent | baseline present recall | baseline absent detection | DA free-present rate |
|---|---:|---:|---:|---:|---:|---:|
| /t/ (eight) | 8 | 5 | 3 | 1.00 | 1.00 | 1.00 |
| /n/ (one/seven/nine/ten) | 9 | 8 | 1 | 0.875 | 0.00 | 0.375 |
| /r/ (four) | 6 | 1 | 5 | 1.00 | **0.20** | **0.00** |
| /v/ (five) | 3 | 1 | 2 | 1.00 | 1.00 | 1.00 |
| /s/ (six) | 2 | 1 | 1 | 1.00 | 1.00 | 0.00 |

Words: eight 8 (present 5/absent 3), four 6 (1/5), seven 4 (3/1), five 3 (1/2), six 2 (1/1),
one 2 (2/0), ten 2 (2/0), nine 1 (1/0). Subgroups with n<5 are reported but carry no claim.

Deletion rate (absent accepted) = 5/12 overall, 4/5 within /r/.
False rejection (present rejected) = 1/16 (the /n/ window case).
No valid "insertion" rate can be computed from this evaluation design (the forced alignment
never inserts phones; the greedy stream is not scored) — recorded as a measurement gap.

## 6. PHASE F — root-cause classification

Rules (documented in `experiments/analyze_cases.py`): assessability refusal states kept
separate; present-rejected → ALIGNMENT if a raw window recovers it, else SCORING/THRESHOLD
when evidence is strong, MIXED when weak, ACOUSTIC when none; absent-accepted → DELETION when
evidence is NONE, MIXED when weak/strong.

**Primary distribution (28 blind-labeled consonant finals, decision failures):**
```
DELETION : 3/6  (50.0%)   child_01_four, child_03_four, child_06_four
MIXED    : 2/6  (33.3%)   child_02_four (weak evidence /r/), child_07_seven (acoustic/human conflict)
ALIGNMENT: 1/6  (16.7%)   child_07_one  (full 0.0 vs raw 72.5)
ACOUSTIC : 0/6
SCORING  : 0/6
THRESHOLD: 0/6
```
**Inferred from word verdicts (PME/SM consonant finals, secondary):** DELETION 2/4,
ALIGNMENT 1/4, ACOUSTIC 1/4.

**Process-level mechanism prevalence (all 80 tokens):**
- window inversion (full vs raw flips the pass/fail) — **21/80 (26.3%)**;
- accepted with no acoustic support (`evidence_class NONE`) — **14/80 (17.5%)**;
- deletion-aware presence disagrees with baseline presence — **25/80 (31.3%)**;
- greedy has the final phone but baseline rejects it — 1/80 (`child_04_three`, vowel final);
- baseline span in a near-empty region while deletion-aware span finds strong evidence —
  1/80 strict (`child_01_nine`: span max 0.0155 vs 0.9793), plus the window-inversion set.

## 7. PHASE G — decision gate

### GATE A — ALIGNMENT / DELETION IS PRIMARY BOTTLENECK

Evidence: 4/6 labeled decision failures are alignment/deletion mechanisms; the deletion
representation cannot express absence and the acceptance rule converts absence into a match;
window/alignment instability affects 26.3% of tokens; the deletion-aware margin is the only
signal that detects the absent-/r/ class, but it cannot do so without rejecting present
finals.

Secondary findings that qualify (not replace) the gate:
- **Acoustic insensitivity is real and material:** several human-present finals (/n/, /s/, /r/
  tokens) have no evidence in either alignment; requiring evidence (any floor) harms present
  recall. Fixing alignment/deletion alone will not create evidence that the encoder lacks.
- **MIXED 2/6:** `child_07_seven` (strong acoustic evidence, human hears absent) is an
  annotation/acoustic conflict; `child_02_four` has weak /r/ evidence.
- B1 (frozen encoder + head) could not fix any of this: it changes acoustic posteriors but
  leaves the forced-alignment/deletion architecture intact — consistent with its unchanged
  SCORER_MISS "four" results and final-consonant regression.

Not selected: GATE B (acoustic primary) — no labeled decision failure was purely acoustic;
GATE C (scoring primary) — no scoring-only rule dominates; GATE D — the distribution is
not inconclusive: alignment/deletion is the dominant mechanism.

## 8. PHASE H — assessability check

- 0 failure cases in this corpus were flipped to ASSESSABILITY_FAILURE: no fidelity-v2 refusal
  state occurs among the 80 tokens, and the 1.9.15 independent not-assessable recordings are
  disjoint from this corpus.
- 21 tokens have human word-level UNCERTAIN/CONFLICTED verdicts and no final labels; they are
  kept as HUMAN_UNCERTAIN (not forced into pronunciation errors) and excluded from the
  primary distribution.
- No phone score was used as an assessability proxy.

## 9. PHASE I — reproducibility

- Model: `facebook/wav2vec2-xlsr-53-espeak-cv-ft`, snapshot `2c733782da5604684829819a5eb744c193fe9398`
  (present locally); frozen pipeline hashes recorded (`PhoneEvidenceV2` `87254B7B…`,
  inventory `97D07C12…`).
- Audio: gitignored `ExternalData/zenodo_200495` (+ derived 16k cache); no raw child audio in
  Git; `.gitignore` untouched; no tracked file modified (only new `Phase1_9_17/`).
- Deterministic: no randomness in this diagnostic; deletion-aware module is the frozen
  1.9.14 prototype.
- Human labels: 1.9.12 blind final review (single reviewer, documented); 1.9.9/1.9.10/1.9.11
  verdicts used only as context/inference and marked as such.

## 10. Answers to the required questions

1. **Where do final-consonant failures occur?** Dominantly at the
   alignment/deletion/acceptance stage (`DELETION 50%`, `ALIGNMENT 17%`, `MIXED 33%` of the
   6 labeled decision failures), not in the acoustic encoder (0/6 pure acoustic), with
   pervasive window/boundary instability (21/80).
2. **Does forced alignment lose evidence?** Yes: 21/80 window inversions; `child_01_nine`
   baseline span 0.0155 vs deletion-aware 0.9793; `child_07_one` full 0.0 vs raw 72.5.
3. **Is deleted-final a true bottleneck?** Yes for absent /r/: 4/5 accepted, all with no
   acoustic support; the baseline has no way to express absence.
4. **How many cases have encoder evidence the scorer loses?** At minimum 1 labeled
   (`child_07_one`, window) + 1 span-collapse (`child_01_nine`) + the inferred
   `child_04_seven`; strictly more via the 25/80 baseline-vs-deletion-aware disagreements.
5. **How many have no acoustic evidence at all?** Among labeled failures 0/6 pure acoustic
   (absent phones show no support, which is correct for deletion); among human-present tokens,
   many have `evidence_class NONE` (5 labeled + more unlabeled) — the encoder is insensitive,
   which is a real secondary limit.
6. **Why did B1 fail — model or architecture?** Architecture/representation first: B1 changed
   posteriors but could not fix deletion (`four` unchanged) and regressed finals; the
   alignment/acceptance path was untouched.
7. **Is encoder-level training worth it?** Not yet. Fix alignment/deletion first, then
   re-measure whether evidence gaps remain; only then quantify what encoder adaptation could
   add.
8. **Is a deletion-aware scorer worth doing?** Yes, as the next research step, with FRR-first
   constraints (free-skip alignment alone is unsafe: FRR 7/16 in 1.9.14).
9. **Do we need Vietnamese age-4 data before continuing?** Not for the alignment/deletion
   work; yes for eventual child/accent calibration (the /r/ present case and age-4 population
   remain unresolved).
10. **Most reasonable next experiment?** A research-only deletion-aware evidence/decision
    layer evaluated on the 28 labeled finals + 80-token corpus, with explicit UNCERTAIN state
    and a posterior/margin floor calibrated speaker-disjoint — before any B2 model training.

## 11. Files created / experiments run

```
Research/Speech/Phase1_9_17/
  DELETION_ALIGNMENT_DIAGNOSTIC.md     this report
  FAILURE_CASE_ANALYSIS.csv            80 rows, all flags + classification
  EXPERIMENT_RESULTS.json              counterfactuals, aggregation, mechanisms, root cause
  NEXT_RESEARCH_RECOMMENDATION.md      GATE A next work package
  experiments/
    del_alignment_diagnostic.py        replay + frame evidence + counterfactual sweeps
    analyze_cases.py                   classification v2 + mechanism counts + evidence floor
    _inspect.py / _report_table.py     table extraction utilities
  artifacts/case_evidence.csv          per-token frame/alignment/deletion evidence
```
No tracked file was modified; nothing was committed or pushed.
