// B_Brain/HintService.cs — Agent B (W0-T1). Per-quest hint ladder 0..4.
// Plain C# service, deterministic. Constructed ONLY by GameInstaller:
//   new HintService(IGameEventBus)
// Implements frozen IHintService plus W0-T1 extras: Demo(q) -> L3, Simplify(q) -> L4.
//
// Ladder: 0 none, 1 visual_glow, 2 milo_point, 3 milo_demo, 4 auto_simplify.
//   ReportWrong: WrongCount 3->L1, 5->L2, 7->L3, >=9->L4 (publish HintLevelChanged on change).
//   Tick(q, dt, totalIdle): totalIdle>=8 -> L1, >=15 -> L2 (publish on change).
//   Demo / Simplify: explicit Agent-B "child is stuck" decision -> L3 / L4 (publish on change).
//   Reset: clears WrongCount/IdleSec/Level (no carry, no publish; the next quest
//     announces itself via QuestStartedEvent).
// Levels are monotonic per quest (only Reset lowers), so idle- and wrong-driven
// hints compose: the child always sees the highest level earned so far.
using System;
using System.Collections.Generic;

public sealed class HintService : IHintService {
  readonly IGameEventBus _bus;
  readonly Dictionary<QuestId, QuestHintState> _states = new Dictionary<QuestId, QuestHintState>();

  const float IdleL1Sec = 8f;
  const float IdleL2Sec = 15f;

  public HintService(IGameEventBus bus) {
    _bus = bus;
  }

  public QuestHintState GetState(QuestId q) {
    return GetOrCreate(q);
  }

  public void ReportWrong(QuestId q) {
    QuestHintState s = GetOrCreate(q);
    s.WrongCount++;
    int target = s.WrongCount >= 9 ? 4
      : s.WrongCount >= 7 ? 3
      : s.WrongCount >= 5 ? 2
      : s.WrongCount >= 3 ? 1 : s.Level;
    SetLevel(s, target);
  }

  public void Tick(QuestId q, float deltaSec, float totalIdleSec) {
    QuestHintState s = GetOrCreate(q);
    if (deltaSec > 0f) s.IdleSec += deltaSec;
    int target = totalIdleSec >= IdleL2Sec ? 2
      : totalIdleSec >= IdleL1Sec ? 1 : s.Level;
    SetLevel(s, target);
  }

  // Agent B decides the child is stuck: show a demo (L3).
  public void Demo(QuestId q) {
    SetLevel(GetOrCreate(q), 3);
  }

  // Agent B decides the child is still stuck: simplify the task (L4).
  public void Simplify(QuestId q) {
    SetLevel(GetOrCreate(q), 4);
  }

  public void Reset(QuestId q) {
    QuestHintState s = GetOrCreate(q);
    s.WrongCount = 0;
    s.IdleSec = 0f;
    s.Level = 0;
  }

  QuestHintState GetOrCreate(QuestId q) {
    QuestHintState s;
    if (!_states.TryGetValue(q, out s)) {
      s = new QuestHintState { Id = q, WrongCount = 0, IdleSec = 0f, Level = 0 };
      _states[q] = s;
    }
    return s;
  }

  void SetLevel(QuestHintState s, int target) {
    if (target < s.Level) target = s.Level; // monotonic ladder; only Reset lowers.
    if (target == s.Level) return;          // publish ONLY on change.
    s.Level = target;
    _bus.Publish(new HintLevelChanged(s.Id, target));
  }
}
