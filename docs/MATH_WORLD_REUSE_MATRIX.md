# MATH_WORLD_REUSE_MATRIX — P3.0.1.1 (rewritten on HEAD 15ad8bb)

Date: 2026-09-21. Input for P3.0.2 Thinking / P3.0.3 English / P3.0.4 Vietnamese.
Complements `Assets/Documentation/SUBJECT_WORLD_FOUNDATION.md` (living record);
this file adds the audit's foundation findings and promotion rules.

Legend: **PROVEN SHARED** (≥2 consumers or locked contract) ·
**REUSABLE CANDIDATE** (one consumer; second decides) ·
**SUBJECT-SPECIFIC** · **NOT YET PROVEN**.

---

## 1. Shared platform (keep, extend only)

| System | Class | Evidence | Variation point | Risk |
|---|---|---|---|---|
| GameInstaller DI + typed EventBus + LocalSave | PROVEN SHARED | Phase 1/2 locks; arch lint | additive only | save/DI break |
| `WorldTransition` + `ISceneOps`/`UnitySceneOps` | PROVEN SHARED | CT-P34/P37/P38; Math live | loader per subject via data | one loader only |
| Transition cover (persistent HUD canvas) | PROVEN SHARED | CT-P38; travel + return | timing only | must stay time-based (no fake progress) |
| Hub gates + walk-ins for spatial subjects | PROVEN SHARED | 3 spatial subjects + hub-rounds tests | per-subject coords | user-tuned layout |
| `SubjectDefinition` world data (incl. `SceneName`) | PROVEN SHARED | CT-P31/CT-P33/P35 | new data fields only | no learning fields (P31G) |
| `SubjectGate` + ClickRouter mouth/ray snap | PROVEN SHARED | CT-P33H/I + CT-P39 numerics | per-gate radius | snap radii pinned |
| Player/Camera/Click contracts | PROVEN SHARED | CT-P32 + Phase locks | beats via statics | do not fork |
| QuestManager/Hints/Learning | PROVEN SHARED | Math reuses all three | content per subject | no per-subject managers |
| AudioDirector + pregen + voice router | PROVEN SHARED | Phase 2 + manifest | voice ids | approved audio gate |
| CharacterPresentation + Golden rig | PROVEN SHARED | Milo/Mia/Tess | tints + rig | re-measure per rig |
| NatureLibrary + NatureKit + LwGrass | PROVEN SHARED | Main + Math | palette/slots | no-collider rule |
| WorldNameLabel | PROVEN SHARED | NPCs, gates, returns | height/offset | label must not sit on NPC root |

## 2. Math pilot systems — promotion state

| System | Class | Second-subject trigger | Why |
|---|---|---|---|
| MathHostPresenter pattern (Bind/IClickTarget/greet/hint/proximity) | CANDIDATE | Thinking host | extract `SubjectHostPresenter` only if Thinking matches without forks |
| MathQuestDirector shape (first-talk/find/complete narration) | CANDIDATE | Thinking director | merge only on a real second consumer |
| Scene-travel wiring shape (lock → cover → load → verify → hide Main → warp → camera → HUD → unlock) | PROVEN SHARED in shape, **Math-branched in code** (audit F10) | — | generalize to data before Thinking: remove `MathWorldBuilder.*`/`"MathScene"`/`MathEntryPoint` from MarketBootstrap/GameInstaller |
| `MathLearningEntries` stable-ID pattern | PROVEN SHARED pattern | per-subject ID class | Phase 3.1 resolves by ID |
| `MathTokenCarry` World→Carried→Consumed | CANDIDATE | second carry quest | bus-only lifecycle reusable as-is |
| `MathBloomDisplay` (world-change consumer) | CANDIDATE | second reward consumer | banking proven (P36D); consumer per subject |
| `MathBeacon` | CANDIDATE | second attention target | motion-only, zero materials |
| Ceiling pop (×1.4) | CANDIDATE | second celebration | no particles by decision |
| Lobby/area/decor grammar | CANDIDATE | Thinking first area | v2 grammar in redesign §3; promote after Math Batch 1 proves it |
| Host nook staging | NOT YET PROVEN | Batch 2 | doesn't exist yet |
| Loop path + thresholds | NOT YET PROVEN | Batch 1 | new in redesign |

## 3. Subject-specific (never copy)

- Counting Garden plot, brook/bridge layout, number stones, counting tree,
  crops, Counting Frame/abacus, entry bead arch — Math identity.
- `math_counting` quest + math_01/math_02 lines + math_bloom.
- Tess (visual/voice/name) and Math palette roles.
- Math world coordinates and constants.

## 4. Foundation hardening queue (shared, from audit)

| Item | Class | Action |
|---|---|---|
| Dead Math district still built in MarketScene (F9) | PROVEN-SHARED BUG | skip road/district/return for `SceneName != null`; re-pin P31F/P33/P39/P43E |
| Math literals in shared travel code (F10) | PROVEN-SHARED SMELL | move offset/bounds/anchors into per-subject data; flows stay identical |
| Boundary gaps r18 (~4.9 m) (F11) | SHARED BUG | compose/close rim; enclosure test |
| Return arch in MathScene has no "Về" label (F13) | MATH GAP | one label + pin |
| No layout/composition test rails (F15) | CANDIDATE | add invariant tests (openings, landmark heights, clear zones, loop connectivity) |
| Arrival audio identity (F12) | CANDIDATE | hook in arrival beat + per-subject cue |

## 5. Promotion rules

1. A CANDIDATE is promoted only when a second subject uses it **unchanged**.
2. Subject content/palette/landmarks never enter shared code or shared tests.
3. Shared-code edits require the full suite + a build (travel path is
   high-impact).
4. Any new generic abstraction needs two real consumers; otherwise keep the
   Math implementation local and documented as candidate.
