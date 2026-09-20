// A_World/NatureSway.cs — "the world is alive" micro-motion for soft foliage.
// Single-component sine lean (deterministic phase from world position, no RNG
// state, no allocation). Grass/flowers/plants only — never trees/props.
// Amplitude ~1 degree: readable life, zero gameplay distraction.
using UnityEngine;

[DisallowMultipleComponent]
public class NatureSway : MonoBehaviour {
  public float amplitudeDeg = 1.1f;
  public float speed = 1.0f;

  float _phase;
  Quaternion _base;

  void Start() {
    _base = transform.rotation;
    float p = transform.position.x * 12.9898f + transform.position.z * 78.233f;
    _phase = p - Mathf.Floor(p / 6.28318f) * 6.28318f;
  }

  void Update() {
    float lean = Mathf.Sin(Time.time * speed + _phase) * amplitudeDeg;
    transform.rotation = _base * Quaternion.Euler(lean * 0.4f, 0f, lean);
  }
}
