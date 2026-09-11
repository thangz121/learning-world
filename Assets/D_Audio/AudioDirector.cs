// D_Audio/AudioDirector.cs — Agent D (W0-T1). Owns ALL voice output.
// Constructor (IGameEventBus, ISpeechSynthesisProvider) — wired ONLY by GameInstaller.
// Priority gate P0>...>P7 (AUDIO_DESIGN §6 freeze):
//   P0 arrives            -> Interrupt all, play now.
//   P1 arrives over P3    -> fade-out 150ms stub + PauseResume P3 after P1.
//   P2 arrives over P3    -> Queue behind current P3 (no cut-in).
//   P4 arrives over P1/P2 -> Ignore (feedback waits, anti-spam).
//   P5-7 arrives over speech -> Duck (music -20% / ambient -40% log stub), queued behind.
//   Same priority         -> Queue FIFO, max 3, oldest dropped + log when over.
//   Duplicate text+voice within 2s -> dedupe, skip (anti "apple apple" spam).
// SafetyFilter word-count runs BEFORE every Speak path; rejected lines never hit TTS.
// One AudioSource for voice (lazy). Music/SFX/Focus beyond the voice channel are
// honest state+log stubs in W0 (full Mixer in W1). Pre-gen Addressables hook is
// TryLoadPregenAsync (returns L2 re-check in W0, real Addressables override in W1).
// Playback backends are protected virtual seams (Synthesize/Decode/Play/Pregen) so
// W1 hooks in without touching the gate, and tests observe without real audio.
// Realtime TTS failure NEVER blocks learning core: fallback pre-gen attempt, then
// silent drop + log (WorkerTtsContract error table).
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class AudioDirector : IAudioDirector {
  // Freeze values (AUDIO_DESIGN §6).
  public const float MusicDuckRatioP1 = 0.20f;
  public const float AmbientDuckRatioP1 = 0.40f;
  public const int MaxQueued = 3;
  public const double QueueTimeoutSec = 5.0;
  public const string DefaultLang = "en-US";
  public const float VocabRateNormal = 0.85f; // learning_v1 normal (§3) -> worker "normal"
  public const float VocabRateSlow = 0.70f;   // worker "slow" is runtime fallback only;
                                              // master slow assets use time-stretch in W1 (§5b)

  static readonly VoiceProfileId LearningVoice = new VoiceProfileId("learning_v1");
  static readonly LanguageCode FrozenLang = new LanguageCode("en-US");

  readonly IGameEventBus _bus;
  readonly ISpeechSynthesisProvider _tts;
  readonly AudioCache _cache = new AudioCache();

  readonly object _gate = new object();
  readonly SemaphoreSlim _playSlot = new SemaphoreSlim(1, 1);
  readonly HashSet<string> _inflight = new HashSet<string>();
  readonly Queue<SpeechJob> _queue = new Queue<SpeechJob>();
  CancellationTokenSource _currentCts;
  AudioPriority? _current;
  string _currentKey;
  SpeechJob _activeJob;
  SpeechJob _pausedResume; // P3 paused by P1, replayed after P1 finishes

  GameObject _voiceRoot;
  AudioSource _voice;

  public float MusicDuckRatio { get; private set; }
  public float AmbientDuckRatio { get; private set; }
  public AudioFocusMode Focus { get; private set; }
  public string LastSfxId { get; private set; }
  public string CurrentMusicId { get; private set; }

  public AudioPriority? CurrentVoicePriority {
    get { lock (_gate) { return _current; } }
  }

  public bool IsVoicePlaying {
    get { lock (_gate) { return _current.HasValue; } }
  }

  public int QueuedCount {
    get { lock (_gate) { return _queue.Count; } }
  }

  public AudioDirector(IGameEventBus bus, ISpeechSynthesisProvider tts) {
    _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    _tts = tts ?? throw new ArgumentNullException(nameof(tts));
    Focus = AudioFocusMode.Learning;
  }

  // Pure gate function (unit-testable): what does `incoming` do to `current`?
  public static AudioInterruption ResolveInterruption(AudioPriority incoming, AudioPriority? current) {
    if (current == null) return AudioInterruption.Interrupt; // idle voice channel
    if (incoming == current) return AudioInterruption.Queue; // same priority: FIFO
    if (incoming < current) { // higher priority (lower number)
      if (incoming == AudioPriority.P0_Safety) return AudioInterruption.Interrupt;
      if (incoming == AudioPriority.P1_Pronunciation && current == AudioPriority.P3_Dialogue)
        return AudioInterruption.PauseResume; // fade stub, P3 resumes after P1
      if (incoming == AudioPriority.P2_Instruction && current == AudioPriority.P3_Dialogue)
        return AudioInterruption.Queue; // instruction never cuts dialogue
      return AudioInterruption.Interrupt;
    }
    // Lower priority than current.
    if ((current == AudioPriority.P1_Pronunciation || current == AudioPriority.P2_Instruction)
        && incoming == AudioPriority.P4_Feedback)
      return AudioInterruption.Ignore; // feedback waits, anti-spam
    if (incoming >= AudioPriority.P5_Sfx) return AudioInterruption.Duck; // duck under speech
    return AudioInterruption.Queue;
  }

  public async Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode mode) {
    string text = ResolveVocabText(wordId);
    float rate = mode == VocabularyAudioMode.Normal ? VocabRateNormal : VocabRateSlow;
    var tts = new TtsRequest(text, LearningVoice, FrozenLang, rate, 0f,
      SpeechStyle.Clear, AudioFormat.Mp3_44100, AudioPriority.P1_Pronunciation);
    var dialog = new DialogueRequest(text, LearningVoice, FrozenLang, rate, 0f,
      SpeechStyle.Clear, AudioFormat.Mp3_44100, AudioPriority.P1_Pronunciation);
    var job = SpeechJob.ForVocabulary(wordId, mode, tts, dialog);
    await SubmitAsync(job).ConfigureAwait(false);
  }

  public async Task SpeakAsync(DialogueRequest request) {
    var lang = new LanguageCode(
      string.IsNullOrEmpty(request.Lang.Value) ? DefaultLang : request.Lang.Value);
    var tts = new TtsRequest(request.Text, request.Voice, lang, request.Rate,
      request.Pitch, request.Style, request.Format, request.Priority);
    var job = SpeechJob.ForDialogue(request, tts);
    await SubmitAsync(job).ConfigureAwait(false);
  }

  public void PlaySfx(SfxId id) {
    LastSfxId = id.Value;
    if (IsVoicePlaying) {
      Debug.Log("[AudioDirector] SFX '" + id.Value + "' ducked under speech "
        + CurrentVoicePriority + " (unrelated SFX off during P1-P4).");
      return;
    }
    Debug.Log("[AudioDirector] SFX play (W0 stub, no mixer yet): " + id.Value);
  }

  public void PlayMusic(MusicId id) {
    CurrentMusicId = id.Value;
    Debug.Log("[AudioDirector] Music play (W0 stub, crossfade in W1): " + id.Value);
  }

  public void SetAudioFocus(AudioFocusMode mode) {
    Focus = mode;
    if (mode == AudioFocusMode.Muted) StopAll();
    else Debug.Log("[AudioDirector] Focus -> " + mode);
  }

  // Scene-unload entry point (Lead/A wires SceneManager.sceneUnloaded -> here in W1):
  // stops all P1-P4 voice, cancels pending, clears duck. Music crossfade is W1.
  public void StopAll() {
    CancelCurrent();
    lock (_gate) {
      while (_queue.Count > 0) {
        var dropped = _queue.Dequeue();
        dropped.Done.TrySetResult(false);
      }
      _pausedResume = null;
    }
    ClearDucking();
    Debug.Log("[AudioDirector] StopAll: voice stopped, queue cleared.");
  }

  // ---- submission gate ----

  async Task SubmitAsync(SpeechJob job) {
    job.Key = AudioCache.CacheKey(job.Tts.Text, job.Tts.Voice, job.Tts.Lang,
      job.Tts.Rate, job.Tts.Pitch, job.Tts.Style, job.Tts.Format);
    job.EnqueuedUtc = DateTime.UtcNow;

    bool isMilo = (job.Tts.Voice.Value ?? "") == "milo_v1";
    if (!SafetyFilter.ValidateLine(job.Tts.Text, isMilo, out string reason)) {
      Debug.LogWarning("[AudioDirector] SafetyFilter rejected (" + reason + "), TTS skipped: '"
        + job.Tts.Text + "'");
      job.Done.TrySetResult(false);
      return;
    }

    if (_cache.IsDuplicateWithinWindow(job.Key)) {
      Debug.Log("[AudioDirector] Dedupe: same text+voice within 2s, skipped: '"
        + job.Tts.Text + "'");
      job.Done.TrySetResult(false);
      return;
    }

    AudioPriority? current;
    lock (_gate) { current = _current; }
    AudioInterruption action = ResolveInterruption(job.Tts.Priority, current);

    if (action == AudioInterruption.Ignore) {
      Debug.Log("[AudioDirector] Ignored " + job.Tts.Priority + " during " + current
        + " (feedback waits).");
      job.Done.TrySetResult(false);
      return;
    }
    if (action == AudioInterruption.Queue || action == AudioInterruption.Duck) {
      if (action == AudioInterruption.Duck) {
        Debug.Log("[AudioDirector] Duck: " + job.Tts.Priority
          + " queued behind speech (music -20%, ambient -40%).");
      }
      lock (_gate) {
        if (_queue.Count >= MaxQueued) {
          var dropped = _queue.Dequeue();
          dropped.Done.TrySetResult(false);
          Debug.LogWarning("[AudioDirector] Queue full (max " + MaxQueued
            + "): dropped oldest + log.");
        }
        _queue.Enqueue(job);
      }
      await job.Done.Task.ConfigureAwait(false);
      return;
    }

    // Interrupt or PauseResume: preempt current, then play.
    if (action == AudioInterruption.PauseResume) {
      lock (_gate) { _pausedResume = _activeJob; }
      Debug.Log("[AudioDirector] P1 over P3: fade-out 150ms (stub), P3 resumes after P1.");
      FadeOutCurrent();
    }
    CancelCurrent();
    await RunExclusiveAsync(job, true).ConfigureAwait(false);

    // Replay paused P3 (bypass its stale dedupe timestamp), then drain FIFO queue.
    SpeechJob resume = null;
    lock (_gate) {
      resume = _pausedResume;
      _pausedResume = null;
    }
    if (resume != null) {
      _cache.Forget(resume.Key);
      await RunExclusiveAsync(resume, false).ConfigureAwait(false);
    }
    await DrainQueueAsync().ConfigureAwait(false);
  }

  async Task DrainQueueAsync() {
    while (true) {
      SpeechJob next = null;
      lock (_gate) {
        while (_queue.Count > 0) {
          var peek = _queue.Peek();
          if ((DateTime.UtcNow - peek.EnqueuedUtc).TotalSeconds > QueueTimeoutSec) {
            _queue.Dequeue().Done.TrySetResult(false);
            Debug.Log("[AudioDirector] Queue entry timed out (>5s), dropped: '"
              + peek.Tts.Text + "'");
            continue;
          }
          next = _queue.Dequeue();
          break;
        }
      }
      if (next == null) return;
      _cache.Forget(next.Key); // queued wait must not self-dedupe on playout
      await RunExclusiveAsync(next, true).ConfigureAwait(false);
    }
  }

  async Task RunExclusiveAsync(SpeechJob job, bool signalDone) {
    var cts = new CancellationTokenSource();
    bool entered = false;
    try {
      await _playSlot.WaitAsync(cts.Token).ConfigureAwait(false);
      entered = true;
      lock (_gate) {
        _currentCts = cts;
        _current = job.Tts.Priority;
        _currentKey = job.Key;
        _inflight.Add(job.Key);
        _activeJob = job;
      }
      ApplyDucking(job.Tts.Priority);
      _bus.Publish(new DialogueRequested(
        job.Dialog.Voice, job.Dialog.Lang, job.Dialog.Priority, job.Dialog.Text));

      bool fromCache = false;
      AudioClip clip = null;
      if (_cache.TryGetMemory(job.Key, out clip) && clip != null) {
        fromCache = true;
      } else {
        byte[] bytes;
        if (!_cache.TryGetDisk(job.Key, out bytes) || bytes == null) {
          TtsAudioResult r = await SynthesizeAsync(job.Tts, cts.Token).ConfigureAwait(false);
          bytes = r.Mp3;
          fromCache = r.FromCache;
          if (bytes != null && bytes.Length > 0) StoreCaches(job.Key, null, bytes);
        } else {
          fromCache = true;
        }
        cts.Token.ThrowIfCancellationRequested(); // late TTS after cancel: drop silently
        if (bytes != null && bytes.Length > 0) {
          clip = await DecodeAudioAsync(bytes, job.Key, cts.Token).ConfigureAwait(false);
          if (clip != null) StoreCaches(job.Key, clip, null);
        }
      }

      if (clip != null) {
        await PlayClipAsync(clip, job.Tts.Priority, cts.Token).ConfigureAwait(false);
      } else {
        Debug.LogWarning("[AudioDirector] No decodable audio for '" + job.Tts.Text
          + "' (L2 bytes kept for W1 Addressables hook). Quest continues.");
      }
      if (job.IsVocabulary) {
        _bus.Publish(new VocabularyPlayed(job.VocabWord, job.VocabMode, fromCache));
      }
      if (signalDone) job.Done.TrySetResult(true);
    } catch (OperationCanceledException) {
      Debug.Log("[AudioDirector] Request cancelled, dropped silently: '" + job.Tts.Text + "'");
      if (signalDone) job.Done.TrySetResult(false);
    } catch (TtsException tex) {
      Debug.LogWarning("[AudioDirector] TTS failed (" + tex.Message
        + ") -> pre-gen/offline fallback. Quest continues.");
      await PlayPregenFallbackAsync(job, cts.Token).ConfigureAwait(false);
      if (signalDone) job.Done.TrySetResult(false);
    } catch (Exception ex) {
      Debug.LogWarning("[AudioDirector] Playback error (quest continues): " + ex.Message);
      if (signalDone) job.Done.TrySetResult(false);
    } finally {
      lock (_gate) {
        if (_currentKey == job.Key) {
          _current = null;
          _currentKey = null;
        }
        _inflight.Remove(job.Key);
        if (_activeJob == job) _activeJob = null;
        if (_currentCts == cts) _currentCts = null;
      }
      ClearDucking();
      if (entered) _playSlot.Release();
      try { cts.Dispose(); } catch (Exception) { }
    }
  }

  async Task PlayPregenFallbackAsync(SpeechJob job, CancellationToken ct) {
    try {
      byte[] pre = await TryLoadPregenAsync(job.Tts, job.Key, ct).ConfigureAwait(false);
      if (pre == null || pre.Length == 0) {
        Debug.Log("[AudioDirector] No pre-gen available in W0 for '" + job.Tts.Text + "'.");
        return;
      }
      AudioClip clip = await DecodeAudioAsync(pre, job.Key, ct).ConfigureAwait(false);
      if (clip != null) await PlayClipAsync(clip, job.Tts.Priority, ct).ConfigureAwait(false);
      if (job.IsVocabulary) {
        _bus.Publish(new VocabularyPlayed(job.VocabWord, job.VocabMode, true));
      }
    } catch (OperationCanceledException) {
      Debug.Log("[AudioDirector] Pre-gen fallback cancelled, dropped silently: '"
        + job.Tts.Text + "'");
      return;
    } catch (Exception ex) {
      Debug.LogWarning("[AudioDirector] Pre-gen fallback failed: " + ex.Message);
    }
  }

  void CancelCurrent() {
    CancellationTokenSource cts;
    lock (_gate) { cts = _currentCts; }
    if (cts != null) {
      try { cts.Cancel(); } catch (Exception) { }
    }
    try {
      if (_voice != null) _voice.Stop();
    } catch (Exception) { }
  }

  // ---- protected virtual backends (W1 hooks + test seams) ----

  protected virtual Task<TtsAudioResult> SynthesizeAsync(TtsRequest req, CancellationToken ct) {
    return _tts.SynthesizeAsync(req, ct);
  }

  protected virtual void StoreCaches(string key, AudioClip clip, byte[] mp3) {
    _cache.Store(key, clip, mp3);
  }

  // W1 Addressables override. W0: re-check L2 (covers bytes stored by a racing request).
  protected virtual Task<byte[]> TryLoadPregenAsync(
      TtsRequest req, string cacheKey, CancellationToken ct) {
    if (_cache.TryGetDisk(cacheKey, out byte[] bytes) && bytes != null) {
      return Task.FromResult(bytes);
    }
    return Task.FromResult<byte[]>(null);
  }

  // Default: decode the L2 file via UnityWebRequestMultimedia (real path).
  // Returns null (with log) when decode is unavailable — L2 bytes are kept.
  protected virtual async Task<AudioClip> DecodeAudioAsync(
      byte[] mp3, string cacheKey, CancellationToken ct) {
    string path = AudioCache.DiskPathFor(cacheKey);
    using (var req = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(
        new Uri(path).AbsoluteUri, AudioType.MPEG)) {
      var op = req.SendWebRequest();
      while (!op.isDone) {
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
      }
      ct.ThrowIfCancellationRequested();
      if (req.result != UnityEngine.Networking.UnityWebRequest.Result.Success) {
        Debug.LogWarning("[AudioDirector] Decode failed: " + req.error);
        return null;
      }
      AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(req);
      if (clip == null) Debug.LogWarning("[AudioDirector] Decode returned null clip.");
      return clip;
    }
  }

  // Default: the single voice AudioSource, real playback.
  protected virtual async Task PlayClipAsync(
      AudioClip clip, AudioPriority priority, CancellationToken ct) {
    EnsureVoiceSource();
    if (_voice == null || clip == null) return;
    _voice.clip = clip;
    _voice.Play();
    while (_voice.isPlaying) {
      if (ct.IsCancellationRequested) {
        FadeOutCurrent();
        try { _voice.Stop(); } catch (Exception) { }
        ct.ThrowIfCancellationRequested();
      }
      await Task.Yield();
    }
  }

  protected virtual void FadeOutCurrent() {
    Debug.Log("[AudioDirector] Fade-out 150ms (log stub, full mixer in W1).");
  }

  // W0: WordId string is the spoken text. W1: override to read Content display text.
  protected virtual string ResolveVocabText(WordId word) {
    return word.Value ?? "";
  }

  public void ApplyDucking(AudioPriority priority) {
    if (priority <= AudioPriority.P4_Feedback) {
      MusicDuckRatio = MusicDuckRatioP1;
      AmbientDuckRatio = AmbientDuckRatioP1;
      Debug.Log("[AudioDirector] Duck on: music -20%, ambient -40% during " + priority
        + " (single voice, others paused).");
    } else {
      MusicDuckRatio = 0f;
      AmbientDuckRatio = 0f;
    }
  }

  public void ClearDucking() {
    MusicDuckRatio = 0f;
    AmbientDuckRatio = 0f;
  }

  void EnsureVoiceSource() {
    if (_voice != null) return;
    try {
      _voiceRoot = new GameObject("LWE_AudioDirector_Voice");
      _voiceRoot.hideFlags = HideFlags.HideAndDontSave;
      if (Application.isPlaying) GameObject.DontDestroyOnLoad(_voiceRoot);
      _voice = _voiceRoot.AddComponent<AudioSource>();
      _voice.playOnAwake = false;
    } catch (Exception ex) {
      Debug.LogWarning("[AudioDirector] Voice source unavailable: " + ex.Message);
      _voice = null;
    }
  }

  // Internal job carrier (gate bookkeeping, not a SharedKernel event).
  protected sealed class SpeechJob {
    public TtsRequest Tts;
    public DialogueRequest Dialog;
    public bool IsVocabulary;
    public WordId VocabWord;
    public VocabularyAudioMode VocabMode;
    public string Key;
    public DateTime EnqueuedUtc;
    public readonly TaskCompletionSource<bool> Done = new TaskCompletionSource<bool>();

    public static SpeechJob ForDialogue(DialogueRequest dialog, TtsRequest tts) {
      return new SpeechJob { Dialog = dialog, Tts = tts, IsVocabulary = false };
    }

    public static SpeechJob ForVocabulary(
        WordId word, VocabularyAudioMode mode, TtsRequest tts, DialogueRequest dialog) {
      return new SpeechJob {
        Dialog = dialog, Tts = tts, IsVocabulary = true, VocabWord = word, VocabMode = mode,
      };
    }
  }
}
