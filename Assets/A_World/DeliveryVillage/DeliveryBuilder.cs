// A_World/DeliveryVillage/DeliveryBuilder.cs — S3-P2Z15 GAMEPLAY #5 vertical
// slice "GIAO HÀNG ĐÚNG SỐ" (deliver the apples). The Delivery Village's OWN
// lazy scene (DeliveryScene), reached from the Math Hub's delivery_village gate
// through the shared micro slot — never at boot, never stacked, no second
// loader. Identity: an order board -> an apple stall -> a lane with image-only
// direction arrows -> a receiving booth where Mia waits with a delivery crate
// (the delivered crate IS the progress count). One village serves every order
// 1..9 (10 apples: the target plus a spare for the gentle overshoot).
// Firewall: presentation + the exit door only — no quest/bus/save here; the
// activity (DeliveryGame) is wired by GameInstaller.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DeliveryBuilder : MonoBehaviour {
  public const string SceneName = "DeliveryScene";

  // Separate island east of the build yard (Main 0, Math +60, garden +120,
  // counting play +180, stair play +240, rabbit play +300, build yard +360).
  public static readonly Vector3 WorldOffset = new Vector3(420f, 0f, 0f);
  public const float BoundX = 18f;
  public const float BoundZ = 18f;
  // Arrival spawn sits clear of the exit portal fire radius (J4 lesson: the
  // portal only arms after the child walks clear of it once).
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -11.5f);
  // Follow camera: north of the child, looking south INTO the arena.
  public static readonly Vector3 FollowOffset = new Vector3(0f, 4.1f, -5.3f);

  public const string ObjectiveEn = "Deliver the Apples";
  public const string ObjectiveVi = "Giao Táo";

  // Orders live in 1..MaxTarget (one stall, one receiver; never nine worlds).
  public const int MaxTarget = 9;
  public const int Target = 4;       // default/fallback target (reference round)
  public const int ItemCount = 10;   // the target plus a spare for the correction

  // Apple geometry (ONE source for placement, flight targets, tests).
  public const float AppleSize = 0.34f;
  public const float AppleRestY = 0.45f;  // on the stall trays

  // ---- acting layout (child scale; entry z=-3, the board faces the child) ----
  public static readonly Vector3 BoardPos = new Vector3(0f, 0f, 7.6f);
  public static readonly Vector3 TeacherStart = new Vector3(-1.4f, 0f, 6.6f);
  public static readonly Vector3 StudentStart = new Vector3(0.7f, 0f, 5.7f);
  public static readonly Vector3 StudentReturn = new Vector3(1.6f, 0f, 5.4f);
  // Apple stall (west) <-> receiving booth (east): a ~5m lane, a 20-60s round.
  public static readonly Vector3 StallCenter = new Vector3(-2.4f, 0f, 3.0f);
  public static readonly Vector3 StallStand = new Vector3(-2.2f, 0f, 1.45f);
  public static readonly Vector3 ReceiverStand = new Vector3(2.1f, 0f, 2.15f);
  public static readonly Vector3 MiaStart = new Vector3(3.0f, 0f, 3.95f);
  public static readonly Vector3 BoothCounter = new Vector3(2.7f, 0f, 3.2f);
  public const float CounterTopY = 0.9f;

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public GameObject NumberBoard { get; private set; } // order digit (pulses)
  public GameObject OrderAppleIcon { get; private set; }
  public GameObject Result { get; private set; }      // digit + tick (hidden)
  public List<GameObject> Apples { get; private set; } = new List<GameObject>();
  public Vector3[] AppleHomes { get; private set; }
  public Transform DeliveryAnchor { get; private set; } // the receiver's door
  public GameObject Crate { get; private set; }         // delivered apples live here
  public GameObject ExitCue { get; private set; }       // shown after completion
  public Transform CamTeaching { get; private set; }
  public Transform LookTeaching { get; private set; }
  public Transform CamDemo { get; private set; }
  public Transform LookDemo { get; private set; }
  public Transform CamSuccess { get; private set; }
  public Transform LookSuccess { get; private set; }

  // The round's order comes from the area's ladder (progression/CLI); the
  // board stages that digit so the world always shows the order.
  public int BoardTarget = Target;

  public static int ClampTarget(int t) {
    if (t < 1) return 1;
    if (t > MaxTarget) return MaxTarget;
    return t;
  }

  // Delivered-apple slot i (0-based) in the receiver crate: a 3x3 grid on the
  // counter. The crate IS the visible count — never a UI tracker.
  public static Vector3 CrateSlot(int i, Vector3 counter) {
    if (i < 0) i = 0;
    if (i > MaxTarget - 1) i = MaxTarget - 1;
    int col = i % 3, row = i / 3;
    return new Vector3(counter.x + (col - 1) * 0.27f,
      CounterTopY + 0.16f, counter.z + (row - 1) * 0.27f);
  }

  static readonly Color Lawn = new Color(0.38f, 0.64f, 0.36f);
  static readonly Color Meadow = new Color(0.46f, 0.71f, 0.42f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);
  static readonly Color CourtyardSand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color StoneGrey = new Color(0.68f, 0.68f, 0.66f);
  static readonly Color BoardCream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color WoodTan = new Color(0.78f, 0.66f, 0.46f);
  static readonly Color FenceWood = new Color(0.55f, 0.40f, 0.24f);
  static readonly Color Coral = new Color(0.90f, 0.52f, 0.38f);
  static readonly Color MintLeaf = new Color(0.70f, 0.90f, 0.72f);
  static readonly Color AppleRed = new Color(0.85f, 0.25f, 0.25f);
  static readonly Color StemBrown = new Color(0.42f, 0.30f, 0.18f);
  static readonly Color LeafGreen = new Color(0.30f, 0.65f, 0.28f);
  static readonly Color BoothBlue = new Color(0.42f, 0.60f, 0.80f);

  // Scene entry (GameInstaller calls this after the lazy load).
  public void Build() {
    BuildContent(transform);
    BuildNavMesh(transform);
  }

  // Runtime NavMesh bake for THIS scene only (CollectObjects.Children on the
  // arena root — J8 lesson: a scene-wide bake would collect Main/Math meshes).
  void BuildNavMesh(Transform parent) {
    Unity.AI.Navigation.NavMeshSurface surface =
      parent.gameObject.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
    if (surface == null) surface = parent.gameObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
    surface.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
    surface.BuildNavMesh();
  }

  public void BuildContent(Transform root) {
    BuildGround(root);
    BuildEntryAndExit(root);
    BuildPaths(root);
    BuildDressing(root);
    BuildOrderBoard(root);
    BuildStall(root);
    BuildRoute(root);
    BuildReceiverBooth(root);
    BuildCountAndResult(root);
    BuildCameras(root);
    BuildAnchors(root);
    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  void BuildGround(Transform parent) {
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "DVRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(40f, 1.4f, 40f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    StripCollider(rim);
    IgnoreFromBuild(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "DVGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localPosition = Vector3.zero;
    ground.transform.localScale = new Vector3(3.8f, 1f, 3.8f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    MeadowPatch(parent, "DVMeadowW", new Vector3(-9f, 0f, 4f), 11f, 9f);
    MeadowPatch(parent, "DVMeadowE", new Vector3(9f, 0f, 4f), 11f, 9f);
    float[] angles = { 15f, 45f, 75f, 105f, 135f, 160f, 200f, 225f, 255f, 285f, 315f, 345f };
    for (int i = 0; i < angles.Length; i++) {
      float rad = angles[i] * Mathf.Deg2Rad;
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "DVHedge" + i;
      bush.transform.SetParent(parent, false);
      bush.transform.localPosition = new Vector3(Mathf.Sin(rad) * 15.5f, 0.55f, 3f + Mathf.Cos(rad) * 15.5f);
      bush.transform.localScale = (i % 2 == 0) ? new Vector3(3.4f, 2.2f, 3.4f) : new Vector3(2.8f, 1.9f, 2.8f);
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

  void BuildEntryAndExit(Transform parent) {
    Box(parent, "DVEntryPostL", new Vector3(-1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    Box(parent, "DVEntryPostR", new Vector3(1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    GameObject beam = Box(parent, "DVEntryBeam", new Vector3(0f, 2.06f, -9.5f),
      new Vector3(3.4f, 0.16f, 0.16f), WorldBeauty.BlossomPink);
    IgnoreFromBuild(beam);
    WorldBeauty.Ball(parent, "DVEntryBlossom0", new Vector3(0f, 2.42f, -9.5f), 1.1f,
      WorldBeauty.BlossomPink);
    Pad(parent, "DVThresholdL", new Vector3(-1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "DVThresholdR", new Vector3(1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "DVExitDisc", new Vector3(0f, 0.01f, -11.1f), 3.2f, WorldBeauty.Petal);
    GameObject exitGo = new GameObject("DVExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.fireRadius = 1.35f;
    exit.areaId = DeliveryArea.AreaId;
    ExitPortal = exit;
    Box(parent, "DVExitPostL", new Vector3(-1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Box(parent, "DVExitPostR", new Vector3(1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Sphere(parent, "DVExitCapL", new Vector3(-1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
    Sphere(parent, "DVExitCapR", new Vector3(1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
    // EXIT CUE (brief §22): a gold beacon over the exit arch, hidden until the
    // order is complete — the world itself says "the way home is open".
    GameObject cue = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cue.name = "DVExitCue";
    cue.transform.SetParent(parent, false);
    cue.transform.localPosition = new Vector3(0f, 2.75f, -11.5f);
    cue.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    cue.transform.localScale = new Vector3(0.5f, 0.5f, 0.14f);
    cue.GetComponent<Renderer>().sharedMaterial = LitEmissive(Gold, 0.55f);
    StripCollider(cue);
    IgnoreFromBuild(cue);
    cue.SetActive(false);
    ExitCue = cue;
  }

  void BuildPaths(Transform parent) {
    Pad(parent, "DVPlazaPad", new Vector3(0f, 0.004f, 0f), 7.4f, CourtyardSand);
    Seg(parent, "DVPathEntry", new Vector3(0f, 0f, -10.4f), new Vector3(0f, 0f, -1.0f), 1.7f);
    Seg(parent, "DVPathStage", new Vector3(0f, 0f, -1.0f), new Vector3(0f, 0f, 8.2f), 1.7f);
    // THE delivery lane: stall front (west) -> receiving booth (east), so the
    // route reads as one obvious line for the child.
    Seg(parent, "DVPathLane", new Vector3(-1.9f, 0f, 1.8f), new Vector3(1.9f, 0f, 1.8f), 1.5f);
    Seg(parent, "DVPathStall", new Vector3(-2.6f, 0f, 2.0f), new Vector3(-2.2f, 0f, 1.5f), 1.3f);
    Seg(parent, "DVPathBooth", new Vector3(2.4f, 0f, 2.0f), new Vector3(2.2f, 0f, 1.6f), 1.3f);
  }

  void BuildDressing(Transform parent) {
    // Trees stay OFF every walk line (entry/order/stall/lane/booth corridors).
    Vector3[] trees = {
      new Vector3(-6.5f, 0f, -3.5f), new Vector3(6.5f, 0f, -3.5f),
      new Vector3(-10f, 0f, 8f), new Vector3(10f, 0f, 8f),
      new Vector3(-5.5f, 0f, 15.5f), new Vector3(5.5f, 0f, 15.5f),
    };
    float[] scales = { 0.62f, 0.62f, 0.7f, 0.7f, 0.7f, 0.7f };
    for (int i = 0; i < trees.Length; i++) {
      WorldBeauty.BlossomTree(parent, "DVBlossomTree" + i, trees[i], scales[i]);
      WorldBeauty.PetalCarpet(parent, "DVPetalCarpet" + i, trees[i], 1.5f * scales[i] + 0.5f);
    }
    Vector3[] drifts = {
      new Vector3(-4.8f, 0f, 5.6f), new Vector3(4.8f, 0f, 6.0f),
      new Vector3(-4.6f, 0f, -6f), new Vector3(4.6f, 0f, -6f),
    };
    for (int i = 0; i < drifts.Length; i++)
      WorldBeauty.FlowerDrift(parent, "DVFlowerDrift" + i, drifts[i], 1.2f);
    WorldBeauty.PetalFall(parent, "DVPetalFall", new Vector3(0f, 0f, -1f), 9f, 12, 51109);
    WorldBeauty.Butterfly(parent, "DVButterfly0", new Vector3(-3.4f, 0f, 5.6f), 2.2f, 0.1f,
      WorldBeauty.BlossomDeep, WorldBeauty.BlossomCream);
    WorldBeauty.Butterfly(parent, "DVButterfly1", new Vector3(3.4f, 0f, 6.0f), 2.2f, 0.6f,
      WorldBeauty.Lilac, WorldBeauty.BlossomPink);
  }

  // The order board: the digit + an apple icon (the brief's "apple x N" read).
  void BuildOrderBoard(Transform parent) {
    Box(parent, "DVBoardL", BoardPos + new Vector3(-1.25f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), FenceWood);
    Box(parent, "DVBoardR", BoardPos + new Vector3(1.25f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), FenceWood);
    GameObject panel = Box(parent, "DVBoardPanel", BoardPos + new Vector3(0f, 1.95f, 0f),
      new Vector3(2.6f, 1.7f, 0.12f), BoardCream);
    SetMaterial(panel, LitEmissive(BoardCream, 0.22f));
    int n = ClampTarget(BoardTarget);
    NumberBoard = CountingGardenBuilder.Digit(parent, "DVNumberDigit",
      BoardPos + new Vector3(-0.42f, 1.42f, -0.09f), 1.3f, 1.0f, Gold, 90f, n);
    SetMaterial(NumberBoard, LitEmissive(Gold, 0.5f));
    IgnoreFromBuild(NumberBoard);
    // The order's item icon on the same panel: the child reads "N apples".
    OrderAppleIcon = MakeApple(parent, "DVBoardApple",
      BoardPos + new Vector3(0.78f, 1.35f, -0.09f), 1.15f);
  }

  // The apple stall (pickup area, west): counter + striped awning + two trays
  // holding the 10 apples (find-and-choose, never a test row).
  void BuildStall(Transform parent) {
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ground.name = "DVStallGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localPosition = new Vector3(StallCenter.x, 0.006f, StallCenter.z);
    ground.transform.localScale = new Vector3(4.2f, 0.006f, 3.2f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(CourtyardSand);
    StripCollider(ground);
    IgnoreFromBuild(ground);
    // Counter (the stall's back) + two awning posts + a two-tone awning.
    Box(parent, "DVStallCounter", StallCenter + new Vector3(0f, 0.5f, 0.95f),
      new Vector3(2.6f, 1.0f, 0.4f), WoodTan);
    Box(parent, "DVStallPostL", StallCenter + new Vector3(-1.2f, 1.0f, 0.75f),
      new Vector3(0.12f, 2.0f, 0.12f), FenceWood);
    Box(parent, "DVStallPostR", StallCenter + new Vector3(1.2f, 1.0f, 0.75f),
      new Vector3(0.12f, 2.0f, 0.12f), FenceWood);
    GameObject awnA = Box(parent, "DVStallAwningA", StallCenter + new Vector3(-0.6f, 2.06f, 0.35f),
      new Vector3(1.35f, 0.1f, 1.0f), Coral);
    awnA.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
    IgnoreFromBuild(awnA);
    GameObject awnB = Box(parent, "DVStallAwningB", StallCenter + new Vector3(0.6f, 2.06f, 0.35f),
      new Vector3(1.35f, 0.1f, 1.0f), BoardCream);
    awnB.transform.localRotation = Quaternion.Euler(9f, 0f, 0f);
    IgnoreFromBuild(awnB);
    // Two low trays with the 10 apples (5 + 5).
    Tray(parent, "DVStallTrayA", StallCenter + new Vector3(-0.62f, 0f, -0.15f));
    Tray(parent, "DVStallTrayB", StallCenter + new Vector3(0.62f, 0f, -0.15f));
    Vector3[] offs = {
      new Vector3(-0.98f, 0f, -0.15f), new Vector3(-0.74f, 0f, -0.15f),
      new Vector3(-0.50f, 0f, -0.15f), new Vector3(-0.26f, 0f, -0.15f),
      new Vector3(-0.86f, 0f, -0.42f), new Vector3(-0.38f, 0f, -0.42f),
      new Vector3(0.26f, 0f, -0.15f), new Vector3(0.50f, 0f, -0.15f),
      new Vector3(0.74f, 0f, -0.15f), new Vector3(0.98f, 0f, -0.15f),
    };
    Apples.Clear();
    AppleHomes = new Vector3[ItemCount];
    for (int i = 0; i < ItemCount; i++) {
      Vector3 home = new Vector3(StallCenter.x + offs[i].x, AppleRestY, StallCenter.z + offs[i].z);
      AppleHomes[i] = home;
      GameObject apple = MakeApple(parent, "DVApple" + i, home, 1f);
      Apples.Add(apple);
    }
    // A little painted sign: a crate with an apple on the counter's front.
    Box(parent, "DVStallSignCrate", StallCenter + new Vector3(0f, 1.16f, -0.6f),
      new Vector3(0.5f, 0.34f, 0.2f), FenceWood);
    GameObject signApple = MakeApple(parent, "DVStallSignApple",
      StallCenter + new Vector3(0f, 1.42f, -0.6f), 0.7f);
    if (signApple != null) signApple.transform.localPosition += new Vector3(0f, 0.1f, 0f);
  }

  void Tray(Transform parent, string name, Vector3 pos) {
    Box(parent, name, pos + new Vector3(0f, 0.26f, 0f), new Vector3(1.0f, 0.08f, 0.7f), FenceWood);
    Box(parent, name + "LegL", pos + new Vector3(-0.4f, 0.11f, 0f),
      new Vector3(0.1f, 0.22f, 0.55f), SoilLeg());
    Box(parent, name + "LegR", pos + new Vector3(0.4f, 0.11f, 0f),
      new Vector3(0.1f, 0.22f, 0.55f), SoilLeg());
  }

  static Color SoilLeg() { return new Color(0.45f, 0.32f, 0.20f); }

  // The delivery lane (brief §12): image-only direction arrows, one obvious
  // route from the stall to the receiving booth. No maze, no text.
  void BuildRoute(Transform parent) {
    Seg(parent, "DVRouteMarkA", new Vector3(-0.9f, 0f, 2.35f), new Vector3(-0.5f, 0f, 2.35f), 1.0f);
    Seg(parent, "DVRouteMarkB", new Vector3(0.3f, 0f, 2.35f), new Vector3(0.7f, 0f, 2.35f), 1.0f);
    Seg(parent, "DVRouteMarkC", new Vector3(1.2f, 0f, 2.35f), new Vector3(1.6f, 0f, 2.35f), 1.0f);
    Arrow(parent, "DVArrowA", new Vector3(-0.7f, 0f, 2.95f));
    Arrow(parent, "DVArrowB", new Vector3(0.5f, 0f, 2.95f));
    Arrow(parent, "DVArrowC", new Vector3(1.5f, 0f, 2.95f));
    // Parcel dressing along the lane (off the walk line).
    Box(parent, "DVParcelA", new Vector3(0.2f, 0.22f, 4.6f),
      new Vector3(0.44f, 0.44f, 0.44f), WoodTan);
    Box(parent, "DVParcelB", new Vector3(0.62f, 0.18f, 4.75f),
      new Vector3(0.36f, 0.36f, 0.36f), Coral);
    Box(parent, "DVParcelC", new Vector3(-4.9f, 0.25f, 1.2f),
      new Vector3(0.5f, 0.5f, 0.5f), WoodTan);
    Box(parent, "DVParcelD", new Vector3(-4.9f, 0.62f, 1.2f),
      new Vector3(0.36f, 0.28f, 0.36f), FenceWood);
  }

  void Arrow(Transform parent, string name, Vector3 pos) {
    GameObject root = new GameObject(name);
    root.transform.SetParent(parent, false);
    root.transform.localPosition = pos;
    Box(root.transform, name + "Post", new Vector3(0f, 0.42f, 0f),
      new Vector3(0.1f, 0.84f, 0.1f), FenceWood);
    Box(root.transform, name + "Panel", new Vector3(-0.05f, 0.95f, 0f),
      new Vector3(0.62f, 0.34f, 0.08f), Gold);
    // A ">" chevron of two slim boards pointing east (toward the receiver) —
    // image-only direction language, no text.
    GameObject up = Box(root.transform, name + "ChevA", new Vector3(0.3f, 1.02f, 0f),
      new Vector3(0.3f, 0.09f, 0.08f), BoardCream);
    up.transform.localRotation = Quaternion.Euler(0f, -32f, 0f);
    GameObject down = Box(root.transform, name + "ChevB", new Vector3(0.3f, 0.88f, 0f),
      new Vector3(0.3f, 0.09f, 0.08f), BoardCream);
    down.transform.localRotation = Quaternion.Euler(0f, 32f, 0f);
  }

  // The receiving booth (east): counter + awning + Mia + the delivered crate.
  void BuildReceiverBooth(Transform parent) {
    Pad(parent, "DVBoothGround", new Vector3(BoothCounter.x, 0.006f, BoothCounter.z), 4.0f, CourtyardSand);
    Box(parent, "DVBoothCounter", BoothCounter + new Vector3(0f, 0.45f, 0f),
      new Vector3(2.4f, 0.9f, 0.5f), BoothBlue);
    Box(parent, "DVBoothPostL", BoothCounter + new Vector3(-1.25f, 1.15f, -0.2f),
      new Vector3(0.12f, 2.3f, 0.12f), FenceWood);
    Box(parent, "DVBoothPostR", BoothCounter + new Vector3(1.25f, 1.15f, -0.2f),
      new Vector3(0.12f, 2.3f, 0.12f), FenceWood);
    GameObject awnA = Box(parent, "DVBoothAwningA", BoothCounter + new Vector3(-0.62f, 2.3f, -0.5f),
      new Vector3(1.4f, 0.1f, 1.1f), Coral);
    awnA.transform.localRotation = Quaternion.Euler(-9f, 0f, 0f);
    IgnoreFromBuild(awnA);
    GameObject awnB = Box(parent, "DVBoothAwningB", BoothCounter + new Vector3(0.62f, 2.3f, -0.5f),
      new Vector3(1.4f, 0.1f, 1.1f), BoardCream);
    awnB.transform.localRotation = Quaternion.Euler(-9f, 0f, 0f);
    IgnoreFromBuild(awnB);
    // The delivery crate ON the counter: 3x3 slots, the visible count.
    Box(parent, "DVCrateBase", new Vector3(BoothCounter.x, CounterTopY + 0.05f, BoothCounter.z),
      new Vector3(1.1f, 0.1f, 1.1f), WoodTan);
    for (int i = 0; i < 4; i++) {
      float sx = (i % 2 == 0) ? -1f : 1f;
      float sz = (i / 2 == 0) ? -1f : 1f;
      Box(parent, "DVCrateWall" + i,
        new Vector3(BoothCounter.x + sx * 0.52f, CounterTopY + 0.18f, BoothCounter.z + sz * 0.52f),
        new Vector3(0.06f, 0.26f, 1.1f), WoodTan);
    }
    Box(parent, "DVCrateFront", new Vector3(BoothCounter.x, CounterTopY + 0.18f, BoothCounter.z - 0.55f),
      new Vector3(1.1f, 0.26f, 0.06f), FenceWood);
    Box(parent, "DVCrateBack", new Vector3(BoothCounter.x, CounterTopY + 0.18f, BoothCounter.z + 0.55f),
      new Vector3(1.1f, 0.26f, 0.06f), FenceWood);
    Transform crateBase = FindDeep(parent, "DVCrateBase");
    Crate = crateBase != null ? crateBase.gameObject : null;
    // A welcome mat where the child stops to hand the apple over.
    Pad(parent, "DVDeliverMat", new Vector3(ReceiverStand.x, 0.012f, ReceiverStand.z), 1.7f, MintLeaf);
    // The receiver's door (click/proximity) just in front of the counter.
    GameObject anchor = new GameObject("DVDeliveryAnchor");
    anchor.transform.SetParent(parent, false);
    anchor.transform.localPosition = new Vector3(2.45f, 0f, 2.55f);
    DeliveryAnchor = anchor.transform;
    // Booth front sign: an apple + a crate (image-only identity).
    Box(parent, "DVBoothSignBoard", new Vector3(BoothCounter.x, 0.62f, BoothCounter.z - 0.28f),
      new Vector3(0.9f, 0.5f, 0.06f), BoardCream);
    GameObject boothApple = MakeApple(parent, "DVBoothSignApple",
      new Vector3(BoothCounter.x, 0.72f, BoothCounter.z - 0.32f), 0.75f);
    if (boothApple != null) boothApple.transform.localPosition += new Vector3(0f, 0.06f, 0f);
    // Parcel stack beside the booth (receiver identity, off the walk line).
    Box(parent, "DVBoothParcelA", new Vector3(BoothCounter.x + 1.75f, 0.24f, BoothCounter.z + 0.8f),
      new Vector3(0.48f, 0.48f, 0.48f), WoodTan);
    Box(parent, "DVBoothParcelB", new Vector3(BoothCounter.x + 1.75f, 0.62f, BoothCounter.z + 0.8f),
      new Vector3(0.36f, 0.28f, 0.36f), Coral);
  }

  void BuildCountAndResult(Transform parent) {
    int n = ClampTarget(BoardTarget);
    GameObject result = new GameObject("DVResult");
    result.transform.SetParent(parent, false);
    result.transform.localPosition = new Vector3(3.4f, 0f, -0.6f);
    Box(result.transform, "DVResultPost", new Vector3(0f, 0.65f, 0f),
      new Vector3(0.13f, 1.3f, 0.13f), FenceWood);
    GameObject resultFrame = Box(result.transform, "DVResultFrame", new Vector3(0f, 1.6f, 0f),
      new Vector3(1.2f, 1.05f, 0.12f), BoardCream);
    SetMaterial(resultFrame, LitEmissive(BoardCream, 0.18f));
    GameObject resultDigit = CountingGardenBuilder.Digit(result.transform, "DVResultDigit",
      new Vector3(0f, 1.2f, -0.09f), 0.6f, 0.45f, Gold, 90f, n);
    SetMaterial(resultDigit, LitEmissive(Gold, 0.45f));
    CountingGardenBuilder.CheckMark(result.transform, "DVResultCheck",
      new Vector3(0f, 1.82f, -0.09f), 0.3f, MintLeaf);
    result.SetActive(false);
    Result = result;
    DemoJuice.AttachSpotlight(parent, "DVGameSpotlight", new Vector3(0f, 0.018f, 2.6f), 6.2f);
  }

  void BuildCameras(Transform parent) {
    // A: the order (board + teacher + student + field glimpse).
    CamTeaching = CamAnchor(parent, "DVCamA", new Vector3(0.5f, 2.3f, -1.6f));
    LookTeaching = CamAnchor(parent, "DVLookA", new Vector3(0.2f, 1.5f, 6.6f));
    // B: the demo (stall + student + lane + Mia, one wide frame).
    CamDemo = CamAnchor(parent, "DVCamB", new Vector3(0.6f, 3.0f, -3.2f));
    LookDemo = CamAnchor(parent, "DVLookB", new Vector3(0.2f, 0.9f, 3.2f));
    // C: the payoff (player + Mia + crate + board + result).
    CamSuccess = CamAnchor(parent, "DVCamC", new Vector3(2.9f, 2.1f, -0.6f));
    LookSuccess = CamAnchor(parent, "DVLookC", new Vector3(2.6f, 1.1f, 3.3f));
  }

  static Transform CamAnchor(Transform parent, string name, Vector3 pos) {
    GameObject go = new GameObject(name);
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    return go.transform;
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "DeliveryPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0f, 0f, 2.6f));
    a.Npc = a.EnsureSlot("NpcAnchor", TeacherStart);
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0.6f, 3.2f, -5.5f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0.2f, 1.1f, 3.0f));
    a.Prompt = a.EnsureSlot("PromptAnchor", new Vector3(0f, 1.7f, 2.6f));
    a.Feedback = a.EnsureSlot("FeedbackAnchor", new Vector3(2.5f, 1.2f, 3.2f));
    a.Reward = a.EnsureSlot("RewardAnchor", BoothCounter);
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  // ---- helpers (same discipline as the earlier arena builders) ------------------

  // One apple: red body + stem + leaf, collider-free, bake-ignored (it MOVES).
  GameObject MakeApple(Transform parent, string name, Vector3 pos, float scale) {
    GameObject apple = new GameObject(name);
    apple.transform.SetParent(parent, false);
    apple.transform.localPosition = pos;
    apple.transform.localScale = Vector3.one * Mathf.Max(0.2f, scale);
    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    body.name = name + "Body";
    body.transform.SetParent(apple.transform, false);
    body.transform.localPosition = Vector3.zero;
    body.transform.localScale = new Vector3(AppleSize, AppleSize * 0.9f, AppleSize);
    body.GetComponent<Renderer>().sharedMaterial = Lit(AppleRed);
    StripCollider(body);
    GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stem.name = name + "Stem";
    stem.transform.SetParent(apple.transform, false);
    stem.transform.localPosition = new Vector3(0f, AppleSize * 0.52f, 0f);
    stem.transform.localScale = new Vector3(0.045f, AppleSize * 0.32f, 0.045f);
    stem.GetComponent<Renderer>().sharedMaterial = Lit(StemBrown);
    StripCollider(stem);
    IgnoreFromBuild(stem);
    GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    leaf.name = name + "Leaf";
    leaf.transform.SetParent(apple.transform, false);
    leaf.transform.localPosition = new Vector3(0.09f, AppleSize * 0.5f, 0f);
    leaf.transform.localScale = new Vector3(0.14f, 0.035f, 0.08f);
    leaf.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
    leaf.GetComponent<Renderer>().sharedMaterial = Lit(LeafGreen);
    StripCollider(leaf);
    IgnoreFromBuild(leaf);
    IgnoreFromBuild(apple);
    return apple;
  }

  static Transform FindDeep(Transform t, string name) {
    if (t == null) return null;
    if (t.name == name) return t;
    for (int i = 0; i < t.childCount; i++) {
      Transform f = FindDeep(t.GetChild(i), name);
      if (f != null) return f;
    }
    return null;
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

  static GameObject Pad(Transform parent, string name, Vector3 pos, float diameter, Color color) {
    GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pad.name = name;
    pad.transform.SetParent(parent, false);
    pad.transform.localPosition = pos;
    pad.transform.localScale = new Vector3(diameter, 0.01f, diameter);
    pad.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(pad);
    return pad;
  }

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
      Unity.AI.Navigation.NavMeshModifier mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (System.Exception) { }
  }

  static Material LitEmissive(Color color, float emission) {
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    if (mat.HasProperty("_EmissionColor")) {
      mat.EnableKeyword("_EMISSION");
      mat.SetColor("_EmissionColor", color * emission);
    }
    mat.enableInstancing = true;
    return mat;
  }

  static void SetMaterial(GameObject go, Material mat) {
    if (go == null || mat == null) return;
    Renderer r = go.GetComponent<Renderer>();
    if (r != null) r.sharedMaterial = mat;
  }

  static readonly System.Collections.Generic.Dictionary<string, Material> _mats =
    new System.Collections.Generic.Dictionary<string, Material>();

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
