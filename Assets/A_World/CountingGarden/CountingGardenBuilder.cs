// A_World/CountingGarden/CountingGardenBuilder.cs — S3 P2V VISUAL RECOMPOSITION.
// The Counting Garden is its OWN additive scene (lazy-loaded from the Math Hub
// counting gate). This builder code-builds ALL content (one root GO, plain
// primitives + shared Lit materials + the existing PropKit, deterministic).
//
// S3-P2V layout (recomposed after the real-camera visual audit — the old
// 5 circular fenced plots OVERLAPPED each other into a fence snake, the zones
// hid behind blossom trees, the demo board faced edge-on and the number sat
// off-frame; see the phase report):
//
//                 EXIT (0,-10.4)   ENTRY (0,-8)      <- warp in/out, north
//                        \            /
//                         entry walk (x=0)
//                              |
//                  PLAZA (0,1.5) d7.6 + number stones 1..5
//                    /          |          \
//          bed0   bed1     DEMO THEATRE     bed3   bed4     <- crescent r9.5
//        (-70°)  (-35°)   (0°) facing N    (+35°)  (+70°)
//                              |
//                    reward pocket (west of plaza)
//
// The demo theatre is a PURPOSE-BUILT stage facing the plaza (the child's
// viewing floor): number board -> apples -> basket -> result board read left
// to right from the stage camera. Beds are future activity gardens (soil +
// counted crops + numbered mouth posts), NOT empty enclosures.
//
// Everything is STATIC presentation (no Interactable, no presenter, no quest/
// collect/scoring): Phase 3 owns behaviour. Nav discipline unchanged: solids
// bake off-corridor, overhead dressing is bake-ignored, pads stay thin.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CountingGardenBuilder : MonoBehaviour {
  public const string SceneName = "CountingGardenScene";
  public const int ZoneCount = 5;

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
  // Plaza heart (orientation + viewing floor for the demo theatre).
  public static readonly Vector3 ArcCenter = new Vector3(0f, 0f, 1.5f);
  // Follow camera for THIS world. Two fixes over the inherited Math offset:
  // closer/lower (the world read like a map from 0,6,8.3) AND flipped to the
  // NORTH side — the garden opens SOUTH from the entry, so the camera must
  // look INTO the garden (the old +z offset stared at the entry arch with the
  // whole world behind the child; S3-P2V round-2 capture).
  public static readonly Vector3 FollowOffset = new Vector3(0f, 3.8f, -5.0f);
  // Crescent: 5 plots at r9.5, fan -70..+70 deg from south (+z), index 2 = demo.
  public const float CrescentRadius = 9.5f;
  static readonly float[] ZoneAngles = { -70f, -35f, 0f, 35f, 70f };

  // Demo theatre (stage centre = ZoneCenters[2] = (0,11)); the viewing spot is
  // the theatre's own DOOR at the plot edge.
  // S3-P2Z11 (user: "cổng các khu trò chơi cho lùi về các khu trò chơi... hiện
  // đang để giữa sân, sau này thêm trò khác sẽ rất chật"): the door used to sit
  // at (0,2.6) on the plaza rim — 8.4m out. It now hugs the plot border (7.4),
  // so the yard centre stays free for future games and every zone keeps its own
  // threshold. The focus camera is a 3/4 from the east side, so the child at
  // the door never stands between the camera and the mini stage.
  public static readonly Vector3 DemoMouthLocal = new Vector3(0f, 0f, 7.4f);
  public const float DemoViewRadius = 5.0f;
  // Lesson staging (2-NPC mini lesson, user script S3-P2W): depth order from
  // the child's view (camera north of the stage, looking south) =
  //   BOARD (back) -> TEACHER -> STUDENT -> 5-BALL FIELD -> BASKET (front).
  // Teacher stands BESIDE the board's left edge and the student a step to the
  // right-front: side by side in shot A (round-4 capture: centered staging
  // made the two actors stack/occlude each other, and the teacher's head hid
  // the number "2").
  // Authored demo-stage ORIGIN: ZoneCenters[2] = ArcCenter + r9.5 south. The
  // lesson layout constants below are LOCAL to the garden root, which is why
  // the S2 play arena reuses the same authored stage 1:1 (BuildDemoStageInto
  // translates them by the caller's origin) — one lesson, two stages.
  public static readonly Vector3 DemoStageOrigin = new Vector3(0f, 0f, 11f);
  public static readonly Vector3 DemoNpcStart = new Vector3(-1.35f, 0f, 11.6f);
  public static readonly Vector3 DemoStudentStart = new Vector3(0.75f, 0f, 10.1f);
  public static readonly Vector3 DemoBallStand = new Vector3(-0.2f, 0f, 9.6f);
  public static readonly Vector3 DemoBall2Stand = new Vector3(0.5f, 0f, 9.6f);
  public static readonly Vector3 DemoBasketStand = new Vector3(2.1f, 0f, 9.6f);
  static readonly Vector3 DemoBoardPos = new Vector3(0f, 0f, 12.4f);
  public static readonly Vector3 DemoBallFieldPos = new Vector3(0f, 0f, 9.6f);
  static readonly Vector3 DemoBasketPos = new Vector3(2.4f, 0f, 9.0f);
  public static readonly Vector3 DemoResultPos = new Vector3(2.9f, 0f, 9.3f);
  // Shot A (lesson): board + teacher + student + ball field + basket in one
  // frame. Shot B (action): student + 2 balls + basket + result, tighter and
  // lower. The sequence reframes between them (user: camera must switch).
  // Shot A (lesson): the whole stage, board fully in frame (a clipped number
  // board reads as a WRONG number — user report).
  static readonly Vector3 DemoCamPos = new Vector3(0.3f, 2.5f, 6.6f);
  static readonly Vector3 DemoLookPos = new Vector3(0.7f, 1.5f, 11.6f);
  // Shot B (action): closer push-in on the student + balls + basket, raised
  // board and result board still fully inside the frame.
  static readonly Vector3 DemoActionCamPos = new Vector3(0.9f, 2.3f, 4.8f);
  static readonly Vector3 DemoActionLookPos = new Vector3(0.85f, 1.05f, 10.3f);
  // Shot C (result, S3-P2Y): right side of the stage at child height — the
  // "2 + tick" board and the basket fill the frame while the teacher confirms
  // (user round: the lesson needs a real camera change on the payoff shot).
  static readonly Vector3 DemoResultCamPos = new Vector3(2.3f, 1.8f, 6.3f);
  static readonly Vector3 DemoResultLookPos = new Vector3(1.9f, 1.15f, 9.7f);
  // S3-P2Y miniature: before the child chooses, the zone-2 lesson runs as a
  // small diorama INSIDE its plot (user: "thu bé khu chơi lại trước khi player
  // chọn"); picking "Vào chơi" opens the full-size arena as before. Scale is
  // applied to a pivot-compensated root, so every authored local coordinate
  // still lands on its designed world spot (the demo controller sells the
  // motion in local space and does not care).
  public const float DemoMiniScale = 0.62f;
  // The five balls of the field (the student must take exactly TWO of them).
  public static readonly Vector3[] DemoBallHomes = {
    new Vector3(-1.6f, 0.17f, 9.6f), new Vector3(-0.9f, 0.17f, 9.6f),
    new Vector3(-0.2f, 0.17f, 9.6f), new Vector3(0.5f, 0.17f, 9.6f),
    new Vector3(1.2f, 0.17f, 9.6f),
  };

  static readonly Color Lawn = new Color(0.38f, 0.64f, 0.36f);
  static readonly Color Meadow = new Color(0.46f, 0.71f, 0.42f);
  static readonly Color BorderWood = new Color(0.52f, 0.36f, 0.22f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);
  static readonly Color CourtyardSand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color Soil = new Color(0.42f, 0.30f, 0.20f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color AppleRed = new Color(0.85f, 0.25f, 0.25f);
  static readonly Color BasketBrown = new Color(0.55f, 0.38f, 0.22f);
  static readonly Color BasketRim = new Color(0.72f, 0.54f, 0.32f);
  static readonly Color MintLeaf = new Color(0.70f, 0.90f, 0.72f);
  static readonly Color StoneGrey = new Color(0.68f, 0.68f, 0.66f);
  static readonly Color BoardCream = new Color(0.99f, 0.95f, 0.85f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public readonly List<Vector3> ZoneCenters = new List<Vector3>();
  // S3 P2X zone picker: one click/proximity spot per crescent plot (index 2 =
  // the demo theatre carries the play door). Pushed into CountingGardenArea on
  // each lazy load (SetGarden) so focus/panel always points at live scene nodes.
  public readonly List<GardenZoneSpot> ZoneSpots = new List<GardenZoneSpot>();
  public MicroWorldPortal ExitPortal { get; private set; }
  // Demo stage refs (scene-authored; CountingDemo reads these).
  public GameObject DemoNumber { get; private set; }
  public Transform DemoBasket { get; private set; }
  public readonly List<GameObject> DemoBalls = new List<GameObject>();
  public GameObject DemoResult { get; private set; }
  public Transform DemoCam { get; private set; }
  public Transform DemoLook { get; private set; }
  public Transform DemoActionCam { get; private set; }
  public Transform DemoActionLook { get; private set; }
  public Transform DemoResultCam { get; private set; }
  public Transform DemoResultLook { get; private set; }
  // Miniature root (S3-P2Y): the demo stage + actors live under this scaled
  // node in the garden; the play arena builds the same layout at full scale.
  public Transform DemoMiniRoot { get; private set; }
  public Vector3 DemoStageCenter { get; private set; }
  public Vector3 DemoMouth { get; private set; }

  // Stage references handed to CountingDemo (and to any scene that reuses the
  // authored lesson layout via BuildDemoStageInto).
  public sealed class DemoRefs {
    public GameObject Number;
    public Transform Basket;
    public readonly List<GameObject> Balls = new List<GameObject>();
    public GameObject Result;
    public Transform CamA;
    public Transform LookA;
    public Transform CamB;
    public Transform LookB;
    public Transform CamC;
    public Transform LookC;
    public Vector3 Mouth;
    public Vector3 Center;
    // Parent the actors/FX must use (miniature in the garden, root in the arena).
    public Transform StageParent;
    // S3-P2Z4: the ACTING layout is data now (user: the reference gameplay must
    // not be nailed to the garden's straight test row). Builders fill these
    // points; CountingDemo reads them instead of the old constants.
    public Vector3 NpcStart = new Vector3(-1.35f, 0f, 11.6f);
    public Vector3 StudentStart = new Vector3(0.75f, 0f, 10.1f);
    public Vector3 BallStand = new Vector3(-0.2f, 0f, 9.6f);
    public Vector3 BallStand2 = new Vector3(0.5f, 0f, 9.6f);
    public Vector3 BasketStand = new Vector3(2.1f, 0f, 9.6f);
    public Vector3 BoardPoint = new Vector3(0f, 1.4f, 12.25f);
    public Vector3 BallFieldPoint = new Vector3(0f, 0.4f, 9.6f);
    public Vector3[] BallHomes = {
      new Vector3(-1.6f, 0.17f, 9.6f), new Vector3(-0.9f, 0.17f, 9.6f),
      new Vector3(-0.2f, 0.17f, 9.6f), new Vector3(0.5f, 0.17f, 9.6f),
      new Vector3(1.2f, 0.17f, 9.6f),
    };
  }

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

  // ---- the crescent: 4 garden beds + the demo theatre (index 2) -----------------

  void BuildCrescent(Transform parent) {
    ZoneCenters.Clear();
    for (int z = 0; z < ZoneCount; z++) {
      float a = ZoneAngles[z] * Mathf.Deg2Rad;
      Vector3 center = ArcCenter + new Vector3(Mathf.Sin(a) * CrescentRadius, 0f,
        Mathf.Cos(a) * CrescentRadius);
      ZoneCenters.Add(center);
    }
    DemoStageCenter = ZoneCenters[2];
    DemoMouth = DemoMouthLocal;
    BuildBed(parent, 0, 3, "carrot");
    BuildBed(parent, 1, 4, "strawberry");
    BuildDemoStage(parent);
    BuildDemoPlotBorder(parent);
    BuildDemoPlotAnchor(parent);
    BuildDemoDoorGate(parent);
    BuildBed(parent, 3, 5, "corn");
    BuildBed(parent, 4, 2, "pumpkin");
    BuildZoneSpots(parent);
  }

  // S3-P2Z6 (user: "demo ở sân không có cổng, đến giữa sân là nó tự chọn"):
  // the stage's DOOR now exists as a real threshold arch at the viewing spot.
  // The lesson's audience gate (2.2m) arms exactly here, so the demo starts
  // when the child reaches the door — never from the middle of the yard.
  // Overhead beam is bake-ignored + collider-free (headroom rule).
  void BuildDemoDoorGate(Transform parent) {
    Vector3 m = DemoMouthLocal;
    Box(parent, "CGDemoDoorPostL", new Vector3(m.x - 1.7f, 1.05f, m.z),
      new Vector3(0.18f, 2.1f, 0.18f), BasketBrown);
    Box(parent, "CGDemoDoorPostR", new Vector3(m.x + 1.7f, 1.05f, m.z),
      new Vector3(0.18f, 2.1f, 0.18f), BasketBrown);
    GameObject beam = Box(parent, "CGDemoDoorBeam", new Vector3(m.x, 2.16f, m.z),
      new Vector3(3.8f, 0.16f, 0.16f), WorldBeauty.BlossomPink);
    IgnoreFromBuild(beam);
    Sphere(parent, "CGDemoDoorCapL", new Vector3(m.x - 1.7f, 2.3f, m.z), 0.42f, Gold, true);
    Sphere(parent, "CGDemoDoorCapR", new Vector3(m.x + 1.7f, 2.3f, m.z), 0.42f, Gold, true);
    WorldBeauty.Ball(parent, "CGDemoDoorBlossom0", new Vector3(m.x, 2.42f, m.z), 0.7f,
      WorldBeauty.BlossomPink);
    WorldBeauty.Ball(parent, "CGDemoDoorBlossomL", new Vector3(m.x - 0.6f, 2.32f, m.z), 0.5f,
      WorldBeauty.BlossomCream);
    WorldBeauty.Ball(parent, "CGDemoDoorBlossomR", new Vector3(m.x + 0.6f, 2.34f, m.z), 0.55f,
      WorldBeauty.BlossomDeep);
  }

  // S3-P2Y boundary for the demo plot (same language as the beds): a flat
  // contrast ring under the stage (the crescent walk covers it where they
  // cross, so the path stays clean) + back/side fence pieces placed OUTSIDE
  // the walk band (r>8.8 from the arc centre) — the north side stays open as
  // the theatre mouth facing the plaza.
  void BuildDemoPlotBorder(Transform parent) {
    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ring.name = "CGZone2Border";
    ring.transform.SetParent(parent, false);
    ring.transform.localPosition = new Vector3(DemoStageCenter.x, 0.004f, DemoStageCenter.z);
    ring.transform.localScale = new Vector3(7.2f, 0.006f, 7.2f);
    ring.GetComponent<Renderer>().sharedMaterial = Lit(BorderWood);
    StripCollider(ring);
    Vector3 c = DemoStageCenter;
    for (int i = 0; i < 3; i++) {
      PlaceProp(parent, "fence_simpleLow", "CGZone2Fence" + (2 + i),
        new Vector3(c.x + (i - 1) * 1.4f, 0f, c.z + 3.0f), -90f, 1.0f);
    }
    for (int s = 0; s < 2; s++) {
      float sign = (s == 0) ? -1f : 1f;
      for (int i = 0; i < 2; i++) {
        PlaceProp(parent, "fence_simpleLow", "CGZone2Fence" + (5 + s * 2 + i),
          new Vector3(c.x + sign * 2.7f, 0f, c.z + 1.2f + i * 1.0f), 0f, 1.0f);
      }
    }
  }

  // S3 P2X (user order §47B): every plot gets a GardenZoneSpot — a thin click
  // pad at the mouth (collider KEPT for ClickRouter, bake-ignored so the pad
  // never textures the NavMesh) plus its own camera/look pair for the focus
  // beat. Zone 2 (the demo theatre) is the only plot with staged play today;
  // the 4 skeleton beds stay look-only. All positions derive from the same
  // crescent math as the beds (no magic numbers duplicated).
  void BuildZoneSpots(Transform parent) {
    ZoneSpots.Clear();
    for (int z = 0; z < ZoneCount; z++) {
      Vector3 center = ZoneCenters[z];
      Vector3 outDir = (center - ArcCenter).normalized;
      Vector3 mouth = (z == 2) ? DemoMouthLocal : center - outDir * 1.35f;
      GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      pad.name = "CGZone" + z + "Spot";
      pad.transform.SetParent(parent, false);
      pad.transform.localPosition = new Vector3(mouth.x, 0.04f, mouth.z);
      pad.transform.localScale = new Vector3(2.0f, 0.03f, 2.0f);
      pad.GetComponent<Renderer>().sharedMaterial = Lit(z == 2 ? Gold : WorldBeauty.Petal);
      pad.AddComponent<GardenZoneSpot>(); // collider stays: this is the click door
      IgnoreFromBuild(pad);
      GardenZoneSpot spot = pad.GetComponent<GardenZoneSpot>();
      spot.zoneIndex = z;
      spot.playEnabled = (z == 2);
      // Focus framing: 3.6m back toward the plaza at child-comfort height,
      // looking at the plot's centre. Zone 2 is the MINIATURE stage (S3-P2Y),
      // so its focus camera sits much closer — the diorama fills the frame.
      Vector3 camPos, lookPos;
      if (z == 2) {
        // S3-P2Z11b (user ảnh: "camera đang chiếu vào cái cột"): the focus
        // camera used to sit BEHIND the theatre door, so a door post filled
        // the left edge. It now sits INSIDE the plot (past the door), almost
        // on the centre line: faces of the actors, board centred, basket at
        // the right edge — no post, no avatar between camera and stage.
        camPos = new Vector3(1.4f, 1.85f, 7.9f);
        lookPos = new Vector3(0.15f, 0.8f, 11.0f);
      } else {
        camPos = mouth - outDir * 3.6f + new Vector3(0f, 2.4f, 0f);
        lookPos = center + new Vector3(0f, 0.9f, 0f);
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
  // top (bake-ignored — the headroom rule) and blossom balls on the beam. The
  // doorway stays 2.2m open so the crescent walk passes straight through.
  List<Transform> BuildBedGate(Transform parent, int index, string tag, Vector3 mouth,
      Vector3 lat, Color accent) {
    List<Transform> beads = new List<Transform>();
    Vector3 postL = mouth + lat * 1.1f;
    Vector3 postR = mouth - lat * 1.1f;
    Cylinder(parent, tag + "Post", postL + new Vector3(0f, 0.85f, 0f),
      0.17f, 1.7f, BasketBrown);
    for (int i = 0; i < index + 1; i++) {
      GameObject bead = Sphere(parent, tag + "PostBead" + i,
        postL + new Vector3(0f, 1.82f + i * 0.19f, 0f), 0.17f, Gold, false);
      if (bead != null) beads.Add(bead.transform);
    }
    Cylinder(parent, tag + "GatePostR", postR + new Vector3(0f, 0.7f, 0f),
      0.15f, 1.4f, BasketBrown);
    GameObject beam = Box(parent, tag + "GateBeam", mouth + new Vector3(0f, 1.52f, 0f),
      new Vector3(0.13f, 0.13f, 2.5f), accent);
    if (beam != null) {
      beam.transform.localRotation = Quaternion.LookRotation(new Vector3(lat.x, 0f, lat.z));
      IgnoreFromBuild(beam);
    }
    Sphere(parent, tag + "GateBall0", mouth + new Vector3(0f, 1.78f, 0f), 0.42f, accent, true);
    Sphere(parent, tag + "GateBall1", mouth + lat * 0.55f + new Vector3(0f, 1.68f, 0f),
      0.26f, Gold, true);
    Sphere(parent, tag + "GateBall2", mouth - lat * 0.55f + new Vector3(0f, 1.68f, 0f),
      0.26f, Gold, true);
    return beads;
  }

  static Color AccentFor(int index) {
    switch (index) {
      case 0: return new Color(0.95f, 0.55f, 0.20f); // carrot
      case 1: return new Color(0.90f, 0.30f, 0.42f); // strawberry
      case 3: return new Color(0.98f, 0.80f, 0.28f); // corn
      case 4: return new Color(0.90f, 0.45f, 0.15f); // pumpkin
      default: return Gold;
    }
  }

  // The demo theatre is a plot too: give it the same Zone2 anchor name the
  // crescent contract uses (its floor pad is CG DemoStagePad — see P46D).
  void BuildDemoPlotAnchor(Transform parent) {
    GameObject anchor = new GameObject("CGZone2Anchor");
    anchor.transform.SetParent(parent, false);
    anchor.transform.localPosition = DemoMouthLocal;
  }

  // The demo theatre: a purpose-built stage facing the plaza. Camera-first
  // layout (child at the plaza looking south): number board (screen-left) ->
  // balls -> basket -> result board (screen-right); NPC works the back row.
  // BuildDemoStageInto is SCENE-AGNOSTIC (origin-translated): the garden uses
  // it for its zone-2 stage and the S2 play arena reuses the exact authored
  // lesson 1:1 (one layout, no drifting copies).
  void BuildDemoStage(Transform parent) {
    // Pivot compensation: world = P + s*local must keep the stage centre at
    // DemoStageCenter, so P = C*(1-s). Every authored coordinate (balls, NPC
    // starts, camera markers) stays valid in the mini root's local space.
    float s = DemoMiniScale;
    GameObject mini = new GameObject("CGDemoMiniRoot");
    mini.transform.SetParent(parent, false);
    mini.transform.localPosition = DemoStageCenter * (1f - s);
    mini.transform.localScale = new Vector3(s, s, s);
    DemoMiniRoot = mini.transform;
    DemoRefs r = BuildDemoStageInto(mini.transform, DemoStageOrigin);
    DemoNumber = r.Number;
    DemoBasket = r.Basket;
    DemoBalls.Clear();
    DemoBalls.AddRange(r.Balls);
    DemoResult = r.Result;
    DemoCam = r.CamA;
    DemoLook = r.LookA;
    DemoActionCam = r.CamB;
    DemoActionLook = r.LookB;
    DemoResultCam = r.CamC;
    DemoResultLook = r.LookC;
  }

  public static DemoRefs BuildDemoStageInto(Transform parent, Vector3 origin) {
    DemoRefs r = new DemoRefs();
    r.Center = origin;
    r.StageParent = parent;
    Vector3 off = origin - DemoStageOrigin;
    // Stage floor + low hedge backdrop (frames the stage, never the action).
    GameObject stagePad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stagePad.name = "CGDemoStagePad";
    stagePad.transform.SetParent(parent, false);
    stagePad.transform.localPosition = new Vector3(origin.x, 0.006f, origin.z);
    stagePad.transform.localScale = new Vector3(5.8f, 0.012f, 5.8f);
    stagePad.GetComponent<Renderer>().sharedMaterial = Lit(CourtyardSand);
    StripCollider(stagePad);
    // Stage wings: low fences framing the theatre's flanks (keeps the open
    // side toward the plaza) — also the plot's fence contract (P46D).
    PlaceProp(parent, "fence_simpleLow", "CGZone2Fence0",
      origin + new Vector3(-3.0f, 0f, -0.6f), 90f, 0.92f);
    PlaceProp(parent, "fence_simpleLow", "CGZone2Fence1",
      origin + new Vector3(3.0f, 0f, -0.6f), 90f, 0.92f);
    // Backdrop bushes sit BEHIND the board (a≈0 keeps them at z>stage, out of
    // the basket/result side — user report: the right-side bush covered the
    // result board).
    for (int i = 0; i < 3; i++) {
      float a = (-35f + i * 35f) * Mathf.Deg2Rad;
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "CGDemoBackdrop" + i;
      bush.transform.SetParent(parent, false);
      bush.transform.localPosition = new Vector3(origin.x + Mathf.Sin(a) * 4.1f, 0.5f,
        origin.z + Mathf.Cos(a) * 4.1f);
      bush.transform.localScale = new Vector3(2.4f, 1.7f, 2.4f);
      bush.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.28f, 0.58f, 0.32f));
      StripCollider(bush);
    }
    // NUMBER BOARD (back): cream panel with a big gold "2" + 2 dots, facing
    // the child (north). The teacher stands IN FRONT of it so one frame holds
    // "teacher + number two" while she explains (user camera rule).
    // The board rides HIGH above the teacher's hat (round-1 capture: a chest
    // height board hid its own "2" behind the teacher's head).
    Box(parent, "CGDemoBoardL", DemoBoardPos + off + new Vector3(-1.1f, 0.8f, 0f),
      new Vector3(0.14f, 1.6f, 0.14f), BasketBrown);
    Box(parent, "CGDemoBoardR", DemoBoardPos + off + new Vector3(1.1f, 0.8f, 0f),
      new Vector3(0.14f, 1.6f, 0.14f), BasketBrown);
    Box(parent, "CGDemoBoardPanel", DemoBoardPos + off + new Vector3(0f, 2.3f, 0f),
      new Vector3(2.3f, 1.5f, 0.12f), BoardCream);
    r.Number = Digit2(parent, "CGDemoNumber2", DemoBoardPos + off + new Vector3(0f, 1.78f, -0.08f),
      1.05f, 0.8f, Gold, 90f);
    // BALL FIELD (front): five balls in a row — the student must take TWO.
    Pad(parent, "CGDemoBallField", DemoBallFieldPos + off + new Vector3(0f, -0.006f, 0f), 3.4f,
      new Color(0.93f, 0.90f, 0.78f));
    Color[] ballColors = { AppleRed, new Color(0.30f, 0.55f, 0.95f), Gold,
      new Color(0.35f, 0.75f, 0.40f), WorldBeauty.BlossomDeep };
    for (int i = 0; i < DemoBallHomes.Length; i++) {
      GameObject ball = Sphere(parent, "CGDemoBall" + i, DemoBallHomes[i] + off, 0.34f,
        ballColors[i % ballColors.Length], false);
      if (ball != null) r.Balls.Add(ball);
    }
    // BASKET (front-right): big enough to read from the viewing spot.
    GameObject basketGo = Cylinder(parent, "CGDemoBasket",
      DemoBasketPos + off + new Vector3(0f, 0.27f, 0f), 1.05f, 0.55f, BasketBrown);
    r.Basket = basketGo != null ? basketGo.transform : null;
    Cylinder(parent, "CGDemoBasketRim", DemoBasketPos + off + new Vector3(0f, 0.55f, 0f),
      1.15f, 0.1f, BasketRim);
    // RESULT BOARD (front-right of the basket): appears ONLY after both balls
    // are in, so the last shot reads "2 balls -> basket -> 2 tick".
    GameObject result = new GameObject("CGDemoResult");
    result.transform.SetParent(parent, false);
    result.transform.localPosition = DemoResultPos + off;
    // Raised on its own post: the celebrating student's body can no longer
    // cover the "2 tick" (round-3 capture).
    Box(result.transform, "CGDemoResultPost", new Vector3(0f, 0.65f, 0f),
      new Vector3(0.13f, 1.3f, 0.13f), BasketBrown);
    Box(result.transform, "CGDemoResultFrame", new Vector3(0f, 1.6f, 0f),
      new Vector3(1.1f, 1.0f, 0.12f), BoardCream);
    Digit2(result.transform, "CGDemoResultTwo", new Vector3(0f, 1.2f, -0.08f),
      0.55f, 0.4f, Gold, 90f);
    CheckMark(result.transform, "CGDemoResultCheck", new Vector3(0f, 1.86f, -0.08f),
      0.3f, MintLeaf);
    result.SetActive(false);
    r.Result = result;
    // Camera markers (scene-authored transforms, no second system): shot A =
    // lesson frame, shot B = action frame.
    GameObject cam = new GameObject("CGDemoCam");
    cam.transform.SetParent(parent, false);
    cam.transform.localPosition = DemoCamPos + off;
    r.CamA = cam.transform;
    GameObject look = new GameObject("CGDemoLook");
    look.transform.SetParent(parent, false);
    look.transform.localPosition = DemoLookPos + off;
    r.LookA = look.transform;
    GameObject camB = new GameObject("CGDemoCamAction");
    camB.transform.SetParent(parent, false);
    camB.transform.localPosition = DemoActionCamPos + off;
    r.CamB = camB.transform;
    GameObject lookB = new GameObject("CGDemoLookAction");
    lookB.transform.SetParent(parent, false);
    lookB.transform.localPosition = DemoActionLookPos + off;
    r.LookB = lookB.transform;
    // Shot C (result): the payoff frame for the "2 + tick" board + basket.
    GameObject camC = new GameObject("CGDemoCamResult");
    camC.transform.SetParent(parent, false);
    camC.transform.localPosition = DemoResultCamPos + off;
    r.CamC = camC.transform;
    GameObject lookC = new GameObject("CGDemoLookResult");
    lookC.transform.SetParent(parent, false);
    lookC.transform.localPosition = DemoResultLookPos + off;
    r.LookC = lookC.transform;
    r.Mouth = DemoMouthLocal + off;
    // Acting layout (garden defaults; the arena builder overrides its own).
    r.NpcStart = DemoNpcStart + off;
    r.StudentStart = DemoStudentStart + off;
    r.BallStand = DemoBallStand + off;
    r.BallStand2 = DemoBall2Stand + off;
    r.BasketStand = DemoBasketStand + off;
    r.BoardPoint = new Vector3(0f, 1.4f, 12.25f) + off;
    r.BallFieldPoint = DemoBallFieldPos + off + new Vector3(0f, 0.4f, 0f);
    Vector3[] homes = new Vector3[DemoBallHomes.Length];
    for (int i = 0; i < homes.Length; i++) homes[i] = DemoBallHomes[i] + off;
    r.BallHomes = homes;
    // A soft pulsing pool under the stage ("it's a show", readable from afar).
    DemoJuice.AttachSpotlight(parent, "CGDemoSpotlight",
      new Vector3(origin.x, 0.018f, origin.z), 4.6f);
    return r;
  }

  // CONNECTED seven-segment "2" (A/B/G/E/D) in the ZY plane, thin in X, yaw 90
  // faces the plaza (north). Segments overlap at the joints (length + t) so the
  // glyph is one solid connected number — the user rejected the earlier floating
  // bars ("chưa có số hoàn chỉnh"). Local +z maps to world -x at yaw 90 and the
  // north viewer reads screen-right = local +z, so B (upper vertical) sits at
  // +z and E (lower vertical) at -z, exactly like a real "2".
  // Group origin at the base so emphasis pulses grow upward.
  public static GameObject Digit2(Transform parent, string name, Vector3 origin, float h, float w,
      Color color, float yawDeg) {
    GameObject g = new GameObject(name);
    g.transform.SetParent(parent, false);
    g.transform.localPosition = origin;
    g.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
    float t = Mathf.Max(0.09f, h * 0.19f);          // stroke thickness
    float hLen = w + t;                              // horizontal segments
    float vLen = h * 0.5f + t;                       // vertical segments (overlap)
    // Per-segment X thickness: overlapping joints would otherwise leave
    // coplanar faces that z-fight (journey screenshot: striped top bar).
    Box(g.transform, name + "A", new Vector3(0f, h, 0f), new Vector3(t * 1.00f, t, hLen), color);
    Box(g.transform, name + "B", new Vector3(0f, h * 0.75f, w * 0.5f),
      new Vector3(t * 0.90f, vLen, t), color);
    Box(g.transform, name + "G", new Vector3(0f, h * 0.5f, 0f), new Vector3(t * 1.06f, t, hLen), color);
    Box(g.transform, name + "E", new Vector3(0f, h * 0.25f, -w * 0.5f),
      new Vector3(t * 0.94f, vLen, t), color);
    Box(g.transform, name + "D", new Vector3(0f, 0f, 0f), new Vector3(t * 1.03f, t, hLen), color);
    return g;
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
    // Crescent walk: an arc band joining every bed mouth (8 segments).
    const float walkR = 8.0f;
    for (int i = 0; i < 8; i++) {
      float a0 = -70f + i * 17.5f - 1.2f;
      float a1 = a0 + 17.5f + 2.4f;
      Vector3 p0 = ArcCenter + new Vector3(Mathf.Sin(a0 * Mathf.Deg2Rad) * walkR, 0f,
        Mathf.Cos(a0 * Mathf.Deg2Rad) * walkR);
      Vector3 p1 = ArcCenter + new Vector3(Mathf.Sin(a1 * Mathf.Deg2Rad) * walkR, 0f,
        Mathf.Cos(a1 * Mathf.Deg2Rad) * walkR);
      Seg(parent, "CGPathCrescent" + i, p0, p1, 1.6f);
    }
    // Demo approach spur (plaza -> crescent walk on the centre line).
    Seg(parent, "CGPathDemoSpur", new Vector3(0f, 0f, 4.6f), new Vector3(0f, 0f, 8.4f), 1.7f);
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
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", DemoStageCenter);
    a.Npc = a.EnsureSlot("NpcAnchor", DemoNpcStart);
    // Arrival reveal: over the entry arch, across the plaza to the crescent.
    // (The DEMO card uses its own CG DemoCam/CGDemoLook markers — the two
    // framings must never fight.)
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
