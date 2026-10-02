// A_World/ComparisonMarket/ComparisonMarketBuilder.cs — "Khu Chợ Của Bé".
// One connected market, short walks. Presentation only. C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ComparisonMarketBuilder : MonoBehaviour {
  public const string SceneName = "ComparisonMarketPlayScene";
  public static readonly Vector3 WorldOffset = new Vector3(720f, 0f, 0f);
  public const float BoundX = 14f;
  public const float BoundZ = 14f;
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -10.8f);
  public static readonly Vector3 FollowOffset = new Vector3(0f, 2.8f, -4.2f);
  public static readonly Vector3 PlaySpotLocal = new Vector3(0f, 0f, 0.2f);
  public static readonly Vector3 SlotA = new Vector3(-2.2f, 0f, 2.4f);
  public static readonly Vector3 SlotB = new Vector3(2.2f, 0f, 2.4f);
  public static readonly Vector3 SlotC = new Vector3(0f, 0f, 4.3f);
  public static readonly Vector3 TableLocal = new Vector3(0f, 0f, 3.4f);
  public static readonly Vector3 CartLocal = new Vector3(0f, 0f, 6.2f);
  public const string ObjectiveEn = "Compare at the market";
  public const string ObjectiveVi = "Khu chợ của bé";

  static readonly Color Lawn = new Color(0.44f, 0.70f, 0.42f);
  static readonly Color Sand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color Wood = new Color(0.55f, 0.40f, 0.24f);
  static readonly Color Cream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color Leaf = new Color(0.45f, 0.70f, 0.42f);
  static readonly Color AppleRed = new Color(0.92f, 0.32f, 0.34f);
  static readonly Color Orange = new Color(0.98f, 0.62f, 0.18f);
  static readonly Color BasketBlue = new Color(0.42f, 0.60f, 0.80f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public Transform PlaySpot { get; private set; }
  public Transform RoundRoot { get; private set; }
  public readonly List<ComparisonChoice> Choices = new List<ComparisonChoice>();
  public readonly List<ComparisonFruit> Fruits = new List<ComparisonFruit>();
  public ComparisonChoice CartChoice { get; private set; }
  public Transform Cart { get; private set; }

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
    BuildCart(root);
    GameObject rr = new GameObject("CMRound");
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
    rim.name = "CMRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(36f, 1.4f, 36f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    Strip(rim);
    Ignore(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "CMGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    Pad(parent, "CMPlaza", new Vector3(0f, 0.006f, 2.4f), 10.5f, new Color(0.93f, 0.84f, 0.58f));
    Box(parent, "CMPath", new Vector3(0f, 0.01f, -4.6f), new Vector3(1.8f, 0.02f, 8.4f),
      new Color(0.76f, 0.60f, 0.40f));
    StageLight(parent, new Vector3(0f, 7.5f, 2.8f));
    // USER ROUND 2026-10-01: the big glowing floor disc was removed (annoying
    // blob under the child).
  }

  void BuildEntryAndExit(Transform parent) {
    Box(parent, "CMEntryPostL", new Vector3(-1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Wood);
    Box(parent, "CMEntryPostR", new Vector3(1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Wood);
    GameObject beam = Box(parent, "CMEntryBeam", new Vector3(0f, 2.08f, -9.2f),
      new Vector3(3.2f, 0.16f, 0.16f), Cream);
    Ignore(beam);
    Pad(parent, "CMPlayDisc", PlaySpotLocal + new Vector3(0f, 0.012f, 0f), 3.0f, Gold);
    GameObject spot = new GameObject("CMPlaySpot");
    spot.transform.SetParent(parent, false);
    spot.transform.localPosition = PlaySpotLocal;
    PlaySpot = spot.transform;
    Pad(parent, "CMExitDisc", ExitLocal + new Vector3(0f, 0.012f, 0.3f), 3.0f,
      new Color(0.70f, 0.90f, 0.72f));
    GameObject exitGo = new GameObject("CMExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.PlayExit = true;
    exit.fireRadius = 1.35f;
    ExitPortal = exit;
  }

  void BuildBackdrops(Transform parent) {
    // Fruit stall (west).
    Box(parent, "CMStallCounter", new Vector3(-4.6f, 0.45f, 2.6f), new Vector3(2.2f, 0.9f, 1.2f), Wood);
    GameObject awn = Box(parent, "CMStallAwning", new Vector3(-4.6f, 1.9f, 2.6f),
      new Vector3(2.6f, 0.12f, 1.6f), AppleRed);
    Ignore(awn);
    // Matching table (center).
    Box(parent, "CMTable", new Vector3(0f, 0.28f, 3.4f), new Vector3(3.4f, 0.14f, 1.8f), Wood);
    // Toy shelf (east).
    Box(parent, "CMShelf", new Vector3(4.6f, 0.6f, 2.6f), new Vector3(1.8f, 1.2f, 0.8f), Wood);
    // Rope rack (north-east).
    Box(parent, "CMRackL", new Vector3(3.0f, 0.7f, 4.9f), new Vector3(0.14f, 1.4f, 0.14f), Wood);
    Box(parent, "CMRackR", new Vector3(4.4f, 0.7f, 4.9f), new Vector3(0.14f, 1.4f, 0.14f), Wood);
    // Align rail for fair length comparison.
    Box(parent, "CMRail", new Vector3(0f, 0.05f, 4.9f), new Vector3(3.2f, 0.06f, 0.18f), Cream);
  }

  void BuildCart(Transform parent) {
    GameObject cart = new GameObject("CMCart");
    cart.transform.SetParent(parent, false);
    cart.transform.localPosition = CartLocal;
    Cart = cart.transform;
    GameObject bed = GameObject.CreatePrimitive(PrimitiveType.Cube);
    bed.name = "Bed";
    bed.transform.SetParent(cart.transform, false);
    bed.transform.localPosition = new Vector3(0f, 0.55f, 0f);
    bed.transform.localScale = new Vector3(1.6f, 0.18f, 1.1f);
    bed.GetComponent<Renderer>().sharedMaterial = Lit(Wood);
    Strip(bed);
    Ignore(bed);
    for (int i = 0; i < 2; i++) {
      float x = i == 0 ? -0.6f : 0.6f;
      GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      wheel.name = "Wheel" + i;
      wheel.transform.SetParent(cart.transform, false);
      wheel.transform.localPosition = new Vector3(x, 0.3f, 0f);
      wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
      wheel.transform.localScale = new Vector3(0.6f, 0.12f, 0.6f);
      wheel.GetComponent<Renderer>().sharedMaterial = Lit(AppleRed);
      Strip(wheel);
      Ignore(wheel);
    }
    BoxCollider box = cart.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, 0.6f, 0f);
    box.size = new Vector3(1.8f, 1.0f, 1.3f);
    Ignore(cart);
    CartChoice = cart.AddComponent<ComparisonChoice>();
    CartChoice.ChoiceId = "cart";
    CartChoice.IsCart = true;
  }

  void BuildDressing(Transform parent) {
    WorldBeauty.BlossomTree(parent, "CMTree0", new Vector3(-7.2f, 0f, 6.2f), 0.7f);
    WorldBeauty.BlossomTree(parent, "CMTree1", new Vector3(7.4f, 0f, 6.0f), 0.66f);
    WorldBeauty.FlowerDrift(parent, "CMFlowers0", new Vector3(-3.2f, 0f, -1.2f), 1.0f);
    WorldBeauty.FlowerDrift(parent, "CMFlowers1", new Vector3(3.4f, 0f, -1.0f), 1.0f);
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "ComparisonMarketPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0f, 0f, 2.8f));
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 4.4f, -6.2f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 1.2f, 3.2f));
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  public void ClearRound() {
    Choices.Clear();
    Fruits.Clear();
    if (RoundRoot == null) return;
    for (int i = RoundRoot.childCount - 1; i >= 0; i--) {
      GameObject c = RoundRoot.GetChild(i).gameObject;
      CharacterPresentation.DestroyNow(c);
    }
  }

  // ---- round spawners (called by the game) ----

  public ComparisonChoice SpawnBasket(Vector3 local, Color basketColor, int fruitCount,
      float spread, Color fruitColor, float fruitSize, string id, bool answer) {
    GameObject root = new GameObject("CMBasket_" + id);
    root.transform.SetParent(RoundRoot, false);
    root.transform.localPosition = local;
    GameObject tub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    tub.name = "Tub";
    tub.transform.SetParent(root.transform, false);
    tub.transform.localPosition = new Vector3(0f, 0.28f, 0f);
    tub.transform.localScale = new Vector3(1.5f, 0.55f, 1.5f);
    tub.GetComponent<Renderer>().sharedMaterial = Lit(basketColor);
    Strip(tub);
    Ignore(tub);
    for (int i = 0; i < fruitCount; i++) {
      float gx = (i % 3 - 1) * 0.42f * spread;
      float gz = (i / 3 - 0.5f) * 0.42f * spread;
      GameObject f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      f.name = "Fruit" + i;
      f.transform.SetParent(root.transform, false);
      f.transform.localPosition = new Vector3(gx, 0.62f + (i % 2) * 0.1f, gz);
      f.transform.localScale = Vector3.one * fruitSize;
      f.GetComponent<Renderer>().sharedMaterial = Lit(fruitColor);
      Strip(f);
      Ignore(f);
    }
    PadOn(root.transform, local);
    BoxCollider box = root.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, 0.5f, 0f);
    box.size = new Vector3(1.8f, 1.0f, 1.8f);
    Ignore(root);
    ComparisonChoice c = root.AddComponent<ComparisonChoice>();
    c.ChoiceId = id;
    c.IsAnswer = answer;
    Choices.Add(c);
    return c;
  }

  public ComparisonChoice SpawnBall(Vector3 local, float radius, Color color, string id, bool answer) {
    GameObject root = new GameObject("CMBall_" + id);
    root.transform.SetParent(RoundRoot, false);
    root.transform.localPosition = local;
    GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    b.name = "Ball";
    b.transform.SetParent(root.transform, false);
    b.transform.localPosition = new Vector3(0f, radius, 0f);
    b.transform.localScale = Vector3.one * radius * 2f;
    b.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Strip(b);
    Ignore(b);
    PadOn(root.transform, local);
    BoxCollider box = root.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, 0.5f, 0f);
    box.size = new Vector3(1.4f, 1.2f, 1.4f);
    Ignore(root);
    ComparisonChoice c = root.AddComponent<ComparisonChoice>();
    c.ChoiceId = id;
    c.IsAnswer = answer;
    Choices.Add(c);
    return c;
  }

  public ComparisonChoice SpawnEqualPad(Vector3 local, string id) {
    GameObject root = new GameObject("CMEqual_" + id);
    root.transform.SetParent(RoundRoot, false);
    root.transform.localPosition = local;
    GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    disc.name = "Disc";
    disc.transform.SetParent(root.transform, false);
    disc.transform.localPosition = new Vector3(0f, 0.03f, 0f);
    disc.transform.localScale = new Vector3(1.7f, 0.04f, 1.7f);
    disc.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
    Strip(disc);
    Ignore(disc);
    GameObject a = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    a.name = "PipA";
    a.transform.SetParent(root.transform, false);
    a.transform.localPosition = new Vector3(-0.28f, 0.28f, 0f);
    a.transform.localScale = Vector3.one * 0.36f;
    a.GetComponent<Renderer>().sharedMaterial = Lit(AppleRed);
    Strip(a);
    Ignore(a);
    GameObject b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    b.name = "PipB";
    b.transform.SetParent(root.transform, false);
    b.transform.localPosition = new Vector3(0.28f, 0.28f, 0f);
    b.transform.localScale = Vector3.one * 0.36f;
    b.GetComponent<Renderer>().sharedMaterial = Lit(AppleRed);
    Strip(b);
    Ignore(b);
    BoxCollider box = root.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, 0.35f, 0f);
    box.size = new Vector3(1.8f, 0.8f, 1.8f);
    Ignore(root);
    ComparisonChoice c = root.AddComponent<ComparisonChoice>();
    c.ChoiceId = id;
    c.IsAnswer = true;
    Choices.Add(c);
    return c;
  }

  public ComparisonChoice SpawnBoxProp(Vector3 local, float size, Color color, string id, bool answer) {
    GameObject root = new GameObject("CMBox_" + id);
    root.transform.SetParent(RoundRoot, false);
    root.transform.localPosition = local;
    GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
    b.name = "Box";
    b.transform.SetParent(root.transform, false);
    b.transform.localPosition = new Vector3(0f, size * 0.5f, 0f);
    b.transform.localScale = new Vector3(size, size, size);
    b.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Strip(b);
    Ignore(b);
    PadOn(root.transform, local);
    BoxCollider box = root.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, 0.5f, 0f);
    box.size = new Vector3(1.4f, 1.2f, 1.4f);
    Ignore(root);
    ComparisonChoice c = root.AddComponent<ComparisonChoice>();
    c.ChoiceId = id;
    c.IsAnswer = answer;
    Choices.Add(c);
    return c;
  }

  public ComparisonChoice SpawnRope(Vector3 local, float len, Color color, string id, bool answer) {
    GameObject root = new GameObject("CMRope_" + id);
    root.transform.SetParent(RoundRoot, false);
    // Left-aligned on the rail so position never decides: ends start together.
    root.transform.localPosition = local;
    GameObject r = GameObject.CreatePrimitive(PrimitiveType.Cube);
    r.name = "Rope";
    r.transform.SetParent(root.transform, false);
    r.transform.localPosition = new Vector3(len * 0.5f, 0.12f, 0f);
    r.transform.localScale = new Vector3(len, 0.12f, 0.16f);
    r.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Strip(r);
    Ignore(r);
    GameObject knob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    knob.name = "Knob";
    knob.transform.SetParent(root.transform, false);
    knob.transform.localPosition = new Vector3(len, 0.12f, 0f);
    knob.transform.localScale = Vector3.one * 0.24f;
    knob.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
    Strip(knob);
    Ignore(knob);
    BoxCollider box = root.AddComponent<BoxCollider>();
    box.center = new Vector3(len * 0.5f, 0.3f, 0f);
    box.size = new Vector3(Mathf.Max(1f, len), 0.8f, 0.8f);
    Ignore(root);
    ComparisonChoice c = root.AddComponent<ComparisonChoice>();
    c.ChoiceId = id;
    c.IsAnswer = answer;
    Choices.Add(c);
    return c;
  }

  public ComparisonFruit SpawnFruit(Vector3 local, float size, Color color, int group) {
    GameObject f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    f.name = "CMPairFruit_g" + group;
    f.transform.SetParent(RoundRoot, false);
    f.transform.localPosition = local;
    f.transform.localScale = Vector3.one * size;
    f.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Ignore(f);
    ComparisonFruit cf = f.AddComponent<ComparisonFruit>();
    cf.Group = group;
    Fruits.Add(cf);
    return cf;
  }

  void PadOn(Transform root, Vector3 local) {
    GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pad.name = "Pad";
    pad.transform.SetParent(root, false);
    pad.transform.localPosition = new Vector3(0f, 0.012f - local.y, 0f);
    pad.transform.localScale = new Vector3(1.9f, 0.01f, 1.9f);
    pad.GetComponent<Renderer>().sharedMaterial = Lit(Cream);
    Strip(pad);
    Ignore(pad);
  }

  public static Color AppleColor(int i) { return i % 2 == 0 ? AppleRed : Leaf; }
  public static Color BasketColor(int i) { return i % 2 == 0 ? Wood : BasketBlue; }
  public static Color OrangeColor() { return Orange; }

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
    GameObject go = new GameObject("CMFillLight");
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
