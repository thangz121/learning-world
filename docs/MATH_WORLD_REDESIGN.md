# MATH_WORLD_REDESIGN — P3.0.1.1 (Stage C, rewritten on HEAD 15ad8bb)

Date: 2026-09-21. Status: DESIGN PROPOSAL + Batch plan. Human Art Director
review gates are marked. Companion docs: `MATH_WORLD_DESIGN_AUDIT.md`,
`MATH_WORLD_REUSE_MATRIX.md`.

---

## 1. Design intent

Turn the Math pilot from "lawn with pads and props" into **one remembered
place**: a counting meadow the child enters through a bead gate, meets Tess at
a courtyard, walks a loop past a fenced counting garden and across a brook
bridge to a number-stone clearing, and comes home under a gold arch. Every
element keeps its proven contract (scene travel, quests, token, bloom,
learning entries); composition, landmarks, paths and staging become real.

World name (working): **Vườn Đếm** (Counting Meadow). No new on-screen text is
required: existing labels are "Toán" (hub gate), "Tess", "Về" (return).

## 2. Non-negotiables carried from the audit

- Keep: scene travel, `WorldTransition`, spawn at lobby center (user rule),
  zone centers lobby(0,0) / garden(−10.5,3) / bridge(10.5,−3), entry z=−8,
  return z=8, zone-pad/path z-layering (32 mm), corridor clearances, object
  budget, all CT-P36/40/41/42/43 pins unless explicitly re-pinned in a batch.
- No new frameworks: reuse `CharacterPresentation`, `Interactable`,
  `ProximityDiscovery`, `IAudioDirector`, `QuestManager`, `MathBeacon`,
  `MathTokenCarry`, `MathBloomDisplay`, `NatureKit`, `WorldNameLabel`.
- No Math literals added to shared contracts (audit F10 reverses existing ones
  in a hardening batch).

## 3. Reusable composition grammar (the real P3.0.1.1 deliverable)

```
SubjectWorld
├── Threshold      entry arch + apron + sightline to the hub
├── Hub            courtyard: ground signature, rim rhythm, host nook,
│                  destination mouths (1 per area) framed by decor
├── Spokes         path ribbons (width 1.8 → 1.4 → 1.2), bends ≥120°,
│                  a landmark visible at every bend
├── Area           composable: pad/plot + fence or bank + identity landmark +
│                  activity anchors + stand-spots + reward nook
├── Loop           optional return path closing Hub→Area A→Area B→Hub
└── Return         gold arch + disc + "Về" + one-way trigger
```

Variation points (data, never forks): palette, landmark kind, area identity,
crops/props, quest ids, entry ids. This grammar goes into
`Assets/Documentation/SUBJECT_WORLD_FOUNDATION.md` as the v2 skeleton after
Batch 1 proves it in Math.

## 4. Math World v2 — concrete plan (local coords; world = +60x)

### 4.1 Ground signatures (replaces pad-as-only-language)

| Zone | Signature | Build |
|---|---|---|
| Hub | sand courtyard r4.5 + stone rim (12 rim stones) + 4 planters | replace periwinkle `MathLobbyPad` fill (keep name) |
| Garden | dark-soil plot r4.6 + wattle fence + crop beds | keep `CountingGardenPad` name, restyle: meadow ring → soil center → fence |
| Bridge | brook banks + pebble strips + reeds + lily pads; sand approach wedge | keep `NumberBridgePad`, add banks |
| Entry/Return | tan aprons (existing paths), threshold beams | existing + arch upgrades |

### 4.2 Landmark hierarchy

1. **Entry Bead Arch** — upgrade the entry board at z=−7 into a true arch:
   2 posts (±1.1, keep), rounded top beam at y2.05 (keep) + 5 giant beads
   (0.26) + 2 inner hanging strings; child sees it behind the spawn on
   turnaround. (Pin P43B updated 3 → 5 beads if chosen; Batch 1 keeps 3 + adds
   2 to satisfy the redesign and updates the pin.)
2. **Lobby Counting Frame** — upgrade the abacus at (−2.6,−1.2): 5 rods,
   3 beads per rod, 2.0 m tall, gold/blue; the tallest thing in the hub,
   visible from spawn (ahead-left), clear of the garden line (keep P41E).
3. **Garden Counting Tree** — trunk + canopy + 5 hanging bead-fruit at
   (−13.4,5.6) inside the plot; 3.4 m silhouette; doubles as the reward nook
   backdrop.
4. **Bridge + Stone Circle** — boardwalk crossing (see 4.4) with 5 count posts
   and, on the far bank, a circle of 5 flat stones with 1–5 pips + a gold bead
   pile (reward nook).

Nothing else may exceed 2.6 m between the hub and the garden tree (sightline
rule).

### 4.3 Counting Garden (area A, west)

- **Plot**: r4.6 around (−10.5,3). Wattle fence (posts + 2 rails, 0.55 m)
  with openings: **east** (toward the lobby path, 2.2 m) and **north**
  (toward the loop path, 1.8 m). Fence is bake-visible (real boundary).
- **Garden gate**: small bead-tipped arch at the east opening (1.9 m).
- **Beds** (keep `GardenBedW/E` names + positions, add 2):
  - W (−11.3,3.8): 3 carrots (tapered orange + green tops).
  - E (−9.7,3.8): 2 pumpkins (squashed orange + stem).
  - N (−10.5,4.9): 4 sunflowers (tall stems + yellow discs + brown centers).
  - S (−10.5,1.5) berry bush strip: 5 red berries on one green bush.
- **Counting row** (pinned): pedestals 1/2/3 + gold `one` cube stay exactly
  where they are; add a small wood frame behind each pedestal so they read as
  a designed row, not isolated blocks.
- **Number stones**: 5 flat stones with 1–5 gold pips along the garden path
  inside the plot, from the east gate to the tree. (This is the walkable
  version of the pinned number row; the pinned row stays as the outer
  "counting walk" beside the path.)
- **Reward nook**: keep 3 hidden blooms (`MathBloomRoot`), add a visible
  giant sunflower (1.6 m) at (−13.0,6.2) as the plot's terminus.
- **Stand-spots**: clear 1.2 m in front of each bed on the plot path; no decor.

### 4.4 Number Bridge (area B, east)

- **Brook**: keep `BridgeStream` (10.5,0.012,−3) as the water strip, restyle:
  darker water (0.24,0.48,0.80), 2 pebble bank rows (0.16 spheres, grey) along
  both sides, 3 reed clusters at the banks, 3 lily pads (flat green discs) on
  the water, 1 small pool disc at the south end. **Add 2 foot carves**
  (west x 8.0–9.6, east x 11.4–13.0, z −3.7…−2.3) leaving the deck corridor
  1.8 m open — water is no longer walkable (crossing matters). Navmetry check.
- **Boardwalk**: keep `BridgeRailW/E`; add 2 stringers, 4 posts, 5 planks
  (existing), 5 count posts on the west rail (pips 1–5 facing the approach),
  and replace the floating gold discs (`MathBridgeDisc`) with hanging bead
  lanterns at y2.4 over the deck — same names, readable meaning (gold beads).
- **Far bank (east, x>13)**: **Number-Stone Circle** at (14.0,−4.2): 5 flat
  stones (r0.35) in a ring with 1–5 pips; a gold bead pile (0.8 m) at
  (14.8,−5.0) as the area reward nook; 1 NatureKit tree behind it as the
  backdrop (move `MathQTree4` if it conflicts).
- **Approach**: chevrons stay; add a small threshold signpost at (8.6,−2.4)
  with a bridge glyph (no text).

### 4.5 Hub courtyard + host nook

- **Courtyard**: `MathLobbyPad` restyled to sand (0.86,0.78,0.62) with a rim
  of 12 small stones at r4.2 and 4 planter pots at the diagonals.
- **Destination mouths**: decor frames (2 pots + 1 low bush each) at the four
  path exits (entry north, garden WSW, bridge ESE, return south) so the
  child's eye reads four doors.
- **Host nook** (Tess at (2.2,0.8)): round mat r0.9, 2 low planters with
  pastel blooms on the east side, a woven basket prop (0.4 m) south-west of
  her, and a clean backdrop: move `MathQBushB` to (5.2,2.6) and keep ≥2 m
  clear behind Tess in the camera direction.
- **Abacus → Counting Frame** (see 4.2) stays at (−2.6,−1.2); its new height
  (2.0 m) and width (1.9 m) must keep 0.9 m clear of the entry corridor
  (P41E) — verify numerically.

### 4.6 Paths, loop, thresholds

- Keep pinned: `MathPathEntry` (0,−4) 1.6×8.4; `MathPathReturn` (0,4)
  1.6×8.4; `MathPathGarden` (−5.25,1.5) yaw 11 m; `MathPathBridge`
  (5.25,−1.5) yaw 11 m; chevrons.
- Add **loop path** segments (width 1.3, sand-tan 0.80/0.68/0.50):
  `MathPathLoopA` garden north opening (−10.5,7.2) → (−5.0,8.2);
  `MathPathLoopB` (−5.0,8.2) → (4.0,7.6);
  `MathPathLoopC` (4.0,7.6) → return path junction (0.0,5.0) — reuse return
  path; plus `MathPathLoopD` garden north (−10.5,7.2) → bridge north bank
  (10.0,−6.5) is left open lawn by design (no path: the meadow is the
  shortcut, the loop is the scenic route).
- Thresholds: garden gate arch (4.3), bridge entry posts (4.4), entry arch
  (4.2), return arch (existing + "Về" label, audit F13).
- Width grammar: 1.8 hub spokes → 1.4 area approaches → 1.2 inside plots.

### 4.7 Boundary + backdrop (audit F11)

- Keep the 16 base hedge spheres; fill the ~4.9 m gaps with **NatureKit bush
  clusters** (Bush1–3, scale 1.1–1.4) + 6 rocks so the rim reads continuous;
  add 4 taller trees behind the rim (outside r18) for skyline depth.
- Close foot gaps: colliders on the filler bushes (bake-visible); no carve
  ring needed if the rim is visually and physically continuous.
- Backdrop blobs stay outside; add 2 more behind the entry/return axes.

### 4.8 Decor rhythm rules

Cluster → gap → landmark → breathing space. Concrete limits:
- No decor within: hub courtyard r4.5, host nook r2.2, garden plot interior
  (except crops/stones), bridge deck ±1.2 m, all paths + 1.0 m, entry/return
  corridors, stand-spots.
- Per zone: max 6 accent clusters, min 1.5 m gaps; blooms only at planters,
  nook and reward nooks (no scatter).
- NatureKit placements: 19 existing + ≤8 new (rim fill + backgrounds) — cap
  total 27; keep every clearance test green.

### 4.9 Camera beats (Math-space only, no StoryMoment)

| Beat | When | Shot |
|---|---|---|
| Arrival | existing | frames Tess (keep) |
| Find | first WordSeen "one" | brief frame on the cube (FocusOnFor 2 s) |
| Bring | bring complete at Tess | frame Tess + child (existing arrival-style, 2 s) |
| Reward | quest complete | frame the lobby Counting Frame + a new lobby reward bead-glow, 2.5 s (fixes F7 without off-screen garden framing) |
| Return | trigger | existing Follow |

Implement in `MathQuestDirector` (Beat 4 is Batch 3; Beat 2/3 Batch 2).
No camera calls from world scripts.

## 5. Batch plan (Stage D)

**Batch 1 — Composition (this phase. world only, no gameplay change)**
Deliver: restyled hub courtyard + rim/planters; entry arch 5 beads; Counting
Frame upgrade; host nook (mat/planters/basket, backdrop move); garden plot:
fence + gate + 4 crop beds + counting tree + number stones + giant sunflower;
bridge: banks/reeds/lilies/pool + carves + count posts + lantern discs +
far-bank stone circle + bead pile; loop paths; boundary fill + 4 rim trees;
"Về" label in MathScene; decor rhythm pass; census; tests updated only where
pins were skeleton-specific; new layout tests (fence openings, landmark
heights, clear zones).
Not in Batch 1: quest/learning changes, camera beats, audio, F9/F10.

**Batch 2 — Host + camera + arrival audio**
Host nook polish, bubble icon, Math-space beats (find/bring), arrival cue via
`IAudioDirector` (SfxId or Tess line), label/bubble overlap test.

**Batch 3 — Counting activity + content sidecar + reward visibility**
Agent C/D sidecar: numbers 1–5 vocab + approved audio + dialogue lines +
counting icon specs. Bind beds/stones/pedestals to `Interactable`s (typed
words), extend `math_counting` (or a second quest) to count 1→3→5, reward
beat in the hub (F7), post-quest "next" cue to the bridge stones.

**Batch 4 — Foundation hardening**
F9: retire the spatial Math district in MarketScene (honor `SceneName` in
`SubjectWorldBuilder`) and re-pin CT-P31/P33/P39/P43E. F10: generalize
scene-backed travel (data-driven offset/bounds/anchors; remove Math branches
from MarketBootstrap/GameInstaller). F12 audio ambience if budget allows.

**Batch 5 — Visual QA + perf + build**
Census, draw-call sanity, before/after screenshots at fixed viewpoints,
`MATH_WORLD_VISUAL_QA.md`, final suite + standalone build.

## 6. Batch 1 acceptance criteria

1. Hub reads as a courtyard (sand + rim + planters + four framed mouths), not
   a colored disc.
2. Entry arch / Counting Frame / Counting Tree / Bridge are identifiable from
   the spawn viewpoint without labels (screenshot evidence).
3. Garden: fence with exactly 2 openings (≥1.8 m/2.2 m), 4 beds with
   countable crops (1..5), tree with 5 beads, stones with 1..5 pips.
4. Bridge: banks + reeds + lilies, water carved except the 1.8 m deck
   corridor, count posts 1–5, far-bank 5-stone circle + bead pile.
5. Boundary: no visual gap > 2 m at r18; no walkout ring beyond the rim.
6. Corridors: CT-P42B green; new path/loop waypoints walkable; nav telemetry
   shows no PathPartial on gate→hub→garden→bridge→return.
7. Budget: content transforms ≤ 320 (CT-P42C), ideally ≤ 280; ≤ +8 materials.
8. All pinned tests green after deliberate pin updates; no gameplay/quest/
   save/audio/camera contract changes.
9. Build succeeds; boot has zero exceptions; travel + quest loop unchanged.

## 7. Risks

| Risk | Mitigation |
|---|---|
| Fence/carve severs nav | openings ≥1.8 m; runtime telemetry; EditMode opening test |
| Object budget overflow | compose with ~60–80 new transforms; census before/after |
| Pinned tests break on purpose | update only skeleton-specific pins, in the same batch, with comments |
| Landmark occludes follow camera | beams ≥2.2 m + `ignoreFromBuild`; screenshot at spawn/approach |
| Crop decor reads cluttered | one crop language per bed; no bloom scatter |
| Loop path crosses pinned samples | paths are walkable-exempt in P42B; verify sample list |
| Time/scope | Batch 1 world-only; gameplay untouched so quest loop stays proven |

## 8. Human Art Director decision points (review before Batch 2)

1. **Entry arch beads**: 5 (world number, recommended) vs 3 (current pin).
2. **Garden counts**: crops 3/2/4/5 as proposed vs strict 1–4.
3. **Reward location** (Batch 3): hub bead-glow (recommended, visible) vs
   garden tree bloom (scenic but off-screen).
4. **Boundary style**: composed bush/rock rim (recommended) vs keep spheres.
5. **Loop path**: scenic north loop (recommended) vs straight hub spokes only.
