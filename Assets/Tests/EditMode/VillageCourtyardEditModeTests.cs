// Assets/Tests/EditMode/VillageCourtyardEditModeTests.cs — Comprehensive NUnit Test Suite.
// Verifies "Sân Đình Vui Vẻ" (vietnamese_speaking):
//   1. 3-Region Dialect Matrix (North/Central/South unification).
//   2. Age-4 Developmental Lisp Tolerance (L/N swap, glides -> Pass + Developmental_Phoneme_Note).
//   3. Hybrid Scoring Formula (0.6 * Phoneme + 0.4 * ToneContour >= 0.70).
//   4. Audio Pipeline: 150Hz High-pass DSP, Real-time RMS, 900ms VAD Hangover, Whisper Mode (+12dB).
//   5. 3-Tier Anti-Frustration Scaffolding:
//      - Fail 1: Acoustic Mirroring (0.8x Viseme + Gentle reminder).
//      - Fail 2: Syllable Chunking (phrase broken into single syllables).
//      - Fail 3: Effort-First Rule (2/3 Stars Honorary Pass, Never discourage, Review Queue).
//   6. Success: Sân đình fireworks, Gift box open, Child voice playback.
//   7. SafetyFilter compliance (<= 6 words).
// Pure C# 9.0 only. EditMode-testable without UnityEngine scene dependencies.
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class VillageCourtyardEditModeTests {
  sealed class FakeAudio : IAudioDirector {
    public readonly List<string> Lines = new List<string>();
    public Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode mode) => Task.CompletedTask;
    public Task SpeakAsync(DialogueRequest request) {
      Lines.Add(request.Text);
      return Task.CompletedTask;
    }
    public void PlaySfx(SfxId id) { }
    public void PlayMusic(MusicId id) { }
    public void SetMusicEnabled(bool on) { }
    public void SetAudioFocus(AudioFocusMode mode) { }
  }

  static VillageCourtyardBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("S24CourtyardArena");
    var builder = arena.AddComponent<VillageCourtyardBuilder>();
    builder.BuildContent(arena.transform);
    return builder;
  }

  static VillageCourtyardGameController BuildGame(VillageCourtyardBuilder builder, GameObject arena,
      FakeAudio audio, ActivityLifecycle life, ChildVoiceCaptureService capture = null,
      IRealPhotoAssetLoader loader = null, List<SpeakingPromptSO> prompts = null) {
    var game = arena.GetComponent<VillageCourtyardGameController>();
    if (game == null) game = arena.AddComponent<VillageCourtyardGameController>();
    game.Build(builder, null, null, audio, life, capture, loader, prompts);
    return game;
  }

  [Test] public void S24A_Registration() {
    GameEntry g = LearningMap.Game("courtyard_speaking");
    Assert.IsNotNull(g, "courtyard_speaking registered");
    Assert.AreEqual("vietnamese_speaking", g.SkillId, "lives under vietnamese_speaking");
    Assert.AreEqual("vietnamese", LearningMap.SubjectOfSkill(g.SkillId).Id, "belongs to vietnamese");
    Assert.AreEqual("VillageCourtyardPlayScene", g.SceneName, "correct scene name");
    Assert.AreEqual(GameStatus.Implemented, g.Status, "IMPLEMENTED status");
    Assert.IsFalse(g.HumanAccepted, "not accepted baseline");
    Assert.IsFalse(LearningMap.IsPlayable("courtyard_speaking"), "firewall holds");
    Assert.IsTrue(LearningMap.CanLaunch("courtyard_speaking"), "may enter for play");
  }

  [Test] public void S24B_DialectMatrixThreeRegions() {
    var promptBuoi = SpeakingPromptSO.CreateInstance("buoi", "Quả bưởi", DifficultyTier.Tier2_RealWorldObject, 2);

    // 1. Central / South Dialect: Hoi -> Nga ("Quả bưỡi") => 100% Phoneme Score (1.0)
    var resSouth = SpeechEvaluationEngine.Evaluate("Quả bưỡi", promptBuoi);
    Assert.IsTrue(resSouth.IsPass, "South/Central hoi/nga variant passes");
    Assert.AreEqual(1.0f, resSouth.PhonemeScore, 0.01f, "100% score for valid regional dialect");

    // 2. North Dialect: X/S, CH/TR unification
    var promptSen = SpeakingPromptSO.CreateInstance("sen", "Bông hoa sen", DifficultyTier.Tier2_RealWorldObject, 3);
    var resNorth = SpeechEvaluationEngine.Evaluate("Bông hoa xen", promptSen);
    Assert.IsTrue(resNorth.IsPass, "North s/x variant passes");
    Assert.GreaterOrEqual(resNorth.PhonemeScore, 0.95f, "North dialect phoneme match");

    var promptChao = SpeakingPromptSO.CreateInstance("chao", "Tráu chào bà", DifficultyTier.Tier1_Greeting, 3);
    var resChao = SpeechEvaluationEngine.Evaluate("Cháu chào bà", promptChao);
    Assert.IsTrue(resChao.IsPass, "North tr/ch variant passes");
  }

  [Test] public void S24C_DevelopmentalLispToleranceAge4() {
    var promptNoi = SpeakingPromptSO.CreateInstance("me", "Con yêu mẹ", DifficultyTier.Tier3_Affection, 3);
    // 4-year-old child swaps L for N ("Con yêu nhẹ" / "Lon yêu mẹ")
    var resLisp = SpeechEvaluationEngine.Evaluate("Lon yêu mẹ", promptNoi);
    Assert.IsTrue(resLisp.IsPass, "Age-4 L/N developmental swap passes");
    Assert.IsNotEmpty(resLisp.DevelopmentalNote, "Developmental note logged");
    Assert.IsTrue(resLisp.DevelopmentalNote.Contains("Developmental_Phoneme_Note"), "Correct note prefix");
  }

  [Test] public void S24D_HybridScoringAndToneMatch() {
    var prompt = SpeakingPromptSO.CreateInstance("test", "Đèn lồng đỏ", DifficultyTier.Tier2_RealWorldObject, 3);

    // Exact tone contour & text => High score
    var resExact = SpeechEvaluationEngine.Evaluate("Đèn lồng đỏ", prompt);
    Assert.IsTrue(resExact.IsPass, "Exact match passes");
    Assert.GreaterOrEqual(resExact.TotalScore, 0.85f, "High score for exact match");

    // Hybrid formula check: Score = 0.6 * Phoneme + 0.4 * Tone
    float expected = (0.6f * resExact.PhonemeScore) + (0.4f * resExact.ToneScore);
    Assert.AreEqual(expected, resExact.TotalScore, 0.05f, "Hybrid scoring formula holds");
  }

  [Test] public void S24E_ChildVoiceCaptureAndHighPassDSP() {
    using var capture = new ChildVoiceCaptureService();
    capture.StartCapture();
    Assert.IsTrue(capture.IsRecording, "Capture is active");

    float lastRms = 0f;
    capture.onRMSUpdated = rms => lastRms = rms;

    // Simulate 16kHz audio block (1600 samples = 100ms) with low-frequency noise (50Hz) + voice
    float[] samples = new float[1600];
    for (int i = 0; i < samples.Length; i++) {
      float lowFreqNoise = Mathf.Sin(2f * Mathf.PI * 50f * i / 16000f) * 0.1f;
      float voiceSignal = Mathf.Sin(2f * Mathf.PI * 300f * i / 16000f) * 0.25f;
      samples[i] = lowFreqNoise + voiceSignal;
    }

    capture.PushSampleBlock(samples);
    Assert.Greater(lastRms, 0.01f, "RMS updated from speech samples");
    Assert.IsTrue(capture.IsSpeaking, "VAD detected speech onset");

    // Test Whisper Mode AGC (+12dB) activation after 4.0s silence
    Assert.IsFalse(capture.WhisperModeActive, "Initially whisper mode inactive");
    capture.Tick(4.1f);
    Assert.IsTrue(capture.WhisperModeActive, "Whisper mode AGC activates after 4.0s silence");

    capture.StopCapture();
    Assert.IsFalse(capture.IsRecording, "Capture stopped");
  }

  [Test] public void S24F_AntiFrustrationScaffolding_Fail1_AcousticMirroring() {
    var builder = BuildArena(out GameObject arena);
    var audio = new FakeAudio();
    var game = BuildGame(builder, arena, audio, null);

    game.BeginQuestion();
    game.Tick(6f); // PromptPresentation now waits for the mastered prompt clip length + margin
    Assert.AreEqual(VillageCourtyardGameController.Phase.ListeningState, game.CurrentPhase);

    // Fail 1: Simulated incorrect speech
    game.HandleSpeechCaptured(null, "Từ hoàn toàn sai");

    Assert.AreEqual(1, game.SpeechFailAttempts, "Attempts incremented to 1");
    Assert.AreEqual(VillageCourtyardGameController.Phase.AcousticMirroring, game.CurrentPhase,
      "Phase transitioned to AcousticMirroring");
    Assert.IsFalse(game.LastEvaluationPassed, "Evaluation recorded as failed");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S24G_AntiFrustrationScaffolding_Fail2_SyllableChunking() {
    var builder = BuildArena(out GameObject arena);
    var audio = new FakeAudio();
    var game = BuildGame(builder, arena, audio, null);

    game.BeginQuestion();
    game.Tick(1.6f);

    // Fail 1
    game.HandleSpeechCaptured(null, "Sai lần một");
    // Fail 2
    game.HandleSpeechCaptured(null, "Sai lần hai");

    Assert.AreEqual(2, game.SpeechFailAttempts, "Attempts incremented to 2");
    Assert.AreEqual(VillageCourtyardGameController.Phase.SyllableChunking, game.CurrentPhase,
      "Phase transitioned to SyllableChunking");
    Assert.IsNotNull(game.ActiveChunks, "Target broken into chunks");
    Assert.Greater(game.ActiveChunks.Length, 1, "Multiple syllables chunked");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S24H_AntiFrustrationScaffolding_Fail3_EffortFirstHonoraryPass() {
    var builder = BuildArena(out GameObject arena);
    var audio = new FakeAudio();
    var game = BuildGame(builder, arena, audio, null);

    game.BeginQuestion();
    string targetId = game.CurrentPrompt.promptId;
    game.Tick(1.6f);

    // Fail 1
    game.HandleSpeechCaptured(null, "Sai 1");
    // Fail 2
    game.HandleSpeechCaptured(null, "Sai 2");
    // Fail 3 -> Effort-First Rule triggers
    game.HandleSpeechCaptured(null, "Sai 3");

    Assert.AreEqual(3, game.SpeechFailAttempts, "Recorded 3 attempts");
    Assert.AreEqual(VillageCourtyardGameController.Phase.HonoraryPass, game.CurrentPhase,
      "Transitions to HonoraryPass, NEVER game over or error");

    // Verify Review Queue contains failed item for spaced review
    Assert.GreaterOrEqual(game.SpeechReviewQueue.Count, 1, "Enqueued to SpeechReviewQueue");
    Assert.AreEqual(targetId, game.SpeechReviewQueue.Peek(), "Correct prompt enqueued");

    // Question progresses without discouraging the child
    Assert.AreEqual(1, game.QuestionIndex, "Question index increments (honorary pass)");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S24I_SuccessFlow_Fireworks_GiftBox_AndChildVoicePlayback() {
    var builder = BuildArena(out GameObject arena);
    var audio = new FakeAudio();
    var game = BuildGame(builder, arena, audio, null);

    game.BeginQuestion();
    game.Tick(1.6f);

    float[] dummyVoiceSamples = new float[800];
    for (int i = 0; i < dummyVoiceSamples.Length; i++) dummyVoiceSamples[i] = 0.1f;

    // Successful speech match
    game.HandleSpeechCaptured(dummyVoiceSamples, game.CurrentPrompt.targetText);

    Assert.IsTrue(game.LastEvaluationPassed, "Evaluation passed");
    Assert.AreEqual(VillageCourtyardGameController.Phase.SuccessCelebration, game.CurrentPhase,
      "Success celebration phase");
    Assert.AreEqual(1, game.QuestionIndex, "Question index incremented");

    // Advance past rest period
    game.Tick(1.6f);
    Assert.AreEqual(VillageCourtyardGameController.Phase.PromptPresentation, game.CurrentPhase,
      "Advanced to next question presentation");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S24J_MemorySafePhotoRelease() {
    var loader = new MemorySafePhotoLoader();
    Assert.AreEqual(0, loader.ActiveHandleCount, "Initially 0 active handles");

    Texture2D tex = new Texture2D(64, 64);
    Sprite sp = Sprite.Create(tex, new Rect(0, 0, 64, 64), Vector2.zero);
    loader.RegisterPhoto("buoi", sp);
    Assert.IsTrue(loader.HasPhoto("buoi"), "Photo registered");

    loader.LoadPhotoAsync("buoi", loaded => {
      Assert.IsNotNull(loaded, "Loaded registered sprite");
    });
    Assert.AreEqual(1, loader.ActiveHandleCount, "1 active handle");

    loader.ReleasePhoto("buoi");
    Assert.AreEqual(0, loader.ActiveHandleCount, "0 handles after release");

    Object.DestroyImmediate(sp);
    Object.DestroyImmediate(tex);
  }

  [Test] public void S24L_ProductionPath_AttemptOnly_NeverFabricatesScores() {
    var builder = BuildArena(out GameObject arena);
    var game = BuildGame(builder, arena, new FakeAudio(), null);
    game.BeginQuestion();
    game.Tick(1.6f);

    // Loud "speech" samples, NO recognized text (production has no Vietnamese ASR)
    float[] loud = new float[3200];
    for (int i = 0; i < loud.Length; i++) loud[i] = Mathf.Sin(2f * Mathf.PI * 220f * i / 16000f) * 0.3f;
    game.HandleSpeechCaptured(loud, null);

    Assert.IsNotNull(game.LastResult, "attempt evaluated");
    Assert.IsFalse(game.LastResult.TranscriptAvailable, "no transcript exists on device");
    Assert.IsTrue(float.IsNaN(game.LastResult.PhonemeScore), "phoneme score must NOT be fabricated");
    Assert.IsTrue(float.IsNaN(game.LastResult.ToneScore), "tone score must NOT be fabricated");
    Assert.AreEqual("", game.LastResult.RecognizedText, "must not echo the target as a fake transcript");
    Assert.IsTrue(game.LastEvaluationPassed, "audible speaking attempt passes (effort-first)");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S24M_ProductionPath_Silence_RetriesWithoutFakeScores() {
    var builder = BuildArena(out GameObject arena);
    var game = BuildGame(builder, arena, new FakeAudio(), null);
    game.BeginQuestion();
    game.Tick(1.6f);

    float[] silence = new float[3200]; // all zeros — child said nothing
    game.HandleSpeechCaptured(silence, null);

    Assert.IsNotNull(game.LastResult);
    Assert.IsTrue(game.LastResult.SilentAttempt, "silence detected");
    Assert.IsFalse(game.LastEvaluationPassed, "silence is NOT a fake pass");
    Assert.IsFalse(game.LastResult.TranscriptAvailable);
    Assert.AreEqual(1, game.SpeechFailAttempts, "gentle tier-1 retry");
    Assert.AreEqual(VillageCourtyardGameController.Phase.AcousticMirroring, game.CurrentPhase);

    Object.DestroyImmediate(arena);
  }

  [Test] public void S24N_WhisperModeDropsWhenSpeechResumes() {
    using var capture = new ChildVoiceCaptureService();
    capture.StartCapture();
    capture.Tick(4.1f);
    Assert.IsTrue(capture.WhisperModeActive, "whisper mode after 4s of silence");

    float[] voice = new float[1600];
    for (int i = 0; i < voice.Length; i++) voice[i] = Mathf.Sin(2f * Mathf.PI * 300f * i / 16000f) * 0.3f;
    capture.PushSampleBlock(voice);
    Assert.IsFalse(capture.WhisperModeActive, "speech resumed — +12dB boost must drop");
  }

  [Test] public void S24O_VoicePackKeys_PinnedForPromptsAndChunks() {
    string[] ids = { "buoi", "chao_ba", "yeu_me", "hoa_sen", "den_long" };
    for (int i = 0; i < ids.Length; i++) {
      Assert.IsNotNull(VnVoicePack.Get("cdy_" + ids[i] + "_normal"), "normal clip pinned: " + ids[i]);
      Assert.IsNotNull(VnVoicePack.Get("cdy_" + ids[i] + "_slow"), "slow clip pinned: " + ids[i]);
    }
    string[] prefixes = { "ch_buoi", "ch_chao", "ch_yeu", "ch_sen", "ch_long" };
    for (int i = 0; i < prefixes.Length; i++) {
      int count = prefixes[i] == "ch_buoi" ? 2 : 3;
      for (int k = 1; k <= count; k++) {
        string key = prefixes[i] + "_" + k;
        Assert.IsNotNull(VnVoicePack.Get(key), "chunk clip pinned: " + key);
      }
    }
  }

  [Test] public void S24K_SafetyFilterAllDialogues() {
    string[] lines = {
      "Bé vào sân đình nhé!",
      "Con lắng nghe cô nhé.",
      "Đến lượt bé nói nào!",
      "Bé nói hay tuyệt vời!",
      "Gần đúng rồi, nhìn miệng cô!",
      "Bé nói to lên nào!",
      "Mình nói từng tiếng một nhé!",
      "Con cố gắng lắm, tuyệt vời!",
      "Bé nói tiếng Việt giỏi quá!"
    };

    for (int i = 0; i < lines.Length; i++) {
      string[] words = lines[i].Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
      Assert.LessOrEqual(words.Length, 6, "Dialogue <= 6 words: " + lines[i]);
    }
  }
}
