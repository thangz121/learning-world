// A_World/WorldQuestionBubble.cs — Agent A (World & Visual). Reusable
// world-anchored visual question: a floating thought bubble (white shell +
// icon prop) that shows WHAT an NPC is asking, next to the NPC — never a
// fixed HUD quiz panel. W1 icon: mini apple (built from primitives, same
// language as the crate apple). Future asks swap the icon child only.
// API: Show()/Hide(). Gentle bob + Y-only billboard (stable, no tilt).
// Quest phases drive it (MarketBootstrap); it owns no rules. C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class WorldQuestionBubble : MonoBehaviour {
  Transform _shell;
  Transform _icon; // R6: gentle attention pulse (scale only, no new objects)
  float _bobPhase;
  float _pulsePhase;
  bool _shown = true;

  void Awake() {
    BuildBubble();
  }

  void Update() {
    if (!_shown) return;
    _bobPhase += Time.deltaTime * Mathf.PI * 2f * 0.5f;
    transform.position = _basePos + Vector3.up * (Mathf.Sin(_bobPhase) * 0.05f);
    // R6 quest-hint motion (FINAL POLISH): the apple icon breathes gently
    // (R9: 1 +/- 0.08 at ~1Hz) so a 4yo's eye is drawn without arcade flashing.
    // Shell stays rock-steady (only the icon pulses, never the whole bubble).
    if (_icon != null) {
      _pulsePhase += Time.deltaTime * Mathf.PI * 2f * 1f;
      float k = 1f + 0.08f * Mathf.Sin(_pulsePhase);
      _icon.transform.localScale = new Vector3(k, k, k);
    }
    Camera cam = Camera.main;
    if (cam == null) return;
    Vector3 toCam = cam.transform.position - transform.position;
    toCam.y = 0f;
    if (toCam.sqrMagnitude > 0.0001f)
      transform.rotation = Quaternion.LookRotation(toCam);
  }

  Vector3 _basePos;

  // R9 hint-placement CONTRACT (player report: the hint must read instantly,
  // current + future NPCs all follow this one rule): the bubble parks EAST-
  // SOUTH of the asker at head height (1.78m), 1.45m east — never over the
  // 2.35m name label, never over the face/body, clear of awning volumes.
  // MarketBuilder places Mia's bubble through this; future NPCs call it too.
  // Icon-first thought language carries the meaning before any text is read.
  public static Vector3 AnchorFor(Vector3 npcPos) {
    return npcPos + new Vector3(1.45f, 1.78f, 0.55f);
  }

  // Phase 2E icon contract (closes 2B P1-5: the icon was hardcoded apple).
  // SetIcon rebuilds ONLY the icon child from a word-driven spec table —
  // target changes, icon changes, no per-word code after this point:
  // "apple" -> red fruit + stem + leaf (R9 language, unchanged);
  // "ball" -> BIG ORANGE quest ball + white band (player report: the old blue
  // sphere was identical to the distractor — now color/size/band all differ);
  // anything else -> neutral thought dot (never apple by accident).
  // Existing Apple appearance is byte-identical (CT-P09 pins names/sizes).
  public void SetIcon(WordId target) {
    if (_icon == null) {
      // Order-safe: composition roots may set the icon before first build
      // (same pattern as WorldNameLabel.Setup building on demand).
      if (_shell == null) BuildBubbleImmediate();
      if (_icon == null) return;
    }
    DestroyIconChildren(_icon);
    BuildIconContent(_icon, SpecFor(target.Value));
  }

  public string CurrentIconId {
    get { return _iconId; }
  }

  string _iconId = "apple";

  struct IconSpec {
    public string fruitName;
    public Vector3 fruitScale;
    public Color fruitColor;
    public bool stem;
    public bool leaf;
    public bool band; // white equatorial stripe (quest-ball identity)
  }

  static IconSpec SpecFor(string word) {
    string w = (word ?? "").Trim().ToLowerInvariant();
    if (w == "apple") {
      return new IconSpec {
        fruitName = "AskApple",
        fruitScale = new Vector3(0.30f, 0.30f, 0.30f),
        fruitColor = new Color(0.85f, 0.15f, 0.15f),
        stem = true, leaf = true,
      };
    }
    if (w == "ball") {
      return new IconSpec {
        fruitName = "AskBall",
        fruitScale = new Vector3(0.34f, 0.34f, 0.34f),
        fruitColor = new Color(1.0f, 0.55f, 0.10f), // quest orange (matches the crate ball)
        stem = false, leaf = false, band = true,
      };
    }
    return new IconSpec {
      fruitName = "AskUnknown",
      fruitScale = new Vector3(0.26f, 0.26f, 0.26f),
      fruitColor = new Color(0.85f, 0.75f, 0.55f),
      stem = false, leaf = false, band = false,
    };
  }

  void DestroyIconChildren(Transform icon) {
    for (int i = icon.childCount - 1; i >= 0; i--) {
      CharacterPresentation.DestroyNow(icon.GetChild(i).gameObject);
    }
  }

  // Placement entry point (MarketBuilder positions it beside the stall so the
  // awning never occludes it). Remembers base for the bob.
  public void Place(Vector3 worldPos) {
    _basePos = worldPos;
    transform.position = worldPos;
  }

  public void Show() {
    _shown = true;
    gameObject.SetActive(true);
  }

  public void Hide() {
    _shown = false;
    gameObject.SetActive(false);
  }

  void BuildBubble() {
    BuildBubbleImmediate();
  }

  // Deterministic build hook (tests/snapshot tools): same pattern as
  // BuildUiImmediate/BuildFaceImmediate — batch EditMode does not guarantee
  // Awake delivery.
  public void BuildBubbleImmediate() {
    if (_shell != null) return;
    _shell = new GameObject("BubbleShell").transform;
    _shell.SetParent(transform);
    _shell.localPosition = Vector3.zero;
    // Thought-bubble language (final polish): a SMALL white shell with a soft
    // cream outline reads as "someone is thinking this" instead of a floating
    // debug ball. Icon sits proud of the shell face (+z, toward the viewer).
    // Layering: the outline sits BEHIND the shell front plane (z -0.08 with a
    // shallower depth) so it renders as a halo rim, never swallowing the shell
    // (R1 bug: coplanar fronts z-fought and the tan ball ate the shell).
    GameObject outline = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    outline.name = "ShellOutline";
    outline.transform.SetParent(_shell);
    outline.transform.localPosition = new Vector3(0f, 0f, -0.08f);
    // R6 readability (FINAL POLISH): shell +18% so the apple icon reads at
    // normal gameplay distance (~5m). Names/shapes unchanged (CT-P03C).
    // R9 (player report: hint must read INSTANTLY): shell +30% more, icon
    // +25%, pulse 6% -> 8%. Still a thought (outline + tail untouched in
    // language), just impossible to miss. CT-P03C pins names, not sizes.
    outline.transform.localScale = new Vector3(0.95f, 0.77f, 0.36f);
    outline.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.93f, 0.86f, 0.72f));
    CharacterPresentation.DestroyNow(outline.GetComponent<Collider>());
    GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    shell.name = "Shell";
    shell.transform.SetParent(_shell);
    shell.transform.localPosition = Vector3.zero;
    shell.transform.localScale = new Vector3(0.85f, 0.68f, 0.36f);
    shell.GetComponent<Renderer>().sharedMaterial = Lit(new Color(1f, 1f, 1f));
    CharacterPresentation.DestroyNow(shell.GetComponent<Collider>());

    // Tail: two small puffs descending toward the thinker (straight down reads
    // from every camera orbit, since the thinker is always below the bubble).
    AddPuff("TailPuff1", new Vector3(0f, -0.40f, 0f), 0.125f);
    AddPuff("TailPuff2", new Vector3(0f, -0.60f, 0f), 0.08f);

    // Icon: the ASKED thing in crate/prop language (2E: built from the spec
    // table via SetIcon's path, so apple/ball/future share one builder).
    // Depth: the whole icon rides PROUD of the shell front plane (+0.15), so
    // the apple dome + stem + leaf all clear the shell face (R2 bug: the icon
    // sat at +0.10 with only a sliver peeking and read as a plain white ball
    // in close framings).
    GameObject icon = new GameObject("AskIcon");
    icon.transform.SetParent(transform);
    icon.transform.localPosition = new Vector3(0f, 0.03f, 0.20f);
    _icon = icon.transform; // R6: pulse driver (Update), structure unchanged
    _iconId = "apple";
    BuildIconContent(_icon, SpecFor("apple"));
  }

  // Single icon builder (shared by the default build and SetIcon): fruit
  // sphere in the target's world language + stem/leaf only when the spec
  // says so (apple has them, ball/fallback don't).
  void BuildIconContent(Transform icon, IconSpec spec) {
    _iconId = spec.fruitName == "AskApple" ? "apple"
      : spec.fruitName == "AskBall" ? "ball" : "unknown";
    GameObject fruit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    fruit.name = spec.fruitName;
    fruit.transform.SetParent(icon);
    fruit.transform.localPosition = Vector3.zero;
    // R6: apple +20% (0.20 -> 0.24) — the ICON is hierarchy level 1, it must
    // read before any text. Stem/leaf scale with it (same apple language).
    fruit.transform.localScale = spec.fruitScale;
    fruit.GetComponent<Renderer>().sharedMaterial = Lit(spec.fruitColor);
    CharacterPresentation.DestroyNow(fruit.GetComponent<Collider>());
    if (spec.band) {
      GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      band.name = "AskBand";
      band.transform.SetParent(fruit.transform);
      band.transform.localPosition = Vector3.zero;
      band.transform.localScale = new Vector3(1.04f, 0.12f, 1.04f);
      band.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.96f, 0.96f, 0.97f));
      CharacterPresentation.DestroyNow(band.GetComponent<Collider>());
      return;
    }
    if (!spec.stem && !spec.leaf) return;
    if (spec.stem) {
      GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      stem.name = "AskStem";
      stem.transform.SetParent(icon);
      stem.transform.localPosition = new Vector3(0f, 0.19f, 0f);
      stem.transform.localScale = new Vector3(0.068f, 0.19f, 0.068f);
      stem.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.4f, 0.26f, 0.12f));
      CharacterPresentation.DestroyNow(stem.GetComponent<Collider>());
    }
    if (spec.leaf) {
      GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      leaf.name = "AskLeaf";
      leaf.transform.SetParent(icon.transform);
      leaf.transform.localPosition = new Vector3(0.105f, 0.19f, 0f);
      leaf.transform.localScale = new Vector3(0.135f, 0.045f, 0.075f);
      leaf.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.25f, 0.6f, 0.25f));
      CharacterPresentation.DestroyNow(leaf.GetComponent<Collider>());
    }
    if (spec.band) {
      GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      band.name = "AskBand";
      band.transform.SetParent(icon.transform);
      band.transform.localPosition = Vector3.zero;
      band.transform.localScale = new Vector3(1.04f, 0.12f, 1.04f);
      band.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.96f, 0.96f, 0.97f));
      CharacterPresentation.DestroyNow(band.GetComponent<Collider>());
    }
  }

  void AddPuff(string puffName, Vector3 localPos, float size) {
    GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    puff.name = puffName;
    puff.transform.SetParent(transform);
    puff.transform.localPosition = localPos;
    puff.transform.localScale = new Vector3(size, size, size);
    puff.GetComponent<Renderer>().sharedMaterial = Lit(new Color(1f, 1f, 1f));
    CharacterPresentation.DestroyNow(puff.GetComponent<Collider>());
  }

  static Material Lit(Color color) {
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    return mat;
  }
}
