# COUNTING GARDEN — "ĐƯA ĐÚNG SỐ LƯỢNG VÀO RỔ" (REFERENCE GAMEPLAY BLUEPRINT)

Date: 2026-09-23. Owner: Lead Gameplay + Experience Design + Tech Direction.
Scope: the FIRST reference gameplay of the Micro-World system. Reuse Core only.

## 1. Experience (what the child lives)

A school-garden moment: a TEACHER explains number 2 at the board, a STUDENT
demonstrates the task (take two balls, put them in the basket), then the child
takes control and does it — the teacher counts along, the world confirms with
number + objects + spoken word, mistakes are handled as a counting lesson, and
success is a small celebration. No adult UI, no fail screens, no text reliance.

Representations that must always agree: BOARD "2" > BALLS (5) > BASKET (count).

## 2. Gameplay state flow

INTRO (teacher teaches -> student demonstrates, real walking/picking/placing)
  -> TASK_READY (student tidies, both observe the child; "Now you try!")
  -> PLAYER_ACTIVE (child free; pick/carry/place; teacher counts 1, 2)
  -> COUNTING (per ball: count display pips + teacher line)
  -> SUCCESS (2 in basket: board/result "2 + tick", confetti, both celebrate)
  -> WRONG (a 3rd ball enters: teacher counts 1-2-3, "the board says two",
     "we only need two"; the extra ball gently returns home; back to SUCCESS)
  -> COMPLETED (deterministic; re-entry shows the completed visual, no replay).

State is held in ONE module (`CountingGame`) + the shared `ActivityLifecycle`
(in-memory, owned by the Math-side area so it survives the arena unload).
No bool soup, no new manager/singleton/service.

## 3. NPC acting flow

TEACHER: IDLE -> LOOK_AT_BOARD -> POINT_BOARD -> LOOK_BALLS -> POINT_BALLS ->
POINT_BASKET -> OBSERVE -> COUNT (1,2) -> CONFIRM -> CELEBRATE | CORRECT (wrong).
STUDENT: NOTICE -> LOOK_TEACHER -> LOOK_BOARD -> LOOK_BALLS -> NOD (understood)
-> WALK_BALLS -> PICK (arc to hand, squash) -> CARRY -> WALK_BASKET -> PLACE
(arc into basket, bounce) -> LOOK_BASKET -> LOOK_TEACHER -> CELEBRATE -> tidy.

Gesture language (must agree head + torso + arm):
POINT_TO_BOARD / POINT_TO_BALLS / POINT_TO_BASKET = body turns to the target,
arm aims at it, head follows (procedural bones; no new rigging framework).
Both actors keep breathing/idle motion; nothing teleports or snaps.

## 4. Spatial + camera design

ARENA (CountingPlayScene) layout, local coords (entry at z=-3, child walks +z):
- Board (target "2")  (0, 0, 7.6) facing the entry; teacher (-1.4, 0, 6.6).
- Student (0.7, 0, 5.7) beside the teacher.
- Ball cluster centred (0, 0, 3.2): 5 balls placed naturally (2 close, 1 offset,
  1 behind, 1 near the flowers) — find-and-choose, never a straight test row.
- Basket (2.4, 0, 1.4) + count display beside it (pips, no text).
- Balls <-> basket loop ~3m: a 20-60s round for 4-year-old legs.
- Camera: intro shots A (board+teacher+student), B (balls+student), C (basket+
  result) via the existing SmartCamera FrameAnchor beats, then Follow; no
  shake, no hard cuts, no cinematic hijack.

## 5. Animation audit

Existing (reused): Animator triggers Idle/Victory/PickUp/Celebrate on the
Worker rigs; procedural face kit (expressions, blink, hop, wave, squash),
procedural walk/face-toward, ball arcs + bounce, confetti/sparkle/spotlight.
Missing (added procedurally, small): point-at-target gesture, nod, count pips
pop, basket wobble, result tick. No Animation Rigging/Timeline dependency:
the reference needs readable gestures, not film quality (documented decision).

## 6. GitHub research notes (ADOPT/ADAPT/REFERENCE/REJECT)

- SST-Systems/Interaction-Objects (MIT, FPS raycast pickup + physics hand):
  REJECT — first-person physics carry; Core already has ClickRouter/IClickTarget
  + a hand anchor for third-person click-to-move.
- mariusrubo/Unity-Humanoid-TransportObjects (walk/grab 2-hand/place): REJECT —
  needs paid Final IK + GPLv3.
- Unity-Technologies BossRoom PickUpAction.cs (parent + PositionConstraint to a
  hand socket): REFERENCE ONLY — pattern matches our hand-anchor follow.
- makeplayhappy/headlook, EyeXD/Aim-IK, Bonnate procedural animation,
  EggyStudio HeadTrackingTargetFollow: ADAPT — clamped, smoothed procedural
  head/torso aim (implemented with the existing bone references).
- robertrumney/ik-tools (Animator.SetLookAt/IK weights): REFERENCE ONLY —
  rigs lack an IK pass; procedural bones are safer here.
- HumlabLu/GestAlt (Animation Rigging gestures): REJECT dep — package + rig
  setup not justified for this loop.
- Code Monkey pickup tutorial / Hrober0 FPP controller: REJECT — tutorial/FPP.

## 7. Test matrix (must run)

A fresh entry, B teacher intro, C student demo, D 1 ball, E 2 balls, F place 2,
G correct completion, H 3rd ball wrong path, I retry, J drop/abandon, K spam
click, L leave, M re-enter, N completed state, O audio interrupted, P camera
framing, Q NavMesh, R collider blocking, S exceptions, T standalone build.

## 8. Definition of done

Teacher/student act naturally; number 2 obvious; points to board/balls/basket;
speech matches actions; student really demonstrates; player controls the rest;
pick/carry/place convincing (no floating, no snapping); basket count correct;
wrong count gentle; celebration natural; camera shows the action; navigation
child-sized; no teleport/deadlock/soft-lock; re-entry deterministic; save
architecture untouched; standalone build verified with shots A-H; human visual
review checklist reported separately (never self-declared PASS).
