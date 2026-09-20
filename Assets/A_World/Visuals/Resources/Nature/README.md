# ThirdParty provenance — Quaternius Ultimate Nature Pack (subset)

Source: "Ultimate Nature Pack" by quaternius
- https://quaternius.com/packs/ultimatenature.html
- Mirror used: https://opengameart.org/content/low-poly-nature-pack-1
  (`ultimate_nature_pack_by_quaternius_1.zip`, 23 MB, June 2019 pack)
- License: CC0 1.0 Universal (public domain; commercial use allowed, no
  permission needed). Credit: quaternius (appreciated, not required).

Copied FBX (17 of 150 in the final set; nothing else from the pack):
- Trees: CommonTree_1, CommonTree_3, CommonTree_5, PineTree_2, Willow_2
- Bushes: Bush_1, Bush_2, BushBerries_1
- Flowers: Flowers
- Rocks: Rock_2, Rock_4, Rock_6, Rock_Moss_2
- Plants: Plant_2, Plant_4
- Props: TreeStump, WoodLog

Evaluated live in a player build, then REMOVED (kept out to stay lean):
- Grass, Grass_2, Grass_Short — their meshes carry no usable normals and
  render black under URP/Lit in every importer mode tried (Import +
  Calculate). Tufts use the LW code-generated clump system instead
  (A_World/LwGrass.cs: guaranteed normals, same palette). Files deleted.

Deliberately left out (style/season fit): snow/dead/autumn variants,
cacti, palms, corn, wheat, lilypads, stumps/logs with snow.

Adaptations (all in code, imported sub-assets stay pristine):
- Runtime height normalization per role (authoring units vary).
- Slot-name palette harmonization (Wood/Green/DarkGreen/Berry/Rock/…)
  onto shared URP/Lit materials (see NatureLibrary.cs).
- Colliders stripped; NavMesh untouched; deterministic placement.
- Gentle sway on grass/flowers/plants only (NatureSway.cs).

Full audit: Assets/Documentation/ENVIRONMENT_ASSET_SOURCES.md
