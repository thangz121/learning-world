// A_World/GeometryPlay/GeometryPiece.cs — Interactive pickable shape.
// Supports direct tap, parabolic flight with sparkles, magnetic snap, and squash/wobble.
// C# 9.0 only.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class GeometryPiece : MonoBehaviour, IClickTarget {
  public enum PieceState { Idle, Carried, Placed, Flying }

  public GeometryKind Kind;
  public PieceState State { get; private set; } = PieceState.Idle;
  public Vector3 HomeLocal;
  public float HomeYaw;
  public bool EnvRole;
  public GeometryPlay Game;

  Collider _col;
  UnityEngine.AI.NavMeshObstacle _obs;
  Transform _root;
  Vector3 _baseScale = Vector3.one;

  bool _isFlying;
  float _flightT;
  Vector3 _flightStartPos;
  Vector3 _flightTargetPos;
  float _flightTargetYaw;
  Transform _flightSocket;
  Action _onFlightLanded;
  float _hoverTime;
  float _hoverOffset;

  public void Bind(GeometryPlay game, Transform island, GeometryKind kind, Vector3 homeLocal, float yaw) {
    Game = game;
    _root = island;
    Kind = kind;
    HomeLocal = homeLocal;
    HomeYaw = yaw;
    State = PieceState.Idle;
    _col = GetComponent<Collider>();
    _obs = GetComponent<UnityEngine.AI.NavMeshObstacle>();
    _baseScale = transform.localScale;
    _hoverOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
    SetBlocking(true);
  }

  void Update() {
    float dt = Time.deltaTime;
    if (dt <= 0f) return;

    if (_isFlying) {
      _flightT += dt * 2.2f;
      float t = Mathf.Clamp01(_flightT);
      float smooth = Mathf.SmoothStep(0f, 1f, t);
      float height = Mathf.Sin(t * Mathf.PI) * 1.5f;

      Vector3 target = _flightSocket != null
        ? _flightSocket.position + new Vector3(0f, 0.12f, 0f)
        : _flightTargetPos;

      Vector3 pos = Vector3.Lerp(_flightStartPos, target, smooth);
      pos.y += height;
      transform.position = pos;
      transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, _flightTargetYaw, 0f), smooth);

      if (t >= 1f) {
        _isFlying = false;
        State = PieceState.Placed;
        if (_flightSocket != null) {
          transform.SetParent(_flightSocket, true);
          transform.localPosition = new Vector3(0f, 0.12f, 0f);
          transform.localRotation = Quaternion.Euler(0f, _flightTargetYaw, 0f);
        }
        if (_col != null) _col.enabled = false;
        MagicJuice.FairySparkleBurst(transform, Vector3.up * 0.15f, 16);
        _onFlightLanded?.Invoke();
        _onFlightLanded = null;
      }
      return;
    }

    if (State == PieceState.Idle && !EnvRole) {
      _hoverTime += dt;
      if (_root != null && transform.parent == _root) {
        float hover = Mathf.Sin(_hoverTime * 2.8f + _hoverOffset) * 0.035f;
        transform.localPosition = HomeLocal + new Vector3(0f, hover, 0f);
      }
    }
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TrySelect(this);
  }

  public void BeginFlyTo(Transform socket, float targetYaw, Action onArrived) {
    if (State == PieceState.Placed) return;
    State = PieceState.Flying;
    _isFlying = true;
    _flightT = 0f;
    _flightStartPos = transform.position;
    _flightSocket = socket;
    _flightTargetPos = socket != null ? socket.position + new Vector3(0f, 0.12f, 0f) : transform.position;
    _flightTargetYaw = targetYaw;
    _onFlightLanded = onArrived;
    if (_col != null) _col.enabled = false;
    SetBlocking(false);
    MagicJuice.FairySparkleBurst(transform, Vector3.up * 0.2f, 8);
  }

  public void PlayWrongWobble() {
    GameJuice.Wobble(transform, 12f, 0.45f);
  }

  public void BeginCarry(Transform holder) {
    if (State == PieceState.Placed) return;
    _isFlying = false;
    State = PieceState.Carried;
    transform.SetParent(holder, true);
    transform.localPosition = new Vector3(0.55f, 0.88f, 0.95f);
    transform.localRotation = Quaternion.Euler(0f, HomeYaw, 0f);
    transform.localScale = _baseScale * 0.7f;
    if (_col != null) _col.enabled = false;
    SetBlocking(false);
  }

  public void ReturnHome() {
    _isFlying = false;
    State = PieceState.Idle;
    if (_root != null) transform.SetParent(_root, true);
    transform.localPosition = HomeLocal;
    transform.localRotation = Quaternion.Euler(0f, HomeYaw, 0f);
    transform.localScale = _baseScale;
    if (_col != null) _col.enabled = true;
    SetBlocking(true);
  }

  public void PlaceAt(Transform socket, float yaw) {
    _isFlying = false;
    State = PieceState.Placed;
    transform.SetParent(socket, true);
    transform.localPosition = new Vector3(0f, 0.12f, 0f);
    transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    if (_col != null) _col.enabled = false;
    SetBlocking(false);
  }

  void SetBlocking(bool on) {
    if (_obs != null) _obs.enabled = on && !EnvRole;
  }

  public float Yaw {
    get { return transform.eulerAngles.y; }
  }
}
