// A_World/CountingGarden/GardenZonePanel.cs — S3 P2X ZONE PICKER (user order §47B).
// The pink in-game card that appears when a Counting-Garden zone is focused:
// zone name + TWO big buttons — "Vào chơi" (Play, staged zones only) and
// "Quay lại" (Back, always). Style follows LanguageDialog (rounded cream card +
// pink border + blossom accents + GraphicRaycaster) and the panel is built on
// the PERSISTENT bootstrap object, so it survives the garden <-> play scene
// swap. It never covers the whole screen: clicks outside it stay world clicks
// (the double-click cancel lives in CountingGardenArea).
// C# 9.0 only.
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GardenZonePanel : MonoBehaviour {
  static readonly Color PanelFill = new Color(0.99f, 0.93f, 0.95f, 0.97f);
  static readonly Color PanelBorder = new Color(0.96f, 0.76f, 0.85f, 1f);
  static readonly Color BoxFill = new Color(1f, 0.97f, 0.975f, 1f);
  static readonly Color TitleInk = new Color(0.36f, 0.18f, 0.28f, 1f);
  static readonly Color SubInk = new Color(0.72f, 0.44f, 0.56f, 1f);
  static readonly Color PlayAccent = new Color(0.45f, 0.78f, 0.55f, 1f);
  static readonly Color BackAccent = new Color(0.93f, 0.45f, 0.55f, 1f);
  static readonly Color BlossomA = new Color(0.99f, 0.78f, 0.86f, 0.95f);
  static readonly Color BlossomB = new Color(0.97f, 0.66f, 0.78f, 0.9f);

  CountingGardenArea _area;
  GameObject _canvasGo;
  RectTransform _playFrame;
  Text _title;
  Text _hint;

  public bool HasArea { get { return _area != null; } }

  public bool IsOpen { get; private set; }
  public bool IsBuilt { get { return _canvasGo != null; } }
  public bool HasPlayButton { get { return _playFrame != null; } }
  public bool HasBackButton { get { return _backFrame != null; } }
  public bool PlayVisible { get { return _playFrame != null && _playFrame.gameObject.activeSelf; } }
  public string TitleText { get { return _title != null ? _title.text : ""; } }

  RectTransform _backFrame;

  public void Bind(CountingGardenArea area) {
    _area = area;
    try {
      Debug.Log("[GardenZonePanel] bound area=" + (area != null)
        + " id=" + (area != null ? area.GetHashCode() : 0), this);
    } catch (Exception) { }
  }

  // Self-heal (journey diagnosis): if the early composition binding was missed
  // or the area instance was replaced, resolve it on demand instead of going
  // silent under the child's finger.
  void EnsureArea() {
    if (_area != null) return;
    try {
      _area = FindAnyObjectByType<CountingGardenArea>();
      Debug.Log("[GardenZonePanel] self-healed area=" + (_area != null), this);
    } catch (Exception) { }
  }

  public void Build() {
    if (_canvasGo != null) return;
    BuildUi();
  }

  // zoneName = the focused zone (already localized by the caller),
  // playEnabled = this zone has staged play (skeleton plots show Back only).
  public void ShowFor(string zoneName, bool playEnabled) {
    if (_canvasGo == null) Build();
    if (_canvasGo == null) return;
    if (_title != null) _title.text = zoneName;
    if (_playFrame != null) _playFrame.gameObject.SetActive(playEnabled);
    if (_hint != null) {
      _hint.text = playEnabled
        ? DialogueLang.T("Let's play!", "Mình cùng chơi nhé!")
        : DialogueLang.T("Coming soon!", "Sắp mở rồi!");
    }
    _canvasGo.SetActive(true);
    IsOpen = true;
  }

  public void Hide() {
    if (_canvasGo != null) _canvasGo.SetActive(false);
    IsOpen = false;
  }

  void HandlePlay() {
    try {
      EnsureArea();
      Debug.Log("[GardenZonePanel] play pressed (area=" + (_area != null) + ").", this);
      if (_area != null) _area.EnterPlay();
    } catch (Exception) { }
  }

  void HandleBack() {
    try {
      EnsureArea();
      Debug.Log("[GardenZonePanel] back pressed.", this);
      if (_area != null) _area.CancelFocus();
    } catch (Exception) { }
  }

  // ---- UI ----------------------------------------------------------------------

  void BuildUi() {
    _canvasGo = new GameObject("GardenZoneCanvas");
    _canvasGo.transform.SetParent(transform, false);
    Canvas canvas = _canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 85; // above HUD (10) + tunnel (80), below language chooser (90)
    _canvasGo.AddComponent<GraphicRaycaster>();
    CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1920f, 1080f);
    scaler.matchWidthOrHeight = 0.5f;

    RectTransform panel = Rect(_canvasGo.transform, "ZonePanel",
      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
      new Vector2(780f, 300f));
    Image panelBg = panel.gameObject.AddComponent<Image>();
    panelBg.sprite = Rounded(64, 22, PanelFill);
    panelBg.type = Image.Type.Sliced;
    RectTransform border = Rect(_canvasGo.transform, "ZonePanelBorder",
      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
      new Vector2(804f, 324f));
    Image borderImg = border.gameObject.AddComponent<Image>();
    borderImg.sprite = Rounded(64, 26, PanelBorder);
    borderImg.type = Image.Type.Sliced;
    borderImg.raycastTarget = false;
    border.SetSiblingIndex(panel.GetSiblingIndex()); // border behind the card

    Blossom(panel, new Vector2(-352f, 112f), 54f, BlossomA);
    Blossom(panel, new Vector2(354f, 108f), 46f, BlossomB);

    _title = Label(panel, "ZoneTitle", "", 46f, TitleInk,
      new Vector2(0f, 100f), new Vector2(700f, 60f));

    // Two BIG boxes (4-6yo target size), side by side: play (green) + back (pink).
    _playFrame = ZoneBox(panel, "ZonePlay", new Vector2(-176f, -26f),
      DialogueLang.T("Play", "Vào chơi"), PlayAccent, HandlePlay);
    _backFrame = ZoneBox(panel, "ZoneBack", new Vector2(176f, -26f),
      DialogueLang.T("Back", "Quay lại"), BackAccent, HandleBack);

    _hint = Label(panel, "ZoneHint", "", 28f, SubInk,
      new Vector2(0f, -124f), new Vector2(700f, 44f));

    _canvasGo.SetActive(false);
  }

  RectTransform ZoneBox(RectTransform parent, string name, Vector2 pos, string label,
      Color accent, Action onClick) {
    RectTransform frame = Rect(parent, name + "Frame", new Vector2(0.5f, 0.5f),
      new Vector2(0.5f, 0.5f), pos, new Vector2(320f, 132f));
    Image frameImg = frame.gameObject.AddComponent<Image>();
    frameImg.sprite = Rounded(64, 20, accent);
    frameImg.type = Image.Type.Sliced;
    frameImg.raycastTarget = false;
    RectTransform box = Rect(frame, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    box.anchorMin = Vector2.zero;
    box.anchorMax = Vector2.one;
    box.offsetMin = new Vector2(7f, 7f);
    box.offsetMax = new Vector2(-7f, -7f);
    Image boxImg = box.gameObject.AddComponent<Image>();
    boxImg.sprite = Rounded(64, 16, BoxFill);
    boxImg.type = Image.Type.Sliced;
    Button btn = box.gameObject.AddComponent<Button>();
    btn.targetGraphic = boxImg;
    ColorBlock colors = btn.colors;
    colors.normalColor = Color.white;
    colors.highlightedColor = new Color(1f, 0.93f, 0.96f, 1f);
    colors.pressedColor = new Color(0.95f, 0.80f, 0.87f, 1f);
    colors.selectedColor = Color.white;
    colors.fadeDuration = 0.06f;
    btn.colors = colors;
    btn.onClick.AddListener(delegate { onClick(); });
    Label(box, name + "Label", label, 44f, TitleInk,
      new Vector2(0f, 0f), new Vector2(280f, 60f));
    return frame;
  }

  void Blossom(RectTransform parent, Vector2 pos, float size, Color color) {
    RectTransform rect = Rect(parent, "Blossom", new Vector2(0.5f, 0.5f),
      new Vector2(0.5f, 0.5f), pos, new Vector2(size, size));
    Image img = rect.gameObject.AddComponent<Image>();
    img.sprite = Rounded(64, 32, color); // circle
    img.raycastTarget = false;
  }

  static Text Label(RectTransform parent, string name, string text, float size, Color color,
      Vector2 pos, Vector2 rectSize) {
    RectTransform rect = Rect(parent, name, new Vector2(0.5f, 0.5f),
      new Vector2(0.5f, 0.5f), pos, rectSize);
    Text label = rect.gameObject.AddComponent<Text>();
    label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    label.fontSize = Mathf.RoundToInt(size);
    label.color = color;
    label.alignment = TextAnchor.MiddleCenter;
    label.horizontalOverflow = HorizontalWrapMode.Overflow;
    label.verticalOverflow = VerticalWrapMode.Overflow;
    label.text = text;
    label.raycastTarget = false;
    return label;
  }

  static RectTransform Rect(Transform parent, string name, Vector2 anchorMin,
      Vector2 anchorMax, Vector2 pos, Vector2 size) {
    GameObject go = new GameObject(name);
    go.transform.SetParent(parent, false);
    RectTransform rect = go.AddComponent<RectTransform>();
    rect.anchorMin = anchorMin;
    rect.anchorMax = anchorMax;
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = pos;
    rect.sizeDelta = size;
    return rect;
  }

  // Procedural rounded-rect sprite (same proven helper as MarketHUD/LanguageDialog).
  static Sprite Rounded(int size, int radius, Color color) {
    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    tex.wrapMode = TextureWrapMode.Clamp;
    Color clear = new Color(color.r, color.g, color.b, 0f);
    float r = radius;
    for (int y = 0; y < size; y++) {
      for (int x = 0; x < size; x++) {
        float dx = Mathf.Min(x, size - 1 - x);
        float dy = Mathf.Min(y, size - 1 - y);
        bool inside = dx >= r || dy >= r
          || ((r - dx) * (r - dx) + (r - dy) * (r - dy)) <= r * r;
        tex.SetPixel(x, y, inside ? color : clear);
      }
    }
    tex.Apply();
    return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
      100f, 0u, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
  }
}
