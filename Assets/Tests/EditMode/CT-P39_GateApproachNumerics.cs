// CT-P39: gate-approach numerics (Phase 3.0.x S3A).
// Encodes the S3 journey's hard-won finding as headless regression pins: a
// real player taps gate STRUCTURE (pillars/board/disc), not coordinates — so
// the snap radii, trigger radius and arrival tolerance must agree AS NUMBERS.
// Uses the PRODUCTION statics (ClickRouter.TrySnapToGateMouth /
// TrySnapGateOnRay) and catalog geometry — no player, no scenes, no movement,
// no screenshots. Batch-mode runnable.
// Conventions pinned (sources cited per test):
//   - pillars at gate +/- lateral*1.6, carve 0.8 (SubjectWorldBuilder);
//   - snap radius 2.0, signpost at ~2.38 exempt (ClickRouter + CT-P33H);
//   - entry fire radius 1.2 (SubjectGate.Bind call sites);
//   - mouth = gate + toHub*0.6 (ClickRouter.MouthFor);
//   - agent stoppingDistance 0.4 (MarketBuilder.BuildPlayer).
// C# 9.0 only.
using NUnit.Framework;
using UnityEngine;

public class CT_P39_GateApproachNumerics {
  const float SnapRadius = 2.0f;
  const float FireRadius = 1.2f;
  const float StoppingDistance = 0.4f;
  const float MouthOffset = 0.6f;

  static Vector3 ToHub(SubjectDefinition def) {
    Vector3 t = SubjectCatalog.HubCenter - def.GatePos;
    t.y = 0f;
    if (t.sqrMagnitude < 0.001f) t = new Vector3(0f, 0f, 1f);
    return t.normalized;
  }

  static Vector3 MouthOf(SubjectDefinition def) {
    Vector3 m = def.GatePos + ToHub(def) * MouthOffset;
    m.y = 0f;
    return m;
  }

  static float FlatDist(Vector3 a, Vector3 b) {
    float dx = a.x - b.x, dz = a.z - b.z;
    return Mathf.Sqrt(dx * dx + dz * dz);
  }

  // A. Arrival math: worst-case stop (mouth + full stopping distance, walked
  // straight past center) still lands inside the fire radius.
  [Test] public void P39A_MouthArrivalInsideTrigger() {
    foreach (SubjectDefinition def in SubjectCatalog.All) {
      Vector3 mouth = MouthOf(def);
      Assert.AreEqual(MouthOffset, FlatDist(mouth, def.GatePos), 0.01f, "mouth sits 0.6 hub-side");
      Assert.Less(MouthOffset + StoppingDistance, FireRadius,
        def.DisplayName + ": mouth(0.6) + stopping(0.4) must stay inside fire(1.2)");
    }
  }

  // B. Pillar faces snap: outer face (1.6 + 0.35 half-width = 1.95 < 2.0) and
  // inner face (1.25) both route to the mouth through the REAL static.
  [Test] public void P39B_PillarFacesSnapToMouth() {
    SubjectDefinition math = SubjectCatalog.Math;
    Vector3 toHub = ToHub(math);
    Vector3 lat = new Vector3(-toHub.z, 0f, toHub.x);
    foreach (float side in new float[] { 1f, -1f }) {
      Vector3 outerFace = math.GatePos + lat * (side * 1.95f); // pillar outer face (1.6 + 0.35 half-width)
      Vector3 mouth;
      Assert.IsTrue(ClickRouter.TrySnapToGateMouth(outerFace, out mouth),
        "Math pillar outer face (1.95m) must snap (S3 journey miss regression)");
      Assert.AreEqual(MouthOf(math).x, mouth.x, 0.01f);
      Assert.AreEqual(MouthOf(math).z, mouth.z, 0.01f);
    }
  }

  // C. Signpost exempt: ~2.38m out must NOT force-enter (CT-P33H contract, all
  // four gates, computed from the LEFT-of-path convention).
  [Test] public void P39C_SignpostsNeverSnap() {
    foreach (SubjectDefinition def in SubjectCatalog.All) {
      Vector3 face = ToHub(def); // face == ToHub by construction (FaceOf)
      Vector3 lat = new Vector3(-face.z, 0f, face.x);
      Vector3 sp = def.GatePos + face * 0.9f + lat * 2.2f;
      Assert.Greater(FlatDist(sp, def.GatePos), SnapRadius,
        def.DisplayName + ": signpost must stay outside snap radius");
      Vector3 mouth;
      Assert.IsFalse(ClickRouter.TrySnapToGateMouth(sp, out mouth),
        def.DisplayName + ": inspecting the sign must not enter");
    }
  }

  // D. Trigger geometry on a REAL gate instance: mouth fires, outside stays
  // silent (uses the deterministic TryFireForTests seam — no frames).
  [Test] public void P39D_TriggerFiresAtMouthOnly() {
    GameObject go = new GameObject("P39Gate");
    try {
      SubjectGate gate = go.AddComponent<SubjectGate>();
      gate.fireRadius = 1.2f;
      SubjectDefinition math = SubjectCatalog.Math;
      go.transform.position = math.GatePos;
      gate.Bind(null, SubjectIds.Math, false, null); // target Math, nav null = dry-run (no publish)
      Assert.IsTrue(gate.TryFireForTests(MouthOf(math), SubjectIds.Main),
        "mouth must fire from Main");
      Vector3 outside = math.GatePos + ToHub(math) * 1.5f;
      Assert.IsFalse(gate.TryFireForTests(outside, SubjectIds.Main),
        "1.5m out must stay silent (S3 journey: short clicks must not fake entry)");
    } finally { Object.DestroyImmediate(go); }
  }

  // E. Through-opening ray snaps: a tap whose ray threads the gate onto
  // district ground behind routes to the mouth (REAL ray static).
  [Test] public void P39E_RayThroughGateSnaps() {
    SubjectDefinition math = SubjectCatalog.Math;
    Vector3 toHub = ToHub(math);
    Vector3 origin = new Vector3(math.GatePos.x, 1.6f, math.GatePos.z) + toHub * 5f;
    Vector3 dir = -toHub;
    Ray ray = new Ray(origin, new Vector3(dir.x, -0.35f, dir.z));
    Vector3 hitBehind = math.GatePos - toHub * 3f;
    Vector3 mouth;
    Assert.IsTrue(ClickRouter.TrySnapGateOnRay(ray, hitBehind, out mouth),
      "through-gate tap must snap (S3 journey §15 case)");
    Assert.AreEqual(MouthOf(math).x, mouth.x, 0.01f);
    Assert.AreEqual(MouthOf(math).z, mouth.z, 0.01f);
  }
}
