// A_World/SentenceExpress/SentenceExpress.cs — "Đoàn Tàu Ghép Câu"
// (English -> Sentences for 4-year-olds).
// Progressive 3-stage curriculum specially calibrated for preschoolers:
//   Level 1: Ghép chữ cái tạo từ ngắn (CVC phonics: C - A - T -> CAT, C - A - R -> CAR)
//   Level 2: Ghép cụm 2 từ (Big + Ball, Red + Apple)
//   Level 3: Ghép câu 3 từ mầm non (I + see + a car)
// C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SentenceExpress : MonoBehaviour {
  public enum Phase { Wait, Play, Done }

  public Phase Current { get; private set; }
  public int Level { get; private set; }
  public int RoundIndex { get; private set; }
  public bool Completed { get; private set; }
  public int CurrentStep { get; private set; }
  public int WrongCount { get; private set; }
  public bool LastCorrect { get; private set; }
  public string FullTargetSentence { get; private set; }
  public int CarCount { get { return _builder != null ? _builder.Choices.Count : 0; } }

  SentenceExpressBuilder _builder;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PacedVoice _voice;
  Transform _fx;
  float _phaseT;
  bool _waitSaid;
  float _restT;
  float _idleT;
  bool _finishAfterRest;
  bool _introSaid;

  // 3-Stage Curriculum calibrated for 4-year-olds
  // Level 1: Letters (C-A-R, C-A-T)
  static readonly string[][] Level1Items = {
    new string[] { "C", "A", "R" },
    new string[] { "C", "A", "T" },
  };
  static readonly string[] Level1Words = { "car", "cat" };

  // Level 2: 2-Word Collocations (Big car, Red apple)
  static readonly string[][] Level2Items = {
    new string[] { "Red", "Apple" },
    new string[] { "Big", "Ball" },
  };

  // Level 3: 3-Word Baby Sentences (I see car, It is apple)
  static readonly string[][] Level3Items = {
    new string[] { "I", "see", "a car" },
    new string[] { "It", "is", "an apple" },
  };

  public int MissingIndex { get; private set; }
  public string MissingLetter { get; private set; }

  public void Build(SentenceExpressBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _voice = new PacedVoice();
    _voice.Audio = audio;
    _voice.LogTag = "SentenceExpress";
    _fx = builder != null ? builder.transform : transform;
    if (_life != null) {
      try {
        if (_life.State == ActivityState.Completed) { Adopt(); return; }
        _life.MarkAvailable("sentence express offered");
      } catch (Exception) { }
    }
    Level = 1;
    RoundIndex = 0;
    Current = Phase.Wait;
    _phaseT = 0f;
    Follow();
  }

  void Update() { Tick(Time.deltaTime); }

  bool _isTrainWarmingUp;
  float _trainWarmUpT;
  bool _isTrainRolling;
  float _trainRollT;

  public void Tick(float dt) {
    if (_voice != null) _voice.Tick(dt);
    if (dt <= 0f || _builder == null) return;

    // 1. Train engine warm up phase (chugging steam, revving up whistle)
    if (_isTrainWarmingUp) {
      _trainWarmUpT -= dt;
      if (_builder.EngineTransform != null) {
        // Rhythmic engine puffing while preparing to depart
        if (Mathf.Repeat(_trainWarmUpT, 0.22f) < dt) {
          MagicJuice.MagicPuffSteam(_builder.EngineTransform, new Vector3(0.75f, 1.4f, 0f), MagicJuice.MagicPink, 0.5f);
          MagicJuice.MagicPuffSteam(_builder.EngineTransform, new Vector3(0.75f, 1.4f, 0f), MagicJuice.MagicSky, 0.45f);
        }
        // Train vibration/shudder during warm up
        float shake = Mathf.Sin(Time.time * 30f) * 0.02f;
        _builder.EngineTransform.localPosition = SentenceExpressBuilder.EngineLocal + new Vector3(0f, shake, 0f);
      }
      if (_trainWarmUpT <= 0f) {
        _isTrainWarmingUp = false;
        _isTrainRolling = true;
        _trainRollT = 0f;
        PlaySfx("success");
      }
    }

    // 2. Train rolling phase along the extended track
    if (_isTrainRolling) {
      _trainRollT += dt;
      // Animate entire train along the track
      if (_builder.RoundRoot != null) {
        _builder.RoundRoot.position += Vector3.right * (3.0f * dt);
      }
      if (_builder.EngineTransform != null) {
        _builder.EngineTransform.position += Vector3.right * (3.0f * dt);
        // Puff colorful steam continuously while moving
        if (Mathf.Repeat(_trainRollT, 0.25f) < dt) {
          MagicJuice.MagicPuffSteam(_builder.EngineTransform, new Vector3(0.75f, 1.4f, 0f), MagicJuice.MagicSky, 0.5f);
          MagicJuice.MagicPuffSteam(_builder.EngineTransform, new Vector3(0.75f, 1.4f, 0f), MagicJuice.MagicPink, 0.55f);
        }
      }
    }

    if (Completed && Current == Phase.Done) return;
    if (_restT > 0f) {
      _restT -= dt;
      if (_restT <= 0f) AfterRest();
      return;
    }
    _phaseT += dt;
    if (Current == Phase.Wait) TickWait(dt);
    else if (Current == Phase.Play) {
      _idleT += dt;
    }
  }

  void TickWait(float dt) {
    if (!_waitSaid && _phaseT >= 1.2f) {
      _waitSaid = true;
      Say("Come to the train station!", "Con ra ga xe lửa nhé!");
      ActivityGuide.PointAt(SpotWorld());
    }
    if (PlayerNear(SpotWorld(), 1.5f) || _phaseT >= 8f) BeginRound();
  }

  public void BeginRound() {
    if (Completed) return;
    Current = Phase.Play;
    _phaseT = 0f;
    _idleT = 0f;
    _restT = 0f;
    CurrentStep = 0;
    _isTrainRolling = false;
    _trainRollT = 0f;

    // Reset Engine position to station anchor
    if (_builder != null && _builder.EngineTransform != null) {
      _builder.EngineTransform.localPosition = SentenceExpressBuilder.EngineLocal;
    }
    if (_builder != null && _builder.RoundRoot != null) {
      _builder.RoundRoot.localPosition = Vector3.zero;
    }

    string[] parts;
    string wordForPic = "car";
    if (Level == 1) {
      parts = Level1Items[RoundIndex % Level1Items.Length];
      FullTargetSentence = Level1Words[RoundIndex % Level1Words.Length];
      wordForPic = FullTargetSentence;
    } else if (Level == 2) {
      parts = Level2Items[RoundIndex % Level2Items.Length];
      FullTargetSentence = string.Join(" ", parts);
      wordForPic = (RoundIndex % 2 == 0) ? "apple" : "ball";
    } else {
      parts = Level3Items[RoundIndex % Level3Items.Length];
      FullTargetSentence = string.Join(" ", parts);
      wordForPic = (RoundIndex % 2 == 0) ? "car" : "apple";
    }

    _builder.ClearRound();

    // 1. Display 3D Picture Model of target word
    _builder.ShowWordPicture(wordForPic);

    // 2. Select missing letter/word index (for 4yo: hide index 1 or 0)
    MissingIndex = Mathf.Min(1, parts.Length - 1);
    MissingLetter = parts[MissingIndex];

    // 3. Spawn train cars
    for (int i = 0; i < parts.Length; i++) {
      bool isMissing = (i == MissingIndex);
      SentenceCarChoice car = _builder.SpawnCar(i, parts.Length, parts[i], isMissing);
      if (car != null) car.Game = this;
    }

    // 4. Generate 4 options for the child (1 correct + 3 distractors)
    List<string> options = GenerateChoices(MissingLetter);
    for (int i = 0; i < options.Count; i++) {
      string opt = options[i];
      SentenceLetterChoice choice = _builder.SpawnLetterChoice(i, options.Count, opt, opt == MissingLetter);
      if (choice != null) choice.Game = this;
    }

    ActivityFeedback.ProgressKeep(RoundIndex, 2);
    if (_life != null && _life.State == ActivityState.Available) {
      try { _life.Begin("sentence round"); } catch (Exception) { }
    }

    if (!_introSaid) {
      _introSaid = true;
      Say("Look at the picture and find the missing letter for the train!",
          "Nhìn hình và chọn chữ cái còn thiếu cho đoàn tàu nhé!");
    } else {
      Say("What is the missing letter for: " + FullTargetSentence + "?",
          "Chữ cái còn thiếu của " + FullTargetSentence + " là gì nào?");
    }
  }

  static readonly string[] DistractorPool = { "A", "B", "C", "D", "E", "M", "O", "P", "R", "S", "T" };

  List<string> GenerateChoices(string correctLetter) {
    List<string> list = new List<string>();
    list.Add(correctLetter);
    int distractorIdx = 0;
    while (list.Count < 4 && distractorIdx < DistractorPool.Length) {
      string candidate = DistractorPool[distractorIdx++];
      if (!list.Contains(candidate)) list.Add(candidate);
    }
    // Simple deterministic shuffle so it feels varied
    if (list.Count >= 4) {
      string tmp = list[0];
      int swapIdx = (RoundIndex + 1) % 4;
      list[0] = list[swapIdx];
      list[swapIdx] = tmp;
    }
    return list;
  }

  // When child taps one of the 4 letter choices
  public void TrySelectLetter(SentenceLetterChoice choice) {
    if (choice == null || Completed) return;
    if (Current != Phase.Play || _restT > 0f) return;
    _idleT = 0f;

    if (_player != null) {
      ClickToMove ctm = _player.GetComponent<ClickToMove>();
      if (ctm != null) ctm.Stop();
    }

    if (choice.IsCorrect) {
      choice.PlayCorrectFx();
      PlaySfx("ding");
      LastCorrect = true;

      // Reveal letter in the missing wagon
      if (_builder != null && MissingIndex >= 0 && MissingIndex < _builder.CarWagons.Count) {
        SentenceCarChoice wagon = _builder.CarWagons[MissingIndex];
        if (wagon != null) wagon.RevealLetter(choice.Letter);
      }

      WinRound();
    } else {
      choice.PlayWrongFx();
      Reject();
    }
  }

  // Backward compatibility for direct car tapping
  public void TryTapCar(SentenceCarChoice car) {
    if (car == null || Completed) return;
    if (Current != Phase.Play || _restT > 0f) return;
    _idleT = 0f;

    if (_player != null) {
      ClickToMove ctm = _player.GetComponent<ClickToMove>();
      if (ctm != null) ctm.Stop();
    }

    if (car.CorrectStepOrder == CurrentStep && !car.IsCoupled) {
      car.PlayCoupleFx();
      PlaySfx("ding");
      CurrentStep++;
      LastCorrect = true;
      Say(car.CardText, car.CardText);
      if (CurrentStep >= _builder.CarWagons.Count) WinRound();
    } else if (!car.IsCoupled) {
      car.PlayWrongWobble();
      Reject();
    }
  }

  void Reject() {
    WrongCount++;
    LastCorrect = false;
    Say("Try again! Which letter is missing?", "Thử lại nào! Chữ cái nào còn thiếu nhỉ?");
    ActivityFeedback.Retry();
    GameJuice.WrongFx(_builder != null ? _builder.transform : transform, _fx,
      _player != null ? _player.position : Vector3.zero);
    if (_builder != null && _builder.BunnyConductorTransform != null) {
      GameJuice.Wobble(_builder.BunnyConductorTransform, 10f, 0.45f);
    }
  }

  void WinRound() {
    LastCorrect = true;
    PlaySfx("ding");
    GameJuice.CorrectFx(_fx, _player != null ? _player.position : Vector3.zero, false);

    // Locomotive train warm up / engine revving for departure!
    _isTrainWarmingUp = true;
    _trainWarmUpT = 1.35f;
    _isTrainRolling = false;
    _trainRollT = 0f;

    if (_builder != null) {
      MagicJuice.FairySparkleBurst(_builder.transform, new Vector3(0f, 1.2f, SentenceExpressBuilder.RailZ), 24);
      MagicJuice.MagicHaloRing(_builder.transform, new Vector3(0f, 0.5f, SentenceExpressBuilder.RailZ), MagicJuice.MagicGold, 3.0f);
      if (_builder.EngineTransform != null) {
        MagicJuice.MagicPuffSteam(_builder.EngineTransform, new Vector3(0.75f, 1.4f, 0f), MagicJuice.MagicPink, 0.6f);
      }
      if (_builder.BunnyConductorTransform != null) {
        GameJuice.Pop(_builder.BunnyConductorTransform, 0.35f, 0.55f);
      }
    }

    ActivityFeedback.Correct();
    if (_builder != null && _builder.GuideNpc != null) _builder.GuideNpc.Celebrate();

    Say("Choo choo! Great job: " + FullTargetSentence + "!",
      "Tu tu xình xịch! Giỏi lắm: " + FullTargetSentence + "!");

    RoundIndex++;
    ActivityFeedback.ProgressKeep(RoundIndex, 2);
    bool finish = false;
    if (RoundIndex >= 2) {
      Level++;
      RoundIndex = 0;
      finish = Level > 3;
    }
    RestThenAdvance(finish);
  }

  void RestThenAdvance(bool finish) {
    _finishAfterRest = finish;
    _restT = Application.isPlaying ? 3.8f : 0f;
    if (_restT <= 0f) AfterRest();
  }

  void AfterRest() {
    _restT = 0f;
    if (_finishAfterRest) FinishVisit();
    else BeginRound();
  }

  void FinishVisit() {
    Completed = true;
    Current = Phase.Done;
    ActivityGuide.Clear();
    Say("The sentence train is rolling!", "Đoàn tàu ghép câu lăn bánh rồi!");
    if (_life != null) {
      try { _life.MarkCompleted("sentence express done"); } catch (Exception) { }
    }
  }

  void Adopt() {
    Completed = true;
    Current = Phase.Done;
    Level = 3;
  }

  public void SetLevelForTests(int level, int round) {
    Level = Mathf.Clamp(level, 1, 3);
    RoundIndex = Mathf.Max(0, round);
    BeginRound();
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
    try { _cam.Follow(_player, SentenceExpressBuilder.FollowOffset); } catch (Exception) { }
  }

  bool PlayerNear(Vector3 world, float r) {
    if (_player == null) return true;
    Vector3 d = _player.position - world;
    d.y = 0f;
    return d.sqrMagnitude <= r * r;
  }

  Vector3 SpotWorld() {
    return _builder != null
      ? _builder.transform.TransformPoint(SentenceExpressBuilder.PlaySpotLocal)
      : SentenceExpressBuilder.PlaySpotLocal;
  }
}
