# MATH_WORLD_DESIGN_AUDIT — P3.0.1.1 (Stage A/B, rewritten on HEAD 15ad8bb)

Date: 2026-09-21. Baseline: `main` = `15ad8bb` (tag `3.0.0.1` ancestor line),
MathScene pilot live. Method: source inspection (file:line), test-contract
inspection, static census, user-verdict history in HANDOFF §26–27. No code was
changed during this audit.

> Supersedes the first audit pass (written on the stale P3.0.0 tree before the
> pull). This version audits the REAL Math World that ships in `MathScene`.

---

## 0. What actually exists (verified inventory)

**Scene travel / lifecycle (P3.0.1 S1–S2)**
- `SubjectDefinition.Math.SceneName = "MathScene"`; the two other spatial
  subjects stay walk-in (`SubjectDefinition.cs:63`).
- `WorldTransition` machine + `ISceneOps`/`UnitySceneOps`; single load/unload
  call pair (CT-P34/P37/P38). Cover fade in the persistent HUD canvas.
- `GameInstaller.OnSubjectSceneLoaded` builds MathScene synchronously
  (`GameInstaller.cs:120-154`): finds `MathWorld` root, applies
  `MathWorldBuilder.WorldOffset (60,0,0)`, calls `builder.Build(nav, player)`,
  wires Tess + director + carry + bloom (`WireMathContent`, `:161-218`).
- `MarketBootstrap.TravelToSubjectAsync` (`MarketBootstrap.cs:561-665`): lock →
  HUD "Entering…" → fade → `EnterAsync` → verify entry → deactivate Main
  (builder+Milo+Mia+labels+guide) → router `boundCenter=WorldOffset`,
  `boundX/Z=MathWorldBuilder.BoundX/Z` → `WarpTo(entry)` → Follow → arrival
  beat frames Tess 2 s → HUD "Toán World" → fade in. Return path
  (`:706-757`) warps to cached Main position, restores bounds/HUD/camera,
  unloads MathScene.

**World (MathWorldBuilder, 670 lines)**
- Ground: cylinder r19 grass at y−0.05 + 2 meadow flats.
- Boundary: 16 hedge spheres on r18 (scale 2.2 × 1.2, colliders kept).
- Pads (flat colored discs, tops 0.015): lobby r4 periwinkle at (0,0),
  entry r1.5 tan at (0,−8), garden r3 mint at (−10.5,3), bridge r3 sand at
  (10.5,−3). Paths (tops 0.047): entry (0,−4), return (0,4), garden diagonal
  (−5.25,1.5) yaw, bridge diagonal (5.25,−1.5) yaw — all 1.6 m wide.
- Lobby: abacus landmark (blue frame + 6 beads) at (−2.6,−1.2); entry board
  (2 posts ±1.1 + beam y2.06 + 3 beads) at z=−7.
- Garden: 2 soil beds (−11.3/9.7, 3.8), pedestals 2-cube/3-cube at
  (−11.4/9.6, 2.0), pedestal 1 + gold `Interactable "one"` at (−10.5,0.7,2.0)
  with `MathBeacon`; hidden bloom root (3 gold spheres) at (−10.5,3).
- Bridge: flat blue stream box (10.5,0.012,−3) 5×1.4; 5 planks; 2 rail boxes;
  3 number blocks; 3 gold discs floating at y2.4; reeds at random-looking spots.
- Number row: 5 walkable pads + 15 gold pips, 1.6 m off the garden path.
- Shape trio, return arch (pillars ±1.25, beam, gold disc, trigger r1.6).
- Decor: pots/tufts/pebbles/garden blooms/chevrons/return flowers/3 backdrop
  blobs; Quaternius NatureKit: 6 trees + 4 bushes + 3 rocks + 6 grass = 19.

**NPC / quest / learning (S3–S4)**
- `MathHostPresenter` (384 lines): Golden rig (TessVisual = Worker_Female +
  shared NPC controller, Math-blue vest/gold hat), click + greet-once +
  0.75 m proximity bring + hint tick + wave/celebrate. No StoryMoment.
- `MathQuestDirector`: first talk starts `math_counting`, HUD
  "Find the one" → "Bring it to Tess" → "Math World"; Tess voice lines.
- `MathTokenCarry`: World→Carried→Consumed (bus-only), re-entry adopt.
- `MathBloomDisplay`: 3 hidden gold blooms pop on completion (×1.4 scale).
- `MathLearningEntries`: stable IDs `math.lobby`, `math.counting_garden`,
  `math.number_bridge`.
- Content: `math_counting.json` (find_one/bring_one, reward math_bloom);
  manifest math_01/math_02 (pack frozen at 40 lines).
- **Learning seam usage: one word ("one") only.**

---

## 1. Diagnostic findings

### F1 — The world still reads as a skeleton (critical, visual)
**Problem:** zones are flat colored discs on a 38 m lawn (periwinkle lobby r4,
mint garden, sand bridge); structures are raw primitives (boxes/cylinders/
spheres). The 3.0.2 round enlarged the land and added Quaternius nature, but the
compositional language did not change.
**Why:** the pilot was built as a functional skeleton; pads-as-zones and
primitive landmarks were the fastest readable implementation.
**Player impact:** a child sees a big lawn with colored circles and small block
toys, not a place. The user's own verdict "World Toán sơ sài, thiếu chất Toán"
(HANDOFF §26) led only to additive motifs (number row, shape trio, beads).
**Arch impact:** lobby/area grammar is the template future subjects copy; if it
is "pad + props", Thinking/English/VN inherit skeleton worlds.
**Scope:** shared template + Math content.
**Fix:** replace pad-as-zone with composed ground signatures (courtyard,
garden plot, brook banks), build zone landmarks, paths with thresholds, and a
decor rhythm (cluster→gap→cluster). See redesign §4–§9.
**Risk:** object budget (CT-P42C <320) and corridor pins (CT-P42B) — compose
within them or raise deliberately.
**Verify:** before/after screenshots at 4 fixed viewpoints + census.

### F2 — No landmark hierarchy (critical, visual)
**Problem:** nothing in Math is tall or readable at distance: the abacus is
~1.2 m, the entry board ~2.2 m and 8 m behind spawn, the return arch ~2 m; the
only tall objects are generic Quaternius trees scattered at the rim, and the
floating gold discs over the bridge (y2.4). The child cannot answer "where am
I / where is the interesting thing" without walking and reading.
**Why:** landmarks were added as motifs, not as orientation hierarchy.
**Player impact:** no memorable Math image; no pull toward destinations.
**Arch impact:** `SubjectDefinition.Landmark` is an enum; the actual landmark
strategy is ad hoc per builder.
**Scope:** shared (landmark grammar) + Math content (which landmark).
**Fix:** 3-tier landmarks: entry arch (door language), lobby Counting Frame
(1.9–2.4 m, gold/blue), garden Counting Tree (3.4 m + bead fruit), bridge
crossing + far-bank stone circle. Distinct silhouettes, visible from spawn.
**Risk:** camera obstruction pull-in; keep beams ≥2.2 m and `ignoreFromBuild`
(proven lesson).
**Verify:** spawn screenshot (landmarks legible), walk-line screenshots.

### F3 — No spatial grammar: no threshold, choice, loop or reveal (critical, level design)
**Problem:** spawn is the lobby center (user-requested); the entry board at
z=−8 is never crossed; garden and bridge are straight diagonal strips from the
center; the return arch sits behind the spawn. There is no sequence
ENTRY→ORIENTATION→DISCOVERY→CHOICE→ACTIVITY→REWARD.
**Why:** zones were placed on axes for implementation convenience.
**Player impact:** the child can see everything at once; nothing invites
approach; travel is empty lawn-walking.
**Arch impact:** no reusable path/threshold grammar for subjects.
**Scope:** shared.
**Fix:** keep spawn center (user rule) but build a readable hub: courtyard
ring with four destination mouths (entry arch north, garden west, bridge east,
return south); paths become the loop lobby→garden→north arc→bridge→lobby with
width changes and small threshold arches/markers; decor frames each mouth.
**Risk:** moving zones breaks pins; Batch 1 composes around existing centers
first, relocation is a later deliberate change.
**Verify:** waypoint walk with screenshots at each threshold; new layout tests.

### F4 — The "Counting Garden" is not a garden (critical, learning environment)
**Problem:** the garden is a pad with two *empty* soil beds and three cube
pedestals; there is no fence, no crops, no path inside, no landmark, no
stand-spots. The only countable object is the gold "one" cube.
**Why:** skeleton scope; pedestals encoded 1/2/3 abstractly.
**Player impact:** the place does not teach or suggest counting; it is a
display of blocks.
**Arch impact:** area grammar ("pad + identity dressing") proven only at
minimum; nothing to reuse for real subject areas.
**Scope:** Math-specific content + candidate shared area grammar.
**Fix:** enclose the plot with a low wattle fence (one opening from the lobby
path, one toward the loop), fill beds with countable crops (3 carrots, 2
pumpkins, 4 sunflowers, 5 berries), add a garden gate arch, a Counting Tree
with 5 bead-fruit, number stones along the garden path, and a reward nook.
**Risk:** fence meshes bake (bake reads render meshes) — openings must be
≥1.6 m and checked by nav telemetry.
**Verify:** CT-P42 corridor samples + new fence-opening test + walking run.

### F5 — The Number Bridge is not a bridge (critical, world)
**Problem:** a flat blue box (5×1.4) with 5 planks on top; water is walkable
(collider stripped, no carve); rails are two plain boxes; no banks, no reeds
at the deck, no far-bank destination; three floating discs at y2.4 have no
readable meaning.
**Why:** skeleton scope; "stream reads water" was a color change only.
**Player impact:** no anticipation, no crossing feeling, no reason to go east.
**Arch impact:** crossing grammar unproven; future river/road features copy
nothing.
**Scope:** Math-specific + candidate crossing kit.
**Fix:** brook course with pebble banks and reed clusters, water carve except
the deck gap (crossing matters), proper boardwalk (stringers/posts/low rails)
with 5 count posts, a far-bank Number-Stones circle (1–5 pips) and a small
reward nook. Keep pinned object names.
**Risk:** carve errors sever nav (P3.0 lesson: verify by telemetry, not by eye).
**Verify:** pathStatus/remaining across the bridge both ways.

### F6 — Host staging is functional but bare (NPC staging)
**Problem:** Tess stands at (2.2,0.8) on open grass (no mat, no nook, no
props, no backdrop control); no `WorldQuestionBubble`; no interaction camera
beats in Math; only the arrival beat frames her.
**Why:** Golden body and quest wiring were the S3/S4 priority.
**Player impact:** meeting the host is less special than meeting Milo/Mia; the
"talk to me" affordance is weaker.
**Arch impact:** `MathHostPresenter` is a CANDIDATE pattern; staging isn't
part of it yet.
**Scope:** Math-specific staging; candidate shared host-nook pattern.
**Fix:** host nook (mat + 2 planters + basket prop), clean backdrop bush
cluster, bubble anchor using `WorldQuestionBubble.AnchorFor` with a counting
icon spec, click/find/complete camera beats (short, like Main).
**Risk:** framing a hidden Main anchor (already learned: no StoryMoment in
Math; use Math-space beats only).
**Verify:** approach/interaction screenshots; bubble/label non-overlap.

### F7 — Reward and quest geography are disconnected (gameplay)
**Problem:** the quest target sits ~11 m from Tess; after the child returns
the gold cube to Tess, `math_bloom` fires in the *garden* — off-screen. HUD
returns to "Math World", a dead end. Re-entry resumes silently.
**Why:** bloom consumer was bound to the garden root for implementation
simplicity.
**Player impact:** the world-change reward is never seen; no celebration
moment in frame; no "next discovery".
**Arch impact:** reward visibility is a shared UX concern (`world_change`
banking is proven; consumers are per-subject).
**Scope:** shared UX + Math content.
**Fix:** Batch 3 — completion beat: Tess celebrates + camera frames the
garden/tree (or a lobby reward object appears), and a "next" hint points to
the bridge stones. Keep banking contract.
**Risk:** camera beats must use Math-space anchors (no StoryMoment).
**Verify:** completion run screenshots + log.

### F8 — Learning coverage is a single word (learning design)
**Problem:** only `one` is interactable; the environment contains 1–5 numbers
(pips, pedestal cubes, beads, blocks) but nothing binds 2–5, comparison,
sequence or quantity. `one`/`two` vocab are active but **not approved**
(audio not generated); `three`–`five` do not exist.
**Why:** pilot scope + frozen 40-line manifest.
**Player impact:** the Math world does not teach math; it decorates it.
**Arch impact:** subject worlds need a content-readiness gate before activity
design.
**Scope:** Math content / pipeline.
**Fix:** Batch 3 needs a content sidecar (vocab 1–5 + approved audio + dialogue
lines + counting icons) before activity expansion. Until then the world must
at least expose named anchors for every countable.
**Risk:** content/audio outside repo control; TTS degrades gracefully today.
**Verify:** validator ship-gate + in-game audio.

### F9 — Dead/duplicate Math district in MarketScene (foundation)
**Problem:** `MarketBuilder.Awake` still calls
`SubjectWorldBuilder.BuildShell/BuildDecor` for **all** subjects
(`MarketBuilder.cs:112,123`; `SubjectWorldBuilder.cs:56` iterates
`SubjectCatalog.All`), so MarketScene still contains the old Math road, old
district gate, medallion, abacus, return arch and decor — while Math now
travels to MathScene. Two "Math" identities, duplicated draw calls, reachable
dead content.
**Why:** scene-based travel was added without retiring the spatial district.
**Player impact:** a child who walks east in Main meets a second, emptier
"Math" area; visual inconsistency.
**Arch impact:** every future scene-backed subject will duplicate itself
unless the builder honors `SceneName`.
**Scope:** shared foundation.
**Fix:** in `SubjectWorldBuilder`, skip road/district/playground/return for
subjects with a non-empty `SceneName` (keep the hub gate + signpost); update
CT-P31F/P33/P39/P43E expectations deliberately (gate stays, district retired).
**Risk:** pins and hub layout; do as a named batch with tests updated in the
same commit.
**Verify:** suite + MarketScene census before/after.

### F10 — Math special-casing inside shared Bootstrap/Installer (architecture/reuse)
**Problem:** `MarketBootstrap` hardcodes `MathWorldBuilder.WorldOffset`,
`BoundX/Z`, `EntryWorldPos`, `HostWorldPos` and
`to.Id == SubjectIds.Math`; `GameInstaller` handles only `"MathScene"` and
`WireMathContent`; `OnSubjectSceneLoaded` builds by root name `"MathWorld"`.
**Why:** Math is the first scene-backed subject; no generic profile existed.
**Player impact:** none today; future subjects can't plug in.
**Arch impact:** contradicts "no per-subject loader/branch" firewall
(SUBJECT_WORLD_FOUNDATION §5). Thinking would require editing shared code.
**Scope:** shared foundation.
**Fix:** move scene-backed travel data into data (e.g.,
`SubjectDefinition.SceneName` + per-subject travel profile or world-owned
provider resolved through an interface), keep the machine generic; CT-P36G
already pins "no Math literals in shared contracts" — extend it.
**Risk:** touching the proven travel path; keep the exact same flow and
failure semantics, only parameterized.
**Verify:** CT-P34/P37/P38 + new generic-path tests.

### F11 — Boundary is porous and repetitive (safety/visual)
**Problem:** 16 hedge spheres on r18 leave ~4.9 m gaps; ground extends to
r19; router bounds are ±20 — the child can walk between hedges onto the outer
rim (void edge) and the boundary reads as repeated identical spheres.
**Why:** boundary was visual dressing, not a designed wall.
**Player impact:** weak sense of enclosure; possible worry at the world edge.
**Arch impact:** shared boundary grammar missing.
**Fix:** compose the rim (bush clusters + rocks + occasional tall trees) and
close walkable gaps (colliders or carves at foot level) so r18 reads as a real
hedge; align ground/bounds/hedge radii.
**Risk:** navmesh changes at the rim; telemetry check.
**Verify:** boundary walk test + new enclosure test (no walkable nav ring
outside r18.5 — assert via carve geometry, EditMode-practical).

### F12 — World transition has no audio identity (audio/narrative)
**Problem:** entering Math = fade + HUD text. No chime, no host line, no
ambience. A pre-reader gets no signal that a new world began.
**Why:** S3A focused on honesty and cover.
**Scope:** shared beat + per-subject content.
**Fix:** Batch 2/3 — arrival cue (SfxId or Tess line) through `IAudioDirector`;
optional low-key ambience later (budget/loop ownership by Agent D).

### F13 — Return affordance in MathScene is unnamed (wayfinding)
**Problem:** the 4 spatial subjects got gold discs + "Về" labels (CT-P43E),
but the MathScene return arch has **no label** (the P43E test only scans
MarketScene). The gold disc + arch are there; the name is missing.
**Fix:** add a `WorldNameLabel` "Về" on the Math return arch (one-liner in
`MathWorldBuilder`), pin it in CT-P43.
**Verify:** label test + screenshot.

### F14 — Perf budget is an unmeasured ceiling (performance)
**Problem:** CT-P42C caps content transforms at 320; current count is
estimated ~150–190 + FBX children. Decor composition will add ~60–100. No
runtime census or draw-call data is recorded anywhere.
**Fix:** Batch 1/5 — keep ≤ +100 transforms, record a census log line in the
build run, measure before/after.
**Verify:** census + standalone build.

### F15 — Tests pin skeleton specifics, not composition (technical quality)
**Problem:** CT-P41/42/43 pin pad/path names, number-row positions and
clearances; nothing pins sightlines, thresholds, landmark visibility, fence
openings or decor rhythm. A composition redesign can silently regress
readability while staying green.
**Why:** tests were built as safety rails for the skeleton, not design rails.
**Fix:** add EditMode invariants for: path graph connectivity (waypoint
segments clear), fence openings ≥1.6 m, landmark heights/silhouettes, no
decor inside zone cores, join points of loop paths. Keep pure geometry (no
rendering).
**Risk:** over-constraining art; assert invariants, never taste.

---

## 2. What must not be damaged

- Composition root / DI, typed EventBus, save format, quest/learning/hint
  services, audio pipeline, camera/player/input contracts (all Phase 1/2 locks).
- `WorldTransition` + `ISceneOps` (idempotent, spam-safe, fail-truthful) and
  the travel/return flow with cover and failure restore.
- `SubjectWorldBuilder` hub gates + hub-arc layout (user-tuned) for the 3
  spatial subjects.
- Math quest/token/bloom/director wiring and their CT-P36/P40/P41 pins.
- Golden NPC standard (Tess rig/identity).
- The batch-verification rule from HANDOFF: numeric batch tests, no foreground
  driving; human eyes only at phase end.

---

## 3. Audit verdict

Math World is **enterable, traversable, returnable, quest-completable and
tested** — a solid pilot foundation. It is still a **skeleton place**: flat
pads instead of districts, motifs instead of landmarks, strips instead of
paths, a cube instead of a counting activity, and a reward the child never
sees. The next step is not more props; it is composition: ground grammar,
landmarks, thresholds, a garden that is a garden, a bridge that is a bridge,
host staging, and reward visibility — plus three foundation hardenings
(dead district retirement, generic scene-backed travel, boundary closure).

Redesign: `MATH_WORLD_REDESIGN.md`. Classification: `MATH_WORLD_REUSE_MATRIX.md`.

---

## 4. Verification evidence for this audit

- Baseline suite on unmodified `15ad8bb`: recorded in
  `MATH_WORLD_VISUAL_QA.md` (Stage G) — launched before any edit.
- `tools/validate_content.py` authoring: PASS (check at Stage G for changes).
- Static math census (this audit): ~150–190 transforms in `BuildContent`,
  excluding FBX child nodes; CT-P42C cap 320.
