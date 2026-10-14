// A_World/SentenceExpress/SentenceExpressBuilder.cs — "Đoàn Tàu Ghép Câu"
// (English -> Sentences for 4-year-olds). Presentation only: fairy train station,
// cute wooden steam locomotive engine, pastel rail tracks, and connecting train cars.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SentenceExpressBuilder : MonoBehaviour {
  public const string SceneName = "SentenceExpressPlayScene";
  public static readonly Vector3 WorldOffset = new Vector3(900f, 0f, 200f);
  public const float BoundX = 18f;
  public const float BoundZ = 18f;
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -4f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -11.5f);
  public static readonly Vector3 FollowOffset = new Vector3(0f, 5.2f, -7.5f);
  public static readonly Vector3 PlaySpotLocal = new Vector3(0f, 0f, -1.2f);
  public static readonly Vector3 EngineLocal = new Vector3(5.2f, 0f, 3.2f);
  public const float RailZ = 3.2f;
  public const float SlotSpacing = 2.4f;
  public const string ObjectiveEn = "Find the missing letter to complete the train!";
  public const string ObjectiveVi = "Tìm chữ cái còn thiếu để hoàn thành đoàn tàu nhé!";

  static readonly Color MeadowLawn = new Color(0.48f, 0.78f, 0.52f);
  static readonly Color PlatformWood = new Color(0.85f, 0.70f, 0.82f); // Pastel fairy purple wood
  static readonly Color RailIron = new Color(0.65f, 0.60f, 0.70f);
  static readonly Color Cream = new Color(0.99f, 0.96f, 0.92f);
  static readonly Color Gold = new Color(1.0f, 0.85f, 0.38f);
  static readonly Color FairyPink = new Color(1.0f, 0.65f, 0.82f);
  static readonly Color FairyMint = new Color(0.65f, 0.96f, 0.85f);
  static readonly Color FairySky = new Color(0.65f, 0.85f, 1.0f);
  static readonly Color EngineRed = new Color(0.95f, 0.45f, 0.55f);

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public Transform PlaySpot { get; private set; }
  public Transform TrainTrackRoot { get; private set; }
  public Transform RoundRoot { get; private set; }
  public Transform ChoicePanelRoot { get; private set; }
  public Transform WordPicAnchor { get; private set; }
  public Transform EngineTransform { get; private set; }
  public Transform BunnyConductorTransform { get; private set; }
  public TeacherGuidePresenter GuideNpc { get; private set; }
  public readonly List<SentenceCarChoice> CarWagons = new List<SentenceCarChoice>();
  public List<SentenceCarChoice> Choices => CarWagons;
  public readonly List<SentenceLetterChoice> LetterChoices = new List<SentenceLetterChoice>();

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
    BuildTrainStationPlatform(root);
    BuildTracksAndLocomotive(root);
    BuildGuideNpc(root);

    // Anchor for the 3D Picture Model of the target word (placed prominently on front stage)
    GameObject picAnchor = new GameObject("SEWordPicAnchor");
    picAnchor.transform.SetParent(root, false);
    picAnchor.transform.localPosition = new Vector3(-3.2f, 0f, 1.2f);
    WordPicAnchor = picAnchor.transform;
    Pad(picAnchor.transform, "PicPedestal", Vector3.zero, 2.4f, Cream);
    Pad(picAnchor.transform, "PicPedestalRim", new Vector3(0f, -0.005f, 0f), 2.7f, Gold);

    GameObject rr = new GameObject("SERound");
    rr.transform.SetParent(root, false);
    RoundRoot = rr.transform;

    GameObject cp = new GameObject("SEChoicePanel");
    cp.transform.SetParent(root, false);
    ChoicePanelRoot = cp.transform;

    BuildDressing(root);
    BuildAnchors(root);
    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  void BuildGround(Transform parent) {
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "SERim";
    rim.transform.SetParent(parent, false);
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(44f, 1.4f, 44f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.52f, 0.42f, 0.55f));
    Strip(rim);
    Ignore(rim);

    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "SEGround";
    ground.transform.SetParent(parent, false);
    ground.transform.localScale = new Vector3(3.8f, 1f, 3.8f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(MeadowLawn);

    Pad(parent, "SEStationDeck", new Vector3(0f, 0.008f, 0.5f), 13.5f, PlatformWood);
    Box(parent, "SEPath", new Vector3(0f, 0.012f, -6.0f), new Vector3(2.2f, 0.02f, 9.5f), Cream);
    StageLight(parent, new Vector3(0f, 7.5f, 1.5f));
  }

  void BuildEntryAndExit(Transform parent) {
    Box(parent, "SEEntryPostL", new Vector3(-1.6f, 1.0f, -9.8f), new Vector3(0.2f, 2.0f, 0.2f), FairyMint);
    Box(parent, "SEEntryPostR", new Vector3(1.6f, 1.0f, -9.8f), new Vector3(0.2f, 2.0f, 0.2f), FairyMint);
    GameObject beam = Box(parent, "SEEntryBeam", new Vector3(0f, 2.08f, -9.8f),
      new Vector3(3.4f, 0.16f, 0.16f), FairyPink);
    Ignore(beam);
    Ball(parent, "SEEntryStar", new Vector3(0f, 2.35f, -9.8f), 0.24f, Gold);

    Pad(parent, "SEPlayDisc", PlaySpotLocal + new Vector3(0f, 0.014f, 0f), 3.0f, Gold);
    GameObject spot = new GameObject("SEPlaySpot");
    spot.transform.SetParent(parent, false);
    spot.transform.localPosition = PlaySpotLocal;
    PlaySpot = spot.transform;

    Pad(parent, "SEExitDisc", ExitLocal + new Vector3(0f, 0.012f, 0.3f), 3.0f, FairyMint);
    GameObject exitGo = new GameObject("SEExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    MicroWorldPortal exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.PlayExit = true;
    exit.fireRadius = 1.35f;
    ExitPortal = exit;
  }

  void BuildTrainStationPlatform(Transform parent) {
    Box(parent, "SEPlatformL", new Vector3(-7.5f, 1.4f, RailZ + 1.2f), new Vector3(0.18f, 2.8f, 0.18f), PlatformWood);
    Box(parent, "SEPlatformR", new Vector3(7.5f, 1.4f, RailZ + 1.2f), new Vector3(0.18f, 2.8f, 0.18f), PlatformWood);
    GameObject canopy = Box(parent, "SECanopy", new Vector3(0f, 2.85f, RailZ + 1.2f), new Vector3(15.8f, 0.15f, 2.6f), FairySky);
    Ignore(canopy);
  }

  void BuildTracksAndLocomotive(Transform parent) {
    GameObject trackRoot = new GameObject("SETracks");
    trackRoot.transform.SetParent(parent, false);
    TrainTrackRoot = trackRoot.transform;

    // Extra long parallel rails (28 meters)
    Box(trackRoot.transform, "Rail1", new Vector3(0f, 0.02f, RailZ - 0.45f), new Vector3(28.0f, 0.04f, 0.08f), RailIron);
    Box(trackRoot.transform, "Rail2", new Vector3(0f, 0.02f, RailZ + 0.45f), new Vector3(28.0f, 0.04f, 0.08f), RailIron);

    // 32 Wooden railway ties
    for (int i = 0; i < 32; i++) {
      float rx = -13.0f + i * 0.85f;
      Box(trackRoot.transform, "Tie_" + i, new Vector3(rx, 0.015f, RailZ), new Vector3(0.22f, 0.03f, 1.2f), PlatformWood);
    }

    // Locomotive heading RIGHT (+X)
    GameObject eng = new GameObject("SEEngine");
    eng.transform.SetParent(parent, false);
    eng.transform.localPosition = EngineLocal;

    // Boiler body (facing +X)
    GameObject boiler = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    boiler.name = "Boiler";
    boiler.transform.SetParent(eng.transform, false);
    boiler.transform.localPosition = new Vector3(0.4f, 0.65f, 0f);
    boiler.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
    boiler.transform.localScale = new Vector3(0.7f, 0.65f, 0.7f);
    boiler.GetComponent<Renderer>().sharedMaterial = Lit(EngineRed);
    Strip(boiler);
    Ignore(boiler);

    // Cab (at the rear, -X)
    Box(eng.transform, "Cab", new Vector3(-0.35f, 0.85f, 0f), new Vector3(0.7f, 0.95f, 0.8f), FairyPink);
    Box(eng.transform, "Roof", new Vector3(-0.35f, 1.35f, 0f), new Vector3(0.85f, 0.1f, 0.95f), Gold);

    // Smokestack & Chimney (at the front, +X) — realistic bell-top steam stack
    GameObject stack = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stack.name = "Smokestack";
    stack.transform.SetParent(eng.transform, false);
    stack.transform.localPosition = new Vector3(0.75f, 1.20f, 0f);
    stack.transform.localScale = new Vector3(0.28f, 0.38f, 0.28f);
    stack.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
    Strip(stack);
    Ignore(stack);

    // Chimney Rim (funnel top flared lip)
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "SmokestackRim";
    rim.transform.SetParent(eng.transform, false);
    rim.transform.localPosition = new Vector3(0.75f, 1.40f, 0f);
    rim.transform.localScale = new Vector3(0.38f, 0.08f, 0.38f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(EngineRed);
    Strip(rim);
    Ignore(rim);

    // Steam Dome (golden rounded dome in middle of boiler)
    GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    dome.name = "SteamDome";
    dome.transform.SetParent(eng.transform, false);
    dome.transform.localPosition = new Vector3(0.25f, 1.08f, 0f);
    dome.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
    dome.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
    Strip(dome);
    Ignore(dome);

    // Front Pilot / Cowcatcher (grate at very front)
    Box(eng.transform, "Cowcatcher", new Vector3(0.95f, 0.32f, 0f), new Vector3(0.25f, 0.22f, 0.85f), Gold);

    // Rear Coupler bar linking locomotive to the first wagon (-X side)
    Box(eng.transform, "EngineCoupler", new Vector3(-0.95f, 0.38f, 0f), new Vector3(0.85f, 0.08f, 0.12f), RailIron);
    Ball(eng.transform, "EngineCouplerPin", new Vector3(-1.35f, 0.42f, 0f), 0.10f, Gold);

    // 4 Wheels
    for (int w = 0; w < 4; w++) {
      float wx = -0.45f + (w % 2) * 0.9f;
      float wz = (w / 2 == 0) ? -0.42f : 0.42f;
      GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      wheel.name = "Wheel_" + w;
      wheel.transform.SetParent(eng.transform, false);
      wheel.transform.localPosition = new Vector3(wx, 0.24f, wz);
      wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
      wheel.transform.localScale = new Vector3(0.48f, 0.06f, 0.48f);
      wheel.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
      Strip(wheel);
      Ignore(wheel);
    }

    BuildBunnyConductor(eng.transform, new Vector3(-0.35f, 0.92f, 0f));
    EngineTransform = eng.transform;

    UnityEngine.AI.NavMeshObstacle obs = eng.AddComponent<UnityEngine.AI.NavMeshObstacle>();
    obs.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
    obs.center = new Vector3(0f, 0.6f, 0f);
    obs.size = new Vector3(2.0f, 1.4f, 1.2f);
    obs.carving = true;
    Ignore(eng);
  }

  void BuildBunnyConductor(Transform parent, Vector3 localPos) {
    GameObject bunny = new GameObject("SEBunnyConductor");
    bunny.transform.SetParent(parent, false);
    bunny.transform.localPosition = localPos;
    bunny.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // Looking forward (+X)
    BunnyConductorTransform = bunny.transform;

    Ball(bunny.transform, "BunnyBody", new Vector3(0f, 0.22f, 0f), 0.24f, Cream);
    Ball(bunny.transform, "BunnyHead", new Vector3(0f, 0.44f, 0f), 0.20f, Cream);
    Ball(bunny.transform, "EarL", new Vector3(-0.08f, 0.62f, 0f), 0.07f, Cream);
    Ball(bunny.transform, "EarR", new Vector3(0.08f, 0.62f, 0f), 0.07f, Cream);
    GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    cap.name = "Cap";
    cap.transform.SetParent(bunny.transform, false);
    cap.transform.localPosition = new Vector3(0f, 0.55f, 0f);
    cap.transform.localScale = new Vector3(0.20f, 0.06f, 0.20f);
    cap.GetComponent<Renderer>().sharedMaterial = Lit(FairySky);
    Strip(cap);
    Ignore(cap);
    Ignore(bunny);
  }

  void BuildGuideNpc(Transform parent) {
    GameObject guideGo = new GameObject("SETeacherGuide");
    guideGo.transform.SetParent(parent, false);
    TeacherGuidePresenter guide = guideGo.AddComponent<TeacherGuidePresenter>();
    guide.Setup(new Vector3(-4.2f, 0f, 1.5f));
    GuideNpc = guide;
  }

  void BuildDressing(Transform parent) {
    WorldBeauty.BlossomTree(parent, "SETree0", new Vector3(-8.5f, 0f, 6.2f), 0.85f);
    WorldBeauty.BlossomTree(parent, "SETree1", new Vector3(8.5f, 0f, 6.2f), 0.85f);
    WorldBeauty.FlowerDrift(parent, "SEFlowers0", new Vector3(-3.4f, 0f, -2.4f), 1.2f);
    WorldBeauty.FlowerDrift(parent, "SEFlowers1", new Vector3(3.4f, 0f, -2.4f), 1.2f);

    PropKit.Place(parent, "fence_simple", new Vector3(-5.2f, 0f, RailZ + 2.0f), 0f, 1.2f, true);
    PropKit.Place(parent, "fence_simple", new Vector3(5.2f, 0f, RailZ + 2.0f), 0f, 1.2f, true);
    PropKit.Place(parent, "plant_bushDetailed", new Vector3(-6.2f, 0f, 0f), 30f, 1.2f, true);
    PropKit.Place(parent, "plant_bushDetailed", new Vector3(6.2f, 0f, 0f), -30f, 1.2f, true);
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "SentenceExpressPresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(0f, 0f, RailZ));
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 5.0f, -7.2f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 1.4f, RailZ));
    a.Exit = a.EnsureSlot("ExitAnchor", ExitLocal);
  }

  public static Vector3 CarSlotAt(int index, int count) {
    float x = 2.4f - (count - index) * SlotSpacing;
    return new Vector3(x, 0f, RailZ);
  }

  public void ClearRound() {
    CarWagons.Clear();
    LetterChoices.Clear();
    if (RoundRoot != null) {
      for (int i = RoundRoot.childCount - 1; i >= 0; i--) {
        CharacterPresentation.DestroyNow(RoundRoot.GetChild(i).gameObject);
      }
    }
    if (ChoicePanelRoot != null) {
      for (int i = ChoicePanelRoot.childCount - 1; i >= 0; i--) {
        CharacterPresentation.DestroyNow(ChoicePanelRoot.GetChild(i).gameObject);
      }
    }
    if (WordPicAnchor != null) {
      for (int i = WordPicAnchor.childCount - 1; i >= 0; i--) {
        Transform child = WordPicAnchor.GetChild(i);
        if (child.name != "PicPedestal") CharacterPresentation.DestroyNow(child.gameObject);
      }
    }
  }

  // Shows the 3D model of the word on the display pedestal
  public void ShowWordPicture(string wordId) {
    if (WordPicAnchor == null) return;
    string id = (wordId ?? "").ToLowerInvariant();

    if (id == "ball") {
      GameObject ballGo = Ball(WordPicAnchor, "BallProp", new Vector3(0f, 0.45f, 0f), 0.38f, new Color(0.98f, 0.55f, 0.25f));
      GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      band.name = "Band";
      band.transform.SetParent(ballGo.transform, false);
      band.transform.localPosition = Vector3.zero;
      band.transform.localScale = new Vector3(1.05f, 0.05f, 1.05f);
      band.GetComponent<Renderer>().sharedMaterial = Lit(Cream);
      Strip(band);
      Ignore(band);
      GameJuice.Pop(ballGo.transform, 0.35f, 0.45f);
      return;
    }

    float targetSize = 0.85f;
    string propName = ListeningWords.PropName(id);
    GameObject prop = PropKit.PlaceFitted(WordPicAnchor, propName,
      new Vector3(0f, 0.1f, 0f), 0f, targetSize, true);
    if (prop != null) {
      GameJuice.Pop(prop.transform, 0.35f, 0.45f);
    } else {
      // Graceful fallback for words like "cat" using cute procedural kitty head
      GameObject catProp = new GameObject("CatDisplayProp");
      catProp.transform.SetParent(WordPicAnchor, false);
      catProp.transform.localPosition = new Vector3(0f, 0.25f, 0f);
      Ball(catProp.transform, "CatHead", Vector3.zero, 0.26f, Cream);
      Ball(catProp.transform, "EarL", new Vector3(-0.14f, 0.18f, 0f), 0.09f, FairyPink);
      Ball(catProp.transform, "EarR", new Vector3(0.14f, 0.18f, 0f), 0.09f, FairyPink);
      Ball(catProp.transform, "Nose", new Vector3(0f, -0.02f, -0.22f), 0.04f, FairyPink);
      GameJuice.Pop(catProp.transform, 0.35f, 0.45f);
    }
  }

  // Spawns one wagon with letter or placeholder
  public SentenceCarChoice SpawnCar(int carIndex, int totalCars, string text, bool isMissingSlot) {
    GameObject root = new GameObject("SECar_" + text);
    root.transform.SetParent(RoundRoot, false);
    root.transform.localPosition = CarSlotAt(carIndex, totalCars);

    // Wagon chassis
    Color chassisCol = isMissingSlot ? FairySky : FairyPink;
    Box(root.transform, "WagonChassis", new Vector3(0f, 0.42f, 0f), new Vector3(1.4f, 0.45f, 0.85f), chassisCol);
    Box(root.transform, "WagonTrim", new Vector3(0f, 0.68f, 0f), new Vector3(1.46f, 0.08f, 0.9f), Gold);

    for (int w = 0; w < 4; w++) {
      float wx = (w % 2 == 0) ? -0.45f : 0.45f;
      float wz = (w / 2 == 0) ? -0.44f : 0.44f;
      GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      wheel.name = "Wheel_" + w;
      wheel.transform.SetParent(root.transform, false);
      wheel.transform.localPosition = new Vector3(wx, 0.22f, wz);
      wheel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
      wheel.transform.localScale = new Vector3(0.42f, 0.05f, 0.42f);
      wheel.GetComponent<Renderer>().sharedMaterial = Lit(Gold);
      Strip(wheel);
      Ignore(wheel);
    }

    Box(root.transform, "CardPost", new Vector3(0f, 0.95f, 0f), new Vector3(0.1f, 0.55f, 0.1f), PlatformWood);
    Box(root.transform, "CardBoard", new Vector3(0f, 1.35f, 0f), new Vector3(1.1f, 0.55f, 0.1f), Cream);
    Box(root.transform, "CardBorder", new Vector3(0f, 1.35f, 0.02f), new Vector3(1.16f, 0.6f, 0.06f), isMissingSlot ? Gold : FairyMint);

    GameObject textGo = new GameObject("CarLabel");
    textGo.transform.SetParent(root.transform, false);
    textGo.transform.localPosition = new Vector3(0f, 1.35f, -0.06f);
    TextMesh tm = textGo.AddComponent<TextMesh>();
    tm.text = isMissingSlot ? "?" : text;
    tm.fontSize = 44;
    tm.characterSize = 0.08f;
    tm.anchor = TextAnchor.MiddleCenter;
    tm.alignment = TextAlignment.Center;
    tm.color = isMissingSlot ? new Color(0.85f, 0.25f, 0.25f) : new Color(0.25f, 0.20f, 0.35f);

    // Coupler bars between wagons (front hook + rear link)
    // Front hitch (+X side) connecting towards next car / locomotive
    Box(root.transform, "FrontCoupler", new Vector3(0.85f, 0.36f, 0f), new Vector3(0.55f, 0.07f, 0.10f), RailIron);
    Ball(root.transform, "FrontPin", new Vector3(1.10f, 0.40f, 0f), 0.08f, Gold);

    // Rear hitch (-X side) connecting towards preceding car
    Box(root.transform, "RearCoupler", new Vector3(-0.85f, 0.36f, 0f), new Vector3(0.55f, 0.07f, 0.10f), RailIron);
    Ball(root.transform, "RearPin", new Vector3(-1.10f, 0.40f, 0f), 0.08f, Gold);

    SentenceCarChoice choice = root.AddComponent<SentenceCarChoice>();
    choice.CardText = text;
    choice.CorrectStepOrder = carIndex;
    CarWagons.Add(choice);
    return choice;
  }

  // Spawns 4 multiple-choice letter pills on the player's front interaction plaza
  public SentenceLetterChoice SpawnLetterChoice(int index, int total, string letter, bool isCorrect) {
    GameObject root = new GameObject("SEChoice_" + letter);
    root.transform.SetParent(ChoicePanelRoot, false);

    // Arranged in front of the child's feet (z = 0.5f, spaced horizontally)
    float spacing = 1.45f;
    float x = (index - (total - 1) * 0.5f) * spacing;
    root.transform.localPosition = new Vector3(x, 0f, 0.5f);

    // Beautiful tilted interaction easel pod facing camera (tilted 35 degrees up)
    GameObject stand = new GameObject("ChoiceStand");
    stand.transform.SetParent(root.transform, false);
    stand.transform.localPosition = new Vector3(0f, 0.15f, 0f);
    stand.transform.localRotation = Quaternion.Euler(-32f, 0f, 0f); // Tilted towards top-down camera!

    // Plinth base
    Box(root.transform, "Plinth", new Vector3(0f, 0.04f, 0f), new Vector3(1.15f, 0.08f, 0.85f), PlatformWood);

    // Outer Glowing Border
    GameObject border = Box(stand.transform, "Border", new Vector3(0f, 0.5f, 0f), new Vector3(1.22f, 0.98f, 0.12f), Gold);
    Ignore(border);

    // Inner bright creamy signboard
    GameObject signBoard = Box(stand.transform, "SignBoard", new Vector3(0f, 0.5f, -0.04f), new Vector3(1.12f, 0.88f, 0.10f), Cream);
    Ignore(signBoard);

    // Large high-contrast 3D Letter text
    GameObject textGo = new GameObject("ChoiceText");
    textGo.transform.SetParent(stand.transform, false);
    textGo.transform.localPosition = new Vector3(0f, 0.5f, -0.12f);
    TextMesh tm = textGo.AddComponent<TextMesh>();
    tm.text = letter;
    tm.fontSize = 52;
    tm.characterSize = 0.095f;
    tm.fontStyle = FontStyle.Bold;
    tm.anchor = TextAnchor.MiddleCenter;
    tm.alignment = TextAlignment.Center;
    tm.color = new Color(0.18f, 0.10f, 0.32f);

    // Large generous click collider so 4yo children easily hit it on mobile or PC
    BoxCollider box = root.AddComponent<BoxCollider>();
    box.center = new Vector3(0f, 0.5f, 0f);
    box.size = new Vector3(1.35f, 1.15f, 1.25f);
    Ignore(root);

    SentenceLetterChoice choice = root.AddComponent<SentenceLetterChoice>();
    choice.Letter = letter;
    choice.IsCorrect = isCorrect;
    LetterChoices.Add(choice);
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
    GameObject go = new GameObject("SEFillLight");
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
