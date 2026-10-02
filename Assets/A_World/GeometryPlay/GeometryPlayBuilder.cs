// A_World/GeometryPlay/GeometryPlayBuilder.cs — "Xưởng Hình Học" (one scene,
// three zones, short walks). Presentation only. C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GeometryPlayBuilder : MonoBehaviour {
  public const string SceneName = "GeometryPlayScene";
  public static readonly Vector3 WorldOffset = new Vector3(660f, 0f, 0f);
  public const float BoundX = 14f;
  public const float BoundZ = 14f;
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -10.8f);
  public static readonly Vector3 FollowOffset = new Vector3(0f, 2.8f, -4.2f);
  public static readonly Vector3 PlaySpotLocal = new Vector3(0f, 0f, 0.2f);
  public static readonly Vector3 HuntCenter = new Vector3(0f, 0f, 2.6f);
  public static readonly Vector3 WorkshopLocal = new Vector3(0f, 0f, 5.1f);
  public static readonly Vector3 BuildLocal = new Vector3(4.4f, 0f, 4.4f);
  public const string ObjectiveEn = "Play with shapes";
  public const string ObjectiveVi = "Lắp hình vui";

  static readonly Color Lawn = new Color(0.44f, 0.70f, 0.42f);
  static readonly Color Sand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color Wood = new Color(0.55f, 0.40f, 0.24f);
  static readonly Color Cream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color Stone = new Color(0.68f, 0.68f, 0.66f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public Transform PlaySpot { get; private set; }
  public readonly List<GeometryPiece> Pieces = new List<GeometryPiece>();
  public readonly List<GeometrySocket> Sockets = new List<GeometrySocket>();
  public GeometryPiece EnvWheel { get; private set; }
  public GeometryPiece EnvWindow { get; private set; }
  public GeometryPiece EnvRoof { get; private set; }
  public GeometryPiece EnvDoor { get; private set; }
  public Transform Crate { get; private set; }
  public Transform HuntRoot { get; private set; }
  public Transform SocketRoot { get; private set; }

  static Mesh _triMesh;

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
    BuildZones(root);
    BuildEnv(root);
    BuildDressing(root);
    BuildAnchors(root);
    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  void BuildGround(Transform parent) {
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "GPRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(36f, 1.4f, 36f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    Strip(rim);
    Ignore(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "GPGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    Pad(parent, "GPPlaza", new Vector3(0f, 0.006f, 1.8f), 9.2f, new Color(0.93f, 0.84f, 0.58f));
    Box(parent, "GPPath", new Vector3(0f, 0.01f, -4.6f), new Vector3(1.8f, 0.02f, 8.4f), PathTan);
    StageLight(parent, new Vector3(0f, 7.5f, 2.8f));
    // USER ROUND 2026-10-01: the big glowing floor disc was removed (it read as
    // an annoying blob under the child in every arena).
  }

  void BuildEntryAndExit(Transform parent) {
    Box(parent, "GPEntryPostL", new Vector3(-1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Wood);
    Box(parent, "GPEntryPostR", new Vector3(1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Wood);
    GameObject beam = Box(parent, "GPEntryBeam", new Vector3(0f, 2.08f, -9.2f),
      new Vector3(3.2f, 0.16f, 0.16f), Cream);
    Ignore(beam);
    Pad(parent, "GPPlayDisc", PlaySpotLocal + new Vector3(0f, 0.012f, 0f), 3.0f, Gold);
    GameObject spot = new GameObject("GPPlaySpot");
    spot.transform.SetParent(parent, false);
    spot.transform.localPosition = PlaySpotLocal;
    PlaySpot = spot.transform;
    Pad(parent, "GPExitDisc", ExitLocal + new Vector3(0f, 0.012f, 0.3f), 3.0f, new Color(0.70f, 0.90f, 0.72f));
    GameObject exitGo = new GameObject("GPExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.PlayExit = true;
    exit.fireRadius = 1.35f;
    ExitPortal = exit;
  }

  void BuildZones(Transform parent) {
    GameObject hunt = new GameObject("GPHunt");
    hunt.transform.SetParent(parent, false);
    HuntRoot = hunt.transform;
    Pad(parent, "GPHuntRing", HuntCenter + new Vector3(0f, 0.008f, 0f), 5.6f, new Color(0.80f, 0.72f, 0.52f));
    GameObject work = new GameObject("GPWorkshop");
    work.transform.SetParent(parent, false);
    work.transform.localPosition = WorkshopLocal;
    Box(work.transform, "GPTable", new Vector3(0f, 0.22f, 0f), new Vector3(2.6f, 0.12f, 1.6f), Wood);
    GameObject sockRoot = new GameObject("GPSockets");
    sockRoot.transform.SetParent(parent, false);
    SocketRoot = sockRoot.transform;
    MakeSocket(sockRoot.transform, "GPSocketA", WorkshopLocal, GeometryKind.Circle, false, 0f);
    MakeSocket(sockRoot.transform, "GPSocketB", BuildLocal, GeometryKind.Square, false, 0f);
    MakeSocket(sockRoot.transform, "GPSocketC", BuildLocal + new Vector3(0f, 0f, 1.1f), GeometryKind.Triangle, true, 0f);
    MakeSocket(sockRoot.transform, "GPSocketD", BuildLocal + new Vector3(-0.7f, 0f, -0.7f), GeometryKind.Circle, false, 0f);
    MakeSocket(sockRoot.transform, "GPSocketE", BuildLocal + new Vector3(0.7f, 0f, -0.7f), GeometryKind.Circle, false, 0f);
    HideAllSockets();
  }

  void BuildEnv(Transform parent) {
    EnvWheel = MakeShape(parent, "GPEnvWheel", GeometryKind.Circle,
      new Vector3(-4.8f, 0.45f, 2.6f), GeometryShapes.ColorAt(1), 1.15f, 90f, true);
    EnvWheel.EnvRole = true;
    Box(parent, "GPWallW", new Vector3(5.2f, 0.9f, 1.4f), new Vector3(0.18f, 1.8f, 1.8f), Stone);
    EnvWindow = MakeShape(parent, "GPEnvWindow", GeometryKind.Square,
      new Vector3(4.95f, 1.15f, 1.4f), GeometryShapes.ColorAt(2), 0.7f, 0f, true);
    EnvWindow.EnvRole = true;
    Box(parent, "GPHutBody", new Vector3(-4.2f, 0.55f, 5.2f), new Vector3(1.4f, 1.1f, 1.1f), Wood);
    EnvRoof = MakeShape(parent, "GPEnvRoof", GeometryKind.Triangle,
      new Vector3(-4.2f, 1.35f, 5.2f), GeometryShapes.ColorAt(0), 0.95f, 0f, true);
    EnvRoof.EnvRole = true;
    Box(parent, "GPDoorWall", new Vector3(3.4f, 0.85f, 6.4f), new Vector3(1.6f, 1.7f, 0.16f), Stone);
    EnvDoor = MakeShape(parent, "GPEnvDoor", GeometryKind.Rectangle,
      new Vector3(3.4f, 0.7f, 6.22f), GeometryShapes.ColorAt(3), 0.85f, 0f, true);
    EnvDoor.EnvRole = true;
    SetEnvClickable(false);
    GameObject crate = Box(parent, "GPCrate", new Vector3(2.2f, 0.45f, 3.4f),
      new Vector3(1.1f, 0.9f, 1.1f), Wood);
    Crate = crate.transform;
  }

  void BuildDressing(Transform parent) {
    WorldBeauty.BlossomTree(parent, "GPTree0", new Vector3(-7.2f, 0f, 6.2f), 0.72f);
    WorldBeauty.BlossomTree(parent, "GPTree1", new Vector3(7.4f, 0f, 6.0f), 0.68f);
    WorldBeauty.FlowerDrift(parent, "GPFlowers0", new Vector3(-3.2f, 0f, -1.2f), 1.0f);
    WorldBeauty.FlowerDrift(parent, "GPFlowers1", new Vector3(3.4f, 0f, -1.0f), 1.0f);
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "GeometryPlayPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", HuntCenter);
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 4.4f, -6.2f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 1.2f, 3.2f));
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  public void ClearHuntPieces() {
    for (int i = Pieces.Count - 1; i >= 0; i--) {
      GeometryPiece p = Pieces[i];
      if (p == null || p.EnvRole) continue;
      Pieces.RemoveAt(i);
      if (p.gameObject != null) CharacterPresentation.DestroyNow(p.gameObject);
    }
  }

  public GeometryPiece SpawnHuntPiece(GeometryKind kind, Vector3 local, Color color,
      float scale, float yaw) {
    GeometryPiece p = MakeShape(HuntRoot != null ? HuntRoot : transform, "GPPiece_" + kind,
      kind, local, color, scale, yaw, false);
    Pieces.Add(p);
    return p;
  }

  public void HideAllSockets() {
    for (int i = 0; i < Sockets.Count; i++) {
      if (Sockets[i] == null) continue;
      Sockets[i].ClearOccupant();
      Sockets[i].gameObject.SetActive(false);
    }
  }

  public GeometrySocket ShowSocket(int index, Vector3 local, GeometryKind kind, bool upright) {
    if (index < 0 || index >= Sockets.Count) return null;
    GeometrySocket s = Sockets[index];
    s.transform.localPosition = local;
    s.Bind(s.Game, kind, upright, 0f);
    DressSocket(s, kind);
    s.gameObject.SetActive(true);
    return s;
  }

  public void SetEnvClickable(bool on) {
    GeometryPiece[] env = { EnvWheel, EnvWindow, EnvRoof, EnvDoor };
    for (int i = 0; i < env.Length; i++) {
      if (env[i] == null) continue;
      Collider c = env[i].GetComponent<Collider>();
      if (c != null) c.enabled = on;
    }
  }

  public GeometryPiece EnvOf(GeometryKind k) {
    if (k == GeometryKind.Circle) return EnvWheel;
    if (k == GeometryKind.Square) return EnvWindow;
    if (k == GeometryKind.Triangle) return EnvRoof;
    return EnvDoor;
  }

  GeometrySocket MakeSocket(Transform parent, string name, Vector3 local, GeometryKind kind,
      bool upright, float yaw) {
    GameObject root = new GameObject(name);
    root.transform.SetParent(parent, false);
    root.transform.localPosition = local;
    GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pad.name = "Pad";
    pad.transform.SetParent(root.transform, false);
    pad.transform.localPosition = new Vector3(0f, 0.02f, 0f);
    pad.transform.localScale = new Vector3(1.45f, 0.04f, 1.45f);
    pad.GetComponent<Renderer>().sharedMaterial = Lit(Cream);
    Strip(pad);
    Ignore(pad);
    BoxCollider box = root.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, 0.18f, 0f);
    box.size = new Vector3(1.5f, 0.45f, 1.5f);
    Ignore(root);
    GeometrySocket sock = root.AddComponent<GeometrySocket>();
    sock.Bind(null, kind, upright, yaw);
    Sockets.Add(sock);
    return sock;
  }

  void DressSocket(GeometrySocket s, GeometryKind kind) {
    if (s == null) return;
    Transform pad = s.transform.Find("Pad");
    if (pad != null) {
      Renderer r = pad.GetComponent<Renderer>();
      if (r != null)
        r.sharedMaterial = Lit(Color.Lerp(GeometryShapes.ColorAt((int)kind), Cream, 0.55f));
    }
    Transform old = s.transform.Find("Ghost");
    if (old != null) CharacterPresentation.DestroyNow(old.gameObject);
    GameObject ghost = new GameObject("Ghost");
    ghost.transform.SetParent(s.transform, false);
    ghost.transform.localPosition = new Vector3(0f, 0.16f, 0f);
    ghost.transform.localScale = Vector3.one * 0.7f;
    BuildKindVisual(ghost.transform, kind,
      Color.Lerp(GeometryShapes.ColorAt((int)kind), Cream, 0.28f), true);
    Ignore(ghost);
  }

  public static GeometryPiece MakeShape(Transform parent, string name, GeometryKind kind,
      Vector3 local, Color color, float scale, float yaw, bool env) {
    GameObject go = new GameObject(name);
    go.transform.SetParent(parent, false);
    go.transform.localPosition = local;
    go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    go.transform.localScale = Vector3.one * scale;
    BuildKindVisual(go.transform, kind, color, !env);
    BoxCollider box = go.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, env ? 0.28f : 0.36f, 0f);
    box.size = env ? new Vector3(1.25f, 0.7f, 1.25f) : new Vector3(0.9f, 0.8f, 0.9f);
    Ignore(go);
    if (!env) {
      UnityEngine.AI.NavMeshObstacle obs = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
      obs.carving = true;
      obs.shape = UnityEngine.AI.NavMeshObstacleShape.Capsule;
      obs.center = new Vector3(0f, 0.36f, 0f);
      obs.radius = 0.42f;
      obs.height = 0.85f;
    }
    GeometryPiece piece = go.AddComponent<GeometryPiece>();
    piece.Kind = kind;
    piece.HomeLocal = local;
    piece.HomeYaw = yaw;
    piece.EnvRole = env;
    return piece;
  }

  public static void BuildKindVisual(Transform parent, GeometryKind kind, Color color) {
    BuildKindVisual(parent, kind, color, false);
  }

  public static void BuildKindVisual(Transform parent, GeometryKind kind, Color color, bool toy) {
    if (kind == GeometryKind.Circle) {
      GameObject c = GameObject.CreatePrimitive(toy ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
      c.name = "Vis";
      c.transform.SetParent(parent, false);
      c.transform.localPosition = new Vector3(0f, toy ? 0.28f : 0.1f, 0f);
      c.transform.localScale = toy ? new Vector3(0.56f, 0.56f, 0.56f) : new Vector3(1.0f, 0.14f, 1.0f);
      c.GetComponent<Renderer>().sharedMaterial = Lit(color);
      Strip(c);
      Ignore(c);
      return;
    }
    if (kind == GeometryKind.Triangle) {
      GameObject t = new GameObject("Vis");
      t.transform.SetParent(parent, false);
      t.transform.localPosition = toy ? new Vector3(0f, 0.14f, 0f) : Vector3.zero;
      // USER ROUND 2026-10-01: the toy triangle was scaled 4.6x tall (a spike)
      // so a child could not read it as a triangle. Squat it into an obvious
      // triangular prism instead.
      t.transform.localScale = toy ? new Vector3(1.7f, 1.9f, 1.7f) : Vector3.one;
      MeshFilter mf = t.AddComponent<MeshFilter>();
      mf.sharedMesh = TriangleMesh();
      MeshRenderer mr = t.AddComponent<MeshRenderer>();
      mr.sharedMaterial = Lit(color);
      Ignore(t);
      return;
    }
    GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
    b.name = "Vis";
    b.transform.SetParent(parent, false);
    b.transform.localPosition = new Vector3(0f, toy ? 0.4f : 0.1f, 0f);
    if (kind == GeometryKind.Rectangle)
      b.transform.localScale = toy ? new Vector3(1.15f, 0.8f, 0.5f) : new Vector3(1.35f, 0.28f, 0.78f);
    else
      b.transform.localScale = toy ? new Vector3(0.8f, 0.8f, 0.8f) : new Vector3(1.0f, 0.28f, 1.0f);
    b.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Strip(b);
    Ignore(b);
  }

  static Mesh TriangleMesh() {
    if (_triMesh != null) return _triMesh;
    Mesh m = new Mesh();
    m.name = "GPTriangle";
    m.vertices = new Vector3[] {
      new Vector3(0f, 0.18f, 0.52f), new Vector3(-0.52f, 0.18f, -0.42f), new Vector3(0.52f, 0.18f, -0.42f),
      new Vector3(0f, 0.02f, 0.52f), new Vector3(-0.52f, 0.02f, -0.42f), new Vector3(0.52f, 0.02f, -0.42f),
    };
    m.triangles = new int[] {
      0, 1, 2, 5, 4, 3,
      0, 3, 4, 0, 4, 1,
      1, 4, 5, 1, 5, 2,
      2, 5, 3, 2, 3, 0
    };
    m.RecalculateNormals();
    _triMesh = m;
    return m;
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

  static void StageLight(Transform parent, Vector3 local) {
    GameObject go = new GameObject("GPFillLight");
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
