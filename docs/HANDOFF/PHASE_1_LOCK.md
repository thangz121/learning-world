# PHASE 1 — FORMAL LOCK

> Phase 1 locked as production baseline; remaining items are tracked
> as non-blocking polish backlog.

This file is the formal Phase 1 lock record. The project's living
session log remains at the repo root (`HANDOFF.md` §§1–17); this file
is the checkpoint, not a duplicate of it.

## PHASE

Phase 1

## STATUS

LOCKED

## LOCK DATE

2026-09-14

## UNITY VERSION

6000.6.0f1 (ProjectSettings/ProjectVersion.txt,
m_EditorVersionWithRevision `6000.6.0f1 (f7f8ed4d1e24)`)

## BUILD STATUS

PASS (with note below on what "PASS" means at lock time)

- Last fully-evidenced standalone: **R9v4 FINAL** (2026-09-13/14):
  `Succeeded errors=0`, managed payload DLLs verified fresh
  (exe-stub mtime is NOT a freshness signal — lesson 34; freshness is
  judged by `LWE.World/Brain/Bootstrap.dll` mtime), survey COMPLETE,
  0 TIMEOUT, 0 exceptions, boot check 70s+ with 3×FACE_OK.
- Lock-session final regression build: executed as part of this lock
  operation (see "Final regression" section below). If that build's
  log differs from R9v4, the differing build id governs and this file
  must be amended — the lock never floats above evidence.
- Earlier chain (all Succeeded + COMPLETE + 0 exceptions):
  R4, R6 builds C/D/E, R7, R8, R9 v1–v4.

## EDITMODE

**69/69 PASS** on the locked tree (zero temp files).

Note on the mission brief's "52/52": that number was the **R4**
baseline. The chain after R4 added tests without regressing:

- R4: 52/52 (golden-template closure)
- R7 (CT-P04 softlock gates + cursor): 58/58
- R8 (CT-P04G marker + CT-P04H proximity + CT-P02 update): 60/60
- R9 (CT-P05 9 tests): 69/69

69/69 is the governing number. 52/52 is retained here only as
provenance.

## REAL STANDALONE

PASS

- R9v4 FINAL survey: pre-talk tap (crate kept, carried kept,
  HUD "Talk to Milo", marker on apple) → talk (crate kept) →
  ball PICKUP (pedestal empty, carriedBall=True, 0 wrongs) →
  ball-bring wrongs=1 (click arrival + proximity echo-guard证明:
  one bring = one wrong) → find (objIdx=1) →
  hands-full legacy-wrong wrongs=2 → bring completed=True,
  wrongs stay 2 (apple echo also guarded) → `Great job!` +
  Mia celebrate + label crisp + arrow cursor visible.
- Player.log: 0 exceptions. Zero survey TIMEOUTs on the final code
  (C/D/E + R7 + R8 + R9v4).
- Earlier real-player chain preserved: spawn → Milo → talk → quest →
  target → wrong → retry → correct → celebration → completion,
  all through genuine raycast clicks (never editor-only).

## REAL PLAYER FLOW

PASS — full loop verified live on standalone:

SPAWN → MILO → TALK → QUEST → TARGET → WRONG → RETRY →
CORRECT → CELEBRATION → COMPLETION

including the R7 pre-talk softlock recovery path and the R9
pickup/wrong/swap/echo paths.

## VISUAL BASELINE

PASS WITH KNOWN NON-BLOCKING POLISH ITEMS (listed below).

Validated on standalone screenshots (60+ inspected across the
chain; R9v4 macros + beats re-verified):

- CHARACTER: grounding contact + shadow on all 3 rigs (SOLE2
  bone-bind 0.001–0.037, photos show contact, no daylight gap);
  face artifact cleanup (no white ellipse, no skeleton/debug
  geometry); sculpt marks read as doll brow/blush; Milo blue mat
  + Mia pink hat identity; readable faces/feet at gameplay scale.
- WORLD: layered ground/path/material variation, hedge boundary
  (R9: fence → hedge, no more cage look), trees, outer green,
  stall north, Mia in front of her shop, flower reward nook SW
  (compact ~0.7m, off every quest path), apple-vs-ball separation
  (red vs blue + crate vs pedestal, 3.22m).
- GAMEPLAY: navigation, NPC interaction, dialogue, wrong/retry/
  correct/completion, `Great job!`, label gating (Mia null
  pre-talk), HUD discipline (compact chip + whisper + fade),
  dialogue/found/emotional framing, cursor arrow + hover marker.
- CAMERA: SmartCamera + FramePointFor authored poses (celebrate
  ensemble, wrong close-up, stall view, macros); obstruction
  handling preserved.

## KNOWN ISSUES (NON-BLOCKING POLISH BACKLOG)

Recorded explicitly. None of these break gameplay, block
interaction, prevent completion, or make template reuse
unreliable — that is why the lock proceeds with them open.
Do NOT reopen Phase 1 merely because an improvement here is
theoretically possible.

1. `Walk_Carry` animation not wired (prop apple rides the hand;
   carry locomotion reuses the base walk clip). Carried since §5.
2. `PregenSeeder seeded 0` in player: shipped mp3s fall back to
   silent/offline path; audio architecture untouched, packaging
   fix is a separate work item. Carried since §5. Side effect:
   Milo's "Bring it to Mia!" redirect is audio-only, so a silent
   build shows no visual redirect (consider a visual redirect if
   audio stays mute — R7 follow-up).
3. Milo label slight crop in spawn follow-view (HUD-over-world
   family, accepted since R5V/R6).
4. "Hear it again" replay chip can cover Mia's feet in hint
   close-ups (same accepted HUD-over-world family).
5. Milo/Mia foot grounding: locked lifts (Player 0.005, Milo
   0.01, Mia 0.02 VisualRoot-local) read as contact on all final
   photos, but stride mid-flight frames remain inherently
   inconclusive from stills (normal locomotion flight, not a
   defect) — any future "improvement" here needs stance-photo
   evidence, never BakeMesh numbers (BakeMesh is BANNED from
   probes: 40–50x double-scale liar, lessons 18/21/26).
6. Walking stride/foot motion: player walk reads coherent
   (push-off / mid-stride / stop, speed 2.2 m/s fix); Milo/Mia
   are stationary BY DESIGN (shopkeepers, no locomotion clip —
   adding walk is a new system, out of Phase 1 scope).
7. Quest hint: R9 shell/icon enlargement + pulse + AnchorFor
   contract is the locked hint language; further polish is
   aesthetic, not functional.
8. Quest visual lifecycle: single-quest slice terminal state is
   `Great job!` + flowers; there is no quest 2 by design
   (multi-quest chaining is Phase 2 work).
9. Animation/emotion timing: celebrate ensemble beat verified
   in-beat + settled + durable-Happy post-beat; beat-window
   arithmetic (settle vs 3.2s) stays a manual pre-computation
   for every future beat (lesson 28).
10. Ambient liveliness (blink/breath/glance/hop) exists in code;
    stills cannot prove motion — a future motion pass needs
    video/frame-series evidence, not a reopen of this lock.

Reopen gate (ANY of these reopens Phase 1 regardless of the
list above): gameplay broken; player cannot understand the
interaction; NPC/object interaction fails; character severely
floating; quest cannot complete; camera prevents gameplay;
severe visual artifact breaking presentation; Phase 2 template
reuse unreliable.

## GOLDEN TEMPLATE (FROZEN BASELINE)

Do NOT rewrite these in Phase 2 unless a real requirement
proves the architecture cannot support the new content.

- CHARACTER: CharacterRoot gameplay authority; VisualRoot
  presentation separation; CharacterPresentation (Set/Pulse/
  LookAt/BlinkNow, MeasureBandFront, SoleSeatFor bone-bind);
  face construction; material strategy (smoothness 0.5,
  metallic 0); grounding strategy (single VisualRoot lift
  source); Animator structure; interaction presentation;
  Golden Character Standard v1 (`docs/GOLDEN_CHARACTER_STANDARD.md`).
- NPC: proximity gating; facing gating; rising-edge behavior;
  click-to-NPC navigation; stop-distance logic (arrivalRange
  1.5m, apple 2.0m, Mia bring 1.5m — one handover point);
  authored interaction framing; R7 quest gates; R9 echo guard.
- CAMERA: accepted direction; SmartCamera; FramePointFor;
  dialogue/wrong/celebrate framing; obstruction handling.
- QUEST: state flow; dialogue-led instruction; visual target
  presentation (bubble + AnchorFor + icon language); wrong/
  correct feedback; retry; celebration; completion; lifecycle
  (crate/carried/bubble/glow/flower rules incl. R6 active-gate
  + R9 swap/echo rules).
- UI: compact HUD chip + whisper + adaptive fade; contextual
  objective; no face obstruction; no giant center tutorial;
  cursor arrow + down-arrow hover marker; 2x name labels at
  2.35m with facing contract (CT-S01M).
- WORLD: layered ground; playable path (warm tan 0.76/0.60/
  0.40); environment boundary (hedge); NPC spatial identity
  (Milo mat, Mia stall); target/distractor separation;
  stylized lighting/material baseline (sun 68°, shadow 0.65,
  ambient 0.68/0.71/0.75).
- ARCHITECTURE: CompositionRoot/GameInstaller (only `new`
  site); typed IDs/events (WordId/NpcId/QuestId, typed bus);
  existing quest/audio/NPC-interaction architectures;
  Cloudflare Worker TTS abstraction (never direct Unity→Google
  TTS); IAudioDirector + pregen local audio + Azure STT +
  offline fallback.

## BASELINE SNAPSHOT

- Tag: `phase-1-locked` (and alias `v0.1-phase1-locked` pointing
  at the same commit — see git log).
- Commit contains ONLY production files (Library/Temp/Logs/Obj/
  Builds, generated build output, R*Survey/R*Build/Editor temp,
  local machine state, screenshots, caches all excluded by
  `.gitignore` + lockdown discipline).
- Rollback promise: if Phase 2 goes wrong, `git checkout
  phase-1-locked` returns to this baseline.

## TEMP CLASSIFICATION AT LOCK

PRODUCTION (kept): everything under `Assets/` except below;
`Content/` JSON + audio manifest; `tools/` validators/seeders;
`docs/` + root `HANDOFF.md` + this file; `Packages/`,
`ProjectSettings/`.

TEMPORARY (removed, 0 files remain): `R9Survey(.meta)`,
`R9Build(.meta)`, `Assets/Editor(.meta)`, temporary
InputSystem asmdef ref (reverted), survey drivers, grounding
probes, screenshot drivers, sidecar logs.

Reusable diagnostics intentionally kept: NONE in-tree (recipes
in `HANDOFF.md` §§6/8/9 rebuild the ~20-line `W1Build.Run` +
survey driver from git history when needed; `W1CapWatch.ps1`
stays outside the repo at
`C:\Users\ASUS\AppData\Local\Temp\opencode\`).

## FINAL REGRESSION (LOCK SESSION)

Route: SPAWN → MILO → TALK → QUEST → TARGET → WRONG → RETRY →
CORRECT → CELEBRATION → COMPLETION.

- EditMode on locked tree: 69/69 PASS.
- Clean standalone: Succeeded errors=0, payload DLLs fresh.
- Boot check: alive 65s+, 3×FACE_OK, 0 exceptions.
- Survey: COMPLETE, wrongs counted once per bring, completed=True,
  `Great job!`, crate/carried/bubble null post-quest, HUD clean,
  camera beats hit, no missing materials, no debug objects.
- Build identifier + survey log name: recorded in the lock
  commit message and in `HANDOFF.md` §18 (appended by the lock
  operation). If this paragraph outlives its build, the
  HANDOFF entry governs.

## HANDOFF CONVENTION NOTE

The project historically keeps ONE living handoff at the repo
root (`HANDOFF.md`). This lock record lives at
`docs/HANDOFF/PHASE_1_LOCK.md` per the Phase 1→2 mission order;
the root file gains a short §18 pointer (not a fork). Future
Phase 2 records (`PHASE_2_QA_CHECKLIST.md`, `PHASE_2A_HANDOFF.md`)
live beside this file.
