// CT-P13: Phase 2.1-local speech assessment engine — deterministic EditMode suite.
// M1 scope: target pronunciation data (content-owned phonemes -> engine contract).
// Later milestones extend THIS file (M2 DSP core, M3 provider+policy, M4 benchmark).
// Labels: SIMULATED = synthetic fixtures through the real code path (honest);
// PROVEN = observed on real hardware/audio; NOT PROVEN = explicitly listed gaps.
using NUnit.Framework;
using System;
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

// CT-P13 M2: DSP core on SIMULATED fixtures (deterministic formant synthesis
// through the REAL ExtractFrames->Normalize->DTW->segmental path).
// SIMULATED, not child speech: proves the machinery discriminates
// full/partial/wrong/repeated shapes; child-human validity stays NOT PROVEN.
public static class AcousticFixtures {
  public const int SR = 16000;
  const float F0 = 260f; // child-like pitch (synthetic; NOT a speaker model)

  static float[] RenderVowel(float f1, float f2, float durSec, float amp) {
    int n = (int)(durSec * SR);
    var s = new float[n];
    for (int i = 0; i < n; i++) {
      float t = (float)i / SR;
      float env = Math.Min(1f, i / (0.02f * SR)) * Math.Min(1f, (n - i) / (0.03f * SR));
      double v = Math.Sin(2 * Math.PI * F0 * t)
        + 0.5 * Math.Sin(2 * Math.PI * 2 * F0 * t)
        + 0.9 * Math.Sin(2 * Math.PI * f1 * t)
        + 0.6 * Math.Sin(2 * Math.PI * f2 * t)
        + 0.25 * Math.Sin(2 * Math.PI * 3 * F0 * t);
      s[i] = amp * env * (float)(v / 3.0);
    }
    return s;
  }

  static uint _rng = 12345;
  static float Rand() {
    _rng = _rng * 1664525u + 1013904223u;
    return (float)(_rng >> 8) / 16777216f * 2f - 1f;
  }

  static float[] RenderNoise(float durSec, float amp, float bright, float voiceAmp) {
    int n = (int)(durSec * SR);
    var s = new float[n];
    float prev = 0f;
    for (int i = 0; i < n; i++) {
      float t = (float)i / SR;
      float w = Rand();
      float hp = w - prev * (1f - bright); // brighter = harsher highpass
      prev = w;
      float v = hp * 0.7f + voiceAmp * (float)Math.Sin(2 * Math.PI * 150f * t);
      float env = Math.Min(1f, i / (0.01f * SR)) * Math.Min(1f, (n - i) / (0.02f * SR));
      s[i] = amp * env * v;
    }
    return s;
  }

  static float[] RenderStop(bool voiced, float amp) {
    // 30 ms closure (near-silence) + burst + 25 ms voice-bar/aspiration.
    // Short transient like a real child stop (50 ms tails created phantom regimes).
    int closure = (int)(0.03f * SR), tail = (int)(0.025f * SR);
    var s = new float[closure + tail];
    for (int i = 0; i < tail; i++) {
      float t = (float)i / SR;
      float env = 1f - (float)i / tail;
      float v = Rand() * 0.6f * env;
      if (voiced) v += 0.8f * (float)Math.Sin(2 * Math.PI * 130f * t);
      s[closure + i] = amp * v * 0.8f;
    }
    return s;
  }

  // One phoneme render. Coarse acoustic cousins (NOT speaker/phoneme models):
  // vowels/sonorants differ by F-shapes, fricatives by noise, stops by burst.
  public static float[] RenderPhoneme(string id) {
    switch (id) {
      case "AO": return RenderVowel(500f, 900f, 0.14f, 0.5f);
      case "AE": return RenderVowel(700f, 1800f, 0.13f, 0.5f);
      case "AH": return RenderVowel(700f, 1200f, 0.12f, 0.5f);
      case "EH": return RenderVowel(550f, 1800f, 0.12f, 0.5f);
      case "IY": return RenderVowel(300f, 2200f, 0.13f, 0.5f);
      case "L": return RenderVowel(400f, 1200f, 0.10f, 0.38f);
      case "R": return RenderVowel(350f, 1300f, 0.10f, 0.38f);
      case "W": return RenderVowel(350f, 800f, 0.06f, 0.36f);
      case "Y": return RenderVowel(300f, 2000f, 0.06f, 0.36f);
      case "M":
      case "N":
      case "NG": return RenderVowel(250f, 900f, 0.10f, 0.34f);
      case "Z":
      case "V":
      case "DH": return RenderNoise(0.12f, 0.4f, 0.6f, 0.5f);
      case "S":
      case "F":
      case "TH":
      case "SH":
      case "HH": return RenderNoise(0.12f, 0.42f, 0.8f, 0f);
      case "B":
      case "D":
      case "G":
      case "JH": return RenderStop(true, 0.5f);
      case "P":
      case "T":
      case "K":
      case "CH": return RenderStop(false, 0.5f);
      default: return RenderVowel(600f, 1400f, 0.10f, 0.4f);
    }
  }

  public static float[] RenderWord(IList<string> phonemes, float rate) {
    var parts = new List<float[]>();
    int total = 0;
    for (int i = 0; i < phonemes.Count; i++) {
      float[] p = RenderPhoneme(phonemes[i]);
      int stretch = Math.Max(8, (int)(p.Length * rate));
      float[] st = new float[stretch];
      for (int k = 0; k < stretch; k++)
        st[k] = p[Math.Min(p.Length - 1, (int)((float)k / stretch * p.Length))];
      parts.Add(st);
      total += stretch;
    }
    // 25 ms crossfade joins (coarticulation sketch).
    int fade = SR * 25 / 1000;
    int outLen = total + 2 * fade;
    var s = new float[outLen];
    int at = fade;
    for (int i = 0; i < parts.Count; i++) {
      float[] p = parts[i];
      for (int k = 0; k < p.Length; k++) {
        float w = 1f;
        if (k < fade && at + k - fade >= 0) w = (float)k / fade * 0.5f + 0.5f;
        s[at + k] += p[k] * w;
      }
      at += p.Length;
    }
    return s;
  }

  public static float[] Word(string word, float rate) {
    return RenderWord(BenchmarkIds(word), rate);
  }

  public static List<string> BenchmarkIds(string word) {
    switch (word) {
      case "ball": return new List<string> { "B", "AO", "L" };
      case "apple": return new List<string> { "AE", "P", "AH", "L" };
      case "red": return new List<string> { "R", "EH", "D" };
      case "one": return new List<string> { "W", "AH", "N" };
      case "please": return new List<string> { "P", "L", "IY", "Z" };
      case "teddy": return new List<string> { "T", "EH", "D", "IY" };
      default: return new List<string> { "AH" };
    }
  }

  public static float[] Truncated(string word, int keepFirst) {
    var ids = BenchmarkIds(word).GetRange(0, keepFirst);
    return RenderWord(ids, 1f);
  }

  public static float[] SubOnset(string word, string otherOnset) {
    var ids = new List<string>(BenchmarkIds(word));
    ids[0] = otherOnset;
    return RenderWord(ids, 1f);
  }

  public static float[] Quiet(float[] s, float gain) {
    var o = new float[s.Length];
    for (int i = 0; i < s.Length; i++) o[i] = s[i] * gain;
    return o;
  }

  public static float[] Noisy(float[] s, float snrDb) {
    var o = new float[s.Length];
    double sig = 0;
    for (int i = 0; i < s.Length; i++) sig += s[i] * s[i];
    sig = Math.Sqrt(sig / Math.Max(1, s.Length));
    double noiseRms = sig / Math.Pow(10.0, snrDb / 20.0);
    for (int i = 0; i < s.Length; i++)
      o[i] = s[i] + (float)(Rand() * noiseRms * 1.4);
    return o;
  }

  public static float[] Concat(params float[][] parts) {
    int gap = SR / 10, total = gap * 2;
    foreach (var p in parts) total += p.Length + gap;
    var s = new float[total];
    int at = gap;
    foreach (var p in parts) {
      for (int i = 0; i < p.Length; i++) s[at + i] = p[i];
      at += p.Length + gap;
    }
    return s;
  }

  public static float[] Silence(float durSec) { return new float[(int)(durSec * SR)]; }

  public static TargetPronunciation Pron(string word) {
    return PhonemeTable.FromIds(new WordId(word), BenchmarkIds(word));
  }
}

public class CT_P13_M2_DspCore {
  [Test] public void P13M2_FftCentroidReadsTone() {
    int sr = 16000, n = 1600;
    var s = new float[n];
    for (int i = 0; i < n; i++) s[i] = 0.5f * (float)Math.Sin(2 * Math.PI * 440 * i / sr);
    FrameFeatures[] f = AcousticAnalysis.ExtractFrames(s, sr);
    Assert.Greater(f.Length, 3);
    double meanC = 0;
    for (int i = 0; i < f.Length; i++) meanC += f[i].Centroid;
    meanC /= f.Length;
    Assert.AreEqual(440.0 / 8000.0, meanC, 0.03, "centroid of a 440 Hz tone ≈ 0.055");
  }

  [Test] public void P13M2_SilenceGivesNoEnergy() {
    FrameFeatures[] f = AcousticAnalysis.ExtractFrames(AcousticFixtures.Silence(0.5f), 16000);
    foreach (FrameFeatures x in f) {
      Assert.AreEqual(0f, x.Energy, 1e-6f);
      Assert.AreEqual(0f, x.Zcr, 1e-6f);
    }
  }

  [Test] public void P13M2_SelfMatchDetectsAllPresentEnding() {
    float[] ball = AcousticFixtures.Word("ball", 1f);
    AcousticEvidence ev = AcousticAnalysis.Analyze(ball, 16000, AcousticFixtures.Pron("ball"));
    Assert.IsTrue(ev.HasAcousticData);
    Assert.GreaterOrEqual(ev.OverallMatch, 0.4f, "SIMULATED self-match must be credible (notes=" + ev.Notes + ")");
    Assert.AreEqual(3, ev.PerPhoneme.Length);
    foreach (PhonemeAcousticEvidence p in ev.PerPhoneme)
      Assert.IsTrue(p.Detected, p.PhonemeId + " should detect on self-match (" + ev.Notes + ")");
    Assert.IsFalse(ev.MissingEnding, "full ball keeps its L (" + ev.Notes + ")");
    Assert.AreEqual(1, ev.EstimatedSyllables);
  }

  [Test] public void P13M2_TruncatedBallIsMissingEnding() {
    float[] ba = AcousticFixtures.Truncated("ball", 2); // B AO, no L
    AcousticEvidence ev = AcousticAnalysis.Analyze(ba, 16000, AcousticFixtures.Pron("ball"));
    Assert.IsTrue(ev.HasAcousticData);
    Assert.IsTrue(ev.PerPhoneme[0].Detected, "B stays detected");
    Assert.IsTrue(ev.PerPhoneme[1].Detected, "vowel stays detected");
    Assert.IsTrue(ev.PerPhoneme[2].Missing, "final L must read missing, not wrong-word (" + ev.Notes + ")");
    Assert.IsTrue(ev.MissingEnding, "MANDATORY §5 case: ba-for-ball flags MissingEnding (" + ev.Notes + ")");
  }

  [Test] public void P13M2_WrongWordScoresBelowSelf() {
    float[] apple = AcousticFixtures.Word("apple", 1f);
    AcousticEvidence asBall = AcousticAnalysis.Analyze(apple, 16000, AcousticFixtures.Pron("ball"));
    AcousticEvidence asApple = AcousticAnalysis.Analyze(apple, 16000, AcousticFixtures.Pron("apple"));
    Assert.IsTrue(asBall.HasAcousticData && asApple.HasAcousticData);
    Assert.Greater(asApple.OverallMatch, asBall.OverallMatch,
      "closed-set: apple audio must resemble apple > ball (" + asApple.Notes + " vs " + asBall.Notes + ")");
  }

  [Test] public void P13M2_ClosedSetArgminPicksTarget() {
    string[] words = { "ball", "apple", "red", "one", "please", "teddy" };
    foreach (string w in words) {
      float[] s = AcousticFixtures.Word(w, 1f);
      string best = null;
      float bestScore = -1f;
      foreach (string c in words) {
        AcousticEvidence ev = AcousticAnalysis.Analyze(s, 16000, AcousticFixtures.Pron(c));
        if (ev.OverallMatch > bestScore) { bestScore = ev.OverallMatch; best = c; }
      }
      Assert.AreEqual(w, best, "SIMULATED closed-set must classify " + w + " (score " + bestScore.ToString("0.0") + ")");
    }
  }

  [Test] public void P13M2_QuietKeepsMatchAfterNormalization() {
    float[] loud = AcousticFixtures.Word("ball", 1f);
    float[] quiet = AcousticFixtures.Quiet(loud, 0.12f);
    AcousticEvidence a = AcousticAnalysis.Analyze(loud, 16000, AcousticFixtures.Pron("ball"));
    AcousticEvidence b = AcousticAnalysis.Analyze(quiet, 16000, AcousticFixtures.Pron("ball"));
    Assert.IsTrue(b.HasAcousticData);
    Assert.AreEqual(a.OverallMatch, b.OverallMatch, 0.25f,
      "gain normalization: quiet must nearly match loud (" + a.Notes + " vs " + b.Notes + ")");
  }

  [Test] public void P13M2_RepetitionFlaggedNotFailed() {
    float[] bb = AcousticFixtures.Concat(AcousticFixtures.Word("ball", 1f), AcousticFixtures.Word("ball", 1f));
    AcousticEvidence ev = AcousticAnalysis.Analyze(bb, 16000, AcousticFixtures.Pron("ball"));
    Assert.IsTrue(ev.HasAcousticData);
    Assert.IsTrue(ev.IsRepetition, "ball ball must flag repetition (" + ev.Notes + ")");
    Assert.GreaterOrEqual(ev.OverallMatch, 0.3f, "repetition still resembles the target");
  }

  [Test] public void P13M2_AppleCountsTwoSyllables() {
    float[] apple = AcousticFixtures.Word("apple", 1f);
    AcousticEvidence ev = AcousticAnalysis.Analyze(apple, 16000, AcousticFixtures.Pron("apple"));
    Assert.AreEqual(2, ev.EstimatedSyllables, "apple = 2 humps (" + ev.Notes + ")");
  }

  [Test] public void P13M2_NullEmptyNeverThrows() {
    Assert.IsFalse(AcousticAnalysis.Analyze(null, 16000, AcousticFixtures.Pron("ball")).HasAcousticData);
    Assert.IsFalse(AcousticAnalysis.Analyze(new float[0], 16000, AcousticFixtures.Pron("ball")).HasAcousticData);
    Assert.IsFalse(AcousticAnalysis.Analyze(AcousticFixtures.Word("ball", 1f), 16000, null).HasAcousticData);
    Assert.IsFalse(AcousticAnalysis.Analyze(AcousticFixtures.Silence(0.02f), 16000, AcousticFixtures.Pron("ball")).HasAcousticData);
  }

  [Test] public void P13M2_TwoSecondAnalysisBurstsFast() {
    float[] s = AcousticFixtures.Concat(
      AcousticFixtures.Word("apple", 1.4f), AcousticFixtures.Word("please", 1.2f));
    var sw = System.Diagnostics.Stopwatch.StartNew();
    AcousticEvidence ev = AcousticAnalysis.Analyze(s, 16000, AcousticFixtures.Pron("apple"));
    sw.Stop();
    Assert.IsTrue(ev.HasAcousticData);
    Assert.Less(sw.ElapsedMilliseconds, 500, "burst budget: 2 s audio must analyze in <500 ms (took " + sw.ElapsedMilliseconds + " ms)");
  }
}
