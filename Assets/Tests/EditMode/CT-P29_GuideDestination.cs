// CT-P29: player-report wayfinding (ground guide line + destination cross).
// Pins: guide stage/visibility contract (Milo pre-talk, Mia on bring, hidden
// on the find answer-step), line geometry (ground height, gold, collider-free),
// destination cross (flat plus, gold, collider-free, test seams), ClickToMove
// destination introspection. Pure EditMode. C# 9.0 only.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class CT_P29_GuideDestination {
  // 1. Guide starts hidden; answer step hides instantly.
  [Test] public void P29A_GuideStageContract() {
    GameObject go = new GameObject("GuideStageTest");
    GameObject npc = new GameObject("NpcDecoy");
    try {
      var guide = go.AddComponent<QuestGuideLine>();
      guide.BuildDotsImmediate();
      Assert.AreEqual(GuideStage.Hidden, guide.Stage, "guide starts hidden");
      Assert.IsFalse(guide.IsShowing, "nothing drawn before staging");
      guide.SetStage(GuideStage.ToMilo, npc.transform);
      Assert.AreEqual(GuideStage.ToMilo, guide.Stage, "stage follows the HUD objective");
      guide.SetStage(GuideStage.Hidden, null);
      Assert.AreEqual(GuideStage.Hidden, guide.Stage, "answer step hides");
      Assert.IsFalse(guide.IsShowing, "answer step hides instantly, not next frame");
      guide.SetStage(GuideStage.ToMia, null);
      Assert.IsFalse(guide.IsShowing, "a stage without a target never draws");
    } finally {
      Object.DestroyImmediate(go);
      Object.DestroyImmediate(npc);
    }
  }

  // 2. Guide geometry: arrows on the ground pointing player -> target,
  // bigger toward the NPC, collider-free, vivid orange material.
  [Test] public void P29B_GuideGeometry() {
    GameObject go = new GameObject("GuideGeoTest");
    try {
      var guide = go.AddComponent<QuestGuideLine>();
      guide.BuildDotsImmediate();
      guide.DrawBetweenForTests(new Vector3(0f, 0f, 4.5f), new Vector3(0f, 0f, -0.8f));
      Assert.IsTrue(guide.IsShowing, "drawn chain must show");
      Transform dot0 = go.transform.Find("GuideDot0");
      Assert.IsNotNull(dot0, "chain owns pooled arrows");
      // 5.3m / 0.65 spacing = 8 arrows (discrete, never a solid beam).
      Assert.AreEqual(8, guide.VisibleDotCount, "arrow count follows segment length");
      Assert.AreEqual(QuestGuideLine.DotY, dot0.position.y, 0.001f, "arrows ride the ground");
      Assert.AreEqual(0f, dot0.position.x, 0.001f, "chain runs player -> target");
      Transform dot1 = go.transform.Find("GuideDot1");
      Assert.IsNotNull(dot1, "chain has a second arrow");
      Assert.Less(dot1.position.z, dot0.position.z, "arrows order toward the target");
      // Arrow shape: shaft + two chevron arms, tip leading toward +Z travel.
      Transform shaft = dot0.Find("Shaft");
      Transform headL = dot0.Find("HeadL");
      Transform headR = dot0.Find("HeadR");
      Assert.IsNotNull(shaft, "arrow needs its shaft");
      Assert.IsNotNull(headL, "arrow needs its left head arm");
      Assert.IsNotNull(headR, "arrow needs its right head arm");
      Assert.Greater(headL.localPosition.z, shaft.localPosition.z, "head leads the shaft");
      Assert.AreEqual(45f, Mathf.Abs(headL.localRotation.eulerAngles.y), 0.001f, "chevron arms slant");
      Assert.AreEqual(0, go.GetComponentsInChildren<Collider>(true).Length,
        "guide arrows must never eat clicks");
      Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
      Assert.GreaterOrEqual(rends.Length, 8, "every arrow must render");
      foreach (Renderer r in rends) {
        Assert.IsNotNull(r.sharedMaterial, "arrows must carry a material (never magenta)");
        if (r.sharedMaterial.HasProperty("_BaseColor")) {
          Color c = r.sharedMaterial.GetColor("_BaseColor");
          Assert.Greater(c.r, 0.9f, "vivid orange, never washed out");
          Assert.Less(c.b, 0.2f, "vivid orange, never washed out");
        }
      }
    } finally {
      Object.DestroyImmediate(go);
    }
  }

  // 2c. Size gradient: arrows grow toward the NPC (relative, never absolute).
  [Test] public void P29B3_ArrowGradientGrowsToNpc() {
    Assert.AreEqual(0.7f, QuestGuideLine.Gradient(0, 8), 0.001f, "player end is smallest");
    Assert.AreEqual(1.5f, QuestGuideLine.Gradient(7, 8), 0.001f, "NPC end is biggest");
    Assert.Greater(QuestGuideLine.Gradient(5, 8), QuestGuideLine.Gradient(2, 8),
      "gradient rises monotonically toward the NPC");
    Assert.AreEqual(1f, QuestGuideLine.Gradient(0, 1), 0.001f, "degenerate chain stays neutral");
    GameObject go = new GameObject("GuideGradientTest");
    try {
      var guide = go.AddComponent<QuestGuideLine>();
      guide.BuildDotsImmediate();
      guide.DrawBetweenForTests(new Vector3(0f, 0f, 4.5f), new Vector3(0f, 0f, -0.8f));
      Transform dot0 = go.transform.Find("GuideDot0");
      Transform dot7 = go.transform.Find("GuideDot7");
      Assert.Greater(dot7.localScale.x, dot0.localScale.x, "live arrows grow toward the NPC");
    } finally {
      Object.DestroyImmediate(go);
    }
  }

  // 2b. Marching wave: each dot peaks LATER than the one before it (the
  // bright hump travels player -> NPC), trough never vanishes. Peak times
  // come from the pure WaveScale (no live frames needed).
  [Test] public void P29B2_GuideWaveMarchesToNpc() {
    for (int i = 0; i < QuestGuideLine.MaxDots; i++) {
      float s = QuestGuideLine.WaveScale(i, 0.37f);
      Assert.GreaterOrEqual(s, 0.44f, "trough stays visible (dot " + i + ")");
      Assert.LessOrEqual(s, 1.01f, "peak never overshoots (dot " + i + ")");
    }
    // One period holds one hump per dot; dots 0..10 sit clear of the wrap.
    for (int i = 0; i < 10; i++) {
      float t0 = PeakTime(i);
      float t1 = PeakTime(i + 1);
      Assert.Greater(t1, t0, "dot " + (i + 1) + " must peak after dot " + i + " (march to NPC)");
    }
  }

  static float PeakTime(int dot) {
    float period = 1f / QuestGuideLine.WaveHertz;
    float bestT = 0f;
    float bestS = float.MinValue;
    for (float t = 0f; t < period; t += 0.001f) {
      float s = QuestGuideLine.WaveScale(dot, t);
      if (s > bestS) { bestS = s; bestT = t; }
    }
    return bestT;
  }

  // 3. Destination cross: flat gold plus, hidden until shown, collider-free.
  [Test] public void P29C_DestinationCrossContract() {
    GameObject go = new GameObject("DestCrossTest");
    try {
      var marker = go.AddComponent<DestinationMarker>();
      marker.BuildMarkImmediate();
      Assert.IsFalse(marker.IsShowing, "cross starts hidden (idle player)");
      Transform cross = go.transform.Find("DestinationCross");
      Assert.IsNotNull(cross, "must own exactly one reusable cross");
      Assert.IsNotNull(cross.Find("CrossArmX"), "plus needs its X arm");
      Assert.IsNotNull(cross.Find("CrossArmZ"), "plus needs its Z arm");
      Assert.AreEqual(0, cross.GetComponentsInChildren<Collider>(true).Length,
        "cross must never intercept the click raycast");
      Renderer[] rends = cross.GetComponentsInChildren<Renderer>(true);
      Assert.GreaterOrEqual(rends.Length, 2, "cross must render both arms");
      foreach (Renderer r in rends)
        Assert.IsNotNull(r.sharedMaterial, "arms must carry a material (never default-white)");
      marker.ShowAtForTests(new Vector3(2f, 0f, 3f));
      Assert.IsTrue(marker.IsShowing, "tap-to-move must show the cross");
      Assert.AreEqual(DestinationMarker.MarkY, cross.position.y, 0.001f, "cross lies on the ground");
      Assert.AreEqual(2f, cross.position.x, 0.001f, "cross sits at the destination");
      marker.HideForTests();
      Assert.IsFalse(marker.IsShowing, "arrival must hide the cross");
    } finally {
      Object.DestroyImmediate(go);
    }
  }

  // 4. ClickToMove destination introspection (the cross reads this).
  [Test] public void P29D_MoverDestinationSeam() {
    GameObject go = new GameObject("MoverSeamTest");
    try {
      var mover = go.AddComponent<ClickToMove>();
      Assert.IsFalse(mover.HasDestination, "fresh mover holds no destination");
      Vector3 d = mover.Destination;
      Assert.IsTrue(float.IsFinite(d.x + d.y + d.z), "destination getter must never NaN");
    } finally {
      Object.DestroyImmediate(go);
    }
  }

  // 5. REC badge is dot-only while recording (no text can burn into exports).
  [Test] public void P29E_IndicatorDotOnlyWhileRecording() {
    GameObject go = new GameObject("IndicatorDotTest");
    try {
      var badge = go.AddComponent<RecordingIndicator>();
      badge.BuildUiImmediate();
      badge.SetRecording(true, 12f);
      Assert.IsTrue(badge.IsRecordingShown, "badge stays live while recording");
      Text text = null;
      Image dot = null;
      Image box = null;
      foreach (Text t in go.GetComponentsInChildren<Text>(true)) {
        if (t != null && t.gameObject.name == "Text") text = t;
      }
      foreach (Image img in go.GetComponentsInChildren<Image>(true)) {
        if (img == null) continue;
        if (img.gameObject.name == "Dot") dot = img;
        else if (img.gameObject.name == "Box") box = img;
      }
      Assert.IsNotNull(text, "badge owns its text slot");
      Assert.IsNotNull(dot, "badge owns its dot");
      Assert.IsNotNull(box, "badge owns its box");
      Assert.AreEqual(string.Empty, text.text, "no text while recording (never in the file)");
      Assert.IsFalse(box.enabled, "box background off while recording");
      Assert.IsTrue(dot.gameObject.activeSelf, "dot keeps the live cue");
      badge.SetFinishing();
      Assert.AreEqual("Đang kết thúc bản thu...", text.text, "finishing line returns after stop");
      Assert.IsTrue(box.enabled, "box background returns for the finishing line");
      badge.Hide();
      Assert.IsFalse(badge.IsShowing, "hide clears the badge");
    } finally {
      Object.DestroyImmediate(go);
    }
  }
}
