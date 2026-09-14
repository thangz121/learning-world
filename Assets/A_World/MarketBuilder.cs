// A_World/MarketBuilder.cs — Agent A (World & Visual), W1 vertical slice.
// Builds the ENTIRE constrained 3D market mini-world in code (Awake), so the
// scene file stays a one-GameObject shell. Fixed world contract (metres):
//   ground X[-8,8] Z[-6,6] | player spawn (0,0,4.5) | Milo anchor (2.5,0,1.5)
//   Mia stall anchor (-3.5,0,-2.5) | apple crate (3.5,0,-2.0) | flower bed (-4.6,0,5.0)
// Visuals are URP/Lit colored materials (no greybox): green grass, warm path,
// sky-blue background + fog, striped awning stall, leafy tree, hedge boundary.
// (R9: fence -> hedge; flower bed compact + parked in the SW corner.)
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
  // Phase-1 closure: Milo hosts the market stall front (his place; Mia keeps
  // the counter as shopkeeper), so spawn path + onboarding lead southwest.
  public static readonly Vector3 PlayerSpawn = new Vector3(0f, 0f, 4.5f);
  // R5V-2 composition: Milo moves EAST to his own place on the path
  // (0.0,-0.8), 3.89m from Mia (-3.5,-2.5) — clears the 3.6m minimum floor so
  // the two anchors stop competing in one frame. Camera midpoints derive from
  // these anchors, so intro/celebrate poses follow automatically.
  public static readonly Vector3 MiloAnchorPos = new Vector3(0f, 0f, -0.8f);
  public static readonly Vector3 MiaAnchorPos = new Vector3(-3.5f, 0f, -2.5f);
  public static readonly Vector3 CrateAnchorPos = new Vector3(3.5f, 0f, -2.0f);
  // R9 (player report: the flower bed ate frame + camera): compact cluster
  // (~0.7m tall, was ~1.2m) parked in the SW corner, off every quest path and
  // behind the default follow camera — a reward nook, never an occluder.
  // R9b: 2m clear of the west tree (was hugging its trunk, stalks unreadable).
  public static readonly Vector3 FlowerAnchorPos = new Vector3(-4.6f, 0f, 5.0f);
  public const float BoundX = 8f;
  public const float BoundZ = 6f;

  // Lead wiring surface (assigned in Awake; bound in BuildServices).
  public ClickToMove Player { get; private set; }
  public Interactable Apple { get; private set; }
  public GameObject FlowerRoot { get; private set; }
  public Transform MiloAnchor { get; private set; }
  public Transform MiaAnchor { get; private set; }
  public GameObject StallCounter { get; private set; }
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
  public CursorPresenter Cursor { get; private set; }
  public ProximityDiscovery AppleDiscovery { get; private set; }

  IGameEventBus _bus;

  void Awake() {
    BuildEnvironment();
    BuildStall();
    BuildTreeAndHedge();
    BuildAppleCrate();
    BuildFlowerBed();
    BuildDistractor();
    BuildBubble();
    // NavMesh bakes BEFORE the player exists: the agent enables against a
    // valid NavMesh (no "failed to create agent"), and the player capsule
    // itself is excluded from the baked geometry.
    BuildNavMesh();
    BuildNavCarves();
    BuildMiloMat(); // R5V-b: post-NavMesh so the bake never sees it
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
    if (AppleDiscovery != null) AppleDiscovery.Bind(bus, audio, Player, Router);
    if (Router != null) {
      Router.Bind(bus, audio);
      Router.AttachPlayer(Player);
    }
    if (WorldCamera != null) WorldCamera.Bind(bus);
    // R5j: ride the FIST bone (reads as "held"). The old root-child anchor
    // floated beside the head (R5i photo proof); kept only as fallback.
    // R9: the pickable distractor ball rides the SAME hand (one hand, one
    // item — the swap rule keeps carry exclusive, see DistractorChoice).
    Transform handAnchor = (PlayerViz != null && PlayerViz.HandBone != null)
      ? PlayerViz.HandBone : PlayerHand;
    if (ApplePresenter != null) {
      ApplePresenter.Bind(bus);
      ApplePresenter.SetCrateApple(CrateApple);
      ApplePresenter.AttachHand(handAnchor);
    }
    if (Distractor != null) Distractor.SetHand(handAnchor);
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
    // R5V-1 stylized daylight (was 0.75/0.78/0.82: faces washed, grass neon):
    // softer ambient keeps skin volume without blowing out the lawn.
    RenderSettings.ambientLight = new Color(0.68f, 0.71f, 0.75f);
    RenderSettings.ambientIntensity = 1f;

    GameObject sun = new GameObject("DirectionalLight");
    sun.transform.SetParent(transform);
    // R5V-1: higher sun (50 -> 62 deg) shortens the dominating long shadows;
    // due-south-east yaw keeps faces lit from the gameplay camera side.
    // R5V-b (r5-wrong/complete photos: stall shadow covers ~40% frame, fence
    // stripes dominate foreground): 62 -> 68 deg + strength 0.72 -> 0.65.
    // Candidate values only: faces + contact shadows in photos decide.
    sun.transform.rotation = Quaternion.Euler(68f, -35f, 0f);
    Light light = sun.AddComponent<Light>();
    light.type = LightType.Directional;
    light.color = new Color(1f, 0.96f, 0.88f); // warm sunlight
    light.intensity = 0.95f; // R5V-1: 1.1 blew out path + brow speculars
    light.shadows = LightShadows.Soft;
    light.shadowStrength = 0.65f; // R5V-1: full-strength shadows dominated composition
    // R5V-b: 0.72 -> 0.65 (r5-low-mia: stall shadow filled half the frame).

    // Green grass ground covering exactly X[-8,8] Z[-6,6].
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "Ground";
    ground.transform.SetParent(transform);
    ground.transform.position = Vector3.zero;
    ground.transform.localScale = new Vector3(1.6f, 1f, 1.2f); // 10m plane -> 16x12m
    // R5V-1: toned down (0.35,0.68,0.32 glowed neon under sun+ambient).
    ground.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.33f, 0.62f, 0.30f));

    // R5V-1 outer world: a darker skirt far below/outside the fence so the
    // playable lawn reads as a place inside a larger world, not a floating
    // island in sky-blue void. Unlit-cheap, no gameplay, no NavMesh.
    GameObject outer = GameObject.CreatePrimitive(PrimitiveType.Plane);
    outer.name = "OuterGround";
    outer.transform.SetParent(transform);
    outer.transform.position = new Vector3(0f, -0.12f, 0f);
    outer.transform.localScale = new Vector3(6f, 1f, 6f);
    outer.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.24f, 0.47f, 0.33f));

    // Warm path stripe from spawn toward Milo's place (onboarding: the
    // world itself points first-time players at Milo; HUD stays secondary).
    // R5V-2: re-aimed straight NORTH spawn (0,4.5) -> Milo (0,-0.8).
    GameObject path = GameObject.CreatePrimitive(PrimitiveType.Cube);
    path.name = "Path";
    path.transform.SetParent(transform);
    path.transform.position = new Vector3(0f, 0.015f, 1.85f);
    path.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    path.transform.localScale = new Vector3(2.2f, 0.03f, 5.3f);
    // R4 (P1Survey p2-approach/walk 2026-09-13): (0.92,0.78,0.55) under the
    // 1.1 warm sun + 0.75 ambient blew out to near-white carpet. (0.76,0.60,
    // 0.40) renders as the intended warm tan under the same lighting.
    path.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.76f, 0.60f, 0.40f));

    // EventSystem required for the HUD replay button (New Input System module).
    GameObject uiEvents = new GameObject("EventSystem");
    uiEvents.transform.SetParent(transform);
    uiEvents.AddComponent<EventSystem>();
    uiEvents.AddComponent<InputSystemUIInputModule>();
  }

  // ---- market stall with striped awning (Milo's place; Mia keeps counter) ----
  // Phase-1 closure readability pass: every element audited (A gameplay /
  // B identity / C composition). Counter (A+B shop landmark, lowered so Mia
  // reads over it), 4 slim posts + 4 narrow slats (B market identity, raised
  // so the canopy clears heads and gameplay sightlines). Removed: 2 extra
  // slats + counter bulk that dominated the frame and hid Mia (survey:
  // sightlines BLOCKED_BY AwningStripe, stall-S camera dive).

  void BuildStall() {
    GameObject stall = new GameObject("MarketStall");
    stall.transform.SetParent(transform);
    // R5V-b (r5-talk/wrong/face4m: Mia at (-3.5,-2.5) reads as SITTING on the
    // counter — only 0.4m south of its front face): stall rides 0.5m further
    // north (-0.9 -> -1.4) so Mia stands 0.9m clear of the counter like a
    // shopkeeper before her shop. Mia/post/anchor-derived cams/bubble/probes
    // all stay untouched; only the stall + its carve move.
    stall.transform.position = new Vector3(MiaAnchorPos.x, 0f, MiaAnchorPos.z - 1.4f);
    Color wood = new Color(0.55f, 0.36f, 0.2f);
    Color cream = new Color(0.99f, 0.95f, 0.86f);
    Color red = new Color(0.85f, 0.25f, 0.22f);

    GameObject counter = GameObject.CreatePrimitive(PrimitiveType.Cube);
    counter.name = "Counter";
    counter.transform.SetParent(stall.transform);
    counter.transform.localPosition = Vector3.zero;
    counter.transform.localScale = new Vector3(2.4f, 0.75f, 1f);
    counter.transform.position = new Vector3(counter.transform.position.x, 0.375f, counter.transform.position.z);
    counter.GetComponent<Renderer>().sharedMaterial = Lit(wood);
    StallCounter = counter; // Lead wires a ClickForwarder so counter taps reach Mia

    for (int i = 0; i < 4; i++) {
      float px = (i % 2 == 0) ? -0.95f : 0.95f;
      float pz = (i < 2) ? -0.45f : 0.45f;
      GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      post.name = "AwningPost";
      post.transform.SetParent(stall.transform);
      post.transform.localPosition = new Vector3(px, 1.5f, pz);
      post.transform.localScale = new Vector3(0.09f, 2.2f, 0.09f);
      post.GetComponent<Renderer>().sharedMaterial = Lit(wood);
    }

    // Striped awning: alternating red/cream slats, tilted for depth readability.
    for (int i = 0; i < 4; i++) {
      GameObject slat = GameObject.CreatePrimitive(PrimitiveType.Cube);
      slat.name = "AwningStripe";
      slat.transform.SetParent(stall.transform);
      slat.transform.localPosition = new Vector3(-0.93f + i * 0.62f, 2.62f, 0f);
      slat.transform.localScale = new Vector3(0.62f, 0.08f, 1.4f);
      slat.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
      slat.GetComponent<Renderer>().sharedMaterial = Lit(i % 2 == 0 ? red : cream);
    }
  }

  // ---- tree + hedge boundary --------------------------------------------------

  void BuildTreeAndHedge() {
    BuildTree(new Vector3(-6.2f, 0f, 3.8f), 1f);
    // R5V-1 background depth: two OUTSIDE trees so the boundary reads as a
    // garden edge inside a larger world (foreground path / midground play /
    // background green), plus three small bushes inside corners for charm.
    // Each answers WHY: orientation + horizon depth, never clutter.
    BuildTree(new Vector3(-11f, -0.1f, -3f), 1.6f);
    BuildTree(new Vector3(10.5f, -0.1f, 4.5f), 1.4f);
    BuildTree(new Vector3(3f, -0.1f, -10.5f), 1.8f);
    BuildBush(new Vector3(-6.8f, 0f, -4.8f));
    BuildBush(new Vector3(6.8f, 0f, -4.8f));
    BuildBush(new Vector3(6.8f, 0f, 4.8f));
    BuildHedgeEdge();
  }

  void BuildTree(Vector3 pos, float s) {
    GameObject tree = new GameObject("Tree");
    tree.transform.SetParent(transform);
    tree.transform.position = pos;
    tree.transform.localScale = Vector3.one * s;
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
  }

  // R5V-1 corner bush: one squashed canopy sphere (orientation + charm).
  void BuildBush(Vector3 pos) {
    GameObject bush = new GameObject("Bush");
    bush.transform.SetParent(transform);
    bush.transform.position = pos;
    GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    leaf.name = "BushLeaf";
    leaf.transform.SetParent(bush.transform);
    leaf.transform.localPosition = new Vector3(0f, 0.35f, 0f);
    leaf.transform.localScale = new Vector3(1.1f, 0.7f, 1.1f);
    leaf.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.28f, 0.60f, 0.30f));
  }

  // R9 (player report: the fence reads as a tiger cage): the play-area edge
  // is a low garden HEDGE now — rounded bushes + tiny flower tufts along the
  // same boundary footprint (carves + router bounds untouched). Nothing rises
  // above ~0.8m: sightlines stay open from every camera, faces never occluded.
  void BuildHedgeEdge() {
    GameObject hedge = new GameObject("HedgeEdge");
    hedge.transform.SetParent(transform);
    Color leafA = new Color(0.28f, 0.60f, 0.30f);
    Color leafB = new Color(0.22f, 0.52f, 0.28f);
    Color[] tuft = {
      new Color(0.95f, 0.55f, 0.65f),
      new Color(0.98f, 0.82f, 0.30f),
      new Color(0.96f, 0.95f, 0.90f),
    };
    // Deterministic alternation (never Random: every build is identical).
    int n = 0;
    for (float x = -8f; x <= 8.01f; x += 1.6f) {
      AddHedgeBush(hedge.transform, new Vector3(x, 0.28f, -6f), n);
      AddHedgeBush(hedge.transform, new Vector3(x, 0.28f, 6f), n + 1);
      if (n % 4 == 1) AddFlowerTuft(hedge.transform, new Vector3(x, 0f, -6f), tuft[(n / 4) % 3]);
      n++;
    }
    for (float z = -4.4f; z <= 4.41f; z += 1.6f) {
      AddHedgeBush(hedge.transform, new Vector3(-8f, 0.28f, z), n);
      AddHedgeBush(hedge.transform, new Vector3(8f, 0.28f, z), n + 1);
      n++;
    }

    void AddHedgeBush(Transform parent, Vector3 pos, int seed) {
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "HedgeBush";
      bush.transform.SetParent(parent);
      bush.transform.position = pos;
      bush.transform.localScale = (seed % 2 == 0)
        ? new Vector3(1.15f, 0.62f, 1.15f)
        : new Vector3(0.95f, 0.55f, 0.95f);
      bush.GetComponent<Renderer>().sharedMaterial = Lit(seed % 2 == 0 ? leafA : leafB);
      // Collider kept (same bake/click behavior the fence posts had).
    }

    void AddFlowerTuft(Transform parent, Vector3 groundPos, Color color) {
      GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      stem.name = "HedgeTuftStem";
      stem.transform.SetParent(parent);
      stem.transform.position = groundPos + new Vector3(0f, 0.5f, 0f);
      stem.transform.localScale = new Vector3(0.05f, 0.35f, 0.05f);
      stem.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.25f, 0.55f, 0.28f));
      GameObject bloom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bloom.name = "HedgeTuftBloom";
      bloom.transform.SetParent(parent);
      bloom.transform.position = groundPos + new Vector3(0f, 0.72f, 0f);
      bloom.transform.localScale = new Vector3(0.13f, 0.13f, 0.13f);
      bloom.GetComponent<Renderer>().sharedMaterial = Lit(color);
    }
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
    // R8 (player report: range feels too generous): 2.5 -> 2.0m — the child
    // must walk visibly UP TO the crate, not snipe it across the lawn. Still
    // forgiving (no pixel-hunting); proximity discovery follows automatically.
    Apple.interactionDistance = 2.0f;
    Apple.ParseIds(); // fields assigned post-Awake: re-parse or events drop
    AppleDiscovery = apple.AddComponent<ProximityDiscovery>();
  }

  // ---- hidden flower-pot group (revealed by FlowerPotPresenter) ----------------

  void BuildFlowerBed() {
    GameObject bed = new GameObject("FlowerBed");
    bed.transform.SetParent(transform);
    bed.transform.position = FlowerAnchorPos;

    GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pot.name = "FlowerPot";
    pot.transform.SetParent(bed.transform);
    // R9 compact cluster (player report: the old bed ate frame + camera):
    // smaller pot, shorter stalks, tighter blooms — ~0.7m tall total (was
    // ~1.2m), still 3 blooms. Parked in the SW corner (see FlowerAnchorPos).
    pot.transform.localPosition = new Vector3(0f, 0.17f, 0f);
    pot.transform.localScale = new Vector3(0.56f, 0.34f, 0.56f);
    pot.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.8f, 0.42f, 0.25f));

    Color[] bloom = {
      new Color(0.95f, 0.45f, 0.6f),
      new Color(0.98f, 0.8f, 0.25f),
      new Color(0.95f, 0.45f, 0.6f),
    };
    for (int i = 0; i < 3; i++) {
      float ox = (i - 1) * 0.13f;
      GameObject stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      stalk.name = "FlowerStalk";
      stalk.transform.SetParent(bed.transform);
      stalk.transform.localPosition = new Vector3(ox, 0.44f, 0f);
      stalk.transform.localScale = new Vector3(0.07f, 0.3f, 0.07f);
      stalk.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.25f, 0.55f, 0.28f));
      GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      head.name = "FlowerHead";
      head.transform.SetParent(bed.transform);
      head.transform.localPosition = new Vector3(ox, 0.66f, 0f);
      head.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
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
    // R5V-2: 3.22m from the apple crate (was 1.63m: a tiny red-on-red cluster
    // unreadable at 4yo). East-south placement keeps both on the same side.
    stand.transform.position = new Vector3(5.4f, 0f, 0.6f);

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
    // R5V-2: BLUE ball (was near-identical red: apple 0.85/0.15/0.15 vs ball
    // 0.80/0.12/0.18). Color + position + context now differ (Roblox visual-
    // language rule: never color-alone, so silhouette context differs too:
    // ball on tall pedestal vs apple in low crate). Name kept for survey/tests.
    ball.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.20f, 0.42f, 0.90f));

    Distractor = ball.AddComponent<DistractorChoice>();
  }

  // ---- world-anchored visual question bubble (beside Mia's stall) --------------
  // Shows WHAT Mia is asking (mini apple icon). Driven by quest phases in
  // MarketBootstrap (Show on start, Hide on complete). Reusable: swap icon.
  // Final polish: parked EAST of Mia (not overhead) so it never overlaps her
  // name label, and stays clear of the awning volume.

  void BuildBubble() {
    GameObject bubbleGo = new GameObject("QuestionBubble");
    bubbleGo.transform.SetParent(transform);
    Bubble = bubbleGo.AddComponent<WorldQuestionBubble>();
    // R9: placement goes through the shared contract (AnchorFor) — every
    // future NPC parks its hint the same way (east-south, head height, clear
    // of labels/faces/awnings). Icon-first thought language inside.
    Bubble.Place(WorldQuestionBubble.AnchorFor(MiaAnchorPos));
  }

  // R5V-b Milo's place: a flat round mat under Milo's anchor (his identity +
  // orientation cue — he no longer stands on bare grass). WHY: separates his
  // anchor from Mia's stall in screen space, gives the onboarding path a
  // destination. Flat (no trip), collider destroyed (never eats ground
  // clicks), matte (never glows). Post-NavMesh: not baked, not walkable.
  void BuildMiloMat() {
    GameObject mat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    mat.name = "MiloMat";
    mat.transform.SetParent(transform);
    mat.transform.position = new Vector3(MiloAnchorPos.x, 0.012f, MiloAnchorPos.z);
    mat.transform.localScale = new Vector3(2.2f, 0.024f, 2.2f);
    mat.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.45f, 0.60f, 0.85f));
    Collider c = mat.GetComponent<Collider>();
    if (c != null) Destroy(c);
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
    // R6 walk (FINAL POLISH): 3.5 m/s is a jog for a ~1.3m stylized child
    // (~2.7 body-lengths/s) and skates against the chibi walk cycle. Candidate
    // 2.2 m/s (~1.7 BL/s, brisk child walk) to match foot cycle to root
    // translation. Walk burst macros decide (raise if it reads as trudging,
    // lower if feet still skate).
    agent.speed = 2.2f;
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

    // R7 software cursor (presentation only: no bus, no services). Built with
    // the frame services so it exists from the first rendered frame; hides
    // the hardware arrow and highlights clickables on hover by itself.
    GameObject cursorGo = new GameObject("MouseCursor");
    cursorGo.transform.SetParent(transform);
    Cursor = cursorGo.AddComponent<CursorPresenter>();
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
    CarveBox("StallCarve", new Vector3(MiaAnchorPos.x, 0.5f, MiaAnchorPos.z - 1.4f), new Vector3(2.6f, 1f, 1.4f));
    CarveBox("CrateCarve", new Vector3(CrateAnchorPos.x, 0.3f, CrateAnchorPos.z), new Vector3(1.2f, 0.6f, 1.2f));
    CarveBox("PedestalCarve", new Vector3(5.4f, 0.4f, 0.6f), new Vector3(0.9f, 0.8f, 0.9f));
    // R9: fence -> hedge (same footprint, renamed with the visuals).
    CarveBox("EdgeCarveN", new Vector3(0f, 0.5f, -6f), new Vector3(16.4f, 1f, 0.4f));
    CarveBox("EdgeCarveS", new Vector3(0f, 0.5f, 6f), new Vector3(16.4f, 1f, 0.4f));
    CarveBox("EdgeCarveW", new Vector3(-8f, 0.5f, 0f), new Vector3(0.4f, 1f, 12.4f));
    CarveBox("EdgeCarveE", new Vector3(8f, 0.5f, 0f), new Vector3(0.4f, 1f, 12.4f));
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
    // R5V-1 unified stylized finish: matte environment (specular highlights
    // on grass/path read as neon/glow under the warm sun).
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    return mat;
  }
}
