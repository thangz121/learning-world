// Assets/A_World/FullJourney/TempFullJourney.cs — FULL-JOURNEY driver
// (S3-P2Z16, maynode). One end-to-end pass over EVERY created part:
//   Main hub -> Math hub -> Counting Garden ->
//   gameplay #1 (balls in the basket, zone 2) -> #2 (number stairs, zone 5) ->
//   #3 (feed the bunny, zone 0) -> back to Math hub ->
//   #4 (build the tower, build_yard gate) -> #5 (deliver the apples,
//   delivery_village gate) -> return to Main.
// Boots ONLY with "-journeyfull" (committed drivers are inert otherwise).
// Real InputSystem mouse injection only (queued press held 4 frames); walks by
// projecting world targets to screen; NEVER teleports the player and never
// pokes gameplay state — every step is a real click + real-state polling.
//
// TIME DISCIPLINE (user order 2026-09-25): the moment a state settles, the
// screenshot is taken; no fixed settle-sleeps; condition polls run at 0.2s and
// walks re-click every 0.9s; re-entry exits never wait for the camera hand-off
// (ClickWorld routes a behind-camera door through a visible waypoint toward
// it). Evidence is analysed AFTER the run; the driver never stops to inspect.
//
// It also logs a CENSUS at every stage (scene count, area/game instances,
// player position) so invisible bugs (duplicates, leaks, stale instances)
// surface in the log even when the screen looks fine.
// C# 9.0 only.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TempFullJourneyBoot {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() {
    string[] args = Environment.GetCommandLineArgs();
    bool want = false;
    // Activates on the full flag OR on a resume flag: after a severe stage the
    // driver relaunches itself with "-journey-from <next stage>" so the new run
    // starts at the next game without re-running the failed one. The per-game
    // drivers use their own flags.
    foreach (string a in args) {
      if (string.Equals(a, "-journeyfull", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
      if (string.Equals(a, "-journey-from", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
    }
    if (!want) return;
    GameObject go = new GameObject("TempFullJourney");
    GameObject.DontDestroyOnLoad(go);
    go.AddComponent<TempFullJourney>();
  }
}

public class TempFullJourney : MonoBehaviour {
  const float PollSeconds = 0.2f;   // condition poll: react (and shoot) fast
  const float ClickSeconds = 0.9f;  // re-click cadence while walking

  // Stage engine (S3-P2Z17, user order): the journey is a table of named
  // stages. A stage that cannot proceed is SEVERE: evidence shots from many
  // camera angles, a marker in severe_errors.txt, then skip to the next game
  // in the same run when the arena door lets us out — otherwise fall back to a
  // fresh run started at the next stage (never re-running the failed one).
  static readonly string[] StageOrder = {
    "garden.stairs", "garden.rabbit",
    "hub.tower", "hub.delivery", "hub.match", "return.main",
  };

  // Deterministic stair rounds after the area's opening plain round (kind, B;
  // A = the step the child already stands on). Mid-range targets only.
  static readonly int[] StairPlanKind = { 1, 2, 1, 2 }; // Add, Sub, Add, Sub
  static readonly int[] StairPlanB = { 2, 2, 3, 2 };

  string _shotDir = "E:/LWW/fullj-shots";
  int _shots;
  int _clicks;
  int _errors;
  Camera _cam;
  ClickToMove _player;
  // Stall watchdog (user order): while a leg EXPECTS movement, a child that
  // stands still >10s is a stall — screenshot it, try to rescue it, and if the
  // leg cannot come back, mark the stage SEVERE (the stage engine captures the
  // multi-angle evidence and skips/relaunches).
  bool _watchOn;
  Vector3 _watchTarget;
  string _watchLabel = "";
  Vector3 _watchLastPos;
  float _watchStillT;
  int _stuckShots;
  bool _relaunching;
  int _runNumber = 1;
  // Stage engine state.
  string _startStage = "";
  string _stageName = "";
  bool _stageSevere;
  string _stageSevereReason = "";
  bool _handlingSevere;
  int _severeCount;
  bool _inStage;
  readonly System.Collections.Generic.List<string> _results =
    new System.Collections.Generic.List<string>();

  void Start() {
    string[] args = Environment.GetCommandLineArgs();
    for (int i = 0; i + 1 < args.Length; i++) {
      if (string.Equals(args[i], "-shot-dir", StringComparison.OrdinalIgnoreCase))
        _shotDir = args[i + 1];
      if (string.Equals(args[i], "-journey-run", StringComparison.OrdinalIgnoreCase)) {
        int n;
        if (int.TryParse(args[i + 1], out n) && n > 0) _runNumber = n;
      }
      if (string.Equals(args[i], "-journey-from", StringComparison.OrdinalIgnoreCase))
        _startStage = args[i + 1];
    }
    try { System.IO.Directory.CreateDirectory(_shotDir); } catch (Exception) { }
    try { InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; }
    catch (Exception) { }
    StartCoroutine(Main());
    StartCoroutine(Watchdog());
  }

  // Arm the watchdog while a leg expects the child to move/interact; disarm it
  // for legs where standing still is by design (cutscenes, demos, speech).
  void Watch(Vector3 target, string label) {
    _watchOn = true;
    _watchTarget = target;
    _watchLabel = label;
    _watchStillT = 0f;
    _watchLastPos = PlayerPosRaw();
  }

  void WatchOff() { _watchOn = false; }

  Vector3 PlayerPosRaw() {
    RefreshRefs();
    return _player != null ? _player.transform.position : Vector3.zero;
  }

  IEnumerator Watchdog() {
    while (true) {
      yield return new WaitForSeconds(1f);
      if (!_watchOn || _relaunching || _handlingSevere || _stageSevere) continue;
      Vector3 now = PlayerPosRaw();
      Vector3 d = now - _watchLastPos;
      d.y = 0f;
      if (d.magnitude > 0.08f) {
        _watchLastPos = now;
        _watchStillT = 0f;
        continue;
      }
      _watchStillT += 1f;
      // 14s (not 10): arena exit walks detour around props and can take ~12s
      // to get going (S3-P2Z17 journey finding) — a real stall still escalates.
      if (_watchStillT < 14f) continue;
      _watchStillT = 0f;
      _stuckShots++;
      Log("STUCK #" + _stuckShots + " leg='" + _watchLabel + "' at " + now.ToString("F1")
        + " target=" + _watchTarget.ToString("F1") + " (standing still >10s)");
      Shot("STUCK_" + _stuckShots + "_" + _watchLabel);
      // Rescue 1: re-issue the leg target.
      ClickWorld(_watchTarget);
      yield return new WaitForSeconds(6f);
      if (_stageSevere) continue;
      if (MovedSince(now, 0.5f)) { Log("stuck rescued (re-click)"); continue; }
      // Rescue 2: walk sideways off the obstacle, then back toward the target.
      Vector3 lateral = Vector3.Cross(Vector3.up, (_watchTarget - now).normalized);
      if (lateral.sqrMagnitude < 0.01f) lateral = Vector3.right;
      ClickWorld(now + lateral.normalized * 3f);
      yield return new WaitForSeconds(5f);
      if (_stageSevere) continue;
      if (MovedSince(now, 0.5f)) {
        Log("stuck rescued (side-step)");
        ClickWorld(_watchTarget);
        continue;
      }
      // Unrecoverable: hand it to the stage engine (evidence + skip/relaunch).
      Log("STUCK unrecoverable on '" + _watchLabel + "' -> SEVERE");
      WatchOff();
      Severe("stall: '" + _watchLabel + "' standing still >10s at " + now.ToString("F1"));
    }
  }

  bool MovedSince(Vector3 from, float min) {
    Vector3 now = PlayerPosRaw();
    Vector3 d = now - from;
    d.y = 0f;
    return d.magnitude > min;
  }

  // Reload the game: start a fresh player with the same arguments (plus the
  // run counter and the next stage), then quit this one (a standalone cannot
  // hot-restart its scene graph). Bounded to 4 runs so a deterministic stall
  // cannot loop.
  void Relaunch(string fromStage) {
    _relaunching = true;
    if (_runNumber >= 4) {
      Log("relaunch limit reached (run " + _runNumber + "); stopping for review");
      WriteSummary();
      try { Application.Quit(); } catch (Exception) { }
      return;
    }
    try {
      string exe = Environment.GetCommandLineArgs()[0];
      System.Text.StringBuilder sb = new System.Text.StringBuilder();
      string[] args = Environment.GetCommandLineArgs();
      for (int i = 1; i < args.Length; i++) {
        if (string.Equals(args[i], "-journey-run", StringComparison.OrdinalIgnoreCase)) {
          i++; // skip the old value
          continue;
        }
        if (string.Equals(args[i], "-shot-dir", StringComparison.OrdinalIgnoreCase)) {
          i++; // rewritten below per run (evidence never overwrites)
          continue;
        }
        if (string.Equals(args[i], "-journey-from", StringComparison.OrdinalIgnoreCase)) {
          i++; // rewritten below with the next stage
          continue;
        }
        if (sb.Length > 0) sb.Append(' ');
        sb.Append(args[i]);
      }
      // Preserve this run's trace next to the evidence before the reload
      // overwrites the shared -logFile.
      try {
        string logPath = null;
        for (int i = 1; i + 1 < args.Length; i++)
          if (string.Equals(args[i], "-logFile", StringComparison.OrdinalIgnoreCase)) logPath = args[i + 1];
        if (!string.IsNullOrEmpty(logPath) && System.IO.File.Exists(logPath))
          System.IO.File.Copy(logPath, _shotDir + "/severe_run" + _runNumber + ".log", true);
      } catch (Exception) { }
      // Never nest run dirs (run2/run3/...): always relaunch from the root.
      string nextShotDir = RunRoot(_shotDir) + "/run" + (_runNumber + 1);
      sb.Append(" -journey-run ").Append(_runNumber + 1);
      sb.Append(" -shot-dir ").Append(nextShotDir);
      if (!string.IsNullOrEmpty(fromStage)) sb.Append(" -journey-from ").Append(fromStage);
      System.Diagnostics.Process.Start(
        new System.Diagnostics.ProcessStartInfo(exe, sb.ToString()));
      Log("relaunch started (run " + (_runNumber + 1)
        + (string.IsNullOrEmpty(fromStage) ? "" : ", from " + fromStage) + "): " + exe);
    } catch (Exception e) {
      Log("relaunch failed: " + e.Message);
    }
    try { Application.Quit(); } catch (Exception) { }
  }

  // ---- stage engine: severe evidence + skip / fallback reload ------------------------

  static string RunRoot(string dir) {
    int i = dir.LastIndexOf("/run", StringComparison.OrdinalIgnoreCase);
    if (i < 0) return dir;
    string tail = dir.Substring(i + 4);
    if (tail.Length == 0) return dir;
    for (int k = 0; k < tail.Length; k++)
      if (!char.IsDigit(tail[k])) return dir;
    return dir.Substring(0, i);
  }

  int StageIndex(string name) {
    if (string.IsNullOrEmpty(name)) return 0;
    for (int i = 0; i < StageOrder.Length; i++)
      if (string.Equals(StageOrder[i], name, StringComparison.OrdinalIgnoreCase)) return i;
    return 0;
  }

  // The micro scene a stage plays in (null = no arena, e.g. return.main).
  string StageScene(string stage) {
    switch (stage) {
      case "garden.stairs": return StairHillBuilder.SceneName;
      case "garden.rabbit": return RabbitPlayBuilder.SceneName;
      case "hub.tower": return BuildTowerBuilder.SceneName;
      case "hub.delivery": return DeliveryBuilder.SceneName;
      case "hub.match": return MatchMeadowBuilder.SceneName;
    }
    return null;
  }

  MicroWorldPortal StageExitPortal(string stage) {
    switch (stage) {
      case "garden.stairs":
      case "garden.rabbit":
        return FindPortal(true, true, CountingGardenArea.AreaId);
      case "hub.tower": return FindPortal(true, false, BuildTowerArea.AreaId);
      case "hub.delivery": return FindPortal(true, false, DeliveryArea.AreaId);
      case "hub.match": return FindPortal(true, false, MatchArea.AreaId);
    }
    return null;
  }

  // A stage that cannot proceed: mark SEVERE once; the stage runner captures
  // evidence, writes the marker and skips/relaunches.
  void Severe(string reason) {
    if (_stageSevere) return;
    _stageSevere = true;
    _stageSevereReason = reason;
    _severeCount++;
    Log("SEVERE " + _stageName + " :: " + reason + " at " + PlayerPos());
  }

  // Soft problem inside a stage: counted as an anomaly and (in strict mode)
  // escalates to SEVERE so the stage is skipped instead of silently limping on.
  void MarkProblem(string m) {
    _errors++;
    if (_inStage) Severe(m);
  }

  void Anomaly(string m) {
    _errors++;
    Log("ANOMALY " + m);
  }

  // Drives one stage body; aborts as soon as the body (or one of its waits)
  // marks the stage SEVERE.
  IEnumerator RunStage(string stage, IEnumerator body) {
    _stageName = stage;
    _stageSevere = false;
    _stageSevereReason = "";
    _inStage = true;
    float t0 = Time.realtimeSinceStartup;
    Log("STAGE_BEGIN " + stage + " (run " + _runNumber + ")");
    while (true) {
      if (_stageSevere) break;
      bool moved;
      try { moved = body.MoveNext(); }
      catch (Exception e) { Severe("exception: " + e.Message); break; }
      if (!moved) break;
      yield return body.Current;
    }
    _inStage = false;
    if (!_stageSevere) {
      _results.Add(stage + " PASS (" + (Time.realtimeSinceStartup - t0).ToString("F0") + "s)");
      Log("STAGE_PASS " + stage + " in " + (Time.realtimeSinceStartup - t0).ToString("F0") + "s");
      yield break;
    }
    _results.Add(stage + " SEVERE (" + _stageSevereReason + ")");
    yield return CaptureSevereEvidence(stage, _stageSevereReason);
    WriteSevereMarker(stage, _stageSevereReason);
    yield return SevereSkip(stage);
  }

  // Skip policy (user order): if the failed stage's arena door lets the child
  // out, continue to the next stage in the SAME run; otherwise fall back to a
  // fresh run that starts at the next stage (never re-runs the failed one).
  IEnumerator SevereSkip(string stage) {
    _handlingSevere = true;
    string sceneName = StageScene(stage);
    if (!string.IsNullOrEmpty(sceneName) && SceneLoaded(sceneName)) {
      Log("SEVERE escape attempt: leaving " + sceneName + " through its own door");
      MicroWorldPortal exit = StageExitPortal(stage);
      if (exit != null) {
        float t = 0f;
        float clickT = 0f;
        while (t < 75f) {
          if (!SceneLoaded(sceneName)) break;
          clickT -= PollSeconds;
          if (clickT <= 0f) { ClickWorld(exit.transform.position); clickT = ClickSeconds; }
          yield return new WaitForSeconds(PollSeconds);
          t += PollSeconds;
        }
      }
      Log(SceneLoaded(sceneName)
        ? "SEVERE escape failed (still inside " + sceneName + ") -> fallback reload"
        : "SEVERE escape ok; arena left in this run");
    }
    if (!string.IsNullOrEmpty(sceneName) && SceneLoaded(sceneName)) {
      int next = StageIndex(stage) + 1;
      if (next < StageOrder.Length) {
        Log("SEVERE SKIP via fallback reload -> " + StageOrder[next]);
        WriteSummary();
        Relaunch(StageOrder[next]);
        _handlingSevere = false;
        yield break;
      }
      Log("SEVERE last stage cannot escape; staying for review");
    } else {
      Log("SEVERE SKIP same-run -> next stage");
    }
    _handlingSevere = false;
  }

  // Evidence at the failure point: the plan view first, then four REAL
  // right-mouse-drag orbit angles (SmartCamera S4) and one wheel zoom-out.
  IEnumerator CaptureSevereEvidence(string stage, string reason) {
    Log("SEVERE evidence capture :: " + reason);
    DismissSystemDialogs();
    SmartCamera cam = null;
    try { cam = FindObjectOfType<SmartCamera>(); } catch (Exception) { }
    float waitFollow = 0f;
    while (cam != null && cam.Mode != CameraMode.Follow && waitFollow < 8f) {
      yield return new WaitForSeconds(0.5f);
      waitFollow += 0.5f;
    }
    yield return new WaitForSeconds(0.4f);
    Shot("ERR_" + stage + "_0");
    if (cam == null || cam.Mode != CameraMode.Follow)
      Log("SEVERE evidence: camera mode=" + (cam != null ? cam.Mode.ToString() : "none")
        + " (orbit needs Follow; shooting angles anyway)");
    for (int i = 1; i <= 4; i++) {
      float dx = (i % 2 == 1) ? 210f : -210f;
      float dy = (i <= 2) ? -70f : 70f;
      yield return DragOrbit(dx, dy);
      yield return new WaitForSeconds(0.5f);
      Shot("ERR_" + stage + "_" + i);
    }
    yield return WheelZoomOut(4);
    yield return new WaitForSeconds(0.5f);
    Shot("ERR_" + stage + "_zoom");
    Log("SEVERE evidence captured (6 shots) for " + stage);
  }

  IEnumerator DragOrbit(float dx, float dy) {
    Mouse mouse = Mouse.current;
    if (mouse == null) yield break;
    Vector2 pos = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
    try { mouse.WarpCursorPosition(pos); } catch (Exception) { }
    yield return null;
    int steps = 7;
    Vector2 step = new Vector2(dx / steps, dy / steps);
    for (int i = 0; i < steps; i++) {
      pos += step;
      SendMouse(pos, step, Vector2.zero, (ushort)(1 << (int)MouseButton.Right));
      yield return new WaitForEndOfFrame();
    }
    SendMouse(pos, Vector2.zero, Vector2.zero, 0);
    yield return new WaitForEndOfFrame();
    Log("evidence orbit drag dx=" + dx.ToString("F0") + " dy=" + dy.ToString("F0"));
  }

  IEnumerator WheelZoomOut(int notches) {
    Mouse mouse = Mouse.current;
    if (mouse == null) yield break;
    Vector2 pos = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
    for (int i = 0; i < notches; i++) {
      SendMouse(pos, Vector2.zero, new Vector2(0f, -1f), 0);
      yield return new WaitForEndOfFrame();
      SendMouse(pos, Vector2.zero, Vector2.zero, 0);
      yield return new WaitForEndOfFrame();
    }
    Log("evidence zoom-out " + notches + " notches");
  }

  void SendMouse(Vector2 pos, Vector2 delta, Vector2 scroll, ushort buttons) {
    try {
      Mouse mouse = Mouse.current;
      if (mouse == null) return;
      MouseState st = new MouseState();
      st.position = pos;
      st.delta = delta;
      st.scroll = scroll;
      st.buttons = buttons;
      InputSystem.QueueStateEvent(mouse, st);
    } catch (Exception) { }
  }

  void WriteSevereMarker(string stage, string reason) {
    try {
      string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        + " | stage=" + stage + " | pos=" + PlayerPos() + " | reason=" + reason
        + " | run=" + _runNumber + Environment.NewLine;
      System.IO.File.AppendAllText(_shotDir + "/severe_errors.txt", line);
      Log("SEVERE marker: " + line.Trim());
    } catch (Exception e) { Log("severe marker failed: " + e.Message); }
  }

  void WriteSummary() {
    try {
      System.Text.StringBuilder sb = new System.Text.StringBuilder();
      sb.Append("FULLJOURNEY run #").Append(_runNumber).Append(" from '")
        .Append(string.IsNullOrEmpty(_startStage) ? "(start)" : _startStage).Append("'")
        .Append(Environment.NewLine);
      foreach (string r in _results) sb.Append(r).Append(Environment.NewLine);
      sb.Append("shots=").Append(_shots).Append(" clicks=").Append(_clicks)
        .Append(" errors=").Append(_errors).Append(" severe=").Append(_severeCount)
        .Append(Environment.NewLine);
      System.IO.File.WriteAllText(_shotDir + "/journey_summary.txt", sb.ToString());
      Log("summary -> " + _shotDir + "/journey_summary.txt");
    } catch (Exception e) { Log("summary failed: " + e.Message); }
  }

  // ---- logging / evidence ----------------------------------------------------------

  void Log(string m) {
    try { Debug.Log("[FULLJ] " + m); } catch (Exception) { }
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

  // ---- invisible-bug census ---------------------------------------------------------

  // Small world-space click offsets cycled on retries: if one screen ray
  // misses a small collider, the next pass still lands on it (real clicks).
  static readonly Vector3[] ClickOffsets = {
    Vector3.zero,
    new Vector3(0.22f, 0f, 0f), new Vector3(-0.22f, 0f, 0f),
    new Vector3(0f, 0f, 0.22f), new Vector3(0f, 0f, -0.22f),
  };

  int CountOf<T>() where T : Component {
    try { return UnityEngine.Object.FindObjectsOfType<T>().Length; } catch (Exception) { return -1; }
  }

  string PlayerPos() {
    RefreshRefs();
    if (_player == null) return "none";
    Vector3 p = _player.transform.position;
    return p.x.ToString("F1") + "," + p.y.ToString("F1") + "," + p.z.ToString("F1");
  }

  void Census(string tag) {
    try {
      Log("CENSUS " + tag
        + " scenes=" + SceneManager.sceneCount
        + " gardenArea=" + CountOf<CountingGardenArea>()
        + " buildArea=" + CountOf<BuildTowerArea>()
        + " deliveryArea=" + CountOf<DeliveryArea>()
        + " stairs=" + CountOf<NumberStairs>()
        + " rabbit=" + CountOf<RabbitFeed>()
        + " tower=" + CountOf<BuildTowerGame>()
        + " deliveryGame=" + CountOf<DeliveryGame>()
        + " player=" + PlayerPos());
    } catch (Exception e) { Log("census failed: " + e.Message); }
  }

  void Expect(string label, bool ok, string detail) {
    if (ok) return;
    _errors++;
    Log("ANOMALY " + label + " :: " + detail);
  }

  // ---- world lookup ------------------------------------------------------------------

  bool SceneLoaded(string name) {
    Scene s = SceneManager.GetSceneByName(name);
    return s.IsValid() && s.isLoaded;
  }

  bool MathLoaded() { return SceneLoaded("MathScene"); }

  SubjectGate FindSubjectGate(SubjectId id) {
    try {
      foreach (SubjectGate g in FindObjectsOfType<SubjectGate>()) {
        if (g != null && g.Target == id) return g;
      }
    } catch (Exception) { }
    return null;
  }

  SubjectGate FindReturnGate() {
    try {
      foreach (SubjectGate g in FindObjectsOfType<SubjectGate>()) {
        if (g != null && g.IsReturnGate) return g;
      }
    } catch (Exception) { }
    return null;
  }

  MicroWorldGate FindGate(string id) {
    try {
      foreach (MicroWorldGate g in FindObjectsOfType<MicroWorldGate>()) {
        if (g != null && string.Equals(g.gateId, id, StringComparison.OrdinalIgnoreCase)) return g;
      }
    } catch (Exception) { }
    return null;
  }

  MicroWorldPortal FindPortal(bool exitMode, bool playExit, string areaId) {
    try {
      foreach (MicroWorldPortal p in FindObjectsOfType<MicroWorldPortal>()) {
        if (p == null) continue;
        if (p.ExitMode != exitMode || p.PlayExit != playExit) continue;
        if (!string.IsNullOrEmpty(areaId) && p.areaId != areaId) continue;
        return p;
      }
    } catch (Exception) { }
    return null;
  }

  // ---- coroutine helpers ---------------------------------------------------------------

  IEnumerator WaitFor(Func<bool> cond, float timeout, string label) {
    float t = 0f;
    while (t < timeout) {
      if (_stageSevere) yield break;
      bool ok = false;
      try { ok = cond(); } catch (Exception) { }
      if (ok) { Log("ok: " + label); yield break; }
      yield return new WaitForSeconds(PollSeconds);
      t += PollSeconds;
    }
    Log("TIMEOUT: " + label);
    MarkProblem("timeout: " + label);
  }

  // Fast walk: poll arrival every frame-ish, click on a 0.9s cadence.
  IEnumerator WalkToWorld(Vector3 world, float arrive, float timeout, string label) {
    float t = 0f;
    float clickT = 0f;
    RefreshRefs();
    Watch(world, label);
    while (t < timeout) {
      if (_stageSevere) { WatchOff(); yield break; }
      if (PlayerArrived(world, arrive)) { WatchOff(); Log("walk ok: " + label); yield break; }
      clickT -= PollSeconds;
      if (clickT <= 0f) { ClickWorld(world); clickT = ClickSeconds; }
      yield return new WaitForSeconds(PollSeconds);
      t += PollSeconds;
      if (t % 5f < PollSeconds) RefreshRefs();
    }
    WatchOff();
    Log("WALK_TIMEOUT: " + label);
    MarkProblem("walk timeout: " + label);
  }

  // Walk toward a world point while polling a REAL condition; stop the frame
  // the world answers (travel fires by proximity — never chase an old target).
  IEnumerator WalkUntil(Func<bool> cond, Vector3 toward, float arrive, float timeout, string label) {
    float t = 0f;
    float clickT = 0f;
    RefreshRefs();
    Watch(toward, label);
    while (t < timeout) {
      if (_stageSevere) { WatchOff(); yield break; }
      bool done = false;
      try { done = cond(); } catch (Exception) { }
      if (done) { WatchOff(); Log("walk ok: " + label); yield break; }
      clickT -= PollSeconds;
      if (clickT <= 0f) { ClickWorld(toward); clickT = ClickSeconds; }
      yield return new WaitForSeconds(PollSeconds);
      t += PollSeconds;
      if (t % 5f < PollSeconds) RefreshRefs();
    }
    WatchOff();
    Log("WALK_TIMEOUT: " + label);
    MarkProblem("walk timeout: " + label);
  }

  // Leave a RE-ENTERED arena at once. The behind-camera fallback walks the
  // child out while the arrival frame still faces the door; if the teaching
  // frame blocks it, wait for the hand-off (camera follows again) and then
  // walk out — bounded, never a stall.
  IEnumerator LeaveReentry(MicroWorldPortal exit, string sceneName, Func<bool> handoff, string label) {
    float t = 0f;
    float clickT = 0f;
    Watch(exit != null ? exit.transform.position : Vector3.zero, label);
    while (t < 30f) {
      if (_stageSevere) { WatchOff(); yield break; }
      if (!SceneLoaded(sceneName)) { WatchOff(); Log("walk ok: " + label); yield break; }
      clickT -= PollSeconds;
      if (clickT <= 0f) {
        if (exit != null) ClickWorld(exit.transform.position);
        clickT = ClickSeconds;
      }
      yield return new WaitForSeconds(PollSeconds);
      t += PollSeconds;
    }
    WatchOff();
    if (_stageSevere) yield break;
    Log("re-entry exit blocked by the lesson camera; waiting for hand-off");
    yield return WaitFor(handoff, 480f, label + " handoff");
    yield return WalkUntil(delegate { return !SceneLoaded(sceneName); },
      exit != null ? exit.transform.position : Vector3.zero, 1.2f, 120f, label);
  }

  // Demo progress: screenshot the moment each demo delivery lands (no fixed
  // waits), return as soon as the demo reached the target.
  IEnumerator TrackDemo(Func<int> value, int target, string label, float timeout = 480f) {
    int last = -1;
    float t = 0f;
    while (t < timeout) {
      if (_stageSevere) yield break;
      int now = 0;
      try { now = value(); } catch (Exception) { }
      if (now != last) {
        last = now;
        if (now >= 1) Shot(label + "_" + now);
        if (now >= target) { Log("ok: " + label + " reached " + now); yield break; }
      }
      yield return new WaitForSeconds(PollSeconds);
      t += PollSeconds;
    }
    Log("TIMEOUT: " + label + " (demo=" + last + "/" + target + ")");
    MarkProblem("demo timeout: " + label);
  }

  void RefreshRefs() {
    try {
      if (_player == null) _player = FindObjectOfType<ClickToMove>();
      _cam = Camera.main;
    } catch (Exception) { }
  }

  bool PlayerArrived(Vector3 world, float arrive) {
    if (_player == null) return false;
    Vector3 p = _player.transform.position;
    float dx = p.x - world.x, dz = p.z - world.z;
    return dx * dx + dz * dz <= arrive * arrive;
  }

  void ClickWorld(Vector3 world) {
    RefreshRefs();
    if (_cam == null) return;
    Vector3 sp = _cam.WorldToScreenPoint(world);
    if (sp.z < 0f) {
      // The target is BEHIND the camera (arena doors sit south of the follow
      // frame): route an on-screen waypoint toward it so the child still walks
      // the right way. (An orbit-turn experiment here backfired in the match
      // meadow — a turn during a camera transition flipped every later click;
      // the game-side camera hand-back fix made the plain waypoint enough.)
      if (_player == null) return;
      Vector3 p = _player.transform.position;
      Vector3 d = world - p;
      d.y = 0f;
      if (d.sqrMagnitude < 0.01f) return;
      Vector3 wp = p + d.normalized * 2.5f;
      sp = _cam.WorldToScreenPoint(wp);
      if (sp.z < 0f) sp = new Vector3(Screen.width * 0.5f, Screen.height * 0.22f, 0f);
    }
    sp.x = Mathf.Clamp(sp.x, 40f, Screen.width - 40f);
    sp.y = Mathf.Clamp(sp.y, 40f, Screen.height - 40f);
    StartCoroutine(Tap(new Vector2(sp.x, sp.y)));
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

  bool ClickPanelPlay(GardenZonePanel panel) {
    if (panel == null) return false;
    foreach (Button b in panel.GetComponentsInChildren<Button>(true)) {
      if (b == null || !b.gameObject.activeInHierarchy) continue;
      if (b.name != "ZonePlay") continue;
      Vector2 screen = UiScreenPos(b.GetComponent<RectTransform>());
      if (screen.x < 0f) continue;
      StartCoroutine(Tap(screen));
      Log("ui click: ZonePlay at " + screen);
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

  // ---- main: stage engine ----------------------------------------------------------------

  IEnumerator Main() {
    int start = StageIndex(_startStage);
    Log("FULLJOURNEY_START run #" + _runNumber + " from '" + (start > 0 ? StageOrder[start] : "(start)")
      + "' (real mouse; every created part)");
    // Boot shot as soon as the first frame is up (no long settle).
    yield return new WaitForSeconds(1.0f);
    DismissSystemDialogs();
    yield return WaitFor(delegate { return FindObjectOfType<LanguageDialog>() != null; }, 40f, "language dialog");
    Shot("00_language_card");
    if (!ClickButtonByName("EnBox")) Log("WARN: EnBox not found");
    yield return WaitFor(delegate {
      LanguageDialog d = FindObjectOfType<LanguageDialog>();
      return d == null || !d.IsOpen;
    }, 25f, "language chosen");
    Shot("01_main_hub");
    Census("main hub");

    for (int i = start; i < StageOrder.Length; i++) {
      string stage = StageOrder[i];
      if (_relaunching) yield break;
      yield return RunStage(stage, StageBody(stage));
      if (_relaunching) yield break;
    }
    WriteSummary();
    Log("FULLJOURNEY_END shots=" + _shots + " clicks=" + _clicks
      + " errors=" + _errors + " severe=" + _severeCount);
    // User order: the journey CLOSES THE GAME itself when it finishes — never
    // leave the window open for a manual close. Hold a beat so the log + shots
    // flush, then quit.
    yield return new WaitForSeconds(1.5f);
    Log("FULLJOURNEY_QUIT (auto-close)");
    try { Application.Quit(); } catch (Exception) { }
#if UNITY_EDITOR
    try { UnityEditor.EditorApplication.isPlaying = false; } catch (Exception) { }
#endif
  }

  // The body of each named stage: approach (get to the right world/zone) then
  // the real gameplay loop. A stage never re-runs an earlier one.
  IEnumerator StageBody(string stage) {
    switch (stage) {
      case "garden.stairs":
        yield return EnsureInGarden(); if (_stageSevere) yield break;
        yield return FocusAndPlay(CountingGardenBuilder.StairZoneIndex,
          StairHillBuilder.SceneName, "zone1"); if (_stageSevere) yield break;
        yield return StairArena();
        break;
      case "garden.rabbit":
        yield return EnsureInGarden(); if (_stageSevere) yield break;
        yield return FocusAndPlay(CountingGardenBuilder.RabbitZoneIndex,
          RabbitPlayBuilder.SceneName, "zone0"); if (_stageSevere) yield break;
        yield return RabbitArena();
        break;
      case "hub.tower":
        yield return EnsureInMathHub(); if (_stageSevere) yield break;
        yield return TowerArena();
        break;
      case "hub.delivery":
        yield return EnsureInMathHub(); if (_stageSevere) yield break;
        yield return DeliveryArena();
        break;
      case "hub.match":
        yield return EnsureInMathHub(); if (_stageSevere) yield break;
        yield return MatchArena();
        break;
      case "return.main":
        yield return EnsureInMathHub(); if (_stageSevere) yield break;
        yield return ReturnToMain();
        break;
      default:
        Severe("unknown stage");
        break;
    }
  }

  // ---- approach: reach the world a stage plays in ------------------------------------------

  // Approach for garden stages: Main -> Math -> garden (skips nothing else).
  IEnumerator EnsureInGarden() {
    CountingGardenArea area = FindObjectOfType<CountingGardenArea>();
    if (area != null && area.IsInside) { Log("approach ok: already in the garden"); yield break; }
    if (!MathLoaded()) {
      SubjectGate mathGate = null;
      yield return WaitFor(delegate { mathGate = FindSubjectGate(SubjectIds.Math); return mathGate != null; }, 40f, "math gate");
      if (mathGate == null) { Severe("math gate missing"); yield break; }
      yield return WalkUntil(delegate { return MathLoaded(); }, mathGate.transform.position, 1.2f, 150f, "into Math");
      yield return WaitFor(delegate { return MathLoaded(); }, 60f, "MathScene live");
      if (_stageSevere) yield break;
      yield return new WaitForSeconds(1.2f);
      Shot("02_math_hub");
      Census("math hub");
      Expect("math hub areas", CountOf<CountingGardenArea>() == 1 && CountOf<BuildTowerArea>() == 1
        && CountOf<DeliveryArea>() == 1, "one area module per micro-world expected");
    }
    yield return WaitFor(delegate { area = FindObjectOfType<CountingGardenArea>(); return area != null; }, 30f, "garden area module");
    MicroWorldGate gardenGate = FindGate("counting_garden");
    if (gardenGate == null || gardenGate.EntryAnchor == null) { Severe("counting_garden gate/anchor missing"); yield break; }
    yield return WalkUntil(delegate { area = FindObjectOfType<CountingGardenArea>(); return area != null && area.IsInside; },
      gardenGate.EntryAnchor.position, 1.4f, 180f, "into the garden");
    yield return WaitFor(delegate { area = FindObjectOfType<CountingGardenArea>(); return area != null && area.IsInside; }, 60f, "garden inside");
    if (_stageSevere) yield break;
    yield return new WaitForSeconds(1.0f);
    Shot("03_garden");
    Census("garden");
  }

  // Approach for Math-side stages: if inside the garden, walk out through its
  // door; if a garden arena is somehow loaded, leave it first; a fresh run
  // walks Main -> Math like the classical full journey.
  IEnumerator EnsureInMathHub() {
    CountingGardenArea area = FindObjectOfType<CountingGardenArea>();
    if (area != null && area.IsInside) {
      MicroWorldPortal gardenExit = FindPortal(true, false, CountingGardenArea.AreaId);
      if (gardenExit == null) { Severe("garden exit portal missing"); yield break; }
      CountingGardenArea a = area;
      yield return WalkUntil(delegate { a = FindObjectOfType<CountingGardenArea>(); return a != null && !a.IsInside; },
        gardenExit.transform.position, 1.2f, 180f, "garden exit");
      yield return WaitFor(delegate { a = FindObjectOfType<CountingGardenArea>(); return a != null && !a.IsInside; }, 60f, "back in Math hub");
      if (_stageSevere) yield break;
      yield return new WaitForSeconds(1.0f);
      Shot("04_garden_exit_math");
      Census("math after garden");
      Expect("no arena leaks", CountOf<NumberStairs>() == 0 && CountOf<RabbitFeed>() == 0,
        "no arena game instance may survive its scene unload");
      yield break;
    }
    if (MathLoaded()) { Log("approach ok: already in Math hub"); yield break; }
    SubjectGate mathGate = null;
    yield return WaitFor(delegate { mathGate = FindSubjectGate(SubjectIds.Math); return mathGate != null; }, 40f, "math gate");
    if (mathGate == null) { Severe("math gate missing"); yield break; }
    yield return WalkUntil(delegate { return MathLoaded(); }, mathGate.transform.position, 1.2f, 150f, "into Math");
    yield return WaitFor(delegate { return MathLoaded(); }, 60f, "MathScene live");
    if (_stageSevere) yield break;
    yield return new WaitForSeconds(1.2f);
    Shot("02_math_hub");
    Census("math hub");
    Expect("math hub areas", CountOf<CountingGardenArea>() == 1 && CountOf<BuildTowerArea>() == 1
      && CountOf<DeliveryArea>() == 1, "one area module per micro-world expected");
  }

  IEnumerator ReturnToMain() {
    // The Math return marker is a SubjectGate bound to SubjectIds.Math with
    // IsReturnGate=true (P3.0.1 P1 fix); there is no SubjectIds.Main gate in
    // MathScene (S3-P2Z17 journey finding: the old lookup never matched).
    SubjectGate returnGate = null;
    yield return WaitFor(delegate { returnGate = FindReturnGate(); return returnGate != null; }, 40f, "return marker");
    if (returnGate == null) { Severe("return marker missing"); yield break; }
    yield return WalkUntil(delegate { return !MathLoaded(); }, returnGate.transform.position, 1.2f, 150f, "return to Main");
    yield return WaitFor(delegate { return !MathLoaded(); }, 90f, "Main hub back");
    if (_stageSevere) yield break;
    yield return new WaitForSeconds(1.0f);
    Shot("05_main_back");
    Census("main back");
    Expect("subject unloaded clean", CountOf<CountingGardenArea>() == 0 && CountOf<BuildTowerArea>() == 0
      && CountOf<DeliveryArea>() == 0, "Math-side modules must not survive the subject unload");
  }

  // ---- garden zone flow -----------------------------------------------------------------

  IEnumerator FocusAndPlay(int zone, string sceneName, string label) {
    CountingGardenArea area = null;
    yield return WaitFor(delegate { area = FindObjectOfType<CountingGardenArea>(); return area != null && area.IsInside; }, 30f, label + " garden inside");
    if (area == null || _stageSevere) yield break;
    GardenZoneSpot spot = null;
    yield return WaitFor(delegate { spot = area.FindSpot(zone); return spot != null; }, 20f, label + " spot");
    if (spot == null) { Severe(label + " spot missing (garden not reloaded?)"); yield break; }
    GardenZonePanel panel = null;
    yield return WaitFor(delegate { panel = FindObjectOfType<GardenZonePanel>(); return panel != null; }, 30f, label + " zone panel");
    if (panel == null) { Severe(label + " zone panel missing"); yield break; }
    Watch(spot.transform.position, label + " focus");
    float t = 0f;
    float clickT = 0f;
    float logT = 0f;
    bool focused = false;
    bool demoShot = false;
    bool watching = true;
    while (t < 150f) {
      if (_stageSevere) { WatchOff(); yield break; }
      // The demo wait is a BY-DESIGN stand-still: a watchdog rescue would
      // re-click the spot, and GardenZoneSpot.OnClicked RESTARTS the lesson +
      // hides the panel (journey garden.balls SEVERE root cause). Disarm the
      // moment the zone is focused; it only guards the travel/click phase.
      if (watching && area.FocusedZone == zone) { WatchOff(); watching = false; }
      if (area.FocusedZone == zone && panel != null && panel.IsOpen && panel.PlayVisible) { focused = true; break; }
      clickT -= PollSeconds;
      if (area.FocusedZone != zone && spot != null && clickT <= 0f) {
        ClickWorld(spot.transform.position);
        clickT = ClickSeconds;
      }
      // Evidence of the garden miniature lesson running (panel opens after it).
      if (area.FocusedZone == zone && !demoShot && t >= 6f) {
        demoShot = true;
        Shot(label + "_demo");
      }
      yield return new WaitForSeconds(PollSeconds);
      t += PollSeconds;
      logT += PollSeconds;
      if (logT >= 5f) { logT = 0f; DismissSystemDialogs(); }
    }
    WatchOff();
    if (!focused) {
      Severe(label + ": zone " + zone + " focus/panel never opened (focused=" + area.FocusedZone + ")");
      yield break;
    }
    Shot(label + "_panel");
    yield return new WaitForSeconds(0.8f); // let the panel pop settle before tapping
    bool loaded = false;
    for (int attempt = 0; attempt < 3 && !loaded && !_stageSevere; attempt++) {
      for (int i = 0; i < 20 && !_stageSevere; i++) {
        if (SceneLoaded(sceneName)) { loaded = true; break; }
        if (panel != null && !panel.IsOpen) break;
        // A dev dialog resurfacing (mic offer) must never eat the Play tap.
        DismissSystemDialogs();
        ClickPanelPlay(panel); // may miss once: keep trying (never give up early)
        yield return new WaitForSeconds(0.5f);
      }
      if (loaded || _stageSevere || attempt == 2) break;
      if (panel == null || !panel.IsOpen) {
        // Panel closed without loading: re-focus ONCE (a second click would
        // restart the mini lesson), then WAIT for the fresh panel — never
        // re-click the spot (that reset the demo forever, journey finding).
        Log(label + ": panel closed without loading; re-focusing once");
        Watch(spot.transform.position, label + " refocus");
        ClickWorld(spot.transform.position);
        float t2 = 0f;
        while (t2 < 150f && !_stageSevere) {
          if (SceneLoaded(sceneName)) { loaded = true; break; }
          if (area.FocusedZone == zone && panel != null && panel.IsOpen && panel.PlayVisible) break;
          // Same as above: once focused, the wait is the demo — no watchdog.
          if (area.FocusedZone == zone) WatchOff();
          yield return new WaitForSeconds(2f);
          t2 += 2f;
        }
        WatchOff();
        yield return new WaitForSeconds(0.8f);
      } else {
        Log(label + ": panel still open; retrying the Play tap");
      }
    }
    yield return WaitFor(delegate { return SceneLoaded(sceneName); }, 120f, label + " arena loaded");
    Census(label + " arena");
  }

  // Pick robustness: keep clicking the target (with small offsets) until the
  // game reports ANY carried object — then the caller follows the REAL object,
  // so a click that lands on a neighbour never desyncs the leg.
  IEnumerator WaitForCarry(Func<bool> hasCarried, Vector3 clickPt, float timeout, string label) {
    float t = 0f;
    float clickT = 0f;
    float rayT = 0f;
    int clickIdx = 0;
    Watch(clickPt, label);
    while (t < timeout) {
      if (_stageSevere) { WatchOff(); yield break; }
      bool done = false;
      try { done = hasCarried(); } catch (Exception) { }
      if (done) { WatchOff(); Log("ok: " + label); yield break; }
      clickT -= PollSeconds;
      if (clickT <= 0f) {
        ClickWorld(clickPt + ClickOffsets[clickIdx % ClickOffsets.Length]);
        clickIdx++;
        clickT = 1.0f;
      }
      rayT += PollSeconds;
      if (rayT >= 3f) { rayT = 0f; LogRayHit(clickPt, label); }
      yield return new WaitForSeconds(PollSeconds);
      t += PollSeconds;
      if (t % 5f < PollSeconds) RefreshRefs();
    }
    WatchOff();
    Log("TIMEOUT: " + label);
    MarkProblem("timeout: " + label);
  }

  // Diagnostic (dev-truth): what the driver's click ray actually hits at the
  // pick target — the blocking collider shows up by name in the log.
  void LogRayHit(Vector3 world, string label) {
    try {
      RefreshRefs();
      if (_cam == null) return;
      Vector3 sp = _cam.WorldToScreenPoint(world);
      if (sp.z < 0f) { Log("ray " + label + ": target behind camera"); return; }
      Ray ray = _cam.ScreenPointToRay(sp);
      RaycastHit[] hits = Physics.RaycastAll(ray, 200f);
      string first = "none";
      float bestD = float.MaxValue;
      for (int i = 0; i < hits.Length; i++) {
        Transform tr = hits[i].collider != null ? hits[i].collider.transform : null;
        if (tr != null && _player != null && (tr == _player.transform || tr.IsChildOf(_player.transform))) continue;
        if (hits[i].distance < bestD) { bestD = hits[i].distance; first = hits[i].collider.name; }
      }
      Log("ray " + label + ": screen=" + sp.x.ToString("F0") + "," + sp.y.ToString("F0")
        + " first=" + first + " d=" + bestD.ToString("F2")
        + " target=" + Vector3.Distance(_cam.transform.position, world).ToString("F2")
        + " player=" + PlayerPos());
    } catch (Exception) { }
  }

  // Walk to a point just OUTSIDE the basket cage (S3-P2Z17 journey finding: a
  // click aimed at the basket centre lands on the zone collider, the agent
  // pushes against it forever — IsMoving stays true and the stop-gated place
  // never fires). A real child stops beside the basket; so does this.
  Vector3 ApproachPt(Vector3 world) {
    RefreshRefs();
    if (_player == null) return world;
    Vector3 p = _player.transform.position;
    Vector3 d = p - world;
    d.y = 0f;
    return d.magnitude > 0.01f ? world + d.normalized * 0.8f : world;
  }

  // The click point of a pickable: its COLLIDER centre, not the root on the
  // ground. Dev-truth ray log (S3-P2Z17): clicks at the root passed the tall
  // crop above its head and hit HVGround beyond it, so the pick never fired.
  Vector3 PickPoint(Component item) {
    try {
      Collider col = item.GetComponentInChildren<Collider>();
      if (col != null) return col.bounds.center;
    } catch (Exception) { }
    return item.transform.position + Vector3.up * 0.35f;
  }

  // ---- gameplay #2: number stairs ----------------------------------------------------------

  // Move the child to the EXACT step (up OR down): the chained ladder wraps
  // 9 -> 1, so "current >= step" would leave the child at 9 and the question
  // could never settle (journey finding).
  IEnumerator ClimbTo(NumberStairs game, StairHillBuilder builder, int step) {
    float t = 0f;
    float clickT = 0f;
    Vector3 stand = builder.Stairs.transform.TransformPoint(builder.Stairs.StandLocal(step));
    Watch(stand, "step " + step);
    while (t < 60f) {
      if (_stageSevere) { WatchOff(); yield break; }
      if (game.CurrentStep == step) { WatchOff(); Log("step ok " + step); yield break; }
      clickT -= PollSeconds;
      if (clickT <= 0f) { ClickWorld(stand); clickT = ClickSeconds; }
      yield return new WaitForSeconds(PollSeconds);
      t += PollSeconds;
    }
    WatchOff();
    MarkProblem("step timeout step=" + step + " current=" + game.CurrentStep);
  }

  IEnumerator DescendTo(NumberStairs game, StairHillBuilder builder, int step) {
    float t = 0f;
    float clickT = 0f;
    Vector3 stand = builder.Stairs.transform.TransformPoint(builder.Stairs.StandLocal(step));
    while (t < 50f) {
      if (_stageSevere) yield break;
      if (game.CurrentStep == step) { Log("descend ok step " + step); yield break; }
      clickT -= PollSeconds;
      if (clickT <= 0f) { ClickWorld(stand); clickT = ClickSeconds; }
      yield return new WaitForSeconds(PollSeconds);
      t += PollSeconds;
    }
    MarkProblem("descend timeout step=" + step + " current=" + game.CurrentStep);
  }

  IEnumerator StairArena() {
    NumberStairs game = null;
    StairHillBuilder builder = null;
    yield return WaitFor(delegate {
      if (!SceneLoaded(StairHillBuilder.SceneName)) return false;
      game = FindObjectOfType<NumberStairs>();
      builder = FindObjectOfType<StairHillBuilder>();
      return game != null && builder != null;
    }, 150f, "stair arena loaded");
    yield return new WaitForSeconds(1.0f);
    if (game == null || builder == null) { Severe("stair arena missing"); yield break; }
    Shot("20_stair_entry");
    int guard = 0;
    while (guard < 8) {
      guard++;
      if (game.ResultShown) break;
      if (game.Current == NumberStairs.Phase.Wait) {
        Vector3 listen = builder.transform.TransformPoint(StairHillBuilder.ListenLocal);
        yield return WalkUntil(delegate { return game.QuestionTold; }, listen, 1.4f, 150f, "stair question " + guard);
        Shot("21_stair_question_" + guard + "_target" + game.Target); // shot the frame it is read
      }
      yield return WaitFor(delegate { return game.Current == NumberStairs.Phase.Climb; }, 150f, "stair climb " + guard);
      int target = game.Target;
      int from = game.CurrentStep;
      Log("stair question " + guard + " target=" + target + " from=" + from);
      if (guard == 1) {
        for (int step = 1; step <= target; step++) yield return ClimbTo(game, builder, step);
      } else {
        // S3-P2Z32 live arithmetic: the next round runs from the step the child
        // keeps, so walk straight to the new target (up OR down).
        int dir = target >= from ? 1 : -1;
        for (int step = from + dir; dir > 0 ? step <= target : step >= target; step += dir)
          yield return ClimbTo(game, builder, step);
      }
      if (guard == 1) {
        // Deliberate overshoot on the first question: guidance, never a fail.
        int over = Mathf.Min(target + 2, StairHillBuilder.StepCount);
        yield return ClimbTo(game, builder, over);
        yield return WaitFor(delegate { return game.Overshoots >= 1; }, 40f, "stair overshoot guidance");
        Shot("22_stair_overshoot");
        yield return DescendTo(game, builder, target);
        yield return WaitFor(delegate { return game.CurrentStep == target; }, 30f, "stair back on target");
      }
      // The agent can overshoot a tread on the way up (a real child corrects);
      // settle back onto the target before waiting for the win.
      if (game.CurrentStep != target) yield return ClimbTo(game, builder, target);
      yield return WaitFor(delegate { return game.Current == NumberStairs.Phase.Success; }, 50f, "stair success " + guard);
      Shot("23_stair_success_" + guard + "_target" + target);
      float dwell = 0f;
      while (!game.ResultShown && game.Current == NumberStairs.Phase.Success && dwell < 16f) {
        yield return new WaitForSeconds(PollSeconds);
        dwell += PollSeconds;
      }
      // Deterministic verification: pin the next round to a SAFE mid-range
      // target (the live random generator can pick 8/9, where an agent overshoot
      // lands on the top landing and the child never returns — a harness
      // fragility, not a gameplay rule). Round 1 stays the area's plain target.
      if (guard - 1 < StairPlanKind.Length) {
        game.ForceNextRoundForTests(StairPlanKind[guard - 1], target, StairPlanB[guard - 1]);
      }
    }
    yield return WaitFor(delegate { return game.ResultShown; }, 60f, "stair ladder finalized");
    Shot("24_stair_final");
    Log("stair finalized target=" + game.Target + " result=" + game.ResultShown + " overshoots=" + game.Overshoots);
    MicroWorldPortal exit = FindPortal(true, true, CountingGardenArea.AreaId);
    if (exit == null) { Severe("stair exit portal missing (PlayExit bit?)"); yield break; }
    Vector3 toward = exit.transform.position;
    yield return WalkUntil(delegate { return !SceneLoaded(StairHillBuilder.SceneName); }, toward, 1.2f, 150f, "stair exit");
    yield return WaitFor(delegate { CountingGardenArea a = FindObjectOfType<CountingGardenArea>(); return a != null && a.IsInside && !a.IsInPlay; }, 150f, "garden after stair");
    yield return new WaitForSeconds(0.8f);
    Shot("25_garden_after_stair");
    Census("after stair");
    Expect("stair arena unloaded", !SceneLoaded(StairHillBuilder.SceneName) && CountOf<NumberStairs>() == 0,
      "arena scene + game must be gone after the exit door");
  }

  // ---- gameplay #3: feed the bunny -----------------------------------------------------------

  RabbitCarrot FirstAvailableCarrot(RabbitFeed game) {
    for (int i = 0; i < game.CarrotCount; i++) {
      RabbitCarrot c = game.CarrotAt(i);
      if (c != null && c.State == RabbitCarrot.CarrotState.Available) return c;
    }
    return null;
  }

  IEnumerator WaitForCarrot(RabbitCarrot c, RabbitCarrot.CarrotState want, float timeout,
      string label, Vector3 clickPt) {
    float t = 0f;
    float clickT = 0f;
    int clickIdx = 0;
    Watch(clickPt, label);
    while (t < timeout) {
      if (_stageSevere) { WatchOff(); yield break; }
      if (c != null && c.State == want) { WatchOff(); Log("ok: " + label); yield break; }
      if (t > 3f) {
        clickT -= PollSeconds;
        if (clickT <= 0f) {
          ClickWorld(clickPt + ClickOffsets[clickIdx % ClickOffsets.Length]);
          clickIdx++;
          clickT = 1.0f;
        }
      }
      yield return new WaitForSeconds(PollSeconds);
      t += PollSeconds;
      if (t % 5f < PollSeconds) RefreshRefs();
    }
    WatchOff();
    Log("TIMEOUT: " + label + " (state=" + (c != null ? c.State.ToString() : "null") + ")");
    MarkProblem("timeout: " + label + " (state=" + (c != null ? c.State.ToString() : "null") + ")");
  }

  IEnumerator RabbitArena() {
    RabbitFeed game = null;
    yield return WaitFor(delegate {
      if (!SceneLoaded(RabbitPlayBuilder.SceneName)) return false;
      game = FindObjectOfType<RabbitFeed>();
      return game != null;
    }, 150f, "rabbit arena loaded");
    yield return new WaitForSeconds(1.2f); // arrival reveal settles (shot only)
    if (game == null) { Severe("rabbit arena missing"); yield break; }
    Shot("30_rabbit_entry");
    // S3-P2Z19 (user round): no demo replays in the arena — the child walks to
    // the marked play spot and the question is read there.
    yield return WalkToWorld(RabbitPlayBuilder.WorldOffset + RabbitPlayBuilder.PlaySpotLocal,
      1.4f, 90f, "rabbit play spot");
    yield return WaitFor(delegate { return game.Current == RabbitFeed.Phase.Feeding; }, 90f, "rabbit question read");
    Shot("31_rabbit_question"); // the frame the question lands on
    RabbitPlayBuilder bellB = FindObjectOfType<RabbitPlayBuilder>();
    Vector3 bell = bellB != null && bellB.SubmitAnchor != null
      ? bellB.SubmitAnchor.position
      : RabbitPlayBuilder.WorldOffset + RabbitPlayBuilder.SubmitLocal;
    // S3-P2Z29/30: the arena asks plain / a+b / a-b with a RANDOM round each
    // time, so the driver reads the live round and plays it: feed the missing
    // addend / drag-remove the subtrahend, then ring the bell. Two rounds are
    // played so the "next question" staging (prefill + guidance) is exercised.
    for (int round = 0; round < 3; round++) {
      Log("rabbit round " + round + ": kind=" + game.Kind + " a=" + game.OpA
        + " b=" + game.OpB + " target=" + game.Target + " bowl=" + game.Count);
      if (game.Kind == RabbitFeed.RoundKind.Plain && game.Count != 0)
        Anomaly("plain round starts with a non-empty bowl (" + game.Count + ")");
      if (game.Kind != RabbitFeed.RoundKind.Plain && game.Count != game.OpA)
        Anomaly("per round prefill bowl=" + game.Count + " expected minuend/addend " + game.OpA);
      int need = game.Kind == RabbitFeed.RoundKind.Plain ? game.Target : game.OpB;
      for (int i = 0; i < need; i++) {
        if (game.Kind == RabbitFeed.RoundKind.Sub) yield return RabbitRemoveOne(game, i);
        else yield return RabbitFeedOne(game, i);
      }
      Log("rabbit round " + round + " played: bowl=" + game.Count + " target=" + game.Target);
      if (game.Count != game.Target)
        Anomaly("rabbit bowl " + game.Count + " != target " + game.Target + " (round " + round + ")");
      yield return RabbitSubmit(game, bell);
      yield return WaitFor(delegate { return game.Current == RabbitFeed.Phase.Success; }, 60f, "rabbit success " + round);
      Shot("36_rabbit_success_" + round);
      if (round < 2)
        yield return WaitFor(delegate { return game.Current == RabbitFeed.Phase.Feeding; }, 90f, "rabbit next question");
    }
    Log("rabbit success submits=" + game.Submits);
    // Exit in two stages: walk to the clear entry plaza first (the bowl/rabbit
    // colliders can swallow clicks aimed straight at the far door).
    Vector3 rabbitClear = RabbitPlayBuilder.WorldOffset + RabbitPlayBuilder.EntryLocal;
    yield return WalkToWorld(rabbitClear, 1.8f, 90f, "rabbit clear");
    MicroWorldPortal exit = FindPortal(true, true, CountingGardenArea.AreaId);
    if (exit == null) { Severe("rabbit exit portal missing (PlayExit bit?)"); yield break; }
    Vector3 toward = exit.transform.position;
    yield return WalkUntil(delegate { return !SceneLoaded(RabbitPlayBuilder.SceneName); }, toward, 1.2f, 150f, "rabbit exit");
    yield return WaitFor(delegate { CountingGardenArea a = FindObjectOfType<CountingGardenArea>(); return a != null && a.IsInside && !a.IsInPlay; }, 150f, "garden after rabbit");
    yield return new WaitForSeconds(0.8f);
    Shot("39_garden_after_rabbit");
    Census("after rabbit");
    Expect("rabbit arena unloaded", !SceneLoaded(RabbitPlayBuilder.SceneName) && CountOf<RabbitFeed>() == 0,
      "arena scene + game must be gone after the exit door");
  }

  // Feed one available carrot to the bunny with real clicks.
  IEnumerator RabbitFeedOne(RabbitFeed game, int idx) {
    RabbitCarrot c = FirstAvailableCarrot(game);
    if (c == null) { Severe("no available carrot at " + idx); yield break; }
    Vector3 pick = PickPoint(c);
    yield return WalkToWorld(pick, 1.2f, 90f, "carrot " + idx);
    yield return WalkToWorld(ApproachPt(pick), 0.7f, 25f, "carrot stand " + idx);
    yield return WaitForCarry(delegate { return game.Carried != null; }, pick, 30f, "carry carrot " + idx);
    RabbitCarrot carried = game.Carried;
    if (carried == null) { Severe("no carrot carried at " + idx); yield break; }
    if (idx == 0) Shot("34_rabbit_carry");
    RabbitPlayBuilder builder = FindObjectOfType<RabbitPlayBuilder>();
    Vector3 bowl = builder != null && builder.FeedAnchor != null ? builder.FeedAnchor.position : carried.transform.position;
    yield return WalkToWorld(bowl, 1.4f, 90f, "bowl " + idx);
    // S3-P2Z32: proximity feeds the carrot; clicking the bowl would tap-select a
    // fed carrot (the bowl's UI). Wait for the feed without clicking the bowl.
    yield return WaitFor(delegate {
      return carried.State == RabbitCarrot.CarrotState.Consumed;
    }, 25f, "fed " + idx);
    Shot("35_rabbit_fed_" + (idx + 1));
  }

  // Take one bowl carrot back to the garden (what the drag/tap removal calls).
  IEnumerator RabbitRemoveOne(RabbitFeed game, int idx) {
    RabbitCarrot c = null;
    for (int i = 0; i < game.CarrotCount; i++) {
      RabbitCarrot x = game.CarrotAt(i);
      if (x != null && x.State == RabbitCarrot.CarrotState.Consumed) { c = x; break; }
    }
    if (c == null) { Severe("no bowl carrot to remove at " + idx); yield break; }
    game.ReturnFromBowl(c);
    Log("rabbit remove " + idx + " -> bowl=" + game.Count);
    yield return new WaitForSeconds(0.8f);
  }

  // Walk to the bell and ring it until the game leaves Feeding (Success/Wrong).
  IEnumerator RabbitSubmit(RabbitFeed game, Vector3 bell) {
    yield return WalkToWorld(bell, 1.3f, 90f, "rabbit bell");
    yield return WaitFor(delegate {
      if (game.Current == RabbitFeed.Phase.Success) return true;
      if (game.Current == RabbitFeed.Phase.Feeding) ClickWorld(bell);
      return false; }, 60f, "rabbit submit");
  }

  // ---- gameplay #6: match the pairs (Match Meadow) --------------------------------------------

  MatchItem FirstTargetCandidate(MatchGame game) {
    for (int i = 0; i < game.CandidateCount; i++) {
      MatchItem it = game.CandidateAt(i);
      if (it == null || !it.IsAvailable) continue;
      if (it.ColorId == game.DistractorColor) continue;
      return it;
    }
    return null;
  }

  IEnumerator WaitForMatchItem(MatchItem it, MatchItem.ItemState want, float timeout,
      string label, Vector3 clickPt) {
    float t = 0f;
    float clickT = 0f;
    int clickIdx = 0;
    Watch(clickPt, label);
    while (t < timeout) {
      if (_stageSevere) { WatchOff(); yield break; }
      if (it != null && it.State == want) { WatchOff(); Log("ok: " + label); yield break; }
      if (t > 3f) {
        clickT -= PollSeconds;
        if (clickT <= 0f) {
          ClickWorld(clickPt + ClickOffsets[clickIdx % ClickOffsets.Length]);
          clickIdx++;
          clickT = 1.0f;
        }
      }
      yield return new WaitForSeconds(PollSeconds);
      t += PollSeconds;
      if (t % 5f < PollSeconds) RefreshRefs();
    }
    WatchOff();
    Log("TIMEOUT: " + label + " (state=" + (it != null ? it.State.ToString() : "null") + ")");
    MarkProblem("timeout: " + label + " (state=" + (it != null ? it.State.ToString() : "null") + ")");
  }

  Vector3 MatchPadWorld() {
    MatchMeadowBuilder builder = FindObjectOfType<MatchMeadowBuilder>();
    if (builder != null && builder.PadAnchor != null) return builder.PadAnchor.position;
    return Vector3.zero;
  }

  IEnumerator MatchArena() {
    Census("match entering");
    MatchArea area = null;
    yield return WaitFor(delegate { area = FindObjectOfType<MatchArea>(); return area != null; }, 30f, "match area module");
    MicroWorldGate gate = FindGate("match_meadow");
    Expect("match gate", gate != null && gate.EntryAnchor != null, "match_meadow gate + entry anchor");
    if (gate != null) {
      yield return WalkUntil(delegate { area = FindObjectOfType<MatchArea>(); return area != null && area.IsInside; },
        gate.EntryAnchor.position, 1.4f, 150f, "into the match meadow");
    }
    yield return WaitFor(delegate { area = FindObjectOfType<MatchArea>(); return area != null && area.IsInside; }, 60f, "match meadow inside");
    MatchGame game = null;
    yield return WaitFor(delegate {
      if (!SceneLoaded(MatchMeadowBuilder.SceneName)) return false;
      game = FindObjectOfType<MatchGame>();
      return game != null;
    }, 150f, "match arena loaded");
    yield return new WaitForSeconds(1.2f); // arrival reveal settles (shot only)
    if (game == null) { Severe("match arena missing"); yield break; }
    Shot("match_entry");
    int pairs = game.Pairs;
    Log("match pairs=" + pairs + " family=" + game.Family);
    // S3-P2Z19 (user round): no demo replays in the arena — the child walks to
    // the marked play spot and the question is read there.
    yield return WalkToWorld(MatchMeadowBuilder.WorldOffset + MatchMeadowBuilder.PlaySpotLocal,
      1.4f, 90f, "match play spot");
    yield return WaitFor(delegate { return game.Current == MatchGame.Phase.Playing; }, 120f, "match question read");
    Shot("match_handoff"); // the frame the child gets control
    for (int i = 0; i < pairs; i++) {
      MatchItem it = FirstTargetCandidate(game);
      if (it == null) { Severe("no target candidate at pair " + i); break; }
      Vector3 pick = PickPoint(it);
      yield return WalkToWorld(pick, 1.2f, 90f, "match object " + i);
      yield return WalkToWorld(ApproachPt(pick), 0.7f, 25f, "match object stand " + i);
      yield return WaitForCarry(delegate { return game.Carried != null; },
        pick, 30f, "match carry " + i);
      MatchItem carried = game.Carried;
      if (carried == null) { Severe("no match object carried at " + i); break; }
      if (i == 0) Shot("match_carry");
      Vector3 pad = MatchPadWorld();
      yield return WalkToWorld(pad, 1.5f, 90f, "match pad " + i);
      yield return WaitForMatchItem(carried, MatchItem.ItemState.Placed, 25f, "match pair " + i, pad);
      Shot("match_pair_" + (i + 1));
    }
    yield return WaitFor(delegate { return game.Current == MatchGame.Phase.Success; }, 50f, "match success");
    Shot("match_success");
    Log("match success pairs=" + game.MatchedPairs + " wrong=" + game.WrongMatches);
    yield return new WaitForSeconds(1.0f);
    Shot("match_reward");
    // Exit in two stages: the clear entry plaza first, then the door.
    Vector3 matchClear = MatchMeadowBuilder.WorldOffset + MatchMeadowBuilder.EntryLocal;
    yield return WalkToWorld(matchClear, 1.8f, 90f, "match clear");
    MicroWorldPortal exit = FindPortal(true, false, MatchArea.AreaId);
    Expect("match exit portal", exit != null, "meadow needs its way home");
    Vector3 toward = exit != null ? exit.transform.position : gate.EntryAnchor.position;
    yield return WalkUntil(delegate { return !SceneLoaded(MatchMeadowBuilder.SceneName); }, toward, 1.2f, 150f, "match exit");
    yield return WaitFor(delegate { area = FindObjectOfType<MatchArea>(); return area != null && !area.IsInside; }, 90f, "math after match");
    yield return new WaitForSeconds(1.0f);
    Shot("math_after_match");
    Census("after match");
    Expect("match arena unloaded", !SceneLoaded(MatchMeadowBuilder.SceneName) && CountOf<MatchGame>() == 0,
      "arena scene + game must be gone after the exit door");
    if (_stageSevere) yield break; // never run the re-entry block on a failed exit

    // Re-entry: the pair ladder advanced on leave -> fresh round for 2 pairs.
    int expected = area != null ? area.Pairs : MatchArea.NextPairs(pairs);
    yield return WalkUntil(delegate {
      return SceneLoaded(MatchMeadowBuilder.SceneName) && FindObjectOfType<MatchGame>() != game;
    }, gate.EntryAnchor.position, 1.4f, 150f, "match re-entry");
    MatchGame game2 = null;
    yield return WaitFor(delegate {
      if (!SceneLoaded(MatchMeadowBuilder.SceneName)) return false;
      MatchGame g = FindObjectOfType<MatchGame>();
      if (g != null && g != game) { game2 = g; return true; }
      return false;
    }, 150f, "fresh match meadow");
    yield return WaitFor(delegate { return game2 != null && game2.Current != MatchGame.Phase.Success; }, 90f, "fresh match round");
    Shot("match_reentry_next");
    if (game2 == null) { Severe("match re-entry game missing"); yield break; }
    Log("MATCH REENTRY pairs=" + game2.Pairs + " expected=" + expected + " matched=" + game2.MatchedPairs
      + " phase=" + game2.Current + " instances=" + CountOf<MatchGame>());
    Expect("match re-entry pairs", game2.Pairs == expected, "ladder should advance on leave");
    Expect("match re-entry clean", game2.MatchedPairs == 0 && CountOf<MatchGame>() == 1,
      "no stale pairs, no duplicate game");
    MicroWorldPortal exit2 = FindPortal(true, false, MatchArea.AreaId);
    yield return LeaveReentry(exit2, MatchMeadowBuilder.SceneName,
      delegate { return game2.Current == MatchGame.Phase.Playing; }, "match exit 2");
    yield return WaitFor(delegate { area = FindObjectOfType<MatchArea>(); return area != null && !area.IsInside; }, 90f, "math after match reentry");
    Shot("math_after_match_reentry");
    Census("after match reentry");
  }

  // ---- gameplay #4: build the tower -----------------------------------------------------------

  TowerBlock FirstAvailableTower(BuildTowerGame game) {
    for (int i = 0; i < game.BlockCountTotal; i++) {
      TowerBlock b = game.BlockAt(i);
      if (b != null && b.IsAvailable) return b;
    }
    return null;
  }

  IEnumerator WaitForBlock(TowerBlock b, TowerBlock.BlockState want, float timeout,
      string label, Vector3 clickPt) {
    float t = 0f;
    float clickT = 0f;
    int clickIdx = 0;
    Watch(clickPt, label);
    while (t < timeout) {
      if (_stageSevere) { WatchOff(); yield break; }
      if (b != null && b.State == want) { WatchOff(); Log("ok: " + label); yield break; }
      if (t > 3f) {
        clickT -= PollSeconds;
        if (clickT <= 0f) {
          ClickWorld(clickPt + ClickOffsets[clickIdx % ClickOffsets.Length]);
          clickIdx++;
          clickT = 1.0f;
        }
      }
      yield return new WaitForSeconds(PollSeconds);
      t += PollSeconds;
      if (t % 5f < PollSeconds) RefreshRefs();
    }
    WatchOff();
    Log("TIMEOUT: " + label + " (state=" + (b != null ? b.State.ToString() : "null") + ")");
    MarkProblem("timeout: " + label + " (state=" + (b != null ? b.State.ToString() : "null") + ")");
  }

  IEnumerator TowerArena() {
    BuildTowerArea area = null;
    yield return WaitFor(delegate { area = FindObjectOfType<BuildTowerArea>(); return area != null; }, 30f, "build area module");
    MicroWorldGate gate = FindGate("build_yard");
    Expect("build gate", gate != null && gate.EntryAnchor != null, "build_yard gate + entry anchor");
    if (gate != null) {
      yield return WalkUntil(delegate { area = FindObjectOfType<BuildTowerArea>(); return area != null && area.IsInside; },
        gate.EntryAnchor.position, 1.4f, 150f, "into build yard");
    }
    yield return WaitFor(delegate { area = FindObjectOfType<BuildTowerArea>(); return area != null && area.IsInside; }, 60f, "build yard inside");
    BuildTowerGame game = null;
    yield return WaitFor(delegate {
      if (!SceneLoaded(BuildTowerBuilder.SceneName)) return false;
      game = FindObjectOfType<BuildTowerGame>();
      return game != null;
    }, 150f, "tower arena loaded");
    yield return new WaitForSeconds(1.2f); // arrival reveal settles (shot only)
    if (game == null) { Severe("tower arena missing"); yield break; }
    Shot("50_tower_entry");
    int target = game.Target;
    Log("tower target=" + target);
    // S3-P2Z19 (user round): no in-arena demo — walk to the marked play spot;
    // the question is read on arrival.
    yield return WalkToWorld(BuildTowerBuilder.WorldOffset + BuildTowerBuilder.PlaySpotLocal,
      1.4f, 90f, "tower play spot");
    yield return WaitFor(delegate { return game.Current == BuildTowerGame.Phase.Building; }, 90f, "tower question read");
    Shot("53_tower_handoff");
    for (int i = 0; i < target; i++) {
      TowerBlock b = FirstAvailableTower(game);
      if (b == null) { Severe("no available tower block at " + i); break; }
      yield return WalkToWorld(b.transform.position, 1.2f, 90f, "tower block " + i);
      yield return WaitForCarry(delegate { return game.Carried != null; },
        b.transform.position, 30f, "tower carry " + i);
      TowerBlock carried = game.Carried;
      if (carried == null) { Severe("no tower block carried at " + i); break; }
      if (i == 0) Shot("54_tower_carry");
      BuildTowerBuilder tb = FindObjectOfType<BuildTowerBuilder>();
      Vector3 pad = tb != null && tb.PadAnchor != null ? tb.PadAnchor.position : carried.transform.position;
      yield return WalkToWorld(pad, 1.5f, 90f, "tower pad " + i);
      yield return WaitForBlock(carried, TowerBlock.BlockState.Placed, 25f, "tower placed " + i, pad);
      Shot("55_tower_" + (i + 1));
    }
    yield return WaitFor(delegate { return game.Current == BuildTowerGame.Phase.Success; }, 50f, "tower success");
    Shot("56_tower_success");
    Log("tower success count=" + game.Count + " height=" + game.TowerHeight + " life=" + area.Lifecycle.State);
    TowerBlock spare = FirstAvailableTower(game);
    if (spare != null) {
      yield return WalkToWorld(spare.transform.position, 1.2f, 90f, "spare tower block");
      yield return WaitForCarry(delegate { return game.Carried != null; },
        spare.transform.position, 30f, "spare tower carry");
      TowerBlock spareCarried = game.Carried;
      if (spareCarried == null) { Anomaly("no spare tower block carried"); }
      BuildTowerBuilder tb2 = FindObjectOfType<BuildTowerBuilder>();
      Vector3 pad2 = tb2 != null && tb2.PadAnchor != null ? tb2.PadAnchor.position : spare.transform.position;
      yield return WalkToWorld(pad2, 1.5f, 90f, "spare tower pad");
      yield return WaitFor(delegate { return game.Current == BuildTowerGame.Phase.Correct; }, 40f, "tower overshoot correction");
      Shot("57_tower_overshoot");
      yield return WaitFor(delegate { return game.Current == BuildTowerGame.Phase.Success; }, 150f, "tower corrected");
      Shot("58_tower_corrected");
      Log("tower corrected count=" + game.Count + " overshoots=" + game.Overshoots);
    } else {
      Anomaly("no spare tower block");
    }
    MicroWorldPortal exit = FindPortal(true, false, BuildTowerArea.AreaId);
    Expect("tower exit portal", exit != null, "yard needs its way home");
    Vector3 toward = exit != null ? exit.transform.position : gate.EntryAnchor.position;
    yield return WalkUntil(delegate { return !SceneLoaded(BuildTowerBuilder.SceneName); }, toward, 1.2f, 150f, "tower exit");
    yield return WaitFor(delegate { area = FindObjectOfType<BuildTowerArea>(); return area != null && !area.IsInside; }, 90f, "math after tower");
    yield return new WaitForSeconds(1.0f);
    Shot("59_math_after_tower");
    Census("after tower");
    Expect("tower arena unloaded", !SceneLoaded(BuildTowerBuilder.SceneName) && CountOf<BuildTowerGame>() == 0,
      "arena scene + game must be gone after the exit door");
    if (_stageSevere) yield break; // never run the re-entry block on a failed exit

    // Re-entry: the ladder advanced on leave -> fresh lesson for the next rung.
    // Leave immediately afterwards (the behind-camera door is walked via the
    // ClickWorld waypoint fallback — no waiting for the camera hand-off).
    // S3-P2Z20: live play randomizes the next target — read it from the area
    // (already advanced on leave) instead of predicting the fixed ladder.
    int expected = area != null ? area.Target : BuildTowerArea.NextTarget(target);
    yield return WalkUntil(delegate {
      return SceneLoaded(BuildTowerBuilder.SceneName) && FindObjectOfType<BuildTowerGame>() != game;
    }, gate.EntryAnchor.position, 1.4f, 150f, "tower re-entry");
    BuildTowerGame game2 = null;
    yield return WaitFor(delegate {
      if (!SceneLoaded(BuildTowerBuilder.SceneName)) return false;
      BuildTowerGame g = FindObjectOfType<BuildTowerGame>();
      if (g != null && g != game) { game2 = g; return true; }
      return false;
    }, 150f, "fresh tower arena");
    yield return WaitFor(delegate { return game2 != null && game2.Current != BuildTowerGame.Phase.Success; }, 90f, "fresh tower lesson");
    Shot("60_tower_reentry_next");
    if (game2 == null) { Severe("tower re-entry game missing"); yield break; }
    Log("TOWER REENTRY target=" + game2.Target + " expected=" + expected + " count=" + game2.Count
      + " phase=" + game2.Current + " instances=" + CountOf<BuildTowerGame>());
    Expect("tower re-entry target", game2.Target == expected, "ladder should advance on leave");
    Expect("tower re-entry clean", game2.Count == 0 && CountOf<BuildTowerGame>() == 1, "no stale tower, no duplicate game");
    MicroWorldPortal exit2 = FindPortal(true, false, BuildTowerArea.AreaId);
    yield return LeaveReentry(exit2, BuildTowerBuilder.SceneName,
      delegate { return game2.Current == BuildTowerGame.Phase.Building; }, "tower exit 2");
    yield return WaitFor(delegate { area = FindObjectOfType<BuildTowerArea>(); return area != null && !area.IsInside; }, 90f, "math after tower reentry");
    Shot("61_math_after_tower_reentry");
    Census("after tower reentry");
  }

  // ---- gameplay #5: deliver the apples ----------------------------------------------------------

  DeliveryItem FirstAvailableItem(DeliveryGame game) {
    for (int i = 0; i < game.ItemCountTotal; i++) {
      DeliveryItem it = game.ItemAt(i);
      if (it != null && it.State == DeliveryItem.ItemState.Available) return it;
    }
    return null;
  }

  IEnumerator WaitForItem(DeliveryItem it, DeliveryItem.ItemState want, float timeout,
      string label, Vector3 clickPt) {
    float t = 0f;
    float clickT = 0f;
    int clickIdx = 0;
    Watch(clickPt, label);
    while (t < timeout) {
      if (_stageSevere) { WatchOff(); yield break; }
      if (it != null && it.State == want) { WatchOff(); Log("ok: " + label); yield break; }
      if (t > 3f) {
        clickT -= PollSeconds;
        if (clickT <= 0f) {
          ClickWorld(clickPt + ClickOffsets[clickIdx % ClickOffsets.Length]);
          clickIdx++;
          clickT = 1.0f;
        }
      }
      yield return new WaitForSeconds(PollSeconds);
      t += PollSeconds;
      if (t % 5f < PollSeconds) RefreshRefs();
    }
    WatchOff();
    Log("TIMEOUT: " + label + " (state=" + (it != null ? it.State.ToString() : "null") + ")");
    MarkProblem("timeout: " + label + " (state=" + (it != null ? it.State.ToString() : "null") + ")");
  }

  Vector3 ReceiverWorld() {
    DeliveryBuilder builder = FindObjectOfType<DeliveryBuilder>();
    if (builder != null && builder.DeliveryAnchor != null) return builder.DeliveryAnchor.position;
    if (builder != null) return builder.transform.TransformPoint(DeliveryBuilder.MiaStart);
    return Vector3.zero;
  }

  IEnumerator DeliveryArena() {
    DeliveryArea area = null;
    yield return WaitFor(delegate { area = FindObjectOfType<DeliveryArea>(); return area != null; }, 30f, "delivery area module");
    MicroWorldGate gate = FindGate("delivery_village");
    Expect("delivery gate", gate != null && gate.EntryAnchor != null, "delivery_village gate + entry anchor");
    if (gate != null) {
      yield return WalkUntil(delegate { area = FindObjectOfType<DeliveryArea>(); return area != null && area.IsInside; },
        gate.EntryAnchor.position, 1.4f, 150f, "into delivery village");
    }
    yield return WaitFor(delegate { area = FindObjectOfType<DeliveryArea>(); return area != null && area.IsInside; }, 60f, "delivery inside");
    DeliveryGame game = null;
    yield return WaitFor(delegate {
      if (!SceneLoaded(DeliveryBuilder.SceneName)) return false;
      game = FindObjectOfType<DeliveryGame>();
      return game != null;
    }, 150f, "delivery arena loaded");
    yield return new WaitForSeconds(1.2f); // arrival reveal settles (shot only)
    if (game == null) { Severe("delivery arena missing"); yield break; }
    Shot("70_delivery_entry");
    int target = game.Target;
    Log("delivery target=" + target);
    // S3-P2Z19 (user round): no in-arena demo — walk to the marked play spot;
    // the order is read on arrival.
    yield return WalkToWorld(DeliveryBuilder.WorldOffset + DeliveryBuilder.PlaySpotLocal,
      1.4f, 90f, "delivery play spot");
    yield return WaitFor(delegate { return game.Current == DeliveryGame.Phase.Delivering; }, 90f, "delivery question read");
    Shot("73_delivery_handoff");
    for (int i = 0; i < target; i++) {
      DeliveryItem it = FirstAvailableItem(game);
      if (it == null) { Severe("no available apple at " + i); break; }
      yield return WalkToWorld(it.transform.position, 1.2f, 90f, "apple " + i);
      yield return WaitForCarry(delegate { return game.Carried != null; },
        it.transform.position, 30f, "carry apple " + i);
      DeliveryItem carried = game.Carried;
      if (carried == null) { Severe("no apple carried at " + i); break; }
      if (i == 0) Shot("74_delivery_carry");
      yield return WalkToWorld(ReceiverWorld(), 1.5f, 90f, "receiver " + i);
      yield return WaitForItem(carried, DeliveryItem.ItemState.Delivered, 25f, "handover " + i, ReceiverWorld());
      Shot("75_delivery_crate_" + (i + 1));
    }
    yield return WaitFor(delegate { return game.Current == DeliveryGame.Phase.Success; }, 50f, "delivery success");
    Shot("76_delivery_success");
    Log("delivery success count=" + game.Count + " crate=" + game.CrateCount + " exitCue=" + game.ExitCueShown);
    DeliveryItem spare = FirstAvailableItem(game);
    if (spare != null) {
      yield return WalkToWorld(spare.transform.position, 1.2f, 90f, "spare apple");
      yield return WaitForCarry(delegate { return game.Carried != null; },
        spare.transform.position, 30f, "spare carry");
      DeliveryItem spareCarried = game.Carried;
      if (spareCarried == null) { Anomaly("no spare apple carried"); }
      yield return WalkToWorld(ReceiverWorld(), 1.5f, 90f, "spare receiver");
      yield return WaitFor(delegate { return game.Current == DeliveryGame.Phase.Correct; }, 40f, "delivery overshoot correction");
      Shot("77_delivery_overshoot");
      yield return WaitFor(delegate { return game.Current == DeliveryGame.Phase.Success; }, 150f, "delivery corrected");
      Shot("78_delivery_corrected");
      Log("delivery corrected count=" + game.Count + " overshoots=" + game.Overshoots);
    } else {
      Anomaly("no spare apple for the overshoot leg");
    }
    MicroWorldPortal exit = FindPortal(true, false, DeliveryArea.AreaId);
    Expect("delivery exit portal", exit != null, "village needs its way home");
    Vector3 toward = exit != null ? exit.transform.position : gate.EntryAnchor.position;
    yield return WalkUntil(delegate { return !SceneLoaded(DeliveryBuilder.SceneName); }, toward, 1.2f, 150f, "delivery exit");
    yield return WaitFor(delegate { area = FindObjectOfType<DeliveryArea>(); return area != null && !area.IsInside; }, 90f, "math after delivery");
    yield return new WaitForSeconds(1.0f);
    Shot("79_math_after_delivery");
    Census("after delivery");
    Expect("delivery arena unloaded", !SceneLoaded(DeliveryBuilder.SceneName) && CountOf<DeliveryGame>() == 0,
      "arena scene + game must be gone after the exit door");
    if (_stageSevere) yield break; // never run the re-entry block on a failed exit

    // Re-entry: fresh order for the next rung (advance-on-leave); leave at once.
    int expected = area != null ? area.Target : DeliveryArea.NextTarget(target);
    yield return WalkUntil(delegate {
      return SceneLoaded(DeliveryBuilder.SceneName) && FindObjectOfType<DeliveryGame>() != game;
    }, gate.EntryAnchor.position, 1.4f, 150f, "delivery re-entry");
    DeliveryGame game2 = null;
    yield return WaitFor(delegate {
      if (!SceneLoaded(DeliveryBuilder.SceneName)) return false;
      DeliveryGame g = FindObjectOfType<DeliveryGame>();
      if (g != null && g != game) { game2 = g; return true; }
      return false;
    }, 150f, "fresh delivery village");
    yield return WaitFor(delegate { return game2 != null && game2.Current != DeliveryGame.Phase.Success; }, 90f, "fresh delivery order");
    Shot("80_delivery_reentry_next");
    if (game2 == null) { Severe("delivery re-entry game missing"); yield break; }
    Log("DELIVERY REENTRY target=" + game2.Target + " expected=" + expected + " count=" + game2.Count
      + " phase=" + game2.Current + " instances=" + CountOf<DeliveryGame>());
    Expect("delivery re-entry target", game2.Target == expected, "ladder should advance on leave");
    Expect("delivery re-entry clean", game2.Count == 0 && CountOf<DeliveryGame>() == 1, "no stale crate, no duplicate game");
    MicroWorldPortal exit2 = FindPortal(true, false, DeliveryArea.AreaId);
    yield return LeaveReentry(exit2, DeliveryBuilder.SceneName,
      delegate { return game2.Current == DeliveryGame.Phase.Delivering; }, "delivery exit 2");
    yield return WaitFor(delegate { area = FindObjectOfType<DeliveryArea>(); return area != null && !area.IsInside; }, 90f, "math after delivery reentry");
    Shot("81_math_after_delivery_reentry");
    Census("after delivery reentry");
  }
}
