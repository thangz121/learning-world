// A_World/OrderingStation/OrderingStation.cs — Ga Thứ Tự.
// Starts at LV3. Observe -> rule -> arrange -> check. No timer, no game over.
// The WHOLE track is validated, never a single placement. C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class OrderingStation : MonoBehaviour {
  public enum Phase { Wait, Arrange, TapAnswer, Done }
  public enum TaskKind { FullOrder, Insert, TapPosition }

  public const int FirstLevel = 3;
  public const int LastLevel = 10;

  public Phase Current { get; private set; }
  public int Level { get; private set; }
  public int RoundIndex { get; private set; }
  public OrderDim Dim { get; private set; }
  public bool Descending { get; private set; }
  public TaskKind Task { get; private set; }
  public OrderingPiece Carried { get; private set; }
  public bool Completed { get; private set; }
  public int WrongCount { get; private set; }
  public bool LastCorrect { get; private set; }
  // LV7 tap questions: which slot answers (first/last/before/after target).
  public int TapTargetSlot { get; private set; }

  OrderingStationBuilder _builder;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PacedVoice _voice;
  Transform _fx;
  float _phaseT;
  bool _waitCalled;
  float _restT;
  bool _finishAfterRest;
  float _idleT;
  readonly Queue<string> _askEn = new Queue<string>();
  readonly Queue<string> _askVi = new Queue<string>();

  // Object Yard (south of the Ordering Track): pieces are collectable here.
  // USER ROUND 2026-10-01: the old slots (z 2.2..3.2) sat INSIDE the track
  // slots' colliders, so a click on a piece hit the slot instead. The yard is
  // now clearly separated from the track (z 0.4..1.6).
  static readonly Vector3[] PlazaSlots = {
    new Vector3(-2.6f, 0f, 0.5f), new Vector3(-1.3f, 0f, 0.5f),
    new Vector3(0f, 0f, 0.5f), new Vector3(1.3f, 0f, 0.5f),
    new Vector3(-1.95f, 0f, 1.6f), new Vector3(-0.65f, 0f, 1.6f),
    new Vector3(0.65f, 0f, 1.6f),
  };

  public void Build(OrderingStationBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _voice = new PacedVoice();
    _voice.Audio = audio;
    _voice.LogTag = "OrderingStation";
    _fx = builder != null ? builder.transform : transform;
    if (_life != null) {
      try {
        if (_life.State == ActivityState.Completed) { Adopt(); return; }
        _life.MarkAvailable("station offered");
      } catch (Exception) { }
    }
    Level = FirstLevel;
    RoundIndex = 0;
    Current = Phase.Wait;
    _phaseT = 0f;
    Follow();
  }

  void Update() { Tick(Time.deltaTime); }

  public void Tick(float dt) {
    if (dt <= 0f || _builder == null) {
      if (_voice != null) _voice.Tick(dt);
      return;
    }
    if (_voice != null) _voice.Tick(dt);
    TickAsk();
    if (Completed && Current == Phase.Done) return;
    if (_restT > 0f) {
      _restT -= dt;
      if (_restT <= 0f) AfterWin();
      return;
    }
    _phaseT += dt;
    if (Current == Phase.Wait) TickWait(dt);
    else if (Current == Phase.Arrange || Current == Phase.TapAnswer) {
      _idleT += dt;
      if (_idleT >= 8f && _idleT - dt < 8f) PointTask();
      else if (_idleT >= 15f && _idleT - dt < 15f) PointTask();
      if (Current == Phase.Arrange) TickPlay(dt);
    }
  }

  void TickWait(float dt) {
    if (!_waitCalled && _phaseT >= 1.2f) {
      _waitCalled = true;
      Say("Come to the station!", "Ra ga cùng con!");
      ActivityGuide.PointAt(SpotWorld());
    }
    if (PlayerNear(SpotWorld(), 1.5f) || _phaseT >= 8f) BeginRound();
  }

  Vector3 _lastPlayerPos;
  void TickPlay(float dt) {
    if (_player == null) { _lastPlayerPos = Vector3.zero; return; }
    Vector3 p = _player.position;
    float dx = p.x - _lastPlayerPos.x, dz = p.z - _lastPlayerPos.z;
    bool moving = dx * dx + dz * dz > 0.0004f;
    _lastPlayerPos = p;
    // USER ROUND 2026-10-01: only snap when the child has STOPPED at a slot —
    // walking past a slot on the way to another one must never drop the piece.
    if (moving) return;
    if (Carried != null) {
      OrderingSlot near = NearestSlot(0.55f);
      if (near != null) TryPlace(near);
    }
  }

  void TickAsk() {
    if (_askEn.Count <= 0 || _voice == null || !_voice.Idle || _voice.HasLine) return;
    Say(_askEn.Dequeue(), _askVi.Dequeue());
  }

  public void BeginRound() {
    if (Completed) return;
    Current = Phase.Arrange;
    _phaseT = 0f;
    Carried = null;
    TapTargetSlot = -1;
    _idleT = 0f;
    ApplyRound();
    ActivityFeedback.ProgressKeep(RoundIndex, RoundsOf(Level));
    foreach (OrderingSlot s in _builder.Slots)
      if (s != null) s.Game = this;
    foreach (OrderingPiece p in _builder.Pieces)
      if (p != null && p.Game == null) p.Game = this;
    if (_life != null && _life.State == ActivityState.Available) {
      try { _life.Begin("station round"); } catch (Exception) { }
    }
    SpeakTask();
    PointTask();
  }

  void ApplyRound() {
    _builder.ClearRound();
    if (Level == 3) ApplySize(3, false);
    else if (Level == 4) ApplyLength(4, RoundIndex % 2 == 1);
    else if (Level == 5) ApplyHeight(3, RoundIndex % 2 == 1);
    else if (Level == 6) ApplyInsert();
    else if (Level == 7) ApplyPosition();
    else if (Level == 8) ApplyMixed(5);
    else if (Level == 9) ApplyInsertFive();
    else ApplyMission();
  }

  // Spawn n pieces with ranks 1..n in a shuffled plaza order; colors shuffled
  // independently so color never cues rank.
  void SpawnSet(OrderDim dim, int n, int seed) {
    int[] order = new int[n];
    for (int i = 0; i < n; i++) order[i] = i + 1;
    for (int i = n - 1; i > 0; i--) {
      int j = (seed * 7 + i * 13) % (i + 1);
      int t = order[i]; order[i] = order[j]; order[j] = t;
    }
    for (int i = 0; i < n; i++) {
      int colorIdx = (seed + i * 2 + 1) % OrderingStationBuilder.Palette.Length;
      Vector3 home = PlazaSlots[i % PlazaSlots.Length];
      OrderingPiece p = _builder.SpawnPiece(home, order[i], dim, colorIdx, "p" + i, false);
      p.Bind(this, _builder.transform, order[i], dim, "p" + i, home, false);
    }
  }

  void ApplySize(int n, bool desc) {
    Dim = OrderDim.Size;
    Descending = desc;
    Task = TaskKind.FullOrder;
    _builder.BuildTrack(n);
    SpawnSet(OrderDim.Size, n, RoundIndex + 1);
  }

  void ApplyLength(int n, bool desc) {
    Dim = OrderDim.Length;
    Descending = desc;
    Task = TaskKind.FullOrder;
    _builder.BuildTrack(n);
    SpawnSet(OrderDim.Length, n, RoundIndex + 3);
  }

  void ApplyHeight(int n, bool desc) {
    Dim = OrderDim.Height;
    Descending = desc;
    Task = TaskKind.FullOrder;
    _builder.BuildTrack(n);
    SpawnSet(OrderDim.Height, n, RoundIndex + 5);
  }

  void ApplyMixed(int n) {
    Dim = (OrderDim)(RoundIndex % 3);
    Descending = RoundIndex % 2 == 1;
    Task = TaskKind.FullOrder;
    _builder.BuildTrack(n);
    SpawnSet(Dim, n, RoundIndex + 7);
  }

  // Insert: a correct partial sequence is locked in; the child places the rest.
  void ApplyInsert() {
    Dim = (OrderDim)(RoundIndex % 3);
    Descending = false;
    Task = TaskKind.Insert;
    int n = 4;
    _builder.BuildTrack(n);
    float[] ranks = { 1f, 2f, 3f, 4f };
    // Locked correct pieces leave exactly one gap (rotated by round).
    int gap = RoundIndex % n;
    for (int s = 0; s < n; s++) {
      if (s == gap) continue;
      Vector3 home = PlazaSlots[s % PlazaSlots.Length];
      OrderingPiece p = _builder.SpawnPiece(home, ranks[s], Dim, s, "pre" + s, true);
      p.Bind(this, _builder.transform, ranks[s], Dim, "pre" + s, home, true);
      _builder.PrePlace(s, p);
    }
    Vector3 missHome = PlazaSlots[gap % PlazaSlots.Length];
    OrderingPiece miss = _builder.SpawnPiece(missHome, ranks[gap], Dim, gap + 2, "miss", false);
    miss.Bind(this, _builder.transform, ranks[gap], Dim, "miss", missHome, false);
    _builder.HighlightEmpty();
  }

  void ApplyInsertFive() {
    Dim = (OrderDim)((RoundIndex + 1) % 3);
    Descending = RoundIndex % 2 == 1;
    Task = TaskKind.Insert;
    int n = 5;
    _builder.BuildTrack(n);
    // Locked correct pieces; the missing rank depends on direction.
    float[] asc = { 1f, 2f, 3f, 4f, 5f };
    int gap = (RoundIndex + 2) % n;
    for (int s = 0; s < n; s++) {
      if (s == gap) continue;
      float rank = Descending ? 5f - s : asc[s];
      Vector3 home = PlazaSlots[s % PlazaSlots.Length];
      OrderingPiece p = _builder.SpawnPiece(home, rank, Dim, s, "pre" + s, true);
      p.Bind(this, _builder.transform, rank, Dim, "pre" + s, home, true);
      _builder.PrePlace(s, p);
    }
    float missRank = Descending ? 5f - gap : asc[gap];
    Vector3 missHome = PlazaSlots[gap % PlazaSlots.Length];
    OrderingPiece miss = _builder.SpawnPiece(missHome, missRank, Dim, gap + 1, "miss", false);
    miss.Bind(this, _builder.transform, missRank, Dim, "miss", missHome, false);
    _builder.HighlightEmpty();
  }

  // LV7 positional language on a pre-built correct track.
  void ApplyPosition() {
    Dim = OrderDim.Size;
    Descending = false;
    Task = TaskKind.TapPosition;
    Current = Phase.TapAnswer;
    int n = 4;
    _builder.BuildTrack(n);
    float[] ranks = { 1f, 2f, 3f, 4f };
    for (int s = 0; s < n; s++) {
      Vector3 home = PlazaSlots[s % PlazaSlots.Length];
      OrderingPiece p = _builder.SpawnPiece(home, ranks[s], Dim, s, "pre" + s, true);
      p.Bind(this, _builder.transform, ranks[s], Dim, "pre" + s, home, true);
      _builder.PrePlace(s, p);
    }
    int q = RoundIndex % 4;
    if (q == 0) TapTargetSlot = 0;
    else if (q == 1) TapTargetSlot = n - 1;
    else if (q == 2) TapTargetSlot = 1; // before the third piece
    else TapTargetSlot = 2; // after the big piece
  }

  void ApplyMission() {
    Dim = OrderDim.Length;
    Descending = false;
    Task = TaskKind.FullOrder;
    _builder.BuildTrack(5);
    SpawnSet(OrderDim.Length, 5, 11);
  }

  void SpeakTask() {
    if (Level == 3) Ask("Order small to large.", "Xếp từ nhỏ đến lớn.");
    else if (Level == 4) {
      if (Descending) Ask("Order long to short.", "Xếp từ dài đến ngắn.");
      else Ask("Order short to long.", "Xếp từ ngắn đến dài.");
    } else if (Level == 5) {
      if (Descending) Ask("Order high to low.", "Xếp từ cao đến thấp.");
      else Ask("Order low to high.", "Xếp từ thấp đến cao.");
    } else if (Level == 6) Ask("Put it in place.", "Đặt vào đúng chỗ.");
    else if (Level == 7) {
      int q = RoundIndex % 4;
      if (q == 0) Ask("Who stands first?", "Con nào đứng đầu?");
      else if (q == 1) Ask("Who stands last?", "Con nào đứng cuối?");
      else if (q == 2) Ask("Which stands second?", "Con nào đứng thứ hai?");
      else Ask("Which stands third?", "Con nào đứng thứ ba?");
    } else if (Level == 8) SpeakOrder();
    else if (Level == 9) Ask("Which spot is empty?", "Chỗ nào còn trống?");
    else Ask("Order boxes onto the train.", "Xếp hộp lên tàu.");
  }

  void SpeakOrder() {
    if (Dim == OrderDim.Height) {
      if (Descending) Ask("Order high to low.", "Xếp cao xuống thấp.");
      else Ask("Order low to high.", "Xếp thấp lên cao.");
    } else if (Dim == OrderDim.Length) {
      if (Descending) Ask("Order long to short.", "Xếp dài đến ngắn.");
      else Ask("Order short to long.", "Xếp ngắn đến dài.");
    } else {
      if (Descending) Ask("Order large to small.", "Xếp lớn đến nhỏ.");
      else Ask("Order small to large.", "Xếp nhỏ đến lớn.");
    }
  }

  void PointTask() {
    if (_builder == null) return;
    if (Task == TaskKind.Insert) {
      OrderingSlot gap = FirstEmptySlot();
      if (gap != null) { ActivityGuide.PointAt(gap.transform.position); return; }
    }
    if (Task == TaskKind.TapPosition && TapTargetSlot >= 0
        && TapTargetSlot < _builder.Slots.Count && _builder.Slots[TapTargetSlot] != null) {
      ActivityGuide.PointAt(_builder.Slots[TapTargetSlot].transform.position);
      return;
    }
    if (Carried != null) {
      OrderingSlot open = FirstEmptySlot();
      if (open != null) { ActivityGuide.PointAt(open.transform.position); return; }
    }
    ActivityGuide.PointAt(_builder.transform.TransformPoint(OrderingStationBuilder.TrackCenter));
  }

  public void TrySelect(OrderingPiece piece) {
    if (piece == null || Completed) return;
    if (_restT > 0f) return;
    if (Current == Phase.Wait || Current == Phase.Done) return;
    if (Current == Phase.TapAnswer) {
      TryTapAnswer(piece);
      return;
    }
    if (piece.Locked) return;
    if (Carried != null) return;
    if (piece.State != OrderingPiece.PieceState.Idle
        && piece.State != OrderingPiece.PieceState.Placed) return;
    piece.BeginCarry(_player != null ? _player : transform);
    Carried = piece;
    _idleT = 0f;
    PlaySfx("pickup");
    GameJuice.PickFx(_fx, piece.transform.position, piece.transform);
    PointTask();
  }

  void TryTapAnswer(OrderingPiece piece) {
    OrderingSlot slot = SlotOf(piece);
    if (slot != null && slot.SlotIndex == TapTargetSlot) WinRound();
    else {
      Reject();
      Ask("Look at the track.", "Nhìn lên đường ray.");
    }
  }

  public void TryPlace(OrderingSlot slot) {
    if (slot == null || Carried == null || Completed) return;
    if (_restT > 0f) return;
    if (Current != Phase.Arrange) return;
    if (!_builder.Slots.Contains(slot)) return;
    OrderingPiece piece = Carried;
    slot.Accept(piece);
    Carried = null;
    _idleT = 0f;
    PlaySfx("place");
    GameJuice.PlaceFx(_fx, slot.transform.position, slot.transform);
    if (TrackFull()) CheckSequence();
  }

  public void DetachFromSlot(OrderingPiece piece) {
    for (int i = 0; i < _builder.Slots.Count; i++) {
      if (_builder.Slots[i] != null) _builder.Slots[i].Vacate(piece);
    }
  }

  bool TrackFull() {
    if (_builder.Slots.Count == 0) return false;
    for (int i = 0; i < _builder.Slots.Count; i++)
      if (_builder.Slots[i] == null || !_builder.Slots[i].Filled) return false;
    return true;
  }

  float[] SlotRanks() {
    float[] ranks = new float[_builder.Slots.Count];
    for (int i = 0; i < _builder.Slots.Count; i++)
      ranks[i] = _builder.Slots[i] != null && _builder.Slots[i].Occupant != null
        ? _builder.Slots[i].Occupant.Rank : 0f;
    return ranks;
  }

  OrderingSlot SlotOf(OrderingPiece piece) {
    for (int i = 0; i < _builder.Slots.Count; i++) {
      if (_builder.Slots[i] != null && _builder.Slots[i].Occupant == piece)
        return _builder.Slots[i];
    }
    return null;
  }

  void CheckSequence() {
    if (OrderLogic.IsOrdered(SlotRanks(), Descending)) {
      WinRound();
    } else {
      WrongCount++;
      LastCorrect = false;
      Say("Check the order.", "Xem lại thứ tự nhé.");
      Ask("Before or after?", "Trước hay sau nhỉ?");
      ActivityFeedback.Retry();
      GameJuice.WrongFx(_builder != null ? _builder.transform : transform, _fx,
        _player != null ? _player.position : Vector3.zero);
    }
  }

  void Reject() {
    WrongCount++;
    LastCorrect = false;
    Say("Not that one.", "Chưa phải đâu.");
    ActivityFeedback.Retry();
    GameJuice.WrongFx(_builder != null ? _builder.transform : transform, _fx,
      _player != null ? _player.position : Vector3.zero);
  }

  void WinRound() {
    LastCorrect = true;
    PlaySfx("success");
    GameJuice.CorrectFx(_fx, _player != null ? _player.position : Vector3.zero, false);
    ActivityFeedback.Correct();
    SayPraise();
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

  void SayPraise() {
    if (Task == TaskKind.TapPosition) Say("Right!", "Đúng rồi!");
    else if (Dim == OrderDim.Height) {
      if (Descending) Say("Right! High to low.", "Đúng! Cao đến thấp.");
      else Say("Right! Low to high.", "Đúng! Thấp đến cao.");
    } else if (Dim == OrderDim.Length) {
      if (Descending) Say("Right! Long to short.", "Đúng! Dài đến ngắn.");
      else Say("Right! Short to long.", "Đúng! Ngắn đến dài.");
    } else if (Descending) Say("Right! Big to small.", "Đúng rồi! Lớn đến nhỏ.");
    else Say("Right! Small to large.", "Đúng rồi! Nhỏ đến lớn.");
  }

  public static int RoundsOf(int level) {
    if (level == 3) return 3;
    if (level == 4) return 2;
    if (level == 5) return 2;
    if (level == 6) return 2;
    if (level == 7) return 4;
    if (level == 8) return 2;
    if (level == 9) return 2;
    if (level == 10) return 1;
    return 1;
  }

  void FinishVisit() {
    Completed = true;
    Current = Phase.Done;
    ActivityGuide.Clear();
    Say("Great! The order is right!", "Giỏi lắm! Xếp đúng rồi!");
    if (_life != null) {
      try { _life.MarkCompleted("station done"); } catch (Exception) { }
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
    try { _cam.Follow(_player, OrderingStationBuilder.FollowOffset); } catch (Exception) { }
  }

  bool PlayerNear(Vector3 world, float r) {
    if (_player == null) return true;
    Vector3 d = _player.position - world;
    d.y = 0f;
    return d.sqrMagnitude <= r * r;
  }

  Vector3 SpotWorld() {
    return _builder != null
      ? _builder.transform.TransformPoint(OrderingStationBuilder.PlaySpotLocal)
      : OrderingStationBuilder.PlaySpotLocal;
  }

  OrderingSlot NearestSlot(float r) {
    if (_player == null) return null;
    OrderingSlot best = null;
    float bestD = r * r;
    for (int i = 0; i < _builder.Slots.Count; i++) {
      OrderingSlot s = _builder.Slots[i];
      if (s == null) continue;
      Vector3 d = _player.position - s.transform.position;
      d.y = 0f;
      float m = d.sqrMagnitude;
      if (m <= bestD) { bestD = m; best = s; }
    }
    return best;
  }

  OrderingSlot FirstEmptySlot() {
    if (_builder == null) return null;
    for (int i = 0; i < _builder.Slots.Count; i++) {
      OrderingSlot s = _builder.Slots[i];
      if (s != null && !s.Filled) return s;
    }
    return null;
  }
}
