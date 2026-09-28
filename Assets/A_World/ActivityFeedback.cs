// A_World/ActivityFeedback.cs — S3-P2Z34 (user: "UX/UI ở mức cao nhất", P2).
// A SHARED screen-space feedback layer used by every play activity: a compact
// round-progress row and a big, legible correct/retry banner. Self-contained
// uGUI built in code (same discipline as MarketHUD), a single persistent
// instance created on demand, taps pass through (every graphic
// raycastTarget=false). No font-glyph risk: dots are tinted Images, the banner
// is plain text in both languages. ReduceMotion removes the pop.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ActivityFeedback : MonoBehaviour {
  public enum Kind { Correct, Retry, Info }

  static readonly Color Fill = new Color(0.36f, 0.80f, 0.45f);
  static readonly Color Empty = new Color(1f, 1f, 1f, 0.30f);
  static readonly Color CorrectTint = new Color(0.35f, 0.85f, 0.45f);
  static readonly Color RetryTint = new Color(0.98f, 0.74f, 0.30f);
  static readonly Color InfoTint = new Color(0.55f, 0.82f, 0.98f);

  static ActivityFeedback _instance;

  RectTransform _progressRow;
  Text _banner;
  Outline _bannerOutline;
  readonly List<Image> _dots = new List<Image>();

  float _bannerT, _bannerDur;
  float _progressHold;

  public static ActivityFeedback Ensure() {
    if (_instance != null) return _instance;
    GameObject go = new GameObject("ActivityFeedbackOverlay");
    if (Application.isPlaying) DontDestroyOnLoad(go);
    _instance = go.AddComponent<ActivityFeedback>();
    _instance.Build();
    return _instance;
  }

  // Test/teardown: drop the singleton and its UI.
  public static void ResetForTests() {
    if (_instance != null) {
      try { CharacterPresentation.DestroyNow(_instance.gameObject); } catch (System.Exception) { }
    }
    _instance = null;
  }

  // ---- public verbs ----------------------------------------------------------

  public static void Correct(string text = null) {
    Banner(text != null ? text : DialogueLang.T("GOOD!", "GIỎI!"), Kind.Correct, 1.3f);
  }

  public static void Retry(string text = null) {
    Banner(text != null ? text : DialogueLang.T("TRY AGAIN", "LÀM LẠI NHÉ"), Kind.Retry, 1.5f);
  }

  public static void Banner(string text, Kind kind, float seconds) {
    if (string.IsNullOrWhiteSpace(text)) return;
    ActivityFeedback f = Ensure();
    f.ShowBanner(text, kind, seconds);
  }

  // Round progress. total <= 0 hides the row (free-play). done is clamped.
  public static void Progress(int done, int total) {
    ActivityFeedback f = Ensure();
    f.ShowProgress(done, total);
  }

  public static void Clear() {
    if (_instance == null) return;
    _instance.ShowProgress(0, 0);
    _instance._bannerT = 0f;
    _instance._bannerDur = 0f;
    if (_instance._banner != null) SetBannerAlpha(_instance._banner, 0f);
  }

  public static bool VisibleForTests { get { return _instance != null; } }

  // ---- build -----------------------------------------------------------------

  Canvas _canvas;

  void Build() {
    if (_canvas != null) return;
    GameObject canvasGo = new GameObject("ActivityFeedbackCanvas");
    canvasGo.transform.SetParent(transform, false);
    _canvas = canvasGo.AddComponent<Canvas>();
    _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    _canvas.sortingOrder = 78; // above the in-hub chip, below tunnel (80)/cursor (100)
    CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1920f, 1080f);
    scaler.matchWidthOrHeight = 0.5f;

    Font font = UiFont.Get();

    // Progress row (top-center).
    GameObject rowGo = new GameObject("ProgressRow");
    rowGo.transform.SetParent(canvasGo.transform, false);
    _progressRow = rowGo.AddComponent<RectTransform>();
    _progressRow.anchorMin = new Vector2(0.5f, 1f);
    _progressRow.anchorMax = new Vector2(0.5f, 1f);
    _progressRow.pivot = new Vector2(0.5f, 1f);
    _progressRow.anchoredPosition = new Vector2(0f, -54f);
    HorizontalLayoutGroup layout = rowGo.AddComponent<HorizontalLayoutGroup>();
    layout.spacing = 14f;
    layout.childAlignment = TextAnchor.MiddleCenter;
    layout.childForceExpandWidth = false;
    layout.childForceExpandHeight = false;
    _progressRow.gameObject.SetActive(false);

    // Banner (centre, a little above the middle so it never covers the child).
    GameObject bannerGo = new GameObject("Banner");
    bannerGo.transform.SetParent(canvasGo.transform, false);
    RectTransform br = bannerGo.AddComponent<RectTransform>();
    br.anchorMin = new Vector2(0.5f, 0.5f);
    br.anchorMax = new Vector2(0.5f, 0.5f);
    br.pivot = new Vector2(0.5f, 0.5f);
    br.anchoredPosition = new Vector2(0f, 150f);
    br.sizeDelta = new Vector2(1200f, 200f);
    _banner = bannerGo.AddComponent<Text>();
    _banner.font = font;
    _banner.fontSize = 96;
    _banner.fontStyle = FontStyle.Bold;
    _banner.alignment = TextAnchor.MiddleCenter;
    _banner.raycastTarget = false;
    _banner.supportRichText = true;
    _bannerOutline = bannerGo.AddComponent<Outline>();
    _bannerOutline.effectColor = new Color(0.10f, 0.12f, 0.16f, 0.9f);
    _bannerOutline.effectDistance = new Vector2(3f, -3f);
    SetBannerAlpha(_banner, 0f);
  }

  static void SetBannerAlpha(Text t, float a) {
    if (t == null) return;
    Color c = t.color;
    c.a = a;
    t.color = c;
  }

  // ---- behaviour -------------------------------------------------------------

  void ShowBanner(string text, Kind kind, float seconds) {
    if (_banner == null) return;
    _banner.text = text;
    _banner.color = TintFor(kind);
    _bannerT = 0f;
    _bannerDur = Mathf.Max(0.2f, seconds);
    _banner.transform.localScale = GameJuice.ReduceMotion ? Vector3.one : Vector3.one * 0.7f;
    SetBannerAlpha(_banner, 1f);
  }

  static Color TintFor(Kind kind) {
    switch (kind) {
      case Kind.Correct: return CorrectTint;
      case Kind.Retry: return RetryTint;
      default: return InfoTint;
    }
  }

  void ShowProgress(int done, int total) {
    if (_progressRow == null) return;
    if (total <= 0) { _progressRow.gameObject.SetActive(false); return; }
    _progressRow.gameObject.SetActive(true);
    _progressHold = 6f;
    while (_dots.Count < total) {
      GameObject dotGo = new GameObject("Dot" + _dots.Count);
      dotGo.transform.SetParent(_progressRow, false);
      LayoutElement le = dotGo.AddComponent<LayoutElement>();
      le.preferredWidth = 22f; le.preferredHeight = 22f;
      Image img = dotGo.AddComponent<Image>();
      img.raycastTarget = false;
      _dots.Add(img);
    }
    for (int i = 0; i < _dots.Count; i++) {
      _dots[i].gameObject.SetActive(i < total);
      _dots[i].color = (i < done) ? Fill : Empty;
    }
  }

  void Update() { Tick(Time.deltaTime); }

  public void Tick(float dt) {
    if (_bannerT < _bannerDur && _banner != null && dt > 0f) {
      _bannerT += dt;
      float p = Mathf.Clamp01(_bannerT / _bannerDur);
      // pop-in over the first 18%, then hold and fade out over the last 30%.
      float scale = 1f;
      if (!GameJuice.ReduceMotion && p < 0.18f) scale = Mathf.Lerp(0.7f, 1f, p / 0.18f);
      _banner.transform.localScale = Vector3.one * scale;
      float a = p < 0.7f ? 1f : (1f - (p - 0.7f) / 0.3f);
      SetBannerAlpha(_banner, Mathf.Clamp01(a));
      if (_bannerT >= _bannerDur) SetBannerAlpha(_banner, 0f);
    }
    if (_progressHold > 0f) {
      _progressHold -= dt;
      if (_progressHold <= 0f && _progressRow != null) _progressRow.gameObject.SetActive(false);
    }
  }

  public int DotCountForTests { get { return _dots.Count; } }
  public Color DotColorForTests(int i) { return (i >= 0 && i < _dots.Count) ? _dots[i].color : Color.clear; }
  public bool ProgressVisibleForTests { get { return _progressRow != null && _progressRow.gameObject.activeSelf; } }
  public string BannerTextForTests { get { return _banner != null ? _banner.text : ""; } }
  public float BannerAlphaForTests { get { return _banner != null ? _banner.color.a : 0f; } }
}
