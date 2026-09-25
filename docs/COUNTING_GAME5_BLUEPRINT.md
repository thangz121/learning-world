# COUNTING GARDEN — GAMEPLAY #5: "GIAO HÀNG ĐÚNG SỐ" (DELIVER THE APPLES) — BLUEPRINT

Phase: S3-P2Z15 (2026-09-25). Full vertical slice: Math Hub delivery gate →
transition → independent Micro-World → order → demo → player → completion →
reward → exit → return hub → re-entry.
Reference: gameplay #1-#4 blueprints/reports — reused as QUALITY/ARCHITECTURE/
PRESENTATION reference, never copied as a gameplay.

## 1. Experience (what the child lives)

NGHE ĐƠN → NHỚ AI + BAO NHIÊU → ĐI LẤY TÁO → MANG → TÌM NGƯỜI NHẬN → GIAO
TỪNG QUẢ → ĐƯỢC CẢM ƠN → ĐƯỢC XÁC NHẬN.

Pattern: **REMEMBER → PICK → CARRY → HAND OVER** (vs #4 PICK/CARRY/STACK).
Identity: the transfer reads as a HANDOVER to a person (Mia receives, reacts,
thanks), not an object into a container.

## 2. Gate (Math Hub) — finish, never redesign

- Gate `delivery_village` already exists in `MicroWorldCatalog` ("Làng Giao
  Hàng", "DELIVER / GIVE / BRING", ring index 6) with cottage/roof/chimney/
  mailbox/parcel-stack identity — the human-reviewed hub language is untouched.
- ADDED: a walk-in portal at the gate (same contract as #4: whole-arch 0.7m
  hub-side, fireRadius 1.8, cold-start latch → walking past never triggers).
- No hub landmark this round (brief does not ask for one; scope §31).

## 3. Portal seam (small shared capability, proven by test)

Three micro-world areas now exist; `MicroWorldPortal` gains a tiny seam instead
of a third typed field branch:

    interface IMicroWorldArea { ClickToMove Player { get; }
                                void EnterFromHub(); void ExitToHub(); }

`CountingGardenArea`, `BuildTowerArea`, `DeliveryArea` implement it (their
existing members already satisfy it — no signature changes). The portal keeps
its concrete fields for compatibility and resolves through the interface; the
PlayExit case stays garden-typed. Pinned by CT-P57A (compile + type checks).

## 4. Micro-World: DELIVERY VILLAGE (independent scene)

- `DeliveryScene` (lazy, `EnterMicroAsync` only, one micro slot) at +420x.
- `DeliveryArea` (MathScene-side module, same contract as #4) owns the travel
  beats, the `ActivityLifecycle("deliver_apples")` and the target ladder.

## 5. Spatial layout (child scale; entry z=-3, board faces the child)

    ENTRY (0,-3)
      -> ORDER: board "4 + apple" (0,7.6) + teacher (-1.4,6.6) + student (0.7,5.7)
      -> APPLE STALL west (-2.4,3.0): counter + awning + 10 apples in two crates
      -> DELIVERY ROUTE: arrow signposts + parcel dressing along the lane
      -> RECEIVER east (2.6,3.2): counter booth + Mia (3.0,3.9) + delivered crate
      -> RESULT (3.4,-0.6) + EXIT (0,-11.5) + exit cue (hidden -> shown at success)

- Loop stall→receiver ≈ 5m: a 20-60s round per apple, #1-#4 rhythm. All
  dressing off-corridor; route arrows are image-only (no text).

## 6. Camera-first shots

| shot | purpose | framing |
|---|---|---|
| teaching | teacher explains the order | board + teacher + student + field glimpse |
| demo | student delivers | wide: stall + student + route + Mia |
| success | payoff | player + Mia + delivered crate + board + result |
| follow | the child works | north of the child; stall left, receiver right |

## 7. Item state (brief §10)

ONE component: `DeliveryItem` — Available → Picked → Carried → Delivered.
Each item has `Kind` (Apple for content; the receiver refuses anything else —
the wrong-item guard, pinned with a synthetic item). Delivered apples park in
the receiver crate's 10 slots: the crate IS the visible progress count.
No duplicate counting; a delivered apple can never be re-picked.

## 8. Receiver (brief §11)

Mia: notices (faces the player/item), receives (arm-forward gesture + nod),
thanks (voice line "Thank you!" / "Cảm ơn con!", P4 so it never cuts the
teacher), hops on the target landing and on completion. The apple flies from
the player's hand to her hands, then arcs into the crate slot — the transfer is
a person-to-person handover, not a trigger.

## 9. Demo = real handover (brief §6)

Student: listens → walks to the stall → picks (arc to his fist) → carries →
walks to Mia → hands over (arc + receive gesture + thanks) × N → teacher counts
each → "Đúng rồi!" → the delivered apples tidy home during handoff (the child
starts from an empty crate) → "Bây giờ đến lượt con nhé!". Demo and player use
the SAME components/calls.

## 10. Player loop, wrong/short/long (brief §9/§15)

click apple (walk → bend → carry) → click the receiver OR stop at her counter
(reach → hand over → thanks → crate grows). Undershoot: "Còn N quả nữa nhé!"
(8s cooldown, near the work). Overshoot: the extra apple is NOT accepted —
"Đủ bốn quả rồi." + board line + the apple arcs home → success returns. Wrong
item: refused gently, no count. Spam/double/empty-hand: all state-guarded.

## 11. Order memory (brief §16)

The order board shows the digit + an apple icon; the teacher speaks the order
once at the start and reminds it once at handoff; delivered progress lives in
the WORLD (the crate), never a quest-tracker UI. No extra reminder UI.

## 12. Completion / reward / exit (brief §21/§22)

Success: teacher confirms, receiver reacts, result board (digit + tick), short
celebration (no confetti, #2-#4 discipline), lifecycle Completed + the game
pulses the EXIT CUE (a gold beacon above the exit arch, hidden before success)
and the teacher says "Mình ra cổng nhé!" — the child walks out themselves, no
auto-return (the exit portal is always live, human-reviewed pattern).

## 13. Target 1-9 + ladder

Ladder `{ 4, 5, 7, 9, 1, 3 }` (first visit teaches the brief's reference 4;
covers test targets 1,3,5,7,9), `-deliver-target N` CLI, one stall/one
receiver/10 apples (target + spare), completion = crate count == target.

## 14. Audio (brief §18)

Teacher (npc_female_01) counts each handover and confirms; receiver (mia_v1,
from NpcRoster) says thanks on every handover (P4_Feedback so it ducks under
the teacher, never cuts a count); new procedural `give` SFX for the handover,
existing pickup/success/ding reused. Speech follows the REAL landing.

## 15. GitHub-first research (ADAPT / REFERENCE / REJECT)

| source | verdict | what was taken |
|---|---|---|
| Unity coop sample PickUpAction.cs (hand socket) | REFERENCE ONLY | confirms hand-anchored carry; ours already exists (#1-#4) |
| SunnyValleyStudio/Unity-simple-Pick-Up-system | REJECT | raycast+inventory+UI pattern; no inventory by scope |
| Unity discussions pickup/place (hand child GO) | REFERENCE ONLY | same hand-anchor idea already proven in-project |
| #3/#4 in-project kits (LessonActors, PacedVoice, DemoJuice, MicroWorldPortal) | REUSE | all shared kits reused; no new framework |

Tools: Animator (PickUp/Victory/Celebrate reused), NavMesh runtime bake,
SmartCamera anchors, ActivityLifecycle, AudioDirector (+1 `give` clip), PropKit
NOT used for the apples (primitives = known size, same as ApplePresenter's
mini apple language). No package/manager/singleton/service; bus/quest/save
untouched; in-memory activity.

## 16. Test matrix → CT-P57

| brief item | test |
|---|---|
| A gate portal + interface seam | P57A |
| B arena structure + child scale + crate slots | P57B |
| C targets 1..9 | P57C |
| D full flow @4 (intro/demo/handoff/player/success) | P57D |
| E undershoot nudge | P57E |
| F overshoot refusal + correction | P57F |
| G wrong item refused | P57G |
| H item lifecycle + crate slot determinism | P57H |
| I re-entry adopt | P57I |
| J lazy slot + Build Settings + scene shell | P57J |
| K ladder + CLI + area seams | P57K |
| L speech safety @9 (recorded real run, both voices) | P57L |
| M carry robustness | P57M |
| N exit cue + result after completion | P57N |

## 17. Definition of Done mapping

Gate identity/portal; transition = real micro-scene; independent world (entry →
order → stall → route → receiver → reward → exit); order board; teacher; real
demo; handoff; real pickup/carry/delivery; receiver reaction; target 1-9;
under/over/wrong handled; spam-safe; audio matches action; camera serves
action; completion/reward/exit/return/re-entry; EditMode + Windows build;
standalone journey on maynode (driver committed — standing order); evidence
there; human visual review pending.
