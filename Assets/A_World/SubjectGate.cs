// A_World/SubjectGate.cs — Phase 3.0 WORLD FOUNDATION (Agent A).
// Dumb world-space gate trigger: when the PLAYER walks into its radius, it
// calls the world-nav service exactly once per entry (rising edge + cooldown).
// One-way by construction: an entry gate fires ONLY from Main, a return gate
// fires ONLY from its subject — walking back through the same gate can never
// ping-pong, and spamming movement can never double-fire (service is
// idempotent on top). Polling (like ProximityDiscovery), NOT physics triggers:
// no Rigidbody is added to the player, no trigger colliders, zero change to
// the frozen player/NPC physics. No Update allocation, no events published
// here (the service owns WorldChangedEvent). C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class SubjectGate : MonoBehaviour {
  // Fire radius (m, horizontal XZ): comfortably inside the 1.8m road so the
  // child passes through it, never around it.
  public float fireRadius = 1.2f;
  // Cooldown after a successful fire (s): lets the walker clear the radius.
  public float cooldownSec = 1.0f;

  IWorldNavService _nav;
  SubjectId _target = SubjectIds.Main;
  bool _isReturnGate;
  Transform _playerT;
  float _cooldownUntil;
  bool _wasInside;

  // Injection boundary (wired by MarketBuilder.SetWorldNav; services come from
  // GameInstaller, never newed here). Binds are explicit, never discovered.
  public void Bind(IWorldNavService nav, SubjectId target, bool isReturnGate, Transform player) {
    _nav = nav;
    _target = target;
    _isReturnGate = isReturnGate;
    _playerT = player;
    _cooldownUntil = 0f;
    _wasInside = false;
  }

  public SubjectId Target {
    get { return _target; }
  }

  public bool IsReturnGate {
    get { return _isReturnGate; }
  }

  void Update() {
    if (_nav == null || _playerT == null) return;
    if (Time.time < _cooldownUntil) { _wasInside = IsInside(_playerT.position); return; }
    bool inside = IsInside(_playerT.position);
    bool entered = inside && !_wasInside;
    _wasInside = inside;
    if (!entered) return;
    if (_isReturnGate) {
      // Return arch: only meaningful while inside its own subject.
      if (_nav.Current == _target) {
        _nav.ReturnToMain();
        _cooldownUntil = Time.time + cooldownSec;
      }
    } else {
      // Entry gate: fires whenever the player is NOT already in this subject
      // (from Main, or walking over from another subject). Entering the
      // current subject is an idempotent no-op, so walking out through the
      // same gate can never ping-pong and trigger spam can never double-fire.
      if (_nav.Current != _target) {
        _nav.Enter(_target);
        _cooldownUntil = Time.time + cooldownSec;
      }
    }
  }

  bool IsInside(Vector3 playerPos) {
    float dx = playerPos.x - transform.position.x;
    float dz = playerPos.z - transform.position.z;
    return dx * dx + dz * dz <= fireRadius * fireRadius;
  }

  // Deterministic test seam (same one-way + idempotency rules as Update,
  // without needing a live frame or cooldown clock).
  public bool TryFireForTests(Vector3 playerPos, SubjectId currentWorld) {
    if (!_isReturnGate) {
      if (currentWorld == _target) return false; // already inside: no-op
      float dx = playerPos.x - transform.position.x;
      float dz = playerPos.z - transform.position.z;
      if (dx * dx + dz * dz > fireRadius * fireRadius) return false;
      if (_nav != null) _nav.Enter(_target);
      return true;
    } else {
      if (currentWorld != _target) return false;
      float dx = playerPos.x - transform.position.x;
      float dz = playerPos.z - transform.position.z;
      if (dx * dx + dz * dz > fireRadius * fireRadius) return false;
      if (_nav != null) _nav.ReturnToMain();
      return true;
    }
  }
}
