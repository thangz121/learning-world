// A_World/ListeningGarden/ListeningGardenBuilder.cs — "Vườn Tai Thính"
// (English -> Listening). Presentation only: lawn stage, picture pods, the
// hear-it-again bell, entry/exit. C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ListeningGardenBuilder : MonoBehaviour {
  public const string SceneName = "ListeningGardenPlayScene";
  public static readonly Vector3 WorldOffset = new Vector3(900f, 0f, 0f);
  public const float BoundX = 14f;
  public const float BoundZ = 14f;
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -10.8f);
  public static readonly Vector3 FollowOffset = new Vector3(0f, 2.8f, -4.2f);
  public static readonly Vector3 PlaySpotLocal = new Vector3(0f, 0f, -0.5f);
  public static readonly Vector3 BellLocal = new Vector3(0f, 0f, 1.2f);
  public const float PodZ = 3.7f;
  public const float PodSpacing = 2.2f;
  public const string ObjectiveEn = "Listen and find the picture.";
  public const string ObjectiveVi = "Nghe và tìm hình nhé.";

  static readonly Color Lawn = new Color(0.44f, 0.70f, 0.42f);
  static readonly Color Wood = new Color(0.55f, 0.40f, 0.24f);
  static readonly Color Cream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color Leaf = new Color(0.45f, 0.70f, 0.42f);
  static readonly Color Coral = new Color(0.92f, 0.45f, 0.38f);
  static readonly Color Sky = new Color(0.62f, 0.80f, 0.92f);
  static readonly Color BallOrange = new Color(0.98f, 0.62f, 0.18f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public Transform PlaySpot { get; private set; }
  public Transform RoundRoot { get; private set; }
  public ListeningGardenBell Bell { get; private set; }
  public readonly List<ListeningGardenChoice> Choices = new List<ListeningGardenChoice>();

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
    BuildBell(root);
    BuildBackdrops(root);
    GameObject rr = new GameObject("LGRound");
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
    rim.name = "LGRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(36f, 1.4f, 36f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    Strip(rim);
    Ignore(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "LGGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    Pad(parent, "LGPlaza", new Vector3(0f, 0.006f, 2.2f), 11.5f, new Color(0.93f, 0.84f, 0.58f));
    Box(parent, "LGStage", new Vector3(0f, 0.012f, PodZ), new Vector3(10.4f, 0.015f, 2.1f),
      new Color(0.86f, 0.78f, 0.62f));
    Box(parent, "LGPath", new Vector3(0f, 0.01f, -4.6f), new Vector3(1.8f, 0.02f, 8.4f),
      new Color(0.76f, 0.60f, 0.40f));
    StageLight(parent, new Vector3(0f, 7.5f, 2.6f));
  }

  void BuildEntryAndExit(Transform parent) {
    Box(parent, "LGEntryPostL", new Vector3(-1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Wood);
    Box(parent, "LGEntryPostR", new Vector3(1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Wood);
    GameObject beam = Box(parent, "LGEntryBeam", new Vector3(0f, 2.08f, -9.2f),
      new Vector3(3.2f, 0.16f, 0.16f), Cream);
    Ignore(beam);
    Pad(parent, "LGPlayDisc", PlaySpotLocal + new Vector3(0f, 0.012f, 0f), 3.0f, Gold);
    GameObject spot = new GameObject("LGPlaySpot");
    spot.transform.SetParent(parent, false);
    spot.transform.localPosition = PlaySpotLocal;
    PlaySpot = spot.transform;
    Pad(parent, "LGExitDisc", ExitLocal + new Vector3(0f, 0.012f, 0.3f), 3.0f,
      new Color(0.70f, 0.90f, 0.72f));
    GameObject exitGo = new GameObject("LGExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.PlayExit = true;
    exit.fireRadius = 1.35f;
    ExitPortal = exit;
  }

  // The hear-it-again bell: the child taps it any time during a round.
  void BuildBell(Transform parent) {
    GameObject bell = new GameObject("LGBell");
    bell.transform.SetParent(parent, false);
    bell.transform.localPosition = BellLocal;
    Box(bell.transform, "Post", new Vector3(0f, 0.42f, 0f), new Vector3(0.10f, 0.84f, 0.10f), Wood);
    GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    dome.name = "Dome";
    dome.transform.SetParent(bell.transform, false);
    dome.transform.localPosition = new Vector3(0f, 1.02f, 0f);
    dome.transform.localScale = Vector3.one * 0.42f;
    dome.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
    Strip(dome);
    Ignore(dome);
    Ball(bell.transform, "Clapper", new Vector3(0f, 0.86f, 0f), 0.07f, Cream);
    Pad(bell.transform, "BellPad", new Vector3(0f, 0.012f - BellLocal.y, 0f), 1.3f, Cream);
    BoxCollider box = bell.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, 0.85f, 0f);
    box.size = new Vector3(0.9f, 1.5f, 0.9f);
    Ignore(bell);
    Bell = bell.AddComponent<ListeningGardenBell>();
  }

  void BuildBackdrops(Transform parent) {
    // A little open-air sound garden: two flower beds + a low rail behind the pods.
    Box(parent, "LGRail", new Vector3(0f, 0.16f, 4.9f), new Vector3(9.2f, 0.08f, 0.14f), Wood);
    Box(parent, "LGRailL", new Vector3(-4.6f, 0.3f, 4.9f), new Vector3(0.12f, 0.6f, 0.12f), Wood);
    Box(parent, "LGRailR", new Vector3(4.6f, 0.3f, 4.9f), new Vector3(0.12f, 0.6f, 0.12f), Wood);
    Pad(parent, "LGBedL", new Vector3(-3.6f, 0.01f, 6.2f), 2.4f, new Color(0.62f, 0.50f, 0.32f));
    Pad(parent, "LGBedR", new Vector3(3.6f, 0.01f, 6.2f), 2.4f, new Color(0.62f, 0.50f, 0.32f));
  }

  void BuildDressing(Transform parent) {
    WorldBeauty.BlossomTree(parent, "LGTree0", new Vector3(-7.2f, 0f, 6.2f), 0.7f);
    WorldBeauty.BlossomTree(parent, "LGTree1", new Vector3(7.4f, 0f, 6.0f), 0.66f);
    WorldBeauty.FlowerDrift(parent, "LGFlowers0", new Vector3(-3.2f, 0f, -1.4f), 1.0f);
    WorldBeauty.FlowerDrift(parent, "LGFlowers1", new Vector3(3.4f, 0f, -1.2f), 1.0f);
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "ListeningGardenPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0f, 0f, 2.4f));
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 4.4f, -6.2f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 1.2f, 3.0f));
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  // Pod slot for N choices: evenly spaced on the stage strip, left to right.
  public static Vector3 SlotAt(int index, int count) {
    float x = (index - (count - 1) * 0.5f) * PodSpacing;
    return new Vector3(x, 0f, PodZ);
  }

  public void ClearRound() {
    Choices.Clear();
    if (RoundRoot == null) return;
    for (int i = RoundRoot.childCount - 1; i >= 0; i--) {
      GameObject c = RoundRoot.GetChild(i).gameObject;
      CharacterPresentation.DestroyNow(c);
    }
  }

  // One picture pod: pedestal + the word's picture (PropKit food or the ball).
  public ListeningGardenChoice SpawnPod(int slotIndex, int count, string wordId, bool answer) {
    GameObject root = new GameObject("LGPod_" + wordId);
    root.transform.SetParent(RoundRoot, false);
    root.transform.localPosition = SlotAt(slotIndex, count);
    GameObject ped = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ped.name = "Pedestal";
    ped.transform.SetParent(root.transform, false);
    ped.transform.localPosition = new Vector3(0f, 0.24f, 0f);
    ped.transform.localScale = new Vector3(0.68f, 0.24f, 0.68f);
    ped.GetComponent<Renderer>().sharedMaterial = Lit(Wood);
    Strip(ped);
    Ignore(ped);
    Pad(root.transform, "CardTop", new Vector3(0f, 0.49f, 0f), 0.95f, Cream);
    if (wordId == "ball") {
      Ball(root.transform, "BallProp", new Vector3(0f, 0.74f, 0f), 0.36f, BallOrange);
      GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      band.name = "Band";
      band.transform.SetParent(root.transform, false);
      band.transform.localPosition = new Vector3(0f, 0.74f, 0f);
      band.transform.localScale = new Vector3(0.38f, 0.02f, 0.38f);
      band.GetComponent<Renderer>().sharedMaterial = Lit(Cream);
      Strip(band);
      Ignore(band);
    } else {
      GameObject prop = PropKit.Place(root.transform, ListeningWords.PropName(wordId),
        new Vector3(0f, 0.52f, 0f), 0f, 1.4f, true);
      if (prop == null) {
        Ball(root.transform, "FallbackProp", new Vector3(0f, 0.66f, 0f), 0.22f, Coral);
      }
    }
    BoxCollider box = root.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, 0.66f, 0f);
    box.size = new Vector3(1.0f, 1.3f, 1.0f);
    Ignore(root);
    ListeningGardenChoice c = root.AddComponent<ListeningGardenChoice>();
    c.WordId = wordId;
    c.IsAnswer = answer;
    Choices.Add(c);
    return c;
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
    GameObject go = new GameObject("LGFillLight");
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
