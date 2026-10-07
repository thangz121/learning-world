# REVIEWER README — LWE Blind Final-Consonant Review (WP-1.9.26)

Thank you for helping. You will listen to short recordings of children saying single words or short
sentences and decide whether the **final consonant** of the target word is present.

## How to run the review

1. On the lab machine:
   `D:\speech-lab\venvs\p0\Scripts\python.exe Research\Speech\Phase1_9_26\experiments\serve_review_1926.py 8791`
2. Open `http://<machine-ip>:8791` in a browser (same machine: `http://127.0.0.1:8791`).
3. Enter your reviewer ID (e.g. `REV-A`). Your progress is saved automatically; you can stop and
   resume later. Your labels are stored in a file only you write to.
4. For each candidate you see only: an anonymous ID, the target word, the target final phone, and
   the audio. **No machine prediction is shown.**
5. Listen (replay as often as you like), then choose:

| label | meaning |
|---|---|
| **PRESENT** | you can defend that the target final phone is there (weak/child-like realizations count) |
| **ABSENT** | no defensible evidence of the target final phone |
| **UNCERTAIN** | you cannot decide; this is a valid answer — do not guess |

6. Pick confidence (HIGH / MEDIUM / LOW) and optionally write what you heard.
7. Click **Export my labels** when finished and send the CSV/JSON back.

## The one question

> "Is there defensible acoustic evidence consistent with the target final phone?"

Not: "does the child pronounce it like an adult?" Weak but plausible productions are PRESENT.
For /r/: a short or weakly rhotic constriction still counts if you judge it present.

## Uncertainty handling

- "I cannot hear it" is **not** automatically ABSENT; if you cannot make a defensible decision,
  choose UNCERTAIN.
- If noise prevents judgment, choose UNCERTAIN and note it.
- Do not try to agree with any machine or with previous reviewers (you cannot see them).

## Privacy

- Recordings contain child speech. Do not copy, share, or redistribute the audio.
- Use only the reviewer ID you were given; no personal data is stored.

## Files you may see

- `REVIEW_CANDIDATES.csv` (coordinator copy with machine fields — do not open before labeling if you
  want a fully blind session; the server never shows those fields).
