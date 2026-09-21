// A_World/MathWorld/MathBeacon.cs — Phase 3.0.x S4. Quest-target attention.
// Gentle bob + slow spin on the counting cube so the child knows where to
// look/go without a wall of text (preschool readability). Motion-only cue:
// no materials touched (shared Lit cache stays shared), no colliders added,
// no gameplay state (the Interactable + carrier own meaning). Dies with its
// GameObject (cube hides on find — beacon hides with it). C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class MathBeacon : MonoBehaviour {
  public float BobAmplitude = 0.08f;
  public float BobHertz = 0.6f;
  public float SpinDegPerSec = 40f;

  Vector3 _basePos;
  bool _ready;

  void Awake() {
    _basePos = transform.localPosition;
    _ready = true;
  }

  void Update() {
    if (!_ready) return;
    float t = Time.time * BobHertz * Mathf.PI * 2f;
    transform.localPosition = _basePos + new Vector3(0f, Mathf.Sin(t) * BobAmplitude, 0f);
    transform.localRotation *= Quaternion.Euler(0f, SpinDegPerSec * Time.deltaTime, 0f);
  }
}
