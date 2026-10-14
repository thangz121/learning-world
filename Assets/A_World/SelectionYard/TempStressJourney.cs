// TempStressJourney.cs — FULL QUESTION-BANK STRESS JOURNEY (tooling, committed with the build).
// Boots ONLY with `-stress-stage <name>` / `-stress-all` / `-stress-from <name>`.
// Stages: geometry | market | city | rabbit | stairs | listening | speaking.
// Real OS input only (UxOsInput SendInput): every question is walked to and clicked.
// Evidence: per-question screenshots + .txt sidecars, [STRESS] log trace, and a
// stress_summary.txt in the shot dir; STRESS_END reports errors/severe/cancel and
// the per-arena question/level coverage. Zero-error gate: errors=0 severe=0 cancel=False.
// C# 9.0 only.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TempStressJourneyBoot {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() {
    string[] args = Environment.GetCommandLineArgs();
    bool want = false;
    for (int i = 0; i < args.Length; i++) {
      if (string.Equals(args[i], "-stress-stage", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
      if (string.Equals(args[i], "-stress-all", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
      if (string.Equals(args[i], "-stress-from", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
    }
    if (!want) return;
    GameObject go = new GameObject("TempStressJourney");
    UnityEngine.Object.DontDestroyOnLoad(go);
    go.AddComponent<TempStressJourney>();
  }
}

public class TempStressJourney : MonoBehaviour {
  const float StuckMoveEps = 0.35f;
  const float StuckSeconds = 16f;
  const float ArrivedNoProgressSeconds = 14f;
  const float IdleSeconds = 12f;
  const float WatchdogSeconds = 21600f; // 6h hard stop

  static readonly string[] AllStages = {
    "geometry", "market", "city", "rabbit", "listening", "speaking"
  };

  string _shotDir = "D:/Vscode/stress-shots";
  string _stageArg = "";       // -stress-stage <name> ("" = chain/all)
  bool _allMode;
  string _startStage = "";     // -stress-from <name>
  int _runNumber = 1;

  int _shots, _clicks, _steps, _errors, _severeCount, _idleCount, _questions;
  int _failStreak;
  bool _cancel;
  bool _stepOk;
  bool _stageSevere;
  bool _reloadNeeded;
  string _severeReason = "";
  bool _intent, _shotPending, _relaunching;
  string _stageName = "";
  Camera _cam;
  ClickToMove _player;
  float _started;

  readonly Dictionary<string, int> _qByArena = new Dictionary<string, int>();
  readonly Dictionary<string, string> _coverage = new Dictionary<string, string>();
  readonly List<string> _summary = new List<string>();

  void Start() {
    _started = Time.realtimeSinceStartup;
    try {
      string[] args = Environment.GetCommandLineArgs();
      for (int i = 0; i + 1 < args.Length; i++) {
        if (string.Equals(args[i], "-shot-dir", StringComparison.OrdinalIgnoreCase)) _shotDir = args[i + 1];
        else if (string.Equals(args[i], "-stress-stage", StringComparison.OrdinalIgnoreCase)) _stageArg = args[i + 1];
        else if (string.Equals(args[i], "-stress-from", StringComparison.OrdinalIgnoreCase)) _startStage = args[i + 1];
        else if (string.Equals(args[i], "-stress-run", StringComparison.OrdinalIgnoreCase)) {
          int n; if (int.TryParse(args[i + 1], out n) && n > 0) _runNumber = n;
        }
      }
    } catch (Exception) { }
    _allMode = string.IsNullOrEmpty(_stageArg);
    try { System.IO.Directory.CreateDirectory(_shotDir); } catch (Exception) { }
    try { Application.runInBackground = true; } catch (Exception) { }
    try { ClickRouter.Diag = true; } catch (Exception) { }
    StartCoroutine(Watchdog());
    StartCoroutine(IdleMonitor());
    StartCoroutine(Main());
  }

  void Log(string m) { try { Debug.Log("[STRESS] " + m); } catch (Exception) { } }

  IEnumerator Watchdog() {
    yield return new WaitForSeconds(WatchdogSeconds);
    Log("WATCHDOG_TIMEOUT elapsed=" + (Time.realtimeSinceStartup - _started).ToString("F0") + "s");
    Quit(2);
  }

  void Quit(int code) {
    Log("STRESS_QUIT code=" + code);
    try { Application.Quit(code); } catch (Exception) { }
  }

  // ---- evidence ---------------------------------------------------------------

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

  string Census() {
    StringBuilder sb = new StringBuilder();
    sb.Append("scene=[").Append(LoadedScenes()).Append("] player=").Append(Pos());
    try {
      GeometryPlay g = FindObjectOfType<GeometryPlay>();
      if (g != null) sb.Append(" geo=").Append(g.Current).Append("/L").Append(g.Level).Append("/R").Append(g.RoundIndex);
      ComparisonMarket m = FindObjectOfType<ComparisonMarket>();
      if (m != null) sb.Append(" cmp=").Append(m.Current).Append("/L").Append(m.Level).Append("/R").Append(m.RoundIndex);
      ClassificationCity c = FindObjectOfType<ClassificationCity>();
      if (c != null) sb.Append(" cls=").Append(c.Current).Append("/L").Append(c.Level).Append("/R").Append(c.RoundIndex);
      RabbitFeed r = FindObjectOfType<RabbitFeed>();
      if (r != null) sb.Append(" rab=").Append(r.Current).Append(" target=").Append(r.Target)
        .Append(" kind=").Append(r.Kind).Append(" count=").Append(r.Count).Append(" subs=").Append(r.Submits);
      NumberStairs st = FindObjectOfType<NumberStairs>();
      if (st != null) sb.Append(" stair=").Append(st.Current).Append(" target=").Append(st.Target)
        .Append(" step=").Append(st.CurrentStep);
      ListeningGarden lg = FindObjectOfType<ListeningGarden>();
      if (lg != null) sb.Append(" lg=").Append(lg.Current).Append("/L").Append(lg.Level).Append("/R").Append(lg.RoundIndex);
      SpeakingStage ss = FindObjectOfType<SpeakingStage>();
      if (ss != null) sb.Append(" ss=").Append(ss.Current).Append("/L").Append(ss.Level).Append("/R").Append(ss.RoundIndex)
        .Append(" target=").Append(ss.TargetWord).Append(" att=").Append(ss.AttemptsUsed).Append(" env=").Append(ss.EnvErrors);
    } catch (Exception) { }
    return sb.ToString();
  }

  void Shot(string label) {
    try {
      DismissSystemDialogs();
      _intent = true;
      _shotPending = true;
      string tag = _shots.ToString("000") + "_" + Sanitize(label);
      string path = _shotDir + "/" + tag + ".png";
      ScreenCapture.CaptureScreenshot(path);
      try {
        System.IO.File.WriteAllText(_shotDir + "/" + tag + ".txt",
          "label=" + label + "\nstage=" + _stageName + "\ntime="
          + (Time.realtimeSinceStartup - _started).ToString("F1") + "s\nclicks=" + _clicks
          + "\nquestions=" + _questions + "\n" + Census() + "\n");
      } catch (Exception) { }
      Log("SHOT " + tag + " | " + Census());
      _shots++;
      _shotPending = false;
      _intent = false;
    } catch (Exception e) {
      _shotPending = false;
      _intent = false;
      Log("shot failed: " + e.Message);
    }
  }

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
        Log("IDLE12 #" + _idleCount + " stage=" + _stageName + " | " + Census());
        if (_idleCount % 3 == 1) Shot("IDLE12_" + _idleCount + "_" + _stageName);
      }
    }
  }

  bool _watchOff;

  // ---- accounting -------------------------------------------------------------

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
    if (_failStreak >= 4) {
      _cancel = true;
      Log("STRESS_CANCEL: four consecutive step failures (last: " + step + ")");
    }
    return false;
  }

  void Question(string arena, int level, int round, string kind, string detail) {
    _questions++;
    int n;
    _qByArena.TryGetValue(arena, out n);
    _qByArena[arena] = n + 1;
    Log("Q#" + _questions + " arena=" + arena + " level=" + level + " round=" + round
      + " kind=" + kind + (string.IsNullOrEmpty(detail) ? "" : " " + detail));
  }

  void Coverage(string key, string value) {
    _coverage[key] = value;
  }

  // ---- main / stage engine ----------------------------------------------------

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
    try {
      StringBuilder sb = new StringBuilder();
      sb.Append("STRESS_SUMMARY run=").Append(_runNumber).Append('\n');
      sb.Append("questions=").Append(_questions).Append(" errors=").Append(_errors)
        .Append(" severe=").Append(_severeCount).Append(" idle=").Append(_idleCount)
        .Append(" cancel=").Append(_cancel).Append(" shots=").Append(_shots)
        .Append(" clicks=").Append(_clicks).Append('\n');
      foreach (KeyValuePair<string, int> kv in _qByArena)
        sb.Append("arena=").Append(kv.Key).Append(" questions=").Append(kv.Value).Append('\n');
      foreach (KeyValuePair<string, string> kv in _coverage)
        sb.Append("coverage ").Append(kv.Key).Append(" = ").Append(kv.Value).Append('\n');
      for (int i = 0; i < _summary.Count; i++) sb.Append(_summary[i]).Append('\n');
      System.IO.File.WriteAllText(_shotDir + "/stress_summary.txt", sb.ToString());
    } catch (Exception) { }
    Shot(_cancel ? "999_cancel_final" : "999_final");
    Log("STRESS_END shots=" + _shots + " clicks=" + _clicks + " steps=" + _steps
      + " questions=" + _questions + " errors=" + _errors + " severe=" + _severeCount
      + " idle=" + _idleCount + " cancel=" + _cancel
      + " elapsed=" + (Time.realtimeSinceStartup - _started).ToString("F0") + "s");
    yield return new WaitForSeconds(1.5f);
    Log("STRESS_AUTOCLOSE");
    Quit(_cancel ? 2 : (_errors == 0 && _severeCount == 0 ? 0 : 1));
  }

  IEnumerator MainBody() {
    Log("STRESS_START run=" + _runNumber + " stage='" + (_allMode ? "all" : _stageArg)
      + "' from='" + _startStage + "'");
    _watchOff = true;
    yield return new WaitForSeconds(7f);
    ShotRawOnly("000_boot_raw");
    DismissSystemDialogs();
    yield return EnsureForeground();

    MicSetupDialog mic0 = FindObjectOfType<MicSetupDialog>();
    if (mic0 != null && mic0.IsShowing) {
      bool declined = ClickTextButton("Bỏ qua");
      yield return WaitStep(delegate {
        MicSetupDialog m = FindObjectOfType<MicSetupDialog>();
        return m == null || !m.IsShowing;
      }, 20f, "mic offer handled");
      Record("boot: mic offer declined", _stepOk, declined ? "" : "no text button");
    }
    yield return WaitStep(delegate {
      LanguageDialog d = FindObjectOfType<LanguageDialog>();
      return d != null && d.IsOpen;
    }, 40f, "language dialog");
    bool langOk = false;
    for (int i = 0; i < 3 && !langOk; i++) {
      yield return EnsureForeground();
      yield return ClickButtonByName("EnBox");
      yield return WaitStep(delegate { return !IsLanguageOpen(); }, 8f, "language chosen try " + (i + 1));
      langOk = _stepOk;
    }
    if (!langOk) {
      Vector3 away = PlayerPos() + new Vector3(0f, 0f, 8f);
      yield return WalkStep(delegate { return !IsLanguageOpen(); }, away, 2.5f, 25f,
        "walk away from language card", null, null);
      langOk = _stepOk;
    }
    _watchOff = false;
    Record("boot + language", langOk, "");
    Shot("001_hub");
    if (!StepGuard()) yield break;

    List<string> stages = new List<string>();
    if (_allMode) {
      int start = StageIndex(_startStage);
      for (int i = start; i < AllStages.Length; i++) stages.Add(AllStages[i]);
    } else {
      stages.Add(_stageArg);
    }

    for (int si = 0; si < stages.Count; si++) {
      if (_cancel) break;
      _stageName = stages[si];
      _stageSevere = false;
      _reloadNeeded = false;
      _severeReason = "";
      Log("STAGE_BEGIN " + _stageName);
      yield return Guard(StageBody(_stageName));
      Log("STAGE_END " + _stageName + " severe=" + _stageSevere
        + (string.IsNullOrEmpty(_severeReason) ? "" : " reason=" + _severeReason));
      _summary.Add("stage " + _stageName + " severe=" + _stageSevere
        + (string.IsNullOrEmpty(_severeReason) ? "" : " reason=" + _severeReason));
      if (_reloadNeeded && !_relaunching) {
        int next = Mathf.Min(si + 1, stages.Count - 1);
        Relaunch(stages[next]);
        yield break;
      }
    }
  }

  int StageIndex(string name) {
    if (string.IsNullOrEmpty(name)) return 0;
    for (int i = 0; i < AllStages.Length; i++)
      if (string.Equals(AllStages[i], name, StringComparison.OrdinalIgnoreCase)) return i;
    return 0;
  }

  IEnumerator StageBody(string name) {
    switch (name) {
      case "geometry": yield return StageGeometry(); break;
      case "market": yield return StageMarket(); break;
      case "city": yield return StageCity(); break;
      case "rabbit": yield return StageRabbitVisit(); break;
      case "stairs": yield return StageStairsVisit(); break;
      case "listening": yield return StageListeningVisit(); break;
      case "speaking": yield return StageSpeakingVisit(); break;
    }
  }

  // ---- stages -----------------------------------------------------------------

  IEnumerator StageGeometry() {
    yield return ReturnToMathSkillYard();
    if (_reloadNeeded) yield break;
    yield return EnterDoorStep("math_geometry", "geometry skill",
      delegate { return YardGame("math_geometry") != null; }, 120f);
    Record("geometry C yard", _stepOk, "");
    if (!_stepOk) { Severe("geometry.yard", "cannot enter geometry game yard"); yield break; }
    yield return EnterDoorStep("shape_builder", "geometry arena",
      delegate { return SceneLoaded(GeometryPlayBuilder.SceneName); }, 150f);
    Record("geometry arena loads", _stepOk, "");
    if (!_stepOk) { Severe("geometry", "geometry arena never loaded"); yield break; }
    _watchOff = true;
    yield return new WaitForSeconds(3.5f);
    Shot("geometry_enter");
    _watchOff = false;
    yield return PlayGeometryBank();
    yield return ExitArenaStep("math_geometry", "geometry exit");
    Record("geometry exit to C", _stepOk, "");
    if (!_stepOk) Severe("geometry.exit", "cannot leave the geometry arena");
  }

  IEnumerator StageMarket() {
    yield return ReturnToMathSkillYard();
    if (_reloadNeeded) yield break;
    yield return EnterDoorStep("math_comparison", "comparison skill",
      delegate { return YardGame("math_comparison") != null; }, 120f);
    Record("market C yard", _stepOk, "");
    if (!_stepOk) { Severe("market.yard", "cannot enter comparison game yard"); yield break; }
    yield return EnterDoorStep("comparison_market", "market arena",
      delegate { return SceneLoaded(ComparisonMarketBuilder.SceneName); }, 150f);
    Record("market arena loads", _stepOk, "");
    if (!_stepOk) { Severe("market", "market arena never loaded"); yield break; }
    _watchOff = true;
    yield return new WaitForSeconds(3.5f);
    Shot("market_enter");
    _watchOff = false;
    yield return PlayMarketBank();
    yield return ExitArenaStep("math_comparison", "market exit");
    Record("market exit to C", _stepOk, "");
    if (!_stepOk) Severe("market.exit", "cannot leave the market arena");
  }

  IEnumerator StageCity() {
    yield return ReturnToMathSkillYard();
    if (_reloadNeeded) yield break;
    yield return EnterDoorStep("math_classification", "classification skill",
      delegate { return YardGame("math_classification") != null; }, 120f);
    Record("city C yard", _stepOk, "");
    if (!_stepOk) { Severe("city.yard", "cannot enter classification game yard"); yield break; }
    yield return EnterDoorStep("classification_city", "city arena",
      delegate { return SceneLoaded(ClassificationCityBuilder.SceneName); }, 150f);
    Record("city arena loads", _stepOk, "");
    if (!_stepOk) { Severe("city", "city arena never loaded"); yield break; }
    _watchOff = true;
    yield return new WaitForSeconds(3.5f);
    Shot("city_enter");
    _watchOff = false;
    yield return PlayCityBank();
    yield return ExitArenaStep("math_classification", "city exit");
    Record("city exit to C", _stepOk, "");
    if (!_stepOk) Severe("city.exit", "cannot leave the city arena");
  }

  IEnumerator StageRabbitVisit() {
    yield return ReturnToMathSkillYard();
    if (_reloadNeeded) yield break;
    yield return EnterDoorStep("math_counting", "counting skill",
      delegate { return YardGame("math_counting") != null; }, 120f);
    Record("rabbit C yard", _stepOk, "");
    if (!_stepOk) { Severe("rabbit.yard", "cannot enter counting game yard"); yield break; }
    yield return EnterDoorStep("rabbit_feeding", "rabbit arena",
      delegate { return SceneLoaded(RabbitPlayBuilder.SceneName); }, 150f);
    Record("rabbit arena loads", _stepOk, "");
    if (!_stepOk) { Severe("rabbit", "rabbit arena never loaded"); yield break; }
    _watchOff = true;
    yield return new WaitForSeconds(3.5f);
    Shot("rabbit_enter");
    _watchOff = false;
    yield return PlayRabbitBank();
    yield return ExitArenaStep("math_counting", "rabbit exit");
    Record("rabbit exit to C", _stepOk, "");
    if (!_stepOk) Severe("rabbit.exit", "cannot leave the rabbit arena");
  }

  IEnumerator StageStairsVisit() {
    yield return ReturnToMathSkillYard();
    if (_reloadNeeded) yield break;
    yield return EnterDoorStep("math_counting", "counting skill",
      delegate { return YardGame("math_counting") != null; }, 120f);
    Record("stairs C yard", _stepOk, "");
    if (!_stepOk) { Severe("stairs.yard", "cannot enter counting game yard"); yield break; }
    yield return EnterDoorStep("number_stairs", "stairs arena",
      delegate { return SceneLoaded(StairHillBuilder.SceneName); }, 150f);
    Record("stairs arena loads", _stepOk, "");
    if (!_stepOk) { Severe("stairs", "stairs arena never loaded"); yield break; }
    _watchOff = true;
    yield return new WaitForSeconds(3.5f);
    Shot("stairs_enter");
    _watchOff = false;
    yield return PlayStairsBank();
    yield return ExitArenaStep("math_counting", "stairs exit");
    Record("stairs exit to C", _stepOk, "");
    if (!_stepOk) Severe("stairs.exit", "cannot leave the stairs arena");
  }

  IEnumerator StageListeningVisit() {
    yield return ReturnToHub();
    if (_reloadNeeded) yield break;
    yield return WalkIntoSubjectGateStep(SubjectIds.English, "english", "into english");
    Record("english B", _stepOk, "");
    if (!_stepOk) { Severe("listening.yard", "cannot enter english skill yard"); yield break; }
    yield return EnterDoorStep("english_listening", "listening skill",
      delegate { return YardGame("english_listening") != null; }, 120f);
    Record("listening C yard", _stepOk, "");
    if (!_stepOk) { Severe("listening.yard", "cannot enter listening game yard"); yield break; }
    yield return EnterDoorStep("listen_garden", "listening arena",
      delegate { return SceneLoaded(ListeningGardenBuilder.SceneName); }, 150f);
    Record("listening arena loads", _stepOk, "");
    if (!_stepOk) { Severe("listening", "listening arena never loaded"); yield break; }
    _watchOff = true;
    yield return new WaitForSeconds(3.5f);
    Shot("listening_enter");
    _watchOff = false;
    yield return PlayListeningBank();
    yield return ExitArenaStep("english_listening", "listening exit");
    Record("listening exit to C", _stepOk, "");
    if (!_stepOk) Severe("listening.exit", "cannot leave the listening arena");
  }

  IEnumerator StageSpeakingVisit() {
    yield return ReturnToHub();
    if (_reloadNeeded) yield break;
    yield return WalkIntoSubjectGateStep(SubjectIds.English, "english", "into english (2)");
    Record("english B (2)", _stepOk, "");
    if (!_stepOk) { Severe("speaking.yard", "cannot enter english skill yard"); yield break; }
    yield return EnterDoorStep("english_speaking", "speaking skill",
      delegate { return YardGame("english_speaking") != null; }, 120f);
    Record("speaking C yard", _stepOk, "");
    if (!_stepOk) { Severe("speaking.yard", "cannot enter speaking game yard"); yield break; }
    yield return EnterDoorStep("speaking_stage", "speaking arena",
      delegate { return SceneLoaded(SpeakingStageBuilder.SceneName); }, 150f);
    Record("speaking arena loads", _stepOk, "");
    if (!_stepOk) { Severe("speaking", "speaking arena never loaded"); yield break; }
    _watchOff = true;
    yield return new WaitForSeconds(4f);
    Shot("speaking_enter");
    _watchOff = false;
    yield return PlaySpeakingBank();
    yield return ExitArenaStep("english_speaking", "speaking exit");
    Record("speaking exit to C", _stepOk, "");
    if (!_stepOk) Severe("speaking.exit", "cannot leave the speaking arena");
  }

  // ---- GEOMETRY BANK (LV3..LV10, 29 rounds) -----------------------------------

  IEnumerator PlayGeometryBank() {
    GeometryPlay game = FindObjectOfType<GeometryPlay>();
    if (game == null) { Severe("geometry", "GeometryPlay missing"); yield break; }
    Transform spot = FindNamed("GPPlaySpot");
    if (spot != null) {
      yield return WalkStep(delegate {
        GeometryPlay g = FindObjectOfType<GeometryPlay>();
        return g != null && g.Current != GeometryPlay.Phase.Wait;
      }, spot.position, 1.2f, 90f, "geometry play spot", null, null);
      Record("geometry question starts", _stepOk, "");
    }
    string lastKey = "";
    int failGuard = 0;
    int guard = 0;
    while (guard++ < 400) {
      if (_cancel) yield break;
      GeometryPlay g = FindObjectOfType<GeometryPlay>();
      if (g == null) { Record("geometry game alive", false, "gone"); yield break; }
      if (g.Completed) break;
      if (g.Current == GeometryPlay.Phase.Done) break;
      if (g.Current == GeometryPlay.Phase.Hunt || g.Current == GeometryPlay.Phase.Carry) {
        string key = g.Level + "#" + g.RoundIndex;
        if (key != lastKey) {
          lastKey = key;
          Question("geometry", g.Level, g.RoundIndex, g.Action.ToString(), "target=" + g.Target);
          Shot("geo_l" + g.Level + "_r" + g.RoundIndex + "_" + g.Action);
        }
        bool acted = false;
        bool wantBehind = false;
        try {
          if (g.Action == GeometryPlay.ActionKind.TapEnv) acted = true;
          else if (g.Action == GeometryPlay.ActionKind.FindBehind) wantBehind = true;
          else acted = true;
        } catch (Exception e) { Log("geometry step exception: " + e.Message); }
        if (g.Action == GeometryPlay.ActionKind.TapEnv) {
          yield return GeoEnvStep(g);
        } else if (wantBehind) {
          yield return StepGeoBehind(g);
        } else if (g.Carried == null) {
          yield return GeoPickStep(g);
        } else {
          yield return GeoPlaceStep(g);
        }
        if (!acted) {
          failGuard++;
          if (failGuard >= 3) {
            Record("geometry solve L" + g.Level + " R" + g.RoundIndex, false,
              "no legal action (action=" + g.Action + " carried=" + (g.Carried != null) + ")");
            failGuard = 0;
            yield return new WaitForSeconds(2f);
          }
        } else failGuard = 0;
      }
      yield return new WaitForSeconds(1.6f);
    }
    GeometryPlay fin = FindObjectOfType<GeometryPlay>();
    bool done = fin != null && (fin.Completed || fin.Current == GeometryPlay.Phase.Done);
    Record("geometry bank completed (LV10)", done,
      fin == null ? "gone" : "level=" + fin.Level + " round=" + fin.RoundIndex);
    Coverage("geometry.completed", done ? "yes" : "no");
    yield return new WaitForSeconds(2f);
    Shot("geometry_done");
  }

  static GeometryPiece FindPieceOfKind(GeometryKind kind, bool env, bool idleOnly) {
    GeometryPlayBuilder b = FindObjectOfType<GeometryPlayBuilder>();
    if (b == null) return null;
    for (int i = 0; i < b.Pieces.Count; i++) {
      GeometryPiece p = b.Pieces[i];
      if (p == null) continue;
      if (p.EnvRole != env) continue;
      if (idleOnly && p.State != GeometryPiece.PieceState.Idle) continue;
      if (p.Kind != kind) continue;
      return p;
    }
    return null;
  }

  static GeometryPiece FindAnyIdlePiece(bool env) {
    GeometryPlayBuilder b = FindObjectOfType<GeometryPlayBuilder>();
    if (b == null) return null;
    for (int i = 0; i < b.Pieces.Count; i++) {
      GeometryPiece p = b.Pieces[i];
      if (p != null && p.EnvRole == env && p.State == GeometryPiece.PieceState.Idle) return p;
    }
    return null;
  }

  static GeometrySocket FindOpenSocket(GeometryKind kind) {
    GeometryPlayBuilder b = FindObjectOfType<GeometryPlayBuilder>();
    if (b == null) return null;
    for (int i = 0; i < b.Sockets.Count; i++) {
      GeometrySocket s = b.Sockets[i];
      if (s == null || s.Filled) continue;
      if (!s.gameObject.activeInHierarchy) continue;
      if (s.Kind != kind) continue;
      return s;
    }
    return null;
  }

  static GeometryKind PreferredKind(GeometryPlay g) {
    if (g.Need != null && g.Need.Length > 0) {
      for (int i = 0; i < g.Need.Length; i++) {
        if (FindOpenSocket(g.Need[i]) != null && FindPieceOfKind(g.Need[i], false, true) != null)
          return g.Need[i];
      }
    }
    GeometryPlayBuilder b = FindObjectOfType<GeometryPlayBuilder>();
    if (b != null) {
      for (int i = 0; i < b.Sockets.Count; i++) {
        GeometrySocket s = b.Sockets[i];
        if (s != null && !s.Filled && s.gameObject.activeInHierarchy
            && FindPieceOfKind(s.Kind, false, true) != null) return s.Kind;
      }
    }
    return g.Target;
  }

  IEnumerator GeoEnvStep(GeometryPlay g) {
    GeometryPlayBuilder b = FindObjectOfType<GeometryPlayBuilder>();
    GeometryPiece p = b != null ? b.EnvOf(g.Target) : null;
    if (p == null) p = FindPieceOfKind(g.Target, true, false);
    if (p == null) {
      _geoEnvNulls++;
      if (_geoEnvNulls >= 5) {
        Record("geometry env target exists L" + g.Level, false, "no env piece for " + g.Target);
        _geoEnvNulls = 0;
      }
      yield return new WaitForSeconds(1f);
      yield break;
    }
    _geoEnvNulls = 0;
    int lv = g.Level;
    int rd = g.RoundIndex;
    Vector3 pos = p.transform.position;
    yield return WalkStep(delegate { return PlayerNear(pos, 1.6f); }, pos, 1.6f, 40f,
      "geometry env walk", null, null);
    if (!_stepOk) {
      Log("geometry env walk failed; tapping in place " + Fmt(pos));
      yield return TapWorld(pos);
      yield return new WaitForSeconds(2.5f);
      yield break;
    }
    for (int i = 0; i < 3; i++) {
      yield return TapWorld(p.transform.position);
      yield return new WaitForSeconds(1.8f);
      GeometryPlay gg = FindObjectOfType<GeometryPlay>();
      if (gg == null || gg.Level != lv || gg.RoundIndex != rd || gg.Current != GeometryPlay.Phase.Hunt) yield break;
    }
    for (int i = 0; i < 4; i++) {
      float ang = 45f * (i + 1);
      Vector3 dir = Quaternion.Euler(0f, 90f + ang, 0f) * new Vector3(0f, 0f, 1f);
      Vector3 staging = p.transform.position + dir * 1.6f;
      yield return WalkStep(delegate { return PlayerNear(staging, 1.0f); }, staging, 1.0f, 20f,
        "geometry env approach " + ang, null, null);
      yield return TapWorld(p.transform.position);
      yield return new WaitForSeconds(1.5f);
      GeometryPlay gg = FindObjectOfType<GeometryPlay>();
      if (gg == null || gg.Level != lv || gg.RoundIndex != rd || gg.Current != GeometryPlay.Phase.Hunt) yield break;
    }
    Record("geometry env tap L" + lv + " R" + rd, false, "env tap never registered target=" + g.Target);
  }

  int _geoEnvNulls;

  IEnumerator StepGeoBehind(GeometryPlay g) {
    GeometryPiece p = FindPieceOfKind(g.Target, false, false);
    if (p == null) { Log("geometry FindBehind: no target piece"); yield break; }
    Transform crate = FindNamed("GPCrate");
    Vector3 dir = crate != null ? p.transform.position - crate.position : new Vector3(0f, 0f, 1f);
    dir.y = 0f;
    if (dir.sqrMagnitude < 0.01f) dir = new Vector3(0f, 0f, 1f);
    dir.Normalize();
    float[] angles = { 0f, 45f, -45f, 90f, -90f, 135f, 180f };
    for (int i = 0; i < angles.Length; i++) {
      GeometryPlay gg = FindObjectOfType<GeometryPlay>();
      if (gg == null || gg.Completed || gg.Current == GeometryPlay.Phase.Done
          || gg.Current == GeometryPlay.Phase.Carry) yield break;
      Vector3 d = Quaternion.Euler(0f, angles[i], 0f) * dir;
      Vector3 staging = p.transform.position + d * 2.1f;
      staging.y = p.transform.position.y;
      yield return WalkStep(delegate { return PlayerNear(staging, 1.2f); }, staging, 1.2f, 30f,
        "geometry behind approach " + angles[i], null, null);
      yield return TapWorld(p.transform.position);
      float t = 0f;
      while (t < 4f) {
        GeometryPlay now = FindObjectOfType<GeometryPlay>();
        if (now == null || now.Completed || now.Current != GeometryPlay.Phase.Hunt) yield break;
        yield return new WaitForSeconds(0.5f);
        t += 0.5f;
      }
    }
  }

  IEnumerator GeoPickStep(GeometryPlay g) {
    GeometryKind want = (g.Action == GeometryPlay.ActionKind.Memory) ? g.Target : PreferredKind(g);
    GeometryPiece p = FindPieceOfKind(want, false, true);
    if (p == null) p = FindAnyIdlePiece(false);
    if (p == null) { yield return new WaitForSeconds(1f); yield break; }
    Vector3 pos = p.transform.position;
    yield return WalkStep(delegate { return PlayerNear(pos, 1.7f); }, pos, 1.7f, 30f,
      "geometry walk to piece", null, null, true);
    GeometryPiece again = FindPieceOfKind(want, false, true);
    Vector3 tap = again != null ? again.transform.position : pos;
    Log("geometry pick: want=" + want + " piece=" + (again != null ? again.name : "?")
      + " at " + Fmt(tap));
    yield return TapWorld(tap);
    int lv = g.Level;
    int rd = g.RoundIndex;
    float t = 0f;
    while (t < 12f) {
      GeometryPlay gg = FindObjectOfType<GeometryPlay>();
      if (gg == null) yield break;
      if (gg.Carried != null || gg.Level != lv || gg.RoundIndex != rd
          || gg.Current != GeometryPlay.Phase.Hunt) yield break;
      yield return new WaitForSeconds(0.5f);
      t += 0.5f;
    }
  }

  IEnumerator GeoPlaceStep(GeometryPlay g) {
    GeometryPiece carried = g.Carried;
    if (carried == null) yield break;
    GeometrySocket s = FindOpenSocket(carried.Kind);
    if (s == null) { yield return new WaitForSeconds(1.5f); yield break; }
    Vector3 spos = s.transform.position;
    int lv = g.Level;
    int rd = g.RoundIndex;
    Log("geometry place: carried=" + carried.Kind + " socket=" + s.name + " filled=" + s.Filled
      + " L" + lv + "R" + rd);
    yield return WalkStep(delegate {
      GeometryPlay gg = FindObjectOfType<GeometryPlay>();
      return gg == null || gg.Carried == null || s == null || s.Filled
        || gg.Current == GeometryPlay.Phase.Done || gg.Level != lv || gg.RoundIndex != rd;
    }, spos, 1.0f, 40f, "geometry carry to socket", null, null, true);
    GeometryPlay now = FindObjectOfType<GeometryPlay>();
    if (now != null && now.Carried != null && (s == null || !s.Filled)) {
      yield return WalkStep(delegate {
        GeometryPlay gg = FindObjectOfType<GeometryPlay>();
        return gg == null || gg.Carried == null || s == null || s.Filled
          || gg.Level != lv || gg.RoundIndex != rd;
      }, spos, 0.7f, 20f, "geometry place close", null, null, true);
      GeometryPlay now2 = FindObjectOfType<GeometryPlay>();
      if (now2 != null && now2.Carried != null && (s == null || !s.Filled)) {
        yield return TapWorld(spos);
      }
    }
    float t = 0f;
    while (t < 8f) {
      GeometryPlay gg = FindObjectOfType<GeometryPlay>();
      if (gg == null || gg.Carried == null || gg.Level != lv || gg.RoundIndex != rd) yield break;
      yield return new WaitForSeconds(0.5f);
      t += 0.5f;
    }
  }

  // ---- MARKET BANK (LV3..LV10, 30 rounds) ------------------------------------

  IEnumerator PlayMarketBank() {
    ComparisonMarket game = FindObjectOfType<ComparisonMarket>();
    if (game == null) { Severe("market", "ComparisonMarket missing"); yield break; }
    Transform spot = FindNamed("CMPlaySpot");
    if (spot != null) {
      yield return WalkStep(delegate {
        ComparisonMarket m = FindObjectOfType<ComparisonMarket>();
        return m != null && m.Current != ComparisonMarket.Phase.Wait;
      }, spot.position, 1.2f, 90f, "market play spot", null, null);
      Record("market question starts", _stepOk, "");
    }
    bool wrongDone = false;
    string lastKey = "";
    int guard = 0;
    while (guard++ < 300) {
      if (_cancel) yield break;
      ComparisonMarket m = FindObjectOfType<ComparisonMarket>();
      if (m == null) { Record("market game alive", false, "gone"); yield break; }
      if (m.Completed) break;
      string key = m.Level + "#" + m.RoundIndex;
      if (key != lastKey && (m.Current == ComparisonMarket.Phase.Choose
          || m.Current == ComparisonMarket.Phase.Pair
          || m.Current == ComparisonMarket.Phase.Confirm
          || m.Current == ComparisonMarket.Phase.Deliver)) {
        lastKey = key;
        Question("market", m.Level, m.RoundIndex, m.CType.ToString(),
          "relation=" + m.Relation + " phase=" + m.Current);
        Shot("mkt_l" + m.Level + "_r" + m.RoundIndex + "_" + m.CType);
      }
      switch (m.Current) {
        case ComparisonMarket.Phase.Wait:
          yield return new WaitForSeconds(1f);
          break;
        case ComparisonMarket.Phase.Choose: {
          if (!wrongDone && m.Level == 3 && m.RoundIndex == 0) {
            ComparisonChoice wrong = FindChoice(false, false);
            if (wrong != null) {
              yield return WalkAndTap(wrong.transform.position, 1.8f, "market wrong choice");
              yield return WaitStep(delegate {
                ComparisonMarket mm = FindObjectOfType<ComparisonMarket>();
                return mm != null && mm.WrongCount > 0;
              }, 15f, "market wrong registered");
              Record("market wrong tap tolerated", _stepOk, "");
              wrongDone = true;
            }
          }
          ComparisonChoice ans = FindChoice(true, false);
          if (ans != null) {
            yield return WalkAndTap(ans.transform.position, 1.8f, "market answer");
            yield return WaitStep(delegate {
              ComparisonMarket mm = FindObjectOfType<ComparisonMarket>();
              return mm == null || mm.Completed || mm.Level != m.Level || mm.RoundIndex != m.RoundIndex
                || mm.Current == ComparisonMarket.Phase.Deliver;
            }, 25f, "market round advanced");
          }
          break;
        }
        case ComparisonMarket.Phase.Pair: {
          ComparisonFruit f = FirstUnpairedFruit();
          if (f != null) {
            yield return WalkAndTap(f.transform.position, 1.6f, "market fruit pair");
            yield return WaitStep(delegate {
              ComparisonMarket mm = FindObjectOfType<ComparisonMarket>();
              return mm == null || mm.Current != ComparisonMarket.Phase.Pair
                || mm.PairsDone >= mm.PairsNeed;
            }, 20f, "market pair counted");
          } else yield return new WaitForSeconds(1f);
          break;
        }
        case ComparisonMarket.Phase.Confirm: {
          ComparisonChoice ans = FindChoice(true, false);
          if (ans != null) {
            yield return WalkAndTap(ans.transform.position, 1.8f, "market confirm basket");
            yield return WaitStep(delegate {
              ComparisonMarket mm = FindObjectOfType<ComparisonMarket>();
              return mm == null || mm.Completed || mm.RoundIndex != m.RoundIndex || mm.Level != m.Level;
            }, 25f, "market confirm advanced");
          }
          break;
        }
        case ComparisonMarket.Phase.Deliver: {
          ComparisonChoice cart = FindChoice(false, true);
          if (cart != null) {
            yield return WalkAndTap(cart.transform.position, 2.0f, "market deliver cart");
            yield return WaitStep(delegate {
              ComparisonMarket mm = FindObjectOfType<ComparisonMarket>();
              return mm == null || mm.Completed || mm.RoundIndex != m.RoundIndex || mm.Level != m.Level;
            }, 25f, "market deliver advanced");
          }
          break;
        }
        default:
          yield return new WaitForSeconds(1f);
          break;
      }
    }
    ComparisonMarket fin = FindObjectOfType<ComparisonMarket>();
    bool done = fin != null && fin.Completed;
    Record("market bank completed (LV10)", done,
      fin == null ? "gone" : "level=" + fin.Level + " round=" + fin.RoundIndex);
    Coverage("market.completed", done ? "yes" : "no");
    yield return new WaitForSeconds(2f);
    Shot("market_done");
  }

  static ComparisonChoice FindChoice(bool answer, bool cart) {
    ComparisonMarketBuilder b = FindObjectOfType<ComparisonMarketBuilder>();
    if (b == null) return null;
    for (int i = 0; i < b.Choices.Count; i++) {
      ComparisonChoice c = b.Choices[i];
      if (c == null) continue;
      if (c.IsCart != cart) continue;
      if (c.IsAnswer != answer) continue;
      return c;
    }
    return null;
  }

  static ComparisonFruit FirstUnpairedFruit() {
    ComparisonMarketBuilder b = FindObjectOfType<ComparisonMarketBuilder>();
    if (b == null) return null;
    for (int i = 0; i < b.Fruits.Count; i++) {
      ComparisonFruit f = b.Fruits[i];
      if (f != null && !f.Paired) return f;
    }
    return null;
  }

  IEnumerator WalkAndTap(Vector3 world, float arrive, string label) {
    yield return WalkStep(delegate { return PlayerNear(world, arrive + 0.3f); }, world, arrive, 45f, label, null, null);
    yield return TapWorld(world);
  }

  // ---- CITY BANK (LV3..LV10, 18 rounds) ---------------------------------------

  IEnumerator PlayCityBank() {
    ClassificationCity game = FindObjectOfType<ClassificationCity>();
    if (game == null) { Severe("city", "ClassificationCity missing"); yield break; }
    Transform spot = FindNamed("CCPlaySpot");
    if (spot != null) {
      yield return WalkStep(delegate {
        ClassificationCity c = FindObjectOfType<ClassificationCity>();
        return c != null && c.Current != ClassificationCity.Phase.Wait;
      }, spot.position, 1.2f, 90f, "city play spot", null, null);
      Record("city question starts", _stepOk, "");
    }
    bool wrongDone = false;
    string lastKey = "";
    int guard = 0;
    while (guard++ < 500) {
      if (_cancel) yield break;
      ClassificationCity c = FindObjectOfType<ClassificationCity>();
      if (c == null) { Record("city game alive", false, "gone"); yield break; }
      if (c.Completed) break;
      string key = c.Level + "#" + c.RoundIndex;
      if (key != lastKey) {
        lastKey = key;
        Question("city", c.Level, c.RoundIndex, c.Criterion.ToString(),
          "groups=" + (c.Groups != null ? string.Join(",", c.Groups) : "?"));
        Shot("cty_l" + c.Level + "_r" + c.RoundIndex + "_" + c.Criterion);
      }
      if (c.Current != ClassificationCity.Phase.Sort) {
        yield return new WaitForSeconds(1f);
        continue;
      }
      if (c.Level == 9 && !c.OddPicked) {
        ClassificationItem odd = FindOddItem(c);
        if (odd != null) {
          yield return WalkAndTap(odd.transform.position, 1.6f, "city odd pick");
          yield return WaitStep(delegate {
            ClassificationCity cc = FindObjectOfType<ClassificationCity>();
            return cc == null || cc.OddPicked || cc.Carried != null;
          }, 20f, "city odd picked");
        }
        yield return new WaitForSeconds(0.6f);
        continue;
      }
      if (c.Carried == null) {
        ClassificationItem item = FirstIdleItem();
        if (item == null) { yield return new WaitForSeconds(1f); continue; }
        yield return WalkAndTap(item.transform.position, 1.6f, "city item pick");
        yield return WaitStep(delegate {
          ClassificationCity cc = FindObjectOfType<ClassificationCity>();
          return cc == null || cc.Carried != null || cc.Completed;
        }, 20f, "city item carried");
        if (!wrongDone && c.Level == 3 && c.RoundIndex == 0) {
          ClassificationBin wrongBin = FindWrongBin(c);
          if (wrongBin != null) {
            yield return WalkAndTap(wrongBin.transform.position, 1.4f, "city wrong bin");
            yield return WaitStep(delegate {
              ClassificationCity cc = FindObjectOfType<ClassificationCity>();
              return cc != null && cc.WrongCount > 0;
            }, 15f, "city wrong registered");
            Record("city wrong bin tolerated", _stepOk, "");
            wrongDone = true;
          }
        }
      } else {
        ClassificationItem carried = c.Carried;
        ClassificationBin bin = null;
        if (c.Level == 9) bin = FindBinKey("other");
        else {
          string want = ClassLogic.KeyFor(carried.Props, c.Criterion);
          bin = FindBinKey(want);
        }
        if (bin == null) { yield return new WaitForSeconds(1f); continue; }
        yield return WalkStep(delegate {
          ClassificationCity cc = FindObjectOfType<ClassificationCity>();
          return cc == null || cc.Carried == null || cc.Completed;
        }, bin.transform.position, 1.05f, 45f, "city carry to bin", null, null);
        ClassificationCity cc2 = FindObjectOfType<ClassificationCity>();
        if (cc2 != null && cc2.Carried != null) yield return TapWorld(bin.transform.position);
        yield return new WaitForSeconds(1.8f);
      }
    }
    ClassificationCity fin = FindObjectOfType<ClassificationCity>();
    bool done = fin != null && fin.Completed;
    Record("city bank completed (LV10)", done,
      fin == null ? "gone" : "level=" + fin.Level + " round=" + fin.RoundIndex);
    Coverage("city.completed", done ? "yes" : "no");
    yield return new WaitForSeconds(2f);
    Shot("city_done");
  }

  static ClassificationItem FirstIdleItem() {
    ClassificationCityBuilder b = FindObjectOfType<ClassificationCityBuilder>();
    if (b == null) return null;
    for (int i = 0; i < b.Items.Count; i++) {
      ClassificationItem it = b.Items[i];
      if (it != null && it.State == ClassificationItem.ItemState.Idle) return it;
    }
    return null;
  }

  static ClassificationBin FindBinKey(string key) {
    ClassificationCityBuilder b = FindObjectOfType<ClassificationCityBuilder>();
    if (b == null) return null;
    for (int i = 0; i < b.Bins.Count; i++) {
      ClassificationBin bin = b.Bins[i];
      if (bin != null && bin.GroupKey == key) return bin;
    }
    return null;
  }

  static ClassificationBin FindWrongBin(ClassificationCity c) {
    ClassificationCityBuilder b = FindObjectOfType<ClassificationCityBuilder>();
    if (b == null || b.Bins.Count == 0) return null;
    string want = c.Carried != null ? ClassLogic.KeyFor(c.Carried.Props, c.Criterion) : "";
    for (int i = 0; i < b.Bins.Count; i++) {
      ClassificationBin bin = b.Bins[i];
      if (bin != null && bin.GroupKey != want) return bin;
    }
    return null;
  }

  static ClassificationItem FindOddItem(ClassificationCity c) {
    ClassificationCityBuilder b = FindObjectOfType<ClassificationCityBuilder>();
    if (b == null) return null;
    List<ClassificationItem> idle = new List<ClassificationItem>();
    for (int i = 0; i < b.Items.Count; i++) {
      ClassificationItem it = b.Items[i];
      if (it != null && it.State == ClassificationItem.ItemState.Idle) idle.Add(it);
    }
    for (int i = 0; i < idle.Count; i++) {
      string key = ClassLogic.KeyFor(idle[i].Props, c.Criterion);
      int n = 0;
      for (int j = 0; j < idle.Count; j++)
        if (ClassLogic.KeyFor(idle[j].Props, c.Criterion) == key) n++;
      if (n == 1) return idle[i];
    }
    return null;
  }

  // ---- RABBIT BANK (kinds + quiz + targets coverage, random by design) ---------

  IEnumerator PlayRabbitBank() {
    RabbitFeed game = FindObjectOfType<RabbitFeed>();
    if (game == null) { Severe("rabbit", "RabbitFeed missing"); yield break; }
    Transform spot = FindNamed("RPPlaySpot");
    if (spot != null) {
      yield return WalkStep(delegate {
        RabbitFeed f = FindObjectOfType<RabbitFeed>();
        return f != null && f.Current != RabbitFeed.Phase.Wait;
      }, spot.position, 1.4f, 90f, "rabbit play spot", null, null);
      Record("rabbit question starts", _stepOk, "");
    }
    HashSet<string> targets = new HashSet<string>();
    HashSet<string> kinds = new HashSet<string>();
    int quizzes = 0;
    int cap = 36;
    int solved = 0;
    bool wrongQuizDone = false;
    bool wrongSubmitDone = false;
    string lastQKey = "";
    bool solvedLogged = false;
    float tCap = 1800f;
    float t0 = Time.realtimeSinceStartup;
    int guard = 0;
    while (guard++ < 900 && Time.realtimeSinceStartup - t0 < tCap) {
      if (_cancel) yield break;
      RabbitFeed f = FindObjectOfType<RabbitFeed>();
      if (f == null) break;
      RabbitFeed.Phase ph = f.Current;
      string qKey = ph + "#" + f.Target + "#" + f.Kind + "#" + f.IsQuizRound + "#" + f.Submits + "#" + quizzes;
      if ((ph == RabbitFeed.Phase.Feeding || ph == RabbitFeed.Phase.Quiz) && qKey != lastQKey) {
        lastQKey = qKey;
        solvedLogged = false;
        Question("rabbit", 0, solved, f.IsQuizRound ? "Quiz" : f.Kind.ToString(),
          "target=" + f.Target + " opA=" + f.OpA + " opB=" + f.OpB + " count=" + f.Count);
        Shot("rab_q" + solved + "_" + (f.IsQuizRound ? "quiz" : f.Kind.ToString()));
      }
      if (ph == RabbitFeed.Phase.Success) {
        if (!solvedLogged) {
          solvedLogged = true;
          solved++;
          targets.Add(f.Target.ToString());
          kinds.Add(f.IsQuizRound ? "Quiz" : f.Kind.ToString());
          if (f.IsQuizRound) quizzes++;
          Log("RABBIT_SOLVED n=" + solved + " target=" + f.Target + " kind=" + f.Kind
            + " quiz=" + f.IsQuizRound);
        }
        bool coverage = targets.Count >= 9 && kinds.Contains("Plain") && kinds.Contains("Add")
          && kinds.Contains("Sub") && quizzes >= 2;
        if (coverage && solved >= 12) {
          Log("RABBIT_COVERAGE_DONE " + CoverageStr(targets, kinds, quizzes));
          break;
        }
        if (solved >= cap) {
          Log("RABBIT_CAP_REACHED " + CoverageStr(targets, kinds, quizzes));
          break;
        }
        yield return new WaitForSeconds(1.5f);
        continue;
      }
      if (ph == RabbitFeed.Phase.Demo || ph == RabbitFeed.Phase.Intro || ph == RabbitFeed.Phase.Wrong) {
        yield return new WaitForSeconds(1.5f);
        continue;
      }
      if (ph == RabbitFeed.Phase.Quiz) {
        RabbitAnswerTile[] tiles = FindObjectsOfType<RabbitAnswerTile>();
        if (!wrongQuizDone) {
          RabbitAnswerTile wrongTile = null;
          for (int i = 0; i < tiles.Length; i++) {
            if (tiles[i] != null && tiles[i].gameObject.activeInHierarchy && tiles[i].Count != f.Target) {
              wrongTile = tiles[i];
              break;
            }
          }
          if (wrongTile != null) {
            yield return WalkAndTap(wrongTile.transform.position, 1.8f, "rabbit wrong quiz tile");
            yield return WaitStep(delegate {
              RabbitFeed ff = FindObjectOfType<RabbitFeed>();
              return ff == null || ff.Current == RabbitFeed.Phase.Wrong
                || ff.Current == RabbitFeed.Phase.Quiz || ff.Current == RabbitFeed.Phase.Success;
            }, 20f, "rabbit wrong quiz registered");
            Record("rabbit wrong quiz tap tolerated", _stepOk, "");
            wrongQuizDone = true;
            continue;
          }
        }
        RabbitAnswerTile ans = null;
        for (int i = 0; i < tiles.Length; i++) {
          if (tiles[i] != null && tiles[i].gameObject.activeInHierarchy && tiles[i].Count == f.Target) {
            ans = tiles[i];
            break;
          }
        }
        if (ans != null) {
          yield return WalkAndTap(ans.transform.position, 1.8f, "rabbit quiz answer");
        } else {
          yield return new WaitForSeconds(1.5f);
        }
        continue;
      }
      if (ph == RabbitFeed.Phase.Feeding) {
        int need = f.Target - f.Count;
        RabbitPlayBuilder bb = FindObjectOfType<RabbitPlayBuilder>();
        Vector3 bowl = bb != null && bb.FeedAnchor != null ? bb.FeedAnchor.position : Vector3.zero;
        Vector3 bell = bb != null && bb.SubmitAnchor != null ? bb.SubmitAnchor.position : Vector3.zero;
        if (need > 0) {
          RabbitCarrot c = FirstAvailableCarrot(f);
          if (c == null) { yield return new WaitForSeconds(1.5f); continue; }
          Vector3 cp = c.transform.position;
          yield return WalkStep(delegate { return PlayerNear(cp, 1.3f); }, cp, 1.3f, 40f,
            "rabbit carrot", null, null);
          int before = f.Count;
          yield return WalkStep(delegate {
            RabbitFeed ff = FindObjectOfType<RabbitFeed>();
            return ff == null || ff.Count > before || ff.Current != RabbitFeed.Phase.Feeding;
          }, bowl, 1.5f, 45f, "rabbit bowl feed", null, null);
        } else if (need < 0) {
          RabbitCarrot c = FirstBowlCarrot(f);
          if (c == null) { yield return new WaitForSeconds(1.5f); continue; }
          Vector3 cp = c.transform.position;
          yield return WalkStep(delegate { return PlayerNear(cp, 1.7f); }, cp, 1.7f, 40f,
            "rabbit bowl carrot", null, null);
          int before = f.Count;
          yield return TapWorld(cp);
          float w = 0f;
          while (w < 8f) {
            RabbitFeed ff = FindObjectOfType<RabbitFeed>();
            if (ff == null || ff.Count < before || ff.Current != RabbitFeed.Phase.Feeding) break;
            yield return new WaitForSeconds(0.5f);
            w += 0.5f;
          }
        } else {
          int sub0 = f.Submits;
          if (!wrongSubmitDone && solved == 1) {
            // intentional wrong submit once: add nothing first (if plain) -> near/wrong path
            if (need == 0 && f.Count != f.Target) { }
            yield return WalkStep(delegate {
              RabbitFeed ff = FindObjectOfType<RabbitFeed>();
              return ff == null || ff.Submits > sub0 || ff.ResultShown
                || ff.Current == RabbitFeed.Phase.Success;
            }, bell, 1.4f, 50f, "rabbit submit", null, null);
            wrongSubmitDone = true;
            yield return new WaitForSeconds(2.5f);
            continue;
          }
          yield return WalkStep(delegate {
            RabbitFeed ff = FindObjectOfType<RabbitFeed>();
            return ff == null || ff.Submits > sub0 || ff.ResultShown
              || ff.Current == RabbitFeed.Phase.Success;
          }, bell, 1.4f, 50f, "rabbit submit", null, null);
        }
        yield return new WaitForSeconds(0.8f);
        continue;
      }
      yield return new WaitForSeconds(1f);
    }
    Log("RABBIT_COVERAGE " + CoverageStr(targets, kinds, quizzes));
    Coverage("rabbit.targets", JoinSet(targets));
    Coverage("rabbit.kinds", JoinSet(kinds));
    Coverage("rabbit.quiz_rounds", quizzes.ToString());
    Coverage("rabbit.questions_solved", solved.ToString());
    Record("rabbit rounds exercised", solved >= 6,
      "solved=" + solved + " targets=" + targets.Count + " kinds=" + JoinSet(kinds) + " quizzes=" + quizzes);
    yield return new WaitForSeconds(2f);
    Shot("rabbit_done");
  }

  static string JoinSet(HashSet<string> set) {
    List<string> list = new List<string>(set);
    list.Sort();
    return string.Join(",", list.ToArray());
  }

  static string CoverageStr(HashSet<string> targets, HashSet<string> kinds, int quizzes) {
    return "targets=[" + JoinSet(targets) + "] kinds=[" + JoinSet(kinds) + "] quizzes=" + quizzes;
  }

  static RabbitCarrot FirstAvailableCarrot(RabbitFeed game) {
    for (int i = 0; i < game.CarrotCount; i++) {
      RabbitCarrot c = game.CarrotAt(i);
      if (c != null && c.State == RabbitCarrot.CarrotState.Available) return c;
    }
    return null;
  }

  static RabbitCarrot FirstBowlCarrot(RabbitFeed game) {
    for (int i = 0; i < game.CarrotCount; i++) {
      RabbitCarrot c = game.CarrotAt(i);
      if (c != null && c.State == RabbitCarrot.CarrotState.Consumed) return c;
    }
    return null;
  }

  // ---- STAIRS BANK (one live visit = 5 rounds; target forced by CLI) -----------

  IEnumerator PlayStairsBank() {
    NumberStairs stairs = FindObjectOfType<NumberStairs>();
    if (stairs == null) { Severe("stairs", "NumberStairs missing"); yield break; }
    Vector3 listen = StairHillBuilder.WorldOffset + StairHillBuilder.ListenLocal;
    yield return WalkStep(delegate {
      NumberStairs s = FindObjectOfType<NumberStairs>();
      return s != null && s.Current != NumberStairs.Phase.Wait;
    }, listen, 1.2f, 90f, "stairs listen ring", null, null);
    Record("stairs question starts", _stepOk, "");
    Coverage("stairs.opening_target", stairs.Target.ToString());
    int solved = 0;
    string lastKey = "";
    float t0 = Time.realtimeSinceStartup;
    int guard = 0;
    while (guard++ < 200 && Time.realtimeSinceStartup - t0 < 900f) {
      if (_cancel) yield break;
      NumberStairs s = FindObjectOfType<NumberStairs>();
      if (s == null) break;
      if (s.ResultShown) break;
      if (s.Current == NumberStairs.Phase.Climb) {
        string key = s.Kind + "#" + s.OpA + "#" + s.OpB + "#" + s.Target;
        if (key != lastKey) {
          lastKey = key;
          Question("stairs", 0, solved, s.Kind.ToString(),
            "target=" + s.Target + " opA=" + s.OpA + " opB=" + s.OpB + " step=" + s.CurrentStep);
          Shot("stair_q" + solved + "_" + s.Kind + "_t" + s.Target);
        }
        int t = Mathf.Clamp(s.Target, 1, StairHillBuilder.StepCount);
        Vector3 stepWorld = StairHillBuilder.WorldOffset
          + new Vector3(StairHillBuilder.CenterX, 0f,
            StairHillBuilder.BaseZ + (t - 0.5f) * StairHillBuilder.Tread);
        yield return WalkStep(delegate {
          NumberStairs ss = FindObjectOfType<NumberStairs>();
          return ss == null || ss.Current == NumberStairs.Phase.Success
            || ss.Current == NumberStairs.Phase.Demo || ss.CurrentStep == t
            || ss.ResultShown;
        }, stepWorld, 1.1f, 75f, "stairs climb to " + t, null, null);
        bool won = false;
        float w = 0f;
        while (w < 45f) {
          NumberStairs ss = FindObjectOfType<NumberStairs>();
          if (ss == null) break;
          if (ss.Current == NumberStairs.Phase.Success) { won = true; break; }
          if (ss.ResultShown) break;
          if (ss.Current == NumberStairs.Phase.Climb && ss.CurrentStep != t) {
            float dx = ss.CurrentStep - t;
            if (dx != 0f) {
              int tt = t;
              yield return WalkStep(delegate {
                NumberStairs s2 = FindObjectOfType<NumberStairs>();
                return s2 == null || s2.Current != NumberStairs.Phase.Climb || s2.CurrentStep == tt
                  || s2.Current == NumberStairs.Phase.Success;
              }, stepWorld, 1.0f, 30f, "stairs re-climb " + tt, null, null);
            }
          }
          yield return new WaitForSeconds(1f);
          w += 1f;
        }
        if (won) {
          solved++;
          Log("STAIRS_SOLVED n=" + solved + " target=" + s.Target + " kind=" + s.Kind
            + " from=" + s.OpA + " opB=" + s.OpB);
        }
      }
      yield return new WaitForSeconds(1.2f);
    }
    Record("stairs visit rounds exercised", solved >= 1, "solved=" + solved);
    Coverage("stairs.rounds_solved", solved.ToString());
    yield return new WaitForSeconds(2f);
    Shot("stairs_done");
  }

  // ---- LISTENING BANK (12 rounds; wrong tap + bell once) ----------------------

  IEnumerator PlayListeningBank() {
    ListeningGarden game = FindObjectOfType<ListeningGarden>();
    if (game == null) { Severe("listening", "ListeningGarden missing"); yield break; }
    Transform spot = FindNamed("LGPlaySpot");
    if (spot != null) {
      yield return WalkStep(delegate {
        ListeningGarden g = FindObjectOfType<ListeningGarden>();
        ListeningGardenBuilder b = FindObjectOfType<ListeningGardenBuilder>();
        return g != null && g.Current == ListeningGarden.Phase.Play && b != null && b.Choices.Count > 0;
      }, spot.position, 1.4f, 120f, "listening play spot", null, null);
    }
    Record("listening first round ready", _stepOk, "");
    int solved = 0;
    bool wrongDone = false;
    bool bellDone = false;
    string lastKey = "";
    float t0 = Time.realtimeSinceStartup;
    int guard = 0;
    while (guard++ < 200 && Time.realtimeSinceStartup - t0 < 1500f) {
      if (_cancel) yield break;
      ListeningGarden g = FindObjectOfType<ListeningGarden>();
      if (g == null) break;
      if (g.Completed) break;
      if (g.Current != ListeningGarden.Phase.Play) {
        yield return new WaitForSeconds(1f);
        continue;
      }
      string key = g.Level + "#" + g.RoundIndex;
      if (key != lastKey) {
        lastKey = key;
        Question("listening", g.Level, g.RoundIndex, "TapPicture", "target=" + g.TargetWord);
        Shot("lis_l" + g.Level + "_r" + g.RoundIndex);
        int round0 = g.RoundIndex;
        int level0 = g.Level;
        int wrong0 = g.WrongCount;
        yield return new WaitForSeconds(5f);
        if (round0 == 1 && !wrongDone) {
          yield return TapPodUntil(false, delegate {
            ListeningGarden gg = FindObjectOfType<ListeningGarden>();
            if (gg == null) return true;
            if (gg.RoundIndex != round0 || gg.Level != level0 || gg.Completed) return true;
            return gg.WrongCount > wrong0;
          }, 50f, "listening wrong tap");
          bool reg = false;
          ListeningGarden afterWrong = FindObjectOfType<ListeningGarden>();
          if (afterWrong != null) reg = afterWrong.WrongCount > wrong0;
          Record("listening wrong tap tolerated", reg,
            afterWrong == null ? "gone" : "wrong0=" + wrong0 + " now=" + afterWrong.WrongCount);
          wrongDone = true;
          yield return WaitStep(delegate {
            ListeningGarden gg = FindObjectOfType<ListeningGarden>();
            return gg == null || gg.Current == ListeningGarden.Phase.Play;
          }, 40f, "listening re-armed");
          yield return new WaitForSeconds(5f);
        }
        if (round0 == 2 && !bellDone) {
          ListeningGardenBuilder b = FindObjectOfType<ListeningGardenBuilder>();
          if (b != null && b.Bell != null) {
            Vector3 bp = b.Bell.transform.position;
            yield return WalkStep(delegate { return PlayerNear(bp, 2.2f); }, bp, 2.2f, 30f,
              "listening bell walk", null, null);
            yield return TapWorld(bp + new Vector3(0f, 0.9f, 0f));
            yield return new WaitForSeconds(2.5f);
            ListeningGarden gg = FindObjectOfType<ListeningGarden>();
            Record("listening bell replay keeps the round", gg != null && gg.Current == ListeningGarden.Phase.Play, "");
          }
          bellDone = true;
        }
        yield return TapPodUntil(true, delegate {
          ListeningGarden gg = FindObjectOfType<ListeningGarden>();
          return gg == null || gg.Completed || gg.RoundIndex != round0 || gg.Level != level0;
        }, 60f, "listening answer");
        ListeningGarden doneG = FindObjectOfType<ListeningGarden>();
        bool advanced = doneG == null || doneG.Completed
          || doneG.RoundIndex != round0 || doneG.Level != level0;
        if (advanced) solved++;
        else Record("listening round L" + level0 + " R" + round0 + " advance", false, "no advance after answer");
        yield return new WaitForSeconds(1.5f);
      } else {
        yield return new WaitForSeconds(1f);
      }
    }
    ListeningGarden fin = FindObjectOfType<ListeningGarden>();
    bool complete = fin != null && fin.Completed;
    Record("listening bank completed (12 rounds)", complete,
      fin == null ? "gone" : "level=" + fin.Level + " round=" + fin.RoundIndex);
    Coverage("listening.rounds_solved", solved.ToString());
    Coverage("listening.completed", complete ? "yes" : "no");
    yield return new WaitForSeconds(2f);
    Shot("listening_done");
  }

  static ListeningGardenChoice FindPod(bool answer) {
    ListeningGardenBuilder b = FindObjectOfType<ListeningGardenBuilder>();
    if (b == null) return null;
    for (int i = 0; i < b.Choices.Count; i++) {
      ListeningGardenChoice c = b.Choices[i];
      if (c != null && c.IsAnswer == answer) return c;
    }
    return null;
  }

  IEnumerator TapPodUntil(bool wantAnswer, Func<bool> stop, float deadline, string label) {
    float t = 0f;
    int attemptNo = 0;
    while (t < deadline) {
      bool done = false;
      try { done = stop(); } catch (Exception) { }
      if (done) {
        Log("tap stop accepted: " + label + " attempts=" + attemptNo);
        yield break;
      }
      ListeningGardenChoice pod = FindPod(wantAnswer);
      if (pod == null) { yield return new WaitForSeconds(1f); t += 1f; continue; }
      attemptNo++;
      Vector3 pp = pod.transform.position;
      Vector2 sp;
      if (!ProjectPodClick(pod, pp, out sp)) {
        yield return TapGroundToward(pp);
        yield return new WaitForSeconds(1.6f);
        t += 1.6f;
        continue;
      }
      Log("tap pod " + label + " attempt=" + attemptNo + " pod=" + pod.WordId
        + " sp=(" + sp.x.ToString("F0") + "," + sp.y.ToString("F0") + ")");
      yield return Tap(sp);
      float w = 0f;
      while (w < 8f) {
        bool stopNow = false;
        try { stopNow = stop(); } catch (Exception) { }
        if (stopNow) break;
        yield return new WaitForSeconds(0.4f);
        w += 0.4f; t += 0.4f;
      }
    }
    Log("TAP_TIMEOUT " + label + " attempts=" + attemptNo + " pos=" + Pos());
  }

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
    yield return Tap(new Vector2(sp.x, sp.y));
  }

  // ---- SPEAKING BANK (8 rounds via the scripted wav seam) ---------------------

  IEnumerator PlaySpeakingBank() {
    SpeakingStage stage = FindObjectOfType<SpeakingStage>();
    if (stage == null) { Severe("speaking", "SpeakingStage missing"); yield break; }
    Transform spot = FindNamed("SSPlaySpot");
    if (spot != null) {
      yield return WalkStep(delegate {
        SpeakingStage s = FindObjectOfType<SpeakingStage>();
        return s != null && s.Current != SpeakingStage.Phase.Wait;
      }, spot.position, 1.4f, 90f, "speaking play spot", null, null);
      Record("speaking question starts", _stepOk, "");
    }
    int attempts = 0;
    int lastAtt = -1;
    string lastKey = "";
    float t0 = Time.realtimeSinceStartup;
    int guard = 0;
    while (guard++ < 400 && Time.realtimeSinceStartup - t0 < 720f) {
      if (_cancel) yield break;
      SpeakingStage s = FindObjectOfType<SpeakingStage>();
      if (s == null) break;
      if (s.Completed) break;
      if (s.EnvErrors >= 2 && s.Current == SpeakingStage.Phase.Done) break;
      string key = (s.TargetWord ?? "") + "#" + s.AttemptsUsed + "#" + s.EnvErrors;
      if (key != lastKey) {
        lastKey = key;
        string dec = s.HasLastLevel ? s.LastAssessment.Decision.ToString() : "-";
        if (s.AttemptsUsed > 0) attempts++;
        Log("SPEAK target=" + s.TargetWord + " attempt=" + s.AttemptsUsed
          + " decision=" + dec + " env=" + s.EnvErrors
          + " level=" + (s.HasLastLevel ? s.LastLevel.ToString() : "none")
          + " pct=" + s.LastPercent);
        if (s.AttemptsUsed > 0 || s.EnvErrors > 0) Shot("spk_" + s.TargetWord + "_a" + s.AttemptsUsed + "_e" + s.EnvErrors);
      }
      if (s.EnvErrors > 0 && s.EnvErrors != lastAtt) {
        lastAtt = s.EnvErrors;
        Record("speaking environment error #" + s.EnvErrors, false,
          "target=" + s.TargetWord + " reason=env");
      }
      yield return new WaitForSeconds(0.25f);
    }
    SpeakingStage fin = FindObjectOfType<SpeakingStage>();
    bool done = fin != null && fin.Completed;
    int env = fin != null ? fin.EnvErrors : -1;
    Record("speaking bank completed (8 rounds)", done, fin == null ? "gone" : "env=" + env);
    Record("speaking zero environment errors", env == 0, "env=" + env);
    Coverage("speaking.attempts", attempts.ToString());
    Coverage("speaking.env_errors", env.ToString());
    Coverage("speaking.completed", done ? "yes" : "no");
    yield return new WaitForSeconds(2f);
    Shot("speaking_done");
  }

  // ---- navigation helpers -----------------------------------------------------

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
      if (!b.IsGameLevel && string.Equals(b.SubjectId, "math", StringComparison.OrdinalIgnoreCase)) yield break;
      bool wasGame = b.IsGameLevel;
      yield return SafeSpot(b, "yard safe");
      if (AnyArenaLoaded()) continue;
      yield return BackStep(delegate {
        return wasGame ? YardSkill("math") != null : !YardLoaded();
      }, 90f, wasGame ? "game yard -> math" : "yard -> hub");
      if (!_stepOk) {
        if (AnyArenaLoaded() && escapes++ < 3) {
          Severe("escape", "unexpected arena launched while leaving a yard");
          continue;
        }
        Severe("navigation", "cannot back out of the current yard");
        _reloadNeeded = true;
        yield break;
      }
    }
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
      Func<bool> wrong, string allowGateId, bool noSock = false) {
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
          Severe("no_exit:" + label, "arrived at " + Fmt(toward) + " but the world never reacted");
          Shot("noctx_" + Sanitize(label));
          yield break;
        }
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
        if (noSock) sp = AvoidClickable(sp);
        yield return Tap(new Vector2(sp.x, sp.y));
      }
      yield return new WaitForSeconds(1.2f);
      t += 1.2f;
      if (((int)t) % 12 == 0) RefreshRefs();
    }
    Log("WALK_TIMEOUT: " + label + " pos=" + Pos());
    Shot("timeout_" + Sanitize(label));
  }

  IEnumerator EnsureForeground() {
    float t = 0f;
    while (t < 20f) {
      UxOsInput.Focus();
      if (UxOsInput.IsForeground()) { Log("FOREGROUND_OK"); yield break; }
      yield return new WaitForSeconds(0.5f);
      t += 0.5f;
    }
    Log("FOREGROUND_WARN: window not foreground after 20s");
  }

  // While carrying (or walking to a piece) a walk-tap must never land on an
  // interactable (sockets/pieces/choices): an accidental click walks the child
  // to THAT object and fires it (found: L7 car assembly stuck on reject clicks).
  // Shifts the aim point until the ray hits plain world geometry.
  Vector3 AvoidClickable(Vector3 sp) {
    try {
      if (_cam == null) return sp;
      Vector2[] offs = {
        new Vector2(0f, -70f), new Vector2(0f, -140f), new Vector2(90f, 0f),
        new Vector2(-90f, 0f), new Vector2(0f, 70f), new Vector2(160f, -80f),
        new Vector2(-160f, -80f)
      };
      for (int k = -1; k < offs.Length; k++) {
        Vector2 q = k < 0 ? new Vector2(sp.x, sp.y) : new Vector2(sp.x + offs[k].x, sp.y + offs[k].y);
        if (q.x < 40f || q.x > Screen.width - 40f) continue;
        if (q.y < 40f || q.y > Screen.height - 40f) continue;
        RaycastHit hit;
        if (!Physics.Raycast(_cam.ScreenPointToRay(new Vector3(q.x, q.y, 0f)), out hit, 80f))
          return new Vector3(q.x, q.y, 0f);
        if (hit.collider.GetComponentInParent<GeometrySocket>() == null
            && hit.collider.GetComponentInParent<GeometryPiece>() == null
            && hit.collider.GetComponentInParent<ClassificationItem>() == null
            && hit.collider.GetComponentInParent<ClassificationBin>() == null
            && hit.collider.GetComponentInParent<ComparisonChoice>() == null
            && hit.collider.GetComponentInParent<ComparisonFruit>() == null
            && hit.collider.GetComponentInParent<ListeningGardenChoice>() == null)
          return new Vector3(q.x, q.y, 0f);
      }
    } catch (Exception) { }
    return sp;
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

  // ---- state probes -----------------------------------------------------------

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

  static readonly string[] ArenaScenes = {
    GeometryPlayBuilder.SceneName, ComparisonMarketBuilder.SceneName,
    ClassificationCityBuilder.SceneName, RabbitPlayBuilder.SceneName,
    StairHillBuilder.SceneName, ListeningGardenBuilder.SceneName,
    SpeakingStageBuilder.SceneName
  };

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
    if (_player == null) _player = FindObjectOfType<ClickToMove>();
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
    } catch (Exception) { }
  }

  void ShotRawOnly(string label) {
    try {
      string tag = _shots.ToString("000") + "_" + Sanitize(label);
      ScreenCapture.CaptureScreenshot(_shotDir + "/" + tag + ".png");
      Log("SHOT_RAW " + tag);
      _shots++;
    } catch (Exception) { }
  }

  static Transform FindNamed(string name) {
    if (string.IsNullOrEmpty(name)) return null;
    Transform[] all = FindObjectsOfType<Transform>();
    for (int i = 0; i < all.Length; i++)
      if (all[i] != null && all[i].name == name) return all[i];
    return null;
  }

  void RefreshRefs() {
    try {
      if (_player == null) _player = FindObjectOfType<ClickToMove>();
      _cam = Camera.main;
    } catch (Exception) { }
  }

  // ---- REAL input ---------------------------------------------------------------

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

  bool ClickTextButton(string text) {
    try {
      foreach (Button b in FindObjectsOfType<Button>()) {
        if (b == null) continue;
        Text t = b.GetComponentInChildren<Text>();
        if (t != null && t.text != null && t.text.Contains(text)) {
          RectTransform rt = b.GetComponent<RectTransform>();
          if (rt == null) continue;
          StartCoroutine(Tap(UiScreenPos(rt)));
          return true;
        }
      }
    } catch (Exception) { }
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

  bool StepGuard() { return !_cancel; }

  // ---- relaunch-from-stage (only when the world cannot be left) ----------------

  void Relaunch(string fromStage) {
    _relaunching = true;
    if (_runNumber >= 6) {
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
        if (string.Equals(args[i], "-stress-run", StringComparison.OrdinalIgnoreCase)) { i++; continue; }
        if (string.Equals(args[i], "-shot-dir", StringComparison.OrdinalIgnoreCase)) { i++; continue; }
        if (string.Equals(args[i], "-stress-from", StringComparison.OrdinalIgnoreCase)) { i++; continue; }
        if (string.Equals(args[i], "-stress-stage", StringComparison.OrdinalIgnoreCase)) { i++; continue; }
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
      sb.Append(" -stress-run ").Append(_runNumber + 1);
      sb.Append(" -shot-dir ").Append(nextShotDir);
      if (!string.IsNullOrEmpty(fromStage)) sb.Append(" -stress-from ").Append(fromStage);
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
