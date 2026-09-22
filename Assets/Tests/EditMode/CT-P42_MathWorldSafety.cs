// CT-P42: Math world safety, headless (Phase 3.0.x S4).
// Targeted navigation/collider sanity after MEDIUM-impact decor changes
// (S4 §1/§10): dressing must never block feet or eat clicks, corridors stay
// walkable, object count stays bounded, quest beacon ships on its cube.
// Batch-mode runnable (no player, no scenes, no screenshots): builds content
// headlessly, queries REAL colliders via Physics.OverlapSphere + catalog
// geometry. S5 still owns the full Spawn-to-Feature audit. C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class CT_P42_MathWorldSafety {
  GameObject _root;
  MathWorldBuilder _builder;

  void SetUp() {
    _root = new GameObject("P42MathWorld");
    _builder = _root.AddComponent<MathWorldBuilder>();
    _builder.BuildContent(_root.transform);
    try { Physics.SyncTransforms(); } catch (System.Exception) { }
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

  static void CollectByPrefix(Transform t, string prefix, List<GameObject> outList) {
    if (t == null) return;
    if (t.name.StartsWith(prefix)) outList.Add(t.gameObject);
    for (int i = 0; i < t.childCount; i++) CollectByPrefix(t.GetChild(i), prefix, outList);
  }

  // A. Dressing is collider-free (S4 rule: decor can never block or eat clicks)
  // while the quest cube MUST keep its collider (it is the click target).
  [Test] public void P42A_DressingHasNoColliders() {
    SetUp();
    try {
      var decor = new List<GameObject>();
      foreach (string prefix in new string[] {
        "MathPebble", "MathBloom", "MathBead", "MathReeds", "MathBackdrop",
        "MathReturnFlower", "MathEntryBead", "MathBridgeDisc", "MathShape",
        "MathFence", "MathCrop", "MathProp", "MathGardenStone", "MathClearingStone",
        "MathGardenStonePip", "MathClearingPip", "MathHostFlower",
        "MathDomino", "MathTower", "MathSign", "MathBedPips",
        "MathWorldSignBead",
      }) CollectByPrefix(_root.transform, prefix, decor);
      Assert.Greater(decor.Count, 10, "dressing must exist to be checked");
      var offenders = new List<string>();
      foreach (GameObject go in decor) {
        if (go.GetComponent<Collider>() != null) offenders.Add(go.name);
      }
      Assert.AreEqual(0, offenders.Count,
        "collider on dressing (blocker/click-eater risk): " + string.Join(",", offenders.ToArray()));
      // Pedestal decor cubes ride stripped; the quest cube stays clickable.
      foreach (string ped in new string[] { "GardenPedestal2", "GardenPedestal3" }) {
        Transform p = FindDeep(_root.transform, ped);
        Assert.IsNotNull(p, ped + " built");
      }
      var cubes = new List<GameObject>();
      CollectByPrefix(_root.transform, "GardenPedestal", cubes);
      foreach (GameObject go in cubes) {
        if (go.name.Contains("Cube")) Assert.IsNull(go.GetComponent<Collider>(), go.name + " stripped");
      }
      Transform quest = FindDeep(_root.transform, "MathOneCube");
      Assert.IsNotNull(quest, "quest cube built");
      Assert.IsNotNull(quest.GetComponent<Collider>(), "quest cube keeps its click collider");
    } finally { TearDown(); }
  }

  // B. Corridors walkable: torso-height sphere finds no structural blocker on
  // the entry/garden/bridge/return spokes, the meadow loop and the garden
  // inner path (walkable ground, pads, paths, discs, stones and the quest
  // cube itself are exempt by name). B1R: new district-scale coordinates.
  [Test] public void P42B_CorridorsClear() {
    SetUp();
    try {
      Vector3[] samples = {
        new Vector3(0f, 0.5f, -10f), new Vector3(0f, 0.5f, -5f), new Vector3(0f, 0.5f, -1f),
        new Vector3(-8f, 0.5f, 2.5f), new Vector3(7.75f, 0.5f, -2.5f),
        new Vector3(0f, 0.5f, 5f), new Vector3(0f, 0.5f, 10f),
        new Vector3(9f, 0.5f, -9.2f), new Vector3(2f, 0.5f, -9.6f), new Vector3(-5f, 0.5f, -9f),
        new Vector3(-11f, 0.5f, -6.8f), new Vector3(-16f, 0.5f, -1.5f),
        new Vector3(-12.2f, 0.5f, 3.6f), new Vector3(-14.6f, 0.5f, 3f), new Vector3(-18.2f, 0.5f, 1.2f),
      };
      var blockers = new List<string>();
      foreach (Vector3 s in samples) {
        Collider[] hits;
        try { hits = Physics.OverlapSphere(s, 0.45f); }
        catch (System.Exception) { Assert.Inconclusive("physics queries unavailable headless"); return; }
        foreach (Collider h in hits) {
          if (h == null) continue;
          string n = h.gameObject.name;
          if (n.StartsWith("MathGround") || n.Contains("Pad") || n.StartsWith("MathPath")
            || n.StartsWith("MathReturnDisc") || n.StartsWith("MathChevron")
            || n.StartsWith("MathOneCube") || n.StartsWith("MathBloomRoot")
            || n.StartsWith("MathMeadow") || n.StartsWith("MathGardenStone")
            || n.StartsWith("MathClearingStone") || n.StartsWith("MathBridgeDeck")) continue;
          blockers.Add(n + "@" + s);
        }
      }
      Assert.AreEqual(0, blockers.Count,
        "structural blocker on walking lines: " + string.Join(",", blockers.ToArray()));
    } finally { TearDown(); }
  }

  // C. Object budget guardrail (perf: decor must not bloat the scene).
  // B1R2 measured 311; B1R3 added the sky rim/clouds, world-name column,
  // bunting and pink/red accents (measured 400) -> cap re-pinned to 440 with
  // the number recorded in MATH_WORLD_VISUAL_QA.md; shared materials only.
  // Hub phase measured ~600 (10 gate skeletons + landmark + spurs, all shared
  // Lit/PropKit materials, zero colliders, zero lights) -> cap re-pinned to
  // 640, recorded in MATH_HUB_VISUAL_QA.md. Micro-World 1 re-pins again.
  [Test] public void P42C_ObjectBudget() {
    SetUp();
    try {
      int count = _root.transform.GetComponentsInChildren<Transform>(true).Length;
      Assert.Less(count, 640, "content object count bounded (perf guardrail)");
      Assert.Greater(count, 100, "district-scale skeleton + dressing present (not a bare shell)");
    } finally { TearDown(); }
  }

  // D. Quest beacon ships on the counting cube (readability motion, S4 §5).
  [Test] public void P42D_BeaconOnQuestCube() {
    SetUp();
    try {
      Assert.AreEqual(1, _builder.CountingObjects.Count);
      MathBeacon beacon = _builder.CountingObjects[0].GetComponent<MathBeacon>();
      Assert.IsNotNull(beacon, "One cube carries the attention beacon");
      Assert.Greater(beacon.BobAmplitude, 0f);
      Assert.Greater(beacon.SpinDegPerSec, 0f);
    } finally { TearDown(); }
  }
}
