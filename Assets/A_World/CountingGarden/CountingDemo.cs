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
using System.Threading.Tasks;
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
  // S3-P2Z4: the lesson finished; the actors watch the child play (the arena
  // intro hands control over instead of looping).
  Observing,
}

[DisallowMultipleComponent]
public class CountingDemo : MonoBehaviour {
  const float WalkSpeed = 0.9f;   // slow enough for a 4yo to follow
  const float WatchRadius = CountingGardenBuilder.DemoViewRadius;
  const float RearmMargin = 2.0f;
  const float BeatHoldSeconds = 3.0f;

  static readonly VoiceProfileId DemoVoice = new VoiceProfileId("npc_female_01");

  // Stage refs (pushed by Build).
  GameObject _number;
  Transform _basket;
  Vector3 _basketBase = Vector3.one;
  readonly List<GameObject> _balls = new List<GameObject>();
  GameObject _result;
  Transform _camA, _lookA, _camB, _lookB, _camC, _lookC;
  Transform _fxRoot;
  Vector3 _mouth;

  // Live refs (nullable; every use is null-guarded).
  Transform _playerT;
  SmartCamera _cam;
  IAudioDirector _audio;
  // Island the stage lives on (garden +120x, play arena +180x): the watch
  // radius is measured from the player's position ON THIS ISLAND, so the same
  // controller works in both scenes without hardcoded map coordinates.
  Vector3 _islandOffset = CountingGardenBuilder.WorldOffset;

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
    // Juice (S3-P2Y): squash/stretch around the authored visual scale.
    public Vector3 BaseScale = Vector3.one;
    public float SquashT;
    // S3-P2Z4 gesture language: point-at-target + nod (procedural bones).
    public Transform HeadBone;
    public Vector3 PointTarget;
    public float PointT;
    public Quaternion HeadBase = Quaternion.identity;
    public float NodT;
  }

  NpcActor _teacher;
  NpcActor _student;
  bool _actorsBuilt;
  bool _built;
  // S3-P2Y: the GARDEN instance runs as an ambient miniature — its card
  // camera must never hijack the child's view (the zone FOCUS does that);
  // the arena instance keeps the automatic beats. Default true so the
  // existing tests/behaviour are unchanged.
  public bool CameraBeatsEnabled = true;
  // Fired when a full lesson loop completes (the zone picker uses this to show
  // the "Vào chơi / Quay lại" panel only AFTER the child watched the demo).
  public Action OnLoopCompleted;
  // S3-P2Z4 reference gameplay: the arena runs the lesson ONCE as the INTRO
  // (S3-P2Z9) NoIntroMode: the arena is a PLAY space — the lesson never replays
  // there; the teacher only announces the assignment when the child reaches the
  // play field (CountingGame owns that beat).
  public bool NoIntroMode;
  // (LoopForever=false) and then hands control to the child (OnIntroCompleted);
  // the garden miniature keeps looping forever.
  public bool LoopForever = true;
  public Action OnIntroCompleted;
  // Who the actors watch while observing the child (set by the arena game).
  public Transform ObserveTarget;
  // S3-P2Z4: the arena game disables the gate once control has passed (or when
  // a completed activity is adopted) — no re-teach, no camera hijack while the
  // child plays. The garden miniature keeps the gate on (one pass per visit).
  public bool AudienceGateEnabled = true;
  // Acting layout (data-driven; defaults to the garden stage when the caller
  // passes no refs — see CountingGardenBuilder.DemoRefs).
  CountingGardenBuilder.DemoRefs _refs = new CountingGardenBuilder.DemoRefs();
  Transform _stageParent;
  float _introShotT;

  // Sequence state (Step() drives it; tests drive Step with fake dt).
  DemoPhase _phase = DemoPhase.Ready;
  float _phaseT;
  public DemoPhase Phase { get { return _phase; } }
  public int LoopCount { get; private set; }
  public int DemoBeatsFired { get; private set; }
  public bool ActorsBuilt { get { return _actorsBuilt; } }
  public bool ResultShown { get { return _result != null && _result.activeSelf; } }
  public bool ShotIsAction { get { return _shot == 1; } }
  public bool ShotIsResult { get { return _shot == 2; } }

  // S3-P2Y camera shots: 0 = lesson (A), 1 = action (B), 2 = result (C).
  int _shot;
  float _basketPopT = 1f;
  // One in-flight ball transfer (arc field<->hand<->basket; same object).
  GameObject _flyBall;
  Vector3 _flyFrom, _flyTo;
  float _flyT, _flyDur, _flyLift;
  bool _flying;
  bool _carry0, _carry1;
  GameObject _ball0, _ball1; // the two chosen balls (indices into _balls)

  bool _beatLatched;
  float _beatRefreshT;
  // Journey fix (user report round): the lesson card used to re-issue forever
  // while the child stood inside the watch radius, which locked the camera and
  // hid the way out behind it. Hold the card for a few re-issues, then release
  // the camera (the lesson keeps playing; walking out and back re-arms).
  int _beatReissues;
  const int MaxBeatReissues = 3;
  bool _beatReleased;
  // Follow framing to restore after the card releases (the demo's islands all
  // use the same child-height offset).
  public Vector3 FollowOffset = new Vector3(0f, 3.8f, -5f);
  public int ShotIssues { get; private set; }
  public int BeatReissues { get { return _beatReissues; } }
  bool _didPulse;
  bool _saidNumber, _saidToday, _saidAssign, _cheered, _asked;
  bool _firedPick, _placed;
  float _walkT;

  // ---- build -------------------------------------------------------------------

  // Garden theatre stage (zone 2 of the crescent). S3-P2Y: the stage is the
  // MINIATURE diorama, so the actors/FX are parented to the scaled mini root.
  public void Build(CountingGardenBuilder b, Transform playerT, SmartCamera cam, IAudioDirector audio) {
    CountingGardenBuilder.DemoRefs refs = null;
    Transform stageParent = transform;
    if (b != null) {
      refs = new CountingGardenBuilder.DemoRefs();
      refs.Number = b.DemoNumber;
      refs.Basket = b.DemoBasket;
      if (b.DemoBalls != null) refs.Balls.AddRange(b.DemoBalls);
      refs.Result = b.DemoResult;
      refs.CamA = b.DemoCam;
      refs.LookA = b.DemoLook;
      refs.CamB = b.DemoActionCam;
      refs.LookB = b.DemoActionLook;
      refs.CamC = b.DemoResultCam;
      refs.LookC = b.DemoResultLook;
      refs.StageParent = b.DemoMiniRoot != null ? b.DemoMiniRoot : b.transform;
      refs.Mouth = b.DemoMouth;
      stageParent = b.transform;
    }
    BuildFrom(refs, CountingGardenBuilder.WorldOffset, playerT, cam, audio, stageParent);
    // Garden: the lesson starts AT THE STAGE DOOR (user order §56: "lùi phần
    // tự động về sát cửa") — not from the middle of the yard. 1.4m = the
    // child's toes on the threshold (the portal-scale radius).
    _watchRadius = 1.4f;
  }

  // S3-P2Z4: the ARENA runs the lesson as the reference gameplay's INTRO
  // (LoopForever=false), staged by CountingPlayBuilder.BuildActivity.
  public void Build(CountingPlayBuilder b, Transform playerT, SmartCamera cam, IAudioDirector audio) {
    CountingGardenBuilder.DemoRefs refs = b != null ? b.Activity : null;
    BuildFrom(refs, CountingPlayBuilder.WorldOffset, playerT, cam, audio,
      b != null ? b.transform : transform);
    // Arena: the child SPAWNS at the door and the intro must greet them right
    // there (the stage is compact), so this site keeps the wide radius.
    _watchRadius = CountingGardenBuilder.DemoViewRadius;
  }

  void BuildFrom(CountingGardenBuilder.DemoRefs refs, Vector3 islandOffset,
      Transform playerT, SmartCamera cam, IAudioDirector audio, Transform stageParent) {
    if (refs != null) _refs = refs;
    if (refs != null) {
      _number = refs.Number;
      _basket = refs.Basket;
      _balls.Clear();
      if (refs.Balls != null) _balls.AddRange(refs.Balls);
      _result = refs.Result;
      _camA = refs.CamA;
      _lookA = refs.LookA;
      _camB = refs.CamB;
      _lookB = refs.LookB;
      _camC = refs.CamC;
      _lookC = refs.LookC;
      _mouth = refs.Mouth;
      _built = true;
      if (refs.StageParent != null) stageParent = refs.StageParent;
    }
    _stageParent = stageParent;
    _islandOffset = islandOffset;
    _playerT = playerT;
    _cam = cam;
    _audio = audio;
    if (_basket != null) _basketBase = _basket.localScale;
    BuildActorsImmediate(stageParent);
    if (stageParent != null) {
      GameObject fx = new GameObject("CGDemoFx");
      fx.transform.SetParent(stageParent, false);
      _fxRoot = fx.transform;
    }
    ResetActors();
    _phase = DemoPhase.Ready;
    _phaseT = 0f;
    if (NoIntroMode) {
      // Straight to observers: no lesson replay, no audience camera hijack.
      // The game (CountingGame) announces the assignment on approach instead.
      SkipToObserving();
      AudienceGateEnabled = false;
    }
  }

  bool _actorAttempt;

  public void BuildActorsImmediate(Transform stageParent) {
    if (_actorAttempt) return;
    _actorAttempt = true;
    try {
      _teacher = BuildNpc(stageParent, "CGDemoTeacher", "NpcVisuals/TessVisual", 0.5f,
        _refs.NpcStart, new Color(0.25f, 0.45f, 0.85f),
        new Color(0.98f, 0.78f, 0.25f));
      _student = BuildNpc(stageParent, "CGDemoStudent", "NpcVisuals/MiloVisual", 0.42f,
        _refs.StudentStart, new Color(0.30f, 0.62f, 0.45f),
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
    a.BaseScale = visual.transform.localScale;
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
          if (a.HeadBone == null && bone.name == "Head") a.HeadBone = bone;
        }
      }
    }
    if (a.HeadBone != null) a.HeadBase = a.HeadBone.localRotation;
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
        if (_balls[i] != null) _balls[i].transform.localPosition = _refs.BallHomes[i];
      }
      if (_teacher != null && _teacher.Root != null) {
        _teacher.Root.transform.localPosition = _refs.NpcStart;
        FaceSnap(_teacher, BoardPoint());
        if (_teacher.Face != null) _teacher.Face.SetExpression(CharacterExpression.Neutral);
      }
      if (_student != null && _student.Root != null) {
        _student.Root.transform.localPosition = _refs.StudentStart;
        FaceSnap(_student, TeacherPoint());
        if (_student.Face != null) _student.Face.SetExpression(CharacterExpression.Neutral);
      }
    } catch (Exception) { }
  }

  // ---- sequence -----------------------------------------------------------------

  void Update() {
    try { Step(Time.deltaTime); } catch (Exception) { }
  }

  // S3-P2Y zone picker: the child focusing the plot starts a fresh "try run"
  // of the lesson (the panel only appears once this run completes).
  public void RestartLesson() {
    try {
      ResetActors();
      _ball0 = null;
      _ball1 = null;
      _saidNumber = false;
      _saidToday = false;
      _saidAssign = false;
      _asked = false;
      _cheered = false;
      _didPulse = false;
      SetShot(0);
      _phase = DemoPhase.Ready;
      _phaseT = 0f;
    } catch (Exception) { }
  }

  // S3-P2Z8 (user: "bấm vào vườn đếm, NPC không chạy demo"): focusing the plot
  // IS the child choosing to watch, so the lesson STARTS on the focus — it no
  // longer waits for the 1.4m door radius (clicking the spot from a distance
  // used to focus the camera and then do nothing at all).
  // A focused run is owned by the ZONE, not by the proximity gate: it keeps
  // playing wherever the child stands in the yard and stops when the focus is
  // released (panel Back / another plot / walking clear).
  bool _focusRun;
  public bool LessonEngaged { get { return _engaged; } }

  public void StartFocusedLesson() {
    if (!_built || !_actorsBuilt) return;
    if (_engaged) return;             // already running: never restart on re-click
    _focusRun = true;
    _engaged = true;
    _armed = false;
    _passDone = false;
    RestartLesson();
    try { _audio.SetAudioFocus(AudioFocusMode.Learning); } catch (Exception) { }
    try { Debug.Log("[CountingDemo] focused lesson start (zone door clicked)", this); }
    catch (Exception) { }
  }

  public void StopFocusedLesson() {
    if (!_focusRun) return;
    _focusRun = false;
    AbortLesson(); // cuts the voice + resets the stage to the playground state
    try { Debug.Log("[CountingDemo] focused lesson stopped (zone focus released)", this); }
    catch (Exception) { }
  }

  public void Step(float dt) {
    if (!_built || !_actorsBuilt || dt <= 0f) return;
    WatchPlayer();
    // S3-P2L2 audience gate (user order): the garden is a PLAYGROUND first —
    // the lesson only exists while the child stands at the viewing spot.
    // Nobody watching = no acting, no speech. The arena child arrives inside
    // the radius, so the intro runs immediately; the game disables the gate
    // once control has passed (no re-teach while they play).
    TickLiveliness(dt);
    TickSpeech(dt);
    if (!_engaged) return;
    // S3-P2Z4 intro camera: the lesson must STAY framed (the child watches the
    // board/balls/basket, not the player's back). Re-issue the current shot
    // while the intro runs; the garden miniature keeps its zone-focus camera.
    if (!LoopForever && CameraBeatsEnabled && _phase != DemoPhase.Observing) {
      _introShotT += dt;
      if (_introShotT >= 2.4f) {
        _introShotT = 0f;
        IssueShot();
      }
    }
    _phaseT += dt;
    switch (_phase) {
      case DemoPhase.Ready:
        // After a completed pass the actors hold the final idle state; the
        // lesson restarts only after the child walks away and comes back.
        if (_passDone && LoopForever) break;
        if (_phaseT >= 0.8f) {
          To(DemoPhase.TeacherLookBoard);
          if (!LoopForever && CameraBeatsEnabled) IssueShot(); // frame the lesson
        }
        break;

      // ---- teacher introduces: "look at the board / this is number two" ----
      case DemoPhase.TeacherLookBoard:
        FaceTowards(_teacher, BoardPoint(), dt, 4f);
        FaceTowards(_student, TeacherPoint(), dt, 3f);
        if (_phaseT >= 1.0f) {
          To(DemoPhase.TeacherSayBoard);
          Wave(_teacher);
          Speak(DialogueLang.T("Look at the board!", "Nhìn lên bảng nhé!"),
            SpeechStyle.Clear, AudioPriority.P2_Instruction);
        }
        break;
      case DemoPhase.TeacherSayBoard:
        FaceTowards(_teacher, BoardPoint(), dt, 4f);
        PulseNumber();
        if (!_saidNumber) {
          _saidNumber = true;
          Speak(DialogueLang.T("This is number two.", "Đây là số hai."),
            SpeechStyle.Clear, AudioPriority.P2_Instruction);
          PointActor(_teacher, _refs.BoardPoint, 2.4f); // head+torso+arm at the 2
        }
        if (_phaseT >= 2.4f) To(DemoPhase.TeacherSayTwo);
        break;
      case DemoPhase.TeacherSayTwo:
        FaceTowards(_teacher, BoardPoint(), dt, 4f);
        PulseNumber();
        if (!_didPulse && _phaseT >= 0.2f) {
          _didPulse = true;
          Speak(DialogueLang.T("Two.", "Hai."), SpeechStyle.Clear, AudioPriority.P2_Instruction);
        }
        if (_phaseT >= 1.4f) To(DemoPhase.TeacherSayToday);
        break;
      case DemoPhase.TeacherSayToday:
        FaceTowards(_teacher, StudentPoint(), dt, 4f);
        if (!_saidToday) {
          _saidToday = true;
          Speak(DialogueLang.T("Today, we take two balls.", "Hôm nay lấy hai bóng."),
            SpeechStyle.Clear, AudioPriority.P2_Instruction);
        }
        if (_phaseT >= 2.6f) To(DemoPhase.TeacherAssign);
        break;

      // ---- teacher assigns: point at the balls, then at the basket ----
      case DemoPhase.TeacherAssign:
        if (!_saidAssign) {
          _saidAssign = true;
          Wave(_teacher);
          Speak(DialogueLang.T("Take two balls, please!", "Con lấy hai bóng nhé."),
            SpeechStyle.Clear, AudioPriority.P2_Instruction);
          PointActor(_teacher, _refs.BallFieldPoint, 2.4f); // point at the balls
        }
        FaceTowards(_teacher, BallFieldPoint(), dt, 4f);
        if (_phaseT >= 2.2f) To(DemoPhase.StudentLook);
        break;
      case DemoPhase.StudentLook:
        FaceTowards(_teacher, BasketPoint(), dt, 4f);
        if (_phaseT >= 0.4f && !_didPulse) {
          _didPulse = true;
          Speak(DialogueLang.T("Put them in the basket!", "Bỏ vào giỏ nhé."),
            SpeechStyle.Clear, AudioPriority.P2_Instruction);
          PointActor(_teacher, _refs.BasketStand + new Vector3(0f, 0.4f, 0f), 2.0f);
          NodStudent(); // "understood" beat (Phase D)
        }
        FaceTowards(_student, TeacherPoint(), dt, 3f);
        if (_phaseT >= 2.2f) {
          SetShot(1); // action frame: student + balls + basket
          To(DemoPhase.WalkBalls);
        }
        break;

      // ---- the student fetches exactly two of the five balls ----
      case DemoPhase.WalkBalls:
        if (WalkTo(_student, _refs.BallStand, dt)) To(DemoPhase.PickOne);
        break;
      case DemoPhase.PickOne:
        FaceTowards(_student, BallPoint(), dt, 5f);
        if (!_firedPick) {
          _firedPick = true;
          Trigger(_student, "PickUp");
          _ball0 = PickBall(2);
          Fly(_ball0, BallHome(2), CarrySlot(_student, 0), 0.25f, 0.9f, 0.5f);
          Sparkle(BallPoint(), 6, 41, 0.28f);
        }
        if (_phaseT >= 0.9f && !_didPulse) {
          _didPulse = true;
          Speak(DialogueLang.T("One ball.", "Một quả bóng."),
            SpeechStyle.Clear, AudioPriority.P2_Instruction);
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
          Sparkle(BallPoint2(), 6, 42, 0.28f);
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
          Speak(DialogueLang.T("Two balls!", "Hai quả bóng!"),
            SpeechStyle.Excited, AudioPriority.P4_Feedback);
          Wave(_teacher);
          Pulse(_teacher, CharacterExpression.Happy, 2f);
          Pulse(_student, CharacterExpression.Happy, 2f);
          Hop(_student); // excited little hop with the two balls
          Sparkle(StudentPoint() + new Vector3(0f, 0.3f, 0f), 10, 43, 0.42f);
        }
        if (_phaseT >= 2.2f) To(DemoPhase.WalkBasket);
        break;

      // ---- carry to the basket and drop both in ----
      case DemoPhase.WalkBasket:
        if (WalkTo(_student, _refs.BasketStand, dt)) To(DemoPhase.PlaceOne);
        break;
      case DemoPhase.PlaceOne:
        FaceTowards(_student, BasketPoint(), dt, 5f);
        if (!_placed) {
          _placed = true;
          _carry0 = false;
          Fly(_ball0, CarrySlot(_student, 0), BasketSlot(0), 0.1f, 0.8f, 0.4f);
          BasketPop();
        }
        if (_phaseT >= 1.1f) To(DemoPhase.PlaceTwo);
        break;
      case DemoPhase.PlaceTwo:
        FaceTowards(_student, BasketPoint(), dt, 5f);
        if (!_placed) {
          _placed = true;
          _carry1 = false;
          Fly(_ball1, CarrySlot(_student, 1), BasketSlot(1), 0.1f, 0.8f, 0.4f);
          BasketPop();
        }
        if (_phaseT >= 1.1f) To(DemoPhase.TeacherAsks);
        break;

      // ---- teacher asks + confirms: the result board shows 2 tick ----
      case DemoPhase.TeacherAsks:
        FaceTowards(_teacher, BasketPoint(), dt, 4f);
        FaceTowards(_student, BasketPoint(), dt, 4f);
        if (!_asked) {
          _asked = true;
          Speak(DialogueLang.T("How many balls?", "Có mấy quả bóng?"),
            SpeechStyle.Clear, AudioPriority.P2_Instruction);
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
          SetShot(2);       // payoff frame: basket + "2 tick"
          Confetti(new Vector3(2.6f, 1.5f, 9.4f), 14, 44, 1.3f, 1.9f);
          Sparkle(ResultPoint(), 8, 45, 0.3f);
          Sparkle(BoardPoint(), 6, 46, 0.35f);
        }
        if (!_cheered) {
          _cheered = true;
          Speak(DialogueLang.T("Two balls!", "Hai quả bóng!"),
            SpeechStyle.Excited, AudioPriority.P4_Feedback);
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
          Confetti(new Vector3(0f, 2.4f, 11f), 22, 47, 3.2f, 2.6f);
          Sparkle(new Vector3(0f, 1.2f, 10.4f), 12, 48, 0.9f);
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
        SetShot(0);
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
            if (_balls[i] != null) _balls[i].transform.localPosition = _refs.BallHomes[i];
          }
          if (_teacher != null && _teacher.Face != null) _teacher.Face.SetExpression(CharacterExpression.Neutral);
          if (_student != null && _student.Face != null) _student.Face.SetExpression(CharacterExpression.Neutral);
          To(DemoPhase.StudentReturn);
        }
        break;
      case DemoPhase.StudentReturn:
        if (WalkTo(_student, _refs.StudentStart, dt)) To(DemoPhase.TeacherReturn);
        break;
      case DemoPhase.TeacherReturn:
        FaceTowards(_teacher, BoardPoint(), dt, 3f);
        if (_phaseT >= 0.6f) {
          LoopCount++;
          _passDone = true; // hold the idle state; re-arm needs the child to leave
          // S3-P2Y: a full try-run finished — the zone picker shows the panel.
          if (OnLoopCompleted != null) {
            try { OnLoopCompleted(); } catch (Exception) { }
          }
          if (!LoopForever) {
            // S3-P2Z4: the reference gameplay runs the lesson ONCE as the
            // intro, then the actors observe the child (control handover).
            To(DemoPhase.Observing);
            if (OnIntroCompleted != null) {
              try { OnIntroCompleted(); } catch (Exception) { }
            }
          } else {
            To(DemoPhase.Ready);
          }
        }
        break;

      case DemoPhase.Observing:
        // Watch the child (or the basket) while they play; a gesture in flight
        // (PointAt) owns the body until it finishes.
        if (_teacher != null && _teacher.PointT <= 0f)
          FaceTowards(_teacher, ObserveLocal(), dt, 1.6f);
        if (_student != null && _student.PointT <= 0f)
          FaceTowards(_student, ObserveLocal(), dt, 1.6f);
        break;
    }
    TickFlight(dt);
    TickCarry();
    TickWave(dt);
    TickPoint(_teacher, dt);
    TickPoint(_student, dt);
    TickNod(_teacher, dt);
    TickNod(_student, dt);
  }

  // ---- audience gating (user order: one pass, then silence + playground) ----

  bool _engaged;   // a lesson pass is running (or holding its final state)
  bool _armed = true; // the child must be clear to arm the next pass
  bool _passDone;     // the last pass reached its end (waiting for re-arm)

  // Starts a clean pass. Called from WatchPlayer when an ARMED child walks in.
  void BeginLesson() {
    _engaged = true;
    _armed = false;
    _passDone = false;
    ResetActors();
    _phase = DemoPhase.Ready;
    _phaseT = 0f;
    _firedPick = false;
    _placed = false;
    _didPulse = false;
    _saidNumber = false;
    _saidToday = false;
    _saidAssign = false;
    _asked = false;
    _cheered = false;
    _ball0 = null;
    _ball1 = null;
    try { _audio.SetAudioFocus(AudioFocusMode.Learning); } catch (Exception) { }
    try { Debug.Log("[CountingDemo] lesson start (audience arrived)", this); } catch (Exception) { }
  }

  // Child left: cut the voice immediately and return the stage to its idle,
  // playground state (no speech, no half-played lesson left behind).
  void AbortLesson() {
    _engaged = false;
    _armed = true;
    _passDone = false;
    _focusRun = false; // defensive: any abort also releases the focused run
    StopVoice();
    ResetActors();
    _phase = DemoPhase.Ready;
    _phaseT = 0f;
    try { Debug.Log("[CountingDemo] lesson aborted (audience left): voice off, stage reset", this); }
    catch (Exception) { }
  }

  void StopVoice() {
    try {
      if (_audio == null) return;
      _audio.SetAudioFocus(AudioFocusMode.Muted);   // Director: stops all voice
      _audio.SetAudioFocus(AudioFocusMode.Learning); // restore the normal focus
    } catch (Exception) { }
  }

  Vector3 ObserveLocal() {
    if (ObserveTarget != null && _stageParent != null)
      return _stageParent.InverseTransformPoint(ObserveTarget.position);
    return _refs.BasketStand;
  }

  // ---- reference-gameplay API (S3-P2Z4) -----------------------------------------

  void PointActor(NpcActor actor, Vector3 localTarget, float seconds) {
    if (actor == null) return;
    actor.PointTarget = localTarget;
    actor.PointT = Mathf.Max(0.4f, seconds);
  }

  // Teacher points (head + torso + arm) at a WORLD target for `seconds`.
  public void PointTeacherAt(Vector3 worldTarget, float seconds) {
    if (_teacher == null || _stageParent == null) return;
    _teacher.PointTarget = _stageParent.InverseTransformPoint(worldTarget);
    _teacher.PointT = Mathf.Max(0.4f, seconds);
  }

  public void PointStudentAt(Vector3 worldTarget, float seconds) {
    if (_student == null || _stageParent == null) return;
    _student.PointTarget = _stageParent.InverseTransformPoint(worldTarget);
    _student.PointT = Mathf.Max(0.4f, seconds);
  }

  public void NodStudent() { if (_student != null) _student.NodT = 0.6f; }

  // Teacher line for the gameplay beats (same voice/priority path as the lesson).
  public void TeacherSay(string en, string vi, SpeechStyle style, AudioPriority priority) {
    Speak(DialogueLang.T(en, vi), style, priority);
  }

  public void TeacherSay(string en, string vi) {
    TeacherSay(en, vi, SpeechStyle.Clear, AudioPriority.P2_Instruction);
  }

  public void CelebrateBoth() {
    Trigger(_teacher, "Celebrate");
    Trigger(_student, "Celebrate");
    Wave(_teacher);
    Pulse(_teacher, CharacterExpression.Happy, 3f);
    Pulse(_student, CharacterExpression.Happy, 3f);
    Hop(_teacher);
    Hop(_student);
    Confetti(new Vector3(0f, 2.4f, 3.2f), 22, 77, 3.2f, 2.6f);
    Sparkle(new Vector3(0f, 1.2f, 3.2f), 12, 78, 0.9f);
  }

  public bool IntroDone { get { return _phase == DemoPhase.Observing; } }
  public bool PassDone { get { return _passDone; } }

  // Re-entry: jump straight to the observing state (no intro replay).
  public void SkipToObserving() {
    _phase = DemoPhase.Observing;
    _phaseT = 0f;
    _engaged = true;  // the completed tableau still breathes/observes
    _passDone = true;
    ResetActors();
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
      Vector3 home = _refs.BallHomes[i];
      try {
        Vector3 p = b.transform.localPosition;
        if ((p - home).sqrMagnitude > 0.01f) continue; // flying/placed: leave it
        b.transform.localPosition = new Vector3(home.x,
          home.y + Mathf.Sin(Time.time * 2.1f + _ballPhase[i % _ballPhase.Length]) * 0.015f, home.z);
      } catch (Exception) { }
    }
    // S3-P2Y squash & stretch: hops read as weight, not a sliding pose.
    TickSquash(_teacher, dt);
    TickSquash(_student, dt);
    // Basket pop after each ball lands.
    if (_basket != null && _basketPopT < 1f) {
      _basketPopT = Mathf.Min(1f, _basketPopT + dt / 0.35f);
      float s = 1f + 0.14f * Mathf.Sin(Mathf.PI * _basketPopT);
      try {
        _basket.localScale = new Vector3(_basketBase.x * s, _basketBase.y, _basketBase.z * s);
      } catch (Exception) { }
    }
  }

  void TickSquash(NpcActor actor, float dt) {
    if (actor == null || actor.Visual == null) return;
    if (actor.SquashT <= 0f) return;
    try {
      actor.SquashT = Mathf.Max(0f, actor.SquashT - dt);
      float k = actor.SquashT / 0.35f;
      float bulge = Mathf.Sin(k * Mathf.PI);
      Vector3 baseS = actor.BaseScale;
      float sy = 1f + 0.16f * bulge;
      float sxz = 1f - 0.11f * bulge;
      actor.Visual.localScale = new Vector3(baseS.x * sxz, baseS.y * sy, baseS.z * sxz);
      if (actor.SquashT <= 0f) actor.Visual.localScale = baseS;
    } catch (Exception) { }
  }

  // ---- juice helpers (S3-P2Y) -----------------------------------------------------

  void Sparkle(Vector3 localPos, int count, int seed, float radius) {
    if (_fxRoot == null) return;
    try { DemoJuice.Sparkle(_fxRoot, localPos, count, seed, radius); } catch (Exception) { }
  }

  void Confetti(Vector3 localPos, int count, int seed, float spread, float kick) {
    if (_fxRoot == null) return;
    try { DemoJuice.Confetti(_fxRoot, localPos, count, seed, spread, kick); } catch (Exception) { }
  }

  void BasketPop() { _basketPopT = 0f; }

  Vector3 ResultPoint() {
    return _result != null ? _result.transform.localPosition + new Vector3(0f, 1.2f, 0f)
      : CountingGardenBuilder.DemoResultPos + new Vector3(0f, 1.2f, 0f);
  }

  void SpeelCelebrate() {
    Speak(DialogueLang.T("Yes! Two balls! Well done!", "Đúng rồi! Hai bóng! Giỏi!"),
      SpeechStyle.Excited, AudioPriority.P4_Feedback);
  }

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
    }
  }

  // ---- shot control --------------------------------------------------------------

  void SetShot(int shot) {
    if (_shot == shot) return;
    _shot = shot;
    IssueShot(); // crisp reframe on the phase boundary (user camera rule)
  }

  void IssueShot() {
    ShotIssues++;
    if (_cam == null) return;
    Transform c, l;
    if (_shot == 2) { c = _camC; l = _lookC; }
    else if (_shot == 1) { c = _camB; l = _lookB; }
    else { c = _camA; l = _lookA; }
    if (c == null || l == null) return;
    try {
      _cam.FrameAnchor(c, l, BeatHoldSeconds);
      Debug.Log("[CountingDemo] shot " + _shot + " issued (reissue=" + _beatReissues
        + " total=" + ShotIssues + ").", this);
    } catch (Exception) { }
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
    return index >= 0 && index < _refs.BallHomes.Length
      ? _refs.BallHomes[index] : Vector3.zero;
  }

  Vector3 BallPoint() { return _refs.BallStand + new Vector3(0f, 0.3f, 0f); }
  Vector3 BallPoint2() { return _refs.BallStand2 + new Vector3(0f, 0.3f, 0f); }
  Vector3 BallFieldPoint() { return _refs.BallFieldPoint; }
  Vector3 BoardPoint() { return _refs.BoardPoint; }
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
      : _refs.BasketStand + new Vector3(0f, 0.4f, 0f);
  }
  Vector3 ViewerPoint() {
    return _camA != null ? _camA.localPosition : _refs.Mouth;
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
    Vector3 b = _basket != null ? _basket.localPosition : _refs.BasketStand;
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

  // S3-P2Z6 (user: "để nó có ngắt nghỉ chứ đừng nói một mạch"): every line is
  // PACED — the next line waits until the previous one finished playing (the
  // Director's Task completes with playback) plus a short breath. Same-priority
  // lines used to queue in the Director and stream back-to-back, which read as
  // one unbroken monologue. Only the latest pending line is kept (the audio
  // never lags far behind the acting).
  Task _speechTask;
  bool _hasPendingSpeech;
  string _pendingSpeech;
  SpeechStyle _pendingStyle;
  AudioPriority _pendingPriority;
  float _speechGapT;
  const float SpeechGapSeconds = 0.65f;

  void Speak(string text, SpeechStyle style, AudioPriority priority) {
    // Audience gate: no listener, no line (belt & braces with StopVoice).
    if (_audio == null || !_engaged || string.IsNullOrEmpty(text)) return;
    if (!VoiceIdle()) { // still talking (or breathing): hold the newest line
      _pendingSpeech = text;
      _pendingStyle = style;
      _pendingPriority = priority;
      _hasPendingSpeech = true;
      return;
    }
    Submit(text, style, priority);
  }

  bool VoiceIdle() {
    if (_speechTask != null && !_speechTask.IsCompleted) return false;
    return _speechGapT <= 0f;
  }

  void Submit(string text, SpeechStyle style, AudioPriority priority) {
    try {
      var req = new DialogueRequest(text, DemoVoice, DialogueLang.Language, 1f, 1f, style,
        AudioFormat.Mp3_44100, priority);
      _speechTask = _audio.SpeakAsync(req); // awaited implicitly by the pacer
      _speechGapT = SpeechGapSeconds;
      _hasPendingSpeech = false;
      _pendingSpeech = null;
      try { Debug.Log("[CountingDemo] say '" + text + "'", this); } catch (Exception) { }
    } catch (Exception) { }
  }

  // Paced drain: runs every frame (also while the audience is away so a stale
  // line never fires after a pause).
  void TickSpeech(float dt) {
    if (_speechGapT > 0f) _speechGapT -= dt;
    if (!_engaged) { _hasPendingSpeech = false; _pendingSpeech = null; return; }
    if (!_hasPendingSpeech || string.IsNullOrEmpty(_pendingSpeech)) return;
    if (!VoiceIdle()) return;
    Submit(_pendingSpeech, _pendingStyle, _pendingPriority);
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
    if (actor != null) actor.SquashT = 0.35f;
  }

  void Wave(NpcActor actor) {
    if (actor == null) return;
    actor.WaveT = 1.4f;
  }

  void TickWave(float dt) {
    TickWaveOne(_teacher, dt);
    TickWaveOne(_student, dt);
  }

  // Point gesture (S3-P2Z4): the whole body turns to the target (head follows
  // via the rig) and the right arm aims at it — head + torso + arm together,
  // never an arm floating against a wrong gaze.
  void TickPoint(NpcActor actor, float dt) {
    if (actor == null || actor.Root == null || actor.PointT <= 0f) return;
    try {
      actor.PointT = Mathf.Max(0f, actor.PointT - dt);
      FaceTowards(actor, actor.PointTarget, dt, 5f);
      if (actor.WaveBone != null) {
        if (!actor.Waving) {
          actor.Waving = true;
          actor.WaveBase = actor.WaveBone.localRotation;
        }
        actor.WaveBone.localRotation = actor.WaveBase * Quaternion.Euler(0f, 0f, -68f);
      }
      if (actor.PointT <= 0f && actor.WaveBone != null) {
        actor.WaveBone.localRotation = actor.WaveBase;
        actor.Waving = false;
      }
    } catch (Exception) { }
  }

  // Small affirmative nod (student "understood" beat).
  void TickNod(NpcActor actor, float dt) {
    if (actor == null || actor.HeadBone == null || actor.NodT <= 0f) return;
    try {
      actor.NodT = Mathf.Max(0f, actor.NodT - dt);
      float k = 1f - actor.NodT / 0.6f;
      actor.HeadBone.localRotation = actor.HeadBase *
        Quaternion.Euler(Mathf.Sin(k * Mathf.PI) * 14f, 0f, 0f);
      if (actor.NodT <= 0f) actor.HeadBone.localRotation = actor.HeadBase;
    } catch (Exception) { }
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

  // ---- camera-first watching + audience gate --------------------------------------

  const float AbortMargin = 1.5f; // hysteresis: a step out never kills the lesson
  // Standing anywhere inside the stage plot also counts as attending the
  // lesson (the plot is the theatre floor).
  const float StageAttendRadius = 3.4f;
  // Island-local centre of the actual ball field, resolved lazily from the LIVE
  // ball transforms: the garden stages the lesson as a SCALED miniature, so the
  // authored refs' local space is not the player's space (using refs.Center
  // made this circle land in the middle of the yard — journey-caught).
  Vector3 _stageAttendLocal;
  bool _stageAttendValid;

  void EnsureStageAttendLocal() {
    if (_stageAttendValid) return;
    Vector3 c = Vector3.zero;
    int n = 0;
    for (int i = 0; i < _balls.Count; i++) {
      GameObject b = _balls[i];
      if (b == null) continue;
      c += b.transform.position;
      n++;
    }
    _stageAttendLocal = n > 0 ? (c / n) - _islandOffset : (_mouth + _islandOffset) - _islandOffset;
    _stageAttendValid = true;
  }

  static float d2s(Vector3 a, Vector3 b) {
    float x = a.x - b.x, z = a.z - b.z;
    return x * x + z * z;
  }
  // Per-site audience radius (set by each Build): the garden lesson triggers AT
  // THE STAGE DOOR (2.2m), the compact arena at its spawn (wide). A single
  // shared radius made the garden start the lesson from the middle of the yard.
  float _watchRadius = CountingGardenBuilder.DemoViewRadius;

  void WatchPlayer() {
    // The audience gate runs on its own (no camera needed): worlds/tests without
    // a live SmartCamera still get the one-pass lesson contract.
    if (_playerT == null) return;
    try {
      Vector3 local = _playerT.position - _islandOffset;
      float dx = local.x - _mouth.x, dz = local.z - _mouth.z;
      float d2 = dx * dx + dz * dz;
      float insideR = _watchRadius * _watchRadius;
      float abortR = (_watchRadius + AbortMargin) * (_watchRadius + AbortMargin);
      float clearR = (_watchRadius + RearmMargin) * (_watchRadius + RearmMargin);
      // Standing INSIDE the stage plot counts as attending too (user: "bấm vào
      // vườn đếm" walks the child into the plot, away from the door radius —
      // the NPC must still run the lesson there).
      EnsureStageAttendLocal();
      bool atStage = (d2s(local, _stageAttendLocal) <= StageAttendRadius * StageAttendRadius);
      bool inside = d2 <= insideR || atStage;
      if (!inside) _armed = true; // must stand clear to arm the next pass

      // Audience gate: one pass per visit; the child must walk clear to re-arm.
      // A FOCUSED run (zone clicked) is exempt: the zone owns it and releases
      // it explicitly, so distance never kills a lesson the child asked for.
      if (AudienceGateEnabled && inside && !_engaged && _armed) BeginLesson();
      if (AudienceGateEnabled && _engaged && !_focusRun
          && d2 > abortR && !atStage) {
        AbortLesson();
        _beatLatched = false; // walking back in re-frames the stage
        return;
      }
      // After control has passed (or on a completed re-entry) the demo neither
      // re-teaches nor hijacks the camera: the child plays.
      if (!AudienceGateEnabled) return;
      // Camera beat (optional: needs live stage + camera refs; the garden
      // miniature keeps its zone-focus camera).
      if (!CameraBeatsEnabled) return;
      if (_cam == null || _camA == null || _lookA == null) return;
      if (inside) {
        if (!_beatLatched) {
          _beatLatched = true;
          DemoBeatsFired++;
          _beatRefreshT = 0f;
          _beatReissues = 0;
          _beatReleased = false;
          IssueShot();
        } else if (_beatReissues < MaxBeatReissues) {
          _beatRefreshT += Time.deltaTime;
          if (_beatRefreshT >= BeatHoldSeconds - 1.2f) {
            _beatRefreshT = 0f;
            _beatReissues++;
            IssueShot();
          }
        } else if (!_beatReleased) {
          // Explicit hand-back: the child has watched; the camera must return
          // to them even if the beat timer path ever stalls (journey report:
          // the card held the camera and hid the way out behind it).
          _beatReleased = true;
          try {
            if (_cam != null && _playerT != null) _cam.Follow(_playerT, FollowOffset);
            Debug.Log("[CountingDemo] beat hold finished; camera returned to follow.", this);
          } catch (Exception) { }
        }
      } else if (_beatLatched && d2 > clearR) {
        _beatLatched = false;
        _beatReissues = 0;
        _beatReleased = false;
      }
    } catch (Exception) { }
  }
}
