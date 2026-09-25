// A_World/BuildYard/BuildYardGateHint.cs — S3-P2Z14 GAMEPLAY #4.
// The Build Gate's gentle interaction cue (brief §2): when the child walks into
// the approach radius, a soft gold glow breathes over the threshold pad. It is
// presentation only — the MicroWorldPortal owns the walk-in trigger and its
// cold-start debounce; this component never moves, never takes input, never
// shows UI, and every piece is collider-free + bake-ignored so the hub's
// reviewed walk surface is untouched.
// C# 9.0 only.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildYardGateHint : MonoBehaviour {
  public MicroWorldPortal Portal;
  public float ApproachRadius = 5.0f;
  public float GlowDiameter = 3.1f;
  public Transform Glow { get; private set; }

  float _near;

  // Built by MathWorldBuilder at the portal spot; creates its own glow disc so
  // the shared hub material cache stays untouched (no material edits).
  public void Build(MicroWorldPortal portal) {
    Portal = portal;
    GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    glow.name = "BuildYardGateGlow";
    glow.transform.SetParent(transform, false);
    glow.transform.localPosition = new Vector3(0f, 0.035f, 0f);
    glow.transform.localScale = new Vector3(GlowDiameter, 0.012f, GlowDiameter);
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    Color gold = new Color(1f, 0.88f, 0.42f);
    mat.SetColor("_BaseColor", gold);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    if (mat.HasProperty("_EmissionColor")) {
      mat.EnableKeyword("_EMISSION");
      mat.SetColor("_EmissionColor", gold * 0.55f);
    }
    mat.enableInstancing = true;
    glow.GetComponent<Renderer>().sharedMaterial = mat;
    try {
      Collider c = glow.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (Exception) { }
    try {
      Unity.AI.Navigation.NavMeshModifier mod = glow.AddComponent<Unity.AI.Navigation.NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (Exception) { }
    glow.SetActive(false);
    Glow = glow.transform;
  }

  // Pure proximity seam (EditMode-coverable): 1 inside the approach radius.
  public float NearForTests(Vector3 playerPos) {
    Vector3 d = playerPos - transform.position;
    d.y = 0f;
    return d.magnitude <= ApproachRadius ? 1f : 0f;
  }

  void Update() {
    ClickToMove player = Portal != null && Portal.BuildArea != null
      ? Portal.BuildArea.Player : null;
    float want = player != null ? NearForTests(player.transform.position) : 0f;
    _near = Mathf.MoveTowards(_near, want, Time.deltaTime / 0.35f);
    ApplyGlow();
  }

  void ApplyGlow() {
    if (Glow == null) return;
    bool visible = _near > 0.01f;
    if (Glow.gameObject.activeSelf != visible) Glow.gameObject.SetActive(visible);
    if (!visible) return;
    float breathe = 1f + 0.05f * Mathf.Sin(Time.time * 3.2f);
    Glow.localScale = new Vector3(GlowDiameter * _near * breathe, 0.012f,
      GlowDiameter * _near * breathe);
  }
}
