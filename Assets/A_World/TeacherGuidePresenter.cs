// A_World/TeacherGuidePresenter.cs — 3D Guide NPC for Activity Arenas
// (Listening Garden, Speaking Stage, etc.).
// Walks / stands beside the demonstration items, points at them with procedural
// arm IK gesture, looks at the player, speaks names & instructions, and celebrates.
// Null-guarded throughout. C# 9.0 only.
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class TeacherGuidePresenter : MonoBehaviour {
  public Vector3 HomePosition = Vector3.zero;
  public Transform LookTarget;

  Animator _animator;
  Transform _headBone;
  Transform _armR;
  Transform _armL;
  Quaternion _armRBase = Quaternion.identity;
  Quaternion _armLBase = Quaternion.identity;
  CharacterPresentation _presentation;

  bool _isPointing;
  float _pointWeight;
  bool _isListening;
  float _listenWeight;
  Vector3 _pointTargetWorld;
  float _celebrateT;

  public void Setup(Vector3 homePos) {
    HomePosition = homePos;
    transform.localPosition = homePos;
    BuildVisual();
  }

  void BuildVisual() {
    // Uses the beautiful Quaternius Worker_Female visual rig (same as Mia/Tess)
    GameObject visualPrefab = Resources.Load<GameObject>("NpcVisuals/MiaVisual");
    if (visualPrefab == null) visualPrefab = Resources.Load<GameObject>("NpcVisuals/TessVisual");
    if (visualPrefab == null) visualPrefab = Resources.Load<GameObject>("NpcVisuals/MiloVisual");
    if (visualPrefab == null) return;

    GameObject visual = Instantiate(visualPrefab, transform, false);
    visual.name = "GuideVisualRoot";
    visual.transform.localPosition = new Vector3(0f, 0.01f, 0f);
    visual.transform.localRotation = Quaternion.identity;
    visual.transform.localScale = Vector3.one * 0.5f;

    _animator = visual.GetComponentInChildren<Animator>(true);

    SkinnedMeshRenderer skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
    if (skin != null) {
      // Elegant lavender/plum cardigan teacher outfit for a warm, welcoming presence
      CharacterPresentation.TintSharedMaterials(skin, "Vest", new Color(0.35f, 0.65f, 0.85f));
      CharacterPresentation.TintSharedMaterials(skin, "Shirt", new Color(0.98f, 0.96f, 0.92f));
      CharacterPresentation.TintSharedMaterials(skin, "Face", new Color(0.95f, 0.74f, 0.58f), 0.25f);
      CharacterPresentation.TintSharedMaterials(skin, "Skin", new Color(0.50f, 0.35f, 0.25f), 0.3f);
    }

    Transform footL = null;
    Transform footR = null;
    if (skin != null && skin.bones != null) {
      foreach (Transform bone in skin.bones) {
        if (bone == null) continue;
        if (_headBone == null && bone.name == "Head") _headBone = bone;
        if (_armR == null && (bone.name == "UpperArm.R" || bone.name == "Shoulder.R")) _armR = bone;
        if (_armL == null && (bone.name == "UpperArm.L" || bone.name == "Shoulder.L")) _armL = bone;
        if (footL == null && bone.name == "Foot.L") footL = bone;
        if (footR == null && bone.name == "Foot.R") footR = bone;
      }
    }

    _presentation = gameObject.AddComponent<CharacterPresentation>();
    if (footL != null) _presentation.QueueShoe(footL, "ShoeL");
    if (footR != null) _presentation.QueueShoe(footR, "ShoeR");
    _presentation.SetupFace(skin, _headBone, transform, visual.transform);
  }

  void Update() {
    float dt = Time.deltaTime;
    if (dt <= 0f) return;

    // Face toward target or camera
    Vector3 faceDir = Vector3.forward;
    if (LookTarget != null) {
      faceDir = LookTarget.position - transform.position;
      faceDir.y = 0f;
    } else {
      Camera cam = Camera.main;
      if (cam != null) {
        faceDir = cam.transform.position - transform.position;
        faceDir.y = 0f;
      }
    }
    if (faceDir.sqrMagnitude > 0.001f) {
      Quaternion targetRot = Quaternion.LookRotation(faceDir);
      transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * 4f);
    }

    // Smooth arm pointing weight
    float targetWeight = _isPointing ? 1f : 0f;
    _pointWeight = Mathf.MoveTowards(_pointWeight, targetWeight, dt * 3.5f);

    float targetListenWeight = _isListening ? 1f : 0f;
    _listenWeight = Mathf.MoveTowards(_listenWeight, targetListenWeight, dt * 3.5f);

    if (_celebrateT > 0f) {
      _celebrateT -= dt;
    }
  }

  void LateUpdate() {
    // Procedural arm point gesture (Right Arm)
    if (_armR != null && _pointWeight > 0.001f) {
      if (_armRBase == Quaternion.identity) _armRBase = _armR.localRotation;
      
      Vector3 toTarget = _pointTargetWorld - _armR.position;
      toTarget.y += 0.3f;
      if (toTarget.sqrMagnitude > 0.01f) {
        // Point smoothly towards target with small natural gentle breathing swing
        float sway = Mathf.Sin(Time.time * 3f) * 4f;
        Quaternion pointRot = Quaternion.Euler(-35f + sway, 45f, -65f);
        _armR.localRotation = Quaternion.Slerp(_armRBase, _armRBase * pointRot, _pointWeight);
      }
    } else if (_armR != null && _armRBase != Quaternion.identity && _listenWeight <= 0.001f) {
      _armR.localRotation = _armRBase;
    }

    // Procedural attentive listening pose (hands cupped near chest/ear)
    if (_armL != null && _listenWeight > 0.001f) {
      if (_armLBase == Quaternion.identity) _armLBase = _armL.localRotation;
      Quaternion listenRot = Quaternion.Euler(-45f, -30f, 40f);
      _armL.localRotation = Quaternion.Slerp(_armLBase, _armLBase * listenRot, _listenWeight);
    } else if (_armL != null && _armLBase != Quaternion.identity) {
      _armL.localRotation = _armLBase;
    }
  }

  public void PointAt(Vector3 targetWorld) {
    _isPointing = true;
    _isListening = false;
    _pointTargetWorld = targetWorld;
    if (_presentation != null) _presentation.PulseExpression(CharacterExpression.Curious, 2f);
  }

  public void StopPointing() {
    _isPointing = false;
  }

  public void PoseListen() {
    _isPointing = false;
    _isListening = true;
    if (_presentation != null) _presentation.PulseExpression(CharacterExpression.Curious, 2.5f);
  }

  public void StopListening() {
    _isListening = false;
  }

  public void Celebrate() {
    _celebrateT = 2.0f;
    _isPointing = false;
    _isListening = false;
    if (_animator != null) {
      try { _animator.SetTrigger("Celebrate"); } catch (Exception) { }
    }
    if (_presentation != null) {
      _presentation.PulseExpression(CharacterExpression.Happy, 2.5f);
      _presentation.PlayHop();
    }
  }
}
