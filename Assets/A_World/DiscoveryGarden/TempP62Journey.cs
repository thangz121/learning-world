// TempP62Journey.cs — journey driver GAMEPLAY #7 "VƯỜN KHÁM PHÁ" (Discovery
// Garden). Committed with the bundle per the standing order (2026-09-25):
// drop-in path Assets/A_World/DiscoveryGarden/TempP62Journey.cs.
// Inert in production: boots ONLY with the `-journey62` CLI flag (per-driver
// flags convention: -journey55/56/57/62/full are mutually exclusive). Real
// InputSystem mouse injection (queued press held 4 frames), condition-based
// walking, real-state polling only. No teleport, no state pokes.
// Optional: `-shot-dir <path>` picks the evidence folder.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TempP62JourneyBoot {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() {
    string[] args = Environment.GetCommandLineArgs();
    bool want = false;
    foreach (string a in args) {
      if (string.Equals(a, "-journey62", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
    }
    if (!want) return;
    GameObject go = new GameObject("TempP62Journey");
    GameObject.DontDestroyOnLoad(go);
    go.AddComponent<TempP62Journey>();
  }
}

public class TempP62Journey : MonoBehaviour {
  const string DefaultShotDir = "D:/Vscode/p62j-shots";
  string _shotDir = DefaultShotDir;
  int _shots;
  int _clicks;
  int _found;
  int _wrong;
  int _errors;
  Camera _cam;
  ClickToMove _player;

  void Start() {
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
    StartCoroutine(Main());
  }

  void Log(string m) {
    try { Debug.Log("[P62J] " + m); } catch (Exception) { }
  }

  // The dev system dialogs (mic offer / phone-camera HUD) can pop over a shot
  // and eat clicks — hidden before every capture (tooling-only; the same
  // discipline as the full-journey driver).
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

  IEnumerator Main() {
    Log("JOURNEY_START (real mouse, discovery vertical slice)");
    yield return new WaitForSeconds(6f);
    Shot("00_boot");
    // 1. Language card -> English.
    yield return WaitFor(delegate { return FindObjectOfType<LanguageDialog>() != null; }, 30f, "language dialog");
    yield return new WaitForSeconds(1f);
    if (!ClickButtonByName("EnBox")) Log("WARN: EnBox not found");
    yield return new WaitForSeconds(2f);
    Shot("01_language");
    yield return WaitFor(delegate {
      LanguageDialog d = FindObjectOfType<LanguageDialog>();
      return d == null || !d.IsOpen;
    }, 20f, "language chosen");
    // 2. Math gate (walk-in portal).
    SubjectGate mathGate = null;
    yield return WaitFor(delegate {
      SubjectGate[] gates = FindObjectsOfType<SubjectGate>();
      foreach (SubjectGate g in gates) {
        if (g != null && g.Target == SubjectIds.Math) { mathGate = g; return true; }
      }
      return false;
    }, 30f, "math gate found");
    if (mathGate != null) {
      yield return WalkUntil(delegate { return MathLoaded(); }, mathGate.transform.position, 1.2f, 90f, "math gate");
    }
    yield return WaitFor(delegate { return MathLoaded(); }, 30f, "math scene loaded");
    yield return new WaitForSeconds(2.5f);
    Shot("02_math_hub");
    // 3. Discovery gate: walk IN (the portal fires on its own — no UI).
    MicroWorldGate discoveryGate = null;
    yield return WaitFor(delegate {
      MicroWorldGate[] gates = FindObjectsOfType<MicroWorldGate>();
      foreach (MicroWorldGate g in gates) {
        if (g != null && string.Equals(g.gateId, "discovery_garden", StringComparison.OrdinalIgnoreCase)) {
          discoveryGate = g; return true;
        }
      }
      return false;
    }, 30f, "discovery gate found");
    if (discoveryGate != null) yield return WalkToWorld(discoveryGate.transform.position, 3.0f, 120f, "discovery gate");
    yield return new WaitForSeconds(0.5f);
    Shot("03_discovery_gate");
    DiscoveryArea area = null;
    yield return WaitFor(delegate {
      area = FindObjectOfType<DiscoveryArea>();
      return area != null && area.IsInside;
    }, 10f, "discovery garden entered (transition)");
    if (area != null && !area.IsInside) {
      yield return WalkUntil(delegate {
        area = FindObjectOfType<DiscoveryArea>();
        return area != null && area.IsInside;
      }, discoveryGate.transform.position, 0.8f, 60f, "through the gate");
    }
    yield return WaitFor(delegate {
      area = FindObjectOfType<DiscoveryArea>();
      return area != null && area.IsInside;
    }, 60f, "discovery garden entered (transition)");
    Shot("04_transition");
    DiscoveryGame game = null;
    yield return WaitFor(delegate {
      Scene s = SceneManager.GetSceneByName(DiscoveryBuilder.SceneName);
      if (!s.IsValid() || !s.isLoaded) return false;
      game = FindObjectOfType<DiscoveryGame>();
      return game != null;
    }, 120f, "discovery scene loaded");
    yield return new WaitForSeconds(3f);
    Shot("05_world_entry");
    int round = game.Round;
    Log("round=" + round + " (area ladder)");
    // 4. Task + demo search (poll the REAL phases).
    yield return new WaitForSeconds(7f);
    Shot("06_reference_board");
    yield return WaitFor(delegate { return game.Current == DiscoveryGame.Phase.Demo; }, 60f, "demo started");
    yield return new WaitForSeconds(6f);
    Shot("07_demo_search");
    yield return WaitFor(delegate { return game.DemoChecks >= 1; }, 120f, "first demo check");
    yield return WaitFor(delegate { return game.DemoFound; }, 180f, "demo find");
    Shot("08_demo_found");
    yield return WaitFor(delegate { return game.Current == DiscoveryGame.Phase.Tasks; }, 90f, "child control");
    Shot("09_handoff");
    // 5. A wrong candidate first (the gentle lesson), then the three tasks.
    DiscoveryItem wrongProbe = FindItem(game, DiscoveryItem.ItemKind.Ball);
    if (wrongProbe != null) {
      yield return WalkToWorld(wrongProbe.transform.position, 1.2f, 60f, "wrong probe");
      ClickWorld(wrongProbe.transform.position);
      for (int i = 0; i < 12 && game.WrongAttempts == 0; i++) yield return new WaitForSeconds(0.5f);
      _wrong = game.WrongAttempts;
      Shot("10_wrong_gentle");
      Log("wrong attempts=" + _wrong + " (no fail, no reset expected)");
    }
    for (int task = 0; task < 3; task++) {
      DiscoveryItem target = FindItem(game, game.CurrentTaskKind);
      if (target == null) { Log("WARN: no active target for task " + task); _errors++; break; }
      Log("task " + (task + 1) + " target=" + target.Kind);
      yield return WalkToWorld(target.transform.position, 1.2f, 60f, "target " + task);
      yield return WaitForItem(target, DiscoveryItem.ItemState.Found, 25f, "found " + task);
      _found++;
      Shot("11_found_" + _found);
      // The next task line + icon swap needs a beat; wait for the game to move on.
      if (task < 2) {
        DiscoveryItem.ItemKind was = target.Kind;
        yield return WaitFor(delegate {
          return game.Current == DiscoveryGame.Phase.Success || game.CurrentTaskKind != was;
        }, 30f, "next task announced");
        Shot("12_task_" + (task + 2));
      }
    }
    yield return WaitFor(delegate { return game.Current == DiscoveryGame.Phase.Success; }, 40f, "success");
    yield return new WaitForSeconds(3f);
    Shot("13_success");
    Log("SUCCESS found=" + game.FoundCount + " life=" + LifeState(area)
      + " result=" + game.ResultShown + " exitCue=" + game.ExitCueShown);
    // 6. Reward echo, then the real door home.
    yield return new WaitForSeconds(2f);
    Shot("14_reward");
    MicroWorldPortal exit = null;
    foreach (MicroWorldPortal p in FindObjectsOfType<MicroWorldPortal>()) {
      if (p != null && p.ExitMode && p.areaId == DiscoveryArea.AreaId) { exit = p; break; }
    }
    if (exit != null) {
      yield return WalkUntil(delegate {
        Scene s = SceneManager.GetSceneByName(DiscoveryBuilder.SceneName);
        return !s.IsValid() || !s.isLoaded;
      }, exit.transform.position, 1.2f, 90f, "exit door");
    }
    yield return WaitFor(delegate {
      Scene s = SceneManager.GetSceneByName(DiscoveryBuilder.SceneName);
      return !s.IsValid() || !s.isLoaded;
    }, 120f, "garden unloaded (returned to hub)");
    yield return new WaitForSeconds(2f);
    Shot("15_return_hub");
    yield return WaitFor(delegate { return area != null && !area.IsInside; }, 30f, "area back outside");
    // 7. Re-entry -> adopt the finished picture (fresh instance, no replay).
    yield return WalkUntil(delegate {
      Scene s = SceneManager.GetSceneByName(DiscoveryBuilder.SceneName);
      return s.IsValid() && s.isLoaded && FindObjectOfType<DiscoveryGame>() != game;
    }, discoveryGate.transform.position, 0.8f, 90f, "re-enter the gate");
    DiscoveryGame game2 = null;
    yield return WaitFor(delegate {
      Scene s = SceneManager.GetSceneByName(DiscoveryBuilder.SceneName);
      if (!s.IsValid() || !s.isLoaded) return false;
      DiscoveryGame g = FindObjectOfType<DiscoveryGame>();
      if (g != null && g != game) { game2 = g; return true; }
      return false;
    }, 120f, "fresh discovery garden");
    yield return WaitFor(delegate {
      return game2 != null && game2.Current == DiscoveryGame.Phase.Success;
    }, 60f, "adopt completed");
    yield return new WaitForSeconds(2f);
    Shot("16_reentry_adopt");
    Log("ADOPT result=" + game2.ResultShown + " exitCue=" + game2.ExitCueShown
      + " life=" + LifeState(area));
    Log("JOURNEY_END shots=" + _shots + " clicks=" + _clicks + " found=" + _found
      + " wrong=" + _wrong + " errors=" + _errors);
  }

  string LifeState(DiscoveryArea area) {
    try {
      if (area == null || area.Lifecycle == null) return "none";
      return area.Lifecycle.State.ToString();
    } catch (Exception) { return "err"; }
  }

  bool MathLoaded() {
    Scene s = SceneManager.GetSceneByName("MathScene");
    return s.IsValid() && s.isLoaded;
  }

  DiscoveryItem FindItem(DiscoveryGame game, DiscoveryItem.ItemKind kind) {
    for (int i = 0; i < game.ItemCountTotal; i++) {
      DiscoveryItem it = game.ItemAt(i);
      if (it != null && it.Kind == kind && it.gameObject.activeSelf
          && it.State == DiscoveryItem.ItemState.Available) return it;
    }
    return null;
  }

  IEnumerator WaitFor(Func<bool> cond, float timeout, string label) {
    float t = 0f;
    while (t < timeout) {
      bool ok = false;
      try { ok = cond(); } catch (Exception) { }
      if (ok) { Log("ok: " + label); yield break; }
      yield return new WaitForSeconds(0.5f);
      t += 0.5f;
    }
    Log("TIMEOUT: " + label);
    _errors++;
  }

  IEnumerator WaitForItem(DiscoveryItem it, DiscoveryItem.ItemState want, float timeout, string label) {
    float t = 0f;
    while (t < timeout) {
      if (it != null && it.State == want) { Log("ok: " + label); yield break; }
      if (t > 4f && ((int)(t * 2f)) % 4 == 0) ClickWorld(it.transform.position);
      yield return new WaitForSeconds(0.5f);
      t += 0.5f;
    }
    Log("TIMEOUT: " + label + " (state=" + (it != null ? it.State.ToString() : "null") + ")");
    _errors++;
  }

  IEnumerator WalkToWorld(Vector3 world, float arrive, float timeout, string label) {
    float t = 0f;
    RefreshRefs();
    while (t < timeout) {
      if (PlayerArrived(world, arrive)) { Log("walk ok: " + label); yield break; }
      ClickWorld(world);
      yield return new WaitForSeconds(1.2f);
      t += 1.2f;
      if (((int)t) % 12 == 0) RefreshRefs();
    }
    Log("WALK_TIMEOUT: " + label);
    _errors++;
  }

  IEnumerator WalkUntil(Func<bool> cond, Vector3 toward, float arrive, float timeout, string label) {
    float t = 0f;
    RefreshRefs();
    while (t < timeout) {
      bool done = false;
      try { done = cond(); } catch (Exception) { }
      if (done) { Log("walk ok: " + label); yield break; }
      ClickWorld(toward);
      yield return new WaitForSeconds(1.2f);
      t += 1.2f;
      if (((int)t) % 12 == 0) RefreshRefs();
    }
    Log("WALK_TIMEOUT: " + label);
    _errors++;
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
      if (_player == null) return;
      Vector3 p = _player.transform.position;
      Vector3 d = world - p;
      d.y = 0f;
      if (d.sqrMagnitude < 0.01f) return;
      d = d.normalized * 2.5f;
      sp = _cam.WorldToScreenPoint(p + d);
      if (sp.z < 0f) sp = new Vector3(Screen.width * 0.5f, Screen.height * 0.35f, 0f);
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
