// CT-012: duplicate subscribe idempotent (v5). Owner: Lead. Must stay GREEN.
using NUnit.Framework;

public class CT_012_DuplicateIdempotent {
  struct Ping { public int N; }

  [Test] public void CT_012_SameHandlerTwice_ReceivesOnce() {
    var bus = new GameEventBus(); int calls = 0;
    System.Action<Ping> h = _ => calls++;
    bus.Subscribe(h);
    bus.Subscribe(h);
    bus.Publish(new Ping());
    Assert.AreEqual(1, calls, "duplicate Subscribe must be idempotent, never double-deliver");
  }
}
