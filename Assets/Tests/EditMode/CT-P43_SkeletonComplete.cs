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
      // B1 re-pin: the entry door carries the world's number (5 beads) — the
      // 3-bead skeleton version predated the composition round.
      Assert.AreEqual(5, beads.Count, "5 abacus beads ride the beam");
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

  // D. Counting stones (B1R re-pin: the off-path number-row pads were
  // removed as hub clutter; the 1..5 language now lives on the garden
  // stepping stones along the inner path). 5 stones, 1+2+3+4+5 gold pips,
  // pips click-through, every stone inside the fenced plot (r6.5).
  [Test] public void P43D_NumberRowCounts() {
    SetUpMath();
    try {
      var stones = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathGardenStone", stones);
      // Prefix also matches the pips (MathGardenStonePip*): filter by exact
      // trailing digit.
      int stoneCount = 0;
      for (int i = 0; i < 5; i++) {
        if (FindDeep(_root.transform, "MathGardenStone" + i) != null) stoneCount++;
      }
      Assert.AreEqual(5, stoneCount, "5 counting stones 1..5");
      var pips = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathGardenStonePip", pips);
      Assert.AreEqual(15, pips.Count, "1+2+3+4+5 pips");
      foreach (GameObject pip in pips) {
        Assert.IsNull(pip.GetComponent<Collider>(), pip.name + " click-through");
      }
      Vector3 c = new Vector3(-16f, 0f, 5f);
      for (int i = 0; i < 5; i++) {
        Transform s = FindDeep(_root.transform, "MathGardenStone" + i);
        Vector3 p = s.localPosition;
        p.y = 0f;
        Assert.LessOrEqual(Vector3.Distance(p, c), 6.5f, s.name + " inside the fenced plot");
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

  // H. B1 composition: the MathScene return arch finally carries the "Về"
  // label the 3 spatial subjects ship (audit F13).
  [Test] public void P43H_ReturnLabelInMath() {
    SetUpMath();
    try {
      var labels = _root.GetComponentsInChildren<WorldNameLabel>(true);
      int veCount = 0;
      foreach (WorldNameLabel wl in labels) {
        if (wl != null && wl.CurrentName == "Về") veCount++;
      }
      Assert.AreEqual(1, veCount, "Math return arch reads Về");
    } finally { TearDownMath(); }
  }

  // I. B1R garden plot: Kenney fence with 2 openings + gate, counting tree
  // with 5 bead-fruit, 5 stepping stones (1..5 pips), countable Kenney crops
  // in every bed (3 carrots / 2 pumpkins / 4 corn / 5 strawberries).
  [Test] public void P43I_GardenPlotReadable() {
    SetUpMath();
    try {
      var fence = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathFence", fence);
      Assert.GreaterOrEqual(fence.Count, 24, "Kenney fence modules around the plot");
      Assert.IsNotNull(FindDeep(_root.transform, "MathGardenGate"), "garden gate module");
      Assert.IsNotNull(FindDeep(_root.transform, "MathCountingTree"), "counting tree");
      var beads = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathTreeBead", beads);
      Assert.AreEqual(5, beads.Count, "5 bead-fruit on the tree");
      int stoneCount = 0;
      for (int i = 0; i < 5; i++) {
        if (FindDeep(_root.transform, "MathGardenStone" + i) != null) stoneCount++;
      }
      Assert.AreEqual(5, stoneCount, "5 stepping stones");
      var pips = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathGardenStonePip", pips);
      Assert.AreEqual(15, pips.Count, "1+2+3+4+5 pips");
      var carrots = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathCropCarrot", carrots);
      Assert.AreEqual(3, carrots.Count, "3 carrots");
      var pumpkins = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathCropPumpkin", pumpkins);
      Assert.AreEqual(2, pumpkins.Count, "2 pumpkins");
      var corn = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathCropCorn", corn);
      Assert.AreEqual(4, corn.Count, "4 corn");
      var berries = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathCropStrawberry", berries);
      Assert.AreEqual(5, berries.Count, "5 strawberries");
    } finally { TearDownMath(); }
  }

  // J. B1R hub/bridge composition: courtyard rim + host mat, meadow loop
  // paths + garden inner path, Kenney bridge modules + rail posts, far-bank
  // stone circle + bead pile. (The hub pot cluster + mouth markers were
  // removed as clutter in the user round.)
  [Test] public void P43J_HubBridgeComposition() {
    SetUpMath();
    try {
      var rim = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathRimStone", rim);
      // B1R6 declutter: 4 larger rim stones on the courtyard diagonals.
      Assert.AreEqual(4, rim.Count, "courtyard rim stones");
      Assert.IsNotNull(FindDeep(_root.transform, "MathHostMat"), "host nook mat");
      var loopPaths = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathPathMeadow", loopPaths);
      Assert.AreEqual(5, loopPaths.Count, "5 meadow loop segments");
      Assert.IsNotNull(FindDeep(_root.transform, "MathPathGardenInnerA"), "garden inner path");
      var modules = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathBridgeModule", modules);
      Assert.AreEqual(3, modules.Count, "3 Kenney bridge modules");
      var posts = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathBridgeCountPost", posts);
      Assert.AreEqual(5, posts.Count, "5 rail posts on the bridge");
      var stones = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathClearingStone", stones);
      Assert.AreEqual(5, stones.Count, "5 far-bank stones");
      var pips = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathClearingPip", pips);
      Assert.AreEqual(15, pips.Count, "1+2+3+4+5 far-bank pips");
      var beads = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathRewardBead", beads);
      Assert.AreEqual(3, beads.Count, "gold bead reward pile");
    } finally { TearDownMath(); }
  }

  // K. MathScene return gate binding (journey P1 regression): SubjectGate's
  // return branch fires only when nav.Current == target, so a scene return
  // arch MUST be bound to its SUBJECT id (binding Main trapped the player).
  [Test] public void P43K_ReturnGateBindsSubject() {
    GameObject root = new GameObject("P43ReturnGateWorld");
    try {
      MathWorldBuilder b = root.AddComponent<MathWorldBuilder>();
      var bus = new GameEventBus();
      var nav = new WorldNavService(bus);
      nav.Enter(SubjectIds.Math);
      b.Build(nav, null);
      Transform gateT = FindDeep(root.transform, "MathReturnGate");
      Assert.IsNotNull(gateT, "return gate built");
      SubjectGate gate = gateT.GetComponent<SubjectGate>();
      Assert.IsNotNull(gate);
      Assert.IsTrue(gate.IsReturnGate, "gate is a return gate");
      Assert.AreEqual(SubjectIds.Math, gate.Target, "return gate targets the subject id");
      Assert.IsTrue(gate.TryFireForTests(gateT.position, SubjectIds.Math),
        "return gate fires when the player is inside Math");
      Assert.AreEqual(SubjectIds.Main, nav.Current, "return goes to Main");
    } finally { Object.DestroyImmediate(root); }
  }

  // G. Kenney props (CC0) ship placed + click-through + off the walking
  // lines. B1R3: 11 rim trees + 7 nature trees + 25 meadow props = 43
  // (the +5 are the red/pink accent flowers + mushroom).
  [Test] public void P43G_NaturePlacedAndClear() {
    SetUpMath();
    try {
      var nature = new List<GameObject>();
      CollectByPrefix(_root.transform, "MathProp", nature);
      Assert.AreEqual(43, nature.Count, "43 Kenney prop placements");
      foreach (GameObject go in nature) {
        Assert.IsNull(go.GetComponent<Collider>(), go.name + " click-through (PropKit strips)");
      }
      // Kit FBX must never ship author cameras/lamps (PropKit strips them).
      foreach (GameObject go in nature) {
        Assert.AreEqual(0, go.GetComponentsInChildren<Camera>(true).Length, go.name + " ships no camera");
        Assert.AreEqual(0, go.GetComponentsInChildren<Light>(true).Length, go.name + " ships no lamp");
      }
      // Walking lines (B1R): entry x=0 z[-12,0], garden (0,0)->(-16,5),
      // bridge (0,0)->(15.5,-5), return x=0 z[0,12]. Trees need 1.5m+,
      // small dressing 1.0m+.
      foreach (GameObject go in nature) {
        Vector3 p = go.transform.localPosition;
        p.y = 0f;
        float need = (go.name.Contains("Tree") || go.name.Contains("Pine")) ? 1.5f : 1.0f;
        Assert.GreaterOrEqual(DistToSegment(p, new Vector3(0f, 0f, -12f), new Vector3(0f, 0f, 0f)), need, go.name + " clear of entry");
        Assert.GreaterOrEqual(DistToSegment(p, new Vector3(0f, 0f, 0f), new Vector3(-16f, 0f, 5f)), need, go.name + " clear of garden line");
        Assert.GreaterOrEqual(DistToSegment(p, new Vector3(0f, 0f, 0f), new Vector3(15.5f, 0f, -5f)), need, go.name + " clear of bridge line");
        Assert.GreaterOrEqual(DistToSegment(p, new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 12f)), need, go.name + " clear of return");
      }
    } finally { TearDownMath(); }
  }
}
