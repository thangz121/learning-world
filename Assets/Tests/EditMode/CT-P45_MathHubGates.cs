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

  static Transform FindDeep(Transform t, string name) {
    if (t == null) return null;
    if (t.name == name) return t;
    for (int i = 0; i < t.childCount; i++) {
      Transform f = FindDeep(t.GetChild(i), name);
      if (f != null) return f;
    }
    return null;
  }

  // The test assembly has no AI-Navigation reference by design (contract
  // firewall): probe the bake-ignore flag by component name + reflection.
  static bool IsIgnoredFromBuild(GameObject go) {
    if (go == null) return false;
    Component c = null;
    try { c = go.GetComponent("NavMeshModifier"); } catch (System.Exception) { }
    if (c == null) return false;
    System.Reflection.PropertyInfo p = c.GetType().GetProperty("ignoreFromBuild");
    if (p == null) return false;
    try { return (bool)p.GetValue(c, null); } catch (System.Exception) { return false; }
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
      // S3 cartoon gates measured 698 -> cap 720; S6 beauty+pink measured 777;
      // S7 full-bloom measured 870; S2 pioneer micro-world measured 938 ->
      // cap re-pinned 990, recorded in MATH_HUB_VISUAL_QA.md (shared
      // materials only, zero colliders).
      Assert.Less(count, 990, "hub content bounded (gates + landmark + spurs); measured=" + count);
      Assert.Greater(count, 400, "hub richer than the pre-gate skeleton; measured=" + count);
    } finally { TearDown(); }
  }

  // S1: ring circulation never suggests walking through the brook carves
  // (invisible walls). Waypoints + chord midpoints stay outside both carve
  // rects; the deck waypoint is the only ford.
  [Test] public void P45G_RingAvoidsCarves() {
    SetUp();
    try {
      Assert.GreaterOrEqual(_builder.RingWaypoints.Count, 12, "ring waypoints exposed");
      float[] rectW = { 11.5f, 14.6f, -6.1f, -3.9f };
      float[] rectE = { 16.4f, 19.5f, -6.1f, -3.9f };
      var pts = new List<Vector3>(_builder.RingWaypoints);
      for (int i = 0; i < pts.Count; i++) {
        Vector3 a = pts[i];
        Vector3 b = pts[(i + 1) % pts.Count];
        Vector3 mid = (a + b) * 0.5f;
        foreach (Vector3 p in new Vector3[] { a, mid }) {
          Assert.IsFalse(p.x > rectW[0] && p.x < rectW[1] && p.z > rectW[2] && p.z < rectW[3],
            "ring inside west carve: " + p);
          Assert.IsFalse(p.x > rectE[0] && p.x < rectE[1] && p.z > rectE[2] && p.z < rectE[3],
            "ring inside east carve: " + p);
        }
      }
    } finally { TearDown(); }
  }

  // S1: hub dressing exists (compass heart + zone tints), collider-free.
  // S4 declutter: entry pebbles/tufts removed (noise around the gates).
  [Test] public void P45H_HubDressingPresent() {
    SetUp();
    try {
      foreach (string name in new string[] {
        "MathGateCompass", "MathGateCompassDot",
        "MathGateZoneN", "MathGateZoneW", "MathGateZoneE",
        "MathGateLandmark",
        "MathGateBackdropN1", "MathGateBackdropN2", "MathGateBackdropE",
        "MathGateFrameL", "MathGateFrameR",
        "MathGateAccent0Stem", "MathGateAccent1Stem",
      }) {
        Transform t = _root.transform.Find(name);
        if (t == null) {
          // Landmark root is top-level; zones/compass likewise.
          foreach (Transform c in _root.transform) {
            if (c.name == name) { t = c; break; }
          }
        }
        Assert.IsNotNull(t, name + " built");
        Assert.IsNull(t.GetComponent<Collider>(), name + " collider-free");
      }
    } finally { TearDown(); }
  }

  // S1-final: filler is gone (pebbles/FB blooms), landmarks relocated but
  // present (tower to east backdrop, signs to entry flanks), return flowers kept.
  [Test] public void P45I_DeclutterAndRelocation() {
    SetUp();
    try {
      var all = new List<GameObject>();
      CollectByPrefix(_root.transform, "Math", all);
      var names = new HashSet<string>();
      foreach (GameObject go in all) names.Add(go.name);
      // DressBloom names children <base>Stem/<base>Head (no bare base GO);
      // DressPebble names the single GO exactly.
      foreach (string filler in new string[] {
        "MathPebble0", "MathPebble1",
        "MathBloomFB0Stem", "MathBloomFB1Stem", "MathBloomFB2Stem", "MathBloomFB3Stem",
      }) Assert.IsFalse(names.Contains(filler), filler + " removed (filler)");
      Assert.IsTrue(names.Contains("MathReturnFlowerLStem"), "return framing kept");
      // Tower stands east-backdrop now, clear of the spawn frame.
      Transform tower = _root.transform.Find("MathTower0");
      // MathTowerN are direct children of root.
      if (tower == null) {
        foreach (Transform c in _root.transform) {
          if (c.name == "MathTower0") { tower = c; break; }
        }
      }
      Assert.IsNotNull(tower, "MathTower0 kept");
      Assert.Greater(tower.localPosition.x, 10f, "tower out of the east foreground");
      Transform sign = null;
      foreach (Transform c in _root.transform) {
        if (c.name == "MathSignPlusV") { sign = c; break; }
      }
      Assert.IsNotNull(sign, "MathSignPlusV kept");
      Assert.Less(sign.localPosition.z, -5f, "signs welcome at entry, not the east lawn");
    } finally { TearDown(); }
  }

  [Test] public void P45F_TravelCriticalIntact() {    SetUp();
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

  // S3 cartoon gate contract (user round: "các cổng giống nhau quá… muốn
  // cartoon và đúng bản chất từng micro world"): every destination gate
  // stands on two named cartoon legs at ±1.6, carries its OWN motif (the
  // distinctness map below) and its own bake-ignored arch/span pieces — the
  // old shared post-and-lintel frame is gone for good. Legs stay collider-free.
  [Test] public void P45J_GatesHaveRealFrame() {
    SetUp();
    try {
      // Body stands clear of the 1.5m ring strip (half-width 0.75).
      Assert.GreaterOrEqual(-MathWorldBuilder.GateBodyZ - 0.14f, 1.2f,
        "gate body clears the ring strip with margin");
      var motifByGate = new Dictionary<string, string> {
        { "counting_garden", "MathGateBead0" },
        { "discovery_garden", "MathGateGlass" },
        { "fruit_orchard", "MathGateBasket" },
        { "match_meadow", "MathGateCrown" },
        { "sorting_park", "MathGateBin0" },
        { "puzzle_workshop", "MathGatePeg" },
        { "delivery_village", "MathGateMailBox" },
        { "build_yard", "MathGateCraneHook" },
        { "number_bridge", "MathGateKeystone" },
        { "memory_grove", "MathGateMoon" },
      };
      foreach (MicroWorldGate g in _builder.MicroGates) {
        Transform body = FindDeep(g.transform, "MathGateBody");
        Assert.IsNotNull(body, g.gateId + " gate body built");
        Assert.AreEqual(MathWorldBuilder.GateBodyZ, body.localPosition.z, 0.001f,
          g.gateId + " body stands off the ring waypoint");
        Transform postL = FindDeep(body, "MathGatePostL");
        Transform postR = FindDeep(body, "MathGatePostR");
        Assert.IsNotNull(postL, g.gateId + " post L");
        Assert.IsNotNull(postR, g.gateId + " post R");
        Assert.AreEqual(-1.6f, postL.localPosition.x, 0.001f, g.gateId + " post L x");
        Assert.AreEqual(1.6f, postR.localPosition.x, 0.001f, g.gateId + " post R x");
        Assert.IsNull(postL.GetComponent<Collider>(), g.gateId + " legs collider-free");
        Assert.IsNull(postR.GetComponent<Collider>(), g.gateId + " legs collider-free");
        // Own silhouette: >=1 arch/span piece, every piece bake-ignored.
        int archPieces = 0;
        for (int i = 0; i < body.childCount; i++) {
          Transform c = body.GetChild(i);
          if (!c.name.StartsWith("MathGateArch")) continue;
          archPieces++;
          Assert.IsTrue(IsIgnoredFromBuild(c.gameObject),
            g.gateId + " arch piece bake-ignored: " + c.name);
        }
        Assert.GreaterOrEqual(archPieces, 1, g.gateId + " own arch/span built");
        // Own motif: the per-gate signature object must exist.
        Assert.IsNotNull(FindDeep(g.transform, motifByGate[g.gateId]),
          g.gateId + " carries its own motif");
      }
      // Marker, not gate: the way home ships no arch (S2 user round).
      Assert.IsNull(FindDeep(_root.transform, "MathReturnA"), "return is a marker, not a gate");
      Assert.IsNull(FindDeep(_root.transform, "MathReturnBeam"), "no return beam");
    } finally { TearDown(); }
  }

  // S6 beauty + pink pass (user rounds: "world đẹp hơn" + "gam hồng cho con
  // gái"): blossom trees + petal carpets + pink flower drifts on the lawns,
  // and the pastel rainbow landing behind the north gates. Purely visual:
  // every piece collider-free, the rainbow bake-ignored.
  [Test] public void P45K_BeautyPassLandmarks() {
    SetUp();
    try {
      Transform tree = FindDeep(_root.transform, "MathBlossomTree0Trunk");
      Assert.IsNotNull(tree, "blossom tree built");
      Assert.IsNotNull(FindDeep(_root.transform, "MathBlossomTree0CanopyA"), "pink canopy built");
      Assert.IsNull(tree.GetComponent<Collider>(), "blossom trunk collider-free");
      Assert.IsNotNull(FindDeep(_root.transform, "MathPetalCarpet0"), "petal carpet built");
      Assert.IsNotNull(FindDeep(_root.transform, "MathFlowerDrift0Head0"), "pink flower drift built");
      Transform band = FindDeep(_root.transform, "MathRainbow0");
      Assert.IsNotNull(band, "pastel rainbow band built");
      Assert.IsTrue(IsIgnoredFromBuild(band.gameObject), "rainbow band bake-ignored");
      Assert.IsNull(band.GetComponent<Collider>(), "rainbow band collider-free");
      Assert.IsNotNull(FindDeep(_root.transform, "MathRainbowCloudL"), "rainbow cloud foot built");
      // S7 full-bloom: falling petals + flapping butterflies + entry crown.
      Transform petal = FindDeep(_root.transform, "MathPetalFall0");
      Assert.IsNotNull(petal, "falling petal built");
      Assert.IsNull(petal.GetComponent<Collider>(), "petal collider-free");
      Assert.IsNotNull(FindDeep(_root.transform, "MathPetalFall"), "petal fall root built");
      Transform butterfly = FindDeep(_root.transform, "MathButterfly0");
      Assert.IsNotNull(butterfly, "butterfly built");
      Assert.IsNotNull(FindDeep(_root.transform, "MathButterfly0WingL"), "butterfly wing built");
      Assert.IsNull(butterfly.GetComponent<Collider>(), "butterfly collider-free");
      Assert.IsNotNull(FindDeep(_root.transform, "MathEntryBlossom0"), "entry blossom crown built");
      // S8: beauty planting keeps CLEAR of gate bodies and the real Number
      // Bridge deck (user screenshot: a blossom tree was stuck to the bridge).
      var trees = new List<Transform>();
      for (int i = 0; i < 12; i++) {
        Transform trunk = FindDeep(_root.transform, "MathBlossomTree" + i + "Trunk");
        Assert.IsNotNull(trunk, "blossom tree " + i + " built");
        trees.Add(trunk);
      }
      var drifts2 = new List<Transform>();
      for (int i = 0; i < 14; i++) {
        Transform stem = FindDeep(_root.transform, "MathFlowerDrift" + i + "Stem0");
        Assert.IsNotNull(stem, "flower drift " + i + " built");
        drifts2.Add(stem);
      }
      Vector3 deck = new Vector3(15.5f, 0f, -5f); // real boardwalk bridge centre
      Vector3 garden = new Vector3(-16f, 0f, 5f); // counting garden centre
      foreach (Transform blossom in trees) {
        Vector3 p = blossom.position;
        p.y = 0f;
        foreach (MicroWorldGate g in _builder.MicroGates) {
          Vector3 body = g.transform.TransformPoint(new Vector3(0f, 0f, MathWorldBuilder.GateBodyZ));
          body.y = 0f;
          Assert.GreaterOrEqual(Vector3.Distance(p, body), 3.5f, blossom.name + " clear of " + g.gateId);
        }
        Assert.GreaterOrEqual(Vector3.Distance(p, deck), 3.5f, blossom.name + " clear of the real bridge");
        Assert.GreaterOrEqual(Vector3.Distance(p, garden), 3.5f, blossom.name + " clear of the garden");
      }
      foreach (Transform drift in drifts2) {
        Vector3 p = drift.position;
        p.y = 0f;
        foreach (MicroWorldGate g in _builder.MicroGates) {
          Vector3 body = g.transform.TransformPoint(new Vector3(0f, 0f, MathWorldBuilder.GateBodyZ));
          body.y = 0f;
          Assert.GreaterOrEqual(Vector3.Distance(p, body), 2.0f, drift.name + " clear of " + g.gateId);
        }
        Assert.GreaterOrEqual(Vector3.Distance(p, deck), 2.0f, drift.name + " clear of the real bridge");
      }
    } finally { TearDown(); }
  }

  // S2 GATE IDENTITY (scope: entrance only, no gameplay): every gate carries
  // the shared threshold ring under the arch (the "doorway" grammar) and the
  // five gates that lacked a strong crown got their primary landmark. Purely
  // static, collider-free presentation — no interactables, no activities.
  [Test] public void P45L_GateEntranceIdentity() {
    SetUp();
    try {
      foreach (MicroWorldGate g in _builder.MicroGates) {
        Transform body = FindDeep(g.transform, "MathGateBody");
        Assert.IsNotNull(body, g.gateId + " body");
        Transform ring = FindDeep(g.transform, "MathGateThreshold");
        Assert.IsNotNull(ring, g.gateId + " threshold ring built");
        Assert.IsNull(ring.GetComponent<Collider>(), g.gateId + " threshold collider-free");
        Assert.IsNotNull(FindDeep(g.transform, "MathGateThresholdInner"), g.gateId + " threshold inner");
        Assert.IsNull(g.GetComponentInChildren<Interactable>(true), g.gateId + " has no activity");
      }
      foreach (string crown in new[] {
        "MathGateCountCrown0", "MathGateGiantApple", "MathGateJigsawCrown",
        "MathGateParcelCrown", "MathGateSeqCrown0",
      }) {
        Transform t = FindDeep(_root.transform, crown);
        Assert.IsNotNull(t, crown + " crown built");
        Assert.IsNull(t.GetComponent<Collider>(), crown + " collider-free");
      }
    } finally { TearDown(); }
  }
}
