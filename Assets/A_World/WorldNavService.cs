// A_World/WorldNavService.cs — Phase 3.0 WORLD FOUNDATION (Agent A).
// In-memory world-navigation state (Scene/Session lifetime, like QuestManager).
// Tracks CurrentWorld/CurrentSubject ONLY — no Lesson/Topic/Question/Answer/
// Progress/Completion here (that is Learning State, phases 3.1+, elsewhere).
// Idempotent: entering the current world publishes NOTHING, so gate polling,
// trigger spam and rapid re-entry are safe by construction. ONE instance,
// created ONLY by GameInstaller (same only-new rule as every other service).
// Plain C# (no MonoBehaviour): no Update, no allocation per frame, no leak
// surface — transitions create/destroy ZERO GameObjects. C# 9.0 only.
using System;

public class WorldNavService : IWorldNavService {
  readonly IGameEventBus _bus;

  SubjectId _current = SubjectIds.Main;

  public WorldNavService(IGameEventBus bus) {
    _bus = bus;
  }

  public SubjectId Current {
    get { return _current; }
  }

  public bool IsInSubject {
    get { return _current != SubjectIds.Main; }
  }

  // Enter a subject (or Main). No-op when already there: no event, no work.
  public void Enter(SubjectId subject) {
    if (subject.Value == null) return;
    if (_current == subject) return;
    SubjectId from = _current;
    _current = subject;
    if (_bus != null) {
      try { _bus.Publish(new WorldChangedEvent(from, subject, DateTime.UtcNow)); }
      catch (Exception) {
        // Navigation state is already committed above; a bus failure must
        // never corrupt it. Presentation catches up on the next change.
      }
    }
  }

  public void ReturnToMain() {
    Enter(SubjectIds.Main);
  }
}
