// A_World/GeometryPlay/GeometrySocket.cs — placement pad. Kind + upright flag.
// Shows glowing blueprint silhouette ghost, pulses gently, and triggers snap FX on accept.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class GeometrySocket : MonoBehaviour, IClickTarget {
  public GeometryKind Kind;
  public bool NeedsUpright;
  public float Yaw;
  public GeometryPlay Game;
  public GeometryPiece Occupant { get; private set; }
  public bool Filled { get { return Occupant != null; } }

  Transform _ghostTransform;
  Vector3 _baseGhostScale = Vector3.one * 0.7f;
  float _pulseTime;

  void Start() {
    _ghostTransform = transform.Find("Ghost");
    if (_ghostTransform != null) _baseGhostScale = _ghostTransform.localScale;
  }

  void Update() {
    float dt = Time.deltaTime;
    if (dt <= 0f) return;

    if (!Filled && _ghostTransform != null && _ghostTransform.gameObject.activeSelf) {
      _pulseTime += dt;
      float scalePulse = 1.0f + Mathf.Sin(_pulseTime * 3.5f) * 0.05f;
      _ghostTransform.localScale = _baseGhostScale * scalePulse;
    }
  }

  public void Bind(GeometryPlay game, GeometryKind kind, bool upright, float yaw) {
    Game = game;
    Kind = kind;
    NeedsUpright = upright;
    Yaw = yaw;
    Occupant = null;
    _ghostTransform = transform.Find("Ghost");
    if (_ghostTransform != null) {
      _baseGhostScale = _ghostTransform.localScale;
      _ghostTransform.gameObject.SetActive(true);
    }
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TryPlace(this);
  }

  public void Accept(GeometryPiece piece) {
    Occupant = piece;
    if (_ghostTransform == null) _ghostTransform = transform.Find("Ghost");
    if (_ghostTransform != null) _ghostTransform.gameObject.SetActive(false);

    if (piece != null) piece.PlaceAt(transform, Yaw);

    MagicJuice.MagicHaloRing(transform, Vector3.up * 0.08f, MagicJuice.MagicGold, 1.4f);
    GameJuice.PlaceFx(transform, transform.position, transform);
  }

  public void ClearOccupant() {
    Occupant = null;
    if (_ghostTransform == null) _ghostTransform = transform.Find("Ghost");
    if (_ghostTransform != null) _ghostTransform.gameObject.SetActive(true);
  }
}
