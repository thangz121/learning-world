# ENVIRONMENT ASSET SOURCES — Phase 2.4 Premium World Upgrade

Research date: 2026-09-18. Target: Unity 6000.6.0f1 + URP 17 (Forward),
flat-shade preschool look (established by the in-project Quaternius
character pack). Rule: CC0/MIT only, style-match or reject, LW picks
technique over bulk import.

## A. Quaternius — Ultimate Nature Pack (2019, 150 models) — SELECTED

- SOURCE: quaternius (https://quaternius.com/packs/ultimatenature.html),
  mirror used: https://opengameart.org/content/low-poly-nature-pack-1
  (`ultimate_nature_pack_by_quaternius_1.zip`, 23 MB, downloaded + inspected).
- LICENSE: CC0 1.0 Universal (public domain; commercial use allowed, no
  permission needed). Credit appreciated, not required.
- UNITY VERSION: version-agnostic FBX/OBJ/Blend. Verified import in this
  project (Unity 6000.6.0f1): 20/20 FBX import clean, importer auto-maps
  materials to URP/Lit, 164–5357 verts per model.
- URP SUPPORT: yes — imported materials arrive as URP/Lit; LW overrides slot
  colors in code (imports stay pristine, same discipline as the character pack).
- WHAT WE USE (17 FBX final, copied to
  `Assets/A_World/Visuals/Resources/Nature/`):
  CommonTree_1/3/5, PineTree_2, Willow_2, Bush_1/2, BushBerries_1,
  Flowers, Rock_2/4/6, Rock_Moss_2, Plant_2/4, TreeStump, WoodLog.
  (Grass/Grass_2/Grass_Short were trialed live, then deleted: their meshes
  carry no usable normals and render black under URP/Lit in Import AND
  Calculate importer modes. Tufts use the LW generated clump system
  `A_World/LwGrass.cs` instead — brief §6 fallback, same palette.)
- FILES IMPORTED: the 20 `.fbx` files above (+ Unity-generated `.meta`).
- WHY SELECTED: same author + same flat-shade untextured aesthetic as the
  Milo/Mia/player rigs already in the game — zero style-harmonization risk
  (the anti-asset-soup rule). Semantic material slots (Wood/Green/
  DarkGreen/Berry/Rock/Leaves/…) allow palette harmonization in code.
  Poly counts fit a 16×12 m preschool lawn with GPU instancing + shared mats.
- REDISTRIBUTION NOTES: CC0 — may ship in builds, no attribution required.
  Provenance: `Assets/A_World/Visuals/Resources/Nature/README.md`.

## B. MatrixRex — Uber-Stylized-Water — REJECTED (no water feature)

- URL: https://github.com/MatrixRex/Uber-Stylized-Water — MIT, 496 stars,
  Unity 6 (6000.0.30)+, URP Forward/Forward+/Deferred, Gerstner waves,
  shoreline foam, caustics, planar reflections. Inspected README + compat.
- REASON: the LW map has no pond/river/fountain location; the brief forbids
  forcing water. No import. Revisit only if a future map adds a water zone.

## C. Mithzzx — Project-GrassFlow — REJECTED (overkill; technique noted)

- URL: https://github.com/Mithzzx/Project-GrassFlow — MIT, 46 stars,
  Unity 6000+ URP, compute-shader indirect GPU grass (1M blades, HiZ culling,
  LOD, wind). Inspected README + architecture + docs list.
- REASON: built for cinematic-scale fields; LW is a 16×12 m lawn where
  ~20 grass CLUMPS carry the look. Compute pipeline + Terrain-slot coupling
  + custom passes = unjustifiable risk to the locked URP/recording setup.
- TECHNIQUE TAKEN: clumps-over-blades, tri-tone tip/mid/root gradient, hash
  color jitter, baked root AO — implemented in the lightweight LW clump
  path (model choice + tint variation), no import.

## D. roundyyy — ProceduralGrassGenerator — REJECTED (tool import; technique noted)

- URL: https://github.com/roundyyy/Procedural-Grass-Generator — 20 stars,
  Unity 2020.3+, URP/Built-in, Editor-window blade-clump generator with
  vertex-color gradient + baked AO + basic wind shader. License: informal
  "free personal/commercial" statement, no LICENSE file / no SPDX.
- REASON: Editor-tool import + 2020-era shaders + non-standard license text
  fail the gate. Technique (blade clumps, vertex AO, wind sway) already
  covered by the selected Quaternius grass variants + LW sway.

## E. unitycoder — ProceduralGrassGenerator — UNAVAILABLE, REJECTED

- No such repository exists. Closest hits: `unitycoder/UnityDeferredGrass`
  (deferred pipeline — incompatible with URP Forward) and a WIP blog-only
  "Grass Maker" billboard experiment (no importable source). Nothing to test.

## F. VKev — Unity-URP-Shaders-Code — REJECTED (pipeline risk; technique noted)

- URL: https://github.com/VKev/Unity-URP-Shaders-Code — MIT, 97 stars,
  Unity 2023.2-era hand-written URP shaders (grass/terrain-blend, toon,
  water, outline render feature). Requires `VkevShaderLib.hlsl` at Assets
  root + per-material coupling.
- REASON: foreign shader lib + render-feature changes touch the locked
  lighting/recording path (a broken game-view shader fails every gate).
  Technique noted (terrain-blend, instancing, wind uniforms); not imported.

## G. mozankatip — StylizedTrees — REJECTED (style + version gap)

- URL: https://github.com/mozankatip/StylizedTrees — MIT, 16 stars,
  2023-era ShaderGraph "Leaves" wind shader + fluffy LOD trees.
- REASON: fluffy-shader look ≠ LW flat-shade family; ShaderGraph 2023→6000.6
  upgrade + wind/LOD tuning risk for 3 garden trees. Rejected on the soup rule.

## H. ProblematicToucan — stylized-vegetation — REJECTED (version gap)

- URL: https://github.com/ProblematicToucan/stylized-vegetation — MIT,
  17 stars, 7 commits, Unity 2020.3 URP + Cyanilux custom-lighting
  ShaderGraphs. 2020.3 graphs + custom lighting vs locked URP 17 lighting =
  upgrade risk with no unique payoff. Rejected.

## I. SkywolfGameStudios — CC0Tree — REJECTED (library too thin)

- URL: https://github.com/SkywolfGameStudios/CC0Tree — CC0 1.0, 1 star,
  launched 2026-05. Only 3 assets (bowling ball/pin, 1 tree). License is
  perfect but there is nothing LW needs that Quaternius does not cover
  better. Rejected.

## J. TinyTreats — Pretty Park set — REJECTED (style mismatch)

- URL: https://github.com/TinyTreats-Game-Assets/Tiny-Treats-Pretty-Park-1.0
  (+ itch.io, OpenGameArt) — CC0 1.0, 14+ models, gradient-atlas TEXTURED
  look, fountain/tiles/hedges.
- REASON: textured-atlas aesthetic clashes with the flat-shade world (soup
  rule); the fountain is a water-adjacent feature the map does not need;
  hedges already exist. Rejected.

## K. Quaternius — Ultimate Stylized Nature Pack (2022, textured) — REJECTED

- URL: https://quaternius.com/packs/ultimatestylizednature.html — 60+ assets
  with seamless TEXTURES + normal maps. Same author, but the textured look
  would sit next to untextured flat-shade characters as a different game.
  Rejected on the soup rule; the 2019 untextured pack (A) is the match.

## SELECTED TECHNOLOGY (final)

1. Quaternius Ultimate Nature Pack 2019, CC0, 20 curated FBX (A).
2. LW `NatureLibrary`: Resources-load + height-normalize + slot-palette
   harmonization, shared URP/Lit materials, collider-free, deterministic.
3. LW `NatureSway`: single-component sine sway for grass/flowers/plants
   (the "alive" requirement without particles or shader risk).
4. Existing cluster/zone/clear-zone composition system (extended, not forked).
