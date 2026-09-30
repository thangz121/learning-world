// A_World/ClassificationCity/ClassificationCity.cs — Thành Phố Phân Loại.
// Starts at LV3. Observe -> rule -> classify -> adapt. No timer, no game over.
// C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ClassificationCity : MonoBehaviour {
  public enum Phase { Wait, Sort, Done }

  public const int FirstLevel = 3;
  public const int LastLevel = 10;

  public Phase Current { get; private set; }
  public int Level { get; private set; }
  public int RoundIndex { get; private set; }
  public ClassCriterion Criterion { get; private set; }
  public string[] Groups { get; private set; }
  public ClassificationItem Carried { get; private set; }
  public bool Completed { get; private set; }
  public int WrongCount { get; private set; }
  public bool LastCorrect { get; private set; }
  public int PlacedCount { get; private set; }
  public int NeedCount { get; private set; }
  public bool OddPicked { get; private set; }
  public int OddIndex { get; private set; }

  ClassificationCityBuilder _builder;
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
  readonly List<ClassificationBin> _liveBins = new List<ClassificationBin>();
  readonly Queue<string> _askEn = new Queue<string>();
  readonly Queue<string> _askVi = new Queue<string>();

  public void Build(ClassificationCityBuilder builder, Transform player, SmartCamera cam,
      IAudioDirector audio, ActivityLifecycle life) {
    _builder = builder;
    _player = player;
    _cam = cam;
    _audio = audio;
    _life = life;
    _voice = new PacedVoice();
    _voice.Audio = audio;
    _voice.LogTag = "ClassificationCity";
    _fx = builder != null ? builder.transform : transform;
    if (_life != null) {
      try {
        if (_life.State == ActivityState.Completed) { Adopt(); return; }
        _life.MarkAvailable("city offered");
      } catch (Exception) { }
    }
    Level = FirstLevel;
    RoundIndex = 0;
    Current = Phase.Wait;
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
    if (Completed && Current == Phase.Done) return;
    if (_restT > 0f) {
      _restT -= dt;
      if (_restT <= 0f) AfterWin();
      return;
    }
    _phaseT += dt;
    if (Current == Phase.Wait) TickWait(dt);
    else if (Current == Phase.Sort) {
      _idleT += dt;
      if (_idleT >= 8f && _idleT - dt < 8f) PointTask();
      else if (_idleT >= 15f && _idleT - dt < 15f) PointTask();
      TickPlay(dt);
    }
  }

  void TickWait(float dt) {
    if (!_waitCalled && _phaseT >= 1.2f) {
      _waitCalled = true;
      Say("Come sort with me!", "Ra xếp cùng con!");
      ActivityGuide.PointAt(SpotWorld());
    }
    if (PlayerNear(SpotWorld(), 1.5f) || _phaseT >= 8f) BeginRound();
  }

  void TickPlay(float dt) {
    if (Carried != null) {
      ClassificationBin near = NearestBin(1.05f);
      if (near != null) TryPlace(near);
    }
  }

  void TickAsk() {
    if (_askEn.Count <= 0 || _voice == null || !_voice.Idle || _voice.HasLine) return;
    Say(_askEn.Dequeue(), _askVi.Dequeue());
  }

  public void BeginRound() {
    if (Completed) return;
    Current = Phase.Sort;
    _phaseT = 0f;
    Carried = null;
    PlacedCount = 0;
    OddPicked = false;
    OddIndex = -1;
    _idleT = 0f;
    ApplyRound();
    ActivityFeedback.ProgressKeep(RoundIndex, RoundsOf(Level));
    foreach (ClassificationBin b in _builder.Bins)
      if (b != null) b.Game = this;
    foreach (ClassificationItem it in _builder.Items)
      if (it != null && it.Game == null) it.Game = this;
    if (_life != null && _life.State == ActivityState.Available) {
      try { _life.Begin("city round"); } catch (Exception) { }
    }
    SpeakTask();
    PointTask();
  }

  void ApplyRound() {
    _builder.ClearRound();
    _liveBins.Clear();
    Groups = null;
    if (Level == 3) ApplyTwoGroups();
    else if (Level == 4) ApplySize();
    else if (Level == 5) ApplyAttribute();
    else if (Level == 6) ApplyThreeGroups();
    else if (Level == 7) ApplyRuleChange();
    else if (Level == 8) ApplyFunction();
    else if (Level == 9) ApplyOdd();
    else ApplyDiscover();
  }

  // ---- item catalog (colors rotate; color never cues except LV7-color) ----

  static ClassItem Animal(int color, bool big) {
    return new ClassItem("animal", big, color, false, false, ClassFunction.Play);
  }
  static ClassItem Vehicle(int color, bool big) {
    return new ClassItem("vehicle", big, color, true, true, ClassFunction.Move);
  }
  static ClassItem Food(int color, bool big) {
    return new ClassItem("food", big, color, false, false, ClassFunction.Eat);
  }
  static ClassItem ToyBall(int color, bool big) {
    return new ClassItem("toy", big, color, false, false, ClassFunction.Play);
  }
  static ClassItem ToyBlock(int color, bool big) {
    return new ClassItem("toy", big, color, false, true, ClassFunction.Play);
  }

  static readonly Vector3[] PlazaSlots = {
    new Vector3(-1.8f, 0f, 2.2f), new Vector3(-0.6f, 0f, 2.2f),
    new Vector3(0.6f, 0f, 2.2f), new Vector3(1.8f, 0f, 2.2f),
    new Vector3(-1.2f, 0f, 3.2f), new Vector3(0f, 0f, 3.2f),
    new Vector3(1.2f, 0f, 3.2f),
  };

  static Vector3[] BinSlots(int n, int round) {
    Vector3[] base3 = {
      ClassificationCityBuilder.SlotA, ClassificationCityBuilder.SlotB,
      ClassificationCityBuilder.SlotC
    };
    Vector3[] two = { ClassificationCityBuilder.SlotA, ClassificationCityBuilder.SlotB };
    Vector3[] src = n <= 2 ? two : base3;
    Vector3[] outSlots = new Vector3[n];
    for (int i = 0; i < n; i++) outSlots[i] = src[(i + round) % n];
    return outSlots;
  }

  void LiveBin(Vector3 slot, string kind, string groupKey, int variant) {
    ClassificationBin b = _builder.SpawnBin(slot, kind, groupKey, variant);
    if (b != null) {
      b.Game = this;
      _liveBins.Add(b);
    }
  }

  void SpawnAll(ClassItem[] props) {
    for (int i = 0; i < props.Length; i++) {
      Vector3 home = PlazaSlots[i % PlazaSlots.Length];
      ClassificationItem it = _builder.SpawnItem(home, props[i], "i" + i, false);
      it.Game = this;
    }
    NeedCount = props.Length;
  }

  // ---- levels ----

  void ApplyTwoGroups() {
    Criterion = ClassCriterion.ObjectType;
    Groups = new string[] { "animal", "vehicle" };
    Vector3[] slots = BinSlots(2, RoundIndex);
    // Rotate bin sides so animal is not always left.
    string first = RoundIndex % 2 == 0 ? "animal" : "vehicle";
    string second = first == "animal" ? "vehicle" : "animal";
    LiveBin(slots[0], first == "animal" ? "house" : "garage", first, 0);
    LiveBin(slots[1], second == "animal" ? "house" : "garage", second, 0);
    ClassItem[] items = {
      Animal(0, false), Animal(1, true), Animal(2, false),
      Vehicle(3, false), Vehicle(1, true), Vehicle(0, false),
    };
    SpawnAll(items);
  }

  void ApplySize() {
    Criterion = ClassCriterion.Size;
    Groups = new string[] { "big", "small" };
    Vector3[] slots = BinSlots(2, RoundIndex);
    string first = RoundIndex % 2 == 0 ? "big" : "small";
    string second = first == "big" ? "small" : "big";
    LiveBin(slots[0], first == "big" ? "bigpad" : "smallpad", first, 0);
    LiveBin(slots[1], second == "big" ? "bigpad" : "smallpad", second, 0);
    ClassItem[] items = {
      ToyBall(0, true), Vehicle(1, true), ToyBlock(2, false), Food(3, false),
    };
    SpawnAll(items);
  }

  void ApplyAttribute() {
    Criterion = ClassCriterion.Wheels;
    Groups = new string[] { "wheels", "nowheels" };
    Vector3[] slots = BinSlots(2, RoundIndex);
    string first = RoundIndex % 2 == 0 ? "wheels" : "nowheels";
    string second = first == "wheels" ? "nowheels" : "wheels";
    LiveBin(slots[0], first == "wheels" ? "wheelspad" : "plainpad", first, 0);
    LiveBin(slots[1], second == "wheels" ? "wheelspad" : "plainpad", second, 0);
    ClassItem[] items = {
      Vehicle(0, false), Vehicle(1, true), ToyBall(2, false), ToyBlock(3, true),
    };
    SpawnAll(items);
  }

  void ApplyThreeGroups() {
    Criterion = ClassCriterion.ObjectType;
    Groups = new string[] { "animal", "food", "vehicle" };
    Vector3[] slots = BinSlots(3, RoundIndex);
    string[] order = { "animal", "food", "vehicle" };
    string[] kinds = { "house", "market", "garage" };
    for (int i = 0; i < 3; i++) {
      string g = order[(i + RoundIndex) % 3];
      int k = g == "animal" ? 0 : (g == "food" ? 1 : 2);
      LiveBin(slots[i], kinds[k], g, 0);
    }
    ClassItem[] items = {
      Animal(0, false), Food(1, false), Vehicle(2, false),
      Animal(3, true), Food(0, true), Vehicle(1, true),
    };
    SpawnAll(items);
  }

  void ApplyRuleChange() {
    int sub = RoundIndex % 3;
    if (sub == 0) {
      Criterion = ClassCriterion.ObjectType;
      Groups = new string[] { "animal", "vehicle" };
      Vector3[] slots = BinSlots(2, RoundIndex);
      LiveBin(slots[0], "house", "animal", 0);
      LiveBin(slots[1], "garage", "vehicle", 0);
      ClassItem[] items = {
        Animal(0, false), Animal(1, true), Vehicle(2, false), Vehicle(3, true),
      };
      SpawnAll(items);
    } else if (sub == 1) {
      Criterion = ClassCriterion.Size;
      Groups = new string[] { "big", "small" };
      Vector3[] slots = BinSlots(2, RoundIndex);
      LiveBin(slots[0], "bigpad", "big", 0);
      LiveBin(slots[1], "smallpad", "small", 0);
      ClassItem[] items = {
        Animal(0, true), Vehicle(1, false), Animal(2, false), Vehicle(3, true),
      };
      SpawnAll(items);
    } else {
      Criterion = ClassCriterion.Color;
      Groups = new string[] { "red", "notred" };
      Vector3[] slots = BinSlots(2, RoundIndex);
      LiveBin(slots[0], "redpad", "red", 0);
      LiveBin(slots[1], "notredpad", "notred", 0);
      ClassItem[] items = {
        Animal(0, false), Vehicle(0, true), Animal(1, false), Vehicle(2, true),
      };
      SpawnAll(items);
    }
  }

  void ApplyFunction() {
    Criterion = ClassCriterion.Function;
    Groups = new string[] { "food", "transport", "toy" };
    Vector3[] slots = BinSlots(3, RoundIndex);
    string[] order = { "food", "transport", "toy" };
    string[] kinds = { "market", "garage", "playground" };
    for (int i = 0; i < 3; i++) {
      string g = order[(i + RoundIndex) % 3];
      int k = g == "food" ? 0 : (g == "transport" ? 1 : 2);
      LiveBin(slots[i], kinds[k], g, 0);
    }
    ClassItem[] items = {
      Food(0, false), Vehicle(1, false), ToyBall(2, false),
      Food(3, true), Vehicle(0, true), ToyBlock(1, false),
    };
    SpawnAll(items);
  }

  void ApplyOdd() {
    Criterion = ClassCriterion.ObjectType;
    Groups = new string[] { "other" };
    int k = RoundIndex % 3;
    LiveBin(ClassificationCityBuilder.SlotB, "other", "other", 0);
    ClassItem[] items;
    if (k == 0) {
      items = new ClassItem[] { Animal(0, false), Animal(1, false), Animal(2, false), Vehicle(3, false) };
      Criterion = ClassCriterion.ObjectType;
    } else if (k == 1) {
      items = new ClassItem[] { Vehicle(0, false), Vehicle(1, true), Vehicle(2, false), ToyBall(3, false) };
      Criterion = ClassCriterion.Wheels;
    } else {
      items = new ClassItem[] { ToyBall(0, true), Vehicle(1, true), ToyBlock(2, true), ToyBall(3, false) };
      Criterion = ClassCriterion.Size;
    }
    // Shuffle which plaza slot the odd item takes (never always last).
    OddIndex = -1;
    ClassItem[] placed = new ClassItem[items.Length];
    int[] map = { 0, 1, 2, 3 };
    int shift = (RoundIndex + 1) % 4;
    for (int i = 0; i < 4; i++) map[i] = (i + shift) % 4;
    for (int i = 0; i < 4; i++) placed[map[i]] = items[i];
    for (int i = 0; i < placed.Length; i++) {
      Vector3 home = PlazaSlots[i % PlazaSlots.Length];
      ClassificationItem it = _builder.SpawnItem(home, placed[i], "i" + i, false);
      it.Game = this;
    }
    OddIndex = ClassLogic.OddIndex(placed, Criterion);
    NeedCount = 1;
  }

  void ApplyDiscover() {
    int k = RoundIndex % 2;
    if (k == 0) {
      Criterion = ClassCriterion.Size;
      Groups = new string[] { "big", "small" };
    } else {
      Criterion = ClassCriterion.Wheels;
      Groups = new string[] { "wheels", "nowheels" };
    }
    Vector3[] slots = BinSlots(2, RoundIndex);
    string b0 = Groups[0], b1 = Groups[1];
    LiveBin(slots[0], "house", b0, 0);
    LiveBin(slots[1], "house", b1, 0);
    // Pre-sorted examples (visual, non-interactive).
    ClassItem exA0, exA1, exB0, exB1, nw0, nw1;
    if (k == 0) {
      exA0 = ToyBall(0, true); exA1 = Vehicle(1, true);
      exB0 = ToyBall(2, false); exB1 = Food(3, false);
      nw0 = ToyBlock(0, true); nw1 = Food(1, false);
    } else {
      exA0 = Vehicle(0, false); exA1 = Vehicle(1, true);
      exB0 = ToyBall(2, false); exB1 = Food(3, false);
      nw0 = Vehicle(2, true); nw1 = ToyBlock(0, false);
    }
    ParkExample(exA0, 0);
    ParkExample(exA1, 0);
    ParkExample(exB0, 1);
    ParkExample(exB1, 1);
    ClassItem[] news = { nw0, nw1 };
    for (int i = 0; i < news.Length; i++) {
      Vector3 home = PlazaSlots[i % PlazaSlots.Length];
      ClassificationItem it = _builder.SpawnItem(home, news[i], "n" + i, false);
      it.Game = this;
    }
    NeedCount = news.Length;
  }

  void ParkExample(ClassItem props, int binIdx) {
    ClassificationBin bin = _liveBins.Count > binIdx ? _liveBins[binIdx] : null;
    Vector3 at = bin != null ? bin.transform.localPosition + new Vector3(-0.4f + bin.Occupants.Count * 0.5f, 0f, 0.3f)
      : PlazaSlots[0];
    ClassificationItem it = _builder.SpawnItem(at, props, "ex" + binIdx + "_" + (bin != null ? bin.Occupants.Count : 0), true);
    it.Game = this;
    if (bin != null) bin.Occupants.Add(it);
  }

  void SpeakTask() {
    if (Level == 3) Ask("Animals home, cars garage.", "Thú vào nhà, xe gara.");
    else if (Level == 4) Ask("Big to big, small to small.", "Lớn sân lớn, nhỏ sân nhỏ.");
    else if (Level == 5) Ask("Wheels to the workshop.", "Có bánh vào xưởng.");
    else if (Level == 6) Ask("Animals home, food market, cars garage.", "Vật nhà, ăn chợ, xe gara.");
    else if (Level == 7) {
      int sub = RoundIndex % 3;
      if (sub > 0) Ask("New sorting rule!", "Đổi cách xếp nhé!");
      if (sub == 0) Ask("Animals home, cars garage.", "Thú vào nhà, xe gara.");
      else if (sub == 1) Ask("Big to big, small to small.", "Lớn sân lớn, nhỏ sân nhỏ.");
      else Ask("Red things go here.", "Đỏ vào đây nhé.");
    } else if (Level == 8) Ask("Food market, cars garage, toys playground.", "Ăn chợ, xe gara, chơi sân.");
    else if (Level == 9) Ask("Who is different?", "Ai khác nhóm?");
    else {
      Ask("Look at both houses.", "Nhìn hai nhà nhé.");
      Ask("Sort like the houses.", "Xếp giống hai nhà.");
    }
  }

  void PointTask() {
    if (_builder == null) return;
    if (Carried != null) {
      ClassificationBin want = MatchingBin();
      if (want != null) { ActivityGuide.PointAt(want.transform.position); return; }
    }
    foreach (ClassificationItem it in _builder.Items) {
      if (it != null && it.State == ClassificationItem.ItemState.Idle) {
        ActivityGuide.PointAt(it.transform.position);
        return;
      }
    }
    ActivityGuide.PointAt(_builder.transform.TransformPoint(new Vector3(0f, 0f, 2.2f)));
  }

  public void TrySelect(ClassificationItem item) {
    if (item == null || Completed) return;
    if (_restT > 0f) return;
    if (Current == Phase.Wait || Current == Phase.Done) return;
    if (item.State == ClassificationItem.ItemState.Placed
        || item.State == ClassificationItem.ItemState.Example) return;
    if (Carried != null) return;
    if (Level == 9 && !OddPicked) {
      // First tap names the odd one; it must be the true odd item.
      ClassItem[] all = CurrentProps();
      int odd = ClassLogic.OddIndex(all, Criterion);
      int idx = IndexOf(item);
      if (idx == odd) {
        OddPicked = true;
        item.BeginCarry(_player != null ? _player : transform);
        Carried = item;
        _idleT = 0f;
        PlaySfx("pickup");
        Say("Right! To the other pad.", "Đúng! Mang ra khác.");
        ClassificationBin other = _liveBins.Count > 0 ? _liveBins[0] : null;
        if (other != null) ActivityGuide.PointAt(other.transform.position);
      } else {
        Reject();
        Ask("Who is different?", "Ai khác nhóm?");
      }
      return;
    }
    item.BeginCarry(_player != null ? _player : transform);
    Carried = item;
    _idleT = 0f;
    PlaySfx("pickup");
    GameJuice.PickFx(_fx, item.transform.position, item.transform);
    PointTask();
  }

  public void TryPlace(ClassificationBin bin) {
    if (bin == null || Carried == null || Completed) return;
    if (_restT > 0f) return;
    if (!_liveBins.Contains(bin)) return;
    ClassificationItem item = Carried;
    if (Level == 9) {
      if (bin.GroupKey == "other" && OddPicked) {
        bin.Accept(item);
        Carried = null;
        PlacedCount++;
        WinRound();
      } else {
        Reject();
      }
      return;
    }
    int g = ClassLogic.GroupFor(item.Props, Criterion, Groups);
    string want = g >= 0 && Groups != null && g < Groups.Length ? Groups[g] : "";
    if (bin.GroupKey == want) {
      bin.Accept(item);
      Carried = null;
      PlacedCount++;
      _idleT = 0f;
      PlaySfx("place");
      GameJuice.PlaceFx(_fx, bin.transform.position, bin.transform);
      Say("Right group!", "Đúng nhóm rồi!");
      if (PlacedCount >= NeedCount) WinRound();
    } else {
      LastCorrect = false;
      Reject();
    }
  }

  ClassItem[] CurrentProps() {
    List<ClassItem> list = new List<ClassItem>();
    foreach (ClassificationItem it in _builder.Items) {
      if (it == null) continue;
      if (it.State == ClassificationItem.ItemState.Example) continue;
      list.Add(it.Props);
    }
    return list.ToArray();
  }

  int IndexOf(ClassificationItem item) {
    int idx = 0;
    foreach (ClassificationItem it in _builder.Items) {
      if (it == null) continue;
      if (it.State == ClassificationItem.ItemState.Example) continue;
      if (it == item) return idx;
      idx++;
    }
    return -1;
  }

  void Reject() {
    WrongCount++;
    LastCorrect = false;
    Say("Not that group.", "Chưa đúng nhóm.");
    ActivityFeedback.Retry();
    GameJuice.WrongFx(_builder != null ? _builder.transform : transform, _fx,
      _player != null ? _player.position : Vector3.zero);
  }

  void WinRound() {
    LastCorrect = true;
    PlaySfx("success");
    GameJuice.CorrectFx(_fx, _player != null ? _player.position : Vector3.zero, false);
    ActivityFeedback.Correct();
    if (Carried != null) { Carried.ReturnHome(); Carried = null; }
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

  public static int RoundsOf(int level) {
    if (level == 3) return 2;
    if (level == 4) return 2;
    if (level == 5) return 2;
    if (level == 6) return 2;
    if (level == 7) return 3;
    if (level == 8) return 2;
    if (level == 9) return 3;
    if (level == 10) return 2;
    return 1;
  }

  void FinishVisit() {
    Completed = true;
    Current = Phase.Done;
    ActivityGuide.Clear();
    Say("Great! You can sort!", "Giỏi lắm! Biết phân loại rồi!");
    if (_life != null) {
      try { _life.MarkCompleted("city done"); } catch (Exception) { }
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
    try { _cam.Follow(_player, ClassificationCityBuilder.FollowOffset); } catch (Exception) { }
  }

  bool PlayerNear(Vector3 world, float r) {
    if (_player == null) return true;
    Vector3 d = _player.position - world;
    d.y = 0f;
    return d.sqrMagnitude <= r * r;
  }

  Vector3 SpotWorld() {
    return _builder != null
      ? _builder.transform.TransformPoint(ClassificationCityBuilder.PlaySpotLocal)
      : ClassificationCityBuilder.PlaySpotLocal;
  }

  ClassificationBin NearestBin(float r) {
    if (_player == null) return null;
    ClassificationBin best = null;
    float bestD = r * r;
    for (int i = 0; i < _liveBins.Count; i++) {
      ClassificationBin b = _liveBins[i];
      if (b == null) continue;
      if (!BinMatchesCarried(b)) continue;
      Vector3 d = _player.position - b.transform.position;
      d.y = 0f;
      float m = d.sqrMagnitude;
      if (m <= bestD) { bestD = m; best = b; }
    }
    return best;
  }

  bool BinMatchesCarried(ClassificationBin bin) {
    if (bin == null || Carried == null) return false;
    if (Level == 9) return bin.GroupKey == "other";
    int g = ClassLogic.GroupFor(Carried.Props, Criterion, Groups);
    string want = g >= 0 && Groups != null && g < Groups.Length ? Groups[g] : "";
    return bin.GroupKey == want;
  }

  ClassificationBin MatchingBin() {
    for (int i = 0; i < _liveBins.Count; i++) {
      if (BinMatchesCarried(_liveBins[i])) return _liveBins[i];
    }
    return null;
  }
}
