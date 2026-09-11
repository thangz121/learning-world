// CT-A02: P1 playing ducks music 20% + ambient 40%, no second voice. Owner: Agent D (W0-T1 GREEN).
// Sync half: interruption table + duck ratios. Async half: a P3 arriving mid-P1 is queued,
// never a concurrent second voice, and drains after P1.
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class CT_A02_FocusDucking {
  sealed class CTA02_StubTts : ISpeechSynthesisProvider {
    public Task<TtsAudioResult> SynthesizeAsync(TtsRequest request, CancellationToken ct) {
      return Task.FromResult(new TtsAudioResult {
        Mp3 = new byte[] { 7, 7, 7 }, CacheKey = "cta02", FromCache = false,
      });
    }
  }

  sealed class CTA02_BlockingDirector : AudioDirector {
    public int Plays;
    public readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>();
    public readonly TaskCompletionSource<bool> Released = new TaskCompletionSource<bool>();
    static AudioClip _clip;
    public CTA02_BlockingDirector(IGameEventBus bus, ISpeechSynthesisProvider tts)
      : base(bus, tts) { }
    protected override Task<AudioClip> DecodeAudioAsync(
        byte[] mp3, string cacheKey, CancellationToken ct) {
      if (_clip == null) _clip = AudioClip.Create("cta02", 100, 1, 44100, false);
      return Task.FromResult(_clip);
    }
    protected override async Task PlayClipAsync(
        AudioClip clip, AudioPriority priority, CancellationToken ct) {
      Plays++;
      Started.TrySetResult(true);
      await Released.Task.ConfigureAwait(false);
      ct.ThrowIfCancellationRequested();
    }
    protected override void StoreCaches(string key, AudioClip clip, byte[] mp3) { }
  }

  static DialogueRequest Req(string text, string voice, AudioPriority prio) {
    return new DialogueRequest(text, new VoiceProfileId(voice), new LanguageCode("en-US"),
      0.85f, 0f, SpeechStyle.Clear, AudioFormat.Mp3_44100, prio);
  }

  static async Task<bool> WaitUntilAsync(System.Func<bool> cond, int timeoutMs) {
    var sw = System.Diagnostics.Stopwatch.StartNew();
    while (!cond()) {
      if (sw.ElapsedMilliseconds > timeoutMs) return false;
      await Task.Delay(20).ConfigureAwait(false);
    }
    return true;
  }

  [Test] public async Task CT_A02() {
    // Interruption table (AUDIO_DESIGN §6 freeze).
    Assert.AreEqual(AudioInterruption.PauseResume,
      AudioDirector.ResolveInterruption(AudioPriority.P1_Pronunciation, AudioPriority.P3_Dialogue));
    Assert.AreEqual(AudioInterruption.Queue,
      AudioDirector.ResolveInterruption(AudioPriority.P2_Instruction, AudioPriority.P3_Dialogue));
    Assert.AreEqual(AudioInterruption.Ignore,
      AudioDirector.ResolveInterruption(AudioPriority.P4_Feedback, AudioPriority.P1_Pronunciation));
    Assert.AreEqual(AudioInterruption.Ignore,
      AudioDirector.ResolveInterruption(AudioPriority.P4_Feedback, AudioPriority.P2_Instruction));
    Assert.AreEqual(AudioInterruption.Queue,
      AudioDirector.ResolveInterruption(AudioPriority.P3_Dialogue, AudioPriority.P1_Pronunciation));
    Assert.AreEqual(AudioInterruption.Interrupt,
      AudioDirector.ResolveInterruption(AudioPriority.P0_Safety, AudioPriority.P1_Pronunciation));

    var dir = new CTA02_BlockingDirector(new GameEventBus(), new CTA02_StubTts());

    // Duck ratios while P1 owns the voice channel.
    dir.ApplyDucking(AudioPriority.P1_Pronunciation);
    Assert.AreEqual(0.20f, dir.MusicDuckRatio, 1e-6f, "music ducks 20% during P1");
    Assert.AreEqual(0.40f, dir.AmbientDuckRatio, 1e-6f, "ambient ducks 40% during P1");
    dir.ClearDucking();

    // One voice at a time: P3 during P1 queues, never plays concurrently.
    Task p1 = dir.SpeakAsync(Req("Apple", "learning_v1", AudioPriority.P1_Pronunciation));
    Assert.IsTrue(await WaitUntilAsync(() => dir.Plays >= 1, 2000), "P1 must start playing");
    Assert.AreEqual(AudioPriority.P1_Pronunciation, dir.CurrentVoicePriority);
    Assert.AreEqual(0.20f, dir.MusicDuckRatio, 1e-6f);
    Assert.AreEqual(0.40f, dir.AmbientDuckRatio, 1e-6f);

    Task p3 = dir.SpeakAsync(Req("Hi! Come here!", "npc_female_01", AudioPriority.P3_Dialogue));
    await Task.Delay(150).ConfigureAwait(false);
    Assert.AreEqual(1, dir.Plays, "no second voice may start while P1 plays");
    Assert.AreEqual(1, dir.QueuedCount, "P3 waits in FIFO queue");

    dir.Released.TrySetResult(true);
    await Task.WhenAny(Task.WhenAll(p1, p3), Task.Delay(5000)).ConfigureAwait(false);
    Assert.IsTrue(p1.IsCompleted && p3.IsCompleted, "both flights must finish after release");
    Assert.AreEqual(2, dir.Plays, "queued P3 drains after P1");
    Assert.IsNull(dir.CurrentVoicePriority, "voice channel idle at end");
    Assert.AreEqual(0f, dir.MusicDuckRatio, 1e-6f, "duck cleared at end");
  }
}
