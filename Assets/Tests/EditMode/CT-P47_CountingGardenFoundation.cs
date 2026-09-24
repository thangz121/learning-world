// CT-P47: S3 P1/P2V COUNTING GARDEN FOUNDATION (recomposed layout).
// Pins the visual-recomposition contract on top of the v2 lazy-scene contract:
// plaza + number stones, crescent of 4 garden beds + demo theatre, reward
// pocket, entry threshold/exit landmark, camera-first demo stage, and the
// no-gameplay firewall (static presentation only).
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

  // A. Zones/space exist with their Phase-1 roles (beds + demo + identity).
  [Test] public void P47A_FoundationZonesHaveRoles() {
    GameObject garden = new GameObject("P47GardenWorld");
    try {
      CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
      builder.BuildContent(garden.transform);
      // S3-P2Z12 re-pin: 6 plots (4 beds + demo theatre + number-stair hill).
      Assert.AreEqual(6, builder.ZoneCenters.Count, "six crescent plots (4 beds + demo + stair hill)");
      Vector3 demo = builder.ZoneCenters[2];
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
      // DEMO THEATRE: number board + 2 apples + basket + hidden result board.
      string[] demoKit = { "CGDemoBoardPanel", "CGDemoBoardL", "CGDemoBoardR",
        "CGDemoNumber2", "CGDemoBallField", "CGDemoBall0", "CGDemoBall4",
        "CGDemoBasket", "CGDemoResultFrame", "CGDemoResultTwo", "CGDemoResultCheckArm" };
      foreach (string n in demoKit) {
        Transform t = FindDeep(garden.transform, n);
        Assert.IsNotNull(t, "demo static " + n);
        // .position: result-board parts are nested under their group.
        Assert.Less(Dist2D(t.position, demo), 4.0f, n + " sits inside the demo theatre");
      }
      Assert.IsFalse(builder.DemoResult.activeSelf, "result hidden until the apples land");
      // GARDEN BEDS: soil + counted crops + numbered mouth post + fence.
      int[] bedIndices = { 0, 1, 3, 4 };
      int[] cropCounts = { 3, 4, 5, 2 };
      for (int i = 0; i < bedIndices.Length; i++) {
        int z = bedIndices[i];
        Vector3 center = builder.ZoneCenters[z];
        Assert.IsNotNull(FindDeep(garden.transform, "CGZone" + z + "Pad"), "bed soil " + z);
        Assert.IsNotNull(FindDeep(garden.transform, "CGZone" + z + "Fence0"), "bed fence " + z);
        Assert.IsNotNull(FindDeep(garden.transform, "CGZone" + z + "Anchor"), "bed mouth anchor " + z);
        Assert.IsNotNull(FindDeep(garden.transform, "CGZone" + z + "Post"), "numbered post " + z);
        for (int c = 0; c < cropCounts[i]; c++) {
          Transform crop = FindDeep(garden.transform, "CGZone" + z + "Crop" + c);
          Assert.IsNotNull(crop, "bed " + z + " crop " + c);
          Assert.Less(Dist2D(crop.localPosition, center), 2.2f, "crop inside its bed");
        }
        Assert.IsNotNull(FindDeep(garden.transform, "CGZone" + z + "PostBead" + z),
          "bed " + z + " mouth post carries " + (z + 1) + " beads");
      }
      // FEEDBACK / REWARD pocket + EXIT landmark.
      Assert.IsNotNull(FindDeep(garden.transform, "CGRewardDisc"), "reward disc");
      Assert.IsNotNull(FindDeep(garden.transform, "CGRewardPlinth"), "reward plinth (empty)");
      Assert.IsNotNull(FindDeep(garden.transform, "CGRewardBeam"), "reward garland beam");
      Assert.IsNotNull(FindDeep(garden.transform, "CGExitPostL"), "exit landmark L");
      Assert.IsNotNull(FindDeep(garden.transform, "CGExitPostR"), "exit landmark R");
      Assert.IsNotNull(FindDeep(garden.transform, "CGPathCrescent0"), "crescent walk");
      Assert.IsNotNull(FindDeep(garden.transform, "CGPathDemoSpur"), "demo approach walk");
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
      Assert.Less(Dist2D(a.GameplayFocus.localPosition, builder.DemoStageCenter), 0.1f,
        "focus anchor = demo theatre");
      Assert.Less(Dist2D(a.Npc.localPosition, CountingGardenBuilder.DemoNpcStart), 0.1f,
        "NPC anchor rides the demo stage start (the performance space)");
      Transform disc = FindDeep(garden.transform, "CGRewardDisc");
      Assert.Less(Dist2D(a.Reward.localPosition, disc.localPosition), 1.0f,
        "reward anchor rides the celebration medallion");
      Assert.Less(a.Camera.localPosition.z, a.Entry.localPosition.z,
        "arrival camera sits NORTH (behind) the entry");
      Assert.Greater(a.Camera.localPosition.y, 5f, "arrival camera elevated for world reveal");
      Assert.Greater(a.CameraLook.localPosition.z, 2f, "arrival look crosses the plaza");
      float camDist = Vector3.Distance(a.Camera.localPosition, a.CameraLook.localPosition);
      Assert.Greater(camDist, 10f, "arrival frames the whole garden, not a close-up");
      Assert.Less(camDist, 25f, "arrival keeps the world readable (kids-eye medium)");
      // Demo card camera: west of the stage, looking at it, lower/closer than
      // the arrival (an instruction card, not a landscape).
      Assert.Less(builder.DemoCam.localPosition.z, builder.DemoStageCenter.z,
        "demo camera sits on the plaza side of the stage");
      Assert.Less(builder.DemoCam.localPosition.y, 3f, "demo camera is at child-comfort height");
      Assert.Less(Dist2D(builder.DemoLook.localPosition, builder.DemoStageCenter), 1.5f,
        "demo look point is the stage centre");
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
      Vector3 plaza = CountingGardenBuilder.ArcCenter;
      Vector3 stage = builder.DemoStageCenter;
      // Orientation trio clears the entry walk + the plaza->demo axis.
      for (int i = 0; i < 3; i++) {
        Transform trio = FindDeep(garden.transform, "CGOrientTrio" + i);
        Assert.Greater(DistToSeg2D(trio.localPosition, entryA, entryB), 1.6f,
          "landmark trio off the entry walk");
        Assert.Greater(DistToSeg2D(trio.localPosition, plaza, stage), 1.4f,
          "landmark trio off the demo axis");
      }
      // Reward plinth + posts clear the entry walk and the demo axis.
      Assert.Greater(DistToSeg2D(FindDeep(garden.transform, "CGRewardPlinth").localPosition,
        plaza, stage), 2.0f, "reward plinth off the demo axis");
      Assert.Greater(DistToSeg2D(FindDeep(garden.transform, "CGRewardPostL").localPosition,
        entryA, entryB), 2.0f, "garland post off the entry walk");
      // Demo props stay in the stage's front row (never on the approach walk:
      // the spur runs (0,4.6)->(0,8.4), so measure against THAT segment).
      Vector3 spurA = new Vector3(0f, 0f, 4.6f), spurB = new Vector3(0f, 0f, 8.4f);
      foreach (string n in new[] { "CGDemoBoardPanel", "CGDemoBallField", "CGDemoBasket" }) {
        Transform t = FindDeep(garden.transform, n);
        Assert.Greater(DistToSeg2D(t.position, spurA, spurB), 0.9f,
          n + " sits past the stage mouth, not on the approach");
      }
      // Discs are thin walkable pads (never step walls for the climber).
      foreach (string n in new[] { "CGThresholdL", "CGNpcStagingDisc", "CGRewardDisc",
        "CGPlazaPad", "CGDemoStagePad" }) {
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
        "CGDemoBoardPanel", "CGRewardDisc", "CGExitPostL", "CGNumberStone4" };
      foreach (string n in foundation) {
        Transform t = FindDeep(garden.transform, n);
        Assert.Less(Dist2D(t.localPosition, hub), 16f, n + " fits the island (hedge r19)");
      }
      float entryToDemo = Dist2D(CountingGardenBuilder.EntryLocal, builder.DemoStageCenter);
      Assert.Greater(entryToDemo, 8f, "the garden is a place to explore, not a closet");
      Assert.Less(entryToDemo, 25f, "the garden is a stroll, not a hike (4yo legs)");
      // Crescent: uniform, walkable spacing between the activity plots.
      for (int i = 0; i < builder.ZoneCenters.Count - 1; i++) {
        float chord = Dist2D(builder.ZoneCenters[i], builder.ZoneCenters[i + 1]);
        Assert.Greater(chord, 4.5f, "plots keep their own identity (no overlap)");
        Assert.Less(chord, 8f, "plots stay neighbours (no dead walks)");
      }
    } finally { Object.DestroyImmediate(garden); }
  }
}
