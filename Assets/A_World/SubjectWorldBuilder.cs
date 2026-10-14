// A_World/SubjectWorldBuilder.cs — hub Subject Gate arc (PHASE 5 trimmed).
// Code-builds the Learning World hub piece: the FIVE subject gates (landmark
// arches on the arc) + the outer hedge. The old in-world districts (roads,
// playgrounds, return arches, signposts, boundary rings) are DELETED — subject
// gates now lead straight to the Selection Yard (A -> B).
// Split to respect the frozen NavMesh lifecycle (MarketBuilder pattern):
//   BuildShell  — PRE-bake: gate pillars (solid; they keep colliders so the
//                 bake sees them) + outer hedge shell.
//   BuildCarves — runtime stationary carves (pillar feet; never rebake).
//   BuildDecor  — POST-bake: collider-free hedge dressing.
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
    public readonly List<CarveSpec> Carves = new List<CarveSpec>();
  }

  // World extension contract (metres). Ground must cover these (MarketBuilder
  // enlarges the plane accordingly); ClickRouter bounds cover them too.
  public const float OuterHedgeX = 17f;
  public const float OuterHedgeZ = 14.5f;

  static readonly Color RoadTan = new Color(0.76f, 0.60f, 0.40f);
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
    // PHASE 5 LEGACY PURGE: one entry gate per subject on the hub arc + the
    // outer hedge. The in-world districts (roads, playgrounds, return arches,
    // signposts, boundary rings) are DELETED — subject gates now lead straight
    // to the Selection Yard.
    foreach (SubjectDefinition def in SubjectCatalog.All) {
      BuildEntryGate(parent, def, result);
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
    // PHASE 5: only the outer hedge dressing survives (district rings/floors
    // and road shoulders are deleted with the districts).
    BuildOuterHedgeDressing(parent, rng);
  }

  // ---- roads -----------------------------------------------------------------
  // One flat walkable strip per subject, same warm tan as the main path so all
  // roads read as ONE world. Pre-bake (walkable). Colliders kept (matches Path).

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
      case SubjectLandmarkKind.Compass: BuildCompassGate(parent, def, pillarA, pillarB, lat); break;
    }

    // Tinted medallion under the gate (walkable ground treatment).
    // Hub-arc size (2.6m): full 3.2m discs would touch on the ~3.6m spacing.
    GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    disc.name = name + "GateDisc";
    disc.transform.SetParent(parent);
    disc.transform.position = def.GatePos + new Vector3(0f, 0.012f, 0f);
    disc.transform.localScale = new Vector3(2.6f, 0.024f, 2.6f);
    disc.GetComponent<Renderer>().sharedMaterial = Lit(def.GroundTint);
    // S6+ dreamy hub pass (user order "mộng mơ màu hồng"): a tinted threshold
    // ring under every gate and a blossom crown on Blocks/Gears/Books lintels
    // (Vietnamese keeps its festival hat). Collider-free dressing.
    WorldBeauty.GateRing(parent, name + "Gate", def.GatePos, def.Primary, 3.0f);
    // PHASE 2: the Compass gate carries its own magnifier crown — no blossom
    // stacking (same reason Vietnamese skips it).
    if (def.Landmark != SubjectLandmarkKind.Scrolls &&
        def.Landmark != SubjectLandmarkKind.Compass) {
      WorldBeauty.BlossomCrown(parent, name + "GateCrown",
        def.GatePos + new Vector3(0f, 2.95f, 0f), 0.9f);
    }

    // Gate name, mounted ON the arch (user round: hub beauty — the name is
    // one body with the gate, dressed per subject, instead of a pill box
    // floating high above it).
    BuildGateNameBoard(parent, def, face, lat);

    result.Carves.Add(new CarveSpec { Name = name + "PillarCarveA", Pos = pillarA + new Vector3(0f, 1f, 0f), Size = new Vector3(0.8f, 2f, 0.8f) });
    result.Carves.Add(new CarveSpec { Name = name + "PillarCarveB", Pos = pillarB + new Vector3(0f, 1f, 0f), Size = new Vector3(0.8f, 2f, 0.8f) });

    SubjectGate gate = root.AddComponent<SubjectGate>();
    gate.fireRadius = 1.2f;
    result.EntryGates.Add(gate);
  }

  // ---- gate name boards (one body with the arch) -------------------------------
  // Light-wood board carrying the subject pill MOUNTED ON the lintel face (S2
  // gate-shape pass: the board used to float above the beam; now the name is
  // one body with the gate at gate height). Vietnamese paints its pill
  // straight onto the pink banner. Visual-only (no collider, bake-ignored).
  static void BuildGateNameBoard(Transform parent, SubjectDefinition def, Vector3 face, Vector3 lat) {
    string name = def.DisplayName;
    Vector3 mid = def.GatePos;
    float yaw = YawAlongX(lat);
    if (def.Landmark == SubjectLandmarkKind.Scrolls) {
      // Banner IS the board: pill painted straight onto the pink face
      // (locked mode — fixed yaw toward the hub, never billboards, so the
      // name reads as one body with the gate instead of a floating box).
      GameObject labelGo = new GameObject(name + "GateLabel");
      labelGo.transform.SetParent(parent);
      WorldNameLabel label = labelGo.AddComponent<WorldNameLabel>();
      label.SetupLocked(def.DisplayName,
        mid + face * 0.35f + new Vector3(0f, 2.5f, 0f),
        SubjectCatalog.HubCenter);
      label.Show();
      return;
    }
    Vector3 boardPos;
    if (def.Landmark == SubjectLandmarkKind.Books) {
      boardPos = mid + face * 0.22f + new Vector3(0f, 2.5f, 0f); // on the slab face
    } else if (def.Landmark == SubjectLandmarkKind.Gears) {
      boardPos = mid + face * 0.25f + new Vector3(0f, 2.62f, 0f); // on the puzzle beam
    } else {
      boardPos = mid + face * 0.25f + new Vector3(0f, 2.38f, 0f); // on the cylinder beam
    }
    GameObject board = Box(parent, name + "NameBoard", boardPos,
      new Vector3(1.5f, 0.42f, 0.14f), new Color(0.72f, 0.55f, 0.34f), false);
    board.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    IgnoreFromBuild(board);
    GameObject labelGo2 = new GameObject(name + "GateLabel");
    labelGo2.transform.SetParent(parent);
    WorldNameLabel label2 = labelGo2.AddComponent<WorldNameLabel>();
    label2.SetupLocked(def.DisplayName,
      boardPos + face * 0.12f,
      SubjectCatalog.HubCenter);
    label2.Show();
  }

  // Math: stacked-cube pillars + cylinder lintel + finial duo (geometry
  // motif, no curriculum). S2 gate-shape pass (user round: the dev gates must
  // read as gates): pillars grow to 2.45m and the beam rides the pillar tops
  // with a short overhang, so the Math gate is a real doorway silhouette.
  static void BuildBlocksGate(Transform parent, SubjectDefinition def, Vector3 a, Vector3 b, Vector3 lat) {
    BuildCubePillar(parent, "MathPillarA", a, def.Primary);
    BuildCubePillar(parent, "MathPillarB", b, def.Primary);
    Vector3 mid = (a + b) * 0.5f + new Vector3(0f, 2.38f, 0f);
    GameObject lintel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    lintel.name = "MathLintel";
    lintel.transform.SetParent(parent);
    lintel.transform.position = mid;
    lintel.transform.localScale = new Vector3(0.38f, 2.4f, 0.38f);
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
    // Shape duo (geometry family): cube + sphere finials ride the pillar tops.
    Box(parent, "MathFinialA", a + new Vector3(0f, 2.72f, 0f),
      new Vector3(0.4f, 0.4f, 0.4f), def.Primary, false);
    Ball(parent, "MathFinialB", b + new Vector3(0f, 2.7f, 0f), 0.46f, def.Secondary, false);
  }

  static void BuildCubePillar(Transform parent, string pillarName, Vector3 basePos, Color color) {
    Box(parent, pillarName + "Base", basePos + new Vector3(0f, 0.425f, 0f),
      new Vector3(0.85f, 0.85f, 0.85f), color, true);
    Box(parent, pillarName + "Mid", basePos + new Vector3(0f, 1.175f, 0f),
      new Vector3(0.65f, 0.65f, 0.65f), color, true);
    Box(parent, pillarName + "Top", basePos + new Vector3(0f, 1.975f, 0f),
      new Vector3(0.5f, 0.95f, 0.5f), color, true);
  }

  // Thinking: gear-wheel pillars (cylinder + teeth) + slotted puzzle lintel.
  // S2 gate-shape pass: wheels grow to 2.1m pillars and the puzzle beam rides
  // at 2.62m — a real doorway on the district road.
  static void BuildGearsGate(Transform parent, SubjectDefinition def, Vector3 a, Vector3 b, Vector3 lat) {
    BuildGearPillar(parent, "ThinkingGearA", a, def.Primary);
    BuildGearPillar(parent, "ThinkingGearB", b, def.Primary);
    Vector3 mid = (a + b) * 0.5f;
    // Puzzle lintel: two slabs leaving a center notch (interlock motif).
    // Visual-only (no collider + ignoreFromBuild): the road must pass UNDER.
    Vector3 along = lat;
    GameObject lintelL = Box(parent, "ThinkingLintelL", mid + along * 0.95f + new Vector3(0f, 2.62f, 0f),
      SlabScale(along, 1.5f), def.Secondary, false);
    GameObject lintelR = Box(parent, "ThinkingLintelR", mid - along * 0.95f + new Vector3(0f, 2.62f, 0f),
      SlabScale(along, 1.5f), def.Secondary, false);
    lintelL.transform.localRotation = Quaternion.Euler(0f, YawAlongX(along), 0f);
    lintelR.transform.localRotation = Quaternion.Euler(0f, YawAlongX(along), 0f);
    IgnoreFromBuild(lintelL);
    IgnoreFromBuild(lintelR);
    // Name board parks on the beam (see BuildGateNameBoard).
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
    // (User round: solid green columns read as TREE TRUNKS from hub views.
    // Restyle: light-stone wheel (rock/metal gear, zero foliage read) + green
    // chunkier teeth keep the Thinking identity + cream hub. S2: the wheel
    // grows to a 2.1m gate pillar — same shape language, real gate scale.)
    Color stone = new Color(0.80f, 0.78f, 0.72f);
    GameObject foot = Box(parent, gearName + "Foot", basePos + new Vector3(0f, 0.15f, 0f),
      new Vector3(0.7f, 0.3f, 0.7f), stone, true);
    StripCollider(foot);
    IgnoreFromBuild(foot);
    GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    wheel.name = gearName + "Wheel";
    wheel.transform.SetParent(parent);
    wheel.transform.position = basePos + new Vector3(0f, 1.35f, 0f);
    wheel.transform.localScale = new Vector3(0.95f, 1.05f, 0.95f);
    wheel.GetComponent<Renderer>().sharedMaterial = Lit(stone);
    for (int i = 0; i < 6; i++) {
      float ang = i * 60f;
      Vector3 dir = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad), 0f, Mathf.Sin(ang * Mathf.Deg2Rad));
      GameObject tooth = Box(parent, gearName + "Tooth" + i,
        basePos + dir * 0.55f + new Vector3(0f, 1.35f, 0f),
        new Vector3(0.26f, 1.5f, 0.26f), color, false);
      tooth.transform.localRotation = Quaternion.Euler(0f, -ang, 0f);
      StripCollider(tooth);
      IgnoreFromBuild(tooth);
    }
    Ball(parent, gearName + "Hub", basePos + new Vector3(0f, 1.35f, 0f), 0.32f,
      new Color(0.96f, 0.95f, 0.90f), false).transform.SetParent(parent);
  }

  // Exploration (KHÁM PHÁ, FULL ARCHITECTURE RESET PHASE 2): pebble-stack
  // pillars + cylinder lintel + a magnifier-ring crown with a globe hub —
  // the "look closer at the world" motif. Distinct silhouette from Blocks
  // (cubes), Gears (wheels), Books (slabs), Scrolls (banners).
  static void BuildCompassGate(Transform parent, SubjectDefinition def, Vector3 a, Vector3 b, Vector3 lat) {
    BuildPebblePillar(parent, "ExplorePillarA", a, def.Primary);
    BuildPebblePillar(parent, "ExplorePillarB", b, def.Primary);
    Vector3 mid = (a + b) * 0.5f;
    Vector3 face = FaceOf(def);
    GameObject lintel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    lintel.name = "ExploreLintel";
    lintel.transform.SetParent(parent);
    lintel.transform.position = mid + new Vector3(0f, 2.42f, 0f);
    lintel.transform.localScale = new Vector3(0.34f, 2.4f, 0.34f);
    lintel.transform.localRotation = Quaternion.FromToRotation(Vector3.up, lat);
    lintel.GetComponent<Renderer>().sharedMaterial = Lit(def.Secondary);
    // Headroom rule (same as every gate): beams are visual-only — no collider
    // + ignoreFromBuild — so the bake never severs the road into islands.
    StripCollider(lintel);
    IgnoreFromBuild(lintel);
    // Magnifier ring crowning the gate, disc facing the child on approach.
    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ring.name = "ExploreMagnifierRing";
    ring.transform.SetParent(parent);
    ring.transform.position = mid + new Vector3(0f, 3.06f, 0f);
    ring.transform.localScale = new Vector3(0.92f, 0.05f, 0.92f);
    ring.transform.localRotation = Quaternion.FromToRotation(Vector3.up, face);
    ring.GetComponent<Renderer>().sharedMaterial = Lit(def.Primary);
    StripCollider(ring);
    IgnoreFromBuild(ring);
    Ball(parent, "ExploreGlobe", mid + new Vector3(0f, 3.06f, 0f), 0.52f, def.Secondary, false);
    Box(parent, "ExploreNeedleV", mid + new Vector3(0f, 3.06f, 0f),
      new Vector3(0.06f, 1.05f, 0.06f), def.Primary, false);
    Box(parent, "ExploreNeedleH", mid + new Vector3(0f, 3.06f, 0f),
      new Vector3(1.05f, 0.06f, 0.06f), def.Primary, false);
    Ball(parent, "ExploreStarA", a + new Vector3(0f, 2.34f, 0f), 0.34f, def.Secondary, false);
    Ball(parent, "ExploreStarB", b + new Vector3(0f, 2.34f, 0f), 0.34f, def.Secondary, false);
  }

  static void BuildPebblePillar(Transform parent, string pillarName, Vector3 basePos, Color color) {
    GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    foot.name = pillarName + "Foot";
    foot.transform.SetParent(parent);
    foot.transform.position = basePos + new Vector3(0f, 0.2f, 0f);
    foot.transform.localScale = new Vector3(0.9f, 0.2f, 0.9f);
    foot.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.72f, 0.70f, 0.64f));
    Pebble(parent, pillarName + "Pebble0", basePos + new Vector3(0f, 0.66f, 0f), 0.98f, color);
    Pebble(parent, pillarName + "Pebble1", basePos + new Vector3(0f, 1.44f, 0f), 0.82f,
      new Color(0.78f, 0.76f, 0.70f));
    Pebble(parent, pillarName + "Pebble2", basePos + new Vector3(0f, 2.08f, 0f), 0.62f, color);
  }

  static void Pebble(Transform parent, string name, Vector3 pos, float diameter, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    go.name = name;
    go.transform.SetParent(parent);
    go.transform.position = pos;
    go.transform.localScale = new Vector3(diameter, diameter * 0.72f, diameter);
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(go);
    IgnoreFromBuild(go);
  }

  // English: open-book pillars (two tilted slabs) + slab lintel + ascending
  // open-book crown over the beam. S2 gate-shape pass: pillars grow to 2.3m
  // and the beam rides at 2.5m — a real reading-gate silhouette.
  static void BuildBooksGate(Transform parent, SubjectDefinition def, Vector3 a, Vector3 b, Vector3 lat) {
    Vector3 face = FaceOf(def);
    BuildBookPillar(parent, "EnglishBookA", a, def.Primary, def.Secondary, lat, face);
    BuildBookPillar(parent, "EnglishBookB", b, def.Primary, def.Secondary, lat, face);
    Vector3 mid = (a + b) * 0.5f;
    GameObject engLintel = Box(parent, "EnglishLintel", mid + new Vector3(0f, 2.5f, 0f),
      SlabScale(lat, 3.4f), def.Secondary, false);
    engLintel.transform.localScale = new Vector3(3.4f, 0.2f, 0.34f);
    engLintel.transform.localRotation = Quaternion.Euler(0f, YawAlongX(lat), 0f);
    IgnoreFromBuild(engLintel);
    // Open-book crown (reading motif): two slabs meeting over the beam.
    // Visual-only: crowns the road (ignoreFromBuild).
    float crownYaw = YawFaceZ(FaceOf(def));
    GameObject crownL = Box(parent, "EnglishCrownL", mid + new Vector3(0f, 2.85f, 0f),
      new Vector3(0.8f, 0.55f, 0.08f), def.Primary, false);
    crownL.transform.localRotation = Quaternion.Euler(0f, crownYaw + 22f, 0f);
    IgnoreFromBuild(crownL);
    GameObject crownR = Box(parent, "EnglishCrownR", mid + new Vector3(0f, 2.85f, 0f),
      new Vector3(0.8f, 0.55f, 0.08f), def.Secondary, false);
    crownR.transform.localRotation = Quaternion.Euler(0f, crownYaw - 22f, 0f);
    IgnoreFromBuild(crownR);
    // (User round: pencil + blocks + bubble sign removed — side clutter
    // around a hub gate. Pillars + lintel + crown carry the identity.)
  }

  static void BuildBookPillar(Transform parent, string bookName, Vector3 basePos, Color cover, Color pages, Vector3 lat, Vector3 face) {
    Box(parent, bookName + "Base", basePos + new Vector3(0f, 0.15f, 0f),
      new Vector3(0.85f, 0.3f, 0.85f), cover, true);
    float yaw = YawFaceZ(face);
    GameObject left = Box(parent, bookName + "PageL", basePos + new Vector3(0f, 1.325f, 0f),
      new Vector3(0.6f, 1.95f, 0.09f), pages, true);
    left.transform.localRotation = Quaternion.Euler(0f, yaw + 18f, 0f);
    GameObject right = Box(parent, bookName + "PageR", basePos + new Vector3(0f, 1.325f, 0f),
      new Vector3(0.6f, 1.95f, 0.09f), cover, true);
    right.transform.localRotation = Quaternion.Euler(0f, yaw - 18f, 0f);
  }

  // Vietnamese: tablet pillars with caps + banner lintel with scroll ends +
  // straw-hat crown. S2 gate-shape pass: tablets grow to 2.1m, banner rides
  // at 2.5m and the hat crowns at ~3.1m — a real festival gate silhouette.
  static void BuildScrollsGate(Transform parent, SubjectDefinition def, Vector3 a, Vector3 b, Vector3 lat) {
    BuildTabletPillar(parent, "VietnameseTabletA", a, def.Primary, def.Secondary);
    BuildTabletPillar(parent, "VietnameseTabletB", b, def.Primary, def.Secondary);
    Vector3 mid = (a + b) * 0.5f;
    // Banner + rolls + hat are visual-only (headroom rule: road passes under).
    GameObject banner = Box(parent, "VietnameseBanner", mid + new Vector3(0f, 2.5f, 0f),
      SlabScale(lat, 3.2f), def.Secondary, false);
    banner.transform.localScale = new Vector3(3.6f, 0.32f, 0.22f);
    banner.transform.localRotation = Quaternion.Euler(0f, YawAlongX(lat), 0f);
    IgnoreFromBuild(banner);
    // Scroll ends: two short vertical rolls hanging at the banner tips.
    GameObject rollL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rollL.name = "VietnameseRollL";
    rollL.transform.SetParent(parent);
    rollL.transform.position = mid + lat * 1.75f + new Vector3(0f, 2.35f, 0f);
    rollL.transform.localScale = new Vector3(0.24f, 0.5f, 0.24f);
    rollL.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.99f, 0.94f, 0.84f));
    StripCollider(rollL);
    IgnoreFromBuild(rollL);
    GameObject rollR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rollR.name = "VietnameseRollR";
    rollR.transform.SetParent(parent);
    rollR.transform.position = mid - lat * 1.75f + new Vector3(0f, 2.35f, 0f);
    rollR.transform.localScale = new Vector3(0.24f, 0.5f, 0.24f);
    rollR.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.99f, 0.94f, 0.84f));
    StripCollider(rollR);
    IgnoreFromBuild(rollR);
    // Straw-hat crowning the gate, now two-tiered (festival motif).
    GameObject brim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    brim.name = "VietnameseHatBrim";
    brim.transform.SetParent(parent);
    brim.transform.position = mid + new Vector3(0f, 2.86f, 0f);
    brim.transform.localScale = new Vector3(1.35f, 0.08f, 1.35f);
    brim.GetComponent<Renderer>().sharedMaterial = Lit(StrawC);
    StripCollider(brim);
    IgnoreFromBuild(brim);
    GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    crown.name = "VietnameseHatCrown";
    crown.transform.SetParent(parent);
    crown.transform.position = mid + new Vector3(0f, 2.93f, 0f);
    crown.transform.localScale = new Vector3(0.5f, 0.26f, 0.5f);
    crown.GetComponent<Renderer>().sharedMaterial = Lit(StrawC);
    StripCollider(crown);
    IgnoreFromBuild(crown);
    GameObject tier2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    tier2.name = "VietnameseHatTier2";
    tier2.transform.SetParent(parent);
    tier2.transform.position = mid + new Vector3(0f, 2.99f, 0f);
    tier2.transform.localScale = new Vector3(0.58f, 0.07f, 0.58f);
    tier2.GetComponent<Renderer>().sharedMaterial = Lit(StrawC);
    StripCollider(tier2);
    IgnoreFromBuild(tier2);
    GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    knob.name = "VietnameseHatKnob";
    knob.transform.SetParent(parent);
    knob.transform.position = mid + new Vector3(0f, 3.07f, 0f);
    knob.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
    knob.GetComponent<Renderer>().sharedMaterial = Lit(def.Secondary);
    StripCollider(knob);
    IgnoreFromBuild(knob);
    // (User round: tassels + side drum + dot stones removed — side clutter
    // around a hub gate. Pillars + banner + rolls + hat carry the identity.)
  }

  static void BuildTabletPillar(Transform parent, string tabletName, Vector3 basePos, Color body, Color cap) {
    Box(parent, tabletName + "Body", basePos + new Vector3(0f, 1.05f, 0f),
      new Vector3(0.62f, 2.1f, 0.42f), body, true);
    Box(parent, tabletName + "Cap", basePos + new Vector3(0f, 2.19f, 0f),
      new Vector3(0.78f, 0.18f, 0.52f), cap, true);
  }

  // ---- playground shells -------------------------------------------------------
  // Medallion + landmark core (carved) + return arch + playground tree.
  // The arrival zone (entry -> center) is kept furniture-free.

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
  // PHASE 5 LEGACY PURGE: district rings/floors and road shoulders are deleted;
  // only the outer-hedge dressing survives.

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
      if (c == null) return;
      // 3.0.1.1 headless-safe (P43E builds the shell in EditMode): Destroy
      // is illegal outside play mode — DestroyImmediate there. Player path
      // unchanged.
      if (Application.isPlaying) UnityEngine.Object.Destroy(c);
      else UnityEngine.Object.DestroyImmediate(c);
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

  static void AddBackdropBlob(Transform parent, Vector3 pos, float s) {
    GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    blob.name = "SubjectBackdrop";
    blob.transform.SetParent(parent);
    blob.transform.position = pos + new Vector3(0f, 0.35f * s, 0f);
    blob.transform.localScale = new Vector3(2.0f * s, 0.7f * s, 2.0f * s);
    blob.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.26f, 0.50f, 0.34f));
    StripCollider(blob);
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
