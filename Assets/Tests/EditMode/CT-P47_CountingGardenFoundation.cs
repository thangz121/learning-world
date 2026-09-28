// CT-P47: COUNTING GARDEN FOUNDATION (two-plot layout).
// Pins the layout contract on top of the v2 lazy-scene contract: plaza +
// number stones, crescent of 2 plots (carrot bed + number-stair hill), reward
// pocket, entry threshold/exit landmark, and the no-gameplay firewall
// (static presentation only).
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

  // A. Zones/space exist with their Phase-1 roles (carrot bed + stair hill).
  [Test] public void P47A_FoundationZonesHaveRoles() {
    GameObject garden = new GameObject("P47GardenWorld");
    try {
      CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
      builder.BuildContent(garden.transform);
      Assert.AreEqual(2, builder.ZoneCenters.Count, "two crescent plots (carrot + stair hill)");
      // Entry threshold + orientation + staging.
      Assert.IsNotNull(FindDeep(garden.transform, "CGThresholdL"), "entry threshold L");
      Assert.IsNotNull(FindDeep(garden.transform, "CGThresholdR"), "entry threshold R");
      Assert.IsNotNull(FindDeep(garden.transform, "CGOrientTrio0"), "orientation landmark");
      Assert.IsNotNull(FindDeep(garden.transform, "CGNpcStagingDisc"), "NPC staging disc");
      Assert.IsNotNull(FindDeep(garden.transform, "CGPlazaPad"), "orientation plaza");
      // Counting identity: number stones 1..5 (beads per stone).
      for (int i = 0; i < 5; i++) {
        Assert.IsNotNull(FindDeep(garden.transform, "CGNumberStone" + i), "number stone " + i);
        Assert.IsNotNull(FindDeep(garden.transform, "CGNumberStone" + i + "Bead" + i),
          "number stone " + i + " carries " + (i + 1) + " beads");
      }
      // CARROT BED (zone 0): soil + counted crops + numbered mouth post + fence.
      Vector3 carrot = builder.ZoneCenters[0];
      Assert.IsNotNull(FindDeep(garden.transform, "CGZone0Pad"), "carrot soil");
      Assert.IsNotNull(FindDeep(garden.transform, "CGZone0Fence0"), "carrot fence");
      Assert.IsNotNull(FindDeep(garden.transform, "CGZone0Anchor"), "carrot mouth anchor");
      Assert.IsNotNull(FindDeep(garden.transform, "CGZone0Post"), "carrot numbered post");
      for (int c = 0; c < 3; c++) {
        Transform crop = FindDeep(garden.transform, "CGZone0Crop" + c);
        Assert.IsNotNull(crop, "carrot crop " + c);
        Assert.Less(Dist2D(crop.localPosition, carrot), 2.2f, "crop inside the carrot bed");
      }
      Assert.IsNotNull(FindDeep(garden.transform, "CGZone0PostBead0"), "carrot post bead 0");
      // STAIR HILL (zone 1): the second plot keeps the bed contract.
      Assert.IsNotNull(FindDeep(garden.transform, "CGZone1Border"), "stair border");
      Assert.IsNotNull(FindDeep(garden.transform, "CGZone1Pad"), "stair pad");
      Assert.IsNotNull(FindDeep(garden.transform, "CGZone1Fence0"), "stair fence");
      Assert.IsNotNull(FindDeep(garden.transform, "CGZone1Anchor"), "stair mouth anchor");
      // FEEDBACK / REWARD pocket + EXIT landmark + walks.
      Assert.IsNotNull(FindDeep(garden.transform, "CGRewardDisc"), "reward disc");
      Assert.IsNotNull(FindDeep(garden.transform, "CGRewardPlinth"), "reward plinth (empty)");
      Assert.IsNotNull(FindDeep(garden.transform, "CGRewardBeam"), "reward garland beam");
      Assert.IsNotNull(FindDeep(garden.transform, "CGExitPostL"), "exit landmark L");
      Assert.IsNotNull(FindDeep(garden.transform, "CGExitPostR"), "exit landmark R");
      Assert.IsNotNull(FindDeep(garden.transform, "CGPathCrescent0"), "crescent walk");
      Assert.IsNotNull(FindDeep(garden.transform, "CGPathCarrotSpur"), "carrot approach walk");
    } finally { Object.DestroyImmediate(garden); }
  }

  // B. Foundation carries ZERO gameplay: no interactables/presenters/managers,
  // no second anchor system, exit portal only.
  [Test] public void P47B_NoGameplayBehaviour() {
    GameObject garden = new GameObject("P47GardenNoPlay");
    try {
      CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
      builder.BuildContent(garden.transform);
      Assert.IsNull(garden.GetComponentInChildren<Interactable>(),
        "no Interactable in Phase 1/2V (static only)");
      Assert.IsNull(garden.GetComponentInChildren<ApplePresenter>(), "no apple logic");
      Assert.IsNull(garden.GetComponentInChildren<BallPresenter>(), "no ball logic");
      Assert.IsNull(garden.GetComponentInChildren<FlowerPotPresenter>(), "no pot logic");
      Assert.IsNull(garden.GetComponentInChildren<DistractorChoice>(), "no choice logic");
      Assert.IsNull(garden.GetComponentInChildren<ProximityDiscovery>(), "no discovery logic");
      Assert.IsNull(garden.GetComponentInChildren<DestinationMarker>(), "no destination logic");
      Assert.IsNull(garden.GetComponentInChildren<WorldQuestionBubble>(), "no question UI");
      foreach (Transform t in garden.GetComponentsInChildren<Transform>(true)) {
        Assert.IsFalse(t.name.Contains("Manager"),
          "no new manager (" + t.name + " violates the phase scope)");
      }
      MicroWorldPortal[] portals = garden.GetComponentsInChildren<MicroWorldPortal>(true);
      Assert.AreEqual(1, portals.Length, "only the exit portal lives in the garden scene");
      Assert.IsTrue(portals[0].ExitMode, "the single portal is the way home");
      ActivityAnchors[] registries = garden.GetComponentsInChildren<ActivityAnchors>(true);
      Assert.AreEqual(1, registries.Length, "one anchor registry (no second system)");
    } finally { Object.DestroyImmediate(garden); }
  }

  // C. Camera-first anchors: arrival camera reveals the world from the entry
  // side; NPC/reward anchors ride their staging visuals; demo markers sit at
  // the stage (and never fight the arrival framing).
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
      Assert.Less(Dist2D(a.GameplayFocus.localPosition, CountingGardenBuilder.ArcCenter), 0.1f,
        "focus anchor = plaza heart");
      Transform disc = FindDeep(garden.transform, "CGNpcStagingDisc");
      Assert.Less(Dist2D(a.Npc.localPosition, disc.localPosition), 0.1f,
        "NPC anchor rides the staging disc");
      Transform reward = FindDeep(garden.transform, "CGRewardDisc");
      Assert.Less(Dist2D(a.Reward.localPosition, reward.localPosition), 1.0f,
        "reward anchor rides the celebration medallion");
      Assert.Less(a.Camera.localPosition.z, a.Entry.localPosition.z,
        "arrival camera sits NORTH (behind) the entry");
      Assert.Greater(a.Camera.localPosition.y, 5f, "arrival camera elevated for world reveal");
      Assert.Greater(a.CameraLook.localPosition.z, 2f, "arrival look crosses the plaza");
      float camDist = Vector3.Distance(a.Camera.localPosition, a.CameraLook.localPosition);
      Assert.Greater(camDist, 10f, "arrival frames the whole garden, not a close-up");
      Assert.Less(camDist, 25f, "arrival keeps the world readable (kids-eye medium)");
    } finally { Object.DestroyImmediate(garden); }
  }

  // D. Walk corridors stay clear: solids sit off the entry walk/plaza sight
  // lines, pads stay thin/walkable, overhead dressing never cuts headroom.
  [Test] public void P47D_CorridorsClear() {
    GameObject garden = new GameObject("P47GardenCorridor");
    try {
      CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
      builder.BuildContent(garden.transform);
      Vector3 entryA = new Vector3(0f, 0f, -11f), entryB = new Vector3(0f, 0f, -1f);
      // Orientation trio clears the entry walk.
      for (int i = 0; i < 3; i++) {
        Transform trio = FindDeep(garden.transform, "CGOrientTrio" + i);
        Assert.Greater(DistToSeg2D(trio.localPosition, entryA, entryB), 1.6f,
          "landmark trio off the entry walk");
      }
      // Reward garland post clears the entry walk.
      Assert.Greater(DistToSeg2D(FindDeep(garden.transform, "CGRewardPostL").localPosition,
        entryA, entryB), 2.0f, "garland post off the entry walk");
      // Discs are thin walkable pads (never step walls for the climber).
      foreach (string n in new[] { "CGThresholdL", "CGNpcStagingDisc", "CGRewardDisc",
        "CGPlazaPad" }) {
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
        "CGRewardDisc", "CGExitPostL", "CGNumberStone4" };
      foreach (string n in foundation) {
        Transform t = FindDeep(garden.transform, n);
        Assert.Less(Dist2D(t.localPosition, hub), 16f, n + " fits the island (hedge r19)");
      }
      float entryToPlots = Dist2D(CountingGardenBuilder.EntryLocal, builder.ZoneCenters[1]);
      Assert.Greater(entryToPlots, 8f, "the garden is a place to explore, not a closet");
      Assert.Less(entryToPlots, 25f, "the garden is a stroll, not a hike (4yo legs)");
      // Crescent: the two plots keep their own identity (no overlap).
      float chord = Dist2D(builder.ZoneCenters[0], builder.ZoneCenters[1]);
      Assert.Greater(chord, 4.5f, "plots keep their own identity (no overlap)");
      Assert.Less(chord, 16f, "plots stay within the same plaza (no dead walks)");
    } finally { Object.DestroyImmediate(garden); }
  }
}
