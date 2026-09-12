// B_Brain/MiaPresenter.cs — Agent B (W1 Phase 1.1). Warm shopkeeper Mia presenter.
//
// Architecture (frozen): GameplayRoot (THIS transform: SpawnPosition,
// CapsuleCollider interaction, IClickTarget, carrying state, quest wiring) vs
// VisualRoot (child: quaternius Worker_Female mesh + Animator + doll face
// kit). Swapping the model later touches ONLY BuildVisual + face offsets;
// quest, routing, rewards and colliders are untouched.
// MonoBehaviour (Unity instantiates): parameterless ctor + public Bind only.
// Flow: apple WordSeen arms carryingApple; clicking Mia while carrying reports
// the explicit Bring action (the ONLY path that advances bring_apple); clicking
// Mia empty-handed is a gentle correction (hint wrong-count + Milo encouragement,
// never a fail state). No Camera calls, no `new` services, no provider/Worker
// refs. Typed IDs only. Null-guarded throughout (batch-safe). C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MiaPresenter : MonoBehaviour, IClickTarget {
  const float WaveDuration = 1.4f;

  [Header("Lead-wired placement")]
  public Vector3 SpawnPosition = new Vector3(-3.5f, 0f, -2.5f);
  [Header("Lead-wired player reference (Transform only, null-guarded)")]
  public Transform PlayerTarget;

  IGameEventBus _bus;
  IQuestService _quests;
  IHintService _hints;
  readonly List<IDisposable> _subs = new List<IDisposable>();

  QuestId _activeQuest = new QuestId("w1_mia_apple");
  readonly WordId _appleWord = new WordId("apple");
  bool _carryingApple;

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
    // so the wired value wins before the first frame.
    ApplySpawnPosition();
    FaceCameraImmediate();
    // Face setup waits for Start: world transforms read during Awake (inside
    // AddComponent) are stale-identity, which made the surface probe miss.
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
    _subs.Add(_bus.Subscribe<StoryMomentEvent>(OnStoryMoment));
  }

  // IClickTarget entry point for A's router (no input code in this file).
  public void OnClicked() {
    OnMiaClicked();
  }

  // Click entry point for A's router (no input code in this file).
  // Visible friendly response: short wave on every click. Post-completion
  // clicks are inert (narrative context: no more wrongs after the quest).
  public void OnMiaClicked() {
    _waveT = WaveDuration;
    if (_quests != null && _quests.GetState(_activeQuest).Completed) return;
    if (_carryingApple) {
      if (_quests == null) return;
      _quests.ReportAction(PlayerAction.Bring, _appleWord);
      _carryingApple = false;
      if (_presentation != null) _presentation.PulseExpression(CharacterExpression.Happy, 3f);
      if (_animator != null) _animator.SetTrigger("PickUp"); // bend receiving the apple
      if (_bus != null) _bus.Publish(new StoryMomentEvent(StoryMoment.CorrectChoice, DateTime.UtcNow));
    } else {
      if (_hints != null) _hints.ReportWrong(_activeQuest);
      if (_bus != null) _bus.Publish(new StoryMomentEvent(StoryMoment.WrongChoice, DateTime.UtcNow));
    }
  }

  void OnWordSeen(WordSeenEvent e) {
    if (e.WordId.Value == _appleWord.Value) {
      _carryingApple = true;
      if (_presentation != null) _presentation.PulseExpression(CharacterExpression.Happy, 2.5f);
    }
  }

  void OnQuestStarted(QuestStartedEvent e) {
    _activeQuest = e.QuestId;
    _carryingApple = false;
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    if (e.QuestId.Value != _activeQuest.Value) return;
    // Golden reaction (§11.4): durable Happy baseline after the completed quest.
    if (_presentation != null) _presentation.SetExpression(CharacterExpression.Happy);
    if (_animator != null) _animator.SetTrigger("Celebrate");
  }

  // Narrative reaction ownership (golden §11): Mia owns the sad response to
  // wrong choices (child-friendly "oops", retry preserved). Encouragement
  // voice belongs to Milo (he subscribes WrongChoice himself).
  void OnStoryMoment(StoryMomentEvent e) {
    if (_presentation == null) return;
    if (e.Moment == StoryMoment.WrongChoice) {
      _presentation.PulseExpression(CharacterExpression.Sad, 2.5f);
    } else if (e.Moment == StoryMoment.CorrectChoice) {
      _presentation.PulseExpression(CharacterExpression.Happy, 3f);
    }
  }

  void Update() {
    // Shopkeeper greeting posture: face the gameplay camera (not the player:
    // the follow camera sits behind the player, so player-facing turns the
    // doll face AWAY from the viewer). Y-only, smoothed (no snap, no spin).
    Camera cam = Camera.main;
    Vector3 faceDir;
    if (cam != null) {
      faceDir = cam.transform.position - transform.position;
    } else if (PlayerTarget != null) {
      faceDir = PlayerTarget.position - transform.position;
    } else {
      return;
    }
    faceDir.y = 0f;
    if (faceDir.sqrMagnitude > 0.0001f) {
      Quaternion look = Quaternion.LookRotation(faceDir);
      float t = 1f - Mathf.Exp(-3f * Time.deltaTime);
      transform.rotation = Quaternion.Slerp(transform.rotation, look, t);
    }
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
    GameObject visualPrefab = Resources.Load<GameObject>("NpcVisuals/MiaVisual");
    if (visualPrefab == null) {
      Debug.LogError("[MiaPresenter] Missing NpcVisuals/MiaVisual prefab; Mia has no body.", this);
      AddInteractionCapsule();
      return;
    }
    GameObject visual = Instantiate(visualPrefab, transform, false);
    visual.name = "MiaVisualRoot";
    // W1 grounding: same rig/offset as Milo (W1TOUR GROUND 2026-09-12 live
    // Idle minVert -0.493, root at 0). Parent space, scale 1 -> 0.493 local.
    visual.transform.localPosition = new Vector3(0f, 0.493f, 0f);
    visual.transform.localRotation = Quaternion.identity;
    // Scale fix: the quaternius armature imports at 100x (3.4m tall giant).
    // Half the visual so Mia stands ~1.7m next to the 1.6m player capsule.
    // GameplayRoot (collider/identity) stays at scale 1. Face-kit compensation
    // divides by bone lossyScale, so it adapts to this scale automatically.
    visual.transform.localScale = Vector3.one * 0.5f;

    _animator = visual.GetComponentInChildren<Animator>(true);
    if (_animator == null) {
      Debug.LogError("[MiaPresenter] MiaVisual has no Animator; idle/celebrate clips will not play.", this);
    }

    SkinnedMeshRenderer skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
    if (skin != null) {
      // Readability adaptation for preschoolers (documented, reversible):
      // instance copies only (imported sub-assets stay pristine): coral-pink
      // vest (Mia identity, distinct from Milo's orange), warm tan face,
      // warm mid-brown skin instead of the near-black artist default.
      TintSharedMaterials(skin, "Vest", new Color(0.95f, 0.45f, 0.4f));
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
      Debug.LogWarning("[MiaPresenter] Head bone not found; face kit attached to visual root.", this);
    }
    // Reusable presentation layer (face geometry setup deferred to Start).
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
