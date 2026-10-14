// A_World/CountingGarden/GardenPrimitives.cs — shared number/digit primitives.
// Extracted from the deleted CountingGardenBuilder (PHASE 5 legacy purge): the
// two approved arenas (rabbit + stairs) and CT-P54 still build seven-segment
// digits, check marks and the shared step-wood tone. Same geometry contract.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

public static class GardenPrimitives {
  // Gameplay #2 (stair hill) shared tone — same warm wood language as the beds.
  public static readonly Color StepWood = new Color(0.62f, 0.45f, 0.28f);

  // CONNECTED seven-segment digits 0-9 — one table drives every number board.
  // Standard 7-seg sets, index = digit (out of range reads as 8).
  public static readonly string[][] DigitSegSets = {
    new[] { "A", "B", "C", "D", "E", "F" },       // 0
    new[] { "B", "C" },                            // 1
    new[] { "A", "B", "G", "E", "D" },             // 2
    new[] { "A", "B", "G", "C", "D" },             // 3
    new[] { "B", "C", "F", "G" },                  // 4
    new[] { "A", "C", "D", "F", "G" },             // 5
    new[] { "A", "C", "D", "E", "F", "G" },        // 6
    new[] { "A", "B", "C" },                       // 7
    new[] { "A", "B", "C", "D", "E", "F", "G" },   // 8
    new[] { "A", "B", "C", "D", "F", "G" },        // 9
  };

  public static GameObject Digit(Transform parent, string name, Vector3 origin, float h, float w,
      Color color, float yawDeg, int n) {
    GameObject g = new GameObject(name);
    g.transform.SetParent(parent, false);
    g.transform.localPosition = origin;
    g.transform.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
    float t = Mathf.Max(0.09f, h * 0.19f);          // stroke thickness
    float hLen = w + t;                              // horizontal segments
    float vLen = h * 0.5f + t;                       // vertical segments (overlap)
    string[] set = (n >= 0 && n < DigitSegSets.Length) ? DigitSegSets[n] : DigitSegSets[8];
    foreach (string s in set) {
      switch (s) {
        case "A":
          Box(g.transform, name + "A", new Vector3(0f, h, 0f),
            new Vector3(t * 1.00f, t, hLen), color);
          break;
        case "B":
          Box(g.transform, name + "B", new Vector3(0f, h * 0.75f, w * 0.5f),
            new Vector3(t * 0.90f, vLen, t), color);
          break;
        case "C":
          Box(g.transform, name + "C", new Vector3(0f, h * 0.25f, w * 0.5f),
            new Vector3(t * 0.94f, vLen, t), color);
          break;
        case "D":
          Box(g.transform, name + "D", new Vector3(0f, 0f, 0f),
            new Vector3(t * 1.03f, t, hLen), color);
          break;
        case "E":
          Box(g.transform, name + "E", new Vector3(0f, h * 0.25f, -w * 0.5f),
            new Vector3(t * 0.94f, vLen, t), color);
          break;
        case "F":
          Box(g.transform, name + "F", new Vector3(0f, h * 0.75f, -w * 0.5f),
            new Vector3(t * 0.90f, vLen, t), color);
          break;
        case "G":
          Box(g.transform, name + "G", new Vector3(0f, h * 0.5f, 0f),
            new Vector3(t * 1.06f, t, hLen), color);
          break;
      }
    }
    return g;
  }

  // Group origin at the base so emphasis pulses grow upward.
  public static GameObject Digit2(Transform parent, string name, Vector3 origin, float h, float w,
      Color color, float yawDeg) {
    return Digit(parent, name, origin, h, w, color, yawDeg, 2);
  }

  // CONNECTED seven-segment "3" (A/B/G/C/D) — kept as a named wrapper so the
  // gameplay #1/#2 pins keep reading; new code uses Digit(..., n).
  public static GameObject Digit3(Transform parent, string name, Vector3 origin, float h, float w,
      Color color, float yawDeg) {
    return Digit(parent, name, origin, h, w, color, yawDeg, 3);
  }

  // Floor tick: short down-stroke + long up-stroke in the ZY plane.
  public static void CheckMark(Transform parent, string name, Vector3 origin, float size, Color color) {
    GameObject s1 = Box(parent, name + "Stem", origin + new Vector3(0f, size * 0.4f, size * 0.25f),
      new Vector3(0.11f, 0.11f, size * 0.55f), color);
    s1.transform.localRotation = Quaternion.Euler(-55f, 0f, 0f);
    GameObject s2 = Box(parent, name + "Arm", origin + new Vector3(0f, size * 0.75f, -size * 0.25f),
      new Vector3(0.11f, 0.11f, size * 0.95f), color);
    s2.transform.localRotation = Quaternion.Euler(48f, 0f, 0f);
  }

  static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    go.transform.localScale = scale;
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(go);
    return go;
  }

  static void StripCollider(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
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
