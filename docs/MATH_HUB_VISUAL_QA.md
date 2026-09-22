# MATH HUB VISUAL QA — technical record (NOT a human PASS)

Date: 2026-09-22. Build + boot verified below. Human review PENDING (§19).
S2 gate-shape pass (2026-09-22): see §5.

## 1. Census (headless BuildContent)

- Before (B1R3): 400 objects. Hub-v1: 601. S1: measured 614
  (−10 spurs +12 ring +2 compass +3 zones +6 blooms; TempCensus 2026-09-22).
  S1-final: measured 619 (−10 filler +3 BG + entry/edge/tuft dressing).
  S2: measured 634 (10 gate frames + bodies − 3 return-arch parts).
  S3: measured 698 (cartoon per-gate silhouettes).
  S4: measured 681 (declutter −17: gate scatter + entry pebbles/tufts).
  S6: measured 777 (beauty + pink: 6 blossom trees + carpets, 8 flower
  drifts, pastel rainbow + cloud feet).
  S7: measured 870 (full bloom: +6 trees, +6 drifts, falling petals,
  5 butterflies, entry blossom crown).
  S8: measured 868 (bridge: deck+rails out, keystone in; pretty arch posts).
  S2 pioneer: measured 938 (Counting Garden micro-world staging + portals).
  Cap re-pinned 640 -> 720 -> 820 -> 920 -> 990 (this file is the record).
- All shared Lit/PropKit materials, 0 colliders, 0 lights, 0 new packages.

## 2. Technical verification (headless + build)

- EditMode full suite 561:556/0/5 (CT-P45 10/10 incl. P45J cartoon gates +
  P45K beauty landmarks; CT-P32 +3 mouse-orbit pins; P41D/P43A return marker;
  P43L subject gates; P43E F9 Spatial Hub cleanup).
- P42A: MathGate* + beauty decor collider-free. P42B: 15 corridor samples clear.
- P42C: budget green (870 < 920). P42D: quest beacon intact.
- P41/P43 geometry pins green (no zone moved); F9 fixed: additive-scene
  subjects build no dead district/return in the Spatial Hub.
- Windows standalone build: see §4. Boot: entry + camera + HUD + quest
  wiring verified via log (FACE_OK path), no exceptions.

## 3. Camera viewpoints for the human reviewer (player camera truth)

1. Spawn (0,0 + follow): lobby → Tess → abacus → return arch → north gates.
2. Walk to (-9,-2): Counting gate mouth + garden behind.
3. Walk to (8.5,.5): Orchard + bridge deck to the east.
4. Walk to (3.5,15)/(−6,13.5)/(−10,13)/(−2.5,17): north gate row panorama.
5. Walk to (6,−13.5)/(19.5,−0.5): south + far-east gates, meadow loop.
6. Return marker (0,12): look back south — hub overview + entry board.

## 4. Build record — S1 (filled after build)

- Result: Succeeded (StandaloneWindows64, release, no Development flag).
  errors=4 (headless-GPU batch noise, zero CS errors), warnings=8 (all
  pre-existing, none from S1 files), size=109,392,409 bytes (~109MB).
- Exe: `D:\Vscode\HubBuild\LWE.exe`. Boot log `D:\Vscode\s1-boot.log`:
  services wired, localcam Live 320x240@30, FACE_OK, HUD 'Choose a gate!',
  0 exceptions. Process stopped cleanly after check (Start-Process +
  explicit kill — no hang).
- S1 camera tweak (+30% spawn height): FollowOffset (0,4.6,6.4)->(0,6.0,8.3),
  arrival CameraAnchor y 5.5->7.2. Rebuild Succeeded errors=0 warnings=3
  size=109,653,960, boot clean (`s1c-boot.log`, FACE_OK).
- S1-final: filler removed, tower/signs relocated, BG/FG/path dressing.
  Rebuild Succeeded errors=0 warnings=5 (pre-existing) size=109,654,472,
  boot clean (`s1f-boot.log`, FACE_OK, 0 exceptions).
- S2 gate-shape (2026-09-22): 10 gate frames + bodies, return marker,
  main-hall subject gates raised. Rebuild Succeeded errors=0 warnings=5
  (pre-existing) size=109,391,897, boot clean (`s2-boot.log`, FACE_OK,
  0 exceptions). TempCensus math=634 / hubShell=289.
- S3 cartoon gates (2026-09-22, user round "các cổng giống nhau quá"): the
  shared frame replaced by per-gate cartoon silhouettes + motifs. Rebuild
  Succeeded errors=0 warnings=5 (pre-existing) size=109,395,481, boot clean
  (`s3-boot.log`, FACE_OK, 0 exceptions). TempCensus math=698 / hubShell=263.
- S4 declutter + camera orbit (2026-09-22, user round "rối mắt / cổng lỗi /
  xoay hướng nhìn"): arch segments tightened (1.06 overlap), workshop jigsaw
  + bench legs fixed, build-yard crane = mast+jib+cable+hook, village roof
  slimmed, bridge/deck tidy; gate ground scatter (trio, bush/pebbles, rill,
  pair balls) and entry pebbles/tufts removed; SmartCamera gains middle/right
  drag orbit (neutral = frozen framing). Rebuild Succeeded errors=0 warnings=4
  size=109,395,481, boot clean (`s4-boot.log`, FACE_OK, 0 exceptions).
  TempCensus math=681.
- S5 gate-shape fix (2026-09-22, user screenshot "Nó đang kiểu gì đây?"):
  Number Bridge deck now seats on the arch crown with chunky parapet posts
  (the thin mid-rails read as sticks); Build Yard crane rebuilt as a gold
  gantry — mast + symmetric jib + hanging load/hook (the thin diagonal arm
  read as a broken scaffold). Rebuild Succeeded errors=0 warnings=4
  size=109,395,993, boot clean (`s5-boot.log`, FACE_OK, 0 exceptions).
- S6 beauty + pink pass (2026-09-22, user rounds "world đẹp hơn" + "gam hồng
  cho con gái"): shared `WorldBeauty` kit (blossom trees, petal carpets, pink
  flower drifts, pastel rainbow) applied to the Math hub AND the Main hub;
  Math clouds tinted pink-white; per-world atmosphere swapped on travel
  (Math: 32-170m soft haze so sky/clouds/rainbow read; Main: unchanged 18-45m
  restored on return). Rebuild Succeeded errors=0 warnings=4 size=109,400,745,
  boot clean (`s6-boot.log`, FACE_OK, 0 exceptions). TempCensus math=777.
- S7 full-bloom (2026-09-22, user round "đẩy tới nóc"): +6 blossom trees &
  drifts, falling pink petals (`PetalFall`, 20 petals over the hub),
  flapping pastel butterflies (`ButterflyDrift`, 5), blossom crown over the
  entry board; Main hub got the same treatment (arch + petals + butterflies);
  Math sky tinted sakura (camera background via atmosphere swap). Rebuild
  Succeeded errors=0 warnings=4 size=109,405,641, boot clean (`s7-boot.log`,
  FACE_OK, 0 exceptions). TempCensus math=870.
- S8 gate fixes (2026-09-22, user screenshots: "cái thanh ở trên, Failed" +
  "chỗ này dính vào cổng" + "cột phải đẹp tương xứng"): Number Bridge top
  plank + tiny rails removed (clean stone arch + keystone + ground boardwalk);
  two blossom trees moved off the real bridge deck / Orchard gate (new
  clearance pins in P45K: trees ≥3.5m, drifts ≥2.0m from every gate body and
  the bridge deck); Main-hall blossom arch posts rebuilt as flared two-tier
  posts with ball caps + green leaf accents. Rebuild Succeeded errors=0
  warnings=4 size=109,406,153, boot clean (`s8-boot.log`, FACE_OK,
  0 exceptions). TempCensus math=868.
- S2 PIONEER MICRO-WORLD (2026-09-22): Counting Garden staging + travel
  portals + area module + anchors + CT-P46 (5/5). Rebuild Succeeded errors=0
  warnings=4 size=109,414,665, boot clean (`s2p-boot.log`, FACE_OK,
  0 exceptions). TempCensus math=938. Full record: docs/S2_COUNTING_GARDEN.md.

## 5. Reviewer questions (§19, unanswered until human plays)

1. Real place? 2. Gates exciting? 3. Visually distinct? 4. Activity
   readable pre-text? 5. Spacious? 6. Navigation obvious w/o HUD?
   7. Prototype leftovers? 8. Enough depth? 9. Cohesive? 10. Presentation bar?
- S2 PIONEER v2 (2026-09-22, user-corrected): Counting Garden = OWN LAZY scene
  (not in-scene staging). WorldTransition micro slot + CountingGardenBuilder
  (5 fenced zones on an arc) + lazy scene arrival in GameInstaller + CT-P46
  6/6 (lazy contract pinned). Suite 567/562/0/5. Rebuild Succeeded errors=0
  warnings=6 size=109,948,697, boot clean (`s2v2-boot.log`, FACE_OK,
  0 exceptions). Record: docs/S2_COUNTING_GARDEN.md.
