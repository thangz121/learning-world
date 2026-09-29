# JOURNEY DRIVERS — standing order (user, 2026-09-25)

**From this round on, the journey driver (runtime) and its build driver (editor)
are COMMITTED with the bundle** — no longer loose `.cs.txt` files. Both are
inert in production: each runtime driver boots ONLY with its OWN CLI flag, so a
normal player run never touches it and two drivers can never run at once
(found on maynode: both drivers listened to `-journey` and fought over clicks).

## Shipped drivers

| game | runtime driver (drop-in path) | flag | build driver (editor) | journey build |
|---|---|---|---|---|
| #3 rabbit "Cho thỏ ăn" | `Assets/A_World/CountingGarden/TempP55Journey.cs` | `-journey55` | `Assets/Editor/TempBuildP55.cs` | `TempBuildP55.BuildJourney` |
| #4 tower "Xây tháp theo số" | `Assets/A_World/BuildYard/TempP56Journey.cs` | `-journey56` | `Assets/Editor/TempBuildP56.cs` | `TempBuildP56.BuildJourney` → `E:/LWW/P56JBuild` |
| #5 delivery "Giao hàng đúng số" | `Assets/A_World/DeliveryVillage/TempP57Journey.cs` | `-journey57` | `Assets/Editor/TempBuildP57.cs` | `TempBuildP57.BuildJourney` |
| FULL #1..#5 (S3-P2Z16) | `Assets/A_World/FullJourney/TempFullJourney.cs` | `-journeyfull` | reuse `TempBuildP56.BuildJourney` | `E:/LWW/P56JBuild` |

The full-journey driver walks ONE real-click pass over every created part
(Main → Math → garden #1/#2/#3/#6/#7 with their garden mini lessons → Math →
#4 → #5 → #6 → Main) and logs a `CENSUS`
(scene count, area/game instances, player position) at every stage so invisible
bugs (duplicates, leaks, stale instances) surface in the log even when the
screen looks fine. All flags are mutually exclusive; a plain boot runs none.

### Stage engine + severe-error skip (S3-P2Z17, user order)

The full driver is a table of named stages
(`garden.balls|stairs|rabbit|strawberry|corn`, `hub.tower|delivery|match`,
`return.main`). A stage that cannot proceed (timeout, stall, missing
game/zone) is marked **SEVERE**:

1. Evidence: 6 shots at the failure point — `ERR_<stage>_0` (plan view), four
   real right-mouse-drag orbit angles (`ERR_<stage>_1..4`, SmartCamera S4) and
   one wheel zoom-out (`ERR_<stage>_zoom`).
2. Marker: `<shot-dir>/severe_errors.txt` (time, stage, player position,
   reason, run) + `[FULLJ] SEVERE <stage> :: <reason>` in the log.
3. Skip: if the stage's arena door still lets the child out, leave it and
   continue the NEXT stage in the SAME run; if not, the driver relaunches
   itself with `-journey-from <next stage>` (fresh process, new run dir,
   capped at 4 runs) so the failed stage is never re-run. `journey_summary.txt`
   in the shot dir lists every stage as PASS/SEVERE.

## Run (maynode)

1. Build the journey player: batchmethod `TempBuildP5x.BuildJourney`
   (driver edits only recompile scripts; the player EXE stays valid).
2. Run windowed foreground:
   `LWE.exe -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile <log> -journeyfull -lang en -shot-dir E:/LWW/fullj-shots`
   (per-game flags: `-journey55` rabbit, `-journey56` tower, `-journey57`
   delivery; resume after a severe stage: `-journey-from <stage>` — implies the
   full driver; `-lang en` skips the language-card dependency; `-shot-dir`
   splits evidence per run).
3. Expect `[FULLJ]` trace ending in `FULLJOURNEY_END ... errors=0` (or
   `severe=N` with the skipped stages listed in `journey_summary.txt`); shots go
   to the `-shot-dir` folder.
4. The production deliverable (`P5xBuild`, no flag) stays behaviourally
   identical: the driver only activates on the flag. Boot smoke must show no
   `[P55J]`/`[P56J]` line.
5. The runtime driver hides the dev system dialogs (Mic offer / phone-camera
   HUD) before every shot — tooling-only; gameplay code is never touched.

## Driver design rules (keep when adding #5+)

- Real input only: `InputSystem.QueueStateEvent` mouse press held 4 frames (a
  single-frame press is seen by UI but not by `ClickRouter`).
- Walk by REAL conditions (world loaded / area inside / scene unloaded / game
  phase), never by arrival radii across a travel — after a world change, the
  old target is meaningless (found the hard way in game #4's first run).
- Never read/write gameplay state to steer: only clicks + polling.
- Screenshots into a per-game folder next to the build; log a `[PxxJ]` trace so
  the report can quote it.
