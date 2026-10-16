// Assets/A_World/VillageMarket/MarketItemUI.cs — Visual & Interaction Component for market stall items.
// Handles:
//   1. Real Photography rendering (with clean pictogram fallback if asset not yet registered).
//   2. Hidden text label (revealed at completion or Fail 3 Guided Discovery).
//   3. Tap and Drag-to-Basket interactions.
//   4. Scaffolding visual states: Gentle shake (Fail 1), Visual narrowing fade-out (Fail 2), Gold frame highlight & scale (Fail 3).
// Pure C# 9.0 only.
using System;
using UnityEngine;
using UnityEngine.UI;

public class MarketItemUI : MonoBehaviour, IClickTarget {
  public MarketItemOption Option { get; private set; }
  public string ItemId => Option != null ? Option.itemId : "";
  public string DisplayName => Option != null ? Option.displayName : "";

  public bool IsActive { get; private set; } = true;
  public bool IsNarrowedOut { get; private set; } = false;
  public bool IsPromoted { get; private set; } = false;
  public bool IsInBasket { get; private set; } = false;
  public bool IsLabelVisible { get; private set; } = false;
  public float NormalizedX { get; set; } = 0.5f; // [0..1] screen horizontal position

  // Visual transforms
  public Transform VisualRoot;
  public SpriteRenderer PhotoRenderer;
  public TextMesh LabelText;
  public GameObject GoldFrame;
  public GameObject NarrowScrim;

  public Action<MarketItemUI> OnTapped;
  public Action<MarketItemUI> OnDraggedToBasket;

  Vector3 _homePosition;
  Vector3 _targetScale = Vector3.one;
  float _shakeTimer = 0f;

  public void Initialize(MarketItemOption option, Vector3 localPos, float normX, IRealPhotoAssetLoader loader = null) {
    Option = option;
    transform.localPosition = localPos;
    _homePosition = localPos;
    NormalizedX = normX;
    IsActive = true;
    IsNarrowedOut = false;
    IsPromoted = false;
    IsInBasket = false;
    IsLabelVisible = false;
    _targetScale = Vector3.one;

    BuildVisualsIfNeeded(loader);
    SetLabelVisible(false);
  }

  void BuildVisualsIfNeeded(IRealPhotoAssetLoader loader) {
    if (VisualRoot == null) {
      var rootGo = new GameObject("VisualRoot");
      rootGo.transform.SetParent(transform, false);
      VisualRoot = rootGo.transform;
    }

    if (PhotoRenderer == null) {
      var photoGo = new GameObject("Photo");
      photoGo.transform.SetParent(VisualRoot, false);
      PhotoRenderer = photoGo.AddComponent<SpriteRenderer>();
    }

    // Load registered photo or fallback to clean pictogram
    Sprite targetSprite = null;
    if (Option != null && Option.realPhotoRef != null && Option.realPhotoRef.EditorAsset != null) {
      targetSprite = Option.realPhotoRef.EditorAsset;
    }
    if (loader != null && !string.IsNullOrEmpty(ItemId)) {
      loader.LoadPhotoAsync(ItemId, sp => {
        if (sp != null && PhotoRenderer != null) PhotoRenderer.sprite = sp;
      });
    }

    if (targetSprite != null) {
      PhotoRenderer.sprite = targetSprite;
    } else {
      PhonicsPictograms.Build(VisualRoot, ItemId);
    }

    // Create label text (hidden by default)
    if (LabelText == null) {
      var labelGo = new GameObject("LabelText");
      labelGo.transform.SetParent(VisualRoot, false);
      labelGo.transform.localPosition = new Vector3(0, -0.65f, 0);
      LabelText = labelGo.AddComponent<TextMesh>();
      LabelText.text = DisplayName;
      LabelText.fontSize = 24;
      LabelText.characterSize = 0.08f;
      LabelText.alignment = TextAlignment.Center;
      LabelText.anchor = TextAnchor.MiddleCenter;
      LabelText.color = new Color(0.15f, 0.12f, 0.08f, 1f); // Rustic bamboo brown
    }

    // Gold frame for Tier-3 highlight
    if (GoldFrame == null) {
      GoldFrame = GameObject.CreatePrimitive(PrimitiveType.Quad);
      GoldFrame.name = "GoldHighlightFrame";
      GoldFrame.transform.SetParent(VisualRoot, false);
      GoldFrame.transform.localScale = new Vector3(1.4f, 1.4f, 1f);
      GoldFrame.transform.localPosition = new Vector3(0, 0, 0.02f);
      var mr = GoldFrame.GetComponent<MeshRenderer>();
      if (mr != null) {
        mr.sharedMaterial = Lit(new Color(1.0f, 0.85f, 0.2f, 0.45f));
      }
      GoldFrame.SetActive(false);
    }

    // Add BoxCollider for click/tap interaction
    var col = GetComponent<BoxCollider>();
    if (col == null) col = gameObject.AddComponent<BoxCollider>();
    col.size = new Vector3(1.2f, 1.2f, 0.2f);
  }

  public void SetLabelVisible(bool visible) {
    IsLabelVisible = visible;
    if (LabelText != null) {
      LabelText.gameObject.SetActive(visible);
      LabelText.text = DisplayName;
    }
  }

  // Tier 1 Scaffolding: Gentle shake without harsh error sounds
  public void GentleShake() {
    _shakeTimer = 0.45f;
  }

  // Tier 2 Scaffolding: Visual narrowing — cất bớt 1 đáp án sai
  public void DismissNarrowOut() {
    IsActive = false;
    IsNarrowedOut = true;
    _targetScale = Vector3.zero;
    if (PhotoRenderer != null) {
      PhotoRenderer.color = new Color(1f, 1f, 1f, 0.2f);
    }
  }

  // Tier 3 Scaffolding: Guided discovery — phóng to món đồ đúng, bật khung vàng, hiện chữ
  public void PromoteHighlight() {
    IsPromoted = true;
    _targetScale = new Vector3(1.35f, 1.35f, 1.35f);
    SetLabelVisible(true);
    if (GoldFrame != null) GoldFrame.SetActive(true);
  }

  public void PlaceInBasket(Vector3 basketWorldPos) {
    IsInBasket = true;
    IsActive = false;
    transform.position = basketWorldPos;
    _targetScale = new Vector3(0.75f, 0.75f, 0.75f);
    SetLabelVisible(true);
    if (GoldFrame != null) GoldFrame.SetActive(false);
  }

  // Testing & runtime tap/click trigger
  public void OnPointerClick() {
    if (!IsActive && !IsPromoted) return;
    OnTapped?.Invoke(this);
  }

  // ClickRouter entry (1.9.49): stall items must answer the real click system — the legacy
  // OnMouseDown below never fires under the project's New-only Input System.
  public void OnClicked() {
    OnPointerClick();
  }

  // Testing & runtime drag-to-basket trigger
  public void SimulateDragToBasket(Vector3 basketPos) {
    if (!IsActive && !IsPromoted) return;
    PlaceInBasket(basketPos);
    OnDraggedToBasket?.Invoke(this);
  }

  public void Tick(float dt) {
    if (VisualRoot == null) return;

    // Smooth scaling transition
    VisualRoot.localScale = Vector3.MoveTowards(VisualRoot.localScale, _targetScale, 3.5f * dt);

    // Shake animation
    if (_shakeTimer > 0f) {
      _shakeTimer -= dt;
      float offset = Mathf.Sin(_shakeTimer * 30f) * 0.08f;
      VisualRoot.localPosition = new Vector3(offset, 0, 0);
    } else if (!IsInBasket) {
      VisualRoot.localPosition = Vector3.zero;
    }
  }

  void OnMouseDown() {
    OnPointerClick();
  }

  static readonly System.Collections.Generic.Dictionary<string, Material> _mats = new System.Collections.Generic.Dictionary<string, Material>();
  static Material Lit(Color color) {
    string key = color.r.ToString("F2") + "," + color.g.ToString("F2") + "," + color.b.ToString("F2") + "," + color.a.ToString("F2");
    if (_mats.TryGetValue(key, out var cached) && cached != null) return cached;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color"));
    mat.color = color;
    _mats[key] = mat;
    return mat;
  }
}
