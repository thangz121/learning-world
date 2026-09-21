# SUBJECT WORLD FOUNDATION (Phase 3.0.x — Math pilot)

Living record of the reusable subject-world skeleton. Rule: Math is an
architectural PILOT, never a hard-coded template — future subjects reuse the
skeleton below through configuration, never by copying Math content.

## 1. World skeleton (frozen shell contract)

```
SubjectWorld (additive scene, offset from Main, own NavMesh)
├── WorldRoot            (scene root GO, e.g. MathWorld)
├── EnvironmentRoot      (ground, boundary, sun — per-subject palette)
├── LobbyRoot            (medallion, host anchor, identity landmark)
├── GameplayAreasRoot    (Area A / Area B pads + dressing)
├── NpcRoot              (reserved; S3A wires host under WorldRoot — S4 target)
├── LearningEntryRoot    (stable entry markers BY ID, never by coordinates)
├── EntryPoint           (travel warp-in — 3.0.2: the LOBBY CENTER, not the
│                         gate: the child spawns mid-world, greeted by host)
└── ReturnPoint          (travel warp-out + return trigger `(0,0,8)` local)
```

Shell carries roots/markers ONLY (CT-P34H pins zero Core scripts in shell).

## 2. Classification

### PROVEN SHARED (reuse as-is)
- `WorldTransition` machine (Idle/Loading/InSubject/Unloading, idempotent,
  spam-safe, fail-truthful + LastError) — subject-agnostic (CT-P36G).
- `ISceneOps` + `UnitySceneOps` adapter (single load/unload call pair, CT-P37D).
- `SubjectCatalog` (Id/DisplayName/positions/palette/landmark/SceneName;
  null SceneName = legacy walk-in — NO learning fields, CT-P31G).
- `SubjectGate` one-way triggers + ClickRouter mouth/ray snap (radii pinned
  CT-P33H/I + CT-P39 numerics: snap 2.0 / fire 1.2 / mouth 0.6 / stop 0.4).
- `MathLearningEntries` pattern: stable string IDs resolved to markers.
- Travel wiring shape (lock input → HUD Entering → fade → load → verify →
  deactivate Main → warp → camera → HUD → unlock; fail restores Main).
- Transition cover in persistent HUD canvas (time-based, no fake progress).
- Quest flow shape (first-talk start → find → bring → complete → restore)
  over the SHARED QuestManager/Hints/Learning (no per-subject managers).
- Carry lifecycle World→Carried→Consumed (bus-only, CT-P40).
- Reward ledger + inert change-id banking (visual consumers per subject).
- Host body via Golden rig reuse (3.0.1.1: TessVisual = MiaVisual structure,
  Worker_Female + shared NPC controller, Math-blue vest + gold hat identity;
  presenter exposes idempotent BuildBodyImmediate — CT-P43F).

### REUSABLE CANDIDATE (proven once — second subject decides)
- Host presenter pattern (MathHostPresenter: Bind + IClickTarget + greet-once
  + hint tick + click/proximity bring + nod). Thinking host will confirm or
  fork it — do NOT generalize prematurely.
- Quest director shape (MathQuestDirector: talk/seen/complete narration).
  Possibly merges into a generic SubjectQuestDirector if Thinking matches.
- Lobby composition grammar (medallion + identity landmark + host east +
  paths). Grammar reusable, content never copied.
- Area grammar (pad + identity dressing + entry/exit path + seam marker).
- Decor grammar (collider-free dressing + corridor-clearance rule CT-P42).
- Attention beacon (bob+spin on quest target).
- Celebration pop (one-shot scale state change, no particles).

### SUBJECT-SPECIFIC (never reuse, never copy)
- Counting Garden / Number Bridge (layout, beds, bridge, blocks).
- Abacus lobby landmark, gold One cube, Tess (visual + voice + name).
- `math_counting` quest JSON + math_01/math_02 lines.
- math_bloom visual (garden blooms).
- Palette/landmark shape language per SubjectDefinition.

### NOT YET PROVEN
- Second subject end-to-end (Thinking will prove or break candidates).
- Unload-stress across repeated entry/exit (S5 adversarial).
- Save interplay with subject progress (Phase 3.1 owns, no format change yet).
- Performance at 4-subject scale (single-subject baseline only so far).

## 3. Reuse matrix

| Component | Math | Thinking | English | Vietnamese | Class |
|---|---|---|---|---|---|
| WorldTransition / ISceneOps | ✅ pilot | ⬜ | ⬜ | ⬜ | PROVEN SHARED |
| SubjectCatalog entry | ✅ | ✅ spatial | ✅ spatial | ✅ spatial | PROVEN SHARED |
| Gate triggers + click snap | ✅ | ⬜ | ⬜ | ⬜ | PROVEN SHARED |
| Travel/return wiring | ✅ | ⬜ | ⬜ | ⬜ | PROVEN SHARED |
| Transition cover | ✅ | ⬜ | ⬜ | ⬜ | PROVEN SHARED |
| QuestManager/Hints/Learning | ✅ reused | ⬜ | ⬜ | ⬜ | PROVEN SHARED |
| Host presenter pattern | ✅ Tess | ⬜ | ⬜ | ⬜ | CANDIDATE |
| Quest director shape | ✅ | ⬜ | ⬜ | ⬜ | CANDIDATE |
| Lobby/area/decor grammar | ✅ | ⬜ | ⬜ | ⬜ | CANDIDATE |
| Beacon / celebration pop | ✅ | ⬜ | ⬜ | ⬜ | CANDIDATE |
| Garden/Bridge/content | ✅ | — | — | — | SPECIFIC |
| 2nd-subject proof | — | ⬜ | ⬜ | ⬜ | NOT YET PROVEN |

## 4. Variation points (per-subject configuration, never forks)
SceneName, palette, landmark kind, gate/playground/entry/return coordinates,
host identity (roster + presenter + voice), quest JSON, area dressing,
reward change-id + consumer. All flow through SubjectCatalog + entry IDs.

## 5. Hard rules (firewall restatement)
No Math literals in shared contracts (CT-P36G). No per-subject loader,
quest manager, NPC system, or transition manager (CT-P37D). No Question/
Topic/Lesson/Curriculum fields in world layer (CT-P31G). Gameplay Pattern
(find→bring shell) is not a Question Type (CT-P36I).
