// A_World/CountingGarden/RabbitLessonDemo.cs — S3-P2Z19 user round.
// The GARDEN-side miniature lesson for the carrot patch (zone 0): "Vườn củ cà
// rốt ở ngoài chưa có demo ở sân chọn game." A compact diorama of the rabbit
// arena — the "3" board, a mini carrot patch, a mini hutch with the bunny and
// its bowl ON the roof — where the teacher ASKS the number, the student
// ANSWERS, then demonstrates the simplified gameplay (pick -> carry -> feed)
// while the teacher counts. It is the tutorial for the real arena (which no
// longer replays any demo), exactly like the other zones' mini lessons.
// Runs ambient when the child walks up (audience gate, one pass per visit) and
// on zone focus (StartFocusedLesson — the panel opens after this pass);
// StopFocusedLesson cuts the voice and resets. NO bus, NO camera, NO gameplay
// state: pure presentation in the garden. C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RabbitLessonDemo : MonoBehaviour, IGardenZoneDemo {
  public enum Phase { Ready, Beat, Observing }

  public const float MiniScale = 0.62f;
  public const int DemoItems = 3; // the reference round
  public static readonly Vector3 BoardPos = new Vector3(-1.7f, 0f, 1.9f);
  public static readonly Vector3 TeacherStart = new Vector3(-1.15f, 0f, 1.35f);
  public static readonly Vector3 StudentStart = new Vector3(0.15f, 0f, 0.95f);
  public static readonly Vector3 PatchCenter = new Vector3(-1.6f, 0f, 0.7f);
  public static readonly Vector3 PatchStand = new Vector3(-0.9f, 0f, 0.85f);
  public static readonly Vector3 HutchCenter = new Vector3(1.55f, 0f, 0.7f);
  public static readonly Vector3 FeedStand = new Vector3(0.85f, 0f, 0.85f);
  public static readonly Vector3 BowlPos = new Vector3(1.55f, 0.62f, 0.42f);
  // Fed carrots land in DISTINCT slots (user: "3 củ dồn vào 1 chỗ, chỉ thấy 1"):
  // a small 3-slot zig-zag inside the bowl so every fed carrot reads on camera.
  static readonly Vector2[] BowlOffsets = {
    new Vector2(-0.065f, -0.02f), new Vector2(0.07f, -0.06f), new Vector2(-0.02f, 0.075f),
  };
  Vector3 BowlSlot(int i) {
    if (i < 0) i = 0;
    if (i >= BowlOffsets.Length) i = BowlOffsets.Length - 1;
    Vector2 o = BowlOffsets[i];
    return new Vector3(BowlPos.x + o.x, BowlPos.y + 0.06f, BowlPos.z + o.y);
  }
  public static readonly Vector3 BoardPoint = new Vector3(-1.7f, 1.35f, 1.65f);
  const float WalkSpeed = 0.9f;
  // Audience gate (same contract as the other miniature lessons).
  const float AudienceRadius = 4.2f;
  const float RearmMargin = 1.6f;

  static readonly Color BoardCream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color BasketBrown = new Color(0.55f, 0.38f, 0.22f);
  static readonly Color SoilBrown = new Color(0.45f, 0.32f, 0.20f);
  static readonly Color MintLeaf = new Color(0.70f, 0.90f, 0.72f);
  static readonly Color BunnyWhite = new Color(0.96f, 0.95f, 0.93f);

  static readonly string[] NumEn = { "one", "two", "three" };
  static readonly string[] NumVi = { "một", "hai", "ba" };
  static readonly string[] CountEn = { "One carrot.", "Two carrots.", "Three carrots." };
  static readonly string[] CountVi = { "Một củ cà rốt.", "Hai củ cà rốt.", "Ba củ cà rốt." };

  public Phase Current { get; private set; } = Phase.Ready;
  public int LoopCount { get; private set; }
  public bool PassDone { get; private set; }
  public bool ActorsBuilt { get { return _teacher != null && _teacher.Root != null
      && _student != null && _student.Root != null; } }
  public bool Engaged { get { return _engaged; } }
  public bool FocusRun { get { return _focusRun; } }
  public Transform MiniRoot { get { return _root; } }
  public int CarrotsFed { get { return _fed; } }

  CountingGardenBuilder _garden;
  Transform _root;
  Transform _player;
  Vector3 _worldCenter;
  IAudioDirector _audio;
  PacedVoice _voice;
  LessonActor _teacher;
  LessonActor _student;
  GameObject[] _items;
  Vector3[] _itemHomes;
  GameObject _rabbit;
  GameObject _rabbitHead;
  Vector3 _headBase;
  Transform _fx;

  int _beat = -1;
  int _stage;        // 0 walk to patch, 1 picking, 2 walk to bunny, 3 feeding
  int _fed;
  float _beatT;
  bool _itemFlying;
  float _flyT;
  bool _saidAnswer, _saidYes;
  bool _engaged;
  bool _focusRun;
  bool _audienceArmed = true;
  bool _built;
  float _nibbleT;

  // Scene wiring (GameInstaller calls this when the garden scene loads).
  public void Build(CountingGardenBuilder garden, Transform playerT, IAudioDirector audio) {
    if (garden == null || garden.ZoneCenters == null
        || garden.ZoneCenters.Count <= CountingGardenBuilder.RabbitZoneIndex) {
      Debug.LogWarning("[RabbitLessonDemo] no carrot plot; demo parked.", this);
      return;
    }
    _garden = garden;
    _player = playerT;
    _audio = audio;
    Vector3 center = garden.ZoneCenters[CountingGardenBuilder.RabbitZoneIndex];
    Vector3 outDir = (center - CountingGardenBuilder.ArcCenter).normalized;
    float outYaw = Mathf.Atan2(outDir.x, outDir.z) * Mathf.Rad2Deg;
    GameObject mini = new GameObject("CGRabbitDemoMiniRoot");
    mini.transform.SetParent(transform, false);
    mini.transform.localPosition = center;
    mini.transform.localRotation = Quaternion.Euler(0f, outYaw, 0f);
    mini.transform.localScale = Vector3.one * MiniScale;
    _root = mini.transform;
    _worldCenter = transform.TransformPoint(center);
    BuildStage(_root);
    BuildActors(_root);
    GameObject fx = new GameObject("CGRabbitDemoFx");
    fx.transform.SetParent(_root, false);
    _fx = fx.transform;
    _voice = new PacedVoice();
    _voice.Audio = audio;
    _voice.LogTag = "RabbitLessonDemo";
    ResetStage();
    Current = Phase.Ready;
    _built = true;
    try { Debug.Log("[RabbitLessonDemo] miniature built at " + _worldCenter.ToString("F1")); }
    catch (Exception) { }
  }

  // ---- diorama -----------------------------------------------------------------

  void BuildStage(Transform parent) {
    // Board "3" facing the mouth (same staging as the harvest mini lessons).
    GameObject board = new GameObject("CGRabbitMiniBoard");
    board.transform.SetParent(parent, false);
    board.transform.localPosition = BoardPos;
    Box(board.transform, "CGRabbitMiniBoardL", new Vector3(-0.72f, 0.58f, 0f),
      new Vector3(0.10f, 1.16f, 0.10f), BasketBrown);
    Box(board.transform, "CGRabbitMiniBoardR", new Vector3(0.72f, 0.58f, 0f),
      new Vector3(0.10f, 1.16f, 0.10f), BasketBrown);
    Box(board.transform, "CGRabbitMiniBoardPanel", new Vector3(0f, 1.36f, 0f),
      new Vector3(1.55f, 1.15f, 0.09f), BoardCream);
    CountingGardenBuilder.Digit(board.transform, "CGRabbitMiniBoard3",
      new Vector3(0f, 1.02f, -0.13f), 0.82f, 0.62f, Gold, 90f, DemoItems);
    NoShadows(board);

    // Mini carrot patch: soil pad + three real carrots (Kenney prop).
    Cyl(parent, "CGRabbitMiniSoil", PatchCenter + new Vector3(0f, 0.02f, 0f), 1.5f, 0.05f, SoilBrown);
    _items = new GameObject[DemoItems];
    _itemHomes = new Vector3[DemoItems];
    Vector3[] offs = {
      PatchCenter + new Vector3(-0.4f, 0.03f, -0.15f),
      PatchCenter + new Vector3(0.05f, 0.03f, 0.25f),
      PatchCenter + new Vector3(0.45f, 0.03f, -0.1f),
    };
    for (int i = 0; i < DemoItems; i++) {
      _itemHomes[i] = offs[i];
      GameObject item = new GameObject("CGRabbitMiniCarrot" + i);
      item.transform.SetParent(parent, false);
      item.transform.localPosition = offs[i];
      item.transform.localRotation = Quaternion.Euler(0f, i * 55f, 0f);
      PropKit.Place(item.transform, "carrot", Vector3.zero, 0f, 0.7f);
      _items[i] = item;
    }

    // Mini hutch with the bunny + bowl ON the roof (the arena's new design).
    Box(parent, "CGRabbitMiniHutchBack", HutchCenter + new Vector3(0f, 0.28f, 0.45f),
      new Vector3(0.9f, 0.55f, 0.09f), BasketBrown);
    Box(parent, "CGRabbitMiniHutchSideL", HutchCenter + new Vector3(-0.42f, 0.28f, 0f),
      new Vector3(0.09f, 0.55f, 0.85f), BasketBrown);
    Box(parent, "CGRabbitMiniHutchSideR", HutchCenter + new Vector3(0.42f, 0.28f, 0f),
      new Vector3(0.09f, 0.55f, 0.85f), BasketBrown);
    Box(parent, "CGRabbitMiniHutchRoof", HutchCenter + new Vector3(0f, 0.60f, 0f),
      new Vector3(1.1f, 0.07f, 1.0f), new Color(0.97f, 0.72f, 0.80f));
    Cyl(parent, "CGRabbitMiniBowl", BowlPos + new Vector3(0f, 0.03f, 0f), 0.42f, 0.09f,
      new Color(0.99f, 0.93f, 0.88f));
    // The bunny: body + head + ears, sitting on the roof beside the bowl.
    GameObject rabbit = new GameObject("CGRabbitMiniBunny");
    rabbit.transform.SetParent(parent, false);
    rabbit.transform.localPosition = new Vector3(BowlPos.x + 0.05f, 0.63f, BowlPos.z + 0.42f);
    rabbit.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0f, -1f));
    _rabbit = rabbit;
    Ball(rabbit.transform, "CGRabbitMiniBody", new Vector3(0f, 0.11f, 0.06f), 0.30f, BunnyWhite);
    GameObject head = new GameObject("CGRabbitMiniHead");
    head.transform.SetParent(rabbit.transform, false);
    head.transform.localPosition = new Vector3(0f, 0.24f, -0.12f);
    _rabbitHead = head;
    _headBase = head.transform.localPosition;
    Ball(head.transform, "CGRabbitMiniSkull", Vector3.zero, 0.18f, BunnyWhite);
    Ball(head.transform, "CGRabbitMiniEyeL", new Vector3(-0.05f, 0.03f, -0.07f), 0.035f, Color.black);
    Ball(head.transform, "CGRabbitMiniEyeR", new Vector3(0.05f, 0.03f, -0.07f), 0.035f, Color.black);
    Ball(rabbit.transform, "CGRabbitMiniEarL", new Vector3(-0.05f, 0.42f, -0.1f), 0.09f, BunnyWhite);
    Ball(rabbit.transform, "CGRabbitMiniEarR", new Vector3(0.05f, 0.42f, -0.1f), 0.09f, BunnyWhite);
  }

  void BuildActors(Transform parent) {
    _teacher = LessonActors.Build(parent, "CGRabbitMiniTeacher", "NpcVisuals/TessVisual", 0.5f,
      TeacherStart, new Color(0.25f, 0.45f, 0.85f), Gold);
    _student = LessonActors.Build(parent, "CGRabbitMiniStudent", "NpcVisuals/MiloVisual", 0.42f,
      StudentStart, new Color(0.30f, 0.62f, 0.45f), new Color(0.55f, 0.35f, 0.20f));
  }

  // ---- audience gate + focus ----------------------------------------------------

  void Watch(float dt) {
    if (!_built || _player == null || _focusRun) return;
    Vector3 p = _player.position;
    float dx = p.x - _worldCenter.x, dz = p.z - _worldCenter.z;
    float d2 = dx * dx + dz * dz;
    float outR = AudienceRadius + RearmMargin;
    if (_engaged && d2 > outR * outR) { Abort("child left"); return; }
    if (_engaged) return;
    if (d2 > outR * outR) _audienceArmed = true;
    if (_audienceArmed && d2 <= AudienceRadius * AudienceRadius) {
      _audienceArmed = false;
      BeginPass("audience arrived");
    }
  }

  public void StartFocusedLesson() {
    if (!_built || _engaged) return;
    _focusRun = true;
    BeginPass("zone focused");
  }

  public void StopFocusedLesson() {
    if (!_focusRun) return;
    _focusRun = false;
    Abort("zone focus released");
  }

  void BeginPass(string reason) {
    _engaged = true;
    PassDone = false;
    _fed = 0;
    _stage = 0;
    _itemFlying = false;
    _flyT = 0f;
    _saidYes = false;
    ResetStage();
    _beat = 0;
    _beatT = 0f;
    Current = Phase.Beat;
    try { _audio.SetAudioFocus(AudioFocusMode.Learning); } catch (Exception) { }
    Log("lesson start (" + reason + ")");
  }

  void Abort(string reason) {
    _engaged = false;
    PassDone = false;
    _beat = -1;
    StopVoice();
    ResetStage();
    Current = Phase.Ready;
    Log("lesson aborted (" + reason + ")");
  }

  void StopVoice() {
    try {
      if (_audio == null) return;
      _audio.SetAudioFocus(AudioFocusMode.Muted);
      _audio.SetAudioFocus(AudioFocusMode.Learning);
    } catch (Exception) { }
  }

  void CompletePass() {
    _engaged = false;
    PassDone = true;
    LoopCount++;
    Current = Phase.Observing;
    Log("pass done (loop " + LoopCount + ")");
  }

  // ---- the scripted beats -------------------------------------------------------

  void To(int next) { _beat = next; _beatT = 0f; }

  void Step(float dt) {
    if (!_built) return;
    Watch(dt);
    TickVoice(dt);
    TickActing(dt);
    if (Current != Phase.Beat) return;
    _beatT += dt;
    switch (_beat) {
      case 0:
        FaceTowards(_teacher, BoardPoint, dt, 4f);
        FaceTowards(_student, TeacherPoint(), dt, 3f);
        if (_beatT >= 0.5f) { Wave(_teacher); Say("Look at the board!", "Nhìn lên bảng nhé!"); To(1); }
        break;
      case 1:
        FaceTowards(_teacher, BoardPoint, dt, 4f);
        if (_beatT >= 1.2f) { Say("This is number three.", "Đây là số ba."); Point(_teacher, BoardPoint, 2.0f); To(2); }
        break;
      case 2:
        FaceTowards(_teacher, StudentPoint(), dt, 4f);
        if (_beatT >= 1.8f) { Say("How many carrots?", "Có mấy củ cà rốt?"); To(3); }
        break;
      case 3:
        FaceTowards(_student, TeacherPoint(), dt, 4f);
        if (!_saidAnswer && _beatT >= 0.8f) {
          _saidAnswer = true;
          Say("Three!", "Ba ạ!");
          Nod(_student);
        }
        if (_beatT >= 2.0f) { Say("Pick three carrots!", "Hái ba củ cà rốt nhé!"); Point(_teacher, PatchCenter, 2.0f); To(4); }
        break;
      case 4: RunPickFeed(dt); break;
      case 5:
        if (!_saidYes && _beatT >= 0.4f) {
          _saidYes = true;
          Say("Yes! Three carrots!", "Đúng rồi! Ba củ cà rốt!");
          Wave(_teacher);
          HopRabbit();
          Sparkle(BowlPos, 8, 61, 0.3f);
        }
        if (_beatT >= 2.4f) { Say("Now it's your turn!", "Giờ đến lượt con!"); To(6); }
        break;
      default:
        if (_beatT >= 1.4f) CompletePass();
        break;
    }
  }

  // One carrot at a time, for real: walk to the patch -> pick (arc to the
  // student's fist) -> walk to the hutch -> feed (arc into the roof bowl) ->
  // the teacher counts. Same simplified gameplay as the real arena.
  void RunPickFeed(float dt) {
    GameObject item = _fed < DemoItems ? _items[_fed] : null;
    switch (_stage) {
      case 0:
        FaceTowards(_student, PatchCenter, dt, 5f);
        if (_beatT >= 0.8f && WalkTo(_student, PatchStand, dt)) {
          _stage = 1;
          if (item != null) { _itemFlying = false; }
        }
        break;
      case 1:
        if (item != null && !_itemFlying) {
          _itemFlying = true;
          _flyT = 0f;
        }
        if (_itemFlying) {
          _flyT += dt;
          float t = Mathf.Clamp01(_flyT / 0.4f);
          Vector3 hand = StudentHandLocal();
          Vector3 mid = (_itemHomes[_fed] + hand) * 0.5f + new Vector3(0f, 0.3f, 0f);
          item.transform.localPosition = Vector3.Lerp(
            Vector3.Lerp(_itemHomes[_fed], mid, t), Vector3.Lerp(mid, hand, t), t);
          if (t >= 1f) { _stage = 2; _itemFlying = false; }
        }
        break;
      case 2:
        FaceTowards(_student, HutchCenter, dt, 5f);
        if (item != null) item.transform.localPosition = StudentHandLocal();
        if (WalkTo(_student, FeedStand, dt)) {
          _stage = 3;
          _itemFlying = false;
          _flyT = 0f;
        }
        break;
      default:
        if (item != null) {
          Vector3 slot = BowlSlot(_fed);
          _flyT += dt;
          float t = Mathf.Clamp01(_flyT / 0.4f);
          Vector3 from = StudentHandLocal();
          Vector3 mid = (from + slot) * 0.5f + new Vector3(0f, 0.3f, 0f);
          item.transform.localPosition = Vector3.Lerp(
            Vector3.Lerp(from, mid, t), Vector3.Lerp(mid, slot, t), t);
          if (t >= 1f) {
            item.transform.localPosition = slot;
            _fed++;
            Nibble();
            Say(CountEn[_fed - 1], CountVi[_fed - 1]);
            Point(_teacher, BowlPos, 1.4f);
            if (_fed >= DemoItems) { To(5); }
            else { _stage = 0; _beatT = 0f; _itemFlying = false; }
          }
        } else {
          _fed++;
          if (_fed >= DemoItems) To(5); else { _stage = 0; _beatT = 0f; }
        }
        break;
    }
  }

  void ResetStage() {
    _saidAnswer = false;
    if (_items == null) return;
    for (int i = 0; i < _items.Length; i++) {
      if (_items[i] == null) continue;
      _items[i].transform.localPosition = _itemHomes[i];
    }
    if (_teacher != null && _teacher.Root != null) {
      _teacher.Root.transform.localPosition = TeacherStart;
      FaceSnap(_teacher, BoardPoint);
    }
    if (_student != null && _student.Root != null) {
      _student.Root.transform.localPosition = StudentStart;
      FaceSnap(_student, TeacherPoint());
    }
    if (_rabbitHead != null) _rabbitHead.transform.localPosition = _headBase;
  }

  // ---- motion / acting helpers ---------------------------------------------------

  Vector3 StudentHandLocal() {
    if (_student == null) return PatchStand;
    if (_student.HandBone != null)
      return _root.InverseTransformPoint(_student.HandBone.position);
    if (_student.CarryAnchor != null)
      return _root.InverseTransformPoint(_student.CarryAnchor.position);
    Vector3 p = _student.Root.transform.localPosition;
    return p + new Vector3(0f, 0.6f, 0f);
  }

  Vector3 TeacherPoint() {
    return _teacher != null && _teacher.Root != null
      ? _teacher.Root.transform.localPosition + new Vector3(0f, 1.0f, 0f)
      : TeacherStart + new Vector3(0f, 1.0f, 0f);
  }
  Vector3 StudentPoint() {
    return _student != null && _student.Root != null
      ? _student.Root.transform.localPosition + new Vector3(0f, 0.85f, 0f)
      : StudentStart + new Vector3(0f, 0.85f, 0f);
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
  void Nod(LessonActor a) { if (a != null) a.NodT = 0.6f; }

  void Point(LessonActor a, Vector3 targetLocal, float seconds) {
    if (a == null) return;
    a.PointTarget = targetLocal;
    a.PointT = Mathf.Max(0.4f, seconds);
  }

  void TickActing(float dt) {
    TickWaveOne(_teacher, dt);
    TickWaveOne(_student, dt);
    TickPointOne(_teacher, dt);
    TickPointOne(_student, dt);
    TickNodOne(_student, dt);
    TickNibble(dt);
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

  void TickNodOne(LessonActor a, float dt) {
    if (a == null || a.HeadBone == null || a.NodT <= 0f) return;
    try {
      a.NodT = Mathf.Max(0f, a.NodT - dt);
      float k = 1f - a.NodT / 0.6f;
      a.HeadBone.localRotation = a.HeadBase *
        Quaternion.Euler(Mathf.Sin(k * Mathf.PI) * 14f, 0f, 0f);
      if (a.NodT <= 0f) a.HeadBone.localRotation = a.HeadBase;
    } catch (Exception) { }
  }

  void Nibble() { _nibbleT = 1.2f; }
  void HopRabbit() { _nibbleT = Mathf.Max(_nibbleT, 0.9f); }

  void TickNibble(float dt) {
    if (_nibbleT <= 0f || _rabbitHead == null) return;
    _nibbleT -= dt;
    float k = Mathf.Clamp01(1f - _nibbleT / 1.2f);
    float bob = Mathf.Sin(k * Mathf.PI * 3f) * 0.05f;
    try { _rabbitHead.transform.localPosition = _headBase + new Vector3(0f, -Mathf.Abs(bob), -bob * 0.5f); }
    catch (Exception) { }
    if (_nibbleT <= 0f) {
      try { _rabbitHead.transform.localPosition = _headBase; } catch (Exception) { }
    }
  }

  void TickVoice(float dt) { if (_voice != null) _voice.Tick(dt); }

  void Say(string en, string vi) {
    if (_voice == null) return;
    _voice.Speak(DialogueLang.T(en, vi));
  }

  void Sparkle(Vector3 localPos, int count, int seed, float radius) {
    if (_fx == null) return;
    try { DemoJuice.Sparkle(_fx, localPos, count, seed, radius); } catch (Exception) { }
  }

  void Log(string message) {
    try { Debug.Log("[RabbitLessonDemo] " + message, this); } catch (Exception) { }
  }

  void Update() {
    try { Step(Time.deltaTime); } catch (Exception) { }
  }

  // ---- tiny primitive helpers (same language as the other mini lessons) ----------

  static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    go.transform.localScale = scale;
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Strip(go);
    return go;
  }

  static GameObject Cyl(Transform parent, string name, Vector3 pos, float diameter, float height, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Strip(go);
    return go;
  }

  static GameObject Ball(Transform parent, string name, Vector3 pos, float diameter, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    go.transform.localScale = new Vector3(diameter, diameter, diameter);
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Strip(go);
    return go;
  }

  static void NoShadows(GameObject go) {
    try {
      foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) {
        if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      }
    } catch (Exception) { }
  }

  static void Strip(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (Exception) { }
  }

  static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

  static Material Lit(Color color) {
    string key = color.r.ToString("F2") + "," + color.g.ToString("F2") + "," + color.b.ToString("F2");
    Material cached;
    if (_mats.TryGetValue(key, out cached) && cached != null) return cached;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    mat.enableInstancing = true;
    _mats[key] = mat;
    return mat;
  }
}
