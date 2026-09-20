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
using System.Collections.Generic;
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
  // Phase 3.0: extended Learning World bounds (districts + outer hedge).
  // Main World X[-8,8] Z[-6,6] is untouched; these cover roads + playgrounds.
  public const float BoundX = 16f;
  public const float BoundZ = 14f;

  // Lead wiring surface (assigned in Awake; bound in BuildServices).
  public ClickToMove Player { get; private set; }
  public Interactable Apple { get; private set; }
  public Interactable Ball { get; private set; }
  public GameObject FlowerRoot { get; private set; }
  public Transform MiloAnchor { get; private set; }
  public Transform MiaAnchor { get; private set; }
  public GameObject StallCounter { get; private set; }
  public ClickRouter Router { get; private set; }
  public MarketHUD Hud { get; private set; }
  public SmartCamera WorldCamera { get; private set; }
  public ApplePresenter ApplePresenter { get; private set; }
  public BallPresenter BallPresenter { get; private set; }
  public FlowerPotPresenter FlowerPresenter { get; private set; }
  public Transform PlayerHand { get; private set; }
  public GameObject CrateApple { get; private set; }
  public GameObject CrateBall { get; private set; }
  public PlayerVisual PlayerViz { get; private set; }
  public DistractorChoice Distractor { get; private set; }
  public WorldQuestionBubble Bubble { get; private set; }
  public CursorPresenter Cursor { get; private set; }
  public ProximityDiscovery AppleDiscovery { get; private set; }
  public ProximityDiscovery BallDiscovery { get; private set; }
  public GameAudioTap GameTap { get; private set; }

  IGameEventBus _bus;

  void Awake() {
    BuildEnvironment();
    BuildStall();
    BuildTreeAndHedge();
    BuildAppleCrate();
    BuildBallCrate();
    BuildFlowerBed();
    BuildDistractor();
    BuildBubble();
    // Phase 3.0: Learning World shell BEFORE the bake (roads + medallions are
    // walkable; pillars/cores/trees bake as geometry and get runtime carves).
    _worldResult = SubjectWorldBuilder.BuildShell(transform);
    // NavMesh bakes BEFORE the player exists: the agent enables against a
    // valid NavMesh (no "failed to create agent"), and the player capsule
    // itself is excluded from the baked geometry.
    BuildNavMesh();
    BuildNavCarves();
    SubjectWorldBuilder.BuildCarves(_worldResult, AddCarve); // Phase 3.0: gate/core/tree/boundary/outer carves
    BuildMiloMat(); // R5V-b: post-NavMesh so the bake never sees it
    BuildAmbientDecor(); // Phase 2.4: post-bake ambient (pure visual, no carve)
    SubjectWorldBuilder.BuildDecor(transform); // Phase 3.0: post-bake dressing (collider-free)
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
    if (Ball != null) Ball.Bind(bus);
    if (BallDiscovery != null) BallDiscovery.Bind(bus, audio, Player, Router);
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
    if (BallPresenter != null) {
      BallPresenter.Bind(bus);
      BallPresenter.SetCrateBall(CrateBall);
      BallPresenter.AttachHand(handAnchor);
    }
    if (Distractor != null) Distractor.SetHand(handAnchor);
    if (FlowerPresenter != null) {
      FlowerPresenter.Bind(bus);
      FlowerPresenter.SetFlowerRoot(FlowerRoot);
    }
    if (BallDiscovery != null) BallDiscovery.Bind(bus, audio, Player, Router);
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

  // ---- Phase 3.0 Learning World wiring ----------------------------------------
  // The shell result is built in Awake (pre-NavMesh); gates are bound here,
  // after scene load, when the nav service + player both exist.

  SubjectWorldBuilder.BuildResult _worldResult;
  IWorldNavService _worldNav;

  public IWorldNavService WorldNav {
    get { return _worldNav; }
  }

  // Runtime carve entry point for SubjectWorldBuilder (same obstacle pattern
  // as BuildNavCarves: stationary carving, no rebake).
  public void AddCarve(string carveName, Vector3 pos, Vector3 size) {
    CarveBox(carveName, pos, size);
  }

  // Pushes the nav service into every subject gate (entry one-way in, return
  // one-way out). Null-safe: an unbound world runs exactly as before.
  public void SetWorldNav(IWorldNavService nav) {
    _worldNav = nav;
    if (_worldResult == null || nav == null || Player == null) return;
    Transform playerT = Player.transform;
    for (int i = 0; i < SubjectCatalog.All.Length && i < _worldResult.EntryGates.Count; i++) {
      SubjectGate g = _worldResult.EntryGates[i];
      if (g != null) g.Bind(nav, SubjectCatalog.All[i].Id, false, playerT);
    }
    for (int i = 0; i < SubjectCatalog.All.Length && i < _worldResult.ReturnGates.Count; i++) {
      SubjectGate g = _worldResult.ReturnGates[i];
      if (g != null) g.Bind(nav, SubjectCatalog.All[i].Id, true, playerT);
    }
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
    ground.transform.localScale = new Vector3(3.8f, 1f, 3.2f); // 10m plane -> 38x32m (Phase 3.0: covers districts)
    // R5V-1: toned down (0.35,0.68,0.32 glowed neon under sun+ambient).
    ground.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.33f, 0.62f, 0.30f));

    // R5V-1 outer world: a darker skirt far below/outside the fence so the
    // playable lawn reads as a place inside a larger world, not a floating
    // island in sky-blue void. Unlit-cheap, no gameplay, no NavMesh.
    GameObject outer = GameObject.CreatePrimitive(PrimitiveType.Plane);
    outer.name = "OuterGround";
    outer.transform.SetParent(transform);
    outer.transform.position = new Vector3(0f, -0.12f, 0f);
    outer.transform.localScale = new Vector3(8f, 1f, 8f);
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
    // Phase 3.0: the old east backdrop tree stood at (10.5, 4.5) — its canopy
    // crossed the Math follow sightline x=12 (P3 visual QA: obstruction
    // pull-in parked the playground camera 1.6m behind the player). Parked
    // clear of the road, the sightline and the playground.
    BuildTree(new Vector3(14.5f, -0.1f, 6.5f), 1.0f);
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
    // Phase 3.0: gaps where the 4 subject roads cross (|x|<1.65 on N/E/W,
    // Vietnamese S road runs at x=3.5 so the spawn camera axis stays clear).
    int n = 0;
    for (float x = -8f; x <= 8.01f; x += 1.6f) {
      bool gapN = Mathf.Abs(x) < 1.65f;
      bool gapS = Mathf.Abs(x - 3.5f) < 1.65f;
      if (!gapN) AddHedgeBush(hedge.transform, new Vector3(x, 0.28f, -6f), n);
      if (!gapS) AddHedgeBush(hedge.transform, new Vector3(x, 0.28f, 6f), n + 1);
      if (!gapN && n % 4 == 1) AddFlowerTuft(hedge.transform, new Vector3(x, 0f, -6f), tuft[(n / 4) % 3]);
      n++;
    }
    for (float z = -4.4f; z <= 4.41f; z += 1.6f) {
      if (Mathf.Abs(z - 1.8f) >= 1.65f) {
        AddHedgeBush(hedge.transform, new Vector3(-8f, 0.28f, z), n);
        AddHedgeBush(hedge.transform, new Vector3(8f, 0.28f, z), n + 1);
      }
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
    // Player report follow-up (pickup radius too generous): 2.0 -> 1.3m.
    // FLOOR (measured live 2026-09-19): CrateCarve denies feet within ~1.1m
    // of the crate center, so anything below ~1.2m softlocks the find (the
    // walker can never get closer). 1.3m is the smallest working radius —
    // the child still stands AT the crate. Proximity discovery follows
    // automatically (same IsInRange gate).
    Apple.interactionDistance = 1.3f;
    Apple.ParseIds(); // fields assigned post-Awake: re-parse or events drop
    AppleDiscovery = apple.AddComponent<ProximityDiscovery>();
  }

  // ---- ball quest target crate (mirrors apple crate structure) ------------------
  // 2F placement (§6): east lawn (5.5, 0, 3.2) — off the Milo-Mia axis so the
  // child must FIND it, 2.6m from the distractor pedestal (5.4, 0, 0.6) with a
  // different presentation (low crate vs tall pedestal), reachable open grass,
  // outside every carve, visible from the mid-lawn approach.
  public static readonly Vector3 BallCrateAnchorPos = new Vector3(5.5f, 0f, 3.2f);

  void BuildBallCrate() {
    GameObject crate = GameObject.CreatePrimitive(PrimitiveType.Cube);
    crate.name = "BallCrate";
    crate.transform.SetParent(transform);
    crate.transform.position = BallCrateAnchorPos;
    crate.transform.localScale = new Vector3(1.2f, 0.4f, 1.2f);
    crate.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.65f, 0.45f, 0.3f));

    // BIG ORANGE quest ball with a white equatorial band (player report: the
    // old small blue sphere was identical to the distractor ball — same color,
    // same size, and SQUASHED by this crate's non-uniform scale (1.2,0.4,1.2),
    // so children could not tell which ball counts). Now unmistakable: orange
    // vs blue, big vs small, banded vs plain, and counter-scaled to a TRUE
    // sphere in world space (0.8 diameter: local = world / parent scale).
    GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    ball.name = "QuestBall";
    ball.transform.SetParent(crate.transform);
    ball.transform.localPosition = new Vector3(0f, 1.5f, 0f);
    ball.transform.localScale = new Vector3(0.667f, 2.0f, 0.667f);
    ball.GetComponent<Renderer>().sharedMaterial = Lit(new Color(1.0f, 0.55f, 0.10f)); // quest orange
    GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    band.name = "QuestBallBand";
    band.transform.SetParent(ball.transform);
    band.transform.localPosition = Vector3.zero;
    band.transform.localScale = new Vector3(1.04f, 0.06f, 1.04f);
    band.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.96f, 0.96f, 0.97f));
    Collider bandCollider = band.GetComponent<Collider>();
    if (bandCollider != null) Destroy(bandCollider); // one click volume (the ball) per quest item

    CrateBall = ball;
    // 2F click-robustness: the Interactable rides the CRATE root (not the
    // ball sphere) — low-angle rays hit the wide crate body first, and
    // GetComponentInParent only searches UP, so a sphere-mounted Interactable
    // silently drops crate-body clicks to plain movement (found live: the
    // find beat only fired via proximity backup). Crate-or-ball clicks both
    // mean the ball (one quest item per crate, same as the apple language).
    Ball = crate.AddComponent<Interactable>();
    Ball.wordId = "ball";
    Ball.interactionId = "take_ball";
    Ball.npcId = "mia";
    Ball.interactionDistance = 1.3f; // same carve-floor physics as the apple crate
    Ball.ParseIds();
    BallDiscovery = crate.AddComponent<ProximityDiscovery>();
    BallDiscovery.questIdValue = "w1_mia_ball"; // arms on the ball quest only
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
    // 2E: the asked thing goes through the icon contract too (same apple
    // visual as the default build — now explicitly staged, so 2F stages the
    // ball by changing one word, not the bubble).
    Bubble.SetIcon(new WordId("apple"));
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

  // Phase 2.4 FINAL POLISH — stylized preschool world dressing (post-NavMesh,
  // pure visual, collider-free, no rebake). Composed as illustration layers
  // (foreground accents / midground play / background silhouettes), NOT as a
  // uniform scatter. Every anchor is hand-placed (deterministic, seed-pinned);
  // only intra-cluster jitter (rotation/scale/offset) uses the seeded RNG, so
  // every build is identical. GameplayClearZone stays empty: player spawn,
  // path corridor, Milo/Mia anchors, both crates, pedestal, flower bed.
  void BuildAmbientDecor() {
    var rng = new System.Random(20260918);
    BuildGroundVariation(rng);
    BuildGrassClusters(rng);
    BuildFlowerClusters(rng);
    BuildBushClusters(rng);
    BuildTreeComposition(rng);
    BuildRockClusters(rng);
    BuildBoundaryVegetation(rng);
    BuildPathTransition(rng);
    BuildStoryProps(rng);
    // Legacy focal: wooden barrel beside the apple crate (non-interactive).
    AddBarrel(new Vector3(CrateAnchorPos.x + 0.9f, 0f, CrateAnchorPos.z + 0.3f));
  }

  // GameplayClearZone: decor with volume (trees/bushes/rocks/barrel) must stay
  // out; flat ground patches and walkable grass may sit anywhere. Radii cover
  // interaction reach + agent footprint + camera framing margin.
  static bool IsInGameplayClearZone(Vector3 pos) {
    if (Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(PlayerSpawn.x, 0f, PlayerSpawn.z)) < 1.4f) return true;
    if (Mathf.Abs(pos.x) < 1.5f && pos.z > -1.7f && pos.z < 5.2f) return true; // path corridor
    if (Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(MiloAnchorPos.x, 0f, MiloAnchorPos.z)) < 1.7f) return true;
    if (Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(MiaAnchorPos.x, 0f, MiaAnchorPos.z)) < 1.9f) return true;
    if (Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(CrateAnchorPos.x, 0f, CrateAnchorPos.z)) < 1.5f) return true;
    if (Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(BallCrateAnchorPos.x, 0f, BallCrateAnchorPos.z)) < 1.5f) return true;
    if (Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(5.4f, 0f, 0.6f)) < 1.3f) return true; // pedestal
    if (Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(FlowerAnchorPos.x, 0f, FlowerAnchorPos.z)) < 1.3f) return true;
    // Milo->Mia and Milo->apple walking corridors (keep solid decor off them).
    if (DistToSegment(pos, MiloAnchorPos, MiaAnchorPos) < 0.9f) return true;
    if (DistToSegment(pos, MiloAnchorPos, CrateAnchorPos) < 0.9f) return true;
    // Phase 3.0: subject roads + playgrounds stay furniture-free for ambient
    // decor (roads are ground treatment; districts are composed separately).
    if (Mathf.Abs(pos.z - 1.8f) < 1.3f && pos.x > 5.3f && pos.x < 15.8f) return true; // Math road
    if (Mathf.Abs(pos.z - 1.8f) < 1.3f && pos.x < -5.3f && pos.x > -15.8f) return true; // Thinking road
    if (Mathf.Abs(pos.x) < 1.3f && pos.z < -3.3f && pos.z > -13.8f) return true; // English road
    if (Mathf.Abs(pos.x - 3.5f) < 1.3f && pos.z > 3.3f && pos.z < 13.8f) return true; // Vietnamese road (x=3.5)
    if (Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(12.2f, 0f, 1.8f)) < 4.4f) return true;
    if (Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(-12.2f, 0f, 1.8f)) < 4.4f) return true;
    if (Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(0f, 0f, -10.0f)) < 4.4f) return true;
    if (Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(3.5f, 0f, 10.0f)) < 4.4f) return true;
    return false;
  }

  static float DistToSegment(Vector3 p, Vector3 a, Vector3 b) {
    Vector2 pa = new Vector2(p.x - a.x, p.z - a.z);
    Vector2 ba = new Vector2(b.x - a.x, b.z - a.z);
    float t = Mathf.Clamp01((pa.x * ba.x + pa.y * ba.y) / Mathf.Max(0.001f, ba.sqrMagnitude));
    return new Vector2(pa.x - ba.x * t, pa.y - ba.y * t).magnitude;
  }

  // ---- ground: soft value/hue breakup so the lawn is a stage, not a sheet --
  void BuildGroundVariation(System.Random rng) {
    // Irregular flat discs (y just above grass, collider-free, walkable).
    AddGroundPatch(new Vector3(-5.5f, 0f, 2.5f), new Vector3(2.2f, 1f, 1.6f), new Color(0.36f, 0.66f, 0.33f), rng);
    AddGroundPatch(new Vector3(-4.0f, 0f, -4.0f), new Vector3(2.0f, 1f, 1.5f), new Color(0.30f, 0.58f, 0.28f), rng);
    AddGroundPatch(new Vector3(4.5f, 0f, -4.5f), new Vector3(2.4f, 1f, 1.7f), new Color(0.36f, 0.66f, 0.33f), rng);
    AddGroundPatch(new Vector3(5.5f, 0f, 1.8f), new Vector3(1.8f, 1f, 1.4f), new Color(0.38f, 0.64f, 0.32f), rng);
    AddGroundPatch(new Vector3(-2.4f, 0f, 1.2f), new Vector3(1.5f, 1f, 1.2f), new Color(0.30f, 0.58f, 0.28f), rng);
    AddGroundPatch(new Vector3(2.6f, 0f, 1.2f), new Vector3(1.6f, 1f, 1.3f), new Color(0.36f, 0.66f, 0.33f), rng);
    AddGroundPatch(new Vector3(-1.5f, 0f, -4.6f), new Vector3(2.0f, 1f, 1.3f), new Color(0.38f, 0.64f, 0.32f), rng);
    AddGroundPatch(new Vector3(-6.5f, 0f, -0.5f), new Vector3(1.7f, 1f, 1.4f), new Color(0.30f, 0.58f, 0.28f), rng);
    AddGroundPatch(new Vector3(0.6f, 0f, 4.9f), new Vector3(1.4f, 1f, 1.0f), new Color(0.36f, 0.66f, 0.33f), rng);
  }

  void AddGroundPatch(Vector3 pos, Vector3 size, Color color, System.Random rng) {
    GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    patch.name = "GrassPatch";
    patch.transform.SetParent(transform);
    float yaw = (float)(rng.NextDouble() * 360.0);
    patch.transform.position = pos + new Vector3(0f, 0.012f, 0f);
    patch.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    patch.transform.localScale = new Vector3(size.x, 0.012f, size.z);
    patch.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Collider c = patch.GetComponent<Collider>();
    if (c != null) Destroy(c);
  }

  // ---- low vegetation: irregular tuft clusters, walkable, off-path rhythm --
  // User round (thoáng): thinned 14 -> 8, kept as breathing accents.
  void BuildGrassClusters(System.Random rng) {
    // Legacy trio (kept at exact positions) + dead-zone clusters.
    AddGrassTuft(new Vector3(-4f, 0f, 5.2f), 0.9f, rng);
    AddGrassTuft(new Vector3(3f, 0f, -5f), 1f, rng);
    AddGrassTuft(new Vector3(6.5f, 0f, 1.5f), 0.85f, rng);
    AddGrassTuft(new Vector3(-5.0f, 0f, -4.2f), 1.05f, rng);
    AddGrassTuft(new Vector3(-2.2f, 0f, -4.8f), 0.9f, rng);
    AddGrassTuft(new Vector3(4.0f, 0f, -0.2f), 0.85f, rng);
    AddGrassTuft(new Vector3(-1.7f, 0f, 3.0f), 0.8f, rng);
    AddGrassTuft(new Vector3(1.7f, 0f, 3.2f), 0.9f, rng);
  }

  // ---- flowers: pastel clusters (never carpets), at focal adjacencies ------
  static readonly Color[] PastelBlooms = {
    new Color(0.95f, 0.55f, 0.65f), // pink
    new Color(0.96f, 0.95f, 0.90f), // white
    new Color(0.98f, 0.82f, 0.30f), // yellow
    new Color(0.75f, 0.60f, 0.90f), // lavender
    new Color(0.98f, 0.65f, 0.40f), // light orange
  };

  void BuildFlowerClusters(System.Random rng) {
    // Legacy north-lawn patch (kept) + composition clusters near
    // tree bases / rocks / hedge / landmarks — always with breathing room.
    // User round (thoáng): 8 -> 5 clusters, fewer blooms each.
    AddFlowerPatch(new Vector3(2f, 0f, 4.8f), rng);
    AddFlowerCluster(new Vector3(-5.5f, 0f, 4.2f), 3, 0, rng); // NW tree base
    AddFlowerCluster(new Vector3(3.2f, 0f, 5.0f), 3, 1, rng);  // north meadow
    AddFlowerCluster(new Vector3(-1.8f, 0f, 2.5f), 3, 2, rng); // west of path
    AddFlowerCluster(new Vector3(-6.5f, 0f, -5.2f), 2, 0, rng); // SW corner
    AddFlowerCluster(new Vector3(1.7f, 0f, -4.6f), 2, 2, rng);  // south border
  }

  void AddFlowerCluster(Vector3 pos, int blooms, int paletteOffset, System.Random rng) {
    if (IsInGameplayClearZone(pos)) return;
    // Premium path: one sculpted Quaternius cluster, pastel-pair tinted.
    Color a1 = PastelBlooms[paletteOffset % PastelBlooms.Length];
    Color a2 = PastelBlooms[(paletteOffset + 2) % PastelBlooms.Length];
    GameObject grown = NatureLibrary.Spawn(transform, "Flowers", pos,
      rng != null ? (float)(rng.NextDouble() * 360.0) : 0f,
      0.40f + (blooms - 3) * 0.03f,
      new Color(0.28f, 0.58f, 0.30f), a1, a2, true);
    if (grown != null) {
      grown.name = "FlowerCluster";
      grown.AddComponent<NatureSway>().amplitudeDeg = 0.8f;
      // Extra filler blooms around the sculpted core (primitive, harmonized).
      GameObject filler = new GameObject("ClusterFiller");
      filler.transform.SetParent(grown.transform);
      filler.transform.localPosition = Vector3.zero;
      for (int i = 0; i < blooms; i++) {
        float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
        float rad = 0.30f + (float)rng.NextDouble() * 0.18f;
        AddTinyBloomAt(filler.transform,
          grown.transform.position + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad),
          PastelBlooms[(paletteOffset + i) % PastelBlooms.Length],
          "ClusterSprig", "ClusterBloom");
      }
      return;
    }
    LegacyFlowerCluster(pos, blooms, paletteOffset, rng);
  }

  void LegacyFlowerCluster(Vector3 pos, int blooms, int paletteOffset, System.Random rng) {
    GameObject cluster = new GameObject("FlowerCluster");
    cluster.transform.SetParent(transform);
    cluster.transform.position = pos;
    for (int i = 0; i < blooms; i++) {
      float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
      float rad = 0.12f + (float)rng.NextDouble() * 0.22f;
      Vector3 off = new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
      float h = 0.30f + (float)rng.NextDouble() * 0.14f;
      Color bloom = PastelBlooms[(paletteOffset + i) % PastelBlooms.Length];
      GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      stem.name = "ClusterStem";
      stem.transform.SetParent(cluster.transform);
      stem.transform.position = pos + off + new Vector3(0f, h * 0.5f, 0f);
      stem.transform.localScale = new Vector3(0.04f, h * 0.5f, 0.04f);
      stem.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.25f, 0.55f, 0.28f));
      Collider sc = stem.GetComponent<Collider>();
      if (sc != null) Destroy(sc);
      GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      head.name = "ClusterBloom";
      head.transform.SetParent(cluster.transform);
      head.transform.position = pos + off + new Vector3(0f, h, 0f);
      float bs = 0.12f + (float)rng.NextDouble() * 0.05f;
      head.transform.localScale = new Vector3(bs, bs, bs);
      head.GetComponent<Renderer>().sharedMaterial = Lit(bloom);
      Collider bc = head.GetComponent<Collider>();
      if (bc != null) Destroy(bc);
    }
  }

  // ---- bushes: edge/corner/landmark accents that break empty lawns ---------
  void BuildBushClusters(System.Random rng) {
    AddDecorBush(new Vector3(-7.0f, 0f, 0.5f), 1.0f, false, rng);
    AddDecorBush(new Vector3(-5.2f, 0f, -5.0f), 1.1f, false, rng);
    AddDecorBush(new Vector3(5.0f, 0f, -5.0f), 1.0f, false, rng);
    AddDecorBush(new Vector3(2.5f, 0f, 5.1f), 0.9f, false, rng);
    AddDecorBush(new Vector3(-6.0f, 0f, 2.0f), 0.85f, false, rng); // NW tree base
    AddDecorBush(new Vector3(-5.4f, 0f, -3.4f), 0.9f, false, rng); // stall west
    AddDecorBush(new Vector3(-1.4f, 0f, -3.2f), 0.85f, false, rng); // stall east
    AddDecorBush(new Vector3(-1.9f, 0f, -0.6f), 0.8f, false, rng);  // path bend
    AddDecorBush(new Vector3(-7.0f, 0f, -4.5f), 1.0f, true, rng);   // flowering SW
    AddDecorBush(new Vector3(7.0f, 0f, 4.5f), 0.95f, true, rng);    // flowering NE
    AddDecorBush(new Vector3(-6.9f, 0f, 5.0f), 0.9f, true, rng);    // flowering NW
  }

  static readonly string[] BushModels = { "Bush_1", "Bush_2" };

  void AddDecorBush(Vector3 pos, float s, bool flowering, System.Random rng) {
    if (IsInGameplayClearZone(pos)) return;
    // Premium path: sculpted bushes; berries become pastel blooms.
    string model = flowering ? "BushBerries_1" : BushModels[rng.Next(BushModels.Length)];
    Color leaf = (rng.NextDouble() < 0.5)
      ? new Color(0.28f, 0.60f, 0.30f)
      : new Color(0.32f, 0.63f, 0.32f);
    GameObject grown = NatureLibrary.Spawn(transform, model, pos,
      (float)(rng.NextDouble() * 360.0), 0.62f * s, leaf,
      new Color(0.95f, 0.55f, 0.65f), new Color(0.98f, 0.82f, 0.30f));
    if (grown != null) { grown.name = "DecorBush"; return; }
    LegacyDecorBush(pos, s, flowering, rng, leaf);
  }

  void LegacyDecorBush(Vector3 pos, float s, bool flowering, System.Random rng, Color leaf) {
    GameObject bush = new GameObject("DecorBush");
    GameObject leafGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    leafGo.name = "DecorBushLeaf";
    leafGo.transform.SetParent(bush.transform);
    leafGo.transform.localPosition = new Vector3(0f, 0.32f * s, 0f);
    leafGo.transform.localScale = new Vector3(1.05f * s, 0.62f * s, 1.05f * s);
    leafGo.GetComponent<Renderer>().sharedMaterial = Lit(leaf);
    Collider c = leafGo.GetComponent<Collider>();
    if (c != null) Destroy(c);
    if (flowering) {
      for (int i = 0; i < 3; i++) {
        float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
        Vector3 off = new Vector3(Mathf.Cos(ang) * 0.32f * s, 0.52f * s, Mathf.Sin(ang) * 0.32f * s);
        GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = "DecorBushBloom";
        dot.transform.SetParent(bush.transform);
        dot.transform.localPosition = off;
        dot.transform.localScale = new Vector3(0.13f, 0.13f, 0.13f);
        dot.GetComponent<Renderer>().sharedMaterial = Lit(PastelBlooms[(i * 2) % PastelBlooms.Length]);
        Collider bc = dot.GetComponent<Collider>();
        if (bc != null) Destroy(bc);
      }
    }
  }

  // ---- trees: 3 scale classes; inside = small/medium anchors, outside = BG --
  // Premium path: curated Quaternius silhouettes (round/pine/willow) with
  // runtime height normalization; primitive fallback keeps domains green.
  void BuildTreeComposition(System.Random rng) {
    // Inside-boundary anchors (carved in BuildNavCarves, visuals here).
    AddDecorTree(new Vector3(-6.5f, 0f, -2.8f), "CommonTree_1", 1.6f, new Color(0.25f, 0.58f, 0.28f), rng);
    AddDecorTree(new Vector3(6.8f, 0f, -3.2f), "PineTree_2", 1.7f, new Color(0.26f, 0.57f, 0.29f), rng);
    AddDecorTree(new Vector3(-2.8f, 0f, 4.9f), "Willow_2", 2.3f, new Color(0.30f, 0.62f, 0.30f), rng);
    // Background silhouettes outside play (soft, lower-contrast greens).
    AddDecorTree(new Vector3(-2f, -0.1f, -11f), "CommonTree_5", 4.2f, new Color(0.24f, 0.53f, 0.30f), rng);
    AddDecorTree(new Vector3(12f, -0.1f, -2f), "CommonTree_3", 3.6f, new Color(0.24f, 0.53f, 0.30f), rng);
    AddDecorTree(new Vector3(-12f, -0.1f, 5f), "CommonTree_1", 3.8f, new Color(0.24f, 0.53f, 0.30f), rng);
    AddDecorTree(new Vector3(7f, -0.1f, 10f), "PineTree_2", 3.4f, new Color(0.24f, 0.53f, 0.30f), rng);
    AddDecorTree(new Vector3(-7f, -0.1f, 10.5f), "CommonTree_5", 3.6f, new Color(0.24f, 0.53f, 0.30f), rng);
  }

  void AddDecorTree(Vector3 pos, string model, float height, Color leaf, System.Random rng) {
    bool inside = Mathf.Abs(pos.x) < BoundX && Mathf.Abs(pos.z) < BoundZ;
    if (inside && IsInGameplayClearZone(pos)) return;
    float yaw = rng != null ? (float)(rng.NextDouble() * 360.0) : 0f;
    GameObject grown = NatureLibrary.Spawn(transform, model, pos, yaw, height,
      leaf, leaf, leaf);
    if (grown != null) { grown.name = "DecorTree"; return; }
    LegacyDecorTree(pos, height * 0.45f, leaf, rng);
  }

  void LegacyDecorTree(Vector3 pos, float s, Color leaf, System.Random rng) {
    GameObject tree = new GameObject("DecorTree");
    tree.transform.SetParent(transform);
    tree.transform.position = pos;
    tree.transform.localScale = Vector3.one * s;
    tree.transform.localRotation = Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f);
    Color trunkC = new Color(0.45f, 0.30f, 0.16f);
    GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    trunk.name = "DecorTrunk";
    trunk.transform.SetParent(tree.transform);
    trunk.transform.localPosition = new Vector3(0f, 0.9f, 0f);
    trunk.transform.localScale = new Vector3(0.5f, 1.8f, 0.5f);
    trunk.GetComponent<Renderer>().sharedMaterial = Lit(trunkC);
    Collider tc = trunk.GetComponent<Collider>();
    if (tc != null) Destroy(tc);
    Vector3[] canopyAt = {
      new Vector3(0f, 2.3f, 0f),
      new Vector3(0.7f, 1.9f, 0.3f),
      new Vector3(-0.6f, 2.0f, -0.3f),
    };
    for (int i = 0; i < canopyAt.Length; i++) {
      GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      canopy.name = "DecorCanopy";
      canopy.transform.SetParent(tree.transform);
      canopy.transform.localPosition = canopyAt[i];
      canopy.transform.localScale = new Vector3(1.6f, 1.3f, 1.6f);
      canopy.GetComponent<Renderer>().sharedMaterial = Lit(leaf);
      Collider cc = canopy.GetComponent<Collider>();
      if (cc != null) Destroy(cc);
    }
  }

  // ---- rocks: always grouped with vegetation, never lone obstacles ---------
  void BuildRockClusters(System.Random rng) {
    // Legacy path-edge pair (kept) + grouped clusters.
    AddRock(new Vector3(1.2f, 0f, 2.8f), 0.5f, rng);
    AddRock(new Vector3(-1.3f, 0f, 0.2f), 0.45f, rng);
    AddRockWithGreens(new Vector3(-5.8f, 0f, 1.0f), 0.6f, rng); // NW tree base
    AddRockWithGreens(new Vector3(4.8f, 0f, -3.5f), 0.55f, rng); // apple nook
    AddRockWithGreens(new Vector3(-2.0f, 0f, -4.5f), 0.5f, rng); // south mid
    AddRockWithGreens(new Vector3(6.7f, 0f, 1.8f), 0.5f, rng);   // east mid
  }

  void AddRockWithGreens(Vector3 pos, float s, System.Random rng) {
    if (IsInGameplayClearZone(pos)) return;
    AddRock(pos, s, rng);
    // Pebble companions + one grass sprig + one tiny bloom: a composed group.
    for (int i = 0; i < 2; i++) {
      float ang = (float)(rng.NextDouble() * Mathf.PI * 2f);
      Vector3 off = new Vector3(Mathf.Cos(ang) * 0.45f, 0f, Mathf.Sin(ang) * 0.45f);
      AddPebble(pos + off, 0.5f + (float)rng.NextDouble() * 0.4f);
    }
    float gang = (float)(rng.NextDouble() * Mathf.PI * 2f);
    Vector3 goff = new Vector3(Mathf.Cos(gang) * 0.55f, 0f, Mathf.Sin(gang) * 0.55f);
    if (!IsInGameplayClearZone(pos + goff)) AddGrassTuft(pos + goff, 0.7f, rng);
  }

  void AddPebble(Vector3 pos, float s) {
    GameObject grown = NatureLibrary.Spawn(transform, "Rock_6", pos, 0f, 0.13f * s,
      Color.white, Color.white, Color.white);
    if (grown != null) { grown.name = "Pebble"; return; }
    GameObject pebble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    pebble.name = "Pebble";
    pebble.transform.SetParent(transform);
    pebble.transform.position = pos + new Vector3(0f, 0.06f, 0f);
    pebble.transform.localScale = new Vector3(0.22f * s, 0.12f * s, 0.26f * s);
    pebble.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.60f, 0.60f, 0.62f));
    Collider c = pebble.GetComponent<Collider>();
    if (c != null) Destroy(c);
  }

  // ---- storytelling props: stump + log seats with a reason to exist --------
  void BuildStoryProps(System.Random rng) {
    // Stump beside the NW tree nook (nature seat) + flowers already cluster it.
    if (!IsInGameplayClearZone(new Vector3(-6.0f, 0f, 2.6f))) {
      GameObject stump = NatureLibrary.Spawn(transform, "TreeStump",
        new Vector3(-6.0f, 0f, 2.6f), (float)(rng.NextDouble() * 360.0), 0.42f,
        new Color(0.28f, 0.58f, 0.30f), Color.white, Color.white);
      if (stump != null) stump.name = "TreeStumpProp";
    }
    // Fallen log bench at the north meadow edge (story corner, off the path).
    if (!IsInGameplayClearZone(new Vector3(-2.0f, 0f, 5.3f))) {
      GameObject log = NatureLibrary.Spawn(transform, "WoodLog",
        new Vector3(-2.0f, 0f, 5.3f), 25f + (float)(rng.NextDouble() * 20.0), 0.35f,
        new Color(0.28f, 0.58f, 0.30f), Color.white, Color.white);
      if (log != null) log.name = "WoodLogProp";
    }
    // Tiny leafy plants: tree bases, stall corners, rock groups.
    AddGroundPlant(new Vector3(-5.9f, 0f, 3.3f), rng);
    AddGroundPlant(new Vector3(6.4f, 0f, -2.7f), rng);
    AddGroundPlant(new Vector3(-5.5f, 0f, -1.8f), rng);
    AddGroundPlant(new Vector3(4.9f, 0f, -3.3f), rng);
  }

  static readonly string[] PlantModels = { "Plant_2", "Plant_4" };

  void AddGroundPlant(Vector3 pos, System.Random rng) {
    if (IsInGameplayClearZone(pos)) return;
    Color leaf = new Color(0.29f, 0.59f, 0.30f);
    GameObject grown = NatureLibrary.Spawn(transform, PlantModels[rng.Next(PlantModels.Length)],
      pos, (float)(rng.NextDouble() * 360.0), 0.30f, leaf, leaf, leaf, true);
    if (grown != null) {
      grown.name = "GroundPlant";
      grown.AddComponent<NatureSway>();
      return;
    }
    AddGrassTuft(pos, 0.6f, rng);
  }

  // ---- boundary: turn the hedge line into garden depth, hide the void ------
  void BuildBoundaryVegetation(System.Random rng) {
    // Inside accents just off the hedge (fill hedge gaps, keep sightlines low).
    AddDecorBush(new Vector3(-3.0f, 0f, 5.4f), 0.8f, false, rng);
    AddDecorBush(new Vector3(5.8f, 0f, -5.3f), 0.85f, false, rng);
    AddDecorBush(new Vector3(-7.3f, 0f, -2.0f), 0.8f, false, rng);
    AddFlowerCluster(new Vector3(4.5f, 0f, 5.3f), 2, 1, rng);
    AddFlowerCluster(new Vector3(-4.2f, 0f, -5.3f), 2, 3, rng);
    // Outside backdrop blobs on the dark skirt (soft depth behind the hedge).
    AddBackdropBlob(new Vector3(-4f, -0.1f, 8.5f), 2.2f);
    AddBackdropBlob(new Vector3(5f, -0.1f, -8.5f), 2.6f);
    AddBackdropBlob(new Vector3(-10.5f, -0.1f, 0f), 2.4f);
    AddBackdropBlob(new Vector3(10.5f, -0.1f, 1f), 2.0f);
  }

  void AddBackdropBlob(Vector3 pos, float s) {
    GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    blob.name = "BackdropBlob";
    blob.transform.SetParent(transform);
    blob.transform.position = pos + new Vector3(0f, 0.35f * s, 0f);
    blob.transform.localScale = new Vector3(2.0f * s, 0.7f * s, 2.0f * s);
    blob.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.26f, 0.50f, 0.34f));
    Collider c = blob.GetComponent<Collider>();
    if (c != null) Destroy(c);
  }

  // ---- path: worn-edge transition so the road feels walked, not pasted -----
  void BuildPathTransition(System.Random rng) {
    // User round (thoáng): 6 -> 4 accents per side, wider rhythm.
    for (int side = -1; side <= 1; side += 2) {
      for (int i = 0; i < 4; i++) {
        float z = -0.5f + i * 1.1f + (float)(rng.NextDouble() * 0.3 - 0.15);
        float x = side * (1.28f + (float)rng.NextDouble() * 0.18f);
        Vector3 p = new Vector3(x, 0f, z);
        if (IsInGameplayClearZone(p) && Mathf.Abs(x) < 1.5f && z > -1.7f && z < 5.2f) {
          // Path corridor is clear-zone by definition; edge accents live JUST
          // outside it — nudge outward instead of skipping (keeps rhythm).
          p.x = side * 1.62f;
        }
        int pick = rng.Next(3);
        if (pick == 0) AddPebble(p, 0.6f + (float)rng.NextDouble() * 0.5f);
        else if (pick == 1) AddGrassTuft(p, 0.55f + (float)rng.NextDouble() * 0.2f, rng);
        else AddTinyBloom(p, PastelBlooms[rng.Next(PastelBlooms.Length)]);
      }
    }
    // Subtle dirt variation ON the path shoulders (flat, underfoot, no block).
    AddGroundPatch(new Vector3(1.05f, 0f, 1.5f), new Vector3(0.5f, 1f, 2.2f), new Color(0.72f, 0.56f, 0.38f), rng);
    AddGroundPatch(new Vector3(-1.05f, 0f, 2.8f), new Vector3(0.45f, 1f, 1.8f), new Color(0.72f, 0.56f, 0.38f), rng);
  }

  void AddTinyBloom(Vector3 pos, Color bloom) {
    AddTinyBloomAt(transform, pos, bloom, "PathSprig", "PathTinyBloom");
  }

  void AddTinyBloomAt(Transform parent, Vector3 pos, Color bloom, string stemName, string bloomName) {
    GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stem.name = stemName;
    stem.transform.SetParent(parent);
    stem.transform.position = pos + new Vector3(0f, 0.10f, 0f);
    stem.transform.localScale = new Vector3(0.03f, 0.10f, 0.03f);
    stem.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.25f, 0.55f, 0.28f));
    Collider sc = stem.GetComponent<Collider>();
    if (sc != null) Destroy(sc);
    GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    head.name = bloomName;
    head.transform.SetParent(parent);
    head.transform.position = pos + new Vector3(0f, 0.19f, 0f);
    head.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
    head.GetComponent<Renderer>().sharedMaterial = Lit(bloom);
    Collider bc = head.GetComponent<Collider>();
    if (bc != null) Destroy(bc);
  }

  // Legacy helpers (kept names/shapes; scale/rotation now vary via seeded RNG).
  // Premium path first: harmonized Quaternius CC0 clumps; primitive fallback
  // keeps EditMode-batch and missing-Resources domains green.
  // (Grass uses the LW clump system above — external blades lack normals.)

  void AddGrassTuft(Vector3 pos, float s, System.Random rng) {
    float yaw = rng != null ? (float)(rng.NextDouble() * 360.0) : 0f;
    float sv = rng != null ? 0.8f + (float)rng.NextDouble() * 0.4f : 1f;
    Color leaf = (rng != null && rng.NextDouble() < 0.5)
      ? new Color(0.30f, 0.58f, 0.32f)
      : new Color(0.27f, 0.55f, 0.30f);
    // LW clump system (guaranteed normals/lighting); Quaternius blades stay
    // out — their meshes carry no usable normals under URP/Lit (black).
    GameObject grown = LwGrass.SpawnTuft(transform, pos + new Vector3(0f, 0.02f, 0f),
      yaw, 0.34f * s * sv, leaf);
    if (grown != null) return;
    GameObject tuft = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    tuft.name = "GrassTuft";
    tuft.transform.SetParent(transform);
    tuft.transform.position = pos + new Vector3(0f, 0.18f, 0f);
    tuft.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    tuft.transform.localScale = new Vector3(0.7f * s * sv, 0.35f * s * sv, 0.7f * s * sv);
    tuft.GetComponent<Renderer>().sharedMaterial = Lit(leaf);
    Collider c = tuft.GetComponent<Collider>();
    if (c != null) Destroy(c);
  }

  static readonly string[] RockModels = { "Rock_2", "Rock_4", "Rock_6", "Rock_Moss_2" };

  void AddRock(Vector3 pos, float s, System.Random rng) {
    // Hand-placed (path-edge pair kept); low profile, collider-free.
    float yaw = rng != null ? (float)(rng.NextDouble() * 360.0) : 0f;
    string model = RockModels[rng != null ? rng.Next(RockModels.Length) : 0];
    GameObject grown = NatureLibrary.Spawn(transform, model, pos,
      yaw, 0.30f * s, Color.white, Color.white, Color.white);
    if (grown != null) { grown.name = "Rock"; return; }
    GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    rock.name = "Rock";
    rock.transform.SetParent(transform);
    rock.transform.position = pos + new Vector3(0f, 0.12f, 0f);
    rock.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    rock.transform.localScale = new Vector3(0.55f * s, 0.30f * s, 0.65f * s);
    rock.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.55f, 0.55f, 0.58f));
    Collider c = rock.GetComponent<Collider>();
    if (c != null) Destroy(c);
  }

  void AddFlowerPatch(Vector3 pos, System.Random rng) {
    // Legacy north-lawn trio (kept): 3 blooms = P25H patch pin.
    Color[] blooms = { new Color(0.95f, 0.55f, 0.65f), new Color(0.98f, 0.82f, 0.30f), new Color(0.96f, 0.95f, 0.90f) };
    for (int i = 0; i < 3; i++) {
      float ox = (i - 1) * 0.18f;
      GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      stem.name = "PatchStem";
      stem.transform.SetParent(transform);
      stem.transform.position = pos + new Vector3(ox, 0.22f, 0f);
      stem.transform.localScale = new Vector3(0.04f, 0.22f, 0.04f);
      stem.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.25f, 0.55f, 0.28f));
      Collider sc = stem.GetComponent<Collider>();
      if (sc != null) Destroy(sc);
      GameObject bloom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bloom.name = "PatchBloom";
      bloom.transform.SetParent(transform);
      bloom.transform.position = pos + new Vector3(ox, 0.38f, 0f);
      bloom.transform.localScale = new Vector3(0.14f, 0.14f, 0.14f);
      bloom.GetComponent<Renderer>().sharedMaterial = Lit(blooms[i % 3]);
      Collider bcc = bloom.GetComponent<Collider>();
      if (bcc != null) Destroy(bcc);
    }
  }

  void AddBarrel(Vector3 pos) {
    // Hand-placed beside the crate (outside its carve, non-interactive).
    GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    barrel.name = "Barrel";
    barrel.transform.SetParent(transform);
    barrel.transform.position = pos + new Vector3(0f, 0.35f, 0f);
    barrel.transform.localScale = new Vector3(0.45f, 0.70f, 0.45f);
    barrel.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.52f, 0.36f, 0.22f));
    Collider c = barrel.GetComponent<Collider>();
    if (c != null) Destroy(c);
    // Wood hoops
    for (int i = 0; i < 2; i++) {
      GameObject hoop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      hoop.name = "BarrelHoop";
      hoop.transform.SetParent(barrel.transform);
      hoop.transform.localPosition = new Vector3(0f, (i == 0 ? 0.25f : -0.25f), 0f);
      hoop.transform.localScale = new Vector3(1.05f, 0.06f, 1.05f);
      hoop.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.35f, 0.35f, 0.38f));
      Collider hc = hoop.GetComponent<Collider>();
      if (hc != null) Destroy(hc);
    }
  }

  // ---- player capsule + anchors -------------------------------------------------

  // Phase 2.4: gender is applied to PlayerVisual before BuildVisual runs
  // (Awake order), so MarketBuilder stamps Gender first then adds the component.
  PlayerGender _pendingGender = PlayerGender.Boy;

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
    // Gender must be set BEFORE PlayerVisual.Awake calls BuildVisual.
    var viz = player.AddComponent<PlayerVisual>();
    // Apply pending gender via reflection-free path: set field before Awake already ran,
    // but Awake already built with default Boy, so re-tint if needed.
    if (_pendingGender != PlayerGender.Boy) viz.SetGender(_pendingGender);
    PlayerViz = viz;

    GameObject hand = new GameObject("HandAnchor");
    hand.transform.SetParent(player.transform);
    hand.transform.localPosition = new Vector3(0.45f, 1.25f, 0.3f);
    PlayerHand = hand.transform;

    MiloAnchor = NewAnchor("MiloAnchor", MiloAnchorPos);
    MiaAnchor = NewAnchor("MiaAnchor", MiaAnchorPos);
  }

  // Called by GameInstaller before Awake (via pending) or live to switch.
  public void SetPlayerGender(PlayerGender gender) {
    _pendingGender = gender;
    if (PlayerViz != null) PlayerViz.SetGender(gender);
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
    // Phase 2.5: game-audio tap for recordings (MUST sit on the listener
    // object for OnAudioFilterRead; read-only copy, armed only while recording).
    GameTap = camGo.AddComponent<GameAudioTap>();
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

    GameObject ballPresenterGo = new GameObject("BallPresenter");
    ballPresenterGo.transform.SetParent(transform);
    BallPresenter = ballPresenterGo.AddComponent<BallPresenter>();

    GameObject flowerGo = new GameObject("FlowerPresenter");
    flowerGo.transform.SetParent(transform);
    FlowerPresenter = flowerGo.AddComponent<FlowerPotPresenter>();

    // R7 software cursor (presentation only: no bus, no services). Built with
    // the frame services so it exists from the first rendered frame; hides
    // the hardware arrow and highlights clickables on hover by itself.
    GameObject cursorGo = new GameObject("MouseCursor");
    cursorGo.transform.SetParent(transform);
    Cursor = cursorGo.AddComponent<CursorPresenter>();

    // Player-report follow-up (ground click -> gold plus at the destination
    // while walking). Built with the frame services (post-NavMesh-bake, like
    // the cursor marker, so its geometry never touches the bake).
    GameObject destGo = new GameObject("DestinationMarker");
    destGo.transform.SetParent(transform);
    destGo.AddComponent<DestinationMarker>();
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
    // Phase 2.4 landscape polish: carves for the three new inside-boundary
    // decor trees (same pattern as TreeCarve — deny foot placement only,
    // interaction reach untouched; runtime carving, no rebake needed).
    CarveBox("DecorTreeCarveW", new Vector3(-6.5f, 1f, -2.8f), new Vector3(1.2f, 2f, 1.2f));
    CarveBox("DecorTreeCarveE", new Vector3(6.8f, 1f, -3.2f), new Vector3(1.2f, 2f, 1.2f));
    CarveBox("DecorTreeCarveN", new Vector3(-2.8f, 1f, 4.9f), new Vector3(1.4f, 2f, 1.4f));
    CarveBox("StallCarve", new Vector3(MiaAnchorPos.x, 0.5f, MiaAnchorPos.z - 1.4f), new Vector3(2.6f, 1f, 1.4f));
    CarveBox("CrateCarve", new Vector3(CrateAnchorPos.x, 0.3f, CrateAnchorPos.z), new Vector3(1.2f, 0.6f, 1.2f));
    CarveBox("BallCrateCarve", new Vector3(BallCrateAnchorPos.x, 0.3f, BallCrateAnchorPos.z), new Vector3(1.2f, 0.6f, 1.2f));
    CarveBox("PedestalCarve", new Vector3(5.4f, 0.4f, 0.6f), new Vector3(0.9f, 0.8f, 0.9f));
    // R9: fence -> hedge (same footprint, renamed with the visuals).
    // Phase 3.0: the 4 subject roads cross the inner hedge through 2m+ gaps
    // (N/S gap at x[-1,1], E/W gap at z[0.5,3.1] around the 1.8 axis).
    CarveBox("EdgeCarveN_L", new Vector3(-4.6f, 0.5f, -6f), new Vector3(7.2f, 1f, 0.4f));
    CarveBox("EdgeCarveN_R", new Vector3(4.6f, 0.5f, -6f), new Vector3(7.2f, 1f, 0.4f));
    // S gap follows the Vietnamese road at x=3.5 (gap x[2.3,4.7]).
    CarveBox("EdgeCarveS_L", new Vector3(-2.95f, 0.5f, 6f), new Vector3(10.5f, 1f, 0.4f));
    CarveBox("EdgeCarveS_R", new Vector3(6.45f, 0.5f, 6f), new Vector3(3.5f, 1f, 0.4f));
    CarveBox("EdgeCarveW_N", new Vector3(-8f, 0.5f, -2.85f), new Vector3(0.4f, 1f, 6.7f));
    CarveBox("EdgeCarveW_S", new Vector3(-8f, 0.5f, 4.65f), new Vector3(0.4f, 1f, 3.1f));
    CarveBox("EdgeCarveE_N", new Vector3(8f, 0.5f, -2.85f), new Vector3(0.4f, 1f, 6.7f));
    CarveBox("EdgeCarveE_S", new Vector3(8f, 0.5f, 4.65f), new Vector3(0.4f, 1f, 3.1f));
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

  // Shared matte material cache (landscape polish): one instance per color
  // instead of one per object — fewer materials, instancing-friendly.
  static readonly Dictionary<string, Material> _litCache = new Dictionary<string, Material>();

  static Material Lit(Color color) {
    string key = color.r.ToString("F2") + "," + color.g.ToString("F2") + "," + color.b.ToString("F2");
    if (_litCache.TryGetValue(key, out Material cached) && cached != null) return cached;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    // R5V-1 unified stylized finish: matte environment (specular highlights
    // on grass/path read as neon/glow under the warm sun).
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    mat.enableInstancing = true;
    _litCache[key] = mat;
    return mat;
  }
}
