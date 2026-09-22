// _SharedKernel/ActivityLifecycle.cs — Lead owns. P1-1 (P3.0.1 foundation).
// Reusable activity lifecycle: the explicit state machine every gameplay
// activity drives instead of hand-rolling bools (J4-class transition bugs).
// Plain C# (no MonoBehaviour, no bus, no UnityEngine): fully EditMode-testable.
// Owner is explicit (constructor arg): exactly one director owns each instance
// and is the ONLY writer; readers (router/gate/telemetry) poll State.
// Technique ADAPTED from Studio-23-xyz/InteractionSystem (MIT): explicit state
// enum + single-writer transitions. NOT imported: no singleton manager, no
// sub-interaction stack, no async/cancellation (preschool activities are
// short; speech/audio interrupts stay D-owned).
// Real consumers: MathQuestDirector (math_counting) + MarketBootstrap
// (w1 market quests). C# 9.0 only.
public enum ActivityState {
  Unavailable, // gated/locked: input rejected, no prompt
  Available,   // offered: child may enter
  Entering,    // entry beat in flight (walk-in/travel/camera)
  Ready,       // staged: prompt shown, quest not yet active
  Active,      // quest running: the ONLY input-accepting state
  Paused,      // interrupted: input held, resume or exit follows
  Completing,  // completion beat in flight (celebrate/reward/save)
  Completed,   // done: reward banked, re-entry adopts visuals
  Exiting,     // exit beat in flight (return warp/unload)
}

public sealed class ActivityLifecycle {
  public string ActivityId { get; private set; }
  public string Owner { get; private set; }
  public ActivityState State { get; private set; }
  public string LastReason { get; private set; }

  public ActivityLifecycle(string activityId, string owner) {
    ActivityId = string.IsNullOrEmpty(activityId) ? "activity" : activityId;
    Owner = string.IsNullOrEmpty(owner) ? "unknown" : owner;
    State = ActivityState.Unavailable;
    LastReason = "init";
  }

  // The ONLY input-accepting state. Router/gate poll this (via the owning
  // director's gate acquire/release), never a scattered bool.
  public bool CanAcceptInput {
    get { return State == ActivityState.Active; }
  }

  public bool MarkAvailable(string reason) {
    if (State != ActivityState.Unavailable && State != ActivityState.Completed
        && State != ActivityState.Exiting) return false;
    return Set(ActivityState.Available, reason);
  }

  public bool BeginEnter(string reason) {
    if (State != ActivityState.Available) return false;
    return Set(ActivityState.Entering, reason);
  }

  public bool MarkReady(string reason) {
    if (State != ActivityState.Entering) return false;
    return Set(ActivityState.Ready, reason);
  }

  // Talk-gated quests (math/Mia: quest starts on first talk, no staged Ready
  // beat) may jump Available -> Active directly; staged entries go via Ready.
  public bool Begin(string reason) {
    if (State != ActivityState.Ready && State != ActivityState.Available) return false;
    return Set(ActivityState.Active, reason);
  }

  public bool Pause(string reason) {
    if (State != ActivityState.Active) return false;
    return Set(ActivityState.Paused, reason);
  }

  public bool Resume(string reason) {
    if (State != ActivityState.Paused) return false;
    return Set(ActivityState.Active, reason);
  }

  public bool BeginCompleting(string reason) {
    if (State != ActivityState.Active && State != ActivityState.Paused) return false;
    return Set(ActivityState.Completing, reason);
  }

  // Event-driven completion (QuestCompletedEvent arrives while Active, before
  // any BeginCompleting call) may jump Active -> Completed directly.
  public bool MarkCompleted(string reason) {
    if (State != ActivityState.Completing && State != ActivityState.Active
        && State != ActivityState.Paused) return false;
    return Set(ActivityState.Completed, reason);
  }

  public bool BeginExit(string reason) {
    if (State != ActivityState.Completed && State != ActivityState.Active
        && State != ActivityState.Paused) return false;
    return Set(ActivityState.Exiting, reason);
  }

  // Re-entry offer after an exit (or replayable activity reset).
  public bool MarkExitedAvailable(string reason) {
    if (State != ActivityState.Exiting) return false;
    return Set(ActivityState.Available, reason);
  }

  // Failure/recovery (interrupted audio/speech/record/quit): from ANY state.
  public bool MarkUnavailable(string reason) {
    return Set(ActivityState.Unavailable, reason);
  }

  // Silent adopt for re-entry: a fresh director on a completed quest lands
  // directly in Completed without replaying the beat (J6/J7-class).
  public bool AdoptCompleted(string reason) {
    if (State == ActivityState.Completed) return true;
    return Set(ActivityState.Completed, reason);
  }

  bool Set(ActivityState next, string reason) {
    State = next;
    LastReason = string.IsNullOrEmpty(reason) ? next.ToString() : reason;
    return true;
  }
}
