// TempP57Journey.cs — journey driver GAMEPLAY #5 "GIAO HÀNG ĐÚNG SỐ"
// (deliver the apples). Committed with the bundle per the standing order
// (2026-09-25): drop-in path Assets/A_World/DeliveryVillage/TempP57Journey.cs.
// Inert in production: boots ONLY with the -journey CLI flag. Real
// InputSystem mouse injection (queued press held 4 frames), condition-based
// walking (never chases an old-world target after a travel), real-state
// polling only. No teleport, no state pokes.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TempP57JourneyBoot {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() {
    string[] args = Environment.GetCommandLineArgs();
    bool want = false;
    foreach (string a in args) {
      if (string.Equals(a, "-journey", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
    }
    if (!want) return;
    GameObject go = new GameObject("TempP57Journey");
    GameObject.DontDestroyOnLoad(go);
    go.AddComponent<TempP57Journey>();
  }
}

public class TempP57Journey : MonoBehaviour {
  const string ShotDir = "D:/Vscode/p57j-shots";
  int _shots;
  int _clicks;
  int _delivered;
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
    try { Debug.Log("[P57J] " + m); } catch (Exception) { }
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
    Log("JOURNEY_START (real mouse, delivery vertical slice)");
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
    // 3. Delivery gate: walk IN (the portal fires on its own — no UI).
    MicroWorldGate deliveryGate = null;
    yield return WaitFor(delegate {
      MicroWorldGate[] gates = FindObjectsOfType<MicroWorldGate>();
      foreach (MicroWorldGate g in gates) {
        if (g != null && string.Equals(g.gateId, "delivery_village", StringComparison.OrdinalIgnoreCase)) {
          deliveryGate = g; return true;
        }
      }
      return false;
    }, 30f, "delivery gate found");
    if (deliveryGate != null) yield return WalkToWorld(deliveryGate.transform.position, 3.0f, 120f, "delivery gate");
    yield return new WaitForSeconds(0.5f);
    Shot("03_delivery_gate");
    DeliveryArea area = null;
    yield return WaitFor(delegate {
      area = FindObjectOfType<DeliveryArea>();
      return area != null && area.IsInside;
    }, 10f, "delivery village entered (transition)");
    if (area != null && !area.IsInside) {
      yield return WalkUntil(delegate {
        area = FindObjectOfType<DeliveryArea>();
        return area != null && area.IsInside;
      }, deliveryGate.transform.position, 0.8f, 60f, "through the gate");
    }
    yield return WaitFor(delegate {
      area = FindObjectOfType<DeliveryArea>();
      return area != null && area.IsInside;
    }, 60f, "delivery village entered (transition)");
    Shot("04_transition");
    DeliveryGame game = null;
    yield return WaitFor(delegate {
      Scene s = SceneManager.GetSceneByName(DeliveryBuilder.SceneName);
      if (!s.IsValid() || !s.isLoaded) return false;
      game = FindObjectOfType<DeliveryGame>();
      return game != null;
    }, 120f, "delivery scene loaded");
    yield return new WaitForSeconds(3f);
    Shot("05_world_entry");
    int target = game.Target;
    Log("target=" + target + " (area ladder)");
    // 4. Order + demo + handoff (poll the REAL phases).
    yield return new WaitForSeconds(9f);
    Shot("06_order_board");
    yield return WaitFor(delegate { return game.Current == DeliveryGame.Phase.Demo; }, 60f, "demo started");
    yield return new WaitForSeconds(12f);
    Shot("07_demo_pickup");
    yield return WaitFor(delegate { return game.DemoApplesDelivered >= 1; }, 120f, "first demo handover");
    Shot("08_demo_handover");
    yield return WaitFor(delegate { return game.DemoApplesDelivered >= target; }, 420f, "demo delivered " + target);
    Shot("09_demo_done");
    yield return WaitFor(delegate { return game.Current == DeliveryGame.Phase.Delivering; }, 60f, "child control");
    Shot("10_handoff");
    // 5. The child's round: pick -> carry -> hand over, one apple at a time.
    for (int i = 0; i < target; i++) {
      DeliveryItem item = FirstAvailable(game);
      if (item == null) { Log("WARN: no available apple at " + i); _errors++; break; }
      yield return WalkToWorld(item.transform.position, 1.2f, 60f, "apple " + i);
      yield return WaitForItem(item, DeliveryItem.ItemState.Carried, 25f, "carry " + i);
      if (i == 0) Shot("11_player_carry");
      yield return WalkToWorld(ReceiverWorld(), 1.5f, 60f, "receiver " + i);
      yield return WaitForItem(item, DeliveryItem.ItemState.Delivered, 25f, "handover " + i);
      _delivered++;
      Shot("12_crate_" + _delivered);
    }
    yield return WaitFor(delegate { return game.Current == DeliveryGame.Phase.Success; }, 40f, "success");
    yield return new WaitForSeconds(3f);
    Shot("13_success");
    Log("SUCCESS count=" + game.Count + " crate=" + game.CrateCount + " life=" + LifeState(area)
      + " result=" + game.ResultShown + " exitCue=" + game.ExitCueShown);
    // 6. Overshoot: one spare apple -> correction -> success again.
    DeliveryItem spare = FirstAvailable(game);
    if (spare != null) {
      yield return WalkToWorld(spare.transform.position, 1.2f, 60f, "spare apple");
      yield return WaitForItem(spare, DeliveryItem.ItemState.Carried, 25f, "spare carry");
      yield return WalkToWorld(ReceiverWorld(), 1.5f, 60f, "spare handover");
      yield return WaitFor(delegate { return game.Current == DeliveryGame.Phase.Correct; }, 30f, "correction");
      Shot("14_overshoot");
      yield return WaitFor(delegate { return game.Current == DeliveryGame.Phase.Success; }, 120f, "corrected");
      Shot("15_corrected");
      Log("CORRECTED count=" + game.Count + " overshoots=" + game.Overshoots);
    } else Log("WARN: no spare apple for the overshoot leg");
    // 7. Reward echo (result + exit cue held), then the real door home.
    yield return new WaitForSeconds(2f);
    Shot("16_reward");
    MicroWorldPortal exit = null;
    foreach (MicroWorldPortal p in FindObjectsOfType<MicroWorldPortal>()) {
      if (p != null && p.ExitMode && p.areaId == DeliveryArea.AreaId) { exit = p; break; }
    }
    if (exit != null) {
      yield return WalkUntil(delegate {
        Scene s = SceneManager.GetSceneByName(DeliveryBuilder.SceneName);
        return !s.IsValid() || !s.isLoaded;
      }, exit.transform.position, 1.2f, 90f, "exit door");
    }
    yield return WaitFor(delegate {
      Scene s = SceneManager.GetSceneByName(DeliveryBuilder.SceneName);
      return !s.IsValid() || !s.isLoaded;
    }, 120f, "village unloaded (returned to hub)");
    yield return new WaitForSeconds(2f);
    Shot("17_return_hub");
    yield return WaitFor(delegate { return area != null && !area.IsInside; }, 30f, "area back outside");
    // 8. Re-entry -> adopt the finished order (fresh instance, no replay).
    yield return WalkUntil(delegate {
      Scene s = SceneManager.GetSceneByName(DeliveryBuilder.SceneName);
      return s.IsValid() && s.isLoaded && FindObjectOfType<DeliveryGame>() != game;
    }, deliveryGate.transform.position, 0.8f, 90f, "re-enter the gate");
    DeliveryGame game2 = null;
    yield return WaitFor(delegate {
      Scene s = SceneManager.GetSceneByName(DeliveryBuilder.SceneName);
      if (!s.IsValid() || !s.isLoaded) return false;
      DeliveryGame g = FindObjectOfType<DeliveryGame>();
      if (g != null && g != game) { game2 = g; return true; }
      return false;
    }, 120f, "fresh delivery village");
    yield return WaitFor(delegate {
      return game2 != null && game2.Current == DeliveryGame.Phase.Success;
    }, 60f, "adopt completed");
    yield return new WaitForSeconds(2f);
    Shot("18_reentry_adopt");
    Log("ADOPT count=" + game2.Count + " result=" + game2.ResultShown
      + " exitCue=" + game2.ExitCueShown + " life=" + LifeState(area));
    Log("JOURNEY_END shots=" + _shots + " clicks=" + _clicks + " delivered=" + _delivered
      + " errors=" + _errors);
  }

  string LifeState(DeliveryArea area) {
    try {
      if (area == null || area.Lifecycle == null) return "none";
      return area.Lifecycle.State.ToString();
    } catch (Exception) { return "err"; }
  }

  bool MathLoaded() {
    Scene s = SceneManager.GetSceneByName("MathScene");
    return s.IsValid() && s.isLoaded;
  }

  Vector3 ReceiverWorld() {
    DeliveryBuilder builder = FindObjectOfType<DeliveryBuilder>();
    if (builder == null) return Vector3.zero;
    if (builder.DeliveryAnchor != null) return builder.DeliveryAnchor.position;
    return builder.transform.TransformPoint(DeliveryBuilder.MiaStart);
  }

  DeliveryItem FirstAvailable(DeliveryGame game) {
    for (int i = 0; i < game.ItemCountTotal; i++) {
      DeliveryItem it = game.ItemAt(i);
      if (it != null && it.State == DeliveryItem.ItemState.Available) return it;
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

  IEnumerator WaitForItem(DeliveryItem it, DeliveryItem.ItemState want, float timeout, string label) {
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
