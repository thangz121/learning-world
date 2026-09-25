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
  public static readonly Vector3 FollowOffset = new Vector3(0f, 4.1f, -5.3f);

  public const string ObjectiveEn = "Feed the Bunny";
  public const string ObjectiveVi = "Cho Thỏ Ăn";

  // Targets live in 1..MaxTarget (brief: 1-9 on ONE patch, never 9 plots).
  public const int MaxTarget = 9;
  public const int Target = 3; // default/fallback target (reference round)
  public const int CarrotCount = 10; // the target plus a spare for the correction

  // The round's mission comes from the area's ladder (progression/CLI); the
  // boards stage that digit so the world always shows the mission.
  public int BoardTarget = Target;

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
  public static readonly Vector3 RabbitHome = new Vector3(2.3f, 0f, 3.9f);
  public static readonly Vector3 BowlPos = new Vector3(2.3f, 0f, 3.1f);
  public static readonly Vector3 FeedStand = new Vector3(1.5f, 0f, 2.2f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public GameObject NumberBoard { get; private set; } // target digit (pulses)
  public GameObject Result { get; private set; }      // target + tick (hidden)
  public GameObject[] CountPips { get; private set; }
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
    BuildPatch(root);
    BuildRabbit(root);
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
    int n = ClampTarget(BoardTarget);
    NumberBoard = CountingGardenBuilder.Digit(parent, "RPNumberDigit",
      BoardPos + new Vector3(0f, 1.42f, -0.09f), 1.3f, 1.0f, Gold, 90f, n);
    SetMaterial(NumberBoard, LitEmissive(Gold, 0.5f));
    IgnoreFromBuild(NumberBoard);
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
    // A small sign: this is the carrot corner (carrot-top marker).
    Box(parent, "RPPatchSignPost", PatchCenter + new Vector3(-1.9f, 0.5f, -1.3f),
      new Vector3(0.12f, 1.0f, 0.12f), FenceWood);
    Sphere(parent, "RPPatchSignTop", PatchCenter + new Vector3(-1.9f, 1.08f, -1.3f),
      0.3f, CarrotOrange, true);
    Sphere(parent, "RPPatchSignLeaf", PatchCenter + new Vector3(-1.9f, 1.28f, -1.3f),
      0.2f, LeafGreen, true);

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
      Vector3 home = new Vector3(PatchCenter.x + offs[i].x, 0.12f, PatchCenter.z + offs[i].z);
      CarrotHomes[i] = home;
      GameObject carrot = new GameObject("RPCarrot" + i);
      carrot.transform.SetParent(parent, false);
      carrot.transform.localPosition = home;
      carrot.transform.localRotation = Quaternion.Euler(0f, (i * 47f) % 360f, 8f);
      // Orange body (half-sunk) + green leaves: reads as a carrot at distance.
      GameObject body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      body.name = "Body";
      body.transform.SetParent(carrot.transform, false);
      body.transform.localPosition = new Vector3(0f, 0.02f, 0f);
      body.transform.localScale = new Vector3(0.24f, 0.34f, 0.24f);
      body.GetComponent<Renderer>().sharedMaterial = Lit(CarrotOrange);
      StripCollider(body);
      GameObject leaf0 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      leaf0.name = "Leaf0";
      leaf0.transform.SetParent(carrot.transform, false);
      leaf0.transform.localPosition = new Vector3(0.05f, 0.24f, 0f);
      leaf0.transform.localScale = new Vector3(0.1f, 0.22f, 0.1f);
      leaf0.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
      leaf0.GetComponent<Renderer>().sharedMaterial = Lit(LeafGreen);
      StripCollider(leaf0);
      IgnoreFromBuild(leaf0);
      GameObject leaf1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      leaf1.name = "Leaf1";
      leaf1.transform.SetParent(carrot.transform, false);
      leaf1.transform.localPosition = new Vector3(-0.05f, 0.24f, 0f);
      leaf1.transform.localScale = new Vector3(0.1f, 0.22f, 0.1f);
      leaf1.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
      leaf1.GetComponent<Renderer>().sharedMaterial = Lit(LeafGreen);
      StripCollider(leaf1);
      IgnoreFromBuild(leaf1);
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
    bowl.transform.localPosition = new Vector3(BowlPos.x, 0.07f, BowlPos.z);
    bowl.transform.localScale = new Vector3(0.95f, 0.14f, 0.95f);
    bowl.GetComponent<Renderer>().sharedMaterial = Lit(WorldBeauty.BlossomCream);
    StripCollider(bowl);
    GameObject bowlRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    bowlRim.name = "RPFeedBowlRim";
    bowlRim.transform.SetParent(parent, false);
    bowlRim.transform.localPosition = new Vector3(BowlPos.x, 0.03f, BowlPos.z);
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
    outer.transform.localScale = new Vector3(0.1f, 0.44f, 0.07f);
    outer.GetComponent<Renderer>().sharedMaterial = Lit(BunnyGrey);
    StripCollider(outer);
    IgnoreFromBuild(outer);
    GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Cube);
    inner.name = name + "Inner";
    inner.transform.SetParent(ear.transform, false);
    inner.transform.localPosition = new Vector3(0f, 0.2f, -0.02f);
    inner.transform.localScale = new Vector3(0.05f, 0.3f, 0.03f);
    inner.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.95f, 0.65f, 0.68f));
    StripCollider(inner);
    IgnoreFromBuild(inner);
    return ear;
  }

  void BuildCountAndResult(Transform parent) {
    // Count display: nine slots in a 3x3 grid (grey empty, gold as fed) —
    // the board reads "0/9 .. 9/9" at a glance for every target, no text.
    GameObject count = new GameObject("RPCountDisplay");
    count.transform.SetParent(parent, false);
    count.transform.localPosition = new Vector3(3.8f, 0f, 1.9f);
    Box(count.transform, "RPCountPost", new Vector3(0f, 0.6f, 0f),
      new Vector3(0.12f, 1.2f, 0.12f), FenceWood);
    GameObject frame = Box(count.transform, "RPCountFrame", new Vector3(0f, 1.7f, 0f),
      new Vector3(1.35f, 1.35f, 0.1f), BoardCream);
    SetMaterial(frame, LitEmissive(BoardCream, 0.18f));
    frame.transform.localRotation = Quaternion.Euler(0f, -25f, 0f);
    CountPips = new GameObject[MaxTarget];
    for (int i = 0; i < MaxTarget; i++) {
      int col = i % 3, row = i / 3;
      GameObject pip = Cylinder(count.transform, "RPCountPip" + i,
        new Vector3((col - 1) * 0.36f, 1.28f + (2 - row) * 0.36f, -0.10f), 0.3f, 0.02f, StoneGrey);
      pip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
      IgnoreFromBuild(pip);
      CountPips[i] = pip;
    }

    // Result board ("N + tick"): right-front of the bowl, OFF the spawn
    // sightline (same lesson as gameplay #1).
    int n = ClampTarget(BoardTarget);
    GameObject result = new GameObject("RPResult");
    result.transform.SetParent(parent, false);
    result.transform.localPosition = new Vector3(3.2f, 0f, -0.4f);
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

  void BuildCameras(Transform parent) {
    // A: the lesson (board + teacher + student + patch glimpse).
    CamTeaching = CamAnchor(parent, "RPCamA", new Vector3(0.5f, 2.3f, -1.6f));
    LookTeaching = CamAnchor(parent, "RPLookA", new Vector3(0.2f, 1.5f, 6.6f));
    // B: the demo (patch + student + rabbit, one wide frame).
    CamDemo = CamAnchor(parent, "RPCamB", new Vector3(0.4f, 2.7f, -2.4f));
    LookDemo = CamAnchor(parent, "RPLookB", new Vector3(0.4f, 0.7f, 3.4f));
    // C: the payoff (player + rabbit + board + result).
    CamSuccess = CamAnchor(parent, "RPCamC", new Vector3(2.8f, 2.0f, -0.8f));
    LookSuccess = CamAnchor(parent, "RPLookC", new Vector3(1.6f, 1.0f, 3.6f));
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
      Unity.AI.Navigation.NavMeshModifier mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
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
