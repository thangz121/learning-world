// A_World/PhonicsGarden/PhonicsPictograms.cs — Simple procedural ILLUSTRATIONS (pictograms)
// used as the visual-anchor fallback when no REAL photo is registered for a word.
// HONESTY: these are recognizable primitive-shape drawings for 3-6 year olds — they are
// ILLUSTRATIONS, never presented to the child or in telemetry as photographs.
// C# 9.0 only.
using UnityEngine;

public static class PhonicsPictograms {
  public static GameObject Build(Transform parent, string photoKeyOrWordId) {
    GameObject root = new GameObject("PhotoFramePictogram");
    root.transform.SetParent(parent, false);
    root.transform.localPosition = new Vector3(0f, 0f, 0.22f);

    string k = (photoKeyOrWordId ?? "").ToLowerInvariant();
    if (k.Contains("fish") || k.Contains("ca")) Fish(root);
    else if (k.Contains("cow") || k.Contains("bo")) Cow(root);
    else if (k.Contains("rooster") || k.Contains("ga")) Rooster(root);
    else if (k.Contains("orange") || k.Contains("cam")) OrangeFruit(root);
    else if (k.Contains("apple") || k.Contains("tao")) AppleFruit(root);
    else if (k.Contains("car") || k.Contains("xe")) Car(root);
    else if (k.Contains("train") || k.Contains("tau")) Train(root);
    else if (k.Contains("pen") || k.Contains("but")) Pen(root);
    else if (k.Contains("flower") || k.Contains("hoa")) Flower(root);
    else if (k.Contains("hand") || k.Contains("tay")) Hand(root);
    else if (k.Contains("mother") || k.Contains("grandmother") || k.Contains("ba") || k.Contains("me")) Face(root);
    else Ball(root);
    return root;
  }

  // ---- individual drawings (all primitive parts, colliders stripped) ----

  static void Fish(GameObject root) {
    Color bodyC = new Color(0.25f, 0.62f, 0.88f);
    Part(root, PrimitiveType.Sphere, "Body", new Vector3(0f, 0f, 0f), new Vector3(0.85f, 0.5f, 0.35f), bodyC);
    GameObject tail = Part(root, PrimitiveType.Cube, "Tail", new Vector3(-0.52f, 0f, 0f), new Vector3(0.3f, 0.3f, 0.12f), bodyC);
    tail.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    Part(root, PrimitiveType.Sphere, "EyeWhite", new Vector3(0.28f, 0.1f, 0.16f), Vector3.one * 0.12f, Color.white);
    Part(root, PrimitiveType.Sphere, "Eye", new Vector3(0.3f, 0.1f, 0.22f), Vector3.one * 0.06f, Color.black);
    Part(root, PrimitiveType.Cube, "Fin", new Vector3(0.02f, 0.26f, 0f), new Vector3(0.22f, 0.16f, 0.08f), new Color(0.95f, 0.85f, 0.35f));
  }

  static void Cow(GameObject root) {
    Color white = new Color(0.96f, 0.95f, 0.92f), brown = new Color(0.55f, 0.36f, 0.2f);
    Part(root, PrimitiveType.Cube, "Body", new Vector3(-0.05f, 0.02f, 0f), new Vector3(0.8f, 0.46f, 0.36f), white);
    Part(root, PrimitiveType.Cube, "Head", new Vector3(0.45f, 0.16f, 0f), new Vector3(0.36f, 0.34f, 0.32f), white);
    Part(root, PrimitiveType.Cube, "EarL", new Vector3(0.42f, 0.36f, 0.18f), new Vector3(0.12f, 0.1f, 0.08f), brown);
    Part(root, PrimitiveType.Cube, "EarR", new Vector3(0.42f, 0.36f, -0.18f), new Vector3(0.12f, 0.1f, 0.08f), brown);
    Part(root, PrimitiveType.Cube, "Muzzle", new Vector3(0.62f, 0.08f, 0f), new Vector3(0.14f, 0.16f, 0.2f), new Color(0.95f, 0.7f, 0.68f));
    Part(root, PrimitiveType.Sphere, "SpotL", new Vector3(-0.2f, 0.14f, 0.19f), new Vector3(0.22f, 0.16f, 0.06f), brown);
    Part(root, PrimitiveType.Sphere, "SpotR", new Vector3(0.1f, -0.05f, 0.18f), new Vector3(0.18f, 0.14f, 0.06f), brown);
    Part(root, PrimitiveType.Sphere, "EyeL", new Vector3(0.5f, 0.26f, 0.12f), Vector3.one * 0.055f, Color.black);
    Part(root, PrimitiveType.Sphere, "EyeR", new Vector3(0.5f, 0.26f, -0.12f), Vector3.one * 0.055f, Color.black);
    for (int i = 0; i < 4; i++) {
      float x = (i < 2 ? -0.28f : 0.26f);
      float z = (i % 2 == 0 ? 0.14f : -0.14f);
      Part(root, PrimitiveType.Cube, "Leg" + i, new Vector3(x, -0.33f, z), new Vector3(0.12f, 0.24f, 0.12f), brown);
    }
  }

  static void Rooster(GameObject root) {
    Color bodyC = new Color(0.85f, 0.42f, 0.22f), comb = new Color(0.9f, 0.2f, 0.2f);
    Part(root, PrimitiveType.Sphere, "Body", new Vector3(-0.08f, -0.05f, 0f), new Vector3(0.55f, 0.45f, 0.4f), bodyC);
    Part(root, PrimitiveType.Sphere, "Head", new Vector3(0.24f, 0.3f, 0f), Vector3.one * 0.3f, bodyC);
    Part(root, PrimitiveType.Sphere, "Comb1", new Vector3(0.16f, 0.48f, 0f), Vector3.one * 0.13f, comb);
    Part(root, PrimitiveType.Sphere, "Comb2", new Vector3(0.26f, 0.51f, 0f), Vector3.one * 0.13f, comb);
    Part(root, PrimitiveType.Sphere, "Comb3", new Vector3(0.36f, 0.46f, 0f), Vector3.one * 0.11f, comb);
    Part(root, PrimitiveType.Cube, "Beak", new Vector3(0.46f, 0.28f, 0f), new Vector3(0.14f, 0.08f, 0.1f), new Color(0.98f, 0.75f, 0.2f));
    Part(root, PrimitiveType.Sphere, "EyeL", new Vector3(0.34f, 0.36f, 0.12f), Vector3.one * 0.05f, Color.black);
    Part(root, PrimitiveType.Sphere, "EyeR", new Vector3(0.34f, 0.36f, -0.12f), Vector3.one * 0.05f, Color.black);
    GameObject t1 = Part(root, PrimitiveType.Cube, "Tail1", new Vector3(-0.5f, 0.22f, 0f), new Vector3(0.34f, 0.14f, 0.08f), new Color(0.3f, 0.6f, 0.85f));
    t1.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
    GameObject t2 = Part(root, PrimitiveType.Cube, "Tail2", new Vector3(-0.55f, 0.05f, 0f), new Vector3(0.36f, 0.14f, 0.08f), new Color(0.35f, 0.75f, 0.4f));
    t2.transform.localRotation = Quaternion.Euler(0f, 0f, -6f);
    GameObject t3 = Part(root, PrimitiveType.Cube, "Tail3", new Vector3(-0.5f, -0.14f, 0f), new Vector3(0.34f, 0.14f, 0.08f), new Color(0.9f, 0.75f, 0.25f));
    t3.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
    Part(root, PrimitiveType.Cube, "LegL", new Vector3(0f, -0.36f, 0.1f), new Vector3(0.06f, 0.22f, 0.06f), new Color(0.95f, 0.7f, 0.2f));
    Part(root, PrimitiveType.Cube, "LegR", new Vector3(0f, -0.36f, -0.1f), new Vector3(0.06f, 0.22f, 0.06f), new Color(0.95f, 0.7f, 0.2f));
  }

  static void OrangeFruit(GameObject root) {
    Part(root, PrimitiveType.Sphere, "Fruit", Vector3.zero, Vector3.one * 0.68f, new Color(0.98f, 0.62f, 0.16f));
    Part(root, PrimitiveType.Cylinder, "Stem", new Vector3(0f, 0.38f, 0f), new Vector3(0.05f, 0.08f, 0.05f), new Color(0.4f, 0.26f, 0.12f));
    GameObject leaf = Part(root, PrimitiveType.Sphere, "Leaf", new Vector3(0.12f, 0.42f, 0f), new Vector3(0.22f, 0.05f, 0.12f), new Color(0.3f, 0.65f, 0.32f));
    leaf.transform.localRotation = Quaternion.Euler(0f, 20f, -15f);
    Part(root, PrimitiveType.Sphere, "Glint", new Vector3(-0.18f, 0.14f, 0.27f), Vector3.one * 0.1f, new Color(1f, 0.85f, 0.6f));
  }

  static void AppleFruit(GameObject root) {
    Part(root, PrimitiveType.Sphere, "Fruit", Vector3.zero, new Vector3(0.66f, 0.62f, 0.66f), new Color(0.9f, 0.22f, 0.24f));
    Part(root, PrimitiveType.Cylinder, "Stem", new Vector3(0f, 0.36f, 0f), new Vector3(0.045f, 0.07f, 0.045f), new Color(0.38f, 0.24f, 0.12f));
    GameObject leaf = Part(root, PrimitiveType.Sphere, "Leaf", new Vector3(0.13f, 0.4f, 0f), new Vector3(0.2f, 0.05f, 0.11f), new Color(0.3f, 0.65f, 0.32f));
    leaf.transform.localRotation = Quaternion.Euler(0f, 25f, -18f);
  }

  static void Car(GameObject root) {
    Color bodyC = new Color(0.9f, 0.3f, 0.3f), dark = new Color(0.2f, 0.2f, 0.24f);
    Part(root, PrimitiveType.Cube, "Body", new Vector3(0f, -0.08f, 0f), new Vector3(0.9f, 0.28f, 0.4f), bodyC);
    Part(root, PrimitiveType.Cube, "Cabin", new Vector3(-0.08f, 0.14f, 0f), new Vector3(0.44f, 0.24f, 0.34f), bodyC);
    Part(root, PrimitiveType.Cube, "WindowF", new Vector3(0.16f, 0.14f, 0f), new Vector3(0.06f, 0.18f, 0.3f), new Color(0.65f, 0.85f, 0.95f));
    Part(root, PrimitiveType.Cube, "WindowS", new Vector3(-0.34f, 0.14f, 0f), new Vector3(0.06f, 0.18f, 0.3f), new Color(0.65f, 0.85f, 0.95f));
    Part(root, PrimitiveType.Cylinder, "WheelL", new Vector3(-0.26f, -0.26f, 0f), new Vector3(0.22f, 0.06f, 0.22f), dark).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
    Part(root, PrimitiveType.Cylinder, "WheelR", new Vector3(0.26f, -0.26f, 0f), new Vector3(0.22f, 0.06f, 0.22f), dark).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
  }

  static void Train(GameObject root) {
    Color bodyC = new Color(0.22f, 0.5f, 0.85f), dark = new Color(0.2f, 0.2f, 0.24f);
    Part(root, PrimitiveType.Cube, "Body", new Vector3(-0.02f, -0.05f, 0f), new Vector3(0.85f, 0.34f, 0.36f), bodyC);
    Part(root, PrimitiveType.Cube, "Cabin", new Vector3(-0.22f, 0.22f, 0f), new Vector3(0.34f, 0.3f, 0.34f), new Color(0.9f, 0.35f, 0.3f));
    Part(root, PrimitiveType.Cylinder, "Stack", new Vector3(0.24f, 0.26f, 0f), new Vector3(0.14f, 0.14f, 0.14f), dark);
    Part(root, PrimitiveType.Sphere, "Dome", new Vector3(0.05f, 0.22f, 0f), Vector3.one * 0.16f, new Color(0.98f, 0.82f, 0.25f));
    Part(root, PrimitiveType.Sphere, "Lamp", new Vector3(0.44f, 0.02f, 0f), Vector3.one * 0.1f, new Color(0.98f, 0.9f, 0.4f));
    Part(root, PrimitiveType.Cube, "Cowcatcher", new Vector3(0.46f, -0.2f, 0f), new Vector3(0.1f, 0.14f, 0.3f), new Color(0.5f, 0.5f, 0.55f));
    Part(root, PrimitiveType.Cylinder, "Wheel1", new Vector3(-0.26f, -0.26f, 0f), new Vector3(0.16f, 0.05f, 0.16f), dark).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
    Part(root, PrimitiveType.Cylinder, "Wheel2", new Vector3(0.12f, -0.26f, 0f), new Vector3(0.16f, 0.05f, 0.16f), dark).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
    Part(root, PrimitiveType.Cylinder, "Wheel3", new Vector3(0.34f, -0.26f, 0f), new Vector3(0.12f, 0.05f, 0.12f), dark).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
  }

  static void Pen(GameObject root) {
    GameObject body = Part(root, PrimitiveType.Cylinder, "Barrel", Vector3.zero, new Vector3(0.1f, 0.55f, 0.1f), new Color(0.25f, 0.5f, 0.9f));
    body.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
    GameObject tip = Part(root, PrimitiveType.Cylinder, "Tip", new Vector3(-0.19f, -0.5f, 0f), new Vector3(0.065f, 0.1f, 0.065f), new Color(0.9f, 0.8f, 0.6f));
    tip.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
    GameObject clip = Part(root, PrimitiveType.Cube, "Clip", new Vector3(0.1f, 0.5f, 0f), new Vector3(0.04f, 0.2f, 0.08f), new Color(0.85f, 0.85f, 0.9f));
    clip.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
  }

  static void Flower(GameObject root) {
    Part(root, PrimitiveType.Cylinder, "Stem", new Vector3(0f, -0.18f, 0f), new Vector3(0.05f, 0.4f, 0.05f), new Color(0.3f, 0.65f, 0.32f));
    Part(root, PrimitiveType.Sphere, "Core", new Vector3(0f, 0.28f, 0f), Vector3.one * 0.24f, new Color(0.98f, 0.82f, 0.25f));
    for (int i = 0; i < 5; i++) {
      float a = i * Mathf.PI * 2f / 5f;
      Part(root, PrimitiveType.Sphere, "Petal" + i,
        new Vector3(Mathf.Cos(a) * 0.26f, 0.28f + Mathf.Sin(a) * 0.26f, 0f),
        new Vector3(0.22f, 0.22f, 0.1f), new Color(0.95f, 0.45f, 0.65f));
    }
    GameObject leaf = Part(root, PrimitiveType.Sphere, "Leaf", new Vector3(0.14f, -0.28f, 0f), new Vector3(0.22f, 0.05f, 0.1f), new Color(0.3f, 0.65f, 0.32f));
    leaf.transform.localRotation = Quaternion.Euler(0f, 0f, -25f);
  }

  static void Hand(GameObject root) {
    Color skin = new Color(0.96f, 0.78f, 0.62f);
    Part(root, PrimitiveType.Cube, "Palm", new Vector3(0f, -0.12f, 0f), new Vector3(0.5f, 0.4f, 0.16f), skin);
    for (int i = 0; i < 4; i++) {
      float x = -0.19f + i * 0.13f;
      float h = i == 1 || i == 2 ? 0.3f : 0.24f;
      Part(root, PrimitiveType.Cube, "Finger" + i, new Vector3(x, 0.2f + h * 0.5f - 0.1f, 0f), new Vector3(0.1f, h, 0.13f), skin);
    }
    GameObject thumb = Part(root, PrimitiveType.Cube, "Thumb", new Vector3(-0.32f, 0.02f, 0f), new Vector3(0.22f, 0.1f, 0.13f), skin);
    thumb.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
  }

  static void Face(GameObject root) {
    Color skin = new Color(0.96f, 0.78f, 0.62f);
    Part(root, PrimitiveType.Sphere, "Head", new Vector3(0f, -0.05f, 0f), new Vector3(0.66f, 0.62f, 0.6f), skin);
    Part(root, PrimitiveType.Sphere, "Hair", new Vector3(0f, 0.22f, 0f), new Vector3(0.6f, 0.34f, 0.55f), new Color(0.4f, 0.3f, 0.26f));
    Part(root, PrimitiveType.Sphere, "EyeL", new Vector3(-0.16f, -0.02f, 0.26f), Vector3.one * 0.07f, Color.black);
    Part(root, PrimitiveType.Sphere, "EyeR", new Vector3(0.16f, -0.02f, 0.26f), Vector3.one * 0.07f, Color.black);
    Part(root, PrimitiveType.Sphere, "CheekL", new Vector3(-0.26f, -0.18f, 0.22f), new Vector3(0.12f, 0.08f, 0.06f), new Color(0.95f, 0.6f, 0.6f));
    Part(root, PrimitiveType.Sphere, "CheekR", new Vector3(0.26f, -0.18f, 0.22f), new Vector3(0.12f, 0.08f, 0.06f), new Color(0.95f, 0.6f, 0.6f));
    Part(root, PrimitiveType.Sphere, "Smile", new Vector3(0f, -0.2f, 0.28f), new Vector3(0.2f, 0.05f, 0.05f), new Color(0.75f, 0.35f, 0.35f));
  }

  static void Ball(GameObject root) {
    Part(root, PrimitiveType.Sphere, "Ball", Vector3.zero, Vector3.one * 0.6f, new Color(0.45f, 0.65f, 0.9f));
    Part(root, PrimitiveType.Sphere, "Patch", new Vector3(0.14f, 0.1f, 0.22f), Vector3.one * 0.18f, new Color(0.95f, 0.85f, 0.4f));
    Part(root, PrimitiveType.Sphere, "Spark", new Vector3(-0.34f, 0.3f, 0f), Vector3.one * 0.08f, new Color(1f, 1f, 1f));
  }

  // ---- helpers ----

  static GameObject Part(GameObject root, PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Color color) {
    GameObject go = GameObject.CreatePrimitive(type);
    go.name = name;
    go.transform.SetParent(root.transform, false);
    go.transform.localPosition = localPos;
    go.transform.localScale = localScale;
    Renderer r = go.GetComponent<Renderer>();
    if (r != null) {
      Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      m.SetColor("_BaseColor", color);
      if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.25f);
      r.sharedMaterial = m;
    }
    Collider c = go.GetComponent<Collider>();
    if (c != null) CharacterPresentation.DestroyNow(c);
    try {
      Unity.AI.Navigation.NavMeshModifier mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (System.Exception) { }
    return go;
  }
}
