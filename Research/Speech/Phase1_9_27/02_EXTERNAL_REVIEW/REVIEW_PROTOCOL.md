# REVIEW PROTOCOL — WP-1.9.27 Part 3

> **Tóm tắt (VI):** Định nghĩa đầy đủ cho review mù: inclusion/exclusion, assessability,
> PRESENT/ABSENT/UNCERTAIN, HIGH/MEDIUM/LOW, xử lý /r/ yếu, coarticulation, deletion, noise,
> giọng trẻ, ambiguous realization; luật blinding; thiết kế 2–3 reviewer; giữ nguyên bất đồng.

## 1. Purpose

Produce trusted listening labels that can separate: (a) true weak productions, (b) encoder
missing-evidence, (c) encoder false-evidence, (d) label ambiguity, (e) acceptance-layer errors.

## 2. Inclusion / exclusion

- **Inclusion:** local recordings (LWE, SO762, SIAK) with a known target word and target final
  phone; candidates in pools A–O of Pack R.
- **Exclusion:** target word not identifiable; audio shorter than 250 ms; duplicate tokens.
- The 7 historical listening labels are re-inserted blind as hidden consistency controls (pool N).
- Score-derived labels (SO762/SIAK) are NEVER promoted to listening labels.

## 3. Assessability

| verdict | definition |
|---|---|
| ASSESSABLE | target word identifiable; final region not masked by noise/clipping |
| NOT_ASSESSABLE | recording unusable (severe noise, clipping, truncation); does not mean ABSENT |

## 4. Label definitions

- **PRESENT** — defensible acoustic/phonetic evidence consistent with the target final phone, not
  necessarily the canonical adult realization. A weak but plausible child production may be
  PRESENT. For /r/: a short or weakly rhotic constriction still counts if judged present.
- **ABSENT** — no defensible evidence of the target final phone: silence, deletion, or the acoustic
  event belongs to another phone (substitution).
- **UNCERTAIN** — the listener cannot make a defensible decision. Legitimate; never forced.

## 5. Confidence

- **HIGH** — clear perceptual decision, no hesitation.
- **MEDIUM** — decision likely correct but some ambiguity (weak/noisy/short).
- **LOW** — a guess between two options; keep a note explaining the alternatives.

## 6. Special cases

| case | instruction |
|---|---|
| weak /r/ | judge whether there is a rhotic constriction/lowering; F3 is not shown; if you hear "r-like" colouring, PRESENT (note strength) |
| coarticulation | judge the target phone, not the neighbour; note if fused with the preceding vowel |
| final deletion | if the final region is silent or the word ends on the vowel, ABSENT |
| recording noise | if noise prevents judgment: NOT_ASSESSABLE or UNCERTAIN; do not guess |
| child speech | accept child-typical realizations (stopping, gliding of /r/, final devoicing); the question is presence, not adult accuracy |
| ambiguous realization | UNCERTAIN with a note naming the competing interpretations |

## 7. Blinding rules

- Reviewer sees only: blind_id, word, target final phone, audio.
- Machine predictions, pools, historical labels, other reviewers: hidden.
- No machine output may be shown before the label is recorded.
- Each reviewer writes to a separate store; reviewers do not see each other's files.

## 8. Multi-reviewer design

- Reviewer A and B (optional C) label the same pack independently; per-reviewer randomized order.
- Disagreements are preserved; no majority vote. Adjudication (third listener) only after raw labels
  are frozen.
- Agreement: raw agreement; Cohen's kappa (2 raters); Fleiss' kappa (>=3); weighted kappa;
  confidence-stratified, /r/-specific, TYPE-B-specific, final-consonant, diagnostic-vs-control.

## 9. Outputs

- Server writes one JSONL per reviewer; export JSON/CSV per reviewer.
- `experiments/label_analysis.py` produces `HUMAN_LABEL_RESULTS.csv`, `REVIEWER_AGREEMENT.csv`,
  `ADJUDICATION_RESULTS.csv`, `label_sufficiency.json` (currently all empty/zero).
- Notes are preserved verbatim; nothing is auto-converted.
