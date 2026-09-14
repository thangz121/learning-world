// CT-P13: Phase 2.1-local speech assessment engine — deterministic EditMode suite.
// M1: target pronunciation data. M2: DSP core on SIMULATED fixtures.
// M3: provider + policy fusion (this file). M4: full benchmark matrix.
// Labels: SIMULATED = synthetic fixtures through the real code path (honest);
// PROVEN = observed on real hardware/audio; NOT PROVEN = explicitly listed gaps.
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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

  static float[] RenderVowel(float f1, float f2, float durSec, float amp, float f0) {
    int n = (int)(durSec * SR);
    var s = new float[n];
    for (int i = 0; i < n; i++) {
      float t = (float)i / SR;
      float env = Math.Min(1f, i / (0.02f * SR)) * Math.Min(1f, (n - i) / (0.03f * SR));
      double v = Math.Sin(2 * Math.PI * f0 * t)
        + 0.5 * Math.Sin(2 * Math.PI * 2 * f0 * t)
        + 0.9 * Math.Sin(2 * Math.PI * f1 * t)
        + 0.6 * Math.Sin(2 * Math.PI * f2 * t)
        + 0.25 * Math.Sin(2 * Math.PI * 3 * f0 * t);
      s[i] = amp * env * (float)(v / 3.0);
    }
    return s;
  }

  static uint _rng = 12345;
  static void Reseed(uint seed) { _rng = seed; }
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
    return RenderStop(voiced, amp, 1f);
  }

  // Duration-scaled stop: closure lengthens with rate (slow speech holds
  // closures), the burst transient itself stays fixed (M4: resampling stops
  // smeared transients unrealistically).
  static float[] RenderStop(bool voiced, float amp, float rate) {
    // 30 ms closure (near-silence) + burst + 25 ms voice-bar/aspiration.
    // Burst scaled to vowel level (M4 DIAG: hotter bursts dominated peak-norm
    // and read as utterance peaks).
    int closure = (int)(0.03f * rate * SR), tail = (int)(0.025f * SR);
    var s = new float[closure + tail];
    for (int i = 0; i < tail; i++) {
      float t = (float)i / SR;
      float env = 1f - (float)i / tail;
      float v = Rand() * 0.6f * env;
      if (voiced) v += 0.8f * (float)Math.Sin(2 * Math.PI * 130f * t);
      s[closure + i] = amp * v * 0.55f;
    }
    return s;
  }

  // One phoneme render. Coarse acoustic cousins (NOT speaker/phoneme models):
  // vowels/sonorants differ by F-shapes, fricatives by noise, stops by burst.
  // f0 is parametric so pitch-height/contour robustness is testable (tonal-L1 kids).
  public static float[] RenderPhoneme(string id) { return RenderPhoneme(id, 260f); }

  public static float[] RenderPhoneme(string id, float f0) {
    return RenderPhoneme(id, f0, 1f);
  }

  // Duration-scaled render (M4 CORRECTNESS FIX): slow/fast speech stretches
  // TIME (more cycles at the same F0/formants), never resamples (which would
  // shift pitch+formants like slowing tape — wrong vocal-tract model).
  public static float[] RenderPhoneme(string id, float f0, float rate) {
    switch (id) {
      case "AO": return RenderVowel(500f, 900f, 0.14f * rate, 0.5f, f0);
      case "AE": return RenderVowel(700f, 1800f, 0.13f * rate, 0.5f, f0);
      case "AH": return RenderVowel(700f, 1200f, 0.12f * rate, 0.5f, f0);
      case "EH": return RenderVowel(550f, 1800f, 0.12f * rate, 0.5f, f0);
      case "IY": return RenderVowel(300f, 2200f, 0.13f * rate, 0.5f, f0);
      case "L": return RenderVowel(400f, 1200f, 0.10f * rate, 0.38f, f0);
      case "R": return RenderVowel(350f, 1300f, 0.10f * rate, 0.38f, f0);
      case "W": return RenderVowel(350f, 800f, 0.06f * rate, 0.36f, f0);
      case "Y": return RenderVowel(300f, 2000f, 0.06f * rate, 0.36f, f0);
      case "M":
      case "N":
      case "NG": return RenderVowel(250f, 900f, 0.10f * rate, 0.34f, f0);
      case "Z":
      case "V":
      case "DH": return RenderNoise(0.12f * rate, 0.4f, 0.6f, 0.5f);
      case "S":
      case "F":
      case "TH":
      case "SH":
      case "HH": return RenderNoise(0.12f * rate, 0.42f, 0.8f, 0f);
      case "B":
      case "D":
      case "G":
      case "JH": return RenderStop(true, 0.5f, rate);
      case "P":
      case "T":
      case "K":
      case "CH": return RenderStop(false, 0.5f, rate);
      default: return RenderVowel(600f, 1400f, 0.10f, 0.4f, f0);
    }
  }

  public static float[] RenderWord(IList<string> phonemes, float rate) {
    return RenderWord(phonemes, rate, 260f, 0f);
  }

  // f0EndDelta != 0: pitch glides f0 -> f0+delta across the word (tone-like
  // contour; proves F0-invariance for tonal-L1 kids — features discard F0).
  public static float[] RenderWord(IList<string> phonemes, float rate, float f0, float f0EndDelta) {
    Reseed(12345); // deterministic per word regardless of test order
    var parts = new List<float[]>();
    int total = 0;
    for (int i = 0; i < phonemes.Count; i++) {
      float fi = f0 + f0EndDelta * i / Math.Max(1, phonemes.Count - 1);
      float[] p = RenderPhoneme(phonemes[i], fi, rate); // duration-scaled, spectrum kept
      parts.Add(p);
      total += p.Length;
    }
    // 5 ms edge fades (click guard only): long fades gift every truncated word
    // a fake "closure" (trailing zeros) and dilute tail energy (M4: wuh/re).
    // Real offsets end in decay, not fade — the 25 ms release envelopes stay.
    int fade = SR * 5 / 1000;
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

  public static float[] WordHigh(string word) {
    return RenderWord(BenchmarkIds(word), 1f, 340f, 0f); // high-pitched child
  }

  public static float[] WordGlide(string word) {
    return RenderWord(BenchmarkIds(word), 1f, 220f, 120f); // tone-like rise
  }

  public static float[] FirstHalf(float[] s) {
    var o = new float[s.Length / 2];
    for (int i = 0; i < o.Length; i++) o[i] = s[i];
    return o;
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

  public static float[] Noisy(float[] s, float snrDb) { return Noisy(s, snrDb, 777); }

  public static float[] Noisy(float[] s, float snrDb, uint seed) {
    Reseed(seed);
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

  public static float[] NoiseOnly(float durSec, float rms, uint seed) {
    Reseed(seed);
    int n = (int)(durSec * SR);
    var s = new float[n];
    for (int i = 0; i < n; i++) s[i] = Rand() * rms;
    return s;
  }

  public static float[] Blip() {
    // 30 ms faint tone: too short for phoneme evidence (VAD-adjacent edge).
    int n = SR * 30 / 1000;
    var s = new float[n];
    for (int i = 0; i < n; i++)
      s[i] = 0.06f * (float)Math.Sin(2 * Math.PI * 440 * i / SR) * (1f - (float)i / n);
    return s;
  }

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
        // Rank on RAW (unquantized total order breaks 0.1-quantum ties honestly;
        // reported OverallMatch stays quantized — never fake precision).
        if (ev.MatchRaw > bestScore) { bestScore = ev.MatchRaw; best = c; }
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

  [Test] public void P13M2_TwoSecondAnalysisBurstsFast() {    float[] s = AcousticFixtures.Concat(
      AcousticFixtures.Word("apple", 1.4f), AcousticFixtures.Word("please", 1.2f));
    var sw = System.Diagnostics.Stopwatch.StartNew();
    AcousticEvidence ev = AcousticAnalysis.Analyze(s, 16000, AcousticFixtures.Pron("apple"));
    sw.Stop();
    Assert.IsTrue(ev.HasAcousticData);
    Assert.Less(sw.ElapsedMilliseconds, 500, "burst budget: 2 s audio must analyze in <500 ms (took " + sw.ElapsedMilliseconds + " ms)");
  }
}

// CT-P13 M3: provider + policy fusion on SIMULATED fixtures through the REAL
// capture -> provider -> policy -> runner path. Offline-first proof: no
// transcript engine anywhere, yet clear attempts pass and missing endings
// read Partial (never transcript-equals-target auto-pass — there IS no transcript).
public class CT_P13_M3_ProviderPolicy {
  sealed class ReadyMic : IMicrophoneDevice {
    public event Action<MicStatus> StatusChanged;
    public MicStatus Status => MicStatus.Ready;
    public string SelectedDevice => "test-mic";
    public string[] Devices => new[] { "test-mic" };
    public SpeechCapability Capability => new SpeechCapability { Status = MicStatus.Ready, DeviceName = "test-mic", DeviceCount = 1 };
    public void Refresh() { }
    public void ReportCaptureFailure() { }
  }

  static ScriptedPronunciationProvider SixWords() {
    return new ScriptedPronunciationProvider()
      .Add("ball", "B", "AO", "L")
      .Add("apple", "AE", "P", "AH", "L")
      .Add("red", "R", "EH", "D")
      .Add("one", "W", "AH", "N")
      .Add("please", "P", "L", "IY", "Z")
      .Add("teddy", "T", "EH", "D", "IY");
  }

  static CapturedSpeech Seg(float[] pcm) {
    float mean = VoiceActivity.MeanAbsolute(pcm);
    float dur = (float)pcm.Length / 16000;
    return new CapturedSpeech {
      Samples = pcm, SampleRate = 16000, Channels = 1,
      DurationSec = dur, MeanEnergy = mean, PeakEnergy = mean * 2f,
      VoicedSec = dur * 0.7f, TimedOut = false, Cancelled = false, Error = string.Empty
    };
  }

  static SpeechRecognitionResult Recognize(float[] pcm, string word, ITargetPronunciationProvider targets) {
    var provider = new LocalAcousticProvider(targets);
    return provider.RecognizeAsync(Seg(pcm), new WordId(word), CancellationToken.None)
      .GetAwaiter().GetResult();
  }

  static SpeakingAssessment Assess(float[] pcm, string word) {
    SpeechRecognitionResult r = Recognize(pcm, word, SixWords());
    return SpeakingPassPolicy.Default().Decide(r, new WordId(word), "spk-m3");
  }

  [Test] public void P13M3_ProviderContractHonest() {
    var provider = new LocalAcousticProvider(SixWords());
    Assert.AreEqual("local-acoustic", provider.ProviderId);
    Assert.IsFalse(provider.ProvidesTranscript);
    Assert.IsFalse(provider.ProvidesPhonemeEvidence, "acoustic != phoneme evidence (honest split)");
    Assert.IsFalse(provider.RequiresNetwork, "offline-first: no network");
  }

  [Test] public void P13M3_ProviderAttachesAcoustic() {
    SpeechRecognitionResult r = Recognize(AcousticFixtures.Word("ball", 1f), "ball", SixWords());
    Assert.IsFalse(r.IsError);
    Assert.IsTrue(r.HasSpeech);
    Assert.IsTrue(string.IsNullOrEmpty(r.Transcript), "no STT: transcript stays empty, never faked");
    Assert.AreEqual(0f, r.RecognitionConfidence);
    Assert.IsTrue(r.Pronunciation.HasAcousticData);
    Assert.GreaterOrEqual(r.Pronunciation.AcousticMatch, 0.4f);
    Assert.IsFalse(r.Pronunciation.AcousticMissingEnding);
    Assert.IsFalse(r.Pronunciation.HasPhonemeData);
  }

  [Test] public void P13M3_ProviderFallsBackWithoutPronunciation() {
    SpeechRecognitionResult r = Recognize(AcousticFixtures.Word("ball", 1f), "bag", SixWords());
    Assert.IsFalse(r.IsError);
    Assert.IsTrue(r.HasSpeech, "VAD evidence is real even without pronunciation data");
    Assert.IsFalse(r.Pronunciation.HasAcousticData, "unknown word: acoustic NOT AVAILABLE (never guessed)");
  }

  [Test] public void P13M3_ProviderNeverThrows() {
    var provider = new LocalAcousticProvider(SixWords());
    var empty = new CapturedSpeech { Samples = null, SampleRate = 16000, Error = string.Empty };
    SpeechRecognitionResult r = provider.RecognizeAsync(empty, new WordId("ball"), CancellationToken.None)
      .GetAwaiter().GetResult();
    Assert.IsTrue(r.IsError || !r.HasSpeech, "null audio degrades gracefully, never throws");
    var errSeg = new CapturedSpeech { Error = SpeechFailureReasons.DeviceError };
    SpeechRecognitionResult e = provider.RecognizeAsync(errSeg, new WordId("ball"), CancellationToken.None)
      .GetAwaiter().GetResult();
    Assert.IsTrue(e.IsError);
    var cts = new CancellationTokenSource();
    cts.Cancel();
    SpeechRecognitionResult c = provider.RecognizeAsync(Seg(AcousticFixtures.Word("ball", 1f)),
      new WordId("ball"), cts.Token).GetAwaiter().GetResult();
    Assert.IsTrue(c.IsError);
    Assert.AreEqual(SpeechFailureReasons.Cancelled, c.ErrorReason);
  }

  [Test] public void P13M3_SelfBallPasses() {
    SpeakingAssessment a = Assess(AcousticFixtures.Word("ball", 1f), "ball");
    Assert.IsTrue(a.Decision == SpeakingDecision.Pass || a.Decision == SpeakingDecision.StrongPass,
      "SIMULATED clear ball must pass offline (got " + a.Decision + ", " + a.Evidence + ")");
    Assert.IsTrue(a.AttemptDetected);
    Assert.IsFalse(a.IsEnvironmentError);
    Assert.IsTrue(a.IntelligibilityIsProxy, "human intelligibility still NOT PROVEN");
  }

  [Test] public void P13M3_TruncatedBaIsPartialWithMissingEnding() {
    SpeakingAssessment a = Assess(AcousticFixtures.Truncated("ball", 2), "ball");
    Assert.AreEqual(SpeakingDecision.Partial, a.Decision,
      "ba-for-ball: Partial, never WrongWord/PASS (got " + a.Decision + ", " + a.Evidence + ")");
    Assert.AreEqual(SpeechLevel.Almost, a.ToSpeechLevel(), "partial never advances the frozen Speak gate");
    StringAssert.Contains("MISSING_ENDING", a.Evidence);
  }

  [Test] public void P13M3_WrongWordAcoustic() {
    SpeakingAssessment a = Assess(AcousticFixtures.Word("apple", 1f), "ball");
    Assert.AreEqual(SpeakingDecision.WrongWord, a.Decision,
      "SIMULATED apple-for-ball must read WrongWord (got " + a.Decision + ", " + a.Evidence + ")");
    Assert.IsFalse(a.IsEnvironmentError);
  }

  [Test] public void P13M3_RepetitionPassesCapped() {
    float[] bb = AcousticFixtures.Concat(
      AcousticFixtures.Word("ball", 1f), AcousticFixtures.Word("ball", 1f));
    SpeakingAssessment a = Assess(bb, "ball");
    Assert.AreEqual(SpeakingDecision.Pass, a.Decision,
      "ball-ball: honored attempt, capped at Pass (got " + a.Decision + ", " + a.Evidence + ")");
  }

  [Test] public void P13M3_NoAcousticStaysPossibleAttempt() {
    // 2.1 honesty path preserved: speech without ANY resemblance engine.
    SpeechRecognitionResult r = Recognize(AcousticFixtures.Word("ball", 1f), "ball", null);
    Assert.IsFalse(r.Pronunciation.HasAcousticData);
    SpeakingAssessment a = SpeakingPassPolicy.Default().Decide(r, new WordId("ball"), "spk-m3");
    Assert.AreEqual(SpeakingDecision.PossibleAttempt, a.Decision);
  }

  [Test] public void P13M3_QuietTooWeakUnchanged() {
    var r = new SpeechRecognitionResult {
      Transcript = "", RecognitionConfidence = 0f, HasSpeech = true,
      AudioDurationSec = 0.8f, SpeechDurationSec = 0.6f, MeanEnergy = 0.002f,
      ProviderId = "local-acoustic", LatencyMs = 1, ErrorReason = "", IsError = false,
      Pronunciation = PronunciationEvidence.None()
    };
    SpeakingAssessment a = SpeakingPassPolicy.Default().Decide(r, new WordId("ball"), "spk-m3");
    Assert.AreEqual(SpeakingDecision.TooWeak, a.Decision, "VAD energy gates stay before acoustic evidence");
  }

  static SpeechRecognitionResult ScriptedAcoustic(
      string transcript, float conf, float match, bool missingEnding) {
    var pron = PronunciationEvidence.None();
    pron.HasAcousticData = true;
    pron.AcousticMatch = match;
    pron.AcousticOnset = 0.7f;
    pron.AcousticCoda = missingEnding ? 0.2f : 0.8f;
    pron.AcousticMissingEnding = missingEnding;
    return new SpeechRecognitionResult {
      Transcript = transcript, RecognitionConfidence = conf, HasSpeech = true,
      AudioDurationSec = 0.8f, SpeechDurationSec = 0.6f, MeanEnergy = 0.05f,
      ProviderId = "fake", LatencyMs = 5, ErrorReason = "", IsError = false,
      Pronunciation = pron
    };
  }

  [Test] public void P13M3_TranscriptPassDowngradedByMissingEnding() {
    // §16: ASR "ball" + audio missing the L must NEVER auto-pass.
    SpeechRecognitionResult r = ScriptedAcoustic("ball", 0.9f, 0.6f, true);
    SpeakingAssessment a = SpeakingPassPolicy.Default().Decide(r, new WordId("ball"), "spk-m3");
    Assert.AreEqual(SpeakingDecision.Partial, a.Decision,
      "transcript says ball but coda missing -> Partial (got " + a.Decision + ")");
  }

  [Test] public void P13M3_TranscriptPassStandsWithGoodAcoustic() {
    SpeechRecognitionResult r = ScriptedAcoustic("ball", 0.9f, 0.8f, false);
    SpeakingAssessment a = SpeakingPassPolicy.Default().Decide(r, new WordId("ball"), "spk-m3");
    Assert.AreEqual(SpeakingDecision.StrongPass, a.Decision);
  }

  [Test] public void P13M3_TranscriptPassDowngradedByLowMatch() {
    SpeechRecognitionResult r = ScriptedAcoustic("ball", 0.9f, 0.2f, false);
    SpeakingAssessment a = SpeakingPassPolicy.Default().Decide(r, new WordId("ball"), "spk-m3");
    Assert.AreEqual(SpeakingDecision.Partial, a.Decision,
      "confident transcript + disagreeing audio -> Partial (got " + a.Decision + ")");
  }

  [Test] public void P13M3_RunnerPassesBallEndToEnd() {
    var mic = new ReadyMic();
    var capture = new FakeSpeechCapture(() => Seg(AcousticFixtures.Word("ball", 1f)));
    var recognizer = new SpeechRecognizer(mic, capture, new LocalAcousticProvider(SixWords()));
    var bus = new GameEventBus();
    WordSpokenEvent? spoken = null;
    bus.Subscribe<WordSpokenEvent>(e => spoken = e);
    var runner = new SpeakingExerciseRunner(recognizer, null, bus);
    SpeakingExerciseResult result = runner.RunAsync(
      SpeakingExerciseConfig.DefaultFor(new WordId("ball")), CancellationToken.None)
      .GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.Passed, result.Outcome);
    Assert.AreEqual(1, result.AttemptsUsed);
    Assert.IsTrue(spoken.HasValue, "pass publishes learning evidence");
  }

  [Test] public void P13M3_RunnerPartialCompletesBa() {
    var mic = new ReadyMic();
    var capture = new FakeSpeechCapture(() => Seg(AcousticFixtures.Truncated("ball", 2)));
    var recognizer = new SpeechRecognizer(mic, capture, new LocalAcousticProvider(SixWords()));
    var bus = new GameEventBus();
    var runner = new SpeakingExerciseRunner(recognizer, null, bus);
    var config = SpeakingExerciseConfig.DefaultFor(new WordId("ball"));
    config.attemptsAllowed = 1;
    SpeakingExerciseResult result = runner.RunAsync(config, CancellationToken.None)
      .GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.PartialComplete, result.Outcome);
    Assert.AreEqual("Try again.", result.ChildMessage, "child hears encouragement, never a score");
  }
}

// CT-P13 M4: acceptance benchmark A–P (§20) on SIMULATED fixtures + performance.
// Every verdict below is SIMULATED (deterministic synthesis through the real
// path). Real-child validity stays NOT PROVEN until curated samples land
// (P13M4_RealSamplesIfPresent, currently Ignore-d by design).
public class CT_P13_M4_Benchmark {
  static readonly string[] Words = { "ball", "apple", "red", "one", "please", "teddy" };

  sealed class NoDeviceMic : IMicrophoneDevice {
    public event Action<MicStatus> StatusChanged;
    public MicStatus Status => MicStatus.NoDevice;
    public string SelectedDevice => null;
    public string[] Devices => new string[0];
    public SpeechCapability Capability => new SpeechCapability { Status = MicStatus.NoDevice };
    public void Refresh() { }
    public void ReportCaptureFailure() { }
  }

  sealed class DelayedProvider : ISpeechAssessmentProvider {
    public string ProviderId => "delayed";
    public bool ProvidesTranscript => false;
    public bool ProvidesPhonemeEvidence => false;
    public bool RequiresNetwork => false;
    public async System.Threading.Tasks.Task<SpeechRecognitionResult> RecognizeAsync(
        CapturedSpeech audio, WordId target, CancellationToken ct) {
      await System.Threading.Tasks.Task.Delay(3000, ct);
      return new SpeechRecognitionResult { ProviderId = "delayed", IsError = true,
        ErrorReason = SpeechFailureReasons.ProviderError };
    }
  }

  static ScriptedPronunciationProvider SixWords() {
    return new ScriptedPronunciationProvider()
      .Add("ball", "B", "AO", "L")
      .Add("apple", "AE", "P", "AH", "L")
      .Add("red", "R", "EH", "D")
      .Add("one", "W", "AH", "N")
      .Add("please", "P", "L", "IY", "Z")
      .Add("teddy", "T", "EH", "D", "IY");
  }

  static CapturedSpeech Seg(float[] pcm) {
    float mean = VoiceActivity.MeanAbsolute(pcm);
    float dur = (float)pcm.Length / 16000;
    return new CapturedSpeech {
      Samples = pcm, SampleRate = 16000, Channels = 1,
      DurationSec = dur, MeanEnergy = mean, PeakEnergy = mean * 2f,
      VoicedSec = dur * 0.7f, TimedOut = false, Cancelled = false, Error = string.Empty
    };
  }

  static SpeakingAssessment Assess(float[] pcm, string word) {
    var provider = new LocalAcousticProvider(SixWords());
    SpeechRecognitionResult r = provider.RecognizeAsync(
      Seg(pcm), new WordId(word), CancellationToken.None).GetAwaiter().GetResult();
    return SpeakingPassPolicy.Default().Decide(r, new WordId(word), "spk-m4");
  }

  static bool Passed(SpeakingDecision d) {
    return d == SpeakingDecision.Pass || d == SpeakingDecision.StrongPass;
  }

  // ---------- A. clear targets ----------
  [Test] public void P13M4_A_ClearTargetsPass() {
    foreach (string w in Words) {
      SpeakingAssessment a = Assess(AcousticFixtures.Word(w, 1f), w);
      Assert.IsTrue(Passed(a.Decision),
        "SIMULATED clear " + w + " must pass (got " + a.Decision + ", " + a.Evidence + ")");
    }
  }

  // ---------- B+C. partials + missing endings ----------
  static float[] TruncatedTo(string word, int keep) {
    return AcousticFixtures.RenderWord(
      AcousticFixtures.BenchmarkIds(word).GetRange(0, keep), 1f);
  }

  [Test] public void P13M4_BC_PartialsNeverFailOrPass() {
    var cases = new Dictionary<string, int> {
      { "ball", 2 }, { "apple", 3 }, { "red", 2 },
      { "one", 2 }, { "please", 3 }, { "teddy", 3 }
    };
    foreach (var kv in cases) {
      SpeakingAssessment a = Assess(TruncatedTo(kv.Key, kv.Value), kv.Key);
      Assert.AreEqual(SpeakingDecision.Partial, a.Decision,
        "SIMULATED partial " + kv.Key + " must be Partial (got " + a.Decision + ", " + a.Evidence + ")");
    }
  }

  [Test] public void P13M4_C_MissingEndingFlagged() {
    // Abrupt loud ends (sonorant/fricative codas): MISSING_ENDING expected.
    foreach (string w in new[] { "ball", "apple", "red", "one", "please" }) {
      int keep = AcousticFixtures.BenchmarkIds(w).Count - 1;
      SpeakingAssessment a = Assess(TruncatedTo(w, keep), w);
      StringAssert.Contains("MISSING_ENDING", a.Evidence,
        "SIMULATED " + w + "-minus-coda must flag MissingEnding (" + a.Evidence + ")");
    }
    // teddy-minus-IY ends in a stop burst + decay: Partial with weak-or-missing coda.
    SpeakingAssessment t = Assess(TruncatedTo("teddy", 3), "teddy");
    Assert.AreEqual(SpeakingDecision.Partial, t.Decision);
    Assert.IsTrue(t.Evidence.Contains("MISSING_ENDING") || t.Evidence.Contains("weak-ending"),
      "tedd must mark the coda (" + t.Evidence + ")");
  }

  // ---------- D. substitutions ----------
  [Test] public void P13M4_D_CoarseSubstitutionNotPassed() {
    float[] sall = AcousticFixtures.SubOnset("ball", "S"); // fricative for stop
    SpeakingAssessment a = Assess(sall, "ball");
    SpeakingAssessment self = Assess(AcousticFixtures.Word("ball", 1f), "ball");
    Assert.IsFalse(Passed(a.Decision),
      "SIMULATED sall-for-ball must not pass (got " + a.Decision + ", " + a.Evidence + ")");
    Assert.Less(a.OverallScore, self.OverallScore, "substitution scores below self");
  }

  [Test] public void P13M4_D_FineSubstitutionOrderedDocumented() {
    // Resolution floor (honest): R<->W glides and B<->G stops share manner
    // templates, so the engine orders them below self but may still pass.
    // Fine place-of-articulation inside one manner = NOT PROVEN (V2: per-ID
    // vowel/prototype refinement). Coarse manner/voicing IS resolved (D above).
    SpeakingAssessment wed = Assess(AcousticFixtures.SubOnset("red", "W"), "red");
    SpeakingAssessment redSelf = Assess(AcousticFixtures.Word("red", 1f), "red");
    Assert.LessOrEqual(wed.OverallScore, redSelf.OverallScore, "wed orders at-or-below red");
    SpeakingAssessment gall = Assess(AcousticFixtures.SubOnset("ball", "G"), "ball");
    SpeakingAssessment ballSelf = Assess(AcousticFixtures.Word("ball", 1f), "ball");
    Assert.LessOrEqual(gall.OverallScore, ballSelf.OverallScore, "gall orders at-or-below ball");
    SpeakingAssessment pall = Assess(AcousticFixtures.SubOnset("ball", "P"), "ball");
    Assert.IsFalse(pall.Decision == SpeakingDecision.StrongPass, "pall never Strong");
    Assert.Less(pall.OverallScore, ballSelf.OverallScore, "voicing swap (P/B) orders below self");
  }

  // ---------- E. wrong words ----------
  [Test] public void P13M4_E_CrossSyllableWrongWords() {
    string[][] pairs = {
      new[] { "teddy", "ball" }, new[] { "ball", "apple" },
      new[] { "ball", "teddy" }, new[] { "apple", "teddy" }
    };
    foreach (string[] p in pairs) {
      SpeakingAssessment a = Assess(AcousticFixtures.Word(p[0], 1f), p[1]);
      Assert.AreEqual(SpeakingDecision.WrongWord, a.Decision,
        "SIMULATED " + p[0] + "-for-" + p[1] + " must be WrongWord (got " + a.Decision + ", " + a.Evidence + ")");
    }
  }

  [Test] public void P13M4_E_SameSyllableNeverPasses() {
    // Minimal pairs with one shared manner template: at minimum never pass.
    // (one-as-ball is the known hardest: same syllable + sonorant-heavy both.)
    string[][] pairs = {
      new[] { "one", "ball" }, new[] { "ball", "one" },
      new[] { "red", "ball" }, new[] { "ball", "red" },
      new[] { "red", "one" }, new[] { "one", "red" },
      new[] { "please", "ball" }, new[] { "please", "red" },
      new[] { "red", "please" }, new[] { "one", "please" }
    };
    foreach (string[] p in pairs) {
      SpeakingAssessment a = Assess(AcousticFixtures.Word(p[0], 1f), p[1]);
      Assert.IsFalse(Passed(a.Decision),
        "SIMULATED " + p[0] + "-for-" + p[1] + " must never pass (got " + a.Decision + ", " + a.Evidence + ")");
    }
  }

  // ---------- F. repetition ----------
  [Test] public void P13M4_F_SelfCorrectionHonored() {
    float[] bababall = AcousticFixtures.Concat(
      AcousticFixtures.Truncated("ball", 2), AcousticFixtures.Word("ball", 1f));
    SpeakingAssessment a = Assess(bababall, "ball");
    Assert.IsTrue(a.Decision == SpeakingDecision.Pass || a.Decision == SpeakingDecision.Partial,
      "SIMULATED ba-ba-ball: attempt honored, never failed (got " + a.Decision + ")");
  }

  // ---------- G. quiet ----------
  [Test] public void P13M4_G_QuietMatchesLoud() {
    foreach (string w in new[] { "ball", "apple" }) {
      float[] loud = AcousticFixtures.Word(w, 1f);
      SpeakingAssessment a = Assess(loud, w);
      SpeakingAssessment q = Assess(AcousticFixtures.Quiet(loud, 0.12f), w);
      Assert.AreEqual(a.Decision, q.Decision,
        "SIMULATED quiet " + w + " decides like loud (" + a.Decision + " vs " + q.Decision + ")");
    }
  }

  // ---------- H. silence ----------
  [Test] public void P13M4_H_SilenceIsNoSpeech() {
    SpeakingAssessment a = Assess(AcousticFixtures.Silence(1f), "ball");
    Assert.AreEqual(SpeakingDecision.NoSpeech, a.Decision);
    Assert.IsFalse(a.AttemptDetected);
  }

  // ---------- I. noise ----------
  [Test] public void P13M4_I_NoiseNeverPassesWordSurvives() {
    SpeakingAssessment n = Assess(AcousticFixtures.NoiseOnly(1f, 0.05f, 4242), "ball");
    Assert.IsFalse(Passed(n.Decision),
      "SIMULATED noise-only must never pass (got " + n.Decision + ")");
    float[] ball = AcousticFixtures.Word("ball", 1f);
    SpeakingAssessment w = Assess(AcousticFixtures.Noisy(ball, 12f, 4243), "ball");
    Assert.IsTrue(w.Decision == SpeakingDecision.Pass || w.Decision == SpeakingDecision.Partial
      || w.Decision == SpeakingDecision.StrongPass,
      "SIMULATED ball@12dB honored (got " + w.Decision + ", " + w.Evidence + ")");
  }

  // ---------- J. very short ----------
  [Test] public void P13M4_J_BlipIsNotEvidence() {
    SpeakingAssessment a = Assess(AcousticFixtures.Blip(), "ball");
    Assert.IsFalse(Passed(a.Decision), "30 ms blip never passes");
    Assert.IsTrue(a.Decision == SpeakingDecision.PossibleAttempt
      || a.Decision == SpeakingDecision.NoSpeech
      || a.Decision == SpeakingDecision.TooWeak
      || a.Decision == SpeakingDecision.Unclear,
      "blip is non-evidence (got " + a.Decision + ")");
  }

  // ---------- K. rate ----------
  [Test] public void P13M4_K_SlowFastHonored() {
    foreach (string w in new[] { "ball", "apple", "red" }) {
      SpeakingAssessment slow = Assess(AcousticFixtures.Word(w, 1.4f), w);
      Assert.IsTrue(Passed(slow.Decision),
        "SIMULATED slow " + w + " passes — slow-correct is never punished (got " + slow.Decision + ", " + slow.Evidence + ")");
      SpeakingAssessment fast = Assess(AcousticFixtures.Word(w, 0.7f), w);
      Assert.IsTrue(Passed(fast.Decision) || fast.Decision == SpeakingDecision.Partial,
        "SIMULATED fast " + w + " honored (got " + fast.Decision + ", " + fast.Evidence + ")");
    }
  }

  // ---------- L. pitch/tone robustness (F0 discarded by construction) ----------
  [Test] public void P13M4_L_PitchHeightAndContourIgnored() {
    SpeakingAssessment hi = Assess(AcousticFixtures.WordHigh("ball"), "ball");
    Assert.IsTrue(Passed(hi.Decision), "high-pitched ball passes (got " + hi.Decision + ", " + hi.Evidence + ")");
    SpeakingAssessment glide = Assess(AcousticFixtures.WordGlide("ball"), "ball");
    Assert.IsTrue(Passed(glide.Decision),
      "tone-like gliding ball passes — tonal-L1 kids are not punished for F0 (got " + glide.Decision + ", " + glide.Evidence + ")");
  }

  // ---------- M. variant tolerance ----------
  [Test] public void P13M4_M_SlowVariantsPassAllSix() {
    foreach (string w in Words) {
      SpeakingAssessment a = Assess(AcousticFixtures.Word(w, 1.3f), w);
      Assert.IsTrue(Passed(a.Decision),
        "SIMULATED slow variant " + w + " passes (got " + a.Decision + ", " + a.Evidence + ")");
    }
  }

  // ---------- N. interruption ----------
  [Test] public void P13M4_N_HalfCaptureNeverPassesOrCrashes() {
    SpeakingAssessment a = Assess(
      AcousticFixtures.FirstHalf(AcousticFixtures.Word("ball", 1f)), "ball");
    Assert.IsFalse(Passed(a.Decision), "half-captured ball never passes");
    Assert.IsTrue(a.AttemptDetected, "interruption is still an attempt, never silence");
  }

  // ---------- O. no microphone ----------
  [Test] public void P13M4_O_NoMicDefersWithAcousticStack() {
    var recognizer = new SpeechRecognizer(new NoDeviceMic(),
      new FakeSpeechCapture(() => Seg(AcousticFixtures.Word("ball", 1f))),
      new LocalAcousticProvider(SixWords()));
    var runner = new SpeakingExerciseRunner(recognizer, null, new GameEventBus());
    SpeakingExerciseResult r = runner.RunAsync(
      SpeakingExerciseConfig.DefaultFor(new WordId("ball")), CancellationToken.None)
      .GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.SkippedNoMic, r.Outcome);
    Assert.AreEqual(0, r.AttemptsUsed);
  }

  // ---------- P. timeout ----------
  [Test] public void P13M4_P_SlowProviderTimesOutAsEnvironment() {
    var recognizer = new SpeechRecognizer(new CT_P13_M3_ProviderPolicy_ReadyMic(),
      new FakeSpeechCapture(() => Seg(AcousticFixtures.Word("ball", 1f))),
      new DelayedProvider(), SpeakingPassPolicy.Default(), null, 8f, 3f, 0.3f);
    SpeakingAssessment a = recognizer.StartAttemptAsync(
      new WordId("ball"), CancellationToken.None).GetAwaiter().GetResult();
    Assert.IsTrue(a.IsEnvironmentError);
    Assert.AreEqual(SpeechFailureReasons.Timeout, a.FailureReason);
  }

  // ---------- performance ----------
  [Test] public void P13M4_Perf_EightSecondBurst() {
    float[] s = AcousticFixtures.Concat(
      AcousticFixtures.Word("teddy", 1f), AcousticFixtures.Word("apple", 1f),
      AcousticFixtures.Word("please", 1f), AcousticFixtures.Word("ball", 1f),
      AcousticFixtures.Word("red", 1f), AcousticFixtures.Word("one", 1f));
    Assert.GreaterOrEqual((float)s.Length / 16000, 2.5f, "fixture sanity: multi-word concat");
    var sw = System.Diagnostics.Stopwatch.StartNew();
    AcousticEvidence ev = AcousticAnalysis.Analyze(s, 16000, AcousticFixtures.Pron("teddy"));
    sw.Stop();
    Assert.IsTrue(ev.HasAcousticData);
    Assert.Less(sw.ElapsedMilliseconds, 3000,
      "burst budget: multi-second analysis < 3 s (took " + sw.ElapsedMilliseconds + " ms)");
  }

  [Test] public void P13M4_Perf_DeterministicNoRetention() {
    float[] s = AcousticFixtures.Word("apple", 1f);
    TargetPronunciation p = AcousticFixtures.Pron("apple");
    AcousticEvidence a = AcousticAnalysis.Analyze(s, 16000, p);
    AcousticEvidence b = AcousticAnalysis.Analyze(s, 16000, p);
    Assert.AreEqual(a.OverallMatch, b.OverallMatch, "pure function: identical inputs, identical outputs");
    Assert.AreEqual(a.PerPhoneme.Length, b.PerPhoneme.Length);
    for (int i = 0; i < a.PerPhoneme.Length; i++) {
      Assert.AreEqual(a.PerPhoneme[i].MatchScore, b.PerPhoneme[i].MatchScore);
      Assert.AreEqual(a.PerPhoneme[i].Missing, b.PerPhoneme[i].Missing);
    }
  }

  [Test] public void P13M4_Perf_ProviderLatencyTwoSeconds() {
    float[] s = AcousticFixtures.Concat(
      AcousticFixtures.Word("apple", 1f), AcousticFixtures.Word("ball", 1f));
    var provider = new LocalAcousticProvider(SixWords());
    var sw = System.Diagnostics.Stopwatch.StartNew();
    SpeechRecognitionResult r = provider.RecognizeAsync(
      Seg(s), new WordId("apple"), CancellationToken.None).GetAwaiter().GetResult();
    sw.Stop();
    Assert.IsFalse(r.IsError);
    Assert.Less(sw.ElapsedMilliseconds, 1000,
      "provider burst < 1 s for ~2 s audio (took " + sw.ElapsedMilliseconds + " ms)");
  }

  // ---------- matrix instrument (always passes; prints the 6x6 table) ----------

  [Test] public void P13M4_MatrixInstrument() {
    var sb = new System.Text.StringBuilder();
    sb.Append("\nM4 confusion matrix (SIMULATED rows=audio cols=target: decision/match/margin):\n");
    string[] words = { "ball", "apple", "red", "one", "please", "teddy" };
    foreach (string audio in words) {
      float[] pcm = AcousticFixtures.Word(audio, 1f);
      var matches = new Dictionary<string, float>();
      var decisions = new Dictionary<string, SpeakingDecision>();
      foreach (string target in words) {
        SpeakingAssessment a = Assess(pcm, target);
        matches[target] = a.OverallScore;
        decisions[target] = a.Decision;
      }
      foreach (string target in words) {
        float bestOther = -1f;
        foreach (string other in words) {
          if (other != target && matches[other] > bestOther) bestOther = matches[other];
        }
        float margin = matches[target] - bestOther;
        sb.Append(audio).Append("->").Append(target).Append("=")
          .Append(decisions[target]).Append("/").Append(matches[target].ToString("0.0"))
          .Append("/m").Append(margin >= 0 ? "+" : "").Append(margin.ToString("0.00")).Append("  ");
      }
      sb.Append("\n");
    }
    Assert.Pass("instrument: full 6x6 matrix exercised without asserting (see M4_E_* for pins):"
      + sb.ToString());
  }

  // ---------- real samples (curated import pipeline; absent = Ignore) ----------
  [Test] public void P13M4_RealSamplesIfPresent() {
    string dir = Path.Combine(ContentRootM4(), "speech_samples");
    string manifest = Path.Combine(dir, "manifest.json");
    if (!Directory.Exists(dir) || !File.Exists(manifest))
      Assert.Ignore("no curated real samples — child-human validity NOT PROVEN (see tools/import_speech_sample.py)");
    Assert.Fail("manifest present but runner not implemented in M4 (wire when first sample lands)");
  }

  static string ContentRootM4() {
    string assets = Application.dataPath;
    return Path.Combine(Directory.GetParent(assets).FullName, "Content");
  }
}

// CT-P13 M5: staged speaking beat on the REAL production path with REAL content
// files (no scripted pronunciations, no fake provider): Content/vocab/*.json
// -> TargetPronunciationCatalog -> LocalAcousticProvider -> SpeechRecognizer
// (capture seam) -> SpeakingExerciseRunner -> WordSpokenEvent -> frozen
// QuestManager.AdvanceOnSpoken (+ Learning). SIMULATED audio (fixtures), REAL
// everything else — the runtime Bootstrap wiring (mic + scene + NPC prompt
// visuals) awaits stager-era content loading, like CatalogQuestProvider (2F).
public class CT_P13_M5_StagedBeat {
  static string ContentRoot() {
    string assets = Application.dataPath;
    return Path.Combine(Directory.GetParent(assets).FullName, "Content");
  }

  static TargetPronunciationCatalog RealCatalog() {
    string[] files = Directory.GetFiles(Path.Combine(ContentRoot(), "vocab"), "*.json");
    Assert.GreaterOrEqual(files.Length, 51, "real Content bundle present");
    var entries = new List<VocabEntry>();
    foreach (string path in files)
      entries.Add(ContentDatabase.ParseVocab(File.ReadAllText(path)));
    var catalog = new TargetPronunciationCatalog(entries);
    Assert.AreEqual(6, catalog.Count(), "benchmark pronunciation coverage (ball/apple/red/one/please/teddy)");
    return catalog;
  }

  sealed class ReadyMic : IMicrophoneDevice {
    public event Action<MicStatus> StatusChanged;
    public MicStatus Status => MicStatus.Ready;
    public string SelectedDevice => "test-mic";
    public string[] Devices => new[] { "test-mic" };
    public SpeechCapability Capability => new SpeechCapability { Status = MicStatus.Ready, DeviceName = "test-mic", DeviceCount = 1 };
    public void Refresh() { }
    public void ReportCaptureFailure() { }
  }

  static CapturedSpeech Seg(float[] pcm) {
    float mean = VoiceActivity.MeanAbsolute(pcm);
    float dur = (float)pcm.Length / 16000;
    return new CapturedSpeech {
      Samples = pcm, SampleRate = 16000, Channels = 1,
      DurationSec = dur, MeanEnergy = mean, PeakEnergy = mean * 2f,
      VoicedSec = dur * 0.7f, TimedOut = false, Cancelled = false, Error = string.Empty
    };
  }

  [Test] public void P13M5_RealCatalogDrivesProvider() {
    var provider = new LocalAcousticProvider(RealCatalog());
    SpeechRecognitionResult r = provider.RecognizeAsync(
      Seg(AcousticFixtures.Word("apple", 1f)), new WordId("apple"), CancellationToken.None)
      .GetAwaiter().GetResult();
    Assert.IsFalse(r.IsError);
    Assert.IsTrue(r.Pronunciation.HasAcousticData, "real content feeds real acoustic evidence");
  }

  [Test] public void P13M5_AcousticAppleCompletesFrozenSpeakLeg() {
    // market_help_mia: find->bring (driven directly, as in P12G) then SPEAK via
    // the acoustic stack. Proves the staged beat: assessment -> WordSpokenEvent
    // -> frozen AdvanceOnSpoken (Perfect/Great gate untouched).
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints);
    bus.Subscribe<WordSpokenEvent>(e => quests.AdvanceOnSpoken(e.WordId, e.Result.Level));
    var q = new QuestId("market_help_mia");
    quests.StartQuest(q);
    quests.AdvanceOnSeen(new WordId("apple"));
    quests.ReportAction(PlayerAction.Bring, new WordId("apple"));
    Assert.AreEqual(2, quests.GetState(q).ObjectiveIndex, "speak leg reached");

    var recognizer = new SpeechRecognizer(new ReadyMic(),
      new FakeSpeechCapture(() => Seg(AcousticFixtures.Word("apple", 1f))),
      new LocalAcousticProvider(RealCatalog()));
    var runner = new SpeakingExerciseRunner(recognizer, null, bus);
    SpeakingExerciseResult result = runner.RunAsync(
      SpeakingExerciseConfig.DefaultFor(new WordId("apple")), CancellationToken.None)
      .GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.Passed, result.Outcome,
      "SIMULATED clear apple passes the staged beat (" + result.BestAssessment.Evidence + ")");
    Assert.IsTrue(quests.GetState(q).Completed, "frozen Speak gate advances on acoustic Pass");
  }

  [Test] public void P13M5_WrongWordKeepsQuestOpen() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints);
    bus.Subscribe<WordSpokenEvent>(e => quests.AdvanceOnSpoken(e.WordId, e.Result.Level));
    var q = new QuestId("market_help_mia");
    quests.StartQuest(q);
    quests.AdvanceOnSeen(new WordId("apple"));
    quests.ReportAction(PlayerAction.Bring, new WordId("apple"));

    var recognizer = new SpeechRecognizer(new ReadyMic(),
      new FakeSpeechCapture(() => Seg(AcousticFixtures.Word("ball", 1f))),
      new LocalAcousticProvider(RealCatalog()));
    var runner = new SpeakingExerciseRunner(recognizer, null, bus);
    var config = SpeakingExerciseConfig.DefaultFor(new WordId("apple"));
    config.attemptsAllowed = 1;
    SpeakingExerciseResult result = runner.RunAsync(config, CancellationToken.None)
      .GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.NotPassed, result.Outcome);
    Assert.IsFalse(quests.GetState(q).Completed, "wrong word never advances the frozen gate");
    Assert.AreEqual(2, quests.GetState(q).ObjectiveIndex, "quest stays OPEN at the speak leg");
  }

  [Test] public void P13M5_PartialKeepsQuestOpenForRetry() {
    var bus = new GameEventBus();
    var learning = new LearningService(bus);
    var hints = new HintService(bus);
    var quests = new QuestManager(bus, learning, hints);
    bus.Subscribe<WordSpokenEvent>(e => quests.AdvanceOnSpoken(e.WordId, e.Result.Level));
    var q = new QuestId("market_help_mia");
    quests.StartQuest(q);
    quests.AdvanceOnSeen(new WordId("apple"));
    quests.ReportAction(PlayerAction.Bring, new WordId("apple"));

    var appl = AcousticFixtures.RenderWord(
      new List<string> { "AE", "P", "AH" }, 1f); // "appl": coda-deleted apple
    var recognizer = new SpeechRecognizer(new ReadyMic(),
      new FakeSpeechCapture(() => Seg(appl)),
      new LocalAcousticProvider(RealCatalog()));
    var runner = new SpeakingExerciseRunner(recognizer, null, bus);
    var config = SpeakingExerciseConfig.DefaultFor(new WordId("apple"));
    config.attemptsAllowed = 1;
    SpeakingExerciseResult result = runner.RunAsync(config, CancellationToken.None)
      .GetAwaiter().GetResult();
    Assert.AreEqual(SpeakingExerciseOutcome.PartialComplete, result.Outcome,
      "SIMULATED coda-deleted apple encourages retry, never fails");
    Assert.IsFalse(quests.GetState(q).Completed);
    StringAssert.Contains("MISSING_ENDING", result.BestAssessment.Evidence);
  }
}

// M4 needs a Ready mic too (M3's is private-nested).
public sealed class CT_P13_M3_ProviderPolicy_ReadyMic : IMicrophoneDevice {
  public event Action<MicStatus> StatusChanged;
  public MicStatus Status => MicStatus.Ready;
  public string SelectedDevice => "test-mic";
  public string[] Devices => new[] { "test-mic" };
  public SpeechCapability Capability => new SpeechCapability { Status = MicStatus.Ready, DeviceName = "test-mic", DeviceCount = 1 };
  public void Refresh() { }
  public void ReportCaptureFailure() { }
}
