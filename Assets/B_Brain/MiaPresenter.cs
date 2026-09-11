// B_Brain/MiaPresenter.cs — Agent B (W1). Warm shopkeeper Mia presenter.
// MonoBehaviour (Unity instantiates): parameterless ctor + public Bind(...) only.
// Lead wiring: set SpawnPosition ((-3.5,0,-2.5)), then Bind(bus, quests, hints).
// A's click router calls OnMiaClicked() on click (no input code in this file).
// Flow: apple WordSeen arms carryingApple; clicking Mia while carrying reports
// the explicit Bring action (the ONLY path that advances bring_apple); clicking
// Mia empty-handed is a gentle correction (hint wrong-count + Milo encouragement,
// never a fail state). No Camera calls, no `new` services, no provider/Worker
// refs. Typed IDs only. Null-guarded throughout (batch-safe).
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MiaPresenter : MonoBehaviour, IClickTarget {
  [Header("Lead-wired placement")]
  public Vector3 SpawnPosition = new Vector3(-3.5f, 0f, -2.5f);

  IGameEventBus _bus;
  IQuestService _quests;
  IHintService _hints;
  readonly List<IDisposable> _subs = new List<IDisposable>();

  QuestId _activeQuest = new QuestId("w1_mia_apple");
  readonly WordId _appleWord = new WordId("apple");
  bool _carryingApple;
  bool _built;

  void Awake() {
    BuildMia();
    ApplySpawnPosition();
  }

  void Start() {
    // Lead sets SpawnPosition after AddComponent (post-Awake); re-apply here
    // so the wired value wins before the first frame.
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
  }

  // IClickTarget entry point for A's router (no input code in this file).
  public void OnClicked() {
    OnMiaClicked();
  }

  // Click entry point for A's router (no input code in this file).
  public void OnMiaClicked() {
    if (_carryingApple) {
      if (_quests == null) return;
      _quests.ReportAction(PlayerAction.Bring, _appleWord);
      _carryingApple = false;
    } else {
      if (_hints != null) _hints.ReportWrong(_activeQuest);
      Milo.Encourage();
    }
  }

  void OnWordSeen(WordSeenEvent e) {
    if (e.WordId.Value == _appleWord.Value) _carryingApple = true;
  }

  void OnQuestStarted(QuestStartedEvent e) {
    _activeQuest = e.QuestId;
    _carryingApple = false;
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

  // Warm shopkeeper figure: capsule body (apron coral) + sphere head (skin
  // tone) + hair cap + eyes. Distinct palette/silhouette from Milo.
  // Primitives keep their colliders so A's router raycast can hit Mia.
  void BuildMia() {
    if (_built) return;
    _built = true;

    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
    if (body == null) return;
    body.name = "MiaBody";
    body.transform.SetParent(transform, false);
    body.transform.localPosition = new Vector3(0f, 0.8f, 0f);
    body.transform.localScale = new Vector3(0.75f, 0.8f, 0.75f);
    SetColor(body, new Color(0.95f, 0.45f, 0.4f));

    GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    if (head == null) return;
    head.name = "MiaHead";
    head.transform.SetParent(transform, false);
    head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
    head.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
    SetColor(head, new Color(1f, 0.85f, 0.7f));

    GameObject hair = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    if (hair == null) return;
    hair.name = "MiaHair";
    hair.transform.SetParent(transform, false);
    hair.transform.localPosition = new Vector3(0f, 1.94f, -0.03f);
    hair.transform.localScale = new Vector3(0.58f, 0.3f, 0.58f);
    SetColor(hair, new Color(0.35f, 0.2f, 0.12f));

    MakeEye("MiaEyeL", new Vector3(-0.11f, 1.76f, 0.24f));
    MakeEye("MiaEyeR", new Vector3(0.11f, 1.76f, 0.24f));
  }

  void MakeEye(string eyeName, Vector3 localPos) {
    GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    if (eye == null) return;
    eye.name = eyeName;
    eye.transform.SetParent(transform, false);
    eye.transform.localPosition = localPos;
    eye.transform.localScale = new Vector3(0.08f, 0.1f, 0.06f);
    SetColor(eye, Color.black);
  }

  static void SetColor(GameObject go, Color color) {
    if (go == null) return;
    Renderer r = go.GetComponent<Renderer>();
    if (r == null) return;
    r.material.color = color;
  }
}
