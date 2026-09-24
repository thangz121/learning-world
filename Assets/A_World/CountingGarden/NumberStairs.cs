// A_World/CountingGarden/NumberStairs.cs — S3-P2Z12 GAMEPLAY #2 (+9-step round)
// "BẬC THANG CON SỐ" (number stairs). The Counting Garden's sixth plot activity,
// staged in its OWN lazy scene (StairPlayScene).
// The experience: the teacher links the TARGET NUMBER to that many steps at the
// board -> the child student walks up and counts 1..N as a demonstration ->
// handover ("Now it's your turn!") -> the CHILD climbs, the teacher counts with
// them, and the activity succeeds when the child stands on step N; climbing past
// it is never a punishment (the teacher calls them back and counts again), and
// stopping short earns a gentle "how many more" nudge, never a fail.
// One staircase (MAXIMUM = 9, brief §2), one mechanic, many targets: the target
// only decides which step the child must stop on (progression owned by the
// area: 3 -> 5 -> 7 -> 9 -> 1). No confetti on success (brief §28): glow +
// sound + NPC reaction only.
// Architecture (brief §0/§1, §40): scene-local component, no manager/singleton,
// no new bus/service; reuses LessonActors (body kit shared with gameplay #1),
// PacedVoice (paced speech), StairRun (deterministic step identity), DemoJuice
// (FX), SmartCamera beats, ActivityLifecycle (owned by the Math-side area),
// MicroWorldPortal (the door). C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NumberStairs : MonoBehaviour {
  public enum Phase {
    Intro,    // teacher links the board's 3 to three steps
    Demo,     // the student walks up and counts 1, 2, 3
    Handoff,  // "Now it's your turn!" + camera returns to the child
    Climb,    // the child climbs; step identity drives feedback + success
    Success,  // standing on step 3 — celebrated, activity settled
  }

  // Beat timings (one place; deterministic for Step(dt) tests).
  const float SuccessDwell = 0.9f;      // "stand still on the target step" window
  const float StepHold = 0.7f;          // teacher count beat between student steps
  const float OvershootNagCooldown = 5f;
  const float UnderNudgeCooldown = 8f;  // undershoot hint spacing (brief §20)
  const float UnderDwell = 2.0f;        // settled-below-target before a nudge
  const float StepSettleSeconds = 0.15f; // band must hold this long to commit
                                        // (brief §16: filters frame-scale flicker, but a steadily
                                        // walking child (~0.3s per tread at 2.2m/s) still counts
                                        // every step — counts must never be skipped mid-climb)
  const float UnderNearStairsXZ = 4.5f; // base nudge only near the stair foot
  const float WalkSpeed = 0.85f;        // student legs
  public static readonly Vector3 FollowOffset = StairHillBuilder.FollowOffset;

  // Number words (brief §17): Vietnamese first; English prepared alongside.
  // Every composed line stays inside the SafetyFilter NPC cap (<= 6 tokens).
  static readonly string[] NumEn = {
    "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
  static readonly string[] NumVi = {
    "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
  static readonly string[] CountEn = {
    "One.", "Two.", "Three.", "Four.", "Five.", "Six.", "Seven.", "Eight.", "Nine." };
  static readonly string[] CountVi = {
    "Một.", "Hai.", "Ba.", "Bốn.", "Năm.", "Sáu.", "Bảy.", "Tám.", "Chín." };

  static int ClampN(int i) { return Mathf.Clamp(i, 1, StairHillBuilder.StepCount); }
  static string N(int i) { return NumEn[ClampN(i) - 1]; }
  static string Nvi(int i) { return NumVi[ClampN(i) - 1]; }
  // English counts the noun; Vietnamese "bậc" never changes.
  static string Steps(int n) { return ClampN(n) == 1 ? "step" : "steps"; }
  static string Cap(string s) {
    return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
  }

  public Phase Current { get; private set; } = Phase.Intro;
  public int Target { get; private set; } = StairHillBuilder.Target;
  public int CurrentStep { get; private set; }
  public int HighestStep { get; private set; }
  public int Overshoots { get; private set; }
  public int UndershootNudges { get; private set; }
  public bool IntroDone { get { return Current != Phase.Intro; } }
  public bool ResultShown { get { return _result != null && _result.activeSelf; } }
  public bool DemoStarted { get { return Current != Phase.Intro; } }
  public int DemoStepsClimbed { get { return _demoStep; } }

  StairHillBuilder _builder;
  StairRun _run;
  Transform _root;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PlayerVisual _viz;
  ClickToMove _mover;

  LessonActor _teacher;
  LessonActor _student;
  PacedVoice _voice;
  Transform _fx;

  GameObject _board;
  GameObject _result;
  GameObject[] _stepCues;
  Transform _camTeaching, _lookTeaching, _camDemo, _lookDemo, _camSuccess, _lookSuccess;

  float _phaseT;
  float _dwellT;
  float _shotT = -1f;
  bool _shotIssued;
  bool _cameraDone; // camera handed back to the child for good
  int _shot; // 0 teaching, 1 demo, 2 success

  // Intro flags.
  bool _saidBoard, _saidNumber, _saidThree, _saidToday, _saidCount;
  // Demo sub-state.
  int _demoStage;      // 0 walk to base, 1 walking up, 2 done
  int _demoStep;       // last step the student reached (1..3)
  float _demoHoldT;
  bool _saidWatch, _saidYes;
  // Handoff flags.
  bool _saidTurn, _saidClimb, _followHanded, _studentReturned;
  int _studentLeg;
  bool _successHanded;
  // Climb flags.
  float _overshootNagT;
  float _underCooldownT;
  float _underT;
  int _pendingStep;
  float _pendingT;
  float _victoryT = -1f;
  float _resultPopT = 1f;
  Vector3 _lastPos;
  // The recap 1..Target after success: PacedVoice is newest-wins, so the ORDER
  // is owned here — one line in flight, the rest wait (brief §28).
  readonly Queue<string> _recapEn = new Queue<string>();
  readonly Queue<string> _recapVi = new Queue<string>();

  public void Build(StairHillBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life, int target = 0) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    Target = StairHillBuilder.ClampTarget(target <= 0 ? StairHillBuilder.Target : target);
    _viz = player != null ? player.GetComponent<PlayerVisual>() : null;
    _mover = player != null ? player.GetComponent<ClickToMove>() : null;
    if (_builder == null) {
      Debug.LogWarning("[NumberStairs] no builder; activity parked.", this);
      return;
    }
    _run = _builder.Stairs;
    _root = _builder.transform;
    _board = _builder.NumberBoard;
    _result = _builder.Result;
    _stepCues = _builder.StepCues;
    _camTeaching = _builder.CamTeaching;
    _lookTeaching = _builder.LookTeaching;
    _camDemo = _builder.CamDemo;
    _lookDemo = _builder.LookDemo;
    _camSuccess = _builder.CamSuccess;
    _lookSuccess = _builder.LookSuccess;

    BuildActors();
    GameObject fx = new GameObject("NSFx");
    fx.transform.SetParent(_root, false);
    _fx = fx.transform;

    // Re-entry policy FIRST (same lesson as gameplay #1): a completed activity
    // adopts its finished picture without replaying the intro/demo, and the
    // shared lifecycle lives in MathScene so it survives this scene's unload.
    if (_life != null && _life.State == ActivityState.Completed) {
      ApplyCompletedState("adopt");
      return;
    }
    if (_life != null) {
      try {
        _life.MarkAvailable("number_stairs staged");
        _life.BeginEnter("stair scene built");
        _life.MarkReady("intro staged");
      } catch (Exception) { }
    }
    _lastPos = _player != null ? _player.position : Vector3.zero;
    try { Debug.Log("[NumberStairs] activity staged (intro will play).", this); } catch (Exception) { }
  }

  void BuildActors() {
    _teacher = LessonActors.Build(_root, "NSTeacher", "NpcVisuals/TessVisual", 0.5f,
      StairHillBuilder.TeacherStart, new Color(0.25f, 0.45f, 0.85f),
      new Color(0.98f, 0.78f, 0.25f));
    _student = LessonActors.Build(_root, "NSStudent", "NpcVisuals/MiloVisual", 0.42f,
      StairHillBuilder.StudentStart, new Color(0.30f, 0.62f, 0.45f),
      new Color(0.55f, 0.35f, 0.20f));
    _voice = new PacedVoice();
    _voice.Audio = _audio;
    _voice.LogTag = "NumberStairs";
    if (_teacher != null && _teacher.Root != null) FaceSnap(_teacher, BoardLocal());
    if (_student != null && _student.Root != null) FaceSnap(_student, TeacherLocal());
    if (_builder != null && _builder.Result != null) _builder.Result.SetActive(false);
  }

  // Terminal adopt: skip the lesson, show the finished picture (result board +
  // observing actors); the child may still climb freely.
  void ApplyCompletedState(string reason) {
    Current = Phase.Success;
    _phaseT = 0f;
    _followHanded = true;
    _cameraDone = true;
    if (_result != null) _result.SetActive(true);
    if (_teacher != null && _teacher.Root != null) {
      _teacher.Root.transform.localPosition = StairHillBuilder.TeacherStart;
      FaceSnap(_teacher, StairsLocal());
    }
    if (_student != null && _student.Root != null) {
      _student.Root.transform.localPosition = StairHillBuilder.StudentReturn;
      FaceSnap(_student, StairsLocal());
    }
    if (_player != null) _lastPos = _player.position;
    try { Debug.Log("[NumberStairs] adopted COMPLETED state (" + reason + ").", this); } catch (Exception) { }
  }

  void Update() { Tick(Time.deltaTime); }

  // Deterministic tick (EditMode cover: no live frame needed).
  public void Tick(float dt) {
    if (dt <= 0f || _builder == null) return;
    try {
      TickVoice(dt);
      switch (Current) {
        case Phase.Intro: TickIntro(dt); break;
        case Phase.Demo: TickDemo(dt); break;
        case Phase.Handoff: TickHandoff(dt); break;
        case Phase.Climb: TickClimb(dt); break;
        case Phase.Success: TickSuccess(dt); break;
      }
      TickActing(dt);
      TickCamera(dt);
      TickJuice(dt);
    } catch (Exception) { }
  }

  void TickVoice(float dt) { if (_voice != null) _voice.Tick(dt); }

  void To(Phase next) { Current = next; _phaseT = 0f; }

  // ---- teacher intro (brief §6) --------------------------------------------------
  // The board holds the round's target (brief §9): every line below is composed
  // from it, so the SAME script teaches 1..9 without a second lesson.

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
    if (!_saidThree && t >= 5.6f) {
      _saidThree = true;
      Say(CountEn[Target - 1], CountVi[Target - 1]);
      PulseBoard(1.4f);
    }
    if (!_saidToday && t >= 7.2f) {
      _saidToday = true;
      FaceTowards(_teacher, StairsLocal(), dt, 4f);
      Say("Today, we climb " + N(Target) + " " + Steps(Target) + ".",
        "Hôm nay leo " + Nvi(Target) + " bậc.");
      Point(_teacher, StairsWorld(), 2.8f);
    }
    if (!_saidCount && t >= 10.2f) {
      _saidCount = true;
      Wave(_teacher);
      Say("Let's count!", "Cùng đếm nhé!");
    }
    if (t >= 11.4f) {
      To(Phase.Demo);
      SetShot(1);
      FaceTowards(_student, StairsLocal(), dt, 4f);
    }
  }

  // ---- student demonstration (brief §7-§9) ---------------------------------------
  // Sub-state: 0 = walk to the stair foot, 1 = walking onto step _demoStep,
  // 2 = hold the count beat on that tread, 3 = confirm + prepare the handover.
  void TickDemo(float dt) {
    _phaseT += dt;
    if (!_saidWatch) {
      _saidWatch = true;
      Say("Watch your friend!", "Xem bạn làm nhé!");
      Point(_teacher, StairsWorld(), 2.4f);
    }
    switch (_demoStage) {
      case 0:
        FaceTowards(_student, StairsLocal(), dt, 5f);
        if (_phaseT >= 1.2f && WalkTo(_student, StairHillBuilder.StudentBase, dt)) {
          _demoStage = 1;
          _demoStep = 1;
        }
        break;
      case 1:
        FaceTowards(_student, StairsLocal(), dt, 5f);
        if (WalkTo(_student, StepLocal(_demoStep), dt)) {
          PlaySfx("step");
          PulseStep(_demoStep);
          Point(_teacher, StepWorld(_demoStep), 1.8f);
          // One step = one count, from the same tables as the child's climb.
          Say(CountEn[_demoStep - 1], CountVi[_demoStep - 1]);
          _demoStage = 2;
          _demoHoldT = StepHold;
        }
        break;
      case 2:
        _demoHoldT -= dt;
        if (_demoHoldT <= 0f) {
          if (_demoStep < Target) { _demoStep++; _demoStage = 1; }
          else { _demoStage = 3; _demoHoldT = 1.4f; }
        }
        break;
      default:
        FaceTowards(_student, PlayerLocal(), dt, 3f);
        _demoHoldT -= dt;
        if (!_saidYes && _demoHoldT <= 1.0f) {
          _saidYes = true;
          Say("Yes! " + Cap(N(Target)) + " " + Steps(Target) + "!",
            "Đúng rồi! " + Cap(Nvi(Target)) + " bậc!");
          Point(_teacher, StepWorld(Target), 2.2f);
          CelebrateActor(_student, soft: true);
          Sparkle(StepWorld(Target) + new Vector3(0f, 0.2f, 0f), 8, 91, 0.4f);
        }
        if (_demoHoldT <= 0f) To(Phase.Handoff);
        break;
    }
  }

  // The student's per-step target is the NEXT stand point; after reaching step
  // N the demo advances by incrementing _demoStep on the following hold.
  Vector3 StepLocal(int step) { return _run != null ? _run.StandLocal(step) : Vector3.zero; }
  Vector3 StepWorld(int step) { return _root != null ? _root.TransformPoint(StepLocal(step)) : Vector3.zero; }

  // ---- handoff (brief §10/§28) ---------------------------------------------------

  void TickHandoff(float dt) {
    _phaseT += dt;
    float t = _phaseT;
    if (!_saidTurn && t >= 0.4f) {
      _saidTurn = true;
      FaceTowards(_teacher, PlayerLocal(), dt, 5f);
      Say("Now it's your turn!", "Giờ đến lượt con!");
    }
    if (!_saidClimb && t >= 2.4f) {
      _saidClimb = true;
      Say("Climb " + N(Target) + " " + Steps(Target) + "!",
        "Con lên " + Nvi(Target) + " bậc nhé!");
      Point(_teacher, StairsWorld(), 2.6f);
    }
    // The student walks OFF the stairs — down to the base, then beside the
    // teacher — so he is never standing in the child's climb path (journey
    // shot: he used to stop mid-staircase and the child walked through him).
    if (!_studentReturned) {
      if (_studentLeg == 0) {
        if (WalkTo(_student, StairHillBuilder.StudentBase, dt)) _studentLeg = 1;
      } else if (WalkTo(_student, StairHillBuilder.StudentReturn, dt)) {
        _studentReturned = true;
      }
      if (t >= 8.0f) _studentReturned = true; // safety: the lesson never stalls
    } else {
      FaceTowards(_student, StairsLocal(), dt, 2.5f);
    }
    // Camera back to the child at 4.2s; control is theirs — but only once the
    // student has actually cleared the stairs (movement was never locked).
    if (!_followHanded && t >= 4.2f) {
      _followHanded = true;
      Follow();
      if (_life != null) { try { _life.Begin("handoff done"); } catch (Exception) { } }
    }
    if (_followHanded && Current == Phase.Handoff && (_studentReturned || t >= 8.0f)) {
      To(Phase.Climb);
      _lastPos = _player != null ? _player.position : Vector3.zero;
      try { Debug.Log("[NumberStairs] child control (climb phase).", this); } catch (Exception) { }
    }
  }

  // ---- the child's climb (brief §11-§21) -----------------------------------------
  // The polled band only COMMITS after holding StepSettleSeconds (brief §16):
  // a body straddling a tread boundary must not count, un-count, and re-count
  // every frame — each ENTER commits exactly one count.

  void TickClimb(float dt) {
    _phaseT += dt;
    if (_run == null || _player == null) return;
    if (_overshootNagT > 0f) _overshootNagT -= dt;
    if (_underCooldownT > 0f) _underCooldownT -= dt;
    int band = _run.StepAt(_player.position);
    if (band == CurrentStep) {
      _pendingStep = band;
      _pendingT = 0f;
    } else if (band == _pendingStep) {
      _pendingT += dt;
      if (_pendingT >= StepSettleSeconds) CommitStep(band);
    } else {
      _pendingStep = band;
      _pendingT = 0f;
    }
    // Confirm only a STABLE stand on exactly the target step (brief §18/§29:
    // no CHECK button — the world notices, then waits ~1s).
    bool moving = IsPlayerMoving();
    if (CurrentStep == Target && !moving) {
      _dwellT += dt;
      if (_dwellT >= SuccessDwell) Success();
    } else {
      _dwellT = 0f;
    }
    // Undershoot nudge (brief §20): settled BELOW the target earns a gentle
    // "how many more" — never pointing at the target step itself, never spam.
    if (CurrentStep >= 0 && CurrentStep < Target && !moving
        && (CurrentStep >= 1 || NearStairFoot())) {
      _underT += dt;
      if (_underT >= UnderDwell && _underCooldownT <= 0f) {
        _underCooldownT = UnderNudgeCooldown;
        _underT = 0f;
        UndershootNudges++;
        int remain = Target - CurrentStep;
        Say(Cap(N(remain)) + " more " + Steps(remain) + "!",
          "Còn " + Nvi(remain) + " bậc nữa nhé!");
        Point(_teacher, StairsWorld(), 2.0f);
        Log("undershoot at step " + CurrentStep + " (" + remain + " more)");
      }
    } else {
      _underT = 0f;
    }
    _lastPos = _player.position;
    // The teacher keeps watching the child.
    FaceTowards(_teacher, PlayerLocal(), dt, 2.2f);
    FaceTowards(_student, PlayerLocal(), dt, 2.2f);
  }

  void CommitStep(int step) {
    int prev = CurrentStep;
    CurrentStep = step;
    _pendingStep = step;
    _pendingT = 0f;
    OnStepChanged(prev, step);
  }

  // The base-of-stairs nudge only fires near the stair foot (not while the
  // child explores the entry plaza — no nagging across the arena).
  bool NearStairFoot() {
    if (_player == null || _run == null) return false;
    Vector3 p = _player.position;
    Vector3 foot = _root != null
      ? _root.TransformPoint(new Vector3(_run.centerX, 0f, _run.baseZ))
      : new Vector3(_run.centerX, 0f, _run.baseZ);
    float dx = p.x - foot.x, dz = p.z - foot.z;
    return dx * dx + dz * dz <= UnderNearStairsXZ * UnderNearStairsXZ;
  }

  void OnStepChanged(int prev, int step) {
    if (step > HighestStep) HighestStep = step;
    if (step > prev && step >= 1 && step <= Target) {
      // One step = one count (never accumulate: the count IS the current step).
      PlaySfx("step");
      PulseStep(step);
      Point(_teacher, StepWorld(step), 1.6f);
      Say(CountEn[step - 1], CountVi[step - 1]);
      Log("step " + step + " (count follows the feet)");
      return;
    }
    if (step > Target && step > prev) {
      // Overshoot CLIMBING PAST the target: guidance, never game over (brief
      // §19). Walking back DOWN through a high tread is not an overshoot (it is
      // the child correcting) — only upward passes count and nag.
      Overshoots++;
      if (_overshootNagT <= 0f) {
        _overshootNagT = OvershootNagCooldown;
        Say("We only need " + N(Target) + ".", "Mình chỉ cần " + Nvi(Target) + ".");
        Point(_teacher, StepWorld(Target), 2.2f);
        Say("Come back down to " + N(Target) + "!", "Quay lại bậc " + Nvi(Target) + " nhé!");
      }
      Log("overshoot to step " + step + " (guide back, no fail)");
      return;
    }
    if (step < prev) Log("back to step " + step + " (count follows the feet)");
  }

  bool IsPlayerMoving() {
    if (_mover != null) return _mover.IsMoving;
    if (_player == null) return false;
    Vector3 d = _player.position - _lastPos;
    d.y = 0f;
    return d.sqrMagnitude > 0.0004f;
  }

  void Success() {
    if (Current == Phase.Success) return;
    Current = Phase.Success;
    _phaseT = 0f;
    _dwellT = 0f;
    _underT = 0f;
    PlaySfx("success");
    // Confirm with the target itself, then RECOUNT 1..Target (brief §28:
    // "Hai... ba... bốn... năm!") — the recap lines wait their turn in the
    // pacer's single slot (TickRecap), never a burst.
    Say(Cap(N(Target)) + " " + Steps(Target) + "! Well done!",
      Cap(Nvi(Target)) + " bậc! Giỏi!");
    _recapEn.Clear();
    _recapVi.Clear();
    for (int i = 1; i <= Target; i++) {
      _recapEn.Enqueue(CountEn[i - 1]);
      _recapVi.Enqueue(CountVi[i - 1]);
    }
    Point(_teacher, StepWorld(Target), 2.2f);
    if (_teacher != null) { Wave(_teacher); CelebrateActor(_teacher, soft: false); }
    if (_student != null) CelebrateActor(_student, soft: false);
    Sparkle(StepWorld(Target) + new Vector3(0f, 0.25f, 0f), 12, 92, 0.55f);
    Sparkle(PlayerWorld() + new Vector3(0f, 0.9f, 0f), 10, 93, 0.5f);
    if (_result != null) {
      _result.SetActive(true);
      _result.transform.localScale = Vector3.one * 0.65f;
      _resultPopT = 0f;
    }
    SetShot(2);
    _victoryT = 0.9f; // the child's own little victory, after the reach settles
    if (_life != null) {
      try {
        if (_life.State != ActivityState.Completed)
          _life.MarkCompleted("stood on step " + Target);
      } catch (Exception) { }
    }
    Log("SUCCESS: child stands on step " + Target);
  }

  // One recap line in flight at a time (PacedVoice is newest-wins by design —
  // submitting the whole recap at once would keep only the last line).
  void TickRecap() {
    if (_recapEn.Count <= 0 || _voice == null || !_voice.Idle || _voice.HasLine) return;
    Say(_recapEn.Dequeue(), _recapVi.Dequeue());
  }

  void TickSuccess(float dt) {
    _phaseT += dt;
    TickRecap();
    FaceTowards(_teacher, PlayerLocal(), dt, 2f);
    FaceTowards(_student, PlayerLocal(), dt, 2f);
    // Hand the camera back after the celebration frame (the child plays on).
    // Once only: an unguarded call spammed Follow() every frame (journey log).
    if (_phaseT >= 4.6f && !_successHanded) {
      _successHanded = true;
      _cameraDone = true;
      Follow();
    }
  }

  // ---- camera (brief §27) --------------------------------------------------------

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
    if (_cameraDone) return; // hand-back happened (or adopt): never re-frame
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
    try { Debug.Log("[NumberStairs] camera returned to follow.", this); } catch (Exception) { }
  }

  // ---- actor motion / gestures (shared acting language with the lesson) -----------

  Vector3 LocalPoint(Vector3 world) {
    return _root != null ? _root.InverseTransformPoint(world) : world;
  }

  Vector3 BoardLocal() { return LocalPoint(BoardWorld()); }
  Vector3 StairsLocal() { return LocalPoint(StairsWorld()); }
  Vector3 PlayerLocal() { return _player != null ? LocalPoint(_player.position) : StairsLocal(); }
  Vector3 PlayerWorld() { return _player != null ? _player.position : StairsWorld(); }

  Vector3 BoardWorld() {
    return _root != null
      ? _root.TransformPoint(new Vector3(StairHillBuilder.TeacherStart.x - 1.0f, StairHillBuilder.BoardPointY, StairHillBuilder.TeacherStart.z + 0.3f))
      : Vector3.zero;
  }

  Vector3 StairsWorld() {
    if (_run != null) return _run.transform.TransformPoint(new Vector3(_run.centerX, StairHillBuilder.StairsPointY, _run.baseZ + StairHillBuilder.Tread));
    return _root != null ? _root.position : Vector3.zero;
  }

  Vector3 TeacherLocal() {
    return _teacher != null && _teacher.Root != null
      ? _teacher.Root.transform.localPosition + new Vector3(0f, 1.1f, 0f)
      : LocalPoint(StairHillBuilder.TeacherStart);
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

  // Walk in the LOCAL space including Y (the student physically climbs the
  // treads; no teleport — brief §7/§14). Arrives within 0.12m.
  bool WalkTo(LessonActor a, Vector3 targetLocal, float dt) {
    if (a == null || a.Root == null) return true;
    Vector3 p = a.Root.transform.localPosition;
    Vector3 flat = new Vector3(targetLocal.x - p.x, 0f, targetLocal.z - p.z);
    float dist = flat.magnitude;
    float dy = targetLocal.y - p.y;
    if (dist <= 0.12f && Mathf.Abs(dy) <= 0.06f) return true;
    FaceTowards(a, targetLocal, dt, 6f);
    float step = Mathf.Min(WalkSpeed * dt, Mathf.Max(dist, Mathf.Abs(dy)));
    Vector3 dir = dist > 0.0001f ? flat / dist : Vector3.zero;
    Vector3 next = new Vector3(p.x + dir.x * Mathf.Min(step, dist), p.y, p.z + dir.z * Mathf.Min(step, dist));
    // Follow the tread profile smoothly (climb or descend) instead of snapping.
    next.y = Mathf.MoveTowards(p.y, targetLocal.y, WalkSpeed * dt * 0.9f);
    a.Root.transform.localPosition = next;
    _walkT += dt;
    try {
      if (a.Visual != null) {
        Vector3 v = a.Visual.localPosition;
        v.y = 0.02f + Mathf.Abs(Mathf.Sin(_walkT * 9f)) * 0.05f;
        a.Visual.localPosition = v;
      }
    } catch (Exception) { }
    return false;
  }

  float _walkT;

  void Wave(LessonActor a) { if (a != null) a.WaveT = 1.4f; }

  void Point(LessonActor a, Vector3 worldTarget, float seconds) {
    if (a == null || _root == null) return;
    a.PointTarget = LocalPoint(worldTarget);
    a.PointT = Mathf.Max(0.4f, seconds);
  }

  void CelebrateActor(LessonActor a, bool soft) {
    if (a == null) return;
    Trigger(a, "Celebrate");
    if (a.Face != null) a.Face.PulseExpression(CharacterExpression.Happy, 3f);
    // Brief §28: success reads through glow + sound + NPC reaction — NO
    // confetti, no giant UI. The hop + squash carry the joy.
    try { a.Face.PlayHop(); } catch (Exception) { }
    a.SquashT = 0.35f;
  }

  void Trigger(LessonActor a, string name) {
    try { if (a != null && a.Animator != null) a.Animator.SetTrigger(name); } catch (Exception) { }
  }

  // Per-frame gesture ticks (wave / point / nod / squash): the same procedural
  // bone language as gameplay #1, driven off the shared LessonActor data.
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

  // ---- juice / cues ----------------------------------------------------------------

  void TickJuice(float dt) {
    // Result board pop-in.
    if (_result != null && _result.activeSelf && _resultPopT < 1f) {
      _resultPopT = Mathf.Min(1f, _resultPopT + dt / 0.3f);
      float s = Mathf.Lerp(0.65f, 1f, Mathf.SmoothStep(0f, 1f, _resultPopT));
      try { _result.transform.localScale = Vector3.one * s; } catch (Exception) { }
    }
    // The child's own victory hop once the stand has settled.
    if (_victoryT > 0f) {
      _victoryT -= dt;
      if (_victoryT <= 0f && _viz != null && !IsPlayerMoving()) {
        _victoryT = -1f;
        try { _viz.PlayVictory(); } catch (Exception) { }
      }
    }
    // Step cue pulses (transform-only bead rows).
    if (_stepPulse > 0f) {
      _stepPulse -= dt;
      if (_pulsingCue != null) {
        float k = Mathf.Clamp01(_stepPulse / 0.5f);
        float s = 1f + 0.35f * Mathf.Sin(k * Mathf.PI);
        _pulsingCue.transform.localScale = new Vector3(s, s, s);
      }
      if (_stepPulse <= 0f && _pulsingCue != null) {
        _pulsingCue.transform.localScale = Vector3.one;
        _pulsingCue = null;
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
  }

  GameObject _pulsingCue;
  float _stepPulse;
  float _boardPulseT;

  void PulseStep(int step) {
    if (_stepCues == null || step < 1 || step > _stepCues.Length) return;
    _pulsingCue = _stepCues[step - 1];
    _stepPulse = 0.5f;
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
    try { Debug.Log("[NumberStairs] " + message, this); } catch (Exception) { }
  }

  // ---- test seams (no live scene needed) -------------------------------------------

  public void SetPhaseForTests(Phase p) { Current = p; _phaseT = 0f; }
  public void MarkLifecycleActiveForTests() {
    if (_life == null) return;
    try {
      if (_life.State == ActivityState.Ready || _life.State == ActivityState.Available)
        _life.Begin("test active");
    } catch (Exception) { }
  }
}
