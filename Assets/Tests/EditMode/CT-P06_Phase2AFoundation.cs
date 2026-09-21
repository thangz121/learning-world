// CT-P06: Phase 2A content-foundation contracts (QUEST DATA, not quest code).
// Proves the reusable pipeline on real Content/*.json through the production
// readers (ContentDatabase + QuestContentCatalog), with ZERO changes to the
// frozen systems (QuestManager / QuestRewardService / presenters / camera):
//   A. the existing w1_mia_apple quest is representable as catalog data
//      (pattern B, chapter w1, apple audio, next = w1_mia_ball).
//   B. the NEW w1_mia_ball quest + ball vocab validate clean.
//   C. the pattern classifier covers every shipped quest JSON.
//   D. CatalogQuestProvider runs BOTH quests through the real QuestManager +
//      QuestRewardService (find -> bring -> completed, friendship + flower).
//   E. audio/pregen mapping covers the new word + lines (BuildPreGenList).
//   F. pack invariants for the grown bundle (51 vocab / 16 active / 38 lines).
// Pure EditMode. C# 9.0 only.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class CT_P06_Phase2AFoundation {
  static readonly QuestId AppleQuest = new QuestId("w1_mia_apple");
  static readonly QuestId BallQuest = new QuestId("w1_mia_ball");
  static readonly WordId Apple = new WordId("apple");
  static readonly WordId Ball = new WordId("ball");

  static string ContentRoot() {
    string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Content");
    Assert.IsTrue(Directory.Exists(root), "Content/ bundle must exist next to Assets");
    return root;
  }

  static QuestEntry LoadQuest(string root, string id) {
    return ContentDatabase.ParseQuest(File.ReadAllText(Path.Combine(root, "quests", id + ".json")));
  }

  static VocabEntry LoadVocab(string root, string id) {
    return ContentDatabase.ParseVocab(File.ReadAllText(Path.Combine(root, "vocab", id + ".json")));
  }

  static DialoguePack LoadPack(string root) {
    return ContentDatabase.ParseDialoguePack(
      File.ReadAllText(Path.Combine(root, "dialogues", "manifest.json")));
  }

  static List<VocabEntry> LoadAllVocabs(string root) {
    var list = new List<VocabEntry>();
    foreach (string path in Directory.GetFiles(Path.Combine(root, "vocab"), "*.json"))
      list.Add(ContentDatabase.ParseVocab(File.ReadAllText(path)));
    return list;
  }

  static QuestContentEntry AppleEntry(string root, DialoguePack pack, List<VocabEntry> vocabs) {
    return QuestContentCatalog.BuildEntry(
      LoadQuest(root, "w1_mia_apple"), vocabs, pack,
      new List<string> { "inst_02", "inst_04", "ok_01", "retry_03" },
      "retry_03", "ok_01",
      new List<string> { "ball" }, "w1_mia_ball");
  }

  static QuestContentEntry BallEntry(string root, DialoguePack pack, List<VocabEntry> vocabs) {
    return QuestContentCatalog.BuildEntry(
      LoadQuest(root, "w1_mia_ball"), vocabs, pack,
      new List<string> { "inst_07", "ok_05", "retry_03" },
      "retry_03", "ok_05",
      new List<string> { "apple" }, "");
  }

  // A. Existing quest, represented as data (no gameplay duplication).
  [Test] public void CT_P06A_AppleQuestAsData() {
    string root = ContentRoot();
    DialoguePack pack = LoadPack(root);
    List<VocabEntry> vocabs = LoadAllVocabs(root);
    QuestContentEntry e = AppleEntry(root, pack, vocabs);
    Assert.AreEqual("w1_mia_apple", e.questId);
    Assert.AreEqual("w1", e.chapterId, "w1_ prefix stages chapter w1");
    Assert.AreEqual("shopkeeper_mia", e.npc);
    Assert.AreEqual(QuestPattern.BringToNpc, e.pattern, "find+bring is PATTERN B");
    Assert.AreEqual(2, e.objectives.Count);
    Assert.AreEqual("apple", e.targetObject);
    Assert.AreEqual(1, e.vocabulary.Count, "distinct targets only");
    Assert.AreEqual("apple", e.vocabulary[0]);
    Assert.AreEqual(1, e.audio.Count);
    Assert.AreEqual("audio/apple_normal.mp3", e.audio[0].normal);
    Assert.AreEqual("audio/apple_slow.mp3", e.audio[0].slow);
    Assert.AreEqual("learning_v1", e.audio[0].voice);
    Assert.AreEqual("en-US", e.audio[0].lang);
    Assert.AreEqual("w1_mia_ball", e.nextQuest, "apple chains into the ball quest");
    Assert.AreEqual("flower_pot", e.completionEffect);
    List<string> errors = QuestContentCatalog.Validate(e, pack);
    Assert.AreEqual(0, errors.Count, "apple entry must validate: " + string.Join("; ", errors));
  }

  // B. New quest + new vocab validate clean.
  [Test] public void CT_P06B_BallQuestAsData() {
    string root = ContentRoot();
    DialoguePack pack = LoadPack(root);
    List<VocabEntry> vocabs = LoadAllVocabs(root);
    VocabEntry ball = LoadVocab(root, "ball");
    Assert.IsTrue(ball.active, "ball is an ACTIVE quest target");
    Assert.AreEqual("object", ball.category);
    Assert.AreEqual("learning_v1", ball.audioVoice);
    QuestContentEntry e = BallEntry(root, pack, vocabs);
    Assert.AreEqual(QuestPattern.BringToNpc, e.pattern, "ball reuses PATTERN B, no new logic");
    Assert.AreEqual("w1", e.chapterId);
    Assert.AreEqual("ball", e.targetObject);
    Assert.AreEqual("", e.nextQuest, "ball is terminal until 2F grows the chain");
    List<string> errors = QuestContentCatalog.Validate(e, pack);
    Assert.AreEqual(0, errors.Count, "ball entry must validate: " + string.Join("; ", errors));
  }

  // C. Classifier covers every shipped quest JSON.
  [Test] public void CT_P06C_PatternsCoverShippedQuests() {
    string root = ContentRoot();
    var expected = new Dictionary<string, QuestPattern> {
      { "w1_mia_apple", QuestPattern.BringToNpc },
      { "w1_mia_ball", QuestPattern.BringToNpc },
      { "market_help_mia", QuestPattern.MultiStep },
      { "lost_teddy", QuestPattern.MultiStep },
      { "colors_counting", QuestPattern.MultiStep },
      { "please_thank_you", QuestPattern.MultiStep },
      { "big_or_small", QuestPattern.ChooseCorrect },
    };
    foreach (KeyValuePair<string, QuestPattern> kv in expected) {
      QuestEntry q = LoadQuest(root, kv.Key);
      var actions = new List<PlayerAction>();
      var targets = new List<string>();
      foreach (QuestObjective o in q.objectives) { actions.Add(o.action); targets.Add(o.target); }
      Assert.AreEqual(kv.Value, QuestPatternDef.Classify(actions, targets),
        "quest " + kv.Key + " must classify as " + kv.Value);
    }
    Assert.AreEqual(QuestPattern.FindIdentify,
      QuestPatternDef.Classify(new List<PlayerAction> { PlayerAction.Find },
        new List<string> { "apple" }), "lone Find is PATTERN A");
    Assert.AreEqual(QuestPattern.MatchWord,
      QuestPatternDef.Classify(new List<PlayerAction> { PlayerAction.Speak },
        new List<string> { "apple" }), "lone Speak is PATTERN D");
    Assert.AreEqual(QuestPattern.Unknown,
      QuestPatternDef.Classify(new List<PlayerAction>(), new List<string>()), "empty is Unknown");
    Assert.AreEqual(QuestPattern.Unknown,
      QuestPatternDef.Classify(null), "null is Unknown, never throws");
  }

  // D. Pipeline proof: catalog data runs BOTH quests through the REAL
  // QuestManager + QuestRewardService (frozen logic, new provider).
  [Test] public void CT_P06D_CatalogRunsBothQuestsLive() {
    string root = ContentRoot();
    QuestEntry appleQ = LoadQuest(root, "w1_mia_apple");
    QuestEntry ballQ = LoadQuest(root, "w1_mia_ball");
    var provider = new CatalogQuestProvider(new List<QuestEntry> { appleQ, ballQ });
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints, provider);
    var rewards = new QuestRewardService(bus, provider);

    quests.StartQuest(AppleQuest);
    quests.AdvanceOnSeen(Apple);
    Assert.AreEqual(1, quests.GetState(AppleQuest).ObjectiveIndex, "apple find must advance");
    quests.ReportAction(PlayerAction.Bring, Apple);
    Assert.IsTrue(quests.GetState(AppleQuest).Completed, "apple bring must complete");

    quests.StartQuest(BallQuest);
    quests.AdvanceOnSeen(Ball);
    Assert.AreEqual(1, quests.GetState(BallQuest).ObjectiveIndex, "ball find must advance");
    quests.ReportAction(PlayerAction.Bring, Ball);
    Assert.IsTrue(quests.GetState(BallQuest).Completed, "ball bring must complete");

    Assert.AreEqual(20, rewards.GetFriendship(new NpcId("mia")), "10 + 10 friendship across both quests");
    Assert.IsTrue(rewards.HasWorldChange("flower_pot"), "completion effect recorded");
  }

  // E. Audio/content pipeline: new word + lines get deterministic mappings.
  [Test] public void CT_P06E_PregenCoversNewContent() {
    string root = ContentRoot();
    VocabEntry ball = LoadVocab(root, "ball");
    DialoguePack pack = LoadPack(root);
    var lines = new List<DialogueLine>();
    foreach (DialogueLine l in pack.lines) {
      if (l.id == "inst_07" || l.id == "ok_05") lines.Add(l);
    }
    Assert.AreEqual(2, lines.Count, "ball dialogue lines must exist in the pack");
    List<PreGenItem> pregen = ContentDatabase.BuildPreGenList(
      new List<VocabEntry> { ball }, lines);
    PreGenItem normal = pregen.Find(p => p.id == "ball_normal");
    PreGenItem slow = pregen.Find(p => p.id == "ball_slow");
    Assert.IsNotNull(normal, "ball_normal must be mapped");
    Assert.IsNotNull(slow, "ball_slow must be mapped");
    Assert.AreEqual(0.85f, normal.rate, 0.001f);
    Assert.AreEqual(0.70f, slow.rate, 0.001f);
    Assert.AreEqual("learning_v1", normal.voiceProfile);
    Assert.AreEqual("en-US", normal.lang);
    foreach (DialogueLine l in lines) {
      PreGenItem item = pregen.Find(p => p.id == l.id);
      Assert.IsNotNull(item, l.id + " must be mapped");
      Assert.AreEqual(0.85f, item.rate, 0.001f);
      Assert.AreEqual("en-US", item.lang);
    }
  }

  // F. Grown-bundle invariants (extends the CT-008 pins for Phase 2A).
  [Test] public void CT_P06F_GrownPackInvariants() {
    string root = ContentRoot();
    string[] vocabFiles = Directory.GetFiles(Path.Combine(root, "vocab"), "*.json");
    Assert.AreEqual(51, vocabFiles.Length, "vocab pack is 51 after +ball");
    int active = 0;
    var vocabIds = new HashSet<string>();
    foreach (string path in vocabFiles) {
      VocabEntry v = ContentDatabase.ParseVocab(File.ReadAllText(path));
      Assert.AreEqual(Path.GetFileNameWithoutExtension(path), v.id, "vocab id must match filename");
      vocabIds.Add(v.id);
      if (v.active) active++;
    }
    Assert.AreEqual(16, active, "active vocab is 16 after +ball");
    DialoguePack pack = LoadPack(root);
    Assert.AreEqual(pack.count, pack.lines.Count, "manifest.count must match real lines");
    // Phase 3.0.x S3: pack grows 38 -> 40 (+2 Tess lines math_01/math_02, cap frozen).
    Assert.AreEqual(40, pack.lines.Count, "dialogue pack is 40 after +2 math lines");
    foreach (string qid in new string[] { "w1_mia_apple", "w1_mia_ball", "math_counting" }) {
      QuestEntry q = LoadQuest(root, qid);
      Assert.AreEqual(qid, q.id, "quest id must match filename");
      foreach (QuestObjective o in q.objectives)
        Assert.IsTrue(vocabIds.Contains(o.target), qid + ": target '" + o.target + "' needs a vocab file");
    }
  }
}
