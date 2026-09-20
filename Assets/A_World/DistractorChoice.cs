// A_World/DistractorChoice.cs — Agent A (World & Visual). Reusable wrong-choice
// prop: a plausible-but-incorrect selectable object (W1: blue ball next to the
// apple crate). Implements SharedKernel IClickTarget so the existing router
// arrival flow works with ZERO router changes; the IClickTarget path plays NO
// vocab audio, so distractors need no Content/audio entries (counts frozen).
//
// R9 (player report: distractors must be PICKABLE to raise difficulty).
// New rule — pickup is neutral, the BRING decides:
//   - In-quest click, hands empty  -> PICK UP: the ball hides, a mini ball
//     rides the player hand, WordSeen(ball) arms Mia's wrong-carry context.
//     (WordSeen(ball) is proven safe: Milo only resets a clock, QuestManager
//     advances nothing, Bootstrap/ApplePresenter filter on apple.)
//   - Carry the ball to Mia (click OR proximity) -> WRONG: Mia reports wrong +
//     WrongChoice moment, the ball hops home (retry preserved, never a lock).
//   - In-quest click while carrying the APPLE -> legacy instant-wrong (hands
//     are full of the quest item; tapping the wrong prop is a real mistake).
//   - Apple seen while carrying the ball -> SWAP: ball hops home, apple carry
//     proceeds (proximity discovery near the crate auto-swaps: forgiving).
//   - Pre-quest clicks stay legacy instant-wrong (R7 lesson: no pre-talk state
//     changes); post-quest clicks stay inert. The prop itself stays.
// QuestId arrives as a plain string field (inspector/content configurable).
// C# 9.0 only. No Brain references (World->Brain contact is bus-only).
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DistractorChoice : MonoBehaviour, IClickTarget {
  [Tooltip("Quest this distractor counts against (raw string, parsed at boundary).")]
  public string questIdValue = "w1_mia_apple";

  // Word tracked for the swap rule (Mia arms apple-carry on this same event).
  const string AppleWord = "apple";
  readonly WordId _ballWord = new WordId("ball");

  IGameEventBus _bus;
  IHintService _hints;
  IQuestService _quests;
  readonly List<IDisposable> _subs = new List<IDisposable>();

  QuestId _activeQuest;
  bool _questStarted;
  bool _carryingBall;
  bool _appleCarried;

  Transform _hand;
  GameObject _carriedBall;

  float _squashT;
  Vector3 _baseScale;

  void Awake() {
    _baseScale = transform.localScale;
    _activeQuest = new QuestId(questIdValue);
  }

  // Injection boundary (MarketBuilder wires services; never newed here).
  public void Bind(IGameEventBus bus, IHintService hints, IQuestService quests) {
    ClearSubs();
    _bus = bus;
    _hints = hints;
    _quests = quests;
    if (_bus == null) return;
    _subs.Add(_bus.Subscribe<QuestStartedEvent>(OnQuestStarted));
    _subs.Add(_bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted));
    _subs.Add(_bus.Subscribe<WordSeenEvent>(OnWordSeen));
    _subs.Add(_bus.Subscribe<StoryMomentEvent>(OnStoryMoment));
  }

  // Player hand anchor (same fist-bone anchor the apple rides; set by Lead).
  public void SetHand(Transform hand) {
    _hand = hand;
    if (_carriedBall != null && _hand != null) _carriedBall.transform.SetParent(_hand);
  }

  // Lead introspection (survey telemetry + tests).
  public bool IsCarrying {
    get { return _carryingBall; }
  }

  // Spoken readout (player rule: every clickable reads its name). The router
  // plays this through the shared vocab path on arrival (same audio the
  // crates get — an IClickTarget carries no vocab channel of its own).
  public WordId SpeakWord {
    get { return _ballWord; }
  }

  public bool IsBallHome {
    get { return gameObject.activeSelf; }
  }

  // IClickTarget entry point for the router (arrival-gated, like Milo/Mia).
  public void OnClicked() {
    QuestId quest = new QuestId(questIdValue);
    if (_quests != null && _quests.GetState(quest).Completed) return; // post-quest: inert
    if (!_questStarted) {
      LegacyWrong(quest); // pre-talk: no state changes (R7 softlock lesson)
      return;
    }
    if (_carryingBall) return; // already holding it (live-unreachable: ball hidden)
    if (_appleCarried) {
      LegacyWrong(quest); // hands full of the quest item: a genuine mistake
      return;
    }
    PickUp();
  }

  // Legacy instant-wrong (pre-quest + hands-full paths): hint ladder + moment,
  // visual squash, prop stays. Retry always preserved.
  void LegacyWrong(QuestId quest) {
    _squashT = 0.35f; // visible "oops" squash, visual only
    if (_hints != null) _hints.ReportWrong(quest);
    if (_bus != null) _bus.Publish(new StoryMomentEvent(StoryMoment.WrongChoice, DateTime.UtcNow));
  }

  void PickUp() {
    _carryingBall = true;
    gameObject.SetActive(false); // the pedestal empties: the ball is in hand now
    AttachCarriedBall();
    // Arms Mia's wrong-carry context + learning exposure; quest/logic untouched.
    if (_bus != null) _bus.Publish(new WordSeenEvent(_ballWord, LearnSource.Object, DateTime.UtcNow));
  }

  // The ball hops home: pedestal refills, mini hides. Idempotent (every wrong
  // path funnels here; calling it with the ball home is a no-op).
  void RestoreBall() {
    _carryingBall = false;
    if (!gameObject.activeSelf) gameObject.SetActive(true);
    if (_carriedBall != null) _carriedBall.SetActive(false);
  }

  void AttachCarriedBall() {
    if (_carriedBall == null) _carriedBall = BuildMiniBall();
    if (_carriedBall == null) return;
    _carriedBall.SetActive(true);
    if (_hand != null) {
      _carriedBall.transform.SetParent(_hand);
      _carriedBall.transform.localPosition = Vector3.zero;
      _carriedBall.transform.localRotation = Quaternion.identity;
    }
  }

  GameObject BuildMiniBall() {
    GameObject mini = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    mini.name = "CarriedBall";
    mini.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
    Renderer renderer = mini.GetComponent<Renderer>();
    if (renderer != null) {
      Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      mat.SetColor("_BaseColor", new Color(0.20f, 0.42f, 0.90f)); // same blue as the pedestal ball
      renderer.sharedMaterial = mat;
    }
    Collider collider = mini.GetComponent<Collider>();
    if (collider != null) CharacterPresentation.DestroyNow(collider); // carried decoy must not eat clicks
    return mini;
  }

  void OnQuestStarted(QuestStartedEvent e) {
    _activeQuest = e.QuestId;
    _questStarted = true;
    _carryingBall = false;
    _appleCarried = false;
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    if (e.QuestId.Value != _activeQuest.Value) return;
    _questStarted = false;
    _appleCarried = false;
    RestoreBall(); // defense in depth (completion needs the apple, so this is a no-op live)
  }

  void OnWordSeen(WordSeenEvent e) {
    // Player rule: pre-quest taps arm nothing (mirrors the pickup gate above).
    if (!_questStarted) return;
    if (e.WordId.Value == AppleWord) {
      _appleCarried = true;
      // SWAP: the quest item takes the hands; the ball hops home so the item
      // is never active in two places and the child is never stuck holding
      // the wrong thing (proximity discovery near the crate auto-swaps).
      if (_carryingBall) RestoreBall();
    }
  }

  void OnStoryMoment(StoryMomentEvent e) {
    if (e.Moment == StoryMoment.CorrectChoice) {
      _appleCarried = false; // the bring landed: hands are empty again
    } else if (e.Moment == StoryMoment.WrongChoice) {
      // Wrong-bring of the ball funnels here (Mia publishes it): the ball
      // hops home. Legacy wrongs (ball already home) are idempotent no-ops.
      if (_carryingBall) RestoreBall();
    }
  }

  void Update() {
    if (_squashT <= 0f) return;
    _squashT -= Time.deltaTime;
    float k = _squashT > 0f ? 1f - Mathf.Sin(Mathf.Clamp01(_squashT / 0.35f) * Mathf.PI) * 0.25f : 1f;
    transform.localScale = new Vector3(_baseScale.x / k, _baseScale.y * k, _baseScale.z / k);
    if (_squashT <= 0f) transform.localScale = _baseScale;
  }

  // NOTE: ClearSubs lives in OnDestroy, NOT OnDisable — PickUp hides this
  // GameObject (which fires OnDisable), and the hidden ball MUST keep its
  // subscriptions (swap-on-apple + restore-on-wrong arrive while hidden).
  void OnDestroy() {
    ClearSubs();
  }

  void ClearSubs() {
    foreach (IDisposable s in _subs) {
      if (s != null) s.Dispose();
    }
    _subs.Clear();
  }
}
