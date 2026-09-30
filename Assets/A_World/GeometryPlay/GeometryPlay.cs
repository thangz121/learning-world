// A_World/GeometryPlay/GeometryPlay.cs — Lắp Hình Vui Nhộn. Starts at LV3.
// Actions change by level. No timer, no game over. C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GeometryPlay : MonoBehaviour {
  public enum Phase { Wait, Hunt, Carry, Done }
  public enum ActionKind { PlacePad, TapEnv, Assemble, Memory, Spatial, FindBehind }

  public const int FirstLevel = 3;
  public const int LastLevel = 10;

  public Phase Current { get; private set; }
  public int Level { get; private set; }
  public int RoundIndex { get; private set; }
  public GeometryKind Target { get; private set; }
  public ActionKind Action { get; private set; }
  public GeometryPiece Carried { get; private set; }
  public bool Completed { get; private set; }
  public int WrongCount { get; private set; }
  public bool LastWrongKind { get; private set; }
  public bool LastOrientationFix { get; private set; }
  public int MemoryStep { get; private set; }
  public GeometryKind[] Need { get; private set; }
  public string PropertyId { get; private set; }

  GeometryPlayBuilder _builder;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PacedVoice _voice;
  Transform _fx;
  float _phaseT;
  bool _waitCalled;
  bool _asked;
  float _restT;
  bool _finishAfterRest;
  float _idleT;
  Vector3[] _huntSlots;
  readonly List<GeometrySocket> _liveSockets = new List<GeometrySocket>();
  readonly Queue<string> _askEn = new Queue<string>();
  readonly Queue<string> _askVi = new Queue<string>();

  static readonly Vector3[] HuntSlots = {
    new Vector3(-1.6f, 0.12f, 2.1f), new Vector3(1.6f, 0.12f, 2.1f),
    new Vector3(-1.4f, 0.12f, 3.5f), new Vector3(1.4f, 0.12f, 3.5f),
  };

  public void Build(GeometryPlayBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _voice = new PacedVoice();
    _voice.Audio = audio;
    _voice.LogTag = "GeometryPlay";
    _fx = builder != null ? builder.transform : transform;
    BindPieces();
    if (_life != null) {
      try {
        if (_life.State == ActivityState.Completed) { Adopt(); return; }
        _life.MarkAvailable("geometry offered");
      } catch (Exception) { }
    }
    Level = FirstLevel;
    RoundIndex = 0;
    Current = Phase.Wait;
    _phaseT = 0f;
    Follow();
  }

  void BindPieces() {
    if (_builder == null) return;
    GeometryPiece[] env = { _builder.EnvWheel, _builder.EnvWindow, _builder.EnvRoof, _builder.EnvDoor };
    for (int i = 0; i < env.Length; i++) {
      if (env[i] == null) continue;
      env[i].Bind(this, _builder.transform, env[i].Kind, env[i].transform.localPosition,
        env[i].transform.eulerAngles.y);
    }
    for (int i = 0; i < _builder.Sockets.Count; i++) {
      if (_builder.Sockets[i] != null) _builder.Sockets[i].Game = this;
    }
  }

  void Update() { Tick(Time.deltaTime); }

  public void Tick(float dt) {
    if (dt <= 0f || _builder == null || Completed && Current == Phase.Done) {
      if (_voice != null) _voice.Tick(dt);
      return;
    }
    if (_voice != null) _voice.Tick(dt);
    TickAsk();
    if (_restT > 0f) {
      _restT -= dt;
      if (_restT <= 0f) AfterWin();
      return;
    }
    _phaseT += dt;
    if (Current == Phase.Wait) TickWait(dt);
    else if (Current == Phase.Hunt || Current == Phase.Carry) {
      _idleT += dt;
      if (_idleT >= 8f && _idleT - dt < 8f) PointTask();
      else if (_idleT >= 15f && _idleT - dt < 15f) PointTask();
      TickPlay(dt);
    }
  }

  void TickWait(float dt) {
    if (!_waitCalled && _phaseT >= 1.2f) {
      _waitCalled = true;
      Say("Come here first!", "Con tới đây trước nhé!");
      ActivityGuide.PointAt(SpotWorld());
    }
    if (PlayerNear(SpotWorld(), 1.5f) || _phaseT >= 8f) BeginRound();
  }

  void TickPlay(float dt) {
    if (Carried != null && Action != ActionKind.TapEnv && Action != ActionKind.FindBehind) {
      GeometrySocket near = NearestOpenSocket(1.05f);
      if (near != null) TryPlace(near);
    }
  }

  void TickAsk() {
    if (_askEn.Count <= 0 || _voice == null || !_voice.Idle || _voice.HasLine) return;
    Say(_askEn.Dequeue(), _askVi.Dequeue());
  }

  public void BeginRound() {
    if (Completed) return;
    Current = Phase.Hunt;
    _phaseT = 0f;
    _asked = false;
    Carried = null;
    MemoryStep = 0;
    LastWrongKind = false;
    LastOrientationFix = false;
    _idleT = 0f;
    ApplyRound();
    ActivityFeedback.ProgressKeep(RoundIndex, RoundsOf(Level));
    if (_life != null && _life.State == ActivityState.Available) {
      try { _life.Begin("geometry round"); } catch (Exception) { }
    }
    SpeakTask();
    PointTask();
  }

  void ApplyRound() {
    Action = ActionFor(Level);
    _builder.ClearHuntPieces();
    _builder.HideAllSockets();
    _builder.SetEnvClickable(false);
    _liveSockets.Clear();
    Need = null;
    PropertyId = "";
    if (Level == 3) ApplyRecognize();
    else if (Level == 4) ApplyDistinguish();
    else if (Level == 5) ApplyProperty();
    else if (Level == 6) ApplyWorld();
    else if (Level == 7) ApplyAssemble();
    else if (Level == 8) ApplyMemory();
    else if (Level == 9) ApplySpatial();
    else ApplyConstruct();
  }

  public static ActionKind ActionFor(int level) {
    if (level <= 5) return ActionKind.PlacePad;
    if (level == 6) return ActionKind.TapEnv;
    if (level == 7 || level == 10) return ActionKind.Assemble;
    if (level == 8) return ActionKind.Memory;
    if (level == 9) return ActionKind.Spatial;
    return ActionKind.PlacePad;
  }

  void ApplyRecognize() {
    GeometryKind[] cycle = {
      GeometryKind.Triangle, GeometryKind.Circle, GeometryKind.Square,
      GeometryKind.Rectangle, GeometryKind.Triangle, GeometryKind.Square
    };
    Target = cycle[RoundIndex % cycle.Length];
    SpawnFour(false, false, 0);
    ShowPad(Target);
  }

  void ApplyDistinguish() {
    GeometryKind[] cycle = {
      GeometryKind.Square, GeometryKind.Triangle, GeometryKind.Rectangle,
      GeometryKind.Circle, GeometryKind.Square, GeometryKind.Triangle
    };
    Target = cycle[RoundIndex % cycle.Length];
    SpawnFour(true, true, RoundIndex + 2);
    ShowPad(Target);
  }

  void ApplyProperty() {
    string[] ids = { "corners3", "nocorners", "equal4", "longshort" };
    PropertyId = ids[RoundIndex % ids.Length];
    Target = GeometryShapes.FromProperty(PropertyId);
    SpawnFour(true, true, 5);
    ShowPad(Target);
  }

  void ApplyWorld() {
    GeometryKind[] cycle = {
      GeometryKind.Circle, GeometryKind.Square, GeometryKind.Triangle, GeometryKind.Rectangle
    };
    Target = cycle[RoundIndex % cycle.Length];
    _builder.SetEnvClickable(true);
  }

  void ApplyAssemble() {
    bool house = (RoundIndex % 2) == 0;
    if (house) {
      Need = new GeometryKind[] { GeometryKind.Square, GeometryKind.Triangle };
      SpawnKinds(Need, true, 1);
      LiveSocket(0, GeometryPlayBuilder.BuildLocal, GeometryKind.Square, false);
      LiveSocket(1, GeometryPlayBuilder.BuildLocal + new Vector3(0f, 0.95f, 0f), GeometryKind.Triangle, true);
    } else {
      Need = new GeometryKind[] { GeometryKind.Rectangle, GeometryKind.Circle, GeometryKind.Circle };
      SpawnKinds(Need, true, 3);
      LiveSocket(0, GeometryPlayBuilder.BuildLocal, GeometryKind.Rectangle, false);
      LiveSocket(1, GeometryPlayBuilder.BuildLocal + new Vector3(-0.7f, 0f, -0.75f), GeometryKind.Circle, false);
      LiveSocket(2, GeometryPlayBuilder.BuildLocal + new Vector3(0.7f, 0f, -0.75f), GeometryKind.Circle, false);
    }
    Target = Need[0];
  }

  void ApplyMemory() {
    Need = new GeometryKind[] { GeometryKind.Square, GeometryKind.Circle, GeometryKind.Triangle };
    Target = Need[0];
    SpawnKinds(Need, true, 2);
    LiveSocket(0, GeometryPlayBuilder.WorkshopLocal + new Vector3(-1.0f, 0f, 0f), GeometryKind.Square, false);
    LiveSocket(1, GeometryPlayBuilder.WorkshopLocal, GeometryKind.Circle, false);
    LiveSocket(2, GeometryPlayBuilder.WorkshopLocal + new Vector3(1.0f, 0f, 0f), GeometryKind.Triangle, false);
  }

  void ApplySpatial() {
    int k = RoundIndex % 3;
    if (k == 0) {
      Action = ActionKind.Spatial;
      Target = GeometryKind.Triangle;
      Need = new GeometryKind[] { GeometryKind.Square, GeometryKind.Triangle };
      SpawnKinds(Need, true, 4);
      LiveSocket(0, GeometryPlayBuilder.WorkshopLocal, GeometryKind.Square, false);
      LiveSocket(1, GeometryPlayBuilder.WorkshopLocal + new Vector3(0f, 0.95f, 0f), GeometryKind.Triangle, true);
    } else if (k == 1) {
      Action = ActionKind.FindBehind;
      Target = GeometryKind.Circle;
      Vector3 behind = _builder.Crate.localPosition + new Vector3(0f, 0.12f, 0.95f);
      Vector3[] pos = {
        behind,
        new Vector3(-1.5f, 0.12f, 2.0f),
        new Vector3(0.2f, 0.12f, 2.0f),
        new Vector3(1.6f, 0.12f, 2.2f)
      };
      GeometryKind[] kinds = {
        GeometryKind.Circle, GeometryKind.Square, GeometryKind.Triangle, GeometryKind.Rectangle
      };
      for (int i = 0; i < 4; i++)
        SpawnOne(kinds[i], pos[i], GeometryShapes.ColorAt(i + 1), 1f, i * 20f);
    } else {
      Action = ActionKind.Spatial;
      Target = GeometryKind.Circle;
      Need = new GeometryKind[] { GeometryKind.Square, GeometryKind.Circle };
      SpawnKinds(Need, true, 6);
      LiveSocket(0, GeometryPlayBuilder.WorkshopLocal, GeometryKind.Square, false);
      LiveSocket(1, GeometryPlayBuilder.WorkshopLocal + new Vector3(-1.2f, 0f, 0f), GeometryKind.Circle, false);
    }
  }

  void ApplyConstruct() {
    ApplyAssemble();
    Action = ActionKind.Assemble;
  }

  void SpawnFour(bool varySize, bool varyYaw, int seed) {
    GeometryKind[] kinds = GeometryShapes.All;
    for (int i = 0; i < 4; i++) {
      float scale = varySize ? (0.75f + ((seed + i) % 3) * 0.18f) : 1f;
      float yaw = varyYaw ? ((seed * 37 + i * 51) % 160) : 0f;
      Color col = GeometryShapes.ColorAt(seed + i + (int)kinds[i]);
      SpawnOne(kinds[i], HuntSlots[i], col, scale, yaw);
    }
  }

  void SpawnKinds(GeometryKind[] kinds, bool vary, int seed) {
    for (int i = 0; i < kinds.Length; i++) {
      Vector3 p = HuntSlots[i % HuntSlots.Length];
      if (i >= HuntSlots.Length) p += new Vector3(0f, 0f, 0.4f);
      float scale = vary ? (0.85f + (i % 2) * 0.15f) : 1f;
      float yaw = vary ? (i * 28f) : 0f;
      SpawnOne(kinds[i], p, GeometryShapes.ColorAt(seed + i + 1), scale, yaw);
    }
  }

  void SpawnOne(GeometryKind kind, Vector3 local, Color color, float scale, float yaw) {
    GeometryPiece p = _builder.SpawnHuntPiece(kind, local, color, scale, yaw);
    p.Bind(this, _builder.transform, kind, local, yaw);
  }

  void ShowPad(GeometryKind kind) {
    LiveSocket(0, GeometryPlayBuilder.WorkshopLocal, kind, false);
  }

  void LiveSocket(int index, Vector3 local, GeometryKind kind, bool upright) {
    GeometrySocket s = _builder.ShowSocket(index, local, kind, upright);
    if (s != null) {
      s.Game = this;
      _liveSockets.Add(s);
    }
  }

  void SpeakTask() {
    if (Level == 5) SpeakProperty();
    else if (Level == 6) {
      Ask("Find a " + GeometryShapes.NameEn(Target) + " thing.",
        "Tìm vật hình " + GeometryShapes.NameVi(Target) + ".");
    } else if (Level == 7 || Level == 10) {
      bool house = Need != null && Need.Length == 2;
      Ask(house ? "Build a house!" : "Build a car!",
        house ? "Lắp một ngôi nhà!" : "Lắp một xe hơi!");
    } else if (Level == 8) {
      Ask("Square, then circle.", "Vuông, rồi tròn.");
      Ask("Then the triangle.", "Rồi tam giác.");
    } else if (Level == 9) {
      int k = RoundIndex % 3;
      if (k == 0) Ask("Triangle on the square.", "Tam giác lên hình vuông.");
      else if (k == 1) Ask("Find the shape behind.", "Tìm hình phía sau hộp.");
      else Ask("Circle on the left.", "Hình tròn bên trái.");
    } else {
      Ask("Find the " + GeometryShapes.NameEn(Target) + ".",
        "Tìm hình " + GeometryShapes.NameVi(Target) + ".");
    }
  }

  void SpeakProperty() {
    if (PropertyId == "corners3")
      Ask("Find the three-corner shape.", "Tìm hình có ba góc.");
    else if (PropertyId == "nocorners")
      Ask("Find the shape with no corners.", "Tìm hình không có góc.");
    else if (PropertyId == "equal4")
      Ask("Find four equal sides.", "Tìm hình bốn cạnh đều.");
    else
      Ask("Find long short sides.", "Tìm cạnh dài ngắn.");
  }

  void PointTask() {
    if (Action == ActionKind.TapEnv) {
      GeometryPiece env = _builder.EnvOf(Target);
      if (env != null) ActivityGuide.PointAt(env.transform.position);
    } else if (Action == ActionKind.FindBehind && _builder.Crate != null) {
      ActivityGuide.PointAt(_builder.Crate.position);
    } else if (Carried != null) {
      GeometrySocket open = FirstOpenMatching();
      if (open != null) ActivityGuide.PointAt(open.transform.position);
    } else {
      GeometryPiece hunt = HuntOf(Target);
      if (hunt != null) ActivityGuide.PointAt(hunt.transform.position);
      else ActivityGuide.PointAt(_builder.transform.TransformPoint(GeometryPlayBuilder.HuntCenter));
    }
  }

  public void TrySelect(GeometryPiece piece) {
    if (piece == null || Current == Phase.Wait || Current == Phase.Done) return;
    if (_restT > 0f) return;
    if (Action == ActionKind.TapEnv || Action == ActionKind.FindBehind) {
      TryTap(piece);
      return;
    }
    if (Carried != null) return;
    if (piece.State != GeometryPiece.PieceState.Idle) return;
    if (Carried != null && Carried != piece) return;
    if (Action == ActionKind.Memory) {
      if (Need == null || MemoryStep >= Need.Length || piece.Kind != Need[MemoryStep]) {
        Reject("Not this shape.", "Chưa phải hình này.");
        return;
      }
    }
    if (!PlayerNear(piece.transform.position, 1.8f) && _player != null) {
      // ClickRouter walks the child in; tests warp. Allow if close enough in tests
      // by warping callers. Live play arrives then clicks again / pending fires.
    }
    Transform holder = _player != null ? _player : transform;
    piece.BeginCarry(holder);
    Carried = piece;
    Current = Phase.Carry;
    _idleT = 0f;
    PlaySfx("pickup");
    GameJuice.PickFx(_fx, piece.transform.position, piece.transform);
    if (_liveSockets.Count > 0 && _liveSockets[0] != null)
      ActivityGuide.PointAt(_liveSockets[0].transform.position);
  }

  public void TryTap(GeometryPiece piece) {
    if (piece == null) return;
    if (_restT > 0f) return;
    _idleT = 0f;
    if (piece.Kind == Target) {
      WinRound();
      return;
    }
    Reject("Not this shape.", "Chưa phải hình này.");
    Ask("You want a " + GeometryShapes.NameEn(Target) + ".",
      "Con đang tìm hình " + GeometryShapes.NameVi(Target) + ".");
  }

  public void TryPlace(GeometrySocket socket) {
    if (socket == null || Carried == null) return;
    if (_restT > 0f) return;
    if (!socket.gameObject.activeInHierarchy) return;
    GeometryPiece piece = Carried;
    if (piece.Kind != socket.Kind) {
      LastWrongKind = true;
      Reject("Not this shape.", "Chưa phải hình này.");
      return;
    }
    if (socket.NeedsUpright && !GeometryShapes.OrientationOk(piece.Kind, piece.Yaw, socket.Yaw)) {
      LastOrientationFix = true;
      piece.transform.rotation = Quaternion.Euler(0f, socket.Yaw, 0f);
      Say("Turn it this way!", "Xoay hình như này!");
    }
    socket.Accept(piece);
    Carried = null;
    _idleT = 0f;
    PlaySfx("place");
    GameJuice.PlaceFx(_fx, socket.transform.position, socket.transform);
    if (Action == ActionKind.Memory) {
      MemoryStep++;
      if (Need != null && MemoryStep < Need.Length) Target = Need[MemoryStep];
    }
    if (AllSocketsFilled()) WinRound();
    else if (_liveSockets.Count > MemoryStep && MemoryStep < _liveSockets.Count)
      ActivityGuide.PointAt(_liveSockets[Mathf.Min(MemoryStep, _liveSockets.Count - 1)].transform.position);
  }

  bool AllSocketsFilled() {
    if (_liveSockets.Count == 0) return false;
    for (int i = 0; i < _liveSockets.Count; i++)
      if (_liveSockets[i] == null || !_liveSockets[i].Filled) return false;
    return true;
  }

  void Reject(string en, string vi) {
    WrongCount++;
    LastWrongKind = true;
    Say(en, vi);
    ActivityFeedback.Retry();
    GameJuice.WrongFx(_builder != null ? _builder.transform : transform, _fx,
      _player != null ? _player.position : Vector3.zero);
  }

  void WinRound() {
    PlaySfx("success");
    GameJuice.CorrectFx(_fx, _player != null ? _player.position : Vector3.zero, false);
    ActivityFeedback.Correct();
    Say("That's a " + GeometryShapes.NameEn(Target) + "!",
      "Đúng rồi! Hình " + GeometryShapes.NameVi(Target) + "!");
    if (Carried != null) { Carried.ReturnHome(); Carried = null; }
    int max = RoundsOf(Level);
    RoundIndex++;
    ActivityFeedback.ProgressKeep(RoundIndex, max);
    bool finish = false;
    if (RoundIndex >= max) {
      Level++;
      RoundIndex = 0;
      finish = Level > LastLevel;
      if (!finish) Say("Well done!", "Giỏi lắm!");
    }
    RestThenAdvance(finish);
  }

  void RestThenAdvance(bool finish) {
    _finishAfterRest = finish;
    _restT = Application.isPlaying ? 1.6f : 0f;
    if (_restT <= 0f) AfterWin();
  }

  void AfterWin() {
    _restT = 0f;
    if (_finishAfterRest) FinishVisit();
    else BeginRound();
  }

  public static int RoundsOf(int level) {
    if (level == 3 || level == 4) return 6;
    if (level == 5 || level == 6) return 4;
    if (level == 7 || level == 8 || level == 10) return 2;
    if (level == 9) return 3;
    return 1;
  }

  void FinishVisit() {
    Completed = true;
    Current = Phase.Done;
    ActivityGuide.Clear();
    Say("Well done!", "Giỏi lắm!");
    if (_life != null) {
      try { _life.MarkCompleted("shape workshop done"); } catch (Exception) { }
    }
  }

  void Adopt() {
    Completed = true;
    Current = Phase.Done;
    Level = LastLevel;
  }

  public void SetLevelForTests(int level, int round) {
    Level = Mathf.Clamp(level, FirstLevel, LastLevel);
    RoundIndex = Mathf.Max(0, round);
    BeginRound();
  }

  void Ask(string en, string vi) { _askEn.Enqueue(en); _askVi.Enqueue(vi); }

  void Say(string en, string vi) {
    if (_voice == null) return;
    _voice.Speak(DialogueLang.T(en, vi));
  }

  void PlaySfx(string id) {
    if (_audio == null) return;
    try { _audio.PlaySfx(new SfxId(id)); } catch (Exception) { }
  }

  void Follow() {
    if (_cam == null || _player == null) return;
    try { _cam.Follow(_player, GeometryPlayBuilder.FollowOffset); } catch (Exception) { }
  }

  bool PlayerNear(Vector3 world, float r) {
    if (_player == null) return true;
    Vector3 d = _player.position - world;
    d.y = 0f;
    return d.sqrMagnitude <= r * r;
  }

  Vector3 SpotWorld() {
    return _builder != null
      ? _builder.transform.TransformPoint(GeometryPlayBuilder.PlaySpotLocal)
      : GeometryPlayBuilder.PlaySpotLocal;
  }

  GeometrySocket NearestOpenSocket(float r) {
    if (_player == null) return null;
    GeometrySocket best = null;
    float bestD = r * r;
    for (int i = 0; i < _liveSockets.Count; i++) {
      GeometrySocket s = _liveSockets[i];
      if (s == null || !s.gameObject.activeInHierarchy || s.Filled) continue;
      if (Carried != null && s.Kind != Carried.Kind) continue;
      Vector3 d = _player.position - s.transform.position;
      d.y = 0f;
      float m = d.sqrMagnitude;
      if (m <= bestD) { bestD = m; best = s; }
    }
    return best;
  }

  GeometrySocket FirstOpenMatching() {
    for (int i = 0; i < _liveSockets.Count; i++) {
      GeometrySocket s = _liveSockets[i];
      if (s == null || !s.gameObject.activeInHierarchy || s.Filled) continue;
      if (Carried != null && s.Kind != Carried.Kind) continue;
      return s;
    }
    return null;
  }

  GeometryPiece HuntOf(GeometryKind kind) {
    if (_builder == null) return null;
    for (int i = 0; i < _builder.Pieces.Count; i++) {
      GeometryPiece p = _builder.Pieces[i];
      if (p != null && !p.EnvRole && p.Kind == kind
          && p.State == GeometryPiece.PieceState.Idle) return p;
    }
    return null;
  }
}
