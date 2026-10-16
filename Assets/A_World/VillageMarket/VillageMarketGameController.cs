// Assets/A_World/VillageMarket/VillageMarketGameController.cs — Core State Machine for "Chợ Quê Nhộn Nhịp".
// Supports:
//   1. 3 Listening Modes: Level1_DirectNoun, Level2_AttributeRiddle, Level3_SequentialOrder.
//   2. 3-Tier Scaffolding: Fail 1 (Acoustic Boost), Fail 2 (Visual Narrowing & Spatial Pan), Fail 3 (Guided Discovery & Hand-off).
//   3. Sequential 2-step Drag-to-Basket logic for Level 3.
//   4. ListeningReviewQueue with Spaced Repetition BufferGap >= 2.
//   5. Registered-photo release on question advance.
// Pure C# 9.0 only. EditMode-testable without scene dependencies.
using System;
using System.Collections.Generic;
using UnityEngine;
using LweAssets;

public class VillageMarketGameController : MonoBehaviour {
  public enum Phase {
    PromptPlayback,
    AwaitingResponse,
    AcousticBoost,     // Sai lần 1
    VisualNarrowing,   // Sai lần 2
    GuidedDiscovery,   // Sai lần 3
    SuccessCelebration,
    Complete
  }

  public class ReviewEntry {
    public string QuestionId;
    public int AllowedAtQuestionIndex;
  }

  public Phase CurrentPhase { get; private set; } = Phase.PromptPlayback;
  public int QuestionIndex { get; private set; } = 0;
  public int TotalQuestions => _questions.Count;
  public int WrongAttempts { get; private set; } = 0;
  public int CurrentSequenceStep { get; private set; } = 0;

  public ListeningQuestionSO CurrentQuestion =>
    (QuestionIndex >= 0 && QuestionIndex < _questions.Count) ? _questions[QuestionIndex] : null;

  public readonly List<MarketItemUI> ActiveItemUIs = new List<MarketItemUI>();
  public readonly Queue<ReviewEntry> ReviewQueue = new Queue<ReviewEntry>();

  public SpatialAudioDuckingService AudioService { get; private set; }
  public IRealPhotoAssetLoader PhotoLoader { get; private set; }
  public ActivityLifecycle Lifecycle { get; private set; }
  public IAudioDirector AudioDirector { get; private set; }
  public VillageMarketBuilder Builder { get; private set; }

  List<ListeningQuestionSO> _questions = new List<ListeningQuestionSO>();
  float _phaseTimer = 0f;
  const float PhaseDelay = 1.4f;
  PacedVoice _voice;

  SmartCamera _cam;
  Transform _player;

  public void Build(VillageMarketBuilder builder, Transform playerTransform, SmartCamera worldCamera,
      IAudioDirector audio, ActivityLifecycle life, SpatialAudioDuckingService audioService = null,
      IRealPhotoAssetLoader photoLoader = null, List<ListeningQuestionSO> questions = null) {
    Builder = builder;
    _player = playerTransform;
    _cam = worldCamera;
    AudioDirector = audio;
    Lifecycle = life;
    _voice = new PacedVoice();
    _voice.Audio = audio;
    _voice.LogTag = "VillageMarket";
    AudioService = audioService ?? new SpatialAudioDuckingService();
    PhotoLoader = photoLoader;

    _questions = (questions != null && questions.Count > 0) ? questions : GenerateDefaultCurriculum();
    QuestionIndex = 0;
    WrongAttempts = 0;

    if (Lifecycle != null) {
      try {
        if (Lifecycle.State == ActivityState.Completed) { CompleteGame(); return; }
        Lifecycle.MarkAvailable("village market listening offered");
      } catch (Exception) { }
    }
  }

  public void StartGame() {
    QuestionIndex = 0;
    BeginQuestion();
  }

  public void BeginQuestion() {
    if (QuestionIndex >= TotalQuestions) {
      CompleteGame();
      return;
    }

    WrongAttempts = 0;
    CurrentSequenceStep = 0;
    CurrentPhase = Phase.PromptPlayback;
    _phaseTimer = 0f;

    var q = CurrentQuestion;
    if (q == null) return;

    // 1. Rebuild stall items
    SpawnStallItems(q);

    // 2. Audio Ducking & Voice Prompt. Voice = the mastered VnVoicePack clip for the built-in
    //    curriculum (generated via the project TTS worker). Custom questions without mastered
    //    clips fall back to the honest world sign text — never fake audio.
    AudioClip normalClip = (q.normalPromptAudio != null && q.normalPromptAudio.EditorAsset != null)
      ? q.normalPromptAudio.EditorAsset
      : VnVoicePack.Get(VoiceKeyFor(q, "normal"));
    if (AudioService != null) {
      AudioService.PlayVoicePrompt(normalClip, 1.0f, 0f);
    }
    if (Builder != null) {
      if (normalClip != null) Builder.HidePromptText();
      else Builder.ShowPromptText("Bà cần: " + GetPrimaryTargetDisplayName(q) + "!");
    }

    // 3. Dialogue
    SayInitialPrompt(q);
  }

  void SpawnStallItems(ListeningQuestionSO q) {
    // Clean old UIs
    ReleaseCurrentItems();

    if (q.allOptionsOnStall == null || q.allOptionsOnStall.Count == 0) return;

    int count = q.allOptionsOnStall.Count;
    float stallWidth = 4.2f;
    float startX = -stallWidth * 0.5f;
    float stepX = count > 1 ? stallWidth / (count - 1) : 0f;

    for (int i = 0; i < count; i++) {
      var opt = q.allOptionsOnStall[i];
      float normX = count > 1 ? (float)i / (count - 1) : 0.5f;
      Vector3 localPos = new Vector3(startX + i * stepX, 0.4f, 0f);

      var go = new GameObject("MarketItem_" + opt.itemId);
      go.transform.SetParent(transform, false);

      var ui = go.AddComponent<MarketItemUI>();
      ui.Initialize(opt, localPos, normX, PhotoLoader);

      ui.OnTapped = HandleItemTapped;
      ui.OnDraggedToBasket = HandleItemDragged;

      ActiveItemUIs.Add(ui);
    }
  }

  void SayInitialPrompt(ListeningQuestionSO q) {
    switch (q.mode) {
      case ListeningMode.Level1_DirectNoun:
        Say("Find the item for grandma!", "Lấy giúp bà món này nhé!");
        break;
      case ListeningMode.Level2_AttributeRiddle:
        Say("Listen carefully to grandma's riddle!", "Nghe câu đố của bà nhé!");
        break;
      case ListeningMode.Level3_SequentialOrder:
        Say("Put two things in basket!", "Bỏ vào giỏ giúp bà nhé!");
        break;
    }
  }

  // --- Interaction Router ---
  public void HandleItemTapped(MarketItemUI item) {
    if (item == null) return;

    if (CurrentPhase == Phase.GuidedDiscovery && item.IsPromoted) {
      // At Tier 3, clicking the promoted item puts it in basket and finishes
      HandleGuidedDrop(item);
      return;
    }

    if (CurrentPhase != Phase.AwaitingResponse &&
        CurrentPhase != Phase.AcousticBoost &&
        CurrentPhase != Phase.VisualNarrowing) return;

    var q = CurrentQuestion;
    if (q == null) return;

    if (q.mode == ListeningMode.Level3_SequentialOrder) {
      // In sequential mode, tap simulates drag to basket
      HandleItemDragged(item);
      return;
    }

    // Level 1 & Level 2 single-item selection
    string targetId = (q.targetItemIdsInOrder.Count > 0) ? q.targetItemIdsInOrder[0] : "";
    if (string.Equals(item.ItemId, targetId, StringComparison.OrdinalIgnoreCase)) {
      HandleSuccess();
    } else {
      HandleFailure(item);
    }
  }

  public void HandleItemDragged(MarketItemUI item) {
    if (item == null) return;
    var q = CurrentQuestion;
    if (q == null) return;

    if (CurrentPhase == Phase.GuidedDiscovery && item.IsPromoted) {
      HandleGuidedDrop(item);
      return;
    }

    if (q.mode != ListeningMode.Level3_SequentialOrder) {
      HandleItemTapped(item);
      return;
    }

    // Sequential 2-step validation
    if (CurrentSequenceStep < q.targetItemIdsInOrder.Count) {
      string expectedTarget = q.targetItemIdsInOrder[CurrentSequenceStep];
      if (string.Equals(item.ItemId, expectedTarget, StringComparison.OrdinalIgnoreCase)) {
        // Step passed
        item.PlaceInBasket(GetBasketPosition());
        PlaySfx("ui_correct_small");
        CurrentSequenceStep++;

        if (CurrentSequenceStep >= q.targetItemIdsInOrder.Count) {
          HandleSuccess();
        } else {
          Say("Next, get one more thing!", "Lấy thêm một món nữa nhé!");
        }
      } else {
        HandleFailure(item);
      }
    }
  }

  void HandleGuidedDrop(MarketItemUI item) {
    item.PlaceInBasket(GetBasketPosition());
    PlaySfx("ui_correct_small");
    HandleSuccess();
  }

  // --- 3-Tier Adaptive Scaffolding ---
  public void HandleFailure(MarketItemUI selectedWrongItem) {
    WrongAttempts++;
    PlaySfx("ui_mistake_soft");

    var q = CurrentQuestion;
    if (q == null) return;

    if (WrongAttempts == 1) {
      // Tier 1: Acoustic Boost & Real Sound Cue
      CurrentPhase = Phase.AcousticBoost;
      if (selectedWrongItem != null) selectedWrongItem.GentleShake();

      Say("Listen carefully again, baby!", "Con lắng nghe kỹ lại nhé!");

      if (AudioService != null) {
        AudioClip slowClip = q.slowEmphasizedAudio != null ? q.slowEmphasizedAudio.EditorAsset : null;
        if (slowClip == null) slowClip = VnVoicePack.Get(VoiceKeyFor(q, "slow"));
        AudioService.PlayVoicePrompt(slowClip, 1.0f, 0f); // slow is BAKED (0.8x, natural pitch) — never pitch-shift again

        // Play real physical / animal sound cue if available
        var targetOption = FindOption(GetPrimaryTargetId(q));
        if (targetOption != null && targetOption.realSoundCue != null && targetOption.realSoundCue.EditorAsset != null) {
          AudioService.PlaySoundCue(targetOption.realSoundCue.EditorAsset, 0f);
        }
      }
    } else if (WrongAttempts == 2) {
      // Tier 2: Visual Narrowing & Spatial Pan
      CurrentPhase = Phase.VisualNarrowing;
      string primaryTarget = GetPrimaryTargetId(q);

      // Dog (Cậu Vàng) runs to target side
      var targetUi = ActiveItemUIs.Find(u => string.Equals(u.ItemId, primaryTarget, StringComparison.OrdinalIgnoreCase));
      float targetPan = 0f;
      if (targetUi != null) {
        targetPan = SpatialAudioDuckingService.CalculatePan(targetUi.NormalizedX);
        if (Builder != null) Builder.DogAssist(targetUi.transform.position);
      }

      // Eliminate 1 wrong option (cất bớt 1 đáp án sai -> 50/50)
      var wrongToDismiss = ActiveItemUIs.Find(u => u.IsActive && !string.Equals(u.ItemId, primaryTarget, StringComparison.OrdinalIgnoreCase));
      if (wrongToDismiss != null) {
        wrongToDismiss.DismissNarrowOut();
      }

      Say("Look, doggy shows the way!", "Kìa, chú cún chỉ đường kìa!");

      if (AudioService != null) {
        AudioClip slowClip = q.slowEmphasizedAudio != null ? q.slowEmphasizedAudio.EditorAsset : null;
        if (slowClip == null) slowClip = VnVoicePack.Get(VoiceKeyFor(q, "slow"));
        AudioService.PlayVoicePrompt(slowClip, 1.0f, targetPan); // slow is BAKED (0.8x, natural pitch)
      }
    } else {
      // Tier 3: Guided Discovery & Hand-off
      CurrentPhase = Phase.GuidedDiscovery;
      string primaryTarget = GetPrimaryTargetId(q);

      // Lock all wrong items
      for (int i = 0; i < ActiveItemUIs.Count; i++) {
        var u = ActiveItemUIs[i];
        if (!string.Equals(u.ItemId, primaryTarget, StringComparison.OrdinalIgnoreCase)) {
          u.DismissNarrowOut();
        } else {
          u.PromoteHighlight();
        }
      }

      // Enqueue to ReviewQueue with BufferGap >= 2
      EnqueueReviewQuestion(q.questionId, 2);

      Say("Here it is, put in basket!", "Đây rồi, bỏ vào giỏ nhé!");

      if (AudioService != null) {
        AudioClip teachClip = q.teachBackAudio != null ? q.teachBackAudio.EditorAsset : null;
        if (teachClip == null) teachClip = VnVoicePack.Get(VoiceKeyFor(q, "teach"));
        AudioService.PlayVoicePrompt(teachClip, 1.0f, 0f);
      }
      if (Builder != null) {
        Builder.GrandmaHandOff();
      }
    }
  }

  void EnqueueReviewQuestion(string qId, int gap) {
    if (string.IsNullOrEmpty(qId)) return;
    foreach (var e in ReviewQueue) {
      if (e.QuestionId == qId) return; // one pending repeat per question (no queue flooding)
    }
    ReviewQueue.Enqueue(new ReviewEntry {
      QuestionId = qId,
      AllowedAtQuestionIndex = QuestionIndex + gap
    });
  }

  public void HandleSuccess() {
    CurrentPhase = Phase.SuccessCelebration;
    _phaseTimer = 0f;

    PlaySfx("ui_correct");
    ActivityFeedback.Correct();
    Say("Wonderful listening, good job baby!", "Bé nghe giỏi tuyệt vời!");

    // Reveal labels for all items
    for (int i = 0; i < ActiveItemUIs.Count; i++) {
      ActiveItemUIs[i].SetLabelVisible(true);
    }

    if (Builder != null) {
      Builder.CelebrateAll();
    }

    QuestionIndex++;
    ActivityFeedback.ProgressKeep(QuestionIndex, TotalQuestions);
  }

  public void Tick(float dt) {
    if (_voice != null) _voice.Tick(dt);
    if (AudioService != null) AudioService.Tick(dt);

    for (int i = 0; i < ActiveItemUIs.Count; i++) {
      if (ActiveItemUIs[i] != null) ActiveItemUIs[i].Tick(dt);
    }

    if (CurrentPhase == Phase.PromptPlayback) {
      _phaseTimer += dt;
      if (_phaseTimer >= 1.2f) {
        CurrentPhase = Phase.AwaitingResponse;
      }
    } else if (CurrentPhase == Phase.SuccessCelebration) {
      _phaseTimer += dt;
      if (_phaseTimer >= PhaseDelay) {
        CheckReviewQueueAndAdvance();
      }
    }
  }

  void CheckReviewQueueAndAdvance() {
    if (ReviewQueue.Count > 0 && QuestionIndex < TotalQuestions) {
      var nextReview = ReviewQueue.Peek();
      if (QuestionIndex >= nextReview.AllowedAtQuestionIndex) {
        ReviewQueue.Dequeue();
        var rQ = _questions.Find(x => x.questionId == nextReview.QuestionId);
        if (rQ != null) {
          // Actually REPEAT the missed question now (spaced repetition). The old code dequeued
          // and did nothing — the advertised BufferGap >= 2 was a no-op.
          _questions[QuestionIndex] = rQ;
        }
      }
    }
    if (QuestionIndex >= TotalQuestions) CompleteGame();
    else BeginQuestion();
  }

  void CompleteGame() {
    CurrentPhase = Phase.Complete;
    ReleaseCurrentItems();
    if (Lifecycle != null) {
      try { Lifecycle.MarkCompleted("village market listening finished"); } catch (Exception) { }
    }
    Say("Market day finished, great job!", "Đi chợ xong, bé giỏi quá!");
  }

  void ReleaseCurrentItems() {
    for (int i = 0; i < ActiveItemUIs.Count; i++) {
      if (ActiveItemUIs[i] != null) {
        if (PhotoLoader != null && !string.IsNullOrEmpty(ActiveItemUIs[i].ItemId)) {
          PhotoLoader.ReleasePhoto(ActiveItemUIs[i].ItemId);
        }
        DestroyImmediate(ActiveItemUIs[i].gameObject);
      }
    }
    ActiveItemUIs.Clear();
  }

  string GetPrimaryTargetId(ListeningQuestionSO q) {
    if (q == null || q.targetItemIdsInOrder == null || q.targetItemIdsInOrder.Count == 0) return "";
    int idx = Mathf.Clamp(CurrentSequenceStep, 0, q.targetItemIdsInOrder.Count - 1);
    return q.targetItemIdsInOrder[idx];
  }

  string GetPrimaryTargetDisplayName(ListeningQuestionSO q) {
    string id = GetPrimaryTargetId(q);
    var opt = FindOption(id);
    return opt != null && !string.IsNullOrEmpty(opt.displayName) ? opt.displayName : id;
  }

  // Voice-pack key: only the built-in curriculum ids are mastered (Q1_Cam/Q2_DuaHau/Q3_ChuoiSua).
  // Custom questions fall back to the honest text prompt until their clips are generated.
  string VoiceKeyFor(ListeningQuestionSO q, string suffix) {
    if (q == null || string.IsNullOrEmpty(q.questionId)) return null;
    if (q.questionId.StartsWith("Q1", System.StringComparison.OrdinalIgnoreCase)) return "mkt_q1_" + suffix;
    if (q.questionId.StartsWith("Q2", System.StringComparison.OrdinalIgnoreCase)) return "mkt_q2_" + suffix;
    if (q.questionId.StartsWith("Q3", System.StringComparison.OrdinalIgnoreCase)) return "mkt_q3_" + suffix;
    return null;
  }

  MarketItemOption FindOption(string itemId) {
    if (CurrentQuestion == null || CurrentQuestion.allOptionsOnStall == null) return null;
    return CurrentQuestion.allOptionsOnStall.Find(o => string.Equals(o.itemId, itemId, StringComparison.OrdinalIgnoreCase));
  }

  Vector3 GetBasketPosition() {
    if (Builder != null && Builder.BasketDropTarget != null) {
      return Builder.BasketDropTarget.position;
    }
    return new Vector3(2.2f, 0.2f, 0.8f);
  }

  void Say(string en, string vi) {
    if (_voice != null) {
      _voice.Speak(DialogueLang.T(en, vi));
    }
  }

  void PlaySfx(string sfxName) {
    if (AudioDirector != null) {
      AudioDirector.PlaySfx(new SfxId(sfxName));
    }
  }

  void OnDestroy() {
    ReleaseCurrentItems();
    if (AudioService != null) AudioService.Dispose();
  }

  // Fallback 3-Question Curriculum covering all 3 modes
  public static List<ListeningQuestionSO> GenerateDefaultCurriculum() {
    var list = new List<ListeningQuestionSO>();

    // Q1: Level 1 - Direct Noun (Quả Cam vs Quả Táo vs Củ Cà Rốt)
    var q1 = ListeningQuestionSO.CreateInstance("Q1_Cam", ListeningMode.Level1_DirectNoun,
      new List<MarketItemOption> {
        new MarketItemOption("orange", "Quả Cam"),
        new MarketItemOption("apple", "Quả Táo"),
        new MarketItemOption("carrot", "Củ Cà Rốt")
      },
      new List<string> { "orange" }
    );
    list.Add(q1);

    // Q2: Level 2 - Attribute Riddle (Quả dưa hấu: vỏ xanh, ruột đỏ)
    var q2 = ListeningQuestionSO.CreateInstance("Q2_DuaHau", ListeningMode.Level2_AttributeRiddle,
      new List<MarketItemOption> {
        new MarketItemOption("watermelon", "Quả Dưa Hấu"),
        new MarketItemOption("banana", "Quả Chuối"),
        new MarketItemOption("orange", "Quả Cam")
      },
      new List<string> { "watermelon" }
    );
    list.Add(q2);

    // Q3: Level 3 - Sequential Order (Chuối trước -> Sữa sau)
    var q3 = ListeningQuestionSO.CreateInstance("Q3_ChuoiSua", ListeningMode.Level3_SequentialOrder,
      new List<MarketItemOption> {
        new MarketItemOption("banana", "Quả Chuối"),
        new MarketItemOption("milk", "Hộp Sữa"),
        new MarketItemOption("bread", "Ổ Bánh Mì"),
        new MarketItemOption("apple", "Quả Táo")
      },
      new List<string> { "banana", "milk" }
    );
    list.Add(q3);

    return list;
  }
}
