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
public class RabbitCarrot : MonoBehaviour, IClickTarget, IDragTarget {
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
  bool _feeding, _feedActive, _feedHovered;
  float _feedT, _feedDelay, _feedLiftDur, _feedDropDur;
  Vector3 _feedFrom, _feedTo;
  float _feedLift;

  public bool IsAvailable { get { return State == CarrotState.Available; } }
  public bool IsFlying { get { return _flying; } }

  // S3-P2Z23 (user: "củ cà rốt phải để từ trên xuống, dứt điểm"): the Kenney
  // carrot's pivot is NOT its base, so placing the root at the bowl height sank
  // the body under the rim. Measure the prop's real bottom and rest it ON the
  // surface.
  public float SitOffset { get; private set; }

  public void Bind(RabbitFeed game, Transform hand) {
    Game = game;
    _hand = hand;
    _baseScale = transform.localScale;
    _collider = GetComponent<Collider>();
    SitOffset = ComputeSitOffset(transform);
  }

  static float ComputeSitOffset(Transform root) {
    if (root == null) return 0f;
    Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
    float min = float.MaxValue;
    for (int i = 0; i < rs.Length; i++) {
      if (rs[i] == null) continue;
      float y = rs[i].bounds.min.y;
      if (y < min) min = y;
    }
    if (min == float.MaxValue) return 0f;
    return root.position.y - min;
  }

  public void SetHand(Transform hand) { _hand = hand; }

  public void OnClicked() {
    if (State != CarrotState.Available || Game == null) return;
    Game.TryPick(this);
  }

  // S3-P2Z26 (user: "kéo lại carrot từ trên bục về lại vườn"): a fed carrot on
  // the bowl is draggable while the child is in control. The scene's
  // RabbitBowlDrag moves it; ClickRouter only asks CanDragNow so the press is
  // never ALSO routed as a walk/click.
  public bool CanDragNow {
    get {
      return State == CarrotState.Consumed && Game != null
        && Game.Current == RabbitFeed.Phase.Feeding && Game.PlayerAtBowl;
    }
  }
  public bool Dragging { get; private set; }

  // S3-P2Z30: the two-tap removal — tap a bowl carrot (selected, pops) then tap
  // the garden to send it home. Cleared on any state change.
  public bool Selected { get; set; }

  public void DragBegin() { Dragging = true; }

  public void DragMoveTo(Vector3 world) {
    if (!Dragging) return;
    transform.position = world;
  }

  public void DragEnd() { Dragging = false; }

  // Pickup: the child bends (player PickUp clip); once the hand is down, the
  // carrot arcs up into it and rides the fist. No ground->hand snap.
  public void BeginCarry(float delay = 0f) {
    if (State != CarrotState.Available) return;
    State = CarrotState.Picked;
    Selected = false;
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
  // rabbit, then LIFTS clear of the bowl and DROPS straight down onto the slot.
  // S3-P2Z24 (user: "carot bay xuyên qua bục từ dưới lên"): the old single arc
  // rose from the hand through the bowl geometry. Two beats now: rise to a
  // point ABOVE the bowl (0.7m clear), then a vertical drop — it always lands
  // from above, never tunnels up through the pedestal.
  public void BeginFeed(Vector3 mouthLocal, float delay = 0f) {
    if (State != CarrotState.Carried) return;
    State = CarrotState.Delivered;
    _picking = false;
    _feeding = true;
    _feedActive = false;
    _feedHovered = false;
    _feedT = 0f;
    _feedDelay = Mathf.Max(0f, delay);
    _feedLiftDur = 0.34f; // hand -> hover point above the bowl
    _feedDropDur = 0.22f; // hover -> straight down onto the slot
    _feedLift = 0.7f;
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
    Selected = false;
    transform.localPosition = slotLocal;
    transform.localRotation = Quaternion.identity; // stand upright in the bowl
    gameObject.SetActive(true);
  }

  public void MarkRemoved() {
    State = CarrotState.Removed;
    Selected = false;
    if (_collider != null) _collider.enabled = false;
  }

  public void ResetHome() {
    State = CarrotState.Available;
    _flying = false;
    _picking = false;
    _feeding = false;
    _feedActive = false;
    _returning = false;
    Selected = false;
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
      _feedT = 0f;
    }
    Vector3 hover = _feedTo + new Vector3(0f, _feedLift, 0f);
    if (!_feedHovered) {
      // Beat 1: rise from the fist to a point clearly ABOVE the bowl.
      float t = Mathf.Clamp01(_feedT / _feedLiftDur);
      Vector3 mid = (_feedFrom + hover) * 0.5f + new Vector3(0f, 0.18f, 0f);
      transform.localPosition = Vector3.Lerp(
        Vector3.Lerp(_feedFrom, mid, t), Vector3.Lerp(mid, hover, t), t);
      if (t < 1f) return;
      _feedHovered = true;
      _feedT = 0f;
    }
    // Beat 2: fall straight down onto the slot (accelerating), never through it.
    float d = Mathf.Clamp01(_feedT / _feedDropDur);
    transform.localPosition = Vector3.Lerp(hover, _feedTo, d * d);
    if (d < 1f) return;
    _feeding = false;
    _feedActive = false;
    _bounceT = 0f;
    // S3-P2Z19 (user: "không nhìn thấy số lượng đã lấy"): the fed carrot STAYS
    // in the bowl — the bowl is the visible counter. The munch beat fires on
    // arrival, after the drop settles.
    State = CarrotState.Consumed;
    transform.localPosition = _feedTo;
    // S3-P2Z22 (user: "vẫn bị ngược carrot"): the carrot rode the animated
    // hand, so its rotation was the fist's — snap it upright on landing.
    transform.localRotation = Quaternion.identity;
    gameObject.SetActive(true);
    if (OnMunched != null) {
      try { OnMunched(); } catch (Exception) { }
    }
  }

  void FollowHand(float dt) {
    // S3-P2Z19 (user: "carrot ở dưới háng"): hold it in FRONT of the fist and
    // a bit up. The old bare-fist position read at crotch height from behind
    // (the model faces -root.forward — same convention as the NPC carry).
    Vector3 fwd = _hand.root != null ? -_hand.root.forward : Vector3.forward;
    fwd.y = 0f;
    if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
    Vector3 want = _hand.position + fwd.normalized * 0.18f + Vector3.up * 0.14f;
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

  // Generic flight, used ONLY by the correction trip home now (the feed has its
  // own lift-then-drop path in TickFeed — S3-P2Z24).
  void TickFlight(float dt) {
    _flyT += dt;
    float t = Mathf.Clamp01(_flyT / _flyDur);
    Vector3 mid = (_flyFrom + _flyTo) * 0.5f + new Vector3(0f, _flyLift, 0f);
    transform.localPosition = Vector3.Lerp(
      Vector3.Lerp(_flyFrom, mid, t), Vector3.Lerp(mid, _flyTo, t), t);
    if (t < 1f) return;
    _flying = false;
    _bounceT = 0f;
    if (_returning) {
      _returning = false;
      State = CarrotState.Available;
      if (_collider != null) _collider.enabled = true;
    }
  }

  void TickBounceAndPulse(float dt) {
    if (_bounceT < 1f) {
      _bounceT = Mathf.Min(1f, _bounceT + dt / 0.3f);
      float s = 1f + 0.16f * Mathf.Sin(Mathf.PI * _bounceT);
      transform.localScale = _baseScale * s;
      return;
    }
    if (State == CarrotState.Consumed) {
      // S3-P2Z30: a selected bowl carrot pops so the child sees which one will
      // go back to the garden on the next garden tap.
      if (Selected) {
        float s = 1.24f + 0.08f * Mathf.Sin(Time.time * 6f);
        transform.localScale = _baseScale * s;
      } else {
        transform.localScale = _baseScale;
      }
      return;
    }
    if (State == CarrotState.Available && Game != null && Game.PlayerNear(transform.position, 2.4f)) {
      float s = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
      transform.localScale = _baseScale * s;
    } else {
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

// S3-P2Z23: the submit bell's click hook — ringing it turns the bowl's count in
// (right or wrong). The game owns the state; this is the door only.
[DisallowMultipleComponent]
public class RabbitSubmitZone : MonoBehaviour, IClickTarget {
  public RabbitFeed Game;

  public void Bind(RabbitFeed game) {
    Game = game;
    BoxCollider box = gameObject.AddComponent<BoxCollider>();
    // A slim ground-to-bell post: big enough to click from afar (routes the
    // child over) but never a wall across the patch sightline.
    box.size = new Vector3(0.8f, 1.2f, 0.8f);
    box.center = new Vector3(0f, 0.6f, 0f);
  }

  public void OnClicked() {
    if (Game != null) Game.TrySubmit();
  }
}

[DisallowMultipleComponent]
public class RabbitFeed : MonoBehaviour {
  public enum Phase {
    Wait,    // the child walks to the marked play spot; nothing is taught yet
    Intro,   // the teacher names the job there (the board shows the number)
    Feeding, // the child picks, carries and feeds; the teacher counts along
    Wrong,   // the child submitted a wrong count: gentle line, bowl empties, retry
    Success, // exact count: celebrated, then bowl clears -> walk back -> next number
  }

  // S3-P2Z26 (user: random numbers + +/- within 10): each round is a plain
  // number, an addition (a+b) or a subtraction (a-b). The board stages the
  // expression; the bowl starts prefilled with the first operand for +/-.
  public enum RoundKind { Plain = 0, Add = 1, Sub = 2 }

  // Beat timings (one place; deterministic for Tick tests).
  // S3-P2Z23: how long the "wrong, count again" beat holds before the bowl
  // empties and the child retries.
  const float WrongHoldSeconds = 2.6f;
  // S3-P2Z25 (user): after ANY submit, hold a beat then clear the bowl. On a
  // correct submit the child then walks back to the play spot, the "next
  // question" line plays and the board number swaps to the next target.
  const float SuccessHoldSeconds = 1.7f;
  const float NextQuestionWalkTimeout = 4.5f;
  const float DigitOutSeconds = 0.30f;
  const float DigitInSeconds = 0.50f;
  // S3-P2Z19 (user: "khi vào arena ... đọc hướng dẫn 'Hãy bước vào vị trí chơi
  // nhé'"): the guidance waits for the area's arrival reveal (2.2s), then asks
  // the child onto the marked spot; the question is read ONLY on arrival.
  const float WaitCallSeconds = 2.4f;
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
    "Nine carrots.", "Ten carrots." };
  static readonly string[] CountVi = {
    "Một củ cà rốt.", "Hai củ cà rốt.", "Ba củ cà rốt.", "Bốn củ cà rốt.",
    "Năm củ cà rốt.", "Sáu củ cà rốt.", "Bảy củ cà rốt.", "Tám củ cà rốt.",
    "Chín củ cà rốt.", "Mười củ cà rốt." };

  static int ClampN(int i) { return Mathf.Clamp(i, 1, RabbitPlayBuilder.MaxTarget); }
  static string N(int i) { return NumEn[ClampN(i) - 1]; }
  static string Nvi(int i) { return NumVi[ClampN(i) - 1]; }
  static string Carrots(int n) { return ClampN(n) == 1 ? "carrot" : "carrots"; }
  static string Cap(string s) {
    return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
  }

  public Phase Current { get; private set; } = Phase.Wait;
  public int Target { get; private set; } = RabbitPlayBuilder.Target;
  // S3-P2Z26: the round expression. Kind Plain -> Target=OpA (B ignored);
  // Add -> Target=OpA+OpB; Sub -> Target=OpA-OpB. ArithmeticEnabled is turned on
  // by GameInstaller in live play (tests keep the deterministic plain ladder).
  public RoundKind Kind { get; private set; } = RoundKind.Plain;
  public int OpA { get; private set; } = RabbitPlayBuilder.Target;
  public int OpB { get; private set; }
  public bool ArithmeticEnabled;
  public int Count { get; private set; }
  public RabbitCarrot Carried { get; private set; }
  // Kept for the recorded telemetry/tests: with the submit mechanic there is no
  // auto-overshoot correction, so this stays 0 (a wrong count is a wrong submit).
  public int Overshoots { get; private set; }
  public int UndershootNudges { get; private set; }
  // S3-P2Z23: how many times the child rang the bell.
  public int Submits { get; private set; }
  public bool IntroDone { get { return Current != Phase.Wait && Current != Phase.Intro; } }
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

  bool _saidBoard, _saidTask;
  bool _waitCalled;      // the walk-in guidance was spoken
  float _ringPulseT;
  float _victoryT = -1f;
  float _resultPopT = 1f;
  float _boardPulseT;
  Vector3 _lastPos;

  // S3-P2Z25: correct-submit transition + the re-ask line queue.
  bool _adopted;
  bool _cleared;
  bool _advance;
  bool _quickIntro;
  float _returnT;
  int _digitPhase;   // 0 idle, 1 old number out, 2 new number in
  float _digitT;
  GameObject _boardOld;
  GameObject _boardNew;
  readonly Queue<string> _askEn = new Queue<string>();
  readonly Queue<string> _askVi = new Queue<string>();
  // Pending bowl prefill (fills as the previous round's carrots land).
  int _prefillWant;
  // Wrong-submit beat (timed, deterministic; no coroutines so tests can tick).
  float _wrongT;

  // Rabbit life: nibble bursts + ear twitches + breathing (procedural, small).
  float _nibbleT;
  float _earTwitchT = 3f;
  float _twitchT;
  float _breatheT;
  int _sparkleSeed = 900;

  public int PipCount { get; private set; }

  public void Build(RabbitPlayBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life, int target = 0, Transform hand = null) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    // S3-P2Z29 (user: "lúc nào vào cũng là số 3"): live play opens on a RANDOM
    // question (plain/add/sub) instead of the area's cold-start 3. Re-entry on a
    // completed lifecycle still adopts the finished picture for its target.
    bool adopting = false;
    try { adopting = _life != null && _life.State == ActivityState.Completed; }
    catch (Exception) { }
    if (!adopting && ArithmeticEnabled) SetRoundRandom();
    else SetRound(RoundKind.Plain, target <= 0 ? RabbitPlayBuilder.Target : target, 0);
    _viz = player != null ? player.GetComponent<PlayerVisual>() : null;
    _mover = player != null ? player.GetComponent<ClickToMove>() : null;
    // S3-P2Z19 bug: GameInstaller resolved the player's HandBone but never
    // passed it, so the carried carrot rode the ROOT (crotch height). The hand
    // bone now comes in; the root stays the fallback.
    _playerHand = hand != null ? hand : player;
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
    // Render the first question (plain target built by the builder).
    RefreshQuestionBoard();

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
      sc.radius = 0.5f;
      sc.center = new Vector3(0f, 0.2f, 0f);
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
    // The submit bell's click door (S3-P2Z23).
    if (_builder.SubmitAnchor != null) {
      RabbitSubmitZone sub = _builder.SubmitAnchor.gameObject.GetComponent<RabbitSubmitZone>();
      if (sub == null) sub = _builder.SubmitAnchor.gameObject.AddComponent<RabbitSubmitZone>();
      sub.Bind(this);
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
    try { Debug.Log("[RabbitFeed] activity staged (waiting on the play spot).", this); } catch (Exception) { }
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
    // Re-entry shows the finished picture and stays there; the live correct
    // transition (walk back -> next number) only runs inside a play session.
    _adopted = true;
    _cleared = true;
    _followHanded = true;
    _cameraDone = true;
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
      if (i < Target) { c.ParkInBowl(BowlSlotFor(c, i)); _fed.Add(c); }
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
      TickAsk();
      TickPrefill();
      for (int i = 0; i < _carrots.Count; i++) {
        if (_carrots[i] != null) _carrots[i].TickForTests(dt);
      }
      switch (Current) {
        case Phase.Wait: TickWait(dt); break;
        case Phase.Intro: TickIntro(dt); break;
        case Phase.Feeding: TickFeeding(dt); break;
        case Phase.Success: TickSuccess(dt); break;
        case Phase.Wrong: TickWrong(dt); break;
      }
      TickActing(dt);
      TickCamera(dt);
      TickJuice(dt);
      TickRabbit(dt);
    } catch (Exception) { }
  }

  void TickVoice(float dt) { if (_voice != null) _voice.Tick(dt); }
  void To(Phase next) { Current = next; _phaseT = 0f; }

  // Paced multi-line speech (the SafetyFilter caps each NPC line at 6 words, so
  // a longer question is queued as short lines and spoken one at a time — same
  // discipline as the stair recap).
  void Ask(string en, string vi) {
    _askEn.Enqueue(en);
    _askVi.Enqueue(vi);
  }

  void TickAsk() {
    if (_askEn.Count <= 0 || _voice == null || !_voice.Idle || _voice.HasLine) return;
    Say(_askEn.Dequeue(), _askVi.Dequeue());
  }

  // ---- round expression + bowl prefill (S3-P2Z26) -------------------------------

  void SetRound(RoundKind kind, int a, int b) {
    Kind = kind;
    OpA = RabbitPlayBuilder.ClampTarget(a);
    if (kind == RoundKind.Plain) { OpB = 0; Target = OpA; return; }
    OpB = RabbitPlayBuilder.ClampTarget(b);
    if (kind == RoundKind.Sub) {
      if (OpB >= OpA) OpB = Mathf.Max(1, OpA - 1);
      Target = RabbitPlayBuilder.ClampTarget(OpA - OpB);
    } else {
      Target = RabbitPlayBuilder.ClampTarget(OpA + OpB);
    }
  }

  // Random round for the next question: plain / addition (sum <= 9) /
  // subtraction (result >= 1), never the same result twice in a row.
  void SetRoundRandom() {
    int prev = Target;
    RoundKind kind = RoundKind.Plain;
    int a = prev, b = 0, result = prev;
    for (int guard = 0; guard < 40; guard++) {
      int roll = UnityEngine.Random.Range(0, 3);
      if (roll == 0) {
        kind = RoundKind.Plain;
        a = UnityEngine.Random.Range(1, RabbitPlayBuilder.MaxTarget + 1);
        b = 0; result = a;
      } else if (roll == 1) {
        kind = RoundKind.Add;
        a = UnityEngine.Random.Range(1, RabbitPlayBuilder.MaxTarget);
        b = UnityEngine.Random.Range(1, RabbitPlayBuilder.MaxTarget + 1 - a);
        result = a + b;
      } else {
        kind = RoundKind.Sub;
        a = UnityEngine.Random.Range(2, RabbitPlayBuilder.MaxTarget + 1);
        b = UnityEngine.Random.Range(1, a);
        result = a - b;
      }
      if (result != prev || guard >= 39) break;
    }
    SetRound(kind, a, b);
  }

  // Rebuild the board question + result digit for the current round.
  void RefreshQuestionBoard() {
    if (_builder == null) return;
    _board = _builder.SpawnQuestion((int)Kind, OpA, OpB);
    _builder.SpawnResultDigit(Target);
    _boardPulseT = 0f;
    // S3-P2Z36: the persistent task line.
    if (Kind == RoundKind.Plain) {
      ActivityFeedback.Objective(DialogueLang.T(
        "Feed " + N(Target) + " carrots.", "Cho thỏ ăn " + Nvi(Target) + " củ."));
    } else if (Kind == RoundKind.Add) {
      ActivityFeedback.Objective(DialogueLang.T(
        "Add " + N(OpB) + " more.", "Thêm " + Nvi(OpB) + " củ nữa."));
    } else {
      ActivityFeedback.Objective(DialogueLang.T(
        "Take " + N(OpB) + " away.", "Bỏ " + Nvi(OpB) + " củ về vườn."));
    }
  }

  // Test seam: pin a specific round (deterministic; live generates randomly).
  public void SetRoundForTests(int kind, int a, int b) {
    SetRound((RoundKind)Mathf.Clamp(kind, 0, 2), a, b);
    RefreshQuestionBoard();
  }

  // Test seam: force the NEXT round picked by the correct-submit transition
  // (kind < 0 = the normal live/test path).
  public void ForceNextRoundForTests(int kind, int a, int b) {
    ForcedNextKind = kind;
    ForcedNextA = a;
    ForcedNextB = b;
  }
  int ForcedNextKind = -1;
  int ForcedNextA, ForcedNextB;

  // Place the first operand in the bowl for a+b / a-b. S3-P2Z31 (journey
  // evidence: "prefill want=8 placed=2 availBefore=2"): the previous round's
  // carrots may still be FLYING home, so the prefill is a PENDING target that
  // keeps filling as they land (TickPrefill every frame) instead of placing a
  // short count once.
  void PrefillBowl(int n) {
    _prefillWant = Mathf.Max(0, n);
    TickPrefill();
  }

  void TickPrefill() {
    if (_prefillWant <= 0) return;
    for (int i = 0; i < _carrots.Count && _prefillWant > 0; i++) {
      RabbitCarrot c = _carrots[i];
      if (c == null || c.State != RabbitCarrot.CarrotState.Available) continue;
      c.SetHand(_playerHand);
      c.ParkInBowl(BowlSlotFor(c, _fed.Count));
      _fed.Add(c);
      _prefillWant--;
    }
    Count = _fed.Count;
    SetPip(Count);
    if (_prefillWant <= 0) Log("prefill done bowl=" + Count);
    else Log("prefill waiting: need " + _prefillWant + " more (bowl=" + Count + ")");
  }

  // The drag drop: a fed carrot dragged back to the garden leaves the bowl and
  // flies home (Available again) so the count drops.
  public void ReturnFromBowl(RabbitCarrot c) {
    if (c == null || Current != Phase.Feeding) return;
    if (c.State != RabbitCarrot.CarrotState.Consumed) return;
    if (!_fed.Remove(c)) return;
    Count = Mathf.Max(0, Count - 1);
    SetPip(Count);
    c.Selected = false;
    c.SetHand(_playerHand);
    c.BeginReturnHome();
    RepackBowl();
    PlaySfx("pickup");
    Log("carrot back to the garden; bowl count=" + Count);
  }

  // A drag released outside the garden: the carrot returns to its bowl slot.
  public void SnapCarrotToBowl(RabbitCarrot c) { RepackBowl(); }

  void RepackBowl() {
    for (int i = 0; i < _fed.Count; i++) {
      RabbitCarrot c = _fed[i];
      if (c == null) continue;
      c.ParkInBowl(BowlSlotFor(c, i));
    }
  }

  // The garden drop zone the drag tests against (the carrot patch).
  public bool IsOverGarden(Vector3 world) {
    Vector3 p = PatchWorld();
    float dx = world.x - p.x, dz = world.z - p.z;
    return dx * dx + dz * dz <= 3.0f * 3.0f;
  }

  // ---- wait on the marked play spot (S3-P2Z19 user round) -----------------------
  // "Tất cả các game sau khi đã vào arena thì không phát lại demo nữa mà chỉ
  // chờ player bước đến đúng vị trí chơi thì bắt đầu đọc câu hỏi." The teacher
  // idles, the spot pulses, the guidance line asks the child over; the question
  // is read ONLY when the child actually stands on the spot.

  Vector3 PlaySpotWorld() {
    if (_builder != null && _builder.PlaySpot != null)
      return _builder.PlaySpot.transform.position;
    return _root != null ? _root.TransformPoint(RabbitPlayBuilder.PlaySpotLocal) : Vector3.zero;
  }

  bool PlayerOnSpot() {
    if (_player == null) return false;
    Vector3 p = _player.position;
    Vector3 q = PlaySpotWorld();
    float dx = p.x - q.x, dz = p.z - q.z;
    return dx * dx + dz * dz <= RabbitPlayBuilder.PlaySpotRadius * RabbitPlayBuilder.PlaySpotRadius;
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
    _quickIntro = false;
    _saidBoard = false;
    _saidTask = false;
    To(Phase.Intro);
    SetShot(0);
    // S3-P2Z26: for a+b / a-b the bowl starts with the FIRST operand (user:
    // "4-2 thì bục có sẵn 4 củ, trẻ bỏ bớt 2"; "3+2 thì có sẵn 3, trẻ thêm 2").
    if (Kind != RoundKind.Plain) PrefillBowl(OpA);
    Log("question read (child on the play spot): kind=" + Kind + " a=" + OpA
      + " b=" + OpB + " target=" + Target);
  }

  void PulsePlayRing(float dt) {
    if (_builder == null || _builder.PlayRing == null) return;
    _ringPulseT += dt;
    float s = 1f + 0.07f * Mathf.Sin(_ringPulseT * 3.4f);
    try { _builder.PlayRing.transform.localScale = new Vector3(2.5f * s, 0.01f, 2.5f * s); }
    catch (Exception) { }
  }

  // ---- teacher question (no demo in the arena — the garden teaches once) --------
  // The board holds the round's target: every line below is composed from it,
  // so the SAME script asks 1..9 without a second lesson.

  void TickIntro(float dt) {
    _phaseT += dt;
    float t = _phaseT;
    // S3-P2Z28 (user: "chưa thấy audio hướng dẫn"): a NEXT-round question runs the
    // same intro, just shorter — the board line is skipped (already said) and the
    // job/operation guidance is read before control returns.
    float boardAt = _quickIntro ? 999f : 0.5f;
    float taskAt = _quickIntro ? 0.6f : 2.2f;
    float endAt = _quickIntro ? 2.0f : 3.6f;
    FaceTowards(_teacher, BoardLocal(), dt, 4f);
    FaceTowards(_student, TeacherLocal(), dt, 3f);
    if (!_saidBoard && t >= boardAt) {
      _saidBoard = true;
      Wave(_teacher);
      Say("Look at the board!", "Nhìn lên bảng nhé!");
      Point(_teacher, BoardWorld(), 2.4f);
    }
    if (!_saidTask && t >= taskAt) {
      _saidTask = true;
      FaceTowards(_teacher, RabbitLocal(), dt, 4f);
      // S3-P2Z23 (user: "không nói đây là số mấy, trẻ tự nhận biết"): a PLAIN
      // number is NOT spoken — the board shows it; the teacher only names the
      // job. S3-P2Z29 (user): an operation is read as a "have / want / how many"
      // question (board still shows "8+1").
      if (Kind == RoundKind.Plain) {
        Say("Feed the bunny!", "Cho thỏ ăn nhé!");
      } else {
        AskOperationQuestion();
      }
      Point(_teacher, RabbitWorld(), 2.8f);
      PulseBoard(1.4f);
    }
    if (t >= endAt) StartFeeding();
  }

  void StartFeeding() {
    To(Phase.Feeding);
    Follow();
    _followHanded = true;
    if (_life != null) { try { _life.Begin("question read"); } catch (Exception) { } }
    _lastPos = _player != null ? _player.position : Vector3.zero;
    Log("child control (feeding phase).");
  }

  // (S3-P2Z19: the in-arena student demo + handoff beats are GONE — the garden
  // miniature teaches once; the arena reads the question on the play spot.)

  // ---- the child's feeding --------------------------------------------------------
  // Real actions in the world: click a carrot (walk -> bend -> carry in the
  // fist) -> click the bowl or stop beside the bunny (reach -> the carrot
  // flies to the mouth -> the bunny nibbles -> the teacher counts).

  public void TryPick(RabbitCarrot carrot) {
    if (carrot == null || !carrot.IsAvailable) return;
    if (Current != Phase.Feeding) return;
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

  // S3-P2Z23 (user: "phải có cơ chế nộp bài, có thể thừa hoặc thiếu"): feeding
  // just adds to the bowl — up to ALL the carrots. NEVER auto-succeeds and never
  // auto-corrects; the child decides when the count is right and rings the bell.
  public void TryFeed() {
    if (Carried == null) return;
    if (Current != Phase.Feeding) return;
    RabbitCarrot carrot = Carried;
    Carried = null;
    Count++;
    _fed.Add(carrot);
    if (_viz != null) {
      _viz.FaceTowards(RabbitWorld(), true);
      // S3-P2Z24 (user: "bé cúi đầu như bỏ vào rổ trong khi bục cao hơn đầu"):
      // the old PickUp bend read as dropping the carrot into a ground basket.
      // The bowl sits on the hutch roof, so the child now STANDS and faces it
      // while the carrot lifts and drops onto the bowl (see RabbitCarrot feed).
    }
    carrot.OnMunched = OnCarrotMunched;
    // S3-P2Z19: the carrot flies into the BOWL (on the hutch roof) and STAYS
    // visible there — the bowl is the counter the child can read.
    carrot.BeginFeed(BowlSlotFor(carrot, Count - 1), FeedDelay);
    SetPip(Count);
    Log("feed -> bowl=" + Count + " kind=" + Kind + " a=" + OpA + " b=" + OpB + " target=" + Target);
  }

  void OnCarrotMunched() {
    Nibble();
    PlaySfx("munch");
    Sparkle(RabbitWorld() + new Vector3(0f, 0.6f, 0f), 10, _sparkleSeed++, 0.5f);
    // The teacher counts the bowl aloud one item at a time (it can pass the
    // target — that is the child's choice until they submit).
    if (Current != Phase.Feeding) return;
    int k = Mathf.Clamp(Count, 1, CountEn.Length);
    Say(CountEn[k - 1], CountVi[k - 1]);
    Point(_teacher, RabbitWorld(), 1.6f);
  }

  // The bell: turn the bowl's count in. EXACT is the win; too few or too many is
  // a gentle "count again" and the bowl empties so the child retries — the child
  // really can be wrong (user round).
  public void TrySubmit() {
    if (Current != Phase.Feeding) return;
    Submits++;
    PlaySfx("pickup");
    if (Count == Target) { SuccessBeats(); return; }
    To(Phase.Wrong);
    _wrongT = 0f;
    FaceTowards(_teacher, PlayerLocal(), 0.2f, 5f);
    // S3-P2Z33: a warm, non-punishing "not yet" — board wobble + soft sparkle.
    GameJuice.WrongFx(_board != null ? _board.transform : null, _fx, BoardWorld());
    ActivityFeedback.Retry();
    Say(Count < Target ? "Not enough. Count again!" : "Too many. Count again!",
      Count < Target ? "Chưa đủ rồi. Đếm lại nhé!" : "Thừa rồi. Đếm lại nhé!");
    Point(_teacher, RabbitWorld(), 2.2f);
    Log("wrong submit: count=" + Count + " target=" + Target);
  }

  // The wrong beat: hold the line, then every fed carrot walks home and the
  // child gets the bowl back empty to try again (never a fail screen).
  // S3-P2Z25 (user): after clearing the bowl the teacher RE-READS the question
  // ("look at the board, take the right number of carrots, put them on the
  // bowl"). SafetyFilter caps an NPC line at 6 words, so the question is queued
  // as three short lines and paced out one at a time.
  void TickWrong(float dt) {
    _phaseT += dt;
    FaceTowards(_teacher, PlayerLocal(), dt, 2.5f);
    FaceTowards(_student, PlayerLocal(), dt, 2.5f);
    if (_phaseT < WrongHoldSeconds) return;
    for (int i = 0; i < _fed.Count; i++) {
      RabbitCarrot c = _fed[i];
      if (c != null && c.State != RabbitCarrot.CarrotState.Available) {
        c.SetHand(_playerHand);
        c.BeginReturnHome();
      }
    }
    _fed.Clear();
    Count = 0;
    SetPip(0);
    // S3-P2Z27 (user: "sai thì hướng dẫn làm lại bài đó chi tiết"): a wrong +/- 
    // round is REDONE — the first operand goes back on the bowl so the child can
    // add/remove again.
    if (Kind != RoundKind.Plain) PrefillBowl(OpA);
    To(Phase.Feeding);
    _lastPos = _player != null ? _player.position : Vector3.zero;
    AskQuestionAgain();
    Log("wrong beat done — bowl reset, question re-read.");
  }

  // S3-P2Z25/27/29: re-read the task. A plain number gets the generic lines; an
  // operation re-poses the "have / want / how many" question. Each line stays
  // inside the SafetyFilter NPC cap (<= 6 tokens).
  void AskQuestionAgain() {
    if (Kind == RoundKind.Plain) {
      Ask("Look at the number!", "Nhìn số trên bảng nhé!");
      Ask("Take the right amount.", "Lấy đúng số cà rốt nhé!");
      Ask("Put them on the bowl!", "Đặt lên bục nhé!");
    } else {
      Ask("Look at the board!", "Nhìn lên bảng nhé!");
      AskOperationQuestion();
      Ask("Try again!", "Làm lại nhé!");
    }
    Point(_teacher, BoardWorld(), 2.4f);
  }

  // S3-P2Z29 (user: "thỏ đang có 8 củ, thỏ muốn có 9 củ, cần lấy thêm mấy củ"):
  // the operation question read aloud, one short line at a time.
  void AskOperationQuestion() {
    bool add = Kind == RoundKind.Add;
    int want = Target;
    Ask("The bunny has " + N(OpA) + " carrots.", "Thỏ có " + Nvi(OpA) + " củ cà rốt.");
    Ask(add ? ("The bunny wants " + N(want) + ".") : ("The bunny wants " + N(want) + "."),
      add ? ("Thỏ muốn có " + Nvi(want) + " củ.") : ("Thỏ muốn còn " + Nvi(want) + " củ."));
    Ask(add ? "How many more?" : "How many to take?",
      add ? "Lấy thêm mấy củ?" : "Bỏ bớt mấy củ?");
    Ask(add ? ("To have " + N(want) + ".") : ("To leave " + N(want) + "."),
      add ? ("Để có " + Nvi(want) + " củ.") : ("Để còn " + Nvi(want) + " củ."));
  }

  void SuccessBeats() {
    To(Phase.Success);
    // Reset the correct-submit transition (runs again every round).
    _cleared = false;
    _advance = false;
    _digitPhase = 0;
    _digitT = 0f;
    _returnT = 0f;
    _boardOld = null;
    _boardNew = null;
    PlaySfx("success");
    Sparkle(RabbitWorld() + new Vector3(0f, 0.7f, 0f), 14, _sparkleSeed++, 0.8f);
    // S3-P2Z33: layered win feedback + board pop.
    GameJuice.CorrectFx(_fx, RabbitWorld(), false);
    ActivityFeedback.Correct();
    ActivityGuide.Clear();
    if (_board != null) GameJuice.Pop(_board.transform, 0.10f, 0.3f);
    if (Kind == RoundKind.Plain) {
      Say(Cap(N(Target)) + " " + Carrots(Target) + "! Well done!",
        Cap(Nvi(Target)) + " củ cà rốt! Giỏi!");
    } else {
      // S3-P2Z27 (user: "nếu đúng thì trên bảng hiện 4-2=2"): the board resolves
      // the equation for the payoff. S3-P2Z26: the explanation is queued as short
      // paced lines (SafetyFilter caps an NPC line at 6 words).
      if (_builder != null) {
        _board = _builder.SpawnQuestion((int)Kind, OpA, OpB, Target);
        _boardPulseT = 0f;
      }
      bool add = Kind == RoundKind.Add;
      Ask(add ? "That's right! This is addition." : "That's right! This is subtraction.",
        add ? "Đúng rồi! Đây là phép cộng." : "Đúng rồi! Đây là phép trừ.");
      Ask(Cap(N(OpA)) + " " + Carrots(OpA) + (add ? " plus " : " minus ") + N(OpB) + ".",
        Cap(Nvi(OpA)) + " củ " + (add ? "cộng " : "trừ ") + Nvi(OpB) + " củ.");
      Ask(add ? (Cap(N(Target)) + " carrots on the bowl.")
              : (Cap(N(Target)) + " carrots are left."),
        add ? ("Được " + Nvi(Target) + " củ trên bục.")
            : ("Còn " + Nvi(Target) + " củ trên bục."));
    }
    // S3-P2Z24 (user: "sau khi báo kết quả 'giỏi quá' thì vẫn lại đếm số lượng
    // carot trên bục"): the post-success recap is GONE. The teacher counts each
    // carrot as it is fed (OnCarrotMunched); the win line names the finished
    // count once and stops — no second count-up after the praise.
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
    TickFeedProximity();
    // S3-P2Z22/23: no undershoot hint, no auto-success — the child counts and
    // rings the bell when they think the bowl is right.
    _lastPos = _player != null ? _player.position : Vector3.zero;
    FaceTowards(_teacher, PlayerLocal(), dt, 2.2f);
    FaceTowards(_student, PlayerLocal(), dt, 2.2f);
    FaceRabbitTo(PlayerWorld(), dt);
    // S3-P2Z35: show where to bring the carried carrot.
    if (Carried != null) ActivityGuide.PointAt(BowlWorld()); else ActivityGuide.Clear();
  }

  // "Stop, then feed" (same discipline as gameplay #1): walking PAST the bowl
  // must never fling the carrot out of the hand — the child stops first.
  void TickFeedProximity() {
    if (Carried == null || _player == null) return;
    if (_mover != null && _mover.IsMoving) return;
    // S3-P2Z19: the bowl is the feed target (on the hutch roof), so proximity
    // is measured to the BOWL — standing beside the hutch reaches it.
    if (PlayerNear(BowlWorld(), 1.6f)) TryFeed();
  }

  bool IsPlayerMoving() {
    if (_mover != null) return _mover.IsMoving;
    if (_player == null) return false;
    Vector3 d = _player.position - _lastPos;
    d.y = 0f;
    return d.sqrMagnitude > 0.0004f;
  }

  // S3-P2Z25 (user): on a correct submit the payoff holds a beat, the bowl is
  // cleared, the child walks back to the play spot, then the board number swaps
  // to the next target ("câu hỏi tiếp theo") and play resumes. Re-entry (adopt)
  // keeps the frozen finished picture.
  void TickSuccess(float dt) {
    _phaseT += dt;
    FaceTowards(_teacher, PlayerLocal(), dt, 2f);
    FaceTowards(_student, PlayerLocal(), dt, 2f);
    FaceRabbitTo(PlayerWorld(), dt);
    if (_adopted) {
      if (_phaseT >= 4.6f && !_followHanded) {
        _followHanded = true;
        _cameraDone = true;
        Follow();
      }
      return;
    }
    if (_advance) { TickNumberChange(dt); return; }
    if (!_cleared) {
      // S3-P2Z30 (user: "phần đọc giải thích cần đọc trước khi câu hỏi kế tiếp
      // được đưa ra"): hold the payoff until every explanation line has been
      // spoken, THEN clear the bowl and move on.
      if (_phaseT >= SuccessHoldSeconds && ExplanationSpoken()) ClearBowlAndWalkBack();
      return;
    }
    // Bowl is empty: wait until the child is back on the marked spot (or a
    // timeout) before the next number appears.
    _returnT += dt;
    if (PlayerOnSpot() || _returnT >= NextQuestionWalkTimeout) BeginNextNumber();
  }

  // True once the queued explanation has fully drained and the last line has
  // finished playing.
  bool ExplanationSpoken() {
    if (_askEn.Count > 0) return false;
    if (_voice == null) return true;
    return _voice.Idle && !_voice.HasLine;
  }

  // Test seam for the explanation gate.
  public bool ExplanationSpokenForTests() { return ExplanationSpoken(); }

  // Clear the bowl (every fed carrot walks home) and send the child back to the
  // play spot for the next round.
  void ClearBowlAndWalkBack() {
    _cleared = true;
    _returnT = 0f;
    for (int i = 0; i < _fed.Count; i++) {
      RabbitCarrot c = _fed[i];
      if (c != null && c.State != RabbitCarrot.CarrotState.Available) {
        c.SetHand(_playerHand);
        c.BeginReturnHome();
      }
    }
    _fed.Clear();
    Count = 0;
    SetPip(0);
    if (_result != null) _result.SetActive(false);
    _followHanded = true;
    _cameraDone = true;
    if (_mover != null) {
      try { _mover.MoveTo(PlaySpotWorld()); } catch (Exception) { }
    }
    Follow();
    Log("bowl cleared — child walks back to the play spot.");
  }

  // Swap the board digit to the next target: say "next question", shrink the old
  // number out, pop the new one in (TickNumberChange drives the animation).
  void BeginNextNumber() {
    _advance = true;
    _digitPhase = 1;
    _digitT = 0f;
    _boardPulseT = 0f;
    // S3-P2Z26: live plays a random round (plain / add / sub); tests keep the
    // deterministic ladder (ArithmeticEnabled stays false there).
    if (ForcedNextKind >= 0) {
      SetRound((RoundKind)Mathf.Clamp(ForcedNextKind, 0, 2), ForcedNextA, ForcedNextB);
      ForcedNextKind = -1;
    } else if (ArithmeticEnabled) SetRoundRandom();
    else SetRound(RoundKind.Plain, CountingGardenArea.NextRabbitTarget(Target), 0);
    // S3-P2Z28: the new +/- question must show its first operand on the bowl
    // (the old bug: the next question was staged with an empty bowl).
    if (Kind != RoundKind.Plain) PrefillBowl(OpA);
    if (_builder != null) {
      _boardOld = _board;
      _boardNew = _builder.SpawnQuestion((int)Kind, OpA, OpB);
      if (_boardNew != null) _boardNew.transform.localScale = Vector3.zero;
      _builder.SpawnResultDigit(Target);
      _board = _boardNew;
    }
    SetShot(0); // frame the board (child on the spot in shot)
    Say("Next question!", "Câu hỏi tiếp theo!");
    Point(_teacher, BoardWorld(), 2.4f);
    Log("next question staged: kind=" + Kind + " a=" + OpA + " b=" + OpB + " target=" + Target);
  }

  void TickNumberChange(float dt) {
    if (_digitPhase == 1) {
      _digitT += dt;
      float t = Mathf.Clamp01(_digitT / DigitOutSeconds);
      if (_boardOld != null) {
        try { _boardOld.transform.localScale = Vector3.one * (1f - t); } catch (Exception) { }
      }
      if (t < 1f) return;
      if (_boardOld != null) {
        try { _boardOld.SetActive(false); } catch (Exception) { }
      }
      _digitPhase = 2;
      _digitT = 0f;
      return;
    }
    if (_digitPhase == 2) {
      _digitT += dt;
      float t = Mathf.Clamp01(_digitT / DigitInSeconds);
      float s = EaseOutBack(t);
      if (_boardNew != null) {
        try { _boardNew.transform.localScale = Vector3.one * s; } catch (Exception) { }
      }
      if (t < 1f) return;
      if (_boardNew != null) {
        try { _boardNew.transform.localScale = Vector3.one; } catch (Exception) { }
      }
      _digitPhase = 0;
      // S3-P2Z28: read the new question's guidance (quick intro) before feeding.
      _quickIntro = true;
      _saidBoard = true;   // "Look at the board!" was already said
      _saidTask = false;
      To(Phase.Intro);
      Log("next question ready: kind=" + Kind + " a=" + OpA + " b=" + OpB
        + " target=" + Target);
    }
  }

  static float EaseOutBack(float t) {
    const float c1 = 1.70158f;
    const float c3 = c1 + 1f;
    float u = t - 1f;
    return 1f + c3 * u * u * u + c1 * u * u;
  }

  // (S3-P2Z23: the old auto-overshoot correction is GONE — a wrong count is a
  // wrong SUBMIT now: TickWrong holds the line, empties the bowl, and the child
  // retries. See TrySubmit/TickWrong.)

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
    // The correction path sets _followHanded (FinishCorrect): the camera must
    // STOP re-issuing the success shot then. The old guard let Success re-issue
    // forever after a correction, leaving the camera in Interaction and the
    // exit walk unroutable (S3-P2Z17 journey finding).
    if (_followHanded) return;
    if (!_shotIssued) {
      // Let the area's arrival reveal (2.2s) play first, then hold the
      // teaching frame while the teacher introduces the board.
      if (Current == Phase.Intro && _phaseT >= 2.0f) IssueShot();
      return;
    }
    if (Current != Phase.Intro && Current != Phase.Success) return;
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

  // S3-P2Z32 (UX): bowl manipulation (drag / tap-select) is live only while the
  // child STANDS AT THE BOWL. From across the arena a click on a fed carrot must
  // WALK the child over (ClickRouter routes it) — never silently select a carrot
  // and leave the child standing (journey finding: clicks meant to walk to the
  // bowl were swallowed by the drag target, and a later garden tap returned the
  // carrot, dropping the count).
  public const float BowlReach = 2.4f;
  public bool PlayerAtBowl { get { return PlayerNear(BowlWorld(), BowlReach); } }

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

  // Fed carrots rest IN the bowl — each gets its own slot so the count reads
  // from the gameplay camera, never a pile. S3-P2Z29 (user: "lấy thêm 1 củ mà
  // tổng vẫn 8"): the old list hid the 9th carrot under the middle ones; a
  // 6-outer + 4-inner ring keeps all TEN countable.
  static readonly Vector2[] BowlOffsets = {
    new Vector2(0.30f, 0.00f), new Vector2(0.15f, 0.26f), new Vector2(-0.15f, 0.26f),
    new Vector2(-0.30f, 0.00f), new Vector2(-0.15f, -0.26f), new Vector2(0.15f, -0.26f),
    new Vector2(0.12f, 0.07f), new Vector2(-0.07f, 0.12f), new Vector2(-0.12f, -0.07f),
    new Vector2(0.07f, -0.12f),
  };

  Vector3 BowlSlot(int i) {
    Vector3 b = RabbitPlayBuilder.BowlPos;
    if (_root == null) return b;
    if (i < 0) i = 0;
    if (i >= BowlOffsets.Length) i = BowlOffsets.Length - 1;
    Vector2 o = BowlOffsets[i];
    // Builder-local == game-local (both hang under the arena root). The slot is
    // the bowl RIM surface; the carrot's measured SitOffset is added on top so
    // its real bottom rests here (S3-P2Z23).
    return new Vector3(b.x + o.x, b.y + 0.06f, b.z + o.y);
  }

  // The bowl slot tuned to a specific carrot's measured bottom (S3-P2Z23).
  Vector3 BowlSlotFor(RabbitCarrot c, int i) {
    Vector3 s = BowlSlot(i);
    if (c != null) s.y += c.SitOffset;
    return s;
  }

  // S3-P2Z19: state only — the pip board is gone (the bowl is the counter).
  void SetPip(int n) {
    PipCount = n;
    // S3-P2Z34: live bowl progress ("4/7 on the bowl") while the child feeds.
    if (Current == Phase.Feeding || Current == Phase.Wrong || Current == Phase.Success)
      ActivityFeedback.Progress(n, Target);
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

  void OnDestroy() { ActivityGuide.Clear(); ActivityFeedback.Clear(); }
}
