// _SharedKernel/ReferenceSynthesizer.cs — Phase 2.1-local canonical reference
// renderer (Lead owns). Pure C# (NO UnityEngine), deterministic (fixed seed).
//
// WHY SYNTHETIC REFERENCES (literature: template-matching ASR, Hair et al.):
// the engine compares the child's attempt against a REFERENCE utterance of the
// target via DTW — exemplar tracks beat parametric manner prototypes (real
// timing, real coarticulation sketch, symmetric normalization). We have no
// recorded reference speakers (privacy + no data yet), so the reference is
// synthesized formant audio. HONEST LABELS:
// - The synth is a coarse acoustic cousin (NOT a speaker/voice model): harmonic
//   vowels by F1/F2, noise frication, closure+transient stops. ±30% tolerance.
// - Canonical params (f0 220, seed 999) differ from test child variants (f0
//   260–340, seeds) BY DESIGN, so SIMULATED benchmarks carry real mismatch.
// - Test fixtures are an INTENTIONALLY FORKED renderer (independent artifacts),
//   not calls into this file — shared code would correlate systematics and
//   inflate self-scores. Duplication here is honesty, not debt.
// - Upgrading references (TTS-decoded tracks, recorded speakers) is a
//   provider-level swap: engine + tests keep working (they consume tracks).
// No audio retained: renders are transient per-attempt bursts (<100 ms audio).
using System;
using System.Collections.Generic;

public static class ReferenceSynthesizer {
  public const int SampleRate = 16000;
  // Child-centered reference pitch (target users are 4-year-olds, F0 250–400):
  // smaller harmonic mismatch to child attempts than an adult 130–220 Hz
  // reference would give (M4: F0 340 read Partial against a 220 reference —
  // band/centroid features track harmonics). Adult testers sit farther off;
  // they degrade gracefully (Partial, never fail). Formant (vocal-tract)
  // differences are NOT normalized — child-human validation stays NOT PROVEN.
  public const float CanonicalF0 = 300f;
  public const uint CanonicalSeed = 999;

  static uint _rng = CanonicalSeed;
  static float Rand() {
    _rng = _rng * 1664525u + 1013904223u;
    return (float)(_rng >> 8) / 16777216f * 2f - 1f;
  }

  struct Formants { public float F1, F2; }

  // Coarse typical formants (order-of-magnitude anchors, NOT precision claims).
  static Formants FormantsFor(string id) {
    switch (id) {
      case "AO": return new Formants { F1 = 500f, F2 = 900f };
      case "AE": return new Formants { F1 = 700f, F2 = 1800f };
      case "AH": return new Formants { F1 = 700f, F2 = 1200f };
      case "EH": return new Formants { F1 = 550f, F2 = 1800f };
      case "IY": return new Formants { F1 = 300f, F2 = 2200f };
      case "AA": return new Formants { F1 = 730f, F2 = 1090f };
      case "AW": return new Formants { F1 = 650f, F2 = 1200f };
      case "AY": return new Formants { F1 = 600f, F2 = 1800f };
      case "ER": return new Formants { F1 = 490f, F2 = 1350f };
      case "EY": return new Formants { F1 = 570f, F2 = 1970f };
      case "IH": return new Formants { F1 = 390f, F2 = 1990f };
      case "OW": return new Formants { F1 = 570f, F2 = 850f };
      case "OY": return new Formants { F1 = 500f, F2 = 1500f };
      case "UH": return new Formants { F1 = 440f, F2 = 1020f };
      case "UW": return new Formants { F1 = 300f, F2 = 870f };
      case "L": return new Formants { F1 = 400f, F2 = 1200f };
      case "R": return new Formants { F1 = 350f, F2 = 1300f };
      case "W": return new Formants { F1 = 350f, F2 = 800f };
      case "Y": return new Formants { F1 = 300f, F2 = 2000f };
      case "M":
      case "N":
      case "NG": return new Formants { F1 = 250f, F2 = 900f };
      default: return new Formants { F1 = 600f, F2 = 1400f };
    }
  }

  static float BaseAmp(string id) {
    switch (id) {
      case "W":
      case "Y": return 0.36f;
      case "L":
      case "R": return 0.38f;
      case "M":
      case "N":
      case "NG": return 0.34f;
      default: return 0.50f; // vowels
    }
  }

  static float BaseDur(string id) {
    switch (id) {
      case "W":
      case "Y": return 0.06f;
      default: return 0.12f; // vowels/sonorants (stops/fricatives handled below)
    }
  }

  static bool IsStop(string id) {
    return id == "B" || id == "D" || id == "G" || id == "K"
      || id == "P" || id == "T" || id == "CH" || id == "JH";
  }

  static bool IsVoicedStop(string id) {
    return id == "B" || id == "D" || id == "G" || id == "JH";
  }

  static bool IsFricative(string id) {
    return id == "S" || id == "F" || id == "TH" || id == "SH" || id == "HH"
      || id == "Z" || id == "V" || id == "DH" || id == "ZH" || id == "CH" || id == "JH";
  }

  static bool IsVoicedFricative(string id) {
    return id == "Z" || id == "V" || id == "DH" || id == "ZH" || id == "JH";
  }

  static float[] RenderVowel(Formants f, float durSec, float amp, float f0) {
    int n = Math.Max(8, (int)(durSec * SampleRate));
    var s = new float[n];
    for (int i = 0; i < n; i++) {
      float t = (float)i / SampleRate;
      float env = Math.Min(1f, i / (0.015f * SampleRate)) * Math.Min(1f, (n - i) / (0.025f * SampleRate));
      double v = 0.6 * Math.Sin(2 * Math.PI * f0 * t)
        + 0.3 * Math.Sin(2 * Math.PI * 2 * f0 * t)
        + 1.0 * Math.Sin(2 * Math.PI * f.F1 * t)
        + 0.9 * Math.Sin(2 * Math.PI * f.F2 * t)
        + 0.15 * Math.Sin(2 * Math.PI * 3 * f0 * t);
      s[i] = amp * env * (float)(v / 2.35);
    }
    return s;
  }

  static float[] RenderStop(string id) {
    int closure = (int)(0.030f * SampleRate);
    int tail = (int)(0.025f * SampleRate);
    var s = new float[closure + tail];
    for (int i = 0; i < tail; i++) {
      float t = (float)i / SampleRate;
      float env = 1f - (float)i / tail;
      float v = Rand() * 0.6f * env;
      if (IsVoicedStop(id)) v += 0.8f * (float)Math.Sin(2 * Math.PI * 130f * t);
      s[closure + i] = 0.5f * v * 0.55f;
    }
    return s;
  }

  static float[] RenderFricative(string id) {
    float bright = IsVoicedFricative(id) ? 0.6f : 0.8f;
    float voice = IsVoicedFricative(id) ? 0.5f : 0f;
    int n = (int)(0.12f * SampleRate);
    var s = new float[n];
    float prev = 0f;
    for (int i = 0; i < n; i++) {
      float t = (float)i / SampleRate;
      float w = Rand();
      float hp = w - prev * (1f - bright);
      prev = w;
      float v = hp * 0.7f + voice * (float)Math.Sin(2 * Math.PI * 150f * t);
      float env = Math.Min(1f, i / (0.01f * SampleRate)) * Math.Min(1f, (n - i) / (0.02f * SampleRate));
      s[i] = 0.42f * env * v;
    }
    return s;
  }

  static float[] RenderOne(string id) {
    if (IsStop(id)) return RenderStop(id);
    if (IsFricative(id)) return RenderFricative(id);
    return RenderVowel(FormantsFor(id), BaseDur(id), BaseAmp(id), CanonicalF0);
  }

  // Canonical reference PCM + per-phoneme SAMPLE spans (start inclusive, end
  // exclusive). Deterministic: same ids -> same bytes, every run, every machine.
  public static float[] Render(IList<string> phonemeIds, out int[] phoneSampleStart, out int[] phoneSampleEnd) {
    _rng = CanonicalSeed;
    phoneSampleStart = new int[0];
    phoneSampleEnd = new int[0];
    if (phonemeIds == null || phonemeIds.Count == 0) return new float[0];
    var parts = new List<float[]>();
    phoneSampleStart = new int[phonemeIds.Count];
    phoneSampleEnd = new int[phonemeIds.Count];
    int total = 0;
    for (int i = 0; i < phonemeIds.Count; i++) {
      float[] p = RenderOne(phonemeIds[i]);
      parts.Add(p);
      phoneSampleStart[i] = total;
      total += p.Length;
      phoneSampleEnd[i] = total;
    }
    // 15 ms crossfade joins (deliberately NOT the fixture 25 ms: independent artifacts).
    int fade = SampleRate * 15 / 1000;
    var s = new float[total];
    int at = 0;
    for (int i = 0; i < parts.Count; i++) {
      float[] p = parts[i];
      for (int k = 0; k < p.Length; k++) {
        float w = 1f;
        if (i > 0 && k < fade) w = 0.5f + 0.5f * (float)k / fade;
        s[at + k] += p[k] * w;
      }
      phoneSampleStart[i] = at;
      at += p.Length;
      phoneSampleEnd[i] = at;
    }
    return s;
  }

  public static float[] RenderWord(WordId word, IList<string> phonemeIds) {
    int[] a, b;
    return Render(phonemeIds, out a, out b);
  }
}
