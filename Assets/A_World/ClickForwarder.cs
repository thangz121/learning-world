// A_World/ClickForwarder.cs — Agent A (World & Visual). Reusable click proxy:
// makes bulky scenery (a shop counter) answer as its NPC (the shopkeeper) so
// players never need pixel taps on bodies partially hidden behind geometry.
// Implements the SharedKernel IClickTarget contract and forwards to a wired
// target (Lead wires the Brain presenter as IClickTarget; World never
// references Brain types). Quest/hint reactions live in the real target, so
// retry/dead-end semantics are unchanged. C# 9.0 only.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class ClickForwarder : MonoBehaviour, IClickTarget {
  IClickTarget _target;

  // Injection boundary (Lead wires the presenter's IClickTarget after build).
  public void Bind(IClickTarget target) { _target = target; }

  // IClickTarget entry point for the router (arrival-gated like any NPC).
  public void OnClicked() {
    IClickTarget target = _target;
    if (target == null) return;
    MonoBehaviour behaviour = target as MonoBehaviour;
    if (behaviour == null) return;
    try {
      target.OnClicked();
    } catch (Exception e) {
      Debug.LogWarning("[ClickForwarder] Forwarded OnClicked failed: " + e.Message, this);
    }
  }
}
