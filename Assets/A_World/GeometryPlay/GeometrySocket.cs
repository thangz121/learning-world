// A_World/GeometryPlay/GeometrySocket.cs — placement pad. Kind + upright flag.
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

  public void Bind(GeometryPlay game, GeometryKind kind, bool upright, float yaw) {
    Game = game;
    Kind = kind;
    NeedsUpright = upright;
    Yaw = yaw;
    Occupant = null;
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TryPlace(this);
  }

  public void Accept(GeometryPiece piece) {
    Occupant = piece;
    if (piece != null) piece.PlaceAt(transform, Yaw);
  }

  public void ClearOccupant() {
    Occupant = null;
  }
}
