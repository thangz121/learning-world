// A_World/CountingGarden/RabbitFeed.cs — GAMEPLAY #3 "CHO THỎ ĂN ĐÚNG SỐ"
// (feed the rabbit the right number of carrots). The Counting Garden's carrot
// patch (zone 0) activity, staged in its OWN lazy scene (RabbitPlayScene).
// The experience: the teacher links the TARGET NUMBER to that many carrots at
// the board -> the child student fetches them one by one and feeds them to the
// bunny while the teacher counts 1..N -> handover ("Now it's your turn!") ->
// the CHILD walks, picks, carries and feeds: one carrot = one count; feeding
// past the target is guidance, never failure, and stopping short earns a
// gentle "how many more" nudge, never a fail.
// One patch (MAXIMUM = 9, brief §12), one mechanic, many targets: the target
// only decides how many carrots the bunny gets (progression owned by the area:
// 3 -> 4 -> 5 -> 6 -> 7 -> 8 -> 9 -> 1 -> 2). No confetti on success: glow +
// sound + NPC reaction only (same discipline as gameplay #2).
// Architecture: scene-local components, no manager/singleton, no new
// bus/service; reuses LessonActors (body kit shared with #1/#2), PacedVoice
// (paced speech), DemoJuice (FX), SmartCamera beats, ActivityLifecycle (owned
// by the Math-side area), MicroWorldPortal (the door). C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RabbitCarrot : MonoBehaviour, IClickTarget {
  public enum CarrotState { Available, Picked, Carried, Delivered, Consumed, Removed }

  public CarrotState State { get; private set; } = CarrotState.Available;
  public Vector3 HomeLocal;
  public RabbitFeed Game;
  // Fired the moment the feed flight settles at the rabbit's mouth. The game
  // hangs the munch + the counting/celebration beats on it, so feedback
  // follows the REAL moment the carrot arrives — not the click that started it.
  public Action OnMunched;

  Transform _hand;
  Vector3 _flyFrom, _flyTo;
  float _flyT, _flyDur, _flyLift;
  bool _flying;
  bool _returning;
  float _bounceT = 1f;
  Vector3 _baseScale = Vector3.one;
  Collider _collider;

  // Pickup/feed ACTIONS (same discipline as gameplay #1): the carrot waits for
  // the child's body to reach it (pick delay = the bend), rides the animated
  // fist while the child walks, then flies the last stretch to the mouth.
  bool _picking;
  float _pickT, _pickDelay, _pickDur;
  Vector3 _pickFrom;
  bool _feeding, _feedActive;
  float _feedT, _feedDelay, _feedDur;
  Vector3 _feedFrom, _feedTo;
  float _feedLift;

  public bool IsAvailable { get { return State == CarrotState.Available; } }
  public bool IsFlying { get { return _flying; } }

  public void Bind(RabbitFeed game, Transform hand) {
    Game = game;
    _hand = hand;
    _baseScale = transform.localScale;
    _collider = GetComponent<Collider>();
  }

  public void SetHand(Transform hand) { _hand = hand; }

  public void OnClicked() {
    if (State != CarrotState.Available || Game == null) return;
    Game.TryPick(this);
  }

  // Pickup: the child bends (player PickUp clip); once the hand is down, the
  // carrot arcs up into it and rides the fist. No ground->hand snap.
  public void BeginCarry(float delay = 0f) {
    if (State != CarrotState.Available) return;
    State = CarrotState.Picked;
    if (_collider != null) _collider.enabled = false;
    _flying = false;
    _returning = false;
    _feeding = false;
    _feedActive = false;
    _picking = true;
    _pickT = 0f;
    _pickDelay = Mathf.Max(0f, delay);
    _pickDur = 0.42f;
    _pickFrom = transform.localPosition;
  }

  // Feed: the carrot keeps riding the fist while the child reaches toward the
  // rabbit, then flies the last stretch to the mouth with a small lift.
  public void BeginFeed(Vector3 mouthLocal, float delay = 0f) {
    if (State != CarrotState.Carried) return;
    State = CarrotState.Delivered;
    _picking = false;
    _feeding = true;
    _feedActive = false;
    _feedT = 0f;
    _feedDelay = Mathf.Max(0f, delay);
    _feedDur = 0.38f;
    _feedLift = 0.22f;
    _feedTo = mouthLocal;
  }

  // Correction path: the extra carrot leaves the mouth and returns home.
  public void BeginReturnHome() {
    State = CarrotState.Carried; // re-uses the flight while it travels
    _picking = false;
    _feeding = false;
    _feedActive = false;
    _returning = true;
    _flyFrom = transform.localPosition;
    _flyTo = HomeLocal;
    _flyT = 0f;
    _flyDur = 0.6f;
    _flyLift = 0.8f;
    _flying = true;
  }

  public void ParkInBowl(Vector3 slotLocal) {
    State = CarrotState.Consumed;
    _flying = false;
    _picking = false;
    _feeding = false;
    _feedActive = false;
    transform.localPosition = slotLocal;
    gameObject.SetActive(true);
  }

  public void MarkRemoved() {
    State = CarrotState.Removed;
    if (_collider != null) _collider.enabled = false;
  }

  public void ResetHome() {
    State = CarrotState.Available;
    _flying = false;
    _picking = false;
    _feeding = false;
    _feedActive = false;
    _returning = false;
    transform.localPosition = HomeLocal;
    gameObject.SetActive(true);
    if (_collider != null) _collider.enabled = true;
  }

  // Deterministic tick — owned by RabbitFeed.Tick (single owner, no self
  // Update, so live play and EditMode advance flights exactly once).
  public void TickForTests(float dt) {
    try {
      if (_picking) { TickPick(dt); return; }
      if (_feeding) { TickFeed(dt); return; }
      if (_flying) { TickFlight(dt); return; }
      if (State == CarrotState.Carried && _hand != null) FollowHand(dt);
      TickBounceAndPulse(dt);
    } catch (Exception) {
      _picking = false;
      _feeding = false;
      _flying = false;
    }
  }

  void TickPick(float dt) {
    _pickT += dt;
    if (_pickT < _pickDelay) return; // the hand is still on its way down
    float t = Mathf.Clamp01((_pickT - _pickDelay) / _pickDur);
    Vector3 handLocal = HandLocal();
    Vector3 mid = (_pickFrom + handLocal) * 0.5f + new Vector3(0f, 0.35f, 0f);
    transform.localPosition = Vector3.Lerp(
      Vector3.Lerp(_pickFrom, mid, t), Vector3.Lerp(mid, handLocal, t), t);
    if (t >= 1f) {
      _picking = false;
      State = CarrotState.Carried;
      _bounceT = 0f;
    }
  }

  void TickFeed(float dt) {
    _feedT += dt;
    if (_feedT < _feedDelay) {
      if (_hand != null) FollowHand(dt);
      return;
    }
    if (!_feedActive) {
      _feedActive = true;
      _feedFrom = transform.localPosition;
      _flyFrom = _feedFrom;
      _flyTo = _feedTo;
      _flyT = 0f;
      _flyDur = _feedDur;
      _flyLift = _feedLift;
      _flying = true;
    }
    TickFlight(dt);
  }

  void FollowHand(float dt) {
    Vector3 want = _hand.position + Vector3.up * 0.02f;
    transform.position = Vector3.Lerp(transform.position, want,
      1f - Mathf.Exp(-16f * dt));
    transform.rotation = Quaternion.Slerp(transform.rotation, _hand.rotation,
      1f - Mathf.Exp(-10f * dt));
  }

  Vector3 HandLocal() {
    if (_hand == null) return _pickFrom;
    return transform.parent != null
      ? transform.parent.InverseTransformPoint(_hand.position)
      : _hand.position;
  }

  void TickFlight(float dt) {
    _flyT += dt;
    float t = Mathf.Clamp01(_flyT / _flyDur);
    Vector3 mid = (_flyFrom + _flyTo) * 0.5f + new Vector3(0f, _flyLift, 0f);
    transform.localPosition = Vector3.Lerp(
      Vector3.Lerp(_flyFrom, mid, t), Vector3.Lerp(mid, _flyTo, t), t);
    if (t < 1f) return;
    _flying = false;
    _bounceT = 0f;
    bool munched = _feedActive;
    _feedActive = false;
    _feeding = false;
    if (_returning) {
      _returning = false;
      State = CarrotState.Available;
      if (_collider != null) _collider.enabled = true;
    }
    if (munched) {
      State = CarrotState.Consumed;
      gameObject.SetActive(false);
      if (OnMunched != null) {
        try { OnMunched(); } catch (Exception) { }
      }
    }
  }

  void TickBounceAndPulse(float dt) {
    if (_bounceT < 1f) {
      _bounceT = Mathf.Min(1f, _bounceT + dt / 0.3f);
      float s = 1f + 0.16f * Mathf.Sin(Mathf.PI * _bounceT);
      transform.localScale = _baseScale * s;
    }
    if (State == CarrotState.Available && Game != null && Game.PlayerNear(transform.position, 2.4f)) {
      float s = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
      transform.localScale = _baseScale * s;
    } else if (State == CarrotState.Available && _bounceT >= 1f) {
      transform.localScale = _baseScale;
    }
  }
}

// The bowl's door: clicking it walks the child up and feeds; simply carrying a
// carrot close to it (and stopping) does the same — one call, state-guarded,
// spam-safe. Proximity is polled by RabbitFeed.Tick (single owner); this
// component is the click hook only.
[DisallowMultipleComponent]
public class FeedZone : MonoBehaviour, IClickTarget {
  public RabbitFeed Game;
  public float feedRadius = 1.5f;

  public void Bind(RabbitFeed game) {
    Game = game;
    // Unconditional (same lesson as gameplay #1: the builder strips colliders
    // for bake safety with deferred Destroy, so a null-check here would leave
    // the bowl click-less).
    BoxCollider box = gameObject.AddComponent<BoxCollider>();
    box.size = new Vector3(1.3f, 0.7f, 1.3f);
    box.center = new Vector3(0f, 0.35f, 0f);
  }

  public void OnClicked() {
    if (Game != null) Game.TryFeed();
  }
}

[DisallowMultipleComponent]
public class RabbitFeed : MonoBehaviour {
  public enum Phase {
    Intro,   // teacher links the board's number to carrots for the bunny
    Demo,    // the student fetches N carrots one by one and feeds the bunny
    Handoff, // "Now it's your turn!" + camera returns to the child
    Feeding, // the child picks, carries and feeds; the teacher counts along
    Success, // the target count is fed — celebrated, field stays open
    Correct, // one too many: a gentle counting correction, then Success again
  }

  // Beat timings (one place; deterministic for Tick tests).
  const float DemoHold = 0.7f;            // teacher count beat per demo carrot
  const float OvershootNagCooldown = 5f;
  const float UnderNudgeCooldown = 8f;
  const float UnderDwell = 2.0f;         // settled-below-target before a nudge
  const float UnderNearXZ = 4.5f;        // nudge only near the patch/bunny
  const float WalkSpeed = 0.85f;         // student legs
  public static readonly Vector3 FollowOffset = RabbitPlayBuilder.FollowOffset;

  // Number words: Vietnamese first; English prepared alongside. Every composed
  // line stays inside the SafetyFilter NPC cap (<= 6 tokens).
  static readonly string[] NumEn = {
    "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
  static readonly string[] NumVi = {
    "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
  static readonly string[] CountEn = {
    "One carrot.", "Two carrots.", "Three carrots.", "Four carrots.",
    "Five carrots.", "Six carrots.", "Seven carrots.", "Eight carrots.",
    "Nine carrots." };
  static readonly string[] CountVi = {
    "Một củ cà rốt.", "Hai củ cà rốt.", "Ba củ cà rốt.", "Bốn củ cà rốt.",
    "Năm củ cà rốt.", "Sáu củ cà rốt.", "Bảy củ cà rốt.", "Tám củ cà rốt.",
    "Chín củ cà rốt." };

  static int ClampN(int i) { return Mathf.Clamp(i, 1, RabbitPlayBuilder.MaxTarget); }
  static string N(int i) { return NumEn[ClampN(i) - 1]; }
  static string Nvi(int i) { return NumVi[ClampN(i) - 1]; }
  static string Carrots(int n) { return ClampN(n) == 1 ? "carrot" : "carrots"; }
  static string Cap(string s) {
    return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
  }

  public Phase Current { get; private set; } = Phase.Intro;
  public int Target { get; private set; } = RabbitPlayBuilder.Target;
  public int Count { get; private set; }
  public RabbitCarrot Carried { get; private set; }
  public int Overshoots { get; private set; }
  public int UndershootNudges { get; private set; }
  public int DemoCarrotsFed { get { return _demoFed; } }
  public bool IntroDone { get { return Current != Phase.Intro; } }
  public bool ResultShown { get { return _result != null && _result.activeSelf; } }
  public int CarrotCount { get { return _carrots.Count; } }
  public RabbitCarrot CarrotAt(int i) { return i >= 0 && i < _carrots.Count ? _carrots[i] : null; }

  RabbitPlayBuilder _builder;
  Transform _root;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PlayerVisual _viz;
  ClickToMove _mover;
  Transform _playerHand;

  LessonActor _teacher;
  LessonActor _student;
  PacedVoice _voice;
  Transform _fx;

  GameObject _board;
  GameObject _result;
  GameObject _rabbit;
  GameObject _rabbitHead;
  GameObject _rabbitEarL;
  GameObject _rabbitEarR;
  GameObject _rabbitBody;
  Vector3 _headBase;
  Quaternion _earLBase;
  Quaternion _earRBase;
  Vector3 _bodyBase;
  Transform _camTeaching, _lookTeaching, _camDemo, _lookDemo, _camSuccess, _lookSuccess;

  readonly List<RabbitCarrot> _carrots = new List<RabbitCarrot>();
  readonly List<RabbitCarrot> _fed = new List<RabbitCarrot>();

  public float PickDelay = 0.45f;
  public float FeedDelay = 0.5f;

  float _phaseT;
  float _shotT = -1f;
  bool _shotIssued;
  bool _cameraDone;
  bool _followHanded;
  int _shot;

  bool _saidBoard, _saidNumber, _saidCountWord, _saidToday, _saidFeed;
  int _demoStage; // 0 walk to patch, 1 picking, 2 walk to bunny, 3 feeding, 4 hold, 5 confirm
  int _demoFed;
  float _demoHoldT;
  bool _saidWatch, _saidYes;
  bool _saidTurn, _saidTask;
  bool _studentReturned;
  float _victoryT = -1f;
  float _resultPopT = 1f;
  float _boardPulseT;
  Vector3 _lastPos;
  readonly Queue<string> _recapEn = new Queue<string>();
  readonly Queue<string> _recapVi = new Queue<string>();

  // Correction beats (timed, deterministic; no coroutines so tests can tick).
  int _correctStep = -1;
  float _correctT;
  RabbitCarrot _extra;
  int _landBeats; // what the NEXT munch means (count line vs success)
  float _overshootNagT;
  float _underCooldownT;
  float _underT;

  // Rabbit life: nibble bursts + ear twitches + breathing (procedural, small).
  float _nibbleT;
  float _earTwitchT = 3f;
  float _twitchT;
  float _breatheT;
  int _sparkleSeed = 900;
  readonly float[] _pipPopT = { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
  int _lastPip = -1;

  public int PipCount { get; private set; }

  public void Build(RabbitPlayBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life, int target = 0) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    Target = RabbitPlayBuilder.ClampTarget(target <= 0 ? RabbitPlayBuilder.Target : target);
    _viz = player != null ? player.GetComponent<PlayerVisual>() : null;
    _mover = player != null ? player.GetComponent<ClickToMove>() : null;
    _playerHand = player;
    if (_builder == null) {
      Debug.LogWarning("[RabbitFeed] no builder; activity parked.", this);
      return;
    }
    _root = _builder.transform;
    _board = _builder.NumberBoard;
    _result = _builder.Result;
    _rabbit = _builder.RabbitRoot;
    _rabbitHead = _builder.RabbitHead;
    _rabbitEarL = _builder.RabbitEarL;
    _rabbitEarR = _builder.RabbitEarR;
    _rabbitBody = _builder.RabbitBody;
    if (_rabbitHead != null) _headBase = _rabbitHead.transform.localPosition;
    if (_rabbitEarL != null) _earLBase = _rabbitEarL.transform.localRotation;
    if (_rabbitEarR != null) _earRBase = _rabbitEarR.transform.localRotation;
    if (_rabbitBody != null) _bodyBase = _rabbitBody.transform.localScale;
    _camTeaching = _builder.CamTeaching;
    _lookTeaching = _builder.LookTeaching;
    _camDemo = _builder.CamDemo;
    _lookDemo = _builder.LookDemo;
    _camSuccess = _builder.CamSuccess;
    _lookSuccess = _builder.LookSuccess;

    BuildActors();
    GameObject fx = new GameObject("RPFx");
    fx.transform.SetParent(_root, false);
    _fx = fx.transform;

    // Carrots become real, clickable game objects (collider re-added: the
    // builder strips it for bake safety, clicks need it).
    _carrots.Clear();
    List<GameObject> carrots = _builder.Carrots;
    for (int i = 0; i < carrots.Count; i++) {
      GameObject go = carrots[i];
      if (go == null) continue;
      // UNCONDITIONAL (same lesson as gameplay #1: StripCollider uses deferred
      // Destroy at runtime, so a null-check here would leave carrots click-less).
      SphereCollider sc = go.AddComponent<SphereCollider>();
      sc.radius = 0.55f;
      sc.center = new Vector3(0f, 0.15f, 0f);
      RabbitCarrot carrot = go.GetComponent<RabbitCarrot>();
      if (carrot == null) carrot = go.AddComponent<RabbitCarrot>();
      carrot.HomeLocal = _builder.CarrotHomes != null && i < _builder.CarrotHomes.Length
        ? _builder.CarrotHomes[i] : go.transform.localPosition;
      carrot.Bind(this, _playerHand);
      _carrots.Add(carrot);
    }
    if (_builder.FeedAnchor != null) {
      FeedZone zone = _builder.FeedAnchor.gameObject.GetComponent<FeedZone>();
      if (zone == null) zone = _builder.FeedAnchor.gameObject.AddComponent<FeedZone>();
      zone.Bind(this);
    }

    // Re-entry policy FIRST (same lesson as #1/#2): a completed activity
    // adopts its finished picture without replaying the lesson, and the shared
    // lifecycle lives in MathScene so it survives this scene's unload.
    if (_life != null && _life.State == ActivityState.Completed) {
      ApplyCompletedState("adopt");
      return;
    }
    if (_life != null) {
      try {
        _life.MarkAvailable("rabbit_feed staged");
        _life.BeginEnter("rabbit scene built");
        _life.MarkReady("intro staged");
      } catch (Exception) { }
    }
    _lastPos = _player != null ? _player.position : Vector3.zero;
    try { Debug.Log("[RabbitFeed] activity staged (intro will play).", this); } catch (Exception) { }
  }

  void BuildActors() {
    _teacher = LessonActors.Build(_root, "RFTeacher", "NpcVisuals/TessVisual", 0.5f,
      RabbitPlayBuilder.TeacherStart, new Color(0.25f, 0.45f, 0.85f),
      new Color(0.98f, 0.78f, 0.25f));
    _student = LessonActors.Build(_root, "RFStudent", "NpcVisuals/MiloVisual", 0.42f,
      RabbitPlayBuilder.StudentStart, new Color(0.30f, 0.62f, 0.45f),
      new Color(0.55f, 0.35f, 0.20f));
    _voice = new PacedVoice();
    _voice.Audio = _audio;
    _voice.LogTag = "RabbitFeed";
    if (_teacher != null && _teacher.Root != null) FaceSnap(_teacher, BoardLocal());
    if (_student != null && _student.Root != null) FaceSnap(_student, TeacherLocal());
    if (_builder != null && _builder.Result != null) _builder.Result.SetActive(false);
  }

  void ApplyCompletedState(string reason) {
    Current = Phase.Success;
    _followHanded = true;
    _cameraDone = true;
    _landBeats = 0;
    _victoryT = -1f;
    if (_teacher != null && _teacher.Root != null) {
      _teacher.Root.transform.localPosition = RabbitPlayBuilder.TeacherStart;
      FaceSnap(_teacher, RabbitLocal());
    }
    if (_student != null && _student.Root != null) {
      _student.Root.transform.localPosition = RabbitPlayBuilder.StudentReturn;
      FaceSnap(_student, RabbitLocal());
    }
    // The finished picture: the target count sits in the bowl, the rest is
    // scenery, the result board is up.
    _fed.Clear();
    for (int i = 0; i < _carrots.Count; i++) {
      RabbitCarrot c = _carrots[i];
      if (c == null) continue;
      c.SetHand(_playerHand);
      if (i < Target) { c.ParkInBowl(BowlSlot(i)); _fed.Add(c); }
      else c.MarkRemoved();
    }
    Count = Target;
    SetPip(Target);
    if (_result != null) _result.SetActive(true);
    if (_player != null) _lastPos = _player.position;
    try { Debug.Log("[RabbitFeed] adopted COMPLETED state (" + reason + ").", this); }
    catch (Exception) { }
  }

  void Update() { Tick(Time.deltaTime); }

  // Deterministic tick (EditMode cover: no live frame needed). Owns the carrot
  // flights too (single owner — carrots have no self Update, so live play and
  // tests advance each flight exactly once).
  public void Tick(float dt) {
    if (dt <= 0f || _builder == null) return;
    try {
      TickVoice(dt);
      for (int i = 0; i < _carrots.Count; i++) {
        if (_carrots[i] != null) _carrots[i].TickForTests(dt);
      }
      switch (Current) {
        case Phase.Intro: TickIntro(dt); break;
        case Phase.Demo: TickDemo(dt); break;
        case Phase.Handoff: TickHandoff(dt); break;
        case Phase.Feeding: TickFeeding(dt); break;
        case Phase.Success: TickSuccess(dt); break;
        case Phase.Correct: TickCorrect(dt); break;
      }
      TickActing(dt);
      TickCamera(dt);
      TickJuice(dt);
      TickRabbit(dt);
    } catch (Exception) { }
  }

  void TickVoice(float dt) { if (_voice != null) _voice.Tick(dt); }
  void To(Phase next) { Current = next; _phaseT = 0f; }

  // ---- teacher intro ----------------------------------------------------------
  // The board holds the round's target: every line below is composed from it,
  // so the SAME script teaches 1..9 without a second lesson.

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
    if (!_saidNumber && t >= 3.4f) {
      _saidNumber = true;
      Say("This is number " + N(Target) + ".", "Đây là số " + Nvi(Target) + ".");
    }
    if (!_saidCountWord && t >= 5.6f) {
      _saidCountWord = true;
      Say(CountEn[Target - 1], CountVi[Target - 1]);
      PulseBoard(1.4f);
    }
    if (!_saidToday && t >= 7.2f) {
      _saidToday = true;
      FaceTowards(_teacher, RabbitLocal(), dt, 4f);
      Say(Cap(N(Target)) + " " + Carrots(Target) + " for bunny!",
        Cap(Nvi(Target)) + " củ cà rốt cho thỏ!");
      Point(_teacher, RabbitWorld(), 2.8f);
    }
    if (!_saidFeed && t >= 10.2f) {
      _saidFeed = true;
      Wave(_teacher);
      Say("Let's feed!", "Cùng cho ăn nhé!");
    }
    if (t >= 11.4f) {
      To(Phase.Demo);
      SetShot(1);
      FaceTowards(_student, PatchLocal(), dt, 4f);
    }
  }

  // ---- student demonstration ---------------------------------------------------
  // One carrot at a time, for real: walk to the patch -> pick (arc to the
  // student's fist) -> walk to the bunny -> feed (arc to the mouth, nibble,
  // teacher counts) -> next. No teleport, no snaps.
  void TickDemo(float dt) {
    _phaseT += dt;
    if (!_saidWatch) {
      _saidWatch = true;
      Say("Watch your friend!", "Xem bạn làm nhé!");
      Point(_teacher, PatchWorld(), 2.4f);
    }
    RabbitCarrot carrot = _demoFed < _carrots.Count ? _carrots[_demoFed] : null;
    switch (_demoStage) {
      case 0:
        FaceTowards(_student, PatchLocal(), dt, 5f);
        if (_phaseT >= 1.2f && WalkTo(_student, RabbitPlayBuilder.PatchStand, dt)) {
          _demoStage = 1;
          if (carrot != null) {
            carrot.SetHand(StudentHand());
            FaceTowards(_student, PatchLocal(), dt, 5f);
            carrot.BeginCarry(0.3f);
          }
        }
        break;
      case 1:
        if (carrot == null || carrot.State == RabbitCarrot.CarrotState.Carried) {
          _demoStage = 2;
        }
        break;
      case 2:
        FaceTowards(_student, RabbitLocal(), dt, 5f);
        if (WalkTo(_student, RabbitPlayBuilder.FeedStand, dt)) {
          _demoStage = 3;
          if (carrot != null) {
            carrot.OnMunched = OnDemoMunched;
            carrot.BeginFeed(MouthLocal(), 0.3f);
          }
        }
        break;
      case 3:
        if (carrot == null || carrot.State == RabbitCarrot.CarrotState.Consumed) {
          _demoStage = 4;
          _demoHoldT = DemoHold;
        }
        break;
      case 4:
        _demoHoldT -= dt;
        if (_demoHoldT <= 0f) {
          _demoFed++;
          if (_demoFed < Target) { _demoStage = 0; }
          else { _demoStage = 5; _demoHoldT = 1.6f; }
        }
        break;
      default:
        FaceTowards(_student, PlayerLocal(), dt, 3f);
        _demoHoldT -= dt;
        if (!_saidYes && _demoHoldT <= 1.0f) {
          _saidYes = true;
          Say("Yes! " + Cap(N(Target)) + " " + Carrots(Target) + "!",
            "Đúng rồi! " + Cap(Nvi(Target)) + " củ cà rốt!");
          Point(_teacher, RabbitWorld(), 2.2f);
          CelebrateActor(_student, soft: true);
          HopRabbit();
          Sparkle(RabbitWorld() + new Vector3(0f, 0.5f, 0f), 8, 91, 0.4f);
        }
        if (_demoHoldT <= 0f) {
          ResetPatch();
          To(Phase.Handoff);
        }
        break;
    }
  }

  void OnDemoMunched() {
    Nibble();
    PlaySfx("munch");
    int k = Mathf.Min(_demoFed + 1, Target);
    Say(CountEn[k - 1], CountVi[k - 1]);
    Point(_teacher, RabbitWorld(), 1.6f);
  }

  // The demo ate real carrots: grow them back for the child's round and hand
  // every carrot to the player's hand.
  void ResetPatch() {
    for (int i = 0; i < _carrots.Count; i++) {
      RabbitCarrot c = _carrots[i];
      if (c == null) continue;
      c.SetHand(_playerHand);
      c.OnMunched = null;
      if (c.State == RabbitCarrot.CarrotState.Consumed) c.ResetHome();
    }
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
    if (!_saidTask && t >= 2.4f) {
      _saidTask = true;
      Say("Feed " + N(Target) + " " + Carrots(Target) + "!",
        "Cho thỏ " + Nvi(Target) + " củ nhé!");
      Point(_teacher, RabbitWorld(), 2.6f);
    }
    // The student walks back beside the teacher so he never blocks the feed
    // stand (same lesson as gameplay #2).
    if (!_studentReturned) {
      if (WalkTo(_student, RabbitPlayBuilder.StudentReturn, dt)) _studentReturned = true;
      if (t >= 8.0f) _studentReturned = true; // safety: the lesson never stalls
    } else {
      FaceTowards(_student, RabbitLocal(), dt, 2.5f);
    }
    if (!_followHanded && t >= 4.2f) {
      _followHanded = true;
      Follow();
      if (_life != null) { try { _life.Begin("handoff done"); } catch (Exception) { } }
    }
    if (_followHanded && Current == Phase.Handoff && (_studentReturned || t >= 8.0f)) {
      To(Phase.Feeding);
      _lastPos = _player != null ? _player.position : Vector3.zero;
      try { Debug.Log("[RabbitFeed] child control (feeding phase).", this); } catch (Exception) { }
    }
  }

  // ---- the child's feeding --------------------------------------------------------
  // Real actions in the world: click a carrot (walk -> bend -> carry in the
  // fist) -> click the bowl or stop beside the bunny (reach -> the carrot
  // flies to the mouth -> the bunny nibbles -> the teacher counts).

  public void TryPick(RabbitCarrot carrot) {
    if (carrot == null || !carrot.IsAvailable) return;
    if (Current != Phase.Feeding && Current != Phase.Success) return;
    if (Carried != null) return; // one carrot at a time
    Carried = carrot;
    carrot.SetHand(_playerHand);
    if (_viz != null) {
      _viz.FaceTowards(carrot.transform.position, true);
      _viz.PlayPickup();
    }
    PlaySfx("pickup");
    Sparkle(carrot.transform.position, 6, _sparkleSeed++, 0.3f);
    carrot.BeginCarry(PickDelay);
  }

  public void TryFeed() {
    if (Carried == null) return;
    if (Current != Phase.Feeding && Current != Phase.Success) return;
    RabbitCarrot carrot = Carried;
    Carried = null;
    Count++;
    _fed.Add(carrot);
    if (_viz != null) {
      _viz.FaceTowards(RabbitWorld(), true);
      _viz.PlayPickup();
    }
    carrot.OnMunched = OnCarrotMunched;
    if (Count <= Target) {
      carrot.BeginFeed(MouthLocal(), FeedDelay);
      SetPip(Count);
      if (Count == Target) _landBeats = 2; // the success moment waits to land
      else _landBeats = 1;                 // a plain count line waits to land
    } else {
      // CORRECTION path: one too many — a counting lesson, never a punishment.
      // The extra carrot visibly reaches the mouth first, then goes home after
      // the teacher explains.
      _extra = carrot;
      carrot.BeginFeed(MouthLocal(), FeedDelay);
      To(Phase.Correct);
      _correctStep = 0;
      _correctT = 0f;
      Overshoots++;
    }
  }

  void OnCarrotMunched() {
    Nibble();
    PlaySfx("munch");
    Sparkle(RabbitWorld() + new Vector3(0f, 0.6f, 0f), 10, _sparkleSeed++, 0.5f);
    if (_landBeats == 1) {
      _landBeats = 0;
      int k = Mathf.Min(Count, Target);
      Say(CountEn[k - 1], CountVi[k - 1]);
      Point(_teacher, RabbitWorld(), 1.6f);
    } else if (_landBeats == 2) {
      _landBeats = 0;
      if (Current != Phase.Feeding) return;
      SuccessBeats();
    }
  }

  void SuccessBeats() {
    To(Phase.Success);
    PlaySfx("success");
    Sparkle(RabbitWorld() + new Vector3(0f, 0.7f, 0f), 14, _sparkleSeed++, 0.8f);
    Say(Cap(N(Target)) + " " + Carrots(Target) + "! Well done!",
      Cap(Nvi(Target)) + " củ cà rốt! Giỏi!");
    // The recap 1..Target waits its turn in the pacer's single slot (same
    // discipline as gameplay #2): one line in flight, the rest wait.
    _recapEn.Clear();
    _recapVi.Clear();
    for (int i = 1; i <= Target; i++) {
      _recapEn.Enqueue(CountEn[i - 1]);
      _recapVi.Enqueue(CountVi[i - 1]);
    }
    Point(_teacher, RabbitWorld(), 2.0f);
    CelebrateBoth();
    HopRabbit();
    if (_result != null) {
      _result.SetActive(true);
      _result.transform.localScale = Vector3.one * 0.65f;
      _resultPopT = 0f;
    }
    SetShot(2);
    _victoryT = 1.35f;
    MarkLifeCompleted();
  }

  void MarkLifeCompleted() {
    if (_life == null) return;
    try {
      if (_life.State != ActivityState.Completed) _life.MarkCompleted("fed the bunny " + Target);
    } catch (Exception) { }
  }

  void TickFeeding(float dt) {
    _phaseT += dt;
    if (_overshootNagT > 0f) _overshootNagT -= dt;
    if (_underCooldownT > 0f) _underCooldownT -= dt;
    TickFeedProximity();
    // Undershoot nudge: settled BELOW the target earns a gentle "how many
    // more" — near the patch or the bunny only, never across the arena, never
    // spam.
    bool moving = IsPlayerMoving();
    if (Count < Target && !moving && NearWork()) {
      _underT += dt;
      if (_underT >= UnderDwell && _underCooldownT <= 0f) {
        _underCooldownT = UnderNudgeCooldown;
        _underT = 0f;
        UndershootNudges++;
        int remain = Target - Count;
        Say(Cap(N(remain)) + " more " + Carrots(remain) + "!",
          "Còn " + Nvi(remain) + " củ nữa nhé!");
        Point(_teacher, remain == Target ? PatchWorld() : RabbitWorld(), 2.0f);
        Log("undershoot at " + Count + " (" + remain + " more)");
      }
    } else {
      _underT = 0f;
    }
    _lastPos = _player != null ? _player.position : Vector3.zero;
    FaceTowards(_teacher, PlayerLocal(), dt, 2.2f);
    FaceTowards(_student, PlayerLocal(), dt, 2.2f);
    FaceRabbitTo(PlayerWorld(), dt);
  }

  // "Stop, then feed" (same discipline as gameplay #1): walking PAST the bowl
  // must never fling the carrot out of the hand — the child stops first.
  void TickFeedProximity() {
    if (Carried == null || _player == null) return;
    if (_mover != null && _mover.IsMoving) return;
    if (PlayerNear(RabbitWorld(), 1.5f)) TryFeed();
  }

  bool NearWork() {
    return PlayerNear(PatchWorld(), UnderNearXZ) || PlayerNear(RabbitWorld(), UnderNearXZ);
  }

  bool IsPlayerMoving() {
    if (_mover != null) return _mover.IsMoving;
    if (_player == null) return false;
    Vector3 d = _player.position - _lastPos;
    d.y = 0f;
    return d.sqrMagnitude > 0.0004f;
  }

  void TickSuccess(float dt) {
    _phaseT += dt;
    TickRecap();
    FaceTowards(_teacher, PlayerLocal(), dt, 2f);
    FaceTowards(_student, PlayerLocal(), dt, 2f);
    FaceRabbitTo(PlayerWorld(), dt);
    TickFeedProximity(); // the field stays open: a spare carrot feeds the lesson
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

  // ---- correction beats (deterministic timer) ---------------------------------------
  // The extra carrot reached the mouth; the teacher counts the bowl 1..N,
  // points at the board ("the board says N"), names the limit ("N is enough"),
  // and the carrot hops home. Then Success again — gently, like a clean run.

  void TickCorrect(float dt) {
    _correctT += dt;
    if (_correctStep < 0) return;
    if (_correctStep == 0 && _correctT >= 0.4f) {
      _correctStep = 1;
      Say("Let's count again!", "Cùng đếm lại nhé!");
      Point(_teacher, RabbitWorld(), 2.2f);
      return;
    }
    // Counts 1..Target, one beat each (1.7s apart so the pacer breathes).
    if (_correctStep >= 1 && _correctStep <= Target) {
      float at = 0.4f + _correctStep * 1.7f;
      if (_correctT >= at) {
        int k = _correctStep;
        _correctStep++;
        Say(CountEn[k - 1], CountVi[k - 1]);
        if (k == Target) Point(_teacher, BoardWorld(), 2.4f);
      }
      return;
    }
    float afterCounts = 0.4f + (Target + 1) * 1.7f;
    if (_correctStep == Target + 1 && _correctT >= afterCounts) {
      _correctStep = Target + 2;
      Say("The board says " + N(Target) + ".", "Bảng ghi số " + Nvi(Target) + ".");
      return;
    }
    if (_correctStep == Target + 2 && _correctT >= afterCounts + 1.8f) {
      _correctStep = Target + 3;
      Say(Cap(N(Target)) + " is enough.", "Đủ " + Nvi(Target) + " củ rồi.");
      if (_extra != null) _extra.BeginReturnHome();
      return;
    }
    if (_correctStep == Target + 3 && _correctT >= afterCounts + 3.6f) {
      FinishCorrect();
    }
  }

  void FinishCorrect() {
    _extra = null;
    _landBeats = 0;
    // Every carrot fed BEYOND the target goes home, so the bowl ends with
    // exactly the board's number — whatever the child picked.
    for (int i = Target; i < _fed.Count; i++) {
      RabbitCarrot c = _fed[i];
      if (c != null && c.State != RabbitCarrot.CarrotState.Available) c.BeginReturnHome();
    }
    while (_fed.Count > Target) _fed.RemoveAt(_fed.Count - 1);
    Count = Target;
    SetPip(Target);
    if (_result != null) {
      _result.SetActive(true);
      _result.transform.localScale = Vector3.one * 0.65f;
      _resultPopT = 0f;
    }
    Say(Cap(N(Target)) + " " + Carrots(Target) + "! Well done!",
      Cap(Nvi(Target)) + " củ cà rốt! Giỏi!");
    CelebrateBoth();
    HopRabbit();
    MarkLifeCompleted();
    To(Phase.Success);
    _phaseT = 0f;
    _followHanded = true; // the celebration frame already played; stay with follow
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
      // teaching frame while the teacher introduces the board.
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
    try { Debug.Log("[RabbitFeed] camera returned to follow.", this); } catch (Exception) { }
  }

  // ---- rabbit life (procedural, small — the bunny must feel fed, not triggered) ----

  void Nibble() { _nibbleT = 1.4f; }

  void HopRabbit() {
    if (_rabbit == null) return;
    try {
      CharacterPresentation face = _rabbit.GetComponent<CharacterPresentation>();
      if (face != null) face.PlayHop();
    } catch (Exception) { }
    _nibbleT = Mathf.Max(_nibbleT, 0.9f);
  }

  void TickRabbit(float dt) {
    _breatheT += dt;
    if (_rabbit == null) return;
    // Breathing: a 2% swell at ~0.4Hz on the body (same language as Tess).
    if (_rabbitBody != null) {
      float b = 1f + 0.02f * Mathf.Sin(_breatheT * 2.5f);
      Vector3 s = _bodyBase;
      if (_nibbleT > 0f) {
        float k = 1f - _nibbleT / 1.4f;
        float bulge = Mathf.Sin(k * Mathf.PI);
        s = new Vector3(_bodyBase.x * (1f - 0.06f * bulge), _bodyBase.y * (1f + 0.09f * bulge),
          _bodyBase.z * (1f - 0.06f * bulge));
      }
      try { _rabbitBody.transform.localScale = new Vector3(s.x * b, s.y, s.z * b); }
      catch (Exception) { }
    }
    // Nibble: the head bobs down to the mouth and back, ears wiggle.
    if (_nibbleT > 0f) {
      _nibbleT -= dt;
      float k = Mathf.Clamp01(1f - _nibbleT / 1.4f);
      float bob = Mathf.Sin(k * Mathf.PI * 3f) * 0.07f;
      if (_rabbitHead != null) {
        try { _rabbitHead.transform.localPosition = _headBase + new Vector3(0f, -Mathf.Abs(bob), -bob * 0.5f); }
        catch (Exception) { }
      }
      float wig = Mathf.Sin(k * Mathf.PI * 6f) * 12f;
      if (_rabbitEarL != null) {
        try { _rabbitEarL.transform.localRotation = _earLBase * Quaternion.Euler(wig, 0f, 0f); }
        catch (Exception) { }
      }
      if (_rabbitEarR != null) {
        try { _rabbitEarR.transform.localRotation = _earRBase * Quaternion.Euler(-wig, 0f, 0f); }
        catch (Exception) { }
      }
      if (_nibbleT <= 0f) {
        try {
          if (_rabbitHead != null) _rabbitHead.transform.localPosition = _headBase;
          if (_rabbitEarL != null) _rabbitEarL.transform.localRotation = _earLBase;
          if (_rabbitEarR != null) _rabbitEarR.transform.localRotation = _earRBase;
        } catch (Exception) { }
      }
      return;
    }
    // Idle ear twitches every few seconds (alive, not statue-still).
    _earTwitchT -= dt;
    if (_earTwitchT <= 0f) {
      _earTwitchT = 3.5f + (_sparkleSeed % 10) * 0.3f;
      _twitchT = 0.5f;
    }
    if (_twitchT > 0f) {
      _twitchT -= dt;
      float k = 1f - _twitchT / 0.5f;
      float tw = Mathf.Sin(k * Mathf.PI) * 14f;
      if (_rabbitEarL != null) {
        try { _rabbitEarL.transform.localRotation = _earLBase * Quaternion.Euler(tw, 0f, 0f); }
        catch (Exception) { }
      }
      if (_twitchT <= 0f && _rabbitEarL != null) {
        try { _rabbitEarL.transform.localRotation = _earLBase; } catch (Exception) { }
      }
    }
  }

  void FaceRabbitTo(Vector3 world, float dt) {
    if (_rabbit == null) return;
    try {
      Vector3 p = _rabbit.transform.position;
      Vector3 d = new Vector3(world.x - p.x, 0f, world.z - p.z);
      if (d.sqrMagnitude < 0.04f) return; // too close: keep the bowl facing
      if (d.sqrMagnitude > 9f) return;    // too far: keep the bowl facing
      Quaternion want = Quaternion.LookRotation(d);
      if (dt <= 0f) { _rabbit.transform.rotation = want; return; }
      _rabbit.transform.rotation = Quaternion.Slerp(_rabbit.transform.rotation, want,
        1f - Mathf.Exp(-2f * dt));
    } catch (Exception) { }
  }

  // ---- actor motion / gestures (same acting language as #1/#2) ---------------------

  Transform StudentHand() {
    if (_student == null) return _playerHand;
    if (_student.HandBone != null) return _student.HandBone;
    if (_student.CarryAnchor != null) return _student.CarryAnchor;
    return _playerHand;
  }

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
    if (_boardPulseT > 0f) {
      _boardPulseT -= dt;
      if (_board != null) {
        float k = Mathf.Clamp01(_boardPulseT / 1.4f);
        float s = 1f + 0.12f * Mathf.Sin(k * Mathf.PI);
        _board.transform.localScale = new Vector3(s, s, s);
      }
      if (_boardPulseT <= 0f && _board != null) _board.transform.localScale = Vector3.one;
    }
    if (_builder == null || _builder.CountPips == null) return;
    for (int i = 0; i < _builder.CountPips.Length && i < _pipPopT.Length; i++) {
      if (_pipPopT[i] >= 1f) continue;
      _pipPopT[i] = Mathf.Min(1f, _pipPopT[i] + dt / 0.28f);
      GameObject pip = _builder.CountPips[i];
      if (pip == null) continue;
      float s = 1f + 0.5f * Mathf.Sin(Mathf.PI * _pipPopT[i]);
      pip.transform.localScale = new Vector3(0.3f * s, 0.01f, 0.3f * s);
    }
  }

  void PulseBoard(float seconds) { _boardPulseT = Mathf.Max(_boardPulseT, seconds); }

  void Say(string en, string vi) {
    if (_voice == null) return;
    _voice.Speak(DialogueLang.T(en, vi));
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
    try { Debug.Log("[RabbitFeed] " + message, this); } catch (Exception) { }
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
    return _builder != null && _builder.NumberBoard != null
      ? _builder.NumberBoard.transform.position : transform.position;
  }
  Vector3 BoardLocal() { return LocalPoint(BoardWorld()); }

  Vector3 RabbitWorld() {
    return _rabbit != null ? _rabbit.transform.position
      : (_root != null ? _root.TransformPoint(RabbitPlayBuilder.RabbitHome) : Vector3.zero);
  }
  Vector3 RabbitLocal() { return LocalPoint(RabbitWorld()); }

  Vector3 PatchWorld() {
    return _root != null ? _root.TransformPoint(RabbitPlayBuilder.PatchCenter)
      : Vector3.zero;
  }
  Vector3 PatchLocal() { return LocalPoint(PatchWorld()); }

  Vector3 PlayerLocal() {
    return _player != null ? LocalPoint(_player.position) : RabbitLocal();
  }
  Vector3 PlayerWorld() {
    return _player != null ? _player.position : RabbitWorld();
  }
  Vector3 TeacherLocal() {
    return _teacher != null && _teacher.Root != null
      ? _teacher.Root.transform.localPosition + new Vector3(0f, 1.1f, 0f)
      : LocalPoint(RabbitPlayBuilder.TeacherStart);
  }

  Vector3 MouthLocal() {
    Vector3 mouthWorld = RabbitWorld() + new Vector3(0f, 0.5f, 0f);
    if (_rabbit != null) {
      try {
        Vector3 fwd = _rabbit.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.001f) mouthWorld = RabbitWorld() + fwd.normalized * 0.45f + new Vector3(0f, 0.5f, 0f);
      } catch (Exception) { }
    }
    return LocalPoint(mouthWorld);
  }

  Vector3 BowlWorld() {
    return _builder != null && _builder.FeedAnchor != null
      ? _builder.FeedAnchor.position : RabbitWorld();
  }

  // Fed carrots rest IN the bowl (only their tops show) — each gets its own
  // slot so the count reads from the gameplay camera, never a pile.
  static readonly Vector2[] BowlOffsets = {
    new Vector2(-0.18f, 0.12f), new Vector2(0.0f, 0.14f), new Vector2(0.18f, 0.12f),
    new Vector2(-0.18f, -0.08f), new Vector2(0.0f, -0.06f), new Vector2(0.18f, -0.08f),
    new Vector2(-0.1f, -0.22f), new Vector2(0.12f, -0.22f), new Vector2(0.0f, 0.0f),
  };

  Vector3 BowlSlot(int i) {
    Vector3 b = RabbitPlayBuilder.BowlPos;
    if (_root == null) return b;
    if (i < 0) i = 0;
    if (i >= BowlOffsets.Length) i = BowlOffsets.Length - 1;
    Vector2 o = BowlOffsets[i];
    // Builder-local == game-local (both hang under the arena root).
    return new Vector3(b.x + o.x, 0.18f, b.z + o.y);
  }

  // Empty slots stay visible (grey) and turn gold as the bunny is fed.
  Material _pipGold;
  Material _pipEmpty;

  void SetPip(int n) {
    PipCount = n;
    if (_builder == null || _builder.CountPips == null) return;
    if (_pipGold == null) _pipGold = PipMaterial(new Color(0.98f, 0.78f, 0.25f), 0.45f);
    if (_pipEmpty == null) _pipEmpty = PipMaterial(new Color(0.68f, 0.68f, 0.66f), 0f);
    for (int i = 0; i < _builder.CountPips.Length; i++) {
      GameObject pip = _builder.CountPips[i];
      if (pip == null) continue;
      pip.SetActive(true);
      Renderer rend = pip.GetComponent<Renderer>();
      if (rend != null) rend.sharedMaterial = (i < n) ? _pipGold : _pipEmpty;
      if (i < n && i > _lastPip && i < _pipPopT.Length) _pipPopT[i] = 0f;
    }
    _lastPip = n - 1;
  }

  static Material PipMaterial(Color color, float emission) {
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    if (emission > 0f && mat.HasProperty("_EmissionColor")) {
      mat.EnableKeyword("_EMISSION");
      mat.SetColor("_EmissionColor", color * emission);
    }
    mat.enableInstancing = true;
    return mat;
  }

  // Test seams (no live scene needed).
  public void SetPhaseForTests(Phase p) { Current = p; _phaseT = 0f; }
  public void MarkLifecycleActiveForTests() {
    if (_life == null) return;
    try {
      if (_life.State == ActivityState.Ready || _life.State == ActivityState.Available)
        _life.Begin("test active");
    } catch (Exception) { }
  }
}
