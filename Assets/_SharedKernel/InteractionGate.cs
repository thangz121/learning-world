// _SharedKernel/InteractionGate.cs — Lead owns. P1-6 (P3.0.1 foundation).
// ONE input lock for world interaction, replacing scattered booleans
// (_travelLock is async reentrancy only; presenters had no lock at all).
// Channels (P3.0.1 §17): World (router clicks), Npc/Activity (proximity
// brings), Camera (beats). UI is DELIBERATELY absent: dialogs are
// click-through by rule (J1 fix: card/dim/text raycastTarget=false), so an
// open setup prompt must NEVER block world taps — the child can never be
// trapped by a parent-facing dialog (O1 stays open AND harmless).
// Locks: transition (travel/unload in flight) + activity (Completing/Exiting
// beat in flight, driven by ActivityLifecycle owners). Either blocks world
// routing. Owner strings make the blocker debuggable (no anonymous bool).
// Plain C# (no MonoBehaviour/bus): EditMode-testable. Owner: MarketBootstrap
// creates ONE instance and pushes it into Router + SubjectGates; directors
// acquire/release the activity side around completion beats.
// Real consumers: ClickRouter + SubjectGate (both directions) + directors.
// C# 9.0 only.
using System.Collections.Generic;

public sealed class InteractionGate {
  readonly HashSet<string> _transitions = new HashSet<string>();
  readonly HashSet<string> _activities = new HashSet<string>();

  public bool TransitionBusy {
    get { return _transitions.Count > 0; }
  }

  public bool ActivityBusy {
    get { return _activities.Count > 0; }
  }

  // World clicks route only when NEITHER side is held.
  public bool CanRouteWorld {
    get { return _transitions.Count == 0 && _activities.Count == 0; }
  }

  public void BeginTransition(string owner) {
    if (string.IsNullOrEmpty(owner)) owner = "transition";
    _transitions.Add(owner);
  }

  public void EndTransition(string owner) {
    if (string.IsNullOrEmpty(owner)) owner = "transition";
    _transitions.Remove(owner);
  }

  public void BeginActivity(string owner) {
    if (string.IsNullOrEmpty(owner)) owner = "activity";
    _activities.Add(owner);
  }

  public void EndActivity(string owner) {
    if (string.IsNullOrEmpty(owner)) owner = "activity";
    _activities.Remove(owner);
  }

  // Test/debug: who holds the gate right now ("" = free).
  public string Blocker() {
    foreach (string o in _transitions) return "transition:" + o;
    foreach (string o in _activities) return "activity:" + o;
    return "";
  }
}
