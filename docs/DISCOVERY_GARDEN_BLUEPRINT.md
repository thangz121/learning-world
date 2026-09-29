# MICRO-WORLD — GAMEPLAY #7: "VƯỜN KHÁM PHÁ" (DISCOVERY GARDEN) — BLUEPRINT

Phase: S3-P2Z18 (2026-09-29). Full vertical slice: Math Hub discovery gate →
transition → independent Micro-World → 3 search tasks → completion → reward →
exit → return hub → re-entry.
Reference: gameplay #2-#6 reports/blueprints — reused as QUALITY/ARCHITECTURE/
PRESENTATION reference, never copied as a gameplay.

## 1. Experience (what the child lives)

NHÌN MẪU → NGHE → QUAN SÁT → ĐI TÌM → "À, THẤY RỒI!" → TIẾN LẠI → TƯƠNG TÁC →
ĐƯỢC XÁC NHẬN → TÌM MẪU KẾ TIẾP.

Pattern: **LOOK → SEARCH → DISCOVER** (the first non-carry gameplay: no pickup,
no carry, no delivery — the reward is FINDING). Wrong = gentle, keep looking.

## 2. Placement + architecture (no new framework)

- Gate `discovery_garden` (index 1, magnifier identity: ring + glass + handle —
  already human-reviewed) gains a walk-in portal + `MicroGateHint` approach
  glow, same contract as #4/#5/#6.
- NEW lazy scene `DiscoveryScene` at island +480x (free slot between Delivery
  +420 and Match +600), built by `DiscoveryBuilder`, driven by scene-local
  `DiscoveryGame` + `DiscoveryArea` (MathScene-side, `IMicroWorldArea`).
- `MicroWorldPortal` gains `DiscoveryArea` (+ dispatch); `MicroGateHint`
  resolver gains the same line. No Core changes, no new manager/singleton.

## 3. Spatial design (child scale, ~7m loop)

    ENTRY (0,-3)
      -> ORIENTATION: reference board + magnifier motif (0,7.4)
         teacher (-1.4,6.4) + student (0.7,5.5)
      -> POCKET A west  (-3.0,3.2): apple tree + 2 apples (TASK 1 clue)
      -> POCKET B mid   (-0.2,3.4): flower bed + 2 flowers (TASK 2 clue)
      -> POCKET C east  ( 2.9,3.4): butterfly bush + 1 FLYING butterfly (TASK 3)
      -> RESULT board (3.4,-0.6): three icon slots (apple/flower/butterfly)
      -> EXIT (0,-11.5) + exit cue beacon (hidden until completion)

- Three short gravel spurs + stepping circles give breadcrumbs (navigation
  without arrows). Bushes are visual pockets, never walls; every item reachable.
- Distractors (balls, leaves, mushrooms) sit in/between pockets. Difficulty
  ladder `Round` (in-memory): round 0 = core set only; round 1 = +5 extra
  distractors active (more candidates, same arena). CLI `-discovery-round N`.

## 4. Items

- `DiscoveryItem`: Kind { Apple, Flower, Butterfly, Ball, Leaf, Mushroom },
  `IsExtra` (round-tier), state **Available → Found** (honest for a
  no-carry gameplay). Click = walk → arrive → interact.
- Found visuals: Pop + Ring + a small emissive tick disc appears above it;
  found items never count again. Butterflies ORBIT their bush while available,
  LAND (gentle hover) when found.

## 5. Tasks (one visit = 3 discoveries)

    TASK 1 Find Apple  -> TASK 2 Find Flower -> TASK 3 Find Butterfly

- Reference board swaps the task icon per task; teacher names each task
  ("Find the apple!" / "Giờ tìm bông hoa nhé!"); `ActivityFeedback.Objective`
  mirrors it; progress dots = `ActivityFeedback.Progress(found, 3)`.
- Wrong candidate: gentle wobble + "Chưa đúng. Con tìm tiếp nhé!" (4s
  cooldown), NO reset, NO fail. Correct: GameJuice CorrectFx + `found` SFX +
  teacher praise + student hops.

## 6. Demo = a REAL search sequence (never a straight line)

Teacher shows the apple on the board → the student walks to the fork → checks
the flower bed (pause, "Where is it?") → checks toward the butterfly bush
(pause) → THEN notices the apple in pocket A → walks to it → interacts
("Con tìm thấy rồi!") → teacher confirms → the apple tidies (findable again) →
"Bây giờ đến lượt con tìm nhé!". `DemoChecks` (2 distractor inspections) is
pinned by tests: the demo may never go straight to the target.

## 7. Camera

| shot | purpose | framing |
|---|---|---|
| teaching | teacher shows the reference | board + teacher + student + field glimpse |
| demo | the search | wide over the pockets: student + bushes + items |
| success | payoff | player + result board + field + teacher |
| follow | the child searches | slightly higher follow so pockets read |

## 8. Completion / reward / exit (no auto-return)

After task 3: teacher "Tìm thấy hết rồi! Giỏi!" + both NPCs celebrate + the
result board lights all three slots + the exit cue beacon lights + queued
"Time to go home!". The child walks out themselves. Re-entry adopts the
finished picture (result lit, cue lit, all task-kind items Found).

## 9. Audio

Teacher (npc_female_01) + student voice (milo_v1 from the roster, passed by
the installer — same seam as #5's receiver). New procedural `found` SFX for
the discovery moment; existing ding/pickup/success reused. Speech follows the
real interaction moment; wrong is spoken BEFORE any success line (pinned).

## 10. GitHub-first research (ADAPT / REFERENCE / REJECT)

| source | verdict | what was taken |
|---|---|---|
| swapnilrane24/Hidden-Object (MIT) | REJECT implementation | 2D UI list-based hidden object; ADAPT only the principle "shape/silhouette search with distractors, no timers, no penalties" |
| Unity Learn / kid-game UX guidance (feedback layers) | REUSE | GameJuice/ActivityFeedback already exist in-project (maynode P1-P5) and are wired here too |
| in-project kits (LessonActors, PacedVoice, SmartCamera, WorldTransition, IMicroWorldArea) | REUSE | everything; no new package, no new pattern |

## 11. Test matrix → CT-P62

| brief item | test |
|---|---|
| A gate portal + seam | P62A |
| B arena structure (pockets/items/board/result/cue) | P62B |
| C tasks + round ladder + CLI | P62C |
| D full flow (demo search checks → handoff → 3 finds → success) | P62D |
| E wrong recovery (×3 wrong then correct, no reset) | P62E |
| F duplicate/spam guards | P62F |
| G item state machine (+ found tick, reset on handoff) | P62G |
| H re-entry adopt | P62H |
| I lazy slot + Build Settings + scene shell | P62I |
| J round ladder + CLI + area seams | P62J |
| K speech safety (recorded full run, both voices) | P62K |
| L demo never goes straight to the target (search ordering) | P62L |
| M exit cue + reward + no auto-exit | P62M |

## 12. Definition of Done mapping

Gate identity/portal/glow; transition = real micro-scene; independent world
(entry → orientation → pockets → result → exit); real search demo; handoff;
real search gameplay (look/search/approach/interact); correct + gentle wrong;
3 tasks; camera; navigation breadcrumbs; audio matches action; completion +
reward + exit cue; return + re-entry adopt; EditMode ≥ baseline; build;
standalone journey on maynode (drivers committed); evidence there; human
visual review pending. STOP after Discovery Garden — the other 5 skeletons
stay untouched.
