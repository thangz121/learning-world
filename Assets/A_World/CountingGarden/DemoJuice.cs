// A_World/CountingGarden/DemoJuice.cs — S3 P2Y ANIMATION JUICE (user: "animation
// hết mức... nhìn vào là muốn chơi"). Zero-dependency, primitive-built,
// collider-free celebration kit for the counting lesson (works at ANY parent
// scale, so the garden miniature and the full-size play arena share it):
//   Confetti  — a burst of spinning paper bits with gravity, self-destroying
//   Sparkle   — a ring of twinkling shards that pop and fade
//   StageSpotlight — a soft pulsing stage pool that makes the lesson read as
//                    "a show" from a distance
// Deterministic (seeded), batch-safe (Update-driven, no coroutines/assets).
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

public static class DemoJuice {
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color Pink = new Color(0.95f, 0.55f, 0.72f);
  static readonly Color Sky = new Color(0.45f, 0.70f, 0.92f);
  static readonly Color Mint = new Color(0.45f, 0.78f, 0.60f);
  static readonly Color Cream = new Color(0.99f, 0.90f, 0.62f);
  static readonly Color[] Party = { Gold, Pink, Sky, Mint, Cream };

  // Burst of confetti from a point (local to `parent`).
  public static void Confetti(Transform parent, Vector3 origin, int count, int seed,
      float spread, float kick) {
    if (parent == null) return;
    System.Random rnd = new System.Random(seed);
    for (int i = 0; i < count; i++) {
      GameObject bit = GameObject.CreatePrimitive(PrimitiveType.Cube);
      bit.name = "FxConfetti" + i;
      bit.transform.SetParent(parent, false);
      float f = (float)rnd.NextDouble();
      Vector3 pos = origin + new Vector3(
        ((float)rnd.NextDouble() - 0.5f) * spread, 0f,
        ((float)rnd.NextDouble() - 0.5f) * spread);
      bit.transform.localPosition = pos;
      float s = 0.06f + f * 0.05f;
      bit.transform.localScale = new Vector3(s, s * 0.45f, s);
      bit.transform.localRotation = Quaternion.Euler(
        (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, 0f);
      StripCollider(bit);
      ConfettiBit b = bit.AddComponent<ConfettiBit>();
      float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
      b.Velocity = new Vector3(Mathf.Cos(ang) * 0.45f, kick * (0.7f + (float)rnd.NextDouble() * 0.6f),
        Mathf.Sin(ang) * 0.45f);
      b.Spin = new Vector3(((float)rnd.NextDouble() - 0.5f) * 720f,
        ((float)rnd.NextDouble() - 0.5f) * 720f, ((float)rnd.NextDouble() - 0.5f) * 720f);
      b.Tint = Party[i % Party.Length];
    }
  }

  // Ring of sparkles at a point (local to `parent`).
  public static void Sparkle(Transform parent, Vector3 origin, int count, int seed, float radius) {
    if (parent == null) return;
    System.Random rnd = new System.Random(seed);
    for (int i = 0; i < count; i++) {
      GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
      shard.name = "FxSparkle" + i;
      shard.transform.SetParent(parent, false);
      float ang = (i / (float)count) * Mathf.PI * 2f;
      shard.transform.localPosition = origin + new Vector3(
        Mathf.Cos(ang) * radius, 0.05f + (float)rnd.NextDouble() * 0.1f, Mathf.Sin(ang) * radius);
      float s = 0.05f + (float)rnd.NextDouble() * 0.05f;
      shard.transform.localScale = new Vector3(s, s, s);
      StripCollider(shard);
      SparkleBit bit = shard.AddComponent<SparkleBit>();
      bit.Tint = Party[i % Party.Length];
      bit.Rise = 0.25f + (float)rnd.NextDouble() * 0.35f;
    }
  }

  // Pulsing stage floor ring (persistent; attached by the builder).
  public static GameObject AttachSpotlight(Transform parent, string name, Vector3 pos, float diameter) {
    if (parent == null) return null;
    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ring.name = name;
    ring.transform.SetParent(parent, false);
    ring.transform.localPosition = pos;
    ring.transform.localScale = new Vector3(diameter, 0.004f, diameter);
    // A primitive without an explicit material ships the built-in default
    // shader, which is MAGENTA under URP (journey screenshot bug): give the
    // pool a warm stage-light tint.
    Renderer rend = ring.GetComponent<Renderer>();
    if (rend != null) rend.sharedMaterial = Lit(new Color(0.97f, 0.90f, 0.70f));
    StripCollider(ring);
    try {
      Unity.AI.Navigation.NavMeshModifier mod = ring.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (System.Exception) { }
    StageSpotlight spot = ring.AddComponent<StageSpotlight>();
    return ring;
  }

  static void StripCollider(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (System.Exception) { }
  }

  static readonly Dictionary<Color, Material> _mats = new Dictionary<Color, Material>();

  public static Material Lit(Color color) {
    Material cached;
    if (_mats.TryGetValue(color, out cached) && cached != null) return cached;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    mat.enableInstancing = true;
    _mats[color] = mat;
    return mat;
  }
}

// One flying paper bit: gravity + spin, fades out, self-destroys.
public class ConfettiBit : MonoBehaviour {
  public Vector3 Velocity;
  public Vector3 Spin;
  public Color Tint = Color.white;
  float _life = 2.4f;
  Vector3 _startScale = Vector3.one;

  void Start() {
    Renderer rend = GetComponent<Renderer>();
    if (rend != null) rend.sharedMaterial = DemoJuice.Lit(Tint);
    _startScale = transform.localScale;
  }

  void Update() {
    float dt = Time.deltaTime;
    Velocity += new Vector3(0f, -2.6f * dt, 0f);
    transform.localPosition += Velocity * dt;
    transform.localRotation *= Quaternion.Euler(Spin * dt);
    _life -= dt;
    if (_life <= 0.8f) transform.localScale = _startScale * Mathf.Clamp01(_life / 0.8f);
    if (transform.localPosition.y < -0.2f || _life <= 0f) Destroy(gameObject);
  }
}

// One twinkling shard: rises, spins, shrinks, self-destroys.
public class SparkleBit : MonoBehaviour {
  public Color Tint = Color.white;
  public float Rise = 0.3f;
  float _life = 0.9f;
  float _base = 0.08f;

  void Start() {
    Renderer rend = GetComponent<Renderer>();
    if (rend != null) rend.sharedMaterial = DemoJuice.Lit(Tint);
    _base = transform.localScale.x;
  }

  void Update() {
    float dt = Time.deltaTime;
    _life -= dt;
    transform.localPosition += new Vector3(0f, Rise * dt, 0f);
    transform.localRotation *= Quaternion.Euler(0f, 0f, 540f * dt);
    float s = Mathf.Clamp01(_life / 0.9f);
    float pulse = 0.7f + 0.3f * Mathf.Sin(_life * 26f);
    transform.localScale = Vector3.one * (_base * s * pulse);
    if (_life <= 0f) Destroy(gameObject);
  }
}

// Soft pulsing stage pool: scale + gentle bob, no gameplay.
public class StageSpotlight : MonoBehaviour {
  float _phase;
  Vector3 _base;

  void Start() {
    _base = transform.localScale;
    Vector3 p = transform.position;
    _phase = Mathf.Repeat((p.x + p.z) * 0.7f, Mathf.PI * 2f);
  }

  void Update() {
    float t = Time.time * 0.9f + _phase;
    float s = 1f + 0.035f * Mathf.Sin(t);
    transform.localScale = new Vector3(_base.x * s, _base.y, _base.z * s);
  }
}
