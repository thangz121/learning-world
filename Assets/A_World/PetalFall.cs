// A_World/PetalFall.cs — S7 FULL-BLOOM pass (user round: "đẩy tới nóc").
// A loose swirl of pink petals drifting over the hub: transform-only
// animation (no physics, no colliders, bake-ignored by the builder), seeded
// deterministic layout from WorldBeauty. Batch/EditMode-safe: Update is a
// no-op until Collect() sees the petals (the builder calls it right after).
// No gameplay, no click targets. C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class PetalFall : MonoBehaviour {
  public float fallSpeed = 0.5f;
  public float swayAmplitude = 0.35f;
  public float swaySpeed = 0.9f;
  public float spinSpeed = 70f;
  public float topY = 4.4f;
  public float minY = 0.15f;

  Transform[] _petals;
  Vector3[] _base;
  float[] _phase;

  public void Collect() {
    int n = transform.childCount;
    _petals = new Transform[n];
    _base = new Vector3[n];
    _phase = new float[n];
    for (int i = 0; i < n; i++) {
      _petals[i] = transform.GetChild(i);
      _base[i] = _petals[i].localPosition;
      _phase[i] = i * 0.7f;
    }
  }

  void Update() {
    if (_petals == null || _petals.Length == 0) return;
    float dt = Time.deltaTime;
    for (int i = 0; i < _petals.Length; i++) {
      Transform p = _petals[i];
      if (p == null) continue;
      Vector3 pos = p.localPosition;
      pos.y -= fallSpeed * dt;
      if (pos.y < minY) pos.y = topY;
      pos.x = _base[i].x + Mathf.Sin(Time.time * swaySpeed + _phase[i]) * swayAmplitude;
      pos.z = _base[i].z + Mathf.Cos(Time.time * swaySpeed * 0.8f + _phase[i]) * swayAmplitude * 0.6f;
      p.localPosition = pos;
      p.localRotation = Quaternion.Euler(40f, Time.time * spinSpeed + _phase[i] * 60f, 25f);
    }
  }
}
