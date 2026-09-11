// B_Brain/MiloPresenter.cs — Agent B (W1). Procedural Milo companion presenter.
// MonoBehaviour (Unity instantiates): parameterless ctor + public Bind(...) only.
// Lead wiring: set SpawnPosition (A anchor (2.5,0,1.5)) + PlayerTarget, then
//   Bind(bus, quests, hints). Implements SharedKernel IClickTarget so A's
// click router calls OnClicked() (typed, no reflection, no input code here).
// All voice output goes through the static Milo class (IAudioDirector only).
// No input code (no OnMouseDown), no Camera calls, no `new` services,
// no provider/Worker refs. Null-guarded throughout (batch-safe).
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MiloPresenter : MonoBehaviour, IClickTarget {
  const float GreetDistance = 3f;
  const float HopDuration = 0.8f;

  [Header("Lead-wired placement (A anchor)")]
  public Vector3 SpawnPosition = new Vector3(2.5f, 0f, 1.5f);
  [Header("Lead-wired player reference (Transform only, null-guarded)")]
  public Transform PlayerTarget;

  IGameEventBus _bus;
  IQuestService _quests;
  IHintService _hints;
  readonly List<IDisposable> _subs = new List<IDisposable>();

  QuestId _activeQuest = new QuestId("w1_mia_apple");
  float _clockSinceProgress;
  bool _greeted;
  float _hopT;
  Transform _body;
  Vector3 _bodyBase;
  float _bobPhase;
  bool _built;

  // Unique-per-instance bob offset without Object.GetInstanceID (obsolete as
  // error in Unity 6). Monotonic session counter is all the visual needs.
  static int s_bobSeed;

  void Awake() {
    _bobPhase = (float)(s_bobSeed++ % 360);
    BuildMilo();
    ApplySpawnPosition();
  }

  void Start() {
    // Lead sets SpawnPosition after AddComponent (post-Awake); re-apply here
    // so the Inspector/wired value wins before the first frame.
    ApplySpawnPosition();
  }

  void ApplySpawnPosition() {
    transform.position = SpawnPosition;
  }

  // Injection boundary (wired by Lead/GameInstaller). Re-bind safe: old
  // subscriptions are disposed first. Null bus -> unbound, no subscriptions.
  public void Bind(IGameEventBus bus, IQuestService quests, IHintService hints) {
    ClearSubs();
    _bus = bus;
    _quests = quests;
    _hints = hints;
    if (_bus == null) return;
    _subs.Add(_bus.Subscribe<WordSeenEvent>(OnWordSeen));
    _subs.Add(_bus.Subscribe<QuestStartedEvent>(OnQuestStarted));
    _subs.Add(_bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted));
    _subs.Add(_bus.Subscribe<HintLevelChanged>(OnHintLevel));
  }

  // IClickTarget entry point for A's router (no input code in this file).
  public void OnClicked() {
    OnMiloClicked();
  }

  // Click entry point for A's router (no input code in this file).
  public void OnMiloClicked() {
    Milo.RepeatInstruction();
  }

  void OnWordSeen(WordSeenEvent e) {
    _clockSinceProgress = 0f;
  }

  void OnQuestStarted(QuestStartedEvent e) {
    _activeQuest = e.QuestId;
    _clockSinceProgress = 0f;
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    _clockSinceProgress = 0f;
    if (e.QuestId.Value == _activeQuest.Value) _hopT = HopDuration;
  }

  void OnHintLevel(HintLevelChanged e) {
    if (e.QuestId.Value != _activeQuest.Value) return;
    if (e.Level >= 3) Milo.DemoHint();
    else if (e.Level >= 2) Milo.PointHint();
  }

  void Update() {
    float dt = Time.deltaTime;
    _clockSinceProgress += dt;

    if (_hints != null) {
      bool done = false;
      if (_quests != null) done = _quests.GetState(_activeQuest).Completed;
      if (!done) _hints.Tick(_activeQuest, dt, _clockSinceProgress);
    }

    if (PlayerTarget != null) {
      Vector3 toPlayer = PlayerTarget.position - transform.position;
      toPlayer.y = 0f;
      if (toPlayer.sqrMagnitude > 0.0001f)
        transform.rotation = Quaternion.LookRotation(toPlayer);
      if (!_greeted && toPlayer.magnitude < GreetDistance) {
        _greeted = true;
        Milo.Greet();
      }
    }

    if (_hopT > 0f) {
      _hopT -= dt;
      if (_hopT <= 0f) transform.position = SpawnPosition;
      else transform.position = SpawnPosition + new Vector3(0f, Mathf.Abs(Mathf.Sin(_hopT * 10f)) * 0.25f, 0f);
    }

    if (_body != null)
      _body.localPosition = _bodyBase + new Vector3(0f, Mathf.Sin(Time.time * 2f + _bobPhase) * 0.05f, 0f);
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

  // Procedural marshmallow Milo: capsule body + sphere head + eyes, warm
  // orange. Primitives keep their colliders so A's router raycast can hit
  // Milo; this file reads clicks ONLY via OnMiloClicked().
  void BuildMilo() {
    if (_built) return;
    _built = true;

    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
    if (body == null) return;
    body.name = "MiloBody";
    body.transform.SetParent(transform, false);
    body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
    body.transform.localScale = new Vector3(0.7f, 0.75f, 0.7f);
    SetColor(body, new Color(1f, 0.62f, 0.25f));
    _body = body.transform;
    _bodyBase = _body.localPosition;

    GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    if (head == null) return;
    head.name = "MiloHead";
    head.transform.SetParent(transform, false);
    head.transform.localPosition = new Vector3(0f, 1.65f, 0f);
    head.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
    SetColor(head, new Color(1f, 0.68f, 0.32f));

    MakeEye("MiloEyeL", new Vector3(-0.12f, 1.72f, 0.26f));
    MakeEye("MiloEyeR", new Vector3(0.12f, 1.72f, 0.26f));
  }

  void MakeEye(string eyeName, Vector3 localPos) {
    GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    if (eye == null) return;
    eye.name = eyeName;
    eye.transform.SetParent(transform, false);
    eye.transform.localPosition = localPos;
    eye.transform.localScale = new Vector3(0.09f, 0.11f, 0.06f);
    SetColor(eye, Color.black);
  }

  static void SetColor(GameObject go, Color color) {
    if (go == null) return;
    Renderer r = go.GetComponent<Renderer>();
    if (r == null) return;
    r.material.color = color;
  }
}
