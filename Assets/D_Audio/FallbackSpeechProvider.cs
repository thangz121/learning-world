// D_Audio/FallbackSpeechProvider.cs — Agent D (W0-T1). OFFLINE intent mode (production).
// StartListening publishes exactly one Raw with ErrorReason="Offline" + empty transcript
// then completes. Downstream: Policy -> TryTogether -> Hint demo path, quest continues
// with pre-gen core audio (CT-A03). Never throws, never hangs.
using System;

public sealed class FallbackSpeechProvider : ISpeechProvider {
  public event Action<RawSpeechResult> Raw;

  public void StartListening(WordId expected, int timeoutSec) {
    Raw?.Invoke(new RawSpeechResult {
      Transcript = "",
      Confidence = 0f,
      PronScore = 0f,
      FluencyScore = 0f,
      CompletenessScore = 0f,
      ErrorReason = "Offline",
    });
  }

  public void Stop() {
    // Nothing to cancel in W0: no-op.
  }
}
