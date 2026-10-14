// A_World/SelectionYard/SelectionGate.cs — FULL ARCHITECTURE RESET (2026-09-29).
// The generic yard gate: a walk-in trigger for the A->B->C navigation, reusing
// the proven MicroWorldPortal poll contract (XZ distance, cold-start latch so
// walking past never fires, re-arm only after walking clear). ONE component
// serves every yard gate, with context-correct targets:
//   Skill (A doors, target = subjectId) -> EnterSkill(subjectId)
//   Game  (B doors, target = skillId)   -> EnterGame(skillId)   // its game yard
//   Play  (C doors, target = gameId)    -> PlayGame(gameId)     // the arena
//   Back  (any yard)                    -> Back()               // up one level
// Presentation only owns the poll; travel beats live in the area.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class SelectionGate : MonoBehaviour {
  public enum GateKind { Skill, Game, Play, Back }

  public SelectionYardArea Area;
  public GateKind Kind = GateKind.Skill;
  public string TargetId = "";
  public float fireRadius = 1.6f;
  public float rearmMargin = 0.8f;

  // COLD START (same lesson as the micro-world portals): the latch starts TRUE
  // so a spawn inside the radius never fires; the gate arms after the child
  // walks clear once.
  bool _wasInside = true;

  public void Bind(SelectionYardArea area, GateKind kind, string targetId) {
    Area = area;
    Kind = kind;
    TargetId = targetId;
  }

  // Pure seam (EditMode-coverable): would this position fire?
  public bool WouldFire(Vector3 playerPos) {
    float dx = playerPos.x - transform.position.x;
    float dz = playerPos.z - transform.position.z;
    return dx * dx + dz * dz <= fireRadius * fireRadius;
  }

  void Update() {
    if (Area == null) return;
    ClickToMove player = Area.Player;
    if (player == null) return;
    Vector3 p = player.transform.position;
    float dx = p.x - transform.position.x;
    float dz = p.z - transform.position.z;
    float d2 = dx * dx + dz * dz;
    if (!_wasInside && d2 <= fireRadius * fireRadius) {
      _wasInside = true;
      switch (Kind) {
        case GateKind.Game: Area.EnterGame(TargetId); break;
        case GateKind.Play: Area.PlayGame(TargetId); break;
        case GateKind.Back: Area.Back(); break;
        default: Area.EnterSkill(TargetId); break;
      }
    } else if (_wasInside && d2 > (fireRadius + rearmMargin) * (fireRadius + rearmMargin)) {
      _wasInside = false;
    }
  }

  public static readonly System.Collections.Generic.List<SelectionGate> ActiveGates =
    new System.Collections.Generic.List<SelectionGate>();

  void OnEnable() {
    if (!ActiveGates.Contains(this)) ActiveGates.Add(this);
  }

  void OnDisable() {
    ActiveGates.Remove(this);
  }

  void OnDestroy() {
    ActiveGates.Remove(this);
  }

  // Snaps hit/ray clicks near SelectionGate directly to the gate portal mouth
  public static bool TrySnapSelectionGate(Vector3 hitPoint, out Vector3 mouth) {
    mouth = hitPoint;
    float best2 = 2.4f * 2.4f;
    bool found = false;
    Vector3 bestPos = Vector3.zero;
    for (int i = 0; i < ActiveGates.Count; i++) {
      SelectionGate g = ActiveGates[i];
      if (g == null || !g.isActiveAndEnabled) continue;
      Vector3 p = g.transform.position;
      float dx = hitPoint.x - p.x;
      float dz = hitPoint.z - p.z;
      float d2 = dx * dx + dz * dz;
      if (d2 < best2) {
        best2 = d2;
        bestPos = p;
        found = true;
      }
    }
    if (!found) return false;
    mouth = new Vector3(bestPos.x, 0f, bestPos.z);
    return true;
  }
}
