// CT-P50: S3 P2X GARDEN ZONE FLOW (user order §47B).
// Pins the "pick a plot -> play its own arena" contract on top of the S2 lazy
// micro-world machinery: five click/proximity zone spots (only the demo plot
// opens play), the pink 2-button panel, the double-click cancel, the play
// arena scene (same authored lesson layout, its own exit door), and the
// EnterMicro/ExitMicro swap (anti-double-enter, honest failure).
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class CT_P50_GardenZoneFlow {
  sealed class FakeOps : ISceneOps {
    public readonly HashSet<string> Loaded = new HashSet<string>();
    public bool FailLoads;
    public bool IsLoaded(string sceneName) { return Loaded.Contains(sceneName); }
    public Task LoadAdditiveAsync(string sceneName) {
      if (FailLoads) throw new System.InvalidOperationException("fake load failure");
      Loaded.Add(sceneName);
      return Task.CompletedTask;
    }
    public Task UnloadAsync(string sceneName) {
      Loaded.Remove(sceneName);
      return Task.CompletedTask;
    }
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

  static float Dist2D(Vector3 a, Vector3 b) {
    float dx = a.x - b.x, dz = a.z - b.z;
    return Mathf.Sqrt(dx * dx + dz * dz);
  }

  static List<GardenZoneSpot> BuildSpots(out GameObject garden, out CountingGardenBuilder builder) {
    garden = new GameObject("P50GardenWorld");
    builder = garden.AddComponent<CountingGardenBuilder>();
    builder.BuildContent(garden.transform);
    return builder.ZoneSpots;
  }

  static void TearDown(GameObject go) {
    if (go != null) Object.DestroyImmediate(go);
  }

  // A. Six zones, one door each: every plot carries a click pad with a
  // collider (ClickRouter's door) that never touches the NavMesh; camera pair
  // present; the demo theatre (index 2), the stair hill (index 5) and the
  // carrot patch (index 0) have play enabled — gameplay #3 re-pin,
  // deliberately three staged plots.
  [Test] public void P50A_ZoneSpotsBuilt() {
    GameObject garden;
    CountingGardenBuilder builder;
    List<GardenZoneSpot> spots = BuildSpots(out garden, out builder);
    try {
      Assert.AreEqual(CountingGardenBuilder.ZoneCount, spots.Count, "one spot per crescent plot");
      int playCount = 0;
      for (int z = 0; z < spots.Count; z++) {
        GardenZoneSpot spot = spots[z];
        Assert.IsNotNull(spot, "spot " + z + " exists");
        Assert.IsNotNull(spot.GetComponent<Collider>(), "spot " + z + " is clickable");
        Component mod = null;
        try { mod = spot.GetComponent("NavMeshModifier"); } catch (System.Exception) { }
        Assert.IsNotNull(mod, "spot " + z + " is bake-ignored");
        System.Reflection.PropertyInfo p = mod.GetType().GetProperty("ignoreFromBuild");
        Assert.IsTrue((bool)p.GetValue(mod, null), "spot " + z + " never bakes");
        Assert.IsNotNull(spot.CameraAnchor, "spot " + z + " has a focus camera");
        Assert.IsNotNull(spot.LookAnchor, "spot " + z + " has a focus look point");
        // The demo theatre's mouth is the plaza viewing spot (its stage sits
        // 8.4m south) — the other four ride their crescent mouths.
        Vector3 mouth = (z == 2) ? builder.DemoMouth : builder.ZoneCenters[z];
        Assert.Less(Dist2D(spot.transform.position, mouth), 2.0f,
          "spot " + z + " rides its plot mouth");
        if (spot.playEnabled) playCount++;
      }
      Assert.AreEqual(3, playCount, "three staged plots open play (ball arena + stair hill + rabbit)");
      Assert.IsTrue(spots[2].playEnabled, "the play plot is the demo theatre (index 2)");
      Assert.IsTrue(spots[5].playEnabled, "the stair hill opens its own lazy play scene");
      Assert.IsTrue(spots[0].playEnabled, "the carrot patch opens its own rabbit play scene");
      // Each staged plot names its scene; skeleton plots stay empty.
      Assert.AreEqual(CountingPlayBuilder.SceneName, CountingGardenArea.PlaySceneFor(spots[2]),
        "the demo theatre opens the reference ball arena");
      Assert.AreEqual(StairHillBuilder.SceneName, CountingGardenArea.PlaySceneFor(spots[5]),
        "the stair hill opens StairPlayScene");
      Assert.AreEqual(RabbitPlayBuilder.SceneName, CountingGardenArea.PlaySceneFor(spots[0]),
        "the carrot patch opens RabbitPlayScene");
      Assert.AreEqual(CountingPlayBuilder.SceneName, CountingGardenArea.PlaySceneFor(spots[1]),
        "skeleton plots fall back to the default arena name (never a lookup failure)");
      Assert.AreEqual(DialogueLang.T("Counting stage", "Sân đếm"), GardenZoneSpot.NameOf(2),
        "zone names follow the plot identity (language-agnostic pin)");
      Assert.AreEqual(DialogueLang.T("Stair hill", "Đồi Bậc Thang"), GardenZoneSpot.NameOf(5),
        "the sixth plot is the number-stair hill");
    } finally { TearDown(garden); }
  }

  // B. Focus + the user-chosen double-click cancel (outside the panel).
  [Test] public void P50B_FocusAndDoubleClickCancel() {
    GameObject garden;
    CountingGardenBuilder builder;
    List<GardenZoneSpot> spots = BuildSpots(out garden, out builder);
    GameObject areaGo = new GameObject("P50Area");
    try {
      CountingGardenArea area = areaGo.AddComponent<CountingGardenArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.SetGarden(CountingGardenBuilder.WorldOffset + CountingGardenBuilder.EntryLocal,
        builder.Anchors, spots);
      Assert.IsFalse(area.IsFocused, "no focus before entering");
      area.FocusZone(2);
      Assert.IsFalse(area.IsFocused, "no focus while outside the garden");
      Assert.IsTrue(area.TryEnterForTests(), "enter the garden");
      area.FocusZone(2);
      Assert.IsTrue(area.IsFocused, "click focus set");
      Assert.AreEqual(2, area.FocusedZone, "focus remembers the zone");
      area.FocusZone(99);
      Assert.AreEqual(2, area.FocusedZone, "a bogus zone never clears the focus");
      Assert.IsFalse(area.RegisterOutsideClick(10.0f), "one click never cancels");
      Assert.IsTrue(area.IsFocused, "still focused after a single click");
      Assert.IsFalse(area.RegisterOutsideClick(11.0f), "a late second click is a new first click");
      Assert.IsTrue(area.IsFocused, "still focused");
      Assert.IsTrue(area.RegisterOutsideClick(11.3f), "two clicks within 0.4s cancel the focus");
      Assert.IsFalse(area.IsFocused, "focus cleared");
      Assert.IsFalse(area.RegisterOutsideClick(12.0f), "cancel cannot fire while unfocused");
    } finally {
      TearDown(areaGo);
      TearDown(garden);
    }
  }

  // C. Proximity focus: walking within 2m focuses the nearest plot; after a
  // cancel the spot only re-arms once the child walks clear (no ping-pong).
  [Test] public void P50C_ProximityFocusRearms() {
    GameObject garden;
    CountingGardenBuilder builder;
    List<GardenZoneSpot> spots = BuildSpots(out garden, out builder);
    GameObject areaGo = new GameObject("P50AreaProx");
    try {
      CountingGardenArea area = areaGo.AddComponent<CountingGardenArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.SetGarden(CountingGardenBuilder.WorldOffset + CountingGardenBuilder.EntryLocal,
        builder.Anchors, spots);
      Assert.IsTrue(area.TryEnterForTests(), "enter the garden");
      // The entry walk is far from every plot mouth (the demo mouth is 1.1m
      // from the ARC CENTRE by design, so "far" must be the entry side).
      Vector3 far = CountingGardenBuilder.EntryLocal;
      Assert.IsFalse(area.TickProximityForTests(far), "the entry walk focuses nothing");
      Vector3 bed0 = spots[0].transform.position;
      Assert.IsTrue(area.TickProximityForTests(bed0), "walking onto a plot focuses it");
      Assert.AreEqual(0, area.FocusedZone, "nearest plot wins");
      area.CancelFocus();
      Assert.IsFalse(area.TickProximityForTests(bed0), "standing on the plot does not re-focus");
      Assert.IsFalse(area.IsFocused, "cancel holds while inside the radius");
      area.TickProximityForTests(bed0 + new Vector3(0f, 0f, 4f));
      Assert.IsTrue(area.TickProximityForTests(bed0), "walking clear then back re-focuses");
      Assert.AreEqual(0, area.FocusedZone, "re-focus on the plot");
    } finally {
      TearDown(areaGo);
      TearDown(garden);
    }
  }

  // D. The panel: pink card with TWO buttons (play + back), built with its own
  // raycaster, hidden by default, play visible only for staged plots.
  [Test] public void P50D_PanelTwoButtons() {
    GameObject panelGo = new GameObject("P50Panel");
    try {
      GardenZonePanel panel = panelGo.AddComponent<GardenZonePanel>();
      panel.Build();
      Assert.IsTrue(panel.IsBuilt, "panel built");
      Assert.IsFalse(panel.IsOpen, "panel starts hidden");
      Assert.IsTrue(panel.HasPlayButton, "play button exists");
      Assert.IsTrue(panel.HasBackButton, "back button exists");
      Assert.IsNotNull(panelGo.GetComponentInChildren<GraphicRaycaster>(true),
        "panel carries a GraphicRaycaster (dead buttons were a real bug in LanguageDialog)");
      Button[] buttons = panelGo.GetComponentsInChildren<Button>(true);
      Assert.GreaterOrEqual(buttons.Length, 2, "two pressable boxes");
      panel.ShowFor(GardenZoneSpot.NameOf(2), true);
      Assert.IsTrue(panel.IsOpen, "focused play zone opens the panel");
      Assert.IsTrue(panel.PlayVisible, "staged plot offers Play");
      Assert.AreEqual(GardenZoneSpot.NameOf(2), panel.TitleText, "title names the zone");
      panel.ShowFor(GardenZoneSpot.NameOf(1), false);
      Assert.IsFalse(panel.PlayVisible, "skeleton plot has no Play door (user order)");
      Assert.IsTrue(panel.IsOpen, "skeleton plot still offers the way back");
      panel.Hide();
      Assert.IsFalse(panel.IsOpen, "back hides the panel");
    } finally { TearDown(panelGo); }
  }

  // E. Play travel REUSES the micro slot: the arena cannot load while the
  // garden micro is up (anti-double-enter) and is reached through the
  // ExitMicro -> EnterMicro pair; the subject return stays blocked meanwhile.
  [Test] public void P50E_PlaySwapUsesMicroSlot() {
    var ops = new FakeOps();
    var t = new WorldTransition(SubjectIds.Main);
    var math = new SubjectId("math");
    Assert.IsTrue(t.EnterAsync(ops, math, "MathScene").GetAwaiter().GetResult(), "subject loads");
    Assert.IsTrue(t.EnterMicroAsync(ops, CountingGardenBuilder.SceneName).GetAwaiter().GetResult(),
      "garden micro loads");
    Assert.IsFalse(t.EnterMicroAsync(ops, CountingPlayBuilder.SceneName).GetAwaiter().GetResult(),
      "the arena cannot stack onto the garden (one micro slot)");
    Assert.IsFalse(ops.Loaded.Contains(CountingPlayBuilder.SceneName), "no partial play load");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "garden unload is the swap's first half");
    Assert.IsTrue(t.EnterMicroAsync(ops, CountingPlayBuilder.SceneName).GetAwaiter().GetResult(),
      "arena loads into the freed slot");
    Assert.IsTrue(ops.Loaded.Contains("MathScene"), "the subject world stays loaded underneath");
    Assert.IsFalse(t.ReturnAsync(ops, "MathScene").GetAwaiter().GetResult(),
      "subject return is refused while the arena is up");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "arena exit unloads");
    Assert.IsTrue(t.EnterMicroAsync(ops, CountingGardenBuilder.SceneName).GetAwaiter().GetResult(),
      "the garden reloads on the way back");
    ops.FailLoads = true;
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "garden unload");
    Assert.IsFalse(t.EnterMicroAsync(ops, CountingPlayBuilder.SceneName).GetAwaiter().GetResult(),
      "a failed arena load reports false (area then reloads the garden)");
    Assert.IsNull(t.MicroScene, "the slot stays free after the failure (retry possible)");
  }

  // F. S3-P2Z3 (user order): the play arena is the GAME field ONLY — separate
  // island, its own exit door back to the garden, empty of the NPC demo (the
  // Number-2 lesson lives in the garden miniature now), anchors staged for the
  // child's future game design.
  [Test] public void P50F_PlayArenaScene() {
    GameObject arena = new GameObject("P50PlayWorld");
    try {
      CountingPlayBuilder builder = arena.AddComponent<CountingPlayBuilder>();
      builder.BuildContent(arena.transform);
      Assert.Greater(Vector3.Distance(CountingPlayBuilder.WorldOffset, CountingGardenBuilder.WorldOffset), 40f,
        "the arena is a separate island (no overlap with the garden)");
      Assert.Greater(Vector3.Distance(CountingPlayBuilder.WorldOffset, MathWorldBuilder.WorldOffset), 40f,
        "the arena is a separate island (no overlap with Math)");
      Assert.IsNotNull(builder.ExitPortal, "the arena has its way home");
      Assert.IsTrue(builder.ExitPortal.ExitMode, "it is an exit portal");
      Assert.IsTrue(builder.ExitPortal.PlayExit, "it returns to the GARDEN (not the Math hub)");
      Assert.AreEqual(CountingGardenArea.AreaId, builder.ExitPortal.areaId, "it targets the garden area");
      MicroWorldPortal[] portals = arena.GetComponentsInChildren<MicroWorldPortal>(true);
      Assert.AreEqual(1, portals.Length, "one door only");
      float spawnClear = Vector2.Distance(
        new Vector2(builder.EntryPoint.localPosition.x, builder.EntryPoint.localPosition.z),
        new Vector2(builder.ExitPortal.transform.localPosition.x, builder.ExitPortal.transform.localPosition.z));
      Assert.GreaterOrEqual(spawnClear, builder.ExitPortal.fireRadius + builder.ExitPortal.rearmMargin,
        "entry spawn clears the exit re-arm radius (J4 lesson)");
      // S3-P2Z4: the arena now stages the REFERENCE GAMEPLAY (board + natural
      // ball cluster + basket/count/result). NPCs are runtime components, so
      // the built scene stays free of controllers. Full pins: CT-P51A.
      Assert.IsNotNull(builder.Activity, "reference activity refs exposed");
      Assert.AreEqual(5, builder.Activity.Balls.Count, "five balls staged");
      Assert.IsNotNull(FindDeep(arena.transform, "CPNumber2"), "board 2 staged");
      Assert.IsNotNull(FindDeep(arena.transform, "CPBasket"), "basket staged");
      Assert.IsNotNull(FindDeep(arena.transform, "CPCountDisplay"), "count display staged");
      Assert.IsNull(arena.GetComponentInChildren<CountingDemo>(true), "no demo controller in the built scene");
      Assert.IsNull(arena.GetComponentInChildren<CountingGame>(true), "no game controller in the built scene");
      // Infrastructure + anchors survive.
      Assert.IsNotNull(builder.EntryPoint, "entry marker");
      Assert.IsNotNull(FindDeep(arena.transform, "CPGround"), "arena ground");
      Assert.IsNotNull(FindDeep(arena.transform, "CPPathStage"), "arena path");
      ActivityAnchors a = builder.Anchors;
      Assert.IsNotNull(a, "anchor registry");
      foreach (Transform slot in new[] { a.Entry, a.GameplayFocus, a.Npc, a.Camera, a.CameraLook,
          a.Prompt, a.Feedback, a.Reward, a.Exit }) {
        Assert.IsNotNull(slot, "anchor slot present");
      }
      Assert.Less(Dist2D(a.GameplayFocus.localPosition, builder.Activity.Center), 0.1f,
        "focus = the activity field centre");
      Assert.Greater(Dist2D(a.GameplayFocus.localPosition, builder.EntryPoint.localPosition), 4f,
        "the activity sits away from the spawn");
      Assert.Less(Dist2D(a.Exit.localPosition, CountingPlayBuilder.ExitLocal), 0.1f, "exit = the door");
      Assert.Less(a.Camera.localPosition.y, 8f, "arrival camera stays readable");
      Assert.Less(a.Camera.localPosition.z, a.Entry.localPosition.z, "arrival camera sits behind the spawn");
    } finally { TearDown(arena); }
  }

  // H. S3-P2Y redesign pins: every plot has a boundary + a moving vignette,
  // the demo runs as a pivot-compensated MINIATURE (always alive), and the
  // panel appears only after one full try-run of the lesson completes.
  [Test] public void P50H_MiniDemoAndPanelAfterTryRun() {
    GameObject garden;
    CountingGardenBuilder builder;
    List<GardenZoneSpot> spots = BuildSpots(out garden, out builder);
    GameObject areaGo = new GameObject("P50AreaGate");
    GameObject panelGo = new GameObject("P50PanelGate");
    try {
      // Boundaries: borders on every plot, fence ring on the demo plot.
      for (int z = 0; z < CountingGardenBuilder.ZoneCount; z++) {
        string border = (z == 2) ? "CGZone2Border" : "CGZone" + z + "Border";
        Assert.IsNotNull(FindDeep(garden.transform, border), "plot boundary " + z);
      }
      for (int i = 2; i < 9; i++) {
        Assert.IsNotNull(FindDeep(garden.transform, "CGZone2Fence" + i),
          "demo plot fence piece " + i);
      }
      // Moving vignettes on the four counted beds (beads + crops).
      int[] cropCounts = { 3, 4, 5, 2 };
      int[] bedIndices = { 0, 1, 3, 4 };
      for (int i = 0; i < bedIndices.Length; i++) {
        Transform vigT = FindDeep(garden.transform, "CGZone" + bedIndices[i] + "Vignette");
        Assert.IsNotNull(vigT, "bed vignette " + bedIndices[i]);
        GardenZoneVignette vig = vigT.GetComponent<GardenZoneVignette>();
        Assert.IsNotNull(vig, "vignette component " + bedIndices[i]);
        Assert.AreEqual(bedIndices[i] + 1, vig.BeadCount, "beads = bed number");
        Assert.AreEqual(cropCounts[i], vig.CropCount, "crops bound to the vignette");
      }
      // The miniature keeps the authored stage centre (pivot compensation) and
      // the actors/fx parent INSIDE it (so the whole lesson scales as one toy).
      Assert.IsNotNull(builder.DemoMiniRoot, "mini root built");
      Assert.Less(builder.DemoMiniRoot.localScale.x, 1f, "the garden stage is smaller than the arena");
      Vector3 stageWorld = builder.DemoMiniRoot.TransformPoint(builder.DemoStageCenter);
      Assert.Less(Dist2D(stageWorld, builder.DemoStageCenter), 0.01f,
        "pivot compensation keeps the stage centre in place");
      Assert.Less(Dist2D(builder.DemoCam.position, builder.DemoStageCenter), 3.0f,
        "shot A marker lives inside the miniature");
      // Demo + audience gate (S3-P2L2): the lesson acts only for a child at the
      // viewing spot, one pass per visit; camera beats off (zone focus owns it).
      CountingDemo demo = garden.AddComponent<CountingDemo>();
      demo.CameraBeatsEnabled = false;
      GameObject viewer = new GameObject("P50HViewer");
      viewer.transform.SetParent(garden.transform, true);
      viewer.transform.position = CountingGardenBuilder.WorldOffset + builder.DemoMouth;
      demo.Build(builder, viewer.transform, null, null);
      Assert.IsNotNull(demo.transform, "demo built");
      DemoPhase startPhase = demo.Phase;
      for (int i = 0; i < 30; i++) demo.Step(0.1f);
      Assert.AreNotEqual(startPhase, demo.Phase,
        "with the child at the viewing spot the lesson starts (audience gate)");
      CountingGardenArea area = areaGo.AddComponent<CountingGardenArea>();
      GardenZonePanel panel = panelGo.AddComponent<GardenZonePanel>();
      panel.Build();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.BindPanel(panel);
      area.SetGarden(CountingGardenBuilder.WorldOffset + CountingGardenBuilder.EntryLocal,
        builder.Anchors, spots);
      area.BindDemo(demo);
      Assert.IsTrue(area.TryEnterForTests(), "enter the garden");
      area.FocusZone(2);
      Assert.IsTrue(area.AwaitingDemo, "staged zone waits for the try-run first");
      Assert.IsFalse(panel.IsOpen, "panel NOT shown before the try-run (user order)");
      int guard = 0;
      while (demo.LoopCount < 1 && guard < 4000) { demo.Step(0.1f); guard++; }
      Assert.GreaterOrEqual(demo.LoopCount, 1, "the try-run completes");
      area.TickDemoGateForTests();
      Assert.IsFalse(area.AwaitingDemo, "gate consumed");
      Assert.IsTrue(panel.IsOpen, "panel appears after the demo finished");
      Assert.IsTrue(panel.PlayVisible, "the staged zone offers Play");
      // Skeleton zone: immediate panel, no play door (unchanged).
      area.CancelFocus();
      area.FocusZone(1);
      Assert.IsFalse(area.AwaitingDemo, "skeleton plots need no try-run");
      Assert.IsTrue(panel.IsOpen, "skeleton panel opens at once");
      Assert.IsFalse(panel.PlayVisible, "skeleton has no Play door");
    } finally {
      TearDown(panelGo);
      TearDown(areaGo);
      TearDown(garden);
    }
  }

  // G. Area play seams: play only from a focused STAGED plot, no double enter,
  // and the exit door refuses while in the garden / double-exits.
  [Test] public void P50G_AreaPlaySeams() {
    GameObject garden;
    CountingGardenBuilder builder;
    List<GardenZoneSpot> spots = BuildSpots(out garden, out builder);
    GameObject areaGo = new GameObject("P50AreaPlay");
    try {
      CountingGardenArea area = areaGo.AddComponent<CountingGardenArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.SetGarden(CountingGardenBuilder.WorldOffset + CountingGardenBuilder.EntryLocal,
        builder.Anchors, spots);
      Assert.IsFalse(area.TryEnterPlayForTests(), "cannot play from outside the garden");
      Assert.IsTrue(area.TryEnterForTests(), "enter the garden");
      Assert.IsFalse(area.TryEnterPlayForTests(), "cannot play without a focused zone");
      area.FocusZone(1);
      Assert.IsFalse(area.TryEnterPlayForTests(), "a skeleton plot never opens play");
      // Gameplay #3: the carrot patch is staged and opens its panel at once
      // (no garden try-run — the full lesson runs inside the rabbit arena).
      area.FocusZone(0);
      Assert.IsFalse(area.AwaitingDemo, "the rabbit plot needs no garden try-run");
      Assert.IsTrue(area.TryEnterPlayForTests(), "the carrot patch opens play");
      Assert.IsTrue(area.TryExitPlayForTests(), "back to the garden before the next focus");
      area.FocusZone(2);
      Assert.IsTrue(area.CanExit, "exit door available before play");
      Assert.IsTrue(area.TryEnterPlayForTests(), "staged plot opens play");
      Assert.IsFalse(area.CanExit, "the garden exit is unavailable while in the arena");
      Assert.IsFalse(area.TryEnterPlayForTests(), "double enter blocked");
      Assert.IsFalse(area.TryExitForTests(), "garden exit seam fails while in play");
      Assert.IsTrue(area.TryExitPlayForTests(), "the arena exit returns to the garden");
      Assert.IsFalse(area.TryExitPlayForTests(), "double arena exit blocked");
      Assert.IsTrue(area.CanExit, "garden exit available again");
      Assert.IsTrue(area.TryExitForTests(), "leave the garden");
      Assert.IsFalse(area.IsInside, "outside again");
    } finally {
      TearDown(areaGo);
      TearDown(garden);
    }
  }
}
