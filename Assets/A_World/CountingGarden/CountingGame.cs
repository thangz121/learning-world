// A_World/CountingGarden/CountingGame.cs — S3-P2Z4 REFERENCE GAMEPLAY
// "ĐƯA ĐÚNG SỐ LƯỢNG VÀO RỔ" (put the right number in the basket).
// Three scene-local pieces, no manager/singleton/service:
//   CountingBall — one real ball: grounded -> carried (arc to the hand, then a
//                  smooth follow) -> in basket (arc + bounce) -> removed.
//                  Clickable through the existing ClickRouter/IClickTarget
//                  contract (click -> walk -> arrival -> pick).
//   BasketZone   — the basket's click/proximity door: places the carried ball
//                  into a deterministic slot and reports the count.
//   CountingGame — the activity's state machine (INTRO -> FREE_PLAY -> count
//                  1, 2 -> SUCCESS | WRONG -> COMPLETED), the teacher's counting
//                  lines/points, the count display + result board, and the
//                  deterministic re-entry policy through the shared
//                  ActivityLifecycle (owned by the Math-side area so it
//                  survives the arena unload; save format untouched).
// C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CountingBall : MonoBehaviour, IClickTarget {
  public enum BallState { Grounded, Carried, InBasket, Removed }

  public BallState State { get; private set; } = BallState.Grounded;
  public Vector3 HomeLocal;
  public CountingGame Game;
  // Fired the moment a place flight settles in the basket slot. The game hangs
  // the basket sound + the counting/celebration beats on it, so feedback
  // follows the REAL moment the ball lands — not the click that started it.
  public Action OnLanded;

  Transform _hand;
  Vector3 _flyFrom, _flyTo;
  float _flyT, _flyDur, _flyLift;
  bool _flying;
  bool _returning;
  float _bounceT = 1f;
  Vector3 _baseScale = Vector3.one;
  Collider _collider;

  // S3-P2Z10 pickup/place ACTIONS (user brief §5/§8): the ball waits for the
  // child's body to reach it (pick delay = the bend), rides the animated fist
  // while the child reaches over the rim (place delay), then drops the short
  // distance into the slot. Ground -> hand -> basket, all visible.
  bool _picking;
  float _pickT, _pickDelay, _pickDur;
  Vector3 _pickFrom;
  bool _placing, _placeActive;
  float _placeT, _placeDelay, _placeDur;
  Vector3 _placeFrom, _placeTo;
  float _placeLift;

  public bool IsGrounded { get { return State == BallState.Grounded; } }
  public bool IsFlying { get { return _flying; } }

  public void Bind(CountingGame game, Transform hand) {
    Game = game;
    _hand = hand;
    _baseScale = transform.localScale;
    _collider = GetComponent<Collider>();
  }

  public void OnClicked() {
    if (State != BallState.Grounded || Game == null) return;
    Game.TryPick(this);
  }

  // Pickup: the child bends (player PickUp clip); once the hand is down, the
  // ball arcs up into it and rides the fist. No ground->hand snap.
  public void BeginCarry(float delay = 0f) {
    if (State != BallState.Grounded) return;
    State = BallState.Carried;
    if (_collider != null) _collider.enabled = false;
    _flying = false;
    _returning = false;
    _placing = false;
    _placeActive = false;
    _picking = true;
    _pickT = 0f;
    _pickDelay = Mathf.Max(0f, delay);
    _pickDur = 0.42f;
    _pickFrom = transform.localPosition;
  }

  // Place: the ball keeps riding the fist while the child reaches over the rim,
  // then drops the last stretch into the slot with a small bounce.
  public void BeginPlace(Vector3 slotLocal, float delay = 0f) {
    if (State != BallState.Carried) return;
    State = BallState.InBasket;
    _picking = false;
    _placing = true;
    _placeActive = false;
    _placeT = 0f;
    _placeDelay = Mathf.Max(0f, delay);
    _placeDur = 0.34f;
    _placeLift = 0.16f;
    _placeTo = slotLocal;
  }

  // Wrong path: the extra ball leaves the basket and returns home (gentle).
  public void BeginReturnHome() {
    State = BallState.Carried; // re-uses the flight while it travels
    _picking = false;
    _placing = false;
    _placeActive = false;
    _returning = true;
    _flyFrom = transform.localPosition;
    _flyTo = HomeLocal;
    _flyT = 0f;
    _flyDur = 0.6f;
    _flyLift = 0.8f;
    _flying = true;
  }

  public void ParkInBasket(Vector3 slotLocal) {
    State = BallState.InBasket;
    _flying = false;
    _picking = false;
    _placing = false;
    _placeActive = false;
    transform.localPosition = slotLocal;
  }

  public void MarkRemoved() {
    State = BallState.Removed;
    if (_collider != null) _collider.enabled = false;
  }

  void Update() { TickForTests(Time.deltaTime); }

  // Deterministic tick (EditMode cover: no live frame needed).
  public void TickForTests(float dt) {
    try {
      if (_picking) { TickPick(dt); return; }
      if (_placing) { TickPlace(dt); return; }
      if (_flying) { TickFlight(dt); return; }
      if (State == BallState.Carried && _hand != null) FollowHand(dt);
      TickBounceAndPulse(dt);
    } catch (Exception) {
      _picking = false;
      _placing = false;
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
    if (t >= 1f) { _picking = false; _bounceT = 0f; }
  }

  void TickPlace(float dt) {
    _placeT += dt;
    if (_placeT < _placeDelay) {
      // Still reaching: the ball rides the fist down toward the rim.
      if (_hand != null) FollowHand(dt);
      return;
    }
    if (!_placeActive) {
      _placeActive = true;
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
    bool landedInBasket = _placeActive;
    _placeActive = false;
    _placing = false;
    if (_returning) {
      // Landed back home: clickable again (the correction is done).
      _returning = false;
      State = BallState.Grounded;
      if (_collider != null) _collider.enabled = true;
    }
    if (landedInBasket && OnLanded != null) {
      try { OnLanded(); } catch (Exception) { }
    }
  }

  void TickBounceAndPulse(float dt) {
    // Small landing bounce after a place/return.
    if (_bounceT < 1f) {
      _bounceT = Mathf.Min(1f, _bounceT + dt / 0.3f);
      float s = 1f + 0.16f * Mathf.Sin(Mathf.PI * _bounceT);
      transform.localScale = _baseScale * s;
    }
    // Grounded: gentle proximity pulse (a soft "I'm pickable" cue).
    if (State == BallState.Grounded && Game != null && Game.PlayerNear(transform.position, 2.4f)) {
      float s = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
      transform.localScale = _baseScale * s;
    } else if (State == BallState.Grounded && _bounceT >= 1f) {
      transform.localScale = _baseScale;
    }
  }
}

// The basket's door: click it (walk-up arrival) OR simply carry a ball close to
// it — both route to the same place call (state-guarded, spam-safe).
[DisallowMultipleComponent]
public class BasketZone : MonoBehaviour, IClickTarget {
  public CountingGame Game;
  public float placeRadius = 1.5f;
  Transform _player;
  ClickToMove _mover;

  public void Bind(CountingGame game, Transform player) {
    Game = game;
    _player = player;
    _mover = player != null ? player.GetComponent<ClickToMove>() : null;
    // Unconditional (see CountingGame.Build: stripped colliders die at the end
    // of the frame, so a null-check here would leave the basket click-less).
    BoxCollider box = gameObject.AddComponent<BoxCollider>();
    box.size = new Vector3(1.3f, 0.7f, 1.3f);
    box.center = new Vector3(0f, 0.35f, 0f);
  }

  public void OnClicked() {
    if (Game != null) Game.TryPlace();
  }

  void Update() {
    if (Game == null || _player == null || Game.Carried == null) return;
    // "Stop, then place" (user brief §8): walking PAST the basket must never
    // fling the ball out of the hand — the child stops at the basket first.
    if (_mover != null && _mover.IsMoving) return;
    Vector3 a = _player.position;
    Vector3 b = transform.position;
    float dx = a.x - b.x, dz = a.z - b.z;
    if (dx * dx + dz * dz <= placeRadius * placeRadius) Game.TryPlace();
  }
}

[DisallowMultipleComponent]
public class CountingGame : MonoBehaviour {
  public enum Phase { Intro, FreePlay, Success, Wrong, Completed }

  public Phase Current { get; private set; } = Phase.Intro;
  public CountingBall Carried { get; private set; }
  public int Count { get; private set; }
  public int Target = 2;
  public int WrongCount { get; private set; }

  CountingDemo _demo;
  CountingPlayBuilder _builder;
  Transform _player;
  Transform _hand;
  PlayerVisual _viz;
  ActivityLifecycle _life;
  IAudioDirector _audio;
  readonly List<CountingBall> _balls = new List<CountingBall>();
  // Placement order (the first Target balls stay; extras go home on a wrong path).
  readonly List<CountingBall> _inBasket = new List<CountingBall>();

  // Action timing (S3-P2Z10): the ball waits for the child's PickUp bend, and
  // for the place it rides the fist while the child reaches over the rim. Both
  // values are deliberately short (a 4yo must not wait), long enough that the
  // ground->hand->basket path is visible.
  public float PickDelay = 0.45f;
  public float PlaceDelay = 0.5f;

  // Wrong-path beat (timed, deterministic; no coroutines so tests can tick it).
  int _wrongStep = -1;
  float _wrongT;
  CountingBall _extra;
  // What the NEXT landing means (1 = say "One ball.", 2 = success beats).
  int _landBeats;
  float _playerVictoryT = -1f;
  // S3-P2Z11 juice + the listen circle.
  Transform _fx;
  float _callBackT;
  float _listenT;
  bool _listenSettled;
  Vector3 _listenRingBase = Vector3.one;
  float _trailT;
  int _sparkleSeed = 700;
  readonly float[] _pipPopT = { 1f, 1f };
  int _lastPip = -1;

  public bool IntroDone { get { return Current != Phase.Intro; } }
  public int BallCount { get { return _balls.Count; } }
  public CountingBall BallAt(int i) { return i >= 0 && i < _balls.Count ? _balls[i] : null; }

  public void Build(CountingDemo demo, CountingPlayBuilder builder, Transform player,
      Transform hand, ActivityLifecycle life, IAudioDirector audio) {
    _demo = demo;
    _builder = builder;
    _player = player;
    _hand = hand != null ? hand : player;
    _viz = player != null ? player.GetComponent<PlayerVisual>() : null;
    _life = life;
    _audio = audio;
    if (_builder == null || _builder.Activity == null) return;
    // Balls become real, clickable game objects (collider re-added: the
    // builder strips it for bake safety, clicks need it).
    _balls.Clear();
    List<GameObject> balls = _builder.Activity.Balls;
    for (int i = 0; i < balls.Count; i++) {
      GameObject go = balls[i];
      if (go == null) continue;
      // UNCONDITIONAL: the builder's StripCollider uses Destroy (deferred to
      // the end of the frame) at runtime, so a GetComponent==null check here
      // still sees the doomed collider and skips adding one — the ball ends up
      // click-less (journey bug: the child could not pick anything up).
      SphereCollider sc = go.AddComponent<SphereCollider>();
      sc.radius = 0.7f; // a touch more forgiving for small hands (0.31m world)
      CountingBall ball = go.GetComponent<CountingBall>();
      if (ball == null) ball = go.AddComponent<CountingBall>();
      ball.HomeLocal = _builder.Activity.BallHomes != null && i < _builder.Activity.BallHomes.Length
        ? _builder.Activity.BallHomes[i] : go.transform.localPosition;
      ball.Bind(this, _hand);
      _balls.Add(ball);
    }
    // Basket door.
    if (_builder.Activity.Basket != null) {
      BasketZone zone = _builder.Activity.Basket.GetComponent<BasketZone>();
      if (zone == null) zone = _builder.Activity.Basket.gameObject.AddComponent<BasketZone>();
      zone.Bind(this, _player);
    }
    // Juice root (sparkles are transform-only DemoJuice bits, arena-scoped).
    GameObject fx = new GameObject("CPGameFx");
    fx.transform.SetParent(_builder.transform, false);
    _fx = fx.transform;
    if (_builder.ListenRing != null) _listenRingBase = _builder.ListenRing.transform.localScale;
    if (_demo != null) {
      _demo.ObserveTarget = _player;
      _demo.OnIntroCompleted = OnIntroCompleted;
    }
    // Re-entry policy FIRST: a completed activity adopts its finished visual
    // without replaying the intro (the lifecycle lives in MathScene, so it
    // survives the arena unload). Journey bug (S3-P2Z10): this check used to
    // run AFTER the no-intro handover, and the staged MarkAvailable/BeginEnter/
    // MarkReady sequence (which ran last) left the lifecycle in Ready — so
    // MarkCompleted silently failed and every re-entry started from 0.
    if (_life != null && _life.State == ActivityState.Completed) {
      ApplyCompletedState("adopt");
      return;
    }
    if (_life != null) {
      try {
        _life.MarkAvailable("counting_game staged");
        _life.BeginEnter("arena built");
        _life.MarkReady("intro staged");
      } catch (Exception) { }
    }
    // S3-P2Z9 (user order): the arena never replays the demo — the child came
    // to PLAY. Control is handed over at build; the assignment ("đề bài") is
    // read once the child reaches the play field (TickTask below). Ready ->
    // Active so the completion path (Success/Complete) can actually settle.
    if (_demo != null && _demo.NoIntroMode) {
      Current = Phase.FreePlay;
      try { if (_life != null) _life.Begin("arena play mode"); } catch (Exception) { }
    }
  }

  // ---- state flow ---------------------------------------------------------------

  void OnIntroCompleted() {
    if (Current != Phase.Intro) return;
    Current = Phase.FreePlay;
    if (_life != null) { try { _life.Begin("intro done"); } catch (Exception) { } }
    if (_demo != null) {
      // Control has passed: no re-teaching, no camera hijack while the child
      // plays (the audience gate stays off for the rest of this visit).
      _demo.AudienceGateEnabled = false;
      _demo.TeacherSay("Now you try!", "Con làm thử nhé!");
    }
  }

  public void TryPick(CountingBall ball) {
    if (ball == null || !ball.IsGrounded) return;
    // The intro owns the stage first; after the goal is met the child may keep
    // playing (a 3rd ball then becomes the gentle counting correction).
    if (Current != Phase.FreePlay && Current != Phase.Success) return;
    // S3-P2Z11 (user: "sau khi nghe câu hỏi thì mới chơi"): the assignment must
    // be heard at the listen circle before the balls accept clicks. The teacher
    // calls the child back once (not a nag), pointing at the circle.
    if (!_taskTold) {
      if (_callBackT <= 0f && _demo != null) {
        _callBackT = 6f;
        _demo.TeacherSay("Stand on the circle first!", "Con đứng vào vòng nhé!");
        _demo.PointTeacherAt(ListenWorld(), 2.2f);
      }
      return;
    }
    if (Carried != null) return;             // one ball at a time
    Carried = ball;
    // The child's OWN body acts: turn to the ball and bend down (real PickUp
    // clip on the player rig); the ball only starts moving once the hand is
    // down (PickDelay), so the pickup reads as one physical action.
    if (_viz != null) {
      _viz.FaceTowards(ball.transform.position, true);
      _viz.PlayPickup();
    }
    PlaySfx("pickup");
    Sparkle(ball.transform.position, 6, _sparkleSeed++, 0.3f);
    ball.BeginCarry(PickDelay);
  }

  public void TryPlace() {
    if (Carried == null) return;
    if (Current != Phase.FreePlay && Current != Phase.Success) return;
    CountingBall ball = Carried;
    Carried = null;
    Count++;
    _inBasket.Add(ball);
    // Turn to the basket and reach over the rim (same PickUp clip — a real
    // reach-and-lower action), then the ball drops the short distance in.
    if (_viz != null) {
      _viz.FaceTowards(BasketWorld(), true);
      _viz.PlayPickup();
    }
    ball.OnLanded = OnBallLanded;
    if (Count <= Target) {
      ball.BeginPlace(BasketSlot(Count - 1), PlaceDelay);
      SetPip(Count);
      if (Count == 1) {
        _landBeats = 1;
      } else {
        Success();
      }
    } else {
      // WRONG path: the child placed one too many — a counting lesson, never a
      // punishment. The extra ball goes back home after the teacher explains.
      _extra = ball;
      ball.BeginPlace(BasketSlot(Target), PlaceDelay); // visibly lands in the basket first
      Current = Phase.Wrong;
      _wrongStep = 0;
      _wrongT = 0f;
      WrongCount++;
    }
  }

  // The goal is met (2 in the basket): confirm it, but keep the field open —
  // if the child adds a 3rd ball the gentle counting correction runs instead.
  // The spoken confirmation waits for the ball to actually LAND (OnBallLanded).
  void Success() {
    Current = Phase.Success;
    if (_builder != null && _builder.Activity != null && _builder.Activity.Result != null) {
      GameObject result = _builder.Activity.Result;
      result.SetActive(true);
      result.transform.localScale = Vector3.one * 0.65f;
      _resultPopT = 0f;
    }
    _landBeats = 2;
    MarkLifeCompleted();
  }

  // The ball settled in its slot: now the world answers. (Basket sound always;
  // "One ball." after the first; the full success moment after the second.)
  void OnBallLanded() {
    PlaySfx("basket");
    Sparkle(BasketWorld() + new Vector3(0f, 0.55f, 0f), 10, _sparkleSeed++, 0.5f);
    if (_landBeats == 1) {
      _landBeats = 0;
      if (_demo != null) {
        _demo.TeacherSay("One ball.", "Một quả bóng.");
        _demo.PointTeacherAt(BasketWorld(), 1.6f);
      }
    } else if (_landBeats == 2) {
      _landBeats = 0;
      if (Current != Phase.Success) return; // a correction started meanwhile
      SuccessBeats();
    }
  }

  // The "À, xong rồi!" moment: teacher confirms + points, both NPCs celebrate,
  // and the child's own avatar does a little victory once the reach is done.
  void SuccessBeats() {
    PlaySfx("success");
    Sparkle(BasketWorld() + new Vector3(0f, 0.7f, 0f), 14, _sparkleSeed++, 0.8f);
    if (_demo != null) {
      _demo.TeacherSay("Two balls!", "Hai quả bóng!", SpeechStyle.Excited, AudioPriority.P4_Feedback);
      _demo.PointTeacherAt(BasketWorld(), 2.0f);
      _demo.CelebrateBoth();
    }
    _playerVictoryT = 1.35f; // after the place animation, before the child walks off
  }

  void MarkLifeCompleted() {
    if (_life == null) return;
    try {
      if (_life.State != ActivityState.Completed) _life.MarkCompleted("two balls in the basket");
    } catch (Exception) { }
  }

  // Terminal state: the activity is settled (clean run left as Success with the
  // field still open; the correction or an exit settles it here).
  void Complete() {
    Current = Phase.Completed;
    _landBeats = 0;
    _playerVictoryT = -1f;
    MarkLifeCompleted();
    // Leftover grounded balls are finished business: keep them as scenery.
    for (int i = 0; i < _balls.Count; i++) {
      CountingBall b = _balls[i];
      if (b != null && b.IsGrounded) b.MarkRemoved();
    }
    try { Debug.Log("[CountingGame] COMPLETED (count=" + Count + ", wrong=" + WrongCount + ").", this); }
    catch (Exception) { }
  }

  // Deterministic re-entry: show exactly the finished picture.
  void ApplyCompletedState(string reason) {
    Current = Phase.Completed;
    _landBeats = 0;
    _playerVictoryT = -1f;
    if (_builder == null || _builder.Activity == null) return;
    // The demo FIRST: SkipToObserving() runs ResetActors(), which moves every
    // ball back to its home and hides the result board. Parking the balls or
    // showing the result before this call is undone by it (journey bug: the
    // re-entry basket looked empty even though the state said Completed).
    if (_demo != null) {
      _demo.ObserveTarget = _player;
      _demo.OnIntroCompleted = null;   // never replay the intro on re-entry
      _demo.AudienceGateEnabled = false; // nor re-teach / re-frame the camera
      _demo.SkipToObserving();         // actors start as observers, not teachers
    }
    for (int i = 0; i < _balls.Count; i++) {
      CountingBall b = _balls[i];
      if (b == null) continue;
      if (i < Target) b.ParkInBasket(BasketSlot(i));
      else b.MarkRemoved();
    }
    Count = Target;
    _inBasket.Clear();
    for (int i = 0; i < Target && i < _balls.Count; i++) _inBasket.Add(_balls[i]);
    SetPip(1);
    SetPip(2);
    if (_builder.Activity.Result != null) _builder.Activity.Result.SetActive(true);
    try { Debug.Log("[CountingGame] adopted COMPLETED state (" + reason + ").", this); }
    catch (Exception) { }
  }

  // ---- wrong path beats (deterministic timer) ------------------------------------

  void Update() { TickForTests(Time.deltaTime); }

  // Deterministic tick (EditMode cover: no live frame needed).
  public void TickForTests(float dt) {
    TickResultPop(dt);
    TickTask(dt);
    TickTaskBeats(dt);
    TickPlayerVictory(dt);
    TickListen(dt);
    TickTrail(dt);
    TickPips(dt);
    if (_callBackT > 0f) _callBackT -= dt;
    if (Current != Phase.Wrong) return;
    TickWrong(dt);
  }

  // The listen circle pulses until the assignment is read, then settles to a
  // calm mint (the spot "switches on" and the child may play).
  void TickListen(float dt) {
    if (_builder == null || _builder.ListenRing == null) return;
    if (_taskTold) {
      if (_listenSettled) return;
      _listenSettled = true;
      _builder.ListenRing.transform.localScale = _listenRingBase;
      Renderer r = _builder.ListenRing.GetComponent<Renderer>();
      if (r != null) r.sharedMaterial = PipMaterial(new Color(0.55f, 0.85f, 0.60f), 0.25f);
      return;
    }
    _listenT += dt;
    float s = 1f + 0.075f * Mathf.Sin(_listenT * 3.4f);
    _builder.ListenRing.transform.localScale =
      new Vector3(_listenRingBase.x * s, _listenRingBase.y, _listenRingBase.z * s);
  }

  // A tiny sparkle trail follows the carried ball (it reads as "magic hands",
  // and the child always sees where their ball is).
  void TickTrail(float dt) {
    if (Carried == null) { _trailT = 0f; return; }
    _trailT += dt;
    if (_trailT < 0.35f) return;
    _trailT = 0f;
    Sparkle(Carried.transform.position + new Vector3(0f, 0.1f, 0f), 3, _sparkleSeed++, 0.14f);
  }

  // Filled count pips pop once (transform-only).
  void TickPips(float dt) {
    if (_builder == null || _builder.CountPips == null) return;
    for (int i = 0; i < _builder.CountPips.Length && i < _pipPopT.Length; i++) {
      if (_pipPopT[i] >= 1f) continue;
      _pipPopT[i] = Mathf.Min(1f, _pipPopT[i] + dt / 0.28f);
      GameObject pip = _builder.CountPips[i];
      if (pip == null) continue;
      float s = 1f + 0.5f * Mathf.Sin(Mathf.PI * _pipPopT[i]);
      pip.transform.localScale = new Vector3(0.36f * s, 0.01f, 0.36f * s);
    }
  }

  // The child's own little victory hop, timed to land after the place reach.
  void TickPlayerVictory(float dt) {
    if (_playerVictoryT < 0f) return;
    _playerVictoryT -= dt;
    if (_playerVictoryT > 0f) return;
    _playerVictoryT = -1f;
    if (_viz != null && !_viz.IsMoving) _viz.PlayVictory();
  }

  // ---- task announcement (S3-P2Z9) -----------------------------------------------
  // User: "đã vào arena thì không chạy lại demo, đưa ra đề bài luôn — nhưng đợi
  // trẻ đến gần chỗ chơi mới bắt đầu đọc." So: one proximity-gated announcement
  // on the way in, two short spaced lines (the demo's speech pacer handles the
  // breathing room), then the actors observe while the child works.

  public float TaskRadius = 1.5f;
  bool _taskTold;
  int _taskStep = -1;
  float _taskT;

  public bool TaskTold { get { return _taskTold; } }

  // The fixed listening spot (S3-P2Z11): the marked circle the child stands on
  // to hear the assignment. The teacher reads the task only here.
  Vector3 ListenWorld() {
    if (_builder != null && _builder.ListenPad != null)
      return _builder.ListenPad.transform.position;
    return transform.position;
  }

  // Test seam: the picking gate without walking a live player to the circle.
  public void MarkTaskToldForTests() {
    if (_taskTold) return;
    _taskTold = true;
    _taskStep = 0;
    _taskT = 0f;
  }

  void TickTask(float dt) {
    if (_taskTold || Current == Phase.Completed || _builder == null) return;
    if (!PlayerNear(ListenWorld(), TaskRadius)) return;
    _taskTold = true;
    _taskStep = 0;
    _taskT = 0f;
    PlaySfx("ding");
    Sparkle(ListenWorld() + new Vector3(0f, 0.35f, 0f), 8, _sparkleSeed++, 0.45f);
    try { Debug.Log("[CountingGame] task announced (child stood on the listen circle).", this); }
    catch (Exception) { }
  }

  void TickTaskBeats(float dt) {
    if (_taskStep < 0) return;
    _taskT += dt;
    if (_taskStep == 0 && _taskT >= 0.15f) {
      _taskStep = 1;
      if (_demo != null) {
        _demo.TeacherSay("Put two balls in the basket!", "Bỏ hai bóng vào giỏ nhé!");
        _demo.PointTeacherAt(BasketWorld(), 2.4f);
      }
    } else if (_taskStep == 1 && _taskT >= 2.8f) {
      _taskStep = 2;
      if (_demo != null) {
        _demo.TeacherSay("The board says two!", "Bảng ghi số hai!");
        _demo.PointTeacherAt(BoardWorld(), 2.2f);
      }
    }
  }

  void TickWrong(float dt) {
    if (Current != Phase.Wrong) return;
    _wrongT += dt;
    if (_demo == null) { FinishWrong(); return; }
    switch (_wrongStep) {
      // Beat spacing (S3-P2Z6): the demo PACES speech (a line waits for the
      // previous + a breath), so these beats leave room for each count line —
      // the old 1.4s gaps made the correction sound like one run-on sentence.
      case 0:
        if (_wrongT >= 0.4f) {
          _wrongStep = 1;
          _demo.TeacherSay("Let's count again!", "Cùng đếm lại nhé!");
          _demo.PointTeacherAt(BasketWorld(), 2.2f);
        }
        break;
      case 1:
        if (_wrongT >= 2.0f) { _wrongStep = 2; _demo.TeacherSay("One ball.", "Một quả bóng."); }
        break;
      case 2:
        if (_wrongT >= 3.7f) { _wrongStep = 3; _demo.TeacherSay("Two balls.", "Hai quả bóng."); }
        break;
      case 3:
        if (_wrongT >= 5.4f) {
          _wrongStep = 4;
          _demo.TeacherSay("Three balls.", "Ba quả bóng.");
          _demo.PointTeacherAt(BoardWorld(), 2.4f);
        }
        break;
      case 4:
        if (_wrongT >= 7.2f) { _wrongStep = 5; _demo.TeacherSay("The board says two.", "Bảng ghi số hai."); }
        break;
      case 5:
        if (_wrongT >= 9.0f) {
          _wrongStep = 6;
          _demo.TeacherSay("We only need two.", "Mình chỉ cần hai.");
          if (_extra != null) _extra.BeginReturnHome();
        }
        break;
      case 6:
        if (_wrongT >= 10.8f) FinishWrong();
        break;
    }
  }

  void FinishWrong() {
    _extra = null;
    _landBeats = 0;
    // Every ball placed BEYOND the target goes home (placement order), so the
    // basket ends with exactly the board's number — whatever the child picked.
    for (int i = Target; i < _inBasket.Count; i++) {
      CountingBall b = _inBasket[i];
      if (b != null && b.State == CountingBall.BallState.InBasket) b.BeginReturnHome();
    }
    while (_inBasket.Count > Target) _inBasket.RemoveAt(_inBasket.Count - 1);
    Count = Target;
    SetPip(1);
    SetPip(2);
    // The correction lands exactly on the board's number: confirm it the same
    // way a clean run would (result board + gentle celebration), then complete.
    if (_builder != null && _builder.Activity != null && _builder.Activity.Result != null) {
      _builder.Activity.Result.SetActive(true);
      _builder.Activity.Result.transform.localScale = Vector3.one * 0.65f;
      _resultPopT = 0f;
    }
    if (_demo != null) {
      _demo.TeacherSay("Two balls! Well done!", "Hai bóng! Giỏi!");
      _demo.CelebrateBoth();
    }
    Complete();
  }

  float _resultPopT = 1f;

  void TickResultPop(float dt) {
    if (_builder == null || _builder.Activity == null || _builder.Activity.Result == null) return;
    if (!_builder.Activity.Result.activeSelf || _resultPopT >= 1f) return;
    _resultPopT = Mathf.Min(1f, _resultPopT + dt / 0.3f);
    float s = Mathf.Lerp(0.65f, 1f, Mathf.SmoothStep(0f, 1f, _resultPopT));
    _builder.Activity.Result.transform.localScale = Vector3.one * s;
  }

  // ---- helpers --------------------------------------------------------------------

  public bool PlayerNear(Vector3 world, float radius) {
    if (_player == null) return false;
    Vector3 p = _player.position;
    float dx = p.x - world.x, dz = p.z - world.z;
    return dx * dx + dz * dz <= radius * radius;
  }

  Vector3 BasketWorld() {
    return _builder != null && _builder.Activity != null && _builder.Activity.Basket != null
      ? _builder.Activity.Basket.position : transform.position;
  }

  Vector3 BoardWorld() {
    return _builder != null && _builder.Activity != null && _builder.Activity.Number != null
      ? _builder.Activity.Number.transform.position : transform.position;
  }

  // Balls rest INSIDE the basket (only their tops show above the rim) — the
  // journey shot showed them perched on the rim like decorations. Designed
  // slots (user brief §9): each ball gets its own spot so the count is
  // readable from the gameplay camera, never a pile on the same point.
  static readonly Vector2[] SlotOffsets = {
    new Vector2(-0.20f, 0.10f),
    new Vector2(0.22f, -0.08f),
  };

  Vector3 BasketSlot(int i) {
    Vector3 b = _builder != null && _builder.Activity != null && _builder.Activity.Basket != null
      ? _builder.Activity.Basket.localPosition : Vector3.zero;
    if (i < 0) i = 0;
    if (i >= SlotOffsets.Length) i = SlotOffsets.Length - 1;
    Vector2 o = SlotOffsets[i];
    return new Vector3(b.x + o.x, 0.52f, b.z + o.y);
  }

  void PlaySfx(string id) {
    if (_audio == null) return;
    try { _audio.PlaySfx(new SfxId(id)); } catch (Exception) { }
  }

  void Sparkle(Vector3 world, int count, int seed, float radius) {
    if (_fx == null) return;
    try {
      DemoJuice.Sparkle(_fx, _fx.InverseTransformPoint(world), count, seed, radius);
    } catch (Exception) { }
  }

  // Empty slots stay visible (grey) and turn gold as the child counts: the
  // board reads "0/2 -> 1/2 -> 2/2" at a glance, no text.
  Material _pipGold;
  Material _pipEmpty;
  // How many slots are filled (test seam; the pips themselves stay visible as
  // grey empty slots so the board reads as a counter at a glance).
  public int PipCount { get; private set; }

  void SetPip(int n) {
    PipCount = n;
    if (_builder == null || _builder.CountPips == null) return;
    if (_pipGold == null) _pipGold = PipMaterial(new Color(0.98f, 0.78f, 0.25f), 0.45f);
    if (_pipEmpty == null) _pipEmpty = PipMaterial(new Color(0.68f, 0.68f, 0.66f), 0f);
    for (int i = 0; i < _builder.CountPips.Length; i++) {
      GameObject pip = _builder.CountPips[i];
      if (pip == null) continue;
      pip.SetActive(true);
      Renderer rend = pip.GetComponent<Renderer>();
      if (rend != null) rend.sharedMaterial = (i < n) ? _pipGold : _pipEmpty;
      if (i < n && i > _lastPip && i < _pipPopT.Length) _pipPopT[i] = 0f; // pop the new one
    }
    _lastPip = n - 1;
  }

  static Material PipMaterial(Color color, float emission) {
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    if (emission > 0f && mat.HasProperty("_EmissionColor")) {
      mat.EnableKeyword("_EMISSION");
      mat.SetColor("_EmissionColor", color * emission);
    }
    mat.enableInstancing = true;
    return mat;
  }

  // Test seams (no live scene needed).
  public void SetPhaseForTests(Phase p) { Current = p; }
  public void TickWrongForTests(float dt) {
    float keep = Time.deltaTime;
    try { Update(); } catch (Exception) { }
  }
}
