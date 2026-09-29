// CT-S13: FULL ARCHITECTURE RESET — PHASE 2 (Subject Selection, A).
// The Main world is the Subject Yard: FIVE gates (Toán, Tư duy, Tiếng Việt,
// Tiếng Anh, Khám phá) that open the subject's SKILL yard through the shared
// SelectionYardArea. Pins: catalog ↔ taxonomy consistency, the 5-gate arc
// geometry (7m neighbours, all in front of spawn), and the gate→yard wiring
// (legacy world-nav is never touched by a yard-bound entry gate).
// C# 9.0 only.
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class CT_S13_SubjectYard {
  // A. One taxonomy, three views: SubjectIds, SubjectCatalog and LearningMap
  // must agree on the five subjects — a mismatch would open the wrong yard.
  [Test] public void S13A_CatalogMatchesTaxonomy() {
    Assert.AreEqual(5, SubjectIds.Subjects.Length, "five canonical subjects");
    Assert.AreEqual(5, SubjectCatalog.All.Length, "five catalog gates");
    Assert.AreEqual(SubjectIds.Subjects.Length, SubjectCatalog.All.Length, "1:1 slots");
    var seen = new HashSet<string>();
    for (int i = 0; i < SubjectCatalog.All.Length; i++) {
      SubjectDefinition def = SubjectCatalog.All[i];
      Assert.AreEqual(SubjectIds.Subjects[i], def.Id, "catalog order matches SubjectIds (" + def.DisplayName + ")");
      Assert.IsTrue(seen.Add(def.Id.Value), "catalog ids unique");
      Assert.IsNotNull(SubjectCatalog.Get(def.Id), "catalog resolves " + def.Id.Value);
      Assert.IsNotNull(LearningMap.Subject(def.Id.Value), "taxonomy knows " + def.Id.Value);
      Assert.IsFalse(string.IsNullOrEmpty(def.DisplayName), "subject has a gate label");
      Assert.IsFalse(string.IsNullOrEmpty(LearningMap.SkillYardTitle(def.Id.Value)),
        "every subject has a skill-yard title");
    }
    Assert.IsTrue(seen.Contains("exploration"), "KHÁM PHÁ gate exists");
    Assert.IsNull(SubjectCatalog.Get(new SubjectId("nope")), "unknown subject never resolves");
  }

  // B. The 5-gate arc: 7m spacing (no occlusion), all in front of the spawn
  // camera, distinct landmark per gate, centre slot = Khám phá.
  [Test] public void S13B_FiveGateArc() {
    for (int i = 0; i < SubjectCatalog.All.Length; i++) {
      SubjectDefinition def = SubjectCatalog.All[i];
      Assert.LessOrEqual(Mathf.Abs(def.GatePos.x), 16f, def.DisplayName + " inside x bounds");
      Assert.Less(def.GatePos.z, 5f, def.DisplayName + " in front of the spawn camera");
      Assert.Greater(def.GatePos.z, -10.5f, def.DisplayName + " inside z bounds");
      for (int j = i + 1; j < SubjectCatalog.All.Length; j++) {
        float gap = (def.GatePos - SubjectCatalog.All[j].GatePos).magnitude;
        Assert.Greater(gap, 6f, "neighbours never occlude: " + def.DisplayName
          + " vs " + SubjectCatalog.All[j].DisplayName + " (" + gap.ToString("F2") + "m)");
      }
    }
    var kinds = new HashSet<SubjectLandmarkKind>();
    for (int i = 0; i < SubjectCatalog.All.Length; i++) kinds.Add(SubjectCatalog.All[i].Landmark);
    Assert.AreEqual(5, kinds.Count, "five distinct landmark shapes");
    SubjectDefinition center = null;
    for (int i = 0; i < SubjectCatalog.All.Length; i++)
      if (Mathf.Abs(SubjectCatalog.All[i].GatePos.x) < 1e-6f) center = SubjectCatalog.All[i];
    Assert.IsNotNull(center, "one gate holds the arc centre");
    Assert.AreEqual(SubjectIds.Exploration, center.Id, "centre slot is Khám phá");
  }

  // C. Yard-bound entry gate: fires the subject's SKILL yard and NEVER touches
  // the legacy world-nav (no event, no travel to a subject scene).
  [Test] public void S13C_EntryGateOpensSkillYard() {
    GameObject go = null;
    GameObject areaGo = null;
    try {
      var bus = new GameEventBus();
      int events = 0;
      bus.Subscribe<WorldChangedEvent>(_ => events++);
      var nav = new WorldNavService(bus);
      areaGo = new GameObject("S13Area");
      SelectionYardArea area = areaGo.AddComponent<SelectionYardArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      go = new GameObject("S13Gate");
      go.transform.position = SubjectCatalog.Math.GatePos;
      SubjectGate gate = go.AddComponent<SubjectGate>();
      gate.Bind(nav, SubjectIds.Math, false, null);
      gate.BindYard(area);
      Assert.IsTrue(gate.HasYard, "gate knows its yard");
      Assert.IsTrue(gate.TryFireForTests(SubjectCatalog.Math.GatePos, SubjectIds.Main),
        "walking into the gate fires");
      Assert.AreEqual(SelectionYardArea.YardLevel.Skill, area.PendingLevel,
        "the subject's SKILL yard is requested");
      Assert.AreEqual("math", area.PendingSubjectId, "right subject requested");
      Assert.AreEqual(SubjectIds.Main, nav.Current, "legacy world-nav untouched by the yard path");
      Assert.AreEqual(0, events, "no WorldChangedEvent for yard travel");
      // Re-arm rule still applies: standing inside must not double-fire.
      gate.NotifyWarpedAway();
      Assert.IsFalse(gate.TryFireForTests(SubjectCatalog.Math.GatePos, SubjectIds.Main),
        "disarmed until the child walks clear");
    } finally {
      if (go != null) Object.DestroyImmediate(go);
      if (areaGo != null) Object.DestroyImmediate(areaGo);
    }
  }

  // E. Return triggers: with the yard bound, returns stay the yard's own
  // portal — the legacy district trigger neither fires nor throws unbound.
  [Test] public void S13E_ReturnTriggerGoesInert() {
    GameObject go = null;
    try {
      go = new GameObject("S13Return");
      go.transform.position = SubjectCatalog.English.ReturnPoint;
      SubjectGate gate = go.AddComponent<SubjectGate>();
      gate.Bind(null, SubjectIds.English, true, null); // legacy return, no nav (PHASE 2 unbind)
      Assert.IsFalse(gate.TryFireForTests(SubjectCatalog.English.ReturnPoint, SubjectIds.English),
        "unbound legacy return never fires");
      Assert.IsFalse(gate.TryFireForTests(SubjectCatalog.English.ReturnPoint, SubjectIds.Main),
        "and never fires from Main either");
    } finally {
      if (go != null) Object.DestroyImmediate(go);
    }
  }

  // F. Every catalog subject resolves to a real skill yard in the taxonomy:
  // walk the product path in data (A door -> B level -> C level exists).
  [Test] public void S13F_AtoBYardPathResolves() {
    for (int i = 0; i < SubjectCatalog.All.Length; i++) {
      SubjectDefinition def = SubjectCatalog.All[i];
      SkillEntry[] skills = LearningMap.SkillsOf(def.Id.Value);
      Assert.Greater(skills.Length, 0, def.DisplayName + " has a skill yard with doors");
      for (int s = 0; s < skills.Length; s++) {
        Assert.IsNotNull(LearningMap.Skill(skills[s].Id), "skill resolves: " + skills[s].Id);
        Assert.IsFalse(string.IsNullOrEmpty(LearningMap.GameYardTitle(skills[s].Id)),
          "skill's game yard has a title: " + skills[s].Id);
      }
    }
    GameEntry[] counting = LearningMap.GamesOf("math_counting");
    Assert.AreEqual(2, counting.Length, "counting still owns exactly the two accepted games");
  }

  // G. PHASE 3 loader contract (foreground journey regression): the skill/game
  // yard is a micro scene entered STRAIGHT FROM the Subject Yard (State Idle,
  // no subject scene underneath) and unloads back to it. The legacy nesting
  // (subject scene -> micro) must keep working for the old garden flow.
  sealed class S13FakeOps : ISceneOps {
    public readonly HashSet<string> Loaded = new HashSet<string>();
    public bool IsLoaded(string s) { return Loaded.Contains(s); }
    public Task LoadAdditiveAsync(string s) { Loaded.Add(s); return Task.CompletedTask; }
    public Task UnloadAsync(string s) { Loaded.Remove(s); return Task.CompletedTask; }
  }

  [Test] public void S13G_YardMicroEntryFromSubjectYard() {
    var t = new WorldTransition(SubjectIds.Main);
    var ops = new S13FakeOps();
    Assert.IsTrue(t.EnterMicroAsync(ops, SelectionYardBuilder.SceneName).GetAwaiter().GetResult(),
      "the skill/game yard loads straight from the Main world");
    Assert.AreEqual(SelectionYardBuilder.SceneName, t.MicroScene, "the micro slot owns the yard");
    Assert.AreEqual(WorldTransitionState.Idle, t.State, "Main stays the active world");
    Assert.IsTrue(t.ExitMicroAsync(ops).GetAwaiter().GetResult(), "the yard unloads back to Main");
    Assert.IsNull(t.MicroScene, "slot freed");
    Assert.AreEqual(WorldTransitionState.Idle, t.State, "still back in the Subject Yard");
    // Legacy nesting unchanged: subject scene base -> yard micro.
    var t2 = new WorldTransition(SubjectIds.Main);
    var ops2 = new S13FakeOps();
    Assert.IsTrue(t2.EnterAsync(ops2, SubjectIds.Math, "MathScene").GetAwaiter().GetResult(),
      "subject scene enters (legacy path)");
    Assert.IsTrue(t2.EnterMicroAsync(ops2, SelectionYardBuilder.SceneName).GetAwaiter().GetResult(),
      "micro works under a loaded subject too");
    Assert.AreEqual(WorldTransitionState.InSubject, t2.State, "subject base stays InSubject");
  }
}
