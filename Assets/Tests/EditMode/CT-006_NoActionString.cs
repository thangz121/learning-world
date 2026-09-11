// CT-006: No Action<string> / no dual TTS path (v6.1 FIX-001, v4). Owner: Lead. Must stay GREEN.
using NUnit.Framework;

public class CT_006_NoActionString {
  [Test] public void CT_006_ISpeechProvider_HasNoSpeak() {
    Assert.IsNull(typeof(ISpeechProvider).GetMethod("Speak"),
      "ISpeechProvider must be INPUT-only; OUTPUT speech only via IAudioDirector");
  }
  [Test] public void CT_006_IQuestService_HasNoCompletedEvent() {
    Assert.IsNull(typeof(IQuestService).GetEvent("Completed"),
      "Quest completion only via QuestCompletedEvent on IGameEventBus");
  }
}
