# REVIEWER A INSTRUCTIONS — WP-1.9.27

> **Tóm tắt (VI):** Hướng dẫn từng bước cho Reviewer A (độc lập, mù). Reviewer B có file riêng;
> hai người không xem kết quả của nhau trước khi cả hai nộp.

1. Coordinator starts the server on the lab machine:
   `python serve_review_1927.py 8791` (Pack R, 276 candidates).
2. Open `http://<lab-machine>:8791`; enter reviewer ID `REV-A`.
3. Review in your own pace. For each candidate:
   - listen with headphones in a quiet room;
   - label PRESENT / ABSENT / UNCERTAIN;
   - set confidence HIGH / MEDIUM / LOW;
   - set assessability; add a short note when useful; submit.
4. Do NOT look at Reviewer B's session or ask about specific cases before both of you have
   submitted. Disagreement is data, not an error.
5. You may stop and resume any time with the same reviewer ID (progress is saved).
6. When finished (or when stopping for the day), click "Export my labels (CSV)" and send the file
   to the coordinator. Filename suggestion: `revA_packR_<date>.csv`.
7. If the tool forces re-review of an already submitted case, do not use overwrite; report it to the
   coordinator instead.

Priority if you cannot finish: TYPE-B strong false-evidence (5) -> /r/ (24) -> weak present (25)
-> balanced/random control (rest).

Reminder: "cannot clearly hear it" alone is not ABSENT; use UNCERTAIN. Child production does not
have to match an adult canonical form. Do not redistribute the audio.
