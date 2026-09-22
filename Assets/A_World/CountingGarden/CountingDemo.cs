// A_World/CountingGarden/CountingDemo.cs — S3 P2 COUNTING DEMO PIONEER.
// ONE Number-2 living visual instruction (NOT cutscene, NOT gameplay, NOT AI):
//   NUMBER 2 -> NPC looks -> "Number two." -> NPC walks to 2 apples ->
//   "Two apples." -> picks apple 1 -> picks apple 2 -> carries (visible) ->
//   walks to basket -> places 1 -> places 2 -> result "2 tick" appears ->
//   "Two apples!" + celebrate -> tidy reset -> LOOP.
// Scene-local sequence controller (the lightweight local controller §15
// allows — NOT a manager/AI/framework): transform-only motion (walk waypoints
// with bob, yaw facing), VISIBLE arc object transfer (the same apple object
// flies pedestal->hand->basket; never teleport/disappear), existing PickUp /
// Celebrate Animator triggers, Happy face pulses, LookAt-style facing,
// SmartCamera.FrameAnchor beat when the child walks up, speech through the
// existing IAudioDirector (npc_female_01, fire-and-forget like Tess).
// Touches NOTHING gameplay: no bus, no quest, no save, no progression, no
// score, no Interactable, no collider on the host (click-through).
// The host spawns in Build (post-NavMesh-bake, installer-driven) so it never
// bakes as a phantom obstacle. C# 9.0 only.
using System;
using UnityEngine;

public enum DemoPhase {
  Ready,
  LookNumber,
  SayNumber,
  WalkApples,
  ArriveApples,
  PickOne,
  PickTwo,
  CarryShow,
  WalkBasket,
  PlaceOne,
  PlaceTwo,
  ShowResult,
  Celebrate,
  HoldResult,
  ResetBeat,
  WalkStart,
}

[DisallowMultipleComponent]
public class CountingDemo : MonoBehaviour {
  // Garden-local choreography (S3-P2V demo theatre, stage centre (0,11)):
  // NPC works the BACK row (z 10.9) behind the front-row props (z 9.9) so it
  // never occludes the number/apples/basket/result from the viewing spot.
  static readonly Vector3 StartStand = CountingGardenBuilder.DemoNpcStart;
  static readonly Vector3 AppleStand = CountingGardenBuilder.DemoAppleStand;
  static readonly Vector3 BasketStand = CountingGardenBuilder.DemoBasketStand;
  const float WalkSpeed = 0.9f; // slow enough for a 4yo to follow
  const float WatchRadius = CountingGardenBuilder.DemoViewRadius;
  const float RearmMargin = 2.0f;
  const float BeatHoldSeconds = 3.0f; // re-issued while the child stays watching

  static readonly VoiceProfileId DemoVoice = new VoiceProfileId("npc_female_01");
  static readonly LanguageCode EnUs = new LanguageCode("en-US");

  // Stage refs (pushed by Build, read never written as magic vectors).
  GameObject _number;
  Transform _basket;
  GameObject _apple0;
  GameObject _apple1;
  Vector3 _home0;
  Vector3 _home1;
  GameObject _result;
  Transform _camT;
  Transform _lookT;
  Vector3 _mouth;
  Vector3 _numberFocus = new Vector3(-2.1f, 1.1f, 9.9f); // the number board

  // Live refs (nullable; every use is null-guarded).
  Transform _playerT;
  SmartCamera _cam;
  IAudioDirector _audio;

  // Host rig (Tess identity, Math-blue vest + gold hat via the prefab).
  GameObject _host;
  Transform _visual;
  Animator _animator;
  CharacterPresentation _face;
  Transform _carryAnchor;
  bool _hostBuilt;
  bool _built;

  // Sequence state (EditMode-drives-Step seams for tests).
  DemoPhase _phase = DemoPhase.Ready;
  float _phaseT;
  public DemoPhase Phase { get { return _phase; } }
  public int LoopCount { get; private set; }
  public int DemoBeatsFired { get; private set; }
  public bool HostBuilt { get { return _hostBuilt; } }
  public bool ResultShown { get { return _result != null && _result.activeSelf; } }

  // One in-flight apple transfer (arc pedestal<->hand<->basket, same object).
  GameObject _flyApple;
  Vector3 _flyFrom;
  Vector3 _flyTo;
  float _flyT;
  float _flyDur = 1f;
  float _flyLift = 0.5f;
  float _flyDelay;
  bool _flying;
  // Carry-follow flags (apple glued to the hand anchor while carried).
  bool _carry0;
  bool _carry1;

  bool _beatLatched;
  float _beatRefreshT;
  bool _saidNumber;
  bool _saidApples;
  bool _cheered;
  bool _firedPick;
  bool _placed;
  float _walkT; // bob clock

  // Installer entry: stage refs + live refs, then spawn the host (post-bake).
  public void Build(CountingGardenBuilder b, Transform playerT, SmartCamera cam, IAudioDirector audio) {
    if (b != null) {
      _number = b.DemoNumber;
      _basket = b.DemoBasket;
      _apple0 = b.DemoApple0;
      _apple1 = b.DemoApple1;
      _home0 = b.DemoAppleHome0;
      _home1 = b.DemoAppleHome1;
      _result = b.DemoResult;
      _camT = b.DemoCam;
      _lookT = b.DemoLook;
      _mouth = b.DemoMouth;
      _built = true;
    }
    _playerT = playerT;
    _cam = cam;
    _audio = audio;
    BuildHostImmediate(b != null ? b.transform : transform);
    ResetActors();
    _phase = DemoPhase.Ready;
    _phaseT = 0f;
  }

  bool _hostAttempted;

  public void BuildHostImmediate(Transform stageParent) {
    if (_hostAttempted) return;
    _hostAttempted = true;
    try {
      GameObject prefab = Resources.Load<GameObject>("NpcVisuals/TessVisual");
      if (prefab == null) {
        Debug.LogError("[CountingDemo] Missing NpcVisuals/TessVisual; demo parks.", this);
        return;
      }
      _host = new GameObject("CGDemoHost");
      _host.transform.SetParent(stageParent, false);
      _host.transform.localPosition = StartStand;
      _host.transform.localRotation = Quaternion.identity;
      GameObject visual = Instantiate(prefab, _host.transform, false);
      visual.name = "CGDemoHostVisual";
      visual.transform.localPosition = new Vector3(0f, 0.02f, 0f);
      visual.transform.localRotation = Quaternion.identity;
      visual.transform.localScale = Vector3.one * 0.5f;
      _visual = visual.transform;
      _animator = visual.GetComponentInChildren<Animator>(true);
      SkinnedMeshRenderer skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
      Transform head = null, footL = null, footR = null;
      if (skin != null && skin.bones != null) {
        foreach (Transform bone in skin.bones) {
          if (bone == null) continue;
          if (head == null && bone.name == "Head") head = bone;
          if (footL == null && bone.name == "Foot.L") footL = bone;
          if (footR == null && bone.name == "Foot.R") footR = bone;
        }
      }
      _face = _host.AddComponent<CharacterPresentation>();
      try { _face.SetupFace(skin, head, _host.transform, _visual); } catch (Exception) { }
      try { _face.BuildFaceImmediate(); } catch (Exception) { }
      try {
        if (footL != null) _face.QueueShoe(footL, "ShoeL");
        if (footR != null) _face.QueueShoe(footR, "ShoeR");
        _face.BuildShoesImmediate();
      } catch (Exception) { }
      GameObject anchor = new GameObject("CGDemoCarryAnchor");
      anchor.transform.SetParent(_host.transform, false);
      // In FRONT of the host (local -z): the host faces the props/camera, so
      // the carried apples stay visible between the host and the child.
      anchor.transform.localPosition = new Vector3(0f, 0.95f, -0.35f);
      _carryAnchor = anchor.transform;
      // Click-through: the demo host must never eat walk clicks (no capsule,
      // no mesh colliders — the stage floor stays clickable under the NPC).
      try { CharacterPresentation.DestroyColliders(_host); } catch (Exception) { }
      _hostBuilt = true;
    } catch (Exception e) {
      try { Debug.LogError("[CountingDemo] host build failed: " + e.Message, this); }
      catch (Exception) { }
    }
  }

  void ResetActors() {
    _carry0 = false;
    _carry1 = false;
    _flying = false;
    _flyApple = null;
    try {
      if (_apple0 != null) _apple0.transform.localPosition = _home0;
      if (_apple1 != null) _apple1.transform.localPosition = _home1;
      if (_result != null) _result.SetActive(false);
      if (_number != null) _number.transform.localScale = Vector3.one;
      if (_host != null) {
        _host.transform.localPosition = StartStand;
        FaceSnap(CourtyardDir());
      }
      if (_face != null) _face.SetExpression(CharacterExpression.Neutral);
    } catch (Exception) { }
  }

  // Facing helper for the host: toward the viewing spot (the child), i.e.
  // north across the stage, so "talking" always reads on the face.
  Vector3 CourtyardDir() {
    Vector3 d = CountingGardenBuilder.DemoMouthLocal;
    if (_host != null) d = d - _host.transform.localPosition;
    d.y = 0f;
    return d.sqrMagnitude > 0.0001f ? d : new Vector3(0f, 0f, -1f);
  }

  void Update() {
    try { Step(Time.deltaTime); } catch (Exception) { }
  }

  // Time-stepped state machine (tests drive Step directly with fake dt).
  public void Step(float dt) {
    if (!_built || !_hostBuilt || dt <= 0f) return;
    WatchPlayer();
    _phaseT += dt;
    switch (_phase) {
      case DemoPhase.Ready:
        if (_phaseT >= 0.8f) To(DemoPhase.LookNumber);
        break;
      case DemoPhase.LookNumber:
        FaceTowards(_numberFocus, dt, 4f);
        if (_phaseT >= 1.2f) {
          To(DemoPhase.SayNumber);
          Speak("Number two.", SpeechStyle.Clear, AudioPriority.P2_Instruction);
        }
        break;
      case DemoPhase.SayNumber:
        FaceTowards(_numberFocus, dt, 4f);
        PulseNumber();
        if (_phaseT >= 2.2f) {
          if (_number != null) _number.transform.localScale = Vector3.one;
          To(DemoPhase.WalkApples);
        }
        break;
      case DemoPhase.WalkApples:
        if (WalkTo(AppleStand, dt)) To(DemoPhase.ArriveApples);
        break;
      case DemoPhase.ArriveApples:
        FaceTowards(AppleFocus(), dt, 5f);
        if (!_saidApples) {
          _saidApples = true;
          Speak("Two apples.", SpeechStyle.Clear, AudioPriority.P2_Instruction);
        }
        if (_phaseT >= 1.8f) To(DemoPhase.PickOne);
        break;
      case DemoPhase.PickOne:
        FaceTowards(AppleFocus(), dt, 5f);
        if (!_firedPick) {
          _firedPick = true;
          Trigger("PickUp");
          Fly(_apple0, _home0, CarrySlot(0), 0.25f, 0.9f, 0.5f);
        }
        if (_phaseT >= 1.4f) {
          _carry0 = true;
          To(DemoPhase.PickTwo);
        }
        break;
      case DemoPhase.PickTwo:
        FaceTowards(AppleFocus(), dt, 5f);
        if (!_firedPick) {
          _firedPick = true;
          Trigger("PickUp");
          Fly(_apple1, _home1, CarrySlot(1), 0.25f, 0.9f, 0.5f);
        }
        if (_phaseT >= 1.4f) {
          _carry1 = true;
          To(DemoPhase.CarryShow);
        }
        break;
      case DemoPhase.CarryShow:
        FaceTowards(ViewerPoint(), dt, 4f);
        if (!_didPulse && _phaseT >= 0.2f) {
          _didPulse = true;
          try { if (_face != null) _face.PulseExpression(CharacterExpression.Happy, 2.0f); }
          catch (Exception) { }
        }
        if (_phaseT >= 1.6f) To(DemoPhase.WalkBasket);
        break;
      case DemoPhase.WalkBasket:
        if (WalkTo(BasketStand, dt)) To(DemoPhase.PlaceOne);
        break;
      case DemoPhase.PlaceOne:
        FaceTowards(BasketPoint(), dt, 5f);
        if (!_placed) {
          _placed = true;
          _carry0 = false;
          Fly(_apple0, CarrySlot(0), BasketSlot(0), 0.1f, 0.8f, 0.4f);
        }
        if (_phaseT >= 1.1f) To(DemoPhase.PlaceTwo);
        break;
      case DemoPhase.PlaceTwo:
        FaceTowards(BasketPoint(), dt, 5f);
        if (!_placed) {
          _placed = true;
          _carry1 = false;
          Fly(_apple1, CarrySlot(1), BasketSlot(1), 0.1f, 0.8f, 0.4f);
        }
        if (_phaseT >= 1.1f) To(DemoPhase.ShowResult);
        break;
      case DemoPhase.ShowResult:
        FaceTowards(BasketPoint(), dt, 5f);
        if (_result != null && !_result.activeSelf) {
          try { _result.SetActive(true); } catch (Exception) { }
        }
        if (_phaseT >= 1.0f) {
          To(DemoPhase.Celebrate);
          Trigger("Celebrate");
          try { if (_face != null) _face.SetExpression(CharacterExpression.Happy); }
          catch (Exception) { }
          Speak("Two apples!", SpeechStyle.Excited, AudioPriority.P4_Feedback);
        }
        break;
      case DemoPhase.Celebrate:
        FaceTowards(ViewerPoint(), dt, 3f);
        if (_phaseT >= 2.8f) To(DemoPhase.HoldResult);
        break;
      case DemoPhase.HoldResult:
        if (_phaseT >= 1.5f) To(DemoPhase.ResetBeat);
        break;
      case DemoPhase.ResetBeat:
        FaceTowards(AppleFocus(), dt, 4f);
        if (_phaseT >= 0.2f && _result != null && _result.activeSelf) {
          try { _result.SetActive(false); } catch (Exception) { }
        }
        if (!_firedPick) {
          _firedPick = true;
          Fly(_apple0, BasketSlot(0), _home0, 0.2f, 0.7f, 0.45f);
        }
        if (_phaseT >= 0.9f && !_saidNumber) {
          _saidNumber = true;
          Fly(_apple1, BasketSlot(1), _home1, 0.0f, 0.7f, 0.45f);
        }
        if (_phaseT >= 2.4f) {
          try {
            if (_apple0 != null) _apple0.transform.localPosition = _home0;
            if (_apple1 != null) _apple1.transform.localPosition = _home1;
          } catch (Exception) { }
          To(DemoPhase.WalkStart);
        }
        break;
      case DemoPhase.WalkStart:
        if (WalkTo(StartStand, dt)) {
          LoopCount++;
          To(DemoPhase.Ready);
        }
        break;
    }
    TickFlight(dt);
    TickCarry();
  }

  bool _didPulse;

  void To(DemoPhase next) {
    _phase = next;
    _phaseT = 0f;
    _firedPick = false;
    _placed = false;
    _didPulse = false;
    if (next == DemoPhase.ArriveApples) _saidApples = false;
    if (next == DemoPhase.ResetBeat) { _saidNumber = false; }
    if (next == DemoPhase.Ready) {
      _saidApples = false;
      _saidNumber = false;
      try { if (_face != null) _face.SetExpression(CharacterExpression.Neutral); }
      catch (Exception) { }
    }
  }

  Vector3 AppleFocus() {
    return new Vector3((_home0.x + _home1.x) * 0.5f, 0.75f, (_home0.z + _home1.z) * 0.5f);
  }

  Vector3 ViewerPoint() {
    if (_camT != null) return _camT.localPosition;
    return _host.transform.localPosition + CourtyardDir();
  }

  Vector3 BasketPoint() {
    if (_basket != null) return _basket.localPosition;
    return BasketStand;
  }

  void PulseNumber() {
    if (_number == null) return;
    try {
      float t = Mathf.Clamp01(_phaseT / 2.2f);
      float s = 1f + 0.12f * Mathf.Sin(Mathf.PI * t);
      _number.transform.localScale = new Vector3(s, s, s);
    } catch (Exception) { }
  }

  void Trigger(string name) {
    try { if (_animator != null) _animator.SetTrigger(name); } catch (Exception) { }
  }

  void Speak(string text, SpeechStyle style, AudioPriority priority) {
    if (_audio == null) return;
    try {
      var req = new DialogueRequest(text, DemoVoice, EnUs, 1f, 1f, style,
        AudioFormat.Mp3_44100, priority);
      _audio.SpeakAsync(req); // fire-and-forget: the Director owns playback.
    } catch (Exception) { }
  }

  // Walk with yaw facing + visual bob (no teleport, no snap, no clipping step
  // larger than speed*dt). Returns true on arrival (0.15m).
  bool WalkTo(Vector3 target, float dt) {
    Vector3 p = _host.transform.localPosition;
    Vector3 flat = new Vector3(target.x - p.x, 0f, target.z - p.z);
    float dist = flat.magnitude;
    if (dist <= 0.15f) {
      try {
        if (_visual != null) {
          Vector3 v = _visual.localPosition;
          v.y = 0.02f;
          _visual.localPosition = v;
        }
      } catch (Exception) { }
      return true;
    }
    FaceTowards(target, dt, 6f);
    float step = Mathf.Min(WalkSpeed * dt, dist);
    Vector3 dir = flat / (dist > 0.0001f ? dist : 1f);
    try { _host.transform.localPosition = new Vector3(p.x + dir.x * step, 0f, p.z + dir.z * step); }
    catch (Exception) { }
    _walkT += dt;
    try {
      if (_visual != null) {
        Vector3 v = _visual.localPosition;
        v.y = 0.02f + Mathf.Abs(Mathf.Sin(_walkT * 9f)) * 0.05f;
        _visual.localPosition = v;
      }
    } catch (Exception) { }
    return false;
  }

  void FaceTowards(Vector3 targetLocal, float dt, float rate) {
    if (_host == null) return;
    try {
      Vector3 p = _host.transform.localPosition;
      Vector3 d = new Vector3(targetLocal.x - p.x, 0f, targetLocal.z - p.z);
      if (d.sqrMagnitude < 0.0001f) return;
      Quaternion want = Quaternion.LookRotation(d);
      float t = 1f - Mathf.Exp(-rate * dt);
      _host.transform.localRotation = Quaternion.Slerp(_host.transform.localRotation, want, t);
    } catch (Exception) { }
  }

  void FaceSnap(Vector3 dir) {
    if (_host == null) return;
    try {
      if (dir.sqrMagnitude < 0.0001f) return;
      _host.transform.localRotation = Quaternion.LookRotation(dir);
    } catch (Exception) { }
  }

  Vector3 CarrySlot(int i) {
    if (_carryAnchor != null) {
      Vector3 c = _carryAnchor.localPosition;
      Vector3 h = _host.transform.localPosition;
      return new Vector3(h.x + c.x + (i == 0 ? -0.13f : 0.13f), c.y, h.z + c.z);
    }
    Vector3 hp = _host.transform.localPosition;
    return hp + new Vector3(i == 0 ? -0.13f : 0.13f, 0.95f, 0.35f);
  }

  // Where a placed apple comes to rest: INSIDE the basket, slightly apart
  // (visible above the rim from the plaza camera).
  Vector3 BasketSlot(int i) {
    Vector3 b = BasketStand;
    if (_basket != null) b = _basket.localPosition;
    return new Vector3(b.x + (i == 0 ? -0.17f : 0.17f), 0.62f, b.z + (i == 0 ? 0.06f : -0.06f));
  }

  void Fly(GameObject apple, Vector3 from, Vector3 to, float delay, float dur, float lift) {
    _flyApple = apple;
    _flyFrom = from;
    _flyTo = to;
    _flyT = -delay;
    _flyDur = Mathf.Max(0.2f, dur);
    _flyLift = lift;
    _flying = true;
  }

  void TickFlight(float dt) {
    if (!_flying || _flyApple == null) return;
    try {
      _flyT += dt;
      if (_flyT < 0f) return; // pickup bend reads before the apple moves
      float t = Mathf.Clamp01(_flyT / _flyDur);
      Vector3 mid = (_flyFrom + _flyTo) * 0.5f + new Vector3(0f, _flyLift, 0f);
      Vector3 a = Vector3.Lerp(_flyFrom, mid, t);
      Vector3 b = Vector3.Lerp(mid, _flyTo, t);
      _flyApple.transform.localPosition = Vector3.Lerp(a, b, t);
      if (t >= 1f) { _flying = false; _flyApple = null; }
    } catch (Exception) { _flying = false; }
  }

  void TickCarry() {
    try {
      if (_carry0 && _apple0 != null && !_flying) _apple0.transform.localPosition = CarrySlot(0);
      if (_carry1 && _apple1 != null && !_flying) {
        // While apple1 flies, apple0 keeps following the hand.
        _apple1.transform.localPosition = CarrySlot(1);
      }
    } catch (Exception) { }
  }

  // Camera-first: while the child stands at the viewing spot, HOLD the stage
  // frame (re-issued every beat so the instruction stays a readable "card" —
  // not a 3s flick that snaps back mid-demo). Re-arms after walking clear.
  void WatchPlayer() {
    if (_playerT == null || _cam == null || _camT == null || _lookT == null) return;
    try {
      Vector3 p = _playerT.position;
      // Player lives in another scene graph — compare in garden space only if
      // the player is actually inside the garden island (x > 60).
      if (p.x < 60f) return;
      Vector3 local = p - new Vector3(120f, 0f, 0f);
      float dx = local.x - _mouth.x, dz = local.z - _mouth.z;
      float d2 = dx * dx + dz * dz;
      if (d2 <= WatchRadius * WatchRadius) {
        if (!_beatLatched) {
          _beatLatched = true;
          DemoBeatsFired++;
          _beatRefreshT = 0f;
          _cam.FrameAnchor(_camT, _lookT, BeatHoldSeconds);
        } else {
          _beatRefreshT += Time.deltaTime;
          if (_beatRefreshT >= BeatHoldSeconds - 1.2f) {
            _beatRefreshT = 0f;
            _cam.FrameAnchor(_camT, _lookT, BeatHoldSeconds);
          }
        }
      } else if (_beatLatched && d2 > (WatchRadius + RearmMargin) * (WatchRadius + RearmMargin)) {
        _beatLatched = false;
      }
    } catch (Exception) { }
  }
}
