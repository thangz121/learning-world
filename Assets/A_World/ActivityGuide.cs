// A_World/ActivityGuide.cs — S3-P2Z35 (user: "UX/UI ở mức cao nhất", P3).
// Onboarding-lite: ONE soft in-world marker that shows where the child should
// bring the thing they are carrying (the bowl / the pad / the receiver), or —
// on the stairs — the step to reach. Pure presentation: collider-free,
// bake-ignored, self-managed persistent instance, taps pass through. Hidden
// whenever nothing needs pointing at. ReduceMotion stops the bob/pulse.
// C# 9.0 only.
using UnityEngine;

public class ActivityGuide : MonoBehaviour {
  static ActivityGuide _instance;
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);

  GameObject _ring;
  GameObject _beacon;
  Vector3 _target;
  bool _active;
  float _phase;

  public static ActivityGuide Ensure() {
    if (_instance != null) return _instance;
    GameObject go = new GameObject("ActivityGuide");
    if (Application.isPlaying) DontDestroyOnLoad(go);
    _instance = go.AddComponent<ActivityGuide>();
    _instance.Build();
    return _instance;
  }

  public static void PointAt(Vector3 world) {
    ActivityGuide g = Ensure();
    if (!g._active) {
      g._active = true;
      if (g._ring != null) g._ring.SetActive(true);
      if (g._beacon != null) g._beacon.SetActive(true);
    }
    g._target = world;
  }

  public static void Clear() {
    if (_instance == null) return;
    _instance._active = false;
    if (_instance._ring != null) _instance._ring.SetActive(false);
    if (_instance._beacon != null) _instance._beacon.SetActive(false);
  }

  public static void ResetForTests() {
    if (_instance != null) {
      try { CharacterPresentation.DestroyNow(_instance.gameObject); } catch (System.Exception) { }
    }
    _instance = null;
  }

  // ---- build -----------------------------------------------------------------

  void Build() {
    if (_beacon != null) return;
    // USER ROUND 2026-10-01: the big flat gold GuideRing ("ô tròn vàng") under
    // the child was removed — it read as a blob on the floor in every arena.
    // The small bobbing beacon diamond stays as the wordless guide.

    _beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
    _beacon.name = "GuideBeacon";
    _beacon.transform.SetParent(transform, false);
    _beacon.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
    _beacon.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
    _beacon.GetComponent<Renderer>().sharedMaterial = DemoJuice.Lit(Gold);
    Strip(_beacon);

    _beacon.SetActive(false);
  }

  void Update() {
    if (!_active) return;
    _phase += Time.deltaTime;
    float pulse = GameJuice.ReduceMotion ? 1f : 1f + 0.10f * Mathf.Sin(_phase * 3.4f);
    float bob = GameJuice.ReduceMotion ? 0f : 0.12f * Mathf.Sin(_phase * 3f);
    _beacon.transform.position = new Vector3(_target.x, _target.y + 1.7f + bob, _target.z);
    _beacon.transform.localScale = Vector3.one * (0.38f * pulse);
  }

  static void Strip(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (System.Exception) { }
    try {
      Unity.AI.Navigation.NavMeshModifier mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (System.Exception) { }
  }

  public bool ActiveForTests { get { return _active; } }
  public Vector3 TargetForTests { get { return _target; } }
}
