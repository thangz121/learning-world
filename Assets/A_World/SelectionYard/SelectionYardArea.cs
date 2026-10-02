// A_World/SelectionYard/SelectionYardArea.cs — FULL ARCHITECTURE RESET
// (2026-09-29). The persistent module behind the generic yard scene: it owns
// the A -> B -> C travel beats (tunnel, lazy load through the shared micro
// slot, warp, bounds, camera, HUD) for BOTH yard levels:
//   Subject Yard (A) = the Main world itself (subject gates live there);
//   Skill Yard   (B) = SelectionYardScene(Level=skill, SubjectId=...);
//   Game Yard    (C) = SelectionYardScene(Level=game, SkillId=...);
//   Game         (D) = the approved arena scenes (wired in PHASE 4).
// Lives in MarketScene (which never unloads), so it survives every yard swap
// — the same proven ownership pattern as the old garden area.
// C# 9.0 only.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class SelectionYardArea : MonoBehaviour, IMicroWorldArea {
  public const string AreaId = "selection_yard";
  const float TunnelSeconds = 0.35f;

  public enum YardLevel { None = 0, Skill = 1, Game = 2 }

  // ---- pending context (read by GameInstaller when the yard scene loads) ----
  public YardLevel PendingLevel { get; private set; } = YardLevel.None;
  public string PendingSubjectId { get; private set; } = "";
  public string PendingSkillId { get; private set; } = "";

  // ---- live context (the yard currently loaded) -----------------------------
  public YardLevel CurrentLevel { get; private set; } = YardLevel.None;
  public string CurrentSubjectId { get; private set; } = "";
  public string CurrentSkillId { get; private set; } = "";

  public Vector3 HubReturnPos;     // subject-yard landing (Main world)
  public ActivityAnchors Anchors;  // yard scene registry (after load)

  WorldTransition _transition;
  ISceneOps _sceneOps;
  ClickToMove _player;
  SmartCamera _camera;
  MarketHUD _hud;
  ClickRouter _router;
  Vector3 _yardEntry = new Vector3(0f, 0f, -3f);
  float _tunnelT = -1f;
  string _preObjective;

  public bool IsInside { get; private set; }
  public bool IsBusy { get; private set; }
  public bool CanEnter { get { return !IsInside || CurrentLevel == YardLevel.Game; } }
  public bool CanExit { get { return IsInside && !IsBusy; } }
  public ClickToMove Player { get { return _player; } }
  public WorldTransition Transition { get { return _transition; } }

  // The objective line shown while a yard is open (data-driven from the map).
  public string PendingObjective {
    get {
      if (PendingLevel == YardLevel.Game) return LearningMap.GameYardTitle(PendingSkillId);
      if (PendingLevel == YardLevel.Skill) return LearningMap.SkillYardTitle(PendingSubjectId);
      return "";
    }
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
    CurrentLevel = YardLevel.None;
    // Diagnostic target overrides (same CLI pattern as the old garden area):
    // "-stair-target N" / "-rabbit-target N", inert otherwise.
    try {
      StairTarget = ParseStairTargetArg(Environment.GetCommandLineArgs(), StairDefaultTarget);
      _stairLifeTarget = StairTarget;
      RabbitTarget = ParseRabbitTargetArg(Environment.GetCommandLineArgs(), RabbitDefaultTarget);
      _rabbitLifeTarget = RabbitTarget;
    } catch (Exception) { }
  }

  public void BindRouter(ClickRouter router) { _router = router; }

  // Called by GameInstaller after the yard scene finishes loading (lazy).
  public void SetYard(Vector3 entry, ActivityAnchors anchors) {
    _yardEntry = entry;
    Anchors = anchors;
  }

  // ---- forward navigation (called by SelectionGate) ------------------------------

  public void EnterSkill(string subjectId) {
    if (IsBusy) return;
    if (LearningMap.Subject(subjectId) == null) {
      Log("refused: unknown subject '" + subjectId + "'");
      return;
    }
    if (CurrentLevel == YardLevel.Skill && Same(CurrentSubjectId, subjectId)) return; // idempotent
    SetPending(YardLevel.Skill, subjectId, "");
    EnterYardAsync();
  }

  public void EnterGame(string skillId) {
    if (IsBusy) return;
    if (LearningMap.Skill(skillId) == null) {
      Log("refused: unknown skill '" + skillId + "'");
      return;
    }
    if (CurrentLevel == YardLevel.Game && Same(CurrentSkillId, skillId)) return; // idempotent
    SetPending(YardLevel.Game, "", skillId);
    EnterYardAsync();
  }

  // ---- approved-game play flow (PHASE 4) -------------------------------------------
  // The C yard's game doors launch the EXISTING arenas through the shared micro
  // slot; the arena exits come back to THIS same Game Yard. Lifecycle ownership
  // was re-homed here from the obsolete CountingGardenArea (PHASE 4): one latch
  // per game, target ladders + CLI diagnostics, adopt/replay rules. The arenas
  // keep ALL their gameplay (demo, questions, feedback) — only the entry/exit
  // wiring is ours.
  public string LastPlayRequest { get; private set; } = "";
  public int PlayRequests { get; private set; }
  public bool IsInPlay { get; private set; }
  public string CurrentGameId { get; private set; } = "";

  public void PlayGame(string gameId) {
    if (IsBusy || IsInPlay) return;
    GameEntry game = LearningMap.Game(gameId);
    bool ok = game != null && CurrentLevel == YardLevel.Game
      && string.Equals(game.SkillId, CurrentSkillId, StringComparison.OrdinalIgnoreCase)
      && LearningMap.CanLaunch(gameId);
    if (!ok) {
      Log("refused: '" + gameId + "' is not a playable game of this yard");
      return;
    }
    LastPlayRequest = gameId;
    PlayRequests++;
    Log("arena launch requested for '" + gameId + "'");
    // Live wiring only when the shared services exist (EditMode seams call
    // PlayGame with no refs and observe the request alone).
    if (_transition != null && _sceneOps != null) EnterGameAsync(game);
  }

  // ---- stair lifecycle (ported from CountingGardenArea; owner renamed) ----------
  public ActivityLifecycle StairLifecycle { get; private set; } =
    new ActivityLifecycle("number_stairs", "SelectionYardArea");
  public NumberStairs StairGame { get; private set; }
  public void BindStairGame(NumberStairs game) { StairGame = game; }

  public const int StairDefaultTarget = 3;
  public const string StairTargetFlag = "-stair-target";
  public static readonly int[] StairProgression = { 3, 5, 7, 9, 1 };
  public int StairTarget { get; private set; } = StairDefaultTarget;
  int _stairLifeTarget = StairDefaultTarget;

  public static int NextStairTarget(int t) {
    for (int i = 0; i < StairProgression.Length; i++) {
      if (StairProgression[i] == t) return StairProgression[(i + 1) % StairProgression.Length];
    }
    return StairDefaultTarget;
  }

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

  void MaybeAdvanceStairTarget() {
    if (StairLifecycle == null) return;
    if (StairLifecycle.State != ActivityState.Completed) return;
    if (_stairLifeTarget != StairTarget) return;
    int next = NextStairTarget(StairTarget);
    StairTarget = next;
    StairLifecycle = new ActivityLifecycle("number_stairs", "SelectionYardArea");
    _stairLifeTarget = next;
    Log("stair target advanced to " + next);
  }

  public void SetStairTargetForTests(int t) {
    StairTarget = StairHillBuilder.ClampTarget(t);
    _stairLifeTarget = StairTarget;
  }
  public void TickStairProgressionForTests() { MaybeAdvanceStairTarget(); }

  // ---- rabbit lifecycle (ported, same discipline) --------------------------------
  public ActivityLifecycle RabbitLifecycle { get; private set; } =
    new ActivityLifecycle("rabbit_feed", "SelectionYardArea");
  public RabbitFeed RabbitGame { get; private set; }
  public void BindRabbitGame(RabbitFeed game) { RabbitGame = game; }

  public const int RabbitDefaultTarget = 3;
  public const string RabbitTargetFlag = "-rabbit-target";
  public static readonly int[] RabbitProgression = { 3, 4, 5, 6, 7, 8, 9, 1, 2 };
  public bool RandomRabbitTargets; // S3-P2Z20: live play turns this on
  public int RabbitTarget { get; private set; } = RabbitDefaultTarget;
  int _rabbitLifeTarget = RabbitDefaultTarget;

  public static int NextRabbitTarget(int t) {
    for (int i = 0; i < RabbitProgression.Length; i++) {
      if (RabbitProgression[i] == t) return RabbitProgression[(i + 1) % RabbitProgression.Length];
    }
    return RabbitDefaultTarget;
  }

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

  void MaybeAdvanceRabbitTarget() {
    if (RabbitLifecycle == null) return;
    if (RabbitLifecycle.State != ActivityState.Completed) return;
    if (_rabbitLifeTarget != RabbitTarget) return;
    int next;
    if (RandomRabbitTargets) {
      next = RabbitTarget;
      for (int guard = 0; guard < 32 && next == RabbitTarget; guard++)
        next = UnityEngine.Random.Range(1, RabbitPlayBuilder.MaxTarget + 1);
    } else {
      next = NextRabbitTarget(RabbitTarget);
    }
    RabbitTarget = next;
    RabbitLifecycle = new ActivityLifecycle("rabbit_feed", "SelectionYardArea");
    _rabbitLifeTarget = next;
    Log("rabbit target advanced to " + next);
  }

  public void SetRabbitTargetForTests(int t) {
    RabbitTarget = RabbitPlayBuilder.ClampTarget(t);
    _rabbitLifeTarget = RabbitTarget;
  }
  public void TickRabbitProgressionForTests() { MaybeAdvanceRabbitTarget(); }

  public ActivityLifecycle GeometryLifecycle { get; private set; } =
    new ActivityLifecycle("shape_builder", "SelectionYardArea");
  public GeometryPlay GeometryGame { get; private set; }
  public void BindGeometryGame(GeometryPlay game) { GeometryGame = game; }

  public ActivityLifecycle MarketLifecycle { get; private set; } =
    new ActivityLifecycle("comparison_market", "SelectionYardArea");
  public ComparisonMarket MarketGame { get; private set; }
  public void BindMarketGame(ComparisonMarket game) { MarketGame = game; }

  public ActivityLifecycle CityLifecycle { get; private set; } =
    new ActivityLifecycle("classification_city", "SelectionYardArea");
  public ClassificationCity CityGame { get; private set; }
  public void BindCityGame(ClassificationCity game) { CityGame = game; }

  public ActivityLifecycle StationLifecycle { get; private set; } =
    new ActivityLifecycle("ordering_station", "SelectionYardArea");
  public OrderingStation StationGame { get; private set; }
  public void BindStationGame(OrderingStation game) { StationGame = game; }

  // ---- arena travel (single micro slot: yard out -> arena in, and back) ----------
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
  Vector3 _playEntry;
  ActivityAnchors _playAnchors;
  Vector3 _playCenter;
  float _playBoundX = 40f;
  float _playBoundZ = 40f;
  Vector3 _playFollow = new Vector3(0f, 5f, -7f);
  string _playObjective;

  async void EnterGameAsync(GameEntry game) {
    IsBusy = true;
    try {
      PlayTunnel();
      if (_router != null) { try { _router.enabled = false; } catch (Exception) { } }
      string yardObjective = LearningMap.GameYardTitle(CurrentSkillId);
      // Swap the shared micro slot: unload the game yard, load the arena.
      try { await _transition.ExitMicroAsync(_sceneOps); } catch (Exception) { }
      bool entered = false;
      try {
        entered = await _transition.EnterMicroAsync(_sceneOps, game.SceneName);
      } catch (Exception e) { Log("game load failed: " + e.Message); }
      if (!entered) {
        // Honest failure: reload the game yard with the SAME context.
        SetPending(YardLevel.Game, "", CurrentSkillId);
        try { await _transition.EnterMicroAsync(_sceneOps, SelectionYardBuilder.SceneName); } catch (Exception) { }
        CurrentLevel = YardLevel.Game;
        WarpPlayer(_yardEntry);
        PushYardBounds();
        if (_camera != null && _player != null)
          _camera.Follow(_player.transform, SelectionYardBuilder.FollowOffset);
        ShowObjective(yardObjective);
        Log("game enter failed; back in the game yard");
        return;
      }
      IsInPlay = true;
      CurrentGameId = game.Id;
      if (_player != null) {
        bool warped = _player.WarpTo(_playEntry);
        if (!warped) warped = _player.WarpToLoose(_playEntry);
        Log("play warp " + (warped ? "ok " : "forced ") + Fmt(_playEntry)
          + " player=" + Fmt(_player.transform.position));
      }
      PushPlayBounds();
      if (_camera != null && _player != null)
        _camera.Follow(_player.transform, _playFollow);
      if (_camera != null && _playAnchors != null && _playAnchors.Camera != null
          && _playAnchors.CameraLook != null)
        _camera.FrameAnchor(_playAnchors.Camera, _playAnchors.CameraLook, 2.2f);
      ShowObjective(string.IsNullOrEmpty(_playObjective) ? LearningMap.GameDisplay(game.Id) : _playObjective);
      Log("entered arena '" + game.Id + "' (warp " + Fmt(_playEntry) + ")");
    } catch (Exception e) {
      Log("game enter issue: " + e.Message);
    }
    if (_router != null) { try { _router.enabled = true; } catch (Exception) { } }
    IsBusy = false;
    StopTunnelSoon();
  }

  // The arena's own exit portal calls this (MicroWorldPortal.PlayExit routes
  // to the yard when it is bound): swap the slot back to the GAME YARD with
  // the same skill context and apply the real-mission ladder advance.
  public void ExitGameToYard() {
    if (!IsInPlay || IsBusy) return;
    ExitGameAsync();
  }

  async void ExitGameAsync() {
    IsBusy = true;
    try {
      PlayTunnel();
      if (_router != null) { try { _router.enabled = false; } catch (Exception) { } }
      try { await _transition.ExitMicroAsync(_sceneOps); } catch (Exception) { }
      // Rebuild the SAME game yard (the installer reads the pending context).
      SetPending(YardLevel.Game, "", CurrentSkillId);
      bool back = false;
      try {
        back = await _transition.EnterMicroAsync(_sceneOps, SelectionYardBuilder.SceneName);
      } catch (Exception e) { Log("game yard reload failed: " + e.Message); }
      if (!back) {
        // Stay truthfully in the arena (still loaded): the child can retry.
        Log("game exit failed: game yard reload refused");
        return;
      }
      IsInPlay = false;
      CurrentGameId = "";
      CurrentLevel = YardLevel.Game;
      WarpPlayer(_yardEntry);
      PushYardBounds();
      if (_camera != null && _player != null)
        _camera.Follow(_player.transform, SelectionYardBuilder.FollowOffset);
      if (_camera != null && Anchors != null && Anchors.Camera != null && Anchors.CameraLook != null)
        _camera.FrameAnchor(Anchors.Camera, Anchors.CameraLook, 2.4f);
      ShowObjective(LearningMap.GameYardTitle(CurrentSkillId));
      // Real-mission advance (same rules as the old garden): a COMPLETED life
      // advances the next visit's target; a mid-lesson exit replays it.
      MaybeAdvanceStairTarget();
      MaybeAdvanceRabbitTarget();
      Log("back in the game yard (" + CurrentSkillId + ")");
    } catch (Exception e) {
      Log("game exit issue: " + e.Message);
    }
    if (_router != null) { try { _router.enabled = true; } catch (Exception) { } }
    IsBusy = false;
    StopTunnelSoon();
  }

  // Test seams (no live refs).
  public bool TryEnterPlayForTests() {
    if (IsBusy || IsInPlay || CurrentLevel != YardLevel.Game) return false;
    IsInPlay = true;
    return true;
  }

  public bool TryExitPlayForTests() {
    if (!IsInPlay) return false;
    IsInPlay = false;
    return true;
  }

  // Up one level: Game yard -> Skill yard of that skill's subject;
  // Skill yard -> back to the Subject Yard (the Main world).
  public void Back() {
    if (IsBusy) return;
    if (CurrentLevel == YardLevel.Game) {
      SkillEntry skill = ResolveBackSkill();
      if (skill != null) { EnterSkill(skill.SubjectId); return; }
    }
    ExitToHub();
  }

  SkillEntry ResolveBackSkill() {
    // Real flow stores the SKILL id in the game level; the game-id fallback
    // keeps Back() honest if a test (or future caller) holds the game id.
    SkillEntry skill = LearningMap.Skill(CurrentSkillId);
    return skill != null ? skill : LearningMap.SkillOfGame(CurrentSkillId);
  }

  // Test seam: where would Back() lead right now? ("skill:<subject>" |
  // "hub" | "blocked").
  public string BackTargetForTests() {
    if (IsBusy) return "blocked";
    if (CurrentLevel == YardLevel.Game) {
      SkillEntry skill = ResolveBackSkill();
      return skill != null ? "skill:" + skill.SubjectId : "hub";
    }
    return "hub";
  }

  // ---- travel beats ----------------------------------------------------------------

  async void EnterYardAsync() {
    IsBusy = true;
    try {
      PlayTunnel();
      // PHASE 3 hot-fix (foreground evidence: HUD showed "Đếm — Chọn trò chơi"
      // back in the hub): only the FIRST entry (from the Subject Yard) caches
      // the resting objective — yard-to-yard swaps must never clobber it.
      if (!IsInside) CacheObjective();
      if (IsInside) {
        // Swapping one yard for another (game -> skill back-navigation).
        try { await _transition.ExitMicroAsync(_sceneOps); } catch (Exception) { }
        IsInside = false;
      }
      bool loaded = false;
      try {
        loaded = await _transition.EnterMicroAsync(_sceneOps, SelectionYardBuilder.SceneName);
      } catch (Exception e) {
        Log("yard load failed: " + e.Message);
      }
      if (!loaded) {
        // Honest failure: back to the Main subject yard, HUD restored.
        RestoreObjective();
        IsBusy = false;
        StopTunnelSoon();
        return;
      }
      IsInside = true;
      CurrentLevel = PendingLevel;
      CurrentSubjectId = PendingSubjectId;
      CurrentSkillId = PendingSkillId;
      WarpPlayer(_yardEntry);
      PushYardBounds();
      if (_camera != null && _player != null)
        _camera.Follow(_player.transform, SelectionYardBuilder.FollowOffset);
      if (_camera != null && Anchors != null && Anchors.Camera != null && Anchors.CameraLook != null)
        _camera.FrameAnchor(Anchors.Camera, Anchors.CameraLook, 2.4f);
      ShowObjective(PendingObjective);
      Log("entered " + PendingLevel + " yard (" + PendingSubjectId + PendingSkillId + ")");
    } catch (Exception e) {
      Log("enter issue: " + e.Message);
    }
    IsBusy = false;
    StopTunnelSoon();
  }

  public async void ExitToHub() {
    if (!CanExit) return;
    IsBusy = true;
    try {
      PlayTunnel();
      WarpPlayer(HubReturnPos);
      PushMainBounds();
      if (_camera != null && _player != null)
        _camera.Follow(_player.transform, MarketBuilder.HubFollowOffset);
      RestoreObjective();
      try { await _transition.ExitMicroAsync(_sceneOps); } catch (Exception) { }
      IsInside = false;
      CurrentLevel = YardLevel.None;
      CurrentSubjectId = "";
      CurrentSkillId = "";
      Log("back in the subject yard (Main)");
    } catch (Exception e) {
      Log("exit issue: " + e.Message);
    }
    IsBusy = false;
    StopTunnelSoon();
  }

  // ---- IMicroWorldArea -----------------------------------------------------------
  // EnterFromHub = (re)enter the pending level (shared-interface seam; the
  // selection gates call EnterSkill/EnterGame directly).
  public void EnterFromHub() {
    if (PendingLevel != YardLevel.None) EnterYardAsync();
  }

  // Robust reposition: the strict WarpTo can fail while a freshly loaded
  // arena's NavMesh is still settling; fall back to the loose warp so the
  // child is never left off the island.
  void WarpPlayer(Vector3 p) {
    if (_player == null) return;
    if (!_player.WarpTo(p)) _player.WarpToLoose(p);
  }

  void PushYardBounds() {
    PushIslandBounds(SelectionYardBuilder.WorldOffset,
      SelectionYardBuilder.BoundX, SelectionYardBuilder.BoundZ);
  }

  void PushPlayBounds() {
    PushIslandBounds(_playCenter, _playBoundX, _playBoundZ);
  }

  static string Fmt(Vector3 v) {
    return "(" + v.x.ToString("F1") + "," + v.z.ToString("F1") + ")";
  }

  void PushMainBounds() {
    PushIslandBounds(Vector3.zero, MarketBuilder.BoundX, MarketBuilder.BoundZ);
  }

  void PushIslandBounds(Vector3 center, float x, float z) {
    if (_router == null) return;
    try {
      _router.boundCenter = center;
      _router.boundX = x;
      _router.boundZ = z;
    } catch (Exception) { }
  }

  void SetPending(YardLevel level, string subjectId, string skillId) {
    PendingLevel = level;
    PendingSubjectId = subjectId != null ? subjectId : "";
    PendingSkillId = skillId != null ? skillId : "";
  }

  // ---- pure state seams (EditMode cover without live refs) ----------------------

  public void SetPendingForTests(YardLevel level, string subjectId, string skillId) {
    SetPending(level, subjectId, skillId);
  }

  public void SetCurrentForTests(YardLevel level, string subjectId, string skillId) {
    CurrentLevel = level;
    CurrentSubjectId = subjectId != null ? subjectId : "";
    CurrentSkillId = skillId != null ? skillId : "";
  }

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

  // ---- internals ------------------------------------------------------------------

  void Update() {
    if (_tunnelT > 0f) {
      _tunnelT -= Time.deltaTime;
      if (_tunnelT <= 0f) {
        try { if (_hud != null) _hud.StopTunnel(); } catch (Exception) { }
      }
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
      else _hud.ShowObjective(DialogueLang.T("Choose a gate!", "Chọn một cổng nhé!"));
    } catch (Exception) { }
  }

  void ShowObjective(string text) {
    try { if (_hud != null && !string.IsNullOrEmpty(text)) _hud.ShowObjective(text); } catch (Exception) { }
  }

  void Log(string message) {
    try { Debug.Log("[SelectionYard] " + message, this); } catch (Exception) { }
  }

  static bool Same(string a, string b) {
    return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
  }
}
