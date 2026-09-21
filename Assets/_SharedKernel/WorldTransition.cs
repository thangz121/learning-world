// _SharedKernel/WorldTransition.cs — Lead-owned world-module transition contract.
// Phase 3.0.x: subject worlds are SEPARATE additive scenes sharing ONE Core
// (user-locked direction). This file owns the transition CONTRACT + a
// scene-op-agnostic state machine; real Unity scene calls live behind
// ISceneOps (Bootstrap adapter), so the machine is fully EditMode-testable
// with fakes. States always reflect REAL op completion — never fake progress:
// a failed load returns to Idle, a failed unload stays InSubject.
// C# 9.0 only (no records, no target-typed new).
using System;
using System.Threading.Tasks;

public enum WorldTransitionState {
  Idle,      // Main world active, no subject loaded
  Loading,   // subject scene load in flight
  InSubject, // subject scene loaded and active
  Unloading  // subject scene unload in flight
}

// Scene-operation seam (Bootstrap implements with SceneManager; tests fake it).
public interface ISceneOps {
  bool IsLoaded(string sceneName);
  Task LoadAdditiveAsync(string sceneName);
  Task UnloadAsync(string sceneName);
}

// Subject-world transition state machine. One instance owned by GameInstaller.
// Spatial (scene-less) subjects are NOT handled here — empty sceneName is a
// no-op false so the caller keeps the legacy walk-in path. Spam-safe: any call
// while busy, or re-entering the current world, returns false without side
// effects (matches the §24 stress list: spam gate / spam movement).
// Phase 3.0.x S3A: LastError carries the honest failure reason for the last
// op (null/empty = last op clean or never ran). No-op refusals (busy,
// re-enter, scene-less) are NOT errors — they leave LastError untouched — so
// a consumer can tell "nothing needed doing" apart from "loading truly
// failed". C# 9.0 only (no records, no target-typed new).
public sealed class WorldTransition {
  public SubjectId Current { get; private set; }
  public WorldTransitionState State { get; private set; }
  public string LastError { get; private set; }

  readonly SubjectId _main;

  public WorldTransition(SubjectId main) {
    _main = main;
    Current = main;
    State = WorldTransitionState.Idle;
  }

  public async Task<bool> EnterAsync(ISceneOps ops, SubjectId target, string sceneName) {
    if (ops == null || string.IsNullOrEmpty(sceneName)) return false;
    if (State != WorldTransitionState.Idle) return false;
    if (Current.Value == target.Value) return false;
    State = WorldTransitionState.Loading;
    LastError = null;
    try {
      await ops.LoadAdditiveAsync(sceneName).ConfigureAwait(false);
    } catch (Exception e) {
      State = WorldTransitionState.Idle;
      LastError = "load failed: " + e.Message;
      return false;
    }
    if (!ops.IsLoaded(sceneName)) {
      State = WorldTransitionState.Idle;
      LastError = "load unverified: " + sceneName + " not loaded after op";
      return false;
    }
    Current = target;
    State = WorldTransitionState.InSubject;
    return true;
  }

  public async Task<bool> ReturnAsync(ISceneOps ops, string sceneName) {
    if (ops == null || string.IsNullOrEmpty(sceneName)) return false;
    if (State != WorldTransitionState.InSubject) return false;
    State = WorldTransitionState.Unloading;
    LastError = null;
    try {
      await ops.UnloadAsync(sceneName).ConfigureAwait(false);
    } catch (Exception e) {
      State = WorldTransitionState.InSubject;
      LastError = "unload failed: " + e.Message;
      return false;
    }
    if (ops.IsLoaded(sceneName)) {
      State = WorldTransitionState.InSubject;
      LastError = "unload unverified: " + sceneName + " still loaded after op";
      return false;
    }
    Current = _main;
    State = WorldTransitionState.Idle;
    return true;
  }
}
