# COUNTING GARDEN — GAMEPLAY #2: "BẬC THANG CON SỐ" (NUMBER STAIRS) — BLUEPRINT

Phase: S3-P2Z12 (2026-09-24). Reference: gameplay #1 "ĐƯA ĐÚNG SỐ LƯỢNG VÀO RỔ"
(`COUNTING_GAME_BLUEPRINT.md` / `COUNTING_GAME_REPORT.md`) — reused as
QUALITY/ARCHITECTURE/PRESENTATION reference, never copied as a gameplay.

## 1. Experience (what the child lives)

NHÌN SỐ → NGHE → XEM MẪU → BƯỚC 1 → BƯỚC 2 → BƯỚC 3 → NHẬN RA ĐÃ ĐỦ → ĐƯỢC XÁC NHẬN.

The teacher links NUMBER 3 to THREE STEPS at the board, the child student
demonstrates the climb while the teacher counts 1-2-3, then the CHILD climbs:
one step = one count; stopping stably on step 3 succeeds. Climbing past it is
guidance, never failure. Target 3 of 6 steps (brief §2/§19).

Pattern: **COUNT → MOVE → SEQUENCE → STOP** (vs #1 PICK → CARRY → PLACE).

## 2. Placement (settled with the user)

- The Counting Garden gains a **sixth plot**: "Đồi Bậc Thang" / "Stair hill"
  (index 5, east end +105° of the crescent). The five original plots (4 crop
  beds + demo theatre) keep their indices/angles — every authored coordinate of
  gameplay #1 is untouched.
- The plot's "Vào chơi" opens **StairPlayScene**, its own LAZY scene — same
  micro-slot contract as CountingPlayScene: never loaded at boot, loaded only
  through `WorldTransition.EnterMicroAsync` (one micro scene at a time),
  unloaded on the way home. Chosen over in-arena reuse so each gameplay keeps
  its own space, and over the four crop beds so their future designs stay free.

## 3. Spatial design (brief §3/§24/§26)

    ENTRY (0,-3)  ->  TEACHING/DEMO AREA  ->  STAIR ARENA  ->  LANDING
    spawn + arch       board "3" + teacher     6 steps (w3.4)   goal arch + flag
                       student beside           rise .19/tread .66

- Child-scale stairs: 3.4m wide, 19cm risers, 66cm treads, 6 steps (target 3),
  6+ climb time ≈ 20–45s for a 4yo.
- The hill is a ziggurat silhouette (full-height columns + light tread caps +
  a bead row per tread: N gold beads = the step's number, the garden's counting
  language); the landing carries the goal arch + flag so "the top" is a place.
- No overhead geometry crosses the steps; the goal beam clears 2.3m over the
  landing and is NavMeshModifier-ignored (headroom rule). Side bushes keep the
  staircase the only way up.
- Treads are the CLICK PATH: their colliders STAY (a stripped tread lets the
  click fall through to the ground behind the hill) — the bake still rasterizes
  render meshes, so colliders change nothing about the NavMesh.

## 4. Deterministic step identity (brief §12/§13/§18/§20)

ONE component decides "which step is the child on": `StairRun.StepAt(worldPos)`.

    band = XZ band along the run (0 outside, 1..N treads, N+1 landing)
    step = band, accepted only if |bodyY - treadTop| <= 0.32m

- No scattered `player.position.y > X` checks; no `currentStep += 1` on clicks.
- The count IS the current step: walking back 3→1 reads 1 (never 1+1+1+1);
  standing under or beside the stairs is 0 (never a phantom step).
- The NavMesh approximates stairs as a ramp; the Y window (±32cm ≈ 1.7 risers)
  absorbs that approximation while still rejecting bodies under the stairs.
- Editor tests drive the same pure function with synthetic positions.

## 5. Acting (brief §6-§9)

- **Teacher (TessVisual):** "Look at the board!" → "This is number three." →
  "Three." → "Today, we climb three steps." → "Let's count!" (points at the
  board, then the stairs — never at step 3 specifically).
- **Student (MiloVisual):** "Watch your friend!" → walks to the stair foot
  (no teleport) → climbs 1-2-3 with a count beat on each tread (SFX `step`,
  bead pulse, teacher count line through the speech PACER) → teacher "Yes!
  Three steps!" → celebrates lightly.
- **Handoff (§10/§28):** teacher to the child ("Now it's your turn!" / "Climb
  three steps!"), student walks OFF the stairs (base → beside the teacher) so
  he never blocks the climb, camera returns to Follow, control is the child's —
  movement was never locked.

## 6. The child's climb (brief §11/§16/§17/§19/§21)

- Step up (1..3): soft wood SFX + bead-row pulse + teacher counts the number.
- Overshoot (step 4+ while CLIMBING): teacher "We only need three." + "Come back
  to three!" (5s cooldown, no punishment, no teleport, no reset). Walking back
  DOWN through a high tread is not an overshoot — only upward passes count.
- Success: standing stably on exactly step 3 (0.9s dwell, standing still) →
  teacher confirms ("Three steps! Well done!"), both NPCs celebrate, the child's
  avatar does its own little Victory, the "3 ✓" result board pops, the success
  camera holds the payoff frame (Player on step 3 + Teacher + board "3" +
  result), then the camera hands back for free play.
- No UI beyond the HUD objective; no button, no "next", no arrow.

## 7. Camera (brief §27/§22)

| shot | purpose | framing |
|---|---|---|
| teaching | teacher explains at the board | board + teacher + student + foot of the stairs |
| demo | student climbs | whole staircase + student + progress |
| success | payoff | player on step 3 + teacher + board "3" + result "3✓" |
| follow | the child climbs | behind/below the child, the next treads always visible |

Arrival: the area's 2.2s reveal anchor first; the teaching shot joins after
~2s so the reveal is never stomped. Success holds ~4.6s, then Follow.

## 8. Lazy + lifecycle + re-entry (brief §23)

- `StairPlayScene` ships in Build Settings; `GameInstaller.BuildStairPlayScene`
  runs on `sceneLoaded` (only when the door is used) and wires the activity.
- `ActivityLifecycle("number_stairs")` lives in the Math-side
  `CountingGardenArea` (survives the arena unload): fresh entry = full lesson;
  re-entry after completion = adopt the finished picture (result board shown,
  actors observing, no lesson replay, no camera hijack; the climb stays free).
- Failure discipline unchanged: a failed load/transition reloads the garden
  and puts the child back at the entry — never stranded, never a void.

## 9. GitHub-first research + Unity tool audit (brief §30/§31)

| source | verdict | what was taken |
|---|---|---|
| Unity Manual — Building a NavMesh ("stairs are represented as a flat surface", Step Height) | REFERENCE | stairs = visual steps + navmesh ramp; rise must stay under agentClimb (0.4) |
| com.unity.ai.navigation docs — NavigationWindow (agent Radius/Height/Step Height/Max Slope) | REFERENCE | confirmed the runtime surface bake uses the agent settings; the run was sized (19cm × 66cm) so the bake reads it as a gentle ramp |
| Unity-Technologies/CharacterControllerSamples — steps & slopes tutorial | REFERENCE ONLY | confirms step-handling/max-step-height concepts; REJECTED as implementation (our child moves by NavMeshAgent, click-to-move, not a KCC) |
| Cobertos — "How to climb stairs as a Rigidbody" | REJECT | rigidbody stair-climbing maths would fight the NavMeshAgent; no physics clamps imported |
| dropecho/unity_footstep | REJECT | footstep detection package unnecessary: the game already owns `PlaySfx("step")` (procedural clip added in AudioDirector) |
| Unity manual — Height Mesh (`buildHeightMesh`, advanced bake) | CANDIDATE (documented, not enabled) | could remove the residual ±9cm ramp-vs-tread gap for exact foot placement; not enabled to avoid touching the shared bake settings this round — recorded in the report as the next visual-polish lever |

Tools audited: Animator (PickUp/Victory/Moving — reused, no new clips), AI
Navigation/NavMeshSurface (runtime bake on the scene root, Children — reused
pattern), SmartCamera beats (reused, no new camera system), EventBus (NOT
touched by this activity), Audio/Speech (IAudioDirector + PacedVoice — reused),
Save (NOT touched: the activity is in-memory like #1), WorldTransition
(modified ONLY by adding the new scene name at the call sites, no new loader).

No new package, no new manager/service/singleton, no second loader.

## 10. Reuse vs generalize vs keep local (brief §1)

- **REUSED:** CountingGardenArea travel/zone flow (lazy slot + bounds + panel),
  WorldTransition, SmartCamera, MarketHUD tunnel/objective, DemoJuice FX,
  MicroWorldPortal, ActivityLifecycle, CharacterPresentation face/shoe kit,
  DialogueLang, the `SfxId` channel (new `step` clip).
- **GENERALIZED MINIMALLY:** `CountingDemo`'s actor construction moved to
  `LessonActors` (shared; CountingDemo delegates — behaviour byte-equal,
  CT-P48 green) + `PacedVoice` (the paced speech helper both lessons share);
  `CountingGardenArea.SetPlay` now takes the play scene's island/bounds/follow/
  objective (two lazy arenas, one contract; gameplay #1's call site passes its
  own constants unchanged); `GardenZoneSpot` gained `playSceneName` +
  `demoGate` (which zone opens which scene; only the demo theatre previews
  through the garden miniature).
- **KEPT LOCAL (game-specific):** `NumberStairs` (the state machine + acting +
  step feedback), `StairRun` (step identity), `StairHillBuilder` (the arena).

## 11. Test matrix (brief §32) → CT-P53

| brief item | test |
|---|---|
| A plot/structure | P53A (garden plot) + P53C (arena) |
| B step identity (incl. under/beside/landing) | P53B |
| C Teacher intro | P53D (phase flow) + P53H (lines) |
| D Student demo (counts 1-2-3) | P53D (`DemoStepsClimbed`) |
| E Handoff | P53D (lifecycle Active at Climb) |
| F/G/H climb 1-2-3 + success | P53D |
| I Overshoot | P53E (no fail, guidance, later success) |
| J Walk back | P53D (1→2→1 reads 1, never completes) |
| K Spam/no-op | P53B determinism + P53E (re-stand) |
| L Leave/re-entry/adopt | P53G (no replay, no speech, free climb) |
| M Lazy load/one slot | P53F (WorldTransition fake ops + Build Settings + scene shell) |
| N Audio/speech | P53H (SafetyFilter caps both languages + recorded beats) + SFX pins |
| O Colliders/click path | P53C (treads keep colliders) |
| P Camera | anchors pinned in P53C; framing audited by the journey shots |
| Q Standalone build | production build + boot smoke + journey (report) |

## 12. Definition of Done (brief §35) — mapped

Uses the #1 reference (shared kits), independent gameplay (COUNT/MOVE/STOP),
real staircase arena, visible number target, teacher explains, student
demonstrates + counts, control handoff, physical walking (click-to-move, no
teleport), deterministic step identity, current step updates, backtracking,
overshoot guidance without punishment, correct count completes, teacher
confirms, student reacts, camera communicates, audio matches action, no
unnecessary UI, no new architecture, in-memory re-entry, standalone verified,
evidence captured, human visual review prepared (TECHNICAL PASS /
VISUAL REVIEW REQUIRED split in the report).

## 13. 9-step round (MAXIMUM = 9, brief ��1�43)

One staircase for every target; the target only decides the stopping step.

- Geometry: `StepCount` 6?9, `Rise` 0.19?0.16 (total 1.44m � NHI?U B?C TH?P),
  tread/width/base unchanged; landing + goal arch + mound + hill trees shift
  north by the same formulas (no magic numbers); side stringers (slim
  bake-ignored boards riding the slope) unify the run; bushes 4?6 pairs,
  step flowers 3?5. Bead rows extend 1..9 with the same language.
- Board/result stage the round digit via `Digit(parent,�,n)` (new general
  7-seg 0�9 table; `Digit2`/`Digit3` kept as thin wrappers so old pins hold);
  `StairHillBuilder.BoardTarget` (default 3) is pushed by the installer before
  `Build()`; names renamed `SHNumberDigit`/`SHResultDigit` (deliberate re-pin).
- `NumberStairs.Target` (settable, clamped 1..9, default 3): number-word
  tables EN/VI + singular helper (`Steps(n)`); intro/demo/handoff/overshoot/
  success lines all compose from the target; demo climbs Target steps
  (matches gameplay, �40); success confirms + FIFO-recaps 1..Target through
  the pacer's single slot (PacedVoice stays newest-wins � untouched shared
  code); undershoot nudge ("C?n N b?c n?a nh�!" / "N more steps!", cooldown 8s,
  only when settled below target, at step ?1 or near the foot for step 0);
  band commits only after 0.15s settle (brief �16: kills boundary spam but a
  steady 2.2 m/s climb still counts every tread); NO confetti on success
  (brief �28: glow + sound + NPC reaction only).
- Area ladder: `StairTarget` (default 3) + `StairProgression` [3,5,7,9,1] +
  `-stair-target N` CLI (same pattern as `-lang`) + advance-on-completion
  (latch-free: fires exactly when a Completed life still belongs to the
  current target, then issues a FRESH lifecycle). Save untouched (in-memory).
- Research (�37): Unity manual (stairs bake as flat ramp; Step Height) ?
  ADOPT existing bake, rise 0.16 ? 0.4; CharacterController package ? REJECT
  (would replace NavMeshAgent � �23 forbids touching Core); trigger volumes ?
  REJECT (polled StepAt stays the ONE identity, �12/�14); IK/Rigging packages
  ? REJECT (geometry/camera/animation first, �24); foot placement stays
  procedural (squash/hop/face kit already audited).
- Tests: new `CT-P54_StairTargets` (A digit maps, B validity 1�9, C flow@5 +
  recap counts, D overshoot@9 landing, E undershoot nudge + cooldown, F
  boundary debounce, G 1..9 walk + recap drain, H backtrack@7, I ladder+CLI,
  J recorded-lines safety @9, K singular grammar @1); P53 re-pinned for settle
  (multi-tick after teleports) + digit renames + 9 steps.
- DoD mapping (�41): all boxes ticked in code+tests except human visual review
  (�34 camera reads at 9 verified by journey shots; feet/occlusion stay
  human-owned).
