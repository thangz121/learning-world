// A_World/MatchMeadow/MatchArea.cs — S3-P2Z17 GAMEPLAY #6 "GHÉP ĐÚNG CẶP".
// The Match Meadow is its OWN Micro-World: the Math Hub's match_meadow gate
// (two matching halves, aqua) opens the LAZY MatchMeadowScene through the
// shared micro slot — never at boot, never stacked, no second loader. This
// scene-local MODULE (living in MathScene, which stays loaded underneath) owns
// the travel beats exactly like the garden/build/delivery areas: tunnel ->
// EnterMicro -> warp to the world's entry -> island bounds -> camera
// follow/reveal -> HUD objective, and the mirrored exit home.
// It also owns the activity's ActivityLifecycle + the PAIR ladder (1 -> 2 -> 3
// pairs, one arena) so the state survives the arena unload (in-memory only)
// and a re-entry adopts the finished picture instead of replaying the lesson.
// C# 9.0 only.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class MatchArea : MonoBehaviour, IMicroWorldArea {
  public const string AreaId = "match_meadow";
  const float TunnelSeconds = 0.35f;

  public Vector3 HubReturnPos;    // Math-hub landing after exiting the meadow
  public ActivityAnchors Anchors; // meadow scene registry (after load)

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
    get { return DialogueLang.T("Match Meadow", "Đồng Cỏ Ghép Đôi"); }
  }

  // The activity lifecycle lives HERE (MathScene) so it survives the arena's
  // lazy unload — re-entry adopts the matched pairs instead of replaying.
  public ActivityLifecycle Lifecycle { get; private set; } =
    new ActivityLifecycle("match_pairs", "MatchArea");
  public MatchGame Game { get; private set; }
  public void BindGame(MatchGame game) { Game = game; }

  // ---- pair ladder (ONE meadow, 1..3 pairs) ------------------------------------
  // First visit teaches the reference 1 pair, then 2, then 3, then loops back.
  // In-memory only (save untouched, like every activity state).
  // A diagnostic run pins ANY count with "-match-pairs N" (same CLI pattern).
  public const int DefaultPairs = 1;
  public const string PairsFlag = "-match-pairs";
  public static readonly int[] Progression = { 1, 2, 3 };
  // S3-P2Z20 user: random number questions in live play (installer turns it on;
  // tests keep the deterministic ladder).
  public bool RandomPairs;
  public int Pairs { get; private set; } = DefaultPairs;
  int _lifePairs = DefaultPairs;
  public int LastCompletedPairs { get; private set; }

  public static int NextPairs(int p) {
    for (int i = 0; i < Progression.Length; i++) {
      if (Progression[i] == p) return Progression[(i + 1) % Progression.Length];
    }
    return DefaultPairs;
  }

  public static int ParsePairsArg(string[] args, int fallback) {
    if (args == null) return fallback;
    for (int i = 0; i + 1 < args.Length; i++) {
      if (!string.Equals(args[i], PairsFlag, StringComparison.OrdinalIgnoreCase)) continue;
      int n;
      if (int.TryParse(args[i + 1], out n) && n >= 1 && n <= MatchMeadowBuilder.MaxPairs)
        return n;
      return fallback;
    }
    return fallback;
  }

  // The ladder advances ONLY when the child has LEFT the meadow (same
  // discipline as #4/#5 and the garden): a completed round keeps its pair
  // count while the child is inside; the next entry stages a FRESH life.
  void MaybeAdvancePairs() {
    if (Lifecycle == null) return;
    if (Lifecycle.State != ActivityState.Completed) return;
    if (_lifePairs != Pairs) return;
    int next;
    if (RandomPairs) {
      next = Pairs;
      for (int guard = 0; guard < 32 && next == Pairs; guard++)
        next = UnityEngine.Random.Range(1, MatchMeadowBuilder.MaxPairs + 1);
    } else {
      next = NextPairs(Pairs);
    }
    Pairs = next;
    Lifecycle = new ActivityLifecycle("match_pairs", "MatchArea");
    _lifePairs = next;
    try { Debug.Log("[MatchArea] pairs advanced to " + next + ".", this); } catch (Exception) { }
  }

  public void NotifyCompleted(int pairs) {
    LastCompletedPairs = pairs <= 0 ? Pairs : pairs;
    try { Debug.Log("[MatchArea] round completed at " + LastCompletedPairs + " pairs.", this); }
    catch (Exception) { }
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
      int cli = ParsePairsArg(Environment.GetCommandLineArgs(), DefaultPairs);
      Pairs = cli;
      _lifePairs = cli;
    } catch (Exception) { }
  }

  // The router bounds must follow the ACTIVE island or the child cannot walk
  // inside the meadow (same lesson as the other micro-worlds).
  public void BindRouter(ClickRouter router) { _router = router; }

  // Called by GameInstaller when the meadow scene finishes loading (lazy).
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

  void PushMeadowBounds() {
    PushIslandBounds(MatchMeadowBuilder.WorldOffset,
      MatchMeadowBuilder.BoundX, MatchMeadowBuilder.BoundZ);
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
        loaded = await _transition.EnterMicroAsync(_sceneOps, MatchMeadowBuilder.SceneName);
      } catch (Exception e) {
        try { Debug.LogWarning("[MatchArea] micro load failed: " + e.Message, this); }
        catch (Exception) { }
      }
      if (!loaded) {
        try {
          UnityEngine.Debug.LogWarning("[MatchArea] micro load refused: "
            + (_transition != null ? _transition.LastError : "no transition")
            + " state=" + (_transition != null ? _transition.State.ToString() : "-"), this);
        } catch (Exception) { }
        RestoreObjective();
        IsBusy = false;
        StopTunnelSoon();
        return;
      }
      IsInside = true;
      if (_player != null) _player.WarpTo(_worldEntry);
      PushMeadowBounds();
      if (_camera != null && _player != null)
        _camera.Follow(_player.transform, MatchMeadowBuilder.FollowOffset);
      if (_camera != null && Anchors != null && Anchors.Camera != null && Anchors.CameraLook != null)
        _camera.FrameAnchor(Anchors.Camera, Anchors.CameraLook, 2.4f);
      ShowObjective(string.IsNullOrEmpty(_objective) ? WorldObjectiveText : _objective);
      try { Debug.Log("[MatchArea] entered Match Meadow (warp " + _worldEntry.ToString("F1") + ").", this); }
      catch (Exception) { }
    } catch (Exception e) {
      try { Debug.LogWarning("[MatchArea] enter issue: " + e.Message, this); } catch (Exception) { }
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
      MaybeAdvancePairs();
      try { Debug.Log("[MatchArea] exited (warp " + HubReturnPos.ToString("F1") + ").", this); }
      catch (Exception) { }
    } catch (Exception e) {
      try { Debug.LogWarning("[MatchArea] exit issue: " + e.Message, this); } catch (Exception) { }
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
    MaybeAdvancePairs();
    return true;
  }

  public void SetPairsForTests(int p) {
    Pairs = Mathf.Clamp(p, 1, MatchMeadowBuilder.MaxPairs);
    _lifePairs = Pairs;
  }

  public void TickProgressionForTests() { MaybeAdvancePairs(); }

  // ---- internals -----------------------------------------------------------------

  void Update() {
    if (_tunnelT > 0f) {
      _tunnelT -= Time.deltaTime;
      if (_tunnelT <= 0f) {
        try { if (_hud != null) _hud.StopTunnel(); } catch (Exception) { }
      }
    }
    // No ladder tick here on purpose: the pair count advances on LEAVE.
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
