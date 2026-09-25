// TempP55Journey.cs — TEMP real-click journey driver for gameplay #3
// "CHO THỎ ĂN ĐÚNG SỐ" (feed the bunny). RECREATED on ASUS for maynode (the
// original was deleted from the repo before the game #3 commit). Drop into
// Assets/A_World/CountingGarden/TempP55Journey.cs, build with TempBuildP55,
// run with the -journey flag. Injects REAL InputSystem mouse events (queued
// press held 4 frames), walks by projecting world targets to screen
// (edge-clamped) and polls REAL game state (scenes/phases/states). No
// teleport, no state pokes. Delete after the run (production must be clean).
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TempP55JourneyBoot {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() {
    string[] args = Environment.GetCommandLineArgs();
    bool want = false;
    // Driver-specific flag: both journey drivers are committed, so a plain
    // "-journey" must never boot TWO drivers at once (they fight over clicks).
    foreach (string a in args) {
      if (string.Equals(a, "-journey55", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
    }
    if (!want) return;
    GameObject go = new GameObject("TempP55Journey");
    GameObject.DontDestroyOnLoad(go);
    go.AddComponent<TempP55Journey>();
  }
}

public class TempP55Journey : MonoBehaviour {
  const string ShotDir = "E:/LWW/p55j-shots";
  int _shots;
  int _clicks;
  int _fed;
  int _errors;
  Camera _cam;
  ClickToMove _player;

  void Start() {
    try { System.IO.Directory.CreateDirectory(ShotDir); } catch (Exception) { }
    try { InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; }
    catch (Exception) { }
    StartCoroutine(Main());
  }

  void Log(string m) {
    try { Debug.Log("[P55J] " + m); } catch (Exception) { }
  }

  void Shot(string label) {
    try {
      string path = ShotDir + "/" + _shots.ToString("00") + "_" + label + ".png";
      ScreenCapture.CaptureScreenshot(path);
      Log("SHOT " + _shots.ToString("00") + " " + label + " clicks=" + _clicks);
      _shots++;
    } catch (Exception e) { Log("shot failed: " + e.Message); }
  }

  IEnumerator Main() {
    Log("JOURNEY_START (real mouse, rabbit feeding vertical slice)");
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
    // 2. Math gate (walk-in portal): walk by REAL condition, never chase an
    // old-world target after travel.
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
    // 3. Counting-garden gate inside Math -> garden scene.
    MicroWorldGate gardenGate = null;
    yield return WaitFor(delegate {
      MicroWorldGate[] gates = FindObjectsOfType<MicroWorldGate>();
      foreach (MicroWorldGate g in gates) {
        if (g != null && string.Equals(g.gateId, "counting_garden", StringComparison.OrdinalIgnoreCase)) {
          gardenGate = g; return true;
        }
      }
      return false;
    }, 30f, "garden gate found");
    CountingGardenArea area = null;
    yield return WaitFor(delegate {
      area = FindObjectOfType<CountingGardenArea>();
      return area != null;
    }, 30f, "garden area module");
    if (gardenGate != null && gardenGate.EntryAnchor != null) {
      yield return WalkUntil(delegate {
        area = FindObjectOfType<CountingGardenArea>();
        return area != null && area.IsInside;
      }, gardenGate.EntryAnchor.position, 1.4f, 120f, "garden entered");
    }
    yield return WaitFor(delegate { return area != null && area.IsInside; }, 60f, "garden entered");
    yield return new WaitForSeconds(2f);
    Shot("03_garden");
    // 4. Carrot patch (zone 0): focus (click/proximity) -> panel -> Play.
    GardenZoneSpot spot0 = null;
    yield return WaitFor(delegate {
      spot0 = area.FindSpot(0);
      return spot0 != null;
    }, 20f, "zone-0 spot");
    GardenZonePanel panel = null;
    yield return WaitFor(delegate {
      panel = FindObjectOfType<GardenZonePanel>();
      return panel != null;
    }, 30f, "garden panel");
    yield return WalkUntil(delegate {
      return panel != null && panel.IsOpen && panel.PlayVisible;
    }, spot0.transform.position, 1.6f, 120f, "rabbit plot focused");
    yield return new WaitForSeconds(1f);
    Shot("04_plot_panel");
    if (!ClickPanelPlay(panel)) Log("WARN: play button click missed");
    RabbitFeed game = null;
    yield return WaitFor(delegate {
      Scene s = SceneManager.GetSceneByName(RabbitPlayBuilder.SceneName);
      if (!s.IsValid() || !s.isLoaded) return false;
      game = FindObjectOfType<RabbitFeed>();
      return game != null;
    }, 120f, "rabbit arena loaded");
    yield return new WaitForSeconds(3f);
    Shot("05_arena_arrival");
    int target = game.Target;
    Log("target=" + target + " (area ladder)");
    // 5. Intro + demo + handoff (poll the REAL phases).
    yield return new WaitForSeconds(9f);
    Shot("06_intro_board");
    yield return WaitFor(delegate { return game.Current == RabbitFeed.Phase.Demo; }, 60f, "demo started");
    yield return new WaitForSeconds(12f);
    Shot("07_demo_feed");
    yield return WaitFor(delegate { return game.DemoCarrotsFed >= target; }, 300f, "demo fed " + target);
    Shot("08_demo_done");
    yield return WaitFor(delegate { return game.Current == RabbitFeed.Phase.Feeding; }, 60f, "child control");
    Shot("09_handoff");
    // 6. The child's round: pick -> carry -> feed, one carrot at a time.
    for (int i = 0; i < target; i++) {
      RabbitCarrot c = FirstAvailable(game);
      if (c == null) { Log("WARN: no available carrot at " + i); _errors++; break; }
      yield return WalkToWorld(c.transform.position, 1.2f, 60f, "carrot " + i);
      yield return WaitForCarrot(c, RabbitCarrot.CarrotState.Carried, 25f, "carry " + i);
      if (i == 0) Shot("10_player_carry");
      RabbitPlayBuilder builder = FindObjectOfType<RabbitPlayBuilder>();
      Vector3 bowl = builder != null && builder.FeedAnchor != null
        ? builder.FeedAnchor.position : c.transform.position;
      yield return WalkToWorld(bowl, 1.4f, 60f, "bowl " + i);
      yield return WaitForCarrot(c, RabbitCarrot.CarrotState.Consumed, 25f, "fed " + i);
      _fed++;
      Shot("11_fed_" + _fed);
    }
    yield return WaitFor(delegate { return game.Current == RabbitFeed.Phase.Success; }, 40f, "success");
    yield return new WaitForSeconds(3f);
    Shot("12_success");
    Log("SUCCESS count=" + game.Count + " life=" + LifeState(area) + " result=" + game.ResultShown);
    // 7. Overshoot: one spare carrot -> correction -> success again.
    RabbitCarrot spare = FirstAvailable(game);
    if (spare != null) {
      yield return WalkToWorld(spare.transform.position, 1.2f, 60f, "spare carrot");
      yield return WaitForCarrot(spare, RabbitCarrot.CarrotState.Carried, 25f, "spare carry");
      RabbitPlayBuilder b2 = FindObjectOfType<RabbitPlayBuilder>();
      Vector3 bowl2 = b2 != null && b2.FeedAnchor != null ? b2.FeedAnchor.position : spare.transform.position;
      yield return WalkToWorld(bowl2, 1.4f, 60f, "spare bowl");
      yield return WaitFor(delegate { return game.Current == RabbitFeed.Phase.Correct; }, 30f, "correction");
      Shot("13_overshoot_correct");
      yield return WaitFor(delegate { return game.Current == RabbitFeed.Phase.Success; }, 120f, "corrected");
      Shot("14_corrected");
      Log("CORRECTED count=" + game.Count + " overshoots=" + game.Overshoots);
    } else Log("WARN: no spare carrot for the overshoot leg");
    // 8. Exit through the real door -> garden.
    MicroWorldPortal exit = null;
    foreach (MicroWorldPortal p in FindObjectsOfType<MicroWorldPortal>()) {
      if (p != null && p.PlayExit) { exit = p; break; }
    }
    if (exit != null) {
      yield return WalkUntil(delegate {
        Scene s = SceneManager.GetSceneByName(RabbitPlayBuilder.SceneName);
        return !s.IsValid() || !s.isLoaded;
      }, exit.transform.position, 1.2f, 90f, "exit door");
    }
    yield return WaitFor(delegate {
      area = FindObjectOfType<CountingGardenArea>();
      return area != null && area.IsInside && !area.IsInPlay;
    }, 120f, "back in the garden");
    yield return new WaitForSeconds(2f);
    Shot("15_garden_back");
    // 9. Re-entry -> adopt the finished picture (fresh instance, no replay).
    yield return WaitUntilFocusable(spot0, panel, 120f, "re-enter patch");
    if (!ClickPanelPlay(panel)) Log("WARN: replay button click missed");
    RabbitFeed game2 = null;
    yield return WaitFor(delegate {
      Scene s = SceneManager.GetSceneByName(RabbitPlayBuilder.SceneName);
      if (!s.IsValid() || !s.isLoaded) return false;
      RabbitFeed g = FindObjectOfType<RabbitFeed>();
      if (g != null && g != game) { game2 = g; return true; }
      return false;
    }, 120f, "fresh rabbit arena");
    yield return WaitFor(delegate {
      return game2 != null && game2.Current == RabbitFeed.Phase.Success;
    }, 60f, "adopt completed");
    yield return new WaitForSeconds(2f);
    Shot("16_reentry_adopt");
    Log("ADOPT count=" + game2.Count + " result=" + game2.ResultShown);
    Log("JOURNEY_END shots=" + _shots + " clicks=" + _clicks + " fed=" + _fed + " errors=" + _errors);
  }

  string LifeState(CountingGardenArea area) {
    try {
      if (area == null || area.RabbitLifecycle == null) return "none";
      return area.RabbitLifecycle.State.ToString();
    } catch (Exception) { return "err"; }
  }

  RabbitCarrot FirstAvailable(RabbitFeed game) {
    for (int i = 0; i < game.CarrotCount; i++) {
      RabbitCarrot c = game.CarrotAt(i);
      if (c != null && c.State == RabbitCarrot.CarrotState.Available) return c;
    }
    return null;
  }

  bool MathLoaded() {
    Scene s = SceneManager.GetSceneByName("MathScene");
    return s.IsValid() && s.isLoaded;
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

  // The zone panel only opens once the plot is FOCUSED. After an arena exit the
  // proximity latch is disarmed until the child walks clear, so: walk out to
  // the plaza first (re-arm), then click the spot until the panel opens. The
  // spot itself is a click door, so an explicit click always works.
  IEnumerator WaitUntilFocusable(GardenZoneSpot spot, GardenZonePanel panel, float timeout, string label) {
    float t = 0f;
    Vector3 plaza = CountingGardenBuilder.ArcCenter + new Vector3(0f, 0f, -2.5f);
    while (t < timeout) {
      bool open = false;
      try { open = panel != null && panel.IsOpen && panel.PlayVisible; } catch (Exception) { }
      if (open) { Log("ok: " + label); yield break; }
      if (!PlayerArrived(plaza, 2.0f)) ClickWorld(plaza);
      else ClickWorld(spot.transform.position);
      yield return new WaitForSeconds(1.2f);
      t += 1.2f;
    }
    Log("TIMEOUT: " + label);
    _errors++;
  }

  IEnumerator WaitForCarrot(RabbitCarrot c, RabbitCarrot.CarrotState want, float timeout, string label) {
    float t = 0f;
    while (t < timeout) {
      if (c != null && c.State == want) { Log("ok: " + label); yield break; }
      if (t > 4f && ((int)(t * 2f)) % 4 == 0) ClickWorld(c.transform.position);
      yield return new WaitForSeconds(0.5f);
      t += 0.5f;
    }
    Log("TIMEOUT: " + label + " (state=" + (c != null ? c.State.ToString() : "null") + ")");
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

  // Walk toward a world point while polling a REAL condition; stop the moment
  // the world answers (travel fires by proximity — never chase an old target).
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
}
