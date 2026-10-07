# LABELING PROTOCOL — WP-1.9.26 Part 4

> **Tóm tắt (VI):** Protocol chính thức cho review mù 276 ứng viên: định nghĩa PRESENT/ABSENT/
> UNCERTAIN, HIGH/MEDIUM/LOW, xử lý /r/ yếu, coarticulation, deletion, noise, giọng trẻ; nguyên tắc
> "không bắt buộc nghe đúng phoneme người lớn chuẩn tắc"; không ép ABSENT khi không chắc.

## 1. Purpose

Produce trusted listening labels that can separate (a) true weak productions, (b) encoder
missing-evidence, (c) encoder false-evidence, (d) label ambiguity, and (e) acceptance-layer errors.

## 2. Inclusion / exclusion

**Inclusion:** local recordings (LWE, SO762, SIAK) with a known target word and target final phone;
candidate pools A–O (see `REVIEW_PACK_MANIFEST.csv`).
**Exclusion:** recordings where the target word cannot be identified; audio shorter than 250 ms;
duplicate tokens; tokens whose historical label is already a listening label (only 7 are re-inserted
as hidden consistency controls).

## 3. Assessability

| verdict | definition |
|---|---|
| ASSESSABLE | the target word is identifiable and the final region is not masked by noise/clipping |
| NOT_ASSESSABLE | recording unusable (severe noise, clipping, truncation); label NOT_ASSESSABLE |

## 4. Label definitions

- **PRESENT** — defensible acoustic/phonetic evidence consistent with the target final phone
  (not necessarily the canonical adult realization). A weak but plausible child production may be
  PRESENT. For /r/, a short or weakly rhotic constriction still counts if the listener judges it
  present; do not require a full adult-like rhotic.
- **ABSENT** — no defensible evidence of the target final phone; the final region is silent, or the
  acoustic event belongs to another phone (e.g., deletion, substitution).
- **UNCERTAIN** — the listener cannot make a defensible decision. UNCERTAIN is a legitimate outcome;
  never force ABSENT.

## 5. Confidence

- **HIGH** — clear perceptual decision, no hesitation.
- **MEDIUM** — decision likely correct but some ambiguity (weak/noisy/short).
- **LOW** — a guess between two options; keep the note explaining the alternatives.

## 6. Special cases

| case | instruction |
|---|---|
| weak /r/ | judge whether there is a rhotic constriction/lowering; F3 is not available to the reviewer; if you hear "r-like" colouring, PRESENT (note strength) |
| coarticulation | judge the target phone, not the neighbor; note if the final is fused with the preceding vowel |
| final deletion | if the final region is silent or the word ends on the vowel, ABSENT |
| recording noise | if noise prevents judgment, NOT_ASSESSABLE or UNCERTAIN; do not guess |
| child speech | accept child-typical realizations (stopping of fricatives, gliding of /r/, final devoicing); the question is presence, not adult accuracy |
| ambiguous realization | UNCERTAIN with a note naming the competing interpretations |

## 7. Blinding rules

- The reviewer sees only: blind_id, word, target final phone, audio.
- Machine predictions, pool, historical labels and other reviewers' decisions are hidden.
- No machine output may be shown before the label is recorded.
- Reviewers do not see each other's files; each reviewer writes to a separate JSONL.

## 8. Multi-reviewer design

- Reviewer A, B (and optional C) label the same pack independently (per-reviewer randomized order).
- Disagreements are preserved; no majority vote. A separate adjudication pass (a third listener)
  may be scheduled only after raw labels are frozen.
- Agreement analysis: raw agreement; Cohen's kappa for two raters; Fleiss' kappa for ≥3;
  confidence-stratified and /r/-specific agreement (script: `experiments/reviewer_agreement.py`).

## 9. Outputs

- `HUMAN_LABEL_RESULTS.csv` (schema in this directory; currently empty, 0 labels).
- `REVIEWER_AGREEMENT.csv` (populated after ≥2 reviewers).
- Notes are preserved verbatim; nothing is auto-converted.
