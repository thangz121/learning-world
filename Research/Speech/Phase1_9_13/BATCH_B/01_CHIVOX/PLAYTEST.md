# CHIVOX — Playtest

STATUS: **BLOCKED**

SYSTEM: Chivox English Speech Assessment
BLOCKED_AT: All execution paths require credentials:
  - HTTP API: `appKey` + `sig = ALG(appKey + timestamp + secretKey)` (sha256) — secretKey is
    customer-issued.
  - MCP: `https://mcp.cloud.chivox.com` with `apiKey` (`sk-...`).
  - Pilot credits/pricing: sales contact form ("reply within one business day").
WHAT_IS_NEEDED: Human requests pilot credits via the Chivox contact form and provides appKey +
  secretKey (or MCP key) via environment variable (never committed).
WHAT_THE_HUMAN_MUST_DO: Submit the contact form for pilot credits; once keys arrive, the agent
  can run the 14-case corpus through en.word.score / en.sent.score and archive raw JSON,
  including age/level settings if exposed.

No corpus cases run. All evidence is E1 documentation.
