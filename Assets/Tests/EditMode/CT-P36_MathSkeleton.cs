// CT-P36: S3 Math playable-skeleton pins (Phase 3.0.x).
// Data/contract-level ONLY: quest JSON schema, BuiltIn mirrors, full quest
// loop through the real QuestManager + QuestRewardService, manifest math
// lines, Tess roster, no-Math-leak in shared contracts, catalog regression.
// NO player movement, NO teleport/warp, NO scene loading — experience proof
// belongs to the user-gated S3 journey, never to these tests. C# 9.0 only.
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CT_P36_MathSkeleton {
  static string ContentRoot() {
    string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Content");
    Assert.IsTrue(Directory.Exists(root), "Content/ bundle must exist next to Assets");
    return root;
  }

  static string ReadRepoFile(params string[] parts) {
    var all = new List<string>();
    all.Add(Application.dataPath);
    all.AddRange(parts);
    return File.ReadAllText(Path.Combine(all.ToArray()));
  }

  // A. Math quest JSON: schema freeze (same rules CT-008 enforces for old
  // quests) + Tess staging + find/bring "one".
  [Test] public void P36A_MathQuestJson() {
    string path = Path.Combine(ContentRoot(), "quests", "math_counting.json");
    Assert.IsTrue(File.Exists(path), "Content/quests/math_counting.json must ship");
    QuestEntry q = ContentDatabase.ParseQuest(File.ReadAllText(path));
    Assert.AreEqual("math_counting", q.id, "quest id must match filename");
    Assert.AreEqual("math_host", q.npc);
    Assert.IsNotNull(NpcRoster.ResolveQuestNpc(q.npc), "math npc must resolve in the roster");
    Assert.AreEqual("tess", NpcRoster.ResolveQuestNpc(q.npc).id, "math quest stages Tess");
    Assert.IsTrue(q.oneObjectiveAtATime, "one_objective_at_a_time must be true");
    Assert.AreEqual(2, q.objectives.Count);
    Assert.AreEqual(PlayerAction.Find, q.objectives[0].action);
    Assert.AreEqual("one", q.objectives[0].target);
    Assert.AreEqual(PlayerAction.Bring, q.objectives[1].action);
    Assert.AreEqual("one", q.objectives[1].target);
    string[] freeze = { "visual_glow", "milo_point", "milo_demo", "auto_simplify" };
    Assert.AreEqual(freeze.Length, q.hintLevels.Count, "hint_levels must be the 4-level freeze");
    for (int i = 0; i < freeze.Length; i++) Assert.AreEqual(freeze[i], q.hintLevels[i]);
    Assert.IsTrue(q.simplifyReduceChoices > 0 && q.simplifyDemo, "simplify_path required");
    Assert.AreEqual("math_bloom", q.rewardWorldChange);
  }

  // B. BuiltIn mirror serves the math quest (zero file IO at runtime).
  [Test] public void P36B_BuiltInServesMath() {
    var bus = new GameEventBus();
    var quests = new QuestManager(bus, new LearningService(bus), new HintService(bus));
    quests.StartQuest(new QuestId("math_counting"));
    QuestState s = quests.GetState(new QuestId("math_counting"));
    Assert.IsFalse(s.Completed, "fresh math quest is open");
    Assert.AreEqual(0, s.ObjectiveIndex);
    quests.AdvanceOnSeen(new WordId("one"));
    s = quests.GetState(new QuestId("math_counting"));
    Assert.AreEqual(1, s.ObjectiveIndex, "seen 'one' advances the find objective");
    Assert.IsFalse(s.Completed, "bring still pending");
  }

  // C. Full pilot loop through REAL services: find -> bring -> completed +
  // QuestCompletedEvent (the ONLY completion signal) fires exactly once.
  [Test] public void P36C_MathLoopCompletes() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints);
    var seen = new List<QuestCompletedEvent>();
    bus.Subscribe<QuestCompletedEvent>(e => seen.Add(e));
    quests.StartQuest(new QuestId("math_counting"));
    quests.AdvanceOnSeen(new WordId("one"));
    Assert.AreEqual(0, seen.Count, "find alone must not complete");
    quests.ReportAction(PlayerAction.Bring, new WordId("one"));
    Assert.IsTrue(quests.GetState(new QuestId("math_counting")).Completed, "bring completes the pilot");
    Assert.AreEqual(1, seen.Count, "exactly one completion event");
    Assert.AreEqual("math_counting", seen[0].QuestId.Value);
  }

  // D. Math reward is inert-by-contract: banks "math_bloom", credits NOBODY
  // (friendship stays Mia-ledger-only per W0-T1; multi-NPC is Phase 4).
  [Test] public void P36D_MathRewardInert() {
    var bus = new GameEventBus();
    var rewards = new QuestRewardService(bus);
    bus.Publish(new QuestCompletedEvent(new QuestId("math_counting"), DateTime.UtcNow));
    Assert.IsTrue(rewards.HasWorldChange("math_bloom"), "math change id banked");
    Assert.AreEqual(0, rewards.GetFriendship(new NpcId("mia")), "no Mia credit for Tess's quest");
    Assert.AreEqual(0, rewards.GetFriendship(new NpcId("tess")), "no ledger exists for Tess yet (Phase 4)");
  }

  // E. Manifest math lines: exact texts, Tess voice, pack stays 30-40.
  [Test] public void P36E_ManifestMathLines() {
    DialoguePack pack = ContentDatabase.ParseDialoguePack(
      File.ReadAllText(Path.Combine(ContentRoot(), "dialogues", "manifest.json")));
    Assert.AreEqual(pack.lines.Count, pack.count, "manifest.count must match real lines");
    Assert.AreEqual(40, pack.lines.Count, "pack grows 38 -> 40 (cap frozen)");
    DialogueLine ask = pack.lines.Find(l => l.id == "math_01");
    DialogueLine praise = pack.lines.Find(l => l.id == "math_02");
    Assert.IsNotNull(ask, "math_01 must ship");
    Assert.IsNotNull(praise, "math_02 must ship");
    Assert.AreEqual("tess", ask.npc);
    Assert.AreEqual("npc_female_01", ask.voice);
    Assert.AreEqual("Find the one!", ask.text);
    Assert.AreEqual("tess", praise.npc);
    Assert.AreEqual("npc_female_01", praise.voice);
    Assert.AreEqual("Great! One!", praise.text);
  }

  // F. Tess roster identity (frozen shape, same fields as Milo/Mia).
  [Test] public void P36F_TessRoster() {
    NpcDefinition tess = NpcRoster.Get("tess");
    Assert.IsNotNull(tess, "tess resolves");
    Assert.AreEqual("Tess", tess.displayName);
    Assert.AreEqual("npc_female_01", tess.voice);
    Assert.AreEqual(2.35f, tess.labelHeight, 0.001f, "label height matches the cast");
    Assert.AreEqual("tess", NpcRoster.ResolveQuestNpc("math_host").id);
    Assert.AreEqual("tess", NpcRoster.ResolveQuestNpc("TESS").id, "alias match case-insensitive");
  }

  // G. Architecture safety: NO Math-WORLD knowledge leaks into shared
  // contracts (WorldTransition / WorldFoundation / WorldNavService / EventBus).
  // Math resolves through SubjectCatalog + entry IDs; Core never names its
  // scene, host, entries or areas. NOTE: SubjectIds.Math (the shared subject
  // identifier, sibling of Thinking/English) LEGITIMATELY lives in
  // WorldFoundation — ban world-specific tokens, not the id. (Plain "Math"
  // also false-positives on Mathf.)
  [Test] public void P36G_NoMathLeakInSharedContracts() {
    var banned = new System.Text.RegularExpressions.Regex(
      @"MathScene|MathWorld|math_counting|math_host|math\.lobby|math\.counting|Counting|Bridge|Tess|tess");
    foreach (string[] parts in new string[][] {
      new string[] { "_SharedKernel", "WorldTransition.cs" },
      new string[] { "_SharedKernel", "WorldFoundation.cs" },
      new string[] { "A_World", "WorldNavService.cs" },
      new string[] { "_SharedKernel", "GameEventBus.cs" },
    }) {
      string src = ReadRepoFile(parts);
      Assert.IsFalse(banned.IsMatch(src),
        string.Join("/", parts) + " must stay subject-agnostic (no Math/Tess leak)");
    }
  }

  // H. Catalog regression: Math stays the only scene-backed subject.
  [Test] public void P36H_CatalogUnchanged() {
    Assert.AreEqual("MathScene", SubjectCatalog.Math.SceneName);
    Assert.IsTrue(string.IsNullOrEmpty(SubjectCatalog.Thinking.SceneName));
    Assert.IsTrue(string.IsNullOrEmpty(SubjectCatalog.English.SceneName));
    Assert.IsTrue(string.IsNullOrEmpty(SubjectCatalog.Vietnamese.SceneName));
  }

  // I. Gameplay Pattern reuse pin: the math pilot classifies as BringToNpc
  // (PATTERN B, like apple/ball) — Gameplay Pattern reuses quest logic,
  // counting behavior is NOT a new Question Type (Phase 3.1 owns that).
  [Test] public void P36I_MathReusesBringPattern() {
    string path = Path.Combine(ContentRoot(), "quests", "math_counting.json");
    QuestEntry q = ContentDatabase.ParseQuest(File.ReadAllText(path));
    var actions = new List<PlayerAction>();
    var targets = new List<string>();
    foreach (QuestObjective o in q.objectives) { actions.Add(o.action); targets.Add(o.target); }
    Assert.AreEqual(QuestPattern.BringToNpc, QuestPatternDef.Classify(actions, targets),
      "math pilot reuses PATTERN B, no new quest logic");
  }
}
