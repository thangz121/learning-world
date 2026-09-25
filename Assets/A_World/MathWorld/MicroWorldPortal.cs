// A_World/MathWorld/MicroWorldPortal.cs — S2 PIONEER MICRO-WORLD (v1).
// Walk-in portal for a micro-world area: polls the player's XZ distance (same
// proven pattern as SubjectGate — no physics triggers, no input hacks) and
// fires the area beat once, re-arming only after the player walks clear. One
// component, two modes: Enter (hub gate -> area) / Exit (area mouth -> hub).
// Reusable for the other 9 micro-worlds when their turn comes.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class MicroWorldPortal : MonoBehaviour {
  public CountingGardenArea Area;
  // Gameplay #4 (S3-P2Z14): an independent micro-world (Build Yard) uses the
  // same walk-in portal contract. Exactly ONE of the two targets is set.
  public BuildTowerArea BuildArea;
  public bool ExitMode;
  // S3 P2X play arena: this exit unloads the play scene and brings the child
  // back to the Counting Garden (the exit portal of the GARDEN keeps
  // ExitMode -> Math Hub). Same poll contract, one mode bit.
  public bool PlayExit;
  public string areaId = CountingGardenArea.AreaId;
  public float fireRadius = 1.3f;
  public float rearmMargin = 0.8f;

  // COLD START (journey J4 lesson, user report "vừa sang sân đã bị quay lại"):
  // the latch starts TRUE, so a player who SPAWNS inside the radius (e.g. the
  // garden entry warp next to the exit disc) does NOT fire — the portal only
  // arms after the player has walked clear of the radius at least once.
  bool _wasInside = true;

  void Update() {
    ClickToMove player = Area != null ? Area.Player : (BuildArea != null ? BuildArea.Player : null);
    if (player == null) return;
    Vector3 p = player.transform.position;
    float dx = p.x - transform.position.x;
    float dz = p.z - transform.position.z;
    float d2 = dx * dx + dz * dz;
    if (!_wasInside && d2 <= fireRadius * fireRadius) {
      _wasInside = true;
      if (PlayExit) { if (Area != null) Area.ExitPlayToGarden(); }
      else if (ExitMode) { if (BuildArea != null) BuildArea.ExitToHub(); else if (Area != null) Area.ExitToHub(); }
      else { if (BuildArea != null) BuildArea.EnterFromHub(); else if (Area != null) Area.EnterFromHub(); }
    } else if (_wasInside && d2 > (fireRadius + rearmMargin) * (fireRadius + rearmMargin)) {
      _wasInside = false;
    }
  }
}
