// C_Content/VocabularyProgression.cs — Phase 2C (learning foundation, ADDITIVE).
// Answers "which vocabulary next?" deterministically from CONTENT metadata +
// PLAYER learning state, without touching QuestManager, LearningService, or
// any frozen system (all inputs are read-only: DTOs in, ranked ids out).
//
// Model (no new state machine — the repo already owns MasteryFSM):
//   EVENTS (LearningService, write path): Seen / Selected(hit|miss) /
//     Spoken(level) / ContextUse. Counters accumulate; misses raise totals
//     without hits, so wrong answers DELAY promotion by construction.
//   PROGRESSION STATE (derived, read path): MasteryStage per word +
//     this policy's ranking. Stages come from ARCHITECTURE.md §5 thresholds
//     (3 seen / 4-of-5 selects / Great+ 2-of-4 speaks / 1 context / review).
//   CONTENT METADATA (new in 2C): introOrder + prerequisites per vocab.
//   PLAYER STATE (session memory in LearningService; persistence format
//     exists in LocalSave but NOTHING wires it yet — Phase 4 scope).
//
// DESIGN DECISIONS (marked per mission §7 — no live evidence exists for a
// fuller model; every rule below is deterministic and unit-pinned):
//   D1: a prerequisite is satisfied iff its stage != Unknown (introduced).
//   D2: Retained words with no due review leave the active rotation
//       (MasteredResting) — this is the "familiar enough" exit.
//   D3: review-due (UsedInContext + NextReview passed) outranks everything:
//       recall is the PRODUCT.md KPI (Learning Transfer).
//   D4: passive words are NEVER recommended (promotion is an authoring act
//       in Content JSON, never a runtime decision).
//   D5: ties break by (stage rank, introOrder, word id) — no randomness,
//       so output is fully deterministic and testable.
// C# 9.0 only. No UnityEngine, no IO, no service references.
using System;
using System.Collections.Generic;

public enum RecommendationReason {
  ReviewDue = 0,      // recall check outstanding (PRODUCT.md transfer KPI)
  ActiveTarget = 1,   // active word targeted by a quest, unblocked
  ActivePractice = 2, // active word worth practicing, unblocked
  BlockedPrereq = 3,  // a prerequisite is still Unknown (not recommended)
  MasteredResting = 4,// Retained, review not due (out of rotation)
  PassiveAuthoring = 5,// passive: promotion is authoring, never runtime
}

public sealed class ProgressionView {
  public string wordId;
  public bool active;
  public MasteryStage stage;
  public int exposure;
  public int recognitionHit;
  public int recognitionTotal;
  public int speakingHit;
  public int speakingTotal;
  public int contextUse;
  public int introOrder;
  public List<string> prerequisites = new List<string>();
  public List<string> blockedBy = new List<string>();
  public bool isQuestTarget;
  public RecommendationReason reason;
}

public static class VocabularyProgression {
  public const int DefaultIntroOrder = 999;

  static MasteryStage StageOf(Func<string, WordMastery> masteryOf, string word) {
    if (masteryOf == null) return MasteryStage.Unknown;
    try {
      WordMastery m = masteryOf(word);
      if (m == null) return MasteryStage.Unknown;
      return m.Stage;
    } catch (Exception) {
      return MasteryStage.Unknown;
    }
  }

  static void FillCounters(ProgressionView v, Func<string, WordMastery> masteryOf) {
    if (masteryOf == null) return;
    try {
      WordMastery m = masteryOf(v.wordId);
      if (m == null) return;
      v.exposure = m.Exposure;
      v.recognitionHit = m.RecognitionHit;
      v.recognitionTotal = m.RecognitionTotal;
      v.speakingHit = m.SpeakingHit;
      v.speakingTotal = m.SpeakingTotal;
      v.contextUse = m.ContextUse;
    } catch (Exception) { /* unreadable state reads as zero */ }
  }

  static bool ReviewDueNow(Func<string, WordMastery> masteryOf, string word) {
    if (masteryOf == null) return false;
    try {
      WordMastery m = masteryOf(word);
      if (m == null) return false;
      if (m.Stage != MasteryStage.UsedInContext && m.Stage != MasteryStage.Retained) return false;
      if (m.NextReview == default(DateTime)) return false;
      return DateTime.UtcNow >= m.NextReview;
    } catch (Exception) {
      return false;
    }
  }

  static int StageRank(MasteryStage s) {
    switch (s) {
      case MasteryStage.Unknown: return 0;
      case MasteryStage.Exposed: return 1;
      case MasteryStage.Recognized: return 2;
      case MasteryStage.Produced: return 3;
      case MasteryStage.UsedInContext: return 4;
      default: return 5; // Retained
    }
  }

  // Full per-word view (content + player state + reason). Pure.
  public static ProgressionView BuildView(
      VocabEntry vocab,
      Func<string, WordMastery> masteryOf,
      HashSet<string> questTargetWords) {
    if (vocab == null) throw new ArgumentNullException("vocab");
    var v = new ProgressionView();
    v.wordId = vocab.id;
    v.active = vocab.active;
    v.stage = StageOf(masteryOf, vocab.id);
    FillCounters(v, masteryOf);
    v.introOrder = vocab.introOrder;
    if (vocab.prerequisites != null) v.prerequisites.AddRange(vocab.prerequisites);
    v.isQuestTarget = questTargetWords != null && questTargetWords.Contains(vocab.id);
    foreach (string p in v.prerequisites) {
      if (StageOf(masteryOf, p) == MasteryStage.Unknown) v.blockedBy.Add(p);
    }
    v.reason = ReasonFor(v, masteryOf);
    return v;
  }

  public static RecommendationReason ReasonFor(
      ProgressionView v, Func<string, WordMastery> masteryOf) {
    if (v == null) throw new ArgumentNullException("v");
    if (!v.active) return RecommendationReason.PassiveAuthoring;
    if (v.blockedBy.Count > 0) return RecommendationReason.BlockedPrereq;
    if (masteryOf != null && ReviewDueNow(masteryOf, v.wordId))
      return RecommendationReason.ReviewDue; // D3: recall outranks everything
    if (v.stage == MasteryStage.Retained) return RecommendationReason.MasteredResting;
    return v.isQuestTarget ? RecommendationReason.ActiveTarget : RecommendationReason.ActivePractice;
  }

  public static RecommendationReason ReasonFor(
      VocabEntry vocab,
      Func<string, WordMastery> masteryOf,
      HashSet<string> questTargetWords) {
    return ReasonFor(BuildViewShallow(vocab, masteryOf, questTargetWords), masteryOf);
  }

  static ProgressionView BuildViewShallow(
      VocabEntry vocab,
      Func<string, WordMastery> masteryOf,
      HashSet<string> questTargetWords) {
    var v = new ProgressionView();
    v.wordId = vocab.id;
    v.active = vocab.active;
    v.stage = StageOf(masteryOf, vocab.id);
    v.introOrder = vocab.introOrder;
    if (vocab.prerequisites != null) v.prerequisites.AddRange(vocab.prerequisites);
    v.isQuestTarget = questTargetWords != null && questTargetWords.Contains(vocab.id);
    foreach (string p in v.prerequisites) {
      if (StageOf(masteryOf, p) == MasteryStage.Unknown) v.blockedBy.Add(p);
    }
    return v;
  }

  // Deterministic recommendation: eligible words ordered
  // ReviewDue → ActiveTarget → ActivePractice, ties by (stage, order, id).
  // Blocked / MasteredResting / Passive words are omitted (reasons stay
  // available via ReasonFor for transparency/debugging).
  public static List<string> RecommendNext(
      List<VocabEntry> vocabs,
      Func<string, WordMastery> masteryOf,
      HashSet<string> questTargetWords) {
    var ranked = new List<KeyValuePair<ProgressionView, int>>();
    if (vocabs != null) {
      foreach (VocabEntry vocab in vocabs) {
        if (vocab == null || string.IsNullOrEmpty(vocab.id)) continue;
        ProgressionView v = BuildViewShallow(vocab, masteryOf, questTargetWords);
        RecommendationReason r = ReasonFor(v, masteryOf);
        if (r != RecommendationReason.ReviewDue
            && r != RecommendationReason.ActiveTarget
            && r != RecommendationReason.ActivePractice) continue;
        int bucket = r == RecommendationReason.ReviewDue ? 0
          : r == RecommendationReason.ActiveTarget ? 1 : 2;
        ranked.Add(new KeyValuePair<ProgressionView, int>(v, bucket));
      }
    }
    ranked.Sort(delegate (KeyValuePair<ProgressionView, int> a, KeyValuePair<ProgressionView, int> b) {
      if (a.Value != b.Value) return a.Value.CompareTo(b.Value);
      int sa = StageRank(a.Key.stage);
      int sb = StageRank(b.Key.stage);
      if (sa != sb) return sa.CompareTo(sb);
      if (a.Key.introOrder != b.Key.introOrder) return a.Key.introOrder.CompareTo(b.Key.introOrder);
      return string.Compare(a.Key.wordId, b.Key.wordId, StringComparison.Ordinal);
    });
    var out_ = new List<string>();
    foreach (KeyValuePair<ProgressionView, int> kv in ranked) out_.Add(kv.Key.wordId);
    return out_;
  }

  // Quest-side helper (data only): union of objective targets across quests.
  // Keeps the policy free of quest types; callers pass QuestEntry lists.
  public static HashSet<string> QuestTargetUnion(List<QuestEntry> quests) {
    var set = new HashSet<string>();
    if (quests == null) return set;
    foreach (QuestEntry q in quests) {
      if (q == null || q.objectives == null) continue;
      foreach (QuestObjective o in q.objectives) {
        if (o == null || string.IsNullOrEmpty(o.target)) continue;
        set.Add(o.target);
      }
    }
    return set;
  }
}
