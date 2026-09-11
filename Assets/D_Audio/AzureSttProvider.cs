// D_Audio/AzureSttProvider.cs — Agent D (W0-T1). ONLINE input stub (real Azure in W1).
// Throws on StartListening until the key/SDK lands; Raw never fires spontaneously.
// Router default still points here so the offline->online swap path is exercised.
using System;

public sealed class AzureSttProvider : ISpeechProvider {
  // Required by ISpeechProvider; fires only in W1 (real Azure session). Pragma: no
  // spontaneous fire in W0 by contract, so the event is intentionally unraised here.
#pragma warning disable CS0067
  public event Action<RawSpeechResult> Raw;
#pragma warning restore CS0067

  public void StartListening(WordId expected, int timeoutSec) {
    throw new InvalidOperationException("Azure STT key not configured (W1)");
  }

  public void Stop() {
    // No session exists in W0: no-op.
  }
}
