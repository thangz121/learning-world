// A_World/MicSetupDialog.cs — Agent A (World & Visual).
// Parent-facing microphone-setup modal, built fully in code (uGUI, same
// pattern as MarketHUD: ScreenSpaceOverlay canvas, builtin font, solid
// panels — no imported assets). Vietnamese wording (setup is operated by
// parents; the strings are consts so the content team can revise them in
// one place).
//
// Two panels, never both visible:
//   OFFER: "Hiện đang không có microphone kết nối, bạn có muốn kết nối
//          bằng điện thoại không?"  [Có, dùng điện thoại] [Bỏ qua]
//   WAIT:  3-step phone instructions + live status line
//          [Đã xong, kiểm tra lại] [Bỏ qua bài nghe]
// The dialog NEVER touches audio/mic APIs: the driver (MicSetupMonitor)
// owns the gate + probing and only calls Show*/SetStatus/Hide here.
// Hidden = SetActive(false), so it can never eat clicks when not in use.
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MicSetupDialog : MonoBehaviour {
  // Parent-facing copy (content-owned wording, single source here).
  public const string OfferTitle = "Microphone";
  public const string OfferBody =
    "Hiện đang không có microphone kết nối.\nBạn có muốn kết nối bằng điện thoại không?";
  public const string OfferAccept = "Có, dùng điện thoại";
  public const string OfferDecline = "Bỏ qua";
  public const string WaitTitle = "Kết nối bằng điện thoại";
  public const string WaitBody =
    "1. Trên máy tính: chạy phần mềm gateway (tools/start_phone_mic.ps1).\n" +
    "2. Mở trang QR trên máy tính, dùng điện thoại quét mã.\n" +
    "3. Trên điện thoại: nhấn START MIC + START CAMERA và cho phép dùng micro + camera.";
  public const string WaitRecheck = "Đã xong, kiểm tra lại";
  public const string WaitSkip = "Bỏ qua bài nghe";
  public const string StatusWaiting = "Đang chờ kết nối...";
  public const string StatusQrStarting =
    "Đang bật phần mềm gateway trên máy tính...";
  public const string StatusQrScan =
    "Dùng điện thoại quét mã QR, rồi nhấn START MIC và START CAMERA, cho phép dùng micro + camera.";
  public const string StatusQrManual =
    "Không tự bật được gateway. Hãy chạy tools/start_phone_mic.ps1 trên máy tính, rồi quét mã.";
  public const string StatusPhoneOpen =
    "Đã thấy điện thoại mở trang! Hãy nhấn START trên điện thoại.";
  public const string StatusPhoneStopped =
    "Điện thoại đã dừng. Nhấn START lại trên điện thoại để nói tiếp.";
  public const string StatusPhoneLost =
    "Điện thoại đã ngắt kết nối. Mở lại trang và nhấn START để nối lại.";
  public const string StatusLinked = "Đã thấy điện thoại! Vào game thôi.";
  public const string StatusGatewayDown =
    "Chưa thấy phần mềm gateway trên máy tính. Hãy chạy gateway rồi nhấn kiểm tra lại.";
  public const string StatusNoPhone =
    "Đã thấy gateway. Hãy quét QR và nhấn START trên điện thoại, rồi kiểm tra lại.";

  GameObject _root;
  GameObject _offerPanel;
  GameObject _waitPanel;
  Text _waitStatus;
  // In-game QR (auto-started gateway): hidden until SetQrImage succeeds.
  // Late Skip (30 s rule): the wait panel's secondary button starts hidden.
  RawImage _waitQr;
  GameObject _waitSkipGo;

  Action _onAccept;
  Action _onDecline;
  Action _onRecheck;
  Action _onSkip;

  void Awake() {
    BuildUiImmediate();
  }

  // Deterministic build hook (same pattern as MarketHUD.BuildUiImmediate):
  // tests/snapshot tools build synchronously instead of relying on Awake.
  public void BuildUiImmediate() {
    if (_root != null) return;
    BuildUi();
  }

  public bool IsShowing => _root != null && _root.activeSelf
    && ((_offerPanel != null && _offerPanel.activeSelf)
      || (_waitPanel != null && _waitPanel.activeSelf));

  public bool IsOfferShowing => _root != null && _root.activeSelf
    && _offerPanel != null && _offerPanel.activeSelf;

  public bool IsWaitShowing => _root != null && _root.activeSelf
    && _waitPanel != null && _waitPanel.activeSelf;

  public void ShowOffer(Action onAccept, Action onDecline) {
    BuildUiImmediate();
    _onAccept = onAccept;
    _onDecline = onDecline;
    _root.SetActive(true);
    _offerPanel.SetActive(true);
    _waitPanel.SetActive(false);
  }

  public void ShowWait(string status, Action onRecheck, Action onSkip) {
    BuildUiImmediate();
    _onRecheck = onRecheck;
    _onSkip = onSkip;
    _root.SetActive(true);
    _offerPanel.SetActive(false);
    _waitPanel.SetActive(true);
    ClearQr();
    SetSkipVisible(false); // 30 s rule: skip appears late (monitor owns the timer)
    SetWaitStatus(status);
  }

  public void SetWaitStatus(string status) {
    if (_waitStatus != null)
      _waitStatus.text = string.IsNullOrEmpty(status) ? StatusWaiting : status;
  }

  // Shows the auto-started gateway QR (PNG bytes read from the gateway's
  // --qr-png file). False = undecodable/missing (caller keeps manual text).
  public bool SetQrImage(byte[] png) {
    try {
      if (_waitQr == null || png == null || png.Length < 8) return false;
      var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
      if (!tex.LoadImage(png)) {
        try { UnityEngine.Object.Destroy(tex); } catch (Exception) { }
        return false;
      }
      ClearQrTexture();
      _waitQr.texture = tex;
      _waitQr.gameObject.SetActive(true);
      return true;
    } catch (Exception) { return false; }
  }

  public bool IsQrShowing => _waitQr != null && _waitQr.gameObject.activeSelf;

  public void ClearQr() {
    try {
      ClearQrTexture();
      if (_waitQr != null) _waitQr.gameObject.SetActive(false);
    } catch (Exception) { }
  }

  void ClearQrTexture() {
    try {
      if (_waitQr != null && _waitQr.texture != null) {
        // EditMode-safe: Destroy() in edit mode logs an error (P15T); the
        // player path (isPlaying) keeps the frame-safe Destroy.
        try {
          if (Application.isPlaying) UnityEngine.Object.Destroy(_waitQr.texture);
          else UnityEngine.Object.DestroyImmediate(_waitQr.texture);
        } catch (Exception) { }
        _waitQr.texture = null;
      }
    } catch (Exception) { }
  }

  // Late-skip rule: hidden at show, revealed by the monitor after 30 s.
  public void SetSkipVisible(bool visible) {
    try { if (_waitSkipGo != null) _waitSkipGo.SetActive(visible); } catch (Exception) { }
  }

  public bool IsSkipVisible => _waitSkipGo != null && _waitSkipGo.activeSelf;

  public void Hide() {
    if (_root != null) _root.SetActive(false);
    ClearQr();
    _onAccept = _onDecline = _onRecheck = _onSkip = null;
  }

  // ---- code-built uGUI ------------------------------------------------------
  void BuildUi() {
    Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    _root = new GameObject("MicSetupRoot");
    _root.transform.SetParent(transform, false);
    Canvas canvas = _root.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 50; // above the HUD chip (10), below nothing
    CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1280f, 720f);
    _root.AddComponent<GraphicRaycaster>();

    GameObject dimGo = new GameObject("Dim");
    dimGo.transform.SetParent(_root.transform, false);
    Image dim = dimGo.AddComponent<Image>();
    dim.color = new Color(0f, 0f, 0f, 0.55f);
    Stretch(dimGo);

    GameObject offerSkip;
    _offerPanel = BuildPanel("OfferPanel", OfferTitle, OfferBody,
      OfferAccept, OfferDecline,
      () => SafeInvoke(_onAccept), () => SafeInvoke(_onDecline), font, null, 460f, out offerSkip);
    GameObject waitSkip;
    _waitPanel = BuildPanel("WaitPanel", WaitTitle, WaitBody,
      WaitRecheck, WaitSkip,
      () => SafeInvoke(_onRecheck), () => SafeInvoke(_onSkip), font, t => _waitStatus = t, 640f, out waitSkip);
    _waitSkipGo = waitSkip;
    _waitSkipGo.SetActive(false);
    // QR image: 140px square in the free band between the body text (ends
    // ~330 from card top) and the status line (~478): full-size 180px QRs
    // covered body line 3 + the status (R10 photo proof). Must stay inside
    // 340..480 from card top = -160..-20 around the middle (bottom < top,
    // P15 inverted-offset rule).
    GameObject qrGo = new GameObject("QrImage");
    qrGo.transform.SetParent(_waitPanel.transform, false);
    _waitQr = qrGo.AddComponent<RawImage>();
    _waitQr.color = Color.white;
    Place(qrGo, 0.5f, 0.5f, 0.5f, 0.5f, -70f, -160f, 70f, -20f);
    qrGo.SetActive(false);
    _root.SetActive(false);
  }

  // Builds one centered card; statusOut receives the status Text when the
  // panel needs a live status line (WAIT), else null (OFFER). skipOut
  // receives the secondary ("skip") button for the late-skip rule.
  GameObject BuildPanel(string name, string title, string body,
      string primary, string secondary,
      Action onPrimary, Action onSecondary, Font font, Action<Text> statusOut,
      float cardH, out GameObject skipOut) {
    skipOut = null;
    GameObject panelGo = new GameObject(name);
    panelGo.transform.SetParent(_root.transform, false);
    Image panel = panelGo.AddComponent<Image>();
    panel.color = new Color(1f, 0.97f, 0.90f, 1f);
    RectTransform rt = panelGo.GetComponent<RectTransform>();
    rt.anchorMin = new Vector2(0.5f, 0.5f);
    rt.anchorMax = new Vector2(0.5f, 0.5f);
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.sizeDelta = new Vector2(640f, cardH);
    rt.anchoredPosition = Vector2.zero;

    GameObject titleGo = new GameObject("Title");
    titleGo.transform.SetParent(panelGo.transform, false);
    Text titleT = titleGo.AddComponent<Text>();
    titleT.font = font;
    titleT.fontSize = 40;
    titleT.color = new Color(0.35f, 0.22f, 0.12f);
    titleT.alignment = TextAnchor.MiddleCenter;
    titleT.text = title;
    Place(titleGo, 0f, 1f, 1f, 1f, 24f, -88f, -24f, -24f);

    GameObject bodyGo = new GameObject("Body");
    bodyGo.transform.SetParent(panelGo.transform, false);
    Text bodyT = bodyGo.AddComponent<Text>();
    bodyT.font = font;
    bodyT.fontSize = 26;
    bodyT.color = new Color(0.25f, 0.18f, 0.10f);
    bodyT.alignment = TextAnchor.UpperLeft;
    bodyT.verticalOverflow = VerticalWrapMode.Overflow;
    bodyT.text = body;
    Place(bodyGo, 0f, 1f, 1f, 1f, 40f, -330f, -40f, -104f);

    float statusH = statusOut != null ? 44f : 0f;
    if (statusOut != null) {
      GameObject stGo = new GameObject("Status");
      stGo.transform.SetParent(panelGo.transform, false);
      Text st = stGo.AddComponent<Text>();
      st.font = font;
      st.fontSize = 24;
      st.fontStyle = FontStyle.Italic;
      st.color = new Color(0.15f, 0.35f, 0.15f);
      st.alignment = TextAnchor.MiddleCenter;
      st.text = StatusWaiting;
      Place(stGo, 0f, 0f, 1f, 0f, 24f, 118f, -24f, 118f + statusH);
      statusOut(st);
    }

    GameObject pGo = MakeButton(primary, new Color(0.20f, 0.55f, 0.30f), font);
    pGo.transform.SetParent(panelGo.transform, false);
    pGo.GetComponent<Button>().onClick.AddListener(() => onPrimary());
    Place(pGo, 0f, 0f, 0.5f, 0f, 24f, 32f, -12f, 96f);

    GameObject sGo = MakeButton(secondary, new Color(0.55f, 0.55f, 0.58f), font);
    sGo.transform.SetParent(panelGo.transform, false);
    sGo.GetComponent<Button>().onClick.AddListener(() => onSecondary());
    Place(sGo, 0.5f, 0f, 1f, 0f, 12f, 32f, -24f, 96f);
    if (statusOut != null) skipOut = sGo; // WAIT only: late-skip rule owns this

    panelGo.SetActive(false);
    return panelGo;
  }

  static GameObject MakeButton(string label, Color color, Font font) {
    GameObject go = new GameObject("Button");
    Image img = go.AddComponent<Image>();
    img.color = color;
    Button btn = go.AddComponent<Button>();
    GameObject lGo = new GameObject("Label");
    lGo.transform.SetParent(go.transform, false);
    Text l = lGo.AddComponent<Text>();
    l.font = font;
    l.fontSize = 24;
    l.color = Color.white;
    l.alignment = TextAnchor.MiddleCenter;
    l.text = label;
    Stretch(lGo);
    return go;
  }

  // Anchored placement inside the card. Offsets are Unity RectTransform
  // offsets: for bottom-anchored rects BOTTOM < TOP numerically
  // (e.g. bottom=32, top=96), for top-anchored both negative with
  // bottom < top (e.g. bottom=-330, top=-104). Inverted pairs collapse
  // the rect (buttons/status silently vanish) — see P15 milestone fix.
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
