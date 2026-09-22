// A_World/WorldBeauty.cs — S6 BEAUTY + PINK PASS (user rounds: "biến world
// này trông đẹp hơn" + "thêm gam hồng vào game vì đây là game cho con gái").
// Shared deterministic kit for BOTH worlds (primitives only, shared Lit
// materials, collider-free dressing, no gameplay/click targets):
//   - BlossomTree: cartoon cherry tree (trunk + 3 pink canopy balls)
//   - PetalCarpet: flat soft-pink ground treatment under a tree
//   - FlowerDrift: pastel flower cluster (2 blooms, pink-forward)
//   - PastelRainbow: 6 pastel bands + cloud feet (the pink landmark)
//   - ApplyMainAtmosphere / ApplyMathAtmosphere: per-world fog + ambient.
//     Main's fog (18-45m, set by MarketBuilder) washed the Math sky/clouds
//     out; the Math scene gets a soft far haze so sky, clouds and the
//     rainbow read. MarketBootstrap swaps them on travel/return.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

public static class WorldBeauty {
  // Pink-forward pastel palette (girl-first art direction, S6).
  public static readonly Color BlossomPink = new Color(0.98f, 0.72f, 0.82f);
  public static readonly Color BlossomDeep = new Color(0.95f, 0.55f, 0.72f);
  public static readonly Color BlossomCream = new Color(0.99f, 0.90f, 0.93f);
  public static readonly Color Petal = new Color(0.96f, 0.80f, 0.85f);
  public static readonly Color CloudPink = new Color(0.99f, 0.94f, 0.96f);
  public static readonly Color Peach = new Color(0.99f, 0.78f, 0.62f);
  public static readonly Color CreamGold = new Color(0.99f, 0.90f, 0.62f);
  public static readonly Color Mint = new Color(0.70f, 0.90f, 0.72f);
  public static readonly Color Sky = new Color(0.68f, 0.84f, 0.98f);
  public static readonly Color Lilac = new Color(0.82f, 0.74f, 0.96f);
  static readonly Color Trunk = new Color(0.45f, 0.30f, 0.16f);

  // ---- blossom tree + petal carpet ------------------------------------------

  public static void BlossomTree(Transform parent, string name, Vector3 pos, float scale) {
    GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    trunk.name = name + "Trunk";
    trunk.transform.SetParent(parent);
    trunk.transform.localPosition = pos + new Vector3(0f, 0.75f * scale, 0f);
    trunk.transform.localScale = new Vector3(0.34f * scale, 0.75f * scale, 0.34f * scale);
    trunk.GetComponent<Renderer>().sharedMaterial = Lit(Trunk);
    StripCollider(trunk);
    Ball(parent, name + "CanopyA", pos + new Vector3(0f, 1.85f * scale, 0f),
      2.1f * scale, BlossomPink);
    Ball(parent, name + "CanopyB", pos + new Vector3(0.7f * scale, 1.55f * scale, 0.25f * scale),
      1.5f * scale, BlossomDeep);
    Ball(parent, name + "CanopyC", pos + new Vector3(-0.6f * scale, 1.65f * scale, -0.3f * scale),
      1.4f * scale, BlossomCream);
  }

  public static void TrunkPost(Transform parent, string name, Vector3 pos, float height, float radius) {
    GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    trunk.name = name;
    trunk.transform.SetParent(parent);
    trunk.transform.localPosition = pos + new Vector3(0f, height * 0.5f, 0f);
    trunk.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
    trunk.GetComponent<Renderer>().sharedMaterial = Lit(Trunk);
    StripCollider(trunk);
  }

  // Pretty post (S8 user round: "làm cột cũng phải đẹp tương xứng"): flared
  // foot + tapered wooden shaft + ball cap — the cartoon family look.
  public static void PrettyPost(Transform parent, string name, Vector3 pos, float height,
      float radius, Color body, Color cap) {
    GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    foot.name = name + "Foot";
    foot.transform.SetParent(parent);
    foot.transform.localPosition = pos + new Vector3(0f, height * 0.175f, 0f);
    foot.transform.localScale = new Vector3(radius * 2.7f, height * 0.175f, radius * 2.7f);
    foot.GetComponent<Renderer>().sharedMaterial = Lit(body);
    StripCollider(foot);
    GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    shaft.name = name;
    shaft.transform.SetParent(parent);
    shaft.transform.localPosition = pos + new Vector3(0f, height * 0.5f, 0f);
    shaft.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
    shaft.GetComponent<Renderer>().sharedMaterial = Lit(body);
    StripCollider(shaft);
    Ball(parent, name + "Cap", pos + new Vector3(0f, height + radius * 0.9f, 0f),
      radius * 2.2f, cap);
  }

  public static void PetalCarpet(Transform parent, string name, Vector3 pos, float diameter) {
    GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    patch.name = name;
    patch.transform.SetParent(parent);
    patch.transform.localPosition = pos + new Vector3(0f, 0.012f, 0f);
    patch.transform.localScale = new Vector3(diameter, 0.012f, diameter);
    patch.GetComponent<Renderer>().sharedMaterial = Lit(Petal);
    StripCollider(patch);
    IgnoreFromBuild(patch);
  }

  // ---- pastel flower drift (2 blooms; pink-forward) --------------------------

  public static void FlowerDrift(Transform parent, string name, Vector3 pos, float scale) {
    Color[] heads = { BlossomDeep, BlossomCream };
    for (int i = 0; i < 2; i++) {
      float off = (i == 0) ? -0.26f : 0.26f;
      GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      stem.name = name + "Stem" + i;
      stem.transform.SetParent(parent);
      stem.transform.localPosition = pos + new Vector3(off, 0.12f * scale, 0f);
      stem.transform.localScale = new Vector3(0.05f, 0.12f * scale, 0.05f);
      stem.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.30f, 0.60f, 0.30f));
      StripCollider(stem);
      Ball(parent, name + "Head" + i,
        pos + new Vector3(off, 0.26f * scale, 0f), 0.22f * scale, heads[i]);
    }
  }

  // ---- pastel rainbow (pink-forward landmark) ---------------------------------

  public static void PastelRainbow(Transform parent, string name, Vector3 pos, float radius) {
    Color[] bands = { BlossomDeep, Peach, CreamGold, Mint, Sky, Lilac };
    const int segments = 5;
    for (int b = 0; b < bands.Length; b++) {
      float r = radius - b * 0.85f;
      for (int i = 0; i < segments; i++) {
        float a = Mathf.PI * (i + 0.5f) / segments;
        float x = -Mathf.Cos(a) * r;
        float y = 0.2f + Mathf.Sin(a) * r;
        float x0 = -Mathf.Cos(Mathf.PI * i / segments) * r;
        float y0 = Mathf.Sin(Mathf.PI * i / segments) * r;
        float x1 = -Mathf.Cos(Mathf.PI * (i + 1) / segments) * r;
        float y1 = Mathf.Sin(Mathf.PI * (i + 1) / segments) * r;
        float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) * 1.06f;
        GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seg.name = name + (b * segments + i);
        seg.transform.SetParent(parent);
        seg.transform.localPosition = pos + new Vector3(x, y, 0f);
        seg.transform.localScale = new Vector3(len, 0.8f, 0.8f);
        seg.transform.localRotation = Quaternion.Euler(0f, 0f,
          Mathf.Atan2(Mathf.Cos(a) * r, Mathf.Sin(a) * r) * Mathf.Rad2Deg);
        seg.GetComponent<Renderer>().sharedMaterial = Lit(bands[b]);
        StripCollider(seg);
        IgnoreFromBuild(seg);
      }
    }
    GameObject cloudL = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    cloudL.name = name + "CloudL";
    cloudL.transform.SetParent(parent);
    cloudL.transform.localPosition = pos + new Vector3(-radius - 1f, 1.2f, 0f);
    cloudL.transform.localScale = new Vector3(5.0f, 2.4f, 4.0f);
    cloudL.GetComponent<Renderer>().sharedMaterial = Lit(CloudPink);
    StripCollider(cloudL);
    IgnoreFromBuild(cloudL);
    GameObject cloudR = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    cloudR.name = name + "CloudR";
    cloudR.transform.SetParent(parent);
    cloudR.transform.localPosition = pos + new Vector3(radius + 1f, 1.2f, 0f);
    cloudR.transform.localScale = new Vector3(5.0f, 2.4f, 4.0f);
    cloudR.GetComponent<Renderer>().sharedMaterial = Lit(CloudPink);
    StripCollider(cloudR);
    IgnoreFromBuild(cloudR);
  }

  // ---- falling petals + butterflies (S7 full-bloom) ---------------------------

  public static void PetalFall(Transform parent, string name, Vector3 center,
      float radius, int count, int seed) {
    GameObject root = new GameObject(name);
    root.transform.SetParent(parent);
    root.transform.localPosition = center;
    System.Random rng = new System.Random(seed);
    Color[] petals = { BlossomPink, BlossomDeep, BlossomCream };
    for (int i = 0; i < count; i++) {
      float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
      float rad = Mathf.Sqrt((float)rng.NextDouble()) * radius;
      GameObject petal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      petal.name = name + i;
      petal.transform.SetParent(root.transform);
      petal.transform.localPosition = new Vector3(
        Mathf.Cos(ang) * rad,
        0.4f + (float)rng.NextDouble() * 3.6f,
        Mathf.Sin(ang) * rad);
      petal.transform.localScale = new Vector3(0.26f, 0.02f, 0.18f);
      petal.transform.localRotation = Quaternion.Euler(40f, (float)rng.NextDouble() * 360f, 25f);
      petal.GetComponent<Renderer>().sharedMaterial = Lit(petals[i % petals.Length]);
      StripCollider(petal);
      IgnoreFromBuild(petal);
    }
    root.AddComponent<PetalFall>().Collect();
  }

  public static void Butterfly(Transform parent, string name, Vector3 center,
      float radius, float phase, Color bodyColor, Color wingColor) {
    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
    body.name = name;
    body.transform.SetParent(parent);
    body.transform.localPosition = center;
    body.transform.localScale = new Vector3(0.12f, 0.16f, 0.12f);
    body.GetComponent<Renderer>().sharedMaterial = Lit(bodyColor);
    StripCollider(body);
    IgnoreFromBuild(body);
    GameObject wingL = GameObject.CreatePrimitive(PrimitiveType.Cube);
    wingL.name = name + "WingL";
    wingL.transform.SetParent(body.transform);
    wingL.transform.localPosition = new Vector3(-0.24f, 0.04f, 0f);
    wingL.transform.localScale = new Vector3(0.42f, 0.03f, 0.30f);
    wingL.GetComponent<Renderer>().sharedMaterial = Lit(wingColor);
    StripCollider(wingL);
    IgnoreFromBuild(wingL);
    GameObject wingR = GameObject.CreatePrimitive(PrimitiveType.Cube);
    wingR.name = name + "WingR";
    wingR.transform.SetParent(body.transform);
    wingR.transform.localPosition = new Vector3(0.24f, 0.04f, 0f);
    wingR.transform.localScale = new Vector3(0.42f, 0.03f, 0.30f);
    wingR.GetComponent<Renderer>().sharedMaterial = Lit(wingColor);
    StripCollider(wingR);
    IgnoreFromBuild(wingR);
    ButterflyDrift drift = body.AddComponent<ButterflyDrift>();
    drift.center = center;
    drift.radius = radius;
    drift.phase = phase;
    drift.Wire(wingL.transform, wingR.transform);
  }

  // ---- per-world atmosphere (fog + ambient) -----------------------------------
  // MarketBootstrap applies these on travel/return so each world keeps its
  // own air: Main stays the crisp 18-45m haze, the Math island gets a soft
  // far haze (sky/clouds/rainbow readable), both Flat ambient (v6.4 look).

  public static void ApplyMainAtmosphere() {
    RenderSettings.fog = true;
    RenderSettings.fogMode = FogMode.Linear;
    RenderSettings.fogColor = new Color(0.75f, 0.88f, 0.96f);
    RenderSettings.fogStartDistance = 18f;
    RenderSettings.fogEndDistance = 45f;
    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    RenderSettings.ambientLight = new Color(0.68f, 0.71f, 0.75f);
    RenderSettings.ambientIntensity = 1f;
    TintSky(new Color(0.53f, 0.81f, 0.98f));
  }

  public static void ApplyMathAtmosphere() {
    RenderSettings.fog = true;
    RenderSettings.fogMode = FogMode.Linear;
    RenderSettings.fogColor = new Color(0.84f, 0.87f, 0.99f);
    RenderSettings.fogStartDistance = 32f;
    RenderSettings.fogEndDistance = 170f;
    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    RenderSettings.ambientLight = new Color(0.72f, 0.71f, 0.78f);
    RenderSettings.ambientIntensity = 1f;
    // Sakura sky (S7): the sky itself carries the pink gam.
    TintSky(new Color(0.72f, 0.84f, 0.99f));
  }

  // Scene camera background tint (both worlds own their sky). Null-safe: the
  // batch/test contexts may have no tagged camera.
  static void TintSky(Color c) {
    try {
      Camera cam = Camera.main;
      if (cam != null) cam.backgroundColor = c;
    } catch (System.Exception) { }
  }

  // ---- local helpers (shared material cache, collider/bake discipline) --------

  public static void Ball(Transform parent, string goName, Vector3 pos, float diameter, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    go.name = goName;
    go.transform.SetParent(parent);
    go.transform.localPosition = pos;
    go.transform.localScale = new Vector3(diameter, diameter, diameter);
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(go);
  }

  static void StripCollider(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (System.Exception) { }
  }

  static void IgnoreFromBuild(GameObject go) {
    if (go == null) return;
    try {
      Unity.AI.Navigation.NavMeshModifier mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (System.Exception) { }
  }

  static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();

  static Material Lit(Color color) {
    string key = color.r.ToString("F2") + "," + color.g.ToString("F2") + "," + color.b.ToString("F2");
    Material cached;
    if (_mats.TryGetValue(key, out cached) && cached != null) return cached;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    mat.enableInstancing = true;
    _mats[key] = mat;
    return mat;
  }
}
