# REVIEWER README — WP-1.9.27 external review package

> **Tóm tắt (VI):** Hướng dẫn reviewer ngoài: nhiệm vụ là NGHE và quyết định có bằng chứng
> acoustic hợp lý cho phụ âm cuối mục tiêu hay không; PRESENT / ABSENT / UNCERTAIN + độ tin cậy;
> không thấy dự đoán máy, không thấy reviewer khác. Ưu tiên TYPE-B, /r/, weak-present.

## What you are given

- A local web review tool (blind). The coordinator runs it on the lab machine; you open it in a
  browser at `http://<lab-machine>:8791`.
- A set of audio clips of child speech. For each clip you see ONLY:
  - a blind case ID,
  - the target word,
  - the target final phone.
- You do NOT see any model output, score, prediction, pool, or another reviewer's decisions.

## Your task

For each clip, answer one question:

> "Is there defensible acoustic/phonetic evidence consistent with the target final phone?"

Then record:

- **PRESENT** — you hear/perceive defensible evidence of the target final phone (a weak but
  plausible child production still counts; it does NOT have to match a canonical adult pronunciation).
- **ABSENT** — no defensible evidence; the final region is silent, or the sound belongs to another
  phone (deletion/substitution).
- **UNCERTAIN** — you cannot make a defensible decision. This is a legitimate answer; never force
  ABSENT when unsure.
- **Confidence**: HIGH / MEDIUM / LOW.
- **Assessability**: ASSESSABLE / NOT_ASSESSABLE (recording unusable: severe noise, clipping,
  truncated word).
- Optional short note saying what you heard.

"I cannot clearly hear it" is NOT automatically ABSENT. If the recording is fine and you simply
cannot decide, choose UNCERTAIN.

## How to run a review session

1. Open the URL; type your reviewer ID (e.g. `REV-A` or `REV-B`). Use the ID the coordinator gave
   you; do not share IDs.
2. Cases are shuffled per reviewer and presented in a fixed personal order.
3. Play each clip (headphones recommended; quiet room).
4. Pick the label, then confidence, then assessability; add a note if useful; submit.
5. Progress auto-saves; you can close the browser and resume later with the same reviewer ID.
6. At the end (or any time), click "Export my labels (CSV)" and send the file to the coordinator.
   JSON export is also available.

## Priority order (if you cannot do all 276 in one session)

1. P0 — strong false-evidence cases (TYPE-B): 5 candidates.
2. P1 — /r/ candidates (24) and weak-present cases (25).
3. P2 — balanced and random-control candidates (the rest).

Do not discuss cases with the other reviewer before both are submitted. Disagreements are expected
and preserved; there is no "correct" tie-break at this stage.

## Rules

- Do not redistribute the audio or the pack. The recordings are project-local research data.
- Do not look for labels/scores elsewhere; your listening judgment is the measurement.
- Do not skip clips silently; either label them or mark NOT_ASSESSABLE.
- Take breaks: sessions over ~1 hour reduce label quality. Two shorter sittings are better.
