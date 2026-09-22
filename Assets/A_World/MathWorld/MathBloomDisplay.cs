// A_World/MathWorld/MathBloomDisplay.cs — Phase 3.0.x S3B. math_bloom consumer.
// Producer: QuestRewardService banks the "math_bloom" world-change id on
// math_counting completion (P36D pins the ledger). Consumer (this file): the
// garden blooms pre-built HIDDEN by MathWorldBuilder flip visible on the SAME
// QuestCompletedEvent — the completion reward reads as a world change in situ.
// Bus-only wiring (no direct refs), unload-safe (lives under the MathWorld
// root, disposes on disable). Emits one dev-truth log line so S5 log review
// can prove the event crossed runtime (players see blooms, never the log).
// C# 9.0 only.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class MathBloomDisplay : MonoBehaviour {
  static readonly QuestId MathQuest = new QuestId("math_counting");

  IGameEventBus _bus;
  IQuestService _quests;
  IDisposable _sub;
  bool _built;

  public bool BloomShown { get; private set; }

  // Injection boundary (GameInstaller calls this on MathScene load, on the
  // bloom-root GO the builder exposes). Blooms are CHILDREN of this GO.
  // B1-journey fix (P2): the consumer must also ADOPT an already-completed
  // quest — a fresh re-entry after completion never receives the event, so
  // the reward visual used to vanish on the second visit.
  public void Build(IGameEventBus bus, IQuestService quests) {
    if (_built) return;
    _built = true;
    _bus = bus;
    _quests = quests;
    try {
      if (_quests != null && _quests.GetState(MathQuest) != null
          && _quests.GetState(MathQuest).Completed) {
        ApplyBlooms();
      }
    } catch (Exception) { }
    if (_bus == null) return;
    try { _sub = _bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted); }
    catch (Exception) { }
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    if (e.QuestId.Value != MathQuest.Value) return;
    ApplyBlooms();
  }

  void ApplyBlooms() {
    if (BloomShown) return; // idempotent: event + adopt can never double-pop
    BloomShown = true;
    try {
      // S4 celebration pop: one-shot 1.4x scale (deterministic state change,
      // no particle system — see S4 GitHub verdict: packages disproportionate
      // for a pilot reward beat).
      foreach (Transform child in transform) {
        if (child == null) continue;
        child.gameObject.SetActive(true);
        child.localScale = child.localScale * 1.4f;
      }
    } catch (Exception) { }
    try { Debug.Log("[MathBloom] applied math_bloom (garden blooms shown).", this); }
    catch (Exception) { }
  }

  void OnDisable() {
    if (_sub == null) return;
    try { _sub.Dispose(); } catch (Exception) { }
    _sub = null;
  }
}
