# Phase 2B Quest Pattern Audit

## Status

PASS WITH OPEN ITEMS — the audit is conclusive, no P0 blocker exists,
no code was changed (nothing needed changing yet). The headline finding
is stated bluntly in §Final Verdict: the repo stages exactly ONE gameplay
loop end-to-end, and every production decision below follows from that.

## Scope

Audit only (§14: no bulk content, no world work, no redesign, no polish).
Inspected: QuestManager, QuestRewardService, HintService, LearningService,
MiaPresenter, MiloPresenter, Milo (voice), MarketBootstrap, MarketBuilder,
Interactable, ProximityDiscovery, DistractorChoice, ApplePresenter,
FlowerPotPresenter, WorldQuestionBubble, ClickRouter, Services/Events/Ids,
Content/quests (7) + vocab (51) + dialogues, all quest tests (CT-001..012,
CT-P01..06, CT-S01), both validators, Phase 1 lock docs, 2A handoff, and
the 2A diff (77c559f, 794 insertions). Every claim below traces to a
file:line. Uncertain items are marked UNKNOWN — none remain.

## Baseline

- Phase 1 LOCKED (2851717, tags phase-1-locked / v0.1-phase1-locked).
- Phase 2A landed (77c559f + 0202274 metas, tag phase-2-content-foundation).
- Git tree was CLEAN at audit start; still clean (this doc is the only
  addition — see §Files Changed).
- Frozen rule honored: zero production lines touched. All P1 items below
  are recorded for 2E/2F, not fixed here, with the reason stated each time.

## Quest Inventory

Shipped quest JSONs (Content/quests, all validator-PASS):

| Quest | Objectives | 2A Pattern |
|-------|-----------|------------|
| w1_mia_apple | find→bring (apple) | B BringToNpc |
| w1_mia_ball | find→bring (ball) | B BringToNpc |
| market_help_mia | find→bring→speak (apple) | E MultiStep |
| lost_teddy | find→bring→speak (teddy/thank_you) | E MultiStep |
| colors_counting | find→bring→speak (red/apple/one) | E MultiStep |
| please_thank_you | speak→give→speak | E MultiStep |
| big_or_small | find→select→speak | C ChooseCorrect |

Runtime staging in the world (MarketBuilder + MarketBootstrap):
ONE staged loop — w1_mia_apple only. No other quest is ever started
(MarketBootstrap.W1Quest `w1_mia_apple`, line 13), no other Interactable
exists (single apple crate, MarketBuilder.cs:415-424), no other receiver
exists (Mia), no other reward visual exists (flower_pot), no Speak/Select/
Give production path exists at all (see §Pattern Matrix).

## Quest Pattern Matrix

Gameplay-pattern rule applied (§4): two quests differ in pattern ONLY if
state machine / player action / success condition differ. Vocabulary,
object, NPC, dialogue, reward, animation, text differences do NOT split
patterns. Result — the 2A A–E taxonomy classifies DATA shapes; the STAGED
reality is narrower:

| Quest | Pattern (data) | Trigger | Player Action (live) | Success Condition (live) | Failure/Retry (live) | Reusable? |
|-------|---------------|---------|---------------------|--------------------------|---------------------|-----------|
| w1_mia_apple | B | talk Milo | walk→click/proximity find; carry→click/proximity bring | ReportAction(Bring,apple) | wrong→hint ladder+moments, ball hops home, retry intact | YES (proven R6–R9 + lock) |
| w1_mia_ball | B (data) | UNKNOWN — never started live | NONE — ball carry routes to WrongBring | NOT STAGEABLE (Mia.CompleteBring reports apple only) | inherits apple quest's ladder while staged as distractor | DATA yes / WORLD no |
| market_help_mia | E | never staged | speak step has no publisher | cannot complete in world | n/a | NO (data only) |
| lost_teddy / colors_counting | E | never staged | teddy has no prop; speak unwired | cannot complete | n/a | NO |
| please_thank_you | E | never staged | no Give receiver anywhere | cannot complete | n/a | NO |
| big_or_small | C | never staged | no ReportAction(Select) caller | cannot complete | n/a | NO |

| Pattern ID | Description | Existing Quests | Runtime Support | Data-driven | Hard-coded Dependencies | Production Ready |
|------------|-------------|-----------------|----------------|-------------|------------------------|------------------|
| B (Find→Bring, talk-gated, wrong/retry, celebration) | the golden loop | apple (live), ball (data) | FULL (router, discovery, Mia ×2 paths, hints, bubble, HUD, flower) | YES (P06A/B/D) | apple-word completion + voice + icon + reward (see §Hard-Code) | YELLOW — one word at a time, staging is manual C# |
| A (lone Find) | data shape only | none shipped | PARTIAL (find path works; no quest uses it alone) | classifier only | — | YELLOW (no staged example) |
| C (Select) | data shape only | big_or_small | NONE in world (Select advances only via tests; LearningService.ReportSelected exists) | classifier only | — | RED |
| D (Speak) | data shape only | in 4× E-quests | NONE in world — zero production publishers of WordSpokenEvent (only MarketBootstrap subscribes; only tests drive AdvanceOnSpoken) | classifier only | — | RED |
| E (MultiStep) | data shape only | 4 quests | NONE beyond B's two steps (Give/Select/Speak legs unwired) | classifier only | — | RED |
| Give | verb, no pattern | please_thank_you | NONE (no receiver) | enum only | — | RED |

Net: the system has ONE production pattern (B), FIVE data labels. This is
not a criticism of 2A — the taxonomy was the correct first step — but 2B
must not let the labels masquerade as staged loops.

## Content vs Gameplay Separation

Per quest, split by "quest #20 of the same pattern: DATA or C#?":

w1_mia_apple (the ONLY staged quest):
- CONTENT (data today): quest JSON, apple vocab, Milo/Mia dialogue lines,
  audio mappings, friendship value, HUD strings ("Find the apple"...).
- GAMEPLAY (C# today, all frozen): QuestManager transitions (generic!),
  router arrival, Interactable/Discovery find path, Mia carry+bring paths,
  proximity bring, echo guard, hint ladder, bubble show/hide, HUD chip,
  flower reveal, camera beats, cursor/marker.
- Quest #20 of pattern B needs: new JSON + vocab + dialogue (DATA) PLUS
  today — and this is the audit's core cost finding — a world prop
  (Interactable + discovery), a receiver path for the new word
  (Mia completes apple-only), a bubble icon (apple hardcoded), voice
  lines (Milo says "apple"), a reward visual (flower const), and HUD
  strings (Bootstrap apple filter). I.e. ~6 small C# staging edits across
  frozen files. The STATE MACHINE needs nothing; the STAGING needs hands.

w1_mia_ball: proves the state-machine half (P06D runs it through the real
QuestManager + rewards with zero logic changes) and exposes exactly the
staging half still missing. That is precisely what 2A claimed — no more.

Verdict on the §6 question: for pattern B, quest #20 is DATA at the
service layer and MANUAL STAGING at the world layer. The code path proof:
QuestManager.TryAdvance compares (action,target) against loaded objectives
— fully generic (QuestManager.cs:130-142); nothing in the file names any
quest, word, or NPC except the fallback mirror (see §Hard-Code).

## Hard-Code Audit

Searched all quest-related code for id/item/quest branches. Classification
per §7 (SAFE = config default; COUPLING = works-today-blocks-tomorrow;
DEBT = known-mirror; no GAMEPLAY COUPLING found — the state machine is clean):

1. MiaPresenter._appleWord + CompleteBridge→`ReportAction(Bring,_appleWord)`
   (MiaPresenter.cs:33,138) — CONTENT COUPLING, P1. The single line that
   makes ball-as-target unst stageable: any ball carry falls into
   WrongBring. Fix belongs to 2F alongside the ball staging (needs the
   second concrete receiver case to abstract correctly — generalizing to
   "current quest's bring target" on a sample of one staged quest risks
   guessing the 2E NPC shape wrong).
2. MiaPresenter._ballWord + swap rule (cs:34,179-185) — SAFE CONFIGURATION
   (this IS the R9 distractor design, unit+live proven). Becomes part of
   the 2F staging conversation (distractor list per quest), not a bug.
3. Milo.InstructFind "Find the apple!" + SetInstructionTarget index→line map
   (Milo.cs:60-62,78-82) + Bootstrap SetInstructionTarget(0)/(1) + HUD
   strings + AppleWord filter (MarketBootstrap.cs:13-14,122,140-145) —
   CONTENT COUPLING, P1 for 2F (narration must become entry-driven:
   instruction/praise/HUD text from QuestContentEntry.dialogueIds).
4. ApplePresenter consts W1QuestId + AppleWord (ApplePresenter.cs:14-16,
   100,111) — SAFE today (single staged quest; quest-gate is the R7
   softlock fix, must be preserved in any generalization). P1 for 2F:
   lifecycle (crate hide/carried/breathe/glow) must key off the STAGED
   quest's target, not the apple const.
5. FlowerPotPresenter.W1QuestId const (FlowerPotPresenter.cs:19,72) —
   SAFE today, P1 for 2F (per-quest completionEffect decision recorded
   in 2A handoff; one effect exists, don't invent a system for one).
6. Bubble hardcoded AskApple/AskStem/AskLeaf, no SetIcon API
   (WorldQuestionBubble.cs:120-147) — CONTENT COUPLING, P1 for 2E
   ("reusable: swap icon" comment overclaims; swap needs code today).
7. QuestManager.BuiltInQuestContentProvider (2 quests) +
   QuestRewardService.BuiltInRewardContent (2 rewards) (QuestManager.cs:164-185,
   QuestRewardService.cs:65-81) — ARCHITECTURAL DEBT, P2. Deliberate
   zero-IO fallback; superseded by CatalogQuestProvider (unwired — 2F).
   Do NOT delete (offline fallback has value; decide in 2F).
8. DistractorChoice/ProximityDiscovery questIdValue defaults
   ("w1_mia_apple") — SAFE CONFIGURATION (public fields, settable; single
   quest today). Unproven for multi-quest, not broken.
9. Milo/Mia _activeQuest defaults ("w1_mia_apple", overwritten by every
   QuestStartedEvent) — SAFE (bootstrap ordering covers it; both presenters
   re-key on QuestStarted — genuinely quest-agnostic receivers of the
   EVENT layer).
10. Tests using "apple" as example word (CT-001..012, P01..) — SAFE
    (test fixtures, not production coupling).

No `switch(questId)`, no item switch/case, no hidden scene dependencies
beyond the documented single-GO shell + Resources prefabs (MiloVisual/
MiaVisual — presentation-only, correctly separated).

## Data-Driven Audit

Simulation: "tomorrow, 10 new quests of pattern B (pickable find→bring)".

| # | Step | Class |
|---|------|-------|
| 1 | vocab JSON per word (schema known, validator guides) | DATA ONLY |
| 2 | quest JSON per quest (objectives/hints/simplify/reward) | DATA ONLY |
| 3 | 2 dialogue lines per quest in manifest (+count) | DATA ONLY |
| 4 | catalog entry (dialogueIds, distractors, next) + Validate | DATA ONLY (code exists) |
| 5 | provider serves them; QuestManager+rewards run unchanged | PROVEN (P06D) |
| 6 | pregen mappings (automatic via BuildPreGenList) | DATA ONLY |
| 7 | world prop: Interactable + mesh + discovery per target | CODE + PREFAB REQUIRED (MarketBuilder C#, frozen file) |
| 8 | receiver path for the new word (Mia completes apple-only) | CODE REQUIRED (frozen file) |
| 9 | carry visual + lifecycle per target (ApplePresenter apple-keyed) | CODE REQUIRED (frozen file) |
| 10 | bubble icon per target (apple hardcoded, no API) | CODE REQUIRED (frozen file) |
| 11 | Milo instruction/praise voice per quest | CODE REQUIRED (frozen file) |
| 12 | HUD objective strings per quest | CODE REQUIRED (frozen file) |
| 13 | reward visual per quest (flower const) | CODE REQUIRED (frozen file) |
| 14 | audio binaries (2D pack) | AUDIO REQUIRED |
| 15 | quest trigger/start wiring (Bootstrap starts apple only) | CODE REQUIRED (frozen file) |

Production friction, precisely: steps 1–6 are frictionless DATA (2A
delivered); steps 7–13 + 15 are EIGHT small staging edits in FROZEN files
per quest. That is the 20–50-quest answer for Q9: NO — not without either
(a) a staging binder that turns QuestContentEntry into world wiring, or
(b) accepting ~8 frozen-file edits per quest (rejected: permission +
regression cost per quest kills throughput). The binder is the minimum
abstraction (§Recommended Abstraction Boundary), and it must wait for the
ball staging (2nd example) per §9's two-example rule.

## Production Readiness

- Core state machine (QuestManager + rewards + hints + learning verbs):
  GREEN. Generic, typed, unit-proven across all 5 verbs; no quest/word/NPC
  coupling outside documented fallback mirrors.
- Pattern-B staging (world→service→world round trip): YELLOW. Proven for
  exactly one word; seven manual staging couplings listed above.
- Patterns A/C/D/E/Give staging: RED. No world path (speak has zero
  publishers; select/give have zero callers).
- Narration (Milo voice + HUD + bubble icon): YELLOW. Correct for quest 1,
  hardcoded to it.
- Rewards: YELLOW. State generic (any string); visual single-effect.
- Content pipeline (JSON→parse→validate→catalog→provider→pregen):
  GREEN at data level (P06A–C,E,F + validator PASS).

## Pattern Readiness Matrix

| Pattern | Verdict | Rationale |
|---------|---------|-----------|
| B Find→Bring (talk-gated, wrong/retry, celebrate) | YELLOW | one word staged+proven; data path proven for N words; staging manual |
| A lone Find | YELLOW | find path staged inside B; no standalone example — do not abstract alone |
| C ChooseCorrect | RED | no Select caller in world; learning verb exists only |
| D/MatchWord + Speak | RED | zero WordSpokenEvent publishers in production |
| E MultiStep | RED | legs beyond B unwired (Give/Select/Speak) |
| Give | RED | no receiver |

No GREEN is awarded: per §11, GREEN needs data-only production of the
NEXT quest, which steps 7–13 block. The YELLOWs are honest progress, the
REDs are explicit scope for later phases — not failures of 2A.

## False Reusability Findings (mandatory section)

1. **P06D "ball runs through QuestManager" ≠ staged.** Integration-test
   proof only. The live world cannot start, carry, or complete the ball
   quest (Bootstrap starts apple; Mia completes apple; ball carry = wrong).
   Status stays NOT YET RUNTIME-PROVEN. The 2A handoff says this; 2B
   re-confirms by code trace (the exact blocking lines are §Hard-Code 1–3).
2. **questIdValue configurability is unproven.** DistractorChoice/
   ProximityDiscovery accept any quest id, but nothing ever sets a second
   one and four sibling components key off consts. Configurable ≠ reused.
3. **simplify_path data is write-only.** Parsed (ContentDatabase.cs:110-111),
   pinned (CT-008), consumed by NOTHING (zero readers of
   simplifyReduceChoices/simplifyDemo). L4 exists in the ladder but the
   reduce/demo fields never reach any system. Either 2C+ wires a consumer
   or the fields should be cut — recorded P2, not cut here (validator
   freeze + cross-phase impact need 2C's learning-loop decision first).
4. **QuestEntry.npc is write-only.** Same shape: parsed, cataloged,
   validated, consumed by nothing (Bootstrap hardcodes both NPCs +
   labels). Needed by 2E (NPC roster) — keep, don't cut.
5. **Bubble "swap icon" is a comment, not an API.** No SetIcon exists;
   reuse requires editing BuildBubbleImmediate. 2E must add the API.
6. **CatalogQuestProvider is test-only today.** GameInstaller still news
   the default BuiltIn provider. The pipeline's runtime half is unwired
   until 2F — exactly as 2A documented, re-verified (GameInstaller.cs:42
   unchanged).
7. **Manifest audio mappings ≠ shipped audio.** 38 lines mapped, binaries
   outstanding (known 2D scope; CT-008 scope note already honest).
8. **Multi-quest Content (5 E/C quests) ≠ stageable content.** Data-valid,
   validator-green, runtime-dark (speak/select/give legs unwired). They
   must not be mistaken for a backlog of "ready" quests; each needs its
   legs staged (2F+).

## Test Coverage Audit

| Concern | Level | Evidence |
|---------|-------|----------|
| quest creation (data) | UNIT | P06A/B/F over real JSON |
| initialization | UNIT+INTEGRATION | StartQuest paths (CT-002, P06D) |
| objective progression | UNIT+INTEGRATION | CT-002, CT-S01, P06D |
| correct interaction | REAL PLAYER (apple only) | R6–R9 + lock survey; ball: UNIT only |
| wrong interaction | REAL PLAYER (apple) + UNIT (ball) | R9 survey; CT-P05 |
| retry | REAL PLAYER (apple) | R9/lock surveys |
| completion | REAL PLAYER (apple) + INTEGRATION (ball) | lock COMPLETE; P06D |
| reward state | UNIT+INTEGRATION | CT-003, P06D (friendship 20, flower_pot) |
| reward visual | REAL PLAYER (apple only) | flower photos R6–R9 |
| dialogue mapping | UNIT | P06A/B/E (ids resolve; text/audio mapped) |
| dialogue playback | REAL PLAYER (apple; silent-fallback known issue) | PregenSeeder seeded 0/1 observed |
| vocabulary learning flow | UNIT | CT-001/010 (Seen/Selected/Spoken/ContextUse) |
| audio mapping | UNIT | P06E, CT-008 |
| quest transition (apple→ball chain) | UNIT (nextQuest field) | P06A asserts next; NO runtime chaining exists |
| quest start gating | REAL PLAYER | talk-gate (lock survey START→TALK) |

Ball quest overall: DATA-PROVEN + INTEGRATION-PROVEN, NOT YET
RUNTIME-PROVEN. Quest chaining: DATA field only, no runtime — 2F scope.

## Quest Creation Cost

| Task | Current Effort | Why | Desired State |
|------|---------------|-----|---------------|
| New vocabulary | LOW | schema + validator guide; 10-min DATA task | same (GREEN) |
| New quest data | LOW | JSON + catalog + Validate; P06 proves | same (GREEN) |
| New dialogue | LOW | manifest + caps + compat rules enforced | same (GREEN) |
| New distractors (data) | LOW | entry.distractorObjects + Validate | same |
| New audio mapping | LOW | dynamic pregen; binaries in 2D | same |
| New object (world prop) | HIGH | MarketBuilder C# + mesh + discovery + hand anchor, frozen file | staging binder reads entry (2F) |
| New receiver path | HIGH | Mia apple-keyed completion, frozen file | entry-driven carry/complete (2F) |
| New carry/lifecycle visual | HIGH | ApplePresenter consts, frozen file | target-keyed lifecycle (2F) |
| New hint icon | MEDIUM | bubble rebuild, no API, frozen file | SetIcon(target) API (2E) |
| New narration/voice | MEDIUM | Milo/Bootstrap/HUD apple strings, frozen files | entry-driven lines (2F) |
| New reward visual | MEDIUM | flower const; single effect exists | completionEffect router (2F) |
| New quest trigger/start | MEDIUM | Bootstrap starts apple only | quest-chain start (2F) |
| New NPC | HIGH | bespoke presenter + visual + staging (Milo/Mia each ~350 lines) | Golden NPC template (2E) |
| New quest pattern (verb) | HIGH | needs world path (speak/select/give have none) | stage one pattern at a time (2F+) |
| New pattern (data label) | LOW | classifier + test | same |

## P0 Blockers

NONE. 2C (vocabulary progression — data/table work) and 2D (audio binaries
— tools/pack work) require zero runtime changes. No frozen file needs
touching before them. (If this section is empty, 2B did its job: the audit
found the bottleneck without inventing an emergency.)

## P1 Important Items (for 2E/2F, NOT now — each needs its 2nd example first)

- P1-1 Mia completion word-keyed (§Hard-Code 1) → 2F ball staging.
- P1-2 Narration word-keyed: Milo lines + Bootstrap HUD/apple filter
  (§Hard-Code 3) → 2F entry-driven narration.
- P1-3 ApplePresenter lifecycle consts (§Hard-Code 4) → 2F target-keyed.
- P1-4 Flower const (§Hard-Code 5) → 2F completionEffect router.
- P1-5 Bubble icon API (§Hard-Code 6) → 2E SetIcon.
- P1-6 CatalogQuestProvider wiring (GameInstaller default still BuiltIn)
  (§False 6) → 2F runtime switch (keep BuiltIn as fallback).
- P1-7 Quest chaining runtime (nextQuest field exists, no consumer;
  Bootstrap starts one quest) → 2F after ball stages.

Why not now: every one of these generalizes from exactly ONE staged quest.
The two-example rule (§9) is not bureaucracy — the ball staging WILL
reveal whether "current target" suffices or NPC/receiver roles need
separating, and guessing today risks a wrong abstraction baked into
frozen-adjacent code. Documented > premature.

## P2 Deferred Debt

- P2-1 seed_content.py drift (missing w1_mia_apple + ball + 7th quest;
  rerun destroys content). Regenerate-from-Content or delete in 2D.
- P2-2 simplify_path fields write-only (§False 3). 2C decides: wire a
  consumer or cut the fields (validator freeze changes with it).
- P2-3 QuestEntry.npc write-only (§False 4). 2E roster consumes it.
- P2-4 BuiltIn mirrors duplication (2 quests × 2 services). Keep as
  fallback; revisit when provider switch lands (2F).
- P2-5 TestManifest lacks P/S-series rows (dormant PR-only CI never
  checks; convention drift since CT-P01). Fix with the CI pass, not here.
- P2-6 PregenSeeder silent fallback (Phase 1 known issue #2) — voice
  playback evidence stays weak until 2D ships binaries.

## P3 Polish

Nothing in 2B scope. (Deliberately empty — §14 held.)

## Recommended Abstraction Boundary

When the ball staging lands (2F) and only then, build the smallest binder
that makes quest #20 data-only:

```
QuestContentEntry (data, exists)
  → QuestStager (NEW, Bootstrap-side, small):
      Interactable+Discovery prop binding (target word)
    + receiver registration (which NPC completes which word)
    + carry/lifecycle keying (replaces apple consts)
    + bubble SetIcon(target) + narration lines + HUD strings
    + reward effect routing + chain start (nextQuest)
  → existing frozen runtime (UNTOUCHED: router, QuestManager,
      hints, presenters' generic paths, camera, HUD chip)
```

Explicitly NOT to build: GenericQuestMegaSystem, UniversalObjectiveFactory,
interface-per-verb, NPC mega-system, speak/select runtime (no staged
example), reward-effect framework (one effect exists). DATA → small stager
→ frozen runtime. The stager earns its existence from TWO staged quests;
today we have one.

## What Phase 2C Can Assume

- JSON/vocab/manifest schemas frozen and validated (51/16/35/38).
- Catalog entry shape stable (fields won't be renamed under 2C).
- simplify_path/npc fields stay present (2C decides their fate — do not
  cut or consume them speculatively).
- No runtime changes needed for progression tables.

## What Phase 2D Can Assume

- Pregen collection dynamic (new words/lines flow automatically — P06E).
- learning_v1/en-US freeze + ≤6-word caps enforced by two validators.
- Binaries outstanding for ball_normal/slow + inst_07/ok_05 (only new).
- seed_content.py must not be rerun (P2-1).

## What Phase 2E Can Assume

- Golden Character Standard intact (Milo/Mia presenters untouched).
- Bubble placement contract AnchorFor reusable; icon API missing (P1-5).
- QuestEntry.npc available for the roster; labels/names hardcoded today.
- MiloPresenter event-rekeying is already quest-agnostic (good template).

## What Phase 2F Must Still Prove

- Ball world staging: prop, carry, Mia ball-completion, icon, narration,
  HUD, reward, chain start — each per PHASE_2_QA_CHECKLIST.md.
- Provider runtime switch + fallback behavior.
- Full standalone real-player ball run (the NOT YET RUNTIME-PROVEN item).
- Regression: apple loop intact, 75/75 green, validator PASS.

## Files Changed

This doc only (audit task; zero production lines touched — §2 held):
- ADD docs/HANDOFF/PHASE_2B_AUDIT.md

## Validation Evidence

- No code changed → compile risk nil; still verified per §16:
- EditMode full suite: 75/75 PASS (BEFORE 75 → AFTER 75, no delta).
- Content validator: PASS (authoring, 16/35/51, 38 lines).
- Phase 1 frozen 69/69: PASS (inside the 75).
- Phase 2A 6/6: PASS (inside the 75).
- Coverage: unchanged (no delta to reduce).

## Git Evidence

- Baseline at audit start: CLEAN (0202274).
- Commit for 2B: `phase2b: audit quest patterns and production boundaries`
  (doc only). No tag (per §17 — no phase-2 lock claimed).
- Post-commit status: CLEAN.

## Final Verdict

A. Can we create a new quest of an EXISTING pattern using DATA ONLY?
   AT THE SERVICE LAYER: yes — proven (P06D runs ball through the real
   QuestManager + rewards with zero logic changes). IN THE WORLD: no —
   eight staging couplings (§Data-Driven steps 7–13, 15) still need small
   C# edits in frozen files per quest. Overall: NOT YET for production
   throughput.

B. Can we create a NEW quest pattern without modifying core quest runtime?
   The core (QuestManager) already accepts all five verbs generically — no
   modification needed there. But a new pattern is only real when the WORLD
   stages its legs, and speak/select/give have no world paths (RED). So:
   data-label yes, staged-pattern no. The next pattern must be staged, not
   declared.

C. What is the minimum abstraction still required?
   One small Bootstrap-side QuestStager (entry → prop/receiver/lifecycle/
   icon/narration/HUD/reward/chain wiring), built during 2F against TWO
   staged quests. Nothing else.

D. What must NOT be abstracted yet?
   Everything listed in §Recommended Abstraction Boundary: mega-systems,
   verb interfaces, NPC system, speak/select runtime, reward framework.
   Each has at most one — mostly zero — staged examples.

E. Is the quest pipeline ready for Phase 2C? YES — progression tables
   need no runtime. (2C must decide P2-2/P2-3 fields' fate.)
F. Is the quest pipeline ready for Phase 2D? YES — mappings proven,
   collection dynamic; only binaries outstanding.
G. Is the quest pipeline ready for Phase 2E? YES with the noted gaps —
   P1-5 (icon API) and npc-field consumption are 2E's own work items.
H. Is the quest pipeline ready for Phase 2F? CONDITIONAL — the checklist
   and the P1 list define exactly what 2F must build and prove; the ball
   run stays NOT YET RUNTIME-PROVEN until then.
I. What remains unproven by real-player runtime validation?
   Everything ball (start/carry/complete/celebrate in-world), all
   speak/select/give legs, quest chaining, provider switch, reward
   variety, and voice playback audibility (silent-fallback issue #2).
   The apple loop remains the sole real-player-proven quest.
