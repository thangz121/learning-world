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

  static readonly Color Lawn = new Color(0.44f, 0.70f, 0.42f);
  static readonly Color Meadow = new Color(0.54f, 0.78f, 0.48f);
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
    BuildDressing(root);
    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  // ---- labels ---------------------------------------------------------------------
  // PHASE 4 FIX (foreground evidence: no door/sign names anywhere in the yard):
  // WorldNameLabel.SetupLocked takes WORLD coordinates, but every call below
  // used LOCAL offsets — the labels were stranded at the map origin, edge-on
  // to the camera (and leaked ghost text into the hub). StageLabel transforms
  // the offsets through the island root and points the readable face at the
  // yard entry.
  static void StageLabel(Transform parent, string name, string text, Vector3 localPos) {
    GameObject labelGo = new GameObject(name);
    labelGo.transform.SetParent(parent, false);
    WorldNameLabel label = labelGo.AddComponent<WorldNameLabel>();
    label.SetupLocked(text, parent.TransformPoint(localPos), parent.TransformPoint(EntryLocal));
    label.Show();
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
      bush.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.28f, 0.58f, 0.34f));
      Strip(bush);
      Ignore(bush);
      WorldBeauty.HedgeBloom(parent, "SYHedgeBloom" + i, bush.transform.localPosition);
    }
  }

  void BuildEntryAndBack(Transform parent) {
    Box(parent, "SYEntryPostL", new Vector3(-1.6f, 1.0f, -9.5f), new Vector3(0.22f, 2.0f, 0.22f), Gold);
    Box(parent, "SYEntryPostR", new Vector3(1.6f, 1.0f, -9.5f), new Vector3(0.22f, 2.0f, 0.22f), Gold);
    GameObject beam = Box(parent, "SYEntryBeam", new Vector3(0f, 2.06f, -9.5f), new Vector3(3.4f, 0.16f, 0.16f), Cream);
    Ignore(beam);
    WorldBeauty.BlossomCrown(parent, "SYEntryCrown", new Vector3(0f, 2.72f, -9.5f), 0.62f);
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
    StageLabel(parent, "SYBackLabel", "Về", BackLocal + new Vector3(0f, 2.2f, 0f));
    WorldBeauty.BlossomCrown(parent, "SYBackCrown", BackLocal + new Vector3(0f, 2.45f, 0f), 0.48f);
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
    GameObject frame = Box(parent, "SYTitleFrame", pos + new Vector3(0f, 2.0f, 0.08f),
      new Vector3(4.5f, 1.32f, 0.08f), Wood);
    Ignore(frame);
    GameObject panel = Box(parent, "SYTitlePanel", pos + new Vector3(0f, 2.0f, 0f), new Vector3(4.15f, 1.08f, 0.12f), Cream);
    SetMat(panel, Emissive(Cream, 0.2f));
    Ignore(panel);
    GameObject stripe = Box(parent, "SYTitleStripe", pos + new Vector3(0f, 2.48f, -0.02f),
      new Vector3(4.0f, 0.1f, 0.1f), Gold);
    Ignore(stripe);
    TitleBoard = panel;
    StageLabel(parent, "SYTitleLabel", title, pos + new Vector3(0f, 2.02f, -0.2f));
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
        bool live = LearningMap.CanLaunch(games[i].Id);
        Color accent = games[i].HumanAccepted ? Gold : (live ? Blue : Cream);
        BuildGate(parent, i, games.Length, games[i].Id, games[i].DisplayName,
          accent, live, SelectionGate.GateKind.Play);
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
    GameObject frame = Box(parent, "SYPlaceholderFrame", pos + new Vector3(0f, 2.05f, 0.08f),
      new Vector3(4.9f, 1.6f, 0.08f), Wood);
    Ignore(frame);
    GameObject panel = Box(parent, "SYPlaceholderPanel", pos + new Vector3(0f, 2.05f, 0f),
      new Vector3(4.4f, 1.3f, 0.14f), Cream);
    SetMat(panel, Emissive(Cream, 0.18f));
    Ignore(panel);
    Box(parent, "SYPlaceholderHint", pos + new Vector3(0f, 2.5f, -0.1f), new Vector3(0.6f, 0.6f, 0.1f), Blue);
    PlaceholderBoard = panel;
    StageLabel(parent, "SYPlaceholderLabel", LearningMap.EmptyYardText,
      pos + new Vector3(0f, 2.05f, -0.22f));
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
    // Floating label over the gate (world-staged: the island root sits at
    // +540, local offsets MUST be transformed — PHASE 4 label fix).
    StageLabel(parent, "Label", display, new Vector3(x, 2.75f, z));
    // Cream plaque behind the pill so the name reads as a sign, not sky text.
    // Sits above agent height and is bake-ignored (headroom rule).
    GameObject plaqueFrame = Box(parent, "SYNameFrame_" + id, new Vector3(x, 2.72f, z + 0.16f),
      new Vector3(1.95f, 0.62f, 0.06f), Wood);
    Ignore(plaqueFrame);
    GameObject plaque = Box(parent, "SYNamePlaque_" + id, new Vector3(x, 2.72f, z + 0.1f),
      new Vector3(1.72f, 0.46f, 0.06f), Cream);
    Ignore(plaque);
    // USER ROUND 2026-10-01 ("tên cổng xiên vào bảng"): the label faces the yard
    // entry point (which yaws side gates), but the plaque was axis-aligned — so
    // the name looked skewed against the board. Yaw the plaque to the SAME
    // facing as the label so name + board read as one sign.
    Vector3 faceTo = new Vector3(x, 0f, z) - EntryLocal;
    faceTo.y = 0f;
    if (faceTo.sqrMagnitude > 0.0001f) {
      Quaternion signRot = Quaternion.LookRotation(faceTo);
      plaqueFrame.transform.localRotation = signRot;
      plaque.transform.localRotation = signRot;
    }
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
    // USER ROUND 2026-09-29 (round 2): every door reads its identity by SHAPE.
    DecorateDoor(parent, kind, id, x, z);
    // PHASE 4b (user order: a pre-reader must CHOOSE BY PICTURE): each game
    // door carries a small wordless diorama of its game right in front.
    if (kind == SelectionGate.GateKind.Play) BuildGamePreview(parent, id, x, z);
  }

  // A themed medallion over the beam, drawn from the door's own id: math =
  // counting cubes, thinking = a gear, vietnamese = a lotus, english = an open
  // book, exploration = a leaf + magnifier. Game doors get a gold "play here"
  // banner (their diorama carries the identity).
  void DecorateDoor(Transform parent, SelectionGate.GateKind kind, string id, float x, float z) {
    Vector3 top = new Vector3(x, 2.34f, z);
    if (kind == SelectionGate.GateKind.Game) {
      Box(parent, "SYBanner_" + id, new Vector3(x, 2.2f, z),
        new Vector3(1.9f, 0.15f, 0.09f), Gold);
      return;
    }
    if (id.StartsWith("math_")) {
      Box(parent, "SYMarkA_" + id, top, new Vector3(0.26f, 0.26f, 0.26f), Blue);
      Box(parent, "SYMarkB_" + id, top + new Vector3(0.19f, 0.17f, 0f),
        new Vector3(0.20f, 0.20f, 0.20f), Gold);
      Box(parent, "SYMarkC_" + id, top + new Vector3(-0.19f, 0.17f, 0f),
        new Vector3(0.20f, 0.20f, 0.20f), Coral);
    } else if (id.StartsWith("thinking_")) {
      GameObject gear = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      gear.name = "SYMarkGear_" + id;
      gear.transform.SetParent(parent, false);
      gear.transform.localPosition = top;
      gear.transform.localScale = new Vector3(0.34f, 0.05f, 0.34f);
      gear.GetComponent<Renderer>().sharedMaterial = Lit(Leaf);
      Strip(gear);
      Ignore(gear);
      for (int k = 0; k < 4; k++) {
        float rad = (k * 90f + 45f) * Mathf.Deg2Rad;
        Box(parent, "SYTooth_" + id + k,
          top + new Vector3(Mathf.Cos(rad) * 0.21f, 0f, Mathf.Sin(rad) * 0.21f),
          new Vector3(0.09f, 0.09f, 0.09f), Wood);
      }
    } else if (id.StartsWith("vietnamese_")) {
      Ball(parent, "SYMarkLotus_" + id, top, 0.26f, Plum);
      Ball(parent, "SYMarkPetalA_" + id, top + new Vector3(0.17f, -0.05f, 0f), 0.15f, Coral);
      Ball(parent, "SYMarkPetalB_" + id, top + new Vector3(-0.17f, -0.05f, 0f), 0.15f, Coral);
    } else if (id.StartsWith("english_")) {
      GameObject pageL = Box(parent, "SYMarkBookL_" + id, top + new Vector3(-0.11f, 0f, 0f),
        new Vector3(0.30f, 0.34f, 0.08f), Cream);
      pageL.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
      GameObject pageR = Box(parent, "SYMarkBookR_" + id, top + new Vector3(0.11f, 0f, 0f),
        new Vector3(0.30f, 0.34f, 0.08f), Coral);
      pageR.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
    } else if (id.StartsWith("exploration_")) {
      Ball(parent, "SYMarkLeaf_" + id, top + new Vector3(-0.05f, 0f, 0f), 0.30f, Leaf);
      GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      lens.name = "SYMarkLens_" + id;
      lens.transform.SetParent(parent, false);
      lens.transform.localPosition = top + new Vector3(0.14f, 0.02f, -0.02f);
      lens.transform.localScale = new Vector3(0.22f, 0.03f, 0.22f);
      lens.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
      Strip(lens);
      Ignore(lens);
      Box(parent, "SYMarkHandle_" + id, top + new Vector3(0.30f, -0.14f, 0f),
        new Vector3(0.07f, 0.20f, 0.07f), Wood);
    } else {
      Box(parent, "SYMark_" + id, top, new Vector3(0.24f, 0.24f, 0.24f), Mint);
    }
  }

  // Wordless game previews (primitives only, same visual language as the
  // arenas): rabbit feeding = bunny + carrots on a cream pad; number stairs =
  // three mini steps + the gold orb. Sits between the child and the door so
  // the choice is read before any text.
  void BuildGamePreview(Transform parent, string gameId, float x, float z) {
    // 2.3m in front of the door: outside the 1.6m trigger radius, so the
    // child can study the picture WITHOUT entering (walk on = choose).
    Vector3 p = new Vector3(x, 0f, z - 2.3f);
    Pad(parent, "SYPreviewPad_" + gameId, p + new Vector3(0f, 0.012f, 0f), 1.6f, Cream);
    if (gameId == "rabbit_feeding") {
      GameObject bunny = new GameObject("SYPreview_rabbit_feeding");
      bunny.transform.SetParent(parent, false);
      bunny.transform.localPosition = p + new Vector3(-0.28f, 0f, 0f);
      bunny.transform.localScale = Vector3.one * 1.4f;
      Ignore(bunny);
      Ball(bunny.transform, "Body", new Vector3(0f, 0.26f, 0f), 0.40f,
        new Color(0.97f, 0.96f, 0.94f));
      Ball(bunny.transform, "Head", new Vector3(0.02f, 0.52f, 0f), 0.30f,
        new Color(0.97f, 0.96f, 0.94f));
      GameObject earL = Ball(bunny.transform, "EarL", new Vector3(-0.06f, 0.72f, 0f), 0.11f,
        new Color(0.97f, 0.96f, 0.94f));
      earL.transform.localScale = new Vector3(0.09f, 0.24f, 0.09f);
      GameObject earR = Ball(bunny.transform, "EarR", new Vector3(0.10f, 0.72f, 0f), 0.11f,
        new Color(0.97f, 0.96f, 0.94f));
      earR.transform.localScale = new Vector3(0.09f, 0.24f, 0.09f);
      Ball(bunny.transform, "Nose", new Vector3(0.14f, 0.49f, 0.13f), 0.06f, Coral);
      Ball(bunny.transform, "EyeL", new Vector3(-0.04f, 0.56f, 0.12f), 0.045f, new Color(0.15f, 0.12f, 0.12f));
      Ball(bunny.transform, "EyeR", new Vector3(0.08f, 0.56f, 0.12f), 0.045f, new Color(0.15f, 0.12f, 0.12f));
      Ball(bunny.transform, "EarInL", new Vector3(-0.06f, 0.74f, 0.04f), 0.05f, WorldBeauty.BlossomDeep);
      Ball(bunny.transform, "EarInR", new Vector3(0.10f, 0.74f, 0.04f), 0.05f, WorldBeauty.BlossomDeep);
      // Two carrots in front of the bunny (the feeding motif).
      for (int i = 0; i < 2; i++) {
        GameObject carrot = new GameObject("Carrot" + i);
        carrot.transform.SetParent(parent, false);
        carrot.transform.localPosition = p + new Vector3(0.32f + i * 0.16f, 0f, i == 0 ? 0.02f : -0.04f);
        carrot.transform.localScale = Vector3.one * 1.4f;
        Ignore(carrot);
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        root.name = "CarrotRoot";
        root.transform.SetParent(carrot.transform, false);
        root.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        root.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 8f : -8f);
        root.transform.localScale = new Vector3(0.11f, 0.22f, 0.11f);
        root.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.95f, 0.52f, 0.16f));
        Strip(root);
        Ignore(root);
        Ball(carrot.transform, "CarrotLeaf", new Vector3(0f, 0.47f, 0f), 0.12f, Leaf);
      }
    } else if (gameId == "number_stairs") {
      GameObject stairs = new GameObject("SYPreview_number_stairs");
      stairs.transform.SetParent(parent, false);
      stairs.transform.localPosition = p + new Vector3(-0.3f, 0f, 0f);
      stairs.transform.localScale = Vector3.one * 1.4f;
      Ignore(stairs);
      Box(stairs.transform, "Step1", new Vector3(0f, 0.07f, 0f), new Vector3(0.30f, 0.14f, 0.34f),
        new Color(0.95f, 0.78f, 0.52f));
      Box(stairs.transform, "Step2", new Vector3(0.24f, 0.14f, 0f), new Vector3(0.30f, 0.28f, 0.34f),
        new Color(0.86f, 0.62f, 0.38f));
      Box(stairs.transform, "Step3", new Vector3(0.48f, 0.21f, 0f), new Vector3(0.30f, 0.42f, 0.34f),
        new Color(0.72f, 0.50f, 0.30f));
      Ball(stairs.transform, "Orb", new Vector3(0.48f, 0.52f, 0f), 0.22f, Gold);
      GameObject flag = Box(stairs.transform, "FlagPole", new Vector3(0.48f, 0.62f, -0.12f),
        new Vector3(0.04f, 0.5f, 0.04f), Wood);
      Box(flag.transform, "Flag", new Vector3(0.09f, 0.16f, 0f), new Vector3(0.18f, 0.12f, 0.03f), Coral);
    } else if (gameId == "shape_builder") {
      GameObject kit = new GameObject("SYPreview_shape_builder");
      kit.transform.SetParent(parent, false);
      kit.transform.localPosition = p;
      Ignore(kit);
      GeometryPlayBuilder.BuildKindVisual(kit.transform, GeometryKind.Triangle,
        GeometryShapes.ColorAt(0));
      GameObject sq = new GameObject("Sq");
      sq.transform.SetParent(kit.transform, false);
      sq.transform.localPosition = new Vector3(0.38f, 0f, 0f);
      sq.transform.localScale = Vector3.one * 0.55f;
      GeometryPlayBuilder.BuildKindVisual(sq.transform, GeometryKind.Square,
        GeometryShapes.ColorAt(1));
      GameObject ci = new GameObject("Ci");
      ci.transform.SetParent(kit.transform, false);
      ci.transform.localPosition = new Vector3(-0.38f, 0f, 0.12f);
      ci.transform.localScale = Vector3.one * 0.5f;
      GeometryPlayBuilder.BuildKindVisual(ci.transform, GeometryKind.Circle,
        GeometryShapes.ColorAt(2));
    } else if (gameId == "comparison_market") {
      GameObject kit = new GameObject("SYPreview_comparison_market");
      kit.transform.SetParent(parent, false);
      kit.transform.localPosition = p;
      Ignore(kit);
      // Two baskets, one fuller: the market motif, no words.
      for (int i = 0; i < 2; i++) {
        float bx = i == 0 ? -0.34f : 0.34f;
        int n = i == 0 ? 2 : 4;
        GameObject tub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tub.name = "Tub" + i;
        tub.transform.SetParent(kit.transform, false);
        tub.transform.localPosition = new Vector3(bx, 0.14f, 0f);
        tub.transform.localScale = new Vector3(0.5f, 0.2f, 0.5f);
        tub.GetComponent<Renderer>().sharedMaterial = Lit(i == 0 ? Wood : Gold);
        Strip(tub);
        Ignore(tub);
        for (int f = 0; f < n; f++) {
          Ball(kit.transform, "Apple" + i + "_" + f,
            new Vector3(bx + (f % 2 - 0.5f) * 0.2f, 0.3f + (f / 2) * 0.12f, (f % 3 - 1) * 0.1f),
            0.14f, new Color(0.92f, 0.32f, 0.34f));
        }
      }
    } else if (gameId == "classification_city") {
      GameObject kit = new GameObject("SYPreview_classification_city");
      kit.transform.SetParent(parent, false);
      kit.transform.localPosition = p;
      Ignore(kit);
      // Little house + garage: the sorting-town motif, no words.
      Box(kit.transform, "House", new Vector3(-0.4f, 0.25f, 0f), new Vector3(0.5f, 0.5f, 0.4f), Cream);
      Box(kit.transform, "HouseRoof", new Vector3(-0.4f, 0.55f, 0f), new Vector3(0.6f, 0.1f, 0.5f), Leaf);
      Box(kit.transform, "Garage", new Vector3(0.4f, 0.25f, 0f), new Vector3(0.5f, 0.5f, 0.4f), Gold);
      Ball(kit.transform, "Pet", new Vector3(-0.4f, 0.62f, 0f), 0.14f, Coral);
      Box(kit.transform, "Cartoy", new Vector3(0.4f, 0.55f, 0f), new Vector3(0.24f, 0.12f, 0.14f), Blue);
    } else if (gameId == "ordering_station") {
      GameObject kit = new GameObject("SYPreview_ordering_station");
      kit.transform.SetParent(parent, false);
      kit.transform.localPosition = p;
      Ignore(kit);
      // Three ascending steps: the ordering motif, no words.
      Box(kit.transform, "Rail", new Vector3(0f, 0.03f, 0.15f), new Vector3(1.3f, 0.06f, 0.16f), Wood);
      float[] hs = { 0.18f, 0.32f, 0.46f };
      for (int i = 0; i < 3; i++) {
        float sx = -0.4f + i * 0.4f;
        Box(kit.transform, "Step" + i, new Vector3(sx, hs[i] * 0.5f, 0f),
          new Vector3(0.26f, hs[i], 0.26f), AccentFor(i));
        Ball(kit.transform, "Orb" + i, new Vector3(sx, hs[i] + 0.1f, 0f), 0.14f, Gold);
      }
    }
  }

  static Color AccentFor(int index) {
    Color[] palette = { Gold, Blue, Coral, Leaf, Plum, Sky };
    return palette[Mathf.Abs(index) % palette.Length];
  }

  // Collider-free garden so a yard never reads as an empty lawn. Trees and
  // lanterns sit off the entry spine and the gate mouths; Seal keeps every
  // mesh out of the NavMesh bake (headroom rule).
  void BuildDressing(Transform parent) {
    GameObject dress = new GameObject("SYDressing");
    dress.transform.SetParent(parent, false);
    Transform d = dress.transform;
    Vector3[] trees = {
      new Vector3(-11.2f, 0f, 5.6f), new Vector3(11.2f, 0f, 5.6f),
      new Vector3(-10.6f, 0f, -5.4f), new Vector3(10.6f, 0f, -5.4f),
    };
    float[] scales = { 0.85f, 0.78f, 0.72f, 0.8f };
    for (int i = 0; i < trees.Length; i++) {
      WorldBeauty.BlossomTree(d, "SYBlossomTree" + i, trees[i], scales[i]);
      WorldBeauty.PetalCarpet(d, "SYPetalCarpet" + i, trees[i], 2.2f);
    }
    WorldBeauty.PetalCarpet(d, "SYPlazaPetal", new Vector3(0f, 0f, 1.2f), 10.5f);
    Vector3[] drifts = {
      new Vector3(-5.4f, 0f, 0.4f), new Vector3(5.4f, 0f, 0.4f),
      new Vector3(-6.2f, 0f, 7.2f), new Vector3(6.2f, 0f, 7.2f),
      new Vector3(-3.6f, 0f, -6.4f), new Vector3(3.6f, 0f, -6.4f),
      new Vector3(-4.8f, 0f, 8.6f), new Vector3(4.8f, 0f, 8.6f),
    };
    for (int i = 0; i < drifts.Length; i++)
      WorldBeauty.FlowerDrift(d, "SYFlowerDrift" + i, drifts[i], 1.15f + (i % 2) * 0.2f);
    for (int i = 0; i < 8; i++)
      WorldBeauty.PathStone(d, "SYPathStone" + i, new Vector3(0f, 0f, -8.4f + i * 1.15f), 1.55f);
    WorldBeauty.Lantern(d, "SYLanternL0", new Vector3(-3.5f, 0f, -6.2f));
    WorldBeauty.Lantern(d, "SYLanternR0", new Vector3(3.5f, 0f, -6.2f));
    WorldBeauty.Lantern(d, "SYLanternL1", new Vector3(-3.5f, 0f, 0.6f));
    WorldBeauty.Lantern(d, "SYLanternR1", new Vector3(3.5f, 0f, 0.6f));
    WorldBeauty.Butterfly(d, "SYButterfly0", new Vector3(-4.2f, 0f, 2.2f), 1.6f, 0.2f,
      WorldBeauty.BlossomDeep, WorldBeauty.BlossomCream);
    WorldBeauty.Butterfly(d, "SYButterfly1", new Vector3(4.4f, 0f, 6.4f), 1.6f, 0.7f,
      WorldBeauty.Lilac, WorldBeauty.BlossomPink);
    WorldBeauty.PetalFall(d, "SYPetalFall", new Vector3(0f, 0f, 2f), 8f, 10, 43030);
    for (int i = 0; i < GateRoots.Count; i++) {
      Vector3 g = GateRoots[i].localPosition;
      WorldBeauty.FlowerDrift(d, "SYGateFlowersL" + i, g + new Vector3(-1.35f, 0f, -0.15f), 0.7f);
      WorldBeauty.FlowerDrift(d, "SYGateFlowersR" + i, g + new Vector3(1.35f, 0f, -0.15f), 0.7f);
    }
    if (PlaceholderBoard != null) {
      WorldBeauty.Pond(d, "SYPond", new Vector3(4.6f, 0f, 1.1f), 2.6f);
      WorldBeauty.BlossomTree(d, "SYQuietTree", new Vector3(-5.4f, 0f, 2.0f), 0.9f);
      WorldBeauty.PetalCarpet(d, "SYQuietCarpet", new Vector3(-5.4f, 0f, 2.0f), 2.4f);
      WorldBeauty.FlowerDrift(d, "SYQuietFlowersL", new Vector3(-2.3f, 0f, 3.1f), 1.1f);
      WorldBeauty.FlowerDrift(d, "SYQuietFlowersR", new Vector3(2.3f, 0f, 3.1f), 1.1f);
      WorldBeauty.Butterfly(d, "SYQuietButterfly", new Vector3(3.2f, 0f, 2.4f), 1.4f, 0.4f,
        WorldBeauty.Peach, WorldBeauty.BlossomPink);
    }
    WorldBeauty.Seal(dress);
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
