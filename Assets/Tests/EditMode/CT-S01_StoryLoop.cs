// CT-S01: reusable Apple story loop (storytelling presentation, NOT scene-specific).
// Owner: Lead (storytelling-loop pass). Covers the frozen reusable behavior:
// DistractorChoice wrong-path, MiaPresenter correct-path, StoryMoment routing,
// WorldQuestionBubble lifecycle, SmartCamera temporary focus + return.
// Pure EditMode: deterministic public-API calls only, no timing, no screenshots.
// NOTE (batch-runner lifecycle): Awake/Update-driven construction does not
// reliably materialize for scene objects created inside these tests, so every
// test below drives explicit methods only (Bind/OnClicked/Setup/Show/Build
// hooks) and asserts their direct effects — never Awake-built side effects.
// Geometry IS covered explicitly via BuildFaceImmediate (F/G).
// C# 9.0 only.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CT_S01_StoryLoop {
  static readonly QuestId W1Quest = new QuestId("w1_mia_apple");
  static readonly WordId Apple = new WordId("apple");

  sealed class Fixture {
    public GameEventBus Bus;
    public LearningService Learning;
    public HintService Hints;
    public QuestManager Quests;
    public List<StoryMomentEvent> Moments;
    public Fixture() {
      Bus = new GameEventBus();
      Learning = new LearningService(Bus);
      Hints = new HintService(Bus);
      Quests = new QuestManager(Bus, Learning, Hints); // built-in w1_mia_apple: find -> bring
      Moments = new List<StoryMomentEvent>();
      Bus.Subscribe<StoryMomentEvent>(e => Moments.Add(e));
    }
  }

  // A. DistractorChoice R9 (pickup is neutral, the BRING decides): hands-full
  // taps stay legacy instant-wrong, retry preserved, post-quest inert.
  // (Pickup/bring-wrong/swap paths are pinned in CT-P05; this keeps the
  // legacy contract the story loop was frozen on.)
  [Test] public void CT_S01A_DistractorWrongRetry() {
    var f = new Fixture();
    GameObject go = new GameObject("BallTest");
    try {
      DistractorChoice ball = go.AddComponent<DistractorChoice>();
      ball.Bind(f.Bus, f.Hints, f.Quests);
      f.Quests.StartQuest(W1Quest);

      // Hands full of the quest item: tapping the wrong prop is a real mistake.
      f.Bus.Publish(new WordSeenEvent(Apple, LearnSource.Object, DateTime.UtcNow));
      ball.OnClicked();
      Assert.AreEqual(1, f.Moments.Count, "hands-full tap must emit exactly one moment");
      Assert.AreEqual(StoryMoment.WrongChoice, f.Moments[0].Moment, "hands-full tap must emit WrongChoice");
      Assert.IsFalse(f.Quests.GetState(W1Quest).Completed, "wrong must never complete the quest");
      Assert.AreEqual(1, f.Hints.GetState(W1Quest).WrongCount, "wrong must feed the hint ladder");
      Assert.IsFalse(ball.IsCarrying, "hands-full tap must not pick the ball up");

      ball.OnClicked(); // retry: prop stays usable, never locks the child out
      Assert.AreEqual(2, f.Moments.Count, "retry must stay possible");
      Assert.AreEqual(StoryMoment.WrongChoice, f.Moments[1].Moment);
      Assert.AreEqual(2, f.Hints.GetState(W1Quest).WrongCount);
      Assert.IsFalse(f.Quests.GetState(W1Quest).Completed, "repeated wrongs must not complete");

      // Finish the quest through the real path, then the prop goes inert.
      f.Quests.AdvanceOnSeen(Apple);
      f.Quests.ReportAction(PlayerAction.Bring, Apple);
      Assert.IsTrue(f.Quests.GetState(W1Quest).Completed, "setup: quest must be complete");
      f.Moments.Clear();
      ball.OnClicked();
      Assert.AreEqual(0, f.Moments.Count, "post-quest distractor clicks must be inert");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // B. Correct choice through the real Mia component: arming via WordSeen (as
  // MarketBootstrap routes it), then Mia click emits Correct + completes bring.
  [Test] public void CT_S01B_MiaCorrectCompletesBring() {
    var f = new Fixture();
    GameObject go = new GameObject("MiaTest");
    try {
      MiaPresenter mia = go.AddComponent<MiaPresenter>();
      mia.Bind(f.Bus, f.Quests, f.Hints);
      f.Quests.StartQuest(W1Quest);

      // Live-slice routing (MarketBootstrap): seen advances the find objective.
      f.Bus.Publish(new WordSeenEvent(Apple, LearnSource.Object, DateTime.UtcNow));
      f.Quests.AdvanceOnSeen(Apple);
      Assert.AreEqual(1, f.Quests.GetState(W1Quest).ObjectiveIndex, "find must advance first");

      mia.OnMiaClicked();
      Assert.IsTrue(f.Quests.GetState(W1Quest).Completed, "bring to Mia must complete the quest");
      Assert.AreEqual(1, f.Moments.Count, "correct click must emit exactly one moment");
      Assert.AreEqual(StoryMoment.CorrectChoice, f.Moments[0].Moment, "correct click must emit CorrectChoice");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // C. StoryMoment routing keeps Correct and Wrong distinguishable on one bus.
  [Test] public void CT_S01C_MomentsDistinguishable() {
    var f = new Fixture();
    f.Bus.Publish(new StoryMomentEvent(StoryMoment.WrongChoice, DateTime.UtcNow));
    f.Bus.Publish(new StoryMomentEvent(StoryMoment.CorrectChoice, DateTime.UtcNow));
    Assert.AreEqual(2, f.Moments.Count);
    Assert.AreEqual(StoryMoment.WrongChoice, f.Moments[0].Moment);
    Assert.AreEqual(StoryMoment.CorrectChoice, f.Moments[1].Moment);
    Assert.AreNotEqual(f.Moments[0].Moment, f.Moments[1].Moment, "receivers must tell Correct apart from Wrong");
  }

  // D. WorldQuestionBubble show/hide lifecycle (no screenshots).
  [Test] public void CT_S01D_BubbleLifecycle() {
    GameObject go = new GameObject("BubbleTest");
    try {
      WorldQuestionBubble bubble = go.AddComponent<WorldQuestionBubble>();
      bubble.Place(new Vector3(-3.5f, 2.1f, -1.6f));
      bubble.Show();
      Assert.IsTrue(go.activeSelf, "Show must leave the bubble active");
      bubble.Hide();
      Assert.IsFalse(go.activeSelf, "Hide must deactivate the bubble (quest complete)");
      bubble.Show();
      Assert.IsTrue(go.activeSelf, "Show must reactivate (next story reuse)");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // E. SmartCamera temporary focus: FocusOnFor switches to Interaction framing;
  // Follow restores Follow mode (the same call the auto-return path uses, so a
  // focus beat can never strand the camera). No frame timing involved.
  [Test] public void CT_S01E_CameraFocusAndReturn() {
    GameObject camGo = new GameObject("CamTest");
    GameObject targetGo = new GameObject("TargetTest");
    GameObject anchorGo = new GameObject("AnchorTest");
    try {
      SmartCamera cam = camGo.AddComponent<SmartCamera>();
      Vector3 offset = new Vector3(0f, 3.4f, -5.2f);
      cam.Follow(targetGo.transform, offset);
      Assert.AreEqual(CameraMode.Follow, cam.Mode, "setup: Follow mode");

      cam.FocusOnFor(anchorGo.transform.position, 3f, 2f);
      Assert.AreEqual(CameraMode.Interaction, cam.Mode, "emotional beat must frame the anchor");

      cam.Follow(targetGo.transform, offset); // auto-return path resumes Follow
      Assert.AreEqual(CameraMode.Follow, cam.Mode, "camera must return to gameplay framing");
    } finally {
      UnityEngine.Object.DestroyImmediate(camGo);
      UnityEngine.Object.DestroyImmediate(targetGo);
      UnityEngine.Object.DestroyImmediate(anchorGo);
    }
  }

  // F. Fallback face (no mesh data): geometry still builds on skull
  // proportions, and the child-friendly Sad frown toggles like every other
  // expression. Uses fabricated head/head_end bones (deterministic hook).
  [Test] public void CT_S01F_FallbackFaceExpressions() {
    GameObject root = new GameObject("FaceTest");
    GameObject headGo = new GameObject("Head");
    GameObject endGo = new GameObject("Head_end");
    try {
      headGo.transform.SetParent(root.transform, false);
      endGo.transform.SetParent(headGo.transform, false);
      headGo.transform.position = new Vector3(0f, 1.2f, 0f);
      endGo.transform.position = new Vector3(0f, 1.52f, 0f); // skull 0.32
      CharacterPresentation face = root.AddComponent<CharacterPresentation>();
      face.SetupFace(null, headGo.transform, root.transform, root.transform);
      face.BuildFaceImmediate();
      Transform eyeL = headGo.transform.Find("EyeL");
      Transform frown = headGo.transform.Find("MouthFrown");
      Transform smile = headGo.transform.Find("MouthSmile");
      Transform flat = headGo.transform.Find("MouthFlat");
      Assert.IsNotNull(eyeL, "fallback must still build eyes");
      Assert.IsNotNull(frown, "fallback must still build the child-friendly frown");
      face.PulseExpression(CharacterExpression.Sad, 5f);
      Assert.IsTrue(frown.gameObject.activeSelf, "Sad must show the frown (oops, never angry)");
      face.PulseExpression(CharacterExpression.Happy, 5f);
      Assert.IsTrue(smile.gameObject.activeSelf, "Happy must show the smile");
      face.SetExpression(CharacterExpression.Neutral);
      Assert.IsTrue(flat.gameObject.activeSelf, "Neutral must restore the flat mouth");
    } finally {
      UnityEngine.Object.DestroyImmediate(root);
    }
  }

  // G. Real-mesh regression: with the actual Milo/Mia head geometry, the
  // measured eye pane must sit BEHIND the nose-tip plane (FaceDiag 2026-09-12:
  // nose ~= +0.36, eye pane ~= +0.24). Guards the side-view floating fix.
  [Test] public void CT_S01G_RealMeshEyeBandSeated() {
    AssertEyeBand("Assets/B_Brain/Visuals/Resources/NpcVisuals/MiloVisual.prefab", 0.18f, 0.30f);
    AssertEyeBand("Assets/B_Brain/Visuals/Resources/NpcVisuals/MiaVisual.prefab", 0.18f, 0.30f);
  }

  // H. First-talk hook: fires EXACTLY once no matter how often Milo is
  // clicked (the quest-start gate must never double-open the story).
  [Test] public void CT_S01H_FirstTalkFiresOnce() {
    var f = new Fixture();
    GameObject go = new GameObject("MiloTalkTest");
    try {
      MiloPresenter milo = go.AddComponent<MiloPresenter>();
      milo.Bind(f.Bus, f.Quests, f.Hints);
      int talks = 0;
      milo.OnFirstTalk = () => talks++;
      milo.OnMiloClicked();
      milo.OnMiloClicked();
      milo.OnMiloClicked();
      Assert.AreEqual(1, talks, "first talk must fire exactly once across repeat clicks");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // I. HUD wording ownership: Bootstrap's action line survives QuestStarted;
  // completion still celebrates. (Guards the compact-chip contract.)
  [Test] public void CT_S01I_HudKeepsActionLine() {
    var f = new Fixture();
    GameObject go = new GameObject("HudWordingTest");
    try {
      MarketHUD hud = go.AddComponent<MarketHUD>();
      hud.Bind(f.Bus, null);
      hud.ShowObjective("Talk to Milo");
      f.Bus.Publish(new QuestStartedEvent(W1Quest, DateTime.UtcNow));
      Assert.AreEqual("Talk to Milo", hud.CurrentObjective, "quest start must not overwrite the action line");
      f.Bus.Publish(new QuestCompletedEvent(W1Quest, DateTime.UtcNow));
      Assert.AreEqual("Great job!", hud.CurrentObjective, "completion must still celebrate");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // J. Name-label lifecycle: Setup names it, Hide/Show gate story-timed reveal.
  [Test] public void CT_S01J_NameLabelLifecycle() {
    GameObject follow = new GameObject("FollowTest");
    GameObject go = new GameObject("LabelTest");
    try {
      WorldNameLabel label = go.AddComponent<WorldNameLabel>();
      label.Setup("Mia", follow.transform, 2.05f);
      Assert.AreEqual("Mia", label.CurrentName, "setup must name the tag");
      label.Hide();
      Assert.IsFalse(go.activeSelf, "Hide must conceal (Mia pre-introduction)");
      label.Show();
      Assert.IsTrue(go.activeSelf, "Show must reveal (Mia introduction beat)");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
      UnityEngine.Object.DestroyImmediate(follow);
    }
  }

  // K. Replay gating: the button starts hidden (nothing spoken yet) and the
  // story shows it with the first instruction. Guards the onboarding contract.
  [Test] public void CT_S01K_ReplayGatedUntilFirstInstruction() {
    GameObject go = new GameObject("HudReplayTest");
    try {
      MarketHUD hud = go.AddComponent<MarketHUD>();
      hud.BuildUiImmediate(); // batch EditMode does not guarantee Awake delivery
      hud.Bind(new GameEventBus(), null);
      Assert.IsFalse(hud.IsReplayVisible, "replay must start hidden (nothing to hear again yet)");
      hud.SetReplayVisible(true);
      Assert.IsTrue(hud.IsReplayVisible, "story must be able to show replay with the first line");
      hud.SetReplayVisible(false);
      Assert.IsFalse(hud.IsReplayVisible, "replay gating must be reversible");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // L. Name-label readability: the identity cue carries a dark pill backing
  // (bare cream text washed out against sky/awning at gameplay distance).
  [Test] public void CT_S01L_NameLabelHasContrastPill() {
    GameObject follow = new GameObject("FollowPillTest");
    GameObject go = new GameObject("LabelPillTest");
    try {
      WorldNameLabel label = go.AddComponent<WorldNameLabel>();
      label.Setup("Milo", follow.transform, 2.05f);
      Transform pill = go.transform.Find("LabelCanvas/LabelPill");
      Assert.IsNotNull(pill, "label must build a contrast pill under the text");
      Assert.IsTrue(pill.gameObject.activeSelf, "pill must render with the label");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
      UnityEngine.Object.DestroyImmediate(follow);
    }
  }

  // M. Name-label facing: the legible (-Z) face must look at the gameplay
  // camera. Player-build audit 2026-09-12 showed mirrored tags ("oliM"/"siM")
  // from settled framings; the billboard must point +Z away from the viewer.
  // Pure math (no rendering, no frames): pins BillboardRotation's contract.
  [Test] public void CT_S01M_NameLabelFacesCamera() {
    Vector3 label = new Vector3(2.5f, 2.35f, 1.5f);
    Vector3[] cameras = new Vector3[] {
      new Vector3(3.8f, 2.45f, 3.6f),   // m-label macro (close, level)
      new Vector3(-1.9f, 1.5f, 0.3f),   // g-mia framing (low, near)
      new Vector3(-0.8f, 2f, 1.2f),     // k-correct celebrate (settled pose)
      new Vector3(0f, 3.43f, -0.7f),    // spawn follow (high, far)
    };
    foreach (Vector3 cam in cameras) {
      Quaternion r = WorldNameLabel.BillboardRotation(label, cam);
      Vector3 legibleFace = r * new Vector3(0f, 0f, -1f);
      Vector3 toCam = cam - label; toCam.y = 0f;
      float facing = Vector3.Dot(legibleFace.normalized, toCam.normalized);
      Assert.Greater(facing, 0.99f, "legible face must track the camera (audit: mirrored tags)");
    }
  }

  static void AssertEyeBand(string prefabPath, float minEye, float maxEye) {
#if UNITY_EDITOR
    GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
    Assert.IsNotNull(prefab, "prefab must exist: " + prefabPath);
    GameObject inst = UnityEngine.Object.Instantiate(prefab);
    GameObject anchor = new GameObject("AnchorTest");
    GameObject holder = new GameObject("FaceHolderTest");
    try {
      inst.transform.position = Vector3.zero;
      inst.transform.rotation = Quaternion.identity;
      inst.transform.localScale = Vector3.one * 0.5f;
      anchor.transform.position = Vector3.zero;
      anchor.transform.rotation = Quaternion.identity;
      Physics.SyncTransforms(); // settle world matrices (batch-safe, EditMode has no frame tick)
      SkinnedMeshRenderer skin = inst.GetComponentInChildren<SkinnedMeshRenderer>(true);
      Assert.IsNotNull(skin, "visual must carry a skinned mesh");
      Transform head = null;
      foreach (Transform b in skin.bones) {
        if (b != null && b.name == "Head") { head = b; break; }
      }
      Assert.IsNotNull(head, "rig must carry a Head bone");
      Transform headEnd = null;
      foreach (Transform t in head.GetComponentsInChildren<Transform>(true)) {
        if (t != null && t.name == "Head_end") { headEnd = t; break; }
      }
      Assert.IsNotNull(headEnd, "rig must carry Head_end");
      Vector3 center = (head.position + headEnd.position) * 0.5f;
      CharacterPresentation face = holder.AddComponent<CharacterPresentation>();
      face.SetupFace(skin, head, anchor.transform, inst.transform);
      face.BuildFaceImmediate();
      Transform eyeL = head.Find("EyeL");
      Assert.IsNotNull(eyeL, "measured face must seat EyeL");
      float eyeFwd = Vector3.Dot(eyeL.position - center, Vector3.forward);
      Assert.GreaterOrEqual(eyeFwd, minEye, prefabPath + ": eye pane must stay off the skull interior");
      Assert.LessOrEqual(eyeFwd, maxEye, prefabPath + ": eye pane must stay behind the nose-tip plane");
      // Mouths share the measured pane (never buried in the chin: a mouth-band
      // p95 estimate was tried and reverted — throat verts drag it to ≈ −0.14).
      Transform mouth = head.Find("MouthFlat");
      Assert.IsNotNull(mouth, "measured face must seat the mouth kit");
      float mouthFwd = Vector3.Dot(mouth.position - center, Vector3.forward);
      Assert.GreaterOrEqual(mouthFwd, 0.15f, prefabPath + ": mouth must ride the face pane, not the throat");
    } finally {
      UnityEngine.Object.DestroyImmediate(inst);
      UnityEngine.Object.DestroyImmediate(anchor);
      UnityEngine.Object.DestroyImmediate(holder);
    }
#else
    Assert.Ignore("editor-only asset access");
#endif
  }
}
