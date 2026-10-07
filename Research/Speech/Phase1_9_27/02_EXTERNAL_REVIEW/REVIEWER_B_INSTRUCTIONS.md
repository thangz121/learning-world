# REVIEWER B INSTRUCTIONS — WP-1.9.27

> **Tóm tắt (VI):** Hướng dẫn từng bước cho Reviewer B (độc lập, mù). Không xem kết quả của
> Reviewer A; thứ tự case được xáo riêng cho B.

1. Coordinator starts the server on the lab machine:
   `python serve_review_1927.py 8791` (Pack R, 276 candidates).
2. Open `http://<lab-machine>:8791`; enter reviewer ID `REV-B`.
3. Your case order is shuffled independently from Reviewer A's. Label every case you attempt:
   - PRESENT / ABSENT / UNCERTAIN;
   - confidence HIGH / MEDIUM / LOW;
   - assessability; optional note; submit.
4. Independence is the point: do not open Reviewer A's export, do not discuss individual cases
   until both exports are delivered. If you happen to know A's opinion on a case, ignore it and
   still give your own best judgment.
5. Stop/resume freely; progress is tied to the ID `REV-B`.
6. When done, click "Export my labels (CSV)" and send the file to the coordinator.
   Filename suggestion: `revB_packR_<date>.csv`.
7. Report any tool problem (audio not loading, duplicate prompt) to the coordinator; do not edit the
   export by hand.

Priority if time-limited: TYPE-B (5) -> /r/ (24) -> weak present (25) -> the rest.

Reminder: UNCERTAIN is a legitimate label; do not force ABSENT. Judge presence, not adult-perfect
pronunciation. Do not redistribute the audio.
