// A_World/NatureLibrary.cs — premium nature integration (Quaternius CC0 subset).
// Loads FBX prefabs from Resources/Nature ONCE; Spawn() instantiates with:
//   - runtime height normalization (authoring units vary per model)
//   - slot-name palette harmonization onto SHARED matte URP/Lit materials
//   - colliders stripped (decor never touches NavMesh/clicks)
// Imports stay pristine (see Resources/Nature/README.md). Callers own
// placement/seeds/clear-zones; null-safe (caller falls back to primitives).
// C# 9.0 only. No services, no events, no audio.
using System.Collections.Generic;
using UnityEngine;

public static class NatureLibrary {
  // Fixed harmonized slots (warm browns, light stone, storybook toadstools).
  static readonly Dictionary<string, Color> SlotBase = new Dictionary<string, Color> {
    { "Wood", new Color(0.45f, 0.30f, 0.16f) },
    { "LightWood", new Color(0.62f, 0.44f, 0.26f) },
    { "Rock", new Color(0.55f, 0.55f, 0.58f) },
    { "Mushroom_Top", new Color(0.80f, 0.30f, 0.28f) },
    { "Mushroom_Bottom", new Color(0.93f, 0.87f, 0.78f) },
  };

  static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();
  static readonly Dictionary<string, Material> SlotMats = new Dictionary<string, Material>();

  // Diagnostics (read by the temp verify driver; zero-cost otherwise).
  public static int SpawnOk;
  public static int SpawnFail;

  public static GameObject Load(string prefabName) {
    if (PrefabMissing(prefabName) == false && Prefabs.TryGetValue(prefabName, out GameObject hit))
      return hit;
    GameObject prefab = Resources.Load<GameObject>("Nature/" + prefabName);
    if (prefab == null) return null;
    Prefabs[prefabName] = prefab;
    return prefab;
  }

  static bool PrefabMissing(string prefabName) { return !Prefabs.ContainsKey(prefabName); }

  // Spawn a harmonized instance. foliage tints Green/Leaves slots,
  // foliageDark tints DarkGreen, accent1 tints Berry/Cyan, accent2 Yellow.
  // doubleSided (thin blades/petals) uses Cull-Off variants so backfaces
  // catch light instead of rendering black. Returns null when the prefab is
  // unavailable (caller falls back).
  public static GameObject Spawn(Transform parent, string prefabName, Vector3 pos,
      float yawDeg, float targetHeight, Color foliage, Color accent1, Color accent2,
      bool doubleSided = false) {
    GameObject prefab = Load(prefabName);
    if (prefab == null) { SpawnFail++; return null; }
    SpawnOk++;
    Color foliageDark = new Color(foliage.r * 0.78f, foliage.g * 0.82f, foliage.b * 0.80f);
    GameObject go = Object.Instantiate(prefab, pos, Quaternion.Euler(0f, yawDeg, 0f));
    go.transform.SetParent(parent, true);
    // Height normalization on the DEPLOYED instance (active renderers report
    // valid world bounds; converges from any authoring unit in ≤4 passes).
    go.transform.localScale = Vector3.one;
    for (int k = 0; k < 4; k++) {
      float deployed = DeployedHeight(go);
      if (deployed < 0.001f) break;
      float corr = Mathf.Clamp(targetHeight / deployed, 0.05f, 20f);
      go.transform.localScale = go.transform.localScale * corr;
      if (Mathf.Abs(corr - 1f) < 0.02f) break;
    }
    foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true)) {
      Material[] slots = r.sharedMaterials;
      bool dirty = false;
      for (int i = 0; i < slots.Length; i++) {
        Material mapped = MapSlot(slots[i] != null ? slots[i].name : "", foliage, foliageDark, accent1, accent2, doubleSided);
        if (mapped != null) { slots[i] = mapped; dirty = true; }
      }
      if (dirty) r.sharedMaterials = slots;
    }
    foreach (Collider c in go.GetComponentsInChildren<Collider>(true))
      Object.Destroy(c);
    return go;
  }

  static float DeployedHeight(GameObject go) {
    Bounds b = new Bounds(go.transform.position, Vector3.zero);
    bool any = false;
    foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) {
      if (any) b.Encapsulate(r.bounds);
      else { b = r.bounds; any = true; }
    }
    return any ? b.size.y : 0f;
  }

  static Material MapSlot(string slot, Color foliage, Color foliageDark, Color a1, Color a2, bool doubleSided) {
    string clean = slot.Replace(" (Instance)", "");
    if (SlotBase.TryGetValue(clean, out Color fixedColor))
      return Lit(clean, fixedColor, false); // closed solids: single-sided
    if (clean.Contains("DarkGreen")) return Lit("foliageDark", foliageDark, doubleSided);
    if (clean.Contains("Green")) return Lit("foliage", foliage, doubleSided);
    if (clean.Contains("Leaves")) return Lit("foliage", foliage, doubleSided);
    if (clean.Contains("Berry")) return Lit("accent1", a1, doubleSided);
    if (clean.Contains("Cyan")) return Lit("accent1", a1, doubleSided);
    if (clean.Contains("Yellow")) return Lit("accent2", a2, doubleSided);
    return null; // unknown slot: keep the imported material
  }

  static Material Lit(string key, Color color, bool doubleSided = false) {
    string cacheKey = (doubleSided ? "ds" : "ss") + key + color.r.ToString("F2") + color.g.ToString("F2") + color.b.ToString("F2");
    if (SlotMats.TryGetValue(cacheKey, out Material hit) && hit != null) return hit;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    if (doubleSided) mat.SetInt("_Cull", 0); // Cull Off: thin blades lit both sides
    mat.enableInstancing = true;
    SlotMats[cacheKey] = mat;
    return mat;
  }
}
