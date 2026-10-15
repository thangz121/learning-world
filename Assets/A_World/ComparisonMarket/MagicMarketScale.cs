// A_World/ComparisonMarket/MagicMarketScale.cs — The Magic Seesaw Balance Scale.
// Visually represents More, Less, and Equal with damped spring physics for preschoolers.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class MagicMarketScale : MonoBehaviour {
  public Transform BeamRoot;
  public Transform LeftPan;
  public Transform RightPan;
  public Transform Pointer;

  float _currentAngle;
  float _targetAngle;
  float _velocity;
  const float SpringK = 38f;
  const float Damping = 7.5f;

  public float CurrentAngle => _currentAngle;
  public float TargetAngle => _targetAngle;

  public void SetTargetAngle(float angleDeg) {
    _targetAngle = Mathf.Clamp(angleDeg, -12f, 12f);
  }

  public void SnapToTarget(float angleDeg) {
    _targetAngle = Mathf.Clamp(angleDeg, -12f, 12f);
    _currentAngle = _targetAngle;
    _velocity = 0f;
    ApplyRotation();
  }

  void Update() {
    // NOTE: driven by ComparisonMarket.Tick (single source of truth). An internal Update()
    // here would double-rate the spring physics during play. Keep Tick() public for tests.
  }

  public void Tick(float dt) {
    if (dt <= 0f) return;
    float dtClamped = Mathf.Min(dt, 0.05f);
    float force = (_targetAngle - _currentAngle) * SpringK - _velocity * Damping;
    _velocity += force * dtClamped;
    _currentAngle += _velocity * dtClamped;
    ApplyRotation();
  }

  void ApplyRotation() {
    if (BeamRoot != null) {
      BeamRoot.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);
    }
    if (Pointer != null) {
      Pointer.localRotation = Quaternion.Euler(0f, 0f, -_currentAngle * 1.5f);
    }
    // Keep pans upright regardless of beam tilt
    if (LeftPan != null) {
      LeftPan.localRotation = Quaternion.Euler(0f, 0f, -_currentAngle);
    }
    if (RightPan != null) {
      RightPan.localRotation = Quaternion.Euler(0f, 0f, -_currentAngle);
    }
  }

  public static MagicMarketScale Build(Transform parent, Vector3 centerLocal, Vector3 slotA, Vector3 slotB) {
    GameObject root = new GameObject("CMMagicScale");
    root.transform.SetParent(parent, false);
    root.transform.localPosition = centerLocal;

    MagicMarketScale scale = root.AddComponent<MagicMarketScale>();

    // 1. Fulcrum Base (Chân đế đồng mộc)
    GameObject fulcrum = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    fulcrum.name = "FulcrumBase";
    fulcrum.transform.SetParent(root.transform, false);
    fulcrum.transform.localPosition = new Vector3(0f, 0.28f, 0f);
    fulcrum.transform.localScale = new Vector3(0.72f, 0.28f, 0.72f);
    SetLitMat(fulcrum, new Color(0.68f, 0.52f, 0.28f));
    Strip(fulcrum);

    // Fulcrum Column
    GameObject col = GameObject.CreatePrimitive(PrimitiveType.Cube);
    col.name = "FulcrumPillar";
    col.transform.SetParent(root.transform, false);
    col.transform.localPosition = new Vector3(0f, 0.62f, 0f);
    col.transform.localScale = new Vector3(0.24f, 0.65f, 0.24f);
    SetLitMat(col, new Color(0.55f, 0.40f, 0.22f));
    Strip(col);

    // Balance Dial (Mặt đồng hồ cân bằng)
    GameObject dial = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    dial.name = "BalanceDial";
    dial.transform.SetParent(root.transform, false);
    dial.transform.localPosition = new Vector3(0f, 0.95f, -0.1f);
    dial.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    dial.transform.localScale = new Vector3(0.48f, 0.04f, 0.48f);
    SetLitMat(dial, new Color(0.98f, 0.88f, 0.60f));
    Strip(dial);

    // 2. Beam Pivot Root
    GameObject beamRoot = new GameObject("BeamRoot");
    beamRoot.transform.SetParent(root.transform, false);
    beamRoot.transform.localPosition = new Vector3(0f, 0.88f, 0f);
    scale.BeamRoot = beamRoot.transform;

    // Horizontal Beam Bar (Thanh đòn cân dài)
    float span = Mathf.Abs(slotB.x - slotA.x);
    GameObject beamBar = GameObject.CreatePrimitive(PrimitiveType.Cube);
    beamBar.name = "BeamBar";
    beamBar.transform.SetParent(beamRoot.transform, false);
    beamBar.transform.localPosition = Vector3.zero;
    beamBar.transform.localScale = new Vector3(span + 0.4f, 0.08f, 0.12f);
    SetLitMat(beamBar, new Color(0.72f, 0.56f, 0.30f));
    Strip(beamBar);

    // Pointer (Kim chỉ vàng)
    GameObject pointer = GameObject.CreatePrimitive(PrimitiveType.Cube);
    pointer.name = "Pointer";
    pointer.transform.SetParent(beamRoot.transform, false);
    pointer.transform.localPosition = new Vector3(0f, 0.14f, -0.12f);
    pointer.transform.localScale = new Vector3(0.04f, 0.28f, 0.02f);
    SetLitMat(pointer, new Color(0.96f, 0.28f, 0.25f));
    Strip(pointer);
    scale.Pointer = pointer.transform;

    // 3. Left and Right Hanging Pans (Đĩa cân vàng bóng)
    scale.LeftPan = CreateHangingPan(beamRoot.transform, new Vector3(slotA.x - centerLocal.x, 0f, 0f), "PanLeft");
    scale.RightPan = CreateHangingPan(beamRoot.transform, new Vector3(slotB.x - centerLocal.x, 0f, 0f), "PanRight");

    return scale;
  }

  static Transform CreateHangingPan(Transform parent, Vector3 localPos, string name) {
    GameObject panGo = new GameObject(name);
    panGo.transform.SetParent(parent, false);
    panGo.transform.localPosition = localPos;

    // Chords (dây treo đĩa cân)
    for (int i = 0; i < 2; i++) {
      float xOff = (i == 0 ? -0.55f : 0.55f);
      GameObject cord = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      cord.name = "Cord" + i;
      cord.transform.SetParent(panGo.transform, false);
      cord.transform.localPosition = new Vector3(xOff * 0.5f, -0.36f, 0f);
      cord.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 18f : -18f);
      cord.transform.localScale = new Vector3(0.02f, 0.38f, 0.02f);
      SetLitMat(cord, new Color(0.85f, 0.75f, 0.45f));
      Strip(cord);
    }

    // Dish (Đĩa cân tròn)
    GameObject dish = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    dish.name = "Dish";
    dish.transform.SetParent(panGo.transform, false);
    dish.transform.localPosition = new Vector3(0f, -0.72f, 0f);
    dish.transform.localScale = new Vector3(1.75f, 0.035f, 1.75f);
    SetLitMat(dish, new Color(0.95f, 0.82f, 0.40f));
    Strip(dish);

    return panGo.transform;
  }

  static void Strip(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (System.Exception) { }
  }

  static void SetLitMat(GameObject go, Color color) {
    Renderer r = go.GetComponent<Renderer>();
    if (r == null) return;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);
    r.sharedMaterial = mat;
  }
}
