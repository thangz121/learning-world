// A_World/RecordingLocationDialog.cs — Agent A (World & Visual).
// In-game "where to save?" chooser (code-built uGUI, same modal pattern as
// the other setup dialogs: dim swallows clicks while shown, hidden =
// SetActive(false)). Opens with a short scale+fade animation (Update-driven,
// ~0.22 s, no assets). Three actions, never mixed with recording state —
// the driver (MediaRecordingService) owns prefs/validation/start and only
// calls Show*/SetError/Hide here. Vietnamese wording, consts in one place.
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RecordingLocationDialog : MonoBehaviour {
  public const string Title = "Lưu bản thu ở đâu?";
  public const string Lead = "Chọn chỗ lưu video MP4 + audio MP3.\nGame nhớ lựa chọn này (nhấn F2 2 lần liên tiếp để đổi chỗ khác).";
  public const string UseDefault = "Thư mục mặc định";
  public const string Browse = "Chọn chỗ khác...";
  public const string Cancel = "Hủy";

  const float AnimSec = 0.22f;

  GameObject _root;
  GameObject _panel;
  CanvasGroup _fade;
  Text _body;
  Text _error;
  Button _defaultBtn;
  Button _browseBtn;
  Button _cancelBtn;

  Action _onDefault;
  Action _onBrowse;
  Action _onCancel;

  float _animT = 1f;

  void Awake() {
    BuildUiImmediate();
  }

  public void BuildUiImmediate() {
    if (_root != null) return;
    BuildUi();
  }

  public bool IsShowing => _root != null && _root.activeSelf
    && _panel != null && _panel.activeSelf;

  // currentDisplay: resolved default dir or saved dir (one line).
  // errorOrNull: shown in red when the last choice/start failed validation.
  public void ShowChooser(string currentDisplay, string errorOrNull,
      Action onDefault, Action onBrowse, Action onCancel) {
    BuildUiImmediate();
    _onDefault = onDefault;
    _onBrowse = onBrowse;
    _onCancel = onCancel;
    SetBody(Lead + "\n\nHiện tại: " + (currentDisplay ?? string.Empty));
    SetError(errorOrNull);
    _root.SetActive(true);
    _panel.SetActive(true);
    _animT = 0f; // replay the open animation every show
  }

  public void SetError(string s) {
    try {
      if (_error == null) return;
      _error.text = s ?? string.Empty;
      _error.gameObject.SetActive(!string.IsNullOrEmpty(s));
    } catch (Exception) { }
  }

  public void Hide() {
    if (_root != null) _root.SetActive(false);
    _onDefault = _onBrowse = _onCancel = null;
  }

  void Update() {
    try {
      if (!IsShowing || _fade == null || _panel == null) return;
      if (_animT >= 1f) return;
      _animT = Math.Min(1f, _animT + Time.unscaledDeltaTime / AnimSec);
      float e = 1f - (1f - _animT) * (1f - _animT); // ease-out
      _fade.alpha = e;
      float s = 0.85f + 0.15f * e;
      _panel.transform.localScale = new Vector3(s, s, 1f);
    } catch (Exception) { }
  }

  void SetBody(string s) {
    try { if (_body != null) _body.text = s ?? string.Empty; } catch (Exception) { }
  }

  void BuildUi() {
    Font font = UiFont.Get();

    _root = new GameObject("RecLocationRoot");
    _root.transform.SetParent(transform, false);
    Canvas canvas = _root.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 70; // same topmost band as the dependency prompt
    CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1280f, 720f);
    _root.AddComponent<GraphicRaycaster>();

    GameObject dimGo = new GameObject("Dim");
    dimGo.transform.SetParent(_root.transform, false);
    Image dim = dimGo.AddComponent<Image>();
    dim.color = new Color(0f, 0f, 0f, 0.55f);
    // P1-6/O3 (J1 rule): setup prompts are click-through — only buttons capture.
    dim.raycastTarget = false;
    Stretch(dimGo);

    _panel = new GameObject("Panel");
    _panel.transform.SetParent(_root.transform, false);
    Image card = _panel.AddComponent<Image>();
    card.color = new Color(1f, 0.97f, 0.90f, 1f);
    card.raycastTarget = false; // P1-6/O3: card graphics never eat world clicks
    RectTransform rt = _panel.GetComponent<RectTransform>();
    rt.anchorMin = new Vector2(0.5f, 0.5f);
    rt.anchorMax = new Vector2(0.5f, 0.5f);
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.sizeDelta = new Vector2(660f, 520f);
    rt.anchoredPosition = Vector2.zero;
    _fade = _panel.AddComponent<CanvasGroup>();

    Text title = MakeText("Title", _panel.transform, font, 38,
      new Color(0.35f, 0.22f, 0.12f), TextAnchor.MiddleCenter, false);
    title.text = Title;
    Place(title.gameObject, 0f, 1f, 1f, 1f, 24f, -76f, -24f, -20f);

    _body = MakeText("Body", _panel.transform, font, 24,
      new Color(0.25f, 0.18f, 0.10f), TextAnchor.UpperLeft, true);
    Place(_body.gameObject, 0f, 1f, 1f, 1f, 40f, -330f, -40f, -92f);

    _error = MakeText("Error", _panel.transform, font, 22,
      new Color(0.75f, 0.15f, 0.12f), TextAnchor.MiddleCenter, true);
    Place(_error.gameObject, 0f, 0f, 1f, 0f, 24f, 168f, -24f, 226f);
    _error.gameObject.SetActive(false);

    GameObject dGo = MakeButton(UseDefault, new Color(0.20f, 0.55f, 0.30f), font);
    dGo.transform.SetParent(_panel.transform, false);
    _defaultBtn = dGo.GetComponent<Button>();
    _defaultBtn.onClick.AddListener(() => SafeInvoke(_onDefault));
    Place(dGo, 0f, 0f, 0.5f, 0f, 24f, 96f, -12f, 156f);

    GameObject bGo = MakeButton(Browse, new Color(0.25f, 0.45f, 0.75f), font);
    bGo.transform.SetParent(_panel.transform, false);
    _browseBtn = bGo.GetComponent<Button>();
    _browseBtn.onClick.AddListener(() => SafeInvoke(_onBrowse));
    Place(bGo, 0.5f, 0f, 1f, 0f, 12f, 96f, -24f, 156f);

    GameObject cGo = MakeButton(Cancel, new Color(0.55f, 0.55f, 0.58f), font);
    cGo.transform.SetParent(_panel.transform, false);
    _cancelBtn = cGo.GetComponent<Button>();
    _cancelBtn.onClick.AddListener(() => SafeInvoke(_onCancel));
    Place(cGo, 0.25f, 0f, 0.75f, 0f, 12f, 32f, -12f, 88f);

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
    t.raycastTarget = false; // P1-6/O3: text never eats world clicks
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
    l.raycastTarget = false; // the button IMAGE is the single click target
    RectTransform rt = lGo.GetComponent<RectTransform>();
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;
    return go;
  }

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
