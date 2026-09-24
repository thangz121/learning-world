// A_World/CountingGarden/StairHillBuilder.cs — S3-P2Z12 GAMEPLAY #2
// "BẬC THANG CON SỐ" (number stairs). The Counting Garden's sixth plot opens its
// OWN LAZY play scene — same micro-slot contract as CountingPlayScene (S3 P2X):
// registered in Build Settings, loaded ONLY through WorldTransition.EnterMicroAsync
// when "Vào chơi" is pressed, unloaded through ExitMicroAsync on the way home.
// Never loaded at boot, never stacked with another micro scene.
//
// Space (user brief §3): ENTRY -> TEACHING/DEMO AREA -> STAIR ARENA -> LANDING.
// The child spawns south (z=-3), the teaching board (round target) + teacher
// stand west of the stair foot, the 9-step child-scale hill rises north to a
// landing with the goal arch. All code-built (primitives + shared materials),
// NavMesh baked at runtime on THIS root only (CollectObjects.Children — J8).
// No bus/quest/save here: presentation + the door. C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class StairHillBuilder : MonoBehaviour {
  public const string SceneName = "StairPlayScene";
  // Separate island east of the counting arena (Main 0, Math +60, garden +120,
  // ball arena +180).
  public static readonly Vector3 WorldOffset = new Vector3(240f, 0f, 0f);
  public const float BoundX = 18f;
  public const float BoundZ = 20f;
  // Arrival spawn clears the exit portal fire radius (J4 lesson: the portal
  // only arms after the child walks clear once).
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -11.5f);
  // Follow camera: south of the child, looking north INTO the hill (the stairs
  // rise away from the arrival, so the child always sees the next step).
  public static readonly Vector3 FollowOffset = new Vector3(0f, 4.2f, -5.6f);
  // HUD objective for the arena (localized by the area).
  public const string ObjectiveEn = "Number Steps";
  public const string ObjectiveVi = "Đồi Bậc Thang";

  // ---- stair geometry (child scale; the single source of truth) ----------------
  // MAXIMUM = 9 (brief §2): ONE staircase for every target 1..9 — never
  // rebuilt, hidden, or rescaled per target. The target only decides which
  // step the child must stop on. NHIỀU BẬC THẤP (brief §5): 16cm risers keep
  // the whole 9-step hill at 1.44m (agent climb default 0.4m — the baked ramp
  // stays gentle).
  public const int StepCount = 9;
  public const int Target = 3;          // default/fallback target (reference round)
  public const float Rise = 0.16f;      // each tread is 16cm — a 4yo stair
  public const float Tread = 0.88f;     // user round: 0.66 read "too short" — the
                                        // child's whole foot filled the tread and
                                        // the next riser touched their heel
  public const float StairWidth = 3.8f;
  public const float BaseZ = 4.6f;      // first riser (front edge) local z
  public const float CenterX = 0f;
  public const float LandingDepth = 3.4f;
  public const float LandingWidth = StairWidth + 2.0f;
  // The round's mission, pushed by the installer BEFORE Build() (the area owns
  // it: progression + CLI override). Read through ClampTarget, never raw.
  public int BoardTarget = Target;

  // Targets live in 1..StepCount (brief §2: MAXIMUM = 9); out-of-range values
  // clamp to the walkable range so the board never blanks and the lesson never
  // asks for an unstandable step.
  public static int ClampTarget(int t) {
    if (t < 1) return 1;
    if (t > StepCount) return StepCount;
    return t;
  }

  // ---- acting layout ------------------------------------------------------------
  public static readonly Vector3 TeacherStart = new Vector3(-1.9f, 0f, 2.9f);
  public static readonly Vector3 StudentStart = new Vector3(0.75f, 0f, 3.3f);
  public static readonly Vector3 StudentReturn = new Vector3(1.7f, 0f, 2.6f);
  public static readonly Vector3 StudentBase = new Vector3(0f, 0f, 4.05f);
  static readonly Vector3 BoardPos = new Vector3(-3.0f, 0f, 3.4f);
  static readonly Vector3 ResultPos = new Vector3(2.9f, 0f, 3.4f);
  public const float BoardPointY = 1.75f;
  public const float StairsPointY = 1.15f;

  static readonly Vector3 CamTeachingPos = new Vector3(2.2f, 3.0f, -2.2f);
  static readonly Vector3 CamTeachingLook = new Vector3(-0.6f, 1.25f, 5.0f);
  // Demo shot: ONE setup must cover the FULL 9-step run for any target demo
  // (brief §12/§22) — the whole staircase + the climbing student + the board
  // edge in one frame, re-issued while the demo runs.
  static readonly Vector3 CamDemoPos = new Vector3(6.3f, 3.5f, -0.2f);
  static readonly Vector3 CamDemoLook = new Vector3(-0.7f, 1.0f, 7.9f);
  // Success shot (brief §22): Player on the TARGET step + Teacher + board +
  // result board in one frame, from the east side pulled back/up so the whole
  // run (z 2..11) reads; verified per target in the journey shots.
  static readonly Vector3 CamSuccessPos = new Vector3(7.3f, 3.5f, -0.4f);
  static readonly Vector3 CamSuccessLook = new Vector3(-0.9f, 1.1f, 5.4f);

  static readonly Color Lawn = new Color(0.38f, 0.64f, 0.36f);
  static readonly Color Meadow = new Color(0.46f, 0.71f, 0.42f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);
  static readonly Color CourtyardSand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color StepCap = new Color(0.90f, 0.82f, 0.62f);
  static readonly Color StoneGrey = new Color(0.68f, 0.68f, 0.66f);
  static readonly Color BoardCream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color BasketBrown = new Color(0.55f, 0.38f, 0.22f);
  static readonly Color BasketRim = new Color(0.72f, 0.54f, 0.32f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color Sky = new Color(0.45f, 0.70f, 0.92f);
  static readonly Color MintLeaf = new Color(0.70f, 0.90f, 0.72f);
  static readonly Color BushGreen = new Color(0.24f, 0.55f, 0.30f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public StairRun Stairs { get; private set; }
  public GameObject NumberBoard { get; private set; }   // target digit (pulses)
  public GameObject Result { get; private set; }        // target + tick (hidden)
  public GameObject[] StepCues { get; private set; }    // bead row per step
  Renderer[] _stepTops;                                 // torched while selected
  Material _stepGlowMat;
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

  // User round "chọn bậc nào thì sáng bậc đó": the selected/hovered tread top
  // swaps to an emissive gold material (0 clears). Called only on change, so no
  // per-frame material churn.
  public void GlowStep(int step) {
    if (_stepTops == null) return;
    Material glow = (step >= 1 && step <= StepCount) ? GlowMat() : null;
    Material plain = Lit(StepCap);
    for (int i = 0; i < _stepTops.Length; i++) {
      if (_stepTops[i] == null) continue;
      _stepTops[i].sharedMaterial = (glow != null && i == step - 1) ? glow : plain;
    }
  }

  Material GlowMat() {
    if (_stepGlowMat == null) _stepGlowMat = LitEmissive(Gold, 0.85f);
    return _stepGlowMat;
  }

  // Runtime NavMesh bake for THIS scene only (CollectObjects.Children on the
  // hill root — a scene-wide bake would collect Market/Math meshes).
  // HEIGHT MESH (user round "chân bị lún xuống bậc"): the plain render bake
  // approximated the staircase as a ramp, so the agent — and the child's feet —
  // rode up to 8cm BELOW each tread top. buildHeightMesh keeps the agent on the
  // actual tread surfaces while leaving the navigation shape exactly as it was
  // (a collider bake produced a navmesh hole by the exit door in the journey).
  void BuildNavMesh(Transform parent) {
    Unity.AI.Navigation.NavMeshSurface surface =
      parent.gameObject.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
    if (surface == null) surface = parent.gameObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
    surface.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
    surface.buildHeightMesh = true;
    surface.BuildNavMesh();
  }

  public void BuildContent(Transform root) {
    BuildGround(root);
    BuildEntryAndExit(root);
    BuildPaths(root);
    BuildStairs(root);
    BuildTeachingArea(root);
    BuildDressing(root);
    BuildAnchors(root);
    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  // ---- ground -------------------------------------------------------------------

  void BuildGround(Transform parent) {
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "SHRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(40f, 1.4f, 40f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    StripCollider(rim);
    IgnoreFromBuild(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "SHGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localPosition = Vector3.zero;
    ground.transform.localScale = new Vector3(3.8f, 1f, 3.8f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    MeadowPatch(parent, "SHMeadowW", new Vector3(-9.5f, 0f, 3f), 11f, 10f);
    MeadowPatch(parent, "SHMeadowE", new Vector3(9.5f, 0f, 3f), 11f, 10f);
    // Hedge ring framing the hill; the exit corridor (north, z-) stays open.
    float[] angles = { 15f, 45f, 75f, 105f, 135f, 165f, 200f, 230f, 260f, 290f, 320f, 345f };
    for (int i = 0; i < angles.Length; i++) {
      float rad = angles[i] * Mathf.Deg2Rad;
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "SHHedge" + i;
      bush.transform.SetParent(parent, false);
      bush.transform.localPosition = new Vector3(Mathf.Sin(rad) * 15.5f, 0.55f, 4f + Mathf.Cos(rad) * 15.5f);
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
    Box(parent, "SHEntryPostL", new Vector3(-1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    Box(parent, "SHEntryPostR", new Vector3(1.6f, 1.0f, -9.5f),
      new Vector3(0.22f, 2.0f, 0.22f), WorldBeauty.BlossomDeep);
    GameObject beam = Box(parent, "SHEntryBeam", new Vector3(0f, 2.06f, -9.5f),
      new Vector3(3.4f, 0.16f, 0.16f), WorldBeauty.BlossomPink);
    IgnoreFromBuild(beam);
    WorldBeauty.Ball(parent, "SHEntryBlossom0", new Vector3(0f, 2.42f, -9.5f), 1.1f,
      WorldBeauty.BlossomPink);
    Pad(parent, "SHThresholdL", new Vector3(-1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "SHThresholdR", new Vector3(1.5f, 0.005f, -3.6f), 1.0f, StoneGrey);
    Pad(parent, "SHExitDisc", new Vector3(0f, 0.01f, -11.1f), 3.2f, WorldBeauty.Petal);
    GameObject exitGo = new GameObject("SHExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.PlayExit = true; // -> back to the Counting Garden
    exit.fireRadius = 1.35f;
    exit.areaId = CountingGardenArea.AreaId;
    ExitPortal = exit;
    Box(parent, "SHExitPostL", new Vector3(-1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Box(parent, "SHExitPostR", new Vector3(1.6f, 0.9f, -11.5f),
      new Vector3(0.16f, 1.8f, 0.16f), MintLeaf);
    Sphere(parent, "SHExitCapL", new Vector3(-1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
    Sphere(parent, "SHExitCapR", new Vector3(1.6f, 1.9f, -11.5f), 0.4f, MintLeaf, true);
  }

  void BuildPaths(Transform parent) {
    Pad(parent, "SHPlazaPad", new Vector3(0f, 0.004f, 0f), 7.0f, CourtyardSand);
    Seg(parent, "SHPathEntry", new Vector3(0f, 0f, -10.4f), new Vector3(0f, 0f, -1.0f), 1.7f);
    Seg(parent, "SHPathStairs", new Vector3(0f, 0f, 1.0f), new Vector3(0f, 0f, 4.2f), 1.7f);
  }

  // ---- the stair hill ------------------------------------------------------------

  // Nine child-scale steps as full-height columns (a ziggurat) so the silhouette
  // reads "climb me" from the spawn; each tread gets a light cap + a bead row
  // (N gold beads = the step's number, the garden's counting language). No
  // overhead geometry crosses the steps: every beam above a corridor is
  // bake-ignored (headroom rule) and the landing arch clears 2.3m.
  // Slim side stringers (bake-ignored boards riding the slope) unify the run
  // into ONE staircase (brief §6) without touching the walkable band.
  void BuildStairs(Transform parent) {
    GameObject stairsGo = new GameObject("SHStairs");
    stairsGo.transform.SetParent(parent, false);
    StairRun run = stairsGo.AddComponent<StairRun>();
    run.stepCount = StepCount;
    run.rise = Rise;
    run.tread = Tread;
    run.width = StairWidth;
    run.baseZ = BaseZ;
    run.centerX = CenterX;
    run.landingDepth = LandingDepth;
    run.landingHalfWidth = LandingWidth * 0.5f;
    Stairs = run;
    StepCues = new GameObject[StepCount];
    _stepTops = new Renderer[StepCount];
    for (int i = 1; i <= StepCount; i++) {
      float top = i * Rise;
      float zc = BaseZ + (i - 0.5f) * Tread;
      // Solid (collider KEPT): the treads are the CLICK PATH up the hill — a
      // stripped step lets clicks fall through to the ground behind it (the
      // child would walk past the stairs instead of up them). The collider
      // top is ALSO the walkable surface the NavMesh bakes from (feet on board).
      GameObject tread = BoxSolid(parent, "SHStep" + i, new Vector3(CenterX, top * 0.5f, zc),
        new Vector3(StairWidth, top, Tread), CountingGardenBuilder.StepWood);
      StairTread treadId = tread.AddComponent<StairTread>();
      treadId.Bind(run, i);
      GameObject cap = Box(parent, "SHStepTop" + i, new Vector3(CenterX, top + 0.0045f, zc),
        new Vector3(StairWidth - 0.06f, 0.008f, Tread - 0.05f), StepCap);
      _stepTops[i - 1] = cap.GetComponent<Renderer>();
      // Bead row on the LEFT edge of the tread (reads 1..N like the plots).
      GameObject cue = new GameObject("SHStepCue" + i);
      cue.transform.SetParent(parent, false);
      cue.transform.localPosition = new Vector3(CenterX, top, zc);
      StepCues[i - 1] = cue;
      for (int b = 0; b < i; b++) {
        float t = i == 1 ? 0f : (b / (float)(i - 1) - 0.5f);
        // Bake-ignored: the beads are counting cues, never obstacles on the
        // tread (a baked bead would erode the walkable width by agentRadius).
        Sphere(cue.transform, "SHStepBead" + i + "_" + b,
          new Vector3(-StairWidth * 0.5f + 0.28f, 0.10f, t * 0.42f), 0.15f, Gold, true);
      }
    }
    // Top landing (walkable, level with step 9) + the goal: gate arch + flag.
    // It doubles as the celebration/viewpoint (brief §27): flower beds, the
    // goal arch, and a view back over the garden — never an empty plane.
    float topN = StepCount * Rise;
    float landingZ = BaseZ + StepCount * Tread + LandingDepth * 0.5f;
    BoxSolid(parent, "SHLanding", new Vector3(CenterX, topN * 0.5f, landingZ),
      new Vector3(LandingWidth, topN, LandingDepth), CountingGardenBuilder.StepWood);
    Box(parent, "SHLandingTop", new Vector3(CenterX, topN + 0.0045f, landingZ),
      new Vector3(LandingWidth - 0.08f, 0.008f, LandingDepth - 0.06f), StepCap);
    // Goal arch on the landing: two posts + a high bake-ignored beam + blossom
    // crown, so "the top" is a place, not a cliff.
    float archZ = BaseZ + StepCount * Tread + LandingDepth - 0.6f;
    Box(parent, "SHGoalPostL", new Vector3(-2.3f, topN + 1.15f, archZ),
      new Vector3(0.2f, 2.3f, 0.2f), BasketBrown);
    Box(parent, "SHGoalPostR", new Vector3(2.3f, topN + 1.15f, archZ),
      new Vector3(0.2f, 2.3f, 0.2f), BasketBrown);
    GameObject goalBeam = Box(parent, "SHGoalBeam", new Vector3(0f, topN + 2.36f, archZ),
      new Vector3(4.9f, 0.18f, 0.18f), Sky);
    IgnoreFromBuild(goalBeam);
    Sphere(parent, "SHGoalCapL", new Vector3(-2.3f, topN + 2.48f, archZ), 0.42f, Gold, true);
    Sphere(parent, "SHGoalCapR", new Vector3(2.3f, topN + 2.48f, archZ), 0.42f, Gold, true);
    WorldBeauty.Ball(parent, "SHGoalBlossom0", new Vector3(0f, topN + 2.62f, archZ), 0.8f,
      WorldBeauty.BlossomPink);
    // Landing flower beds (celebration corner dressing, off the walk line).
    PlaceProp(parent, "flower_yellowA", "SHLandingFlowerL",
      new Vector3(-2.2f, 0f, landingZ - 0.6f), 0f, 0.9f);
    PlaceProp(parent, "flower_yellowA", "SHLandingFlowerR",
      new Vector3(2.2f, 0f, landingZ - 0.6f), 0f, 0.9f);
    // Hill mound BEHIND the landing (visual body of the hill; its front edge
    // stays clear of the walkable landing).
    GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    mound.name = "SHHillMound";
    mound.transform.SetParent(parent, false);
    mound.transform.localPosition = new Vector3(0f, 0.9f, 17.9f);
    mound.transform.localScale = new Vector3(9.5f, 2.6f, 3.2f);
    mound.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.60f, 0.34f));
    StripCollider(mound);
    WorldBeauty.BlossomTree(parent, "SHHillTree0", new Vector3(-5.5f, 0f, 15.0f), 0.55f);
    WorldBeauty.BlossomTree(parent, "SHHillTree1", new Vector3(5.5f, 0f, 15.0f), 0.5f);
    // Base flags flanking the first step (identity: a place you climb).
    Flag(parent, "SHFlagL", new Vector3(-2.35f, 0f, 4.3f), 0.0f);
    Flag(parent, "SHFlagR", new Vector3(2.35f, 0f, 4.3f), 0.0f);
    // Side bushes: flanking walls that keep the climb the only way up.
    for (int i = 0; i < 8; i++) {
      float z = 4.9f + i * 1.06f;
      SideBush(parent, "SHStairBushL" + i, new Vector3(-2.95f, 0f, z), 1.35f);
      SideBush(parent, "SHStairBushR" + i, new Vector3(2.95f, 0f, z), 1.35f);
    }
    BuildStringers(parent);
  }

  // Slim boards riding the slope on both flanks (brief §6: side structure that
  // reads the run as ONE staircase). Bake-ignored + collider-free: pure visual
  // edging, outside the walkable band, so the NavMesh never sees them.
  void BuildStringers(Transform parent) {
    float run = StepCount * Tread;
    float height = StepCount * Rise;
    float len = Mathf.Sqrt(run * run + height * height) + 0.3f;
    float pitch = -Mathf.Atan2(height, run) * Mathf.Rad2Deg;
    float zc = BaseZ + run * 0.5f;
    float yc = height * 0.5f + 0.02f;
    foreach (float side in new[] { -1f, 1f }) {
      GameObject board = Box(parent, side < 0f ? "SHStringerL" : "SHStringerR",
        new Vector3(CenterX + side * (StairWidth * 0.5f + 0.06f), yc, zc),
        new Vector3(0.12f, 0.24f, len), CountingGardenBuilder.StepWood);
      if (board != null) {
        board.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        IgnoreFromBuild(board);
      }
    }
  }

  void Flag(Transform parent, string name, Vector3 pos, float yaw) {
    Cylinder(parent, name + "Post", pos + new Vector3(0f, 0.75f, 0f), 0.07f, 1.5f, BasketBrown);
    GameObject cloth = Box(parent, name, pos + new Vector3(0.17f, 1.28f, 0f),
      new Vector3(0.34f, 0.22f, 0.02f), Gold);
    if (cloth != null) cloth.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
  }

  void SideBush(Transform parent, string name, Vector3 pos, float scale) {
    GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    bush.name = name;
    bush.transform.SetParent(parent, false);
    bush.transform.localPosition = pos + new Vector3(0f, scale * 0.42f, 0f);
    bush.transform.localScale = new Vector3(scale, scale * 1.15f, scale);
    bush.GetComponent<Renderer>().sharedMaterial = Lit(BushGreen);
    StripCollider(bush);
  }

  // ---- teaching / demo area ------------------------------------------------------

  // The target board + the result board (target + tick) + the camera markers.
  // Both boards are built in a rotated GROUP whose local -Z faces the arriving
  // child, using the SAME construction as the garden boards (panel thin in
  // local z, digit at yaw 90) — one proven frame trick, no per-scene
  // orientation math. The digit is the ROUND's target (brief §9/§34): the
  // board always holds the current mission, never a fixed "3".
  void BuildTeachingArea(Transform parent) {
    int n = ClampTarget(BoardTarget);
    GameObject board = new GameObject("SHBoard");
    board.transform.SetParent(parent, false);
    board.transform.localPosition = BoardPos;
    board.transform.localRotation = Quaternion.identity; // faces the child (south)
    Box(board.transform, "SHBoardL", new Vector3(-1.15f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), BasketBrown);
    Box(board.transform, "SHBoardR", new Vector3(1.15f, 0.85f, 0f),
      new Vector3(0.15f, 1.7f, 0.15f), BasketBrown);
    GameObject panel = Box(board.transform, "SHBoardPanel", new Vector3(0f, 2.0f, 0f),
      new Vector3(2.4f, 1.6f, 0.12f), BoardCream);
    SetMaterial(panel, LitEmissive(BoardCream, 0.22f));
    NoShadows(panel);
    // The digit rides 18cm OUT of the panel mass (journey shot: at 9cm the bars
    // intersected the panel and the seam read as z-fighting teeth), and takes
    // no shadows (the glyph is the one thing a child must read).
    GameObject digit = CountingGardenBuilder.Digit(board.transform, "SHNumberDigit",
      new Vector3(0f, 1.5f, -0.18f), 1.15f, 0.9f, Gold, 90f, n);
    SetMaterial(digit, LitEmissive(Gold, 0.5f));
    NoShadows(digit);
    NumberBoard = digit;

    GameObject result = new GameObject("SHResult");
    result.transform.SetParent(parent, false);
    result.transform.localPosition = ResultPos;
    Box(result.transform, "SHResultPost", new Vector3(0f, 0.65f, 0f),
      new Vector3(0.13f, 1.3f, 0.13f), BasketBrown);
    GameObject resultFrame = Box(result.transform, "SHResultFrame", new Vector3(0f, 1.6f, 0f),
      new Vector3(1.2f, 1.05f, 0.12f), BoardCream);
    SetMaterial(resultFrame, LitEmissive(BoardCream, 0.18f));
    NoShadows(resultFrame);
    GameObject resultDigit = CountingGardenBuilder.Digit(result.transform, "SHResultDigit",
      new Vector3(0f, 1.2f, -0.18f), 0.6f, 0.45f, Gold, 90f, n);
    SetMaterial(resultDigit, LitEmissive(Gold, 0.45f));
    NoShadows(resultDigit);
    CountingGardenBuilder.CheckMark(result.transform, "SHResultCheck",
      new Vector3(0f, 1.82f, -0.18f), 0.3f, MintLeaf);
    result.SetActive(false);
    Result = result;

    // Camera markers (scene-authored transforms, no second camera system).
    CamTeaching = Marker(parent, "SHCamTeaching", CamTeachingPos);
    LookTeaching = Marker(parent, "SHLookTeaching", CamTeachingLook);
    CamDemo = Marker(parent, "SHCamDemo", CamDemoPos);
    LookDemo = Marker(parent, "SHLookDemo", CamDemoLook);
    CamSuccess = Marker(parent, "SHCamSuccess", CamSuccessPos);
    LookSuccess = Marker(parent, "SHLookSuccess", CamSuccessLook);
    // A soft stage pool at the stair foot ("this is the stage").
    DemoJuice.AttachSpotlight(parent, "SHStageLight", new Vector3(0f, 0.018f, 3.4f), 5.6f);
  }

  static Transform Marker(Transform parent, string name, Vector3 pos) {
    GameObject go = new GameObject(name);
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    return go.transform;
  }

  // ---- dressing -------------------------------------------------------------------

  void BuildDressing(Transform parent) {
    Vector3[] trees = {
      new Vector3(-6.5f, 0f, -3.5f), new Vector3(6.5f, 0f, -3.5f),
      new Vector3(-10f, 0f, 8f), new Vector3(10f, 0f, 8f),
      new Vector3(-5.5f, 0f, -8.5f), new Vector3(5.5f, 0f, -8.5f),
    };
    float[] scales = { 0.62f, 0.62f, 0.7f, 0.7f, 0.66f, 0.66f };
    for (int i = 0; i < trees.Length; i++) {
      WorldBeauty.BlossomTree(parent, "SHBlossomTree" + i, trees[i], scales[i]);
      WorldBeauty.PetalCarpet(parent, "SHPetalCarpet" + i, trees[i], 1.5f * scales[i] + 0.5f);
    }
    Vector3[] drifts = {
      new Vector3(-3.4f, 0f, 1.6f), new Vector3(3.4f, 0f, 1.6f),
      new Vector3(-4.6f, 0f, -6f), new Vector3(4.6f, 0f, -6f),
    };
    for (int i = 0; i < drifts.Length; i++)
      WorldBeauty.FlowerDrift(parent, "SHFlowerDrift" + i, drifts[i], 1.2f);
    WorldBeauty.PetalFall(parent, "SHPetalFall", new Vector3(0f, 0f, -1f), 9f, 12, 71212);
    WorldBeauty.Butterfly(parent, "SHButterfly0", new Vector3(3.4f, 0f, 1.6f), 2.2f, 0.1f,
      WorldBeauty.BlossomDeep, WorldBeauty.BlossomCream);
    WorldBeauty.Butterfly(parent, "SHButterfly1", new Vector3(-3.4f, 0f, 1.6f), 2.2f, 0.6f,
      WorldBeauty.Lilac, WorldBeauty.BlossomPink);
    // Step-side flower beds (outside the stair width, inside the bush line).
    for (int i = 0; i < 7; i++) {
      PlaceProp(parent, "flower_yellowA", "SHStairFlowerL" + i,
        new Vector3(-2.35f, 0f, 5.4f + i * 1.1f), 0f, 0.85f);
      PlaceProp(parent, "flower_yellowA", "SHStairFlowerR" + i,
        new Vector3(2.35f, 0f, 5.4f + i * 1.1f), 0f, 0.85f);
    }
  }

  // ---- anchors ---------------------------------------------------------------------

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "StairPlayPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0f, 0f, BaseZ + Tread));
    a.Npc = a.EnsureSlot("NpcAnchor", TeacherStart);
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0.5f, 4.6f, -8.0f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0.1f, 1.4f, 3.8f));
    a.Prompt = a.EnsureSlot("PromptAnchor", new Vector3(0f, 1.7f, 3.4f));
    a.Feedback = a.EnsureSlot("FeedbackAnchor", new Vector3(1.8f, 1.2f, 3.4f));
    a.Reward = a.EnsureSlot("RewardAnchor", new Vector3(2.9f, 0f, 3.4f));
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  // ---- helpers (same discipline as the neighbouring builders) ----------------------

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

  // Same cube, but the collider STAYS: used for the walkable treads/landing,
  // which must answer ClickRouter rays (clicks walk the child up the stairs).
  static GameObject BoxSolid(Transform parent, string name, Vector3 pos, Vector3 scale, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    go.transform.localScale = scale;
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    return go;
  }

  static void Pad(Transform parent, string name, Vector3 pos, float diameter, Color color) {
    GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pad.name = name;
    pad.transform.SetParent(parent, false);
    pad.transform.localPosition = pos;
    pad.transform.localScale = new Vector3(diameter, 0.01f, diameter);
    pad.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(pad);
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

  static GameObject PlaceProp(Transform parent, string prop, string goName, Vector3 pos, float yaw, float scale) {
    GameObject go = PropKit.Place(parent, prop, pos, yaw, scale);
    if (go != null) go.name = goName;
    return go;
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

  // Boards/digits read by children must never catch shadow acne (journey shot:
  // the glyph's seam showed sawtooth shading at gameplay distance).
  static void NoShadows(GameObject go) {
    if (go == null) return;
    foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) {
      if (r == null) continue;
      r.receiveShadows = false;
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
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

// Which tread a ray hit (hover highlight + click identity): carried by the
// tread colliders only, so a click/hover on a step face reads the step number
// without any position math.
[DisallowMultipleComponent]
public class StairTread : MonoBehaviour {
  public StairRun run;
  public int step;
  public void Bind(StairRun stairRun, int stepIndex) { run = stairRun; step = stepIndex; }
}

// The stair geometry contract: ONE place decides which step a world position
// stands on (user brief §12: no scattered "player.y > X" checks). The game
// polls StepAt(); tests drive it directly with synthetic positions.
//   0            = not on the stairs (ground, beside them, or under a tread)
//   1..stepCount = the step being stood on
//   stepCount+1  = the top landing
[DisallowMultipleComponent]
public class StairRun : MonoBehaviour {
  public int stepCount = 6;
  public float rise = 0.19f;
  public float tread = 0.66f;
  public float width = 3.4f;
  public float baseZ = 4.6f;
  public float centerX = 0f;
  public float landingDepth = 3.2f;
  public float landingHalfWidth = 2.7f;
  // How far above/below the exact tread top a body may be and still count as
  // standing on it (the NavMesh approximates stairs as a ramp, so the agent's
  // y rides the ramp — this window absorbs the half-rise difference).
  public float yTolerance = 0.32f;
  public float xMargin = 0.12f;

  // The step band a position belongs to by XZ, or 0 when outside the run/landing.
  public int BandAt(Vector3 worldPos) {
    Vector3 p = transform.InverseTransformPoint(worldPos);
    if (Mathf.Abs(p.x - centerX) > width * 0.5f + xMargin) return 0;
    float z = p.z - baseZ;
    if (z < 0f) return 0;
    int i = Mathf.FloorToInt(z / tread) + 1;
    if (i <= stepCount) return i;
    float landingEnd = stepCount * tread + landingDepth;
    if (z <= landingEnd && Mathf.Abs(p.x - centerX) <= landingHalfWidth + xMargin)
      return stepCount + 1;
    return 0;
  }

  // The deterministic "current step": band + the physical height check (a body
  // standing UNDER the stairs reads 0, not a phantom step).
  public int StepAt(Vector3 worldPos) {
    int band = BandAt(worldPos);
    if (band <= 0) return 0;
    float top = TopY(band);
    Vector3 p = transform.InverseTransformPoint(worldPos);
    if (Mathf.Abs(p.y - top) > yTolerance) return 0;
    return band;
  }

  // World-space height of a step's tread (0 = ground).
  public float TopY(int step) {
    if (step <= 0) return transform.position.y;
    int capped = Mathf.Min(step, stepCount);
    return transform.position.y + capped * rise;
  }

  // Centre of a tread in root-local coordinates (the lesson walks the student
  // here; the builder exposes the same point for camera/gesture targets).
  public Vector3 StandLocal(int step) {
    int i = Mathf.Clamp(step, 1, stepCount);
    return new Vector3(centerX, i * rise, baseZ + (i - 0.5f) * tread);
  }
}
