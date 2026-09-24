# REFERENCE GAMEPLAY REPORT — "ĐƯA ĐÚNG SỐ LƯỢNG VÀO RỔ"

Date: 2026-09-23 (round 2 closure 2026-09-24). Scope: Counting Garden → zone
"Sân đếm" → play arena.
Status: TECHNICAL PASS / VISUAL REVIEW REQUIRED (human gate pending).

Round 2 (S3-P2Z10) closed the action gaps from the 29-section brief: the CHILD
now bends down for real (player-rig PickUp clip), the place is stop-gated and
the ball rides the fist over the rim before the short drop, action sounds play
(procedural, ducked under speech), the board's "2" stays inside the follow
frame, and two real re-entry bugs found by the journey are fixed (see §13).

Round 3 (S3-P2Z11, user feedback): the demo student now holds the ball in its
ACTUAL fist (the flight homes on the animated hand, both scales), every crescent
plot has a real crop-coloured gate and the demo door moved from the yard centre
to its plot edge, and the arena gained a fixed listen circle (the assignment is
read only there and the balls stay locked until it was heard) plus bunting,
counting stones, toy blocks, sparkles, a carry trail and pip pops.

Round 3b (S3-P2Z11b, user screenshots): the zone-2 focus camera moved INSIDE the
plot (past the theatre door) so no door post fills the frame, and the carrying
arm is now aimed at the chest hold point (`TickCarryPose`, one-bone aim) with a
small forward offset on the ball — the carried balls read in the hand at chest
height instead of sinking into the belly.

## 1. Experience blueprint
`docs/COUNTING_GAME_BLUEPRINT.md` (experience → spatial → acting → interaction
→ state → code). Flow: INTRO (teacher teaches number 2, student demonstrates
the real task) → TASK_READY ("Now you try!") → PLAYER_ACTIVE (pick/carry/place)
→ COUNTING (pips + teacher lines) → SUCCESS (result board, confetti, celebrate)
| WRONG (3rd ball: teacher counts 1-2-3, points at the board, the extra ball
returns home) → COMPLETED (deterministic re-entry).

## 2. Gameplay state flow
`CountingGame` (arena-scoped) + shared `ActivityLifecycle` owned by
`CountingGardenArea` (MathScene) so it survives the arena unload:
Intro → FreePlay → (Success) → Completed; Wrong runs a deterministic 11s
teacher-correction beat, then Completed. No bool soup, no manager/singleton.

## 3. NPC acting flow
Teacher: look/point board → point balls → point basket → observe → count 1,2 →
confirm/celebrate | correct. Student: notice → look teacher → look board →
look balls → NOD → walk → pick (arc) → carry → place (arc+bounce) → check →
celebrate → tidy. Gestures = head+torso (root turn) + arm (procedural bone);
the intro runs ONCE (`LoopForever=false`) then both observe the child.

## 4. Animation audit
Reused: Animator Idle/Victory/PickUp/Celebrate; procedural face kit (eyes,
blink, expressions, hop, wave, squash/stretch), procedural walk/face-toward,
ball arcs + bounce, confetti/sparkle/spotlight. Added (procedural, small):
point-at gesture, nod, count pips pop, basket wobble, result pop, held intro
camera. NOT used: Animation Rigging/Timeline (documented decision: readable
gestures over film quality; no new package).

## 5. Spatial / camera audit
Arena (local): board "2" at z=5.6 (emissive, 2.6x1.7 panel, digit 1.3), teacher
(-1.35,4.8), student (0.7,4.2), ball cluster centre (0,1.6) — 5 balls in a
natural spread (2 close, 1 offset, 1 behind, 1 near flowers), basket (2.3,0.6)
~2.4m away, count display (3.6,0.9), result board (3.2,-0.4) off the spawn
sightline. Intro shots A/B/C (lesson/balls/basket) held through the intro; then
child-height Follow. Arrival anchors = child-height field view (not a map).

## 6. GitHub research notes
ADAPT: procedural clamped head/torso aim (makeplayhappy/headlook, EyeXD/Aim-IK,
Bonnate, EggyStudio HeadTrackingTargetFollow). REFERENCE ONLY: BossRoom
PickUpAction (parent + hand socket), robertrumney/ik-tools (Animator IK).
REJECT: SST-Systems FPS physics carry, mariusrubo Final-IK transport (paid +
GPL), HumlabLu/GestAlt (Animation Rigging dep), Code Monkey/Hrober0 FPP.

## 7. Implementation summary
- `CountingPlayBuilder.BuildActivity` (staging + acting layout + emissive
  boards) and refs handed to `CountingDemo` (layout is DATA now).
- `CountingDemo`: layout-driven acting, point/nod gestures, intro-once +
  `OnIntroCompleted`, `SkipToObserving`, held intro camera, gameplay lines.
- `CountingGame.cs`: `CountingBall` (grounded→carried→basket, arcs, no snaps),
  `BasketZone` (click/proximity door), `CountingGame` (state machine, count
  pips, wrong path, completion, re-entry adopt).
- Core-safe fixes found by the journey: SmartCamera beat re-issue return,
  collider re-add after deferred Destroy, HUD pill click-through, language
  chooser `IsShowing`, panel two-way bind.

## 8. Test results
EditMode **608/603/0/5** (round 3: CT-P51J added for the listen circle — the
fixed spot exists before the field, the balls refuse clicks until the
assignment was heard, the teacher calls the child back by name of the circle,
and standing on it fires the task + ding + unlocks play; P51B/C/H/I gained the
`MarkTaskToldForTests` seam; no regressions).

## 9. Standalone build result
Release build **Succeeded errors=0 warnings=2** (110,185,729 bytes), boot
FACE_OK 1× / 0 exceptions; the full journey (real injected mouse, full-HD) ran
EN → Math → Counting Garden (four bed gates + the demo door at its plot) → zone
→ mini lesson (student carries in the fist) → panel → arena → listen circle →
assignment → pick/place ×2 → success → 3rd-ball correction → exit → re-entry
adopt, 0 exceptions. Shots `Temp/opencode/p2z11-shots/`.

## 10. Screenshot shots A-H (+ P1-P9)
`D:\Vscode\p2z5-final\` — A arrival, B teacher points 2, C task, D student
balls, E carry, F place, G two in basket, H celebrate, I now-you-try,
P1 pick, P2 first-in (pip counter), P3 success, P4 third-pick, P5 wrong,
P6 corrected, P7 spam, P8 back-garden, P9 re-entry completed.

## 11. Known limitations
- Point gesture reads as a raised arm (head+torso turn correctly); no hand IK.
- NPC skin is the existing dark tone; petals read as pills up close.
- The carried ball rides one hand (the rig's fist); the brief allows one hand
  when the read is clear.
- The teacher's second task line ("The board says two!") is pacer-pending and
  can be replaced if the child acts immediately — the first line carries the
  task.
- Activity state is in-memory per session (save format untouched by order).
- 4 skeleton zones still have no gameplay (per earlier scope).

## 13. Round 2 journey-found bugs (fixed, S3-P2Z10)
1. Re-entry never adopted the completed state: since the no-intro handover the
   lifecycle was left in Ready, so MarkCompleted silently failed and every
   visit started from 0. Fixed by checking Completed first and calling Begin
   after the staged sequence.
2. The re-entry basket looked empty: ApplyCompletedState parked the balls
   before the demo's SkipToObserving (whose actor reset moved them home).
   Fixed by running the demo skip first, then parking the balls/result.

## 12. Human visual review checklist
1. Board "2" obvious from arrival/intro/play? 2. Teacher points read? 3. Ball
pickup/carry looks held? 4. Basket count + pips match? 5. Wrong path gentle?
6. Celebration natural? 7. Camera shows the action? 8. Loop 20-60s? 9. Re-entry
shows the finished picture? 10. Anything a 4yo would misread?
