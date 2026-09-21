// CT-P43: Phase 3.0.1.1 skeleton-complete hardening, headless.
// Pins the user-round fixes: Math return gap widened (feet pass to the
// trigger), entry board corridor kept, pad/path z-separation (no more
// medallion flicker), number-row counts, spatial return way-home markers,
// Tess Golden prefab (real rig, shared NPC controller).
// Batch-mode runnable (no player, no scenes, no screenshots). C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class CT_P43_SkeletonComplete {
  GameObject _root;
  MathWorldBuilder _builder;

  void SetUpMath() {
    _root = new GameObject("P43MathWorld");
    _builder = _root.AddComponent<MathWorldBuilder>();
    _builder.BuildContent(_root.transform);
    try { Physics.SyncTransforms(); } catch (System.Exception) { }
  }

  void TearDownMath() {
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

  static float TopY(Transform t) {
    return t.position.y + t.localScale.y;
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

  // A. Math return arch passes feet: pillars at +-1.25 (2.2m clear, ~1.2m
  // after agent erosion — the old 1.5m gap baked down to a ~0.5m slot).
  [Test] public void P43A_ReturnGapWide() {
    SetUpMath();
    try {
      Transform a = FindDeep(_root.transform, "MathReturnA");
      Transform b = FindDeep(_root.transform, "MathReturnB");
      Transform beam = FindDeep(_root.transform, "MathReturnBeam");
      Assert.IsNotNull(a, "return pillar A built");
      Assert.IsNotNull(b, "return pillar B built");
      Assert.IsNotNull(beam, "return beam built");
      Assert.AreEqual(-1.25f, a.localPosition.x, 0.001f, "pillar A x");
      Assert.AreEqual(1.25f, b.localPosition.x, 0.001f, "pillar B x");
      Assert.AreEqual(2.8f, beam.localScale.x, 0.001f, "beam spans the widened gap");
      Assert.IsTrue(IsIgnoredFromBuild(beam.gameObject), "beam ignore flag set (headroom rule)");
    } finally { TearDownMath(); }
  }

  // B. Entry board keeps the arrival corridor: posts at +-1.1, beam + beads
  // above 2m and bake-ignored, everything click-through.
  [Test] public void P43B_EntryBoardCorridor() {
    SetUpMath();
    try {
      Transform l = FindDeep(_root.transform, "MathEntryPostL");
      Transform r = FindDeep(_root.transform, "MathEntryPostR");
      Transform beam = FindDeep(_root.transform, "MathEntryBeam");
      Assert.IsNotNull(l, "entry post L built");
      Assert.IsNotNull(r, "entry post R built");
      Assert.IsNotNull(beam, "entry beam built");
      Assert.AreEqual(-1.1f, l.localPosition.x, 0.001f, "post L x");
      Assert.AreEqual(1.1f, r.localPosition.x, 0.001f, "post R x");
      Assert.GreaterOrEqual(beam.localPosition.y, 2.0f, "beam above head passage");
      Assert.IsTrue(IsIgnoredFromBuild(beam.gameObject), "entry beam bake-ignored");
      var beads = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathEntryBead", beads);
      Assert.AreEqual(3, beads.Count, "3 abacus beads ride the beam");
      foreach (GameObject bead in beads) {
        Assert.GreaterOrEqual(bead.transform.localPosition.y, 2.0f, "bead above head passage");
        Assert.IsNull(bead.GetComponent<Collider>(), bead.name + " click-through");
        Assert.IsTrue(IsIgnoredFromBuild(bead), bead.name + " bake-ignored");
      }
    } finally { TearDownMath(); }
  }

  // C. Pad/path z-separation: path tops ride 25mm+ above pad tops (the
  // medallion flicker was coplanar tops at 0.03).
  [Test] public void P43C_PadPathSeparation() {
    SetUpMath();
    try {
      Transform pad = FindDeep(_root.transform, "MathLobbyPad");
      Transform path = FindDeep(_root.transform, "MathPathEntry");
      Transform pathR = FindDeep(_root.transform, "MathPathReturn");
      Transform disc = FindDeep(_root.transform, "MathReturnDisc");
      Assert.IsNotNull(pad, "lobby pad built");
      Assert.IsNotNull(path, "entry path built");
      Assert.IsNotNull(pathR, "return path built");
      Assert.IsNotNull(disc, "return disc built");
      float sep = TopY(path) - TopY(pad);
      Assert.GreaterOrEqual(sep, 0.025f, "path rides above pad (no coplanar flicker): " + sep);
      float sepR = TopY(pathR) - TopY(pad);
      Assert.GreaterOrEqual(sepR, 0.025f, "return path rides above pad: " + sepR);
      Assert.GreaterOrEqual(TopY(disc) - TopY(pathR), 0.010f, "return disc reads above the path");
    } finally { TearDownMath(); }
  }

  // D. Number row: 5 walkable pads south of the garden path, pad i carries
  // i+1 gold pips (1..5); pads keep feet colliders, pips are click-through.
  [Test] public void P43D_NumberRowCounts() {
    SetUpMath();
    try {
      var pads = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathNumPad", pads);
      Assert.AreEqual(5, pads.Count, "5 counting pads 1..5");
      var pips = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathNumPip", pips);
      Assert.AreEqual(15, pips.Count, "1+2+3+4+5 pips");
      foreach (GameObject pip in pips) {
        Assert.IsNull(pip.GetComponent<Collider>(), pip.name + " click-through");
      }
      // Corridor guard: pads stay off the lobby->garden walking line
      // (3.0.2 segment (0,0)->(-10.5,3), keep 0.8m+ clearance).
      Vector3 a = new Vector3(0f, 0f, 0f);
      Vector3 b = new Vector3(-10.5f, 0f, 3f);
      foreach (GameObject pad in pads) {
        Vector3 p = pad.transform.localPosition;
        p.y = 0f;
        float dist = DistToSegment(p, a, b);
        Assert.GreaterOrEqual(dist, 0.8f, pad.name + " clear of the garden line: " + dist);
      }
    } finally { TearDownMath(); }
  }

  static float DistToSegment(Vector3 p, Vector3 a, Vector3 b) {
    Vector3 ab = b - a;
    float denom = ab.sqrMagnitude;
    if (denom < 0.000001f) return Vector3.Distance(p, a);
    float t = Vector3.Dot(p - a, ab) / denom;
    if (t < 0f) t = 0f;
    if (t > 1f) t = 1f;
    return Vector3.Distance(p, a + ab * t);
  }

  // E. Spatial returns show the way home: each subject gets a gold disc +
  // a "Về" label (the invisible trigger alone could not be found).
  [Test] public void P43E_SpatialReturnWayHome() {
    GameObject parent = new GameObject("P43MainWorld");
    try {
      SubjectWorldBuilder.BuildResult result = SubjectWorldBuilder.BuildShell(parent.transform);
      Assert.IsNotNull(result, "shell builds headlessly");
      Assert.AreEqual(4, result.ReturnGates.Count, "4 return triggers");
      var discs = new List<GameObject>();
      CollectBySuffix(parent.transform, "ReturnDisc", discs);
      Assert.AreEqual(4, discs.Count, "4 return discs (one per subject)");
      int veCount = 0;
      var labels = parent.GetComponentsInChildren<WorldNameLabel>(true);
      foreach (WorldNameLabel wl in labels) {
        if (wl != null && wl.CurrentName == "Về") veCount++;
      }
      Assert.AreEqual(4, veCount, "4 return labels read Về");
    } finally { Object.DestroyImmediate(parent); }
  }

  static void CollectBySuffix(Transform t, string suffix, List<GameObject> outList) {
    if (t == null) return;
    if (t.name.EndsWith(suffix)) outList.Add(t.gameObject);
    for (int i = 0; i < t.childCount; i++) CollectBySuffix(t.GetChild(i), suffix, outList);
  }

  // F. Tess Golden body: TessVisual prefab ships a real NPC rig (Animator +
  // shared controller), and the presenter builds it under a VisualRoot with
  // the interaction capsule intact (quest wiring untouched).
  [Test] public void P43F_TessVisualGolden() {
    GameObject prefab = Resources.Load<GameObject>("NpcVisuals/TessVisual");
    Assert.IsNotNull(prefab, "TessVisual prefab ships in Resources");
    Animator anim = prefab.GetComponentInChildren<Animator>(true);
    Assert.IsNotNull(anim, "Tess rig carries an Animator");
    Assert.IsNotNull(anim.runtimeAnimatorController, "Animator shares the NPC controller");
    GameObject host = new GameObject("P43Tess");
    try {
      MathHostPresenter presenter = host.AddComponent<MathHostPresenter>();
      Assert.IsNotNull(presenter, "presenter instantiates headlessly");
      // Batch EditMode does not deliver Awake on AddComponent — drive the
      // explicit body build (production Awake path is identical + guarded).
      presenter.BuildBodyImmediate();
      presenter.BuildBodyImmediate(); // idempotent: second call is a no-op
      CapsuleCollider col = host.GetComponent<CapsuleCollider>();
      Assert.IsNotNull(col, "body built (interaction capsule marked)");
      int kids = host.transform.childCount;
      string names = "";
      for (int i = 0; i < kids; i++) names += host.transform.GetChild(i).name + ";";
      Transform visual = host.transform.Find("TessVisualRoot");
      Assert.IsNotNull(visual, "VisualRoot built (children: " + kids + " [" + names + "])");
      Assert.AreEqual(0.5f, visual.localScale.x, 0.001f, "rig scaled to chibi (Golden §3)");
      Assert.AreEqual(1.7f, col.height, 0.001f, "capsule matches the 1.7m rig");
      Assert.AreEqual(1, host.GetComponents<CapsuleCollider>().Length, "single capsule after double build");
      int roots = 0;
      for (int i = 0; i < host.transform.childCount; i++) {
        if (host.transform.GetChild(i).name == "TessVisualRoot") roots++;
      }
      Assert.AreEqual(1, roots, "single VisualRoot after double build");
    } finally { Object.DestroyImmediate(host); }
  }

  // G. GitHub nature ships placed + click-through + off the walking lines
  // (3.0.2 decor round: 6 trees + 4 bushes + 3 rocks + 6 grass = 19).
  [Test] public void P43G_NaturePlacedAndClear() {
    SetUpMath();
    try {
      var nature = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathQ", nature);
      Assert.AreEqual(19, nature.Count, "19 Quaternius placements");
      foreach (GameObject go in nature) {
        Assert.IsNull(go.GetComponent<Collider>(), go.name + " click-through (FBX carry none)");
      }
      // Blender scene furniture must not ship: the 2018 FBX carry author
      // Camera + Lamp nodes (P41E caught one at |x|=18.7).
      foreach (GameObject go in nature) {
        Assert.AreEqual(0, go.GetComponentsInChildren<Camera>(true).Length, go.name + " ships no camera");
        Assert.AreEqual(0, go.GetComponentsInChildren<Light>(true).Length, go.name + " ships no lamp");
      }
      // Walking lines: entry x=0 z[-8,0], garden (0,0)->(-10.5,3),
      // bridge (0,0)->(10.5,-3), return x=0 z[0,8]. Trees need 1.5m+,
      // small dressing 1.0m+.
      foreach (GameObject go in nature) {
        Vector3 p = go.transform.localPosition;
        p.y = 0f;
        float need = go.name.Contains("Tree") ? 1.5f : 1.0f;
        Assert.GreaterOrEqual(DistToSegment(p, new Vector3(0f, 0f, -8f), new Vector3(0f, 0f, 0f)), need, go.name + " clear of entry");
        Assert.GreaterOrEqual(DistToSegment(p, new Vector3(0f, 0f, 0f), new Vector3(-10.5f, 0f, 3f)), need, go.name + " clear of garden line");
        Assert.GreaterOrEqual(DistToSegment(p, new Vector3(0f, 0f, 0f), new Vector3(10.5f, 0f, -3f)), need, go.name + " clear of bridge line");
        Assert.GreaterOrEqual(DistToSegment(p, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 8f)), need, go.name + " clear of return");
      }
    } finally { TearDownMath(); }
  }
}
