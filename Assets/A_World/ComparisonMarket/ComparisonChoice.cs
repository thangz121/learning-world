// A_World/ComparisonMarket/ComparisonChoice.cs — tappable market choice.
// IClickTarget only. C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class ComparisonChoice : MonoBehaviour, IClickTarget {
  public string ChoiceId = "";
  public bool IsAnswer;
  public bool IsCart;
  public ComparisonMarket Game;

  public void Bind(ComparisonMarket game, string id, bool answer, bool cart) {
    Game = game;
    ChoiceId = id;
    IsAnswer = answer;
    IsCart = cart;
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TryTapChoice(this);
  }
}
