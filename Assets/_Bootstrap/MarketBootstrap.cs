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
  static readonly QuestId W1Quest = new QuestId("w1_mia_apple");
  static readonly WordId AppleWord = new WordId("apple");

  IGameEventBus _bus;
  IQuestService _quests;
  MarketHUD _hud;
  MarketBuilder _builder;
  WorldNameLabel _miloLabel;
  WorldNameLabel _miaLabel;
  bool _built;

  // Called ONCE by GameInstaller after MarketScene is loaded. All services are
  // constructed; the MarketBuilder (A) has built the world in its Awake.
  public void Build(IGameEventBus bus, IQuestService quests, IHintService hints, MarketBuilder builder) {
    if (_built) return;
    _built = true;
    _bus = bus;
    _quests = quests;
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

    var miaGo = new GameObject("Mia");
    MiaPresenter miaPresenter = miaGo.AddComponent<MiaPresenter>();
    miaPresenter.PlayerTarget = builder.Player != null ? builder.Player.transform : null;
    miaPresenter.Bind(bus, quests, hints);

    // In-world identity (reusable for future chapters): Milo is named from
    // frame one (he is the first action target); Mia stays unlabeled until
    // the story introduces her, so frame one never splits attention.
    // Labels live on CHILD objects: WorldNameLabel drives its own world
    // position every frame, so it must never sit on the NPC root itself
    // (it would fight the presenter's transform).
    GameObject miloLabelGo = new GameObject("MiloLabel");
    miloLabelGo.transform.SetParent(miloGo.transform, false);
    _miloLabel = miloLabelGo.AddComponent<WorldNameLabel>();
    // Player-experience audit 2026-09-12 (W1Audit m-label/g-mia/k-correct):
    // 2.05m sits inside Milo's hard-hat ridge / Mia's hair volume, so level
    // cameras see the pill with the text occluded. 2.35m clears all headwear
    // (Milo 1.65 + hat ~0.3, Mia 1.65 + hair ~0.3, pill half-height 0.19).
    _miloLabel.Setup("Milo", miloGo.transform, 2.35f);
    _miloLabel.Show();
    GameObject miaLabelGo = new GameObject("MiaLabel");
    miaLabelGo.transform.SetParent(miaGo.transform, false);
    _miaLabel = miaLabelGo.AddComponent<WorldNameLabel>();
    _miaLabel.Setup("Mia", miaGo.transform, 2.35f);
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

    // Opening is talk-gated (first-time readability): the HUD names the one
    // action ("Talk to Milo"); Milo's proximity greet + name label do the
    // inviting. The quest (bubble, instruction voice, HUD action line) starts
    // when the player actually talks to him — never before.
    miloPresenter.OnFirstTalk = OnFirstTalk;
    if (builder.Bubble != null) builder.Bubble.Hide();
    if (_hud != null) _hud.ShowObjective("Talk to Milo");
  }

  // First talk: Milo opens the story. Same block that used to run at build;
  // quest rules, bubble, HUD and voice are untouched, only re-sequenced.
  // This is also Mia's introduction beat: her label appears together with
  // the question bubble over her stall, so the player can locate her.
  void OnFirstTalk() {
    if (_bus == null || _quests == null) return;
    _quests.StartQuest(W1Quest);
    _bus.Publish(new StoryMomentEvent(StoryMoment.StoryIntro, DateTime.UtcNow));
    if (_builder != null && _builder.Bubble != null) _builder.Bubble.Show();
    if (_miaLabel != null) _miaLabel.Show();
    if (_hud != null) {
      _hud.ShowObjective("Find the apple");
      _hud.SetReplayVisible(true); // first spoken line exists from here on
    }
    Milo.SetInstructionTarget(0);
    // Attention guidance (player-experience audit): Milo's line names Mia, so
    // the camera takes the player to her stall front once, then auto-returns.
    // An authored pose (never through her awning) wins over the generic
    // quest-start sweep (same frame, Interaction mode).
    if (_builder != null && _builder.WorldCamera != null && _builder.MiaAnchor != null) {
      Vector3 miaHead = _builder.MiaAnchor.position + new Vector3(0f, 1.2f, 0f);
      _builder.WorldCamera.FramePointFor(new Vector3(-1.2f, 1.9f, 0.8f), miaHead, 2.5f);
    }
  }

  void OnWordSeen(WordSeenEvent e) {
    if (_bus == null || _quests == null) return;
    if (e.WordId.Value != AppleWord.Value) return;
    if (_quests.GetState(W1Quest).Completed) return; // post-completion clicks: no re-instruct
    Milo.PraiseFound();
    Milo.SetInstructionTarget(1);
    if (_hud != null) _hud.ShowObjective("Bring the apple to Mia");
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    if (e.QuestId.Value != W1Quest.Value) return;
    Milo.Celebrate();
    if (_builder != null && _builder.Bubble != null) _builder.Bubble.Hide();
    if (_bus != null) _bus.Publish(new StoryMomentEvent(StoryMoment.QuestComplete, DateTime.UtcNow));
    // Celebration framing (player-experience audit): the generic pull-back
    // landed inside the awning twice. Authored stall-front pose, auto-returns.
    if (_builder != null && _builder.WorldCamera != null && _builder.MiaAnchor != null) {
      Vector3 miaHead = _builder.MiaAnchor.position + new Vector3(0f, 1.2f, 0f);
      _builder.WorldCamera.FramePointFor(new Vector3(-0.8f, 2f, 1.2f), miaHead, 3.2f);
    }
  }

  // Wrong choices get a closer framing on Mia's sad reaction (face must fill
  // enough frame to read at gameplay distance), then the camera returns by
  // itself. Retry context is fully preserved.
  void OnStoryMoment(StoryMomentEvent e) {
    if (e.Moment == StoryMoment.WrongChoice) FocusMia(2.6f, 2.4f);
  }

  void FocusMia(float distance, float seconds) {
    if (_builder == null || _builder.WorldCamera == null || _builder.MiaAnchor == null) return;
    // Aim at head height (player-experience audit): aiming at the anchor feet
    // framed the counter, not the face.
    Vector3 miaHead = _builder.MiaAnchor.position + new Vector3(0f, 1.2f, 0f);
    _builder.WorldCamera.FocusOnFor(miaHead, distance, seconds);
  }
}
