// A_World/ButterflyDrift.cs — S7 FULL-BLOOM pass: pastel butterflies circling
// the flower drifts. Transform-only (no physics/colliders), the builder wires
// the two wings so they flap. Batch-safe: Update no-ops without wings.
// No gameplay, no click targets. C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class ButterflyDrift : MonoBehaviour {
  public Vector3 center = Vector3.zero;
  public float radius = 3f;
  public float speed = 0.5f;
  public float height = 1.4f;
  public float bobAmplitude = 0.25f;
  public float flapDeg = 55f;
  public float phase = 0f;

  Transform _wingL;
  Transform _wingR;
  float _t;

  public void Wire(Transform wingL, Transform wingR) {
    _wingL = wingL;
    _wingR = wingR;
  }

  void Update() {
    _t += Time.deltaTime * speed;
    float ang = (_t + phase) * Mathf.PI * 2f;
    transform.localPosition = center + new Vector3(
      Mathf.Cos(ang) * radius,
      height + Mathf.Sin(ang * 2f) * bobAmplitude,
      Mathf.Sin(ang) * radius);
    transform.localRotation = Quaternion.Euler(0f, -(ang * Mathf.Rad2Deg) + 90f, 0f);
    float flap = Mathf.Sin(Time.time * 14f + phase * 6f) * flapDeg;
    if (_wingL != null) _wingL.localRotation = Quaternion.Euler(0f, 0f, flap);
    if (_wingR != null) _wingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
  }
}
