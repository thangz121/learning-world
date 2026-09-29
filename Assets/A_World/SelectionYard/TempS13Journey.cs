// TempS13Journey.cs — PHASE 3 FOREGROUND JOURNEY (FULL ARCHITECTURE RESET).
// Committed with the phase (standing order: journey drivers ride the bundle).
// Drop-in path Assets/A_World/SelectionYard/TempS13Journey.cs.
// Inert in production: boots ONLY with the `-journeys3` CLI flag
// (per-driver flags convention: -journey55/56/57/62/full/s3 are mutually
// exclusive). Real InputSystem mouse injection, condition-based walking,
// real-state polling only. Optional: `-shot-dir <path>`. AUTO-CLOSES always
// (normal end, cancel, watchdog or a caught exception).
//
// STUCK DISCIPLINE (user order 2026-09-29):
//  - standing is NOT an error while a system dialog is open or a transition
//    is in flight (the driver waits);
//  - zero movement while a walk is active is STUCK: screenshot, the step
//    fails, the journey SKIPS to the next step;
//  - arriving with no context change is its own ARRIVED_NO_PROGRESS error;
//  - TWO CONSECUTIVE failed steps cancel the journey (JOURNEY_CANCEL) and
//    close the game so the failure can be hot-fixed from the evidence;
//  - every tap is VALIDATED by real movement; a failed tap triggers a
//    screen-space probe sweep (camera-proof wall-follow);
//  - taps never land inside a non-target gate's 2m click-snap circle.
//
// Paths proven:
//   A hub -> Math gate  -> B "Toán học — Chọn kỹ năng" (5 skill doors)
//   Đếm door -> C "Đếm — Chọn trò chơi" (2 game doors, both accepted)
//   C back -> B back -> A hub (no legacy scene ever loads)
//   A hub -> Khám phá gate -> B "Khám phá — Chọn kỹ năng" (4 skill doors)
//   Tự nhiên door -> C empty yard [CHƯA CÓ TRÒ CHƠI] (no fake game)
// NOTE (product finding for the phase report): the THINKING gate cannot be
// reached by click-walking from the hub — its only corridor squeezes between
// the Thinking district U-carve, the English pillar carve and the English
// gate's 2m click-snap circle (3 foreground attempts: wrong-yard fire before
// the snap guard, permanent orbit after it). The empty-yard rule is proven
// via Khám phá; a corridor/snap fix belongs to a later phase.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TempS13JourneyBoot {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() {
    string[] args = Environment.GetCommandLineArgs();
    bool want = false;
    foreach (string a in args) {
      if (string.Equals(a, "-journeys3", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
    }
    if (!want) return;
    GameObject go = new GameObject("TempS13Journey");
    GameObject.DontDestroyOnLoad(go);
    go.AddComponent<TempS13Journey>();
  }
}

public class TempS13Journey : MonoBehaviour {
  const string DefaultShotDir = "D:/Vscode/s13j-shots";
  const float StuckMoveEps = 0.35f;           // metres of movement that count as progress
  const float StuckSeconds = 16f;             // zero progress while a walk is active -> error
  const float ArrivedNoProgressSeconds = 12f; // at the target, no context change -> error
  const float WatchdogSeconds = 720f;

  static readonly string[] MathSkillIds = {
    "math_counting", "math_geometry", "math_comparison", "math_classification", "math_order"
  };
  static readonly string[] ExplorationSkillIds = {
    "exploration_nature", "exploration_animals", "exploration_world", "exploration_daily_life"
  };

  string _shotDir = DefaultShotDir;
  int _shots;
  int _clicks;
  int _steps;
  int _errors;
  int _failStreak;
  bool _cancel;
  bool _stepOk;
  Camera _cam;
  ClickToMove _player;
  float _started;

  void Start() {
    _started = Time.realtimeSinceStartup;
    try {
      string[] args = Environment.GetCommandLineArgs();
      for (int i = 0; i + 1 < args.Length; i++) {
        if (string.Equals(args[i], "-shot-dir", StringComparison.OrdinalIgnoreCase)) {
          if (!string.IsNullOrEmpty(args[i + 1])) _shotDir = args[i + 1];
          break;
        }
      }
    } catch (Exception) { }
    try { System.IO.Directory.CreateDirectory(_shotDir); } catch (Exception) { }
    try { InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; }
    catch (Exception) { }
    StartCoroutine(Watchdog());
    StartCoroutine(Main());
  }

  void Log(string m) {
    try { Debug.Log("[S13J] " + m); } catch (Exception) { }
  }

  // The window ALWAYS closes: the watchdog is only the outermost net.
  IEnumerator Watchdog() {
    yield return new WaitForSeconds(WatchdogSeconds);
    Log("WATCHDOG_TIMEOUT elapsed=" + (Time.realtimeSinceStartup - _started).ToString("F0") + "s");
    Quit(2);
  }

  void Quit(int code) {
    try { Debug.Log("[S13J] S13J_QUIT code=" + code); } catch (Exception) { }
    try { Application.Quit(code); } catch (Exception) { }
  }

  void DismissSystemDialogs() {
    try {
      MicSetupDialog mic = FindObjectOfType<MicSetupDialog>();
      if (mic != null && mic.IsShowing) { mic.Hide(); Log("dismissed mic dialog"); }
      PhoneCameraHud camHud = FindObjectOfType<PhoneCameraHud>();
      if (camHud != null && camHud.IsShowing) camHud.SetRecordingHide(true);
      MicStatusHud micHud = FindObjectOfType<MicStatusHud>();
      if (micHud != null && micHud.enabled) { micHud.enabled = false; micHud.gameObject.SetActive(false); }
    } catch (Exception) { }
  }

  void Shot(string label) {
    try {
      DismissSystemDialogs();
      string path = _shotDir + "/" + _shots.ToString("00") + "_" + label + ".png";
      ScreenCapture.CaptureScreenshot(path);
      Log("SHOT " + _shots.ToString("00") + " " + label + " clicks=" + _clicks);
      _shots++;
    } catch (Exception e) { Log("shot failed: " + e.Message); }
  }

  static string Sanitize(string s) {
    if (string.IsNullOrEmpty(s)) return "step";
    char[] buf = new char[s.Length];
    for (int i = 0; i < s.Length; i++) {
      char c = s[i];
      buf[i] = (char.IsLetterOrDigit(c) || c == '-' || c == '_') ? c : '_';
    }
    return new string(buf);
  }

  bool Expect(string label, bool ok, string detail) {
    if (ok) { Log("EXPECT_OK " + label + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")")); return true; }
    _errors++;
    Log("EXPECT_FAIL " + label + " " + detail);
    return false;
  }

  // One naming/accounting unit per test step. Two consecutive failures cancel.
  bool Record(string step, bool ok, string detail) {
    _steps++;
    if (ok) {
      _failStreak = 0;
      Log("STEP_OK " + step + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
      return true;
    }
    _failStreak++;
    Log("STEP_FAIL " + step + " streak=" + _failStreak + (string.IsNullOrEmpty(detail) ? "" : " " + detail));
    Shot("fail_" + Sanitize(step));
    if (_failStreak >= 2) {
      _cancel = true;
      Log("JOURNEY_CANCEL: two consecutive step failures (last: " + step + ")");
    }
    return false;
  }

  // True = keep going. False = the journey was cancelled (caller must break).
  bool StepGuard() {
    if (!_cancel) return true;
    Log("CANCEL_ACTIVE -> stopping the journey now");
    return false;
  }

  // ---- guarded main: an exception anywhere still reaches Finish() ---------------

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
      + " errors=" + _errors + " cancel=" + _cancel
      + " elapsed=" + (Time.realtimeSinceStartup - _started).ToString("F0") + "s");
    yield return new WaitForSeconds(1.5f);
    Log("JOURNEY_AUTOCLOSE");
    Quit(_cancel ? 2 : (_errors == 0 ? 0 : 1));
  }

  // ---- journey ------------------------------------------------------------------

  IEnumerator MainBody() {
    Log("JOURNEY_START (phase 3: A -> B -> C skeleton, empty-yard rule)");
    yield return new WaitForSeconds(6f);
    Shot("00_boot");
    yield return WaitStep(delegate { return FindObjectOfType<LanguageDialog>() != null; }, 30f, "language dialog");
    yield return new WaitForSeconds(1f);
    if (!ClickButtonByName("EnBox")) Log("WARN: EnBox not found");
    yield return WaitStep(delegate {
      LanguageDialog d = FindObjectOfType<LanguageDialog>();
      return d == null || !d.IsOpen;
    }, 20f, "language chosen");
    Record("boot + language", _stepOk, "");
    if (!StepGuard()) yield break;
    Shot("01_main_hub");

    // ---- STEP: A -> B (TOAN HOC) --------------------------------------------
    yield return WalkIntoSubjectGateStep(SubjectIds.Math, "math", "walk into Math gate");
    Record("A -> B math skill yard", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2.5f);
    Shot("02_math_skill_yard");
    Record("B content: math 5 doors", CheckSkillYard("math", MathSkillIds), "");
    if (!StepGuard()) yield break;

    // ---- STEP: B -> C (DEM) --------------------------------------------------
    yield return EnterDoorStep("math_counting", "Dem",
      delegate { return YardGame("math_counting") != null; }, 120f);
    Record("B -> C counting game yard", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2.5f);
    Shot("03_counting_game_yard");
    Record("C content: counting 2 accepted games", CheckCountingGameYard(), "");
    if (!StepGuard()) yield break;

    // ---- STEP: C -> GAME (rabbit) -> C (PHASE 4 round trip) ------------------
    yield return EnterDoorStep("rabbit_feeding", "Cho thỏ ăn",
      delegate { return SceneLoaded(RabbitPlayBuilder.SceneName); }, 150f);
    Record("C -> rabbit arena loads", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(3f);
    Shot("03b_rabbit_arena");
    Expect("rabbit arena: RabbitFeed present", FindObjectOfType<RabbitFeed>() != null, "");
    Expect("rabbit arena: game yard unloaded", !YardLoaded(), "");
    Record("rabbit arena content", FindObjectOfType<RabbitFeed>() != null && !YardLoaded(), "");
    if (!StepGuard()) yield break;

    // USER ROUND 2026-09-29: walk onto the play spot — the question runs and
    // (first visit) the DEMO plays before the child gets control. The journey
    // proves the demo path end-to-end in the real build.
    RabbitFeed feed = FindObjectOfType<RabbitFeed>();
    Transform spot = feed != null ? FindDeep(feed.transform, "RPPlaySpot") : null;
    Expect("rabbit arena: play spot staged", spot != null, "");
    if (spot != null) {
      yield return WalkStep(delegate {
        RabbitFeed f = FindObjectOfType<RabbitFeed>();
        return f != null && f.Current != RabbitFeed.Phase.Wait;
      }, spot.position, 1.2f, 90f, "onto the play spot", null, null);
      Record("rabbit arena: question starts on the spot", _stepOk, "");
      if (!StepGuard()) yield break;
      yield return new WaitForSeconds(3.5f);
      Shot("03b1_demo_running");
      yield return WaitStep(delegate {
        RabbitFeed f = FindObjectOfType<RabbitFeed>();
        return f != null && (f.Current == RabbitFeed.Phase.Feeding
          || f.Current == RabbitFeed.Phase.Success);
      }, 60f, "demo done -> child control");
      Record("rabbit arena: demo ran, child got control", _stepOk, "");
      if (!StepGuard()) yield break;
      Shot("03b2_after_demo");
    }

    yield return ExitArenaStep("math_counting", "rabbit arena exit");
    Record("GAME -> C back (rabbit)", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2.5f);
    Shot("03c_back_in_game_yard");
    Record("C re-entry after rabbit: 2 doors intact", CheckCountingGameYard(), "");
    if (!StepGuard()) yield break;

    // ---- STEP: C -> GAME (stairs) -> C (USER ROUND: same discipline) --------
    yield return EnterDoorStep("number_stairs", "Bac thang",
      delegate { return SceneLoaded(StairHillBuilder.SceneName); }, 150f);
    Record("C -> stairs arena loads", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(3f);
    Shot("03d_stairs_arena");
    Expect("stairs arena: NumberStairs present", FindObjectOfType<NumberStairs>() != null, "");
    Record("stairs arena content", FindObjectOfType<NumberStairs>() != null, "");
    if (!StepGuard()) yield break;
    NumberStairs stairs = FindObjectOfType<NumberStairs>();
    Transform listen = stairs != null ? FindDeep(stairs.transform, "SHListenRing") : null;
    Expect("stairs arena: listen circle staged", listen != null, "");
    if (listen != null) {
      yield return WalkStep(delegate {
        NumberStairs g = FindObjectOfType<NumberStairs>();
        return g != null && g.Current != NumberStairs.Phase.Wait;
      }, listen.position, 1.2f, 90f, "onto the listen circle", null, null);
      Record("stairs arena: question starts on the circle", _stepOk, "");
      if (!StepGuard()) yield break;
      yield return new WaitForSeconds(3.5f);
      Shot("03d1_stairs_demo_running");
      yield return WaitStep(delegate {
        NumberStairs g = FindObjectOfType<NumberStairs>();
        return g != null && (g.Current == NumberStairs.Phase.Climb
          || g.Current == NumberStairs.Phase.Success);
      }, 60f, "stairs demo done -> child control");
      Record("stairs arena: demo ran, child got control", _stepOk, "");
      if (!StepGuard()) yield break;
      Shot("03d2_after_stairs_demo");
    }
    yield return ExitArenaStep("math_counting", "stairs arena exit");
    Record("GAME -> C back (stairs)", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2.5f);
    Shot("03e_back_in_game_yard_2");
    Record("C re-entry after stairs: 2 doors intact", CheckCountingGameYard(), "");
    if (!StepGuard()) yield break;

    // ---- STEP: C -> B -> A back ---------------------------------------------
    yield return BackStep(delegate { return YardSkill("math") != null; }, 120f, "back to math skill yard");
    Record("C -> B back (math)", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2f);
    Shot("04_back_to_skill_yard");
    Record("B re-entry: single builder/area, 5 doors", CheckSkillYard("math", MathSkillIds), "");
    if (!StepGuard()) yield break;

    yield return BackStep(delegate { return !YardLoaded(); }, 120f, "back to the subject yard");
    Record("B -> A back", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2f);
    Shot("05_back_in_subject_yard");
    SelectionYardArea area = FindObjectOfType<SelectionYardArea>();
    bool hubOk = (area == null || !area.IsInside) && PlayerNear(SubjectCatalog.HubCenter, 7f);
    bool legacyOk = !SceneLoaded("MathScene") && !SceneLoaded("CountingGardenScene")
      && !SceneLoaded("DiscoveryScene");
    Expect("A restored: area outside", area == null || !area.IsInside, Pos());
    Expect("A restored: player near hub", PlayerNear(SubjectCatalog.HubCenter, 7f), Pos());
    Expect("no legacy scene ever loaded", legacyOk, "");
    Record("B -> A context restored + no legacy scene", hubOk && legacyOk, "");
    if (!StepGuard()) yield break;

    // ---- STEP: A -> B (KHAM PHA) -> C empty (TU NHIEN) ----------------------
    // (Phase report: the Thinking gate is unreachable by click-walking — its
    // corridor is pinched by the district U-carve + the English snap circle;
    // the dead-center Khám phá gate proves the same subject/empty-yard rule.)
    yield return WalkIntoSubjectGateStep(SubjectIds.Exploration, "exploration", "walk into Kham pha gate");
    Record("A -> B exploration skill yard", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2.5f);
    Shot("06_exploration_skill_yard");
    Record("B content: exploration 4 doors", CheckSkillYard("exploration", ExplorationSkillIds), "");
    if (!StepGuard()) yield break;

    yield return EnterDoorStep("exploration_nature", "Tu nhien",
      delegate { return YardGame("exploration_nature") != null; }, 120f);
    Record("B -> C empty game yard (Tu nhien)", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2.5f);
    Shot("07_empty_game_yard");
    Record("C empty: placeholder only, zero doors", CheckEmptyGameYard(), "");
    if (!StepGuard()) yield break;

    yield return BackStep(delegate { return YardSkill("exploration") != null; }, 120f, "back to exploration skill yard");
    Record("C -> B back (exploration)", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2f);
    Shot("08_back_exploration_skill_yard");

    yield return BackStep(delegate { return !YardLoaded(); }, 120f, "back to the subject yard (2)");
    Record("B -> A back (exploration)", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2f);
    Shot("09_final_subject_yard");
    bool finalOk = !SceneLoaded("MathScene") && !SceneLoaded("CountingGardenScene")
      && !SceneLoaded("DiscoveryScene") && !SceneLoaded(SelectionYardBuilder.SceneName);
    Expect("no legacy scene at the end", finalOk, "");
    Record("final: subject yard at rest, no legacy scene", finalOk, "");
  }

  // ---- content checks (each returns the whole step's verdict) ---------------------

  bool CheckSkillYard(string subject, string[] want) {
    SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
    if (b == null) { Expect(subject + " B exists", false, "no builder in scene"); return false; }
    bool ok = true;
    ok &= Expect(subject + " B: level = skill", !b.IsGameLevel, b.Level);
    ok &= Expect(subject + " B: " + want.Length + " doors", b.GatePortals.Count == want.Length,
      "count=" + b.GatePortals.Count);
    for (int i = 0; i < want.Length; i++) {
      string got = i < b.GateTargetIds.Count ? b.GateTargetIds[i] : "missing";
      ok &= Expect(subject + " B door " + i + " = " + want[i], got == want[i], got);
    }
    ok &= Expect(subject + " B: title board staged", b.TitleBoard != null, "");
    ok &= Expect(subject + " B: no placeholder board", b.PlaceholderBoard == null, "");
    ok &= Expect(subject + " B: back gate bound", b.BackGate != null, "");
    ok &= Expect(subject + " B: single builder (idempotent)", CountBuilders() == 1, "count=" + CountBuilders());
    ok &= Expect(subject + " B: single selection area", CountAreas() == 1, "count=" + CountAreas());
    for (int i = 0; i < b.GatePortals.Count; i++)
      ok &= Expect(subject + " B door kind Game " + i, b.GatePortals[i].Kind == SelectionGate.GateKind.Game,
        b.GatePortals[i].Kind.ToString());
    if (subject == "math") {
      int hints = CountHints(b.transform);
      ok &= Expect("math B: only the live skill glows", hints == 1, "hints=" + hints);
    }
    return ok;
  }

  bool CheckCountingGameYard() {
    SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
    if (b == null) { Expect("counting C exists", false, "no builder"); return false; }
    bool ok = true;
    ok &= Expect("counting C: level = game", b.IsGameLevel, b.Level);
    ok &= Expect("counting C: 2 game doors", b.GatePortals.Count == 2, "count=" + b.GatePortals.Count);
    if (b.GateTargetIds.Count >= 2) {
      ok &= Expect("counting C door 0 = rabbit_feeding", b.GateTargetIds[0] == "rabbit_feeding", b.GateTargetIds[0]);
      ok &= Expect("counting C door 1 = number_stairs", b.GateTargetIds[1] == "number_stairs", b.GateTargetIds[1]);
    }
    for (int i = 0; i < b.GatePortals.Count; i++)
      ok &= Expect("counting C door kind Play " + i, b.GatePortals[i].Kind == SelectionGate.GateKind.Play,
        b.GatePortals[i].Kind.ToString());
    ok &= Expect("counting C: no placeholder", b.PlaceholderBoard == null, "");
    ok &= Expect("counting C: title board", b.TitleBoard != null, "");
    ok &= Expect("counting C: back gate bound", b.BackGate != null, "");
    return ok;
  }

  bool CheckEmptyGameYard() {
    SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
    if (b == null) { Expect("empty C exists", false, "no builder"); return false; }
    bool ok = true;
    ok &= Expect("empty C: level = game", b.IsGameLevel, b.Level);
    ok &= Expect("empty C: ZERO game doors (no fake game)", b.GatePortals.Count == 0,
      "count=" + b.GatePortals.Count);
    ok &= Expect("empty C: placeholder board staged", b.PlaceholderBoard != null, "");
    ok &= Expect("empty C: title board", b.TitleBoard != null, "");
    ok &= Expect("empty C: back gate bound", b.BackGate != null, "");
    Transform placeholder = FindDeep(b.transform, "SYPlaceholderLabel");
    ok &= Expect("empty C: placeholder label staged", placeholder != null, "");
    return ok;
  }

  static int CountHints(Transform t) {
    int n = 0;
    MicroGateHint[] hints = t.GetComponentsInChildren<MicroGateHint>(true);
    for (int i = 0; i < hints.Length; i++) if (hints[i] != null) n++;
    return n;
  }

  // ---- step-wise movement ---------------------------------------------------------

  // A -> subject gate: DIRECT closed-loop walk (the loop wall-follows around
  // the district boundary carves and empirically corrects any projection
  // weirdness — see WalkStep). Aborts if the WRONG yard opens.
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
      label + " walk-in", delegate { return YardLoaded() && YardSkill(yardSubject) == null; },
      subject.Value);
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
    Vector3 staging = new Vector3(dp.x, 0f, dp.z - 3.2f); // south of the door, clear of neighbours
    yield return WalkStep(delegate { return PlayerNear(staging, 2.2f); }, staging, 2.2f, 60f,
      label + " staging", null, null);
    if (!_stepOk) yield break;
    yield return WalkStep(done, dp, 1.2f, timeout, label + " walk-in", null, null);
  }

  IEnumerator BackStep(Func<bool> done, float timeout, string label) {
    _stepOk = false;
    SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
    if (b == null || b.BackGate == null) {
      Log("BACK_STEP_FAIL: " + label + " (no builder / back gate present)");
      Shot("fail_" + Sanitize(label));
      yield break;
    }
    Vector3 bp = b.BackGate.transform.position;
    Vector3 staging = new Vector3(bp.x, 0f, bp.z + 3.2f); // north of the back gate
    yield return WalkStep(delegate { return PlayerNear(staging, 2.4f); }, staging, 2.4f, 60f,
      label + " staging", null, null);
    if (!_stepOk) yield break;
    yield return WalkStep(done, bp, 1.2f, timeout, label + " walk-in", null, null);
  }

  // Closed-loop walk: every tap is validated by real player movement toward
  // the target; a failed tap triggers an empirical 9-point screen probe sweep
  // (near-player screen offsets) so the walk works whatever the camera does.
  IEnumerator WalkStep(Func<bool> cond, Vector3 toward, float arrive, float timeout, string label,
      Func<bool> wrong, string allowGateId) {
    _stepOk = false;
    RefreshRefs();
    float t = 0f;
    float still = 0f;
    float arrivedStill = 0f;
    Vector3 last = _player != null ? _player.transform.position : Vector3.zero;
    Vector2? learned = null; // screen-space offset that demonstrably helps
    int badCycles = 0;
    float px = Mathf.Max(1f, Screen.height / 720f); // resolution-independent probes
    Vector2[] probes = {
      new Vector2(260f * px, 0f),
      new Vector2(-260f * px, 0f),
      new Vector2(0f, 190f * px),
      new Vector2(0f, -190f * px),
      new Vector2(260f * px, 190f * px),
      new Vector2(260f * px, -190f * px),
      new Vector2(-260f * px, 190f * px),
      new Vector2(-260f * px, -190f * px),
      new Vector2(0f, -330f * px),
    };
    while (t < timeout) {
      bool done = false;
      try { done = cond(); } catch (Exception) { }
      if (done) { _stepOk = true; Log("walk ok: " + label); yield break; }
      bool bad = false;
      try { bad = wrong != null && wrong(); } catch (Exception) { }
      if (bad) {
        Log("WRONG_TARGET " + label + " pos=" + Pos() + " (an unexpected yard is active)");
        Shot("wrong_" + Sanitize(label));
        yield break;
      }
      if (StandExpected()) {
        still = 0f;
        arrivedStill = 0f;
        if (IsLanguageOpen()) ClickButtonByName("EnBox");
      } else {
        Vector3 p = _player != null ? _player.transform.position : last;
        Vector3 move = p - last;
        Vector3 toT = toward - p;
        toT.y = 0f;
        float distT = toT.magnitude;
        Vector3 dirT = distT > 0.1f ? toT / distT : Vector3.zero;
        bool helped = move.magnitude >= 0.2f && dirT != Vector3.zero
          && Vector3.Dot(move, dirT) > 0.1f;
        if (move.magnitude < StuckMoveEps) still += 1.2f; else still = 0f;
        last = p;
        if (PlayerNear(toward, arrive + 0.5f)) arrivedStill += 1.2f; else arrivedStill = 0f;
        if (still >= StuckSeconds) {
          Log("STUCK_ERROR " + label + " pos=" + Pos() + " no movement for " + still.ToString("F0") + "s");
          Shot("stuck_" + Sanitize(label));
          yield break;
        }
        if (arrivedStill >= ArrivedNoProgressSeconds) {
          Log("ARRIVED_NO_PROGRESS " + label + " pos=" + Pos() + " target=" + Fmt(toward)
            + " (destination reached, context never changed)");
          Shot("noctx_" + Sanitize(label));
          yield break;
        }
        if (helped) {
          badCycles = 0;
        } else {
          badCycles++;
          if (badCycles >= 2) {
            bool found = false;
            for (int i = 0; i < probes.Length && !found; i++) {
              Vector3 probeBase = _player != null ? _player.transform.position : p;
              Vector3 psp = _cam != null ? _cam.WorldToScreenPoint(probeBase) : Vector3.zero;
              if (psp.z <= 0f) break;
              Vector2 tapPt = new Vector2(psp.x + probes[i].x, psp.y + probes[i].y);
              tapPt.x = Mathf.Clamp(tapPt.x, 40f, Screen.width - 40f);
              tapPt.y = Mathf.Clamp(tapPt.y, 40f, Screen.height - 40f);
              yield return Tap(tapPt);
              yield return new WaitForSeconds(0.9f);
              Vector3 np2 = _player != null ? _player.transform.position : probeBase;
              float gained = Vector3.Dot(np2 - probeBase, (toward - probeBase).normalized);
              if (gained > 0.2f) {
                learned = probes[i];
                found = true;
                Log("PROBE_OK " + label + " offset=" + probes[i] + " gained=" + gained.ToString("F2"));
              }
            }
            if (!found) {
              Log("PROBE_FAIL " + label + " pos=" + Pos() + " (no screen direction moves toward the target)");
              Shot("probe_" + Sanitize(label));
              yield break;
            }
            badCycles = 0;
            t += 0.9f * probes.Length;
            continue; // re-evaluate the condition before the next normal tap
          }
        }
        // Tap selection: learned offset wins; else the projected near-player
        // step point. When the step is beyond a SIDE edge, keep the player's
        // own screen height (the ray then hits ground LATERALLY near the
        // player instead of landing in a neighbour gate's snap zone).
        Vector3 psp0 = _cam != null ? _cam.WorldToScreenPoint(p) : Vector3.zero;
        Vector3 sp;
        if (learned.HasValue && _cam != null && psp0.z > 0f) {
          sp = new Vector3(psp0.x + learned.Value.x, psp0.y + learned.Value.y, 0f);
        } else {
          Vector3 stepW = p + (dirT != Vector3.zero ? dirT : new Vector3(0f, 0f, 1f)) * 2.5f;
          // SNAP-SAFE TAP: never tap within 2.3m of a NON-target gate centre
          // (the thinking walkway passes within the English gate's 2m snap
          // radius — a tap there hijacks the walk into the wrong gate).
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
          sp = _cam != null ? _cam.WorldToScreenPoint(stepW) : Vector3.zero;
          if (sp.z <= 0f) {
            sp = psp0.z > 0f ? new Vector3(psp0.x, psp0.y - 180f * px, 0f)
              : new Vector3(Screen.width * 0.5f, Screen.height * 0.4f, 0f);
          } else if (psp0.z > 0f) {
            if (sp.x < 40f) sp = new Vector3(40f, psp0.y, 0f);
            else if (sp.x > Screen.width - 40f) sp = new Vector3(Screen.width - 40f, psp0.y, 0f);
          }
        }
        sp.x = Mathf.Clamp(sp.x, 40f, Screen.width - 40f);
        sp.y = Mathf.Clamp(sp.y, 40f, Screen.height - 40f);
        Log("walk " + label + " p=" + Pos() + " to=" + Fmt(toward)
          + " sp=(" + sp.x.ToString("F0") + "," + sp.y.ToString("F0") + ")"
          + (learned.HasValue ? " learned=" + learned.Value : ""));
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
    float t = 0f;
    while (t < timeout) {
      bool ok = false;
      try { ok = cond(); } catch (Exception) { }
      if (ok) { _stepOk = true; Log("ok: " + label); yield break; }
      yield return new WaitForSeconds(0.5f);
      t += 0.5f;
    }
    Log("TIMEOUT: " + label);
    Shot("timeout_" + Sanitize(label));
  }

  // Standing is EXPECTED (not an error) while a dialog is open or a yard
  // transition is in flight — the driver waits instead of flagging a stuck.
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

  // ---- state probes ---------------------------------------------------------------

  static SubjectGate FindGate(SubjectId target) {
    SubjectGate[] gates = FindObjectsOfType<SubjectGate>();
    foreach (SubjectGate g in gates) {
      if (g != null && !g.IsReturnGate && g.Target == target) return g;
    }
    return null;
  }

  static SelectionYardBuilder YardSkill(string subject) {
    if (!SceneLoaded(SelectionYardBuilder.SceneName)) return null;
    SelectionYardBuilder[] bs = FindObjectsOfType<SelectionYardBuilder>();
    foreach (SelectionYardBuilder b in bs) {
      if (b != null && !b.IsGameLevel
          && string.Equals(b.SubjectId, subject, StringComparison.OrdinalIgnoreCase)) return b;
    }
    return null;
  }

  static SelectionYardBuilder YardGame(string skill) {
    if (!SceneLoaded(SelectionYardBuilder.SceneName)) return null;
    SelectionYardBuilder[] bs = FindObjectsOfType<SelectionYardBuilder>();
    foreach (SelectionYardBuilder b in bs) {
      if (b != null && b.IsGameLevel
          && string.Equals(b.SkillId, skill, StringComparison.OrdinalIgnoreCase)) return b;
    }
    return null;
  }

  static bool YardLoaded() {
    return SceneLoaded(SelectionYardBuilder.SceneName) && FindObjectOfType<SelectionYardBuilder>() != null;
  }

  static bool SceneLoaded(string name) {
    Scene s = SceneManager.GetSceneByName(name);
    return s.IsValid() && s.isLoaded;
  }

  static int CountBuilders() { return FindObjectsOfType<SelectionYardBuilder>().Length; }
  static int CountAreas() { return FindObjectsOfType<SelectionYardArea>().Length; }

  bool PlayerNear(Vector3 world, float radius) {
    if (_player == null) return false;
    Vector3 p = _player.transform.position;
    float dx = p.x - world.x, dz = p.z - world.z;
    return dx * dx + dz * dz <= radius * radius;
  }

  string Pos() {
    if (_player == null) return "player(null)";
    Vector3 p = _player.transform.position;
    return "(" + p.x.ToString("F1") + "," + p.z.ToString("F1") + ")";
  }

  static string Fmt(Vector3 v) {
    return "(" + v.x.ToString("F1") + "," + v.z.ToString("F1") + ")";
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

  // ---- click / input (same harness as the committed journey drivers) --------------

  void RefreshRefs() {
    try {
      if (_player == null) _player = FindObjectOfType<ClickToMove>();
      _cam = Camera.main;
    } catch (Exception) { }
  }

  bool ClickButtonByName(string name) {
    foreach (Button b in FindObjectsOfType<Button>()) {
      if (b == null || b.name != name) continue;
      Vector2 screen = UiScreenPos(b.GetComponent<RectTransform>());
      if (screen.x < 0f) continue;
      StartCoroutine(Tap(screen));
      Log("ui click: " + name + " at " + screen);
      return true;
    }
    return false;
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

  IEnumerator Tap(Vector2 screen) {
    Mouse mouse = Mouse.current;
    if (mouse == null) yield break;
    _clicks++;
    try { mouse.WarpCursorPosition(screen); } catch (Exception) { }
    yield return null;
    MouseState pressed = new MouseState();
    MouseState released = new MouseState();
    bool ready = false;
    try {
      pressed.position = screen;
      pressed.buttons = (ushort)(1 << (int)MouseButton.Left);
      released.position = screen;
      released.buttons = 0;
      ready = true;
    } catch (Exception e) { Log("tap failed: " + e.Message); }
    if (!ready) yield break;
    for (int i = 0; i < 4; i++) {
      try { InputSystem.QueueStateEvent(mouse, pressed); } catch (Exception) { }
      yield return new WaitForEndOfFrame();
    }
    try { InputSystem.QueueStateEvent(mouse, released); } catch (Exception) { }
  }
}
