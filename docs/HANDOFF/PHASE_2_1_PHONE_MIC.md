# Phase 2.1-local M6 — Phone Microphone Input Transport

## Status

**PASS WITH OPEN ITEMS** — phone audio is now ANOTHER INPUT to the frozen
speech pipeline (no new engine, no retune, no cloud). Automated proof is
complete (236 EditMode green, loopback E2E through the real gateway over
real sockets). The one open item is the supervised real-phone run, which
requires a physical phone on the user's LAN and is documented as a
step-by-step procedure in this file (§Manual E2E).

## Baseline

- Checkpoint `d3ede22` (M5 complete). Git was CLEAN at start.
- M1–M5 untouched: no DSP, threshold, policy-fusion, or assessment file
  modified (verified by diff: zero changes outside the files listed below).

## 0. Survey findings (what was already present)

- `ISpeechAudioCapture` already existed as the capture seam, and
  `SpeechRecognizer` depends ONLY on `IMicrophoneDevice` +
  `ISpeechAudioCapture` + `ISpeechAssessmentProvider`. Phone input needs
  exactly one new capture class — no recognizer change (§3 shape confirmed).
- `SpeechFailureReasons.NetworkError` already existed (unused until M6).
- **No prototype reuse**: no phone/LAN/server/WebSocket/bridge code exists
  anywhere in the repo, `tools/`, or Temp logs. The earlier Claude Code
  Phone→LAN→PC proof lives outside the repo and was NOT accessible, so M6
  builds a minimal integration from the current architecture (per the brief:
  no assumption that any proof was production-ready).
- Production `GameInstaller` still wires the OLD v6 speech stack (Azure
  router); the 2.1 local stack lives in tests + harnesses. M6 plugs into
  the 2.1 seams only and does NOT touch `GameInstaller` (same staging rule
  as M5's Bootstrap wiring).

## A. IMPLEMENTED

C# (additive, `Assets/D_Audio/`, frozen systems untouched):
- `PhoneMicProtocol.cs` — wire contract + `SpeechInputSource`
  (LocalMicrophone/Phone) + canonical-format guard (mono PCM16 @16 kHz,
  reject-never-adapt) + PCM16→float32 + strict bridge envelope
  codec (malformed → reason, never throws).
- `PhoneMicrophoneDevice.cs` — `IMicrophoneDevice` over link state
  (Ready only while linked; else NoDevice/PermissionDenied/Error), so the
  existing no-mic short-circuit + `SkippedNoMic`/deferral work unchanged.
- `NetworkMicrophoneCapture.cs` — `ISpeechAudioCapture` over phone audio:
  session latch + foreign-serial drop counting + seq-gap detection +
  bounded buffer (overflow drops oldest, counted) + wall-clock exit +
  failure mapping (down-at-start → `mic_unavailable`; mid-drop →
  `network_error`; cancel → Cancelled; clean empty stop → NoSpeech).
  Includes `TcpPhoneAudioTransport` (plain TCP loopback, `System.Net`
  only, lazy dial+SUBSCRIBE, never throws out of `IsConnected`).

Gateway + page (`tools/`, stdlib only, no new dependencies):
- `phone_mic_gateway.py` — WSS + HTTPS page server (TLS, LAN), minimal
  RFC6455 (stdlib `socket`+`ssl`+`threading`), canonical-format validation
  at `start` (reject 48 kHz/stereo/non-PCM16), session lifecycle (§9),
  TCP bridge to Unity on 127.0.0.1:8451, bounded queues, `--record-dir`
  opt-in WAV only, `--bridge-only` + `--inject-wav` loopback test mode
  (never serves phones), `--selftest` codec checks. No DSP, no recognition,
  no cloud, no GPU, no storage by default.
- `phone_mic_page.html` — vanilla JS, zero CDN (offline-first):
  `getUserMedia({audio:true})` only (never camera), AudioContext 16 kHz
  resample at the boundary, Int16 PCM binary WS frames, START/STOP,
  CONNECTED/MIC_READY/STREAMING/STOPPED/ERROR/RECONNECTING states, level
  meter, duration/packets, auto-reconnect with fresh sessions (§12).
  WSS URL derives from page origin (zero phone config).

Config:
- `.gitignore`: `*.pem`, `*.key`, `phone-mic-recordings/` (keys/recordings
  never committed, §33/§20).

## B. AUTOMATED TESTED

- `CT-P14_PhoneMicTransport` (20 tests): control parse, malformed control,
  bridge round-trip, garbage rejection, format matrix, PCM conversion,
  session delivery, foreign-serial drop, seq-gap tolerance, no-transport
  short-circuit, mid-capture LinkDown → env (never WrongWord), cancel,
  silent-transport timeout (wall-clock, no hang), capability lifecycle,
  SkippedNoMic+deferral, phone-pass → exactly one `WordSpokenEvent` +
  frozen Speak gate completes, env-failure → zero events + mastery
  untouched + quest open, reconnect session independence, M5 local-path
  regression, **real TCP loopback** (`TcpListener` ↔ production
  `TcpPhoneAudioTransport`).
- Gateway `--selftest`: 8/8 codec checks (bridge framing, PCM range,
  RFC6455 accept vector, control parity).
- Full suite: **216 → 236 (235 pass + 1 Ignore-by-design)**, zero failures.
- `validate_content.py`: PASS (authoring).

## C. REAL LOOPBACK E2E PROVEN (real gateway + real sockets + real Unity)

Proof build `Succeeded` (errors=4 headless noise) → gateway
`--bridge-only --inject-wav` (16 000-sample tone) → foregrounded player
`-p14survey` → `P14SURVEY COMPLETE`:
- capture `samples=16000 rate=16000 ch=1 dur=1.00 mean=0.1432 voiced=0.92`
  `err=` (empty) `droppedForeign=0 seqGaps=0 overflow=0` — byte-exact.
- assessment `Partial` (`acoustic=0.0 weak-ending repetition`) — honest:
  a tone is not speech; engine refuses to pass it, refuses to fail it.
- `capture_ms=632 inference_ms=15 fps 60→61`, 3×FACE_OK, 0 exceptions.
- Gateway log: subscribe → `serial=1 samples=16000` → `COMPLETE chunks=10`.

## D. OBSERVED ONLY (not claimed as proof)

- M5's N=1 ambient-room StrongPass stands as an unsupervised observation;
  M6 tuned nothing on it (frozen thresholds, §17 held — diff-verified).
- The injected tone's `Partial` is correct policy behavior on non-speech,
  not evidence about real voice quality.

## E. NOT PROVEN (explicit)

- **Supervised real-phone run** (matrix A–K in the brief): needs a physical
  phone + user permission taps — procedure below, awaiting the user run.
- Audible quality / human intelligibility (standing Phase 4 scope).
- iOS Safari specifics beyond documented notes (no iPhone in this lab).
- Long-soak Wi-Fi behavior (brief disconnect/reconnect covered by tests
  P14K/P14T only at the transport-simulation level).

## F. KNOWN LIMITATIONS

1. Phone page uses `ScriptProcessorNode` (deprecated but universal incl.
   iOS); `AudioWorklet` migration is a later polish, not a correctness gap.
2. Gateway is single-phone (one live session); multi-phone is out of scope.
3. `TryConnectSync` blocks ≤1.5 s when the gateway is absent (bounded, once
   per attempt; no retry storm — runner bounds attempts 1..5 as before).
4. Self-signed TLS needs one-time trust per phone (procedure below); this is
   inherent to LAN-only HTTPS, not a workaround.
5. Browser backgrounding may pause capture (OS behavior); reconnect creates
   a fresh session — stale audio never merges (§12, pinned P14H/P14T).

## G. NEXT STEP

User runs §Manual E2E with a real phone and pastes the survey/phone logs;
any mismatch becomes a transport issue (never a threshold change). After
that: quest #3 stager trigger (separate task, §37 held — not started here).

## Manual E2E (user procedure)

Requirements: PC + phone on the SAME Wi-Fi; Python 3 on PC; mkcert (or
openssl) once for the LAN certificate. Internet is NOT needed after setup.

1. PC setup (once): install mkcert (`choco install mkcert` or GitHub
   release); `mkcert -install`; find PC LAN IP (`ipconfig`, e.g.
   192.168.1.25); `mkcert 192.168.1.25` → `192.168.1.25.pem/.key` in
   `tools/` (gitignored). Alternative without mkcert: `openssl req -x509
   -newkey rsa:2048 -keyout lan.key -out lan.crt -days 825 -nodes -subj
   "/CN=192.168.1.25" -addext "subjectAltName=IP:192.168.1.25"`.
2. Windows firewall: allow inbound TCP 8443 (Private network only).
3. Start server: `python tools/phone_mic_gateway.py --cert <ip>.pem
   --key <ip>.key` (keep the window; logs show connects/sessions).
4. Phone trust: with mkcert, install `mkcert -CAROOT`/rootCA.pem on the
   phone (Android: Settings → Security → Install certificate → CA;
   accept the warning — LAN-only CA). With openssl self-signed: open
   `https://<pc-ip>:8443` and accept the warning explicitly (test only).
5. Phone page: open `https://<pc-ip>:8443/` → expect PC: CONNECTED.
6. Unity: build/run the game (dev build), select Speech Input: Phone,
   start the `say_ball` exercise; phone shows MIC_READY.
7. Matrix: A clear "ball" → Pass/StrongPass path; B silence 3 s →
   NoSpeech; C "ba" → Partial; D "apple" → WrongWord; E airplane-mode
   before START → SkippedNoMic/defer message; F airplane-mode mid-word →
   "Connection lost. Try again.", quest open; G reconnect → new attempt
   works; H PC mic unplugged + phone → works; I neither → "No microphone
   available."; J phone absent → PC path unchanged (M5 regression); K deny
   browser permission → permission error, no speech failure.
8. Offline check: unplug WAN (keep Wi-Fi/AP on) → A still works (FAIL
   otherwise, §29).
9. Report: paste `Player.log` P14 lines + gateway lines + phone Status
   line per case. No recordings needed (and none leave the LAN).

## Troubleshooting

- `getUserMedia` blocked → page not on HTTPS (or clock skew breaking the
  cert): use the exact `https://<ip>:8443/` URL, fix device clocks.
- Phone can't reach PC → different AP/VLAN or firewall: `ping <pc-ip>`
  from phone (app) first; re-check step 2.
- `protocol_error:need mono pcm16@16000` → old page cached: hard-reload
  (page guarantees 16 kHz via AudioContext; gateway rejects anything else).
- Unity `mic_unavailable` at attempt → gateway not running or wrong
  bridge port (default 8451 loopback).
- Stale session after Wi-Fi drop → wait for DISCONNECTED → auto-reconnect;
  every reconnect is a fresh session by design (no resume).

## Security / privacy (§20)

Phone → TLS → LAN gateway → loopback Unity. No cloud endpoint exists in
this path (grep: no URL in gateway/page except `wss://<same-origin>`).
Raw audio lives in bounded RAM only; `--record-dir` is explicit, local,
and gitignored. No raw audio in logs (energies/decisions only). No
transcript shown on the phone. No identity/biometrics.

## Regression protection (§32)

- M5 suite intact inside the 236 (P14S re-pins the local staged beat).
- PC microphone classes untouched (zero diff).
- Assessment/policy/DSP files untouched (zero diff — verify:
  `git diff --stat` shows only the 6 new files + `.gitignore`).
- Phone path adds an option; default remains LocalMicrophone.

## Final evidence

- Commit: M6 (this change; `git status` clean, temp = 0 files).
- EditMode **236 (235 + 1 Ignore)** pre- and post-lockdown.
- Proof build `Succeeded` + P14 loopback COMPLETE (byte-exact, §C).
- Final clean build `Succeeded` + boot 3×FACE_OK + 6/6 SHOE + 0 exceptions.
- Validator PASS (authoring).

## Final verdict

**PASS WITH OPEN ITEMS** — the open item is solely the supervised
real-phone matrix (user-run, procedure above). Nothing in the automated
or loopback evidence required a speech-code change, and none was made.

## QR quick-start (2026-09-15 addendum — no IP typing, step logs)

The manual `ipconfig` + hand-typed `https://<ip>:8443/` flow above still
works, but the gateway now does it for you:

1. Double-click (or Run with PowerShell):
   `tools/start_phone_mic.ps1` — it auto-detects the LAN IP, checks the
   cert, starts the gateway, and opens `https://127.0.0.1:8443/qr` on the
   PC showing a BIG QR. (Or run the gateway directly:
   `python tools/phone_mic_gateway.py --cert tools/lan.crt --key tools/lan.key`.)
2. Scan that QR with the phone camera → the page opens, no typing.
   The same QR is saved to `tools/phone-mic-qr.png` (gitignored) and
   served live at `https://<pc-ip>:8443/qr.png` (QR payload is just the
   page URL; offline-first, stdlib-only — engine is vendored
   `tools/qrcodegen.py`, Project Nayuki MIT).
3. If the PC IP changed (DHCP) and STEP 2 FAILs the cert check, either
   re-issue the cert for the new IP (same command as §Manual E2E step 1)
   or give the PC a DHCP reservation so the IP stops changing.
4. Watch how far it got — PC window logs `[STEP 1/9]`…`[STEP 9/9]`
   (1 lan-ip · 2 cert · 3 QR · 4 bridge · 5 HTTPS/scan · 6 phone WS ·
   7 CAPTURING · 8 audio flowing · 9 stop/complete); the phone page shows
   STEP 1/5…5/5 + a timestamped steps log mirroring PC steps 6–9.
   `/health` now also returns `{lan_ip, page_url, steps_total}`.

## Standalone link test (2026-09-15 — no game, no Unity)

`tools/test_phone_link.py` plays the Unity side (bridge SUBSCRIBE + frame
watch, stdlib only). Two flows, two windows on the PC:

- Flow A — bridge path, NO phone:
  `python tools/test_phone_link.py --gen-tone tone16k.wav` (once), then
  window 1: `gateway --bridge-only --inject-wav tone16k.wav`, window 2:
  `test_phone_link.py --expect-session` → PASS = HELLO + 10 AUDIO chunks
  (16000 samples, 0 gaps) + clean STOP (verified 2026-09-15, exit 0).
- Flow B — real phone, NO game: window 1: gateway normal (QR!), window 2:
  `test_phone_link.py --wait 60`, then scan QR → START → speak → STOP →
  PASS = live AUDIO + STOP (same verdict lines as Flow A).
- Idle gateway + `--wait 3` → exit 0 informational ("gateway OK, phone
  idle"); `--expect-session` with no session → exit 1; bridge down →
  exit 2 ("is the gateway running?").

## In-game mic-setup gate (2026-09-15 addendum — not standalone anymore)

Startup prompt + periodic recheck + skip-listening now live INSIDE the game:

- Game start with no mic/headset-mic listed → modal offer (parent-facing,
  Vietnamese): "Hiện đang không có microphone kết nối, bạn có muốn kết nối
  bằng điện thoại không?" [Có, dùng điện thoại] [Bỏ qua]. Unity's
  `Microphone.devices` is capture-only (speakers never appear), so the
  speaker-vs-headset distinction holds by construction; names only tune the
  wording (`D_Audio/MicDeviceClassifier.cs`).
- YES → instructions panel (chạy gateway → quét QR → START) + live probe of
  the phone↔gateway↔PC link (`PhoneLinkProbe`: AUDIO/STOP frame = linked,
  HELLO-only = gateway idle, TCP refused = gateway down) + [Kiểm tra lại] /
  [Bỏ qua bài nghe]. Link observed → phone device Ready → play.
- Background: PC list re-polled every 5s; phone link re-probed every 30s in
  phone mode — all SILENT (never a mid-play popup; loss → quiet skip,
  recovery → quiet ready). Re-prompt happens ONLY at listening-exercise
  entry (`CheckBeforeListening`, once per exercise).
- Decline/unavailable → frozen `SkippedNoMic`/deferral ("tạm thời bỏ qua bài
  nghe", mastery untouched) via `CompositeMicrophoneDevice` (local wins,
  phone fallback) as the future recognizer input.
- Files: `_SharedKernel/MicSetupGate.cs` (+`IPhoneLinkDevice`), 
  `_SharedKernel/PhoneLinkProbe.cs` (kernel because LWE.World refs kernel
  only; envelope constants mirrored from `PhoneMicProtocol`, pinned by
  CT-P15 loopback), `D_Audio/{MicDeviceClassifier,CompositeMicrophoneDevice}.cs`,
  `A_World/{MicSetupDialog,MicSetupMonitor}.cs`, `_Bootstrap/MicSetupBundle.cs`
  + GameInstaller/MarketBootstrap wiring (bundle null = feature off).
- Evidence: EditMode 252 (251 pass + 1 pre-existing conditional skip),
  CT-P15 16/16 (classifier/gate/composite/loopback-probe/frozen-skip/UI).

## In-game QR + gateway auto-start (2026-09-15 addendum)

Accept (Co, dung dien thoai) now: pauses the game (Time.timeScale=0), auto-starts
tools/phone_mic_gateway.py as a child process (certs from <repo>/tools), shows its QR
PNG inside the WAIT panel (MicSetupDialog.SetQrImage), and re-probes the bridge
every 3 s. PhoneLinked -> QR off + resume (gateway keeps running for captures).
30 s without link -> Skip button appears under the QR; 120 s -> auto-skip
(gate SkipWaiting, resume, child gateway killed). All timers use unscaled time.
Every step is try/caught: no python / no certs / player build without tools/ =
manual instructions fallback. Driver: A_World/MicSetupMonitor.cs; UI: A_World/MicSetupDialog.cs
(P15Q pins QR/late-skip); wiring: MarketBootstrap.FindToolsDir -> Bind(toolsDir).

## Transport hardening (same day)

- Double-START guard: one socket = one live session (old retired as STOP when 0-chunk,
  else replaced error) + page starting flag. Fixes the observed ~2x chunk rate.
- Bridge HELLO is now sent BEFORE joining the broadcast list (no AUDIO-before-HELLO
  for mid-stream subscribers).
- Gateway audio logs now show chunks/audio-sec/peak level/wall/listeners every 100 chunks,
  a no-listener hint on first chunk, and a one-time 10 s bridge-bound notice.

## Presence: mid-game drop/STOP detection (2026-09-15 addendum)

Bridge kinds 5/6 (additive, same envelope; PhoneMicProtocol.KindPresenceUp/Down):
- UP (ws-connected) on phone WS open; DOWN (ws-closed) on WS close. Session audio
  lifecycle still uses AUDIO/STOP/ERROR, so STOP-without-DOWN = user pressed STOP
  (page may stay open, next START resumes) while DOWN = phone gone mid-game.
- Captures skip presence like HELLO (TcpPhoneAudioTransport loops); probe ignores them
  (still needs AUDIO/STOP for PhoneLinked). P14U pins decode + skip over real sockets.
- _SharedKernel/PhonePresenceWatcher.cs: persistent subscriber on a worker thread,
  latest-edge-wins polling (ReadState seq), never throws, no Unity APIs. P15R/S pin
  UP/AUDIO/STOP/DOWN over loopback + GatewayDown on refused port.
- MicSetupMonitor runs the watcher in WaitPhoneLink/ReadyPhone: AUDIO -> linked path
  immediately; UP -> status phone-open; STOP -> log + status (stays Ready);
  DOWN/TransportLost/GatewayDown -> ReportLinkDown + silent skip, next exercise
  re-offers. One-shot probes stay as backup. Live proof 2026-09-15: phone sessions
  1x rate (93 chunks/23.8 s, 57 chunks/15.0 s), clean STOPs, raw bridge bytes verified
  HELLO/UP/DOWN/AUDIO.
E2E 2026-09-15: EditMode FULL 256 (255 pass + 1 pre-existing conditional skip P13M4, 0 fail) on Unity 6000.6.0f1 batchmode; P14 21/21, P15 19/19 (new P14U/P15Q/P15R/P15S green).
