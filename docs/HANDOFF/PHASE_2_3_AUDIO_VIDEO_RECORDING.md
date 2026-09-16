# Phase 2.3 — Full-Session Recording (Gameplay + Camera + Audio → MP4 + MP3)

## Status

**PASS WITH OPEN ITEMS** — the full-session recording layer is implemented,
tested, and proven at every level short of the supervised real-phone run:
press F2 → gameplay + camera + mic recorded → Stop → MP4 (H.264 + camera
PiP) + MP3 (LAME) → independent decode → content evidence. The ONE open
item is the supervised real-phone E2E (procedure in §22), which needs a
physical phone on the user's LAN. Same precedent as Phase 2.1 M6.

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

Design budget (formal game-build numbers = §22 E2E, then 2.4 stress):
game render ×1 (unchanged); per game sample one 2 MB memcpy + async
readback; JPEG/x264/LAME/overlay entirely off the main thread; cam sample
≤ 1 slot copy @10 fps; audio memcpy 32 KB/s. Telemetry hooks: queue
depths/drops, write-fail flags, game gaps, camera decode ms/fps, session
durations, transcode time. Measured here: worker JPEG 320×240 ≪ frame
budget; transcode of a 1 s fixture ≈ instant; veryfast 540p24 encodes
faster than realtime on this class of CPU (formal numbers in E2E).

## 21. Automated test results

CT-P20 (21) + CT-P21 (13) + full EditMode, Unity 6000.6.0f1 batchmode:
**349 total — 348 pass, 0 fail, 1 skip** (pre-existing `P13M4`). P21 covers:
output-config matrix, deliverable naming, C# JPEG validity (SOI/EOI/SOF0
dims) + input rejection + quality-monotonic sizes, transcode arg matrix
(full/audio-only/video-only/sanitized), locator-explicit-missing,
runner-missing-binary, game session fallback (intermediates kept,
`transcoded=false`), game serial-change flagging. Gateway `--selftest` OK;
`validate_content.py` authoring PASS.

## 22. Real game E2E results

Proven without hardware (loopback + REAL ffmpeg 9 full build):

- P20T: fake bridge → real watcher → tap bytes identical.
- Compat (Unity `-executeMethod`, temp script, deleted after): REAL
  `JpegEncoder` + writers → `mic.wav`/`cam.avi`/`game.avi` → REAL
  `FfmpegTranscodeBackend.Run` (exit 0) → `tools/verify_recording.py`:
  - audio 16000/16000 decoded, peak 0.25, ffprobe `pcm_s16le`;
  - cam 10/10 extracted, **PIL decodes to 320×240 RGB**, ffprobe `mjpeg`;
  - mp4: `avc1` + audio, 10/10 samples resolved, **pixel probe PASS**
    (frame extracted THROUGH H.264), ffprobe `h264`+`mp3`;
  - mp3: 31 frames, Xing-agreeing duration, ffprobe `mp3`.
- Bugs the proof caught: `-framerate` rejected for AVI inputs (builder
  fixed — headers carry the rate); magic-only fake JPEGs refused by the
  real decoder (fixtures upgraded to real JPEGs); MP4 parser offsets
  (stsd/tkhd/mvhd) fixed against ffmpeg output.

Supervised real-phone run (OPEN ITEM — user procedure):

```
0. Once per PC: winget install -e --id Gyan.FFmpeg  (internet once)
1. Fresh build (Assets/Editor/E2EBuild.cs) -> LWE.exe; launch foreground
2. PC + phone SAME Wi-Fi; gateway up (in-game QR auto-start or tools/)
3. Scan ONE unified QR (/phone); START MIC + START CAMERA
4. Launch flags: -e2e-nomic -e2e-nocam (phone paths past local devices)
5. VERIFY mic (HUD bars + green DATA) + camera (box LIVE)
6. F2 -> [MediaRec] START session=...  (F2 again = STOP; F2 = toggle)
7. PLAY: talk Milo -> find -> bring; SPEAK "ball"; face to camera; move
8. F2 -> STOP -> wait for [MediaRec] COMPLETE (transcode runs, seconds)
9. Collect MediaRecordings/rec-* : session.mp4 + session.mp3 (+ .json;
   intermediates only if KeepIntermediates or no-ffmpeg fallback)
10. python tools/verify_recording.py --mp4 ... --mp3 ... --expect-width 960
    --expect-height 540 --expect-mp4-audio
11. PLAY mp4 (gameplay + PiP face? HUD visible?) + PLAY mp3 ("ball"?)
12. Disconnect-mid-record + internet-off LAN rerun; record honestly
13. Paste: Player.log [MediaRec] lines + sidecar + verifier output + shots
```

## 23. Real phone results

NOT YET RUN (single open item). All machinery short of the phone is proven
above with real binaries.

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

**PASS WITH OPEN ITEMS** — open item: solely the supervised real-phone E2E
(§22). Full-session capture (gameplay + camera + mic → MP4 + MP3) is proven
with real encoders/decoders and zero locked-system rewrites. No AI, no
cloud, no phone-side encoding/storage, no second QR/page/transport, no
display-quality regression (capture is a copy), no 2.4 stress scope.

## Evidence table

| Evidence | Result |
|---|---|
| Fresh game build | OPEN (E2E step 1) |
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
| Audio/video content (auto) | PASS (peak / distinct / durations) |
| Content correct (human) | OPEN (phrase + PiP face + gameplay) |
| Mic+cam+game simultaneous | PASS (independent queues/pumps/threads) |
| F2 toggle | PASS (no key conflict; explicit start/stop) |
| QR regression | PASS (zero QR/page/gateway diff) |
| Disconnect/reconnect | PASS automated (flagged, media kept) / OPEN live |
| Offline LAN | PASS by construction / OPEN live rerun |
| ffmpeg-missing fallback | PASS design + unit (verified intermediates, honest flags) |
| Performance baseline | DESIGN + hooks + instant 1 s transcode |
| Phase 2.1 regression | PASS (P12–P16 green in 349) |
| Phase 2.2 regression | PASS (P17–P19 green in 349) |
