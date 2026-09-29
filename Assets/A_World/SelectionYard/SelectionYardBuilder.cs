// A_World/SelectionYard/SelectionYardBuilder.cs — FULL ARCHITECTURE RESET
// (2026-09-29). ONE generic yard scene serves BOTH navigation levels:
//   Level = "skill" -> a gate per skill of the subject (B: Sân chọn kỹ năng)
//   Level = "game"  -> a gate per game of the skill (C: Sân chọn trò chơi),
//                      or the [CHƯA CÓ TRÒ CHƠI] placeholder when empty.
// Everything is DATA-DRIVEN from LearningMap: labels, counts, accents and the
// accepted/empty distinction. No fake games are ever created for empty skills.
// The context (Level/SubjectId/SkillId) is pushed by SelectionYardArea before
// the lazy load; this builder only renders it.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SelectionYardBuilder : MonoBehaviour {
  public const string SceneName = "SelectionYardScene";

  // Separate island; the discovery island (+480x) is freed by the same reset.
  public static readonly Vector3 WorldOffset = new Vector3(540f, 0f, 0f);
  public const float BoundX = 18f;
  public const float BoundZ = 18f;
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 BackLocal = new Vector3(0f, 0f, -11.5f);
  // PHASE 3 foreground pass 2: FrameAnchor is only a transient beat (it
  // resumes Follow), so the RESTING yard view is this follow offset. Raised
  // (5.6) and pulled back (8.4) so the full gate row + name labels (y≈2.75
  // across z 2.2..4.6) stay inside the frame at rest.
  public static readonly Vector3 FollowOffset = new Vector3(0f, 5.6f, -8.4f);

  // Context (set by SelectionYardArea before the scene load).
  public string Level = "skill";   // "skill" (B) | "game" (C)
  public string SubjectId = "";    // skill level
  public string SkillId = "";      // game level

  static readonly Color Lawn = new Color(0.38f, 0.64f, 0.36f);
  static readonly Color Meadow = new Color(0.46f, 0.71f, 0.42f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);
  static readonly Color Sand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color Stone = new Color(0.68f, 0.68f, 0.66f);
  static readonly Color Cream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color Wood = new Color(0.55f, 0.40f, 0.24f);
  static readonly Color Mint = new Color(0.70f, 0.90f, 0.72f);
  static readonly Color Blue = new Color(0.42f, 0.60f, 0.80f);
  static readonly Color Coral = new Color(0.90f, 0.52f, 0.38f);
  static readonly Color Plum = new Color(0.62f, 0.45f, 0.72f);
  static readonly Color Sky = new Color(0.45f, 0.72f, 0.95f);
  static readonly Color Leaf = new Color(0.45f, 0.70f, 0.42f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public SelectionGate BackGate { get; private set; }
  public GameObject TitleBoard { get; private set; }
  public GameObject PlaceholderBoard { get; private set; }
  public readonly List<Transform> GateRoots = new List<Transform>();
  public readonly List<SelectionGate> GatePortals = new List<SelectionGate>();
  public readonly List<string> GateTargetIds = new List<string>();
  public Transform CamAnchor { get; private set; }
  public Transform LookAnchor { get; private set; }

  public bool IsGameLevel { get { return string.Equals(Level, "game"); } }

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
    BuildEntryAndBack(root);
    BuildOrientation(root);
    BuildGates(root);
    BuildAnchors(root);
    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  // ---- ground / entry / back ----------------------------------------------------

  void BuildGround(Transform parent) {
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "SYRim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(40f, 1.4f, 40f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    Strip(rim);
    Ignore(rim);
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "SYGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localPosition = Vector3.zero;
    ground.transform.localScale = new Vector3(3.8f, 1f, 3.8f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);
    Pad(parent, "SYPlaza", new Vector3(0f, 0.004f, 0f), 7.6f, Sand);
    Pad(parent, "SYMidMeadow", new Vector3(0f, 0.004f, 6f), 12f, Meadow);
    float[] angles = { 15f, 45f, 75f, 105f, 135f, 160f, 200f, 225f, 255f, 285f, 315f, 345f };
    for (int i = 0; i < angles.Length; i++) {
      float rad = angles[i] * Mathf.Deg2Rad;
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "SYHedge" + i;
      bush.transform.SetParent(parent, false);
      bush.transform.localPosition = new Vector3(Mathf.Sin(rad) * 15.5f, 0.55f, 3f + Mathf.Cos(rad) * 15.5f);
      bush.transform.localScale = (i % 2 == 0) ? new Vector3(3.4f, 2.2f, 3.4f) : new Vector3(2.8f, 1.9f, 2.8f);
      bush.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.24f, 0.55f, 0.30f));
      Strip(bush);
    }
  }

  void BuildEntryAndBack(Transform parent) {
    Box(parent, "SYEntryPostL", new Vector3(-1.6f, 1.0f, -9.5f), new Vector3(0.22f, 2.0f, 0.22f), Gold);
    Box(parent, "SYEntryPostR", new Vector3(1.6f, 1.0f, -9.5f), new Vector3(0.22f, 2.0f, 0.22f), Gold);
    GameObject beam = Box(parent, "SYEntryBeam", new Vector3(0f, 2.06f, -9.5f), new Vector3(3.4f, 0.16f, 0.16f), Cream);
    Ignore(beam);
    Pad(parent, "SYEntryDisc", new Vector3(0f, 0.008f, -3.6f), 2.4f, Stone);
    // BACK portal: up one level (game -> skill -> subject yard).
    Pad(parent, "SYBackDisc", new Vector3(0f, 0.01f, -11.1f), 3.2f, Mint);
    Box(parent, "SYBackPostL", new Vector3(-1.6f, 0.9f, -11.5f), new Vector3(0.16f, 1.8f, 0.16f), Mint);
    Box(parent, "SYBackPostR", new Vector3(1.6f, 0.9f, -11.5f), new Vector3(0.16f, 1.8f, 0.16f), Mint);
    GameObject back = new GameObject("SYBackGate");
    back.transform.SetParent(parent, false);
    back.transform.localPosition = BackLocal;
    BackGate = back.AddComponent<SelectionGate>();
    BackGate.Kind = SelectionGate.GateKind.Back;
    GameObject backLabel = new GameObject("SYBackLabel");
    backLabel.transform.SetParent(parent, false);
    WorldNameLabel label = backLabel.AddComponent<WorldNameLabel>();
    label.SetupLocked("Về", BackLocal + new Vector3(0f, 2.2f, 0f), EntryLocal);
    label.Show();
  }

  // ---- orientation sign: the yard's single title ---------------------------------
  // Skill level: "TOÁN HỌC — Chọn kỹ năng"; game level: "Đếm — Chọn trò chơi".
  void BuildOrientation(Transform parent) {
    string title = IsGameLevel
      ? LearningMap.GameYardTitle(SkillId)
      : LearningMap.SkillYardTitle(SubjectId);
    Vector3 pos = new Vector3(0f, 0f, 7.6f);
    Box(parent, "SYTitlePostL", pos + new Vector3(-1.6f, 0.9f, 0f), new Vector3(0.16f, 1.8f, 0.16f), Wood);
    Box(parent, "SYTitlePostR", pos + new Vector3(1.6f, 0.9f, 0f), new Vector3(0.16f, 1.8f, 0.16f), Wood);
    GameObject panel = Box(parent, "SYTitlePanel", pos + new Vector3(0f, 2.0f, 0f), new Vector3(3.6f, 0.9f, 0.12f), Cream);
    SetMat(panel, Emissive(Cream, 0.2f));
    Ignore(panel);
    TitleBoard = panel;
    GameObject labelGo = new GameObject("SYTitleLabel");
    labelGo.transform.SetParent(parent, false);
    WorldNameLabel label = labelGo.AddComponent<WorldNameLabel>();
    label.SetupLocked(title, pos + new Vector3(0f, 2.02f, -0.2f), EntryLocal);
    label.Show();
  }

  // ---- the gates -----------------------------------------------------------------

  void BuildGates(Transform parent) {
    GateRoots.Clear();
    GatePortals.Clear();
    GateTargetIds.Clear();
    PlaceholderBoard = null;
    if (IsGameLevel) {
      GameEntry[] games = LearningMap.GamesOf(SkillId);
      if (games.Length == 0) {
        BuildPlaceholder(parent);
        return;
      }
      for (int i = 0; i < games.Length; i++) {
        BuildGate(parent, i, games.Length, games[i].Id, games[i].DisplayName,
          games[i].HumanAccepted ? Gold : Cream, games[i].HumanAccepted, SelectionGate.GateKind.Play);
      }
      return;
    }
    SkillEntry[] skills = LearningMap.SkillsOf(SubjectId);
    for (int i = 0; i < skills.Length; i++) {
      bool hasGames = LearningMap.GamesOf(skills[i].Id).Length > 0;
      Color accent = AccentFor(i);
      BuildGate(parent, i, skills.Length, skills[i].Id, skills[i].DisplayName,
        accent, hasGames, SelectionGate.GateKind.Game); // its Game Yard (C)
    }
  }

  // Empty skill: the C yard exists but shows ONLY the placeholder board —
  // never a fake game gate (product rule).
  void BuildPlaceholder(Transform parent) {
    Vector3 pos = new Vector3(0f, 0f, 4.6f);
    Box(parent, "SYPlaceholderPostL", pos + new Vector3(-1.7f, 0.9f, 0f), new Vector3(0.18f, 1.9f, 0.18f), Wood);
    Box(parent, "SYPlaceholderPostR", pos + new Vector3(1.7f, 0.9f, 0f), new Vector3(0.18f, 1.9f, 0.18f), Wood);
    GameObject panel = Box(parent, "SYPlaceholderPanel", pos + new Vector3(0f, 2.05f, 0f),
      new Vector3(4.4f, 1.3f, 0.14f), Cream);
    SetMat(panel, Emissive(Cream, 0.18f));
    Ignore(panel);
    Box(parent, "SYPlaceholderHint", pos + new Vector3(0f, 2.5f, -0.1f), new Vector3(0.6f, 0.6f, 0.1f), Blue);
    PlaceholderBoard = panel;
    GameObject labelGo = new GameObject("SYPlaceholderLabel");
    labelGo.transform.SetParent(parent, false);
    WorldNameLabel label = labelGo.AddComponent<WorldNameLabel>();
    label.SetupLocked(LearningMap.EmptyYardText, pos + new Vector3(0f, 2.05f, -0.22f), EntryLocal);
    label.Show();
    // A quiet bench so the yard never reads as a bug.
    Box(parent, "SYPlaceholderBench", pos + new Vector3(0f, 0.25f, -1.4f), new Vector3(1.8f, 0.12f, 0.5f), Wood);
    Box(parent, "SYPlaceholderBenchLegL", pos + new Vector3(-0.7f, 0.12f, -1.4f), new Vector3(0.14f, 0.24f, 0.44f), Wood);
    Box(parent, "SYPlaceholderBenchLegR", pos + new Vector3(0.7f, 0.12f, -1.4f), new Vector3(0.14f, 0.24f, 0.44f), Wood);
  }

  void BuildGate(Transform parent, int index, int count, string id, string display,
      Color accent, bool live, SelectionGate.GateKind kind) {
    float t = count <= 1 ? 0f : (index / (float)(count - 1)) * 2f - 1f;
    float x = t * Mathf.Min(7.0f, count * 1.55f);
    float z = 4.6f - Mathf.Abs(t) * 1.7f;
    GameObject gate = new GameObject("SYGate_" + id);
    gate.transform.SetParent(parent, false);
    gate.transform.localPosition = new Vector3(x, 0f, z);
    // The gate faces the entry (south).
    gate.transform.localRotation = Quaternion.LookRotation(new Vector3(0f, 0f, -1f));
    Box(gate.transform, "PostL", new Vector3(-1.1f, 0.95f, 0f), new Vector3(0.18f, 1.9f, 0.18f), Wood);
    Box(gate.transform, "PostR", new Vector3(1.1f, 0.95f, 0f), new Vector3(0.18f, 1.9f, 0.18f), Wood);
    GameObject beam = Box(gate.transform, "Beam", new Vector3(0f, 1.98f, 0f), new Vector3(2.6f, 0.16f, 0.16f), accent);
    Ignore(beam);
    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ring.name = "Threshold";
    ring.transform.SetParent(gate.transform, false);
    ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
    ring.transform.localScale = new Vector3(2.9f, 0.012f, 2.9f);
    ring.GetComponent<Renderer>().sharedMaterial = Lit(live ? accent : Stone);
    Strip(ring);
    Ignore(ring);
    Ball(gate.transform, "CapL", new Vector3(-1.1f, 2.16f, 0f), 0.36f, live ? Gold : Cream);
    Ball(gate.transform, "CapR", new Vector3(1.1f, 2.16f, 0f), 0.36f, live ? Gold : Cream);
    // Floating label over the gate.
    GameObject labelGo = new GameObject("Label");
    labelGo.transform.SetParent(parent, false);
    WorldNameLabel label = labelGo.AddComponent<WorldNameLabel>();
    label.SetupLocked(display, new Vector3(x, 2.75f, z), EntryLocal);
    label.Show();
    // The walk-in door (7cm/mouth toward the entry) + approach glow for live
    // gates only (an empty skill/game never pretends to be an activity).
    GameObject door = new GameObject("Door");
    door.transform.SetParent(parent, false);
    door.transform.localPosition = new Vector3(x, 0f, z - 0.7f);
    SelectionGate portal = door.AddComponent<SelectionGate>();
    portal.Bind(null, kind, id);
    if (live) {
      GameObject hintGo = new GameObject("Hint");
      hintGo.transform.SetParent(parent, false);
      hintGo.transform.position = door.transform.position;
      MicroGateHint hint = hintGo.AddComponent<MicroGateHint>();
      hint.BuildForSelection(portal);
    }
    GateRoots.Add(gate.transform);
    GatePortals.Add(portal);
    GateTargetIds.Add(id);
  }

  static Color AccentFor(int index) {
    Color[] palette = { Gold, Blue, Coral, Leaf, Plum, Sky };
    return palette[Mathf.Abs(index) % palette.Length];
  }

  // ---- anchors / camera -----------------------------------------------------------

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "SelectionYardPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0f, 0f, 4.4f));
    // PHASE 3 foreground pass: the first framing sat too low/close — gate
    // beams and the name labels were cut at the top of the frame. Raised and
    // pulled back (labels at y≈2.75-3.2 across z 2.2..4.6 stay in view).
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 5.2f, -7.2f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 2.1f, 3.4f));
    a.Prompt = a.EnsureSlot("PromptAnchor", new Vector3(0f, 1.7f, 4.4f));
    a.Feedback = a.EnsureSlot("FeedbackAnchor", new Vector3(0f, 1.2f, 4.4f));
    a.Reward = a.EnsureSlot("RewardAnchor", new Vector3(0f, 0f, 4.4f));
    a.Exit = a.EnsureSlot("ExitAnchor", BackLocal);
    CamAnchor = a.Camera;
    LookAnchor = a.CameraLook;
  }

  // ---- helpers (same discipline as the other builders) ---------------------------

  static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent, false);
    go.transform.localPosition = pos;
    go.transform.localScale = scale;
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Strip(go);
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
    return pad;
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

  static void Strip(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (System.Exception) { }
  }

  static void Ignore(GameObject go) {
    if (go == null) return;
    try {
      Unity.AI.Navigation.NavMeshModifier mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (System.Exception) { }
  }

  static Material Emissive(Color color, float emission) {
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    if (mat.HasProperty("_EmissionColor")) {
      mat.EnableKeyword("_EMISSION");
      mat.SetColor("_EmissionColor", color * emission);
    }
    mat.enableInstancing = true;
    return mat;
  }

  static void SetMat(GameObject go, Material mat) {
    if (go == null || mat == null) return;
    Renderer r = go.GetComponent<Renderer>();
    if (r != null) r.sharedMaterial = mat;
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
