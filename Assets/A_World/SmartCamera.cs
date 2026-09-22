// A_World/SmartCamera.cs — Agent A (World & Visual), W1 vertical slice.
// Constrained context-driven camera (CONSTRAINED_3D.md §2): modes
// Follow / Interaction / Cinematic. No free-rotate/zoom input (constrained,
// no 360 camera). Smooth-damped movement; perspective camera enforced; camera
// height is clamped above the ground so it can never clip through it.
// W1 additions (API byte-compatible): real cinematic waypoint traversal
// (PlayCinematic now sweeps instead of holding), Bind(IGameEventBus) so a
// quest start triggers a short intro sweep that resumes Follow, and
// FocusPlayer() helper for the router/interaction framing.
// All methods take plain Unity parameters — no dependency on other agents' classes.
using System;
using UnityEngine;

public enum CameraMode { Follow, Interaction, Cinematic }

[DisallowMultipleComponent]
public class SmartCamera : MonoBehaviour {
  [Header("Smoothing")]
  [Tooltip("Position smoothing time (s) for SmoothDamp.")]
  public float positionSmoothTime = 0.35f;
  [Tooltip("Rotation smoothing speed for exponential Slerp.")]
  public float rotationSmoothSpeed = 5f;
  [Header("Follow defaults")]
  // Third-person follow offset: SOUTH-behind (+z), looking NORTH toward the
  // stall row. Phase-1 closure (P1Survey p2 run): the old (0,3.4,-5.2) parked
  // the camera NORTH looking SOUTH, so Milo (north-west of spawn) sat behind
  // the camera (spawn frame empty) and every authored south-front story pose
  // (intro/wrong/complete) forced a 180-degree sweep THROUGH the stall
  // (wrong framing red-out, BLOCKED_BY AwningStripe). South follow matches
  // the authored poses, so transitions stay short and under the awning
  // (canopy at y2.62, sightlines pass below it). Fence clearance: at z=6 the
  // view ray rides at y~1.0 (feet target) / ~1.7 (1m target), above the
  // 0.8m rail top.
  public Vector3 defaultOffset = new Vector3(0f, 3.2f, 4.6f);
  [Header("Obstruction")]
  [Tooltip("Pull the follow camera in front of blocking world geometry (stall awning etc).")]
  public float obstructionSphereRadius = 0.3f;
  [Tooltip("Layers the obstruction pull-in considers (default: everything).")]
  public LayerMask obstructionMask = ~0;
  [Header("Constraints")]
  [Tooltip("Minimum camera height (m) above y=0 ground; never clip through ground.")]
  public float minHeightAboveGround = 1.5f;
  [Tooltip("Obstruction pull-in never parks the camera closer than this (m) to the follow target. Phase-1 closure: the stall pull-in used to dive to ~1.5m/y1.36 and fill the frame with the player's arm.")]
  public float minFollowDistance = 2.6f;
  [Tooltip("Default cinematic sweep duration (s) for quest-start intros.")]
  public float cinematicDuration = 2.5f;
  [Header("Wheel zoom (Follow only)")]
  [Tooltip("Mouse-wheel zoom multiplier on the follow offset. 1 = frozen default framing (all authored beats untouched).")]
  public float zoomFactor = 1f;
  [Tooltip("Closest wheel zoom (multiplier on the follow offset).")]
  public float minZoomFactor = 0.45f;
  [Tooltip("Farthest wheel zoom (multiplier on the follow offset).")]
  public float maxZoomFactor = 3.2f;
  [Tooltip("Zoom change per wheel notch.")]
  public float zoomStep = 0.15f;

  public CameraMode Mode { get; private set; } = CameraMode.Follow;

  Transform _followTarget;
  Vector3 _followOffset;
  bool _hasFollowTarget;
  Vector3 _focusPoint;
  float _focusDistance = 4f;
  bool _hasFocus;

  // Temporary emotional-beat framing (§Blocker7): focus a world point, then
  // automatically resume the previous Follow target. Reusable for any quest;
  // never leaves the camera stranded. Durations stay short (1.5-3s).
  Transform _returnTarget;
  Vector3 _returnOffset;
  bool _hasReturn;
  float _focusT;

  public void FocusOnFor(Vector3 point, float distance, float seconds) {
    _returnTarget = _followTarget;
    _returnOffset = _followOffset;
    _hasReturn = _hasFollowTarget && _followTarget != null;
    FocusOn(point, distance);
    _focusT = Mathf.Max(0.5f, seconds);
  }

  // Explicit story framing (player-experience audit): place the camera at an
  // authored pose looking at a story point (e.g. Mia's stall front, never
  // through her awning), then auto-return like FocusOnFor. Reusable for any
  // introduction/reveal beat whose sightline FocusOnFor cannot guarantee.
  Vector3 _explicitCamPos;
  bool _hasExplicitCamPos;

  public void FramePointFor(Vector3 camPos, Vector3 point, float seconds) {
    _returnTarget = _followTarget;
    _returnOffset = _followOffset;
    _hasReturn = _hasFollowTarget && _followTarget != null;
    _focusPoint = point;
    _explicitCamPos = camPos;
    _hasExplicitCamPos = true;
    _hasFocus = true;
    _hasFollowTarget = false;
    _cinPlaying = false;
    Mode = CameraMode.Interaction;
    _positionVelocity = Vector3.zero;
    _focusT = Mathf.Max(0.5f, seconds);
    EnforcePerspective();
  }

  // P1-2 anchor-driven beat: same framing as FramePointFor but posed by
  // scene-authored anchors (ActivityAnchors.Camera/CameraLook) instead of
  // magic vectors. Null anchors = graceful no-op (caller keeps its vector
  // fallback), so half-staged worlds never strand the camera.
  public void FrameAnchor(Transform camAnchor, Transform lookAnchor, float seconds) {
    if (camAnchor == null || lookAnchor == null) {
      Debug.LogWarning("[SmartCamera] FrameAnchor with null anchor; beat skipped.", this);
      return;
    }
    FramePointFor(camAnchor.position, lookAnchor.position, seconds);
  }

  Vector3[] _cinPath;
  float _cinT;
  float _cinDuration;
  bool _cinPlaying;

  Vector3 _positionVelocity; // Vector3.SmoothDamp state

  IGameEventBus _bus;
  IDisposable _questSub;

  void Awake() {
    _followOffset = defaultOffset;
    EnforcePerspective();
  }

  // Injection boundary (wired by MarketBuilder.BuildServices). Subscribes to
  // QuestStartedEvent for the cinematic slice intro sweep. Null-safe.
  public void Bind(IGameEventBus bus) {
    if (_questSub != null) { _questSub.Dispose(); _questSub = null; }
    _bus = bus;
    if (_bus != null) _questSub = _bus.Subscribe<QuestStartedEvent>(OnQuestStarted);
  }

  void OnDisable() {
    if (_questSub != null) { _questSub.Dispose(); _questSub = null; }
  }

  // Follow mode: track a target with a fixed offset (e.g. player + over-shoulder offset).
  public void Follow(Transform target, Vector3 offset) {
    _followTarget = target;
    _followOffset = offset;
    _hasFollowTarget = target != null;
    _hasFocus = false;
    _hasExplicitCamPos = false;
    _cinPlaying = false;
    Mode = CameraMode.Follow;
    _positionVelocity = Vector3.zero;
  }

  // Interaction mode: depth-aware framing of a world point. Keeps the current
  // view direction, pulls back along it by `distance`, and adds a slight height
  // bias so foreground/background occlusion stays readable on a perspective camera.
  public void FocusOn(Vector3 point, float distance) {
    _focusPoint = point;
    _focusDistance = Mathf.Max(0.5f, distance);
    _hasFocus = true;
    _hasExplicitCamPos = false;
    _hasFollowTarget = false;
    _cinPlaying = false;
    Mode = CameraMode.Interaction;
    _positionVelocity = Vector3.zero;
    EnforcePerspective();
  }

  // Convenience: frame the followed player as an interaction target.
  public void FocusPlayer(float distance) {
    if (_hasFollowTarget && _followTarget != null) FocusOn(_followTarget.position, distance);
  }

  // Cinematic mode: sweep through path waypoints over `duration` seconds, then
  // resume Follow (if a follow target is set) or hold the final pose.
  public void PlayCinematic(Transform[] path) {
    if (path == null || path.Length == 0) {
      Debug.LogWarning("[SmartCamera] PlayCinematic called with an empty path (holding position).", this);
      return;
    }
    Vector3[] points = new Vector3[path.Length];
    for (int i = 0; i < path.Length; i++) {
      points[i] = (path[i] != null) ? path[i].position : transform.position;
    }
    PlayCinematic(points, cinematicDuration);
  }

  // Points overload: no GameObject dependency, usable for generated sweeps.
  public void PlayCinematic(Vector3[] points, float duration) {
    if (points == null || points.Length == 0) {
      Debug.LogWarning("[SmartCamera] PlayCinematic called with an empty path (holding position).", this);
      return;
    }
    _cinPath = points;
    _cinT = 0f;
    _cinDuration = Mathf.Max(0.1f, duration);
    _cinPlaying = true;
    _hasExplicitCamPos = false;
    Mode = CameraMode.Cinematic;
    _positionVelocity = Vector3.zero;
    EnforcePerspective();
  }

  // Quest-start intro sweep: short side-arc around the current view that lands
  // back on the follow target, then resumes Follow mode automatically.
  void OnQuestStarted(QuestStartedEvent e) {
    if (!_hasFollowTarget || _followTarget == null) return;
    Vector3 home = _followTarget.position + _followOffset;
    Vector3 side = new Vector3(2.5f, 1.2f, 0f);
    Vector3[] sweep = new Vector3[] { transform.position, home + side, home };
    PlayCinematic(sweep, cinematicDuration);
  }

  void LateUpdate() {
    switch (Mode) {
      case CameraMode.Follow:
        if (_hasFollowTarget && _followTarget != null) {
          PollWheelZoom(); // Follow only: authored beats keep frozen framing
          TickFollow();
        }
        break;
      case CameraMode.Interaction:
        if (_hasFocus) TickInteraction();
        if (_focusT > 0f) {
          _focusT -= Time.deltaTime;
          if (_focusT <= 0f && _hasReturn && _returnTarget != null) {
            _hasReturn = false;
            Follow(_returnTarget, _returnOffset);
          }
        }
        break;
      case CameraMode.Cinematic:
        if (_cinPlaying) TickCinematic();
        break;
    }
  }

  void TickFollow() {
    Vector3 offset = ZoomedOffset(_followOffset, zoomFactor);
    Vector3 desired = ClampAboveGround(EnforceFollowFloor(
      _followTarget.position,
      ResolveObstruction(_followTarget.position, _followTarget.position + offset)));
    transform.position = Vector3.SmoothDamp(transform.position, desired, ref _positionVelocity, positionSmoothTime);
    LookTowards(_followTarget.position);
  }

  // Wheel zoom (user round: free zoom in/out, no hard camera). Scroll up =
  // closer, scroll down = farther. Pure multiplier; 1.0 reproduces the
  // frozen default framing byte-for-byte, so all beat/camera tests hold.
  void PollWheelZoom() {
    float wheel = ReadWheelDelta();
    if (Mathf.Abs(wheel) < 0.001f) return;
    zoomFactor = ClampZoomFactor(zoomFactor - Mathf.Sign(wheel) * zoomStep, minZoomFactor, maxZoomFactor);
  }

  static float ReadWheelDelta() {
    try {
      var mouse = UnityEngine.InputSystem.Mouse.current;
      if (mouse == null) return 0f;
      return mouse.scroll.ReadValue().y;
    } catch (Exception) { return 0f; }
  }

  // Pure seams (EditMode cover without a live frame).
  public static float ClampZoomFactor(float z, float min, float max) {
    if (min > max) { float t = min; min = max; max = t; }
    return Mathf.Clamp(z, min, max);
  }

  public static Vector3 ZoomedOffset(Vector3 baseOffset, float zoom) {
    return baseOffset * zoom;
  }

  // Follow floor (Phase-1 closure): obstruction pull-in must never park the
  // camera inside the player's personal space (survey: stall-S dove to 1.5m /
  // y1.36 and filled the frame with an arm). Holds a minimum stand-off from
  // the target along the resolved ray. Reusable for any follow target.
  Vector3 EnforceFollowFloor(Vector3 targetPoint, Vector3 pulled) {
    Vector3 away = pulled - targetPoint;
    if (away.magnitude < minFollowDistance) {
      if (away.sqrMagnitude < 0.0001f) away = Vector3.back;
      pulled = targetPoint + away.normalized * minFollowDistance;
    }
    return pulled;
  }

  // Obstruction pull-in (player-experience audit): the unconstrained follow
  // offset can park the camera inside the stall awning or behind the tree, so
  // sweep from the target toward the desired pose and stop in front of the
  // first blocking surface. Hits within 1m are the followed character's own
  // capsule and are ignored. Reusable for any follow target.
  Vector3 ResolveObstruction(Vector3 targetPoint, Vector3 desired) {
    Vector3 from = targetPoint + Vector3.up * 1f;
    Vector3 delta = desired - from;
    float dist = delta.magnitude;
    if (dist < 0.001f) return desired;
    RaycastHit[] hits = Physics.SphereCastAll(
      from, obstructionSphereRadius, delta.normalized, dist, obstructionMask);
    float nearest = dist;
    foreach (RaycastHit h in hits) {
      if (h.collider == null) continue;
      if (h.distance < 1f) continue;
      if (h.distance < nearest) nearest = h.distance;
    }
    if (nearest < dist)
      return from + delta.normalized * Mathf.Max(0.8f, nearest - 0.4f);
    return desired;
  }

  void TickInteraction() {
    Vector3 desired;
    if (_hasExplicitCamPos) {
      desired = ClampAboveGround(ResolveObstruction(_focusPoint, _explicitCamPos));
    } else {
      Vector3 dir = transform.forward;
      if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
      desired = ClampAboveGround(ResolveObstruction(
        _focusPoint, _focusPoint - dir.normalized * _focusDistance + Vector3.up * (_focusDistance * 0.2f)));
    }
    transform.position = Vector3.SmoothDamp(transform.position, desired, ref _positionVelocity, positionSmoothTime);
    LookTowards(_focusPoint);
  }

  void TickCinematic() {
    _cinT += Time.deltaTime / _cinDuration;
    float t = Mathf.Clamp01(_cinT);
    Vector3 desired = ClampAboveGround(SamplePath(_cinPath, Smooth(t)));
    // SmoothDamp with a short time keeps the sweep fluid without lagging corners.
    Vector3 vel = Vector3.zero;
    transform.position = Vector3.SmoothDamp(transform.position, desired, ref vel, 0.08f);
    Vector3 lookAt = (_hasFollowTarget && _followTarget != null)
      ? _followTarget.position
      : SamplePath(_cinPath, Mathf.Clamp01(t + 0.05f));
    LookTowards(lookAt);
    if (_cinT >= 1f) {
      _cinPlaying = false;
      if (_hasFollowTarget && _followTarget != null) {
        Mode = CameraMode.Follow; // intro sweep lands back on the player
      }
    }
  }

  static Vector3 SamplePath(Vector3[] path, float t) {
    if (path.Length == 1) return path[0];
    float scaled = t * (path.Length - 1);
    int seg = Mathf.Min(Mathf.FloorToInt(scaled), path.Length - 2);
    return Vector3.Lerp(path[seg], path[seg + 1], scaled - seg);
  }

  static float Smooth(float t) { return t * t * (3f - 2f * t); }

  Vector3 ClampAboveGround(Vector3 pos) {
    if (pos.y < minHeightAboveGround) pos.y = minHeightAboveGround;
    return pos;
  }

  void LookTowards(Vector3 point) {
    Vector3 toTarget = point - transform.position;
    if (toTarget.sqrMagnitude < 0.0001f) return;
    Quaternion look = Quaternion.LookRotation(toTarget, Vector3.up);
    float t = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
    transform.rotation = Quaternion.Slerp(transform.rotation, look, t);
  }

  void EnforcePerspective() {
    Camera cam = GetComponent<Camera>();
    if (cam != null) cam.orthographic = false; // constrained 3D requires a perspective camera
  }
}
