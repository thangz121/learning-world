// A_World/WonderKitchen/WonderKitchenBuilder.cs — "Căn Bếp Thần Kỳ"
// (English -> Vocabulary). Presentation only: magical pastel kitchen arena,
// fairy potion pot (cauldron), ingredient serving trays, fairy lights & chandeliers.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WonderKitchenBuilder : MonoBehaviour {
  public const string SceneName = "WonderKitchenPlayScene";
  public static readonly Vector3 WorldOffset = new Vector3(900f, 0f, 100f);
  public const float BoundX = 14f;
  public const float BoundZ = 14f;
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -10.8f);
  public static readonly Vector3 FollowOffset = new Vector3(0f, 5.0f, -6.8f);
  public static readonly Vector3 PlaySpotLocal = new Vector3(0f, 0f, -0.6f);
  public static readonly Vector3 CauldronLocal = new Vector3(0f, 0f, 2.2f);
  public const float TrayZ = 4.2f;
  public const float TraySpacing = 2.4f;
  public const string ObjectiveEn = "Put the right food into the magic pot!";
  public const string ObjectiveVi = "Bỏ đúng món ăn vào nồi thần kỳ nhé!";

  static readonly Color FloorTile = new Color(0.95f, 0.90f, 0.98f); // Soft lilac pearl floor
  static readonly Color KitchenWood = new Color(0.85f, 0.65f, 0.75f); // Rose-tinted wood
  static readonly Color Cream = new Color(0.99f, 0.96f, 0.92f);
  static readonly Color Gold = new Color(1.0f, 0.85f, 0.38f);
  static readonly Color FairyMint = new Color(0.65f, 0.96f, 0.85f);
  static readonly Color FairyPink = new Color(1.0f, 0.65f, 0.82f);
  static readonly Color CauldronBrass = new Color(0.92f, 0.75f, 0.35f);
  static readonly Color SoupGlow = new Color(0.98f, 0.55f, 0.85f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public Transform PlaySpot { get; private set; }
  public Transform RoundRoot { get; private set; }
  public Transform CauldronTransform { get; private set; }
  public TeacherGuidePresenter GuideNpc { get; private set; }
  public readonly List<WonderKitchenChoice> Choices = new List<WonderKitchenChoice>();

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
    BuildCauldron(root);
    BuildGuideNpc(root);
    BuildKitchenBackdrop(root);
    GameObject rr = new GameObject("WKRound");
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
    rim.name = "WKRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(36f, 1.4f, 36f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.62f, 0.48f, 0.58f));
    Strip(rim);
    Ignore(rim);

    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "WKGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(FloorTile);

    // Magical checkered tiles & decorative dining rug
    Pad(parent, "WKCenterRug", new Vector3(0f, 0.008f, 2.2f), 10.5f, FairyPink);
    Pad(parent, "WKInnerPlaza", new Vector3(0f, 0.012f, 2.2f), 9.0f, Cream);
    Box(parent, "WKPath", new Vector3(0f, 0.01f, -4.6f), new Vector3(2.0f, 0.02f, 8.4f), FairyMint);
    StageLight(parent, new Vector3(0f, 7.5f, 2.2f));
  }

  void BuildEntryAndExit(Transform parent) {
    Box(parent, "WKEntryPostL", new Vector3(-1.6f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), KitchenWood);
    Box(parent, "WKEntryPostR", new Vector3(1.6f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), KitchenWood);
    GameObject beam = Box(parent, "WKEntryBeam", new Vector3(0f, 2.08f, -9.2f),
      new Vector3(3.4f, 0.16f, 0.16f), FairyPink);
    Ignore(beam);
    Ball(parent, "WKEntryCrownStar", new Vector3(0f, 2.35f, -9.2f), 0.22f, Gold);

    Pad(parent, "WKPlayDisc", PlaySpotLocal + new Vector3(0f, 0.014f, 0f), 3.0f, Gold);
    GameObject spot = new GameObject("WKPlaySpot");
    spot.transform.SetParent(parent, false);
    spot.transform.localPosition = PlaySpotLocal;
    PlaySpot = spot.transform;

    Pad(parent, "WKExitDisc", ExitLocal + new Vector3(0f, 0.012f, 0.3f), 3.0f, FairyMint);
    GameObject exitGo = new GameObject("WKExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.PlayExit = true;
    exit.fireRadius = 1.35f;
    ExitPortal = exit;
  }

  // The Magic Cauldron (Nồi súp thần kỳ)
  void BuildCauldron(Transform parent) {
    GameObject potRoot = new GameObject("WKCauldron");
    potRoot.transform.SetParent(parent, false);
    potRoot.transform.localPosition = CauldronLocal;
    CauldronTransform = potRoot.transform;

    // Glowing base pedestal
    Pad(potRoot.transform, "CauldronBase", new Vector3(0f, 0.02f, 0f), 2.2f, Gold);

    // Brass magical pot belly
    GameObject belly = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    belly.name = "PotBelly";
    belly.transform.SetParent(potRoot.transform, false);
    belly.transform.localPosition = new Vector3(0f, 0.55f, 0f);
    belly.transform.localScale = new Vector3(1.4f, 0.95f, 1.4f);
    belly.GetComponent<Renderer>().sharedMaterial = Lit(CauldronBrass);
    Strip(belly);
    Ignore(belly);

    // Pot golden rim
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "PotRim";
    rim.transform.SetParent(potRoot.transform, false);
    rim.transform.localPosition = new Vector3(0f, 0.92f, 0f);
    rim.transform.localScale = new Vector3(1.3f, 0.12f, 1.3f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
    Strip(rim);
    Ignore(rim);

    // Glowing bubbling soup surface
    GameObject soup = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    soup.name = "PotSoupSurface";
    soup.transform.SetParent(potRoot.transform, false);
    soup.transform.localPosition = new Vector3(0f, 0.88f, 0f);
    soup.transform.localScale = new Vector3(1.15f, 0.02f, 1.15f);
    soup.GetComponent<Renderer>().sharedMaterial = Lit(SoupGlow);
    Strip(soup);
    Ignore(soup);

    // Transforming Dish Presentation Anchor atop cauldron
    GameObject dishRoot = new GameObject("WKDishAnchor");
    dishRoot.transform.SetParent(potRoot.transform, false);
    dishRoot.transform.localPosition = new Vector3(0f, 1.15f, 0f);
    DishAnchor = dishRoot.transform;

    // Adorable Chef Kitten companion perched beside the cauldron
    BuildChefKitten(potRoot.transform, new Vector3(1.25f, 0.1f, -0.2f));

    // Obstacle so player does not walk straight through the cauldron
    UnityEngine.AI.NavMeshObstacle obs = potRoot.AddComponent<UnityEngine.AI.NavMeshObstacle>();
    obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
    obs.center = new Vector3(0f, 0.6f, 0f);
    obs.size = new Vector3(1.6f, 1.2f, 1.6f);
    obs.carving = true;
    Ignore(potRoot);
  }

  public Transform DishAnchor { get; private set; }
  public Transform ChefKittenTransform { get; private set; }

  void BuildChefKitten(Transform parent, Vector3 localPos) {
    GameObject cat = new GameObject("WKChefKitten");
    cat.transform.SetParent(parent, false);
    cat.transform.localPosition = localPos;
    cat.transform.localRotation = Quaternion.Euler(0f, -40f, 0f);
    ChefKittenTransform = cat.transform;

    // Body (Cream white)
    Ball(cat.transform, "CatBody", new Vector3(0f, 0.28f, 0f), 0.28f, Cream);
    // Head
    Ball(cat.transform, "CatHead", new Vector3(0f, 0.52f, 0f), 0.24f, Cream);
    // Chef Hat (Tall white cylinder + puffy top)
    GameObject hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    hat.name = "ChefHat";
    hat.transform.SetParent(cat.transform, false);
    hat.transform.localPosition = new Vector3(0f, 0.72f, 0f);
    hat.transform.localScale = new Vector3(0.22f, 0.16f, 0.22f);
    hat.GetComponent<Renderer>().sharedMaterial = Lit(Cream);
    Strip(hat);
    Ignore(hat);
    Ball(cat.transform, "ChefHatTop", new Vector3(0f, 0.85f, 0f), 0.18f, Cream);
    // Ears (Pink/Lavender tips)
    Ball(cat.transform, "EarL", new Vector3(-0.14f, 0.68f, 0f), 0.08f, FairyPink);
    Ball(cat.transform, "EarR", new Vector3(0.14f, 0.68f, 0f), 0.08f, FairyPink);
    // Little Apron
    Box(cat.transform, "Apron", new Vector3(0f, 0.26f, -0.15f), new Vector3(0.26f, 0.28f, 0.06f), FairyPink);
    Ignore(cat);
  }

  void BuildGuideNpc(Transform parent) {
    GameObject guideGo = new GameObject("WKTeacherGuide");
    guideGo.transform.SetParent(parent, false);
    TeacherGuidePresenter guide = guideGo.AddComponent<TeacherGuidePresenter>();
    guide.Setup(new Vector3(-3.8f, 0f, 2.2f));
    GuideNpc = guide;
  }

  void BuildKitchenBackdrop(Transform parent) {
    // Elegant pastel kitchen buffet counter & arches
    Box(parent, "WKCounterL", new Vector3(-4.8f, 0.5f, 5.0f), new Vector3(3.2f, 1.0f, 1.2f), KitchenWood);
    Box(parent, "WKCounterR", new Vector3(4.8f, 0.5f, 5.0f), new Vector3(3.2f, 1.0f, 1.2f), KitchenWood);
    Box(parent, "WKTopL", new Vector3(-4.8f, 1.05f, 5.0f), new Vector3(3.4f, 0.1f, 1.3f), Cream);
    Box(parent, "WKTopR", new Vector3(4.8f, 1.05f, 5.0f), new Vector3(3.4f, 0.1f, 1.3f), Cream);

    // Back shelves with delicious decorative fruits & plants
    PropKit.Place(parent, "pumpkin", new Vector3(-5.2f, 1.1f, 5.0f), 15f, 0.6f, true);
    PropKit.Place(parent, "watermelon", new Vector3(-4.2f, 1.1f, 5.0f), -20f, 0.55f, true);
    PropKit.Place(parent, "pineapple", new Vector3(4.2f, 1.1f, 5.0f), 0f, 0.55f, true);
    PropKit.Place(parent, "orange", new Vector3(5.2f, 1.1f, 5.0f), 30f, 0.6f, true);
  }

  void BuildDressing(Transform parent) {
    // Cherry blossom trees framing the magic kitchen hall
    WorldBeauty.BlossomTree(parent, "WKTreeL", new Vector3(-7.2f, 0f, 5.8f), 0.85f);
    WorldBeauty.BlossomTree(parent, "WKTreeR", new Vector3(7.2f, 0f, 5.8f), 0.85f);
    WorldBeauty.FlowerDrift(parent, "WKFlowersL", new Vector3(-3.2f, 0f, -1.2f), 1.2f);
    WorldBeauty.FlowerDrift(parent, "WKFlowersR", new Vector3(3.2f, 0f, -1.2f), 1.2f);

    // Floating fairy lights above
    Ball(parent, "WKLamp0", new Vector3(-3.0f, 3.5f, 2.2f), 0.2f, Gold);
    Ball(parent, "WKLamp1", new Vector3(3.0f, 3.5f, 2.2f), 0.2f, Gold);
    Ball(parent, "WKLamp2", new Vector3(0f, 4.2f, 2.2f), 0.28f, FairyPink);

    // PropKit garden & kitchen surroundings
    PropKit.Place(parent, "fence_simple", new Vector3(-5.2f, 0f, -5.0f), 90f, 1.2f, true);
    PropKit.Place(parent, "fence_simple", new Vector3(5.2f, 0f, -5.0f), 90f, 1.2f, true);
    PropKit.Place(parent, "plant_bushDetailed", new Vector3(-4.8f, 0f, -2.5f), 20f, 1.2f, true);
    PropKit.Place(parent, "plant_bushDetailed", new Vector3(4.8f, 0f, -2.5f), -20f, 1.2f, true);
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "WonderKitchenPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", CauldronLocal);
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 4.8f, -6.5f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 1.2f, 2.8f));
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  public static Vector3 TraySlotAt(int index, int count) {
    float x = (index - (count - 1) * 0.5f) * TraySpacing;
    return new Vector3(x, 0f, TrayZ);
  }

  public void ClearRound() {
    Choices.Clear();
    if (RoundRoot == null) return;
    for (int i = RoundRoot.childCount - 1; i >= 0; i--) {
      GameObject c = RoundRoot.GetChild(i).gameObject;
      CharacterPresentation.DestroyNow(c);
    }
  }

  // Spawns one ingredient pedestal/tray
  public WonderKitchenChoice SpawnTray(int slotIndex, int count, string wordId, bool isTarget) {
    GameObject root = new GameObject("WKTray_" + wordId);
    root.transform.SetParent(RoundRoot, false);
    root.transform.localPosition = TraySlotAt(slotIndex, count);

    // Decorative tray pedestal
    GameObject ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ped.name = "Pedestal";
    ped.transform.SetParent(root.transform, false);
    ped.transform.localPosition = new Vector3(0f, 0.22f, 0f);
    ped.transform.localScale = new Vector3(0.72f, 0.22f, 0.72f);
    ped.GetComponent<Renderer>().sharedMaterial = Lit(KitchenWood);
    Strip(ped);
    Ignore(ped);

    // Golden silver platter
    Pad(root.transform, "PlatterTop", new Vector3(0f, 0.45f, 0f), 0.98f, Gold);

    // Place the food prop
    float targetSize = 0.65f;
    GameObject prop = PropKit.PlaceFitted(root.transform, ListeningWords.PropName(wordId),
      new Vector3(0f, 0.47f, 0f), 0f, targetSize, true);
    if (prop == null) {
      Ball(root.transform, "FallbackFood", new Vector3(0f, 0.65f, 0f), 0.24f, FairyPink);
    }

    BoxCollider box = root.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, 0.85f, 0f);
    box.size = new Vector3(1.2f, 1.5f, 1.2f);

    UnityEngine.AI.NavMeshObstacle obs = root.AddComponent<UnityEngine.AI.NavMeshObstacle>();
    obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
    obs.center = new Vector3(0f, 0.5f, 0f);
    obs.size = new Vector3(1.2f, 1.0f, 1.2f);
    obs.carving = true;
    Ignore(root);

    WonderKitchenChoice choice = root.AddComponent<WonderKitchenChoice>();
    choice.WordId = wordId;
    choice.IsTarget = isTarget;
    Choices.Add(choice);
    return choice;
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

  static GameObject Ball(Transform parent, string name, Vector3 pos, float radius, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    go.transform.localScale = Vector3.one * radius * 2f;
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Strip(go);
    Ignore(go);
    return go;
  }

  static void StageLight(Transform parent, Vector3 local) {
    GameObject go = new GameObject("WKFillLight");
    go.transform.SetParent(parent, false);
    go.transform.localPosition = local;
    Light light = go.AddComponent<Light>();
    light.type = LightType.Point;
    light.range = 24f;
    light.intensity = 4.2f;
    light.color = new Color(1f, 0.95f, 0.90f);
    light.shadows = LightShadows.Soft;
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
