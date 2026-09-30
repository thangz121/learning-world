// A_World/ComparisonMarket/ComparisonFruit.cs — LV5 pairing fruit.
// Tap apple then orange to pair one-to-one. C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class ComparisonFruit : MonoBehaviour, IClickTarget {
  public int Group; // 0 = apples, 1 = oranges
  public bool Paired;
  public ComparisonMarket Game;

  public void Bind(ComparisonMarket game, int group) {
    Game = game;
    Group = group;
    Paired = false;
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TryTapFruit(this);
  }
}
