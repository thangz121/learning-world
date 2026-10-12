// A_World/ListeningGarden/ListeningGardenBell.cs — the "hear it again" bell
// beside the play spot (replays the current word; the child never needs text).
// IClickTarget only. C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class ListeningGardenBell : MonoBehaviour, IClickTarget {
  public ListeningGarden Game;

  public void OnClicked() {
    if (Game == null) return;
    Game.ReplayPrompt();
  }
}
