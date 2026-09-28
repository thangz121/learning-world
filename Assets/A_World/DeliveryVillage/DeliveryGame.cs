// A_World/DeliveryVillage/DeliveryGame.cs — S3-P2Z15 GAMEPLAY #5
// "GIAO HÀNG ĐÚNG SỐ" (deliver the apples). The Delivery Village activity,
// staged in its OWN lazy scene (DeliveryScene), reached from the Math Hub's
// delivery_village gate.
// The experience: the teacher reads the order at the board (Mia needs N
// apples) -> the child student fetches the apples one by one and hands them
// over while the teacher counts 1..N -> handover ("Now it's your turn!") ->
// the CHILD delivers: one apple = one count; giving past the order is
// guidance, never failure, and stopping short earns a gentle "how many more"
// nudge, never a fail.
// One stall (MAXIMUM = 9), one mechanic, many orders: the target only decides
// how many apples the order needs (progression owned by the area: 4 -> 5 -> 7
// -> 9 -> 1 -> 3). No confetti on success: glow + sound + NPC reaction only
// (same discipline as #2-#4).
// Architecture: scene-local components, no manager/singleton, no new
// bus/service; reuses LessonActors (shared body kit), PacedVoice (paced
// speech), DemoJuice (FX), SmartCamera beats, ActivityLifecycle (owned by the
// Math-side area), MicroWorldPortal (the door). C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

// One real apple: Available -> Picked -> Carried -> Delivered. Delivery is a
// person-to-person handover: the apple flies to the receiver's hands, she
// reacts, and only then does it arc into the crate slot. A delivered apple can
// never be re-picked or counted twice.
[DisallowMultipleComponent]
public class DeliveryItem : MonoBehaviour, IClickTarget {
  public enum ItemState { Available, Picked, Carried, Delivered, Removed }
  // Pioneer content is APPLES only (brief §14). The kind is the receiver's
  // accept contract: anything else is refused gently (the wrong-item guard).
  public enum ItemKind { Apple, Other }

  public ItemState State { get; private set; } = ItemState.Available;
  public ItemKind Kind = ItemKind.Apple;
  public Vector3 HomeLocal;
  public int CrateIndex { get; private set; } = -1;
  public DeliveryGame Game;
  // Fired the moment the apple reaches the receiver's hands (the real handover
  // moment): the game hangs the thanks + count + react beats on it.
  public Action OnDelivered;
  // Fired when the apple settles into its crate slot (visual bookkeeping).
  public Action OnParked;

  Transform _hand;
  bool _picking;
  float _pickT, _pickDelay, _pickDur;
  Vector3 _pickFrom;
  bool _delivering, _deliverActive;
  float _deliverT, _deliverDelay, _deliverDur;
  Vector3 _handToLocal, _slotLocal;
  bool _hasSlot;
  bool _slotLeg;
  float _slotT, _slotDur;
  bool _returning;
  Vector3 _flyFrom, _flyTo;
  float _flyT, _flyDur, _flyLift;
  bool _flying;
  float _bounceT = 1f;
  Vector3 _baseScale = Vector3.one;
  Collider _collider;

  public bool IsAvailable { get { return State == ItemState.Available; } }
  public bool IsFlying { get { return _flying || _slotLeg || _delivering; } }
  public bool Accepts { get { return Kind == ItemKind.Apple; } }

  public void Bind(DeliveryGame game, Transform hand) {
    Game = game;
    _hand = hand;
    _baseScale = transform.localScale;
    _collider = GetComponent<Collider>();
  }

  public void SetHand(Transform hand) { _hand = hand; }

  public void OnClicked() {
    if (State != ItemState.Available || Game == null) return;
    Game.TryPick(this);
  }

  // Pickup: the child bends (player PickUp clip); once the hand is down, the
  // apple arcs up into it and rides the fist. No ground->hand snap.
  public void BeginCarry(float delay = 0f) {
    if (State != ItemState.Available) return;
    State = ItemState.Picked;
    if (_collider != null) _collider.enabled = false;
    _flying = false;
    _returning = false;
    _delivering = false;
    _deliverActive = false;
    _slotLeg = false;
    _picking = true;
    _pickT = 0f;
    _pickDelay = Mathf.Max(0f, delay);
    _pickDur = 0.42f;
    _pickFrom = transform.localPosition;
  }

  // Delivery: the apple keeps riding the fist while the child reaches toward
  // the receiver, then flies to her HANDS (leg 1). On arrival the game reacts
  // (thanks + count), then the apple arcs into the crate slot (leg 2).
  public void BeginDeliver(Vector3 handLocal, Vector3 slotLocal, int crateIndex, float delay = 0f) {
    if (State != ItemState.Carried) return;
    CrateIndex = crateIndex;
    _picking = false;
    _delivering = true;
    _deliverActive = false;
    _slotLeg = false;
    _deliverT = 0f;
    _deliverDelay = Mathf.Max(0f, delay);
    _deliverDur = 0.4f;
    _handToLocal = handLocal;
    _slotLocal = slotLocal;
    _hasSlot = true;
  }

  // Correction path: the extra apple leaves the crate/hand and returns home.
  public void BeginReturnHome() {
    State = ItemState.Carried; // re-uses the flight while it travels
    CrateIndex = -1;
    _picking = false;
    _delivering = false;
    _deliverActive = false;
    _slotLeg = false;
    _hasSlot = false;
    _returning = true;
    _flyFrom = transform.localPosition;
    _flyTo = HomeLocal;
    _flyT = 0f;
    _flyDur = 0.6f;
    _flyLift = 0.8f;
    _flying = true;
  }

  public void ParkInCrate(int crateIndex, Vector3 slotLocal) {
    State = ItemState.Delivered;
    CrateIndex = crateIndex;
    _flying = false;
    _picking = false;
    _delivering = false;
    _deliverActive = false;
    _slotLeg = false;
    _hasSlot = false;
    transform.localPosition = slotLocal;
    gameObject.SetActive(true);
    if (_collider != null) _collider.enabled = false;
  }

  public void MarkRemoved() {
    State = ItemState.Removed;
    if (_collider != null) _collider.enabled = false;
  }

  public void ResetHome() {
    State = ItemState.Available;
    CrateIndex = -1;
    _flying = false;
    _picking = false;
    _delivering = false;
    _deliverActive = false;
    _slotLeg = false;
    _hasSlot = false;
    _returning = false;
    transform.localPosition = HomeLocal;
    transform.localRotation = Quaternion.identity;
    transform.localScale = _baseScale;
    gameObject.SetActive(true);
    if (_collider != null) _collider.enabled = true;
  }

  // Deterministic tick — owned by DeliveryGame.Tick (single owner, no self
  // Update, so live play and EditMode advance flights exactly once).
  public void TickForTests(float dt) {
    try {
      if (_picking) { TickPick(dt); return; }
      if (_slotLeg) { TickSlotLeg(dt); return; }
      if (_delivering) { TickDeliver(dt); return; }
      if (_flying) { TickFlight(dt); return; }
      if (State == ItemState.Carried && _hand != null) FollowHand(dt);
      TickBounceAndPulse(dt);
    } catch (Exception) {
      _picking = false;
      _delivering = false;
      _slotLeg = false;
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

  void TickDeliver(float dt) {
    _deliverT += dt;
    if (_deliverT < _deliverDelay) {
      if (_hand != null) FollowHand(dt);
      return;
    }
    if (!_deliverActive) {
      _deliverActive = true;
      _flyFrom = transform.localPosition;
      _flyTo = _handToLocal;
      _flyT = 0f;
      _flyDur = _deliverDur;
      _flyLift = 0.22f;
      _flying = true;
    }
    TickFlight(dt);
  }

  void TickSlotLeg(float dt) {
    _slotT += dt;
    float t = Mathf.Clamp01(_slotT / _slotDur);
    Vector3 mid = (_flyFrom + _slotLocal) * 0.5f + new Vector3(0f, 0.3f, 0f);
    transform.localPosition = Vector3.Lerp(
      Vector3.Lerp(_flyFrom, mid, t), Vector3.Lerp(mid, _slotLocal, t), t);
    if (t < 1f) return;
    _slotLeg = false;
    transform.localPosition = _slotLocal;
    transform.localRotation = Quaternion.identity;
    if (OnParked != null) {
      try { OnParked(); } catch (Exception) { }
    }
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
    if (_returning) {
      _returning = false;
      State = ItemState.Available;
      CrateIndex = -1;
      if (_collider != null) _collider.enabled = true;
      return;
    }
    bool handoverDone = _deliverActive;
    _deliverActive = false;
    if (handoverDone) {
      // The receiver has the apple NOW (the real moment): react + count, then
      // the visual leg into the crate runs on.
      State = ItemState.Delivered;
      _delivering = false;
      if (_hasSlot) {
        _slotLeg = true;
        _slotT = 0f;
        _slotDur = 0.32f;
        _flyFrom = transform.localPosition;
      }
      if (OnDelivered != null) {
        try { OnDelivered(); } catch (Exception) { }
      }
    }
  }

  void TickBounceAndPulse(float dt) {
    if (_bounceT < 1f) {
      _bounceT = Mathf.Min(1f, _bounceT + dt / 0.3f);
      float s = 1f + 0.14f * Mathf.Sin(Mathf.PI * _bounceT);
      transform.localScale = _baseScale * s;
      if (_bounceT >= 1f) transform.localScale = _baseScale;
    }
    if (State == ItemState.Available && Game != null && Game.PlayerNear(transform.position, 2.4f)) {
      float s = 1f + 0.06f * Mathf.Sin(Time.time * 4f);
      transform.localScale = _baseScale * s;
    } else if (State == ItemState.Available && _bounceT >= 1f) {
      transform.localScale = _baseScale;
    }
  }
}

// The receiver's door: clicking it walks the child up to the booth and hands
// the apple over; simply carrying an apple close to Mia (and stopping) does
// the same — one call, state-guarded, spam-safe. Proximity is polled by
// DeliveryGame.Tick (single owner).
[DisallowMultipleComponent]
public class DeliveryZone : MonoBehaviour, IClickTarget {
  public DeliveryGame Game;
  public float deliverRadius = 1.7f;

  public void Bind(DeliveryGame game) {
    Game = game;
    // Unconditional (same lesson as #1-#4: the builder strips colliders for
    // bake safety with deferred Destroy, so a null-check would leave the
    // booth click-less).
    BoxCollider box = gameObject.AddComponent<BoxCollider>();
    box.size = new Vector3(1.7f, 0.8f, 1.7f);
    box.center = new Vector3(0f, 0.4f, 0f);
  }

  public void OnClicked() {
    if (Game != null) Game.TryDeliver();
  }
}

[DisallowMultipleComponent]
public class DeliveryGame : MonoBehaviour {
  public enum Phase {
    Wait,      // the child walks to the marked play spot; nothing is taught yet
    Intro,     // teacher reads the order (Mia needs N apples)
    Delivering,// the child picks, carries and hands over; the teacher counts
    Success,   // the order is complete — celebrated, field stays open
    Correct,   // one too many: a gentle counting correction, then Success
  }

  // Beat timings (one place; deterministic for Tick tests).
  const float OvershootNagCooldown = 5f;
  const float UnderNudgeCooldown = 8f;
  const float UnderDwell = 2.0f;         // settled-below-order before a nudge
  const float UnderNearXZ = 5.0f;        // nudge only near the work
  // S3-P2Z19 (user round): the guidance waits for the area's arrival reveal,
  // then asks the child onto the marked spot; the question is read ONLY there.
  const float WaitCallSeconds = 2.4f;
  public static readonly Vector3 FollowOffset = DeliveryBuilder.FollowOffset;

  // Number words: Vietnamese first; English prepared alongside. Every composed
  // line stays inside the SafetyFilter NPC cap (<= 6 tokens).
  static readonly string[] NumEn = {
    "one", "two", "three", "four", "five", "six", "seven", "eight", "nine" };
  static readonly string[] NumVi = {
    "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
  static readonly string[] AppleEn = {
    "One apple.", "Two apples.", "Three apples.", "Four apples.", "Five apples.",
    "Six apples.", "Seven apples.", "Eight apples.", "Nine apples." };
  static readonly string[] AppleVi = {
    "Một quả táo.", "Hai quả táo.", "Ba quả táo.", "Bốn quả táo.", "Năm quả táo.",
    "Sáu quả táo.", "Bảy quả táo.", "Tám quả táo.", "Chín quả táo." };

  static int ClampN(int i) { return Mathf.Clamp(i, 1, DeliveryBuilder.MaxTarget); }
  static string N(int i) { return NumEn[ClampN(i) - 1]; }
  static string Nvi(int i) { return NumVi[ClampN(i) - 1]; }
  static string Apples(int n) { return ClampN(n) == 1 ? "apple" : "apples"; }
  static string Cap(string s) {
    return string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
  }

  public Phase Current { get; private set; } = Phase.Wait;
  public int Target { get; private set; } = DeliveryBuilder.Target;
  public int Count { get; private set; }
  public DeliveryItem Carried { get; private set; }
  public int Overshoots { get; private set; }
  public int UndershootNudges { get; private set; }
  public int WrongItemRefusals { get; private set; }
  public bool IntroDone { get { return Current != Phase.Wait && Current != Phase.Intro; } }
  public bool ResultShown { get { return _result != null && _result.activeSelf; } }
  public bool ExitCueShown { get { return _exitCue != null && _exitCue.activeSelf; } }
  public int ItemCountTotal { get { return _items.Count; } }
  public DeliveryItem ItemAt(int i) { return i >= 0 && i < _items.Count ? _items[i] : null; }
  public DeliveryItem DeliveredAt(int i) { return i >= 0 && i < _delivered.Count ? _delivered[i] : null; }
  // The crate as it really stands: index 0..Count-1 are the delivered apples.
  public int CrateCount { get { return Count; } }

  DeliveryBuilder _builder;
  Transform _root;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PlayerVisual _viz;
  ClickToMove _mover;
  Transform _playerHand;
  Action<int> _onCompleted;
  string _receiverVoiceId;

  LessonActor _teacher;
  LessonActor _student;
  LessonActor _receiver;
  PacedVoice _voice;
  PacedVoice _receiverVoice;
  Transform _fx;

  GameObject _board;
  GameObject _result;
  GameObject _exitCue;
  Transform _deliveryAnchor;
  Transform _camTeaching, _lookTeaching, _camDemo, _lookDemo, _camSuccess, _lookSuccess;

  readonly List<DeliveryItem> _items = new List<DeliveryItem>();
  readonly List<DeliveryItem> _delivered = new List<DeliveryItem>();

  public float PickDelay = 0.45f;
  public float DeliverDelay = 0.45f;

  float _phaseT;
  float _shotT = -1f;
  bool _shotIssued;
  bool _cameraDone;
  bool _followHanded;
  int _shot;

  bool _saidBoard, _saidOrder;
  bool _waitCalled;      // the walk-in guidance was spoken
  float _ringPulseT;
  float _victoryT = -1f;
  float _resultPopT = 1f;
  float _boardPulseT;
  Vector3 _lastPos;
  readonly Queue<string> _recapEn = new Queue<string>();
  readonly Queue<string> _recapVi = new Queue<string>();

  // Correction beats (timed, deterministic; no coroutines so tests can tick).
  int _correctStep = -1;
  float _correctT;
  DeliveryItem _extra;
  int _landBeats; // what the NEXT handover means (count line vs success)
  float _overshootNagT;
  float _underCooldownT;
  float _underT;
  float _wrongCooldownT;
  bool _saidExit;

  // Receiver life: notice/face tracking + a receive gesture window.
  float _receiveT;
  float _breatheT;
  Vector3 _receiverVisualBase = Vector3.one;

  int _sparkleSeed = 1700;
  DeliveryItem _pulsing;
  float _pulseT;

  public void Build(DeliveryBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life, int target,
      Action<int> onCompleted = null, string receiverVoiceId = null,
      Transform hand = null) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _onCompleted = onCompleted;
    _receiverVoiceId = receiverVoiceId;
    Target = DeliveryBuilder.ClampTarget(target <= 0 ? DeliveryBuilder.Target : target);
    _viz = player != null ? player.GetComponent<PlayerVisual>() : null;
    _mover = player != null ? player.GetComponent<ClickToMove>() : null;
    // The carried apple rides the child's REAL fist bone (same lesson as #4):
    // the installer passes PlayerVisual.HandBone; fallbacks keep tests safe.
    _playerHand = hand;
    if (_playerHand == null && _viz != null && _viz.HandBone != null) _playerHand = _viz.HandBone;
    if (_playerHand == null) _playerHand = player;
    if (_builder == null) {
      Debug.LogWarning("[DeliveryGame] no builder; activity parked.", this);
      return;
    }
    _root = _builder.transform;
    _board = _builder.NumberBoard;
    _result = _builder.Result;
    _exitCue = _builder.ExitCue;
    _deliveryAnchor = _builder.DeliveryAnchor;
    _camTeaching = _builder.CamTeaching;
    _lookTeaching = _builder.LookTeaching;
    _camDemo = _builder.CamDemo;
    _lookDemo = _builder.LookDemo;
    _camSuccess = _builder.CamSuccess;
    _lookSuccess = _builder.LookSuccess;

    BuildActors();
    GameObject fx = new GameObject("DVFx");
    fx.transform.SetParent(_root, false);
    _fx = fx.transform;

    // Items become real, clickable game objects (collider re-added: the
    // builder strips it for bake safety, clicks need it).
    _items.Clear();
    List<GameObject> apples = _builder.Apples;
    for (int i = 0; i < apples.Count; i++) {
      GameObject go = apples[i];
      if (go == null) continue;
      // UNCONDITIONAL (same lesson as #1-#4: StripCollider uses deferred
      // Destroy at runtime, so a null-check would leave items click-less).
      SphereCollider sc = go.AddComponent<SphereCollider>();
      sc.radius = 0.5f;
      sc.center = new Vector3(0f, 0.15f, 0f);
      DeliveryItem item = go.GetComponent<DeliveryItem>();
      if (item == null) item = go.AddComponent<DeliveryItem>();
      item.Kind = DeliveryItem.ItemKind.Apple;
      item.HomeLocal = _builder.AppleHomes != null && i < _builder.AppleHomes.Length
        ? _builder.AppleHomes[i] : go.transform.localPosition;
      item.Bind(this, _playerHand);
      _items.Add(item);
    }
    if (_deliveryAnchor != null) {
      DeliveryZone zone = _deliveryAnchor.gameObject.GetComponent<DeliveryZone>();
      if (zone == null) zone = _deliveryAnchor.gameObject.AddComponent<DeliveryZone>();
      zone.Bind(this);
    }

    // Re-entry policy FIRST (same lesson as #1-#4): a completed activity
    // adopts the finished picture without replaying the lesson, and the shared
    // lifecycle lives in MathScene so it survives this scene's unload.
    if (_life != null && _life.State == ActivityState.Completed) {
      ApplyCompletedState("adopt");
      return;
    }
    if (_life != null) {
      try {
        _life.MarkAvailable("deliver_apples staged");
        _life.BeginEnter("delivery village built");
        _life.MarkReady("intro staged");
      } catch (Exception) { }
    }
    _lastPos = _player != null ? _player.position : Vector3.zero;
    try { Debug.Log("[DeliveryGame] activity staged (intro will play).", this); } catch (Exception) { }
  }

  void BuildActors() {
    _teacher = LessonActors.Build(_root, "DVTeacher", "NpcVisuals/TessVisual", 0.5f,
      DeliveryBuilder.TeacherStart, new Color(0.25f, 0.45f, 0.85f),
      new Color(0.98f, 0.78f, 0.25f));
    _student = LessonActors.Build(_root, "DVStudent", "NpcVisuals/MiloVisual", 0.42f,
      DeliveryBuilder.StudentStart, new Color(0.30f, 0.62f, 0.45f),
      new Color(0.55f, 0.35f, 0.20f));
    // The RECEIVER: Mia herself (market identity — coral-pink vest + hat).
    _receiver = LessonActors.Build(_root, "DVMia", "NpcVisuals/MiaVisual", 0.5f,
      DeliveryBuilder.MiaStart, new Color(0.95f, 0.45f, 0.40f),
      new Color(0.95f, 0.45f, 0.40f));
    _voice = new PacedVoice();
    _voice.Audio = _audio;
    _voice.LogTag = "Delivery";
    // The receiver's own voice (Mia's roster profile, passed by the installer
    // so gameplay never reaches into Content), P4 so her thanks never cuts the
    // teacher's count line. Newest-wins per voice, same PacedVoice.
    _receiverVoice = new PacedVoice();
    _receiverVoice.Audio = _audio;
    _receiverVoice.LogTag = "DeliveryMia";
    if (!string.IsNullOrEmpty(_receiverVoiceId))
      _receiverVoice.Voice = new VoiceProfileId(_receiverVoiceId);
    if (_teacher != null && _teacher.Root != null) FaceSnap(_teacher, BoardLocal());
    if (_student != null && _student.Root != null) FaceSnap(_student, TeacherLocal());
    if (_receiver != null && _receiver.Root != null) {
      FaceSnap(_receiver, new Vector3(0f, 0f, -1f)); // toward the lane
      _receiverVisualBase = _receiver.Visual != null ? _receiver.Visual.localScale : Vector3.one;
    }
    if (_builder != null && _builder.Result != null) _builder.Result.SetActive(false);
    if (_builder != null && _builder.ExitCue != null) _builder.ExitCue.SetActive(false);
  }

  // Terminal adopt: the finished picture (crate holds the order, Mia observing
  // at the booth, result up, exit cue lit) — never a replay, field stays open.
  void ApplyCompletedState(string reason) {
    Current = Phase.Success;
    _followHanded = true;
    _cameraDone = true;
    _landBeats = 0;
    _victoryT = -1f;
    if (_teacher != null && _teacher.Root != null) {
      _teacher.Root.transform.localPosition = DeliveryBuilder.TeacherStart;
      FaceSnap(_teacher, ReceiverLocal());
    }
    if (_student != null && _student.Root != null) {
      _student.Root.transform.localPosition = DeliveryBuilder.StudentReturn;
      FaceSnap(_student, ReceiverLocal());
    }
    if (_receiver != null && _receiver.Root != null) {
      _receiver.Root.transform.localPosition = DeliveryBuilder.MiaStart;
      FaceSnap(_receiver, new Vector3(0f, 0f, -1f));
    }
    _delivered.Clear();
    for (int i = 0; i < _items.Count; i++) {
      DeliveryItem b = _items[i];
      if (b == null) continue;
      b.SetHand(_playerHand);
      if (i < Target) {
        b.ParkInCrate(i, DeliveryBuilder.CrateSlot(i, DeliveryBuilder.BoothCounter));
        _delivered.Add(b);
      } else {
        b.MarkRemoved();
      }
    }
    Count = Target;
    if (_result != null) _result.SetActive(true);
    if (_exitCue != null) _exitCue.SetActive(true); // the way home is lit
    if (_player != null) _lastPos = _player.position;
    try { Debug.Log("[DeliveryGame] adopted COMPLETED state (" + reason + ").", this); } catch (Exception) { }
  }

  void Update() { Tick(Time.deltaTime); }

  // Deterministic tick (EditMode cover: no live frame needed). Owns the item
  // flights too (single owner — items have no self Update).
  public void Tick(float dt) {
    if (dt <= 0f || _builder == null) return;
    try {
      TickVoice(dt);
      for (int i = 0; i < _items.Count; i++) {
        if (_items[i] != null) _items[i].TickForTests(dt);
      }
      switch (Current) {
        case Phase.Wait: TickWait(dt); break;
        case Phase.Intro: TickIntro(dt); break;
        case Phase.Delivering: TickDelivering(dt); break;
        case Phase.Success: TickSuccess(dt); break;
        case Phase.Correct: TickCorrect(dt); break;
      }
      TickActing(dt);
      TickCamera(dt);
      TickJuice(dt);
      TickReceiver(dt);
    } catch (Exception) { }
  }

  void TickVoice(float dt) {
    if (_voice != null) _voice.Tick(dt);
    if (_receiverVoice != null) _receiverVoice.Tick(dt);
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
    return _root != null ? _root.TransformPoint(DeliveryBuilder.PlaySpotLocal) : Vector3.zero;
  }

  bool PlayerOnSpot() {
    if (_player == null) return false;
    Vector3 p = _player.position;
    Vector3 q = PlaySpotWorld();
    float dx = p.x - q.x, dz = p.z - q.z;
    return dx * dx + dz * dz <= DeliveryBuilder.PlaySpotRadius * DeliveryBuilder.PlaySpotRadius;
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
    Log("question read (child on the play spot): order " + Target);
  }

  void PulsePlayRing(float dt) {
    if (_builder == null || _builder.PlayRing == null) return;
    _ringPulseT += dt;
    float s = 1f + 0.07f * Mathf.Sin(_ringPulseT * 3.4f);
    try { _builder.PlayRing.transform.localScale = new Vector3(2.5f * s, 0.01f, 2.5f * s); }
    catch (Exception) { }
  }

  // ---- teacher intro (MINIMAL — S3-P2Z20 user: "đừng hướng dẫn tận răng") ----
  // The board shows the order; the teacher only asks the child to look and
  // states the job. Two short lines, then the child works it out.

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
    if (!_saidOrder && t >= 2.2f) {
      _saidOrder = true;
      // S3-P2Z23 (user: "không nói đây là số mấy, trẻ tự nhận biết"): the order
      // count is not spoken — the board shows it.
      Say("Deliver to Mia!", "Giao cho Mia nhé!");
      Point(_teacher, BoardWorld(), 2.2f);
      PulseBoard(1.4f);
    }
    if (t >= 3.6f) StartDelivering();
  }

  void StartDelivering() {
    To(Phase.Delivering);
    Follow();
    _followHanded = true;
    if (_life != null) { try { _life.Begin("question read"); } catch (Exception) { } }
    _lastPos = _player != null ? _player.position : Vector3.zero;
    try { Debug.Log("[DeliveryGame] child control (delivering phase).", this); } catch (Exception) { }
  }

  // (S3-P2Z19: the in-arena student demo + handoff beats are GONE — the garden
  // miniature teaches once; the arena reads the question on the play spot.)

  // ---- the child's delivery ------------------------------------------------------
  // Real actions in the world: click an apple (walk -> bend -> carry in the
  // fist) -> click the booth or stop beside Mia (reach -> the apple flies to
  // her hands -> she reacts and thanks -> the teacher counts).

  public void TryPick(DeliveryItem item) {
    if (item == null || !item.IsAvailable) return;
    if (Current != Phase.Delivering && Current != Phase.Success) return;
    if (Carried != null) return; // one apple at a time
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

  public void TryDeliver() {
    if (Carried == null) return;
    if (Current != Phase.Delivering && Current != Phase.Success) return;
    // The apple must really be in the hand: a booth click during the pickup
    // bend is ignored (the child clicks again; proximity re-fires on arrival).
    if (Carried.State != DeliveryItem.ItemState.Carried) return;
    DeliveryItem item = Carried;
    // The receiver accepts APPLES only (brief §15C: wrong item refused gently).
    // A refusal window keeps the proximity poll from spamming the line.
    if (!item.Accepts) {
      if (_wrongCooldownT <= 0f) {
        _wrongCooldownT = 4f;
        WrongItemRefusals++;
        // S3-P2Z33: warm "not that one" feedback.
        GameJuice.WrongFx(null, _fx, ReceiverWorld());
        ActivityFeedback.Retry();
        Say("Not an apple!", "Không phải táo!");
        Log("wrong item refused (kind=" + item.Kind + ")");
      }
      return;
    }
    Carried = null;
    if (_viz != null) {
      _viz.FaceTowards(ReceiverWorld(), true);
      _viz.PlayPickup();
    }
    if (Count < Target) {
      // The crate grows BY ONE: the slot index is decided here, once.
      int index = Count;
      Count++;
      _delivered.Add(item);
      item.OnDelivered = OnAppleDelivered;
      item.OnParked = OnAppleParked;
      item.BeginDeliver(ReceiverHandLocal(), CrateSlotLocal(index), index, DeliverDelay);
      _landBeats = (Count == Target) ? 2 : 1; // the beat waits for the real handover
    } else {
      // CORRECTION path: one too many — a counting lesson, never a punishment.
      // The extra apple reaches her hands first, then goes home after the
      // teacher explains.
      _extra = item;
      item.OnDelivered = OnExtraDelivered;
      item.BeginDeliver(ReceiverHandLocal(), CrateSlotLocal(Target), Target, DeliverDelay);
      To(Phase.Correct);
      _correctStep = 0;
      _correctT = 0f;
      Overshoots++;
      ActivityFeedback.Retry();
    }
  }

  void OnAppleDelivered() {
    ReceiveGesture();
    ReactReceiver();
    PlaySfx("give");
    SayReceiver();
    Sparkle(ReceiverWorld() + new Vector3(0f, 0.9f, 0f), 10, _sparkleSeed++, 0.5f);
    ActivityFeedback.Progress(Mathf.Min(Count, Target), Target);
    if (_landBeats == 1) {
      _landBeats = 0;
      int k = Mathf.Min(Count, Target);
      Say(AppleEn[k - 1], AppleVi[k - 1]);
      Point(_teacher, ReceiverWorld(), 1.6f);
    } else if (_landBeats == 2) {
      _landBeats = 0;
      if (Current != Phase.Delivering) return;
      SuccessBeats();
    }
  }

  void OnAppleParked() { /* visual bookkeeping only — the beat already played */ }

  // The overshoot apple also reaches her hands (then waits for the correction
  // before going home).
  void OnExtraDelivered() {
    ReceiveGesture();
    ReactReceiver();
    PlaySfx("give");
  }

  // The receiver's line, paced on HER voice (never cuts the teacher's count).
  void SayReceiver() {
    if (_receiverVoice == null) return;
    try {
      _receiverVoice.Speak(DialogueLang.T("Thank you!", "Cảm ơn con!"),
        SpeechStyle.Excited, AudioPriority.P4_Feedback);
    } catch (Exception) { }
  }

  void SuccessBeats() {
    To(Phase.Success);
    PlaySfx("success");
    Sparkle(ReceiverWorld() + new Vector3(0f, 1.0f, 0f), 14, _sparkleSeed++, 0.8f);
    // S3-P2Z33: layered win feedback.
    GameJuice.CorrectFx(_fx, ReceiverWorld() + new Vector3(0f, 1.0f, 0f), false);
    ActivityFeedback.Correct();
    ActivityGuide.Clear();
    Say(Cap(N(Target)) + " " + Apples(Target) + "! Well done!",
      Cap(Nvi(Target)) + " quả táo! Giỏi!");
    // The recap 1..Target waits its turn in the pacer's single slot (same
    // discipline as #2-#4): one line in flight, the rest wait.
    _recapEn.Clear();
    _recapVi.Clear();
    for (int i = 1; i <= Target; i++) {
      _recapEn.Enqueue(AppleEn[i - 1]);
      _recapVi.Enqueue(AppleVi[i - 1]);
    }
    _recapEn.Enqueue(DialogueLang.T("Time to go home!", "Mình ra cổng nhé!"));
    _recapVi.Enqueue(DialogueLang.T("Time to go home!", "Mình ra cổng nhé!"));
    Point(_teacher, ReceiverWorld(), 2.0f);
    CelebrateBoth();
    CelebrateReceiver();
    if (_result != null) {
      _result.SetActive(true);
      _result.transform.localScale = Vector3.one * 0.65f;
      _resultPopT = 0f;
    }
    // Brief §22: the exit landmark lights up — the child walks out themselves.
    if (_exitCue != null) _exitCue.SetActive(true);
    SetShot(2);
    _victoryT = 1.35f;
    MarkLifeCompleted();
    if (_onCompleted != null) {
      try { _onCompleted(Target); } catch (Exception) { }
    }
  }

  void MarkLifeCompleted() {
    if (_life == null) return;
    try {
      if (_life.State != ActivityState.Completed) _life.MarkCompleted("delivered " + Target + " apples");
    } catch (Exception) { }
  }

  void TickDelivering(float dt) {
    _phaseT += dt;
    if (_overshootNagT > 0f) _overshootNagT -= dt;
    if (_wrongCooldownT > 0f) _wrongCooldownT -= dt;
    TickDeliverProximity();
    // S3-P2Z22 (user: no "còn N nữa" hint — the child thinks). The undershoot
    // nudge is GONE; the order board shows the count.
    _lastPos = _player != null ? _player.position : Vector3.zero;
    FaceTowards(_teacher, PlayerLocal(), dt, 2.2f);
    FaceTowards(_student, PlayerLocal(), dt, 2.2f);
    if (Carried != null) ActivityGuide.PointAt(ReceiverWorld()); else ActivityGuide.Clear();
  }

  // "Stop, then hand over" (same discipline as #1-#4): walking PAST the booth
  // must never fling the apple out of the hand — the child stops first.
  void TickDeliverProximity() {
    if (Carried == null || _player == null) return;
    if (_mover != null && _mover.IsMoving) return;
    if (PlayerNear(ReceiverWorld(), 1.7f)) TryDeliver();
  }

  bool NearWork() {
    return PlayerNear(StallWorld(), UnderNearXZ) || PlayerNear(ReceiverWorld(), UnderNearXZ);
  }

  bool IsPlayerMoving() {
    if (_mover != null) return _mover.IsMoving;
    if (_player == null) return false;
    Vector3 d = _player.position - _lastPos;
    d.y = 0f;
    return d.sqrMagnitude > 0.0004f;
  }

  void TickSuccess(float dt) {
    _phaseT += dt;
    TickRecap();
    if (_exitCue != null && _exitCue.activeSelf) {
      float s = 1f + 0.1f * Mathf.Sin(Time.time * 3.2f);
      try { _exitCue.transform.localScale = new Vector3(0.5f * s, 0.5f * s, 0.14f); }
      catch (Exception) { }
    }
    FaceTowards(_teacher, PlayerLocal(), dt, 2f);
    FaceTowards(_student, PlayerLocal(), dt, 2f);
    TickDeliverProximity(); // the field stays open: a spare apple feeds the lesson
    if (_phaseT >= 4.6f && !_followHanded) {
      _followHanded = true;
      _cameraDone = true;
      Follow();
    }
  }

  void TickRecap() {
    if (_recapEn.Count <= 0 || _voice == null || !_voice.Idle || _voice.HasLine) return;
    Say(_recapEn.Dequeue(), _recapVi.Dequeue());
  }

  // ---- correction beats (deterministic timer) ---------------------------------------
  // The extra apple reached her hands; the teacher recounts the crate 1..N,
  // points at the board ("the board says N"), names the limit ("N is enough"),
  // and the apple hops home. Then Success again — gently, like a clean run.

  void TickCorrect(float dt) {
    _correctT += dt;
    if (_correctStep < 0) return;
    if (_correctStep == 0 && _correctT >= 0.4f) {
      _correctStep = 1;
      Say("Let's count again!", "Cùng đếm lại nhé!");
      Point(_teacher, ReceiverWorld(), 2.2f);
      return;
    }
    if (_correctStep >= 1 && _correctStep <= Target) {
      float at = 0.4f + _correctStep * 1.7f;
      if (_correctT >= at) {
        int k = _correctStep;
        _correctStep++;
        Say(AppleEn[k - 1], AppleVi[k - 1]);
        if (k == Target) Point(_teacher, BoardWorld(), 2.4f);
      }
      return;
    }
    float afterCounts = 0.4f + (Target + 1) * 1.7f;
    if (_correctStep == Target + 1 && _correctT >= afterCounts) {
      _correctStep = Target + 2;
      Say("The board says " + N(Target) + ".", "Bảng ghi số " + Nvi(Target) + ".");
      return;
    }
    if (_correctStep == Target + 2 && _correctT >= afterCounts + 1.8f) {
      _correctStep = Target + 3;
      Say(Cap(N(Target)) + " is enough.", "Đủ " + Nvi(Target) + " quả rồi.");
      if (_extra != null) _extra.BeginReturnHome();
      return;
    }
    if (_correctStep == Target + 3 && _correctT >= afterCounts + 3.6f) {
      FinishCorrect();
    }
  }

  void FinishCorrect() {
    _extra = null;
    _landBeats = 0;
    Count = Target;
    _delivered.Clear();
    for (int i = 0; i < _items.Count; i++) {
      DeliveryItem b = _items[i];
      if (b == null) continue;
      if (b.State == DeliveryItem.ItemState.Delivered
          && b.CrateIndex >= 0 && b.CrateIndex < Target) {
        _delivered.Add(b);
      }
    }
    // Every apple beyond the order goes home, so the crate ends exactly the
    // board's count — whatever the child picked.
    for (int i = 0; i < _items.Count; i++) {
      DeliveryItem b = _items[i];
      if (b == null) continue;
      if (b.State != DeliveryItem.ItemState.Available
          && !(b.State == DeliveryItem.ItemState.Delivered && b.CrateIndex < Target)) {
        b.BeginReturnHome();
      }
    }
    if (_result != null) {
      _result.SetActive(true);
      _result.transform.localScale = Vector3.one * 0.65f;
      _resultPopT = 0f;
    }
    if (_exitCue != null) _exitCue.SetActive(true);
    Say(Cap(N(Target)) + " " + Apples(Target) + "! Well done!",
      Cap(Nvi(Target)) + " quả táo! Giỏi!");
    CelebrateBoth();
    CelebrateReceiver();
    MarkLifeCompleted();
    if (_onCompleted != null) {
      try { _onCompleted(Target); } catch (Exception) { }
    }
    To(Phase.Success);
    _followHanded = true; // the celebration frame already played; stay with follow
  }

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
    // After a correction sets _followHanded, stop re-issuing the success shot
    // (S3-P2Z17 journey finding: camera stuck in Interaction blocked the exit).
    if (_followHanded) return;
    if (!_shotIssued) {
      // Let the area's arrival reveal (2.2s) play first, then hold the
      // teaching frame while the teacher reads the order.
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
    try { Debug.Log("[DeliveryGame] camera returned to follow.", this); } catch (Exception) { }
  }

  // ---- receiver life (procedural, small: she must feel like a person) ---------------

  // Receive: both arms come forward (the shared wave bone), a nod, a happy
  // pulse — "she is taking the apple", never a trigger.
  void ReceiveGesture() {
    if (_receiver == null) return;
    _receiveT = 1.2f;
    if (_receiver.WaveBone != null) {
      _receiveT = 1.2f;
    }
    Nod(_receiver);
    if (_receiver.Face != null) _receiver.Face.PulseExpression(CharacterExpression.Happy, 2.4f);
    FaceTowardsLocal(_receiver, PlayerLocal(), 5f);
  }

  void ReactReceiver() {
    if (_receiver == null) return;
    try { if (_receiver.Face != null) _receiver.Face.PlayHop(); } catch (Exception) { }
    _receiver.SquashT = 0.3f;
  }

  void CelebrateReceiver() {
    if (_receiver == null) return;
    try { if (_receiver.Animator != null) _receiver.Animator.SetTrigger("Celebrate"); } catch (Exception) { }
    if (_receiver.Face != null) _receiver.Face.PulseExpression(CharacterExpression.Happy, 3f);
    try { _receiver.Face.PlayHop(); } catch (Exception) { }
    _receiver.SquashT = 0.35f;
  }

  void TickReceiver(float dt) {
    _breatheT += dt;
    if (_receiver == null) return;
    // Breathing (2% at ~0.4Hz) — alive while waiting for customers.
    if (_receiver.Visual != null) {
      float b = 1f + 0.02f * Mathf.Sin(_breatheT * 2.5f);
      _receiver.Visual.localScale = new Vector3(
        _receiverVisualBase.x * b, _receiverVisualBase.y, _receiverVisualBase.z * b);
    }
    // Notice: face the player when they come close (or the carried apple).
    if (_player != null) {
      Vector3 p = _player.position;
      Vector3 m = ReceiverWorld();
      float dx = p.x - m.x, dz = p.z - m.z;
      if (dx * dx + dz * dz <= 16f && Current != Phase.Intro) {
        FaceTowards(_receiver, PlayerLocal(), dt, 2.5f);
      } else if (Current != Phase.Intro) {
        FaceTowards(_receiver, new Vector3(0f, 0f, -2f), dt, 1.2f);
      }
    }
    // Receive-gesture window: hold the arm forward briefly.
    if (_receiveT > 0f) {
      _receiveT = Mathf.Max(0f, _receiveT - dt);
      if (_receiver.WaveBone != null) {
        if (!_receiver.Waving) {
          _receiver.Waving = true;
          _receiver.WaveBase = _receiver.WaveBone.localRotation;
        }
        _receiver.WaveBone.localRotation = _receiver.WaveBase * Quaternion.Euler(0f, 0f, -52f);
      }
      if (_receiveT <= 0f && _receiver.WaveBone != null) {
        _receiver.WaveBone.localRotation = _receiver.WaveBase;
        _receiver.Waving = false;
      }
    }
  }

  // ---- actor motion / gestures (same acting language as #1-#4) ----------------------

  Vector3 ReceiverHandLocal() {
    Vector3 world = ReceiverHandWorld();
    return _root != null ? _root.InverseTransformPoint(world) : world;
  }

  Vector3 ReceiverHandWorld() {
    if (_receiver != null) {
      if (_receiver.HandBone != null) return _receiver.HandBone.position + new Vector3(0f, 0.06f, 0f);
      if (_receiver.CarryAnchor != null) return _receiver.CarryAnchor.position;
      if (_receiver.Root != null) return _receiver.Root.transform.position + new Vector3(0f, 0.95f, 0f);
    }
    return ReceiverWorld() + new Vector3(0f, 0.95f, 0f);
  }

  Vector3 CrateSlotLocal(int i) {
    return DeliveryBuilder.CrateSlot(i, DeliveryBuilder.BoothCounter);
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

  void FaceTowardsLocal(LessonActor a, Vector3 targetLocal, float rate) {
    FaceTowards(a, targetLocal, 0.2f, rate);
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
    TickSquash(_receiver, dt);
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
    if (_pulseT > 0f) {
      _pulseT -= dt;
      if (_pulsing != null) {
        float k = Mathf.Clamp01(_pulseT / 0.4f);
        float s = 1f + 0.18f * Mathf.Sin(k * Mathf.PI);
        _pulsing.transform.localScale = Vector3.one * s;
      }
      if (_pulseT <= 0f && _pulsing != null) {
        _pulsing.transform.localScale = Vector3.one;
        _pulsing = null;
      }
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
    try { Debug.Log("[DeliveryGame] " + message, this); } catch (Exception) { }
  }

  // ---- helpers -------------------------------------------------------------------------------

  public bool PlayerNear(Vector3 world, float radius) {
    if (_player == null) return false;
    Vector3 p = _player.position;
    float dx = p.x - world.x, dz = p.z - world.z;
    return dx * dx + dz * dz <= radius * radius;
  }

  Vector3 LocalPoint(Vector3 world) {
    return _root != null ? _root.InverseTransformPoint(world) : world;
  }

  Vector3 BoardWorld() {
    return _builder != null && _builder.NumberBoard != null
      ? _builder.NumberBoard.transform.position : transform.position;
  }
  Vector3 BoardLocal() { return LocalPoint(BoardWorld()); }

  Vector3 StallWorld() {
    return _root != null ? _root.TransformPoint(DeliveryBuilder.StallCenter) : Vector3.zero;
  }
  Vector3 StallLocal() { return LocalPoint(StallWorld()); }

  Vector3 ReceiverWorld() {
    return _receiver != null && _receiver.Root != null
      ? _receiver.Root.transform.position
      : (_root != null ? _root.TransformPoint(DeliveryBuilder.MiaStart) : Vector3.zero);
  }
  Vector3 ReceiverLocal() { return LocalPoint(ReceiverWorld()); }

  Vector3 PlayerLocal() {
    return _player != null ? LocalPoint(_player.position) : ReceiverLocal();
  }
  Vector3 TeacherLocal() {
    return _teacher != null && _teacher.Root != null
      ? _teacher.Root.transform.localPosition + new Vector3(0f, 1.1f, 0f)
      : LocalPoint(DeliveryBuilder.TeacherStart);
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

  void OnDestroy() { ActivityGuide.Clear(); }
}
