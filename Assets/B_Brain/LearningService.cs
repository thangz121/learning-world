// B_Brain/LearningService.cs — Agent B (W0-T1). MasteryFSM + Meaningful dedup.
// Plain C# service, deterministic. Constructed ONLY by GameInstaller:
//   new LearningService(IGameEventBus)
// Implements frozen ILearningService plus W0-T1 extras:
//   ReportSelected / ReportContextUse / ForceReviewDue / IsMeaningful / GetMeaningfulCount / GetMastery.
//
// MasteryFSM: Unknown -> Exposed -> Recognized -> Produced -> UsedInContext -> Retained.
//   Exposed:       Exposure >= 3 (ReportSeen, any LearnSource).
//   Recognized:    RecognitionHit >= 4 && RecognitionTotal >= 5 (ReportSelected).
//   Produced:      SpeakingHit >= 2 && SpeakingTotal >= 4, hits are SpeechLevel Great+ (ReportSpoken).
//   UsedInContext: ContextUse >= 1 (ReportContextUse, e.g. CT-010 no-visual transfer).
//   Retained:      DateTime.UtcNow >= NextReview once UsedInContext (ForceReviewDue for tests).
// Forward-only; counters accumulate regardless of stage; promotion is re-checked on
// every report and lazily in GetStage (review passing is time-based).
//
// Rate-limit (CT-009 support): IsMeaningful(word, src) is true only if no identical
// (word, source) report was recorded in the last 5s. Exposure/speech/selection
// counters count every report (anti-stall for CT-001); the deduped
// GetMeaningfulCount metric counts genuine engagements (CT-009, DoD Meaningful).
using System;
using System.Collections.Generic;

public sealed class LearningService : ILearningService {
  // No bus usage in W0-T1 (mastery changes publish no event yet); the ctor keeps
  // the exact GameInstaller signature new LearningService(IGameEventBus) for Tier3.

  const int ExposedAfterSeen = 3;
  const int RecognizedHits = 4;
  const int RecognizedTotal = 5;
  const int ProducedHits = 2;
  const int ProducedTotal = 4;
  const int UsedInContextAfter = 1;
  static readonly TimeSpan DedupWindow = TimeSpan.FromSeconds(5);
  static readonly TimeSpan ReviewDelay = TimeSpan.FromHours(24);

  readonly Dictionary<WordId, WordMastery> _words = new Dictionary<WordId, WordMastery>();
  readonly Dictionary<string, DateTime> _lastMeaningfulUtc = new Dictionary<string, DateTime>();
  readonly Dictionary<WordId, int> _meaningfulCount = new Dictionary<WordId, int>();

  public LearningService(IGameEventBus bus) {
  }

  public void ReportSeen(WordId id, LearnSource src) {
    WordMastery w = GetOrCreate(id);
    w.Exposure++;
    BumpMeaningful(id, src);
    Promote(w);
  }

  public void ReportSpoken(WordId id, SpeechLevel level, LearnSource src) {
    WordMastery w = GetOrCreate(id);
    w.SpeakingTotal++;
    if (level == SpeechLevel.Perfect || level == SpeechLevel.Great) w.SpeakingHit++;
    BumpMeaningful(id, src);
    Promote(w);
  }

  // Extra (W0-T1): selection hit/miss on the recognition path (e.g. tap-the-right-card).
  public void ReportSelected(WordId id, bool hit, LearnSource src) {
    WordMastery w = GetOrCreate(id);
    w.RecognitionTotal++;
    if (hit) w.RecognitionHit++;
    BumpMeaningful(id, src);
    Promote(w);
  }

  // Extra (W0-T1): word used in a new context without visual support (transfer, CT-010).
  public void ReportContextUse(WordId id) {
    WordMastery w = GetOrCreate(id);
    w.ContextUse++;
    Promote(w);
  }

  // Extra (tests): move NextReview into the past so the next GetStage observes Retained.
  public void ForceReviewDue(WordId id) {
    GetOrCreate(id).NextReview = DateTime.UtcNow - TimeSpan.FromSeconds(1);
  }

  public MasteryStage GetStage(WordId id) {
    WordMastery w;
    if (!_words.TryGetValue(id, out w)) return MasteryStage.Unknown;
    Promote(w); // lazy: review passing is time-based, observed on read.
    return w.Stage;
  }

  // CT-009 support. True once per (word, source) per 5s window; checking records
  // the window (standard rate-limiter semantics).
  public bool IsMeaningful(WordId id, LearnSource src) {
    string key = id.Value + "|" + src.ToString();
    DateTime now = DateTime.UtcNow;
    DateTime last;
    if (_lastMeaningfulUtc.TryGetValue(key, out last) && (now - last) < DedupWindow)
      return false;
    _lastMeaningfulUtc[key] = now;
    return true;
  }

  // Deduped genuine-engagement counter per word (CT-009 Meaningful metric).
  public int GetMeaningfulCount(WordId id) {
    int n;
    if (_meaningfulCount.TryGetValue(id, out n)) return n;
    return 0;
  }

  // Defensive snapshot for callers/tests (WordMastery is a mutable class).
  public WordMastery GetMastery(WordId id) {
    WordMastery w;
    if (!_words.TryGetValue(id, out w))
      return new WordMastery { Id = id, Stage = MasteryStage.Unknown };
    return new WordMastery {
      Id = w.Id, Stage = w.Stage, Exposure = w.Exposure,
      RecognitionHit = w.RecognitionHit, RecognitionTotal = w.RecognitionTotal,
      SpeakingHit = w.SpeakingHit, SpeakingTotal = w.SpeakingTotal,
      ContextUse = w.ContextUse, NextReview = w.NextReview, Score = w.Score
    };
  }

  void BumpMeaningful(WordId id, LearnSource src) {
    if (IsMeaningful(id, src)) _meaningfulCount[id] = GetMeaningfulCount(id) + 1;
  }

  WordMastery GetOrCreate(WordId id) {
    WordMastery w;
    if (!_words.TryGetValue(id, out w)) {
      w = new WordMastery { Id = id, Stage = MasteryStage.Unknown };
      _words[id] = w;
    }
    return w;
  }

  void Promote(WordMastery w) {
    bool moved;
    do {
      moved = false;
      switch (w.Stage) {
        case MasteryStage.Unknown:
          if (w.Exposure >= ExposedAfterSeen) { w.Stage = MasteryStage.Exposed; moved = true; }
          break;
        case MasteryStage.Exposed:
          if (w.RecognitionTotal >= RecognizedTotal && w.RecognitionHit >= RecognizedHits) {
            w.Stage = MasteryStage.Recognized; moved = true;
          }
          break;
        case MasteryStage.Recognized:
          if (w.SpeakingTotal >= ProducedTotal && w.SpeakingHit >= ProducedHits) {
            w.Stage = MasteryStage.Produced; moved = true;
          }
          break;
        case MasteryStage.Produced:
          if (w.ContextUse >= UsedInContextAfter) {
            w.Stage = MasteryStage.UsedInContext;
            w.NextReview = DateTime.UtcNow + ReviewDelay;
            moved = true;
          }
          break;
        case MasteryStage.UsedInContext:
          if (w.NextReview != default(DateTime) && DateTime.UtcNow >= w.NextReview) {
            w.Stage = MasteryStage.Retained; moved = true;
          }
          break;
      }
    } while (moved);
  }
}
