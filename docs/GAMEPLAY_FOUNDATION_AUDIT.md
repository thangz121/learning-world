# GAMEPLAY FOUNDATION AUDIT — P3.0.1 (AUDIT ONLY, NO CODE)

Date: 2026-09-22. HEAD: `e95d690 phase3011` (suite 534:529/0/5, build Succeeded).
Scope: P3.0.1 sections 1–20, 24–26. No code changed in this pass. No patterns built.
Method: repo read + 3 parallel audits (worlds / contracts / docs-tests-history) + GitHub research.
Journey ref: `docs/P3.0.1_MATH_JOURNEY_AUDIT.md`, `docs/P3.0.1_HIDDEN_BUG_AUDIT.md` (J1–J8), `HANDOFF.md §36`.

## 1. What exists today (evidence)

**Worlds (code-built, no .unity micro scenes):**
- `Assets/A_World/MarketBuilder.cs:52-68,97-125,218-268` — Main/Market: spawn (0,0,4.5), Milo/Mia, crates, ground 38x32m, fog 18-45, sun 68°/-35°, hedge 17/14.5, `SetWorldNav`, carves.
- `Assets/A_World/SubjectWorldBuilder.cs:54-105,119-171,452-593,657-704` — 4 gates (pillars ±1.6, disc 2.6m), playgrounds (medallion 6m, return disc + "Về"), signposts, outer hedge. Foreground/midground/background comments `:346,690`.
- `Assets/A_World/MathWorld/MathWorldBuilder.cs:24-88,60-69,120-933` — island r26 at offset (60,0,0), zones: Lobby pad r11, Counting Frame, Garden (-16,5) r13 + fence/crops/pedestals, Bridge (15.5,-5) + brook/deck/clearing, paths spokes+loop, entry arch z=-12 / return arch z=12 + "Về", HostAnchor (3.4,0,1.2), Sign, FollowOffset (0,4.6,6.4). 1218 lines after phase3011 rewrite. PropKit 58 FBX (Kenney Food/Nature CC0 + Quaternius, `ENVIRONMENT_ASSET_SOURCES.md`).
- `Assets/A_World/PropKit.cs:22-113` — Place/load/strip-collider/harmonize; `WorldQuestionBubble.cs:50-122` — AnchorFor = npc+(1.45,1.78,0.55), icon only.
- Core: `_SharedKernel/WorldFoundation.cs` (SubjectId, WorldChangedEvent, IWorldNavService), `WorldTransition.cs:36-72` (Idle/Loading/InSubject/Unloading, Enter/ReturnAsync, spam-safe), `WorldNavService.cs:30` (idempotent Enter), `GameInstaller.cs:120-218` (only-new-services root, WireMathContent: Tess host + director + carry + bloom), `MarketBootstrap.cs:65-790` (travel/return/camera beats, HUD tunnel/cover).

**Gameplay contracts (current owners):**
- Quest lifecycle EXISTS but narrow: `B_Brain/QuestManager.cs:57-142` (Start/ReportAction/AdvanceOnSeen-Find-only/AdvanceOnSpoken/QuestCompletedEvent-only-signal), content boundary `IQuestContentProvider/QuestData/ObjectiveData :22-55`, `CatalogQuestProvider.cs:16-45`, `QuestPattern.cs:17-45` (A-Find..E-MultiStep Classify, data-shape only), `QuestContentCatalog.cs:71-148`.
- Directors are narration-only: `_Bootstrap/MathQuestDirector.cs:57-110`, `MarketBootstrap.cs:455` (W1).
- Question/QuestionType INTENTIONALLY MISSING (hard ban `SubjectDefinition.cs:5`, `WorldFoundation.cs:3`, guards CT-P31:97-98, CT-P36:154). Typed vocab = `PlayerAction{Find,Bring,Speak,Give,Select}` (`Ids.cs:53`).
- Interaction EXISTS minimal: `IClickTarget.OnClicked()` (`_SharedKernel/IClickTarget.cs:5-6`), impls MathHost/Mia/Milo/DistractorChoice/ClickForwarder; routing `ClickRouter.cs:107-124` (Physics.Raycast + GetComponentInParent + MoveTo, arrivalRange 0.75m); `ClickToMove.cs:101-110` (New Input System + EventSystem guard); `Interactable.cs:7-50` (class + InteractionId, no IInteract interface).
- Player controller MISSING by design (only `PlayerVisual.cs:15-19`, `ClickToMove MoveTo/WarpTo` NavMesh). Camera EXISTS constrained: `SmartCamera.cs:14-17,74-147` (Follow offset 0,3.2,4.6 / Interaction / Cinematic, FocusOnFor/FramePointFor auto-return).
- NPC roster EXISTS: `NpcRoster.cs:29-48` (milo/mia/tess), voices `Milo.cs/Tess.cs/Mia.cs`, presenters own carry/bring state-driven (`MathHostPresenter.cs:143-149` IsFindDone — J5 fix), `CharacterPresentation.cs:19-312,696` (SetupFace/SetExpression/Pulse/LookAt/Hop/Twirl/TickBreath, R5k raycast).
- Feedback EXISTS voice+prio (`Ids.cs:57` P4_Feedback, `AudioDirector.cs:93-95,425` ducking). Reward state EXISTS: `QuestRewardService.cs:17-98` (GetFriendship/HasWorldChange, once, math_bloom friendship 0), visuals `MathBloomDisplay.cs:29-62` (bus+adopt 1.4x pop — J6 fix), `MathTokenCarry.cs:23-75` (InWorld/Carried/Consumed + adopt — J7 fix).
- Save EXISTS `LocalSave.cs:18-89` (lwe_save.json, words/questsDone/playTime/npcVoices/worldSeed/gender, fail-soft); `ISaveService`. Re-entry PARTIAL: bloom/token/host adopt live, but `questsDone` persisted yet never replayed into in-memory `QuestManager`; `VocabularyProgression.cs:15` unwired.
- Audio/Speech LOCKED (do not rewrite): `AudioDirector.cs:25-325` sole voice owner, `SpeechFoundation.cs:1-60`, Recognizer/Capture/Policy/Providers + Azure/Fallback/Mock, `SpeakingExercise.cs:138-237`.
- Tests CT-P38..P43 headless (BuildContent/statics, no player/scene/NavMesh/unload): P38 cover/raycast/tunnel, P39 gate-mouth/pillar-snap, P40 loop+J5-J7 adoption, P41 content pins, P42 safety/budget, P43 geometry/Tess/return-bind (P43K = J2 pin). J1/J2/J3/J4/J8 invisible to unit tests by doc's own admission.

**Journey proven (release standalone, real clicks):** Spawn→Main→Math gate→MathScene additive→Tess→garden click `one` (Carried)→bring (Completed/Consumed/bloom)→Bridge far-bank→return arch→Main→re-enter (adopt OK)→return. 12 dumps, no duplicates. Suite 534 green.

## 2. Pattern vs QuestionType — PRESERVED (do not regress)

- Pattern = data shape (`QuestPattern.cs`), never gameplay. QuestionType hard-ban intact. Math counting reuses Find+Bring (`QuestManager.cs:185-192`), no new type. P3.0.1 §4 holds. Any future `IQuestion` must be learning-interaction struct, never pattern gameplay.

## 3. Section-3 contract matrix (A–T)

| ID | Contract | State | Owner / Evidence | Missing for reuse |
|----|----------|-------|------------------|-------------------|
| A | Gameplay World | PARTIAL | SubjectWorldBuilder + MathWorldBuilder + WorldFoundation | No Micro-World module/files; F9 dead district still built (`BuildShell` ignores SceneName) |
| B | Activity Lifecycle | MISSING | Only QuestManager Start/Complete + WorldTransition Idle/Load/In/Unload | No Unavailable/Available/Entering/Ready/Active/Paused/Completing/Completed/Exiting machine; no owner per state |
| C | Pattern | PARTIAL (data only) | QuestPattern Classify | No pattern gameplay interface; correct to defer, but socket undefined |
| D | Question | INTENTIONALLY ABSENT | Ban guards CT-P31/P36 | Define struct-only contract when 2nd pattern needs it; NOT now |
| E | Content | PARTIAL | QuestData/ObjectiveData + Catalog + MathLearningEntries (lobby/counting_garden/number_bridge) | Single-consumer (math_counting); no 2nd-consumer proof |
| F | Interaction | PARTIAL | IClickTarget + ClickRouter + ClickToMove | No IInteract*, no world/UI/NPC/activity/camera channel split, no transition-lock, no duplicate-click guard spec |
| G | Validation | PARTIAL | QuestManager AdvanceOnSeen/Spoken | Find/Speak-Great+ only; no Select/Count/Match/Sort/Order/Listen contract |
| H | Feedback | PARTIAL | Voice prio + ducking + presenter reactions | No reusable channels (object/NPC/audio/anim/VFX/world) — per-activity ad hoc |
| I | Completion | PARTIAL | QuestCompletedEvent sole path + bloom/token adopt | Event-only→adopt patched (J6/J7); no visual+audio+reward+save+re-entry single contract |
| J | Reward | PARTIAL | QuestRewardService once + bloom pop | friendship 0 placeholder; no claim/reward-moment staging |
| K | Progression | MISSING | VocabularyProgression unwired | No multi-activity unlock/next-activity contract |
| L | Entry/Exit | PARTIAL | WorldNavService idempotent + gates + return arch + warp | No re-arm policy (J4 lesson O2 open); spatial siblings still walk-in legacy (MarketBootstrap:506) |
| M | Save/Resume | PARTIAL | LocalSave JSON frozen | questsDone not replayed; world nav in-memory (locked, OK) but activity-partial state undefined |
| N | Re-entry Adoption | PARTIAL (patched) | J5/J6/J7 pins P40E/F/G | No generic adopt interface; each object hand-rolls bus+adopt |
| O | Audio/Speech | EXISTS (locked) | AudioDirector + Speech stack | No defect found; do not touch |
| P | Camera/Framing | PARTIAL | SmartCamera Follow/Interaction/Cinematic + FramePointFor | Beats are magic vectors (MarketBootstrap:389,828,856; Math arrival:655); no scene-authored anchors |
| Q | World-space UI | PARTIAL | Bubble anchor + HUD chip/tunnel/cover | No UI area/raycast/ownership/lifecycle spec; J1/O1/O3 raycast debt open (mic/recording dialogs) |
| R | Presentation/Feedback | MISSING as contract | Per-builder inline coords | Zero hits for GameplayFocus/Prompt/Feedback/Reward/Exit/Camera/Presentation anchors (grep Assets: 0) |
| S | Telemetry/debug | PARTIAL | Journey logs + pathStatus/remain telemetry lesson | No per-activity evidence spec; bake/nav verified ad hoc (J8 lesson P3) |
| T | Error/recovery | PARTIAL | Transition LastError + spam-safe + fail-soft save | No failure/recovery states (interrupted audio/speech/record/quit/restart) |

## 4. Presentation / spatial audit (hard requirement §8–§9)

- Zones exist but pads-as-zones legacy partially fixed by B1R7 (r26 island, Kenney fence/crops/bridge, tunnel, sky, rim). Foreground/midground/background are comments only — no tags/lighting spec; Math sky-island vs Main lawn diverge (no shared palette/material standard beyond URP/Lit flat + PropKit.Map).
- Landmark hierarchy / path readability improved (entry/return arches, spokes+loop, beads, stones, sunflower) but P43 pins geometry, not composition. Human checklist (arrival/hub/garden/bridge/loop/feel) NOT DONE — `VISUAL_QA.md` explicitly headless + build/boot only.
- NPC staging: MiloAnchor/MiaAnchor + consts exist; Tess host bare→golden host (phase302) but no NPCAnchor/CameraAnchor/PromptAnchor scene objects. Camera beats hardcoded vectors, not anchors → §9 FAIL (magic coordinates scattered in gameplay code).
- Feedback VFX: bloom pop + token carry + voice; no small-VFX/object-react/world-react channel standard. Completion = boolean+bloom, not EVENT staging (no reward-moment anchor).
- World-space UI rules: J1 fix (raycastTarget=false on card/title/body/status) is click-through patch; O1 (mic offer stays open), O3 (recording dialogs raycast) still open → UI-as-blocker risk persists.
- Spatial standard (§18): Entrance/Orientation/Activity/Reward/Exit zones nominally present in Math (entry arch/lobby/garden/bridge-clearing/return arch) but no common principles doc; object density 311/400 budget (P42) but no landmark/path/hierarchy rubric.

## 5. Socket matrix (§20) — can we plug 10 patterns tomorrow?

| Capability | Current | Reusable? | Missing | Owner (today) | Evidence |
|-----------|---------|-----------|---------|---------------|----------|
| Find | yes | PARTIAL | 2nd-consumer proof | QuestManager + MathHost | journey + P40 |
| Count | demo-only | NO | validation contract | MathWorldBuilder counting frame | P36/P41, 1 word `one` |
| Collect (carry/bring) | yes | CANDIDATE | generic carry interface | MathTokenCarry | J7 adopt |
| Match | no | NO | pattern socket | — | — |
| Sort | no | NO | pattern socket | — | — |
| Drag/Drop | no | NO | input + validation | ClickRouter (click only) | no drag path |
| Deliver | partial (bring) | CANDIDATE | generic deliver | MathQuestDirector | narration-only |
| Build | no | NO | pattern socket | — | — |
| Path/Cross | walk-only | NO | activity boundary contract | Bridge deck walkable | bridge water walkable (F-audit) |
| Memory | no | NO | pattern socket | — | — |
| Select | partial | NO | choice contract | DistractorChoice | single use |
| Listen | partial | PARTIAL (locked) | question-audio hookup | AudioDirector | voice by ID |
| Speak | partial | PARTIAL (locked) | assessment hookup | SpeakingExercise | Great+ only |
| Feedback | ad hoc | NO | channels | presenters | no standard |
| Completion | event-only | PARTIAL | staged moment | QuestCompletedEvent | J6/J7 patches |
| Save/Re-entry | partial | NO | replay + adopt iface | LocalSave (frozen) | questsDone not replayed |
| Camera | constrained | PARTIAL | anchors | SmartCamera | magic vectors |
| Presentation | per-builder | NO | anchor registry + standard | MathWorldBuilder | grep anchors 0 |

Verdict on matrix: only Find + Carry/Bring are CANDIDATE-reusable (need 2nd consumer). Everything else is NO/PARTIAL. **Do not claim reusable without 2nd-consumer evidence** (`REUSE_MATRIX.md` agrees: travel PROVEN, host/director/carry/bloom/beacon CANDIDATE).

## 6. GitHub research (§2) — classify, no dependency soup

- `AnisKaram/Unity-Modular-Game-Architecture` (Unity 6, VContainer, FSM, EventBus, IInteractable, drag/drop) — REFERENCE ONLY. Technique to steal: pure-C# FSM states + type-safe bus (we already have typed bus; do not import VContainer).
- `Studio-23-xyz/InteractionSystem` (MIT, InteractableBase + InteractionState Inactive/Active/Paused + sub-interaction stack + conditions + hold) — ADAPT (ideas only): state enum + CanBeInterrupted + cancellation-token for speech/audio interrupt; do not import singleton manager.
- `PSEMO/Base3DUnity` (IPersistable/IState/IMover/IPoolable/IInteractable, node StateMachine, SO-driven) — REFERENCE ONLY. Too big; steal interface-segregation principle only.
- `SunnyValleyStudio/Tips-for-writing-cleaner-code` (IInteractable + IAccessRule composition, SRP DoorInteractable vs DoorController) — ADAPT: split interaction trigger vs activity controller; IAccessRule for Available/Unavailable gating.
- `DhafinFawwaz/Unity-Reusable-FSM-With-Editor` (Core<T,U> reusable FSM + editor) — REFERENCE ONLY. Our lifecycle needs explicit ownership, not generic Core.
- `carolinaaraujo00/mini-majestade` (Unity 2022.3 preschool language therapy, Whisper on-device, per-exercise EventManager + SO data + bootstrapper, FSM) — ADAPT (closest domain match): per-activity EventManager scope + SO content + cue hierarchy (escalating auditory/visual hints) for gentle-correction; do not import Whisper (we have Azure/Fallback/Mock locked).
- `UF-College-of-Education/AR-Expeditions` (literacy AR, SharedCore→4 experiences, QuestItem/QuestionHandler/NPCController) — REFERENCE ONLY: SharedCore owns Player/Camera/Audio/Save, experiences own content — validates our Core-vs-Micro split (§5).
- `WordBaby` (toddler word app, MVC-lite View+Controller, edit-time DI, engine data-binding) — REFERENCE ONLY: View/Controller pairing for bubble/HUD; no runtime container (matches our Installer-only-new rule).
- Camera: `richani-yvan/CameraTools` (asymptotic follow + POI dynamic framing, no deps, 2D/3D) — ADAPT: weighted POI framing for GameplayFocus + Importance lerp for reward moment; `EggyStudio/Camera.FocusPoint` (ray/sphere focus distance, Unity 6000+, optional Cinemachine) — REJECT for now (DoF/physical-camera overkill for preschool); `alejandrodlsp/Unity-Camera-Follow` + Cinemachine Follow docs — REFERENCE ONLY (we keep SmartCamera, add anchors not Cinemachine).
- World-space UI: Unity UGUI World-Space Canvas docs (`docs.unity3d.com`, 800x600 → scale meters) + `BAPCon/UiToolkitWorld` — ADAPT: small world-space canvases with explicit scale math + raycast ownership; REJECT UIToolkit-world dependency.
- `SST-Systems/Interaction-Objects` (pickup/carry/throw, URP outline, wall-layer auto-drop) — REJECT (FPS physics-hand overkill); steal wall-layer + auto-release idea for carry-out-of-bounds only.
- Preschool/educational (`pferreirafabricio/mini-kids` BNCC) — REJECT (no architecture substance).

Rule: small proven techniques over frameworks. No new packages. C#9, no record/with, Installer-only-new, feature asm MUST NOT ref Bootstrap (`AGENTS.md`/`ARCHITECTURE.md` locks hold).

## 7. Findings triaged (P0–P3)

**P0 (blocker/data-corruption/runtime): none open** — J1–J8 fixed + pinned (P40E/F/G, P43K), journey clean, suite green. Deferred O1–O6 are not P0.

**P1 (foundation blocker → pattern duplication/rework if not fixed):**
- P1-1 No Activity Lifecycle machine + owners (§6/B). Every future pattern will hand-roll states → duplicate transition bugs (J4-class).
- P1-2 No Micro-World/Presentation anchor registry (§5/§9/R). Magic vectors in MarketBootstrap/MathWorldBuilder → every pattern reinvents framing; presentation stays prototype.
- P1-3 No generic Re-entry Adoption interface (§16/N). J5/J6/J7 each hand-patched; next activity repeats stale/reset bugs.
- P1-4 questsDone persisted but never replayed into QuestManager (M). Re-entry deterministic claim false for multi-activity future.
- P1-5 No gate re-arm policy (J4 lesson O2). Rapid/duplicate/re-entry interactions unsafe.
- P1-6 Interaction channels unseparated (world/UI/NPC/activity/camera) + no transition-lock (F). J1-class UI-blocker + click-through bugs will recur.
- P1-7 No validation/feedback/completion/reward staged contracts (G/H/I/J). Patterns will hard-wire pattern↔question (violates §4).

**P2 (important quality gaps, fix only if required for standard):**
- P2-1 Camera beats hardcoded; no CameraAnchor/FocusAnchor objects (P).
- P2-2 World-space UI without area/raycast/ownership/lifecycle spec; O1/O3 open (Q).
- P2-3 Completion not an EVENT (no reward-moment staging) (I/J).
- P2-4 bake/nav verified ad hoc; no per-activity telemetry spec (J8 lesson P3) (S).
- P2-5 Spatial principles undocumented; FG/MG/BG + landmark/path rubric missing (§8/§18).
- P2-6 Progression/next-activity undefined (K).
- P2-7 F9 dead district still built; Math-branch vs one-loader firewall contradiction (A).

**P3 (polish/future):** material/lighting shared spec, VFX library, ambient/interaction sounds, memory/perf measurement, recording-compat, dead-space cellulose, sunflower/garden storytelling, bridge-water collision honesty.

## 8. Acceptance check (§28) — current status

- A Architecture: FAIL (no micro-world concept files, no lifecycle, ownership partial).
- B Gameplay: FAIL (entry/exit partial; validation/feedback/completion/reward not contracts).
- C State: FAIL (adopt patched not generic; questsDone not replayed).
- D Presentation: FAIL (no standard, no anchors, magic coords, human review NOT DONE).
- E Spatial: PARTIAL (zones exist, principles missing).
- F Technical: PASS for happy journey (build Succeeded, 534 green, stable) but P1s open.
- G Human: NOT DONE (explicitly required before PASS).

## 9. FINAL VERDICT: BLOCKED

Foundation is **technically stable but not complete**. Happy-path journey passes; reusable foundation does not exist yet. Declaring PASS now would repeat the exact failure mode §0 warns about (gameplay works, presentation prototype) plus guarantee pattern-duplication rework.

**Exact blockers to clear before PASS:** P1-1 through P1-7. P2s only as required for the standard (P2-1..P2-4 recommended in-scope; P2-5..P2-7 docs + firewall fix).
**Out of scope this phase (§23/§24):** Match/Sort/Memory/Drag-drop patterns, Universal* managers, audio/speech/save-format rewrites, new packages.
**Next:** implement minimal explicit contracts justified by math_counting + 2 clearly-expected consumers (Count/Collect/Find-N): ActivityLifecycle + Micro-World anchor registry + Interaction channels/lock + Validation/Feedback/Completion/Reward staged contracts + generic Adopt/Replay + gate re-arm — then build → standalone → HUMAN REVIEW (§22 Q1–Q10) → regression → handoff docs (§27 remaining 8 files).

*End of audit. No code changed.*
