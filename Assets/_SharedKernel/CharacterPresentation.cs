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
  int _frame; // live frames ticked (R5m: shoe-only flush waits for frame 2)
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
  // Gait lift offset (R5d, shared mechanism): walk clips pose the feet higher
  // relative to the root than idle clips, so a single static lift grounds one
  // gait and floats the other (R5c burst: idle planted at -0.012, walk floats
  // ~0.10 world). Owners drive SetLiftOffset from locomotion state; the offset
  // eases toward its target (no visible pop on gait change) and composes with
  // breathing + hop on the same visual-root channel. Local units of the
  // visual root's parent space; default 0 (idle lifts stay exactly as built).
  float _liftOffset;
  float _liftTarget;
  // R5k surface tracking (shared mechanism): the NavMesh root rides ±3cm of
  // bake noise across the lawn while the RENDERED ground is flat, so a baked
  // lift plants one spot and sinks/floats another (R5j: idle -0.012 at (1,2)
  // vs -0.037 at (-5,1) with identical lifts). Track the rendered surface by
  // raycast and hold the VISUAL at its calibrated height above it; the baked
  // lifts keep their meaning (clip pose compensation at the calib spot).
  // Teleports snap; roaming eases like gait. Roam-free NPCs: ~zero change.
  // Local units of the visual root's parent space, like the lift channel.
  float _surfDelta0;
  bool _hasSurfCalib;
  float _surfOffset;
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
      // R5k: calibrate surface-vs-root once (frame-2 live transforms).
      _surfDelta0 = SurfaceDeltaNow();
      _hasSurfCalib = true;
      _surfOffset = 0f;
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
    // Final polish: +12% pupil presence so the doll eyes dominate the
    // sculpted lid shading (shared by every character, verified at 1.5m/6m).
    _eyeL = BuildEye("EyeL", p + n * Proud - right * (s * 0.185f) + Vector3.up * (-s * 0.02f), look, new Vector3(s * 0.2f, s * 0.26f, s * 0.13f));
    _eyeR = BuildEye("EyeR", p + n * Proud + right * (s * 0.185f) + Vector3.up * (-s * 0.02f), look, new Vector3(s * 0.2f, s * 0.26f, s * 0.13f));
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

  // Gait lift driver (see _liftOffset): target is approached smoothly inside
  // TickBreath, so walk<->idle transitions never snap the character.
  public void SetLiftOffset(float localY) { _liftTarget = localY; }

  // R5k rendered-surface delta (world units): surfaceY - rootY, via the same
  // own-subtree-excluding downward raycast the audit probes use. 0 when the
  // physics scene is unavailable (batch-safe: keeps baked behavior).
  float SurfaceDeltaNow() {
    if (_visualRoot == null || _visualRoot.parent == null) return 0f;
    Transform root = _visualRoot.parent;
    float rootY = root.position.y;
    RaycastHit[] hits = Physics.RaycastAll(root.position + Vector3.up * 2f, Vector3.down, 6f);
    if (hits == null) return 0f;
    float best = float.MinValue;
    bool any = false;
    foreach (RaycastHit h in hits) {
      if (h.collider == null) continue;
      Transform t = h.collider.transform;
      bool own = false;
      while (t != null) { if (t == root) { own = true; break; } t = t.parent; }
      if (own) continue;
      if (h.point.y <= rootY + 0.6f && h.point.y > best) { best = h.point.y; any = true; }
    }
    if (!any) return 0f;
    return best - rootY;
  }

  // Deterministic build hook (tests/snapshot tools): builds the face
  // synchronously instead of waiting for the 2nd Update frame. Live spawners
  // keep using the deferred path (SetupFace + Update).
  public void BuildFaceImmediate() {
    if (!_pendingBuild) return;
    _pendingBuild = false;
    _buildFrame = 2;
    BuildFaceNow();
  }

  // Reusable stylized footwear (final polish): the Quaternius pack ships no
  // shoe geometry (dump-proven: no shoe submesh on any of the three rigs), so
  // feet read as bare stubs. One uniform dark shoe cap per foot, built with
  // the same world-placement + SetParent(worldStays) pattern as the face kit,
  // parented to the Foot.L/R bone so it follows Idle/Walk clips with zero
  // extra wiring. Sizes are world units tuned once for the chibi proportion;
  // color is the shared shoe leather (identity stays in clothing, identical
  // for every character by design). Seat is sole-relative (live-baked mesh
  // minima at frame-2 idle): rig "Foot" bones ride high above the sole, so an
  // ankle-relative drop misplaces caps by decimeters (R5l proof).
  public static readonly Vector3 ShoeSize = new Vector3(0.13f, 0.1f, 0.26f);
  public static readonly Color ShoeColor = new Color(0.23f, 0.17f, 0.13f);

  struct QueuedShoe { public Transform Foot; public string Name; }
  readonly System.Collections.Generic.List<QueuedShoe> _pendingShoes =
    new System.Collections.Generic.List<QueuedShoe>();

  // Queue a shoe for the deferred frame-2 build (live path: world transforms
  // read during Awake/AddComponent are stale-identity, same rule as the face).
  public void QueueShoe(Transform footBone, string shoeName) {
    if (footBone == null || string.IsNullOrWhiteSpace(shoeName)) return;
    _pendingShoes.Add(new QueuedShoe { Foot = footBone, Name = shoeName.Trim() });
    _shoeRetry = 0;
  }

  // R5p-fix settle retry: BakeMesh is not only EMPTY on the first frames
  // (R5q) — it bakes the BIND pose at the ORIGIN while the gameplay roots
  // already sit at spawn (player 4.5m, Mia 4.3m, Milo 2.1m from origin: the
  // exact "degenerate verts 4m out" R5o blamed on garbage). The 0.8m gate
  // then rejects EVERYTHING and the old code permanently built a floating
  // drop cap (survey32: 6/6 mode=drop, shoes 20-40cm off, stance TIMEOUT).
  // Live callers now DEFER (keep the queue) while the gate finds nothing and
  // only accept drop after the mesh provably settles or retries exhaust.
  int _shoeRetry;
  const int ShoeRetryMax = 180;

  // Deterministic shoe hook (tests drive this directly; the frame-2 Update
  // path calls it live). Builds every queued shoe at live transforms.
  // R5q: BakeMesh is EMPTY on the first frames even though transforms are
  // live (face math avoids BakeMesh, which is why faces never noticed) — so
  // this is a TRY-build: live callers retry next frame until the bake yields
  // verts; direct/test callers force through (fallback drop applies).
  public void BuildShoesImmediate() { TryBuildShoes(true); }

  bool MeshReady() {
    if (_skin == null || _skin.sharedMesh == null) return true;
    Mesh scratch = new Mesh();
    try { _skin.BakeMesh(scratch); } catch { Destroy(scratch); return false; }
    bool ok = scratch != null && scratch.vertexCount > 0;
    Destroy(scratch);
    return ok;
  }

  bool TryBuildShoes(bool force) {
    if (_pendingShoes.Count == 0) return true;
    if (!force && !MeshReady()) return false;
    // R5p-fix: peek first — if the mesh has verts but none lie within the
    // 0.8m gate yet (bind pose still at origin), DEFER instead of building a
    // permanent floating drop. Forced (test) callers skip the wait.
    if (!force && _shoeRetry < ShoeRetryMax) {
      bool anyGated = false;
      foreach (QueuedShoe peek in _pendingShoes) {
        if (peek.Foot == null) continue;
        string peekMode;
        int peekVerts;
        float peekDist;
        SoleSeatFor(peek.Name, peek.Foot.position, out peekMode, out peekVerts, out peekDist);
        if (peekMode != "drop") { anyGated = true; break; }
      }
      if (!anyGated) {
        if (_shoeRetry == 0 || _shoeRetry % 30 == 0) {
          string footInfo = _pendingShoes.Count > 0 && _pendingShoes[0].Foot != null
            ? _pendingShoes[0].Foot.position.ToString("F2") : "nullfoot";
          string diag = ShoeMeshDiag();
          Debug.Log("[CharacterPresentation] SHOE_DEFER retry=" + _shoeRetry
            + " foot=" + footInfo + " " + diag + " (mesh not settled inside 0.8m gate)", this);
        }
        _shoeRetry++;
        return false;
      }
    }
    Vector3 fwd = Vector3.forward;
    if (_anchorSpace != null) {
      fwd = _anchorSpace.forward;
      fwd.y = 0f;
      if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
    }
    fwd.Normalize();
    Quaternion look = Quaternion.LookRotation(fwd);
    // R5n sole-relative seat: the rigs' "Foot" bones ride well above the mesh
    // sole (R5l projection proof: caps hovered 15-36cm), so an ankle-relative
    // drop is rig-fragile.
    // R5o full-sole seat: bone XZ is ALSO offset from the mesh sole (~15cm:
    // 100x armature amplifies a millimeter bind mismatch). Seat each cap on
    // its OWN foot's live-baked sole minima (side-partitioned by dominant
    // bone name .L/.R); the cap then rides its bone with a correct constant
    // local offset (mesh flex ±2cm stays invisible).
    // R5p garbage-gated seat (R5o caught degenerate verts 4m out): candidates
    // must lie within 0.8m (world) of their foot bone; fallback chain
    // side-partitioned -> unpartitioned-near-bone -> old drop. The chosen
    // mode is logged (audit trail in Player.log).
    foreach (QueuedShoe q in _pendingShoes) {
      if (q.Foot == null) continue;
      GameObject shoe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      shoe.name = q.Name;
      string mode;
      int vertCount;
      float minDist;
      Vector3 seat = SoleSeatFor(q.Name, q.Foot.position, out mode, out vertCount, out minDist);
      Vector3 basePos;
      if (mode != "drop") {
        basePos = new Vector3(seat.x, seat.y + ShoeSize.y * 0.5f, seat.z);
      } else {
        basePos = new Vector3(
          q.Foot.position.x + fwd.x * 0.05f,
          q.Foot.position.y - 0.085f,
          q.Foot.position.z + fwd.z * 0.05f);
      }
      Debug.Log("[CharacterPresentation] SHOE_SEAT " + q.Name + " mode=" + mode
        + " seat=" + basePos.ToString("F3")
        + " foot=" + q.Foot.position.ToString("F3")
        + " verts=" + vertCount + " minDist=" + minDist.ToString("F3")
        + " retry=" + _shoeRetry, this);
      shoe.transform.position = basePos;
      shoe.transform.rotation = look;
      shoe.transform.localScale = ShoeSize;
      PaintShoe(shoe);
      DestroyColliders(shoe);
      shoe.transform.SetParent(q.Foot, true);
    }
    _pendingShoes.Clear();
    _shoeRetry = 0;
    return true;
  }

  // R5p per-foot live sole (world): lowest CURRENT-posed vertex position
  // whose dominant bone name matches the shoe side (.L/.R in the shoe name)
  // AND which lies within 0.8m of the foot bone (far-flung verts rejected).
  // Falls back to the nearest unpartitioned minima ("near"), then "drop".
  // Mapping = face-kit proven bone-bind math: BIND verts (sharedMesh) posed
  // by (dominantBone.localToWorld * bindpose). BakeMesh + renderer.localToWorld
  // is BANNED here (survey36 proof: 40-50x Body scale double-transforms the
  // bake into 20m x 52-85m giants; min-Y only looked plausible by coincidence
  // and never moves with the stride). Diag outs: vertCount + minDist.
  Vector3 SoleSeatFor(string shoeName, Vector3 footPos, out string mode, out int vertCount, out float minDist) {
    Vector3 seat = new Vector3(0f, -1000f, 0f);
    mode = "drop";
    vertCount = 0;
    minDist = 999f;
    try {
      if (_skin == null || _skin.sharedMesh == null) return seat;
      Mesh mesh = _skin.sharedMesh;
      if (mesh == null || !mesh.isReadable) return seat;
      bool wantLeft = shoeName != null && shoeName.EndsWith("L");
      bool wantRight = shoeName != null && shoeName.EndsWith("R");
      Vector3[] verts = mesh.vertices;
      BoneWeight[] weights = mesh.boneWeights;
      if (verts == null || verts.Length == 0) return seat;
      vertCount = verts.Length;
      Transform[] bones = _skin.bones;
      Matrix4x4[] binds = mesh.bindposes;
      if (bones == null || binds == null || bones.Length != binds.Length) return seat;
      float bestSide = float.MaxValue;
      Vector3 seatSide = seat;
      bool anySide = false;
      float bestNear = float.MaxValue;
      Vector3 seatNear = seat;
      bool anyNear = false;
      for (int i = 0; i < verts.Length; i++) {
        int dom = -1;
        string bn = "";
        if (weights != null && i < weights.Length) {
          BoneWeight w = weights[i];
          dom = w.boneIndex0;
          float bw = w.weight0;
          if (w.weight1 > bw) { dom = w.boneIndex1; bw = w.weight1; }
          if (w.weight2 > bw) { dom = w.boneIndex2; bw = w.weight2; }
          if (w.weight3 > bw) { dom = w.boneIndex3; }
          if (dom >= 0 && dom < bones.Length && bones[dom] != null)
            bn = bones[dom].name;
        }
        if (dom < 0 || dom >= bones.Length || dom >= binds.Length || bones[dom] == null) continue;
        Vector3 wp = bones[dom].localToWorldMatrix.MultiplyPoint3x4(binds[dom].MultiplyPoint3x4(verts[i]));
        float d = (wp - footPos).magnitude;
        if (d < minDist) minDist = d;
        if (d > 0.8f) continue; // R5p garbage gate
        bool left = bn.IndexOf(".L") >= 0;
        bool right = bn.IndexOf(".R") >= 0;
        bool sideOk = (wantLeft && left) || (wantRight && right)
          || (!wantLeft && !wantRight);
        if (wp.y < bestNear) { bestNear = wp.y; seatNear = wp; anyNear = true; }
        if (sideOk && wp.y < bestSide) { bestSide = wp.y; seatSide = wp; anySide = true; }
      }
      if (anySide) {
        // R5V sole guard: a "side" minima ABOVE the ankle is not a sole (player
        // Casual_Male survey37: seat +0.033 above the foot while the true sole
        // sits -0.31 below -> 35cm floating caps). Fall through to the
        // unpartitioned near-sole instead of building on ankle verts.
        if (seatSide.y > footPos.y - 0.05f && anyNear && seatNear.y < footPos.y - 0.05f) {
          seat = seatNear; mode = "near";
        } else {
          seat = seatSide; mode = "side";
        }
      }
      else if (anyNear) { seat = seatNear; mode = "near"; }
      else { seat = new Vector3(0f, -1000f, 0f); mode = "drop"; }
    } catch { seat = new Vector3(0f, -1000f, 0f); mode = "drop"; }
    return seat;
  }

  // R5p-fix one-shot mesh diag (throttled by caller): bone-bind mapped bounds
  // vs the gameplay root, so the 40-50x double-scale dispute stays settled.
  string ShoeMeshDiag() {
    try {
      string rootP = transform != null ? transform.position.ToString("F2") : "noroot";
      if (_skin == null || _skin.sharedMesh == null) return "root=" + rootP + " noshared";
      Mesh mesh = _skin.sharedMesh;
      Vector3[] verts = mesh.vertices;
      if (verts == null || verts.Length == 0) return "root=" + rootP + " empty";
      Transform[] bones = _skin.bones;
      Matrix4x4[] binds = mesh.bindposes;
      if (bones == null || binds == null || bones.Length != binds.Length) return "root=" + rootP + " nobind";
      BoneWeight[] weights = mesh.boneWeights;
      Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
      Vector3 mx = new Vector3(float.MinValue, float.MinValue, float.MinValue);
      int mapped = 0;
      for (int i = 0; i < verts.Length; i++) {
        int dom = -1;
        if (weights != null && i < weights.Length) {
          BoneWeight w = weights[i];
          dom = w.boneIndex0;
          float bw = w.weight0;
          if (w.weight1 > bw) { dom = w.boneIndex1; bw = w.weight1; }
          if (w.weight2 > bw) { dom = w.boneIndex2; bw = w.weight2; }
          if (w.weight3 > bw) { dom = w.boneIndex3; }
        }
        if (dom < 0 || dom >= bones.Length || dom >= binds.Length || bones[dom] == null) continue;
        Vector3 wp = bones[dom].localToWorldMatrix.MultiplyPoint3x4(binds[dom].MultiplyPoint3x4(verts[i]));
        mn.x = Mathf.Min(mn.x, wp.x); mn.y = Mathf.Min(mn.y, wp.y); mn.z = Mathf.Min(mn.z, wp.z);
        mx.x = Mathf.Max(mx.x, wp.x); mx.y = Mathf.Max(mx.y, wp.y); mx.z = Mathf.Max(mx.z, wp.z);
        mapped++;
      }
      return "root=" + rootP + " n=" + verts.Length + " mapped=" + mapped
        + " wMin=" + mn.ToString("F2") + " wMax=" + mx.ToString("F2");
    } catch (System.Exception e) { return "diag-throw:" + e.Message; }
  }

  static void PaintShoe(GameObject go) {
    if (go == null) return;
    Renderer r = go.GetComponent<Renderer>();
    if (r == null) return;
    Shader lit = Shader.Find("Universal Render Pipeline/Lit");
    if (lit == null) return;
    Material mat = new Material(lit);
    mat.SetColor("_BaseColor", ShoeColor);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.55f);
    r.sharedMaterial = mat;
  }

  public void BlinkNow() { _blinkPhase = 0f; }

  void Update() {
    _frame++;
    if (_pendingBuild) {
      _buildFrame++;
      if (_buildFrame >= 2) {
        _pendingBuild = false;
        BuildFaceNow();
        TryBuildShoes(false); // R5q: may defer until the bake yields verts
      } else {
        return;
      }
    } else if (_pendingShoes.Count > 0 && _frame >= 2) {
      // Shoe-only usage (no face pending): still wait for frame 2, when
      // world transforms are guaranteed live. Building on frame 0/1 bakes a
      // permanent WRONG local offset from stale-identity bones (R5m: caps
      // hovered 15-36cm all game, proven by the red-shoe player build).
      // R5q: Try (not force) — an empty first bake defers to a later frame.
      TryBuildShoes(false);
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
    _liftOffset = Mathf.Lerp(_liftOffset, _liftTarget, Mathf.Min(1f, dt * 6f));
    // R5k: hold calibrated height above the RENDERED surface. Convert world
    // delta to parent-local units (player root scale 0.8); teleports snap.
    if (_hasSurfCalib) {
      Transform parent = _visualRoot.parent;
      float ps = (parent != null && Mathf.Abs(parent.lossyScale.y) > 0.001f) ? parent.lossyScale.y : 1f;
      float targetLocal = (SurfaceDeltaNow() - _surfDelta0) / ps;
      if (Mathf.Abs(targetLocal - _surfOffset) > 0.25f) _surfOffset = targetLocal;
      else _surfOffset = Mathf.Lerp(_surfOffset, targetLocal, Mathf.Min(1f, dt * 6f));
    }
    float hopY = 0f;
    if (_hopT > 0f) {
      _hopT -= dt;
      float k = Mathf.Clamp01(1f - _hopT / HopDuration);
      hopY = Mathf.Sin(k * Mathf.PI) * HopHeight;
    }
    _visualRoot.localPosition = _visualBasePos + Vector3.up * (Mathf.Sin(_breathPhase) * 0.008f + hopY + _liftOffset + _surfOffset);
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
    glint.transform.localPosition = new Vector3(0.016f, 0.024f, 0.028f);
    glint.transform.localScale = new Vector3(0.024f, 0.024f, 0.016f);
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
      DestroyNow(c);
    }
  }

  // EditMode-safe destroy (tests build kits outside play mode, where
  // Destroy() is a silent no-op that leaks colliders into later tests).
  // Reusable by any world/presentation builder (World -> SharedKernel).
  public static void DestroyNow(UnityEngine.Object o) {
    if (o == null) return;
#if UNITY_EDITOR
    if (!Application.isPlaying) { DestroyImmediate(o); return; }
#endif
    Destroy(o);
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
