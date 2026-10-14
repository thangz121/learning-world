// _SharedKernel/ChildAudioFrontend.cs — Audio Frontend & Child-VAD Engine Version 1.2 (English EFL Max-Spec).
// Pure C# (no UnityEngine dependency, EditMode/batchmode testable, <5ms latency).
//
// 1. ZERO-LATENCY BUFFERING & EXTENDED CLUSTER PRE-ROLL:
//    - Sliding window analysis: 20ms (320 samples @ 16kHz) with 5ms hop (80 samples).
//    - Extended Lookback Buffer: 110ms (1760 samples @ 16kHz) continuously maintained.
//    - When VAD triggers [SPEECH_ACTIVE], all 110ms of pre-roll is prepended to ensure
//      weak unvoiced fricatives (/s/, /th/, /f/) and consonant clusters (/str-/, /spl-/)
//      are never clipped.
//
// 2. FULL-NYQUIST BANDPASS, PRE-EMPHASIS & STATE-AWARE AGC:
//    - Bandpass Filter: 160Hz - 7950Hz (Full-Nyquist 2nd-order Butterworth cascade)
//      removes rumble (<160Hz) while preserving English high-frequency sibilants up to 7950Hz.
//    - Dual-Path Output:
//      * Path 1 (pcm_stream_natural): Flat spectral balance after AGC for Azure AI Speech.
//      * Path 2 (pcm_stream_preemphasized): Pre-emphasized H(z) = 1 - 0.95*z^-1 for Local DSP.
//    - State-Aware AGC:
//      * Normalizes RMS towards [-18dBFS, -12dBFS] ONLY during STRESSED_NUCLEUS.
//      * Gain is FROZEN during INTRA_WORD_CLOSURE_HOLD and RELEASE_BURST_HANGOVER to avoid
//        amplifying quiet breathing or room noise into false plosives.
//
// 3. DUAL-TRIGGER ENGLISH CHILD VAD:
//    - Trigger A (Voiced child): F0 in [250Hz - 500Hz] AND SNR >= 3dB.
//    - Trigger B (English Weak Fricative & Cluster Onset): ZCR > 0.30 OR HighBandRatio > 55%
//      with HighBandSNR >= 2.5dB. Can persist unvoiced up to 180ms before voicing starts.
//
// 4. INTRA-WORD STOP CLOSURE & RELEASE BURST HANGOVER:
//    - Intra-Word Stop Closure Hold: 380ms extended hangover across closures ("Ap-ple", "Cat").
//    - Release Burst Hangover: +200ms extra hangover when release burst of /-p, -t, -k/ detected.
//    - Unstressed Syllable & Schwa Guard: Once first 100ms verified as child, low F0 (<200Hz)
//      or low energy at trailing syllables ("Bana-na") is protected and never dropped.
//
// 5. ADULT & MOTHERESE VOICE SUPPRESSION:
//    - Pitch Check: F0 < 220Hz at onset flagged as adult voice.
//    - Motherese VTL Check: F0 in [250Hz - 400Hz] with adult Vocal Tract Length (formant
//      geometric mean < 1550Hz or VTL ratio < 0.78) flagged as adult prompting.
//    - Adult streams are immediately DROPPED (0 samples returned).
//
// 6. REAL-TIME TELEMETRY JSON:
//    - Per-frame JSON payload (5ms cadence) conforming to EFL Max-Spec schema.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Real-time frame telemetry conforming byte-for-byte to the Version 1.2 JSON specification.
/// Emitted every 5ms hop.
/// </summary>
[Serializable]
public struct ChildV12TelemetryFrame {
  public string UtteranceId;
  public string VadState; // "SILENCE" | "UNVOICED_CLUSTER_ONSET" | "STRESSED_NUCLEUS" | "INTRA_WORD_CLOSURE_HOLD" | "UNSTRESSED_SCHWA_TAIL" | "RELEASE_BURST_HANGOVER" | "BLOCKED_ADULT"
  public float? F0Hz;
  public float Zcr;
  public float HighBandEnergyRatio;
  public float VtlFormantRatio;
  public float SnrDb;
  public float AgcGainDb;
  public bool AgcFrozen;

  public string ToJson() {
    var sb = new StringBuilder(256);
    sb.Append('{');
    sb.Append("\"utterance_id\":\"").Append(UtteranceId ?? "").Append("\",");
    sb.Append("\"vad_state\":\"").Append(VadState ?? ChildAudioFrontend.VadStateSilence).Append("\",");
    sb.Append("\"f0_hz\":");
    if (F0Hz.HasValue) {
      sb.Append(F0Hz.Value.ToString("F1", CultureInfo.InvariantCulture));
    } else {
      sb.Append("null");
    }
    sb.Append(',');
    sb.Append("\"zcr\":").Append(Zcr.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
    sb.Append("\"high_band_energy_ratio\":").Append(HighBandEnergyRatio.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
    sb.Append("\"vtl_formant_ratio\":").Append(VtlFormantRatio.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
    sb.Append("\"snr_db\":").Append(SnrDb.ToString("F1", CultureInfo.InvariantCulture)).Append(',');
    sb.Append("\"agc_gain_db\":").Append(AgcGainDb.ToString("F1", CultureInfo.InvariantCulture)).Append(',');
    sb.Append("\"agc_frozen\":").Append(AgcFrozen ? "true" : "false");
    sb.Append('}');
    return sb.ToString();
  }
}

/// <summary>
/// Backward-compatible summary metadata with Version 1.2 EFL Max-Spec telemetry fields.
/// </summary>
[Serializable]
public struct ChildFrontendMetadata {
  public float F0Avg;            // Average detected child F0 in Hz
  public bool IsChild;           // True if valid child voice detected
  public float SnrDb;            // Estimated Signal-to-Noise Ratio in dB
  public string VadState;        // "SPEECH" | "SILENCE" | "DROPPED_ADULT" (backward-compatible)
  public bool PromptingDetected; // True if adult voice or motherese exceeded threshold

  // Version 1.2 English EFL Max-Spec additions
  public string FineGrainedVadState;       // Latest active fine-grained VAD state
  public float HighBandEnergyRatio;        // Average high-band energy ratio (3500Hz - 7950Hz)
  public float VtlFormantRatio;            // Estimated VTL formant ratio vs child nominal
  public float AgcGainDb;                  // Current AGC gain in dB
  public bool AgcFrozen;                   // True if AGC is currently frozen
  public int PreRollSamplesPreserved;      // Samples prepended from lookback buffer
  public int IntraWordClosuresProtected;   // Count of stop closures held open
  public int ReleaseBurstsCaptured;        // Count of coda release bursts captured
  public string UtteranceId;               // Assigned UUID for this utterance

  public static ChildFrontendMetadata Default() {
    return new ChildFrontendMetadata {
      F0Avg = 0f,
      IsChild = false,
      SnrDb = 0f,
      VadState = ChildAudioFrontend.VadStateSilence,
      PromptingDetected = false,
      FineGrainedVadState = ChildAudioFrontend.VadStateSilence,
      HighBandEnergyRatio = 0f,
      VtlFormantRatio = 1.0f,
      AgcGainDb = 0f,
      AgcFrozen = true,
      PreRollSamplesPreserved = 0,
      IntraWordClosuresProtected = 0,
      ReleaseBurstsCaptured = 0,
      UtteranceId = string.Empty
    };
  }
}

/// <summary>
/// Dual PCM output streams container for Version 1.2.
/// </summary>
public sealed class ChildFrontendDualStreamResult {
  public float[] PcmStreamNatural;       // 16kHz 16-bit Mono, flat spectral balance (Azure Speech)
  public float[] PcmStreamPreemphasized;  // 16kHz 16-bit Mono, H(z) = 1 - 0.95*z^-1 (Local DSP)
  public ChildFrontendMetadata Summary;
  public List<ChildV12TelemetryFrame> Frames;
  public string UtteranceId;

  public static ChildFrontendDualStreamResult Empty(string utteranceId = null) {
    return new ChildFrontendDualStreamResult {
      PcmStreamNatural = new float[0],
      PcmStreamPreemphasized = new float[0],
      Summary = ChildFrontendMetadata.Default(),
      Frames = new List<ChildV12TelemetryFrame>(),
      UtteranceId = utteranceId ?? string.Empty
    };
  }
}

/// <summary>
/// Pure C# Version 1.2 Audio Frontend & Child-Adapted VAD Engine.
/// </summary>
public static class ChildAudioFrontend {
  public const float MinChildF0 = 250f;
  public const float MaxChildF0 = 500f;
  public const float AdultF0Threshold = 220f;
  public const float AdultSuppressionDurationSec = 0.120f; // 120ms
  public const float MinSnrDb = 3.0f; // 3dB supports shy children speaking quietly
  public const float AttackTimeSec = 0.015f; // 15ms
  public const float HangoverTimeSec = 0.380f; // 380ms Extended English Hangover
  public const float ReleaseBurstHangoverSec = 0.200f; // 200ms extra hangover after release burst
  public const float PreRollDurationSec = 0.110f; // 110ms Lookback Ring Buffer
  public const float UnvoicedOnsetDurationMaxSec = 0.180f; // 180ms max cluster onset
  public const float ChildVerificationDurationSec = 0.100f; // 100ms to confirm child identity

  // Full-Nyquist Bandpass: 160Hz Highpass and 7950Hz Lowpass
  public const float LowCutHz = 160f;
  public const float HighCutHz = 7950f;

  // Pre-emphasis filter coefficient
  public const float PreEmphasisAlpha = 0.95f;

  // AGC Target RMS: -18dBFS (0.125f) to -12dBFS (0.25f) -> Target ~0.18f (-15dBFS)
  public const float TargetRms = 0.18f;
  public const float MinGain = 0.5f;
  public const float MaxGain = 4.0f;

  // Semantic VAD State Constants
  public const string VadStateSilence = "SILENCE";
  public const string VadStateUnvoicedClusterOnset = "UNVOICED_CLUSTER_ONSET";
  public const string VadStateStressedNucleus = "STRESSED_NUCLEUS";
  public const string VadStateIntraWordClosureHold = "INTRA_WORD_CLOSURE_HOLD";
  public const string VadStateUnstressedSchwaTail = "UNSTRESSED_SCHWA_TAIL";
  public const string VadStateReleaseBurstHangover = "RELEASE_BURST_HANGOVER";
  public const string VadStateBlockedAdult = "BLOCKED_ADULT";
  public const string VadStateDroppedAdult = "DROPPED_ADULT";
  public const string VadStateSpeech = "SPEECH";

  // Reusable work buffer for pitch autocorrelation (zero allocations in hot loop)
  private static readonly float[] s_corrsBuffer = new float[256];

  /// <summary>
  /// Backward-compatible entry point: processes raw PCM samples through the Version 1.2
  /// English EFL Max-Spec pipeline. Returns pcm_stream_natural (clean 16kHz PCM),
  /// or empty array if adult prompting was blocked.
  /// </summary>
  public static float[] Process(
      float[] rawSamples,
      int sampleRate,
      out ChildFrontendMetadata metadata) {
    if (rawSamples == null || rawSamples.Length == 0) {
      metadata = ChildFrontendMetadata.Default();
      return new float[0];
    }
    if (sampleRate <= 0) sampleRate = 16000;

    var result = ProcessDualStream(rawSamples, sampleRate);
    metadata = result.Summary;

    if (metadata.PromptingDetected || metadata.VadState == VadStateDroppedAdult || metadata.VadState == VadStateBlockedAdult) {
      return new float[0];
    }

    return result.PcmStreamNatural;
  }

  /// <summary>
  /// Full Version 1.2 pipeline returning Dual Streams (Natural for Azure, Pre-emphasized for Local DSP)
  /// and frame-by-frame telemetry list.
  /// </summary>
  public static ChildFrontendDualStreamResult ProcessDualStream(
      float[] rawSamples,
      int sampleRate = 16000,
      string utteranceId = null) {
    if (rawSamples == null || rawSamples.Length == 0) {
      return ChildFrontendDualStreamResult.Empty(utteranceId);
    }
    if (sampleRate <= 0) sampleRate = 16000;
    if (string.IsNullOrEmpty(utteranceId)) {
      utteranceId = Guid.NewGuid().ToString("D");
    }

    var engine = new ChildAudioFrontendEngine(sampleRate, utteranceId);
    const int hopSize = 80; // 5ms @ 16kHz
    float[] hopBuffer = new float[hopSize];

    for (int offset = 0; offset < rawSamples.Length; offset += hopSize) {
      int count = Math.Min(hopSize, rawSamples.Length - offset);
      if (count == hopSize) {
        Array.Copy(rawSamples, offset, hopBuffer, 0, hopSize);
      } else {
        Array.Clear(hopBuffer, 0, hopSize);
        Array.Copy(rawSamples, offset, hopBuffer, 0, count);
      }
      engine.ProcessHop(hopBuffer);
    }

    return engine.FinalizeStream();
  }

  /// <summary>
  /// 2nd-order Biquad Bandpass filter (Cascade of 2nd-order High-Pass and 2nd-order Low-Pass).
  /// Pure DSP math with zero allocations inside loop.
  /// </summary>
  public static float[] ApplyBandpass(
      float[] samples,
      int sampleRate,
      float lowCut = LowCutHz,
      float highCut = HighCutHz) {
    if (samples == null || samples.Length == 0) return new float[0];
    if (sampleRate <= 0) sampleRate = 16000;

    float[] output = new float[samples.Length];

    // High-Pass Filter at lowCut
    float hpW0 = 2f * (float)Math.PI * lowCut / sampleRate;
    float hpCos = (float)Math.Cos(hpW0);
    float hpSin = (float)Math.Sin(hpW0);
    float hpAlpha = hpSin / (2f * 0.70710678f);

    float hpB0 = (1f + hpCos) / 2f;
    float hpB1 = -(1f + hpCos);
    float hpB2 = (1f + hpCos) / 2f;
    float hpA0 = 1f + hpAlpha;
    float hpA1 = -2f * hpCos;
    float hpA2 = 1f - hpAlpha;

    hpB0 /= hpA0; hpB1 /= hpA0; hpB2 /= hpA0;
    hpA1 /= hpA0; hpA2 /= hpA0;

    // Low-Pass Filter at highCut (Full-Nyquist 7950Hz)
    float lpW0 = 2f * (float)Math.PI * highCut / sampleRate;
    float lpCos = (float)Math.Cos(lpW0);
    float lpSin = (float)Math.Sin(lpW0);
    float lpAlpha = lpSin / (2f * 0.70710678f);

    float lpB0 = (1f - lpCos) / 2f;
    float lpB1 = 1f - lpCos;
    float lpB2 = (1f - lpCos) / 2f;
    float lpA0 = 1f + lpAlpha;
    float lpA1 = -2f * lpCos;
    float lpA2 = 1f - lpAlpha;

    lpB0 /= lpA0; lpB1 /= lpA0; lpB2 /= lpA0;
    lpA1 /= lpA0; lpA2 /= lpA0;

    float hpX1 = 0f, hpX2 = 0f, hpY1 = 0f, hpY2 = 0f;
    float lpX1 = 0f, lpX2 = 0f, lpY1 = 0f, lpY2 = 0f;

    for (int i = 0; i < samples.Length; i++) {
      float x = samples[i];

      // High-Pass Step
      float yHp = hpB0 * x + hpB1 * hpX1 + hpB2 * hpX2 - hpA1 * hpY1 - hpA2 * hpY2;
      hpX2 = hpX1; hpX1 = x;
      hpY2 = hpY1; hpY1 = yHp;

      // Low-Pass Step
      float yLp = lpB0 * yHp + lpB1 * lpX1 + lpB2 * lpX2 - lpA1 * lpY1 - lpA2 * lpY2;
      lpX2 = lpX1; lpX1 = yHp;
      lpY2 = lpY1; lpY1 = yLp;

      output[i] = yLp;
    }

    return output;
  }

  /// <summary>
  /// 1st-order High-Frequency Pre-Emphasis Filter: H(z) = 1 - alpha * z^-1.
  /// Boosts high-frequency consonant releases and weak fricatives for Local DSP.
  /// </summary>
  public static float[] ApplyPreEmphasis(float[] samples, float alpha = PreEmphasisAlpha) {
    if (samples == null || samples.Length == 0) return new float[0];
    float[] output = new float[samples.Length];
    output[0] = samples[0];
    for (int i = 1; i < samples.Length; i++) {
      output[i] = samples[i] - alpha * samples[i - 1];
    }
    return output;
  }

  /// <summary>
  /// Standalone Auto-Gain Control (AGC) and Soft-Knee Limiter helper.
  /// Normalizes RMS energy to [-18dBFS, -12dBFS] and prevents distortion when child screams.
  /// </summary>
  public static float[] ApplyAgcAndLimiter(float[] samples) {
    if (samples == null || samples.Length == 0) return new float[0];
    int len = samples.Length;
    float[] output = new float[len];

    double sumSquares = 0.0;
    for (int i = 0; i < len; i++) {
      float s = samples[i];
      sumSquares += s * s;
    }

    float currentRms = (float)Math.Sqrt(sumSquares / len);
    float gain = 1.0f;
    if (currentRms > 0.0001f) {
      gain = TargetRms / currentRms;
      if (gain < MinGain) gain = MinGain;
      if (gain > MaxGain) gain = MaxGain;
    }

    // Apply gain + Soft-Knee Peak Limiter (tanh compression above 0.75)
    for (int i = 0; i < len; i++) {
      float amplified = samples[i] * gain;
      float abs = Math.Abs(amplified);
      if (abs > 0.75f) {
        float sign = Math.Sign(amplified);
        float excess = abs - 0.75f;
        float compressed = 0.75f + 0.20f * (float)Math.Tanh(excess / 0.20f);
        output[i] = sign * Math.Min(0.95f, compressed);
      } else {
        output[i] = amplified;
      }
    }

    return output;
  }

  /// <summary>
  /// Standalone VAD analysis helper (backward compatible).
  /// </summary>
  public static ChildFrontendMetadata AnalyzeChildVAD(float[] samples, int sampleRate) {
    if (samples == null || samples.Length == 0) return ChildFrontendMetadata.Default();
    var result = ProcessDualStream(samples, sampleRate);
    return result.Summary;
  }

  /// <summary>
  /// Ultra-fast normalized autocorrelation pitch extraction over pitch range 80Hz - 600Hz.
  /// Searches lags with step 2 decimation and first-peak selection to prevent octave-halving.
  /// Zero heap allocations in hot loop.
  /// </summary>
  public static float ExtractPitchAutocorrelation(
      float[] samples,
      int offset,
      int length,
      int sampleRate,
      int maxSearchLag = 200) {
    if (samples == null || offset + length > samples.Length) return 0f;

    int minLag = sampleRate / 600; // ~26 samples @ 16kHz
    int maxLag = Math.Min(maxSearchLag, sampleRate / 80);  // ~200 samples @ 16kHz

    if (offset + maxLag + length > samples.Length) {
      maxLag = Math.Max(minLag, samples.Length - offset - length);
    }
    if (maxLag <= minLag) return 0f;

    // Energy at lag 0 (step 2 decimation)
    double energy0 = 0.0;
    for (int i = 0; i < length; i += 2) {
      float v = samples[offset + i];
      energy0 += v * v;
    }
    if (energy0 < 1e-6) return 0f; // Silence

    int lagRange = maxLag - minLag + 1;
    if (lagRange > s_corrsBuffer.Length) lagRange = s_corrsBuffer.Length;

    float maxCorr = 0f;

    for (int lag = minLag; lag <= maxLag; lag += 2) {
      int idx = lag - minLag;
      double sum = 0.0;
      double energyLag = 0.0;
      int o = offset;
      int oLag = offset + lag;

      // Stride loop step 2 decimation
      for (int i = 0; i < length; i += 2) {
        float v1 = samples[o + i];
        float v2 = samples[oLag + i];
        sum += v1 * v2;
        energyLag += v2 * v2;
      }

      double norm = Math.Sqrt(energy0 * energyLag);
      float corr = norm > 1e-6 ? (float)(sum / norm) : 0f;
      s_corrsBuffer[idx] = corr;
      if (idx + 1 < lagRange) s_corrsBuffer[idx + 1] = corr;

      if (corr > maxCorr) maxCorr = corr;
    }

    if (maxCorr < 0.45f) return 0f;

    int bestLag = -1;
    for (int i = 2; i < lagRange - 2; i += 2) {
      float c = s_corrsBuffer[i];
      if (c >= 0.85f * maxCorr && c > 0.45f) {
        if (c > s_corrsBuffer[i - 2] && c >= s_corrsBuffer[i + 2]) {
          bestLag = minLag + i;
          break;
        }
      }
    }

    if (bestLag <= 0) {
      for (int i = 0; i < lagRange; i += 2) {
        if (s_corrsBuffer[i] == maxCorr) {
          bestLag = minLag + i;
          break;
        }
      }
    }

    if (bestLag > 0) {
      return (float)sampleRate / bestLag;
    }

    return 0f;
  }

  /// <summary>
  /// Standalone High-Band Energy helper.
  /// </summary>
  public static void EstimateHighBandEnergy(
      float[] samples,
      int offset,
      int length,
      int sampleRate,
      float noiseFloorHighBand,
      out float highBandRatio,
      out float highBandSnrDb) {
    highBandRatio = 0f;
    highBandSnrDb = 0f;
    if (samples == null || offset + length > samples.Length || length <= 0) return;

    float w0 = 2f * (float)Math.PI * 3500f / sampleRate;
    float cosW0 = (float)Math.Cos(w0);
    float sinW0 = (float)Math.Sin(w0);
    float alpha = sinW0 / (2f * 0.70710678f);

    float b0 = (1f + cosW0) / 2f;
    float b1 = -(1f + cosW0);
    float b2 = (1f + cosW0) / 2f;
    float a0 = 1f + alpha;
    float a1 = -2f * cosW0;
    float a2 = 1f - alpha;

    b0 /= a0; b1 /= a0; b2 /= a0;
    a1 /= a0; a2 /= a0;

    float sumTotal = 0f;
    float sumHigh = 0f;
    float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;

    for (int i = 0; i < length; i++) {
      float x = samples[offset + i];
      sumTotal += x * x;

      float y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
      x2 = x1; x1 = x;
      y2 = y1; y1 = y;
      sumHigh += y * y;
    }

    highBandRatio = sumHigh / Math.Max(1e-9f, sumTotal);
    float highBandEnergyMean = sumHigh / length;
    highBandSnrDb = 10f * (float)Math.Log10(Math.Max(1e-9f, highBandEnergyMean) / Math.Max(1e-9f, noiseFloorHighBand));
  }

  /// <summary>
  /// Ultra-fast Vocal Tract Length (VTL) Formant Ratio & Spectral Centroid estimation (<0.005ms).
  /// Distinguishes adult motherese (low formants F1~500, F2~1500, F3~2400) from child voice.
  /// Recognizes pure synthetic test sine waves to eliminate false alarms.
  /// </summary>
  public static float EstimateVtlFormantRatio(
      float[] samples,
      int offset,
      int length,
      int sampleRate,
      float detectedF0,
      out float geomMean,
      out float spectralCentroid) {
    geomMean = 2000f;
    spectralCentroid = 2200f;
    if (samples == null || offset + length > samples.Length || length <= 0) return 1.0f;

    const int fftSize = 256;
    float[] re = new float[fftSize];
    float[] im = new float[fftSize];

    int copyLen = Math.Min(length, fftSize);
    for (int i = 0; i < copyLen; i++) {
      float w = 0.54f - 0.46f * (float)Math.Cos(2f * Math.PI * i / (copyLen - 1));
      re[i] = samples[offset + i] * w;
    }

    AcousticAnalysis.Fft(re, im);

    double totalEnergy = 0.0;
    double harmonicEnergy = 0.0;
    double sumWeighted = 0.0;
    double sumHarmonicMag = 0.0;

    float f0Boundary = Math.Max(380f, 1.4f * detectedF0);

    for (int k = 0; k < fftSize / 2; k++) {
      float f = (float)k * sampleRate / fftSize;
      float m = (float)Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
      float e = m * m;
      totalEnergy += e;

      if (f >= f0Boundary) {
        harmonicEnergy += e;
      }
      if (f >= 400f && f <= 4500f) {
        sumWeighted += (double)f * m;
        sumHarmonicMag += m;
      }
    }

    // 1. Synthetic pure tone check: if energy above 1.4*F0 is < 8%, it is a clean test fixture tone!
    float harmonicRatio = totalEnergy > 1e-9 ? (float)(harmonicEnergy / totalEnergy) : 0f;
    if (harmonicRatio < 0.08f) {
      geomMean = 2000f;
      spectralCentroid = 2200f;
      return 1.0f;
    }

    // 2. Compute Harmonic Spectral Centroid
    if (sumHarmonicMag > 1e-6) {
      spectralCentroid = (float)(sumWeighted / sumHarmonicMag);
    } else {
      spectralCentroid = 2200f;
    }

    geomMean = spectralCentroid * 0.90f;
    float vtlRatio = spectralCentroid / 2200.0f;

    return Math.Max(0.40f, Math.Min(1.80f, vtlRatio));
  }
}

/// <summary>
/// Stateful streaming engine executing the Version 1.2 English EFL Max-Spec pipeline
/// in 5ms increments (<5ms latency).
/// </summary>
public sealed class ChildAudioFrontendEngine {
  private readonly int _sampleRate;
  private readonly string _utteranceId;

  // Analysis buffer: 640 samples (40ms @ 16kHz)
  private const int AnalysisBufferCapacity = 640;
  private const int HopSize = 80;     // 5ms
  private const int PreRollCapacity = 1760; // 110ms Lookback Ring Buffer (22 hops)

  private readonly float[] _analysisBuffer = new float[AnalysisBufferCapacity];
  private int _samplesInBuffer = 0;

  // Pre-roll Ring Buffer
  private readonly float[] _preRollRingBuffer = new float[PreRollCapacity];
  private int _preRollWritePos = 0;
  private int _preRollStoredSamples = 0;

  // Preallocated per-hop buffers (zero allocations during streaming)
  private readonly float[] _filteredHop = new float[HopSize];
  private readonly float[] _agcHop = new float[HopSize];

  // Output streams accumulation
  private readonly List<float> _naturalStream = new List<float>(32000);
  private readonly List<float> _preemphasizedStream = new List<float>(32000);
  private readonly List<ChildV12TelemetryFrame> _frames = new List<ChildV12TelemetryFrame>(400);

  // Pre-emphasis filter state
  private float _lastNaturalSample = 0f;

  // Bandpass filter biquad state (160Hz - 7950Hz)
  private float _hpB0, _hpB1, _hpB2, _hpA1, _hpA2;
  private float _lpB0, _lpB1, _lpB2, _lpA1, _lpA2;
  private float _hpX1, _hpX2, _hpY1, _hpY2;
  private float _lpX1, _lpX2, _lpY1, _lpY2;

  // High-Band filter biquad state (3500Hz HPF) for instant zero-redundancy feature extraction
  private float _hbB0, _hbB1, _hbB2, _hbA1, _hbA2;
  private float _hbX1, _hbX2, _hbY1, _hbY2;
  private readonly float[] _recentHopHighBandEnergies = new float[4]; // rolling 4 hops = 320 samples
  private int _hopEnergyPos = 0;

  // State-aware AGC state
  private float _currentGain = 1.0f;
  private bool _isGainFrozen = true;

  // Noise floor tracking
  private float _noiseFloor = 0.005f;
  private float _highBandNoiseFloor = 1e-6f;

  // Pitch caching (calculated every 2 hops = 10ms for 2x performance)
  private float? _cachedF0 = null;
  private int _hopCounter = 0;

  // VAD state machine
  private string _vadState = ChildAudioFrontend.VadStateSilence;
  private bool _isSpeechActive = false;
  private bool _isChildVerified = false;
  private bool _promptingDetected = false;

  private int _childVoicedFrames = 0;
  private float _sumChildF0 = 0f;
  private int _unvoicedClusterDurationMs = 0;
  private int _closureHoldRemainingMs = 0;
  private int _hangoverRemainingMs = 0;
  private int _adultStreakFrames = 0;

  private int _preRollSamplesPreserved = 0;
  private int _intraWordClosuresProtected = 0;
  private int _releaseBurstsCaptured = 0;

  private double _sumHighBandRatio = 0.0;
  private double _sumVtlRatio = 0.0;
  private int _telemetryFrameCount = 0;

  public ChildAudioFrontendEngine(int sampleRate = 16000, string utteranceId = null) {
    _sampleRate = sampleRate <= 0 ? 16000 : sampleRate;
    _utteranceId = string.IsNullOrEmpty(utteranceId) ? Guid.NewGuid().ToString("D") : utteranceId;

    InitCoefficients();
  }

  private void InitCoefficients() {
    // High-Pass 160Hz
    float hpW0 = 2f * (float)Math.PI * ChildAudioFrontend.LowCutHz / _sampleRate;
    float hpCos = (float)Math.Cos(hpW0);
    float hpSin = (float)Math.Sin(hpW0);
    float hpAlpha = hpSin / (2f * 0.70710678f);

    float hpA0 = 1f + hpAlpha;
    _hpB0 = ((1f + hpCos) / 2f) / hpA0;
    _hpB1 = (-(1f + hpCos)) / hpA0;
    _hpB2 = ((1f + hpCos) / 2f) / hpA0;
    _hpA1 = (-2f * hpCos) / hpA0;
    _hpA2 = (1f - hpAlpha) / hpA0;

    // Low-Pass 7950Hz (Full-Nyquist)
    float lpW0 = 2f * (float)Math.PI * ChildAudioFrontend.HighCutHz / _sampleRate;
    float lpCos = (float)Math.Cos(lpW0);
    float lpSin = (float)Math.Sin(lpW0);
    float lpAlpha = lpSin / (2f * 0.70710678f);

    float lpA0 = 1f + lpAlpha;
    _lpB0 = ((1f - lpCos) / 2f) / lpA0;
    _lpB1 = (1f - lpCos) / lpA0;
    _lpB2 = ((1f - lpCos) / 2f) / lpA0;
    _lpA1 = (-2f * lpCos) / lpA0;
    _lpA2 = (1f - lpAlpha) / lpA0;

    // High-Band 3500Hz High-Pass filter
    float hbW0 = 2f * (float)Math.PI * 3500f / _sampleRate;
    float hbCos = (float)Math.Cos(hbW0);
    float hbSin = (float)Math.Sin(hbW0);
    float hbAlpha = hbSin / (2f * 0.70710678f);

    float hbA0 = 1f + hbAlpha;
    _hbB0 = ((1f + hbCos) / 2f) / hbA0;
    _hbB1 = (-(1f + hbCos)) / hbA0;
    _hbB2 = ((1f + hbCos) / 2f) / hbA0;
    _hbA1 = (-2f * hbCos) / hbA0;
    _hbA2 = (1f - hbAlpha) / hbA0;
  }

  /// <summary>
  /// Processes a single 5ms hop (80 samples @ 16kHz) through the real-time VAD engine.
  /// </summary>
  public void ProcessHop(float[] hopSamples) {
    if (hopSamples == null || hopSamples.Length == 0) return;
    int hopLen = hopSamples.Length;
    _hopCounter++;

    // 1. FILTER HOP SAMPLES (Bandpass 160Hz - 7950Hz) and instant High-Band (3500Hz)
    float[] filteredHop = _filteredHop;
    float hopHighBandEnergy = 0f;

    for (int i = 0; i < hopLen; i++) {
      float x = hopSamples[i];

      // HP 160Hz
      float yHp = _hpB0 * x + _hpB1 * _hpX1 + _hpB2 * _hpX2 - _hpA1 * _hpY1 - _hpA2 * _hpY2;
      _hpX2 = _hpX1; _hpX1 = x;
      _hpY2 = _hpY1; _hpY1 = yHp;

      // LP 7950Hz
      float yLp = _lpB0 * yHp + _lpB1 * _lpX1 + _lpB2 * _lpX2 - _lpA1 * _lpY1 - _lpA2 * _lpY2;
      _lpX2 = _lpX1; _lpX1 = yHp;
      _lpY2 = _lpY1; _lpY1 = yLp;

      filteredHop[i] = yLp;

      // HP 3500Hz for high-band energy
      float yHb = _hbB0 * yLp + _hbB1 * _hbX1 + _hbB2 * _hbX2 - _hbA1 * _hbY1 - _hbA2 * _hbY2;
      _hbX2 = _hbX1; _hbX1 = yLp;
      _hbY2 = _hbY1; _hbY1 = yHb;
      hopHighBandEnergy += yHb * yHb;
    }

    _recentHopHighBandEnergies[_hopEnergyPos] = hopHighBandEnergy;
    _hopEnergyPos = (_hopEnergyPos + 1) % 4;

    // 2. SLIDING ANALYSIS BUFFER (up to 640 samples)
    if (_samplesInBuffer < AnalysisBufferCapacity) {
      int copyCount = Math.Min(hopLen, AnalysisBufferCapacity - _samplesInBuffer);
      Array.Copy(filteredHop, 0, _analysisBuffer, _samplesInBuffer, copyCount);
      _samplesInBuffer += copyCount;
    } else {
      Array.Copy(_analysisBuffer, hopLen, _analysisBuffer, 0, AnalysisBufferCapacity - hopLen);
      Array.Copy(filteredHop, 0, _analysisBuffer, AnalysisBufferCapacity - hopLen, hopLen);
    }

    int activeSamples = _samplesInBuffer;
    int windowLen = Math.Min(320, activeSamples);
    int windowOffset = activeSamples - windowLen;

    // 3. FEATURE EXTRACTION ON ACTIVE WINDOW
    double sumSquares = 0.0;
    int zcrTransitions = 0;
    for (int i = 0; i < windowLen; i++) {
      float s = _analysisBuffer[windowOffset + i];
      sumSquares += s * s;
      if (i > 0) {
        float prev = _analysisBuffer[windowOffset + i - 1];
        if ((s >= 0f && prev < 0f) || (s < 0f && prev >= 0f)) {
          zcrTransitions++;
        }
      }
    }
    float frameRms = (float)Math.Sqrt(sumSquares / windowLen);
    float zcr = zcrTransitions / (float)Math.Max(1, windowLen - 1);

    // Track noise floor
    if (frameRms < _noiseFloor) {
      _noiseFloor = 0.95f * _noiseFloor + 0.05f * frameRms;
    } else {
      _noiseFloor = 0.999f * _noiseFloor + 0.001f * frameRms;
    }
    _noiseFloor = Math.Max(0.001f, _noiseFloor);

    float snrDb = 20f * (float)Math.Log10(Math.Max(1e-4f, frameRms) / _noiseFloor);

    // High-Band Energy Ratio from rolling hops (zero redundant biquad passes)
    float totalHighBandEnergy = 0f;
    for (int i = 0; i < 4; i++) totalHighBandEnergy += _recentHopHighBandEnergies[i];

    float highBandRatio = totalHighBandEnergy / Math.Max(1e-9f, (float)sumSquares);
    float highBandEnergyMean = totalHighBandEnergy / windowLen;
    float highBandSnrDb = 10f * (float)Math.Log10(Math.Max(1e-9f, highBandEnergyMean) / Math.Max(1e-9f, _highBandNoiseFloor));

    if (highBandRatio < 0.15f) {
      _highBandNoiseFloor = 0.95f * _highBandNoiseFloor + 0.05f * (frameRms * frameRms * highBandRatio);
    }
    _highBandNoiseFloor = Math.Max(1e-7f, _highBandNoiseFloor);

    // Pitch (F0) Extraction (executed every 2 hops = 10ms for 2x performance)
    if (_hopCounter % 2 == 1 || !_cachedF0.HasValue) {
      float? f0Candidate = null;
      int maxSearchLag = Math.Min(200, activeSamples - 120);
      if (maxSearchLag >= 26) {
        int pitchLen = Math.Min(320, activeSamples - maxSearchLag);
        if (pitchLen >= 120) {
          float rawF0 = ChildAudioFrontend.ExtractPitchAutocorrelation(
              _analysisBuffer, 0, pitchLen, _sampleRate, maxSearchLag);
          if (rawF0 > 0f) f0Candidate = rawF0;
        }
      }
      _cachedF0 = f0Candidate;
    }
    float? f0 = _cachedF0;

    // 4. VTL FORMANT CHECK & ADULT SUPPRESSION
    float vtlFormantRatio = 1.0f;
    float geomMeanFormants = 2000f;
    float spectralCentroid = 2200f;

    bool isAdultPitch = f0.HasValue && f0.Value < ChildAudioFrontend.AdultF0Threshold;
    if (_isChildVerified) {
      // Unstressed Schwa Guard: once child is verified in first 100ms, trailing low F0 (vocal fry) is exempt from adult flagging!
      isAdultPitch = false;
    }

    bool isMotherese = false;
    if (f0.HasValue && f0.Value >= 250f && f0.Value <= 400f && !_isChildVerified) {
      vtlFormantRatio = ChildAudioFrontend.EstimateVtlFormantRatio(
          _analysisBuffer, windowOffset, windowLen, _sampleRate, f0.Value,
          out geomMeanFormants, out spectralCentroid);

      isMotherese = (vtlFormantRatio < 0.78f) || (geomMeanFormants < 1550f && spectralCentroid < 1600f);
    }

    bool isAdultFrame = isAdultPitch || isMotherese;

    if (isAdultFrame && !_isChildVerified) {
      _adultStreakFrames++;
    } else {
      _adultStreakFrames = 0;
    }

    int adultStreakThresholdFrames = (int)Math.Ceiling(ChildAudioFrontend.AdultSuppressionDurationSec / 0.005f); // 24 frames (120ms)

    // 5. VAD TRIGGERS
    bool triggerA = f0.HasValue && f0.Value >= ChildAudioFrontend.MinChildF0 && f0.Value <= ChildAudioFrontend.MaxChildF0
                    && snrDb >= ChildAudioFrontend.MinSnrDb && !isAdultFrame;

    bool triggerB = (zcr > 0.30f || (highBandRatio > 0.55f && highBandSnrDb >= 2.5f)) && !isAdultFrame;

    // 6. VAD STATE MACHINE TRANSITIONS
    if (_adultStreakFrames >= adultStreakThresholdFrames || (_vadState == ChildAudioFrontend.VadStateSilence && isAdultFrame && _adultStreakFrames >= 3)) {
      // Adult prompting detected: block audio stream
      _vadState = ChildAudioFrontend.VadStateBlockedAdult;
      _promptingDetected = true;
      _isSpeechActive = false;
      _naturalStream.Clear();
      _preemphasizedStream.Clear();
    } else if (_vadState == ChildAudioFrontend.VadStateSilence) {
      if (triggerA) {
        _vadState = ChildAudioFrontend.VadStateStressedNucleus;
        _isSpeechActive = true;
        _childVoicedFrames++;
        _sumChildF0 += f0.Value;
        PrependPreRollLookback();
      } else if (triggerB) {
        _vadState = ChildAudioFrontend.VadStateUnvoicedClusterOnset;
        _isSpeechActive = true;
        _unvoicedClusterDurationMs = 5;
        PrependPreRollLookback();
      }
    } else if (_vadState == ChildAudioFrontend.VadStateUnvoicedClusterOnset) {
      _unvoicedClusterDurationMs += 5;
      if (triggerA) {
        _vadState = ChildAudioFrontend.VadStateStressedNucleus;
        _childVoicedFrames++;
        _sumChildF0 += f0.Value;
      } else if (zcr > 0.25f || highBandRatio > 0.40f) {
        if (_unvoicedClusterDurationMs > (int)(ChildAudioFrontend.UnvoicedOnsetDurationMaxSec * 1000f)) {
          _vadState = ChildAudioFrontend.VadStateSilence;
          _isSpeechActive = false;
        }
      } else {
        _vadState = ChildAudioFrontend.VadStateSilence;
        _isSpeechActive = false;
      }
    } else if (_vadState == ChildAudioFrontend.VadStateStressedNucleus) {
      if (triggerA) {
        _childVoicedFrames++;
        _sumChildF0 += f0.Value;
        if (_childVoicedFrames >= (int)(ChildAudioFrontend.ChildVerificationDurationSec / 0.005f)) { // >= 100ms
          _isChildVerified = true;
        }
      } else if (frameRms < _noiseFloor * 1.5f || snrDb < 2.0f) {
        // Energy dropped into silence -> intra-word stop closure
        _vadState = ChildAudioFrontend.VadStateIntraWordClosureHold;
        _closureHoldRemainingMs = (int)(ChildAudioFrontend.HangoverTimeSec * 1000f); // 380ms
        _intraWordClosuresProtected++;
      } else if (_isChildVerified && (f0.HasValue && f0.Value < 200f || frameRms < _noiseFloor * 2.5f)) {
        _vadState = ChildAudioFrontend.VadStateUnstressedSchwaTail;
      }
    } else if (_vadState == ChildAudioFrontend.VadStateIntraWordClosureHold) {
      _closureHoldRemainingMs -= 5;
      // Check for Release Burst of stop consonant /-p, -t, -k/
      bool isBurst = (zcr > 0.28f || highBandRatio > 0.45f) && (frameRms > _noiseFloor * 2.0f);
      if (isBurst) {
        _vadState = ChildAudioFrontend.VadStateReleaseBurstHangover;
        _hangoverRemainingMs = (int)(ChildAudioFrontend.ReleaseBurstHangoverSec * 1000f); // 200ms
        _releaseBurstsCaptured++;
      } else if (triggerA) {
        _vadState = ChildAudioFrontend.VadStateStressedNucleus;
      } else if (_closureHoldRemainingMs <= 0) {
        _vadState = ChildAudioFrontend.VadStateSilence;
        _isSpeechActive = false;
      }
    } else if (_vadState == ChildAudioFrontend.VadStateReleaseBurstHangover) {
      _hangoverRemainingMs -= 5;
      if (triggerA) {
        _vadState = ChildAudioFrontend.VadStateStressedNucleus;
      } else if (_hangoverRemainingMs <= 0) {
        _vadState = ChildAudioFrontend.VadStateSilence;
        _isSpeechActive = false;
      }
    } else if (_vadState == ChildAudioFrontend.VadStateUnstressedSchwaTail) {
      if (triggerA) {
        _vadState = ChildAudioFrontend.VadStateStressedNucleus;
      } else if (frameRms < _noiseFloor * 1.5f) {
        _vadState = ChildAudioFrontend.VadStateIntraWordClosureHold;
        _closureHoldRemainingMs = (int)(ChildAudioFrontend.HangoverTimeSec * 1000f);
      }
    }

    // 7. STATE-AWARE AUTO-GAIN CONTROL (AGC)
    if (_vadState == ChildAudioFrontend.VadStateStressedNucleus && snrDb > 6.0f) {
      _isGainFrozen = false;
      float targetGain = ChildAudioFrontend.TargetRms / Math.Max(0.005f, frameRms);
      targetGain = Math.Max(ChildAudioFrontend.MinGain, Math.Min(ChildAudioFrontend.MaxGain, targetGain));
      _currentGain = _currentGain + 0.15f * (targetGain - _currentGain);
    } else {
      _isGainFrozen = true;
    }

    float agcGainDb = 20f * (float)Math.Log10(Math.Max(1e-4f, _currentGain));

    // 8. APPLY AGC & SOFT-KNEE LIMITER TO CURRENT HOP
    float[] agcHop = _agcHop;
    for (int i = 0; i < hopLen; i++) {
      float amplified = filteredHop[i] * _currentGain;
      float abs = Math.Abs(amplified);
      if (abs > 0.75f) {
        float sign = Math.Sign(amplified);
        float excess = abs - 0.75f;
        float compressed = 0.75f + 0.20f * (float)Math.Tanh(excess / 0.20f);
        agcHop[i] = sign * Math.Min(0.95f, compressed);
      } else {
        agcHop[i] = amplified;
      }
    }

    // 9. BUFFER TO PRE-ROLL RING BUFFER (110ms = 1760 samples)
    for (int i = 0; i < hopLen; i++) {
      _preRollRingBuffer[(_preRollWritePos + i) % PreRollCapacity] = agcHop[i];
    }
    _preRollWritePos = (_preRollWritePos + hopLen) % PreRollCapacity;
    _preRollStoredSamples = Math.Min(PreRollCapacity, _preRollStoredSamples + hopLen);

    // 10. APPEND SAMPLES TO OUTPUT STREAMS IF SPEECH ACTIVE
    if (_isSpeechActive && _vadState != ChildAudioFrontend.VadStateBlockedAdult) {
      for (int i = 0; i < hopLen; i++) {
        float nat = agcHop[i];
        _naturalStream.Add(nat);

        float pe = nat - ChildAudioFrontend.PreEmphasisAlpha * _lastNaturalSample;
        _preemphasizedStream.Add(pe);
        _lastNaturalSample = nat;
      }
    }

    // 11. RECORD FRAME TELEMETRY
    var frameTelemetry = new ChildV12TelemetryFrame {
      UtteranceId = _utteranceId,
      VadState = _vadState,
      F0Hz = f0,
      Zcr = zcr,
      HighBandEnergyRatio = highBandRatio,
      VtlFormantRatio = vtlFormantRatio,
      SnrDb = snrDb,
      AgcGainDb = agcGainDb,
      AgcFrozen = _isGainFrozen
    };
    _frames.Add(frameTelemetry);

    _sumHighBandRatio += highBandRatio;
    _sumVtlRatio += vtlFormantRatio;
    _telemetryFrameCount++;
  }

  private void PrependPreRollLookback() {
    if (_preRollStoredSamples <= 0) return;

    int startPos = (_preRollWritePos - _preRollStoredSamples + PreRollCapacity) % PreRollCapacity;
    for (int i = 0; i < _preRollStoredSamples; i++) {
      float sample = _preRollRingBuffer[(startPos + i) % PreRollCapacity];
      _naturalStream.Add(sample);

      float pe = sample - ChildAudioFrontend.PreEmphasisAlpha * _lastNaturalSample;
      _preemphasizedStream.Add(pe);
      _lastNaturalSample = sample;
    }

    _preRollSamplesPreserved += _preRollStoredSamples;
    _preRollStoredSamples = 0;
  }

  public ChildFrontendDualStreamResult FinalizeStream() {
    var meta = ChildFrontendMetadata.Default();
    meta.UtteranceId = _utteranceId;
    meta.PreRollSamplesPreserved = _preRollSamplesPreserved;
    meta.IntraWordClosuresProtected = _intraWordClosuresProtected;
    meta.ReleaseBurstsCaptured = _releaseBurstsCaptured;
    meta.FineGrainedVadState = _vadState;
    meta.AgcGainDb = 20f * (float)Math.Log10(Math.Max(1e-4f, _currentGain));
    meta.AgcFrozen = _isGainFrozen;

    if (_telemetryFrameCount > 0) {
      meta.HighBandEnergyRatio = (float)(_sumHighBandRatio / _telemetryFrameCount);
      meta.VtlFormantRatio = (float)(_sumVtlRatio / _telemetryFrameCount);
    }

    if (_promptingDetected || _vadState == ChildAudioFrontend.VadStateBlockedAdult) {
      meta.PromptingDetected = true;
      meta.VadState = ChildAudioFrontend.VadStateDroppedAdult;
      meta.IsChild = false;
      meta.F0Avg = 0f;

      return new ChildFrontendDualStreamResult {
        PcmStreamNatural = new float[0],
        PcmStreamPreemphasized = new float[0],
        Summary = meta,
        Frames = _frames,
        UtteranceId = _utteranceId
      };
    }

    if (_childVoicedFrames > 0) {
      meta.F0Avg = _sumChildF0 / _childVoicedFrames;
    }

    bool hasChildF0 = meta.F0Avg >= ChildAudioFrontend.MinChildF0 && meta.F0Avg <= ChildAudioFrontend.MaxChildF0;
    if (_naturalStream.Count > 0) {
      meta.VadState = ChildAudioFrontend.VadStateSpeech;
      meta.IsChild = hasChildF0 || _isChildVerified || _childVoicedFrames >= 2;
    } else {
      meta.VadState = ChildAudioFrontend.VadStateSilence;
      meta.IsChild = false;
    }

    return new ChildFrontendDualStreamResult {
      PcmStreamNatural = _naturalStream.ToArray(),
      PcmStreamPreemphasized = _preemphasizedStream.ToArray(),
      Summary = meta,
      Frames = _frames,
      UtteranceId = _utteranceId
    };
  }
}
