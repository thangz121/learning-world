# Microsoft PA — Playtest

STATUS: **BLOCKED**

SYSTEM: Microsoft Pronunciation Assessment
BLOCKED_AT: Azure Speech resource key required. Azure signup requires a payment method;
  policy forbids entering payment info. Speech Studio pronunciation tool requires Microsoft login.
WHAT_IS_NEEDED: A human-provided Azure Speech key (or a free-tier resource created by the human).
WHAT_THE_HUMAN_MUST_DO: Create an Azure Speech resource (free tier F0 is enough), provide the
  key + region via environment variable (never commit), then the agent can run the 14-case corpus
  and archive raw JSON.

Note: Speech Studio (speech.microsoft.com/portal) has a browser Pronunciation Assessment tool,
but it requires login + mic; no file upload for external audio without an account.

Corpus cases NOT_RUN. All findings above are E1 (documentation), not E6.
