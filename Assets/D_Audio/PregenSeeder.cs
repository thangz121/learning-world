// D_Audio/PregenSeeder.cs — Agent D (W1). Offline L2 seeder, no gameplay code.
// Copies pre-generated W1 masters from StreamingAssets/audio/<file> (shipped by
// tools/pregen_w1.py) into the AudioCache L2 slot (persistentDataPath/audio/<key>.mp3)
// so first launch works fully offline. Keys are computed ONLY here in C# via
// AudioCache.CacheKey — python never hashes (key-miss safety).
// Wiring (Lead): call PregenSeeder.SeedFromStreamingAssets() from GameInstaller.Awake
// BEFORE `new AudioDirector(...)`. NEVER throws: catch-all -> 0 + Debug.LogWarning.
// C# 9.0 only. Unity deps: Application + JsonUtility + Debug (+ System.IO).
using System;
using System.IO;
using UnityEngine;

public static class PregenSeeder {
  [Serializable] sealed class PregenManifestEntry {
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

  [Serializable] sealed class PregenManifest {
    public PregenManifestEntry[] entries;
  }

  public static int SeedFromStreamingAssets() {
    try {
      string manifestPath = Path.Combine(Application.streamingAssetsPath, "audio", "manifest.json");
      if (!File.Exists(manifestPath)) {
        Debug.LogWarning("[PregenSeeder] No StreamingAssets/audio/manifest.json, seeded 0.");
        return 0;
      }
      var manifest = JsonUtility.FromJson<PregenManifest>(File.ReadAllText(manifestPath));
      if (manifest == null || manifest.entries == null) {
        Debug.LogWarning("[PregenSeeder] Unparseable manifest, seeded 0.");
        return 0;
      }
      int seeded = 0;
      for (int i = 0; i < manifest.entries.Length; i++) {
        try {
          PregenManifestEntry e = manifest.entries[i];
          if (e == null || e.text == null || string.IsNullOrEmpty(e.file)) continue;
          SpeechStyle style;
          try {
            style = (SpeechStyle)Enum.Parse(typeof(SpeechStyle), e.style);
          } catch (Exception) {
            continue; // unknown style -> skip entry, keep seeding the rest
          }
          AudioFormat format;
          try {
            format = (AudioFormat)Enum.Parse(typeof(AudioFormat), e.format);
          } catch (Exception) {
            continue; // unknown format -> skip entry
          }
          string key = AudioCache.CacheKey(e.text,
            new VoiceProfileId(e.voice ?? ""), new LanguageCode(e.lang ?? ""),
            e.rate, e.pitch, style, format);
          string src = Path.Combine(Application.streamingAssetsPath, "audio", e.file);
          if (!File.Exists(src)) continue;
          string dst = AudioCache.DiskPathFor(key);
          bool needCopy = true;
          if (File.Exists(dst)) {
            needCopy = new FileInfo(src).Length != new FileInfo(dst).Length;
          }
          if (needCopy) {
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(src, dst, true);
            seeded++;
          }
        } catch (Exception ex) {
          Debug.LogWarning("[PregenSeeder] Skipped entry: " + ex.Message);
        }
      }
      // Single boot info line (not spam): proves offline L2 state at startup,
      // which decides whether first-launch audio needs the network at all.
      Debug.Log("[PregenSeeder] Seeded " + seeded + " pregen masters to L2.");
      return seeded;
    } catch (Exception ex) {
      Debug.LogWarning("[PregenSeeder] Seed failed (offline L2 untouched): " + ex.Message);
      return 0;
    }
  }
}
