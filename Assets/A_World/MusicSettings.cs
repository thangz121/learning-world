// A_World/MusicSettings.cs — USER ROUND 2026-09-29: the theme tune starts
// when the game opens, and a parent-facing chip in the top-right corner (next
// to the language chip) turns it on/off. The choice persists in LocalSave
// (PlayerProgress.MusicOn) exactly like the language. C# 9.0 only.
using System;

public static class MusicSettings {
  public static bool On { get; private set; } = true;

  static IAudioDirector _audio;

  // Boot hook (GameInstaller): load the saved choice.
  public static void Init(PlayerProgress progress) {
    try { if (progress != null) On = progress.MusicOn; } catch (Exception) { }
  }

  // Boot hook (GameInstaller): the live director takes the choice now (and
  // starts the loop when ON).
  public static void Bind(IAudioDirector audio) {
    _audio = audio;
    Apply();
  }

  public static void Apply() {
    try { if (_audio != null) _audio.SetMusicEnabled(On); } catch (Exception) { }
  }

  public static void Toggle() {
    On = !On;
    Apply();
    Persist();
  }

  public static void Persist() {
    try {
      var save = new LocalSave();
      PlayerProgress p = save.Load();
      if (p == null) p = new PlayerProgress();
      p.MusicOn = On;
      save.Save(p);
    } catch (Exception) { }
  }

  // Test seam (no refs, no file IO).
  public static void SetForTests(bool on) { On = on; }
}
