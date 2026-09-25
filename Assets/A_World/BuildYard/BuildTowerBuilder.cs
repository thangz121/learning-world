// A_World/BuildYard/BuildTowerBuilder.cs — S3-P2Z14 GAMEPLAY #4
// "XÂY THÁP THEO SỐ" (build the tower by number). The Build Yard's OWN lazy
// scene (BuildTowerScene), reached from the Math Hub's build_yard gate through
// the shared micro slot — never at boot, never stacked, no second loader.
// The arena is a real place with its own identity: entry arch -> orientation
// (teacher + number board) -> block yard (site material stacks) -> build pad
// (platform + pegs + ghost slot) -> result board -> exit. One yard serves every
// target 1..9 (10 blocks: the target plus a spare for the gentle overshoot).
// Firewall: presentation + the exit door only — no quest/bus/save here; the
// activity (BuildTowerGame) is wired by GameInstaller.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildTowerBuilder : MonoBehaviour {
  public const string SceneName = "BuildTowerScene";

  // Separate island east of the rabbit one (Main 0, Math +60, garden +120,
  // counting play +180, stair play +240, rabbit play +300).
  public static readonly Vector3 WorldOffset = new Vector3(360f, 0f, 0f);
  public const float BoundX = 18f;
  public const float BoundZ = 18f;
  // Arrival spawn sits clear of the exit portal fire radius (J4 lesson: the
  // portal only arms after the child walks clear of it once).
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -11.5f);
  // Follow camera: north of the child, looking south INTO the arena.
  public static readonly Vector3 FollowOffset = new Vector3(0f, 4.1f, -5.3f);

  public const string ObjectiveEn = "Build the Tower";
  public const string ObjectiveVi = "Xây Tháp";

  // Targets live in 1..MaxTarget (brief §17: 1-9 on ONE arena, never nine).
  public const int MaxTarget = 9;
  public const int Target = 3;       // default/fallback target (reference round)
  public const int BlockCount = 10;  // the target plus a spare for the correction

  // Block / tower geometry (ONE source for placement, camera height, tests).
  public static readonly Vector3 BlockSize = new Vector3(0.5f, 0.44f, 0.5f);
  public const float BlockHalfH = 0.22f;
  public const float BlockRise = 0.44f;
  public const float PadTopY = 0.12f;

  // The round's mission comes from the area's ladder (progression/CLI); the
  // board stages that digit so the world always shows the mission.
  public int BoardTarget = Target;

  public static int ClampTarget(int t) {
    if (t < 1) return 1;
    if (t > MaxTarget) return MaxTarget;
    return t;
  }

  // Deterministic stack slot i (0-based) for a placed block: pad centre + the
  // stacked block heights. Placement never depends on click counts (brief §10).
  // Slots run 0..MaxTarget: slot MaxTarget is the overshoot landing on top of
  // a full target tower (the correction lesson), never a second placement.
  public static Vector3 StackSlot(int i, Vector3 padPos) {
    if (i < 0) i = 0;
    if (i > MaxTarget) i = MaxTarget;
    return new Vector3(padPos.x, PadTopY + BlockHalfH + i * BlockRise, padPos.z);
  }

  // Top face of a tower of `placed` blocks (0 = the bare pad top).
  public static float TowerTopY(int placed) {
    if (placed < 0) placed = 0;
    if (placed > MaxTarget) placed = MaxTarget;
    return PadTopY + placed * BlockRise;
  }

  static readonly Color Lawn = new Color(0.38f, 0.64f, 0.36f);
  static readonly Color Meadow = new Color(0.46f, 0.71f, 0.42f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);
  static readonly Color CourtyardSand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color StoneGrey = new Color(0.68f, 0.68f, 0.66f);
  static readonly Color BoardCream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color WoodTan = new Color(0.78f, 0.66f, 0.46f);
  static readonly Color Coral = new Color(0.90f, 0.52f, 0.38f);
  static readonly Color SoilBrown = new Color(0.45f, 0.32f, 0.20f);
  static readonly Color FenceWood = new Color(0.55f, 0.40f, 0.24f);
  static readonly Color MintLeaf = new Color(0.70f, 0.90f, 0.72f);
  static readonly Color Hazard = new Color(0.95f, 0.82f, 0.30f);

  // ---- acting layout (child scale; entry z=-3, the board faces the child) ----
  public static readonly Vector3 BoardPos = new Vector3(0f, 0f, 7.6f);
  public static readonly Vector3 TeacherStart = new Vector3(-1.4f, 0f, 6.6f);
  public static readonly Vector3 StudentStart = new Vector3(0.7f, 0f, 5.7f);
  public static readonly Vector3 StudentReturn = new Vector3(1.6f, 0f, 5.4f);
  // Block yard (west) <-> build pad (east): a ~4.5m loop, a 20-60s round.
  public static readonly Vector3 YardCenter = new Vector3(-2.2f, 0f, 3.0f);
  public static readonly Vector3 YardStand = new Vector3(-1.9f, 0f, 1.4f);
  public static readonly Vector3 PadPos = new Vector3(2.3f, 0f, 3.1f);
  public static readonly Vector3 PadStand = new Vector3(1.5f, 0f, 2.0f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public GameObject NumberBoard { get; private set; } // target digit (pulses)
  public GameObject Result { get; private set; }      // target + tick (hidden)
  public List<GameObject> Blocks { get; private set; } = new List<GameObject>();
  public Vector3[] BlockHomes { get; private set; }
  public Transform PadAnchor { get; private set; } // the click/proximity door
  public GameObject Ghost { get; private set; }    // next-slot placement hint
  public Transform CamTeaching { get; private set; }
  public Transform LookTeaching { get; private set; }
  public Transform CamDemo { get; private set; }
  public Transform LookDemo { get; private set; }
  public Transform CamSuccess { get; private set; }
  public Transform LookSuccess { get; private set; }

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
    BuildBoard(root);
    BuildYard(root);
    BuildPad(root);
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
    rim.name = "BTRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(40f, 1.4f, 40f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    StripCollider(rim);
    IgnoreFromBuild(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "BTGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localPosition = Vector3.zero;
    ground.transform.localScale = new Vector3(3.8f, 1f, 3.8f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    MeadowPatch(parent, "BTMeadowW", new Vector3(-9f, 0f, 4f), 11f, 9f);
    MeadowPatch(parent, "BTMeadowE", new Vector3(9f, 0f, 4f), 11f, 9f);
    float[] angles = { 15f, 45f, 75f, 105f, 135f, 160f, 200f, 225f, 255f, 285f, 315f, 345f };
    for (int i = 0; i < angles.Length; i++) {
      float rad = angles[i] * Mathf.Deg2Rad;
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "BTHedge" + i;
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
    Box(parent, "BTEntryPostL", new Vector3(-1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    Box(parent, "BTEntryPostR", new Vector3(1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    GameObject beam = Box(parent, "BTEntryBeam", new Vector3(0f, 2.06f, -9.5f),
      new Vector3(3.4f, 0.16f, 0.16f), WorldBeauty.BlossomPink);
    IgnoreFromBuild(beam);
    WorldBeauty.Ball(parent, "BTEntryBlossom0", new Vector3(0f, 2.42f, -9.5f), 1.1f,
      WorldBeauty.BlossomPink);
    Pad(parent, "BTThresholdL", new Vector3(-1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "BTThresholdR", new Vector3(1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "BTExitDisc", new Vector3(0f, 0.01f, -11.1f), 3.2f, WorldBeauty.Petal);
    GameObject exitGo = new GameObject("BTExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.fireRadius = 1.35f;
    exit.areaId = BuildTowerArea.AreaId; // the yard area (bound by the installer)
    ExitPortal = exit;
    Box(parent, "BTExitPostL", new Vector3(-1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Box(parent, "BTExitPostR", new Vector3(1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Sphere(parent, "BTExitCapL", new Vector3(-1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
    Sphere(parent, "BTExitCapR", new Vector3(1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
  }

  void BuildPaths(Transform parent) {
    Pad(parent, "BTPlazaPad", new Vector3(0f, 0.004f, 0f), 7.0f, CourtyardSand);
    Seg(parent, "BTPathEntry", new Vector3(0f, 0f, -10.4f), new Vector3(0f, 0f, -1.0f), 1.7f);
    Seg(parent, "BTPathStage", new Vector3(0f, 0f, -1.0f), new Vector3(0f, 0f, 8.2f), 1.7f);
    // Spurs to the block yard (west) and the build pad (east) — the work loop.
    Seg(parent, "BTPathYard", new Vector3(0f, 0f, 1.7f), new Vector3(-1.6f, 0f, 1.7f), 1.4f);
    Seg(parent, "BTPathPad", new Vector3(0f, 0f, 2.0f), new Vector3(1.5f, 0f, 2.0f), 1.4f);
  }

  void BuildDressing(Transform parent) {
    // Trees stay OFF every walk line (entry/stage/yard/pad corridors).
    Vector3[] trees = {
      new Vector3(-6.5f, 0f, -3.5f), new Vector3(6.5f, 0f, -3.5f),
      new Vector3(-10f, 0f, 8f), new Vector3(10f, 0f, 8f),
      new Vector3(-5.5f, 0f, 15.5f), new Vector3(5.5f, 0f, 15.5f),
    };
    float[] scales = { 0.62f, 0.62f, 0.7f, 0.7f, 0.7f, 0.7f };
    for (int i = 0; i < trees.Length; i++) {
      WorldBeauty.BlossomTree(parent, "BTBlossomTree" + i, trees[i], scales[i]);
      WorldBeauty.PetalCarpet(parent, "BTPetalCarpet" + i, trees[i], 1.5f * scales[i] + 0.5f);
    }
    Vector3[] drifts = {
      new Vector3(-4.6f, 0f, 5.2f), new Vector3(4.8f, 0f, 5.4f),
      new Vector3(-4.6f, 0f, -6f), new Vector3(4.6f, 0f, -6f),
    };
    for (int i = 0; i < drifts.Length; i++)
      WorldBeauty.FlowerDrift(parent, "BTFlowerDrift" + i, drifts[i], 1.2f);
    WorldBeauty.PetalFall(parent, "BTPetalFall", new Vector3(0f, 0f, -1f), 9f, 12, 51109);
    WorldBeauty.Butterfly(parent, "BTButterfly0", new Vector3(-3.4f, 0f, 5.2f), 2.2f, 0.1f,
      WorldBeauty.BlossomDeep, WorldBeauty.BlossomCream);
    WorldBeauty.Butterfly(parent, "BTButterfly1", new Vector3(3.4f, 0f, 5.4f), 2.2f, 0.6f,
      WorldBeauty.Lilac, WorldBeauty.BlossomPink);
  }

  void BuildBoard(Transform parent) {
    Box(parent, "BTBoardL", BoardPos + new Vector3(-1.25f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), FenceWood);
    Box(parent, "BTBoardR", BoardPos + new Vector3(1.25f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), FenceWood);
    GameObject panel = Box(parent, "BTBoardPanel", BoardPos + new Vector3(0f, 1.95f, 0f),
      new Vector3(2.6f, 1.7f, 0.12f), BoardCream);
    SetMaterial(panel, LitEmissive(BoardCream, 0.22f));
    int n = ClampTarget(BoardTarget);
    NumberBoard = CountingGardenBuilder.Digit(parent, "BTNumberDigit",
      BoardPos + new Vector3(0f, 1.42f, -0.09f), 1.3f, 1.0f, Gold, 90f, n);
    SetMaterial(NumberBoard, LitEmissive(Gold, 0.5f));
    IgnoreFromBuild(NumberBoard);
  }

  // ---- the block yard (west): site material stacks + construction identity ----
  void BuildYard(Transform parent) {
    Pad(parent, "BTYardGround", new Vector3(YardCenter.x, 0.006f, YardCenter.z), 5.2f, SoilBrown);
    // Material rack behind the pile: two posts + two shelves + spare blocks.
    // Shelves/rack pieces are bake-ignored (low overhead geometry must never
    // carve the yard's walkable surface).
    Box(parent, "BTYardRackPostL", YardCenter + new Vector3(-1.5f, 0.7f, 1.55f),
      new Vector3(0.14f, 1.4f, 0.14f), FenceWood);
    Box(parent, "BTYardRackPostR", YardCenter + new Vector3(1.5f, 0.7f, 1.55f),
      new Vector3(0.14f, 1.4f, 0.14f), FenceWood);
    GameObject rackA = Box(parent, "BTYardRackShelfA", YardCenter + new Vector3(0f, 0.75f, 1.55f),
      new Vector3(3.1f, 0.1f, 0.4f), WoodTan);
    GameObject rackB = Box(parent, "BTYardRackShelfB", YardCenter + new Vector3(0f, 1.2f, 1.55f),
      new Vector3(3.1f, 0.1f, 0.4f), WoodTan);
    IgnoreFromBuild(rackA);
    IgnoreFromBuild(rackB);
    GameObject rackBlockA = Box(parent, "BTYardRackBlockA", YardCenter + new Vector3(-0.9f, 0.98f, 1.55f),
      new Vector3(0.34f, 0.34f, 0.34f), Coral);
    GameObject rackBlockB = Box(parent, "BTYardRackBlockB", YardCenter + new Vector3(0.9f, 0.98f, 1.55f),
      new Vector3(0.34f, 0.34f, 0.34f), Gold);
    IgnoreFromBuild(rackBlockA);
    IgnoreFromBuild(rackBlockB);
    // Hazard-striped corner marker: a construction sign (post + panel + cube).
    Box(parent, "BTYardSignPost", YardCenter + new Vector3(-1.9f, 0.55f, -1.5f),
      new Vector3(0.12f, 1.1f, 0.12f), FenceWood);
    GameObject sign = Box(parent, "BTYardSignPanel", YardCenter + new Vector3(-1.9f, 1.22f, -1.5f),
      new Vector3(0.7f, 0.5f, 0.08f), Hazard);
    sign.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
    IgnoreFromBuild(sign);
    Box(parent, "BTYardSignBlock", YardCenter + new Vector3(-1.9f, 1.22f, -1.56f),
      new Vector3(0.26f, 0.26f, 0.05f), SoilBrown);
    // Ten blocks in a natural site pile (find-and-choose, never a test row).
    Vector3[] offs = {
      new Vector3(-1.24f, 0f, -0.42f), new Vector3(-0.62f, 0f, -0.42f),
      new Vector3(0.00f, 0f, -0.42f), new Vector3(0.62f, 0f, -0.42f),
      new Vector3(1.24f, 0f, -0.42f), new Vector3(-1.14f, 0f, 0.43f),
      new Vector3(-0.52f, 0f, 0.43f), new Vector3(0.10f, 0f, 0.43f),
      new Vector3(0.72f, 0f, 0.43f), new Vector3(1.34f, 0f, 0.43f),
    };
    Color[] palette = { WoodTan, Gold, Coral };
    Blocks.Clear();
    BlockHomes = new Vector3[BlockCount];
    for (int i = 0; i < BlockCount; i++) {
      Vector3 home = new Vector3(YardCenter.x + offs[i].x, BlockHalfH, YardCenter.z + offs[i].z);
      BlockHomes[i] = home;
      GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
      block.name = "BTBlock" + i;
      block.transform.SetParent(parent, false);
      block.transform.localPosition = home;
      block.transform.localRotation = Quaternion.Euler(0f, (i * 37f) % 360f, 0f);
      block.transform.localScale = BlockSize;
      block.GetComponent<Renderer>().sharedMaterial = Lit(palette[i % palette.Length]);
      StripCollider(block);
      // Bake-ignored: blocks MOVE (yard -> tower); the walkable surface must
      // stay flat under them or every pick/place would dance around holes.
      IgnoreFromBuild(block);
      Blocks.Add(block);
    }
  }

  // ---- the build pad (east): platform + pegs + ghost slot marker --------------
  void BuildPad(Transform parent) {
    GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    platform.name = "BTPadPlatform";
    platform.transform.SetParent(parent, false);
    platform.transform.localPosition = new Vector3(PadPos.x, PadTopY * 0.5f, PadPos.z);
    platform.transform.localScale = new Vector3(2.7f, PadTopY * 0.5f, 2.7f);
    platform.GetComponent<Renderer>().sharedMaterial = Lit(WoodTan);
    StripCollider(platform);
    IgnoreFromBuild(platform); // the pad GRAPHIC never bakes; the ground does
    for (int i = 0; i < 4; i++) {
      float sx = (i % 2 == 0) ? -1f : 1f;
      float sz = (i / 2 == 0) ? -1f : 1f;
      GameObject peg = Cylinder(parent, "BTPadPeg" + i,
        new Vector3(PadPos.x + sx * 1.05f, 0.22f, PadPos.z + sz * 1.05f), 0.14f, 0.44f, FenceWood);
      // Bake-ignored: the pegs are dressing around the child's working spot —
      // they must never carve the pad out of the walkable surface.
      IgnoreFromBuild(peg);
    }
    // Ghost slot marker: a thin emissive disc that rides the NEXT stack slot
    // (placement preview only — placement still needs the confirm interaction).
    GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ghost.name = "BTGhostSlot";
    ghost.transform.SetParent(parent, false);
    ghost.transform.localPosition = new Vector3(PadPos.x, PadTopY + 0.02f, PadPos.z);
    ghost.transform.localScale = new Vector3(0.64f, 0.01f, 0.64f);
    ghost.GetComponent<Renderer>().sharedMaterial = LitEmissive(Gold, 0.45f);
    StripCollider(ghost);
    IgnoreFromBuild(ghost);
    ghost.SetActive(false);
    Ghost = ghost;
    GameObject anchor = new GameObject("BTPadAnchor");
    anchor.transform.SetParent(parent, false);
    anchor.transform.localPosition = PadPos;
    PadAnchor = anchor.transform;
  }

  void BuildCountAndResult(Transform parent) {
    // Result board ("N + tick"): right-front of the pad, OFF the spawn
    // sightline (same lesson as the earlier arenas).
    int n = ClampTarget(BoardTarget);
    GameObject result = new GameObject("BTResult");
    result.transform.SetParent(parent, false);
    result.transform.localPosition = new Vector3(3.4f, 0f, -0.6f);
    Box(result.transform, "BTResultPost", new Vector3(0f, 0.65f, 0f),
      new Vector3(0.13f, 1.3f, 0.13f), FenceWood);
    GameObject resultFrame = Box(result.transform, "BTResultFrame", new Vector3(0f, 1.6f, 0f),
      new Vector3(1.2f, 1.05f, 0.12f), BoardCream);
    SetMaterial(resultFrame, LitEmissive(BoardCream, 0.18f));
    GameObject resultDigit = CountingGardenBuilder.Digit(result.transform, "BTResultDigit",
      new Vector3(0f, 1.2f, -0.09f), 0.6f, 0.45f, Gold, 90f, n);
    SetMaterial(resultDigit, LitEmissive(Gold, 0.45f));
    CountingGardenBuilder.CheckMark(result.transform, "BTResultCheck",
      new Vector3(0f, 1.82f, -0.09f), 0.3f, MintLeaf);
    result.SetActive(false);
    Result = result;
    DemoJuice.AttachSpotlight(parent, "BTGameSpotlight", new Vector3(0.3f, 0.018f, 3.1f), 5.6f);
  }

  void BuildCameras(Transform parent) {
    // A: the lesson (board + teacher + student + field glimpse).
    CamTeaching = CamAnchor(parent, "BTCamA", new Vector3(0.5f, 2.3f, -1.6f));
    LookTeaching = CamAnchor(parent, "BTLookA", new Vector3(0.2f, 1.5f, 6.6f));
    // B: the demo (yard + student + pad + growing tower, one wide frame).
    CamDemo = CamAnchor(parent, "BTCamB", new Vector3(0.6f, 3.0f, -3.2f));
    LookDemo = CamAnchor(parent, "BTLookB", new Vector3(0.2f, 0.9f, 3.0f));
    // C: the payoff base pose; the game raises/pulls it with the REAL tower
    // height (FramePointFor) so target 9 never leaves the frame (§18).
    CamSuccess = CamAnchor(parent, "BTCamC", new Vector3(2.9f, 2.0f, -0.6f));
    LookSuccess = CamAnchor(parent, "BTLookC", new Vector3(PadPos.x, 1.0f, PadPos.z));
  }

  static Transform CamAnchor(Transform parent, string name, Vector3 pos) {
    GameObject go = new GameObject(name);
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    return go.transform;
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "BuildTowerPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0.2f, 0f, 3.1f));
    a.Npc = a.EnsureSlot("NpcAnchor", TeacherStart);
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0.6f, 3.2f, -5.5f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0.2f, 1.1f, 3.0f));
    a.Prompt = a.EnsureSlot("PromptAnchor", new Vector3(0.2f, 1.7f, 3.1f));
    a.Feedback = a.EnsureSlot("FeedbackAnchor", new Vector3(2.3f, 1.2f, 3.1f));
    a.Reward = a.EnsureSlot("RewardAnchor", PadPos);
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  // ---- helpers (same discipline as the three earlier arena builders) ------------

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

  // Emissive Lit material (URP): the number/boards must read even when the
  // directional light is behind them (same lesson as the earlier arenas).
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
