// CT-P50: S3-P2L2 DEMO AUDIENCE GATE (user order).
// The counting-garden lesson used to act AND speak its whole script from the
// moment the scene loaded, looping its voice forever. Pinned contract now:
//   * no audience  -> no acting, no speech, stage idle (garden = playground);
//   * the child walking to the viewing spot starts ONE clean pass;
//   * the pass never auto-replays (leaving + returning re-arms it);
//   * the child leaving cuts the voice immediately (Director Mute) and resets
//     the stage to idle.
// C# 9.0 only.
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CT_P50_DemoAudienceGate {
  sealed class FakeAudio : IAudioDirector {
    public readonly List<string> Lines = new List<string>();
    public readonly List<AudioFocusMode> Focus = new List<AudioFocusMode>();
    public Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode mode) { return Task.CompletedTask; }
    public Task SpeakAsync(DialogueRequest request) { Lines.Add(request.Text); return Task.CompletedTask; }
    public void PlaySfx(SfxId id) { }
    public void PlayMusic(MusicId id) { }
    public void SetAudioFocus(AudioFocusMode mode) { Focus.Add(mode); }
  }

  CountingGardenBuilder _builder;
  CountingDemo _demo;
  FakeAudio _audio;
  GameObject _garden;
  Transform _viewer;

  void SetUp(bool withViewer) {
    _garden = new GameObject("P50GardenWorld");
    _builder = _garden.AddComponent<CountingGardenBuilder>();
    _builder.BuildContent(_garden.transform);
    _audio = new FakeAudio();
    _demo = _garden.AddComponent<CountingDemo>();
    GameObject viewer = new GameObject("P50Audience");
    viewer.transform.SetParent(_garden.transform, true);
    viewer.transform.position = withViewer
      ? CountingGardenBuilder.WorldOffset + _builder.DemoMouth
      : CountingGardenBuilder.WorldOffset + new Vector3(0f, 0f, -8f); // at the entry
    _viewer = viewer.transform;
    _demo.Build(_builder, _viewer, null, _audio);
  }

  void TearDown() {
    if (_garden != null) UnityEngine.Object.DestroyImmediate(_garden);
  }

  void Steps(int n) { for (int i = 0; i < n; i++) _demo.Step(0.1f); }

  // A. No audience: silence + idle stage, however long we wait.
  [Test] public void P50A_NoAudienceNoLesson() {
    SetUp(false);
    try {
      Steps(1200); // ~2 minutes of standing at the entry
      Assert.AreEqual(0, _audio.Lines.Count, "no speech without an audience");
      Assert.AreEqual(0, _demo.LoopCount, "the lesson never ran");
      Assert.AreEqual(DemoPhase.Ready, _demo.Phase, "stage stays idle");
      Assert.IsFalse(_demo.ResultShown, "no result without a lesson");
      foreach (string line in _audio.Lines)
        Assert.Fail("unexpected line spoken: " + line);
    } finally { TearDown(); }
  }

  // B. Audience arrives: one clean pass, then silence (no auto-replay).
  [Test] public void P50B_OnePassThenSilence() {
    SetUp(true);
    try {
      int guard = 0;
      while (_demo.LoopCount < 1 && guard < 4000) { _demo.Step(0.1f); guard++; }
      Assert.GreaterOrEqual(_demo.LoopCount, 1, "the audience runs the lesson once");
      int linesAfterPass = _audio.Lines.Count;
      Assert.Greater(linesAfterPass, 0, "the lesson spoke");
      Steps(600); // the child keeps standing there for another minute
      Assert.AreEqual(1, _demo.LoopCount, "no automatic replay");
      Assert.AreEqual(linesAfterPass, _audio.Lines.Count, "no further speech for a held audience");
    } finally { TearDown(); }
  }

  // C. The child leaves mid-pass: voice cut + stage reset; coming back replays.
  [Test] public void P50C_LeavingCutsVoiceAndResets() {
    SetUp(true);
    try {
      Steps(60); // walk in, lesson starts
      Assert.Greater(_audio.Lines.Count, 0, "lesson started");
      // Leave the area (beyond the abort radius).
      _viewer.position = CountingGardenBuilder.WorldOffset + new Vector3(0f, 0f, -8f);
      Steps(20);
      Assert.Contains(AudioFocusMode.Muted, _audio.Focus, "voice stopped when the child left");
      Assert.AreEqual(DemoPhase.Ready, _demo.Phase, "stage back to idle");
      Assert.IsFalse(_demo.ResultShown, "stage reset (no result left behind)");
      float ballSpread = 0f;
      for (int i = 0; i < _builder.DemoBalls.Count; i++) {
        Vector3 home = CountingGardenBuilder.DemoBallHomes[i];
        Vector3 p = _builder.DemoBalls[i].transform.localPosition;
        ballSpread += Mathf.Abs(p.x - home.x) + Mathf.Abs(p.z - home.z);
      }
      Assert.Less(ballSpread, 0.5f, "balls back on the field (playground state)");
      // Return: the pass can run again.
      _viewer.position = CountingGardenBuilder.WorldOffset + _builder.DemoMouth;
      int guard = 0;
      while (_demo.LoopCount < 1 && guard < 4000) { _demo.Step(0.1f); guard++; }
      Assert.GreaterOrEqual(_demo.LoopCount, 1, "returning re-arms the lesson");
    } finally { TearDown(); }
  }
}
