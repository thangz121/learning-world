// A_World/CountingGarden/StairLessonDemo.cs — S3-P2Z12b (user report: "NPC dạy
// trẻ chơi ở đâu? Sao không hiện?"). The GARDEN-side miniature lesson for the
// number-stair plot (zone 5): a compact diorama of the arena — the "3" board,
// three chunky steps with bead rows, the goal arch — with the SAME two-NPC
// teaching script as gameplay #1's theatre: the teacher links the number to the
// steps, the student climbs 1-2-3 while the teacher counts, both celebrate,
// the stage tidies and loops.
// Runs ambient when the child walks up to the plot (audience gate, one pass per
// visit) and on zone focus (StartFocusedLesson — the panel opens after this
// pass); StopFocusedLesson cuts the voice and resets. NO bus, NO camera, NO
// gameplay state: pure presentation in the garden, exactly like CountingDemo's
// garden instance. C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class StairLessonDemo : MonoBehaviour, IGardenZoneDemo {
  public enum Phase { Ready, Beat, Observing }

  // Diorama scale + compact stage layout (stage units; the mini root is scaled
  // and rotated so the stage faces the plot mouth).
  public const float MiniScale = 0.62f;
  public static readonly int DemoSteps = StairHillBuilder.Target; // 3 (the goal)
  public const float StepRise = 0.22f;
  public const float StepTread = 0.55f;
  public const float StepWidth = 2.4f;
  public const float BaseZ = 1.15f;
  public static readonly Vector3 BoardPos = new Vector3(-2.1f, 0f, 1.7f);
  public static readonly Vector3 TeacherStart = new Vector3(-1.45f, 0f, 1.35f);
  public static readonly Vector3 StudentStart = new Vector3(0.6f, 0f, 1.0f);
  public static readonly Vector3 StudentBase = new Vector3(0f, 0f, 0.9f);
  public static readonly Vector3 StudentReturn = new Vector3(1.15f, 0f, 1.05f);
  public static readonly Vector3 BoardPoint = new Vector3(-2.1f, 1.5f, 1.45f);
  public static readonly Vector3 StairsPoint = new Vector3(0f, 0.95f, BaseZ + StepTread);
  const float WalkSpeed = 0.85f;
  // Audience gate (same contract as the ball theatre's miniature): one pass per
  // visit; walking clear re-arms; a focused run is owned by the zone.
  const float AudienceRadius = 4.2f;
  const float RearmMargin = 1.6f;

  // Diorama tones (same family as the arena; no cross-builder private access).
  static readonly Color StepCap = new Color(0.90f, 0.82f, 0.62f);
  static readonly Color BoardCream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color BasketBrown = new Color(0.55f, 0.38f, 0.22f);
  static readonly Color Sky = new Color(0.45f, 0.70f, 0.92f);

  public Phase Current { get; private set; } = Phase.Ready;
  public int LoopCount { get; private set; }
  public bool PassDone { get; private set; }
  public bool ActorsBuilt { get { return _teacher != null && _teacher.Root != null
      && _student != null && _student.Root != null; } }
  public int StudentStep { get; private set; }
  public bool Engaged { get { return _engaged; } }
  public bool FocusRun { get { return _focusRun; } }
  public Transform MiniRoot { get { return _root; } }

  CountingGardenBuilder _garden;
  Transform _root;
  Transform _player;
  Vector3 _worldCenter;
  IAudioDirector _audio;
  PacedVoice _voice;
  LessonActor _teacher;
  LessonActor _student;
  GameObject _boardDigit;
  GameObject[] _stepCues;
  Transform _fx;

  int _beat = -1;
  float _beatT;
  bool _engaged;
  bool _focusRun;
  bool _passDoneLatch;
  bool _audienceArmed = true;
  bool _built;

  // Scene wiring (GameInstaller calls this when the garden scene loads).
  public void Build(CountingGardenBuilder garden, Transform playerT, IAudioDirector audio) {
    if (garden == null || garden.ZoneCenters == null
        || garden.ZoneCenters.Count <= CountingGardenBuilder.StairZoneIndex) {
      Debug.LogWarning("[StairLessonDemo] no stair plot in the garden; demo parked.", this);
      return;
    }
    _garden = garden;
    _player = playerT;
    _audio = audio;
    Vector3 center = garden.ZoneCenters[CountingGardenBuilder.StairZoneIndex];
    Vector3 outDir = (center - CountingGardenBuilder.ArcCenter).normalized;
    float outYaw = Mathf.Atan2(outDir.x, outDir.z) * Mathf.Rad2Deg;
    GameObject mini = new GameObject("CGStairDemoMiniRoot");
    mini.transform.SetParent(transform, false);
    mini.transform.localPosition = center;
    mini.transform.localRotation = Quaternion.Euler(0f, outYaw, 0f);
    mini.transform.localScale = Vector3.one * MiniScale;
    _root = mini.transform;
    _worldCenter = transform.TransformPoint(center);
    BuildStage(_root);
    BuildActors(_root);
    GameObject fx = new GameObject("CGStairDemoFx");
    fx.transform.SetParent(_root, false);
    _fx = fx.transform;
    _voice = new PacedVoice();
    _voice.Audio = audio;
    _voice.LogTag = "StairLessonDemo";
    ResetStage();
    Current = Phase.Ready;
    _built = true;
    try { Debug.Log("[StairLessonDemo] garden miniature built at " + _worldCenter.ToString("F1")); }
    catch (Exception) { }
  }

  // ---- diorama -----------------------------------------------------------------

  void BuildStage(Transform parent) {
    // Three chunky steps + a small landing (the goal), read as a staircase even
    // at toy scale (user: the old flat slabs read as planks).
    for (int i = 1; i <= DemoSteps; i++) {
      float top = i * StepRise;
      float zc = BaseZ + (i - 0.5f) * StepTread;
      Solid(parent, "CGStairMiniStep" + i, new Vector3(0f, top * 0.5f, zc),
        new Vector3(StepWidth, top, StepTread), CountingGardenBuilder.StepWood);
      Box(parent, "CGStairMiniStepTop" + i, new Vector3(0f, top + 0.004f, zc),
        new Vector3(StepWidth - 0.06f, 0.008f, StepTread - 0.05f), StepCap);
      GameObject cue = new GameObject("CGStairMiniCue" + i);
      cue.transform.SetParent(parent, false);
      cue.transform.localPosition = new Vector3(0f, top, zc);
      if (_stepCues == null) _stepCues = new GameObject[DemoSteps];
      _stepCues[i - 1] = cue;
      for (int b = 0; b < i; b++) {
        float t = i == 1 ? 0f : (b / (float)(i - 1) - 0.5f);
        Ball(cue.transform, "CGStairMiniBead" + i + "_" + b,
          new Vector3(-StepWidth * 0.5f + 0.22f, 0.10f, t * 0.34f), 0.13f,
          Gold);
      }
    }
    float top3 = DemoSteps * StepRise;
    float landingZ = BaseZ + DemoSteps * StepTread + 0.55f;
    Solid(parent, "CGStairMiniLanding", new Vector3(0f, top3 * 0.5f, landingZ),
      new Vector3(StepWidth + 0.6f, top3, 1.1f), CountingGardenBuilder.StepWood);
    // Goal arch + blossom on the landing (the top is a place).
    Box(parent, "CGStairMiniGoalL", new Vector3(-1.15f, top3 + 0.62f, landingZ + 0.1f),
      new Vector3(0.14f, 1.24f, 0.14f), BasketBrown);
    Box(parent, "CGStairMiniGoalR", new Vector3(1.15f, top3 + 0.62f, landingZ + 0.1f),
      new Vector3(0.14f, 1.24f, 0.14f), BasketBrown);
    Box(parent, "CGStairMiniGoalBeam", new Vector3(0f, top3 + 1.28f, landingZ + 0.1f),
      new Vector3(2.5f, 0.12f, 0.12f), Sky);
    Ball(parent, "CGStairMiniGoalBlossom", new Vector3(0f, top3 + 1.42f, landingZ + 0.1f),
      0.5f, WorldBeauty.BlossomPink);
    // Board "3" facing the mouth (same frame trick as every board: panel thin
    // in local z, digit at yaw 90, pushed clear of the panel mass).
    GameObject board = new GameObject("CGStairMiniBoard");
    board.transform.SetParent(parent, false);
    board.transform.localPosition = BoardPos;
    Box(board.transform, "CGStairMiniBoardL", new Vector3(-0.85f, 0.62f, 0f),
      new Vector3(0.11f, 1.24f, 0.11f), BasketBrown);
    Box(board.transform, "CGStairMiniBoardR", new Vector3(0.85f, 0.62f, 0f),
      new Vector3(0.11f, 1.24f, 0.11f), BasketBrown);
    Box(board.transform, "CGStairMiniBoardPanel", new Vector3(0f, 1.52f, 0f),
      new Vector3(1.8f, 1.3f, 0.1f), BoardCream);
    _boardDigit = CountingGardenBuilder.Digit3(board.transform, "CGStairMiniBoard3",
      new Vector3(0f, 1.14f, -0.15f), 0.9f, 0.7f, Gold, 90f);
    NoShadows(board); // the number is the one thing to read cleanly
    // Two little flags at the stair foot (identity: a place you climb).
    Flag(parent, "CGStairMiniFlagL", new Vector3(-1.6f, 0f, BaseZ - 0.15f));
    Flag(parent, "CGStairMiniFlagR", new Vector3(1.6f, 0f, BaseZ - 0.15f));
  }

  void Flag(Transform parent, string name, Vector3 pos) {
    Box(parent, name + "Post", pos + new Vector3(0f, 0.45f, 0f),
      new Vector3(0.06f, 0.9f, 0.06f), BasketBrown);
    Box(parent, name, pos + new Vector3(0.14f, 0.78f, 0f),
      new Vector3(0.26f, 0.17f, 0.02f), Gold);
  }

  void BuildActors(Transform parent) {
    _teacher = LessonActors.Build(parent, "CGStairMiniTeacher", "NpcVisuals/TessVisual", 0.5f,
      TeacherStart, new Color(0.25f, 0.45f, 0.85f), Gold);
    _student = LessonActors.Build(parent, "CGStairMiniStudent", "NpcVisuals/MiloVisual", 0.42f,
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
    _passDoneLatch = false;
    PassDone = false;
    StudentStep = 0;
    ResetStage();
    _beat = 0;
    _beatT = 0f;
    Current = Phase.Beat;
    try { _audio.SetAudioFocus(AudioFocusMode.Learning); } catch (Exception) { }
    Log("lesson start (" + reason + ")");
  }

  void Abort(string reason) {
    _engaged = false;
    _focusRun = false;
    _beat = -1;
    StudentStep = 0;
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

  void ResetStage() {
    try {
      if (_teacher != null && _teacher.Root != null) {
        _teacher.Root.transform.localPosition = TeacherStart;
        LessonMotion.FaceSnap(_teacher, BoardPoint - TeacherStart);
      }
      if (_student != null && _student.Root != null) {
        _student.Root.transform.localPosition = StudentStart;
        LessonMotion.FaceSnap(_student, StairsPoint - StudentStart);
      }
    } catch (Exception) { }
  }

  // ---- acting (the same script as the reference lesson, compact) -----------------

  void Update() { Step(Time.deltaTime); }

  public void Step(float dt) {
    if (!_built || dt <= 0f) return;
    try {
      Watch(dt);
      TickPulses(dt);
      if (_voice != null) _voice.Tick(dt);
      LessonMotion.Tick(_teacher, dt);
      LessonMotion.Tick(_student, dt);
      if (!_engaged || _beat < 0) return;
      _beatT += dt;
      switch (_beat) {
        case 0: // "Look at the board!"
          // User round: hold ~1.5s when the pass starts so the child settles
          // and looks at the plot before the lesson speaks.
          LessonMotion.FaceTowards(_teacher, BoardPoint, dt, 4f);
          if (_beatT >= 1.5f) {
            Say("Look at the board!", "Nhìn lên bảng nhé!");
            LessonMotion.PointAt(_teacher, BoardPoint, 2.0f);
            Next();
          }
          break;
        case 1: // "This is number three."
          LessonMotion.FaceTowards(_teacher, BoardPoint, dt, 4f);
          PulseDigit();
          if (_beatT >= 1.9f) {
            Say("This is number three.", "Đây là số ba.");
            LessonMotion.PointAt(_teacher, BoardPoint, 2.0f);
            Next();
          }
          break;
        case 2: // "Three."
          LessonMotion.FaceTowards(_teacher, BoardPoint, dt, 4f);
          if (_beatT >= 1.9f) {
            Say("Three.", "Ba.");
            Next();
          }
          break;
        case 3: // "Watch your friend!"
          LessonMotion.FaceTowards(_teacher, StairsPoint, dt, 4f);
          if (_beatT >= 1.5f) {
            Say("Watch your friend!", "Xem bạn làm nhé!");
            LessonMotion.PointAt(_teacher, StairsPoint, 2.0f);
            Next();
          }
          break;
        case 4: // the student walks to the stair foot
          LessonMotion.FaceTowards(_teacher, StairsPoint, dt, 3f);
          if (LessonMotion.WalkTo(_student, StudentBase, dt, WalkSpeed)) Next();
          break;
        default: // 5..: climb step (beat - 4)
          TickClimb(dt);
          break;
      }
    } catch (Exception) { }
  }

  void TickClimb(float dt) {
    int step = _beat - 4; // 1..3 walk up, 4 = confirm, 5 = tidy, 6 = settle
    if (step >= 1 && step <= DemoSteps) {
      LessonMotion.FaceTowards(_teacher, StairsPoint, dt, 3f);
      if (LessonMotion.WalkTo(_student, StepStand(step), dt, WalkSpeed)) {
        StudentStep = step;
        Count(step);
        Current = Phase.Beat;
        Next();
      }
      return;
    }
    if (step == DemoSteps + 1) { // confirm
      LessonMotion.FaceTowards(_student, PlayerLocal(), dt, 3f);
      if (_beatT >= 0.35f && !_saidYes) {
        _saidYes = true;
        Say("Yes! Three steps!", "Đúng rồi! Ba bậc!");
        LessonMotion.PointAt(_teacher, StepStand(DemoSteps) + new Vector3(0f, 0.25f, 0f), 2.0f);
        LessonMotion.Hop(_student);
        Sparkle(StepStand(DemoSteps) + new Vector3(0f, 0.2f, 0f), 8, 71, 0.35f);
      }
      if (_beatT >= 1.4f) { _saidYes = false; Next(); }
      return;
    }
    if (step == DemoSteps + 2) { // tidy: the student walks back
      if (LessonMotion.WalkTo(_student, StudentStart, dt, WalkSpeed)) Next();
      return;
    }
    // settle: hold the pose, count the pass, open the gate for the panel.
    if (_passDoneLatch) return;
    _passDoneLatch = true;
    PassDone = true;
    LoopCount++;
    _engaged = false;
    Current = Phase.Observing;
    StudentStep = DemoSteps;
    Log("loop " + LoopCount + " done (panel gate opened)");
  }

  bool _saidYes;

  Vector3 StepStand(int step) {
    return new Vector3(0f, step * StepRise, BaseZ + (step - 0.5f) * StepTread);
  }

  Vector3 PlayerLocal() {
    return _player != null && _root != null
      ? _root.InverseTransformPoint(_player.position)
      : StairsPoint;
  }

  void Count(int step) {
    PlaySfx("step");
    PulseStep(step);
    if (step == 1) Say("One.", "Một.");
    else if (step == 2) Say("Two.", "Hai.");
    else Say("Three.", "Ba.");
  }

  void Next() { _beat++; _beatT = 0f; }

  void Say(string en, string vi) {
    if (_voice == null) return;
    _voice.Speak(DialogueLang.T(en, vi));
  }

  void PlaySfx(string id) {
    if (_audio == null) return;
    try { _audio.PlaySfx(new SfxId(id)); } catch (Exception) { }
  }

  void Log(string m) {
    try { Debug.Log("[StairLessonDemo] " + m, this); } catch (Exception) { }
  }

  // ---- juice ----------------------------------------------------------------------

  float _stepPulse;
  GameObject _pulsingCue;
  float _digitPulse;

  void PulseStep(int step) {
    if (_stepCues == null || step < 1 || step > _stepCues.Length) return;
    _pulsingCue = _stepCues[step - 1];
    _stepPulse = 0.5f;
  }

  void PulseDigit() { _digitPulse = Mathf.Max(_digitPulse, 0.9f); }

  void TickJuice(float dt) { } // folded into Update via TickPulses below

  void TickPulses(float dt) {
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
    if (_digitPulse > 0f) {
      _digitPulse -= dt;
      if (_boardDigit != null) {
        float k = Mathf.Clamp01(_digitPulse / 0.9f);
        float s = 1f + 0.10f * Mathf.Sin(k * Mathf.PI);
        _boardDigit.transform.localScale = new Vector3(s, s, s);
      }
      if (_digitPulse <= 0f && _boardDigit != null) _boardDigit.transform.localScale = Vector3.one;
    }
  }

  void Sparkle(Vector3 localPos, int count, int seed, float radius) {
    if (_fx == null) return;
    try { DemoJuice.Sparkle(_fx, localPos, count, seed, radius); } catch (Exception) { }
  }

  // ---- stage primitives (diorama only) --------------------------------------------

  static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    go.transform.localScale = scale;
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    return go;
  }

  // Treads keep their colliders? NO: the garden plot is look-only scenery — the
  // whole diorama is collider-free so walk clicks fall through to the garden
  // ground (the plot mouth is where the child stands).
  static GameObject Solid(Transform parent, string name, Vector3 pos, Vector3 scale, Color color) {
    GameObject go = Box(parent, name, pos, scale, color);
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

  static void Strip(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (Exception) { }
  }

  static void NoShadows(GameObject go) {
    if (go == null) return;
    foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) {
      if (r == null) continue;
      r.receiveShadows = false;
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
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
