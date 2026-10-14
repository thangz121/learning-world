// A_World/MathWorld/MicroWorldPortal.cs — walk-in portal (Selection Yard era).
// Polls the player's XZ distance (same proven pattern as SubjectGate — no
// physics triggers, no input hacks) and fires the area beat once, re-arming
// only after the player walks clear. One component, two modes: Enter (gate ->
// area) / Exit (area mouth -> back). PHASE 5 legacy purge: the old counting
// garden / discovery area fields are gone — the Selection Yard is the only
// routing target; arena exits (PlayExit) go back to the game yard.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class MicroWorldPortal : MonoBehaviour {
  // FULL ARCHITECTURE RESET: an arena/yard opened from the Selection Yard
  // routes through this reference (enters and exits).
  public SelectionYardArea YardArea;
  public bool ExitMode;
  // Play arenas: this exit unloads the play scene and brings the child back
  // to the GAME YARD (PlayExit) instead of the hub level.
  public bool PlayExit;
  public float fireRadius = 1.3f;
  public float rearmMargin = 0.8f;

  // COLD START (journey J4 lesson, user report "vừa sang sân đã bị quay lại"):
  // the latch starts TRUE, so a player who SPAWNS inside the radius (e.g. the
  // arena entry warp next to the exit disc) does NOT fire — the portal only
  // arms after the player has walked clear of the radius at least once.
  bool _wasInside = true;

  // The one dispatch seam: the Selection Yard area.
  public IMicroWorldArea TargetArea() {
    return YardArea;
  }

  void Update() {
    IMicroWorldArea target = TargetArea();
    if (target == null) return;
    ClickToMove player = target.Player;
    if (player == null) return;
    Vector3 p = player.transform.position;
    float dx = p.x - transform.position.x;
    float dz = p.z - transform.position.z;
    float d2 = dx * dx + dz * dz;
    if (!_wasInside && d2 <= fireRadius * fireRadius) {
      _wasInside = true;
      if (PlayExit) {
        if (YardArea != null) YardArea.ExitGameToYard();
      } else if (ExitMode) {
        target.ExitToHub();
      } else {
        target.EnterFromHub();
      }
    } else if (_wasInside && d2 > (fireRadius + rearmMargin) * (fireRadius + rearmMargin)) {
      _wasInside = false;
    }
  }
}
