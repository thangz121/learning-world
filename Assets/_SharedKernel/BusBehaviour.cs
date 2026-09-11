// _SharedKernel/BusBehaviour.cs — subscribe helper (Lead owns).
// Usage: On<T>(handler, bus) in OnEnable; auto-disposes in OnDisable.
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BusBehaviour : MonoBehaviour {
  readonly List<IDisposable> _subs = new();
  protected void On<T>(Action<T> handler, IGameEventBus bus) => _subs.Add(bus.Subscribe(handler));
  protected virtual void OnDisable() {
    foreach (var s in _subs) s.Dispose();
    _subs.Clear();
  }
}
