// A_World/CountingGarden/CountingDemo.cs — S3 P2W TWO-NPC MINI LESSON.
// ONE Number-2 living visual instruction (NOT cutscene, NOT gameplay, NOT AI).
// User script: teacher explains number two at the board -> assigns the task
// ("take two balls, put them in the basket") -> the child student fetches
// exactly TWO of the five balls, carries them visibly, drops them in the
// basket -> the teacher asks, confirms ("two balls!") -> result board "2 tick"
// -> both celebrate -> tidy reset -> LOOP.
// Roles are acted, not simulated: the teacher speaks/turns/points and OBSERVES;
// the student looks, walks, picks, carries, places, reacts. Both are staged
// with the existing body kit (TessVisual / MiloVisual prefab, face kit, PickUp
// / Celebrate triggers, procedural arm raise) — no AI, no new framework.
// CAMERA (user rule): two authored shots — A (lesson: board + teacher +
// student + field + basket) while the teacher talks, B (action: student + two
// balls + basket + result) while the student works. The active shot is HELD
// while the child watches (re-issued), so this stays a readable instruction
// card; walking away releases it.
// Touches NOTHING gameplay: no bus, no quest, no save, no score, no
// Interactable, click-through hosts. C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

public enum DemoPhase {
  Ready,
  TeacherLookBoard,
  TeacherSayBoard,
  TeacherSayTwo,
  TeacherSayToday,
  TeacherAssign,
  StudentLook,
  WalkBalls,
  PickOne,
  PickTwo,
  ShowTwo,
  WalkBasket,
  PlaceOne,
  PlaceTwo,
  TeacherAsks,
  Confirm,
  Celebrate,
  HoldResult,
  ResetBalls,
  StudentReturn,
  TeacherReturn,
}

[DisallowMultipleComponent]
public class CountingDemo : MonoBehaviour {
  const float WalkSpeed = 0.9f;   // slow enough for a 4yo to follow
  const float WatchRadius = CountingGardenBuilder.DemoViewRadius;
  const float RearmMargin = 2.0f;
  const float BeatHoldSeconds = 3.0f;

  static readonly VoiceProfileId DemoVoice = new VoiceProfileId("npc_female_01");
  static readonly LanguageCode EnUs = new LanguageCode("en-US");

  // Stage refs (pushed by Build).
  GameObject _number;
  Transform _basket;
  readonly List<GameObject> _balls = new List<GameObject>();
  GameObject _result;
  Transform _camA, _lookA, _camB, _lookB;
  Vector3 _mouth;

  // Live refs (nullable; every use is null-guarded).
  Transform _playerT;
  SmartCamera _cam;
  IAudioDirector _audio;

  // Two staged NPCs (teacher + child student).
  sealed class NpcActor {
    public GameObject Root;
    public Transform Visual;
    public Animator Animator;
    public CharacterPresentation Face;
    public Transform CarryAnchor;
    public Transform WaveBone;
    public Quaternion WaveBase = Quaternion.identity;
    public bool Waving;
    public float WaveT;
  }

  NpcActor _teacher;
  NpcActor _student;
  bool _actorsBuilt;
  bool _built;

  // Sequence state (Step() drives it; tests drive Step with fake dt).
  DemoPhase _phase = DemoPhase.Ready;
  float _phaseT;
  public DemoPhase Phase { get { return _phase; } }
  public int LoopCount { get; private set; }
  public int DemoBeatsFired { get; private set; }
  public bool ActorsBuilt { get { return _actorsBuilt; } }
  public bool ResultShown { get { return _result != null && _result.activeSelf; } }
  public bool ShotIsAction { get; private set; }

  // One in-flight ball transfer (arc field<->hand<->basket; same object).
  GameObject _flyBall;
  Vector3 _flyFrom, _flyTo;
  float _flyT, _flyDur, _flyLift;
  bool _flying;
  bool _carry0, _carry1;
  GameObject _ball0, _ball1; // the two chosen balls (indices into _balls)

  bool _beatLatched;
  float _beatRefreshT;
  bool _didPulse;
  bool _saidNumber, _saidToday, _saidAssign, _cheered, _asked;
  bool _firedPick, _placed;
  float _walkT;

  // ---- build -------------------------------------------------------------------

  public void Build(CountingGardenBuilder b, Transform playerT, SmartCamera cam, IAudioDirector audio) {
    if (b != null) {
      _number = b.DemoNumber;
      _basket = b.DemoBasket;
      _balls.Clear();
      _balls.AddRange(b.DemoBalls);
      _result = b.DemoResult;
      _camA = b.DemoCam;
      _lookA = b.DemoLook;
      _camB = b.DemoActionCam;
      _lookB = b.DemoActionLook;
      _mouth = b.DemoMouth;
      _built = true;
    }
    _playerT = playerT;
    _cam = cam;
    _audio = audio;
    BuildActorsImmediate(b != null ? b.transform : transform);
    ResetActors();
    _phase = DemoPhase.Ready;
    _phaseT = 0f;
  }

  bool _actorAttempt;

  public void BuildActorsImmediate(Transform stageParent) {
    if (_actorAttempt) return;
    _actorAttempt = true;
    try {
      _teacher = BuildNpc(stageParent, "CGDemoTeacher", "NpcVisuals/TessVisual", 0.5f,
        CountingGardenBuilder.DemoNpcStart, new Color(0.25f, 0.45f, 0.85f),
        new Color(0.98f, 0.78f, 0.25f));
      _student = BuildNpc(stageParent, "CGDemoStudent", "NpcVisuals/MiloVisual", 0.42f,
        CountingGardenBuilder.DemoStudentStart, new Color(0.30f, 0.62f, 0.45f),
        new Color(0.55f, 0.35f, 0.20f));
      _actorsBuilt = _teacher != null && _teacher.Root != null
        && _student != null && _student.Root != null;
    } catch (Exception e) {
      try { Debug.LogError("[CountingDemo] actor build failed: " + e.Message, this); }
      catch (Exception) { }
    }
  }

  NpcActor BuildNpc(Transform parent, string name, string prefabName, float scale,
      Vector3 stand, Color vest, Color hat) {
    GameObject prefab = Resources.Load<GameObject>(prefabName);
    if (prefab == null) {
      Debug.LogError("[CountingDemo] Missing " + prefabName + "; actor parked.", this);
      return null;
    }
    NpcActor a = new NpcActor();
    a.Root = new GameObject(name);
    a.Root.transform.SetParent(parent, false);
    a.Root.transform.localPosition = stand;
    a.Root.transform.localRotation = Quaternion.identity;
    GameObject visual = Instantiate(prefab, a.Root.transform, false);
    visual.name = name + "Visual";
    visual.transform.localPosition = new Vector3(0f, 0.02f, 0f);
    visual.transform.localRotation = Quaternion.identity;
    visual.transform.localScale = Vector3.one * scale;
    a.Visual = visual.transform;
    a.Animator = visual.GetComponentInChildren<Animator>(true);
    SkinnedMeshRenderer skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
    Transform head = null, footL = null, footR = null;
    if (skin != null) {
      CharacterPresentation.TintSharedMaterials(skin, "Vest", vest);
      CharacterPresentation.TintSharedMaterials(skin, "Hat", hat);
      if (skin.bones != null) {
        foreach (Transform bone in skin.bones) {
          if (bone == null) continue;
          if (head == null && bone.name == "Head") head = bone;
          if (footL == null && bone.name == "Foot.L") footL = bone;
          if (footR == null && bone.name == "Foot.R") footR = bone;
          if (a.WaveBone == null && (bone.name == "UpperArm.R" || bone.name == "Shoulder.R"))
            a.WaveBone = bone;
        }
      }
    }
    a.Face = a.Root.AddComponent<CharacterPresentation>();
    try { a.Face.SetupFace(skin, head, a.Root.transform, a.Visual); } catch (Exception) { }
    try { a.Face.BuildFaceImmediate(); } catch (Exception) { }
    try {
      if (footL != null) a.Face.QueueShoe(footL, "ShoeL");
      if (footR != null) a.Face.QueueShoe(footR, "ShoeR");
      a.Face.BuildShoesImmediate();
    } catch (Exception) { }
    GameObject anchor = new GameObject(name + "CarryAnchor");
    anchor.transform.SetParent(a.Root.transform, false);
    anchor.transform.localPosition = new Vector3(0f, 0.95f, -0.35f);
    a.CarryAnchor = anchor.transform;
    // Click-through: the lesson must never eat walk clicks.
    try { CharacterPresentation.DestroyColliders(a.Root); } catch (Exception) { }
    return a;
  }

  void ResetActors() {
    _carry0 = false;
    _carry1 = false;
    _flying = false;
    _flyBall = null;
    try {
      if (_number != null) _number.transform.localScale = Vector3.one;
      if (_result != null) _result.SetActive(false);
      for (int i = 0; i < _balls.Count; i++) {
        if (_balls[i] != null) _balls[i].transform.localPosition = CountingGardenBuilder.DemoBallHomes[i];
      }
      if (_teacher != null && _teacher.Root != null) {
        _teacher.Root.transform.localPosition = CountingGardenBuilder.DemoNpcStart;
        FaceSnap(_teacher, BoardPoint());
        if (_teacher.Face != null) _teacher.Face.SetExpression(CharacterExpression.Neutral);
      }
      if (_student != null && _student.Root != null) {
        _student.Root.transform.localPosition = CountingGardenBuilder.DemoStudentStart;
        FaceSnap(_student, TeacherPoint());
        if (_student.Face != null) _student.Face.SetExpression(CharacterExpression.Neutral);
      }
    } catch (Exception) { }
  }

  // ---- sequence -----------------------------------------------------------------

  void Update() {
    try { Step(Time.deltaTime); } catch (Exception) { }
  }

  public void Step(float dt) {
    if (!_built || !_actorsBuilt || dt <= 0f) return;
    WatchPlayer();
    _phaseT += dt;
    switch (_phase) {
      case DemoPhase.Ready:
        if (_phaseT >= 0.8f) To(DemoPhase.TeacherLookBoard);
        break;

      // ---- teacher introduces: "look at the board / this is number two" ----
      case DemoPhase.TeacherLookBoard:
        FaceTowards(_teacher, BoardPoint(), dt, 4f);
        FaceTowards(_student, TeacherPoint(), dt, 3f);
        if (_phaseT >= 1.0f) {
          To(DemoPhase.TeacherSayBoard);
          Wave(_teacher);
          Speak("Look at the board!", SpeechStyle.Clear, AudioPriority.P2_Instruction);
        }
        break;
      case DemoPhase.TeacherSayBoard:
        FaceTowards(_teacher, BoardPoint(), dt, 4f);
        PulseNumber();
        if (!_saidNumber) {
          _saidNumber = true;
          Speak("This is number two.", SpeechStyle.Clear, AudioPriority.P2_Instruction);
        }
        if (_phaseT >= 2.4f) To(DemoPhase.TeacherSayTwo);
        break;
      case DemoPhase.TeacherSayTwo:
        FaceTowards(_teacher, BoardPoint(), dt, 4f);
        PulseNumber();
        if (!_didPulse && _phaseT >= 0.2f) {
          _didPulse = true;
          Speak("Two.", SpeechStyle.Clear, AudioPriority.P2_Instruction);
        }
        if (_phaseT >= 1.4f) To(DemoPhase.TeacherSayToday);
        break;
      case DemoPhase.TeacherSayToday:
        FaceTowards(_teacher, StudentPoint(), dt, 4f);
        if (!_saidToday) {
          _saidToday = true;
          Speak("Today, we take two balls.", SpeechStyle.Clear, AudioPriority.P2_Instruction);
        }
        if (_phaseT >= 2.6f) To(DemoPhase.TeacherAssign);
        break;

      // ---- teacher assigns: point at the balls, then at the basket ----
      case DemoPhase.TeacherAssign:
        if (!_saidAssign) {
          _saidAssign = true;
          Wave(_teacher);
          Speak("Take two balls, please!", SpeechStyle.Clear, AudioPriority.P2_Instruction);
        }
        FaceTowards(_teacher, BallFieldPoint(), dt, 4f);
        if (_phaseT >= 2.2f) To(DemoPhase.StudentLook);
        break;
      case DemoPhase.StudentLook:
        FaceTowards(_teacher, BasketPoint(), dt, 4f);
        if (_phaseT >= 0.4f && !_didPulse) {
          _didPulse = true;
          Speak("Put them in the basket!", SpeechStyle.Clear, AudioPriority.P2_Instruction);
        }
        FaceTowards(_student, TeacherPoint(), dt, 3f);
        if (_phaseT >= 2.2f) {
          SetShot(true); // action frame: student + balls + basket
          To(DemoPhase.WalkBalls);
        }
        break;

      // ---- the student fetches exactly two of the five balls ----
      case DemoPhase.WalkBalls:
        if (WalkTo(_student, CountingGardenBuilder.DemoBallStand, dt)) To(DemoPhase.PickOne);
        break;
      case DemoPhase.PickOne:
        FaceTowards(_student, BallPoint(), dt, 5f);
        if (!_firedPick) {
          _firedPick = true;
          Trigger(_student, "PickUp");
          _ball0 = PickBall(2);
          Fly(_ball0, BallHome(2), CarrySlot(_student, 0), 0.25f, 0.9f, 0.5f);
        }
        if (_phaseT >= 0.9f && !_didPulse) {
          _didPulse = true;
          Speak("One ball.", SpeechStyle.Clear, AudioPriority.P2_Instruction);
        }
        if (_phaseT >= 1.8f) {
          _carry0 = true;
          To(DemoPhase.PickTwo);
        }
        break;
      case DemoPhase.PickTwo:
        FaceTowards(_student, BallPoint2(), dt, 5f);
        if (!_firedPick) {
          _firedPick = true;
          Trigger(_student, "PickUp");
          Pulse(_student, CharacterExpression.Surprised, 1.0f); // "got it!" beat
          _ball1 = PickBall(3);
          Fly(_ball1, BallHome(3), CarrySlot(_student, 1), 0.25f, 0.9f, 0.5f);
        }
        if (_phaseT >= 1.4f) {
          _carry1 = true;
          To(DemoPhase.ShowTwo);
        }
        break;
      case DemoPhase.ShowTwo:
        // Beat: the student faces the child holding BOTH balls, teacher reacts.
        FaceTowards(_student, ViewerPoint(), dt, 4f);
        FaceTowards(_teacher, StudentPoint(), dt, 3f);
        if (!_didPulse && _phaseT >= 0.3f) {
          _didPulse = true;
          Speak("Two balls!", SpeechStyle.Excited, AudioPriority.P4_Feedback);
          Wave(_teacher);
          Pulse(_teacher, CharacterExpression.Happy, 2f);
          Pulse(_student, CharacterExpression.Happy, 2f);
          Hop(_student); // excited little hop with the two balls
        }
        if (_phaseT >= 2.2f) To(DemoPhase.WalkBasket);
        break;

      // ---- carry to the basket and drop both in ----
      case DemoPhase.WalkBasket:
        if (WalkTo(_student, CountingGardenBuilder.DemoBasketStand, dt)) To(DemoPhase.PlaceOne);
        break;
      case DemoPhase.PlaceOne:
        FaceTowards(_student, BasketPoint(), dt, 5f);
        if (!_placed) {
          _placed = true;
          _carry0 = false;
          Fly(_ball0, CarrySlot(_student, 0), BasketSlot(0), 0.1f, 0.8f, 0.4f);
        }
        if (_phaseT >= 1.1f) To(DemoPhase.PlaceTwo);
        break;
      case DemoPhase.PlaceTwo:
        FaceTowards(_student, BasketPoint(), dt, 5f);
        if (!_placed) {
          _placed = true;
          _carry1 = false;
          Fly(_ball1, CarrySlot(_student, 1), BasketSlot(1), 0.1f, 0.8f, 0.4f);
        }
        if (_phaseT >= 1.1f) To(DemoPhase.TeacherAsks);
        break;

      // ---- teacher asks + confirms: the result board shows 2 tick ----
      case DemoPhase.TeacherAsks:
        FaceTowards(_teacher, BasketPoint(), dt, 4f);
        FaceTowards(_student, BasketPoint(), dt, 4f);
        if (!_asked) {
          _asked = true;
          Speak("How many balls?", SpeechStyle.Clear, AudioPriority.P2_Instruction);
          Pulse(_teacher, CharacterExpression.Curious, 1.8f);
          Pulse(_student, CharacterExpression.Curious, 1.8f);
        }
        if (_phaseT >= 2.0f) To(DemoPhase.Confirm);
        break;
      case DemoPhase.Confirm:
        FaceTowards(_teacher, BasketPoint(), dt, 4f);
        if (_result != null && !_result.activeSelf) {
          try { _result.SetActive(true); } catch (Exception) { }
          _resultPopT = 0f; // lively pop-in of the "2 tick" board
        }
        if (!_cheered) {
          _cheered = true;
          Speak("Two balls!", SpeechStyle.Excited, AudioPriority.P4_Feedback);
        }
        if (_phaseT >= 1.6f) To(DemoPhase.Celebrate);
        break;
      case DemoPhase.Celebrate:
        FaceTowards(_teacher, ViewerPoint(), dt, 3f);
        FaceTowards(_student, ViewerPoint(), dt, 3f);
        if (!_didPulse) {
          _didPulse = true;
          Trigger(_teacher, "Celebrate");
          Trigger(_student, "Celebrate");
          Wave(_teacher);
          SpeelCelebrate();
          Pulse(_teacher, CharacterExpression.Happy, 3f);
          Pulse(_student, CharacterExpression.Happy, 3f);
          Hop(_teacher);  // both actors hop: lively praise, not a static pose
          Hop(_student);
        }
        if (_phaseT >= 2.8f) To(DemoPhase.HoldResult);
        break;
      case DemoPhase.HoldResult:
        if (_phaseT >= 1.6f) To(DemoPhase.ResetBalls);
        break;

      // ---- tidy reset: balls go home, actors return, board keeps its 2 ----
      case DemoPhase.ResetBalls:
        if (_result != null && _result.activeSelf) {
          try { _result.SetActive(false); } catch (Exception) { }
        }
        SetShot(false);
        if (!_firedPick) {
          _firedPick = true;
          Fly(_ball0, BasketSlot(0), BallHome(2), 0.2f, 0.7f, 0.45f);
        }
        if (_phaseT >= 0.9f && !_placed) {
          _placed = true;
          Fly(_ball1, BasketSlot(1), BallHome(3), 0.0f, 0.7f, 0.45f);
        }
        if (_phaseT >= 2.2f) {
          for (int i = 0; i < _balls.Count; i++) {
            if (_balls[i] != null) _balls[i].transform.localPosition = CountingGardenBuilder.DemoBallHomes[i];
          }
          if (_teacher != null && _teacher.Face != null) _teacher.Face.SetExpression(CharacterExpression.Neutral);
          if (_student != null && _student.Face != null) _student.Face.SetExpression(CharacterExpression.Neutral);
          To(DemoPhase.StudentReturn);
        }
        break;
      case DemoPhase.StudentReturn:
        if (WalkTo(_student, CountingGardenBuilder.DemoStudentStart, dt)) To(DemoPhase.TeacherReturn);
        break;
      case DemoPhase.TeacherReturn:
        FaceTowards(_teacher, BoardPoint(), dt, 3f);
        if (_phaseT >= 0.6f) {
          LoopCount++;
          To(DemoPhase.Ready);
        }
        break;
    }
    TickFlight(dt);
    TickCarry();
    TickWave(dt);
    TickLiveliness(dt);
  }

  // ---- liveliness (user round: "làm sinh động nhất có thể") ----------------
  // Result board pops in, landed balls bounce once, and the field balls idle
  // with a tiny independent bob. All transform-only, deterministic-ish, and
  // never touching gameplay state.

  float _resultPopT = 1f;
  GameObject _popBall;
  float _popT = 1f;
  readonly float[] _ballPhase = { 0.0f, 1.3f, 2.6f, 3.9f, 5.2f };

  void TickLiveliness(float dt) {
    if (_resultPopT < 1f && _result != null) {
      _resultPopT = Mathf.Min(1f, _resultPopT + dt / 0.28f);
      float s = Mathf.Lerp(0.65f, 1f, Mathf.SmoothStep(0f, 1f, _resultPopT));
      try { _result.transform.localScale = new Vector3(s, s, s); } catch (Exception) { }
    }
    if (_popBall != null) {
      _popT = Mathf.Min(1f, _popT + dt / 0.3f);
      float s = 1f + 0.22f * Mathf.Sin(Mathf.PI * _popT);
      try { _popBall.transform.localScale = Vector3.one * (0.34f * s); } catch (Exception) { }
      if (_popT >= 1f) {
        try { _popBall.transform.localScale = Vector3.one * 0.34f; } catch (Exception) { }
        _popBall = null;
      }
    }
    // Field balls breathe while they wait on the field.
    for (int i = 0; i < _balls.Count; i++) {
      GameObject b = _balls[i];
      if (b == null || b == _ball0 || b == _ball1) continue;
      Vector3 home = CountingGardenBuilder.DemoBallHomes[i];
      try {
        Vector3 p = b.transform.localPosition;
        if ((p - home).sqrMagnitude > 0.01f) continue; // flying/placed: leave it
        b.transform.localPosition = new Vector3(home.x,
          home.y + Mathf.Sin(Time.time * 2.1f + _ballPhase[i % _ballPhase.Length]) * 0.015f, home.z);
      } catch (Exception) { }
    }
  }

  void SpeelCelebrate() {
    Speak("Yes! Two balls! Well done!", SpeechStyle.Excited, AudioPriority.P4_Feedback);
  }

  bool _didPulseReset;

  void To(DemoPhase next) {
    _phase = next;
    _phaseT = 0f;
    _firedPick = false;
    _placed = false;
    _didPulse = false;
    if (next == DemoPhase.Ready) {
      _saidNumber = false;
      _saidToday = false;
      _saidAssign = false;
      _asked = false;
      _cheered = false;
      _ball0 = null;
      _ball1 = null;
      _didPulseReset = false;
    }
  }

  // ---- shot control --------------------------------------------------------------

  void SetShot(bool action) {
    if (ShotIsAction == action) return;
    ShotIsAction = action;
    IssueShot(); // crisp reframe on the phase boundary (user camera rule)
  }

  void IssueShot() {
    if (_cam == null) return;
    Transform c = ShotIsAction ? _camB : _camA;
    Transform l = ShotIsAction ? _lookB : _lookA;
    if (c == null || l == null) return;
    try { _cam.FrameAnchor(c, l, BeatHoldSeconds); } catch (Exception) { }
  }

  // ---- carry / flight / wave ------------------------------------------------------

  void TickCarry() {
    try {
      if (_carry0 && _ball0 != null && !_flying) _ball0.transform.localPosition = CarrySlot(_student, 0);
      if (_carry1 && _ball1 != null && !_flying) _ball1.transform.localPosition = CarrySlot(_student, 1);
    } catch (Exception) { }
  }

  void Fly(GameObject ball, Vector3 from, Vector3 to, float delay, float dur, float lift) {
    _flyBall = ball;
    _flyFrom = from;
    _flyTo = to;
    _flyT = -delay;
    _flyDur = Mathf.Max(0.2f, dur);
    _flyLift = lift;
    _flying = true;
  }

  void TickFlight(float dt) {
    if (!_flying || _flyBall == null) return;
    try {
      _flyT += dt;
      if (_flyT < 0f) return;
      float t = Mathf.Clamp01(_flyT / _flyDur);
      Vector3 mid = (_flyFrom + _flyTo) * 0.5f + new Vector3(0f, _flyLift, 0f);
      _flyBall.transform.localPosition = Vector3.Lerp(
        Vector3.Lerp(_flyFrom, mid, t), Vector3.Lerp(mid, _flyTo, t), t);
      if (t >= 1f) {
        _popBall = _flyBall; // landing bounce (basket, hand, or home)
        _popT = 0f;
        _flying = false;
        _flyBall = null;
      }
    } catch (Exception) { _flying = false; }
  }

  GameObject PickBall(int index) {
    return index >= 0 && index < _balls.Count ? _balls[index] : null;
  }

  Vector3 BallHome(int index) {
    return index >= 0 && index < CountingGardenBuilder.DemoBallHomes.Length
      ? CountingGardenBuilder.DemoBallHomes[index] : Vector3.zero;
  }

  Vector3 BallPoint() { return CountingGardenBuilder.DemoBallStand + new Vector3(0f, 0.3f, 0f); }
  Vector3 BallPoint2() { return CountingGardenBuilder.DemoBall2Stand + new Vector3(0f, 0.3f, 0f); }
  Vector3 BallFieldPoint() { return CountingGardenBuilder.DemoBallFieldPos + new Vector3(0f, 0.4f, 0f); }
  Vector3 BoardPoint() { return new Vector3(0f, 1.4f, 12.25f); }
  Vector3 TeacherPoint() {
    return _teacher != null && _teacher.Root != null
      ? _teacher.Root.transform.localPosition + new Vector3(0f, 1.1f, 0f)
      : new Vector3(0f, 1.1f, 11.4f);
  }
  Vector3 StudentPoint() {
    return _student != null && _student.Root != null
      ? _student.Root.transform.localPosition + new Vector3(0f, 0.9f, 0f)
      : new Vector3(0f, 0.9f, 10.2f);
  }
  Vector3 BasketPoint() {
    return _basket != null ? _basket.localPosition + new Vector3(0f, 0.4f, 0f)
      : CountingGardenBuilder.DemoBasketStand + new Vector3(0f, 0.4f, 0f);
  }
  Vector3 ViewerPoint() {
    return _camA != null ? _camA.localPosition : CountingGardenBuilder.DemoMouthLocal;
  }

  Vector3 CarrySlot(NpcActor actor, int i) {
    if (actor != null && actor.CarryAnchor != null) {
      Vector3 c = actor.CarryAnchor.localPosition;
      Vector3 h = actor.Root.transform.localPosition;
      return new Vector3(h.x + c.x + (i == 0 ? -0.13f : 0.13f), c.y, h.z + c.z);
    }
    return Vector3.zero;
  }

  // Both landed balls must stay visible above the rim from the card camera
  // (round-2 capture: the second ball hid behind the first).
  Vector3 BasketSlot(int i) {
    Vector3 b = _basket != null ? _basket.localPosition : CountingGardenBuilder.DemoBasketStand;
    return new Vector3(b.x + (i == 0 ? -0.26f : 0.30f), 0.76f, b.z + (i == 0 ? 0.16f : -0.10f));
  }

  // ---- motion ---------------------------------------------------------------------

  bool WalkTo(NpcActor actor, Vector3 target, float dt) {
    if (actor == null || actor.Root == null) return true;
    Vector3 p = actor.Root.transform.localPosition;
    Vector3 flat = new Vector3(target.x - p.x, 0f, target.z - p.z);
    float dist = flat.magnitude;
    if (dist <= 0.15f) {
      try {
        if (actor.Visual != null) {
          Vector3 v = actor.Visual.localPosition;
          v.y = 0.02f;
          actor.Visual.localPosition = v;
        }
      } catch (Exception) { }
      return true;
    }
    FaceTowards(actor, target, dt, 6f);
    float step = Mathf.Min(WalkSpeed * dt, dist);
    Vector3 dir = flat / (dist > 0.0001f ? dist : 1f);
    try { actor.Root.transform.localPosition = new Vector3(p.x + dir.x * step, 0f, p.z + dir.z * step); }
    catch (Exception) { }
    _walkT += dt;
    try {
      if (actor.Visual != null) {
        Vector3 v = actor.Visual.localPosition;
        v.y = 0.02f + Mathf.Abs(Mathf.Sin(_walkT * 9f)) * 0.05f;
        actor.Visual.localPosition = v;
      }
    } catch (Exception) { }
    return false;
  }

  void FaceTowards(NpcActor actor, Vector3 targetLocal, float dt, float rate) {
    if (actor == null || actor.Root == null) return;
    try {
      Vector3 p = actor.Root.transform.localPosition;
      Vector3 d = new Vector3(targetLocal.x - p.x, 0f, targetLocal.z - p.z);
      if (d.sqrMagnitude < 0.0001f) return;
      Quaternion want = Quaternion.LookRotation(d);
      actor.Root.transform.localRotation = Quaternion.Slerp(actor.Root.transform.localRotation, want,
        1f - Mathf.Exp(-rate * dt));
    } catch (Exception) { }
  }

  void FaceSnap(NpcActor actor, Vector3 dir) {
    if (actor == null || actor.Root == null) return;
    try {
      Vector3 d = new Vector3(dir.x, 0f, dir.z);
      if (d.sqrMagnitude < 0.0001f) return;
      actor.Root.transform.localRotation = Quaternion.LookRotation(d);
    } catch (Exception) { }
  }

  // ---- speech / gesture -----------------------------------------------------------

  void Speak(string text, SpeechStyle style, AudioPriority priority) {
    if (_audio == null) return;
    try {
      var req = new DialogueRequest(text, DemoVoice, EnUs, 1f, 1f, style,
        AudioFormat.Mp3_44100, priority);
      _audio.SpeakAsync(req); // fire-and-forget: the Director owns playback.
    } catch (Exception) { }
  }

  void Trigger(NpcActor actor, string name) {
    try { if (actor != null && actor.Animator != null) actor.Animator.SetTrigger(name); } catch (Exception) { }
  }

  void Pulse(NpcActor actor, CharacterExpression e, float seconds) {
    try { if (actor != null && actor.Face != null) actor.Face.PulseExpression(e, seconds); }
    catch (Exception) { }
  }

  void Hop(NpcActor actor) {
    try { if (actor != null && actor.Face != null) actor.Face.PlayHop(); } catch (Exception) { }
  }

  void Wave(NpcActor actor) {
    if (actor == null) return;
    actor.WaveT = 1.4f;
  }

  void TickWave(float dt) {
    TickWaveOne(_teacher, dt);
    TickWaveOne(_student, dt);
  }

  void TickWaveOne(NpcActor actor, float dt) {
    if (actor == null || actor.WaveBone == null) return;
    try {
      if (actor.WaveT > 0f) {
        if (!actor.Waving) {
          actor.Waving = true;
          actor.WaveBase = actor.WaveBone.localRotation;
        }
        actor.WaveT -= dt;
        float wave = Mathf.Sin(Time.time * 14f) * 18f;
        actor.WaveBone.localRotation = actor.WaveBase * Quaternion.Euler(0f, 0f, -75f + wave);
        if (actor.WaveT <= 0f) {
          actor.Waving = false;
          actor.WaveBone.localRotation = actor.WaveBase;
        }
      }
    } catch (Exception) { }
  }

  void PulseNumber() {
    if (_number == null) return;
    try {
      float t = Mathf.Clamp01(_phaseT / 2.4f);
      float s = 1f + 0.12f * Mathf.Sin(Mathf.PI * t);
      _number.transform.localScale = new Vector3(s, s, s);
    } catch (Exception) { }
  }

  // ---- camera-first watching -------------------------------------------------------

  void WatchPlayer() {
    if (_playerT == null || _cam == null || _camA == null || _lookA == null) return;
    try {
      Vector3 p = _playerT.position;
      if (p.x < 60f) return; // player not in the garden island
      Vector3 local = p - new Vector3(120f, 0f, 0f);
      float dx = local.x - _mouth.x, dz = local.z - _mouth.z;
      float d2 = dx * dx + dz * dz;
      if (d2 <= WatchRadius * WatchRadius) {
        if (!_beatLatched) {
          _beatLatched = true;
          DemoBeatsFired++;
          _beatRefreshT = 0f;
          IssueShot();
        } else {
          _beatRefreshT += Time.deltaTime;
          if (_beatRefreshT >= BeatHoldSeconds - 1.2f) {
            _beatRefreshT = 0f;
            IssueShot();
          }
        }
      } else if (_beatLatched && d2 > (WatchRadius + RearmMargin) * (WatchRadius + RearmMargin)) {
        _beatLatched = false;
      }
    } catch (Exception) { }
  }
}
