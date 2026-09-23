# REFERENCE GAMEPLAY REPORT — "ĐƯA ĐÚNG SỐ LƯỢNG VÀO RỔ"

Date: 2026-09-23. Scope: Counting Garden → zone "Sân đếm" → play arena.
Status: TECHNICAL PASS / VISUAL REVIEW REQUIRED (human gate pending).

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
EditMode **597/592/0/5** (CT-P51 ×6 + re-pinned CT-P50F/P46A; no regressions).

## 9. Standalone build result
Release build **Succeeded errors=0** (110,056,855 bytes), played on the home
machine via the scheduled interactive task (full-HD).

## 10. Screenshot shots A-H (+ P1-P9)
`D:\Vscode\p2z5-final\` — A arrival, B teacher points 2, C task, D student
balls, E carry, F place, G two in basket, H celebrate, I now-you-try,
P1 pick, P2 first-in (pip counter), P3 success, P4 third-pick, P5 wrong,
P6 corrected, P7 spam, P8 back-garden, P9 re-entry completed.

## 11. Known limitations
- Point gesture reads as a raised arm (head+torso turn correctly); no hand IK.
- NPC skin is the existing dark tone; petals read as pills up close.
- Re-entry camera starts at the intro shot-A pose (state itself is correct).
- Activity state is in-memory per session (save format untouched by order).
- 4 skeleton zones still have no gameplay (per earlier scope).

## 12. Human visual review checklist
1. Board "2" obvious from arrival/intro/play? 2. Teacher points read? 3. Ball
pickup/carry looks held? 4. Basket count + pips match? 5. Wrong path gentle?
6. Celebration natural? 7. Camera shows the action? 8. Loop 20-60s? 9. Re-entry
shows the finished picture? 10. Anything a 4yo would misread?
