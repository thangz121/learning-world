// _SharedKernel/MicSignal.cs — Lead owns. Mic status-HUD signal language.
// Pure C# (NO UnityEngine): connection quality + data-freshness.
//
// FIX 2026-09-17 — bars ĐO CHẤT LƯỢNG KẾT NỐI, không đo độ to giọng.
// Trước đây bars <- mean-absolute PCM energy nên im lặng => Level.None => 0 bars
// xám bị hiểu nhầm là "mất kết nối". Giờ bars <- transport health (link + freshness),
// dot mới phản ánh DATA flowing, cross <- link DOWN. Energy chỉ còn là telemetry,
// không quyết định số vạch.
//
// Thresholds are DERIVED from observed project numbers, not invented:
//   TooWeak floor 0.005 (SpeechFoundation policy), ambient-room mean 0.0259
//   (P12 real-mic probe — reads Medium, i.e. audible room, not speech),
//   close-mic child speech typically >= 0.05 (Strong). Quiet open mic (~0.002)
//   reads Weak, which is honest: no signal present.
using System;

public enum MicSignalSource {
  None,   // no usable source (crossed bars + red dot)
  Local,  // PC mic/headset (headphone icon + side dot, §req-4)
  Phone   // phone-over-LAN (signal bars + under dot, §req-1/2)
}

public enum SignalLevel {
  None,   // no measurement yet (stale/never) — bars unlit grey
  Weak,   // energy < WeakBelow — red
  Medium, // WeakBelow..StrongAtOrAbove — yellow
  Strong  // >= StrongAtOrAbove — green
}

// One honest frame of "what the mic path looks like right now". Produced by
// MicSetupMonitor (the only writer); rendered by MicStatusHud (read-only).
public struct MicSignalSnapshot {
  public MicSignalSource Source;
  public SignalLevel Level;
  public bool DataFlowing; // dot green (payload bytes fresh) vs red
  public bool LinkUp;      // false => draw the cross slash
  public float Energy;     // smoothed energy driving the bars (0 when stale)
}

public static class MicSignal {
  // Energy bands (kept for legacy telemetry/tests — bars no longer use energy).
  public const float WeakBelow = 0.01f;
  public const float StrongAtOrAbove = 0.05f;
  // DATA window: AUDIO payload seen within the last 3 s => green dot.
  // (Bridge chunks flow ~4/s live; 3 s tolerates Wi-Fi jitter without lying.)
  public const int DataFreshMs = 3000;
  // Connection quality windows (bars measure LINK, not loudness).
  // Idle but recently seen audio keeps bars lit so silence != "disconnected".
  public const int ConnStrongMs = 3000;   // data fresh => 3 bars green
  public const int ConnMediumMs = 15000;  // recent data / UP seen => 2 bars yellow
  // Bar ballistics: peak-hold with linear decay so speech flashes then falls.
  public const float DecayPerSec = 0.06f;

  // Pure level mapping (tests pin the boundaries; NaN/negative => None).
  // Legacy voice-energy mapping — kept for tests, not for HUD bars.
  public static SignalLevel ComputeLevel(float energy) {
    if (float.IsNaN(energy) || energy < 0f) return SignalLevel.None;
    if (energy <= 0f) return SignalLevel.None;
    if (energy < WeakBelow) return SignalLevel.Weak;
    if (energy < StrongAtOrAbove) return SignalLevel.Medium;
    return SignalLevel.Strong;
  }

  // Connection-quality level (bars): measures LINK health, not loudness.
  // linkUp false => None (crossed). True + age fresh => Strong, recent => Medium, else Weak (still connected).
  // Never (ageMs <0) stays None (grey, not red) — matches P16M: no measurement yet => unlit, not weak.
  public static SignalLevel ComputeConnectionLevel(int ageMs, bool linkUp) {
    if (!linkUp) return SignalLevel.None;
    if (ageMs < 0) return SignalLevel.None;
    if (ageMs <= ConnStrongMs) return SignalLevel.Strong;
    if (ageMs <= ConnMediumMs) return SignalLevel.Medium;
    return SignalLevel.Weak;
  }

  // Pure data-flow test: payload observed recently (ageMs<0 = never).
  public static bool IsDataFlowing(int ageMs) {
    return ageMs >= 0 && ageMs <= DataFreshMs;
  }

  // Pure peak-hold decay: jumps to target instantly, falls linearly.
  // dtSec <= 0 holds current (never NaN, never negative).
  public static float ApplyDecay(float current, float target, float dtSec) {
    if (float.IsNaN(current) || current < 0f) current = 0f;
    if (float.IsNaN(target) || target < 0f) target = 0f;
    if (target >= current) return target;
    if (dtSec <= 0f) return current;
    float next = current - DecayPerSec * dtSec;
    return next < target ? target : next;
  }

  // Pure bar language: lit count + color + crossed. Level.None with link up
  // = idle (0 lit, grey); link down = crossed regardless of level.
  public static void ComputeBars(SignalLevel level, bool linkUp,
      out int lit, out MicBarColor color, out bool crossed) {
    crossed = !linkUp;
    if (crossed || level == SignalLevel.None) {
      lit = 0;
      color = MicBarColor.Grey;
      return;
    }
    switch (level) {
      case SignalLevel.Weak: lit = 1; color = MicBarColor.Red; break;
      case SignalLevel.Medium: lit = 2; color = MicBarColor.Yellow; break;
      default: lit = 3; color = MicBarColor.Green; break;
    }
  }
}

public enum MicBarColor {
  Grey,   // idle / no measurement
  Red,    // weak
  Yellow, // medium
  Green   // strong
}
