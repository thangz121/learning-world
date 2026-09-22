// A_World/MarketHUD.cs — Agent A (World & Visual), W1 vertical slice.
// Child-friendly objective banner + replay-audio button, built fully in code
// (uGUI). Public API: ShowObjective(string) sets the banner text; the replay
// button invokes OnReplayPressed (Lead wires it to Milo.RepeatInstruction, so
// LWE.World never references the Brain assembly). No debug text, no
// technical language anywhere in the UI.
// ROLE (story hierarchy): the HUD is a minimal progress reminder ONLY
// ("Helping Mia…", "Apple found!", "Great job!"). It NEVER issues story
// instructions — NPC speech (Milo) + the world-anchored WorldQuestionBubble
// own the narrative/question presentation.
// Wiring: Bind(bus, quests). LWE.World does NOT reference LWE.Brain.
using System;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MarketHUD : BusBehaviour {
  public string CurrentObjective { get; private set; } = "Look around!";

  // Lead-wired replay action (Milo.RepeatInstruction). Null = button is inert.
  public Action OnReplayPressed;

  // Lead introspection (also keeps the injected quest service referenced).
  public IQuestService Quests {
    get { return _quests; }
  }

  IGameEventBus _bus;
  IQuestService _quests;
  bool _subscribed;

  Text _objectiveText;
  Button _replayButton;
  GameObject _replayButtonGo;
  CanvasGroup _fade; // adaptive hierarchy: the chip yields to emotional beats
  float _targetAlpha = 1f;
  // S3A transition cover (SceneBridge pattern ADAPTED: fade-to-black covers
  // the load/warp/unload beat, then lifts — no fake progress bar, the cover
  // is time-based while the load underneath stays a real awaited op).
  // Lives LAST in this same canvas (PersistentCore: survives scene unload, no
  // new roots, no new systems). raycastTarget=false: taps pass through.
  Image _coverImage;

  void Awake() {
    BuildUiImmediate();
  }

  // Deterministic build hook (tests/snapshot tools): builds the UI
  // synchronously instead of relying on Awake delivery, which batch EditMode
  // contexts do not guarantee (same pattern as BuildFaceImmediate).
  public void BuildUiImmediate() {
    if (_objectiveText != null) return;
    BuildUi();
  }

  // Injection boundary (wired by MarketBuilder.BuildServices + WireQuestService).
  // Either argument may be null (Lead wires quests in a second step); the HUD
  // degrades to a static banner instead of throwing.
  public void Bind(IGameEventBus bus, IQuestService quests) {
    _bus = bus;
    _quests = quests;
    EnsureSubscribed();
  }

  void OnEnable() {
    EnsureSubscribed();
  }

  CameraMode? _lastCamMode;

  void Update() {
    // Adaptive hierarchy (final polish): while the shared camera holds an
    // emotional/story beat (Interaction/Cinematic), the objective chip fades
    // to a whisper so NPC faces and reactions own the frame; Follow restores
    // it. Same-assembly read of the shared camera (no service lookup). The
    // fade snaps exactly on transitions (reads as intentional with the cut).
    Camera cam = Camera.main;
    SmartCamera smart = cam != null ? cam.GetComponent<SmartCamera>() : null;
    if (smart != null && (!_lastCamMode.HasValue || _lastCamMode.Value != smart.Mode)) {
      _lastCamMode = smart.Mode;
      ApplyCameraMode(smart.Mode);
    }
    UpdateTunnel();
  }

  // ---- B1R3 math tunnel (subject travel transition) ---------------------------
  // A fullscreen overlay of shrinking bead rings + drifting number/symbol
  // glyphs: the "space tunnel" reads as Math before the world appears. Built
  // lazily on the HUD, raycast-free, animated ONLY while playing (UpdateTunnel
  // early-outs when hidden). Public API: PlayTunnel()/StopTunnel().
  GameObject _tunnelGo;
  CanvasGroup _tunnelGroup;
  readonly System.Collections.Generic.List<RectTransform> _tunnelRings =
    new System.Collections.Generic.List<RectTransform>();
  readonly System.Collections.Generic.List<Text> _tunnelSymbols =
    new System.Collections.Generic.List<Text>();
  readonly System.Collections.Generic.List<float> _tunnelSymPhase =
    new System.Collections.Generic.List<float>();
  readonly System.Collections.Generic.List<float> _tunnelSymAngle =
    new System.Collections.Generic.List<float>();
  float _tunnelT;
  bool _tunnelOn;
  float _tunnelAlpha;

  static readonly Color TunnelGold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color TunnelBlue = new Color(0.25f, 0.45f, 0.85f);
  static readonly Color TunnelPink = new Color(0.93f, 0.45f, 0.62f);

  public bool HasTunnel { get { return _tunnelGo != null; } }
  public float TunnelAlpha { get { return _tunnelAlpha; } }

  public void PlayTunnel() {
    if (_tunnelGo == null) BuildUiImmediate();
    if (_tunnelGo == null) return;
    _tunnelOn = true;
    _tunnelT = 0f;
    _tunnelGo.SetActive(true);
  }

  public void StopTunnel() { _tunnelOn = false; }

  void UpdateTunnel() {
    if (_tunnelGo == null) return;
    float dt = Time.deltaTime;
    if (_tunnelOn) {
      _tunnelAlpha = Mathf.MoveTowards(_tunnelAlpha, 1f, dt / 0.18f);
      _tunnelT += dt;
    } else {
      _tunnelAlpha = Mathf.MoveTowards(_tunnelAlpha, 0f, dt / 0.22f);
      if (_tunnelAlpha <= 0f) {
        if (_tunnelGo.activeSelf) _tunnelGo.SetActive(false);
        return;
      }
    }
    if (_tunnelGroup != null) _tunnelGroup.alpha = _tunnelAlpha;
    for (int i = 0; i < _tunnelRings.Count; i++) {
      RectTransform rt = _tunnelRings[i];
      if (rt == null) continue;
      float phase = Mathf.Repeat(_tunnelT * 0.9f + i / (float)_tunnelRings.Count, 1f);
      float scale = Mathf.Lerp(0.10f, 2.2f, phase);
      rt.localScale = new Vector3(scale, scale, 1f);
      Image img = rt.GetComponent<Image>();
      if (img != null) {
        Color c = img.color;
        c.a = Mathf.Sin(Mathf.SmoothStep(0f, 1f, phase) * Mathf.PI);
        img.color = c;
      }
    }
    for (int i = 0; i < _tunnelSymbols.Count; i++) {
      Text t = _tunnelSymbols[i];
      if (t == null) continue;
      float phase = Mathf.Repeat(_tunnelT * 0.5f + _tunnelSymPhase[i], 1f);
      float radius = Mathf.Lerp(30f, 540f, Mathf.SmoothStep(0f, 1f, phase));
      float ang = _tunnelSymAngle[i] * Mathf.Deg2Rad + _tunnelT * 0.45f;
      t.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
      t.rectTransform.localRotation = Quaternion.Euler(0f, 0f, _tunnelT * 60f + i * 18f);
      Color c = t.color;
      c.a = Mathf.Sin(phase * Mathf.PI);
      t.color = c;
    }
  }

  void BuildTunnel(Font font) {
    GameObject canvasGo = new GameObject("MathTunnelCanvas");
    canvasGo.transform.SetParent(transform);
    Canvas canvas = canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 80; // above setup dialogs (50/70), below cursor (100)
    CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1280f, 720f);
    _tunnelGroup = canvasGo.AddComponent<CanvasGroup>();
    _tunnelGroup.alpha = 0f;
    _tunnelGroup.blocksRaycasts = false;
    _tunnelGroup.interactable = false;
    _tunnelGo = canvasGo;

    GameObject bgGo = new GameObject("TunnelBg");
    bgGo.transform.SetParent(canvasGo.transform, false);
    Image bg = bgGo.AddComponent<Image>();
    bg.color = new Color(0.05f, 0.07f, 0.16f, 1f);
    bg.raycastTarget = false;
    RectTransform bgRt = bgGo.GetComponent<RectTransform>();
    bgRt.anchorMin = Vector2.zero;
    bgRt.anchorMax = Vector2.one;
    bgRt.offsetMin = Vector2.zero;
    bgRt.offsetMax = Vector2.zero;

    Color[] ringCols = { TunnelGold, TunnelBlue, TunnelPink };
    for (int i = 0; i < 6; i++) {
      GameObject ringGo = new GameObject("TunnelRing" + i);
      ringGo.transform.SetParent(canvasGo.transform, false);
      Image ring = ringGo.AddComponent<Image>();
      ring.sprite = MakeRingSprite(256, 18, ringCols[i % ringCols.Length]);
      ring.raycastTarget = false;
      RectTransform rt = ringGo.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0.5f, 0.5f);
      rt.anchorMax = new Vector2(0.5f, 0.5f);
      rt.pivot = new Vector2(0.5f, 0.5f);
      rt.sizeDelta = new Vector2(360f, 360f);
      rt.anchoredPosition = Vector2.zero;
      _tunnelRings.Add(rt);
    }
    // LegacyRuntime glyph coverage only (the old ●/▲ rendered as broken
    // boxes on some machines — B1R4 smoothness round).
    string[] glyphs = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "+", "-", "=" };
    for (int i = 0; i < 16; i++) {
      GameObject symGo = new GameObject("TunnelSym" + i);
      symGo.transform.SetParent(canvasGo.transform, false);
      Text t = symGo.AddComponent<Text>();
      t.font = font;
      t.fontSize = 44 + (i % 3) * 16;
      t.alignment = TextAnchor.MiddleCenter;
      t.text = glyphs[i % glyphs.Length];
      t.color = ringCols[i % ringCols.Length];
      t.raycastTarget = false;
      RectTransform rt = symGo.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0.5f, 0.5f);
      rt.anchorMax = new Vector2(0.5f, 0.5f);
      rt.pivot = new Vector2(0.5f, 0.5f);
      rt.sizeDelta = new Vector2(140f, 90f);
      _tunnelSymbols.Add(t);
      _tunnelSymAngle.Add(i * (360f / 16f) + (i % 3) * 7f);
      _tunnelSymPhase.Add((i * 0.37f) % 1f);
    }
    _tunnelGo.SetActive(false);
  }

  static Sprite MakeRingSprite(int size, float thickness, Color color) {
    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    tex.wrapMode = TextureWrapMode.Clamp;
    float c = (size - 1) * 0.5f;
    float outer = size * 0.47f;
    float inner = outer - thickness;
    float aa = 1.6f; // soft edge: no jaggies when the ring upscales
    for (int y = 0; y < size; y++) {
      for (int x = 0; x < size; x++) {
        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
        float aOuter = Mathf.Clamp01((outer - d) / aa);
        float aInner = Mathf.Clamp01((d - inner) / aa);
        float a = Mathf.Min(aOuter, aInner);
        tex.SetPixel(x, y, new Color(color.r, color.g, color.b, a));
      }
    }
    tex.Apply();
    return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
  }

  // Deterministic driver for the fade (tests call this directly; Update polls
  // the live camera and forwards here). Snaps immediately so EditMode asserts
  // exact values; live play smooths via the Update lerp above.
  public void ApplyCameraMode(CameraMode mode) {
    _targetAlpha = (mode == CameraMode.Follow) ? 1f : 0.35f;
    if (_fade != null) _fade.alpha = _targetAlpha;
  }

  protected override void OnDisable() {
    _subscribed = false;
    base.OnDisable();
  }

  // S3A transition cover API (driven by MarketBootstrap travel/return).
  // Alpha 0 = invisible (normal play), 1 = full black (mid-transition).
  // Null-safe before BuildUi (tests call BuildUiImmediate first).
  public bool HasTransitionCover {
    get { return _coverImage != null; }
  }

  public float TransitionCoverAlpha {
    get { return _coverImage != null ? _coverImage.color.a : 0f; }
  }

  public void SetTransitionCover(float alpha) {
    if (_coverImage == null) return;
    if (alpha < 0f) alpha = 0f;
    if (alpha > 1f) alpha = 1f;
    Color c = _coverImage.color;
    c.a = alpha;
    _coverImage.color = c;
    _coverImage.enabled = alpha > 0.001f;
  }

  // Banner text entry point. Empty input keeps the previous line (kids never
  // see a blank banner); technical/debug strings are the caller's contract.
  public void ShowObjective(string text) {
    if (string.IsNullOrWhiteSpace(text)) return;
    CurrentObjective = text.Trim();
    if (_objectiveText != null) _objectiveText.text = CurrentObjective;
  }

  // Replay-button visibility gate (player-experience audit 2026-09-12): the
  // button is meaningless before any line has been spoken ("Hear WHAT
  // again?"), so it starts hidden and the story shows it with the first
  // instruction (MarketBootstrap.OnFirstTalk). Reusable for any captioned flow.
  public bool IsReplayVisible {
    get { return _replayButtonGo != null && _replayButtonGo.activeSelf; }
  }

  public void SetReplayVisible(bool visible) {
    if (_replayButtonGo != null) _replayButtonGo.SetActive(visible);
  }

  void EnsureSubscribed() {
    if (_subscribed || _bus == null || !isActiveAndEnabled) return;
    On<QuestStartedEvent>(OnQuestStarted, _bus);
    On<QuestCompletedEvent>(OnQuestCompleted, _bus);
    _subscribed = true;
  }

  void OnQuestStarted(QuestStartedEvent e) {
    // Intentionally NOT overwriting the chip: MarketBootstrap owns the exact
    // action wording ("Talk to Milo" pre-stage, then per-objective lines) and
    // sets it around StartQuest. A generic line here would flash over it.
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    ShowObjective("Great job!");
  }

  void HandleReplayButton() {
    try {
      if (OnReplayPressed != null) OnReplayPressed();
    } catch (Exception) {
      // Replay must never break the game for kids.
    }
  }

  // ---- code-built uGUI (overlay canvas, compact corner chip, replay button) ----
  // Final polish: the chip is deliberately SMALL (secondary reminder, never a
  // narrator) and adaptive (fades during emotional camera beats). The world
  // (NPC speech, name labels, question bubble) carries the story.

  void BuildUi() {
    GameObject canvasGo = new GameObject("MarketCanvas");
    canvasGo.transform.SetParent(transform);
    Canvas canvas = canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 10;
    _fade = canvasGo.AddComponent<CanvasGroup>();
    _fade.alpha = _targetAlpha;
    CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
    scaler.referenceResolution = new Vector2(1280f, 720f);
    canvasGo.AddComponent<GraphicRaycaster>();

    Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

    GameObject panelGo = new GameObject("ObjectivePanel");
    panelGo.transform.SetParent(canvasGo.transform);
    Image panel = panelGo.AddComponent<Image>();
    panel.sprite = MakeRoundedSprite(64, 18, new Color(1f, 0.96f, 0.87f));
    panel.type = Image.Type.Sliced;
    RectTransform panelRt = panelGo.GetComponent<RectTransform>();
    panelRt.anchorMin = new Vector2(0f, 1f);
    panelRt.anchorMax = new Vector2(0f, 1f);
    panelRt.pivot = new Vector2(0f, 1f);
    panelRt.anchoredPosition = new Vector2(20f, -20f);
    panelRt.sizeDelta = new Vector2(320f, 64f);
    if (MarketBuilder.HubSelectionOnly) {
      // Hub hall: the top-left chip sat on top of gate name boards (the raised
      // hub camera pushes boards high on screen). Park it bottom-center
      // instead — only lawn and feet project there, never labels.
      panelRt.anchorMin = new Vector2(0.5f, 0f);
      panelRt.anchorMax = new Vector2(0.5f, 0f);
      panelRt.pivot = new Vector2(0.5f, 0f);
      panelRt.anchoredPosition = new Vector2(0f, 20f);
    }

    GameObject textGo = new GameObject("ObjectiveText");
    textGo.transform.SetParent(panelGo.transform);
    _objectiveText = textGo.AddComponent<Text>();
    _objectiveText.font = font;
    _objectiveText.fontSize = 23;
    _objectiveText.color = new Color(0.35f, 0.22f, 0.12f);
    _objectiveText.alignment = MarketBuilder.HubSelectionOnly ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
    _objectiveText.verticalOverflow = VerticalWrapMode.Truncate;
    _objectiveText.text = CurrentObjective;
    RectTransform textRt = textGo.GetComponent<RectTransform>();
    textRt.anchorMin = Vector2.zero;
    textRt.anchorMax = Vector2.one;
    textRt.offsetMin = new Vector2(30f, 10f);
    textRt.offsetMax = new Vector2(-30f, -10f);

    GameObject buttonGo = new GameObject("ReplayButton");
    buttonGo.transform.SetParent(canvasGo.transform);
    Image buttonImage = buttonGo.AddComponent<Image>();
    buttonImage.sprite = MakeRoundedSprite(64, 22, new Color(0.35f, 0.65f, 0.95f));
    buttonImage.type = Image.Type.Sliced;
    _replayButton = buttonGo.AddComponent<Button>();
    _replayButton.onClick.AddListener(HandleReplayButton);
    _replayButtonGo = buttonGo;
    buttonGo.SetActive(false); // gated: shown with the first spoken instruction
    RectTransform buttonRt = buttonGo.GetComponent<RectTransform>();
    buttonRt.anchorMin = new Vector2(1f, 0f);
    buttonRt.anchorMax = new Vector2(1f, 0f);
    buttonRt.pivot = new Vector2(1f, 0f);
    buttonRt.anchoredPosition = new Vector2(-30f, 30f);
    buttonRt.sizeDelta = new Vector2(340f, 100f);

    GameObject labelGo = new GameObject("ReplayLabel");
    labelGo.transform.SetParent(buttonGo.transform);
    Text label = labelGo.AddComponent<Text>();
    label.font = font;
    label.fontSize = 32;
    label.color = Color.white;
    label.alignment = TextAnchor.MiddleCenter;
    label.text = "Hear it again";
    RectTransform labelRt = labelGo.GetComponent<RectTransform>();
    labelRt.anchorMin = Vector2.zero;
    labelRt.anchorMax = Vector2.one;
    labelRt.offsetMin = Vector2.zero;
    labelRt.offsetMax = Vector2.zero;

    // S3A transition cover: fullscreen black, LAST sibling (topmost), starts
    // disabled. Taps pass through (raycastTarget=false) so a mid-transition
    // tap still reaches the world underneath.
    GameObject coverGo = new GameObject("TransitionCover");
    coverGo.transform.SetParent(canvasGo.transform);
    _coverImage = coverGo.AddComponent<Image>();
    _coverImage.color = new Color(0f, 0f, 0f, 0f);
    _coverImage.raycastTarget = false;
    _coverImage.enabled = false;
    RectTransform coverRt = coverGo.GetComponent<RectTransform>();
    coverRt.anchorMin = Vector2.zero;
    coverRt.anchorMax = Vector2.one;
    coverRt.offsetMin = Vector2.zero;
    coverRt.offsetMax = Vector2.zero;
    coverGo.transform.SetAsLastSibling();

    // B1R3: the math tunnel lives on its own canvas (order 80) and is built
    // here so EditMode structure tests can drive it headlessly.
    BuildTunnel(font);

    // S3B dev-truth (pill investigation): one boot line proving the hierarchy
    // exists, is active, and carries text — batch-verifiable via Player.log
    // (no foreground needed). Players never see this line.
    try {
      Debug.Log("[MarketHUD] built canvas=" + canvasGo.activeInHierarchy
        + " panel=" + (panelGo.activeInHierarchy ? panelRt.sizeDelta.ToString() : "off")
        + " text=" + (_objectiveText != null ? ("'" + _objectiveText.text + "'") : "null")
        + " font=" + (_objectiveText != null && _objectiveText.font != null ? _objectiveText.font.name : "null")
        + " alpha=" + _targetAlpha
        + " cover=" + (_coverImage != null)
        + " tunnel=" + (_tunnelGo != null), this);
    } catch (System.Exception) { }
  }

  // Procedural rounded-rect sprite (no imported assets; 9-slice border set).
  static Sprite MakeRoundedSprite(int size, int radius, Color color) {
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
    return Sprite.Create(
      tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
      100f, 0u, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
  }
}
