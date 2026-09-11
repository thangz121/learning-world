// A_World/SmartCamera.cs — Agent A (World & Visual), W0-T1.
// Constrained context-driven camera (CONSTRAINED_3D.md §2): modes
// Follow / Interaction / Cinematic. No free-rotate/zoom input (constrained,
// no 360 camera). Smooth-damped movement; perspective camera enforced.
// All methods take plain Unity parameters — no dependency on other agents' classes.
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

  public CameraMode Mode { get; private set; } = CameraMode.Follow;

  Transform _followTarget;
  Vector3 _followOffset;
  bool _hasFollowTarget;

  Vector3 _focusPoint;
  float _focusDistance = 4f;
  bool _hasFocus;

  Transform[] _cinematicPath; // W0-T1 stub storage; traversal lands in a later task.

  Vector3 _positionVelocity; // Vector3.SmoothDamp state

  void Awake() {
    _followOffset = defaultOffset;
    EnforcePerspective();
  }

  // Follow mode: track a target with a fixed offset (e.g. player + over-shoulder offset).
  public void Follow(Transform target, Vector3 offset) {
    _followTarget = target;
    _followOffset = offset;
    _hasFollowTarget = target != null;
    _hasFocus = false;
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
    Mode = CameraMode.Interaction;
    EnforcePerspective();
  }

  // W0-T1 stub: records the path and switches mode; waypoint traversal lands later.
  public void PlayCinematic(Transform[] path) {
    _cinematicPath = path;
    Mode = CameraMode.Cinematic;
    if (path == null || path.Length == 0) {
      Debug.LogWarning("[SmartCamera] PlayCinematic called with an empty path (stub: holding position).", this);
    }
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
        break; // stub: hold position until cinematic traversal lands
    }
  }

  void TickFollow() {
    Vector3 desired = _followTarget.position + _followOffset;
    transform.position = Vector3.SmoothDamp(transform.position, desired, ref _positionVelocity, positionSmoothTime);
    LookTowards(_followTarget.position);
  }

  void TickInteraction() {
    Vector3 dir = transform.forward;
    if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
    Vector3 desired = _focusPoint - dir.normalized * _focusDistance
                      + Vector3.up * (_focusDistance * 0.2f);
    transform.position = Vector3.SmoothDamp(transform.position, desired, ref _positionVelocity, positionSmoothTime);
    LookTowards(_focusPoint);
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
