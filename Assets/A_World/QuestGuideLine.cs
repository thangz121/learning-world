// A_World/QuestGuideLine.cs — Agent A (World & Visual). Player-report follow-up:
// except during the answer step, guidance toward the NPC to interact with
// (pre-talk -> Milo, bring -> Mia; the find step hides so the child answers
// apple-vs-ball unaided). Rendered as a CHAIN OF ARROWS on the ground pointing
// at the NPC: vivid orange, spaced apart (never a solid beam), each arrow
// BIGGER than the one before it (small at the player's feet, big at the NPC —
// size says "this way"), with a brightness WAVE marching NPC-ward on top.
// Staging is owned by MarketBootstrap (it already owns the HUD objective
// lines, so the guide and the HUD can never disagree); this presenter only
// lays arrows player-feet -> target-feet. Presentation ONLY: no gameplay state,
// no events, no services, no audio. Pooled opaque primitives, zero colliders
// (never eat raycasts), null-guarded throughout. C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

public enum GuideStage {
  Hidden, // answer step (find) or no active guidance
  ToMilo, // pre-talk + post-complete: talk to Milo
  ToMia,  // bring step: hand the item to Mia
}

[DisallowMultipleComponent]
public sealed class QuestGuideLine : MonoBehaviour {
  public const float DotY = 0.05f; // paint above grass/path, below feet soles
  public const float DashSpacing = 0.65f; // gap between arrows (the "ngắt quãng")
  public const int MaxDots = 24;
  public static readonly Color DotColor = new Color(1f, 0.52f, 0.08f); // vivid orange
  // Size gradient (pure, tests pin this): arrow i of count grows toward the
  // NPC — 0.7x at the player's feet, 1.5x at the NPC. Relative, never absolute.
  public const float GradNear = 0.7f;
  public const float GradFar = 1.5f;
  // Marching wave (pure, tests pin this): arrow i (0 = at player, up = toward
  // the NPC) pulses with a phase lag, so the bright hump travels toward the
  // NPC. Peak scale 1.0, trough 0.45 (never invisible, never a barrier).
  public const float WaveHertz = 0.55f;
  public const float WaveLagPerDot = 0.45f; // radians of lag per step toward NPC

  // Relative size of arrow i in a chain of count (0 = player end).
  public static float Gradient(int i, int count) {
    if (count < 2) return 1f;
    int clamped = i < 0 ? 0 : (i > count - 1 ? count - 1 : i);
    return GradNear + (GradFar - GradNear) * clamped / (count - 1);
  }

  // Brightness multiplier of arrow i at time t (seconds). Pure: the arrival
  // direction is provable without a live frame (peak times rise with i).
  public static float WaveScale(int i, float t) {
    float phase = t * Mathf.PI * 2f * WaveHertz - i * WaveLagPerDot;
    float s = Mathf.Sin(phase);
    return 0.725f + 0.275f * s;
  }

  GuideStage _stage = GuideStage.Hidden;
  Transform _target;
  Transform _playerT;
  readonly List<Transform> _dots = new List<Transform>();
  Material _dotMat;

  void Awake() {
    BuildDotsImmediate();
  }

  // Deterministic build hook (tests drive this; Awake covers live play).
  // Each GuideDot{i} is a group: Shaft + HeadL/HeadR chevron pointing +Z
  // (LayDots faces +Z along travel, so the tip leads toward the NPC).
  public void BuildDotsImmediate() {
    if (_dots.Count > 0) return;
    Shader lit = Shader.Find("Universal Render Pipeline/Lit");
    _dotMat = new Material(lit != null ? lit : Shader.Find("Standard"));
    try {
      if (_dotMat.HasProperty("_BaseColor")) _dotMat.SetColor("_BaseColor", DotColor);
      else if (_dotMat.HasProperty("_Color")) _dotMat.SetColor("_Color", DotColor);
      if (_dotMat.HasProperty("_Smoothness")) _dotMat.SetFloat("_Smoothness", 0.3f);
      if (_dotMat.HasProperty("_Metallic")) _dotMat.SetFloat("_Metallic", 0f);
    } catch (Exception) { }
    for (int i = 0; i < MaxDots; i++) {
      GameObject dot = new GameObject("GuideDot" + i);
      dot.transform.SetParent(transform, false);
      AddPart(dot.transform, "Shaft", PrimitiveType.Cube,
        new Vector3(0f, 0f, -0.03f), new Vector3(0.07f, 0.02f, 0.22f), 0f);
      AddPart(dot.transform, "HeadL", PrimitiveType.Cube,
        new Vector3(-0.045f, 0f, 0.125f), new Vector3(0.06f, 0.02f, 0.13f), 45f);
      AddPart(dot.transform, "HeadR", PrimitiveType.Cube,
        new Vector3(0.045f, 0f, 0.125f), new Vector3(0.06f, 0.02f, 0.13f), -45f);
      dot.SetActive(false);
      _dots.Add(dot.transform);
    }
  }

  void AddPart(Transform parent, string partName, PrimitiveType kind, Vector3 localPos, Vector3 localScale, float rotYDeg) {
    GameObject part = GameObject.CreatePrimitive(kind);
    part.name = partName;
    part.transform.SetParent(parent, false);
    part.transform.localPosition = localPos;
    part.transform.localScale = localScale;
    part.transform.localRotation = Quaternion.Euler(0f, rotYDeg, 0f);
    Renderer r = part.GetComponent<Renderer>();
    if (r != null) {
      r.sharedMaterial = _dotMat;
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      r.receiveShadows = false;
    }
    // CRITICAL: arrows answer visually only — never intercept clicks.
    CharacterPresentation.DestroyNow(part.GetComponent<Collider>());
  }

  public GuideStage Stage {
    get { return _stage; }
  }

  public bool IsShowing {
    get {
      if (_dots.Count == 0) return false;
      foreach (Transform d in _dots) {
        if (d != null && d.gameObject.activeSelf) return true;
      }
      return false;
    }
  }

  public int VisibleDotCount {
    get {
      int n = 0;
      foreach (Transform d in _dots) {
        if (d != null && d.gameObject.activeSelf) n++;
      }
      return n;
    }
  }

  // Staging seam (MarketBootstrap drives this next to the HUD objective text).
  public void SetStage(GuideStage stage, Transform target) {
    _stage = stage;
    _target = (stage == GuideStage.Hidden) ? null : target;
    // Answer step hides instantly (Update would do it next frame anyway).
    if (stage == GuideStage.Hidden) HideAll();
  }

  // Test/live seam: explicit endpoints without a player lookup (static pose).
  public void DrawBetweenForTests(Vector3 fromFeet, Vector3 toFeet) {
    if (_dots.Count == 0) BuildDotsImmediate();
    LayDots(fromFeet, toFeet, 0f, true);
  }

  void HideAll() {
    foreach (Transform d in _dots) {
      if (d == null) continue;
      try { if (d.gameObject.activeSelf) d.gameObject.SetActive(false); } catch (Exception) { }
    }
  }

  void Update() {
    if (_dots.Count == 0) BuildDotsImmediate();
    if (_stage == GuideStage.Hidden || _target == null) {
      HideAll();
      return;
    }
    if (_playerT == null) {
      try {
        GameObject player = GameObject.Find("Player");
        if (player != null) _playerT = player.transform;
      } catch (Exception) { }
      if (_playerT == null) {
        HideAll();
        return;
      }
    }
    LayDots(_playerT.position, _target.position, Time.time, false);
  }

  // Lay the chain from a to b. staticPose freezes the wave at full brightness
  // (deterministic screenshots/tests); live play marches with Time.time.
  void LayDots(Vector3 a, Vector3 b, float t, bool staticPose) {
    Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
    float len = flat.magnitude;
    if (len < 0.05f) {
      HideAll();
      return;
    }
    Vector3 dir = flat / len;
    Quaternion face = Quaternion.LookRotation(dir);
    int count = Mathf.Clamp(Mathf.FloorToInt(len / DashSpacing), 2, MaxDots);
    for (int i = 0; i < _dots.Count; i++) {
      Transform d = _dots[i];
      if (d == null) continue;
      if (i >= count) {
        try { if (d.gameObject.activeSelf) d.gameObject.SetActive(false); } catch (Exception) { }
        continue;
      }
      float f = (i + 1f) / (count + 1f);
      Vector3 p = a + flat * f;
      try {
        d.position = new Vector3(p.x, DotY, p.z);
        d.rotation = face;
        // Bigger toward the NPC (gradient) × marching brightness (wave).
        float s = Gradient(i, count) * (staticPose ? 1f : WaveScale(i, t));
        d.localScale = new Vector3(s, 1f, s);
        if (!d.gameObject.activeSelf) d.gameObject.SetActive(true);
      } catch (Exception) { }
    }
  }
}
