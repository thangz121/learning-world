// A_World/MarketBuilder.cs — Agent A (World & Visual), W1 vertical slice.
// Builds the ENTIRE constrained 3D market mini-world in code (Awake), so the
// scene file stays a one-GameObject shell. Fixed world contract (metres):
//   ground X[-8,8] Z[-6,6] | player spawn (0,0,4.5) | Milo anchor (2.5,0,1.5)
//   Mia stall anchor (-3.5,0,-2.5) | apple crate (3.5,0,-2.0) | flower bed (-1.5,0,2.5)
// Visuals are URP/Lit colored materials (no greybox): green grass, warm path,
// sky-blue background + fog, striped awning stall, leafy tree, fence boundary.
// Wiring: Lead calls BuildServices(bus, audio) after scene load (GameInstaller
// owns the services; this class NEVER news them). Presenters stay exposed so
// the Lead can re-wire/verify from other agents' components.
// C# 9.0 only. No legacy Input. No TTS/Worker calls (audio via IAudioDirector).
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

[DisallowMultipleComponent]
public class MarketBuilder : MonoBehaviour {
  // Fixed world contract (metres). Single source of truth for W1 coordinates.
  public static readonly Vector3 PlayerSpawn = new Vector3(0f, 0f, 4.5f);
  public static readonly Vector3 MiloAnchorPos = new Vector3(2.5f, 0f, 1.5f);
  public static readonly Vector3 MiaAnchorPos = new Vector3(-3.5f, 0f, -2.5f);
  public static readonly Vector3 CrateAnchorPos = new Vector3(3.5f, 0f, -2.0f);
  public static readonly Vector3 FlowerAnchorPos = new Vector3(-1.5f, 0f, 2.5f);
  public const float BoundX = 8f;
  public const float BoundZ = 6f;

  // Lead wiring surface (assigned in Awake; bound in BuildServices).
  public ClickToMove Player { get; private set; }
  public Interactable Apple { get; private set; }
  public GameObject FlowerRoot { get; private set; }
  public Transform MiloAnchor { get; private set; }
  public Transform MiaAnchor { get; private set; }
  public ClickRouter Router { get; private set; }
  public MarketHUD Hud { get; private set; }
  public SmartCamera WorldCamera { get; private set; }
  public ApplePresenter ApplePresenter { get; private set; }
  public FlowerPotPresenter FlowerPresenter { get; private set; }
  public Transform PlayerHand { get; private set; }
  public GameObject CrateApple { get; private set; }
  public PlayerVisual PlayerViz { get; private set; }
  public DistractorChoice Distractor { get; private set; }
  public WorldQuestionBubble Bubble { get; private set; }

  IGameEventBus _bus;

  void Awake() {
    BuildEnvironment();
    BuildStall();
    BuildTreeAndFences();
    BuildAppleCrate();
    BuildFlowerBed();
    BuildDistractor();
    BuildBubble();
    // NavMesh bakes BEFORE the player exists: the agent enables against a
    // valid NavMesh (no "failed to create agent"), and the player capsule
    // itself is excluded from the baked geometry.
    BuildNavMesh();
    BuildNavCarves();
    BuildPlayer();
    BuildCamera();
    BuildFrameServices();
  }

  // Injection boundary (Lead/GameInstaller calls this; services are passed in,
  // never newed here). Null-safe so missing wiring degrades to warnings, not NREs.
  public void BuildServices(IGameEventBus bus, IAudioDirector audio) {
    _bus = bus;
    if (bus == null) {
      Debug.LogWarning("[MarketBuilder] BuildServices called with null bus; world runs unbound.", this);
      return;
    }
    if (Player != null) Player.Bind(bus);
    if (PlayerViz != null) PlayerViz.Bind(bus);
    if (Apple != null) Apple.Bind(bus);
    if (Router != null) {
      Router.Bind(bus, audio);
      Router.AttachPlayer(Player);
    }
    if (WorldCamera != null) WorldCamera.Bind(bus);
    if (ApplePresenter != null) {
      ApplePresenter.Bind(bus);
      ApplePresenter.SetCrateApple(CrateApple);
      ApplePresenter.AttachHand(PlayerHand);
    }
    if (FlowerPresenter != null) {
      FlowerPresenter.Bind(bus);
      FlowerPresenter.SetFlowerRoot(FlowerRoot);
    }
    // HUD needs IQuestService too, which this signature does not carry:
    // bind bus now, Lead completes quest wiring via WireQuestService.
    if (Hud != null) Hud.Bind(bus, null);
  }

  // Second wiring step for the Lead (IQuestService/IHintService live outside
  // the BuildServices signature). Binds the HUD quest line and the reusable
  // distractor choice prop.
  public void WireQuestService(IQuestService quests, IHintService hints) {
    if (Hud != null) Hud.Bind(_bus, quests);
    if (Distractor != null) Distractor.Bind(_bus, hints, quests);
  }

  // ---- environment: sky, light, ground, path --------------------------------

  void BuildEnvironment() {
    RenderSettings.fog = true;
    RenderSettings.fogMode = FogMode.Linear;
    RenderSettings.fogColor = new Color(0.75f, 0.88f, 0.96f);
    RenderSettings.fogStartDistance = 18f;
    RenderSettings.fogEndDistance = 45f;
    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    RenderSettings.ambientLight = new Color(0.75f, 0.78f, 0.82f);
    RenderSettings.ambientIntensity = 1f;

    GameObject sun = new GameObject("DirectionalLight");
    sun.transform.SetParent(transform);
    sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    Light light = sun.AddComponent<Light>();
    light.type = LightType.Directional;
    light.color = new Color(1f, 0.96f, 0.88f); // warm sunlight
    light.intensity = 1.1f;
    light.shadows = LightShadows.Soft;

    // Green grass ground covering exactly X[-8,8] Z[-6,6].
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "Ground";
    ground.transform.SetParent(transform);
    ground.transform.position = Vector3.zero;
    ground.transform.localScale = new Vector3(1.6f, 1f, 1.2f); // 10m plane -> 16x12m
    ground.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.35f, 0.68f, 0.32f));

    // Warm path stripe from spawn toward the market centre.
    GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
    path.name = "Path";
    path.transform.SetParent(transform);
    path.transform.position = new Vector3(0f, 0.02f, 1.2f);
    path.transform.localScale = new Vector3(2.2f, 0.04f, 7f);
    path.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.92f, 0.78f, 0.55f));

    // EventSystem required for the HUD replay button (New Input System module).
    GameObject uiEvents = new GameObject("EventSystem");
    uiEvents.transform.SetParent(transform);
    uiEvents.AddComponent<EventSystem>();
    uiEvents.AddComponent<InputSystemUIInputModule>();
  }

  // ---- market stall with striped awning (near Mia anchor) --------------------

  void BuildStall() {
    GameObject stall = new GameObject("MarketStall");
    stall.transform.SetParent(transform);
    stall.transform.position = new Vector3(MiaAnchorPos.x, 0f, MiaAnchorPos.z - 0.9f);
    Color wood = new Color(0.55f, 0.36f, 0.2f);
    Color cream = new Color(0.99f, 0.95f, 0.86f);
    Color red = new Color(0.85f, 0.25f, 0.22f);

    GameObject counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
    counter.name = "Counter";
    counter.transform.SetParent(stall.transform);
    counter.transform.localPosition = Vector3.zero;
    counter.transform.localScale = new Vector3(3f, 0.9f, 1.2f);
    counter.transform.position = new Vector3(counter.transform.position.x, 0.45f, counter.transform.position.z);
    counter.GetComponent<Renderer>().sharedMaterial = Lit(wood);

    for (int i = 0; i < 4; i++) {
      float px = (i % 2 == 0) ? -1.4f : 1.4f;
      float pz = (i < 2) ? -0.5f : 0.5f;
      GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      post.name = "AwningPost";
      post.transform.SetParent(stall.transform);
      post.transform.localPosition = new Vector3(px, 1.4f, pz);
      post.transform.localScale = new Vector3(0.12f, 1.8f, 0.12f);
      post.GetComponent<Renderer>().sharedMaterial = Lit(wood);
    }

    // Striped awning: alternating red/cream slats, tilted for depth readability.
    for (int i = 0; i < 6; i++) {
      GameObject slat = GameObject.CreatePrimitive(PrimitiveType.Cube);
      slat.name = "AwningStripe";
      slat.transform.SetParent(stall.transform);
      slat.transform.localPosition = new Vector3(-1.55f + i * 0.62f, 2.35f, 0f);
      slat.transform.localScale = new Vector3(0.62f, 0.08f, 1.9f);
      slat.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
      slat.GetComponent<Renderer>().sharedMaterial = Lit(i % 2 == 0 ? red : cream);
    }
  }

  // ---- tree + fence boundary --------------------------------------------------

  void BuildTreeAndFences() {
    GameObject tree = new GameObject("Tree");
    tree.transform.SetParent(transform);
    tree.transform.position = new Vector3(-6.2f, 0f, 3.8f);
    GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    trunk.name = "Trunk";
    trunk.transform.SetParent(tree.transform);
    trunk.transform.localPosition = new Vector3(0f, 0.9f, 0f);
    trunk.transform.localScale = new Vector3(0.5f, 1.8f, 0.5f);
    trunk.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.45f, 0.3f, 0.16f));
    Vector3[] canopyAt = {
      new Vector3(0f, 2.3f, 0f),
      new Vector3(0.7f, 1.9f, 0.3f),
      new Vector3(-0.6f, 2.0f, -0.3f),
    };
    for (int i = 0; i < canopyAt.Length; i++) {
      GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      leaf.name = "Canopy";
      leaf.transform.SetParent(tree.transform);
      leaf.transform.localPosition = canopyAt[i];
      leaf.transform.localScale = new Vector3(1.6f, 1.3f, 1.6f);
      leaf.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.25f, 0.58f, 0.28f));
    }

    GameObject fence = new GameObject("Fence");
    fence.transform.SetParent(transform);
    Color fenceBrown = new Color(0.6f, 0.42f, 0.25f);
    // Posts every 2m around the X[-8,8] Z[-6,6] boundary + two side rails.
    for (float x = -8f; x <= 8.01f; x += 2f) {
      AddFencePost(fence.transform, fenceBrown, new Vector3(x, 0.45f, -6f));
      AddFencePost(fence.transform, fenceBrown, new Vector3(x, 0.45f, 6f));
    }
    for (float z = -4f; z <= 4.01f; z += 2f) {
      AddFencePost(fence.transform, fenceBrown, new Vector3(-8f, 0.45f, z));
      AddFencePost(fence.transform, fenceBrown, new Vector3(8f, 0.45f, z));
    }
    AddRail(fence.transform, fenceBrown, new Vector3(0f, 0.7f, -6f), new Vector3(16.4f, 0.1f, 0.12f));
    AddRail(fence.transform, fenceBrown, new Vector3(0f, 0.7f, 6f), new Vector3(16.4f, 0.1f, 0.12f));
    AddRail(fence.transform, fenceBrown, new Vector3(-8f, 0.7f, 0f), new Vector3(0.12f, 0.1f, 12.4f));
    AddRail(fence.transform, fenceBrown, new Vector3(8f, 0.7f, 0f), new Vector3(0.12f, 0.1f, 12.4f));
  }

  void AddFencePost(Transform parent, Color color, Vector3 pos) {
    GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
    post.name = "FencePost";
    post.transform.SetParent(parent);
    post.transform.position = pos;
    post.transform.localScale = new Vector3(0.16f, 0.9f, 0.16f);
    post.GetComponent<Renderer>().sharedMaterial = Lit(color);
  }

  void AddRail(Transform parent, Color color, Vector3 pos, Vector3 scale) {
    GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
    rail.name = "FenceRail";
    rail.transform.SetParent(parent);
    rail.transform.position = pos;
    rail.transform.localScale = scale;
    rail.GetComponent<Renderer>().sharedMaterial = Lit(color);
  }

  // ---- apple crate + big red apple (the W1 interaction target) ----------------

  void BuildAppleCrate() {
    GameObject crate = new GameObject("AppleCrate");
    crate.transform.SetParent(transform);
    crate.transform.position = CrateAnchorPos;
    Color crateWood = new Color(0.62f, 0.44f, 0.26f);

    GameObject bottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
    bottom.name = "CrateBottom";
    bottom.transform.SetParent(crate.transform);
    bottom.transform.localPosition = new Vector3(0f, 0.05f, 0f);
    bottom.transform.localScale = new Vector3(1f, 0.1f, 1f);
    bottom.GetComponent<Renderer>().sharedMaterial = Lit(crateWood);
    Vector3[] wallPos = {
      new Vector3(0f, 0.2f, -0.45f), new Vector3(0f, 0.2f, 0.45f),
      new Vector3(-0.45f, 0.2f, 0f), new Vector3(0.45f, 0.2f, 0f),
    };
    for (int i = 0; i < wallPos.Length; i++) {
      GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
      wall.name = "CrateWall";
      wall.transform.SetParent(crate.transform);
      wall.transform.localPosition = wallPos[i];
      wall.transform.localScale = (i < 2)
        ? new Vector3(1f, 0.3f, 0.1f)
        : new Vector3(0.1f, 0.3f, 1f);
      wall.GetComponent<Renderer>().sharedMaterial = Lit(crateWood);
    }

    // Big red apple: sphere + stem + leaf. Interactable lives on the apple root
    // (which carries the SphereCollider the click raycast hits).
    GameObject apple = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    apple.name = "BigApple";
    apple.transform.SetParent(crate.transform);
    apple.transform.localPosition = new Vector3(0f, 0.62f, 0f);
    apple.transform.localScale = new Vector3(0.56f, 0.56f, 0.56f);
    apple.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.85f, 0.15f, 0.15f));

    GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stem.name = "AppleStem";
    stem.transform.SetParent(apple.transform);
    stem.transform.localPosition = new Vector3(0f, 0.62f, 0f);
    stem.transform.localScale = new Vector3(0.12f, 0.4f, 0.12f);
    stem.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.4f, 0.26f, 0.12f));

    GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    leaf.name = "AppleLeaf";
    leaf.transform.SetParent(apple.transform);
    leaf.transform.localPosition = new Vector3(0.22f, 0.55f, 0f);
    leaf.transform.localScale = new Vector3(0.4f, 0.08f, 0.22f);
    leaf.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.25f, 0.6f, 0.25f));

    CrateApple = apple;
    Apple = apple.AddComponent<Interactable>();
    Apple.wordId = "apple";
    Apple.interactionId = "take_apple";
    Apple.npcId = "mia";
    Apple.interactionDistance = 2.5f;
  }

  // ---- hidden flower-pot group (revealed by FlowerPotPresenter) ----------------

  void BuildFlowerBed() {
    GameObject bed = new GameObject("FlowerBed");
    bed.transform.SetParent(transform);
    bed.transform.position = FlowerAnchorPos;

    GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pot.name = "FlowerPot";
    pot.transform.SetParent(bed.transform);
    pot.transform.localPosition = new Vector3(0f, 0.25f, 0f);
    pot.transform.localScale = new Vector3(0.7f, 0.5f, 0.7f);
    pot.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.8f, 0.42f, 0.25f));

    Color[] bloom = {
      new Color(0.95f, 0.45f, 0.6f),
      new Color(0.98f, 0.8f, 0.25f),
      new Color(0.95f, 0.45f, 0.6f),
    };
    for (int i = 0; i < 3; i++) {
      float ox = (i - 1) * 0.25f;
      GameObject stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      stalk.name = "FlowerStalk";
      stalk.transform.SetParent(bed.transform);
      stalk.transform.localPosition = new Vector3(ox, 0.7f, 0f);
      stalk.transform.localScale = new Vector3(0.08f, 0.5f, 0.08f);
      stalk.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.25f, 0.55f, 0.28f));
      GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      head.name = "FlowerHead";
      head.transform.SetParent(bed.transform);
      head.transform.localPosition = new Vector3(ox, 1.05f, 0f);
      head.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
      head.GetComponent<Renderer>().sharedMaterial = Lit(bloom[i]);
    }

    bed.SetActive(false); // hidden until the W1 quest completes
    FlowerRoot = bed;
  }

  // ---- reusable wrong-choice prop (red ball on a pedestal) --------------------
  // A plausible-but-incorrect selectable object for choice-based stories
  // (W1: apple vs ball). IClickTarget (never Interactable) so no vocab audio
  // or Content entries are needed. Bound in WireQuestService.

  void BuildDistractor() {
    GameObject stand = new GameObject("BallStand");
    stand.transform.SetParent(transform);
    stand.transform.position = new Vector3(4.7f, 0f, -0.9f);

    GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pedestal.name = "Pedestal";
    pedestal.transform.SetParent(stand.transform);
    pedestal.transform.localPosition = new Vector3(0f, 0.25f, 0f);
    pedestal.transform.localScale = new Vector3(0.7f, 0.5f, 0.7f);
    pedestal.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.62f, 0.44f, 0.26f));

    GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    ball.name = "RedBall";
    ball.transform.SetParent(stand.transform);
    ball.transform.localPosition = new Vector3(0f, 0.78f, 0f);
    ball.transform.localScale = new Vector3(0.56f, 0.56f, 0.56f);
    ball.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.8f, 0.12f, 0.18f));

    Distractor = ball.AddComponent<DistractorChoice>();
  }

  // ---- world-anchored visual question bubble (above Mia's stall) --------------
  // Shows WHAT Mia is asking (mini apple icon). Driven by quest phases in
  // MarketBootstrap (Show on start, Hide on complete). Reusable: swap icon.

  void BuildBubble() {
    GameObject bubbleGo = new GameObject("QuestionBubble");
    bubbleGo.transform.SetParent(transform);
    Bubble = bubbleGo.AddComponent<WorldQuestionBubble>();
    Bubble.Place(new Vector3(MiaAnchorPos.x, 2.1f, MiaAnchorPos.z + 0.9f));
  }

  // ---- player capsule + anchors -------------------------------------------------

  void BuildPlayer() {
    GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
    player.name = "Player";
    player.transform.SetParent(transform);
    player.transform.position = PlayerSpawn;
    player.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
    player.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.2f, 0.45f, 0.9f));

    NavMeshAgent agent = player.AddComponent<NavMeshAgent>();
    agent.speed = 3.5f;
    agent.angularSpeed = 720f;
    agent.acceleration = 12f;
    agent.stoppingDistance = 0.4f;
    // W1 grounding (W1TOUR AGENT 2026-09-12): a fresh AddComponent agent read
    // baseOffset=1.000 in this Unity version, floating the root at y=0.83 with
    // the NavMesh at 0.03. Pin the agent volume to the capsule it drives:
    // capsule primitive (height 2, radius 0.5) x player scale 0.8.
    agent.baseOffset = 0f;
    agent.height = 1.6f;
    agent.radius = 0.4f;

    Player = player.AddComponent<ClickToMove>();
    PlayerViz = player.AddComponent<PlayerVisual>(); // presentation child (capsule stays for physics, hidden)

    GameObject hand = new GameObject("HandAnchor");
    hand.transform.SetParent(player.transform);
    hand.transform.localPosition = new Vector3(0.45f, 1.25f, 0.3f);
    PlayerHand = hand.transform;

    MiloAnchor = NewAnchor("MiloAnchor", MiloAnchorPos);
    MiaAnchor = NewAnchor("MiaAnchor", MiaAnchorPos);
  }

  Transform NewAnchor(string anchorName, Vector3 pos) {
    GameObject anchor = new GameObject(anchorName);
    anchor.transform.SetParent(transform);
    anchor.transform.position = pos;
    return anchor.transform;
  }

  // ---- constrained camera ---------------------------------------------------------

  void BuildCamera() {
    GameObject camGo = new GameObject("MainCamera");
    camGo.transform.SetParent(transform);
    camGo.tag = "MainCamera";
    Camera cam = camGo.AddComponent<Camera>();
    cam.clearFlags = CameraClearFlags.SolidColor;
    cam.backgroundColor = new Color(0.53f, 0.81f, 0.98f);
    cam.orthographic = false;
    cam.fieldOfView = 55f;
    cam.nearClipPlane = 0.1f;
    cam.farClipPlane = 100f;
    camGo.AddComponent<AudioListener>();
    WorldCamera = camGo.AddComponent<SmartCamera>();
    camGo.transform.position = PlayerSpawn + WorldCamera.defaultOffset;
    camGo.transform.LookAt(PlayerSpawn + Vector3.up);
    if (Player != null) WorldCamera.Follow(Player.transform, WorldCamera.defaultOffset);
    FacePlayerToCamera();
  }

  // Golden spawn presentation (§6.3): the first frame must show the character,
  // not the back of the head. The root yaw is gameplay-authoritative, so this
  // initial facing is presentation seeding only: the NavMeshAgent re-orients
  // toward movement on the first MoveTo (controls are world-space clicks and
  // are never inverted). Physics/collider are rotation-symmetric.
  void FacePlayerToCamera() {
    if (Player == null || WorldCamera == null) return;
    Vector3 toCam = WorldCamera.transform.position - Player.transform.position;
    toCam.y = 0f;
    if (toCam.sqrMagnitude > 0.001f)
      Player.transform.rotation = Quaternion.LookRotation(toCam);
  }

  // ---- frame services (router / HUD / presenters) -----------------------------------

  void BuildFrameServices() {
    GameObject routerGo = new GameObject("ClickRouter");
    routerGo.transform.SetParent(transform);
    Router = routerGo.AddComponent<ClickRouter>();

    GameObject hudGo = new GameObject("MarketHUD");
    hudGo.transform.SetParent(transform);
    Hud = hudGo.AddComponent<MarketHUD>();

    GameObject appleGo = new GameObject("ApplePresenter");
    appleGo.transform.SetParent(transform);
    ApplePresenter = appleGo.AddComponent<ApplePresenter>();

    GameObject flowerGo = new GameObject("FlowerPresenter");
    flowerGo.transform.SetParent(transform);
    FlowerPresenter = flowerGo.AddComponent<FlowerPotPresenter>();
  }

  void BuildNavMesh() {
    GameObject navGo = new GameObject("NavMesh");
    navGo.transform.SetParent(transform);
    NavMeshSurface surface = navGo.AddComponent<NavMeshSurface>();
    surface.collectObjects = CollectObjects.All;
    surface.BuildNavMesh();
  }

  // W1 grounding 2026-09-12: the All-geometry bake turns prop tops (tree
  // canopy, stall counter, crate, pedestal, fence rails) into walkable
  // islands, and the agent climbs them (tour measured player rootY=0.817
  // after walking next to the tree; an earlier shot caught fence-balancing).
  // Stationary carve volumes keep the agent on the grass. Interaction reach
  // (apple 2.5m, NPC clicks) is unaffected: carves only deny foot placement.
  void BuildNavCarves() {
    CarveBox("TreeCarve", new Vector3(-6.2f, 1f, 3.8f), new Vector3(1.4f, 2f, 1.4f));
    CarveBox("StallCarve", new Vector3(MiaAnchorPos.x, 0.5f, MiaAnchorPos.z - 0.9f), new Vector3(3.2f, 1f, 1.6f));
    CarveBox("CrateCarve", new Vector3(CrateAnchorPos.x, 0.3f, CrateAnchorPos.z), new Vector3(1.2f, 0.6f, 1.2f));
    CarveBox("PedestalCarve", new Vector3(4.7f, 0.4f, -0.9f), new Vector3(0.9f, 0.8f, 0.9f));
    CarveBox("FenceCarveN", new Vector3(0f, 0.5f, -6f), new Vector3(16.4f, 1f, 0.4f));
    CarveBox("FenceCarveS", new Vector3(0f, 0.5f, 6f), new Vector3(16.4f, 1f, 0.4f));
    CarveBox("FenceCarveW", new Vector3(-8f, 0.5f, 0f), new Vector3(0.4f, 1f, 12.4f));
    CarveBox("FenceCarveE", new Vector3(8f, 0.5f, 0f), new Vector3(0.4f, 1f, 12.4f));
  }

  void CarveBox(string carveName, Vector3 pos, Vector3 size) {
    GameObject go = new GameObject(carveName);
    go.transform.SetParent(transform);
    go.transform.position = pos;
    NavMeshObstacle obstacle = go.AddComponent<NavMeshObstacle>();
    obstacle.shape = NavMeshObstacleShape.Box;
    obstacle.center = Vector3.zero;
    obstacle.size = size;
    obstacle.carving = true;
    obstacle.carveOnlyStationary = true;
  }

  static Material Lit(Color color) {
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    return mat;
  }
}
