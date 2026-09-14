// B_Brain/QuestManager.cs — Agent B (W0-T1). Quest progression, one objective at a time.
// Plain C# service, deterministic. Constructed by GameInstaller:
//   new QuestManager(EventBus, Learning, Hints)
// The optional 4th ctor arg is test-only (fake content); production uses the
// built-in provider mirroring Content/quests/market_help_mia.json, so the
// GameInstaller 3-arg call keeps compiling.
// Implements frozen IQuestService plus W0-T1 helpers:
//   AdvanceOnSeen(word) — Find path ONLY: reports WordSeen to learning, advances
//     only when the current objective is a (Find, word) match. Bring/Give/Select
//     NEVER advance here (explicit ReportAction path, e.g. bring needs Mia);
//     Speak advances only via AdvanceOnSpoken.
//   AdvanceOnSpoken(word, level) — Speak path: reports to learning, advances only
//     the current (Speak, word) objective on SpeechLevel Great+.
// Completion is published ONLY via _bus.Publish(new QuestCompletedEvent(...)).
// No C# event (CT-006). No string questId/action in logic: raw strings live only
// in ObjectiveData/QuestData fields and are parsed to QuestId/PlayerAction/WordId
// at the content boundary (CT-007).
using System;
using System.Collections.Generic;

// Content boundary (Agent B defines this interface per W0-T1).
public interface IQuestContentProvider {
  QuestData Get(QuestId questId);
}

[Serializable]
public sealed class ObjectiveData {
  public string id;     // raw, e.g. "find_apple"
  public string action; // raw, e.g. "find" (parsed at boundary, case-insensitive)
  public string target; // raw word id, e.g. "apple" (parsed at boundary)

  public PlayerAction Action {
    get {
      PlayerAction a;
      if (Enum.TryParse<PlayerAction>(action, true, out a)) return a;
      throw new ArgumentException("Unknown quest action '" + action + "' (objective '" + id + "').");
    }
  }

  public WordId Target {
    get { return new WordId(target); }
  }
}

[Serializable]
public sealed class QuestData {
  public string id; // raw, e.g. "market_help_mia" (parsed at boundary)
  public List<ObjectiveData> objectives = new List<ObjectiveData>();
  public int rewardFriendshipMia; // raw, mirrors Content quest reward.friendship_mia
  public string rewardWorldChange; // raw, mirrors Content quest reward.world_change

  public QuestId QuestId {
    get { return new QuestId(id); }
  }
}

public sealed class QuestManager : IQuestService {
  readonly IGameEventBus _bus;
  readonly ILearningService _learning;
  readonly IHintService _hints;
  readonly IQuestContentProvider _content;

  readonly Dictionary<QuestId, QuestState> _states = new Dictionary<QuestId, QuestState>();
  readonly Dictionary<QuestId, List<TypedObjective>> _objectives = new Dictionary<QuestId, List<TypedObjective>>();
  QuestId? _active;

  sealed class TypedObjective {
    public PlayerAction Action;
    public WordId Target;
  }

  public QuestManager(IGameEventBus bus, ILearningService learning, IHintService hints, IQuestContentProvider provider = null) {
    _bus = bus;
    _learning = learning;
    _hints = hints;
    _content = provider ?? new BuiltInQuestContentProvider();
  }

  public void StartQuest(QuestId questId) {
    _states[questId] = new QuestState { Id = questId, ObjectiveIndex = 0, Completed = false };
    _objectives[questId] = LoadTyped(questId);
    _active = questId;
    _hints.Reset(questId);
    _bus.Publish(new QuestStartedEvent(questId, DateTime.UtcNow));
  }

  public void ReportAction(PlayerAction action, WordId target) {
    if (!_active.HasValue) return;
    TryAdvance(_active.Value, action, target);
  }

  // Interact/WordSeen path (Find ONLY). Never advances Bring/Give/Select/Speak:
  // those need the explicit ReportAction path, so re-clicking a seen word can
  // never complete them (e.g. clicking the apple twice must not finish bring).
  public void AdvanceOnSeen(WordId word) {
    _learning.ReportSeen(word, LearnSource.Quest);
    if (!_active.HasValue) return;
    QuestId q = _active.Value;
    TypedObjective cur = CurrentObjective(q);
    if (cur != null && cur.Action == PlayerAction.Find && cur.Target.Value == word.Value)
      TryAdvance(q, cur.Action, word);
  }

  // ReportSpoken path. Advances the current (Speak, word) objective only on Great+.
  public void AdvanceOnSpoken(WordId word, SpeechLevel level) {
    _learning.ReportSpoken(word, level, LearnSource.Quest);
    if (level != SpeechLevel.Perfect && level != SpeechLevel.Great) return;
    if (!_active.HasValue) return;
    QuestId q = _active.Value;
    TypedObjective cur = CurrentObjective(q);
    if (cur != null && cur.Action == PlayerAction.Speak && cur.Target.Value == word.Value)
      TryAdvance(q, PlayerAction.Speak, word);
  }

  public QuestState GetState(QuestId questId) {
    QuestState s;
    if (_states.TryGetValue(questId, out s)) return s;
    return new QuestState { Id = questId, ObjectiveIndex = 0, Completed = false };
  }

  TypedObjective CurrentObjective(QuestId q) {
    QuestState s;
    List<TypedObjective> list;
    if (!_states.TryGetValue(q, out s) || s.Completed) return null;
    if (!_objectives.TryGetValue(q, out list)) return null;
    if (s.ObjectiveIndex < 0 || s.ObjectiveIndex >= list.Count) return null;
    return list[s.ObjectiveIndex];
  }

  bool TryAdvance(QuestId q, PlayerAction action, WordId target) {
    TypedObjective cur = CurrentObjective(q);
    if (cur == null) return false;
    if (cur.Action != action || cur.Target.Value != target.Value) return false;
    QuestState s = _states[q];
    s.ObjectiveIndex++;
    List<TypedObjective> list = _objectives[q];
    if (s.ObjectiveIndex >= list.Count) {
      s.Completed = true;
      _bus.Publish(new QuestCompletedEvent(q, DateTime.UtcNow));
    }
    return true;
  }

  List<TypedObjective> LoadTyped(QuestId questId) {
    var list = new List<TypedObjective>();
    QuestData data = _content.Get(questId);
    if (data == null || data.objectives == null) return list;
    foreach (ObjectiveData raw in data.objectives) {
      if (raw == null) continue;
      try {
        var t = new TypedObjective { Action = raw.Action, Target = raw.Target };
        if (t.Target.Value.Length == 0) continue;
        list.Add(t);
      } catch (ArgumentException) {
        continue; // unparseable objective: skip, never crash the quest.
      }
    }
    return list;
  }

  // Fallback content mirroring Content/quests/w1_mia_apple.json and
  // Content/quests/market_help_mia.json (source of truth stays the JSON; this
  // keeps the GameInstaller 3-arg path working with zero file IO).
  sealed class BuiltInQuestContentProvider : IQuestContentProvider {
    public QuestData Get(QuestId questId) {
      if (questId.Value == "w1_mia_apple") {
        return new QuestData {
          id = "w1_mia_apple",
          objectives = new List<ObjectiveData> {
            new ObjectiveData { id = "find_apple", action = "find", target = "apple" },
            new ObjectiveData { id = "bring_apple", action = "bring", target = "apple" }
          }
        };
      }
      if (questId.Value == "w1_mia_ball") {
        return new QuestData {
          id = "w1_mia_ball",
          objectives = new List<ObjectiveData> {
            new ObjectiveData { id = "find_ball", action = "find", target = "ball" },
            new ObjectiveData { id = "bring_ball", action = "bring", target = "ball" }
          }
        };
      }
      if (questId.Value != "market_help_mia") return null;
      return new QuestData {
        id = "market_help_mia",
        objectives = new List<ObjectiveData> {
          new ObjectiveData { id = "find_apple", action = "find", target = "apple" },
          new ObjectiveData { id = "bring_apple", action = "bring", target = "apple" },
          new ObjectiveData { id = "speak_apple", action = "speak", target = "apple" }
        }
      };
    }
  }
}
