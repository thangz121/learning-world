// A_World/CountingGarden/GardenZoneVignette.cs — S3 P2Y (user: "các khu chơi
// chưa có phân định... phải phân bổ ánh nhìn cho các khu khác"). Every bed gets
// a tiny always-on counting performance: its number-post beads pop in sequence
// (1..N) and the crops breathe, so the four skeleton plots draw the eye the
// same way the demo theatre does — motion without gameplay, bus, or services.
// Transform-only (no material instances), collider-free, seeded by phase.
// C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class GardenZoneVignette : MonoBehaviour {
  public float Phase;

  readonly List<Transform> _beads = new List<Transform>();
  readonly List<Transform> _crops = new List<Transform>();
  readonly List<Vector3> _cropHomes = new List<Vector3>();
  readonly List<Vector3> _beadScales = new List<Vector3>();

  public int BeadCount { get { return _beads.Count; } }
  public int CropCount { get { return _crops.Count; } }

  public void Bind(List<Transform> beads, List<Transform> crops) {
    _beads.Clear();
    _beadScales.Clear();
    if (beads != null) {
      foreach (Transform b in beads) {
        if (b == null) continue;
        _beads.Add(b);
        _beadScales.Add(b.localScale);
      }
    }
    _crops.Clear();
    _cropHomes.Clear();
    if (crops != null) {
      foreach (Transform c in crops) {
        if (c == null) continue;
        _crops.Add(c);
        _cropHomes.Add(c.localPosition);
      }
    }
  }

  void Update() {
    float t = Time.time + Phase;
    // Counting walk: one bead pops every 0.33s inside a 2.2s cycle.
    for (int i = 0; i < _beads.Count; i++) {
      Transform b = _beads[i];
      if (b == null) continue;
      float ph = Mathf.Repeat(t * 0.45f - i * 0.16f, 1.1f);
      float pop = ph < 0.28f ? Mathf.Sin(ph / 0.28f * Mathf.PI) : 0f;
      float s = 1f + 0.35f * pop;
      b.localScale = _beadScales[i] * s;
    }
    // Crops breathe gently (height only, positions stay pinned for tests).
    for (int i = 0; i < _crops.Count; i++) {
      Transform c = _crops[i];
      if (c == null) continue;
      Vector3 home = _cropHomes[i];
      c.localPosition = new Vector3(home.x,
        home.y + Mathf.Sin(t * 1.7f + i * 1.3f) * 0.018f, home.z);
    }
  }
}
