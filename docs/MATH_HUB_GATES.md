# MATH HUB GATES — 10 micro-world skeletons

Date: 2026-09-22. Contract: `Assets/A_World/MathWorld/MicroWorldGate.cs`
Builder: `MathWorldBuilder.BuildMicroWorldGates` + `MicroGates` registry.
Tests: CT-P45 (6/6 green). NO gameplay, NO click, NO travel yet.

## 1. Gate-to-Micro-World mapping

| # | GateId | VN label | Pattern identity | Accent | Slot (local) | Visual language |
|---|--------|----------|------------------|--------|--------------|-----------------|
| 01 | counting_garden | Vườn Đếm | COUNT/CHOOSE/COLLECT | leaf green | (-9,-2) | fence arch, bead beam, baskets, count trio |
| 02 | discovery_garden | Vườn Khám Phá | FIND/SEARCH/DISCOVER | mint | (-6,-5) | bush horseshoe, magnifier hoop, pebble trail |
| 03 | fruit_orchard | Vườn Trái Cây | COLLECT/GATHER | berry red | (8.5,.5) | twin fruit trees, apples, basket, leaf beam |
| 04 | match_meadow | Đồng Ghép Cặp | MATCH | aqua | (3,6) | mirrored pillars, paired cubes/balls, balance beam |
| 05 | sorting_park | Công Viên Phân Loại | SORT/CATEGORIZE | blue | (-6,13.5) | 3 color bins + shape toppers, divider rail |
| 06 | puzzle_workshop | Xưởng Xếp Hình | DRAG/DROP/PLACE/ORDER | wood | (3.5,15) | workbench, big blocks, peg board |
| 07 | delivery_village | Làng Giao Hàng | DELIVER/GIVE/BRING | coral | (-10,13) | cottage + roof + door, mailbox + flag, fence |
| 08 | build_yard | Sân Xây Dựng | BUILD/CONSTRUCT | gold | (6,-13.5) | scaffold, stone vs painted stacks, barrel |
| 09 | number_bridge | Cầu Số | PATH/SEQUENCE/ORDER | sky | (19.5,-.5) | mini deck + rails, rill pebbles, bead garland |
| 10 | memory_grove | Rừng Trí Nhớ | MEMORY/RECALL | plum | (-2.5,17) | canopy ring, moon disc, paired stones/cubes |

## 2. Shared gate contract (all 10)

- Root `MathGate_<id>` faces its approach spur (yaw authored).
- `MicroWorldGate` component: gateId / displayName / pattern / accent +
  EntryAnchor (front +1.8z) / ExitAnchor (back −1.8z) / LabelAnchor.
- Sand base disc d3.4 (`MathGateBase`, baked walkable).
- Pill label (`MathGateLabel`, WorldNameLabel @2.7m, catalog VN name).
- Zero colliders, zero raycast targets (walk-to skeleton; clicks fall
  through to ground → ClickRouter walks the child to the mouth).
- Registry: `MathWorldBuilder.MicroGates` + `FindMicroGate(id)`
  (case-insensitive, null-safe). Catalog: `MicroWorldCatalog.All`.

## 3. Skeleton limits (explicit non-goals)

- No Interactable/IClickTarget on gates (would corrupt quest/learning bus).
- No travel wiring (Micro-World 1 adds it on EntryAnchor + GateId).
- No interiors behind gates (gates sit in open zones; backs are dressed).
- No availability/lock visuals (all 10 open as destinations from day one
  of hub review; locking is a progression decision for later).

## 4. Second-consumer proof (reuse)

- Contract consumed 10x by construction (one component, ten instances).
- Readers: CT-P45 (all slots), future Micro-World 1 travel (EntryAnchor),
  future camera beats (anchors, not vectors).
