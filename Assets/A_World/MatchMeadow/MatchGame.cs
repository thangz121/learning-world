// A_World/MatchMeadow/MatchGame.cs — S3-P2Z17 GAMEPLAY #6 "GHÉP ĐÚNG CẶP".
// WORLD-BASED matching (never a memory-card UI): the teacher shows a REFERENCE
// object, the child walks the search field, finds the IDENTICAL object, picks
// it up (it rides the real fist), carries it to the PAIRING area and places it
// beside its reference — the two objects then form a real pair. A wrong object
// earns a gentle correction and walks itself home (never a fail). One meadow,
// 1..3 pairs: the round picks the family (balls/flowers/blocks) and colours.
// Demo and gameplay share the SAME components and calls (student really picks,
// carries and places a candidate; wrong never completes).
// Architecture: self-contained scene component, reusing LessonActors,
// PacedVoice, DemoJuice, SmartCamera beats, ActivityLifecycle (owned by the
// Math-side MatchArea) and MicroWorldPortal. C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

// One real object in the meadow: Available -> Picked -> Carried -> Placed.
// The pair slot is decided at place time by the IDENTITY (family+colour), never
// by click counts; a matched object can never be re-picked or matched twice.
[DisallowMultipleComponent]
public class MatchItem : MonoBehaviour, IClickTarget {
  public enum ItemState { Available, Picked, Carried, Placed, Removed }

  public ItemState State { get; private set; } = ItemState.Available;
  public int Family;
  public int ColorId;
  public bool IsReference;
  public Vector3 HomeLocal;
  public int SlotIndex { get; private set; } = -1;
  public MatchGame Game;
  public Action OnPlaced;

  Transform _hand;
  bool _picking, _placing, _returning, _flying;
  float _pickT, _pickDelay, _pickDur;
  Vector3 _pickFrom;
  float _placeT, _placeDelay, _placeDur;
  Vector3 _placeFrom, _placeTo;
  float _placeLift;
  Vector3 _flyFrom, _flyTo;
  float _flyT, _flyDur, _flyLift;
  float _bounceT = 1f;
  Vector3 _baseScale = Vector3.one;
  Collider _collider;

  public bool IsAvailable { get { return State == ItemState.Available; } }
  public bool IsFlying { get { return _flying; } }

  public void Bind(MatchGame game, Transform hand) {
    Game = game;
    _hand = hand;
    _baseScale = transform.localScale;
    _collider = GetComponent<Collider>();
  }

  public void SetHand(Transform hand) { _hand = hand; }

  public void OnClicked() {
    if (State != ItemState.Available || IsReference || Game == null) return;
    Game.TryPick(this);
  }

  public void BeginCarry(float delay = 0f) {
    if (State != ItemState.Available || IsReference) return;
    State = ItemState.Picked;
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

  public void BeginPlace(Vector3 slotLocal, float delay = 0f) {
    if (State != ItemState.Carried) return;
    _picking = false;
    _placing = true;
    _placeT = 0f;
    _placeDelay = Mathf.Max(0f, delay);
    _placeDur = 0.32f;
    _placeLift = 0.18f;
    _placeTo = slotLocal;
  }

  public void BeginReturnHome() {
    State = ItemState.Carried; // re-uses the flight while it travels
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

  public void ParkInSlot(int slotIndex, Vector3 slotLocal) {
    State = ItemState.Placed;
    SlotIndex = slotIndex;
    _flying = false;
    _picking = false;
    _placing = false;
    transform.localPosition = slotLocal;
    transform.localRotation = Quaternion.identity;
    gameObject.SetActive(true);
    if (_collider != null) _collider.enabled = false;
  }

  public void MarkRemoved() {
    State = ItemState.Removed;
    if (_collider != null) _collider.enabled = false;
  }

  public void ResetHome() {
    State = ItemState.Available;
    SlotIndex = -1;
    _flying = false;
    _picking = false;
    _placing = false;
    _returning = false;
    transform.localPosition = HomeLocal;
    transform.localRotation = Quaternion.identity;
    transform.localScale = _baseScale;
    gameObject.SetActive(true);
    if (_collider != null && !IsReference) _collider.enabled = true;
  }

  // Deterministic tick — owned by MatchGame.Tick (single owner).
  public void TickForTests(float dt) {
    try {
      if (_picking) { TickPick(dt); return; }
      if (_placing) { TickPlace(dt); return; }
      if (_flying) { TickFlight(dt); return; }
      if (State == ItemState.Carried && _hand != null) FollowHand(dt);
      TickBounce(dt);
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
      State = ItemState.Carried;
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
      _flying = true;
    }
    TickFlight(dt);
  }

  void FollowHand(float dt) {
    Vector3 want = _hand.position + Vector3.up * 0.02f;
    transform.position = Vector3.Lerp(transform.position, want, 1f - Mathf.Exp(-16f * dt));
    transform.rotation = Quaternion.Slerp(transform.rotation, _hand.rotation, 1f - Mathf.Exp(-10f * dt));
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
      State = ItemState.Available;
      SlotIndex = -1;
      if (_collider != null && !IsReference) _collider.enabled = true;
      return;
    }
    if (placed) {
      transform.localPosition = _placeTo;
      transform.localRotation = Quaternion.identity;
      State = ItemState.Placed;
      if (OnPlaced != null) {
        try { OnPlaced(); } catch (Exception) { }
      }
    }
  }

  void TickBounce(float dt) {
    if (_bounceT < 1f) {
      _bounceT = Mathf.Min(1f, _bounceT + dt / 0.3f);
      float s = 1f + 0.14f * Mathf.Sin(Mathf.PI * _bounceT);
      transform.localScale = _baseScale * s;
      if (_bounceT >= 1f) transform.localScale = _baseScale;
      return;
    }
    if (State == ItemState.Available && Game != null && Game.PlayerNear(transform.position, 2.4f)) {
      float s = 1f + 0.05f * Mathf.Sin(Time.time * 4f);
      transform.localScale = _baseScale * s;
    } else {
      transform.localScale = _baseScale;
    }
  }
}

// The pairing pad door: clicking it walks the child up and places; carrying an
// object close to it (and stopping) does the same — one call, state-guarded,
// spam-safe. Proximity is polled by MatchGame.Tick.
[DisallowMultipleComponent]
public class MatchPadZone : MonoBehaviour, IClickTarget {
  public MatchGame Game;
  public float placeRadius = 1.7f;

  public void Bind(MatchGame game) {
    Game = game;
    // Unconditional (deferred Destroy lesson): the click door must stay alive.
    // S3-P2Z32: a FLAT ground drop-zone. A tall box's near FACE sits ~1.1m in
    // front of the centre, so a click routes the child only ~0.5m and the agent
    // (stopping distance ~0.5m) never moves — the child stalls short of the pad
    // (journey finding). A ground-hugging disc routes the click to the pad
    // centre and the child walks right onto it.
    BoxCollider box = gameObject.GetComponent<BoxCollider>();
    if (box == null) box = gameObject.AddComponent<BoxCollider>();
    box.isTrigger = false;
    box.size = new Vector3(2.4f, 0.05f, 2.4f);
    box.center = new Vector3(0f, 0.025f, 0f);
  }

  public void OnClicked() {
    if (Game != null) Game.TryPlace();
  }
}

[DisallowMultipleComponent]
public class MatchGame : MonoBehaviour {
  public enum Phase {
    Wait,     // the child walks to the marked play spot; nothing is taught yet
    Intro,    // teacher shows the reference and gives the task
    Playing,  // the child matches the pairs
    Success,  // every pair is matched — celebrated, field stays open
  }

  const float SuccessDwell = 0.9f;
  // S3-P2Z19 (user round): the guidance waits for the area's arrival reveal,
  // then asks the child onto the marked spot; the question is read ONLY there.
  const float WaitCallSeconds = 2.4f;
  static readonly string[] NumEn = {
    "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
  static readonly string[] NumVi = {
    "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };

  static int ClampN(int i) { return Mathf.Clamp(i, 1, 9); }
  static string N(int i) { return NumEn[ClampN(i) - 1]; }
  static string Nvi(int i) { return NumVi[ClampN(i) - 1]; }
  static string Cap(string s) {
    return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
  }

  public Phase Current { get; private set; } = Phase.Wait;
  public int Pairs { get; private set; } = MatchMeadowBuilder.DefaultPairs;
  public int Family { get; private set; }
  public int MatchedPairs { get; private set; }
  public int WrongMatches { get; private set; }
  public MatchItem Carried { get; private set; }
  public bool IntroDone { get { return Current != Phase.Wait && Current != Phase.Intro; } }
  public bool ResultShown { get { return _result != null && _result.activeSelf; } }
  public bool RewardShown { get { return _builder != null && _builder.RewardBloom != null
      && _builder.RewardBloom.activeSelf; } }
  public int ItemCountTotal { get { return _items.Count; } }
  public MatchItem ItemAt(int i) { return i >= 0 && i < _items.Count ? _items[i] : null; }
  public MatchItem ReferenceAt(int i) { return i >= 0 && i < _references.Count ? _references[i] : null; }
  public int CandidateCount { get { return _candidates.Count; } }
  public MatchItem CandidateAt(int i) { return i >= 0 && i < _candidates.Count ? _candidates[i] : null; }
  public int RoundColorAt(int i) { return i >= 0 && i < _roundColors.Length ? _roundColors[i] : -1; }
  public int DistractorColor { get { return _distractor; } }
  public bool PlayerOnPad() { return _padAnchor != null && PlayerNear(_padAnchor.position, 1.7f); }

  MatchMeadowBuilder _builder;
  Transform _root, _player, _playerHand;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PlayerVisual _viz;
  ClickToMove _mover;
  Action<int> _onCompleted;

  LessonActor _teacher, _student;
  PacedVoice _voice;
  Transform _fx;

  GameObject _board, _result;
  Transform _padAnchor;
  Transform _camTeaching, _lookTeaching, _camDemo, _lookDemo, _camSuccess, _lookSuccess;

  readonly List<MatchItem> _items = new List<MatchItem>();
  readonly List<MatchItem> _references = new List<MatchItem>();
  readonly List<MatchItem> _candidates = new List<MatchItem>();
  int[] _roundColors = new int[0];
  int _distractor = MatchMeadowBuilder.ColorGreen;

  public float PickDelay = 0.45f;
  public float PlaceDelay = 0.4f;

  float _phaseT, _shotT = -1f;
  bool _shotIssued, _cameraDone, _followHanded;
  int _shot;
  bool _saidBoard, _saidTask;
  bool _waitCalled;      // the walk-in guidance was spoken
  float _ringPulseT;
  float _victoryT = -1f, _resultPopT = 1f, _boardPulseT;
  int _pulseRef = -1;
  float _pulseT;
  int _sparkleSeed = 2600;
  Vector3 _lastPos;

  // ---- build ---------------------------------------------------------------------

  public void Build(MatchMeadowBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life, int pairs,
      Action<int> onCompleted = null, Transform hand = null) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _onCompleted = onCompleted;
    Pairs = Mathf.Clamp(pairs <= 0 ? MatchMeadowBuilder.DefaultPairs : pairs,
      1, MatchMeadowBuilder.MaxPairs);
    _viz = player != null ? player.GetComponent<PlayerVisual>() : null;
    _mover = player != null ? player.GetComponent<ClickToMove>() : null;
    // The carried object rides the child's REAL fist bone (lesson from #4/#5).
    _playerHand = hand;
    if (_playerHand == null && _viz != null && _viz.HandBone != null) _playerHand = _viz.HandBone;
    if (_playerHand == null) _playerHand = player;
    if (_builder == null) {
      Debug.LogWarning("[MatchGame] no builder; activity parked.", this);
      return;
    }
    _root = _builder.transform;
    _board = _builder.NumberBoard;
    _result = _builder.Result;
    _padAnchor = _builder.PadAnchor;
    _camTeaching = _builder.CamTeaching;
    _lookTeaching = _builder.LookTeaching;
    _camDemo = _builder.CamDemo;
    _lookDemo = _builder.LookDemo;
    _camSuccess = _builder.CamSuccess;
    _lookSuccess = _builder.LookSuccess;

    BuildActors();
    GameObject fx = new GameObject("MMFx");
    fx.transform.SetParent(_root, false);
    _fx = fx.transform;

    // Wire the pool: every object is a real MatchItem; only candidates are
    // clickable (references are scenery).
    _items.Clear();
    List<GameObject> pool = _builder.Items;
    for (int i = 0; i < pool.Count; i++) {
      GameObject go = pool[i];
      if (go == null) continue;
      MatchItem item = go.GetComponent<MatchItem>();
      if (item == null) item = go.AddComponent<MatchItem>();
      item.Family = _builder.ItemFamily != null && i < _builder.ItemFamily.Length
        ? _builder.ItemFamily[i] : 0;
      item.ColorId = _builder.ItemColor != null && i < _builder.ItemColor.Length
        ? _builder.ItemColor[i] : 0;
      item.IsReference = _builder.ItemIsReference != null && i < _builder.ItemIsReference.Length
        && _builder.ItemIsReference[i];
      item.Bind(this, _playerHand);
      _items.Add(item);
    }
    if (_padAnchor != null) {
      MatchPadZone zone = _padAnchor.gameObject.GetComponent<MatchPadZone>();
      if (zone == null) zone = _padAnchor.gameObject.AddComponent<MatchPadZone>();
      zone.Bind(this);
    }
    ConfigureRound();

    // Re-entry policy FIRST: a completed round adopts the matched pairs.
    if (_life != null && _life.State == ActivityState.Completed) {
      ApplyCompletedState("adopt");
      return;
    }
    if (_life != null) {
      try {
        _life.MarkAvailable("match staged");
        _life.BeginEnter("match meadow built");
        _life.MarkReady("intro staged");
      } catch (Exception) { }
    }
    _lastPos = _player != null ? _player.position : Vector3.zero;
    try { Debug.Log("[MatchGame] staged (family " + Family + ", pairs " + Pairs + ", waiting on the play spot).", this); } catch (Exception) { }
  }

  // The round composition: family + colours + one distractor; references on
  // their pedestals, candidates spread over the search field (never crowded).
  void ConfigureRound() {
    Family = MatchMeadowBuilder.FamilyForPairs(Pairs);
    _roundColors = MatchMeadowBuilder.ColorsForPairs(Pairs);
    _distractor = MatchMeadowBuilder.DistractorForPairs(Pairs);
    _references.Clear();
    _candidates.Clear();

    int totalCand = _roundColors.Length + 1; // targets + one distractor
    int[] candSpots = totalCand <= 2 ? new[] { 2, 5 }
      : totalCand == 3 ? new[] { 1, 3, 5 }
      : new[] { 0, 2, 4, 5 };

    for (int i = 0; i < _items.Count; i++) {
      MatchItem item = _items[i];
      if (item == null) continue;
      if (item.IsReference) {
        int slot = IndexOfColor(_roundColors, item.ColorId);
        if (item.Family == Family && slot >= 0) {
          item.transform.localPosition = MatchMeadowBuilder.RefSlotLocal[slot]
            + new Vector3(0f, 0.30f, 0f);
          item.HomeLocal = item.transform.localPosition;
          item.transform.localScale = Vector3.one;
          item.gameObject.SetActive(true);
          item.ParkInSlot(slot, item.transform.localPosition);
          _references.Add(item);
        } else {
          item.MarkRemoved();
          item.gameObject.SetActive(false);
        }
      } else {
        item.gameObject.SetActive(false);
        item.MarkRemoved();
      }
    }
    // Candidates: the target colours first, then the distractor.
    for (int i = 0; i < _roundColors.Length; i++) {
      MatchItem item = FindPool(false, Family, _roundColors[i]);
      if (item == null) continue;
      ActivateCandidate(item, candSpots[Mathf.Min(i, candSpots.Length - 1)]);
    }
    MatchItem distractor = FindPool(false, Family, _distractor);
    if (distractor != null) {
      ActivateCandidate(distractor, candSpots[candSpots.Length - 1]);
    }
  }

  void ActivateCandidate(MatchItem item, int spotIndex) {
    item.gameObject.SetActive(true);
    item.ResetHome();
    Vector3 spot = MatchMeadowBuilder.CandidateSpotLocal[Mathf.Clamp(spotIndex, 0,
      MatchMeadowBuilder.CandidateSpotLocal.Length - 1)];
    item.transform.localPosition = spot;
    item.HomeLocal = spot;
    _candidates.Add(item);
  }

  static int IndexOfColor(int[] colors, int color) {
    for (int i = 0; i < colors.Length; i++) if (colors[i] == color) return i;
    return -1;
  }

  MatchItem FindPool(bool reference, int family, int color) {
    for (int i = 0; i < _items.Count; i++) {
      MatchItem item = _items[i];
      if (item == null) continue;
      if (item.IsReference != reference) continue;
      if (item.Family != family || item.ColorId != color) continue;
      return item;
    }
    return null;
  }

  void BuildActors() {
    _teacher = LessonActors.Build(_root, "MMTeacher", "NpcVisuals/TessVisual", 0.5f,
      MatchMeadowBuilder.TeacherStart, new Color(0.25f, 0.45f, 0.85f),
      new Color(0.98f, 0.78f, 0.25f));
    _student = LessonActors.Build(_root, "MMStudent", "NpcVisuals/MiloVisual", 0.42f,
      MatchMeadowBuilder.StudentStart, new Color(0.30f, 0.62f, 0.45f),
      new Color(0.55f, 0.35f, 0.20f));
    _voice = new PacedVoice();
    _voice.Audio = _audio;
    _voice.LogTag = "MatchGame";
    if (_teacher != null && _teacher.Root != null) FaceSnap(_teacher, BoardLocal());
    if (_student != null && _student.Root != null) FaceSnap(_student, TeacherLocal());
    if (_builder != null && _builder.Result != null) _builder.Result.SetActive(false);
    if (_builder != null && _builder.RewardBloom != null) _builder.RewardBloom.SetActive(false);
  }

  void ApplyCompletedState(string reason) {
    Current = Phase.Success;
    _followHanded = true;
    _cameraDone = true;
    MatchedPairs = Pairs;
    _victoryT = -1f;
    if (_teacher != null && _teacher.Root != null) {
      _teacher.Root.transform.localPosition = MatchMeadowBuilder.TeacherStart;
      FaceSnap(_teacher, PadLocal());
    }
    if (_student != null && _student.Root != null) {
      _student.Root.transform.localPosition = MatchMeadowBuilder.StudentReturn;
      FaceSnap(_student, PadLocal());
    }
    for (int i = 0; i < _references.Count; i++) {
      MatchItem cand = FindPool(false, Family, _references[i].ColorId);
      if (cand != null) {
        cand.gameObject.SetActive(true);
        cand.ParkInSlot(i, MatchMeadowBuilder.PairSlotLocal[i] + new Vector3(0f, 0.30f, 0f));
      }
    }
    if (_result != null) _result.SetActive(true);
    if (_builder != null && _builder.RewardBloom != null) _builder.RewardBloom.SetActive(true);
    if (_player != null) _lastPos = _player.position;
    try { Debug.Log("[MatchGame] adopted COMPLETED state (" + reason + ").", this); } catch (Exception) { }
  }

  // ---- tick ------------------------------------------------------------------------

  void Update() { Tick(Time.deltaTime); }

  public void Tick(float dt) {
    if (dt <= 0f || _builder == null) return;
    try {
      if (_voice != null) _voice.Tick(dt);
      for (int i = 0; i < _items.Count; i++) {
        if (_items[i] != null) _items[i].TickForTests(dt);
      }
      switch (Current) {
        case Phase.Wait: TickWait(dt); break;
        case Phase.Intro: TickIntro(dt); break;
        case Phase.Playing: TickPlaying(dt); break;
        case Phase.Success: TickSuccess(dt); break;
      }
      TickActing(dt);
      TickCamera(dt);
      TickJuice(dt);
    } catch (Exception) { }
  }

  void To(Phase next) { Current = next; _phaseT = 0f; }

  // ---- wait on the marked play spot (S3-P2Z19 user round) -----------------------
  // "Tất cả các game sau khi đã vào arena thì không phát lại demo nữa mà chỉ
  // chờ player bước đến đúng vị trí chơi thì bắt đầu đọc câu hỏi." The teacher
  // idles, the spot pulses, the guidance line asks the child over; the question
  // is read ONLY when the child actually stands on the spot.

  Vector3 PlaySpotWorld() {
    if (_builder != null && _builder.PlaySpot != null)
      return _builder.PlaySpot.transform.position;
    return _root != null ? _root.TransformPoint(MatchMeadowBuilder.PlaySpotLocal) : Vector3.zero;
  }

  bool PlayerOnSpot() {
    if (_player == null) return false;
    Vector3 p = _player.position;
    Vector3 q = PlaySpotWorld();
    float dx = p.x - q.x, dz = p.z - q.z;
    return dx * dx + dz * dz <= MatchMeadowBuilder.PlaySpotRadius * MatchMeadowBuilder.PlaySpotRadius;
  }

  void TickWait(float dt) {
    _phaseT += dt;
    FaceTowards(_teacher, PlayerLocal(), dt, 3f);
    FaceTowards(_student, PlayerLocal(), dt, 2.5f);
    PulsePlayRing(dt);
    // The "Come here!" sign hides once the child is close — never blocks the frame.
    if (_builder != null && _builder.PlaySign != null) {
      bool near = PlayerNear(PlaySpotWorld(), 5.5f);
      if (_builder.PlaySign.activeSelf == near) _builder.PlaySign.SetActive(!near);
    }
    if (!_waitCalled && _phaseT >= WaitCallSeconds) {
      _waitCalled = true;
      Wave(_teacher);
      Say("Step into the play spot!", "Hãy bước vào vị trí chơi nhé!");
      Point(_teacher, PlaySpotWorld(), 2.4f);
    }
    if (PlayerOnSpot()) StartQuestion();
  }

  void StartQuestion() {
    To(Phase.Intro);
    Log("question read (child on the play spot): " + Pairs + " pairs");
  }

  void PulsePlayRing(float dt) {
    if (_builder == null || _builder.PlayRing == null) return;
    _ringPulseT += dt;
    float s = 1f + 0.07f * Mathf.Sin(_ringPulseT * 3.4f);
    try { _builder.PlayRing.transform.localScale = new Vector3(2.5f * s, 0.01f, 2.5f * s); }
    catch (Exception) { }
  }

  // ---- intro -----------------------------------------------------------------------

  void TickIntro(float dt) {
    _phaseT += dt;
    float t = _phaseT;
    FaceTowards(_teacher, BoardLocal(), dt, 4f);
    FaceTowards(_student, TeacherLocal(), dt, 3f);
    if (!_saidBoard && t >= 0.5f) {
      _saidBoard = true;
      Wave(_teacher);
      Say("Look at the board!", "Nhìn lên bảng nhé!");
      Point(_teacher, BoardWorld(), 2.4f);
    }
    if (!_saidTask && t >= 2.2f) {
      _saidTask = true;
      FaceTowards(_teacher, RefWorld(0), dt, 4f);
      // S3-P2Z23 (user: "không nói đây là số mấy, trẻ tự nhận biết"): no count is
      // spoken — the board shows it; the teacher only names the job.
      Say("Find the same!", "Tìm vật giống mẫu nhé!");
      Point(_teacher, RefWorld(0), 2.6f);
    }
    if (t >= 3.6f) StartPlaying();
  }

  void StartPlaying() {
    To(Phase.Playing);
    Follow();
    _followHanded = true;
    if (_life != null) { try { _life.Begin("question read"); } catch (Exception) { } }
    _lastPos = _player != null ? _player.position : Vector3.zero;
    try { Debug.Log("[MatchGame] child control (matching).", this); } catch (Exception) { }
  }

  // (S3-P2Z19: the in-arena student demo + handoff beats are GONE — the garden
  // miniature teaches once; the arena reads the question on the play spot.)

  // ---- the child's matching ------------------------------------------------------------

  public void TryPick(MatchItem item) {
    if (item == null || !item.IsAvailable || item.IsReference) return;
    if (Current != Phase.Playing && Current != Phase.Success) return;
    if (Carried != null) return;
    Carried = item;
    item.SetHand(_playerHand);
    if (_viz != null) {
      _viz.FaceTowards(item.transform.position, true);
      _viz.PlayPickup();
    }
    PlaySfx("pickup");
    Sparkle(item.transform.position, 6, _sparkleSeed++, 0.3f);
    item.BeginCarry(PickDelay);
  }

  public void TryPlace() {
    if (Carried == null) return;
    if (Current != Phase.Playing && Current != Phase.Success) return;
    if (Carried.State != MatchItem.ItemState.Carried) return;
    MatchItem item = Carried;
    Carried = null;
    if (_viz != null) {
      _viz.FaceTowards(PadWorld(), true);
      _viz.PlayPickup();
    }
    int slot = MatchSlotFor(item);
    if (slot >= 0) {
      // CORRECT: the object flies to its pair slot; the praise + the pair count
      // land only when it REALLY lands (brief §20 — never "đúng" before placed).
      int landed = slot;
      item.OnPlaced = delegate { OnMatchLanded(landed); };
      item.BeginPlace(MatchMeadowBuilder.PairSlotLocal[slot] + new Vector3(0f, 0.30f, 0f), PlaceDelay);
    } else {
      // WRONG (gentle): the teacher corrects, the object walks itself home.
      WrongMatches++;
      PlaySfx("pickup");
      GameJuice.WrongFx(_board != null ? _board.transform : null, _fx, PadWorld());
      ActivityFeedback.Retry();
      Say("Not that one. Find the same!", "Chưa đúng rồi. Tìm lại nhé!");
      Point(_teacher, RefWorld(0), 2.2f);
      item.SetHand(_playerHand);
      item.BeginReturnHome();
      try { Debug.Log("[MatchGame] wrong match refused (" + WrongMatches + ").", this); } catch (Exception) { }
    }
  }

  // The pair really landed: count it, praise it, and complete when all pairs
  // are matched (the confirmation follows the physical placement).
  void OnMatchLanded(int slot) {
    MatchedPairs++;
    PlaySfx("give");
    PulseRef(slot);
    Sparkle(RefWorld(slot) + new Vector3(0f, 0.4f, 0f), 8, _sparkleSeed++, 0.4f);
    // S3-P2Z34: pair progress ("2/3 pairs found").
    ActivityFeedback.Progress(MatchedPairs, Pairs);
    Say(ConfirmEn(slot), ConfirmVi(slot));
    Point(_teacher, RefWorld(slot), 1.8f);
    if (MatchedPairs >= Pairs) SuccessBeats();
  }

  // The unmatched reference whose identity equals the object's (family+colour).
  int MatchSlotFor(MatchItem item) {
    for (int i = 0; i < _references.Count; i++) {
      MatchItem reference = _references[i];
      if (reference == null) continue;
      if (reference.ColorId != item.ColorId || reference.Family != item.Family) continue;
      bool alreadyMatched = false;
      for (int c = 0; c < _items.Count; c++) {
        MatchItem other = _items[c];
        if (other == null || other.IsReference) continue;
        if (other.SlotIndex == i && other.State == MatchItem.ItemState.Placed) { alreadyMatched = true; break; }
      }
      if (!alreadyMatched) return i;
    }
    return -1;
  }

  void SuccessBeats() {
    To(Phase.Success);
    PlaySfx("success");
    Sparkle(PadWorld() + new Vector3(0f, 0.8f, 0f), 16, _sparkleSeed++, 0.9f);
    // S3-P2Z33: layered win feedback (big cut — the whole meadow is won).
    GameJuice.CorrectFx(_fx, PadWorld() + new Vector3(0f, 0.8f, 0f), true);
    ActivityFeedback.Correct();
    Say("You found all the pairs!", "Con ghép đúng hết rồi!");
    Point(_teacher, PadWorld(), 2.2f);
    CelebrateBoth();
    if (_result != null) {
      _result.SetActive(true);
      _result.transform.localScale = Vector3.one * 0.65f;
      _resultPopT = 0f;
    }
    if (_builder != null && _builder.RewardBloom != null) _builder.RewardBloom.SetActive(true);
    SetShot(2);
    _victoryT = 1.35f;
    MarkLifeCompleted();
    if (_onCompleted != null) {
      try { _onCompleted(Pairs); } catch (Exception) { }
    }
  }

  void MarkLifeCompleted() {
    if (_life == null) return;
    try {
      if (_life.State != ActivityState.Completed) _life.MarkCompleted("matched " + Pairs + " pairs");
    } catch (Exception) { }
  }

  void TickPlaying(float dt) {
    _phaseT += dt;
    TickPlaceProximity();
    _lastPos = _player != null ? _player.position : Vector3.zero;
    FaceTowards(_teacher, PlayerLocal(), dt, 2.2f);
    FaceTowards(_student, PlayerLocal(), dt, 2.2f);
  }

  void TickSuccess(float dt) {
    _phaseT += dt;
    FaceTowards(_teacher, PlayerLocal(), dt, 2f);
    FaceTowards(_student, PlayerLocal(), dt, 2f);
    TickPlaceProximity(); // the field stays open (the distractor feeds the lesson)
    if (_phaseT >= 4.6f && !_followHanded) {
      _followHanded = true;
      _cameraDone = true;
      Follow();
    }
  }

  void TickPlaceProximity() {
    if (Carried == null || _player == null) return;
    if (_mover != null && _mover.IsMoving) return;
    if (PlayerNear(PadWorld(), 1.7f)) TryPlace();
  }

  // ---- camera ---------------------------------------------------------------------------

  void SetShot(int shot) {
    if (_shot == shot) { IssueShot(); return; }
    _shot = shot;
    IssueShot();
  }

  void IssueShot() {
    if (_cam == null) return;
    Transform c, l;
    if (_shot == 1) { c = _camDemo; l = _lookDemo; }
    else if (_shot == 2) { c = _camSuccess; l = _lookSuccess; }
    else { c = _camTeaching; l = _lookTeaching; }
    if (c == null || l == null) return;
    _shotIssued = true;
    _shotT = 2.8f;
    try { _cam.FrameAnchor(c, l, 2.8f); } catch (Exception) { }
  }

  void TickCamera(float dt) {
    if (_cameraDone) return;
    // After a correction sets _followHanded, stop re-issuing the success shot
    // (S3-P2Z17 journey finding: camera stuck in Interaction blocked the exit).
    if (_followHanded) return;
    if (!_shotIssued) {
      if (Current == Phase.Intro) IssueShot();
      return;
    }
    if (Current != Phase.Intro && Current != Phase.Success) return;
    _shotT -= dt;
    if (_shotT <= 0f) IssueShot();
  }

  void Follow() {
    if (_cam == null || _player == null) return;
    try { _cam.Follow(_player, MatchMeadowBuilder.FollowOffset); } catch (Exception) { }
    Log("camera returned to follow");
  }

  // ---- acting / juice ----------------------------------------------------------------------

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
    if (_pulseT > 0f) {
      _pulseT -= dt;
      if (_pulseRef >= 0 && _pulseRef < _references.Count && _references[_pulseRef] != null) {
        float k = Mathf.Clamp01(_pulseT / 0.5f);
        float s = 1f + 0.16f * Mathf.Sin(k * Mathf.PI);
        _references[_pulseRef].transform.localScale = Vector3.one * s;
      }
      if (_pulseT <= 0f) {
        if (_pulseRef >= 0 && _pulseRef < _references.Count && _references[_pulseRef] != null)
          _references[_pulseRef].transform.localScale = Vector3.one;
        _pulseRef = -1;
      }
    }
  }

  void PulseBoard(float seconds) { _boardPulseT = Mathf.Max(_boardPulseT, seconds); }
  void PulseRef(int slot) { _pulseRef = slot; _pulseT = 0.5f; }

  // ---- lines ---------------------------------------------------------------------------------

  string FamilyEn(int n) {
    if (Family == 0) return n == 1 ? "ball" : "balls";
    if (Family == 1) return n == 1 ? "flower" : "flowers";
    return n == 1 ? "block" : "blocks";
  }
  string FamilyVi(int n) {
    if (Family == 0) return "bóng";
    if (Family == 1) return "bông hoa";
    return "khối";
  }
  string ColorEn(int colorId) {
    switch (colorId) {
      case MatchMeadowBuilder.ColorRed: return "red";
      case MatchMeadowBuilder.ColorYellow: return "yellow";
      case MatchMeadowBuilder.ColorGreen: return "green";
      default: return "blue";
    }
  }
  string ColorVi(int colorId) {
    switch (colorId) {
      case MatchMeadowBuilder.ColorRed: return "đỏ";
      case MatchMeadowBuilder.ColorYellow: return "vàng";
      case MatchMeadowBuilder.ColorGreen: return "xanh lá";
      default: return "xanh dương";
    }
  }
  int RefColor(int slot) {
    if (slot >= 0 && slot < _references.Count && _references[slot] != null)
      return _references[slot].ColorId;
    return _roundColors.Length > 0 ? _roundColors[0] : 0;
  }
  string ConfirmEn(int slot) {
    return "Two " + ColorEn(RefColor(slot)) + " " + FamilyEn(2) + "!";
  }
  string ConfirmVi(int slot) {
    return "Hai " + FamilyVi(2) + " " + ColorVi(RefColor(slot)) + "!";
  }

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
    try { Debug.Log("[MatchGame] " + message, this); } catch (Exception) { }
  }

  // ---- world helpers ---------------------------------------------------------------------------

  public bool PlayerNear(Vector3 world, float radius) {
    if (_player == null) return false;
    Vector3 p = _player.position;
    float dx = p.x - world.x, dz = p.z - world.z;
    return dx * dx + dz * dz <= radius * radius;
  }

  bool IsPlayerMoving() {
    if (_mover != null) return _mover.IsMoving;
    if (_player == null) return false;
    Vector3 d = _player.position - _lastPos;
    d.y = 0f;
    return d.sqrMagnitude > 0.0004f;
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
      : (_root != null ? _root.TransformPoint(new Vector3(0f, 0f, 4.4f)) : Vector3.zero);
  }
  Vector3 PadLocal() { return LocalPoint(PadWorld()); }

  Vector3 RefWorld(int slot) {
    int i = Mathf.Clamp(slot, 0, MatchMeadowBuilder.MaxPairs - 1);
    if (_references.Count > i && _references[i] != null) return _references[i].transform.position;
    return _root != null ? _root.TransformPoint(MatchMeadowBuilder.RefSlotLocal[i]) : Vector3.zero;
  }
  Vector3 RefLocal(int slot) { return LocalPoint(RefWorld(slot)); }

  Vector3 PlayerLocal() {
    return _player != null ? LocalPoint(_player.position) : PadLocal();
  }
  Vector3 TeacherLocal() {
    return _teacher != null && _teacher.Root != null
      ? _teacher.Root.transform.localPosition + new Vector3(0f, 1.1f, 0f)
      : LocalPoint(MatchMeadowBuilder.TeacherStart);
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
