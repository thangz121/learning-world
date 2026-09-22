// CT-P41: Math world functional review, headless (Phase 3.0.x S3B).
// Builds ONLY world content (no NavMesh bake, no gate binding, no scenes)
// via MathWorldBuilder.BuildContent and reviews it functionally: entry IDs
// resolve, quest target exists and is typed, bloom root staged hidden,
// landmarks/roads/return arch present, layout inside the hedge ring, abacus
// clear of the arrival corridor, host anchor inside the lobby pad.
// S3B §5 evidence without foreground (composition eyes stay S5). C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class CT_P41_MathWorldContent {
  GameObject _root;
  MathWorldBuilder _builder;

  void SetUp() {
    _root = new GameObject("P41MathWorld");
    _builder = _root.AddComponent<MathWorldBuilder>();
    _builder.BuildContent(_root.transform);
  }

  void TearDown() {
    if (_root != null) Object.DestroyImmediate(_root);
    _root = null;
    _builder = null;
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

  static float FlatDist(Vector3 a, Vector3 b) {
    float dx = a.x - b.x, dz = a.z - b.z;
    return Mathf.Sqrt(dx * dx + dz * dz);
  }

  static float DistToSegment(Vector3 p, Vector3 a, Vector3 b) {
    Vector2 pa = new Vector2(p.x - a.x, p.z - a.z);
    Vector2 ba = new Vector2(b.x - a.x, b.z - a.z);
    float t = Mathf.Clamp01((pa.x * ba.x + pa.y * ba.y) / Mathf.Max(0.001f, ba.sqrMagnitude));
    return new Vector2(pa.x - ba.x * t, pa.y - ba.y * t).magnitude;
  }

  // A. Learning entries resolve by stable ID (Phase 3.1 plugs in here).
  [Test] public void P41A_LearningEntriesResolve() {
    SetUp();
    try {
      foreach (string id in MathLearningEntries.All) {
        Assert.IsNotNull(FindDeep(_root.transform, id),
          "learning entry '" + id + "' must exist by ID (never by coordinates)");
      }
    } finally { TearDown(); }
  }

  // B. Quest target: exactly one countable, typed "one", in-range.
  [Test] public void P41B_CountingTargetTyped() {
    SetUp();
    try {
      Assert.AreEqual(1, _builder.CountingObjects.Count, "exactly one quest target");
      Interactable inter = _builder.CountingObjects[0];
      Assert.IsNotNull(inter);
      Assert.IsTrue(inter.HasWord, "target must parse its word");
      Assert.AreEqual("one", inter.Word.Value);
      Assert.AreEqual(2.5f, inter.interactionDistance, 0.001f);
    } finally { TearDown(); }
  }

  // C. Bloom root staged hidden (consumer flips on completion).
  [Test] public void P41C_BloomRootStagedHidden() {
    SetUp();
    try {
      Assert.IsNotNull(_builder.BloomRoot, "bloom root exposed");
      Assert.IsTrue(_builder.BloomRoot.gameObject.activeSelf, "root stays active");
      Assert.AreEqual(3, _builder.BloomRoot.childCount, "three blooms");
      foreach (Transform child in _builder.BloomRoot) {
        Assert.IsFalse(child.gameObject.activeSelf, child.name + " starts hidden");
      }
    } finally { TearDown(); }
  }

  // D. Landmarks, paths and return arch all present (no pads-only shell).
  // B1R re-pin: the world grew (r26) and the hub was decluttered; the Kenney
  // fence/bridge modules and the meadow loop are now part of the landmark set.
  [Test] public void P41D_LandmarksPresent() {
    SetUp();
    try {
      foreach (string name in new string[] {
        "MathAbacusPostL", "MathAbacusPostR",
        "GardenBedW", "GardenBedE", "GardenBedN", "GardenBerryBed",
        "GardenPedestal1", "GardenPedestal2", "GardenPedestal3",
        "BridgeStream", "BridgeRailW", "BridgeRailE", "MathBridgeModule0",
        "MathPathEntry", "MathPathReturn", "MathPathGarden", "MathPathBridge",
        "MathPathMeadow1", "MathPathGardenInnerA",
        "MathReturnA", "MathReturnB", "MathReturnBeam",
        "MathLobbyPad", "MathEntryPad", "CountingGardenPad", "NumberBridgePad",
        "MathGardenGate", "MathCountingTree", "MathPropTree0", "MathSkyCloud0",
        "MathTower0", "MathDomino0", "MathSignPlusV", "MathBedPipsW0",
        "MathWorldSignBoard", "MathWorldSignPost", "MathWorldSignLabel",
      }) {
        Assert.IsNotNull(FindDeep(_root.transform, name), name + " must be built");
      }
    } finally { TearDown(); }
  }

  // E. Layout discipline: everything inside the island (r26 ground, r25
  // hedge); abacus clear of the arrival corridor; host anchored inside the
  // lobby pad. The skirt is the ground itself (skipped by name).
  [Test] public void P41E_LayoutDiscipline() {
    SetUp();
    try {
      var skip = new HashSet<string> { "MathSun", "MathNavMesh", "MathReturnGate", "MathOuterField" };
      var all = new List<Transform>();
      var stack = new Stack<Transform>();
      stack.Push(_root.transform);
      while (stack.Count > 0) {
        Transform t = stack.Pop();
        if (!skip.Contains(t.name)) all.Add(t);
        for (int i = 0; i < t.childCount; i++) stack.Push(t.GetChild(i));
      }
      foreach (Transform t in all) {
        if (t.name.StartsWith("MathSky")) continue; // sky dressing lives beyond the island
        Vector3 p = t.position;
        Assert.LessOrEqual(Mathf.Abs(p.x), 25.5f, t.name + " inside island X");
        Assert.LessOrEqual(Mathf.Abs(p.z), 25.5f, t.name + " inside island Z");
      }
      Transform abacus = FindDeep(_root.transform, "MathAbacusPostL");
      Assert.IsNotNull(abacus);
      Assert.Greater(
        DistToSegment(abacus.position, new Vector3(0f, 0f, -12f), new Vector3(0f, 0f, 0f)), 0.9f,
        "abacus stays clear of the entry->lobby arrival corridor");
      Assert.Less(FlatDist(MathWorldBuilder.HostAnchorLocal, Vector3.zero), 6f,
        "host anchors inside the lobby pad");
    } finally { TearDown(); }
  }
}
