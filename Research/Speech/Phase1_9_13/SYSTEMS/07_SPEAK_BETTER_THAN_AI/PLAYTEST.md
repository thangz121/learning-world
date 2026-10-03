# speak-better-than-ai — Playtest

STATUS: **NOT RUN — browser-only** (documented).

BLOCKED_AT: No headless/browser automation available in this environment; the app requires a
Chromium browser with microphone (or its built-in AI-reference grading path) to execute.
WHAT_THE_HUMAN_MUST_DO: `npm install && npm run dev`, open in Chrome, allow mic, run the
14-case corpus manually (or use the AI-reference path with bundled sample audio).

Instead of running, this audit performed an E3 source review: EOS parameters, model ids,
alignment and scoring formulas were extracted directly from `src/main.js` and
`src/phoneme-worker.js` (see TECHNICAL_EVIDENCE.md). Raw files kept outside Git
(`Research/Speech/ExternalData/phase1_9_13_repos/`, gitignored).
