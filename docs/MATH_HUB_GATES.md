# MATH HUB GATES — 10 cartoon micro-world gates (S3 cartoon pass)

Date: 2026-09-22 (S1 ring layout; S2 real-gate pass; S3 cartoon + identity).
Contract: `Assets/A_World/MathWorld/MicroWorldGate.cs`
Builder: `MathWorldBuilder.BuildMicroWorldGates` + `MicroGates` registry.
Tests: CT-P45 (9/9 green, incl. P45J per-gate silhouette + motif pins).
NO gameplay, NO click, NO travel yet (skeleton scope unchanged — the SHAPE is
now cartoon and world-specific).

## 1. Gate-to-Micro-World mapping (S1 slots, local)

| # | GateId | VN label | Pattern | Cartoon silhouette (S3) | Accent | Slot | Faces |
|---|--------|----------|---------|--------------------------|--------|------|-------|
| 01 | counting_garden | Vườn Đếm | COUNT/CHOOSE/COLLECT | toy 1-2-3 block legs + green arch with a bead count row hanging under it | leaf green | (-11,-3) | hub |
| 02 | discovery_garden | Vườn Khám Phá | FIND/SEARCH/DISCOVER | the gate IS a giant magnifier: mint ring + pale glass + handle | mint | (-9.6,12.5) | hub |
| 03 | fruit_orchard | Vườn Trái Cây | COLLECT/GATHER | the legs ARE fruit trees (trunk+canopy), leafy branch arch, apples hanging | berry red | (14.5,4.9) | hub |
| 04 | match_meadow | Đồng Ghép Cặp | MATCH | mirrored halves (aqua/gold) meeting at a pink crown + paired shapes | aqua | (11.5,10.6) | hub |
| 05 | sorting_park | Công Viên Phân Loại | SORT/CATEGORIZE | rainbow arch + shape toppers over three colour bins | blue | (6.3,14.6) | hub |
| 06 | puzzle_workshop | Xưởng Xếp Hình | DRAG/DROP/PLACE/ORDER | two stepped interlocking jigsaw planks over a workbench | wood | (-5.1,15.1) | hub |
| 07 | delivery_village | Làng Giao Hàng | DELIVER/GIVE/BRING | cottage gate: pitched roof + chimney + ridge gold + parcels | coral | (-10.6,-10.6) | hub |
| 08 | build_yard | Sân Xây Dựng | BUILD/CONSTRUCT | hazard-striped site legs + gold gantry crane (mast + jib + load/hook) + material stacks | gold | (6.3,-12.6) | hub |
| 09 | number_bridge | Cầu Số | PATH/SEQUENCE/ORDER | chunky stone arch + keystone + ground boardwalk | sky | (13.2,-8.6) | hub |
| 10 | memory_grove | Rừng Trí Nhớ | MEMORY/RECALL | spotted mushroom legs + dusk-plum arch with the moon + mini pair | plum | (-6.3,-12.6) | hub |

## 2. Shared gate contract (all 10)

- Root `MathGate_<id>` faces its approach spur (yaw authored); registry slot
  and spacing pins unchanged (P45C).
- Cartoon kit legs (S3): every gate stands on two legs NAMED
  `MathGatePostL/R` at local X ±1.6 — rounded post + ball cap, toy blocks,
  hazard stripes, tree, stone or mushroom depending on the world. Legs are
  collider-free and bake as off-path obstacles (feet pass between them).
- Own silhouette: each gate ships its own `MathGateArch*` span (half-ellipse
  arch, magnifier ring, jigsaw planks, cottage roof, crane beam, stone arch)
  in its accent/pattern colours; every arch piece is `ignoreFromBuild`
  (headroom rule). No gate repeats the old shared post-and-lintel frame.
- `MathGateBody` offset: the whole body (legs + span + motif) stands at
  `GateBodyZ = -2.0` local Z, i.e. BEHIND the ring waypoint. The 1.5m
  circulation ring keeps its full width clear while every gate fronts the hub.
- Motif: the per-gate signature object is pinned by P45J (beads, glass,
  basket, crown, bins, peg, mailbox, crane hook, bridge deck, moon).
- Name pill: `WorldNameLabel` @3.95m anchored to the body (floats above
  every crown/topper), catalog VN name, unchanged label contract (P45D).
- `MicroWorldGate` component unchanged: gateId / displayName / pattern /
  accent + EntryAnchor (front +1.8z) / ExitAnchor (back −1.8z) / LabelAnchor
  (on the body). Sand base disc d3.4 (`MathGateBase`, baked walkable).
- Zero colliders, zero raycast targets (walk-to skeleton; clicks fall
  through to ground → ClickRouter walks the child to the mouth).

## 3. Skeleton limits (explicit non-goals)

- No Interactable/IClickTarget on gates (would corrupt quest/learning bus).
- No travel wiring (Micro-World 1 adds it on EntryAnchor + GateId).
- No interiors behind gates (gates sit in open zones; backs are dressed).
- No availability/lock visuals (all 10 open as destinations from day one
  of hub review; locking is a progression decision for later).
- Return is a MARKER, not a gate (S2 user round "quá nhiều cổng thừa"): the
  gold disc + "Về" label + invisible trigger stay; the old arch pillars/beam
  are gone (P43A pins the absence).

## 4. Second-consumer proof (reuse)

- Contract consumed 10x by construction (one component + one cartoon kit,
  ten bespoke silhouettes).
- Readers: CT-P45 (slots, legs, arch, motif, budget), future Micro-World 1
  travel (EntryAnchor), future camera beats (anchors, not vectors).
