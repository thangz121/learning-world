// CT-P47: S3 P1 COUNTING GARDEN FOUNDATION (micro-world foundation, NO gameplay).
// Pins the Phase-1 spatial blueprint on top of the v2 lazy-scene contract:
// roles per zone (demo/activity/reserve), orientation + reward pockets, entry
// threshold, exit landmark, NPC staging, camera-first anchors — and proves the
// foundation carries ZERO gameplay behaviour (static presentation only).
// C# 9.0 only.
using NUnit.Framework;
using UnityEngine;

public class CT_P47_CountingGardenFoundation {
  static Transform FindDeep(Transform t, string name) {
    if (t == null) return null;
    if (t.name == name) return t;
    for (int i = 0; i < t.childCount; i++) {
      Transform f = FindDeep(t.GetChild(i), name);
      if (f != null) return f;
    }
    return null;
  }

  static float Dist2D(Vector3 a, Vector3 b) {
    float dx = a.x - b.x, dz = a.z - b.z;
    return Mathf.Sqrt(dx * dx + dz * dz);
  }

  static float DistToSeg2D(Vector3 p, Vector3 a, Vector3 b) {
    float abx = b.x - a.x, abz = b.z - a.z;
    float len2 = abx * abx + abz * abz;
    if (len2 < 0.0001f) return Dist2D(p, a);
    float t = ((p.x - a.x) * abx + (p.z - a.z) * abz) / len2;
    t = Mathf.Clamp01(t);
    return Dist2D(p, new Vector3(a.x + abx * t, 0f, a.z + abz * t));
  }

  // A. Every Phase-1 zone/space exists and sits where the blueprint says.
  [Test] public void P47A_FoundationZonesHaveRoles() {
    GameObject garden = new GameObject("P47GardenWorld");
    try {
      CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
      builder.BuildContent(garden.transform);
      Assert.AreEqual(5, builder.ZoneCenters.Count, "five plots kept (v2 contract)");
      Vector3 demo = builder.ZoneCenters[2];
      Vector3 act1 = builder.ZoneCenters[1];
      Vector3 act3 = builder.ZoneCenters[3];
      // Entry threshold + orientation + staging.
      Assert.IsNotNull(FindDeep(garden.transform, "CGThresholdL"), "entry threshold L");
      Assert.IsNotNull(FindDeep(garden.transform, "CGThresholdR"), "entry threshold R");
      Assert.IsNotNull(FindDeep(garden.transform, "CGOrientTrio0"), "orientation landmark");
      Assert.IsNotNull(FindDeep(garden.transform, "CGNpcStagingDisc"), "NPC staging disc");
      // DEMO SPACE lives inside the centre plot (static board + 3 pedestals + result).
      string[] demoKit = { "CGDemoBoardPanel", "CGDemoBoardL", "CGDemoBoardR",
        "CGDemoTarget0", "CGDemoTarget1", "CGDemoTarget2", "CGDemoResultFrame" };
      foreach (string n in demoKit) {
        Transform t = FindDeep(garden.transform, n);
        Assert.IsNotNull(t, "demo static " + n);
        Assert.Less(Dist2D(t.localPosition, demo), 3.4f, n + " sits inside the demo plot");
      }
      // ACTIVITY SPACE: basket + apples + sign inside plots 1 and 3.
      string[] actKit = { "CGActivity1Basket", "CGActivity1Apple0", "CGActivity1Apple1",
        "CGActivity1SignCube", "CGActivity3Basket", "CGActivity3Apple0",
        "CGActivity3Apple1", "CGActivity3Apple2", "CGActivity3SignCube" };
      foreach (string n in actKit) {
        Transform t = FindDeep(garden.transform, n);
        Assert.IsNotNull(t, "activity static " + n);
        Vector3 home = n.Contains("CGActivity1") ? act1 : act3;
        Assert.Less(Dist2D(t.localPosition, home), 3.4f, n + " sits inside its activity plot");
      }
      // FEEDBACK / REWARD pocket + EXIT landmark.
      Assert.IsNotNull(FindDeep(garden.transform, "CGRewardDisc"), "reward disc");
      Assert.IsNotNull(FindDeep(garden.transform, "CGRewardPlinth"), "reward plinth (empty)");
      Assert.IsNotNull(FindDeep(garden.transform, "CGRewardBeam"), "reward garland beam");
      Assert.IsNotNull(FindDeep(garden.transform, "CGExitPostL"), "exit landmark L");
      Assert.IsNotNull(FindDeep(garden.transform, "CGExitPostR"), "exit landmark R");
      // Reserve plots 0/4 stay fenced enclosures (future phases).
      for (int z = 0; z < CountingGardenBuilder.ZoneCount; z++) {
        Assert.IsNotNull(FindDeep(garden.transform, "CGZone" + z + "Pad"), "reserve pad " + z);
        Assert.IsNotNull(FindDeep(garden.transform, "CGZone" + z + "Anchor"), "reserve anchor " + z);
      }
    } finally { Object.DestroyImmediate(garden); }
  }

  // B. Foundation carries ZERO gameplay: no interactables/presenters, no
  // managers, no second anchor system, exit portal only.
  [Test] public void P47B_NoGameplayBehaviour() {
    GameObject garden = new GameObject("P47GardenNoPlay");
    try {
      CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
      builder.BuildContent(garden.transform);
      Assert.IsNull(garden.GetComponentInChildren<Interactable>(),
        "no Interactable in Phase 1 (static only)");
      Assert.IsNull(garden.GetComponentInChildren<ApplePresenter>(), "no apple logic");
      Assert.IsNull(garden.GetComponentInChildren<BallPresenter>(), "no ball logic");
      Assert.IsNull(garden.GetComponentInChildren<FlowerPotPresenter>(), "no pot logic");
      Assert.IsNull(garden.GetComponentInChildren<DistractorChoice>(), "no choice logic");
      Assert.IsNull(garden.GetComponentInChildren<ProximityDiscovery>(), "no discovery logic");
      Assert.IsNull(garden.GetComponentInChildren<DestinationMarker>(), "no destination logic");
      Assert.IsNull(garden.GetComponentInChildren<WorldQuestionBubble>(), "no question UI");
      foreach (Transform t in garden.GetComponentsInChildren<Transform>(true)) {
        Assert.IsFalse(t.name.Contains("Manager"),
          "no new manager (" + t.name + " violates S3-P1 §19)");
      }
      MicroWorldPortal[] portals = garden.GetComponentsInChildren<MicroWorldPortal>(true);
      Assert.AreEqual(1, portals.Length, "only the exit portal lives in the garden scene");
      Assert.IsTrue(portals[0].ExitMode, "the single portal is the way home");
      ActivityAnchors[] registries = garden.GetComponentsInChildren<ActivityAnchors>(true);
      Assert.AreEqual(1, registries.Length, "one anchor registry (no second system)");
    } finally { Object.DestroyImmediate(garden); }
  }

  // C. Camera-first anchors: arrival sits behind entry and looks across the
  // courtyard to the arc; NPC/reward anchors sit on their staging visuals.
  [Test] public void P47C_AnchorsAndCameraComposition() {
    GameObject garden = new GameObject("P47GardenAnchors");
    try {
      CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
      builder.BuildContent(garden.transform);
      ActivityAnchors a = builder.Anchors;
      Assert.IsNotNull(a.Entry, "entry anchor");
      Assert.IsNotNull(a.GameplayFocus, "focus anchor");
      Assert.IsNotNull(a.Npc, "npc anchor");
      Assert.IsNotNull(a.Camera, "camera anchor");
      Assert.IsNotNull(a.CameraLook, "camera look anchor");
      Assert.IsNotNull(a.Prompt, "prompt anchor");
      Assert.IsNotNull(a.Feedback, "feedback anchor");
      Assert.IsNotNull(a.Reward, "reward anchor");
      Assert.IsNotNull(a.Exit, "exit anchor");
      Assert.AreEqual(CountingGardenBuilder.EntryLocal, a.Entry.localPosition, "entry = spawn");
      Transform staging = FindDeep(garden.transform, "CGNpcStagingDisc");
      Assert.Less(Dist2D(a.Npc.localPosition, staging.localPosition), 1.1f,
        "NPC anchor rides the staging disc");
      Transform disc = FindDeep(garden.transform, "CGRewardDisc");
      Assert.Less(Dist2D(a.Reward.localPosition, disc.localPosition), 1.0f,
        "reward anchor rides the celebration disc");
      Assert.Less(a.Camera.localPosition.z, a.Entry.localPosition.z,
        "arrival camera sits SOUTH (behind) the entry");
      Assert.Greater(a.Camera.localPosition.y, 5f, "arrival camera elevated for world reveal");
      Assert.Greater(a.CameraLook.localPosition.z, 2f, "arrival look crosses the courtyard");
      float camDist = Vector3.Distance(a.Camera.localPosition, a.CameraLook.localPosition);
      Assert.Greater(camDist, 10f, "arrival frames the whole yard, not a close-up");
      Assert.Less(camDist, 25f, "arrival keeps the action readable (kids-eye medium)");
    } finally { Object.DestroyImmediate(garden); }
  }

  // D. Walk corridors stay clear: solids sit off the entry walk + spokes with
  // room for the 0.5m agent; celebration/staging/threshold discs stay thin and
  // walkable; overhead dressing never cuts headroom.
  [Test] public void P47D_CorridorsClear() {
    GameObject garden = new GameObject("P47GardenCorridor");
    try {
      CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
      builder.BuildContent(garden.transform);
      Vector3 entryA = new Vector3(0f, 0f, -10f), entryB = new Vector3(0f, 0f, -1f);
      Vector3 spokeC = new Vector3(0f, 0f, 2f);
      Vector3 demoMouth = builder.ZoneCenters[2]
        + (CountingGardenBuilder.ArcCenter - builder.ZoneCenters[2]).normalized * 3.4f;
      // Orientation trio clears the central spoke (needs pathHalf 0.65 + half 0.35).
      for (int i = 0; i < 3; i++) {
        Transform trio = FindDeep(garden.transform, "CGOrientTrio" + i);
        Assert.Greater(DistToSeg2D(trio.localPosition, spokeC, demoMouth), 1.0f,
          "landmark trio off the demo walk");
      }
      // Reward plinth + posts clear the central spoke (agent radius 0.5).
      Transform plinth = FindDeep(garden.transform, "CGRewardPlinth");
      Assert.Greater(DistToSeg2D(plinth.localPosition, spokeC, demoMouth), 1.0f,
        "reward plinth off the walk");
      Assert.Greater(DistToSeg2D(FindDeep(garden.transform, "CGRewardPostL").localPosition,
        spokeC, demoMouth), 1.0f, "garland post off the walk");
      // Activity signs stand lateral off their plot mouth walks.
      for (int z = 1; z <= 3; z += 2) {
        string tag = (z == 1) ? "1" : "3";
        Vector3 mouth = builder.ZoneCenters[z]
          + (CountingGardenBuilder.ArcCenter - builder.ZoneCenters[z]).normalized * 3.4f;
        Transform sign = FindDeep(garden.transform, "CGActivity" + tag + "SignCube");
        Assert.Greater(DistToSeg2D(sign.localPosition, mouth, builder.ZoneCenters[z]), 0.95f,
          "number sign off the plot walk");
      }
      // Discs are thin walkable pads (never step walls for the climber).
      string[] pads = { "CGThresholdL", "CGNpcStagingDisc", "CGRewardDisc" };
      foreach (string n in pads) {
        Transform p = FindDeep(garden.transform, n);
        Assert.Less(p.localScale.y, 0.05f, n + " stays a walkable pad");
      }
      // Overhead dressing is bake-ignored (headroom rule; string-based lookup:
      // the EditMode test assembly has no AI.Navigation reference — P43/P45
      // pattern).
      foreach (string n in new[] { "CGRewardBeam", "CGRewardBlossom0", "CGExitCapL" }) {
        Transform t = FindDeep(garden.transform, n);
        Component mod = null;
        try { mod = t.GetComponent("NavMeshModifier"); } catch (System.Exception) { }
        Assert.IsNotNull(mod, n + " carries a NavMeshModifier");
        System.Reflection.PropertyInfo p = mod.GetType().GetProperty("ignoreFromBuild");
        Assert.IsNotNull(p, n + " exposes ignoreFromBuild");
        Assert.IsTrue((bool)p.GetValue(mod, null), n + " never cuts NavMesh headroom");
      }
      // Entry walk itself is unobstructed at its centreline.
      Assert.Greater(DistToSeg2D(builder.ZoneCenters[2], entryA, entryB), 2f,
        "sanity: demo plot far from the entry walk");
    } finally { Object.DestroyImmediate(garden); }
  }

  // E. World scale: everything fits the island; the child walks (not hikes).
  [Test] public void P47E_WorldScaleSanity() {
    GameObject garden = new GameObject("P47GardenScale");
    try {
      CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
      builder.BuildContent(garden.transform);
      Vector3 hub = CountingGardenBuilder.ArcCenter;
      string[] foundation = { "CGThresholdL", "CGOrientTrio1", "CGNpcStagingDisc",
        "CGDemoBoardPanel", "CGActivity1Basket", "CGActivity3Basket",
        "CGRewardDisc", "CGExitPostL" };
      foreach (string n in foundation) {
        Transform t = FindDeep(garden.transform, n);
        Assert.Less(Dist2D(t.localPosition, hub), 16f, n + " fits the island (hedge r19)");
      }
      float entryToDemo = Dist2D(CountingGardenBuilder.EntryLocal, builder.ZoneCenters[2]);
      Assert.Greater(entryToDemo, 8f, "the garden is a place to explore, not a closet");
      Assert.Less(entryToDemo, 25f, "the garden is a stroll, not a hike (4yo legs)");
    } finally { Object.DestroyImmediate(garden); }
  }
}
