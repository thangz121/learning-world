# MATH_WORLD_VISUAL_QA â€” P3.0.1.1 (Stage G, evidence log)

Date: 2026-09-21. HEAD: `15ad8bb` + Batch 1/B1R working-tree edits (uncommitted).
Artifacts live in `C:\Users\PC\AppData\Local\Temp\opencode\p311b-*`.

## 0. B1R â€” user round (floating trees / clutter / size / GitHub assets)

User verdict on the B1 build (screenshot): trees floating in the sky over the
void; too many wooden blocks in the hub; playground too small; find and apply
useful GitHub assets; make it pretty and tidy (may rearrange, may replace UI).

Fixes shipped in B1R:
- Island r19 â†’ **r26 (52m)** + dark skirt; every object now lives inside r25;
  rim = 20 hedge bushes + an 11-tree Kenney line + backdrop blobs ON the
  island (no more floating trees/rocks over the void).
- Hub decluttered: removed the 4 courtyard pots, 6 mouth markers and the
  off-path 1..5 number-row pads; the Counting Frame moved to the hub edge
  (-5.2,-3.2); Tess nook keeps only mat + 2 Kenney flower planters + basket.
- Kenney CC0 kits applied (official zips; provenance in
  `Assets/Documentation/ENVIRONMENT_ASSET_SOURCES.md` Â§L):
  garden = Kenney fence modules + gate + 4 crop beds (3 carrots / 2 pumpkins /
  4 corn / 5 strawberries) + counting tree + 5 stepping stones;
  bridge = 3 Kenney bridge modules over a walkable deck + rail posts + bank
  rocks/plants/lilies; meadow loop paths; Kenney trees/rocks/flowers/
  mushrooms replace the white-canopy Quaternius Math trees.
- World scale: entry arch z=-12, return arch z=+12, garden (-16,5) r6.5,
  bridge (15.5,-5), clearing (15.5,-8.5), meadow loop west; router bounds
  27Ã—27.

B1R evidence (batch, logged):
| Check | Result | Artifact |
|---|---|---|
| EditMode (updated pins P41/P42/P43) | 529 total / **524 pass / 0 fail / 5 skip** | `p311b-b1r-editmode.xml` |
| Census (`BuildContent`) | **311** transforms (B1: 373) | `p311b-census3.log` |
| Prop placements | 38 MathProp + fence/crops/stones | `p311b-census3.log` |
| Release build | **Succeeded**, 109,358,564 bytes | `p311b-b1r-build.log` |
| Headless boot | **FACE_OK, 0 exceptions** | `p311b-b1r-boot.log` |

B1R build for human review:
`C:\Users\PC\AppData\Local\Temp\opencode\p311b-B1RBuild\LWE.exe`

B1R human checklist: (1) no object floats beyond the grass at any zoom;
(2) hub reads open â€” rim + frame + Tess only; (3) garden is a fenced plot
with real crops and a gate; (4) bridge has modules/rails and the water is
denied except the deck; (5) meadow loop closes clearing â†’ garden;
(6) quest `math_counting` still first-talk â†’ find one â†’ bring â†’ celebrate.

## 1. Numeric evidence (batch mode, no foreground)

| Check | Result | Artifact |
|---|---|---|
| Baseline EditMode (unmodified 15ad8bb) | 526 total / **521 pass / 0 fail / 5 skip** | `p311b-baseline-editmode.xml` |
| Baseline census (`BuildContent` transforms) | **176** | `p311b-census.log` |
| Batch 1 EditMode | 529 total / **524 pass / 0 fail / 5 skip** (+3 new P43 tests) | `p311b-b1-editmode.xml` |
| Batch 1 census | **373** (< CT-P42C cap 400; +197 composition objects) | `p311b-census2.log` |
| Release build (3 scenes, no Development flag) | **Succeeded**, 108,488,772 bytes | `p311b-build.log` |
| Headless boot smoke | **FACE_OK, 0 exceptions** | `p311b-boot.log` |
| Content validator | authoring PASS (recorded in audit Â§4) | `tools/validate_content.py` |

Build output for human review (Stage E):
`C:\Users\PC\AppData\Local\Temp\opencode\p311b-B1Build\LWE.exe`

## 2. Batch 1 change list

Changed:
- `Assets/A_World/MathWorld/MathWorldBuilder.cs`
  - hub courtyard (sand pad + rim stones + 4 planters + 6 destination mouth
    markers), host nook (mat + 2 planters + basket), Counting Frame upgrade
    (5 rods Ã— 3 beads, 2.0 m), entry arch 5 beads, garden plot (fence with 2
    openings + gate arch + 2 new beds + countable crops 3/2/4/5 + counting
    tree with 5 beads + 5 stepping stones with 1..5 pips + giant sunflower),
    brook (banks/reeds/lilies + boardwalk stringers/posts + bead garland),
    far-bank stone clearing (5 stones with 1..5 pips + bead reward), meadow
    loop paths + inner garden path, rim fill (8 bushes + 4 backdrop trees),
    brook carves (water denied except the 1.8 m deck corridor), return "Vá»"
    label.
- `Assets/Tests/EditMode/CT-P42_MathWorldSafety.cs` â€” budget cap 320 â†’ 400
  with measured justification.
- `Assets/Tests/EditMode/CT-P43_SkeletonComplete.cs` â€” entry beads pin 3 â†’ 5;
  new P43H (return label), P43I (garden plot), P43J (hub/bridge composition).

Unchanged (verified):
- Quest/learning/audio/camera/save contracts; `math_counting` flow; Tess
  presenter logic; token carry; bloom consumer; scene travel/return; all
  CT-001..CT-040 pins (suite green); no new services; no new materials
  systems (Lit cache only); no new packages.

Temp tooling deleted (lockdown): `Assets/Editor/TempMathCensus.cs`,
`Assets/Editor/TempBuild.cs` (+ meta; `Assets/Editor/` removed).

## 3. Human visual review checklist (Stage E â€” not yet done)

I cannot screenshot from this session (batch rule, no foreground). The human
should run the B1 build and verify at minimum:

1. **Arrival**: enter Math from the hub gate; arrival beat frames Tess; after
   Follow, the courtyard, Counting Frame, garden mouth and bridge mouth are
   readable without labels.
2. **Hub**: sand courtyard + rim stones + 4 planters read as one place; the
   entry bead arch (5 beads) is visible when turning around; return arch shows
   "Vá»".
3. **Garden**: fence encloses the plot with two openings; gate arch passes
   under; 3 carrots / 2 pumpkins / 4 sunflowers / 5 berries are countable at
   child height; counting tree with 5 bead-fruit; 5 stepping stones read 1..5.
4. **Bridge**: brook banks/reeds/lilies; walk across the boardwalk only (water
   denied); bead garland overhead; far-bank 5 stones + gold bead pile.
5. **Loop**: from the clearing, the meadow path leads west to the garden north
   opening and back to the courtyard (no dead end, no blocked corridor).
6. **No regressions**: quest `math_counting` first-talk â†’ find "one" â†’ bring to
   Tess â†’ celebrate + blooms; return arch returns to Main with the pre-entry
   HUD.
7. **Feel**: empty-lawn ratio, decor density, camera framing at spawn/garden/
   bridge; anything that still reads "skeleton" gets written down for Batch 2.

Known limitations carried into Batch 2+:
- Quest/learning still one word ("one"); reward bloom still fires at the
  garden (off-screen from Tess) â€” Batch 3.
- Arrival audio still silent â€” Batch 2.
- Dead spatial Math district still built in MarketScene â€” Batch 4 (F9).
- Scene-backed travel still Math-branched in shared code â€” Batch 4 (F10).
- No framerate/census screenshot in this doc yet; add at Stage G final.

## 0b. B1R2 — root causes found by runtime self-test (click + floating)

User round 2 (screenshot): trees still at/over the rim; click does not move the
player; no distinctive "math playground" identity.

Runtime self-test (temp script, logged, deleted after) proved three REAL bugs:

1. **Ground was r13, not r26.** Unity's cylinder mesh has radius 0.5, so
   `localScale 26` produced a 26m-diameter disc; worse, the primitive
   collider is a CapsuleCollider that scales non-uniformly into a giant
   sphere. Result: click rays missed the ground past ~13m (no movement) and
   the NavMesh never reached the garden/bridge (r16-24) — the agent could
   not path to the districts; rim props stood beyond the grass.
   Fix: ground is a **Plane** (5.2 -> 52x52m, flat MeshCollider like Main);
   pads are collider-free visual treatment (their cylinder colliders were
   5.5m-radius spheres); `MathOuterField` (300m horizon backdrop) is
   stripped + ignoreFromBuild.
   Evidence: self-test `afterFarMove pos=(44,0.11,5) pathStatus=PathComplete`
   (garden reached) and `clickRay hit=MathGround`.

2. **Startup mic-offer Dim swallowed every click.** `[ClickSpy] press ...
   overUi=True hits=Title@50|OfferPanel@50|Dim@50|` — the Phase 2.1 mic
   offer's fullscreen Dim ate world clicks while shown (child trapped).
   Fix: `MicSetupDialog` + `DependencySetupDialog` Dims are
   `raycastTarget=false`; panels stay visible and their buttons interactive.

3. **Rim composition.** With the real r26 ground and the outer field, the
   hedge (r24) and tree line (r22) sit on the lawn; nothing floats.

Math identity added (B1R2): domino line 1..6 flanking the entry axis, a
5-cube Number Tower (3.5m) ahead-right, + / - signs on the east lawn, and
count pips on every bed front. Bridge rails turned wood.

B1R2 evidence (all logged):
| Check | Result | Artifact |
|---|---|---|
| EditMode suite | 529 / **524 pass / 0 fail / 5 skip** | `p311b-b1r2-editmode.xml` |
| Self-test far walk | garden PathComplete (r26 navmesh) | `p311b-selftest8.log` |
| Self-test click ray | hits MathGround | `p311b-selftest8.log` |
| Release build | **Succeeded** (World.dll 23:32) | `p311b-b1r2-finalbuild.log` |
| Headless boot | FACE_OK, 0 exceptions | `p311b-b1r2-boot.log` |

Review build: `C:\Users\PC\AppData\Local\Temp\opencode\p311b-B1R2Test\LWE.exe`

## 0c. B1R3 — polish round (tunnel, sky, red/pink, world column, camera)

User round 3 requests, all shipped:
1. **Math tunnel transition** (`MarketHUD`): fullscreen overlay on its own
   canvas (order 80, click-through) with 6 bead rings (gold/blue/pink) rushing
   outward + 16 drifting glyphs (1-9, + - =, ?, ?). `PlayTunnel()`/`StopTunnel()`
   driven by `MarketBootstrap` for BOTH enter and return (replaces the black
   fade for subject travel). Structure pinned by CT-P38D.
2. **Red/pink accents**: bunting flags over the courtyard, pink/red flowers
   and a red mushroom cluster, Number Tower + Counting Frame + entry beads now
   cycle gold/blue/pink.
3. **Sky instead of endless green** (`BuildGround`): the island floats — soil
   rim under the lawn, a cloud sea 14m below, 9 cloud blobs around the rim and
   3 high clouds. All bake-ignored + collider-free (open horizon, no
   claustrophobia).
4. **World-name column + arrival beat**: `MathWorldSignPost/Board` + "Toán"
   label + 3 beads at the entry plaza (`MathWorldBuilder.SignWorldPos`); the
   Math arrival beat frames the column for 2.2s, then the camera auto-returns
   to the character (FramePointFor).
5. **Higher spawn camera**: `MathWorldBuilder.FollowOffset (0,4.6,6.4)` used
   for the Math spawn Follow (Main/hub offsets untouched).

Evidence (logged):
| Check | Result | Artifact |
|---|---|---|
| EditMode suite | 530 / **525 pass / 0 fail / 5 skip** | `p311b-b1r3-editmode3.xml` |
| Census | 400 transforms (< cap 440) | P42C pin |
| Release build | **Succeeded** (World.dll 23:53) | `p311b-b1r3-build.log` |
| Headless boot | FACE_OK, 0 exceptions | `p311b-b1r3-boot.log` |

Review build: `C:\Users\PC\AppData\Local\Temp\opencode\p311b-B1R3Build\LWE.exe`

## 0d. B1R4 — smoothness round (disco floor, bunting, tunnel)

User round 4: transition ok but too long; transition visibly broken/not
smooth; Math playground flickers like a disco floor while walking.

Root causes + fixes:
1. **Disco floor**: `MathSkyRim` (island cliff) top sat exactly at y=0 —
   coplanar with the ground plane, so the brown rim fought the grass under
   the depth buffer (worst while the camera moves). Rim now rides 15cm below
   the lawn (position y=-1.55, height 2.8) — a clean cliff edge, zero fight.
2. **Bunting detach**: `LookRotation` aligns local +Z with the span, but the
   cord length was written on X — the cord rendered as a stray cross-bar and
   the flags read as floating diamonds. Length moved to Z.
3. **Tunnel quality/time**: rings now use a 256px anti-aliased ring sprite
   (soft edges, no upscale jaggies), glyphs limited to LegacyRuntime-covered
   characters (1-9, + - =; the old ?/? rendered as broken boxes on some
   machines), SmoothStep motion + slower rotation, max ring scale 2.9 ? 2.2,
   fade in/out 0.18s/0.22s, pre-load hold 320ms ? 120ms.

Evidence: EditMode 530 / **525 pass / 0 fail / 5 skip**; release build
**Succeeded** (World.dll 06:27); boot **FACE_OK 0 exception**.
Review build: `C:\Users\PC\AppData\Local\Temp\opencode\p311b-B1R4Build\LWE.exe`

## 0e. B1R5 — rim burial fix

The B1R4 rim change used scaleY 2.8 on a cylinder primitive (height = 2 x
scaleY = 5.6) so its top reached +1.25m and the brown rim BURIED the whole
island (user screenshot: "nu?c ng?p h?t sân"). Corrected: scaleY 1.4,
center y=-1.5 -> top -0.10m (cliff edge under the lawn, no burial, no
z-fight). Build B1R5: World.dll 06:36; suite 530/525/0/5.

## 0f. B1R6 — sign fix + declutter round

User round 6: playground cluttered; world name board broken.
- **World sign**: the floating label sat inside the board box (dark board, no
  text). Now uses the proven hub-gate pattern — `WorldNameLabel.SetupLocked`
  parked 22cm in front of the board, yaw locked toward the courtyard; beads
  lowered to the board top.
- **Declutter**: courtyard bunting removed (diamonds read as floating noise);
  dominoes pulled to the entry-path shoulders (x=+-1.9, 1.5m spacing, larger
  tiles) as one readable number walk; rim stones reduced 6 small pale discs ->
  4 larger darker stones on the diagonals.
- Pins: P41D (MathWorldSignLabel), P42A (bunting prefix removed), P43J
  (rim stones == 4). Suite 530/525/0/5; build B1R6 World.dll 06:43.

## 0g. B1R7 — outer world = sky

The pale "cloud sea" plane at y=-14 filled the horizon with white. Removed:
beyond the island the camera background is sky blue again, with 9 cloud
blobs around the rim (lowered) and 4 clouds under the cliff so the island
visibly floats. P41D pin updated (MathSkyCloudSea -> MathSkyCloud0).
Suite 530/525/0/5; build World.dll 06:57.
