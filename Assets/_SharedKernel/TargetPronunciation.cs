// _SharedKernel/TargetPronunciation.cs — Phase 2.1-local target pronunciation contract (Lead owns).
// Pure C# (NO UnityEngine): EditMode-testable without a device.
//
// TARGET-CONSTRAINED PRINCIPLE (§4): the game ALWAYS knows the target word.
// The engine never solves open vocabulary ("what is the child saying?") —
// it solves the closed question ("is this segment an attempt at BALL, and how
// are BALL's expected sounds realized?"). Content owns the pronunciation
// (vocab JSON speech.phonemes, ARPAbet ids); the engine consumes it through
// ITargetPronunciationProvider. NO word literals live in the engine: the
// phoneme table below is keyed by PHONEME id (acoustic properties), never by
// word. Adding a word = content data + zero engine changes.
//
// ARPAbet scope: full US-English inventory is tabled so future words need no
// code change; only the 6 benchmark words (ball/apple/red/one/please/teddy)
// carry content data in V1. Duration weights are coarse heuristics (stops
// short, vowels long) for proportional segment splits — DTW absorbs absolute
// rate, so these never need fake precision.
using System;
using System.Collections.Generic;

public enum PhonemeManner {
  Stop,      // B D G K P T (burst + gap; short)
  Fricative, // F S SH Z TH DH V HH (noise; high ZCR)
  Nasal,     // M N NG (voiced murmur; low + damped)
  Liquid,    // L R (voiced continuant; vowel-like + tilt)
  Glide,     // W Y (vowel-like transition; short)
  Vowel      // AA AE AH AO AW AY EH ER EY IH IY OW OY UH UW (nucleus; high energy)
}

[Serializable]
public struct TargetPhoneme {
  public string Id;          // "B", "AO", ...
  public PhonemeManner Manner;
  public bool Voiced;
  public float DurationWeight; // relative only; DTW absorbs absolute rate

  public bool IsVowel() { return Manner == PhonemeManner.Vowel; }
  public bool IsSonorant() {
    return Manner == PhonemeManner.Vowel || Manner == PhonemeManner.Nasal
      || Manner == PhonemeManner.Liquid || Manner == PhonemeManner.Glide;
  }
}

[Serializable]
public sealed class TargetPronunciation {
  public WordId Word;
  public TargetPhoneme[] Phonemes = new TargetPhoneme[0];
  public int SyllableCount;  // vowel-count heuristic (apple/teddy = 2, rest of benchmark = 1)
  public float TotalWeight;

  public int Length() { return Phonemes != null ? Phonemes.Length : 0; }
  public bool IsUsable() { return Phonemes != null && Phonemes.Length > 0; }

  public TargetPhoneme First() { return Phonemes[0]; }
  public TargetPhoneme Last() { return Phonemes[Phonemes.Length - 1]; }

  // Manner RUNS: consecutive same-manner phonemes form one acoustic regime
  // (ball B/AO/L = 3 runs; a geminate-free inventory keeps this exact).
  // The engine compares it against observed spectral regimes for deletion
  // detection — generic over any phoneme list, no word branches.
  public int MannerRuns() {
    if (Phonemes == null || Phonemes.Length == 0) return 0;
    int runs = 1;
    for (int i = 1; i < Phonemes.Length; i++)
      if (Phonemes[i].Manner != Phonemes[i - 1].Manner) runs++;
    return runs;
  }

  // Proportional frame budget for phoneme i given total voiced frames N.
  public int FrameBudget(int index, int totalFrames) {
    if (TotalWeight <= 0f || totalFrames <= 0) return 0;
    float w = Phonemes[index].DurationWeight;
    return Math.Max(1, (int)Math.Round(w / TotalWeight * totalFrames));
  }
}

// Content -> engine seam. Implementations: catalog over VocabEntry (production),
// scripted dictionary (tests). The ENGINE never reads JSON or Resources.
public interface ITargetPronunciationProvider {
  bool TryGet(WordId word, out TargetPronunciation pronunciation);
}

public static class PhonemeTable {
  struct Row { public string Id; public PhonemeManner Manner; public bool Voiced; public float W; }

  static readonly Row[] Rows = new Row[] {
    // vowels (long weights coarse: diphthongs/long monophthongs slightly longer)
    new Row { Id = "AA", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.2f },
    new Row { Id = "AE", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.2f },
    new Row { Id = "AH", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.0f },
    new Row { Id = "AO", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.3f },
    new Row { Id = "AW", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.4f },
    new Row { Id = "AY", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.4f },
    new Row { Id = "EH", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.1f },
    new Row { Id = "ER", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.2f },
    new Row { Id = "EY", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.4f },
    new Row { Id = "IH", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.0f },
    new Row { Id = "IY", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.2f },
    new Row { Id = "OW", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.4f },
    new Row { Id = "OY", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.4f },
    new Row { Id = "UH", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.0f },
    new Row { Id = "UW", Manner = PhonemeManner.Vowel, Voiced = true, W = 1.2f },
    // stops
    new Row { Id = "B", Manner = PhonemeManner.Stop, Voiced = true, W = 0.6f },
    new Row { Id = "D", Manner = PhonemeManner.Stop, Voiced = true, W = 0.6f },
    new Row { Id = "G", Manner = PhonemeManner.Stop, Voiced = true, W = 0.6f },
    new Row { Id = "K", Manner = PhonemeManner.Stop, Voiced = false, W = 0.6f },
    new Row { Id = "P", Manner = PhonemeManner.Stop, Voiced = false, W = 0.6f },
    new Row { Id = "T", Manner = PhonemeManner.Stop, Voiced = false, W = 0.6f },
    // fricatives
    new Row { Id = "F", Manner = PhonemeManner.Fricative, Voiced = false, W = 1.0f },
    new Row { Id = "S", Manner = PhonemeManner.Fricative, Voiced = false, W = 1.0f },
    new Row { Id = "SH", Manner = PhonemeManner.Fricative, Voiced = false, W = 1.0f },
    new Row { Id = "Z", Manner = PhonemeManner.Fricative, Voiced = true, W = 1.0f },
    new Row { Id = "TH", Manner = PhonemeManner.Fricative, Voiced = false, W = 1.0f },
    new Row { Id = "DH", Manner = PhonemeManner.Fricative, Voiced = true, W = 1.0f },
    new Row { Id = "V", Manner = PhonemeManner.Fricative, Voiced = true, W = 1.0f },
    new Row { Id = "HH", Manner = PhonemeManner.Fricative, Voiced = false, W = 0.8f },
    new Row { Id = "CH", Manner = PhonemeManner.Fricative, Voiced = false, W = 0.9f },
    new Row { Id = "JH", Manner = PhonemeManner.Fricative, Voiced = true, W = 0.9f },
    new Row { Id = "ZH", Manner = PhonemeManner.Fricative, Voiced = true, W = 1.0f },
    // nasals
    new Row { Id = "M", Manner = PhonemeManner.Nasal, Voiced = true, W = 1.0f },
    new Row { Id = "N", Manner = PhonemeManner.Nasal, Voiced = true, W = 1.0f },
    new Row { Id = "NG", Manner = PhonemeManner.Nasal, Voiced = true, W = 1.0f },
    // liquids
    new Row { Id = "L", Manner = PhonemeManner.Liquid, Voiced = true, W = 1.0f },
    new Row { Id = "R", Manner = PhonemeManner.Liquid, Voiced = true, W = 1.0f },
    // glides
    new Row { Id = "W", Manner = PhonemeManner.Glide, Voiced = true, W = 0.8f },
    new Row { Id = "Y", Manner = PhonemeManner.Glide, Voiced = true, W = 0.8f },
  };

  static Dictionary<string, TargetPhoneme> _map;
  static Dictionary<string, TargetPhoneme> Map() {
    if (_map != null) return _map;
    _map = new Dictionary<string, TargetPhoneme>(StringComparer.Ordinal);
    for (int i = 0; i < Rows.Length; i++) {
      _map[Rows[i].Id] = new TargetPhoneme {
        Id = Rows[i].Id, Manner = Rows[i].Manner,
        Voiced = Rows[i].Voiced, DurationWeight = Rows[i].W
      };
    }
    return _map;
  }

  public static bool TryResolve(string id, out TargetPhoneme phoneme) {
    phoneme = default(TargetPhoneme);
    if (string.IsNullOrWhiteSpace(id)) return false;
    return Map().TryGetValue(id.Trim().ToUpperInvariant(), out phoneme);
  }

  // Generic factory: ANY word's id list -> pronunciation. Returns null when
  // the list is empty or carries an unknown id (engine then reports acoustic
  // NOT AVAILABLE instead of guessing — honesty over coverage).
  public static TargetPronunciation FromIds(WordId word, IList<string> ids) {
    if (ids == null || ids.Count == 0) return null;
    var phonemes = new List<TargetPhoneme>(ids.Count);
    for (int i = 0; i < ids.Count; i++) {
      TargetPhoneme p;
      if (!TryResolve(ids[i], out p)) return null;
      phonemes.Add(p);
    }
    if (phonemes.Count == 0) return null;
    int vowels = 0;
    float total = 0f;
    for (int i = 0; i < phonemes.Count; i++) {
      if (phonemes[i].IsVowel()) vowels++;
      total += phonemes[i].DurationWeight;
    }
    return new TargetPronunciation {
      Word = word,
      Phonemes = phonemes.ToArray(),
      SyllableCount = Math.Max(1, vowels),
      TotalWeight = total
    };
  }
}
