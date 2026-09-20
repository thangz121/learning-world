// A_World/WorldNameLabel.cs — Agent A (World & Visual). Reusable in-world NPC
// identity tag: a small world-space name label floating above a character
// ("Milo", "Mia", future chapter NPCs). World + NPC presentation stays
// PRIMARY for identification; the HUD never names characters.
// Attached by the composition root (MarketBootstrap), never by Brain
// presenters (LWE.Brain cannot reference LWE.World). API: Setup(name, follow,
// height) + Show()/Hide() (Hide keeps Mia unemphasized until the story
// introduces her). Y-only billboard, stable, no tilt. Null-guarded,
// batch-safe (no Camera -> no rotation work). C# 9.0 only.
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class WorldNameLabel : MonoBehaviour {
  Transform _follow;
  float _height = 2f;
  Text _text;
  // Locked mode (hub beauty round): the text is PAINTED onto a physical
  // board — fixed yaw, never billboards. Real signboards don't rotate to
  // face you; the old always-face pill read as a black box floating in air
  // (user report). Same font/shrink contract as floating labels.
  bool _locked;

  void Awake() {
    BuildLabel();
  }

  void Update() {
    if (_locked) return;
    if (_follow != null) transform.position = _follow.position + Vector3.up * _height;
    Camera cam = Camera.main;
    if (cam == null) return;
    // Player-experience audit 2026-09-12 (W1Audit m-label/g-mia/k-correct,
    // two player builds with settled framings): uGUI world-space text shows
    // its legible face toward -Z, so conventional LookRotation(cam - pos)
    // presents the mirrored back ("oliM"/"siM" in every close-up). Point +Z
    // away from the camera instead: the legible face then tracks the viewer.
    Vector3 awayFromCam = transform.position - cam.transform.position;
    awayFromCam.y = 0f;
    if (awayFromCam.sqrMagnitude > 0.0001f)
      transform.rotation = BillboardRotation(transform.position, cam.transform.position);
  }

  // Pure Y-billboard used by Update (extracted for EditMode regression cover:
  // CT-S01M pins the legible-face-toward-camera contract without rendering).
  public static Quaternion BillboardRotation(Vector3 labelPos, Vector3 camPos) {
    Vector3 away = labelPos - camPos;
    away.y = 0f;
    if (away.sqrMagnitude <= 0.0001f) return Quaternion.identity;
    return Quaternion.LookRotation(away);
  }

  // Placement entry point. Follow is the gameplay root (stable; head bobs).
  // Self-sufficient: builds the label on demand instead of trusting Awake
  // ordering (composition roots call Setup immediately after AddComponent in
  // scene-load/test contexts where Awake delivery is not guaranteed).
  public void Setup(string displayName, Transform follow, float heightAboveRoot) {
    if (_text == null) BuildLabel();
    _follow = follow;
    _locked = false;
    _height = heightAboveRoot;
    if (_text != null && !string.IsNullOrWhiteSpace(displayName)) {
      string clean = displayName.Trim();
      _text.text = clean;
      // Fit guarantee (user round: Vietnamese subject names must show FULLY,
      // never wrap/truncate): the 600px pill @112pt fits ~9 chars on one
      // line; longer names ("Tiếng Việt" 10, "Về sảnh chính" 12) step down
      // to 88pt so they stay on a single uncut line. Same LegacyRuntime
      // font (Vietnamese diacritics proven in player builds), same pill.
      _text.fontSize = clean.Length > 9 ? 88 : 112;
    }
    if (_follow != null) transform.position = _follow.position + Vector3.up * _height;
  }

  public string CurrentName {
    get { return _text != null ? _text.text : ""; }
  }

  // Locked setup: paint the name onto a physical board at worldPos, facing
  // viewerPos ONCE (same legible-face convention as the billboard math, so
  // the proven font rendering carries over). No follow, no rotation after.
  public void SetupLocked(string displayName, Vector3 worldPos, Vector3 viewerPos) {
    if (_text == null) BuildLabel();
    _follow = null;
    _locked = true;
    if (_text != null && !string.IsNullOrWhiteSpace(displayName)) {
      string clean = displayName.Trim();
      _text.text = clean;
      _text.fontSize = clean.Length > 9 ? 88 : 112;
    }
    transform.position = worldPos;
    transform.rotation = BillboardRotation(worldPos, viewerPos);
  }

  public void Show() {
    gameObject.SetActive(true);
  }

  public void Hide() {
    gameObject.SetActive(false);
  }

  void BuildLabel() {
    // R8 zoom detail (player report: zoom-in must not lose detail): the canvas
    // renders at 2x texel density (600x192 @ 112pt, root scale halved) for the
    // IDENTICAL 1.2m world size — close-up name tags stay crisp instead of
    // going blocky. Faces/props are geometry (infinite zoom); text was the
    // only resolution-bound detail on the NPCs.
    transform.localScale = Vector3.one * 0.002f;
    GameObject canvasGo = new GameObject("LabelCanvas");
    canvasGo.transform.SetParent(transform, false);
    Canvas canvas = canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.WorldSpace;
    RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
    canvasRt.sizeDelta = new Vector2(600f, 192f);
    Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    // High-contrast pill (reusable identity cue): the old bare cream text
    // washed out against sky/awning at gameplay distance. Dark backing +
    // white text reads on any backdrop; sized for a 2-4m viewing distance.
    GameObject pillGo = new GameObject("LabelPill");
    pillGo.transform.SetParent(canvasGo.transform, false);
    Image pill = pillGo.AddComponent<Image>();
    pill.sprite = MakeRoundedSprite(48, 14, new Color(0.2f, 0.12f, 0.08f, 1f));
    pill.type = Image.Type.Sliced;
    RectTransform pillRt = pillGo.GetComponent<RectTransform>();
    pillRt.anchorMin = Vector2.zero;
    pillRt.anchorMax = Vector2.one;
    pillRt.offsetMin = Vector2.zero;
    pillRt.offsetMax = Vector2.zero;
    GameObject textGo = new GameObject("LabelText");
    textGo.transform.SetParent(canvasGo.transform, false);
    _text = textGo.AddComponent<Text>();
    _text.font = font;
    _text.fontSize = 112;
    _text.color = Color.white;
    _text.alignment = TextAnchor.MiddleCenter;
    Shadow shadow = textGo.AddComponent<Shadow>();
    shadow.effectColor = new Color(0.25f, 0.16f, 0.1f, 0.85f);
    shadow.effectDistance = new Vector2(6f, -6f);
    RectTransform textRt = textGo.GetComponent<RectTransform>();
    textRt.anchorMin = Vector2.zero;
    textRt.anchorMax = Vector2.one;
    textRt.offsetMin = Vector2.zero;
    textRt.offsetMax = Vector2.zero;
  }

  // Procedural rounded-rect sprite (no imported assets; mirrors MarketHUD).
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
