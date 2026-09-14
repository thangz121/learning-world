// D_Audio/PregenManifestIndex.cs — Phase 2D (audio pipeline, ADDITIVE).
// Read-only index over the shipped pregen mirror
// (Assets/StreamingAssets/audio/manifest.json, written by tools/pregen_w1.py
// from Content/audio_manifest.json). Answers, without throwing and without
// touching playback:
//   "does dialogue/vocab id X have a shipped binary, and where?"
// Used by tests today (CT-P08) and available to the 2F debug/diagnostics
// surface for OBSERVABLE missing-audio reporting (§12/§20: missing audio
// must never be silent-swallowable — this is the lookup half; the player
// half already warns in AudioDirector). Playback resolution itself stays on
// the L2 cache-key path (AudioCache + PregenSeeder); this index never
// participates in it, so it cannot change what plays.
// C# 9.0 only. File IO isolated in two static entry points.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class PregenManifestIndex {
  [Serializable]
  public sealed class Entry {
    public string id;
    public string text;
    public string voice;
    public string lang;
    public float rate;
    public float pitch;
    public string style;
    public string format;
    public string file;
  }

  [Serializable]
  sealed class Mirror {
    public Entry[] entries;
  }

  // Loads the shipped mirror. Returns empty (never null, never throws) when
  // the mirror is absent/unparseable — callers treat that as "no binaries".
  public static List<Entry> LoadStreamingMirror() {
    var list = new List<Entry>();
    try {
      string path = Path.Combine(Application.streamingAssetsPath, "audio", "manifest.json");
      if (!File.Exists(path)) return list;
      var mirror = JsonUtility.FromJson<Mirror>(File.ReadAllText(path));
      if (mirror == null || mirror.entries == null) return list;
      foreach (Entry e in mirror.entries) {
        if (e == null || string.IsNullOrEmpty(e.id)) continue;
        list.Add(e);
      }
    } catch (Exception) {
      // Absent/unreadable mirror reads as empty (offline/editor-safe).
    }
    return list;
  }

  // Binary presence for one manifest entry (plain filename under audio/).
  public static bool HasBinary(Entry e) {
    if (e == null || string.IsNullOrEmpty(e.file)) return false;
    try {
      if (Path.GetFileName(e.file) != e.file) return false;
      string path = Path.Combine(Application.streamingAssetsPath, "audio", e.file);
      return File.Exists(path) && new FileInfo(path).Length > 0;
    } catch (Exception) {
      return false;
    }
  }

  // The runtime cache key this entry WILL hit when requested with these
  // exact params (AudioCache.CacheKey). Lets tests prove manifest params
  // match runtime request params without any network or audio hardware.
  public static string CacheKeyFor(Entry e) {
    if (e == null) return "";
    SpeechStyle style;
    try {
      style = (SpeechStyle)Enum.Parse(typeof(SpeechStyle), e.style);
    } catch (Exception) {
      return "";
    }
    AudioFormat format;
    try {
      format = (AudioFormat)Enum.Parse(typeof(AudioFormat), e.format);
    } catch (Exception) {
      return "";
    }
    return AudioCache.CacheKey(e.text ?? "",
      new VoiceProfileId(e.voice ?? ""), new LanguageCode(e.lang ?? ""),
      e.rate, e.pitch, style, format);
  }
}
