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
  float _bobPhase;
  bool _shown = true;

  void Awake() {
    BuildBubble();
  }

  void Update() {
    if (!_shown) return;
    _bobPhase += Time.deltaTime * Mathf.PI * 2f * 0.5f;
    transform.position = _basePos + Vector3.up * (Mathf.Sin(_bobPhase) * 0.05f);
    Camera cam = Camera.main;
    if (cam == null) return;
    Vector3 toCam = cam.transform.position - transform.position;
    toCam.y = 0f;
    if (toCam.sqrMagnitude > 0.0001f)
      transform.rotation = Quaternion.LookRotation(toCam);
  }

  Vector3 _basePos;

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
    outline.transform.localScale = new Vector3(0.62f, 0.5f, 0.3f);
    outline.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.93f, 0.86f, 0.72f));
    CharacterPresentation.DestroyNow(outline.GetComponent<Collider>());
    GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    shell.name = "Shell";
    shell.transform.SetParent(_shell);
    shell.transform.localPosition = Vector3.zero;
    shell.transform.localScale = new Vector3(0.55f, 0.44f, 0.3f);
    shell.GetComponent<Renderer>().sharedMaterial = Lit(new Color(1f, 1f, 1f));
    CharacterPresentation.DestroyNow(shell.GetComponent<Collider>());

    // Tail: two small puffs descending toward the thinker (straight down reads
    // from every camera orbit, since the thinker is always below the bubble).
    AddPuff("TailPuff1", new Vector3(0f, -0.3f, 0f), 0.1f);
    AddPuff("TailPuff2", new Vector3(0f, -0.49f, 0f), 0.065f);

    // Icon: mini apple (future asks replace this child only). Stem + leaf use
    // the crate-apple language so it reads as APPLE, never a red dot.
    // Depth: the whole icon rides PROUD of the shell front plane (+0.15), so
    // the apple dome + stem + leaf all clear the shell face (R2 bug: the icon
    // sat at +0.10 with only a sliver peeking and read as a plain white ball
    // in close framings).
    GameObject icon = new GameObject("AskIcon");
    icon.transform.SetParent(transform);
    icon.transform.localPosition = new Vector3(0f, 0.02f, 0.17f);
    GameObject fruit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    fruit.name = "AskApple";
    fruit.transform.SetParent(icon.transform);
    fruit.transform.localPosition = Vector3.zero;
    fruit.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
    fruit.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.85f, 0.15f, 0.15f));
    CharacterPresentation.DestroyNow(fruit.GetComponent<Collider>());
    GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stem.name = "AskStem";
    stem.transform.SetParent(icon.transform);
    stem.transform.localPosition = new Vector3(0f, 0.13f, 0f);
    stem.transform.localScale = new Vector3(0.045f, 0.13f, 0.045f);
    stem.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.4f, 0.26f, 0.12f));
    CharacterPresentation.DestroyNow(stem.GetComponent<Collider>());
    GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    leaf.name = "AskLeaf";
    leaf.transform.SetParent(icon.transform);
    leaf.transform.localPosition = new Vector3(0.07f, 0.13f, 0f);
    leaf.transform.localScale = new Vector3(0.09f, 0.03f, 0.05f);
    leaf.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.25f, 0.6f, 0.25f));
    CharacterPresentation.DestroyNow(leaf.GetComponent<Collider>());
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
