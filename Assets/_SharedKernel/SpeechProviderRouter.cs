// _SharedKernel/SpeechProviderRouter.cs — Lead owns (v6.3 Fix 3).
// Gameplay systems inject THIS (as ISpeechProvider) once and keep it forever.
// Runtime online<->offline switches happen INSIDE the router, so every consumer
// always talks to the current provider. No system caches a stale Azure instance.
using System;

public sealed class SpeechProviderRouter : ISpeechProvider {
  ISpeechProvider _inner;
  public event Action<RawSpeechResult> Raw;

  public SpeechProviderRouter(ISpeechProvider initial) {
    _inner = initial ?? throw new ArgumentNullException(nameof(initial));
    _inner.Raw += Forward;
  }

  public void SwitchTo(ISpeechProvider next) {
    if (next == null || ReferenceEquals(next, _inner)) return;
    try { _inner.Stop(); } catch (Exception) { }
    _inner.Raw -= Forward;
    _inner = next;
    _inner.Raw += Forward;
  }

  public void StartListening(WordId expected, int timeoutSec) => _inner.StartListening(expected, timeoutSec);
  public void Stop() { try { _inner.Stop(); } catch (Exception) { } }

  void Forward(RawSpeechResult r) => Raw?.Invoke(r);
}
