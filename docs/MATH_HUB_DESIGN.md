# MATH HUB DESIGN — P3.0.1 Micro-World production, Phase 1 (hub only)
# S1 REDESIGN (2026-09-22): arc/court composition + ring circulation.
# S2 GATE-SHAPE PASS (2026-09-22, user round): every destination gate is a
# real gate silhouette (frame + motif); the return arch became a marker.
# S3 CARTOON PASS (2026-09-22, user round): the shared frame is gone — each
# gate got its own cartoon silhouette + motif matching its micro-world.

Date: 2026-09-22. Status: TECHNICAL PASS, HUMAN REVIEW PENDING (not a visual PASS).
Suite: 557 total / 552 pass / 0 fail / 5 skip (5 pre-existing skips).

## 1. Concept

Math Village: a real 3D selection hub, not a menu. Entry south (z=-12),
return arch north (z=+12), lobby courtyard at center, quest garden west,
bridge east (both keep the pilot `math_counting` quest working), Great
Abacus landmark north-center, 10 gate structures in the surveyed free
zones A-E. Spawn view reads: lobby → Tess → abacus → return arch → gates.

## 2. What was kept (journey pins)

- ALL pilot zones at exact positions: lobby pad/frame, host nook + Tess
  spawn (63.4,0,1.2 world), garden + OneCube + bloom, bridge + clearing,
  entry/return arches, sign, tower, dominoes, paths, nature, sun, rim.
- Travel contract untouched: WorldOffset, EntryPoint, FollowOffset,
  SignWorldPos, Bound 27, 8 presentation anchors, return-gate Math binding,
  AdoptAll, tunnel/HUD order. (CT-P40..P43 green,unchanged.)

## 3. What was added

- Great Abacus landmark at local (5.5, 0, 9.5): 2 blue posts h3.2, 3 rods,
  9 gold/red beads, sand base. North star for orientation.
- 10 gates (see MATH_HUB_GATES.md) at surveyed slots, each >=2.0m off all
  4 walking spokes (pinned by CT-P45C) and >=3.4m gate-to-gate. S2: gates
  became real silhouettes; S3 (cartoon pass): the shared frame was replaced
  by the cartoon kit — two named cartoon legs at ±1.6 + a per-gate chunky
  arch/topper (see GATES §2), all standing 2.0m behind the ring waypoint so
  the gates read from the lobby AND the 1.5m ring keeps its full width clear.
- Return = gold disc (d2.6) + "Về" label + invisible trigger (S2: the old
  gold arch was removed — the way home must not compete with the gates).
- 10 approach spurs (`MathPathGate0..9`, walkable flats, P42B-exempt).

## 4. Layout map — S1 ARC (local coords, +X east, +Z north)

All 10 gates sit ON a circulation ring (r~13-16) facing the hub center.
Ring waypoints (exposed as `RingWaypoints`, lighter sand `MathRing00-11`):
Sorting → Match → Orchard → bridge deck (ford) → NumBridge → Build →
Memory → Village → Counting → fence-mid → Discovery → Workshop → close.

```
z=19    tree . . tree
z=17      . . .
z=15   [5 Sorting](6.3) [6 Workshop](-5.1)
z=13 [7? no—Village SW] [2 Discovery](-9.6,12.5)
z=12  ===== return arch (gold, Về) =====
z=10.6    [4 Match](11.5) . [Abacus](5.5,9.5)
z=6     Tess(3.4,1.2) . lobby pad
z=4.9   [3 Orchard](14.5)
z=2..5   garden path . . .
z=0    ============ ENTRY (0,0,0 spawn) ============
z=-2/-3  [1 Counting](-11,-3) . frame . tower(5.6,-6.6)
z=-5    deck(15.5,-5) . stream
z=-8.6  [9 NumBridge](13.2)
z=-9..-13 meadow loop . [8 Build](6.3,-12.6) [10 Memory](-6.3,-12.6)
z=-10.6 [7 Village](-10.6)
z=-12  ===== entry board (5 beads) =====
```

S1 changes vs hub-v1: gates moved from scattered free zones onto the ring;
10 spoke-spurs replaced by the loop; shape trio relocated from the garden
mouth to the abacus flanks (composed totems); compass medallion at lobby
(0,1); 3 zone tint discs (N/W/E); 3 accent flower clusters on the ring.
Arrival camera re-staged to a south-high wide shot into the hub
(Camera (0,5.5,-11) / Look (0,1.5,5) — scene-authored anchors).

## 5. Shared art direction (one world, §14)

Shared: island ground + sand paths, URP/Lit flat palette (blue/gold/wood/
soil/leaf/stone), Kenney + Quaternius CC0 kits, fence-wood beams, sand
bases, pill labels (WorldNameLabel, billboard, 2.7m). Per-gate identity via
silhouette + motif props + ONE accent color (see GATES doc).

## 6. Spacing / NavMesh / collision

- Gates are collider-free (P42 dressing rule); posts bake as off-path
  NavMesh obstacles (no through-walk, no corridor blockers).
- Spurs are baked walkable flats. 15 P42 corridor samples stay clear
  (P42B green). Return/entry/garden/bridge spokes untouched.
- Census: 400 -> 601 (hub-v1) -> 614 (S1) -> 619 (S1-final, TempCensus)
  -> 634 (S2 gate frames + bodies − return arch)
  -> 698 (S3 cartoon gates)
  -> 681 (S4 declutter)
  -> 777 (S6 beauty + pink pass)
  -> 870 (S7 full bloom)
  -> 868 (S8 gate fixes; TempCensus 2026-09-22).
  Cap re-pinned 640 -> 720 -> 820 -> 920 (recorded in
  MATH_HUB_VISUAL_QA.md). All shared Lit/PropKit materials, 0 colliders,
  0 lights.

## 8. S1-FINAL composition pass (2026-09-22, human-directed)

- Depth: 3 background silhouette blobs (N1/N2/E) + tower moved east-backdrop.
- Foreground: entry bushes (±2.5,-11) frame the arrival shot; tower and +/−
  signs removed from the east foreground (tower → (17,9) backdrop,
  signs → entry flanks (±2.8,-8) as welcome markers).
- Declutter: 2 stray pebbles + 4 far-flung blooms deleted (return flowers
  kept as arch framing). Shape trio already totems at the abacus.
- Path integration: 6 entry edge pebbles + 4 spoke-side grass tufts.
- Camera: UNCHANGED (approved direction; small-anchor tuning deferred —
  no new evidence of a framing defect, blind tuning risks worse).
- PIP box: explicitly deferred per human instruction (Full HD ratio correct).

## 7. Known limitations (for human review)

- Gates are cartoon gate skeletons: real unique silhouettes + motifs, but no
  interiors, no gameplay, no click-travel.
- Labels are billboard pills (consistent with NPC labels), not painted boards.
- Meadow-loop south + far west stay quiet (intentional negative space).
- Exact staging (label heights, prop density) is human-tunable via named
  hierarchy nodes (`MathGate_<id>` roots).

## 9. S2 gate-shape pass (2026-09-22, human-directed)

- User round: "quá nhiều cổng thừa (cổng về) nhưng cổng cần dev thì lại quá
  mờ nhạt — chưa ra hình hài của cổng." Applied to BOTH areas:
- Math hub: all 10 gates rebuilt as real gates (shared frame + pattern motif,
  see MATH_HUB_GATES.md §2); the gold return arch removed -> marker only.
- Main hall: the 4 subject gates raised into real gate silhouettes (Math
  pillars 2.45m + beam, Thinking gear pillars 2.1m + puzzle beam at 2.62m,
  English book pillars 2.3m + beam 2.5m + crown, Vietnamese tablets 2.1m +
  banner 2.5m + hat ~3.1m). Name boards mounted ON the beam faces.
- Nav unchanged: every beam is bake-ignored (headroom rule), pillar
  positions/carves untouched, ring/roads keep their corridors.
- Pins updated deliberately: P41D (return disc), P43A (marker not gate),
  + new P43L (subject gates are real gates) and P45J (gate frames).
- Spatial Hub F9 cleanup (same round): `SubjectWorldBuilder.BuildShell` now
  honors `SceneName` — Math keeps its hub gate + signpost but builds no dead
  duplicate district (medallion/core/tree/road) and no stray "Về" marker in
  MarketScene; the return slot stays catalog-aligned as a null so subject
  binding indices cannot shift (P43E re-pinned: 3 spatial returns).

## 10. S3 cartoon pass (2026-09-22, human-directed)

- User round: "Các cổng đang giống nhau quá… muốn làm cổng dạng cartoon một
  chút và phải đúng với bản chất của từng micro world."
- Fix: the shared post-and-lintel frame was replaced by a cartoon kit
  (`CartoonPost`, `BlockPost`, `StripedPost`, `CartoonArch`, `CartoonRing`,
  `MushroomPost`) and each gate got a bespoke silhouette + signature motif
  (beads, magnifier+glass, fruit trees with hanging apples, mirrored halves +
  crown, rainbow + shape toppers + bins, interlocking jigsaw planks, cottage
  roof + parcels, hazard legs + crane hook, stone arch bridge + deck, spotted
  mushrooms + moon). Rounded legs with ball caps + chunky arcs = cartoon read.
- Nav unchanged: every arch/span piece is bake-ignored (headroom rule), legs
  are collider-free and bake as off-path obstacles, ring stays clear (body
  offset GateBodyZ=-2.0 kept).
- Pins updated deliberately: P45J now pins the per-gate legs, bake-ignored
  arch pieces and the signature motif map; P42C/P45E budget re-pinned to 720
  (measured 698) — recorded in MATH_HUB_VISUAL_QA.md.

## 11. S4 declutter + camera orbit (2026-09-22, human-directed)

- User round (3 screenshots): "còn 1 số thứ đang bị rối mắt quá. Cổng vẫn có
  cái bị lỗi. Cần giữ chuột/con lăn để điều chỉnh hướng nhìn map."
- Gates: arch segment overlap 1.25 -> 1.06 (no more jutting corners), workshop
  jigsaw re-locked + bench legs restored, build-yard crane rebuilt as
  mast+jib+cable+hook (was a diagonal plank that read as a seesaw), village
  roof slimmed to the post span, bridge/sorting/memory ground scatter trimmed.
- Declutter: gate scatter (counting trio, discovery bush+pebbles, bridge rill
  pebbles, memory pair balls) and the entry edge pebbles + spoke grass tufts
  removed (the domino walk + chevrons already guide the eye). Census 698 -> 681.
- Camera (global, `SmartCamera`): hold the middle (wheel) OR right mouse
  button and drag to orbit the Follow view — yaw free, pitch clamped
  (−22°..+42°); wheel zoom unchanged. Neutral orbit (0,0) + zoom 1.0 exactly
  reproduces the frozen authored framing, so every beat/camera contract holds.
  Contract note added to CONSTRAINED_3D.md §2; pins CT-P32E/F/G.

## 12. S6 beauty + pink pass (2026-09-22, human-directed)

- User rounds: "biến world này trông đẹp hơn — sửa bất cứ gì" + "thêm gam
  hồng vì đây là game cho con gái là chủ yếu".
- New shared kit `A_World/WorldBeauty.cs` (primitives, shared materials,
  collider-free): `BlossomTree` (trunk + 3 pink canopy balls), `PetalCarpet`,
  `FlowerDrift` (pink-forward pastel pair), `PastelRainbow` (6 bands pink →
  lilac + pink cloud feet), `ApplyMainAtmosphere`/`ApplyMathAtmosphere`.
- Math hub: 6 blossom trees + carpets on the lawns, 8 flower drifts at the
  lobby/entry, pastel rainbow landing at (0,0,21) r11 behind the north gates
  (the arrival/lobby view looks north), clouds tinted pink-white.
- Main hub: 4 blossom trees + carpets + 6 flower drifts on the hub lawns
  (guarded by clear-zone/walkway/gate-plaza predicates).
- Atmosphere: MarketBootstrap swaps fog/ambient on travel — Math gets a soft
  32-170m haze (sky, clouds and the rainbow read; Main's 18-45m fog washed
  them out) and Main restores its crisp 18-45m air on return.
- Pins: P45K (beauty landmarks + rainbow bake-ignored), P42A prefixes
  extended, P42C/P45E budget re-pinned to 820 (measured 777). Art direction
  recorded in GAME_DESIGN.md §8 (pink-led, green/sky base kept for contrast).

## 13. S7 full-bloom (2026-09-22, human-directed "đẩy tới nóc")

- User: "Đẹp đấy, đẩy mạnh hơn nữa… đẩy tới nóc đi."
- Shared kit additions (`WorldBeauty` + 2 new components):
  `PetalFall` (deterministic pink petals drifting over the hub, transform-only
  animation), `ButterflyDrift` (pastel butterflies orbiting the flower spots,
  wings flapping), `TrunkPost` (bare trunk for the blossom arch).
- Math hub: 12 blossom trees + carpets on every lawn, 14 pink flower drifts,
  20 falling petals over the lobby, 5 butterflies, blossom crown over the
  entry board; sakura sky tint (camera background) via the atmosphere swap.
- Main hub: 6 blossom trees + carpets, 10 drifts, blossom arch framing the
  path north of the spawn (posts at ±1.7 outside the corridor, canopy 2.3m+
  clearance), 12 falling petals, 2 butterflies.
- Budget: measured 870, cap re-pinned 820 -> 920 (MATH_HUB_VISUAL_QA.md).
- Pins: P45K extended (petals/butterflies/entry crown), P42A prefixes
  extended; suite 561:556/0/5.

## 14. S8 gate fixes (2026-09-22, human screenshots)

- User: (1) "Cái này còn 1 cái thanh ở trên, Failed" — the Number Bridge
  gate's wooden top plank + tiny parapet rails read as a board stuck on the
  arch; (2) "chỗ này đang bị dính vào cổng" — a S7 blossom tree (17.5,-2.5)
  visually stuck to the real Number Bridge deck; (3) "cái cổng ở sân spawn
  có đám mây trên cổng… làm cột cũng phải đẹp tương xứng" — the Main-hall
  blossom arch used bare trunk posts.
- Fixes: bridge = clean stone arch + keystone + ground boardwalk (deck/rails
  removed, P45J motif re-pinned to `MathGateKeystone`); blossom trees moved to
  (21,-1.5)/(21.5,8.5) and (-9.5,-7.5); `WorldBeauty.PrettyPost` (flared foot
  + tapered shaft + ball cap) now carries the spawn arch, topped with the
  blossom cloud + two green leaf accents.
- New clearance pins in P45K: every blossom tree ≥3.5m and every flower drift
  ≥2.0m from every gate body AND from the real bridge deck / garden centre —
  the "stuck to the gate" class can't come back.
