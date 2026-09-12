// A_World/DistractorChoice.cs — Agent A (World & Visual). Reusable wrong-choice
// prop: a plausible-but-incorrect selectable object (W1: red ball next to the
// apple crate). Implements SharedKernel IClickTarget so the existing router
// arrival flow works with ZERO router changes; the IClickTarget path plays NO
// vocab audio, so distractors need no Content/audio entries (counts frozen).
// OnClicked (only while its quest is incomplete): HintService.ReportWrong
// (existing ladder), a visual squash (presentation-only), and a
// StoryMomentEvent(WrongChoice) that Brain presenters react to (Mia sad, Milo
// encourages, camera focuses). Retry is always preserved: the prop stays.
// QuestId arrives as a plain string field (inspector/content configurable).
// C# 9.0 only. No Brain references (World->Brain contact is bus-only).
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class DistractorChoice : MonoBehaviour, IClickTarget {
  [Tooltip("Quest this distractor counts against (raw string, parsed at boundary).")]
  public string questIdValue = "w1_mia_apple";

  IGameEventBus _bus;
  IHintService _hints;
  IQuestService _quests;

  float _squashT;
  Vector3 _baseScale;

  void Awake() {
    _baseScale = transform.localScale;
  }

  // Injection boundary (MarketBuilder wires services; never newed here).
  public void Bind(IGameEventBus bus, IHintService hints, IQuestService quests) {
    _bus = bus;
    _hints = hints;
    _quests = quests;
  }

  // IClickTarget entry point for the router (arrival-gated, like Milo/Mia).
  public void OnClicked() {
    QuestId quest = new QuestId(questIdValue);
    if (_quests != null && _quests.GetState(quest).Completed) return; // post-quest: inert
    _squashT = 0.35f; // visible "oops" squash, visual only
    if (_hints != null) _hints.ReportWrong(quest);
    if (_bus != null) _bus.Publish(new StoryMomentEvent(StoryMoment.WrongChoice, DateTime.UtcNow));
  }

  void Update() {
    if (_squashT <= 0f) return;
    _squashT -= Time.deltaTime;
    float k = _squashT > 0f ? 1f - Mathf.Sin(Mathf.Clamp01(_squashT / 0.35f) * Mathf.PI) * 0.25f : 1f;
    transform.localScale = new Vector3(_baseScale.x / k, _baseScale.y * k, _baseScale.z / k);
    if (_squashT <= 0f) transform.localScale = _baseScale;
  }
}
