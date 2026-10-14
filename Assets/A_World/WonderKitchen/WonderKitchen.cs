// A_World/WonderKitchen/WonderKitchen.cs — "Căn Bếp Thần Kỳ"
// (English -> Vocabulary). Interactive gameplay: Hear target ingredient,
// tap the matching tray, food flies into the magic cauldron, pot bubbles & erupts in sparkles!
// Voice-first, progressive rounds. C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WonderKitchen : MonoBehaviour {
  public enum Phase { Wait, Play, Done }

  public Phase Current { get; private set; }
  public int Level { get; private set; }
  public int RoundIndex { get; private set; }
  public bool Completed { get; private set; }
  public int WrongCount { get; private set; }
  public bool LastCorrect { get; private set; }
  public string TargetWord { get; private set; }
  public int ChoiceCount { get { return _builder != null ? _builder.Choices.Count : 0; } }

  WonderKitchenBuilder _builder;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PacedVoice _voice;
  Transform _fx;
  float _phaseT;
  bool _waitSaid;
  float _promptT;
  float _restT;
  float _slowReplayT;
  float _idleT;
  bool _finishAfterRest;
  bool _introSaid;

  public static readonly string[] FoodPool = {
    "apple", "banana", "orange", "carrot", "tomato", "watermelon", "grape"
  };

  public void Build(WonderKitchenBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _voice = new PacedVoice();
    _voice.Audio = audio;
    _voice.LogTag = "WonderKitchen";
    _fx = builder != null ? builder.transform : transform;
    if (_life != null) {
      try {
        if (_life.State == ActivityState.Completed) { Adopt(); return; }
        _life.MarkAvailable("wonder kitchen offered");
      } catch (Exception) { }
    }
    Level = 1;
    RoundIndex = 0;
    Current = Phase.Wait;
    _phaseT = 0f;
    Follow();
  }

  void Update() { Tick(Time.deltaTime); }

  float _ambientBubbleT;
  GameObject _activeDish;

  public void Tick(float dt) {
    if (_voice != null) _voice.Tick(dt);
    if (dt <= 0f || _builder == null) return;

    // Ambient floating magic bubbles from cauldron
    _ambientBubbleT += dt;
    if (_ambientBubbleT >= 2.5f) {
      _ambientBubbleT = 0f;
      if (_builder.CauldronTransform != null) {
        MagicJuice.FairyBubbles(_builder.CauldronTransform, new Vector3(0f, 0.9f, 0f), 3);
      }
    }

    if (_slowReplayT > 0f) {
      _slowReplayT -= dt;
      if (_slowReplayT <= 0f) PlayWord(true);
    }
    if (Completed && Current == Phase.Done) return;
    if (_restT > 0f) {
      _restT -= dt;
      if (_restT <= 0f) AfterRest();
      return;
    }
    if (_promptT > 0f) {
      _promptT -= dt;
      if (_promptT <= 0f) PlayWord(false);
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
      Say("Come to the magic kitchen!", "Con vào căn bếp thần kỳ nhé!");
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
    _slowReplayT = 0f;

    // Clear previous dish if any
    if (_activeDish != null) {
      CharacterPresentation.DestroyNow(_activeDish);
      _activeDish = null;
    }

    // Pick target food and 2 or 3 choices
    int choicesCount = Level == 1 ? 2 : 3;
    TargetWord = FoodPool[(RoundIndex + (Level - 1) * 3) % FoodPool.Length];

    List<string> choicesList = new List<string>();
    choicesList.Add(TargetWord);
    int distractorIdx = 0;
    while (choicesList.Count < choicesCount) {
      string cand = FoodPool[(RoundIndex + distractorIdx + 1) % FoodPool.Length];
      if (!choicesList.Contains(cand)) choicesList.Add(cand);
      distractorIdx++;
    }

    // Shuffle deterministic
    if (RoundIndex % 2 == 1 && choicesList.Count >= 2) {
      string tmp = choicesList[0];
      choicesList[0] = choicesList[1];
      choicesList[1] = tmp;
    }

    _builder.ClearRound();
    for (int i = 0; i < choicesList.Count; i++) {
      bool isTarget = choicesList[i] == TargetWord;
      WonderKitchenChoice choice = _builder.SpawnTray(i, choicesList.Count, choicesList[i], isTarget);
      if (choice != null) choice.Game = this;
    }

    ActivityFeedback.ProgressKeep(RoundIndex, 3);
    if (_life != null && _life.State == ActivityState.Available) {
      try { _life.Begin("kitchen round"); } catch (Exception) { }
    }

    if (!_introSaid) {
      _introSaid = true;
      Say("Add the right ingredient to the pot!", "Bỏ đúng nguyên liệu vào nồi nhé!");
      _promptT = 1.6f;
    } else {
      _promptT = 0.8f;
    }
  }

  public void TrySelectChoice(WonderKitchenChoice choice) {
    if (choice == null || Completed) return;
    if (Current != Phase.Play || _restT > 0f) return;
    _idleT = 0f;

    // Halt player
    if (_player != null) {
      ClickToMove ctm = _player.GetComponent<ClickToMove>();
      if (ctm != null) ctm.Stop();
    }

    if (choice.IsTarget) {
      Vector3 cauldronPos = _builder.CauldronTransform != null
        ? _builder.CauldronTransform.position
        : Vector3.zero;
      choice.FlyToCauldron(cauldronPos);
      WinRound();
    } else {
      choice.PlayWrongWobble();
      Reject();
    }
  }

  void Reject() {
    WrongCount++;
    LastCorrect = false;
    Say("Not that one. Listen again!", "Chưa phải món này. Nghe lại nhé!");
    ActivityFeedback.Retry();
    GameJuice.WrongFx(_builder != null ? _builder.transform : transform, _fx,
      _player != null ? _player.position : Vector3.zero);
    if (_builder != null && _builder.ChefKittenTransform != null) {
      GameJuice.Wobble(_builder.ChefKittenTransform, 8f, 0.4f);
    }
    _slowReplayT = 1.8f;
  }

  void WinRound() {
    LastCorrect = true;
    PlaySfx("success");
    GameJuice.CorrectFx(_fx, _player != null ? _player.position : Vector3.zero, false);
    
    // Cauldron eruption magic burst & steam puffs!
    if (_builder != null && _builder.CauldronTransform != null) {
      MagicJuice.FairySparkleBurst(_builder.CauldronTransform, new Vector3(0f, 1.1f, 0f), 24);
      MagicJuice.MagicHaloRing(_builder.CauldronTransform, new Vector3(0f, 0.9f, 0f), MagicJuice.MagicGold, 2.4f);
      MagicJuice.FairyBubbles(_builder.CauldronTransform, new Vector3(0f, 1.0f, 0f), 8);
      MagicJuice.MagicPuffSteam(_builder.CauldronTransform, new Vector3(0f, 1.2f, 0f), MagicJuice.MagicPink, 0.7f);
      
      // Spawn floating Princess Dish transformation!
      SpawnPrincessDish(_builder.DishAnchor);
    }

    // Chef Kitten joy reaction
    if (_builder != null && _builder.ChefKittenTransform != null) {
      GameJuice.Pop(_builder.ChefKittenTransform, 0.35f, 0.5f);
    }

    ActivityFeedback.Correct();
    RecordCorrectAnswer(TargetWord);
    if (_builder != null && _builder.GuideNpc != null) _builder.GuideNpc.Celebrate();

    Say("Yummy! " + Capitalize(ListeningWords.TextEn(TargetWord)) + "!",
      "Ngon quá! " + Capitalize(ListeningWords.DisplayVi(TargetWord)) + "!");

    RoundIndex++;
    ActivityFeedback.ProgressKeep(RoundIndex, 3);
    bool finish = false;
    if (RoundIndex >= 3) {
      Level++;
      RoundIndex = 0;
      finish = Level > 2;
    }
    RestThenAdvance(finish);
  }

  // Spawns a magical transformed princess cake / parfait hovering with sparkles
  void SpawnPrincessDish(Transform anchor) {
    if (anchor == null) return;
    if (_activeDish != null) CharacterPresentation.DestroyNow(_activeDish);

    GameObject dish = new GameObject("PrincessMagicalDish");
    dish.transform.SetParent(anchor, false);
    dish.transform.localPosition = Vector3.zero;
    _activeDish = dish;

    // Cake tier 1 (Pink)
    GameObject tier1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    tier1.name = "Tier1";
    tier1.transform.SetParent(dish.transform, false);
    tier1.transform.localPosition = new Vector3(0f, 0.1f, 0f);
    tier1.transform.localScale = new Vector3(0.7f, 0.2f, 0.7f);
    tier1.GetComponent<Renderer>().sharedMaterial = MagicJuice.Lit(MagicJuice.MagicPink);
    StripCol(tier1);

    // Cake tier 2 (Cream)
    GameObject tier2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    tier2.name = "Tier2";
    tier2.transform.SetParent(dish.transform, false);
    tier2.transform.localPosition = new Vector3(0f, 0.28f, 0f);
    tier2.transform.localScale = new Vector3(0.48f, 0.16f, 0.48f);
    tier2.GetComponent<Renderer>().sharedMaterial = MagicJuice.Lit(new Color(0.99f, 0.96f, 0.92f));
    StripCol(tier2);

    // Crown strawberry star on top
    GameObject star = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    star.name = "DishStar";
    star.transform.SetParent(dish.transform, false);
    star.transform.localPosition = new Vector3(0f, 0.46f, 0f);
    star.transform.localScale = Vector3.one * 0.22f;
    star.GetComponent<Renderer>().sharedMaterial = MagicJuice.Lit(MagicJuice.MagicGold);
    StripCol(star);

    GameJuice.Pop(dish.transform, 0.4f, 0.6f);
    MagicJuice.FairySparkleBurst(dish.transform, new Vector3(0f, 0.3f, 0f), 12);
  }

  static void StripCol(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (Exception) { }
  }

  void RecordCorrectAnswer(string wordId) {
    if (string.IsNullOrEmpty(wordId)) return;
    try {
      LocalSave save = new LocalSave();
      PlayerProgress progress = save.Load() ?? new PlayerProgress();
      if (progress.Words == null) progress.Words = new Dictionary<string, WordMastery>();
      WordMastery m;
      if (!progress.Words.TryGetValue(wordId, out m) || m == null) {
        m = new WordMastery { Id = new WordId(wordId), Stage = MasteryStage.Unknown };
        progress.Words[wordId] = m;
      }
      m.RecognitionHit++;
      m.RecognitionTotal++;
      save.Save(progress);
    } catch (Exception) { }
  }

  void RestThenAdvance(bool finish) {
    _finishAfterRest = finish;
    _restT = Application.isPlaying ? 1.6f : 0f;
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
    Say("The magic feast is ready!", "Bữa tiệc phép thuật đã xong rồi!");
    if (_life != null) {
      try { _life.MarkCompleted("wonder kitchen done"); } catch (Exception) { }
    }
  }

  void Adopt() {
    Completed = true;
    Current = Phase.Done;
    Level = 2;
  }

  public void SetLevelForTests(int level, int round) {
    Level = Mathf.Clamp(level, 1, 2);
    RoundIndex = Mathf.Max(0, round);
    BeginRound();
  }

  void PlayWord(bool slow) {
    if (_audio == null || string.IsNullOrEmpty(TargetWord)) return;
    ListenPackAudio.Play(_audio, TargetWord, slow);
  }

  static string Capitalize(string s) {
    if (string.IsNullOrEmpty(s)) return s;
    return char.ToUpperInvariant(s[0]) + s.Substring(1);
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
    try { _cam.Follow(_player, WonderKitchenBuilder.FollowOffset); } catch (Exception) { }
  }

  bool PlayerNear(Vector3 world, float r) {
    if (_player == null) return true;
    Vector3 d = _player.position - world;
    d.y = 0f;
    return d.sqrMagnitude <= r * r;
  }

  Vector3 SpotWorld() {
    return _builder != null
      ? _builder.transform.TransformPoint(WonderKitchenBuilder.PlaySpotLocal)
      : WonderKitchenBuilder.PlaySpotLocal;
  }
}
