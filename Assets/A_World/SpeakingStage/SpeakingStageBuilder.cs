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
  public static readonly Vector3 FollowOffset = new Vector3(0f, 2.8f, -4.2f);
  public static readonly Vector3 PlaySpotLocal = new Vector3(0f, 0f, -0.8f);
  public static readonly Vector3 MicLocal = new Vector3(0f, 0f, 0.9f);
  public static readonly Vector3 PictureLocal = new Vector3(0f, 0f, 3.1f);
  public const string ObjectiveEn = "Say the word you hear.";
  public const string ObjectiveVi = "Nói từ con nghe được nhé.";

  static readonly Color Lawn = new Color(0.44f, 0.70f, 0.42f);
  static readonly Color Wood = new Color(0.55f, 0.40f, 0.24f);
  static readonly Color Cream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color Coral = new Color(0.92f, 0.45f, 0.38f);
  static readonly Color Plum = new Color(0.62f, 0.42f, 0.72f);
  static readonly Color BallOrange = new Color(0.98f, 0.62f, 0.18f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public Transform PlaySpot { get; private set; }
  public Transform PictureRoot { get; private set; }
  public Transform MicRing { get; private set; }
  public GameObject ExitMarker { get; private set; }

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
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    Strip(rim);
    Ignore(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "SSGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    Pad(parent, "SSPlaza", new Vector3(0f, 0.006f, 1.8f), 11.5f, new Color(0.86f, 0.72f, 0.70f));
    Box(parent, "SSStageDeck", new Vector3(0f, 0.05f, 1.6f), new Vector3(9.6f, 0.12f, 6.4f),
      new Color(0.72f, 0.50f, 0.42f));
    Box(parent, "SSPath", new Vector3(0f, 0.01f, -4.6f), new Vector3(1.8f, 0.02f, 8.4f),
      new Color(0.76f, 0.60f, 0.40f));
    StageLight(parent, new Vector3(0f, 7.5f, 2.2f));
  }

  void BuildEntryAndExit(Transform parent) {
    Box(parent, "SSEntryPostL", new Vector3(-1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Wood);
    Box(parent, "SSEntryPostR", new Vector3(1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Wood);
    GameObject beam = Box(parent, "SSEntryBeam", new Vector3(0f, 2.08f, -9.2f),
      new Vector3(3.2f, 0.16f, 0.16f), Cream);
    Ignore(beam);
    Pad(parent, "SSPlayDisc", PlaySpotLocal + new Vector3(0f, 0.012f, 0f), 3.0f, Gold);
    GameObject spot = new GameObject("SSPlaySpot");
    spot.transform.SetParent(parent, false);
    spot.transform.localPosition = PlaySpotLocal;
    PlaySpot = spot.transform;
    GameObject exitDisc = Pad(parent, "SSExitDisc", ExitLocal + new Vector3(0f, 0.012f, 0.3f), 3.0f,
      new Color(0.70f, 0.90f, 0.72f));
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
    Box(mic.transform, "Post", new Vector3(0f, 0.5f, 0f), new Vector3(0.12f, 1.0f, 0.12f), Wood);
    GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    head.name = "Head";
    head.transform.SetParent(mic.transform, false);
    head.transform.localPosition = new Vector3(0f, 1.2f, 0f);
    head.transform.localScale = Vector3.one * 0.5f;
    head.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
    Strip(head);
    Ignore(head);
    Ball(mic.transform, "Tip", new Vector3(0f, 0.95f, 0f), 0.10f, Cream);
    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ring.name = "MicRing";
    ring.transform.SetParent(parent, false);
    ring.transform.localPosition = MicLocal + new Vector3(0f, 0.02f, 0f);
    ring.transform.localScale = new Vector3(2.2f, 0.02f, 2.2f);
    ring.GetComponent<Renderer>().sharedMaterial = Lit(Cream);
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
    ped.GetComponent<Renderer>().sharedMaterial = Lit(Wood);
    Strip(ped);
    Ignore(ped);
    Pad(root.transform, "CardTop", new Vector3(0f, 0.71f, 0f), 1.35f, Cream);
  }

  void BuildBackdrops(Transform parent) {
    // Curtain pillars + top beam behind the picture podium.
    Box(parent, "SSCurtainL", new Vector3(-3.4f, 1.5f, 4.8f), new Vector3(0.7f, 3.0f, 0.4f), Plum);
    Box(parent, "SSCurtainR", new Vector3(3.4f, 1.5f, 4.8f), new Vector3(0.7f, 3.0f, 0.4f), Plum);
    GameObject top = Box(parent, "SSCurtainTop", new Vector3(0f, 3.2f, 4.8f),
      new Vector3(7.5f, 0.5f, 0.4f), Coral);
    Ignore(top);
  }

  void BuildDressing(Transform parent) {
    WorldBeauty.BlossomTree(parent, "SSTree0", new Vector3(-7.2f, 0f, 6.0f), 0.7f);
    WorldBeauty.BlossomTree(parent, "SSTree1", new Vector3(7.4f, 0f, 5.8f), 0.66f);
    WorldBeauty.FlowerDrift(parent, "SSFlowers0", new Vector3(-3.4f, 0f, -1.6f), 1.0f);
    WorldBeauty.FlowerDrift(parent, "SSFlowers1", new Vector3(3.6f, 0f, -1.4f), 1.0f);
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

  // Rebuild the picture for a round (wordless: the child sees the object).
  public void ShowPicture(string wordId) {
    if (PictureRoot == null) return;
    for (int i = PictureRoot.childCount - 1; i >= 0; i--) {
      GameObject c = PictureRoot.GetChild(i).gameObject;
      if (c.name == "Pedestal" || c.name == "CardTop") continue;
      CharacterPresentation.DestroyNow(c);
    }
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
      prop = PropKit.Place(PictureRoot, ListeningWords.PropName(wordId),
        new Vector3(0f, 0.74f, 0f), 0f, 2.2f, true);
      if (prop == null) Ball(PictureRoot, "PicFallback", new Vector3(0f, 1.0f, 0f), 0.3f, Coral);
    }
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
