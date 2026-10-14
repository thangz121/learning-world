// Assets/Tests/EditMode/CT-P22S_SpeakingUiSyllableColors.cs — Backlog #11 per-syllable color pin.
// Pins the payload-driven color mapping used by the SpeakingStage word/IPA segment rows.

using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class CT_P22S_SpeakingUiSyllableColors {

  [Test]
  public void P22S_SyllableUiColor_MapsGreenYellowRedExactly() {
    Assert.That(SpeakingStageBuilder.UiColorToColor("GREEN"), Is.EqualTo(new Color(0.12f, 0.65f, 0.28f)));
    Assert.That(SpeakingStageBuilder.UiColorToColor("YELLOW"), Is.EqualTo(new Color(0.92f, 0.60f, 0.12f)));
    Assert.That(SpeakingStageBuilder.UiColorToColor("RED"), Is.EqualTo(new Color(0.85f, 0.25f, 0.25f)));
  }

  [Test]
  public void P22S_SyllableUiColor_UnknownFallsBackToNeutralNeverThrows() {
    Assert.That(SpeakingStageBuilder.UiColorToColor(null), Is.EqualTo(new Color(0.45f, 0.40f, 0.55f)));
    Assert.That(SpeakingStageBuilder.UiColorToColor("purple"), Is.EqualTo(new Color(0.45f, 0.40f, 0.55f)));
  }
}
