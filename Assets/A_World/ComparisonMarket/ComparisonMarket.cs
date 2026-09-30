// A_World/ComparisonMarket/ComparisonMarket.cs — Khu Chợ Của Bé.
// Starts at LV3. Observe -> compare -> decide -> act. No timer, no game over.
// C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ComparisonMarket : MonoBehaviour {
  public enum Phase { Wait, Choose, Pair, Confirm, Deliver, Done }

  public const int FirstLevel = 3;
  public const int LastLevel = 10;

  public Phase Current { get; private set; }
  public int Level { get; private set; }
  public int RoundIndex { get; private set; }
  public ComparisonType CType { get; private set; }
  public TargetRelation Relation { get; private set; }
  public bool Completed { get; private set; }
  public int WrongCount { get; private set; }
  public int PairsDone { get; private set; }
  public int PairsNeed { get; private set; }
  public ComparisonChoice Selected { get; private set; }
  public bool LastCorrect { get; private set; }

  ComparisonMarketBuilder _builder;
  Transform _player;
  SmartCamera _cam;
  IAudioDirector _audio;
  ActivityLifecycle _life;
  PacedVoice _voice;
  Transform _fx;
  float _phaseT;
  bool _waitCalled;
  float _restT;
  bool _finishAfterRest;
  float _idleT;
  readonly Queue<string> _askEn = new Queue<string>();
  readonly Queue<string> _askVi = new Queue<string>();

  public void Build(ComparisonMarketBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _voice = new PacedVoice();
    _voice.Audio = audio;
    _voice.LogTag = "ComparisonMarket";
    _fx = builder != null ? builder.transform : transform;
    BindStatic();
    if (_life != null) {
      try {
        if (_life.State == ActivityState.Completed) { Adopt(); return; }
        _life.MarkAvailable("market offered");
      } catch (Exception) { }
    }
    Level = FirstLevel;
    RoundIndex = 0;
    Current = Phase.Wait;
    _phaseT = 0f;
    Follow();
  }

  void BindStatic() {
    if (_builder == null || _builder.CartChoice == null) return;
    _builder.CartChoice.Game = this;
  }

  void Update() { Tick(Time.deltaTime); }

  public void Tick(float dt) {
    if (dt <= 0f || _builder == null) {
      if (_voice != null) _voice.Tick(dt);
      return;
    }
    if (_voice != null) _voice.Tick(dt);
    TickAsk();
    if (Completed && Current == Phase.Done) return;
    if (_restT > 0f) {
      _restT -= dt;
      if (_restT <= 0f) AfterWin();
      return;
    }
    _phaseT += dt;
    if (Current == Phase.Wait) TickWait(dt);
    else if (Current != Phase.Done) {
      _idleT += dt;
      if (_idleT >= 8f && _idleT - dt < 8f) PointTask();
      else if (_idleT >= 15f && _idleT - dt < 15f) PointTask();
    }
  }

  void TickWait(float dt) {
    if (!_waitCalled && _phaseT >= 1.2f) {
      _waitCalled = true;
      Say("Come to the market!", "Con ra chợ nhé!");
      ActivityGuide.PointAt(SpotWorld());
    }
    if (PlayerNear(SpotWorld(), 1.5f) || _phaseT >= 8f) BeginRound();
  }

  void TickAsk() {
    if (_askEn.Count <= 0 || _voice == null || !_voice.Idle || _voice.HasLine) return;
    Say(_askEn.Dequeue(), _askVi.Dequeue());
  }

  public void BeginRound() {
    if (Completed) return;
    Current = Phase.Choose;
    _phaseT = 0f;
    Selected = null;
    PairsDone = 0;
    PairsNeed = 0;
    _idleT = 0f;
    ApplyRound();
    ActivityFeedback.ProgressKeep(RoundIndex, RoundsOf(Level));
    foreach (ComparisonChoice c in _builder.Choices)
      if (c != null) c.Game = this;
    foreach (ComparisonFruit f in _builder.Fruits)
      if (f != null && f.Game == null) f.Game = this;
    if (_life != null && _life.State == ActivityState.Available) {
      try { _life.Begin("market round"); } catch (Exception) { }
    }
    SpeakTask();
    PointTask();
  }

  void ApplyRound() {
    _builder.ClearRound();
    BindStatic();
    if (Level == 3) ApplyMoreLess();
    else if (Level == 4) ApplyInvariance();
    else if (Level == 5) ApplyPairing();
    else if (Level == 6) ApplyMismatch();
    else if (Level == 7) ApplySize();
    else if (Level == 8) ApplyLength();
    else if (Level == 9) ApplyThreeWay();
    else ApplyDelivery();
  }

  // LV3: 6 rounds, 2 baskets. askMore rotates; answer side rotates (no bias).
  void ApplyMoreLess() {
    CType = ComparisonType.Quantity;
    int[,] pairs = { { 3, 5 }, { 2, 4 }, { 4, 2 }, { 6, 4 }, { 3, 5 }, { 5, 3 } };
    bool[] more = { true, true, false, true, false, true };
    int i = RoundIndex % 6;
    int left = pairs[i, 0], right = pairs[i, 1];
    bool askMore = more[i];
    // Rotate sides on odd rounds so the answer is not always one side.
    if (i % 2 == 1) { int t = left; left = right; right = t; }
    Relation = askMore ? TargetRelation.More : TargetRelation.Less;
    int ans = ComparisonLogic.AnswerMoreLess(left, right, askMore);
    _builder.SpawnBasket(ComparisonMarketBuilder.SlotA, ComparisonMarketBuilder.BasketColor(i),
      left, 1f, ComparisonMarketBuilder.AppleColor(i), 0.32f, "L", ans == 0);
    _builder.SpawnBasket(ComparisonMarketBuilder.SlotB, ComparisonMarketBuilder.BasketColor(i + 1),
      right, 1f, ComparisonMarketBuilder.AppleColor(i + 1), 0.32f, "R", ans == 1);
  }

  // LV4: equal counts, different spacing. Both baskets answer: the point is EQUAL.
  void ApplyInvariance() {
    CType = ComparisonType.Quantity;
    Relation = TargetRelation.Equal;
    int[] counts = { 4, 3, 5, 4 };
    int n = counts[RoundIndex % 4];
    _builder.SpawnBasket(ComparisonMarketBuilder.SlotA, ComparisonMarketBuilder.BasketColor(0),
      n, 0.7f, ComparisonMarketBuilder.AppleColor(0), 0.32f, "L", false);
    _builder.SpawnBasket(ComparisonMarketBuilder.SlotB, ComparisonMarketBuilder.BasketColor(1),
      n, 1.6f, ComparisonMarketBuilder.AppleColor(1), 0.32f, "R", false);
    _builder.SpawnEqualPad(new Vector3(0f, 0f, 3.4f), "EQ");
  }

  // LV5: tap-to-pair, then tap the leftover group.
  void ApplyPairing() {
    CType = ComparisonType.Quantity;
    Relation = TargetRelation.More;
    Current = Phase.Pair;
    int[,] pairs = { { 3, 4 }, { 2, 3 }, { 4, 5 } };
    int i = RoundIndex % 3;
    int apples = pairs[i, 0], oranges = pairs[i, 1];
    PairsNeed = Math.Min(apples, oranges);
    for (int a = 0; a < apples; a++) {
      Vector3 p = new Vector3(-1.5f + (a % 3) * 0.55f, 0.5f, 2.6f + (a / 3) * 0.55f);
      ComparisonFruit f = _builder.SpawnFruit(p, 0.3f, ComparisonMarketBuilder.AppleColor(a), 0);
      f.Game = this;
    }
    for (int o = 0; o < oranges; o++) {
      Vector3 p = new Vector3(0.6f + (o % 3) * 0.55f, 0.5f, 2.6f + (o / 3) * 0.55f);
      ComparisonFruit f = _builder.SpawnFruit(p, 0.3f, ComparisonMarketBuilder.OrangeColor(), 1);
      f.Game = this;
    }
    bool leftMore = apples > oranges;
    _builder.SpawnBasket(ComparisonMarketBuilder.SlotA, ComparisonMarketBuilder.BasketColor(0),
      apples, 1f, ComparisonMarketBuilder.AppleColor(0), 0.3f, "L", leftMore);
    _builder.SpawnBasket(ComparisonMarketBuilder.SlotB, ComparisonMarketBuilder.BasketColor(1),
      oranges, 1f, ComparisonMarketBuilder.OrangeColor(), 0.3f, "R", !leftMore);
    foreach (ComparisonChoice c in _builder.Choices) c.Game = this;
  }

  // LV6: MORE with mismatched item sizes (size must not cue quantity).
  // Even rounds ask MORE, odd rounds are EQUAL (both answer).
  void ApplyMismatch() {
    CType = ComparisonType.Quantity;
    int i = RoundIndex % 4;
    if (i % 2 == 1) {
      Relation = TargetRelation.Equal;
      int n = i == 1 ? 3 : 2;
      _builder.SpawnBasket(ComparisonMarketBuilder.SlotA, ComparisonMarketBuilder.BasketColor(0),
        n, 1f, ComparisonMarketBuilder.AppleColor(0), 0.42f, "L", false);
      _builder.SpawnBasket(ComparisonMarketBuilder.SlotB, ComparisonMarketBuilder.BasketColor(1),
        n, 1f, ComparisonMarketBuilder.OrangeColor(), 0.24f, "R", false);
      _builder.SpawnEqualPad(new Vector3(0f, 0f, 3.4f), "EQ");
      return;
    }
    Relation = TargetRelation.More;
    int big = i == 0 ? 2 : 3, small = i == 0 ? 4 : 5;
    bool bigLeft = i == 0;
    int left = bigLeft ? big : small, right = bigLeft ? small : big;
    float leftSize = bigLeft ? 0.44f : 0.24f, rightSize = bigLeft ? 0.24f : 0.44f;
    int ans = ComparisonLogic.AnswerMoreLess(left, right, true);
    var b = _builder;
    if (bigLeft) {
      b.SpawnBasket(ComparisonMarketBuilder.SlotA, ComparisonMarketBuilder.BasketColor(0),
        left, 1f, ComparisonMarketBuilder.AppleColor(0), leftSize, "L", ans == 0);
      b.SpawnBasket(ComparisonMarketBuilder.SlotB, ComparisonMarketBuilder.BasketColor(1),
        right, 1f, ComparisonMarketBuilder.OrangeColor(), rightSize, "R", ans == 1);
    } else {
      b.SpawnBasket(ComparisonMarketBuilder.SlotA, ComparisonMarketBuilder.BasketColor(0),
        left, 1f, ComparisonMarketBuilder.OrangeColor(), leftSize, "L", ans == 0);
      b.SpawnBasket(ComparisonMarketBuilder.SlotB, ComparisonMarketBuilder.BasketColor(1),
        right, 1f, ComparisonMarketBuilder.AppleColor(0), rightSize, "R", ans == 1);
    }
  }

  // LV7: bigger / smaller. Object count never cues (one object each side).
  void ApplySize() {
    CType = ComparisonType.Size;
    int i = RoundIndex % 4;
    bool askLarger = i % 2 == 0;
    Relation = askLarger ? TargetRelation.Larger : TargetRelation.Smaller;
    bool bigLeft = i < 2;
    Color c0 = ComparisonMarketBuilder.AppleColor(i), c1 = ComparisonMarketBuilder.AppleColor(i + 1);
    if (i >= 2) {
      _builder.SpawnBoxProp(ComparisonMarketBuilder.SlotA, bigLeft ? 0.9f : 0.5f, c0, "L", askLarger == bigLeft);
      _builder.SpawnBoxProp(ComparisonMarketBuilder.SlotB, bigLeft ? 0.5f : 0.9f, c1, "R", askLarger != bigLeft);
    } else {
      _builder.SpawnBall(ComparisonMarketBuilder.SlotA, bigLeft ? 0.45f : 0.28f, c0, "L", askLarger == bigLeft);
      _builder.SpawnBall(ComparisonMarketBuilder.SlotB, bigLeft ? 0.28f : 0.45f, c1, "R", askLarger != bigLeft);
    }
  }

  // LV8: longer / shorter ropes, left-aligned on the rail (fair setup).
  void ApplyLength() {
    CType = ComparisonType.Length;
    int i = RoundIndex % 4;
    bool askLonger = i % 2 == 0;
    Relation = askLonger ? TargetRelation.Longer : TargetRelation.Shorter;
    float longLen = 2.2f, shortLen = 1.3f;
    bool longLeft = i < 2;
    Vector3 a = new Vector3(-1.8f, 0f, 4.9f), b = new Vector3(0.2f, 0f, 4.9f);
    Color c0 = ComparisonMarketBuilder.AppleColor(i), c1 = ComparisonMarketBuilder.AppleColor(i + 1);
    // Left rope is the asked one exactly when its side holds the asked length.
    bool leftAns = longLeft == askLonger;
    float ll = longLeft ? (askLonger ? longLen : shortLen) : (askLonger ? shortLen : longLen);
    float rl = longLeft ? (askLonger ? shortLen : longLen) : (askLonger ? longLen : shortLen);
    _builder.SpawnRope(a, ll, c0, "L", leftAns);
    _builder.SpawnRope(b, rl, c1, "R", !leftAns);
  }

  // LV9: three-way. Third object is a real contender.
  void ApplyThreeWay() {
    CType = ComparisonType.Ordinal;
    int i = RoundIndex % 3;
    if (i == 0) {
      Relation = TargetRelation.Largest;
      SpawnThreeBoxes(new float[] { 0.5f, 0.95f, 0.7f }, 1);
    } else if (i == 1) {
      Relation = TargetRelation.Shortest;
      SpawnThreeRopes(new float[] { 2.2f, 1.3f, 1.7f }, 1);
    } else {
      Relation = TargetRelation.Most;
      int[] n = { 2, 5, 3 };
      int ans = ComparisonLogic.LargestIndex(n);
      Vector3[] slots = { ComparisonMarketBuilder.SlotA, ComparisonMarketBuilder.SlotB, ComparisonMarketBuilder.SlotC };
      for (int k = 0; k < 3; k++)
        _builder.SpawnBasket(slots[k], ComparisonMarketBuilder.BasketColor(k), n[k], 1f,
          ComparisonMarketBuilder.AppleColor(k), 0.3f, "S" + k, k == ans);
    }
  }

  void SpawnThreeBoxes(float[] sizes, int ans) {
    Vector3[] slots = { ComparisonMarketBuilder.SlotA, ComparisonMarketBuilder.SlotB, ComparisonMarketBuilder.SlotC };
    for (int k = 0; k < 3; k++)
      _builder.SpawnBoxProp(slots[k], sizes[k], ComparisonMarketBuilder.AppleColor(k), "S" + k, k == ans);
  }

  void SpawnThreeRopes(float[] lens, int ans) {
    Vector3[] slots = {
      new Vector3(-2.4f, 0f, 4.9f), new Vector3(-0.6f, 0f, 4.9f), new Vector3(1.2f, 0f, 4.9f)
    };
    for (int k = 0; k < 3; k++)
      _builder.SpawnRope(slots[k], lens[k], ComparisonMarketBuilder.AppleColor(k), "S" + k, k == ans);
  }

  // LV10: choose the right basket, then put it on the cart.
  void ApplyDelivery() {
    CType = ComparisonType.Quantity;
    int i = RoundIndex % 2;
    Current = Phase.Choose;
    if (i == 0) {
      Relation = TargetRelation.More;
      _builder.SpawnBasket(ComparisonMarketBuilder.SlotA, ComparisonMarketBuilder.BasketColor(0),
        3, 1f, ComparisonMarketBuilder.AppleColor(0), 0.32f, "L", false);
      _builder.SpawnBasket(ComparisonMarketBuilder.SlotB, ComparisonMarketBuilder.BasketColor(1),
        5, 1f, ComparisonMarketBuilder.AppleColor(1), 0.32f, "R", true);
    } else {
      Relation = TargetRelation.Smaller;
      CType = ComparisonType.Size;
      _builder.SpawnBoxProp(ComparisonMarketBuilder.SlotA, 0.9f,
        ComparisonMarketBuilder.AppleColor(0), "L", false);
      _builder.SpawnBoxProp(ComparisonMarketBuilder.SlotB, 0.5f,
        ComparisonMarketBuilder.AppleColor(1), "R", true);
    }
  }

  void SpeakTask() {
    if (Level == 3 || Level == 4) {
      if (Relation == TargetRelation.Equal) Ask("Are they the same?", "Bằng nhau không?");
      else if (Relation == TargetRelation.More) Ask("Which basket has more?", "Giỏ nào nhiều hơn?");
      else Ask("Which basket has less?", "Giỏ nào ít hơn?");
    } else if (Level == 5) {
      Ask("Match apple with orange.", "Xếp một táo một cam.");
    } else if (Level == 6) {
      if (Relation == TargetRelation.Equal) Ask("Are they the same?", "Bằng nhau không?");
      else Ask("Which basket has more?", "Giỏ nào nhiều hơn?");
    } else if (Level == 7) {
      bool boxes = (RoundIndex % 4) >= 2;
      if (Relation == TargetRelation.Larger) {
        if (boxes) Ask("Which box is bigger?", "Hộp nào lớn hơn?");
        else Ask("Which ball is bigger?", "Bóng nào lớn hơn?");
      } else {
        if (boxes) Ask("Which box is smaller?", "Hộp nào nhỏ hơn?");
        else Ask("Which ball is smaller?", "Bóng nào nhỏ hơn?");
      }
    } else if (Level == 8) {
      if (Relation == TargetRelation.Longer) Ask("Which rope is longer?", "Dây nào dài hơn?");
      else Ask("Which rope is shorter?", "Dây nào ngắn hơn?");
    } else if (Level == 9) {
      if (Relation == TargetRelation.Largest) Ask("Which box is biggest?", "Hộp nào lớn nhất?");
      else if (Relation == TargetRelation.Shortest) Ask("Which rope is shortest?", "Dây nào ngắn nhất?");
      else Ask("Which basket has most?", "Giỏ nào nhiều nhất?");
    } else {
      if (Relation == TargetRelation.More) Ask("More apples onto the cart.", "Giỏ nhiều táo lên xe.");
      else Ask("Small box onto the cart.", "Hộp nhỏ lên xe.");
    }
  }

  void PointTask() {
    if (_builder == null) return;
    if (Level == 10 && Current == Phase.Deliver && _builder.Cart != null) {
      ActivityGuide.PointAt(_builder.Cart.position);
      return;
    }
    ComparisonChoice mark = null;
    for (int i = 0; i < _builder.Choices.Count; i++) {
      ComparisonChoice c = _builder.Choices[i];
      if (c != null && c.IsAnswer && !c.IsCart) { mark = c; break; }
    }
    if (mark != null) ActivityGuide.PointAt(mark.transform.position);
    else ActivityGuide.PointAt(_builder.transform.TransformPoint(new Vector3(0f, 0f, 2.6f)));
  }

  public void TryTapChoice(ComparisonChoice choice) {
    if (choice == null || Completed) return;
    if (_restT > 0f) return;
    if (Current == Phase.Wait || Current == Phase.Done) return;
    _idleT = 0f;
    if (choice.IsCart) { TryDeliver(); return; }
    if (Level == 10) {
      if (choice.IsAnswer) {
        Selected = choice;
        Current = Phase.Deliver;
        choice.transform.localScale = Vector3.one * 1.12f;
        PlaySfx("pickup");
        Say("To the cart!", "Mang ra xe nào!");
        ActivityGuide.PointAt(_builder.Cart.position);
      } else {
        Reject();
        Ask("Compare again!", "So sánh lại nhé!");
      }
      return;
    }
    if (Current == Phase.Pair) return; // baskets wait for pairing
    if (Current == Phase.Confirm) { TryConfirmBasket(choice); return; }
    if (choice.IsAnswer) WinRound();
    else {
      Reject();
      Ask("Compare again!", "So sánh lại nhé!");
    }
  }

  public void TryTapFruit(ComparisonFruit fruit) {
    if (fruit == null || Completed || Current != Phase.Pair) return;
    if (_restT > 0f) return;
    _idleT = 0f;
    if (fruit.Paired) return;
    // Pair with the first unpaired fruit of the other group.
    ComparisonFruit mate = null;
    foreach (ComparisonFruit f in _builder.Fruits) {
      if (f != null && !f.Paired && f.Group != fruit.Group) { mate = f; break; }
    }
    if (mate == null) return;
    fruit.Paired = true;
    mate.Paired = true;
    PairsDone++;
    PlaySfx("pickup");
    GameJuice.PickFx(_fx, fruit.transform.position, fruit.transform);
    try {
      Vector3 mid = (fruit.transform.position + mate.transform.position) * 0.5f;
      mid.y = 0.8f;
      mate.transform.position = mid;
      fruit.transform.position = mid + new Vector3(0.25f, 0f, 0f);
    } catch (Exception) { }
    if (PairsDone >= PairsNeed) {
      Current = Phase.Confirm;
      Say("Which group is left?", "Nhóm nào còn thừa?");
      ActivityGuide.PointAt(_builder.transform.TransformPoint(new Vector3(0f, 0f, 2.6f)));
    }
  }

  // LV5 confirm: tap the basket of the leftover group.
  public void TryConfirmBasket(ComparisonChoice basket) {
    if (Current != Phase.Confirm || basket == null) return;
    if (basket.IsAnswer) WinRound();
    else {
      Reject();
      Ask("Which group is left?", "Nhóm nào còn thừa?");
    }
  }

  void TryDeliver() {
    if (Level != 10 || Current != Phase.Deliver || Selected == null) return;
    WinRound();
  }

  void Reject() {
    WrongCount++;
    LastCorrect = false;
    Say("Not yet. Look again.", "Chưa đúng. Xem lại nhé.");
    ActivityFeedback.Retry();
    GameJuice.WrongFx(_builder != null ? _builder.transform : transform, _fx,
      _player != null ? _player.position : Vector3.zero);
  }

  void WinRound() {
    LastCorrect = true;
    PlaySfx("success");
    GameJuice.CorrectFx(_fx, _player != null ? _player.position : Vector3.zero, false);
    ActivityFeedback.Correct();
    SayPraise();
    int max = RoundsOf(Level);
    RoundIndex++;
    ActivityFeedback.ProgressKeep(RoundIndex, max);
    bool finish = false;
    if (RoundIndex >= max) {
      Level++;
      RoundIndex = 0;
      finish = Level > LastLevel;
      if (!finish) Say("Well done!", "Giỏi lắm!");
    }
    RestThenAdvance(finish);
  }

  void RestThenAdvance(bool finish) {
    _finishAfterRest = finish;
    _restT = Application.isPlaying ? 1.6f : 0f;
    if (_restT <= 0f) AfterWin();
  }

  void AfterWin() {
    _restT = 0f;
    if (_finishAfterRest) FinishVisit();
    else BeginRound();
  }

  void SayPraise() {
    if (Relation == TargetRelation.Equal) Say("Right! They are equal.", "Đúng rồi! Bằng nhau.");
    else if (Relation == TargetRelation.More || Relation == TargetRelation.Most)
      Say("Right! This one has more.", "Đúng rồi! Giỏ này nhiều hơn.");
    else if (Relation == TargetRelation.Less) Say("Right! This one has less.", "Đúng rồi! Giỏ này ít hơn.");
    else if (Relation == TargetRelation.Larger || Relation == TargetRelation.Largest)
      Say("Right! This one is bigger.", "Đúng rồi! Cái này lớn hơn.");
    else if (Relation == TargetRelation.Smaller || Relation == TargetRelation.Smallest)
      Say("Right! This one is smaller.", "Đúng rồi! Cái này nhỏ hơn.");
    else if (Relation == TargetRelation.Longer || Relation == TargetRelation.Longest)
      Say("Right! This one is longer.", "Đúng rồi! Dây này dài hơn.");
    else Say("Right! This is shorter.", "Đúng rồi! Dây này ngắn hơn.");
  }

  public static int RoundsOf(int level) {
    if (level == 3) return 6;
    if (level == 4) return 4;
    if (level == 5) return 3;
    if (level == 6) return 4;
    if (level == 7) return 4;
    if (level == 8) return 4;
    if (level == 9) return 3;
    if (level == 10) return 2;
    return 1;
  }

  void FinishVisit() {
    Completed = true;
    Current = Phase.Done;
    ActivityGuide.Clear();
    Say("Well done!", "Giỏi lắm!");
    if (_life != null) {
      try { _life.MarkCompleted("market done"); } catch (Exception) { }
    }
  }

  void Adopt() {
    Completed = true;
    Current = Phase.Done;
    Level = LastLevel;
  }

  public void SetLevelForTests(int level, int round) {
    Level = Mathf.Clamp(level, FirstLevel, LastLevel);
    RoundIndex = Mathf.Max(0, round);
    BeginRound();
  }

  void Ask(string en, string vi) { _askEn.Enqueue(en); _askVi.Enqueue(vi); }

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
    try { _cam.Follow(_player, ComparisonMarketBuilder.FollowOffset); } catch (Exception) { }
  }

  bool PlayerNear(Vector3 world, float r) {
    if (_player == null) return true;
    Vector3 d = _player.position - world;
    d.y = 0f;
    return d.sqrMagnitude <= r * r;
  }

  Vector3 SpotWorld() {
    return _builder != null
      ? _builder.transform.TransformPoint(ComparisonMarketBuilder.PlaySpotLocal)
      : ComparisonMarketBuilder.PlaySpotLocal;
  }
}
