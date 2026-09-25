// A_World/BuildYard/BuildTowerArea.cs — S3-P2Z14 GAMEPLAY #4 vertical slice
// "XÂY THÁP THEO SỐ" (build the tower by number). The Build Yard is its OWN
// Micro-World: the Math Hub's build_yard gate (crane + blocks) opens the LAZY
// BuildTowerScene through the shared micro slot — never at boot, never stacked,
// no second loader. This scene-local MODULE (living in MathScene, which stays
// loaded underneath) owns the travel beats exactly like CountingGardenArea:
// tunnel -> EnterMicro -> warp to the world's entry -> island bounds -> camera
// follow/reveal -> HUD objective, and the mirrored exit home.
// It also owns the activity's ActivityLifecycle + target ladder so the state
// survives the arena unload (in-memory only, save untouched) and a re-entry
// adopts the finished picture instead of replaying the lesson.
// C# 9.0 only.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class BuildTowerArea : MonoBehaviour {
  public const string AreaId = "build_yard";
  const float TunnelSeconds = 0.35f;

  public Vector3 HubReturnPos;    // Math-hub landing after exiting the yard
  public ActivityAnchors Anchors; // yard scene registry (after load)
  // The hub landmark beside the build gate: its mini tower height reflects the
  // last completed target (brief §25 progression reflection).
  public BuildYardLandmark Landmark;

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
    get { return DialogueLang.T("Build Yard", "Sân Xây Dựng"); }
  }

  // The activity lifecycle lives HERE (MathScene) so it survives the arena's
  // lazy unload — re-entry adopts the completed tower instead of replaying.
  public ActivityLifecycle Lifecycle { get; private set; } =
    new ActivityLifecycle("build_tower", "BuildTowerArea");
  public BuildTowerGame Game { get; private set; }
  public void BindGame(BuildTowerGame game) { Game = game; }

  // ---- target ladder (ONE arena, many targets: 1..9) --------------------------
  // The target only decides how many blocks the tower gets. First visit teaches
  // the reference 3, then the tower climbs 5 -> 7 -> 9, then a 1-block breather
  // loops back. In-memory only (save untouched, like every activity state).
  // A diagnostic run pins ANY target with "-build-target N" (same CLI pattern
  // as the stair/rabbit ladders), rejoining the ladder at 3 afterwards.
  public const int DefaultTarget = 3;
  public const string TargetFlag = "-build-target";
  public static readonly int[] Progression = { 3, 5, 7, 9, 1 };
  public int Target { get; private set; } = DefaultTarget;
  int _lifeTarget = DefaultTarget;
  // Last completed height pushed to the hub landmark (survives the scene swap
  // while MathScene lives; MathScene rebuild restores it from the installer).
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
      if (int.TryParse(args[i + 1], out n) && n >= 1 && n <= BuildTowerBuilder.MaxTarget)
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
    Lifecycle = new ActivityLifecycle("build_tower", "BuildTowerArea");
    _lifeTarget = next;
    try { Debug.Log("[BuildTowerArea] target advanced to " + next + ".", this); } catch (Exception) { }
  }

  // The game reports a finished tower (success or a corrected overshoot): the
  // hub landmark grows to the completed height.
  public void NotifyCompleted(int target) {
    LastCompletedTarget = target <= 0 ? Target : target;
    if (Landmark != null) Landmark.ShowHeight(LastCompletedTarget);
    try { Debug.Log("[BuildTowerArea] tower completed at " + LastCompletedTarget
      + " blocks (landmark updated).", this); } catch (Exception) { }
  }

  public void PushLandmarkState() {
    if (Landmark != null && LastCompletedTarget > 0) Landmark.ShowHeight(LastCompletedTarget);
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
  // inside the yard (same lesson as the garden/arenas).
  public void BindRouter(ClickRouter router) { _router = router; }

  // Called by GameInstaller when the yard scene finishes loading (lazy).
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

  void PushBuildBounds() {
    PushIslandBounds(BuildTowerBuilder.WorldOffset,
      BuildTowerBuilder.BoundX, BuildTowerBuilder.BoundZ);
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
        loaded = await _transition.EnterMicroAsync(_sceneOps, BuildTowerBuilder.SceneName);
      } catch (Exception e) {
        try { Debug.LogWarning("[BuildTowerArea] micro load failed: " + e.Message, this); }
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
      PushBuildBounds();
      if (_camera != null && _player != null)
        _camera.Follow(_player.transform, BuildTowerBuilder.FollowOffset);
      if (_camera != null && Anchors != null && Anchors.Camera != null && Anchors.CameraLook != null)
        _camera.FrameAnchor(Anchors.Camera, Anchors.CameraLook, 2.4f);
      ShowObjective(string.IsNullOrEmpty(_objective) ? WorldObjectiveText : _objective);
      try { Debug.Log("[BuildTowerArea] entered Build Yard (warp " + _worldEntry.ToString("F1") + ").", this); }
      catch (Exception) { }
    } catch (Exception e) {
      try { Debug.LogWarning("[BuildTowerArea] enter issue: " + e.Message, this); } catch (Exception) { }
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
      try { Debug.Log("[BuildTowerArea] exited (warp " + HubReturnPos.ToString("F1") + ").", this); }
      catch (Exception) { }
    } catch (Exception e) {
      try { Debug.LogWarning("[BuildTowerArea] exit issue: " + e.Message, this); } catch (Exception) { }
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
    Target = Mathf.Clamp(t, 1, BuildTowerBuilder.MaxTarget);
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
