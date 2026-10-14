// A_World/ListeningGarden/ListeningGardenBell.cs — the "hear it again" bell
// beside the play spot (replays the current word; the child never needs text).
// IClickTarget only. C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class ListeningGardenBell : MonoBehaviour, IClickTarget {
  public ListeningGarden Game;

  public const float RetractDepth = -1.02f;
  public float CurrentY = 0f;
  public float TargetY = 0f;
  float _bounceOffset = 0f;

  public void SetRetracted(bool retracted) {
    TargetY = retracted ? RetractDepth : 0f;
  }

  public void Tick(float dt) {
    if (dt <= 0f) return;
    CurrentY = Mathf.MoveTowards(CurrentY, TargetY, dt * 3.5f);
    if (_bounceOffset > 0f) {
      _bounceOffset = Mathf.MoveTowards(_bounceOffset, 0f, dt * 0.8f);
    }
    Vector3 basePos = ListeningGardenBuilder.BellLocal;
    transform.localPosition = new Vector3(basePos.x, CurrentY + _bounceOffset, basePos.z);
  }

  public void OnClicked() {
    if (Game == null) return;
    _bounceOffset = 0.08f;
    Game.ReplayPrompt();
  }
}
