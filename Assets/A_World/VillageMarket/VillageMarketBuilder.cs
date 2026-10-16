// Assets/A_World/VillageMarket/VillageMarketBuilder.cs — 3D Visual Environment for "Chợ Quê Nhộn Nhịp".
// Constructs:
//   1. Chợ Quê under bamboo shade: Bamboo stalls (sạp tre), woven bamboo trays (mâm tre), conical hats (nón lá).
//   2. Bà Ngoại's bamboo carrying pole and twin woven baskets (quang gánh mây tre, with BasketDropTarget).
//   3. NPC Bà Ngoại (áo bà ba, nón lá, gentle elder expression).
//   4. NPC Cậu Vàng (swift golden pup that assists, wags tail, and runs toward correct targets).
// Pure C# 9.0 only. EditMode-safe.
using System;
using UnityEngine;

public class VillageMarketBuilder : MonoBehaviour {
  public const string SceneName = "VillageMarketPlayScene";
  public static readonly Vector3 WorldOffset = new Vector3(0, 0, 0);
  public static readonly Vector3 EntryLocal = new Vector3(0, 0, -3.5f);
  public const float BoundX = 14f;
  public const float BoundZ = 12f;
  public static readonly Vector3 FollowOffset = new Vector3(0, 4.5f, -7.5f);

  public ActivityAnchors Anchors;
  public MicroWorldPortal ExitPortal;
  public Transform BasketDropTarget;

  // Prompt sign (1.9.49): shows the target name as TEXT while no Vietnamese voice pack exists.
  public TextMesh PromptSign;
  GameObject _promptSignRoot;
  public AudioSource VoiceSource;
  public AudioSource SfxSource;

  // Visual Nodes
  public Transform GrandmaTransform;
  public Transform DogTransform;
  public Transform BambooStallTransform;
  public Transform LeftBasketTransform;
  public Transform RightBasketTransform;

  // Animation states
  Vector3 _dogTargetPos;
  Vector3 _dogHomePos = new Vector3(1.8f, 0, -1.2f);
  float _tailWagT = 0f;
  float _grandmaGestureT = 0f;

  public void Build() {
    BuildContent(transform);
  }

  public void BuildContent(Transform root) {
    if (root == null) root = transform;

    // 1. Ground & Bamboo Canopy
    BuildGroundAndAwning(root);

    // 2. Bamboo Stall (Sạp Tre)
    BuildBambooStall(root);

    // 3. Carrying Pole & Baskets (Quang Gánh)
    BuildQuangGanh(root);

    // 4. NPC Bà Ngoại
    BuildGrandmaNpc(root);

    // 5. NPC Cậu Vàng (Golden Pup)
    BuildDogNpc(root);

    // 6. Rural Details (Nón lá, Khóm tre, Lồng chim)
    BuildVillageAccents(root);

    // 7. Activity Anchors & Exit Portal
    BuildAnchorsAndPortal(root);

    // 8. Prompt sign + playback sources (honest text fallback until a Vietnamese voice pack lands)
    BuildPromptSign(root);
    VoiceSource = root.gameObject.AddComponent<AudioSource>();
    VoiceSource.playOnAwake = false;
    VoiceSource.spatialBlend = 0f;
    SfxSource = root.gameObject.AddComponent<AudioSource>();
    SfxSource.playOnAwake = false;
    SfxSource.spatialBlend = 0f;
  }

  void BuildPromptSign(Transform root) {
    var sign = new GameObject("PromptSign");
    sign.transform.SetParent(root, false);
    sign.transform.localPosition = new Vector3(0f, 2.35f, 0.45f);
    _promptSignRoot = sign;

    var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
    board.name = "PromptBoard";
    board.transform.SetParent(sign.transform, false);
    board.transform.localScale = new Vector3(3.6f, 0.8f, 0.08f);
    var bmr = board.GetComponent<MeshRenderer>();
    if (bmr != null) {
      SetColor(bmr, new Color(0.99f, 0.95f, 0.85f));
    }
    var bc = board.GetComponent<Collider>();
    if (bc != null) { try { CharacterPresentation.DestroyNow(bc); } catch (Exception) { } }

    var textGo = new GameObject("PromptText");
    textGo.transform.SetParent(sign.transform, false);
    textGo.transform.localPosition = new Vector3(0f, 0f, -0.08f);
    textGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // face the child/camera
    PromptSign = textGo.AddComponent<TextMesh>();
    PromptSign.fontSize = 40;
    PromptSign.characterSize = 0.075f;
    PromptSign.anchor = TextAnchor.MiddleCenter;
    PromptSign.alignment = TextAlignment.Center;
    PromptSign.color = new Color(0.45f, 0.26f, 0.10f);
    PromptSign.text = "";

    sign.SetActive(false);
  }

  public void ShowPromptText(string lineVi) {
    if (_promptSignRoot == null) return;
    if (PromptSign != null) PromptSign.text = lineVi;
    _promptSignRoot.SetActive(true);
  }

  public void HidePromptText() {
    if (_promptSignRoot != null) _promptSignRoot.SetActive(false);
  }

  // EditMode-safe material tinting: renderer.material (getter) logs a leak ERROR in EditMode
  // and Unity's test framework fails on it — always build a fresh material and assign sharedMaterial.
  static void SetColor(Renderer r, Color c) {
    if (r == null) return;
    Material m = new Material(Shader.Find("Standard") ?? Shader.Find("Diffuse"));
    m.color = c;
    r.sharedMaterial = m;
  }

  void BuildGroundAndAwning(Transform root) {
    var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "VillageGround";
    ground.transform.SetParent(root, false);
    ground.transform.localScale = new Vector3(1.6f, 1f, 1.4f);
    ground.transform.localPosition = Vector3.zero;

    var mr = ground.GetComponent<MeshRenderer>();
    if (mr != null) {
      SetColor(mr, new Color(0.82f, 0.76f, 0.62f)); // Earthy village soil
    }

    // Bamboo awning roof (Mái hiên tre mộc mạc)
    var awning = GameObject.CreatePrimitive(PrimitiveType.Cube);
    awning.name = "BambooAwning";
    awning.transform.SetParent(root, false);
    awning.transform.localPosition = new Vector3(0, 3.8f, 1.2f);
    awning.transform.localScale = new Vector3(6.5f, 0.15f, 3.8f);
    awning.transform.localRotation = Quaternion.Euler(12f, 0, 0);

    var awningMr = awning.GetComponent<MeshRenderer>();
    if (awningMr != null) {
      SetColor(awningMr, new Color(0.55f, 0.48f, 0.32f)); // Dried bamboo thatch
    }
  }

  void BuildBambooStall(Transform root) {
    var stall = new GameObject("BambooStall");
    stall.transform.SetParent(root, false);
    stall.transform.localPosition = new Vector3(0, 0.8f, 0.5f);
    BambooStallTransform = stall.transform;

    // Table top
    var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
    table.name = "TableTop";
    table.transform.SetParent(stall.transform, false);
    table.transform.localScale = new Vector3(5.2f, 0.18f, 1.8f);
    var mr = table.GetComponent<MeshRenderer>();
    if (mr != null) {
      SetColor(mr, new Color(0.78f, 0.66f, 0.44f)); // Golden woven bamboo
    }

    // 4 Bamboo Legs
    float[] legX = { -2.3f, 2.3f };
    float[] legZ = { -0.7f, 0.7f };
    for (int x = 0; x < 2; x++) {
      for (int z = 0; z < 2; z++) {
        var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        leg.name = $"BambooLeg_{x}_{z}";
        leg.transform.SetParent(stall.transform, false);
        leg.transform.localPosition = new Vector3(legX[x], -0.4f, legZ[z]);
        leg.transform.localScale = new Vector3(0.14f, 0.4f, 0.14f);
        var legMr = leg.GetComponent<MeshRenderer>();
        SetColor(legMr, new Color(0.58f, 0.48f, 0.30f));
      }
    }
  }

  void BuildQuangGanh(Transform root) {
    var quangGanh = new GameObject("QuangGanh");
    quangGanh.transform.SetParent(root, false);
    quangGanh.transform.localPosition = new Vector3(2.4f, 0, 0.8f);

    // Bamboo carrying pole (Đòn gánh uốn cong)
    var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pole.name = "DonGanh";
    pole.transform.SetParent(quangGanh.transform, false);
    pole.transform.localPosition = new Vector3(0, 1.1f, 0);
    pole.transform.localRotation = Quaternion.Euler(0, 0, 85f);
    pole.transform.localScale = new Vector3(0.06f, 1.1f, 0.06f);
    var poleMr = pole.GetComponent<MeshRenderer>();
    SetColor(poleMr, new Color(0.72f, 0.60f, 0.38f));

    // Front Basket (Sọt trước - Basket Drop Target)
    var frontBasket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    frontBasket.name = "FrontBasket_DropTarget";
    frontBasket.transform.SetParent(quangGanh.transform, false);
    frontBasket.transform.localPosition = new Vector3(-0.9f, 0.35f, 0);
    frontBasket.transform.localScale = new Vector3(1.1f, 0.35f, 1.1f);
    var fMr = frontBasket.GetComponent<MeshRenderer>();
    SetColor(fMr, new Color(0.85f, 0.74f, 0.52f));
    BasketDropTarget = frontBasket.transform;
    LeftBasketTransform = frontBasket.transform;

    // Rear Basket (Sọt sau)
    var rearBasket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rearBasket.name = "RearBasket";
    rearBasket.transform.SetParent(quangGanh.transform, false);
    rearBasket.transform.localPosition = new Vector3(0.9f, 0.35f, 0);
    rearBasket.transform.localScale = new Vector3(1.1f, 0.35f, 1.1f);
    var rMr = rearBasket.GetComponent<MeshRenderer>();
    SetColor(rMr, new Color(0.85f, 0.74f, 0.52f));
    RightBasketTransform = rearBasket.transform;
  }

  void BuildGrandmaNpc(Transform root) {
    var grandma = new GameObject("Npc_Grandma");
    grandma.transform.SetParent(root, false);
    grandma.transform.localPosition = new Vector3(-2.6f, 0, 0.8f);
    GrandmaTransform = grandma.transform;

    // Body (Áo bà ba nâu mộc mạc)
    var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
    body.name = "Body";
    body.transform.SetParent(grandma.transform, false);
    body.transform.localPosition = new Vector3(0, 0.9f, 0);
    body.transform.localScale = new Vector3(0.65f, 0.85f, 0.55f);
    var bodyMr = body.GetComponent<MeshRenderer>();
    SetColor(bodyMr, new Color(0.42f, 0.28f, 0.18f)); // Brown silk bà ba

    // Head
    var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    head.name = "Head";
    head.transform.SetParent(grandma.transform, false);
    head.transform.localPosition = new Vector3(0, 1.75f, 0);
    head.transform.localScale = new Vector3(0.48f, 0.48f, 0.48f);
    var headMr = head.GetComponent<MeshRenderer>();
    SetColor(headMr, new Color(0.96f, 0.82f, 0.72f));

    // Conical Hat (Nón lá)
    var hat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    hat.name = "NonLa";
    hat.transform.SetParent(grandma.transform, false);
    hat.transform.localPosition = new Vector3(0, 2.05f, 0);
    hat.transform.localScale = new Vector3(0.95f, 0.08f, 0.95f);
    hat.transform.localRotation = Quaternion.Euler(-8f, 0, 0);
    var hatMr = hat.GetComponent<MeshRenderer>();
    SetColor(hatMr, new Color(0.92f, 0.86f, 0.68f));
  }

  void BuildDogNpc(Transform root) {
    var dog = new GameObject("Npc_CauVang");
    dog.transform.SetParent(root, false);
    dog.transform.localPosition = _dogHomePos;
    DogTransform = dog.transform;
    _dogTargetPos = _dogHomePos;

    // Body (Chú chó vàng)
    var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
    body.name = "DogBody";
    body.transform.SetParent(dog.transform, false);
    body.transform.localPosition = new Vector3(0, 0.35f, 0);
    body.transform.localRotation = Quaternion.Euler(90f, 0, 0);
    body.transform.localScale = new Vector3(0.38f, 0.45f, 0.35f);
    var bMr = body.GetComponent<MeshRenderer>();
    SetColor(bMr, new Color(0.92f, 0.65f, 0.22f)); // Golden fur

    // Head
    var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    head.name = "DogHead";
    head.transform.SetParent(dog.transform, false);
    head.transform.localPosition = new Vector3(0, 0.6f, 0.35f);
    head.transform.localScale = new Vector3(0.32f, 0.32f, 0.32f);
    var hMr = head.GetComponent<MeshRenderer>();
    SetColor(hMr, new Color(0.94f, 0.68f, 0.24f));

    // Tail (Đuôi ngoe nguẩy)
    var tail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    tail.name = "DogTail";
    tail.transform.SetParent(dog.transform, false);
    tail.transform.localPosition = new Vector3(0, 0.5f, -0.4f);
    tail.transform.localRotation = Quaternion.Euler(-45f, 0, 0);
    tail.transform.localScale = new Vector3(0.06f, 0.18f, 0.06f);
    var tMr = tail.GetComponent<MeshRenderer>();
    SetColor(tMr, new Color(0.88f, 0.60f, 0.18f));
  }

  void BuildVillageAccents(Transform root) {
    // Bamboo grove behind (Khóm tre xanh)
    for (int i = 0; i < 4; i++) {
      var bamboo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      bamboo.name = "BambooClump_" + i;
      bamboo.transform.SetParent(root, false);
      bamboo.transform.localPosition = new Vector3(-4.5f + i * 2.8f, 2.5f, 3.8f);
      bamboo.transform.localScale = new Vector3(0.18f, 2.5f, 0.18f);
      var bMr = bamboo.GetComponent<MeshRenderer>();
      SetColor(bMr, new Color(0.32f, 0.58f, 0.22f));
    }
  }

  void BuildAnchorsAndPortal(Transform root) {
    var anchorsGo = new GameObject("ActivityAnchors");
    anchorsGo.transform.SetParent(root, false);
    Anchors = anchorsGo.AddComponent<ActivityAnchors>();

    var portalGo = new GameObject("VMExitPortal");
    portalGo.transform.SetParent(root, false);
    portalGo.transform.localPosition = new Vector3(0, 0, -4.8f);
    ExitPortal = portalGo.AddComponent<MicroWorldPortal>();
    ExitPortal.ExitMode = true;
    ExitPortal.PlayExit = true;
  }

  // Scaffolding & Animation Hooks
  public void DogAssist(Vector3 targetPos) {
    // Dog runs to front of correct item
    _dogTargetPos = new Vector3(targetPos.x * 0.8f, 0f, -0.2f);
  }

  public void GrandmaHandOff() {
    _grandmaGestureT = 1.0f;
  }

  public void CelebrateAll() {
    _grandmaGestureT = 1.5f;
    _dogTargetPos = _dogHomePos;
  }

  void Update() {
    float dt = Time.deltaTime;
    if (DogTransform != null) {
      DogTransform.localPosition = Vector3.MoveTowards(DogTransform.localPosition, _dogTargetPos, 4.0f * dt);
      _tailWagT += dt * 12f;
      var tail = DogTransform.Find("DogTail");
      if (tail != null) {
        tail.localRotation = Quaternion.Euler(-45f, Mathf.Sin(_tailWagT) * 25f, 0);
      }
    }
  }
}
