// _SharedKernel/AcousticAnalysis.cs — Phase 2.1-local DSP core (Lead owns).
// Pure C# (NO UnityEngine, NO allocations retained): every method takes input
// arrays and returns fresh structs/arrays the caller owns. Burst-only by design:
// a 2 s utterance costs <20 ms CPU and a few hundred KB of transient floats,
// all released when the assessment returns (no caching, no statics holding audio).
//
// PIPELINE: mono PCM @~16 kHz -> framing (25 ms / 10 ms) -> per-frame features
// [energy, ZCR, centroid, lowBand, highBand] -> per-utterance gain/timbre
// normalization (CMN-style: child loudness/mic gain must not decide) ->
// DTW (Sakoe-Chiba band) vs PARAMETRIC expected sequence built from the
// target's phoneme manners -> segmental per-phoneme evidence + tail (ending)
// analysis + syllable estimate.
//
// HONESTY (no fake precision):
// - Expected prototypes are COARSE manner shapes (vowel = loud/low-ZCR/low,
//   fricative = noisy/high-ZCR/bright, ...), NOT speaker models. They separate
//   1-vs-2 syllables, onsets with different manners, and present-vs-deleted
//   codas — they do NOT identify the intruder phoneme in a substitution
//   (R->W reports "onset mismatch", never "heard W").
// - All scores are QUANTIZED to 0.1 steps (Quantize()) so no output pretends
//   to 2-decimal acoustic truth.
// - Thresholds are named consts below, calibrated on synthetic fixtures
//   (CT-P13 M2/M4, SIMULATED) — child-speech validation is NOT PROVEN.
using System;

[Serializable]
public struct FrameFeatures {
  public float Energy;    // 0..1 mean-absolute in Hamming window
  public float Zcr;       // 0..1 zero-crossing rate
  public float Centroid;  // 0..1 spectral centroid / (sampleRate/2)
  public float LowRatio;  // 0..1 energy in 0..1 kHz / total
  public float MidRatio;  // 0..1 energy in 1..4 kHz / total (F2 region: vowel/liquid identity)
  public float HighRatio; // 0..1 energy in 4..8 kHz / total

  public static float Distance(FrameFeatures a, FrameFeatures b) {
    // Silence-aware: a near-silent frame has NO meaningful spectrum (0/0),
    // so comparing its ratios to a prototype is noise. Level-only match with
    // an undefined-spectrum floor (loud-vs-silence is strong contradiction —
    // M4: 0.10 let frication slide into closures; 0.25 separates while the
    // both-silent branch keeps true closures/gaps cheap).
    if (a.Energy < 0.05f || b.Energy < 0.05f) {
      float level = Math.Abs(a.Energy - b.Energy);
      if (a.Energy < 0.05f && b.Energy < 0.05f) return level;
      return level + 0.25f;
    }
    float de = a.Energy - b.Energy;
    float dz = (a.Zcr - b.Zcr) * 0.7f;
    float dc = (a.Centroid - b.Centroid) * 0.8f;
    float dl = (a.LowRatio - b.LowRatio) * 0.8f;
    float dm = (a.MidRatio - b.MidRatio) * 0.8f;
    float dh = (a.HighRatio - b.HighRatio) * 0.8f;
    float d = (float)Math.Sqrt(de * de + dz * dz + dc * dc + dl * dl + dm * dm + dh * dh);
    // Stochastic-class correction: two independent frication realizations differ
    // by chance (~0.5-0.7) while meaning the same thing — discount the expected
    // noise distance so frication matches frication (M4: please-self sank on
    // seed variance while cross-words floated). Tone/stop/vowel pairs unaffected.
    if (a.Zcr > 0.5f && b.Zcr > 0.5f && a.Energy >= 0.15f && b.Energy >= 0.15f) {
      d -= 0.30f;
      if (d < 0f) d = 0f;
    }
    return d;
  }
}

[Serializable]
public struct PhonemeAcousticEvidence {
  public string PhonemeId;
  public PhonemeManner Manner;
  public float MatchScore; // 0..1 quantized 0.1 (1 = strong, 0 = missing/mismatch)
  public float Coverage;   // 0..1 quantized 0.1 (expected frames credibly matched)
  public bool Stretched;   // expected frames explainable ONLY by stretching a few
                           // child frames (DTW path-shape feature: deletion-like even
                           // when local pair distances look small, e.g. vowel covering a liquid coda)
  public bool Detected;    // !Missing && score >= MinPhonemeScore
  public bool Missing;     // coverage < MissingCoverageCeil || Stretched (deletion-like)
}

[Serializable]
public struct AcousticEvidence {
  public bool HasAcousticData;
  public float OverallMatch; // 0..1 quantized 0.1 (target resemblance, REPORTED — never fake precision)
  public float MatchRaw;     // unquantized blend (RANKING only: argmin/tiebreaks need total
                             // order; never displayed, never a quality claim)
  public float OnsetScore;   // 0..1 quantized 0.1 (first-phoneme realization; §5A initial sound)
  public float CodaScore;    // 0..1 quantized 0.1 (last-phoneme realization; §5C ending sound)
  public PhonemeAcousticEvidence[] PerPhoneme; // null when unavailable
  public bool MissingEnding; // last phoneme deletion-like (mandatory §5 capability)
  public bool WeakEnding;    // last phoneme partially realized
  public int EstimatedSyllables;
  public int ExpectedSyllables;
  public bool IsRepetition;  // voiced humps well above expected (e.g. "ball ball")
  public float DurationSec;
  public float VoicedSec;
  public string Notes; // compact developer string (no PII, no audio)
}

public static class AcousticAnalysis {
  // ---- named thresholds (calibrated on synthetic fixtures; child sessions retune) ----
  public const int WindowMs = 25;  public const int HopMs = 10;
  public const float LoosePairCeil = 1.1f;       // pair distance above this = not credibly matched
  public const float NormDistCeil = 1.0f;        // DTW norm distance mapping to OverallMatch 0
  public const float MinPhonemeCoverage = 0.35f;
  public const float MinPhonemeScore = 0.50f;    // Detected needs a real match (M4: 0.30
                                                 // "detected" every substitution (0.4-0.6),
                                                 // blinding the attempt-hood floor)
  public const float MissingCoverageCeil = 0.30f;
  public const float TailMissingCoverage = 0.35f;
  public const float TailWeakCoverage = 0.60f;
  public const float TailQuietRatio = 0.18f;     // tail energy < 18% of peak = decayed/cut
  public const float TailMissingScoreCeil = 0.60f; // stretched tail scoring below this never
                                                 // realized the coda (weak-but-present scores higher)
  public const float EdgeMismatchDeduction = 0.20f; // mangled edge costs two quanta (weakest-link)
  public const float StructuralGate = 0.65f;     // resemblance at/above this needs no structural
                                                 // tiebreaks (slow-correct keeps its Pass; weak
                                                 // resemblances below still get demoted)
  public const int TrimEdgeRollFrames = 3;       // 30 ms speech-edge roll kept when trimming silence
  public const float TrimFloorAbs = 0.002f;      // absolute trim floor (raw frame energy)
  public const int InstanceGapFrames = 12;       // >= 120 ms silence splits word instances
  public const int MinInstanceFrames = 10;       // instances shorter than 100 ms are fragments
  public const float BestInstanceCap = 0.70f;    // merged repetition resemblance ceiling (Pass, never Strong)
  public const float VoicedZcrCeil = 0.30f;      // tail ZCR below this = sonorant-like continuation
  public const float VoicedLowFloor = 0.45f;     // tail low-band above this = sonorant-like
  public const float SyllablePeakRatio = 0.30f;  // peak >= 30% of max energy
  public const int SyllableMinGapFrames = 12;    // 120 ms between syllable peaks
  public const float SyllableValleyRatio = 0.60f;// valley < 60% of smaller peak = separated
  public const float TailContrastFloor = 0.25f;  // mid-vs-tail mean distance below this =
                                                 // no spectral change into the coda region
  public const float TailInternalFloor = 0.15f;  // final-quarter halves below this =
                                                 // tail is one steady vowel to a loud stop
  public const float SyllableMismatchPenalty = 0.20f; // two quanta off OverallMatch
                                                 // hump count != expected syllables (structural term)
  public const float SyllableAgreementReward = 0.10f; // one quantum for hump count ==
                                                 // expected syllables (nucleus-count agreement)
  public const float CodaIdentityMargin = 0.15f; // coda owns its frames only if closer
                                                 // than the nucleus prototype by this margin
                                                 // (M4: 0.05 called realized IY-codas "not
                                                 // coda" — coarse vowel/vowel gaps need room;
                                                 // true deletions still miss by 0.2+)
                                                 // (M2: 0.10 rejected realized sonorant codas —
                                                 // coarse prototypes overlap, so the bar is
                                                 // tie-tolerance, not proof-beyond-doubt)
  public const float AbruptTailRatio = 0.70f;    // (legacy level gate, superseded by cliff below)
  public const float TailCliffFloor = 0.30f;     // max frame-to-frame energy DROP inside the tail:
                                                 // truncation stops cold (ba: 0.45+ over 1-2 frames);
                                                 // natural offsets decay gradually (<0.3/frame).
                                                 // M4: 0.45 missed fade-softened cuts.
  public const float FluxPeakFloor = 0.18f;      // frame-to-frame spectral jump above
                                                 // this starts a new acoustic regime
  public const float VouchFluxFloor = 0.30f;     // re-onset flux needed to vouch a closure
                                                 // (closure→burst ≈ 0.5+; noise-null dips
                                                 // and attack ramps ≈ 0.2 stay below)
  public const int FluxMinGapFrames = 6;         // regimes last >= 60 ms (child rate)
  public const float LengthMismatchRatio = 1.5f; // (legacy frame gate, superseded)
  public const float LengthMismatchDurRatio = 1.3f; // voiced duration > 1.3x prototype AND
                                                 // syllable mismatch TOGETHER = different word
                                                 // (either alone is innocent: slow speech
                                                 // stretches time; hesitation splits humps)
  public const float LengthMismatchPenalty = 0.20f; // two quanta (pairs with the syllable
                                                 // term; repetitions exempt — see below)
  public const int MinFramesForAssessment = 5;   // <50 ms voiced = too short for phoneme evidence
  public const float SilenceEnergyFloor = 0.001f;

  public static float Quantize(float v) {
    if (v < 0f) v = 0f;
    if (v > 1f) v = 1f;
    return (float)Math.Round(v * 10.0) / 10f;
  }

  // ================= FEATURE EXTRACTION =================

  public static void FrameLayout(int sampleRate, out int win, out int hop) {
    win = Math.Max(64, sampleRate * WindowMs / 1000);
    hop = Math.Max(32, sampleRate * HopMs / 1000);
  }

  // Trims long leading/trailing silence runs (capture quiet-start + silence
  // timeout), keeping a short speech-edge roll so stop closures/releases
  // survive. Threshold is relative (5% of peak, min absolute floor) so quiet
  // speech is never eaten. Returns a fresh array (caller-owned, transient).
  public static FrameFeatures[] TrimFrames(FrameFeatures[] frames) {
    if (frames == null || frames.Length == 0) return frames;
    float peak = 0f;
    for (int i = 0; i < frames.Length; i++)
      if (frames[i].Energy > peak) peak = frames[i].Energy;
    float thr = Math.Max(TrimFloorAbs, peak * 0.05f);
    int first = 0;
    while (first < frames.Length && frames[first].Energy < thr) first++;
    if (first >= frames.Length) return new FrameFeatures[0];
    int last = frames.Length - 1;
    while (last > first && frames[last].Energy < thr) last--;
    int s0 = Math.Max(0, first - TrimEdgeRollFrames);
    int s1 = Math.Min(frames.Length - 1, last + TrimEdgeRollFrames);
    var out_ = new FrameFeatures[s1 - s0 + 1];
    for (int i = s0; i <= s1; i++) out_[i - s0] = frames[i];
    return out_;
  }

  public static FrameFeatures[] ExtractFrames(float[] samples, int sampleRate) {
    if (samples == null || samples.Length == 0 || sampleRate <= 0)
      return new FrameFeatures[0];
    int win, hop;
    FrameLayout(sampleRate, out win, out hop);
    int n = (samples.Length - win) / hop + 1;
    if (n <= 0) return new FrameFeatures[0];
    // mono-average is the caller's job; guard anyway (interleaved stereo -> average pairs).
    var out_ = new FrameFeatures[n];
    float[] window = Hamming(win);
    float[] re = new float[NextPow2(win)];
    float[] im = new float[re.Length];
    int specN = re.Length;
    for (int f = 0; f < n; f++) {
      int off = f * hop;
      double e = 0;
      int zc = 0;
      float prev = 0f;
      for (int i = 0; i < win; i++) {
        float s = off + i < samples.Length ? samples[off + i] : 0f;
        float w = s * window[i];
        re[i] = w;
        im[i] = 0f;
        e += Math.Abs(w);
        if (i > 0 && ((prev >= 0f) != (w >= 0f))) zc++;
        prev = w;
      }
      for (int i = win; i < specN; i++) { re[i] = 0f; im[i] = 0f; }
      Fft(re, im);
      double total = 0, low = 0, mid = 0, high = 0, wsum = 0;
      int bins = specN / 2;
      for (int k = 1; k < bins; k++) {
        double mag = Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
        double freq = (double)k * sampleRate / specN;
        total += mag;
        wsum += mag * freq;
        if (freq < 1000.0) low += mag;
        else if (freq < 4000.0) mid += mag;
        if (freq >= 4000.0 && freq <= 8000.0) high += mag;
      }
      out_[f] = new FrameFeatures {
        Energy = (float)(e / win),
        Zcr = (float)zc / (win - 1),
        Centroid = total > 1e-9 ? (float)(wsum / total / (sampleRate * 0.5)) : 0f,
        LowRatio = total > 1e-9 ? (float)(low / total) : 0f,
        MidRatio = total > 1e-9 ? (float)(mid / total) : 0f,
        HighRatio = total > 1e-9 ? (float)(high / total) : 0f
      };
    }
    return out_;
  }

  // Per-utterance normalization: ENERGY peak-normalized (log-ish) so mic gain
  // and child loudness never decide. Spectral dims (ZCR/centroid/bands) stay
  // RAW on purpose: per-utterance spectral centering was tried and REJECTED —
  // it erases vowel identity (AO vs AE vs IY all collapse to the mean) and
  // closed-set classification collapses with it (M2 calibration: red->ball).
  // Mic coloration differences remain a documented limitation (NOT PROVEN).
  public static void NormalizeUtterance(FrameFeatures[] frames) {
    if (frames == null || frames.Length == 0) return;
    float peak = 0f;
    for (int i = 0; i < frames.Length; i++)
      if (frames[i].Energy > peak) peak = frames[i].Energy;
    if (peak < 1e-6f) peak = 1e-6f;
    for (int i = 0; i < frames.Length; i++) {
      FrameFeatures f = frames[i];
      float e = f.Energy / peak; // 0..1
      f.Energy = e < 0.02f ? e * 2f : 0.04f + 0.96f * (float)Math.Log10(1.0 + 9.0 * e);
      frames[i] = f;
    }
  }

  // ================= EXPECTED TRACK (synth exemplar — see ReferenceSynthesizer) =================
  // Reference tracks are rendered per attempt (transient, never retained), so
  // there is no prototype table here at all: the ONLY hand values left in
  // this file are decision thresholds (documented, calibrated, quarantined).

  // Edge gate by manner: self stop edges score ~0.7+ BY CONSTRUCTION (closure↔
  // closure pairs are free) while sung-through stops sit ~0.5-0.6; continuants
  // self ~0.8+. So stops demote below 0.65, continuants below 0.6. (History:
  // uniform 0.6 demoted self stops in the parametric era; 0.5 let sall pass
  // in the exemplar era. Exemplar symmetry moved self stops to 0.7+.)
  public static float EdgeGateFor(PhonemeManner manner) {
    return manner == PhonemeManner.Stop ? 0.65f : 0.6f;
  }

  // ================= DTW (Sakoe-Chiba band, normalized) =================

  public static float DtwNormalizedDistance(FrameFeatures[] a, FrameFeatures[] b) {
    int[] pathA, pathB;
    return DtwAlign(a, b, out pathA, out pathB);
  }

  // Returns path-cost / path-length. Path arrays map each alignment step to an
  // index in a / b (caller-owned, released by scope).
  public static float DtwAlign(FrameFeatures[] a, FrameFeatures[] b, out int[] pathA, out int[] pathB) {
    pathA = new int[0];
    pathB = new int[0];
    if (a == null || b == null || a.Length == 0 || b.Length == 0) return float.PositiveInfinity;
    int n = a.Length, m = b.Length;
    // Band MUST span the length difference (|n-m|) or no path exists at all
    // (long/repeated attempts vs a short target). Grows with mismatch, stays
    // banded for the normal similar-length case. M2 calibration: repetition
    // and 2-word attempts returned dtw-fail with a fixed 10% band.
    int band = Math.Max(12, Math.Abs(n - m) + Math.Max(n, m) / 10);
    const float INF = 1e30f;
    float[] prev = new float[m];
    float[] cur = new float[m];
    for (int j = 0; j < m; j++) prev[j] = INF;
    for (int i = 0; i < n; i++) {
      for (int j = 0; j < m; j++) cur[j] = INF;
      int j0 = Math.Max(0, i - band);
      int j1 = Math.Min(m - 1, i + band);
      for (int j = j0; j <= j1; j++) {
        float cost = FrameFeatures.Distance(a[i], b[j]);
        float best;
        if (i == 0 && j == 0) best = 0f;
        else {
          best = INF;
          if (i > 0 && j > 0) best = Math.Min(best, prev[j - 1]); // diag (in-band: |i-j|<=band)
          if (i > 0) best = Math.Min(best, prev[j]);              // up (out-of-band cells stay INF)
          if (j > j0) best = Math.Min(best, cur[j - 1]);          // left (j-1 in-band by j0)
        }
        cur[j] = cost + best;
      }
      float[] t = prev; prev = cur; cur = t;
    }
    float total = prev[m - 1];
    if (float.IsInfinity(total) || total >= INF / 2) return float.PositiveInfinity;
    // Backtrace needs the full matrix; recompute compactly for short utterances
    // (n*m <= ~64k cells for our windows — transient, released on return).
    float[,] d = new float[n, m];
    for (int i = 0; i < n; i++)
      for (int j = 0; j < m; j++)
        d[i, j] = INF;
    for (int i = 0; i < n; i++) {
      int j0 = Math.Max(0, i - band);
      int j1 = Math.Min(m - 1, i + band);
      for (int j = j0; j <= j1; j++) {
        float cost = FrameFeatures.Distance(a[i], b[j]);
        if (i == 0 && j == 0) d[i, j] = cost;
        else {
          float best = INF;
          if (i > 0 && j > 0) best = Math.Min(best, d[i - 1, j - 1]);
          if (i > 0 && Math.Abs((i - 1) - j) <= band) best = Math.Min(best, d[i - 1, j]);
          if (j > 0 && Math.Abs(i - (j - 1)) <= band) best = Math.Min(best, d[i, j - 1]);
          d[i, j] = cost + best;
        }
      }
    }
    // Greedy backtrace from (n-1, m-1).
    var ra = new int[n + m];
    var rb = new int[n + m];
    int len = 0, x = n - 1, y = m - 1, guard = n + m + 5;
    while (guard-- > 0) {
      ra[len] = x; rb[len] = y; len++;
      if (x == 0 && y == 0) break;
      float cDiag = (x > 0 && y > 0) ? d[x - 1, y - 1] : INF;
      float cUp = x > 0 ? d[x - 1, y] : INF;
      float cLeft = y > 0 ? d[x, y - 1] : INF;
      if (cDiag <= cUp && cDiag <= cLeft) { x--; y--; }
      else if (cUp <= cLeft) x--;
      else y--;
    }
    pathA = new int[len];
    pathB = new int[len];
    for (int k = 0; k < len; k++) {
      pathA[k] = ra[len - 1 - k];
      pathB[k] = rb[len - 1 - k];
    }
    return total / Math.Max(1, len);
  }

  // ================= TOP-LEVEL MATCH =================

  public static AcousticEvidence Analyze(float[] samples, int sampleRate, TargetPronunciation pron) {
    return AnalyzeInner(samples, sampleRate, pron, true);
  }

  // allowSplit=false for best-instance recursion (depth 1, single words).
  static AcousticEvidence AnalyzeInner(float[] samples, int sampleRate, TargetPronunciation pron, bool allowSplit) {
    var ev = new AcousticEvidence {
      HasAcousticData = false, OverallMatch = 0f, OnsetScore = 0f, CodaScore = 0f,
      PerPhoneme = null,
      MissingEnding = false, WeakEnding = false, EstimatedSyllables = 0,
      ExpectedSyllables = pron != null ? pron.SyllableCount : 0,
      IsRepetition = false, DurationSec = 0f, VoicedSec = 0f, Notes = "no-data"
    };
    if (samples == null || samples.Length == 0 || sampleRate <= 0) return ev;
    if (pron == null || !pron.IsUsable()) return ev;
    ev.DurationSec = (float)samples.Length / sampleRate;

    FrameFeatures[] frames = ExtractFrames(samples, sampleRate);
    // Production-critical trim: capture windows carry leading quiet-start and
    // trailing silence-timeout (up to seconds). Long silence runs are NOT
    // speech and must not enter DTW (they would smear every alignment), but
    // stop closures/releases live at speech edges — keep a 50 ms roll each
    // side so closures survive while room/timeout silence does not.
    frames = TrimFrames(frames);
    if (frames.Length < MinFramesForAssessment) {
      ev.Notes = "too-short";
      return ev;
    }
    // Voiced length from raw energy (pre-normalization copy).
    int voiced = 0;
    for (int i = 0; i < frames.Length; i++)
      if (frames[i].Energy >= SilenceEnergyFloor) voiced++;
    ev.VoicedSec = (float)voiced * HopMs / 1000f;

    NormalizeUtterance(frames);
    // Canonical reference track: the SAME extract->trim->normalize pipeline
    // applied to the synth exemplar (symmetric normalization — the parametric
    // era compared normalized child against UNNORMALIZED hand prototypes, a
    // systematic bias). Per-phoneme spans are real synth boundaries (no
    // duration-weight estimation). Transient per attempt, never retained.
    var refIds = new System.Collections.Generic.List<string>(pron.Length());
    for (int i = 0; i < pron.Length(); i++) refIds.Add(pron.Phonemes[i].Id);
    int[] refS0, refS1;
    float[] refPcm = ReferenceSynthesizer.Render(refIds, out refS0, out refS1);
    if (refPcm == null || refPcm.Length == 0) return ev;
    FrameFeatures[] expected = ExtractFrames(refPcm, sampleRate);
    expected = TrimFrames(expected);
    if (expected.Length == 0) return ev;
    NormalizeUtterance(expected);
    int fwin, fhop;
    FrameLayout(sampleRate, out fwin, out fhop);
    int hop1 = Math.Max(1, fhop);
    int phoneCount = pron.Length();
    int[] pStart = new int[phoneCount], pEnd = new int[phoneCount];
    for (int i = 0; i < phoneCount; i++) {
      pStart[i] = Math.Max(0, Math.Min(expected.Length, refS0[i] / hop1));
      pEnd[i] = Math.Max(pStart[i] + 1, Math.Min(expected.Length, (refS1[i] + hop1 - 1) / hop1));
    }
    if (expected.Length == 0) return ev;

    int[] pathA, pathB;
    float normDist = DtwAlign(frames, expected, out pathA, out pathB);
    if (float.IsInfinity(normDist)) {
      ev.Notes = "dtw-fail";
      return ev;
    }
    ev.HasAcousticData = true;
    // Base resemblance from the whole-path distance, EDGE-blended: DTW warping
    // evaporates edge mismatches (red-as-ball: R frames slide onto the vowel
    // prototype; please-as-ball: Z frication smears across the liquid coda).
    // Edges carry lexical identity (cohort model: onsets constrain the word
    // most; codas close it — and §5 mandates ending-sound evidence anyway),
    // so 0.25/0.20 edge weights are principled, not a hack.
    // M2 calibration: unblended and onset-only blends still tied (please/ball).
    float baseMatch = 1f - Clamp01(normDist / NormDistCeil);
    // Edge means over ALL aligned pairs (silence included: closures and gaps
    // are evidence, not dirt). M4: a speech-only subset blinded continuant
    // onsets to stop gaps AND misfired stop dip checks (the subset removed
    // the very closure it looked for — self-ball capped at 0.4).
    float onsetMean = LoosePairCeil, codaMean = LoosePairCeil;
    // Stop release-half means (VOT-bearing part of the edge): closure matches
    // everybody, the release (transient + voicing/aspiration) carries voicing
    // identity (pall-as-ball: aspiration where voice-bar belongs). M4.
    double onsetRelSum = 0, codaRelSum = 0;
    int onsetRelCnt = 0, codaRelCnt = 0;
    int lastPhone = pStart.Length - 1;
    int on0 = pStart[0], on1 = pEnd[0], co0 = pStart[lastPhone], co1 = pEnd[lastPhone];
    int onRel0 = on0 + (on1 - on0) / 2, coRel0 = co0 + (co1 - co0) / 2;
    {
      int cntO = 0, cntC = 0;
      double sumO = 0, sumC = 0;
      for (int s = 0; s < pathA.Length; s++) {
        int ej = pathB[s];
        int ci = pathA[s];
        // Trim rolls are capture artifacts (leading quiet-start / trailing
        // timeout silence re-added after stripping): they carry no phoneme
        // identity, so edge scoring skips them (M4: 3 free perfect pairs for
        // every onset, 3 free drag pairs for every coda).
        if (ej >= on0 && ej < on1 && ci >= TrimEdgeRollFrames) {
          sumO += FrameFeatures.Distance(frames[ci], expected[ej]);
          cntO++;
          if (ej >= onRel0) { onsetRelSum += FrameFeatures.Distance(frames[ci], expected[ej]); onsetRelCnt++; }
        }
        if (ej >= co0 && ej < co1 && ci < frames.Length - TrimEdgeRollFrames) {
          sumC += FrameFeatures.Distance(frames[ci], expected[ej]);
          cntC++;
          if (ej >= coRel0) { codaRelSum += FrameFeatures.Distance(frames[ci], expected[ej]); codaRelCnt++; }
        }
      }
      if (cntO > 0) onsetMean = (float)(sumO / cntO);
      if (cntC > 0) codaMean = (float)(sumC / cntC);
    }
    float onsetScore = 1f - Clamp01(onsetMean / LoosePairCeil);
    float codaScore = 1f - Clamp01(codaMean / LoosePairCeil);
    // Gap-in-sonorant-coda (POSITIONAL, warp-proof): a sonorant coda (voicing
    // expected throughout) containing an interior gap means a stop closure
    // sits where none belongs (red-audio's D gap inside one-target's N).
    // Stops/fricatives exempt (closures are their nature). M4.
    TargetPhoneme lastPhoneDef = pron.Phonemes[pStart.Length - 1];
    if (lastPhoneDef.IsSonorant()
        && HasClosureGap(frames,
          Math.Max(0, frames.Length - TrimEdgeRollFrames - 2 * Math.Max(1, co1 - co0)),
          frames.Length - TrimEdgeRollFrames))
      codaScore = Math.Min(codaScore, 0.4f);
    // Stop release-half check: closure matches everything (silence is silence),
    // so a stop edge is judged on its release (voicing/aspiration identity).
    // pall-as-ball: aspiration where B's voice-bar belongs. Gate 0.60 with the
    // same deduction (self releases score 0.7+ with margin). M4.
    bool releaseOnsetBad = false, releaseCodaBad = false;
    {
      TargetPhoneme firstPhoneDef = pron.Phonemes[0];
      if (firstPhoneDef.Manner == PhonemeManner.Stop && onsetRelCnt > 0) {
        float rel = 1f - Clamp01((float)(onsetRelSum / onsetRelCnt) / LoosePairCeil);
        if (rel < 0.60f) releaseOnsetBad = true;
      }
      if (lastPhoneDef.Manner == PhonemeManner.Stop && codaRelCnt > 0) {
        float rel = 1f - Clamp01((float)(codaRelSum / codaRelCnt) / LoosePairCeil);
        if (rel < 0.60f) releaseCodaBad = true;
      }
    }
    ev.OnsetScore = Quantize(onsetScore);
    ev.CodaScore = Quantize(codaScore);
    ev.OverallMatch = Quantize(0.55f * baseMatch + 0.25f * onsetScore + 0.20f * codaScore);
    float rawOverall = 0.55f * baseMatch + 0.25f * onsetScore + 0.20f * codaScore;
    ev.MatchRaw = rawOverall;
    // Mangled edges cap resemblance (weakest-link), with manner-aware gates:
    // self stop edges score ~0.7+ BY CONSTRUCTION (closure↔closure pairs are
    // free), so stops demote below 0.65 while continuants demote below 0.6.
    // A fired deduction also forfeits the syllable-agreement reward below
    // (mangled edges + clean structure still means an imperfect production —
    // M4: sall would otherwise rebound 0.4→0.5→0.6 Pass on the reward).
    bool edgeFired = false;
    if (ev.OnsetScore < EdgeGateFor(pron.Phonemes[0].Manner) || releaseOnsetBad) {
      ev.OverallMatch = Quantize(ev.OverallMatch - EdgeMismatchDeduction);
      rawOverall -= EdgeMismatchDeduction;
      edgeFired = true;
    }
    if (ev.CodaScore < EdgeGateFor(pron.Phonemes[pStart.Length - 1].Manner) || releaseCodaBad) {
      ev.OverallMatch = Quantize(ev.OverallMatch - EdgeMismatchDeduction);
      rawOverall -= EdgeMismatchDeduction;
    }

    // Per-phoneme: for each expected phoneme, gather aligned child frames.
    // TWO signals (comparison-based framework: path SHAPE + local distance):
    // coverage (credible pairs / budgeted) and stretch (distinct child frames
    // covering the phoneme — a vowel stretched over a liquid coda still pairs
    // closely frame-by-frame, but the path goes vertical: few distinct frames).
    int m = pron.Length();
    var per = new PhonemeAcousticEvidence[m];
    for (int i = 0; i < m; i++) {
      int matched = 0, credible = 0;
      int childFirst = int.MaxValue, childLast = int.MinValue;
      double sumDist = 0;
      int budgeted = Math.Max(1, pEnd[i] - pStart[i]);
      for (int s = 0; s < pathA.Length; s++) {
        int ej = pathB[s];
        if (ej >= pStart[i] && ej < pEnd[i]) {
          matched++;
          int ci = pathA[s];
          if (ci < childFirst) childFirst = ci;
          if (ci > childLast) childLast = ci;
          float dd = FrameFeatures.Distance(frames[ci], expected[ej]);
          sumDist += dd;
          if (dd < LoosePairCeil) credible++;
        }
      }
      int distinct = matched > 0 ? childLast - childFirst + 1 : 0;
      // Stretched: expected frames squeeze onto at most half the budgeted
      // child frames (DTW path goes vertical). Boundary-inclusive (a 12-frame
      // coda on 6 child frames is stretched, not "exactly half"). M4: strict <
      // missed tedd's IY (12-vs-6) by one frame.
      bool stretched = distinct * 2 <= budgeted;
      float coverage = (float)credible / budgeted;
      float mean = matched > 0 ? (float)(sumDist / matched) : LoosePairCeil;
      float score = 1f - Clamp01(mean / LoosePairCeil);
      bool missing = coverage < MissingCoverageCeil || stretched;
      per[i] = new PhonemeAcousticEvidence {
        PhonemeId = pron.Phonemes[i].Id,
        Manner = pron.Phonemes[i].Manner,
        MatchScore = Quantize(score),
        Coverage = Quantize(coverage),
        Stretched = stretched,
        Detected = !missing && score >= MinPhonemeScore,
        Missing = missing
      };
    }
    ev.PerPhoneme = per;

    // Ending analysis on the LAST phoneme: alignment overlap + raw tail shape.
    // A deleted sonorant coda ("ba" for ball) does NOT look quiet: the child
    // stops at vowel peak, and DTW drapes those loud vowel frames over the
    // liquid prototype (coverage reads 1.0 while the L was never produced).
    // The deciding cue is path SHAPE: does the coda own DEDICATED child frames,
    // or does it reuse the vowel's? sharedFraction = tail frames also claimed
    // by earlier phonemes / tail frames. M2 calibration: spectral-contrast
    // metrics failed here (synthetic release-decay mimics evolution, onset
    // bursts contaminate the mid window) — overlap is rate-robust where
    // contrast is not. Gated on duration (repetitions reuse frames by nature).
    PhonemeAcousticEvidence tail = per[m - 1];
    float peak = 0f;
    for (int i = 0; i < frames.Length; i++)
      if (frames[i].Energy > peak) peak = frames[i].Energy;
    int hopFramesPerSec = sampleRate * HopMs / 1000;
    // Coda-relative tail window: last third of the utterance (min 3 frames,
    // max 200 ms), EXCLUDING the trailing trim roll (fade-out silence would
    // dilute tail energy and fake "decayed" + sink abrupt readings — M4: wuh).
    int tail200 = sampleRate * 200 / 1000 / Math.Max(1, hopFramesPerSec);
    int tailN = Math.Max(3, Math.Min(frames.Length, Math.Min(tail200, Math.Max(3, frames.Length / 3))));
    int tailEnd = Math.Max(tailN, frames.Length - TrimEdgeRollFrames);
    int tailStart = Math.Max(0, tailEnd - tailN);
    double tailE = 0;
    double tailZ = 0, tailL = 0;
    for (int i = tailStart; i < tailEnd; i++) {
      tailE += frames[i].Energy;
      tailZ += frames[i].Zcr;
      tailL += frames[i].LowRatio;
    }
    int tailCount = Math.Max(1, tailEnd - tailStart);
    tailE /= tailCount; tailZ /= tailCount; tailL /= tailCount;
    bool abruptEnd = peak > 1e-6f && tailE >= peak * AbruptTailRatio;
    bool decayedEnd = peak > 1e-6f && tailE < peak * TailQuietRatio;
    bool sonorantTail = tailZ < VoicedZcrCeil && tailL > VoicedLowFloor;
    TargetPhoneme lastP = pron.Last();
    // Stop-coda closure vouch (POSITIONAL mirror of the onset rule): a stop
    // coda with NO closure gap in its window was never produced ("re" for red:
    // EH sung straight through where D's closure belongs) → mark Missing and
    // let the ending rules turn it into MissingEnding. Full stops vouch by
    // their closure and flow to the normal coverage/score path. M4.
    float closureChildEnergy = -1f; // diagnostic (Notes): 1 = vouched, 0 = none
    if (lastP.Manner == PhonemeManner.Stop) {
      int codaBudget = Math.Max(1, pEnd[m - 1] - pStart[m - 1]);
      int cw1 = Math.Max(0, frames.Length - TrimEdgeRollFrames - 2 * codaBudget);
      int cw0 = Math.Max(0, frames.Length - TrimEdgeRollFrames);
      bool vouched = HasClosureGap(frames, cw1, cw0);
      closureChildEnergy = vouched ? 1f : 0f;
      if (!vouched) {
        PhonemeAcousticEvidence noClo = per[m - 1];
        noClo.Missing = true;
        noClo.Detected = false;
        per[m - 1] = noClo;
        tail = noClo;
      }
    }

    ev.EstimatedSyllables = CountSyllables(frames);
    float protoDurSec = (float)expected.Length * HopMs / 1000f;
    float durRatio = protoDurSec > 0f ? ev.VoicedSec / protoDurSec : 1f;
    // Slow speech stretches humps apart without adding syllables: scale the
    // inter-peak gap by sounding rate (M4: slow-ball's stretched AO|L valley
    // split at the fixed 120 ms gap and punished slow-correct as WrongWord).
    if (durRatio > 1.15f) {
      int scaledGap = Math.Max(SyllableMinGapFrames,
        (int)Math.Round(SyllableMinGapFrames * Math.Min(1.5f, durRatio)));
      ev.EstimatedSyllables = CountSyllables(frames, scaledGap);
    }

    // Overlap second pass over the alignment path (transient, no retention).
    int tailMin = int.MaxValue, tailMax = int.MinValue, earlierMax = int.MinValue;
    for (int s = 0; s < pathA.Length; s++) {
      int ej = pathB[s];
      if (ej >= pStart[m - 1]) {
        if (pathA[s] < tailMin) tailMin = pathA[s];
        if (pathA[s] > tailMax) tailMax = pathA[s];
      } else {
        if (pathA[s] > earlierMax) earlierMax = pathA[s];
      }
    }
    int tailSpan = tailMax >= tailMin ? tailMax - tailMin + 1 : 0;
    int shared = tailSpan > 0 ? Math.Max(0, earlierMax - tailMin + 1) : tailSpan;
    float sharedFraction = tailSpan > 0 ? (float)shared / tailSpan : 1f;
    // Coda identity shootout: do the tail frames look like the CODA, or are
    // they just more of the preceding NUCLEUS vowel? ("ba": tail frames ARE
    // AO — closer to the AO prototype than to L by a wide margin, so the L
    // has no independent identity. Full "ball": tail frames ARE L.)
    // Rate-robust (no duration involved) and speaker-light (relative, not
    // absolute). Needs a preceding vowel; without one the test abstains.
    int nucleusIdx = -1;
    for (int i = m - 2; i >= 0; i--) {
      if (pron.Phonemes[i].IsVowel()) { nucleusIdx = i; break; }
    }
    float dCoda = 0f, dNucleus = 0f;
    if (nucleusIdx >= 0 && tailSpan > 0) {
      // Data-driven protos: means of the REFERENCE spans (no hand table).
      FrameFeatures codaProto = MeanFrames(expected, pStart[m - 1], pEnd[m - 1]);
      FrameFeatures nucProto = MeanFrames(expected, pStart[nucleusIdx], pEnd[nucleusIdx]);
      double sC = 0, sN = 0;
      int cnt = 0;
      for (int c = tailMin; c <= tailMax; c++) {
        if (frames[c].Energy < 0.05f) continue; // trailing fade/decay silence carries
                                               // no phoneme identity (M2: it compressed
                                               // the shootout margin to noise)
        // Identity lives in SPECTRUM, not level (M2: offset-decay inflates the
        // energy dim for both prototypes) — spectral-emphasis distance here.
        sC += IdentityDistance(frames[c], codaProto);
        sN += IdentityDistance(frames[c], nucProto);
        cnt++;
      }
      if (cnt > 0) {
        dCoda = (float)(sC / cnt);
        dNucleus = (float)(sN / cnt);
      }
    }
    // Regime deficit as corroboration (rate-robust: lengthened "baaa" still
    // has fewer regimes than ball). Diagnostic in Notes; decision uses identity.
    int regimesObserved = CountRegimes(frames);
    int regimesExpected = pron.MannerRuns();
    // Truncation cliff: max frame-to-frame energy drop inside the tail window
    // (stops cold vs decays — level-based "abrupt" misread realized sonorant
    // sustain (60-65% of peak) as truncation, M4).
    float tailCliff = 0f;
    for (int i = tailStart + 1; i < tailEnd; i++) {
      float drop = frames[i - 1].Energy - frames[i].Energy;
      if (drop > tailCliff) tailCliff = drop;
    }
    bool cliffEnd = tailCliff >= TailCliffFloor;
    // Three-way nearest-prototype verdict on the tail (data-driven protos):
    // coda-like (dCoda clearly smaller) = realized; nucleus-like + cliff =
    // truncated ON the vowel ("ba", "appl": the L never started); within the
    // margin both ways = ambiguous (weak, never deletion). M4: a separate
    // "stopped" test compared cross-word sonorants and false-fired.
    bool codaLike = dCoda + CodaIdentityMargin < dNucleus;
    bool nucleusLike = nucleusIdx >= 0 && dNucleus + CodaIdentityMargin < dCoda;
    // Truncation = nucleus-like tail that STOPS (cliff = cold cut; loud-level
    // abrupt = stops at vowel peak with no decay). Either stop signature plus
    // nucleus-likeness means the coda never started. Double-gated so realized
    // codas (coda-like) and soft fades (no stop) never trip it. M4.
    bool stopsCold = cliffEnd || abruptEnd;
    bool noCodaAttempt = durRatio < 1.6f && stopsCold && nucleusLike && !codaLike;
    if (noCodaAttempt) {
      // No dedicated coda frames exist: mark the tail phoneme missing even
      // though its coverage pairs looked close (they are the vowel's frames).
      PhonemeAcousticEvidence t = per[m - 1];
      t.Stretched = true;
      t.Missing = true;
      t.Detected = false;
      per[m - 1] = t;
      tail = t;
    }
    bool tailGoodShape = !tail.Stretched && tail.Coverage >= TailWeakCoverage;
    if (noCodaAttempt) {
      ev.MissingEnding = true;
      ev.WeakEnding = false;
    } else if (tailGoodShape && !decayedEnd) {
      ev.MissingEnding = false;
      ev.WeakEnding = tail.Coverage < 0.85f;
    } else if (tail.Missing && (abruptEnd || decayedEnd || tail.MatchScore < TailMissingScoreCeil || tail.Coverage < TailMissingCoverage)) {
      bool sonorantTrace = lastP.IsSonorant() && sonorantTail && !tail.Stretched && !decayedEnd;
      ev.MissingEnding = !sonorantTrace; // weak-but-present sonorant coda: never deletion
      ev.WeakEnding = !ev.MissingEnding;
    } else {
      ev.WeakEnding = true; // partial realization, not deletion
    }

    ev.IsRepetition = (ev.EstimatedSyllables >= ev.ExpectedSyllables + 2)
      || (ev.EstimatedSyllables > ev.ExpectedSyllables && durRatio > 1.8f);

    // Best-instance merge for repetitions/self-corrections ("ba-ba-ball"): the
    // whole-utterance match dilutes the good final copy, so score voiced spans
    // separately (depth-1 recursion, single words — no further splits), keep
    // the BEST resemblance but the LAST span's ending (recency: the child's
    // final production state), capped at Pass (disfluent != model production).
    // Structural terms below exempt repetitions, so the merged score stands.
    bool mergedBest = false;
    if (allowSplit) {
      int win, hop;
      FrameLayout(sampleRate, out win, out hop);
      int[][] spans = SplitInstances(frames);
      if (spans.Length >= 2) {
        AcousticEvidence best = ev;
        bool haveBest = false;
        for (int si = 0; si < spans.Length; si++) {
          int s0 = spans[si][0] * hop;
          int s1 = Math.Min(samples.Length, spans[si][1] * hop + win);
          if (s1 - s0 < win) continue;
          var sub = new float[s1 - s0];
          for (int k = s0; k < s1; k++) sub[k - s0] = samples[k];
          AcousticEvidence ie = AnalyzeInner(sub, sampleRate, pron, false);
          if (!ie.HasAcousticData) continue;
          if (!haveBest || ie.OverallMatch > best.OverallMatch) { best = ie; haveBest = true; }
        }
        if (haveBest) {
          int ls0 = spans[spans.Length - 1][0] * hop;
          int ls1 = Math.Min(samples.Length, spans[spans.Length - 1][1] * hop + win);
          AcousticEvidence last = ev;
          if (ls1 - ls0 >= win) {
            var lsub = new float[ls1 - ls0];
            for (int k = ls0; k < ls1; k++) lsub[k - ls0] = samples[k];
            AcousticEvidence le = AnalyzeInner(lsub, sampleRate, pron, false);
            if (le.HasAcousticData) last = le;
          }
          ev.OverallMatch = Math.Min(best.OverallMatch, BestInstanceCap);
          rawOverall = Math.Min(best.MatchRaw, BestInstanceCap);
          ev.OnsetScore = best.OnsetScore;
          ev.CodaScore = best.CodaScore;
          ev.PerPhoneme = best.PerPhoneme;
          ev.MissingEnding = last.MissingEnding;
          ev.WeakEnding = last.WeakEnding;
          ev.IsRepetition = true;
          mergedBest = true;
        }
      }
    }

    // Structural terms: nucleus-count agreement/disagreement is target
    // structure (content syllables), plus unexplained VOICED length (pauses and
    // leading/trailing silence excluded — only sounding material counts).
    // Both skipped for repetitions. Slow speech (long, humps agree) and
    // hesitation (split humps, normal length) each trip at most ONE term;
    // a different word trips both (apple-as-ball: 2 humps + 1.4x sounding).
    // M2/M3: needed to break ties without punishing slow-correct speech.
    bool sylMismatch = ev.EstimatedSyllables != ev.ExpectedSyllables;
    int sylDeficit = ev.ExpectedSyllables - ev.EstimatedSyllables; // >0: whole nucleus missing
    if (!ev.IsRepetition && ev.ExpectedSyllables > 0) {
      if (!sylMismatch && !edgeFired) {
        ev.OverallMatch = Quantize(ev.OverallMatch + SyllableAgreementReward);
        rawOverall += SyllableAgreementReward;
      } else if (ev.OverallMatch < StructuralGate) {
        ev.OverallMatch = Quantize(ev.OverallMatch - SyllableMismatchPenalty);
        rawOverall -= SyllableMismatchPenalty;
      }
      if (sylMismatch && durRatio > LengthMismatchDurRatio && ev.OverallMatch < StructuralGate) {
        ev.OverallMatch = Quantize(ev.OverallMatch - LengthMismatchPenalty);
        rawOverall -= LengthMismatchPenalty;
      }
      // A missing nucleus at NORMAL rate is a different word, not slow speech
      // (ball-as-apple: 1 hump where 2 belong, sounding at 0.9x — the apple
      // syllable never happened). M4: ball-as-apple otherwise stays Partial.
      if (sylDeficit >= 1 && durRatio < LengthMismatchDurRatio && ev.OverallMatch < StructuralGate) {
        ev.OverallMatch = Quantize(ev.OverallMatch - SyllableMismatchPenalty);
        rawOverall -= SyllableMismatchPenalty;
      }
      // Interior-gap contradiction (evidence asymmetry: presence of a stop gap
      // where the target has NO stop phoneme at all is positive counter-
      // evidence; absence of gaps proves little and is never punished here).
      // red-audio's D closure inside all-sonorant one (W AH N). M4.
      if (ev.OverallMatch < StructuralGate && TargetHasNoStop(pron) && InteriorGapFrac(frames) > 0.10f) {
        ev.OverallMatch = Quantize(ev.OverallMatch - SyllableMismatchPenalty);
        rawOverall -= SyllableMismatchPenalty;
      }
    }
    if (rawOverall < 0f) rawOverall = 0f;
    if (rawOverall > 1f) rawOverall = 1f;
    ev.MatchRaw = rawOverall;

    float tailContrast = Contrast(frames); // diagnostic only (see noCodaAttempt)
    int detN = 0;
    for (int i = 0; i < per.Length; i++) if (per[i].Detected) detN++;
    ev.Notes = "match=" + ev.OverallMatch.ToString("0.0")
      + " base=" + baseMatch.ToString("0.00")
      + " det=" + detN + "/" + per.Length
      + " tailCov=" + tail.Coverage.ToString("0.0")
      + " shared=" + sharedFraction.ToString("0.00")
      + " identity=" + dCoda.ToString("0.00") + "vs" + dNucleus.ToString("0.00")
      + " regimes=" + regimesObserved + "/" + regimesExpected
      + " diagnostic-contrast=" + tailContrast.ToString("0.00")
      + " syl=" + ev.EstimatedSyllables + "/" + ev.ExpectedSyllables
      + " durRatio=" + durRatio.ToString("0.0")
      + (closureChildEnergy >= 0f ? " cloE=" + closureChildEnergy.ToString("0.00") : "")
      + " cliff=" + tailCliff.ToString("0.00")
      + (mergedBest ? " best-instance" : "")
      + (ev.MissingEnding ? " MISSING_ENDING" : ev.WeakEnding ? " weak-ending" : " ending-ok")
      + (ev.IsRepetition ? " repetition" : "");
    return ev;
  }

  // Mean-feature distance between the final-quarter halves ([3n/4, 7n/8) vs
  // [7n/8, n)): a realized coda evolves here (onset + decay); a truncated
  // vowel is steady. Companion to Contrast() — see noCodaAttempt.
  public static float TailInternal(FrameFeatures[] frames) {
    if (frames == null || frames.Length < 16) return 1f;
    int n = frames.Length;
    int a0 = n * 3 / 4, a1 = n * 7 / 8, b1 = n;
    return RegionDistance(frames, a0, a1, a1, b1);
  }

  // Mean-feature distance between the utterance middle ([n/4, n/2)) and the
  // final third ([2n/3, n)): vowel->coda transitions score high, a single
  // vowel running to a loud stop scores near zero. Pure shape cue.
  public static float Contrast(FrameFeatures[] frames) {
    if (frames == null || frames.Length < 8) return 1f;
    int n = frames.Length;
    return RegionDistance(frames, n / 4, n / 2, n * 2 / 3, n);
  }

  static float RegionDistance(FrameFeatures[] frames, int s0, int s1, int t0, int t1) {
    if (s1 - s0 < 2 || t1 - t0 < 2) return 1f;
    double e0 = 0, z0 = 0, c0 = 0, l0 = 0, m0 = 0, h0 = 0;
    for (int i = s0; i < s1; i++) {
      e0 += frames[i].Energy; z0 += frames[i].Zcr; c0 += frames[i].Centroid;
      l0 += frames[i].LowRatio; m0 += frames[i].MidRatio; h0 += frames[i].HighRatio;
    }
    double e1 = 0, z1 = 0, c1 = 0, l1 = 0, m1 = 0, h1 = 0;
    for (int i = t0; i < t1; i++) {
      e1 += frames[i].Energy; z1 += frames[i].Zcr; c1 += frames[i].Centroid;
      l1 += frames[i].LowRatio; m1 += frames[i].MidRatio; h1 += frames[i].HighRatio;
    }
    double ns = Math.Max(1, s1 - s0), nt = Math.Max(1, t1 - t0);
    var A = new FrameFeatures {
      Energy = (float)(e0 / ns), Zcr = (float)(z0 / ns), Centroid = (float)(c0 / ns),
      LowRatio = (float)(l0 / ns), MidRatio = (float)(m0 / ns), HighRatio = (float)(h0 / ns)
    };
    var B = new FrameFeatures {
      Energy = (float)(e1 / nt), Zcr = (float)(z1 / nt), Centroid = (float)(c1 / nt),
      LowRatio = (float)(l1 / nt), MidRatio = (float)(m1 / nt), HighRatio = (float)(h1 / nt)
    };
    return FrameFeatures.Distance(A, B);
  }

  // Spectral regimes: 1 + spectral-flux peaks (frame-to-frame full-feature
  // distance above FluxPeakFloor, >= FluxMinGapFrames apart). Leading silence
  // is skipped (not a regime); interior gaps SPLIT (apple's P closure is a
  // real boundary). Counts acoustic sounds actually surfaced — compare with
  // TargetPronunciation.MannerRuns() for deletion detection.
  public static int CountRegimes(FrameFeatures[] frames) {
    if (frames == null || frames.Length == 0) return 0;
    int start = 0;
    while (start < frames.Length && frames[start].Energy < SilenceEnergyFloor) start++;
    if (start >= frames.Length - 1) return 0;
    int count = 1, lastPeak = start;
    for (int i = start + 1; i < frames.Length; i++) {
      float flux = SpectralFlux(frames[i - 1], frames[i]);
      if (flux >= FluxPeakFloor && i - lastPeak >= FluxMinGapFrames) {
        count++;
        lastPeak = i;
      }
    }
    return count;
  }

  // Phoneme-identity distance: spectrum decides, level only whispers.
  // Used ONLY by the coda shootout (DTW keeps the level-aware Distance).
  static float IdentityDistance(FrameFeatures a, FrameFeatures b) {
    float dz = (a.Zcr - b.Zcr) * 0.7f;
    float dc = (a.Centroid - b.Centroid) * 0.8f;
    float dl = (a.LowRatio - b.LowRatio) * 0.8f;
    float dm = (a.MidRatio - b.MidRatio) * 1.2f; // F2 region decides AO-vs-L
    float dh = (a.HighRatio - b.HighRatio) * 0.8f;
    float de = (a.Energy - b.Energy) * 0.5f;
    return (float)Math.Sqrt(de * de + dz * dz + dc * dc + dl * dl + dm * dm + dh * dh);
  }

  static float SpectralFlux(FrameFeatures a, FrameFeatures b) {    float dz = (a.Zcr - b.Zcr) * 0.7f;
    float dc = (a.Centroid - b.Centroid) * 0.8f;
    float dl = (a.LowRatio - b.LowRatio) * 0.8f;
    float dm = (a.MidRatio - b.MidRatio) * 1.2f; // F2 jumps weigh most (AO->L)
    float dh = (a.HighRatio - b.HighRatio) * 0.8f;
    float de = (a.Energy - b.Energy) * 0.5f;
    return (float)Math.Sqrt(de * de + dz * dz + dc * dc + dl * dl + dm * dm + dh * dh);
  }

  // Target has no stop phoneme anywhere: any interior stop gap in the attempt
  // contradicts an all-continuant target (used by the gap-contradiction term).
  static bool TargetHasNoStop(TargetPronunciation pron) {
    if (pron == null || !pron.IsUsable()) return false;
    for (int i = 0; i < pron.Length(); i++)
      if (pron.Phonemes[i].Manner == PhonemeManner.Stop) return false;
    return true;
  }

  // Fraction of middle-80% frames that are near-silence (stop-gap evidence
  // independent of alignment). Edges excluded (roll + offset decay live there).
  static float InteriorGapFrac(FrameFeatures[] frames) {
    if (frames == null || frames.Length < 10) return 0f;
    int s0 = frames.Length / 10, s1 = frames.Length * 9 / 10;
    int gap = 0, total = 0;
    for (int i = s0; i < s1; i++) {
      total++;
      if (frames[i].Energy < 0.05f) gap++;
    }
    return total > 0 ? (float)gap / total : 0f;
  }

  // Mean feature vector over a frame range (reference-span protos for the
  // identity shootout). Empty range -> zero vector (never throws).
  static FrameFeatures MeanFrames(FrameFeatures[] frames, int s0, int s1) {
    var m = new FrameFeatures();
    if (frames == null || frames.Length == 0) return m;
    int a = Math.Max(0, s0), b = Math.Min(frames.Length, s1);
    if (b <= a) return m;
    double e = 0, z = 0, c = 0, l = 0, d = 0, h = 0;
    for (int i = a; i < b; i++) {
      e += frames[i].Energy; z += frames[i].Zcr; c += frames[i].Centroid;
      l += frames[i].LowRatio; d += frames[i].MidRatio; h += frames[i].HighRatio;
    }
    double n = Math.Max(1, b - a);
    m.Energy = (float)(e / n); m.Zcr = (float)(z / n); m.Centroid = (float)(c / n);
    m.LowRatio = (float)(l / n); m.MidRatio = (float)(d / n); m.HighRatio = (float)(h / n);
    return m;
  }

  // Closure-gap probe (POSITIONAL, warp-proof, room-proof): a RUN of ≥2
  // sub-threshold frames ending in a SHARP re-onset (flux spike = release burst
  // after closure). Single-frame dips (frication attacks, noise nulls, glottal
  // blips, Hamming smear) and gradual ramps (offset decays) are NOT gaps —
  // M4: S-attack vouched sall's missing closure; fade-softened cuts must use
  // the cliff test instead. Stops vouch BY gaps; sonorants are contradicted BY
  // gaps. One rule, two directions. M4.
  static bool HasClosureGap(FrameFeatures[] frames, int s0, int s1) {
    if (frames == null || frames.Length == 0) return false;
    int a = Math.Max(0, s0), b = Math.Min(frames.Length, s1);
    for (int i = a; i < b; i++) {
      if (frames[i].Energy >= 0.10f) continue;
      int run = 0;
      while (i + run < b && frames[i + run].Energy < 0.10f) run++;
      if (run >= 2) {
        int reon = Math.Min(frames.Length - 1, i + run);
        int rpre = Math.Max(0, i + run - 1);
        if (SpectralFlux(frames[rpre], frames[reon]) >= VouchFluxFloor) return true;
      }
      i += Math.Max(0, run - 1);
    }
    return false;
  }
  // silence gaps (normalized energy: gain-invariant). Stop closures and brief
  // dips never split (gap must reach InstanceGapFrames); fragments under
  // MinInstanceFrames are dropped. Pure shape logic, no thresholds on content.
  static int[][] SplitInstances(FrameFeatures[] frames) {
    var spans = new System.Collections.Generic.List<int[]>();
    int n = frames == null ? 0 : frames.Length;
    int i = 0;
    while (i < n) {
      while (i < n && frames[i].Energy < 0.03f) i++; // skip gap
      int s = i;
      while (i < n) {
        if (frames[i].Energy < 0.03f) {
          int g = i;
          while (g < n && frames[g].Energy < 0.03f) g++;
          if (g - i >= InstanceGapFrames) break; // real gap: span ends at i
          i = g; // brief dip: continues the span
        } else {
          i++;
        }
      }
      if (i - s >= MinInstanceFrames) spans.Add(new int[] { s, i });
    }
    return spans.ToArray();
  }

  public static int CountSyllables(FrameFeatures[] frames) {
    return CountSyllables(frames, SyllableMinGapFrames);
  }

  public static int CountSyllables(FrameFeatures[] frames, int minGapFrames) {
    if (frames == null || frames.Length < 3) return frames == null || frames.Length == 0 ? 0 : 1;
    float peak = 0f;
    for (int i = 0; i < frames.Length; i++)
      if (frames[i].Energy > peak) peak = frames[i].Energy;
    if (peak <= 0f) return 0;
    // 5-frame moving average envelope.
    float[] env = new float[frames.Length];
    for (int i = 0; i < frames.Length; i++) {
      double s = 0; int c = 0;
      for (int k = -2; k <= 2; k++) {
        int j = i + k;
        if (j >= 0 && j < frames.Length) { s += frames[j].Energy; c++; }
      }
      env[i] = (float)(s / Math.Max(1, c));
    }
    float floor = peak * SyllablePeakRatio;
    int count = 0, lastPeak = -minGapFrames - 1;
    for (int i = 1; i < frames.Length - 1; i++) {
      if (env[i] >= floor && env[i] >= env[i - 1] && env[i] > env[i + 1]
          && i - lastPeak > minGapFrames) {
        // prominence: valley since last peak below ratio of smaller peak.
        bool prominent = true;
        if (count > 0) {
          float valley = float.MaxValue;
          for (int j = lastPeak; j <= i; j++)
            if (env[j] < valley) valley = env[j];
          float smaller = Math.Min(env[lastPeak], env[i]);
          prominent = smaller > 0f && valley < smaller * SyllableValleyRatio;
        }
        if (prominent) { count++; lastPeak = i; }
      }
    }
    return Math.Max(1, count);
  }

  // ================= small math helpers (no Unity, no Linq) =================

  static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }

  static float[] Hamming(int n) {
    var w = new float[n];
    for (int i = 0; i < n; i++)
      w[i] = 0.54f - 0.46f * (float)Math.Cos(2.0 * Math.PI * i / (n - 1));
    return w;
  }

  static int NextPow2(int n) {
    int p = 1;
    while (p < n) p <<= 1;
    return Math.Max(2, p);
  }

  // Iterative radix-2 FFT (in-place, real input in re, imag zeros in im).
  public static void Fft(float[] re, float[] im) {
    int n = re.Length;
    for (int i = 1, j = 0; i < n; i++) {
      int bit = n >> 1;
      for (; (j & bit) != 0; bit >>= 1) j &= ~bit;
      j |= bit;
      if (i < j) {
        float t = re[i]; re[i] = re[j]; re[j] = t;
        t = im[i]; im[i] = im[j]; im[j] = t;
      }
    }
    for (int len = 2; len <= n; len <<= 1) {
      double ang = -2.0 * Math.PI / len;
      float wr = (float)Math.Cos(ang), wi = (float)Math.Sin(ang);
      for (int i = 0; i < n; i += len) {
        float cwr = 1f, cwi = 0f;
        for (int k = 0; k < len / 2; k++) {
          float ur = re[i + k], ui = im[i + k];
          float vr = re[i + k + len / 2] * cwr - im[i + k + len / 2] * cwi;
          float vi = re[i + k + len / 2] * cwi + im[i + k + len / 2] * cwr;
          re[i + k] = ur + vr; im[i + k] = ui + vi;
          re[i + k + len / 2] = ur - vr; im[i + k + len / 2] = ui - vi;
          float nwr = cwr * wr - cwi * wi;
          cwi = cwr * wi + cwi * wr;
          cwr = nwr;
        }
      }
    }
  }
}
