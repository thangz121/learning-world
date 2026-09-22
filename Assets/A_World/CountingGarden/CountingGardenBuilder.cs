// A_World/CountingGarden/CountingGardenBuilder.cs — S3 P1 COUNTING GARDEN
// FOUNDATION (micro-world foundation, DESIGN-FIRST).
// The Counting Garden is its OWN additive scene (like MathScene is to the
// subject-selection hall), LAZY-loaded only when the child walks into the
// counting-garden gate in the Math Hub. This builder code-builds the scene
// content (same pattern as MathWorldBuilder/MarketBuilder: one root GO, all
// geometry in code, deterministic, shared Lit materials).
// S3-P1 scope (NO gameplay, NO demo, NO activity logic): WORLD FOUNDATION +
// SPATIAL BLUEPRINT + ENVIRONMENT + ENTRY + ORIENTATION + DEMO SPACE +
// ACTIVITY SPACE + FEEDBACK/REWARD SPACE + EXIT + NPC STAGING + CAMERA
// COMPOSITION. The five fenced arc plots are kept (v2 contract) and given
// Phase-1 ROLES: Zone2 (centre) = DEMO SPACE (static board + target row +
// result frame), Zone1/Zone3 = MAIN ACTIVITY SPACE (static basket + apples +
// number sign), Zone0/Zone4 = reserve for future phases. The courtyard hosts
// ORIENTATION (landmark trio + NPC staging disc) and FEEDBACK/REWARD
// (celebration disc + empty plinth + garland). Every foundation object is
// STATIC/PRESENTATION ONLY: plain primitives, collider-free, NO Interactable,
// NO presenter, NO quest/collect/scoring logic — Phase 2 (demo) and Phase 3
// (Count & Collect) stage into these spaces later.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CountingGardenBuilder : MonoBehaviour {
  public const string SceneName = "CountingGardenScene";
  public const int ZoneCount = 5;

  // Separate island far from MarketScene (0) and MathScene (+60x).
  public static readonly Vector3 WorldOffset = new Vector3(120f, 0f, 0f);
  // Entry spawn sits CLEAR of the exit portal radius (journey J4 lesson: never
  // land inside a fire radius) — the child walks in, then walks back out.
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -8f);
  public static readonly Vector3 HubReturn = new Vector3(0f, 0f, 0f); // filled by the hub side
  public static readonly Vector3 ArcCenter = new Vector3(0f, 0f, 2f);

  static readonly Color Lawn = new Color(0.38f, 0.64f, 0.36f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);
  static readonly Color CourtyardSand = new Color(0.86f, 0.78f, 0.62f);
  // S3-P1 foundation palette (static presentation only — shapes read without text).
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color AppleRed = new Color(0.85f, 0.25f, 0.25f);
  static readonly Color BasketBrown = new Color(0.55f, 0.38f, 0.22f);
  static readonly Color MintLeaf = new Color(0.70f, 0.90f, 0.72f);
  static readonly Color StoneGrey = new Color(0.68f, 0.68f, 0.66f);
  static readonly Color BoardCream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color SkyBlue = new Color(0.68f, 0.84f, 0.98f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public readonly List<Vector3> ZoneCenters = new List<Vector3>();
  public MicroWorldPortal ExitPortal { get; private set; }

  // Scene entry (GameInstaller calls this after the lazy load).
  public void Build() {
    BuildContent(transform);
    BuildNavMesh(transform);
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
    BuildZones(root);
    BuildPaths(root);
    BuildDressing(root);
    BuildFoundation(root);
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
    rim.transform.SetParent(parent);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(42f, 1.4f, 42f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    StripCollider(rim);
    IgnoreFromBuild(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "CGGround";
    ground.transform.SetParent(parent);
    ground.transform.localPosition = Vector3.zero;
    ground.transform.localScale = new Vector3(4.0f, 1f, 4.0f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    Pad(parent, "CGCenterPad", new Vector3(0f, -0.005f, 2f), 9f, CourtyardSand);
    Pad(parent, "CGEntryPad", new Vector3(0f, -0.005f, -10f), 4.2f, CourtyardSand);
    // Hedge ring for depth (collider-free dressing, inside the rim).
    float[] angles = { 10f, 40f, 70f, 100f, 130f, 160f, 190f, 220f, 250f, 280f, 310f, 340f };
    for (int i = 0; i < angles.Length; i++) {
      float rad = angles[i] * Mathf.Deg2Rad;
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "CGHedge" + i;
      bush.transform.SetParent(parent);
      bush.transform.localPosition = new Vector3(Mathf.Sin(rad) * 19f, 0.55f, 2f + Mathf.Cos(rad) * 19f);
      bush.transform.localScale = (i % 2 == 0) ? new Vector3(3.2f, 2.2f, 3.2f) : new Vector3(2.6f, 1.8f, 2.6f);
      bush.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.24f, 0.55f, 0.30f));
      StripCollider(bush);
    }
  }

  // ---- entry / return ----------------------------------------------------------

  void BuildEntryAndReturn(Transform parent) {
    Box(parent, "CGEntryPostL", new Vector3(-1.6f, 1.0f, -12f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    Box(parent, "CGEntryPostR", new Vector3(1.6f, 1.0f, -12f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    GameObject beam = Box(parent, "CGEntryBeam", new Vector3(0f, 2.06f, -12f),
      new Vector3(3.4f, 0.16f, 0.16f), WorldBeauty.BlossomPink);
    IgnoreFromBuild(beam);
    WorldBeauty.Ball(parent, "CGEntryBlossom0", new Vector3(0f, 2.42f, -12f), 1.1f,
      WorldBeauty.BlossomPink);
    WorldBeauty.Ball(parent, "CGEntryBlossomL", new Vector3(-0.9f, 2.3f, -12f), 0.8f,
      WorldBeauty.BlossomCream);
    WorldBeauty.Ball(parent, "CGEntryBlossomR", new Vector3(0.9f, 2.34f, -12f), 0.85f,
      WorldBeauty.BlossomDeep);
    // Walk-home marker (walking back to it unloads the scene -> Math Hub).
    Pad(parent, "CGExitDisc", new Vector3(0f, 0.02f, -10f), 3.2f, WorldBeauty.Petal);
    GameObject exitGo = new GameObject("CGExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = new Vector3(0f, 0f, -10.4f);
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.fireRadius = 1.35f;
    exit.areaId = CountingGardenArea.AreaId;
    ExitPortal = exit;
  }

  // ---- the five fenced zones in an arc -----------------------------------------

  void BuildZones(Transform parent) {
    // Five equal plots on an arc around the north of the courtyard (the child
    // reads them as "the games garden"): 50..130 deg, r 11 from the courtyard
    // centre. Only enclosures for now (user order) — fences + a mouth toward
    // the courtyard + a sand pad + a marker anchor per zone.
    ZoneCenters.Clear();
    float[] angles = { 50f, 70f, 90f, 110f, 130f };
    for (int z = 0; z < ZoneCount; z++) {
      float a = angles[z];
      Vector3 center = ArcCenter + new Vector3(Mathf.Sin(a * Mathf.Deg2Rad) * 11f, 0f,
        Mathf.Cos(a * Mathf.Deg2Rad) * 11f);
      ZoneCenters.Add(center);
      Pad(parent, "CGZone" + z + "Pad", center + new Vector3(0f, 0.005f, 0f), 7.4f,
        (z % 2 == 0) ? new Color(0.55f, 0.75f, 0.55f) : new Color(0.62f, 0.78f, 0.58f));
      // Fence ring with the mouth facing the courtyard.
      Vector3 toCenter = (ArcCenter - center);
      toCenter.y = 0f;
      toCenter.Normalize();
      float mouthAng = Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg;
      const float r = 3.4f;
      for (int i = 0; i < 22; i++) {
        float ang = i * (360f / 22f);
        if (Mathf.Abs(Mathf.DeltaAngle(ang, mouthAng)) < 20f) continue; // mouth
        float rad = ang * Mathf.Deg2Rad;
        Vector3 p = center + new Vector3(Mathf.Sin(rad) * r, 0f, Mathf.Cos(rad) * r);
        string model = (i % 6 == 0) ? "fence_simpleLow" : "fence_simple";
        PlaceProp(parent, model, "CGZone" + z + "Fence" + i, p, ang, 1.05f);
      }
      // Zone mouth marker (a low arch so each plot reads as an entrance).
      Vector3 mouth = center + toCenter * r;
      Vector3 lat = new Vector3(-toCenter.z, 0f, toCenter.x);
      Box(parent, "CGZone" + z + "PostL", mouth + lat * 0.8f + new Vector3(0f, 0.7f, 0f),
        new Vector3(0.14f, 1.4f, 0.14f), PathTan);
      Box(parent, "CGZone" + z + "PostR", mouth - lat * 0.8f + new Vector3(0f, 0.7f, 0f),
        new Vector3(0.14f, 1.4f, 0.14f), PathTan);
      GameObject top = Box(parent, "CGZone" + z + "Top", mouth + new Vector3(0f, 1.5f, 0f),
        new Vector3(1.9f, 0.14f, 0.14f), WorldBeauty.BlossomPink);
      top.transform.localRotation = Quaternion.Euler(0f, mouthAng, 0f);
      IgnoreFromBuild(top);
      GameObject marker = new GameObject("CGZone" + z + "Anchor");
      marker.transform.SetParent(parent, false);
      marker.transform.localPosition = mouth;
    }
  }

  void BuildPaths(Transform parent) {
    Seg(parent, "CGPathEntry", new Vector3(0f, 0f, -10f), new Vector3(0f, 0f, -1f), 1.6f);
    for (int z = 0; z < ZoneCount; z++) {
      Vector3 mouth = ZoneCenters[z] + (ArcCenter - ZoneCenters[z]).normalized * 3.4f;
      Seg(parent, "CGPathZone" + z, ArcCenter, mouth, 1.3f);
    }
    Seg(parent, "CGPathArc0", ZoneCenters[0] + (ArcCenter - ZoneCenters[0]).normalized * 4.6f,
      ZoneCenters[1] + (ArcCenter - ZoneCenters[1]).normalized * 4.6f, 1.1f);
    Seg(parent, "CGPathArc1", ZoneCenters[1] + (ArcCenter - ZoneCenters[1]).normalized * 4.6f,
      ZoneCenters[2] + (ArcCenter - ZoneCenters[2]).normalized * 4.6f, 1.1f);
    Seg(parent, "CGPathArc2", ZoneCenters[2] + (ArcCenter - ZoneCenters[2]).normalized * 4.6f,
      ZoneCenters[3] + (ArcCenter - ZoneCenters[3]).normalized * 4.6f, 1.1f);
    Seg(parent, "CGPathArc3", ZoneCenters[3] + (ArcCenter - ZoneCenters[3]).normalized * 4.6f,
      ZoneCenters[4] + (ArcCenter - ZoneCenters[4]).normalized * 4.6f, 1.1f);
  }

  // ---- S3-P1 foundation (STATIC presentation only — NO gameplay) ----------------
  // Spatial blueprint (local coords; runtime offset +120x separates islands):
  //   ENTRY (z -12..-8, threshold + arch + exit disc, spawn clears exit radius)
  //     -> ORIENTATION (courtyard 0,2: landmark trio + NPC staging disc)
  //     -> DEMO SPACE (Zone2 centre: static board + 3 pedestals + result frame)
  //     -> MAIN ACTIVITY (Zone1/Zone3: static basket + apples + number sign)
  //     -> FEEDBACK/REWARD (pocket 0,6.4: gold disc + empty plinth + garland)
  //     -> EXIT (disc + mint posts, south). Zone0/Zone4 stay fenced reserve.
  // Nav discipline (journey lessons): ground solids bake as obstacles, so every
  // foundation piece sits OFF the walk corridors (entry walk half 0.8m, spokes
  // half 0.65m, agent radius 0.5m); anything overhead is ignoreFromBuild
  // (headroom rule); pads stay thin/walkable. Nothing here has gameplay
  // behaviour: NO Interactable, NO presenter, NO quest/collect/scoring.

  void BuildFoundation(Transform parent) {
    // ENTRY threshold: two low stone discs flanking the entry walk (read the
    // doorway without text; walkable pads, never block the 1.6m walk; y-stepped
    // above the entry pad so overlapping pads never z-fight).
    Pad(parent, "CGThresholdL", new Vector3(-1.3f, 0.015f, -9.2f), 1.0f, StoneGrey);
    Pad(parent, "CGThresholdR", new Vector3(1.3f, 0.015f, -9.2f), 1.0f, StoneGrey);
    // ORIENTATION: landmark trio (counting identity 1-2-3 as gold/blue/pink
    // blocks) east of the courtyard + NPC staging disc (Phase 2 stages Tess
    // here; today it is a painted circle, no presenter, no AI).
    Box(parent, "CGOrientTrio0", new Vector3(2.0f, 0.25f, 4.8f),
      new Vector3(0.5f, 0.5f, 0.5f), Gold);
    Box(parent, "CGOrientTrio1", new Vector3(2.7f, 0.25f, 4.8f),
      new Vector3(0.5f, 0.5f, 0.5f), SkyBlue);
    Box(parent, "CGOrientTrio2", new Vector3(3.4f, 0.25f, 4.8f),
      new Vector3(0.5f, 0.5f, 0.5f), WorldBeauty.BlossomDeep);
    Pad(parent, "CGNpcStagingDisc", new Vector3(-2.2f, 0.015f, 0.5f), 2.2f, BoardCream);
    // DEMO SPACE (Zone2 centre plot): static board at the back, three target
    // pedestals in a row, one result frame to the side. Off the mouth axis
    // (x=0 walk enters south, stops at the row — a destination, not a block).
    if (ZoneCenters.Count == ZoneCount) {
      Vector3 demo = ZoneCenters[2];
      Vector3 back = demo + ((demo - ArcCenter).normalized * 1.8f);
      Box(parent, "CGDemoBoardL", back + new Vector3(-1.0f, 0.65f, 0f),
        new Vector3(0.14f, 1.3f, 0.14f), BasketBrown);
      Box(parent, "CGDemoBoardR", back + new Vector3(1.0f, 0.65f, 0f),
        new Vector3(0.14f, 1.3f, 0.14f), BasketBrown);
      Box(parent, "CGDemoBoardPanel", back + new Vector3(0f, 1.35f, 0f),
        new Vector3(2.2f, 1.2f, 0.12f), BoardCream);
      Vector3 row = demo + ((demo - ArcCenter).normalized * 0.8f);
      Box(parent, "CGDemoTarget0", row + new Vector3(-1.1f, 0.25f, 0f),
        new Vector3(0.5f, 0.5f, 0.5f), MintLeaf);
      Box(parent, "CGDemoTarget1", row + new Vector3(0f, 0.25f, 0f),
        new Vector3(0.5f, 0.5f, 0.5f), SkyBlue);
      Box(parent, "CGDemoTarget2", row + new Vector3(1.1f, 0.25f, 0f),
        new Vector3(0.5f, 0.5f, 0.5f), WorldBeauty.Lilac);
      Box(parent, "CGDemoResultFrame", new Vector3(demo.x + 1.8f, 0.5f, demo.z - 0.2f),
        new Vector3(0.9f, 1.0f, 0.12f), Gold);
      // MAIN ACTIVITY SPACE (Zone1: 2 apples / Zone3: 3 apples — static count
      // variety, NO pickup logic). Basket at the back, sign lateral off-walk.
      BuildActivityPlot(parent, ZoneCenters[1], 2);
      BuildActivityPlot(parent, ZoneCenters[3], 3);
    }
    // FEEDBACK / REWARD pocket (courtyard north): gold celebration disc under
    // the walk (walkable), EMPTY plinth to the side (Phase 3 fills it — today
    // no reward logic, no bloom), blossom garland overhead (ignored in bake).
    Pad(parent, "CGRewardDisc", new Vector3(0f, 0.015f, 6.4f), 2.6f, Gold);
    Cylinder(parent, "CGRewardPlinth", new Vector3(1.6f, 0.2f, 7.2f), 0.8f, 0.4f, BoardCream);
    Box(parent, "CGRewardPostL", new Vector3(-1.6f, 0.9f, 6.4f),
      new Vector3(0.16f, 1.8f, 0.16f), BasketBrown);
    Box(parent, "CGRewardPostR", new Vector3(1.6f, 0.9f, 6.4f),
      new Vector3(0.16f, 1.8f, 0.16f), BasketBrown);
    GameObject garland = Box(parent, "CGRewardBeam", new Vector3(0f, 1.9f, 6.4f),
      new Vector3(3.4f, 0.14f, 0.14f), WorldBeauty.BlossomPink);
    IgnoreFromBuild(garland);
    Sphere(parent, "CGRewardBlossom0", new Vector3(-0.9f, 2.15f, 6.4f), 0.55f,
      WorldBeauty.BlossomPink, true);
    Sphere(parent, "CGRewardBlossom1", new Vector3(0f, 2.25f, 6.4f), 0.65f,
      WorldBeauty.BlossomCream, true);
    Sphere(parent, "CGRewardBlossom2", new Vector3(0.9f, 2.15f, 6.4f), 0.55f,
      WorldBeauty.BlossomDeep, true);
    // EXIT landmark: mint posts flanking the return disc (read "way home" by
    // colour/shape; the portal trigger itself is untouched).
    Box(parent, "CGExitPostL", new Vector3(-1.6f, 0.9f, -10.4f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Box(parent, "CGExitPostR", new Vector3(1.6f, 0.9f, -10.4f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Sphere(parent, "CGExitCapL", new Vector3(-1.6f, 1.9f, -10.4f), 0.4f, MintLeaf, true);
    Sphere(parent, "CGExitCapR", new Vector3(1.6f, 1.9f, -10.4f), 0.4f, MintLeaf, true);
  }

  // One activity plot (STATIC): basket at the back half, N apples in/around
  // it, number sign on the lateral side off the mouth walk. Plain primitives
  // only — deliberately NOT Interactable/presenter (Phase 3 owns behaviour).
  void BuildActivityPlot(Transform parent, Vector3 center, int appleCount) {
    string tag = (appleCount == 2) ? "1" : "3";
    Vector3 toOut = (center - ArcCenter).normalized;
    Vector3 lat = new Vector3(-toOut.z, 0f, toOut.x);
    if (lat.x > 0f) lat = -lat; // keep signs on the courtyard side, off-walk
    Vector3 basket = center + toOut * 1.6f;
    Cylinder(parent, "CGActivity" + tag + "Basket", basket + new Vector3(0f, 0.25f, 0f),
      0.9f, 0.5f, BasketBrown);
    for (int i = 0; i < appleCount; i++) {
      // Apple 0 rests INSIDE the basket; the rest sit on the ground beside it
      // (static fruit — deliberately no pickup behaviour; Phase 3 owns that).
      Vector3 off = (i == 0) ? new Vector3(0f, 0.55f, 0f)
        : (i == 1) ? new Vector3(0.75f, 0.14f, 0.2f)
        : new Vector3(-0.7f, 0.14f, 0.3f);
      Sphere(parent, "CGActivity" + tag + "Apple" + i, basket + off, 0.28f, AppleRed, false);
    }
    Vector3 sign = center + toOut * 0.4f + lat * 1.7f;
    Box(parent, "CGActivity" + tag + "SignPost", sign + new Vector3(0f, 0.5f, 0f),
      new Vector3(0.12f, 1.0f, 0.12f), BasketBrown);
    Box(parent, "CGActivity" + tag + "SignCube", sign + new Vector3(0f, 1.15f, 0f),
      new Vector3(0.55f, 0.55f, 0.55f), Gold);
  }

  // ---- dressing (S6/S7 beauty kit) ---------------------------------------------

  void BuildDressing(Transform parent) {
    Vector3[] trees = {
      new Vector3(-8f, 0f, 6f), new Vector3(8f, 0f, 6f), new Vector3(-12f, 0f, -2f),
      new Vector3(12f, 0f, -2f), new Vector3(-4f, 0f, -6.5f), new Vector3(4f, 0f, -6.5f),
    };
    for (int i = 0; i < trees.Length; i++) {
      WorldBeauty.BlossomTree(parent, "CGBlossomTree" + i, trees[i], 0.95f + (i % 3) * 0.1f);
      WorldBeauty.PetalCarpet(parent, "CGPetalCarpet" + i, trees[i], 2.7f);
    }
    Vector3[] drifts = {
      new Vector3(-3.4f, 0f, 3.5f), new Vector3(3.4f, 0f, 3.5f),
      new Vector3(-6.5f, 0f, -4f), new Vector3(6.5f, 0f, -4f),
      new Vector3(0f, 0f, -6.5f), new Vector3(-9.5f, 0f, 2f), new Vector3(9.5f, 0f, 2f),
    };
    for (int i = 0; i < drifts.Length; i++)
      WorldBeauty.FlowerDrift(parent, "CGFlowerDrift" + i, drifts[i], 1.3f + (i % 2) * 0.2f);
    WorldBeauty.PetalFall(parent, "CGPetalFall", new Vector3(0f, 0f, 0f), 12f, 18, 60909);
    WorldBeauty.Butterfly(parent, "CGButterfly0", new Vector3(3.4f, 0f, 3.5f), 2.2f, 0.1f,
      WorldBeauty.BlossomDeep, WorldBeauty.BlossomCream);
    WorldBeauty.Butterfly(parent, "CGButterfly1", new Vector3(-3.4f, 0f, 3.5f), 2.2f, 0.6f,
      WorldBeauty.Lilac, WorldBeauty.BlossomPink);
    WorldBeauty.PastelRainbow(parent, "CGRainbow", new Vector3(0f, 0f, 20f), 12f);
  }

  // ---- anchors -----------------------------------------------------------------

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "CountingGardenPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0f, 0f, 2f));
    a.Npc = a.EnsureSlot("NpcAnchor", new Vector3(-2.2f, 0f, 0.5f));
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 8.5f, -14f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 1.2f, 4f));
    a.Prompt = a.EnsureSlot("PromptAnchor", new Vector3(-0.8f, 1.78f, 0.9f));
    a.Feedback = a.EnsureSlot("FeedbackAnchor", new Vector3(-1.4f, 1.2f, 1.4f));
    a.Reward = a.EnsureSlot("RewardAnchor", new Vector3(0f, 0f, 6.4f));
    a.Exit = a.EnsureSlot("ExitAnchor", new Vector3(0f, 0f, -10.4f));
  }

  // ---- helpers -----------------------------------------------------------------

  static void PlaceProp(Transform parent, string prop, string goName, Vector3 pos, float yaw, float scale) {
    GameObject go = PropKit.Place(parent, prop, pos, yaw, scale);
    if (go != null) go.name = goName;
  }

  static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent);
    go.transform.localPosition = pos;
    go.transform.localScale = scale;
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(go);
    return go;
  }

  static void Pad(Transform parent, string name, Vector3 pos, float diameter, Color color) {
    GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pad.name = name;
    pad.transform.SetParent(parent);
    pad.transform.localPosition = pos;
    pad.transform.localScale = new Vector3(diameter, 0.02f, diameter);
    pad.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(pad);
  }

  // Knee-high static solid (basket/plinth): bakes as an obstacle — callers
  // keep it off the walk corridors.
  static GameObject Cylinder(Transform parent, string name, Vector3 pos,
      float diameter, float height, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    go.name = name;
    go.transform.SetParent(parent);
    go.transform.localPosition = pos;
    go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(go);
    return go;
  }

  // Static ball (apple/blossom/cap). Overhead dressing passes ignore=true so
  // it never cuts NavMesh headroom (beam lesson); ground fruit stays baked.
  static GameObject Sphere(Transform parent, string name, Vector3 pos,
      float diameter, Color color, bool ignore) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    go.name = name;
    go.transform.SetParent(parent);
    go.transform.localPosition = pos;
    go.transform.localScale = new Vector3(diameter, diameter, diameter);
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(go);
    if (ignore) IgnoreFromBuild(go);
    return go;
  }

  static void Seg(Transform parent, string name, Vector3 a, Vector3 b, float width) {
    Vector3 flatA = new Vector3(a.x, 0f, a.z);
    Vector3 flatB = new Vector3(b.x, 0f, b.z);
    Vector3 mid = (flatA + flatB) * 0.5f;
    mid.y = 0.032f;
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent);
    go.transform.localPosition = mid;
    go.transform.localRotation = Quaternion.LookRotation((flatB - flatA).normalized);
    go.transform.localScale = new Vector3(width, 0.03f, Vector3.Distance(flatA, flatB));
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
    Unity.AI.Navigation.NavMeshModifier mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
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
