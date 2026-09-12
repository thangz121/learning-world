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

  protected override void OnDisable() {
    _subscribed = false;
    base.OnDisable();
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
  // The objective chip is SECONDARY and minimal: small top-left corner, short
  // action text only ("Talk to Milo"). The world (NPC speech, name labels,
  // question bubble) carries the story, never this chip.

  void BuildUi() {
    GameObject canvasGo = new GameObject("MarketCanvas");
    canvasGo.transform.SetParent(transform);
    Canvas canvas = canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = 10;
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
    panelRt.anchoredPosition = new Vector2(24f, -24f);
    panelRt.sizeDelta = new Vector2(430f, 84f);

    GameObject textGo = new GameObject("ObjectiveText");
    textGo.transform.SetParent(panelGo.transform);
    _objectiveText = textGo.AddComponent<Text>();
    _objectiveText.font = font;
    _objectiveText.fontSize = 30;
    _objectiveText.color = new Color(0.35f, 0.22f, 0.12f);
    _objectiveText.alignment = TextAnchor.MiddleLeft;
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
