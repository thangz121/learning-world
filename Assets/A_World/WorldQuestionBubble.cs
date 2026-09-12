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
    _shell = new GameObject("BubbleShell").transform;
    _shell.SetParent(transform);
    _shell.localPosition = Vector3.zero;
    GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    shell.name = "Shell";
    shell.transform.SetParent(_shell);
    shell.transform.localPosition = Vector3.zero;
    shell.transform.localScale = new Vector3(0.7f, 0.55f, 0.4f);
    shell.GetComponent<Renderer>().sharedMaterial = Lit(new Color(1f, 1f, 1f));
    Destroy(shell.GetComponent<Collider>());

    // Icon: mini apple (future asks replace this child only).
    GameObject icon = new GameObject("AskIcon");
    icon.transform.SetParent(transform);
    icon.transform.localPosition = new Vector3(0f, 0.02f, 0.12f);
    GameObject fruit = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    fruit.name = "AskApple";
    fruit.transform.SetParent(icon.transform);
    fruit.transform.localPosition = Vector3.zero;
    fruit.transform.localScale = new Vector3(0.26f, 0.26f, 0.26f);
    fruit.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.85f, 0.15f, 0.15f));
    Destroy(fruit.GetComponent<Collider>());
    GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stem.name = "AskStem";
    stem.transform.SetParent(icon.transform);
    stem.transform.localPosition = new Vector3(0f, 0.17f, 0f);
    stem.transform.localScale = new Vector3(0.05f, 0.15f, 0.05f);
    stem.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.4f, 0.26f, 0.12f));
    Destroy(stem.GetComponent<Collider>());
  }

  static Material Lit(Color color) {
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    return mat;
  }
}
