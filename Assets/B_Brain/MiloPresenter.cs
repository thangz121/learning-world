// B_Brain/MiloPresenter.cs — Agent B (W1 Phase 1.1). Milo companion presenter.
//
// Architecture (frozen): GameplayRoot (THIS transform: SpawnPosition,
// CapsuleCollider interaction, IClickTarget, quest/hint subscriptions) vs
// VisualRoot (child: quaternius Worker_Male mesh + Animator + doll face kit).
// Swapping the model later touches ONLY BuildVisual + face offsets; quest,
// routing, rewards and colliders are untouched.
// MonoBehaviour (Unity instantiates): parameterless ctor + public Bind only.
// All voice output goes through the static Milo class (IAudioDirector only).
// No input code, no Camera calls, no `new` services, no provider/Worker refs.
// Null-guarded throughout (batch-safe). C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MiloPresenter : MonoBehaviour, IClickTarget {
  // R5V-2 interaction zone (spec §18-19): greet 2.0-2.2m + facing cone ±60° +
  // rising edge. (DERIVED project parameter for the 1.65m NPC scale.)
  const float GreetDistance = 2.1f;
  const float GreetResetDistance = 2.6f;
  const float GreetFacingDot = 0.5f; // cos(60°): player must face Milo
  const float WaveDuration = 1.6f;

  [Header("Lead-wired placement (A anchor)")]
  // Phase-1 closure: Milo hosts the market stall front (his place; Mia keeps
  // the counter as shopkeeper). Open grass east of the counter, clear of the
  // stall carve, 2.3m conversational distance from Mia so both stage together.
  // R5V-2 composition: Milo hosts his own place EAST on the path (0.0,-0.8),
  // 3.89m from Mia (-3.5,-2.5): two distinct visual anchors, no cross-trigger.
  // (DERIVED project parameter from the 1.65m NPC scale, not a universal law.)
  public Vector3 SpawnPosition = new Vector3(0f, 0f, -0.8f);
  [Header("Lead-wired player reference (Transform only, null-guarded)")]
  public Transform PlayerTarget;

  IGameEventBus _bus;
  IQuestService _quests;
  IHintService _hints;
  readonly List<IDisposable> _subs = new List<IDisposable>();

  // First-talk seam for the onboarding flow (MarketBootstrap wires it; the
  // quest starts on first talk, not at scene build). Fires EXACTLY once, after
  // the normal click feedback. Plain Action (no bus, no strings).
  public Action OnFirstTalk;
  bool _talked;
  bool _questStarted;

  QuestId _activeQuest = new QuestId("w1_mia_apple");
  float _clockSinceProgress;
  bool _greeted;

  // Visual rig (presentation only, never gameplay state).
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
    BuildVisual();
    ApplySpawnPosition();
  }

  void Start() {
    // Lead sets SpawnPosition after AddComponent (post-Awake); re-apply here
    // so the Inspector/wired value wins before the first frame.
    ApplySpawnPosition();
    FaceCameraImmediate();
    // Face setup waits for Start: world transforms read during Awake (inside
    // AddComponent) are stale-identity, which made the surface probe miss.
    // Facing is applied first so the face anchors on the camera side.
    if (_presentation != null) _presentation.SetupFace(_skinForFace, _headBone, transform, _visualForFace);
  }

  void ApplySpawnPosition() {
    transform.position = SpawnPosition;
  }

  // Injection boundary (wired by Lead/GameInstaller). Re-bind safe: old
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
    _subs.Add(_bus.Subscribe<StoryMomentEvent>(OnStoryMoment));
  }

  // IClickTarget entry point for A's router (no input code in this file).
  public void OnClicked() {
    OnMiloClicked();
  }

  // Click entry point for A's router (no input code in this file).
  // Visible click feedback: short wave while the instruction replays.
  // First click additionally opens the story (one-shot hook for Bootstrap).
  public void OnMiloClicked() {
    _waveT = WaveDuration;
    if (_presentation != null) _presentation.PulseExpression(CharacterExpression.Happy, 2f);
    Milo.RepeatInstruction();
    if (!_talked) {
      _talked = true;
      if (OnFirstTalk != null) OnFirstTalk();
    }
  }

  void OnWordSeen(WordSeenEvent e) {
    _clockSinceProgress = 0f;
  }

  void OnQuestStarted(QuestStartedEvent e) {
    _activeQuest = e.QuestId;
    _questStarted = true;
    _clockSinceProgress = 0f;
    _waveT = WaveDuration; // greeting gesture aligns with the opening line
    if (_presentation != null) _presentation.PulseExpression(CharacterExpression.Happy, 3f);
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    _clockSinceProgress = 0f;
    if (e.QuestId.Value != _activeQuest.Value) return;
    if (_animator != null) _animator.SetTrigger("Celebrate");
    // Golden reaction (§11.4): post-quest baseline stays Happy — the completed
    // state is durable and capturable, not a 4s pulse. Transient moments
    // (greet/click) keep short pulses.
    if (_presentation != null) _presentation.SetExpression(CharacterExpression.Happy);
  }

  void OnHintLevel(HintLevelChanged e) {
    if (e.QuestId.Value != _activeQuest.Value) return;
    if (e.Level >= 3) Milo.DemoHint();
    else if (e.Level >= 2) Milo.PointHint();
  }

  // Narrative reaction ownership (golden §11): Milo owns encouragement on
  // wrong choices and a shared wave on correct ones. Quest rules untouched.
  void OnStoryMoment(StoryMomentEvent e) {
    if (_presentation == null) return;
    if (e.Moment == StoryMoment.WrongChoice) {
      Milo.Encourage();
    } else if (e.Moment == StoryMoment.CorrectChoice) {
      _waveT = WaveDuration;
      _presentation.PulseExpression(CharacterExpression.Happy, 2f);
    }
  }

  void Update() {
    float dt = Time.deltaTime;
    _clockSinceProgress += dt;

    if (_hints != null && _questStarted) {
      bool done = false;
      if (_quests != null) done = _quests.GetState(_activeQuest).Completed;
      if (!done) _hints.Tick(_activeQuest, dt, _clockSinceProgress);
    }

    if (PlayerTarget != null) {
      Vector3 toPlayer = PlayerTarget.position - transform.position;
      toPlayer.y = 0f;
      // Face readability: the follow camera sits behind the player, so facing
      // the player turns the face AWAY from the gameplay camera. Face the
      // camera instead (player fallback); greet logic still uses distance.
      Vector3 faceDir = CameraFaceDirection(toPlayer);
      if (faceDir.sqrMagnitude > 0.0001f)
        transform.rotation = Quaternion.LookRotation(faceDir);
      float dist = toPlayer.magnitude;
      if (dist > GreetResetDistance) {
        _greeted = false; // re-arm: leaving the zone allows one future greet
      } else if (!_greeted && dist < GreetDistance && PlayerFacesMilo()) {
        _greeted = true;
        if (_presentation != null) _presentation.PulseExpression(CharacterExpression.Happy, 3f);
        Milo.Greet();
      }
    }
  }

  // R5V-2 facing cone: the PLAYER must face Milo (dot(playerFwd, toMilo) >=
  // cos60°). Walking behind/away never greets; standing inside stays silent
  // (rising edge via _greeted). Null-safe (no player transform = no greet).
  bool PlayerFacesMilo() {
    if (PlayerTarget == null) return false;
    Vector3 toMilo = transform.position - PlayerTarget.position;
    toMilo.y = 0f;
    if (toMilo.sqrMagnitude < 0.0001f) return true;
    Vector3 fwd = PlayerTarget.forward;
    fwd.y = 0f;
    if (fwd.sqrMagnitude < 0.0001f) return false;
    return Vector3.Dot(fwd.normalized, toMilo.normalized) >= GreetFacingDot;
  }

  // Camera-facing direction (Y-only). Falls back to the player direction when
  // no MainCamera exists (batch/headless safe).
  Vector3 CameraFaceDirection(Vector3 toPlayer) {
    Camera cam = Camera.main;
    if (cam == null) return toPlayer;
    Vector3 toCam = cam.transform.position - transform.position;
    toCam.y = 0f;
    if (toCam.sqrMagnitude > 0.0001f) return toCam;
    return toPlayer;
  }

  void FaceCameraImmediate() {
    Camera cam = Camera.main;
    if (cam == null) return;
    Vector3 toCam = cam.transform.position - transform.position;
    toCam.y = 0f;
    if (toCam.sqrMagnitude > 0.0001f)
      transform.rotation = Quaternion.LookRotation(toCam);
  }

  // Secondary gesture layer: procedural arm wave applied AFTER the Animator
  // evaluated (LateUpdate wins for the frame). Restores the Animator-driven
  // pose exactly when the timer lapses, so idle motion is never frozen.
  void LateUpdate() {
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

  void OnDisable() {
    ClearSubs();
  }

  void ClearSubs() {
    foreach (IDisposable s in _subs) {
      if (s != null) s.Dispose();
    }
    _subs.Clear();
  }

  // ---- VisualRoot: real animated mesh + doll face kit -------------------------
  // GameplayRoot (this transform) carries the explicit interaction capsule;
  // the visual child carries mesh + Animator + face and may be swapped freely.
  void BuildVisual() {
    GameObject visualPrefab = Resources.Load<GameObject>("NpcVisuals/MiloVisual");
    if (visualPrefab == null) {
      Debug.LogError("[MiloPresenter] Missing NpcVisuals/MiloVisual prefab; Milo has no body.", this);
      AddInteractionCapsule();
      return;
    }
    GameObject visual = Instantiate(visualPrefab, transform, false);
    visual.name = "MiloVisualRoot";
    // R6 grounding (FINAL POLISH 2026-09-13: the 0.493 lift was historical
    // contamination from the discredited BakeMesh minMapped era. Two
    // independent lines agree it is pure float: (1) low-angle macros show
    // ~0.4m daylight + detached shadow; (2) trusted bone-bind SOLE2 reads
    // sole=0.519 at root 0, i.e. off=0.519 ~= lift, so the TRUE Worker idle
    // sink ~= 0.03. Build-A round (0.05) read SOLE2 ~0.07 + tight contact
    // shadow in f6-17 (planted look, 7cm number): 0.02 lands the sole at
    // ~+0.04 with breathing floor +0.03, safely above penetration. Build-B
    // (0.02) reads contact at SOLE2 ~0.045: final 0.01 lands ~+0.03, floor
    // ~+0.02. Per-rig measured (never copy lifts across rigs blind).
    // Photos decide.
    visual.transform.localPosition = new Vector3(0f, 0.01f, 0f);
    visual.transform.localRotation = Quaternion.identity;
    // Scale fix: the quaternius armature imports at 100x (3.3m tall giant).
    // Half the visual so Milo stands ~1.65m next to the 1.6m player capsule.
    // GameplayRoot (collider/identity) stays at scale 1. Face-kit compensation
    // divides by bone lossyScale, so it adapts to this scale automatically.
    visual.transform.localScale = Vector3.one * 0.5f;

    _animator = visual.GetComponentInChildren<Animator>(true);
    if (_animator == null) {
      Debug.LogError("[MiloPresenter] MiloVisual has no Animator; idle/celebrate clips will not play.", this);
    }

    SkinnedMeshRenderer skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
    if (skin != null) {
      // Readability adaptation for preschoolers (documented, reversible):
      // quaternius Face/Skin run near-white/near-black, which reads as a
      // silhouette at gameplay distance. Tint instance copies only (the
      // imported sub-assets stay pristine): orange vest (Milo identity),
      // warm tan face, warm mid-brown skin. Shared 2E helper (was a local
      // copy identical to Mia's — behavior unchanged).
      CharacterPresentation.TintSharedMaterials(skin, "Vest", new Color(1f, 0.55f, 0.12f));
      // R5-A material test (2026-09-13): Face 0.45 -> 0.25, Skin 0.50 -> 0.30
      // (candidate values; macro + gameplay photos decide).
      // R5c: deepen Face tan one step (brows persisted => diffuse sculpt).
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
      Debug.LogWarning("[MiloPresenter] Head bone not found; face kit attached to visual root.", this);
    }
    // Reusable presentation layer: surface-anchored face, blink, breathing,
    // attention glances, expressions (SharedKernel, no gameplay coupling).
    // Face geometry setup is deferred to Start (see comment there).
    _presentation = gameObject.AddComponent<CharacterPresentation>();
    // Final polish footwear (shared helper, Foot.L/R proven on all rigs): the
    // pack ships no shoe geometry, so feet read as bare stubs without caps.
    _presentation.QueueShoe(_footL, "ShoeL");
    _presentation.QueueShoe(_footR, "ShoeR");
    _skinForFace = skin;
    _visualForFace = visual.transform;
    AddInteractionCapsule();
  }

  void AddInteractionCapsule() {
    CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
    col.radius = 0.4f;
    col.height = 1.7f;
    col.center = new Vector3(0f, 0.85f, 0f);
  }

}
