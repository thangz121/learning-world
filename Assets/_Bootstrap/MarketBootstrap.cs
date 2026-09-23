// _Bootstrap/MarketBootstrap.cs — Lead owns. W1 vertical-slice wiring.
// Lives on the same GameObject as GameInstaller (BootstrapScene). After
// GameInstaller creates services and loads MarketScene, Build() instantiates
// the B_Brain NPC presenters at the world anchors, wires replay/objective HUD,
// starts the W1 quest, and drives Milo's spoken quest flow over the EventBus.
// No gameplay rules here (those live in QuestManager/HintService/Milo);
// this class only connects agents' components in the mandated order.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class MarketBootstrap : MonoBehaviour {
  static readonly QuestId W1QuestApple = new QuestId("w1_mia_apple");
  static readonly QuestId W1QuestBall = new QuestId("w1_mia_ball");
  static readonly WordId AppleWord = new WordId("apple");
  static readonly WordId BallWord = new WordId("ball");

  IGameEventBus _bus;
  IQuestService _quests;
  IAudioDirector _audio;
  MarketHUD _hud;
  MarketBuilder _builder;
  WorldNameLabel _miloLabel;
  WorldNameLabel _miaLabel;
  // Ground guidance line (player report): pre-talk/post-complete -> Milo,
  // bring -> Mia, find (answer step) -> hidden. Driven next to the HUD
  // objective text so the two can never disagree.
  QuestGuideLine _guide;
  Transform _miloT;
  Transform _miaT;
  bool _built;
  // Phase 3.0.x S2 scene travel (scene-backed subjects only; the spatial
  // siblings never touch these).
  WorldTransition _worldTransition;
  ISceneOps _sceneOps;
  bool _travelLock;
  Vector3 _mainReturnPos;
  Vector3 _mainReturnGate;      // gate of the subject being entered (journey fix)
  Vector3 _mainReturnOut;       // gate -> hub direction (safe re-entry landing)
  string _activeSubjectScene;
  GameObject _miloGo;
  GameObject _miaGo;
  // Track which quest is active (only one at a time in W1)
  QuestId? _activeQuest;
  // R7: pre-talk finds must not narrate. Set by QuestStartedEvent.
  bool _questStarted;
  // P1-6: the ONE shared input lock (router + subject gates read it).
  // Created here (Lead-owned Bootstrap), pushed into the builder's router +
  // gates. Open dialogs NEVER lock (J1 click-through rule).
  readonly InteractionGate _gate = new InteractionGate();
  // P1-1 second consumer (math director is the first): the Main market
  // activity lifecycle. Talk-gated like math: Available -> Active on quest
  // start, Active -> Completed on w1 completion, back to Available on next start.
  readonly ActivityLifecycle _marketLifecycle = new ActivityLifecycle("market", "MarketBootstrap");

  public InteractionGate Gate {
    get { return _gate; }
  }

  public ActivityLifecycle MarketLifecycle {
    get { return _marketLifecycle; }
  }
  // Phase 3.0: objective cached before a world entry, restored on return —
  // quest text is never clobbered by world navigation.

  // 2F ball-quest dialogue (data-driven, NOT new literals): exact manifest
  // texts, pinned to Content/dialogues/manifest.json by CT-P10 (code mirrors
  // content until runtime catalog loading lands; validators own the truth).
  // Call params match the 2D L2 contract exactly (rate/pitch 1.0, Clear).
  // S3-P2L: English stays the pinned mirror default; Vietnamese mode speaks the
  // translated line (same manifest slot, delivered live through the Director).
  static string BallAskText { get { return DialogueLang.T("Ball please!", "Bóng nhé!"); } } // manifest inst_07
  static string BallPraiseText { get { return DialogueLang.T("Great! Ball!", "Gi?i! Bóng!"); } } // manifest ok_05

  // Called ONCE by GameInstaller after MarketScene is loaded. All services are
  // constructed; the MarketBuilder (A) has built the world in its Awake.
  // mic is optional (null = mic-setup gate off; all existing flows untouched).
  public MicSetupMonitor MicMonitor { get; private set; }
  public GameCameraStreamService CameraStream { get; private set; }
  public LocalCameraService LocalCamera { get; private set; }
  public MediaRecordingService MediaRecorder { get; private set; }
  public DependencySetupService DependencySetup { get; private set; }
  // S3-P2X zone picker (persistent UI; bound to CountingGardenArea by GameInstaller).
  public GardenZonePanel ZonePanel { get; private set; }
  public void Build(IGameEventBus bus, IQuestService quests, IHintService hints, MarketBuilder builder, IAudioDirector audio = null, MicSetupBundle mic = null, WorldTransition worldTransition = null, ISceneOps sceneOps = null) {
    if (_built) return;
    _built = true;
    _bus = bus;
    _quests = quests;
    _audio = audio;
    if (bus == null || quests == null || hints == null || builder == null) {
      Debug.LogError("[MarketBootstrap] Build called with null dependencies; slice cannot start.", this);
      return;
    }
    _builder = builder;
    _worldTransition = worldTransition;
    _sceneOps = sceneOps;
    try { builder.BuildPersistentCore(); } catch (System.Exception) { }

// B presenters (Unity instantiates via AddComponent; Bind injects services).
    // Hub-selection mode: NO NPCs in the gate-selection hall (quests live in
    // subject worlds now). Milo/Mia presenters + labels are skipped entirely;
    // everything below null-guards them, and the quest loop never starts
    // without a first talk.
    MiloPresenter miloPresenter = null;
    MiaPresenter miaPresenter = null;
    _miloGo = null;
    _miaGo = null;
    if (!MarketBuilder.HubSelectionOnly) {
      _miloGo = new GameObject("Milo");
      miloPresenter = _miloGo.AddComponent<MiloPresenter>();
      miloPresenter.PlayerTarget = builder.Player != null ? builder.Player.transform : null;
      miloPresenter.Bind(bus, quests, hints);
      miloPresenter.OnFirstTalk = OnFirstTalk;
      miloPresenter.OnTalk = OnTalk;

      _miaGo = new GameObject("Mia");
      miaPresenter = _miaGo.AddComponent<MiaPresenter>();
      miaPresenter.PlayerTarget = builder.Player != null ? builder.Player.transform : null;
      miaPresenter.Bind(bus, quests, hints);
      _miloT = _miloGo.transform;
      _miaT = _miaGo.transform;
    }

    // Shop-counter click proxy (Phase-1 closure): tapping Mia's counter reaches
    // Mia herself, so players never need pixel taps on her body behind the
    // counter/awning. Reusable ClickForwarder; quest reactions stay in Mia.
    if (builder.StallCounter != null) {
      ClickForwarder counterFwd = builder.StallCounter.GetComponent<ClickForwarder>();
      if (counterFwd == null) counterFwd = builder.StallCounter.AddComponent<ClickForwarder>();
      counterFwd.Bind(miaPresenter);
    }

    // In-world identity (reusable for future chapters): skipped entirely in
    // hub-selection mode (no NPCs, no labels).
    if (!MarketBuilder.HubSelectionOnly) {
    // Milo is named from frame one (he is the first action target); Mia stays
    // unlabeled until the story introduces her, so frame one never splits
    // attention.
    // Labels live on CHILD objects: WorldNameLabel drives its own world
    // position every frame, so it must never sit on the NPC root itself
    // (it would fight the presenter's transform).
    // 2E: display names + heights come from the NPC roster (single source;
    // no "Milo"/"Mia" literals scattered). Behavior unchanged.
    NpcDefinition miloDef = NpcRoster.Get("milo");
    NpcDefinition miaDef = NpcRoster.Get("mia");
    string miloName = miloDef != null ? miloDef.displayName : "Milo";
    string miaName = miaDef != null ? miaDef.displayName : "Mia";
    float miloHeight = miloDef != null ? miloDef.labelHeight : 2.35f;
    float miaHeight = miaDef != null ? miaDef.labelHeight : 2.35f;
    GameObject miloLabelGo = new GameObject("MiloLabel");
    miloLabelGo.transform.SetParent(_miloGo.transform, false);
    _miloLabel = miloLabelGo.AddComponent<WorldNameLabel>();
    // Player-experience audit 2026-09-12 (W1Audit m-label/g-mia/k-correct):
    // 2.05m sits inside Milo's hard-hat ridge / Mia's hair volume, so level
    // cameras see the pill with the text occluded. 2.35m clears all headwear
    // (Milo 1.65 + hat ~0.3, Mia 1.65 + hair ~0.3, pill half-height 0.19).
    _miloLabel.Setup(miloName, _miloGo.transform, miloHeight);
    _miloLabel.Show();
    GameObject miaLabelGo = new GameObject("MiaLabel");
    miaLabelGo.transform.SetParent(_miaGo.transform, false);
    _miaLabel = miaLabelGo.AddComponent<WorldNameLabel>();
    _miaLabel.Setup(miaName, _miaGo.transform, miaHeight);
    // Player report: Mia's name shows from frame one, like Milo's (no more
    // hidden-until-introduction — the child should always read who is who).
    _miaLabel.Show();
    }

    // HUD: objective text + replay delegates to Milo (no World->Brain reference).
    _hud = builder.Hud;
    if (_hud != null) _hud.OnReplayPressed = Milo.RepeatInstruction;

    // S3-P2L+ (user order): a beautiful language chooser at EVERY launch —
    // the game mixes Vietnamese and English subjects, so the child picks the
    // system dialogue language before play. DialogueLang stays the single
    // source of truth (save + HUD chip unchanged).
    try {
      GameObject langGo = new GameObject("LanguageDialog");
      langGo.transform.SetParent(transform, false);
      LanguageDialog dialog = langGo.AddComponent<LanguageDialog>();
      dialog.Build(_hud);
      // Waits for every system notification (mic/dependency/recording) to be
      // confirmed first — Update shows it once nothing else is on screen.
      dialog.QueueShow();
    } catch (System.Exception) { }

    // S3-P2X zone picker (user order §47B): the pink card for the Counting
    // Garden's plot focus. Built here (persistent bootstrap object) so it
    // survives the garden <-> play scene swap; GameInstaller binds the area
    // when MathScene wires the garden (late binding, single owner).
    try {
      GameObject zoneGo = new GameObject("GardenZonePanel");
      zoneGo.transform.SetParent(transform, false);
      ZonePanel = zoneGo.AddComponent<GardenZonePanel>();
      ZonePanel.Build();
      ZonePanel.Hide();
      try { Debug.Log("[MarketBootstrap] zone panel created id=" + ZonePanel.GetHashCode(), this); }
      catch (System.Exception) { }
    } catch (System.Exception e) {
      Debug.LogWarning("[MarketBootstrap] zone panel build failed: " + e.Message, this);
    }

    // Quest flow narration (event-driven; rules stay in services/Milo).
    // Production event routing: WordSeen/WordSpoken on the bus drive quest
    // advancement (tests drive Advance* directly, so this glue affects only
    // the live slice, never unit-test behavior). IQuestService stays frozen;
    // the W0-T1 helpers live on QuestManager, hence the scoped downcast.
    if (quests is QuestManager questManager) {
      bus.Subscribe<WordSeenEvent>(e => questManager.AdvanceOnSeen(e.WordId));
      bus.Subscribe<WordSpokenEvent>(e => questManager.AdvanceOnSpoken(e.WordId, e.Result.Level));
    } else {
      Debug.LogWarning("[MarketBootstrap] QuestService is not QuestManager; seen/spoken routing disabled.", this);
    }
    bus.Subscribe<WordSeenEvent>(OnWordSeen);
    bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted);
    bus.Subscribe<StoryMomentEvent>(OnStoryMoment);
    bus.Subscribe<QuestStartedEvent>(OnQuestStartedFlag);
    bus.Subscribe<WorldChangedEvent>(OnWorldChanged); // Phase 3.0: world-transition presentation

    // Opening is talk-gated (first-time readability): the HUD names the one
    // action ("Talk to Milo"); Milo's proximity greet + name label do the
    // inviting. The quest (bubble, instruction voice, HUD action line) starts
    // when the player actually talks to him — never before.
    // Hub-selection mode: no Milo, no quest — the hall invites gate-picking
    // instead ("Choose a gate!"), and the quest guide stays home.
    if (miloPresenter != null) miloPresenter.OnFirstTalk = OnFirstTalk;
    if (builder.Bubble != null) builder.Bubble.Hide();
    if (_hud != null) _hud.ShowObjective(MarketBuilder.HubSelectionOnly
      ? DialogueLang.T("Choose a gate!", "Ch?n m?t c?ng nhé!")
      : DialogueLang.T("Talk to Milo", "Nói chuy?n v?i Milo"));
    if (!MarketBuilder.HubSelectionOnly) {
      // Guide starts at Milo (the one pre-talk action).
      GameObject guideGo = new GameObject("QuestGuideLine");
      _guide = guideGo.AddComponent<QuestGuideLine>();
      _guide.SetStage(GuideStage.ToMilo, _miloT);
    }

    // Mic-setup gate (Phase 2.1, additive): startup offer when no mic, silent
    // background rechecks, exercise-entry re-prompt. The monitor's Start()
    // runs the startup check on the next frame; future listening exercises
    // gate on MicMonitor.CheckBeforeListening(token) (quest #3 wiring).
    // NOTE 2026-09-18: gender selection removed by user order (girl visual
    // failed gate) — Boy-only, mic wires immediately, no identity panel.
    if (mic != null && mic.Gate != null) {
      WireMic(mic);
    }
    // Phone camera stream (Phase 2.2, additive, independent of the mic gate):
    // realtime face preview, bottom-left. Own bridge port (8452), own thread,
    // own frame slot — camera can never starve speech. Entry/exit logged (one
    // line each: R10 log-spam lesson); any failure leaves the game running
    // with CameraStream=null (gameplay never depends on camera, §17).
    PhoneCameraHud camHud = null;
    try {
      Debug.Log("[PhoneCamera] wiring camera stream service + HUD");
      GameObject camSvcGo = new GameObject("PhoneCameraStream");
      GameCameraStreamService camSvc = camSvcGo.AddComponent<GameCameraStreamService>();
      camSvc.Bind(PhoneCameraProtocol.LoopbackHost,
        PhoneCameraProtocol.DefaultBridgePort, PhoneCameraConfig.Default);
      camSvc.StartService();
      GameObject camHudGo = new GameObject("PhoneCameraHud");
      camHud = camHudGo.AddComponent<PhoneCameraHud>();
      camHud.Bind(camSvc);
      CameraStream = camSvc;
      Debug.Log("[PhoneCamera] wired (service running=" + camSvc.IsRunning
        + " state=" + camSvc.State + ")");
    } catch (Exception e) {
      Debug.LogWarning("[PhoneCamera] wiring failed, game continues without camera: " + e.Message);
      CameraStream = null;
    }
    // Local PC camera (precedence follow-up, additive): the laptop/integrated
    // camera — or a plugged-in USB webcam, which outranks it — plays DIRECTLY
    // in the same HUD box (local Live wins, phone is the fallback). Own
    // capture, own polling, no LAN, no gateway. Any failure (no camera,
    // denied, headless) leaves the phone path exactly as before.
    try {
      GameObject localCamGo = new GameObject("LocalCameraStream");
      LocalCameraService localCam = localCamGo.AddComponent<LocalCameraService>();
      localCam.StartService();
      LocalCamera = localCam;
      if (camHud != null) camHud.BindLocal(localCam);
      Debug.Log("[LocalCamera] wired (service running=" + localCam.IsRunning + ")");
    } catch (Exception e) {
      Debug.LogWarning("[LocalCamera] wiring failed, phone camera path unchanged: " + e.Message);
      LocalCamera = null;
    }
    // Media recording (Phase 2.3, additive): PC-side session recorder —
    // gameplay capture + phone-camera stream + phone-mic audio -> WAV/AVI
    // intermediates -> MP4 (H.264 + camera PiP) + MP3 (LAME) via the isolated
    // FFmpeg backend when available (verified-intermediate fallback when
    // not). Explicit control only (F2 dev/E2E toggle + public Start/Stop
    // API); nothing auto-records on connect or game start. Any failure
    // leaves the game running with MediaRecorder=null (gameplay never
    // depends on recording).
    try {
      Debug.Log("[MediaRec] wiring recording service");
      GameObject recGo = new GameObject("MediaRecorder");
      MediaRecordingService rec = recGo.AddComponent<MediaRecordingService>();
      Camera gameCam = null;
      try {
        if (builder.WorldCamera != null) gameCam = builder.WorldCamera.GetComponent<Camera>();
      } catch (Exception) { }
      if (gameCam == null) {
        try { gameCam = Camera.main; } catch (Exception) { }
      }
      rec.Bind(MediaRecordingConfig.Default, CameraStream, LocalCamera,
        () => MicMonitor != null ? MicMonitor.CurrentAudioWatcher : null, null,
        gameCam, FindToolsDir(),
        () => {
          try {
            return MicMonitor != null && MicMonitor.Gate != null
              && MicMonitor.Gate.State == MicSetupState.ReadyLocal;
          } catch (Exception) { return false; }
        },
        () => {
          try {
            if (mic != null && mic.LocalMic != null) {
              var cap = mic.LocalMic.Capability;
              if (cap.IsAvailable()) return mic.LocalMic.SelectedDevice ?? string.Empty;
            }
          } catch (Exception) { }
          return null;
        });
      GameObject recLocGo = new GameObject("RecordingLocationDialog");
      RecordingLocationDialog recLoc = recLocGo.AddComponent<RecordingLocationDialog>();
      rec.BindLocationDialog(recLoc);
      GameObject recConfirmGo = new GameObject("RecordingConfirmDialog");
      RecordingConfirmDialog recConfirm = recConfirmGo.AddComponent<RecordingConfirmDialog>();
      rec.BindConfirmDialog(recConfirm);
      GameObject recIndGo = new GameObject("RecordingIndicator");
      RecordingIndicator recInd = recIndGo.AddComponent<RecordingIndicator>();
      rec.BindIndicator(recInd);
      GameObject recToastGo = new GameObject("RecordingToast");
      RecordingToast recToast = recToastGo.AddComponent<RecordingToast>();
      rec.BindToast(recToast);
      // One face in the file (user rule): the recorder hides this box while
      // recording (the PiP carries the face). Null when the camera path
      // failed to wire — Bind is null-safe.
      try { rec.BindCameraHud(camHud); } catch (Exception) { }
      // Phase 2.5: voice+gameplay mix in exports (null-safe: mic-only legacy when absent).
      try { rec.BindGameAudioTap(builder.GameTap); } catch (Exception) { }
      MediaRecorder = rec;
      Debug.Log("[MediaRec] wired (explicit start only; F2 toggles, double-F2 changes save folder)");
    } catch (Exception e) {
      Debug.LogWarning("[MediaRec] wiring failed, game continues without recording: " + e.Message);
      MediaRecorder = null;
    }
    // Startup dependency setup (Phase 2.3c, additive): checks FFmpeg /
    // Python / LAN cert shortly after boot and offers one-click install.
    // All missing = silent when everything is present; declining never
    // blocks gameplay (fallbacks stay active). Any failure leaves
    // DependencySetup=null and the game runs exactly as before.
    // Hub-selection hall: skip the check entirely (the LAN-cert/FFmpeg/Python
    // modal is a dev flow — kids choosing a gate must never see it).
    if (!MarketBuilder.HubSelectionOnly) try {
      string appTools = null;
      try { appTools = System.IO.Path.Combine(Application.persistentDataPath, "Tools"); }
      catch (Exception) { }
      GameObject depDlgGo = new GameObject("DependencySetupDialog");
      DependencySetupDialog depDlg = depDlgGo.AddComponent<DependencySetupDialog>();
      GameObject depSvcGo = new GameObject("DependencySetupService");
      DependencySetupService depSvc = depSvcGo.AddComponent<DependencySetupService>();
      depSvc.Bind(FindToolsDir(), appTools, depDlg);
      DependencySetup = depSvc;
      // The recorder's transcoder also learns the app-local tools dir, so an
      // auto-installed portable FFmpeg is found without PATH changes.
      try {
        if (MediaRecorder != null) MediaRecorder.SetAppToolsDir(appTools);
      } catch (Exception) { }
      Debug.Log("[DepSetup] wired (startup check in background)");
    } catch (Exception e) {
      Debug.LogWarning("[DepSetup] wiring failed, game continues without setup check: " + e.Message);
      DependencySetup = null;
    }
    // S3B dev-truth (UI visibility forensics, batch-verifiable): screen pixels
    // vs capture size decides tooling-crop vs render-bug. Players never see it.
    try {
      Debug.Log("[Boot] screen=" + Screen.width + "x" + Screen.height
        + " hud=" + (_hud != null ? ("'" + _hud.CurrentObjective + "'") : "null"), this);
    } catch (System.Exception) { }
    // P1-1/P1-6 foundation wiring (additive, null-safe): offer the market
    // activity, push the shared gate into router + gates, adopt live w1 state
    // (fresh boot on a completed save lands Completed without a replayed beat).
    try {
      _marketLifecycle.MarkAvailable("bootstrap built");
      if (_quests != null) {
        QuestState a = _quests.GetState(W1QuestApple);
        QuestState b = _quests.GetState(W1QuestBall);
        if ((a != null && a.Completed) || (b != null && b.Completed)) {
          _questStarted = true;
          _marketLifecycle.AdoptCompleted("boot adopted");
        } else if ((a != null && a.ObjectiveIndex > 0) || (b != null && b.ObjectiveIndex > 0)) {
          _questStarted = true;
          _marketLifecycle.Begin("boot resumed");
        }
      }
    } catch (Exception) { }
    try { if (builder != null) builder.SetInteractionGate(_gate); } catch (Exception) { }
  }

  // <repo>/tools (phone_mic_gateway.py + lan certs) for the monitor's
  // in-game gateway auto-start. Null when not found (player builds without
  // the dev tools folder): the monitor falls back to manual instructions.
  // Tools folder resolution (user bug: every new build prompted again and
  // could not install because the standalone has no <build>/tools). Search
  // several layouts, then MIRROR into the stable per-user folder
  // (persistentDataPath/Tools) so later builds/machines stay silent.
  static string FindToolsDir() {
    try {
      string appTools = System.IO.Path.Combine(Application.persistentDataPath, "Tools");
      if (ToolsValid(appTools)) return appTools;
      string env = null;
      try { env = System.Environment.GetEnvironmentVariable("LWE_TOOLS"); } catch (Exception) { }
      string root = null;
      try { root = System.IO.Directory.GetParent(Application.dataPath).FullName; } catch (Exception) { }
      string[] candidates = {
        appTools,
        env,
        root != null ? System.IO.Path.Combine(root, "tools") : null,
        root != null ? System.IO.Path.Combine(root, "..", "tools") : null,
        root != null ? System.IO.Path.Combine(root, "..", "little-world-english", "tools") : null,
        root != null ? System.IO.Path.Combine(root, "..", "learning-world", "tools") : null,
      };
      foreach (string c in candidates) {
        if (!ToolsValid(c)) continue;
        if (!string.Equals(c, appTools, StringComparison.OrdinalIgnoreCase)) {
          MirrorTools(c, appTools);
          if (ToolsValid(appTools)) return appTools;
        }
        return c;
      }
    } catch (Exception) { }
    return null;
  }

  // A tools dir is usable when the phone gateway + LAN cert pieces are there.
  static bool ToolsValid(string dir) {
    try {
      if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir)) return false;
      bool gateway = System.IO.File.Exists(System.IO.Path.Combine(dir, "phone_mic_gateway.py"));
      bool cert = System.IO.File.Exists(System.IO.Path.Combine(dir, "lan.crt"))
        && System.IO.File.Exists(System.IO.Path.Combine(dir, "lan.key"));
      return gateway || cert;
    } catch (Exception) { return false; }
  }

  // One-time copy of the small tools folder into the stable user dir (certs +
  // gateway scripts + pages, ~100KB). Best effort: a failed mirror just keeps
  // the found path for this run.
  static void MirrorTools(string from, string to) {
    try {
      System.IO.Directory.CreateDirectory(to);
      foreach (string file in System.IO.Directory.GetFiles(from)) {
        try { System.IO.File.Copy(file, System.IO.Path.Combine(to, System.IO.Path.GetFileName(file)), true); }
        catch (Exception) { }
      }
    } catch (Exception) { }
  }

  // First talk: Milo opens the story. Same block that used to run at build;
  // quest rules, bubble, HUD and voice are untouched, only re-sequenced.
  // This is also Mia's introduction beat: her label appears together with
  // the question bubble over her stall, so the player can locate her.
  // Mic flow wiring (extracted for the Phase 2.5 identity gate: runs at Build
  // when gender is chosen, or right after the child's first gender pick).
  // Future listening exercises gate on MicMonitor.CheckBeforeListening(token).
  void WireMic(MicSetupBundle mic) {
    try {
      if (mic == null || mic.Gate == null) return;
      GameObject dialogGo = new GameObject("MicSetupDialog");
      MicSetupDialog dialog = dialogGo.AddComponent<MicSetupDialog>();
      GameObject monitorGo = new GameObject("MicSetupMonitor");
      MicSetupMonitor monitor = monitorGo.AddComponent<MicSetupMonitor>();
      monitor.Bind(mic.Gate, mic.LocalMic, mic.PhoneMic, dialog,
        mic.BridgeHost, mic.BridgePort, FindToolsDir());
      MicMonitor = monitor;
      // Corner status widget (measured signal bars / headphone + data dot).
      // Presentation-only: polls the monitor snapshot, never eats clicks.
      GameObject hudGo = new GameObject("MicStatusHud");
      MicStatusHud hud = hudGo.AddComponent<MicStatusHud>();
      hud.Bind(monitor);
    } catch (Exception) { }
  }

  void OnFirstTalk() {
    if (_bus == null || _quests == null) return;
    
    // Check if apple quest is completed - if so, start ball quest instead
    QuestState appleState = _quests.GetState(W1QuestApple);
    QuestId questToStart = (!appleState.Completed) ? W1QuestApple : W1QuestBall;
    
    _quests.StartQuest(questToStart);
    _bus.Publish(new StoryMomentEvent(StoryMoment.StoryIntro, DateTime.UtcNow));
    if (_builder != null && _builder.Bubble != null) _builder.Bubble.Show();
    if (_miaLabel != null) _miaLabel.Show();
    if (_hud != null) {
      string objective = (questToStart == W1QuestApple)
        ? DialogueLang.T("Find the apple", "T?m qu? táo")
        : DialogueLang.T("Find the ball", "T?m qu? bóng");
      _hud.ShowObjective(objective);
      _hud.SetReplayVisible(true);
    }
    // Answer step (find apple-vs-ball unaided): guide hides.
    if (_guide != null) _guide.SetStage(GuideStage.Hidden, null);
    Milo.SetInstructionTarget(0);
    // Attention guidance: Milo's line names Mia, so camera takes player to her stall front
    if (_builder != null && _builder.WorldCamera != null && _builder.MiaAnchor != null && _builder.MiloAnchor != null) {
      Vector3 midHead = (_builder.MiloAnchor.position + _builder.MiaAnchor.position) * 0.5f + new Vector3(0f, 1.2f, 0f);
      _builder.WorldCamera.FramePointFor(new Vector3(-0.6f, 2f, 1.6f), midHead, 2.5f);
    }
  }

  // Fires on every click to Milo. Handles quest progression: if the current
  // active quest is completed and there's a next quest, start it.
  void OnTalk() {
    if (_bus == null || _quests == null) return;
    if (_activeQuest == null) return;
    
    QuestState currentState = _quests.GetState(_activeQuest.Value);
    if (!currentState.Completed) return; // current quest not done yet
    
    // Determine next quest based on current active quest
    QuestId? nextQuest = null;
    if (_activeQuest.Value == W1QuestApple.Value) {
      nextQuest = W1QuestBall;
    }
    // Add more quest chains here as needed
    
    if (nextQuest.HasValue) {
      QuestState nextState = _quests.GetState(nextQuest.Value);
      if (!nextState.Completed) { // only start if not already completed
        _quests.StartQuest(nextQuest.Value);
        _bus.Publish(new StoryMomentEvent(StoryMoment.StoryIntro, DateTime.UtcNow));
        if (_builder != null && _builder.Bubble != null) _builder.Bubble.Show();
        if (_miaLabel != null) _miaLabel.Show();
        if (_hud != null) {
          string objective = (nextQuest.Value == W1QuestBall)
            ? DialogueLang.T("Find the ball", "T?m qu? bóng")
            : DialogueLang.T("Find the apple", "T?m qu? táo");
          _hud.ShowObjective(objective);
          _hud.SetReplayVisible(true);
        }
        // Next answer step: guide hides again.
        if (_guide != null) _guide.SetStage(GuideStage.Hidden, null);
        if (nextQuest.Value == W1QuestBall) {
          // 2F entry-driven narration: the ASK comes from manifest data
          // (mia voice per roster), not Milo's apple-worded instruction.
          SayQuestLine(BallAskText, MiaVoice(), AudioPriority.P2_Instruction);
        } else {
          Milo.SetInstructionTarget(0);
        }
        if (_builder != null && _builder.WorldCamera != null && _builder.MiaAnchor != null && _builder.MiloAnchor != null) {
          Vector3 midHead = (_builder.MiloAnchor.position + _builder.MiaAnchor.position) * 0.5f + new Vector3(0f, 1.2f, 0f);
          _builder.WorldCamera.FramePointFor(new Vector3(-0.6f, 2f, 1.6f), midHead, 2.5f);
        }
      }
    }
  }

  void OnWordSeen(WordSeenEvent e) {
    if (_bus == null || _quests == null) return;
    if (!_questStarted) return; // pre-talk find: silent (quest begins at Talk)
    if (_activeQuest == null) return;

    QuestState state = _quests.GetState(_activeQuest.Value);
    if (state.Completed) return; // post-completion clicks: no re-instruct

    if (e.WordId.Value == AppleWord.Value && _activeQuest.Value == W1QuestApple.Value) {
      Milo.PraiseFound();
      Milo.SetInstructionTarget(1);
      if (_hud != null) _hud.ShowObjective(DialogueLang.T("Bring the apple to Mia", "Mang táo cho cô Mia"));
      if (_guide != null) _guide.SetStage(GuideStage.ToMia, _miaT);
    } else if (e.WordId.Value == BallWord.Value && _activeQuest.Value == W1QuestBall.Value) {
      // 2F entry-driven praise: manifest correct-response line (Mia voice),
      // mirroring Milo.PraiseFound's role in the apple branch — word-free
      // generic praise would misname the target, manifest data names it.
      SayQuestLine(BallPraiseText, MiaVoice(), AudioPriority.P4_Feedback);
      if (_hud != null) _hud.ShowObjective(DialogueLang.T("Bring the ball to Mia", "Mang bóng cho cô Mia"));
      if (_guide != null) _guide.SetStage(GuideStage.ToMia, _miaT);
    }
  }

  static string MiaVoice() {
    NpcDefinition mia = NpcRoster.Get("mia");
    return mia != null && !string.IsNullOrEmpty(mia.voice) ? mia.voice : "mia_v1";
  }

  // 2F narration entry point: manifest text + roster voice through the frozen
  // 2D audio contract (rate/pitch 1.0, Clear — the exact params P08B pins for
  // L2 hits). Fire-and-forget: the Director owns failures as warnings; the
  // quest never blocks on voice. No new sentences live here beyond the two
  // manifest mirrors above (CT-P10 pins them to Content/).
  void SayQuestLine(string text, string voice, AudioPriority priority) {
    if (_audio == null || string.IsNullOrEmpty(text)) return;
    var req = new DialogueRequest(text, new VoiceProfileId(voice ?? ""),
      DialogueLang.Language, 1f, 1f, SpeechStyle.Clear,
      AudioFormat.Mp3_44100, priority);
    FireLine(req);
  }

  async void FireLine(DialogueRequest req) {
    if (_audio == null) return;
    try {
      await _audio.SpeakAsync(req);
    } catch (Exception) {
      // Director already logs; narration must never break quest flow.
    }
  }

  // Phase 3.0: objective cached before a world entry, restored on return —
  // quest text is never clobbered by world navigation.
  string _preWorldObjective;

  // Phase 3.0 world-transition presentation (additive; quest narration is
  // untouched). Enter: HUD names the new world + camera frames the entry
  // beat, then auto-returns to Follow (the child walked in, no teleport).
  // Return: the player is warped to the main-side road head (same GameObjects,
  // same services — nothing duplicated, nothing leaked), HUD restores the
  // exact pre-entry objective, camera re-anchors Follow on the player.
  void OnWorldChanged(WorldChangedEvent e) {
    if (_builder == null) return;
    // S3-P2L: the ENGLISH subject always teaches English — while it is active,
    // DialogueLang keeps every line in English even in Vietnamese mode.
    // (Forward hook: the English subject has no scene yet; every travel
    // already publishes this event.)
    DialogueLang.EnglishSubjectActive = e.To.Value == "english";
    SubjectDefinition to = SubjectCatalog.Get(e.To);
    if (to != null) {
      // Phase 3.0.x S2: scene-backed subjects (Math pilot) travel through the
      // shared loader; spatial siblings keep the legacy walk-in beat below.
      if (!string.IsNullOrEmpty(to.SceneName) && _worldTransition != null && _sceneOps != null) {
        TravelToSubjectAsync(to);
        return;
      }
      if (_hud != null) {
        // Cache discipline (chaos survey: cross-subject switches broke a
        // single-slot cache): only an entry FROM Main overwrites the cached
        // Main-world objective; subject-to-subject switches keep it.
        if (e.From == SubjectIds.Main) {
          try { _preWorldObjective = _hud.CurrentObjective; } catch (Exception) { }
        }
        // Vietnamese mode: the subject name alone ("Toán") — English mode keeps
        // the "<Name> World" label, byte-identical to before.
        _hud.ShowObjective(DialogueLang.T(to.DisplayName + " World", to.DisplayName));
      }
      if (_builder.WorldCamera != null) {
        Vector3 look = to.EntryPoint + new Vector3(0f, 1.0f, 0f);
        Vector3 outDir = to.GatePos - to.PlaygroundCenter;
        outDir.y = 0f;
        if (outDir.sqrMagnitude < 0.001f) outDir = new Vector3(0f, 0f, 1f);
        outDir.Normalize();
        // Three-quarter beat (P3 visual QA: an on-axis camera looks straight
        // through its own lintel and the obstruction pull-in parks it inside
        // the beam). Offset laterally so the arch + road + playground frame.
        Vector3 lateral = new Vector3(outDir.z, 0f, -outDir.x);
        Vector3 camPos = to.GatePos + outDir * 3.2f + lateral * 2.4f + new Vector3(0f, 2.6f, 0f);
        _builder.WorldCamera.FramePointFor(camPos, look, 2.0f);
      }
      return;
    }
    if (e.To == SubjectIds.Main) {
      // Phase 3.0.x S2: returning from a scene-backed subject unloads it and
      // restores Main; spatial returns keep the legacy warp below.
      if (!string.IsNullOrEmpty(_activeSubjectScene)) {
        ReturnFromSubjectAsync();
        return;
      }
      SubjectDefinition from = SubjectCatalog.Get(e.From);
      if (from != null && _builder.Player != null) {
        Vector3 outDir = from.GatePos - from.PlaygroundCenter;
        outDir.y = 0f;
        if (outDir.sqrMagnitude < 0.001f) outDir = new Vector3(0f, 0f, 1f);
        outDir.Normalize();
        try { _builder.Player.WarpTo(from.GatePos + outDir * 2.0f); } catch (Exception) { }
      }
      if (_hud != null) {
        if (!string.IsNullOrEmpty(_preWorldObjective)) _hud.ShowObjective(_preWorldObjective);
        else _hud.ShowObjective(DialogueLang.T("Look around!", "Nh?n quanh nhé!"));
      }
      if (_builder.WorldCamera != null && _builder.Player != null) {
        try { _builder.WorldCamera.Follow(_builder.Player.transform, MarketBuilder.FollowOffset(_builder.WorldCamera.defaultOffset)); }
        catch (Exception) { }
      }
    }
  }

  // Phase 3.0.x S2 scene travel (Math pilot). Fire-and-forget by design (bus
  // event handler): every failure path restores Main playability and reports
  // via HUD + log — never a stranded player, never a false InSubject.
  // Awaits resume on the main thread (no ConfigureAwait), so Unity API below
  // is safe; the machine itself never touches Unity objects.
  async void TravelToSubjectAsync(SubjectDefinition to) {
    if (_travelLock) return;
    _travelLock = true;
    // P1-6: world input freezes for the whole travel beat (router + gates
    // read the same gate — no scattered booleans). _travelLock stays as the
    // async reentrancy guard; the gate is the INPUT lock.
    try { _gate.BeginTransition("travel:" + to.Id.Value); } catch (Exception) { }
    try {
      if (_builder == null || _builder.Player == null || _worldTransition == null || _sceneOps == null) return;
      _mainReturnPos = _builder.Player.transform.position;
      // P3.0.1 journey fix (P1): remember the gate so the return warp can
      // land OUTSIDE its fire radius. Warping back onto the cached position
      // (often right on the gate mouth) left SubjectGate._wasInside latched
      // true from the first entry, so the gate could never re-fire and the
      // child could not re-enter Math without first walking away and back.
      _mainReturnGate = to.GatePos;
      Vector3 toHub = SubjectCatalog.HubCenter - to.GatePos;
      toHub.y = 0f;
      _mainReturnOut = toHub.sqrMagnitude > 0.001f ? toHub.normalized : new Vector3(0f, 0f, 1f);
      // Phase 3.0.x S3: cache the Main objective for the scene return path
      // (the spatial branch caches on entry; without this the unload restore
      // in ReturnFromSubjectCoreAsync falls back to "Look around!" and the hub
      // "Choose a gate!" line is lost).
      if (_hud != null) {
        try { _preWorldObjective = _hud.CurrentObjective; } catch (System.Exception) { }
      }
      try { _builder.Player.Stop(); } catch (System.Exception) { }
      if (_builder.Router != null) { try { _builder.Router.enabled = false; } catch (System.Exception) { } }
      if (_hud != null) { try { _hud.ShowObjective(DialogueLang.T("Entering ", "Ðang vào ") + to.DisplayName + "…"); } catch (System.Exception) { } }
      // B1R3 math tunnel (user round): bead rings + number/symbol glyphs rush
      // past while the world loads — the transition itself reads "Math".
      if (_hud != null) { try { _hud.PlayTunnel(); } catch (System.Exception) { } }
      await System.Threading.Tasks.Task.Delay(120);
      bool entered = false;
      try { entered = await _worldTransition.EnterAsync(_sceneOps, to.Id, to.SceneName); }
      catch (System.Exception e) { Debug.LogWarning("[MarketBootstrap] Enter failed: " + e.Message, this); }
      GameInstaller installer = null;
      try { installer = GetComponent<GameInstaller>(); } catch (System.Exception) { }
      Transform entry = installer != null ? installer.MathEntryPoint : null;
      if (!entered || entry == null) {
        // Truthful failure (S3A §8): dev-side reason in the log (machine
        // LastError), player-side objective restore (no fake progress, no
        // tech language — the child simply taps the gate again).
        try {
          string reason = _worldTransition != null ? _worldTransition.LastError : null;
          Debug.LogWarning("[MarketBootstrap] Enter " + to.SceneName + " failed"
            + (string.IsNullOrEmpty(reason) ? " (entry missing)." : ": " + reason), this);
        } catch (System.Exception) { }
        // Nothing switched yet: close the tunnel, clean up, restore HUD.
        if (_hud != null) { try { _hud.StopTunnel(); } catch (System.Exception) { } }
        await ReturnFromSubjectCoreAsync(false);
        if (_hud != null) {
          try {
            if (!string.IsNullOrEmpty(_preWorldObjective)) _hud.ShowObjective(_preWorldObjective);
            else _hud.ShowObjective(DialogueLang.T("Look around!", "Nh?n quanh nhé!"));
          } catch (System.Exception) { }
        }
        return;
      }
      _activeSubjectScene = to.SceneName;
      // S6 beauty pass: scene-backed worlds get their own air (soft far haze
      // so the sky, clouds and the pastel rainbow read — Main's 18-45m fog
      // washed the Math sky out). Restored symmetrically on return below.
      if (to.Id == SubjectIds.Math) {
        try { WorldBeauty.ApplyMathAtmosphere(); } catch (System.Exception) { }
      }
      DeactivateMainPresentation();
      if (_builder.Router != null) {
        try { _builder.Router.boundCenter = MathWorldBuilder.WorldOffset; } catch (System.Exception) { }
        // 3.0.2 district scale (38m ground): widen click bounds for Math,
        // restored to Main values on return below.
        try { _builder.Router.boundX = MathWorldBuilder.BoundX; } catch (System.Exception) { }
        try { _builder.Router.boundZ = MathWorldBuilder.BoundZ; } catch (System.Exception) { }
      }
      bool warped = false;
      try { warped = _builder.Player.WarpTo(entry.position); } catch (System.Exception) { }
      if (!warped) {
        try { Debug.LogWarning("[MarketBootstrap] Enter " + to.SceneName + " warp failed; staying Main.", this); }
        catch (System.Exception) { }
        if (_hud != null) { try { _hud.StopTunnel(); } catch (System.Exception) { } }
        await ReturnFromSubjectCoreAsync(false);
        if (_hud != null) {
          try {
            if (!string.IsNullOrEmpty(_preWorldObjective)) _hud.ShowObjective(_preWorldObjective);
            else _hud.ShowObjective(DialogueLang.T("Look around!", "Nh?n quanh nhé!"));
          } catch (System.Exception) { }
        }
        return;
      }
      if (_builder.WorldCamera != null) {
        // B1R3: Math spawns with a higher, wider follow framing (user round).
        Vector3 spawnOffset = (to.Id == SubjectIds.Math)
          ? MathWorldBuilder.FollowOffset : _builder.WorldCamera.defaultOffset;
        try { _builder.WorldCamera.Follow(_builder.Player.transform, spawnOffset); }
        catch (System.Exception) { }
      }
      // B1R3 arrival beat (user round): frame the world-name column first
      // ("this is the Math world"), then the camera returns to the character
      // by itself (FramePointFor auto-resumes Follow).
      // P1-2: anchor-posed when the world staged them, vector fallback otherwise.
      if (to.Id == SubjectIds.Math && _builder.WorldCamera != null) {
        try {
          bool anchored = false;
          try {
            GameInstaller gi = GetComponent<GameInstaller>();
            ActivityAnchors ma = gi != null ? gi.MathAnchors : null;
            if (ma != null && ma.Camera != null && ma.CameraLook != null) {
              _builder.WorldCamera.FrameAnchor(ma.Camera, ma.CameraLook, 2.2f);
              anchored = true;
            }
          } catch (Exception) { }
          if (!anchored) {
            Vector3 signW = MathWorldBuilder.SignWorldPos;
            Vector3 camPos = signW + new Vector3(-2.6f, 1.7f, 4.8f);
            _builder.WorldCamera.FramePointFor(camPos, signW + new Vector3(0f, 1.7f, 0f), 2.2f);
          }
        } catch (System.Exception) { }
      }
      if (_hud != null) { try { _hud.ShowObjective(DialogueLang.T(to.DisplayName + " World", to.DisplayName)); } catch (System.Exception) { } }
      if (_builder.Router != null) { try { _builder.Router.enabled = true; } catch (System.Exception) { } }
      // S3A §8: success is dev-verifiable in the log (states stay truthful end
      // to end: Loading HUD -> InSubject HUD + this line). Cover lifts AFTER
      // the warp + camera + HUD are all in place (never half-switched).
      try { Debug.Log("[MarketBootstrap] Entered " + to.SceneName + " (loader InSubject).", this); }
      catch (System.Exception) { }
      if (_hud != null) { try { _hud.StopTunnel(); } catch (System.Exception) { } }
    } catch (System.Exception e) {
      // Fail-safe: an unexpected throw must never strand the child inside a
      // tunnel (or a dead travel lock — finally below still runs).
      try { Debug.LogWarning("[MarketBootstrap] Travel crashed: " + e.Message, this); }
      catch (System.Exception) { }
      if (_hud != null) { try { _hud.StopTunnel(); } catch (System.Exception) { } }
    } finally {
      try { _gate.EndTransition("travel:" + (to != null ? to.Id.Value : "?")); } catch (Exception) { }
      _travelLock = false;
    }
  }

  async void ReturnFromSubjectAsync() {
    if (_travelLock) return;
    _travelLock = true;
    try { _gate.BeginTransition("return"); } catch (Exception) { }
    try {
      await ReturnFromSubjectCoreAsync(true);
    } finally {
      try { _gate.EndTransition("return"); } catch (Exception) { }
      _travelLock = false;
    }
  }

  // S3A transition cover driver (SceneBridge pattern ADAPTED: fade covers the
  // load/warp/unload beat, then lifts — time-based cover over a real awaited
  // op, never a fake progress bar). Main-thread note: these awaits resume on
  // Unity's SynchronizationContext (no ConfigureAwait), so the SetTransition-
  // Cover calls below are main-thread safe — the same pattern the existing
  // post-await WarpTo/camera calls already rely on. Never throws.
  async System.Threading.Tasks.Task FadeCoverAsync(float target, float seconds) {
    try {
      if (_hud == null) return;
      float from = 0f;
      try { from = _hud.TransitionCoverAlpha; } catch (System.Exception) { }
      int steps = 6;
      if (seconds <= 0f) {
        try { _hud.SetTransitionCover(target); } catch (System.Exception) { }
        return;
      }
      for (int i = 1; i <= steps; i++) {
        try { await System.Threading.Tasks.Task.Delay((int)(seconds * 1000f / steps)); }
        catch (System.Exception) { break; }
        try { _hud.SetTransitionCover(from + (target - from) * i / steps); }
        catch (System.Exception) { break; }
      }
      try { _hud.SetTransitionCover(target); } catch (System.Exception) { }
    } catch (System.Exception) { }
  }

  // Shared return core (called with the travel lock held): warp back, unload,
  // reactivate Main, restore camera/HUD/input. Idempotent pieces make it safe
  // from both the return-gate path and the enter-failure path. withFade covers
  // the switch beat (S3A); enter-failure callers pass false (nothing switched
  // yet — they only lift the entry cover).
  async System.Threading.Tasks.Task ReturnFromSubjectCoreAsync(bool withFade) {
    try {
      // B1R3: same math tunnel covers the way home (symmetric transition).
      if (withFade) {
        if (_hud != null) { try { _hud.PlayTunnel(); } catch (System.Exception) { } }
        await System.Threading.Tasks.Task.Delay(120);
      }
      if (_builder != null && _builder.Player != null) {
        try { _builder.Player.Stop(); } catch (System.Exception) { }
        // Journey fix: never land inside the entry gate's fire radius (that
        // latches SubjectGate._wasInside and blocks re-entry). If the cached
        // spot sits within 2.2m of the gate, land 3m hub-side instead.
        Vector3 target = _mainReturnPos;
        Vector3 flat = new Vector3(target.x, 0f, target.z);
        Vector3 gateFlat = new Vector3(_mainReturnGate.x, 0f, _mainReturnGate.z);
        if (Vector3.Distance(flat, gateFlat) < 2.2f) {
          Vector3 outDir = _mainReturnOut.sqrMagnitude > 0.001f
            ? _mainReturnOut : new Vector3(0f, 0f, 1f);
          target = _mainReturnGate + outDir * 3.0f;
        }
        bool back = false;
        try { back = _builder.Player.WarpTo(target); } catch (System.Exception) { }
        if (!back) { try { back = _builder.Player.WarpTo(MarketBuilder.PlayerSpawn); } catch (System.Exception) { } }
        if (!back) {
          try { Debug.LogWarning("[MarketBootstrap] Return warp failed; player stays.", this); }
          catch (System.Exception) { }
        } else {
          try { Debug.Log("[MarketBootstrap] Return warp -> " + target.ToString("F1"), this); }
          catch (System.Exception) { }
        }
      }
      if (_builder != null && _builder.Router != null) {
        try { _builder.Router.boundCenter = Vector3.zero; } catch (System.Exception) { }
        try { _builder.Router.boundX = MarketBuilder.BoundX; } catch (System.Exception) { }
        try { _builder.Router.boundZ = MarketBuilder.BoundZ; } catch (System.Exception) { }
      }
      if (_worldTransition != null && _sceneOps != null && !string.IsNullOrEmpty(_activeSubjectScene)) {
        try { await _worldTransition.ReturnAsync(_sceneOps, _activeSubjectScene); }
        catch (System.Exception e) { Debug.LogWarning("[MarketBootstrap] Unload failed: " + e.Message, this); }
        try {
          string reason = _worldTransition != null ? _worldTransition.LastError : null;
          if (!string.IsNullOrEmpty(reason)) Debug.LogWarning("[MarketBootstrap] Return unload issue: " + reason, this);
          else Debug.Log("[MarketBootstrap] Returned to Main (loader Idle).", this);
        } catch (System.Exception) { }
      }
      _activeSubjectScene = null;
      ReactivateMainPresentation();
      // S6 beauty pass: back to the crisp Main air (symmetric with the enter
      // swap; harmless on the enter-failure path — it just re-applies Main).
      try { WorldBeauty.ApplyMainAtmosphere(); } catch (System.Exception) { }
      if (_hud != null) {
        try {
          if (!string.IsNullOrEmpty(_preWorldObjective)) _hud.ShowObjective(_preWorldObjective);
          else _hud.ShowObjective(DialogueLang.T("Look around!", "Nh?n quanh nhé!"));
        } catch (System.Exception) { }
      }
      if (_builder != null && _builder.WorldCamera != null && _builder.Player != null) {
        try { _builder.WorldCamera.Follow(_builder.Player.transform, MarketBuilder.FollowOffset(_builder.WorldCamera.defaultOffset)); }
        catch (System.Exception) { }
      }
      if (_builder != null && _builder.Router != null) {
        try { _builder.Router.enabled = true; } catch (System.Exception) { }
      }
      // B1R3: close the tunnel AFTER the world is switched back and
      // HUD/camera restored (the child never sees half-restored state).
      if (withFade && _hud != null) { try { _hud.StopTunnel(); } catch (System.Exception) { } }
    } catch (System.Exception e) {
      try { Debug.LogWarning("[MarketBootstrap] Return cleanup failed: " + e.Message, this); }
      catch (System.Exception) { }
      // Fail-safe: never strand the child inside the tunnel.
      if (_hud != null) { try { _hud.StopTunnel(); } catch (System.Exception) { } }
    }
  }

  void DeactivateMainPresentation() {
    try { if (_builder != null) _builder.gameObject.SetActive(false); } catch (System.Exception) { }
    try { if (_miloGo != null) _miloGo.SetActive(false); } catch (System.Exception) { }
    try { if (_miaGo != null) _miaGo.SetActive(false); } catch (System.Exception) { }
    try { if (_miloLabel != null) _miloLabel.Hide(); } catch (System.Exception) { }
    try { if (_miaLabel != null) _miaLabel.Hide(); } catch (System.Exception) { }
    try { if (_guide != null) _guide.gameObject.SetActive(false); } catch (System.Exception) { }
  }

  void ReactivateMainPresentation() {
    try { if (_builder != null) _builder.gameObject.SetActive(true); } catch (System.Exception) { }
    try { if (_miloGo != null) _miloGo.SetActive(true); } catch (System.Exception) { }
    try { if (_miaGo != null) _miaGo.SetActive(true); } catch (System.Exception) { }
    try { if (_miloLabel != null) _miloLabel.Show(); } catch (System.Exception) { }
    try { if (_miaLabel != null) _miaLabel.Show(); } catch (System.Exception) { }
    try { if (_guide != null) _guide.gameObject.SetActive(true); } catch (System.Exception) { }
  }

  void OnQuestStartedFlag(QuestStartedEvent e) {
    if (e.QuestId.Value == W1QuestApple.Value || e.QuestId.Value == W1QuestBall.Value) {
      _questStarted = true;
      _activeQuest = e.QuestId;
      // P1-1: (re)offer then activate — a next quest after a completion goes
      // Completed -> Available -> Active through the same contract.
      try {
        _marketLifecycle.MarkAvailable("quest started");
        _marketLifecycle.Begin("quest started");
      } catch (Exception) { }
      // Update bubble icon based on which quest started
      if (_builder != null && _builder.Bubble != null) {
        if (e.QuestId.Value == W1QuestApple.Value) {
          _builder.Bubble.SetIcon(new WordId("apple"));
        } else if (e.QuestId.Value == W1QuestBall.Value) {
          _builder.Bubble.SetIcon(new WordId("ball"));
        }
      }
    }
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    if (e.QuestId.Value == W1QuestApple.Value || e.QuestId.Value == W1QuestBall.Value) {
      // P1-1: completion beat state (input stays accepted; the beat is short).
      try { _marketLifecycle.MarkCompleted("quest completed"); } catch (Exception) { }
    }
    if (e.QuestId.Value == W1QuestApple.Value) {
      Milo.Celebrate();
      if (_builder != null && _builder.Bubble != null) _builder.Bubble.Hide();
      // Quest closed: guide returns to Milo (next talk / replay invitation).
      if (_guide != null) _guide.SetStage(GuideStage.ToMilo, _miloT);
      if (_bus != null) _bus.Publish(new StoryMomentEvent(StoryMoment.QuestComplete, DateTime.UtcNow));
      if (_builder != null && _builder.WorldCamera != null && _builder.MiaAnchor != null) {
        Vector3 miaPlayerMid = _builder.MiaAnchor.position + new Vector3(0.1f, 1.0f, 0.35f);
        _builder.WorldCamera.FramePointFor(new Vector3(-1.3f, 2.3f, 0.6f), miaPlayerMid, 3.2f);
      }
    } else if (e.QuestId.Value == W1QuestBall.Value) {
      Milo.Celebrate();
      if (_builder != null && _builder.Bubble != null) _builder.Bubble.Hide();
      if (_guide != null) _guide.SetStage(GuideStage.ToMilo, _miloT);
      if (_bus != null) _bus.Publish(new StoryMomentEvent(StoryMoment.QuestComplete, DateTime.UtcNow));
      if (_builder != null && _builder.WorldCamera != null && _builder.MiaAnchor != null) {
        Vector3 miaPlayerMid = _builder.MiaAnchor.position + new Vector3(0.1f, 1.0f, 0.35f);
        _builder.WorldCamera.FramePointFor(new Vector3(-1.3f, 2.3f, 0.6f), miaPlayerMid, 3.2f);
      }
    }
  }

  // Wrong choices get a closer framing on Mia's sad reaction (face must fill
  // enough frame to read at gameplay distance), then the camera returns by
  // itself. Retry context is fully preserved. Authored stall-front pose
  // (Phase-1 closure): the generic FocusOnFor kept its arrival view direction
  // and parked inside the apple/ball on some approaches. Round-B: moved
  // closer (2.3m vs 3.7m) so the sad frown reads; still south-front, below
  // the awning, outside the stall carve.
  void OnStoryMoment(StoryMomentEvent e) {
    if (e.Moment == StoryMoment.WrongChoice) FrameMiaFront(2.4f);
  }

  void FrameMiaFront(float seconds) {
    if (_builder == null || _builder.WorldCamera == null || _builder.MiaAnchor == null) return;
    Vector3 miaHead = _builder.MiaAnchor.position + new Vector3(0f, 1.2f, 0f);
    _builder.WorldCamera.FramePointFor(new Vector3(-2.2f, 1.6f, -0.6f), miaHead, seconds);
  }

  void FocusMia(float distance, float seconds) {
    if (_builder == null || _builder.WorldCamera == null || _builder.MiaAnchor == null) return;
    // Aim at head height (player-experience audit): aiming at the anchor feet
    // framed the counter, not the face.
    Vector3 miaHead = _builder.MiaAnchor.position + new Vector3(0f, 1.2f, 0f);
    _builder.WorldCamera.FocusOnFor(miaHead, distance, seconds);
  }
}
