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
  // P1-5 explicit re-arm (J4 lesson O2): after a fire the gate stays DISARMED
  // until the player leaves the re-arm radius. Landing a warp INSIDE the fire
  // radius (return path) can therefore never latch _wasInside and block the
  // next entry — the gate re-arms on DISTANCE, not on a single stale sample.
  // P1-6: optional InteractionGate (transition busy blocks firing).
  InteractionGate _gate;
  bool _armed = true;
  public float rearmRadius = 2.2f; // must exceed fireRadius (validated in Bind)

  // Injection boundary (wired by MarketBuilder.SetWorldNav; services come from
  // GameInstaller, never newed here). Binds are explicit, never discovered.
  public void Bind(IWorldNavService nav, SubjectId target, bool isReturnGate, Transform player) {
    _nav = nav;
    _target = target;
    _isReturnGate = isReturnGate;
    _playerT = player;
    _cooldownUntil = 0f;
    _wasInside = false;
    _armed = true;
    if (rearmRadius <= fireRadius) rearmRadius = fireRadius + 1.0f;
  }

  // P1-6 additive seam (no signature break): push the shared gate in after
  // Bind. Null = legacy behaviour (fires regardless of transition state).
  public void BindGate(InteractionGate gate) { _gate = gate; }

  // Explicit re-arm request (travel code calls this after a warp): the next
  // Update re-derives armed state from distance, so a warp landing inside the
  // fire radius starts disarmed and re-arms only after walking clear.
  public void NotifyWarpedAway() {
    _armed = false;
    _wasInside = true;
  }

  public SubjectId Target {
    get { return _target; }
  }

  public bool IsReturnGate {
    get { return _isReturnGate; }
  }

  void Update() {
    if (_nav == null || _playerT == null) return;
    if (_gate != null && !_gate.CanRouteWorld) {
      // Transition/activity beat in flight: freeze edge detection (never fire
      // on a stale sample) but keep tracking presence so the edge is clean.
      _wasInside = IsInside(_playerT.position);
      return;
    }
    if (Time.time < _cooldownUntil) { _wasInside = IsInside(_playerT.position); return; }
    Vector3 p = _playerT.position;
    // P1-5: disarmed gates re-arm ONLY outside the re-arm radius.
    if (!_armed) {
      if (!IsInsideRadius(p, rearmRadius)) _armed = true;
      _wasInside = IsInside(p);
      return;
    }
    bool inside = IsInside(p);
    bool entered = inside && !_wasInside;
    _wasInside = inside;
    if (!entered) return;
    if (_isReturnGate) {
      // Return arch: only meaningful while inside its own subject.
      if (_nav.Current == _target) {
        _nav.ReturnToMain();
        _cooldownUntil = Time.time + cooldownSec;
        _armed = false; // walk clear before the next fire
      }
    } else {
      // Entry gate: fires whenever the player is NOT already in this subject
      // (from Main, or walking over from another subject). Entering the
      // current subject is an idempotent no-op, so walking out through the
      // same gate can never ping-pong and trigger spam can never double-fire.
      if (_nav.Current != _target) {
        _nav.Enter(_target);
        _cooldownUntil = Time.time + cooldownSec;
        _armed = false; // walk clear before the next fire
      }
    }
  }

  bool IsInside(Vector3 playerPos) {
    return IsInsideRadius(playerPos, fireRadius);
  }

  bool IsInsideRadius(Vector3 playerPos, float radius) {
    float dx = playerPos.x - transform.position.x;
    float dz = playerPos.z - transform.position.z;
    return dx * dx + dz * dz <= radius * radius;
  }

  // Deterministic test seam (same one-way + idempotency + re-arm rules as
  // Update, without needing a live frame or cooldown clock).
  public bool TryFireForTests(Vector3 playerPos, SubjectId currentWorld) {
    if (_gate != null && !_gate.CanRouteWorld) return false; // transition beat: frozen
    if (!_armed) {
      if (!IsInsideRadius(playerPos, rearmRadius)) _armed = true;
      else return false; // still inside: must walk clear first (J4 re-entry case)
    }
    if (!_isReturnGate) {
      if (currentWorld == _target) return false; // already inside: no-op
      float dx = playerPos.x - transform.position.x;
      float dz = playerPos.z - transform.position.z;
      if (dx * dx + dz * dz > fireRadius * fireRadius) return false;
      if (_nav != null) _nav.Enter(_target);
      _armed = false;
      return true;
    } else {
      if (currentWorld != _target) return false;
      float dx = playerPos.x - transform.position.x;
      float dz = playerPos.z - transform.position.z;
      if (dx * dx + dz * dz > fireRadius * fireRadius) return false;
      if (_nav != null) _nav.ReturnToMain();
      _armed = false;
      return true;
    }
  }
}
