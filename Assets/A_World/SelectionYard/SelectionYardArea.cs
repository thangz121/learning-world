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
    CurrentLevel = YardLevel.None;
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

  // PHASE 1 SKELETON: the game doors (C level) record the launch request; the
  // approved arenas are wired in PHASE 4 (JOURNEY to RabbitPlayScene /
  // StairPlayScene and back into this same game yard). Recording here keeps
  // the door contract testable without any live scene refs.
  public string LastPlayRequest { get; private set; } = "";
  public int PlayRequests { get; private set; }

  public void PlayGame(string gameId) {
    if (IsBusy) return;
    if (!LearningMap.IsPlayable(gameId)) {
      Log("refused: '" + gameId + "' is not a human-accepted game");
      return;
    }
    LastPlayRequest = gameId;
    PlayRequests++;
    Log("arena launch requested for '" + gameId + "' (PHASE 4 wires the travel)");
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
      CacheObjective();
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
      if (_player != null) _player.WarpTo(_yardEntry);
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
      if (_player != null) _player.WarpTo(HubReturnPos);
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

  void PushYardBounds() {
    PushIslandBounds(SelectionYardBuilder.WorldOffset,
      SelectionYardBuilder.BoundX, SelectionYardBuilder.BoundZ);
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
