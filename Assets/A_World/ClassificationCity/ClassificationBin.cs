// A_World/ClassificationCity/ClassificationBin.cs — a group destination.
// One bin answers exactly one group key per round. C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ClassificationBin : MonoBehaviour, IClickTarget {
  public string GroupKey = "";
  public string BinId = "";
  public ClassificationCity Game;
  public readonly List<ClassificationItem> Occupants = new List<ClassificationItem>();

  public void Bind(ClassificationCity game, string binId, string groupKey) {
    Game = game;
    BinId = binId;
    GroupKey = groupKey;
    Occupants.Clear();
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TryPlace(this);
  }

  public void Accept(ClassificationItem item) {
    if (item == null) return;
    Occupants.Add(item);
    item.PlaceAt(transform);
  }
}
