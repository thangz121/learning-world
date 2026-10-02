// TempUxRealJourney.cs — REAL-INPUT UX journey (2026-10-01, maynode).
// Boots ONLY with `-journeyreal`. Every click is a genuine OS click delivered
// by user32 SendInput to the FOREGROUND game window (UxOsInput); the cursor is
// moved gradually, never warped; nothing is injected into the Unity input
// event queue. Screenshots are real game frames and every shot gets a data
// sidecar (scene/player/phase census) for offline analysis.
//
// Rules honoured (user order 2026-10-01):
//  - a child standing still >=10s that is NOT an intentional wait or a
//    screenshot beat is captured as IDLE10 evidence;
//  - a leg that cannot find its way / is stuck is tagged at the highest damage
//    level (SEVERE), screenshotted, then skipped when the world can still be
//    left, otherwise the game RELAUNCHES at the next stage (-journey-from);
//  - the driver never fixes anything: it only records evidence.
// C# 9.0 only.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TempUxRealJourneyBoot {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() {
    string[] args = Environment.GetCommandLineArgs();
    bool want = false;
    for (int i = 0; i < args.Length; i++) {
      if (string.Equals(args[i], "-journeyreal", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
      if (string.Equals(args[i], "-uxrj-from", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
    }
    if (!want) return;
    GameObject go = new GameObject("TempUxRealJourney");
    GameObject.DontDestroyOnLoad(go);
    go.AddComponent<TempUxRealJourney>();
  }
}

public class TempUxRealJourney : MonoBehaviour {
  const float StuckMoveEps = 0.35f;
  const float StuckSeconds = 12f;
  const float ArrivedNoProgressSeconds = 10f;
  const float IdleSeconds = 10f;
  const float WatchdogSeconds = 2400f;

  // Ga Thứ Tự TẠM ĐÓNG 2026-10-02: rút "order" khỏi journey (mở lại thì thêm lại
  // stage + SkillIds/GameIds/ArenaScenes idx3 + case "order" + solver).
  static readonly string[] StageOrder = {
    "hub.boot", "math.yard",
    "counting.yard", "rabbit", "stairs",
    "math.return",
    "geometry", "comparison", "classification",
    "exploration.empty", "hub.final"
  };

  static readonly string[] SkillIds = { "math_geometry", "math_comparison", "math_classification" };
  static readonly string[] GameIds = { "shape_builder", "comparison_market", "classification_city" };
  static readonly string[] ArenaScenes = {
    GeometryPlayBuilder.SceneName, ComparisonMarketBuilder.SceneName,
    ClassificationCityBuilder.SceneName,
    RabbitPlayBuilder.SceneName, StairHillBuilder.SceneName
  };

  string _shotDir = "E:/LWW/real-shots";
  string _stageName = "";
  string _startStage = "";
  int _runNumber = 1;
  int _shots;
  int _clicks;
  int _steps;
  int _errors;
  int _severeCount;
  int _idleCount;
  bool _cancel;
  bool _stepOk;
  bool _stageSevere;
  bool _reloadNeeded;
  string _severeReason = "";
  bool _intent;              // deliberate stand (dialog/demo/wait/shot)
  bool _shotPending;         // a screenshot is being written this frame
  bool _relaunching;
  Camera _cam;
  ClickToMove _player;
  float _started;

  void Start() {
    _started = Time.realtimeSinceStartup;
    try {
      string[] args = Environment.GetCommandLineArgs();
      for (int i = 0; i + 1 < args.Length; i++) {
        if (string.Equals(args[i], "-shot-dir", StringComparison.OrdinalIgnoreCase)) _shotDir = args[i + 1];
        if (string.Equals(args[i], "-uxrj-from", StringComparison.OrdinalIgnoreCase)) _startStage = args[i + 1];
        if (string.Equals(args[i], "-journey-run", StringComparison.OrdinalIgnoreCase)) {
          int n; if (int.TryParse(args[i + 1], out n) && n > 0) _runNumber = n;
        }
      }
    } catch (Exception) { }
    try { System.IO.Directory.CreateDirectory(_shotDir); } catch (Exception) { }
    try { Application.runInBackground = true; } catch (Exception) { }
    try { ClickRouter.Diag = true; } catch (Exception) { }
    StartCoroutine(Watchdog());
    StartCoroutine(IdleMonitor());
    StartCoroutine(Main());
  }

  void Log(string m) { try { Debug.Log("[UXRJ] " + m); } catch (Exception) { } }

  IEnumerator Watchdog() {
    yield return new WaitForSeconds(WatchdogSeconds);
    Log("WATCHDOG_TIMEOUT elapsed=" + (Time.realtimeSinceStartup - _started).ToString("F0") + "s");
    Quit(2);
  }

  void Quit(int code) {
    Log("UXRJ_QUIT code=" + code);
    try { Application.Quit(code); } catch (Exception) { }
  }

  // ---- evidence -----------------------------------------------------------------

  static string Sanitize(string s) {
    if (string.IsNullOrEmpty(s)) return "step";
    char[] buf = new char[s.Length];
    for (int i = 0; i < s.Length; i++) {
      char c = s[i];
      buf[i] = (char.IsLetterOrDigit(c) || c == '-' || c == '_') ? c : '_';
    }
    return new string(buf);
  }

  string LoadedScenes() {
    StringBuilder sb = new StringBuilder();
    int n = SceneManager.sceneCount;
    for (int i = 0; i < n; i++) {
      Scene s = SceneManager.GetSceneAt(i);
      if (!s.IsValid() || !s.isLoaded) continue;
      if (sb.Length > 0) sb.Append('|');
      sb.Append(s.name);
    }
    return sb.ToString();
  }

  string PhaseCensus() {
    StringBuilder sb = new StringBuilder();
    try {
      GeometryPlay g = FindObjectOfType<GeometryPlay>();
      if (g != null) sb.Append("geo=").Append(g.Current).Append(' ');
      ComparisonMarket m = FindObjectOfType<ComparisonMarket>();
      if (m != null) sb.Append("cmp=").Append(m.Current).Append(' ');
      ClassificationCity c = FindObjectOfType<ClassificationCity>();
      if (c != null) sb.Append("cls=").Append(c.Current).Append(' ');
      RabbitFeed r = FindObjectOfType<RabbitFeed>();
      if (r != null) sb.Append("rab=").Append(r.Current).Append(" count=").Append(r.Count)
        .Append(" target=").Append(r.Target).Append(" subs=").Append(r.Submits).Append(' ');
      NumberStairs st = FindObjectOfType<NumberStairs>();
      if (st != null) sb.Append("stair=").Append(st.Current).Append(" step=").Append(st.CurrentStep)
        .Append(" target=").Append(st.Target).Append(' ');
      MicroWorldPortal[] ports = FindObjectsOfType<MicroWorldPortal>();
      int playPorts = 0;
      for (int i = 0; i < ports.Length; i++) if (ports[i] != null && ports[i].PlayExit) playPorts++;
      sb.Append("portals=").Append(ports.Length).Append("/play=").Append(playPorts).Append(' ');
    } catch (Exception) { }
    return sb.ToString();
  }

  string Census() {
    return "scene=[" + LoadedScenes() + "] player=" + Pos() + " " + PhaseCensus();
  }

  void Shot(string label) {
    try {
      DismissSystemDialogs();
      _intent = true;
      _shotPending = true;
      string tag = _shots.ToString("00") + "_" + Sanitize(label);
      string path = _shotDir + "/" + tag + ".png";
      ScreenCapture.CaptureScreenshot(path);
      try {
        System.IO.File.WriteAllText(_shotDir + "/" + tag + ".txt",
          "label=" + label + "\nstage=" + _stageName + "\ntime=" + (Time.realtimeSinceStartup - _started).ToString("F1")
          + "s\nclicks=" + _clicks + "\n" + Census() + "\n");
      } catch (Exception) { }
      Log("SHOT " + tag + " clicks=" + _clicks + " | " + Census());
      _shots++;
      _shotPending = false;
      _intent = false;
    } catch (Exception e) {
      _shotPending = false;
      _intent = false;
      Log("shot failed: " + e.Message);
    }
  }

  // Highest damage level (user order): recorded, screenshotted; the runner then
  // skips the leg or relaunches depending on whether the world can be left.
  void Severe(string tag, string reason) {
    if (_stageSevere) return;
    _stageSevere = true;
    _severeReason = reason;
    _severeCount++;
    Log("SEVERE[" + tag + "] " + reason + " | " + Census());
    Shot("SEVERE_" + tag + "_" + _stageName);
  }

  IEnumerator IdleMonitor() {
    Vector3 last = PlayerPos();
    float still = 0f;
    while (true) {
      yield return new WaitForSeconds(1f);
      if (_relaunching || _cancel) continue;
      if (_intent || _shotPending || StandExpected() || _watchOff) { still = 0f; last = PlayerPos(); continue; }
      Vector3 now = PlayerPos();
      Vector3 a = new Vector3(now.x, 0f, now.z);
      Vector3 b = new Vector3(last.x, 0f, last.z);
      if (Vector3.Distance(a, b) > 0.08f) { still = 0f; last = now; continue; }
      still += 1f;
      if (still >= IdleSeconds) {
        still = 0f;
        last = now;
        _idleCount++;
        Log("IDLE10 #" + _idleCount + " stage=" + _stageName + " | " + Census());
        Shot("IDLE10_" + _idleCount + "_" + _stageName);
      }
    }
  }

  // Intentional-stand holds: the leg itself owns the child's stillness.
  bool _watchOff; // true while a deliberate long wait owns the screen

  // ---- step accounting ----------------------------------------------------------

  bool Record(string step, bool ok, string detail) {
    _steps++;
    if (ok) {
      _failStreak = 0;
      Log("STEP_OK " + step + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
      return true;
    }
    _failStreak++;
    _errors++;
    Log("STEP_FAIL " + step + " streak=" + _failStreak + (string.IsNullOrEmpty(detail) ? "" : " " + detail) + " | " + Census());
    Shot("fail_" + Sanitize(step));
    if (_failStreak >= 3) {
      _cancel = true;
      Log("JOURNEY_CANCEL: three consecutive step failures (last: " + step + ")");
    }
    return false;
  }

  int _failStreak;
  bool StepGuard() { return !_cancel; }

  // ---- main / stage engine ------------------------------------------------------

  IEnumerator Main() {
    yield return Guard(MainBody());
    yield return Finish();
  }

  IEnumerator Guard(IEnumerator body) {
    while (true) {
      object cur = null;
      bool more = false;
      try {
        more = body.MoveNext();
        if (more) cur = body.Current;
      } catch (Exception e) {
        _errors++;
        _cancel = true;
        Log("EXCEPTION " + e.GetType().Name + ": " + e.Message);
        Log(e.StackTrace);
        yield break;
      }
      if (!more) yield break;
      yield return cur;
    }
  }

  IEnumerator Finish() {
    Shot(_cancel ? "99_cancel_final" : "99_final");
    Log("JOURNEY_END shots=" + _shots + " clicks=" + _clicks + " steps=" + _steps
      + " errors=" + _errors + " severe=" + _severeCount + " idle=" + _idleCount + " cancel=" + _cancel
      + " elapsed=" + (Time.realtimeSinceStartup - _started).ToString("F0") + "s");
    yield return new WaitForSeconds(1.5f);
    Log("JOURNEY_AUTOCLOSE");
    Quit(_cancel ? 2 : (_errors == 0 && _severeCount == 0 ? 0 : 1));
  }

  IEnumerator MainBody() {
    Log("JOURNEY_START (REAL OS input) run=" + _runNumber + " from='" + _startStage + "'");
    _watchOff = true;
    yield return new WaitForSeconds(6f);
    yield return WaitStep(delegate { return FindObjectOfType<LanguageDialog>() != null; }, 40f, "language dialog");
    Shot("00_boot");
    yield return new WaitForSeconds(0.5f);
    yield return ClickButtonByName("EnBox");
    yield return WaitStep(delegate {
      LanguageDialog d = FindObjectOfType<LanguageDialog>();
      return d == null || !d.IsOpen;
    }, 30f, "language chosen");
    _watchOff = false;
    Record("boot + language", _stepOk, "");
    Shot("01_main_hub");
    if (!StepGuard()) yield break;

    int start = StageIndex(_startStage);
    for (int i = 0; i < StageOrder.Length; i++) {
      if (i < start) { Log("STAGE_SKIPPED " + StageOrder[i]); continue; }
      if (_cancel) break;
      _stageName = StageOrder[i];
      _stageSevere = false;
      _reloadNeeded = false;
      _severeReason = "";
      Log("STAGE_BEGIN " + _stageName);
      yield return Guard(StageBody(_stageName));
      Log("STAGE_END " + _stageName + " severe=" + _stageSevere
        + (string.IsNullOrEmpty(_severeReason) ? "" : " reason=" + _severeReason));
      if (_reloadNeeded && !_relaunching) {
        int next = Mathf.Min(i + 1, StageOrder.Length - 1);
        Relaunch(StageOrder[next]);
        yield break;
      }
    }
  }

  IEnumerator StageBody(string name) {
    switch (name) {
      case "hub.boot": yield break;
      case "math.yard": yield return StageMathYard(); break;
      case "counting.yard": yield return StageCountingYard(); break;
      case "rabbit": yield return StageRabbit(); break;
      case "stairs": yield return StageStairs(); break;
      case "math.return": yield return StageMathReturn(); break;
      case "geometry": yield return StageArena(0); break;
      case "comparison": yield return StageArena(1); break;
      case "classification": yield return StageArena(2); break;
      case "exploration.empty": yield return StageExplorationEmpty(); break;
      case "hub.final": yield return StageHubFinal(); break;
    }
  }

  int StageIndex(string name) {
    if (string.IsNullOrEmpty(name)) return 0;
    for (int i = 0; i < StageOrder.Length; i++)
      if (string.Equals(StageOrder[i], name, StringComparison.OrdinalIgnoreCase)) return i;
    return 0;
  }

  // ---- stages -------------------------------------------------------------------

  IEnumerator StageMathYard() {
    yield return ReturnToMathSkillYard();
    if (_reloadNeeded) yield break;
    yield return new WaitForSeconds(2.5f);
    Shot("02_math_skill_yard");
    SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
    bool ok = b != null && !b.IsGameLevel && b.GatePortals.Count == 5;
    Record("math B: 5 skill doors + labels", ok,
      b == null ? "no builder" : "doors=" + b.GatePortals.Count);
  }

  IEnumerator StageCountingYard() {
    yield return ReturnToMathSkillYard();
    if (_reloadNeeded) yield break;
    yield return EnterDoorStep("math_counting", "counting",
      delegate { return YardGame("math_counting") != null; }, 120f);
    Record("counting C yard", _stepOk, "");
    if (!_stepOk) { Severe("counting.yard", "cannot enter the counting game yard"); yield break; }
    yield return new WaitForSeconds(2.5f);
    Shot("03_counting_game_yard");
    SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
    bool ok = b != null && b.IsGameLevel && b.GatePortals.Count == 2;
    Record("counting C: 2 game doors", ok, b == null ? "no builder" : "doors=" + b.GatePortals.Count);
  }

  IEnumerator StageRabbit() {
    yield return ReturnToMathSkillYard();
    if (_reloadNeeded) yield break;
    yield return EnterDoorStep("math_counting", "counting",
      delegate { return YardGame("math_counting") != null; }, 120f);
    if (!_stepOk) { Severe("rabbit", "no counting yard"); yield break; }
    yield return EnterDoorStep("rabbit_feeding", "rabbit arena",
      delegate { return SceneLoaded(RabbitPlayBuilder.SceneName); }, 150f);
    Record("rabbit arena loads", _stepOk, "");
    if (!_stepOk) { Severe("rabbit", "rabbit arena never loaded"); yield break; }
    _watchOff = true;
    yield return new WaitForSeconds(3f);
    Shot("04_rabbit_arrival");
    _watchOff = false;

    RabbitFeed game = FindObjectOfType<RabbitFeed>();
    if (game == null) { Severe("rabbit", "RabbitFeed missing"); yield break; }
    Transform spot = FindDeep(game.transform, "RPPlaySpot");
    if (spot != null) {
      yield return WalkStep(delegate {
        RabbitFeed f = FindObjectOfType<RabbitFeed>();
        return f != null && f.Current != RabbitFeed.Phase.Wait;
      }, spot.position, 1.2f, 90f, "rabbit play spot", null, null);
      Record("rabbit question starts", _stepOk, "");
    }
    _watchOff = true;
    yield return new WaitForSeconds(3.5f);
    Shot("05_rabbit_demo");
    yield return WaitStep(delegate {
      RabbitFeed f = FindObjectOfType<RabbitFeed>();
      return f != null && (f.Current == RabbitFeed.Phase.Feeding || f.Current == RabbitFeed.Phase.Success);
    }, 90f, "rabbit demo done");
    _watchOff = false;
    Record("rabbit demo ran", _stepOk, "");
    Shot("06_rabbit_after_demo");

    _watchOff = true;
    int submittedBefore = game.Submits;
    RabbitPlayBuilder bb = FindObjectOfType<RabbitPlayBuilder>();
    Vector3 bowl = bb != null && bb.FeedAnchor != null ? bb.FeedAnchor.position
      : RabbitPlayBuilder.WorldOffset;
    Vector3 bell = bb != null && bb.SubmitAnchor != null ? bb.SubmitAnchor.position
      : RabbitPlayBuilder.WorldOffset + RabbitPlayBuilder.SubmitLocal;
    for (int i = 0; i < 9; i++) {
      RabbitFeed f = FindObjectOfType<RabbitFeed>();
      if (f == null || f.Current != RabbitFeed.Phase.Feeding) break;
      if (f.Submits > submittedBefore || f.ResultShown) break;
      int before = f.Count;
      int need = f.Target - f.Count;
      if (need <= 0) {
        yield return WalkStep(delegate {
          RabbitFeed g = FindObjectOfType<RabbitFeed>();
          return g != null && (g.Submits > submittedBefore || g.ResultShown || g.Current == RabbitFeed.Phase.Success);
        }, bell, 1.3f, 60f, "rabbit submit", null, null);
        break;
      }
      RabbitCarrot c = FirstAvailable(f);
      if (c == null) { Log("rabbit: no available carrot at i=" + i); break; }
      Vector3 cp = c.transform.position;
      yield return WalkStep(delegate { return PlayerNear(cp, 1.2f); }, cp, 1.2f, 40f,
        "rabbit carrot " + i, null, null);
      if (i == 0) Shot("07_rabbit_carry");
      yield return WalkStep(delegate {
        RabbitFeed g = FindObjectOfType<RabbitFeed>();
        return g != null && g.Count > before;
      }, bowl, 1.4f, 50f, "rabbit bowl " + i, null, null);
      Shot("08_rabbit_fed_" + (i + 1));
    }
    _watchOff = false;
    RabbitFeed fin = FindObjectOfType<RabbitFeed>();
    bool won = fin != null && (fin.Submits > submittedBefore || fin.ResultShown);
    Record("rabbit submitted", won, fin == null ? "gone" : "subs=" + fin.Submits + " result=" + fin.ResultShown);
    yield return new WaitForSeconds(3f);
    Shot("09_rabbit_success");

    yield return ExitArenaStep("math_counting", "rabbit exit");
    Record("rabbit exit", _stepOk, "");
    if (!_stepOk) Severe("rabbit", "cannot leave the rabbit arena");
  }

  IEnumerator StageStairs() {
    yield return ReturnToMathSkillYard();
    if (_reloadNeeded) yield break;
    yield return EnterDoorStep("math_counting", "counting",
      delegate { return YardGame("math_counting") != null; }, 120f);
    if (!_stepOk) { Severe("stairs", "no counting yard"); yield break; }
    yield return EnterDoorStep("number_stairs", "stairs arena",
      delegate { return SceneLoaded(StairHillBuilder.SceneName); }, 150f);
    Record("stairs arena loads", _stepOk, "");
    if (!_stepOk) { Severe("stairs", "stairs arena never loaded"); yield break; }
    _watchOff = true;
    yield return new WaitForSeconds(3f);
    Shot("10_stairs_arrival");
    _watchOff = false;

    NumberStairs stairs = FindObjectOfType<NumberStairs>();
    if (stairs == null) { Severe("stairs", "NumberStairs missing"); yield break; }
    Vector3 listen = StairHillBuilder.WorldOffset + StairHillBuilder.ListenLocal;
    yield return WalkStep(delegate {
      NumberStairs s = FindObjectOfType<NumberStairs>();
      return s != null && s.Current != NumberStairs.Phase.Wait;
    }, listen, 1.2f, 90f, "stairs listen ring", null, null);
    Record("stairs question starts", _stepOk, "");
    _watchOff = true;
    yield return new WaitForSeconds(3.5f);
    Shot("11_stairs_demo");
    yield return WaitStep(delegate {
      NumberStairs s = FindObjectOfType<NumberStairs>();
      return s != null && (s.Current == NumberStairs.Phase.Climb || s.Current == NumberStairs.Phase.Success);
    }, 90f, "stairs demo done");
    _watchOff = false;
    Record("stairs demo ran", _stepOk, "");
    Shot("12_stairs_question");

    for (int round = 0; round < 3; round++) {
      NumberStairs s = FindObjectOfType<NumberStairs>();
      if (s == null || s.Current == NumberStairs.Phase.Success) break;
      int t = Mathf.Clamp(s.Target, 1, StairHillBuilder.StepCount);
      Vector3 stepWorld = StairHillBuilder.WorldOffset
        + new Vector3(StairHillBuilder.CenterX, 0f, StairHillBuilder.BaseZ + (t - 0.5f) * StairHillBuilder.Tread);
      Log("stairs round " + round + " target=" + t + " stepWorld=" + Fmt(stepWorld));
      yield return WalkStep(delegate {
        NumberStairs g = FindObjectOfType<NumberStairs>();
        return g != null && (g.Current == NumberStairs.Phase.Success || g.CurrentStep == t);
      }, stepWorld, 1.1f, 75f, "stairs climb to " + t, null, null);
      Shot("13_stairs_on_" + t);
      _watchOff = true;
      yield return WaitStep(delegate {
        NumberStairs g = FindObjectOfType<NumberStairs>();
        return g != null && (g.Current == NumberStairs.Phase.Success
          || g.Current == NumberStairs.Phase.Climb);
      }, 30f, "stairs settle " + t);
      _watchOff = false;
      NumberStairs after = FindObjectOfType<NumberStairs>();
      if (after != null && after.Current == NumberStairs.Phase.Success) break;
    }
    Record("stairs climbed to a target step", true, "target=" + stairs.Target);
    yield return new WaitForSeconds(2f);
    Shot("14_stairs_after");

    yield return ExitArenaStep("math_counting", "stairs exit");
    Record("stairs exit", _stepOk, "");
    if (!_stepOk) Severe("stairs", "cannot leave the stairs arena");
  }

  IEnumerator StageMathReturn() {
    yield return ReturnToMathSkillYard();
    if (_reloadNeeded) yield break;
    yield return new WaitForSeconds(1.5f);
    Shot("15_math_skill_yard_again");
    Record("math B restored", YardSkill("math") != null, "");
  }

  IEnumerator StageArena(int idx) {
    string skill = SkillIds[idx];
    string gameId = GameIds[idx];
    string scene = ArenaScenes[idx];
    string tag = Sanitize(gameId);
    yield return ReturnToMathSkillYard();
    if (_reloadNeeded) yield break;
    yield return EnterDoorStep(skill, tag,
      delegate { return YardGame(skill) != null; }, 120f);
    Record(skill + " C yard", _stepOk, "");
    if (!_stepOk) { Severe(tag + ".yard", "cannot enter " + skill + " game yard"); yield break; }
    yield return new WaitForSeconds(2f);
    Shot(tag + "_yard");

    yield return EnterDoorStep(gameId, tag,
      delegate { return SceneLoaded(scene); }, 150f);
    Record(gameId + " arena loads", _stepOk, "");
    if (!_stepOk) { Severe(tag, "arena never loaded: " + gameId); yield break; }
    _watchOff = true;
    yield return new WaitForSeconds(3.2f);
    Shot(tag + "_enter");
    _watchOff = false;

    Transform playSpot = FindNamed(PlaySpot(idx));
    if (playSpot != null) {
      yield return WalkStep(delegate { return ArenaPlaying(); }, playSpot.position, 1.2f, 90f,
        tag + " play spot", null, null);
      Record(gameId + " question starts", _stepOk, "");
    }
    _watchOff = true;
    yield return new WaitForSeconds(2f);
    Shot(tag + "_task");
    _watchOff = false;

    for (int round = 0; round < 3; round++) {
      Transform clickable = FirstArenaClickable();
      if (clickable == null) { Log(tag + " round " + round + ": no clickable"); break; }
      yield return WalkStep(delegate { return PlayerNear(clickable.position, 1.8f); },
        clickable.position, 1.8f, 50f, tag + " walk piece " + round, null, null);
      yield return TapWorld(clickable.position);
      _watchOff = true;
      yield return new WaitForSeconds(1.4f);
      Shot(tag + "_click_" + round);
      _watchOff = false;
    }

    yield return ExitArenaStep(skill, tag + " exit");
    Record(tag + " back to C", _stepOk, "");
    if (!_stepOk) { Severe(tag, "cannot leave arena " + gameId); yield break; }
    yield return new WaitForSeconds(1.5f);
    Shot(tag + "_back_c");
  }

  // Ga Thứ Tự TẠM ĐÓNG 2026-10-02: ordering solver rút cùng journey.

  // Ga Thứ Tự TẠM ĐÓNG 2026-10-02: SolveOrderingTap + TrackStr rút cùng journey.

  // Ga Thứ Tự TẠM ĐÓNG 2026-10-02: SolveOrderingTrack rút cùng journey.

  // Ga Thứ Tự TẠM ĐÓNG 2026-10-02: SlotForRank + PlaceInto rút cùng journey.

  IEnumerator StageExplorationEmpty() {
    yield return ReturnToHub();
    if (_reloadNeeded) yield break;
    yield return WalkIntoSubjectGateStep(SubjectIds.Exploration, "exploration", "into exploration");
    Record("exploration B", _stepOk, "");
    if (!_stepOk) { Severe("exploration", "cannot enter exploration yard"); yield break; }
    yield return new WaitForSeconds(2.5f);
    Shot("16_exploration_skill_yard");
    yield return EnterDoorStep("exploration_nature", "nature",
      delegate { return YardGame("exploration_nature") != null; }, 120f);
    Record("exploration empty C", _stepOk, "");
    yield return new WaitForSeconds(2.5f);
    Shot("17_empty_game_yard");
    SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
    Record("empty C: placeholder, zero doors", b != null && b.PlaceholderBoard != null && b.GatePortals.Count == 0,
      b == null ? "no builder" : "doors=" + b.GatePortals.Count);
    yield return BackStep(delegate { return YardSkill("exploration") != null; }, 120f, "back to exploration B");
    yield return BackStep(delegate { return !YardLoaded(); }, 120f, "back to hub");
  }

  IEnumerator StageHubFinal() {
    yield return ReturnToHub();
    if (_reloadNeeded) yield break;
    yield return new WaitForSeconds(2f);
    Shot("18_final_hub");
    Record("back at the hub", !YardLoaded() && !AnyArenaLoaded(), "");
  }

  // ---- navigation helpers -------------------------------------------------------

  IEnumerator ReturnToMathSkillYard() {
    int guard = 0;
    int escapes = 0;
    while (guard++ < 16) {
      if (_cancel) yield break;
      if (AnyArenaLoaded()) {
        yield return ExitArenaAny("to math");
        if (!_stepOk) { Severe("navigation", "cannot exit an arena to reach math"); _reloadNeeded = true; yield break; }
        yield return SettleInYard();
        continue;
      }
      SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
      if (b == null) {
        yield return WalkIntoSubjectGateStep(SubjectIds.Math, "math", "hub -> math");
        if (!_stepOk) { Severe("navigation", "cannot walk into the math gate from the hub"); _reloadNeeded = true; yield break; }
        continue;
      }
      Log("nav: yard level=" + b.Level + " subject=" + b.SubjectId + " skill=" + b.SkillId
        + " player=" + Pos() + " backGate=" + (b.BackGate != null ? Fmt(b.BackGate.transform.position) : "null"));
      if (!b.IsGameLevel && string.Equals(b.SubjectId, "math", StringComparison.OrdinalIgnoreCase)) yield break;
      bool wasGame = b.IsGameLevel;
      yield return SafeSpot(b, "yard safe");
      if (AnyArenaLoaded()) continue;
      yield return BackStep(delegate {
        return wasGame ? YardSkill("math") != null : !YardLoaded();
      }, 90f, wasGame ? "game yard -> math" : "yard -> hub");
      if (!_stepOk) {
        if (AnyArenaLoaded() && escapes++ < 3) {
          Severe("escape", "unexpected arena launched while leaving the "
            + (wasGame ? "game" : "skill") + " yard (a Play door fired on the way out)");
          continue;
        }
        Severe("navigation", "cannot back out of the current yard");
        _reloadNeeded = true;
        yield break;
      }
    }
    Log("ReturnToMathSkillYard exhausted its guard");
  }

  IEnumerator ReturnToHub() {
    int guard = 0;
    int escapes = 0;
    while (guard++ < 16) {
      if (_cancel) yield break;
      if (AnyArenaLoaded()) {
        yield return ExitArenaAny("to hub");
        if (!_stepOk) { Severe("navigation", "cannot exit an arena to reach the hub"); _reloadNeeded = true; yield break; }
        yield return SettleInYard();
        continue;
      }
      SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
      if (b == null) yield break;
      yield return SafeSpot(b, "yard safe");
      if (AnyArenaLoaded()) continue;
      yield return BackStep(delegate { return !YardLoaded(); }, 90f, "yard -> hub");
      if (!_stepOk) {
        if (AnyArenaLoaded() && escapes++ < 3) {
          Severe("escape", "unexpected arena launched while leaving a yard");
          continue;
        }
        Severe("navigation", "cannot back out to the hub");
        _reloadNeeded = true;
        yield break;
      }
    }
  }

  // Walk to the yard's entry safe spot (clear of every gate mouth) before any
  // back walk, so a stray Play door is never crossed.
  IEnumerator SafeSpot(SelectionYardBuilder b, string label) {
    yield return WaitCameraSettle();
    Vector3 safe = SelectionYardBuilder.WorldOffset + SelectionYardBuilder.EntryLocal;
    yield return WalkStep(delegate { return PlayerNear(safe, 1.4f); }, safe, 1.4f, 40f,
      label, delegate { return AnyArenaLoaded(); }, null);
  }

  IEnumerator SettleInYard() {
    yield return WaitStep(delegate { return YardLoaded(); }, 25f, "yard restored");
    Vector3 safe = SelectionYardBuilder.WorldOffset + SelectionYardBuilder.EntryLocal;
    yield return WaitStep(delegate { return PlayerNear(safe, 3f); }, 12f, "warp home");
    yield return WaitCameraSettle();
  }

  // A warp/arena hand-off can leave the camera mid-transition; projections are
  // meaningless until it stops moving.
  IEnumerator WaitCameraSettle() {
    Vector3 lastCam = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
    float stable = 0f;
    for (int i = 0; i < 48 && stable < 1.0f; i++) {
      yield return new WaitForSeconds(0.25f);
      Vector3 c = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
      if (Vector3.Distance(c, lastCam) < 0.05f) stable += 0.25f; else stable = 0f;
      lastCam = c;
    }
  }

  IEnumerator ExitArenaAny(string label) {
    _stepOk = false;
    MicroWorldPortal exit = null;
    yield return WaitStep(delegate {
      MicroWorldPortal[] ps = FindObjectsOfType<MicroWorldPortal>();
      foreach (MicroWorldPortal p in ps) {
        if (p != null && p.PlayExit) { exit = p; return true; }
      }
      return false;
    }, 30f, label + " portal found");
    if (!_stepOk || exit == null) yield break;
    Vector3 ep = exit.transform.position;
    Vector3 staging = new Vector3(ep.x, 0f, ep.z + 2.8f);
    yield return WalkStep(delegate { return PlayerNear(staging, 2.2f); }, staging, 2.2f, 60f,
      label + " staging", null, null);
    if (!_stepOk) yield break;
    yield return WalkStep(delegate { return !AnyArenaLoaded(); }, ep, 1.3f, 150f,
      label + " walk-in", null, null);
  }

  IEnumerator ExitArenaStep(string expectSkill, string label) {
    _stepOk = false;
    MicroWorldPortal exit = null;
    yield return WaitStep(delegate {
      MicroWorldPortal[] ps = FindObjectsOfType<MicroWorldPortal>();
      foreach (MicroWorldPortal p in ps) {
        if (p != null && p.PlayExit) { exit = p; return true; }
      }
      return false;
    }, 30f, label + " portal found");
    if (!_stepOk || exit == null) yield break;
    Vector3 ep = exit.transform.position;
    Vector3 staging = new Vector3(ep.x, 0f, ep.z + 2.8f);
    yield return WalkStep(delegate { return PlayerNear(staging, 2.2f); }, staging, 2.2f, 60f,
      label + " staging", null, null);
    if (!_stepOk) yield break;
    yield return WalkStep(delegate { return YardGame(expectSkill) != null; }, ep, 1.3f, 150f,
      label + " walk-in", null, null);
  }

  IEnumerator WalkIntoSubjectGateStep(SubjectId subject, string yardSubject, string label) {
    _stepOk = false;
    SubjectGate gate = null;
    yield return WaitStep(delegate { gate = FindGate(subject); return gate != null; }, 30f, label + " gate found");
    if (!_stepOk || gate == null) yield break;
    yield return WalkStep(delegate { return YardSkill(yardSubject) != null; }, gate.transform.position, 1.4f, 180f,
      label + " walk-in", delegate { return YardLoaded() && YardSkill(yardSubject) == null; }, subject.Value);
  }

  IEnumerator EnterDoorStep(string targetId, string label, Func<bool> done, float timeout) {
    _stepOk = false;
    SelectionGate door = null;
    yield return WaitStep(delegate {
      SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
      if (b == null) return false;
      for (int i = 0; i < b.GatePortals.Count; i++) {
        SelectionGate g = b.GatePortals[i];
        if (g != null && string.Equals(g.TargetId, targetId, StringComparison.OrdinalIgnoreCase)) { door = g; return true; }
      }
      return false;
    }, 30f, label + " door found");
    if (!_stepOk || door == null) yield break;
    Vector3 dp = door.transform.position;
    Vector3 staging = new Vector3(dp.x, 0f, dp.z - 3.2f);
    yield return WalkStep(delegate { return PlayerNear(staging, 2.2f); }, staging, 2.2f, 60f,
      label + " staging", null, null);
    if (!_stepOk) yield break;
    yield return WalkStep(done, dp, 1.2f, timeout, label + " walk-in", null, null);
  }

  IEnumerator BackStep(Func<bool> done, float timeout, string label) {
    _stepOk = false;
    SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
    if (b == null || b.BackGate == null) {
      Log("BACK_STEP_FAIL: " + label);
      Shot("fail_" + Sanitize(label));
      yield break;
    }
    Vector3 bp = b.BackGate.transform.position;
    Vector3 staging = new Vector3(bp.x, 0f, bp.z + 3.2f);
    yield return WaitCameraSettle();
    yield return WalkStep(delegate { return PlayerNear(staging, 2.4f); }, staging, 2.4f, 60f,
      label + " staging", delegate { return AnyArenaLoaded(); }, null);
    if (!_stepOk) yield break;
    yield return WalkStep(done, bp, 1.2f, timeout, label + " walk-in",
      delegate { return AnyArenaLoaded(); }, null);
  }

  IEnumerator WalkStep(Func<bool> cond, Vector3 toward, float arrive, float timeout, string label,
      Func<bool> wrong, string allowGateId) {
    _stepOk = false;
    RefreshRefs();
    float t = 0f;
    float still = 0f;
    float arrivedStill = 0f;
    int noProgress = 0;
    int rotIdx = 0;
    float px = Mathf.Max(1f, Screen.height / 720f);
    float[] rots = { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 150f, 180f };
    Vector3 last = _player != null ? _player.transform.position : Vector3.zero;
    while (t < timeout) {
      bool done = false;
      try { done = cond(); } catch (Exception) { }
      if (done) { _stepOk = true; Log("walk ok: " + label); yield break; }
      bool bad = false;
      try { bad = wrong != null && wrong(); } catch (Exception) { }
      if (bad) {
        Log("WRONG_TARGET " + label + " pos=" + Pos());
        Shot("wrong_" + Sanitize(label));
        yield break;
      }
      if (StandExpected()) {
        still = 0f;
        arrivedStill = 0f;
        if (IsLanguageOpen()) yield return ClickButtonByName("EnBox");
      } else {
        Vector3 p = _player != null ? _player.transform.position : last;
        Vector3 move = p - last;
        Vector3 toT = toward - p;
        toT.y = 0f;
        float distT = toT.magnitude;
        Vector3 dirT = distT > 0.1f ? toT / distT : Vector3.zero;
        bool helped = move.magnitude >= 0.25f && dirT != Vector3.zero && Vector3.Dot(move, dirT) > 0.08f;
        if (move.magnitude < StuckMoveEps) still += 1.2f; else still = 0f;
        last = p;
        if (PlayerNear(toward, arrive + 0.5f)) arrivedStill += 1.2f; else arrivedStill = 0f;
        if (still >= StuckSeconds) {
          Severe("no_path:" + label, "stuck: no movement for " + still.ToString("F0") + "s toward "
            + Fmt(toward) + " at " + Pos());
          Shot("stuck_" + Sanitize(label));
          yield break;
        }
        if (arrivedStill >= ArrivedNoProgressSeconds) {
          Severe("no_exit:" + label, "arrived at " + Fmt(toward) + " but the world never reacted (no context change)");
          Shot("noctx_" + Sanitize(label));
          yield break;
        }
        // WORLD-DIRECTION PROBING: if the direct heading does not move the
        // child toward the goal, rotate the heading until one demonstrably does.
        if (helped) { noProgress = 0; rotIdx = 0; }
        else {
          noProgress++;
          if (noProgress >= 2 && dirT != Vector3.zero) { rotIdx = (rotIdx + 1) % rots.Length; noProgress = 0; }
        }
        Vector3 moveDir = dirT != Vector3.zero ? dirT : new Vector3(0f, 0f, 1f);
        if (rotIdx > 0) moveDir = Quaternion.Euler(0f, rots[rotIdx], 0f) * dirT;
        float stepDist = Mathf.Clamp(distT, 0.4f, 2.5f);
        Vector3 stepW = p + moveDir * stepDist;
        foreach (SubjectDefinition def in SubjectCatalog.All) {
          if (def == null) continue;
          if (!string.IsNullOrEmpty(allowGateId)
              && string.Equals(def.Id.Value, allowGateId, StringComparison.OrdinalIgnoreCase)) continue;
          float dx = stepW.x - def.GatePos.x, dz = stepW.z - def.GatePos.z;
          float d2 = dx * dx + dz * dz;
          if (d2 < 2.3f * 2.3f) {
            Vector3 away = new Vector3(dx, 0f, dz);
            if (away.sqrMagnitude < 0.0001f) away = new Vector3(0f, 0f, -1f);
            away.Normalize();
            stepW = def.GatePos + away * 2.3f;
          }
        }
        Vector3 psp0 = _cam != null ? _cam.WorldToScreenPoint(p) : Vector3.zero;
        Vector3 sp = _cam != null ? _cam.WorldToScreenPoint(stepW) : Vector3.zero;
        if (sp.z <= 0f) {
          sp = psp0.z > 0f
            ? new Vector3(psp0.x, Mathf.Min(Screen.height - 60f, psp0.y + 220f * px), 0f)
            : new Vector3(Screen.width * 0.5f, Screen.height * 0.75f, 0f);
        }
        sp.x = Mathf.Clamp(sp.x, 40f, Screen.width - 40f);
        sp.y = Mathf.Clamp(sp.y, 40f, Screen.height - 40f);
        Log("walk " + label + " p=" + Pos() + " to=" + Fmt(toward) + " rot=" + rots[rotIdx]
          + " sp=(" + sp.x.ToString("F0") + "," + sp.y.ToString("F0") + ")");
        yield return Tap(new Vector2(sp.x, sp.y));
      }
      yield return new WaitForSeconds(1.2f);
      t += 1.2f;
      if (((int)t) % 12 == 0) RefreshRefs();
    }
    Log("WALK_TIMEOUT: " + label + " pos=" + Pos());
    Shot("timeout_" + Sanitize(label));
  }

  IEnumerator WaitStep(Func<bool> cond, float timeout, string label) {
    _stepOk = false;
    _intent = true;
    float t = 0f;
    while (t < timeout) {
      bool ok = false;
      try { ok = cond(); } catch (Exception) { }
      if (ok) { _stepOk = true; _intent = false; Log("ok: " + label); yield break; }
      yield return new WaitForSeconds(0.5f);
      t += 0.5f;
    }
    _intent = false;
    Log("TIMEOUT: " + label);
    Shot("timeout_" + Sanitize(label));
  }

  // ---- arena play probes --------------------------------------------------------

  static bool ArenaPlaying() {
    GeometryPlay geo = FindObjectOfType<GeometryPlay>();
    if (geo != null) return geo.Current != GeometryPlay.Phase.Wait;
    ComparisonMarket m = FindObjectOfType<ComparisonMarket>();
    if (m != null) return m.Current != ComparisonMarket.Phase.Wait;
    ClassificationCity c = FindObjectOfType<ClassificationCity>();
    if (c != null) return c.Current != ClassificationCity.Phase.Wait;
    return false;
  }

  static string PlaySpot(int idx) {
    switch (idx) {
      case 0: return "GPPlaySpot";
      case 1: return "CMPlaySpot";
      case 2: return "CCPlaySpot";
    }
    return "";
  }

  static Transform FirstArenaClickable() {
    GeometryPiece gp = FirstIdlePiece();
    if (gp != null) return gp.transform;
    ComparisonChoice ch = FirstChoice();
    if (ch != null) return ch.transform;
    ClassificationItem it = FirstIdleItem();
    if (it != null) return it.transform;
    return null;
  }

  static GeometryPiece FirstIdlePiece() {
    GeometryPiece[] all = FindObjectsOfType<GeometryPiece>();
    for (int i = 0; i < all.Length; i++)
      if (all[i] != null && !all[i].EnvRole && all[i].State == GeometryPiece.PieceState.Idle) return all[i];
    return null;
  }

  static ComparisonChoice FirstChoice() {
    ComparisonChoice[] all = FindObjectsOfType<ComparisonChoice>();
    for (int i = 0; i < all.Length; i++)
      if (all[i] != null && !all[i].IsCart) return all[i];
    return null;
  }

  static ClassificationItem FirstIdleItem() {
    ClassificationItem[] all = FindObjectsOfType<ClassificationItem>();
    for (int i = 0; i < all.Length; i++)
      if (all[i] != null && all[i].State == ClassificationItem.ItemState.Idle) return all[i];
    return null;
  }

  // Ga Thứ Tự TẠM ĐÓNG 2026-10-02: FirstIdleOrder rút cùng journey.

  static RabbitCarrot FirstAvailable(RabbitFeed game) {
    for (int i = 0; i < game.CarrotCount; i++) {
      RabbitCarrot c = game.CarrotAt(i);
      if (c != null && c.State == RabbitCarrot.CarrotState.Available) return c;
    }
    return null;
  }

  // ---- state probes -------------------------------------------------------------

  static SubjectGate FindGate(SubjectId target) {
    SubjectGate[] gates = FindObjectsOfType<SubjectGate>();
    foreach (SubjectGate g in gates)
      if (g != null && !g.IsReturnGate && g.Target == target) return g;
    return null;
  }

  static SelectionYardBuilder YardSkill(string subject) {
    if (!SceneLoaded(SelectionYardBuilder.SceneName)) return null;
    SelectionYardBuilder[] bs = FindObjectsOfType<SelectionYardBuilder>();
    foreach (SelectionYardBuilder b in bs)
      if (b != null && !b.IsGameLevel
          && string.Equals(b.SubjectId, subject, StringComparison.OrdinalIgnoreCase)) return b;
    return null;
  }

  static SelectionYardBuilder YardGame(string skill) {
    if (!SceneLoaded(SelectionYardBuilder.SceneName)) return null;
    SelectionYardBuilder[] bs = FindObjectsOfType<SelectionYardBuilder>();
    foreach (SelectionYardBuilder b in bs)
      if (b != null && b.IsGameLevel
          && string.Equals(b.SkillId, skill, StringComparison.OrdinalIgnoreCase)) return b;
    return null;
  }

  static bool AnyMathGameYard() {
    if (!SceneLoaded(SelectionYardBuilder.SceneName)) return false;
    SelectionYardBuilder[] bs = FindObjectsOfType<SelectionYardBuilder>();
    foreach (SelectionYardBuilder b in bs)
      if (b != null && b.IsGameLevel && !string.IsNullOrEmpty(b.SkillId)
          && b.SkillId.StartsWith("math_", StringComparison.OrdinalIgnoreCase)) return true;
    return false;
  }

  static bool AnyArenaLoaded() {
    for (int i = 0; i < ArenaScenes.Length; i++)
      if (SceneLoaded(ArenaScenes[i])) return true;
    return false;
  }

  static bool YardLoaded() {
    return SceneLoaded(SelectionYardBuilder.SceneName) && FindObjectOfType<SelectionYardBuilder>() != null;
  }

  static bool SceneLoaded(string name) {
    Scene s = SceneManager.GetSceneByName(name);
    return s.IsValid() && s.isLoaded;
  }

  bool PlayerNear(Vector3 world, float radius) {
    if (_player == null) return false;
    Vector3 p = _player.transform.position;
    float dx = p.x - world.x, dz = p.z - world.z;
    return dx * dx + dz * dz <= radius * radius;
  }

  Vector3 PlayerPos() {
    try {
      if (_player == null) _player = FindObjectOfType<ClickToMove>();
      return _player != null ? _player.transform.position : Vector3.zero;
    } catch (Exception) { return Vector3.zero; }
  }

  string Pos() {
    Vector3 p = PlayerPos();
    return "(" + p.x.ToString("F1") + "," + p.z.ToString("F1") + ")";
  }

  static string Fmt(Vector3 v) {
    return "(" + v.x.ToString("F1") + "," + v.z.ToString("F1") + ")";
  }

  bool StandExpected() {
    try {
      if (IsLanguageOpen()) return true;
      MicSetupDialog mic = FindObjectOfType<MicSetupDialog>();
      if (mic != null && mic.IsShowing) return true;
      SelectionYardArea area = FindObjectOfType<SelectionYardArea>();
      if (area != null && area.IsBusy) return true;
    } catch (Exception) { }
    return false;
  }

  static bool IsLanguageOpen() {
    try {
      LanguageDialog d = FindObjectOfType<LanguageDialog>();
      return d != null && d.IsOpen;
    } catch (Exception) { return false; }
  }

  void DismissSystemDialogs() {
    try {
      MicSetupDialog mic = FindObjectOfType<MicSetupDialog>();
      if (mic != null && mic.IsShowing) mic.Hide();
      PhoneCameraHud camHud = FindObjectOfType<PhoneCameraHud>();
      if (camHud != null && camHud.IsShowing) camHud.SetRecordingHide(true);
      MicStatusHud micHud = FindObjectOfType<MicStatusHud>();
      if (micHud != null && micHud.enabled) { micHud.enabled = false; micHud.gameObject.SetActive(false); }
    } catch (Exception) { }
  }

  static Transform FindNamed(string name) {
    if (string.IsNullOrEmpty(name)) return null;
    Transform[] all = FindObjectsOfType<Transform>();
    for (int i = 0; i < all.Length; i++)
      if (all[i] != null && all[i].name == name) return all[i];
    return null;
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

  void RefreshRefs() {
    try {
      if (_player == null) _player = FindObjectOfType<ClickToMove>();
      _cam = Camera.main;
    } catch (Exception) { }
  }

  // ---- REAL input -----------------------------------------------------------------

  IEnumerator TapWorld(Vector3 world) {
    RefreshRefs();
    if (_cam == null) yield break;
    Vector3 sp = _cam.WorldToScreenPoint(world);
    if (sp.z <= 0f) yield break;
    yield return Tap(new Vector2(sp.x, sp.y));
  }

  IEnumerator Tap(Vector2 unityScreen) {
    _clicks++;
    UxOsInput.Focus();
    Vector2 os = UxOsInput.ToScreenPoint(unityScreen);
    yield return MoveCursor((int)os.x, (int)os.y);
    yield return null;
    yield return null;
    UxOsInput.LeftDown();
    yield return new WaitForSeconds(0.06f);
    UxOsInput.LeftUp();
    yield return new WaitForSeconds(0.06f);
  }

  IEnumerator MoveCursor(int tx, int ty) {
    if (!UxOsInput.Focus()) Log("WARN: focus failed");
    for (int i = 0; i < 800; i++) {
      Vector2 c = UxOsInput.Cursor();
      float dx = tx - c.x, dy = ty - c.y;
      float d = Mathf.Sqrt(dx * dx + dy * dy);
      if (d <= 3f) yield break;
      float step = Mathf.Min(d, 16f);
      UxOsInput.Step(Mathf.RoundToInt(dx / d * step), Mathf.RoundToInt(dy / d * step));
      yield return null;
    }
  }

  bool FindButton(string name, out RectTransform rt) {
    rt = null;
    foreach (Button b in FindObjectsOfType<Button>()) {
      if (b == null || b.name != name) continue;
      RectTransform r = b.GetComponent<RectTransform>();
      if (r == null) continue;
      rt = r; return true;
    }
    return false;
  }

  IEnumerator ClickButtonByName(string name) {
    RectTransform rt;
    if (!FindButton(name, out rt)) { Log("WARN: button not found: " + name); yield break; }
    Vector2 screen = UiScreenPos(rt);
    if (screen.x < 0f) yield break;
    Log("ui click: " + name + " at " + screen);
    yield return Tap(screen);
  }

  Vector2 UiScreenPos(RectTransform rt) {
    try {
      if (rt == null) return new Vector2(-1f, -1f);
      Canvas canvas = rt.GetComponentInParent<Canvas>();
      if (canvas == null) return new Vector2(-1f, -1f);
      RectTransform root = canvas.GetComponent<RectTransform>();
      Vector3[] rc = new Vector3[4];
      root.GetWorldCorners(rc);
      float minX = Mathf.Min(rc[0].x, rc[3].x), maxX = Mathf.Max(rc[0].x, rc[3].x);
      float minY = Mathf.Min(rc[0].y, rc[1].y), maxY = Mathf.Max(rc[0].y, rc[1].y);
      float fx = Screen.width / Mathf.Max(1f, maxX - minX);
      float fy = Screen.height / Mathf.Max(1f, maxY - minY);
      Vector3[] bc = new Vector3[4];
      rt.GetWorldCorners(bc);
      Vector3 c = (bc[0] + bc[2]) * 0.5f;
      return new Vector2((c.x - minX) * fx, (c.y - minY) * fy);
    } catch (Exception) { return new Vector2(-1f, -1f); }
  }

  // ---- relaunch-from-stage (only when the world cannot be left) -----------------

  void Relaunch(string fromStage) {
    _relaunching = true;
    if (_runNumber >= 4) {
      Log("relaunch limit reached (run " + _runNumber + "); stopping for review");
      _reloadNeeded = false;
      return;
    }
    try {
      string exe = Environment.GetCommandLineArgs()[0];
      string[] args = Environment.GetCommandLineArgs();
      StringBuilder sb = new StringBuilder();
      string logPath = null;
      for (int i = 1; i < args.Length; i++) {
        if (string.Equals(args[i], "-journey-run", StringComparison.OrdinalIgnoreCase)) { i++; continue; }
        if (string.Equals(args[i], "-shot-dir", StringComparison.OrdinalIgnoreCase)) { i++; continue; }
        if (string.Equals(args[i], "-uxrj-from", StringComparison.OrdinalIgnoreCase)) { i++; continue; }
        if (string.Equals(args[i], "-journey-from", StringComparison.OrdinalIgnoreCase)) { i++; continue; }
        if (string.Equals(args[i], "-logFile", StringComparison.OrdinalIgnoreCase)) { logPath = i + 1 < args.Length ? args[i + 1] : null; i++; continue; }
        if (sb.Length > 0) sb.Append(' ');
        sb.Append(args[i]);
      }
      try {
        if (!string.IsNullOrEmpty(logPath) && System.IO.File.Exists(logPath))
          System.IO.File.Copy(logPath, _shotDir + "/severe_run" + _runNumber + ".log", true);
      } catch (Exception) { }
      string root = RunRoot(_shotDir);
      string nextShotDir = root + "/run" + (_runNumber + 1);
      sb.Append(" -journey-run ").Append(_runNumber + 1);
      sb.Append(" -shot-dir ").Append(nextShotDir);
      if (!string.IsNullOrEmpty(fromStage)) sb.Append(" -uxrj-from ").Append(fromStage);
      if (!string.IsNullOrEmpty(logPath)) sb.Append(" -logFile ").Append(nextShotDir).Append("/player.log");
      System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, sb.ToString()));
      Log("RELAUNCH started run " + (_runNumber + 1) + " from " + fromStage);
    } catch (Exception e) {
      Log("relaunch failed: " + e.Message);
    }
    try { Application.Quit(3); } catch (Exception) { }
  }

  static string RunRoot(string dir) {
    int i = dir.LastIndexOf("/run", StringComparison.OrdinalIgnoreCase);
    if (i < 0) return dir;
    string tail = dir.Substring(i + 4);
    if (tail.Length == 0) return dir;
    for (int k = 0; k < tail.Length; k++)
      if (!char.IsDigit(tail[k])) return dir;
    return dir.Substring(0, i);
  }
}
