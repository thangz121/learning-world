// D_Audio/MockSpeechProvider.cs — Agent D (W0-T1). TEST-ONLY (never production).
// Queue RawSpeechResults, StartListening fires the next one FIFO. Empty queue fires a
// single default item with ErrorReason="MockEmpty" so awaiting listeners always complete.
using System;
using System.Collections.Generic;

public sealed class MockSpeechProvider : ISpeechProvider {
  readonly Queue<RawSpeechResult> _queued = new Queue<RawSpeechResult>();
  readonly object _gate = new object();

  public event Action<RawSpeechResult> Raw;

  public int QueuedCount {
    get { lock (_gate) { return _queued.Count; } }
  }

  public void EnqueueRaw(RawSpeechResult result) {
    lock (_gate) { _queued.Enqueue(result); }
  }

  public void StartListening(WordId expected, int timeoutSec) {
    RawSpeechResult next;
    bool has;
    lock (_gate) {
      has = _queued.Count > 0;
      next = has ? _queued.Dequeue() : default(RawSpeechResult);
    }
    if (!has) {
      next = new RawSpeechResult {
        Transcript = "",
        Confidence = 0f,
        PronScore = 0f,
        FluencyScore = 0f,
        CompletenessScore = 0f,
        ErrorReason = "MockEmpty",
      };
    }
    Raw?.Invoke(next);
  }

  public void Stop() {
    // Test double holds no session: no-op.
  }
}
