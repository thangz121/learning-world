// CT-P27: REMOVAL order 2026-09-18 (user: girl visual failed gate, delete
// gender selection). Pins the Boy-lock contract: Girl requests coerce to
// Boy, zero girl parts ever build, dialog/panel/toggle/bow/hair are gone,
// save DTO still round-trips (architecture intact). C# 9.0 only.
using NUnit.Framework;
using System.IO;
using UnityEngine;

public class CT_P27_GenderIdentity {
  [Test] public void P27A_GenderFieldsPersist() {
    var fresh = new PlayerProgress();
    Assert.IsFalse(fresh.GenderChosen, "default DTO state");
    string name = "LWE-P27-" + System.Guid.NewGuid().ToString("N") + ".json";
    var save = new LocalSave(name);
    try {
      var p = new PlayerProgress { PlayerGender = PlayerGender.Girl, GenderChosen = true };
      save.Save(p);
      PlayerProgress back = save.Load();
      Assert.IsTrue(back.GenderChosen, "DTO round-trips (save format unbroken)");
      Assert.AreEqual(PlayerGender.Girl, back.PlayerGender, "DTO stores what it is given (coercion happens at gameplay, not storage)");
    } finally {
      try { File.Delete(save.FilePath); } catch (System.Exception) { }
    }
  }

  [Test] public void P27B_GirlRequestCoercesToBoy() {
    var go = new GameObject("P27B");
    var viz = go.AddComponent<PlayerVisual>();
    try {
      Assert.DoesNotThrow(() => viz.SetGender(PlayerGender.Girl));
      Assert.AreEqual(PlayerGender.Boy, viz.Gender, "Girl unavailable: visual stays Boy");
      Assert.AreEqual(0, viz.GirlPartCount, "zero girl parts ever build");
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void P27C_NoGirlMeshesExist() {
    var go = new GameObject("P27C");
    var viz = go.AddComponent<PlayerVisual>();
    try {
      viz.SetGender(PlayerGender.Girl);
      viz.SetGender(PlayerGender.Boy);
      foreach (string n in new[] {
          "GirlHead", "GirlTorso", "GirlHair", "GirlFringe", "GirlPonyBase",
          "GirlDress", "GirlSkirt", "GirlShoeL", "GirlShoeR", "GirlBowL" }) {
        Transform found = null;
        try {
          foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) {
            if (t != null && t.name == n) { found = t; break; }
          }
        } catch (System.Exception) { }
        Assert.IsNull(found, "removed visual must not exist: " + n);
      }
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void P27D_TwirlApiStillExists() {
    // Kit API untouched by the removal (Boy celebrate path owns PlayHop;
    // PlayTwirl stays available, simply never triggered by the player).
    var go = new GameObject("P27D");
    var kit = go.AddComponent<CharacterPresentation>();
    try {
      Assert.DoesNotThrow(() => kit.PlayTwirl());
      Assert.IsTrue(kit.IsTwirling);
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void P27E_FaceKitDefaultsKeepGolden() {
    var go = new GameObject("P27E");
    var kit = go.AddComponent<CharacterPresentation>();
    try {
      Assert.AreEqual(1f, kit.FaceBoost, 0.001f, "default boost reproduces golden numbers exactly");
      Assert.IsFalse(kit.BrightEyes, "iris path off by default (Milo/Mia/Boy untouched)");
      Assert.IsFalse(kit.IsFaceBuilt, "bare kit reports no face");
      Assert.DoesNotThrow(() => kit.RebuildFaceNow(), "rebuild with no head warns, never throws");
    } finally { Object.DestroyImmediate(go); }
  }
}
