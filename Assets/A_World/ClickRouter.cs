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
  // R8 (player report: NPC commands fire from too far): 1.9 -> 1.5m — the
  // child walks right up to Milo/Mia/the ball (arrival lands ~1.2-1.6m from
  // center: conversational distance, faces readable, taps feel earned).
  public float arrivalRange = 1.5f;
  public float boundX = 8f;
  public float boundZ = 6f;

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
    if (Mathf.Abs(hit.point.x) > boundX || Mathf.Abs(hit.point.z) > boundZ) return; // (d) ignore

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

    _player.MoveTo(hit.point); // (b) ground / player / scenery
    ClearPending();
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
    if (Vector3.Distance(playerPos, _pendingPoint) > arrivalRange) return;
    ClearPending();
    _player.Stop(); // arrival: halt AT range before the callback
    try {
      target.OnClicked();
    } catch (Exception e) {
      Debug.LogWarning("[ClickRouter] OnClicked callback failed: " + e.Message, this);
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
    if (Mathf.Abs(point.x) > boundX || Mathf.Abs(point.z) > boundZ) return;
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
    _player.MoveTo(point);
    ClearPending();
  }
}
