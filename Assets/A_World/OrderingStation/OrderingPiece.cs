// A_World/OrderingStation/OrderingPiece.cs — sortable station object.
// IClickTarget only. C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class OrderingPiece : MonoBehaviour, IClickTarget {
  public enum PieceState { Idle, Carried, Placed }

  public float Rank;
  public OrderDim Dim;
  public string PieceId = "";
  public bool Locked;
  public PieceState State { get; private set; }
  public Vector3 HomeLocal;
  public OrderingStation Game;

  Collider _col;
  Transform _root;
  Vector3 _baseScale = Vector3.one;

  public void Bind(OrderingStation game, Transform island, float rank, OrderDim dim,
      string id, Vector3 homeLocal, bool locked) {
    Game = game;
    _root = island;
    Rank = rank;
    Dim = dim;
    PieceId = id;
    HomeLocal = homeLocal;
    Locked = locked;
    State = PieceState.Idle;
    _col = GetComponent<Collider>();
    _baseScale = transform.localScale;
    if (locked && _col != null) _col.enabled = false;
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TrySelect(this);
  }

  public void BeginCarry(Transform holder) {
    if (Locked || State == PieceState.Carried) return;
    if (State == PieceState.Placed) Game.DetachFromSlot(this);
    State = PieceState.Carried;
    transform.SetParent(holder, true);
    transform.localPosition = new Vector3(0.55f, 0.88f, 0.95f);
    transform.localRotation = Quaternion.identity;
    if (_col != null) _col.enabled = false;
  }

  public void ReturnHome() {
    if (Locked) return;
    State = PieceState.Idle;
    if (_root != null) transform.SetParent(_root, true);
    transform.localPosition = HomeLocal;
    transform.localRotation = Quaternion.identity;
    transform.localScale = _baseScale;
    if (_col != null) _col.enabled = true;
  }

  public void PlaceAt(Transform slot) {
    State = PieceState.Placed;
    transform.SetParent(slot, true);
    transform.localPosition = new Vector3(0f, 0.32f, 0f);
    transform.localRotation = Quaternion.identity;
    // USER ROUND 2026-10-01 (spec: "objects must be movable after placement"):
    // keep the collider ENABLED so the child can pick the piece back up, move it
    // or swap/insert it elsewhere without a full reset.
    if (_col != null) _col.enabled = true;
  }
}
