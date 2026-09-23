// A_World/LanguageDialog.cs — startup language chooser (user order).
// v2 (user feedback): NO full-screen modal — two SMALL in-game boxes with a
// clear surrounding border (affordance for 4-6yo: big target + visible box +
// immediate response), pinned top-centre so play stays visible. ROOT-CAUSE
// FIX: the canvas now ships a GraphicRaycaster (without it clicks never
// register, which is why "Tiếng Việt" felt dead).
// DialogueLang stays the single source of truth (save + HUD chip unchanged).
// C# 9.0 only.
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LanguageDialog : MonoBehaviour {
  // Girl-first pink palette (S6/S7 art direction).
  static readonly Color PanelFill = new Color(0.99f, 0.93f, 0.95f, 0.96f);
  static readonly Color PanelBorder = new Color(0.96f, 0.76f, 0.85f, 1f);
  static readonly Color BoxFill = new Color(1f, 0.97f, 0.975f, 1f);
  static readonly Color BoxPressed = new Color(0.98f, 0.84f, 0.90f, 1f);
  static readonly Color TitleInk = new Color(0.36f, 0.18f, 0.28f, 1f);
  static readonly Color SubInk = new Color(0.72f, 0.44f, 0.56f, 1f);
  static readonly Color ViAccent = new Color(0.93f, 0.45f, 0.55f, 1f);
  static readonly Color EnAccent = new Color(0.45f, 0.70f, 0.92f, 1f);
  static readonly Color BlossomA = new Color(0.99f, 0.78f, 0.86f, 0.95f);
  static readonly Color BlossomB = new Color(0.97f, 0.66f, 0.78f, 0.9f);

  MarketHUD _hud;
  GameObject _canvasGo;
  bool _pendingShow;

  public bool IsOpen { get; private set; }
  public bool IsBuilt { get { return _canvasGo != null; } }

  public void Build(MarketHUD hud) {
    _hud = hud;
    if (_canvasGo != null) return;
    BuildUi();
  }

  // Startup order (user order): the chooser must WAIT until every system
  // notification (phone/mic offer, dependency/LAN setup, recording dialogs) is
  // confirmed — only then it appears, so nothing overlaps another dialog.
  public void QueueShow() {
    _pendingShow = true;
    if (_canvasGo != null) _canvasGo.SetActive(false);
    IsOpen = false;
  }

  // S3-P2Z6 (journey-found bug): the chooser floated at screen centre FOREVER
  // until a box was tapped — it sat on top of the zone panel and swallowed the
  // "Vào chơi" (Play) button, so the arena was unreachable. The chooser is an
  // offer, never a wall: it closes itself as soon as the child starts playing
  // (walks away from the arrival) or picks a language.
  public Transform Player;        // wired by MarketBootstrap (null = no auto-hide)
  Vector3 _showAnchor;
  bool _hasAnchor;

  float _busyLogT = -10f;

  void Update() { Tick(Time.unscaledDeltaTime); }

  // Test seam: same logic without a live frame.
  public void Tick(float dt) {
    if (_pendingShow) {
      if (SystemDialogBusy()) {
        // Dev-truth (journey diagnosis): name what is holding the chooser back.
        if (Time.unscaledTime - _busyLogT >= 5f) {
          _busyLogT = Time.unscaledTime;
          try { Debug.Log("[LanguageDialog] waiting; busy=" + BusyReason(), this); } catch (Exception) { }
        }
        return;
      }
      _pendingShow = false;
      Show();
      return;
    }
    if (!IsOpen || Player == null) return;
    if (!_hasAnchor) { _hasAnchor = true; _showAnchor = Player.position; return; }
    if ((Player.position - _showAnchor).sqrMagnitude > 9f) { // walked 3m away
      try { Debug.Log("[LanguageDialog] auto-closed (the child started playing).", this); }
      catch (Exception) { }
      Hide();
    }
  }

  static string BusyReason() {
    try {
      MicSetupDialog mic = FindAnyObjectByType<MicSetupDialog>();
      if (mic != null && mic.IsShowing) return "MicSetupDialog";
      DependencySetupDialog dep = FindAnyObjectByType<DependencySetupDialog>();
      if (dep != null && dep.IsShowing) return "DependencySetupDialog";
      RecordingConfirmDialog confirm = FindAnyObjectByType<RecordingConfirmDialog>();
      if (confirm != null && confirm.IsShowing) return "RecordingConfirmDialog";
      RecordingLocationDialog loc = FindAnyObjectByType<RecordingLocationDialog>();
      if (loc != null && loc.IsShowing) return "RecordingLocationDialog";
    } catch (Exception) { }
    return "?";
  }

  // Any VISIBLE system dialog blocks the chooser. ROOT-CAUSE FIX (journey
  // report: the chooser never appeared): these dialog COMPONENTS exist from
  // boot with their panels hidden, so a bare FindObjectOfType found them
  // forever. Every dialog exposes IsShowing — ask that instead.
  static bool SystemDialogBusy() {
    try {
      MicSetupDialog mic = FindAnyObjectByType<MicSetupDialog>();
      if (mic != null && mic.IsShowing) return true;
      DependencySetupDialog dep = FindAnyObjectByType<DependencySetupDialog>();
      if (dep != null && dep.IsShowing) return true;
      RecordingConfirmDialog confirm = FindAnyObjectByType<RecordingConfirmDialog>();
      if (confirm != null && confirm.IsShowing) return true;
      RecordingLocationDialog loc = FindAnyObjectByType<RecordingLocationDialog>();
      if (loc != null && loc.IsShowing) return true;
    } catch (Exception) { }
    return false;
  }

  public void Show() {
    if (_canvasGo != null) _canvasGo.SetActive(true);
    IsOpen = true;
    _hasAnchor = Player != null;
    if (_hasAnchor) _showAnchor = Player.position;
    try { Debug.Log("[LanguageDialog] shown (current=" + DialogueLang.Current + ").", this); }
    catch (Exception) { }
  }

  public void Hide() {
    if (_canvasGo != null) _canvasGo.SetActive(false);
    IsOpen = false;
  }

  // Choice seam (buttons + tests): set the language, persist it, refresh the
  // HUD chip objective text, close the boxes with an immediate reaction.
  public void Choose(DialogueLanguage lang) {
    try {
      DialogueLang.Set(lang);
      DialogueLang.Persist();
      if (_hud != null) _hud.RefreshLanguageLabel();
    } catch (Exception) { }
    try { Debug.Log("[LanguageDialog] chosen=" + DialogueLang.Current + ".", this); }
    catch (Exception) { }
    Hide();
  }

  // ---- compact in-game panel ---------------------------------------------------

  void BuildUi() {
    _canvasGo = new GameObject("LangCanvas");
    _canvasGo.transform.SetParent(transform, false);
    Canvas canvas = _canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 90; // above the HUD (80)
    // CRITICAL: without the raycaster the two boxes are silent (user report).
    _canvasGo.AddComponent<GraphicRaycaster>();
    CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1920f, 1080f);
    scaler.matchWidthOrHeight = 0.5f;

    // Small panel, DEAD CENTRE (user order) — play stays visible behind it.
    RectTransform panel = Rect(_canvasGo.transform, "LangPanel",
      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(860f, 340f));
    Image panelBg = panel.gameObject.AddComponent<Image>();
    panelBg.sprite = Rounded(64, 22, PanelFill);
    panelBg.type = Image.Type.Sliced;
    RectTransform border = Rect(_canvasGo.transform, "LangPanelBorder",
      new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(884f, 364f));
    Image borderImg = border.gameObject.AddComponent<Image>();
    borderImg.sprite = Rounded(64, 26, PanelBorder);
    borderImg.type = Image.Type.Sliced;
    borderImg.raycastTarget = false;
    border.SetSiblingIndex(panel.GetSiblingIndex()); // border behind the panel

    // Tiny blossom accents (still, no text dependency).
    Blossom(panel, new Vector2(-392f, 132f), 54f, BlossomA);
    Blossom(panel, new Vector2(394f, 128f), 46f, BlossomB);

    Label(panel, "Title", "Chọn ngôn ngữ · Choose language", 46f, TitleInk,
      new Vector2(0f, 122f), new Vector2(800f, 56f));

    // Two small boxes: outer accent border + inner cream face + big labels.
    LangBox(panel, "ViBox", new Vector2(-196f, -8f), "Tiếng Việt", "Vietnamese",
      ViAccent, DialogueLanguage.Vietnamese);
    LangBox(panel, "EnBox", new Vector2(196f, -8f), "English", "Tiếng Anh",
      EnAccent, DialogueLanguage.English);

    Label(panel, "Hint", "Con chọn ngôn ngữ nhé!", 28f, SubInk,
      new Vector2(0f, -132f), new Vector2(760f, 44f));
  }

  void LangBox(RectTransform parent, string name, Vector2 pos, string label, string sub,
      Color accent, DialogueLanguage lang) {
    // Visible BOX: accent border frame + cream face (affordance for kids).
    RectTransform frame = Rect(parent, name + "Frame", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
      pos, new Vector2(344f, 152f));
    Image frameImg = frame.gameObject.AddComponent<Image>();
    frameImg.sprite = Rounded(64, 20, accent);
    frameImg.type = Image.Type.Sliced;
    frameImg.raycastTarget = false;
    RectTransform box = Rect(frame, name, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-14f, -14f));
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
    DialogueLanguage captured = lang;
    btn.onClick.AddListener(delegate { Choose(captured); });
    Label(box, "Label", label, 52f, TitleInk, new Vector2(0f, 22f), new Vector2(320f, 70f));
    Label(box, "Sub", sub, 26f, SubInk, new Vector2(0f, -34f), new Vector2(320f, 44f));
  }

  void Blossom(RectTransform parent, Vector2 pos, float size, Color color) {
    RectTransform rect = Rect(parent, "Blossom", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
      pos, new Vector2(size, size));
    Image img = rect.gameObject.AddComponent<Image>();
    img.sprite = Rounded(64, 32, color); // circle
    img.raycastTarget = false;
  }

  static Text Label(RectTransform parent, string name, string text, float size, Color color,
      Vector2 pos, Vector2 rectSize) {
    RectTransform rect = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, rectSize);
    Text label = rect.gameObject.AddComponent<Text>();
    label.font = UiFont.Get();
    label.fontSize = Mathf.RoundToInt(size);
    label.color = color;
    label.alignment = TextAnchor.MiddleCenter;
    label.horizontalOverflow = HorizontalWrapMode.Overflow;
    label.verticalOverflow = VerticalWrapMode.Overflow;
    label.text = text;
    label.raycastTarget = false;
    return label;
  }

  static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
      Vector2 pos, Vector2 size) {
    GameObject go = new GameObject(name);
    go.transform.SetParent(parent, false);
    RectTransform rect = go.AddComponent<RectTransform>();
    rect.anchorMin = anchorMin;
    rect.anchorMax = anchorMax;
    rect.pivot = new Vector2(0.5f, 0.5f);
    rect.anchoredPosition = pos;
    rect.sizeDelta = size;
    if (anchorMin == Vector2.zero && anchorMax == Vector2.one) rect.offsetMin = rect.offsetMax = Vector2.zero;
    return rect;
  }

  // Procedural rounded-rect sprite (same proven helper as MarketHUD/WorldNameLabel).
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
