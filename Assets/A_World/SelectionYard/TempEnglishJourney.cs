// TempEnglishJourney.cs — FOREGROUND ENGLISH PATH JOURNEY (WP-1.9.33 QA).
// Committed with the phase (standing order: journey drivers ride the build).
// Inert in production: boots ONLY with `-journeyen` (wav-seam mode) or
// `-journeyen-nomic` (no-mic UX mode). Real InputSystem mouse injection,
// closed-loop walks, real-state polling, screenshots at every beat.
// AUTO-CLOSES always (normal end, cancel, watchdog, or a caught exception).
//
// Path: hub -> TIENG ANH gate -> B (4 skills) -> Listening C -> "Vuon Tai Thinh"
//       -> 4 real rounds (incl. a wrong tap + the bell) -> exit -> back ->
//       Speaking C -> "San Khau Nhi" -> full 8-round visit (wav mode: correct /
//       wrong-word / silence files) or the no-mic UX path -> back -> hub.
// Optional: `-shot-dir <path>`.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TempEnglishJourneyBoot {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() {
    string[] args = Environment.GetCommandLineArgs();
    bool want = false;
    foreach (string a in args) {
      if (string.Equals(a, "-journeyen", StringComparison.OrdinalIgnoreCase)
          || string.Equals(a, "-journeyen-nomic", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
    }
    if (!want) return;
    GameObject go = new GameObject("TempEnglishJourney");
    GameObject.DontDestroyOnLoad(go);
    go.AddComponent<TempEnglishJourney>();
  }
}

public class TempEnglishJourney : MonoBehaviour {
  const string DefaultShotDir = "D:/Vscode/enj-shots";
  const float StuckMoveEps = 0.35f;
  const float StuckSeconds = 16f;
  const float WatchdogSeconds = 900f;

  static readonly string[] EnglishSkillIds = {
    "english_listening", "english_speaking", "english_vocabulary", "english_sentences"
  };

  // Wav-seam plan: file order matches ScriptedWavCapture's sorted *.wav list.
  enum Expected { Pass, Wrongish, NoSpeech }
  struct PlanEntry { public string target; public int attempt; public Expected expect;
    public PlanEntry(string t, int a, Expected e) { target = t; attempt = a; expect = e; } }
  static readonly PlanEntry[] Plan = {
    new PlanEntry("apple", 1, Expected.Pass),
    new PlanEntry("ball", 1, Expected.Wrongish),
    new PlanEntry("ball", 2, Expected.NoSpeech),
    new PlanEntry("ball", 3, Expected.Pass),
    new PlanEntry("banana", 1, Expected.Pass),
    new PlanEntry("orange", 1, Expected.Pass),
    new PlanEntry("carrot", 1, Expected.Pass),
    new PlanEntry("tomato", 1, Expected.Pass),
    new PlanEntry("watermelon", 1, Expected.Pass),
    new PlanEntry("grape", 1, Expected.Pass),
  };

  string _shotDir = DefaultShotDir;
  bool _nomic;
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
  // Stall discipline (user order 2026-10-13): standing >10 s must be CLASSIFIED:
  // ACTIVE (dialog/transition/screenshot/guidance/game progress) = keep waiting,
  // or ERROR (nothing progresses) = screenshot + analysis + skip the segment.
  string _logPath;
  string _currentStep = "-";
  float _lastActivity;
  float _lastShotT = -999f;
  float _lastSceneChangeT = -999f;
  bool _stalled;
  string _stallInfo = "";
  string _activeReason;
  float _activeReasonSince;
  readonly List<string> _recent = new List<string>();

  void Start() {
    _started = Time.realtimeSinceStartup;
    _lastActivity = _started;
    try {
      string[] args = Environment.GetCommandLineArgs();
      for (int i = 0; i + 1 < args.Length; i++) {
        if (string.Equals(args[i], "-shot-dir", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(args[i + 1])) _shotDir = args[i + 1];
        if (string.Equals(args[i], "-journeyen-nomic", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(args[i], "-shot-dir", StringComparison.OrdinalIgnoreCase)) _nomic = true;
      }
      foreach (string a in args)
        if (string.Equals(a, "-journeyen-nomic", StringComparison.OrdinalIgnoreCase)) _nomic = true;
    } catch (Exception) { }
    try { System.IO.Directory.CreateDirectory(_shotDir); } catch (Exception) { }
    _logPath = _shotDir + "/journey.log";
    try {
      System.IO.File.WriteAllText(_logPath, "[ENJ] JOURNEY_LOG_OPEN t=0 mode="
        + (_nomic ? "nomic" : "wav") + "\n");
    } catch (Exception) { }
    try {
      UnityEngine.SceneManagement.SceneManager.sceneLoaded += delegate { _lastSceneChangeT = Time.realtimeSinceStartup; };
    } catch (Exception) { }
    try { InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; }
    catch (Exception) { }
    StartCoroutine(Watchdog());
    StartCoroutine(Heartbeat());
    StartCoroutine(StallMonitor());
    StartCoroutine(Main());
  }

  void Log(string m) { Write("[ENJ] " + m, true); }

  // Repetitive retry chatter: streamed to terminal/log but NOT counted as
  // progress activity (a tap that never lands must not mask a stall).
  void LogQuiet(string m) { Write("[ENJ] " + m, false); }

  void Write(string line, bool activity) {
    try { Debug.Log(line); } catch (Exception) { }
    try {
      _recent.Add(line);
      if (_recent.Count > 40) _recent.RemoveAt(0);
    } catch (Exception) { }
    try { System.IO.File.AppendAllText(_logPath, line + "\n"); } catch (Exception) { }
    if (activity) _lastActivity = Time.realtimeSinceStartup;
  }

  // Terminal view: a live heartbeat so the console log visibly moves.
  IEnumerator Heartbeat() {
    var wait = new WaitForSeconds(3f);
    while (true) {
      yield return wait;
      string pos = "?";
      try { if (_player == null) _player = FindObjectOfType<ClickToMove>(); } catch (Exception) { }
      try {
        if (_player != null) {
          Vector3 p = _player.transform.position;
          pos = "(" + p.x.ToString("F0") + "," + p.z.ToString("F0") + ")";
        }
      } catch (Exception) { }
      float idle = Time.realtimeSinceStartup - _lastActivity;
      Write("[ENJ] HB t=" + (Time.realtimeSinceStartup - _started).ToString("F0")
        + " step=" + _currentStep + " pos=" + pos + " idle=" + idle.ToString("F1") + "s", false);
    }
  }

  // ---- stall discipline -------------------------------------------------------

  IEnumerator StallMonitor() {
    float delay = 0.5f;
    string snapKey = "";
    Transform guide = null;
    Vector3 lastGuide = Vector3.zero;
    while (true) {
      yield return new WaitForSeconds(delay);
      float now = Time.realtimeSinceStartup;
      // Game snapshot changes (rounds, phases, attempts, bell, yards) = progress.
      string key = SnapshotKey();
      if (key != snapKey) { snapKey = key; _lastActivity = now; }
      // The parallel guidance feature moves its teacher NPC; count as progress.
      try {
        if (guide == null) {
          GameObject g = GameObject.Find("LGTeacherGuide");
          if (g != null) guide = g.transform;
        }
        if (guide != null) {
          Vector3 gp = guide.position;
          if ((gp - lastGuide).sqrMagnitude > 0.0004f) _lastActivity = now;
          lastGuide = gp;
        }
      } catch (Exception) { }
      float idle = now - _lastActivity;
      if (idle >= 10f) {
        string activeReason = ActiveIdleReason(now);
        if (activeReason != null) {
          if (_activeReason != activeReason) {
            _activeReason = activeReason;
            _activeReasonSince = now;
            Log("IDLE_ACTIVE (" + activeReason + ") step=" + _currentStep
              + " idle=" + idle.ToString("F1") + "s - keep waiting");
          } else if (now - _activeReasonSince > 45f) {
            // A "context" idle that never ends is itself a bug (a stuck dialog
            // or a hung transition): escalate to the stall path.
            activeReason = "escalated " + activeReason;
          } else {
            if (((int)(now - _activeReasonSince)) % 30 < 1)
              Log("IDLE_ACTIVE (" + activeReason + ") still, step=" + _currentStep
                + " for=" + (now - _activeReasonSince).ToString("F0") + "s");
            _lastActivity = now;
          }
        }
        if (activeReason == null || activeReason.StartsWith("escalated")) {
          _activeReason = null;
          _stalled = true;
          _stallInfo = (activeReason == null ? "" : "[" + activeReason + "] ") + DescribeStall(idle);
          _errors++;
          Log("STALL_ERROR step=" + _currentStep + " idle=" + idle.ToString("F1") + "s " + _stallInfo);
          Log("STALL_ANALYSIS " + _stallInfo);
          Log("STALL_RECENT\n  " + string.Join("\n  ", _recent.GetRange(
            Mathf.Max(0, _recent.Count - 12), Mathf.Min(12, _recent.Count)).ToArray()));
          Shot("stall_" + Sanitize(_currentStep));
          Log("STALL_SKIP step=" + _currentStep + " (segment aborted; the journey continues)");
          _lastActivity = now; // cooldown window for the next classification
        }
      }
    }
  }

  string ActiveIdleReason(float now) {
    try {
      if (IsLanguageOpen()) return "language dialog";
      MicSetupDialog mic = FindObjectOfType<MicSetupDialog>();
      if (mic != null && mic.IsShowing) return "mic dialog";
      SelectionYardArea area = FindObjectOfType<SelectionYardArea>();
      if (area != null && area.IsBusy) return "yard transition";
      if (now - _lastShotT < 4f) return "screenshot in progress";
      if (now - _lastSceneChangeT < 5f) return "scene just changed";
    } catch (Exception) { }
    return null;
  }

  string SnapshotKey() {
    try {
      var sb = new System.Text.StringBuilder(96);
      ListeningGarden lg = FindObjectOfType<ListeningGarden>();
      if (lg != null) {
        sb.Append("L").Append((int)lg.Current).Append(':').Append(lg.Level).Append(':')
          .Append(lg.RoundIndex).Append(':').Append(lg.WrongCount).Append(':').Append(lg.Completed ? 1 : 0);
        ListeningGardenBuilder lb = FindObjectOfType<ListeningGardenBuilder>();
        if (lb != null && lb.Bell != null) sb.Append(':').Append(lb.Bell.CurrentY.ToString("F2"));
      }
      SpeakingStage ss = FindObjectOfType<SpeakingStage>();
      if (ss != null) {
        sb.Append("S").Append((int)ss.Current).Append(':').Append(ss.Level).Append(':')
          .Append(ss.RoundIndex).Append(':').Append(ss.AttemptsUsed).Append(':')
          .Append(ss.EnvErrors).Append(':').Append(ss.Completed ? 1 : 0);
      }
      SelectionYardBuilder yb = FindObjectOfType<SelectionYardBuilder>();
      if (yb != null) sb.Append("Y").Append(yb.Level).Append(':').Append(yb.SkillId).Append(':').Append(yb.SubjectId);
      return sb.ToString();
    } catch (Exception) { return "snap-error"; }
  }

  string DescribeStall(float idle) {
    var sb = new System.Text.StringBuilder(160);
    sb.Append("idle=").Append(idle.ToString("F1")).Append("s player=").Append(Pos());
    try {
      SelectionYardBuilder yb = FindObjectOfType<SelectionYardBuilder>();
      if (yb != null) sb.Append(" yard=").Append(yb.Level).Append('/').Append(yb.SubjectId)
        .Append('/').Append(yb.SkillId).Append(" doors=").Append(yb.GatePortals.Count);
      ListeningGarden lg = FindObjectOfType<ListeningGarden>();
      if (lg != null) sb.Append(" garden phase=").Append(lg.Current).Append(" lvl=").Append(lg.Level)
        .Append(" round=").Append(lg.RoundIndex).Append(" target=").Append(lg.TargetWord)
        .Append(" wrong=").Append(lg.WrongCount);
      SpeakingStage ss = FindObjectOfType<SpeakingStage>();
      if (ss != null) sb.Append(" stage phase=").Append(ss.Current).Append(" lvl=").Append(ss.Level)
        .Append(" round=").Append(ss.RoundIndex).Append(" target=").Append(ss.TargetWord)
        .Append(" attempt=").Append(ss.AttemptsUsed).Append(" env=").Append(ss.EnvErrors)
        .Append(" hasLevel=").Append(ss.HasLastLevel ? ss.LastLevel.ToString() : "none")
        .Append(" pct=").Append(ss.LastPercent);
      SelectionYardArea area = FindObjectOfType<SelectionYardArea>();
      if (area != null) sb.Append(" area=").Append(area.CurrentLevel).Append(" busy=").Append(area.IsBusy)
        .Append(" inPlay=").Append(area.IsInPlay).Append(" game=").Append(area.CurrentGameId);
      sb.Append(" scenes=");
      for (int i = 0; i < SceneManager.sceneCount; i++) {
        Scene s = SceneManager.GetSceneAt(i);
        if (s.isLoaded) sb.Append(s.name).Append(',');
      }
    } catch (Exception e) { sb.Append(" dump-error=").Append(e.Message); }
    return sb.ToString();
  }

  IEnumerator Watchdog() {
    yield return new WaitForSeconds(WatchdogSeconds);
    Log("WATCHDOG_TIMEOUT elapsed=" + (Time.realtimeSinceStartup - _started).ToString("F0") + "s");
    Quit(2);
  }

  void Quit(int code) {
    try { Debug.Log("[ENJ] ENJ_QUIT code=" + code); } catch (Exception) { }
    try { Application.Quit(code); } catch (Exception) { }
  }

  void DismissSystemDialogs(bool hideCameraHud) {
    try {
      if (hideCameraHud) {
        PhoneCameraHud camHud = FindObjectOfType<PhoneCameraHud>();
        if (camHud != null && camHud.IsShowing) camHud.SetRecordingHide(true);
        MicStatusHud micHud = FindObjectOfType<MicStatusHud>();
        if (micHud != null && micHud.enabled) { micHud.enabled = false; micHud.gameObject.SetActive(false); }
      }
    } catch (Exception) { }
  }

  // Raw screenshot (no dialog fiddling) — used once for the boot evidence.
  void RawShot(string label) {
    try {
      string path = _shotDir + "/" + _shots.ToString("00") + "_" + label + ".png";
      ScreenCapture.CaptureScreenshot(path);
      Log("SHOT " + _shots.ToString("00") + " " + label);
      _shots++;
    } catch (Exception e) { Log("shot failed: " + e.Message); }
  }

  void Shot(string label) {
    try {
      DismissSystemDialogs(true);
      _lastShotT = Time.realtimeSinceStartup;
      RawShot(label);
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

  bool Record(string step, bool ok, string detail) {
    _steps++;
    if (_stalled) {
      detail = (string.IsNullOrEmpty(detail) ? "" : detail + " ") + "[stall: " + _stallInfo + "]";
      _stalled = false;
      _stallInfo = "";
    }
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

  bool StepGuard() {
    if (!_cancel) return true;
    Log("CANCEL_ACTIVE -> stopping the journey now");
    return false;
  }

  IEnumerator Main() {
    yield return Guard(RunNested(MainBody()));
    yield return Finish();
  }

  // Execute ANY nested step enumerator INSIDE this driver's try/catch: a nested
  // exception (e.g. a stale destroyed reference) becomes an honest cancel +
  // autoclose instead of silently killing the coroutine tree (that left the
  // game idle with the journey dead once — fixed 2026-10-13).
  IEnumerator RunNested(IEnumerator inner) {
    while (true) {
      object cur = null;
      bool more = false;
      try {
        more = inner.MoveNext();
        cur = more ? inner.Current : null;
      } catch (Exception e) {
        _errors++;
        _cancel = true;
        Log("NESTED_EXCEPTION " + e.GetType().Name + ": " + e.Message);
        Log(e.StackTrace);
        Shot("nested_exception");
        yield break;
      }
      if (!more) yield break;
      IEnumerator sub = cur as IEnumerator;
      if (sub != null) yield return RunNested(sub);
      else yield return cur;
    }
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
    Log("JOURNEY_START english path (mode=" + (_nomic ? "nomic" : "wav") + ")");
    yield return new WaitForSeconds(7f);
    RawShot("00_boot_raw"); // evidence BEFORE any dialog is dismissed
    DismissSystemDialogs(true);

    // Mic offer first (no local mic), then the language chooser.
    yield return WaitStep(delegate {
      MicSetupDialog mic = FindObjectOfType<MicSetupDialog>();
      LanguageDialog lang = FindObjectOfType<LanguageDialog>();
      return (mic != null && mic.IsShowing) || (lang != null && lang.IsOpen);
    }, 30f, "first system dialog");
    MicSetupDialog mic0 = FindObjectOfType<MicSetupDialog>();
    if (mic0 != null && mic0.IsShowing) {
      bool declined = ClickTextButton("Bỏ qua");
      yield return WaitStep(delegate {
        MicSetupDialog m = FindObjectOfType<MicSetupDialog>();
        return m == null || !m.IsShowing;
      }, 20f, "mic offer declined");
      Record("boot: mic offer declined", _stepOk && declined, "");
      if (!StepGuard()) yield break;
    }
    yield return WaitStep(delegate {
      LanguageDialog d = FindObjectOfType<LanguageDialog>();
      return d != null && d.IsOpen;
    }, 30f, "language dialog");
    bool viClicked = ClickButtonByName("ViBox");
    yield return WaitStep(delegate {
      LanguageDialog d = FindObjectOfType<LanguageDialog>();
      return d == null || !d.IsOpen;
    }, 20f, "language chosen (vi)");
    Record("boot + language=vi", _stepOk && viClicked, "");
    if (!StepGuard()) yield break;
    Shot("01_hub");

    // ---- A -> B (TIENG ANH) -------------------------------------------------------
    yield return WalkIntoSubjectGateStep(SubjectIds.English, "english", "walk into English gate");
    Record("A -> B english skill yard", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2.5f);
    Shot("02_english_skill_yard");
    Record("B content: english 4 skills, listening+speaking live", CheckEnglishSkillYard(), "");
    if (!StepGuard()) yield break;

    // ---- B -> C listening ---------------------------------------------------------
    yield return EnterDoorStep("english_listening", "Listening",
      delegate { return YardGame("english_listening") != null; }, 120f);
    Record("B -> C listening game yard", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2.5f);
    Shot("03_listening_game_yard");
    Record("C listening: 1 game door + mic-free preview", CheckListeningGameYard(), "");
    if (!StepGuard()) yield break;

    // ---- C -> GAME (listen garden) ------------------------------------------------
    yield return EnterDoorStep("listen_garden", "Vuon Tai Thinh",
      delegate { return SceneLoaded(ListeningGardenBuilder.SceneName); }, 150f);
    Record("C -> listening arena loads", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(3.5f);
    Shot("04_listening_arena");
    ListeningGarden garden = FindObjectOfType<ListeningGarden>();
    Record("listening arena content (game + no yard)", garden != null && !YardLoaded(), "");
    if (!StepGuard()) yield break;

    // ROUND 1: answer directly.
    yield return ListeningRoundStep(true, false, "round1");
    Record("listening round 1: answer pod advances", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(1.2f);
    Shot("04a_round1_win");

    // ROUND 2: wrong tap first (expect keep playing + retry), then answer.
    yield return ListeningRoundStep(false, true, "round2");
    Record("listening round 2: wrong tap tolerated, answer advances", _stepOk, "");
    if (!StepGuard()) yield break;
    Shot("04b_round2_win");

    // ROUND 3: bell replay, then answer.
    yield return ListeningBellStep("round3 bell");
    Record("listening bell replays without breaking the round", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return ListeningRoundStep(true, false, "round3");
    Record("listening round 3: answer advances", _stepOk, "");
    if (!StepGuard()) yield break;
    Shot("04c_round3_win");

    yield return ExitArenaStep("english_listening", "listening arena exit");
    Record("GAME -> C back (listening)", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2.5f);
    Shot("05_back_in_listening_yard");
    Record("C re-entry: listening door intact", CheckListeningGameYard(), "");
    if (!StepGuard()) yield break;

    yield return BackStep(delegate { return YardSkill("english") != null; }, 120f, "back to english skill yard");
    Record("C -> B back (english)", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2f);
    Shot("06_back_english_skill_yard");
    Record("B re-entry: 4 skills, 2 live", CheckEnglishSkillYard(), "");
    if (!StepGuard()) yield break;

    yield return BackStep(delegate { return !YardLoaded(); }, 120f, "back to the hub");
    Record("B -> A back (english)", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2f);
    Shot("07_back_hub");
    SelectionYardArea area0 = FindObjectOfType<SelectionYardArea>();
    Record("hub restored", (area0 == null || !area0.IsInside) && PlayerNear(SubjectCatalog.HubCenter, 8f), Pos());
    if (!StepGuard()) yield break;

    // ---- A -> B -> SPEAKING -------------------------------------------------------
    yield return WalkIntoSubjectGateStep(SubjectIds.English, "english", "walk into English gate (2)");
    Record("A -> B english (2)", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2f);
    yield return EnterDoorStep("english_speaking", "Speaking",
      delegate { return YardGame("english_speaking") != null; }, 120f);
    Record("B -> C speaking game yard", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2.5f);
    Shot("08_speaking_game_yard");
    Record("C speaking: 1 game door + mic preview", CheckSpeakingGameYard(), "");
    if (!StepGuard()) yield break;

    yield return EnterDoorStep("speaking_stage", "San Khau Nhi",
      delegate { return SceneLoaded(SpeakingStageBuilder.SceneName); }, 150f);
    Record("C -> speaking arena loads", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(4f);
    Shot("09_speaking_arena");
    SpeakingStage stage = FindObjectOfType<SpeakingStage>();
    Record("speaking arena content (game + no yard)", stage != null && !YardLoaded(), "");
    if (!StepGuard()) yield break;

    if (_nomic) yield return SpeakingNoMicRun(stage);
    else yield return SpeakingWavRun(stage);
    if (!StepGuard()) yield break;

    yield return ExitArenaStep("english_speaking", "speaking arena exit");
    Record("GAME -> C back (speaking)", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2.5f);
    Shot("11_back_in_speaking_yard");
    Record("C re-entry: speaking door intact", CheckSpeakingGameYard(), "");
    if (!StepGuard()) yield break;

    yield return BackStep(delegate { return YardSkill("english") != null; }, 120f, "back to english skill yard (2)");
    Record("C -> B back (speaking)", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2f);
    yield return BackStep(delegate { return !YardLoaded(); }, 120f, "back to the hub (2)");
    Record("B -> A back (2)", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2f);
    Shot("12_final_hub");
    bool finalOk = !SceneLoaded("MathScene") && !SceneLoaded("CountingGardenScene")
      && !SceneLoaded("DiscoveryScene") && !SceneLoaded(SelectionYardBuilder.SceneName);
    Record("final: hub at rest, no legacy scene", finalOk, "");
  }

  // ---- speaking sub-journeys ------------------------------------------------------

  IEnumerator SpeakingWavRun(SpeakingStage stage) {
    _currentStep = "speaking(wav)";
    int planIndex = 0;
    string lastKey = "";
    float t = 0f;
    while (t < 240f) {
      SpeakingStage s = FindObjectOfType<SpeakingStage>();
      if (s == null) break;
      if (s.Completed) break;
      string key = (s.TargetWord ?? "") + "#" + s.AttemptsUsed + "#" + s.EnvErrors;
      if (key != lastKey) _currentStep = "speaking:" + key;
      if (s.EnvErrors > 0 && key != lastKey) {
        lastKey = key;
        Shot("10_speak_env_" + s.EnvErrors);
        Expect("speaking attempt run has no environment errors (wav mode)", false,
          "EnvErrors=" + s.EnvErrors + " target=" + s.TargetWord);
      } else if (key != lastKey && s.AttemptsUsed > 0) {
        lastKey = key;
        SpeakingDecision dec = s.LastAssessment.Decision;
        string label = (s.TargetWord ?? "?") + " attempt " + s.AttemptsUsed;
        if (planIndex < Plan.Length) {
          PlanEntry e = Plan[planIndex];
          bool match = string.Equals(e.target, s.TargetWord, StringComparison.OrdinalIgnoreCase)
            && e.attempt == s.AttemptsUsed;
          bool classOk;
          if (e.expect == Expected.Pass)
            classOk = dec == SpeakingDecision.Pass || dec == SpeakingDecision.StrongPass;
          else if (e.expect == Expected.NoSpeech)
            classOk = dec == SpeakingDecision.NoSpeech || dec == SpeakingDecision.TooWeak;
          else
            classOk = dec == SpeakingDecision.WrongWord || dec == SpeakingDecision.Partial
              || dec == SpeakingDecision.Unclear || dec == SpeakingDecision.PossibleAttempt;
          Expect("speaking plan[" + planIndex + "] " + label + " expect=" + e.expect + " got=" + dec,
            match && classOk, "dec=" + dec + " lvl=" + (s.HasLastLevel ? s.LastLevel.ToString() : "none")
            + " pct=" + s.LastPercent + "% target=" + s.TargetWord);
          planIndex++;
        } else {
          Expect("speaking extra attempt beyond plan", false, "target=" + s.TargetWord
            + " attempt=" + s.AttemptsUsed + " dec=" + dec);
        }
        Shot("10_speak_" + Sanitize(s.TargetWord) + "_a" + s.AttemptsUsed);
      }
      yield return new WaitForSeconds(0.25f);
      t += 0.25f;
    }
    SpeakingStage stage2 = FindObjectOfType<SpeakingStage>();
    bool done = stage2 != null && stage2.Completed;
    Expect("speaking visit completed", done, done ? "" : "timeout dec=" + (stage2 != null && stage2.HasLastLevel ? stage2.LastLevel.ToString() : "?"));
    Expect("speaking plan fully consumed", planIndex == Plan.Length,
      "consumed=" + planIndex + "/" + Plan.Length);
    SelectionYardArea area = FindObjectOfType<SelectionYardArea>();
    Expect("speaking lifecycle completed", area != null && area.SpeakingLifecycle != null
      && area.SpeakingLifecycle.State == ActivityState.Completed, "");
    Shot("10_speak_completed");
    Record("speaking wav visit (plan + completion)", done && planIndex == Plan.Length, "");
  }

  IEnumerator SpeakingNoMicRun(SpeakingStage stage) {
    _currentStep = "speaking(nomic)";
    float t = 0f;
    int lastEnv = -1;
    while (t < 90f) {
      SpeakingStage s = FindObjectOfType<SpeakingStage>();
      if (s == null) break;
      if (s.EnvErrors != lastEnv && s.EnvErrors > 0) {
        lastEnv = s.EnvErrors;
        Shot("10_nomic_env_" + s.EnvErrors);
      }
      if (s.EnvErrors >= 2 && s.Current == SpeakingStage.Phase.Done) break;
      yield return new WaitForSeconds(0.25f);
      t += 0.25f;
    }
    SpeakingStage s2 = FindObjectOfType<SpeakingStage>();
    bool stopped = s2 != null && s2.EnvErrors >= 2 && s2.Current == SpeakingStage.Phase.Done;
    Expect("no-mic: two environment failures reached", s2 != null && s2.EnvErrors >= 2,
      s2 != null ? ("env=" + s2.EnvErrors) : "no stage");
    Expect("no-mic: kind stop, visit NOT completed", stopped && !s2.Completed, "");
    Expect("no-mic: no attempt counted as child failure", s2.AttemptsUsed == 0,
      s2 != null ? ("attempts=" + s2.AttemptsUsed) : "no stage");
    Shot("10_nomic_stopped");
    Record("speaking no-mic UX path (kind stop + retryable)", stopped && s2 != null && !s2.Completed, "");
  }

  // ---- listening helpers -----------------------------------------------------------

  IEnumerator ListeningRoundStep(bool firstAnswer, bool tapWrongFirst, string label) {
    _stepOk = false;
    ListeningGarden g = null;
    yield return WaitStep(delegate {
      g = FindObjectOfType<ListeningGarden>();
      ListeningGardenBuilder b = FindObjectOfType<ListeningGardenBuilder>();
      return g != null && g.Current == ListeningGarden.Phase.Play
        && b != null && b.Choices.Count > 0;
    }, 120f, label + " round ready");
    if (!_stepOk) yield break;
    // The guide/intro beats (parallel feature) run inside Phase.Play: let them
    // settle before interacting — standing here is expected, not stuck.
    yield return new WaitForSeconds(7f);
    ListeningGarden gg0 = FindObjectOfType<ListeningGarden>();
    if (gg0 == null) yield break;
    int round0 = gg0.RoundIndex;
    int level0 = gg0.Level;
    int wrong0 = gg0.WrongCount;
    if (tapWrongFirst) {
      // Wrong pod: accepted when WrongCount rises WITHOUT the round moving on.
      yield return TapPodUntil(false, delegate {
        ListeningGarden gg = FindObjectOfType<ListeningGarden>();
        if (gg == null) return false;
        if (gg.RoundIndex != round0 || gg.Level != level0 || gg.Completed) {
          _tapWhy = "round advanced during the wrong tap (the aim resolved to another pod)";
          return true; // stop the loop; _tapOk stays false -> honest report
        }
        return gg.WrongCount > wrong0;
      }, 50f, label + " wrong tap");
      if (!_tapOk) yield break;
      _stepOk = true;
      Shot(label + "_wrong_tap");
      // Wait for the retry beat to re-arm the round (prompt + word again).
      yield return WaitStep(delegate {
        ListeningGarden gg = FindObjectOfType<ListeningGarden>();
        return gg != null && gg.Current == ListeningGarden.Phase.Play;
      }, 40f, label + " re-armed after retry");
      if (!_stepOk) yield break;
      yield return new WaitForSeconds(6f);
    }
    // Answer: accepted when the round/level advances (or the visit completes).
    yield return TapPodUntil(true, delegate {
      ListeningGarden gg = FindObjectOfType<ListeningGarden>();
      return gg != null && (gg.RoundIndex != round0 || gg.Level != level0 || gg.Completed);
    }, 70f, label + " answer");
  }

  // Aim-and-tap loop over the REAL click path: re-finds the pod every attempt
  // (pods are rebuilt between rounds), verifies the camera ray actually reaches
  // THAT pod (no guide-NPC/other-pod occlusion), approaches when needed, and
  // re-aims after every walk. Alive to the parallel guidance beats.
  bool _tapOk;
  string _tapWhy = "";

  IEnumerator TapPodUntil(bool wantAnswer, Func<bool> stop, float deadline, string label) {
    _tapOk = false;
    _tapWhy = "never tapped";
    _currentStep = "tap:" + label;
    float t = 0f;
    int attemptNo = 0;
    while (t < deadline) {
      if (_stalled) { Log("step aborted by stall: " + label); yield break; }
      bool done = false;
      try { done = stop(); } catch (Exception) { }
      if (done) {
        ListeningGarden gg = FindObjectOfType<ListeningGarden>();
        _tapOk = gg != null && wantAnswer
          ? true
          : (gg != null && gg.WrongCount > 0 && _tapWhy == "never tapped");
        // For the answer case `done` == accepted; for the wrong case the stop
        // callback distinguishes accepted (WrongCount) from "advanced anyway".
        if (!wantAnswer) {
          // rejected above when the round advanced; recompute cleanly:
          // accepted iff the previous _tapWhy marker set by the callback is empty.
        }
        if (string.IsNullOrEmpty(_tapWhy)) _tapOk = true;
        Log((_tapOk ? "tap accepted: " : "tap stop: ") + label + " why=" + _tapWhy);
        yield break;
      }
      ListeningGarden g = FindObjectOfType<ListeningGarden>();
      if (g == null) { _tapWhy = "no garden"; yield break; }
      ListeningGardenChoice pod = FindChoice(wantAnswer);
      Vector3 pp;
      if (pod == null || !TryPos(pod, out pp)) {
        _tapWhy = "pod missing";
        yield return new WaitForSeconds(1f); t += 1f; continue;
      }
      attemptNo++;
      Vector2 sp;
      if (!ProjectPodClick(pod, pp, out sp)) {
        // Out of view / occluded: walk a step toward it, then re-aim.
        yield return TapGroundToward(pp);
        yield return new WaitForSeconds(1.6f); t += 1.6f;
        continue;
      }
      LogQuiet("tap pod " + label + " attempt=" + attemptNo
        + " pod=" + pod.WordId + " sp=(" + sp.x.ToString("F0") + "," + sp.y.ToString("F0") + ")");
      yield return Tap(sp);
      float w = 0f;
      while (w < 8f) {
        bool stopNow = false;
        try { stopNow = stop(); } catch (Exception) { }
        if (stopNow) break;
        yield return new WaitForSeconds(0.4f);
        w += 0.4f; t += 0.4f;
      }
      _tapWhy = "tap #" + attemptNo + " not accepted yet";
    }
    if (!_tapOk) Log("TAP_FAIL " + label + " why=" + _tapWhy + " pos=" + Pos());
  }

  // True when the camera ray through the pod's click point actually hits THAT
  // pod (probes small offsets for collider edge); false = occluded/out of view.
  bool ProjectPodClick(ListeningGardenChoice pod, Vector3 world, out Vector2 sp) {
    sp = Vector2.zero;
    RefreshRefs();
    if (_cam == null) return false;
    Vector3 p = _cam.WorldToScreenPoint(world);
    if (p.z <= 0f) return false;
    Vector2[] pts = {
      new Vector2(p.x, p.y),
      new Vector2(p.x + 50f, p.y), new Vector2(p.x - 50f, p.y),
      new Vector2(p.x, p.y + 50f), new Vector2(p.x, p.y - 50f),
      new Vector2(p.x + 90f, p.y - 60f), new Vector2(p.x - 90f, p.y - 60f),
    };
    for (int i = 0; i < pts.Length; i++) {
      Vector2 q = pts[i];
      if (q.x < 60f || q.x > Screen.width - 60f) continue;
      if (q.y < 60f || q.y > Screen.height - 120f) continue;
      RaycastHit hit;
      if (!Physics.Raycast(_cam.ScreenPointToRay(new Vector3(q.x, q.y, 0f)), out hit, 80f)) continue;
      ListeningGardenChoice hitPod = hit.collider.GetComponentInParent<ListeningGardenChoice>();
      if (hitPod == pod) { sp = q; return true; }
    }
    return false;
  }

  IEnumerator TapGroundToward(Vector3 world) {
    RefreshRefs();
    Vector3 p = _player != null ? _player.transform.position : Vector3.zero;
    Vector3 dir = world - p;
    dir.y = 0f;
    if (dir.sqrMagnitude < 0.01f) dir = new Vector3(0f, 0f, 1f);
    dir.Normalize();
    Vector3 step = p + dir * 1.8f;
    Vector3 sp = _cam != null ? _cam.WorldToScreenPoint(step) : Vector3.zero;
    if (sp.z <= 0f) sp = _cam != null ? _cam.WorldToScreenPoint(p + dir * 0.8f) : Vector3.zero;
    if (sp.z <= 0f) yield break;
    sp.x = Mathf.Clamp(sp.x, 60f, Screen.width - 60f);
    sp.y = Mathf.Clamp(sp.y, 140f, Screen.height - 60f);
    LogQuiet("approach toward pod " + Fmt(world) + " step=" + Fmt(step));
    yield return Tap(new Vector2(sp.x, sp.y));
  }

  static bool TryPos(MonoBehaviour mb, out Vector3 p) {
    p = Vector3.zero;
    try {
      if (mb == null) return false;
      p = mb.transform.position;
      return true;
    } catch (Exception) { return false; }
  }

  IEnumerator ListeningBellStep(string label) {
    _stepOk = false;
    _currentStep = "bell:" + label;
    ListeningGardenBuilder b = FindObjectOfType<ListeningGardenBuilder>();
    if (b == null || b.Bell == null) {
      Log("no bell for " + label);
      _stepOk = false;
      yield break;
    }
    try {
      Log("bell state before tap: y=" + b.Bell.CurrentY.ToString("F2")
        + " active=" + b.Bell.gameObject.activeInHierarchy);
    } catch (Exception) { }
    Vector3 bp;
    if (!TryPos(b.Bell, out bp)) {
      Log("bell pos unavailable");
      yield break;
    }
    yield return TapWorld(bp + new Vector3(0f, 0.9f, 0f));
    yield return new WaitForSeconds(2.5f);
    ListeningGarden g = FindObjectOfType<ListeningGarden>();
    _stepOk = g != null && g.Current == ListeningGarden.Phase.Play;
    try {
      Log("bell step: still playing=" + _stepOk + " bellY=" + b.Bell.CurrentY.ToString("F2"));
    } catch (Exception) { }
  }

  static ListeningGardenChoice FindChoice(bool answer) {
    ListeningGardenBuilder b = FindObjectOfType<ListeningGardenBuilder>();
    if (b == null) return null;
    for (int i = 0; i < b.Choices.Count; i++) {
      ListeningGardenChoice c = b.Choices[i];
      if (c != null && c.IsAnswer == answer) return c;
    }
    return null;
  }

  IEnumerator TapWorld(Vector3 world) {
    RefreshRefs();
    Vector3 sp = _cam != null ? _cam.WorldToScreenPoint(world) : Vector3.zero;
    if (sp.z <= 0f) {
      Vector3 p = _player != null ? _player.transform.position : Vector3.zero;
      sp = _cam != null ? _cam.WorldToScreenPoint(p + (world - p).normalized * 2f) : Vector3.zero;
    }
    if (sp.z <= 0f) sp = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
    sp.x = Mathf.Clamp(sp.x, 40f, Screen.width - 40f);
    sp.y = Mathf.Clamp(sp.y, 40f, Screen.height - 40f);
    Log("tap world " + Fmt(world) + " sp=(" + sp.x.ToString("F0") + "," + sp.y.ToString("F0") + ")");
    yield return Tap(new Vector2(sp.x, sp.y));
  }

  // ---- content checks -------------------------------------------------------------

  bool CheckEnglishSkillYard() {
    SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
    if (b == null) { Expect("english B exists", false, "no builder"); return false; }
    bool ok = true;
    ok &= Expect("english B: level = skill", !b.IsGameLevel, b.Level);
    ok &= Expect("english B: 4 skill doors", b.GatePortals.Count == 4,
      "count=" + b.GatePortals.Count);
    for (int i = 0; i < EnglishSkillIds.Length && i < b.GateTargetIds.Count; i++)
      ok &= Expect("english B door " + i + " = " + EnglishSkillIds[i],
        b.GateTargetIds[i] == EnglishSkillIds[i], b.GateTargetIds[i]);
    int hints = CountHints(b.transform);
    ok &= Expect("english B: listening+speaking live glow", hints == 2, "hints=" + hints);
    ok &= Expect("english B: no placeholder", b.PlaceholderBoard == null, "");
    ok &= Expect("english B: no title board", FindDeep(b.transform, "SYTitleLabel") == null, "");
    ok &= Expect("english B: back gate", b.BackGate != null, "");
    return ok;
  }

  bool CheckListeningGameYard() {
    SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
    if (b == null) { Expect("listening C exists", false, "no builder"); return false; }
    bool ok = true;
    ok &= Expect("listening C: level = game", b.IsGameLevel, b.Level);
    ok &= Expect("listening C: 1 door", b.GatePortals.Count == 1, "count=" + b.GatePortals.Count);
    if (b.GateTargetIds.Count >= 1)
      ok &= Expect("listening C door = listen_garden", b.GateTargetIds[0] == "listen_garden",
        b.GateTargetIds[0]);
    if (b.GatePortals.Count >= 1)
      ok &= Expect("listening C door kind Play", b.GatePortals[0].Kind == SelectionGate.GateKind.Play,
        b.GatePortals[0].Kind.ToString());
    ok &= Expect("listening C: preview staged", FindDeep(b.transform, "SYPreview_listen_garden") != null, "");
    ok &= Expect("listening C: no placeholder", b.PlaceholderBoard == null, "");
    return ok;
  }

  bool CheckSpeakingGameYard() {
    SelectionYardBuilder b = FindObjectOfType<SelectionYardBuilder>();
    if (b == null) { Expect("speaking C exists", false, "no builder"); return false; }
    bool ok = true;
    ok &= Expect("speaking C: level = game", b.IsGameLevel, b.Level);
    ok &= Expect("speaking C: 1 door", b.GatePortals.Count == 1, "count=" + b.GatePortals.Count);
    if (b.GateTargetIds.Count >= 1)
      ok &= Expect("speaking C door = speaking_stage", b.GateTargetIds[0] == "speaking_stage",
        b.GateTargetIds[0]);
    ok &= Expect("speaking C: preview staged", FindDeep(b.transform, "SYPreview_speaking_stage") != null, "");
    ok &= Expect("speaking C: no placeholder", b.PlaceholderBoard == null, "");
    return ok;
  }

  static int CountHints(Transform t) {
    int n = 0;
    MicroGateHint[] hints = t.GetComponentsInChildren<MicroGateHint>(true);
    for (int i = 0; i < hints.Length; i++) if (hints[i] != null) n++;
    return n;
  }

  // ---- steps (same discipline as the committed S13 driver) ------------------------

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
      Log("BACK_STEP_FAIL: " + label + " (no builder / back gate present)");
      Shot("fail_" + Sanitize(label));
      yield break;
    }
    Vector3 bp = b.BackGate.transform.position;
    Vector3 staging = new Vector3(bp.x, 0f, bp.z + 3.2f);
    yield return WalkStep(delegate { return PlayerNear(staging, 2.4f); }, staging, 2.4f, 60f,
      label + " staging", null, null);
    if (!_stepOk) yield break;
    yield return WalkStep(done, bp, 1.2f, timeout, label + " walk-in", null, null);
  }

  IEnumerator WalkStep(Func<bool> cond, Vector3 toward, float arrive, float timeout, string label,
      Func<bool> wrong, string allowGateId) {
    _stepOk = false;
    _currentStep = "walk:" + label;
    RefreshRefs();
    float t = 0f;
    float still = 0f;
    float arrivedStill = 0f;
    Vector3 last = _player != null ? _player.transform.position : Vector3.zero;
    Vector2? learned = null;
    int badCycles = 0;
    float px = Mathf.Max(1f, Screen.height / 720f);
    Vector2[] probes = {
      new Vector2(260f * px, 0f), new Vector2(-260f * px, 0f),
      new Vector2(0f, 190f * px), new Vector2(0f, -190f * px),
      new Vector2(260f * px, 190f * px), new Vector2(260f * px, -190f * px),
      new Vector2(-260f * px, 190f * px), new Vector2(-260f * px, -190f * px),
      new Vector2(0f, -330f * px),
    };
    while (t < timeout) {
      if (_stalled) { Log("step aborted by stall: " + label); yield break; }
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
        if (IsLanguageOpen()) ClickButtonByName("ViBox");
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
        if (arrivedStill >= 12f) {
          Log("ARRIVED_NO_PROGRESS " + label + " pos=" + Pos() + " target=" + Fmt(toward));
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
              if (gained > 0.2f) { learned = probes[i]; found = true;
                Log("PROBE_OK " + label + " offset=" + probes[i]); }
            }
            if (!found) {
              Log("PROBE_FAIL " + label + " pos=" + Pos());
              Shot("probe_" + Sanitize(label));
              yield break;
            }
            badCycles = 0;
            t += 0.9f * probes.Length;
            continue;
          }
        }
        Vector3 psp0 = _cam != null ? _cam.WorldToScreenPoint(p) : Vector3.zero;
        Vector3 sp;
        if (learned.HasValue && _cam != null && psp0.z > 0f) {
          sp = new Vector3(psp0.x + learned.Value.x, psp0.y + learned.Value.y, 0f);
        } else {
          Vector3 stepW = p + (dirT != Vector3.zero ? dirT : new Vector3(0f, 0f, 1f)) * 2.5f;
          foreach (SubjectDefinition def in SubjectCatalog.All) {
            if (def == null) continue;
            if (!string.IsNullOrEmpty(allowGateId)
                && string.Equals(def.Id.Value, allowGateId, StringComparison.OrdinalIgnoreCase)) continue;
            float dx = stepW.x - def.GatePos.x, dz = stepW.z - def.GatePos.z;
            if (dx * dx + dz * dz < 2.3f * 2.3f) {
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
        LogQuiet("walk " + label + " p=" + Pos() + " to=" + Fmt(toward)
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
    _currentStep = "wait:" + label;
    float t = 0f;
    while (t < timeout) {
      if (_stalled) { Log("step aborted by stall: " + label); yield break; }
      bool ok = false;
      try { ok = cond(); } catch (Exception) { }
      if (ok) { _stepOk = true; Log("ok: " + label); yield break; }
      yield return new WaitForSeconds(0.5f);
      t += 0.5f;
    }
    Log("TIMEOUT: " + label);
    Shot("timeout_" + Sanitize(label));
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

  // ---- click / input ---------------------------------------------------------------

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

  // Click the button whose child label text matches (mic dialog buttons are all
  // named "Button" with a "Label" child).
  bool ClickTextButton(string text) {
    foreach (Button b in FindObjectsOfType<Button>()) {
      if (b == null) continue;
      Text[] texts = b.GetComponentsInChildren<Text>(true);
      bool hit = false;
      for (int i = 0; i < texts.Length; i++) {
        if (texts[i] != null && texts[i].text != null
            && texts[i].text.Contains(text)) { hit = true; break; }
      }
      if (!hit) continue;
      Vector2 screen = UiScreenPos(b.GetComponent<RectTransform>());
      if (screen.x < 0f) continue;
      StartCoroutine(Tap(screen));
      Log("ui click (text '" + text + "') at " + screen);
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
