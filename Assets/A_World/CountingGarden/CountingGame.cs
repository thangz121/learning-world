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

  Transform _hand;
  Vector3 _flyFrom, _flyTo;
  float _flyT, _flyDur, _flyLift;
  bool _flying;
  bool _placing;
  bool _returning;
  float _bounceT = 1f;
  Vector3 _baseScale = Vector3.one;
  Collider _collider;

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

  // Pickup: an anticipation dip, then an arc into the hand (no snapping).
  public void BeginCarry() {
    if (State != BallState.Grounded) return;
    State = BallState.Carried;
    if (_collider != null) _collider.enabled = false;
    _placing = false;
    _flyFrom = transform.localPosition;
    _flyTo = _flyFrom + new Vector3(0f, 0.35f, 0f);
    _flyT = 0f;
    _flyDur = 0.28f;
    _flyLift = 0.45f;
    _flying = true;
  }

  // Place: arc from the hand into the basket slot, then a small bounce.
  public void BeginPlace(Vector3 slotLocal) {
    if (State != BallState.Carried) return;
    State = BallState.InBasket;
    _placing = true;
    _flyFrom = transform.localPosition;
    _flyTo = slotLocal;
    _flyT = 0f;
    _flyDur = 0.35f;
    _flyLift = 0.55f;
    _flying = true;
  }

  // Wrong path: the extra ball leaves the basket and returns home (gentle).
  public void BeginReturnHome() {
    State = BallState.Carried; // re-uses the flight while it travels
    _placing = false;
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
    transform.localPosition = slotLocal;
  }

  public void MarkRemoved() {
    State = BallState.Removed;
    if (_collider != null) _collider.enabled = false;
  }

  void Update() {
    if (_flying) { TickFlight(); return; }
    // Carried: smooth follow of the hand (a held ball, never a floating prop).
    if (State == BallState.Carried && _hand != null) {
      Vector3 want = _hand.position + Vector3.up * 0.02f;
      transform.position = Vector3.Lerp(transform.position, want,
        1f - Mathf.Exp(-16f * Time.deltaTime));
      transform.rotation = Quaternion.Slerp(transform.rotation, _hand.rotation,
        1f - Mathf.Exp(-10f * Time.deltaTime));
    }
    // Small landing bounce after a place/return.
    if (_bounceT < 1f) {
      _bounceT = Mathf.Min(1f, _bounceT + Time.deltaTime / 0.3f);
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

  void TickFlight() {
    try {
      _flyT += Time.deltaTime;
      float t = Mathf.Clamp01(_flyT / _flyDur);
      Vector3 mid = (_flyFrom + _flyTo) * 0.5f + new Vector3(0f, _flyLift, 0f);
      transform.localPosition = Vector3.Lerp(
        Vector3.Lerp(_flyFrom, mid, t), Vector3.Lerp(mid, _flyTo, t), t);
      if (t >= 1f) {
        _flying = false;
        _bounceT = 0f;
        if (_placing) _placing = false;
        if (_returning) {
          // Landed back home: clickable again (the correction is done).
          _returning = false;
          State = BallState.Grounded;
          if (_collider != null) _collider.enabled = true;
        }
      }
    } catch (Exception) { _flying = false; }
  }
}

// The basket's door: click it (walk-up arrival) OR simply carry a ball close to
// it — both route to the same place call (state-guarded, spam-safe).
[DisallowMultipleComponent]
public class BasketZone : MonoBehaviour, IClickTarget {
  public CountingGame Game;
  public float placeRadius = 1.5f;
  Transform _player;

  public void Bind(CountingGame game, Transform player) {
    Game = game;
    _player = player;
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
  ActivityLifecycle _life;
  IAudioDirector _audio;
  readonly List<CountingBall> _balls = new List<CountingBall>();
  // Placement order (the first Target balls stay; extras go home on a wrong path).
  readonly List<CountingBall> _inBasket = new List<CountingBall>();

  // Wrong-path beat (timed, deterministic; no coroutines so tests can tick it).
  int _wrongStep = -1;
  float _wrongT;
  CountingBall _extra;

  public bool IntroDone { get { return Current != Phase.Intro; } }
  public int BallCount { get { return _balls.Count; } }
  public CountingBall BallAt(int i) { return i >= 0 && i < _balls.Count ? _balls[i] : null; }

  public void Build(CountingDemo demo, CountingPlayBuilder builder, Transform player,
      Transform hand, ActivityLifecycle life, IAudioDirector audio) {
    _demo = demo;
    _builder = builder;
    _player = player;
    _hand = hand != null ? hand : player;
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
    if (_demo != null) {
      _demo.ObserveTarget = _player;
      _demo.OnIntroCompleted = OnIntroCompleted;
    }
    // Re-entry policy: a completed activity adopts its finished visual without
    // replaying the intro (the lifecycle lives in MathScene, so it survives).
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
    if (Carried != null) return;             // one ball at a time
    Carried = ball;
    ball.BeginCarry();
  }

  public void TryPlace() {
    if (Carried == null) return;
    if (Current != Phase.FreePlay && Current != Phase.Success) return;
    CountingBall ball = Carried;
    Carried = null;
    Count++;
    _inBasket.Add(ball);
    if (Count <= Target) {
      ball.BeginPlace(BasketSlot(Count - 1));
      SetPip(Count);
      if (Count == 1) {
        if (_demo != null) {
          _demo.TeacherSay("One ball.", "Một quả bóng.");
          _demo.PointTeacherAt(BasketWorld(), 1.6f);
        }
      } else {
        Success();
      }
    } else {
      // WRONG path: the child placed one too many — a counting lesson, never a
      // punishment. The extra ball goes back home after the teacher explains.
      _extra = ball;
      ball.BeginPlace(BasketSlot(Target)); // visibly lands in the basket first
      Current = Phase.Wrong;
      _wrongStep = 0;
      _wrongT = 0f;
      WrongCount++;
    }
  }

  // The goal is met (2 in the basket): confirm it, but keep the field open —
  // if the child adds a 3rd ball the gentle counting correction runs instead.
  void Success() {
    Current = Phase.Success;
    if (_builder != null && _builder.Activity != null && _builder.Activity.Result != null) {
      GameObject result = _builder.Activity.Result;
      result.SetActive(true);
      result.transform.localScale = Vector3.one * 0.65f;
      _resultPopT = 0f;
    }
    if (_demo != null) {
      _demo.TeacherSay("Two balls!", "Hai quả bóng!", SpeechStyle.Excited, AudioPriority.P4_Feedback);
      _demo.PointTeacherAt(BasketWorld(), 2.0f);
      _demo.CelebrateBoth();
    }
    MarkLifeCompleted();
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
    if (_builder == null || _builder.Activity == null) return;
    List<GameObject> balls = _builder.Activity.Balls;
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
    if (_demo != null) {
      _demo.ObserveTarget = _player;
      _demo.OnIntroCompleted = null;   // never replay the intro on re-entry
      _demo.AudienceGateEnabled = false; // nor re-teach / re-frame the camera
      _demo.SkipToObserving();         // actors start as observers, not teachers
    }
    // Result LAST: SkipToObserving resets the actors (and hides the result).
    if (_builder.Activity.Result != null) _builder.Activity.Result.SetActive(true);
    try { Debug.Log("[CountingGame] adopted COMPLETED state (" + reason + ").", this); }
    catch (Exception) { }
  }

  // ---- wrong path beats (deterministic timer) ------------------------------------

  void Update() {
    TickResultPop(Time.deltaTime);
    if (Current != Phase.Wrong) return;
    TickWrong(Time.deltaTime);
  }

  void TickWrong(float dt) {
    if (Current != Phase.Wrong) return;
    _wrongT += dt;
    if (_demo == null) { FinishWrong(); return; }
    switch (_wrongStep) {
      case 0:
        if (_wrongT >= 0.4f) {
          _wrongStep = 1;
          _demo.TeacherSay("Let's count again!", "Cùng đếm lại nhé!");
          _demo.PointTeacherAt(BasketWorld(), 2.2f);
        }
        break;
      case 1:
        if (_wrongT >= 2.6f) { _wrongStep = 2; _demo.TeacherSay("One ball.", "Một quả bóng."); }
        break;
      case 2:
        if (_wrongT >= 4.0f) { _wrongStep = 3; _demo.TeacherSay("Two balls.", "Hai quả bóng."); }
        break;
      case 3:
        if (_wrongT >= 5.4f) {
          _wrongStep = 4;
          _demo.TeacherSay("Three balls.", "Ba quả bóng.");
          _demo.PointTeacherAt(BoardWorld(), 2.4f);
        }
        break;
      case 4:
        if (_wrongT >= 7.4f) { _wrongStep = 5; _demo.TeacherSay("The board says two.", "Bảng ghi số hai."); }
        break;
      case 5:
        if (_wrongT >= 9.2f) {
          _wrongStep = 6;
          _demo.TeacherSay("We only need two.", "Mình chỉ cần hai.");
          if (_extra != null) _extra.BeginReturnHome();
        }
        break;
      case 6:
        if (_wrongT >= 11.0f) FinishWrong();
        break;
    }
  }

  void FinishWrong() {
    _extra = null;
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
  // journey shot showed them perched on the rim like decorations.
  Vector3 BasketSlot(int i) {
    Vector3 b = _builder != null && _builder.Activity != null && _builder.Activity.Basket != null
      ? _builder.Activity.Basket.localPosition : Vector3.zero;
    return i <= 0
      ? new Vector3(b.x - 0.20f, 0.52f, b.z + 0.10f)
      : new Vector3(b.x + 0.22f, 0.52f, b.z - 0.08f);
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
    }
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
