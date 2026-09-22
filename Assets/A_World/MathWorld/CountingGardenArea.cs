// A_World/MathWorld/CountingGardenArea.cs — S2 PIONEER MICRO-WORLD (v2).
// The Counting Garden is its OWN scene (CountingGardenBuilder.SceneName),
// LAZY-loaded only when the child walks into the counting-garden gate in the
// Math Hub — never at boot. This scene-local MODULE owns the travel beats
// (tunnel, subject-scene stay-alive, warp into/out of the micro scene, camera
// frame/follow, HUD cache/restore) and drives EXISTING systems only:
// WorldTransition/ISceneOps (extended with the micro slot), ClickToMove.WarpTo,
// SmartCamera beats, MarketHUD tunnel. The garden scene's anchors are pushed
// in by GameInstaller after each lazy load (SetGarden).
// EditMode-safe: pure state seams; runtime beats are async void like Bootstrap.
// C# 9.0 only.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class CountingGardenArea : MonoBehaviour {
  public const string AreaId = "counting_garden";
  const float TunnelSeconds = 0.35f;

  public Vector3 HubReturnPos;   // Math-hub landing after exiting the garden
  public ActivityAnchors Anchors; // garden scene registry (after load)

  WorldTransition _transition;
  ISceneOps _sceneOps;
  ClickToMove _player;
  SmartCamera _camera;
  MarketHUD _hud;
  Vector3 _gardenEntry;
  float _tunnelT = -1f;
  string _preObjective;

  public bool IsInside { get; private set; }
  public bool IsBusy { get; private set; }
  public bool CanEnter { get { return !IsInside && !IsBusy; } }
  public bool CanExit { get { return IsInside && !IsBusy; } }
  public ClickToMove Player { get { return _player; } }
  public WorldTransition Transition { get { return _transition; } }

  public void Bind(WorldTransition transition, ISceneOps sceneOps, ClickToMove player,
      SmartCamera camera, MarketHUD hud, Vector3 hubReturn) {
    _transition = transition;
    _sceneOps = sceneOps;
    _player = player;
    _camera = camera;
    _hud = hud;
    HubReturnPos = hubReturn;
    IsInside = false;
    IsBusy = false;
  }

  // Called by GameInstaller every time the garden scene finishes loading
  // (lazy): pushes the scene-authored entry + anchor registry in.
  public void SetGarden(Vector3 entry, ActivityAnchors anchors) {
    _gardenEntry = entry;
    Anchors = anchors;
  }

  // ---- travel beats ------------------------------------------------------------

  public async void EnterFromHub() {
    if (!CanEnter) return;
    IsBusy = true;
    try {
      PlayTunnel();
      CacheObjective();
      bool loaded = false;
      try {
        loaded = await _transition.EnterMicroAsync(_sceneOps, CountingGardenBuilder.SceneName);
      } catch (Exception e) {
        try { Debug.LogWarning("[CountingGarden] micro load failed: " + e.Message, this); }
        catch (Exception) { }
      }
      if (!loaded) {
        // Honest failure: stay in the hub, restore the HUD, no fake progress.
        RestoreObjective();
        IsBusy = false;
        StopTunnelSoon();
        return;
      }
      if (_player != null) _player.WarpTo(_gardenEntry);
      if (_camera != null && Anchors != null && Anchors.Camera != null && Anchors.CameraLook != null)
        _camera.FrameAnchor(Anchors.Camera, Anchors.CameraLook, 2.4f);
      ShowObjective("Vườn Đếm");
      IsInside = true;
      try { Debug.Log("[CountingGarden] entered scene (warp " + _gardenEntry.ToString("F1") + ").", this); }
      catch (Exception) { }
    } catch (Exception e) {
      try { Debug.LogWarning("[CountingGarden] enter issue: " + e.Message, this); } catch (Exception) { }
    }
    IsBusy = false;
    StopTunnelSoon();
  }

  public async void ExitToHub() {
    if (!CanExit) return;
    IsBusy = true;
    try {
      PlayTunnel();
      if (_player != null) _player.WarpTo(HubReturnPos);
      if (_camera != null && _player != null)
        _camera.Follow(_player.transform, MathWorldBuilder.FollowOffset);
      RestoreObjective();
      try { await _transition.ExitMicroAsync(_sceneOps); } catch (Exception) { }
      IsInside = false;
      try { Debug.Log("[CountingGarden] exited scene (warp " + HubReturnPos.ToString("F1") + ").", this); }
      catch (Exception) { }
    } catch (Exception e) {
      try { Debug.LogWarning("[CountingGarden] exit issue: " + e.Message, this); } catch (Exception) { }
    }
    IsBusy = false;
    StopTunnelSoon();
  }

  // ---- pure state seams (EditMode cover without live refs) ----------------------

  public bool TryEnterForTests() {
    if (!CanEnter) return false;
    IsInside = true;
    return true;
  }

  public bool TryExitForTests() {
    if (!CanExit) return false;
    IsInside = false;
    return true;
  }

  // ---- internals -----------------------------------------------------------------

  void Update() {
    if (_tunnelT <= 0f) return;
    _tunnelT -= Time.deltaTime;
    if (_tunnelT <= 0f) {
      try { if (_hud != null) _hud.StopTunnel(); } catch (Exception) { }
    }
  }

  void StopTunnelSoon() { _tunnelT = TunnelSeconds; }
  void PlayTunnel() { try { if (_hud != null) _hud.PlayTunnel(); } catch (Exception) { } }

  void CacheObjective() {
    try { if (_hud != null) _preObjective = _hud.CurrentObjective; } catch (Exception) { }
  }

  void RestoreObjective() {
    try {
      if (_hud == null) return;
      if (!string.IsNullOrEmpty(_preObjective)) _hud.ShowObjective(_preObjective);
      else _hud.ShowObjective("Math World");
    } catch (Exception) { }
  }

  void ShowObjective(string text) {
    try { if (_hud != null) _hud.ShowObjective(text); } catch (Exception) { }
  }
}
