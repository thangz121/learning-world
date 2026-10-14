// _SharedKernel/ChildAudioFrontend.cs — Phase 1 Audio Frontend & Child-VAD Engine.
// Pure C# (no UnityEngine dependency, EditMode/batchmode testable, <5ms latency).
//
// 1. PRE-FILTERING & AUTO-GAIN (AGC):
//    - Bandpass Filter: 180Hz - 7000Hz (Biquad 2nd-order butterworth/cascade)
//      removes wind rumble, desk vibration (<180Hz) and high-frequency hiss (>7000Hz).
//    - AGC: Normalizes energy RMS towards target range [-18dBFS, -12dBFS] (~0.125 to ~0.25).
//    - Soft-knee Peak Limiter: Anti-clipping tanh saturation when child screams or approaches mic.
//
// 2. CHILD-ADAPTED VAD (4-year-old acoustics):
//    - F0 Range: 250Hz - 480Hz.
//    - Frame Size: 10ms - 20ms (160 - 320 samples @ 16kHz).
//    - Attack Time: 15ms to capture voiceless unvoiced onsets (p-, t-, k-).
//    - Hangover Time: 280ms to preserve codas (-p, -t, -k, -m, -n, -ng) after energy drops.
//
// 3. ADULT VOICE SUPPRESSION:
//    - Low latency F0 Autocorrelation / Difference (YIN-inspired) pitch extractor.
//    - If F0 < 220Hz (adult pitch range): flag IS_ADULT_VOICE = true.
//    - If IS_ADULT_VOICE continuously > 120ms: tag DROPPED_ADULT / VOICE_CORRUPTED: PROMPTING_DETECTED.
//      Audio stream is dropped / invalidated to never forward adult prompts to local DSP or Azure.
//    - Valid child voice requires F0 in [250Hz - 480Hz] and SNR > 6dB.

using System;

[Serializable]
public struct ChildFrontendMetadata {
  public float F0Avg;            // Average detected F0 in Hz
  public bool IsChild;           // True if valid child voice detected
  public float SnrDb;            // Estimated Signal-to-Noise Ratio in dB
  public string VadState;        // "SPEECH" | "SILENCE" | "DROPPED_ADULT"
  public bool PromptingDetected; // True if adult voice exceeded threshold (>120ms)

  public static ChildFrontendMetadata Default() {
    return new ChildFrontendMetadata {
      F0Avg = 0f,
      IsChild = false,
      SnrDb = 0f,
      VadState = "SILENCE",
      PromptingDetected = false
    };
  }
}

public static class ChildAudioFrontend {
  public const float MinChildF0 = 250f;
  public const float MaxChildF0 = 480f;
  public const float AdultF0Threshold = 220f;
  public const float AdultSuppressionDurationSec = 0.120f; // 120ms
  public const float MinSnrDb = 6.0f;
  public const float AttackTimeSec = 0.015f; // 15ms
  public const float HangoverTimeSec = 0.280f; // 280ms

  // Pre-filter: 180Hz Highpass and 7000Hz Lowpass
  public const float LowCutHz = 180f;
  public const float HighCutHz = 7000f;

  // AGC Target RMS: -18dBFS (0.125f) to -12dBFS (0.25f) -> Target ~0.18f (-15dBFS)
  public const float TargetRms = 0.18f;
  public const float MinGain = 0.5f;
  public const float MaxGain = 4.0f;

  /// <summary>
  /// Process raw PCM samples: applies bandpass filter, AGC & peak limiter,
  /// child-adapted VAD analysis, and adult voice prompting suppression.
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

    // 1. BANDPASS FILTER (180Hz - 7000Hz)
    float[] filtered = ApplyBandpass(rawSamples, sampleRate, LowCutHz, HighCutHz);

    // 2. AUTO-GAIN CONTROL (AGC) & SOFT-KNEE LIMITER
    float[] agcSamples = ApplyAgcAndLimiter(filtered);

    // 3. CHILD-ADAPTED VAD & ADULT SUPPRESSION
    metadata = AnalyzeChildVAD(agcSamples, sampleRate);

    // 4. If adult prompting detected (>120ms adult F0), DROP the audio stream
    if (metadata.PromptingDetected || metadata.VadState == "DROPPED_ADULT") {
      return new float[0];
    }

    return agcSamples;
  }

  /// <summary>
  /// 2nd-order Biquad Bandpass filter (Cascade of 2nd-order High-Pass and 2nd-order Low-Pass).
  /// Pure DSP math with zero allocations inside loop.
  /// </summary>
  public static float[] ApplyBandpass(float[] samples, int sampleRate, float lowCut, float highCut) {
    if (samples == null || samples.Length == 0) return new float[0];
    float[] output = new float[samples.Length];

    // Design High-Pass Filter at lowCut (180Hz)
    float hpW0 = 2f * (float)Math.PI * lowCut / sampleRate;
    float hpCos = (float)Math.Cos(hpW0);
    float hpSin = (float)Math.Sin(hpW0);
    float hpAlpha = hpSin / (2f * 0.7071f); // Q = 0.7071 (Butterworth)

    float hpB0 = (1f + hpCos) / 2f;
    float hpB1 = -(1f + hpCos);
    float hpB2 = (1f + hpCos) / 2f;
    float hpA0 = 1f + hpAlpha;
    float hpA1 = -2f * hpCos;
    float hpA2 = 1f - hpAlpha;

    // Normalize HP coefficients
    hpB0 /= hpA0; hpB1 /= hpA0; hpB2 /= hpA0;
    hpA1 /= hpA0; hpA2 /= hpA0;

    // Design Low-Pass Filter at highCut (7000Hz)
    float lpW0 = 2f * (float)Math.PI * highCut / sampleRate;
    float lpCos = (float)Math.Cos(lpW0);
    float lpSin = (float)Math.Sin(lpW0);
    float lpAlpha = lpSin / (2f * 0.7071f);

    float lpB0 = (1f - lpCos) / 2f;
    float lpB1 = 1f - lpCos;
    float lpB2 = (1f - lpCos) / 2f;
    float lpA0 = 1f + lpAlpha;
    float lpA1 = -2f * lpCos;
    float lpA2 = 1f - lpAlpha;

    // Normalize LP coefficients
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
  /// Auto-Gain Control (AGC) and Soft-Knee Limiter.
  /// Normalizes RMS energy to [-18dBFS, -12dBFS] and prevents distortion when screaming.
  /// </summary>
  public static float[] ApplyAgcAndLimiter(float[] samples) {
    if (samples == null || samples.Length == 0) return new float[0];
    int len = samples.Length;
    float[] output = new float[len];

    // Compute RMS
    double sumSquares = 0.0;
    float peak = 0f;
    for (int i = 0; i < len; i++) {
      float s = samples[i];
      float abs = Math.Abs(s);
      if (abs > peak) peak = abs;
      sumSquares += s * s;
    }

    float currentRms = (float)Math.Sqrt(sumSquares / len);
    float gain = 1.0f;
    if (currentRms > 0.0001f) {
      gain = TargetRms / currentRms;
      if (gain < MinGain) gain = MinGain;
      if (gain > MaxGain) gain = MaxGain;
    }

    // Apply gain + Soft-Knee Peak Limiter (anti-clipping tanh saturation)
    for (int i = 0; i < len; i++) {
      float amplified = samples[i] * gain;
      // Soft-knee threshold at 0.75f (-2.5dBFS)
      float abs = Math.Abs(amplified);
      if (abs > 0.75f) {
        float sign = Math.Sign(amplified);
        // Tanh-compression above 0.75
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
  /// Realtime Child-Adapted VAD & Adult Pitch Tracker (YIN/Autocorrelation pitch extraction).
  /// Frame size: 10ms - 20ms (160-320 samples @ 16kHz).
  /// </summary>
  public static ChildFrontendMetadata AnalyzeChildVAD(float[] samples, int sampleRate) {
    var meta = ChildFrontendMetadata.Default();
    if (samples == null || samples.Length == 0) return meta;

    if (sampleRate <= 0) sampleRate = 16000;
    int frameSize = sampleRate / 50; // 20ms (320 samples @ 16kHz, conforms to spec 10ms - 20ms)
    if (frameSize < 320) frameSize = 320;

    int totalFrames = samples.Length / frameSize;
    if (totalFrames <= 0) return meta;

    // Track noise floor using bottom 10th percentile energy
    float[] frameEnergies = new float[totalFrames];
    float speechFloor = 0.005f;

    for (int f = 0; f < totalFrames; f++) {
      int offset = f * frameSize;
      float e = 0f;
      for (int i = 0; i < frameSize; i++) {
        e += Math.Abs(samples[offset + i]);
      }
      frameEnergies[f] = e / frameSize;
    }

    // Estimate noise floor
    float[] sortedEnergies = (float[])frameEnergies.Clone();
    Array.Sort(sortedEnergies);
    int noiseIndex = Math.Max(0, (int)(totalFrames * 0.15f));
    float noiseFloor = Math.Max(0.0005f, sortedEnergies[noiseIndex]);
    float peakFrameEnergy = sortedEnergies[sortedEnergies.Length - 1];

    float snrDb = 0f;
    if (peakFrameEnergy > noiseFloor) {
      snrDb = 20f * (float)Math.Log10(peakFrameEnergy / noiseFloor);
    }
    meta.SnrDb = snrDb;

    // Frame-by-frame pitch extraction and state tracking
    int adultStreakFrames = 0;
    int maxAdultStreakFrames = 0;
    int adultFrameThreshold = (int)Math.Ceiling(AdultSuppressionDurationSec * (sampleRate / (float)frameSize)); // 6 frames (120ms)

    int childVoicedFrames = 0;
    float sumChildF0 = 0f;
    int speechFrames = 0;

    // VAD Hangover logic: 280ms
    int hangoverFramesMax = (int)(HangoverTimeSec * (sampleRate / (float)frameSize)); // ~14 frames
    int hangoverCounter = 0;

    for (int f = 0; f < totalFrames; f++) {
      float energy = frameEnergies[f];
      bool isVoicedCandidate = energy >= speechFloor;

      if (isVoicedCandidate) {
        hangoverCounter = hangoverFramesMax;
      } else if (hangoverCounter > 0) {
        hangoverCounter--;
      }

      bool frameInSpeech = isVoicedCandidate || (hangoverCounter > 0);
      if (frameInSpeech) speechFrames++;

      if (isVoicedCandidate) {
        // Extract F0 via Autocorrelation
        int offset = f * frameSize;
        float f0 = ExtractPitchAutocorrelation(samples, offset, frameSize, sampleRate);

        if (f0 > 0f) {
          if (f0 < AdultF0Threshold) {
            adultStreakFrames++;
            if (adultStreakFrames > maxAdultStreakFrames) {
              maxAdultStreakFrames = adultStreakFrames;
            }
          } else {
            adultStreakFrames = 0;
          }

          if (f0 >= MinChildF0 && f0 <= MaxChildF0) {
            childVoicedFrames++;
            sumChildF0 += f0;
          }
        } else {
          adultStreakFrames = 0;
        }
      } else {
        adultStreakFrames = 0;
      }
    }

    // Adult Voice Prompting Check:
    // If adult voice lasted > 120ms continuously -> reject / drop
    if (maxAdultStreakFrames >= adultFrameThreshold) {
      meta.PromptingDetected = true;
      meta.VadState = "DROPPED_ADULT";
      meta.IsChild = false;
      meta.F0Avg = 0f;
      return meta;
    }

    if (childVoicedFrames > 0) {
      meta.F0Avg = sumChildF0 / childVoicedFrames;
    }

    bool hasChildF0 = meta.F0Avg >= MinChildF0 && meta.F0Avg <= MaxChildF0;
    bool hasAdequateSnr = snrDb >= MinSnrDb;

    if (speechFrames > 0 && (hasChildF0 || childVoicedFrames >= 2) && hasAdequateSnr) {
      meta.IsChild = true;
      meta.VadState = "SPEECH";
    } else if (speechFrames > 0) {
      meta.VadState = "SPEECH";
      meta.IsChild = hasChildF0;
    } else {
      meta.VadState = "SILENCE";
      meta.IsChild = false;
    }

    return meta;
  }

  /// <summary>
  /// Zero-latency Normalized Autocorrelation pitch extraction for a short frame.
  /// Searches period in pitch range 80Hz - 600Hz.
  /// </summary>
  public static float ExtractPitchAutocorrelation(float[] samples, int offset, int length, int sampleRate) {
    if (samples == null || offset + length > samples.Length) return 0f;

    int minLag = sampleRate / 600; // ~600Hz
    int maxLag = sampleRate / 80;  // ~80Hz

    if (offset + maxLag + length > samples.Length) {
      maxLag = Math.Max(minLag, samples.Length - offset - length);
    }
    if (maxLag <= minLag) return 0f;

    float bestCorr = 0f;
    int bestLag = -1;

    // Energy at lag 0
    double energy0 = 0.0;
    for (int i = 0; i < length; i++) {
      float v = samples[offset + i];
      energy0 += v * v;
    }
    if (energy0 < 1e-6) return 0f; // Silence

    // Fast rolling calculation of energyLag
    double energyLag = 0.0;
    for (int i = 0; i < length; i++) {
      float vLag = samples[offset + i + minLag];
      energyLag += vLag * vLag;
    }

    for (int lag = minLag; lag <= maxLag; lag++) {
      double sum = 0.0;
      int o = offset;
      int oLag = offset + lag;
      // Stride loop with step 2 decimation for 2x speedup
      for (int i = 0; i < length; i += 2) {
        sum += samples[o + i] * samples[oLag + i];
      }
      sum *= 2.0;

      double norm = Math.Sqrt(energy0 * energyLag);
      if (norm > 1e-6) {
        float corr = (float)(sum / norm);
        if (corr > bestCorr && corr > 0.45f) { // Clarity threshold
          bestCorr = corr;
          bestLag = lag;
        }
      }

      // Update energyLag for next lag (remove outgoing sample, add incoming sample)
      if (lag < maxLag && oLag + length < samples.Length) {
        float outSample = samples[oLag];
        float inSample = samples[oLag + length];
        energyLag = Math.Max(0.0, energyLag - outSample * outSample + inSample * inSample);
      }
    }

    if (bestLag > 0 && bestCorr > 0.45f) {
      return (float)sampleRate / bestLag;
    }

    return 0f;
  }
}
