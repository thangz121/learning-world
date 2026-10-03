# ELSA — Playtest

STATUS: **BLOCKED**

SYSTEM: ELSA Speak / ELSA API
BLOCKED_AT:
  (a) API requires a partner token (`Authorization: ELSA <API_token>`; request access via ELSA).
  (b) App/browser tools require account + microphone; no upload of external audio files.
  (c) Speechanalyzer requires login.
WHAT_IS_NEEDED: Partner API token from ELSA (B2B), or a human with an account + mic to run the
  scripted cases live.
WHAT_THE_HUMAN_MUST_DO: Either obtain an API token through official channels, or run the app's
  pronunciation exercise with the scripted cases (correct / missing final consonant / silence /
  Vietnamese) and report the phoneme feedback UI.

Checked paths:
- elsaspeak.com/en/elsa-api → feature list + demo (mic-based)
- api-external-doc.elsanow.co → partner docs; token-gated
- speechanalyzer.elsaspeak.com → login required

No external-file upload path. Corpus cases NOT_RUN.
