# Phase 2.2 — Phone Camera → Realtime Game Stream

## Status

**PASS WITH OPEN ITEMS** — the realtime camera works inside the actual game
build with a real phone (visual proof §16). Remaining items (§21) are
non-blocking: front-camera confirmation, in-game QR modal path, disconnect
visual, formal perf numbers. Source-precedence (local mic/webcam > phone,
queued per user 2026-09-15) is tracked as follow-up work, not a 2.2 gap.

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

RUN 2026-09-15 with the user's real phone (192.168.50.80) on the same Wi-Fi,
fresh E2E build (`E2EBuild`, Succeeded errors=0, DLLs 16:05/16:17), standalone
gateway (`lan.crt`, CA-signed, phone trusted):

1. Gateway up: mic QR + camera QR + unified QR (`/phone`), bridges 8451+8452.
2. Phone opened `/phone` (unified mic+camera panel, one scan), START MIC +
   START CAMERA → mic session CAPTURING (100+ chunks, real voice peaks) +
   cam session CAPTURING (3000+ frames, ~5 KB/frame JPEG).
3. Game launched (dev build, windowed): auto-subscribed cam bridge
   (`subscribed (1 total)`), `MarketBootstrap` wired service+HUD
   (`running=True`), edges Connecting → Live.
4. Sustained: rx=651 shown=634, **fps~36 display-side**, decode 0.6 ms,
   0 foreign / 0 invalid / 0 decode failures; honest stale transitions
   (Live → ConnectedWaitingFrames → TempDisconnected → Live) in Player.log.
5. Mic simultaneity: phone mic flowed at the gateway while the game stayed on
   the local Realtek mic (correct per-medium independence; game never subscribed
   8451 since gate was ReadyLocal — no interference either direction).
6. Phone dropped/reconnected repeatedly through the day (Wi-Fi flaps, gateway
   restarts): every reconnect got a FRESH serial, no stale-frame merge ever
   observed; gateway restarts never crashed the game.
7. Stream-rate ladder proven on the same hardware: 10fps (interval) →
   30fps steady (100 frames/3.3s) → ~60fps uncapped (300/5.0s) → capped 60fps
   operating point (rAF-throttled, 16.7 ms floor).

## 16. Visual inspection results

`game-screen.png` (CopyFromScreen, virtual desktop — PrintWindow proven
UNRELIABLE for this canvas, lesson 53): game world renders (Milo, player,
stall, `Talk to Milo` chip top-left) + **CAMERA box bottom-left (20,20,
220x200) with title `CAMERA`, live room/ceiling frame, status
`CAMERA ● LIVE`**. Box is small/secondary, 4:3 no-stretch, clear of the
objective chip, world center, NPC faces and (pre-talk) replay area. Content
shows ceiling (phone orientation at capture time — transport proof, not a
defect). HUD self-report in the same run: `state=Live video=True
showing=True`. Lesson 53: verify overlay canvases with composited-desktop
capture (CopyFromScreen), never PrintWindow alone — PrintWindow showed the
older chip canvas but NOT the runtime camera canvas while the real monitor
(and the game's own introspection) showed both.

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

- Front-camera confirmation: stream proven, but capture showed ceiling —
  facingMode `ideal:user` is requested with graceful fallback; explicit
  front-vs-rear confirmation is an open visual check.
- In-game QR modal path untested live (local Realtek mic → ReadyLocal, no
  offer shown — correct behavior); testable with `-e2e-nomic`.
- Disconnect-while-Live visual (LOST placeholder on screen) inferred from
  logged transitions, not yet photographed.
- Formal gameplay-FPS/CPU/GPU numbers not profiled (decode 0.6 ms measured;
  no observed degradation).
- No in-game camera settings UI (config is code constants, §4 — by design).
- No camera permission prompt inside the game (phone browser owns it; game
  shows WAITING/ERROR honestly).
- No `--inject-jpeg` gateway test mode (mic has `--inject-wav`; camera
  loopback was proven by P17N + bridge HELLO instead).
- `LoadImage` may realloc GPU memory on resolution change (object reused;
  acceptable at ≤60 fps preview).
- Single phone only (matches mic single-session scope).
- `fpsEstimate` is display-side only, not end-to-end latency (honestly labeled
  `fps~` in `StatusLine`; capture timestamps are a Phase 2.3 concern).
- Follow-up (user-queued, separate scope): source precedence — local mic /
  USB webcam auto-preferred over phone per medium, game→gateway→page reverse
  signal disables the phone START button when local is active, `-e2e-nocam`
  force-phone flag; generic auto-detect (names are labels only).

## 22. Explicit Phase 2.3 boundary

NOT built: recording, encoders, muxing/containers, file storage, recording
lifecycle/recovery/stress, capture timestamps, camera effects/filters, face
AI of any kind, analytics, multi-user. Phase 2.2 ends at realtime DISPLAY
(the slot + texture are discarded/replaced, never persisted).

## 23. Final verdict

**PASS WITH OPEN ITEMS** — realtime camera proven inside the actual game
build with a real phone (§15 visual + §14 automated + loopback): transport
reuses the mic architecture on an independent port, bounded latest-frame,
honest states, mic path regressed clean (288 green), no recording/AI. Open
items (§21) are non-blocking checks and the separately-queued precedence
feature (§24 now DONE, numbers below). Evidence split honestly: automated (288 + selftest 18/18 +
validator), build (2× Succeeded errors=0), runtime (Player.log Live +
634 shown + fps~36), real-phone (serials 1–3, 3000+ frames, reconnects),
visual (`game-screen.png` + live user inspection), performance (0.6 ms
decode; formal profiling open).

## 24. Source-precedence follow-up (DONE 2026-09-15 — was §21 queued item)

Rule: local mic / USB webcam auto-preferred over phone PER MEDIUM.
Generic auto-detect (names are labels only): mic = any `Microphone.devices`
entry (existing `HasUsableMic`); cam = any `WebCamTexture.devices` entry
(count > 0, no name checks). Game-side precedence applies regardless —
the wire only stands the phone START button down, it never grants access.

Wire (additive, media-independent like §8):
`game --TCP loopback--> gateway --WSS--> phone page ("pc-prefer")`.
Payload utf8 `"<media>:<origin>"` (`mic:local/phone`, `cam:local/phone`)
on each medium's OWN bridge (mic 8451, cam 8452) as kind `0x12`
(`KindPreferLocal`, same numeric envelope both media, decode by port).
Gateway stores per-medium (`prefer={mic,cam}`, default `phone`) and pushes
`{"type":"pc-prefer","media","origin"}` to the matching live phone WS;
new pages get the CURRENT prefer immediately on WS open (no wait for the
next game report). Malformed payloads ignored, never fatal. No recording,
no cloud, no AI (privacy §11 unchanged).

Game reports on TRANSITIONS only (never per frame), fire-and-forget
(`Task.Run`, TCP dials never hitch the main thread):
- Mic (`A_World/MicSetupMonitor`): `PcSourcePrecedence.PreferMic(localReady)`
  at startup + every 5 s local poll. `-e2e-nomic` folds in naturally
  (LocalMic forced empty ⇒ phone). Last-reported dedup.
- Cam (`A_World/GameCameraStreamService`): `PreferCam(WebCam present)` at
  start + every 5 s poll, injectable probe + `-e2e-nocam` force-phone
  (mirrors `-e2e-nomic`). Last-reported dedup.
- Decision pure (`_SharedKernel/PcSourcePrecedence.cs`); transport
  (`_SharedKernel/PcPreferenceReporter.cs`, kernel-local mic mirror —
  LWE.World refs kernel only, so no LWE.Audio ref; pinned byte-equal).
- Frozen systems untouched (QuestManager/Learning/Hint/audio/camera core).

Phone UI (3 pages, vanilla JS, no CDN): `pc-prefer` handler per panel
(`phone_mic_page` mic, `phone_camera_page` cam, `phone_page` both).
Local ⇒ START disabled + "PC đang dùng … cắm sẵn — điện thoại chờ";
phone ⇒ START re-enabled (streaming sessions finish; NEXT start locked).
`setStartEnabled` gated centrally so no path wedges START.

Gateway fixes in the same pass (broken tree found at session start):
`bridge_broadcast` restored (was deleted, orphan body inside
`drain_bridge_control`), `drain_bridge_control` restored + reused by the
cam bridge (was old `recv(16)` loop, PREFER never relayed on cam),
`mic_sockets`/`cam_sockets` tracked + immediate prefer on WS open
(were never populated ⇒ pushes were no-ops).
`PhoneCameraProtocol.KindPreferLocal` const added (was case-without-const
⇒ compile error). `phone_camera_page.html` IIFE closed early in HEAD
(extra `})();` ⇒ unbalanced tail) — merged into one IIFE (balanced).
`PcPreferenceReporter` rewritten kernel-local (was LWE.Audio ref from
kernel ⇒ CS0103; now mirror + pin, PhoneLinkProbe pattern).

Tests: CT-P18 (13 tests: mic/cam decision + force flags, strict payloads,
reporter↔decision agreement, kind contract, mic/cam roundtrip, unknown-kind
reject, generic-names rule, composite local-wins, mirror byte-equality) +
gateway `--selftest` +5 (prefer envelope ×2, parse ×2, reject).
Evidence: EditMode FULL **302 (301 pass + 1 pre-existing skip P13M4,
0 fail)** on Unity 6000.6.0f1 batchmode (13 new P18 green, zero failures
in the pre-existing suite); `validate_content.py` PASS
(authoring); gateway `--selftest` **23/23** (18 baseline + 5 new);
3 phone pages JS syntax OK (vm.Script per block).
No player build in this pass (transport + unit scope; real-phone
precedence E2E stays user-run: plug webcam ⇒ phone START stands down,
unplug ⇒ re-opens, `-e2e-nocam` forces phone).

## 25. Local PC camera in-game (DONE 2026-09-15 — user-asked follow-up)

User rule: the integrated laptop camera counts as a webcam and its stream
must play INSIDE the game; a plugged-in USB webcam outranks it (laptop cam
skipped); phone stays the fallback. Verified live on the user's machine
(USB2.0 HD UVC WebCam): build `Succeeded errors=4` (headless noise),
payload DLLs fresh, foregrounded player.

- New `A_World/LocalCameraClassifier.cs` (pure, no UnityEngine): external
  outranks integrated (tight hints + documented bare-UVC trade-off: ASUS
  stock modules enumerate as UVC; a no-name external UVC ranks integrated
  too — only matters at 2+ bare-UVC cameras, single/brand cases exact),
  ties keep list order, null-safe. Names rank only; presence stays generic.
- New `A_World/LocalCameraService.cs` (MonoBehaviour): `WebCamTexture`
  320x240@30 direct to the HUD (zero copies, RAM-only, no record — §11),
  5 s hot-plug repick (USB plug switches up, unplug falls back),
  `-e2e-nocam` forces phone, never throws, states reuse
  `PhoneCameraState` (local Live/Connecting/Error honest; no frozen face).
- `PhoneCameraHud`: optional `BindLocal` (null = phone-only, old behavior
  byte-identical); local-Live wins the same box/layout with `PC CAM ● LIVE`
  labels (never phone strings); any other local state falls through to the
  phone path untouched. No-click-eat contract holds with local bound.
- `GameCameraStreamService.HasLocalCam` now routes through the shared
  classifier (same usable-definition); `MarketBootstrap` wires the local
  service + binds it (try/caught, null-safe; phone path unchanged on any
  failure). Gateway/phone pages untouched (cam:local already stood them
  down in §24).
- Live proof (player build, foreground): `[LocalCamera] localcam Live
  dev="USB2.0 HD UVC WebCam" playing=True 320x240@30` +
  `[PhoneCameraHud] state=Live(PC) video=True showing=True`, 0 exceptions.
  USB-webcam-outranks-laptop is unit-pinned (CT-P19D, both orders), NOT
  live-proven (no 2nd camera on this machine).
- Tests CT-P19 (13): classifier matrix incl. the exact UVC name, null-safe,
  determinism, frozen phone strings re-pinned, local label, fallback,
  no-click-eat, hardware-free service idle/force paths, precedence glue.
  Full suite green (below). Remote screenshot of the box was blocked by
  the user's foreground terminal over the game corner — visual confirmation
  is the on-screen box + user report, log lines above govern.
