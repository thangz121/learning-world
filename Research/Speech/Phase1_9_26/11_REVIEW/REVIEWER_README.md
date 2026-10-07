# REVIEWER README (EXTERNAL PACKAGE) — WP-1.9.26

This is the same guide as `01_HUMAN_LABEL_ACQUISITION/REVIEWER_README.md`, packaged for external
reviewers.

## Purpose

Decide, by listening only, whether the **target final consonant** of a child's word is present.
Your labels settle whether the model's remaining errors come from the encoder or from ambiguous
labels. No machine prediction is shown to you.

## Task

1. Open the review page (your coordinator will give the address, e.g. `http://192.168.x.x:8791`).
2. Enter your reviewer ID.
3. For each item: word + target final phone + audio (replay freely).
4. Answer the one question: *"Is there defensible acoustic evidence consistent with the target
   final phone?"*
   - **PRESENT** — yes, even if weak or child-like.
   - **ABSENT** — no defensible evidence (silent ending, deletion, or a different phone).
   - **UNCERTAIN** — you cannot decide; this is a valid answer.
5. Choose confidence HIGH / MEDIUM / LOW and add a short note.
6. Export your labels when done; send the file back.

## Important

- Weak/child-like realizations count as PRESENT. Do not require adult-like pronunciation.
- For /r/: a short or weakly rhotic constriction counts if you judge it present.
- "I cannot hear it" is not automatically ABSENT — use UNCERTAIN.
- Do not try to match any machine or other reviewer (you cannot see them).
- Do not copy or share the audio (child speech, privacy).
