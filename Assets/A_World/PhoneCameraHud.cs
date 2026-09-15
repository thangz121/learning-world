// A_World/PhoneCameraHud.cs — Agent A (World & Visual).
// Phase 2.2 realtime face-stream box: a SMALL secondary video window pinned to
// the BOTTOM-LEFT corner. The GAME WORLD stays primary: this box never grows,
// never moves, never covers the objective chip (top-left), the mic widget
// (top-right), the replay button (bottom-right), the centered QR/setup modal,
// or the world center where NPC faces, quest targets and the question bubble
// live.
//
//   layout (Box-space, origin bottom-left of the 220x200 Box):
//     Box      220x200 at anchor(0,0) pivot(0,0) pos(20,20) — bottom-left,
//                diagonally opposite the mic box and replay button.
//     Title    "CAMERA" tiny header (12px, grey).
//     Video    RawImage 200x140 with AspectRatioFitter FIT (no stretch, §22).
//     Status   one-line state text (Waiting/Live/lost — never a frozen face).
//     Canvas order 55: above the setup dialog (50) and HUD chip (10), below
//       mic HUD (60) and cursor (100).
//
// Presentation ONLY: polls GameCameraStreamService (read-only State +
// CurrentTexture), owns no network state, fires no events, touches no camera
// APIs. Never eats clicks: no GraphicRaycaster, every Graphic
// raycastTarget=false (same contract as CursorPresenter/MicStatusHud, pinned
// by tests). Code-built uGUI, no assets. Privacy (§11): shows RAM frames only,
// writes nothing to disk.
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PhoneCameraHud : MonoBehaviour {
  public const int CanvasOrder = 55;
  public static readonly Vector2 BoxSize = new Vector2(220f, 200f);
  public static readonly Vector2 BoxPosition = new Vector2(20f, 20f);
  public static readonly Vector2 VideoSize = new Vector2(200f, 140f);

  GameCameraStreamService _service;

  GameObject _root;
  GameObject _boxGo;
  RawImage _video;
  Text _status;
  Text _placeholder;
  AspectRatioFitter _fitter;

  public void Bind(GameCameraStreamService service) {
    _service = service;
  }

  void Awake() {
    BuildHudImmediate();
  }

  // Deterministic build hook (same pattern as MicStatusHud): tests build
  // synchronously instead of relying on Awake.
  public void BuildHudImmediate() {
    if (_root != null) return;
    BuildHud();
  }

  public bool IsShowing => _root != null && _root.activeSelf
    && _boxGo != null && _boxGo.activeSelf;

  public bool IsVideoVisible => _video != null && _video.gameObject.activeSelf
    && _video.texture != null;

  public string StatusText => _status != null ? _status.text : string.Empty;

  void Update() {
    if (_service == null) return;
    PhoneCameraState state;
    Texture tex;
    try {
      state = _service.State;
      tex = _service.CurrentTexture; // null unless Live (service gates staleness)
    } catch (Exception) { return; }
    ApplyState(state, tex);
  }

  // Test seam: drive rendering deterministically (no live service).
  public void RefreshForTests(PhoneCameraState state, Texture tex) {
    if (_root == null) BuildHudImmediate();
    ApplyState(state, tex);
  }

  void ApplyState(PhoneCameraState state, Texture tex) {
    if (_root == null) return;
    try {
      if (!_root.activeSelf) _root.SetActive(true);
      bool live = state == PhoneCameraState.Live && tex != null;
      _video.texture = live ? tex : null;
      _video.gameObject.SetActive(live);
      _placeholder.gameObject.SetActive(!live);
      _placeholder.text = PlaceholderFor(state);
      _status.text = ShortStatusFor(state);
    } catch (Exception) { }
  }

  static string PlaceholderFor(PhoneCameraState state) {
    switch (state) {
      case PhoneCameraState.Live: return string.Empty;
      case PhoneCameraState.Disabled: return "Camera off";
      case PhoneCameraState.WaitingForPhone: return "Waiting for phone…";
      case PhoneCameraState.Connecting: return "Connecting…";
      case PhoneCameraState.ConnectedWaitingFrames: return "Camera ready — start on phone";
      case PhoneCameraState.TempDisconnected: return "Camera lost — reconnecting…";
      case PhoneCameraState.Error: return "Camera error";
      case PhoneCameraState.Stopped: return "Camera stopped";
      default: return "Camera…";
    }
  }

  static string ShortStatusFor(PhoneCameraState state) {
    switch (state) {
      case PhoneCameraState.Live: return "CAMERA ● LIVE";
      case PhoneCameraState.Disabled: return "CAMERA ○ OFF";
      case PhoneCameraState.WaitingForPhone: return "CAMERA ○ WAITING";
      case PhoneCameraState.Connecting: return "CAMERA ○ CONNECTING";
      case PhoneCameraState.ConnectedWaitingFrames: return "CAMERA ○ READY";
      case PhoneCameraState.TempDisconnected: return "CAMERA ○ LOST";
      case PhoneCameraState.Error: return "CAMERA ○ ERROR";
      case PhoneCameraState.Stopped: return "CAMERA ○ STOPPED";
      default: return "CAMERA ○";
    }
  }

  // ---- code-built uGUI -----------------------------------------------------
  void BuildHud() {
    _root = new GameObject("PhoneCameraRoot");
    _root.transform.SetParent(transform, false);
    Canvas canvas = _root.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = CanvasOrder;
    // NOTE: no GraphicRaycaster on purpose — this box never eats clicks.
    Stretch(_root);

    _boxGo = new GameObject("Box");
    _boxGo.transform.SetParent(_root.transform, false);
    RectTransform boxRt = _boxGo.AddComponent<RectTransform>();
    boxRt.anchorMin = new Vector2(0f, 0f);
    boxRt.anchorMax = new Vector2(0f, 0f);
    boxRt.pivot = new Vector2(0f, 0f);
    boxRt.sizeDelta = BoxSize;
    boxRt.anchoredPosition = BoxPosition;
    Image box = _boxGo.AddComponent<Image>();
    box.color = new Color(0.06f, 0.08f, 0.10f, 0.72f);
    box.raycastTarget = false;

    Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    GameObject titleGo = new GameObject("Title");
    titleGo.transform.SetParent(_boxGo.transform, false);
    Text title = titleGo.AddComponent<Text>();
    title.font = font;
    title.fontSize = 12;
    title.color = new Color(0.75f, 0.78f, 0.82f);
    title.alignment = TextAnchor.UpperLeft;
    title.text = "CAMERA";
    title.raycastTarget = false;
    RectTransform titleRt = titleGo.GetComponent<RectTransform>();
    titleRt.anchorMin = Vector2.zero;
    titleRt.anchorMax = Vector2.zero;
    titleRt.pivot = new Vector2(0f, 1f);
    titleRt.anchoredPosition = new Vector2(8f, 192f);
    titleRt.sizeDelta = new Vector2(120f, 18f);

    GameObject videoGo = new GameObject("Video");
    videoGo.transform.SetParent(_boxGo.transform, false);
    _video = videoGo.AddComponent<RawImage>();
    _video.color = Color.white;
    _video.raycastTarget = false;
    RectTransform videoRt = videoGo.GetComponent<RectTransform>();
    videoRt.anchorMin = Vector2.zero;
    videoRt.anchorMax = Vector2.zero;
    videoRt.pivot = new Vector2(0f, 0f);
    videoRt.anchoredPosition = new Vector2(10f, 30f);
    videoRt.sizeDelta = VideoSize;
    _fitter = videoGo.AddComponent<AspectRatioFitter>();
    _fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
    _fitter.aspectRatio = 4f / 3f;
    _video.gameObject.SetActive(false);

    GameObject phGo = new GameObject("Placeholder");
    phGo.transform.SetParent(_boxGo.transform, false);
    _placeholder = phGo.AddComponent<Text>();
    _placeholder.font = font;
    _placeholder.fontSize = 13;
    _placeholder.color = new Color(0.65f, 0.68f, 0.72f);
    _placeholder.alignment = TextAnchor.MiddleCenter;
    _placeholder.text = "Waiting for phone…";
    _placeholder.raycastTarget = false;
    RectTransform phRt = phGo.GetComponent<RectTransform>();
    phRt.anchorMin = Vector2.zero;
    phRt.anchorMax = Vector2.zero;
    phRt.pivot = new Vector2(0f, 0f);
    phRt.anchoredPosition = new Vector2(10f, 30f);
    phRt.sizeDelta = VideoSize;

    GameObject stGo = new GameObject("Status");
    stGo.transform.SetParent(_boxGo.transform, false);
    _status = stGo.AddComponent<Text>();
    _status.font = font;
    _status.fontSize = 12;
    _status.color = new Color(0.85f, 0.87f, 0.90f);
    _status.alignment = TextAnchor.LowerLeft;
    _status.text = "CAMERA ○ WAITING";
    _status.raycastTarget = false;
    RectTransform stRt = stGo.GetComponent<RectTransform>();
    stRt.anchorMin = Vector2.zero;
    stRt.anchorMax = Vector2.zero;
    stRt.pivot = new Vector2(0f, 0f);
    stRt.anchoredPosition = new Vector2(8f, 8f);
    stRt.sizeDelta = new Vector2(204f, 18f);
  }

  static void Stretch(GameObject go) {
    RectTransform rt = go.GetComponent<RectTransform>();
    if (rt == null) rt = go.AddComponent<RectTransform>();
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;
  }
}
