// A_World/CountingGarden/CountingPlayBuilder.cs — S3 P2X PLAY ARENA (user order
// §47B): the Counting Garden's zone-2 "Vào chơi" destination is its OWN lazy
// scene (same pattern as CountingGardenScene), reached through the shared
// micro-scene slot (EnterMicro/ExitMicro) — never loaded at boot, never a
// second loader. Content is code-built around the SAME authored lesson stage
// as the garden theatre (CountingGardenBuilder.BuildDemoStageInto with this
// island's origin), so the demo lesson is one layout in two places.
// Firewall: no quest/bus/save here — this is presentation + the exit door.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class CountingPlayBuilder : MonoBehaviour {
  public const string SceneName = "CountingPlayScene";

  // Separate island east of the garden (Main 0, Math +60, garden +120).
  public static readonly Vector3 WorldOffset = new Vector3(180f, 0f, 0f);
  public const float BoundX = 18f;
  public const float BoundZ = 18f;
  // Arrival spawn sits clear of the exit portal fire radius (J4 lesson: the
  // portal only arms after the child walks clear of it once).
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -11.5f);
  // Follow camera: north of the child, looking south INTO the arena (the
  // lesson stage faces north) — same reading direction as the garden.
  // S3-P2Z11 (journey shot): a touch higher/farther so the board's "2" stays
  // fully inside the frame from the listen circle and the basket too.
  public static readonly Vector3 FollowOffset = new Vector3(0f, 4.1f, -5.3f);

  static readonly Color Lawn = new Color(0.38f, 0.64f, 0.36f);
  static readonly Color Meadow = new Color(0.46f, 0.71f, 0.42f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);
  static readonly Color CourtyardSand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color MintLeaf = new Color(0.70f, 0.90f, 0.72f);
  static readonly Color StoneGrey = new Color(0.68f, 0.68f, 0.66f);
  static readonly Color BasketBrown = new Color(0.55f, 0.38f, 0.22f);
  static readonly Color BasketRim = new Color(0.72f, 0.54f, 0.32f);
  static readonly Color BoardCream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color AppleRed = new Color(0.85f, 0.25f, 0.25f);

  // S3-P2Z11 (user: "phải có chỗ đứng cố định lúc nghe câu hỏi, sau khi nghe
  // câu hỏi thì mới chơi"): the marked listen circle just before the ball
  // field. The teacher reads the assignment only when the child stands here,
  // and the balls only accept clicks after it was read.
  public static readonly Vector3 ListenLocal = new Vector3(0f, 0f, 0.35f);
  public GameObject ListenPad { get; private set; }
  public GameObject ListenRing { get; private set; }

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  // S3-P2Z3 (user order): the arena is the GAME space only — the Number-2 NPC
  // demo (teacher/student + stage props) was removed from here; the child's
  // future game will be staged on this empty field. The garden keeps the
  // ambient miniature + try-run panel flow unchanged.

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

  // S3-P2Z4 REFERENCE GAMEPLAY staging: the activity field (board "2", natural
  // ball cluster, basket + count display + result board) and the acting layout
  // handed to CountingDemo. Positions follow the blueprint: the child enters
  // at z=-3, the board faces them, the balls sit mid-field, the basket is a
  // 3m walk away — a 20-60s round for 4-year-old legs.
  public CountingGardenBuilder.DemoRefs Activity { get; private set; }
  public GameObject CountDisplay { get; private set; }
  public GameObject[] CountPips { get; private set; }

  public void BuildContent(Transform root) {
    BuildGround(root);
    BuildEntryAndExit(root);
    BuildPaths(root);
    BuildDressing(root);
    BuildActivity(root);
    BuildAnchors(root);
    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  void BuildGround(Transform parent) {
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "CPRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(40f, 1.4f, 40f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    StripCollider(rim);
    IgnoreFromBuild(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "CPGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localPosition = Vector3.zero;
    ground.transform.localScale = new Vector3(3.8f, 1f, 3.8f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    MeadowPatch(parent, "CPMeadowW", new Vector3(-9f, 0f, 4f), 11f, 9f);
    MeadowPatch(parent, "CPMeadowE", new Vector3(9f, 0f, 4f), 11f, 9f);
    // Hedge ring framing the arena; the north bushes skip the exit corridor.
    float[] angles = { 15f, 45f, 75f, 105f, 135f, 160f, 200f, 225f, 255f, 285f, 315f, 345f };
    for (int i = 0; i < angles.Length; i++) {
      float rad = angles[i] * Mathf.Deg2Rad;
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "CPHedge" + i;
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
    // Entry arch: pulled BEHIND the spawn (-9.5) so it frames the arrival view
    // without sitting between the follow camera (-8) and the child (-3).
    Box(parent, "CPEntryPostL", new Vector3(-1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    Box(parent, "CPEntryPostR", new Vector3(1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    GameObject beam = Box(parent, "CPEntryBeam", new Vector3(0f, 2.06f, -9.5f),
      new Vector3(3.4f, 0.16f, 0.16f), WorldBeauty.BlossomPink);
    IgnoreFromBuild(beam);
    WorldBeauty.Ball(parent, "CPEntryBlossom0", new Vector3(0f, 2.42f, -9.5f), 1.1f,
      WorldBeauty.BlossomPink);
    // Entry threshold discs + walk-home marker + exit portal (behind the arch).
    Pad(parent, "CPThresholdL", new Vector3(-1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "CPThresholdR", new Vector3(1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "CPExitDisc", new Vector3(0f, 0.01f, -11.1f), 3.2f, WorldBeauty.Petal);
    GameObject exitGo = new GameObject("CPExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.PlayExit = true; // -> back to the Counting Garden (not the Math hub)
    exit.fireRadius = 1.35f;
    exit.areaId = CountingGardenArea.AreaId;
    ExitPortal = exit;
    Box(parent, "CPExitPostL", new Vector3(-1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Box(parent, "CPExitPostR", new Vector3(1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Sphere(parent, "CPExitCapL", new Vector3(-1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
    Sphere(parent, "CPExitCapR", new Vector3(1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
  }

  void BuildPaths(Transform parent) {
    Pad(parent, "CPPlazaPad", new Vector3(0f, 0.004f, 0f), 7.0f, CourtyardSand);
    Seg(parent, "CPPathEntry", new Vector3(0f, 0f, -10.4f), new Vector3(0f, 0f, -1.0f), 1.7f);
    Seg(parent, "CPPathStage", new Vector3(0f, 0f, 1.0f), new Vector3(0f, 0f, 8.0f), 1.7f);
  }

  void BuildDressing(Transform parent) {
    Vector3[] trees = {
      new Vector3(-6.5f, 0f, -3.5f), new Vector3(6.5f, 0f, -3.5f),
      new Vector3(-10f, 0f, 8f), new Vector3(10f, 0f, 8f),
      new Vector3(-5.5f, 0f, 15.5f), new Vector3(5.5f, 0f, 15.5f),
    };
    float[] scales = { 0.62f, 0.62f, 0.7f, 0.7f, 0.7f, 0.7f };
    for (int i = 0; i < trees.Length; i++) {
      WorldBeauty.BlossomTree(parent, "CPBlossomTree" + i, trees[i], scales[i]);
      WorldBeauty.PetalCarpet(parent, "CPPetalCarpet" + i, trees[i], 1.5f * scales[i] + 0.5f);
    }
    Vector3[] drifts = {
      new Vector3(-3.4f, 0f, 2.6f), new Vector3(3.4f, 0f, 2.6f),
      new Vector3(-4.6f, 0f, -6f), new Vector3(4.6f, 0f, -6f),
    };
    for (int i = 0; i < drifts.Length; i++)
      WorldBeauty.FlowerDrift(parent, "CPFlowerDrift" + i, drifts[i], 1.2f);
    WorldBeauty.PetalFall(parent, "CPPetalFall", new Vector3(0f, 0f, -1f), 9f, 12, 51109);
    WorldBeauty.Butterfly(parent, "CPButterfly0", new Vector3(3.4f, 0f, 2.6f), 2.2f, 0.1f,
      WorldBeauty.BlossomDeep, WorldBeauty.BlossomCream);
    WorldBeauty.Butterfly(parent, "CPButterfly1", new Vector3(-3.4f, 0f, 2.6f), 2.2f, 0.6f,
      WorldBeauty.Lilac, WorldBeauty.BlossomPink);
  }

  void BuildActivity(Transform parent) {
    CountingGardenBuilder.DemoRefs r = new CountingGardenBuilder.DemoRefs();
    r.StageParent = parent;
    r.Center = new Vector3(0f, 0f, 1.6f);
    r.Mouth = new Vector3(0f, 0f, 1.6f);

    // Board "2" (target): CLOSE to the child's field and EMISSIVE so the number
    // is obvious from every angle (user: number 2 must be visually obvious).
    Vector3 board = new Vector3(0f, 0f, 5.6f);
    Box(parent, "CPBoardL", board + new Vector3(-1.25f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), BasketBrown);
    Box(parent, "CPBoardR", board + new Vector3(1.25f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), BasketBrown);
    // S3-P2Z10 (journey shot): the follow camera framed the board with its top
    // cropped. Lowered the panel + digit so the whole "2" stays inside the
    // gameplay view (user brief §11: the board holds the target at all times).
    GameObject panel = Box(parent, "CPBoardPanel", board + new Vector3(0f, 1.95f, 0f),
      new Vector3(2.6f, 1.7f, 0.12f), BoardCream);
    SetMaterial(panel, LitEmissive(BoardCream, 0.22f));
    GameObject digit = CountingGardenBuilder.Digit2(parent, "CPNumber2",
      board + new Vector3(0f, 1.42f, -0.09f), 1.3f, 1.0f, Gold, 90f);
    SetMaterial(digit, LitEmissive(Gold, 0.5f));
    r.Number = digit;

    // Natural ball cluster (find-and-choose, never a straight test row):
    // two close together, one offset, one behind, one near the flowers.
    Color[] ballColors = { AppleRed, new Color(0.30f, 0.55f, 0.95f), Gold,
      new Color(0.35f, 0.75f, 0.40f), WorldBeauty.BlossomDeep };
    Vector3[] homes = {
      new Vector3(-0.85f, 0.22f, 1.85f), new Vector3(-0.20f, 0.22f, 1.55f),
      new Vector3(0.55f, 0.22f, 2.05f), new Vector3(1.05f, 0.22f, 1.35f),
      new Vector3(-0.55f, 0.22f, 0.95f),
    };
    for (int i = 0; i < homes.Length; i++) {
      GameObject ball = Sphere(parent, "CPBall" + i, homes[i], 0.44f,
        ballColors[i % ballColors.Length], false);
      if (ball != null) r.Balls.Add(ball);
    }
    r.BallHomes = homes;

    // Basket: a 2.4m walk from the cluster (short loop, child-sized).
    GameObject basketGo = Cylinder(parent, "CPBasket",
      new Vector3(2.3f, 0.27f, 0.6f), 1.05f, 0.55f, BasketBrown);
    r.Basket = basketGo != null ? basketGo.transform : null;
    Cylinder(parent, "CPBasketRim", new Vector3(2.3f, 0.55f, 0.6f), 1.15f, 0.1f, BasketRim);

    // Count display: two EMPTY slots always visible (grey), gold as counted.
    GameObject count = new GameObject("CPCountDisplay");
    count.transform.SetParent(parent, false);
    count.transform.localPosition = new Vector3(3.6f, 0f, 0.9f);
    Box(count.transform, "CPCountPost", new Vector3(0f, 0.5f, 0f),
      new Vector3(0.12f, 1.0f, 0.12f), BasketBrown);
    GameObject countFrame = Box(count.transform, "CPCountFrame", new Vector3(0f, 1.3f, 0f),
      new Vector3(1.05f, 1.05f, 0.1f), BoardCream);
    SetMaterial(countFrame, LitEmissive(BoardCream, 0.18f));
    CountDisplay = count;
    CountPips = new GameObject[2];
    for (int i = 0; i < 2; i++) {
      // Flat disc slots facing the child (they read as a counter, not balls
      // stuck on a board — journey shot P2).
      GameObject pip = Cylinder(count.transform, "CPCountPip" + i,
        new Vector3(0f, 1.08f + i * 0.46f, -0.10f), 0.36f, 0.02f, StoneGrey);
      pip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
      IgnoreFromBuild(pip);
      CountPips[i] = pip;
    }

    // Result board ("2 + tick"): RIGHT-FRONT of the basket, OFF the spawn
    // sightline (journey re-entry shot: it sat in front of the entry and the
    // child saw its big blank back) and clear of the count display in shot C.
    GameObject result = new GameObject("CPResult");
    result.transform.SetParent(parent, false);
    result.transform.localPosition = new Vector3(3.2f, 0f, -0.4f);
    Box(result.transform, "CPResultPost", new Vector3(0f, 0.65f, 0f),
      new Vector3(0.13f, 1.3f, 0.13f), BasketBrown);
    GameObject resultFrame = Box(result.transform, "CPResultFrame", new Vector3(0f, 1.6f, 0f),
      new Vector3(1.2f, 1.05f, 0.12f), BoardCream);
    SetMaterial(resultFrame, LitEmissive(BoardCream, 0.18f));
    GameObject resultTwo = CountingGardenBuilder.Digit2(result.transform, "CPResultTwo",
      new Vector3(0f, 1.2f, -0.09f), 0.6f, 0.45f, Gold, 90f);
    SetMaterial(resultTwo, LitEmissive(Gold, 0.45f));
    CountingGardenBuilder.CheckMark(result.transform, "CPResultCheck",
      new Vector3(0f, 1.82f, -0.09f), 0.3f, MintLeaf);
    result.SetActive(false);
    r.Result = result;

    // ---- listen circle (S3-P2Z11): fixed spot for the assignment -----------
    ListenRing = Pad(parent, "CPListenRing", ListenLocal + new Vector3(0f, 0.006f, 0f),
      3.1f, Gold);
    Pad(parent, "CPListenEdge", ListenLocal + new Vector3(0f, 0.004f, 0f),
      3.5f, new Color(0.80f, 0.42f, 0.20f));
    // Sky-blue face so the circle reads against the sand path (cream-on-sand
    // was invisible in the journey shot).
    ListenPad = Pad(parent, "CPListenPad", ListenLocal + new Vector3(0f, 0.014f, 0f),
      2.3f, new Color(0.62f, 0.85f, 0.97f));
    SetMaterial(ListenPad, LitEmissive(new Color(0.62f, 0.85f, 0.97f), 0.14f));
    for (int i = 0; i < 4; i++) {
      float a = (i / 4f) * Mathf.PI * 2f + Mathf.PI * 0.25f;
      Sphere(parent, "CPListenStud" + i,
        ListenLocal + new Vector3(Mathf.Cos(a) * 1.02f, 0.06f, Mathf.Sin(a) * 1.02f),
        0.16f, Gold, true);
    }
    Box(parent, "CPListenPost", ListenLocal + new Vector3(1.45f, 0.5f, 0.15f),
      new Vector3(0.12f, 1.0f, 0.12f), BasketBrown);
    Sphere(parent, "CPListenBell", ListenLocal + new Vector3(1.45f, 1.08f, 0.15f),
      0.34f, Gold, true);

    // ---- more eye candy (user: "gameplay chưa bắt mắt") --------------------
    // Bunting under the entry arch.
    Color[] bunting = { AppleRed, Gold, MintLeaf, WorldBeauty.BlossomDeep,
      new Color(0.30f, 0.55f, 0.95f) };
    for (int i = 0; i < 5; i++) {
      GameObject flag = Box(parent, "CPBunting" + i,
        new Vector3(-1.1f + i * 0.55f, 1.86f, -9.5f),
        new Vector3(0.34f, 0.34f, 0.05f), bunting[i % bunting.Length]);
      if (flag != null) {
        flag.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        IgnoreFromBuild(flag);
      }
    }
    // Counting stones + a toy block stack on the flanks (off every walk line).
    Vector3[] stones = {
      new Vector3(-3.1f, 0f, 0.6f), new Vector3(-4.2f, 0f, 2.0f),
      new Vector3(3.9f, 0f, 2.3f),
    };
    for (int i = 0; i < stones.Length; i++) {
      Cylinder(parent, "CPNumberStone" + i, stones[i] + new Vector3(0f, 0.05f, 0f),
        0.8f, 0.1f, StoneGrey);
      for (int b = 0; b <= i; b++) {
        float t = i == 0 ? 0f : (b / (float)i - 0.5f);
        Sphere(parent, "CPNumberStone" + i + "Bead" + b,
          stones[i] + new Vector3(t * 0.45f, 0.18f, 0f), 0.17f, Gold, false);
      }
    }
    Box(parent, "CPBlock0", new Vector3(4.2f, 0.25f, -0.6f), new Vector3(0.5f, 0.5f, 0.5f), AppleRed);
    Box(parent, "CPBlock1", new Vector3(4.2f, 0.72f, -0.6f), new Vector3(0.45f, 0.45f, 0.45f), Gold);
    Box(parent, "CPBlock2", new Vector3(3.65f, 0.22f, -0.7f), new Vector3(0.42f, 0.42f, 0.42f), MintLeaf);

    // Acting layout (compressed to child scale: the teacher/board are 8m from
    // the spawn, the balls 3-4m, the basket 2.4m from the cluster).
    r.NpcStart = new Vector3(-1.35f, 0f, 4.8f);
    r.StudentStart = new Vector3(0.7f, 0f, 4.2f);
    r.BallStand = new Vector3(-0.3f, 0f, 1.6f);
    r.BallStand2 = new Vector3(0.5f, 0f, 1.6f);
    r.BasketStand = new Vector3(1.8f, 0f, 1.4f);
    r.BoardPoint = board + new Vector3(0f, 1.45f, -0.12f);
    r.BallFieldPoint = new Vector3(0f, 0.6f, 1.6f);

    // Intro camera shots (A lesson, B balls, C basket/result) — the existing
    // SmartCamera beats, held through the intro, no shake, no cuts.
    GameObject camA = new GameObject("CPCamA");
    camA.transform.SetParent(parent, false);
    camA.transform.localPosition = new Vector3(0.5f, 2.3f, -1.6f);
    r.CamA = camA.transform;
    GameObject lookA = new GameObject("CPLookA");
    lookA.transform.SetParent(parent, false);
    lookA.transform.localPosition = new Vector3(0.3f, 1.5f, 4.8f);
    r.LookA = lookA.transform;
    GameObject camB = new GameObject("CPCamB");
    camB.transform.SetParent(parent, false);
    camB.transform.localPosition = new Vector3(0.9f, 1.9f, -2.6f);
    r.CamB = camB.transform;
    GameObject lookB = new GameObject("CPLookB");
    lookB.transform.SetParent(parent, false);
    lookB.transform.localPosition = new Vector3(0.1f, 0.8f, 1.6f);
    r.LookB = lookB.transform;
    GameObject camC = new GameObject("CPCamC");
    camC.transform.SetParent(parent, false);
    camC.transform.localPosition = new Vector3(2.6f, 1.8f, -1.6f);
    r.CamC = camC.transform;
    GameObject lookC = new GameObject("CPLookC");
    lookC.transform.SetParent(parent, false);
    lookC.transform.localPosition = new Vector3(2.2f, 1.0f, 0.8f);
    r.LookC = lookC.transform;
    DemoJuice.AttachSpotlight(parent, "CPGameSpotlight", new Vector3(0f, 0.018f, 1.6f), 5.4f);
    Activity = r;
  }

  // Emissive Lit material (URP): the number/boards must read even when the
  // directional light is behind them (journey screenshot: the board washed out).
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

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "CountingPlayPresentationRoot");
    Anchors = a;
    if (a == null) return;
    // Anchors mirror the reference gameplay staging (S3-P2Z4). The arrival beat
    // is a CHILD-height look at the field (journey shot A was a map view: the
    // board/number were unreadable), not a bird's eye.
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0f, 0f, 1.6f));
    a.Npc = a.EnsureSlot("NpcAnchor", new Vector3(-1.35f, 0f, 4.8f));
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0.6f, 3.2f, -5.5f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0.2f, 1.1f, 3.0f));
    a.Prompt = a.EnsureSlot("PromptAnchor", new Vector3(0f, 1.7f, 1.6f));
    a.Feedback = a.EnsureSlot("FeedbackAnchor", new Vector3(1.5f, 1.2f, 1.0f));
    a.Reward = a.EnsureSlot("RewardAnchor", new Vector3(2.3f, 0f, 0.6f));
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  // ---- helpers (same discipline as the garden builder) -------------------------

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
