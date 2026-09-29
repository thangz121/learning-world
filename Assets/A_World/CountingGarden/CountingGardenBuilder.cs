// A_World/CountingGarden/CountingGardenBuilder.cs — TWO-PLOT GARDEN.
// The Counting Garden is its OWN additive scene (lazy-loaded from the Math Hub
// counting gate). This builder code-builds ALL content (one root GO, plain
// primitives + shared Lit materials + the existing PropKit, deterministic).
//
// Layout (two playable plots on a r9.5 crescent around ArcCenter):
//
//                 EXIT (0,-10.4)   ENTRY (0,-8)      <- warp in/out, north
//                        \            /
//                         entry walk (x=0)
//                              |
//                  PLAZA (0,1.5) d7.6 + number stones 1..5
//                    /                    \
//              carrot bed             number-stair hill     <- crescent r9.5
//              (-50°)                  (+50°)
//                              |
//                    reward pocket (west of plaza)
//
// Only two activities remain: the carrot patch (rabbit) at zone 0 and the
// number-stair hill at zone 1. Each keeps the bed contract (border + pad +
// fence ring + numbered gate/anchor + vignette); the mini lessons are runtime
// components (RabbitLessonDemo / StairLessonDemo). Everything is STATIC
// presentation (no Interactable, no presenter, no quest/collect/scoring).
// Nav discipline unchanged: solids bake off-corridor, overhead dressing is
// bake-ignored, pads stay thin.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CountingGardenBuilder : MonoBehaviour {
  public const string SceneName = "CountingGardenScene";
  // The garden keeps exactly two playable plots: the carrot patch (rabbit,
  // zone 0, west) and the number-stair hill ("Đồi Bậc Thang", zone 1, east of
  // the plaza).
  public const int ZoneCount = 2;
  // Gameplay #2: the number-stair hill opens its own lazy play scene.
  public const int StairZoneIndex = 1;
  // Gameplay #3 ("Cho thỏ ăn đúng số"): the carrot patch opens the rabbit arena.
  public const int RabbitZoneIndex = 0;

  // Separate island far from MarketScene (0) and MathScene (+60x).
  public static readonly Vector3 WorldOffset = new Vector3(120f, 0f, 0f);
  // Click bounds while the child is INSIDE this micro world (S3-P2V journey
  // bug: the router kept the Math bounds (60±27) after the warp, so every
  // garden click at x≈120 was silently ignored — the child could not walk).
  public const float BoundX = 22f;
  public const float BoundZ = 22f;
  // Entry spawn sits CLEAR of the exit portal radius (journey J4 lesson).
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -8f);
  public static readonly Vector3 HubReturn = new Vector3(0f, 0f, 0f); // filled by the hub side
  // Plaza heart (orientation + viewing floor for the two plots).
  public static readonly Vector3 ArcCenter = new Vector3(0f, 0f, 1.5f);
  // Follow camera for THIS world. Two fixes over the inherited Math offset:
  // closer/lower (the world read like a map from 0,6,8.3) AND flipped to the
  // NORTH side — the garden opens SOUTH from the entry, so the camera must
  // look INTO the garden (the old +z offset stared at the entry arch with the
  // whole world behind the child; S3-P2V round-2 capture).
  public static readonly Vector3 FollowOffset = new Vector3(0f, 3.8f, -5.0f);
  // Crescent: TWO plots at r9.5 around the plaza: the carrot patch (west,
  // -50°) and the number-stair hill (east of the plaza, +50°).
  public const float CrescentRadius = 9.5f;
  static readonly float[] ZoneAngles = { -50f, 50f };

  static readonly Color Lawn = new Color(0.38f, 0.64f, 0.36f);
  static readonly Color Meadow = new Color(0.46f, 0.71f, 0.42f);
  static readonly Color BorderWood = new Color(0.52f, 0.36f, 0.22f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);
  static readonly Color CourtyardSand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color Soil = new Color(0.42f, 0.30f, 0.20f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color BasketBrown = new Color(0.55f, 0.38f, 0.22f);
  static readonly Color MintLeaf = new Color(0.70f, 0.90f, 0.72f);
  static readonly Color StoneGrey = new Color(0.68f, 0.68f, 0.66f);
  static readonly Color BoardCream = new Color(0.99f, 0.95f, 0.85f);
  // Gameplay #2 (stair hill) shared tones — same warm wood language as the beds.
  public static readonly Color StepWood = new Color(0.62f, 0.45f, 0.28f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public readonly List<Vector3> ZoneCenters = new List<Vector3>();
  // One click/proximity spot per crescent plot, pushed into CountingGardenArea
  // on each lazy load (SetGarden) so focus/panel always points at live nodes.
  public readonly List<GardenZoneSpot> ZoneSpots = new List<GardenZoneSpot>();
  // Per-zone gate root (hidden while that plot's demo plays).
  public readonly List<GameObject> BedGates = new List<GameObject>();
  public MicroWorldPortal ExitPortal { get; private set; }

  // Scene entry (GameInstaller calls this after the lazy load).
  public void Build() {
    BuildContent(transform);
    // Flat-ground discipline (rabbit/journey lesson): every decor renderer is
    // excluded from the bake so no low mesh can carve the walk surface.
    IgnoreAllDecorExceptGround(transform, "CGGround");
    BuildNavMesh(transform);
  }

  static void IgnoreAllDecorExceptGround(Transform root, string groundName) {
    if (root == null) return;
    foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true)) {
      if (r == null || r.gameObject == null) continue;
      if (r.gameObject.name == groundName) continue;
      IgnoreFromBuild(r.gameObject);
    }
  }

  // Runtime NavMesh bake for THIS scene only (CollectObjects.Children on the
  // garden root — same J8 lesson as MathWorldBuilder: a scene-wide bake would
  // collect MarketScene/MathScene meshes and bake the player as an obstacle).
  void BuildNavMesh(Transform parent) {
    Unity.AI.Navigation.NavMeshSurface surface =
      parent.gameObject.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
    if (surface == null) surface = parent.gameObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
    surface.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
    surface.BuildNavMesh();
  }

  public void BuildContent(Transform root) {
    BuildGround(root);
    BuildEntryAndReturn(root);
    BuildCrescent(root);
    BuildPaths(root);
    BuildFoundation(root);
    BuildDressing(root);
    BuildAnchors(root);
    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  // ---- ground ------------------------------------------------------------------

  void BuildGround(Transform parent) {
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "CGRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(42f, 1.4f, 42f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    StripCollider(rim);
    IgnoreFromBuild(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "CGGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localPosition = Vector3.zero;
    ground.transform.localScale = new Vector3(4.0f, 1f, 4.0f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    // Meadow patches (S3-P2V: break the single-green monotony with two value
    // steps of grass; flat, bake-ignored, nothing gameplay).
    MeadowPatch(parent, "CGMeadowW", new Vector3(-7.5f, 0f, -2.5f), 10f, 7f);
    MeadowPatch(parent, "CGMeadowE", new Vector3(7.5f, 0f, -2.5f), 10f, 7f);
    MeadowPatch(parent, "CGMeadowS", new Vector3(0f, 0f, 14.5f), 12f, 8f);
    // Hedge ring (fewer, bigger bushes — framing, never blocking).
    float[] angles = { 10f, 40f, 70f, 100f, 130f, 160f, 190f, 220f, 250f, 280f, 310f, 340f };
    for (int i = 0; i < angles.Length; i++) {
      float rad = angles[i] * Mathf.Deg2Rad;
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "CGHedge" + i;
      bush.transform.SetParent(parent, false);
      bush.transform.localPosition = new Vector3(Mathf.Sin(rad) * 19f, 0.55f, 2f + Mathf.Cos(rad) * 19f);
      bush.transform.localScale = (i % 2 == 0) ? new Vector3(3.6f, 2.4f, 3.6f) : new Vector3(3.0f, 2.0f, 3.0f);
      bush.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.24f, 0.55f, 0.30f));
      StripCollider(bush);
    }
  }

  void MeadowPatch(Transform parent, string name, Vector3 pos, float dx, float dz) {
    GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    patch.name = name;
    patch.transform.SetParent(parent, false);
    patch.transform.localPosition = new Vector3(pos.x, 0.004f, pos.z);
    patch.transform.localScale = new Vector3(dx, 0.004f, dz);
    patch.GetComponent<Renderer>().sharedMaterial = Lit(Meadow);
    StripCollider(patch);
    IgnoreFromBuild(patch);
  }

  // ---- entry / return ----------------------------------------------------------

  void BuildEntryAndReturn(Transform parent) {
    // Entry arch: the gate the child "came through", pulled BEHIND the spawn
    // (z-14.5) so it frames the arrival shot without sitting between the
    // flipped follow camera and the child.
    Box(parent, "CGEntryPostL", new Vector3(-1.6f, 1.0f, -14.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    Box(parent, "CGEntryPostR", new Vector3(1.6f, 1.0f, -14.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    GameObject beam = Box(parent, "CGEntryBeam", new Vector3(0f, 2.06f, -14.5f),
      new Vector3(3.4f, 0.16f, 0.16f), WorldBeauty.BlossomPink);
    IgnoreFromBuild(beam);
    WorldBeauty.Ball(parent, "CGEntryBlossom0", new Vector3(0f, 2.42f, -14.5f), 1.1f,
      WorldBeauty.BlossomPink);
    WorldBeauty.Ball(parent, "CGEntryBlossomL", new Vector3(-0.9f, 2.3f, -14.5f), 0.8f,
      WorldBeauty.BlossomCream);
    WorldBeauty.Ball(parent, "CGEntryBlossomR", new Vector3(0.9f, 2.34f, -14.5f), 0.85f,
      WorldBeauty.BlossomDeep);
    // Entry threshold: two low stone discs flanking the walk (readable doorway).
    Pad(parent, "CGThresholdL", new Vector3(-1.5f, 0.005f, -8.6f), 1.0f, StoneGrey);
    Pad(parent, "CGThresholdR", new Vector3(1.5f, 0.005f, -8.6f), 1.0f, StoneGrey);
    // Walk-home marker (walking back to it unloads the scene -> Math Hub).
    Pad(parent, "CGExitDisc", new Vector3(0f, 0.01f, -10f), 3.2f, WorldBeauty.Petal);
    GameObject exitGo = new GameObject("CGExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = new Vector3(0f, 0f, -10.4f);
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.fireRadius = 1.35f;
    exit.areaId = CountingGardenArea.AreaId;
    ExitPortal = exit;
    // Exit landmark (mint posts: "way home" by colour, portal untouched).
    Box(parent, "CGExitPostL", new Vector3(-1.6f, 0.9f, -10.4f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Box(parent, "CGExitPostR", new Vector3(1.6f, 0.9f, -10.4f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Sphere(parent, "CGExitCapL", new Vector3(-1.6f, 1.9f, -10.4f), 0.4f, MintLeaf, true);
    Sphere(parent, "CGExitCapR", new Vector3(1.6f, 1.9f, -10.4f), 0.4f, MintLeaf, true);
  }

  // ---- the crescent: the carrot bed (zone 0) + the number-stair hill (zone 1) ----

  void BuildCrescent(Transform parent) {
    ZoneCenters.Clear();
    for (int z = 0; z < ZoneCount; z++) {
      float a = ZoneAngles[z] * Mathf.Deg2Rad;
      Vector3 center = ArcCenter + new Vector3(Mathf.Sin(a) * CrescentRadius, 0f,
        Mathf.Cos(a) * CrescentRadius);
      ZoneCenters.Add(center);
    }
    BuildBed(parent, 0, 3, "carrot");
    BuildStairHillPlot(parent, StairZoneIndex);
    BuildZoneSpots(parent);
  }

  // ---- S3-P2Z12 gameplay #2 plot: the number-stair hill -------------------------
  // The garden-side MINIATURE of the "Bậc thang con số" arena: the same shaped
  // hill (6 steps, target "3" board, goal flag) built at toy scale inside the
  // plot, so the child sees what the door opens. The plot keeps the bed
  // contract (CGZone1Pad/Border/Fence/Anchor + gate + vignette) so the picker
  // and the layout tests read one shape for every plot.
  // The diorama numbers live with StairLessonDemo (it builds the mini lesson);
  // the plot keeps only the arena's target for its gate beads.
  public const int StairTarget = 3;

  void BuildStairHillPlot(Transform parent, int index) {
    Vector3 center = ZoneCenters[index];
    Vector3 outDir = (center - ArcCenter).normalized;
    Vector3 lat = new Vector3(-outDir.z, 0f, outDir.x);
    float latYaw = Mathf.Atan2(lat.x, lat.z) * Mathf.Rad2Deg;
    float outYaw = Mathf.Atan2(outDir.x, outDir.z) * Mathf.Rad2Deg;
    string tag = "CGZone" + index;
    Vector3 mouth = center - outDir * 1.35f;
    // Border ring + walkable pad (same language as the beds).
    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ring.name = tag + "Border";
    ring.transform.SetParent(parent, false);
    ring.transform.localPosition = new Vector3(center.x, 0.004f, center.z);
    ring.transform.localScale = new Vector3(3.5f, 0.006f, 2.7f);
    ring.transform.localRotation = Quaternion.Euler(0f, latYaw, 0f);
    ring.GetComponent<Renderer>().sharedMaterial = Lit(BorderWood);
    StripCollider(ring);
    GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pad.name = tag + "Pad";
    pad.transform.SetParent(parent, false);
    pad.transform.localPosition = new Vector3(center.x, 0.008f, center.z);
    pad.transform.localScale = new Vector3(3.0f, 0.012f, 2.2f);
    pad.transform.localRotation = Quaternion.Euler(0f, latYaw, 0f);
    pad.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.72f, 0.72f, 0.55f));
    StripCollider(pad);
    // NOTE (S3-P2Z12b): the diorama itself — the two-NPC mini lesson with its
    // board/steps/arch — is built at RUNTIME by StairLessonDemo (a runtime
    // component like the ball theatre's CountingDemo), so the plot keeps only
    // its contract pieces (border/pad/fence/gate/anchor/vignette) here.
    // Fence ring (bed contract: 3 outer + 2x2 ends).
    for (int i = 0; i < 3; i++) {
      float t = (i - 1) * 0.95f;
      PlaceProp(parent, "fence_simpleLow", tag + "Fence" + i,
        center + outDir * 1.12f + lat * t, latYaw, 0.92f);
    }
    for (int s = 0; s < 2; s++) {
      float sign = (s == 0) ? -1f : 1f;
      for (int i = 0; i < 2; i++) {
        PlaceProp(parent, "fence_simpleLow", tag + "Fence" + (3 + s * 2 + i),
          center + lat * (sign * 1.55f) + outDir * ((i - 0.5f) * 0.95f), outYaw, 0.92f);
      }
    }
    // Gate at the mouth with THREE beads (the arena's target) + stair-sky accent.
    List<Transform> beads = BuildBedGate(parent, index, tag, mouth, lat,
      new Color(0.45f, 0.70f, 0.92f), StairTarget);
    PlaceProp(parent, "flower_yellowA", tag + "FlowerL", mouth + lat * 1.7f, 0f, 0.9f);
    PlaceProp(parent, "flower_yellowA", tag + "FlowerR", mouth - lat * 1.7f, 0f, 0.9f);
    GameObject anchor = new GameObject(tag + "Anchor");
    anchor.transform.SetParent(parent, false);
    anchor.transform.localPosition = mouth;
    // Always-on counting performance on the gate beads (like every plot).
    GameObject vigGo = new GameObject(tag + "Vignette");
    vigGo.transform.SetParent(parent, false);
    vigGo.transform.localPosition = center;
    GardenZoneVignette vig = vigGo.AddComponent<GardenZoneVignette>();
    vig.Phase = index * 0.9f;
    vig.Bind(beads, new List<Transform>());
  }

  // Every plot gets a GardenZoneSpot — a thin click pad at the mouth (collider
  // KEPT for ClickRouter, bake-ignored so the pad never textures the NavMesh)
  // plus its own camera/look pair for the focus beat. Both plots are staged
  // with play (rabbit at zone 0, stairs at zone 1). All positions derive from
  // the same crescent math as the beds (no magic numbers duplicated).
  void BuildZoneSpots(Transform parent) {
    ZoneSpots.Clear();
    for (int z = 0; z < ZoneCount; z++) {
      Vector3 center = ZoneCenters[z];
      Vector3 outDir = (center - ArcCenter).normalized;
      Vector3 mouth = center - outDir * 1.35f;
      GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      pad.name = "CGZone" + z + "Spot";
      pad.transform.SetParent(parent, false);
      pad.transform.localPosition = new Vector3(mouth.x, 0.04f, mouth.z);
      pad.transform.localScale = new Vector3(2.0f, 0.03f, 2.0f);
      pad.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
      pad.AddComponent<GardenZoneSpot>(); // collider stays: this is the click door
      IgnoreFromBuild(pad);
      GardenZoneSpot spot = pad.GetComponent<GardenZoneSpot>();
      spot.zoneIndex = z;
      spot.playEnabled = true;
      // Which LAZY play scene the door opens (empty = no arena at all).
      // S3-P2Z22: the plot's gate root (the area hides it during the demo so the
      // focus camera never looks through the doorway frame).
      spot.GateRoot = (z < BedGates.Count) ? BedGates[z] : null;
      spot.playSceneName = (z == StairZoneIndex) ? StairHillBuilder.SceneName
        : (z == RabbitZoneIndex) ? RabbitPlayBuilder.SceneName : "";
      // Both plots preview their lesson through a garden miniature and wait for
      // one try-run before the play door opens.
      spot.demoGate = true;
      // Focus framing: the carrot diorama is the WIDEST (patch west + hutch
      // east), so its camera pulls back toward the plaza; the stair camera sits
      // INSIDE the plot, past the mouth, looking across the diorama (no gate
      // between camera and stage).
      Vector3 camPos, lookPos;
      if (z == RabbitZoneIndex) {
        camPos = mouth - outDir * 1.9f + new Vector3(0f, 2.9f, 0f);
        lookPos = center + new Vector3(0f, 0.5f, 0f);
      } else {
        camPos = mouth + outDir * 0.55f + new Vector3(0f, 1.85f, 0f);
        lookPos = center + outDir * 1.55f + new Vector3(0f, 0.65f, 0f);
      }
      // ROOT-CAUSE FIX (user report "camera đang fail"): these anchors were
      // parented to the PAD, which is a cylinder scaled (2, 0.03, 2) — the
      // child transform inherited that scale, so the camera landed at y≈0.1
      // and looked past the garden rim. Anchors must live on the unscaled
      // garden root at their designed world positions.
      GameObject cam = new GameObject("SpotCam");
      cam.transform.SetParent(parent, false);
      cam.transform.localPosition = camPos;
      GameObject look = new GameObject("SpotLook");
      look.transform.SetParent(parent, false);
      look.transform.localPosition = lookPos;
      spot.CameraAnchor = cam.transform;
      spot.LookAnchor = look.transform;
      ZoneSpots.Add(spot);
    }
  }

  // One future activity garden: soil bed + counted crops + fence ring (mouth
  // toward the plaza) + numbered mouth post (N gold beads = the bed's number).
  // S3-P2Y (user: "các khu chơi chưa có phân định ranh giới"): every plot gets
  // a contrasting ground BORDER RING + an always-on drawing vignette (beads pop
  // in sequence, crops breathe) so all five zones read as equal places and
  // share the eye (not only the demo theatre).
  void BuildBed(Transform parent, int index, int cropCount, string crop) {
    Vector3 center = ZoneCenters[index];
    Vector3 outDir = (center - ArcCenter).normalized;
    Vector3 lat = new Vector3(-outDir.z, 0f, outDir.x);
    float latYaw = Mathf.Atan2(lat.x, lat.z) * Mathf.Rad2Deg;
    float outYaw = Mathf.Atan2(outDir.x, outDir.z) * Mathf.Rad2Deg;
    string tag = "CGZone" + index;
    // Border ring FIRST (wider, slightly lower): the plot edge, readable from
    // the entry; the soil sits on top of it.
    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ring.name = tag + "Border";
    ring.transform.SetParent(parent, false);
    ring.transform.localPosition = new Vector3(center.x, 0.004f, center.z);
    ring.transform.localScale = new Vector3(3.5f, 0.006f, 2.7f);
    ring.transform.localRotation = Quaternion.Euler(0f, latYaw, 0f);
    ring.GetComponent<Renderer>().sharedMaterial = Lit(BorderWood);
    StripCollider(ring);
    // Soil bed (tangential 3.0 x radial 2.2) — warm brown breaks the green.
    GameObject soil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    soil.name = tag + "Pad";
    soil.transform.SetParent(parent, false);
    soil.transform.localPosition = new Vector3(center.x, 0.008f, center.z);
    soil.transform.localScale = new Vector3(3.0f, 0.012f, 2.2f);
    soil.transform.localRotation = Quaternion.Euler(0f, latYaw, 0f);
    soil.GetComponent<Renderer>().sharedMaterial = Lit(Soil);
    StripCollider(soil);
    // Counted crops (the bed's future activity already has a countable garden).
    List<Transform> crops = new List<Transform>();
    for (int i = 0; i < cropCount; i++) {
      float t = cropCount > 1 ? (i / (float)(cropCount - 1) - 0.5f) : 0f;
      Vector3 p = center + lat * (t * 2.1f) + outDir * ((i % 2 == 0) ? -0.32f : 0.42f);
      GameObject cropGo = PlaceProp(parent, crop, tag + "Crop" + i, p, outYaw + 180f, 0.85f);
      if (cropGo != null) crops.Add(cropGo.transform);
    }
    // Fence: 3 outer pieces + 2 ends x 2 = low, light, framing only.
    for (int i = 0; i < 3; i++) {
      float t = (i - 1) * 0.95f;
      PlaceProp(parent, "fence_simpleLow", tag + "Fence" + i,
        center + outDir * 1.12f + lat * t, latYaw, 0.92f);
    }
    for (int s = 0; s < 2; s++) {
      float sign = (s == 0) ? -1f : 1f;
      for (int i = 0; i < 2; i++) {
        PlaceProp(parent, "fence_simpleLow", tag + "Fence" + (3 + s * 2 + i),
          center + lat * (sign * 1.55f) + outDir * ((i - 0.5f) * 0.95f), outYaw, 0.92f);
      }
    }
    // S3-P2Z11 (user: "chỉ thấy 2 cổng rõ ràng"): every bed now owns a real
    // garden GATE at its mouth — two posts + a crop-accent beam — and the
    // numbered post (N gold beads) rides the LEFT gatepost, so the doorway
    // stays open (the old centre post stood in the middle of the entrance).
    Vector3 mouth = center - outDir * 1.35f;
    List<Transform> beads = BuildBedGate(parent, index, tag, mouth, lat, AccentFor(index));
    PlaceProp(parent, "flower_yellowA", tag + "FlowerL", mouth + lat * 1.7f, 0f, 0.9f);
    PlaceProp(parent, "flower_yellowA", tag + "FlowerR", mouth - lat * 1.7f, 0f, 0.9f);
    GameObject anchor = new GameObject(tag + "Anchor");
    anchor.transform.SetParent(parent, false);
    anchor.transform.localPosition = mouth;
    // Always-on counting performance (transform-only).
    GameObject vigGo = new GameObject(tag + "Vignette");
    vigGo.transform.SetParent(parent, false);
    vigGo.transform.localPosition = center;
    GardenZoneVignette vig = vigGo.AddComponent<GardenZoneVignette>();
    vig.Phase = index * 0.9f;
    vig.Bind(beads, crops);
  }

  // One real garden gate per bed (S3-P2Z11): a tall numbered post on the left
  // (N gold beads), a plain post on the right, a crop-accent beam across the
  // top (bake-ignored — the headroom rule) and blossom balls on the beam.
  // User round ("player còn cao hơn cả cổng"): the beam sat at 1.52m — lower
  // than the child (1.6m collider), so they clipped through it. The whole gate
  // rose ~0.5m: the doorway is 2.0m clear now, still child-scaled.
  List<Transform> BuildBedGate(Transform parent, int index, string tag, Vector3 mouth,
      Vector3 lat, Color accent, int beadCount = -1) {
    List<Transform> beads = new List<Transform>();
    // S3-P2Z22 (user: "khi xem demo thì bỏ cái cửa này đi"): the whole gate lives
    // under ONE root so the area can hide it while the plot's demo plays (the
    // carrot focus camera sits outside the mouth and used to look through it).
    GameObject gateRoot = new GameObject(tag + "GateRoot");
    gateRoot.transform.SetParent(parent, false);
    while (BedGates.Count <= index) BedGates.Add(null);
    BedGates[index] = gateRoot;
    Transform gp = gateRoot.transform;
    Vector3 postL = mouth + lat * 1.1f;
    Vector3 postR = mouth - lat * 1.1f;
    Cylinder(gp, tag + "Post", postL + new Vector3(0f, 1.1f, 0f),
      0.17f, 2.2f, BasketBrown);
    int beadsOnPost = beadCount >= 0 ? beadCount : index + 1;
    for (int i = 0; i < beadsOnPost; i++) {
      GameObject bead = Sphere(gp, tag + "PostBead" + i,
        postL + new Vector3(0f, 2.32f + i * 0.18f, 0f), 0.17f, Gold, false);
      if (bead != null) beads.Add(bead.transform);
    }
    Cylinder(gp, tag + "GatePostR", postR + new Vector3(0f, 0.9f, 0f),
      0.15f, 1.8f, BasketBrown);
    GameObject beam = Box(gp, tag + "GateBeam", mouth + new Vector3(0f, 1.98f, 0f),
      new Vector3(0.13f, 0.13f, 2.5f), accent);
    if (beam != null) {
      beam.transform.localRotation = Quaternion.LookRotation(new Vector3(lat.x, 0f, lat.z));
      IgnoreFromBuild(beam);
    }
    Sphere(gp, tag + "GateBall0", mouth + new Vector3(0f, 2.24f, 0f), 0.42f, accent, true);
    Sphere(gp, tag + "GateBall1", mouth + lat * 0.55f + new Vector3(0f, 2.14f, 0f),
      0.26f, Gold, true);
    Sphere(gp, tag + "GateBall2", mouth - lat * 0.55f + new Vector3(0f, 2.14f, 0f),
      0.26f, Gold, true);
    return beads;
  }

  static Color AccentFor(int index) {
    switch (index) {
      case 0: return new Color(0.95f, 0.55f, 0.20f); // carrot
      default: return Gold;
    }
  }

  // CONNECTED seven-segment digits 0-9 — one table drives every number board.
  // Same plane/thickness discipline as the original Digit2: segments overlap at
  // the joints (length + t) so the glyph is one solid connected number, and
  // per-segment X thickness avoids coplanar z-fighting (journey: striped bar).
  // Local +z maps to world -x at yaw 90 and the north viewer reads screen-right
  // = local +z, so B/C (right verticals) sit at +z and E/F (left) at -z.
  // Group origin at the base so emphasis pulses grow upward.
  // Standard 7-seg sets, index = digit (out of range reads as 8).
  public static readonly string[][] DigitSegSets = {
    new[] { "A", "B", "C", "D", "E", "F" },       // 0
    new[] { "B", "C" },                            // 1
    new[] { "A", "B", "G", "E", "D" },             // 2
    new[] { "A", "B", "G", "C", "D" },             // 3
    new[] { "B", "C", "F", "G" },                  // 4
    new[] { "A", "C", "D", "F", "G" },             // 5
    new[] { "A", "C", "D", "E", "F", "G" },        // 6
    new[] { "A", "B", "C" },                       // 7
    new[] { "A", "B", "C", "D", "E", "F", "G" },   // 8
    new[] { "A", "B", "C", "D", "F", "G" },        // 9
  };

  public static GameObject Digit(Transform parent, string name, Vector3 origin, float h, float w,
      Color color, float yawDeg, int n) {
    GameObject g = new GameObject(name);
    g.transform.SetParent(parent, false);
    g.transform.localPosition = origin;
    g.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
    float t = Mathf.Max(0.09f, h * 0.19f);          // stroke thickness
    float hLen = w + t;                              // horizontal segments
    float vLen = h * 0.5f + t;                       // vertical segments (overlap)
    string[] set = (n >= 0 && n < DigitSegSets.Length) ? DigitSegSets[n] : DigitSegSets[8];
    foreach (string s in set) {
      switch (s) {
        case "A":
          Box(g.transform, name + "A", new Vector3(0f, h, 0f),
            new Vector3(t * 1.00f, t, hLen), color);
          break;
        case "B":
          Box(g.transform, name + "B", new Vector3(0f, h * 0.75f, w * 0.5f),
            new Vector3(t * 0.90f, vLen, t), color);
          break;
        case "C":
          Box(g.transform, name + "C", new Vector3(0f, h * 0.25f, w * 0.5f),
            new Vector3(t * 0.94f, vLen, t), color);
          break;
        case "D":
          Box(g.transform, name + "D", new Vector3(0f, 0f, 0f),
            new Vector3(t * 1.03f, t, hLen), color);
          break;
        case "E":
          Box(g.transform, name + "E", new Vector3(0f, h * 0.25f, -w * 0.5f),
            new Vector3(t * 0.94f, vLen, t), color);
          break;
        case "F":
          Box(g.transform, name + "F", new Vector3(0f, h * 0.75f, -w * 0.5f),
            new Vector3(t * 0.90f, vLen, t), color);
          break;
        case "G":
          Box(g.transform, name + "G", new Vector3(0f, h * 0.5f, 0f),
            new Vector3(t * 1.06f, t, hLen), color);
          break;
      }
    }
    return g;
  }

  // Group origin at the base so emphasis pulses grow upward.
  public static GameObject Digit2(Transform parent, string name, Vector3 origin, float h, float w,
      Color color, float yawDeg) {
    return Digit(parent, name, origin, h, w, color, yawDeg, 2);
  }

  // CONNECTED seven-segment "3" (A/B/G/C/D) — kept as a named wrapper so the
  // gameplay #1/#2 pins keep reading; new code uses Digit(..., n).
  public static GameObject Digit3(Transform parent, string name, Vector3 origin, float h, float w,
      Color color, float yawDeg) {
    return Digit(parent, name, origin, h, w, color, yawDeg, 3);
  }

  // Floor tick: short down-stroke + long up-stroke in the ZY plane.
  public static void CheckMark(Transform parent, string name, Vector3 origin, float size, Color color) {
    GameObject s1 = Box(parent, name + "Stem", origin + new Vector3(0f, size * 0.4f, size * 0.25f),
      new Vector3(0.11f, 0.11f, size * 0.55f), color);
    s1.transform.localRotation = Quaternion.Euler(-55f, 0f, 0f);
    GameObject s2 = Box(parent, name + "Arm", origin + new Vector3(0f, size * 0.75f, -size * 0.25f),
      new Vector3(0.11f, 0.11f, size * 0.95f), color);
    s2.transform.localRotation = Quaternion.Euler(48f, 0f, 0f);
  }

  // ---- paths -------------------------------------------------------------------

  void BuildPaths(Transform parent) {
    // Plaza (orientation heart) + entry walk.
    Pad(parent, "CGPlazaPad", new Vector3(ArcCenter.x, 0.004f, ArcCenter.z), 7.6f, CourtyardSand);
    Seg(parent, "CGPathEntry", new Vector3(0f, 0f, -11f), new Vector3(0f, 0f, -1.0f), 1.7f);
    // Crescent walk: an arc band joining the two plot mouths (-50..+50).
    const float walkR = 8.0f;
    for (int i = 0; i < 6; i++) {
      float a0 = -60f + i * 20f - 2f;
      float a1 = a0 + 20f + 4f;
      Vector3 p0 = ArcCenter + new Vector3(Mathf.Sin(a0 * Mathf.Deg2Rad) * walkR, 0f,
        Mathf.Cos(a0 * Mathf.Deg2Rad) * walkR);
      Vector3 p1 = ArcCenter + new Vector3(Mathf.Sin(a1 * Mathf.Deg2Rad) * walkR, 0f,
        Mathf.Cos(a1 * Mathf.Deg2Rad) * walkR);
      Seg(parent, "CGPathCrescent" + i, p0, p1, 1.6f);
    }
    // Two approach spurs: plaza -> carrot mouth (west) and plaza -> stair mouth.
    Seg(parent, "CGPathCarrotSpur", new Vector3(-2.0f, 0f, 3.2f), new Vector3(-6.2f, 0f, 6.7f), 1.5f);
    Seg(parent, "CGPathStairSpur", new Vector3(2.0f, 0f, 3.2f), new Vector3(6.2f, 0f, 6.7f), 1.5f);
    // Reward spur (plaza -> celebration pocket, west).
    Seg(parent, "CGPathReward", new Vector3(-3.0f, 0f, 1.9f), new Vector3(-5.0f, 0f, 2.2f), 1.4f);
  }

  // ---- S3-P1 foundation (STATIC presentation only — NO gameplay) ----------------

  void BuildFoundation(Transform parent) {
    // Number stones 1..5 across the plaza's south half (flat, readable from the
    // arrival camera; each stone carries N gold beads — the counting signature).
    for (int i = 0; i < 5; i++) {
      Vector3 p = new Vector3(-2.4f + i * 1.2f, 0f, 3.0f);
      Cylinder(parent, "CGNumberStone" + i, p + new Vector3(0f, 0.04f, 0f), 0.75f, 0.08f, StoneGrey);
      for (int b = 0; b <= i; b++) {
        float t = (i == 0) ? 0f : (b / (float)i - 0.5f);
        Sphere(parent, "CGNumberStone" + i + "Bead" + b,
          p + new Vector3(t * 0.42f, 0.15f, 0f), 0.16f, Gold, false);
      }
    }
    // Orientation landmark trio (counting stack 1-2-3) east of the plaza.
    Box(parent, "CGOrientTrio0", new Vector3(2.9f, 0.25f, 3.4f),
      new Vector3(0.5f, 0.5f, 0.5f), Gold);
    Box(parent, "CGOrientTrio1", new Vector3(3.6f, 0.25f, 3.4f),
      new Vector3(0.5f, 0.5f, 0.5f), WorldBeauty.BlossomDeep);
    Box(parent, "CGOrientTrio2", new Vector3(4.3f, 0.25f, 3.4f),
      new Vector3(0.5f, 0.5f, 0.5f), WorldBeauty.Lilac);
    // NPC staging disc (Phase 2 stages a host here; today a painted circle).
    Pad(parent, "CGNpcStagingDisc", new Vector3(2.8f, 0.012f, 0.8f), 2.2f, BoardCream);
    // FEEDBACK / REWARD pocket (west of the plaza): gold medallion + empty
    // trophy plinth + blossom arch. No reward logic — Phase 3 fills it.
    Pad(parent, "CGRewardDisc", new Vector3(-5.0f, 0.008f, 2.2f), 2.4f, Gold);
    Cylinder(parent, "CGRewardPlinth", new Vector3(-5.0f, 0.25f, 1.0f), 0.8f, 0.5f, BoardCream);
    Sphere(parent, "CGRewardTrophy", new Vector3(-5.0f, 0.62f, 1.0f), 0.36f, Gold, true);
    Box(parent, "CGRewardPostL", new Vector3(-5.0f, 0.9f, 3.5f),
      new Vector3(0.16f, 1.8f, 0.16f), BasketBrown);
    Box(parent, "CGRewardPostR", new Vector3(-5.0f, 0.9f, 0.9f),
      new Vector3(0.16f, 1.8f, 0.16f), BasketBrown);
    GameObject garland = Box(parent, "CGRewardBeam", new Vector3(-5.0f, 1.9f, 2.2f),
      new Vector3(0.14f, 0.14f, 2.6f), WorldBeauty.BlossomPink);
    IgnoreFromBuild(garland);
    Sphere(parent, "CGRewardBlossom0", new Vector3(-5.0f, 2.15f, 1.7f), 0.55f,
      WorldBeauty.BlossomPink, true);
    Sphere(parent, "CGRewardBlossom1", new Vector3(-5.0f, 2.25f, 2.2f), 0.65f,
      WorldBeauty.BlossomCream, true);
    Sphere(parent, "CGRewardBlossom2", new Vector3(-5.0f, 2.15f, 2.7f), 0.55f,
      WorldBeauty.BlossomDeep, true);
  }

  // ---- dressing (S6/S7 beauty kit + PropKit) ------------------------------------

  void BuildDressing(Transform parent) {
    // Blossom trees: entry flanks + rim framing. S3-P2V: pulled OFF the
    // sightlines (the old (12,-2)/(8,6) trees hid the whole zone arc).
    Vector3[] trees = {
      new Vector3(-3.8f, 0f, -6.4f), new Vector3(3.8f, 0f, -6.4f),
      new Vector3(-11.5f, 0f, 9.5f), new Vector3(11.5f, 0f, 9.5f),
      new Vector3(-13f, 0f, -1f), new Vector3(13f, 0f, -1f),
      new Vector3(5.2f, 0f, 16.0f),
    };
    float[] scales = { 0.62f, 0.62f, 0.72f, 0.72f, 0.7f, 0.7f, 0.7f };
    for (int i = 0; i < trees.Length; i++) {
      WorldBeauty.BlossomTree(parent, "CGBlossomTree" + i, trees[i], scales[i]);
      // Smaller carpets: the old 2.2-3m discs read as white puddles.
      WorldBeauty.PetalCarpet(parent, "CGPetalCarpet" + i, trees[i], 1.5f * scales[i] + 0.5f);
    }
    // Flower drifts: gaps between the plaza and the crescent walk (off-path).
    Vector3[] drifts = {
      new Vector3(-3.0f, 0f, 4.2f), new Vector3(3.0f, 0f, 4.2f),
      new Vector3(-6.8f, 0f, 6.8f), new Vector3(6.8f, 0f, 6.8f),
      new Vector3(-2.6f, 0f, -4.6f), new Vector3(2.6f, 0f, -4.6f),
    };
    for (int i = 0; i < drifts.Length; i++)
      WorldBeauty.FlowerDrift(parent, "CGFlowerDrift" + i, drifts[i], 1.2f + (i % 2) * 0.2f);
    // Garden accents at the bed flanks (PropKit flowers, garden identity).
    // Petals over the plaza/entry — NOT over the lesson stage (round-2 capture:
    // drifting petals crossed the instruction card as translucent blobs).
    WorldBeauty.PetalFall(parent, "CGPetalFall", new Vector3(0f, 0f, -2f), 9f, 12, 60909);
    WorldBeauty.Butterfly(parent, "CGButterfly0", new Vector3(3.0f, 0f, 4.2f), 2.2f, 0.1f,
      WorldBeauty.BlossomDeep, WorldBeauty.BlossomCream);
    WorldBeauty.Butterfly(parent, "CGButterfly1", new Vector3(-3.0f, 0f, 4.2f), 2.2f, 0.6f,
      WorldBeauty.Lilac, WorldBeauty.BlossomPink);
    // Pastel rainbow: a far WEST-side landmark. Two rounds of capture taught
    // the distance: anywhere closer and its legs crossed the demo card frame.
    WorldBeauty.PastelRainbow(parent, "CGRainbow", new Vector3(-22f, 0f, 16f), 7f);
  }

  // ---- anchors -----------------------------------------------------------------

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "CountingGardenPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", ArcCenter);
    a.Npc = a.EnsureSlot("NpcAnchor", new Vector3(2.8f, 0f, 0.8f));
    // Arrival reveal: over the entry arch, across the plaza to the crescent.
    // (higher + further back: the old framing let the entry-arch blossom
    // cluster cover the plaza/crescent in the first frame)
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 9.5f, -17.5f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 1.0f, 2.5f));
    a.Prompt = a.EnsureSlot("PromptAnchor", new Vector3(0f, 1.7f, 8.8f));
    a.Feedback = a.EnsureSlot("FeedbackAnchor", new Vector3(-0.8f, 1.2f, 9.2f));
    a.Reward = a.EnsureSlot("RewardAnchor", new Vector3(-5.0f, 0f, 2.2f));
    a.Exit = a.EnsureSlot("ExitAnchor", new Vector3(0f, 0f, -10.4f));
  }

  // ---- helpers -----------------------------------------------------------------

  static GameObject PlaceProp(Transform parent, string prop, string goName, Vector3 pos, float yaw, float scale) {
    GameObject go = PropKit.Place(parent, prop, pos, yaw, scale);
    if (go != null) go.name = goName;
    return go;
  }

  static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    go.transform.localScale = scale;
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(go);
    return go;
  }

  // Walkable / ground pad: thin cylinder, top at y≈0.015 (paths sit on top).
  static void Pad(Transform parent, string name, Vector3 pos, float diameter, Color color) {
    GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pad.name = name;
    pad.transform.SetParent(parent, false);
    pad.transform.localPosition = pos;
    pad.transform.localScale = new Vector3(diameter, 0.01f, diameter);
    pad.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(pad);
  }

  // Knee-high static solid (basket/plinth/post base): bakes as an obstacle —
  // callers keep it off the walk corridors.
  static GameObject Cylinder(Transform parent, string name, Vector3 pos,
      float diameter, float height, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(go);
    return go;
  }

  // Static ball (apple/bead/trophy). Overhead dressing passes ignore=true so
  // it never cuts NavMesh headroom (beam lesson); ground solids stay baked.
  static GameObject Sphere(Transform parent, string name, Vector3 pos,
      float diameter, Color color, bool ignore) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    go.transform.localScale = new Vector3(diameter, diameter, diameter);
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(go);
    if (ignore) IgnoreFromBuild(go);
    return go;
  }

  // Path ribbon: intersects the pads slightly (no coplanar z-fight), top 0.043.
  static void Seg(Transform parent, string name, Vector3 a, Vector3 b, float width) {
    Vector3 flatA = new Vector3(a.x, 0f, a.z);
    Vector3 flatB = new Vector3(b.x, 0f, b.z);
    Vector3 mid = (flatA + flatB) * 0.5f;
    mid.y = 0.028f;
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = mid;
    go.transform.localRotation = Quaternion.LookRotation((flatB - flatA).normalized);
    go.transform.localScale = new Vector3(width, 0.015f, Vector3.Distance(flatA, flatB) + width * 0.5f);
    go.GetComponent<Renderer>().sharedMaterial = Lit(PathTan);
  }

  static void StripCollider(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (System.Exception) { }
  }

  static void IgnoreFromBuild(GameObject go) {
    if (go == null) return;
    try {
      NavMeshModifierRef(go);
    } catch (System.Exception) { }
  }

  static void NavMeshModifierRef(GameObject go) {
    Unity.AI.Navigation.NavMeshModifier mod = go.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
    if (mod == null) mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
    mod.ignoreFromBuild = true;
  }

  static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

  static Material Lit(Color color) {
    string key = color.r.ToString("F2") + "," + color.g.ToString("F2") + "," + color.b.ToString("F2");
    Material cached;
    if (_mats.TryGetValue(key, out cached) && cached != null) return cached;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    mat.enableInstancing = true;
    _mats[key] = mat;
    return mat;
  }
}
