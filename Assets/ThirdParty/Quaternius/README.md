# ThirdParty/Quaternius — character art provenance

Source: "Ultimate Animated Character Pack" by quaternius
- https://quaternius.com/packs/ultimatedanimatedcharacter.html
- Mirror used: https://opengameart.org/content/animated-characters-pack
- License: CC0 1.0 Universal (public domain; commercial use allowed, no
  permission needed). Credit: quaternius (appreciated, not required).

Used in this project (copied FBX only, nothing else from the 48 MB pack):
- Worker_Male.fbx → Milo (hard-hat market helper)
- Worker_Female.fbx → Mia (shopkeeper)

Rejected after Game-view review (kept out to stay lean):
- Casual_Female.fbx, Chef_Female.fbx (fine models, weaker market-crew fit)

Adaptations (all in code, imported sub-assets stay pristine):
- Instance material tints (orange vest for Milo, coral vest for Mia, warm
  tan faces, warm brown skin) for preschool readability at gameplay distance.
- Geometric doll face kit (eyes/smile, URP/Lit) parented to the Head bone.
- Procedural arm-wave layer (LateUpdate) for greeting/click feedback.
- Runtime Animator triggers Celebrate (Victory clip) and PickUp clip.

Generated from these FBX (by temp Editor tooling, then tooling deleted):
- Assets/B_Brain/Visuals/Resources/NpcVisuals/MiloVisual.prefab
- Assets/B_Brain/Visuals/Resources/NpcVisuals/MiaVisual.prefab
- Assets/B_Brain/Visuals/Resources/NpcVisuals/MiloController.controller
- Assets/B_Brain/Visuals/Resources/NpcVisuals/MiaController.controller
  (states Idle + Victory + PickUp; triggers Celebrate/PickUp)
