// _SharedKernel/ActivityCompletion.cs — Lead owns. P1-7 (P3.0.1 foundation).
// Staged completion persistence: the SAVE half of the completion contract.
// Quest RULES stay in QuestManager; reward STATE stays in QuestRewardService;
// this helper is the single place that banks a completed quest into the save
// file (previously NOBODY appended to questsDone, so P1-4 replay had nothing
// to replay and every completion was lost on restart).
// Called centrally by GameInstaller on QuestCompletedEvent (covers ALL quests,
// present + future — directors never hand-roll save code). Format unchanged
// (LocalSave JSON frozen): only the questsDone list grows. C# 9.0 only.
using System.Collections.Generic;

public static class ActivityCompletion {
  // Banks questId into save.QuestsDone (idempotent) and writes the file.
  // Returns true when the save now contains the quest. Never throws: save IO
  // must never break the celebration beat (fail-soft like LocalSave.Load).
  public static bool PersistQuestDone(ISaveService save, QuestId questId) {
    if (save == null) return false;
    string id = questId.Value;
    if (string.IsNullOrEmpty(id)) return false;
    try {
      PlayerProgress p = save.Load();
      if (p == null) p = new PlayerProgress();
      if (p.QuestsDone == null) p.QuestsDone = new List<string>();
      bool found = false;
      foreach (string q in p.QuestsDone) {
        if (string.Equals(q, id, System.StringComparison.OrdinalIgnoreCase)) { found = true; break; }
      }
      if (!found) p.QuestsDone.Add(id);
      save.Save(p);
      return true;
    } catch (System.Exception) { return false; }
  }
}
