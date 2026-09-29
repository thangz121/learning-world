// A_World/DiscoveryGarden/DiscoveryArea.cs — S3-P2Z18 GAMEPLAY #7
// "VƯỜN KHÁM PHÁ" (Discovery Garden). The Discovery Garden is its OWN
// Micro-World: the Math Hub's discovery_garden gate (magnifier identity)
// opens the LAZY DiscoveryScene through the shared micro slot — never at
// boot, never stacked, no second loader. This scene-local MODULE (living in
// MathScene, which stays loaded underneath) owns the travel beats exactly
// like the garden/build/delivery/match areas: tunnel -> EnterMicro -> warp to
// the world's entry -> island bounds -> camera follow/reveal -> HUD
// objective, and the mirrored exit home.
// It also owns the activity's ActivityLifecycle + the round ladder (0 = core
// candidate set, 1 = full garden with extra distractors) so the state
// survives the arena unload (in-memory only) and a re-entry adopts the
// finished picture instead of replaying the lesson.
// C# 9.0 only.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class DiscoveryArea : MonoBehaviour, IMicroWorldArea {
  public const string AreaId = "discovery_garden";
  const float TunnelSeconds = 0.35f;

  public Vector3 HubReturnPos;    // Math-hub landing after exiting the garden
  public ActivityAnchors Anchors; // discovery scene registry (after load)

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
    get { return DialogueLang.T("Discovery Garden", "Vườn Khám Phá"); }
  }

  // The activity lifecycle lives HERE (MathScene) so it survives the arena's
  // lazy unload — re-entry adopts the finished picture instead of replaying.
  public ActivityLifecycle Lifecycle { get; private set; } =
    new ActivityLifecycle("discover_items", "DiscoveryArea");
  public DiscoveryGame Game { get; private set; }
  public void BindGame(DiscoveryGame game) { Game = game; }

  // ---- round ladder (ONE garden, two halves of the day) -----------------------
  // Round 0 (first visit): the core candidate set only — a short search.
  // Round 1: the full garden (extra distractors active) — a longer search.
  // In-memory only (save untouched, like every activity state). A diagnostic
  // run pins the round with "-discovery-round N" (same CLI pattern as #2-#6).
  public const int DefaultRound = 0;
  public const int MaxRound = 1;
  public const string RoundFlag = "-discovery-round";
  public static readonly int[] Progression = { 0, 1 };
  public int Round { get; private set; } = DefaultRound;
  int _lifeRound = DefaultRound;
  public int LastCompletedTasks { get; private set; }

  public static int NextRound(int r) {
    for (int i = 0; i < Progression.Length; i++) {
      if (Progression[i] == r) return Progression[(i + 1) % Progression.Length];
    }
    return DefaultRound;
  }

  public static int ParseRoundArg(string[] args, int fallback) {
    if (args == null) return fallback;
    for (int i = 0; i + 1 < args.Length; i++) {
      if (!string.Equals(args[i], RoundFlag, StringComparison.OrdinalIgnoreCase)) continue;
      int n;
      if (int.TryParse(args[i + 1], out n) && n >= 0 && n <= MaxRound) return n;
      return fallback;
    }
    return fallback;
  }

  void MaybeAdvanceRound() {
    if (Lifecycle == null) return;
    if (Lifecycle.State != ActivityState.Completed) return;
    if (_lifeRound != Round) return;
    int next = NextRound(Round);
    Round = next;
    Lifecycle = new ActivityLifecycle("discover_items", "DiscoveryArea");
    _lifeRound = next;
    try { Debug.Log("[DiscoveryArea] round advanced to " + next + ".", this); } catch (Exception) { }
  }

  // The game reports a finished exploration (all 3 tasks found).
  public void NotifyCompleted(int tasksFound) {
    LastCompletedTasks = tasksFound <= 0 ? 3 : tasksFound;
    try { Debug.Log("[DiscoveryArea] exploration completed (" + LastCompletedTasks
      + " tasks).", this); } catch (Exception) { }
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
      int cli = ParseRoundArg(Environment.GetCommandLineArgs(), DefaultRound);
      Round = cli;
      _lifeRound = cli;
    } catch (Exception) { }
  }

  // The router bounds must follow the ACTIVE island or the child cannot walk
  // inside the garden (same lesson as the other areas).
  public void BindRouter(ClickRouter router) { _router = router; }

  // Called by GameInstaller when the discovery scene finishes loading (lazy).
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

  void PushDiscoveryBounds() {
    PushIslandBounds(DiscoveryBuilder.WorldOffset,
      DiscoveryBuilder.BoundX, DiscoveryBuilder.BoundZ);
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
        loaded = await _transition.EnterMicroAsync(_sceneOps, DiscoveryBuilder.SceneName);
      } catch (Exception e) {
        try { Debug.LogWarning("[DiscoveryArea] micro load failed: " + e.Message, this); }
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
      PushDiscoveryBounds();
      if (_camera != null && _player != null)
        _camera.Follow(_player.transform, DiscoveryBuilder.FollowOffset);
      if (_camera != null && Anchors != null && Anchors.Camera != null && Anchors.CameraLook != null)
        _camera.FrameAnchor(Anchors.Camera, Anchors.CameraLook, 2.4f);
      ShowObjective(string.IsNullOrEmpty(_objective) ? WorldObjectiveText : _objective);
      try { Debug.Log("[DiscoveryArea] entered Discovery Garden (warp " + _worldEntry.ToString("F1") + ").", this); }
      catch (Exception) { }
    } catch (Exception e) {
      try { Debug.LogWarning("[DiscoveryArea] enter issue: " + e.Message, this); } catch (Exception) { }
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
      try { Debug.Log("[DiscoveryArea] exited (warp " + HubReturnPos.ToString("F1") + ").", this); }
      catch (Exception) { }
    } catch (Exception e) {
      try { Debug.LogWarning("[DiscoveryArea] exit issue: " + e.Message, this); } catch (Exception) { }
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

  public void SetRoundForTests(int r) {
    Round = Mathf.Clamp(r, 0, MaxRound);
    _lifeRound = Round;
  }

  public void TickProgressionForTests() { MaybeAdvanceRound(); }

  // ---- internals -----------------------------------------------------------------

  void Update() {
    if (_tunnelT > 0f) {
      _tunnelT -= Time.deltaTime;
      if (_tunnelT <= 0f) {
        try { if (_hud != null) _hud.StopTunnel(); } catch (Exception) { }
      }
    }
    if (!IsInside || IsBusy) return;
    MaybeAdvanceRound();
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
