// A_World/DestinationMarker.cs — Agent A (World & Visual). Player-report
// follow-up: tapping the ground shows a gold PLUS (+) flat on the dirt where
// the child is walking to, until arrival (or a new tap / stop). One reusable
// object, collider-free (never eats the clicks it answers to), null-guarded.
// Polls ClickToMove (presentation reads movement state; movement never reads
// the marker). C# 9.0 only.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DestinationMarker : MonoBehaviour {
  public const float MarkY = 0.03f; // paint above path/grass, below soles
  public static readonly Color MarkColor = new Color(1f, 0.78f, 0.25f); // quest gold
  const float ArmLen = 0.32f;
  const float ArmThin = 0.07f;
  const float ArriveHideDist = 0.45f; // arrived: hide even before the mover clears

  ClickToMove _mover;
  Transform _playerT;
  GameObject _mark;

  void Awake() {
    BuildMarkImmediate();
  }

  // Deterministic build hook (tests drive this; Awake covers live play).
  public void BuildMarkImmediate() {
    if (_mark != null) return;
    _mark = new GameObject("DestinationCross");
    _mark.transform.SetParent(transform, false);
    Shader lit = Shader.Find("Universal Render Pipeline/Lit");
    Material mat = new Material(lit != null ? lit : Shader.Find("Standard"));
    try {
      if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", MarkColor);
      else if (mat.HasProperty("_Color")) mat.SetColor("_Color", MarkColor);
      if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);
      if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    } catch (Exception) { }
    AddArm("CrossArmX", new Vector3(ArmLen, 0.02f, ArmThin), mat);
    AddArm("CrossArmZ", new Vector3(ArmThin, 0.02f, ArmLen), mat);
    _mark.SetActive(false);
  }

  void AddArm(string armName, Vector3 size, Material mat) {
    GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
    arm.name = armName;
    arm.transform.SetParent(_mark.transform, false);
    arm.transform.localScale = size;
    Renderer r = arm.GetComponent<Renderer>();
    if (r != null) {
      r.sharedMaterial = mat;
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
      r.receiveShadows = false;
    }
    // CRITICAL: the marker answers clicks visually only — it must never
    // intercept the router's raycast (same contract as the hover marker).
    CharacterPresentation.DestroyNow(arm.GetComponent<Collider>());
  }

  public bool IsShowing {
    get { return _mark != null && _mark.activeSelf; }
  }

  // Test/live seam: explicit placement without a mover lookup.
  public void ShowAtForTests(Vector3 groundPoint) {
    if (_mark == null) BuildMarkImmediate();
    _mark.transform.position = new Vector3(groundPoint.x, MarkY, groundPoint.z);
    _mark.SetActive(true);
  }

  public void HideForTests() {
    if (_mark != null) _mark.SetActive(false);
  }

  void Update() {
    if (_mark == null) BuildMarkImmediate();
    if (!TryResolve(out Vector3 dest)) {
      if (_mark.activeSelf) _mark.SetActive(false);
      return;
    }
    _mark.transform.position = new Vector3(dest.x, MarkY, dest.z);
    if (!_mark.activeSelf) _mark.SetActive(true);
  }

  bool TryResolve(out Vector3 dest) {
    dest = Vector3.zero;
    try {
      if (_mover == null || _playerT == null) {
        GameObject player = GameObject.Find("Player");
        if (player == null) return false;
        _playerT = player.transform;
        _mover = player.GetComponent<ClickToMove>();
        if (_mover == null) return false;
      }
      if (!_mover.HasDestination) return false;
      Vector3 d = _mover.Destination;
      // Arrived (or already standing on it): no mark needed.
      if (Vector3.Distance(_playerT.position, d) <= ArriveHideDist) return false;
      dest = d;
      return true;
    } catch (Exception) {
      return false;
    }
  }
}
