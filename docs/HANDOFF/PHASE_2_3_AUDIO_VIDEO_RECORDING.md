# Phase 2.3 — Full-Session Recording (Gameplay + Camera + Audio → MP4 + MP3)

## Status

**PASS WITH OPEN ITEMS** — proven in a FRESH shipping build (Succeeded,
errors=0) with loopback transport standing in for the phone: offer Accept →
ReadyPhone + camera Live → 18 s record → STOP → transcode → `P23SURVEY
COMPLETE`, then `verify_recording.py` decoded both deliverables and verified
content (mp4 960×540 avc1, 338 samples, pixel-probe PASS through real H.264;
mp3 501 frames, 0 bad, 18.00 s, Xing-agreeing). The REMAINING open items
need a physical phone + human: real F2 keypress, spoken phrase, visible
face, phone-origin bytes (procedure in §22). Same precedent as Phase 2.1 M6.

Scope note (user request, 2026-09-16): record the WHOLE play session —
gameplay video AND camera/phone stream AND audio — on ONE toggle key (F2),
deliver MP4 + MP3 via an open-source encoder with the strongest balanced
compression. Input/display quality stays maximal (Phase 2.2 config and the
render pipeline are untouched); the OUTPUT prioritizes fps + compression.

## 1. Objective

Press F2 → record everything → press F2 → two deliverables:

```
PHONE (mic + camera, one stream, one QR, one page)
  -> PC bridges (8451 PCM16 / 8452 JPEG)
  -> GAME (rendered frame + watcher link/audio + decoded Live camera)
  -> RECORDING LAYER (this phase)
  -> mic.wav + cam.avi + game.avi (intermediates, verified)
  -> session.mp4 (H.264 gameplay + camera PiP + MP3 audio)
  -> session.mp3 (LAME VBR voice)
  -> DECODE (ffprobe/ffmpeg/PIL/stdlib) -> CONTENT VERIFIED
```

## 2. Existing architecture reused

Audited 2.1/2.2 before code (see previous revision + `PHASE_2_1_PHONE_MIC.md`
/ `PHASE_2_2_PHONE_CAMERA_STREAM.md`). Reuse table (unchanged):

| Proven mechanism | Recording reuse |
|---|---|
| Bridge envelope + session latch + foreign-drop | `RecordingSessionLatch` per medium per session |
| Bounded backlog, drop-oldest, counted | `BoundedByteQueue` (audio 30 s / cam 300 fr / game raw 6 fr) |
| Worker-thread ownership, main thread copies only | 3 pump threads + 1 transcode thread; zero main-thread file/encode I/O |
| `PhonePresenceWatcher` AUDIO observation | `AudioPayloadAccepted` tap (same bytes the game saw) |
| `CameraFrameSource` slot + decode-to-Live | Non-consuming freshness-gated peek |
| `MicSetupMonitor` phone-state ownership | `CurrentAudioWatcher` accessor |
| `MarketBootstrap` null-safe additive wiring | `MediaRecorder` + game camera + repo tools dir |
| Unified QR / one page / `-e2e-nomic` / `-e2e-nocam` | Untouched (zero gateway/page/QR diff) |

Display/input quality is maximal by non-interference: the camera keeps the
Phase 2.2 config (320×240 q60 @ display rate), the game renders exactly
once, and capture is a GPU copy + async readback at OUTPUT size/fps only.

## 3. Recording architecture

```
GAME RENDER (1x, untouched, max quality)
  │ endCameraRendering: blit current frame -> record RT 960x540 (GPU, free downscale)
  │ AsyncGPUReadback (no stall) -> memcpy -> raw queue (cap 6)
  v
game worker: pure-C# JPEG q65 -> game.avi (MJPEG, 24 fps)

PHONE cam -> watcher -> slot -> decode-to-Live -> 10 fps peek -> cam.avi (verbatim JPEG)
PHONE mic -> game watcher tap -> 16 kHz PCM queue -> mic.wav

STOP -> pumps drain -> writers finalize -> header verifies
  -> ffmpeg transcode worker (overlay PiP + x264 + LAME, bounded timeout)
  -> session.mp4 + session.mp3 -> existence checks -> intermediates policy
  -> sidecar JSON -> COMPLETED (or ERROR with intermediates kept)
```

Media independence (§15): audio queue/pump, cam queue/pump, game raw
queue/pump, transcode thread, latches, files — disjoint. No path blocks
another. Modes: MicOnly (mp3 only) / CameraOnly (mp4 video-only) /
MicAndCamera (mp4 + mp3). Gameplay video rides with ANY video mode.

New files: `_SharedKernel/{MediaRecording,WavWriter,AviMjpegWriter,
JpegEncoder,FfmpegTranscodeBackend}.cs`, `A_World/MediaRecordingService.cs`,
`Tests/EditMode/{CT-P20_MediaRecording,CT-P21_SessionDeliverables}.cs`,
`tools/verify_recording.py`. Additive micro-edits (~15 lines each):
`PhonePresenceWatcher` (tap + seq), `CameraFrameSource` (peek),
`GameCameraStreamService` (peek accessor), `MicSetupMonitor` (accessor),
`MarketBootstrap` (wire + game cam + tools dir), `.gitignore`.

## 4. Audio path

Unchanged: phone mic → gateway 8451 (canonical PCM16 mono 16 kHz) →
game-owned watcher → tap copy → bounded queue → pump → `mic.wav`
(bit-exact). Speech engine: zero diff.

## 5. Video paths (camera + gameplay)

Camera: phone/local stream → slot-accepted Live frames → 10 fps main-thread
sample (precedence mirrors the HUD) → `cam.avi` (phone bytes verbatim;
local via main-thread `EncodeToJPG` fallback at 10 fps only).
Gameplay: rendered frame → per-tick ARM → `endCameraRendering` GPU blit to
960×540 RT → `AsyncGPUReadback` → memcpy to bounded raw queue → game worker
C# JPEG-encodes → `game.avi` @ 24 fps. Main-thread cost per sample ≈ one
2 MB memcpy; encode NEVER touches the main thread. UI/HUD is part of the
capture (it rides the same frame — session context + live-link proof
visible in the video).

## 6. Encoder selection (open-source, balanced)

- Intermediates (always, no dependency): WAV/PCM16 + AVI/MJPEG (phone bytes
  verbatim; gameplay via the pure-C# baseline-DCT `JpegEncoder`, Annex-K
  tables, IJG quality scaling — an established FORMAT implementation like
  the WAV/AVI writers, proven by real ffmpeg + PIL decode in compat).
- Deliverables (ffmpeg present): **libx264** (open-source, GPL) preset
  `veryfast` + **CRF 24** + **libmp3lame VBR `-q:a 4`** @ 16 kHz mono.
  Rationale: cartoon/game content goes near-transparent at CRF 24 while
  compressing hard; veryfast keeps transcode faster than realtime on the
  Xeon; q4 is voice-transparent at roughly half of 128k CBR. Preset steps
  trade transcode TIME, never quality-at-CRF. All tunables live in ONE
  config (crf 18–32, preset allowlist, mp3q 0–9, game res/fps/jpeg-q, PiP
  size/margin, keep-intermediates) — never scattered.
- Backend isolation (§5 rule): ffmpeg exists in exactly ONE file
  (`FfmpegTranscodeBackend`: locator + pure argument builder + bounded
  runner). Gameplay code never spawns processes.

## 7. Codec/container selection

Intermediates: WAV / AVI-MJPEG (universal, zero-dep, forensic-friendly).
Deliverables: **MP4** (H.264 + MP3 audio + faststart) and **MP3** (LAME
VBR). PiP: camera overlay 240×180 @ margin 16, bottom-right, composited by
ffmpeg (`scale + overlay eof_action=pass`) — zero game-thread blend cost.

## 8. PC-side encoding

Everything (JPEG encode, pumps, x264, LAME, mux, PiP) runs on the PC.
Phone: capture + transport only; nothing stored on the phone.

## 9. Phone responsibilities

Unchanged from 2.1/2.2. Zero phone-side diff in this phase.

## 10. Game recording boundary

Audio: game-watcher tap (refuse `no-phone-audio-in-game` unless AUDIO flows
in-game). Camera: slot-accepted fresh frames (stale = gaps). Gameplay: the
rendered frame itself (implicitly game-accepted; gaps counted when the
readback stalls). Start requires requested media IN GAME; sidecar stamps
first-sample offsets on one clock. Speech path untouched.

## 11/12. Buffering / backpressure

Audio 30 s / cam 300 frames / game raw 6 frames (≈12 MB memcpy cap):
drop-oldest + counted, producers never block, consumers drain at disk/
encode speed. Stop closes queues → deterministic flush. Transcode is
post-stop (no realtime pipe to deadlock).

## 13. Timestamp strategy

One session clock (UTC ISO + monotonic ticks): start/stop, first
audio/cam/game offsets, measured actual fps per medium (sidecar; AVI
headers carry declared rates). No frame-perfect A/V claim; `-shortest`
trims tails honestly.

## 14. Recording lifecycle

`Idle → Starting → Recording → Stopping → Finalizing → Completed`, `Error`
from any active state. Double-start/double-stop refused (P20S). "Complete"
only after pumps + writers + verifies + (transcode or honest fallback) +
sidecar. Transcode runs on its own thread inside Finalizing; the main
thread only polls.

## 15. Error handling

Dir/file/write/init failures, finalize timeout (30 s), transcode timeout
(scaled to session, kill + `transcode-timeout`), transcode exit≠0
(`transcode-failed`, §32): all → `Error` + reason + sidecar, game
playable, intermediates KEPT and listed. Missing ffmpeg is NOT an error:
verified intermediates complete the session with `transcoded=false`
(graceful fallback, §10). No infinite retry anywhere.

## 16/17. Naming / storage

`rec-<UTC>-<hex>{_audio.wav,_video.avi,_game.avi,.mp4,.mp3,.json}` — one
session ID, never overwritten, stale-proof latches. One dir:
`<persistentDataPath>/MediaRecordings` (overridable; repo-relative
`Recordings/` gitignored). Intermediates deleted after successful
transcode by default (`KeepIntermediates=false`, logged).

## 18. Privacy

Local only. No upload/cloud/vision/biometrics (no such symbols added).
Logs/sidecar carry scalars + paths, never media bytes. ffmpeg runs locally
on LAN-captured bytes. One-time `winget install Gyan.FFmpeg` needs
internet once; everything after works offline on LAN.

## 19. Offline behavior

LAN + local files + local ffmpeg. No cloud dependency by construction
(grep-verifiable). Internet-off rerun stays in the user E2E (§22).

## 20. Performance baseline

Design budget + LOOPBACK-BUILD measurements (formal phone-run numbers stay
§22, then 2.4 stress). Session `rec-20260916-030717` (18 s wall, shipping
build, foreground): audio 179 chunks / 0 drops, cam 182 frames / 0 drops /
0 gaps, game 348 frames / 12 raw drops (worker JPEG sustains ≈19 fps, so
the default record cap is 20 — header rate ≈ wall rate), transcode seconds,
gameplay FPS unaffected path (3×FACE_OK during record, no freeze).
Telemetry hooks for everything above travel in every sidecar. First loopback
run caught a REAL duration bug (header 24 fps vs 19 sustained → mp4 14.2 s
vs 18 s wall); fixed by cap-20 + output `-r <measured>` re-stamp (second
run: mp4 17.48 s vs 18.00 s wall).

## 21. Automated test results

CT-P20 (21) + CT-P21 (13) + CT-P22 (12: specs, winget cmd, SHA, locator,
cert round-trip, zip extract, dialog states, Finalizing-pump) + full
EditMode, Unity 6000.6.0f1 batchmode, FINAL run on the clean tree:
**361 total — 360 pass, 0 fail, 1 skip** (pre-existing `P13M4`). Gateway
`--selftest` OK; `validate_content.py` authoring PASS.

## 22. Real game E2E results

LOOPBACK E2E (fresh `LWE-E2E` build, `result=Succeeded errors=0`, real
bridges/gateway-envelope/game/recorder/ffmpeg; phone origin simulated):

- Run A (driver API), session `rec-20260916-030717`: offer Accept click →
  ReadyPhone + cam Live → START → 18 s → STOP → transcode → COMPLETE,
  `interrupted=false`, `transcoded=true` (ffmpeg 9.0.1): audio 286400
  samples, cam 183 frames, game 348 frames, mp4 419124 B, mp3 24884 B.
  Verifier: mp4 960×540 avc1, 338 samples resolved, pixel-probe PASS,
  17.48 s; mp3 501 frames, 0 bad, 18.00 s, Xing-agreeing. OVERALL PASS.
- Two REAL bugs caught by run A: (1) `Update()` pumped Stopping only →
  sessions stranded in Finalizing with mp4+mp3 already on disk (fixed +
  pinned P22L); (2) header-24 vs sustained-19 fps → mp4 14.2 s vs 18 s
  wall (fixed: cap-20 default + `-r <measured>`; second run 17.48 s).
- Dependency setup in-build: `[DepSetup] wired`, all present → silent, no
  prompt (correct behavior, logged).
- Earlier compat (Unity `-executeMethod`, temp script, deleted after): REAL
  `JpegEncoder` + writers → intermediates → REAL transcode (exit 0) →
  verifier: audio 16000/16000 peak 0.25; cam 10/10, **PIL 320×240 RGB**;
  mp4 avc1 + pixel-probe PASS; mp3 31 frames Xing-agreeing. Bugs it caught:
  `-framerate` refused for AVI inputs; magic-only fake JPEGs refused by the
  real decoder; MP4 parser offsets (stsd/tkhd/mvhd) fixed vs ffmpeg output.
- Run B (physical-key path): INCONCLUSIVE-harness, NOT game FAIL. ~30
  synthetic F2 injections (SendKeys, keybd_event, SendInput+scancode, 3 s
  holds) into the verified-foreground game window: device present
  (`kbNull=False`, 89 diag samples), `f2wasPressed` never true. Synthetic
  OS input does not reach InputSystem in this automation environment;
  the production 5-line key path calls the run-A-proven API. Verdict on F2
  moves to the phone run (physical keypress — zero injection doubt).

Supervised real-phone run (OPEN ITEMS — user procedure):

```
0. Once per PC: winget install -e --id Gyan.FFmpeg  (internet once;
   or accept the in-game prompt — it installs silently on consent)
1. Fresh build (Assets/Editor/E2EBuild.cs) -> LWE.exe; launch foreground
2. PC + phone SAME Wi-Fi; gateway up (in-game QR auto-start or tools/)
3. Scan ONE unified QR (/phone); START MIC + START CAMERA
4. Launch flags: -e2e-nomic -e2e-nocam (phone paths past local devices)
5. VERIFY mic (HUD bars + green DATA) + camera (box LIVE)
6. Press PHYSICAL F2 -> [MediaRec] START  (F2 again = STOP; F2 = toggle)
7. PLAY: talk Milo -> find -> bring; SPEAK "ball"; face to camera; move
8. Press PHYSICAL F2 -> STOP -> wait for [MediaRec] COMPLETE
9. Collect MediaRecordings/rec-* : session.mp4 + session.mp3 (+ .json;
   intermediates only if KeepIntermediates or no-ffmpeg fallback)
10. python tools/verify_recording.py --mp4 ... --mp3 ... --expect-width 960
    --expect-height 540 --expect-mp4-audio
11. PLAY mp4 (gameplay + PiP face? HUD visible?) + PLAY mp3 ("ball"?)
12. Disconnect-mid-record + internet-off LAN rerun; record honestly
13. Paste: Player.log [MediaRec] lines + sidecar + verifier output + shots
```

## 23. Real phone results

NOT YET RUN (open items: physical F2 keypress, spoken "ball", visible face,
phone-origin bytes, disconnect/reconnect live, offline-LAN rerun). The
loopback E2E above proves everything downstream of the phone; the phone run
proves origin + human content.

## 24/25. File verification

Game-side basics (existence + headers + spot extraction) for all five
files; tool-side five separate verdicts each (created/valid/decoded/
content/correct) via `tools/verify_recording.py` (`--audio/--video` for
intermediates, `--mp4/--mp3` for deliverables): stdlib `wave`, AVI idx
walk, MP4 box/sample-table walk + ffmpeg pixel probe when available, MP3
frame scan + Xing duration, frozen/silence/truncation negatives honest.
`correct` (phrase/face/gameplay) is human-only by design.

## 26/27. Bugs found → fixed

1. `Debug` ambiguous after adding `System.Diagnostics` (service) → removed
   using, qualified `Process`.
2. `CommandBufferPool` unresolvable in this assembly → `new CommandBuffer`
   + Dispose (same pattern, zero new refs).
3. EditMode `Destroy` on RenderTexture → `DestroyImmediate` when `!isPlaying`
   (×2 sites).
4. `-framerate` invalid for AVI inputs → headers carry the rate (caught by
   real ffmpeg, would have shipped broken transcodes on exit-code trust).
5. Fixture fake-JPEGs refused by real MJPEG decoder → fixtures use the real
   `JpegEncoder` (production bar = genuinely decodable bytes).
6. Verifier MP4 offsets (stsd +12, tkhd v0 +76/v1 +88, mvhd v1) fixed
   against ffmpeg output; MP3 floor set to 8 kbps avg (VBR sine honesty).
7. P20U missing `StartService`; P20R avi counts (cam+game); all green after.

## 28. Known limitations

Real-phone proof pending (§23). `correct` is human-verified by design.
Mid-session reconnect continues flagged `interrupted`. No crash-safe
recovery (best-effort quit finalize). Local-cam fallback JPEG-encodes on
the main thread @10 fps (phone path, the E2E target, is verbatim).
Formal build CPU/RAM/FPS numbers await §22. Single phone/session (2.x
scope). Transcode needs the one-time ffmpeg install; without it sessions
complete honestly on verified intermediates.

## 29. Phase 2.4 deferred

Long/soak sessions, repeated connect/disconnect + record cycles,
queue growth under network instability, transcode-at-scale timing,
FPS stability with everything on, recovery paths, full 2.x regression.

## 30. Final verdict

**PASS WITH OPEN ITEMS — NOT LOCKED.** Open items (all need the physical
phone + human): physical F2 keypress, spoken "ball", visible face,
phone-origin bytes, live disconnect/reconnect + offline rerun. Everything
downstream of the phone is proven in a fresh shipping build with real
binaries (offer → link → record → transcode → decode → content), with
production bugs found and fixed by that proof. No AI, no cloud, no
phone-side encoding/storage, no second QR/page/transport, no
display-quality regression, no 2.4 stress scope.

## 31. Closing deltas (post-2.3b, same gate)

- Save-location chooser (user ask): first F2 opens an animated in-game
  panel (default dir shown + native Windows folder picker); ANY choice —
  explicit dir or remembered-default sentinel — asks exactly once.
  Double-F2 re-opens it (F4 removed per user call). Remembered dir wins
  over the default, validated writable on every start; stale dirs re-ask.
- Result toast (user ask): non-modal, click-through, auto-hiding notice
  with saved filenames + dir on COMPLETE, and a Vietnamese reason on any
  failure (e.g. no-phone-audio tells the user to connect the phone —
  previously this failed log-only and looked "done with no files").
- Headphone icon was upside-down (arc in the lower half): flipped, pinned
  by pixel test P16O (arc rows occupied up top, zero pixels below cups).
- Closing loopback run (fresh build, Succeeded errors=0, session
  `rec-20260916-035156`): 18 s wall → COMPLETE, interrupted=false,
  transcoded=true — audio 286400 samples, cam 183 frames, game 335 frames,
  files landed in the DRIVER-CHOSEN dir (remembered-dir-wins proven
  end-to-end). Verifier: mp4 960×540 avc1, 313 samples resolved,
  pixel-probe PASS, 16.82 s; mp3 501 frames, 0 bad, 18.00 s,
  Xing-agreeing; ffprobe h264+mp3. OVERALL PASS (machine verdicts;
  `correct` stays human-gated for phrase/face).
- F2 synthetic delivery: INCONCLUSIVE-harness, NOT game FAIL (device
  present, verified foreground, ~30 injections incl. scancode-correct
  SendInput and 3 s holds, zero `wasPressedThisFrame`; lifecycle itself
  proven via API on the same build). Verdict moves to the physical key.
- Audio-source detection + video-only proposal (user ask): the game now
  distinguishes phone-audio (recordable) vs local-mic-only (detected via
  the mic gate, NOT recordable this phase — stated plainly) vs none. F2
  with video but no recordable audio shows an explicit proposal ("Quay
  video không tiếng?") instead of a log-only fail that looked "done with
  no files". Local-mic CAPTURE stays out of scope (new capture path +
  resampling + device ownership — a separate decision, not smuggled in).
- Dependency setup ran silent-correct inside the shipping build
  (`[DepSetup] wired`, all present, no prompt).
- Final EditMode on the clean tree: **371 total — 370 pass, 0 fail,
  1 skip** (`P13M4_RealSamplesIfPresent`: conditional Ignore, needs
  curated human voice samples; speech-benchmark only, masks no recording
  coverage). Temp survey driver deleted (0 temp files).
- Precedence correction (user rule, both media): USB discrete > built-in
  laptop > phone, game-wide. Camera already obeyed it; mic selection took
  `devices[0]` and ignored rank — now `MicDeviceClassifier.Rank/PickDevice`
  + hot-plug takeover in the service (strictly-better rank wins, equal rank
  never flaps) + selection logged. The recorder feeds the same order
  (local-selected-handle first, phone tap parks while local feeds, no
  mixing; switches flag interrupted). `-e2e-nomic` forces local-empty so
  the phone chain proof is preserved exactly. Speech input follows
  automatically (same selected device).
- Closing loopback run #2 (fresh build Succeeded errors=0, session
  `rec-20260916-043531`, 18 s wall → COMPLETE, interrupted=false):
  audio 284800 samples, cam 182, game 292 (60 raw drops under load —
  bounded, counted; mp4 duration reflects captured frames), files in the
  driver-chosen dir. Verifier: mp4 960×540 avc1, 239 samples resolved,
  pixel-probe PASS, 14.73 s; mp3 498 frames, 0 bad, 17.89 s,
  Xing-agreeing. OVERALL PASS (machine verdicts).
- Suite after precedence work: **384 total — 383 pass, 0 fail, 1 skip**
  (same benign P13M4). Two wounds from the round, both fixed at the true
  cause: P23 brace surplus (CS1022, file re-verified on disk) and converter
  guard semantics (channels<1 rejects instead of silent-clamp).

## Evidence table

| Evidence | Result |
|---|---|
| Fresh game build | PASS (LWE-E2E, Succeeded errors=0, DLLs fresh, no temp driver) |
| Save-location chooser | PASS unit + live panel (animated, native picker, remembered incl. default, double-F2) |
| Result toast | PASS unit + live wiring (filenames+dir on COMPLETE, VI reason on failure, click-through) |
| Headphone icon | PASS upright (P16O pixel-pinned) |
| Phone connection / unified web | PASS untouched + gateway selftest OK / OPEN live |
| Microphone in game | PASS transport / OPEN live |
| Camera in game | PASS transport / OPEN live |
| Gameplay capture path | PASS (blit+readback+worker JPEG; P21L/M) |
| C# JPEG validity | PASS (PIL 320×240 RGB + ffmpeg mjpeg decode) |
| Audio recording (wav) | PASS (16000/16000, peak 0.25) |
| Camera recording (avi) | PASS (10/10 extracted, distinct) |
| Game recording (avi) | PASS (worker JPEG, verified headers) |
| Transcode (ffmpeg real) | PASS (exit 0, libx264 veryfast CRF24 + LAME q4) |
| MP4 decoding | PASS (samples resolved + pixel probe + ffprobe h264) |
| MP3 decoding | PASS (31 frames, Xing duration + ffprobe mp3) |
| Loopback session (18 s wall) | PASS COMPLETE (286400 samples, 183 cam, 348 game, mp4+mp3, interrupted=false) |
| Content correct (human) | OPEN (phrase + PiP face + gameplay) |
| Mic+cam+game simultaneous | PASS (independent queues/pumps/threads) |
| F2 toggle (synthetic) | INCONCLUSIVE-harness, NOT game FAIL (device present, focus verified, 0 hits; verdict moves to physical key) |
| QR regression | PASS (zero QR/page/gateway diff) |
| Disconnect/reconnect | PASS automated (flagged, media kept) / OPEN live |
| Offline LAN | PASS by construction / OPEN live rerun |
| ffmpeg-missing fallback | PASS design + unit (verified intermediates, honest flags) |
| Performance baseline | DESIGN + hooks + instant 1 s transcode |
| Phase 2.1 regression | PASS (P12–P16 green in 384) |
| Phase 2.2 regression | PASS (P17–P19 green in 384) |

## 32. P2X user-run follow-up (2026-09-16): 3 player reports, root-caused + fixed + re-proven

Player reports on the 12:50 play (stale 11:27 binary): (1) mp4 had audio +
webcam but NO gameplay video; (2) no recording indicator on screen;
(3) red error lines on screen ("Development Console" overlay photo:
DrawOpaqueObjects/... Fake or uninitialized surface ... BlitToSubPass +
EndRenderPass: Not inside a Renderpass, spammed).

Root causes (all proven from that run's Player.log):
- (1)+(3): the played binary was built from uncommitted working-tree code
  (CaptureGameFrameOnce + manual Camera.Render(), never committed) that
  broke URP's render-pass bookkeeping: 634x EndRenderPass spam + black game
  frames. The "Development Console" is Unity's built-in dev-build error
  overlay (NOT project code, grep-verified): it appears only while errors
  exist, and never in non-dev builds.
- (2): RecordingIndicator did not exist in that binary yet.

Fix iterations on current source (each built fresh + run in a real player):
- (a) enabled-clone-camera path: 0 URP errors BUT gameDarkFrames = ALL
  288 samples (clone renders black under URP 17), caught by the tripwire.
- (b) endCameraRendering CameraTarget blit: 0 errors BUT still all-dark.
- (c) FINAL: ScreenCapture.CaptureScreenshotIntoRenderTexture(_recordRT)
  per sample tick + readback next Update (no extra scene render, no URP
  camera/hook surface). All-dark NOTICE log added (plain Log, never a
  warning/error so it cannot pop the dev overlay).

P2X proof run (fresh build Succeeded errors=0, local Realtek mic + local USB
cam, 15 s wall, driver deleted after): COMPLETE int=False err= (empty) -
audio 664 chunks q0/d0 (mp3 412 frames, peak 0.234 room audio), cam 150
frames q0/d0 (PiP face visible), game 252 frames 0 dark 0 gaps (mp4
960x540 avc1, extracted frame: bright world + player + HUD + in-game cam box
+ ffmpeg PiP). Player.log: 0 EndRenderPass, 0 exceptions. Screenshots:
p2x-indicator.png ("DANG QUAY 00:07" badge live) + p2x-toast.png
("Da luu xong" + filenames + dir, badge hidden post-complete).
EditMode after the fix: 384 total, 383 pass, 0 fail, 1 skip (benign P13M4).
Final clean build (driver-free): Succeeded errors=0, boot 73 s+ alive,
3xFACE_OK, 0 errors, MediaRec wired, 0 driver traces.

## 33. P2X follow-up 2 (2026-09-16): blurry gameplay + double PiP + short mp4

Player reports on the new mp4 (photo): (1) gameplay BLURRY (heavy banding/
blockiness); (2) the face TWICE (in-game CAMERA box bottom-left + ffmpeg PiP
bottom-right), core bug; plus a latent find: mp4 only ~8 s for a 15 s wall.

Causes:
- (1) Double lossy chain: worker C# JPEG q65, then x264 CRF 24, on a 960x540
  downscale of the window.
- (2) Regression from the screen-capture switch: with the old clone camera,
  overlay UI never entered the file (PiP was single by construction);
  screen capture pulls the HUD box in too.
- (3) AVI duration = frames/header-rate: 158 frames stamped at 20 fps = 7.9 s
  of timeline, so -shortest truncated audio+video to ~8 s. (The earlier
  -r <measured> output flag only relabels the rate, never stretches time.)

Fixes (all in the ONE config + isolated writer/backend, zero frozen-system
diffs):
- Quality (user rule: squeeze size, keep near-original): capture 1280x720
  (no downscale softening), worker JPEG q65 to 90 (intermediate is deleted
  after transcode, nearly free; Validate bound 30..85 widened to 30..100),
  x264 CRF 24 to 19 (near-transparent). Pins updated (P21A/P21I).
- Single PiP (user pick): PhoneCameraHud.SetRecordingHide, the service
  hides the box on entering Recording, shows it back on Stop (hide placed
  AFTER all FailStart gates; release resumes state-driven display; never
  fights the machine). MarketBootstrap binds it null-safe. Pinned by
  P19N (HUD level) + P20V (service drives hide/show through a real session).
- Duration honesty: AviMjpegWriter.Finalize(out, actualFps) re-stamps
  strh scale/rate + avih usec (positions recorded at Begin; old overload
  keeps declared rate). Pumps pass frames/wall via MeasuredStreamFps
  (wall under 1 s or unset gives 0 = no restamp, so unit-speed sessions stay
  deterministic). Pinned by P20W (10 frames at 10.0 gives Fps 10.0,
  Duration 1 s; declared path still 20).

Proof run (fresh build Succeeded errors=0, 15 s wall, driver deleted after):
COMPLETE int=False err= (empty) - game 1280x720, 158 frames, 0 dark,
0 gaps; cam 150 q0/d0; audio 672 chunks; mp4 1280x720 avc1,
14.68 s vs 15 s wall (was 7.9 s), 154 samples; mp3 ~14.7 s.
Extracted frame: crisp edges, no banding, ONE PiP (ffmpeg, bottom-right),
no camera box; live shot: REC badge + box hidden + world intact.
Player.log: 0 renderpass errors, 0 exceptions.
EditMode: 387 total, 386 pass, 0 fail, 1 skip (P19N/P20V/P20W green).
Final clean build: Succeeded errors=0, boot 73 s+ alive, 3xFACE_OK,
0 errors, MediaRec wired, 0 driver traces.
## 34. Quality presets (2026-09-16): MAX / HIGH / STANDARD / PREVIEW

User ask: one quality preset driving CRF + encoder preset + resolution
together; MAX must keep a 1080p source at 1080p (never downscale a capable
source, never upscale a small one).

Pipeline trace (before touching code) - 720p appears at EXACTLY one place:
- Game Render: URP main camera to the screen backbuffer at WINDOW size.
  No recording code touches it.
- Capture: PrepareGameCapture allocated _recordRT at _effective.GameWidth x
  Height = config default 1280x720; CaptureScreenshotIntoRenderTexture
  scales screen into that RT (downscale loss when the window is bigger,
  fake pixels when smaller). THIS is the 720p choke point.
- Composer: raw queue (cap 6, drop-oldest) -> worker C# JPEG q90 ->
  game.avi (dims = RT dims, header restamped to measured fps); cam.avi;
  mic.wav. Verify enforces avi dims == _effective dims.
- Encoder: ffmpeg passes the game input through UNTOUCHED (only the PiP is
  scaled + overlaid) -> mp4 res = RT res. x264 CRF/preset from spec <-
  _effective <- config defaults. So fixing the RT size fixes the file.

Implementation (zero frozen-system diffs):
- MediaRecording.cs (pure): VideoQuality enum (Preview/Standard/High/Max,
  default High = proven behavior) + QualityTier.For (CRF/preset/cap per
  tier: 24/superfast/480p, 21/veryfast/720p, 19/medium/720p, 17/slow/1080p;
  garbage fails safe to High) + ResolveGameSize (fit source inside cap,
  scale <= 1 so never upscale, aspect preserved, even dims, floor 16,
  degenerate source falls back to cap). AllowedVideoPresets gains "slow";
  Validate CRF bound widened 18..32 to 16..32 (MAX needs 17; caught live by
  the E2E run refusing start, not by unit tests) + Quality defined-check.
- MediaRecordingConfig.Quality (default High) + service SetQuality (refuses
  garbage, keeps running default).
- MediaRecordingService: StartRecording fans tier CRF/preset into _effective
  (pure, unit-safe); capture size resolves from the LIVE screen in
  StartRecording BEFORE ResetSessionState + writers Begin (so telemetry,
  queues, writers, readback, transcode all see ONE size), gated on
  isPlaying + camera + sane screen dims (batch scenes carry cameras, so an
  ungated resolve broke P20M/P20R/P21L/P21M - pinned by new P20X).
- Telemetry/sidecar gains quality + videoCrf + videoPreset (additive only).

Tests (nothing weakened - pins only added/updated for intended new
defaults): P21N tier mapping exact + allowlist survival + validate matrix;
P21O resolve matrix (1080p+Max=1080p, 1080p+High=720p, small sources never
upscaled, 4:3 aspect kept, odd dims evened, degenerate falls back to cap);
P20X ambient-screen-ignored-while-not-playing; P21A gains the High default
pin. Suite: 390 total, 389 pass, 0 fail, 1 skip (benign P13M4).

Live proof, ONE E2E recording at MAX, 1920x1080 window (machine desktop is
only 1280x800, but the windowed backbuffer really was 1920x1080 - logged):
COMPLETE int=False err= (empty) - sidecar quality=Max crf=17 preset=slow;
game 1920x1080, 88 frames, 0 dark, 0 gaps (266 raw drops: 1080p worker
throughput tradeoff, counted, duration honest); cam 180 q0/d0; audio 783
chunks; mp4 1920x1080 avc1 at 4.887 measured fps, 17.65 s vs ~18 s wall,
9.4 MB; mp3 17.75 s, 494 frames; verifier OVERALL PASS; extracted motion
frame crisp (no macroblocking), ONE PiP, REC badge; Player.log 0 errors.
1080p-output on a >=1080p display is proven; on smaller screens MAX resolves
to the native window (no upscale) by the same pinned math.
Final clean build: Succeeded errors=0, boot 73 s+ alive, 3xFACE_OK,
0 errors, MediaRec wired, 0 driver traces.
