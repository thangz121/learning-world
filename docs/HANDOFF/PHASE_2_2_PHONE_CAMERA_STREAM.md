# Phase 2.2 — Phone Camera → Realtime Game Stream

## Status

**FAIL** (blocked on real-phone + real-build E2E — no physical phone exists in
this environment). Everything automatable is green; nothing is claimed beyond
what was measured. The failure is scope-evidence, not code-evidence: the
camera foundation is implemented, unit/loopback-proven, and regression-clean,
but §33 is explicit — PASS requires a real phone camera inside the actual game
build, and that run has not happened.

## 1. Objective

Add realtime phone-camera streaming into the actual game over the PROVEN
Phase 2.1 phone→PC architecture: phone camera → LAN → PC receiver → game
frame source → UI video box → top-right corner. Transport + display ONLY
(no recording, no AI — Phase 2.3 owns storage, nobody owns face AI).

## 2. Existing Phase 2.1 architecture reused

Audited (`docs/HANDOFF/PHASE_2_1_PHONE_MIC.md` + source):

| Proven mic mechanism | Camera reuse |
|---|---|
| Envelope `[u32 len][u8 kind][u32 serial][u32 seq][payload]`, big-endian (`PhoneMicProtocol`) | Same envelope SHAPE byte-for-byte (`PhoneCameraProtocol`; P17O pins byte-equality) |
| `SUBSCRIBE` + HELLO-first (no AUDIO-before-HELLO) | Same (`KindSubscribe`, `KindHello`, greeting before broadcast join) |
| Session latch + foreign-serial drop (`NetworkMicrophoneCapture`) | Same (`CameraFrameSource` latch + `DroppedForeign`) |
| UP/DOWN presence, STOP-vs-DOWN distinction (`PhonePresenceWatcher`) | Same event shape (`PhoneCameraWatcher`: `CameraUp/Frame/Stopped/Down/Error`) |
| Bounded queue, drop-oldest, counted (`MaxBufferedSec`) | Stricter: depth EXACTLY ONE, replace-stale, counted (`ReplacedUnread`) |
| Worker-thread subscriber, latest-edge-wins poll, never throws, no Unity API | Same threading contract, separate thread |
| Gateway: HTTPS+WSS (TLS), QR, `/health`, STEP logs, `--selftest`, `--bridge-only` | Same process: `/camera` page, `/cam` WS, `/cam-qr(.png)`, health extended, selftest +6 |
| Phone page resilience (ready-timeout, heartbeat, fresh-session resume) | Same patterns in `phone_camera_page.html` |
| Code-built uGUI, no raycaster, `raycastTarget=false` (`MicStatusHud`) | Same (`PhoneCameraHud`, order 55 below mic 60) |
| R10 lesson: fullscreen root, Box owns children, all-RectTransform | Same layout discipline (pinned by P17M) |

Deliberately NOT shared (§8 media independence): TCP port (mic 8451, camera
8452 — a JPEG can never head-of-line-block speech), socket, thread, session
serial space, frame store. A camera flood delays camera edges only.

## 3. New camera architecture

```
PHONE (front camera, 320x240 JPEG q60 @ ~10fps)
  --WSS /cam--> gateway (same TLS origin as mic, STEP C + CAM logs)
  --TCP 127.0.0.1:8452--> PhoneCameraWatcher (worker thread, SharedKernel)
  --> CameraFrameSource (depth-ONE slot, session latched, SharedKernel)
  --> GameCameraStreamService (main thread: edges + JPEG decode, A_World)
  --> CurrentTexture (ONE reused Texture2D, null unless Live)
  --> PhoneCameraHud (top-right box BELOW the mic widget, A_World)
```

New files: `_SharedKernel/{PhoneCameraProtocol,CameraFrameSource,
PhoneCameraWatcher}.cs`, `A_World/{GameCameraStreamService,PhoneCameraHud}.cs`,
`tools/phone_camera_page.html`. Modified (additive only): `MarketBootstrap.cs`
(+17 lines, null-safe camera create), `tools/phone_mic_gateway.py` (+310,
camera branches only), `.gitignore` (2 QR artefacts).

## 4. Phone-side implementation

`tools/phone_camera_page.html` (vanilla JS, zero CDN, offline-first):
facingMode `ideal:user` (front camera, degrades to any camera), local
`<video>` preview for framing, canvas cover-crop to 320x240 (no stretch),
`toBlob JPEG 0.6` at 10 fps with send-busy drop (never queue), `cam-start`
`{session,width,height,format:jpeg,fps}` → `cam-ready` (6 s anti-wedge
timeout) → binary frames → `cam-stop`/`cancel`. Permission states explicit:
UNKNOWN/REQUESTING/READY/DENIED/NO_CAMERA/ERROR. Heartbeat 15 s,
fresh-session auto-resume, online/offline + visibility hooks — all mirrored
from the mic page. PC sees denial as rejection/error, never a crash.

## 5. PC-side implementation

Gateway camera path (same process, §2 table): `serve_cam_bridge` (own
subscriber list), `handle_cam_phone` (`cam-start` validates `format:jpeg`,
duplicate-session rejected), `on_cam_frame` (empty/oversize/non-JPEG dropped,
never forwarded; JPEG SOI `FF D8 FF` gate), `on_cam_stop`/`cancel`, link-down
→ `K_CAM_ERROR link_down` + `K_CAM_DOWN`. Unity: `PhoneCameraWatcher`
(HELLO/presence skip, FRAME→source post, STOP→slot clear, DOWN→`ResetSession`,
ERROR→source error) + `CameraFrameSource` (§2). No disk anywhere (privacy §11:
`--record-dir` touches AUDIO only; camera has no record path at all).

## 6. Unity integration

`GameCameraStreamService` (MonoBehaviour, `Bind/StartService/StopService`,
`OnDestroy` disposes watcher + texture): drains latest watcher edge per frame,
decodes JPEG→the ONE `Texture2D` via `LoadImage` on FRAME edges only (no
per-frame allocation, decode ms + fps estimate measured). `CurrentTexture`
returns the texture ONLY in `Live` (stale faces are hidden by construction).
`MarketBootstrap.Build` creates service + HUD in try/catch AFTER the mic block
(signature unchanged — zero regression surface for existing callers);
`CameraStream` property exposes the service. UI consumes `State` +
`CurrentTexture` only — no sockets/sessions/bytes leak into presentation.

## 7. UI implementation

`PhoneCameraHud` (code-built uGUI, canvas order 55): Box 220x200 anchored
bottom-left at (20,20) — diagonally opposite the mic box (top-right) and the
replay button (bottom-right), clear of the objective chip (top-left), the
centered QR modal, and world center (NPC faces, targets, bubble). `RawImage` 200x140 + `AspectRatioFitter FitInParent` 4:3 (no
stretch), per-state placeholder text (never a frozen face), one-line status.
No `GraphicRaycaster`, every Graphic `raycastTarget=false` (pinned P17M).

## 8. Camera format / resolution / FPS

JPEG only (simplest realtime frame the transport carries; no H.264/VP9/AV1 —
explicitly out). Default `PhoneCameraConfig`: 320x240, 60 fps, q60
(~10–20 KB/frame), max 200 KB, stale 3000 ms. Phone grabs rAF-throttled to
60/s (screen average; 90/120 Hz phones save battery with zero visible loss),
minimum 30. `Validate()` bounds
(160–640 × 120–480, 1–120 fps, q30–85, stale 1–10 s) fail at Bind, never
mid-game. Not user-facing (code constants, §4). Phone cover-crops to 4:3 so
the game never stretches.

## 9. Queue / backpressure strategy

Depth ONE (`CameraFrameSource._latest`): POST replaces, game consumes newest.
`Received/DroppedForeign/DroppedInvalid/ReplacedUnread/Displayed` counted.
No list, no growth path — memory bounded by `MaxFrameBytes` at any phone rate.
Phone capture loop drops while `sendBusy` (never queues on the phone either).
Gateway broadcasts immediately (no camera-side queue). "Show the latest face"
by construction.

## 10. Connection state machine

`PhoneCameraState`: Disabled → WaitingForPhone → Connecting →
ConnectedWaitingFrames → Live; TempDisconnected (stale/DOWN/bridge lost);
Error (protocol/permission, recovers on next good edge); Stopped (service
stop, terminal). `NextStateAfterFreshness` pure helper pins the honesty table
(P17K): fresh ⇒ Live; stale Live ⇒ downgrade (texture gate hides); transport
loss ⇒ TempDisconnected (never frozen Live). STOP ⇒ idle slot cleared (P17H).
DOWN/transport-loss ⇒ `ResetSession` (P17I).

## 11. Disconnect / reconnect behavior

Phone disconnect / Wi-Fi loss / tab close / sleep / permission revoke /
gateway death / game restart: watcher posts DOWN/TransportLost/GatewayDown →
source reset → TempDisconnected placeholder; game runs on (camera is
presentation-only, §17). Reconnect = fresh session (new serial latch; old
bytes cleared before latch, §20; double-START guard retires the old sid).
No infinite retry (watcher 2 s/1.5 s backoff, probes stay one-shot), no leak
(Dispose joins ≤3 s, texture destroyed with service, no orphan sockets).

## 12. Privacy behavior

RAM-only path: phone → TLS → gateway broadcast → depth-ONE slot → GPU texture
→ replaced/discarded. No recording switch exists for camera (mic
`--record-dir` is audio-only), no file writes, no screenshots, no cloud
endpoints in gateway/page except `wss://<same-origin>` (grep-verifiable), no
face/AI/biometric code anywhere (P17O + suite compile prove no such symbols
were added). QR PNGs embed LAN IP only and are gitignored.

## 13. Performance observations

No game-build profiling was possible (no real build run — see §15). Design
budget: decode-on-edge only (~10/s max, one `LoadImage` into a reused
texture), zero per-frame allocation in Update (edge poll + freshness check),
no readbacks/conversions/AI, own thread off the main loop, own port off the
audio path. Measured in EditMode: real-JPEG decode succeeds (P17L2),
decode-failure counter works, fps estimator updates. Gameplay-FPS impact is
UNMEASURED — must be profiled in the real-build E2E (§15 runbook).

## 14. Automated test results

CT-P17 (17 tests) + full EditMode on Unity 6000.6.0f1 batchmode:
**288 total — 287 pass, 0 fail, 1 skip** (skip = pre-existing conditional
`P13M4_RealSamplesIfPresent`, by design). P17 17/17: config matrix, envelope
roundtrip ×6 kinds, garbage rejection, JPEG validation, latest-wins,
foreign-drop, invalid-drop, STOP-clears, reset-epoch, freshness, state table,
service edges, real-JPEG decode + texture REUSE (same object across frames) +
corrupt-counted, HUD layout/contract, HUD states, **real-socket loopback**
(fake cam bridge → watcher → source: 1 accepted / 1 invalid / 1 foreign /
UP+STOP edges + byte counts), mic-untouched (byte-identical envelope, separate
ports). Gateway `--selftest`: 18/18 (12 mic + 6 cam). Authoring validator:
PASS. Bridge loopback vs real gateway `--bridge-only`: 8451
`phone-mic-gateway/1` + 8452 `phone-cam-gateway/1` PASS (exit 0).
Two P17 iterations failed first (test bugs, not production — `EncodeToJPG`
casing, case-sensitive status assert, transient-edge race asserting counters
instead); production code unchanged by those fixes.

## 15. Real phone E2E results

NOT RUN — no physical phone in this environment. Runbook for the user (PC +
phone on same Wi-Fi, certs already in `tools/`):

1. `python tools/phone_mic_gateway.py --cert tools/lan.crt --key tools/lan.key`
   → expect `[CAM 1/4]` (camera page + QR) + `[STEP 3/9]` (mic QR).
2. Scan `tools/phone-camera-qr.png` (or open `https://<pc-ip>:8443/cam-qr`) →
   camera page → START CAMERA → allow camera → expect `[CAM 3/4]`, `[CAM 4/4]
   first frame flowing`.
3. Build + run the game (dev build) → face box top-right under the mic widget:
   `Waiting → Connecting → Ready → ● LIVE` with the real face, 4:3, no stretch.
4. Move/play: stream continues; mic matrix (Phase 2.1 handoff §Manual E2E)
   still passes simultaneously (A–K).
5. STOP on phone → box shows `READY`, no frozen face. Close page → `LOST`.
   Reopen + START → LIVE again (fresh session). Airplane-mode mid-stream →
   `LOST`, game continues, quest never fails.
6. Report: `Player.log` cam lines + gateway CAM lines + phone STEP log per case.

## 16. Visual inspection results

NOT DONE (requires §15). Checklist for the run: box position/size below mic
widget, face visibility, 4:3 no-stretch, no black/frozen/flicker/tear frames,
stale→LOST (no frozen face), no overlap with QR/objective/bubble/NPC faces/
world center at 1280x720 + one smaller resolution.

## 17. QR regression

Mic QR untouched (same file/route/STEP 3; `--selftest` 12/12 mic checks;
P14/P15 suites green inside the 288). New camera QR is a separate file/route
(`/cam-qr.png`, `phone-camera-qr.png`, gitignored). In-game `MicSetupDialog`
QR path untouched (P15T green). Same-phone QR-scan + camera-stream conflict:
none — QR is scanned by the NATIVE camera app BEFORE the browser page takes
the front camera; the browser page uses `facingMode:user` while the QR scan
uses the rear camera via the OS. If the OS denies concurrent use, the page
reports NO_CAMERA/ERROR explicitly (documented, not a bug unless it violates
this behavior).

## 18. Microphone regression

Full suite green (P12/P13/P14/P15/P16 inside the 288, zero failures);
`PhoneMicProtocol`/capture/device/gateway-mic code paths byte-identical except
purely additive camera branches (diff: `MarketBootstrap` +17 additive lines,
gateway +310 camera-only branches, `.gitignore` +2). P17O pins the shared-shape/
separate-socket contract + canonical mic format. Live mic+camera simultaneity
is UNPROVEN (needs §15 run A–K with a real phone).

## 19. Bugs found

1. Test-compile `EncodeToJPG` casing (test bug, fixed, no production touch).
2. Test asserts caught 2 more test bugs (status casing; transient-edge race →
   counters). Production code unchanged by all three fixes.
3. No production bug found by automated evidence (honest scope: loopback only).

## 20. Bugs fixed

Items 1–2 above (test-only). No production-code defect has been observed yet;
the real-build E2E (§15) is where P0/P1 verdicts can actually be earned —
per bug policy, no fix may be declared from automated tests alone.

## 21. Known non-blocking limitations

- No in-game camera settings UI (config is code constants, §4 — by design).
- No camera permission prompt inside the game (phone browser owns it; game
  shows WAITING/ERROR honestly).
- No `--inject-jpeg` gateway test mode (mic has `--inject-wav`; camera
  loopback was proven by P17N + bridge HELLO instead).
- `LoadImage` may realloc GPU memory on resolution change (object reused;
  acceptable at ≤10 fps preview).
- Single phone only (matches mic single-session scope).
- `fpsEstimate` is display-side only, not end-to-end latency (honestly labeled
  `fps~` in `StatusLine`; capture timestamps are a Phase 2.3 concern).

## 22. Explicit Phase 2.3 boundary

NOT built: recording, encoders, muxing/containers, file storage, recording
lifecycle/recovery/stress, capture timestamps, camera effects/filters, face
AI of any kind, analytics, multi-user. Phase 2.2 ends at realtime DISPLAY
(the slot + texture are discarded/replaced, never persisted).

## 23. Final verdict

**FAIL** — per §33, PASS requires the realtime camera working inside the
actual game build with a real phone, and that evidence does not exist yet
(§15–16 explicitly NOT RUN). Automated evidence (288 EditMode, selftest 18/18,
validator PASS, dual-bridge loopback PASS), build evidence (assemblies compile
clean in-batch; no player build attempted), runtime evidence (loopback only),
real-phone evidence (none), visual evidence (none), performance evidence
(design + EditMode decode only). To PASS: run §15 with a real phone, attach
logs + screenshots, re-run regression, then relock. The foundation is ready
for that run; nothing here pre-claims it.
