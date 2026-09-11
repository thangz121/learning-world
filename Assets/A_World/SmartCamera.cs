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
  public Vector3 defaultOffset = new Vector3(0f, 4.5f, -6f);
  [Header("Constraints")]
  [Tooltip("Minimum camera height (m) above y=0 ground; never clip through ground.")]
  public float minHeightAboveGround = 1.2f;
  [Tooltip("Default cinematic sweep duration (s) for quest-start intros.")]
  public float cinematicDuration = 2.5f;

  public CameraMode Mode { get; private set; } = CameraMode.Follow;

  Transform _followTarget;
  Vector3 _followOffset;
  bool _hasFollowTarget;

  Vector3 _focusPoint;
  float _focusDistance = 4f;
  bool _hasFocus;

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
    _cinPlaying = false;
    Mode = CameraMode.Follow;
  }

  // Interaction mode: depth-aware framing of a world point. Keeps the current
  // view direction, pulls back along it by `distance`, and adds a slight height
  // bias so foreground/background occlusion stays readable on a perspective camera.
  public void FocusOn(Vector3 point, float distance) {
    _focusPoint = point;
    _focusDistance = Mathf.Max(0.5f, distance);
    _hasFocus = true;
    _hasFollowTarget = false;
    _cinPlaying = false;
    Mode = CameraMode.Interaction;
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
    Mode = CameraMode.Cinematic;
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
        if (_hasFollowTarget && _followTarget != null) TickFollow();
        break;
      case CameraMode.Interaction:
        if (_hasFocus) TickInteraction();
        break;
      case CameraMode.Cinematic:
        if (_cinPlaying) TickCinematic();
        break;
    }
  }

  void TickFollow() {
    Vector3 desired = ClampAboveGround(_followTarget.position + _followOffset);
    transform.position = Vector3.SmoothDamp(transform.position, desired, ref _positionVelocity, positionSmoothTime);
    LookTowards(_followTarget.position);
  }

  void TickInteraction() {
    Vector3 dir = transform.forward;
    if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
    Vector3 desired = ClampAboveGround(
      _focusPoint - dir.normalized * _focusDistance + Vector3.up * (_focusDistance * 0.2f));
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
