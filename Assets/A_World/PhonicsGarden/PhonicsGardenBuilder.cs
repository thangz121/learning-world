// A_World/PhonicsGarden/PhonicsGardenBuilder.cs — Builder for "Vườn Đánh Vần" & "Vườn Tri Thức".
// Creates the Vietnamese garden setting: bamboo gate, lotus pond, wooden phonics board,
// 3 interactive choice cards, and the Teacher NPC. Presentation only. C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PhonicsGardenBuilder : MonoBehaviour {
  public const string SceneName = "PhonicsGardenPlayScene";
  public static readonly Vector3 WorldOffset = new Vector3(840f, 0f, 0f);
  public const float BoundX = 14f;
  public const float BoundZ = 14f;
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -3f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -10.8f);
  public static readonly Vector3 FollowOffset = new Vector3(0f, 2.8f, -4.2f);
  public static readonly Vector3 PlaySpotLocal = new Vector3(0f, 0f, 0.2f);

  public static readonly Vector3 BoardLocal = new Vector3(0f, 1.8f, 4.4f);
  public static readonly Vector3 PhotoFrameLocal = new Vector3(-2.8f, 1.8f, 4.4f);
  public static readonly Vector3 TeacherLocal = new Vector3(2.6f, 0f, 4.2f);

  public static readonly Vector3 SlotA = new Vector3(-2.2f, 0.45f, 2.2f);
  public static readonly Vector3 SlotB = new Vector3(0f, 0.45f, 2.2f);
  public static readonly Vector3 SlotC = new Vector3(2.2f, 0.45f, 2.2f);

  static readonly Color Lawn = new Color(0.44f, 0.70f, 0.42f);
  static readonly Color Wood = new Color(0.55f, 0.40f, 0.24f);
  static readonly Color Bamboo = new Color(0.68f, 0.75f, 0.38f);
  static readonly Color Cream = new Color(0.99f, 0.95f, 0.85f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color LotusPink = new Color(0.95f, 0.55f, 0.72f);
  static readonly Color WaterBlue = new Color(0.35f, 0.65f, 0.85f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public Transform PlaySpot { get; private set; }
  public Transform RoundRoot { get; private set; }
  public PhonicsBoard Board { get; private set; }
  public readonly List<PhonicsChoiceCard> Choices = new List<PhonicsChoiceCard>();

  public GameObject TeacherNpc { get; private set; }
  Animator _teacherAnimator;
  CharacterPresentation _teacherPresentation;

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
    BuildGardenScenery(root);
    BuildPhonicsBoard(root);
    BuildTeacherNpc(root);

    GameObject rr = new GameObject("PGRound");
    rr.transform.SetParent(root, false);
    RoundRoot = rr.transform;

    BuildAnchors(root);

    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  void BuildGround(Transform parent) {
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "PGGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(Lawn);

    Pad(parent, "PGPlaza", new Vector3(0f, 0.006f, 2.4f), 10.5f, new Color(0.93f, 0.84f, 0.58f));
    Box(parent, "PGPath", new Vector3(0f, 0.01f, -4.6f), new Vector3(1.8f, 0.02f, 8.4f),
      new Color(0.76f, 0.60f, 0.40f));
    StageLight(parent, new Vector3(0f, 7.5f, 2.8f));
  }

  void BuildEntryAndExit(Transform parent) {
    // Bamboo Gate (Cổng làng tre uốn cong)
    Box(parent, "PGBambooPostL", new Vector3(-1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Bamboo);
    Box(parent, "PGBambooPostR", new Vector3(1.5f, 1.0f, -9.2f), new Vector3(0.2f, 2.0f, 0.2f), Bamboo);
    GameObject beam = Box(parent, "PGBambooBeam", new Vector3(0f, 2.08f, -9.2f),
      new Vector3(3.2f, 0.16f, 0.16f), Bamboo);
    Ignore(beam);

    Pad(parent, "PGPlayDisc", PlaySpotLocal + new Vector3(0f, 0.012f, 0f), 3.0f, Gold);
    GameObject spot = new GameObject("PGPlaySpot");
    spot.transform.SetParent(parent, false);
    spot.transform.localPosition = PlaySpotLocal;
    PlaySpot = spot.transform;

    Pad(parent, "PGExitDisc", ExitLocal + new Vector3(0f, 0.012f, 0.3f), 3.0f,
      new Color(0.70f, 0.90f, 0.72f));
    GameObject exitGo = new GameObject("PGExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.PlayExit = true;
    exit.fireRadius = 1.35f;
    ExitPortal = exit;
  }

  void BuildGardenScenery(Transform parent) {
    // Lotus Pond (Hồ hoa sen tây-bắc)
    GameObject pond = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pond.name = "LotusPond";
    pond.transform.SetParent(parent, false);
    pond.transform.localPosition = new Vector3(-5.2f, 0.02f, 5.2f);
    pond.transform.localScale = new Vector3(3.6f, 0.04f, 3.6f);
    pond.GetComponent<Renderer>().sharedMaterial = Lit(WaterBlue, 0.8f);
    Strip(pond);

    // Lotus flowers on pond
    for (int i = 0; i < 3; i++) {
      float ang = i * 2.1f;
      Vector3 fPos = new Vector3(-5.2f + Mathf.Cos(ang) * 1.1f, 0.08f, 5.2f + Mathf.Sin(ang) * 1.1f);
      Pad(parent, "Lotus" + i, fPos, 0.5f, LotusPink);
    }

    // Bamboo Grove (Rặng tre đông-bắc)
    for (int i = 0; i < 5; i++) {
      Vector3 bPos = new Vector3(5.0f + (i % 2) * 0.7f, 1.6f, 4.8f + (i / 2) * 0.9f);
      GameObject bStalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      bStalk.name = "BambooStalk" + i;
      bStalk.transform.SetParent(parent, false);
      bStalk.transform.localPosition = bPos;
      bStalk.transform.localScale = new Vector3(0.12f, 1.6f, 0.12f);
      bStalk.GetComponent<Renderer>().sharedMaterial = Lit(Bamboo);
      Strip(bStalk);
      Ignore(bStalk);
    }
  }

  void BuildPhonicsBoard(Transform parent) {
    GameObject root = new GameObject("PGPhonicsBoardRoot");
    root.transform.SetParent(parent, false);
    root.transform.localPosition = BoardLocal;

    // 1. Wooden Stand Posts
    Box(root.transform, "StandL", new Vector3(-1.6f, -0.9f, 0f), new Vector3(0.16f, 1.8f, 0.16f), Wood);
    Box(root.transform, "StandR", new Vector3(1.6f, -0.9f, 0f), new Vector3(0.16f, 1.8f, 0.16f), Wood);

    // 2. Main Board Plank (Bảng gỗ chạm khắc)
    GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
    plank.name = "BoardPlank";
    plank.transform.SetParent(root.transform, false);
    plank.transform.localPosition = Vector3.zero;
    plank.transform.localScale = new Vector3(3.5f, 1.4f, 0.15f);
    plank.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.48f, 0.32f, 0.18f), 0.1f);
    Strip(plank);

    // 3. Word TextMesh Label
    GameObject textGo = new GameObject("WordLabel");
    textGo.transform.SetParent(root.transform, false);
    textGo.transform.localPosition = new Vector3(0f, 0.05f, -0.1f);
    TextMesh tm = textGo.AddComponent<TextMesh>();
    tm.font = UiFont.GetBold();
    MeshRenderer mr = textGo.GetComponent<MeshRenderer>();
    if (mr != null && tm.font != null) mr.sharedMaterial = tm.font.material;
    tm.text = "CÁ";
    tm.fontSize = 72;
    tm.characterSize = 0.06f;
    tm.alignment = TextAlignment.Center;
    tm.anchor = TextAnchor.MiddleCenter;
    tm.color = Color.white;

    // 4. Real Photo Frame on the side (Khung ảnh thật 100% mở ra ở sai lần 2)
    GameObject photoFrame = new GameObject("PhotoFrameRoot");
    photoFrame.transform.SetParent(parent, false);
    photoFrame.transform.localPosition = PhotoFrameLocal;

    GameObject frameBorder = GameObject.CreatePrimitive(PrimitiveType.Cube);
    frameBorder.name = "FrameBorder";
    frameBorder.transform.SetParent(photoFrame.transform, false);
    frameBorder.transform.localPosition = Vector3.zero;
    frameBorder.transform.localScale = new Vector3(1.8f, 1.8f, 0.12f);
    frameBorder.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
    Strip(frameBorder);

    GameObject photoDisplay = new GameObject("PhotoDisplay");
    photoDisplay.transform.SetParent(photoFrame.transform, false);
    photoDisplay.transform.localPosition = new Vector3(0f, 0f, -0.08f);
    photoDisplay.transform.localScale = Vector3.one * 1.25f;
    SpriteRenderer sr = photoDisplay.AddComponent<SpriteRenderer>();

    photoFrame.SetActive(false); // Hidden by default; opens on Mistake 2

    // Attach PhonicsBoard component
    PhonicsBoard pb = root.AddComponent<PhonicsBoard>();
    pb.WordLabel = tm;
    pb.PhotoRenderer = sr;
    pb.PhotoFrameRoot = photoFrame;
    pb.BoardPlank = plank.transform;
    Board = pb;
  }

  void BuildTeacherNpc(Transform parent) {
    GameObject teacherRoot = new GameObject("TeacherNpc");
    teacherRoot.transform.SetParent(parent, false);
    teacherRoot.transform.localPosition = TeacherLocal;
    teacherRoot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
    TeacherNpc = teacherRoot;

    GameObject visualPrefab = Resources.Load<GameObject>("NpcVisuals/MiaVisual");
    if (visualPrefab != null) {
      GameObject visual = Object.Instantiate(visualPrefab, teacherRoot.transform, false);
      visual.name = "TeacherVisualRoot";
      visual.transform.localPosition = new Vector3(0f, 0.02f, 0f);
      visual.transform.localScale = Vector3.one * 0.5f;

      _teacherAnimator = visual.GetComponentInChildren<Animator>(true);
      SkinnedMeshRenderer skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
      Transform headBone = null;
      if (skin != null && skin.bones != null) {
        foreach (Transform b in skin.bones) {
          if (b != null && b.name.IndexOf("Head", System.StringComparison.OrdinalIgnoreCase) >= 0) {
            headBone = b;
            break;
          }
        }
      }
      _teacherPresentation = visual.AddComponent<CharacterPresentation>();
      _teacherPresentation.SetupFace(skin, headBone, visual.transform, visual.transform);
    }
  }

  public void TeacherCelebrate() {
    if (_teacherAnimator != null) _teacherAnimator.SetTrigger("Celebrate");
    if (_teacherPresentation != null) _teacherPresentation.PulseExpression(CharacterExpression.Happy, 2.5f);
  }

  public void TeacherEncourage() {
    if (_teacherPresentation != null) _teacherPresentation.PulseExpression(CharacterExpression.Curious, 2.0f);
  }

  public void TeacherWave() {
    if (_teacherPresentation != null) _teacherPresentation.PulseExpression(CharacterExpression.Happy, 1.8f);
  }

  public void ClearRound() {
    Choices.Clear();
    if (RoundRoot == null) return;
    for (int i = RoundRoot.childCount - 1; i >= 0; i--) {
      GameObject c = RoundRoot.GetChild(i).gameObject;
      CharacterPresentation.DestroyNow(c);
    }
    if (Board != null) Board.HidePhotoFrame();
  }

  // Spawns 3 interactive choice cards at SlotA, SlotB, SlotC
  public void SpawnChoiceCards(PhonicsGarden game, string correctWord, string[] distractors) {
    ClearRound();

    List<string> options = new List<string> { correctWord };
    if (distractors != null) {
      for (int i = 0; i < distractors.Length && options.Count < 3; i++) {
        options.Add(distractors[i]);
      }
    }
    while (options.Count < 3) options.Add("TỪ" + options.Count);

    // Shuffle options across the 3 slots
    for (int i = 0; i < options.Count; i++) {
      int rnd = UnityEngine.Random.Range(i, options.Count);
      string temp = options[i];
      options[i] = options[rnd];
      options[rnd] = temp;
    }

    Vector3[] slots = { SlotA, SlotB, SlotC };
    for (int i = 0; i < 3; i++) {
      string text = options[i];
      bool isAns = string.Equals(text, correctWord, System.StringComparison.OrdinalIgnoreCase);

      GameObject cardGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
      cardGo.name = "ChoiceCard_" + i;
      cardGo.transform.SetParent(RoundRoot, false);
      cardGo.transform.localPosition = slots[i];
      cardGo.transform.localScale = new Vector3(1.7f, 0.9f, 0.15f);
      cardGo.GetComponent<Renderer>().sharedMaterial = Lit(Cream);

      BoxCollider col = cardGo.GetComponent<BoxCollider>();
      col.size = new Vector3(1.8f, 1.0f, 0.4f);

      // Label on the card
      GameObject lblGo = new GameObject("CardLabel");
      lblGo.transform.SetParent(cardGo.transform, false);
      lblGo.transform.localPosition = new Vector3(0f, 0f, -0.1f);
      TextMesh tm = lblGo.AddComponent<TextMesh>();
      tm.font = UiFont.GetBold();
      MeshRenderer mrCard = lblGo.GetComponent<MeshRenderer>();
      if (mrCard != null && tm.font != null) mrCard.sharedMaterial = tm.font.material;
      tm.text = text;
      tm.fontSize = 54;
      tm.characterSize = 0.045f;
      tm.alignment = TextAlignment.Center;
      tm.anchor = TextAnchor.MiddleCenter;
      tm.color = new Color(0.2f, 0.2f, 0.25f);

      PhonicsChoiceCard card = cardGo.AddComponent<PhonicsChoiceCard>();
      card.Bind(game, text, isAns);
      Choices.Add(card);

      GameJuice.Pop(cardGo.transform, 0.15f, 0.3f);
    }
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "PhonicsGardenPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0f, 0f, 2.8f));
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 4.4f, -6.2f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 1.2f, 3.2f));
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
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
    GameObject go = new GameObject("PGFillLight");
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

  static Material Lit(Color color, float smoothness = 0f) {
    string key = color.r.ToString("F2") + "," + color.g.ToString("F2") + "," + color.b.ToString("F2") + "_" + smoothness.ToString("F2");
    Material cached;
    if (_mats.TryGetValue(key, out cached) && cached != null) return cached;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
    mat.enableInstancing = true;
    _mats[key] = mat;
    return mat;
  }
}
