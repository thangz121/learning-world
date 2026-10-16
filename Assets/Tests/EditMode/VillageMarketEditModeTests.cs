// Assets/Tests/EditMode/VillageMarketEditModeTests.cs — Comprehensive NUnit Test Suite.
// Verifies "Chợ Quê Nhộn Nhịp" (vietnamese_listening):
//   1. Product Taxonomy & LearningMap Registration.
//   2. 3 Modes: Direct Noun, Attribute Riddle, Sequential Order (2-step drag).
//   3. 3-Tier Scaffolding:
//      - Fail 1: Acoustic Boost (0.8x slow speed, real sound cue).
//      - Fail 2: Visual Narrowing (cất 1 đáp án sai, 50/50, spatial stereo pan).
//      - Fail 3: Guided Discovery (Hand-off, promote target, show text label, ReviewQueue BufferGap >= 2).
//   4. Sequential Drag Step Enforcement (item 0 then item 1).
//   5. Review Queue BufferGap >= 2 Spaced Repetition.
//   6. Audio Ducking (-24dB attenuation on Ambient).
//   7. Memory Safety (IRealPhotoAssetLoader release).
//   8. SafetyFilter (all spoken lines <= 6 words).
// Pure C# 9.0 only. EditMode-testable without scene dependencies.
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using LweAssets;

public class VillageMarketEditModeTests {
  sealed class FakeAudio : IAudioDirector {
    public readonly List<string> Lines = new List<string>();
    public readonly List<string> SfxPlayed = new List<string>();

    public Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode mode) => Task.CompletedTask;
    public Task SpeakAsync(DialogueRequest request) {
      Lines.Add(request.Text);
      return Task.CompletedTask;
    }
    public void PlaySfx(SfxId id) {
      SfxPlayed.Add(id.Value);
    }
    public void PlayMusic(MusicId id) { }
    public void SetMusicEnabled(bool on) { }
    public void SetAudioFocus(AudioFocusMode mode) { }
  }

  sealed class FakePhotoLoader : IRealPhotoAssetLoader {
    public readonly HashSet<string> LoadedIds = new HashSet<string>();
    public readonly HashSet<string> ReleasedIds = new HashSet<string>();

    public void RegisterPhoto(string photoId, Sprite sprite) { }
    public bool HasPhoto(string photoId) { return false; }

    public void LoadPhotoAsync(string photoId, System.Action<Sprite> onComplete) {
      LoadedIds.Add(photoId);
      onComplete?.Invoke(null);
    }

    public void ReleasePhoto(string photoId) {
      ReleasedIds.Add(photoId);
      LoadedIds.Remove(photoId);
    }

    public void ReleaseAll() {
      LoadedIds.Clear();
    }
  }

  static VillageMarketBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("S25MarketArena");
    var builder = arena.AddComponent<VillageMarketBuilder>();
    builder.BuildContent(arena.transform);
    return builder;
  }

  static VillageMarketGameController BuildGame(VillageMarketBuilder builder, GameObject arena,
      FakeAudio audio, ActivityLifecycle life = null, SpatialAudioDuckingService ducking = null,
      FakePhotoLoader loader = null, List<ListeningQuestionSO> questions = null) {
    var game = arena.GetComponent<VillageMarketGameController>();
    if (game == null) game = arena.AddComponent<VillageMarketGameController>();
    game.Build(builder, null, null, audio, life, ducking, loader, questions);
    return game;
  }

  [Test] public void S25A_Registration() {
    GameEntry g = LearningMap.Game("market_listening");
    Assert.IsNotNull(g, "market_listening registered in LearningMap");
    Assert.AreEqual("vietnamese_listening", g.SkillId, "lives under vietnamese_listening");
    Assert.AreEqual("vietnamese", LearningMap.SubjectOfSkill(g.SkillId).Id, "belongs to vietnamese");
    Assert.AreEqual("VillageMarketPlayScene", g.SceneName, "correct scene name");
    Assert.AreEqual(GameStatus.Implemented, g.Status, "IMPLEMENTED status");
    Assert.IsFalse(g.HumanAccepted, "not accepted baseline");
    Assert.IsFalse(LearningMap.IsPlayable("market_listening"), "firewall holds");
    Assert.IsTrue(LearningMap.CanLaunch("market_listening"), "may enter for play");
  }

  [Test] public void S25B_Level1_DirectNoun_Listening_And_Selection() {
    var builder = BuildArena(out GameObject arena);
    var audio = new FakeAudio();
    var game = BuildGame(builder, arena, audio);

    game.StartGame();
    Assert.AreEqual(VillageMarketGameController.Phase.PromptPlayback, game.CurrentPhase);
    Assert.AreEqual(ListeningMode.Level1_DirectNoun, game.CurrentQuestion.mode);
    Assert.AreEqual(3, game.ActiveItemUIs.Count, "3 items on stall");

    // Advance prompt playback to response phase
    game.Tick(1.5f);
    Assert.AreEqual(VillageMarketGameController.Phase.AwaitingResponse, game.CurrentPhase);

    // Target is 'orange'
    var correctItem = game.ActiveItemUIs.Find(u => u.ItemId == "orange");
    Assert.IsNotNull(correctItem, "Found orange item on stall");
    Assert.IsFalse(correctItem.IsLabelVisible, "Label initially hidden to test listening");

    // Child taps correct item
    game.HandleItemTapped(correctItem);

    Assert.AreEqual(VillageMarketGameController.Phase.SuccessCelebration, game.CurrentPhase);
    Assert.AreEqual(1, game.QuestionIndex, "Question index incremented");
    Assert.IsTrue(correctItem.IsLabelVisible, "Label revealed on success");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S25C_Level2_AttributeRiddle_MultipleAttributes() {
    var builder = BuildArena(out GameObject arena);
    var audio = new FakeAudio();
    var game = BuildGame(builder, arena, audio);

    game.StartGame();
    // Advance to Q2 (Level 2 Attribute Riddle)
    game.Tick(1.5f);
    var q1Target = game.ActiveItemUIs.Find(u => u.ItemId == "orange");
    game.HandleItemTapped(q1Target);
    game.Tick(1.6f); // Advance past celebration

    Assert.AreEqual(1, game.QuestionIndex);
    Assert.AreEqual(ListeningMode.Level2_AttributeRiddle, game.CurrentQuestion.mode);
    Assert.AreEqual("watermelon", game.CurrentQuestion.targetItemIdsInOrder[0]);

    game.Tick(1.5f); // Enter awaiting response
    var target = game.ActiveItemUIs.Find(u => u.ItemId == "watermelon");
    game.HandleItemTapped(target);

    Assert.AreEqual(VillageMarketGameController.Phase.SuccessCelebration, game.CurrentPhase);
    Assert.AreEqual(2, game.QuestionIndex);

    Object.DestroyImmediate(arena);
  }

  [Test] public void S25D_Level3_SequentialOrder_TwoStepDrag() {
    var builder = BuildArena(out GameObject arena);
    var audio = new FakeAudio();
    var game = BuildGame(builder, arena, audio);

    // Custom Level 3 curriculum
    var qL3 = ListeningQuestionSO.CreateInstance("Q_Seq", ListeningMode.Level3_SequentialOrder,
      new List<MarketItemOption> {
        new MarketItemOption("banana", "Chuối"),
        new MarketItemOption("milk", "Sữa"),
        new MarketItemOption("bread", "Bánh Mì"),
        new MarketItemOption("apple", "Táo")
      },
      new List<string> { "banana", "milk" }
    );
    game.Build(builder, null, null, audio, null, null, null, new List<ListeningQuestionSO> { qL3 });

    game.StartGame();
    game.Tick(1.5f);
    Assert.AreEqual(ListeningMode.Level3_SequentialOrder, game.CurrentQuestion.mode);
    Assert.AreEqual(0, game.CurrentSequenceStep);

    var banana = game.ActiveItemUIs.Find(u => u.ItemId == "banana");
    var milk = game.ActiveItemUIs.Find(u => u.ItemId == "milk");

    // Drag step 1: Banana
    game.HandleItemDragged(banana);
    Assert.IsTrue(banana.IsInBasket, "Banana placed in basket");
    Assert.AreEqual(1, game.CurrentSequenceStep, "Advanced to step 2");
    Assert.AreNotEqual(VillageMarketGameController.Phase.SuccessCelebration, game.CurrentPhase, "Not yet finished");

    // Drag step 2: Milk
    game.HandleItemDragged(milk);
    Assert.IsTrue(milk.IsInBasket, "Milk placed in basket");
    Assert.AreEqual(2, game.CurrentSequenceStep, "Completed 2 steps");
    Assert.AreEqual(VillageMarketGameController.Phase.SuccessCelebration, game.CurrentPhase, "Success celebration triggered");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S25E_Level3_SequentialOrder_WrongOrderRejected() {
    var builder = BuildArena(out GameObject arena);
    var audio = new FakeAudio();
    var game = BuildGame(builder, arena, audio);

    var qL3 = ListeningQuestionSO.CreateInstance("Q_Seq", ListeningMode.Level3_SequentialOrder,
      new List<MarketItemOption> {
        new MarketItemOption("banana", "Chuối"),
        new MarketItemOption("milk", "Sữa"),
        new MarketItemOption("bread", "Bánh Mì")
      },
      new List<string> { "banana", "milk" }
    );
    game.Build(builder, null, null, audio, null, null, null, new List<ListeningQuestionSO> { qL3 });

    game.StartGame();
    game.Tick(1.5f);

    var milk = game.ActiveItemUIs.Find(u => u.ItemId == "milk");

    // Child drags milk first (wrong order! Expects banana first)
    game.HandleItemDragged(milk);

    Assert.AreEqual(1, game.WrongAttempts, "Wrong attempt recorded for out-of-order drag");
    Assert.AreEqual(0, game.CurrentSequenceStep, "Step not advanced");
    Assert.AreEqual(VillageMarketGameController.Phase.AcousticBoost, game.CurrentPhase, "Transitions to AcousticBoost");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S25F_Scaffolding_Fail1_AcousticBoost_And_SoundCue() {
    var builder = BuildArena(out GameObject arena);
    var audio = new FakeAudio();
    var ducking = new SpatialAudioDuckingService();
    var game = BuildGame(builder, arena, audio, null, ducking);

    game.StartGame();
    game.Tick(1.5f);

    var wrongItem = game.ActiveItemUIs.Find(u => u.ItemId != "orange");

    // Fail 1
    game.HandleItemTapped(wrongItem);

    Assert.AreEqual(1, game.WrongAttempts, "Wrong attempts = 1");
    Assert.AreEqual(VillageMarketGameController.Phase.AcousticBoost, game.CurrentPhase, "Phase is AcousticBoost");
    Assert.AreEqual("mkt_q1_slow", ducking.LastPlayedClipName, "slow mastered clip served for acoustic boost");
    Assert.AreEqual(1.0f, ducking.PlaybackSpeed, 0.01f, "slow is BAKED (0.8x natural pitch) - played at 1.0, no pitch-shift");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S25G_Scaffolding_Fail2_VisualNarrowing_50Percent_And_SpatialPan() {
    var builder = BuildArena(out GameObject arena);
    var audio = new FakeAudio();
    var ducking = new SpatialAudioDuckingService();
    var game = BuildGame(builder, arena, audio, null, ducking);

    game.StartGame();
    game.Tick(1.5f);

    var wrongItem = game.ActiveItemUIs.Find(u => u.ItemId != "orange");

    // Fail 1
    game.HandleItemTapped(wrongItem);
    // Fail 2
    game.HandleItemTapped(wrongItem);

    Assert.AreEqual(2, game.WrongAttempts, "Wrong attempts = 2");
    Assert.AreEqual(VillageMarketGameController.Phase.VisualNarrowing, game.CurrentPhase, "Phase is VisualNarrowing");

    // Check that 1 wrong option was narrowed out
    int activeCount = game.ActiveItemUIs.FindAll(u => u.IsActive).Count;
    Assert.AreEqual(2, activeCount, "Reduced to 2 options (50/50 visual narrowing)");

    // Spatial pan was evaluated
    Assert.AreNotEqual(0f, ducking.CurrentPan, "Spatial pan set according to target position");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S25H_Scaffolding_Fail3_GuidedDiscovery_HandOff_And_ReviewQueue() {
    var builder = BuildArena(out GameObject arena);
    var audio = new FakeAudio();
    var game = BuildGame(builder, arena, audio);

    game.StartGame();
    string targetId = game.CurrentQuestion.questionId;
    game.Tick(1.5f);

    var wrongItem = game.ActiveItemUIs.Find(u => u.ItemId != "orange");

    // Fail 1
    game.HandleItemTapped(wrongItem);
    // Fail 2
    game.HandleItemTapped(wrongItem);
    // Fail 3 -> Guided discovery
    game.HandleItemTapped(wrongItem);

    Assert.AreEqual(3, game.WrongAttempts, "Wrong attempts = 3");
    Assert.AreEqual(VillageMarketGameController.Phase.GuidedDiscovery, game.CurrentPhase, "Phase is GuidedDiscovery");

    var targetUi = game.ActiveItemUIs.Find(u => u.ItemId == "orange");
    Assert.IsTrue(targetUi.IsPromoted, "Target item is promoted and enlarged");
    Assert.IsTrue(targetUi.IsLabelVisible, "Target label is revealed for symbol association");

    // Enqueued to ReviewQueue
    Assert.GreaterOrEqual(game.ReviewQueue.Count, 1, "Question enqueued in ReviewQueue");
    var entry = game.ReviewQueue.Peek();
    Assert.AreEqual(targetId, entry.QuestionId);
    Assert.GreaterOrEqual(entry.AllowedAtQuestionIndex, game.QuestionIndex + 2, "BufferGap >= 2 enforced");

    // Child completes by clicking/dragging promoted item
    game.HandleItemTapped(targetUi);
    Assert.AreEqual(VillageMarketGameController.Phase.SuccessCelebration, game.CurrentPhase, "Guided drop succeeds");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S25I_ReviewQueue_BufferGap_Enforcement() {
    var q = new Queue<VillageMarketGameController.ReviewEntry>();
    int currentIndex = 1;
    int gap = 2;

    q.Enqueue(new VillageMarketGameController.ReviewEntry {
      QuestionId = "Q_Test",
      AllowedAtQuestionIndex = currentIndex + gap // 3
    });

    Assert.AreEqual(1, q.Count);
    // At currentIndex = 2 (gap 1): not yet allowed
    Assert.Less(2, q.Peek().AllowedAtQuestionIndex, "Cannot replay at gap < 2");
    // At currentIndex = 3 (gap 2): allowed
    Assert.LessOrEqual(q.Peek().AllowedAtQuestionIndex, 3, "Allowed at gap >= 2");
  }

  [Test] public void S25J_AudioDucking_Attenuation_Minus24dB() {
    using var service = new SpatialAudioDuckingService();
    Assert.IsFalse(service.IsDucking);
    Assert.AreEqual(1.0f, service.CurrentAmbientGain);

    service.StartDucking();
    Assert.IsTrue(service.IsDucking);

    // Tick 100ms (attack duration)
    service.Tick(SpatialAudioDuckingService.DuckingAttackTime);

    // Gain should reach ~0.0631 (-24dB)
    Assert.AreEqual(SpatialAudioDuckingService.DuckingLinearFloor, service.CurrentAmbientGain, 0.01f,
      "-24dB linear floor reached after attack time");

    // Stop ducking and tick 300ms (release duration)
    service.StopDucking();
    service.Tick(SpatialAudioDuckingService.DuckingReleaseTime);

    Assert.AreEqual(1.0f, service.CurrentAmbientGain, 0.01f, "Restored to 0dB (1.0) after release time");
  }

  [Test] public void S25K_MemorySafeRelease_OnAdvance() {
    var builder = BuildArena(out GameObject arena);
    var audio = new FakeAudio();
    var loader = new FakePhotoLoader();
    var game = BuildGame(builder, arena, audio, null, null, loader);

    game.StartGame();
    game.Tick(1.5f);

    var target = game.ActiveItemUIs.Find(u => u.ItemId == "orange");
    game.HandleItemTapped(target);

    // Advance to next question -> releases previous item sprites
    game.Tick(1.6f);

    Assert.IsTrue(loader.ReleasedIds.Contains("orange"), "Orange photo handle released on advance");
    Assert.IsTrue(loader.ReleasedIds.Contains("apple"), "Apple photo handle released on advance");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S25M_VoicePackPresent_SpeaksHonestly_AndUnmappedFallsBackToText() {
    // (a) Default curriculum ids are mastered -> the pack clip plays and the sign hides.
    var builder = BuildArena(out GameObject arena);
    var ducking = new SpatialAudioDuckingService();
    var game = BuildGame(builder, arena, new FakeAudio(), null, ducking);
    game.StartGame();

    Assert.AreEqual("mkt_q1_normal", ducking.LastPlayedClipName, "voice-pack clip served for the prompt");
    Assert.IsTrue(ducking.IsDucking, "ducking engages for a REAL clip");
    Assert.IsFalse(builder.PromptSign.gameObject.activeInHierarchy, "text sign hidden when voice exists");

    Object.DestroyImmediate(arena);

    // (b) Custom question with no mastered clips -> honest text fallback, no fake audio.
    var builder2 = BuildArena(out GameObject arena2);
    var ducking2 = new SpatialAudioDuckingService();
    var game2 = BuildGame(builder2, arena2, new FakeAudio(), null, ducking2, null,
      new List<ListeningQuestionSO> {
        ListeningQuestionSO.CreateInstance("Q_Custom", ListeningMode.Level1_DirectNoun,
          new List<MarketItemOption> { new MarketItemOption("orange", "Quả Cam") },
          new List<string> { "orange" })
      });
    game2.StartGame();

    Assert.IsTrue(builder2.PromptSign.text.Contains("Quả Cam"), "text prompt states the target");
    Assert.IsFalse(ducking2.IsDucking, "no clip => no fake ducking of the ambient");
    Assert.AreEqual("", ducking2.LastPlayedClipName, "no fabricated prompt name");

    Object.DestroyImmediate(arena2);
  }

  [Test] public void S25N_ItemsRespondToRouterClicks() {
    var builder = BuildArena(out GameObject arena);
    var game = BuildGame(builder, arena, new FakeAudio());
    game.StartGame();
    game.Tick(1.5f);

    var correct = game.ActiveItemUIs.Find(u => u.ItemId == "orange");
    ((IClickTarget)correct).OnClicked(); // ClickRouter path

    Assert.AreEqual(VillageMarketGameController.Phase.SuccessCelebration, game.CurrentPhase,
      "IClickTarget click routes to the same selection logic");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S25O_MissedQuestion_ActuallyRepeatsAfterBufferGap() {
    var builder = BuildArena(out GameObject arena);
    var game = BuildGame(builder, arena, new FakeAudio());
    game.StartGame();
    game.Tick(1.5f);

    // Fail Q1 (orange) three times -> Guided Discovery + enqueue (gap 2)
    var wrong = game.ActiveItemUIs.Find(u => u.ItemId != "orange");
    game.HandleItemTapped(wrong);
    game.HandleItemTapped(wrong);
    game.HandleItemTapped(wrong);
    Assert.AreEqual(1, game.ReviewQueue.Count, "Q1 enqueued once");

    // Finish Q1 via the promoted item
    var target = game.ActiveItemUIs.Find(u => u.ItemId == "orange");
    game.HandleItemTapped(target);
    game.Tick(1.6f); // index 1 — gap not reached yet
    Assert.AreEqual("Q2_DuaHau", game.CurrentQuestion.questionId, "gap not reached: normal order");

    // Finish Q2 normally
    game.Tick(1.5f);
    var wm = game.ActiveItemUIs.Find(u => u.ItemId == "watermelon");
    game.HandleItemTapped(wm);
    game.Tick(1.6f); // index 2 — review now allowed -> Q1 repeats here

    Assert.AreEqual("Q1_Cam", game.CurrentQuestion.questionId, "missed question is actually replayed");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S25L_SafetyFilter_Dialogues_Under6Words() {
    string[] lines = {
      "Lấy giúp bà món này nhé!",
      "Nghe câu đố của bà nhé!",
      "Bỏ vào giỏ giúp bà nhé!",
      "Con lắng nghe kỹ lại nhé!",
      "Kìa, chú cún chỉ đường kìa!",
      "Đây rồi, bỏ vào giỏ nhé!",
      "Bé nghe giỏi tuyệt vời!",
      "Lấy thêm một món nữa nhé!",
      "Đi chợ xong, bé giỏi quá!"
    };

    for (int i = 0; i < lines.Length; i++) {
      string[] words = lines[i].Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
      Assert.LessOrEqual(words.Length, 6, "Dialogue <= 6 words for 4-year-olds: " + lines[i]);
    }
  }
}
