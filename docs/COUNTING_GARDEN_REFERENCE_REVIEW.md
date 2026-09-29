# COUNTING GARDEN — REFERENCE REVIEW #1–#6 (closing report, S3-P2Z18)

Date: 2026-09-26. Machines: maynode (foreground: real-click journey, evidence) +
ASUS (batch: suite/build in earlier rounds). This is the consolidated review of
the six created gameplay activities of the Counting Garden chain, written after
the first CLEAN full journey (9/9 stages, severe=0). It reports what EXISTS and
what was PROVEN — it creates no new rulebook and no new gameplay.

Blueprint/report per game:
`COUNTING_GAME_BLUEPRINT|REPORT.md` (#1), `COUNTING_GAME2_*` (#2),
`COUNTING_GAME3_BLUEPRINT.md` (#3), `COUNTING_GAME4_*` (#4),
`COUNTING_GAME5_*` (#5), `COUNTING_GAME6_MATCH_BLUEPRINT.md` (#6),
`COUNTING_GAME6_7_BLUEPRINT.md` (harvest beds #6/#7 internal).

## 1. The six activities

| # | name | where (garden zone / hub gate) | pattern | arena scene (lazy) | target | tests |
|---|---|---|---|---|---|---|
| 1 | Đưa đúng số lượng vào rổ (balls) | garden zone 2 "Sân đếm" | PICK → CARRY → PLACE | `CountingPlayScene` | 2 (reference) | CT-P51 + P60A |
| 2 | Bậc thang con số (stairs) | garden zone 5 "Đồi Bậc Thang" | COUNT → MOVE → SEQUENCE → STOP | `StairPlayScene` | ladder 3·5·7·9·1 | CT-P53/P54 + P60B |
| 3 | Cho thỏ ăn đúng số (rabbit) | garden zone 0 "Vườn cà rốt" | PICK → CARRY → FEED | `RabbitPlayScene` | ladder 3…9·1·2 | CT-P55 + P60C |
| 6a | Hái đúng số dâu (strawberry) | garden zone 1 "Vườn dâu" | PICK → CARRY → FILL (basket) | `StrawberryPlayScene` | ladder 3·5·7·9·1 | CT-P58 + P60D |
| 7 | Bẻ đúng số ngô (corn) | garden zone 3 "Vườn ngô" | PICK → CARRY → LOAD (cart) | `CornPlayScene` | ladder 3·5·7·9·1 | CT-P58 + P60E |
| 4 | Xây tháp theo số (tower) | Math hub gate `build_yard` | PICK → CARRY → STACK | `BuildTowerScene` | ladder 3·5·7·9·1 | CT-P56 + P60F |
| 5 | Giao hàng đúng số (delivery) | Math hub gate `delivery_village` | PICK → CARRY → HANDOVER | `DeliveryScene` | ladder 3·5·7·9·1 | CT-P57 + P60G |
| 6 | Ghép đúng cặp (match) | Math hub gate `match_meadow` | LOOK → IDENTIFY → SEARCH → CARRY → PAIR | `MatchMeadowScene` | pair ladder 1→2→3 | CT-P59 + P60H |

Zone 4 "Vườn bí" (pumpkin) stays a skeleton plot (no gameplay) — by scope, not
a bug. Each arena is an INDEPENDENT lazy micro-scene loaded only through
`WorldTransition.EnterMicroAsync` (one micro slot, never at boot).

## 2. Cross-cutting review (the requested categories)

### 2.1 Mechanic
Every game is a real-world action loop (walk → bend → carry in the fist →
place/feed/load/give/pair) driven by the existing ClickRouter/IClickTarget +
NavMesh click-to-move. No mini-game UI, no hotspot popup, no scripted pathing.
Under/overshoot are gentle guidance (recount + return), never a fail screen.
Spam is safe: a placed/carried object refuses re-picks; state components are
single-owner (`CountingBall`, `StairRun`, `RabbitCarrot`, `HarvestItem`,
`TowerBlock`, `DeliveryItem`, `MatchItem`).

### 2.2 World
Counting Garden = 6 plots in MathScene (zones 0–5) + 4 hub gates in Math Hub
(counting/build/delivery/match) + return arch to Main. All arenas are islands
at dedicated offsets (+180x…#+600x) so NavMeshes never overlap; each arena has
its own entry, listen circle (where the brief asks), work field, bin, result
board, reward, exit. Decor is deterministic, collider-free, corridor-safe.

### 2.3 Gate
Garden plots: `GardenZoneSpot` (click or ≤2m proximity focus) → zone camera +
HUD name → demo → `GardenZonePanel` ("Vào chơi"/"Quay lại"). Hub gates:
`MicroWorldPortal` whole-arch walk-in (fireRadius 1.8, 0.7m hub-side,
cold-start latch) + `MicroGateHint` glow; the child is warped to the arena
entry only after the scene is verified loaded; any failure returns to the
hub/garden truthfully (no void).

### 2.4 Demo (mini lesson = real tutorial)
Garden miniatures (`CountingDemo` zone 2, `StairLessonDemo` zone 5,
`HarvestLessonDemo` zones 1/3, `RabbitLessonDemo` zone 0) run two-NPC lessons at
the plot; the panel opens ONLY after one full try-run (audience/focus gate).
S3-P2Z20 user round: NO arena replays a demo — every arena (tower/delivery/
match added here; balls/stairs/rabbit/harvest already did) opens on a marked
play spot ("Come here!" + pulsing ring); the teacher only calls the child over
and reads the question when the child actually stands there. Demo and player use
the SAME components/calls (a wrong demo action could never complete). Fixed
earlier: a lesson ADOPTED by a zone click (`StartFocusedLesson`) is owned by the
zone, so distance never aborts it (pinned P50E).

### 2.5 Gameplay
The child's loop is the demo's loop. Correction paths: #1 3rd ball recounted and
returned; #2 overshoot/undershoot band guidance; #3/#6a/#7 spare item recounted
+ returned; #4 spare block hops home; #5 wrong kind refused + extra apple
recounted; #6 wrong object walks itself home. Completion is deterministic;
re-entry adopts the completed picture (`ActivityLifecycle` lives in the
Math-side area, surviving arena unload), and the ladder advances only on LEAVE.

### 2.6 Camera
Per-game authored shots: teaching (board+teacher+child), demo/action (work
field), success (payoff), plus child-height Follow. Success framings were
re-framed in this round (S3-P2Z18) so the "N + tick" result board sits IN the
payoff axis (was 58–90° off; now 12–31°) — pinned by CT-P60 A–H across all 8
boards; the corn reward flag moved left so it can never cover the board.

### 2.7 Audio
All voice goes through `IAudioDirector` (no direct TTS anywhere); lines are
target-composed and ≤6 tokens; procedural SFX per action (`step`, `munch`,
`block`, `give`, `pickup`, `basket`, `success`). Learning focus ducks music
while a lesson plays. Speech is paced (next line waits for playback + breath).

### 2.8 NPC
Shared `LessonActors` kit (teacher TessVisual + student MiloVisual, mini 0.62x
in garden / 1x in arena) with procedural gestures (point, nod, wave, hop,
squash), face kit (expressions, blink), and REAL hand carry (objects ride the
animated `HandBone`/fist, never the belly). No new rig/package.

### 2.9 Completion
Each activity ends with: confirm line + recap count + result board "N ✓" +
reward (bloom/flag/beacon/disc) + both NPCs celebrate + child Victory +
`ActivityLifecycle.MarkCompleted`. No score, no stars, no fail.

### 2.10 Exit / return
Exit portals/arches are always live; completion never auto-returns. Walking out
returns to the garden/hub (HUD/camera/bounds restored), and the next visit
stages the next ladder rung. Return to Main is the gold "Về" arch in the Math
hub (unchanged).

### 2.11 Persistence
In-memory per session (activities, ladders, rewards). Save format untouched
(owned by later phases). `TempFullJourney` driver state is tooling-only.

### 2.12 Tests
EditMode final: **715 total / 710 passed / 0 failed / 5 skipped** (baseline
714/709 + P50E adopt pin). Relevant fixtures: CT-P51 (#1), CT-P53/P54 (#2),
CT-P55 (#3), CT-P56 (#4), CT-P57 (#5), CT-P58 (harvest #6a/#7), CT-P59 (#6),
CT-P60 (payoff framing), CT-P50 (audience gate/adopt), plus the shared pins
(P42 decor safety, P41 functional layout). Lazy scene contract + Build Settings
pinned per arena; no test pokes gameplay state to steer.

## 3. Journey evidence (real clicks, full-HD, maynode)

Final clean run (2026-09-26, `E:\LWW\p61-shots`, log `E:\LWW\p61-journey.log`):

    FULLJOURNEY run #1 from '(start)'   (real mouse, every created part)
    garden.balls PASS (83s)      garden.stairs PASS (99s)
    garden.rabbit PASS (115s)    garden.strawberry PASS (71s)
    garden.corn PASS (98s)       hub.tower PASS (178s)
    hub.delivery PASS (234s)     hub.match PASS (108s)
    return.main PASS (6s)
    shots=118 clicks=522 errors=0 severe=0

The previously SEVERE stage `garden.balls` is now clean. Root cause (real
source, from the p60b log/evidence): the journey WATCHDOG stayed armed while
the child watched the ~28s zone-2 lesson; its stall rescue re-clicked the plot,
and `GardenZoneSpot.OnClicked` restarts the lesson + hides the panel — the
driver killed its own panel before tapping Play. Fixes: (a) driver disarms the
watchdog the moment the zone is focused (demo wait = by-design stand-still);
(b) `CountingDemo.StartFocusedLesson` adopts a running AMBIENT pass as focused
so the zone owns it (distance no longer aborts it). Pinned P50E; no game
behaviour for normal play changed.

Also in this round: the 6 result boards moved into the payoff axis (CT-P60,
8/8 targeted; suite 714/709 before P50E), builds production `E:\LWW\P56Build`
+ journey `E:\LWW\P56JBuild` **Succeeded errors=0** (World.dll 19:08), boot
smoke `FACE_OK / 0 exceptions`, HUD `'Chọn một cổng nhé!'`.

Earlier evidence per game: `E:\LWW\fullj-shots`, `fullj-z17{c,d,e,f,g}`,
`p53j-shots*`, `p54j-run*`, `p56j-shots*`, `p57j-shots*`, `p60-shots`,
`p60b-shots` (all uncommitted tooling evidence).

## 4. Capability proven (what the codebase can do now)

- One shared lazy micro-world pipeline (WorldTransition + ISceneOps + one micro
  slot) carries ANY subject activity: gate → transition → independent scene →
  entry → demo → real child gameplay → completion → reward → exit → return →
  re-entry adopt — proven live for 8 arenas.
- Two-NPC acting kit with real hand carry at two scales, paced target-composed
  speech, procedural juice — reused by every activity without new packages.
- Garden zone picker (spot → demo gate → panel) + hub portals + progress
  landmarks are reusable patterns for the remaining subjects.
- A stage-engine journey driver with real injected mouse input, census
  telemetry, SEVERE evidence capture + skip/relaunch, and a PASS/SEVERE summary
  — the template for the next world's verification.

## 5. Verdict

**TECHNICAL PASS** for #1–#6 (and the harvest beds): suite green, builds
succeeded, boot smoke clean, and a real-click full journey completed every
stage with 0 exceptions and 0 severe.

**VISUAL REVIEW REQUIRED** — the human eye still owns, per game: payoff
framings (boards now in frame), carry/hand reads at gameplay distance, feet vs
treads on #2, demo tidy-ups reading as "dọn dẹp", and the child-scale feel of
each world. Evidence shots above; the journey shots are the intended review
set.

## 6. Known limitations (carried, not hidden)

- #1 target is the reference 2 (no ladder); the other games own ladders 1–9.
- Zone 4 pumpkin stays skeleton (no design/gameplay ordered yet).
- Activity state is in-memory; a full restart resets ladders/rewards.
- 5 EditMode skips are the known by-design skips (unchanged).
- Everything remains uncommitted (standing order: no commit/push without the
  user's explicit command).

No new rulebook created: the per-game blueprints/reports stay the single
sources for their own contracts; this document only consolidates and cites.
