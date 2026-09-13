// A_World/Interactable.cs — Agent A (World & Visual), W0-T1.
// World-object interaction boundary: Inspector strings are parsed to typed IDs
// (WordId / InteractionId / NpcId) at Awake and never compared as raw strings.
// Publishes WordSeenEvent(wordId, LearnSource.Object, UtcNow) via injected bus.
// No audio/speech/Worker calls, no `new` service.
// W1: OnMouseDown removed — Active Input Handling is New-only, so the legacy
// click path is dead. All clicks route through ClickRouter (New Input System),
// which calls Interact() on arrival. This GameObject still needs its Collider
// for the router's raycast.
// Note: this GameObject needs a Collider so ClickRouter raycasts can hit it.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class Interactable : MonoBehaviour {
  [Header("Typed identity (raw strings; parsed to structs at the Awake boundary)")]
  public string wordId = "";
  public string interactionId = "";
  public string npcId = "";
  [Header("Interaction")]
  [Tooltip("Max distance (m) for a valid interaction. Range check stub for W0-T1.")]
  public float interactionDistance = 2.5f;

  public WordId Word { get; private set; }
  public InteractionId Interaction { get; private set; }
  public NpcId Npc { get; private set; }
  public bool HasWord { get; private set; }

  IGameEventBus _bus;

  // Injection boundary (wired by GameInstaller). MonoBehaviours cannot use
  // constructor injection (Unity instantiates them), so Bind is the pattern.
  public void Bind(IGameEventBus bus) { _bus = bus; }

  void Awake() {
    ParseIds();
  }

  // Boundary: raw strings become typed IDs. Awake parses the Inspector-time
  // values; code-built setup (MarketBuilder assigns fields AFTER AddComponent,
  // when Awake has already run) MUST call this again after assignment —
  // otherwise HasWord stays false and Interact() silently drops every event.
  // Tests use the same hook (set fields, then ParseIds).
  public void ParseIds() {
    HasWord = false;
    if (!string.IsNullOrWhiteSpace(wordId)) {
      Word = new WordId(wordId);
      HasWord = true;
    }
    if (!string.IsNullOrWhiteSpace(interactionId)) Interaction = new InteractionId(interactionId);
    if (!string.IsNullOrWhiteSpace(npcId)) Npc = new NpcId(npcId);
  }

  // Click-to-move arrival callback path (called when the player reaches this object).
  public void Interact() {
    if (!HasWord) {
      Debug.LogWarning("[Interactable] No wordId configured; WordSeenEvent dropped.", this);
      return;
    }
    if (_bus == null) {
      Debug.LogWarning("[Interactable] No IGameEventBus bound; WordSeenEvent dropped.", this);
      return;
    }
    _bus.Publish(new WordSeenEvent(Word, LearnSource.Object, DateTime.UtcNow));
  }

  // Range check stub (W0-T1): distance-only check against interactionDistance.
  public bool IsInRange(Vector3 requesterPosition) {
    return Vector3.Distance(requesterPosition, transform.position) <= interactionDistance;
  }
}
