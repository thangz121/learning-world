// A_World/Resources-adjacent NatureKit.cs — Agent A (World & Visual).
// Code-side loader for the Quaternius CC0 nature FBX
// (Assets/A_World/Resources/NatureKit/*.fbx, provenance in README.md).
// Deterministic placement (position/yaw/scale args, no Random): every build
// identical. Imported meshes carry NO colliders (click-through by construction;
// feet route around trunks/rocks through the BAKED meshes — the runtime bake
// rasterizes render meshes, so solid pieces behave as obstacles without any
// runtime carve). Materials convert to URP Lit at placement (the 2018 FBX
// materials target the built-in pipeline): flat-colour diffusions carry over
// via _BaseColor; batch EditMode (no render device) keeps imports untouched.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

public static class NatureKit {
  static readonly Dictionary<string, Material> _litCache = new Dictionary<string, Material>();

  // Place one nature model under parent at a LOCAL position. Returns the
  // instance root (named MathQ* by callers for test prefixes), or null when
  // the asset is missing (fail-soft: world stays playable, never NREs).
  public static GameObject Place(Transform parent, string fbxName, Vector3 localPos, float yawDeg, float scale) {
    if (parent == null || string.IsNullOrEmpty(fbxName)) return null;
    GameObject asset = null;
    try { asset = Resources.Load<GameObject>("NatureKit/" + fbxName); } catch (System.Exception) { }
    if (asset == null) {
      try { Debug.LogWarning("[NatureKit] Missing NatureKit/" + fbxName + "; skipping.", parent); }
      catch (System.Exception) { }
      return null;
    }
    GameObject go = null;
    try { go = Object.Instantiate(asset, parent, false); } catch (System.Exception) { }
    if (go == null) return null;
    go.transform.localPosition = localPos;
    go.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
    go.transform.localScale = new Vector3(scale, scale, scale);
    // Blender scene furniture: the 2018 FBX carry the author's Camera + Lamp
    // nodes. They are not decor (P41E caught a Camera at |x|=18.7): strip the
    // whole node so only meshes remain. Edit-safe via the shared helper.
    foreach (Camera c in go.GetComponentsInChildren<Camera>(true)) {
      if (c != null) CharacterPresentation.DestroyNow(c.gameObject);
    }
    foreach (Light l in go.GetComponentsInChildren<Light>(true)) {
      if (l != null) CharacterPresentation.DestroyNow(l.gameObject);
    }
    ConvertMaterials(go);
    return go;
  }

  static void ConvertMaterials(GameObject root) {
    Shader urp = null;
    try { urp = Shader.Find("Universal Render Pipeline/Lit"); } catch (System.Exception) { }
    if (urp == null) return; // batch EditMode: leave imports untouched (structure tests only)
    Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
    if (renderers == null) return;
    foreach (Renderer r in renderers) {
      if (r == null) continue;
      Material[] srcs = r.sharedMaterials;
      if (srcs == null || srcs.Length == 0) continue;
      Material[] dsts = new Material[srcs.Length];
      for (int i = 0; i < srcs.Length; i++) {
        dsts[i] = LitFor(srcs[i], urp);
      }
      try { r.sharedMaterials = dsts; } catch (System.Exception) { }
    }
  }

  static Material LitFor(Material src, Shader urp) {
    Color c = Color.white;
    string key = "white";
    try {
      if (src != null && src.HasProperty("_Color")) c = src.color;
      else if (src != null) c = src.color;
    } catch (System.Exception) { }
    key = c.r.ToString("F2") + "," + c.g.ToString("F2") + "," + c.b.ToString("F2");
    Material cached;
    if (_litCache.TryGetValue(key, out cached) && cached != null) return cached;
    Material mat = new Material(urp);
    try { mat.SetColor("_BaseColor", c); } catch (System.Exception) { }
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    _litCache[key] = mat;
    return mat;
  }
}
