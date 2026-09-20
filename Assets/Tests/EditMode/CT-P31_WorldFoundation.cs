// CT-P31: Phase 3.0 WORLD FOUNDATION contracts. Owner: Lead/A. Must stay GREEN.
// Covers: SubjectId identity, SubjectCatalog (4 subjects, distinct visual
// identity + geometry, NO learning fields), WorldNavService (idempotent
// enter/return, full multi-subject loop, stress), SubjectGate one-way firing,
// WorldChangedEvent value semantics. C# 9.0 only.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CT_P31_WorldFoundation {
  // ---- SubjectId ------------------------------------------------------------

  [Test] public void CT_P31A_SubjectId_Normalizes() {
    Assert.AreEqual("math", new SubjectId("  Math ").Value);
    Assert.AreEqual("main", new SubjectId("").Value);
    Assert.AreEqual("main", new SubjectId((string)null).Value);
  }

  [Test] public void CT_P31B_SubjectId_Equality() {
    Assert.IsTrue(new SubjectId("math") == new SubjectId("MATH"));
    Assert.IsTrue(new SubjectId("math") != SubjectIds.English);
    Assert.IsTrue(SubjectIds.Main == new SubjectId("main"));
  }

  [Test] public void CT_P31C_FourDistinctSubjects() {
    var seen = new HashSet<string>();
    foreach (SubjectId s in SubjectIds.Subjects) seen.Add(s.Value);
    Assert.AreEqual(4, seen.Count, "math/thinking/english/vietnamese must be distinct");
    Assert.IsFalse(seen.Contains(SubjectIds.Main.Value), "Main is the hub, not a subject");
  }

  // ---- Catalog: identity + geometry ------------------------------------------

  [Test] public void CT_P31D_CatalogResolvesAll() {
    foreach (SubjectId s in SubjectIds.Subjects) {
      SubjectDefinition def = SubjectCatalog.Get(s);
      Assert.IsNotNull(def, "catalog must resolve " + s.Value);
      Assert.IsFalse(string.IsNullOrEmpty(def.DisplayName));
    }
    Assert.IsNull(SubjectCatalog.Get(new SubjectId("nope")));
  }

  [Test] public void CT_P31E_DistinctVisualIdentity() {
    var kinds = new HashSet<SubjectLandmarkKind>();
    var primaries = new List<Color>();
    var centers = new List<Vector3>();
    foreach (SubjectDefinition def in SubjectCatalog.All) {
      kinds.Add(def.Landmark);
      primaries.Add(def.Primary);
      centers.Add(def.PlaygroundCenter);
    }
    Assert.AreEqual(4, kinds.Count, "shape language must differ per subject (not just color)");
    for (int i = 0; i < primaries.Count; i++)
      for (int j = i + 1; j < primaries.Count; j++) {
        Color d = primaries[i] - primaries[j];
        float dist = d.r * d.r + d.g * d.g + d.b * d.b;
        Assert.IsTrue(dist > 0.01f, "primary palettes must differ");
      }
    for (int i = 0; i < centers.Count; i++)
      for (int j = i + 1; j < centers.Count; j++)
        Assert.Greater((centers[i] - centers[j]).magnitude, 5f,
          "playgrounds must be separate districts");
  }

  [Test] public void CT_P31F_GateEntryReturnGeometry() {
    // HUB-ARC contract (user round): the 4 entry gates stand TOGETHER hugging
    // the yard (~7m apart) so neighbours never occlude each other — all in
    // front of the spawn camera (never behind it). Entry sits just past its
    // gate toward the district; return sits inside its playground, away from
    // the entry gate.
    for (int i = 0; i < SubjectCatalog.All.Length; i++) {
      SubjectDefinition def = SubjectCatalog.All[i];
      Assert.LessOrEqual(Math.Abs(def.GatePos.x), 16f, def.DisplayName + " gate inside the outer bounds (x)");
      Assert.Greater(def.GatePos.z, -10.5f, def.DisplayName + " gate inside the outer bounds (z)");
      Assert.Less(def.GatePos.z, 5f, def.DisplayName + " gate in front of the spawn camera, never behind it");
      for (int j = i + 1; j < SubjectCatalog.All.Length; j++) {
        float gap = (def.GatePos - SubjectCatalog.All[j].GatePos).magnitude;
        Assert.Greater(gap, 6f, def.DisplayName + " vs " + SubjectCatalog.All[j].DisplayName
          + " arc spacing: neighbours must never occlude each other");
      }
      float gateToCenter = (def.GatePos - def.PlaygroundCenter).magnitude;
      float entryToCenter = (def.EntryPoint - def.PlaygroundCenter).magnitude;
      float gateToEntry = (def.GatePos - def.EntryPoint).magnitude;
      Assert.Greater(gateToCenter, 0.5f, "gate must stand off the playground");
      Assert.Less(gateToEntry, gateToCenter, "entry must lie between gate and center");
      Assert.Less(entryToCenter, gateToCenter, "entry must lie between gate and center");
      float returnToCenter = (def.ReturnPoint - def.PlaygroundCenter).magnitude;
      Assert.Less(returnToCenter, 3.5f, def.DisplayName + " return must sit inside its playground");
      Assert.Greater((def.ReturnPoint - def.GatePos).magnitude, 1.5f,
        "return must not overlap the entry gate");
    }
  }

  [Test] public void CT_P31G_NoLearningFieldsRoadmapGuard() {
    // Phase 3.0 MUST NOT pre-build learning architecture. This test fails the
    // moment anyone adds Lesson/Topic/Question/Activity/Curriculum here.
    string[] banned = { "Lesson", "Topic", "Question", "Activity", "Curriculum", "Answer", "Mastery" };
    foreach (string name in banned) {
      Assert.IsNull(typeof(SubjectDefinition).GetField(name),
        "SubjectDefinition must not carry " + name);
      Assert.IsNull(typeof(IWorldNavService).GetMethod("Get" + name),
        "IWorldNavService must not expose " + name);
    }
  }

  // ---- WorldNavService --------------------------------------------------------

  [Test] public void CT_P31H_InitialStateIsMain() {
    var nav = new WorldNavService(new GameEventBus());
    Assert.AreEqual(SubjectIds.Main, nav.Current);
    Assert.IsFalse(nav.IsInSubject);
  }

  [Test] public void CT_P31I_EnterPublishesOnce() {
    var bus = new GameEventBus();
    var seen = new List<WorldChangedEvent>();
    bus.Subscribe<WorldChangedEvent>(e => seen.Add(e));
    var nav = new WorldNavService(bus);
    nav.Enter(SubjectIds.Math);
    Assert.AreEqual(SubjectIds.Math, nav.Current);
    Assert.IsTrue(nav.IsInSubject);
    Assert.AreEqual(1, seen.Count);
    Assert.AreEqual(SubjectIds.Main, seen[0].From);
    Assert.AreEqual(SubjectIds.Math, seen[0].To);
  }

  [Test] public void CT_P31J_ReenterIsIdempotent() {
    var bus = new GameEventBus();
    int events = 0;
    bus.Subscribe<WorldChangedEvent>(_ => events++);
    var nav = new WorldNavService(bus);
    nav.Enter(SubjectIds.Math);
    nav.Enter(SubjectIds.Math);
    nav.Enter(SubjectIds.Math);
    Assert.AreEqual(1, events, "re-entering the current world must publish nothing");
  }

  [Test] public void CT_P31K_ReturnFromMainIsNoop() {
    var bus = new GameEventBus();
    int events = 0;
    bus.Subscribe<WorldChangedEvent>(_ => events++);
    var nav = new WorldNavService(bus);
    nav.ReturnToMain();
    Assert.AreEqual(0, events);
    Assert.AreEqual(SubjectIds.Main, nav.Current);
  }

  [Test] public void CT_P31L_FullMultiSubjectLoop() {
    var bus = new GameEventBus();
    var seen = new List<WorldChangedEvent>();
    bus.Subscribe<WorldChangedEvent>(e => seen.Add(e));
    var nav = new WorldNavService(bus);
    SubjectId[] loop = { SubjectIds.Math, SubjectIds.English, SubjectIds.Thinking, SubjectIds.Vietnamese };
    foreach (SubjectId s in loop) {
      nav.Enter(s);
      Assert.AreEqual(s, nav.Current);
      nav.ReturnToMain();
      Assert.AreEqual(SubjectIds.Main, nav.Current);
    }
    Assert.AreEqual(8, seen.Count, "4 enter + 4 return events");
    Assert.AreEqual(SubjectIds.Main, nav.Current);
    Assert.IsFalse(nav.IsInSubject);
  }

  [Test] public void CT_P31M_RepeatedTransitionStress() {
    var bus = new GameEventBus();
    int events = 0;
    bus.Subscribe<WorldChangedEvent>(_ => events++);
    var nav = new WorldNavService(bus);
    for (int round = 0; round < 5; round++) {
      foreach (SubjectId s in SubjectIds.Subjects) {
        nav.Enter(s);
        nav.ReturnToMain();
      }
    }
    Assert.AreEqual(40, events, "5 rounds x 4 subjects x (enter+return)");
    Assert.AreEqual(SubjectIds.Main, nav.Current);
  }

  [Test] public void CT_P31N_NullBusNeverThrows() {
    var nav = new WorldNavService(null);
    Assert.DoesNotThrow(() => nav.Enter(SubjectIds.Math));
    Assert.DoesNotThrow(() => nav.ReturnToMain());
    Assert.AreEqual(SubjectIds.Main, nav.Current, "state commits even without a bus");
  }

  [Test] public void CT_P31O_WorldChangedEvent_ValueSemantics() {
    DateTime at = DateTime.UtcNow;
    var a = new WorldChangedEvent(SubjectIds.Main, SubjectIds.Math, at);
    var b = new WorldChangedEvent(SubjectIds.Main, SubjectIds.Math, at);
    Assert.IsTrue(a == b);
    Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    Assert.IsTrue(a != new WorldChangedEvent(SubjectIds.Math, SubjectIds.Main, at));
  }

  // ---- SubjectGate one-way firing ---------------------------------------------

  [Test] public void CT_P31P_EntryGateFiresFromMain() {
    GameObject go = null;
    try {
      go = new GameObject("P31EntryGate");
      go.transform.position = SubjectCatalog.Math.GatePos;
      var bus = new GameEventBus();
      var nav = new WorldNavService(bus);
      SubjectGate gate = go.AddComponent<SubjectGate>();
      gate.Bind(nav, SubjectIds.Math, false, null);
      bool fired = gate.TryFireForTests(SubjectCatalog.Math.GatePos, SubjectIds.Main);
      Assert.IsTrue(fired);
      Assert.AreEqual(SubjectIds.Math, nav.Current);
    } finally {
      if (go != null) UnityEngine.Object.DestroyImmediate(go);
    }
  }

  [Test] public void CT_P31Q_EntryGateNoopWhenInside() {
    GameObject go = null;
    try {
      go = new GameObject("P31EntryGate2");
      go.transform.position = SubjectCatalog.Math.GatePos;
      var bus = new GameEventBus();
      int events = 0;
      bus.Subscribe<WorldChangedEvent>(_ => events++);
      var nav = new WorldNavService(bus);
      nav.Enter(SubjectIds.Math);
      SubjectGate gate = go.AddComponent<SubjectGate>();
      gate.Bind(nav, SubjectIds.Math, false, null);
      bool fired = gate.TryFireForTests(SubjectCatalog.Math.GatePos, SubjectIds.Math);
      Assert.IsFalse(fired, "walking out through the same gate must not ping-pong");
      Assert.AreEqual(1, events);
    } finally {
      if (go != null) UnityEngine.Object.DestroyImmediate(go);
    }
  }

  [Test] public void CT_P31R_ReturnGateFiresOnlyInOwnSubject() {
    GameObject go = null;
    try {
      go = new GameObject("P31ReturnGate");
      go.transform.position = SubjectCatalog.English.ReturnPoint;
      var bus = new GameEventBus();
      var nav = new WorldNavService(bus);
      SubjectGate gate = go.AddComponent<SubjectGate>();
      gate.Bind(nav, SubjectIds.English, true, null);
      Assert.IsFalse(gate.TryFireForTests(SubjectCatalog.English.ReturnPoint, SubjectIds.Main),
        "return must not fire from Main");
      nav.Enter(SubjectIds.English);
      Assert.IsTrue(gate.TryFireForTests(SubjectCatalog.English.ReturnPoint, SubjectIds.English));
      Assert.AreEqual(SubjectIds.Main, nav.Current);
    } finally {
      if (go != null) UnityEngine.Object.DestroyImmediate(go);
    }
  }

  [Test] public void CT_P31S_GateIgnoresOutOfRadius() {
    GameObject go = null;
    try {
      go = new GameObject("P31FarGate");
      go.transform.position = SubjectCatalog.Thinking.GatePos;
      var bus = new GameEventBus();
      var nav = new WorldNavService(bus);
      SubjectGate gate = go.AddComponent<SubjectGate>();
      gate.Bind(nav, SubjectIds.Thinking, false, null);
      Vector3 far = SubjectCatalog.Thinking.GatePos + new Vector3(5f, 0f, 0f);
      Assert.IsFalse(gate.TryFireForTests(far, SubjectIds.Main));
      Assert.AreEqual(SubjectIds.Main, nav.Current);
    } finally {
      if (go != null) UnityEngine.Object.DestroyImmediate(go);
    }
  }
}
