// CT-011: Dispose stops delivery; publish never throws (ADR-003, v5). Owner: Lead. Must stay GREEN.
using NUnit.Framework;

public class CT_011_DisposeStopsDelivery {
  struct Ping { public int N; }

  [Test] public void CT_011_DisposedHandler_NotCalled() {
    var bus = new GameEventBus(); int calls = 0;
    var sub = bus.Subscribe<Ping>(_ => calls++);
    sub.Dispose();
    bus.Publish(new Ping());
    Assert.AreEqual(0, calls);
  }

  [Test] public void CT_011_PublishWithoutSubscribers_DoesNotThrow() {
    Assert.DoesNotThrow(() => new GameEventBus().Publish(new Ping()));
  }
}
