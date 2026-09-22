// _Bootstrap/MathQuestDirector.cs — Lead owns. Phase 3.0.x S3.
// Math-world quest flow wiring (mirrors the MarketBootstrap W1 flow, scoped to
// the math_counting pilot): first talk starts the quest, WordSeen narrates the
// find, QuestCompleted celebrates. Quest RULES stay in QuestManager/HintService;
// Tess voice lines stay in Tess; this class only connects them in order.
//
// Why a separate director (not MarketBootstrap branches): the Math scene loads
// additively and unloads on return — this GO lives UNDER the MathWorld root so
// wiring dies with the scene (OnDisable disposes bus subs). Bootstrap stays
// Main-scoped; its global WordSeen->AdvanceOnSeen lambda keeps advancing the
// math find while Math is active (no new advancement path here — narration
// only, so double-advance is impossible by construction).
// Like Bootstrap: NO StoryMoment publishes (Mia-anchor framing would target a
// hidden anchor), HUD-only + voice feedback. C# 9.0 only.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class MathQuestDirector : MonoBehaviour {
  static readonly QuestId MathQuest = new QuestId("math_counting");
  static readonly WordId OneWord = new WordId("one");

  IGameEventBus _bus;
  IQuestService _quests;
  MathHostPresenter _host;
  MarketHUD _hud;

  bool _questStarted;
  QuestId? _activeQuest;
  IDisposable _subSeen;
  IDisposable _subStarted;
  IDisposable _subCompleted;
  bool _built;
  // P1-1: this activity's lifecycle (owner = this director, sole writer).
  // Talk-gated: Available -> Active on first talk (Begin shortcut, no staged
  // Ready beat); Active -> Completed on QuestCompletedEvent.
  readonly ActivityLifecycle _lifecycle = new ActivityLifecycle("math_counting", "MathQuestDirector");

  public ActivityLifecycle Lifecycle {
    get { return _lifecycle; }
  }

  // Called ONCE by GameInstaller after the Math scene builds. Null-safe:
  // missing wiring degrades to a silent world (quest never starts), never NREs.
  public void Build(IGameEventBus bus, IQuestService quests, MathHostPresenter host, MarketHUD hud) {
    if (_built) return;
    _built = true;
    _bus = bus;
    _quests = quests;
    _host = host;
    _hud = hud;
    if (bus == null || quests == null || host == null) {
      Debug.LogWarning("[MathQuestDirector] Build with null dependencies; math quest stays dormant.", this);
      return;
    }
    host.OnFirstTalk = OnFirstTalk;
    host.OnTalk = OnTalk;
    try {
      _subSeen = bus.Subscribe<WordSeenEvent>(OnWordSeen);
      _subStarted = bus.Subscribe<QuestStartedEvent>(OnQuestStartedFlag);
      _subCompleted = bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted);
    } catch (Exception) { }
    // P1-1/P1-3: offer the activity, then adopt live state (re-entry on a
    // completed quest lands directly in Completed — no replayed beat).
    _lifecycle.MarkAvailable("director built");
    try {
      QuestState live = quests.GetState(MathQuest);
      if (live != null && live.Completed) {
        _questStarted = true;
        _activeQuest = MathQuest;
        _lifecycle.AdoptCompleted("re-entry adopted");
        ShowObjective("Math World");
      } else if (live != null && live.ObjectiveIndex > 0) {
        _questStarted = true;
        _activeQuest = MathQuest;
        _lifecycle.Begin("re-entry resumed");
      }
    } catch (Exception) { }
  }

  void OnFirstTalk() {
    if (_bus == null || _quests == null) return;
    QuestState state = _quests.GetState(MathQuest);
    if (state.Completed) {
      ShowObjective("Math World");
      return;
    }
    if (state.ObjectiveIndex > 0 || _questStarted) {
      ResumeNarration(state); // re-entry mid-quest: never restart (no progress wipe)
      _lifecycle.Begin("talk resumed");
      return;
    }
    _quests.StartQuest(MathQuest);
    _lifecycle.Begin("first talk");
    ShowObjective("Find the one");
    Tess.SayFind();
    try { Debug.Log("[MathQuest] started math_counting (HUD: Find the one).", this); }
    catch (Exception) { }
  }

  // Fires on every host click after the first: repeat the current instruction
  // (completed quest: silent nod already happened in the presenter).
  void OnTalk() {
    if (_bus == null || _quests == null) return;
    QuestState state = _quests.GetState(MathQuest);
    if (state.Completed) return;
    if (!_questStarted && state.ObjectiveIndex <= 0) return; // FirstTalk owns the opening
    ResumeNarration(state);
  }

  void ResumeNarration(QuestState state) {
    if (state.ObjectiveIndex >= 1) {
      ShowObjective("Bring it to Tess");
      Tess.SayBring();
    } else {
      ShowObjective("Find the one");
      Tess.SayFind();
    }
  }

  void OnWordSeen(WordSeenEvent e) {
    if (_bus == null || _quests == null) return;
    if (!_questStarted) return; // pre-talk taps: silent (quest begins at Talk)
    if (_activeQuest == null) return;
    if (e.WordId.Value != OneWord.Value) return;
    if (_activeQuest.Value.Value != MathQuest.Value) return;
    QuestState state = _quests.GetState(_activeQuest.Value);
    if (state.Completed) return;
    if (state.ObjectiveIndex < 1) return; // find not yet advanced: stay silent
    Tess.PraiseFound();
    ShowObjective("Bring it to Tess");
    try { Debug.Log("[MathQuest] find done (HUD: Bring it to Tess).", this); }
    catch (Exception) { }
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    if (e.QuestId.Value != MathQuest.Value) return;
    _lifecycle.MarkCompleted("quest completed");
    Tess.Celebrate();
    ShowObjective("Math World");
    try { Debug.Log("[MathQuest] completed math_counting (HUD: Math World).", this); }
    catch (Exception) { }
  }

  void OnQuestStartedFlag(QuestStartedEvent e) {
    if (e.QuestId.Value != MathQuest.Value) return;
    _questStarted = true;
    _activeQuest = e.QuestId;
  }

  void ShowObjective(string text) {
    if (_hud == null) return;
    try { _hud.ShowObjective(text); } catch (Exception) { }
  }

  void OnDisable() {
    try { if (_subSeen != null) _subSeen.Dispose(); } catch (Exception) { }
    try { if (_subStarted != null) _subStarted.Dispose(); } catch (Exception) { }
    try { if (_subCompleted != null) _subCompleted.Dispose(); } catch (Exception) { }
    _subSeen = null;
    _subStarted = null;
    _subCompleted = null;
  }
}
