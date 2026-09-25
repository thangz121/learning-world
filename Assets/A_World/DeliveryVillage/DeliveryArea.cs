// A_World/DeliveryVillage/DeliveryArea.cs — S3-P2Z15 GAMEPLAY #5 vertical
// slice "GIAO HÀNG ĐÚNG SỐ" (deliver the apples). The Delivery Village is its
// OWN Micro-World: the Math Hub's delivery_village gate (cottage + parcels)
// opens the LAZY DeliveryScene through the shared micro slot — never at boot,
// never stacked, no second loader. This scene-local MODULE (living in
// MathScene, which stays loaded underneath) owns the travel beats exactly like
// the garden/build areas: tunnel -> EnterMicro -> warp to the world's entry ->
// island bounds -> camera follow/reveal -> HUD objective, and the mirrored
// exit home. It also owns the activity's ActivityLifecycle + target ladder so
// the state survives the arena unload (in-memory only, save untouched) and a
// re-entry adopts the finished picture instead of replaying the lesson.
// C# 9.0 only.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class DeliveryArea : MonoBehaviour, IMicroWorldArea {
  public const string AreaId = "delivery_village";
  const float TunnelSeconds = 0.35f;

  public Vector3 HubReturnPos;    // Math-hub landing after exiting the village
  public ActivityAnchors Anchors; // village scene registry (after load)

  WorldTransition _transition;
  ISceneOps _sceneOps;
  ClickToMove _player;
  SmartCamera _camera;
  MarketHUD _hud;
  ClickRouter _router;
  Vector3 _worldEntry;
  string _objective;
  float _tunnelT = -1f;
  string _preObjective;

  public bool IsInside { get; private set; }
  public bool IsBusy { get; private set; }
  public bool CanEnter { get { return !IsInside && !IsBusy; } }
  public bool CanExit { get { return IsInside && !IsBusy; } }
  public ClickToMove Player { get { return _player; } }
  public WorldTransition Transition { get { return _transition; } }
  public static string WorldObjectiveText {
    get { return DialogueLang.T("Delivery Village", "Làng Giao Hàng"); }
  }

  // The activity lifecycle lives HERE (MathScene) so it survives the arena's
  // lazy unload — re-entry adopts the delivered crate instead of replaying.
  public ActivityLifecycle Lifecycle { get; private set; } =
    new ActivityLifecycle("deliver_apples", "DeliveryArea");
  public DeliveryGame Game { get; private set; }
  public void BindGame(DeliveryGame game) { Game = game; }

  // ---- target ladder (ONE village, many orders: 1..9) -------------------------
  // The target only decides how many apples the order needs. First visit
  // teaches the brief's reference 4, then the order climbs 5 -> 7 -> 9, then a
  // 1-3 breather loops back. In-memory only (save untouched).
  // A diagnostic run pins ANY target with "-deliver-target N" (same CLI pattern
  // as the stair/rabbit/build ladders), rejoining the ladder at 4 afterwards.
  public const int DefaultTarget = 4;
  public const string TargetFlag = "-deliver-target";
  public static readonly int[] Progression = { 4, 5, 7, 9, 1, 3 };
  public int Target { get; private set; } = DefaultTarget;
  int _lifeTarget = DefaultTarget;
  // Last completed order (report/tests only; the hub gate needs no landmark).
  public int LastCompletedTarget { get; private set; }

  public static int NextTarget(int t) {
    for (int i = 0; i < Progression.Length; i++) {
      if (Progression[i] == t) return Progression[(i + 1) % Progression.Length];
    }
    return DefaultTarget;
  }

  public static int ParseTargetArg(string[] args, int fallback) {
    if (args == null) return fallback;
    for (int i = 0; i + 1 < args.Length; i++) {
      if (!string.Equals(args[i], TargetFlag, StringComparison.OrdinalIgnoreCase)) continue;
      int n;
      if (int.TryParse(args[i + 1], out n) && n >= 1 && n <= DeliveryBuilder.MaxTarget)
        return n;
      return fallback;
    }
    return fallback;
  }

  void MaybeAdvanceTarget() {
    if (Lifecycle == null) return;
    if (Lifecycle.State != ActivityState.Completed) return;
    if (_lifeTarget != Target) return;
    int next = NextTarget(Target);
    Target = next;
    Lifecycle = new ActivityLifecycle("deliver_apples", "DeliveryArea");
    _lifeTarget = next;
    try { Debug.Log("[DeliveryArea] target advanced to " + next + ".", this); } catch (Exception) { }
  }

  // The game reports a finished order (success or a corrected overshoot).
  public void NotifyCompleted(int target) {
    LastCompletedTarget = target <= 0 ? Target : target;
    try { Debug.Log("[DeliveryArea] order completed at " + LastCompletedTarget
      + " apples.", this); } catch (Exception) { }
  }

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
    try {
      int cli = ParseTargetArg(Environment.GetCommandLineArgs(), DefaultTarget);
      Target = cli;
      _lifeTarget = cli;
    } catch (Exception) { }
  }

  // The router bounds must follow the ACTIVE island or the child cannot walk
  // inside the village (same lesson as the garden/build arenas).
  public void BindRouter(ClickRouter router) { _router = router; }

  // Called by GameInstaller when the village scene finishes loading (lazy).
  public void SetWorld(Vector3 entry, ActivityAnchors anchors, string objective) {
    _worldEntry = entry;
    Anchors = anchors;
    _objective = objective;
  }

  void PushIslandBounds(Vector3 center, float x, float z) {
    if (_router == null) return;
    try {
      _router.boundCenter = center;
      _router.boundX = x;
      _router.boundZ = z;
    } catch (Exception) { }
  }

  void PushVillageBounds() {
    PushIslandBounds(DeliveryBuilder.WorldOffset,
      DeliveryBuilder.BoundX, DeliveryBuilder.BoundZ);
  }

  void PushMathBounds() {
    PushIslandBounds(MathWorldBuilder.WorldOffset,
      MathWorldBuilder.BoundX, MathWorldBuilder.BoundZ);
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
        loaded = await _transition.EnterMicroAsync(_sceneOps, DeliveryBuilder.SceneName);
      } catch (Exception e) {
        try { Debug.LogWarning("[DeliveryArea] micro load failed: " + e.Message, this); }
        catch (Exception) { }
      }
      if (!loaded) {
        // Honest failure: stay in the hub, restore the HUD, no fake progress.
        RestoreObjective();
        IsBusy = false;
        StopTunnelSoon();
        return;
      }
      IsInside = true;
      if (_player != null) _player.WarpTo(_worldEntry);
      PushVillageBounds();
      if (_camera != null && _player != null)
        _camera.Follow(_player.transform, DeliveryBuilder.FollowOffset);
      if (_camera != null && Anchors != null && Anchors.Camera != null && Anchors.CameraLook != null)
        _camera.FrameAnchor(Anchors.Camera, Anchors.CameraLook, 2.4f);
      ShowObjective(string.IsNullOrEmpty(_objective) ? WorldObjectiveText : _objective);
      try { Debug.Log("[DeliveryArea] entered Delivery Village (warp " + _worldEntry.ToString("F1") + ").", this); }
      catch (Exception) { }
    } catch (Exception e) {
      try { Debug.LogWarning("[DeliveryArea] enter issue: " + e.Message, this); } catch (Exception) { }
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
      PushMathBounds();
      if (_camera != null && _player != null)
        _camera.Follow(_player.transform, MathWorldBuilder.FollowOffset);
      RestoreObjective();
      try { await _transition.ExitMicroAsync(_sceneOps); } catch (Exception) { }
      IsInside = false;
      try { Debug.Log("[DeliveryArea] exited (warp " + HubReturnPos.ToString("F1") + ").", this); }
      catch (Exception) { }
    } catch (Exception e) {
      try { Debug.LogWarning("[DeliveryArea] exit issue: " + e.Message, this); } catch (Exception) { }
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

  public void SetTargetForTests(int t) {
    Target = Mathf.Clamp(t, 1, DeliveryBuilder.MaxTarget);
    _lifeTarget = Target;
  }

  public void TickProgressionForTests() { MaybeAdvanceTarget(); }

  // ---- internals -----------------------------------------------------------------

  void Update() {
    if (_tunnelT > 0f) {
      _tunnelT -= Time.deltaTime;
      if (_tunnelT <= 0f) {
        try { if (_hud != null) _hud.StopTunnel(); } catch (Exception) { }
      }
    }
    if (!IsInside || IsBusy) return;
    MaybeAdvanceTarget();
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
      else _hud.ShowObjective(DialogueLang.T("Math World", "Thế giới Toán"));
    } catch (Exception) { }
  }

  void ShowObjective(string text) {
    try { if (_hud != null) _hud.ShowObjective(text); } catch (Exception) { }
  }
}
