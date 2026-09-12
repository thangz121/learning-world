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
  const float GreetDistance = 3f;
  const float WaveDuration = 1.6f;

  [Header("Lead-wired placement (A anchor)")]
  public Vector3 SpawnPosition = new Vector3(2.5f, 0f, 1.5f);
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
      if (!_greeted && toPlayer.magnitude < GreetDistance) {
        _greeted = true;
        if (_presentation != null) _presentation.PulseExpression(CharacterExpression.Happy, 3f);
        Milo.Greet();
      }
    }
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
    // W1 grounding (W1TOUR GROUND 2026-09-12: live Idle minVert -0.493 with
    // root at 0; GndDiag2 agrees): clips sink the skeleton rigidly ~0.49m, and
    // a clip always plays, so the VisualRoot carries a permanent lift.
    // localPosition is in PARENT space (gameplay root scale 1): 0.493 local.
    visual.transform.localPosition = new Vector3(0f, 0.493f, 0f);
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
      // warm tan face, warm mid-brown skin.
      TintSharedMaterials(skin, "Vest", new Color(1f, 0.55f, 0.12f));
      TintSharedMaterials(skin, "Face", new Color(1f, 0.82f, 0.64f));
      TintSharedMaterials(skin, "Skin", new Color(0.42f, 0.27f, 0.17f));
    }
    if (skin != null && skin.bones != null) {
      foreach (Transform bone in skin.bones) {
        if (bone == null) continue;
        if (_headBone == null && bone.name == "Head") _headBone = bone;
        if (_waveBone == null && (bone.name == "UpperArm.R" || bone.name == "Shoulder.R"))
          _waveBone = bone;
      }
    }
    if (_headBone == null) {
      Debug.LogWarning("[MiloPresenter] Head bone not found; face kit attached to visual root.", this);
    }
    // Reusable presentation layer: surface-anchored face, blink, breathing,
    // attention glances, expressions (SharedKernel, no gameplay coupling).
    // Face geometry setup is deferred to Start (see comment there).
    _presentation = gameObject.AddComponent<CharacterPresentation>();
    _skinForFace = skin;
    _visualForFace = visual.transform;
    AddInteractionCapsule();
  }

  // Instance-only material tint (imported sub-assets stay pristine).
  static void TintSharedMaterials(SkinnedMeshRenderer skin, string nameFragment, Color color) {
    if (skin == null) return;
    Material[] mats = skin.sharedMaterials;
    bool changed = false;
    for (int i = 0; i < mats.Length; i++) {
      Material m = mats[i];
      if (m == null || m.name == null) continue;
      if (m.name.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) < 0) continue;
      Shader s = m.shader != null ? m.shader : Shader.Find("Universal Render Pipeline/Lit");
      if (s == null) continue;
      Material copy = new Material(s);
      copy.CopyPropertiesFromMaterial(m);
      if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", color);
      else if (copy.HasProperty("_Color")) copy.SetColor("_Color", color);
      copy.name = m.name + "_Tinted";
      mats[i] = copy;
      changed = true;
    }
    if (changed) skin.sharedMaterials = mats;
  }

  void AddInteractionCapsule() {
    CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
    col.radius = 0.4f;
    col.height = 1.7f;
    col.center = new Vector3(0f, 0.85f, 0f);
  }

}
