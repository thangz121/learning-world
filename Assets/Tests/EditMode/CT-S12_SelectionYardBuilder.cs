// CT-S12: FULL ARCHITECTURE RESET — the generic yard rendering. Pins that the
// SAME scene renders the Skill Yard (B) and the Game Yard (C) from LearningMap
// data: one door per entry, live gates only for accepted games, and the
// [CHƯA CÓ TRÒ CHƠI] placeholder for EVERY empty skill (never a fake game).
// C# 9.0 only.
using NUnit.Framework;
using UnityEngine;

public class CT_S12_SelectionYardBuilder {
  static SelectionYardBuilder BuildYard(string level, string subjectId, string skillId,
      out GameObject root) {
    root = new GameObject("S12Yard");
    SelectionYardBuilder builder = root.AddComponent<SelectionYardBuilder>();
    builder.Level = level;
    builder.SubjectId = subjectId;
    builder.SkillId = skillId;
    builder.BuildContent(root.transform);
    return builder;
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

  // A. Skill Yard (B): one door per skill, in the approved order, each with a
  // label; the live skill carries the approach glow, empty skills do not.
  [Test] public void S12A_SkillYard() {
    GameObject root;
    SelectionYardBuilder builder = BuildYard("skill", "math", "", out root);
    try {
      Assert.IsTrue(builder.IsGameLevel == false, "skill level");
      Assert.AreEqual(5, builder.GatePortals.Count, "five skill doors for math");
      string[] want = { "math_counting", "math_geometry", "math_comparison",
        "math_classification", "math_order" };
      for (int i = 0; i < want.Length; i++) {
        Assert.AreEqual(want[i], builder.GateTargetIds[i], "door " + i + " is " + want[i]);
        Assert.IsNotNull(LearningMap.Skill(builder.GateTargetIds[i]), "B doors target skill ids");
        SelectionGate gate = builder.GatePortals[i];
        Assert.AreEqual(SelectionGate.GateKind.Game, gate.Kind, "B door opens a Game Yard");
        Assert.IsNotNull(FindDeep(gate.transform.parent, "Label"), "gate label staged");
      }
      // Live skill (counting, has accepted games) = hint; empty skills = none.
      Assert.IsNotNull(FindDeep(builder.GateRoots[0], "Beam"), "accepting gate structure");
      int hints = 0;
      Transform[] all = root.GetComponentsInChildren<Transform>(true);
      for (int i = 0; i < all.Length; i++)
        if (all[i].GetComponent<MicroGateHint>() != null) hints++;
      Assert.AreEqual(4, hints, "counting, geometry, comparison and classification glow");
      Assert.IsNotNull(builder.BackGate, "skill yard has the way back");
      Assert.AreEqual(SelectionGate.GateKind.Back, builder.BackGate.Kind, "back gate kind");
      Assert.IsNotNull(builder.TitleBoard, "orientation sign staged");
      Assert.IsNull(builder.PlaceholderBoard, "no placeholder in a skill yard");
      Assert.IsNotNull(builder.EntryPoint, "entry marker staged");
      Assert.IsNotNull(builder.Anchors, "anchor registry staged");
      // Every subject renders exactly its own skills.
      for (int i = 0; i < LearningMap.Subjects.Length; i++) {
        GameObject r2;
        SelectionYardBuilder b2 = BuildYard("skill", LearningMap.Subjects[i].Id, "", out r2);
        try {
          Assert.AreEqual(LearningMap.Subjects[i].Skills.Length, b2.GatePortals.Count,
            "doors = skills for " + LearningMap.Subjects[i].Id);
        } finally { Object.DestroyImmediate(r2); }
      }
    } finally { Object.DestroyImmediate(root); }
  }

  // B. Game Yard (C) for counting: exactly the two accepted games, both live.
  [Test] public void S12B_CountingGameYard() {
    GameObject root;
    SelectionYardBuilder builder = BuildYard("game", "", "math_counting", out root);
    try {
      Assert.IsTrue(builder.IsGameLevel, "game level");
      Assert.AreEqual(2, builder.GatePortals.Count, "two game doors");
      Assert.AreEqual("rabbit_feeding", builder.GateTargetIds[0], "rabbit door first");
      Assert.AreEqual("number_stairs", builder.GateTargetIds[1], "stairs door second");
      for (int i = 0; i < builder.GatePortals.Count; i++) {
        Assert.AreEqual(SelectionGate.GateKind.Play, builder.GatePortals[i].Kind, "C door launches a game");
        Assert.IsNotNull(LearningMap.Game(builder.GateTargetIds[i]), "C doors target game ids");
        Assert.IsTrue(LearningMap.IsPlayable(builder.GateTargetIds[i]), "door only for accepted games");
        Assert.IsNotNull(FindDeep(builder.GateRoots[i], "Threshold"), "gate threshold staged");
      }
      int hints = 0;
      Transform[] all = root.GetComponentsInChildren<Transform>(true);
      for (int i = 0; i < all.Length; i++)
        if (all[i].GetComponent<MicroGateHint>() != null) hints++;
      Assert.AreEqual(2, hints, "both accepted games glow");
      Assert.IsNull(builder.PlaceholderBoard, "no placeholder when games exist");
    } finally { Object.DestroyImmediate(root); }
  }

  // C. EMPTY skill: the Game Yard still exists but shows ONLY the placeholder —
  // never a fake game door (product rule).
  [Test] public void S12C_EmptySkillPlaceholder() {
    GameObject root;
    SelectionYardBuilder builder = BuildYard("game", "", "math_order", out root);
    try {
      Assert.AreEqual(0, builder.GatePortals.Count, "no game doors for an empty skill");
      Assert.IsNotNull(builder.PlaceholderBoard, "placeholder board staged");
      Assert.IsNotNull(FindDeep(root.transform, "SYPlaceholderLabel"), "placeholder label staged");
      Assert.IsNotNull(builder.TitleBoard, "the empty skill still names itself");
      Assert.IsNotNull(builder.BackGate, "and still has the way back");
      // EVERY skill without games renders the placeholder and nothing else.
      for (int i = 0; i < LearningMap.Subjects.Length; i++) {
        SkillEntry[] skills = LearningMap.Subjects[i].Skills;
        for (int s = 0; s < skills.Length; s++) {
          if (LearningMap.GamesOf(skills[s].Id).Length > 0) continue;
          GameObject r2;
          SelectionYardBuilder b2 = BuildYard("game", "", skills[s].Id, out r2);
          try {
            Assert.AreEqual(0, b2.GatePortals.Count, "no doors for " + skills[s].Id);
            Assert.IsNotNull(b2.PlaceholderBoard, "placeholder for " + skills[s].Id);
          } finally { Object.DestroyImmediate(r2); }
        }
      }
    } finally { Object.DestroyImmediate(root); }
  }

  // D. Data-driven doors: changing the subject changes the yard (no hardcode).
  [Test] public void S12D_DataDriven() {
    GameObject root;
    BuildYard("skill", "exploration", "", out root);
    try {
      SelectionYardBuilder b = root.GetComponent<SelectionYardBuilder>();
      Assert.AreEqual(4, b.GatePortals.Count, "exploration shows four skills");
      Assert.AreEqual("exploration_nature", b.GateTargetIds[0], "nature first");
      Assert.AreEqual("exploration_daily_life", b.GateTargetIds[3], "daily life last");
    } finally { Object.DestroyImmediate(root); }
  }

  // E. PHASE 4b (user: a pre-reader must CHOOSE BY PICTURE): every game door
  // carries a wordless diorama of its game, in front of the door (entry side);
  // skill yards and empty yards never build one.
  [Test] public void S12F_GamePreviewDioramas() {
    GameObject root;
    SelectionYardBuilder builder = BuildYard("game", "", "math_counting", out root);
    try {
      Transform rabbit = FindDeep(root.transform, "SYPreview_rabbit_feeding");
      Transform stairs = FindDeep(root.transform, "SYPreview_number_stairs");
      Assert.IsNotNull(rabbit, "rabbit feeding preview staged");
      Assert.IsNotNull(stairs, "number stairs preview staged");
      Assert.GreaterOrEqual(rabbit.childCount, 4, "bunny + carrots pieces");
      Assert.GreaterOrEqual(stairs.childCount, 3, "mini stairs pieces");
      // Entry side of their own door (doors sit at z-0.7 from the gate row).
      for (int i = 0; i < builder.GatePortals.Count; i++) {
        Transform preview = i == 0 ? rabbit : stairs;
        Assert.Less(preview.localPosition.z, builder.GatePortals[i].transform.localPosition.z,
          "preview stands BEFORE the door, on the child's approach");
      }
    } finally { Object.DestroyImmediate(root); }
    GameObject skillRoot;
    BuildYard("skill", "math", "", out skillRoot);
    try {
      Assert.IsNull(FindDeep(skillRoot.transform, "SYPreview_rabbit_feeding"),
        "skill doors carry no game previews");
    } finally { Object.DestroyImmediate(skillRoot); }
    GameObject emptyRoot;
    BuildYard("game", "", "math_order", out emptyRoot);
    try {
      Assert.IsNull(FindDeep(emptyRoot.transform, "SYPreview_number_stairs"),
        "empty yard carries no previews");
    } finally { Object.DestroyImmediate(emptyRoot); }
  }

  // E. PHASE 4 label fix: the island root sits at +540 — labels MUST be staged
  // in WORLD space through the root (the old local offsets stranded every
  // name at the map origin, edge-on to the camera = invisible in the yard).
  [Test] public void S12E_LabelsAreWorldStagedAtTheIsland() {
    GameObject root = new GameObject("S12Island");
    try {
      root.transform.position = SelectionYardBuilder.WorldOffset;
      SelectionYardBuilder b = root.AddComponent<SelectionYardBuilder>();
      b.Level = "skill";
      b.SubjectId = "math";
      b.SkillId = "";
      b.BuildContent(root.transform);
      Transform title = FindDeep(root.transform, "SYTitleLabel");
      Assert.IsNotNull(title, "title label staged");
      Assert.Greater(title.position.x, 500f, "title rides the island, never the map origin");
      Transform door = FindDeep(root.transform, "Label");
      Assert.IsNotNull(door, "door label staged");
      Assert.Greater(door.position.x, 500f, "door labels ride the island too");
      Assert.Greater(door.position.z, -10f, "door label above its gate, not at the back");
      Vector3 viewer = SelectionYardBuilder.WorldOffset + SelectionYardBuilder.EntryLocal;
      Vector3 away = door.position - viewer;
      away.y = 0f;
      Assert.Greater(Vector3.Dot(door.transform.forward, away.normalized), 0.95f,
        "legible face points at the child entering the yard");
    } finally { Object.DestroyImmediate(root); }
  }

  // F. Beauty pass: dressing is staged and never a collider (NavMesh/click
  // stay on the ground and the doors). Empty yards get a pond, not a bare lawn.
  [Test] public void S12G_DressingIsColliderFree() {
    GameObject root;
    BuildYard("skill", "math", "", out root);
    try {
      Transform dress = FindDeep(root.transform, "SYDressing");
      Assert.IsNotNull(dress, "yard dressing staged");
      Assert.IsNotNull(FindDeep(root.transform, "SYBlossomTree0Trunk"), "blossom tree staged");
      Assert.IsNotNull(FindDeep(root.transform, "SYNamePlaque_math_counting"), "name sits on a plaque");
      Collider[] cols = dress.GetComponentsInChildren<Collider>(true);
      Assert.AreEqual(0, cols.Length, "dressing never blocks the walk");
    } finally { Object.DestroyImmediate(root); }
    GameObject empty;
    SelectionYardBuilder emptyYard = BuildYard("game", "", "math_order", out empty);
    try {
      Assert.AreEqual(0, emptyYard.GatePortals.Count, "empty yard still has no doors");
      Assert.IsNotNull(FindDeep(empty.transform, "SYPondWater"), "empty yard is a quiet garden");
    } finally { Object.DestroyImmediate(empty); }
  }
}
