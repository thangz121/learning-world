// A_World/GameJuice.cs — S3-P2Z33 (user: "UX/UI ở mức cao nhất").
// The SHARED, dependency-free feedback layer for every play activity. Same
// discipline as DemoJuice: primitives only, collider-free, deterministic,
// Update-driven (no coroutines/assets), works at ANY parent scale so the garden
// miniatures and the full arenas read the same.
//
// Design (research: "Juice it or lose it", Jason Tu, easings.net): ONE player
// action gets SEVERAL redundant channels — motion (pop / squash-stretch /
// wobble), a ground ring, a colour flash, particles (via DemoJuice) and a short
// camera punch — layered within a few frames. Readability comes first: amounts
// are small and every effect decays to rest. An accessibility switch
// (`ReduceMotion`) turns the vestibular channels off.
// C# 9.0 only.
using UnityEngine;

public static class GameJuice {
  // Accessibility / low-end: when true, shake/wobble/flash/camera-punch are
  // skipped; the calm scale pops + particles stay (they never cause motion
  // sickness). The settings UI / launch flag flips this one switch.
  public static bool ReduceMotion;

  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color Mint = new Color(0.55f, 0.92f, 0.62f);
  static readonly Color Soft = new Color(0.98f, 0.72f, 0.55f);   // gentle "not yet"
  static readonly Color Sky = new Color(0.55f, 0.82f, 0.98f);

  // ---- one-shot primitives ---------------------------------------------------

  // Scale punch: a smooth up-and-back pop (never changes the resting scale).
  public static void Pop(Transform t, float amount = 0.16f, float seconds = 0.32f) {
    if (t == null) return;
    AddScaler(t, amount, amount, seconds);
  }

  // Cartoon squash: compress Y while the footprint bulges XZ.
  public static void Squash(Transform t, float amount = 0.18f, float seconds = 0.34f) {
    if (t == null) return;
    AddScaler(t, amount * 0.5f, -amount, seconds);
  }

  // Damped rotation wobble (a gentle "no, not that" or "oops" head-shake).
  public static void Wobble(Transform t, float degrees = 7f, float seconds = 0.5f) {
    if (t == null || ReduceMotion) return;
    JuiceWobble w = t.gameObject.GetComponent<JuiceWobble>();
    if (w == null) w = t.gameObject.AddComponent<JuiceWobble>();
    w.Begin(degrees, seconds);
  }

  // Expanding ground ring (a placement "stamp" / confirmation shockwave).
  public static void Ring(Transform parent, Vector3 localPos, Color color,
      float toRadius = 1.6f, float seconds = 0.5f) {
    if (parent == null) return;
    if (ReduceMotion) seconds *= 0.6f;
    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ring.name = "FxJuiceRing";
    ring.transform.SetParent(parent, false);
    ring.transform.localPosition = localPos + new Vector3(0f, 0.02f, 0f);
    ring.transform.localScale = new Vector3(0.2f, 0.006f, 0.2f);
    Renderer rend = ring.GetComponent<Renderer>();
    if (rend != null) rend.sharedMaterial = DemoJuice.Lit(color);
    StripCollider(ring);
    JuiceRing r = ring.AddComponent<JuiceRing>();
    r.Begin(toRadius, seconds);
  }

  // Brief emissive flash on a renderer, restoring its material afterwards.
  public static void Flash(Renderer r, Color color, float seconds = 0.26f, float intensity = 1.4f) {
    if (r == null || ReduceMotion) return;
    JuiceFlash f = r.gameObject.GetComponent<JuiceFlash>();
    if (f == null) f = r.gameObject.AddComponent<JuiceFlash>();
    f.Begin(color, seconds, intensity);
  }

  // Short camera shake (rotation-only: no positional drift feedback).
  public static void CameraPunch(float degrees = 1.5f, float seconds = 0.22f) {
    if (ReduceMotion) return;
    SmartCamera cam = null;
    try {
      Camera main = Camera.main;
      if (main != null) cam = main.GetComponent<SmartCamera>();
    } catch (System.Exception) { }
    if (cam == null) {
      try { cam = Object.FindObjectOfType<SmartCamera>(); } catch (System.Exception) { }
    }
    if (cam != null) cam.Punch(degrees, seconds);
  }

  // ---- layered presets -------------------------------------------------------
  // These are the "verbs" activities call. Each layers motion + particles +
  // flash + (optionally) a camera punch so the moment reads on every channel.

  // Picked up / grabbed.
  public static void PickFx(Transform fxParent, Vector3 world, Transform actor = null) {
    if (fxParent == null) return;
    if (actor != null) Pop(actor, 0.10f, 0.24f);
    Vector3 local = fxParent.InverseTransformPoint(world);
    DemoJuice.Sparkle(fxParent, local, 6, StableSeed(world, 11), 0.22f);
  }

  // Placed / fed / delivered successfully into a slot.
  public static void PlaceFx(Transform fxParent, Vector3 world, Transform board = null) {
    if (fxParent == null) return;
    Vector3 local = fxParent.InverseTransformPoint(world);
    Ring(fxParent, local, Mint, 1.5f, 0.5f);
    DemoJuice.Sparkle(fxParent, local, 8, StableSeed(world, 23), 0.35f);
    if (board != null) Pop(board, 0.10f, 0.28f);
  }

  // Correct answer / round won. `big` adds confetti + a camera punch.
  public static void CorrectFx(Transform fxParent, Vector3 world, bool big) {
    if (fxParent == null) return;
    Vector3 local = fxParent.InverseTransformPoint(world);
    Ring(fxParent, local, Gold, big ? 2.6f : 1.7f, 0.6f);
    DemoJuice.Sparkle(fxParent, local, big ? 16 : 10, StableSeed(world, 41), big ? 0.6f : 0.4f);
    if (big) {
      DemoJuice.Confetti(fxParent, local + new Vector3(0f, 0.6f, 0f), 18, StableSeed(world, 57), 0.8f, 1.2f);
      CameraPunch(1.6f, 0.24f);
    } else {
      CameraPunch(0.9f, 0.16f);
    }
  }

  // Wrong answer: warm, never punishing — a wobble + a soft single sparkle.
  public static void WrongFx(Transform actorOrBoard, Transform fxParent = null, Vector3 world = default) {
    Wobble(actorOrBoard, 6f, 0.5f);
    if (fxParent != null) {
      Vector3 local = fxParent.InverseTransformPoint(world);
      DemoJuice.Sparkle(fxParent, local, 4, StableSeed(world, 71), 0.3f);
    }
  }

  // ---- internals -------------------------------------------------------------

  static void AddScaler(Transform t, float up, float down, float seconds, bool invert = false) {
    JuiceScaler s = t.gameObject.GetComponent<JuiceScaler>();
    if (s == null) s = t.gameObject.AddComponent<JuiceScaler>();
    s.Begin(up, down, seconds, invert);
  }

  // Deterministic seed from a world point (no UnityEngine.Random → batch-stable).
  static int StableSeed(Vector3 v, int salt) {
    unchecked {
      int h = salt * 73856093;
      h ^= Mathf.RoundToInt(v.x * 100f) * 19349663;
      h ^= Mathf.RoundToInt(v.y * 100f) * 83492791;
      h ^= Mathf.RoundToInt(v.z * 100f) * 668265263;
      return h;
    }
  }

  static void StripCollider(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (System.Exception) { }
  }
}

// ---- components (self-destroying, Update-driven) ----------------------------

// Scale punch / squash: 0 -> peak -> back to rest over `_seconds`.
[DisallowMultipleComponent]
public class JuiceScaler : MonoBehaviour {
  float _t, _seconds = 0.3f, _up, _down;
  Vector3 _base = Vector3.one;

  public void Begin(float up, float down, float seconds, bool invert) {
    _up = up; _down = down; _seconds = Mathf.Max(0.05f, seconds);
    if (invert) { float s = _up; _up = _down; _down = s; }
    _t = 0f;
    _base = transform.localScale;
    enabled = true;
  }

  void Update() { Step(Time.deltaTime); }

  public void Step(float dt) {
    if (_t >= _seconds) { transform.localScale = _base; CharacterPresentation.DestroyNow(this); return; }
    _t += dt;
    float p = Mathf.Clamp01(_t / _seconds);
    // ease-out-back up to the peak at p=0.35, then settle; deterministic.
    float k = Mathf.Sin(p * Mathf.PI);            // 0 -> 1 -> 0
    float s = 1f + _up * k;
    float y = 1f + _down * k;
    transform.localScale = new Vector3(_base.x * s, _base.y * y, _base.z * s);
    if (_t >= _seconds) { transform.localScale = _base; CharacterPresentation.DestroyNow(this); }
  }
}

// Damped 3-cycle wobble about local Z, returning to the resting rotation.
[DisallowMultipleComponent]
public class JuiceWobble : MonoBehaviour {
  float _t, _seconds = 0.5f, _deg;
  Quaternion _base = Quaternion.identity;

  public void Begin(float degrees, float seconds) {
    _deg = degrees; _seconds = Mathf.Max(0.05f, seconds);
    _t = 0f; _base = transform.localRotation; enabled = true;
  }

  void Update() { Step(Time.deltaTime); }

  public void Step(float dt) {
    if (_t >= _seconds) { transform.localRotation = _base; CharacterPresentation.DestroyNow(this); return; }
    _t += dt;
    float p = Mathf.Clamp01(_t / _seconds);
    float a = _deg * Mathf.Sin(p * Mathf.PI * 3f) * (1f - p);
    transform.localRotation = _base * Quaternion.Euler(0f, 0f, a);
    if (_t >= _seconds) { transform.localRotation = _base; CharacterPresentation.DestroyNow(this); }
  }
}

// Expanding ring: scales outward, thins and self-destroys.
[DisallowMultipleComponent]
public class JuiceRing : MonoBehaviour {
  float _t, _seconds = 0.5f, _to = 1.6f, _baseThick = 0.006f;

  public void Begin(float toRadius, float seconds) {
    _to = toRadius; _seconds = Mathf.Max(0.05f, seconds);
    _t = 0f; _baseThick = transform.localScale.y; enabled = true;
  }

  void Update() { Step(Time.deltaTime); }

  public void Step(float dt) {
    if (_t >= _seconds) { CharacterPresentation.DestroyNow(gameObject); return; }
    _t += dt;
    float p = Mathf.Clamp01(_t / _seconds);
    float r = Mathf.Lerp(0.2f, _to, 1f - (1f - p) * (1f - p));
    transform.localScale = new Vector3(r, _baseThick * (1f - p), r);
    if (_t >= _seconds) CharacterPresentation.DestroyNow(gameObject);
  }
}

// One-shot emissive flash: swaps in a bright material, restores the original.
[DisallowMultipleComponent]
public class JuiceFlash : MonoBehaviour {
  float _t, _seconds = 0.26f;
  Material _original;
  Renderer _rend;

  public void Begin(Color color, float seconds, float intensity) {
    _renderer_Init();
    if (_rend == null) { CharacterPresentation.DestroyNow(this); return; }
    _original = _rend.sharedMaterial;
    Material bright = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    bright.SetColor("_BaseColor", color);
    if (bright.HasProperty("_EmissionColor")) {
      bright.EnableKeyword("_EMISSION");
      bright.SetColor("_EmissionColor", color * intensity);
    }
    _rend.sharedMaterial = bright;
    _seconds = Mathf.Max(0.05f, seconds);
    _t = 0f; enabled = true;
  }

  void _renderer_Init() { _rend = GetComponent<Renderer>(); }

  void Update() { Step(Time.deltaTime); }

  public void Step(float dt) {
    if (_t >= _seconds) { if (_rend != null) _rend.sharedMaterial = _original; CharacterPresentation.DestroyNow(this); return; }
    _t += dt;
    if (_t >= _seconds && _rend != null) { _rend.sharedMaterial = _original; CharacterPresentation.DestroyNow(this); }
  }
}
