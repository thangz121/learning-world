// A_World/RecordingToast.cs — Agent A (World & Visual).
// Non-modal result notice (code-built uGUI): tells the player what happened
// ("saved where + filenames", "failed + why") without blocking play.
// Deliberately NOT a dialog: no GraphicRaycaster anywhere, every Graphic
// has raycastTarget=false, so clicks pass straight through to the game.
// Auto-hides after a few seconds. Hidden = SetActive(false).
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RecordingToast : MonoBehaviour {
  const float DefaultSec = 6f;

  GameObject _root;
  Text _text;
  float _hideAt = -1f;

  void Awake() {
    BuildUiImmediate();
  }

  public void BuildUiImmediate() {
    if (_root != null) return;
    BuildUi();
  }

  public bool IsShowing => _root != null && _root.activeSelf;

  public void Show(string msg, float seconds) {
    BuildUiImmediate();
    try {
      if (_text != null) _text.text = msg ?? string.Empty;
      float dur = seconds > 0f ? seconds : DefaultSec;
      if (dur > 30f) dur = 30f;
      _hideAt = Time.unscaledTime + dur;
      _root.SetActive(true);
    } catch (Exception) { }
  }

  public void Hide() {
    try {
      _hideAt = -1f;
      if (_root != null) _root.SetActive(false);
    } catch (Exception) { }
  }

  // Test seam: advance the clock deterministically (EditMode has no frame pump).
  public void TestAdvance(float dt) {
    try {
      if (_hideAt >= 0f) _hideAt -= Math.Max(0f, dt);
      Update();
    } catch (Exception) { }
  }

  void Update() {
    try {
      if (!IsShowing || _hideAt < 0f) return;
      if (Time.unscaledTime >= _hideAt) Hide();
    } catch (Exception) { }
  }

  void BuildUi() {
    Font font = UiFont.Get();

    _root = new GameObject("RecordingToastRoot");
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
    box.color = new Color(0f, 0f, 0f, 0.78f);
    box.raycastTarget = false;
    RectTransform rt = boxGo.GetComponent<RectTransform>();
    rt.anchorMin = new Vector2(0.5f, 0f);
    rt.anchorMax = new Vector2(0.5f, 0f);
    rt.pivot = new Vector2(0.5f, 0f);
    rt.sizeDelta = new Vector2(620f, 120f);
    rt.anchoredPosition = new Vector2(0f, 228f);

    GameObject tGo = new GameObject("Text");
    tGo.transform.SetParent(boxGo.transform, false);
    _text = tGo.AddComponent<Text>();
    _text.font = font;
    _text.fontSize = 22;
    _text.color = Color.white;
    _text.alignment = TextAnchor.MiddleCenter;
    _text.verticalOverflow = VerticalWrapMode.Overflow;
    _text.raycastTarget = false;
    RectTransform trt = tGo.GetComponent<RectTransform>();
    trt.anchorMin = Vector2.zero;
    trt.anchorMax = Vector2.one;
    trt.offsetMin = new Vector2(16f, 8f);
    trt.offsetMax = new Vector2(-16f, -8f);

    _root.SetActive(false);
  }
}
