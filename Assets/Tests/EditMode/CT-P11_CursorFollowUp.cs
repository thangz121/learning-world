// CT-P11: player-report cursor follow-ups (click-direction arrow, marker
// orientation, pointer confinement). Pure EditMode: deterministic seams +
// static contracts only. C# 9.0 only.
using NUnit.Framework;
using UnityEngine;

public class CT_P11_CursorFollowUp {
  // 1. Click direction: the arrow leans where the click points relative to
  // the player (assumed walk direction), not the current facing.
  [Test] public void CT_P11A_ClickDirectionDrivesArrow() {
    GameObject go = new GameObject("ClickDirTest");
    try {
      var cursor = go.AddComponent<CursorPresenter>();
      cursor.BuildCursorImmediate();
      Assert.IsFalse(cursor.HasClickDirection, "no click yet: facing cue covers");
      Vector2 player = new Vector2(400f, 300f);
      cursor.RegisterClickForTests(player, new Vector2(500f, 300f)); // click right
      Assert.IsTrue(cursor.HasClickDirection, "click registers a direction");
      Assert.AreEqual(90f, cursor.CurrentAngle, 0.001f, "click right of player leans +90");
      cursor.RegisterClickForTests(player, new Vector2(400f, 100f)); // click below
      Assert.AreEqual(180f, Mathf.Abs(cursor.CurrentAngle), 0.001f, "click below player flips down");
      cursor.RegisterClickForTests(player, player); // degenerate: straight, never NaN
      Assert.AreEqual(0f, cursor.CurrentAngle, 0.001f, "click on the player reads straight");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  // 2. Marker chevron points DOWN (∨): each arm's inner end (toward x=0)
  // sits LOWER than its outer end, and the tip hangs below the shaft.
  [Test] public void CT_P11B_MarkerPointsDown() {
    GameObject go = new GameObject("MarkerDownTest");
    try {
      var cursor = go.AddComponent<CursorPresenter>();
      cursor.BuildCursorImmediate();
      Transform marker = go.transform.Find("HoverMarker");
      Assert.IsNotNull(marker, "marker exists");
      Transform shaft = marker.Find("MarkShaft");
      Transform headL = marker.Find("MarkHeadL");
      Transform headR = marker.Find("MarkHeadR");
      Assert.IsNotNull(shaft, "shaft kept");
      Assert.IsNotNull(headL, "left arm kept");
      Assert.IsNotNull(headR, "right arm kept");
      Vector2 innerL, outerL, innerR, outerR;
      ArmEnds(headL, true, out innerL, out outerL);
      ArmEnds(headR, false, out innerR, out outerR);
      Assert.Less(innerL.y, outerL.y, "left arm: inner end lower (down chevron)");
      Assert.Less(innerR.y, outerR.y, "right arm: inner end lower (down chevron)");
      float tipY = Mathf.Min(innerL.y, innerR.y);
      float shaftBottom = shaft.localPosition.y - shaft.localScale.y * 0.5f;
      Assert.Less(tipY, shaftBottom, "chevron tip hangs below the shaft");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
    }
  }

  static void ArmEnds(Transform arm, bool isLeft, out Vector2 inner, out Vector2 outer) {
    Vector2 pos = new Vector2(arm.localPosition.x, arm.localPosition.y);
    float halfLen = arm.localScale.x * 0.5f;
    float rad = arm.localRotation.eulerAngles.z * Mathf.Deg2Rad;
    Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    Vector2 endA = pos + dir * halfLen;
    Vector2 endB = pos - dir * halfLen;
    // Inner end = closer to x=0.
    if (Mathf.Abs(endA.x) <= Mathf.Abs(endB.x)) {
      inner = endA;
      outer = endB;
    } else {
      inner = endB;
      outer = endA;
    }
  }

  // 3. Pointer confinement state machine: click confines, M toggles release.
  [Test] public void CT_P11C_LockStateMachine() {
    Assert.AreEqual(CursorLockMode.Confined,
      CursorPresenter.NextLockState(CursorLockMode.None, true, false), "first click confines");
    Assert.AreEqual(CursorLockMode.Confined,
      CursorPresenter.NextLockState(CursorLockMode.Confined, true, false), "clicks while confined stay");
    Assert.AreEqual(CursorLockMode.None,
      CursorPresenter.NextLockState(CursorLockMode.Confined, false, true), "M releases to normal cursor");
    Assert.AreEqual(CursorLockMode.Confined,
      CursorPresenter.NextLockState(CursorLockMode.None, false, true), "M toggles back to confined");
    Assert.AreEqual(CursorLockMode.None,
      CursorPresenter.NextLockState(CursorLockMode.None, false, false), "idle stays released");
    Assert.AreEqual(CursorLockMode.Confined,
      CursorPresenter.NextLockState(CursorLockMode.Confined, false, false), "idle stays confined");
    Assert.AreEqual(CursorLockMode.Confined,
      CursorPresenter.NextLockState(CursorLockMode.None, true, true), "M wins over same-frame click");
  }
}
