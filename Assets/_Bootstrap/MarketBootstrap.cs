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

    // B presenters (Unity instantiates via AddComponent; Bind injects services).
    var miloGo = new GameObject("Milo");
    MiloPresenter miloPresenter = miloGo.AddComponent<MiloPresenter>();
    miloPresenter.PlayerTarget = builder.Player != null ? builder.Player.transform : null;
    miloPresenter.Bind(bus, quests, hints);

    var miaGo = new GameObject("Mia");
    MiaPresenter miaPresenter = miaGo.AddComponent<MiaPresenter>();
    miaPresenter.PlayerTarget = builder.Player != null ? builder.Player.transform : null;
    miaPresenter.Bind(bus, quests, hints);

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

    // Slice opening: quest start FIRST (its QuestStartedEvent drives the HUD's
    // generic line), then the explicit W1 instruction wins; greeting and first
    // instruction follow (same-priority P3 lines queue FIFO in the Director).
    quests.StartQuest(W1Quest);
    if (_hud != null) _hud.ShowObjective("Find the apple!");
    Milo.Greet();
    Milo.SetInstructionTarget(0);
  }

  void OnWordSeen(WordSeenEvent e) {
    if (_bus == null || _quests == null) return;
    if (e.WordId.Value != AppleWord.Value) return;
    if (_quests.GetState(W1Quest).Completed) return; // post-completion clicks: no re-instruct
    Milo.PraiseFound();
    Milo.SetInstructionTarget(1);
    if (_hud != null) _hud.ShowObjective("Bring it to Mia!");
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    if (e.QuestId.Value != W1Quest.Value) return;
    Milo.Celebrate();
  }
}
