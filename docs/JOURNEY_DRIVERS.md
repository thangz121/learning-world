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
| #4 tower "Xây tháp theo số" | `Assets/A_World/BuildYard/TempP56Journey.cs` | `-journey` | `Assets/Editor/TempBuildP56.cs` | `TempBuildP56.BuildJourney` → `E:/LWW/P56JBuild` |

## Run (maynode)

1. Build the journey player: batchmethod `TempBuildP5x.BuildJourney`
   (driver edits only recompile scripts; the player EXE stays valid).
2. Run windowed foreground:
   `LWE.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -logFile <log> -journey -lang en -shot-dir E:/LWW/p56j-shots-t3`
   (`-lang en` skips the language-card dependency; `-shot-dir` splits evidence
   per target run: t3/t5/t9).
3. Expect `[P55J]`/`[P56J]` trace ending in `JOURNEY_END ... errors=0`; shots go
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
