// A_World/ClickRouter.cs — Agent A (World & Visual), W1 vertical slice.
// Single New-Input-System click handler for the constrained market world.
// Each frame: null-mouse returns (batch-safe); on leftButton.wasPressedThisFrame
// raycast from Camera.main:
//   (a) hit Interactable -> MoveTo(hit.point) + pending arrival-interact: while
//       pending, if player is inside pending.IsInRange(playerPos) -> Interact()
//       + vocab audio via injected IAudioDirector + clear pending.
//   (b) hit ground/other -> MoveTo(hit.point) + clear pending.
//   (c) hit IClickTarget -> MoveTo + pending arrival callback (OnClicked).
//       IClickTarget is the SharedKernel click contract (Milo/Mia presenters
//       implement it): typed GetComponentInParent, no reflection, no strings.
//   (d) clicks landing outside X[-8,8] Z[-6,6] are ignored gracefully.
// Wiring: Bind(bus, audio) + AttachPlayer(player) (both called by
// MarketBuilder.BuildServices). NEVER calls providers/Worker. C# 9.0 only.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class ClickRouter : MonoBehaviour {
  [Header("Click picking")]
  [Tooltip("Layers clickable for routing. Defaults to everything.")]
  public LayerMask clickMask = ~0;
  [Tooltip("Max raycast distance (m) for click picking.")]
  public float clickMaxDistance = 200f;
  [Header("Arrival + bounds")]
  [Tooltip("Arrival range (m) for click-target callbacks without their own Interactable.")]
  // R5V-2 (§18): 2.5 -> 1.9m from the CLICK point on the NPC body. With a
  // 0.4m body radius + ~1m click height, arrival lands ~1.6-2.0m from the NPC
  // CENTER (target 1.8m) instead of on top of the NPC.
  // R8 (player report: NPC commands fire from too far): 1.9 -> 1.5m.
  // Player report follow-up: halved again 1.5 -> 0.75m — the child walks
  // right up to Milo/Mia (close handover, faces fill the frame, taps feel
  // earned, no across-the-lawn triggering). Converges with the 0.75m
  // proximity bring (R8 principle: click + proximity meet at the counter).
  public float arrivalRange = 0.75f;
  // Phase 3.0: extended Learning World (districts at |x|<=12.2+3.3, |z|<=10+3.3).
  public float boundX = 16f;
  public float boundZ = 14f;
  // Phase 3.0.x: subject scenes live at an offset (Math +60x). The click bounds
  // recentre per active world (Bootstrap sets this on travel); default zero =
  // legacy Main behaviour, so EditMode + full-world paths are untouched.
  public Vector3 boundCenter;

  IGameEventBus _bus;
  IAudioDirector _audio;
  ClickToMove _player;

  Interactable _pendingInteract;
  bool _hasInteractPending; // explicit: Unity-destroyed targets read as null, so null alone cannot mean "none"
  IClickTarget _pendingClickTarget;
  bool _hasClickPending;
  Vector3 _pendingPoint;

  // Injection boundary (wired by MarketBuilder.BuildServices; services come from
  // GameInstaller, never newed here). MonoBehaviours use Bind, not ctors.
  public void Bind(IGameEventBus bus, IAudioDirector audio) {
    _bus = bus;
    _audio = audio;
  }

  // Player reference (same GameObject family as ClickToMove; kept separate from
  // Bind because the router commands movement but does not own the player).
  public void AttachPlayer(ClickToMove player) { _player = player; }

  // Lead introspection (also keeps the injected bus referenced, not just stored).
  public IGameEventBus Bus {
    get { return _bus; }
  }

  // Arrival is a GROUND concept (walk up TO someone): horizontal distance
  // only. Click points ride ~1m up the body while feet stay on the grass —
  // 3D distance would bake the height gap into every arrival (a 0.75m range
  // with a 0.9m height gap can never trip). Pure so tests pin it.
  public static bool InArrivalRange(Vector3 playerPos, Vector3 point, float range) {
    float dx = playerPos.x - point.x;
    float dz = playerPos.z - point.z;
    return dx * dx + dz * dz <= range * range;
  }

  public bool HasPending {
    get { return _hasInteractPending || _hasClickPending; }
  }

  // Lead introspection for ProximityDiscovery: while a click arrival for this
  // target is pending, proximity stays silent (the router fires on arrival).
  public Interactable PendingInteractTarget {
    get { return _hasInteractPending ? _pendingInteract : null; }
  }

  void Update() {
    Mouse mouse = Mouse.current;
    if (mouse == null) return; // batch-safe: no pointer device, no clicks
    if (mouse.leftButton.wasPressedThisFrame) HandleClick();
    TickPending();
  }

  void HandleClick() {
    if (_player == null) return;
    if (IsPointerOverUi()) return; // HUD button clicks must not move the player
    Camera cam = Camera.main;
    if (cam == null) return;
    Mouse mouse = Mouse.current;
    if (mouse == null) return;
    Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
    if (!Physics.Raycast(ray, out RaycastHit hit, clickMaxDistance, clickMask)) return;
    if (Mathf.Abs(hit.point.x - boundCenter.x) > boundX || Mathf.Abs(hit.point.z - boundCenter.z) > boundZ) return; // (d) ignore

    Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
    if (interactable != null) { // (a) typed world object
      _player.MoveTo(hit.point);
      SetPendingInteract(interactable);
      return;
    }

    IClickTarget clickTarget = TryGetClickTarget(hit.collider);
    if (clickTarget != null) { // (c)
      _player.MoveTo(hit.point);
      _hasInteractPending = false;
      _pendingInteract = null;
      _pendingClickTarget = clickTarget;
      _hasClickPending = true;
      _pendingPoint = hit.point;
      return;
    }

    Vector3 dest = hit.point; // (b) ground / player / scenery
    Vector3 mouth;
    if (TrySnapToGateMouth(hit.point, out mouth)) dest = mouth; // structure clicks enter
    else if (TrySnapGateOnRay(ray, hit.point, out mouth)) dest = mouth; // through-opening clicks enter
    _player.MoveTo(dest);
    ClearPending();
  }

  // Gate-click snapping (user round: clicking the arch/pillars never entered —
  // the raw hit sits inside collider geometry, the agent stalls short of the
  // 1.2m poll radius; then aiming at the corridor CENTRE still routed around
  // it instead of up the brick walkway). A scenery click aimed AT a gate
  // (within 2m of its center, entry or return) retargets to the gate MOUTH:
  // entry = corridor centre pushed 0.6m hub-side (walkway end — the walk reads
  // "up the bricks" and arrival at 0.6m is inside the trigger), return = arch
  // centre. Pure SubjectCatalog data, no hierarchy walk, EditMode-testable.
  // Radius 2m covers pillars/beam centre clicks but NOT the signpost (2.38m
  // out): inspecting the sign never force-enters.
  public static bool TrySnapToGateMouth(Vector3 hitPoint, out Vector3 mouth) {
    mouth = hitPoint;
    if (SubjectCatalog.All == null) return false;
    float best2 = 2.0f * 2.0f;
    bool found = false;
    bool isReturn = false;
    SubjectDefinition bestDef = null;
    foreach (SubjectDefinition def in SubjectCatalog.All) {
      if (def == null) continue;
      for (int i = 0; i < 2; i++) {
        Vector3 c = i == 0 ? def.GatePos : def.ReturnPoint;
        float dx = hitPoint.x - c.x;
        float dz = hitPoint.z - c.z;
        float d2 = dx * dx + dz * dz;
        if (d2 < best2) {
          best2 = d2;
          bestDef = def;
          isReturn = i == 1;
          found = true;
        }
      }
    }
    if (!found) return false;
    mouth = MouthFor(bestDef, isReturn);
    return true;
  }

  // Through-opening clicks (user round: clicking the gate MIDDLE hits district
  // ground metres behind — point-only snap misses, the walk overshoots out the
  // back; NOT a flipped entry direction, the destination just lands behind).
  // If the click RAY itself threads within 1.2m of a gate centre with the gate
  // AT or BEFORE the hit along the ray (aimed at/through the gate, never past
  // it), treat as a gate click. Pure XZ ground math, EditMode-testable.
  public static bool TrySnapGateOnRay(Ray ray, Vector3 hitPoint, out Vector3 mouth) {
    mouth = hitPoint;
    if (SubjectCatalog.All == null) return false;
    Vector3 o = new Vector3(ray.origin.x, 0f, ray.origin.z);
    Vector3 d = new Vector3(ray.direction.x, 0f, ray.direction.z);
    if (d.sqrMagnitude < 1e-6f) return false;
    d.Normalize();
    float tHit = Vector3.Dot(new Vector3(hitPoint.x, 0f, hitPoint.z) - o, d);
    float bestT = float.MaxValue;
    bool found = false;
    bool isReturn = false;
    SubjectDefinition bestDef = null;
    foreach (SubjectDefinition def in SubjectCatalog.All) {
      if (def == null) continue;
      for (int i = 0; i < 2; i++) {
        Vector3 c = i == 0 ? def.GatePos : def.ReturnPoint;
        Vector3 rel = new Vector3(c.x, 0f, c.z) - o;
        float t = Vector3.Dot(rel, d);
        if (t < 0f || t > tHit + 1.0f) continue; // gate must be at/before the hit
        Vector3 closest = o + d * t;
        float dx = closest.x - c.x;
        float dz = closest.z - c.z;
        if (dx * dx + dz * dz < 1.2f * 1.2f && t < bestT) {
          bestT = t;
          bestDef = def;
          isReturn = i == 1;
          found = true;
        }
      }
    }
    if (!found) return false;
    mouth = MouthFor(bestDef, isReturn);
    return true;
  }

  // Mouth target shared by both snappers: entry = corridor centre 0.6m toward
  // the hub (walkway end), return = arch centre.
  static Vector3 MouthFor(SubjectDefinition def, bool isReturn) {
    if (isReturn || def == null) {
      Vector3 rc = def != null ? def.ReturnPoint : Vector3.zero;
      return new Vector3(rc.x, 0f, rc.z);
    }
    // Gate mouth: corridor centre 0.6m toward the hub (== FaceOf in
    // SubjectWorldBuilder: HubCenter - GatePos, normalized).
    Vector3 toHub = SubjectCatalog.HubCenter - def.GatePos;
    toHub.y = 0f;
    if (toHub.sqrMagnitude < 0.001f) toHub = new Vector3(0f, 0f, 1f);
    toHub.Normalize();
    Vector3 m = def.GatePos + toHub * 0.6f;
    m.y = 0f;
    return m;
  }

  void TickPending() {
    if (_player == null) return;
    Vector3 playerPos = _player.transform.position;
    if (_hasInteractPending) {
      Interactable pending = _pendingInteract;
      if (pending == null) { ClearPending(); return; } // target destroyed mid-walk
      if (!pending.IsInRange(playerPos)) return;
      ClearPending();
      _player.Stop(); // arrival: halt AT range (never plow into/under the target)
      pending.Interact(); // publishes WordSeenEvent via its own bound bus
      PlayVocabFireAndForget(pending.Word);
      return;
    }
    if (!_hasClickPending) return;
    IClickTarget target = _pendingClickTarget;
    if (target == null) { ClearPending(); return; } // destroyed mid-walk
    MonoBehaviour targetBehaviour = target as MonoBehaviour;
    if (targetBehaviour == null) { ClearPending(); return; }
    if (!InArrivalRange(playerPos, _pendingPoint, arrivalRange)) return;
    ClearPending();
    _player.Stop(); // arrival: halt AT range before the callback
    try {
      target.OnClicked();
    } catch (Exception e) {
      Debug.LogWarning("[ClickRouter] OnClicked callback failed: " + e.Message, this);
    }
    // Player rule: every clickable reads its name. Interactables speak
    // through their own arrival-interact path; IClickTarget NPCs speak for
    // themselves (Milo/Mia voice lines) — EXCEPT the wordless distractor
    // prop, which borrows the shared vocab channel here (same-assembly).
    try {
      DistractorChoice distractor = target as DistractorChoice;
      if (distractor != null) PlayVocabFireAndForget(distractor.SpeakWord);
    } catch (Exception e) {
      Debug.LogWarning("[ClickRouter] Distractor readout failed: " + e.Message, this);
    }
  }

  void SetPendingInteract(Interactable interactable) {
    _pendingInteract = interactable;
    _hasInteractPending = true;
    _pendingClickTarget = null;
    _hasClickPending = false;
  }

  void ClearPending() {
    _pendingInteract = null;
    _hasInteractPending = false;
    _pendingClickTarget = null;
    _hasClickPending = false;
  }

  // Typed SharedKernel click contract (B presenters implement IClickTarget).
  static IClickTarget TryGetClickTarget(Collider hit) {
    if (hit == null) return null;
    return hit.GetComponentInParent<IClickTarget>();
  }

  static bool IsPointerOverUi() {
    EventSystem events = EventSystem.current;
    if (events == null) return false;
    return events.IsPointerOverGameObject();
  }

  // Fire-and-forget vocab audio: Update cannot await, and a slow/failed fetch
  // must never block movement. Mode Normal (pronunciation clarity stays D-owned).
  async void PlayVocabFireAndForget(WordId word) {
    if (_audio == null) return;
    try {
      await _audio.PlayVocabularyAsync(word, VocabularyAudioMode.Normal);
    } catch (Exception e) {
      Debug.LogWarning("[ClickRouter] Vocabulary audio failed: " + e.Message, this);
    }
  }

  // Test seam: route one raycast hit through the same priority logic as Update.
  // (Lead/tests drive this without needing a live mouse + camera.)
  public void RouteHitForTests(Collider hit, Vector3 point) {
    if (_player == null || hit == null) return;
    if (Mathf.Abs(point.x - boundCenter.x) > boundX || Mathf.Abs(point.z - boundCenter.z) > boundZ) return;
    Interactable interactable = hit.GetComponentInParent<Interactable>();
    if (interactable != null) {
      _player.MoveTo(point);
      SetPendingInteract(interactable);
      return;
    }
    IClickTarget clickTarget = TryGetClickTarget(hit);
    if (clickTarget != null) {
      _player.MoveTo(point);
      _pendingInteract = null;
      _hasInteractPending = false;
      _pendingClickTarget = clickTarget;
      _hasClickPending = true;
      _pendingPoint = point;
      return;
    }
    Vector3 dest = point;
    Vector3 mouth2;
    if (TrySnapToGateMouth(point, out mouth2)) dest = mouth2;
    _player.MoveTo(dest);
    ClearPending();
  }
}
