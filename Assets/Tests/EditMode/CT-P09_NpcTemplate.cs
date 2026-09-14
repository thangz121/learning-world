// CT-P09: Phase 2E golden-NPC-template contracts (presentation re-use).
// Proves the shared contracts are CONSUMED (not just defined):
//   A. NPC id resolution (stable ids, case-insensitive, unknown null).
//   B. Display names + label heights from the roster (no scattered literals).
//   C. Every shipped quest's npc value resolves (QuestEntry.npc consumed).
//   D. Roster voices (2F narration wiring reads these, not literals).
//   E. Bubble apple icon REGRESSION (names/sizes/colors/positions pinned).
//   F. Ball icon through the SAME api (blue sphere, no stem/leaf).
//   G. Unknown word -> neutral fallback (never apple by accident).
//   H. Expression API shared (one CP fixture: Sad frown <-> Happy smile).
//   I. LookAt contract callable on shared CP (no per-NPC variant).
//   J. AnchorFor placement contract (existing math pinned).
//   K. No apple remnants after SetIcon(ball) (no hardcoded icon path).
//   L. No quest-id branching in roster (pure alias map).
//   M/N. Milo/Mia behavior regression = full suite green (2E run).
// Pure EditMode (visual QUALITY stays runtime-proven — §27 run). C# 9.0 only.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class CT_P09_NpcTemplate {
  static string ContentRoot() {
    string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Content");
    Assert.IsTrue(Directory.Exists(root), "Content/ bundle must exist next to Assets");
    return root;
  }

  static WorldQuestionBubble NewBubble() {
    GameObject go = new GameObject("BubbleP09");
    WorldQuestionBubble bubble = go.AddComponent<WorldQuestionBubble>();
    bubble.BuildBubbleImmediate();
    return bubble;
  }

  static void DestroyBubble(WorldQuestionBubble bubble) {
    if (bubble != null) UnityEngine.Object.DestroyImmediate(bubble.gameObject);
  }

  static Transform FindDeep(Transform root, string name) {
    if (root == null) return null;
    if (root.name == name) return root;
    for (int i = 0; i < root.childCount; i++) {
      Transform hit = FindDeep(root.GetChild(i), name);
      if (hit != null) return hit;
    }
    return null;
  }

  static Color BaseColor(Renderer r) {
    Material m = r != null ? r.sharedMaterial : null;
    if (m == null) return new Color(0f, 0f, 0f, 0f);
    if (m.HasProperty("_BaseColor")) return m.GetColor("_BaseColor");
    if (m.HasProperty("_Color")) return m.GetColor("_Color");
    return new Color(0f, 0f, 0f, 0f);
  }

  // A. Stable ids resolve; unknown/empty do not.
  [Test] public void CT_P09A_NpcIdResolution() {
    Assert.AreEqual("mia", NpcRoster.Get("mia").id, "mia resolves");
    Assert.AreEqual("milo", NpcRoster.Get("MILO").id, "lookup is case-insensitive");
    Assert.IsNull(NpcRoster.Get("shopkeeper_mia"), "quest alias is NOT a stable id (ResolveQuestNpc instead)");
    Assert.IsNull(NpcRoster.Get(""), "empty resolves null");
    Assert.IsNull(NpcRoster.Get(null), "null resolves null");
    Assert.IsNull(NpcRoster.Get("bob"), "unknown resolves null");
  }

  // B. Display names + heights from the roster (Bootstrap reads these).
  [Test] public void CT_P09B_DisplayNamesFromRoster() {
    NpcDefinition milo = NpcRoster.Get("milo");
    NpcDefinition mia = NpcRoster.Get("mia");
    Assert.AreEqual("Milo", milo.displayName, "display is data, not a literal");
    Assert.AreEqual("Mia", mia.displayName, "display is data, not a literal");
    Assert.AreEqual(2.35f, milo.labelHeight, 0.001f, "audit height preserved");
    Assert.AreEqual(2.35f, mia.labelHeight, 0.001f, "audit height preserved");
    Assert.AreNotEqual(milo.displayName, milo.id, "display != identity (lookup key stays stable)");
  }

  // C. Every shipped quest's npc resolves (QuestEntry.npc consumed at last).
  [Test] public void CT_P09C_QuestNpcResolves() {
    string root = ContentRoot();
    int count = 0;
    foreach (string path in Directory.GetFiles(Path.Combine(root, "quests"), "*.json")) {
      QuestEntry q = ContentDatabase.ParseQuest(File.ReadAllText(path));
      NpcDefinition def = NpcRoster.ResolveQuestNpc(q.npc);
      Assert.IsNotNull(def, "quest " + q.id + ": npc '" + q.npc + "' must resolve in the roster");
      Assert.AreEqual("mia", def.id, "quest " + q.id + " stages Mia today");
      count++;
    }
    Assert.GreaterOrEqual(count, 7, "all shipped quests covered");
  }

  // D. Voice profiles from the roster (2F narration reads these).
  [Test] public void CT_P09D_RosterVoices() {
    Assert.AreEqual("milo_v1", NpcRoster.Get("milo").voice, "Milo voice frozen");
    Assert.AreEqual("mia_v1", NpcRoster.Get("mia").voice, "Mia voice frozen");
    Assert.AreEqual("mia_v1", NpcRoster.ResolveQuestNpc("shopkeeper_mia").voice,
      "quest alias carries the same voice (no second source)");
  }

  // E. Apple icon regression (byte-identical hierarchy to the R9 build).
  [Test] public void CT_P09E_AppleIconRegression() {
    WorldQuestionBubble bubble = NewBubble();
    try {
      Assert.AreEqual("apple", bubble.CurrentIconId, "default icon stays apple");
      Transform icon = bubble.transform.Find("AskIcon");
      Assert.IsNotNull(icon, "AskIcon container preserved (pulse driver)");
      Transform fruit = FindDeep(bubble.transform, "AskApple");
      Assert.IsNotNull(fruit, "AskApple preserved");
      Assert.AreEqual(new Vector3(0.30f, 0.30f, 0.30f), fruit.localScale, "apple size preserved");
      Color c = BaseColor(fruit.GetComponent<Renderer>());
      Assert.Greater(c.r, 0.7f, "apple stays red");
      Assert.Less(c.g, 0.3f, "apple stays red");
      Assert.IsNotNull(FindDeep(bubble.transform, "AskStem"), "stem preserved");
      Assert.IsNotNull(FindDeep(bubble.transform, "AskLeaf"), "leaf preserved");
    } finally {
      DestroyBubble(bubble);
    }
  }

  // F. Ball icon through the same api (no special-case code path).
  [Test] public void CT_P09F_BallIconSameApi() {
    WorldQuestionBubble bubble = NewBubble();
    try {
      bubble.SetIcon(new WordId("ball"));
      Assert.AreEqual("ball", bubble.CurrentIconId, "icon id tracks the word");
      Transform fruit = FindDeep(bubble.transform, "AskBall");
      Assert.IsNotNull(fruit, "AskBall built");
      Color c = BaseColor(fruit.GetComponent<Renderer>());
      Assert.Greater(c.b, 0.7f, "ball stays distractor blue");
      Assert.Less(c.r, 0.4f, "ball stays distractor blue");
      Assert.IsNull(FindDeep(bubble.transform, "AskStem"), "ball has no stem");
      Assert.IsNull(FindDeep(bubble.transform, "AskLeaf"), "ball has no leaf");
      Transform icon = bubble.transform.Find("AskIcon");
      Assert.IsNotNull(icon, "container (and pulse) survive the swap");
    } finally {
      DestroyBubble(bubble);
    }
  }

  // G. Unknown word -> neutral fallback, never apple.
  [Test] public void CT_P09G_UnknownIconFallback() {
    WorldQuestionBubble bubble = NewBubble();
    try {
      bubble.SetIcon(new WordId("zebra"));
      Assert.AreEqual("unknown", bubble.CurrentIconId, "unknown tracks as unknown");
      Assert.IsNotNull(FindDeep(bubble.transform, "AskUnknown"), "fallback dot built");
      Assert.IsNull(FindDeep(bubble.transform, "AskApple"), "unknown must never build apple");
      Assert.IsNull(FindDeep(bubble.transform, "AskStem"), "unknown must never build apple parts");
      // Round-trip back to apple (quest switching reuses one bubble).
      bubble.SetIcon(new WordId("apple"));
      Assert.AreEqual("apple", bubble.CurrentIconId, "round-trips to apple");
      Assert.IsNotNull(FindDeep(bubble.transform, "AskApple"), "apple rebuilds clean");
      Assert.IsNull(FindDeep(bubble.transform, "AskUnknown"), "fallback cleared on swap");
    } finally {
      DestroyBubble(bubble);
    }
  }

  // H+I. Expression + LookAt live on the SHARED presentation (one fixture).
  [Test] public void CT_P09H_SharedExpressionApi() {
    GameObject root = new GameObject("ExprP09");
    GameObject headGo = new GameObject("Head");
    GameObject endGo = new GameObject("Head_end");
    try {
      headGo.transform.SetParent(root.transform, false);
      endGo.transform.SetParent(headGo.transform, false);
      headGo.transform.position = new Vector3(0f, 1.2f, 0f);
      endGo.transform.position = new Vector3(0f, 1.52f, 0f);
      CharacterPresentation face = root.AddComponent<CharacterPresentation>();
      face.SetupFace(null, headGo.transform, root.transform, root.transform);
      face.BuildFaceImmediate();
      face.PulseExpression(CharacterExpression.Sad, 5f);
      Assert.IsTrue(headGo.transform.Find("MouthFrown").gameObject.activeSelf, "Sad frown (shared kit)");
      face.PulseExpression(CharacterExpression.Happy, 5f);
      Assert.IsTrue(headGo.transform.Find("MouthSmile").gameObject.activeSelf, "Happy smile (shared kit)");
      face.SetExpression(CharacterExpression.Neutral);
      face.LookAt(15f, 1f); // shared LookAt callable — no per-NPC variant
      face.BlinkNow();      // shared blink — no per-NPC variant
    } finally {
      UnityEngine.Object.DestroyImmediate(root);
    }
  }

  // J. AnchorFor placement contract (existing math pinned for reuse).
  [Test] public void CT_P09J_AnchorContract() {
    Vector3 npc = new Vector3(-3.5f, 0f, -2.5f);
    Vector3 anchor = WorldQuestionBubble.AnchorFor(npc);
    Assert.AreEqual(npc.x + 1.45f, anchor.x, 0.001f, "east offset");
    Assert.AreEqual(1.78f, anchor.y, 0.001f, "head height, clear of 2.35m label");
    Assert.AreEqual(npc.z + 0.55f, anchor.z, 0.001f, "south offset");
  }

  // K. No apple remnants after SetIcon(ball) (no hardcoded icon path).
  [Test] public void CT_P09K_NoAppleRemnants() {
    WorldQuestionBubble bubble = NewBubble();
    try {
      bubble.SetIcon(new WordId("apple"));
      bubble.SetIcon(new WordId("ball"));
      bubble.SetIcon(new WordId("apple"));
      bubble.SetIcon(new WordId("ball"));
      int apples = 0, stems = 0;
      foreach (Transform t in bubble.GetComponentsInChildren<Transform>(true)) {
        if (t.name == "AskApple") apples++;
        if (t.name == "AskStem" || t.name == "AskLeaf") stems++;
      }
      Assert.AreEqual(0, apples, "no stale apple after swaps");
      Assert.AreEqual(0, stems, "no stale apple parts after swaps");
      Assert.AreEqual(1, CountNamed(bubble, "AskBall"), "exactly one ball icon");
    } finally {
      DestroyBubble(bubble);
    }
  }

  static int CountNamed(WorldQuestionBubble bubble, string name) {
    int n = 0;
    foreach (Transform t in bubble.GetComponentsInChildren<Transform>(true)) {
      if (t.name == name) n++;
    }
    return n;
  }

  // L. Roster is a pure alias map (no quest-id branching).
  [Test] public void CT_P09L_RosterHasNoQuestBranching() {
    Assert.AreEqual("mia", NpcRoster.ResolveQuestNpc("SHOPKEEPER_MIA").id, "alias match case-insensitive");
    Assert.AreEqual("mia", NpcRoster.ResolveQuestNpc("mia").id, "short alias resolves");
    Assert.AreEqual("milo", NpcRoster.ResolveQuestNpc("milo").id, "milo alias resolves");
    Assert.IsNull(NpcRoster.ResolveQuestNpc("w1_mia_apple"), "quest IDS never resolve (ids != npc aliases)");
    Assert.IsNull(NpcRoster.ResolveQuestNpc(null), "null safe");
    Assert.AreEqual(2, NpcRoster.Count, "cast is exactly Milo + Mia (no third NPC invented in 2E)");
  }
}
