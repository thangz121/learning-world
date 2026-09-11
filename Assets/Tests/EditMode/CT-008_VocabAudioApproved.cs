// CT-008: vocab audio approved + files (mirrors tools/validate_content.py authoring rules).
// Owner: Agent C+D. Wires the Content bundle to Unity: every rule the python
// validator enforces in authoring mode is asserted here over the real
// Content/*.json through the production readers (ContentDatabase.Parse* +
// ValidateWordCount), so Content->Unity can never silently rot.
// SCOPE BOUNDARY (honest, not trivial): --ship file gates (generated=true,
// approved=true, audio binaries on disk) stay with tools/pregen_audio.py +
// the python validator until the pre-gen pack lands (W1). This test pins all
// metadata/schema/voice/word-cap/quest-target/pack invariants; it MUST fail
// if authoring content regresses, and MUST NOT fake approvals or binaries.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class CT_008_VocabAudioApproved {
  static readonly string[] AllowedVoices = {
    "milo_v1", "learning_v1", "mia_v1",
    "npc_female_01", "npc_female_02", "npc_female_03",
    "npc_male_01", "npc_male_02", "npc_male_03",
  };
  static readonly string[] HintFreeze = { "visual_glow", "milo_point", "milo_demo", "auto_simplify" };
  static readonly string[] BannedVoiceNames = { "neural2", "wavenet", "chirp", "googlesamples" };

  static string ContentRoot() {
    string assets = Application.dataPath;
    string root = Path.Combine(Directory.GetParent(assets).FullName, "Content");
    Assert.IsTrue(Directory.Exists(root), "Content/ bundle must exist next to Assets (wired to Unity)");
    return root;
  }

  static string Stem(string path) { return Path.GetFileNameWithoutExtension(path); }

  static void AssertVoicePool(string voice, string ctx) {
    Assert.IsTrue(Array.IndexOf(AllowedVoices, voice) >= 0,
      ctx + ": voice '" + voice + "' must be a VoiceProfileId, never a Google voice name");
  }

  static void AssertNpcVoiceCompat(string npc, string voice, string ctx) {
    if (npc == "milo") Assert.AreEqual("milo_v1", voice, ctx + ": milo must use milo_v1");
    else if (npc == "mia") Assert.AreEqual("mia_v1", voice, ctx + ": mia must use mia_v1");
    else if (npc != null && npc.StartsWith("npc_"))
      Assert.IsTrue(voice != null && (voice.StartsWith("npc_female_") || voice.StartsWith("npc_male_")),
        ctx + ": pool npc '" + npc + "' must use npc_female_*/npc_male_*, got '" + voice + "'");
  }

  [Test] public void CT_008() {
    string root = ContentRoot();

    // ---- vocab: 50 files / 15 active / 35 passive, schema + audio metadata ----
    string[] vocabFiles = Directory.GetFiles(Path.Combine(root, "vocab"), "*.json");
    Assert.AreEqual(50, vocabFiles.Length, "vocab pack must be 50 files");
    var vocabs = new List<VocabEntry>();
    var vocabIds = new HashSet<string>();
    int active = 0, passive = 0;
    foreach (string path in vocabFiles) {
      string raw = File.ReadAllText(path);
      string blob = raw.ToLowerInvariant();
      foreach (string banned in BannedVoiceNames)
        Assert.IsFalse(blob.Contains(banned), Stem(path) + ": no Google voice names in content");
      VocabEntry v = ContentDatabase.ParseVocab(raw);
      Assert.AreEqual(Stem(path), v.id, "vocab id must match filename");
      vocabIds.Add(v.id);
      vocabs.Add(v);
      if (v.active) {
        active++;
        Assert.IsTrue(v.tags != null && v.tags.Count > 0, "active " + v.id + ": missing semantic.tags");
        Assert.IsTrue(v.expectedForms != null && v.expectedForms.Count > 0,
          "active " + v.id + ": missing speech.expectedForms");
        Assert.IsFalse(string.IsNullOrEmpty(v.audioNormal), "active " + v.id + ": missing audio.normal");
        Assert.IsFalse(string.IsNullOrEmpty(v.audioSlow), "active " + v.id + ": missing audio.slow");
        Assert.AreEqual("learning_v1", v.audioVoice, "active " + v.id + ": audio.voice must be learning_v1");
        Assert.AreEqual("en-US", v.audioLang, "active " + v.id + ": audio.lang must freeze en-US");
      } else {
        passive++;
      }
    }
    Assert.AreEqual(15, active, "active vocab must be 15");
    Assert.AreEqual(35, passive, "passive vocab must be 35");

    // ---- quests: >=5, schema freeze, targets resolve to vocab ----
    string[] questFiles = Directory.GetFiles(Path.Combine(root, "quests"), "*.json");
    Assert.GreaterOrEqual(questFiles.Length, 5, "quests must be >= 5");
    foreach (string path in questFiles) {
      QuestEntry q = ContentDatabase.ParseQuest(File.ReadAllText(path));
      Assert.AreEqual(Stem(path), q.id, "quest id must match filename");
      Assert.IsTrue(q.hintLevels != null && q.hintLevels.Count == HintFreeze.Length,
        "quest " + q.id + ": hint_levels must be the 4-level freeze");
      for (int i = 0; i < HintFreeze.Length; i++)
        Assert.AreEqual(HintFreeze[i], q.hintLevels[i], "quest " + q.id + ": hint_levels order frozen");
      Assert.IsTrue(q.simplifyReduceChoices > 0 && q.simplifyDemo,
        "quest " + q.id + ": missing simplify_path");
      Assert.IsTrue(q.oneObjectiveAtATime, "quest " + q.id + ": one_objective_at_a_time must be true");
      Assert.IsNotNull(q.objectives, "quest " + q.id + ": objectives missing");
      foreach (QuestObjective o in q.objectives) {
        Assert.IsFalse(string.IsNullOrEmpty(o.id), "quest " + q.id + ": objective id missing");
        // ParsePlayerAction already ran at the boundary (unknown action throws);
        // target must resolve to a real vocab file.
        Assert.IsTrue(!string.IsNullOrEmpty(o.target) && vocabIds.Contains(o.target),
          "quest " + q.id + ": target '" + o.target + "' has no vocab file");
      }
    }

    // ---- dialogue pack: manifest 30-40 lines, voices, word caps, audio mapping ----
    string manifestPath = Path.Combine(Path.Combine(root, "dialogues"), "manifest.json");
    Assert.IsTrue(File.Exists(manifestPath), "dialogues/manifest.json must exist (pack 30-40 pre-gen lines)");
    DialoguePack pack = ContentDatabase.ParseDialoguePack(File.ReadAllText(manifestPath));
    Assert.AreEqual(pack.count, pack.lines.Count, "manifest.count must match real lines");
    Assert.GreaterOrEqual(pack.lines.Count, 30, "dialogue pack must be 30-40 lines");
    Assert.LessOrEqual(pack.lines.Count, 40, "dialogue pack must be 30-40 lines");
    Assert.AreEqual("en-US", pack.lang, "manifest.lang must freeze en-US");
    var seenIds = new HashSet<string>();
    var seenText = new HashSet<string>();
    foreach (DialogueLine l in pack.lines) {
      Assert.IsFalse(string.IsNullOrEmpty(l.id), "dialogue line id missing");
      Assert.IsTrue(seenIds.Add(l.id), "pack: duplicate line id '" + l.id + "'");
      Assert.IsFalse(string.IsNullOrEmpty(l.npc), l.id + ": missing npcId");
      AssertVoicePool(l.voice, "pack " + l.id);
      AssertNpcVoiceCompat(l.npc, l.voice, "pack " + l.id);
      Assert.IsTrue(ContentDatabase.ValidateWordCount(l.text, l.voice == "milo_v1"),
        "pack " + l.id + ": word count over limit ('" + l.text + "')");
      Assert.IsFalse(string.IsNullOrEmpty(l.audio), "pack " + l.id + ": missing audio asset mapping");
      Assert.IsTrue(seenText.Add(l.npc + "|" + l.text), "pack " + l.id + ": duplicate dialogue text");
    }
  }
}
