// _SharedKernel/RecAudioMixer.cs — Lead owns. Phase 2.5 recording audio mix.
//
// Pure C# (NO UnityEngine): mixes ONE mic chunk (canonical mono PCM16LE
// @16 kHz) with the overlapping game-audio window (same format, pulled from
// the GameAudioTap FIFO) into a single PCM16LE @16 kHz chunk of the SAME
// length as the mic input. Output feeds the existing bounded audio queue,
// WAV writer, MP3 and MP4 audio untouched downstream.
//
// Why here: the exported video had voice-only audio (game TTS/dialogue never
// reached the file — player report 2026-09-17), and the mic sat too quiet in
// the mix (same report). MicGain (default 2.0) + GameGain (default 1.0) with
// a hard clamp — never throws, never allocates beyond the output buffer.
using System;

public static class RecAudioMixer {
  public const float DefaultMicGain = 2.0f;
  public const float DefaultGameGain = 1.0f;
  public const float MaxGain = 8.0f;

  // Mixes micPcm16 with gamePcm16 (may be null/short: zero-padded; may be
  // long: truncated). Returns a buffer of micPcm16.Length (or empty on bad
  // input, in which case the caller keeps prior behavior of dropping).
  public static byte[] MixMicWithGame(byte[] micPcm16, byte[] gamePcm16,
      float micGain, float gameGain) {
    try {
      if (micPcm16 == null || micPcm16.Length < 2) return new byte[0];
      int samples = micPcm16.Length / 2;
      float mg = SanitizeGain(micGain, DefaultMicGain);
      float gg = SanitizeGain(gameGain, DefaultGameGain);
      int gameSamples = gamePcm16 != null ? gamePcm16.Length / 2 : 0;
      var out_ = new byte[samples * 2];
      for (int i = 0; i < samples; i++) {
        float mic = (float)Decode16(micPcm16, i) / 32768f;
        float game = 0f;
        if (i < gameSamples) game = (float)Decode16(gamePcm16, i) / 32768f;
        float v = mic * mg + game * gg;
        if (v > 1f) v = 1f;
        else if (v < -1f) v = -1f;
        short s = (short)(v * 32767f);
        out_[i * 2] = (byte)(s & 0xFF);
        out_[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
      }
      return out_;
    } catch (Exception) { return new byte[0]; }
  }

  public static float SanitizeGain(float gain, float fallback) {
    try {
      if (float.IsNaN(gain) || float.IsInfinity(gain)) return fallback;
      if (gain < 0f) return 0f;
      if (gain > MaxGain) return MaxGain;
      return gain;
    } catch (Exception) { return fallback; }
  }

  static short Decode16(byte[] buf, int sample) {
    int lo = buf[sample * 2];
    int hi = buf[sample * 2 + 1];
    return (short)(lo | (hi << 8));
  }
}
