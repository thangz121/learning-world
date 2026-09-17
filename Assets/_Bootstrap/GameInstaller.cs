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
    EventBus = new GameEventBus();                    // Application
    Save = new LocalSave();                           // Application
    _speechRouter = new SpeechProviderRouter(new AzureSttProvider()); // Application, default online
    SpeechPolicy = new SpeechAssessmentPolicy();      // Application, stateless
    Learning = new LearningService(EventBus);         // Session
    Hints = new HintService(EventBus);                // Session logic, per-quest state
    Quests = new QuestManager(EventBus, Learning, Hints); // Scene/Session
    Rewards = new QuestRewardService(EventBus);             // Session reward state (friendship + world changes)
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

  // Phase 2.4: persisted gender (Boy default for migration). Applied to the
  // already-built PlayerVisual via re-tint (same mesh, no rebuild) so no
  // gameplay interrupts. Public API lets future UI or G-key toggle persist.
  MarketBuilder _activeBuilder;

  public PlayerGender CurrentGender {
    get {
      try { return Load().PlayerGender; } catch (System.Exception) { return PlayerGender.Boy; }
    }
  }

  PlayerProgress Load() {
    try { return Save != null ? Save.Load() : new PlayerProgress(); } catch (System.Exception) { return new PlayerProgress(); }
  }

  public void SetPlayerGender(PlayerGender gender) {
    try {
      PlayerProgress p = Load();
      p.PlayerGender = gender;
      if (Save != null) Save.Save(p);
      if (_activeBuilder != null) _activeBuilder.SetPlayerGender(gender);
      Debug.Log("[GameInstaller] Player gender set to " + gender, this);
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
    // Apply persisted gender to the already-spawned player (re-tint only).
    try {
      PlayerGender g = Load().PlayerGender;
      builder.SetPlayerGender(g);
    } catch (System.Exception) { }
    builder.BuildServices(EventBus, Audio);
    builder.WireQuestService(Quests, Hints);
    MarketBootstrap bootstrap = GetComponent<MarketBootstrap>();
    if (bootstrap == null) {
      Debug.LogError("[GameInstaller] No MarketBootstrap on the installer object; slice cannot start.", this);
      return;
    }
    bootstrap.Build(EventBus, Quests, Hints, builder, Audio, new MicSetupBundle(
      MicGate, LocalMic, PhoneMic, SpeechMic,
      PhoneMicProtocol.LoopbackHost, PhoneMicProtocol.DefaultBridgePort));
  }

  void Update() {
    // Phase 2.4 QA: G toggles Boy/Girl live (same session, persisted).
    try {
      if (Input.GetKeyDown(KeyCode.G)) {
        PlayerGender cur = Load().PlayerGender;
        PlayerGender next = cur == PlayerGender.Boy ? PlayerGender.Girl : PlayerGender.Boy;
        SetPlayerGender(next);
      }
    } catch (System.Exception) { }
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
