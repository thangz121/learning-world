// CT-P50: S3 P2X GARDEN ZONE FLOW (user order §47B).
// Pins the "pick a plot -> play its own arena" contract on top of the S2 lazy
// micro-world machinery: two click/proximity zone spots (both open play), the
// pink 2-button panel, the double-click cancel, the play arena swap through
// the shared micro slot, and the EnterMicro/ExitMicro swap (anti-double-enter,
// honest failure).
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

  // A. Two zones, one door each: every plot carries a click pad with a
  // collider (ClickRouter's door) that never touches the NavMesh; camera pair
  // present; both staged plots (carrot/rabbit zone 0, stair hill zone 1) open
  // play.
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
        Vector3 mouth = builder.ZoneCenters[z];
        Assert.Less(Dist2D(spot.transform.position, mouth), 2.0f,
          "spot " + z + " rides its plot mouth");
        if (spot.playEnabled) playCount++;
      }
      Assert.AreEqual(2, playCount, "both staged plots open play (rabbit + stair)");
      Assert.IsTrue(spots[0].playEnabled, "the carrot patch opens its own rabbit play scene");
      Assert.IsTrue(spots[1].playEnabled, "the stair hill opens its own lazy play scene");
      // Each staged plot names its own lazy scene.
      Assert.AreEqual(RabbitPlayBuilder.SceneName, CountingGardenArea.PlaySceneFor(spots[0]),
        "the carrot patch opens RabbitPlayScene");
      Assert.AreEqual(StairHillBuilder.SceneName, CountingGardenArea.PlaySceneFor(spots[1]),
        "the stair hill opens StairPlayScene");
      Assert.IsTrue(spots[0].demoGate && spots[1].demoGate,
        "both staged plots preview through their garden miniature first");
      Assert.AreEqual(DialogueLang.T("Carrot patch", "Vườn cà rốt"), GardenZoneSpot.NameOf(0),
        "zone names follow the plot identity (language-agnostic pin)");
      Assert.AreEqual(DialogueLang.T("Stair hill", "Đồi Bậc Thang"), GardenZoneSpot.NameOf(1),
        "the second plot is the number-stair hill");
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
      area.FocusZone(0);
      Assert.IsFalse(area.IsFocused, "no focus while outside the garden");
      Assert.IsTrue(area.TryEnterForTests(), "enter the garden");
      area.FocusZone(0);
      Assert.IsTrue(area.IsFocused, "click focus set");
      Assert.AreEqual(0, area.FocusedZone, "focus remembers the zone");
      area.FocusZone(99);
      Assert.AreEqual(0, area.FocusedZone, "a bogus zone never clears the focus");
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
      panel.ShowFor(GardenZoneSpot.NameOf(0), true);
      Assert.IsTrue(panel.IsOpen, "focused play zone opens the panel");
      Assert.IsTrue(panel.PlayVisible, "staged plot offers Play");
      Assert.AreEqual(GardenZoneSpot.NameOf(0), panel.TitleText, "title names the zone");
      panel.ShowFor(GardenZoneSpot.NameOf(1), false);
      Assert.IsFalse(panel.PlayVisible, "a look-only plot has no Play door");
      Assert.IsTrue(panel.IsOpen, "a look-only plot still offers the way back");
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
    Assert.IsFalse(t.EnterMicroAsync(ops, RabbitPlayBuilder.SceneName).GetAwaiter().GetResult(),
      "the arena cannot stack onto the garden (one micro slot)");
    Assert.IsFalse(ops.Loaded.Contains(RabbitPlayBuilder.SceneName), "no partial play load");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "garden unload is the swap's first half");
    Assert.IsTrue(t.EnterMicroAsync(ops, RabbitPlayBuilder.SceneName).GetAwaiter().GetResult(),
      "arena loads into the freed slot");
    Assert.IsTrue(ops.Loaded.Contains("MathScene"), "the subject world stays loaded underneath");
    Assert.IsFalse(t.ReturnAsync(ops, "MathScene").GetAwaiter().GetResult(),
      "subject return is refused while the arena is up");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "arena exit unloads");
    Assert.IsTrue(t.EnterMicroAsync(ops, CountingGardenBuilder.SceneName).GetAwaiter().GetResult(),
      "the garden reloads on the way back");
    ops.FailLoads = true;
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "garden unload");
    Assert.IsFalse(t.EnterMicroAsync(ops, RabbitPlayBuilder.SceneName).GetAwaiter().GetResult(),
      "a failed arena load reports false (area then reloads the garden)");
    Assert.IsNull(t.MicroScene, "the slot stays free after the failure (retry possible)");
  }

  // H. Two-plot redesign pins: every plot has a boundary + a moving vignette,
  // the stair plot's garden miniature drives the "panel after the try-run"
  // gate, and a plot without a demo opens its panel at once.
  [Test] public void P50H_MiniDemoAndPanelAfterTryRun() {
    GameObject garden;
    CountingGardenBuilder builder;
    List<GardenZoneSpot> spots = BuildSpots(out garden, out builder);
    GameObject areaGo = new GameObject("P50AreaGate");
    GameObject panelGo = new GameObject("P50PanelGate");
    GameObject demoGo = new GameObject("P50StairDemo");
    try {
      // Boundaries: a border on each plot.
      for (int z = 0; z < CountingGardenBuilder.ZoneCount; z++) {
        Assert.IsNotNull(FindDeep(garden.transform, "CGZone" + z + "Border"),
          "plot boundary " + z);
      }
      // Moving vignette on the carrot bed (beads = 1, three counted carrots).
      Transform vigT = FindDeep(garden.transform, "CGZone0Vignette");
      Assert.IsNotNull(vigT, "carrot bed vignette");
      GardenZoneVignette vig = vigT.GetComponent<GardenZoneVignette>();
      Assert.IsNotNull(vig, "vignette component");
      Assert.AreEqual(1, vig.BeadCount, "carrot bed carries one bead");
      Assert.AreEqual(3, vig.CropCount, "carrots bound to the vignette");
      // The stair hill previews through its own garden miniature.
      demoGo.transform.SetParent(garden.transform, false);
      StairLessonDemo demo = demoGo.AddComponent<StairLessonDemo>();
      demo.Build(builder, null, null);
      CountingGardenArea area = areaGo.AddComponent<CountingGardenArea>();
      GardenZonePanel panel = panelGo.AddComponent<GardenZonePanel>();
      panel.Build();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.BindPanel(panel);
      area.SetGarden(CountingGardenBuilder.WorldOffset + CountingGardenBuilder.EntryLocal,
        builder.Anchors, spots);
      area.BindDemo(CountingGardenBuilder.StairZoneIndex, demo);
      Assert.IsTrue(area.TryEnterForTests(), "enter the garden");
      area.FocusZone(CountingGardenBuilder.StairZoneIndex);
      Assert.IsTrue(area.AwaitingDemo, "the stair plot waits for the try-run first");
      Assert.IsFalse(panel.IsOpen, "panel NOT shown before the try-run (user order)");
      demo.StartFocusedLesson();
      int guard = 0;
      while (demo.LoopCount < 1 && guard < 4000) { demo.Step(0.1f); guard++; }
      Assert.GreaterOrEqual(demo.LoopCount, 1, "the try-run completes");
      area.TickDemoGateForTests();
      Assert.IsFalse(area.AwaitingDemo, "gate consumed");
      Assert.IsTrue(panel.IsOpen, "panel appears after the demo finished");
      Assert.IsTrue(panel.PlayVisible, "the staged zone offers Play");
      // A plot without a bound demo (carrot here): immediate panel, play door.
      area.CancelFocus();
      area.FocusZone(CountingGardenBuilder.RabbitZoneIndex);
      Assert.IsFalse(area.AwaitingDemo, "a plot without a demo needs no try-run");
      Assert.IsTrue(panel.IsOpen, "the carrot panel opens at once");
      Assert.IsTrue(panel.PlayVisible, "the carrot patch offers Play");
    } finally {
      TearDown(demoGo);
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
      area.FocusZone(99);
      Assert.IsFalse(area.TryEnterPlayForTests(), "a bogus zone never opens play");
      // The carrot patch opens its play door (no demo bound: panel at once).
      area.FocusZone(CountingGardenBuilder.RabbitZoneIndex);
      Assert.IsFalse(area.AwaitingDemo, "no garden try-run bound for the carrot patch");
      Assert.IsTrue(area.TryEnterPlayForTests(), "the carrot patch opens play");
      Assert.IsTrue(area.TryExitPlayForTests(), "back to the garden before the next focus");
      area.FocusZone(CountingGardenBuilder.StairZoneIndex);
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
