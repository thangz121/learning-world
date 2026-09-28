// A_World/MatchMeadow/MatchMeadowBuilder.cs — S3-P2Z17 GAMEPLAY #6
// "GHÉP ĐÚNG CẶP" (match the pair). The Math Hub's match_meadow gate opens
// this OWN lazy scene through the shared micro slot — never at boot, never
// stacked. The meadow is a REAL 3D matching world (never a memory-card UI):
// a clear REFERENCE area (one pedestal per pair), a SEARCH field of candidate
// objects, and a PAIRING pad where the child brings the identical object.
// One meadow serves every pair count (1..3): the round picks the family
// (1=balls, 2=flowers, 3=blocks) and the colours; the builder pre-builds the
// whole pool (3 families x 4 colours x reference+candidate) and the game
// activates only the round's subset. C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MatchMeadowBuilder : MonoBehaviour {
  public const string SceneName = "MatchMeadowScene";
  // Separate island after the corn one (+540 ... delivery +420, build +360).
  public static readonly Vector3 WorldOffset = new Vector3(600f, 0f, 0f);
  public const float BoundX = 18f;
  public const float BoundZ = 18f;
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -11.5f);
  public static readonly Vector3 FollowOffset = new Vector3(0f, 4.2f, -5.4f);
  public const string ObjectiveEn = "Match the Pairs";
  public const string ObjectiveVi = "Ghép Đúng Cặp";

  public const int MaxPairs = 3;
  public const int DefaultPairs = 1;
  public const int FamilyCount = 3;   // 0 balls, 1 flowers, 2 blocks
  public const int ColorCount = 4;    // 0 blue, 1 red, 2 yellow, 3 green
  public const int ColorBlue = 0, ColorRed = 1, ColorYellow = 2, ColorGreen = 3;

  // Round composition (ONE meadow, many pair counts — brief §12/§17).
  public static int FamilyForPairs(int pairs) {
    if (pairs <= 1) return 0;      // balls
    if (pairs == 2) return 1;      // flowers
    return 2;                      // blocks
  }
  public static int[] ColorsForPairs(int pairs) {
    if (pairs <= 1) return new[] { ColorBlue };
    if (pairs == 2) return new[] { ColorBlue, ColorRed };
    return new[] { ColorBlue, ColorRed, ColorYellow };
  }
  public static int DistractorForPairs(int pairs) {
    if (pairs <= 1) return ColorGreen;
    if (pairs == 2) return ColorYellow;
    return ColorGreen;
  }

  // ---- shared layout (child scale; entry z=-3, board faces the child) ---------------
  public static readonly Vector3 BoardPos = new Vector3(0f, 0f, 7.6f);
  // Result board ("N + tick"): RIGHT END of the pairing mat, on the payoff
  // axis (S3-P2Z18 user round: the old right-front spot sat beside the success
  // camera and the payoff shot cropped it). Clear of pedestal 3 + pair slot 3.
  public static readonly Vector3 ResultPos = new Vector3(3.2f, 0f, 5.6f);
  public static readonly Vector3 TeacherStart = new Vector3(-1.4f, 0f, 6.6f);
  public static readonly Vector3 StudentStart = new Vector3(0.7f, 0f, 5.7f);
  public static readonly Vector3 StudentReturn = new Vector3(1.6f, 0f, 5.4f);
  // REFERENCE AREA: three pedestals, one per pair (clear, clean, readable).
  public static readonly Vector3[] RefSlotLocal = {
    new Vector3(-2.6f, 0f, 4.5f), new Vector3(0f, 0f, 4.8f), new Vector3(2.6f, 0f, 4.5f),
  };
  // PAIR AREA: where the matching object is placed, right beside its reference.
  public static readonly Vector3[] PairSlotLocal = {
    new Vector3(-1.55f, 0f, 4.15f), new Vector3(1.05f, 0f, 4.45f), new Vector3(3.65f, 0f, 4.15f),
  };
  // SEARCH FIELD: six candidate spots, spaced, never crowded (brief §16).
  public static readonly Vector3[] CandidateSpotLocal = {
    new Vector3(-4.0f, 0f, 1.0f), new Vector3(-2.3f, 0f, -0.4f), new Vector3(-0.8f, 0f, 1.4f),
    new Vector3(0.8f, 0f, -0.6f), new Vector3(2.3f, 0f, 1.2f), new Vector3(4.0f, 0f, 0.2f),
  };
  public static readonly Vector3 RewardPos = new Vector3(0f, 0f, 6.4f);
  public const float ItemSize = 0.5f;
  // S3-P2Z19 user round: after entering the meadow the child walks to the
  // marked play spot; the question is read ONLY on arrival (no in-arena demo).
  public static readonly Vector3 PlaySpotLocal = new Vector3(0f, 0f, 2.6f);
  public const float PlaySpotRadius = 1.7f;

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public GameObject NumberBoard { get; private set; }
  public GameObject Result { get; private set; }
  public GameObject RewardBloom { get; private set; }
  public List<GameObject> Items { get; private set; } = new List<GameObject>();
  public int[] ItemFamily { get; private set; }
  public int[] ItemColor { get; private set; }
  public bool[] ItemIsReference { get; private set; }
  public Transform[] RefPedestals { get; private set; }
  public GameObject PairPad { get; private set; }
  public Transform PadAnchor { get; private set; }
  // S3-P2Z19: the marked play spot (the question is read only on arrival).
  public GameObject PlaySpot { get; private set; }
  public GameObject PlayRing { get; private set; }
  // S3-P2Z20: the "Come here!" sign (hidden as the child arrives).
  public GameObject PlaySign { get; private set; }
  public Transform CamTeaching { get; private set; }
  public Transform LookTeaching { get; private set; }
  public Transform CamDemo { get; private set; }
  public Transform LookDemo { get; private set; }
  public Transform CamSuccess { get; private set; }
  public Transform LookSuccess { get; private set; }
  public int BoardPairs = DefaultPairs;

  public void Build() {
    BuildContent(transform);
    // Flat-ground discipline (rabbit/journey lesson): every decor renderer is
    // excluded from the bake so no low mesh can carve the play surface.
    IgnoreAllDecorExceptGround(transform, "MMGround");
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
    BuildPlaySpot(root);
    BuildBoard(root);
    BuildResult(root);
    BuildPairingArea(root);
    BuildSearchField(root);
    BuildReward(root);
    BuildDressing(root);
    BuildCameras(root);
    BuildPool(root);
    BuildAnchors(root);
    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  // ---- shell --------------------------------------------------------------------------

  static readonly Color Lawn = new Color(0.38f, 0.64f, 0.36f);
  static readonly Color Meadow = new Color(0.46f, 0.71f, 0.42f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);
  static readonly Color CourtyardSand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color StoneGrey = new Color(0.68f, 0.68f, 0.66f);
  static readonly Color BoardCream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color FenceWood = new Color(0.55f, 0.40f, 0.24f);
  static readonly Color MintLeaf = new Color(0.70f, 0.90f, 0.72f);
  static readonly Color Aqua = new Color(0.24f, 0.60f, 0.72f);
  public static readonly Color[] Palette = {
    new Color(0.25f, 0.48f, 0.90f), // blue
    new Color(0.84f, 0.24f, 0.26f), // red
    new Color(0.95f, 0.80f, 0.22f), // yellow
    new Color(0.30f, 0.66f, 0.32f), // green
  };

  void BuildGround(Transform parent) {
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "MMRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(40f, 1.4f, 40f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    StripCollider(rim);
    IgnoreFromBuild(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "MMGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localPosition = Vector3.zero;
    ground.transform.localScale = new Vector3(3.8f, 1f, 3.8f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    MeadowPatch(parent, "MMMeadowW", new Vector3(-9f, 0f, 4f), 11f, 9f);
    MeadowPatch(parent, "MMMeadowE", new Vector3(9f, 0f, 4f), 11f, 9f);
    float[] angles = { 15f, 45f, 75f, 105f, 135f, 160f, 200f, 225f, 255f, 285f, 315f, 345f };
    for (int i = 0; i < angles.Length; i++) {
      float rad = angles[i] * Mathf.Deg2Rad;
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "MMHedge" + i;
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
    Box(parent, "MMEntryPostL", new Vector3(-1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    Box(parent, "MMEntryPostR", new Vector3(1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    GameObject beam = Box(parent, "MMEntryBeam", new Vector3(0f, 2.06f, -9.5f),
      new Vector3(3.4f, 0.16f, 0.16f), WorldBeauty.BlossomPink);
    IgnoreFromBuild(beam);
    WorldBeauty.Ball(parent, "MMEntryBlossom0", new Vector3(0f, 2.42f, -9.5f), 1.1f,
      WorldBeauty.BlossomPink);
    Pad(parent, "MMThresholdL", new Vector3(-1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "MMThresholdR", new Vector3(1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "MMExitDisc", new Vector3(0f, 0.01f, -11.1f), 3.2f, WorldBeauty.Petal);
    GameObject exitGo = new GameObject("MMExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.fireRadius = 1.35f;
    exit.areaId = MatchArea.AreaId; // the meadow area (bound by the installer)
    ExitPortal = exit;
    Box(parent, "MMExitPostL", new Vector3(-1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Box(parent, "MMExitPostR", new Vector3(1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Sphere(parent, "MMExitCapL", new Vector3(-1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
    Sphere(parent, "MMExitCapR", new Vector3(1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
  }

  void BuildPaths(Transform parent) {
    Pad(parent, "MMPlazaPad", new Vector3(0f, 0.004f, 0f), 7.0f, CourtyardSand);
    Seg(parent, "MMPathEntry", new Vector3(0f, 0f, -10.4f), new Vector3(0f, 0f, -1.0f), 1.7f);
    Seg(parent, "MMPathStage", new Vector3(0f, 0f, -1.0f), new Vector3(0f, 0f, 8.2f), 1.7f);
    Seg(parent, "MMPathFieldW", new Vector3(0f, 0f, 0.6f), new Vector3(-4.6f, 0f, 0.6f), 1.4f);
    Seg(parent, "MMPathFieldE", new Vector3(0f, 0f, 0.6f), new Vector3(4.6f, 0f, 0.6f), 1.4f);
  }

  // The marked play spot (S3-P2Z19 user round): the child walks here; the
  // question is read on arrival (same contract as the earlier arenas).
  void BuildPlaySpot(Transform parent) {
    GameObject spot = new GameObject("MMPlaySpot");
    spot.transform.SetParent(parent, false);
    spot.transform.localPosition = PlaySpotLocal;
    PlaySpot = spot;
    Pad(parent, "MMPlayEdge", PlaySpotLocal + new Vector3(0f, 0.020f, 0f), 3.1f, BoardCream);
    PlayRing = Pad(parent, "MMPlayRing", PlaySpotLocal + new Vector3(0f, 0.026f, 0f), 2.5f, Gold);
    Pad(parent, "MMPlayPad", PlaySpotLocal + new Vector3(0f, 0.032f, 0f), 2.1f,
      new Color(0.99f, 0.93f, 0.72f));
    GameObject signGo = new GameObject("MMPlaySign");
    signGo.transform.SetParent(parent, false);
    signGo.transform.localPosition = PlaySpotLocal + new Vector3(0f, 1.5f, 0f);
    WorldNameLabel sign = signGo.AddComponent<WorldNameLabel>();
    sign.Setup(DialogueLang.T("Come here!", "Vào đây!"), null, 0f);
    PlaySign = signGo;
  }

  void BuildBoard(Transform parent) {
    Box(parent, "MMBoardL", BoardPos + new Vector3(-1.25f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), FenceWood);
    Box(parent, "MMBoardR", BoardPos + new Vector3(1.25f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), FenceWood);
    GameObject panel = Box(parent, "MMBoardPanel", BoardPos + new Vector3(0f, 1.95f, 0f),
      new Vector3(2.6f, 1.7f, 0.12f), BoardCream);
    SetMaterial(panel, LitEmissive(BoardCream, 0.22f));
    int n = Mathf.Clamp(BoardPairs, 1, MaxPairs);
    NumberBoard = CountingGardenBuilder.Digit(parent, "MMNumberDigit",
      BoardPos + new Vector3(0f, 1.42f, -0.09f), 1.3f, 1.0f, Gold, 90f, n);
    SetMaterial(NumberBoard, LitEmissive(Gold, 0.5f));
    IgnoreFromBuild(NumberBoard);
  }

  void BuildResult(Transform parent) {
    int n = Mathf.Clamp(BoardPairs, 1, MaxPairs);
    GameObject result = new GameObject("MMResult");
    result.transform.SetParent(parent, false);
    result.transform.localPosition = ResultPos;
    Box(result.transform, "MMResultPost", new Vector3(0f, 0.65f, 0f),
      new Vector3(0.13f, 1.3f, 0.13f), FenceWood);
    GameObject resultFrame = Box(result.transform, "MMResultFrame", new Vector3(0f, 1.6f, 0f),
      new Vector3(1.2f, 1.05f, 0.12f), BoardCream);
    SetMaterial(resultFrame, LitEmissive(BoardCream, 0.18f));
    GameObject resultDigit = CountingGardenBuilder.Digit(result.transform, "MMResultDigit",
      new Vector3(0f, 1.2f, -0.09f), 0.6f, 0.45f, Gold, 90f, n);
    SetMaterial(resultDigit, LitEmissive(Gold, 0.45f));
    CountingGardenBuilder.CheckMark(result.transform, "MMResultCheck",
      new Vector3(0f, 1.82f, -0.09f), 0.3f, MintLeaf);
    result.SetActive(false);
    Result = result;
  }

  // REFERENCE AREA + PAIR AREA: one pedestal + one pair slot per pair, on a
  // clean aqua mat so the two areas read as the place where things belong.
  void BuildPairingArea(Transform parent) {
    Pad(parent, "MMPairMat", new Vector3(0f, 0.006f, 4.5f), 8.6f, new Color(0.80f, 0.88f, 0.92f));
    RefPedestals = new Transform[MaxPairs];
    for (int i = 0; i < MaxPairs; i++) {
      Cyl(parent, "MMRefPedestal" + i, RefSlotLocal[i] + new Vector3(0f, 0.22f, 0f),
        0.9f, 0.44f, StoneGrey);
      Cyl(parent, "MMRefTop" + i, RefSlotLocal[i] + new Vector3(0f, 0.45f, 0f),
        0.95f, 0.06f, Aqua);
      Pad(parent, "MMPairSlot" + i, PairSlotLocal[i] + new Vector3(0f, 0.012f, 0f),
        0.95f, new Color(0.98f, 0.86f, 0.55f));
      GameObject anchor = new GameObject("MMRefAnchor" + i);
      anchor.transform.SetParent(parent, false);
      anchor.transform.localPosition = RefSlotLocal[i] + new Vector3(0f, 0.48f, 0f);
      RefPedestals[i] = anchor.transform;
    }
    GameObject padAnchor = new GameObject("MMPadAnchor");
    padAnchor.transform.SetParent(parent, false);
    padAnchor.transform.localPosition = new Vector3(0f, 0f, 4.4f);
    PadAnchor = padAnchor.transform;
    Pad(parent, "MMPadZoneDisc", new Vector3(0f, 0.02f, 4.4f), 3.6f, new Color(0.90f, 0.82f, 0.62f));
  }

  void BuildSearchField(Transform parent) {
    Pad(parent, "MMFieldMat", new Vector3(0f, 0.005f, 0.5f), 10.5f, new Color(0.72f, 0.84f, 0.68f));
    for (int i = 0; i < CandidateSpotLocal.Length; i++) {
      Pad(parent, "MMSpot" + i, CandidateSpotLocal[i] + new Vector3(0f, 0.012f, 0f),
        1.05f, new Color(0.62f, 0.78f, 0.60f));
    }
  }

  void BuildReward(Transform parent) {
    // A small goal arch behind the pairing area + a bloom that lights at the end.
    Box(parent, "MMGoalL", new Vector3(-1.5f, 1.0f, RewardPos.z),
      new Vector3(0.18f, 2.0f, 0.18f), Aqua);
    Box(parent, "MMGoalR", new Vector3(1.5f, 1.0f, RewardPos.z),
      new Vector3(0.18f, 2.0f, 0.18f), Aqua);
    GameObject beam = Box(parent, "MMGoalBeam", new Vector3(0f, 2.05f, RewardPos.z),
      new Vector3(3.3f, 0.16f, 0.16f), WorldBeauty.BlossomPink);
    IgnoreFromBuild(beam);
    GameObject bloom = WorldBeauty.Ball(parent, "MMRewardBloom", RewardPos + new Vector3(0f, 2.4f, 0f),
      1.0f, WorldBeauty.BlossomPink);
    bloom.SetActive(false);
    RewardBloom = bloom;
    DemoJuice.AttachSpotlight(parent, "MMSpotlight", new Vector3(0f, 0.018f, 4.4f), 6.0f);
  }

  void BuildDressing(Transform parent) {
    Vector3[] trees = {
      new Vector3(-7.5f, 0f, -3.5f), new Vector3(7.5f, 0f, -3.5f),
      new Vector3(-10.5f, 0f, 8.5f), new Vector3(10.5f, 0f, 8.5f),
      new Vector3(-6f, 0f, 14.5f), new Vector3(6f, 0f, 14.5f),
    };
    float[] scales = { 0.62f, 0.62f, 0.7f, 0.7f, 0.7f, 0.7f };
    for (int i = 0; i < trees.Length; i++) {
      WorldBeauty.BlossomTree(parent, "MMBlossomTree" + i, trees[i], scales[i]);
      WorldBeauty.PetalCarpet(parent, "MMPetalCarpet" + i, trees[i], 1.5f * scales[i] + 0.5f);
    }
    Vector3[] drifts = {
      new Vector3(-6.2f, 0f, 2.6f), new Vector3(6.2f, 0f, 2.6f),
      new Vector3(-6.2f, 0f, -6f), new Vector3(6.2f, 0f, -6f),
    };
    for (int i = 0; i < drifts.Length; i++)
      WorldBeauty.FlowerDrift(parent, "MMFlowerDrift" + i, drifts[i], 1.2f);
    WorldBeauty.PetalFall(parent, "MMPetalFall", new Vector3(0f, 0f, -1f), 9f, 12, 51109);
    WorldBeauty.Butterfly(parent, "MMButterfly0", new Vector3(-4.6f, 0f, 6.2f), 2.2f, 0.1f,
      WorldBeauty.BlossomDeep, WorldBeauty.BlossomCream);
    WorldBeauty.Butterfly(parent, "MMButterfly1", new Vector3(4.6f, 0f, 6.4f), 2.2f, 0.6f,
      WorldBeauty.Lilac, WorldBeauty.BlossomPink);
  }

  void BuildCameras(Transform parent) {
    // A: the lesson — board + teacher + student + the reference row.
    CamTeaching = CamAnchor(parent, "MMCamA", new Vector3(0.6f, 2.6f, -2.4f));
    LookTeaching = CamAnchor(parent, "MMLookA", new Vector3(0.1f, 1.3f, 6.0f));
    // B: the search demo — student + candidate field + reference row behind.
    CamDemo = CamAnchor(parent, "MMCamB", new Vector3(0.3f, 3.2f, -4.4f));
    LookDemo = CamAnchor(parent, "MMLookB", new Vector3(0.1f, 0.8f, 1.4f));
    // C: the payoff — all pairs standing on the pairing mat.
    CamSuccess = CamAnchor(parent, "MMCamC", new Vector3(0.5f, 2.8f, -1.8f));
    LookSuccess = CamAnchor(parent, "MMLookC", new Vector3(0f, 0.9f, 4.6f));
  }

  static Transform CamAnchor(Transform parent, string name, Vector3 pos) {
    GameObject go = new GameObject(name);
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    return go.transform;
  }

  // ---- the item pool (3 families x 4 colours x reference + candidate) ------------------

  void BuildPool(Transform parent) {
    Items.Clear();
    List<int> fam = new List<int>();
    List<int> col = new List<int>();
    List<bool> isRef = new List<bool>();
    for (int f = 0; f < FamilyCount; f++) {
      for (int c = 0; c < ColorCount; c++) {
        GameObject refItem = BuildFamilyItem(parent, "MMRef_" + f + "_" + c, f, c);
        refItem.transform.localPosition = RefSlotLocal[0];
        refItem.SetActive(false);
        Items.Add(refItem); fam.Add(f); col.Add(c); isRef.Add(true);
        GameObject cand = BuildFamilyItem(parent, "MMCand_" + f + "_" + c, f, c);
        cand.transform.localPosition = CandidateSpotLocal[0];
        cand.SetActive(false);
        Items.Add(cand); fam.Add(f); col.Add(c); isRef.Add(false);
      }
    }
    ItemFamily = fam.ToArray();
    ItemColor = col.ToArray();
    ItemIsReference = isRef.ToArray();
  }

  GameObject BuildFamilyItem(Transform parent, string name, int family, int color) {
    GameObject root = new GameObject(name);
    root.transform.SetParent(parent, false);
    Color tint = Palette[Mathf.Clamp(color, 0, Palette.Length - 1)];
    if (family == 0) {
      // Ball: one clean sphere.
      GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      ball.name = name + "Ball";
      ball.transform.SetParent(root.transform, false);
      ball.transform.localPosition = new Vector3(0f, ItemSize * 0.5f, 0f);
      ball.transform.localScale = Vector3.one * ItemSize;
      ball.GetComponent<Renderer>().sharedMaterial = Lit(tint);
      StripCollider(ball);
      IgnoreFromBuild(ball);
    } else if (family == 1) {
      // Flower: stem + coloured blossom.
      GameObject stem = Cyl(root.transform, name + "Stem", new Vector3(0f, 0.22f, 0f),
        0.07f, 0.44f, new Color(0.30f, 0.62f, 0.28f));
      IgnoreFromBuild(stem);
      GameObject blossom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      blossom.name = name + "Blossom";
      blossom.transform.SetParent(root.transform, false);
      blossom.transform.localPosition = new Vector3(0f, 0.52f, 0f);
      blossom.transform.localScale = Vector3.one * 0.46f;
      blossom.GetComponent<Renderer>().sharedMaterial = Lit(tint);
      StripCollider(blossom);
      IgnoreFromBuild(blossom);
      GameObject heart = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      heart.name = name + "Heart";
      heart.transform.SetParent(root.transform, false);
      heart.transform.localPosition = new Vector3(0f, 0.60f, 0f);
      heart.transform.localScale = Vector3.one * 0.16f;
      heart.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
      StripCollider(heart);
      IgnoreFromBuild(heart);
    } else {
      // Block: one chunky cube.
      GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
      block.name = name + "Block";
      block.transform.SetParent(root.transform, false);
      block.transform.localPosition = new Vector3(0f, ItemSize * 0.5f, 0f);
      block.transform.localScale = Vector3.one * ItemSize;
      block.GetComponent<Renderer>().sharedMaterial = Lit(tint);
      StripCollider(block);
      IgnoreFromBuild(block);
    }
    // The whole item root is bake-ignored too: items MOVE (field -> pair slot),
    // so the walk surface must stay flat under them.
    IgnoreFromBuild(root);
    // CLICK DOOR (S3-P2Z17 journey finding): the visuals are collider-free, so
    // without a collider on the root the MatchItem : IClickTarget can never be
    // hit — the child could not pick anything (game #6 was click-dead). One
    // box per item, disabled while carried/returning (MatchItem does that).
    BoxCollider click = root.AddComponent<BoxCollider>();
    click.size = new Vector3(ItemSize * 1.15f, ItemSize * 1.15f, ItemSize * 1.15f);
    click.center = new Vector3(0f, ItemSize * 0.55f, 0f);
    return root;
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "MatchPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0.2f, 0f, 2.6f));
    a.Npc = a.EnsureSlot("NpcAnchor", TeacherStart);
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0.6f, 3.2f, -5.5f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0.2f, 1.1f, 3.0f));
    a.Prompt = a.EnsureSlot("PromptAnchor", new Vector3(0f, 1.7f, 4.5f));
    a.Feedback = a.EnsureSlot("FeedbackAnchor", new Vector3(0f, 1.2f, 4.4f));
    a.Reward = a.EnsureSlot("RewardAnchor", RewardPos);
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  // ---- helpers (same discipline as the earlier arena builders) -------------------------

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

  static GameObject Cyl(Transform parent, string name, Vector3 pos,
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
    } catch (Exception) { }
  }

  static void IgnoreFromBuild(GameObject go) {
    if (go == null) return;
    try {
      Unity.AI.Navigation.NavMeshModifier mod = go.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
      if (mod == null) mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (Exception) { }
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
