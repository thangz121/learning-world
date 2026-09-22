# MATH HUB DESIGN — P3.0.1 Micro-World production, Phase 1 (hub only)

Date: 2026-09-22. Status: TECHNICAL PASS, HUMAN REVIEW PENDING (not a visual PASS).
Suite: 552 total / 547 pass / 0 fail / 5 skip (5 pre-existing skips).

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
  4 walking spokes (pinned by CT-P45C) and >=3.4m gate-to-gate.
- 10 approach spurs (`MathPathGate0..9`, walkable flats, P42B-exempt).

## 4. Layout map (local coords, +X east, +Z north)

```
z=19    tree . . tree
z=17      [10 Memory]播
z=15   [5 Sorting] [6 Workshop]
z=13 [7 Village]
z=12  ===== return arch (gold, Về) =====
z=9       [Great Abacus]
z=6    [4 Match] . . Tess(3.4,1.2)
z=2..5   lobby pad . garden path . orchard[3](8.5,.5)
z=0    ============ ENTRY (0,0,0 spawn) ============
z=-2   [1 Counting](-9,-2) . frame . tower(5.6,-6.6)
z=-5   [2 Discovery](-6,-5) . . . bridge deck(15.5,-5)
z=-9..-14  meadow loop . . [8 Build](6,-13.5)
z=-12  ===== entry board (5 beads) =====
[9 Bridge gate](19.5,-.5) far east
```

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
- Census: 400 -> ~601 (CT-P45E measured). Cap re-pinned 440 -> 640
  (P42C), all shared materials, 0 colliders added, 0 lights added.

## 7. Known limitations (for human review)

- Gates are skeleton shells (no interiors, no gameplay, no click-travel).
- Labels are billboard pills (consistent with NPC labels), not painted boards.
- Meadow-loop south + far west stay quiet (intentional negative space).
- Exact staging (label heights, prop density) is human-tunable via named
  hierarchy nodes (`MathGate_<id>` roots).
