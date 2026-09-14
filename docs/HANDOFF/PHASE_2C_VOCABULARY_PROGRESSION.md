# Phase 2C Vocabulary Progression

## Status

PASS — minimal learning foundation landed: progression is now a
deterministic, data-driven, testable layer over the existing MasteryFSM.
No new state machine, no adaptive AI, no persistence system, no staging
changes. One honest wart found and fixed during validation (JsonUtility
auto-instantiation — see §Validation Evidence).

## Scope

Learning-data / progression only (§22 held: no Ball staging, no Stager,
no NPC/camera/audio-pipeline work, no save system, no speech work, no
bulk content — 0 new quests, 0 new words beyond 2A's ball).

## Baseline

- Phase 1 LOCKED; 2A landed (75/75 at 2B); 2B PASS WITH OPEN ITEMS, no P0.
- 2B assigned 2C two things: (1) no runtime rewrite needed — confirmed,
  none made to any frozen system; (2) the `simplify_path` decision —
  decided below (§`simplify_path` Decision).
- Git was CLEAN at start (937d2d9).

## Vocabulary Inventory (re-read live, not copied)

- TOTAL 51 / ACTIVE 16 / PASSIVE 35 — confirmed from disk (2B numbers hold).
- Categories: food 4, object 3, color 2, verb 3, number 2, social 2,
  world 35 (= every passive carries the seeder default; no semantic
  curation exists for passives — P2).
- Difficulty: ALL 51 are `1`. No difficulty progression exists in data.
- Quest targets (8, all resolve): apple, ball, big, one, please, red,
  teddy, thank_you. NOTE: big + teddy are PASSIVE words targeted by
  quests — active≠required-target is already fuzzy in shipped content.
- Active never targeted by any quest (10): bag, banana, basket, blue,
  bread, bring, find, help, milk, two.
- Dialogue text references 13 vocab words (substring match, metadata only).
- Audio mappings: every active has normal+slow (binaries still 2D scope).

## Existing Learning Events (traced to publishers — §2/§14 audit)

| Event | API | Production publishers | Tests only? | Persisted? |
|-------|-----|----------------------|-------------|------------|
| Seen | LearningService.ReportSeen | QuestManager.AdvanceOnSeen (EVERY WordSeen, any word incl. ball) | no — LIVE | no (session memory) |
| Spoken | ReportSpoken(level) | NONE — zero production WordSpokenEvent publishers (2B-confirmed, re-verified) | YES (CT-002/010, P07E) | no |
| Selected hit/miss | ReportSelected(hit) | NONE in production | YES (CT-010, P07C) | no |
| ContextUse | ReportContextUse | NONE in production | YES (CT-010, P07D) | no |

Consequences, stated plainly:
- Live progression today = Seen accumulation only (apple/ball exposures
  via the apple quest). Recognized/Produced/UsedInContext/Retained are
  UNREACHABLE in live play — full chains exist only in tests.
- NOTHING reads learning state in production (no GetStage/GetMastery
  callers outside tests). Progression is currently write-only telemetry.
- The `hit` bool on ReportSelected already splits attempt vs success at
  the API level (misses raise totals, never hits) — §14 satisfied by
  existing design, pinned by P07C.
- LearningService publishes NO events (comment says so); consumers poll
  GetStage/GetMastery. Kept as-is (no bus churn for a read-mostly model).

## Event vs Progression State

- EVENTS: Seen / Selected(hit|miss) / Spoken(level) / ContextUse —
  LearningService counters, forward-only accumulation, 5s meaningful-dedup
  for the engagement metric (CT-009).
- PROGRESSION STATE (derived): MasteryStage per word (Unknown → Exposed →
  Recognized → Produced → UsedInContext → Retained) + the 2C ranking
  (RecommendationReason per word). No new stage enum was created — the
  repo's terminology (ARCHITECTURE.md §5) is used verbatim per §5.
- A child can see/select-miss/reuse/speak/forget/re-see freely: counters
  never decrease, promotion re-checks every report, misses only delay
  (Recognized needs 4-of-5 hits), review is time-observed on read.

## Active vs Passive Semantics (audited, then pinned)

- ACTIVE = needs active recognition/production to complete gameplay;
  measured (recall, transfer), runs the MasteryFSM, appears in
  recommendation. PASSIVE = heard/seen in world/dialogue, no production
  required, never recommended (D4), promotable to active by authoring
  (`"active": true` — the documented scale path, exercised by ball).
- NOT easy/hard (difficulty is uniformly 1 — nothing to reinterpret),
  NOT known/unknown (that's MasteryStage's job).
- Fuzzy edge, recorded not hidden: big + teddy are passive yet quest
  targets. The policy handles it (passive ⇒ PassiveAuthoring regardless
  of targeting — P07G pins teddy explicitly). Quest authors should prefer
  active targets; validator does NOT forbid passive targets (existing
  quests would fail — backward compat wins, §20).

## Progression Model

MasteryFSM (existing, thresholds from ARCHITECTURE.md §5: 3 seen /
4-of-5 selects / Great+ 2-of-4 speaks / 1 context / review) + 2C
`VocabularyProgression` (pure static, C_Content): ProgressionView per word
+ `RecommendNext` deterministic ranking + `ReasonFor` transparency.
No QuestManager knowledge of scores (already true; no change).
No vocabulary-database knowledge of UI/camera (already true; no change).

## Progression Rules

Buckets: ReviewDue → ActiveTarget → ActivePractice; ties by (stage rank,
introOrder, word id). Excluded with reasons: BlockedPrereq, MasteredResting
(Retained + review not due = the "familiar enough" exit, §7-Q3),
PassiveAuthoring. D1–D5 DESIGN DECISIONS documented in code (no live
evidence for a fuller model; all deterministic + unit-pinned):
D1 prereq satisfied iff stage ≠ Unknown; D2 Retained-rest exit; D3 due
review outranks (PRODUCT.md transfer KPI); D4 passive never recommended;
D5 no randomness.

## Recommendation Policy

`RecommendNext(vocabs, masteryOf, questTargetWords) → List<string>`.
Inputs are data + read-only state access; quest targets arrive as a plain
id set (policy knows no quest types). Output stable across identical
inputs (P07I runs it twice). Reasons queryable per word (P07F/G/H).

## Quest ↔ Vocabulary Contract (traced, §13)

GOOD, no change needed: objectives carry raw target ids → typed WordId at
the ContentDatabase/QuestManager boundary → compared as Values everywhere
(QuestManager.cs:100,134; catalog vocabulary from targets; policy outputs
raw ids that re-parse to themselves — P07J round-trips this). Display
strings appear only in content metadata (display.en), voice lines
("Find the apple!"), and HUD — never as lookup keys. Classification:
SAFE CONTENT (voice/HUD strings are narration, not identity).

## `simplify_path` Decision — OPTION A (WIRE TO THE CONSUMPTION BOUNDARY)

Semantic, fixed: L4 auto_simplify is a PRESENTATION-difficulty directive
(show `simplifyReduceChoices` options + `simplifyDemo` one-step demo),
NEVER a learning-state change. Wired as far as 2C may legally go:
QuestEntry already parsed both fields → QuestContentEntry now carries
them → Validate pins them (reduce > 0, demo true, mirroring the Content
freeze) → P07K proves parse→entry→validate intact. The world-side
consumer (choice-count + demo staging) lands with 2F staging — it cannot
be built in 2C without violating §22, and inventing a consumer now would
be the exact speculative wiring 2B forbade. No ambiguity remains about
what the field MEANS or WHERE it will be read; only the reader is future.

## Session Boundary

LearningService = process-lifetime session memory (fresh instance reads
all-Unknown — P07A pins this, documenting the boundary in code). Quest
restart / wrong answers never wipe state (counters only grow; HintService
resets hints, not learning — verified by read). Scene reload / app restart
LOSES learning state: LocalSave round-trips full WordMastery (format
READY) but the sole production Save/Load caller is NpcVoiceProfileSelector
(voices only); nothing saves/loading learning or quest progress, and
LearningService has no state-import API. No save system built in 2C
(forbidden §22 + unneeded for the policy). Persistence = Phase 4 scope.

## Persistence Boundary

Format ready (LocalSave WordEntry covers stage+counters+review+ticks).
Wiring absent (see above). 2C adds no persistence code, no import API
(YAGNI — no reader exists yet). When Phase 4 wires it, the policy needs
no changes (it reads through `masteryOf`, source-agnostic by design).

## Adaptive Difficulty Boundary

No adaptive system built (§12 held). Foundation laid without overreach:
presentation difficulty travels as data (simplify directive per quest,
hint ladder thresholds untouched in HintService); learning state never
leaks into QuestManager; EASIER/NORMAL/HARDER remain future policies over
(distractor count, hint level, repetition via recommendation) — all readable
from existing records. QuestManager stays score-blind (verified: no new
references).

## Schema Changes (backward compatible, §20)

- VocabEntry += `introOrder` (default 999) + `prerequisites` (default []).
- vocab JSON: optional `"progression": {"introOrder": N≥1, "prerequisites":
  [...]}`. 46/51 files omit it and load identically (P07A pins defaults;
  full suite green = compat proof). Present block must set introOrder ≥ 1
  (validator-enforced; closes the JsonUtility auto-instantiation ambiguity
  where absent blocks read as order 0 — the 2C validation wart, fixed).
- Stamped sample (§19, 5 words): apple 1, ball 2 (prereq apple), red 3,
  one 4, please 5. Rationale: apple→ball mirrors the R9 pre-exposure +
  2A chain; red/one/please are the remaining active quest targets.
- QuestContentEntry += simplifyReduceChoices + simplifyDemo (parsed data
  that already existed — no JSON change).
- Docs: GAME_DESIGN active list + ball (truthfulness; counts still owned
  by JSON per that doc's own rule).

## Runtime Changes

NONE to frozen systems (QuestManager, LearningService, HintService,
presenters, camera, HUD, audio untouched — verified by diff). New code is
pure/additive: VocabularyProgression (policy), 3 DTO/parser fields,
2 catalog fields + validation. Only behavioral surface: ParseVocab output
shape (additive fields) + stricter Validate (new pins).

## Tests (CT-P07, 11 tests — matrix §17 A–K covered; L = suite green)

A new-word initial/defaults · B Seen→Exposed staging · C wrong-only
selects never promote (attempt-vs-success) · D chain to Retained
(service level) · E spoken accounting (SERVICE-CONTRACT ONLY, no live
publisher — stated in the test header) · F due-review outranks /
rested-retained rests · G passive never recommended (teddy pinned) ·
H ball gated on apple, then first (neediest-first) · I determinism
(double-run equality) · J stable id identity round-trip · K simplify
parse→entry→validate. Full matrix L: suite green (below).

## Validation Evidence

- EditMode: BEFORE 75/75 → AFTER 86/86 PASS (75 frozen + 11 new, 0 failed).
  First 2C run went 83/86 (3 failures, ONE root cause: JsonUtility
  auto-instantiates absent nested blocks — absent `progression` read as
  order 0, corrupting defaults and ranking). Fixed via the ≥1 convention
  + validator rule; re-run 86/86. Lesson recorded (§Known Limitations).
- Content validator: PASS (authoring, 16/35/51, 38 lines + new
  progression-block rules).
- Backward compat: 46 progression-less files parse to defaults (P07A +
  suite green); quest/dialogue JSON untouched and loading.
- No duplicate vocab ids (P07J set build + CT-008); no dangling
  quest→vocab refs (CT-008 + P06F + P07J); no dangling dialogue refs
  (CT-008); no malformed progression entries (validator + P07A).

## Known Limitations (NOT PROVEN / honest gaps)

- Live learning = Seen only; Selected/Spoken/ContextUse have no live
  publishers (2B finding, unchanged). Policy rules over those stages are
  service-contract-proven, NOT play-proven.
- Nothing reads recommendations live (no consumer until 2F+ quest
  selection/chaining). The policy is wired to nothing — by design in 2C.
- Retention is time-infrastructure only (NextReview/ForceReviewDue);
  no spaced-repetition engine — DEFERRED Phase 4 (§8).
- Thresholds: code says review 24h, ARCHITECTURE.md §5 says 7 days;
  code says ContextUse ≥ 1, doc says "1/3 situations". BOTH drifts
  recorded as P2 (P2-7 below) — NOT changed: frozen-file behavior with
  zero live reachability is not 2C's to rewrite on aesthetics.
- Passive semantics are seeder-defaulted (35× category world); no
  curation attempted (P2).
- Learning effectiveness (does any of this teach better?): UNPROVEN —
  Phase 4 validation scope (§18).

## Deferred Items

- DEFERRED Phase 4: retention engine, persistence wiring, effectiveness
  validation, adaptive difficulty, import API for saved mastery.
- 2D: audio binaries (mappings ready, P06E).
- 2E: NPC roster consumes QuestEntry.npc; bubble SetIcon.
- 2F: QuestStager, provider switch, ball staging + standalone proof,
  simplify world consumer, quest chaining runtime.
- P2-7 (new): threshold doc/code drifts (24h vs 7d; ContextUse 1 vs 1/3)
  — resolve when retention goes live (Phase 4) or when 2F stages a
  speak/context leg, whichever first. Document of record is code until
  then (tests pin code behavior).

## Files Changed

Production (additive/small):
- ADD Assets/C_Content/VocabularyProgression.cs (policy + reasons + view)
- Assets/C_Content/ContentDtos.cs (+2 fields)
- Assets/C_Content/ContentDatabase.cs (optional block parse)
- Assets/C_Content/QuestContentCatalog.cs (+2 simplify fields + pins)
- Content/vocab/{apple,ball,red,one,please}.json (progression sample)
- tools/validate_content.py (progression rules)
- docs/GAME_DESIGN.md (active list + ball)
Tests:
- ADD Assets/Tests/EditMode/CT-P07_Progression.cs (+.meta)
Docs:
- ADD docs/HANDOFF/PHASE_2C_VOCABULARY_PROGRESSION.md (this file)

## Git Evidence

- Baseline: CLEAN (937d2d9). Commit: `phase2c: vocabulary progression
  foundation` (no tag — no phase lock claimed). Post-commit: CLEAN.

## Final Verdict

A. Progression model: MasteryFSM (existing, doc-backed thresholds) +
   derived ranking (new, deterministic). No new state machine.
B. CONTENT metadata: id/category/difficulty/skills/tags/forms/phonetic/
   prefab/image/audio/active/introOrder/prerequisites/quest objectives/
   hint levels/simplify directive/dialogue lines.
C. PLAYER learning state: counters + stage + NextReview (session memory)
   + derived reason/rank (computed, never stored).
D. Seen: QuestManager.AdvanceOnSeen (LIVE). Selected/Spoken/ContextUse:
   LearningService APIs with NO live publishers (tests only).
E. Tests-only events: Selected, Spoken, ContextUse (+ ForceReviewDue).
F. Wrong answers cannot promote: misses raise totals without hits;
   promotion needs hits — pinned P07C. No wrong-promotion path exists.
G. Active/passive semantics: defined, pinned (P07G incl. passive-target
   teddy edge), promotion = authoring act.
H. Deterministic next-vocabulary: YES (pure function, double-run pinned,
   no randomness anywhere).
I. `simplify_path`: KEPT and WIRED TO THE CONSUMPTION BOUNDARY as a
   presentation-difficulty directive (entry carries + validates it);
   world consumer lands in 2F. Rationale: semantic now fixed and
   provable; building the consumer in 2C would violate scope §22.
J. Stable IDs: YES end-to-end (typed WordId; no display-key lookups;
   round-trip pinned P07J).
K. QuestManager: UNCHANGED (diff-verified; stays score-blind).
L. 2D unblocked: YES — mappings proven, collection dynamic, binaries
   are the only outstanding item.
M. 2E unblocked: YES — npc field + placement/icon gaps itemized as 2E
   work (P1-5, roster).
N. Deferred to Phase 4: retention engine, persistence wiring, saved-state
   import, effectiveness validation, adaptive difficulty.
O. UNPROVEN learning effectiveness: everything about real children
   (recall quality, retention, transfer, pronunciation) — policy correctness
   is proven, teaching power is not, and no test count claims otherwise.
