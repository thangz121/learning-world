// A_World/MathWorld/CountingGardenArea.cs — S2 PIONEER MICRO-WORLD (v2),
// extended S3 P2X with the ZONE PICKER + PLAY ARENA (user order §47B).
// The Counting Garden is its OWN scene (CountingGardenBuilder.SceneName),
// LAZY-loaded only when the child walks into the counting-garden gate in the
// Math Hub — never at boot. This scene-local MODULE (living in MathScene, which
// stays loaded underneath) owns the travel beats (tunnel, subject-scene
// stay-alive, warp into/out of the micro scene, camera frame/follow, HUD
// cache/restore) AND the zone flow on top:
//   focus (click a GardenZoneSpot or walk <=2m) -> panel + camera + HUD name
//   -> double-click outside the panel cancels
//   -> "Vào chơi" swaps the micro slot to the zone's play scene
//      (garden unload -> CountingPlayScene load, EnterMicro/ExitMicro pair;
//       failure reloads the garden so the child is never in the void)
//   -> the play scene's own exit portal swaps back to the garden.
// Drives EXISTING systems only: WorldTransition/ISceneOps (micro slot),
// ClickToMove.WarpTo, SmartCamera beats, MarketHUD tunnel/objective. Anchor +
// spot registries are pushed in by GameInstaller after each lazy load.
// EditMode-safe: pure state seams; runtime beats are async void like Bootstrap.
// C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class CountingGardenArea : MonoBehaviour {
  public const string AreaId = "counting_garden";
  const float TunnelSeconds = 0.35f;
  // Focus beat: re-issued every 4.5s (the camera's own FrameAnchor countdown
  // is 6s) so the framing HOLDS while the child studies the zone.
  const float FocusRefreshSeconds = 4.5f;
  // The user-chosen cancel gesture: two world clicks outside the panel within
  // this window. Chosen over a single click so normal walking never cancels.
  public const float DoubleClickSeconds = 0.4f;
  // Proximity focus: walk this close to a plot -> same beat as clicking it.
  // Re-arms only after walking clear (no focus ping-pong on the spot edge).
  public const float ProximityRearmMargin = 0.6f;

  public Vector3 HubReturnPos;    // Math-hub landing after exiting the garden
  public ActivityAnchors Anchors; // garden scene registry (after load)

  WorldTransition _transition;
  ISceneOps _sceneOps;
  ClickToMove _player;
  SmartCamera _camera;
  MarketHUD _hud;
  ClickRouter _router;
  GardenZonePanel _panel;
  Vector3 _gardenEntry;
  float _tunnelT = -1f;
  string _preObjective;

  readonly List<GardenZoneSpot> _spots = new List<GardenZoneSpot>();
  int _playedZone = 2;        // remembered across the play round-trip
  Vector3 _playEntry;
  ActivityAnchors _playAnchors;
  // S3-P2Z12: each play scene pushes its own island bounds / follow framing /
  // objective when it loads (the area owns ONE micro slot, so one live set of
  // values at a time — the garden reload restores its own).
  Vector3 _playCenter;
  float _playBoundX = CountingPlayBuilder.BoundX;
  float _playBoundZ = CountingPlayBuilder.BoundZ;
  Vector3 _playFollow = CountingPlayBuilder.FollowOffset;
  string _playObjective;
  bool _proximityArmed = true;
  float _focusRefreshT;
  float _doubleClickT = -1f;
  // S3-P2Y (user order): the "Vào chơi / Quay lại" panel only appears AFTER the
  // child watched one full try-run of the zone's demo, so the choice is
  // informed. Each staged plot owns its own garden demo (zone 2 = ball lesson,
  // zone 5 = number stairs) — the area routes by focused zone (S3-P2Z12b).
  readonly Dictionary<int, IGardenZoneDemo> _demos = new Dictionary<int, IGardenZoneDemo>();
  IGardenZoneDemo _demo; // the FOCUSED zone's demo (resolved in FocusZone)
  int _demoLoopBase;
  bool _awaitDemo;

  public bool IsInside { get; private set; }
  public bool IsBusy { get; private set; }
  public bool IsInPlay { get; private set; }
  public int FocusedZone { get; private set; } = -1;
  public bool IsFocused { get { return FocusedZone >= 0; } }
  public bool CanEnter { get { return !IsInside && !IsBusy; } }
  public bool CanExit { get { return IsInside && !IsBusy && !IsInPlay; } }
  public ClickToMove Player { get { return _player; } }
  public WorldTransition Transition { get { return _transition; } }
  public int SpotCount { get { return _spots.Count; } }
  public static string GardenObjectiveText {
    get { return DialogueLang.T("Counting Garden", "Vườn Đếm"); }
  }
  public static string PlayObjectiveText {
    get { return DialogueLang.T("Counting Playground", "Sân chơi đếm"); }
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
    IsInPlay = false;
    FocusedZone = -1;
    // Diagnostic override for the current stair target ("-stair-target N",
    // same CLI pattern as DialogueLang's "-lang vi"); inert otherwise.
    try {
      int cli = ParseStairTargetArg(Environment.GetCommandLineArgs(), StairDefaultTarget);
      StairTarget = cli;
      _stairLifeTarget = cli;
    } catch (Exception) { }
    // Diagnostic override for the current rabbit target ("-rabbit-target N",
    // same CLI pattern as the stair ladder); inert otherwise.
    try {
      int cliRabbit = ParseRabbitTargetArg(Environment.GetCommandLineArgs(), RabbitDefaultTarget);
      RabbitTarget = cliRabbit;
      _rabbitLifeTarget = cliRabbit;
    } catch (Exception) { }
  }

  // S3-P2V journey bug: the router bounds must follow the ACTIVE island or the
  // child cannot walk inside the garden (clicks outside Math's 60±27 were
  // silently dropped). Same push/restore pattern MarketBootstrap uses for
  // Main<->Math; the garden owner owns its own pair.
  public void BindRouter(ClickRouter router) { _router = router; }

  // Zone panel (persistent UI built by MarketBootstrap; survives scene swaps).
  // TWO-WAY bind (journey root cause: the panel's Play button was dead because
  // this only stored the panel here and never handed the area to the panel).
  public void BindPanel(GardenZonePanel panel) {
    _panel = panel;
    if (panel != null) panel.Bind(this);
  }

  // Garden ambient demos (miniatures): drive the "panel after the try-run" gate.
  // Legacy single-arg form = the demo theatre's ball lesson (zone 2), kept for
  // the existing call sites/tests; the two-arg form routes any zone's demo.
  public void BindDemo(CountingDemo demo) { BindDemo(2, demo); }
  public void BindDemo(int zoneIndex, IGardenZoneDemo demo) {
    if (demo == null) _demos.Remove(zoneIndex);
    else _demos[zoneIndex] = demo;
  }
  public IGardenZoneDemo DemoFor(int zoneIndex) {
    IGardenZoneDemo demo;
    return _demos.TryGetValue(zoneIndex, out demo) ? demo : null;
  }
  public bool AwaitingDemo { get { return _awaitDemo; } }

  // S3-P2Z4 reference gameplay: the activity lifecycle lives HERE (MathScene)
  // so it survives the arena's lazy unload — re-entry adopts the completed
  // visual instead of replaying the intro. In-memory only (save untouched).
  public ActivityLifecycle GameLifecycle { get; private set; } =
    new ActivityLifecycle("counting_game", "CountingGardenArea");
  public CountingGame Game { get; private set; }
  public void BindGame(CountingGame game) { Game = game; }

  // S3-P2Z12 gameplay #2 ("Bậc thang con số"): the same lifecycle pattern for
  // the stair hill — owned here, survives the lazy unload of StairPlayScene.
  public ActivityLifecycle StairLifecycle { get; private set; } =
    new ActivityLifecycle("number_stairs", "CountingGardenArea");
  public NumberStairs StairGame { get; private set; }
  public void BindStairGame(NumberStairs game) { StairGame = game; }

  // Target ladder (brief §34/§35): ONE staircase, many targets — the target
  // only decides which step the child must stop on. First visit teaches the
  // reference 3, then difficulty climbs 5 -> 7 -> 9, then a 1-step breather
  // loops back. In-memory only (save untouched, like every activity state).
  // A diagnostic run pins ANY target with "-stair-target N" (same CLI pattern
  // as DialogueLang's "-lang vi"), rejoining the ladder at 3 afterwards.
  public const int StairDefaultTarget = 3;
  public const string StairTargetFlag = "-stair-target";
  public static readonly int[] StairProgression = { 3, 5, 7, 9, 1 };
  public int StairTarget { get; private set; } = StairDefaultTarget;
  int _stairLifeTarget = StairDefaultTarget;

  // Pure helper (EditMode-coverable): the next ladder rung after t, or 3 when
  // t is off-ladder (a diagnostic target rejoins the ladder).
  public static int NextStairTarget(int t) {
    for (int i = 0; i < StairProgression.Length; i++) {
      if (StairProgression[i] == t) return StairProgression[(i + 1) % StairProgression.Length];
    }
    return StairDefaultTarget;
  }

  // Pure helper: read "-stair-target N" (1..9) from a command line, else the
  // current target (the flag is optional and inert without a valid number).
  public static int ParseStairTargetArg(string[] args, int fallback) {
    if (args == null) return fallback;
    for (int i = 0; i + 1 < args.Length; i++) {
      if (!string.Equals(args[i], StairTargetFlag, StringComparison.OrdinalIgnoreCase)) continue;
      int n;
      if (int.TryParse(args[i + 1], out n) && n >= 1 && n <= StairHillBuilder.StepCount)
        return n;
      return fallback;
    }
    return fallback;
  }

  // When the child completes a target and returns to the garden, the NEXT
  // visit teaches the next rung with a FRESH lifecycle (latch-free: it fires
  // exactly when a Completed life still belongs to the current target).
  // Re-entering mid-lesson (not Completed) replays the same target's lesson.
  void MaybeAdvanceStairTarget() {
    if (StairLifecycle == null) return;
    if (StairLifecycle.State != ActivityState.Completed) return;
    if (_stairLifeTarget != StairTarget) return;
    int next = NextStairTarget(StairTarget);
    StairTarget = next;
    StairLifecycle = new ActivityLifecycle("number_stairs", "CountingGardenArea");
    _stairLifeTarget = next;
    try { Debug.Log("[CountingGarden] stair target advanced to " + next + ".", this); }
    catch (Exception) { }
  }

  // Test seams (no live refs needed).
  public void SetStairTargetForTests(int t) {
    StairTarget = StairHillBuilder.ClampTarget(t);
    _stairLifeTarget = StairTarget;
  }
  public void TickStairProgressionForTests() { MaybeAdvanceStairTarget(); }

  // Gameplay #3 ("Cho thỏ ăn đúng số"): the same lifecycle pattern for the
  // rabbit patch — owned here, survives the lazy unload of RabbitPlayScene.
  public ActivityLifecycle RabbitLifecycle { get; private set; } =
    new ActivityLifecycle("rabbit_feed", "CountingGardenArea");
  public RabbitFeed RabbitGame { get; private set; }
  public void BindRabbitGame(RabbitFeed game) { RabbitGame = game; }

  // Target ladder: ONE patch, many targets — the target only decides how many
  // carrots the bunny gets. First visit teaches the reference 3, then the
  // count climbs 4 -> 5 -> 6 -> 7 -> 8 -> 9, then a 1-2 breather loops back.
  // In-memory only (save untouched, like every activity state).
  // A diagnostic run pins ANY target with "-rabbit-target N" (same CLI pattern
  // as the stair ladder), rejoining the ladder at 3 afterwards.
  public const int RabbitDefaultTarget = 3;
  public const string RabbitTargetFlag = "-rabbit-target";
  public static readonly int[] RabbitProgression = { 3, 4, 5, 6, 7, 8, 9, 1, 2 };
  public int RabbitTarget { get; private set; } = RabbitDefaultTarget;
  int _rabbitLifeTarget = RabbitDefaultTarget;

  // Pure helper (EditMode-coverable): the next ladder rung after t, or 3 when
  // t is off-ladder (a diagnostic target rejoins the ladder).
  public static int NextRabbitTarget(int t) {
    for (int i = 0; i < RabbitProgression.Length; i++) {
      if (RabbitProgression[i] == t) return RabbitProgression[(i + 1) % RabbitProgression.Length];
    }
    return RabbitDefaultTarget;
  }

  // Pure helper: read "-rabbit-target N" (1..9) from a command line, else the
  // current target (the flag is optional and inert without a valid number).
  public static int ParseRabbitTargetArg(string[] args, int fallback) {
    if (args == null) return fallback;
    for (int i = 0; i + 1 < args.Length; i++) {
      if (!string.Equals(args[i], RabbitTargetFlag, StringComparison.OrdinalIgnoreCase)) continue;
      int n;
      if (int.TryParse(args[i + 1], out n) && n >= 1 && n <= RabbitPlayBuilder.MaxTarget)
        return n;
      return fallback;
    }
    return fallback;
  }

  // When the child completes a target and returns to the garden, the NEXT
  // visit teaches the next rung with a FRESH lifecycle (latch-free: it fires
  // exactly when a Completed life still belongs to the current target).
  // Re-entering mid-lesson (not Completed) replays the same target's lesson.
  void MaybeAdvanceRabbitTarget() {
    if (RabbitLifecycle == null) return;
    if (RabbitLifecycle.State != ActivityState.Completed) return;
    if (_rabbitLifeTarget != RabbitTarget) return;
    int next = NextRabbitTarget(RabbitTarget);
    RabbitTarget = next;
    RabbitLifecycle = new ActivityLifecycle("rabbit_feed", "CountingGardenArea");
    _rabbitLifeTarget = next;
    try { Debug.Log("[CountingGarden] rabbit target advanced to " + next + ".", this); }
    catch (Exception) { }
  }

  // Test seams (no live refs needed).
  public void SetRabbitTargetForTests(int t) {
    RabbitTarget = RabbitPlayBuilder.ClampTarget(t);
    _rabbitLifeTarget = RabbitTarget;
  }
  public void TickRabbitProgressionForTests() { MaybeAdvanceRabbitTarget(); }

  void PushIslandBounds(Vector3 center, float x, float z) {
    if (_router == null) return;
    try {
      _router.boundCenter = center;
      _router.boundX = x;
      _router.boundZ = z;
    } catch (Exception) { }
  }

  void PushGardenBounds() {
    PushIslandBounds(CountingGardenBuilder.WorldOffset,
      CountingGardenBuilder.BoundX, CountingGardenBuilder.BoundZ);
  }

  void PushMathBounds() {
    PushIslandBounds(MathWorldBuilder.WorldOffset,
      MathWorldBuilder.BoundX, MathWorldBuilder.BoundZ);
  }

  void PushPlayBounds() {
    PushIslandBounds(_playCenter, _playBoundX, _playBoundZ);
  }

  // Called by GameInstaller every time the garden scene finishes loading
  // (lazy): pushes the scene-authored entry + anchor + zone-spot registry in.
  public void SetGarden(Vector3 entry, ActivityAnchors anchors, List<GardenZoneSpot> spots) {
    _gardenEntry = entry;
    Anchors = anchors;
    _spots.Clear();
    if (spots != null) {
      foreach (GardenZoneSpot spot in spots) {
        if (spot == null) continue;
        spot.Bind(this);
        _spots.Add(spot);
      }
    }
    // A fresh scene means the old focus refs are gone; start clean (the
    // installer re-binds the new ambient demo right after this call).
    FocusedZone = -1;
    _proximityArmed = true;
    _awaitDemo = false;
    _demo = null;
    _demos.Clear();
    if (_panel != null) _panel.Hide();
  }

  // Called by GameInstaller when a play scene finishes loading (lazy). Each
  // arena pushes its island + framing + objective so the area never hardcodes
  // one scene's coordinates (gameplay #2 is a second lazy scene).
  public void SetPlay(Vector3 entry, ActivityAnchors anchors, Vector3 center,
      float boundX, float boundZ, Vector3 followOffset, string objective = null) {
    _playEntry = entry;
    _playAnchors = anchors;
    _playCenter = center;
    _playBoundX = boundX;
    _playBoundZ = boundZ;
    _playFollow = followOffset;
    _playObjective = objective;
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
      IsInside = true;
      IsInPlay = false;
      if (_player != null) _player.WarpTo(_gardenEntry);
      PushGardenBounds();
      // Garden follow first (so the arrival beat returns to THIS world's
      // framing, not Math's map-height offset), then the reveal beat.
      if (_camera != null && _player != null)
        _camera.Follow(_player.transform, CountingGardenBuilder.FollowOffset);
      if (_camera != null && Anchors != null && Anchors.Camera != null && Anchors.CameraLook != null)
        _camera.FrameAnchor(Anchors.Camera, Anchors.CameraLook, 2.4f);
      ShowObjective(GardenObjectiveText);
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
    ClearFocus(false);
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
      IsInPlay = false;
      try { Debug.Log("[CountingGarden] exited scene (warp " + HubReturnPos.ToString("F1") + ").", this); }
      catch (Exception) { }
    } catch (Exception e) {
      try { Debug.LogWarning("[CountingGarden] exit issue: " + e.Message, this); } catch (Exception) { }
    }
    IsBusy = false;
    StopTunnelSoon();
  }

  // ---- zone focus (S3 P2X) ------------------------------------------------------

  public GardenZoneSpot FindSpot(int index) {
    for (int i = 0; i < _spots.Count; i++) {
      GardenZoneSpot s = _spots[i];
      if (s != null && s.zoneIndex == index) return s;
    }
    return null;
  }

  public void FocusZone(int index) {
    if (!IsInside || IsInPlay) return;
    GardenZoneSpot spot = FindSpot(index);
    if (spot == null) return;
    FocusedZone = index;
    _proximityArmed = false; // re-arm only after walking clear of the plots
    _focusRefreshT = 0f;
    _doubleClickT = -1f;
    FrameFocus(spot);
    // Staged zone: run the demo FIRST (the child watches the lesson), then the
    // panel offers play. Skeleton zones: focus + name, panel offers the way back.
    // S3-P2Z12b (user report "NPC dạy trẻ chơi ở đâu?"): EVERY staged plot owns
    // a garden miniature — the ball theatre (zone 2) and the stair hill
    // (zone 5) both show their two-NPC lesson before the play door opens.
    _demo = DemoFor(index);
    if (spot.playEnabled && spot.demoGate && _demo != null && !_demo.PassDone) {
      // S3-P2Z8: the focus ITSELF starts the lesson (a click from across the
      // yard used to just frame the camera and do nothing). The panel opens
      // once this pass completes (user order).
      _awaitDemo = true;
      _demoLoopBase = _demo.LoopCount;
      _demo.StartFocusedLesson();
      ShowObjective(DialogueLang.T("Watch!", "Xem nhé!"));
      if (_panel != null) _panel.Hide();
    } else if (spot.playEnabled && spot.demoGate && _demo != null && _demo.PassDone) {
      // They already watched this visit: no forced replay — offer play at once.
      _awaitDemo = false;
      ShowObjective(spot.ZoneName);
      if (_panel != null) _panel.ShowFor(spot.ZoneName, true);
    } else {
      _awaitDemo = false;
      ShowObjective(spot.ZoneName);
      if (_panel != null) _panel.ShowFor(spot.ZoneName, spot.playEnabled);
    }
    try { Debug.Log("[CountingGarden] focus zone " + index + " (" + spot.ZoneName + ")", this); }
    catch (Exception) { }
  }

  public void CancelFocus() { ClearFocus(true); }

  void ClearFocus(bool restoreCamera) {
    if (_panel != null) _panel.Hide();
    FocusedZone = -1;
    _doubleClickT = -1f;
    _awaitDemo = false;
    _proximityArmed = false; // standing on the spot must not instantly re-focus
    // The zone owns the focused lesson: releasing the focus stops it (voice cut
    // + stage back to the playground state).
    if (_demo != null) { try { _demo.StopFocusedLesson(); } catch (Exception) { } }
    if (restoreCamera) { FollowGarden(); ShowObjective(GardenObjectiveText); }
  }

  void FrameFocus(GardenZoneSpot spot) {
    if (_camera == null || spot == null || spot.CameraAnchor == null || spot.LookAnchor == null) return;
    try { _camera.FrameAnchor(spot.CameraAnchor, spot.LookAnchor, FocusRefreshSeconds + 1.5f); }
    catch (Exception) { }
  }

  void FollowGarden() {
    if (_camera == null || _player == null) return;
    try { _camera.Follow(_player.transform, CountingGardenBuilder.FollowOffset); }
    catch (Exception) { }
  }

  bool AnySpotWithin(Vector3 p, float radius) {
    for (int i = 0; i < _spots.Count; i++) {
      GardenZoneSpot s = _spots[i];
      if (s == null) continue;
      Vector3 q = s.transform.position;
      float dx = p.x - q.x, dz = p.z - q.z;
      if (dx * dx + dz * dz <= radius * radius) return true;
    }
    return false;
  }

  GardenZoneSpot NearestSpotWithin(Vector3 p, float radius) {
    GardenZoneSpot best = null;
    float bestD2 = radius * radius;
    for (int i = 0; i < _spots.Count; i++) {
      GardenZoneSpot s = _spots[i];
      if (s == null) continue;
      Vector3 q = s.transform.position;
      float dx = p.x - q.x, dz = p.z - q.z;
      float d2 = dx * dx + dz * dz;
      if (d2 <= bestD2) { bestD2 = d2; best = s; }
    }
    return best;
  }

  // ---- play arena travel (S3 P2X) ----------------------------------------------

  public void EnterPlay() {
    if (!IsInside || IsInPlay || IsBusy) {
      try { Debug.Log("[CountingGarden] play refused (inside=" + IsInside + " inPlay=" + IsInPlay + " busy=" + IsBusy + ").", this); }
      catch (Exception) { }
      return;
    }
    GardenZoneSpot spot = FindSpot(FocusedZone);
    if (spot == null || !spot.playEnabled) {
      try { Debug.Log("[CountingGarden] play refused (zone=" + FocusedZone + " spot=" + (spot != null) + ").", this); }
      catch (Exception) { }
      return;
    }
    _playedZone = spot.zoneIndex;
    EnterPlayAsync(PlaySceneFor(spot));
  }

  // Which lazy scene this plot's door opens (S3-P2Z12: gameplay #2 has its own).
  public static string PlaySceneFor(GardenZoneSpot spot) {
    if (spot != null && !string.IsNullOrEmpty(spot.playSceneName)) return spot.playSceneName;
    return CountingPlayBuilder.SceneName;
  }

  string PlayObjective() {
    return string.IsNullOrEmpty(_playObjective) ? PlayObjectiveText : _playObjective;
  }

  async void EnterPlayAsync(string sceneName) {
    IsBusy = true;
    bool entered = false;
    try {
      PlayTunnel();
      ClearFocus(false);
      if (_router != null) { try { _router.enabled = false; } catch (Exception) { } }
      // Swap the shared micro slot: unload the garden, load the play arena.
      // (EnterMicroAsync refuses while another micro is loaded — that guard is
      // the anti-double-enter contract.) On ANY failure we reload the garden
      // and put the child back at its entry: no void, no stranded player.
      try { await _transition.ExitMicroAsync(_sceneOps); } catch (Exception) { }
      try { entered = await _transition.EnterMicroAsync(_sceneOps, sceneName); }
      catch (Exception e) {
        try { Debug.LogWarning("[CountingGarden] play load failed: " + e.Message, this); }
        catch (Exception) { }
      }
      if (!entered) {
        try {
          string reason = _transition != null ? _transition.LastError : null;
          Debug.LogWarning("[CountingGarden] play enter failed"
            + (string.IsNullOrEmpty(reason) ? " (no play scene)." : ": " + reason), this);
        } catch (Exception) { }
        try { await _transition.EnterMicroAsync(_sceneOps, CountingGardenBuilder.SceneName); } catch (Exception) { }
        IsInPlay = false;
        IsInside = true;
        if (_player != null && _gardenEntry.sqrMagnitude > 0.001f) _player.WarpTo(_gardenEntry);
        PushGardenBounds();
        FollowGarden();
        ShowObjective(GardenObjectiveText);
        return;
      }
      IsInPlay = true;
      IsInside = true;
      if (_player != null) _player.WarpTo(_playEntry);
      PushPlayBounds();
      if (_camera != null && _player != null)
        _camera.Follow(_player.transform, _playFollow);
      if (_camera != null && _playAnchors != null && _playAnchors.Camera != null
          && _playAnchors.CameraLook != null)
        _camera.FrameAnchor(_playAnchors.Camera, _playAnchors.CameraLook, 2.2f);
      ShowObjective(PlayObjective());
      try { Debug.Log("[CountingGarden] entered play arena (warp " + _playEntry.ToString("F1") + ").", this); }
      catch (Exception) { }
    } catch (Exception e) {
      try { Debug.LogWarning("[CountingGarden] play enter issue: " + e.Message, this); } catch (Exception) { }
    } finally {
      if (_router != null) { try { _router.enabled = true; } catch (Exception) { } }
      IsBusy = false;
      StopTunnelSoon();
    }
  }

  public void ExitPlayToGarden() {
    if (!IsInside || !IsInPlay || IsBusy) return;
    ExitPlayAsync();
  }

  async void ExitPlayAsync() {
    IsBusy = true;
    try {
      PlayTunnel();
      if (_router != null) { try { _router.enabled = false; } catch (Exception) { } }
      try { await _transition.ExitMicroAsync(_sceneOps); } catch (Exception) { }
      bool back = false;
      try { back = await _transition.EnterMicroAsync(_sceneOps, CountingGardenBuilder.SceneName); }
      catch (Exception e) {
        try { Debug.LogWarning("[CountingGarden] garden reload failed: " + e.Message, this); }
        catch (Exception) { }
      }
      if (!back) {
        try {
          string reason = _transition != null ? _transition.LastError : null;
          Debug.LogWarning("[CountingGarden] garden reload failed"
            + (string.IsNullOrEmpty(reason) ? "." : ": " + reason), this);
        } catch (Exception) { }
        // Stay truthfully in play (the scene is still loaded and the child can
        // retry the exit) instead of pretending a world that is not there.
        return;
      }
      IsInPlay = false;
      IsInside = true;
      GardenZoneSpot spot = FindSpot(_playedZone);
      Vector3 landing = spot != null ? spot.transform.position : _gardenEntry;
      if (_player != null) _player.WarpTo(new Vector3(landing.x, 0f, landing.z));
      // Landing ON the plot must not instantly re-focus it (the child would be
      // pinned to the zone view right after play): re-arm only after walking
      // clear, exactly like a cancel.
      _proximityArmed = false;
      PushGardenBounds();
      FollowGarden();
      ShowObjective(GardenObjectiveText);
      try { Debug.Log("[CountingGarden] back in the garden (warp " + landing.ToString("F1") + ").", this); }
      catch (Exception) { }
    } catch (Exception e) {
      try { Debug.LogWarning("[CountingGarden] play exit issue: " + e.Message, this); } catch (Exception) { }
    } finally {
      if (_router != null) { try { _router.enabled = true; } catch (Exception) { } }
      IsBusy = false;
      StopTunnelSoon();
    }
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

  public bool TryEnterPlayForTests() {
    if (!IsInside || IsInPlay || IsBusy) return false;
    GardenZoneSpot spot = FindSpot(FocusedZone);
    if (spot == null || !spot.playEnabled) return false;
    IsInPlay = true;
    return true;
  }

  public bool TryExitPlayForTests() {
    if (!IsInside || !IsInPlay) return false;
    IsInPlay = false;
    return true;
  }

  // ---- internals -----------------------------------------------------------------

  void Update() {
    if (_tunnelT > 0f) {
      _tunnelT -= Time.deltaTime;
      if (_tunnelT <= 0f) {
        try { if (_hud != null) _hud.StopTunnel(); } catch (Exception) { }
      }
    }
    if (!IsInside || IsInPlay || IsBusy) return;
    TickProximity();
    TickFocusRefresh();
    TickDemoGate();
    TickCancelClick();
    MaybeAdvanceStairTarget();
    MaybeAdvanceRabbitTarget();
  }

  // The try-run gate: once the ambient demo completes a full loop, the panel
  // appears (user order: only after the child has watched the demo).
  void TickDemoGate() {
    if (!_awaitDemo) return;
    if (_demo == null) { _awaitDemo = false; return; }
    if (_demo.LoopCount <= _demoLoopBase) return;
    _awaitDemo = false;
    GardenZoneSpot spot = FindSpot(FocusedZone);
    if (spot == null) return;
    ShowObjective(spot.ZoneName);
    if (_panel != null) _panel.ShowFor(spot.ZoneName, spot.playEnabled);
    try { Debug.Log("[CountingGarden] try-demo finished (" + _demo.LoopCount + " loops); panel open.", this); }
    catch (Exception) { }
  }

  // Test seam: the same gate pass the frame Update runs.
  public void TickDemoGateForTests() { TickDemoGate(); }

  void TickProximity() {
    if (_player == null) return;
    RunProximity(_player.transform.position);
  }

  bool RunProximity(Vector3 p) {
    if (IsFocused) { _proximityArmed = false; return false; }
    if (!_proximityArmed) {
      if (!AnySpotWithin(p, GardenZoneSpot.FocusRadius + ProximityRearmMargin)) _proximityArmed = true;
      return false;
    }
    GardenZoneSpot near = NearestSpotWithin(p, GardenZoneSpot.FocusRadius);
    if (near == null) return false;
    FocusZone(near.zoneIndex);
    return true;
  }

  // Test seam: the same proximity pass with an explicit player position.
  public bool TickProximityForTests(Vector3 p) { return RunProximity(p); }

  void TickFocusRefresh() {
    if (!IsFocused) return;
    _focusRefreshT += Time.deltaTime;
    if (_focusRefreshT < FocusRefreshSeconds) return;
    _focusRefreshT = 0f;
    FrameFocus(FindSpot(FocusedZone));
  }

  // Double-click cancel (user-chosen gesture): two world clicks OUTSIDE the
  // panel within DoubleClickSeconds. Clicks that land on a zone spot are that
  // spot's own door (ClickRouter will re-focus it) and never count.
  void TickCancelClick() {
    if (!IsFocused) { _doubleClickT = -1f; return; }
    Mouse mouse = Mouse.current;
    if (mouse == null) return; // batch-safe: no pointer device, no clicks
    if (!mouse.leftButton.wasPressedThisFrame) return;
    if (IsPointerOverUi()) return;
    if (RayHitsZoneSpot()) return;
    RegisterOutsideClick(Time.unscaledTime);
  }

  // Test seam + live path: returns true when this click completed the
  // double-click and cancelled the focus.
  public bool RegisterOutsideClick(float now) {
    if (!IsFocused) return false;
    if (_doubleClickT >= 0f && now - _doubleClickT <= DoubleClickSeconds) {
      _doubleClickT = -1f;
      CancelFocus();
      return true;
    }
    _doubleClickT = now;
    return false;
  }

  bool RayHitsZoneSpot() {
    try {
      Camera cam = Camera.main;
      Mouse mouse = Mouse.current;
      if (cam == null || mouse == null) return false;
      Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
      RaycastHit hit;
      if (!Physics.Raycast(ray, out hit, 200f)) return false;
      return hit.collider != null && hit.collider.GetComponentInParent<GardenZoneSpot>() != null;
    } catch (Exception) { return false; }
  }

  static bool IsPointerOverUi() {
    try {
      EventSystem events = EventSystem.current;
      return events != null && events.IsPointerOverGameObject();
    } catch (Exception) { return false; }
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
