// A_World/PlayerVisual.cs — Agent A (World & Visual). Player presentation root.
//
// Architecture: PlayerRoot (THIS GameObject: capsule collider, NavMeshAgent,
// ClickToMove, interaction) vs VisualRoot (child: humanoid model + Animator +
// CharacterPresentation face kit). Swapping the avatar later touches ONLY this
// file + the PlayerVisual prefab (future gender/skin phases), never movement,
// collision, quest or camera code. The capsule collider stays for physics; its
// RENDERER is hidden so no placeholder capsule is ever visible.
// MonoBehaviour + Bind-free (needs no services). Null-guarded, C# 9.0 only.
using System;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class PlayerVisual : MonoBehaviour {
  const float VisualScale = 0.5f; // quaternius 100x armature -> ~1.6m character
  // W1 grounding (W1TOUR GROUND 2026-09-12 live probe + GndDiag2): the
  // quaternius clips pose the feet ~0.32m below the prefab origin at game
  // scale, and the Animator always plays a clip (the rest bind pose is never
  // rendered), so the VisualRoot carries a permanent lift. localPosition is
  // in PARENT space: player root scale is 0.8, so 0.300 local = 0.240 world.
  // R4 (P1Survey p2-gnd-player 2026-09-13): probe read minMapped=+0.076
  // (photo-confirmed 7cm float + shadow gap under shoes), so the 0.395 lift
  // (0.316 world) was cut by exactly the measured float (0.095 local).
  // CharacterPresentation captures this base in SetupFace (breathing/hop ride
  // on top), so the lift composes with all presentation motion.
  const float GroundLiftLocal = 0.300f;

  Animator _animator;
  NavMeshAgent _agent;
  CharacterPresentation _presentation;
  SkinnedMeshRenderer _skinForFace;
  Transform _headForFace;
  Transform _footLForShoe;
  Transform _footRForShoe;
  Transform _visualForFace;
  int _movingHash;

  IGameEventBus _bus;
  IDisposable _questSub;

  void Awake() {
    _agent = GetComponent<NavMeshAgent>();
    HideCapsuleRenderer();
    BuildVisual();
    _movingHash = Animator.StringToHash("Moving");
  }

  // Injection boundary (MarketBuilder wires the bus; visual reacts to the
  // quest-complete moment with a celebratory hop — presentation only).
  public void Bind(IGameEventBus bus) {
    if (_questSub != null) { _questSub.Dispose(); _questSub = null; }
    _bus = bus;
    if (_bus != null) _questSub = _bus.Subscribe<QuestCompletedEvent>(OnQuestCompleted);
  }

  void OnDisable() {
    if (_questSub != null) { _questSub.Dispose(); _questSub = null; }
  }

  void OnQuestCompleted(QuestCompletedEvent e) {
    if (_presentation != null) _presentation.PlayHop();
  }

  void Start() {
    // Face setup waits for Start: world transforms read during Awake are
    // stale-identity, which made the surface probe miss.
    if (_presentation != null) _presentation.SetupFace(_skinForFace, _headForFace, transform, _visualForFace);
  }

  void Update() {
    if (_animator == null || _agent == null) return;
    bool moving = _agent.velocity.sqrMagnitude > 0.25f;
    _animator.SetBool(_movingHash, moving);
  }

  // Future avatar-swap seam: expression/gesture API for dialogue/story code.
  public void SetExpression(CharacterExpression e) {
    if (_presentation != null) _presentation.SetExpression(e);
  }

  public void PulseExpression(CharacterExpression e, float seconds) {
    if (_presentation != null) _presentation.PulseExpression(e, seconds);
  }

  void HideCapsuleRenderer() {
    Renderer r = GetComponent<Renderer>();
    if (r != null) r.enabled = false; // collider untouched: physics identical
  }

  void BuildVisual() {
    GameObject visualPrefab = Resources.Load<GameObject>("PlayerVisuals/PlayerVisual");
    if (visualPrefab == null) {
      Debug.LogError("[PlayerVisual] Missing PlayerVisuals/PlayerVisual prefab; player has no body.", this);
      return;
    }
    GameObject visual = Instantiate(visualPrefab, transform, false);
    visual.name = "PlayerVisualRoot";
    visual.transform.localPosition = new Vector3(0f, GroundLiftLocal, 0f);
    visual.transform.localRotation = Quaternion.identity;
    visual.transform.localScale = Vector3.one * VisualScale;

    _animator = visual.GetComponentInChildren<Animator>(true);
    if (_animator == null) {
      Debug.LogError("[PlayerVisual] PlayerVisual has no Animator; idle/walk will not play.", this);
    }

    SkinnedMeshRenderer skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
    Transform headBone = null;
    if (skin != null) {
      // Player identity: blue shirt (distinct from Milo's orange / Mia's coral),
      // warm tan face, warm mid-brown skin. Instance copies only.
      TryTint(skin, "Shirt", new Color(0.25f, 0.5f, 0.95f));
      TryTint(skin, "Face", new Color(1f, 0.82f, 0.64f), 0.45f);
      TryTint(skin, "Skin", new Color(0.42f, 0.27f, 0.17f), 0.5f);
      if (skin.bones != null) {
        foreach (Transform bone in skin.bones) {
          if (bone == null) continue;
          if (headBone == null && bone.name == "Head") headBone = bone;
          if (_footLForShoe == null && bone.name == "Foot.L") _footLForShoe = bone;
          if (_footRForShoe == null && bone.name == "Foot.R") _footRForShoe = bone;
        }
      }
    }
    if (headBone == null) {
      Debug.LogWarning("[PlayerVisual] Head bone not found; face kit skipped.", this);
      return;
    }
    _presentation = gameObject.AddComponent<CharacterPresentation>();
    // Final polish footwear (shared helper, Foot.L/R proven on all rigs).
    _presentation.QueueShoe(_footLForShoe, "ShoeL");
    _presentation.QueueShoe(_footRForShoe, "ShoeR");
    _skinForFace = skin;
    _headForFace = headBone;
    _visualForFace = visual.transform;
  }

  // Final polish: optional smoothness override (skin 0.5 soft sheen vs matte
  // cloth at import 0.31); negative keeps imported. Metallic pinned to 0.
  static void TryTint(SkinnedMeshRenderer skin, string nameFragment, Color color, float smoothness = -1f) {
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
      if (smoothness >= 0f && copy.HasProperty("_Smoothness")) copy.SetFloat("_Smoothness", smoothness);
      if (copy.HasProperty("_Metallic")) copy.SetFloat("_Metallic", 0f);
      copy.name = m.name + "_Tinted";
      mats[i] = copy;
      changed = true;
    }
    if (changed) skin.sharedMaterials = mats;
  }
}
