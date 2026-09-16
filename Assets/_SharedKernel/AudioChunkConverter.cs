// _SharedKernel/AudioChunkConverter.cs — Lead owns. Phase 2.3e local-mic
// bridge: interleaved float samples (ANY device channels/rate) -> canonical
// mono PCM16LE @ 16 kHz bytes that the existing bounded audio queue, pump,
// WAV writer, and verifier already understand. Downmix = channel average;
// resample = linear interpolation. Pure C# (NO UnityEngine), never throws
// (empty array on bad input), unit-tested with synthetic vectors — headless
// rigs have no microphones, so hardware behavior is proven in the user run.
using System;

public static class AudioChunkConverter {
  public const int TargetRate = 16000;

  public static byte[] ToMono16(float[] interleaved, int channels, int inRate) {
    return ToMono16(interleaved, channels, inRate, TargetRate);
  }

  public static byte[] ToMono16(float[] interleaved, int channels, int inRate, int outRate) {
    try {
      if (interleaved == null || interleaved.Length == 0) return new byte[0];
      if (channels < 1 || channels > 64) return new byte[0]; // garbage geometry: reject, never adapt
      if (channels > 8) channels = 8;
      if (inRate < 1000 || inRate > 192000) return new byte[0];
      if (outRate < 1000 || outRate > 192000) return new byte[0];
      int inFrames = interleaved.Length / channels;
      if (inFrames <= 0) return new byte[0];
      // Mono mixdown first (average keeps voice level honest).
      float[] mono = interleaved;
      if (channels > 1) {
        mono = new float[inFrames];
        for (int i = 0; i < inFrames; i++) {
          double sum = 0;
          int n = 0;
          for (int c = 0; c < channels; c++) {
            int idx = i * channels + c;
            if (idx < interleaved.Length) {
              sum += interleaved[idx];
              n++;
            }
          }
          mono[i] = n > 0 ? (float)(sum / n) : 0f;
        }
      } else if (interleaved.Length != inFrames) {
        var tmp = new float[inFrames];
        Array.Copy(interleaved, tmp, inFrames);
        mono = tmp;
      }
      // Resample (linear) only when the device refused our requested rate.
      float[] out_;
      if (inRate == outRate) {
        out_ = mono;
      } else {
        int outFrames = Math.Max(1, (int)((long)inFrames * outRate / inRate));
        out_ = new float[outFrames];
        for (int i = 0; i < outFrames; i++) {
          double pos = (double)i * inFrames / outFrames;
          int a = (int)pos;
          if (a < 0) a = 0;
          if (a >= inFrames - 1) {
            out_[i] = mono[inFrames - 1];
          } else {
            double frac = pos - a;
            out_[i] = (float)(mono[a] * (1.0 - frac) + mono[a + 1] * frac);
          }
        }
      }
      var bytes = new byte[out_.Length * 2];
      for (int i = 0; i < out_.Length; i++) {
        float v = out_[i];
        if (v > 1f) v = 1f;
        else if (v < -1f) v = -1f;
        short s = (short)(v * 32767f);
        bytes[i * 2] = (byte)(s & 0xFF);
        bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
      }
      return bytes;
    } catch (Exception) { return new byte[0]; }
  }
}
