// CT-P03: final-polish reusable contracts (HUD adaptivity, thought-bubble
// structure, shoe builder). Guards the visual-lock pass: compact secondary
// HUD that yields to emotional beats, a thought bubble that reads as thinking
// (never a debug ball), and uniform shoe caps on every rig. Pure EditMode:
// deterministic public-API calls only, no timing, no screenshots. C# 9.0 only.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class CT_P03_Polish {
  // A. Objective chip is compact (secondary reminder, never a narrator panel).
  [Test] public void CT_P03A_ObjectiveChipIsCompact() {
    GameObject go = new GameObject("HudCompactTest");
    try {
      MarketHUD hud = go.AddComponent<MarketHUD>();
      hud.BuildUiImmediate();
      Transform panel = go.transform.Find("MarketCanvas/ObjectivePanel");
      Assert.IsNotNull(panel, "HUD must build the objective panel");
      RectTransform rt = panel.GetComponent<RectTransform>();
      Assert.IsNotNull(rt, "panel must be a RectTransform");
      Assert.LessOrEqual(rt.sizeDelta.x, 340f, "chip must stay compact (never cover faces)");
      Assert.LessOrEqual(rt.sizeDelta.y, 70f, "chip must stay a single short line");
      Text text = panel.GetComponentInChildren<Text>(true);
      Assert.IsNotNull(text, "chip must carry text");
      Assert.LessOrEqual(text.fontSize, 24, "chip type must stay small");
    } finally {
      Object.DestroyImmediate(go);
    }
  }

  // B. Chip fades during emotional camera beats, restores on Follow.
  [Test] public void CT_P03B_ChipYieldsToEmotionalBeats() {
    GameObject go = new GameObject("HudAdaptTest");
    try {
      MarketHUD hud = go.AddComponent<MarketHUD>();
      hud.BuildUiImmediate();
      CanvasGroup fade = go.transform.Find("MarketCanvas").GetComponent<CanvasGroup>();
      Assert.IsNotNull(fade, "canvas must carry a fade group");
      hud.ApplyCameraMode(CameraMode.Follow);
      Assert.AreEqual(1f, fade.alpha, 0.001f, "follow framing: chip fully present");
      hud.ApplyCameraMode(CameraMode.Interaction);
      Assert.AreEqual(0.35f, fade.alpha, 0.001f, "emotional beat: chip whispers");
      hud.ApplyCameraMode(CameraMode.Cinematic);
      Assert.AreEqual(0.35f, fade.alpha, 0.001f, "cinematic: chip whispers");
      hud.ApplyCameraMode(CameraMode.Follow);
      Assert.AreEqual(1f, fade.alpha, 0.001f, "return: chip restores");
    } finally {
      Object.DestroyImmediate(go);
    }
  }

  // C. Thought-bubble structure: shell + outline + downward tail + apple icon
  // with stem AND leaf; zero colliders anywhere under it.
  [Test] public void CT_P03C_ThoughtBubbleStructure() {
    GameObject go = new GameObject("BubbleStructureTest");
    try {
      WorldQuestionBubble bubble = go.AddComponent<WorldQuestionBubble>();
      bubble.BuildBubbleImmediate();
      Assert.IsNotNull(go.transform.Find("BubbleShell/Shell"), "must keep the shell");
      Assert.IsNotNull(go.transform.Find("BubbleShell/ShellOutline"), "must carry a soft outline");
      Assert.IsNotNull(go.transform.Find("TailPuff1"), "must grow a tail toward the thinker");
      Assert.IsNotNull(go.transform.Find("TailPuff2"), "tail needs the second puff");
      Assert.IsNotNull(go.transform.Find("AskIcon/AskApple"), "must keep the apple icon");
      Assert.IsNotNull(go.transform.Find("AskIcon/AskStem"), "apple needs its stem");
      Assert.IsNotNull(go.transform.Find("AskIcon/AskLeaf"), "apple needs its leaf (never a red dot)");
      Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
      Assert.AreEqual(0, colliders.Length, "bubble must never eat clicks");
      // Idempotent: second build is a no-op, never duplicates the kit.
      bubble.BuildBubbleImmediate();
      Assert.AreEqual(1, CountNamed(go.transform, "Shell"), "rebuild must not duplicate");
    } finally {
      Object.DestroyImmediate(go);
    }
  }

  static int CountNamed(Transform root, string childName) {
    int n = 0;
    foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) {
      if (t != null && t.name == childName) n++;
    }
    return n;
  }

  // D. Shoe builder: uniform cap under the foot bone, no colliders, contract sizes.
  [Test] public void CT_P03D_ShoeBuildContract() {
    GameObject root = new GameObject("ShoeAnchorTest");
    GameObject footGo = new GameObject("Foot.L");
    try {
      footGo.transform.SetParent(root.transform, false);
      footGo.transform.position = new Vector3(1f, 0.5f, 2f);
      root.transform.rotation = Quaternion.identity;
      CharacterPresentation face = root.AddComponent<CharacterPresentation>();
      face.QueueShoe(footGo.transform, "ShoeL");
      face.BuildShoesImmediate();
      Transform shoe = footGo.transform.Find("ShoeL");
      Assert.IsNotNull(shoe, "shoe must parent under the foot bone (follows animation)");
      Assert.AreEqual(CharacterPresentation.ShoeSize.x, shoe.localScale.x, 0.001f, "shoe width pins the contract");
      Assert.AreEqual(CharacterPresentation.ShoeSize.y, shoe.localScale.y, 0.001f, "shoe height pins the contract");
      Assert.AreEqual(CharacterPresentation.ShoeSize.z, shoe.localScale.z, 0.001f, "shoe length pins the contract");
      Collider[] colliders = shoe.GetComponentsInChildren<Collider>(true);
      Assert.AreEqual(0, colliders.Length, "shoes must never eat clicks");
      Renderer r = shoe.GetComponent<Renderer>();
      Assert.IsNotNull(r, "shoe must render");
      Assert.IsNotNull(r.sharedMaterial, "shoe must carry a material (never default-white)");
    } finally {
      Object.DestroyImmediate(root);
      Object.DestroyImmediate(footGo);
    }
  }

  // E. Null foot is a silent no-op (presenters call QueueShoe unconditionally).
  [Test] public void CT_P03E_ShoeNullFootSilent() {
    GameObject root = new GameObject("ShoeNullTest");
    try {
      CharacterPresentation face = root.AddComponent<CharacterPresentation>();
      face.QueueShoe(null, "ShoeL");
      face.BuildShoesImmediate(); // must not throw
      Assert.AreEqual(0, root.GetComponentsInChildren<Transform>(true).Length - 1, "nothing built for null foot");
    } finally {
      Object.DestroyImmediate(root);
    }
  }
}
