// CT-007: IDs typed + PlayerAction enum (v4 FIX-002). Owner: Lead. Must stay GREEN.
using NUnit.Framework;

public class CT_007_IdsTyped {
  [Test] public void CT_007_PlayerAction_HasAllFive() {
    foreach (var n in new[] { "Find", "Bring", "Speak", "Give", "Select" })
      Assert.IsTrue(System.Enum.IsDefined(typeof(PlayerAction), n), "missing PlayerAction." + n);
  }
  [Test] public void CT_007_IDS_Normalize() {
    Assert.AreEqual("apple", new WordId("  Apple ").Value);
    Assert.AreEqual("market_help_mia", new QuestId("Market_Help_Mia").Value);
    Assert.AreEqual("milo_v1", new VoiceProfileId("MILO_V1").Value);
  }
}
