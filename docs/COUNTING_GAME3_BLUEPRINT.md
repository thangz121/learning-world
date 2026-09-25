# COUNTING GARDEN — GAMEPLAY #3: "CHO THỎ ĂN ĐÚNG SỐ" (FEED THE BUNNY) — BLUEPRINT

Phase: S3-P2Z13 (2026-09-25). Reference: gameplay #1 "ĐƯA ĐÚNG SỐ LƯỢNG VÀO RỔ"
(`COUNTING_GAME_BLUEPRINT.md` / `COUNTING_GAME_REPORT.md`) and gameplay #2
"BẬC THANG CON SỐ" (`COUNTING_GAME2_BLUEPRINT.md` / `COUNTING_GAME2_REPORT.md`)
— reused as QUALITY/ARCHITECTURE/PRESENTATION reference, never copied as a gameplay.

## 1. Experience (what the child lives)

NHÌN SỐ → NGHE → XEM BẠN LẤY CÀ RỐT CHO THỎ ĂN → ĐI LẤY → MANG VỀ → CHO ĂN
TỪNG CỦ → ĐẾM CÙNG CÔ → ĐƯỢC XÁC NHẬN.

The teacher links NUMBER 3 to THREE CARROTS FOR THE BUNNY at the board, the
child student fetches them one by one and feeds the bunny while the teacher
counts 1-2-3, then the CHILD does it: one carrot = one count; feeding past the
target is guidance ("Đủ ba củ rồi."), never failure. Target 3 of 10 carrots on
ONE patch (brief §12).

Pattern: **PICK → CARRY → FEED** (vs #1 PICK → CARRY → PLACE, vs #2
COUNT → MOVE → SEQUENCE → STOP). Identity: "cho thỏ ăn" — the payoff is a
living animal that nibbles, not a container that fills.

## 2. Placement

- The Counting Garden's **carrot patch (zone 0, "Vườn cà rốt")** opens
  **RabbitPlayScene**, its own LAZY scene — same micro-slot contract as the two
  earlier arenas: never loaded at boot, loaded only through
  `WorldTransition.EnterMicroAsync` (one micro scene at a time), unloaded on
  the way home. Chosen over in-arena reuse so each gameplay keeps its own
  space, and over new plots so the garden keeps six plots.
- The zone-0 door opens its panel AT ONCE (demoGate=false): the full
  teacher/student lesson runs INSIDE the arena where patch + bunny share one
  frame — the brief requires the demo to read SỐ → CÀ RỐT → THỎ in one place.

## 3. Spatial design (brief §2/§10/§11)

    ENTRY (0,-3) -> TEACHER + BOARD "3" (0,7.6) -> PATCH (west -1.6,3.0)
      <-> BUNNY + BOWL (east 2.3,3.1) -> RESULT (3.2,-0.4, off sightline)

- Child-scale loop: entry→patch ~4.8m, patch→bunny ~3.9m — a 20-60s round.
- Carrot patch: soil bed + 10 carrots (natural cluster) + open south stand.
- Rabbit corner: hutch + grass ring + feeding bowl + picket arc (south open).
- Count display: 3×3 pip grid (grey→gold, reads 0/9..9/9 for every target).
- No overhead geometry over the walk loop; hutch roof bake-ignored (headroom
  rule). Trees/drifts stay off every corridor.

## 4. Deterministic carrot identity (brief §6)

ONE component decides state: `RabbitCarrot` — Available → Picked (hand coming
down) → Carried (rides the animated fist) → Delivered (flying to the mouth) →
Consumed (hidden + munch beat). Correction trip: BeginReturnHome → Available.
No scattered flags; no click-counting; the bowl never counts a carrot that is
still in the patch.

## 5. Acting (brief §3/§4/§8/§13)

- **Teacher (TessVisual):** "Look at the board!" → "This is number three." →
  "Three carrots." → "Three carrots for bunny!" → "Let's feed!" → counts each
  demo/player carrot → "Yes! Three carrots!" → handoff ("Now it's your turn!"
  / "Feed three carrots!") → undershoot nudges ("Còn N củ nữa nhé!") →
  overshoot correction ("Let's count again!" + 1..N + "The board says three."
  + "Three is enough.") — all target-composed, all ≤6 tokens.
- **Student (MiloVisual):** watches → walks to the patch (no teleport) →
  picks (arc to his fist) → carries → feeds (arc to the mouth, nibble, count
  beat) × N → celebrates → walks back beside the teacher (never blocks the
  feed stand) → observes.
- **Bunny:** faces the bowl; nibbles on every arrival (head bob + ear wiggle +
  body squash + `munch` SFX + crumbs); idle ear twitches + breathing; looks at
  the nearby child; hops on success.
- **Handoff:** camera returns to Follow, lifecycle Active, control was never
  locked.

## 6. The child's round (brief §5/§7)

- Click carrot (walk → bend → carry) → click bowl OR stop beside the bunny
  (reach → carrot flies to the mouth → nibble → teacher counts).
- Undershoot: stays Feeding + gentle remainder nudge near the work (8s
  cooldown, no nagging across the arena).
- Overshoot (N+1 while Success): teacher counts 1..N, points at the board,
  "N is enough.", the extra hops home → Success again. Never a fail.
- Success: confirm + recap 1..N through the pacer's single slot + result pop
  + both NPCs celebrate + the child's own victory hop + lifecycle Completed.
  The field stays open (spare carrots feed repeat lessons).
- No drop mechanic: the carrot rides the hand until fed — state can never be
  lost mid-walk, on a far trek, or on re-entry.

## 7. Camera (brief §9)

| shot | purpose | framing |
|---|---|---|
| teaching | teacher explains at the board | board + teacher + student + patch glimpse |
| demo | student feeds | wide: patch + student + rabbit + bowl |
| success | payoff | player + rabbit + board + result |
| follow | the child works | behind/north, patch and rabbit visible |

Arrival: the area's 2.2s reveal first; teaching joins after ~2s. Success holds
~4.6s, then Follow.

## 8. Lazy + lifecycle + re-entry

- `RabbitPlayScene` ships in Build Settings; `GameInstaller.BuildRabbitPlayScene`
  runs on `sceneLoaded` (only when the door is used) and wires the activity.
- `ActivityLifecycle("rabbit_feed")` lives in the Math-side
  `CountingGardenArea` (survives the arena unload): fresh entry = full lesson;
  re-entry after completion = adopt the finished picture (bowl holds N,
  result up, actors observing, no replay, no camera hijack; spare carrots stay
  scenery).
- Failure discipline unchanged: failed load/transition reloads the garden and
  puts the child back at the entry.

## 9. GitHub-first research + Unity tool audit

| source | verdict | what was taken |
|---|---|---|
| SST-Systems/Interaction-Objects (FPS physics hand) | REJECT | first-person physics carry; Core already has ClickRouter/IClickTarget + hand anchor |
| theSalted/Pickup (single-item carry, hand anchor, state mgmt) | ADAPT (idea) | one-carrot-at-a-time + hand-follow + spam-safe, via existing contracts, no import |
| Unity-Technologies/BossRoom PickUpAction (parent + hand socket) | REFERENCE ONLY | pattern matches our hand-anchor follow |
| psmith35/FeedTheAnimals (throw food, meters, lives) | REJECT mechanic | projectile + fail states unfit for 4yo; ADAPT only "fed count fills the meter" as pips |
| Bonnate procedural animation / look-at | ADAPT | clamped procedural nibble/ear-twitch/breathe with existing bones, no Rigging package |
| Unity Learn "feed hungry animals" (collision decisions) | REFERENCE ONLY | trigger discipline — we poll proximity + click door instead |

Tools audited: Animator (PickUp/Victory/Celebrate — reused), AI Navigation
runtime bake (reused pattern), SmartCamera beats (reused), LessonActors +
PacedVoice + DemoJuice (reused shared kits), EventBus (NOT touched),
Save (NOT touched: in-memory like #1/#2), WorldTransition (no changes at all —
new scene name flows through existing call sites), AudioDirector (+1
procedural `munch` clip, same pattern as #2's `step`).

No new package, no new manager/service/singleton, no second loader.

## 10. Reuse vs keep local

- **REUSED:** CountingGardenArea travel/zone flow, WorldTransition,
  SmartCamera, MarketHUD tunnel/objective, DemoJuice FX, MicroWorldPortal,
  ActivityLifecycle, LessonActors + PacedVoice, CharacterPresentation kit,
  DialogueLang, Digit(n) 7-seg 0-9, CheckMark, SfxId channel (+`munch` clip).
- **GENERALIZED MINIMALLY:** zone-0 spot flags (staged + scene + no demo-gate);
  NOTHING else — the area's ladder/progression pattern extended by analogy
  (RabbitTarget/RabbitProgression/CLI + MaybeAdvance), no shared-code edits.
- **KEPT LOCAL:** `RabbitFeed` (+`RabbitCarrot` +`FeedZone`), `RabbitPlayBuilder`.

## 11. Test matrix → CT-P55 (13 tests)

| brief item | test |
|---|---|
| A garden door | P55A |
| B arena structure + child scale | P55B |
| C targets 1..9 | P55C |
| D teacher intro + demo + handoff + feed 3 + success | P55D |
| E overshoot correction | P55E |
| F undershoot nudge | P55F |
| G spam/double-count | P55G |
| H carrot lifecycle | P55H |
| I re-entry adopt | P55I |
| J lazy slot + Build Settings | P55J |
| K ladder + CLI | P55K |
| L speech safety @9 (recorded lines) | P55L |
| M carry robustness (walk-back/far-trek) | P55M |

## 12. Definition of Done — mapped

Demo reads SỐ→CÀ RỐT→THỎ; student really feeds N; player replays by real
input; targets 1–9; under/over handled gently; rabbit reacts (nibble + munch);
audio matches action; camera serves the action; the corner reads as "nơi cho
thỏ ăn"; re-entry adopts; suite green; standalone verified; evidence captured;
human visual review prepared (TECHNICAL PASS / VISUAL REVIEW REQUIRED split in
the report).
