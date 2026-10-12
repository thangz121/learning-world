// A_World/ListeningGarden/ListeningGardenChoice.cs — tappable picture pod.
// IClickTarget only. C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class ListeningGardenChoice : MonoBehaviour, IClickTarget {
  public string WordId = "";
  public bool IsAnswer;
  public ListeningGarden Game;

  public void OnClicked() {
    if (Game == null) return;
    Game.TryTapChoice(this);
  }
}
