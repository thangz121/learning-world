// A_World/CountingGarden/LessonActors.cs — S3-P2Z12 (Gameplay #2 "Bậc thang con số").
// The shared two-NPC kit for the Counting Garden lessons. Extracted from
// CountingDemo (reference gameplay #1) so gameplay #2 reuses the SAME body kit
// (TessVisual/MiloVisual prefabs + face kit + shoes + fist bone), instead of a
// copy — CountingDemo now delegates its actor build here (behaviour byte-equal,
// CT-P48 pins stay green).
//   LessonActor — one staged NPC: root + visual + animator + face + gesture
//                 bones (head/arm/fist). Fields are public data, exactly as the
//                 reference demo drove them.
//   Build       — instantiate + tint + face/shoe kit + click-through colliders.
//                 Adds NO controller: acting stays with the owning lesson.
// PacedVoice — the speech PACER both lessons share (S3-P2Z6 user order: lines
// must breathe, never stream back-to-back). Keeps only the newest pending line,
// waits for the previous Task to finish + a short gap, and logs each line so
// journey evidence can read the acting order.
// C# 9.0 only.
using System;
using System.Threading.Tasks;
using UnityEngine;

public sealed class LessonActor {
  public GameObject Root;
  public Transform Visual;
  public Animator Animator;
  public CharacterPresentation Face;
  public Transform CarryAnchor;
  // The carried prop rides the rig's ACTUAL fist bone when the rig has one.
  public Transform HandBone;
  public Transform WaveBone;
  public Quaternion WaveBase = Quaternion.identity;
  public bool Waving;
  public float WaveT;
  // Juice: squash/stretch around the authored visual scale.
  public Vector3 BaseScale = Vector3.one;
  public float SquashT;
  // Walk bob phase (shared motion kit below).
  public float WalkT;
  // Gesture language: point-at-target + nod (procedural bones).
  public Transform HeadBone;
  public Vector3 PointTarget;
  public float PointT;
  public Quaternion HeadBase = Quaternion.identity;
  public float NodT;
}

public static class LessonActors {
  // One staged NPC: prefab body, tints, face + shoes, click-through.
  public static LessonActor Build(Transform parent, string name, string prefabName, float scale,
      Vector3 stand, Color vest, Color hat) {
    GameObject prefab = Resources.Load<GameObject>(prefabName);
    if (prefab == null) {
      Debug.LogError("[LessonActors] Missing " + prefabName + "; actor parked.");
      return null;
    }
    LessonActor a = new LessonActor();
    a.Root = new GameObject(name);
    a.Root.transform.SetParent(parent, false);
    a.Root.transform.localPosition = stand;
    a.Root.transform.localRotation = Quaternion.identity;
    GameObject visual = UnityEngine.Object.Instantiate(prefab, a.Root.transform, false);
    visual.name = name + "Visual";
    visual.transform.localPosition = new Vector3(0f, 0.02f, 0f);
    visual.transform.localRotation = Quaternion.identity;
    visual.transform.localScale = Vector3.one * scale;
    a.Visual = visual.transform;
    a.BaseScale = visual.transform.localScale;
    a.Animator = visual.GetComponentInChildren<Animator>(true);
    SkinnedMeshRenderer skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
    Transform head = null, footL = null, footR = null;
    if (skin != null) {
      CharacterPresentation.TintSharedMaterials(skin, "Vest", vest);
      CharacterPresentation.TintSharedMaterials(skin, "Hat", hat);
      if (skin.bones != null) {
        foreach (Transform bone in skin.bones) {
          if (bone == null) continue;
          if (head == null && bone.name == "Head") head = bone;
          if (footL == null && bone.name == "Foot.L") footL = bone;
          if (footR == null && bone.name == "Foot.R") footR = bone;
          if (a.WaveBone == null && (bone.name == "UpperArm.R" || bone.name == "Shoulder.R"))
            a.WaveBone = bone;
          if (a.HeadBone == null && bone.name == "Head") a.HeadBone = bone;
          if (a.HandBone == null && (bone.name == "Fist.R" || bone.name == "Hand.R"))
            a.HandBone = bone;
        }
      }
    }
    if (a.HeadBone != null) a.HeadBase = a.HeadBone.localRotation;
    a.Face = a.Root.AddComponent<CharacterPresentation>();
    try { a.Face.SetupFace(skin, head, a.Root.transform, a.Visual); } catch (Exception) { }
    try { a.Face.BuildFaceImmediate(); } catch (Exception) { }
    try {
      if (footL != null) a.Face.QueueShoe(footL, "ShoeL");
      if (footR != null) a.Face.QueueShoe(footR, "ShoeR");
      a.Face.BuildShoesImmediate();
    } catch (Exception) { }
    GameObject anchor = new GameObject(name + "CarryAnchor");
    anchor.transform.SetParent(a.Root.transform, false);
    anchor.transform.localPosition = new Vector3(0f, 0.95f, -0.35f);
    a.CarryAnchor = anchor.transform;
    // Click-through: a lesson must never eat walk clicks.
    try { CharacterPresentation.DestroyColliders(a.Root); } catch (Exception) { }
    return a;
  }
}

// Shared motion/gesture kit (S3-P2Z12b, additive): local-space walking with a
// step bob, facing, and the wave/point/nod/squash gesture ticks driven off the
// LessonActor data. The two OLDER lessons keep their private copies untouched
// (zero risk to the reference gameplay); new lessons reuse this.
public static class LessonMotion {
  // Walk in LOCAL space (XZ + Y, so actors climb treads). Arrives within 0.12m.
  public static bool WalkTo(LessonActor a, Vector3 targetLocal, float dt, float speed) {
    if (a == null || a.Root == null) return true;
    Vector3 p = a.Root.transform.localPosition;
    Vector3 flat = new Vector3(targetLocal.x - p.x, 0f, targetLocal.z - p.z);
    float dist = flat.magnitude;
    float dy = targetLocal.y - p.y;
    if (dist <= 0.10f && Mathf.Abs(dy) <= 0.05f) {
      try {
        if (a.Visual != null) {
          Vector3 v = a.Visual.localPosition;
          v.y = 0.02f;
          a.Visual.localPosition = v;
        }
      } catch (Exception) { }
      return true;
    }
    FaceTowards(a, targetLocal, dt, 6f);
    float step = Mathf.Min(speed * dt, Mathf.Max(dist, Mathf.Abs(dy)));
    Vector3 dir = dist > 0.0001f ? flat / dist : Vector3.zero;
    float moveXZ = Mathf.Min(step, dist);
    Vector3 next = new Vector3(p.x + dir.x * moveXZ, p.y, p.z + dir.z * moveXZ);
    next.y = Mathf.MoveTowards(p.y, targetLocal.y, speed * dt * 0.9f);
    a.Root.transform.localPosition = next;
    a.WalkT += dt;
    try {
      if (a.Visual != null) {
        Vector3 v = a.Visual.localPosition;
        v.y = 0.02f + Mathf.Abs(Mathf.Sin(a.WalkT * 9f)) * 0.05f;
        a.Visual.localPosition = v;
      }
    } catch (Exception) { }
    return false;
  }

  public static void FaceTowards(LessonActor a, Vector3 targetLocal, float dt, float rate) {
    if (a == null || a.Root == null) return;
    try {
      Vector3 p = a.Root.transform.localPosition;
      Vector3 d = new Vector3(targetLocal.x - p.x, 0f, targetLocal.z - p.z);
      if (d.sqrMagnitude < 0.0001f) return;
      Quaternion want = Quaternion.LookRotation(d);
      a.Root.transform.localRotation = Quaternion.Slerp(a.Root.transform.localRotation, want,
        1f - Mathf.Exp(-rate * dt));
    } catch (Exception) { }
  }

  public static void FaceSnap(LessonActor a, Vector3 dir) {
    if (a == null || a.Root == null) return;
    try {
      Vector3 d = new Vector3(dir.x, 0f, dir.z);
      if (d.sqrMagnitude < 0.0001f) return;
      a.Root.transform.localRotation = Quaternion.LookRotation(d);
    } catch (Exception) { }
  }

  public static void Wave(LessonActor a) { if (a != null) a.WaveT = 1.4f; }

  public static void PointAt(LessonActor a, Vector3 targetLocal, float seconds) {
    if (a == null) return;
    a.PointTarget = targetLocal;
    a.PointT = Mathf.Max(0.4f, seconds);
  }

  public static void Nod(LessonActor a) { if (a != null) a.NodT = 0.6f; }

  public static void Trigger(LessonActor a, string name) {
    try { if (a != null && a.Animator != null) a.Animator.SetTrigger(name); } catch (Exception) { }
  }

  public static void Hop(LessonActor a) {
    if (a == null) return;
    try { if (a.Face != null) a.Face.PlayHop(); } catch (Exception) { }
    a.SquashT = 0.35f;
  }

  // One gesture tick pass (call once per frame per actor).
  public static void Tick(LessonActor a, float dt) {
    if (a == null) return;
    TickWaveOne(a, dt);
    TickPointOne(a, dt);
    TickNodOne(a, dt);
    TickSquash(a, dt);
  }

  static void TickWaveOne(LessonActor a, float dt) {
    if (a.WaveBone == null) return;
    try {
      if (a.WaveT > 0f) {
        if (!a.Waving) { a.Waving = true; a.WaveBase = a.WaveBone.localRotation; }
        a.WaveT -= dt;
        float wave = Mathf.Sin(Time.time * 14f) * 18f;
        a.WaveBone.localRotation = a.WaveBase * Quaternion.Euler(0f, 0f, -75f + wave);
        if (a.WaveT <= 0f) { a.Waving = false; a.WaveBone.localRotation = a.WaveBase; }
      }
    } catch (Exception) { }
  }

  static void TickPointOne(LessonActor a, float dt) {
    if (a.Root == null || a.PointT <= 0f) return;
    try {
      a.PointT = Mathf.Max(0f, a.PointT - dt);
      FaceTowards(a, a.PointTarget, dt, 5f);
      if (a.WaveBone != null) {
        if (!a.Waving) { a.Waving = true; a.WaveBase = a.WaveBone.localRotation; }
        a.WaveBone.localRotation = a.WaveBase * Quaternion.Euler(0f, 0f, -68f);
      }
      if (a.PointT <= 0f && a.WaveBone != null) {
        a.WaveBone.localRotation = a.WaveBase;
        a.Waving = false;
      }
    } catch (Exception) { }
  }

  static void TickNodOne(LessonActor a, float dt) {
    if (a.HeadBone == null || a.NodT <= 0f) return;
    try {
      a.NodT = Mathf.Max(0f, a.NodT - dt);
      float k = 1f - a.NodT / 0.6f;
      a.HeadBone.localRotation = a.HeadBase * Quaternion.Euler(Mathf.Sin(k * Mathf.PI) * 14f, 0f, 0f);
      if (a.NodT <= 0f) a.HeadBone.localRotation = a.HeadBase;
    } catch (Exception) { }
  }

  static void TickSquash(LessonActor a, float dt) {
    if (a.Visual == null || a.SquashT <= 0f) return;
    try {
      a.SquashT = Mathf.Max(0f, a.SquashT - dt);
      float k = a.SquashT / 0.35f;
      float bulge = Mathf.Sin(k * Mathf.PI);
      Vector3 baseS = a.BaseScale;
      a.Visual.localScale = new Vector3(baseS.x * (1f - 0.11f * bulge), baseS.y * (1f + 0.16f * bulge),
        baseS.z * (1f - 0.11f * bulge));
      if (a.SquashT <= 0f) a.Visual.localScale = baseS;
    } catch (Exception) { }
  }
}

// Paced speech (shared): one line at a time, gap between lines, newest pending
// wins. The owner gates on its own activity state (audience/engaged), so a
// stale line never fires after a pause — call Clear() when the stage stops.
public sealed class PacedVoice {
  public IAudioDirector Audio;
  public VoiceProfileId Voice = new VoiceProfileId("npc_female_01");
  public string LogTag = "Lesson";
  public float GapSeconds = 0.65f;

  Task _task;
  bool _hasPending;
  string _pending;
  SpeechStyle _pendingStyle;
  AudioPriority _pendingPriority;
  float _gapT;

  public bool Idle {
    get {
      if (_task != null && !_task.IsCompleted) return false;
      return _gapT <= 0f;
    }
  }

  public bool HasLine { get { return _hasPending; } }

  public void Speak(string text, SpeechStyle style, AudioPriority priority) {
    if (Audio == null || string.IsNullOrEmpty(text)) return;
    if (!Idle) { // still talking (or breathing): hold the newest line
      _pending = text;
      _pendingStyle = style;
      _pendingPriority = priority;
      _hasPending = true;
      return;
    }
    Submit(text, style, priority);
  }

  public void Speak(string text) { Speak(text, SpeechStyle.Clear, AudioPriority.P2_Instruction); }

  public void Tick(float dt) {
    if (_gapT > 0f) _gapT -= dt;
    if (!_hasPending || string.IsNullOrEmpty(_pending)) return;
    if (!Idle) return;
    Submit(_pending, _pendingStyle, _pendingPriority);
  }

  public void Clear() {
    _hasPending = false;
    _pending = null;
  }

  void Submit(string text, SpeechStyle style, AudioPriority priority) {
    try {
      var req = new DialogueRequest(text, Voice, DialogueLang.Language, 1f, 1f, style,
        AudioFormat.Mp3_44100, priority);
      _task = Audio.SpeakAsync(req); // awaited implicitly by the pacer
      _gapT = GapSeconds;
      _hasPending = false;
      _pending = null;
      try { Debug.Log("[" + LogTag + "] say '" + text + "'"); } catch (Exception) { }
    } catch (Exception) { }
  }
}
