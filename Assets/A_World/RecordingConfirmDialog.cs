// A_World/RecordingConfirmDialog.cs — Agent A (World & Visual).
// Generic two-button confirm modal (code-built uGUI, same modal pattern as
// the other setup dialogs: dim swallows clicks while shown, hidden =
// SetActive(false)). Used for the explicit video-only proposal when F2
// finds video but no recordable audio: the game PROPOSES ("quay video
// không tiếng?") instead of silently falling back or bare-failing.
// Never touches media/network/disk: the driver (MediaRecordingService)
// decides and only calls Show/Hide here. Vietnamese consts in one place.
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RecordingConfirmDialog : MonoBehaviour {
  GameObject _root;
  GameObject _panel;
  Text _title;
  Text _body;

  Action _onOk;
  Action _onCancel;

  void Awake() {
    BuildUiImmediate();
  }

  public void BuildUiImmediate() {
    if (_root != null) return;
    BuildUi();
  }

  public bool IsShowing => _root != null && _root.activeSelf
    && _panel != null && _panel.activeSelf;

  public void ShowConfirm(string title, string body, string okLabel,
      string cancelLabel, Action onOk, Action onCancel) {
    BuildUiImmediate();
    _onOk = onOk;
    _onCancel = onCancel;
    try {
      if (_title != null) _title.text = title ?? string.Empty;
      if (_body != null) _body.text = body ?? string.Empty;
      SetLabel("OkButton", okLabel);
      SetLabel("CancelButton", cancelLabel);
    } catch (Exception) { }
    _root.SetActive(true);
    _panel.SetActive(true);
  }

  public void Hide() {
    if (_root != null) _root.SetActive(false);
    _onOk = _onCancel = null;
  }

  void SetLabel(string buttonName, string text) {
    try {
      Transform t = _panel.transform.Find(buttonName);
      if (t == null) return;
      Text l = t.GetComponentInChildren<Text>();
      if (l != null) l.text = text ?? string.Empty;
    } catch (Exception) { }
  }

  void BuildUi() {
    Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    _root = new GameObject("RecordingConfirmRoot");
    _root.transform.SetParent(transform, false);
    Canvas canvas = _root.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 70; // topmost modal band (with the chooser)
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
    rt.sizeDelta = new Vector2(640f, 480f);
    rt.anchoredPosition = Vector2.zero;

    _title = MakeText("Title", _panel.transform, font, 36,
      new Color(0.35f, 0.22f, 0.12f), TextAnchor.MiddleCenter, false);
    Place(_title.gameObject, 0f, 1f, 1f, 1f, 24f, -76f, -24f, -20f);

    _body = MakeText("Body", _panel.transform, font, 24,
      new Color(0.25f, 0.18f, 0.10f), TextAnchor.MiddleCenter, true);
    Place(_body.gameObject, 0f, 1f, 1f, 1f, 40f, -330f, -40f, -92f);

    GameObject okGo = MakeButton("OkButton", "Quay video", new Color(0.20f, 0.55f, 0.30f), font);
    okGo.transform.SetParent(_panel.transform, false);
    okGo.GetComponent<Button>().onClick.AddListener(() => SafeInvoke(_onOk));
    Place(okGo, 0f, 0f, 0.5f, 0f, 24f, 32f, -12f, 96f);

    GameObject cancelGo = MakeButton("CancelButton", "Hủy", new Color(0.55f, 0.55f, 0.58f), font);
    cancelGo.transform.SetParent(_panel.transform, false);
    cancelGo.GetComponent<Button>().onClick.AddListener(() => SafeInvoke(_onCancel));
    Place(cancelGo, 0.5f, 0f, 1f, 0f, 12f, 32f, -24f, 96f);

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

  static GameObject MakeButton(string name, string label, Color color, Font font) {
    GameObject go = new GameObject(name);
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
