// A_World/SubjectWorldBuilder.cs — Phase 3.0 WORLD FOUNDATION (Agent A).
// Code-builds the Learning World extension: 4 roads, 4 subject gates
// (landmark arches), 4 playgrounds (medallion + landmark core + return arch +
// boundary ring + tree), 4 signposts, outer hedge at the new bounds.
// Split to respect the frozen NavMesh lifecycle (MarketBuilder pattern):
//   BuildShell  — PRE-bake: walkable ground treatment (roads, medallions) +
//                 solid landmarks (pillars/cores/trees keep colliders, like the
//                 stall/crates) so the bake sees them; carves flush afterwards.
//   BuildCarves — runtime stationary carves (deny feet, never rebake).
//   BuildDecor  — POST-bake: collider-free dressing (bushes/blooms/pebbles).
// Every placement is hand-authored (deterministic, no Random — only a fixed
// seed for intra-cluster jitter, same convention as MarketBuilder).
// Visual identities differ in SHAPE LANGUAGE (blocks/gears/books/scrolls),
// not just color. No lesson/question/topic anywhere. C# 9.0 only.
using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

public static class SubjectWorldBuilder {
  public class CarveSpec {
    public string Name;
    public Vector3 Pos;
    public Vector3 Size;
  }

  public class BuildResult {
    public readonly List<SubjectGate> EntryGates = new List<SubjectGate>();
    public readonly List<SubjectGate> ReturnGates = new List<SubjectGate>();
    public readonly List<CarveSpec> Carves = new List<CarveSpec>();
  }

  // World extension contract (metres). Ground must cover these (MarketBuilder
  // enlarges the plane accordingly); ClickRouter bounds cover them too.
  public const float OuterHedgeX = 17f;
  public const float OuterHedgeZ = 14.5f;

  static readonly Color RoadTan = new Color(0.76f, 0.60f, 0.40f);
  static readonly Color ReturnGold = new Color(0.95f, 0.75f, 0.30f);
  static readonly Color ReturnGoldDark = new Color(0.70f, 0.52f, 0.20f);
  static readonly Color LeafA = new Color(0.28f, 0.60f, 0.30f);
  static readonly Color LeafB = new Color(0.22f, 0.52f, 0.28f);
  static readonly Color TrunkC = new Color(0.45f, 0.30f, 0.16f);
  static readonly Color StrawC = new Color(0.85f, 0.70f, 0.42f);
  static readonly Color[] PastelBlooms = {
    new Color(0.95f, 0.55f, 0.65f),
    new Color(0.96f, 0.95f, 0.90f),
    new Color(0.98f, 0.82f, 0.30f),
    new Color(0.75f, 0.60f, 0.90f),
  };

  // ---- SHELL (pre-NavMesh-bake) ---------------------------------------------

  public static BuildResult BuildShell(Transform parent) {
    BuildResult result = new BuildResult();
    foreach (SubjectDefinition def in SubjectCatalog.All) {
      BuildRoad(parent, def);
      BuildEntryGate(parent, def, result);
      BuildPlaygroundShell(parent, def, result);
      BuildSignpost(parent, def);
    }
    BuildOuterHedgeShell(parent, result);
    return result;
  }

  // ---- CARVES (runtime, after the bake — same pattern as BuildNavCarves) -----

  public static void BuildCarves(BuildResult result, Action<string, Vector3, Vector3> addCarve) {
    if (result == null || addCarve == null) return;
    foreach (CarveSpec c in result.Carves) {
      try { addCarve(c.Name, c.Pos, c.Size); } catch (Exception) { }
    }
  }

  // ---- DECOR (post-bake, collider-free) --------------------------------------

  public static void BuildDecor(Transform parent) {
    System.Random rng = new System.Random(30300);
    foreach (SubjectDefinition def in SubjectCatalog.All) {
      BuildBoundaryRing(parent, def, rng);
      BuildPlaygroundDecor(parent, def, rng);
      BuildRoadEdges(parent, def, rng);
    }
    BuildOuterHedgeDressing(parent, rng);
  }

  // ---- roads -----------------------------------------------------------------
  // One flat walkable strip per subject, same warm tan as the main path so all
  // roads read as ONE world. Pre-bake (walkable). Colliders kept (matches Path).

  static void BuildRoad(Transform parent, SubjectDefinition def) {
    Vector3 gate = def.GatePos;
    Vector3 center = def.PlaygroundCenter;
    if (def.Id == SubjectIds.Math) {
      // East: from inside Main (x 5.6) through the gate to the playground.
      Box(parent, "MathRoad", new Vector3(8.9f, 0.015f, gate.z),
        new Vector3(6.6f, 0.03f, 1.6f), RoadTan, true);
    } else if (def.Id == SubjectIds.Thinking) {
      Box(parent, "ThinkingRoad", new Vector3(-8.9f, 0.015f, gate.z),
        new Vector3(6.6f, 0.03f, 1.6f), RoadTan, true);
    } else {
      // North/South along the gate's own x (Vietnamese runs at x=3.5 so the
      // spawn camera axis x=0 stays clear): from inside Main to the playground.
      float gx = gate.x;
      float z0 = center.z < gate.z ? -3.6f : 3.6f;
      float zMid = (z0 + center.z) * 0.5f;
      float len = Math.Abs(center.z - z0);
      string roadName = def.Id == SubjectIds.English ? "EnglishRoad" : "VietnameseRoad";
      Box(parent, roadName, new Vector3(gx, 0.015f, zMid),
        new Vector3(1.6f, 0.03f, len), RoadTan, true);
    }
  }

  // ---- entry gates ------------------------------------------------------------
  // Landmark arch ON the hedge line + subject label + one-way entry trigger
  // (Main -> subject). Pillars keep colliders AND get carves: feet pass
  // BETWEEN them, never through them.

  static void BuildEntryGate(Transform parent, SubjectDefinition def, BuildResult result) {
    string name = def.DisplayName;
    GameObject root = new GameObject(name + "Gate");
    root.transform.SetParent(parent);
    root.transform.position = def.GatePos;

    // Hub-arc facing: the arch faces the hub (players approach from the
    // yard), NOT the district. Lateral axis fans outward from the arc middle
    // so each gate's side dressing trails AWAY from its neighbours.
    Vector3 face = FaceOf(def);
    Vector3 lat = new Vector3(-face.z, 0f, face.x);
    if (lat.x * def.GatePos.x < 0f) lat = -lat;
    // Pillars at ±1.6 (bake erosion 0.5 + carves leave a 1.4m+ corridor).
    Vector3 pillarA = def.GatePos + lat * 1.6f;
    Vector3 pillarB = def.GatePos - lat * 1.6f;

    switch (def.Landmark) {
      case SubjectLandmarkKind.Blocks: BuildBlocksGate(parent, def, pillarA, pillarB, lat); break;
      case SubjectLandmarkKind.Gears: BuildGearsGate(parent, def, pillarA, pillarB, lat); break;
      case SubjectLandmarkKind.Books: BuildBooksGate(parent, def, pillarA, pillarB, lat); break;
      case SubjectLandmarkKind.Scrolls: BuildScrollsGate(parent, def, pillarA, pillarB, lat); break;
    }

    // Tinted medallion under the gate (walkable ground treatment).
    // Hub-arc size (2.6m): full 3.2m discs would touch on the ~3.6m spacing.
    GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    disc.name = name + "GateDisc";
    disc.transform.SetParent(parent);
    disc.transform.position = def.GatePos + new Vector3(0f, 0.012f, 0f);
    disc.transform.localScale = new Vector3(2.6f, 0.024f, 2.6f);
    disc.GetComponent<Renderer>().sharedMaterial = Lit(def.GroundTint);

    // In-world name (reuses the frozen WorldNameLabel contract). The anchor
    // sits BEHIND the arch (district side, 1.3m off the gate line) at 3.2m:
    // the pill floats above every gate topper (VN straw hat peaks at 2.63m,
    // English crown at 2.3m) so low hub-side cameras read it OVER the beam
    // instead of through the dressing. Y-billboard keeps it readable from
    // every approach angle.
    GameObject anchor = new GameObject(name + "GateAnchor");
    anchor.transform.SetParent(parent);
    anchor.transform.position = def.GatePos - face * 1.3f;
    GameObject labelGo = new GameObject(name + "GateLabel");
    labelGo.transform.SetParent(parent);
    WorldNameLabel label = labelGo.AddComponent<WorldNameLabel>();
    label.Setup(def.DisplayName, anchor.transform, 3.2f);
    label.Show();

    result.Carves.Add(new CarveSpec { Name = name + "PillarCarveA", Pos = pillarA + new Vector3(0f, 1f, 0f), Size = new Vector3(0.8f, 2f, 0.8f) });
    result.Carves.Add(new CarveSpec { Name = name + "PillarCarveB", Pos = pillarB + new Vector3(0f, 1f, 0f), Size = new Vector3(0.8f, 2f, 0.8f) });

    SubjectGate gate = root.AddComponent<SubjectGate>();
    gate.fireRadius = 1.2f;
    result.EntryGates.Add(gate);
  }

  // Math: stacked-cube pillars + cylinder lintel + finial spheres + 3
  // ascending counting cubes beside the road (geometry motif, no curriculum).
  static void BuildBlocksGate(Transform parent, SubjectDefinition def, Vector3 a, Vector3 b, Vector3 lat) {
    BuildCubePillar(parent, "MathPillarA", a, def.Primary);
    BuildCubePillar(parent, "MathPillarB", b, def.Primary);
    Vector3 mid = (a + b) * 0.5f + new Vector3(0f, 1.85f, 0f);
    GameObject lintel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    lintel.name = "MathLintel";
    lintel.transform.SetParent(parent);
    lintel.transform.position = mid;
    // Beam spans the pillars with a short overhang (was 6.8m: filled every
    // closeup frame once gates became the hub stars — survey photo proof).
    lintel.transform.localScale = new Vector3(0.36f, 1.95f, 0.36f);
    // Exact beam axis (was an X/Z snap: connected from the front, floated
    // from the side on diagonal hub-arc gates).
    lintel.transform.localRotation = Quaternion.FromToRotation(Vector3.up, lat);
    lintel.GetComponent<Renderer>().sharedMaterial = Lit(def.Secondary);
    // Headroom rule (P3 survey telemetry: PathPartial at the gate line):
    // the runtime bake rasterizes RENDER MESHES with agentHeight 2m, so ANY
    // beam below 2m clearance severs the road into islands (stripping the
    // collider alone does NOT help). Beams are visual-only: no collider +
    // ignoreFromBuild, feet pass under, pillars keep meshes + carves.
    StripCollider(lintel);
    IgnoreFromBuild(lintel);
    // Shape duo (geometry family): cube + sphere finials — the Math gate
    // reads as shapes before any color does. (User round: counting cubes +
    // abacus row removed — side clutter around a hub gate.)
    Box(parent, "MathFinialA", a + new Vector3(0f, 1.85f, 0f),
      new Vector3(0.36f, 0.36f, 0.36f), def.Primary, false);
    Ball(parent, "MathFinialB", b + new Vector3(0f, 1.85f, 0f), 0.42f, def.Secondary, false);
  }

  static void BuildCubePillar(Transform parent, string pillarName, Vector3 basePos, Color color) {
    Box(parent, pillarName + "Base", basePos + new Vector3(0f, 0.35f, 0f),
      new Vector3(0.7f, 0.7f, 0.7f), color, true);
    Box(parent, pillarName + "Top", basePos + new Vector3(0f, 1.05f, 0f),
      new Vector3(0.55f, 0.7f, 0.55f), color, true);
  }

  // Thinking: gear-wheel pillars (cylinder + teeth) + slotted puzzle lintel +
  // broken square maze ring flat on the ground.
  static void BuildGearsGate(Transform parent, SubjectDefinition def, Vector3 a, Vector3 b, Vector3 lat) {
    BuildGearPillar(parent, "ThinkingGearA", a, def.Primary);
    BuildGearPillar(parent, "ThinkingGearB", b, def.Primary);
    Vector3 mid = (a + b) * 0.5f;
    // Puzzle lintel: two slabs leaving a center notch (interlock motif).
    // Visual-only (no collider + ignoreFromBuild): the road must pass UNDER.
    Vector3 along = lat;
    GameObject lintelL = Box(parent, "ThinkingLintelL", mid + along * 0.95f + new Vector3(0f, 1.95f, 0f),
      SlabScale(along, 1.5f), def.Secondary, false);
    GameObject lintelR = Box(parent, "ThinkingLintelR", mid - along * 0.95f + new Vector3(0f, 1.95f, 0f),
      SlabScale(along, 1.5f), def.Secondary, false);
    lintelL.transform.localRotation = Quaternion.Euler(0f, YawAlongX(along), 0f);
    lintelR.transform.localRotation = Quaternion.Euler(0f, YawAlongX(along), 0f);
    IgnoreFromBuild(lintelL);
    IgnoreFromBuild(lintelR);
    // Puzzle tab locking the center notch (interlock motif, visual-only).
    // (User round: ground maze ring removed — side clutter around a hub gate.)
    GameObject tab = Box(parent, "ThinkingTab", mid + new Vector3(0f, 2.15f, 0f),
      new Vector3(0.3f, 0.5f, 0.3f), def.Primary, false);
    IgnoreFromBuild(tab);
  }

  static Vector3 SlabScale(Vector3 along, float len) {
    // Long axis is ALWAYS local X; callers yaw the slab onto `along` with
    // YawAlongX (exact for diagonal hub-arc gates — axis snaps looked
    // connected from the front but floated from the side).
    return new Vector3(len, 0.3f, 0.5f);
  }

  // Hub-arc facing shared by every gate (players approach from the yard).
  static Vector3 FaceOf(SubjectDefinition def) {
    Vector3 f = SubjectCatalog.HubCenter - def.GatePos;
    f.y = 0f;
    if (f.sqrMagnitude < 0.001f) f = new Vector3(0f, 0f, 1f);
    return f.normalized;
  }

  // Yaw (degrees about Y) sending local +X onto `lat`, / local +Z onto `dir`.
  static float YawAlongX(Vector3 lat) {
    return Mathf.Atan2(-lat.z, lat.x) * Mathf.Rad2Deg;
  }

  static float YawFaceZ(Vector3 dir) {
    return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
  }

  static void BuildGearPillar(Transform parent, string gearName, Vector3 basePos, Color color) {
    GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    wheel.name = gearName + "Wheel";
    wheel.transform.SetParent(parent);
    wheel.transform.position = basePos + new Vector3(0f, 1.0f, 0f);
    wheel.transform.localScale = new Vector3(0.9f, 1.6f, 0.9f);
    wheel.GetComponent<Renderer>().sharedMaterial = Lit(color);
    for (int i = 0; i < 6; i++) {
      float ang = i * 60f;
      Vector3 dir = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), 0f, Mathf.Sin(ang * Mathf.Deg2Rad));
      GameObject tooth = Box(parent, gearName + "Tooth" + i,
        basePos + dir * 0.52f + new Vector3(0f, 1.0f, 0f),
        new Vector3(0.18f, 1.2f, 0.18f), color, false);
      tooth.transform.localRotation = Quaternion.Euler(0f, -ang, 0f);
      StripCollider(tooth);
    }
    Ball(parent, gearName + "Hub", basePos + new Vector3(0f, 1.0f, 0f), 0.30f,
      new Color(0.96f, 0.95f, 0.90f), false).transform.SetParent(parent);
  }

  // English: open-book pillars (two tilted slabs) + slab lintel + ascending
  // blocks + speech-bubble sign (sphere + tail) on a post beside the road.
  static void BuildBooksGate(Transform parent, SubjectDefinition def, Vector3 a, Vector3 b, Vector3 lat) {
    Vector3 face = FaceOf(def);
    BuildBookPillar(parent, "EnglishBookA", a, def.Primary, def.Secondary, lat, face);
    BuildBookPillar(parent, "EnglishBookB", b, def.Primary, def.Secondary, lat, face);
    Vector3 mid = (a + b) * 0.5f;
    // Lowered 1.9 -> 1.7 (P3 visual QA: the slab filled the playground
    // follow frame; the sightline clears its top by ~0.8m now). Slimmed
    // 0.3 -> 0.18 thick so the in-frame doorway reads light, not a wall.
    GameObject engLintel = Box(parent, "EnglishLintel", mid + new Vector3(0f, 1.7f, 0f),
      SlabScale(lat, 3.4f), def.Secondary, false);
    engLintel.transform.localScale = new Vector3(3.4f, 0.18f, 0.3f);
    engLintel.transform.localRotation = Quaternion.Euler(0f, YawAlongX(lat), 0f);
    IgnoreFromBuild(engLintel);
    // Open-book crown (reading motif): two slabs meeting over the beam.
    // Visual-only: crowns the road (ignoreFromBuild).
    float crownYaw = YawFaceZ(FaceOf(def));
    GameObject crownL = Box(parent, "EnglishCrownL", mid + new Vector3(0f, 2.05f, 0f),
      new Vector3(0.7f, 0.5f, 0.07f), def.Primary, false);
    crownL.transform.localRotation = Quaternion.Euler(0f, crownYaw + 22f, 0f);
    IgnoreFromBuild(crownL);
    GameObject crownR = Box(parent, "EnglishCrownR", mid + new Vector3(0f, 2.05f, 0f),
      new Vector3(0.7f, 0.5f, 0.07f), def.Secondary, false);
    crownR.transform.localRotation = Quaternion.Euler(0f, crownYaw - 22f, 0f);
    IgnoreFromBuild(crownR);
    // (User round: pencil + blocks + bubble sign removed — side clutter
    // around a hub gate. Pillars + lintel + crown carry the identity.)
  }

  static void BuildBookPillar(Transform parent, string bookName, Vector3 basePos, Color cover, Color pages, Vector3 lat, Vector3 face) {
    Box(parent, bookName + "Base", basePos + new Vector3(0f, 0.15f, 0f),
      new Vector3(0.8f, 0.3f, 0.8f), cover, true);
    float yaw = YawFaceZ(face);
    GameObject left = Box(parent, bookName + "PageL", basePos + new Vector3(0f, 1.0f, 0f),
      new Vector3(0.55f, 1.30f, 0.08f), pages, true);
    left.transform.localRotation = Quaternion.Euler(0f, yaw + 18f, 0f);
    GameObject right = Box(parent, bookName + "PageR", basePos + new Vector3(0f, 1.0f, 0f),
      new Vector3(0.55f, 1.30f, 0.08f), cover, true);
    right.transform.localRotation = Quaternion.Euler(0f, yaw - 18f, 0f);
  }

  // Vietnamese: tablet pillars with caps + banner lintel with scroll ends +
  // straw-hat disc on top + dot stones beside the road.
  static void BuildScrollsGate(Transform parent, SubjectDefinition def, Vector3 a, Vector3 b, Vector3 lat) {
    BuildTabletPillar(parent, "VietnameseTabletA", a, def.Primary, def.Secondary);
    BuildTabletPillar(parent, "VietnameseTabletB", b, def.Primary, def.Secondary);
    Vector3 mid = (a + b) * 0.5f;
    // Banner + rolls + hat are visual-only (headroom rule: road passes under).
    GameObject banner = Box(parent, "VietnameseBanner", mid + new Vector3(0f, 1.95f, 0f),
      SlabScale(lat, 3.2f), def.Secondary, false);
    banner.transform.localRotation = Quaternion.Euler(0f, YawAlongX(lat), 0f);
    IgnoreFromBuild(banner);
    // Scroll ends: two short vertical rolls hanging at the banner tips.
    GameObject rollL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rollL.name = "VietnameseRollL";
    rollL.transform.SetParent(parent);
    rollL.transform.position = mid + lat * 1.6f + new Vector3(0f, 1.55f, 0f);
    rollL.transform.localScale = new Vector3(0.22f, 0.5f, 0.22f);
    rollL.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.99f, 0.94f, 0.84f));
    StripCollider(rollL);
    IgnoreFromBuild(rollL);
    GameObject rollR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rollR.name = "VietnameseRollR";
    rollR.transform.SetParent(parent);
    rollR.transform.position = mid - lat * 1.6f + new Vector3(0f, 1.55f, 0f);
    rollR.transform.localScale = new Vector3(0.22f, 0.5f, 0.22f);
    rollR.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.99f, 0.94f, 0.84f));
    StripCollider(rollR);
    IgnoreFromBuild(rollR);
    // Straw-hat crowning the gate, now two-tiered (festival motif).
    GameObject brim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    brim.name = "VietnameseHatBrim";
    brim.transform.SetParent(parent);
    brim.transform.position = mid + new Vector3(0f, 2.35f, 0f);
    brim.transform.localScale = new Vector3(1.3f, 0.08f, 1.3f);
    brim.GetComponent<Renderer>().sharedMaterial = Lit(StrawC);
    StripCollider(brim);
    IgnoreFromBuild(brim);
    GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    crown.name = "VietnameseHatCrown";
    crown.transform.SetParent(parent);
    crown.transform.position = mid + new Vector3(0f, 2.42f, 0f);
    crown.transform.localScale = new Vector3(0.45f, 0.25f, 0.45f);
    crown.GetComponent<Renderer>().sharedMaterial = Lit(StrawC);
    StripCollider(crown);
    IgnoreFromBuild(crown);
    GameObject tier2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    tier2.name = "VietnameseHatTier2";
    tier2.transform.SetParent(parent);
    tier2.transform.position = mid + new Vector3(0f, 2.48f, 0f);
    tier2.transform.localScale = new Vector3(0.55f, 0.07f, 0.55f);
    tier2.GetComponent<Renderer>().sharedMaterial = Lit(StrawC);
    StripCollider(tier2);
    IgnoreFromBuild(tier2);
    GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    knob.name = "VietnameseHatKnob";
    knob.transform.SetParent(parent);
    knob.transform.position = mid + new Vector3(0f, 2.56f, 0f);
    knob.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
    knob.GetComponent<Renderer>().sharedMaterial = Lit(def.Secondary);
    StripCollider(knob);
    IgnoreFromBuild(knob);
    // (User round: tassels + side drum + dot stones removed — side clutter
    // around a hub gate. Pillars + banner + rolls + hat carry the identity.)
  }

  static void BuildTabletPillar(Transform parent, string tabletName, Vector3 basePos, Color body, Color cap) {
    Box(parent, tabletName + "Body", basePos + new Vector3(0f, 0.85f, 0f),
      new Vector3(0.6f, 1.7f, 0.4f), body, true);
    Box(parent, tabletName + "Cap", basePos + new Vector3(0f, 1.80f, 0f),
      new Vector3(0.75f, 0.18f, 0.5f), cap, true);
  }

  // ---- playground shells -------------------------------------------------------
  // Medallion + landmark core (carved) + return arch + playground tree.
  // The arrival zone (entry -> center) is kept furniture-free.

  static void BuildPlaygroundShell(Transform parent, SubjectDefinition def, BuildResult result) {
    string name = def.DisplayName;
    Vector3 c = def.PlaygroundCenter;

    GameObject med = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    med.name = name + "Medallion";
    med.transform.SetParent(parent);
    med.transform.position = c + new Vector3(0f, 0.012f, 0f);
    med.transform.localScale = new Vector3(6.0f, 0.024f, 6.0f);
    med.GetComponent<Renderer>().sharedMaterial = Lit(def.GroundTint);

    // Landmark core sits past the center (arrival zone stays open).
    Vector3 toGate = (def.GatePos - c);
    toGate.y = 0f;
    toGate.Normalize();
    Vector3 corePos = c - toGate * 1.4f;
    switch (def.Landmark) {
      case SubjectLandmarkKind.Blocks: BuildAbacus(parent, def, corePos); break;
      case SubjectLandmarkKind.Gears: BuildGearPair(parent, def, corePos); break;
      case SubjectLandmarkKind.Books: BuildBookPodium(parent, def, corePos); break;
      case SubjectLandmarkKind.Scrolls: BuildStele(parent, def, corePos); break;
    }
    result.Carves.Add(new CarveSpec { Name = name + "CoreCarve", Pos = corePos + new Vector3(0f, 0.6f, 0f), Size = new Vector3(1.2f, 1.2f, 1.2f) });

    BuildReturnArch(parent, def, result);
    BuildPlaygroundTree(parent, def, result);
    AppendPlaygroundCarves(def, result.Carves);
  }

  // Math core: abacus frame (posts + bars + rods + beads).
  static void BuildAbacus(Transform parent, SubjectDefinition def, Vector3 p) {
    Box(parent, "MathAbacusPostL", p + new Vector3(-0.7f, 0.6f, 0f),
      new Vector3(0.14f, 1.2f, 0.14f), def.Primary, true);
    Box(parent, "MathAbacusPostR", p + new Vector3(0.7f, 0.6f, 0f),
      new Vector3(0.14f, 1.2f, 0.14f), def.Primary, true);
    Box(parent, "MathAbacusBarT", p + new Vector3(0f, 1.15f, 0f),
      new Vector3(1.55f, 0.12f, 0.12f), def.Primary, true);
    Box(parent, "MathAbacusBarB", p + new Vector3(0f, 0.15f, 0f),
      new Vector3(1.55f, 0.12f, 0.12f), def.Primary, true);
    for (int r = 0; r < 2; r++) {
      float y = 0.55f + r * 0.35f;
      GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      rod.name = "MathAbacusRod" + r;
      rod.transform.SetParent(parent);
      rod.transform.position = p + new Vector3(0f, y, 0f);
      rod.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
      rod.transform.localScale = new Vector3(0.06f, 1.4f, 0.06f);
      rod.GetComponent<Renderer>().sharedMaterial = Lit(TrunkC);
      for (int i = 0; i < 3; i++) {
        Ball(parent, "MathBead" + r + "_" + i, p + new Vector3(-0.35f + i * 0.35f, y, 0f),
          0.20f, i % 2 == 0 ? def.Secondary : def.Primary, false);
      }
    }
  }

  // Thinking core: two meshing gears on posts + 3 marker studs.
  static void BuildGearPair(Transform parent, SubjectDefinition def, Vector3 p) {
    BuildGearPillar(parent, "ThinkingCoreBig", p + new Vector3(-0.45f, -0.3f, 0f), def.Primary);
    BuildGearPillar(parent, "ThinkingCoreSmall", p + new Vector3(0.55f, -0.3f, 0f), def.Secondary);
    for (int i = 0; i < 3; i++) {
      Vector3 s = p + new Vector3(-0.5f + i * 0.5f, 0f, 0.75f);
      GameObject stud = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      stud.name = "ThinkingStud" + i;
      stud.transform.SetParent(parent);
      stud.transform.position = s + new Vector3(0f, 0.2f, 0f);
      stud.transform.localScale = new Vector3(0.16f, 0.4f, 0.16f);
      stud.GetComponent<Renderer>().sharedMaterial = Lit(def.Primary);
      Ball(parent, "ThinkingStudTop" + i, s + new Vector3(0f, 0.48f, 0f), 0.16f, def.Secondary, false);
    }
  }

  // English core: podium + open book + blocks + mini bubble post.
  static void BuildBookPodium(Transform parent, SubjectDefinition def, Vector3 p) {
    Box(parent, "EnglishPodium", p + new Vector3(0f, 0.3f, 0f),
      new Vector3(1.1f, 0.6f, 0.8f), TrunkC, true);
    GameObject left = Box(parent, "EnglishCorePageL", p + new Vector3(-0.18f, 0.78f, 0f),
      new Vector3(0.5f, 0.55f, 0.07f), def.Secondary, true);
    left.transform.localRotation = Quaternion.Euler(0f, 20f, 0f);
    GameObject right = Box(parent, "EnglishCorePageR", p + new Vector3(0.18f, 0.78f, 0f),
      new Vector3(0.5f, 0.55f, 0.07f), def.Primary, true);
    right.transform.localRotation = Quaternion.Euler(0f, -20f, 0f);
    for (int i = 0; i < 2; i++) {
      float s = 0.26f + i * 0.1f;
      GameObject cube = Box(parent, "EnglishCoreBlock" + i,
        p + new Vector3(0.85f, s * 0.5f, -0.2f + i * 0.45f),
        new Vector3(s, s, s), i == 0 ? def.Primary : def.Secondary, false);
      StripCollider(cube);
    }
    GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    post.name = "EnglishCorePost";
    post.transform.SetParent(parent);
    post.transform.position = p + new Vector3(-0.85f, 0.45f, 0.3f);
    post.transform.localScale = new Vector3(0.1f, 0.9f, 0.1f);
    post.GetComponent<Renderer>().sharedMaterial = Lit(TrunkC);
    Ball(parent, "EnglishCoreBubble", p + new Vector3(-0.85f, 1.1f, 0.3f), 0.32f,
      new Color(0.98f, 0.98f, 0.97f), false);
  }

  // Vietnamese core: stele tablet on a base + drum + banner posts.
  static void BuildStele(Transform parent, SubjectDefinition def, Vector3 p) {
    Box(parent, "VietnameseBase", p + new Vector3(0f, 0.15f, 0f),
      new Vector3(1.2f, 0.3f, 0.9f), TrunkC, true);
    BuildTabletPillar(parent, "VietnameseStele", p, def.Primary, def.Secondary);
    GameObject drum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    drum.name = "VietnameseDrum";
    drum.transform.SetParent(parent);
    drum.transform.position = p + new Vector3(0.95f, 0.3f, 0.2f);
    drum.transform.localScale = new Vector3(0.5f, 0.6f, 0.5f);
    drum.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.80f, 0.42f, 0.25f));
    for (int i = -1; i <= 1; i += 2) {
      Vector3 bp = p + new Vector3(i * 0.9f, 0f, -0.55f);
      GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      post.name = "VietnameseBannerPost" + i;
      post.transform.SetParent(parent);
      post.transform.position = bp + new Vector3(0f, 0.55f, 0f);
      post.transform.localScale = new Vector3(0.1f, 1.1f, 0.1f);
      post.GetComponent<Renderer>().sharedMaterial = Lit(TrunkC);
    }
    Box(parent, "VietnameseMiniBanner", p + new Vector3(0f, 0.95f, -0.55f),
      new Vector3(1.8f, 0.35f, 0.1f), def.Secondary, false);
  }

  // ---- return arches ------------------------------------------------------------
  // User round (hub declutter): the selection hub IS the main hall, so NO
  // return arch may appear in the current world: no gold pillars, no lintel,
  // no disc, no way-home label. The one-way return TRIGGER stays (invisible,
  // same position/radius): walking back out of a district still returns to
  // Main exactly as before (mechanics preserved, gate tests green). The full
  // gold arch returns with the subject worlds.

  static void BuildReturnArch(Transform parent, SubjectDefinition def, BuildResult result) {
    string name = def.DisplayName;
    Vector3 rp = def.ReturnPoint;
    GameObject root = new GameObject(name + "Return");
    root.transform.SetParent(parent);
    root.transform.position = rp;

    SubjectGate gate = root.AddComponent<SubjectGate>();
    gate.fireRadius = 1.3f;
    result.ReturnGates.Add(gate);
    // NOTE: Bind order matches EntryGates (SubjectCatalog.All) — MarketBuilder
    // wires entry[i] -> All[i] as entry, return[i] -> All[i] as return.
  }

  // ---- playground trees ----------------------------------------------------------

  static void BuildPlaygroundTree(Transform parent, SubjectDefinition def, BuildResult result) {
    string name = def.DisplayName;
    Vector3 tp;
    // Hub round 3 (user: district trees covered their gates from the yard):
    // trees sit deep in-district, off the gate sightlines (Math/Thinking
    // trees used to stand 6m south of their gates, right in the view).
    if (def.Id == SubjectIds.Math) tp = new Vector3(9.5f, 0f, 3.5f);
    else if (def.Id == SubjectIds.Thinking) tp = new Vector3(-9.5f, 0f, 3.5f);
    else if (def.Id == SubjectIds.English) tp = new Vector3(3.4f, 0f, -12.3f);
    else tp = new Vector3(0.3f, 0f, 12.4f); // VN district NW corner (off its x=3.5 road)
    GameObject tree = new GameObject(name + "Tree");
    tree.transform.SetParent(parent);
    tree.transform.position = tp;
    GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    trunk.name = name + "Trunk";
    trunk.transform.SetParent(tree.transform);
    trunk.transform.localPosition = new Vector3(0f, 0.9f, 0f);
    trunk.transform.localScale = new Vector3(0.5f, 1.8f, 0.5f);
    trunk.GetComponent<Renderer>().sharedMaterial = Lit(TrunkC);
    Vector3[] canopyAt = {
      new Vector3(0f, 2.3f, 0f),
      new Vector3(0.7f, 1.9f, 0.3f),
      new Vector3(-0.6f, 2.0f, -0.3f),
    };
    for (int i = 0; i < canopyAt.Length; i++) {
      GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      leaf.name = name + "Canopy";
      leaf.transform.SetParent(tree.transform);
      leaf.transform.localPosition = canopyAt[i];
      leaf.transform.localScale = new Vector3(1.6f, 1.3f, 1.6f);
      leaf.GetComponent<Renderer>().sharedMaterial = Lit(def.Primary);
    }
    result.Carves.Add(new CarveSpec { Name = name + "TreeCarve", Pos = tp + new Vector3(0f, 1f, 0f), Size = new Vector3(1.2f, 2f, 1.2f) });
  }

  // ---- signposts (main-world side, read the direction) ----------------------------

  static void BuildSignpost(Transform parent, SubjectDefinition def) {
    string name = def.DisplayName;
    // Hub-arc placement: beside its own gate, hub-side, fanned outward —
    // the post never stands in a neighbour's sightline or walkway.
    Vector3 face = FaceOf(def);
    Vector3 lat = new Vector3(-face.z, 0f, face.x);
    if (lat.x * def.GatePos.x < 0f) lat = -lat;
    Vector3 sp = def.GatePos + face * 2.8f + lat * 1.6f;
    GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    post.name = name + "SignPost";
    post.transform.SetParent(parent);
    post.transform.position = sp + new Vector3(0f, 0.55f, 0f);
    post.transform.localScale = new Vector3(0.12f, 1.1f, 0.12f);
    post.GetComponent<Renderer>().sharedMaterial = Lit(TrunkC);
    // Colored cap points toward the subject road (slab toward the road).
    Vector3 toRoad = def.GatePos - sp;
    toRoad.y = 0f;
    toRoad.Normalize();
    GameObject cap = Box(parent, name + "SignCap", sp + toRoad * 0.25f + new Vector3(0f, 1.05f, 0f),
      new Vector3(0.55f, 0.3f, 0.3f), def.Primary, true);
    cap.transform.localRotation = Quaternion.LookRotation(toRoad);
  }

  // ---- outer hedge (new world bounds, full rectangle, no gaps) ---------------------

  static void BuildOuterHedgeShell(Transform parent, BuildResult result) {
    GameObject hedge = new GameObject("OuterHedge");
    hedge.transform.SetParent(parent);
    int n = 0;
    for (float x = -OuterHedgeX; x <= OuterHedgeX + 0.01f; x += 1.6f) {
      AddOuterBush(hedge.transform, new Vector3(x, 0.28f, -OuterHedgeZ), n++);
      AddOuterBush(hedge.transform, new Vector3(x, 0.28f, OuterHedgeZ), n++);
    }
    for (float z = -OuterHedgeZ + 1.6f; z <= OuterHedgeZ - 1.5f; z += 1.6f) {
      AddOuterBush(hedge.transform, new Vector3(-OuterHedgeX, 0.28f, z), n++);
      AddOuterBush(hedge.transform, new Vector3(OuterHedgeX, 0.28f, z), n++);
    }
    result.Carves.Add(new CarveSpec { Name = "OuterCarveN", Pos = new Vector3(0f, 0.5f, -OuterHedgeZ), Size = new Vector3(2 * OuterHedgeX + 0.4f, 1f, 0.4f) });
    result.Carves.Add(new CarveSpec { Name = "OuterCarveS", Pos = new Vector3(0f, 0.5f, OuterHedgeZ), Size = new Vector3(2 * OuterHedgeX + 0.4f, 1f, 0.4f) });
    result.Carves.Add(new CarveSpec { Name = "OuterCarveW", Pos = new Vector3(-OuterHedgeX, 0.5f, 0f), Size = new Vector3(0.4f, 1f, 2 * OuterHedgeZ + 0.4f) });
    result.Carves.Add(new CarveSpec { Name = "OuterCarveE", Pos = new Vector3(OuterHedgeX, 0.5f, 0f), Size = new Vector3(0.4f, 1f, 2 * OuterHedgeZ + 0.4f) });
  }

  static void AddOuterBush(Transform parent, Vector3 pos, int seed) {
    GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    bush.name = "OuterHedgeBush";
    bush.transform.SetParent(parent);
    bush.transform.position = pos;
    bush.transform.localScale = (seed % 2 == 0)
      ? new Vector3(1.15f, 0.62f, 1.15f)
      : new Vector3(0.95f, 0.55f, 0.95f);
    bush.GetComponent<Renderer>().sharedMaterial = Lit(seed % 2 == 0 ? LeafA : LeafB);
  }

  // ---- decor (post-bake) ------------------------------------------------------------
  // Boundary ring (visual; feet are denied by U-carves flushed pre-play via
  // BuildCarves — see BuildPlaygroundCarves below, called from BuildShell).

  static void BuildBoundaryRing(Transform parent, SubjectDefinition def, System.Random rng) {
    Vector3 c = def.PlaygroundCenter;
    Vector3 toGate = def.GatePos - c;
    toGate.y = 0f;
    toGate.Normalize();
    float gateAng = Mathf.Atan2(toGate.x, toGate.z) * Mathf.Rad2Deg;
    // Hub round 2 (user: still too many bushes): 12 -> 8 ring bushes.
    for (int i = 0; i < 8; i++) {
      float ang = i * 45f;
      float dAng = Mathf.DeltaAngle(ang, gateAng);
      if (Math.Abs(dAng) < 20f) continue; // opening toward the entry road
      Vector3 dir = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0f, Mathf.Cos(ang * Mathf.Deg2Rad));
      Vector3 p = c + dir * 3.3f;
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = def.DisplayName + "EdgeBush";
      bush.transform.SetParent(parent);
      bush.transform.position = p + new Vector3(0f, 0.28f, 0f);
      bush.transform.localScale = (i % 2 == 0)
        ? new Vector3(1.15f, 0.62f, 1.15f)
        : new Vector3(0.95f, 0.55f, 0.95f);
      bush.GetComponent<Renderer>().sharedMaterial = Lit(i % 2 == 0 ? LeafA : LeafB);
      StripCollider(bush);
      if (i % 4 == 0) {
        GameObject bloom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bloom.name = def.DisplayName + "EdgeBloom";
        bloom.transform.SetParent(parent);
        bloom.transform.position = p + new Vector3(0f, 0.62f, 0f);
        bloom.transform.localScale = new Vector3(0.16f, 0.16f, 0.16f);
        bloom.GetComponent<Renderer>().sharedMaterial = Lit(PastelBlooms[(i / 3) % PastelBlooms.Length]);
        StripCollider(bloom);
      }
    }
    // Flowering corner accents just outside the ring.
    for (int k = 0; k < 2; k++) {
      float ang = gateAng + 140f + k * 80f;
      Vector3 dir = new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad), 0f, Mathf.Cos(ang * Mathf.Deg2Rad));
      AddTinyBloom(parent, c + dir * 4.1f, PastelBlooms[(k + def.DisplayName.Length) % PastelBlooms.Length]);
    }
    if (rng != null) { } // seed consumed by callers; hook kept for parity
  }

  static void BuildPlaygroundDecor(Transform parent, SubjectDefinition def, System.Random rng) {
    Vector3 c = def.PlaygroundCenter;
    // Tufts + blooms + pebbles scattered off the arrival axis.
    // User round (thoáng): 6 -> 4 picks per playground.
    for (int i = 0; i < 4; i++) {
      float ang = (float)(rng.NextDouble() * 360.0);
      float rad = 1.2f + (float)rng.NextDouble() * 1.4f;
      Vector3 p = c + new Vector3(Mathf.Sin(ang * Mathf.Deg2Rad) * rad, 0f, Mathf.Cos(ang * Mathf.Deg2Rad) * rad);
      // Keep the arrival corridor (gate -> center) furniture-free.
      if (DistToSegment(p, def.GatePos, c) < 0.9f) continue;
      if (Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(def.ReturnPoint.x, 0f, def.ReturnPoint.z)) < 1.0f) continue;
      int pick = rng.Next(3);
      if (pick == 0) AddGrassTuft(parent, p, 0.7f + (float)rng.NextDouble() * 0.3f, rng);
      else if (pick == 1) AddTinyBloom(parent, p, PastelBlooms[rng.Next(PastelBlooms.Length)]);
      else AddPebble(parent, p, 0.5f + (float)rng.NextDouble() * 0.4f, rng);
    }
    // Two ground patches for value breakup.
    AddGroundPatch(parent, c + new Vector3(-1.8f, 0f, 1.2f), new Vector3(1.2f, 1f, 1.0f), def.GroundTint, rng);
    AddGroundPatch(parent, c + new Vector3(1.8f, 0f, -1.0f), new Vector3(1.1f, 1f, 0.9f), def.GroundTint, rng);
  }

  static void BuildRoadEdges(Transform parent, SubjectDefinition def, System.Random rng) {
    // Pebble/bloom rhythm along both road shoulders (outside feet, inside eyes).
    // User round (thoáng): fewer + airier (lateral 1.25 -> 1.45).
    Vector3 a = def.GatePos;
    Vector3 b = def.PlaygroundCenter;
    Vector3 dir = b - a;
    dir.y = 0f;
    float len = dir.magnitude;
    dir.Normalize();
    Vector3 side = new Vector3(-dir.z, 0f, dir.x);
    for (int i = 0; i < 3; i++) {
      float t = 0.2f + i * 0.3f;
      Vector3 mid = a + dir * (len * t);
      for (int s = -1; s <= 1; s += 2) {
        Vector3 p = mid + side * (s * 1.45f);
        if (rng.Next(2) == 0) AddTinyBloom(parent, p, PastelBlooms[rng.Next(PastelBlooms.Length)]);
        else AddGrassTuft(parent, p, 0.55f + (float)rng.NextDouble() * 0.2f, rng);
      }
    }
    // Also dress the main-world half of each road (gate -> main edge).
    Vector3 mainEdge = MainEdgePoint(def);
    Vector3 dir2 = a - mainEdge;
    dir2.y = 0f;
    float len2 = dir2.magnitude;
    if (len2 > 0.5f) {
      dir2.Normalize();
      Vector3 side2 = new Vector3(-dir2.z, 0f, dir2.x);
      for (int i = 0; i < 2; i++) {
        float t = 0.3f + i * 0.4f;
        Vector3 mid = mainEdge + dir2 * (len2 * t);
        for (int s = -1; s <= 1; s += 2) {
          Vector3 p = mid + side2 * (s * 1.45f);
          if (rng.Next(2) == 0) AddTinyBloom(parent, p, PastelBlooms[rng.Next(PastelBlooms.Length)]);
          else AddGrassTuft(parent, p, 0.55f + (float)rng.NextDouble() * 0.2f, rng);
        }
      }
    }
  }

  static Vector3 MainEdgePoint(SubjectDefinition def) {
    if (def.Id == SubjectIds.Math) return new Vector3(5.6f, 0f, def.GatePos.z);
    if (def.Id == SubjectIds.Thinking) return new Vector3(-5.6f, 0f, def.GatePos.z);
    if (def.Id == SubjectIds.English) return new Vector3(def.GatePos.x, 0f, -3.6f);
    return new Vector3(def.GatePos.x, 0f, 3.6f);
  }

  static void BuildOuterHedgeDressing(Transform parent, System.Random rng) {
    // Backdrop blobs behind the outer hedge (depth, same language as main).
    AddBackdropBlob(parent, new Vector3(-6f, -0.1f, 17.5f), 2.4f);
    AddBackdropBlob(parent, new Vector3(7f, -0.1f, -17.5f), 2.6f);
    AddBackdropBlob(parent, new Vector3(-21f, -0.1f, 2f), 2.2f);
    AddBackdropBlob(parent, new Vector3(21f, -0.1f, -3f), 2.4f);
    if (rng != null) { }
  }

  // ---- playground U-carves (feet boundary; opening faces the entry road) -----------
  // Called from BuildShell so carves flush with the rest via BuildCarves.

  public static void AppendPlaygroundCarves(SubjectDefinition def, List<CarveSpec> carves) {
    Vector3 c = def.PlaygroundCenter;
    Vector3 toGate = def.GatePos - c;
    toGate.y = 0f;
    toGate.Normalize();
    // Back + two sides; the gate side stays open.
    Vector3 back = c - toGate * 3.2f;
    Vector3 side = new Vector3(-toGate.z, 0f, toGate.x);
    Vector3 sideA = c + side * 3.2f - toGate * 0.4f;
    Vector3 sideB = c - side * 3.2f - toGate * 0.4f;
    // Sides run PARALLEL to the entry road; the back runs across it (the gate
    // side stays open). Swapped once live (P3 survey: sides walled the road).
    Vector3 alongSize = Math.Abs(toGate.x) > 0.5f
      ? new Vector3(6.9f, 2f, 0.5f)   // road along X: sides run along X
      : new Vector3(0.5f, 2f, 6.9f);  // road along Z: sides run along Z
    Vector3 backSize = Math.Abs(toGate.x) > 0.5f
      ? new Vector3(0.5f, 2f, 6.9f)   // road along X: back runs along Z
      : new Vector3(6.9f, 2f, 0.5f);  // road along Z: back runs along X
    string name = def.DisplayName;
    carves.Add(new CarveSpec { Name = name + "BoundBack", Pos = back + new Vector3(0f, 0.5f, 0f), Size = backSize });
    carves.Add(new CarveSpec { Name = name + "BoundSideA", Pos = sideA + new Vector3(0f, 0.5f, 0f), Size = alongSize });
    carves.Add(new CarveSpec { Name = name + "BoundSideB", Pos = sideB + new Vector3(0f, 0.5f, 0f), Size = alongSize });
  }

  // ---- small builders (primitive-only, deterministic) -------------------------------

  static GameObject Box(Transform parent, string goName, Vector3 pos, Vector3 scale, Color color, bool keepCollider) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = goName;
    go.transform.SetParent(parent);
    go.transform.position = pos;
    go.transform.localScale = scale;
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    if (!keepCollider) StripCollider(go);
    return go;
  }

  static GameObject Ball(Transform parent, string goName, Vector3 pos, float diameter, Color color, bool keepCollider) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    go.name = goName;
    go.transform.SetParent(parent);
    go.transform.position = pos;
    go.transform.localScale = new Vector3(diameter, diameter, diameter);
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    if (!keepCollider) StripCollider(go);
    return go;
  }

  static void StripCollider(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) UnityEngine.Object.Destroy(c);
    } catch (Exception) { }
  }

  // Overhead beams MUST be invisible to the NavMesh bake: the runtime bake
  // rasterizes RENDER MESHES (not colliders) with agentHeight 2m, so any beam
  // below 2m clearance severs the road into isolated islands (P3 telemetry:
  // PathPartial at every gate line). ignoreFromBuild keeps the visual while
  // the road passes under. Foot-level pillars keep their meshes + carves.
  static void IgnoreFromBuild(GameObject go) {
    if (go == null) return;
    try {
      NavMeshModifier mod = go.AddComponent<NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (Exception) { }
  }

  static void AddGrassTuft(Transform parent, Vector3 pos, float s, System.Random rng) {
    float yaw = rng != null ? (float)(rng.NextDouble() * 360.0) : 0f;
    GameObject tuft = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    tuft.name = "SubjectGrassTuft";
    tuft.transform.SetParent(parent);
    tuft.transform.position = pos + new Vector3(0f, 0.18f * s, 0f);
    tuft.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    tuft.transform.localScale = new Vector3(0.7f * s, 0.35f * s, 0.7f * s);
    tuft.GetComponent<Renderer>().sharedMaterial = Lit((rng != null && rng.NextDouble() < 0.5) ? LeafA : LeafB);
    StripCollider(tuft);
  }

  static void AddTinyBloom(Transform parent, Vector3 pos, Color bloom) {
    GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stem.name = "SubjectSprig";
    stem.transform.SetParent(parent);
    stem.transform.position = pos + new Vector3(0f, 0.10f, 0f);
    stem.transform.localScale = new Vector3(0.03f, 0.10f, 0.03f);
    stem.GetComponent<Renderer>().sharedMaterial = Lit(LeafA);
    StripCollider(stem);
    GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    head.name = "SubjectTinyBloom";
    head.transform.SetParent(parent);
    head.transform.position = pos + new Vector3(0f, 0.19f, 0f);
    head.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
    head.GetComponent<Renderer>().sharedMaterial = Lit(bloom);
    StripCollider(head);
  }

  static void AddPebble(Transform parent, Vector3 pos, float s, System.Random rng) {
    float yaw = rng != null ? (float)(rng.NextDouble() * 360.0) : 0f;
    GameObject pebble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    pebble.name = "SubjectPebble";
    pebble.transform.SetParent(parent);
    pebble.transform.position = pos + new Vector3(0f, 0.06f, 0f);
    pebble.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    pebble.transform.localScale = new Vector3(0.22f * s, 0.12f * s, 0.26f * s);
    pebble.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.60f, 0.60f, 0.62f));
    StripCollider(pebble);
  }

  static void AddGroundPatch(Transform parent, Vector3 pos, Vector3 size, Color color, System.Random rng) {
    float yaw = rng != null ? (float)(rng.NextDouble() * 360.0) : 0f;
    GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    patch.name = "SubjectPatch";
    patch.transform.SetParent(parent);
    patch.transform.position = pos + new Vector3(0f, 0.012f, 0f);
    patch.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    patch.transform.localScale = new Vector3(size.x, 0.012f, size.z);
    patch.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(patch);
  }

  static void AddBackdropBlob(Transform parent, Vector3 pos, float s) {
    GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    blob.name = "SubjectBackdrop";
    blob.transform.SetParent(parent);
    blob.transform.position = pos + new Vector3(0f, 0.35f * s, 0f);
    blob.transform.localScale = new Vector3(2.0f * s, 0.7f * s, 2.0f * s);
    blob.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.26f, 0.50f, 0.34f));
    StripCollider(blob);
  }

  static float DistToSegment(Vector3 p, Vector3 a, Vector3 b) {
    Vector2 pa = new Vector2(p.x - a.x, p.z - a.z);
    Vector2 ba = new Vector2(b.x - a.x, b.z - a.z);
    float t = Mathf.Clamp01((pa.x * ba.x + pa.y * ba.y) / Mathf.Max(0.001f, ba.sqrMagnitude));
    return new Vector2(pa.x - ba.x * t, pa.y - ba.y * t).magnitude;
  }

  static readonly Dictionary<string, Material> _litCache = new Dictionary<string, Material>();

  static Material Lit(Color color) {
    string key = "S3:" + color.r.ToString("F2") + "," + color.g.ToString("F2") + "," + color.b.ToString("F2");
    Material cached;
    if (_litCache.TryGetValue(key, out cached) && cached != null) return cached;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    mat.enableInstancing = true;
    _litCache[key] = mat;
    return mat;
  }
}
