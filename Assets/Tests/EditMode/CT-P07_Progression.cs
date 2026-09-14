// CT-P07: Phase 2C vocabulary-progression contracts (learning foundation).
// Proves, over real Content JSON + the real LearningService (session-scoped,
// in-memory — persistence is intentionally NOT wired in 2C):
//   A. new words start Unknown with default progression metadata.
//   B. Seen x3 -> Exposed; recommendation reflects the stage.
//   C. correct selects promote; WRONG-ONLY selects never promote (totals up,
//      hits flat) — wrong answers delay promotion by construction.
//   D. full chain to Produced/UsedInContext/Retained via service events.
//   E. Spoken hit/miss accounting (SERVICE contract only — 2B audit: zero
//      live WordSpokenEvent publishers; no real-player claim here).
//   F. review-due outranks; Retained-without-due leaves the rotation.
//   G. passive words are never recommended (promotion is authoring).
//   H. prerequisites gate (ball blocked until apple leaves Unknown).
//   I. recommendation deterministic (same input -> same order, twice).
//   J. quest -> vocab identity is stable raw ids (no display matching).
//   K. simplify directive survives parse -> catalog entry intact.
//   L. (suite-level: full 75+ run stays green — verified in the 2C run.)
// Pure EditMode. C# 9.0 only.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class CT_P07_Progression {
  static Func<string, WordMastery> MasteryOf(LearningService learning) {
    return delegate (string s) {
      return learning.GetMastery(new WordId(s));
    };
  }

  static string ContentRoot() {
    string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Content");
    Assert.IsTrue(Directory.Exists(root), "Content/ bundle must exist next to Assets");
    return root;
  }

  static List<VocabEntry> LoadAllVocabs(string root) {
    var list = new List<VocabEntry>();
    foreach (string path in Directory.GetFiles(Path.Combine(root, "vocab"), "*.json"))
      list.Add(ContentDatabase.ParseVocab(File.ReadAllText(path)));
    return list;
  }

  static List<QuestEntry> LoadAllQuests(string root) {
    var list = new List<QuestEntry>();
    foreach (string path in Directory.GetFiles(Path.Combine(root, "quests"), "*.json"))
      list.Add(ContentDatabase.ParseQuest(File.ReadAllText(path)));
    return list;
  }

  static VocabEntry ById(List<VocabEntry> vocabs, string id) {
    return vocabs.Find(v => v.id == id);
  }

  static void See(LearningService learning, string word, int n) {
    for (int i = 0; i < n; i++) learning.ReportSeen(new WordId(word), LearnSource.Quest);
  }

  static void DriveToProduced(LearningService learning, string word) {
    See(learning, word, 3);
    for (int i = 0; i < 4; i++) learning.ReportSelected(new WordId(word), true, LearnSource.Quest);
    learning.ReportSelected(new WordId(word), false, LearnSource.Quest);
    for (int i = 0; i < 4; i++) learning.ReportSpoken(new WordId(word), SpeechLevel.Great, LearnSource.Quest);
    Assert.AreEqual(MasteryStage.Produced, learning.GetStage(new WordId(word)), "setup: must reach Produced");
  }

  // A. New word: Unknown stage, default metadata when block absent.
  [Test] public void CT_P07A_NewWordInitial() {
    string root = ContentRoot();
    List<VocabEntry> vocabs = LoadAllVocabs(root);
    var learning = new LearningService(new GameEventBus());
    VocabEntry milk = ById(vocabs, "milk"); // active, no progression block
    Assert.IsNotNull(milk);
    Assert.AreEqual(999, milk.introOrder, "absent block defaults to unordered");
    Assert.AreEqual(0, milk.prerequisites.Count, "absent block defaults to no prerequisites");
    Assert.AreEqual(MasteryStage.Unknown, learning.GetStage(new WordId("milk")), "fresh session reads Unknown");
    ProgressionView view = VocabularyProgression.BuildView(
      milk, MasteryOf(learning), VocabularyProgression.QuestTargetUnion(LoadAllQuests(root)));
    Assert.AreEqual(MasteryStage.Unknown, view.stage);
    Assert.AreEqual(RecommendationReason.ActivePractice, view.reason, "unblocked active non-target practices");
  }

  // B. Seen x3 -> Exposed; view + recommendation follow.
  [Test] public void CT_P07B_SeenPromotesToExposed() {
    string root = ContentRoot();
    List<VocabEntry> vocabs = LoadAllVocabs(root);
    var learning = new LearningService(new GameEventBus());
    See(learning, "apple", 2);
    Assert.AreEqual(MasteryStage.Unknown, learning.GetStage(new WordId("apple")), "2 exposures stay Unknown");
    See(learning, "apple", 1);
    Assert.AreEqual(MasteryStage.Exposed, learning.GetStage(new WordId("apple")), "3rd Seen promotes");
    ProgressionView view = VocabularyProgression.BuildView(
      ById(vocabs, "apple"), MasteryOf(learning), VocabularyProgression.QuestTargetUnion(LoadAllQuests(root)));
    Assert.AreEqual(3, view.exposure);
    Assert.AreEqual(1, view.introOrder, "apple stamped order 1");
    Assert.AreEqual(RecommendationReason.ActiveTarget, view.reason);
  }

  // C. Wrong-only selects NEVER promote (the §14 attempt-vs-success split).
  [Test] public void CT_P07C_WrongSelectsDoNotPromote() {
    var learning = new LearningService(new GameEventBus());
    See(learning, "red", 3);
    for (int i = 0; i < 5; i++) learning.ReportSelected(new WordId("red"), false, LearnSource.Quest);
    WordMastery m = learning.GetMastery(new WordId("red"));
    Assert.AreEqual(5, m.RecognitionTotal, "attempts recorded");
    Assert.AreEqual(0, m.RecognitionHit, "no hits recorded");
    Assert.AreEqual(MasteryStage.Exposed, learning.GetStage(new WordId("red")),
      "5 wrong selects must NOT reach Recognized");
    for (int i = 0; i < 4; i++) learning.ReportSelected(new WordId("red"), true, LearnSource.Quest);
    Assert.AreEqual(MasteryStage.Recognized, learning.GetStage(new WordId("red")),
      "4 hits in 9 totals still promote once earned");
  }

  // D. Full chain to UsedInContext + Retained (service level, like CT-010).
  [Test] public void CT_P07D_ChainToRetained() {
    var learning = new LearningService(new GameEventBus());
    DriveToProduced(learning, "one");
    learning.ReportContextUse(new WordId("one"));
    Assert.AreEqual(MasteryStage.UsedInContext, learning.GetStage(new WordId("one")));
    Assert.AreEqual(MasteryStage.UsedInContext, learning.GetStage(new WordId("one")),
      "review not due yet: stays UsedInContext");
    learning.ForceReviewDue(new WordId("one"));
    Assert.AreEqual(MasteryStage.Retained, learning.GetStage(new WordId("one")), "due review retains");
  }

  // E. Spoken accounting (SERVICE contract — no live publisher per 2B audit).
  [Test] public void CT_P07E_SpokenServiceContract() {
    var learning = new LearningService(new GameEventBus());
    See(learning, "please", 3);
    learning.ReportSpoken(new WordId("please"), SpeechLevel.Almost, LearnSource.Quest);
    learning.ReportSpoken(new WordId("please"), SpeechLevel.TryTogether, LearnSource.Quest);
    WordMastery m = learning.GetMastery(new WordId("please"));
    Assert.AreEqual(2, m.SpeakingTotal, "attempts recorded");
    Assert.AreEqual(0, m.SpeakingHit, "Almost/TryTogether are not hits");
    learning.ReportSpoken(new WordId("please"), SpeechLevel.Great, LearnSource.Quest);
    // Not Produced yet (needs totals>=4 with 2 hits); this pins accounting, not promotion.
    Assert.AreEqual(1, learning.GetMastery(new WordId("please")).SpeakingHit, "Great counts a hit");
  }

  // F. Review-due outranks; Retained-without-due rests.
  [Test] public void CT_P07F_ReviewDueFirstRetainedRests() {
    string root = ContentRoot();
    List<VocabEntry> vocabs = LoadAllVocabs(root);
    HashSet<string> targets = VocabularyProgression.QuestTargetUnion(LoadAllQuests(root));
    var learning = new LearningService(new GameEventBus());
    DriveToProduced(learning, "apple");
    learning.ReportContextUse(new WordId("apple"));
    List<string> before = VocabularyProgression.RecommendNext(vocabs, MasteryOf(learning), targets);
    Assert.AreEqual(RecommendationReason.ActiveTarget,
      VocabularyProgression.ReasonFor(ById(vocabs, "apple"), MasteryOf(learning), targets),
      "UsedInContext with future review stays a normal target, not due");
    Assert.AreEqual("ball", before[0], "unblocked Unknown ball outranks practiced apple");
    learning.ForceReviewDue(new WordId("apple"));
    Assert.AreEqual(MasteryStage.Retained, learning.GetStage(new WordId("apple")));
    List<string> due = VocabularyProgression.RecommendNext(vocabs, MasteryOf(learning), targets);
    Assert.IsTrue(due.Count > 0 && due[0] == "apple", "due review outranks everything");
    Assert.AreEqual(RecommendationReason.ReviewDue,
      VocabularyProgression.ReasonFor(ById(vocabs, "apple"), MasteryOf(learning), targets));
  }

  // G. Passive words never recommended (promotion is authoring, D4).
  [Test] public void CT_P07G_PassiveNeverRecommended() {
    string root = ContentRoot();
    List<VocabEntry> vocabs = LoadAllVocabs(root);
    HashSet<string> targets = VocabularyProgression.QuestTargetUnion(LoadAllQuests(root));
    var learning = new LearningService(new GameEventBus());
    // Heavy exposure on every word incl. passives: policy must still exclude them.
    foreach (VocabEntry v in vocabs) See(learning, v.id, 5);
    List<string> recs = VocabularyProgression.RecommendNext(vocabs, MasteryOf(learning), targets);
    foreach (string id in recs) {
      Assert.IsTrue(ById(vocabs, id).active, id + " recommended but passive");
      Assert.IsTrue(ById(vocabs, id).prerequisites.Count == 0
        || learning.GetStage(new WordId(ById(vocabs, id).prerequisites[0])) != MasteryStage.Unknown,
        id + " recommended while blocked");
    }
    Assert.AreEqual(RecommendationReason.PassiveAuthoring,
      VocabularyProgression.ReasonFor(ById(vocabs, "teddy"), MasteryOf(learning), targets),
      "teddy (passive quest target!) still reasons PassiveAuthoring");
  }

  // H. Prerequisite gating: ball waits for apple (D1).
  [Test] public void CT_P07H_PrereqGatesBall() {
    string root = ContentRoot();
    List<VocabEntry> vocabs = LoadAllVocabs(root);
    HashSet<string> targets = VocabularyProgression.QuestTargetUnion(LoadAllQuests(root));
    var learning = new LearningService(new GameEventBus());
    Assert.AreEqual(RecommendationReason.BlockedPrereq,
      VocabularyProgression.ReasonFor(ById(vocabs, "ball"), MasteryOf(learning), targets),
      "ball blocked while apple Unknown");
    Assert.IsFalse(VocabularyProgression.RecommendNext(vocabs, MasteryOf(learning), targets).Contains("ball"));
    See(learning, "apple", 3);
    Assert.AreEqual(RecommendationReason.ActiveTarget,
      VocabularyProgression.ReasonFor(ById(vocabs, "ball"), MasteryOf(learning), targets),
      "apple Exposed unblocks ball");
    List<string> recs = VocabularyProgression.RecommendNext(vocabs, MasteryOf(learning), targets);
    Assert.AreEqual("ball", recs[0], "unblocked Unknown target outranks Exposed apple (neediest first)");
  }

  // I. Deterministic (D5): same input twice, same order; ties by id.
  [Test] public void CT_P07I_DeterministicOrder() {
    string root = ContentRoot();
    List<VocabEntry> vocabs = LoadAllVocabs(root);
    HashSet<string> targets = VocabularyProgression.QuestTargetUnion(LoadAllQuests(root));
    var learning = new LearningService(new GameEventBus());
    See(learning, "apple", 3);
    See(learning, "red", 1);
    List<string> first = VocabularyProgression.RecommendNext(vocabs, MasteryOf(learning), targets);
    List<string> second = VocabularyProgression.RecommendNext(vocabs, MasteryOf(learning), targets);
    Assert.AreEqual(first, second, "recommendation must be deterministic");
    Assert.Greater(first.Count, 0, "fresh session still recommends");
  }

  // J. Quest -> vocab identity is stable raw ids (no display matching).
  [Test] public void CT_P07J_StableIdentity() {
    string root = ContentRoot();
    List<VocabEntry> vocabs = LoadAllVocabs(root);
    List<QuestEntry> quests = LoadAllQuests(root);
    HashSet<string> targets = VocabularyProgression.QuestTargetUnion(quests);
    var vocabIds = new HashSet<string>();
    foreach (VocabEntry v in vocabs) vocabIds.Add(v.id);
    foreach (string t in targets) {
      Assert.IsTrue(vocabIds.Contains(t), "quest target '" + t + "' must be a vocab id (not display text)");
      Assert.AreEqual(t, new WordId(t).Value, "target must already be normalized id form");
    }
    List<string> recs = VocabularyProgression.RecommendNext(vocabs, MasteryOf(new LearningService(new GameEventBus())), targets);
    foreach (string id in recs)
      Assert.IsTrue(vocabIds.Contains(id), "recommended '" + id + "' must resolve to a vocab file");
  }

  // K. Simplify directive survives parse -> catalog entry (2C decision wiring).
  [Test] public void CT_P07K_SimplifyToBoundary() {
    string root = ContentRoot();
    string appleJson = File.ReadAllText(Path.Combine(root, "quests", "w1_mia_apple.json"));
    QuestEntry q = ContentDatabase.ParseQuest(appleJson);
    Assert.AreEqual(2, q.simplifyReduceChoices, "parsed reduce_choices_to");
    Assert.IsTrue(q.simplifyDemo, "parsed demo_one_step");
    DialoguePack pack = ContentDatabase.ParseDialoguePack(
      File.ReadAllText(Path.Combine(root, "dialogues", "manifest.json")));
    var vocabs = new List<VocabEntry>();
    QuestContentEntry e = QuestContentCatalog.BuildEntry(
      q, vocabs, pack, new List<string>(), "", "", null, "");
    Assert.AreEqual(2, e.simplifyReduceChoices, "entry carries the directive");
    Assert.IsTrue(e.simplifyDemo, "entry carries the demo flag");
    List<string> errors = QuestContentCatalog.Validate(e, pack);
    foreach (string err in errors)
      Assert.IsFalse(err.Contains("simplify"), "simplify must validate: " + err);
  }
}
