// CT-P13: Phase 2.1-local speech assessment engine — deterministic EditMode suite.
// M1 scope: target pronunciation data (content-owned phonemes -> engine contract).
// Later milestones extend THIS file (M2 DSP core, M3 provider+policy, M4 benchmark).
// Labels: SIMULATED = synthetic fixtures through the real code path (honest);
// PROVEN = observed on real hardware/audio; NOT PROVEN = explicitly listed gaps.
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class CT_P13_LocalAcousticEngine {
  static string ContentRoot() {
    string assets = Application.dataPath;
    string root = Path.Combine(Directory.GetParent(assets).FullName, "Content");
    Assert.IsTrue(Directory.Exists(root), "Content/ bundle must exist next to Assets");
    return root;
  }

  static VocabEntry ParseVocabFile(string root, string id) {
    string path = Path.Combine(Path.Combine(root, "vocab"), id + ".json");
    Assert.IsTrue(File.Exists(path), "vocab/" + id + ".json must exist");
    return ContentDatabase.ParseVocab(File.ReadAllText(path));
  }

  // ---------- M1-A. phoneme table (engine contract, no content) ----------

  [Test] public void P13A_BenchmarkIdsResolveWithManners() {
    TargetPhoneme p;
    Assert.IsTrue(PhonemeTable.TryResolve("B", out p) && p.Manner == PhonemeManner.Stop && p.Voiced);
    Assert.IsTrue(PhonemeTable.TryResolve("AO", out p) && p.Manner == PhonemeManner.Vowel);
    Assert.IsTrue(PhonemeTable.TryResolve("L", out p) && p.Manner == PhonemeManner.Liquid && p.Voiced);
    Assert.IsTrue(PhonemeTable.TryResolve("P", out p) && p.Manner == PhonemeManner.Stop && !p.Voiced);
    Assert.IsTrue(PhonemeTable.TryResolve("Z", out p) && p.Manner == PhonemeManner.Fricative && p.Voiced);
    Assert.IsTrue(PhonemeTable.TryResolve("N", out p) && p.Manner == PhonemeManner.Nasal);
    Assert.IsTrue(PhonemeTable.TryResolve("W", out p) && p.Manner == PhonemeManner.Glide);
    Assert.IsTrue(PhonemeTable.TryResolve("R", out p) && p.Manner == PhonemeManner.Liquid);
  }

  [Test] public void P13A_UnknownIdFailsHonestly() {
    TargetPhoneme p;
    Assert.IsFalse(PhonemeTable.TryResolve("XX", out p));
    Assert.IsFalse(PhonemeTable.TryResolve("", out p));
    Assert.IsFalse(PhonemeTable.TryResolve(null, out p));
    Assert.IsNull(PhonemeTable.FromIds(new WordId("ball"), new List<string> { "B", "XX" }),
      "unknown id -> null (engine reports NOT AVAILABLE, never guesses)");
    Assert.IsNull(PhonemeTable.FromIds(new WordId("ball"), new List<string>()));
    Assert.IsNull(PhonemeTable.FromIds(new WordId("ball"), null));
  }

  [Test] public void P13A_SyllableCountsMatchBenchmark() {
    Assert.AreEqual(1, PhonemeTable.FromIds(new WordId("ball"), new List<string> { "B", "AO", "L" }).SyllableCount);
    Assert.AreEqual(2, PhonemeTable.FromIds(new WordId("apple"), new List<string> { "AE", "P", "AH", "L" }).SyllableCount);
    Assert.AreEqual(1, PhonemeTable.FromIds(new WordId("red"), new List<string> { "R", "EH", "D" }).SyllableCount);
    Assert.AreEqual(1, PhonemeTable.FromIds(new WordId("one"), new List<string> { "W", "AH", "N" }).SyllableCount);
    Assert.AreEqual(1, PhonemeTable.FromIds(new WordId("please"), new List<string> { "P", "L", "IY", "Z" }).SyllableCount);
    Assert.AreEqual(2, PhonemeTable.FromIds(new WordId("teddy"), new List<string> { "T", "EH", "D", "IY" }).SyllableCount);
  }

  [Test] public void P13A_FrameBudgetIsProportionalNoWordBranches() {
    var pron = PhonemeTable.FromIds(new WordId("ball"), new List<string> { "B", "AO", "L" });
    int total = 100;
    int b = pron.FrameBudget(0, total), ao = pron.FrameBudget(1, total), l = pron.FrameBudget(2, total);
    Assert.Greater(ao, b, "vowel AO gets a larger budget than stop B (relative weights)");
    Assert.AreEqual(total, b + ao + l, 2, "budgets partition the frame count (rounding tolerance 2)");
  }

  // ---------- M1-B. content -> engine (backward compatible) ----------

  [Test] public void P13B_BenchmarkVocabCarriesPhonemes() {
    string root = ContentRoot();
    var ball = ParseVocabFile(root, "ball");
    CollectionAssert.AreEqual(new[] { "B", "AO", "L" }, ball.phonemes);
    var apple = ParseVocabFile(root, "apple");
    CollectionAssert.AreEqual(new[] { "AE", "P", "AH", "L" }, apple.phonemes);
    var red = ParseVocabFile(root, "red");
    CollectionAssert.AreEqual(new[] { "R", "EH", "D" }, red.phonemes);
    var one = ParseVocabFile(root, "one");
    CollectionAssert.AreEqual(new[] { "W", "AH", "N" }, one.phonemes);
    var please = ParseVocabFile(root, "please");
    CollectionAssert.AreEqual(new[] { "P", "L", "IY", "Z" }, please.phonemes);
    var teddy = ParseVocabFile(root, "teddy");
    CollectionAssert.AreEqual(new[] { "T", "EH", "D", "IY" }, teddy.phonemes);
  }

  [Test] public void P13B_PhonemelessVocabStillParsesEmpty() {
    string root = ContentRoot();
    var bag = ParseVocabFile(root, "bag"); // no phonemes block (backward compat)
    Assert.IsNotNull(bag.phonemes);
    Assert.AreEqual(0, bag.phonemes.Count, "absent phonemes = empty (no assessment), never null/crash");
  }

  [Test] public void P13B_CatalogServesSixBenchmarkWords() {
    string root = ContentRoot();
    string[] ids = { "ball", "apple", "red", "one", "please", "teddy" };
    var entries = new List<VocabEntry>();
    foreach (string id in ids) entries.Add(ParseVocabFile(root, id));
    var catalog = new TargetPronunciationCatalog(entries);
    Assert.AreEqual(6, catalog.Count());
    foreach (string id in ids) {
      TargetPronunciation pron;
      Assert.IsTrue(catalog.TryGet(new WordId(id), out pron), id + " must resolve");
      Assert.IsTrue(pron.IsUsable());
      Assert.AreEqual(id, pron.Word.Value);
    }
    TargetPronunciation missing;
    Assert.IsFalse(catalog.TryGet(new WordId("zebra"), out missing));
    Assert.IsFalse(catalog.TryGet(new WordId("bag"), out missing), "phonemeless word -> false (NOT AVAILABLE)");
  }

  [Test] public void P13B_ScriptedProviderNeedsNoContentFiles() {
    var provider = new ScriptedPronunciationProvider()
      .Add("ball", "B", "AO", "L")
      .Add("red", "R", "EH", "D");
    TargetPronunciation pron;
    Assert.IsTrue(provider.TryGet(new WordId("ball"), out pron));
    Assert.AreEqual(3, pron.Length());
    Assert.IsFalse(provider.TryGet(new WordId("apple"), out pron));
  }
}
