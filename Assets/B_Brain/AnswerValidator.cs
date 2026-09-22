// B_Brain/AnswerValidator.cs — Agent B. P1-7 (P3.0.1 foundation).
// Validation half of the answer contract: pure, deterministic eligibility
// checks over live IQuestService state. Quest RULES (advance/complete/events)
// stay in QuestManager; this answers the presenter's pre-question — "is this
// action meaningful RIGHT NOW?" — so Correct vs GentleRetry vs Ignored is
// decided by contract, not by each presenter re-deriving index logic.
// Pattern/question separation (§4) holds: the validator is keyed by
// PlayerAction (learning interaction), never by pattern.
// Pure static over IQuestService (frozen interface, no downcast): usable from
// any presenter. Real consumers: MathHostPresenter (bring) + MiaPresenter
// (proximity/click pre-check). C# 9.0 only.
public enum AnswerVerdict {
  Ignored,     // no quest context (pre-talk, post-completion): stay silent
  GentleRetry, // quest open but this action is not the current step: gentle re-prompt, never punishment
  Correct,     // this action advances the quest: ReportAction path
}

public static class AnswerValidator {
  // Quest open = started (index > 0 or started flag implied) and not done.
  // Fresh/boot state is index 0 + not completed (GetState default).
  public static bool IsQuestOpen(IQuestService quests, QuestId questId) {
    if (quests == null) return false;
    try {
      QuestState s = quests.GetState(questId);
      return s != null && !s.Completed;
    } catch (System.Exception) { return false; }
  }

  // Bring eligibility for Find->Bring quests (math_counting, w1_mia_*):
  // the find step (index 0) is done, quest not completed. Presenters keep
  // their own carry/object state; this gates the QUEST side only.
  public static bool CanBring(IQuestService quests, QuestId questId) {
    if (quests == null) return false;
    try {
      QuestState s = quests.GetState(questId);
      return s != null && !s.Completed && s.ObjectiveIndex >= 1;
    } catch (System.Exception) { return false; }
  }

  // Full verdict for a bring attempt: pre/post quest -> Ignored, find not
  // done -> GentleRetry, bring expected -> Correct.
  public static AnswerVerdict ValidateBring(IQuestService quests, QuestId questId, bool questStarted) {
    if (quests == null || !questStarted) return AnswerVerdict.Ignored;
    try {
      QuestState s = quests.GetState(questId);
      if (s == null || s.Completed) return AnswerVerdict.Ignored;
      return s.ObjectiveIndex >= 1 ? AnswerVerdict.Correct : AnswerVerdict.GentleRetry;
    } catch (System.Exception) { return AnswerVerdict.Ignored; }
  }
}
