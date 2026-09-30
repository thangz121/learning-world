// A_World/OrderingStation/OrderingStationBuilder.cs — "Ga Thứ Tự".
// One connected station, short walks. Presentation only. C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class OrderingStationBuilder : MonoBehaviour {
  public const string SceneName = "OrderingStationPlayScene";
  public static readonly Vector3 WorldOffset = new Vector3(840f, 0f, 0f);
  public const float BoundX = 14f;
  public const float BoundZ = 14f;
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -10.8f);
  public static readonly Vector3 FollowOffset = new Vector3(0f, 2.8f, -4.2f);
  public static readonly Vector3 PlaySpotLocal = new Vector3(0f, 0f, 0.2f);
  public static readonly Vector3 TrackCenter = new Vector3(0f, 0f, 3.4f);
  public const string ObjectiveEn = "Order at the station";
  public const string ObjectiveVi = "Ga thứ tự";

  static readonly Color Lawn = new Color(0.44f, 0.70f, 0.42f);
  static readonly Color Sand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color Wood = new Color(0.55f, 0.40f, 0.24f);
  static readonly Color Cream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color Steel = new Color(0.45f, 0.48f, 0.55f);
  static readonly Color Brick = new Color(0.85f, 0.45f, 0.35f);

  public static readonly Color[] Palette = {
    new Color(0.92f, 0.32f, 0.34f),
    new Color(0.28f, 0.52f, 0.92f),
    new Color(0.98f, 0.82f, 0.22f),
    new Color(0.28f, 0.72f, 0.38f),
    new Color(0.62f, 0.45f, 0.72f),
  };

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public Transform PlaySpot { get; private set; }
  public Transform RoundRoot { get; private set; }
  public readonly List<OrderingSlot> Slots = new List<OrderingSlot>();
  public readonly List<OrderingPiece> Pieces = new List<OrderingPiece>();

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
    GameObject rr = new GameObject("OSRound");
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
    rim.name = "OSRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(36f, 1.4f, 36f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    Strip(rim);
    Ignore(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "OSGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    Pad(parent, "OSPlaza", new Vector3(0f, 0.006f, 2.2f), 11f, new Color(0.93f, 0.84f, 0.58f));
    Box(parent, "OSPath", new Vector3(0f, 0.01f, -4.6f), new Vector3(1.8f, 0.02f, 8.4f),
      new Color(0.76f, 0.60f, 0.40f));
    StageLight(parent, new Vector3(0f, 7.5f, 2.8f));
    DemoJuice.AttachSpotlight(parent, "OSStageLight", TrackCenter + new Vector3(0f, 0.012f, 0f), 6.6f);
  }

  void BuildEntryAndExit(Transform parent) {
    Box(parent, "OSEntryPostL", new Vector3(-1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Wood);
    Box(parent, "OSEntryPostR", new Vector3(1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Wood);
    GameObject beam = Box(parent, "OSEntryBeam", new Vector3(0f, 2.08f, -9.2f),
      new Vector3(3.2f, 0.16f, 0.16f), Cream);
    Ignore(beam);
    Pad(parent, "OSPlayDisc", PlaySpotLocal + new Vector3(0f, 0.012f, 0f), 3.0f, Gold);
    GameObject spot = new GameObject("OSPlaySpot");
    spot.transform.SetParent(parent, false);
    spot.transform.localPosition = PlaySpotLocal;
    PlaySpot = spot.transform;
    Pad(parent, "OSExitDisc", ExitLocal + new Vector3(0f, 0.012f, 0.3f), 3.0f,
      new Color(0.70f, 0.90f, 0.72f));
    GameObject exitGo = new GameObject("OSExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.PlayExit = true;
    exit.fireRadius = 1.35f;
    ExitPortal = exit;
  }

  void BuildBackdrops(Transform parent) {
    // Station house (west).
    Box(parent, "OSStationHouse", new Vector3(-5.2f, 0.7f, 3.2f), new Vector3(2.0f, 1.4f, 1.6f), Cream);
    Box(parent, "OSStationRoof", new Vector3(-5.2f, 1.55f, 3.2f), new Vector3(2.4f, 0.25f, 2.0f), Brick);
    // Signal posts flanking the track.
    Box(parent, "OSSignalL", new Vector3(-4.2f, 0.8f, 3.4f), new Vector3(0.14f, 1.6f, 0.14f), Wood);
    Ball(parent, "OSSignalLampL", new Vector3(-4.2f, 1.7f, 3.4f), 0.3f, Gold);
    Box(parent, "OSSignalR", new Vector3(4.2f, 0.8f, 3.4f), new Vector3(0.14f, 1.6f, 0.14f), Wood);
    Ball(parent, "OSSignalLampR", new Vector3(4.2f, 1.7f, 3.4f), 0.3f, Gold);
    // Train platform (north): engine + two cars waiting for the mission.
    Box(parent, "OSTrainEngine", new Vector3(-1.2f, 0.5f, 6.6f), new Vector3(1.6f, 1.0f, 1.1f), Steel);
    Box(parent, "OSTrainCarA", new Vector3(0.7f, 0.45f, 6.6f), new Vector3(1.4f, 0.9f, 1.1f), Brick);
    Box(parent, "OSTrainCarB", new Vector3(2.4f, 0.45f, 6.6f), new Vector3(1.4f, 0.9f, 1.1f), Wood);
    for (int i = 0; i < 3; i++) {
      float x = -1.2f + i * 1.8f;
      GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      wheel.name = "OSTrainWheel" + i;
      wheel.transform.SetParent(parent, false);
      wheel.transform.localPosition = new Vector3(x, 0.2f, 6.0f);
      wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
      wheel.transform.localScale = new Vector3(0.4f, 0.1f, 0.4f);
      wheel.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.2f, 0.2f, 0.22f));
      Strip(wheel);
      Ignore(wheel);
    }
  }

  void BuildDressing(Transform parent) {
    WorldBeauty.BlossomTree(parent, "OSTree0", new Vector3(-7.2f, 0f, 6.2f), 0.7f);
    WorldBeauty.BlossomTree(parent, "OSTree1", new Vector3(7.4f, 0f, 6.0f), 0.66f);
    WorldBeauty.FlowerDrift(parent, "OSFlowers0", new Vector3(-3.2f, 0f, -1.2f), 1.0f);
    WorldBeauty.FlowerDrift(parent, "OSFlowers1", new Vector3(3.4f, 0f, -1.0f), 1.0f);
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "OrderingStationPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", TrackCenter);
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 4.4f, -6.2f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 1.2f, 3.2f));
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  public void ClearRound() {
    Slots.Clear();
    Pieces.Clear();
    if (RoundRoot == null) return;
    for (int i = RoundRoot.childCount - 1; i >= 0; i--) {
      GameObject c = RoundRoot.GetChild(i).gameObject;
      CharacterPresentation.DestroyNow(c);
    }
  }

  // ---- track ----

  public void BuildTrack(int n) {
    // Rail sleepers under the slots so the row reads as one track.
    GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
    rail.name = "OSTrackRail";
    rail.transform.SetParent(RoundRoot, false);
    rail.transform.localPosition = new Vector3(TrackCenter.x, 0.03f, TrackCenter.z + 0.75f);
    rail.transform.localScale = new Vector3(n * 1.5f + 0.6f, 0.06f, 0.18f);
    rail.GetComponent<Renderer>().sharedMaterial = Lit(Wood);
    Strip(rail);
    Ignore(rail);
    for (int i = 0; i < n; i++) {
      float x = TrackCenter.x + (i - (n - 1) * 0.5f) * 1.5f;
      GameObject root = new GameObject("OSSlot" + i);
      root.transform.SetParent(RoundRoot, false);
      root.transform.localPosition = new Vector3(x, 0f, TrackCenter.z);
      GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      pad.name = "Pad";
      pad.transform.SetParent(root.transform, false);
      pad.transform.localPosition = new Vector3(0f, 0.03f, 0f);
      pad.transform.localScale = new Vector3(1.2f, 0.03f, 1.2f);
      pad.GetComponent<Renderer>().sharedMaterial = Lit(Cream);
      Strip(pad);
      Ignore(pad);
      Box(root.transform, "Post", new Vector3(0f, 0.35f, 0.62f), new Vector3(0.1f, 0.7f, 0.1f), Wood);
      Ball(root.transform, "Nub", new Vector3(0f, 0.75f, 0.62f), 0.2f, Gold);
      BoxCollider box = root.AddComponent<BoxCollider>();
      box.center = new Vector3(0f, 0.5f, 0f);
      box.size = new Vector3(1.3f, 1.1f, 1.3f);
      Ignore(root);
      OrderingSlot slot = root.AddComponent<OrderingSlot>();
      slot.SlotIndex = i;
      Slots.Add(slot);
    }
  }

  // ---- pieces ----

  public OrderingPiece SpawnPiece(Vector3 home, float rank, OrderDim dim, int colorIdx,
      string id, bool locked) {
    GameObject root = new GameObject("OSPiece_" + id);
    root.transform.SetParent(RoundRoot, false);
    root.transform.localPosition = home;
    Color col = Palette[Mathf.Abs(colorIdx) % Palette.Length];
    if (locked) col = Color.Lerp(col, new Color(0.5f, 0.5f, 0.52f), 0.55f);
    if (dim == OrderDim.Size) {
      float r = 0.22f + rank * 0.09f;
      Ball(root.transform, "Ball", new Vector3(0f, r, 0f), r * 2f, col);
    } else if (dim == OrderDim.Height) {
      float h = 0.35f + rank * 0.28f;
      Box(root.transform, "Tower", new Vector3(0f, h * 0.5f, 0f), new Vector3(0.45f, h, 0.45f), col);
      Box(root.transform, "Cap", new Vector3(0f, h + 0.05f, 0f), new Vector3(0.55f, 0.1f, 0.55f), Gold);
    } else {
      float len = 0.7f + rank * 0.4f;
      GameObject rope = GameObject.CreatePrimitive(PrimitiveType.Cube);
      rope.name = "Rope";
      rope.transform.SetParent(root.transform, false);
      rope.transform.localPosition = new Vector3(0f, 0.1f, 0f);
      rope.transform.localScale = new Vector3(0.16f, 0.12f, len);
      rope.GetComponent<Renderer>().sharedMaterial = Lit(col);
      Strip(rope);
      Ignore(rope);
      Ball(root.transform, "Knob", new Vector3(0f, 0.1f, len * 0.5f), 0.22f, Gold);
    }
    BoxCollider box = root.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, 0.4f, 0f);
    box.size = new Vector3(1.0f, 0.9f, 1.0f);
    Ignore(root);
    OrderingPiece piece = root.AddComponent<OrderingPiece>();
    piece.Rank = rank;
    piece.Dim = dim;
    piece.PieceId = id;
    piece.HomeLocal = home;
    Pieces.Add(piece);
    if (locked) {
      Collider c = root.GetComponent<Collider>();
      if (c != null) c.enabled = false;
    }
    return piece;
  }

  public void HighlightEmpty() {
    for (int i = 0; i < Slots.Count; i++) {
      OrderingSlot s = Slots[i];
      if (s == null) continue;
      Transform pad = s.transform.Find("Pad");
      if (pad == null) continue;
      Renderer r = pad.GetComponent<Renderer>();
      if (r == null) continue;
      r.sharedMaterial = Lit(s.Filled ? Cream : Gold);
    }
  }

  public void PrePlace(int slotIdx, OrderingPiece piece) {
    if (slotIdx < 0 || slotIdx >= Slots.Count || piece == null) return;
    OrderingSlot slot = Slots[slotIdx];
    slot.Accept(piece);
  }

  public static Color PieceColor(int i) { return Palette[Mathf.Abs(i) % Palette.Length]; }

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
    GameObject go = new GameObject("OSFillLight");
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
