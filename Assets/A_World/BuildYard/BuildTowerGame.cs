// A_World/BuildYard/BuildTowerGame.cs — S3-P2Z14 GAMEPLAY #4
// "XÂY THÁP THEO SỐ" (build the tower by number). The Build Yard activity,
// staged in its OWN lazy scene (BuildTowerScene), reached from the Math Hub's
// build_yard gate.
// The experience: the teacher links the TARGET NUMBER to that many blocks at
// the board -> the child student fetches them one by one and stacks them on the
// build pad while the teacher counts 1..N -> handover ("Now it's your turn!") ->
// the CHILD builds: one block = one count; the tower's height IS the count;
// placing past the target is guidance, never failure, and stopping short earns
// a gentle "how many more" nudge, never a fail.
// One yard (MAXIMUM = 9, brief §17), one mechanic, many targets: the target
// only decides how many blocks the tower gets (progression owned by the area:
// 3 -> 5 -> 7 -> 9 -> 1). No confetti on success: glow + sound + NPC reaction
// only (same discipline as #2/#3).
// Architecture: scene-local components, no manager/singleton, no new
// bus/service; reuses LessonActors (shared body kit), PacedVoice (paced
// speech), DemoJuice (FX), SmartCamera beats, ActivityLifecycle (owned by the
// Math-side area), MicroWorldPortal (the door). C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

// One real block: Available -> Picked -> Carried -> Placed. Placement slot is
// fixed by the stack index at place time, never by click counts; a placed
// block can never be re-picked or counted twice.
[DisallowMultipleComponent]
public class TowerBlock : MonoBehaviour, IClickTarget {
  public enum BlockState { Available, Picked, Carried, Placed, Removed }

  public BlockState State { get; private set; } = BlockState.Available;
  public Vector3 HomeLocal;
  public int PlacedIndex { get; private set; } = -1;
  public BuildTowerGame Game;
  // Fired the moment a place flight settles on the tower. The game hangs the
  // wooden clack + counting/celebration beats on it, so feedback follows the
  // REAL moment the block lands — not the click that started it.
  public Action OnPlaced;

  Transform _hand;
  bool _picking;
  float _pickT, _pickDelay, _pickDur;
  Vector3 _pickFrom;
  bool _placing;
  float _placeT, _placeDelay, _placeDur;
  Vector3 _placeFrom, _placeTo;
  float _placeLift;
  bool _returning;
  Vector3 _flyFrom, _flyTo;
  float _flyT, _flyDur, _flyLift;
  bool _flying;
  float _bounceT = 1f;
  Vector3 _baseScale = Vector3.one;
  Collider _collider;

  public bool IsAvailable { get { return State == BlockState.Available; } }
  public bool IsFlying { get { return _flying; } }

  public void Bind(BuildTowerGame game, Transform hand) {
    Game = game;
    _hand = hand;
    _baseScale = transform.localScale;
    _collider = GetComponent<Collider>();
  }

  public void SetHand(Transform hand) { _hand = hand; }

  public void OnClicked() {
    if (State != BlockState.Available || Game == null) return;
    Game.TryPick(this);
  }

  // Pickup: the child bends (player PickUp clip); once the hand is down, the
  // block arcs up into it and rides the fist. No ground->hand snap.
  public void BeginCarry(float delay = 0f) {
    if (State != BlockState.Available) return;
    State = BlockState.Picked;
    if (_collider != null) _collider.enabled = false;
    _flying = false;
    _returning = false;
    _placing = false;
    _picking = true;
    _pickT = 0f;
    _pickDelay = Mathf.Max(0f, delay);
    _pickDur = 0.42f;
    _pickFrom = transform.localPosition;
  }

  // Place: the block keeps riding the fist while the child reaches toward the
  // pad, then flies the last stretch onto the tower and snaps into its slot.
  public void BeginPlace(int stackIndex, Vector3 slotLocal, float delay = 0f) {
    if (State != BlockState.Carried) return;
    PlacedIndex = stackIndex;
    _picking = false;
    _placing = true;
    _placeT = 0f;
    _placeDelay = Mathf.Max(0f, delay);
    _placeDur = 0.32f;
    _placeLift = 0.18f;
    _placeTo = slotLocal;
  }

  // Correction path: the extra block leaves the tower top and returns home.
  public void BeginReturnHome() {
    State = BlockState.Carried; // re-uses the flight while it travels
    _picking = false;
    _placing = false;
    _returning = true;
    _flyFrom = transform.localPosition;
    _flyTo = HomeLocal;
    _flyT = 0f;
    _flyDur = 0.6f;
    _flyLift = 0.8f;
    _flying = true;
  }

  public void ParkInSlot(int stackIndex, Vector3 slotLocal) {
    State = BlockState.Placed;
    PlacedIndex = stackIndex;
    _flying = false;
    _picking = false;
    _placing = false;
    transform.localPosition = slotLocal;
    transform.localRotation = Quaternion.identity;
    gameObject.SetActive(true);
    if (_collider != null) _collider.enabled = false;
  }

  public void MarkRemoved() {
    State = BlockState.Removed;
    if (_collider != null) _collider.enabled = false;
  }

  public void ResetHome() {
    State = BlockState.Available;
    PlacedIndex = -1;
    _flying = false;
    _picking = false;
    _placing = false;
    _returning = false;
    transform.localPosition = HomeLocal;
    transform.localRotation = Quaternion.identity;
    transform.localScale = _baseScale;
    gameObject.SetActive(true);
    if (_collider != null) _collider.enabled = true;
  }

  // Deterministic tick — owned by BuildTowerGame.Tick (single owner, no self
  // Update, so live play and EditMode advance flights exactly once).
  public void TickForTests(float dt) {
    try {
      if (_picking) { TickPick(dt); return; }
      if (_placing) { TickPlace(dt); return; }
      if (_flying) { TickFlight(dt); return; }
      if (State == BlockState.Carried && _hand != null) FollowHand(dt);
      TickBounceAndPulse(dt);
    } catch (Exception) {
      _picking = false;
      _placing = false;
      _flying = false;
    }
  }

  void TickPick(float dt) {
    _pickT += dt;
    if (_pickT < _pickDelay) return;
    float t = Mathf.Clamp01((_pickT - _pickDelay) / _pickDur);
    Vector3 handLocal = HandLocal();
    Vector3 mid = (_pickFrom + handLocal) * 0.5f + new Vector3(0f, 0.35f, 0f);
    transform.localPosition = Vector3.Lerp(
      Vector3.Lerp(_pickFrom, mid, t), Vector3.Lerp(mid, handLocal, t), t);
    if (t >= 1f) {
      _picking = false;
      State = BlockState.Carried;
      _bounceT = 0f;
    }
  }

  void TickPlace(float dt) {
    _placeT += dt;
    if (_placeT < _placeDelay) {
      if (_hand != null) FollowHand(dt);
      return;
    }
    if (!_flying) {
      _placeFrom = transform.localPosition;
      _flyFrom = _placeFrom;
      _flyTo = _placeTo;
      _flyT = 0f;
      _flyDur = _placeDur;
      _flyLift = _placeLift;
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
    bool placed = _placing;
    _placing = false;
    if (_returning) {
      _returning = false;
      State = BlockState.Available;
      PlacedIndex = -1;
      if (_collider != null) _collider.enabled = true;
      return;
    }
    if (placed) {
      // Snap exactly onto the slot (a soft landing, never an intersecting rest).
      transform.localPosition = _placeTo;
      transform.localRotation = Quaternion.identity;
      State = BlockState.Placed;
      if (OnPlaced != null) {
        try { OnPlaced(); } catch (Exception) { }
      }
    }
  }

  void TickBounceAndPulse(float dt) {
    if (_bounceT < 1f) {
      _bounceT = Mathf.Min(1f, _bounceT + dt / 0.3f);
      float s = 1f + 0.14f * Mathf.Sin(Mathf.PI * _bounceT);
      transform.localScale = _baseScale * s;
      if (_bounceT >= 1f) transform.localScale = _baseScale;
    }
    if (State == BlockState.Available && Game != null && Game.PlayerNear(transform.position, 2.4f)) {
      float s = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
      transform.localScale = _baseScale * s;
    } else if (State == BlockState.Available && _bounceT >= 1f) {
      transform.localScale = _baseScale;
    }
  }

  // Test/tower pulse (transform-only celebration when a block lands).
  public void Pulse(float seconds) { _pulseT = Mathf.Max(_pulseT, seconds); }
  float _pulseT;
  public void TickPulseForTests(float dt) { if (_pulseT > 0f) _pulseT -= dt; }
}

// The pad's door: clicking it walks the child up and places; simply carrying a
// block close to it (and stopping) does the same — one call, state-guarded,
// spam-safe. Proximity is polled by BuildTowerGame.Tick (single owner).
[DisallowMultipleComponent]
public class BuildPadZone : MonoBehaviour, IClickTarget {
  public BuildTowerGame Game;
  public float placeRadius = 1.6f;

  public void Bind(BuildTowerGame game) {
    Game = game;
    // Unconditional (same lesson as #1-#3: the builder strips colliders for
    // bake safety with deferred Destroy, so a null-check would leave the pad
    // click-less).
    BoxCollider box = gameObject.AddComponent<BoxCollider>();
    box.size = new Vector3(1.6f, 0.8f, 1.6f);
    box.center = new Vector3(0f, 0.4f, 0f);
  }

  public void OnClicked() {
    if (Game != null) Game.TryPlace();
  }
}

[DisallowMultipleComponent]
public class BuildTowerGame : MonoBehaviour {
  public enum Phase {
    Intro,    // teacher links the board's number to blocks for the tower
    Demo,     // the student fetches N blocks one by one and stacks them
    Handoff,  // "Now it's your turn!" + the demo tower tidies back to the yard
    Building, // the child picks, carries and places; the teacher counts along
    Success,  // the tower has the target height — celebrated, field stays open
    Correct,  // one too many: a gentle counting correction, then Success again
  }

  // Beat timings (one place; deterministic for Tick tests).
  const float DemoHold = 0.7f;           // teacher count beat per demo block
  const float OvershootNagCooldown = 5f;
  const float UnderNudgeCooldown = 8f;
  const float UnderDwell = 2.0f;         // settled-below-target before a nudge
  const float UnderNearXZ = 4.5f;        // nudge only near the yard/pad
  const float WalkSpeed = 0.85f;         // student legs
  public static readonly Vector3 FollowOffset = BuildTowerBuilder.FollowOffset;

  // Number words: Vietnamese first; English prepared alongside. Every composed
  // line stays inside the SafetyFilter NPC cap (<= 6 tokens).
  static readonly string[] NumEn = {
    "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
  static readonly string[] NumVi = {
    "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
  static readonly string[] BlockEn = {
    "One block.", "Two blocks.", "Three blocks.", "Four blocks.", "Five blocks.",
    "Six blocks.", "Seven blocks.", "Eight blocks.", "Nine blocks." };
  static readonly string[] BlockVi = {
    "Một khối.", "Hai khối.", "Ba khối.", "Bốn khối.", "Năm khối.",
    "Sáu khối.", "Bảy khối.", "Tám khối.", "Chín khối." };

  static int ClampN(int i) { return Mathf.Clamp(i, 1, BuildTowerBuilder.MaxTarget); }
  static string N(int i) { return NumEn[ClampN(i) - 1]; }
  static string Nvi(int i) { return NumVi[ClampN(i) - 1]; }
  static string Blocks(int n) { return ClampN(n) == 1 ? "block" : "blocks"; }
  static string Cap(string s) {
    return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
  }

  public Phase Current { get; private set; } = Phase.Intro;
  public int Target { get; private set; } = BuildTowerBuilder.Target;
  public int Count { get; private set; }
  public TowerBlock Carried { get; private set; }
  public int Overshoots { get; private set; }
  public int UndershootNudges { get; private set; }
  public int DemoBlocksPlaced { get { return _demoPlaced; } }
  public bool IntroDone { get { return Current != Phase.Intro; } }
  public bool ResultShown { get { return _result != null && _result.activeSelf; } }
  public int BlockCountTotal { get { return _blocks.Count; } }
  public TowerBlock BlockAt(int i) { return i >= 0 && i < _blocks.Count ? _blocks[i] : null; }
  // The tower as it really stands: index 0..Count-1 are the placed blocks.
  public TowerBlock PlacedAt(int i) { return i >= 0 && i < _placed.Count ? _placed[i] : null; }
  public int TowerHeight { get { return Count; } }
  // The next stack slot (world) a carried block would land on — pinned by tests
  // and used by the ghost marker.
  public Vector3 NextSlotWorld() {
    return _root != null
      ? _root.TransformPoint(BuildTowerBuilder.StackSlot(Count, BuildTowerBuilder.PadPos))
      : Vector3.zero;
  }

  BuildTowerBuilder _builder;
  Transform _root;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PlayerVisual _viz;
  ClickToMove _mover;
  Transform _playerHand;
  Action<int> _onCompleted;

  LessonActor _teacher;
  LessonActor _student;
  PacedVoice _voice;
  Transform _fx;

  GameObject _board;
  GameObject _result;
  GameObject _ghost;
  Transform _padAnchor;
  Transform _camTeaching, _lookTeaching, _camDemo, _lookDemo, _camSuccess, _lookSuccess;

  readonly List<TowerBlock> _blocks = new List<TowerBlock>();
  readonly List<TowerBlock> _placed = new List<TowerBlock>();
  readonly Queue<TowerBlock> _resetQueue = new Queue<TowerBlock>();

  public float PickDelay = 0.45f;
  public float PlaceDelay = 0.45f;

  float _phaseT;
  float _shotT = -1f;
  bool _shotIssued;
  bool _cameraDone;
  bool _followHanded;
  int _shot;

  bool _saidBoard, _saidNumber, _saidCountWord, _saidToday, _saidBuild;
  int _demoStage; // 0 walk to yard, 1 picking, 2 walk to pad, 3 placing, 4 hold, 5 confirm
  int _demoPlaced;
  float _demoHoldT;
  bool _saidWatch, _saidYes;
  bool _saidTurn, _saidTask;
  bool _studentReturned;
  float _victoryT = -1f;
  float _resultPopT = 1f;
  float _boardPulseT;
  float _ghostPulseT;
  Vector3 _lastPos;
  readonly Queue<string> _recapEn = new Queue<string>();
  readonly Queue<string> _recapVi = new Queue<string>();

  // Correction beats (timed, deterministic; no coroutines so tests can tick).
  int _correctStep = -1;
  float _correctT;
  TowerBlock _extra;
  int _landBeats; // what the NEXT landing means (count line vs success)
  float _overshootNagT;
  float _underCooldownT;
  float _underT;
  float _resetT;

  int _sparkleSeed = 1300;
  TowerBlock _pulsing;
  float _pulseT;

  public void Build(BuildTowerBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life, int target,
      Action<int> onCompleted = null) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _onCompleted = onCompleted;
    Target = BuildTowerBuilder.ClampTarget(target <= 0 ? BuildTowerBuilder.Target : target);
    _viz = player != null ? player.GetComponent<PlayerVisual>() : null;
    _mover = player != null ? player.GetComponent<ClickToMove>() : null;
    _playerHand = player;
    if (_builder == null) {
      Debug.LogWarning("[BuildTowerGame] no builder; activity parked.", this);
      return;
    }
    _root = _builder.transform;
    _board = _builder.NumberBoard;
    _result = _builder.Result;
    _ghost = _builder.Ghost;
    _padAnchor = _builder.PadAnchor;
    _camTeaching = _builder.CamTeaching;
    _lookTeaching = _builder.LookTeaching;
    _camDemo = _builder.CamDemo;
    _lookDemo = _builder.LookDemo;
    _camSuccess = _builder.CamSuccess;
    _lookSuccess = _builder.LookSuccess;

    BuildActors();
    GameObject fx = new GameObject("BTFx");
    fx.transform.SetParent(_root, false);
    _fx = fx.transform;

    // Blocks become real, clickable game objects (collider re-added: the
    // builder strips it for bake safety, clicks need it).
    _blocks.Clear();
    List<GameObject> blocks = _builder.Blocks;
    for (int i = 0; i < blocks.Count; i++) {
      GameObject go = blocks[i];
      if (go == null) continue;
      // UNCONDITIONAL (same lesson as #1-#3: StripCollider uses deferred
      // Destroy at runtime, so a null-check would leave blocks click-less).
      BoxCollider bc = go.AddComponent<BoxCollider>();
      bc.size = new Vector3(0.8f, 0.8f, 0.8f);
      TowerBlock block = go.GetComponent<TowerBlock>();
      if (block == null) block = go.AddComponent<TowerBlock>();
      block.HomeLocal = _builder.BlockHomes != null && i < _builder.BlockHomes.Length
        ? _builder.BlockHomes[i] : go.transform.localPosition;
      block.Bind(this, _playerHand);
      _blocks.Add(block);
    }
    if (_padAnchor != null) {
      BuildPadZone zone = _padAnchor.gameObject.GetComponent<BuildPadZone>();
      if (zone == null) zone = _padAnchor.gameObject.AddComponent<BuildPadZone>();
      zone.Bind(this);
    }

    // Re-entry policy FIRST (same lesson as #1-#3): a completed activity
    // adopts the finished tower without replaying the lesson, and the shared
    // lifecycle lives in MathScene so it survives this scene's unload.
    if (_life != null && _life.State == ActivityState.Completed) {
      ApplyCompletedState("adopt");
      return;
    }
    if (_life != null) {
      try {
        _life.MarkAvailable("build_tower staged");
        _life.BeginEnter("build yard built");
        _life.MarkReady("intro staged");
      } catch (Exception) { }
    }
    _lastPos = _player != null ? _player.position : Vector3.zero;
    try { Debug.Log("[BuildTowerGame] activity staged (intro will play).", this); } catch (Exception) { }
  }

  void BuildActors() {
    _teacher = LessonActors.Build(_root, "BTTeacher", "NpcVisuals/TessVisual", 0.5f,
      BuildTowerBuilder.TeacherStart, new Color(0.25f, 0.45f, 0.85f),
      new Color(0.98f, 0.78f, 0.25f));
    _student = LessonActors.Build(_root, "BTStudent", "NpcVisuals/MiloVisual", 0.42f,
      BuildTowerBuilder.StudentStart, new Color(0.30f, 0.62f, 0.45f),
      new Color(0.55f, 0.35f, 0.20f));
    _voice = new PacedVoice();
    _voice.Audio = _audio;
    _voice.LogTag = "BuildTower";
    if (_teacher != null && _teacher.Root != null) FaceSnap(_teacher, BoardLocal());
    if (_student != null && _student.Root != null) FaceSnap(_student, TeacherLocal());
    if (_builder != null && _builder.Result != null) _builder.Result.SetActive(false);
    if (_builder != null && _builder.Ghost != null) _builder.Ghost.SetActive(false);
  }

  // Terminal adopt: the finished tower stands as it was left (first Target
  // blocks placed, the rest scenery), actors observing, result up.
  void ApplyCompletedState(string reason) {
    Current = Phase.Success;
    _followHanded = true;
    _cameraDone = true;
    _landBeats = 0;
    _victoryT = -1f;
    if (_teacher != null && _teacher.Root != null) {
      _teacher.Root.transform.localPosition = BuildTowerBuilder.TeacherStart;
      FaceSnap(_teacher, PadLocal());
    }
    if (_student != null && _student.Root != null) {
      _student.Root.transform.localPosition = BuildTowerBuilder.StudentReturn;
      FaceSnap(_student, PadLocal());
    }
    _placed.Clear();
    for (int i = 0; i < _blocks.Count; i++) {
      TowerBlock b = _blocks[i];
      if (b == null) continue;
      b.SetHand(_playerHand);
      if (i < Target) {
        b.ParkInSlot(i, BuildTowerBuilder.StackSlot(i, BuildTowerBuilder.PadPos));
        _placed.Add(b);
      } else {
        b.MarkRemoved();
      }
    }
    Count = Target;
    if (_result != null) _result.SetActive(true);
    if (_ghost != null) _ghost.SetActive(false);
    if (_player != null) _lastPos = _player.position;
    try { Debug.Log("[BuildTowerGame] adopted COMPLETED state (" + reason + ").", this); } catch (Exception) { }
  }

  void Update() { Tick(Time.deltaTime); }

  // Deterministic tick (EditMode cover: no live frame needed). Owns the block
  // flights too (single owner — blocks have no self Update, so live play and
  // tests advance each flight exactly once).
  public void Tick(float dt) {
    if (dt <= 0f || _builder == null) return;
    try {
      TickVoice(dt);
      for (int i = 0; i < _blocks.Count; i++) {
        if (_blocks[i] != null) _blocks[i].TickForTests(dt);
      }
      switch (Current) {
        case Phase.Intro: TickIntro(dt); break;
        case Phase.Demo: TickDemo(dt); break;
        case Phase.Handoff: TickHandoff(dt); break;
        case Phase.Building: TickBuilding(dt); break;
        case Phase.Success: TickSuccess(dt); break;
        case Phase.Correct: TickCorrect(dt); break;
      }
      TickActing(dt);
      TickCamera(dt);
      TickJuice(dt);
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
      Say(BlockEn[Target - 1], BlockVi[Target - 1]);
      PulseBoard(1.4f);
    }
    if (!_saidToday && t >= 7.2f) {
      _saidToday = true;
      FaceTowards(_teacher, PadLocal(), dt, 4f);
      Say("Build a tower of " + N(Target) + "!", "Xây tháp " + Nvi(Target) + " khối nhé!");
      Point(_teacher, PadWorld(), 2.8f);
    }
    if (!_saidBuild && t >= 10.2f) {
      _saidBuild = true;
      Wave(_teacher);
      Say("Let's build!", "Cùng xây nhé!");
    }
    if (t >= 11.4f) {
      To(Phase.Demo);
      SetShot(1);
      FaceTowards(_student, YardLocal(), dt, 4f);
    }
  }

  // ---- student demonstration ---------------------------------------------------
  // One block at a time, for real: walk to the yard -> pick (arc to the
  // student's fist) -> walk to the pad -> place (arc onto the stack, wooden
  // clack, tower grows) -> next. No teleport, no snaps.
  void TickDemo(float dt) {
    _phaseT += dt;
    if (!_saidWatch) {
      _saidWatch = true;
      Say("Watch your friend!", "Xem bạn làm nhé!");
      Point(_teacher, YardWorld(), 2.4f);
    }
    TowerBlock block = _demoPlaced < _blocks.Count ? _blocks[_demoPlaced] : null;
    switch (_demoStage) {
      case 0:
        FaceTowards(_student, YardLocal(), dt, 5f);
        if (_phaseT >= 1.2f && WalkTo(_student, BuildTowerBuilder.YardStand, dt)) {
          _demoStage = 1;
          if (block != null) {
            block.SetHand(StudentHand());
            FaceTowards(_student, YardLocal(), dt, 5f);
            block.BeginCarry(0.3f);
          }
        }
        break;
      case 1:
        if (block == null || block.State == TowerBlock.BlockState.Carried) {
          _demoStage = 2;
        }
        break;
      case 2:
        FaceTowards(_student, PadLocal(), dt, 5f);
        if (WalkTo(_student, BuildTowerBuilder.PadStand, dt)) {
          _demoStage = 3;
          if (block != null) {
            block.OnPlaced = OnDemoPlaced;
            block.BeginPlace(_demoPlaced, BuildTowerBuilder.StackSlot(_demoPlaced, BuildTowerBuilder.PadPos), 0.3f);
          }
        }
        break;
      case 3:
        if (block == null || block.State == TowerBlock.BlockState.Placed) {
          _demoStage = 4;
          _demoHoldT = DemoHold;
        }
        break;
      case 4:
        _demoHoldT -= dt;
        if (_demoHoldT <= 0f) {
          _demoPlaced++;
          if (_demoPlaced < Target) { _demoStage = 0; }
          else { _demoStage = 5; _demoHoldT = 1.6f; }
        }
        break;
      default:
        FaceTowards(_student, PlayerLocal(), dt, 3f);
        _demoHoldT -= dt;
        if (!_saidYes && _demoHoldT <= 1.0f) {
          _saidYes = true;
          Say("Yes! " + Cap(N(Target)) + " " + Blocks(Target) + "!",
            "Đúng rồi! " + Cap(Nvi(Target)) + " khối!");
          Point(_teacher, PadWorld(), 2.2f);
          CelebrateActor(_student, soft: true);
          Sparkle(PadWorld() + new Vector3(0f, BuildTowerBuilder.TowerTopY(Target), 0f), 8, 91, 0.4f);
        }
        if (_demoHoldT <= 0f) {
          StartResetYard();
          To(Phase.Handoff);
        }
        break;
    }
  }

  void OnDemoPlaced() {
    PlaySfx("block");
    int k = Mathf.Min(_demoPlaced + 1, Target);
    FeedFeedback(k);
    Say(BlockEn[k - 1], BlockVi[k - 1]);
    Point(_teacher, PadWorld(), 1.6f);
  }

  // The demo tower tidies back to the yard (gentle arcs, staggered) so the
  // child starts from an empty pad and builds the tower themselves.
  void StartResetYard() {
    _resetQueue.Clear();
    _resetT = 0f;
    for (int i = 0; i < _placed.Count; i++) {
      TowerBlock b = _placed[i];
      if (b != null) _resetQueue.Enqueue(b);
    }
    _placed.Clear();
    Count = 0;
    if (_ghost != null) _ghost.SetActive(false);
  }

  void TickResetYard(float dt) {
    if (_resetQueue.Count <= 0) return;
    _resetT -= dt;
    if (_resetT > 0f) return;
    _resetT = 0.09f;
    TowerBlock b = _resetQueue.Dequeue();
    if (b != null && b.State != TowerBlock.BlockState.Available) {
      b.SetHand(_playerHand);
      b.OnPlaced = null;
      b.BeginReturnHome();
    }
  }

  // Safety net: by the time the child gets control, every block is pickable.
  void FinishResetYard() {
    _resetQueue.Clear();
    for (int i = 0; i < _blocks.Count; i++) {
      TowerBlock b = _blocks[i];
      if (b == null) continue;
      b.SetHand(_playerHand);
      b.OnPlaced = null;
      if (b.State != TowerBlock.BlockState.Available) b.ResetHome();
    }
    Count = 0;
    _placed.Clear();
  }

  // ---- handoff ------------------------------------------------------------------

  void TickHandoff(float dt) {
    _phaseT += dt;
    float t = _phaseT;
    TickResetYard(dt);
    if (!_saidTurn && t >= 0.4f) {
      _saidTurn = true;
      FaceTowards(_teacher, PlayerLocal(), dt, 5f);
      Say("Now it's your turn!", "Giờ đến lượt con!");
    }
    if (!_saidTask && t >= 2.4f) {
      _saidTask = true;
      Say("Build a tower of " + N(Target) + "!", "Xây tháp " + Nvi(Target) + " khối nhé!");
      Point(_teacher, PadWorld(), 2.6f);
    }
    // The student walks back beside the teacher so he never blocks the pad.
    if (!_studentReturned) {
      if (WalkTo(_student, BuildTowerBuilder.StudentReturn, dt)) _studentReturned = true;
      if (t >= 8.0f) _studentReturned = true; // safety: the lesson never stalls
    } else {
      FaceTowards(_student, PadLocal(), dt, 2.5f);
    }
    if (!_followHanded && t >= 4.2f) {
      _followHanded = true;
      Follow();
      if (_life != null) { try { _life.Begin("handoff done"); } catch (Exception) { } }
    }
    if (_followHanded && Current == Phase.Handoff && (_studentReturned || t >= 8.0f)) {
      FinishResetYard();
      To(Phase.Building);
      _lastPos = _player != null ? _player.position : Vector3.zero;
      try { Debug.Log("[BuildTowerGame] child control (building phase).", this); } catch (Exception) { }
    }
  }

  // ---- the child's build ---------------------------------------------------------
  // Real actions in the world: click a block (walk -> bend -> carry in the
  // fist) -> click the pad or stop beside it (reach -> the block flies onto
  // the next slot -> the tower grows -> the teacher counts).

  public void TryPick(TowerBlock block) {
    if (block == null || !block.IsAvailable) return;
    if (Current != Phase.Building && Current != Phase.Success) return;
    if (Carried != null) return; // one block at a time
    Carried = block;
    block.SetHand(_playerHand);
    if (_viz != null) {
      _viz.FaceTowards(block.transform.position, true);
      _viz.PlayPickup();
    }
    PlaySfx("pickup");
    Sparkle(block.transform.position, 6, _sparkleSeed++, 0.3f);
    block.BeginCarry(PickDelay);
    ShowGhost();
  }

  public void TryPlace() {
    if (Carried == null) return;
    if (Current != Phase.Building && Current != Phase.Success) return;
    // The block must really be in the hand: a pad click during the pickup bend
    // is ignored (the child clicks again; proximity re-fires on arrival).
    if (Carried.State != TowerBlock.BlockState.Carried) return;
    TowerBlock block = Carried;
    Carried = null;
    if (_viz != null) {
      _viz.FaceTowards(PadWorld(), true);
      _viz.PlayPickup();
    }
    HideGhost();
    if (Count < Target) {
      // The real stack grows BY ONE: the slot index is decided here, once.
      int index = Count;
      Count++;
      _placed.Add(block);
      block.OnPlaced = OnBlockPlaced;
      block.BeginPlace(index, BuildTowerBuilder.StackSlot(index, BuildTowerBuilder.PadPos), PlaceDelay);
      _landBeats = (Count == Target) ? 2 : 1; // the beat waits for the real landing
    } else {
      // CORRECTION path: one too many — a counting lesson, never a punishment.
      // The extra block visibly lands on top first, then goes home after the
      // teacher explains.
      _extra = block;
      block.OnPlaced = OnExtraLanded;
      block.BeginPlace(Target, BuildTowerBuilder.StackSlot(Target, BuildTowerBuilder.PadPos), PlaceDelay);
      To(Phase.Correct);
      _correctStep = 0;
      _correctT = 0f;
      Overshoots++;
    }
  }

  void OnBlockPlaced() {
    PlaySfx("block");
    Sparkle(PadWorld() + new Vector3(0f, BuildTowerBuilder.TowerTopY(Count) + 0.1f, 0f), 10, _sparkleSeed++, 0.5f);
    PulseLastPlaced();
    if (_landBeats == 1) {
      _landBeats = 0;
      FeedFeedback(Count);
      Say(BlockEn[Mathf.Min(Count, Target) - 1], BlockVi[Mathf.Min(Count, Target) - 1]);
      Point(_teacher, PadWorld(), 1.6f);
    } else if (_landBeats == 2) {
      _landBeats = 0;
      if (Current != Phase.Building) return;
      SuccessBeats();
    }
  }

  // The overshoot block also LANDS (visibly on top), then waits for the
  // teacher's count before going home.
  void OnExtraLanded() {
    PlaySfx("block");
    PulseLastPlaced();
  }

  // Place feedback: the tower reads "this is ONE piece higher" without UI.
  void FeedFeedback(int k) {
    if (_ghost != null && Carried == null && Current != Phase.Success) _ghost.SetActive(false);
  }

  void PulseLastPlaced() {
    if (_placed.Count <= 0) return;
    TowerBlock b = _placed[_placed.Count - 1];
    if (b == null) return;
    _pulsing = b;
    _pulseT = 0.4f;
  }

  void SuccessBeats() {
    To(Phase.Success);
    PlaySfx("success");
    Sparkle(PadWorld() + new Vector3(0f, BuildTowerBuilder.TowerTopY(Target) + 0.2f, 0f),
      14, _sparkleSeed++, 0.8f);
    Say(Cap(N(Target)) + " " + Blocks(Target) + "! Well done!",
      Cap(Nvi(Target)) + " khối! Giỏi!");
    // The recap 1..Target waits its turn in the pacer's single slot (same
    // discipline as #2/#3): one line in flight, the rest wait.
    _recapEn.Clear();
    _recapVi.Clear();
    for (int i = 1; i <= Target; i++) {
      _recapEn.Enqueue(BlockEn[i - 1]);
      _recapVi.Enqueue(BlockVi[i - 1]);
    }
    Point(_teacher, PadWorld(), 2.0f);
    CelebrateBoth();
    if (_result != null) {
      _result.SetActive(true);
      _result.transform.localScale = Vector3.one * 0.65f;
      _resultPopT = 0f;
    }
    SetShot(2);
    _victoryT = 1.35f;
    MarkLifeCompleted();
    if (_onCompleted != null) {
      try { _onCompleted(Target); } catch (Exception) { }
    }
  }

  void MarkLifeCompleted() {
    if (_life == null) return;
    try {
      if (_life.State != ActivityState.Completed) _life.MarkCompleted("built the tower " + Target);
    } catch (Exception) { }
  }

  void TickBuilding(float dt) {
    _phaseT += dt;
    if (_overshootNagT > 0f) _overshootNagT -= dt;
    if (_underCooldownT > 0f) _underCooldownT -= dt;
    TickPlaceProximity();
    TickGhost(dt);
    // Undershoot nudge: settled BELOW the target earns a gentle "how many
    // more" — near the yard or the pad only, never across the arena, never spam.
    bool moving = IsPlayerMoving();
    if (Count < Target && !moving && NearWork()) {
      _underT += dt;
      if (_underT >= UnderDwell && _underCooldownT <= 0f) {
        _underCooldownT = UnderNudgeCooldown;
        _underT = 0f;
        UndershootNudges++;
        int remain = Target - Count;
        Say(Cap(N(remain)) + " more " + Blocks(remain) + "!",
          "Còn " + Nvi(remain) + " khối nữa nhé!");
        Point(_teacher, Count == 0 ? YardWorld() : PadWorld(), 2.0f);
        Log("undershoot at " + Count + " (" + remain + " more)");
      }
    } else {
      _underT = 0f;
    }
    _lastPos = _player != null ? _player.position : Vector3.zero;
    FaceTowards(_teacher, PlayerLocal(), dt, 2.2f);
    FaceTowards(_student, PlayerLocal(), dt, 2.2f);
  }

  // "Stop, then place" (same discipline as #1/#3): walking PAST the pad must
  // never fling the block out of the hand — the child stops first.
  void TickPlaceProximity() {
    if (Carried == null || _player == null) return;
    if (_mover != null && _mover.IsMoving) return;
    if (PlayerNear(PadWorld(), 1.6f)) TryPlace();
  }

  // The ghost slot marker: rides the NEXT stack slot while a block is carried
  // (placement preview only — placement still needs the confirm interaction).
  void ShowGhost() {
    if (_ghost == null) return;
    _ghost.SetActive(true);
    _ghost.transform.localPosition = BuildTowerBuilder.StackSlot(Count, BuildTowerBuilder.PadPos)
      + new Vector3(0f, 0.02f, 0f);
  }

  void HideGhost() {
    if (_ghost != null) _ghost.SetActive(false);
  }

  void TickGhost(float dt) {
    if (_ghost == null || !_ghost.activeSelf) return;
    _ghostPulseT += dt;
    float s = 1f + 0.08f * Mathf.Sin(_ghostPulseT * 5f);
    _ghost.transform.localScale = new Vector3(0.64f * s, 0.01f, 0.64f * s);
  }

  bool NearWork() {
    return PlayerNear(YardWorld(), UnderNearXZ) || PlayerNear(PadWorld(), UnderNearXZ);
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
    TickPlaceProximity(); // the field stays open: a spare block feeds the lesson
    TickGhost(dt);
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
  // The extra block landed on top; the teacher counts the tower 1..N, points at
  // the board ("the board says N"), names the limit ("N is enough"), and the
  // block hops home. Then Success again — gently, like a clean run.

  void TickCorrect(float dt) {
    _correctT += dt;
    if (_correctStep < 0) return;
    if (_correctStep == 0 && _correctT >= 0.4f) {
      _correctStep = 1;
      Say("Let's count again!", "Cùng đếm lại nhé!");
      Point(_teacher, PadWorld(), 2.2f);
      return;
    }
    if (_correctStep >= 1 && _correctStep <= Target) {
      float at = 0.4f + _correctStep * 1.7f;
      if (_correctT >= at) {
        int k = _correctStep;
        _correctStep++;
        Say(BlockEn[k - 1], BlockVi[k - 1]);
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
      Say(Cap(N(Target)) + " is enough.", "Đủ " + Nvi(Target) + " khối rồi.");
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
    Count = Target;
    _placed.Clear();
    for (int i = 0; i < _blocks.Count; i++) {
      TowerBlock b = _blocks[i];
      if (b == null) continue;
      if (b.State == TowerBlock.BlockState.Placed && b.PlacedIndex >= 0 && b.PlacedIndex < Target) {
        _placed.Add(b);
      }
    }
    // Every block beyond the target goes home, so the tower ends exactly the
    // board's height — whatever the child picked.
    for (int i = 0; i < _blocks.Count; i++) {
      TowerBlock b = _blocks[i];
      if (b == null) continue;
      if (b.State != TowerBlock.BlockState.Available
          && !(b.State == TowerBlock.BlockState.Placed && b.PlacedIndex < Target)) {
        b.BeginReturnHome();
      }
    }
    if (_result != null) {
      _result.SetActive(true);
      _result.transform.localScale = Vector3.one * 0.65f;
      _resultPopT = 0f;
    }
    Say(Cap(N(Target)) + " " + Blocks(Target) + "! Well done!",
      Cap(Nvi(Target)) + " khối! Giỏi!");
    CelebrateBoth();
    MarkLifeCompleted();
    if (_onCompleted != null) {
      try { _onCompleted(Target); } catch (Exception) { }
    }
    To(Phase.Success);
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
    if (_shot == 2) {
      // Success framing follows the REAL tower height (brief §18): the look
      // point sits at the tower's mid-height and the camera rises/pulls back
      // proportionally — one formula for every target, never a target-9 hack.
      float top = BuildTowerBuilder.TowerTopY(Target);
      Vector3 camBase = _camSuccess != null ? _camSuccess.position : PadWorld() + new Vector3(0.6f, 2.0f, -3.7f);
      Vector3 pos = camBase + new Vector3(0f, top * 0.15f, -top * 0.20f);
      Vector3 look = PadWorld() + new Vector3(0f, BuildTowerBuilder.PadTopY + top * 0.5f, 0f);
      _shotIssued = true;
      _shotT = 2.4f;
      try { _cam.FramePointFor(pos, look, 2.8f); } catch (Exception) { }
      return;
    }
    Transform c, l;
    if (_shot == 1) { c = _camDemo; l = _lookDemo; }
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
    try { Debug.Log("[BuildTowerGame] camera returned to follow.", this); } catch (Exception) { }
  }

  // ---- actor motion / gestures (same acting language as #1-#3) ----------------------

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
    // Landing pulse on the block that just landed (transform-only).
    if (_pulseT > 0f) {
      _pulseT -= dt;
      if (_pulsing != null) {
        float k = Mathf.Clamp01(_pulseT / 0.4f);
        float s = 1f + 0.18f * Mathf.Sin(k * Mathf.PI);
        _pulsing.transform.localScale = new Vector3(
          BuildTowerBuilder.BlockSize.x * s, BuildTowerBuilder.BlockSize.y * s, BuildTowerBuilder.BlockSize.z * s);
      }
      if (_pulseT <= 0f && _pulsing != null) {
        _pulsing.transform.localScale = BuildTowerBuilder.BlockSize;
        _pulsing = null;
      }
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
    try { Debug.Log("[BuildTowerGame] " + message, this); } catch (Exception) { }
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

  Vector3 PadWorld() {
    return _padAnchor != null ? _padAnchor.position
      : (_root != null ? _root.TransformPoint(BuildTowerBuilder.PadPos) : Vector3.zero);
  }
  Vector3 PadLocal() { return LocalPoint(PadWorld()); }

  Vector3 YardWorld() {
    return _root != null ? _root.TransformPoint(BuildTowerBuilder.YardCenter) : Vector3.zero;
  }
  Vector3 YardLocal() { return LocalPoint(YardWorld()); }

  Vector3 PlayerLocal() {
    return _player != null ? LocalPoint(_player.position) : PadLocal();
  }
  Vector3 TeacherLocal() {
    return _teacher != null && _teacher.Root != null
      ? _teacher.Root.transform.localPosition + new Vector3(0f, 1.1f, 0f)
      : LocalPoint(BuildTowerBuilder.TeacherStart);
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
