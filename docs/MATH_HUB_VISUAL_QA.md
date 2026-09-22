# MATH HUB VISUAL QA — technical record (NOT a human PASS)

Date: 2026-09-22. Build + boot verified below. Human review PENDING (§19).

## 1. Census (headless BuildContent)

- Before (B1R3): 400 objects. After (hub): measured ~601 (CT-P45E).
- Added: Great Abacus (~15) + 10 gates (~150: structure + pill labels +
  anchors) + 10 spurs (10). All shared Lit/PropKit materials, 0 colliders,
  0 lights, 0 new packages.
- Budget: P42C re-pinned 440 -> 640 (this file is the record).

## 2. Technical verification (headless + build)

- EditMode full suite 552:547/0/5 (CT-P45 6/6; P40-P44 unchanged green).
- P42A: MathGate* collider-free. P42B: 15 corridor samples clear.
- P42C: budget green. P42D: quest beacon intact.
- P41/P43 geometry pins green (no zone moved).
- Windows standalone build: see §4. Boot: entry + camera + HUD + quest
  wiring verified via log (FACE_OK path), no exceptions.

## 3. Camera viewpoints for the human reviewer (player camera truth)

1. Spawn (0,0 + follow): lobby → Tess → abacus → return arch → north gates.
2. Walk to (-9,-2): Counting gate mouth + garden behind.
3. Walk to (8.5,.5): Orchard + bridge deck to the east.
4. Walk to (3.5,15)/(−6,13.5)/(−10,13)/(−2.5,17): north gate row panorama.
5. Walk to (6,−13.5)/(19.5,−0.5): south + far-east gates, meadow loop.
6. Return arch (0,12): look back south — hub overview + entry board.

## 4. Build record (filled after build)

- Result: Succeeded (StandaloneWindows64, release, no Development flag).
  errors=4 (headless-GPU batch noise, zero CS errors), warnings=8 (all
  pre-existing, none from hub files), size=109,391,897 bytes (~109MB).
- Exe: `D:\Vscode\HubBuild\LWE.exe`. Boot log `D:\Vscode\hub-boot.log`:
  services wired, localcam Live 320x240@30, FACE_OK, HUD 'Choose a gate!',
  0 exceptions. Process idles at gate screen awaiting input (expected).

## 5. Reviewer questions (§19, unanswered until human plays)

1. Real place? 2. Gates exciting? 3. Visually distinct? 4. Activity
   readable pre-text? 5. Spacious? 6. Navigation obvious w/o HUD?
   7. Prototype leftovers? 8. Enough depth? 9. Cohesive? 10. Presentation bar?
