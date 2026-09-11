// D_Audio/AudioCache.cs — Agent D (W0-T1). L1 memory + L2 disk cache.
// Key = SHA256(text|voice|locale|rate|pitch|style|format), lowercase hex (AUDIO_DESIGN §7).
// Priority is NOT part of the key: same line at P1 or P3 is the same audio.
// L2 root: Application.persistentDataPath/audio, filename <key>.mp3.
// W0 note: L1 holds a clip only when a decode succeeded (see AudioDirector.DecodeAudioAsync);
// L2 bytes are always stored. Dedupe window (2s, anti "apple apple" spam) is timestamp-based.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public sealed class AudioCache {
  public const double DedupeWindowSec = 2.0;
  const string DiskSubdir = "audio";

  readonly Dictionary<string, AudioClip> _memory = new Dictionary<string, AudioClip>();
  readonly Dictionary<string, DateTime> _lastRequestUtc = new Dictionary<string, DateTime>();
  readonly object _gate = new object();

  public static string CacheKey(string text, VoiceProfileId voice, LanguageCode lang,
      float rate, float pitch, SpeechStyle style, AudioFormat format) {
    string norm = string.Join("|",
      text ?? "",
      (voice.Value ?? "").ToLowerInvariant(),
      lang.Value ?? "",
      rate.ToString("G9", CultureInfo.InvariantCulture),
      pitch.ToString("G9", CultureInfo.InvariantCulture),
      style.ToString(),
      format.ToString());
    using (SHA256 sha = SHA256.Create()) {
      byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(norm));
      var sb = new StringBuilder(64);
      for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
      return sb.ToString();
    }
  }

  public static string DiskPathFor(string key) {
    return Path.Combine(Application.persistentDataPath, DiskSubdir, key + ".mp3");
  }

  public bool TryGetMemory(string key, out AudioClip clip) {
    lock (_gate) {
      if (_memory.TryGetValue(key, out clip) && clip != null) return true;
      clip = null;
      return false;
    }
  }

  public bool TryGetDisk(string key, out byte[] mp3) {
    mp3 = null;
    try {
      string path = DiskPathFor(key);
      if (!File.Exists(path)) return false;
      byte[] bytes = File.ReadAllBytes(path);
      if (bytes == null || bytes.Length == 0) return false;
      mp3 = bytes;
      return true;
    } catch (Exception) {
      mp3 = null;
      return false;
    }
  }

  // Null-tolerant: null clip skips L1, null/empty bytes skip L2.
  public void Store(string key, AudioClip clip, byte[] mp3) {
    if (string.IsNullOrEmpty(key)) return;
    if (clip != null) {
      lock (_gate) { _memory[key] = clip; }
    }
    if (mp3 != null && mp3.Length > 0) {
      try {
        string path = DiskPathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, mp3);
      } catch (Exception e) {
        Debug.LogWarning("[AudioCache] L2 write failed for key " + key + ": " + e.Message);
      }
    }
  }

  // Anti-spam: same key requested again within the window -> true (caller skips).
  // Records the attempt timestamp on first sight.
  public bool IsDuplicateWithinWindow(string key, double windowSec = DedupeWindowSec) {
    DateTime now = DateTime.UtcNow;
    lock (_gate) {
      if (_lastRequestUtc.TryGetValue(key, out DateTime prev)
          && (now - prev).TotalSeconds < windowSec) {
        return true;
      }
      _lastRequestUtc[key] = now;
      return false;
    }
  }

  public void Forget(string key) {
    lock (_gate) { _lastRequestUtc.Remove(key); }
  }

  public void ClearMemory() {
    lock (_gate) { _memory.Clear(); }
  }
}
