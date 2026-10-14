// A_World/PropKit.cs — P3.0.1.1 B1R. Kenney CC0 kit loader (Food Kit +
// Nature Kit subset in Resources/PropKit, provenance in
// Assets/Documentation/ENVIRONMENT_ASSET_SOURCES.md).
// Responsibilities:
//   - deterministic placement (pos/yaw/scale args, no Random)
//   - material harmonization by MATERIAL NAME onto the LW palette so the
//     kit's teal-green/neon wood never clashes with the flat-shade world;
//     textured materials (Food Kit colormap) are preserved untouched
//   - colliders stripped (click-through; feet route around baked meshes)
//   - optional NavMeshModifier.ignoreFromBuild for pieces that must never
//     block the bake (visual bridge modules over the walkable deck)
//   - null-safe: a missing asset logs one warning and returns null.
// C# 9.0 only.
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

public static class PropKit {
  static readonly Dictionary<string, Material> _mats = new Dictionary<string, Material>();
  static readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();

  public static GameObject Place(Transform parent, string propName, Vector3 localPos,
      float yawDeg, float scale, bool ignoreFromBuild = false) {
    if (parent == null || string.IsNullOrEmpty(propName)) return null;
    GameObject asset = Load(propName);
    if (asset == null) return null;
    GameObject go = null;
    try { go = Object.Instantiate(asset, parent, false); } catch (System.Exception) { }
    if (go == null) return null;
    go.name = propName;
    go.transform.localPosition = localPos;
    go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
    go.transform.localScale = new Vector3(scale, scale, scale);
    Strip(go);
    Harmonize(go, propName);
    if (ignoreFromBuild) IgnoreFromBuild(go);
    return go;
  }

  // Places a prop normalized to targetSize, horizontally centered on localPos.x/z,
  // with its lowest vertex resting at localPos.y. Automatically fixes non-centered mesh pivots.
  public static GameObject PlaceFitted(Transform parent, string propName, Vector3 localPos,
      float yawDeg, float targetSize, bool ignoreFromBuild = false) {
    if (parent == null || string.IsNullOrEmpty(propName)) return null;
    GameObject container = new GameObject(propName);
    container.transform.SetParent(parent, false);
    container.transform.localPosition = localPos;
    container.transform.localRotation = Quaternion.identity;
    container.transform.localScale = Vector3.one;

    GameObject go = Place(container.transform, propName, Vector3.zero, yawDeg, 1f, ignoreFromBuild);
    if (go == null) {
      CharacterPresentation.DestroyNow(container);
      return null;
    }

    Renderer[] rends = go.GetComponentsInChildren<Renderer>();
    if (rends != null && rends.Length > 0) {
      Bounds b = rends[0].bounds;
      for (int i = 1; i < rends.Length; i++) {
        if (rends[i] != null) b.Encapsulate(rends[i].bounds);
      }
      float maxDim = Mathf.Max(b.size.x, b.size.y, b.size.z);
      float scale = maxDim > 0.001f ? (targetSize / maxDim) : 1f;
      go.transform.localScale = new Vector3(scale, scale, scale);

      Bounds sb = rends[0].bounds;
      for (int i = 1; i < rends.Length; i++) {
        if (rends[i] != null) sb.Encapsulate(rends[i].bounds);
      }
      Vector3 centerInContainer = container.transform.InverseTransformPoint(sb.center);
      Vector3 minInContainer = container.transform.InverseTransformPoint(new Vector3(sb.center.x, sb.min.y, sb.center.z));
      go.transform.localPosition = new Vector3(-centerInContainer.x, -minInContainer.y, -centerInContainer.z);
    }
    return container;
  }

  static GameObject Load(string propName) {
    GameObject hit;
    if (_prefabs.TryGetValue(propName, out hit) && hit != null) return hit;
    GameObject prefab = null;
    try { prefab = Resources.Load<GameObject>("PropKit/" + propName); } catch (System.Exception) { }
    if (prefab == null) {
      try { Debug.LogWarning("[PropKit] Missing PropKit/" + propName + "; skipping."); } catch (System.Exception) { }
      return null;
    }
    _prefabs[propName] = prefab;
    return prefab;
  }

  static void Strip(GameObject go) {
    foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) {
      if (c != null) CharacterPresentation.DestroyNow(c);
    }
    foreach (Camera c in go.GetComponentsInChildren<Camera>(true)) {
      if (c != null) CharacterPresentation.DestroyNow(c.gameObject);
    }
    foreach (Light l in go.GetComponentsInChildren<Light>(true)) {
      if (l != null) CharacterPresentation.DestroyNow(l.gameObject);
    }
  }

  static void IgnoreFromBuild(GameObject go) {
    try {
      NavMeshModifier mod = go.AddComponent<NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (System.Exception) { }
  }

  // ---- material harmonization (name -> palette) ------------------------------
  // Rules are ordered; the first match wins. Textured colormap materials are
  // preserved (Food Kit palette is flat colours; it already matches the world).

  static void Harmonize(GameObject go, string propName) {
    Shader urp = null;
    try { urp = Shader.Find("Universal Render Pipeline/Lit"); } catch (System.Exception) { }
    foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true)) {
      if (r == null) continue;
      Material[] srcs = r.sharedMaterials;
      if (srcs == null || srcs.Length == 0) continue;
      Material[] dsts = new Material[srcs.Length];
      for (int i = 0; i < srcs.Length; i++) dsts[i] = Map(srcs[i], propName, urp);
      try { r.sharedMaterials = dsts; } catch (System.Exception) { }
    }
  }

  static Material Map(Material src, string propName, Shader urp) {
    if (src == null || urp == null) return src;
    bool textured = false;
    try { textured = src.HasProperty("_BaseMap") && src.GetTexture("_BaseMap") != null; } catch (System.Exception) { }
    if (textured) return src; // Food Kit colormap: keep texture + import material
    string n = (src.name ?? "").ToLowerInvariant();
    Color c;
    if (n.Contains("leafsgreen") || n.Contains("grass")) c = new Color(0.30f, 0.60f, 0.30f);
    else if (n.Contains("leafsdark")) c = new Color(0.22f, 0.50f, 0.28f);
    else if (n.Contains("woodbarkdark")) c = new Color(0.42f, 0.28f, 0.16f);
    else if (n.Contains("woodbark") || n.Contains("woodinner")) c = new Color(0.62f, 0.44f, 0.26f);
    else if (n.Contains("wooddark")) c = new Color(0.50f, 0.34f, 0.20f);
    else if (n.Contains("wood")) c = new Color(0.58f, 0.42f, 0.24f);
    else if (n.Contains("stonedark")) c = new Color(0.55f, 0.55f, 0.58f);
    else if (n.Contains("stone")) c = new Color(0.68f, 0.68f, 0.66f);
    else if (n.Contains("dirt")) c = new Color(0.55f, 0.40f, 0.26f);
    else if (n.Contains("colorred")) c = new Color(0.85f, 0.25f, 0.25f);
    else if (n.Contains("coloryellow")) c = new Color(0.98f, 0.78f, 0.25f);
    else if (n.Contains("colorpurple")) c = new Color(0.70f, 0.55f, 0.90f);
    else if (n.Contains("_defaultmat")) {
      c = (propName != null && propName.StartsWith("tree"))
        ? new Color(0.62f, 0.44f, 0.26f) : new Color(0.66f, 0.66f, 0.64f);
    } else {
      try { c = src.color; } catch (System.Exception) { c = Color.white; }
    }
    string key = c.r.ToString("F2") + "," + c.g.ToString("F2") + "," + c.b.ToString("F2");
    Material cached;
    if (_mats.TryGetValue(key, out cached) && cached != null) return cached;
    Material mat = new Material(urp);
    try { mat.SetColor("_BaseColor", c); } catch (System.Exception) { }
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    mat.enableInstancing = true;
    _mats[key] = mat;
    return mat;
  }
}
