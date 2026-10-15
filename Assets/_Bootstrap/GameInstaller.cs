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
  // WP-1.9.33 speaking foundation: the offline acoustic engine + the local
  // whisper service compose into ONE game-facing recognizer (4-level verdicts).
  public TargetPronunciationCatalog PronunciationCatalog { get; private set; }
  public ISpeechRecognizer Speech { get; private set; }
  LocalAssessServiceClient _assessClient;
  bool _speakingGateBypass; // dev seam (-speak-wav-dir): capture is scripted

  // Read one value from the command line: -flag <value>. Empty when absent.
  static string CLIFlag(string flag) {
    try {
      string[] args = System.Environment.GetCommandLineArgs();
      for (int i = 0; i + 1 < args.Length; i++) {
        if (string.Equals(args[i], flag, System.StringComparison.OrdinalIgnoreCase))
          return args[i + 1];
      }
    } catch (System.Exception) { }
    return "";
  }

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
      // USER ROUND 2026-09-29: the theme tune starts at boot; the top-right
      // chip toggles it and the choice persists in the save.
      try {
        MusicSettings.Init(Save != null ? Save.Load() : null);
        MusicSettings.Bind(Audio);
      } catch (System.Exception) { }
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
    // WP-1.9.33: the speaking stack — offline acoustic engine (always) + the
    // local speak-assess service (transcript when running; graceful without it).
    try {
      PronunciationCatalog = new TargetPronunciationCatalog();
      SpeakingPack.Apply((word, phonemes) => PronunciationCatalog.Register(word, phonemes));
      var speakingPolicy = SpeakingPassPolicy.Default();
      // WP-1.9.42 (owner-approved default flip): the offline acoustic slot now runs the
      // max-spec chain (frontend -> DSP -> resilience -> fusion -> session). Its evidence is
      // POLICY-COMPATIBLE (calibration parity 27/27 — same pass/fail as the classic path),
      // and it unlocks the per-syllable #11 UI + the age-4 session scaffolding payloads.
      // Escape hatch: "-speak-classic-acoustic" restores the pre-1.9.42 LocalAcoustic slot.
      ISpeechAssessmentProvider acousticProvider = new MaxSpecAdaptiveSpeechProvider(PronunciationCatalog);
      if (!string.IsNullOrEmpty(CLIFlag("-speak-classic-acoustic"))) {
        acousticProvider = new LocalAcousticProvider(PronunciationCatalog);
        Debug.Log("[GameInstaller] speaking acoustic slot = LocalAcousticProvider (classic opt-out)", this);
      } else {
        Debug.Log("[GameInstaller] speaking acoustic slot = MaxSpecAdaptiveSpeechProvider (default, policy-compatible)", this);
      }
      _assessClient = new LocalAssessServiceClient(LocalAssessServiceLauncher.DefaultPort);
      var serviceProvider = new LocalServiceAssessmentProvider(_assessClient);

      // Connect Azure Speech Provider when key is configured, fallback to local service
      ISpeechAssessmentProvider activeCloudOrLocalService = serviceProvider;
      var azureProvider = new AzureSpeechAssessmentProvider();
      if (azureProvider.IsConfigured()) {
        activeCloudOrLocalService = azureProvider;
        Debug.Log("[GameInstaller] Azure Speech Pronunciation Assessment ENABLED (Region: " + AzureSpeechAssessmentProvider.ReadRegionFromEnvironment() + ")", this);
      }

      var hybrid = new HybridSpeechAssessmentProvider(acousticProvider, activeCloudOrLocalService, speakingPolicy);
      ISpeechAudioCapture capture;
      IMicrophoneDevice speakingDevices = SpeechMic;
      string wavDir = CLIFlag("-speak-wav-dir");
      if (!string.IsNullOrEmpty(wavDir)) {
        // DEV/TEST seam (foreground journeys): replay WAV files instead of mic.
        // The recognizer gates on the DEVICE capability before it asks the
        // capture, so the seam must also present a Ready scripted device.
        capture = new ScriptedWavCapture(wavDir);
        speakingDevices = new ScriptedWavDevice();
        _speakingGateBypass = true;
        Debug.Log("[GameInstaller] speaking capture = ScriptedWavCapture dir=" + wavDir, this);
      } else {
        var localCapture = new UnityMicrophoneCapture(LocalMic);
        var phoneCapture = new NetworkMicrophoneCapture(
          new TcpPhoneAudioTransport(PhoneMicProtocol.LoopbackHost, PhoneMicProtocol.DefaultBridgePort));
        capture = new GateSpeechCapture(localCapture, phoneCapture, MicGate);
      }
      Speech = new SpeechRecognizer(speakingDevices, capture, hybrid, speakingPolicy,
        EventBus, 5f, 1.5f, 12f);
    } catch (System.Exception ex) {
      Debug.LogWarning("[GameInstaller] speaking stack init failed (speaking stays off): " + ex.Message, this);
      Speech = null;
    }
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

  // PHASE 5 LEGACY PURGE: MathScene arrival (MathWorldRoot/EntryPoint/Anchors)
  // is deleted with the scene and its builder.

  void OnSubjectSceneLoaded(Scene scene, LoadSceneMode mode) {
    if (scene.name == StairHillBuilder.SceneName) {
      BuildStairPlayScene(scene);
      return;
    }
    if (scene.name == RabbitPlayBuilder.SceneName) {
      BuildRabbitPlayScene(scene);
      return;
    }
    if (scene.name == GeometryPlayBuilder.SceneName) {
      BuildGeometryPlayScene(scene);
      return;
    }
    if (scene.name == ComparisonMarketBuilder.SceneName) {
      BuildComparisonMarketPlayScene(scene);
      return;
    }
    if (scene.name == ClassificationCityBuilder.SceneName) {
      BuildClassificationCityPlayScene(scene);
      return;
    }
    if (scene.name == ListeningGardenBuilder.SceneName) {
      BuildListeningGardenPlayScene(scene);
      return;
    }
    if (scene.name == SpeakingStageBuilder.SceneName) {
      BuildSpeakingStagePlayScene(scene);
      return;
    }
    if (scene.name == WonderKitchenBuilder.SceneName) {
      BuildWonderKitchenPlayScene(scene);
      return;
    }
      if (scene.name == SentenceExpressBuilder.SceneName) {
        BuildSentenceExpressPlayScene(scene);
      }
      if (scene.name == PhonicsGardenBuilder.SceneName) {
        BuildPhonicsGardenPlayScene(scene);
      }
      if (scene.name == VillageCourtyardBuilder.SceneName) {
        BuildVillageCourtyardPlayScene(scene);
      }
    // Ga Thứ Tự (ordering_station) DELETED 2026-10-13 (owner order): the whole
    // arena (code folder, scene, dispatch branch and gate) is removed.
    // PHASE 5 LEGACY PURGE: Discovery Garden, MathScene and CountingGardenScene
    // are deleted (scenes, builders, areas, drivers, tests).
    // FULL ARCHITECTURE RESET (PHASE 1 skeleton): the generic selection yard
    // (Skill Yard / Game Yard — one scene, data-driven by the area context).
    if (scene.name == SelectionYardBuilder.SceneName) {
      BuildSelectionYardScene(scene);
    }
  }

  // PHASE 5 LEGACY PURGE: BuildCountingGardenScene deleted with the
  // CountingGardenScene (two-plot garden, mini lessons, zone picker).

  // S3-P2Z12 GAMEPLAY #2 ("Bậc thang con số"): the lazy arena scene built here
  // on demand, never at boot, never stacked.
  void BuildStairPlayScene(Scene scene) {
    ActivityGuide.SetHidden(true); // owner 2026-10-13: no beacon in the arenas
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
      // The round's mission comes from the Selection Yard's ladder (PHASE 5:
      // the obsolete garden fallback is deleted); the boards stage that digit
      // so the world always shows the mission.
      int stairTarget = _yardArea != null ? _yardArea.StairTarget : StairHillBuilder.Target;
      // PHASE 4b (user order: pre-readers cannot read): the stair arena runs
      // voice-first — no task text, no praise banner (dots + voice carry it).
      ActivityFeedback.TextHidden = true;
      builder.BoardTarget = StairHillBuilder.ClampTarget(stairTarget);
      builder.Build();
      if (_yardArea != null) {
        // PHASE 4: the arena binds to the Selection Yard (entry/exit + the
        // re-homed lifecycle).
        Vector3 entry = StairHillBuilder.WorldOffset + StairHillBuilder.EntryLocal;
        _yardArea.SetPlay(entry, builder.Anchors, StairHillBuilder.WorldOffset,
          StairHillBuilder.BoundX, StairHillBuilder.BoundZ, StairHillBuilder.FollowOffset,
          DialogueLang.T(StairHillBuilder.ObjectiveEn, StairHillBuilder.ObjectiveVi));
        if (builder.ExitPortal != null) builder.ExitPortal.YardArea = _yardArea;
      }
      // The activity (teacher + student + the child's climb). Lifecycle is the
      // yard's; the game plays its current target (one staircase, many
      // targets — brief §34).
      try {
        NumberStairs game = root.AddComponent<NumberStairs>();
        // S3-P2Z32: live play mixes random plain numbers with +/- (the body is
        // the number line: climb up = add, walk down = subtract).
        game.ArithmeticEnabled = true;
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        ActivityLifecycle stairLife = _yardArea != null ? _yardArea.StairLifecycle : null;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio,
          stairLife, stairTarget);
        if (_yardArea != null) _yardArea.BindStairGame(game);
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
    ActivityGuide.SetHidden(true); // owner 2026-10-13: no beacon in the arenas
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
      // The round's mission comes from the Selection Yard's ladder (PHASE 5:
      // the obsolete garden fallback is deleted); the boards stage that digit
      // so the world always shows the mission.
      int rabbitTarget = _yardArea != null ? _yardArea.RabbitTarget : RabbitPlayBuilder.Target;
      // PHASE 4b (user order: pre-readers cannot read): the rabbit arena runs
      // voice-first — no task text, no praise banner (dots + voice carry it).
      ActivityFeedback.TextHidden = true;
      builder.BoardTarget = RabbitPlayBuilder.ClampTarget(rabbitTarget);
      builder.Build();
      if (_yardArea != null) {
        // PHASE 4: bind the arena to the Selection Yard (entry/exit + the
        // re-homed rabbit lifecycle).
        Vector3 entry = RabbitPlayBuilder.WorldOffset + RabbitPlayBuilder.EntryLocal;
        _yardArea.SetPlay(entry, builder.Anchors, RabbitPlayBuilder.WorldOffset,
          RabbitPlayBuilder.BoundX, RabbitPlayBuilder.BoundZ, RabbitPlayBuilder.FollowOffset,
          DialogueLang.T(RabbitPlayBuilder.ObjectiveEn, RabbitPlayBuilder.ObjectiveVi));
        if (builder.ExitPortal != null) builder.ExitPortal.YardArea = _yardArea;
      }
      // The activity (teacher + student + the child's feeding). Lifecycle is
      // the yard's; the game plays its current target (one patch, many
      // targets — the same ladder discipline as gameplay #2).
      try {
        RabbitFeed game = root.AddComponent<RabbitFeed>();
        // S3-P2Z26: live play mixes random plain numbers with +/- within 10.
        game.ArithmeticEnabled = true;
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
        ActivityLifecycle rabbitLife = _yardArea != null ? _yardArea.RabbitLifecycle : null;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio,
          rabbitLife, rabbitTarget, hand);
        // S3-P2Z26: drag a fed carrot off the bowl back to the garden.
        RabbitBowlDrag drag = root.AddComponent<RabbitBowlDrag>();
        if (drag != null) drag.Bind(game);
        if (_yardArea != null) _yardArea.BindRabbitGame(game);
        // USER ROUND 2026-09-29 (round 2): the picture-answer boards of the
        // rabbit arena's new question type.
        try { game.BindQuizTiles(builder.QuizTiles); } catch (System.Exception) { }
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

  void BuildGeometryPlayScene(Scene scene) {
    ActivityGuide.SetHidden(true); // owner 2026-10-13: no beacon in the arenas
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "GeometryPlayWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] GeometryPlayScene has no GeometryPlayWorld root.", this);
        return;
      }
      root.transform.position = GeometryPlayBuilder.WorldOffset;
      GeometryPlayBuilder builder = root.GetComponent<GeometryPlayBuilder>();
      if (builder == null) builder = root.AddComponent<GeometryPlayBuilder>();
      ActivityFeedback.TextHidden = true;
      builder.Build();
      if (_yardArea != null) {
        Vector3 entry = GeometryPlayBuilder.WorldOffset + GeometryPlayBuilder.EntryLocal;
        _yardArea.SetPlay(entry, builder.Anchors, GeometryPlayBuilder.WorldOffset,
          GeometryPlayBuilder.BoundX, GeometryPlayBuilder.BoundZ, GeometryPlayBuilder.FollowOffset,
          DialogueLang.T(GeometryPlayBuilder.ObjectiveEn, GeometryPlayBuilder.ObjectiveVi));
        if (builder.ExitPortal != null) builder.ExitPortal.YardArea = _yardArea;
      }
      try {
        GeometryPlay game = root.GetComponent<GeometryPlay>();
        if (game == null) game = root.AddComponent<GeometryPlay>();
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        ActivityLifecycle geoLife = _yardArea != null ? _yardArea.GeometryLifecycle : null;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio, geoLife);
        if (_yardArea != null) _yardArea.BindGeometryGame(game);
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Geometry play wiring failed: " + e.Message, this);
      }
      try {
        Debug.Log("[GameInstaller] Geometry Play scene built entry="
          + (GeometryPlayBuilder.WorldOffset + GeometryPlayBuilder.EntryLocal).ToString("F1"));
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] GeometryPlayScene build failed: " + e.Message, this);
    }
  }

  void BuildComparisonMarketPlayScene(Scene scene) {
    ActivityGuide.SetHidden(true); // owner 2026-10-13: no beacon in the arenas
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "ComparisonMarketWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] ComparisonMarketPlayScene has no ComparisonMarketWorld root.", this);
        return;
      }
      root.transform.position = ComparisonMarketBuilder.WorldOffset;
      ComparisonMarketBuilder builder = root.GetComponent<ComparisonMarketBuilder>();
      if (builder == null) builder = root.AddComponent<ComparisonMarketBuilder>();
      ActivityFeedback.TextHidden = true;
      builder.Build();
      if (_yardArea != null) {
        Vector3 entry = ComparisonMarketBuilder.WorldOffset + ComparisonMarketBuilder.EntryLocal;
        _yardArea.SetPlay(entry, builder.Anchors, ComparisonMarketBuilder.WorldOffset,
          ComparisonMarketBuilder.BoundX, ComparisonMarketBuilder.BoundZ, ComparisonMarketBuilder.FollowOffset,
          DialogueLang.T(ComparisonMarketBuilder.ObjectiveEn, ComparisonMarketBuilder.ObjectiveVi));
        if (builder.ExitPortal != null) builder.ExitPortal.YardArea = _yardArea;
      }
      try {
        ComparisonMarket game = root.GetComponent<ComparisonMarket>();
        if (game == null) game = root.AddComponent<ComparisonMarket>();
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        ActivityLifecycle marketLife = _yardArea != null ? _yardArea.MarketLifecycle : null;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio, marketLife);
        if (_yardArea != null) _yardArea.BindMarketGame(game);
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Market play wiring failed: " + e.Message, this);
      }
      try {
        Debug.Log("[GameInstaller] Market Play scene built entry="
          + (ComparisonMarketBuilder.WorldOffset + ComparisonMarketBuilder.EntryLocal).ToString("F1"));
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] ComparisonMarketPlayScene build failed: " + e.Message, this);
    }
  }

  void BuildClassificationCityPlayScene(Scene scene) {
    ActivityGuide.SetHidden(true); // owner 2026-10-13: no beacon in the arenas
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "ClassificationCityWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] ClassificationCityPlayScene has no ClassificationCityWorld root.", this);
        return;
      }
      root.transform.position = ClassificationCityBuilder.WorldOffset;
      ClassificationCityBuilder builder = root.GetComponent<ClassificationCityBuilder>();
      if (builder == null) builder = root.AddComponent<ClassificationCityBuilder>();
      ActivityFeedback.TextHidden = true;
      builder.Build();
      if (_yardArea != null) {
        Vector3 entry = ClassificationCityBuilder.WorldOffset + ClassificationCityBuilder.EntryLocal;
        _yardArea.SetPlay(entry, builder.Anchors, ClassificationCityBuilder.WorldOffset,
          ClassificationCityBuilder.BoundX, ClassificationCityBuilder.BoundZ, ClassificationCityBuilder.FollowOffset,
          DialogueLang.T(ClassificationCityBuilder.ObjectiveEn, ClassificationCityBuilder.ObjectiveVi));
        if (builder.ExitPortal != null) builder.ExitPortal.YardArea = _yardArea;
      }
      try {
        ClassificationCity game = root.GetComponent<ClassificationCity>();
        if (game == null) game = root.AddComponent<ClassificationCity>();
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        ActivityLifecycle cityLife = _yardArea != null ? _yardArea.CityLifecycle : null;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio, cityLife);
        if (_yardArea != null) _yardArea.BindCityGame(game);
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] City play wiring failed: " + e.Message, this);
      }
      try {
        Debug.Log("[GameInstaller] City Play scene built entry="
          + (ClassificationCityBuilder.WorldOffset + ClassificationCityBuilder.EntryLocal).ToString("F1"));
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] ClassificationCityPlayScene build failed: " + e.Message, this);
    }
  }

  // WP-1.9.32: English -> Listening foundation ("Vườn Tai Thính"). Same build
  // contract as the other reset-era arenas; the item audio ships as pregen
  // masters (voice 'listen_v1') so every prompt plays offline.
  void BuildListeningGardenPlayScene(Scene scene) {
    ActivityGuide.SetHidden(true); // owner 2026-10-13: no beacon in the arenas
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "ListeningGardenWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] ListeningGardenPlayScene has no ListeningGardenWorld root.", this);
        return;
      }
      root.transform.position = ListeningGardenBuilder.WorldOffset;
      ListeningGardenBuilder builder = root.GetComponent<ListeningGardenBuilder>();
      if (builder == null) builder = root.AddComponent<ListeningGardenBuilder>();
      ActivityFeedback.TextHidden = true;
      builder.Build();
      if (_yardArea != null) {
        Vector3 entry = ListeningGardenBuilder.WorldOffset + ListeningGardenBuilder.EntryLocal;
        _yardArea.SetPlay(entry, builder.Anchors, ListeningGardenBuilder.WorldOffset,
          ListeningGardenBuilder.BoundX, ListeningGardenBuilder.BoundZ, ListeningGardenBuilder.FollowOffset,
          DialogueLang.T(ListeningGardenBuilder.ObjectiveEn, ListeningGardenBuilder.ObjectiveVi));
        if (builder.ExitPortal != null) builder.ExitPortal.YardArea = _yardArea;
      }
      try {
        ListeningGarden game = root.GetComponent<ListeningGarden>();
        if (game == null) game = root.AddComponent<ListeningGarden>();
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        ActivityLifecycle listeningLife = _yardArea != null ? _yardArea.ListeningLifecycle : null;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio, listeningLife);
        if (_yardArea != null) _yardArea.BindListeningGame(game);
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Listening garden wiring failed: " + e.Message, this);
      }
      try {
        Debug.Log("[GameInstaller] Listening Garden scene built entry="
          + (ListeningGardenBuilder.WorldOffset + ListeningGardenBuilder.EntryLocal).ToString("F1"));
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] ListeningGardenPlayScene build failed: " + e.Message, this);
    }
  }

  // WP-1.9.33: English -> Speaking foundation ("Sân Khấu Nhí"). The arena gets
  // the ONE game-facing recognizer (Speech); the local assess service is warmed
  // in the background when playing (best-effort, the game works without it).
  void BuildSpeakingStagePlayScene(Scene scene) {
    ActivityGuide.SetHidden(true); // owner 2026-10-13: no beacon in the arenas
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "SpeakingStageWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] SpeakingStagePlayScene has no SpeakingStageWorld root.", this);
        return;
      }
      root.transform.position = SpeakingStageBuilder.WorldOffset;
      SpeakingStageBuilder builder = root.GetComponent<SpeakingStageBuilder>();
      if (builder == null) builder = root.AddComponent<SpeakingStageBuilder>();
      ActivityFeedback.TextHidden = true;
      builder.Build();
      if (_yardArea != null) {
        Vector3 entry = SpeakingStageBuilder.WorldOffset + SpeakingStageBuilder.EntryLocal;
        _yardArea.SetPlay(entry, builder.Anchors, SpeakingStageBuilder.WorldOffset,
          SpeakingStageBuilder.BoundX, SpeakingStageBuilder.BoundZ, SpeakingStageBuilder.FollowOffset,
          DialogueLang.T(SpeakingStageBuilder.ObjectiveEn, SpeakingStageBuilder.ObjectiveVi));
        if (builder.ExitPortal != null) builder.ExitPortal.YardArea = _yardArea;
      }
      try {
        SpeakingStage game = root.GetComponent<SpeakingStage>();
        if (game == null) game = root.AddComponent<SpeakingStage>();
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        ActivityLifecycle speakingLife = _yardArea != null ? _yardArea.SpeakingLifecycle : null;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio, speakingLife,
          Speech, _speakingGateBypass ? null : MicGate);
        if (_yardArea != null) _yardArea.BindSpeakingStage(game);
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Speaking stage wiring failed: " + e.Message, this);
      }
      WarmAssessService();
      try {
        Debug.Log("[GameInstaller] Speaking Stage scene built entry="
          + (SpeakingStageBuilder.WorldOffset + SpeakingStageBuilder.EntryLocal).ToString("F1"));
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] SpeakingStagePlayScene build failed: " + e.Message, this);
    }
  }

  void BuildWonderKitchenPlayScene(Scene scene) {
    ActivityGuide.SetHidden(true);
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "WonderKitchenWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] WonderKitchenPlayScene has no WonderKitchenWorld root.", this);
        return;
      }
      root.transform.position = WonderKitchenBuilder.WorldOffset;
      WonderKitchenBuilder builder = root.GetComponent<WonderKitchenBuilder>();
      if (builder == null) builder = root.AddComponent<WonderKitchenBuilder>();
      ActivityFeedback.TextHidden = true;
      builder.Build();
      if (_yardArea != null) {
        Vector3 entry = WonderKitchenBuilder.WorldOffset + WonderKitchenBuilder.EntryLocal;
        _yardArea.SetPlay(entry, builder.Anchors, WonderKitchenBuilder.WorldOffset,
          WonderKitchenBuilder.BoundX, WonderKitchenBuilder.BoundZ, WonderKitchenBuilder.FollowOffset,
          DialogueLang.T(WonderKitchenBuilder.ObjectiveEn, WonderKitchenBuilder.ObjectiveVi));
        if (builder.ExitPortal != null) builder.ExitPortal.YardArea = _yardArea;
      }
      try {
        WonderKitchen game = root.GetComponent<WonderKitchen>();
        if (game == null) game = root.AddComponent<WonderKitchen>();
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        ActivityLifecycle kitchenLife = _yardArea != null ? _yardArea.KitchenLifecycle : null;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio, kitchenLife);
        if (_yardArea != null) _yardArea.BindKitchenGame(game);
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Wonder kitchen wiring failed: " + e.Message, this);
      }
      try {
        Debug.Log("[GameInstaller] Wonder Kitchen scene built entry="
          + (WonderKitchenBuilder.WorldOffset + WonderKitchenBuilder.EntryLocal).ToString("F1"));
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] WonderKitchenPlayScene build failed: " + e.Message, this);
    }
  }

  void BuildSentenceExpressPlayScene(Scene scene) {
    ActivityGuide.SetHidden(true);
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "SentenceExpressWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] SentenceExpressPlayScene has no SentenceExpressWorld root.", this);
        return;
      }
      root.transform.position = SentenceExpressBuilder.WorldOffset;
      SentenceExpressBuilder builder = root.GetComponent<SentenceExpressBuilder>();
      if (builder == null) builder = root.AddComponent<SentenceExpressBuilder>();
      ActivityFeedback.TextHidden = true;
      builder.Build();
      if (_yardArea != null) {
        Vector3 entry = SentenceExpressBuilder.WorldOffset + SentenceExpressBuilder.EntryLocal;
        _yardArea.SetPlay(entry, builder.Anchors, SentenceExpressBuilder.WorldOffset,
          SentenceExpressBuilder.BoundX, SentenceExpressBuilder.BoundZ, SentenceExpressBuilder.FollowOffset,
          DialogueLang.T(SentenceExpressBuilder.ObjectiveEn, SentenceExpressBuilder.ObjectiveVi));
        if (builder.ExitPortal != null) builder.ExitPortal.YardArea = _yardArea;
      }
      try {
        SentenceExpress game = root.GetComponent<SentenceExpress>();
        if (game == null) game = root.AddComponent<SentenceExpress>();
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        ActivityLifecycle sentenceLife = _yardArea != null ? _yardArea.SentenceLifecycle : null;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio, sentenceLife);
        if (_yardArea != null) _yardArea.BindSentenceGame(game);
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] Sentence express wiring failed: " + e.Message, this);
      }
      try {
        Debug.Log("[GameInstaller] Sentence Express scene built entry="
          + (SentenceExpressBuilder.WorldOffset + SentenceExpressBuilder.EntryLocal).ToString("F1"));
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] SentenceExpressPlayScene build failed: " + e.Message, this);
    }
  }

  void BuildPhonicsGardenPlayScene(Scene scene) {
    ActivityGuide.SetHidden(true);
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "PhonicsGardenWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] PhonicsGardenPlayScene has no PhonicsGardenWorld root.", this);
        return;
      }
      root.transform.position = PhonicsGardenBuilder.WorldOffset;
      PhonicsGardenBuilder builder = root.GetComponent<PhonicsGardenBuilder>();
      if (builder == null) builder = root.AddComponent<PhonicsGardenBuilder>();
      ActivityFeedback.TextHidden = true;
      builder.Build();
      if (_yardArea != null) {
        Vector3 entry = PhonicsGardenBuilder.WorldOffset + PhonicsGardenBuilder.EntryLocal;
        _yardArea.SetPlay(entry, builder.Anchors, PhonicsGardenBuilder.WorldOffset,
          PhonicsGardenBuilder.BoundX, PhonicsGardenBuilder.BoundZ, PhonicsGardenBuilder.FollowOffset,
          DialogueLang.T("Learn Vietnamese with teacher!", "Học tiếng Việt cùng cô nhé!"));
        if (builder.ExitPortal != null) builder.ExitPortal.YardArea = _yardArea;
      }
      try {
        PhonicsGarden game = root.GetComponent<PhonicsGarden>();
        if (game == null) game = root.AddComponent<PhonicsGarden>();
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        // Both Vietnamese games share this scene; the launched gameId picks the mode + lifecycle.
        string gameId = _yardArea != null ? _yardArea.CurrentGameId : "";
        bool discovery = string.Equals(gameId, "discovery_garden", System.StringComparison.OrdinalIgnoreCase);
        ActivityLifecycle life = _yardArea != null
          ? (discovery ? _yardArea.DiscoveryLifecycle : _yardArea.SpellingLifecycle) : null;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio, life, null,
          discovery ? PhonicsPlayMode.RealWorldDiscovery : PhonicsPlayMode.SpellingGarden);
        if (_yardArea != null) {
          if (discovery) _yardArea.BindDiscoveryGarden(game);
          else _yardArea.BindSpellingGarden(game);
        }
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] phonics garden wiring failed: " + e.Message, this);
      }
      try {
        Debug.Log("[GameInstaller] Phonics Garden scene built gameId="
          + (_yardArea != null ? _yardArea.CurrentGameId : "")
          + " entry=" + (PhonicsGardenBuilder.WorldOffset + PhonicsGardenBuilder.EntryLocal).ToString("F1"));
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] PhonicsGardenPlayScene build failed: " + e.Message, this);
    }
  }

  void BuildVillageCourtyardPlayScene(Scene scene) {
    ActivityGuide.SetHidden(true);
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "VillageCourtyardWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] VillageCourtyardPlayScene has no VillageCourtyardWorld root.", this);
        return;
      }
      root.transform.position = VillageCourtyardBuilder.WorldOffset;
      VillageCourtyardBuilder builder = root.GetComponent<VillageCourtyardBuilder>();
      if (builder == null) builder = root.AddComponent<VillageCourtyardBuilder>();
      ActivityFeedback.TextHidden = true;
      builder.Build();
      if (_yardArea != null) {
        Vector3 entry = VillageCourtyardBuilder.WorldOffset + VillageCourtyardBuilder.EntryLocal;
        _yardArea.SetPlay(entry, builder.Anchors, VillageCourtyardBuilder.WorldOffset,
          VillageCourtyardBuilder.BoundX, VillageCourtyardBuilder.BoundZ, VillageCourtyardBuilder.FollowOffset,
          DialogueLang.T("Speak Vietnamese at the courtyard!", "Bé nói tiếng Việt tại sân đình!"));
        if (builder.ExitPortal != null) builder.ExitPortal.YardArea = _yardArea;
      }
      try {
        VillageCourtyardGameController game = root.GetComponent<VillageCourtyardGameController>();
        if (game == null) game = root.AddComponent<VillageCourtyardGameController>();
        Transform playerT = _activeBuilder != null && _activeBuilder.Player != null
          ? _activeBuilder.Player.transform : null;
        ActivityLifecycle life = _yardArea != null ? _yardArea.CourtyardLifecycle : null;
        game.Build(builder, playerT,
          _activeBuilder != null ? _activeBuilder.WorldCamera : null, Audio, life, null, null, null);
        if (_yardArea != null) {
          _yardArea.BindCourtyardGame(game);
        }
      } catch (System.Exception e) {
        Debug.LogWarning("[GameInstaller] village courtyard wiring failed: " + e.Message, this);
      }
      try {
        Debug.Log("[GameInstaller] Village Courtyard scene built entry="
          + (VillageCourtyardBuilder.WorldOffset + VillageCourtyardBuilder.EntryLocal).ToString("F1"));
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] VillageCourtyardPlayScene build failed: " + e.Message, this);
    }
  }

  // Best-effort background warm-up of tools/speak_assess_server.py (never blocks
  // gameplay; the hybrid falls back to acoustic-only when it is absent).
  void WarmAssessService() {
    try {
      if (!Application.isPlaying) return;
      string toolsDir = System.IO.Path.Combine(
        System.IO.Directory.GetParent(Application.dataPath).FullName, "tools");
      int port = LocalAssessServiceLauncher.DefaultPort;
      System.Threading.Tasks.Task.Run(() => {
        try {
          bool ok = LocalAssessServiceLauncher.EnsureRunning(port, toolsDir);
          Debug.Log("[GameInstaller] speak-assess service " + (ok ? "ready" : "unavailable (acoustic-only)"));
        } catch (System.Exception) { }
      });
    } catch (System.Exception) { }
  }

  // Ga Thứ Tự (ordering_station) DELETED 2026-10-13 (owner order): the arena
  // builder, scene and every wiring are gone; there is no station scene.

  // COUNTING GARDEN CLEANUP (product decision): the match_meadow gameplay
  // (scene build + wiring) was removed. Its hub gate remains a LANDMARK.

  // FULL ARCHITECTURE RESET (PHASE 1 skeleton): the generic selection yard.
  // ONE lazy scene renders either the subject's Skill Yard (B) or a skill's
  // Game Yard (C) from LearningMap data; the pending context is pushed by
  // SelectionYardArea before the load. Gameplay is NOT wired here yet
  // (PHASE 4 migrates the two approved arenas into the C level).
  void BuildSelectionYardScene(Scene scene) {
    try {
      GameObject root = null;
      if (scene.IsValid()) {
        foreach (GameObject go in scene.GetRootGameObjects()) {
          if (go != null && go.name == "SelectionYardWorld") { root = go; break; }
        }
      }
      if (root == null) {
        Debug.LogError("[GameInstaller] SelectionYardScene has no SelectionYardWorld root.", this);
        return;
      }
      root.transform.position = SelectionYardBuilder.WorldOffset;
      SelectionYardBuilder builder = root.GetComponent<SelectionYardBuilder>();
      if (builder == null) builder = root.AddComponent<SelectionYardBuilder>();
      SelectionYardArea area = _yardArea;
      if (area == null) {
        try { area = FindObjectOfType<SelectionYardArea>(); } catch (System.Exception) { }
      }
      if (area != null) {
        builder.Level = area.PendingLevel == SelectionYardArea.YardLevel.Game ? "game" : "skill";
        builder.SubjectId = area.PendingSubjectId;
        builder.SkillId = area.PendingSkillId;
      }
      builder.Build();
      if (area != null) {
        Vector3 entry = SelectionYardBuilder.WorldOffset + SelectionYardBuilder.EntryLocal;
        area.SetYard(entry, builder.Anchors);
        for (int i = 0; i < builder.GatePortals.Count; i++) {
          SelectionGate portal = builder.GatePortals[i];
          if (portal != null) portal.Bind(area, portal.Kind, portal.TargetId);
        }
        if (builder.BackGate != null)
          builder.BackGate.Bind(area, SelectionGate.GateKind.Back, "");
      }
      try {
        Debug.Log("[GameInstaller] Selection Yard scene built (level=" + builder.Level
          + " subject=" + builder.SubjectId + " skill=" + builder.SkillId
          + " gates=" + builder.GatePortals.Count + ").", this);
      } catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogError("[GameInstaller] SelectionYardScene build failed: " + e.Message, this);
    }
  }

  // GAMEPLAY #7 ("Vườn Khám Phá") + PHASE 5 LEGACY PURGE: the Discovery Garden
  // scene, its builder/area/game and this build branch are deleted (owner plan
  // §9); no discovery route exists anymore.

  // COUNTING GARDEN CLEANUP (product decision): the build_yard and
  // delivery_village gameplay (scene builds + wiring) were removed. Their hub
  // gates remain LANDMARK SKELETONS with no playable path.

  // PHASE 5 LEGACY PURGE: WireMathContent (MathScene content: Tess host, quest
  // director, token carry, bloom display, counting-garden + discovery areas) is
  // deleted with MathScene/CountingGardenArea/DiscoveryArea.

  // Phase 2.4: persisted gender (Boy default for migration). Applied to the
  // already-built PlayerVisual via re-tint (same mesh, no rebuild) so no
  // gameplay interrupts.
  // REMOVED 2026-09-18 by user order (girl visual failed gate): Boy-only.
  // Gender selection UI, panel, toggle and Girl visual are deleted; any saved
  // Girl coerces to Boy on boot (logged). Save fields stay (architecture
  // intact, no save-format break).
  MarketBuilder _activeBuilder;
  SelectionYardArea _yardArea;

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
    // FULL ARCHITECTURE RESET (2026-09-29, PHASE 1 SKELETON): the selection-yard
    // area module lives here in MarketScene (which never unloads) and owns the
    // A -> B -> C travel beats (Subject Yard = this world; Skill/Game yards =
    // one generic lazy scene). Additive: nothing calls it until PHASE 2 wires
    // the subject gates.
    try {
      SelectionYardArea yard = GetComponentInChildren<SelectionYardArea>(true);
      if (yard == null) {
        GameObject yardGo = new GameObject("SelectionYardArea");
        yardGo.transform.SetParent(builder.transform, true);
        yard = yardGo.AddComponent<SelectionYardArea>();
      }
      _yardArea = yard;
      yard.Bind(
        WorldTransitions,
        SceneOps,
        builder.Player,
        builder.WorldCamera,
        builder.Hud,
        SubjectCatalog.HubCenter);
      yard.BindRouter(builder.Router);
      // S3-P2Z20 (re-homed): live play asks random number questions.
      yard.RandomRabbitTargets = true;
      builder.SetSelectionYard(yard); // PHASE 2: subject gates open skill yards
      try { Debug.Log("[GameInstaller] Selection Yard area wired (phase 1 skeleton).", this); }
      catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogWarning("[GameInstaller] Selection Yard wiring failed: " + e.Message, this);
    }
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
