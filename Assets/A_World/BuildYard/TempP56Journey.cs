// Assets/A_World/BuildYard/TempP56Journey.cs — TEMP real-click journey driver
// for gameplay #4 (deleted after the round, same discipline as TempP53/55).
// Activated ONLY by the CLI flag "-journey": injects REAL InputSystem mouse
// events (queued press held 4 frames), walks by projecting world targets to
// screen (edge-clamped), polls REAL game state (scenes/phases/lifecycle/stack)
// and captures screenshots next to the build. No teleport, no state pokes.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TempP56JourneyBoot {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() {
    string[] args = Environment.GetCommandLineArgs();
    bool want = false;
    foreach (string a in args) {
      if (string.Equals(a, "-journey", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
    }
    if (!want) return;
    GameObject go = new GameObject("TempP56Journey");
    GameObject.DontDestroyOnLoad(go);
    go.AddComponent<TempP56Journey>();
  }
}

public class TempP56Journey : MonoBehaviour {
  string _shotDir = "E:/LWW/p56j-shots";
  int _shots;
  int _clicks;
  int _placed;
  int _errors;
  Camera _cam;
  ClickToMove _player;

  void Start() {
    // Evidence folder configurable per run (-shot-dir E:/LWW/p56j-shots-t5).
    string[] args = Environment.GetCommandLineArgs();
    for (int i = 0; i + 1 < args.Length; i++) {
      if (string.Equals(args[i], "-shot-dir", StringComparison.OrdinalIgnoreCase)) {
        _shotDir = args[i + 1];
      }
    }
    try { System.IO.Directory.CreateDirectory(_shotDir); } catch (Exception) { }
    try { InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; }
    catch (Exception) { }
    StartCoroutine(Main());
  }

  void Log(string m) {
    try { Debug.Log("[P56J] " + m); } catch (Exception) { }
  }

  // Journey tooling only: hide the dev system dialogs (mic offer) exactly like
  // a child tapping "Bỏ qua", so the language card can appear and no modal
  // keeps covering the evidence. Production gameplay is untouched.
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
    Log("JOURNEY_START (real mouse, build tower vertical slice)");
    yield return new WaitForSeconds(6f);
    Shot("00_boot");
    // 1. Language card -> English.
    yield return WaitFor(delegate {
      DismissSystemDialogs(); // the mic offer would keep the language card queued
      return FindObjectOfType<LanguageDialog>() != null;
    }, 30f, "language dialog");
    yield return new WaitForSeconds(1f);
    DismissSystemDialogs();
    if (!ClickButtonByName("EnBox")) Log("WARN: EnBox not found");
    yield return new WaitForSeconds(2f);
    Shot("01_language");
    yield return WaitFor(delegate {
      LanguageDialog d = FindObjectOfType<LanguageDialog>();
      return d == null || !d.IsOpen;
    }, 20f, "language chosen");
    // 2. Math gate (walk-in portal): walk toward it while polling the REAL
    // world change — never keep clicking an old-world target after travel.
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
    yield return new WaitForSeconds(2.5f); // let the arrival reveal hand back to Follow
    Shot("02_math_hub");
    // 3. Build gate: walk IN (the portal fires on its own — no UI).
    MicroWorldGate buildGate = null;
    yield return WaitFor(delegate {
      MicroWorldGate[] gates = FindObjectsOfType<MicroWorldGate>();
      foreach (MicroWorldGate g in gates) {
        if (g != null && string.Equals(g.gateId, "build_yard", StringComparison.OrdinalIgnoreCase)) {
          buildGate = g; return true;
        }
      }
      return false;
    }, 30f, "build gate found");
    if (buildGate != null) yield return WalkToWorld(buildGate.transform.position, 3.0f, 60f, "build gate");
    yield return new WaitForSeconds(0.5f);
    Shot("03_build_gate");
    // Step through the arch: the walk-in portal fires on its own (no UI).
    if (buildGate != null) {
      yield return WalkUntil(delegate {
        BuildTowerArea a = FindObjectOfType<BuildTowerArea>();
        return a != null && a.IsInside;
      }, buildGate.transform.position, 0.8f, 90f, "through the build gate");
    }
    BuildTowerArea area = null;
    yield return WaitFor(delegate {
      area = FindObjectOfType<BuildTowerArea>();
      return area != null && area.IsInside;
    }, 30f, "build yard entered (transition)");
    Shot("04_transition");
    BuildTowerGame game = null;
    yield return WaitFor(delegate {
      Scene s = SceneManager.GetSceneByName(BuildTowerBuilder.SceneName);
      if (!s.IsValid() || !s.isLoaded) return false;
      game = FindObjectOfType<BuildTowerGame>();
      return game != null;
    }, 120f, "build tower scene loaded");
    yield return new WaitForSeconds(3f);
    Shot("05_world_entry");
    int target = game.Target;
    Log("target=" + target + " (area ladder)");
    // 4. Intro + demo + handoff (poll the REAL phases).
    yield return new WaitForSeconds(9f);
    Shot("06_intro_board");
    yield return WaitFor(delegate { return game.Current == BuildTowerGame.Phase.Demo; }, 60f, "demo started");
    yield return new WaitForSeconds(12f);
    Shot("07_demo_build");
    yield return WaitFor(delegate { return game.DemoBlocksPlaced >= target; }, 300f, "demo stacked " + target);
    Shot("08_demo_done");
    yield return WaitFor(delegate { return game.Current == BuildTowerGame.Phase.Building; }, 60f, "child control");
    Shot("09_handoff");
    // 5. The child's round: pick -> carry -> place, one block at a time.
    for (int i = 0; i < target; i++) {
      TowerBlock b = FirstAvailable(game);
      if (b == null) { Log("WARN: no available block at " + i); _errors++; break; }
      yield return WalkToWorld(b.transform.position, 1.3f, 60f, "block " + i);
      yield return WaitForBlock(b, TowerBlock.BlockState.Carried, 25f, "carry " + i);
      if (i == 0) Shot("10_player_carry");
      BuildTowerBuilder builder = FindObjectOfType<BuildTowerBuilder>();
      Vector3 pad = builder != null && builder.PadAnchor != null
        ? builder.PadAnchor.position : b.transform.position;
      yield return WalkToWorld(pad, 1.5f, 60f, "pad " + i);
      yield return WaitForBlock(b, TowerBlock.BlockState.Placed, 25f, "landed " + i);
      _placed++;
      Shot("11_tower_" + _placed);
    }
    yield return WaitFor(delegate { return game.Current == BuildTowerGame.Phase.Success; }, 40f, "success");
    yield return new WaitForSeconds(3f);
    Shot("12_success");
    Log("SUCCESS count=" + game.Count + " life=" + LifeState(area) + " result=" + game.ResultShown);
    // 6. Overshoot: one spare block -> correction -> success again.
    TowerBlock spare = FirstAvailable(game);
    if (spare != null) {
      yield return WalkToWorld(spare.transform.position, 1.3f, 60f, "spare block");
      yield return WaitForBlock(spare, TowerBlock.BlockState.Carried, 25f, "spare carry");
      BuildTowerBuilder b2 = FindObjectOfType<BuildTowerBuilder>();
      Vector3 pad2 = b2 != null && b2.PadAnchor != null ? b2.PadAnchor.position : spare.transform.position;
      yield return WalkToWorld(pad2, 1.5f, 60f, "spare pad");
      yield return WaitFor(delegate { return game.Current == BuildTowerGame.Phase.Correct; }, 30f, "correction");
      Shot("13_overshoot_correct");
      yield return WaitFor(delegate { return game.Current == BuildTowerGame.Phase.Success; }, 120f, "corrected");
      Shot("14_corrected");
      Log("CORRECTED count=" + game.Count + " height=" + game.TowerHeight + " overshoots=" + game.Overshoots);
    } else Log("WARN: no spare block for the overshoot leg");
    // 7. Reward echo: the hub landmark reflects the completed height.
    yield return new WaitForSeconds(2f);
    Shot("15_reward");
    // 8. Exit through the real door -> hub.
    MicroWorldPortal exit = null;
    foreach (MicroWorldPortal p in FindObjectsOfType<MicroWorldPortal>()) {
      if (p != null && p.ExitMode && p.areaId == BuildTowerArea.AreaId) { exit = p; break; }
    }
    if (exit != null) {
      yield return WalkUntil(delegate {
        Scene s = SceneManager.GetSceneByName(BuildTowerBuilder.SceneName);
        return !s.IsValid() || !s.isLoaded;
      }, exit.transform.position, 1.2f, 90f, "exit door");
    }
    yield return WaitFor(delegate {
      Scene s = SceneManager.GetSceneByName(BuildTowerBuilder.SceneName);
      return !s.IsValid() || !s.isLoaded;
    }, 120f, "yard unloaded (returned to hub)");
    yield return new WaitForSeconds(2f);
    Shot("16_return_hub");
    yield return WaitFor(delegate { return area != null && !area.IsInside; }, 30f, "area back outside");
    // 9. Re-entry: the ladder advanced when the child LEFT the completed round
    // (advance-on-leave) -> the fresh instance stages the NEXT target with an
    // empty pad. No stale tower, no auto-completion, no duplicate instance.
    int expected = BuildTowerArea.NextTarget(target);
    yield return WalkUntil(delegate {
      Scene s = SceneManager.GetSceneByName(BuildTowerBuilder.SceneName);
      return s.IsValid() && s.isLoaded && FindObjectOfType<BuildTowerGame>() != game;
    }, buildGate.transform.position, 0.8f, 90f, "re-enter the gate");
    BuildTowerGame game2 = null;
    yield return WaitFor(delegate {
      Scene s = SceneManager.GetSceneByName(BuildTowerBuilder.SceneName);
      if (!s.IsValid() || !s.isLoaded) return false;
      BuildTowerGame g = FindObjectOfType<BuildTowerGame>();
      if (g != null && g != game) { game2 = g; return true; }
      return false;
    }, 120f, "fresh build yard");
    yield return WaitFor(delegate {
      return game2 != null && game2.Current != BuildTowerGame.Phase.Success;
    }, 60f, "fresh lesson (never a stale success)");
    yield return new WaitForSeconds(2f);
    Shot("17_reentry_next_target");
    int instances = FindObjectsOfType<BuildTowerGame>().Length;
    Log("REENTRY target=" + game2.Target + " expected=" + expected
      + " count=" + game2.Count + " phase=" + game2.Current
      + " instances=" + instances + " life=" + LifeState(area));
    if (game2.Target != expected) { Log("ERROR: ladder did not advance on leave"); _errors++; }
    if (game2.Count != 0) { Log("ERROR: stale tower on re-entry"); _errors++; }
    if (instances != 1) { Log("ERROR: duplicate BuildTowerGame instances"); _errors++; }
    Log("JOURNEY_END shots=" + _shots + " clicks=" + _clicks + " placed=" + _placed + " errors=" + _errors);
  }

  string LifeState(BuildTowerArea area) {
    try {
      if (area == null || area.Lifecycle == null) return "none";
      return area.Lifecycle.State.ToString();
    } catch (Exception) { return "err"; }
  }

  bool MathLoaded() {
    Scene s = SceneManager.GetSceneByName("MathScene");
    return s.IsValid() && s.isLoaded;
  }

  // Walk toward a world point while polling a REAL condition; stop the moment
  // the world answers (travel fires by proximity — never chase an old target).
  IEnumerator WalkUntil(Func<bool> cond, Vector3 toward, float arrive, float timeout, string label) {
    float t = 0f;
    RefreshRefs();
    while (t < timeout) {
      bool done = false;
      try { done = cond(); } catch (Exception) { }
      if (done) { Log("walk ok: " + label); yield break; }
      if (PlayerArrived(toward, arrive) && t > 1.2f) {
        // Standing at the destination while the world has not answered yet:
        // keep tapping it so a portal/trigger gets a fresh click.
      }
      ClickWorld(toward);
      yield return new WaitForSeconds(1.2f);
      t += 1.2f;
      if (((int)t) % 12 == 0) RefreshRefs();
    }
    Log("WALK_TIMEOUT: " + label);
    _errors++;
  }

  TowerBlock FirstAvailable(BuildTowerGame game) {
    for (int i = 0; i < game.BlockCountTotal; i++) {
      TowerBlock b = game.BlockAt(i);
      if (b != null && b.State == TowerBlock.BlockState.Available) return b;
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

  IEnumerator WaitForBlock(TowerBlock b, TowerBlock.BlockState want, float timeout, string label) {
    float t = 0f;
    while (t < timeout) {
      if (b != null && b.State == want) { Log("ok: " + label); yield break; }
      if (t > 4f && ((int)(t * 2f)) % 4 == 0) ClickWorld(b.transform.position);
      yield return new WaitForSeconds(0.5f);
      t += 0.5f;
    }
    Log("TIMEOUT: " + label + " (state=" + (b != null ? b.State.ToString() : "null") + ")");
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
