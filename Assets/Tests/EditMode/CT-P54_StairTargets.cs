// CT-P54: GAMEPLAY #2 9-STEP ROUND — "BẬC THANG CON SỐ", TARGETS 1..9.
// Pins the maximum-target contract (brief §2/§38): ONE staircase for every
// target, the board/result staging the round's digit, the full flow at a mid
// target (5), overshoot at the top (9), undershoot guidance, backtracking,
// boundary debounce, the 1..9 walk with recap, and the area's target ladder
// (3 -> 5 -> 7 -> 9 -> 1) with the "-stair-target N" diagnostic override.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CT_P54_StairTargets {
  sealed class FakeAudio : IAudioDirector {
    public readonly List<string> Lines = new List<string>();
    public readonly List<string> Sfx = new List<string>();
    public Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode mode) {
      return Task.CompletedTask;
    }
    public Task SpeakAsync(DialogueRequest request) {
      Lines.Add(request.Text);
      return Task.CompletedTask;
    }
    public void PlaySfx(SfxId id) { Sfx.Add(id.Value); }
    public void PlayMusic(MusicId id) { }
    public void SetMusicEnabled(bool on) { }
    public void SetAudioFocus(AudioFocusMode mode) { }
  }

  static Transform FindDeep(Transform t, string name) {
    if (t == null) return null;
    if (t.name == name) return t;
    for (int i = 0; i < t.childCount; i++) {
      Transform f = FindDeep(t.GetChild(i), name);
      if (f != null) return f;
    }
    return null;
  }

  static StairHillBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("P54StairWorld");
    StairHillBuilder builder = arena.AddComponent<StairHillBuilder>();
    return builder;
  }

  static Vector3 WorldStand(StairHillBuilder builder, int step) {
    return builder.Stairs.transform.TransformPoint(builder.Stairs.StandLocal(step));
  }

  static NumberStairs BuildGame(StairHillBuilder builder, GameObject arena, GameObject player,
      FakeAudio audio, ActivityLifecycle life, int target) {
    NumberStairs game = arena.AddComponent<NumberStairs>();
    game.ChainOnSuccess = false; // single-question mechanics (chaining: P54K)
    game.Build(builder, player.transform, null, audio, life, target);
    return game;
  }

  // S3-P2Z13 (user round): NO arena demo; the question is read only once the
  // child stands on the marked circle — walk them there, then tick to the climb.
  static void AdvanceToClimb(NumberStairs game, GameObject player, StairHillBuilder builder,
      float maxSeconds = 90f) {
    Vector3 circle = builder.ListenPad != null
      ? builder.ListenPad.transform.position
      : builder.transform.TransformPoint(StairHillBuilder.ListenLocal);
    player.transform.position = circle;
    for (float t = 0f; t < maxSeconds && game.Current != NumberStairs.Phase.Climb; t += 0.1f)
      game.Tick(0.1f);
  }

  // Teleport-walk: the settle window (0.3s) commits each band exactly once and
  // the stand-above dwell (0.5s) lets a high band be judged (user round).
  static void StepTo(NumberStairs game, GameObject player, StairHillBuilder builder, int step) {
    player.transform.position = WorldStand(builder, step);
    for (int i = 0; i < 8; i++) game.Tick(0.1f);
  }

  // A. Digit maps 0-9: every target stages its own glyph, nothing else moves.
  [Test] public void P54A_DigitSegMaps() {
    GameObject root = new GameObject("P54Digits");
    try {
      string[][] want = CountingGardenBuilder.DigitSegSets;
      Assert.AreEqual(10, want.Length, "ten digit maps");
      for (int n = 0; n <= 9; n++) {
        GameObject g = CountingGardenBuilder.Digit(root.transform, "P54D" + n,
          Vector3.zero, 1.0f, 0.8f, Color.white, 90f, n);
        Assert.IsNotNull(g, "digit " + n + " builds");
        var names = new List<string>();
        for (int i = 0; i < g.transform.childCount; i++)
          names.Add(g.transform.GetChild(i).name);
        Assert.AreEqual(want[n].Length, names.Count, "digit " + n + " has its segments only");
        foreach (string s in want[n])
          Assert.IsTrue(names.Contains("P54D" + n + s), "digit " + n + " carries " + s);
        Object.DestroyImmediate(g);
      }
      // The named wrappers keep their historic shapes (gameplay #1/#2 pins).
      GameObject d2 = CountingGardenBuilder.Digit2(root.transform, "P54Two",
        Vector3.zero, 1.0f, 0.8f, Color.white, 90f);
      Assert.AreEqual(5, d2.transform.childCount, "Digit2 = A/B/G/E/D");
      Assert.IsNotNull(FindDeep(d2.transform, "P54TwoE"), "Digit2 keeps E");
      Object.DestroyImmediate(d2);
      GameObject d3 = CountingGardenBuilder.Digit3(root.transform, "P54Three",
        Vector3.zero, 1.0f, 0.8f, Color.white, 90f);
      Assert.AreEqual(5, d3.transform.childCount, "Digit3 = A/B/G/C/D");
      Assert.IsNotNull(FindDeep(d3.transform, "P54ThreeC"), "Digit3 keeps C");
      Object.DestroyImmediate(d3);
      // Out of range reads as 8 (never a blank board).
      GameObject dX = CountingGardenBuilder.Digit(root.transform, "P54Dx",
        Vector3.zero, 1.0f, 0.8f, Color.white, 90f, 42);
      Assert.AreEqual(7, dX.transform.childCount, "unknown digit falls back to 8");
      Object.DestroyImmediate(dX);
    } finally { Object.DestroyImmediate(root); }
  }

  // B. Target validity 1..9: the board + result stage the round's digit; the
  // clamp keeps strays inside the walkable range.
  [Test] public void P54B_TargetValidity() {
    Assert.AreEqual(9, StairHillBuilder.StepCount, "MAXIMUM = 9 physical steps");
    Assert.AreEqual(1, StairHillBuilder.ClampTarget(0), "0 clamps to 1");
    Assert.AreEqual(9, StairHillBuilder.ClampTarget(42), "12 clamps to 9");
    Assert.AreEqual(5, StairHillBuilder.ClampTarget(5), "valid targets pass through");
    for (int t = 1; t <= 9; t++) {
      GameObject arena;
      StairHillBuilder builder = BuildArena(out arena);
      try {
        builder.BoardTarget = t;
        builder.BuildContent(arena.transform);
        Transform board = FindDeep(arena.transform, "SHNumberDigit");
        Assert.IsNotNull(board, "target board staged (target " + t + ")");
        var names = new List<string>();
        for (int i = 0; i < board.transform.childCount; i++)
          names.Add(board.transform.GetChild(i).name);
        string[] want = CountingGardenBuilder.DigitSegSets[t];
        Assert.AreEqual(want.Length, names.Count, "board shows " + t + " (not a fixed 3)");
        foreach (string s in want)
          Assert.IsTrue(names.Contains("SHNumberDigit" + s), "board carries " + s);
        Transform result = FindDeep(arena.transform, "SHResultDigit");
        Assert.IsNotNull(result, "result digit staged (target " + t + ")");
        Assert.AreEqual(want.Length, result.transform.childCount, "result shows " + t);
      } finally { Object.DestroyImmediate(arena); }
    }
  }

  // C. Full flow at a mid target (5): intro/demo/handoff, 1..5 climb, dwell,
  // success with the 5-recap, lifecycle completed.
  [Test] public void P54C_FlowAtTarget5() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P54Player5");
    try {
      builder.BoardTarget = 5;
      builder.BuildContent(arena.transform);
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("number_stairs", "test");
      NumberStairs game = BuildGame(builder, arena, player, audio, life, 5);
      Assert.AreEqual(5, game.Target, "the game plays the round's target");
      AdvanceToClimb(game, player, builder);
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "handoff reached");
      Assert.IsTrue(game.QuestionTold, "the question was read on the circle");
      for (int i = 1; i <= 5; i++) {
        StepTo(game, player, builder, i);
        Assert.AreEqual(i, game.CurrentStep, "step " + i);
      }
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "arrival alone never completes");
      for (int i = 0; i < 20 && game.Current != NumberStairs.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "stable stand on 5 succeeds");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.IsTrue(game.ResultShown, "result board shown");
      // The 1..5 counts AND the 1..5 recap all land (paced, never a burst) —
      // no arena demo any more, so each count word lands twice: climbed + recapped.
      for (int i = 0; i < 130; i++) game.Tick(0.1f);
      string five = DialogueLang.T("Five.", "Năm.");
      string one = DialogueLang.T("One.", "Một.");
      Assert.AreEqual(2, audio.Lines.FindAll(s => s == five).Count, "5 climbed + recapped");
      Assert.AreEqual(2, audio.Lines.FindAll(s => s == one).Count, "1 climbed + recapped");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Five steps! Well done!", "Năm bậc! Giỏi!")), "confirm names the target");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // D. Top overshoot (target 9): the landing guides back, never completes;
  // returning to 9 settles it.
  [Test] public void P54D_OvershootAt9() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P54Player9");
    try {
      builder.BoardTarget = 9;
      builder.BuildContent(arena.transform);
      FakeAudio audio = new FakeAudio();
      NumberStairs game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("number_stairs", "test"), 9);
      AdvanceToClimb(game, player, builder);
      Assert.IsTrue(game.QuestionTold, "the question was read on the circle");
      // The landing (band 10) is standing PAST the target.
      Vector3 landing = arena.transform.TransformPoint(new Vector3(0f,
        StairHillBuilder.StepCount * StairHillBuilder.Rise,
        StairHillBuilder.BaseZ + StairHillBuilder.StepCount * StairHillBuilder.Tread + 1.0f));
      player.transform.position = landing;
      for (int i = 0; i < 9; i++) game.Tick(0.1f); // settle + stand-above dwell
      Assert.AreEqual(10, game.CurrentStep, "the landing reads past the target");
      Assert.AreEqual(1, game.Overshoots, "guided once");
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "no fail, no reset (brief §19)");
      Assert.IsFalse(game.ResultShown, "no result yet");
      StepTo(game, player, builder, 9);
      for (int i = 0; i < 20 && game.Current != NumberStairs.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "coming back to 9 succeeds");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // E. Undershoot (target 5): S3-P2Z22 (user: "để trẻ tự suy nghĩ") a settled
  // stand below the target earns NO "how many more" hint; the climb still wins.
  [Test] public void P54E_UndershootNoHint() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P54PlayerU");
    try {
      builder.BoardTarget = 5;
      builder.BuildContent(arena.transform);
      FakeAudio audio = new FakeAudio();
      NumberStairs game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("number_stairs", "test"), 5);
      AdvanceToClimb(game, player, builder);
      int mark = audio.Lines.Count;
      StepTo(game, player, builder, 2);
      for (int i = 0; i < 30; i++) game.Tick(0.1f); // settle 3s on step 2
      Assert.AreEqual(0, game.UndershootNudges, "no 'how many more' hint (child thinks)");
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "no hint, no fail");
      string nudge = DialogueLang.T("Three more steps!", "Còn ba bậc nữa nhé!");
      Assert.IsFalse(audio.Lines.GetRange(mark, audio.Lines.Count - mark).Contains(nudge),
        "the remainder hint is gone");
      for (int i = 0; i < 40; i++) game.Tick(0.1f);
      Assert.AreEqual(0, game.UndershootNudges, "still no hint");
      StepTo(game, player, builder, 3);
      StepTo(game, player, builder, 4);
      StepTo(game, player, builder, 5);
      for (int i = 0; i < 20 && game.Current != NumberStairs.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "the climb still wins");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // F. Boundary debounce (brief §16): a body flickering across a tread boundary
  // commits nothing and speaks nothing; a held stand commits exactly once.
  [Test] public void P54F_BoundaryDebounce() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P54PlayerD");
    try {
      builder.BuildContent(arena.transform);
      FakeAudio audio = new FakeAudio();
      NumberStairs game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("number_stairs", "test"), 3);
      AdvanceToClimb(game, player, builder);
      StepTo(game, player, builder, 2);
      int mark = audio.Lines.Count;
      string three = DialogueLang.T("Three.", "Ba.");
      for (int i = 0; i < 8; i++) { // straddle the 2/3 boundary faster than the settle
        player.transform.position = WorldStand(builder, i % 2 == 0 ? 3 : 2);
        game.Tick(0.05f);
      }
      Assert.AreEqual(2, game.CurrentStep, "flicker commits nothing");
      Assert.IsFalse(audio.Lines.GetRange(mark, audio.Lines.Count - mark).Contains(three),
        "flicker speaks nothing");
      player.transform.position = WorldStand(builder, 3);
      for (int i = 0; i < 8; i++) game.Tick(0.1f); // a real stand commits
      Assert.AreEqual(3, game.CurrentStep, "a held stand commits");
      int threes = audio.Lines.GetRange(mark, audio.Lines.Count - mark).FindAll(s => s == three).Count;
      Assert.AreEqual(1, threes, "one entry = one count (brief §16)");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // G. Maximum target walk (brief §22): 1..9 track, count, dwell, complete —
  // with the recap draining after success.
  [Test] public void P54G_MaxStep9Walk() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P54PlayerM");
    try {
      builder.BoardTarget = 9;
      builder.BuildContent(arena.transform);
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("number_stairs", "test");
      NumberStairs game = BuildGame(builder, arena, player, audio, life, 9);
      AdvanceToClimb(game, player, builder);
      for (int i = 1; i <= 9; i++) {
        StepTo(game, player, builder, i);
        Assert.AreEqual(i, game.CurrentStep, "step " + i + " tracks");
      }
      for (int i = 0; i < 20 && game.Current != NumberStairs.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "target 9 completes");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.IsTrue(game.ResultShown, "result shown");
      for (int i = 0; i < 160; i++) game.Tick(0.1f); // recap 1..9 drains, paced
      for (int i = 1; i <= 9; i++) {
        string line = DialogueLang.T(
          new[] { "One.", "Two.", "Three.", "Four.", "Five.", "Six.", "Seven.", "Eight.", "Nine." }[i - 1],
          new[] { "Một.", "Hai.", "Ba.", "Bốn.", "Năm.", "Sáu.", "Bảy.", "Tám.", "Chín." }[i - 1]);
        Assert.IsTrue(audio.Lines.Contains(line), "count/recap " + i + " spoken");
      }
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Nine steps! Well done!", "Chín bậc! Giỏi!")), "confirm names nine");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // H. Backtracking at a high target (brief §21): the count IS the position.
  [Test] public void P54H_BacktrackAt7() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P54PlayerB");
    try {
      builder.BuildContent(arena.transform);
      NumberStairs game = BuildGame(builder, arena, player, new FakeAudio(),
        new ActivityLifecycle("number_stairs", "test"), 7);
      AdvanceToClimb(game, player, builder);
      foreach (int s in new[] { 1, 4, 2, 6, 5 }) StepTo(game, player, builder, s);
      Assert.AreEqual(5, game.CurrentStep, "1->4->2->6->5 reads 5 (never 1+1+1+1)");
      Assert.AreEqual(6, game.HighestStep, "the climb was tracked");
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "no accidental success");
      Assert.AreEqual(0, game.Overshoots, "nothing above 7 was ever stood on");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // I. The area's target ladder (brief §34/§35): 3 -> 5 -> 7 -> 9 -> 1 -> 3,
  // advanced exactly when a Completed life still belongs to the current target;
  // "-stair-target N" pins any rung for a diagnostic run.
  [Test] public void P54I_TargetLadderAndCli() {
    GameObject areaGo = new GameObject("P54AreaLadder");
    try {
      CountingGardenArea area = areaGo.AddComponent<CountingGardenArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      Assert.AreEqual(3, area.StairTarget, "first visit teaches the reference 3");
      Assert.AreEqual(3, area.StairTarget, "ladder starts at 3");
      foreach (int want in new[] { 5, 7, 9, 1, 3 }) {
        Complete(area.StairLifecycle);
        area.TickStairProgressionForTests();
        Assert.AreEqual(want, area.StairTarget, "ladder advances to " + want);
        Assert.AreNotEqual(ActivityState.Completed, area.StairLifecycle.State,
          "a fresh life rides the new rung");
      }
      // A diagnostic target rejoins the ladder (12 is off-ladder: 3 next).
      area.SetStairTargetForTests(9);
      Complete(area.StairLifecycle);
      area.TickStairProgressionForTests();
      Assert.AreEqual(1, area.StairTarget, "9 wraps to the 1-step breather");
      // A mid-lesson re-entry (not Completed) never advances.
      CountingGardenArea area2 = new GameObject("P54AreaMid").AddComponent<CountingGardenArea>();
      try {
        area2.Bind(null, null, null, null, null, Vector3.zero);
        area2.TickStairProgressionForTests();
        Assert.AreEqual(3, area2.StairTarget, "no completion, no advance");
      } finally { Object.DestroyImmediate(area2.gameObject); }
      // CLI parsing (pure): pin the rung, reject strays.
      Assert.AreEqual(7, CountingGardenArea.ParseStairTargetArg(new[] { "x", "-stair-target", "7" }, 3),
        "CLI pins 7");
      Assert.AreEqual(3, CountingGardenArea.ParseStairTargetArg(new[] { "-stair-target", "12" }, 3),
        "out-of-range falls back");
      Assert.AreEqual(3, CountingGardenArea.ParseStairTargetArg(new[] { "x" }, 3), "no flag, no change");
      Assert.AreEqual(3, CountingGardenArea.ParseStairTargetArg(null, 3), "null args, no change");
    } finally { Object.DestroyImmediate(areaGo); }
  }

  static void Complete(ActivityLifecycle life) {
    life.MarkAvailable("test");
    life.BeginEnter("test");
    life.MarkReady("test");
    life.Begin("test");
    life.MarkCompleted("test");
  }

  // K. Singular grammar at target 1 (§34): one step reads "step", the base
  // nudges with the remainder, and every produced line passes the NPC cap.
  [Test] public void P54K_SingularGrammarAt1() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P54Player1");
    try {
      builder.BoardTarget = 1;
      builder.BuildContent(arena.transform);
      FakeAudio audio = new FakeAudio();
      NumberStairs game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("number_stairs", "test"), 1);
      AdvanceToClimb(game, player, builder);
      Assert.IsTrue(game.QuestionTold, "the question was read on the circle");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("Climb one step!", "Con lên một bậc nhé!")),
        "intro uses the singular");
      // Stand at the foot: S3-P2Z22 no hint is spoken any more.
      player.transform.position = arena.transform.position + new Vector3(0f, 0f, 2.0f);
      for (int i = 0; i < 30; i++) game.Tick(0.1f);
      Assert.AreEqual(0, game.UndershootNudges, "no hint at the foot");
      Assert.IsFalse(audio.Lines.Contains(
        DialogueLang.T("One more step!", "Còn một bậc nữa nhé!")), "no singular nudge");
      StepTo(game, player, builder, 1);
      for (int i = 0; i < 20 && game.Current != NumberStairs.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "one step completes");
      Assert.IsTrue(audio.Lines.Contains(
        DialogueLang.T("One step! Well done!", "Một bậc! Giỏi!")), "singular confirm");
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line passes the NPC cap: '" + line + "' (" + why + ")");
      }
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // J. Speech safety at every rung: EVERY line the round can produce (counts,
  // confirm, recap, nudges, guidance, intro, handoff) passes the NPC cap in
  // both languages — recorded from a REAL target-9 run, not recomposed.
  [Test] public void P54J_LinesSafetyAt9() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P54PlayerL");
    try {
      builder.BoardTarget = 9;
      builder.BuildContent(arena.transform);
      FakeAudio audio = new FakeAudio();
      NumberStairs game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("number_stairs", "test"), 9);
      AdvanceToClimb(game, player, builder);                       // intro + 9-step demo
      for (int i = 1; i <= 4; i++) StepTo(game, player, builder, i);
      for (int i = 0; i < 30; i++) game.Tick(0.1f); // settle on 4: undershoot nudge
      for (int i = 5; i <= 8; i++) StepTo(game, player, builder, i);
      StepTo(game, player, builder, 9);
      Vector3 landing = arena.transform.TransformPoint(
        new Vector3(0f, StairHillBuilder.StepCount * StairHillBuilder.Rise,
        StairHillBuilder.BaseZ + StairHillBuilder.StepCount * StairHillBuilder.Tread + 1.0f));
      player.transform.position = landing;        // overshoot, then back
      for (int i = 0; i < 9; i++) game.Tick(0.1f);
      StepTo(game, player, builder, 9);
      for (int i = 0; i < 20 && game.Current != NumberStairs.Phase.Success; i++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "target 9 completes");
      for (int i = 0; i < 160; i++) game.Tick(0.1f); // recap drains
      Assert.Greater(audio.Lines.Count, 20, "a full round speaks plenty");
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line passes the NPC cap: '" + line + "' (" + why + ")");
      }
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("Nine.", "Chín.")), "nine counted");
      Assert.IsFalse(audio.Lines.Contains(
        DialogueLang.T("Five more steps!", "Còn năm bậc nữa nhé!")), "no remainder hint (child thinks)");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // M. Arithmetic round model (S3-P2Z32): +/- rounds carry operands + a guarded
  // result; the board stages the expression (A op B) and the operator glyph.
  [Test] public void P54M_ArithmeticRoundModel() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P54PlayerAR");
    try {
      builder.BuildContent(arena.transform);
      NumberStairs game = arena.AddComponent<NumberStairs>();
      game.Build(builder, player.transform, null, new FakeAudio(),
        new ActivityLifecycle("number_stairs", "test"), 3);
      game.SetRoundForTests((int)NumberStairs.RoundKind.Sub, 5, 2);
      Assert.AreEqual(NumberStairs.RoundKind.Sub, game.Kind, "sub round");
      Assert.AreEqual(5, game.OpA, "first operand (the body's step)");
      Assert.AreEqual(2, game.OpB, "second operand");
      Assert.AreEqual(3, game.Target, "5 - 2 = 3");
      Assert.IsNotNull(FindDeep(arena.transform, "SHQuestion"), "expression group staged");
      Assert.IsNotNull(FindDeep(arena.transform, "SHNumberDigitA"), "operand A glyph");
      Assert.IsNotNull(FindDeep(arena.transform, "SHNumberDigitB"), "operand B glyph");
      Assert.IsNull(FindDeep(arena.transform, "SHQuestionOpV"), "minus has no vertical bar");
      game.SetRoundForTests((int)NumberStairs.RoundKind.Add, 3, 4);
      Assert.AreEqual(7, game.Target, "3 + 4 = 7");
      Assert.IsNotNull(FindDeep(arena.transform, "SHQuestionOpV"), "plus draws the cross");
      game.SetRoundForTests((int)NumberStairs.RoundKind.Sub, 2, 5);
      Assert.AreEqual(1, game.OpB, "sub B clamps below A");
      Assert.AreEqual(1, game.Target, "2 - 1 = 1 (never zero/negative)");
    } finally { Object.DestroyImmediate(player); Object.DestroyImmediate(arena); }
  }

  // N. Live arithmetic: a win shows the SOLVED equation and reads the
  // explanation before the next round is asked IN PLACE (child keeps their
  // step). The first question stays plain (the area's opening target).
  [Test] public void P54N_OperationWinSolvedBoardAndNextInPlace() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P54PlayerAN");
    try {
      builder.BoardTarget = 3;
      builder.BuildContent(arena.transform);
      FakeAudio audio = new FakeAudio();
      NumberStairs game = arena.AddComponent<NumberStairs>();
      game.ArithmeticEnabled = true;
      game.Build(builder, player.transform, null, audio,
        new ActivityLifecycle("number_stairs", "test"), 3);
      game.ForceNextRoundForTests((int)NumberStairs.RoundKind.Add, 3, 2);
      AdvanceToClimb(game, player, builder);
      Assert.AreEqual(NumberStairs.RoundKind.Plain, game.Kind, "first round is plain (the target)");
      StepTo(game, player, builder, 3);
      for (int t = 0; t < 40 && game.Current != NumberStairs.Phase.Success; t++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "plain round won");
      for (int t = 0; t < 220 && game.Target != 5; t++) game.Tick(0.1f);
      Assert.AreEqual(5, game.Target, "next add round 3+2=5");
      Assert.AreEqual(NumberStairs.RoundKind.Add, game.Kind, "add round staged");
      Assert.AreEqual(3, game.CurrentStep, "child keeps their step (no walk back)");
      Assert.IsFalse(game.ResultShown, "intermediate wins never settle the visit");
      Assert.IsNotNull(FindDeep(arena.transform, "SHNumberDigitA"), "expression A on the board");
      Assert.IsNotNull(FindDeep(arena.transform, "SHQuestionOpV"), "the + operator on the board");
      for (int t = 0; t < 220 && game.Current != NumberStairs.Phase.Climb; t++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "operation question read");
      StepTo(game, player, builder, 4);
      StepTo(game, player, builder, 5);
      for (int t = 0; t < 40 && game.Current != NumberStairs.Phase.Success; t++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "add round won");
      Assert.IsNotNull(FindDeep(arena.transform, "SHNumberDigitR"), "solved result glyph");
      Assert.IsNotNull(FindDeep(arena.transform, "SHQuestionEq0"), "the = sign");
      int solvedTarget = game.Target;
      for (int t = 0; t < 400 && !game.ExplanationSpokenForTests(); t++) game.Tick(0.1f);
      Assert.IsTrue(game.ExplanationSpokenForTests(), "explanation fully spoken");
      Assert.IsTrue(audio.Lines.Contains(DialogueLang.T("That's right! This is addition.",
        "Đúng rồi! Đây là phép cộng.")), "addition explained");
      Assert.AreEqual(5, game.CurrentStep, "the child still stands on step 5");
      for (int t = 0; t < 220 && game.Target == solvedTarget; t++) game.Tick(0.1f);
      Assert.AreNotEqual(solvedTarget, game.Target, "the next random round follows in place");
    } finally { Object.DestroyImmediate(player); Object.DestroyImmediate(arena); }
  }

  // O. Gentle re-ask (S3-P2Z32 "nhắc lại nhẹ"): on a +/- round, a settled stand
  // on a NON-target step re-reads the have/want question — never a scold.
  [Test] public void P54O_GentleReaskOnWrongStep() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P54PlayerGR");
    try {
      builder.BuildContent(arena.transform);
      FakeAudio audio = new FakeAudio();
      NumberStairs game = arena.AddComponent<NumberStairs>();
      game.Build(builder, player.transform, null, audio,
        new ActivityLifecycle("number_stairs", "test"), 3);
      game.SetRoundForTests((int)NumberStairs.RoundKind.Add, 3, 2); // 3 + 2 = 5
      AdvanceToClimb(game, player, builder);
      Assert.IsTrue(game.QuestionTold, "the question was read");
      int mark = audio.Lines.Count;
      StepTo(game, player, builder, 2);              // a non-target step
      for (int t = 0; t < 110; t++) game.Tick(0.1f); // ~11s settled
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "no fail on a wrong step");
      int asks = audio.Lines.GetRange(mark, audio.Lines.Count - mark)
        .FindAll(s => s == DialogueLang.T("How many more steps?", "Đi thêm mấy bậc?")).Count;
      Assert.GreaterOrEqual(asks, 1, "the have/want question is gently re-read");
      Assert.AreEqual(0, game.UndershootNudges, "never the old remainder hint");
    } finally { Object.DestroyImmediate(player); Object.DestroyImmediate(arena); }
  }

  // P. Number-line rail (S3-P2Z32 "dải trục số"): one orb per step; climbed
  // steps light gold, the round's target orb takes the sky colour.
  [Test] public void P54P_NumberRailLights() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    try {
      builder.BuildContent(arena.transform);
      for (int i = 1; i <= 9; i++)
        Assert.IsNotNull(FindDeep(arena.transform, "SHNumberRailOrb" + i), "rail orb " + i);
      Transform orb1 = FindDeep(arena.transform, "SHNumberRailOrb1");
      Transform orb5 = FindDeep(arena.transform, "SHNumberRailOrb5");
      builder.SetRail(0, 0);
      Color dim = orb1.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
      builder.SetRail(2, 5);
      Color lit = orb1.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
      Color goal = orb5.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");
      Assert.Greater(lit.r, dim.r + 0.05f, "orb 1 lights as the child climbs");
      Assert.Greater(goal.b, goal.r, "the target orb reads sky (blue > red)");
    } finally { Object.DestroyImmediate(arena); }
  }

  // Q. Subtraction direction (S3-P2Z32): the child STARTS on step OpA (above
  // the result) and walks down — that start is never an "overshoot"; the
  // "too far" side is BELOW the result.
  [Test] public void P54Q_SubRoundOvershootIsDirectionAware() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P54PlayerQ");
    try {
      builder.BuildContent(arena.transform);
      FakeAudio audio = new FakeAudio();
      NumberStairs game = arena.AddComponent<NumberStairs>();
      game.Build(builder, player.transform, null, audio,
        new ActivityLifecycle("number_stairs", "test"), 3);
      game.SetRoundForTests((int)NumberStairs.RoundKind.Sub, 7, 4); // 7 - 4 = 3
      AdvanceToClimb(game, player, builder);
      Assert.AreEqual(7, game.OpA, "starts on step 7");
      Assert.AreEqual(3, game.Target, "result step 3");
      StepTo(game, player, builder, 7);              // the starting operand
      for (int i = 0; i < 12; i++) game.Tick(0.1f);
      Assert.AreEqual(0, game.Overshoots, "the subtraction start is never a nag");
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "still climbing");
      StepTo(game, player, builder, 2);              // below the result
      for (int i = 0; i < 12; i++) game.Tick(0.1f);
      Assert.GreaterOrEqual(game.Overshoots, 1, "below the result is guided back");
      Assert.AreEqual(NumberStairs.Phase.Climb, game.Current, "no fail on a wrong step");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }

  // L. Chained questions (user round "từ bậc 3 đi tiếp cho câu hỏi sau"): after
  // a win the next rung is asked from where the child already stands — no walk
  // back, no re-entry, no arena demo — and the visit settles when the ladder
  // wraps to the start target (3 -> 5 -> 7 -> 9 -> 1).
  [Test] public void P54L_ChainedQuestionsContinueInPlace() {
    GameObject arena;
    StairHillBuilder builder = BuildArena(out arena);
    GameObject player = new GameObject("P54PlayerChain");
    try {
      builder.BoardTarget = 3;
      builder.BuildContent(arena.transform);
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("number_stairs", "test");
      NumberStairs game = arena.AddComponent<NumberStairs>();
      game.Build(builder, player.transform, null, audio, life, 3); // chaining ON
      AdvanceToClimb(game, player, builder);
      Assert.AreEqual(3, game.Target, "first question stages the visit target");
      StepTo(game, player, builder, 3);
      Assert.IsTrue(game.QuestionTold, "the question was read on the circle");
      int[] chain = { 5, 7, 9, 1 };
      int expectedStep = 3;
      foreach (int next in chain) {
        for (int t = 0; t < 120 && game.Target != next; t++) game.Tick(0.1f);
        Assert.AreEqual(next, game.Target, "the next rung is asked");
        Assert.AreEqual(expectedStep, game.CurrentStep, "the child KEEPS their step (no walk back)");
        Assert.IsFalse(game.ResultShown, "intermediate wins do not settle the visit");
        for (int t = 0; t < 120 && game.Current != NumberStairs.Phase.Climb; t++) game.Tick(0.1f);
        StepTo(game, player, builder, next);
        expectedStep = next;
      }
      for (int t = 0; t < 200 && game.Current != NumberStairs.Phase.Success; t++) game.Tick(0.1f);
      Assert.AreEqual(NumberStairs.Phase.Success, game.Current, "the wrapped ladder settles");
      Assert.IsTrue(game.ResultShown, "the result board shows on the last win");
      Assert.AreEqual(1, game.CurrentStep, "the child finished where they stood");
      Assert.AreEqual(ActivityState.Completed, life.State, "the visit completes the shared lifecycle");
    } finally {
      Object.DestroyImmediate(player);
      Object.DestroyImmediate(arena);
    }
  }
}
