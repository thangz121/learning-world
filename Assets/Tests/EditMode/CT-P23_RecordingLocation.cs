// CT-P23: Phase 2.3d save-location chooser — deterministic suite.
// Where-to-save is asked in-game (animated panel), remembered in prefs
// (explicit dir OR remembered-default sentinel), double-F2 re-opens it;
// the native OS picker is never invoked headless (SuppressForTests) — its
// live behavior is proven in the user E2E run.
using NUnit.Framework;
using System;
using System.IO;
using System.Threading;

public class CT_P23_RecordingLocation {
  static string TempDir(string tag) {
    string d = Path.Combine(Path.GetTempPath(), tag + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(d);
    return d;
  }

  static void WipeDir(string d) {
    try { if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) Directory.Delete(d, true); }
    catch (Exception) { }
  }

  static MediaRecordingService NewService(string dir, string prefsKey) {
    var go = new UnityEngine.GameObject("MediaRecTest23");
    var rec = go.AddComponent<MediaRecordingService>();
    rec.Bind(MediaRecordingConfig.Default, null, null,
      (Func<PhonePresenceWatcher>)(() => (PhonePresenceWatcher)null), dir);
    rec.SetTestForceSourcesAvailable(true);
    rec.SetTestDisableTranscode(true);
    rec.SetPrefsKeyForTests(prefsKey);
    return rec;
  }

  static void KillService(MediaRecordingService rec) {
    try { if (rec != null) UnityEngine.Object.DestroyImmediate(rec.gameObject); }
    catch (Exception) { }
  }

  static bool WaitTerminal(MediaRecordingService rec, int timeoutMs) {
    int waited = 0;
    while (rec.CurrentState == RecordingState.Stopping && waited < timeoutMs) {
      try { rec.PumpForTests(); } catch (Exception) { }
      Thread.Sleep(50);
      waited += 50;
    }
    var s = rec.CurrentState;
    return s == RecordingState.Completed || s == RecordingState.Error;
  }

  static byte[] TonePcm16(int samples, float level) {
    var b = new byte[samples * 2];
    short v = (short)(level * 32767f);
    for (int i = 0; i < samples; i++) {
      b[i * 2] = (byte)(v & 0xFF);
      b[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
    }
    return b;
  }

  [Test] public void P23A_PrefsRoundtrip() {
    string dir = TempDir("LWE-P23-");
    MediaRecordingService rec = null;
    string key = "LWE.Test.P23A." + Guid.NewGuid().ToString("N");
    try {
      rec = NewService(dir, key);
      Assert.IsFalse(rec.HasChosenOutput(), "no choice remembered yet");
      string err;
      Assert.IsTrue(rec.TrySetOutputDir(dir, out err), err);
      Assert.IsTrue(rec.HasChosenOutput());
      Assert.AreEqual(dir, rec.SavedOutputDir());
      Assert.IsTrue(rec.CurrentOutputDisplay().Contains(dir));
      // Choosing the default ALSO remembers (sentinel): asks exactly once.
      rec.UseDefaultLocation();
      Assert.IsTrue(rec.HasChosenOutput(), "remembered-default still counts as chosen");
      Assert.IsNull(rec.SavedOutputDir(), "sentinel carries no dir");
      Assert.IsTrue(rec.CurrentOutputDisplay().Contains("c định"));
      Assert.IsTrue(rec.EnsureOutputReady(out err), "remembered default is usable: " + err);
    } finally {
      KillService(rec);
      try { UnityEngine.PlayerPrefs.DeleteKey(key); } catch (Exception) { }
      WipeDir(dir);
    }
  }

  [Test] public void P23B_RejectsBadDirsLoudly() {
    string dir = TempDir("LWE-P23-");
    MediaRecordingService rec = null;
    string key = "LWE.Test.P23B." + Guid.NewGuid().ToString("N");
    try {
      rec = NewService(dir, key);
      string err;
      string probe = Path.Combine(dir, "file.txt");
      File.WriteAllText(probe, "x");
      Assert.IsFalse(rec.TrySetOutputDir(probe, out err), "a file is not a dir");
      Assert.IsNotEmpty(err);
      Assert.IsFalse(rec.HasChosenOutput(), "rejection saves nothing");
      Assert.IsFalse(rec.TrySetOutputDir(Path.Combine(dir, "nope"), out err));
      Assert.IsNotEmpty(err);
      Assert.IsFalse(rec.TrySetOutputDir(null, out err));
      Assert.IsFalse(rec.TrySetOutputDir(string.Empty, out err));
    } finally {
      KillService(rec);
      try { UnityEngine.PlayerPrefs.DeleteKey(key); } catch (Exception) { }
      WipeDir(dir);
    }
  }

  [Test] public void P23C_EnsureOutputReadyTransitions() {
    string dir = TempDir("LWE-P23-");
    MediaRecordingService rec = null;
    string key = "LWE.Test.P23C." + Guid.NewGuid().ToString("N");
    try {
      rec = NewService(dir, key);
      string err;
      Assert.IsFalse(rec.EnsureOutputReady(out err), "first F2 must ask");
      Assert.AreEqual("no-saved-location", err);
      Assert.IsTrue(rec.TrySetOutputDir(dir, out err), err);
      Assert.IsTrue(rec.EnsureOutputReady(out err), err);
      Directory.Delete(dir, true);
      Assert.IsFalse(rec.EnsureOutputReady(out err), "unplugged dir re-asks");
      Assert.IsNotEmpty(err);
    } finally {
      KillService(rec);
      try { UnityEngine.PlayerPrefs.DeleteKey(key); } catch (Exception) { }
      WipeDir(dir);
    }
  }

  [Test] public void P23D_NativePickerSuppressedHeadless() {
    bool old = NativeFolderDialog.SuppressForTests;
    NativeFolderDialog.SuppressForTests = true;
    try {
      string path;
      Assert.IsFalse(NativeFolderDialog.TryPickFolder("test", out path),
        "never opens OS UI headless");
      Assert.IsNull(path);
    } finally {
      NativeFolderDialog.SuppressForTests = old;
    }
  }

  [Test] public void P23E_DialogStates() {
    var go = new UnityEngine.GameObject("RecLocDlgTest");
    RecordingLocationDialog dlg = null;
    try {
      dlg = go.AddComponent<RecordingLocationDialog>();
      dlg.BuildUiImmediate();
      Assert.IsFalse(dlg.IsShowing);
      bool def = false, browse = false, cancel = false;
      dlg.ShowChooser("C:\\vid", null,
        () => { def = true; }, () => { browse = true; }, () => { cancel = true; });
      Assert.IsTrue(dlg.IsShowing);
      dlg.SetError("thử lỗi");
      dlg.ShowChooser("C:\\vid", "lỗi cũ", () => { }, () => { }, () => { });
      Assert.IsTrue(dlg.IsShowing);
      Assert.IsFalse(def);
      Assert.IsFalse(browse);
      Assert.IsFalse(cancel);
      dlg.Hide();
      Assert.IsFalse(dlg.IsShowing);
    } finally {
      try { UnityEngine.Object.DestroyImmediate(go); } catch (Exception) { }
    }
  }

  [Test] public void P23F_RememberedDirWinsOverDefault() {
    string dirA = TempDir("LWE-P23A-");
    string dirB = TempDir("LWE-P23B-");
    MediaRecordingService rec = null;
    string key = "LWE.Test.P23F." + Guid.NewGuid().ToString("N");
    try {
      rec = NewService(dirA, key);
      string err;
      Assert.IsTrue(rec.TrySetOutputDir(dirB, out err), err);
      Assert.IsTrue(rec.StartRecording(RecordingMode.MicOnly), rec.LastError);
      Assert.IsTrue(rec.TestEnqueueAudio(1, TonePcm16(1600, 0.25f)));
      Assert.IsTrue(rec.StopRecording());
      Assert.IsTrue(WaitTerminal(rec, 15000), "state=" + rec.CurrentState);
      Assert.AreEqual(RecordingState.Completed, rec.CurrentState);
      Assert.IsTrue(rec.AudioPath.StartsWith(dirB), rec.AudioPath + " vs " + dirB);
      Assert.AreEqual(0, Directory.GetFiles(dirA, "*.wav").Length, "default untouched");
    } finally {
      KillService(rec);
      try { UnityEngine.PlayerPrefs.DeleteKey(key); } catch (Exception) { }
      WipeDir(dirA);
      WipeDir(dirB);
    }
  }

  [Test] public void P23G_F2DoubleTracker() {
    var t = new F2DoubleTracker();
    // No remembered choice: first F2 asks immediately (no hold).
    Assert.AreEqual(F2PressResult.OpenChooser, t.Press(10f, false));
    // Remembered: single press arms, hold expiry fires the start.
    Assert.AreEqual(F2PressResult.None, t.Press(10f, true));
    Assert.IsFalse(t.PollStart(10.2f), "hold not expired yet");
    Assert.IsTrue(t.PollStart(10.6f), "hold expired -> start");
    Assert.IsFalse(t.PollStart(11f), "fires exactly once");
    // Double press inside the window opens the chooser instead.
    Assert.AreEqual(F2PressResult.None, t.Press(20f, true));
    Assert.AreEqual(F2PressResult.OpenChooser, t.Press(20.2f, true));
    Assert.IsFalse(t.PollStart(21f), "change won, no start fires");
    // Stale arm behaves like a fresh single press.
    Assert.AreEqual(F2PressResult.None, t.Press(30f, true));
    Assert.IsTrue(t.PollStart(31f));
    // Cancel disarms.
    Assert.AreEqual(F2PressResult.None, t.Press(40f, true));
    t.Cancel();
    Assert.IsFalse(t.PollStart(41f));
  }

  [Test] public void P23H_ToastShowsAndHides() {
    var go = new UnityEngine.GameObject("RecToastTest");
    RecordingToast toast = null;
    try {
      toast = go.AddComponent<RecordingToast>();
      toast.BuildUiImmediate();
      Assert.IsFalse(toast.IsShowing);
      toast.Show("Đã lưu xong:\nsession.mp4\ntại C:\\vid", 5f);
      Assert.IsTrue(toast.IsShowing);
      toast.TestAdvance(4.9f);
      Assert.IsTrue(toast.IsShowing, "still within duration");
      toast.TestAdvance(0.2f);
      Assert.IsFalse(toast.IsShowing, "auto-hides");
      toast.Show("x", 5f);
      toast.Hide();
      Assert.IsFalse(toast.IsShowing);
    } finally {
      try { UnityEngine.Object.DestroyImmediate(go); } catch (Exception) { }
    }
  }

  [Test] public void P23I_ToastNeverEatsClicks() {
    var go = new UnityEngine.GameObject("RecToastTest2");
    RecordingToast toast = null;
    try {
      toast = go.AddComponent<RecordingToast>();
      toast.BuildUiImmediate();
      Assert.IsNull(go.GetComponent<UnityEngine.UI.GraphicRaycaster>(),
        "non-modal: no raycaster on the toast canvas");
      foreach (var g in go.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) {
        Assert.IsFalse(g.raycastTarget, "clicks pass through: " + g.name);
      }
      toast.Show("x", 5f);
      Assert.IsTrue(toast.IsShowing);
    } finally {
      try { UnityEngine.Object.DestroyImmediate(go); } catch (Exception) { }
    }
  }

  [Test] public void P23J_DecideStartMatrix() {
    // Full media -> start; audio missing + video present -> explicit
    // proposal (never silent fallback); nothing usable -> refuse (the
    // Start gates then voice the exact reason).
    Assert.AreEqual(StartDecision.StartAsRequested,
      RecordingStartDecider.DecideStart(true, true, RecordingMode.MicAndCamera));
    Assert.AreEqual(StartDecision.ProposeVideoOnly,
      RecordingStartDecider.DecideStart(false, true, RecordingMode.MicAndCamera));
    Assert.AreEqual(StartDecision.RefuseNoMedia,
      RecordingStartDecider.DecideStart(false, false, RecordingMode.MicAndCamera));
    Assert.AreEqual(StartDecision.RefuseNoMedia,
      RecordingStartDecider.DecideStart(true, false, RecordingMode.MicAndCamera));
    Assert.AreEqual(StartDecision.StartAsRequested,
      RecordingStartDecider.DecideStart(true, false, RecordingMode.MicOnly));
    Assert.AreEqual(StartDecision.RefuseNoMedia,
      RecordingStartDecider.DecideStart(false, true, RecordingMode.MicOnly));
    Assert.AreEqual(StartDecision.StartAsRequested,
      RecordingStartDecider.DecideStart(false, true, RecordingMode.CameraOnly));
    Assert.AreEqual(StartDecision.RefuseNoMedia,
      RecordingStartDecider.DecideStart(false, false, RecordingMode.CameraOnly));
  }

  [Test] public void P23K_AudioSourceStates() {
    string dir = TempDir("LWE-P23-");
    MediaRecordingService rec = null;
    string key = "LWE.Test.P23K." + Guid.NewGuid().ToString("N");
    try {
      rec = NewService(dir, key);
      rec.SetTestForceSourcesAvailable(false); // no phone in this rig
      Assert.AreEqual(AudioSourceState.None, rec.GetAudioSourceState());
      rec.SetLocalMicForTests(true); // laptop mic listed, phone absent
      Assert.AreEqual(AudioSourceState.LocalOnly, rec.GetAudioSourceState());
      rec.SetLocalMicForTests(false);
      Assert.AreEqual(AudioSourceState.None, rec.GetAudioSourceState());
      rec.SetTestForceSourcesAvailable(true); // phone flowing wins
      rec.SetLocalMicForTests(true);
      Assert.AreEqual(AudioSourceState.PhoneReady, rec.GetAudioSourceState());
    } finally {
      KillService(rec);
      try { UnityEngine.PlayerPrefs.DeleteKey(key); } catch (Exception) { }
      WipeDir(dir);
    }
  }

  [Test] public void P23L_ConfirmDialogStates() {
    var go = new UnityEngine.GameObject("RecConfirmTest");
    RecordingConfirmDialog dlg = null;
    try {
      dlg = go.AddComponent<RecordingConfirmDialog>();
      dlg.BuildUiImmediate();
      Assert.IsFalse(dlg.IsShowing);
      bool ok = false, cancel = false;
      dlg.ShowConfirm("T", "B", "OK!", "Back", () => { ok = true; }, () => { cancel = true; });
      Assert.IsTrue(dlg.IsShowing);
      Assert.IsFalse(ok);
      Assert.IsFalse(cancel);
      dlg.Hide();
      Assert.IsFalse(dlg.IsShowing);
    } finally {
      try { UnityEngine.Object.DestroyImmediate(go); } catch (Exception) { }
    }
  }

  [Test] public void P23M_SmartPathRefusesLoudly() {
    string dir = TempDir("LWE-P23-");
    MediaRecordingService rec = null;
    string key = "LWE.Test.P23M." + Guid.NewGuid().ToString("N");
    try {
      rec = NewService(dir, key);
      rec.SetTestForceSourcesAvailable(false); // no phone, no cameras bound
      rec.StartRecordingSmart(); // no confirm dialog bound: falls to gates
      Assert.AreEqual(RecordingState.Error, rec.CurrentState);
      Assert.IsTrue(rec.LastError.Contains("no-audio-in-game"), rec.LastError);
    } finally {
      KillService(rec);
      try { UnityEngine.PlayerPrefs.DeleteKey(key); } catch (Exception) { }
      WipeDir(dir);
    }
  }

  [Test] public void P23N_SmartStartsFullSessionWhenReady() {
    string dir = TempDir("LWE-P23-");
    MediaRecordingService rec = null;
    string key = "LWE.Test.P23N." + Guid.NewGuid().ToString("N");
    try {
      rec = NewService(dir, key);
      rec.SetTestForceSourcesAvailable(true); // phone flowing + cameras pass
      rec.StartRecordingSmart();
      Assert.AreEqual(RecordingState.Recording, rec.CurrentState, rec.LastError);
      Assert.IsTrue(rec.StopRecording());
    } finally {
      KillService(rec);
      try { UnityEngine.PlayerPrefs.DeleteKey(key); } catch (Exception) { }
      WipeDir(dir);
    }
  }
}
