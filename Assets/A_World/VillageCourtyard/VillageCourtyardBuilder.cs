// A_World/VillageCourtyard/VillageCourtyardBuilder.cs — 3D Scene Builder for "Sân Đình Vui Vẻ".
// Builds Sân Đình Làng Quê: sân gạch, mái đình cổ kính, đèn lồng phát sáng theo RMS,
// cánh diều làng quê, hộp quà mây mở nắp, pháo hoa sân đình, và NPC Cô Tấm / Chú Cuội.
// Presentation only. C# 9.0 only. Pure UnityEngine.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class VillageCourtyardBuilder : MonoBehaviour {
  public const string SceneName = "VillageCourtyardPlayScene";
  public static readonly Vector3 WorldOffset = new Vector3(880f, 0f, 0f);
  public const float BoundX = 14f;
  public const float BoundZ = 14f;
  public static readonly Vector3 EntryLocal = new Vector3(0f, 0f, -4f);
  public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -11.5f);
  public static readonly Vector3 FollowOffset = new Vector3(0f, 2.8f, -4.6f);
  public static readonly Vector3 PlaySpotLocal = new Vector3(0f, 0f, 0.5f);

  public static readonly Vector3 CourtyardCenter = new Vector3(0f, 0f, 3.5f);
  public static readonly Vector3 NpcTamLocal = new Vector3(-2.2f, 0f, 4.2f);
  public static readonly Vector3 NpcCuoiLocal = new Vector3(2.2f, 0f, 4.2f);
  public static readonly Vector3 LanternLocal = new Vector3(-1.6f, 2.4f, 2.8f);
  public static readonly Vector3 KiteLocal = new Vector3(2.5f, 3.2f, 3.0f);
  public static readonly Vector3 GiftBoxLocal = new Vector3(0f, 0.45f, 3.2f);

  // Colors of traditional Vietnamese courtyard
  static readonly Color BrickRed = new Color(0.78f, 0.36f, 0.22f); // Gạch Bát Tràng
  static readonly Color WoodDark = new Color(0.42f, 0.28f, 0.16f); // Gỗ lim mái đình
  static readonly Color TileRoof = new Color(0.85f, 0.42f, 0.26f); // Ngói vảy cá
  static readonly Color LanternGold = new Color(0.98f, 0.82f, 0.25f);
  static readonly Color BambooGreen = new Color(0.62f, 0.74f, 0.32f);
  static readonly Color WickerCream = new Color(0.96f, 0.90f, 0.78f); // Hộp quà mây

  public ActivityAnchors Anchors { get; private set; }
  public Transform EntryPoint { get; private set; }
  public MicroWorldPortal ExitPortal { get; private set; }
  public Transform PlaySpot { get; private set; }

  // Interactive and reactive props
  public Transform LanternRoot { get; private set; }
  public Light LanternLight { get; private set; }
  public Transform KiteRoot { get; private set; }
  public GameObject GiftBoxRoot { get; private set; }
  public Transform GiftBoxLid { get; private set; }
  public SpriteRenderer GiftPhotoRenderer { get; private set; }
  public Transform GiftPictogramRoot { get; private set; }
  public GameObject FireworksRoot { get; private set; }

  public GameObject NpcTam { get; private set; }
  public GameObject NpcCuoi { get; private set; }
  CharacterPresentation _tamPresentation;
  CharacterPresentation _cuoiPresentation;
  Animator _tamAnimator;
  Animator _cuoiAnimator;

  public void Build() {
    BuildContent(transform);
    BuildNavMesh(transform);
  }

  void BuildNavMesh(Transform parent) {
    var surface = parent.gameObject.GetComponent<Unity.AI.Navigation.NavMeshSurface>();
    if (surface == null) surface = parent.gameObject.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
    surface.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
    surface.BuildNavMesh();
  }

  public void BuildContent(Transform root) {
    BuildGroundAndPavilion(root);
    BuildEntryAndExit(root);
    BuildReactiveLanternAndKite(root);
    BuildGiftBox(root);
    BuildNpcTamAndCuoi(root);
    BuildFireworksFx(root);
    BuildAnchors(root);

    GameObject entry = new GameObject("EntryPoint");
    entry.transform.SetParent(root, false);
    entry.transform.localPosition = EntryLocal;
    EntryPoint = entry.transform;
  }

  void BuildGroundAndPavilion(Transform parent) {
    // Sân cỏ làng quê
    GameObject lawn = GameObject.CreatePrimitive(PrimitiveType.Plane);
    lawn.name = "CourtyardLawn";
    lawn.transform.SetParent(parent, false);
    lawn.transform.localScale = new Vector3(3.2f, 1f, 3.2f);
    lawn.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.40f, 0.68f, 0.38f));

    // Sân gạch Bát Tràng trung tâm
    Box(parent, "CourtyardPlaza", CourtyardCenter + new Vector3(0f, 0.01f, 0f), new Vector3(11f, 0.02f, 9f), BrickRed);

    // Mái đình làng cổ kính phía sau
    Vector3 roofPos = new Vector3(0f, 3.8f, 7.2f);
    Box(parent, "PavilionBeam", roofPos, new Vector3(12f, 0.4f, 0.6f), WoodDark);
    Box(parent, "PavilionRoof", roofPos + new Vector3(0f, 0.8f, 0.5f), new Vector3(13f, 0.3f, 3.5f), TileRoof);

    // Cột đình gỗ lim
    Box(parent, "ColumnL", new Vector3(-4.8f, 1.8f, 7.0f), new Vector3(0.45f, 3.6f, 0.45f), WoodDark);
    Box(parent, "ColumnR", new Vector3(4.8f, 1.8f, 7.0f), new Vector3(0.45f, 3.6f, 0.45f), WoodDark);

    // Ánh sáng ấm áp sân đình
    GameObject lightGo = new GameObject("CourtyardSunLight");
    lightGo.transform.SetParent(parent, false);
    lightGo.transform.localPosition = new Vector3(0f, 8f, 2f);
    Light sun = lightGo.AddComponent<Light>();
    sun.type = LightType.Directional;
    sun.intensity = 1.2f;
    sun.color = new Color(1f, 0.95f, 0.85f);
    sun.transform.localRotation = Quaternion.Euler(45f, -30f, 0f);
  }

  void BuildEntryAndExit(Transform parent) {
    // Cổng làng tre
    Box(parent, "GatePostL", new Vector3(-1.8f, 1.1f, -10f), new Vector3(0.24f, 2.2f, 0.24f), BambooGreen);
    Box(parent, "GatePostR", new Vector3(1.8f, 1.1f, -10f), new Vector3(0.24f, 2.2f, 0.24f), BambooGreen);
    GameObject beam = Box(parent, "GateBeam", new Vector3(0f, 2.2f, -10f), new Vector3(3.8f, 0.2f, 0.2f), BambooGreen);
    Ignore(beam);

    Pad(parent, "PlayDisc", PlaySpotLocal, 3.0f, LanternGold);
    GameObject spot = new GameObject("PlaySpot");
    spot.transform.SetParent(parent, false);
    spot.transform.localPosition = PlaySpotLocal;
    PlaySpot = spot.transform;

    Pad(parent, "ExitDisc", ExitLocal + new Vector3(0f, 0.01f, 0.4f), 3.0f, new Color(0.7f, 0.9f, 0.7f));
    GameObject exitGo = new GameObject("ExitPortal");
    exitGo.transform.SetParent(parent, false);
    exitGo.transform.localPosition = ExitLocal;
    var exit = exitGo.AddComponent<MicroWorldPortal>();
    exit.ExitMode = true;
    exit.PlayExit = true;
    exit.fireRadius = 1.35f;
    ExitPortal = exit;
  }

  void BuildReactiveLanternAndKite(Transform parent) {
    // 1. Đèn lồng sân đình phản ứng theo âm lượng RMS
    GameObject lanternGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    lanternGo.name = "CourtyardLantern";
    lanternGo.transform.SetParent(parent, false);
    lanternGo.transform.localPosition = LanternLocal;
    lanternGo.transform.localScale = Vector3.one * 0.7f;
    lanternGo.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.98f, 0.35f, 0.22f), 0.5f);
    Strip(lanternGo);
    Ignore(lanternGo);
    LanternRoot = lanternGo.transform;

    GameObject lightGo = new GameObject("LanternGlow");
    lightGo.transform.SetParent(lanternGo.transform, false);
    Light pLight = lightGo.AddComponent<Light>();
    pLight.type = LightType.Point;
    pLight.range = 8f;
    pLight.intensity = 2.5f;
    pLight.color = LanternGold;
    LanternLight = pLight;

    // 2. Cánh diều làng quê bay lượn theo giọng nói
    GameObject kiteGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
    kiteGo.name = "VillageKite";
    kiteGo.transform.SetParent(parent, false);
    kiteGo.transform.localPosition = KiteLocal;
    kiteGo.transform.localScale = new Vector3(0.9f, 0.9f, 0.04f);
    kiteGo.transform.localRotation = Quaternion.Euler(20f, 15f, 45f);
    kiteGo.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.25f, 0.7f, 0.9f));
    Strip(kiteGo);
    Ignore(kiteGo);
    KiteRoot = kiteGo.transform;
  }

  void BuildGiftBox(Transform parent) {
    GameObject boxGo = new GameObject("CloudGiftBoxRoot");
    boxGo.transform.SetParent(parent, false);
    boxGo.transform.localPosition = GiftBoxLocal;
    GiftBoxRoot = boxGo;

    // Hộp mây đan (wicker box base)
    GameObject baseCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
    baseCube.name = "BoxBase";
    baseCube.transform.SetParent(boxGo.transform, false);
    baseCube.transform.localPosition = Vector3.zero;
    baseCube.transform.localScale = new Vector3(1.6f, 0.8f, 1.4f);
    baseCube.GetComponent<Renderer>().sharedMaterial = Lit(WickerCream);
    Strip(baseCube);

    // Nắp hộp quà mây (mở nắp khi pass)
    GameObject lidGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
    lidGo.name = "BoxLid";
    lidGo.transform.SetParent(boxGo.transform, false);
    lidGo.transform.localPosition = new Vector3(0f, 0.45f, 0f);
    lidGo.transform.localScale = new Vector3(1.7f, 0.16f, 1.5f);
    lidGo.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.98f, 0.82f, 0.35f));
    Strip(lidGo);
    GiftBoxLid = lidGo.transform;

    // Khung tranh hiển thị ảnh thật khi mở quà
    GameObject photoGo = new GameObject("GiftPhotoDisplay");
    photoGo.transform.SetParent(boxGo.transform, false);
    photoGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);
    photoGo.transform.localScale = Vector3.one * 1.1f;
    SpriteRenderer sr = photoGo.AddComponent<SpriteRenderer>();
    sr.enabled = false;
    GiftPhotoRenderer = sr;

    GameObject pictoGo = new GameObject("GiftPictogramRoot");
    pictoGo.transform.SetParent(boxGo.transform, false);
    pictoGo.transform.localPosition = new Vector3(0f, 0.95f, 0f);
    GiftPictogramRoot = pictoGo.transform;
  }

  void BuildNpcTamAndCuoi(Transform parent) {
    // 1. Cô Tấm (bên trái)
    NpcTam = new GameObject("NpcCoTam");
    NpcTam.transform.SetParent(parent, false);
    NpcTam.transform.localPosition = NpcTamLocal;
    NpcTam.transform.localRotation = Quaternion.Euler(0f, 150f, 0f);

    // 2. Chú Cuội (bên phải)
    NpcCuoi = new GameObject("NpcChuCuoi");
    NpcCuoi.transform.SetParent(parent, false);
    NpcCuoi.transform.localPosition = NpcCuoiLocal;
    NpcCuoi.transform.localRotation = Quaternion.Euler(0f, 210f, 0f);

    GameObject miaPrefab = Resources.Load<GameObject>("NpcVisuals/MiaVisual");
    if (miaPrefab != null) {
      GameObject vTam = Instantiate(miaPrefab, NpcTam.transform, false);
      vTam.name = "TamVisual";
      vTam.transform.localScale = Vector3.one * 0.48f;
      _tamAnimator = vTam.GetComponentInChildren<Animator>(true);
      _tamPresentation = vTam.AddComponent<CharacterPresentation>();

      GameObject vCuoi = Instantiate(miaPrefab, NpcCuoi.transform, false);
      vCuoi.name = "CuoiVisual";
      vCuoi.transform.localScale = Vector3.one * 0.48f;
      _cuoiAnimator = vCuoi.GetComponentInChildren<Animator>(true);
      _cuoiPresentation = vCuoi.AddComponent<CharacterPresentation>();
    }
  }

  void BuildFireworksFx(Transform parent) {
    GameObject fw = new GameObject("CourtyardFireworks");
    fw.transform.SetParent(parent, false);
    fw.transform.localPosition = CourtyardCenter + new Vector3(0f, 3.5f, 0f);
    FireworksRoot = fw;
  }

  public void UpdateRMSVisuals(float rms) {
    float norm = Mathf.Clamp01(rms * 12f);
    if (LanternRoot != null) {
      LanternRoot.localScale = Vector3.one * (0.7f + norm * 0.35f);
    }
    if (LanternLight != null) {
      LanternLight.intensity = 2.5f + norm * 4.5f;
    }
    if (KiteRoot != null) {
      KiteRoot.localPosition = KiteLocal + new Vector3(0f, norm * 0.8f, 0f);
      KiteRoot.localRotation = Quaternion.Euler(20f + norm * 15f, 15f, 45f + Mathf.Sin(Time.time * 4f) * 10f);
    }
  }

  public void OpenGiftBox(Sprite realPhoto, string wordId) {
    if (GiftBoxLid != null) {
      GiftBoxLid.localPosition = new Vector3(0f, 0.85f, 0.45f);
      GiftBoxLid.localRotation = Quaternion.Euler(-40f, 0f, 0f);
      GameJuice.Pop(GiftBoxLid, 0.2f, 0.3f);
    }

    // Display Real Photo if available, or build 3D Pictogram
    if (realPhoto != null && GiftPhotoRenderer != null) {
      GiftPhotoRenderer.sprite = realPhoto;
      GiftPhotoRenderer.enabled = true;
      GameJuice.Pop(GiftPhotoRenderer.transform, 0.25f, 0.4f);
    } else if (GiftPictogramRoot != null) {
      ClearGiftPictogram();
      PhonicsPictograms.Build(GiftPictogramRoot, wordId);
      GameJuice.Pop(GiftPictogramRoot, 0.25f, 0.4f);
    }
  }

  public void CloseGiftBox() {
    if (GiftBoxLid != null) {
      GiftBoxLid.localPosition = new Vector3(0f, 0.45f, 0f);
      GiftBoxLid.localRotation = Quaternion.identity;
    }
    if (GiftPhotoRenderer != null) {
      GiftPhotoRenderer.sprite = null;
      GiftPhotoRenderer.enabled = false;
    }
    ClearGiftPictogram();
  }

  void ClearGiftPictogram() {
    if (GiftPictogramRoot == null) return;
    for (int i = GiftPictogramRoot.childCount - 1; i >= 0; i--) {
      DestroyImmediate(GiftPictogramRoot.GetChild(i).gameObject);
    }
  }

  public void TriggerFireworks() {
    if (FireworksRoot != null) {
      GameJuice.Pop(FireworksRoot.transform, 0.4f, 0.8f);
    }
  }

  public void NpcCelebrateAll() {
    if (_tamAnimator != null) _tamAnimator.SetTrigger("Celebrate");
    if (_cuoiAnimator != null) _cuoiAnimator.SetTrigger("Celebrate");
    if (_tamPresentation != null) _tamPresentation.PulseExpression(CharacterExpression.Happy, 2.5f);
    if (_cuoiPresentation != null) _cuoiPresentation.PulseExpression(CharacterExpression.Happy, 2.5f);
  }

  public void TamDemonstrateMouth(float speedMultiplier = 0.8f) {
    if (_tamPresentation != null) {
      _tamPresentation.PulseExpression(CharacterExpression.Curious, 2.0f);
    }
    if (_tamAnimator != null) {
      _tamAnimator.speed = speedMultiplier;
    }
  }

  public void CuoiEncourage() {
    if (_cuoiPresentation != null) {
      _cuoiPresentation.PulseExpression(CharacterExpression.Happy, 1.8f);
    }
  }

  void BuildAnchors(Transform parent) {
    ActivityAnchors a = ActivityAnchors.Ensure(parent, "VillageCourtyardAnchors");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", EntryLocal);
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", CourtyardCenter);
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 4.2f, -5.5f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 1.4f, 3.2f));
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

  static void Strip(GameObject go) {
    try {
      var c = go.GetComponent<Collider>();
      if (c != null) DestroyImmediate(c);
    } catch (Exception) { }
  }

  static void Ignore(GameObject go) {
    if (go == null) return;
    try {
      var mod = go.GetComponent<Unity.AI.Navigation.NavMeshModifier>();
      if (mod == null) mod = go.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (Exception) { }
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
