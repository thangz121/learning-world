// CT-P45: Math Hub + 10 micro-world gates (LW P3.0.1 hub phase). Owner: A.
// Headless: BuildContent + query hierarchy. Pins the hub contract:
// 10 distinct skeleton gates, anchors, spacing off walking spokes,
// collider-free dressing, labels, registry lookup, object budget.
// Journey/quest pins stay in P40-P43 (untouched). C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CT_P45_MathHubGates {
  GameObject _root;
  MathWorldBuilder _builder;

  void SetUp() {
    _root = new GameObject("P45MathWorld");
    _builder = _root.AddComponent<MathWorldBuilder>();
    _builder.BuildContent(_root.transform);
    try { Physics.SyncTransforms(); } catch (System.Exception) { }
  }

  void TearDown() {
    if (_root != null) Object.DestroyImmediate(_root);
    _root = null;
    _builder = null;
  }

  static void CollectByPrefix(Transform t, string prefix, List<GameObject> outList) {
    if (t == null) return;
    if (t.name.StartsWith(prefix)) outList.Add(t.gameObject);
    for (int i = 0; i < t.childCount; i++) CollectByPrefix(t.GetChild(i), prefix, outList);
  }

  static float DistToSegXZ(Vector3 p, Vector3 a, Vector3 b) {
    Vector3 ab = new Vector3(b.x - a.x, 0f, b.z - a.z);
    Vector3 ap = new Vector3(p.x - a.x, 0f, p.z - a.z);
    float len2 = ab.x * ab.x + ab.z * ab.z;
    float t = len2 < 1e-6f ? 0f : Mathf.Clamp01((ap.x * ab.x + ap.z * ab.z) / len2);
    float dx = ap.x - ab.x * t;
    float dz = ap.z - ab.z * t;
    return Mathf.Sqrt(dx * dx + dz * dz);
  }

  [Test] public void P45A_TenGatesMatchCatalog() {
    SetUp();
    try {
      Assert.AreEqual(10, _builder.MicroGates.Count, "exactly 10 gate skeletons");
      Assert.AreEqual(MicroWorldCatalog.All.Length, _builder.MicroGates.Count);
      var ids = new HashSet<string>();
      var names = new HashSet<string>();
      for (int i = 0; i < MicroWorldCatalog.All.Length; i++) {
        MicroWorldGate g = _builder.MicroGates[i];
        Assert.IsNotNull(g, "gate " + i + " built");
        Assert.AreEqual(MicroWorldCatalog.All[i].Id, g.gateId, "gate order follows catalog");
        Assert.AreEqual(MicroWorldCatalog.All[i].VnName, g.displayName);
        Assert.IsTrue(ids.Add(g.gateId), "duplicate gate id: " + g.gateId);
        Assert.IsTrue(names.Add(g.displayName), "duplicate gate name: " + g.displayName);
      }
    } finally { TearDown(); }
  }

  [Test] public void P45B_GateAnchorsAndLookup() {
    SetUp();
    try {
      foreach (MicroWorldGate g in _builder.MicroGates) {
        Assert.IsNotNull(g.EntryAnchor, g.gateId + " entry anchor");
        Assert.IsNotNull(g.ExitAnchor, g.gateId + " exit anchor");
        Assert.IsNotNull(g.LabelAnchor, g.gateId + " label anchor");
        Assert.AreNotEqual(g.EntryAnchor.position, g.ExitAnchor.position,
          g.gateId + " entry/exit distinct");
      }
      Assert.AreSame(_builder.MicroGates[0],
        _builder.FindMicroGate("counting_garden"), "registry lookup");
      Assert.AreSame(_builder.MicroGates[9],
        _builder.FindMicroGate("MEMORY_GROVE"), "lookup case-insensitive");
      Assert.IsNull(_builder.FindMicroGate("nope"), "miss returns null");
      Assert.IsNull(_builder.FindMicroGate(null), "null returns null");
    } finally { TearDown(); }
  }

  [Test] public void P45C_GatesSpacedOffSpokes() {
    SetUp();
    try {
      Vector3[][] spokes = {
        new Vector3[] { new Vector3(0f, 0f, -12f), new Vector3(0f, 0f, 0f) },
        new Vector3[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 12f) },
        new Vector3[] { new Vector3(0f, 0f, 0f), new Vector3(-16f, 0f, 5f) },
        new Vector3[] { new Vector3(0f, 0f, 0f), new Vector3(15.5f, 0f, -5f) },
      };
      foreach (MicroWorldGate g in _builder.MicroGates) {
        Vector3 p = g.transform.localPosition;
        Assert.LessOrEqual(Mathf.Abs(p.x), 25.5f, g.gateId + " inside island (x)");
        Assert.LessOrEqual(Mathf.Abs(p.z), 25.5f, g.gateId + " inside island (z)");
        foreach (Vector3[] s in spokes) {
          float d = DistToSegXZ(p, s[0], s[1]);
          Assert.GreaterOrEqual(d, 2.0f, g.gateId + " breathes off the walking spoke");
        }
      }
      // Gate-to-gate breathing room (footprints r~1.7 must not overlap).
      for (int i = 0; i < _builder.MicroGates.Count; i++)
        for (int j = i + 1; j < _builder.MicroGates.Count; j++) {
          Vector3 a = _builder.MicroGates[i].transform.localPosition;
          Vector3 b = _builder.MicroGates[j].transform.localPosition;
          Assert.Greater(Vector3.Distance(a, b), 3.4f,
            _builder.MicroGates[i].gateId + " vs " + _builder.MicroGates[j].gateId);
        }
    } finally { TearDown(); }
  }

  [Test] public void P45D_GatesColliderFreeAndLabeled() {
    SetUp();
    try {
      var parts = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathGate", parts);
      Assert.Greater(parts.Count, 60, "gate dressing exists to be checked");
      var offenders = new List<string>();
      foreach (GameObject go in parts) {
        if (go.GetComponent<Collider>() != null) offenders.Add(go.name);
        if (go.GetComponent<GraphicRaycaster>() != null) offenders.Add(go.name + "+raycaster");
      }
      Assert.AreEqual(0, offenders.Count,
        "gate collider/click-eater (skeleton must be walk-to): " + string.Join(",", offenders.ToArray()));
      // One styled pill label per gate, carrying the catalog VN name.
      WorldNameLabel[] labels = _root.GetComponentsInChildren<WorldNameLabel>(true);
      foreach (MicroWorldCatalog.Entry e in MicroWorldCatalog.All) {
        bool found = false;
        foreach (WorldNameLabel l in labels) {
          if (l != null && l.CurrentName == e.VnName) { found = true; break; }
        }
        Assert.IsTrue(found, "gate label present: " + e.VnName);
      }
      // Landmark present (hub north star).
      Assert.IsNotNull(GameObject.Find("MathGateLandmark"), "Great Abacus landmark built");
    } finally { TearDown(); }
  }

  [Test] public void P45E_HubObjectBudget() {
    SetUp();
    try {
      int count = _root.transform.GetComponentsInChildren<Transform>(true).Length;
      Assert.Less(count, 640, "hub content bounded (gates + landmark + spurs); measured=" + count);
      Assert.Greater(count, 400, "hub richer than the pre-gate skeleton; measured=" + count);
    } finally { TearDown(); }
  }

  [Test] public void P45F_TravelCriticalIntact() {
    SetUp();
    try {
      Assert.AreEqual(new Vector3(60f, 0f, 0f), MathWorldBuilder.WorldOffset);
      Assert.IsNotNull(_builder.Anchors, "presentation anchors intact");
      Assert.IsNotNull(_builder.Anchors.Entry);
      Assert.IsNotNull(_builder.Anchors.Exit);
      Assert.AreEqual(1, _builder.CountingObjects.Count, "pilot quest cube intact");
      Assert.IsNotNull(_builder.BloomRoot, "bloom root intact");
      Assert.AreEqual(27f, MathWorldBuilder.BoundX);
      Assert.AreEqual(27f, MathWorldBuilder.BoundZ);
    } finally { TearDown(); }
  }
}
