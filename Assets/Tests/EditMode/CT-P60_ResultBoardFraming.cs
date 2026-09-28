// CT-P60: RESULT-BOARD PAYOFF FRAMING (S3-P2Z18 user round).
// User report: "các bảng result board cạnh camera bị che" — in the newer
// arenas the result board sat right-front of the bin, i.e. right beside the
// success camera, so the payoff shot cropped it at the frame edge (journey
// evidence: fullj-z17g/52_strawberry_success.png and 38_36_rabbit_success.png
// show the "N + tick" board cut at the right/corner). This pins the board
// INSIDE every arena's payoff frame from now on: in front of the success
// camera, inside a sane view angle, at a readable distance, and with the
// camera on the board's FACE side (never the blank back). Pure geometry on
// the built arena — no player, no scene, no camera rig. C# 9.0 only.
using NUnit.Framework;
using UnityEngine;

public class CT_P60_ResultBoardFraming {
  // The payoff shot reads at the default 16:9 view: half the horizontal FOV is
  // ~45.7°, so a board beyond ~35° off-axis reads as a cropped edge element
  // (the old broken spots measured 58–90°).
  const float MaxAngleDeg = 35f;
  const float MaxDistance = 9f;
  const float BoardCenterY = 1.6f; // the frame/digit centre rides the post

  static void AssertPayoffFrames(Transform cam, Transform look, GameObject result, string label) {
    Assert.IsNotNull(cam, label + ": success camera staged");
    Assert.IsNotNull(look, label + ": success look staged");
    Assert.IsNotNull(result, label + ": result board staged");
    Vector3 center = result.transform.position + new Vector3(0f, BoardCenterY, 0f);
    Vector3 toBoard = center - cam.position;
    Vector3 toLook = look.position - cam.position;
    Assert.Greater(Vector3.Dot(toBoard, toLook.normalized), 0f,
      label + ": result board is IN FRONT of the payoff camera");
    float angle = Vector3.Angle(toLook, toBoard);
    Assert.Less(angle, MaxAngleDeg,
      label + ": result board sits inside the payoff frame (angle " + angle.ToString("F1") + "°)");
    Assert.Less(toBoard.magnitude, MaxDistance,
      label + ": result board reads at the payoff distance (" + toBoard.magnitude.ToString("F2") + "m)");
    // The digit plane is the board's face (local -Z): the camera must be on
    // that side, never behind the blank back.
    Assert.Less(Vector3.Dot(cam.position - center, result.transform.forward), 0f,
      label + ": camera sees the board FACE, not its back");
  }

  // B. Game #2 "Number stairs" arena (StairPlayScene).
  [Test] public void P60B_StairPayoffFramesTheResult() {
    GameObject arena = new GameObject("P60StairWorld");
    StairHillBuilder builder = arena.AddComponent<StairHillBuilder>();
    builder.BuildContent(arena.transform);
    try {
      AssertPayoffFrames(builder.CamSuccess, builder.LookSuccess, builder.Result, "stairs");
    } finally { Object.DestroyImmediate(arena); }
  }

  // C. Game #3 "Feed the bunny" arena (RabbitPlayScene).
  [Test] public void P60C_RabbitPayoffFramesTheResult() {
    GameObject arena = new GameObject("P60RabbitWorld");
    RabbitPlayBuilder builder = arena.AddComponent<RabbitPlayBuilder>();
    builder.BoardTarget = 3;
    builder.BuildContent(arena.transform);
    try {
      AssertPayoffFrames(builder.CamSuccess, builder.LookSuccess, builder.Result, "rabbit");
    } finally { Object.DestroyImmediate(arena); }
  }

  // F. Game #4 "Build the tower" arena (BuildTowerScene). The payoff shot
  // raises/pulls with the REAL tower height, so check the extremes 1 and 9.
  [Test] public void P60F_TowerPayoffFramesTheResultEveryTarget() {
    foreach (int target in new[] { 1, 9 }) {
      GameObject arena = new GameObject("P60TowerWorld" + target);
      BuildTowerBuilder builder = arena.AddComponent<BuildTowerBuilder>();
      builder.BoardTarget = target;
      builder.BuildContent(arena.transform);
      try {
        Transform cam = builder.CamSuccess;
        Assert.IsNotNull(cam, "tower success camera staged");
        Vector3 center = builder.Result.transform.position + new Vector3(0f, BoardCenterY, 0f);
        // Mirror BuildTowerGame.IssueShot's success pose (the formula is the
        // game's contract: camBase + (0, top*0.15, -top*0.20), look at pad).
        float top = BuildTowerBuilder.TowerTopY(target);
        Vector3 pos = cam.position + new Vector3(0f, top * 0.15f, -top * 0.20f);
        Vector3 look = builder.PadAnchor.position
          + new Vector3(0f, BuildTowerBuilder.PadTopY + top * 0.5f, 0f);
        Vector3 toBoard = center - pos;
        float angle = Vector3.Angle(look - pos, toBoard);
        Assert.Less(angle, MaxAngleDeg,
          "target " + target + ": result board inside the payoff frame (angle "
          + angle.ToString("F1") + "°)");
        Assert.Greater(Vector3.Dot(toBoard, (look - pos).normalized), 0f,
          "target " + target + ": board in front of the payoff camera");
      } finally { Object.DestroyImmediate(arena); }
    }
  }

  // G. Game #5 "Deliver the apples" arena (DeliveryScene).
  [Test] public void P60G_DeliveryPayoffFramesTheResult() {
    GameObject arena = new GameObject("P60DeliveryWorld");
    DeliveryBuilder builder = arena.AddComponent<DeliveryBuilder>();
    builder.BoardTarget = 4;
    builder.BuildContent(arena.transform);
    try {
      AssertPayoffFrames(builder.CamSuccess, builder.LookSuccess, builder.Result, "delivery");
    } finally { Object.DestroyImmediate(arena); }
  }

  // H. Game #6 hub "Match the pairs" arena (MatchMeadowScene).
  [Test] public void P60H_MatchPayoffFramesTheResult() {
    GameObject arena = new GameObject("P60MatchWorld");
    MatchMeadowBuilder builder = arena.AddComponent<MatchMeadowBuilder>();
    builder.BoardPairs = 3;
    builder.BuildContent(arena.transform);
    try {
      AssertPayoffFrames(builder.CamSuccess, builder.LookSuccess, builder.Result, "match");
    } finally { Object.DestroyImmediate(arena); }
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
}
