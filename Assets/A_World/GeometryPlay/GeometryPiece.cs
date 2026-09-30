// A_World/GeometryPlay/GeometryPiece.cs — pickable shape. IClickTarget only.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class GeometryPiece : MonoBehaviour, IClickTarget {
  public enum PieceState { Idle, Carried, Placed }

  public GeometryKind Kind;
  public PieceState State { get; private set; }
  public Vector3 HomeLocal;
  public float HomeYaw;
  public bool EnvRole;
  public GeometryPlay Game;

  Collider _col;
  Transform _root;
  Vector3 _baseScale = Vector3.one;

  public void Bind(GeometryPlay game, Transform island, GeometryKind kind, Vector3 homeLocal, float yaw) {
    Game = game;
    _root = island;
    Kind = kind;
    HomeLocal = homeLocal;
    HomeYaw = yaw;
    State = PieceState.Idle;
    _col = GetComponent<Collider>();
    _baseScale = transform.localScale;
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TrySelect(this);
  }

  public void BeginCarry(Transform holder) {
    if (State == PieceState.Placed) return;
    State = PieceState.Carried;
    transform.SetParent(holder, true);
    transform.localPosition = new Vector3(0.15f, 1.05f, 0.35f);
    transform.localRotation = Quaternion.Euler(0f, HomeYaw, 0f);
    if (_col != null) _col.enabled = false;
  }

  public void ReturnHome() {
    State = PieceState.Idle;
    if (_root != null) transform.SetParent(_root, true);
    transform.localPosition = HomeLocal;
    transform.localRotation = Quaternion.Euler(0f, HomeYaw, 0f);
    transform.localScale = _baseScale;
    if (_col != null) _col.enabled = true;
  }

  public void PlaceAt(Transform socket, float yaw) {
    State = PieceState.Placed;
    transform.SetParent(socket, true);
    transform.localPosition = new Vector3(0f, 0.12f, 0f);
    transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    if (_col != null) _col.enabled = false;
  }

  public float Yaw {
    get { return transform.eulerAngles.y; }
  }
}
