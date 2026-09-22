// A_World/DependencySetupDialog.cs — Agent A (World & Visual).
// Parent-facing startup dependency modal, code-built uGUI (same pattern as
// MicSetupDialog: ScreenSpaceOverlay canvas, dim that swallows clicks while
// shown, hidden = SetActive(false) so it can never eat clicks when idle).
// Vietnamese wording, consts in one place. Three states, never mixed:
//   LIST:     missing items (name + why + size)  [Cài đặt ngay] [Để sau]
//   PROGRESS: live per-item status                [Dừng]
//   RESULT:   per-item ✓/✗ + manual hints         [Xong]
// The dialog NEVER touches the network/disk: the driver
// (DependencySetupService) owns checks + installs and only calls Show*/Set*.
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DependencySetupDialog : MonoBehaviour {
  public const string ListTitle = "Thiếu phần mềm bổ trợ";
  public const string ListLead =
    "Game cần thêm mấy thứ này để quay video và kết nối điện thoại.\nBấm CÀI ĐẶT NGAY để máy tự tải và cài (cần mạng 1 lần).";
  public const string ListInstall = "Cài đặt ngay";
  public const string ListLater = "Để sau";
  public const string ProgressTitle = "Đang cài đặt...";
  public const string ProgressStop = "Dừng";
  public const string ResultTitle = "Xong kiểm tra";
  public const string ResultClose = "Xong";

  GameObject _root;
  GameObject _panel;
  Text _title;
  Text _body;
  Text _status;
  Button _primary;
  Text _primaryLabel;
  Button _secondary;
  Text _secondaryLabel;

  Action _onPrimary;
  Action _onSecondary;

  void Awake() {
    BuildUiImmediate();
  }

  public void BuildUiImmediate() {
    if (_root != null) return;
    BuildUi();
  }

  public bool IsShowing => _root != null && _root.activeSelf
    && _panel != null && _panel.activeSelf;

  public void ShowList(string itemsText, Action onInstall, Action onLater) {
    BuildUiImmediate();
    _onPrimary = onInstall;
    _onSecondary = onLater;
    SetTitle(ListTitle);
    SetBody(ListLead + "\n\n" + (itemsText ?? string.Empty));
    SetStatus(string.Empty);
    SetPrimary(ListInstall, true);
    SetSecondary(ListLater, true);
    _root.SetActive(true);
    _panel.SetActive(true);
  }

  public void ShowProgress(Action onStop) {
    BuildUiImmediate();
    _onPrimary = onStop;
    _onSecondary = null;
    SetTitle(ProgressTitle);
    SetPrimary(ProgressStop, true);
    SetSecondary(string.Empty, false);
    _root.SetActive(true);
    _panel.SetActive(true);
  }

  public void SetProgressText(string text) {
    SetStatus(text);
  }

  public void ShowResult(string summaryText, Action onClose) {
    BuildUiImmediate();
    _onPrimary = onClose;
    _onSecondary = null;
    SetTitle(ResultTitle);
    SetBody(summaryText ?? string.Empty);
    SetStatus(string.Empty);
    SetPrimary(ResultClose, true);
    SetSecondary(string.Empty, false);
    _root.SetActive(true);
    _panel.SetActive(true);
  }

  public void Hide() {
    if (_root != null) _root.SetActive(false);
    _onPrimary = _onSecondary = null;
  }

  void SetTitle(string s) {
    try { if (_title != null) _title.text = s ?? string.Empty; } catch (Exception) { }
  }

  void SetBody(string s) {
    try { if (_body != null) _body.text = s ?? string.Empty; } catch (Exception) { }
  }

  void SetStatus(string s) {
    try { if (_status != null) _status.text = s ?? string.Empty; } catch (Exception) { }
  }

  void SetPrimary(string label, bool visible) {
    try {
      if (_primaryLabel != null) _primaryLabel.text = label ?? string.Empty;
      if (_primary != null) _primary.gameObject.SetActive(visible);
    } catch (Exception) { }
  }

  void SetSecondary(string label, bool visible) {
    try {
      if (_secondaryLabel != null) _secondaryLabel.text = label ?? string.Empty;
      if (_secondary != null) _secondary.gameObject.SetActive(visible);
    } catch (Exception) { }
  }

  void BuildUi() {
    Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    _root = new GameObject("DependencySetupRoot");
    _root.transform.SetParent(transform, false);
    Canvas canvas = _root.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 70; // topmost startup modal (mic offer is 50)
    CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1280f, 720f);
    _root.AddComponent<GraphicRaycaster>();

    GameObject dimGo = new GameObject("Dim");
    dimGo.transform.SetParent(_root.transform, false);
    Image dim = dimGo.AddComponent<Image>();
    dim.color = new Color(0f, 0f, 0f, 0.55f);
    // B1R2: startup prompts must never swallow world clicks (see MicSetupDialog).
    dim.raycastTarget = false;
    Stretch(dimGo);

    _panel = new GameObject("Panel");
    _panel.transform.SetParent(_root.transform, false);
    Image card = _panel.AddComponent<Image>();
    card.color = new Color(1f, 0.97f, 0.90f, 1f);
    // P3.0.1 journey fix: card graphics must not swallow world clicks.
    card.raycastTarget = false;
    RectTransform rt = _panel.GetComponent<RectTransform>();
    rt.anchorMin = new Vector2(0.5f, 0.5f);
    rt.anchorMax = new Vector2(0.5f, 0.5f);
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.sizeDelta = new Vector2(680f, 560f);
    rt.anchoredPosition = Vector2.zero;

    _title = MakeText("Title", _panel.transform, font, 38,
      new Color(0.35f, 0.22f, 0.12f), TextAnchor.MiddleCenter, false);
    Place(_title.gameObject, 0f, 1f, 1f, 1f, 24f, -80f, -24f, -20f);

    _body = MakeText("Body", _panel.transform, font, 24,
      new Color(0.25f, 0.18f, 0.10f), TextAnchor.UpperLeft, true);
    Place(_body.gameObject, 0f, 1f, 1f, 1f, 40f, -400f, -40f, -96f);

    _status = MakeText("Status", _panel.transform, font, 22,
      new Color(0.15f, 0.35f, 0.15f), TextAnchor.MiddleCenter, true);
    Place(_status.gameObject, 0f, 0f, 1f, 0f, 24f, 110f, -24f, 168f);

    GameObject pGo = MakeButton("Cài đặt ngay", new Color(0.20f, 0.55f, 0.30f), font);
    pGo.transform.SetParent(_panel.transform, false);
    _primary = pGo.GetComponent<Button>();
    _primaryLabel = pGo.GetComponentInChildren<Text>();
    _primary.onClick.AddListener(() => SafeInvoke(_onPrimary));
    Place(pGo, 0f, 0f, 0.5f, 0f, 24f, 32f, -12f, 96f);

    GameObject sGo = MakeButton("Để sau", new Color(0.55f, 0.55f, 0.58f), font);
    sGo.transform.SetParent(_panel.transform, false);
    _secondary = sGo.GetComponent<Button>();
    _secondaryLabel = sGo.GetComponentInChildren<Text>();
    _secondary.onClick.AddListener(() => SafeInvoke(_onSecondary));
    Place(sGo, 0.5f, 0f, 1f, 0f, 12f, 32f, -24f, 96f);

    _root.SetActive(false);
  }

  static Text MakeText(string name, Transform parent, Font font, int size,
      Color color, TextAnchor align, bool overflow) {
    GameObject go = new GameObject(name);
    go.transform.SetParent(parent, false);
    Text t = go.AddComponent<Text>();
    t.font = font;
    t.fontSize = size;
    t.color = color;
    t.alignment = align;
    t.raycastTarget = false; // P3.0.1 journey fix: text never eats world clicks
    if (overflow) t.verticalOverflow = VerticalWrapMode.Overflow;
    return t;
  }

  static GameObject MakeButton(string label, Color color, Font font) {
    GameObject go = new GameObject("Button");
    Image img = go.AddComponent<Image>();
    img.color = color;
    go.AddComponent<Button>();
    GameObject lGo = new GameObject("Label");
    lGo.transform.SetParent(go.transform, false);
    Text l = lGo.AddComponent<Text>();
    l.font = font;
    l.fontSize = 24;
    l.color = Color.white;
    l.alignment = TextAnchor.MiddleCenter;
    l.text = label;
    RectTransform rt = lGo.GetComponent<RectTransform>();
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;
    return go;
  }

  // Same inverted-offset rule as MicSetupDialog.Place: bottom < top.
  static void Place(GameObject go, float ax0, float ay0, float ax1, float ay1,
      float left, float bottom, float right, float top) {
    RectTransform rt = go.GetComponent<RectTransform>();
    rt.anchorMin = new Vector2(ax0, ay0);
    rt.anchorMax = new Vector2(ax1, ay1);
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.offsetMin = new Vector2(left, bottom);
    rt.offsetMax = new Vector2(right, top);
  }

  static void Stretch(GameObject go) {
    RectTransform rt = go.GetComponent<RectTransform>();
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;
  }

  static void SafeInvoke(Action a) {
    try { if (a != null) a(); } catch (Exception) { }
  }
}
