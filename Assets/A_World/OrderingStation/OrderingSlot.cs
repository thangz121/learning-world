// A_World/OrderingStation/OrderingSlot.cs — one track position.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class OrderingSlot : MonoBehaviour, IClickTarget {
  public int SlotIndex;
  public OrderingStation Game;
  public OrderingPiece Occupant { get; private set; }
  public bool Filled { get { return Occupant != null; } }

  public void Bind(OrderingStation game, int index) {
    Game = game;
    SlotIndex = index;
    Occupant = null;
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TryPlace(this);
  }

  public void Accept(OrderingPiece piece) {
    // A placed piece can be picked back up: evict the unlocked occupant home.
    if (Occupant != null && Occupant != piece) Occupant.ReturnHome();
    Occupant = piece;
    if (piece != null) piece.PlaceAt(transform);
  }

  public void Vacate(OrderingPiece piece) {
    if (Occupant == piece) Occupant = null;
  }

  public void ClearOccupant() {
    Occupant = null;
  }
}
