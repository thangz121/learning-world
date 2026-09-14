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
  // R6 grounding (FINAL POLISH 2026-09-13): the 0.340 lift descends from the
  // same discredited BakeMesh minMapped era as the NPC 0.493. Trusted
  // bone-bind SOLE2 reads sole=0.324 at rootY 0.030 (off=0.294 ~= the 0.272
  // world lift => TRUE Casual_Male idle sink ~= 0.02), and wide shots show a
  // ~10cm daylight gap + detached shadow. Build-A photo round (0.06 local)
  // still showed ~6cm float + detached shadow in the f6-23 zoom: predicted
  // idle sole = raw(~+0.01 world) + lift, so 0.015 local = 0.012 world lands
  // the sole at ~+0.02 (contact shadow sells contact, breathing floor +0.004
  // never penetrates). Build-B (0.015) reads planted in profile but keeps a
  // 3-6cm number + thin daylight in 3/4 stills: final 0.005 local = 0.004
  // world lands ~+0.015, breathing floor ~+0.008, still never sinks on the
  // flat lawn/path. Build-C macros decide.
  const float GroundLiftLocal = 0.005f;

  Animator _animator;
  NavMeshAgent _agent;
  CharacterPresentation _presentation;
  SkinnedMeshRenderer _skinForFace;
  Transform _headForFace;
  Transform _footLForShoe;
  Transform _footRForShoe;
  Transform _visualForFace;
  // R6 gait compensation (Build-A PROVEN, sign flipped): the old -0.03 assumed
  // the walk clip poses feet HIGHER than idle (BakeMesh-era claim). Build-A
  // photos prove the opposite: f6-22 mid-stride support planted with total
  // walk lift 0.024 world while f6-23 idle floated ~6cm on 0.048 world — i.e.
  // the walk clip CROUCHES ~3.6cm below idle (bent support knee, visible).
  // Proven-planted walk total = 0.024 world = 0.03 local; with the 0.015 base
  // the comp was +0.015. Build-B walk-mid (pot-occluded, inconclusive) plus
  // the final base cut to 0.005 keeps the proven total: comp +0.025
  // (0.005+0.025 = 0.03 local = 0.024 world, exactly the f6-22 proof value).
  // If the Build-C walk-mid floats, cut toward 0; if stance sinks, raise base.
  const float WalkLiftLocal = 0.025f;
  int _movingHash;

  IGameEventBus _bus;
  IDisposable _questSub;

  // R5j: the carried-apple anchor rides the FIST bone (reads as "held").
  // (MarketBuilder's old root-child HandAnchor floated beside the head —
  // R5i photo proof.) Null until BuildVisual finds Fist.R; Lead falls back.
  public Transform HandBone { get; private set; }

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
    if (_presentation != null) _presentation.SetLiftOffset(moving ? WalkLiftLocal : 0f);
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
      // R5-A material test (2026-09-13): Face 0.45 -> 0.25, Skin 0.50 -> 0.30
      // to kill the glossy brow/cheek separation (specular on sculpt ridges
      // reading as detached white ellipses). Candidate values only: macro +
      // gameplay photos decide. Revert/tune if the face goes chalky or flat.
      // R5c (brows persisted after R5-A => diffuse sculpt, not specular):
      // deepen Face tan one step to integrate the lid/brow ridges. If the
      // face loses identity or goes muddy in photos, revert this line only.
      TryTint(skin, "Face", new Color(0.93f, 0.70f, 0.52f), 0.25f);
      TryTint(skin, "Skin", new Color(0.42f, 0.27f, 0.17f), 0.3f);
      if (skin.bones != null) {
        foreach (Transform bone in skin.bones) {
          if (bone == null) continue;
          if (headBone == null && bone.name == "Head") headBone = bone;
          if (_footLForShoe == null && bone.name == "Foot.L") _footLForShoe = bone;
          if (_footRForShoe == null && bone.name == "Foot.R") _footRForShoe = bone;
          if (HandBone == null && bone.name == "Fist.R") HandBone = bone;
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
