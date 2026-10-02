// TempUxJourney.cs — real-click UX screenshot pass over the four new arenas.
// Inert in production: boots ONLY with `-journeyux`.
// Walks A -> Math B -> each skill C -> arena, taps one object, shots the
// spots most likely to hide UX/UI bugs, then exits back to B.
// C# 9.0 only.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TempUxJourneyBoot {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() {
    string[] args = Environment.GetCommandLineArgs();
    bool want = false;
    foreach (string a in args) {
      if (string.Equals(a, "-journeyux", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
    }
    if (!want) return;
    GameObject go = new GameObject("TempUxJourney");
    GameObject.DontDestroyOnLoad(go);
    go.AddComponent<TempUxJourney>();
  }
}

public class TempUxJourney : MonoBehaviour {
  const string DefaultShotDir = "E:/LWW/ux-shots";
  const float StuckMoveEps = 0.35f;
  const float StuckSeconds = 16f;
  const float ArrivedNoProgressSeconds = 12f;
  const float WatchdogSeconds = 1200f;

  // Ga Thứ Tự TẠM ĐÓNG 2026-10-02: rút khỏi journey (mở lại thì thêm
  // "math_order" / "ordering_station" / OrderingStationBuilder.SceneName /
  // "OSPlaySpot" vào cuối 4 mảng dưới).
  static readonly string[] SkillIds = {
    "math_geometry", "math_comparison", "math_classification"
  };
  static readonly string[] GameIds = {
    "shape_builder", "comparison_market", "classification_city"
  };
  static readonly string[] SceneNames = {
    GeometryPlayBuilder.SceneName, ComparisonMarketBuilder.SceneName,
    ClassificationCityBuilder.SceneName
  };
  static readonly string[] SpotNames = {
    "GPPlaySpot", "CMPlaySpot", "CCPlaySpot"
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
    try { Debug.Log("[UXJ] " + m); } catch (Exception) { }
  }

  IEnumerator Watchdog() {
    yield return new WaitForSeconds(WatchdogSeconds);
    Log("WATCHDOG_TIMEOUT elapsed=" + (Time.realtimeSinceStartup - _started).ToString("F0") + "s");
    Quit(2);
  }

  void Quit(int code) {
    try { Debug.Log("[UXJ] UXJ_QUIT code=" + code); } catch (Exception) { }
    try { Application.Quit(code); } catch (Exception) { }
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

  bool Record(string step, bool ok, string detail) {
    _steps++;
    if (ok) {
      _failStreak = 0;
      Log("STEP_OK " + step + (string.IsNullOrEmpty(detail) ? "" : " (" + detail + ")"));
      return true;
    }
    _failStreak++;
    _errors++;
    Log("STEP_FAIL " + step + " streak=" + _failStreak + (string.IsNullOrEmpty(detail) ? "" : " " + detail));
    Shot("fail_" + Sanitize(step));
    if (_failStreak >= 2) {
      _cancel = true;
      Log("JOURNEY_CANCEL: two consecutive step failures (last: " + step + ")");
    }
    return false;
  }

  bool StepGuard() { return !_cancel; }

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

  IEnumerator MainBody() {
    Log("JOURNEY_START (UX shots: four new math arenas)");
    yield return new WaitForSeconds(6f);
    Shot("00_boot");
    yield return WaitStep(delegate { return FindObjectOfType<LanguageDialog>() != null; }, 30f, "language dialog");
    yield return new WaitForSeconds(1f);
    ClickButtonByName("EnBox");
    yield return WaitStep(delegate {
      LanguageDialog d = FindObjectOfType<LanguageDialog>();
      return d == null || !d.IsOpen;
    }, 20f, "language chosen");
    Record("boot + language", _stepOk, "");
    if (!StepGuard()) yield break;
    Shot("01_main_hub");

    yield return WalkIntoSubjectGateStep(SubjectIds.Math, "math", "walk into Math gate");
    Record("A -> B math skill yard", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2.5f);
    Shot("02_math_skill_yard");

    for (int i = 0; i < SkillIds.Length; i++) {
      if (!StepGuard()) yield break;
      yield return PlayOneArena(i);
    }
  }

  IEnumerator PlayOneArena(int i) {
    string skill = SkillIds[i];
    string gameId = GameIds[i];
    string scene = SceneNames[i];
    string spot = SpotNames[i];
    string tag = Sanitize(gameId);

    yield return EnterDoorStep(skill, skill,
      delegate { return YardGame(skill) != null; }, 120f);
    Record(skill + " C yard", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(2f);
    Shot(tag + "_yard");

    yield return EnterDoorStep(gameId, gameId,
      delegate { return SceneLoaded(scene); }, 150f);
    Record(gameId + " arena loads", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(3.2f);
    Shot(tag + "_enter");
    yield return new WaitForSeconds(0.4f);

    Transform playSpot = FindNamed(spot);
    if (playSpot != null) {
      yield return WalkStep(delegate { return ArenaPlaying(); }, playSpot.position, 1.2f, 90f,
        tag + " play spot", null, null);
      Record(tag + " question starts", _stepOk, "");
      if (!StepGuard()) yield break;
    }
    yield return new WaitForSeconds(2f);
    Shot(tag + "_task");

    Transform clickable = FirstArenaClickable();
    if (clickable != null) {
      yield return WalkStep(delegate { return PlayerNear(clickable.position, 1.8f); },
        clickable.position, 1.8f, 50f, tag + " walk to piece", null, null);
      Vector3 sp = _cam != null ? _cam.WorldToScreenPoint(clickable.position) : Vector3.zero;
      if (sp.z > 0f) {
        yield return Tap(new Vector2(sp.x, sp.y));
        yield return new WaitForSeconds(1.2f);
      }
    }
    Shot(tag + "_after_click");

    yield return ExitArenaStep(skill, tag + " exit");
    Record(tag + " back to C", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(1.5f);
    Shot(tag + "_back_c");

    yield return BackStep(delegate { return YardSkill("math") != null; }, 120f, tag + " back B");
    Record(tag + " back to B", _stepOk, "");
    if (!StepGuard()) yield break;
    yield return new WaitForSeconds(1.5f);
  }

  static bool ArenaPlaying() {
    GeometryPlay geo = FindObjectOfType<GeometryPlay>();
    if (geo != null) return geo.Current != GeometryPlay.Phase.Wait;
    ComparisonMarket m = FindObjectOfType<ComparisonMarket>();
    if (m != null) return m.Current != ComparisonMarket.Phase.Wait;
    ClassificationCity c = FindObjectOfType<ClassificationCity>();
    if (c != null) return c.Current != ClassificationCity.Phase.Wait;
    return false;
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
    for (int i = 0; i < all.Length; i++) {
      if (all[i] != null && !all[i].EnvRole && all[i].State == GeometryPiece.PieceState.Idle)
        return all[i];
    }
    return null;
  }

  static ComparisonChoice FirstChoice() {
    ComparisonChoice[] all = FindObjectsOfType<ComparisonChoice>();
    for (int i = 0; i < all.Length; i++) {
      if (all[i] != null && !all[i].IsCart) return all[i];
    }
    return null;
  }

  static ClassificationItem FirstIdleItem() {
    ClassificationItem[] all = FindObjectsOfType<ClassificationItem>();
    for (int i = 0; i < all.Length; i++) {
      if (all[i] != null && all[i].State == ClassificationItem.ItemState.Idle) return all[i];
    }
    return null;
  }

  // Ga Thứ Tự TẠM ĐÓNG 2026-10-02: FirstIdleOrder rút cùng journey.

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
    if (!_stepOk) yield break;
    Vector3 home = SelectionYardBuilder.WorldOffset + SelectionYardBuilder.EntryLocal;
    yield return WaitStep(delegate { return PlayerNear(home, 8f); }, 25f, label + " warped home");
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
        if (g != null && string.Equals(g.TargetId, targetId, StringComparison.OrdinalIgnoreCase)) {
          door = g; return true;
        }
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
    yield return WalkStep(delegate { return PlayerNear(staging, 2.4f); }, staging, 2.4f, 60f,
      label + " staging", null, null);
    if (!_stepOk) yield break;
    yield return WalkStep(done, bp, 1.2f, timeout, label + " walk-in", null, null);
  }

  IEnumerator WalkStep(Func<bool> cond, Vector3 toward, float arrive, float timeout, string label,
      Func<bool> wrong, string allowGateId) {
    _stepOk = false;
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
          Log("STUCK_ERROR " + label + " pos=" + Pos());
          Shot("stuck_" + Sanitize(label));
          yield break;
        }
        if (arrivedStill >= ArrivedNoProgressSeconds) {
          Log("ARRIVED_NO_PROGRESS " + label + " pos=" + Pos());
          Shot("noctx_" + Sanitize(label));
          yield break;
        }
        if (helped) badCycles = 0;
        else {
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
              }
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

  static Transform FindNamed(string name) {
    Transform[] all = FindObjectsOfType<Transform>();
    for (int i = 0; i < all.Length; i++) {
      if (all[i] != null && all[i].name == name) return all[i];
    }
    return null;
  }

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
