// A_World/DiscoveryGarden/DiscoveryGame.cs — S3-P2Z18 GAMEPLAY #7
// "VƯỜN KHÁM PHÁ" (Discovery Garden). The garden's activity, staged in its
// OWN lazy scene (DiscoveryScene), reached from the Math Hub's
// discovery_garden gate.
// The experience: the teacher shows the task on the reference board ("Find
// the apple!") -> the child student runs a REAL search (checks the flower bed,
// checks toward the butterfly bush, THEN notices the apple) -> handover ->
// the CHILD searches three tasks in order (apple -> flower -> butterfly);
// wrong candidates earn a gentle "not that one, keep looking", never a fail;
// finding the target sparkles, the teacher praises, and the result board
// gains its icon. After three finds: celebrate, result complete, exit cue.
// Priority (brief final note): CLARITY -> WORLD IDENTITY -> REAL EXPLORATION
// -> CHILD READABILITY -> PRESENTATION -> ROBUSTNESS.
// Architecture: scene-local components, no manager/singleton, no new
// bus/service; reuses LessonActors (shared body kit), PacedVoice (paced
// speech), GameJuice + ActivityFeedback (the shared feedback layers), the
// SmartCamera beats, ActivityLifecycle (owned by the Math-side area) and
// MicroWorldPortal (the door). NO ActivityGuide: exploration must never be
// arrow-guided (brief §7/§10). C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

// One real searchable object: Available -> Found. No carry, no pickup — the
// interaction is LOOK -> APPROACH -> TOUCH; a found object can never be found
// again (no double count), and distractors simply stay Available.
[DisallowMultipleComponent]
public class DiscoveryItem : MonoBehaviour, IClickTarget {
  public enum ItemKind { Apple, Flower, Butterfly, Ball, Leaf, Mushroom }
  public enum ItemState { Available, Found }

  public ItemKind Kind = ItemKind.Apple;
  public bool IsExtra;                // round-1 tier (more candidates)
  public Vector3 HomeLocal;
  public ItemState State { get; private set; } = ItemState.Available;
  public DiscoveryGame Game;
  public GameObject FoundMark { get; private set; }

  float _flyT;
  Vector3 _baseScale = Vector3.one;

  public bool IsAvailable {
    get { return State == ItemState.Available && gameObject.activeSelf; }
  }

  public void Bind(DiscoveryGame game, bool isExtra, GameObject foundMark) {
    Game = game;
    IsExtra = isExtra;
    FoundMark = foundMark;
    _baseScale = transform.localScale;
    _flyT = Mathf.Repeat(HomeLocal.x * 1.7f + HomeLocal.z * 2.3f, Mathf.PI * 2f);
  }

  public void OnClicked() {
    if (State != ItemState.Available || Game == null) return;
    Game.TryInteract(this);
  }

  public void SetFound(bool found) {
    State = found ? ItemState.Found : ItemState.Available;
    if (FoundMark != null) FoundMark.SetActive(found);
    if (found) {
      try { GameJuice.Pop(transform, 0.2f, 0.36f); } catch (Exception) { }
    }
  }

  // Deterministic tick (owned by DiscoveryGame.Tick: single owner, so live
  // play and EditMode advance the butterfly exactly once).
  public void TickForTests(float dt) {
    try {
      if (Kind != ItemKind.Butterfly) {
        TickCloseBreathe(dt);
        return;
      }
      if (State == ItemState.Found) {
        // Landed: a gentle hover in place (it has been discovered).
        float bob = 0.04f * Mathf.Sin(Time.time * 2.1f);
        transform.localPosition = HomeLocal + new Vector3(0f, bob, 0f);
        return;
      }
      // Available: a slow, small orbit around the bush — the movement IS the
      // clue (brief §10), never a marker.
      _flyT += dt * 0.7f;
      Vector3 orbit = new Vector3(Mathf.Cos(_flyT) * DiscoveryBuilder.ButterflyOrbitRadius,
        0f, Mathf.Sin(_flyT) * DiscoveryBuilder.ButterflyOrbitRadius);
      float hover = 0.06f * Mathf.Sin(Time.time * 2.6f);
      transform.localPosition = HomeLocal + orbit + new Vector3(0f, hover, 0f);
      transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(orbit.x, orbit.z) * Mathf.Rad2Deg, 0f);
    } catch (Exception) { }
  }

  // Very subtle close-range breathe: "you can touch this" without a neon glow
  // (brief §7 forbids marker-like highlights; amplitude is deliberately tiny).
  void TickCloseBreathe(float dt) {
    if (Game != null && Game.PlayerNear(transform.position, 1.8f) && State == ItemState.Available) {
      float s = 1f + 0.03f * Mathf.Sin(Time.time * 4f);
      transform.localScale = _baseScale * s;
    } else {
      transform.localScale = _baseScale;
    }
  }
}

[DisallowMultipleComponent]
public class DiscoveryGame : MonoBehaviour {
  public enum Phase {
    Intro,    // teacher shows the task on the reference board
    Demo,     // the student runs a REAL search and finds the apple
    Handoff,  // "Now it's your turn!" + the demo find tidies up
    Tasks,    // the child searches task 1..3 (apple -> flower -> butterfly)
    Success,  // everything found — celebrated, field stays open
  }

  // Beat timings (one place; deterministic for Tick tests).
  const float DemoHold = 0.8f;           // student pause at a checked spot
  const float NextTaskDelay = 1.4f;      // praise -> next task line
  const float SuccessDelay = 1.2f;       // last find -> celebration beats
  const float WrongCooldown = 4f;
  const float WalkSpeed = 0.85f;         // student legs
  public static readonly Vector3 FollowOffset = DiscoveryBuilder.FollowOffset;

  public Phase Current { get; private set; } = Phase.Intro;
  public int Round { get; private set; }
  public int TaskIndex { get; private set; }        // 0..TaskCount
  public int FoundCount { get; private set; }       // = TaskIndex while playing
  public int WrongAttempts { get; private set; }
  public int DemoChecks { get { return _demoChecks; } }
  public bool DemoFound { get { return _demoFound; } }
  public bool ResultShown { get { return _result != null && _result.activeSelf; } }
  public bool ExitCueShown { get { return _exitCue != null && _exitCue.activeSelf; } }
  public int ItemCountTotal { get { return _items.Count; } }
  public int ActiveItemCount {
    get {
      int n = 0;
      for (int i = 0; i < _items.Count; i++)
        if (_items[i] != null && _items[i].gameObject.activeSelf) n++;
      return n;
    }
  }
  public DiscoveryItem ItemAt(int i) { return i >= 0 && i < _items.Count ? _items[i] : null; }
  public DiscoveryItem.ItemKind CurrentTaskKind {
    get { return DiscoveryBuilder.TaskKinds[Mathf.Min(TaskIndex, DiscoveryBuilder.TaskCount - 1)]; }
  }

  DiscoveryBuilder _builder;
  Transform _root;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PlayerVisual _viz;
  ClickToMove _mover;
  Action<int> _onCompleted;
  string _studentVoiceId;

  LessonActor _teacher;
  LessonActor _student;
  PacedVoice _voice;
  PacedVoice _studentVoice;
  Transform _fx;

  GameObject _result;
  GameObject _exitCue;
  Transform _camTeaching, _lookTeaching, _camDemo, _lookDemo, _camSuccess, _lookSuccess;

  readonly List<DiscoveryItem> _items = new List<DiscoveryItem>();
  readonly Queue<GameObject> _resultQueue = new Queue<GameObject>();

  float _phaseT;
  float _shotT = -1f;
  bool _shotIssued;
  bool _cameraDone;
  bool _followHanded;
  int _shot;

  bool _saidBoard, _saidTask, _saidExplore;
  int _demoStage;
  int _demoChecks;
  bool _demoFound;
  float _demoHoldT;
  bool _saidWatch, _saidWhere, _saidFound, _saidYes;
  bool _saidTurn, _saidGo;
  bool _studentReturned;
  float _advanceT = -1f;
  float _successT = -1f;
  float _wrongT;
  float _victoryT = -1f;
  float _resultPopT = 1f;
  Vector3 _lastPos;
  readonly Queue<string> _recapEn = new Queue<string>();
  readonly Queue<string> _recapVi = new Queue<string>();

  // Test seams (no live scene needed).
  public void SetPhaseForTests(Phase p) { Current = p; _phaseT = 0f; }
  public void MarkLifecycleActiveForTests() {
    if (_life == null) return;
    try {
      if (_life.State == ActivityState.Ready || _life.State == ActivityState.Available)
        _life.Begin("test active");
    } catch (Exception) { }
  }

  public void Build(DiscoveryBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life, int round,
      Action<int> onCompleted = null, string studentVoiceId = null) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _onCompleted = onCompleted;
    _studentVoiceId = studentVoiceId;
    Round = Mathf.Clamp(round, 0, DiscoveryArea.MaxRound);
    _viz = player != null ? player.GetComponent<PlayerVisual>() : null;
    _mover = player != null ? player.GetComponent<ClickToMove>() : null;
    if (_builder == null) {
      Debug.LogWarning("[DiscoveryGame] no builder; activity parked.", this);
      return;
    }
    _root = _builder.transform;
    _result = _builder.Result;
    _exitCue = _builder.ExitCue;
    _camTeaching = _builder.CamTeaching;
    _lookTeaching = _builder.LookTeaching;
    _camDemo = _builder.CamDemo;
    _lookDemo = _builder.LookDemo;
    _camSuccess = _builder.CamSuccess;
    _lookSuccess = _builder.LookSuccess;

    BuildActors();
    GameObject fx = new GameObject("DGFx");
    fx.transform.SetParent(_root, false);
    _fx = fx.transform;

    // Items become real, clickable searchables. UNCONDITIONAL collider add
    // (the builder strips colliders for bake safety with deferred Destroy, so
    // a null-check here would leave items click-less).
    _items.Clear();
    List<GameObject> items = _builder.Items;
    for (int i = 0; i < items.Count; i++) {
      GameObject go = items[i];
      if (go == null) continue;
      SphereCollider sc = go.AddComponent<SphereCollider>();
      sc.radius = 0.5f;
      sc.center = new Vector3(0f, 0.12f, 0f);
      DiscoveryItem item = go.GetComponent<DiscoveryItem>();
      if (item == null) item = go.AddComponent<DiscoveryItem>();
      item.Kind = _builder.ItemKinds != null && i < _builder.ItemKinds.Length
        ? _builder.ItemKinds[i] : DiscoveryItem.ItemKind.Apple;
      item.HomeLocal = go.transform.localPosition;
      // Found marker: a small emissive disc that appears above a found item.
      GameObject mark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      mark.name = go.name + "FoundMark";
      mark.transform.SetParent(go.transform, false);
      mark.transform.localPosition = new Vector3(0f, 0.62f, 0f);
      mark.transform.localScale = new Vector3(0.26f, 0.012f, 0.26f);
      Material markMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      Color mint = new Color(0.62f, 0.92f, 0.68f);
      markMat.SetColor("_BaseColor", mint);
      if (markMat.HasProperty("_Smoothness")) markMat.SetFloat("_Smoothness", 0f);
      if (markMat.HasProperty("_Metallic")) markMat.SetFloat("_Metallic", 0f);
      if (markMat.HasProperty("_EmissionColor")) {
        markMat.EnableKeyword("_EMISSION");
        markMat.SetColor("_EmissionColor", mint * 0.6f);
      }
      markMat.enableInstancing = true;
      Renderer mr = mark.GetComponent<Renderer>();
      if (mr != null) mr.sharedMaterial = markMat;
      try {
        Collider mc = mark.GetComponent<Collider>();
        if (mc != null) CharacterPresentation.DestroyNow(mc);
      } catch (Exception) { }
      try {
        Unity.AI.Navigation.NavMeshModifier mod = mark.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
        mod.ignoreFromBuild = true;
      } catch (Exception) { }
      mark.SetActive(false);
      bool isExtra = _builder.ItemExtras != null && i < _builder.ItemExtras.Length
        && _builder.ItemExtras[i];
      item.Bind(this, isExtra, mark);
      _items.Add(item);
    }
    ApplyRound();

    // Reference board: only the current task icon is lit.
    ApplyTaskIcon();

    // Re-entry policy FIRST (same lesson as #2-#6): a completed activity
    // adopts the finished picture without replaying the lesson, and the shared
    // lifecycle lives in MathScene so it survives this scene's unload.
    if (_life != null && _life.State == ActivityState.Completed) {
      ApplyCompletedState("adopt");
      return;
    }
    if (_life != null) {
      try {
        _life.MarkAvailable("discover_items staged");
        _life.BeginEnter("discovery garden built");
        _life.MarkReady("intro staged");
      } catch (Exception) { }
    }
    _lastPos = _player != null ? _player.position : Vector3.zero;
    try { Debug.Log("[DiscoveryGame] activity staged (intro will play).", this); } catch (Exception) { }
  }

  void BuildActors() {
    _teacher = LessonActors.Build(_root, "DGTeacher", "NpcVisuals/TessVisual", 0.5f,
      DiscoveryBuilder.TeacherStart, new Color(0.25f, 0.45f, 0.85f),
      new Color(0.98f, 0.78f, 0.25f));
    _student = LessonActors.Build(_root, "DGStudent", "NpcVisuals/MiloVisual", 0.42f,
      DiscoveryBuilder.StudentStart, new Color(0.30f, 0.62f, 0.45f),
      new Color(0.55f, 0.35f, 0.20f));
    _voice = new PacedVoice();
    _voice.Audio = _audio;
    _voice.LogTag = "Discovery";
    // The student's own voice (Milo's roster profile, passed by the installer),
    // P4 so his discovery line never cuts the teacher.
    _studentVoice = new PacedVoice();
    _studentVoice.Audio = _audio;
    _studentVoice.LogTag = "DiscoveryMilo";
    if (!string.IsNullOrEmpty(_studentVoiceId))
      _studentVoice.Voice = new VoiceProfileId(_studentVoiceId);
    if (_teacher != null && _teacher.Root != null) FaceSnap(_teacher, BoardLocal());
    if (_student != null && _student.Root != null) FaceSnap(_student, TeacherLocal());
    if (_builder != null && _builder.Result != null) _builder.Result.SetActive(false);
    if (_builder != null && _builder.ExitCue != null) _builder.ExitCue.SetActive(false);
  }

  // Round 0: core candidates only. Round 1: the extra distractors wake up —
  // more searching, same garden (brief §13: difficulty = observation).
  void ApplyRound() {
    for (int i = 0; i < _items.Count; i++) {
      DiscoveryItem item = _items[i];
      if (item == null) continue;
      bool active = !item.IsExtra || Round >= 1;
      item.gameObject.SetActive(active);
      if (!active) item.SetFound(false);
    }
  }

  // The reference board shows ONE icon: the current task's kind.
  void ApplyTaskIcon() {
    if (_builder == null || _builder.TaskIcons == null) return;
    int idx = Mathf.Min(TaskIndex, DiscoveryBuilder.TaskCount - 1);
    for (int i = 0; i < _builder.TaskIcons.Length; i++) {
      GameObject icon = _builder.TaskIcons[i];
      if (icon == null) continue;
      bool on = i == idx;
      if (icon.activeSelf != on) icon.SetActive(on);
      if (on) {
        try { GameJuice.Pop(icon.transform, 0.22f, 0.4f); } catch (Exception) { }
      }
    }
  }

  // Terminal adopt: the finished picture (all three icons lit on the result
  // board, every task-kind item found, exit cue lit, actors observing).
  void ApplyCompletedState(string reason) {
    Current = Phase.Success;
    TaskIndex = DiscoveryBuilder.TaskCount;
    FoundCount = DiscoveryBuilder.TaskCount;
    _followHanded = true;
    _cameraDone = true;
    _advanceT = -1f;
    _successT = -1f;
    _victoryT = -1f;
    if (_teacher != null && _teacher.Root != null) {
      _teacher.Root.transform.localPosition = DiscoveryBuilder.TeacherStart;
      FaceSnap(_teacher, FieldLocal());
    }
    if (_student != null && _student.Root != null) {
      _student.Root.transform.localPosition = DiscoveryBuilder.StudentReturn;
      FaceSnap(_student, FieldLocal());
    }
    for (int i = 0; i < _items.Count; i++) {
      DiscoveryItem item = _items[i];
      if (item == null) continue;
      bool taskKind = IsTaskKind(item.Kind);
      item.SetFound(taskKind);
    }
    ApplyTaskIcon();
    ShowResultAll();
    if (_exitCue != null) _exitCue.SetActive(true);
    if (_player != null) _lastPos = _player.position;
    try { Debug.Log("[DiscoveryGame] adopted COMPLETED state (" + reason + ").", this); } catch (Exception) { }
  }

  static bool IsTaskKind(DiscoveryItem.ItemKind kind) {
    for (int i = 0; i < DiscoveryBuilder.TaskKinds.Length; i++)
      if (DiscoveryBuilder.TaskKinds[i] == kind) return true;
    return false;
  }

  void Update() { Tick(Time.deltaTime); }

  // Deterministic tick (EditMode cover: no live frame needed). Owns the item
  // ticks too (single owner — items have no self Update).
  public void Tick(float dt) {
    if (dt <= 0f || _builder == null) return;
    try {
      TickVoice(dt);
      for (int i = 0; i < _items.Count; i++) {
        if (_items[i] != null && _items[i].gameObject.activeSelf) _items[i].TickForTests(dt);
      }
      switch (Current) {
        case Phase.Intro: TickIntro(dt); break;
        case Phase.Demo: TickDemo(dt); break;
        case Phase.Handoff: TickHandoff(dt); break;
        case Phase.Tasks: TickTasks(dt); break;
        case Phase.Success: TickSuccess(dt); break;
      }
      TickActing(dt);
      TickCamera(dt);
      TickJuice(dt);
    } catch (Exception) { }
  }

  void TickVoice(float dt) {
    if (_voice != null) _voice.Tick(dt);
    if (_studentVoice != null) _studentVoice.Tick(dt);
  }
  void To(Phase next) { Current = next; _phaseT = 0f; }

  // ---- teacher intro ----------------------------------------------------------
  // The board holds the task (apple first): every line is composed from it.

  void TickIntro(float dt) {
    _phaseT += dt;
    float t = _phaseT;
    FaceTowards(_teacher, BoardLocal(), dt, 4f);
    FaceTowards(_student, TeacherLocal(), dt, 3f);
    if (!_saidBoard && t >= 1.2f) {
      _saidBoard = true;
      Wave(_teacher);
      Say("Look at the board!", "Nhìn lên bảng nhé!");
      Point(_teacher, BoardWorld(), 2.4f);
    }
    if (!_saidTask && t >= 3.4f) {
      _saidTask = true;
      Say(TaskLineEn(0), TaskLineVi(0));
      ActivityFeedback.Objective(DialogueLang.T(TaskLineEn(0), TaskLineVi(0)));
      ActivityFeedback.Progress(0, DiscoveryBuilder.TaskCount);
      Point(_teacher, BoardWorld(), 2.2f);
    }
    if (!_saidExplore && t >= 5.8f) {
      _saidExplore = true;
      Wave(_teacher);
      Say("Let's explore!", "Cùng khám phá nhé!");
    }
    if (t >= 7.4f) {
      To(Phase.Demo);
      SetShot(1);
      FaceTowards(_student, DiscoveryBuilder.ForkLocal, dt, 4f);
    }
  }

  // ---- the student's REAL search ------------------------------------------------
  // The demo may never walk straight to the target (brief §9): it forks to the
  // flower bed, checks toward the butterfly bush (both recorded as DemoChecks),
  // and only THEN notices the apple. Sequence is short and readable.
  void TickDemo(float dt) {
    _phaseT += dt;
    if (!_saidWatch) {
      _saidWatch = true;
      Say("Watch your friend!", "Xem bạn làm nhé!");
      Point(_teacher, DiscoveryBuilder.ForkLocal, 2.4f);
    }
    switch (_demoStage) {
      case 0:
        FaceTowards(_student, DiscoveryBuilder.ForkLocal, dt, 5f);
        if (_phaseT >= 1.0f && WalkTo(_student, DiscoveryBuilder.ForkLocal, dt)) {
          _demoStage = 1;
          _demoHoldT = 0.2f;
        }
        break;
      case 1: // check the flower bed (not the target)
        FaceTowards(_student, DiscoveryBuilder.FlowerBedPos, dt, 5f);
        if (!_saidWhere && _phaseT >= 1.4f) {
          _saidWhere = true;
          SayStudent("Where is it?", "Nó ở đâu nhỉ?");
        }
        if (WalkTo(_student, DiscoveryBuilder.FlowerBedPos + new Vector3(0f, 0f, -0.9f), dt)) {
          _demoHoldT -= dt;
          if (_demoHoldT <= 0f) {
            _demoChecks++;
            _demoStage = 2;
            _demoHoldT = 0.7f;
          }
        } else {
          _demoHoldT = 0.8f;
        }
        break;
      case 2: // check toward the butterfly bush (not the target)
        FaceTowards(_student, DiscoveryBuilder.ButterflyBushPos, dt, 5f);
        if (WalkTo(_student, DiscoveryBuilder.ButterflyBushPos + new Vector3(-0.9f, 0f, -0.9f), dt)) {
          _demoHoldT -= dt;
          if (_demoHoldT <= 0f) {
            _demoChecks++;
            _demoStage = 3;
          }
        } else {
          _demoHoldT = 0.7f;
        }
        break;
      case 3: { // NOW notice the apple and discover it
        DiscoveryItem apple = FindItem(DiscoveryItem.ItemKind.Apple);
        if (apple == null) { _demoStage = 4; _demoHoldT = 1.0f; break; }
        FaceTowards(_student, apple.transform.localPosition, dt, 5f);
        if (WalkTo(_student, apple.transform.localPosition + new Vector3(0f, 0f, -0.6f), dt)) {
          _demoFound = true;
          apple.SetFound(true);
          PlaySfx("found");
          SayStudent("I found it!", "Con tìm thấy rồi!");
          Sparkle(apple.transform.position + new Vector3(0f, 0.4f, 0f), 10, 77, 0.45f);
          _demoStage = 4;
          _demoHoldT = DemoHold;
        }
        break;
      }
      default:
        FaceTowards(_student, appleWorld(), dt, 3f);
        _demoHoldT -= dt;
        if (!_saidYes && _demoHoldT <= DemoHold * 0.4f) {
          _saidYes = true;
          Say("Yes! The apple!", "Đúng rồi! Quả táo!");
          Point(_teacher, appleWorld(), 2.0f);
          CelebrateActor(_student, soft: true);
        }
        if (_demoHoldT <= 0f) {
          TidyDemoFind();
          To(Phase.Handoff);
        }
        break;
    }
  }

  void TidyDemoFind() {
    DiscoveryItem apple = FindItem(DiscoveryItem.ItemKind.Apple);
    if (apple != null && apple.State == DiscoveryItem.ItemState.Found) apple.SetFound(false);
  }

  Vector3 appleWorld() {
    DiscoveryItem apple = FindItem(DiscoveryItem.ItemKind.Apple);
    return apple != null ? apple.transform.position : _root.position;
  }

  DiscoveryItem FindItem(DiscoveryItem.ItemKind kind) {
    for (int i = 0; i < _items.Count; i++) {
      DiscoveryItem it = _items[i];
      if (it != null && it.Kind == kind && it.gameObject.activeSelf) return it;
    }
    return null;
  }

  // ---- handoff ------------------------------------------------------------------

  void TickHandoff(float dt) {
    _phaseT += dt;
    float t = _phaseT;
    if (!_saidTurn && t >= 0.4f) {
      _saidTurn = true;
      FaceTowards(_teacher, PlayerLocal(), dt, 5f);
      Say("Now it's your turn!", "Giờ đến lượt con!");
    }
    if (!_saidGo && t >= 2.4f) {
      _saidGo = true;
      Say(TaskLineEn(0), TaskLineVi(0));
      Point(_teacher, BoardWorld(), 2.4f);
    }
    if (!_studentReturned) {
      if (WalkTo(_student, DiscoveryBuilder.StudentReturn, dt)) _studentReturned = true;
      if (t >= 8.0f) _studentReturned = true; // safety: the lesson never stalls
    } else {
      FaceTowards(_student, FieldLocal(), dt, 2.5f);
    }
    if (!_followHanded && t >= 4.2f) {
      _followHanded = true;
      Follow();
      if (_life != null) { try { _life.Begin("handoff done"); } catch (Exception) { } }
    }
    if (_followHanded && Current == Phase.Handoff && (_studentReturned || t >= 8.0f)) {
      To(Phase.Tasks);
      TaskIndex = 0;
      FoundCount = 0;
      ApplyTaskIcon();
      ActivityFeedback.Objective(DialogueLang.T(TaskLineEn(0), TaskLineVi(0)));
      ActivityFeedback.Progress(0, DiscoveryBuilder.TaskCount);
      _lastPos = _player != null ? _player.position : Vector3.zero;
      try { Debug.Log("[DiscoveryGame] child control (search phase).", this); } catch (Exception) { }
    }
  }

  // ---- the child's search ---------------------------------------------------------

  public void TryInteract(DiscoveryItem item) {
    if (item == null || !item.IsAvailable) return;
    if (Current != Phase.Tasks) return; // intro/demo/success own the stage
    if (!item.gameObject.activeSelf) return;
    if (item.Kind == CurrentTaskKind) HandleFound(item);
    else HandleWrong(item);
  }

  void HandleFound(DiscoveryItem item) {
    item.SetFound(true);
    PlaySfx("found");
    GameJuice.CorrectFx(_fx, item.transform.position + new Vector3(0f, 0.4f, 0f), true);
    ActivityFeedback.Correct();
    ActivityFeedback.Progress(FoundCount + 1, DiscoveryBuilder.TaskCount);
    Sparkle(item.transform.position + new Vector3(0f, 0.45f, 0f), 10, 300 + FoundCount, 0.45f);
    Say(PraiseEn(CurrentTaskKind), PraiseVi(CurrentTaskKind));
    if (_viz != null) {
      _viz.FaceTowards(item.transform.position, true);
      _viz.PlayPickup();
    }
    CelebrateActor(_student, soft: true);
    FoundCount++;
    if (FoundCount >= DiscoveryBuilder.TaskCount) {
      _successT = SuccessDelay;
      Log("all tasks found (wrong=" + WrongAttempts + ")");
    } else {
      _advanceT = NextTaskDelay;
    }
  }

  void HandleWrong(DiscoveryItem item) {
    WrongAttempts++;
    GameJuice.WrongFx(item.transform, _fx, item.transform.position);
    if (_wrongT > 0f) return; // cooldown: the poll/click cannot spam the line
    _wrongT = WrongCooldown;
    ActivityFeedback.Retry();
    Say("Not that one. Keep looking!", "Chưa đúng. Con tìm tiếp nhé!");
    Log("wrong candidate (kind=" + item.Kind + " wanted=" + CurrentTaskKind + ")");
  }

  void TickTasks(float dt) {
    _phaseT += dt;
    if (_wrongT > 0f) _wrongT -= dt;
    if (_successT > 0f) {
      _successT -= dt;
      if (_successT <= 0f) SuccessBeats();
      return;
    }
    if (_advanceT > 0f) {
      _advanceT -= dt;
      if (_advanceT <= 0f) AdvanceTask();
    }
    FaceTowards(_teacher, PlayerLocal(), dt, 2.2f);
    FaceTowards(_student, PlayerLocal(), dt, 2.2f);
  }

  void AdvanceTask() {
    if (TaskIndex >= DiscoveryBuilder.TaskCount - 1) return;
    TaskIndex++;
    ApplyTaskIcon();
    ActivityFeedback.Objective(DialogueLang.T(TaskLineEn(TaskIndex), TaskLineVi(TaskIndex)));
    Say("Now find the " + KindEn(CurrentTaskKind) + "!", "Giờ tìm " + KindVi(CurrentTaskKind) + " nhé!");
    Point(_teacher, BoardWorld(), 2.2f);
    Log("task " + (TaskIndex + 1) + " -> " + CurrentTaskKind);
  }

  void SuccessBeats() {
    To(Phase.Success);
    PlaySfx("success");
    GameJuice.CorrectFx(_fx, FlowerBedWorld() + new Vector3(0f, 0.8f, 0f), true);
    ActivityFeedback.Correct();
    ActivityFeedback.Progress(DiscoveryBuilder.TaskCount, DiscoveryBuilder.TaskCount);
    Say("You found them all! Well done!", "Tìm thấy hết rồi! Giỏi!");
    // The way-home line waits its turn in the pacer's single slot.
    _recapEn.Clear();
    _recapVi.Clear();
    _recapEn.Enqueue(DialogueLang.T("Time to go home!", "Mình ra cổng nhé!"));
    _recapVi.Enqueue(DialogueLang.T("Time to go home!", "Mình ra cổng nhé!"));
    ShowResultAll();
    if (_exitCue != null) _exitCue.SetActive(true);
    Point(_teacher, BoardWorld(), 2.0f);
    CelebrateBoth();
    SetShot(2);
    _victoryT = 1.35f;
    MarkLifeCompleted();
    if (_onCompleted != null) {
      try { _onCompleted(DiscoveryBuilder.TaskCount); } catch (Exception) { }
    }
  }

  // Result board: all three task icons light up (the reward picture).
  void ShowResultAll() {
    if (_result != null) {
      _result.SetActive(true);
      _result.transform.localScale = Vector3.one * 0.65f;
      _resultPopT = 0f;
    }
    if (_builder != null && _builder.ResultSlots != null) {
      for (int i = 0; i < _builder.ResultSlots.Length; i++) {
        if (_builder.ResultSlots[i] != null) _builder.ResultSlots[i].SetActive(true);
      }
    }
  }

  void MarkLifeCompleted() {
    if (_life == null) return;
    try {
      if (_life.State != ActivityState.Completed) _life.MarkCompleted("found all discovery tasks");
    } catch (Exception) { }
  }

  void TickSuccess(float dt) {
    _phaseT += dt;
    TickRecap();
    if (_exitCue != null && _exitCue.activeSelf) {
      float s = 1f + 0.1f * Mathf.Sin(Time.time * 3.2f);
      try { _exitCue.transform.localScale = new Vector3(0.5f * s, 0.5f * s, 0.14f); }
      catch (Exception) { }
    }
    FaceTowards(_teacher, PlayerLocal(), dt, 2f);
    FaceTowards(_student, PlayerLocal(), dt, 2f);
    if (_phaseT >= 4.6f && !_followHanded) {
      _followHanded = true;
      _cameraDone = true;
      Follow();
    }
  }

  void TickRecap() {
    if (_recapEn.Count <= 0 || _voice == null || !_voice.Idle || _voice.HasLine) return;
    Say(_recapEn.Dequeue(), _recapVi.Dequeue());
  }

  void OnDestroy() {
    try { ActivityFeedback.Clear(); } catch (Exception) { }
  }

  // ---- camera -------------------------------------------------------------------------

  void SetShot(int shot) {
    if (_shot == shot) { IssueShot(); return; }
    _shot = shot;
    IssueShot();
  }

  void IssueShot() {
    if (_cam == null) return;
    Transform c, l;
    if (_shot == 2) { c = _camSuccess; l = _lookSuccess; }
    else if (_shot == 1) { c = _camDemo; l = _lookDemo; }
    else { c = _camTeaching; l = _lookTeaching; }
    if (c == null || l == null) return;
    _shotIssued = true;
    _shotT = 2.4f;
    try { _cam.FrameAnchor(c, l, 2.8f); } catch (Exception) { }
  }

  void TickCamera(float dt) {
    if (_cameraDone) return;
    if (_followHanded && Current != Phase.Success) return;
    if (!_shotIssued) {
      // Let the area's arrival reveal (2.2s) play first, then hold the
      // teaching frame while the teacher shows the task.
      if (Current == Phase.Intro && _phaseT >= 2.0f) IssueShot();
      return;
    }
    if (Current != Phase.Intro && Current != Phase.Demo && Current != Phase.Success) return;
    _shotT -= dt;
    if (_shotT <= 0f) IssueShot();
  }

  void Follow() {
    if (_cam == null || _player == null) return;
    try { _cam.Follow(_player, FollowOffset); } catch (Exception) { }
    try { Debug.Log("[DiscoveryGame] camera returned to follow.", this); } catch (Exception) { }
  }

  // ---- lines (all target-composed, all inside the NPC token cap) ---------------------

  static string KindEn(DiscoveryItem.ItemKind kind) {
    switch (kind) {
      case DiscoveryItem.ItemKind.Apple: return "apple";
      case DiscoveryItem.ItemKind.Flower: return "flower";
      case DiscoveryItem.ItemKind.Butterfly: return "butterfly";
      case DiscoveryItem.ItemKind.Ball: return "ball";
      case DiscoveryItem.ItemKind.Leaf: return "leaf";
      default: return "mushroom";
    }
  }

  static string KindVi(DiscoveryItem.ItemKind kind) {
    switch (kind) {
      case DiscoveryItem.ItemKind.Apple: return "quả táo";
      case DiscoveryItem.ItemKind.Flower: return "bông hoa";
      case DiscoveryItem.ItemKind.Butterfly: return "con bướm";
      case DiscoveryItem.ItemKind.Ball: return "quả bóng";
      case DiscoveryItem.ItemKind.Leaf: return "chiếc lá";
      default: return "cây nấm";
    }
  }

  static string TaskLineEn(int taskIndex) {
    return "Find the " + KindEn(DiscoveryBuilder.TaskKinds[
      Mathf.Min(taskIndex, DiscoveryBuilder.TaskCount - 1)]) + "!";
  }

  static string TaskLineVi(int taskIndex) {
    return "Tìm " + KindVi(DiscoveryBuilder.TaskKinds[
      Mathf.Min(taskIndex, DiscoveryBuilder.TaskCount - 1)]) + " nhé!";
  }

  static string PraiseEn(DiscoveryItem.ItemKind kind) {
    switch (kind) {
      case DiscoveryItem.ItemKind.Apple: return "Yes! The apple!";
      case DiscoveryItem.ItemKind.Flower: return "Yes! The flower!";
      default: return "Yes! The butterfly!";
    }
  }

  static string PraiseVi(DiscoveryItem.ItemKind kind) {
    switch (kind) {
      case DiscoveryItem.ItemKind.Apple: return "Đúng rồi! Quả táo!";
      case DiscoveryItem.ItemKind.Flower: return "Đúng rồi! Bông hoa!";
      default: return "Đúng rồi! Con bướm!";
    }
  }

  // ---- actor motion / gestures (same acting language as #2-#6) ----------------------

  bool WalkTo(LessonActor a, Vector3 targetLocal, float dt) {
    if (a == null || a.Root == null) return true;
    Vector3 p = a.Root.transform.localPosition;
    Vector3 flat = new Vector3(targetLocal.x - p.x, 0f, targetLocal.z - p.z);
    float dist = flat.magnitude;
    if (dist <= 0.12f) return true;
    FaceTowards(a, targetLocal, dt, 6f);
    float step = Mathf.Min(WalkSpeed * dt, dist);
    Vector3 dir = dist > 0.0001f ? flat / dist : Vector3.zero;
    a.Root.transform.localPosition = new Vector3(p.x + dir.x * step, p.y, p.z + dir.z * step);
    return false;
  }

  void FaceTowards(LessonActor a, Vector3 targetLocal, float dt, float rate) {
    if (a == null || a.Root == null) return;
    try {
      Vector3 p = a.Root.transform.localPosition;
      Vector3 d = new Vector3(targetLocal.x - p.x, 0f, targetLocal.z - p.z);
      if (d.sqrMagnitude < 0.0001f) return;
      Quaternion want = Quaternion.LookRotation(d);
      if (dt <= 0f) { a.Root.transform.localRotation = want; return; }
      a.Root.transform.localRotation = Quaternion.Slerp(a.Root.transform.localRotation, want,
        1f - Mathf.Exp(-rate * dt));
    } catch (Exception) { }
  }

  void FaceSnap(LessonActor a, Vector3 dir) {
    if (a == null || a.Root == null) return;
    try {
      Vector3 d = new Vector3(dir.x, 0f, dir.z);
      if (d.sqrMagnitude < 0.0001f) return;
      a.Root.transform.localRotation = Quaternion.LookRotation(d);
    } catch (Exception) { }
  }

  void Wave(LessonActor a) { if (a != null) a.WaveT = 1.4f; }

  void Point(LessonActor a, Vector3 worldTarget, float seconds) {
    if (a == null || _root == null) return;
    a.PointTarget = LocalPoint(worldTarget);
    a.PointT = Mathf.Max(0.4f, seconds);
  }

  void CelebrateActor(LessonActor a, bool soft) {
    if (a == null) return;
    try { if (a.Animator != null) a.Animator.SetTrigger("Celebrate"); } catch (Exception) { }
    if (a.Face != null) a.Face.PulseExpression(CharacterExpression.Happy, 3f);
    try { a.Face.PlayHop(); } catch (Exception) { }
    a.SquashT = 0.35f;
  }

  void CelebrateBoth() {
    CelebrateActor(_teacher, soft: false);
    CelebrateActor(_student, soft: false);
  }

  void TickActing(float dt) {
    TickWaveOne(_teacher, dt);
    TickWaveOne(_student, dt);
    TickPointOne(_teacher, dt);
    TickPointOne(_student, dt);
    TickSquash(_teacher, dt);
    TickSquash(_student, dt);
  }

  void TickWaveOne(LessonActor a, float dt) {
    if (a == null || a.WaveBone == null) return;
    try {
      if (a.WaveT > 0f) {
        if (!a.Waving) { a.Waving = true; a.WaveBase = a.WaveBone.localRotation; }
        a.WaveT -= dt;
        float wave = Mathf.Sin(Time.time * 14f) * 18f;
        a.WaveBone.localRotation = a.WaveBase * Quaternion.Euler(0f, 0f, -75f + wave);
        if (a.WaveT <= 0f) { a.Waving = false; a.WaveBone.localRotation = a.WaveBase; }
      }
    } catch (Exception) { }
  }

  void TickPointOne(LessonActor a, float dt) {
    if (a == null || a.Root == null || a.PointT <= 0f) return;
    try {
      a.PointT = Mathf.Max(0f, a.PointT - dt);
      FaceTowards(a, a.PointTarget, dt, 5f);
      if (a.WaveBone != null) {
        if (!a.Waving) { a.Waving = true; a.WaveBase = a.WaveBone.localRotation; }
        a.WaveBone.localRotation = a.WaveBase * Quaternion.Euler(0f, 0f, -68f);
      }
      if (a.PointT <= 0f && a.WaveBone != null) {
        a.WaveBone.localRotation = a.WaveBase;
        a.Waving = false;
      }
    } catch (Exception) { }
  }

  void TickSquash(LessonActor a, float dt) {
    if (a == null || a.Visual == null || a.SquashT <= 0f) return;
    try {
      a.SquashT = Mathf.Max(0f, a.SquashT - dt);
      float k = a.SquashT / 0.35f;
      float bulge = Mathf.Sin(k * Mathf.PI);
      Vector3 baseS = a.BaseScale;
      a.Visual.localScale = new Vector3(baseS.x * (1f - 0.11f * bulge), baseS.y * (1f + 0.16f * bulge),
        baseS.z * (1f - 0.11f * bulge));
      if (a.SquashT <= 0f) a.Visual.localScale = baseS;
    } catch (Exception) { }
  }

  // ---- juice / cues ---------------------------------------------------------------------

  void TickJuice(float dt) {
    if (_result != null && _result.activeSelf && _resultPopT < 1f) {
      _resultPopT = Mathf.Min(1f, _resultPopT + dt / 0.3f);
      float s = Mathf.Lerp(0.65f, 1f, Mathf.SmoothStep(0f, 1f, _resultPopT));
      try { _result.transform.localScale = Vector3.one * s; } catch (Exception) { }
    }
    if (_victoryT > 0f) {
      _victoryT -= dt;
      if (_victoryT <= 0f && _viz != null && !IsPlayerMoving()) {
        _victoryT = -1f;
        try { _viz.PlayVictory(); } catch (Exception) { }
      }
    }
  }

  bool IsPlayerMoving() {
    if (_mover != null) return _mover.IsMoving;
    if (_player == null) return false;
    Vector3 d = _player.position - _lastPos;
    d.y = 0f;
    return d.sqrMagnitude > 0.0004f;
  }

  void Say(string en, string vi) {
    if (_voice == null) return;
    _voice.Speak(DialogueLang.T(en, vi));
  }

  // The student's line (his own voice, P4 so it never cuts the teacher).
  void SayStudent(string en, string vi) {
    if (_studentVoice == null) return;
    try {
      _studentVoice.Speak(DialogueLang.T(en, vi), SpeechStyle.Excited, AudioPriority.P4_Feedback);
    } catch (Exception) { }
  }

  void PlaySfx(string id) {
    if (_audio == null) return;
    try { _audio.PlaySfx(new SfxId(id)); } catch (Exception) { }
  }

  void Sparkle(Vector3 world, int count, int seed, float radius) {
    if (_fx == null) return;
    try { DemoJuice.Sparkle(_fx, _fx.InverseTransformPoint(world), count, seed, radius); }
    catch (Exception) { }
  }

  void Log(string message) {
    try { Debug.Log("[DiscoveryGame] " + message, this); } catch (Exception) { }
  }

  // ---- helpers -------------------------------------------------------------------------------

  public bool PlayerNear(Vector3 world, float radius) {
    if (_player == null) return false;
    Vector3 p = _player.position;
    float dx = p.x - world.x, dz = p.z - world.z;
    return dx * dx + dz * dz <= radius * radius;
  }

  Vector3 LocalPoint(Vector3 world) {
    return _root != null ? _root.InverseTransformPoint(world) : world;
  }

  Vector3 BoardWorld() {
    return _builder != null && _builder.ReferenceBoard != null
      ? _builder.ReferenceBoard.transform.position : transform.position;
  }
  Vector3 BoardLocal() { return LocalPoint(BoardWorld()); }

  Vector3 FieldLocal() { return new Vector3(0f, 0f, 2.6f); }
  Vector3 FlowerBedWorld() {
    return _root != null ? _root.TransformPoint(DiscoveryBuilder.FlowerBedPos) : Vector3.zero;
  }

  Vector3 PlayerLocal() {
    return _player != null ? LocalPoint(_player.position) : FieldLocal();
  }
  Vector3 TeacherLocal() {
    return _teacher != null && _teacher.Root != null
      ? _teacher.Root.transform.localPosition + new Vector3(0f, 1.1f, 0f)
      : LocalPoint(DiscoveryBuilder.TeacherStart);
  }
}
