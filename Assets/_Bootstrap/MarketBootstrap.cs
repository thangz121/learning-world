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
  bool _built;
  // Track which quest is active (only one at a time in W1)
  QuestId? _activeQuest;
  // R7: pre-talk finds must not narrate. Set by QuestStartedEvent.
  bool _questStarted;

  // 2F ball-quest dialogue (data-driven, NOT new literals): exact manifest
  // texts, pinned to Content/dialogues/manifest.json by CT-P10 (code mirrors
  // content until runtime catalog loading lands; validators own the truth).
  // Call params match the 2D L2 contract exactly (rate/pitch 1.0, Clear).
  const string BallAskText = "Ball please!"; // manifest inst_07
  const string BallPraiseText = "Great! Ball!"; // manifest ok_05

  // Called ONCE by GameInstaller after MarketScene is loaded. All services are
  // constructed; the MarketBuilder (A) has built the world in its Awake.
  // mic is optional (null = mic-setup gate off; all existing flows untouched).
  public MicSetupMonitor MicMonitor { get; private set; }
  public void Build(IGameEventBus bus, IQuestService quests, IHintService hints, MarketBuilder builder, IAudioDirector audio = null, MicSetupBundle mic = null) {
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

// B presenters (Unity instantiates via AddComponent; Bind injects services).
    var miloGo = new GameObject("Milo");
    MiloPresenter miloPresenter = miloGo.AddComponent<MiloPresenter>();
    miloPresenter.PlayerTarget = builder.Player != null ? builder.Player.transform : null;
    miloPresenter.Bind(bus, quests, hints);
    miloPresenter.OnFirstTalk = OnFirstTalk;
    miloPresenter.OnTalk = OnTalk;

    var miaGo = new GameObject("Mia");
    MiaPresenter miaPresenter = miaGo.AddComponent<MiaPresenter>();
    miaPresenter.PlayerTarget = builder.Player != null ? builder.Player.transform : null;
    miaPresenter.Bind(bus, quests, hints);

    // Shop-counter click proxy (Phase-1 closure): tapping Mia's counter reaches
    // Mia herself, so players never need pixel taps on her body behind the
    // counter/awning. Reusable ClickForwarder; quest reactions stay in Mia.
    if (builder.StallCounter != null) {
      ClickForwarder counterFwd = builder.StallCounter.GetComponent<ClickForwarder>();
      if (counterFwd == null) counterFwd = builder.StallCounter.AddComponent<ClickForwarder>();
      counterFwd.Bind(miaPresenter);
    }

    // In-world identity (reusable for future chapters): Milo is named from
    // frame one (he is the first action target); Mia stays unlabeled until
    // the story introduces her, so frame one never splits attention.
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
    miloLabelGo.transform.SetParent(miloGo.transform, false);
    _miloLabel = miloLabelGo.AddComponent<WorldNameLabel>();
    // Player-experience audit 2026-09-12 (W1Audit m-label/g-mia/k-correct):
    // 2.05m sits inside Milo's hard-hat ridge / Mia's hair volume, so level
    // cameras see the pill with the text occluded. 2.35m clears all headwear
    // (Milo 1.65 + hat ~0.3, Mia 1.65 + hair ~0.3, pill half-height 0.19).
    _miloLabel.Setup(miloName, miloGo.transform, miloHeight);
    _miloLabel.Show();
    GameObject miaLabelGo = new GameObject("MiaLabel");
    miaLabelGo.transform.SetParent(miaGo.transform, false);
    _miaLabel = miaLabelGo.AddComponent<WorldNameLabel>();
    _miaLabel.Setup(miaName, miaGo.transform, miaHeight);
    _miaLabel.Hide();

    // HUD: objective text + replay delegates to Milo (no World->Brain reference).
    _hud = builder.Hud;
    if (_hud != null) _hud.OnReplayPressed = Milo.RepeatInstruction;

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

    // Opening is talk-gated (first-time readability): the HUD names the one
    // action ("Talk to Milo"); Milo's proximity greet + name label do the
    // inviting. The quest (bubble, instruction voice, HUD action line) starts
    // when the player actually talks to him — never before.
    miloPresenter.OnFirstTalk = OnFirstTalk;
    if (builder.Bubble != null) builder.Bubble.Hide();
    if (_hud != null) _hud.ShowObjective("Talk to Milo");

    // Mic-setup gate (Phase 2.1, additive): startup offer when no mic, silent
    // background rechecks, exercise-entry re-prompt. The monitor's Start()
    // runs the startup check on the next frame; future listening exercises
    // gate on MicMonitor.CheckBeforeListening(token) (quest #3 wiring).
    if (mic != null && mic.Gate != null) {
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
    }
  }

  // <repo>/tools (phone_mic_gateway.py + lan certs) for the monitor's
  // in-game gateway auto-start. Null when not found (player builds without
  // the dev tools folder): the monitor falls back to manual instructions.
  static string FindToolsDir() {
    try {
      string root = System.IO.Directory.GetParent(Application.dataPath).FullName;
      string td = System.IO.Path.Combine(root, "tools");
      if (System.IO.Directory.Exists(td)) return td;
    } catch (Exception) { }
    return null;
  }

  // First talk: Milo opens the story. Same block that used to run at build;
  // quest rules, bubble, HUD and voice are untouched, only re-sequenced.
  // This is also Mia's introduction beat: her label appears together with
  // the question bubble over her stall, so the player can locate her.
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
      string objective = (questToStart == W1QuestApple) ? "Find the apple" : "Find the ball";
      _hud.ShowObjective(objective);
      _hud.SetReplayVisible(true);
    }
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
          string objective = (nextQuest.Value == W1QuestBall) ? "Find the ball" : "Find the apple";
          _hud.ShowObjective(objective);
          _hud.SetReplayVisible(true);
        }
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
      if (_hud != null) _hud.ShowObjective("Bring the apple to Mia");
    } else if (e.WordId.Value == BallWord.Value && _activeQuest.Value == W1QuestBall.Value) {
      // 2F entry-driven praise: manifest correct-response line (Mia voice),
      // mirroring Milo.PraiseFound's role in the apple branch — word-free
      // generic praise would misname the target, manifest data names it.
      SayQuestLine(BallPraiseText, MiaVoice(), AudioPriority.P4_Feedback);
      if (_hud != null) _hud.ShowObjective("Bring the ball to Mia");
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
      new LanguageCode("en-US"), 1f, 1f, SpeechStyle.Clear,
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

  void OnQuestStartedFlag(QuestStartedEvent e) {
    if (e.QuestId.Value == W1QuestApple.Value || e.QuestId.Value == W1QuestBall.Value) {
      _questStarted = true;
      _activeQuest = e.QuestId;
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
    if (e.QuestId.Value == W1QuestApple.Value) {
      Milo.Celebrate();
      if (_builder != null && _builder.Bubble != null) _builder.Bubble.Hide();
      if (_bus != null) _bus.Publish(new StoryMomentEvent(StoryMoment.QuestComplete, DateTime.UtcNow));
      if (_builder != null && _builder.WorldCamera != null && _builder.MiaAnchor != null) {
        Vector3 miaPlayerMid = _builder.MiaAnchor.position + new Vector3(0.1f, 1.0f, 0.35f);
        _builder.WorldCamera.FramePointFor(new Vector3(-1.3f, 2.3f, 0.6f), miaPlayerMid, 3.2f);
      }
    } else if (e.QuestId.Value == W1QuestBall.Value) {
      Milo.Celebrate();
      if (_builder != null && _builder.Bubble != null) _builder.Bubble.Hide();
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
