# Quaternius Nature (CC0) — provenance

Source: "LowPoly Nature Pack" by Quaternius (CC0 1.0 Universal),
via OpenGameArt.org (`Nature pack vol.3.zip`, 1.2 MB, 2018-04-05):
https://opengameart.org/content/lowpoly-nature-pack
Same artist + license as the character rigs (`ThirdParty/Quaternius/*.fbx`).

Imported 2026-09-21 (Phase 3.0.1.1, user-ordered GitHub decor round):
13 FBX only (Blender/OBJ variants NOT imported):
Tree1-4, Bush1-3, Grass1-3, Rock1-3 → `Assets/A_World/Resources/NatureKit/`
(Resources path so code-built worlds can load them deterministically in
players AND headless EditMode tests).

Rules: never edit the imports (scale/material fixes live in
`NatureKit.cs` at placement time: URP Lit conversion, click-through,
bake/mesh behaviour documented per call-site). CC0: no attribution
required; this note is provenance only.
