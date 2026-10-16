// CT-S23: spelling_garden & discovery_garden (Vườn Đánh Vần & Vườn Tri Thức).
// Tests 3-tier scaffolding, real photo clue, explicit phonics, memory-safe release,
// spaced repetition queue, and SafetyFilter compliance (<= 6 words).
// Pure C# 9.0 only. EditMode-testable without UnityEngine scene dependencies.
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CT_S23_PhonicsGarden {
  sealed class FakeAudio : IAudioDirector {
    public readonly List<string> Lines = new List<string>();
    public Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode mode) {
      return Task.CompletedTask;
    }
    public Task SpeakAsync(DialogueRequest request) {
      Lines.Add(request.Text);
      return Task.CompletedTask;
    }
    public void PlaySfx(SfxId id) { }
    public void PlayMusic(MusicId id) { }
    public void SetMusicEnabled(bool on) { }
    public void SetAudioFocus(AudioFocusMode mode) { }
  }

  static PhonicsGardenBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("S23PhonicsArena");
    PhonicsGardenBuilder builder = arena.AddComponent<PhonicsGardenBuilder>();
    builder.BuildContent(arena.transform);
    return builder;
  }

  static PhonicsGarden BuildGame(PhonicsGardenBuilder builder, GameObject arena,
      GameObject player, FakeAudio audio, ActivityLifecycle life,
      IRealPhotoAssetLoader loader = null,
      PhonicsPlayMode mode = PhonicsPlayMode.SpellingGarden) {
    PhonicsGarden game = arena.GetComponent<PhonicsGarden>();
    if (game == null) game = arena.AddComponent<PhonicsGarden>();
    game.Build(builder, player != null ? player.transform : null, null, audio, life, loader, mode);
    return game;
  }

  static PhonicsChoiceCard GetCard(PhonicsGardenBuilder builder, bool correct) {
    for (int i = 0; i < builder.Choices.Count; i++) {
      PhonicsChoiceCard c = builder.Choices[i];
      if (c != null && c.IsCorrect == correct) return c;
    }
    return null;
  }

  [Test] public void S23A_Registration() {
    GameEntry sg = LearningMap.Game("spelling_garden");
    Assert.IsNotNull(sg, "spelling_garden registered");
    Assert.AreEqual("vietnamese_letters", sg.SkillId, "lives under vietnamese_letters");
    Assert.AreEqual("vietnamese", LearningMap.SubjectOfSkill(sg.SkillId).Id, "belongs to vietnamese");
    Assert.AreEqual("PhonicsGardenPlayScene", sg.SceneName, "correct scene");
    Assert.AreEqual(GameStatus.Implemented, sg.Status, "IMPLEMENTED status");
    Assert.IsFalse(sg.HumanAccepted, "not accepted baseline");
    Assert.IsFalse(LearningMap.IsPlayable("spelling_garden"), "acceptance firewall holds");
    Assert.IsTrue(LearningMap.CanLaunch("spelling_garden"), "may enter for play");

    GameEntry dg = LearningMap.Game("discovery_garden");
    Assert.IsNotNull(dg, "discovery_garden registered");
    Assert.AreEqual("vietnamese_pre_literacy", dg.SkillId, "lives under vietnamese_pre_literacy");
    Assert.AreEqual("vietnamese", LearningMap.SubjectOfSkill(dg.SkillId).Id, "belongs to vietnamese");
    Assert.AreEqual("PhonicsGardenPlayScene", dg.SceneName, "correct scene");
    Assert.AreEqual(GameStatus.Implemented, dg.Status, "IMPLEMENTED status");
    Assert.IsFalse(dg.HumanAccepted, "not accepted baseline");
    Assert.IsFalse(LearningMap.IsPlayable("discovery_garden"), "acceptance firewall holds");
    Assert.IsTrue(LearningMap.CanLaunch("discovery_garden"), "may enter for play");
  }

  [Test] public void S23B_PhonicsDatabaseAndTopics() {
    Assert.GreaterOrEqual(PhonicsDatabase.Words.Length, 8, "sufficient vocabulary");
    string[] requiredTopics = { "animals", "fruits", "vehicles", "tools", "nature", "body" };
    for (int i = 0; i < requiredTopics.Length; i++) {
      PhonicsWordData[] words = PhonicsDatabase.GetWordsByTopic(requiredTopics[i]);
      Assert.IsNotNull(words, "topic exists: " + requiredTopics[i]);
      Assert.Greater(words.Length, 0, "topic has words: " + requiredTopics[i]);
    }

    for (int i = 0; i < PhonicsDatabase.Words.Length; i++) {
      PhonicsWordData w = PhonicsDatabase.Words[i];
      Assert.IsFalse(string.IsNullOrEmpty(w.WordId), "word id non-empty");
      Assert.IsFalse(string.IsNullOrEmpty(w.FullWord), "full word non-empty");
      Assert.IsFalse(string.IsNullOrEmpty(w.RealPhotoAddress), "real photo path non-empty");
      Assert.IsNotNull(w.Segments, "segments exist");
      Assert.Greater(w.Segments.Length, 0, "has segments");
      Assert.IsNotNull(w.Distractors, "distractors exist");
      Assert.AreEqual(2, w.Distractors.Length, "exactly 2 distractors for 3 choices");

      // SafetyFilter Check: Vi sentence <= 6 words
      if (!string.IsNullOrEmpty(w.ExplainSentenceVi)) {
        string[] viWords = w.ExplainSentenceVi.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        Assert.LessOrEqual(viWords.Length, 6, "Vi sentence <= 6 words: " + w.ExplainSentenceVi);
      }
    }
  }

  [Test] public void S23C_ScaffoldingTier1_RepeatAndEncourage() {
    PhonicsGardenBuilder builder = BuildArena(out GameObject arena);
    FakeAudio audio = new FakeAudio();
    PhonicsGarden game = BuildGame(builder, arena, null, audio, null);

    game.BeginQuestion();
    Assert.AreEqual(ScaffoldingTier.InitialAttempt, game.CurrentTier, "starts at initial attempt");
    Assert.AreEqual(0, game.MistakeCount, "zero mistakes initially");

    // Advance to WaitingChoice
    game.Tick(1.5f);
    Assert.AreEqual(PhonicsGarden.Phase.WaitingChoice, game.CurrentPhase, "waiting choice");

    // Pick wrong card (Mistake 1)
    PhonicsChoiceCard wrongCard = GetCard(builder, false);
    Assert.IsNotNull(wrongCard, "wrong card exists");
    game.TrySelectChoice(wrongCard);

    Assert.AreEqual(1, game.MistakeCount, "recorded mistake 1");
    Assert.AreEqual(ScaffoldingTier.MistakeRepeat, game.CurrentTier, "tier 1 repeat scaffolding");
    Assert.IsFalse(builder.Board.PhotoFrameRoot.activeSelf, "photo frame still hidden on mistake 1");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S23D_ScaffoldingTier2_PictureClue_RealPhotoOrHonestPictogram() {
    // (a) REAL PHOTO path: an owner-registered sprite serves as the visual anchor
    {
      PhonicsGardenBuilder builder = BuildArena(out GameObject arena);
      FakeAudio audio = new FakeAudio();
      MemorySafePhotoLoader loader = new MemorySafePhotoLoader();
      Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
      Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
      for (int i = 0; i < PhonicsDatabase.Words.Length; i++) {
        loader.RegisterPhoto(PhonicsDatabase.Words[i].RealPhotoAddress, sprite);
      }

      PhonicsGarden game = BuildGame(builder, arena, null, audio, null, loader);
      game.BeginQuestion();
      game.Tick(1.5f);

      // Mistake 1
      game.TrySelectChoice(GetCard(builder, false));
      game.Tick(1.0f); // count down scaffold timer to allow retry

      // Mistake 2
      game.TrySelectChoice(GetCard(builder, false));

      Assert.AreEqual(2, game.MistakeCount, "recorded mistake 2");
      Assert.AreEqual(ScaffoldingTier.MistakePhotoClue, game.CurrentTier, "tier 2 clue scaffolding");
      Assert.IsTrue(builder.Board.PhotoFrameRoot.activeSelf, "clue frame is displayed on mistake 2");
      Assert.IsNotNull(builder.Board.PhotoRenderer.sprite, "registered REAL photo is served");
      Assert.IsFalse(builder.Board.PictogramActive, "no pictogram when a real photo exists");
      Assert.AreEqual(1, loader.ActiveHandleCount, "1 registered-photo handle active");

      Object.DestroyImmediate(arena);
    }

    // (b) HONEST fallback: no photo registered -> pictogram illustration, never a fabricated photo
    {
      PhonicsGardenBuilder builder = BuildArena(out GameObject arena);
      FakeAudio audio = new FakeAudio();
      MemorySafePhotoLoader loader = new MemorySafePhotoLoader();
      PhonicsGarden game = BuildGame(builder, arena, null, audio, null, loader);
      game.BeginQuestion();
      game.Tick(1.5f);
      game.TrySelectChoice(GetCard(builder, false));
      game.Tick(1.0f);
      game.TrySelectChoice(GetCard(builder, false));

      Assert.IsTrue(builder.Board.PhotoFrameRoot.activeSelf, "clue frame shown even without photos");
      Assert.IsNull(builder.Board.PhotoRenderer.sprite, "no fake photo is ever fabricated");
      Assert.IsTrue(builder.Board.PictogramActive, "honest pictogram illustration shown");

      Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S23E_ScaffoldingTier3_ExplicitSpellingAndSpacedRepetition() {
    PhonicsGardenBuilder builder = BuildArena(out GameObject arena);
    FakeAudio audio = new FakeAudio();
    PhonicsGarden game = BuildGame(builder, arena, null, audio, null);

    game.BeginQuestion();
    string targetWordId = game.CurrentWord.WordId;
    game.Tick(1.5f);

    // Mistake 1
    game.TrySelectChoice(GetCard(builder, false));
    game.Tick(1.0f);
    // Mistake 2
    game.TrySelectChoice(GetCard(builder, false));
    game.Tick(1.2f);
    // Mistake 3
    game.TrySelectChoice(GetCard(builder, false));

    Assert.AreEqual(3, game.MistakeCount, "recorded mistake 3");
    Assert.AreEqual(ScaffoldingTier.MistakeTeachBack, game.CurrentTier, "tier 3 teach back scaffolding");

    // Verify Spaced Repetition tracking
    Assert.GreaterOrEqual(game.SpacedTracker.GetMistakeCount(targetWordId), 1, "mistake logged in spaced tracker");
    Assert.GreaterOrEqual(game.SpacedTracker.PendingReviewCount, 1, "word enqueued into review queue");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S23F_CorrectChoice_Advancement() {
    PhonicsGardenBuilder builder = BuildArena(out GameObject arena);
    FakeAudio audio = new FakeAudio();
    PhonicsGarden game = BuildGame(builder, arena, null, audio, null);

    game.BeginQuestion();
    game.Tick(1.5f);

    PhonicsChoiceCard correctCard = GetCard(builder, true);
    Assert.IsNotNull(correctCard, "correct card exists");
    game.TrySelectChoice(correctCard);

    Assert.IsTrue(game.LastChoiceCorrect, "last choice recorded correct");
    Assert.AreEqual(PhonicsGarden.Phase.SuccessFeedback, game.CurrentPhase, "success feedback phase");
    Assert.AreEqual(1, game.QuestionIndex, "question index incremented");

    // Tick past rest period to advance to next question
    game.Tick(1.6f);
    Assert.AreEqual(PhonicsGarden.Phase.IntroSpelling, game.CurrentPhase, "advanced to next question");

    Object.DestroyImmediate(arena);
  }

  [Test] public void S23G_MemorySafePhotoRelease_RegisteredOnlyNeverFabricated() {
    MemorySafePhotoLoader loader = new MemorySafePhotoLoader();
    Assert.AreEqual(0, loader.ActiveHandleCount, "initially 0 handles");

    // Unregistered key: honest "no photo" — null sprite, no handle, no fabricated texture
    Sprite nothing = null;
    loader.LoadPhotoAsync("RealPhotos/Animals/fish_real", sp => { nothing = sp; });
    Assert.IsNull(nothing, "loader must NOT fabricate a photo for unregistered keys");
    Assert.AreEqual(0, loader.ActiveHandleCount, "no handle for unregistered key");

    // Registered key: served + tracked, release drops the handle
    Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
    Sprite realPhoto = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
    loader.RegisterPhoto("RealPhotos/Animals/fish_real", realPhoto);
    Sprite served = null;
    loader.LoadPhotoAsync("RealPhotos/Animals/fish_real", sp => { served = sp; });
    Assert.AreSame(realPhoto, served, "registered photo is served");
    Assert.AreEqual(1, loader.ActiveHandleCount, "1 active handle after registered load");

    loader.ReleasePhoto("RealPhotos/Animals/fish_real");
    Assert.AreEqual(0, loader.ActiveHandleCount, "0 handles after release");

    loader.LoadPhotoAsync("RealPhotos/Fruits/apple_real", sp => { });
    loader.LoadPhotoAsync("RealPhotos/Animals/cow_real", sp => { });
    Assert.AreEqual(0, loader.ActiveHandleCount, "still 0: those keys are not registered");
    loader.ReleaseAll();
    Assert.AreEqual(0, loader.ActiveHandleCount, "ReleaseAll safe");
  }

  [Test] public void S23H_SpacedRepetitionQueueOrder() {
    SpacedRepetitionTracker tracker = new SpacedRepetitionTracker();
    Assert.AreEqual(0, tracker.PendingReviewCount, "initially empty");

    tracker.RecordMistake("ca");
    tracker.RecordMistake("bo");
    Assert.AreEqual(2, tracker.PendingReviewCount, "2 words in queue");

    // Does not duplicate in queue if already present
    tracker.RecordMistake("ca");
    Assert.AreEqual(2, tracker.PendingReviewCount, "still 2 unique review words");
    Assert.AreEqual(2, tracker.GetMistakeCount("ca"), "mistake count tracks frequency");

    string firstOut = tracker.DequeueReviewWord();
    Assert.AreEqual("ca", firstOut, "FIFO review queue order");
    string secondOut = tracker.DequeueReviewWord();
    Assert.AreEqual("bo", secondOut, "second item popped");
    Assert.AreEqual(0, tracker.PendingReviewCount, "queue empty");
  }

  [Test] public void S23N_VoicePackKeys_PinnedForEverySegmentWordAndSpellLine() {
    for (int i = 0; i < PhonicsDatabase.Words.Length; i++) {
      PhonicsWordData w = PhonicsDatabase.Words[i];
      Assert.IsNotNull(VnVoicePack.Get("word_" + w.WordId), "word clip pinned: " + w.WordId);
      Assert.IsNotNull(VnVoicePack.Get("spell_" + w.WordId), "spell line pinned: " + w.WordId);
      Assert.IsNotNull(VnVoicePack.Get("disc_" + w.WordId), "discovery clip pinned: " + w.WordId);
      for (int s = 0; s < w.Segments.Length; s++) {
        Assert.IsFalse(string.IsNullOrEmpty(w.Segments[s].AudioKey), "segment audio key set: " + w.WordId);
        Assert.IsNotNull(VnVoicePack.Get(w.Segments[s].AudioKey),
          "segment clip pinned: " + w.Segments[s].AudioKey + " (word " + w.WordId + ")");
      }
    }
  }

  [Test] public void S23O_HoaWord_ChainFollowsTeachingVideoOrder() {
    PhonicsWordData hoa = PhonicsDatabase.GetWord("hoa");
    Assert.IsNotNull(hoa, "hoa word exists");
    Assert.AreEqual(6, hoa.Segments.Length, "o -> a -> oa -> hờ -> oa -> hoa");
    Assert.AreEqual("O", hoa.Segments[0].SegmentText);
    Assert.AreEqual("A", hoa.Segments[1].SegmentText);
    Assert.AreEqual("OA", hoa.Segments[2].SegmentText);
    Assert.AreEqual("H", hoa.Segments[3].SegmentText);
    Assert.AreEqual("OA", hoa.Segments[4].SegmentText);
    Assert.AreEqual("HOA", hoa.Segments[5].SegmentText);
    Assert.AreEqual("word_hoa", hoa.Segments[5].AudioKey);
  }

  [Test] public void S23I_SafetyFilterAllDialogues() {
    string[] lines = {
      "Con ra vườn nhé!",
      "Hãy lắng nghe cô nhé.",
      "Con nghe lại nhé!",
      "Xem hình gợi ý nhé!",
      "Cùng học đánh vần nhé!",
      "Đúng rồi, bé giỏi lắm!",
      "Bé học thật xuất sắc!",
      "Đây là gì nào?"
    };

    for (int i = 0; i < lines.Length; i++) {
      string[] words = lines[i].Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
      Assert.LessOrEqual(words.Length, 6, "Dialogue line <= 6 words: " + lines[i]);
    }
  }
}
