// _SharedKernel/CharacterPresentation.cs — Lead owns. Reusable character
// presentation layer (W1 visual polish): surface-anchored doll face kit,
// blinking, breathing, attention glances, and a minimal expression API that
// future dialogue/story code can drive without knowing the implementation:
//   SetExpression(...) / PulseExpression(...) / LookAt(...) / BlinkNow()
// Used by Milo/Mia presenters (B) and the player visual (A). Presentation
// ONLY: no gameplay state, no events, no Camera/input calls, no services.
// Face placement is geometric, not guessed: the visible head surface is
// measured per height band from Head-dominant bind vertices via
// (bone x bindpose) with a robust p95, so pupils seat on the eye pane
// instead of the nose-tip plane (the old global max floated them in side
// view). Null-guarded throughout (batch-safe). C# 9.0 only.
using System;
using UnityEngine;

public enum CharacterExpression { Neutral, Happy, Curious, Surprised, Concerned, Sad }

[DisallowMultipleComponent]
public sealed class CharacterPresentation : MonoBehaviour {
  const float Proud = 0.02f;    // pupils float this far off the surface (no z-fight)

  Transform _headBone;
  Transform _visualRoot;
  Transform _anchorSpace;
  SkinnedMeshRenderer _skin;
  bool _pendingBuild;
  int _buildFrame;
  Vector3 _visualBasePos;
  Quaternion _visualBaseRot;
  bool _hasVisualBase;

  GameObject _eyeL;
  GameObject _eyeR;
  Vector3 _eyeBaseScaleL = Vector3.one;
  Vector3 _eyeBaseScaleR = Vector3.one;
  GameObject _mouthSmile;
  GameObject _mouthFlat;
  GameObject _mouthOpen;
  GameObject _mouthFrown;
  Vector3 _smileBase = Vector3.one;
  float _hopT;
  const float HopDuration = 0.45f;
  const float HopHeight = 0.22f;

  CharacterExpression _baseline = CharacterExpression.Neutral;
  CharacterExpression _shown = CharacterExpression.Neutral;
  float _eyeK = 1f;
  float _pulseT;

  float _blinkT;
  float _blinkPhase = -1f; // <0 idle, else 0..1 across the blink
  float _breathPhase;
  float _attT;             // countdown to next glance
  float _attHold;          // remaining hold time of the current glance
  float _attYaw;
  float _attYawVel;

  // One-time setup: stores references; the face is built on the 2nd Update
  // frame, when world transforms are guaranteed live (Awake/Start reads
  // during AddComponent-instantiation are stale-identity).
  public void SetupFace(SkinnedMeshRenderer skin, Transform headBone, Transform anchorSpace, Transform visualRoot) {
    _skin = skin;
    _headBone = headBone;
    _anchorSpace = anchorSpace;
    _visualRoot = visualRoot;
    if (_visualRoot != null) {
      _visualBasePos = _visualRoot.localPosition;
      _visualBaseRot = _visualRoot.localRotation;
      _hasVisualBase = true;
    }
    _pendingBuild = true;
    _buildFrame = 0;
  }
  void BuildFaceNow() {
    if (_headBone == null || _anchorSpace == null) {
      Debug.LogWarning("[CharacterPresentation] SetupFace missing head/anchor; face skipped.", this);
      return;
    }
    Vector3 fwd = _anchorSpace.forward;
    fwd.y = 0f;
    if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
    fwd.Normalize();
    // Bone-anchored face plane: skull center from Head/Head_end midpoint,
    // front surface at a fraction of skull height along the facing. Pure
    // world-space transform math: no BakeMesh, no colliders, no raycasts,
    // no dependence on mesh import details or physics timing.
    Transform headEnd = FindChildDeep(_headBone, "Head_end");
    Vector3 c = headEnd != null
      ? (_headBone.position + headEnd.position) * 0.5f
      : _headBone.position + Vector3.up * 0.1f;
    float s = headEnd != null
      ? Vector3.Distance(_headBone.position, headEnd.position)
      : 0.3f;
    s = Mathf.Clamp(s, 0.15f, 0.8f);
    Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
    Vector3 n = fwd;
    Quaternion look = Quaternion.LookRotation(n);
    // Face plane from SKINNED VERTEX data (no physics, no timing hazards):
    // Head-bone-dominant vertices transformed by (bone * bindpose) give the
    // true live surface depth along the facing, measured in the EYE band with
    // a robust p95 (FaceDiag 2026-09-12: the global max ≈ +0.36 is hat/hair
    // geometry OUTSIDE the face windows, ~0.12 ahead of the face pane ≈ +0.24
    // on all three meshes; the old global max seated eyes on that plane so
    // they floated in side view). Eyes AND mouths share this one pane:
    // outside-in ray profiling (FaceDiag3) proves the sculpt face is flat
    // (+0.24..0.25 from eye line down past mouth line), so no per-height
    // difference is needed. Falls back to skull proportions when bind data
    // is unusable.
    float eyeFront = MeasureBandFront(_skin, _headBone, c, fwd, right, -s * 0.02f, s * 0.12f, s * 0.45f);
    Vector3 p = c + n * (eyeFront > -10f ? eyeFront + 0.015f : s * 0.55f);
    // Eye line sits slightly BELOW skull center: above is fringe/hat-brim
    // territory (pupils half-buried in hair read as white glint dots); below
    // is open face. Pupils are sized so the sculpt's own eye shading rims
    // them instead of being fully covered (belongs-to-face, not sticker).
    _eyeL = BuildEye("EyeL", p + n * Proud - right * (s * 0.185f) + Vector3.up * (-s * 0.02f), look, new Vector3(s * 0.18f, s * 0.23f, s * 0.12f));
    _eyeR = BuildEye("EyeR", p + n * Proud + right * (s * 0.185f) + Vector3.up * (-s * 0.02f), look, new Vector3(s * 0.18f, s * 0.23f, s * 0.12f));
    // Mouths ride the SAME measured pane as the eyes (shared depth basis:
    // the sculpt face is flat across these heights). A per-height mouth
    // measurement was tried and REVERTED: the mouth band is contaminated by
    // throat/jaw verts, so its p95 (≈ −0.14) buried the mouths inside the
    // chin on Mia/Player while Milo's flat-pane mouth (+0.25) stayed visible.
    Vector3 mp = c + n * (eyeFront > -10f ? eyeFront + 0.015f : s * 0.40f) + Vector3.up * (-s * 0.26f);
    _mouthSmile = BuildMouth("MouthSmile", mp, look, new Vector3(s * 0.25f, s * 0.10f, s * 0.09f), new Color(0.45f, 0.16f, 0.14f));
    _mouthFlat = BuildMouth("MouthFlat", mp, look, new Vector3(s * 0.20f, s * 0.05f, s * 0.08f), new Color(0.4f, 0.14f, 0.12f));
    _mouthOpen = BuildMouth("MouthOpen", mp + Vector3.up * (-s * 0.02f), look, new Vector3(s * 0.12f, s * 0.17f, s * 0.08f), new Color(0.35f, 0.1f, 0.1f));
    // Frown reuses the smile geometry rotated half-turn: same visual language,
    // child-friendly "oops" (never angry/scary). Documented API addition.
    _mouthFrown = BuildMouth("MouthFrown", mp + Vector3.up * (-s * 0.01f), look * Quaternion.Euler(0f, 0f, 180f), new Vector3(s * 0.22f, s * 0.09f, s * 0.08f), new Color(0.38f, 0.13f, 0.12f));
    _eyeBaseScaleL = _eyeL.transform.localScale;
    _eyeBaseScaleR = _eyeR.transform.localScale;
    if (_mouthSmile != null) _smileBase = _mouthSmile.transform.localScale;
    Debug.Log("[CharacterPresentation] FACE_OK skull=" + s.ToString("F3") + " faceFront=" + eyeFront.ToString("F3") + " eyeL=" + _eyeL.transform.position.ToString("F3") + " eyeR=" + _eyeR.transform.position.ToString("F3"), this);
    ResetBlinkTimer();
    ResetAttentionTimer();
    ApplyExpression(_baseline);
  }

  // Reusable presentation API for future dialogue/story/gameplay code.
  public void SetExpression(CharacterExpression e) {
    _baseline = e;
    _pulseT = 0f;
    ApplyExpression(e);
  }

  public void PulseExpression(CharacterExpression e, float seconds) {
    ApplyExpression(e);
    _pulseT = Mathf.Max(0.1f, seconds);
  }

  // Brief attention glance toward a yaw offset (degrees, visual root only).
  public void LookAt(float yawDegrees, float holdSeconds) {
    _attYaw = Mathf.Clamp(yawDegrees, -35f, 35f) * Mathf.Deg2Rad;
    _attHold = Mathf.Max(0.4f, holdSeconds);
  }

  // Reusable celebratory hop (visual root only; colliders/gameplay untouched).
  public void PlayHop() { _hopT = HopDuration; }

  // Deterministic build hook (tests/snapshot tools): builds the face
  // synchronously instead of waiting for the 2nd Update frame. Live spawners
  // keep using the deferred path (SetupFace + Update).
  public void BuildFaceImmediate() {
    if (!_pendingBuild) return;
    _pendingBuild = false;
    _buildFrame = 2;
    BuildFaceNow();
  }

  public void BlinkNow() { _blinkPhase = 0f; }

  void Update() {
    if (_pendingBuild) {
      _buildFrame++;
      if (_buildFrame >= 2) {
        _pendingBuild = false;
        BuildFaceNow();
      } else {
        return;
      }
    }
    float dt = Time.deltaTime;
    if (dt <= 0f) return;
    if (_pulseT > 0f) {
      _pulseT -= dt;
      if (_pulseT <= 0f && _shown != _baseline) ApplyExpression(_baseline);
    }
    TickBlink(dt);
    TickBreath(dt);
    TickAttention(dt);
  }

  void TickBlink(float dt) {
    if (_eyeL == null || _eyeR == null) return;
    if (_blinkPhase < 0f) {
      _blinkT -= dt;
      if (_blinkT <= 0f) _blinkPhase = 0f;
      return;
    }
    _blinkPhase += dt / 0.14f;
    float k = _blinkPhase >= 1f ? 1f : 1f - Mathf.Sin(Mathf.Clamp01(_blinkPhase) * Mathf.PI) * 0.92f;
    ApplyEyeScale(_eyeK, k);
    if (_blinkPhase >= 1f) {
      _blinkPhase = -1f;
      ApplyEyeScale(_eyeK, 1f);
      ResetBlinkTimer();
    }
  }

  void TickBreath(float dt) {
    if (_visualRoot == null || !_hasVisualBase) return;
    _breathPhase += dt * (Mathf.PI * 2f) * 0.25f;
    float hopY = 0f;
    if (_hopT > 0f) {
      _hopT -= dt;
      float k = Mathf.Clamp01(1f - _hopT / HopDuration);
      hopY = Mathf.Sin(k * Mathf.PI) * HopHeight;
    }
    _visualRoot.localPosition = _visualBasePos + Vector3.up * (Mathf.Sin(_breathPhase) * 0.008f + hopY);
  }

  void TickAttention(float dt) {
    if (_visualRoot == null || !_hasVisualBase) return;
    if (_attHold > 0f) {
      _attHold -= dt;
      if (_attHold <= 0f) _attYaw = 0f;
    } else {
      _attT -= dt;
      if (_attT <= 0f) {
        ResetAttentionTimer();
        _attYaw = UnityEngine.Random.Range(0.26f, 0.5f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        _attHold = UnityEngine.Random.Range(1.2f, 2.2f);
        if (UnityEngine.Random.value < 0.4f) PulseExpression(CharacterExpression.Curious, _attHold);
      }
    }
    float cur = Mathf.DeltaAngle(0f, _visualRoot.localRotation.eulerAngles.y) * Mathf.Deg2Rad;
    float next = Mathf.SmoothDamp(cur, _attYaw, ref _attYawVel, 0.35f);
    _visualRoot.localRotation = _visualBaseRot * Quaternion.Euler(0f, next * Mathf.Rad2Deg, 0f);
  }

  void ResetBlinkTimer() { _blinkT = UnityEngine.Random.Range(2.2f, 4.8f); }

  void ResetAttentionTimer() { _attT = UnityEngine.Random.Range(6f, 13f); }

  void ApplyExpression(CharacterExpression e) {
    _shown = e;
    switch (e) {
      case CharacterExpression.Happy:
        // Golden readability (§10.2): Happy must read at 1.5m+ without logs.
        // Geometry carries it: widened + taller smile, slightly larger eyes.
        // Celebration motion comes from the Victory clip trigger (presenters).
        SetEyeScale(1.12f); ShowMouth(_mouthSmile);
        if (_mouthSmile != null)
          _mouthSmile.transform.localScale = new Vector3(_smileBase.x * 1.45f, _smileBase.y * 1.3f, _smileBase.z);
        break;
      case CharacterExpression.Curious:
        SetEyeScale(1f); ShowMouth(_mouthSmile);
        break;
      case CharacterExpression.Surprised:
        SetEyeScale(1.35f); ShowMouth(_mouthOpen);
        break;
      case CharacterExpression.Concerned:
        SetEyeScale(0.9f); ShowMouth(_mouthFlat);
        break;
      case CharacterExpression.Sad:
        SetEyeScale(0.85f); ShowMouth(_mouthFrown);
        break;
      default:
        SetEyeScale(1f); ShowMouth(_mouthFlat);
        break;
    }
  }

  void SetEyeScale(float k) {
    _eyeK = k;
    ApplyEyeScale(k, 1f);
  }

  void ApplyEyeScale(float exprK, float blinkK) {
    if (_eyeL != null) _eyeL.transform.localScale = new Vector3(_eyeBaseScaleL.x * exprK, _eyeBaseScaleL.y * exprK * blinkK, _eyeBaseScaleL.z);
    if (_eyeR != null) _eyeR.transform.localScale = new Vector3(_eyeBaseScaleR.x * exprK, _eyeBaseScaleR.y * exprK * blinkK, _eyeBaseScaleR.z);
  }

  void ShowMouth(GameObject m) {
    if (_mouthSmile != null) _mouthSmile.SetActive(m == _mouthSmile);
    if (_mouthFlat != null) _mouthFlat.SetActive(m == _mouthFlat);
    if (_mouthOpen != null) _mouthOpen.SetActive(m == _mouthOpen);
    if (_mouthFrown != null) _mouthFrown.SetActive(m == _mouthFrown);
  }

  // Live surface depth for one face-height band: transform Head-dominant
  // bind vertices by (boneMatrix * bindpose), keep verts whose height above
  // the skull center is within [yOffset-halfWidth, yOffset+halfWidth] and
  // whose lateral offset is within maxLateral, then return the p95 forward
  // extent from the skull center. p95 (not max) rejects nose-tip-class
  // outliers that share the band edge. Returns -99 when bind data is
  // unusable or the band holds too few verts (caller falls back).
  // C# 9.0 only (no LINQ allocs in the hot path; sort is one-time at build).
  static float MeasureBandFront(SkinnedMeshRenderer skin, Transform headBone, Vector3 center, Vector3 fwd, Vector3 right, float yOffset, float halfWidth, float maxLateral) {
    try {
      if (skin == null || headBone == null) return -99f;
      Mesh mesh = skin.sharedMesh;
      if (mesh == null || !mesh.isReadable) return -99f;
      Transform[] bones = skin.bones;
      Matrix4x4[] binds = mesh.bindposes;
      if (bones == null || binds == null || bones.Length != binds.Length) return -99f;
      int headIdx = -1;
      for (int i = 0; i < bones.Length; i++) {
        if (bones[i] == headBone) { headIdx = i; break; }
      }
      if (headIdx < 0) return -99f;
      Vector3[] verts = mesh.vertices;
      BoneWeight[] weights = mesh.boneWeights;
      if (verts == null || weights == null || verts.Length != weights.Length || verts.Length == 0) return -99f;
      Matrix4x4 m = headBone.localToWorldMatrix * binds[headIdx];
      System.Collections.Generic.List<float> depths = new System.Collections.Generic.List<float>(256);
      for (int i = 0; i < verts.Length; i++) {
        BoneWeight w = weights[i];
        int dom = w.boneIndex0;
        float dw = w.weight0;
        if (w.weight1 > dw) { dw = w.weight1; dom = w.boneIndex1; }
        if (w.weight2 > dw) { dw = w.weight2; dom = w.boneIndex2; }
        if (w.weight3 > dw) { dom = w.boneIndex3; }
        if (dom != headIdx) continue;
        Vector3 world = m.MultiplyPoint3x4(verts[i]);
        Vector3 rel = world - center;
        float y = rel.y; // center/fwd are Y-flattened by the caller, so plain y is the height
        if (Mathf.Abs(y - yOffset) > halfWidth) continue;
        float lat = Vector3.Dot(rel, right);
        if (Mathf.Abs(lat) > maxLateral) continue;
        depths.Add(Vector3.Dot(rel, fwd));
      }
      if (depths.Count < 4) return -99f;
      depths.Sort();
      return depths[(int)(depths.Count * 0.95f)];
    } catch (Exception e) {
      Debug.LogWarning("[CharacterPresentation] Face band measure failed: " + e.Message);
      return -99f;
    }
  }

  static Transform FindChildDeep(Transform root, string childName) {    if (root == null) return null;
    if (root.name == childName) return root;
    for (int i = 0; i < root.childCount; i++) {
      Transform found = FindChildDeep(root.GetChild(i), childName);
      if (found != null) return found;
    }
    return null;
  }

  GameObject BuildEye(string eyeName, Vector3 worldPos, Quaternion look, Vector3 worldSize) {
    GameObject root = new GameObject(eyeName);
    root.transform.position = worldPos;
    root.transform.rotation = look;
    GameObject pupil = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    pupil.name = eyeName + "Pupil";
    pupil.transform.SetParent(root.transform, false);
    pupil.transform.localPosition = Vector3.zero;
    pupil.transform.localScale = worldSize;
    Paint(pupil, Color.black);
    GameObject glint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    glint.name = eyeName + "Glint";
    glint.transform.SetParent(root.transform, false);
    glint.transform.localPosition = new Vector3(0.014f, 0.022f, 0.026f);
    glint.transform.localScale = new Vector3(0.02f, 0.02f, 0.014f);
    Paint(glint, Color.white);
    DestroyColliders(root);
    AttachToHead(root);
    return root;
  }

  GameObject BuildMouth(string mouthName, Vector3 worldPos, Quaternion look, Vector3 worldSize, Color color) {
    GameObject m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    m.name = mouthName;
    m.transform.position = worldPos;
    m.transform.rotation = look;
    m.transform.localScale = worldSize;
    Paint(m, color);
    DestroyColliders(m);
    AttachToHead(m);
    m.SetActive(false);
    return m;
  }

  void AttachToHead(GameObject go) {
    if (go == null || _headBone == null) return;
    // World pose is final: SetParent with worldPositionStays preserves it
    // exactly (bone scale is uniform, so no shear). Do NOT rescale after
    // this call — dividing again shrinks parts 50x into invisibility.
    go.transform.SetParent(_headBone, true);
  }

  static void DestroyColliders(GameObject go) {
    if (go == null) return;
    foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) {
      if (c != null) DestroyImmediate(c);
    }
  }

  static void Paint(GameObject go, Color color) {
    if (go == null) return;
    Renderer r = go.GetComponent<Renderer>();
    if (r == null) return;
    Shader lit = Shader.Find("Universal Render Pipeline/Lit");
    if (lit != null) {
      Material mat = new Material(lit);
      mat.SetColor("_BaseColor", color);
      mat.SetFloat("_Smoothness", 0.4f);
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
