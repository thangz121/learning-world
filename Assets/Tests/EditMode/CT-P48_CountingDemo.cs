// CT-P48: S3 P2 COUNTING DEMO PIONEER (ONE Number-2 living instruction).
// Pins: demo stage geometry (number/basket/2 apples/hidden result/camera
// markers), host contract (Tess-identity body, face, click-through, no
// gameplay surface), full sequence order + loop reset (driven headless via
// Step), camera beat + re-arm, and the no-gameplay-state firewall.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class CT_P48_CountingDemo {
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

  // Builds the garden + demo with null live refs (audio/camera/player absent).
  static void BuildDemo(out GameObject garden, out CountingGardenBuilder builder,
      out CountingDemo demo) {
    garden = new GameObject("P48GardenWorld");
    builder = garden.AddComponent<CountingGardenBuilder>();
    builder.BuildContent(garden.transform);
    demo = garden.AddComponent<CountingDemo>();
    demo.Build(builder, null, null, null);
  }

  static void TearDown(GameObject garden) {
    if (garden != null) Object.DestroyImmediate(garden);
  }

  // A. Stage geometry: the number, basket, EXACTLY 2 apples on pedestals, a
  // result that starts hidden, and demo camera markers inside the demo plot.
  [Test] public void P48A_DemoStageBuilt() {
    GameObject garden;
    CountingGardenBuilder builder;
    CountingDemo demo;
    BuildDemo(out garden, out builder, out demo);
    try {
      Assert.IsNotNull(demo, "demo controller built");
      Vector3 plot = builder.DemoStageCenter;
      Assert.IsNotNull(builder.DemoNumber, "big number 2 staged");
      Assert.Less(Dist2D(builder.DemoNumber.transform.localPosition, plot), 3.4f,
        "number lives inside the demo plot");
      Assert.IsTrue(builder.DemoNumber.transform.localPosition.y < 0.3f,
        "number stands on the floor, not floating");
      Assert.IsNotNull(builder.DemoBasket, "demo basket staged");
      Assert.Less(Dist2D(builder.DemoBasket.localPosition, plot), 3.4f,
        "basket lives inside the demo plot");
      Assert.IsNotNull(builder.DemoApple0, "demo apple 0");
      Assert.IsNotNull(builder.DemoApple1, "demo apple 1");
      Assert.IsNull(FindDeep(garden.transform, "CGDemoApple2"), "exactly 2 demo apples (no ambiguity)");
      Assert.Less(Dist2D(builder.DemoApple0.transform.localPosition, plot), 3.4f, "apple0 in plot");
      Assert.Less(Dist2D(builder.DemoApple1.transform.localPosition, plot), 3.4f, "apple1 in plot");
      Assert.Greater(Dist2D(builder.DemoApple0.transform.localPosition,
        builder.DemoApple1.transform.localPosition), 0.5f, "apples distinct, never stacked");
      Assert.IsNotNull(builder.DemoResult, "result group staged");
      Assert.IsFalse(builder.DemoResult.activeSelf, "result hidden until the apples land");
      Assert.IsNotNull(FindDeep(garden.transform, "CGDemoResultTwo"), "result shows 2");
      Assert.IsNotNull(FindDeep(garden.transform, "CGDemoResultCheckArm"), "result shows tick");
      Assert.IsNotNull(builder.DemoCam, "demo camera marker");
      Assert.IsNotNull(builder.DemoLook, "demo look marker");
      Assert.Less(builder.DemoCam.transform.localPosition.x, plot.x - 2f,
        "demo camera sits west (courtyard side), facing the stage");
      Assert.Greater(builder.DemoCam.transform.localPosition.y, 3f,
        "demo camera elevated for a readable stage frame");
    } finally { TearDown(garden); }
  }

  // B. Host contract: Tess-identity body + face, click-through, camera-facing
  // start, and NO gameplay surface (no click target, no quest/save/bus types).
  [Test] public void P48B_HostContractAndFirewall() {
    GameObject garden;
    CountingGardenBuilder builder;
    CountingDemo demo;
    BuildDemo(out garden, out builder, out demo);
    try {
      Assert.IsTrue(demo.HostBuilt, "demo host body built (TessVisual prefab)");
      Transform host = FindDeep(garden.transform, "CGDemoHost");
      Assert.IsNotNull(host, "host rooted in the garden scene");
      Assert.IsNotNull(host.GetComponentInChildren<Animator>(true), "host has Animator (PickUp/Celebrate)");
      Assert.IsNotNull(host.GetComponentInChildren<CharacterPresentation>(true), "host has face kit");
      Assert.IsNull(host.GetComponentInChildren<Collider>(true), "host click-through (no capsule/mesh colliders)");
      Assert.IsNull(host.GetComponentInChildren<Interactable>(true), "host is not clickable gameplay");
      Assert.IsNotNull(FindDeep(host, "CGDemoCarryAnchor"), "visible carry anchor on the host");
      Assert.IsFalse(typeof(IClickTarget).IsAssignableFrom(typeof(CountingDemo)),
        "demo is not a click target (player cannot drive it)");
      System.Reflection.FieldInfo[] fields = typeof(CountingDemo).GetFields(
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      foreach (System.Reflection.FieldInfo f in fields) {
        string tn = f.FieldType.FullName ?? f.FieldType.Name;
        Assert.IsFalse(tn.Contains("Quest") && !tn.Contains("DemoPhase"),
          "no quest types on the demo (" + f.Name + ":" + tn + ")");
        Assert.IsFalse(tn.Contains("Save") || tn.Contains("Mastery") || tn.Contains("Bus")
          || tn.Contains("Score") || tn.Contains("Manager"),
          "no progression/manager types on the demo (" + f.Name + ":" + tn + ")");
      }
      MicroWorldPortal[] portals = garden.GetComponentsInChildren<MicroWorldPortal>(true);
      Assert.AreEqual(1, portals.Length, "demo adds no portals (exit only, P47 intact)");
    } finally { TearDown(garden); }
  }

  // C. Full loop headless: phases visit in story order, NPC never teleports,
  // apples end home, result hides again, loop counter advances.
  [Test] public void P48C_SequenceLoopsClean() {
    GameObject garden;
    CountingGardenBuilder builder;
    CountingDemo demo;
    BuildDemo(out garden, out builder, out demo);
    try {
      List<DemoPhase> order = new List<DemoPhase>();
      DemoPhase last = demo.Phase;
      order.Add(last);
      Vector3 prevHost = FindDeep(garden.transform, "CGDemoHost").localPosition;
      float maxStep = 0f;
      bool sawResult = false;
      bool applesInBasket = false;
      int iter = 0;
      while (iter < 3000) {
        iter++;
        demo.Step(0.1f);
        if (demo.Phase != last) {
          last = demo.Phase;
          order.Add(last);
          if (last == DemoPhase.Celebrate) {
            // ShowResult just ran: the tick must be visible with both apples
            // resting by the basket (captured here, one beat after the reveal).
            sawResult = demo.ResultShown;
            float d0 = Dist2D(builder.DemoApple0.transform.localPosition,
              builder.DemoBasket.localPosition);
            float d1 = Dist2D(builder.DemoApple1.transform.localPosition,
              builder.DemoBasket.localPosition);
            applesInBasket = d0 < 0.6f && d1 < 0.6f;
          }
        }
        Vector3 hp = FindDeep(garden.transform, "CGDemoHost").localPosition;
        float step = Dist2D(hp, prevHost);
        if (step > maxStep) maxStep = step;
        prevHost = hp;
        if (demo.LoopCount >= 1 && demo.Phase == DemoPhase.LookNumber) break;
      }
      Assert.Less(iter, 3000, "loop completes (no stall)");
      Assert.GreaterOrEqual(demo.LoopCount, 1, "loop counter advances");
      DemoPhase[] story = { DemoPhase.LookNumber, DemoPhase.SayNumber, DemoPhase.WalkApples,
        DemoPhase.ArriveApples, DemoPhase.PickOne, DemoPhase.PickTwo, DemoPhase.CarryShow,
        DemoPhase.WalkBasket, DemoPhase.PlaceOne, DemoPhase.PlaceTwo, DemoPhase.ShowResult,
        DemoPhase.Celebrate, DemoPhase.HoldResult, DemoPhase.ResetBeat, DemoPhase.WalkStart,
        DemoPhase.Ready };
      int cursor = 0;
      foreach (DemoPhase p in order) {
        if (cursor < story.Length && p == story[cursor]) cursor++;
      }
      Assert.AreEqual(story.Length, cursor, "story order intact: 2 -> take 2 -> carry -> basket -> 2-tick");
      Assert.LessOrEqual(maxStep, 0.9f * 0.1f + 0.001f, "NPC never teleports (walk speed bound)");
      Assert.IsTrue(sawResult, "result appears only after both apples land");
      Assert.IsTrue(applesInBasket, "both apples visibly rest by the basket at result time");
      Assert.IsFalse(demo.ResultShown, "result hides again on reset");
      float h0 = Dist2D(builder.DemoApple0.transform.localPosition, builder.DemoAppleHome0);
      float h1 = Dist2D(builder.DemoApple1.transform.localPosition, builder.DemoAppleHome1);
      Assert.Less(h0, 0.05f, "apple0 home after reset");
      Assert.Less(h1, 0.05f, "apple1 home after reset");
    } finally { TearDown(garden); }
  }

  // D. Camera-first: walking up to the demo mouth frames the stage once, and
  // the beat re-arms after walking clear (ыгрок drives nothing else).
  [Test] public void P48D_CameraBeatAndRearm() {
    GameObject garden = new GameObject("P48GardenCam");
    try {
      CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
      builder.BuildContent(garden.transform);
      GameObject camGo = new GameObject("P48Camera");
      SmartCamera cam = camGo.AddComponent<SmartCamera>();
      GameObject playerGo = new GameObject("P48Player");
      // Garden island contract: live player positions carry the +120x offset.
      playerGo.transform.position = new Vector3(0f, 0f, 0f);
      CountingDemo demo = garden.AddComponent<CountingDemo>();
      demo.Build(builder, playerGo.transform, cam, null);
      for (int i = 0; i < 30; i++) demo.Step(0.1f);
      Assert.AreEqual(0, demo.DemoBeatsFired, "no beat while the child is away");
      playerGo.transform.position = CountingGardenBuilder.WorldOffset + builder.DemoMouth;
      for (int i = 0; i < 30; i++) demo.Step(0.1f);
      Assert.AreEqual(1, demo.DemoBeatsFired, "walking up frames the stage once");
      for (int i = 0; i < 30; i++) demo.Step(0.1f);
      Assert.AreEqual(1, demo.DemoBeatsFired, "beat does not spam while staying");
      playerGo.transform.position = new Vector3(60f, 0f, 0f);
      for (int i = 0; i < 30; i++) demo.Step(0.1f);
      playerGo.transform.position = CountingGardenBuilder.WorldOffset + builder.DemoMouth;
      for (int i = 0; i < 30; i++) demo.Step(0.1f);
      Assert.AreEqual(2, demo.DemoBeatsFired, "re-entering re-frames (re-arm)");
      Object.DestroyImmediate(camGo);
      Object.DestroyImmediate(playerGo);
    } finally { Object.DestroyImmediate(garden); }
  }
}
