// A_World/ClassificationCity/ClassificationCityBuilder.cs — "Thành Phố Phân Loại".
// One connected sorting town, short walks. Presentation only. C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ClassificationCityBuilder : MonoBehaviour {
  public const string SceneName = "ClassificationCityPlayScene";
  public static readonly Vector3 WorldOffset = new Vector3(780f, 0f, 0f);
  public const float BoundX = 14f;
  public const float BoundZ = 14f;
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -10.8f);
  public static readonly Vector3 FollowOffset = new Vector3(0f, 2.8f, -4.2f);
  public static readonly Vector3 PlaySpotLocal = new Vector3(0f, 0f, 0.2f);
  public static readonly Vector3 SlotA = new Vector3(-2.6f, 0f, 4.4f);
  public static readonly Vector3 SlotB = new Vector3(2.6f, 0f, 4.4f);
  public static readonly Vector3 SlotC = new Vector3(0f, 0f, 6.2f);
  public const string ObjectiveEn = "Sort the town";
  public const string ObjectiveVi = "Thành phố phân loại";

  static readonly Color Lawn = new Color(0.44f, 0.70f, 0.42f);
  static readonly Color Sand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color Wood = new Color(0.55f, 0.40f, 0.24f);
  static readonly Color Cream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color Brick = new Color(0.85f, 0.45f, 0.35f);
  static readonly Color RoofGreen = new Color(0.30f, 0.60f, 0.38f);
  static readonly Color Leaf = new Color(0.45f, 0.70f, 0.42f);

  public static readonly Color[] Palette = {
    new Color(0.92f, 0.32f, 0.34f),
    new Color(0.28f, 0.52f, 0.92f),
    new Color(0.98f, 0.82f, 0.22f),
    new Color(0.28f, 0.72f, 0.38f),
  };

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public Transform PlaySpot { get; private set; }
  public Transform RoundRoot { get; private set; }
  public readonly List<ClassificationBin> Bins = new List<ClassificationBin>();
  public readonly List<ClassificationItem> Items = new List<ClassificationItem>();

  public void Build() {
    BuildContent(transform);
    BuildNavMesh(transform);
  }

  void BuildNavMesh(Transform parent) {
    Unity.AI.Navigation.NavMeshSurface surface =
      parent.gameObject.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
    if (surface == null) surface = parent.gameObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
    surface.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
    surface.BuildNavMesh();
  }

  public void BuildContent(Transform root) {
    BuildGround(root);
    BuildEntryAndExit(root);
    BuildBackdrops(root);
    GameObject rr = new GameObject("CCRound");
    rr.transform.SetParent(root, false);
    RoundRoot = rr.transform;
    BuildDressing(root);
    BuildAnchors(root);
    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  void BuildGround(Transform parent) {
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "CCRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(36f, 1.4f, 36f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    Strip(rim);
    Ignore(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "CCGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    Pad(parent, "CCPlaza", new Vector3(0f, 0.006f, 2.6f), 11f, new Color(0.93f, 0.84f, 0.58f));
    Box(parent, "CCPath", new Vector3(0f, 0.01f, -4.6f), new Vector3(1.8f, 0.02f, 8.4f),
      new Color(0.76f, 0.60f, 0.40f));
    StageLight(parent, new Vector3(0f, 7.5f, 2.8f));
    // USER ROUND 2026-10-01: the big glowing floor disc was removed (annoying
    // blob under the child).
  }

  void BuildEntryAndExit(Transform parent) {
    Box(parent, "CCEntryPostL", new Vector3(-1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Wood);
    Box(parent, "CCEntryPostR", new Vector3(1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Wood);
    GameObject beam = Box(parent, "CCEntryBeam", new Vector3(0f, 2.08f, -9.2f),
      new Vector3(3.2f, 0.16f, 0.16f), Cream);
    Ignore(beam);
    Pad(parent, "CCPlayDisc", PlaySpotLocal + new Vector3(0f, 0.012f, 0f), 3.0f, Gold);
    GameObject spot = new GameObject("CCPlaySpot");
    spot.transform.SetParent(parent, false);
    spot.transform.localPosition = PlaySpotLocal;
    PlaySpot = spot.transform;
    Pad(parent, "CCExitDisc", ExitLocal + new Vector3(0f, 0.012f, 0.3f), 3.0f,
      new Color(0.70f, 0.90f, 0.72f));
    GameObject exitGo = new GameObject("CCExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.PlayExit = true;
    exit.fireRadius = 1.35f;
    ExitPortal = exit;
  }

  void BuildBackdrops(Transform parent) {
    // Animal house (west): hut with roof.
    Box(parent, "CCBackdropHouse", new Vector3(-5.2f, 0.6f, 3.0f), new Vector3(1.8f, 1.2f, 1.5f), Cream);
    Box(parent, "CCBackdropRoof", new Vector3(-5.2f, 1.45f, 3.0f), new Vector3(2.2f, 0.25f, 1.9f), RoofGreen);
    // Vehicle garage (east): brick block with dark door.
    Box(parent, "CCBackdropGarage", new Vector3(5.2f, 0.7f, 3.0f), new Vector3(2.0f, 1.4f, 1.6f), Brick);
    Box(parent, "CCBackdropDoor", new Vector3(5.2f, 0.55f, 2.15f), new Vector3(1.2f, 1.1f, 0.1f),
      new Color(0.25f, 0.22f, 0.28f));
    // Food market (north): counter + awning.
    Box(parent, "CCBackdropCounter", new Vector3(0f, 0.45f, 6.6f), new Vector3(2.6f, 0.9f, 1.0f), Wood);
    Box(parent, "CCBackdropAwning", new Vector3(0f, 1.9f, 6.6f), new Vector3(3.0f, 0.12f, 1.4f),
      new Color(0.92f, 0.32f, 0.34f));
    // Playground corner (north-east): two posts + slide plank.
    Box(parent, "CCPlayPostA", new Vector3(4.0f, 0.7f, 5.6f), new Vector3(0.14f, 1.4f, 0.14f), Wood);
    Box(parent, "CCPlayPostB", new Vector3(5.0f, 0.7f, 5.6f), new Vector3(0.14f, 1.4f, 0.14f), Wood);
    GameObject slide = Box(parent, "CCPlaySlide", new Vector3(4.5f, 0.5f, 4.7f),
      new Vector3(0.5f, 0.1f, 1.8f), Gold);
    slide.transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
  }

  void BuildDressing(Transform parent) {
    WorldBeauty.BlossomTree(parent, "CCTree0", new Vector3(-7.2f, 0f, 6.2f), 0.7f);
    WorldBeauty.BlossomTree(parent, "CCTree1", new Vector3(7.4f, 0f, 6.0f), 0.66f);
    WorldBeauty.FlowerDrift(parent, "CCFlowers0", new Vector3(-3.2f, 0f, -1.2f), 1.0f);
    WorldBeauty.FlowerDrift(parent, "CCFlowers1", new Vector3(3.4f, 0f, -1.0f), 1.0f);
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "ClassificationCityPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0f, 0f, 2.8f));
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 4.4f, -6.2f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 1.2f, 3.2f));
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  public void ClearRound() {
    Bins.Clear();
    Items.Clear();
    if (RoundRoot == null) return;
    for (int i = RoundRoot.childCount - 1; i >= 0; i--) {
      GameObject c = RoundRoot.GetChild(i).gameObject;
      CharacterPresentation.DestroyNow(c);
    }
  }

  // ---- bins ----

  public ClassificationBin SpawnBin(Vector3 local, string kind, string groupKey, int variant) {
    GameObject root = new GameObject("CCBin_" + kind);
    root.transform.SetParent(RoundRoot, false);
    root.transform.localPosition = local;
    BuildBinVisual(root.transform, kind, variant);
    PadOn(root.transform);
    BoxCollider box = root.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, 0.6f, 0f);
    box.size = new Vector3(2.0f, 1.4f, 2.0f);
    Ignore(root);
    ClassificationBin bin = root.AddComponent<ClassificationBin>();
    bin.GroupKey = groupKey;
    bin.BinId = kind;
    Bins.Add(bin);
    return bin;
  }

  void BuildBinVisual(Transform root, string kind, int variant) {
    if (kind == "house") {
      Box(root, "Wall", new Vector3(0f, 0.5f, 0f), new Vector3(1.5f, 1.0f, 1.2f), Cream);
      Box(root, "Roof", new Vector3(0f, 1.12f, 0f), new Vector3(1.8f, 0.22f, 1.5f), RoofGreen);
    } else if (kind == "garage") {
      Box(root, "Wall", new Vector3(0f, 0.6f, 0f), new Vector3(1.6f, 1.2f, 1.3f), Brick);
      Box(root, "Door", new Vector3(0f, 0.45f, -0.62f), new Vector3(1.0f, 0.9f, 0.08f),
        new Color(0.25f, 0.22f, 0.28f));
    } else if (kind == "market") {
      Box(root, "Counter", new Vector3(0f, 0.4f, 0f), new Vector3(1.6f, 0.8f, 0.9f), Wood);
      Box(root, "Awning", new Vector3(0f, 1.5f, 0f), new Vector3(1.9f, 0.1f, 1.1f),
        new Color(0.92f, 0.32f, 0.34f));
    } else if (kind == "playground") {
      Box(root, "PostA", new Vector3(-0.5f, 0.6f, 0f), new Vector3(0.12f, 1.2f, 0.12f), Wood);
      Box(root, "PostB", new Vector3(0.5f, 0.6f, 0f), new Vector3(0.12f, 1.2f, 0.12f), Wood);
      Box(root, "Bar", new Vector3(0f, 1.25f, 0f), new Vector3(1.2f, 0.1f, 0.1f), Gold);
    } else if (kind == "other") {
      Box(root, "Post", new Vector3(0f, 0.5f, 0f), new Vector3(0.14f, 1.0f, 0.14f), Wood);
      Ball(root, "Top", new Vector3(0f, 1.15f, 0f), 0.4f, Gold);
    } else {
      // Property pads: a low disc + one icon showing the property.
      GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      disc.name = "Disc";
      disc.transform.SetParent(root, false);
      disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
      disc.transform.localScale = new Vector3(1.9f, 0.03f, 1.9f);
      disc.GetComponent<Renderer>().sharedMaterial = Lit(Cream);
      Strip(disc);
      Ignore(disc);
      if (kind == "bigpad") Box(root, "Icon", new Vector3(0f, 0.5f, 0f), new Vector3(0.8f, 0.8f, 0.8f), Wood);
      else if (kind == "smallpad") Box(root, "Icon", new Vector3(0f, 0.25f, 0f), new Vector3(0.4f, 0.4f, 0.4f), Wood);
      else if (kind == "wheelspad") {
        GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        w.name = "Icon";
        w.transform.SetParent(root, false);
        w.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        w.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        w.transform.localScale = new Vector3(0.7f, 0.14f, 0.7f);
        w.GetComponent<Renderer>().sharedMaterial = Lit(Wood);
        Strip(w);
        Ignore(w);
      } else if (kind == "plainpad") Ball(root, "Icon", new Vector3(0f, 0.35f, 0f), 0.55f, Leaf);
      else if (kind == "cornerspad") Box(root, "Icon", new Vector3(0f, 0.4f, 0f), new Vector3(0.6f, 0.6f, 0.6f), Brick);
      else if (kind == "roundpad") Ball(root, "Icon", new Vector3(0f, 0.35f, 0f), 0.6f, Brick);
      else if (kind == "redpad") Box(root, "Icon", new Vector3(0f, 0.35f, 0f), new Vector3(0.6f, 0.6f, 0.6f), Palette[0]);
      else if (kind == "notredpad") Ball(root, "Icon", new Vector3(0f, 0.35f, 0f), 0.6f, Palette[1]);
      else Box(root, "Icon", new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.5f, 0.5f), Gold);
    }
  }

  // ---- items ----

  public ClassificationItem SpawnItem(Vector3 home, ClassItem props, string id, bool example) {
    GameObject root = new GameObject("CCItem_" + id);
    root.transform.SetParent(RoundRoot, false);
    root.transform.localPosition = home;
    float s = props.Big ? 1.25f : 0.8f;
    root.transform.localScale = Vector3.one * s;
    Color col = Palette[Mathf.Abs(props.ColorIdx) % Palette.Length];
    BuildItemVisual(root.transform, props, col);
    BoxCollider box = root.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, 0.4f, 0f);
    box.size = new Vector3(1.0f, 0.9f, 1.0f);
    Ignore(root);
    ClassificationItem item = root.AddComponent<ClassificationItem>();
    item.Props = props;
    item.ItemId = id;
    item.HomeLocal = home;
    Items.Add(item);
    if (example) {
      // Examples are parked visibly inside their bin by the game.
      Collider c = root.GetComponent<Collider>();
      if (c != null) c.enabled = false;
      item.MarkExample();
    }
    return item;
  }

  void BuildItemVisual(Transform root, ClassItem props, Color col) {
    if (props.Category == "animal") {
      GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
      body.name = "Body";
      body.transform.SetParent(root, false);
      body.transform.localPosition = new Vector3(0f, 0.35f, 0f);
      body.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
      body.GetComponent<Renderer>().sharedMaterial = Lit(col);
      Strip(body);
      Ignore(body);
      Ball(root, "Head", new Vector3(0f, 0.68f, -0.1f), 0.34f, col);
      Ball(root, "EarL", new Vector3(-0.12f, 0.86f, -0.1f), 0.12f, col);
      Ball(root, "EarR", new Vector3(0.12f, 0.86f, -0.1f), 0.12f, col);
    } else if (props.Category == "vehicle") {
      Box(root, "Body", new Vector3(0f, 0.35f, 0f), new Vector3(0.7f, 0.3f, 0.4f), col);
      for (int i = 0; i < 2; i++) {
        float x = i == 0 ? -0.22f : 0.22f;
        GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        w.name = "Wheel" + i;
        w.transform.SetParent(root, false);
        w.transform.localPosition = new Vector3(x, 0.16f, 0f);
        w.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        w.transform.localScale = new Vector3(0.24f, 0.08f, 0.24f);
        w.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.2f, 0.2f, 0.22f));
        Strip(w);
        Ignore(w);
      }
    } else if (props.Category == "food") {
      Ball(root, "Fruit", new Vector3(0f, 0.35f, 0f), 0.5f, col);
      Box(root, "Leaf", new Vector3(0.1f, 0.62f, 0f), new Vector3(0.12f, 0.06f, 0.12f), Leaf);
    } else {
      if (props.HasCorners) Box(root, "Toy", new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.5f, 0.5f), col);
      else Ball(root, "Toy", new Vector3(0f, 0.32f, 0f), 0.55f, col);
    }
  }

  public static Color ItemColor(int i) { return Palette[Mathf.Abs(i) % Palette.Length]; }

  void PadOn(Transform root) {
    GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pad.name = "Pad";
    pad.transform.SetParent(root, false);
    pad.transform.localPosition = new Vector3(0f, 0.012f, 0f);
    pad.transform.localScale = new Vector3(2.1f, 0.01f, 2.1f);
    pad.GetComponent<Renderer>().sharedMaterial = Lit(Cream);
    Strip(pad);
    Ignore(pad);
  }

  static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    go.transform.localScale = scale;
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Strip(go);
    Ignore(go);
    return go;
  }

  static GameObject Ball(Transform parent, string name, Vector3 pos, float diameter, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    go.transform.localScale = new Vector3(diameter, diameter, diameter);
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Strip(go);
    Ignore(go);
    return go;
  }

  static GameObject Pad(Transform parent, string name, Vector3 pos, float diameter, Color color) {
    GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pad.name = name;
    pad.transform.SetParent(parent, false);
    pad.transform.localPosition = pos;
    pad.transform.localScale = new Vector3(diameter, 0.01f, diameter);
    pad.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Strip(pad);
    Ignore(pad);
    return pad;
  }

  static void StageLight(Transform parent, Vector3 local) {
    GameObject go = new GameObject("CCFillLight");
    go.transform.SetParent(parent, false);
    go.transform.localPosition = local;
    Light light = go.AddComponent<Light>();
    light.type = LightType.Point;
    light.range = 24f;
    light.intensity = 4.2f;
    light.color = new Color(1f, 0.96f, 0.88f);
  }

  static void Strip(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (System.Exception) { }
  }

  static void Ignore(GameObject go) {
    if (go == null) return;
    try {
      Unity.AI.Navigation.NavMeshModifier mod = go.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
      if (mod == null) mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
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
