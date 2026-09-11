// A_World/Interactable.cs — Agent A (World & Visual), W0-T1.
// World-object interaction boundary: Inspector strings are parsed to typed IDs
// (WordId / InteractionId / NpcId) at Awake and never compared as raw strings.
// Publishes WordSeenEvent(wordId, LearnSource.Object, UtcNow) via injected bus.
// No audio/speech/Worker calls, no `new` service.
// Note: OnMouseDown requires a Collider on this GameObject to receive clicks.
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
    // Boundary: raw strings become typed IDs immediately; empty strings stay unset.
    // (WordId/NpcId constructors throw on null, so guard first.)
    if (!string.IsNullOrWhiteSpace(wordId)) {
      Word = new WordId(wordId);
      HasWord = true;
    }
    if (!string.IsNullOrWhiteSpace(interactionId)) Interaction = new InteractionId(interactionId);
    if (!string.IsNullOrWhiteSpace(npcId)) Npc = new NpcId(npcId);
  }

  void OnMouseDown() { Interact(); }

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
