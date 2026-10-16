// A_World/PhonicsGarden/PhonicsGarden.cs — Gameplay Controller for "Vườn Đánh Vần" & "Vườn Tri Thức".
// State Pattern + 3-Tier Scaffolding + Spaced Repetition + Memory-Safe Photo Release.
// Pure C# 9.0 only. EditMode-testable without UnityEngine scene dependencies.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PhonicsGarden : MonoBehaviour {
  public enum Phase {
    Wait,
    IntroSpelling,
    WaitingChoice,
    MistakeScaffold1,
    MistakeScaffold2,
    MistakeScaffold3,
    SuccessFeedback,
    Done
  }

  public Phase CurrentPhase { get; private set; }
  public PhonicsPlayMode Mode { get; private set; }
  public PhonicsWordData CurrentWord { get; private set; }
  public ScaffoldingTier CurrentTier { get; private set; }
  public int MistakeCount { get; private set; }
  public int QuestionIndex { get; private set; }
  public int TotalQuestions { get; private set; } = 5;
  public bool Completed { get; private set; }
  public bool LastChoiceCorrect { get; private set; }
  public PhonicsChoiceCard SelectedCard { get; private set; }

  public SpacedRepetitionTracker SpacedTracker { get; private set; } = new SpacedRepetitionTracker();
  public IRealPhotoAssetLoader PhotoLoader { get; private set; }

  PhonicsGardenBuilder _builder;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PacedVoice _voice;
  float _phaseT;
  bool _waitCalled;
  float _restT;
  bool _finishAfterRest;
  float _idleT;
  float _scaffoldT;
  readonly Queue<string> _askEn = new Queue<string>();
  readonly Queue<string> _askVi = new Queue<string>();
  readonly List<string> _usedWordIds = new List<string>();

  public void Build(PhonicsGardenBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life, IRealPhotoAssetLoader photoLoader = null,
      PhonicsPlayMode mode = PhonicsPlayMode.SpellingGarden) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _voice = new PacedVoice();
    _voice.Audio = audio;
    _voice.LogTag = "PhonicsGarden";
    PhotoLoader = photoLoader ?? new MemorySafePhotoLoader();
    Mode = mode;

    if (_life != null) {
      try {
        if (_life.State == ActivityState.Completed) { Adopt(); return; }
        _life.MarkAvailable("phonics garden offered");
      } catch (Exception) { }
    }

    CurrentPhase = Phase.Wait;
    QuestionIndex = 0;
    _phaseT = 0f;
    Follow();
  }

  void Update() { Tick(Time.deltaTime); }

  public void Tick(float dt) {
    if (dt <= 0f || _builder == null) {
      if (_voice != null) _voice.Tick(dt);
      return;
    }
    if (_voice != null) _voice.Tick(dt);
    TickAsk();

    if (Completed && CurrentPhase == Phase.Done) return;
    if (_scaffoldT > 0f) {
      _scaffoldT -= dt;
      if (_scaffoldT <= 0f) ResumeWaitingChoice();
    }
    if (_restT > 0f) {
      _restT -= dt;
      if (_restT <= 0f) AfterWin();
      return;
    }

    _phaseT += dt;
    if (CurrentPhase == Phase.Wait) {
      TickWait(dt);
    } else if (CurrentPhase == Phase.IntroSpelling) {
      TickIntroSpelling(dt);
    } else if (CurrentPhase != Phase.Done) {
      _idleT += dt;
      if (_idleT >= 8f && _idleT - dt < 8f) NpcRemind();
      else if (_idleT >= 15f && _idleT - dt < 15f) NpcRemind();
    }
  }

  void TickWait(float dt) {
    if (!_waitCalled && _phaseT >= 1.0f) {
      _waitCalled = true;
      if (_builder != null) _builder.TeacherWave();
      Say("Come to the garden!", "Con ra vườn nhé!");
      ActivityGuide.PointAt(SpotWorld());
    }
    if (PlayerNear(SpotWorld(), 1.5f) || _phaseT >= 6f) {
      BeginQuestion();
    }
  }

  void TickIntroSpelling(float dt) {
    // Wait brief moment for audio / teacher spelling presentation before activating cards
    if (_phaseT >= 1.2f) {
      CurrentPhase = Phase.WaitingChoice;
      _phaseT = 0f;
      _idleT = 0f;
      SetCardsInteractable(true);
    }
  }

  void TickAsk() {
    if (_askEn.Count <= 0 || _voice == null || !_voice.Idle || _voice.HasLine) return;
    Say(_askEn.Dequeue(), _askVi.Dequeue());
  }

  public void BeginQuestion() {
    if (Completed) return;

    // Release previously loaded photo handle to ensure strict memory safety
    if (CurrentWord != null && PhotoLoader != null) {
      PhotoLoader.ReleasePhoto(CurrentWord.RealPhotoAddress);
    }

    // Pick next word: first check Spaced Repetition review queue
    string reviewId = SpacedTracker.DequeueReviewWord();
    if (!string.IsNullOrEmpty(reviewId)) {
      CurrentWord = PhonicsDatabase.GetWord(reviewId);
    }

    if (CurrentWord == null) {
      // Pick next unused word or sequential
      for (int i = 0; i < PhonicsDatabase.Words.Length; i++) {
        PhonicsWordData candidate = PhonicsDatabase.Words[i];
        if (!_usedWordIds.Contains(candidate.WordId)) {
          CurrentWord = candidate;
          _usedWordIds.Add(candidate.WordId);
          break;
        }
      }
    }

    // Fallback if all used
    if (CurrentWord == null && PhonicsDatabase.Words.Length > 0) {
      CurrentWord = PhonicsDatabase.Words[QuestionIndex % PhonicsDatabase.Words.Length];
    }

    MistakeCount = 0;
    CurrentTier = ScaffoldingTier.InitialAttempt;
    SelectedCard = null;
    LastChoiceCorrect = false;
    _phaseT = 0f;
    _idleT = 0f;

      if (_builder != null) {
        _builder.ClearRound();
        if (_builder.Board != null) {
          _builder.Board.SetWord(CurrentWord != null ? CurrentWord.FullWord : "");
          _builder.Board.HidePhotoFrame();
          // Teacher says the word (mastered clip: "cá"/"con cá") — text-only fallback otherwise
          if (CurrentWord != null) {
            _builder.Board.PlayClip(VnVoicePack.Get(NameClipKey(CurrentWord)));
          }
        }

        if (CurrentWord != null) {
          _builder.SpawnChoiceCards(this, CurrentWord.FullWord, CurrentWord.Distractors);
        }
      }

    CurrentPhase = Phase.IntroSpelling;
    SetCardsInteractable(false);

    if (Mode == PhonicsPlayMode.RealWorldDiscovery) {
      // In Discovery mode: immediately display the picture clue (real photo when registered)
      ShowPictureClue();
      Say("What is this?", "Đây là gì nào?");
    } else {
      // In Spelling mode: Teacher pronounces/spells
      Say("Listen to teacher carefully.", "Hãy lắng nghe cô nhé.");
    }
  }

  public void TrySelectChoice(PhonicsChoiceCard card) {
    if (CurrentPhase != Phase.WaitingChoice || card == null) return;

    SelectedCard = card;
    LastChoiceCorrect = card.IsCorrect;

    if (card.IsCorrect) {
      HandleCorrectChoice(card);
    } else {
      HandleMistakeChoice(card);
    }
  }

  void HandleCorrectChoice(PhonicsChoiceCard card) {
    CurrentPhase = Phase.SuccessFeedback;
    SetCardsInteractable(false);
    card.SetHighlight(new Color(0.35f, 0.88f, 0.35f)); // green success

    if (_builder != null) {
      _builder.TeacherCelebrate();
      if (_builder.Board != null) {
        _builder.Board.HighlightWord(new Color(1f, 0.88f, 0.2f));
      }
    }

    PlaySfx("ui_correct");
    ActivityFeedback.Correct();
    Say("Great job, you did it!", "Đúng rồi, bé giỏi lắm!");

    QuestionIndex++;
    ActivityFeedback.ProgressKeep(QuestionIndex, TotalQuestions);

    bool finish = QuestionIndex >= TotalQuestions;
    RestThenAdvance(finish);
  }

  void HandleMistakeChoice(PhonicsChoiceCard card) {
    MistakeCount++;
    card.SetHighlight(new Color(0.92f, 0.35f, 0.35f)); // soft red feedback
    PlaySfx("ui_mistake");

    if (MistakeCount == 1) {
      // Tier 1: Sai lần 1 — Đọc lại kèm động viên
      CurrentTier = ScaffoldingTier.MistakeRepeat;
      CurrentPhase = Phase.MistakeScaffold1;
      if (_builder != null) _builder.TeacherEncourage();
      Say("Listen again, keep trying!", "Con nghe lại nhé!");
      // "Đọc lại" = replay the mastered word clip for the child
      if (_builder != null && _builder.Board != null && CurrentWord != null) {
        _builder.Board.PlayClip(VnVoicePack.Get(NameClipKey(CurrentWord)));
      }
      _scaffoldT = 1.0f;
    } else if (MistakeCount == 2) {
      // Tier 2: Sai lần 2 — Mở mỏ neo thị giác (ảnh thật khi có, nếu không là hình minh họa)
      CurrentTier = ScaffoldingTier.MistakePhotoClue;
      CurrentPhase = Phase.MistakeScaffold2;
      ShowPictureClue();
      if (_builder != null) _builder.TeacherEncourage();
      Say("Look at the picture clue!", "Xem hình gợi ý nhé!");
      _scaffoldT = 1.2f;
    } else {
      // Tier 3: Sai lần 3 — Dạy chi tiết đánh vần + Cho vào hàng đợi ôn tập (Spaced Repetition)
      CurrentTier = ScaffoldingTier.MistakeTeachBack;
      CurrentPhase = Phase.MistakeScaffold3;
      SetCardsInteractable(false);

      if (CurrentWord != null) {
        SpacedTracker.RecordMistake(CurrentWord.WordId);
      }

      HighlightCorrectCard();

      Say("Let us spell together now!", "Cùng học đánh vần nhé!");

      // Explicit phonics instruction on the board, then the full teaching spell-line
      if (_builder != null && _builder.Board != null && CurrentWord != null) {
        _builder.Board.PlayExplicitPhonics(CurrentWord.Segments, () => {
          QuestionIndex++;
          ActivityFeedback.ProgressKeep(QuestionIndex, TotalQuestions);
          bool finish = QuestionIndex >= TotalQuestions;
          RestThenAdvance(finish);
        }, "spell_" + CurrentWord.WordId);
      } else {
        QuestionIndex++;
        bool finish = QuestionIndex >= TotalQuestions;
        RestThenAdvance(finish);
      }
    }
  }

  // Voice-pack key for the teacher saying the word itself
  // ("word_ca" in the spelling garden, "disc_ca" in the discovery garden).
  string NameClipKey(PhonicsWordData w) {
    if (w == null || string.IsNullOrEmpty(w.WordId)) return null;
    return (Mode == PhonicsPlayMode.RealWorldDiscovery ? "disc_" : "word_") + w.WordId;
  }

  void ShowPictureClue() {
    if (CurrentWord == null || PhotoLoader == null || _builder == null || _builder.Board == null) return;
    PhotoLoader.LoadPhotoAsync(CurrentWord.RealPhotoAddress, sprite => {
      if (_builder != null && _builder.Board != null) {
        _builder.Board.ShowPictureClue(sprite, CurrentWord.RealPhotoAddress);
      }
    });
  }

  void HighlightCorrectCard() {
    if (_builder == null) return;
    for (int i = 0; i < _builder.Choices.Count; i++) {
      PhonicsChoiceCard c = _builder.Choices[i];
      if (c != null && c.IsCorrect) {
        c.SetHighlight(new Color(0.35f, 0.88f, 0.35f));
      }
    }
  }

  public void ResumeWaitingChoice() {
    if (Completed) return;
    _scaffoldT = 0f;
    CurrentPhase = Phase.WaitingChoice;
    // Reset non-correct card colors back to cream
    if (_builder != null) {
      for (int i = 0; i < _builder.Choices.Count; i++) {
        PhonicsChoiceCard c = _builder.Choices[i];
        if (c != null && !c.IsCorrect) {
          c.SetHighlight(new Color(0.99f, 0.95f, 0.85f));
        }
      }
    }
  }

  void SetCardsInteractable(bool interactable) {
    if (_builder == null) return;
    for (int i = 0; i < _builder.Choices.Count; i++) {
      PhonicsChoiceCard c = _builder.Choices[i];
      if (c != null) c.SetClickable(interactable);
    }
  }

  void RestThenAdvance(bool finish) {
    _finishAfterRest = finish;
    _restT = 1.6f;
  }

  void AfterWin() {
    _restT = 0f;
    if (_finishAfterRest) FinishVisit();
    else BeginQuestion();
  }

  void FinishVisit() {
    Completed = true;
    CurrentPhase = Phase.Done;
    SetCardsInteractable(false);

    if (PhotoLoader != null) {
      PhotoLoader.ReleaseAll();
    }

    if (_builder != null) {
      _builder.TeacherCelebrate();
      if (_builder.Board != null) _builder.Board.HidePhotoFrame();
    }

    Say("You completed all words!", "Bé học thật xuất sắc!");
    if (_life != null) {
      try { _life.MarkCompleted("phonics garden finished"); } catch (Exception) { }
    }
  }

  void Adopt() {
    Completed = true;
    CurrentPhase = Phase.Done;
    if (_builder != null) _builder.ClearRound();
  }

  void NpcRemind() {
    if (_builder != null) _builder.TeacherEncourage();
    Say("Listen again, keep trying!", "Con nghe lại nhé!");
  }

  void Say(string en, string vi) {
    if (_voice == null) return;
    _voice.Speak(DialogueLang.T(en, vi));
  }

  void PlaySfx(string id) {
    if (_audio == null) return;
    try { _audio.PlaySfx(new SfxId(id)); } catch (Exception) { }
  }

  void Follow() {
    if (_cam == null || _player == null) return;
    try { _cam.Follow(_player, PhonicsGardenBuilder.FollowOffset); } catch (Exception) { }
  }

  bool PlayerNear(Vector3 world, float r) {
    if (_player == null) return true;
    Vector3 d = _player.position - world;
    d.y = 0f;
    return d.sqrMagnitude <= r * r;
  }

  Vector3 SpotWorld() {
    return _builder != null
      ? _builder.transform.TransformPoint(PhonicsGardenBuilder.PlaySpotLocal)
      : PhonicsGardenBuilder.PlaySpotLocal;
  }

  void OnDestroy() {
    if (PhotoLoader != null) {
      PhotoLoader.ReleaseAll();
    }
  }
}
