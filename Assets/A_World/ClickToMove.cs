// A_World/ClickToMove.cs — Agent A (World & Visual), W0-T1.
// Click-to-move inside the constrained gameplay region (CONSTRAINED_3D.md:
// no free-360 camera, no open world). NavMesh-based movement; on arrival at a
// typed interaction target publishes NavigationCompleted(InteractionId, NpcId).
// Dependency injection ONLY via Bind(IGameEventBus). No ServiceLocator,
// no FindObjectOfType, no `new` service, no audio/speech/Worker calls.
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public class ClickToMove : MonoBehaviour {
  [Header("Click picking")]
  [Tooltip("Layers clickable for movement. Defaults to everything.")]
  public LayerMask clickMask = ~0;
  [Tooltip("Max raycast distance (m) for click picking.")]
  public float clickMaxDistance = 200f;

  IGameEventBus _bus;
  NavMeshAgent _agent;
  bool _hasDestination;
  bool _hasTarget; // true when the current destination carries a typed interaction target
  InteractionId _target;
  NpcId _byNpc;

  // Seconds since the last successful action (MoveTo command or arrival).
  // Consumed by the hint system (visual L1 ~8s, Milo point L2 ~15s — owned by B/D).
  public float IdleSeconds { get; private set; }

  // Destination introspection (DestinationMarker presenter): where the player
  // is currently walking to. Valid only while HasDestination is true.
  public bool HasDestination {
    get { return _hasDestination; }
  }

  // S3-P2Z10: "stop, then act" gate for carry-and-place interactions — a place
  // only happens once the child has actually stopped at the basket, never while
  // they are still walking past it.
  public bool IsMoving {
    get { return _agent != null && _agent.velocity.sqrMagnitude > 0.04f; }
  }

  public Vector3 Destination {
    get { return _agent != null ? _agent.destination : transform.position; }
  }

  // Injection boundary (wired by GameInstaller). MonoBehaviours cannot use
  // constructor injection (Unity instantiates them), so Bind is the pattern.
  public void Bind(IGameEventBus bus) { _bus = bus; }

  void Awake() {
    _agent = GetComponent<NavMeshAgent>();
  }

  // Required contract: plain movement with no interaction identity attached.
  // Arrival at a plain destination publishes nothing.
  public void MoveTo(Vector3 destination) {
    if (!TrySetDestination(destination)) return;
    _hasTarget = false;
    ResetIdle();
  }

  // Overload: movement toward a typed interaction target. Arrival within the
  // agent's stopping distance publishes NavigationCompleted(target, byNpc).
  public void MoveTo(Vector3 destination, InteractionId target, NpcId byNpc) {
    if (!TrySetDestination(destination)) return;
    _target = target;
    _byNpc = byNpc;
    _hasTarget = true;
    ResetIdle();
  }

  public void ResetIdle() { IdleSeconds = 0f; }

  public void Stop() {
    _hasDestination = false;
    _hasTarget = false;
    if (_agent != null && _agent.isOnNavMesh) _agent.ResetPath();
  }

  // Phase 3.0: deterministic reposition for world returns (return arch ->
  // main-world road head). Uses NavMeshAgent.Warp (never transform.position)
  // so the agent stays on the NavMesh; snaps to the nearest valid point
  // within 1m and drops any pending destination/target. Same GameObjects
  // throughout: no duplication, no event churn, no state loss.
  public bool WarpTo(Vector3 destination) {
    if (_agent == null || !_agent.isOnNavMesh) return false;
    NavMeshHit hit;
    if (!NavMesh.SamplePosition(destination, out hit, 1.0f, NavMesh.AllAreas)) return false;
    _hasDestination = false;
    _hasTarget = false;
    _agent.Warp(hit.position);
    ResetIdle();
    return true;
  }

  void Update() {
    // Update() only runs while enabled (and the GameObject active), so click
    // input and idle tracking are inherently gated on enabled.
    IdleSeconds += Time.deltaTime;
    HandleClick();
    CheckArrival();
  }

  void HandleClick() {
    // Input System Package (New): Active Input Handling is New-only, so the
    // legacy UnityEngine.Input API is unavailable. Null-guard covers contexts
    // with no pointer device (e.g. batch EditMode runs).
    Mouse mouse = Mouse.current;
    if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return; // W1: HUD button clicks must not move the player
    Camera cam = Camera.main;
    if (cam == null) return;
    Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
    if (Physics.Raycast(ray, out RaycastHit hit, clickMaxDistance, clickMask)) {
      // Plain click-move carries no typed target, so arrival publishes nothing.
      // A new click redirects the player and cancels any pending target arrival.
      MoveTo(hit.point);
    }
  }

  bool TrySetDestination(Vector3 destination) {
    if (_agent == null) {
      Debug.LogWarning("[ClickToMove] No NavMeshAgent attached; ignoring MoveTo.", this);
      return false;
    }
    if (!_agent.isOnNavMesh) {
      Debug.LogWarning("[ClickToMove] Agent is not on a NavMesh; ignoring MoveTo.", this);
      return false;
    }
    _agent.SetDestination(destination);
    _hasDestination = true;
    return true;
  }

  void CheckArrival() {
    if (!_hasDestination || _agent == null) return;
    if (_agent.pathPending) return;
    if (_agent.remainingDistance > _agent.stoppingDistance) return;
    if (_agent.hasPath && _agent.velocity.sqrMagnitude > 0.01f) return; // still settling
    _hasDestination = false;
    ResetIdle();
    if (!_hasTarget) return;
    _hasTarget = false;
    if (_bus == null) {
      Debug.LogWarning("[ClickToMove] Arrived but no IGameEventBus bound; NavigationCompleted dropped.", this);
      return;
    }
    _bus.Publish(new NavigationCompleted(_target, _byNpc));
  }
}
