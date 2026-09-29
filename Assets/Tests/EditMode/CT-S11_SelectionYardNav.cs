// CT-S11: FULL ARCHITECTURE RESET — the selection navigation seams
// (SelectionYardArea + SelectionGate) without any live Unity refs: pending
// context, forward entries, up-one-level Back, idempotence, refusal of
// unknown ids, and the walk-in gate poll contract.
// C# 9.0 only.
using NUnit.Framework;
using UnityEngine;

public class CT_S11_SelectionYardNav {
  static SelectionYardArea BuildArea() {
    GameObject go = new GameObject("S11Area");
    SelectionYardArea area = go.AddComponent<SelectionYardArea>();
    area.Bind(null, null, null, null, null, Vector3.zero);
    return area;
  }

  // A. Pending context carries the level + ids and drives the objective line.
  [Test] public void S11A_PendingContext() {
    SelectionYardArea area = BuildArea();
    try {
      area.SetPendingForTests(SelectionYardArea.YardLevel.Skill, "math", "");
      Assert.AreEqual(SelectionYardArea.YardLevel.Skill, area.PendingLevel, "skill level pending");
      Assert.AreEqual("math", area.PendingSubjectId, "subject pending");
      Assert.AreEqual("Toán học — Chọn kỹ năng", area.PendingObjective, "skill yard objective");
      area.SetPendingForTests(SelectionYardArea.YardLevel.Game, "", "math_counting");
      Assert.AreEqual("Đếm — Chọn trò chơi", area.PendingObjective, "game yard objective");
    } finally { Object.DestroyImmediate(area.gameObject); }
  }

  // B. Forward entries: unknown ids are refused, valid ids set the pending
  // context, re-entering the SAME level is idempotent.
  [Test] public void S11B_ForwardEntries() {
    SelectionYardArea area = BuildArea();
    try {
      area.EnterSkill("nope");
      Assert.AreEqual(SelectionYardArea.YardLevel.None, area.PendingLevel, "unknown subject refused");
      area.EnterGame("nope");
      Assert.AreEqual(SelectionYardArea.YardLevel.None, area.PendingLevel, "unknown skill refused");
      area.EnterSkill("math");
      Assert.AreEqual(SelectionYardArea.YardLevel.Skill, area.PendingLevel, "math skill yard requested");
      Assert.AreEqual("math", area.PendingSubjectId, "math pending subject");
      area.EnterGame("math_counting");
      Assert.AreEqual(SelectionYardArea.YardLevel.Game, area.PendingLevel, "counting game yard requested");
      Assert.AreEqual("math_counting", area.PendingSkillId, "counting pending skill");
      // Idempotent: already standing in the counting game yard.
      area.SetPendingForTests(SelectionYardArea.YardLevel.None, "", "");
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Game, "", "math_counting");
      area.EnterGame("math_counting");
      Assert.AreEqual(SelectionYardArea.YardLevel.None, area.PendingLevel,
        "re-entering the same game yard is a no-op");
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Skill, "math", "");
      area.EnterSkill("math");
      Assert.AreEqual(SelectionYardArea.YardLevel.None, area.PendingLevel,
        "re-entering the same skill yard is a no-op");
      // A DIFFERENT level still navigates.
      area.EnterSkill("thinking");
      Assert.AreEqual(SelectionYardArea.YardLevel.Skill, area.PendingLevel, "other subject navigates");
      Assert.AreEqual("thinking", area.PendingSubjectId, "thinking pending subject");
    } finally { Object.DestroyImmediate(area.gameObject); }
  }

  // C. Back() walks up exactly one level: game yard -> its skill's subject;
  // skill yard -> the subject yard (Main world).
  [Test] public void S11C_BackWalksUpOneLevel() {
    SelectionYardArea area = BuildArea();
    try {
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Game, "", "math_counting");
      Assert.AreEqual("skill:math", area.BackTargetForTests(), "game yard backs to math skills");
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Skill, "math", "");
      Assert.AreEqual("hub", area.BackTargetForTests(), "skill yard backs to the subject yard");
      // Back() from the game yard re-enters the SKILL yard of that game's skill.
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Game, "", "number_stairs");
      area.Back();
      Assert.AreEqual(SelectionYardArea.YardLevel.Skill, area.PendingLevel, "back enters skill yard");
      Assert.AreEqual("math", area.PendingSubjectId, "back lands in the right subject");
    } finally { Object.DestroyImmediate(area.gameObject); }
  }

  // D. Enter/exit seams + busy discipline.
  [Test] public void S11D_StateSeams() {
    SelectionYardArea area = BuildArea();
    try {
      Assert.IsFalse(area.IsInside, "starts outside");
      Assert.IsTrue(area.CanEnter, "can enter");
      Assert.IsTrue(area.TryEnterForTests(), "enter seam");
      Assert.IsFalse(area.TryEnterForTests(), "double enter blocked");
      Assert.IsTrue(area.CanExit, "exit available while inside");
      Assert.IsTrue(area.TryExitForTests(), "exit seam");
      Assert.IsFalse(area.TryExitForTests(), "double exit blocked");
    } finally { Object.DestroyImmediate(area.gameObject); }
  }

  // E. The gate poll contract: bind stores kind+id; the fire radius is pure.
  [Test] public void S11E_GatePoll() {
    GameObject go = new GameObject("S11Gate");
    try {
      SelectionGate gate = go.AddComponent<SelectionGate>();
      go.transform.position = new Vector3(1f, 0f, 2f);
      gate.fireRadius = 1.6f;
      gate.Bind(null, SelectionGate.GateKind.Game, "rabbit_feeding");
      Assert.AreEqual(SelectionGate.GateKind.Game, gate.Kind, "kind stored");
      Assert.AreEqual("rabbit_feeding", gate.TargetId, "target stored");
      Assert.IsTrue(gate.WouldFire(new Vector3(1f, 0f, 2.5f)), "inside radius fires");
      Assert.IsFalse(gate.WouldFire(new Vector3(1f, 0f, 4.2f)), "outside radius never fires");
      Assert.IsFalse(gate.WouldFire(new Vector3(5f, 0f, 2f)), "far X never fires");
    } finally { Object.DestroyImmediate(go); }
  }

  // F. The whole MATH chain resolves through the map: subject -> skills ->
  // counting -> two accepted game entries (the product path in data).
  [Test] public void S11F_MathChain() {
    SelectionYardArea area = BuildArea();
    try {
      area.EnterSkill("math");
      Assert.AreEqual("math", area.PendingSubjectId, "enter math");
      SkillEntry[] skills = LearningMap.SkillsOf(area.PendingSubjectId);
      Assert.AreEqual(5, skills.Length, "math shows five skill doors");
      area.SetPendingForTests(SelectionYardArea.YardLevel.None, "", "");
      area.EnterGame("math_counting");
      Assert.AreEqual("math_counting", area.PendingSkillId, "enter counting");
      GameEntry[] games = LearningMap.GamesOf(area.PendingSkillId);
      Assert.AreEqual(2, games.Length, "counting shows two game doors");
      Assert.AreEqual("rabbit_feeding", games[0].Id, "rabbit door");
      Assert.AreEqual("number_stairs", games[1].Id, "stairs door");
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Game, "", "math_counting");
      Assert.AreEqual("skill:math", area.BackTargetForTests(), "and the chain can walk back");
      // The game doors (C level) record the arena launch request (PHASE 4
      // wires the travel); only human-accepted games may request.
      Assert.AreEqual(0, area.PlayRequests, "no play requests yet");
      area.PlayGame("nope");
      Assert.AreEqual(0, area.PlayRequests, "unknown game refused");
      area.PlayGame("rabbit_feeding");
      Assert.AreEqual(1, area.PlayRequests, "rabbit door fires the launch request");
      Assert.AreEqual("rabbit_feeding", area.LastPlayRequest, "request memorized");
    } finally { Object.DestroyImmediate(area.gameObject); }
  }
}
