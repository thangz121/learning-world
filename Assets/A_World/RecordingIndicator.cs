// A_World/RecordingIndicator.cs — Agent A (World & Visual).
// "Am I recording?" answer (user ask): a small non-modal badge, top-center.
// While RECORDING only a blinking red DOT shows — player rule (no "Đang quay"
// text in the exported file: view capture composites overlay, so any text
// would burn in; the dot keeps the live cue with near-zero export footprint).
// While FINALIZING the full "finishing…" line shows (transient, and capture
// has stopped by then). Deliberately NOT a dialog:
// no GraphicRaycaster anywhere, every Graphic has raycastTarget=false, so
// gameplay clicks pass straight through. The driver (MediaRecordingService)
// owns state and only calls SetRecording/SetFinishing/Hide here.
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RecordingIndicator : MonoBehaviour {
  GameObject _root;
  Image _box;
  Image _dot;
  Text _text;
  bool _recording;
  bool _finishing;

  void Awake() {
    BuildUiImmediate();
  }

  public void BuildUiImmediate() {
    if (_root != null) return;
    BuildUi();
  }

  public bool IsShowing => _root != null && _root.activeSelf;
  public bool IsRecordingShown => IsShowing && _recording;

  public void SetRecording(bool recording, float elapsedSec) {
    BuildUiImmediate();
    try {
      _recording = recording;
      _finishing = false;
      // Dot-only while recording (player rule: no text in the export).
      // NOTE: only the box IMAGE is disabled — the dot/text ride on the box
      // object, so deactivating the object would kill the dot too.
      // Elapsed param kept for the API (callers already compute it).
      if (_text != null) _text.text = string.Empty;
      if (_box != null) _box.enabled = false;
      if (_dot != null && !_dot.gameObject.activeSelf) _dot.gameObject.SetActive(true);
      _root.SetActive(recording);
    } catch (Exception) { }
  }

  public void SetFinishing() {
    BuildUiImmediate();
    try {
      _recording = false;
      _finishing = true;
      if (_box != null && !_box.enabled) _box.enabled = true;
      if (_text != null) _text.text = "Đang kết thúc bản thu...";
      _root.SetActive(true);
    } catch (Exception) { }
  }

  public void Hide() {
    try {
      _recording = false;
      _finishing = false;
      if (_root != null) _root.SetActive(false);
    } catch (Exception) { }
  }

  void Update() {
    try {
      if (!IsShowing) return;
      if (_dot != null) {
        // 2 Hz blink while recording; steady while finishing.
        float a = _recording
          ? (Math.Sin(Time.unscaledTime * Math.PI * 4f) > 0f ? 1f : 0.25f)
          : 1f;
        Color c = _dot.color;
        c.a = a;
        _dot.color = c;
      }
    } catch (Exception) { }
  }

  public static string FormatElapsed(float sec) {
    try {
      if (sec < 0f) sec = 0f;
      if (sec > 35999f) sec = 35999f;
      int s = (int)sec;
      return (s / 60).ToString("00") + ":" + (s % 60).ToString("00");
    } catch (Exception) { return "00:00"; }
  }

  void BuildUi() {
    Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    _root = new GameObject("RecordingIndicatorRoot");
    _root.transform.SetParent(transform, false);
    Canvas canvas = _root.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 65; // above HUDs, below modals (70)
    CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1280f, 720f);
    // NOTE: no GraphicRaycaster on purpose — clicks pass through.

    GameObject boxGo = new GameObject("Box");
    boxGo.transform.SetParent(_root.transform, false);
    Image box = boxGo.AddComponent<Image>();
    box.color = new Color(0f, 0f, 0f, 0.72f);
    box.raycastTarget = false;
    _box = box;
    RectTransform rt = boxGo.GetComponent<RectTransform>();
    rt.anchorMin = new Vector2(0.5f, 1f);
    rt.anchorMax = new Vector2(0.5f, 1f);
    rt.pivot = new Vector2(0.5f, 1f);
    rt.sizeDelta = new Vector2(300f, 60f);
    rt.anchoredPosition = new Vector2(0f, -16f);

    GameObject dotGo = new GameObject("Dot");
    dotGo.transform.SetParent(boxGo.transform, false);
    _dot = dotGo.AddComponent<Image>();
    _dot.color = Color.red;
    _dot.raycastTarget = false;
    RectTransform drt = dotGo.GetComponent<RectTransform>();
    drt.anchorMin = new Vector2(0f, 0.5f);
    drt.anchorMax = new Vector2(0f, 0.5f);
    drt.pivot = new Vector2(0.5f, 0.5f);
    drt.sizeDelta = new Vector2(20f, 20f);
    drt.anchoredPosition = new Vector2(30f, 0f);

    GameObject tGo = new GameObject("Text");
    tGo.transform.SetParent(boxGo.transform, false);
    _text = tGo.AddComponent<Text>();
    _text.font = font;
    _text.fontSize = 26;
    _text.color = Color.white;
    _text.alignment = TextAnchor.MiddleLeft;
    _text.verticalOverflow = VerticalWrapMode.Overflow;
    _text.raycastTarget = false;
    RectTransform trt = tGo.GetComponent<RectTransform>();
    trt.anchorMin = Vector2.zero;
    trt.anchorMax = Vector2.one;
    trt.offsetMin = new Vector2(58f, 0f);
    trt.offsetMax = new Vector2(-12f, 0f);

    _root.SetActive(false);
  }
}
