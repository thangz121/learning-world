// A_World/ProximityDiscovery.cs — Agent A (World & Visual). Reusable proximity
// trigger for Interactables (find-quests): fires the target's Interact() on
// player ENTER range while armed, rising-edge only (standing inside is silent).
// Re-arms on QuestStarted, disarms on QuestCompleted (bus events only — this
// component never touches quest LOGIC; QuestManager advances Find-only, so a
// proximity fire is the same event as a click arrival, idempotent by design).
// Suppressed while the ClickRouter holds a pending arrival for the same target
// (the router fires on arrival; audio dedupes same-word within 2s regardless).
// Bind(bus, audio, player, router) — single call from MarketBuilder.
// Null-guarded throughout (batch-safe). C# 9.0 only. No Brain references.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ProximityDiscovery : MonoBehaviour {
  [Tooltip("Quest this discovery counts for (raw string, parsed at boundary).")]
  public string questIdValue = "w1_mia_apple";

  IGameEventBus _bus;
  IAudioDirector _audio;
  ClickToMove _player;
  ClickRouter _router;
  Interactable _target;
  readonly List<IDisposable> _subs = new List<IDisposable>();

  bool _armed;
  bool _wasInside;

  void Awake() {
    _target = GetComponent<Interactable>();
  }

  // Injection boundary (wired by MarketBuilder.BuildServices).
  public void Bind(IGameEventBus bus, IAudioDirector audio, ClickToMove player, ClickRouter router) {
    ClearSubs();
    _bus = bus;
    _audio = audio;
    _player = player;
    _router = router;
    if (_target == null) _target = GetComponent<Interactable>();
    if (_bus == null) return;
    _subs.Add(_bus.Subscribe<QuestStartedEvent>(OnQuestStarted));
    _subs.Add(_bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted));
  }

  void OnDisable() {
    ClearSubs();
  }

  void ClearSubs() {
    foreach (IDisposable s in _subs) {
      if (s != null) s.Dispose();
    }
    _subs.Clear();
  }

  void OnQuestStarted(QuestStartedEvent e) {
    if (e.QuestId.Value != new QuestId(questIdValue).Value) return;
    _armed = true;
    _wasInside = false;
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    if (e.QuestId.Value != new QuestId(questIdValue).Value) return;
    _armed = false;
  }

  void Update() {
    if (_player != null) PollImmediate(_player.transform.position);
  }

  // Deterministic tick (tests drive this directly; Update forwards live play).
  public void PollImmediate(Vector3 playerPos) {
    if (!_armed || _target == null) return;
    bool inside;
    try {
      inside = _target.IsInRange(playerPos);
    } catch (Exception) {
      return;
    }
    if (inside && !_wasInside && !RouterWillFire()) Fire();
    _wasInside = inside;
  }

  bool RouterWillFire() {
    return _router != null && _router.PendingInteractTarget == _target && _target != null;
  }

  void Fire() {
    _target.Interact(); // same WordSeenEvent path as a click arrival
    PlayVocabFireAndForget();
  }

  // Fire-and-forget vocab audio (router pattern): slow/failed fetch must never
  // block movement. AudioDirector dedupes same text+voice within 2s.
  async void PlayVocabFireAndForget() {
    if (_audio == null || _target == null || !_target.HasWord) return;
    try {
      await _audio.PlayVocabularyAsync(_target.Word, VocabularyAudioMode.Normal);
    } catch (Exception e) {
      Debug.LogWarning("[ProximityDiscovery] Vocabulary audio failed: " + e.Message, this);
    }
  }
}
