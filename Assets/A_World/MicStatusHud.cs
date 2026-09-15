// A_World/MicStatusHud.cs — Agent A (World & Visual).
// Persistent mic-status corner widget (top-right, tiny): answers the parent's
// "is the microphone REALLY working?" without opening any panel.
//
//   PHONE mode (gate ReadyPhone/WaitPhoneLink): signal BARS (3 levels:
//     red=weak, yellow=medium, green=strong) driven by MEASURED AUDIO payload
//     energy from the bridge watcher + a DOT underneath: green = AUDIO bytes
//     arrived within MicSignal.DataFreshMs (real content flowing, not just
//     link control traffic), red = idle/control-only. Link down = bars grey
//     with a red CROSS slash + red dot.
//   LOCAL mode (gate ReadyLocal — laptop built-in, headset, USB...): a
//     HEADPHONE icon replaces the bars, dot sits BESIDE it. The dot is
//     device-presence sampling (listed + Ready + poll fresh): the HUD never
//     opens the mic (captures own the device), so it honestly reports
//     presence, not audio content, on this path.
//   NO source: grey crossed bars + red dot.
//
// Presentation ONLY: polls MicSetupMonitor.CurrentSignal (read-only snapshot),
// owns no state, fires no events, touches no mic/audio APIs. Never eats
// clicks: no GraphicRaycaster, every Image raycastTarget=false (same contract
// as CursorPresenter, pinned by tests). Code-built uGUI, no assets.
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MicStatusHud : MonoBehaviour {
  public const int CanvasOrder = 60; // above dialog modal (50), below cursor (100)
  public static readonly Color BarRed = new Color(0.898f, 0.282f, 0.302f);
  public static readonly Color BarYellow = new Color(0.961f, 0.651f, 0.137f);
  public static readonly Color BarGreen = new Color(0.184f, 0.659f, 0.310f);
  public static readonly Color BarGrey = new Color(0.45f, 0.45f, 0.48f, 0.55f);
  public static readonly Color DotGreen = new Color(0.184f, 0.659f, 0.310f);
  public static readonly Color DotRed = new Color(0.898f, 0.282f, 0.302f);
  public static readonly Color CrossRed = new Color(0.898f, 0.282f, 0.302f);
  public static readonly Color PhoneWhite = Color.white;

  MicSetupMonitor _monitor;

  GameObject _root;
  GameObject _barsGo;      // phone/no-source bars group
  Image[] _bars;           // 3 ascending bars
  GameObject _crossGo;     // red X over the bars (link down)
  GameObject _phoneGo;     // headphone icon (local mode)
  Image _dot;              // data dot (under bars, or beside headphone)
  RectTransform _dotRt;
  bool _dotBeside;         // current dot slot (test introspection)

  public void Bind(MicSetupMonitor monitor) {
    _monitor = monitor;
  }

  void Awake() {
    BuildHudImmediate();
  }

  // Deterministic build hook (same pattern as dialog/cursor): tests build
  // synchronously instead of relying on Awake.
  public void BuildHudImmediate() {
    if (_root != null) return;
    BuildHud();
  }

  public bool IsShowing => _root != null && _root.activeSelf
    && ((_barsGo != null && _barsGo.activeSelf)
      || (_phoneGo != null && _phoneGo.activeSelf));

  public int LitBars {
    get {
      if (_bars == null) return 0;
      int n = 0;
      for (int i = 0; i < _bars.Length; i++) {
        try { if (_bars[i] != null && _bars[i].color.a > 0.9f && _bars[i].color != BarGrey) n++; } catch (Exception) { }
      }
      return n;
    }
  }

  public Color DotColor => _dot != null ? _dot.color : Color.clear;
  public bool IsCrossVisible => _crossGo != null && _crossGo.activeSelf;
  public bool IsHeadphoneVisible => _phoneGo != null && _phoneGo.activeSelf;
  public bool IsBarsVisible => _barsGo != null && _barsGo.activeSelf;
  public bool IsDotBeside => _dotBeside;

  void Update() {
    if (_monitor == null) return;
    MicSignalSnapshot snap;
    try { snap = _monitor.CurrentSignal; }
    catch (Exception) { return; }
    ApplySnapshot(snap);
  }

  // Test seam: drive rendering deterministically (no live monitor).
  public void RefreshForTests(MicSignalSnapshot snap) {
    if (_root == null) BuildHudImmediate();
    ApplySnapshot(snap);
  }

  void ApplySnapshot(MicSignalSnapshot snap) {
    if (_root == null) return;
    try {
      if (!_root.activeSelf) _root.SetActive(true);
      if (snap.Source == MicSignalSource.Local) {
        _barsGo.SetActive(false);
        _crossGo.SetActive(false);
        _phoneGo.SetActive(true);
        PlaceDotBeside();
        _dot.color = snap.DataFlowing ? DotGreen : DotRed;
        return;
      }
      // Phone + None share the bars widget (None = grey + crossed).
      _phoneGo.SetActive(false);
      _barsGo.SetActive(true);
      PlaceDotUnder();
      int lit;
      MicBarColor color;
      bool crossed;
      MicSignal.ComputeBars(snap.Level, snap.LinkUp, out lit, out color, out crossed);
      Color barColor = BarColorOf(color);
      for (int i = 0; i < _bars.Length; i++) {
        _bars[i].color = i < lit ? barColor : BarGrey;
      }
      _crossGo.SetActive(crossed);
      _dot.color = snap.DataFlowing ? DotGreen : DotRed;
    } catch (Exception) { }
  }

  static Color BarColorOf(MicBarColor c) {
    switch (c) {
      case MicBarColor.Red: return BarRed;
      case MicBarColor.Yellow: return BarYellow;
      case MicBarColor.Green: return BarGreen;
      default: return BarGrey;
    }
  }

  // ---- code-built uGUI -----------------------------------------------------
  // Root stays FULLSCREEN (a ScreenSpaceOverlay root ignores its own rect —
  // sizing it was the R10 misplacement bug: children resolved against the
  // wrong ancestor). A fixed 140x170 Box pinned top-right owns every child;
  // every intermediate container is a stretched RectTransform (never a plain
  // Transform), so all coordinates below are Box-space, origin bottom-left.
  void BuildHud() {
    _root = new GameObject("MicStatusRoot");
    _root.transform.SetParent(transform, false);
    Canvas canvas = _root.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = CanvasOrder;
    // NOTE: no GraphicRaycaster on purpose — this widget never eats clicks.
    Stretch(_root);

    GameObject boxGo = new GameObject("Box");
    boxGo.transform.SetParent(_root.transform, false);
    RectTransform boxRt = boxGo.AddComponent<RectTransform>();
    boxRt.anchorMin = new Vector2(1f, 1f);
    boxRt.anchorMax = new Vector2(1f, 1f);
    boxRt.pivot = new Vector2(1f, 1f);
    boxRt.sizeDelta = new Vector2(140f, 170f);
    boxRt.anchoredPosition = new Vector2(-20f, -20f);

    _barsGo = new GameObject("Bars");
    _barsGo.transform.SetParent(boxGo.transform, false);
    Stretch(_barsGo);
    _bars = new Image[3];
    float[] heights = { 34f, 54f, 74f };
    for (int i = 0; i < 3; i++) {
      GameObject barGo = new GameObject("Bar" + (i + 1));
      barGo.transform.SetParent(_barsGo.transform, false);
      Image img = barGo.AddComponent<Image>();
      img.color = BarGrey;
      img.raycastTarget = false;
      RectTransform rt = barGo.GetComponent<RectTransform>();
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.zero;
      rt.pivot = new Vector2(0f, 0f);
      rt.anchoredPosition = new Vector2(31f + i * 28f, 30f);
      rt.sizeDelta = new Vector2(22f, heights[i]);
      _bars[i] = img;
    }

    _crossGo = new GameObject("Cross");
    _crossGo.transform.SetParent(_barsGo.transform, false);
    Stretch(_crossGo); // plain-Transform middlemen misplace children (R10)
    for (int i = 0; i < 2; i++) {
      GameObject slashGo = new GameObject("Slash" + i);
      slashGo.transform.SetParent(_crossGo.transform, false);
      Image slash = slashGo.AddComponent<Image>();
      slash.color = CrossRed;
      slash.raycastTarget = false;
      RectTransform rt = slashGo.GetComponent<RectTransform>();
      rt.anchorMin = Vector2.zero;
      rt.anchorMax = Vector2.zero;
      rt.pivot = new Vector2(0.5f, 0.5f);
      rt.anchoredPosition = new Vector2(70f, 67f);
      rt.sizeDelta = new Vector2(96f, 8f);
      rt.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
    }
    _crossGo.SetActive(false);

    _phoneGo = new GameObject("Headphone");
    _phoneGo.transform.SetParent(boxGo.transform, false);
    Image phoneImg = _phoneGo.AddComponent<Image>();
    phoneImg.sprite = MakeHeadphoneSprite();
    phoneImg.color = PhoneWhite;
    phoneImg.raycastTarget = false;
    RectTransform phoneRt = _phoneGo.GetComponent<RectTransform>();
    phoneRt.anchorMin = Vector2.zero;
    phoneRt.anchorMax = Vector2.zero;
    phoneRt.pivot = new Vector2(0f, 0f);
    phoneRt.anchoredPosition = new Vector2(38f, 70f);
    phoneRt.sizeDelta = new Vector2(64f, 64f);
    _phoneGo.SetActive(false);

    GameObject dotGo = new GameObject("DataDot");
    dotGo.transform.SetParent(boxGo.transform, false);
    _dot = dotGo.AddComponent<Image>();
    _dot.sprite = MakeDotSprite();
    _dot.color = DotRed;
    _dot.raycastTarget = false;
    _dotRt = dotGo.GetComponent<RectTransform>();
    PlaceDotUnder();
    _barsGo.SetActive(true);
  }

  void PlaceDotUnder() {
    _dotBeside = false;
    if (_dotRt == null) return;
    _dotRt.anchorMin = Vector2.zero;
    _dotRt.anchorMax = Vector2.zero;
    _dotRt.pivot = new Vector2(0f, 0f);
    _dotRt.anchoredPosition = new Vector2(62f, 6f);
    _dotRt.sizeDelta = new Vector2(16f, 16f);
  }

  void PlaceDotBeside() {
    _dotBeside = true;
    if (_dotRt == null) return;
    _dotRt.anchorMin = Vector2.zero;
    _dotRt.anchorMax = Vector2.zero;
    _dotRt.pivot = new Vector2(0f, 0f);
    _dotRt.anchoredPosition = new Vector2(110f, 94f);
    _dotRt.sizeDelta = new Vector2(16f, 16f);
  }

  static void Stretch(GameObject go) {
    RectTransform rt = go.GetComponent<RectTransform>();
    if (rt == null) rt = go.AddComponent<RectTransform>();
    rt.anchorMin = Vector2.zero;
    rt.anchorMax = Vector2.one;
    rt.offsetMin = Vector2.zero;
    rt.offsetMax = Vector2.zero;
  }

  // Filled disc (data dot), procedural — no imported assets.
  static Sprite MakeDotSprite() {
    const int size = 32;
    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    tex.wrapMode = TextureWrapMode.Clamp;
    Color clear = new Color(1f, 1f, 1f, 0f);
    Vector2 c = new Vector2(16f, 16f);
    for (int y = 0; y < size; y++) {
      for (int x = 0; x < size; x++) {
        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
        tex.SetPixel(x, y, Vector2.Distance(p, c) <= 15f ? Color.white : clear);
      }
    }
    tex.Apply();
    return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
  }

  // Headphone silhouette (band arc + two earcups), procedural.
  static Sprite MakeHeadphoneSprite() {
    const int size = 64;
    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    tex.wrapMode = TextureWrapMode.Clamp;
    Color clear = new Color(1f, 1f, 1f, 0f);
    Vector2 c = new Vector2(32f, 30f);
    for (int y = 0; y < size; y++) {
      for (int x = 0; x < size; x++) {
        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
        bool band = false;
        float d = Vector2.Distance(p, c);
        if (d >= 19f && d <= 25f && p.y <= 32f) band = true;
        bool cupL = p.x >= 5f && p.x <= 14f && p.y >= 24f && p.y <= 48f;
        bool cupR = p.x >= 50f && p.x <= 59f && p.y >= 24f && p.y <= 48f;
        tex.SetPixel(x, y, (band || cupL || cupR) ? Color.white : clear);
      }
    }
    tex.Apply();
    return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
  }
}
