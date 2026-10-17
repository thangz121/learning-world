// Assets/Tests/PlayMode/ArenaSmokeTests.cs - PlayMode REAL-LOOP arena smoke (code-driven, no window/input).
// Unlike the EditMode pins (which drain controller.Tick(0.1) manually), these run the ACTUAL
// player loop: Update() + real Time.deltaTime + WaitForSeconds + Destroy - catching the timing /
// coroutine / double-tick class of bugs EditMode cannot see. Batchmode-safe (headless, no input).
using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ArenaSmokeTests {
  sealed class FakeAudio : IAudioDirector {
    public Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode mode) { return Task.CompletedTask; }
    public Task SpeakAsync(DialogueRequest request) { return Task.CompletedTask; }
    public void PlaySfx(SfxId id) { }
    public void PlayMusic(MusicId id) { }
    public void SetMusicEnabled(bool on) { }
    public void SetAudioFocus(AudioFocusMode mode) { }
  }

  static ListeningGardenBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("SmokeListeningWorld");
    ListeningGardenBuilder builder = arena.AddComponent<ListeningGardenBuilder>();
    builder.BuildContent(arena.transform);
    return builder;
  }

  static ListeningGardenChoice AnswerOf(ListeningGardenBuilder b) {
    for (int i = 0; i < b.Choices.Count; i++) {
      if (b.Choices[i] != null && b.Choices[i].IsAnswer) return b.Choices[i];
    }
    return null;
  }

  static ListeningGardenChoice WrongOf(ListeningGardenBuilder b) {
    for (int i = 0; i < b.Choices.Count; i++) {
      if (b.Choices[i] != null && !b.Choices[i].IsAnswer) return b.Choices[i];
    }
    return null;
  }

  // Tap every 0.4s until done() or timeout - robust across intro/guiding/replay phases
  // (tap guards ignore taps in wrong phases, so retrying is harmless).
  static IEnumerator RetryTapUntil(ListeningGarden game, ListeningGardenChoice pod, Func<bool> done, float timeoutSec) {
    float t = 0f;
    while (t < timeoutSec && !done()) {
      if (pod != null) game.TryTapChoice(pod);
      yield return new WaitForSeconds(0.4f);
      t += 0.4f;
    }
  }

  [UnityTest] public IEnumerator Smoke_Listening_WrongThenCorrect_RealLoop() {
    ListeningGardenBuilder builder = BuildArena(out GameObject arena);
    try {
      var audio = new FakeAudio();
      var life = new ActivityLifecycle("listen_garden", "playmode-smoke");
      ListeningGarden game = arena.AddComponent<ListeningGarden>();
      game.Build(builder, null, null, audio, life);

      // Let the real loop settle (intro / optional guiding)
      yield return new WaitForSeconds(2f);

      ListeningGardenChoice wrong = WrongOf(builder);
      Assert.IsNotNull(wrong, "a wrong pod exists");
      yield return RetryTapUntil(game, wrong, () => game.WrongCount >= 1, 25f);
      Assert.GreaterOrEqual(game.WrongCount, 1, "wrong tap was registered in the real loop");
      Assert.IsFalse(game.LastCorrect, "no false win from a wrong tap");

      ListeningGardenChoice ans = AnswerOf(builder);
      Assert.IsNotNull(ans, "answer pod exists");
      yield return RetryTapUntil(game, ans, () => game.LastCorrect, 25f);
      Assert.IsTrue(game.LastCorrect, "correct tap wins the round");
      Assert.GreaterOrEqual(game.RoundIndex, 1, "round advanced");
    } finally {
      UnityEngine.Object.Destroy(arena);
    }
  }

  [UnityTest] public IEnumerator Smoke_Listening_IdleRealLoop_NoExceptions() {
    ListeningGardenBuilder builder = BuildArena(out GameObject arena);
    try {
      ListeningGarden game = arena.AddComponent<ListeningGarden>();
      game.Build(builder, null, null, new FakeAudio(), new ActivityLifecycle("listen_garden", "playmode-idle"));

      for (int i = 0; i < 180; i++) yield return null; // real Update() frames, no manual Tick
      Assert.IsFalse(game.Completed, "no accidental completion during an idle loop");
    } finally {
      UnityEngine.Object.Destroy(arena);
    }
  }
}
