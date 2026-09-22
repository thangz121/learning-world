// _SharedKernel/QuestAdoption.cs — Lead owns. P1-3 (P3.0.1 foundation).
// Generic re-entry adoption: replaces the per-object hand patches (J5/J6/J7,
// each pinned separately in P40E/F/G) with ONE contract every quest-bound
// visual implements. The state is PASSED IN (no service reference needed),
// so World-assembly visuals (FlowerPotPresenter) adopt without touching
// Brain types (frozen asmdef ownership holds).
// Wiring: whoever builds the scene calls QuestAdoption.AdoptAll with the live
// QuestState (GameInstaller for Math, MarketBuilder.WireQuestService for Main).
// Apply must be idempotent: AdoptAll can run after an event already applied.
// Real implementers: MathBloomDisplay, MathTokenCarry, MathHostPresenter,
// FlowerPotPresenter. C# 9.0 only.
using System.Collections.Generic;

public interface IQuestAdoptable {
  void AdoptQuestState(QuestState state);
}

public static class QuestAdoption {
  // Pushes the live state into every adoptable under a fresh scene/world.
  // Null-safe: missing service/state/entries degrade to no-ops, never NREs.
  public static void AdoptAll(IEnumerable<IQuestAdoptable> adoptables,
      IQuestService quests, QuestId questId) {
    if (adoptables == null || quests == null) return;
    QuestState state;
    try {
      state = quests.GetState(questId);
    } catch (System.Exception) { return; }
    if (state == null) return;
    foreach (IQuestAdoptable a in adoptables) {
      if (a == null) continue;
      try { a.AdoptQuestState(state); }
      catch (System.Exception) { }
    }
  }
}
