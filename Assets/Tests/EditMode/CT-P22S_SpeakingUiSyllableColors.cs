// Assets/Tests/EditMode/CT-P22S_SpeakingUiSyllableColors.cs — Backlog #11 color pins (real evidence).
// Pins the per-syllable mapping: sub-component statuses + real decision (NOT the uncalibrated
// fusion star color, which produced false REDs on passed words).

using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class CT_P22S_SpeakingUiSyllableColors {

  [Test]
  public void P22S_AllGoodStatuses_GreenOnPass() {
    Assert.That(SpeakingStageBuilder.SyllableColorFor("GOOD", "GOOD", "GOOD", SpeakingDecision.Pass),
        Is.EqualTo(new Color(0.12f, 0.65f, 0.28f)));
  }

  [Test]
  public void P22S_DroppedCodaOnPassedWord_ShowsGentleYellowNeverRed() {
    Assert.That(SpeakingStageBuilder.SyllableColorFor("GOOD", "GOOD", "DROPPED", SpeakingDecision.Pass),
        Is.EqualTo(new Color(0.92f, 0.60f, 0.12f)),
        "4yo gentle cap: a passed word never shows a RED segment.");
  }

  [Test]
  public void P22S_DroppedCodaOnFailedWord_Red() {
    Assert.That(SpeakingStageBuilder.SyllableColorFor("GOOD", "GOOD", "DROPPED", SpeakingDecision.WrongWord),
        Is.EqualTo(new Color(0.85f, 0.25f, 0.25f)));
  }

  [Test]
  public void P22S_NoStatusInfo_FallsBackToDecisionColor() {
    Assert.That(SpeakingStageBuilder.SyllableColorFor("", "", "", SpeakingDecision.Partial),
        Is.EqualTo(new Color(0.92f, 0.60f, 0.12f)));
    Assert.That(SpeakingStageBuilder.SyllableColorFor("", "", "", SpeakingDecision.TooWeak),
        Is.EqualTo(new Color(0.85f, 0.25f, 0.25f)));
  }

  [Test]
  public void P22S_MasterVocabFlags_MapLikeFusionStatuses() {
    Assert.That(SpeakingStageBuilder.SyllableColorFor("OK", "OK", "UNRELEASED_L1", SpeakingDecision.Partial),
        Is.EqualTo(new Color(0.92f, 0.60f, 0.12f)));
    Assert.That(SpeakingStageBuilder.SyllableColorFor("UNASPIRATED_L1", "OK", "DROPPED", SpeakingDecision.WrongWord),
        Is.EqualTo(new Color(0.85f, 0.25f, 0.25f)));
  }
}
