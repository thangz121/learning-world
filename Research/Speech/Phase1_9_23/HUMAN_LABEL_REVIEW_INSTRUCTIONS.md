# HUMAN LABEL REVIEW — INSTRUCTIONS (WP-1.9.23)

Status: **READY_FOR_HUMAN_REVIEW**. NEW_LABELS_COLLECTED = 0 (no reviewer available in-session;
no label was fabricated). The research continued with existing labels.

## Purpose

Provide the trusted positive examples needed to validate the gray zone that the acceptance-rule
redesign could not resolve: weak true-present final consonants (especially /r/) versus weak
false accepts that share the same blank-dominated, low-support feature region.

## Pack

`HUMAN_LABEL_REVIEW_PACK.csv` — 23 candidates, local recordings only, no audio copied into the repo.

| priority | what | n |
|---|---|---:|
| 1 | /r/ present candidates (all unlabelled LWE `four` tokens; word verdicts are negative — no better /r/ present candidate exists in LWE) | 3 |
| 2 | weak-support PRESENT (word verdict positive, max < 0.15) | 2 |
| 3 | rank-2..5 identity accepts (so762, phone score present) | 5 |
| 4 | strong false-accept candidates (TYPE B: absent, max ≥ 0.30) | 5 |
| 5 | additional /r/ absent evidence | 4 |
| 6 | strong /r/ present reference examples | 4 |

Columns: `priority, candidate_id, speaker_id, word, target_phone, source_corpus, audio_reference,
reference_note, machine_rank, machine_max_A, machine_blank_mean, machine_margin,
production_decision, best_rule_decision, why_needed, review_status`.

`reference_note` is a **word-level** human verdict (LWE) or the so762 per-phone score — explicitly
NOT a final-consonant label. It must not be copied into the label field.

## Procedure (existing tooling)

1. Audio references are absolute paths on the lab machine (LWE under
   `Research/Speech/ExternalData/zenodo_200495/...`; SO762 under `D:\speech-lab\data\speechocean762`).
2. Use the existing review server from WP-1.9.15:
   `D:\speech-lab\venvs\p0\Scripts\python.exe Research\Speech\Phase1_9_15\experiments\serve_review.py`
   (or build a small local HTML using the same clip paths; do not commit audio).
3. For each candidate the reviewer listens to the final phone in context and records:
   `human_label` ∈ {PRESENT, ABSENT, UNCERTAIN}, `confidence` ∈ {HIGH, MEDIUM, LOW}, optional note.
4. Rules:
   - Human listening is the authority for pronunciation; ASR/model output is never a label.
   - UNCERTAIN stays UNCERTAIN; do not force agreement.
   - A label may not become PRESENT merely because the model likes it.
   - Do not modify historical labels; new labels are versioned research artifacts.
5. Output: `NEW_HUMAN_LABELS.csv` in `Research/Speech/Phase1_9_23/` with columns
   `label_id, speaker_id, word, target_phone, human_label, confidence, reviewer_count,
   review_notes, source_corpus, research_only` (currently created empty).

## Minimum target

At least 10–20 new confidently-labeled PRESENT final consonants, /r/ first. If fewer are obtainable,
record the exact number and continue with the available set.
