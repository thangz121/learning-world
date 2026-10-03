# Speechace — Playtest

STATUS: **BLOCKED**

SYSTEM: Speechace API
BLOCKED_AT: API key issued via request form / free-trial signup (email + approval; possibly
  business contact). No anonymous public key. CORS disabled → must call from backend.
WHAT_IS_NEEDED: Human obtains a free-trial API key from speechace.com/api-plans (Start Free Trial
  or contact form) and provides it via environment variable (never committed).
WHAT_THE_HUMAN_MUST_DO: Request the trial key; once available, the agent can run all 14 corpus
  cases through score/text (and score/word) and archive raw JSON including fidelity_class.

Public Postman workspace exists (speechace's public workspace) with example requests, but still
requires a key to execute. Corpus cases NOT_RUN (E1 evidence only).
