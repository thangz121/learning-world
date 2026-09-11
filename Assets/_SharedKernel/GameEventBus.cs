// _SharedKernel/GameEventBus.cs — simple typed bus (Lead owns). No weak-refs in core.
// Rules: subscriber owns subscription (dispose in OnDisable); Publish never throws on
// destroyed/disposed subscribers; duplicate Subscribe of same handler is idempotent (CT-011/012).
using System;
using System.Collections.Generic;

public sealed class GameEventBus : IGameEventBus {
  readonly Dictionary<Type, List<Delegate>> _map = new();

  public IDisposable Subscribe<T>(Action<T> handler) {
    var t = typeof(T);
    if (!_map.TryGetValue(t, out var list)) { list = new List<Delegate>(); _map[t] = list; }
    if (list.Contains(handler)) return new Sub(() => { }); // idempotent: second subscribe is a no-op
    list.Add(handler);
    return new Sub(() => list.Remove(handler));
  }

  public void Publish<T>(T gameEvent) {
    if (!_map.TryGetValue(typeof(T), out var list) || list.Count == 0) return;
    var snapshot = list.ToArray(); // disposed handlers removed; never throws for unsubscribed
    foreach (var d in snapshot) {
      try { ((Action<T>)d)(gameEvent); }
      catch (Exception) { /* subscriber destroyed mid-delivery: drop, never crash bus */ }
    }
  }

  sealed class Sub : IDisposable {
    Action _dispose; bool _done;
    public Sub(Action dispose) => _dispose = dispose;
    public void Dispose() { if (_done) return; _done = true; _dispose?.Invoke(); }
  }
}
