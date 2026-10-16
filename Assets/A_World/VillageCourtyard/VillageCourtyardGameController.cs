// A_World/VillageCourtyard/VillageCourtyardGameController.cs — Core State Machine for "Sân Đình Vui Vẻ".
// 3-Tier Anti-Frustration Scaffolding:
//   Fail 1: Gentle Reminder + Acoustic Mirroring (0.8x Viseme)
//   Fail 2: Syllable Chunking (Breaks phrase into individual syllables)
//   Fail 3: Effort-First Rule (2/3 Stars Honorary Pass, Never discourage, Enqueue review)
// Success: Sân đình fireworks, Cloud gift box (Real photo / pictogram), Child voice playback.
// Pure C# 9.0 only. EditMode-testable without UnityEngine scene dependencies.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class VillageCourtyardGameController : MonoBehaviour {
  public enum Phase {
    Wait,
    PromptPresentation,
    ListeningState,
    AcousticMirroring,
    SyllableChunking,
    HonoraryPass,
    SuccessCelebration,
    Done
  }

  public Phase CurrentPhase { get; private set; }
  public SpeakingPromptSO CurrentPrompt { get; private set; }
  public int SpeechFailAttempts { get; private set; }
  public int QuestionIndex { get; private set; }
  public int TotalQuestions { get; private set; } = 5;
  public bool Completed { get; private set; }
  public bool LastEvaluationPassed { get; private set; }
  public SpeechEvaluationResult LastResult { get; private set; }

  // Syllable chunking state for Fail 2
  public int CurrentChunkIndex { get; private set; }
  public string[] ActiveChunks { get; private set; }

  // Spaced Repetition Review Queue for Effort-First items
  public Queue<string> SpeechReviewQueue { get; private set; } = new Queue<string>();

  public ChildVoiceCaptureService VoiceCapture { get; private set; }
  public IRealPhotoAssetLoader PhotoLoader { get; private set; }

  VillageCourtyardBuilder _builder;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PacedVoice _voice;
  AudioSource _playbackAudioSource;

  List<SpeakingPromptSO> _prompts = new List<SpeakingPromptSO>();
  float _phaseT;
  float _restT;
  bool _finishAfterRest;
  bool _waitCalled;
  float _promptClipLen;

  public void Build(VillageCourtyardBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life, ChildVoiceCaptureService capture = null,
      IRealPhotoAssetLoader loader = null, List<SpeakingPromptSO> customPrompts = null) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _voice = new PacedVoice();
    _voice.Audio = audio;
    _voice.LogTag = "VillageCourtyard";

    VoiceCapture = capture ?? new ChildVoiceCaptureService();
    PhotoLoader = loader ?? new MemorySafePhotoLoader();

    // Hook audio source for playing back the child's recorded voice
    _playbackAudioSource = gameObject.GetComponent<AudioSource>();
    if (_playbackAudioSource == null) _playbackAudioSource = gameObject.AddComponent<AudioSource>();

    // Wire real-time RMS to builder visuals (lantern and kite)
    VoiceCapture.onRMSUpdated = rms => {
      if (_builder != null) _builder.UpdateRMSVisuals(rms);
    };

    VoiceCapture.onSpeechEnded = samples => {
      if (CurrentPhase == Phase.ListeningState || CurrentPhase == Phase.SyllableChunking) {
        HandleSpeechCaptured(samples);
      }
    };

    InitPrompts(customPrompts);

    if (_life != null) {
      try {
        if (_life.State == ActivityState.Completed) { Adopt(); return; }
        _life.MarkAvailable("village courtyard speaking offered");
      } catch (Exception) { }
    }

    CurrentPhase = Phase.Wait;
    QuestionIndex = 0;
    _phaseT = 0f;
    Follow();
  }

  void InitPrompts(List<SpeakingPromptSO> custom) {
    _prompts.Clear();
    if (custom != null && custom.Count > 0) {
      _prompts.AddRange(custom);
    } else {
      // Default preschool Vietnamese vocabulary curriculum (3 tiers)
      _prompts.Add(SpeakingPromptSO.CreateInstance("buoi", "Quả bưởi", DifficultyTier.Tier2_RealWorldObject, 2));
      _prompts.Add(SpeakingPromptSO.CreateInstance("chao_ba", "Cháu chào bà", DifficultyTier.Tier1_Greeting, 3));
      _prompts.Add(SpeakingPromptSO.CreateInstance("yeu_me", "Con yêu mẹ", DifficultyTier.Tier3_Affection, 3));
      _prompts.Add(SpeakingPromptSO.CreateInstance("hoa_sen", "Bông hoa sen", DifficultyTier.Tier2_RealWorldObject, 3));
      _prompts.Add(SpeakingPromptSO.CreateInstance("den_long", "Đèn lồng đỏ", DifficultyTier.Tier2_RealWorldObject, 3));
    }
  }

  void Update() { Tick(Time.deltaTime); }

  public void Tick(float dt) {
    if (dt <= 0f) return;

    if (_voice != null) _voice.Tick(dt);
    if (VoiceCapture != null) VoiceCapture.Tick(dt);

    if (Completed && CurrentPhase == Phase.Done) return;
    if (_restT > 0f) {
      _restT -= dt;
      if (_restT <= 0f) AfterWin();
      return;
    }

    _phaseT += dt;
    if (CurrentPhase == Phase.Wait) {
      TickWait(dt);
    } else if (CurrentPhase == Phase.PromptPresentation) {
      TickPresentation(dt);
    }
  }

  void TickWait(float dt) {
    if (!_waitCalled && _phaseT >= 1.0f) {
      _waitCalled = true;
      if (_builder != null) _builder.CuoiEncourage();
      Say("Come to the courtyard!", "Bé vào sân đình nhé!");
      ActivityGuide.PointAt(SpotWorld());
    }
    if (PlayerNear(SpotWorld(), 1.5f) || _phaseT >= 6f) {
      BeginQuestion();
    }
  }

  void TickPresentation(float dt) {
    if (_phaseT >= Mathf.Max(1.5f, _promptClipLen + 0.4f)) {
      TransitionToListening();
    }
  }

  public void BeginQuestion() {
    if (Completed) return;

    // Release previously loaded photo handle for memory safety
    if (CurrentPrompt != null && PhotoLoader != null) {
      PhotoLoader.ReleasePhoto(CurrentPrompt.promptId);
    }

    // Check Spaced Repetition queue first
    if (SpeechReviewQueue.Count > 0) {
      string reviewId = SpeechReviewQueue.Dequeue();
      CurrentPrompt = _prompts.Find(p => p.promptId == reviewId);
    }

    if (CurrentPrompt == null) {
      int idx = QuestionIndex % _prompts.Count;
      CurrentPrompt = _prompts[idx];
    }

    SpeechFailAttempts = 0;
    LastEvaluationPassed = false;
    LastResult = null;
    CurrentChunkIndex = 0;
    ActiveChunks = null;
    _phaseT = 0f;

    if (_builder != null) {
      _builder.CloseGiftBox();
    }

    CurrentPhase = Phase.PromptPresentation;

    // Teacher model reads reference phrase with the mastered voice-pack clip (stretched cadence)
    Say("Listen and repeat after me.", "Con lắng nghe cô nhé.");
    if (_builder != null) {
      _builder.TamDemonstrateMouth(1.0f);
    }
    AudioClip promptClip = PackClip("normal");
    PlayPromptClip(promptClip);
    _promptClipLen = promptClip != null ? promptClip.length : 0f;
  }

  void TransitionToListening() {
    CurrentPhase = Phase.ListeningState;
    _phaseT = 0f;

    if (VoiceCapture != null) {
      VoiceCapture.StartCapture();
    }

    Say("Your turn to speak now!", "Đến lượt bé nói nào!");
  }

  // Recognition trigger entry (called when ChildVoiceCaptureService VAD ends speech, or in tests).
  // HONEST PRODUCTION PATH (1.9.48): no Vietnamese ASR ships in this build. When no recognized
  // text is supplied (the real game), evaluation is ATTEMPT-ONLY from real audio evidence.
  // The controller must NEVER evaluate the target against itself — that was a fake always-pass.
  public void HandleSpeechCaptured(float[] samples, string recognizedSimulatedText = null) {
    if (CurrentPrompt == null) return;

    if (CurrentPhase == Phase.SyllableChunking && ActiveChunks != null && CurrentChunkIndex < ActiveChunks.Length) {
      EvaluateChunkSpeech(recognizedSimulatedText, samples);
      return;
    }

    SpeechEvaluationResult result = !string.IsNullOrEmpty(recognizedSimulatedText)
      ? SpeechEvaluationEngine.Evaluate(recognizedSimulatedText, CurrentPrompt, samples, ChildVoiceCaptureService.SampleRate)
      : SpeechEvaluationEngine.EvaluateAttemptOnly(samples, CurrentPrompt);
    LastResult = result;
    LastEvaluationPassed = result.IsPass;

    if (result.IsPass) {
      HandleSuccess(samples);
    } else {
      HandleFailure(samples, result.SilentAttempt);
    }
  }

  void HandleSuccess(float[] recordedSamples) {
    CurrentPhase = Phase.SuccessCelebration;
    if (VoiceCapture != null) VoiceCapture.StopCapture();

    // 1. Sân đình fireworks celebration
    if (_builder != null) {
      _builder.TriggerFireworks();
      _builder.NpcCelebrateAll();
    }

    // 2. Open cloud gift box (with real photo or 3D pictogram)
    Sprite photoSprite = null;
    if (PhotoLoader != null && CurrentPrompt != null) {
      PhotoLoader.LoadPhotoAsync(CurrentPrompt.promptId, sp => photoSprite = sp);
    }
    if (_builder != null && CurrentPrompt != null) {
      _builder.OpenGiftBox(photoSprite, CurrentPrompt.promptId);
    }

    // 3. Special Feature: Play back the child's own recorded voice through speaker
    PlayChildVoiceRecording(recordedSamples);

    PlaySfx("ui_correct");
    ActivityFeedback.Correct();
    Say("Wonderful, you spoke so well!", "Bé nói hay tuyệt vời!");

    QuestionIndex++;
    ActivityFeedback.ProgressKeep(QuestionIndex, TotalQuestions);

    bool finish = QuestionIndex >= TotalQuestions;
    RestThenAdvance(finish);
  }

  void HandleFailure(float[] samples, bool silent = false) {
    SpeechFailAttempts++;
    PlaySfx("ui_mistake");

    if (SpeechFailAttempts == 1) {
      // Tier 1: Sai lần 1 — Nhắc nhở dịu dàng + Acoustic Mirroring (Zoom camera, 0.8x Viseme)
      CurrentPhase = Phase.AcousticMirroring;
      if (_builder != null) {
        _builder.TamDemonstrateMouth(0.8f);
      }
      ZoomCameraOnNpc();
      if (silent) {
        Say("Speak louder, you can do it!", "Bé nói to lên nào!");
      } else {
        Say("Almost right, watch my mouth!", "Gần đúng rồi, nhìn miệng cô!");
      }
      PlayPromptClip(PackClip("slow")); // bản chậm đã nướng 0.8x (giữ cao độ tự nhiên)

      if (VoiceCapture != null) VoiceCapture.ResetSpeechBuffer();
      // Allow child to retry speaking
      Invoke(nameof(TransitionToListening), 1.2f);
    } else if (SpeechFailAttempts == 2) {
      // Tier 2: Sai lần 2 — Bẻ nhỏ âm tiết (Syllable Chunking)
      CurrentPhase = Phase.SyllableChunking;
      if (_builder != null) {
        _builder.CuoiEncourage();
      }
      Follow(); // reset camera

      // Split target into single syllables
      ActiveChunks = (CurrentPrompt.targetText ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
      CurrentChunkIndex = 0;

      Say("Let us say word by word!", "Mình nói từng tiếng một nhé!");

      if (ActiveChunks.Length > 0) {
        PromptNextChunk();
      } else {
        TransitionToListening();
      }
    } else {
      // Tier 3: Sai lần 3 — Effort-First Rule (Pass Danh Dự)
      // TUYỆT ĐỐI KHÔNG BÁO LỖI, KHÔNG LÀM TRẺ NẢN LÒNG!
      CurrentPhase = Phase.HonoraryPass;
      if (VoiceCapture != null) VoiceCapture.StopCapture();

      // Enqueue to SpeechReviewQueue for future practice
      if (CurrentPrompt != null && !SpeechReviewQueue.Contains(CurrentPrompt.promptId)) {
        SpeechReviewQueue.Enqueue(CurrentPrompt.promptId);
      }

      // Open gift box normally with praise
      Sprite photoSprite = null;
      if (PhotoLoader != null && CurrentPrompt != null) {
        PhotoLoader.LoadPhotoAsync(CurrentPrompt.promptId, sp => photoSprite = sp);
      }
      if (_builder != null && CurrentPrompt != null) {
        _builder.OpenGiftBox(photoSprite, CurrentPrompt.promptId);
        _builder.NpcCelebrateAll();
      }

      PlaySfx("ui_star_award");
      ActivityFeedback.Correct(); // awards 2/3 stars honorary
      Say("Great effort, good job baby!", "Con cố gắng lắm, tuyệt vời!");

      QuestionIndex++;
      ActivityFeedback.ProgressKeep(QuestionIndex, TotalQuestions);

      bool finish = QuestionIndex >= TotalQuestions;
      RestThenAdvance(finish);
    }
  }

  void PromptNextChunk() {
    if (ActiveChunks == null || CurrentChunkIndex >= ActiveChunks.Length) {
      HandleSuccess(null);
      return;
    }
    string chunk = ActiveChunks[CurrentChunkIndex];
    AudioClip chunkClip = ChunkClip(CurrentChunkIndex);
    if (chunkClip != null) {
      PlayPromptClip(chunkClip); // stretched teaching clip reads the syllable itself
      Say("Your turn!", "Bé nói nhé!");
    } else {
      Say("Say this: " + chunk, "Bé nói: " + chunk);
    }

    if (VoiceCapture != null) {
      VoiceCapture.StartCapture();
    }
  }

  void EvaluateChunkSpeech(string text, float[] samples) {
    if (ActiveChunks == null || CurrentChunkIndex >= ActiveChunks.Length) return;
    string targetChunk = ActiveChunks[CurrentChunkIndex];

    var chunkPrompt = SpeakingPromptSO.CreateInstance("chunk", targetChunk, DifficultyTier.Tier2_RealWorldObject, 1);
    var res = !string.IsNullOrEmpty(text)
      ? SpeechEvaluationEngine.Evaluate(text, chunkPrompt, samples, ChildVoiceCaptureService.SampleRate)
      : SpeechEvaluationEngine.EvaluateAttemptOnly(samples, chunkPrompt);
    LastResult = res;
    LastEvaluationPassed = res.IsPass;

    if (res.IsPass) {
      PlaySfx("ui_correct_small");
      CurrentChunkIndex++;
      if (CurrentChunkIndex < ActiveChunks.Length) {
        PromptNextChunk();
      } else {
        HandleSuccess(samples);
      }
    } else {
      HandleFailure(samples, res.SilentAttempt);
    }
  }

  void PlayChildVoiceRecording(float[] samples) {
    if (samples == null || samples.Length == 0 || _playbackAudioSource == null) return;
    try {
      AudioClip clip = AudioClip.Create("ChildVoiceFiltered", samples.Length, 1, ChildVoiceCaptureService.SampleRate, false);
      clip.SetData(samples, 0);
      _playbackAudioSource.PlayOneShot(clip, 1.0f);
    } catch (Exception) { }
  }

  void ZoomCameraOnNpc() {
    if (_cam == null || _builder == null || _builder.NpcTam == null) return;
    try {
      _cam.Follow(_builder.NpcTam.transform, new Vector3(0f, 1.2f, -1.8f));
    } catch (Exception) { }
  }

  void Follow() {
    if (_cam == null || _player == null) return;
    try {
      _cam.Follow(_player, VillageCourtyardBuilder.FollowOffset);
    } catch (Exception) { }
  }

  void RestThenAdvance(bool finish) {
    _finishAfterRest = finish;
    _restT = 1.6f;
  }

  void AfterWin() {
    _restT = 0f;
    Follow();
    if (_finishAfterRest) FinishVisit();
    else BeginQuestion();
  }

  void FinishVisit() {
    Completed = true;
    CurrentPhase = Phase.Done;

    if (VoiceCapture != null) {
      VoiceCapture.StopCapture();
    }
    if (PhotoLoader != null) {
      PhotoLoader.ReleaseAll();
    }
    if (_builder != null) {
      _builder.NpcCelebrateAll();
      _builder.CloseGiftBox();
    }

    Say("You are awesome at speaking!", "Bé nói tiếng Việt giỏi quá!");
    if (_life != null) {
      try { _life.MarkCompleted("village courtyard speaking finished"); } catch (Exception) { }
    }
  }

  void Adopt() {
    Completed = true;
    CurrentPhase = Phase.Done;
    if (_builder != null) _builder.CloseGiftBox();
  }

  void Say(string en, string vi) {
    if (_voice == null) return;
    _voice.Speak(DialogueLang.T(en, vi));
  }

  // Voice-pack helpers: the 5 built-in prompt ids (buoi/chao_ba/yeu_me/hoa_sen/den_long) are
  // mastered as cdy_<id>_normal/slow + ch_<short>_<n> syllable chunks. Missing -> honest fallback.
  AudioClip PackClip(string suffix) {
    if (CurrentPrompt == null || string.IsNullOrEmpty(CurrentPrompt.promptId)) return null;
    return VnVoicePack.Get("cdy_" + CurrentPrompt.promptId + "_" + suffix);
  }

  AudioClip ChunkClip(int index) {
    if (CurrentPrompt == null) return null;
    string prefix = null;
    switch (CurrentPrompt.promptId) {
      case "buoi": prefix = "ch_buoi"; break;
      case "chao_ba": prefix = "ch_chao"; break;
      case "yeu_me": prefix = "ch_yeu"; break;
      case "hoa_sen": prefix = "ch_sen"; break;
      case "den_long": prefix = "ch_long"; break;
    }
    if (prefix == null) return null;
    return VnVoicePack.Get(prefix + "_" + (index + 1));
  }

  void PlayPromptClip(AudioClip clip) {
    if (clip == null || _playbackAudioSource == null) return;
    try { _playbackAudioSource.PlayOneShot(clip, 1f); } catch (Exception) { }
  }

  void PlaySfx(string id) {
    if (_audio == null) return;
    try { _audio.PlaySfx(new SfxId(id)); } catch (Exception) { }
  }

  bool PlayerNear(Vector3 world, float r) {
    if (_player == null) return true;
    Vector3 d = _player.position - world;
    d.y = 0f;
    return d.sqrMagnitude <= r * r;
  }

  Vector3 SpotWorld() {
    return _builder != null
      ? _builder.transform.TransformPoint(VillageCourtyardBuilder.PlaySpotLocal)
      : VillageCourtyardBuilder.PlaySpotLocal;
  }

  void OnDestroy() {
    if (VoiceCapture != null) {
      VoiceCapture.Dispose();
    }
    if (PhotoLoader != null) {
      PhotoLoader.ReleaseAll();
    }
  }
}
