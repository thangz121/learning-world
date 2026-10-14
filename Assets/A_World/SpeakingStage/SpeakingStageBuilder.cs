// A_World/SpeakingStage/SpeakingStageBuilder.cs — "Sân Khấu Nhí"
// (English -> Speaking). Presentation only: a little stage, the golden mic with
// its listening ring, the picture podium (the word to say), entry/exit.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class SpeakingStageBuilder : MonoBehaviour {
  public const string SceneName = "SpeakingStagePlayScene";
  public static readonly Vector3 WorldOffset = new Vector3(960f, 0f, 0f);
  public const float BoundX = 14f;
  public const float BoundZ = 14f;
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -10.8f);
  public static readonly Vector3 FollowOffset = new Vector3(0f, 4.8f, -6.5f);
  public static readonly Vector3 PlaySpotLocal = new Vector3(0f, 0f, -0.8f);
  public static readonly Vector3 MicLocal = new Vector3(0f, 0f, 0.9f);
  public static readonly Vector3 PictureLocal = new Vector3(0f, 0f, 3.1f);
  public const string ObjectiveEn = "Say the word you hear.";
  public const string ObjectiveVi = "Nói từ con nghe được nhé.";

  static readonly Color Lawn = new Color(0.35f, 0.72f, 0.58f);
  static readonly Color Wood = new Color(0.58f, 0.45f, 0.75f); // Royal crystal violet
  static readonly Color Cream = new Color(0.99f, 0.96f, 0.90f);
  static readonly Color Gold = new Color(1.0f, 0.86f, 0.38f);
  static readonly Color Coral = new Color(0.98f, 0.60f, 0.76f); // Fairy blush
  static readonly Color Plum = new Color(0.72f, 0.45f, 0.85f);  // Royal royal velvet
  static readonly Color CelestialBlue = new Color(0.58f, 0.82f, 1.0f);
  static readonly Color BallOrange = new Color(0.98f, 0.65f, 0.25f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public Transform PlaySpot { get; private set; }
  public Transform PictureRoot { get; private set; }
  public Transform MicRing { get; private set; }
  public GameObject ExitMarker { get; private set; }
  public TeacherGuidePresenter GuideNpc { get; private set; }

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
    BuildMic(root);
    BuildPicturePodium(root);
    BuildGuideNpc(root);
    BuildBackdrops(root);
    BuildDressing(root);
    BuildAnchors(root);
    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  void BuildGround(Transform parent) {
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "SSRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(36f, 1.4f, 36f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.48f, 0.38f, 0.55f));
    Strip(rim);
    Ignore(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "SSGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    // Glowing pearl plaza & starlight wooden stage deck
    Pad(parent, "SSPlaza", new Vector3(0f, 0.006f, 1.8f), 11.5f, new Color(0.95f, 0.90f, 0.98f));
    Box(parent, "SSStageDeck", new Vector3(0f, 0.05f, 1.6f), new Vector3(9.6f, 0.12f, 6.4f),
      new Color(0.85f, 0.65f, 0.88f));
    Box(parent, "SSPath", new Vector3(0f, 0.01f, -4.6f), new Vector3(1.8f, 0.02f, 8.4f),
      new Color(0.92f, 0.82f, 0.95f));
    StageLight(parent, new Vector3(0f, 7.5f, 2.2f));
  }

  void BuildEntryAndExit(Transform parent) {
    Box(parent, "SSEntryPostL", new Vector3(-1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Plum);
    Box(parent, "SSEntryPostR", new Vector3(1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Plum);
    GameObject beam = Box(parent, "SSEntryBeam", new Vector3(0f, 2.08f, -9.2f),
      new Vector3(3.2f, 0.16f, 0.16f), Coral);
    Ignore(beam);
    // Golden star crown on the gateway
    Ball(parent, "SSStarCrown", new Vector3(0f, 2.35f, -9.2f), 0.25f, Gold);
    Pad(parent, "SSPlayDisc", PlaySpotLocal + new Vector3(0f, 0.012f, 0f), 3.0f, Gold);
    GameObject spot = new GameObject("SSPlaySpot");
    spot.transform.SetParent(parent, false);
    spot.transform.localPosition = PlaySpotLocal;
    PlaySpot = spot.transform;
    GameObject exitDisc = Pad(parent, "SSExitDisc", ExitLocal + new Vector3(0f, 0.012f, 0.3f), 3.0f,
      CelestialBlue);
    ExitMarker = exitDisc;
    GameObject exitGo = new GameObject("SSExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.PlayExit = true;
    exit.fireRadius = 1.35f;
    ExitPortal = exit;
  }

  // The golden mic: child stands on the play disc and speaks into it. The ring
  // disc pulses while the game is listening.
  void BuildMic(Transform parent) {
    GameObject mic = new GameObject("SSMic");
    mic.transform.SetParent(parent, false);
    mic.transform.localPosition = MicLocal;
    Box(mic.transform, "Post", new Vector3(0f, 0.5f, 0f), new Vector3(0.12f, 1.0f, 0.12f), Plum);
    GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    head.name = "Head";
    head.transform.SetParent(mic.transform, false);
    head.transform.localPosition = new Vector3(0f, 1.2f, 0f);
    head.transform.localScale = Vector3.one * 0.5f;
    head.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
    Strip(head);
    Ignore(head);
    Ball(mic.transform, "Tip", new Vector3(0f, 0.95f, 0f), 0.10f, Coral);
    UnityEngine.AI.NavMeshObstacle micObs = mic.AddComponent<UnityEngine.AI.NavMeshObstacle>();
    micObs.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
    micObs.center = new Vector3(0f, 0.6f, 0f);
    micObs.size = new Vector3(0.6f, 1.2f, 0.6f);
    micObs.carving = true;
    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ring.name = "MicRing";
    ring.transform.SetParent(parent, false);
    ring.transform.localPosition = MicLocal + new Vector3(0f, 0.12f, 0f);
    ring.transform.localScale = new Vector3(2.2f, 0.02f, 2.2f);
    ring.GetComponent<Renderer>().sharedMaterial = Lit(Coral);
    Strip(ring);
    Ignore(ring);
    MicRing = ring.transform;
  }

  // The picture of the word to say (one podium; rebuilt per round).
  void BuildPicturePodium(Transform parent) {
    GameObject root = new GameObject("SSPicture");
    root.transform.SetParent(parent, false);
    root.transform.localPosition = PictureLocal;
    PictureRoot = root.transform;
    GameObject ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ped.name = "Pedestal";
    ped.transform.SetParent(root.transform, false);
    ped.transform.localPosition = new Vector3(0f, 0.35f, 0f);
    ped.transform.localScale = new Vector3(1.0f, 0.35f, 1.0f);
    ped.GetComponent<Renderer>().sharedMaterial = Lit(Plum);
    Strip(ped);
    Ignore(ped);
    Pad(root.transform, "CardTop", new Vector3(0f, 0.71f, 0f), 1.35f, Cream);
    UnityEngine.AI.NavMeshObstacle podObs = root.AddComponent<UnityEngine.AI.NavMeshObstacle>();
    podObs.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
    podObs.center = new Vector3(0f, 0.5f, 0f);
    podObs.size = new Vector3(1.5f, 1.0f, 1.5f);
    podObs.carving = true;
  }

  void BuildGuideNpc(Transform parent) {
    GameObject guideGo = new GameObject("SSTeacherGuide");
    guideGo.transform.SetParent(parent, false);
    // Placed warmly at the side of the stage facing the child & podium
    TeacherGuidePresenter guide = guideGo.AddComponent<TeacherGuidePresenter>();
    guide.Setup(new Vector3(-2.8f, 0f, 1.8f));
    GuideNpc = guide;
  }

  void BuildBackdrops(Transform parent) {
    // Royal velvet curtain pillars + glowing canopy
    Box(parent, "SSCurtainL", new Vector3(-3.4f, 1.5f, 4.8f), new Vector3(0.7f, 3.0f, 0.4f), Plum);
    Box(parent, "SSCurtainR", new Vector3(3.4f, 1.5f, 4.8f), new Vector3(0.7f, 3.0f, 0.4f), Plum);
    GameObject top = Box(parent, "SSCurtainTop", new Vector3(0f, 3.2f, 4.8f),
      new Vector3(7.5f, 0.5f, 0.4f), Coral);
    Ignore(top);
    // Celestial star crystals crowning the stage backdrops
    Ball(parent, "SSStarBackdropL", new Vector3(-3.4f, 3.2f, 4.8f), 0.28f, Gold);
    Ball(parent, "SSStarBackdropR", new Vector3(3.4f, 3.2f, 4.8f), 0.28f, Gold);
    Ball(parent, "SSStarBackdropCenter", new Vector3(0f, 3.65f, 4.8f), 0.35f, Gold);
  }

  void BuildDressing(Transform parent) {
    // Fairy blossom trees with pink falling drift
    WorldBeauty.BlossomTree(parent, "SSTree0", new Vector3(-7.2f, 0f, 6.0f), 0.85f);
    WorldBeauty.BlossomTree(parent, "SSTree1", new Vector3(7.4f, 0f, 5.8f), 0.82f);
    WorldBeauty.FlowerDrift(parent, "SSFlowers0", new Vector3(-3.4f, 0f, -1.6f), 1.3f);
    WorldBeauty.FlowerDrift(parent, "SSFlowers1", new Vector3(3.6f, 0f, -1.4f), 1.3f);

    // 3D PropKit dressing: decorative garden fences and stylized vegetation
    PropKit.Place(parent, "fence_simple", new Vector3(-4.8f, 0f, 4.6f), 0f, 1.2f, true);
    PropKit.Place(parent, "fence_simple", new Vector3(4.8f, 0f, 4.6f), 0f, 1.2f, true);
    PropKit.Place(parent, "flower_purpleA", new Vector3(-2.8f, 0f, 4.2f), 15f, 1.4f, true);
    PropKit.Place(parent, "flower_yellowA", new Vector3(2.8f, 0f, 4.2f), -20f, 1.4f, true);
    PropKit.Place(parent, "flower_redA", new Vector3(-4.0f, 0f, 1.2f), 45f, 1.2f, true);
    PropKit.Place(parent, "flower_purpleA", new Vector3(4.0f, 0f, 1.2f), -45f, 1.2f, true);
    PropKit.Place(parent, "plant_bushDetailed", new Vector3(-5.5f, 0f, 3.2f), 30f, 1.2f, true);
    PropKit.Place(parent, "plant_bushDetailed", new Vector3(5.5f, 0f, 3.2f), -30f, 1.2f, true);
    PropKit.Place(parent, "rock_smallA", new Vector3(-2.2f, 0f, -4.2f), 25f, 1.0f, true);
    PropKit.Place(parent, "rock_smallB", new Vector3(2.2f, 0f, -4.2f), -15f, 1.0f, true);
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "SpeakingStagePresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0f, 0f, 1.6f));
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 3.8f, -5.8f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 1.1f, 1.6f));
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  public GameObject CurrentProp { get; private set; }

  // Rebuild the picture for a round (wordless: the child sees the object).
  public void ShowPicture(string wordId) {
    if (PictureRoot == null) return;
    for (int i = PictureRoot.childCount - 1; i >= 0; i--) {
      GameObject c = PictureRoot.GetChild(i).gameObject;
      if (c.name == "Pedestal" || c.name == "CardTop") continue;
      CharacterPresentation.DestroyNow(c);
    }
    CurrentProp = null;
    GameObject prop;
    if (wordId == "ball") {
      prop = Ball(PictureRoot, "PicBall", new Vector3(0f, 1.15f, 0f), 0.5f, BallOrange);
      GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      band.name = "PicBand";
      band.transform.SetParent(PictureRoot, false);
      band.transform.localPosition = new Vector3(0f, 1.15f, 0f);
      band.transform.localScale = new Vector3(0.52f, 0.03f, 0.52f);
      band.GetComponent<Renderer>().sharedMaterial = Lit(Cream);
      Strip(band);
      Ignore(band);
    } else {
      prop = PropKit.PlaceFitted(PictureRoot, ListeningWords.PropName(wordId),
        new Vector3(0f, 0.74f, 0f), 20f, 1.2f, true);
      if (prop == null) prop = Ball(PictureRoot, "PicFallback", new Vector3(0f, 1.0f, 0f), 0.3f, Coral);
    }
    CurrentProp = prop;
    if (CurrentProp != null) {
      GameJuice.Pop(CurrentProp.transform, 0.2f, 0.4f);
    }
  }

  public void PlayCorrectFx() {
    if (CurrentProp != null) {
      GameJuice.Pop(CurrentProp.transform, 0.35f, 0.5f);
      MagicJuice.FairySparkleBurst(PictureRoot, new Vector3(0f, 1.2f, 0f), 18);
      MagicJuice.MagicHaloRing(PictureRoot, Vector3.zero, MagicJuice.MagicLavender, 2.2f);
    } else if (PictureRoot != null) {
      GameJuice.Pop(PictureRoot, 0.25f, 0.4f);
      MagicJuice.FairySparkleBurst(PictureRoot, new Vector3(0f, 0.8f, 0f), 12);
    }
  }

  public void PlayWrongFx() {
    if (CurrentProp != null) GameJuice.Wobble(CurrentProp.transform, 10f, 0.45f);
    else if (PictureRoot != null) GameJuice.Wobble(PictureRoot, 8f, 0.4f);
  }

  public void SetMicRingActive(bool listening) {
    if (MicRing == null) return;
    try {
      MicRing.GetComponent<Renderer>().sharedMaterial =
        Lit(listening ? Gold : Cream);
    } catch (System.Exception) { }
  }

  // Pulse while listening (called from Tick; visual only).
  public void PulseMicRing(float phase01) {
    if (MicRing == null) return;
    float s = 2.2f + 0.18f * Mathf.Sin(phase01 * Mathf.PI * 2f);
    MicRing.localScale = new Vector3(s, 0.02f, s);
  }

  // ---- small primitives shared with the other arenas --------------------------

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
    GameObject go = new GameObject("SSFillLight");
    go.transform.SetParent(parent, false);
    go.transform.localPosition = local;
    Light light = go.AddComponent<Light>();
    light.type = LightType.Point;
    light.range = 24f;
    light.intensity = 4.2f;
    light.color = new Color(1f, 0.96f, 0.88f);
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

  static readonly System.Collections.Generic.Dictionary<string, Material> _mats =
    new System.Collections.Generic.Dictionary<string, Material>();

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
