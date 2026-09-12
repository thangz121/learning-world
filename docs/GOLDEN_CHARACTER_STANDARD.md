# Golden Character Standard v1 — Little World English

> STATUS: LOCKED (2026-09-12). Future phases may EXTEND this standard but must
> NOT duplicate, fork, bypass, or silently modify it. Any proposed deviation
> requires explicit justification + migration plan + regression validation
> (EditMode green + real player-build visual evidence) + approval.

Reference implementation: Player (`PlayerVisual`), Milo (`MiloPresenter`), Mia (`MiaPresenter`),
all Quaternius CC0 FBX, verified in real player builds (see HANDOFF.md).
Future characters MUST follow this document instead of improvising.

## 1. Hierarchy (§Task1)
```
CharacterRoot (scene root, scale 1, gameplay-authoritative yaw)
├── Collider (capsule; physics only, never reshaped for looks)
├── NavMeshAgent / ClickToMove / Interactable / quest wiring (untouched by visuals)
└── *_VisualRoot (child, uniform scale, presentation only)
    ├── Model (FBX instance) + Animator
    └── CharacterPresentation (face kit, blink, breath, attention, expressions)
```
Rules: gameplay never reads mesh/bones; swapping the model touches ONLY the
visual builder + prefab; visual-only rotation/scale NEVER leaks to the root.

## 2. Orientation (§Task2)
- CharacterRoot forward = model forward = **+Z** (all current FBX verified).
- NPCs face the gameplay camera (player sits between camera and NPC; facing the
  player would show backs). Player faces the camera at spawn
  (`MarketBuilder.FacePlayerToCamera`), then the NavMeshAgent owns yaw.
- If a future FBX faces elsewhere: correct with **VisualRoot local yaw only**,
  never root yaw, never control inversion.

## 3. Scale & grounding (§Task3)
- Target: chibi stylized, NPC ~1.65m, player kid ~1.3m.
- Quaternius armatures import at 100x: VisualRoot scale **0.5** (single location).
- Feet at local y≈0, root at ground y=0. Interaction capsule stays 1.7m on root.

## 4. Face (§Task4)
- Build timing: `SetupFace` stores refs; geometry builds on the **2nd Update**
  (Awake/Start world reads are stale-identity after AddComponent).
- Anchor: skull center = Head/Head_end midpoint; surface depth = measured in
  the EYE band from Head-weighted bind vertices via (bone × bindpose), robust
  p95 (never the global max: hat/hair geometry ≈ +0.36 sits ~0.12 ahead of the
  face pane ≈ +0.24 and the old max seated eyes on that plane → side-view
  floating). Eyes AND mouths share this one pane (outside-in ray profiling
  proves the sculpt face is flat +0.24..0.25 from eye line past mouth line;
  a per-height mouth estimate was tried and reverted — throat/jaw verts in
  the mouth band drag its p95 ≈ −0.14 and bury mouths). Fallback `0.55 * skull`
  (eyes) / `0.40 * skull` (mouth) only when bind data is unusable.
- Eyes: symmetric pupils + white glints, ~0.18–0.23 × skull, line slightly below
  center (never in fringe/brim zone), 0.02 proud. No physics, no raycasts.
- Mouths: smile / flat / open variants, toggled per expression.
- Never: guessed z-offsets, inside-out raycasts, double scale compensation
  (SetParent(head, true) preserves world pose — do NOT rescale after).

## 5. Appeal (§Task5)
- Pupils sized so sculpt shading rims them; glints small; mouths modest.
- Chibi big-head proportions are the pack style — keep uniform, don't re-art one.
- Validate at gameplay distance first, close-up second.

## 6. Expressions (§Task6)
API (`CharacterPresentation`, frozen): `Set/PulseExpression`, `LookAt`, `BlinkNow`.
States: Neutral (flat), Happy (**wide smile ×1.45/×1.3, eyes ×1.12** — must read
at 1.5m+), Curious (soft smile), Surprised (eyes ×1.35 + open mouth),
Concerned (flat + eyes ×0.9). Happy is the durable post-quest baseline
(`SetExpression` on `QuestCompletedEvent`); greet/click use short pulses.
Celebration motion comes from the Victory clip, not face deformation.

## 7. Life (§Task7)
- Breath: visual-root bob ±8mm @0.25Hz. Blink: 2.2–4.8s random, 0.14s close.
- Attention glance: every 6–13s, visual yaw ±15–30°, hold 1.2–2.2s (40% pair
  with Curious). No jitter: smooth-damped, visual root only.

## 8. Animation (§Task8)
Controllers: `Idle` (default, looped via import setting) + `Victory`/`PickUp`
(triggers `Celebrate`/`PickUp`, auto-return) for NPCs; player adds `Walk` on
bool `Moving` (agent speed > 0.5). Missing clip = state omitted, never a
blocking error. Generic rigs, no avatar, no root motion.

## 9. Materials (§Task9)
URP/Lit, instance copies only (imports pristine). Skin mid-brown, face warm tan,
clothing identity colors (Milo orange / Mia coral / player blue). FBX
`isReadable: 1` REQUIRED (face measurement). No emission, metallic 0.

## 10. Camera readability
NPCs camera-facing; spawn faces camera; follow offset (0,3.4,−5.2); faces must
read at 6m (pupil dots) and 1.5m (full doll face).

## 11. Event reactions
Greet/click → Happy pulse; wrong-action → Concerned pulse; quest start → Happy;
quest complete → Happy baseline + Celebrate trigger. Quest logic untouched.

## 12. Integration checklist (future character)
1. License/provenance → `ThirdParty/Quaternius/README.md`. 2. Import, set
`isReadable: 1`. 3. Hierarchy §1 + scale §3. 4. Verify +Z forward (§2) else
VisualRoot yaw. 5. Ground check. 6. Animator §8 (Idle loop + needed states).
7. CharacterPresentation + face verify (§4). 8. Identity tint (§9).
9. Expressions at 1.5m/6m (§6). 10. Event wiring (§11). 11. Real player-build
screenshots (never batch-only). 12. Compile 0 errors, tests green, quest intact.
