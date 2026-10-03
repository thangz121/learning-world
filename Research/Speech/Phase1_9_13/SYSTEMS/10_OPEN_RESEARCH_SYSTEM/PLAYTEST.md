# slip — Playtest

STATUS: **NOT RUN — browser-only** (documented). E3 source audit performed instead.

BLOCKED_AT: Requires a modern Chromium browser + microphone (or bundled demo analysis); no
headless automation available in this environment.
WHAT_THE_HUMAN_MUST_DO: `npm install && npm run dev`, open the app, use a demo line or the mic,
and observe expected→heard per-phone feedback. Optional: the server seam in `services/infer/`.

No corpus cases run. Algorithm details extracted from `lib/gop.ts` and `lib/align.ts`.
