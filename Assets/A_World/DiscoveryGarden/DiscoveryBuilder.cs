// A_World/DiscoveryGarden/DiscoveryBuilder.cs â€” S3-P2Z18 GAMEPLAY #7
// "VÆ¯á»œN KHĂM PHĂ" (Discovery Garden). The garden's OWN lazy scene
// (DiscoveryScene), reached from the Math Hub's discovery_garden gate
// (magnifier identity) through the shared micro slot â€” never at boot, never
// stacked, no second loader.
// Identity: a searchable garden of POCKETS â€” an apple tree (west), a flower
// bed (middle), a butterfly bush (east) â€” joined by gravel spurs and stepping
// circles. Each pocket is the natural CLUE for its kind; the distractors
// (balls, leaves, mushrooms) sit in and between the pockets. One garden serves
// every round: round 0 activates the core candidate set, round 1 adds four
// extra distractors (more searching, same arena).
// Firewall: presentation + the exit door only â€” no quest/bus/save here; the
// activity (DiscoveryGame) is wired by GameInstaller.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DiscoveryBuilder : MonoBehaviour {
  public const string SceneName = "DiscoveryScene";

  // Separate island between the Delivery Village (+420) and the Match Meadow
  // (+600) â€” free slot, no overlap with any live world.
  public static readonly Vector3 WorldOffset = new Vector3(480f, 0f, 0f);
  public const float BoundX = 18f;
  public const float BoundZ = 18f;
  // Arrival spawn sits clear of the exit portal fire radius (J4 lesson: the
  // portal only arms after the child walks clear of it once).
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -11.5f);
  // Follow camera: north of the child, a touch higher than the other worlds
  // (searching reads over the pocket bushes), looking south INTO the garden.
  public static readonly Vector3 FollowOffset = new Vector3(0f, 4.3f, -5.6f);

  public const string ObjectiveEn = "Discovery Garden";
  public const string ObjectiveVi = "VÆ°á»n KhĂ¡m PhĂ¡";

  // The three tasks of one visit, in order (brief Â§23).
  public static readonly DiscoveryItem.ItemKind[] TaskKinds = {
    DiscoveryItem.ItemKind.Apple,
    DiscoveryItem.ItemKind.Flower,
    DiscoveryItem.ItemKind.Butterfly,
  };
  public const int TaskCount = 3;

  // ---- acting layout (child scale; entry z=-3, the board faces the child) ----
  public static readonly Vector3 BoardPos = new Vector3(0f, 0f, 7.4f);
  public static readonly Vector3 TeacherStart = new Vector3(-1.4f, 0f, 6.4f);
  public static readonly Vector3 StudentStart = new Vector3(0.7f, 0f, 5.5f);
  public static readonly Vector3 StudentReturn = new Vector3(1.6f, 0f, 5.2f);
  // The demo/search fork (the student never walks straight at the target).
  public static readonly Vector3 ForkLocal = new Vector3(0f, 0f, 2.0f);
  // Pocket hearts (landmarks + clues).
  public static readonly Vector3 AppleTreePos = new Vector3(-3.0f, 0f, 3.2f);
  public static readonly Vector3 FlowerBedPos = new Vector3(-0.2f, 0f, 3.4f);
  public static readonly Vector3 ButterflyBushPos = new Vector3(2.9f, 0f, 3.4f);
  // The butterfly's gentle orbit around its bush (moving target, slow + small).
  public const float ButterflyOrbitRadius = 0.85f;
  public const float ButterflyHoverY = 1.05f;

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public GameObject ReferenceBoard { get; private set; } // the task icon board
  public GameObject[] TaskIcons { get; private set; }     // apple/flower/butterfly
  public GameObject Result { get; private set; }          // 3 slots + tick (hidden)
  public GameObject[] ResultSlots { get; private set; }
  public GameObject ExitCue { get; private set; }         // shown after completion
  public List<GameObject> Items { get; private set; } = new List<GameObject>();
  public DiscoveryItem.ItemKind[] ItemKinds { get; private set; }
  public bool[] ItemExtras { get; private set; }          // round-1 tier
  public Transform CamTeaching { get; private set; }
  public Transform LookTeaching { get; private set; }
  public Transform CamDemo { get; private set; }
  public Transform LookDemo { get; private set; }
  public Transform CamSuccess { get; private set; }
  public Transform LookSuccess { get; private set; }

  static readonly Color Lawn = new Color(0.38f, 0.64f, 0.36f);
  static readonly Color Meadow = new Color(0.46f, 0.71f, 0.42f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);
  static readonly Color CourtyardSand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color StoneGrey = new Color(0.68f, 0.68f, 0.66f);
  static readonly Color BoardCream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color WoodTan = new Color(0.78f, 0.66f, 0.46f);
  static readonly Color FenceWood = new Color(0.55f, 0.40f, 0.24f);
  static readonly Color SoilBrown = new Color(0.45f, 0.32f, 0.20f);
  static readonly Color MintLeaf = new Color(0.70f, 0.90f, 0.72f);
  static readonly Color AppleRed = new Color(0.85f, 0.25f, 0.25f);
  static readonly Color StemBrown = new Color(0.42f, 0.30f, 0.18f);
  static readonly Color LeafGreen = new Color(0.30f, 0.65f, 0.28f);
  static readonly Color BushGreen = new Color(0.24f, 0.55f, 0.30f);
  static readonly Color PetalPink = new Color(0.95f, 0.62f, 0.75f);
  static readonly Color PetalYellow = new Color(0.98f, 0.86f, 0.42f);
  static readonly Color WingBlue = new Color(0.45f, 0.72f, 0.95f);
  static readonly Color BallBlue = new Color(0.30f, 0.55f, 0.95f);
  static readonly Color MushroomCap = new Color(0.88f, 0.42f, 0.38f);

  // Scene entry (GameInstaller calls this after the lazy load).
  public void Build() {
    BuildContent(transform);
    BuildNavMesh(transform);
  }

  // Runtime NavMesh bake for THIS scene only (CollectObjects.Children on the
  // arena root â€” J8 lesson: a scene-wide bake would collect Main/Math meshes).
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
    BuildReferenceBoard(root);
    BuildPockets(root);
    BuildItems(root);
    BuildResult(root);
    BuildCameras(root);
    BuildAnchors(root);
    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  void BuildGround(Transform parent) {
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "DGRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(40f, 1.4f, 40f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    StripCollider(rim);
    IgnoreFromBuild(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "DGGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localPosition = Vector3.zero;
    ground.transform.localScale = new Vector3(3.8f, 1f, 3.8f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    MeadowPatch(parent, "DGMeadowW", new Vector3(-9f, 0f, 4f), 11f, 9f);
    MeadowPatch(parent, "DGMeadowE", new Vector3(9f, 0f, 4f), 11f, 9f);
    GameObject meadowN = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    meadowN.name = "DGMeadowN";
    meadowN.transform.SetParent(parent, false);
    meadowN.transform.localPosition = new Vector3(0f, 0.004f, 8.5f);
    meadowN.transform.localScale = new Vector3(12f, 0.004f, 6f);
    meadowN.GetComponent<Renderer>().sharedMaterial = Lit(Meadow);
    StripCollider(meadowN);
    IgnoreFromBuild(meadowN);
    float[] angles = { 15f, 45f, 75f, 105f, 135f, 160f, 200f, 225f, 255f, 285f, 315f, 345f };
    for (int i = 0; i < angles.Length; i++) {
      float rad = angles[i] * Mathf.Deg2Rad;
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "DGHedge" + i;
      bush.transform.SetParent(parent, false);
      bush.transform.localPosition = new Vector3(Mathf.Sin(rad) * 15.5f, 0.55f, 3f + Mathf.Cos(rad) * 15.5f);
      bush.transform.localScale = (i % 2 == 0) ? new Vector3(3.4f, 2.2f, 3.4f) : new Vector3(2.8f, 1.9f, 2.8f);
      bush.GetComponent<Renderer>().sharedMaterial = Lit(BushGreen);
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
    Box(parent, "DGEntryPostL", new Vector3(-1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    Box(parent, "DGEntryPostR", new Vector3(1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    GameObject beam = Box(parent, "DGEntryBeam", new Vector3(0f, 2.06f, -9.5f),
      new Vector3(3.4f, 0.16f, 0.16f), WorldBeauty.BlossomPink);
    IgnoreFromBuild(beam);
    WorldBeauty.Ball(parent, "DGEntryBlossom0", new Vector3(0f, 2.42f, -9.5f), 1.1f,
      WorldBeauty.BlossomPink);
    Pad(parent, "DGThresholdL", new Vector3(-1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "DGThresholdR", new Vector3(1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "DGExitDisc", new Vector3(0f, 0.01f, -11.1f), 3.2f, WorldBeauty.Petal);
    GameObject exitGo = new GameObject("DGExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.fireRadius = 1.35f;
    exit.areaId = DiscoveryArea.AreaId;
    ExitPortal = exit;
    Box(parent, "DGExitPostL", new Vector3(-1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Box(parent, "DGExitPostR", new Vector3(1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Sphere(parent, "DGExitCapL", new Vector3(-1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
    Sphere(parent, "DGExitCapR", new Vector3(1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
    // EXIT CUE (brief Â§24): a leaf-green beacon over the exit arch, hidden
    // until every task is found â€” the world says "the way home is open".
    GameObject cue = GameObject.CreatePrimitive(PrimitiveType.Cube);
    cue.name = "DGExitCue";
    cue.transform.SetParent(parent, false);
    cue.transform.localPosition = new Vector3(0f, 2.75f, -11.5f);
    cue.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    cue.transform.localScale = new Vector3(0.5f, 0.5f, 0.14f);
    cue.GetComponent<Renderer>().sharedMaterial = LitEmissive(MintLeaf, 0.6f);
    StripCollider(cue);
    IgnoreFromBuild(cue);
    cue.SetActive(false);
    ExitCue = cue;
  }

  void BuildPaths(Transform parent) {
    Pad(parent, "DGPlazaPad", new Vector3(0f, 0.004f, 0f), 7.4f, CourtyardSand);
    Seg(parent, "DGPathEntry", new Vector3(0f, 0f, -10.4f), new Vector3(0f, 0f, -1.0f), 1.7f);
    Seg(parent, "DGPathStage", new Vector3(0f, 0f, -1.0f), new Vector3(0f, 0f, 7.8f), 1.7f);
    // Three gravel spurs + stepping circles: navigation breadcrumbs toward the
    // pockets, never arrows.
    Seg(parent, "DGSpurA", new Vector3(0f, 0f, 1.6f), new Vector3(-2.4f, 0f, 2.6f), 1.2f);
    Seg(parent, "DGSpurB", new Vector3(-0.2f, 0f, 1.8f), new Vector3(-0.3f, 0f, 2.7f), 1.2f);
    Seg(parent, "DGSpurC", new Vector3(0.3f, 0f, 1.9f), new Vector3(2.2f, 0f, 2.7f), 1.2f);
    Pad(parent, "DGStepA", new Vector3(-2.4f, 0.012f, 2.6f), 1.1f, StoneGrey);
    Pad(parent, "DGStepB", new Vector3(-0.3f, 0.012f, 2.75f), 1.1f, StoneGrey);
    Pad(parent, "DGStepC", new Vector3(2.2f, 0.012f, 2.7f), 1.1f, StoneGrey);
  }

  void BuildDressing(Transform parent) {
    // Trees stay OFF every walk line (entry/orientation/pocket corridors).
    Vector3[] trees = {
      new Vector3(-6.5f, 0f, -3.5f), new Vector3(6.5f, 0f, -3.5f),
      new Vector3(-10f, 0f, 8f), new Vector3(10f, 0f, 8f),
      new Vector3(-5.5f, 0f, 15.5f), new Vector3(5.5f, 0f, 15.5f),
    };
    float[] scales = { 0.62f, 0.62f, 0.7f, 0.7f, 0.7f, 0.7f };
    for (int i = 0; i < trees.Length; i++) {
      WorldBeauty.BlossomTree(parent, "DGBlossomTree" + i, trees[i], scales[i]);
      WorldBeauty.PetalCarpet(parent, "DGPetalCarpet" + i, trees[i], 1.5f * scales[i] + 0.5f);
    }
    Vector3[] drifts = {
      new Vector3(-4.8f, 0f, 5.6f), new Vector3(4.8f, 0f, 6.0f),
      new Vector3(-4.6f, 0f, -6f), new Vector3(4.6f, 0f, -6f),
    };
    for (int i = 0; i < drifts.Length; i++)
      WorldBeauty.FlowerDrift(parent, "DGFlowerDrift" + i, drifts[i], 1.2f);
    WorldBeauty.PetalFall(parent, "DGPetalFall", new Vector3(0f, 0f, -1f), 9f, 12, 51109);
    WorldBeauty.Butterfly(parent, "DGDecorButterfly0", new Vector3(-4.4f, 0f, 6.2f), 2.2f, 0.1f,
      WorldBeauty.BlossomDeep, WorldBeauty.BlossomCream);
    WorldBeauty.Butterfly(parent, "DGDecorButterfly1", new Vector3(4.6f, 0f, 6.4f), 2.2f, 0.6f,
      WorldBeauty.Lilac, WorldBeauty.BlossomPink);
    // A fallen log + stones: natural furniture, all bake-ignored (open field).
    GameObject log = Box(parent, "DGLog", new Vector3(-1.7f, 0.2f, 4.9f),
      new Vector3(1.6f, 0.4f, 0.42f), SoilBrown);
    log.transform.localRotation = Quaternion.Euler(0f, 24f, 0f);
    IgnoreFromBuild(log);
    Sphere(parent, "DGStone0", new Vector3(-1.9f, 0.14f, 4.3f), 0.46f, StoneGrey, true);
    Sphere(parent, "DGStone1", new Vector3(1.6f, 0.12f, 4.6f), 0.4f, StoneGrey, true);
    Sphere(parent, "DGStone2", new Vector3(-4.6f, 0.13f, 1.6f), 0.44f, StoneGrey, true);
  }

  // The reference board: a magnifier motif + the CURRENT task's icon
  // (apple / flower / butterfly). The board is the assignment; the world says
  // where to look (pockets), never an arrow.
  void BuildReferenceBoard(Transform parent) {
    Box(parent, "DGBoardL", BoardPos + new Vector3(-1.25f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), FenceWood);
    Box(parent, "DGBoardR", BoardPos + new Vector3(1.25f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), FenceWood);
    GameObject panel = Box(parent, "DGBoardPanel", BoardPos + new Vector3(0f, 1.95f, 0f),
      new Vector3(2.6f, 1.7f, 0.12f), BoardCream);
    SetMaterial(panel, LitEmissive(BoardCream, 0.22f));
    // Magnifier motif (top-left of the panel): ring + glass + tilted handle.
    GameObject ring = Cylinder(parent, "DGBoardRing", BoardPos + new Vector3(-0.85f, 2.5f, -0.09f),
      0.62f, 0.1f, Gold);
    ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    SetMaterial(ring, LitEmissive(Gold, 0.4f));
    GameObject glass = Cylinder(parent, "DGBoardGlass", BoardPos + new Vector3(-0.85f, 2.5f, -0.12f),
      0.42f, 0.08f, new Color(0.78f, 0.90f, 0.96f));
    glass.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    GameObject handle = Box(parent, "DGBoardHandle", BoardPos + new Vector3(-0.5f, 2.2f, -0.09f),
      new Vector3(0.12f, 0.5f, 0.12f), Gold);
    handle.transform.localRotation = Quaternion.Euler(0f, 0f, -38f);
    // The three task icons, only the current one active.
    TaskIcons = new GameObject[TaskCount];
    Vector3[] iconPos = {
      BoardPos + new Vector3(0.55f, 1.5f, -0.12f),
      BoardPos + new Vector3(0.55f, 1.5f, -0.12f),
      BoardPos + new Vector3(0.55f, 1.5f, -0.12f),
    };
    TaskIcons[0] = MakeIcon(parent, DiscoveryItem.ItemKind.Apple, "DGBoardApple", iconPos[0], 1.15f);
    TaskIcons[1] = MakeIcon(parent, DiscoveryItem.ItemKind.Flower, "DGBoardFlower", iconPos[1], 1.15f);
    TaskIcons[2] = MakeIcon(parent, DiscoveryItem.ItemKind.Butterfly, "DGBoardButterfly", iconPos[2], 1.15f);
    ReferenceBoard = TaskIcons[0];
    for (int i = 1; i < TaskIcons.Length; i++)
      if (TaskIcons[i] != null) TaskIcons[i].SetActive(false);
  }

  // ---- the three searchable pockets ---------------------------------------------
  void BuildPockets(Transform parent) {
    // POCKET A (west): the apple tree â€” the apple clue made visible.
    Pad(parent, "DGPocketGroundA", new Vector3(AppleTreePos.x, 0.006f, AppleTreePos.z), 4.2f, Meadow);
    GameObject trunk = Cylinder(parent, "DGAppleTrunk", AppleTreePos + new Vector3(0f, 0.7f, 0f),
      0.36f, 1.4f, SoilBrown);
    IgnoreFromBuild(trunk);
    for (int i = 0; i < 4; i++) {
      float a = i * Mathf.PI * 0.5f + 0.4f;
      GameObject canopy = Sphere(parent, "DGAppleCanopy" + i,
        AppleTreePos + new Vector3(Mathf.Cos(a) * 0.75f, 1.75f + (i % 2) * 0.35f, Mathf.Sin(a) * 0.75f),
        1.5f, new Color(0.30f, 0.60f, 0.30f), true);
      IgnoreFromBuild(canopy);
    }
    // The clue: ripe apples hang in the tree (decor, collider-free).
    for (int i = 0; i < 3; i++) {
      float a = -0.6f + i * 0.9f;
      Sphere(parent, "DGTreeApple" + i,
        AppleTreePos + new Vector3(Mathf.Cos(a) * 0.9f, 1.5f - (i % 2) * 0.3f, Mathf.Sin(a) * 0.9f),
        0.3f, AppleRed, true);
    }
    // Two bushes frame the pocket mouth.
    Sphere(parent, "DGPocketBushA0", AppleTreePos + new Vector3(-1.5f, 0.4f, 1.3f), 1.3f, BushGreen, true);
    Sphere(parent, "DGPocketBushA1", AppleTreePos + new Vector3(1.4f, 0.38f, 1.5f), 1.2f, BushGreen, true);

    // POCKET B (middle): the flower bed â€” the flower clue made visible.
    Pad(parent, "DGPocketGroundB", new Vector3(FlowerBedPos.x, 0.006f, FlowerBedPos.z), 4.0f, Meadow);
    Box(parent, "DGFlowerBedSoil", FlowerBedPos + new Vector3(0f, 0.08f, 0f),
      new Vector3(2.4f, 0.16f, 1.5f), SoilBrown);
    for (int i = 0; i < 6; i++) {
      float tx = -0.85f + (i % 3) * 0.85f;
      float tz = -0.3f + (i / 3) * 0.6f;
      MakeIcon(parent, DiscoveryItem.ItemKind.Flower, "DGFlowerBedFlower" + i,
        FlowerBedPos + new Vector3(tx, 0.34f, tz), 0.75f);
    }
    Sphere(parent, "DGPocketBushB0", FlowerBedPos + new Vector3(-1.6f, 0.36f, -1.2f), 1.2f, BushGreen, true);
    Sphere(parent, "DGPocketBushB1", FlowerBedPos + new Vector3(1.7f, 0.4f, 1.2f), 1.3f, BushGreen, true);

    // POCKET C (east): the butterfly bush â€” the butterfly clue made visible.
    Pad(parent, "DGPocketGroundC", new Vector3(ButterflyBushPos.x, 0.006f, ButterflyBushPos.z), 4.2f, Meadow);
    for (int i = 0; i < 4; i++) {
      float a = i * Mathf.PI * 0.5f + 0.9f;
      Sphere(parent, "DGButterflyBush" + i,
        ButterflyBushPos + new Vector3(Mathf.Cos(a) * 0.55f, 0.45f + (i % 2) * 0.2f, Mathf.Sin(a) * 0.55f),
        1.1f, BushGreen, true);
    }
    for (int i = 0; i < 3; i++) {
      float a = 0.2f + i * 1.1f;
      MakeIcon(parent, DiscoveryItem.ItemKind.Flower, "DGBushFlower" + i,
        ButterflyBushPos + new Vector3(Mathf.Cos(a) * 0.7f, 0.72f, Mathf.Sin(a) * 0.7f), 0.62f);
    }
    Sphere(parent, "DGPocketBushC0", ButterflyBushPos + new Vector3(-1.5f, 0.38f, 1.4f), 1.25f, BushGreen, true);
    Sphere(parent, "DGPocketBushC1", ButterflyBushPos + new Vector3(1.5f, 0.36f, -1.3f), 1.2f, BushGreen, true);
  }

  // ---- the searchable items (targets + distractors) -----------------------------
  // Item table: name, kind, pos, extra (round-1 tier). Core (round 0) = five
  // candidates; extras activate on round 1 (more searching, same garden).
  void BuildItems(Transform parent) {
    Items.Clear();
    var kinds = new List<DiscoveryItem.ItemKind>();
    var extras = new List<bool>();
    ItemSpec[] specs = {
      // core
      new ItemSpec("DGApple0", DiscoveryItem.ItemKind.Apple, new Vector3(-2.45f, 0f, 2.30f), false),
      new ItemSpec("DGFlower0", DiscoveryItem.ItemKind.Flower, new Vector3(-0.70f, 0f, 3.55f), false),
      new ItemSpec("DGButterfly0", DiscoveryItem.ItemKind.Butterfly, new Vector3(2.9f, 0f, 3.4f), false),
      new ItemSpec("DGBall0", DiscoveryItem.ItemKind.Ball, new Vector3(0.95f, 0f, 2.25f), false),
      new ItemSpec("DGLeaf0", DiscoveryItem.ItemKind.Leaf, new Vector3(-3.15f, 0f, 1.35f), false),
      // round-1 extras
      new ItemSpec("DGApple1", DiscoveryItem.ItemKind.Apple, new Vector3(-3.45f, 0f, 3.00f), true),
      new ItemSpec("DGFlower1", DiscoveryItem.ItemKind.Flower, new Vector3(0.45f, 0f, 3.10f), true),
      new ItemSpec("DGBall1", DiscoveryItem.ItemKind.Ball, new Vector3(-1.20f, 0f, 1.05f), true),
      new ItemSpec("DGMushroom0", DiscoveryItem.ItemKind.Mushroom, new Vector3(1.75f, 0f, 4.55f), true),
    };
    for (int i = 0; i < specs.Length; i++) {
      ItemSpec s = specs[i];
      float restY = RestY(s.Kind);
      GameObject item = MakeIcon(parent, s.Kind, s.Name,
        new Vector3(s.Pos.x, restY, s.Pos.z), 1f);
      AddItem(item, s.Kind, s.Extra, kinds, extras);
    }
    ItemKinds = kinds.ToArray();
    ItemExtras = extras.ToArray();
  }

  void AddItem(GameObject go, DiscoveryItem.ItemKind kind, bool extra,
      List<DiscoveryItem.ItemKind> kinds, List<bool> extras) {
    Items.Add(go);
    kinds.Add(kind);
    extras.Add(extra);
  }

  struct ItemSpec {
    public string Name;
    public DiscoveryItem.ItemKind Kind;
    public Vector3 Pos;
    public bool Extra;
    public ItemSpec(string name, DiscoveryItem.ItemKind kind, Vector3 pos, bool extra) {
      Name = name; Kind = kind; Pos = pos; Extra = extra;
    }
  }

  // Resting height per kind (icon groups are authored around their centre).
  public static float RestY(DiscoveryItem.ItemKind kind) {
    if (kind == DiscoveryItem.ItemKind.Butterfly) return ButterflyHoverY;
    return 0.2f;
  }

  void BuildResult(Transform parent) {
    GameObject result = new GameObject("DGResult");
    result.transform.SetParent(parent, false);
    result.transform.localPosition = new Vector3(3.4f, 0f, -0.6f);
    Box(result.transform, "DGResultPost", new Vector3(0f, 0.65f, 0f),
      new Vector3(0.13f, 1.3f, 0.13f), FenceWood);
    GameObject resultFrame = Box(result.transform, "DGResultFrame", new Vector3(0f, 1.6f, 0f),
      new Vector3(2.0f, 1.15f, 0.12f), BoardCream);
    SetMaterial(resultFrame, LitEmissive(BoardCream, 0.18f));
    ResultSlots = new GameObject[TaskCount];
    for (int i = 0; i < TaskCount; i++) {
      GameObject slot = MakeIcon(result.transform, TaskKinds[i], "DGResultSlot" + i,
        new Vector3(-0.6f + i * 0.6f, 1.55f, -0.09f), 0.55f);
      if (slot != null) slot.SetActive(false);
      ResultSlots[i] = slot;
    }
    CountingGardenBuilder.CheckMark(result.transform, "DGResultCheck",
      new Vector3(0f, 1.95f, -0.09f), 0.3f, MintLeaf);
    result.SetActive(false);
    Result = result;
    // USER ROUND 2026-10-01: the big glowing floor disc was removed (annoying
    // blob under the child).
  }

  void BuildCameras(Transform parent) {
    CamTeaching = CamAnchor(parent, "DGCamA", new Vector3(0.5f, 2.3f, -1.6f));
    LookTeaching = CamAnchor(parent, "DGLookA", new Vector3(0.2f, 1.5f, 6.4f));
    CamDemo = CamAnchor(parent, "DGCamB", new Vector3(0.7f, 3.4f, -3.6f));
    LookDemo = CamAnchor(parent, "DGLookB", new Vector3(0.0f, 0.9f, 3.2f));
    CamSuccess = CamAnchor(parent, "DGCamC", new Vector3(2.6f, 2.3f, -0.8f));
    LookSuccess = CamAnchor(parent, "DGLookC", new Vector3(1.2f, 1.0f, 3.2f));
  }

  static Transform CamAnchor(Transform parent, string name, Vector3 pos) {
    GameObject go = new GameObject(name);
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    return go.transform;
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "DiscoveryPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0f, 0f, 3.0f));
    a.Npc = a.EnsureSlot("NpcAnchor", TeacherStart);
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0.6f, 3.4f, -5.6f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0.0f, 1.0f, 3.0f));
    a.Prompt = a.EnsureSlot("PromptAnchor", new Vector3(0f, 1.7f, 2.6f));
    a.Feedback = a.EnsureSlot("FeedbackAnchor", new Vector3(0.3f, 1.2f, 3.2f));
    a.Reward = a.EnsureSlot("RewardAnchor", FlowerBedPos);
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  // ---- icon kit (primitives; the SAME builders feed the board + items) -----------

  public GameObject MakeIcon(Transform parent, DiscoveryItem.ItemKind kind, string name,
      Vector3 pos, float scale) {
    GameObject icon = new GameObject(name);
    icon.transform.SetParent(parent, false);
    icon.transform.localPosition = pos;
    icon.transform.localScale = Vector3.one * Mathf.Max(0.25f, scale);
    switch (kind) {
      case DiscoveryItem.ItemKind.Apple: BuildApple(icon.transform, name); break;
      case DiscoveryItem.ItemKind.Flower: BuildFlower(icon.transform, name); break;
      case DiscoveryItem.ItemKind.Butterfly: BuildButterfly(icon.transform, name); break;
      case DiscoveryItem.ItemKind.Ball: BuildBall(icon.transform, name); break;
      case DiscoveryItem.ItemKind.Leaf: BuildLeaf(icon.transform, name); break;
      default: BuildMushroom(icon.transform, name); break;
    }
    StripCollider(icon);
    // The bake reads RENDER MESHES: every child piece must be ignored too, or
    // a flying butterfly would carve a headroom hole in the walkable field.
    Transform[] pieces = icon.GetComponentsInChildren<Transform>(true);
    for (int i = 0; i < pieces.Length; i++) IgnoreFromBuild(pieces[i].gameObject);
    return icon;
  }

  void BuildApple(Transform t, string name) {
    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    body.name = name + "Body";
    body.transform.SetParent(t, false);
    body.transform.localScale = new Vector3(0.34f, 0.31f, 0.34f);
    body.GetComponent<Renderer>().sharedMaterial = Lit(AppleRed);
    StripCollider(body);
    GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stem.name = name + "Stem";
    stem.transform.SetParent(t, false);
    stem.transform.localPosition = new Vector3(0f, 0.18f, 0f);
    stem.transform.localScale = new Vector3(0.05f, 0.11f, 0.05f);
    stem.GetComponent<Renderer>().sharedMaterial = Lit(StemBrown);
    StripCollider(stem);
    GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    leaf.name = name + "Leaf";
    leaf.transform.SetParent(t, false);
    leaf.transform.localPosition = new Vector3(0.1f, 0.18f, 0f);
    leaf.transform.localScale = new Vector3(0.15f, 0.035f, 0.09f);
    leaf.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
    leaf.GetComponent<Renderer>().sharedMaterial = Lit(LeafGreen);
    StripCollider(leaf);
  }

  void BuildFlower(Transform t, string name) {
    GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stem.name = name + "Stem";
    stem.transform.SetParent(t, false);
    stem.transform.localPosition = new Vector3(0f, -0.02f, 0f);
    stem.transform.localScale = new Vector3(0.05f, 0.3f, 0.05f);
    stem.GetComponent<Renderer>().sharedMaterial = Lit(LeafGreen);
    StripCollider(stem);
    for (int i = 0; i < 5; i++) {
      float a = i * Mathf.PI * 2f / 5f;
      GameObject petal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      petal.name = name + "Petal" + i;
      petal.transform.SetParent(t, false);
      petal.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.16f, 0.2f, Mathf.Sin(a) * 0.16f);
      petal.transform.localScale = new Vector3(0.2f, 0.05f, 0.14f);
      petal.GetComponent<Renderer>().sharedMaterial = Lit(PetalPink);
      StripCollider(petal);
    }
    GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    core.name = name + "Core";
    core.transform.SetParent(t, false);
    core.transform.localPosition = new Vector3(0f, 0.22f, 0f);
    core.transform.localScale = new Vector3(0.14f, 0.08f, 0.14f);
    core.GetComponent<Renderer>().sharedMaterial = Lit(PetalYellow);
    StripCollider(core);
  }

  void BuildButterfly(Transform t, string name) {
    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    body.name = name + "Body";
    body.transform.SetParent(t, false);
    body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    body.transform.localScale = new Vector3(0.06f, 0.16f, 0.06f);
    body.GetComponent<Renderer>().sharedMaterial = Lit(StemBrown);
    StripCollider(body);
    for (int s = 0; s < 2; s++) {
      float sign = s == 0 ? -1f : 1f;
      GameObject wing = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      wing.name = name + "Wing" + s;
      wing.transform.SetParent(t, false);
      wing.transform.localPosition = new Vector3(sign * 0.19f, 0.05f, 0f);
      wing.transform.localScale = new Vector3(0.3f, 0.16f, 0.34f);
      wing.GetComponent<Renderer>().sharedMaterial = Lit(WingBlue);
      StripCollider(wing);
    }
    GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    head.name = name + "Head";
    head.transform.SetParent(t, false);
    head.transform.localPosition = new Vector3(0f, 0f, 0.16f);
    head.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
    head.GetComponent<Renderer>().sharedMaterial = Lit(StemBrown);
    StripCollider(head);
  }

  void BuildBall(Transform t, string name) {
    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    body.name = name + "Body";
    body.transform.SetParent(t, false);
    body.transform.localScale = new Vector3(0.34f, 0.34f, 0.34f);
    body.GetComponent<Renderer>().sharedMaterial = Lit(BallBlue);
    StripCollider(body);
    GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stripe.name = name + "Stripe";
    stripe.transform.SetParent(t, false);
    stripe.transform.localScale = new Vector3(0.36f, 0.02f, 0.36f);
    stripe.GetComponent<Renderer>().sharedMaterial = Lit(BoardCream);
    StripCollider(stripe);
  }

  void BuildLeaf(Transform t, string name) {
    GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    leaf.name = name + "Blade";
    leaf.transform.SetParent(t, false);
    leaf.transform.localPosition = new Vector3(0f, 0.04f, 0f);
    leaf.transform.localScale = new Vector3(0.4f, 0.05f, 0.24f);
    leaf.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
    leaf.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.45f, 0.68f, 0.30f));
    StripCollider(leaf);
    GameObject vein = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    vein.name = name + "Vein";
    vein.transform.SetParent(t, false);
    vein.transform.localPosition = new Vector3(0f, 0.07f, 0f);
    vein.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
    vein.transform.localScale = new Vector3(0.035f, 0.3f, 0.035f);
    vein.GetComponent<Renderer>().sharedMaterial = Lit(StemBrown);
    StripCollider(vein);
  }

  void BuildMushroom(Transform t, string name) {
    GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stem.name = name + "Stem";
    stem.transform.SetParent(t, false);
    stem.transform.localPosition = new Vector3(0f, -0.04f, 0f);
    stem.transform.localScale = new Vector3(0.12f, 0.16f, 0.12f);
    stem.GetComponent<Renderer>().sharedMaterial = Lit(BoardCream);
    StripCollider(stem);
    GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    cap.name = name + "Cap";
    cap.transform.SetParent(t, false);
    cap.transform.localPosition = new Vector3(0f, 0.12f, 0f);
    cap.transform.localScale = new Vector3(0.34f, 0.2f, 0.34f);
    cap.GetComponent<Renderer>().sharedMaterial = Lit(MushroomCap);
    StripCollider(cap);
  }

  // ---- helpers (same discipline as the earlier arena builders) ------------------

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

