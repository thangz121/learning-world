// A_World/CountingGarden/RabbitPlayBuilder.cs — GAMEPLAY #3 "CHO THỎ ĂN ĐÚNG SỐ"
// (feed the rabbit the right number of carrots). The Counting Garden's carrot
// patch (zone 0) opens its OWN lazy scene (RabbitPlayScene) through the same
// shared micro slot as the two earlier arenas — built on demand, never at boot,
// never stacked, no second loader.
// The arena is a real place, not a UI on the ground: teacher + number board at
// the back, a carrot patch on the west, a rabbit hutch + feeding bowl on the
// east, a short walk between them (child legs, 20-60s rounds), plus count pips
// and a result board. One patch serves every target 1..9 (10 carrots: the
// target plus a spare for the gentle overshoot lesson).
// Firewall: presentation + the exit door only — no quest/bus/save here; the
// activity (RabbitFeed) is wired by GameInstaller like the two earlier games.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RabbitPlayBuilder : MonoBehaviour {
  public const string SceneName = "RabbitPlayScene";

  // Separate island west of the stair hill (Main 0, Math +60, garden +120,
  // counting play +180, stair play +240).
  public static readonly Vector3 WorldOffset = new Vector3(300f, 0f, 0f);
  public const float BoundX = 18f;
  public const float BoundZ = 18f;
  // Arrival spawn sits clear of the exit portal fire radius (J4 lesson: the
  // portal only arms after the child walks clear of it once).
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -11.5f);
  // Follow camera: north of the child, looking south INTO the arena (the
  // lesson stage faces north) — same reading direction as the two earlier
  // arenas.
  // S3-P2Z23 (user: "cao quá"): lowered from 4.1 so the bowl/patch read at eye
  // level instead of a steep top-down.
  public static readonly Vector3 FollowOffset = new Vector3(0f, 3.1f, -5.4f);

  public const string ObjectiveEn = "Feed the Bunny";
  public const string ObjectiveVi = "Cho Thỏ Ăn";

  // Targets live in 1..MaxTarget (brief: 1-9 on ONE patch, never 9 plots).
  public const int MaxTarget = 9;
  public const int Target = 3; // default/fallback target (reference round)
  public const int CarrotCount = 10; // the target plus a spare for the correction

  // The round's mission comes from the area's ladder (progression/CLI); the
  // boards stage that digit so the world always shows the mission.
  public int BoardTarget = Target;

  // S3-P2Z26 (user: random numbers + +/- within 10): the board shows the ROUND
  // EXPRESSION. kind: 0 = plain number, 1 = a+b, 2 = a-b. Defaults render the
  // plain BoardTarget digit, so every existing build/test path is untouched.
  public int QuestionKind;
  public int QuestionA = Target;
  public int QuestionB;

  public static int ClampTarget(int t) {
    if (t < 1) return 1;
    if (t > MaxTarget) return MaxTarget;
    return t;
  }

  static readonly Color Lawn = new Color(0.38f, 0.64f, 0.36f);
  static readonly Color Meadow = new Color(0.46f, 0.71f, 0.42f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);
  static readonly Color CourtyardSand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color StoneGrey = new Color(0.68f, 0.68f, 0.66f);
  static readonly Color BoardCream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color SoilBrown = new Color(0.45f, 0.32f, 0.20f);
  static readonly Color FenceWood = new Color(0.55f, 0.40f, 0.24f);
  static readonly Color CarrotOrange = new Color(0.95f, 0.55f, 0.15f);
  static readonly Color LeafGreen = new Color(0.30f, 0.65f, 0.28f);
  static readonly Color BunnyGrey = new Color(0.82f, 0.80f, 0.78f);
  static readonly Color BunnyWhite = new Color(0.96f, 0.95f, 0.93f);
  static readonly Color MintLeaf = new Color(0.70f, 0.90f, 0.72f);

  // ---- acting layout (child scale; entry z=-3, the board faces the child) ----
  public static readonly Vector3 BoardPos = new Vector3(0f, 0f, 7.6f);
  public static readonly Vector3 TeacherStart = new Vector3(-1.4f, 0f, 6.6f);
  public static readonly Vector3 StudentStart = new Vector3(0.7f, 0f, 5.7f);
  public static readonly Vector3 StudentReturn = new Vector3(1.6f, 0f, 5.4f);
  // The carrot patch (west) and the rabbit (east): a ~3m loop between them.
  public static readonly Vector3 PatchCenter = new Vector3(-1.6f, 0f, 3.0f);
  public static readonly Vector3 PatchStand = new Vector3(-1.5f, 0f, 1.55f);
  // S3-P2Z19 (user round: "đẩy bunny + bát ăn lên nóc hutch"): the bunny and
  // its bowl sit ON the hutch roof (roof top ~1.31m); the child feeds from the
  // ground beside the hutch, reaching up. The bowl is the counter now — fed
  // carrots visibly pile in it (the old pip board is gone).
  public const float RoofTopY = 1.34f;
  public static readonly Vector3 RabbitHome = new Vector3(3.35f, RoofTopY, 4.15f);
  public static readonly Vector3 BowlPos = new Vector3(3.35f, RoofTopY, 3.42f);
  public static readonly Vector3 FeedStand = new Vector3(2.25f, 0f, 2.55f);
  // The play spot: the child walks here first; the question is read on arrival
  // (same contract as the stair arena's marked circle).
  public static readonly Vector3 PlaySpotLocal = new Vector3(0.4f, 0f, 1.4f);
  public const float PlaySpotRadius = 1.7f;

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public GameObject NumberBoard { get; private set; } // target digit (pulses)
  public GameObject Result { get; private set; }      // target + tick (hidden)
  // S3-P2Z19: the marked play spot (the question is read only on arrival).
  public GameObject PlaySpot { get; private set; }
  public GameObject PlayRing { get; private set; }
  // S3-P2Z20: the floating "Come here!" sign (hidden as the child arrives so it
  // never blocks the lesson frame).
  public GameObject PlaySign { get; private set; }
  // S3-P2Z23: the "Nộp bài" (submit) bell at the work area; the child rings it
  // to turn the bowl's count in — right or wrong.
  // S3-P2Z24 (user round: "2 bảng tranh chỗ, chỗ nộp bài khó nhìn"): the old
  // spot (1.5, 2.95) sat right beside the result board — the two boards
  // physically overlapped. S3-P2Z32: the previous spot (-2.6, 0.6) sat BETWEEN
  // the follow camera and the carrot patch, so a click aimed at a patch carrot
  // hit the submit zone first — the child stood still (never picked) and stray
  // clicks rang the bell (journey finding). The bell now stands on its own
  // marked pad on the open plaza, SOUTH-WEST, clear of the patch sightline,
  // every board AND of the three camera rigs (z -2.4..-1.0, x 0.4..2.6).
  public Transform SubmitAnchor { get; private set; }
  public static readonly Vector3 SubmitLocal = new Vector3(-4.8f, 0f, -0.6f);
  public List<GameObject> Carrots { get; private set; } = new List<GameObject>();
  public Vector3[] CarrotHomes { get; private set; }
  public GameObject RabbitRoot { get; private set; }
  public GameObject RabbitHead { get; private set; }
  public GameObject RabbitEarL { get; private set; }
  public GameObject RabbitEarR { get; private set; }
  public GameObject RabbitBody { get; private set; }
  public Transform FeedAnchor { get; private set; } // the click/proximity door
  public Transform CamTeaching { get; private set; }
  public Transform LookTeaching { get; private set; }
  public Transform CamDemo { get; private set; }
  public Transform LookDemo { get; private set; }
  public Transform CamSuccess { get; private set; }
  public Transform LookSuccess { get; private set; }

  // Scene entry (GameInstaller calls this after the lazy load).
  public void Build() {
    BuildContent(transform);
    BuildQuizTiles(transform);
    // S3-P2Z17 journey bug: low decor render meshes bake into the NavMesh and
    // carved the plaza into islands (the child stood at (0, 0.9) and no click
    // could move them). The walkable surface here is the flat ground only, so
    // every decor renderer is excluded from the bake.
    IgnoreAllDecorExceptGround(transform, "RPGround");
    BuildNavMesh(transform);
  }

  // USER ROUND 2026-09-29 (round 2): THREE answer boards for the picture-
  // answer rounds — the child taps the board whose carrot count matches the
  // number on the big board. Hidden until a quiz round stages them. The pick
  // face keeps a real collider (the SAME click system as the carrots).
  public RabbitAnswerTile[] QuizTiles { get; private set; }

  void BuildQuizTiles(Transform parent) {
    QuizTiles = new RabbitAnswerTile[3];
    for (int i = 0; i < 3; i++) {
      Vector3 p = new Vector3(-2.6f + i * 2.6f, 0f, PlaySpotLocal.z + 2.4f);
      GameObject root = new GameObject("RPAnswerTile" + i);
      root.transform.SetParent(parent, false);
      root.transform.localPosition = p;
      Pad(root.transform, "RPAnswerPad" + i, new Vector3(0f, 0.02f, 0f), 2.1f,
        i == 1 ? MintLeaf : BoardCream);
      Box(root.transform, "RPAnswerPostL" + i, new Vector3(-0.72f, 0.62f, 0f),
        new Vector3(0.12f, 1.24f, 0.12f), FenceWood);
      Box(root.transform, "RPAnswerPostR" + i, new Vector3(0.72f, 0.62f, 0f),
        new Vector3(0.12f, 1.24f, 0.12f), FenceWood);
      GameObject board = Box(root.transform, "RPAnswerBoard" + i, new Vector3(0f, 1.32f, 0f),
        new Vector3(1.5f, 1.0f, 0.12f), BoardCream);
      SetMaterial(board, LitEmissive(BoardCream, 0.18f));
      GameObject icons = new GameObject("RPAnswerIcons" + i);
      icons.transform.SetParent(root.transform, false);
      icons.transform.localPosition = new Vector3(0f, 1.32f, -0.10f);
      for (int k = 0; k < 9; k++) {
        int col = k % 3, row = k / 3;
        GameObject slot = new GameObject("Icon" + k);
        slot.transform.SetParent(icons.transform, false);
        slot.transform.localPosition = new Vector3((col - 1) * 0.34f, (1 - row) * 0.26f, 0f);
        GameObject carrotRoot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        carrotRoot.name = "CarrotRoot";
        carrotRoot.transform.SetParent(slot.transform, false);
        carrotRoot.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        carrotRoot.transform.localScale = new Vector3(0.10f, 0.09f, 0.10f);
        carrotRoot.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.95f, 0.52f, 0.16f));
        StripCollider(carrotRoot);
        GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        leaf.name = "CarrotLeaf";
        leaf.transform.SetParent(slot.transform, false);
        leaf.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        leaf.transform.localScale = new Vector3(0.10f, 0.07f, 0.10f);
        leaf.GetComponent<Renderer>().sharedMaterial = Lit(MintLeaf);
        StripCollider(leaf);
      }
      // The tap face: a proud pick board with a REAL collider (Box strips
      // colliders, so this one is built raw); the bake never sees it.
      GameObject pick = GameObject.CreatePrimitive(PrimitiveType.Cube);
      pick.name = "RPAnswerPick" + i;
      pick.transform.SetParent(root.transform, false);
      pick.transform.localPosition = new Vector3(0f, 1.32f, -0.17f);
      pick.transform.localScale = new Vector3(1.62f, 1.12f, 0.06f);
      pick.GetComponent<Renderer>().sharedMaterial = Lit(new Color(1f, 0.98f, 0.92f));
      IgnoreFromBuild(pick);
      RabbitAnswerTile tile = root.AddComponent<RabbitAnswerTile>();
      tile.BindIcons(icons.transform);
      tile.SetCount(1);
      root.SetActive(false);
      QuizTiles[i] = tile;
    }
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
    BuildPatch(root);
    BuildRabbit(root);
    BuildCountAndResult(root);
    BuildSubmit(root);
    BuildCameras(root);
    BuildAnchors(root);
    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  void BuildGround(Transform parent) {
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "RPRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(40f, 1.4f, 40f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    StripCollider(rim);
    IgnoreFromBuild(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "RPGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localPosition = Vector3.zero;
    ground.transform.localScale = new Vector3(3.8f, 1f, 3.8f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    MeadowPatch(parent, "RPMeadowW", new Vector3(-9f, 0f, 4f), 11f, 9f);
    MeadowPatch(parent, "RPMeadowE", new Vector3(9f, 0f, 4f), 11f, 9f);
    float[] angles = { 15f, 45f, 75f, 105f, 135f, 160f, 200f, 225f, 255f, 285f, 315f, 345f };
    for (int i = 0; i < angles.Length; i++) {
      float rad = angles[i] * Mathf.Deg2Rad;
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "RPHedge" + i;
      bush.transform.SetParent(parent, false);
      bush.transform.localPosition = new Vector3(Mathf.Sin(rad) * 15.5f, 0.55f, 3f + Mathf.Cos(rad) * 15.5f);
      bush.transform.localScale = (i % 2 == 0) ? new Vector3(3.4f, 2.2f, 3.4f) : new Vector3(2.8f, 1.9f, 2.8f);
      bush.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.28f, 0.58f, 0.34f));
      StripCollider(bush);
      IgnoreFromBuild(bush);
      WorldBeauty.HedgeBloom(parent, "RPHedgeBloom" + i, bush.transform.localPosition);
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
    Box(parent, "RPEntryPostL", new Vector3(-1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    Box(parent, "RPEntryPostR", new Vector3(1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    GameObject beam = Box(parent, "RPEntryBeam", new Vector3(0f, 2.06f, -9.5f),
      new Vector3(3.4f, 0.16f, 0.16f), WorldBeauty.BlossomPink);
    IgnoreFromBuild(beam);
    WorldBeauty.Ball(parent, "RPEntryBlossom0", new Vector3(0f, 2.42f, -9.5f), 1.1f,
      WorldBeauty.BlossomPink);
    Pad(parent, "RPThresholdL", new Vector3(-1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "RPThresholdR", new Vector3(1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "RPExitDisc", new Vector3(0f, 0.01f, -11.1f), 3.2f, WorldBeauty.Petal);
    GameObject exitGo = new GameObject("RPExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.PlayExit = true; // -> back to the Counting Garden (not the Math hub)
    exit.fireRadius = 1.35f;
    exit.areaId = CountingGardenArea.AreaId;
    ExitPortal = exit;
    Box(parent, "RPExitPostL", new Vector3(-1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Box(parent, "RPExitPostR", new Vector3(1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Sphere(parent, "RPExitCapL", new Vector3(-1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
    Sphere(parent, "RPExitCapR", new Vector3(1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
  }

  void BuildPaths(Transform parent) {
    Pad(parent, "RPPlazaPad", new Vector3(0f, 0.004f, 0f), 7.0f, CourtyardSand);
    Seg(parent, "RPPathEntry", new Vector3(0f, 0f, -10.4f), new Vector3(0f, 0f, -1.0f), 1.7f);
    Seg(parent, "RPPathStage", new Vector3(0f, 0f, -1.0f), new Vector3(0f, 0f, 8.2f), 1.7f);
    // Spurs to the patch (west) and the rabbit (east) — the walk loop.
    Seg(parent, "RPPathPatch", new Vector3(0f, 0f, 1.6f), new Vector3(-1.5f, 0f, 1.6f), 1.4f);
    Seg(parent, "RPPathRabbit", new Vector3(0f, 0f, 2.2f), new Vector3(1.5f, 0f, 2.2f), 1.4f);
  }

  void BuildDressing(Transform parent) {
    // Trees stay OFF every walk line (entry/stage/patch/rabbit corridors).
    Vector3[] trees = {
      new Vector3(-6.5f, 0f, -3.5f), new Vector3(6.5f, 0f, -3.5f),
      new Vector3(-10f, 0f, 8f), new Vector3(10f, 0f, 8f),
      new Vector3(-5.5f, 0f, 15.5f), new Vector3(5.5f, 0f, 15.5f),
    };
    float[] scales = { 0.62f, 0.62f, 0.7f, 0.7f, 0.7f, 0.7f };
    for (int i = 0; i < trees.Length; i++) {
      WorldBeauty.BlossomTree(parent, "RPBlossomTree" + i, trees[i], scales[i]);
      WorldBeauty.PetalCarpet(parent, "RPPetalCarpet" + i, trees[i], 1.5f * scales[i] + 0.5f);
    }
    Vector3[] drifts = {
      new Vector3(-4.4f, 0f, 4.6f), new Vector3(4.6f, 0f, 5.0f),
      new Vector3(-4.6f, 0f, -6f), new Vector3(4.6f, 0f, -6f),
    };
    for (int i = 0; i < drifts.Length; i++)
      WorldBeauty.FlowerDrift(parent, "RPFlowerDrift" + i, drifts[i], 1.2f);
    WorldBeauty.PetalFall(parent, "RPPetalFall", new Vector3(0f, 0f, -1f), 9f, 12, 51109);
    WorldBeauty.Butterfly(parent, "RPButterfly0", new Vector3(-3.4f, 0f, 4.6f), 2.2f, 0.1f,
      WorldBeauty.BlossomDeep, WorldBeauty.BlossomCream);
    WorldBeauty.Butterfly(parent, "RPButterfly1", new Vector3(3.4f, 0f, 5.0f), 2.2f, 0.6f,
      WorldBeauty.Lilac, WorldBeauty.BlossomPink);
  }

  void BuildBoard(Transform parent) {
    // Board "N" (target): emissive so the number is obvious from every angle.
    Box(parent, "RPBoardL", BoardPos + new Vector3(-1.25f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), FenceWood);
    Box(parent, "RPBoardR", BoardPos + new Vector3(1.25f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), FenceWood);
    GameObject panel = Box(parent, "RPBoardPanel", BoardPos + new Vector3(0f, 1.95f, 0f),
      new Vector3(2.6f, 1.7f, 0.12f), BoardCream);
    SetMaterial(panel, LitEmissive(BoardCream, 0.22f));
    int qa = QuestionKind == 0 ? BoardTarget : QuestionA;
    BuildQuestion(parent, QuestionKind, qa, QuestionB, -1);
  }

  // The question group ("RPQuestion"): a single digit for a plain number, or
  // two digits + an operator for a+b / a-b. NumberBoard points at the group so
  // the game can pulse/scale it through the number-swap animation.
  // S3-P2Z27 (user: "nếu đúng thì trên bảng hiện 4-2=2"): when result >= 0 the
  // group renders the SOLVED equation (a op b = r) in a tighter 5-glyph layout.
  void BuildQuestion(Transform parent, int kind, int a, int b, int result) {
    GameObject group = new GameObject("RPQuestion");
    group.transform.SetParent(parent, false);
    group.transform.localPosition = BoardPos + new Vector3(0f, 1.42f, -0.09f);
    PopulateQuestion(group.transform, kind, a, b, result);
    NumberBoard = group;
  }

  void PopulateQuestion(Transform group, int kind, int a, int b, int result) {
    a = ClampTarget(a);
    b = ClampTarget(b);
    if (kind == 0) {
      GameObject d = CountingGardenBuilder.Digit(group, "RPNumberDigit",
        Vector3.zero, 1.3f, 1.0f, Gold, 90f, a);
      SetMaterial(d, LitEmissive(Gold, 0.5f));
      return;
    }
    bool solved = result >= 0;
    if (!solved) {
      const float dh = 1.05f;
      const float dw = 0.68f;
      DigitAt(group, "RPNumberDigitA", -0.72f, dh, dw, a);
      DigitAt(group, "RPNumberDigitB", 0.72f, dh, dw, b);
      Operator(group, kind, 0f, dh * 0.5f, 0.5f);
      return;
    }
    // Solved: a op b = r, five evenly spaced glyphs.
    const float sh = 0.8f;
    const float sw = 0.36f;
    float oy = sh * 0.5f;
    DigitAt(group, "RPNumberDigitA", -1.0f, sh, sw, a);
    Operator(group, kind, -0.5f, oy, 0.36f);
    DigitAt(group, "RPNumberDigitB", 0f, sh, sw, b);
    Equals(group, 0.5f, oy, 0.36f);
    DigitAt(group, "RPNumberDigitR", 1.0f, sh, sw, result);
  }

  void DigitAt(Transform group, string name, float x, float h, float w, int n) {
    GameObject d = CountingGardenBuilder.Digit(group, name, new Vector3(x, 0f, 0f),
      h, w, Gold, 90f, ClampTarget(n));
    SetMaterial(d, LitEmissive(Gold, 0.5f));
  }

  static void Operator(Transform group, int kind, float x, float y, float len) {
    Box(group, "RPQuestionOpH", new Vector3(x, y, 0f), new Vector3(len, 0.12f, 0.12f), Gold);
    if (kind == 1) {
      Box(group, "RPQuestionOpV", new Vector3(x, y, 0f), new Vector3(0.12f, len, 0.12f), Gold);
    }
  }

  static void Equals(Transform group, float x, float y, float len) {
    Box(group, "RPQuestionEq0", new Vector3(x, y - 0.1f, 0f), new Vector3(len, 0.12f, 0.12f), Gold);
    Box(group, "RPQuestionEq1", new Vector3(x, y + 0.1f, 0f), new Vector3(len, 0.12f, 0.12f), Gold);
  }

  // S3-P2Z25/26/27 (user: after a correct submit the NEXT question appears with
  // an animation; a correct +/- round shows its solved equation): re-spawn the
  // board question at runtime. result >= 0 renders "a op b = result".
  public GameObject SpawnQuestion(int kind, int a, int b, int result = -1) {
    Transform old = transform.Find("RPQuestion");
    if (old != null) {
      try { CharacterPresentation.DestroyNow(old.gameObject); } catch (System.Exception) { }
    }
    BuildQuestion(transform, kind, a, b, result);
    return NumberBoard;
  }

  // The result board's digit, rebuilt when the target advances so the next
  // payoff shows the right number.
  public GameObject SpawnResultDigit(int n) {
    if (Result == null) return null;
    Transform old = Result.transform.Find("RPResultDigit");
    if (old != null) {
      try { CharacterPresentation.DestroyNow(old.gameObject); } catch (System.Exception) { }
    }
    GameObject d = CountingGardenBuilder.Digit(Result.transform, "RPResultDigit",
      new Vector3(0f, 1.2f, -0.09f), 0.6f, 0.45f, Gold, 90f, ClampTarget(n));
    SetMaterial(d, LitEmissive(Gold, 0.45f));
    return d;
  }

  void BuildPatch(Transform parent) {
    // Soil bed (walkable pad, thin) + low wooden rim on the far sides only —
    // the south side stays open where the child stands to pick.
    GameObject soil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    soil.name = "RPPatchSoil";
    soil.transform.SetParent(parent, false);
    soil.transform.localPosition = new Vector3(PatchCenter.x, 0.006f, PatchCenter.z);
    soil.transform.localScale = new Vector3(4.6f, 0.012f, 3.4f);
    soil.GetComponent<Renderer>().sharedMaterial = Lit(SoilBrown);
    StripCollider(soil);
    Box(parent, "RPPatchRimW", PatchCenter + new Vector3(-2.2f, 0.12f, 0f),
      new Vector3(0.16f, 0.24f, 3.2f), FenceWood);
    Box(parent, "RPPatchRimN", PatchCenter + new Vector3(0f, 0.12f, 1.6f),
      new Vector3(4.4f, 0.24f, 0.16f), FenceWood);
    // A small sign: this is the carrot corner (a real carrot marker now —
    // S3-P2Z19 user round: the old orange-egg props were "quá xấu").
    Box(parent, "RPPatchSignPost", PatchCenter + new Vector3(-1.9f, 0.5f, -1.3f),
      new Vector3(0.12f, 1.0f, 0.12f), FenceWood);
    PropKit.Place(parent, "carrot", PatchCenter + new Vector3(-1.9f, 1.12f, -1.3f), 30f, 1.4f);

    // Ten carrots in a natural cluster (find-and-choose, never a test row).
    Vector3[] offs = {
      new Vector3(-0.9f, 0f, 0.5f), new Vector3(-0.35f, 0f, 0.15f),
      new Vector3(0.1f, 0f, 0.55f), new Vector3(0.55f, 0f, 0.1f),
      new Vector3(0.9f, 0f, -0.3f), new Vector3(-0.6f, 0f, -0.5f),
      new Vector3(0.3f, 0f, -0.6f), new Vector3(-1.15f, 0f, -0.1f),
      new Vector3(0.75f, 0f, 0.75f), new Vector3(-0.1f, 0f, 0.95f),
    };
    Carrots.Clear();
    CarrotHomes = new Vector3[CarrotCount];
    for (int i = 0; i < CarrotCount; i++) {
      // S3-P2Z19 (user: "hình ảnh carrot quá xấu"): the arena now uses the
      // SAME Kenney carrot.fbx the garden beds use — the old sphere-egg
      // primitive is gone. The root stays the click/flight handle.
      Vector3 home = new Vector3(PatchCenter.x + offs[i].x, 0.015f, PatchCenter.z + offs[i].z);
      CarrotHomes[i] = home;
      GameObject carrot = new GameObject("RPCarrot" + i);
      carrot.transform.SetParent(parent, false);
      carrot.transform.localPosition = home;
      carrot.transform.localRotation = Quaternion.Euler(0f, (i * 47f) % 360f, 0f);
      PropKit.Place(carrot.transform, "carrot", Vector3.zero, 0f, 1.0f);
      Carrots.Add(carrot);
    }
  }

  void BuildRabbit(Transform parent) {
    // Hutch: a little shelter at the back (east), roof slanted, bake-safe —
    // solid boxes sit OFF the walk lines and the bake reads around them.
    Box(parent, "RPHutchBack", new Vector3(3.6f, 0.55f, 4.4f),
      new Vector3(1.5f, 1.1f, 0.18f), FenceWood);
    Box(parent, "RPHutchSideL", new Vector3(2.9f, 0.55f, 3.9f),
      new Vector3(0.18f, 1.1f, 1.1f), FenceWood);
    Box(parent, "RPHutchSideR", new Vector3(4.3f, 0.55f, 3.9f),
      new Vector3(0.18f, 1.1f, 1.1f), FenceWood);
    GameObject roof = Box(parent, "RPHutchRoof", new Vector3(3.6f, 1.25f, 4.0f),
      new Vector3(1.9f, 0.12f, 1.5f), WorldBeauty.BlossomPink);
    roof.transform.localRotation = Quaternion.Euler(0f, 0f, 6f);
    IgnoreFromBuild(roof);
    // Grass ring: this corner is the rabbit's garden.
    Pad(parent, "RPRabbitGrass", new Vector3(2.6f, 0.004f, 3.5f), 4.2f, MintLeaf);
    // Feeding bowl (the mouth target + the click/proximity door anchor).
    GameObject bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    bowl.name = "RPFeedBowl";
    bowl.transform.SetParent(parent, false);
    bowl.transform.localPosition = new Vector3(BowlPos.x, BowlPos.y, BowlPos.z);
    // S3-P2Z20: shallower dish so the fed carrots stand clearly on top (+0.13).
    bowl.transform.localScale = new Vector3(0.95f, 0.10f, 0.95f);
    bowl.GetComponent<Renderer>().sharedMaterial = Lit(WorldBeauty.BlossomCream);
    StripCollider(bowl);
    GameObject bowlRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    bowlRim.name = "RPFeedBowlRim";
    bowlRim.transform.SetParent(parent, false);
    bowlRim.transform.localPosition = new Vector3(BowlPos.x, BowlPos.y - 0.04f, BowlPos.z);
    bowlRim.transform.localScale = new Vector3(1.15f, 0.06f, 1.15f);
    bowlRim.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
    StripCollider(bowlRim);
    FeedAnchor = bowl.transform;

    // The rabbit: body + head + long ears + eyes + tail, facing the bowl
    // (south, toward the child). All parts collider-free; the game drives the
    // nibble (head bob + ear wiggle + squash) procedurally.
    GameObject rabbit = new GameObject("RPRabbit");
    rabbit.transform.SetParent(parent, false);
    rabbit.transform.localPosition = RabbitHome;
    rabbit.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0f, -1f));
    RabbitRoot = rabbit;
    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    body.name = "RPBody";
    body.transform.SetParent(rabbit.transform, false);
    body.transform.localPosition = new Vector3(0f, 0.32f, 0.1f);
    body.transform.localScale = new Vector3(0.5f, 0.44f, 0.62f);
    body.GetComponent<Renderer>().sharedMaterial = Lit(BunnyGrey);
    StripCollider(body);
    RabbitBody = body;
    GameObject head = new GameObject("RPHead");
    head.transform.SetParent(rabbit.transform, false);
    head.transform.localPosition = new Vector3(0f, 0.62f, -0.28f);
    RabbitHead = head;
    GameObject skull = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    skull.name = "RPSkull";
    skull.transform.SetParent(head.transform, false);
    skull.transform.localPosition = Vector3.zero;
    skull.transform.localScale = new Vector3(0.34f, 0.32f, 0.32f);
    skull.GetComponent<Renderer>().sharedMaterial = Lit(BunnyWhite);
    StripCollider(skull);
    GameObject eyeL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    eyeL.name = "RPEyeL";
    eyeL.transform.SetParent(head.transform, false);
    eyeL.transform.localPosition = new Vector3(-0.1f, 0.06f, -0.13f);
    eyeL.transform.localScale = new Vector3(0.06f, 0.07f, 0.05f);
    eyeL.GetComponent<Renderer>().sharedMaterial = Lit(Color.black);
    StripCollider(eyeL);
    IgnoreFromBuild(eyeL);
    GameObject eyeR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    eyeR.name = "RPEyeR";
    eyeR.transform.SetParent(head.transform, false);
    eyeR.transform.localPosition = new Vector3(0.1f, 0.06f, -0.13f);
    eyeR.transform.localScale = new Vector3(0.06f, 0.07f, 0.05f);
    eyeR.GetComponent<Renderer>().sharedMaterial = Lit(Color.black);
    StripCollider(eyeR);
    IgnoreFromBuild(eyeR);
    GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    nose.name = "RPNose";
    nose.transform.SetParent(head.transform, false);
    nose.transform.localPosition = new Vector3(0f, -0.02f, -0.17f);
    nose.transform.localScale = new Vector3(0.06f, 0.05f, 0.05f);
    nose.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.95f, 0.55f, 0.60f));
    StripCollider(nose);
    IgnoreFromBuild(nose);
    GameObject cheekL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    cheekL.name = "RPCheekL";
    cheekL.transform.SetParent(head.transform, false);
    cheekL.transform.localPosition = new Vector3(-0.12f, -0.04f, -0.1f);
    cheekL.transform.localScale = new Vector3(0.07f, 0.05f, 0.04f);
    cheekL.GetComponent<Renderer>().sharedMaterial = Lit(WorldBeauty.BlossomDeep);
    StripCollider(cheekL);
    IgnoreFromBuild(cheekL);
    GameObject cheekR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    cheekR.name = "RPCheekR";
    cheekR.transform.SetParent(head.transform, false);
    cheekR.transform.localPosition = new Vector3(0.12f, -0.04f, -0.1f);
    cheekR.transform.localScale = new Vector3(0.07f, 0.05f, 0.04f);
    cheekR.GetComponent<Renderer>().sharedMaterial = Lit(WorldBeauty.BlossomDeep);
    StripCollider(cheekR);
    IgnoreFromBuild(cheekR);
    GameObject bowL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    bowL.name = "RPBowL";
    bowL.transform.SetParent(head.transform, false);
    bowL.transform.localPosition = new Vector3(-0.07f, 0.14f, 0.06f);
    bowL.transform.localScale = new Vector3(0.1f, 0.08f, 0.06f);
    bowL.GetComponent<Renderer>().sharedMaterial = Lit(WorldBeauty.BlossomDeep);
    StripCollider(bowL);
    IgnoreFromBuild(bowL);
    GameObject bowR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    bowR.name = "RPBowR";
    bowR.transform.SetParent(head.transform, false);
    bowR.transform.localPosition = new Vector3(0.07f, 0.14f, 0.06f);
    bowR.transform.localScale = new Vector3(0.1f, 0.08f, 0.06f);
    bowR.GetComponent<Renderer>().sharedMaterial = Lit(WorldBeauty.BlossomDeep);
    StripCollider(bowR);
    IgnoreFromBuild(bowR);
    RabbitEarL = MakeEar(rabbit.transform, "RPEarL", new Vector3(-0.1f, 0.95f, -0.28f), -10f);
    RabbitEarR = MakeEar(rabbit.transform, "RPEarR", new Vector3(0.1f, 0.95f, -0.28f), 10f);
    GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    tail.name = "RPTail";
    tail.transform.SetParent(rabbit.transform, false);
    tail.transform.localPosition = new Vector3(0f, 0.35f, 0.45f);
    tail.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
    tail.GetComponent<Renderer>().sharedMaterial = Lit(BunnyWhite);
    StripCollider(tail);
    // A low picket arc marks the rabbit's garden without blocking the feed
    // stand (south side stays open).
    for (int i = 0; i < 4; i++) {
      float t = (i - 1.5f) * 0.8f;
      Box(parent, "RPRabbitFence" + i, new Vector3(2.6f + t, 0.2f, 4.9f),
        new Vector3(0.12f, 0.4f, 0.12f), FenceWood);
    }
  }

  GameObject MakeEar(Transform rabbit, string name, Vector3 pos, float tilt) {
    GameObject ear = new GameObject(name);
    ear.transform.SetParent(rabbit, false);
    ear.transform.localPosition = pos;
    ear.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
    GameObject outer = GameObject.CreatePrimitive(PrimitiveType.Cube);
    outer.name = name + "Outer";
    outer.transform.SetParent(ear.transform, false);
    outer.transform.localPosition = new Vector3(0f, 0.22f, 0f);
    outer.transform.localScale = new Vector3(0.13f, 0.62f, 0.08f);
    outer.GetComponent<Renderer>().sharedMaterial = Lit(BunnyGrey);
    StripCollider(outer);
    IgnoreFromBuild(outer);
    GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Cube);
    inner.name = name + "Inner";
    inner.transform.SetParent(ear.transform, false);
    inner.transform.localPosition = new Vector3(0f, 0.2f, -0.02f);
    inner.transform.localScale = new Vector3(0.07f, 0.42f, 0.035f);
    inner.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.95f, 0.65f, 0.68f));
    StripCollider(inner);
    IgnoreFromBuild(inner);
    return ear;
  }

  void BuildCountAndResult(Transform parent) {
    // S3-P2Z19 (user round: "2 cái biển lặp — xóa một"): the 3x3 pip board is
    // GONE (it read as a second number board beside the target board). The
    // count now reads from the BOWL itself: every fed carrot visibly piles in
    // it (RabbitFeed parks them in BowlSlot order).
    //
    // The marked play spot: the child walks here first; the question is read on
    // arrival (same contract as the stair arena's circle). Gold ring + cream
    // pad + a floating "Come here!" sign.
    GameObject spot = new GameObject("RPPlaySpot");
    spot.transform.SetParent(parent, false);
    spot.transform.localPosition = PlaySpotLocal;
    PlaySpot = spot;
    Pad(parent, "RPPlayEdge", PlaySpotLocal + new Vector3(0f, 0.020f, 0f), 3.1f, BoardCream);
    PlayRing = Pad(parent, "RPPlayRing", PlaySpotLocal + new Vector3(0f, 0.026f, 0f), 2.5f, Gold);
    Pad(parent, "RPPlayPad", PlaySpotLocal + new Vector3(0f, 0.032f, 0f), 2.1f,
      new Color(0.99f, 0.93f, 0.72f));
    GameObject signGo = new GameObject("RPPlaySign");
    signGo.transform.SetParent(parent, false);
    signGo.transform.localPosition = PlaySpotLocal + new Vector3(0f, 1.5f, 0f);
    // PHASE 4b (user order: pre-readers cannot read): the old floating
    // "Come here!/Vào đây!" label is a WORDLESS gold arrow pointing down at
    // the pad (RabbitFeed still toggles PlaySign.activeSelf as before).
    Box(signGo.transform, "RPPlaySignStem", new Vector3(0f, 0.2f, 0f),
      new Vector3(0.11f, 0.34f, 0.11f), Gold);
    GameObject headA = Box(signGo.transform, "RPPlaySignHeadA",
      new Vector3(-0.12f, -0.06f, 0f), new Vector3(0.11f, 0.34f, 0.11f), Gold);
    headA.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    GameObject headB = Box(signGo.transform, "RPPlaySignHeadB",
      new Vector3(0.12f, -0.06f, 0f), new Vector3(0.11f, 0.34f, 0.11f), Gold);
    headB.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
    PlaySign = signGo;

    // Result board ("N + tick"): LEFT of the bowl, on the payoff axis. S3-P2Z18
    // user round: the old right-front spot sat beside the success camera (the
    // shot cropped the board at the bottom corner); left of the bowl the payoff
    // frame reads child+bowl -> board with the board facing the child.
    int n = ClampTarget(BoardTarget);
    GameObject result = new GameObject("RPResult");
    result.transform.SetParent(parent, false);
    result.transform.localPosition = new Vector3(0.9f, 0f, 3.3f);
    Box(result.transform, "RPResultPost", new Vector3(0f, 0.65f, 0f),
      new Vector3(0.13f, 1.3f, 0.13f), FenceWood);
    GameObject resultFrame = Box(result.transform, "RPResultFrame", new Vector3(0f, 1.6f, 0f),
      new Vector3(1.2f, 1.05f, 0.12f), BoardCream);
    SetMaterial(resultFrame, LitEmissive(BoardCream, 0.18f));
    GameObject resultDigit = CountingGardenBuilder.Digit(result.transform, "RPResultDigit",
      new Vector3(0f, 1.2f, -0.09f), 0.6f, 0.45f, Gold, 90f, n);
    SetMaterial(resultDigit, LitEmissive(Gold, 0.45f));
    CountingGardenBuilder.CheckMark(result.transform, "RPResultCheck",
      new Vector3(0f, 1.82f, -0.09f), 0.3f, MintLeaf);
    result.SetActive(false);
    Result = result;
    DemoJuice.AttachSpotlight(parent, "RPGameSpotlight", new Vector3(0.4f, 0.018f, 3.2f), 5.4f);
  }

  // S3-P2Z23: the submit bell (user: "có cơ chế nộp bài"). A wooden post + a
  // gold bell + a small standing sign; the child rings it to turn the bowl's
  // count in — the bowl can be too few, exact, or too many.
  // S3-P2Z24: enlarged + moved to its own gold-ringed pad on the open plaza so
  // the station reads clearly (user: "2 bảng tranh nhau chỗ đứng", "chỗ nộp bài
  // khó nhìn"). Name "RPSubmitRing" is the pad the games/tests pin.
  void BuildSubmit(Transform parent) {
    Vector3 p = SubmitLocal;
    Pad(parent, "RPSubmitRing", new Vector3(p.x, 0.014f, p.z), 2.0f, Gold);
    Pad(parent, "RPSubmitPad", new Vector3(p.x, 0.020f, p.z), 1.5f, MintLeaf);
    GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    post.name = "RPSubmitPost";
    post.transform.SetParent(parent, false);
    post.transform.localPosition = new Vector3(p.x, 0.62f, p.z);
    post.transform.localScale = new Vector3(0.16f, 0.62f, 0.16f);
    post.GetComponent<Renderer>().sharedMaterial = Lit(FenceWood);
    StripCollider(post);
    IgnoreFromBuild(post);
    GameObject bell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    bell.name = "RPSubmitBell";
    bell.transform.SetParent(parent, false);
    bell.transform.localPosition = new Vector3(p.x, 1.42f, p.z);
    bell.transform.localScale = new Vector3(0.46f, 0.46f, 0.46f);
    bell.GetComponent<Renderer>().sharedMaterial = LitEmissive(Gold, 0.25f);
    StripCollider(bell);
    IgnoreFromBuild(bell);
    GameObject board = Box(parent, "RPSubmitSign", new Vector3(p.x, 1.95f, p.z),
      new Vector3(1.1f, 0.5f, 0.08f), BoardCream);
    SetMaterial(board, LitEmissive(BoardCream, 0.16f));
    IgnoreFromBuild(board);
    // PHASE 4b (user order: no words for pre-readers): the old floating
    // "Submit/Nộp bài" text label is gone — the golden bell + the pad are the
    // wordless submit cue.
    GameObject anchor = new GameObject("RPSubmitAnchor");
    anchor.transform.SetParent(parent, false);
    anchor.transform.localPosition = p;
    SubmitAnchor = anchor.transform;
  }

  void BuildCameras(Transform parent) {
    // A: the lesson (board + teacher + student + patch glimpse).
    CamTeaching = CamAnchor(parent, "RPCamA", new Vector3(0.5f, 2.3f, -1.6f));
    LookTeaching = CamAnchor(parent, "RPLookA", new Vector3(0.2f, 1.5f, 6.6f));
    // B: the demo (patch + student + rabbit, one wide frame).
    CamDemo = CamAnchor(parent, "RPCamB", new Vector3(0.4f, 2.7f, -2.4f));
    LookDemo = CamAnchor(parent, "RPLookB", new Vector3(0.4f, 0.7f, 3.4f));
    // C: the payoff (player + roof bunny/bowl + board + result). S3-P2Z19:
    // raised to hold the roof-level bowl (the counter) in the frame.
    CamSuccess = CamAnchor(parent, "RPCamC", new Vector3(2.6f, 2.3f, -1.0f));
    LookSuccess = CamAnchor(parent, "RPLookC", new Vector3(2.4f, 1.5f, 3.6f));
  }

  static Transform CamAnchor(Transform parent, string name, Vector3 pos) {
    GameObject go = new GameObject(name);
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    return go.transform;
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "RabbitPlayPresentationRoot");
    Anchors = a;
    if (a == null) return;
    // Child-height arrival: the field (patch + rabbit + board), not a map.
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0.4f, 0f, 3.2f));
    a.Npc = a.EnsureSlot("NpcAnchor", TeacherStart);
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0.6f, 3.2f, -5.5f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0.2f, 1.1f, 3.0f));
    a.Prompt = a.EnsureSlot("PromptAnchor", new Vector3(0.4f, 1.7f, 3.2f));
    a.Feedback = a.EnsureSlot("FeedbackAnchor", new Vector3(2.3f, 1.2f, 3.2f));
    a.Reward = a.EnsureSlot("RewardAnchor", BowlPos);
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  // ---- helpers (same discipline as the two earlier arena builders) --------------

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
      Unity.AI.Navigation.NavMeshModifier mod = go.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
      if (mod == null) mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (System.Exception) { }
  }

  // Emissive Lit material (URP): the number/boards must read even when the
  // directional light is behind them (same lesson as gameplay #1).
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
