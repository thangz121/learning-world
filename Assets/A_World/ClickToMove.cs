// A_World/ClickToMove.cs — Agent A (World & Visual), W0-T1.
// Click-to-move inside the constrained gameplay region (CONSTRAINED_3D.md:
// no free-360 camera, no open world). NavMesh-based movement; on arrival at a
// typed interaction target publishes NavigationCompleted(InteractionId, NpcId).
// Dependency injection ONLY via Bind(IGameEventBus). No ServiceLocator,
// no FindObjectOfType, no `new` service, no audio/speech/Worker calls.
using UnityEngine;
using UnityEngine.AI;

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

  void Update() {
    // Update() only runs while enabled (and the GameObject active), so click
    // input and idle tracking are inherently gated on enabled.
    IdleSeconds += Time.deltaTime;
    HandleClick();
    CheckArrival();
  }

  void HandleClick() {
    if (!Input.GetMouseButtonDown(0)) return;
    Camera cam = Camera.main;
    if (cam == null) return;
    Ray ray = cam.ScreenPointToRay(Input.mousePosition);
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
