// _Bootstrap/GameInstaller.cs — THE ONLY file allowed to `new` services (Lead owns).
// Lives in LWE.Bootstrap (top-level composition assembly), NOT LWE.SharedKernel:
// SharedKernel stays dependency-light; Bootstrap references SharedKernel + World +
// Brain + Content + Audio. Feature assemblies MUST NOT reference LWE.Bootstrap.
// Matches docs/ARCHITECTURE.md §3 v6.2. Agents A/B/C/D receive dependencies via
// constructor from here; never ServiceLocator/FindObjectOfType/new-service outside.
// W0-T0 provides the concrete classes with EXACT constructor signatures used below:
//   LocalSave(), AzureSttProvider(), FallbackSpeechProvider(), MockSpeechProvider(),
//   SpeechAssessmentPolicy(), LearningService(IGameEventBus), HintService(IGameEventBus),
//   QuestManager(IGameEventBus, ILearningService, IHintService),
//   QuestRewardService(IGameEventBus) — completion rewards (friendship + world changes),
//   CloudflareTranslateTtsProvider() — Google Translate TTS source (WorkerTtsContract v6.4.1:
//   GET, text<=200, lang, rate normal|slow, audio/mpeg, no auth; voice/pitch/SSML NOT supported),
//   NpcVoiceProfileSelector(ISaveService),
//   Milo.Bind(IGameEventBus, IQuestService, ILearningService, IHintService, IAudioDirector)
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameInstaller : MonoBehaviour {
  public IGameEventBus EventBus { get; private set; }
  public ILearningService Learning { get; private set; }
  public IQuestService Quests { get; private set; }
  public ISpeechProvider SpeechProvider => _speechRouter; // v6.3: ROUTER — consumers inject once; switch happens inside
  public ISpeechPolicy SpeechPolicy { get; private set; }
  public ISaveService Save { get; private set; }
  public IHintService Hints { get; private set; }
  public QuestRewardService Rewards { get; private set; }
  public IAudioDirector Audio { get; private set; }
  public ISpeechSynthesisProvider Tts { get; private set; }
  public INpcVoiceSelector Voices { get; private set; }
  // Phase 3.0 world navigation (in-memory Scene/Session state, like Quests).
  public IWorldNavService WorldNav { get; private set; }
  // Phase 3.0.x subject scenes (S1b): the transition machine + scene ops.
  // Owned here, never newed elsewhere. Nothing drives them yet (S2 flips the
  // gate switch) — present so ownership is single and P34-tested via fakes.
  public WorldTransition WorldTransitions { get; private set; }
  public ISceneOps SceneOps { get; private set; }
  // Phase 2.1 mic-setup gate (additive): PC mic/headset OR phone mic.
  // SpeechMic (composite) is the future ISpeechRecognizer input so the frozen
  // SkippedNoMic/deferral policy ("tạm thời bỏ qua bài nghe") applies as-is.
  public MicrophoneDeviceService LocalMic { get; private set; }
  public PhoneMicrophoneDevice PhoneMic { get; private set; }
  public CompositeMicrophoneDevice SpeechMic { get; private set; }
  public MicSetupGate MicGate { get; private set; }

  SpeechProviderRouter _speechRouter;

  void Awake() {
    DontDestroyOnLoad(gameObject);
    SceneManager.sceneLoaded += OnSubjectSceneLoaded;
    EventBus = new GameEventBus();                    // Application
    Save = new LocalSave();                           // Application
    _speechRouter = new SpeechProviderRouter(new AzureSttProvider()); // Application, default online
    SpeechPolicy = new SpeechAssessmentPolicy();      // Application, stateless
    Learning = new LearningService(EventBus);         // Session
    Hints = new HintService(EventBus);                // Session logic, per-quest state
    Quests = new QuestManager(EventBus, Learning, Hints); // Scene/Session
    Rewards = new QuestRewardService(EventBus);             // Session reward state (friendship + world changes)
    // P1-4 foundation: boot adopts the persisted completions SILENTLY (no
    // events, no celebration replay) BEFORE any scene builds, so every later
    // Build/Adopt reads deterministic state. Save format untouched.
    // P1-7: every live completion is banked centrally here (covers ALL quests,
    // present + future — directors never hand-roll save code).
    try {
      PlayerProgress boot = Save != null ? Save.Load() : new PlayerProgress();
      // S3-P2L: system dialogue language before ANY line can be produced
      // (save value + "-lang vi/en" launch override).
      DialogueLang.Init(boot);
      System.Collections.Generic.List<string> done =
        boot != null && boot.QuestsDone != null ? boot.QuestsDone : new System.Collections.Generic.List<string>();
      QuestManager qm = Quests as QuestManager;
      if (qm != null) qm.RestoreCompleted(done);
      Rewards.RestoreCompleted(done);
      EventBus.Subscribe<QuestCompletedEvent>(e => {
        try { ActivityCompletion.PersistQuestDone(Save, e.QuestId); }
        catch (System.Exception) { }
      });
    } catch (System.Exception) { }
    WorldNav = new WorldNavService(EventBus);               // Phase 3.0: world-navigation state (in-memory)
    WorldTransitions = new WorldTransition(SubjectIds.Main); // Phase 3.0.x S1b: subject transition machine
    SceneOps = new UnitySceneOps();                          // Phase 3.0.x S1b: production scene adapter
    Tts = new CloudflareTranslateTtsProvider();          // Application, endpoint/config ngoài repo (Translate source)
    Audio = new AudioDirector(EventBus, Tts);         // Application, cache L1/L2 + Mixer + Focus
    Voices = new NpcVoiceProfileSelector(Save);       // Application, save.npcVoices + worldSeed
    // E2E hook (mic-less PC simulation): launch flag "-e2e-nomic" emulates a
    // machine with no microphone so the phone-mic offer/QR flow is reachable
    // in-build. Inert without the flag; production behavior unchanged.
    bool noMic = false;
    try {
      foreach (string a in System.Environment.GetCommandLineArgs())
        if (string.Equals(a, "-e2e-nomic", System.StringComparison.OrdinalIgnoreCase)) { noMic = true; break; }
    } catch (System.Exception) { }
    LocalMic = noMic ? new MicrophoneDeviceService(() => new string[0], null)
      : new MicrophoneDeviceService();                // Application, poll-based (no hot-plug event)
    PhoneMic = new PhoneMicrophoneDevice();             // Application, link-gated (NoDevice until probed)
    SpeechMic = new CompositeMicrophoneDevice(LocalMic, PhoneMic); // local wins, phone fallback
    MicGate = new MicSetupGate(LocalMic, PhoneMic);     // startup offer + exercise-entry policy
    Milo.Bind(EventBus, Quests, Learning, Hints, Audio);
    Mia.Bind(Audio); // shopkeeper voice (name readout + gentle retry)
    PregenSeeder.SeedFromStreamingAssets();           // D: offline L2 seeding before first audio use
    LoadMarketSceneAndBuild();
  }

  // W1 slice entry: BootstrapScene (this object) loads the MarketScene
  // additively, binds the code-built world, then hands quest narration to the
  // MarketBootstrap on this same GameObject. No ServiceLocator: services flow
  // as arguments, and only the composition root news them up.
  // NOTE: the build is driven by sceneLoaded (notinline after LoadScene):
  // depending on entry path the additive load may complete synchronously or
  // deferred; the event covers both, and _sliceBuilt keeps it exactly-once.
  bool _sliceBuilt;

  void LoadMarketSceneAndBuild() {
    Scene market = SceneManager.GetSceneByName("MarketScene");
    if (market.IsValid() && market.isLoaded) { BuildFromScene(market); return; }
    SceneManager.sceneLoaded += OnMarketSceneLoaded;
    SceneManager.LoadScene("MarketScene", LoadSceneMode.Additive);
    Scene after = SceneManager.GetSceneByName("MarketScene");
    if (after.IsValid() && after.isLoaded && !_sliceBuilt) {
      SceneManager.sceneLoaded -= OnMarketSceneLoaded;
      BuildFromScene(after);
    }
  }

  void OnMarketSceneLoaded(Scene scene, LoadSceneMode mode) {
    if (scene.name != "MarketScene") return;
    SceneManager.sceneLoaded -= OnMarketSceneLoaded;
    BuildFromScene(scene);
  }

  // Phase 3.0.x S2: MathScene arrival. Finds the MathWorld root, applies the
  // pilot spatial offset, code-builds the world (ground/arch/boundary/pads),
  // bakes its NavMesh and binds the return gate — all synchronously on the
  // main thread, BEFORE the loader task completes, so travelers never warp
  // into an unready world. Any failure leaves MathWorldRoot null and the
  // Bootstrap travel path cleans up (unload + stay Main) instead of stranding.
  public Transform MathWorldRoot { get; private set; }
  public Transform MathEntryPoint { get; private set; }
  // P1-2: Math presentation registry (null until the scene builds).
  public ActivityAnchors MathAnchors { get; private set; }

  void OnSubjectSceneLoaded(Scene scene, LoadSceneMode mode) {
    if (scene.name == CountingGardenBuilder.SceneName) {
      BuildCountingGardenScene(scene);
      return;
    }
    if (scene.name == CountingPlayBuilder.SceneName) {
      BuildCountingPlayScene(scene);
      return;
    }
    if (scene.name == StairHillBuilder.SceneName) {
      BuildStairPlayScene(scene);
      return;
    }
    if (scene.name == RabbitPlayBuilder.SceneName) {
      BuildRabbitPlayScene(scene);
      return;
    }
    if (scene.name == BuildTowerBuilder.SceneName) {
      BuildBuildTowerScene(scene);
      return;
    }
    if (scene.name != "MathScene") return;
    MathWorldRoot = null;
    MathEntryPoint = null;
    MathAnchors = null;
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "MathWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] MathScene has no MathWorld root.", this);
        return;
      }
      root.transform.position = MathWorldBuilder.WorldOffset;
      MathWorldBuilder builder = root.GetComponent<MathWorldBuilder>();
      if (builder == null) builder = root.AddComponent<MathWorldBuilder>();
      Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
        ? _activeBuilder.Player.transform : null;
      builder.Build(WorldNav, playerT);
      WireMathContent(root, builder);
      Transform entry = root.transform.Find("EntryPoint");
      if (entry == null) {
        Debug.LogError("[GameInstaller] MathScene has no EntryPoint marker.", this);
        return;
      }
      MathWorldRoot = root.transform;
      MathEntryPoint = entry;
      // P1-2/P1-3: expose anchors for the arrival beat, then push live quest
      // state into every adoptable (re-entry visuals without event replay).
      // S2: the MAIN world registry is looked up BY NAME — the Counting Garden
      // added a second ActivityAnchors root earlier in the hierarchy, so a
      // blind GetComponentInChildren could frame the garden instead of the hub.
      try {
        Transform mainAnchors = root.transform.Find("PresentationRoot");
        MathAnchors = mainAnchors != null ? mainAnchors.GetComponent<ActivityAnchors>() : null;
        if (MathAnchors == null) MathAnchors = root.GetComponentInChildren<ActivityAnchors>();
      } catch (System.Exception) { MathAnchors = null; }
      try {
        IQuestAdoptable[] adoptables = root.GetComponentsInChildren<IQuestAdoptable>();
        QuestAdoption.AdoptAll(adoptables, Quests, new QuestId("math_counting"));
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Math adopt-all failed: " + e.Message, this);
      }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] MathScene build failed: " + e.Message, this);
      MathWorldRoot = null;
      MathEntryPoint = null;
      MathAnchors = null;
    }
  }

  // S2 PIONEER MICRO-WORLD: lazy scene arrival for the Counting Garden. Builds
  // the scene content, then pushes the scene-authored entry + anchors into the
  // area module (living in MathScene) so the travel beat can warp the child in.
  void BuildCountingGardenScene(Scene scene) {
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "CountingGardenWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] CountingGardenScene has no CountingGardenWorld root.", this);
        return;
      }
      root.transform.position = CountingGardenBuilder.WorldOffset;
      CountingGardenBuilder builder = root.GetComponent<CountingGardenBuilder>();
      if (builder == null) builder = root.AddComponent<CountingGardenBuilder>();
      builder.Build();
      CountingGardenArea area = _gardenArea;
      if (area == null) {
        try { area = FindObjectOfType<CountingGardenArea>(); } catch (System.Exception) { }
      }
      if (area != null) {
        Vector3 entry = CountingGardenBuilder.WorldOffset + CountingGardenBuilder.EntryLocal;
        area.SetGarden(entry, builder.Anchors, builder.ZoneSpots);
        if (builder.ExitPortal != null) builder.ExitPortal.Area = area;
      }
      // S3-P2Y (user order: "player chưa chọn chơi thì demo vẫn phải chạy"):
      // the garden hosts an AMBIENT MINIATURE of the lesson — it loops for
      // everyone and its card camera stays off (the zone focus frames it), plus
      // it drives the "panel after the try-run" gate through the area module.
      Transform playerT2 = _activeBuilder != null && _activeBuilder.Player != null
        ? _activeBuilder.Player.transform : null;
      try {
        CountingDemo mini = root.AddComponent<CountingDemo>();
        mini.CameraBeatsEnabled = false;
        mini.Build(builder, playerT2,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio);
        if (area != null) area.BindDemo(mini);
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] garden mini demo wiring failed (garden stays quiet): " + e.Message, this);
      }
      // S3-P2Z12b (user report "NPC dạy trẻ chơi ở đâu?"): the stair hill plot
      // gets its OWN garden miniature — the two-NPC number lesson — so the plot
      // never looks empty; it drives the same panel-after-one-pass gate.
      try {
        StairLessonDemo stairs = root.AddComponent<StairLessonDemo>();
        stairs.Build(builder, playerT2, Audio);
        if (area != null) area.BindDemo(CountingGardenBuilder.StairZoneIndex, stairs);
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] stair garden mini demo wiring failed (plot stays scenery): " + e.Message, this);
      }
      try {
        Debug.Log("[GameInstaller] Counting Garden scene built (lazy) entry=" + (CountingGardenBuilder.WorldOffset + CountingGardenBuilder.EntryLocal).ToString("F1")
          + " zones=" + builder.ZoneSpots.Count);
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] CountingGardenScene build failed: " + e.Message, this);
    }
  }

  // S3 P2X PLAY ARENA (user order §47B): the zone-2 "Vào chơi" destination is
  // its OWN lazy scene, loaded into the shared micro slot by CountingGardenArea
  // (garden unload -> play load). Exactly the garden pattern: code-build here,
  // then push the entry/anchors into the area so the travel beat can warp the
  // child in. The demo lesson is wired HERE (its live home now) with the same
  // best-effort discipline: a demo failure degrades to a quiet arena, never a
  // stranded player.
  void BuildCountingPlayScene(Scene scene) {
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "CountingPlayWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] CountingPlayScene has no CountingPlayWorld root.", this);
        return;
      }
      root.transform.position = CountingPlayBuilder.WorldOffset;
      CountingPlayBuilder builder = root.GetComponent<CountingPlayBuilder>();
      if (builder == null) builder = root.AddComponent<CountingPlayBuilder>();
      builder.Build();
      CountingGardenArea area = _gardenArea;
      if (area == null) {
        try { area = FindObjectOfType<CountingGardenArea>(); } catch (System.Exception) { }
      }
      if (area != null) {
        Vector3 entry = CountingPlayBuilder.WorldOffset + CountingPlayBuilder.EntryLocal;
        area.SetPlay(entry, builder.Anchors, CountingPlayBuilder.WorldOffset,
          CountingPlayBuilder.BoundX, CountingPlayBuilder.BoundZ, CountingPlayBuilder.FollowOffset,
          null); // null objective = the area's default "Counting Playground"
        if (builder.ExitPortal != null) builder.ExitPortal.Area = area;
      }
      // S3-P2Z4 REFERENCE GAMEPLAY (user design): the arena runs the Number-2
      // lesson ONCE as the intro (teacher teaches, student demonstrates), then
      // the child plays "put the right number in the basket". The game owns the
      // activity lifecycle (in-memory, handed over from the Math-side area so
      // re-entry adopts the completed visual).
      try {
        CountingDemo intro = root.AddComponent<CountingDemo>();
        intro.LoopForever = false;
        // S3-P2Z9 (user order): the arena is the PLAY space — no demo replay;
        // the teacher reads the assignment once the child reaches the field.
        intro.NoIntroMode = true;
        intro.CameraBeatsEnabled = false;
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        intro.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio);
        CountingGame game = root.AddComponent<CountingGame>();
        // S3-P2Z10: the carried ball rides the child's ANIMATED fist (the same
        // bone the apple/ball presenters use), not the static root anchor — so
        // the pickup bend and the place reach actually move the ball with the
        // hand. Falls back to the root anchor when the rig is unavailable.
        Transform hand = null;
        try {
          if (_activeBuilder != null && _activeBuilder.PlayerViz != null
              && _activeBuilder.PlayerViz.HandBone != null) {
            hand = _activeBuilder.PlayerViz.HandBone;
          } else if (_activeBuilder != null) {
            hand = _activeBuilder.PlayerHand;
          }
        } catch (System.Exception) { }
        game.Build(intro, builder, playerT, hand,
          _gardenArea != null ? _gardenArea.GameLifecycle : null, Audio);
        if (_gardenArea != null) _gardenArea.BindGame(game);
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Counting game wiring failed (arena stays quiet): " + e.Message, this);
      }
      try {
        Debug.Log("[GameInstaller] Counting Play arena built (reference gameplay) entry="
          + (CountingPlayBuilder.WorldOffset + CountingPlayBuilder.EntryLocal).ToString("F1"));
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] CountingPlayScene build failed: " + e.Message, this);
    }
  }

  // S3-P2Z12 GAMEPLAY #2 ("Bậc thang con số"): the Counting Garden's sixth plot
  // opens its OWN lazy scene (StairPlayScene) through the same micro slot as the
  // reference arena — built here on demand, never at boot, never stacked.
  void BuildStairPlayScene(Scene scene) {
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "StairPlayWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] StairPlayScene has no StairPlayWorld root.", this);
        return;
      }
      root.transform.position = StairHillBuilder.WorldOffset;
      StairHillBuilder builder = root.GetComponent<StairHillBuilder>();
      if (builder == null) builder = root.AddComponent<StairHillBuilder>();
      // The round's mission comes from the area's ladder (progression/CLI);
      // the boards stage that digit so the world always shows the mission.
      int stairTarget = _gardenArea != null
        ? _gardenArea.StairTarget : StairHillBuilder.Target;
      builder.BoardTarget = StairHillBuilder.ClampTarget(stairTarget);
      builder.Build();
      CountingGardenArea area = _gardenArea;
      if (area == null) {
        try { area = FindObjectOfType<CountingGardenArea>(); } catch (System.Exception) { }
      }
      if (area != null) {
        Vector3 entry = StairHillBuilder.WorldOffset + StairHillBuilder.EntryLocal;
        area.SetPlay(entry, builder.Anchors, StairHillBuilder.WorldOffset,
          StairHillBuilder.BoundX, StairHillBuilder.BoundZ, StairHillBuilder.FollowOffset,
          DialogueLang.T(StairHillBuilder.ObjectiveEn, StairHillBuilder.ObjectiveVi));
        if (builder.ExitPortal != null) builder.ExitPortal.Area = area;
      }
      // The activity (teacher + student + the child's climb). Lifecycle is the
      // Math-side area's; the game plays the area's current target (one
      // staircase, many targets — brief §34).
      try {
        NumberStairs game = root.AddComponent<NumberStairs>();
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio,
          _gardenArea != null ? _gardenArea.StairLifecycle : null, stairTarget);
        if (_gardenArea != null) _gardenArea.BindStairGame(game);
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Number stairs wiring failed (hill stays empty): " + e.Message, this);
      }
      try {
        Debug.Log("[GameInstaller] Stair Play scene built (gameplay #2) entry="
          + (StairHillBuilder.WorldOffset + StairHillBuilder.EntryLocal).ToString("F1"));
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] StairPlayScene build failed: " + e.Message, this);
    }
  }

  // GAMEPLAY #3 ("Cho thỏ ăn đúng số"): the Counting Garden's carrot patch
  // opens its OWN lazy scene (RabbitPlayScene) through the same micro slot as
  // the two earlier arenas — built here on demand, never at boot, never stacked.
  void BuildRabbitPlayScene(Scene scene) {
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "RabbitPlayWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] RabbitPlayScene has no RabbitPlayWorld root.", this);
        return;
      }
      root.transform.position = RabbitPlayBuilder.WorldOffset;
      RabbitPlayBuilder builder = root.GetComponent<RabbitPlayBuilder>();
      if (builder == null) builder = root.AddComponent<RabbitPlayBuilder>();
      // The round's mission comes from the area's ladder (progression/CLI);
      // the boards stage that digit so the world always shows the mission.
      int rabbitTarget = _gardenArea != null
        ? _gardenArea.RabbitTarget : RabbitPlayBuilder.Target;
      builder.BoardTarget = RabbitPlayBuilder.ClampTarget(rabbitTarget);
      builder.Build();
      CountingGardenArea area = _gardenArea;
      if (area == null) {
        try { area = FindObjectOfType<CountingGardenArea>(); } catch (System.Exception) { }
      }
      if (area != null) {
        Vector3 entry = RabbitPlayBuilder.WorldOffset + RabbitPlayBuilder.EntryLocal;
        area.SetPlay(entry, builder.Anchors, RabbitPlayBuilder.WorldOffset,
          RabbitPlayBuilder.BoundX, RabbitPlayBuilder.BoundZ, RabbitPlayBuilder.FollowOffset,
          DialogueLang.T(RabbitPlayBuilder.ObjectiveEn, RabbitPlayBuilder.ObjectiveVi));
        if (builder.ExitPortal != null) builder.ExitPortal.Area = area;
      }
      // The activity (teacher + student + the child's feeding). Lifecycle is
      // the Math-side area's; the game plays the area's current target (one
      // patch, many targets — the same ladder discipline as gameplay #2).
      try {
        RabbitFeed game = root.AddComponent<RabbitFeed>();
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        Transform hand = null;
        try {
          if (_activeBuilder != null && _activeBuilder.PlayerViz != null
              && _activeBuilder.PlayerViz.HandBone != null) {
            hand = _activeBuilder.PlayerViz.HandBone;
          } else if (_activeBuilder != null) {
            hand = _activeBuilder.PlayerHand;
          }
        } catch (System.Exception) { }
        if (hand == null) hand = playerT;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio,
          _gardenArea != null ? _gardenArea.RabbitLifecycle : null, rabbitTarget);
        if (_gardenArea != null) _gardenArea.BindRabbitGame(game);
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Rabbit feed wiring failed (patch stays empty): " + e.Message, this);
      }
      try {
        Debug.Log("[GameInstaller] Rabbit Play scene built (gameplay #3) entry="
          + (RabbitPlayBuilder.WorldOffset + RabbitPlayBuilder.EntryLocal).ToString("F1"));
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] RabbitPlayScene build failed: " + e.Message, this);
    }
  }

  // GAMEPLAY #4 ("Xây tháp theo số"): the Math Hub's build_yard gate opens its
  // OWN lazy scene (BuildTowerScene) through the same micro slot as the garden
  // worlds — built here on demand, never at boot, never stacked. Travel beats
  // are owned by BuildTowerArea (living in MathScene); this method only builds
  // the world + wires the activity, same best-effort discipline as #1-#3.
  void BuildBuildTowerScene(Scene scene) {
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "BuildTowerWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] BuildTowerScene has no BuildTowerWorld root.", this);
        return;
      }
      root.transform.position = BuildTowerBuilder.WorldOffset;
      BuildTowerBuilder builder = root.GetComponent<BuildTowerBuilder>();
      if (builder == null) builder = root.AddComponent<BuildTowerBuilder>();
      // The round's mission comes from the area's ladder (progression/CLI);
      // the boards stage that digit so the world always shows the mission.
      int buildTarget = _buildArea != null
        ? _buildArea.Target : BuildTowerBuilder.Target;
      builder.BoardTarget = BuildTowerBuilder.ClampTarget(buildTarget);
      builder.Build();
      BuildTowerArea area = _buildArea;
      if (area == null) {
        try { area = FindObjectOfType<BuildTowerArea>(); } catch (System.Exception) { }
      }
      if (area != null) {
        Vector3 entry = BuildTowerBuilder.WorldOffset + BuildTowerBuilder.EntryLocal;
        area.SetWorld(entry, builder.Anchors,
          DialogueLang.T(BuildTowerBuilder.ObjectiveEn, BuildTowerBuilder.ObjectiveVi));
        if (builder.ExitPortal != null) builder.ExitPortal.BuildArea = area;
      }
      // The activity (teacher + student + the child's build). Lifecycle is the
      // Math-side area's; the game plays the area's current target (one yard,
      // many targets — the same ladder discipline as #2/#3).
      try {
        BuildTowerGame game = root.AddComponent<BuildTowerGame>();
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        Transform hand = null;
        try {
          if (_activeBuilder != null && _activeBuilder.PlayerViz != null
              && _activeBuilder.PlayerViz.HandBone != null) {
            hand = _activeBuilder.PlayerViz.HandBone;
          } else if (_activeBuilder != null) {
            hand = _activeBuilder.PlayerHand;
          }
        } catch (System.Exception) { }
        if (hand == null) hand = playerT;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio,
          _buildArea != null ? _buildArea.Lifecycle : null, buildTarget,
          _buildArea != null ? (System.Action<int>)_buildArea.NotifyCompleted : null);
        if (_buildArea != null) _buildArea.BindGame(game);
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Build tower wiring failed (yard stays empty): " + e.Message, this);
      }
      try {
        Debug.Log("[GameInstaller] Build Tower scene built (gameplay #4) entry="
          + (BuildTowerBuilder.WorldOffset + BuildTowerBuilder.EntryLocal).ToString("F1"));
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] BuildTowerScene build failed: " + e.Message, this);
    }
  }

  // Phase 3.0.x S3: Math playable-skeleton wiring (runs on the main thread
  // inside the sceneLoaded callback, before the loader task completes).
  // Tess host + quest director + counting-object bus bindings. Best-effort:
  // any failure degrades to a silent-but-enterable world (travel roots still
  // set by the caller) — never a stranded player, never an exception out.
  void WireMathContent(GameObject root, MathWorldBuilder builder) {
    try {
      if (builder != null && builder.CountingObjects != null && EventBus != null) {
        foreach (Interactable inter in builder.CountingObjects) {
          if (inter != null) inter.Bind(EventBus);
        }
      }
      Tess.Bind(Audio);
      Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
        ? _activeBuilder.Player.transform : null;
      GameObject tessGo = new GameObject("Tess");
      tessGo.transform.SetParent(root.transform, true);
      MathHostPresenter host = tessGo.AddComponent<MathHostPresenter>();
      host.SpawnPosition = MathWorldBuilder.WorldOffset + MathWorldBuilder.HostAnchorLocal;
      host.PlayerTarget = playerT;
      host.Bind(EventBus, Quests, Hints);
      NpcDefinition tessDef = NpcRoster.Get("tess");
      GameObject labelGo = new GameObject("TessLabel");
      labelGo.transform.SetParent(tessGo.transform, false);
      WorldNameLabel label = labelGo.AddComponent<WorldNameLabel>();
      if (tessDef != null) label.Setup(tessDef.displayName, tessGo.transform, tessDef.labelHeight);
      else label.Setup("Tess", tessGo.transform, 2.35f);
      label.Show();
      GameObject dirGo = new GameObject("MathQuestDirector");
      dirGo.transform.SetParent(root.transform, true);
      MathQuestDirector director = dirGo.AddComponent<MathQuestDirector>();
      MarketHUD hud = _activeBuilder != null ? _activeBuilder.Hud : null;
      director.Build(EventBus, Quests, host, hud);
      // S3B: carry-token lifecycle + bloom consumer (bus-only wiring, same
      // best-effort discipline as above).
      Interactable cube = null;
      try {
        if (builder.CountingObjects != null && builder.CountingObjects.Count > 0)
          cube = builder.CountingObjects[0];
      } catch (System.Exception) { }
      Transform hand = null;
      try { hand = _activeBuilder != null ? _activeBuilder.PlayerHand : null; }
      catch (System.Exception) { }
      if (hand == null) hand = playerT;
      GameObject carryGo = new GameObject("MathTokenCarry");
      carryGo.transform.SetParent(root.transform, true);
      MathTokenCarry carry = carryGo.AddComponent<MathTokenCarry>();
      try { carry.Build(EventBus, Quests, cube, hand); }
      catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Math carry wiring failed: " + e.Message, this);
      }
      try {
        if (builder.BloomRoot != null) {
          MathBloomDisplay bloom = builder.BloomRoot.gameObject.AddComponent<MathBloomDisplay>();
          bloom.Build(EventBus, Quests); // journey fix: adopt a completed quest on re-entry
        }
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Math bloom wiring failed: " + e.Message, this);
      }
      // S2 PIONEER MICRO-WORLD (v2): the Counting Garden is its OWN scene,
      // LAZY-loaded only when the child walks into the hub gate. This area
      // module (living in MathScene) drives the travel beats and receives the
      // garden scene's entry + anchors on each load.
      try {
        CountingGardenArea area = root.GetComponent<CountingGardenArea>();
        if (area == null) {
          GameObject areaGo = new GameObject("CountingGardenArea");
          areaGo.transform.SetParent(root.transform, true);
          area = areaGo.AddComponent<CountingGardenArea>();
        }
        _gardenArea = area;
        area.Bind(
          WorldTransitions,
          SceneOps,
          _activeBuilder != null ? _activeBuilder.Player : null,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null,
          _activeBuilder != null ? _activeBuilder.Hud : null,
          MathWorldBuilder.WorldOffset + MathWorldBuilder.GardenHubReturnLocal);
        area.BindRouter(_activeBuilder != null ? _activeBuilder.Router : null);
        // S3-P2X: the persistent zone panel (built by MarketBootstrap on THIS
        // object) is the UI end of the focus beat; the area owns the logic.
        try {
          MarketBootstrap bootstrap = GetComponent<MarketBootstrap>();
          Debug.Log("[GameInstaller] zone panel bind check: bootstrap=" + (bootstrap != null)
            + " panel=" + (bootstrap != null && bootstrap.ZonePanel != null)
            + " area=" + (area != null), this);
          if (bootstrap != null && bootstrap.ZonePanel != null) area.BindPanel(bootstrap.ZonePanel);
        } catch (System.Exception) { }
        if (builder.CountingGardenPortal != null) builder.CountingGardenPortal.Area = area;
        MicroWorldPortal[] portals = root.GetComponentsInChildren<MicroWorldPortal>(true);
        foreach (MicroWorldPortal portal in portals) {
          // S3-P2Z14: the build_yard portal belongs to its OWN area (gameplay
          // #4) — never let the garden swallow it.
          if (portal != null && portal.areaId == CountingGardenArea.AreaId) portal.Area = area;
        }
        try { Debug.Log("[GameInstaller] Counting Garden area wired (" + portals.Length + " hub portals).", this); }
        catch (System.Exception) { }
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Counting Garden wiring failed: " + e.Message, this);
      }
      // S3-P2Z14 GAMEPLAY #4: the Build Yard's own area module (living in
      // MathScene like the garden's) drives gate -> micro-world travel and
      // receives the yard scene's entry + anchors on each load.
      try {
        BuildTowerArea buildArea = root.GetComponent<BuildTowerArea>();
        if (buildArea == null) {
          GameObject buildGo = new GameObject("BuildTowerArea");
          buildGo.transform.SetParent(root.transform, true);
          buildArea = buildGo.AddComponent<BuildTowerArea>();
        }
        _buildArea = buildArea;
        buildArea.Bind(
          WorldTransitions,
          SceneOps,
          _activeBuilder != null ? _activeBuilder.Player : null,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null,
          _activeBuilder != null ? _activeBuilder.Hud : null,
          MathWorldBuilder.WorldOffset + MathWorldBuilder.BuildYardHubReturnLocal);
        buildArea.BindRouter(_activeBuilder != null ? _activeBuilder.Router : null);
        buildArea.Landmark = builder.BuildTowerLandmark;
        buildArea.PushLandmarkState();
        if (builder.BuildTowerPortal != null) builder.BuildTowerPortal.BuildArea = buildArea;
        try { Debug.Log("[GameInstaller] Build Yard area wired (portal="
          + (builder.BuildTowerPortal != null) + " landmark="
          + (builder.BuildTowerLandmark != null) + ").", this); }
        catch (System.Exception) { }
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Build Yard area wiring failed: " + e.Message, this);
      }
    } catch (System.Exception e) {
      Debug.LogWarning("[GameInstaller] Math content wiring failed (world stays enterable): " + e.Message, this);
    }
  }
  // Phase 2.4: persisted gender (Boy default for migration). Applied to the
  // already-built PlayerVisual via re-tint (same mesh, no rebuild) so no
  // gameplay interrupts.
  // REMOVED 2026-09-18 by user order (girl visual failed gate): Boy-only.
  // Gender selection UI, panel, toggle and Girl visual are deleted; any saved
  // Girl coerces to Boy on boot (logged). Save fields stay (architecture
  // intact, no save-format break).
  MarketBuilder _activeBuilder;
  CountingGardenArea _gardenArea;
  BuildTowerArea _buildArea;

  public PlayerGender CurrentGender {
    get {
      try {
        PlayerGender g = Load().PlayerGender;
        return g == PlayerGender.Girl ? PlayerGender.Boy : g;
      } catch (System.Exception) { return PlayerGender.Boy; }
    }
  }

  public bool HasChosenGender() {
    return true; // no panel anymore: everyone is treated as decided
  }

  PlayerProgress Load() {
    try { return Save != null ? Save.Load() : new PlayerProgress(); } catch (System.Exception) { return new PlayerProgress(); }
  }

  public void SetPlayerGender(PlayerGender gender) {
    try {
      if (gender != PlayerGender.Boy) {
        try { Debug.Log("[GameInstaller] Girl unavailable (removed) — staying Boy.", this); }
        catch (System.Exception) { }
        gender = PlayerGender.Boy;
      }
      PlayerProgress p = Load();
      p.PlayerGender = PlayerGender.Boy;
      p.GenderChosen = true;
      if (Save != null) Save.Save(p);
      if (_activeBuilder != null) _activeBuilder.SetPlayerGender(PlayerGender.Boy);
    } catch (System.Exception e) { Debug.LogWarning("[GameInstaller] SetPlayerGender failed: " + e.Message, this); }
  }

  void BuildFromScene(Scene market) {
    if (_sliceBuilt) return;
    _sliceBuilt = true;
    MarketBuilder builder = null;
    if (market.IsValid()) {
      foreach (GameObject root in market.GetRootGameObjects()) {
        builder = root.GetComponentInChildren<MarketBuilder>(true);
        if (builder != null) break;
      }
    }
    if (builder == null) {
      Debug.LogError("[GameInstaller] MarketScene has no MarketBuilder; slice cannot start.", this);
      return;
    }
    _activeBuilder = builder;
    // Boy-only (removal order): persisted gender is coerced — a saved Girl
    // can never spawn a visual anymore.
    try {
      PlayerGender g = Load().PlayerGender;
      if (g != PlayerGender.Boy) {
        try { Debug.Log("[GameInstaller] Saved Girl coerced to Boy (gender selection removed).", this); }
        catch (System.Exception) { }
        g = PlayerGender.Boy;
      }
      builder.SetPlayerGender(g);
    } catch (System.Exception) { }
    builder.BuildServices(EventBus, Audio);
    builder.WireQuestService(Quests, Hints);
    builder.SetWorldNav(WorldNav); // Phase 3.0: push nav service into subject gates
    MarketBootstrap bootstrap = GetComponent<MarketBootstrap>();
    if (bootstrap == null) {
      Debug.LogError("[GameInstaller] No MarketBootstrap on the installer object; slice cannot start.", this);
      return;
    }
    bootstrap.Build(EventBus, Quests, Hints, builder, Audio, new MicSetupBundle(
      MicGate, LocalMic, PhoneMic, SpeechMic,
      PhoneMicProtocol.LoopbackHost, PhoneMicProtocol.DefaultBridgePort),
      WorldTransitions, SceneOps); // Phase 3.0.x S2: shared loader for scene-backed gates
  }

  void Update() {
    // G-toggle REMOVED with gender selection (Boy-only by user order).
  }

  // Runtime online->offline swap INSIDE the router: every injected consumer
  // switches together (no stale Azure instance). Agent D calls this; D never `new`s providers itself.
  public void SwitchToFallbackSpeech() => _speechRouter.SwitchTo(new FallbackSpeechProvider());

  public void SwitchToOnlineSpeech() => _speechRouter.SwitchTo(new AzureSttProvider());

#if UNITY_EDITOR || DEBUG
  // Tests only. Production never uses Mock at runtime. DEBUG (not the deprecated
  // DEVELOPMENT_BUILD, UAC0009) covers Editor + development/test players and is
  // undefined in release production builds.
  public void UseMockSpeechForTests() => _speechRouter.SwitchTo(new MockSpeechProvider());
#endif
}
