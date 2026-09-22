// A_World/MathWorld/MathTokenCarry.cs — Phase 3.0.x S3B. Counting-token lifecycle.
// The quest "one" cube moves World -> Carried -> Consumed through BUS EVENTS
// only (WordSeen / QuestStarted / QuestCompleted) — no direct refs to the
// presenter/director/quest services beyond the frozen boundaries, no new Core
// service. Mirrors the Main carry contract (crate hides on find, hand shows;
// consumed on bring) minus the Main-only hand-rig specifics:
//   - hand anchor is the traveling PlayerHand (child of the Player GO, so it
//     survives into MathScene — parenting under Main hierarchy would hide it);
//   - re-entry resume adopts live quest state at Build (fresh scene + resumed
//     bring = cube starts hidden, no event replay needed).
// Token states are data (MathTokenState) for EditMode pins. C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

public enum MathTokenState {
  InWorld,  // cube sits on its garden pedestal
  Carried,  // cube hidden, hand token shown
  Consumed, // bring complete: hand token hidden, cube stays hidden
}

[DisallowMultipleComponent]
public class MathTokenCarry : MonoBehaviour {
  static readonly QuestId MathQuest = new QuestId("math_counting");
  static readonly WordId OneWord = new WordId("one");

  IGameEventBus _bus;
  IQuestService _quests;
  Interactable _worldCube;
  Transform _handAnchor;
  GameObject _handToken;
  bool _started;
  readonly List<IDisposable> _subs = new List<IDisposable>();
  bool _built;

  public MathTokenState State { get; private set; }

  public bool IsTokenShown {
    get { return _handToken != null && _handToken.activeSelf; }
  }

  // Injection boundary (GameInstaller calls this on MathScene load, after the
  // builder exposes CountingObjects). Null-safe: missing pieces leave the
  // token dormant (quest still completable by walking — skeleton holds).
  public void Build(IGameEventBus bus, IQuestService quests, Interactable worldCube, Transform handAnchor) {
    if (_built) return;
    _built = true;
    State = MathTokenState.InWorld;
    _bus = bus;
    _quests = quests;
    _worldCube = worldCube;
    _handAnchor = handAnchor;
    BuildToken();
    if (_bus == null || _quests == null) return;
    try {
      _subs.Add(_bus.Subscribe<WordSeenEvent>(OnWordSeen));
      _subs.Add(_bus.Subscribe<QuestStartedEvent>(OnQuestStarted));
      _subs.Add(_bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted));
    } catch (Exception) { }
    // Re-entry resume: fresh scene adopts the live quest state (no event
    // replay exists for a fresh subscriber). Both mid-quest (Carried) and
    // completed (Consumed) must be honored — journey evidence: without the
    // Completed branch the reward cube reappeared after completion.
    try {
      QuestState s = _quests.GetState(MathQuest);
      if (s != null && s.Completed) {
        _started = true;
        State = MathTokenState.Consumed;
        if (_worldCube != null) _worldCube.gameObject.SetActive(false);
        if (_handToken != null) _handToken.SetActive(false);
      } else if (s != null && s.ObjectiveIndex >= 1) {
        _started = true;
        SetCarried();
      }
    } catch (Exception) { }
  }

  void OnQuestStarted(QuestStartedEvent e) {
    if (e.QuestId.Value != MathQuest.Value) return;
    _started = true;
  }

  void OnWordSeen(WordSeenEvent e) {
    if (!_started || State != MathTokenState.InWorld) return;
    if (e.WordId.Value != OneWord.Value) return;
    try {
      if (_quests == null) return;
      QuestState s = _quests.GetState(MathQuest);
      if (s == null || s.Completed || s.ObjectiveIndex < 1) return; // find not done yet
      SetCarried();
    } catch (Exception) { }
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    if (e.QuestId.Value != MathQuest.Value) return;
    State = MathTokenState.Consumed;
    try { if (_handToken != null) _handToken.SetActive(false); } catch (Exception) { }
  }

  void SetCarried() {
    State = MathTokenState.Carried;
    try { if (_worldCube != null) _worldCube.gameObject.SetActive(false); } catch (Exception) { }
    try { if (_handToken != null) _handToken.SetActive(true); } catch (Exception) { }
  }

  void BuildToken() {
    try {
      GameObject token = GameObject.CreatePrimitive(PrimitiveType.Cube);
      token.name = "MathCarryToken";
      if (_handAnchor != null) token.transform.SetParent(_handAnchor, false);
      else token.transform.SetParent(transform, false);
      token.transform.localPosition = _handAnchor != null ? Vector3.zero : new Vector3(0f, 1f, 0f);
      token.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
      Renderer r = token.GetComponent<Renderer>();
      if (r != null) {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", new Color(0.98f, 0.78f, 0.25f));
        r.sharedMaterial = mat;
      }
      Collider c = token.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
      token.SetActive(false);
      _handToken = token;
    } catch (Exception) { _handToken = null; }
  }

  void OnDisable() {
    foreach (IDisposable s in _subs) {
      if (s == null) continue;
      try { s.Dispose(); } catch (Exception) { }
    }
    _subs.Clear();
  }
}
