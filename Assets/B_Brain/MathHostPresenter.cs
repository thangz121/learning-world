// B_Brain/MathHostPresenter.cs — Phase 3.0.x S3 (3.0.1.1: Golden body).
// Reuses the Milo/Mia presenter PATTERN with no new framework:
//   GameplayRoot (THIS transform: SpawnPosition, CapsuleCollider interaction,
//   IClickTarget, quest/hint subscriptions) vs VisualRoot (child: Quaternius
//   Worker_Female mesh + Animator + doll face kit, Golden Character Standard).
//   TessVisual prefab = MiaVisual structure (same rig + controller triggers),
//   Tess identity = Math-blue vest + gold hat (Golden §9: identity by clothing
//   tint, instance copies only).
// Differences from Milo/Mia (documented, not drift):
//   - bring completes WITHOUT carry state (skeleton simplification: walking the
//     found "one" to Tess IS the handover; carry token is S4 polish). Click on
//     Tess or 0.75m proximity while find-done reports Bring via the SAME
//     explicit ReportAction path Mia uses — re-clicks can never double-advance;
//   - NO StoryMoment publishes (Bootstrap's WrongChoice framing targets Mia's
//     Main anchor, which is hidden while Math is active — a Math moment would
//     frame a dead anchor). Feedback is voice + face + HUD only, via
//     MathQuestDirector. Golden reactions owned locally (greet/click Happy
//     pulse, bring PickUp, complete Happy baseline + Celebrate).
// MonoBehaviour (Unity instantiates): parameterless ctor + public Bind only.
// All voice output goes through the static Tess class (IAudioDirector only).
// No input code, no Camera calls (except Y-facing like Mia/Milo), no `new`
// services, no provider/Worker refs. Null-guarded throughout. C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MathHostPresenter : MonoBehaviour, IClickTarget {
  static readonly WordId OneWord = new WordId("one");
  static readonly QuestId MathQuest = new QuestId("math_counting");

  const float GreetDistance = 2.1f;
  const float ProximityBringRange = 0.75f; // converges with click arrivalRange (R8 principle, like Mia)
  const float WaveDuration = 1.4f; // acknowledgement gesture (Mia pattern: arm wave, not head nod)

  [Header("Lead-wired placement (Math lobby anchor)")]
  public Vector3 SpawnPosition = new Vector3(2.2f, 0f, 0.8f);
  [Header("Lead-wired player reference (Transform only, null-guarded)")]
  public Transform PlayerTarget;

  IGameEventBus _bus;
  IQuestService _quests;
  IHintService _hints;
  readonly List<IDisposable> _subs = new List<IDisposable>();

  // First-talk seam (MathQuestDirector wires it; the math quest starts on
  // first talk, not at scene build — same contract as Milo/Bootstrap).
  // Fires EXACTLY once, after the normal click feedback.
  public Action OnFirstTalk;
  // Fires on EVERY click (including after first talk).
  public Action OnTalk;
  bool _talked;
  bool _questStarted;

  QuestId _activeQuest = MathQuest;
  float _clockSinceProgress;
  bool _greeted;

  // Visual rig (Golden §1: GameplayRoot here, VisualRoot child with the
  // Quaternius Worker_Female mesh + Animator + doll face kit — same contract
  // as MiaPresenter; presentation only, never gameplay state).
  Animator _animator;
  Transform _headBone;
  Transform _footL;
  Transform _footR;
  CharacterPresentation _presentation;
  SkinnedMeshRenderer _skinForFace;
  Transform _visualForFace;
  Transform _waveBone;
  Quaternion _waveBase = Quaternion.identity;
  bool _waving;
  float _waveT;

  void Awake() {
    BuildBodyImmediate();
  }

  // Explicit body build (MarketHUD.BuildUiImmediate precedent): production
  // calls it from Awake; EditMode batch contexts (where AddComponent does not
  // deliver Awake synchronously) drive it directly in tests. Idempotent —
  // whichever path lands first wins, the other is a no-op.
  bool _bodyBuilt;

  public void BuildBodyImmediate() {
    if (_bodyBuilt) return;
    _bodyBuilt = true;
    // BuildVisual lays the rig + face kit AND the interaction capsule (Mia
    // pattern: capsule is part of the visual build); ApplySpawnPosition then
    // wins over the prefab pose with the Lead-wired anchor.
    BuildVisual();
    ApplySpawnPosition();
  }

  void Start() {
    // Lead sets SpawnPosition after AddComponent (post-Awake); re-apply here
    // so the wired value wins before the first frame (Milo pattern).
    ApplySpawnPosition();
    FaceCameraImmediate();
    // Face setup waits for Start: world transforms read during Awake (inside
    // AddComponent) are stale-identity (Mia pattern, Golden §4).
    if (_presentation != null) _presentation.SetupFace(_skinForFace, _headBone, transform, _visualForFace);
  }

  void ApplySpawnPosition() {
    transform.position = SpawnPosition;
  }

  // Injection boundary (wired by GameInstaller). Re-bind safe: old
  // subscriptions are disposed first. Null bus -> unbound, no subscriptions.
  public void Bind(IGameEventBus bus, IQuestService quests, IHintService hints) {
    ClearSubs();
    _bus = bus;
    _quests = quests;
    _hints = hints;
    if (_bus == null) return;
    _subs.Add(_bus.Subscribe<WordSeenEvent>(OnWordSeen));
    _subs.Add(_bus.Subscribe<QuestStartedEvent>(OnQuestStarted));
    _subs.Add(_bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted));
    _subs.Add(_bus.Subscribe<HintLevelChanged>(OnHintLevel));
  }

  // IClickTarget entry point for A's router (no input code in this file).
  public void OnClicked() {
    Wave();
    if (_presentation != null) _presentation.PulseExpression(CharacterExpression.Happy, 2.5f);
    Tess.SayName(); // player rule: clickables read their name
    if (OnTalk != null) OnTalk();
    if (!_talked) {
      _talked = true;
      if (OnFirstTalk != null) OnFirstTalk();
      return;
    }
    if (!IsFindDone()) return;
    CompleteBring();
  }

  // Find-done = quest started, find objective (index 0) advanced, quest not
  // yet completed. Bring objective sits at index 1 (mirrors the JSON order).
  bool IsFindDone() {
    if (!_questStarted || _quests == null) return false;
    try {
      QuestState s = _quests.GetState(_activeQuest);
      return !s.Completed && s.ObjectiveIndex >= 1;
    } catch (Exception) { return false; }
  }

  // Shared bring completion (click path + proximity path stay identical, Mia
  // pattern): explicit ReportAction is the ONLY path that advances bring.
  void CompleteBring() {
    if (!_questStarted || _quests == null) return;
    try {
      if (_quests.GetState(_activeQuest).Completed) return;
      _quests.ReportAction(PlayerAction.Bring, OneWord);
      Wave();
      // Golden §11: bring received reads on the face + body (Mia pattern).
      if (_presentation != null) _presentation.PulseExpression(CharacterExpression.Happy, 3f);
      if (_animator != null) _animator.SetTrigger("PickUp"); // bend receiving the item
    } catch (Exception) { }
  }

  // Proximity bring (Mia pattern, child-friendly): walking up to Tess while
  // find-done completes the bring even when the click ray is eaten. Silent
  // unless find-done (wandering near Tess must never count as anything).
  // The deterministic overload is what tests drive.
  public void TryProximityBring(Vector3 playerPos) {
    if (!IsFindDone()) return;
    float dx = playerPos.x - transform.position.x;
    float dz = playerPos.z - transform.position.z;
    if (dx * dx + dz * dz > ProximityBringRange * ProximityBringRange) return;
    CompleteBring();
  }

  void OnWordSeen(WordSeenEvent e) {
    _clockSinceProgress = 0f;
  }

  void OnQuestStarted(QuestStartedEvent e) {
    _activeQuest = e.QuestId;
    _questStarted = true;
    _clockSinceProgress = 0f;
    Wave();
    if (_presentation != null) _presentation.PulseExpression(CharacterExpression.Happy, 2.5f);
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    _clockSinceProgress = 0f;
    if (e.QuestId.Value != _activeQuest.Value) return;
    // Golden §11: durable Happy baseline + Celebrate trigger (Mia pattern).
    if (_presentation != null) _presentation.SetExpression(CharacterExpression.Happy);
    if (_animator != null) _animator.SetTrigger("Celebrate");
  }

  void OnHintLevel(HintLevelChanged e) {
    if (e.QuestId.Value != _activeQuest.Value) return;
    if (_quests == null) return;
    // Skeleton: wave + repeat the current instruction (no dedicated L2/L3 Tess
    // voice lines in the frozen 40-line pack — Phase 3.1 math pack owns them).
    Wave();
    try {
      QuestState s = _quests.GetState(_activeQuest);
      if (s != null && !s.Completed && s.ObjectiveIndex >= 1) Tess.SayBring();
      else Tess.SayFind();
    } catch (Exception) { }
  }

  void Update() {
    float dt = Time.deltaTime;
    _clockSinceProgress += dt;

    // Hint escalation tick (Milo pattern): drives rate-limit + NextReview for
    // the active quest. Without a ticking presenter the levels would stall.
    if (_hints != null && _questStarted) {
      bool done = false;
      try { if (_quests != null) done = _quests.GetState(_activeQuest).Completed; }
      catch (Exception) { }
      if (!done) {
        try { _hints.Tick(_activeQuest, dt, _clockSinceProgress); }
        catch (Exception) { }
      }
    }

    // Hostess greeting posture (Mia pattern): face the gameplay camera (not
    // the player: the follow camera sits behind the player, so player-facing
    // turns the doll face AWAY from the viewer). Y-only, smoothed.
    // Greet-once per session (Milo pattern): the latch NEVER re-arms.
    Camera cam = null;
    try { cam = Camera.main; } catch (Exception) { }
    if (cam != null) {
      Vector3 toCam = cam.transform.position - transform.position;
      toCam.y = 0f;
      if (toCam.sqrMagnitude > 0.0001f) {
        Quaternion look = Quaternion.LookRotation(toCam);
        float t = 1f - Mathf.Exp(-3f * dt);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, t);
      }
      if (!_greeted && PlayerTarget != null) {
        Vector3 toPlayer = PlayerTarget.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < GreetDistance * GreetDistance) {
          _greeted = true;
          Wave();
          if (_presentation != null) _presentation.PulseExpression(CharacterExpression.Happy, 2.5f);
          Tess.SayName();
        }
      }
    } else if (PlayerTarget != null) {
      Vector3 toPlayer = PlayerTarget.position - transform.position;
      toPlayer.y = 0f;
      if (toPlayer.sqrMagnitude > 0.0001f)
        transform.rotation = Quaternion.LookRotation(toPlayer);
      if (!_greeted && toPlayer.sqrMagnitude < GreetDistance * GreetDistance) {
        _greeted = true;
        Wave();
        Tess.SayName();
      }
    }

    // Live proximity bring check (deterministic overload above is test-driven).
    if (PlayerTarget != null) {
      try { TryProximityBring(PlayerTarget.position); }
      catch (Exception) { }
    }
  }

  void Wave() {
    _waveT = WaveDuration;
  }

  void LateUpdate() {
    // Secondary gesture layer (Mia pattern): procedural arm wave applied AFTER
    // the Animator evaluated. Restores the Animator-driven pose exactly when
    // the timer lapses, so idle motion is never frozen.
    if (_waveBone == null) return;
    if (_waveT > 0f) {
      if (!_waving) {
        _waving = true;
        _waveBase = _waveBone.localRotation;
      }
      _waveT -= Time.deltaTime;
      float wave = Mathf.Sin(Time.time * 14f) * 18f;
      _waveBone.localRotation = _waveBase * Quaternion.Euler(0f, 0f, -75f + wave);
      if (_waveT <= 0f) {
        _waving = false;
        _waveBone.localRotation = _waveBase;
      }
    }
  }

  void FaceCameraImmediate() {
    Camera cam = null;
    try { cam = Camera.main; } catch (Exception) { }
    if (cam == null) return;
    Vector3 toCam = cam.transform.position - transform.position;
    toCam.y = 0f;
    if (toCam.sqrMagnitude > 0.0001f)
      transform.rotation = Quaternion.LookRotation(toCam);
  }

  void ClearSubs() {
    foreach (IDisposable s in _subs) {
      if (s != null) {
        try { s.Dispose(); } catch (Exception) { }
      }
    }
    _subs.Clear();
  }

  void OnDisable() {
    ClearSubs();
  }

  // ---- VisualRoot: real animated mesh + doll face kit (Golden §1) ------------
  // GameplayRoot (this transform) carries the explicit interaction capsule;
  // the visual child carries mesh + Animator + face and may be swapped freely.
  // TessVisual prefab = MiaVisual structure (Quaternius Worker_Female + the
  // shared NPC controller triggers); Tess identity = Math-blue vest + gold hat
  // (Golden §9, instance copies only — the imported FBX stays pristine).
  void BuildVisual() {
    GameObject visualPrefab = Resources.Load<GameObject>("NpcVisuals/TessVisual");
    if (visualPrefab == null) {
      Debug.LogError("[MathHostPresenter] Missing NpcVisuals/TessVisual prefab; Tess has no body.", this);
      AddInteractionCapsule();
      return;
    }
    GameObject visual = Instantiate(visualPrefab, transform, false);
    visual.name = "TessVisualRoot";
    // R6 grounding (Mia pattern, same rig): visual lift 0.02 lands the sole on
    // the flat lawn; no sink risk. Photos decide.
    visual.transform.localPosition = new Vector3(0f, 0.02f, 0f);
    visual.transform.localRotation = Quaternion.identity;
    // Scale fix: the quaternius armature imports at 100x. Half the visual so
    // Tess stands ~1.7m next to the player capsule. GameplayRoot
    // (collider/identity) stays at scale 1. Face-kit compensation divides by
    // bone lossyScale, so it adapts automatically (Mia pattern).
    visual.transform.localScale = Vector3.one * 0.5f;

    _animator = visual.GetComponentInChildren<Animator>(true);
    if (_animator == null) {
      Debug.LogError("[MathHostPresenter] TessVisual has no Animator; idle/celebrate clips will not play.", this);
    }

    SkinnedMeshRenderer skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
    if (skin != null) {
      // Tess identity (documented, reversible): Math-blue vest (subject
      // colour, distinct from Mia's coral + Milo's orange), gold hat echoing
      // the Math gate beam + quest cube, warm tan face, warm mid-brown skin
      // (same readability values as Mia, R5-A/R5c decided).
      CharacterPresentation.TintSharedMaterials(skin, "Vest", new Color(0.25f, 0.45f, 0.85f));
      CharacterPresentation.TintSharedMaterials(skin, "Hat", new Color(0.98f, 0.78f, 0.25f));
      CharacterPresentation.TintSharedMaterials(skin, "Face", new Color(0.93f, 0.70f, 0.52f), 0.25f);
      CharacterPresentation.TintSharedMaterials(skin, "Skin", new Color(0.42f, 0.27f, 0.17f), 0.3f);
    }
    if (skin != null && skin.bones != null) {
      foreach (Transform bone in skin.bones) {
        if (bone == null) continue;
        if (_headBone == null && bone.name == "Head") _headBone = bone;
        if (_waveBone == null && (bone.name == "UpperArm.R" || bone.name == "Shoulder.R"))
          _waveBone = bone;
        if (_footL == null && bone.name == "Foot.L") _footL = bone;
        if (_footR == null && bone.name == "Foot.R") _footR = bone;
      }
    }
    if (_headBone == null) {
      Debug.LogWarning("[MathHostPresenter] Head bone not found; face kit attached to visual root.", this);
    }
    // Reusable presentation layer (face geometry setup deferred to Start).
    _presentation = gameObject.AddComponent<CharacterPresentation>();
    // Final polish footwear (shared helper, Foot.L/R proven on all rigs).
    _presentation.QueueShoe(_footL, "ShoeL");
    _presentation.QueueShoe(_footR, "ShoeR");
    _skinForFace = skin;
    _visualForFace = visual.transform;
    AddInteractionCapsule();
  }

  void AddInteractionCapsule() {
    // Root-level interaction volume (Mia pattern, same 1.7m rig): generous
    // vertical cover so clicks on head/hat still land the presenter.
    CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
    col.radius = 0.4f;
    col.height = 1.7f;
    col.center = new Vector3(0f, 0.85f, 0f);
  }
}
