# Phase 2F World Staging + Ball Quest

## Status

PASS — w1_mia_ball is WORLD + RUNTIME + REAL PLAYER PROVEN: talk → find →
wrong → retry → bring → complete → celebrate, chained live after the apple
quest in one standalone session, through the same production contracts as
apple. No QuestStager built (verdict A, justified below). No frozen-system
rewrite (three surgical frozen-file diffs, each proven necessary).

## Objective

Close the 2B gap: ball from DATA-ONLY to second real staged quest, proving
CONTENT → QUEST → NPC → WORLD OBJECT → INTERACTION → AUDIO →
PRESENTATION → COMPLETION is a repeatable pipeline, not an apple-only demo.

## Baseline

- Phase 1 LOCKED; 2A/2C/2D PASS; 2B PASS WITH OPEN ITEMS; 2E PASS (103/103).
- Git CLEAN at start (8874fc3). 2E inputs consumed: NpcRoster, SetIcon,
  shared tint, roster voices, 2D audio binaries + key contract.

## Apple Golden Staging Reference (traced, §4)

Apple path, component by component: crate mesh + `Interactable(wordId
apple/take_apple/mia, 2.0m)` + `ProximityDiscovery(quest w1_mia_apple)` on
the APPLE SPHERE; `ApplePresenter` (crate breathing/glow, hide-on-take,
carried mini, quest gate); ClickRouter arrival → `Interact()` →
`WordSeen(apple)` → Bootstrap glue → `AdvanceOnSeen` (find) + HUD +
Milo praise; carry state in Mia → click/proximity `CompleteBring` →
`ReportAction(Bring,apple)` → HUD + bubble hide + Milo celebrate +
flower reveal + carried hide. Verdict per part: state machine GENERIC;
staging (prop/presenter/narration/HUD/icon/reward-visual) APPLE-COUPLED —
exactly the 8 couplings 2B listed. Ball mirrors the path; couplings below
are what 2F changed vs deferred.

## Ball Content Audit

Quest JSON (find_ball→bring_ball, pattern B, hints freeze, simplify 2/+,
friendship 10, flower_pot) + ball vocab (active, object, audio mapped,
order 2 ← apple) + manifest inst_07/ok_05 (mia_v1) + binaries
(ball_normal/slow, inst_07/ok_05, all L2HIT-proven in 2D). Content needed
ZERO changes in 2F — the 2A/2D foundation held.

## Ball World Staging

- `BallCrateAnchorPos` (5.5, 0, 3.2): east lawn, off the Milo-Mia axis
  (real FIND walk), 2.6m from the distractor pedestal with different
  presentation (low crate vs tall pedestal), 4.6m+ from the apple crate,
  5.6m from Mia (no trivial/ambiguous taps), open grass, own nav carve.
- Crate (tan cube) + blue quest sphere; `Interactable(ball/take_ball/
  mia, 2.0m)` + `ProximityDiscovery(w1_mia_ball)` on the CRATE ROOT
  (not the sphere — low-angle rays hit the wide crate body and
  GetComponentInParent searches UP only; sphere-mounting silently dropped
  crate-body clicks to movement, found live twice — lesson 48).
- NEW `BallPresenter` (crate glow, hide-on-take, carried mini, quest gate,
  complete-hide) mirroring ApplePresenter semantics.
- Distractor ball untouched: inert during the ball quest (its apple-quest
  binding reads Completed → silent), still live during apple (R9 intact).

## Target Registration

`builder.Ball` (Interactable) + `builder.CrateBall` (visual) +
`builder.BallDiscovery`, wired in BuildServices like apple. Target word
flows as typed WordId everywhere; no display-string lookups.

## Player Interaction

Unchanged model: click → auto-walk → arrival (1.5m click / 2.0m proximity,
established constants kept — §12 held, no retuning). Router + discovery
both fire the same `Interact()` → `WordSeen(ball)` path (idempotent by
QuestManager design).

## Pickup / Carry

REAL carry system (no fakes): crate sphere hides same-frame the
`CarriedQuestBall` mini parents to the fist-bone hand anchor (one hand,
one item; swap rules with apple preserved). Walk_Carry audit (§14):
NO Walk_Carry animation state exists anywhere in the repo — the player
walks the base cycle with the mini riding the hand, IDENTICAL to the
locked apple carry. Verdict: acceptable (lock-standard parity), NOT
rewired — a carry clip is a new animation system, deferred as polish
(extends Phase 1 lock item, unchanged severity).

## Mia Receiver

The 2F core fix (frozen file, proven necessary): `CompleteBring` reported
`_appleWord` unconditionally and `OnMiaClicked`/proximity treated every
ball as wrong — the ball quest could NEVER complete live. Now both entry
points resolve correctness from (carried word × active quest):
appleQ+apple / ballQ+ball → CompleteBring (reports the CARRIED word);
appleQ+ball / ballQ+apple → WrongBring. R9 apple-quest behavior preserved
exactly (P10B pins all four combos; live ball run: wrongs evolve 0→1→1).

## Dialogue

Ball narration is entry-driven from manifest DATA (no new hardcoded
sentences): quest start → "Ball please!" (inst_07, mia voice per roster),
find → "Great! Ball!" (ok_05), complete → Milo.Celebrate (word-free,
shared). Texts mirrored as consts PINNED to the manifest by CT-P10C
(runtime catalog loading is stager scope, explicitly not 2F).
REMAINDER (P1-2, honestly open): Milo's instruction/replay lines stay
apple-worded ("Find the apple!" on talk-click during ball quest) — the
HUD ("Find the ball") + bubble carry the correct instruction; entry-driven
Milo narration awaits the stager-era voice routing. Downgraded from
blocker: the quest is fully completable and correctly instructed visually.

## Audio Resolution

2D contract consumed, zero new audio work: ball_normal/slow L2HIT live
(`VOCABPLAY word=ball mode=Normal cache=True`), inst_07 requested with
exact manifest text + mia_v1 (log-observed `DIALOG` lines), SpeakAsync
params exactly the P08B-pinned 1.0/1.0/Clear keys. Player.log: zero TTS
failures, zero decode failures, zero exceptions. Audible-by-ear stays
NOT PROVEN (2D boundary, unchanged).

## Question Bubble

`SetIcon(WordId)` consumed on the production path: ball quest start →
ball icon (log `icon=ball`), photographed crisp blue-no-stem mid-quest;
apple icon photographed intact; round-trip restore logged. No ball
branches outside the 2E table.

## simplify_path Consumption (§27)

L0–L3 fully live in the ball run (idle tick → L2 "Come with me!" observed
mid-quest; L1 glow path implemented in BallPresenter; wrongs → ladder).
L4 `reduce_choices_to/demo_one_step`: carried + validated + pinned, but
the slice has NO multi-choice UI to reduce (single target + inert
distractor) — consumption NOT APPLICABLE, not faked. The directive will
meet its consumer with the first choose-type staged quest (pattern C,
future). QuestManager never sees scores (verified unchanged).

## Quest Visual Lifecycle

Pre: crate full, bubble hidden, HUD "Talk to Milo". Start: bubble ball
icon shows, HUD "Find the ball". Find: sphere hides (crate stays),
carried mini in hand, HUD "Bring the ball to Mia". Wrong: ladder +
moment, quest open, retry intact. Complete: `Great job!`, celebrate
framing, bubble hides, carried hides, flower ALREADY bloomed (apple's —
single effect shared, documented), friendship +10 (rewards service).
Post: no stale markers (crate stays empty like apple's; distractor home).
Next: none (ball terminal, `nextQuest=""` honored — no fake objective).

## QuestStager Decision — VERDICT A: NOT BUILT YET

Audited duplication with two examples in hand: ApplePresenter ≅
BallPresenter (~150 shared-shape lines; differ: word/quest consts, mini
builder, glow color, breathing) + staging wirings (prop/discovery/narration
/HUD/icon/carve per quest). Abstractionable? Partially — a CarryPresenter
base would save ~120 lines. Built? NO, for three stated reasons:
(1) the VALUABLE stager (entry→world binding) requires runtime content
loading — Content/ doesn't ship, CatalogQuestProvider is unwired, and
building loader+stager+binder is framework-scale, not "smallest possible";
(2) presenter-base savings are small vs re-verification cost on the LOCKED
apple path, and quest #3 (different verbs/effects) may reshape it again —
abstracting at N=2 risks the wrong seam (2B's own warning);
(3) 2F's mandate (second quest PROVEN) is met without it.
TRIGGER (explicit): third staged quest, or runtime JSON loading need —
then build the entry-driven binder, not a presenter base class. The
duplication map above is the design input. No mega-framework ever (§23).

## Runtime Evidence

Proof build Succeeded → foregrounded `-p2fball` run, router-arrival flow
after real walks (no state calls invoked — survey drives clicks/moves
only; all advancement via production event paths):
`Talk to Milo` → `Find the apple` → `Bring the apple to Mia` →
completed=True wrongs=0 → `Find the ball` → bubble icon=ball →
WRONG wrongs=1 (open) → `Bring the ball to Mia` → completed=True
wrongs=1 → `Great job!` → COMPLETE apple=True ball=True.
ZERO WAIT-TIMEOUTs, ZERO exceptions, ZERO audio failures. Audio lines
observed: full Milo apple flow + `Ball please!` + `Great! Ball!` +
ball vocab L2HIT. Shots: 12/12 written (spawn/talk/find/complete/
celebrate ×2 quests + ball-talk/bubble/wrong).

## Real Player Validation (§29 — router-level real clicks)

Method (lock-conformant): real player build, real scene/services, real
ClickToMove walks, clicks through `RouteHitForTests` (production arrival
logic), real proximity discovery, screenshots + log markers. NOT invoked:
no ReportAction/Advance/Complete bypasses (survey calls only clicks,
moves, waits, and reads). Success matrix §30: 22/22 — bootstrap clean,
no exceptions, quest starts via talk, Mia correct, objective visible,
icon correct, ball exists/findable/clickable, pickup + carry + navigate,
delivery resolves, wrong (empty-hand) cannot complete, correct completes,
reaction + reward + cleanup occur, no stale objective, no cross-trigger,
no timeout, no exception. Wrong-OBJECT live: NOT APPLICABLE in the
chained world (no wrong pickable remains post-apple — crate consumed,
distractor inert); covered by P10B integration on the same WrongBring
code R9 proved live. Stated, not smuggled.

## Apple Regression (§31)

The SAME run completes apple first with the NEW Mia/Bootstrap code:
talk→find→bring→complete, wrongs=0, HUD/bubble/celebrate intact,
crate lifecycle intact, distractor behavior intact (R9 paths preserved
by P10B + untouched DistractorChoice). VERDICT: no regression —
Ball PASS + Apple PASS in one session (§32 sequential architecture:
apple→ball chaining live, no chaining framework needed — OnTalk
next-quest rule suffices for the terminal pair).

## Tests (§38: 103 → 107, all green)

CT-P10 (4): production-path ball loop (BuiltIn providers — stronger than
P06D's test provider) · receiver 4-combo matrix · narration mirror pin ·
staging config geometry. Full suite 107/107 (frozen 69 + 2A/2C/2D/2E
suites intact). Validators: content + audio-manifest PASS.

## Build Validation (§39)

Proof build Succeeded (0 compile errors; errors=4 = known headless noise)
→ smoke COMPLETE → temp deleted → FINAL clean build Succeeded →
boot 3×FACE_OK + Seeded-0 steady state + 0 exceptions. No failure hidden
by filter (full marker log in §Runtime Evidence).

## Visual Acceptance (§34: 8 beats inspected)

Start (roster labels crisp) · ball-talk (HUD+bubble context) · ball-bubble
(BLUE ball, instant read, Mia/Milo/labels staged) · ball-wrong (objective
holds, no state corruption) · ball-find (HUD advances, crate empties,
carried mini) · ball-complete (`Great job!`, delivery pose at counter) ·
apple beats (parity with lock look). Grounding/shadows/scale/colliders:
no float, no clip, no duplicate ball, no helpers, no face-covering UI,
no stale markers, no awkward overlap. Golden look preserved.

## Performance / Complexity (§36)

Per-frame additions: BallPresenter.Update (early-out when no crate/glow —
same as apple), BallDiscovery poll (same as apple's). No new Find calls
per frame, no new subscriptions beyond the presenter pair (bus-disposed),
no coroutines, no DontDestroyOnLoad orphans, no global state (quest
routing stays in services). No optimization needed.

## Files Changed

Staging (additive): ADD BallPresenter.cs · MarketBuilder (Ball fields,
BuildBallCrate, services wiring, BallCrateCarve, SetIcon call site).
Quest content services (surgical): QuestManager + QuestRewardService
BuiltIn mirrors (+ball; ordering bug fixed — early-return swallowed the
new branch). Receiver (P1): MiaPresenter (quest-aware bring routing +
carried-word report). Narration (additive): MarketBootstrap (OnTalk
chaining, ball HUD/icon/entry-driven manifest lines, audio passthrough)
+ MiloPresenter (OnTalk every-click event) + GameInstaller (audio arg).
Tests: ADD CT-P10 (4). Docs: this file + HANDOFF lessons 47–49.
Frozen systems untouched: LearningService, HintService, audio pipeline,
camera core, CharacterPresentation, quest content JSON (zero changes).

## Known Limitations

- Milo instruction/replay voice stays apple-worded during ball quest
  (P1-2 remainder; visual instruction correct; entry-driven Milo routing
  is stager-era work).
- Single shared flower effect (ball reuses flower_pot; per-quest effects
  need the effects router — stager era).
- Live wrong-OBJECT delivery untestable in chained world (no wrong
  pickable remains; P10B + R9 precedent cover the code path).
- Audible-by-ear unproven (standing 2D boundary).
- Walk_Carry: no carry clip exists; base-walk + hand mini (lock parity).
- L4 simplify: carried, no multi-choice UI to consume it yet.

## Deferred Items

- Phase 3: third quest (stager TRIGGER), per-quest effects, narration
  routing, bulk content.
- Phase 4: retention/persistence/effectiveness, Played-vs-Audible.
- Standing P2: seed_content drift, threshold drifts, TestManifest P/S rows.

## Phase 3 Readiness

Phase 3 can stage quest #3 by COPYING the ball pattern today (prop +
presenter + discovery + narration consts + HUD + icon call ≈ 200 lines,
all mapped in this doc) — and MUST trigger the stager decision when doing
so (§QuestStager Decision). Vocabulary/audio/catalog/roster/progression
foundations need no changes for pattern-B quests.

## Git Evidence

- Baseline CLEAN (8874fc3). Commit `phase2f: world staging + ball quest`
  (staging + receiver + narration + tests + doc). No tag (no phase lock
  claimed). Post-commit CLEAN.

## Final Verdict

A. Ball a real world target? YES — crate + sphere + collider at
   (5.5,0,3.2), photographed, clicked, carried.
B. FIND live? YES (walk → arrival/proximity → hide + HUD advance).
C. PICKUP live? YES (crate empties same-frame the hand mini appears).
D. CARRY live? YES (fist-bone mini, navigated lawn → counter).
E. BRING live? YES (click + proximity paths, 1.5m counter handover).
F. Mia via QuestEntry.npc→Roster? YES — shopkeeper_mia resolves; no
   `if questId==` dispatcher anywhere (verified by read).
G. Icon via SetIcon(WordId)? YES — log + photographs, round-trip.
H. Dialogue via content pipeline? YES — manifest texts + roster voice
   through the 2D L2 contract (keys pinned, requests logged).
I. Audio L2HIT? YES — ball vocab cache=True live; zero fallback warnings.
J. Complete ONLY on correct ball→Mia? YES — matrix live (empty-hand
   wrong) + integration (all four combos).
K. Wrong object completes? NO — proven cannot (matrix + live open quest).
L. Lifecycle clean? YES — 5/5 transitions photographed/logged, no stale.
M. Apple regression? NONE — same-session apple PASS on new code.
N. Real duplication? YES — presenter pair + staging wirings (mapped).
O. Smallest abstraction? Entry-driven binder — DEFINED, NOT BUILT.
P. Stager needed? NOT YET (verdict A).
Q. n/a (A). R. Why not: loader missing + N=2 seam risk + mandate met
   without it; explicit trigger recorded.
S. simplify consumed at: L0–L3 live (ladder/glow/hints/voice); L4
   carried-unconsumed (no choice UI — N/A, not faked).
T. Frozen systems changed? Three surgical diffs (Mia receiver ×2
   methods, Milo OnTalk event, BuiltIn mirrors) — each necessary,
   tested, regressed. Nothing else.
U. Visual regression? NONE — ball beats match lock look; golden intact.
V. Real player ball end-to-end? YES — zero-timeout COMPLETE run above.
