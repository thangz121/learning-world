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
  }

  // IClickTarget entry point for A's router (no input code in this file).
  public void OnClicked() {
    OnMiaClicked();
  }

  // Click entry point for A's router (no input code in this file).
  // Visible friendly response: short wave on every click.
  public void OnMiaClicked() {
    _waveT = WaveDuration;
    if (_carryingApple) {
      if (_quests == null) return;
      _quests.ReportAction(PlayerAction.Bring, _appleWord);
      _carryingApple = false;
      if (_animator != null) _animator.SetTrigger("PickUp"); // bend receiving the apple
    } else {
      if (_hints != null) _hints.ReportWrong(_activeQuest);
      Milo.Encourage();
    }
  }

  void OnWordSeen(WordSeenEvent e) {
    if (e.WordId.Value == _appleWord.Value) _carryingApple = true;
  }

  void OnQuestStarted(QuestStartedEvent e) {
    _activeQuest = e.QuestId;
    _carryingApple = false;
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    if (e.QuestId.Value != _activeQuest.Value) return;
    if (_animator != null) _animator.SetTrigger("Celebrate");
  }

  void Update() {
    // Shopkeeper greeting posture: gently face the player like Milo does,
    // so the doll face stays readable from the gameplay camera. Y-only,
    // smoothed (no snap, no spin).
    if (PlayerTarget != null) {
      Vector3 toPlayer = PlayerTarget.position - transform.position;
      toPlayer.y = 0f;
      if (toPlayer.sqrMagnitude > 0.0001f) {
        Quaternion look = Quaternion.LookRotation(toPlayer);
        float t = 1f - Mathf.Exp(-3f * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, t);
      }
    }
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
    visual.transform.localPosition = Vector3.zero;
    visual.transform.localRotation = Quaternion.identity;
    visual.transform.localScale = Vector3.one;

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
    BuildFace(_headBone != null ? _headBone : visual.transform);
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

  // Doll face kit (accessory geometry, not the character): dark eyes + white
  // glints + smile, parented to the Head bone so idle animation carries them.
  // URP/Lit materials via Paint (never default-white). Offsets tuned for the
  // quaternius head size; verified against Game-view screenshots.
  void BuildFace(Transform parent) {
    // NOTE: the quaternius head is LARGE (~0.5m wide chibi skull) with its own
    // white slit eyes modeled near z≈0.28-0.30. Doll pupils/smile sit PROUD of
    // any plausible surface (z≈0.33) so they can never z-fight or bury.
    AddFacePart("MiaEyeL", parent, new Vector3(-0.11f, 0.05f, 0.33f),
      new Vector3(0.12f, 0.15f, 0.08f), Color.black);
    AddFacePart("MiaEyeR", parent, new Vector3(0.11f, 0.05f, 0.33f),
      new Vector3(0.12f, 0.15f, 0.08f), Color.black);
    AddFacePart("MiaSmile", parent, new Vector3(0f, -0.09f, 0.33f),
      new Vector3(0.14f, 0.08f, 0.07f), new Color(0.55f, 0.2f, 0.2f));
  }

  void AddFacePart(string partName, Transform parent, Vector3 localPos, Vector3 localScale, Color color) {
    if (parent == null) return;
    GameObject part = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    if (part == null) return;
    part.name = partName;
    part.transform.SetParent(parent, false);
    // Rig bones can carry huge authoring scales (quaternius Head ≈100x, which
    // is cancelled for skinning by bindposes but NOT for regular children):
    // divide the intended model-space offset/size by the parent world scale.
    Vector3 ps = parent.lossyScale;
    if (Mathf.Abs(ps.x) > 0.0001f && Mathf.Abs(ps.y) > 0.0001f && Mathf.Abs(ps.z) > 0.0001f) {
      part.transform.localPosition = new Vector3(localPos.x / ps.x, localPos.y / ps.y, localPos.z / ps.z);
      part.transform.localScale = new Vector3(localScale.x / ps.x, localScale.y / ps.y, localScale.z / ps.z);
    } else {
      part.transform.localPosition = localPos;
      part.transform.localScale = localScale;
    }
    Paint(part, color);
  }

  // URP/Lit construction identical to world geometry (never null-shader
  // magenta, never default-white): explicit shader + _BaseColor, with a
  // Built-in Standard fallback that cannot exist alongside URP in practice.
  static void Paint(GameObject go, Color color) {
    if (go == null) return;
    Renderer r = go.GetComponent<Renderer>();
    if (r == null) return;
    Shader lit = Shader.Find("Universal Render Pipeline/Lit");
    if (lit != null) {
      Material mat = new Material(lit);
      mat.SetColor("_BaseColor", color);
      r.sharedMaterial = mat;
      return;
    }
    Shader standard = Shader.Find("Standard");
    if (standard != null) {
      Material mat = new Material(standard);
      mat.color = color;
      r.sharedMaterial = mat;
    }
  }
}
