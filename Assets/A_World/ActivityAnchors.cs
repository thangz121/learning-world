// A_World/ActivityAnchors.cs — Agent A. P1-2 (P3.0.1 foundation).
// Presentation anchor registry: the scene-authored answer to "WHERE is
// everything?" Every gameplay area exposes ONE registry with named slots;
// gameplay/camera/UI code reads TRANSFORMS, never magic vectors. Worlds in
// this project are code-built (no .unity micro scenes), so "scene-authored"
// means the builder creates explicit child GOs under a PresentationRoot at
// build time — the coordinates live in the hierarchy, inspectable and
// re-stageable, not scattered in gameplay scripts.
// Slots (§9): Entry, GameplayFocus, Npc, Camera (+LookTarget implied by the
// paired look anchor), Prompt, Feedback, Reward, Exit.
// Real producers: MathWorldBuilder (Math island) + MarketBuilder (Main).
// Real readers: SmartCamera.FrameAnchor + directors/presenters.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class ActivityAnchors : MonoBehaviour {
  [Header("Presentation slots (scene-authored Transforms)")]
  public Transform Entry;
  public Transform GameplayFocus;
  public Transform Npc;
  public Transform Camera;
  public Transform CameraLook;
  public Transform Prompt;
  public Transform Feedback;
  public Transform Reward;
  public Transform Exit;

  // Builds (or finds) the PresentationRoot under a world root. Idempotent:
  // re-builds reuse the existing registry so anchors survive re-entry.
  public static ActivityAnchors Ensure(Transform worldRoot, string rootName) {
    if (worldRoot == null) return null;
    Transform root = worldRoot.Find(rootName ?? "PresentationRoot");
    GameObject go;
    if (root != null) {
      go = root.gameObject;
    } else {
      go = new GameObject(rootName ?? "PresentationRoot");
      go.transform.SetParent(worldRoot, false);
      go.transform.localPosition = Vector3.zero;
      go.transform.localRotation = Quaternion.identity;
    }
    ActivityAnchors anchors = go.GetComponent<ActivityAnchors>();
    if (anchors == null) anchors = go.AddComponent<ActivityAnchors>();
    return anchors;
  }

  // Creates (or reuses) one named slot at a LOCAL position under the registry.
  public Transform EnsureSlot(string slotName, Vector3 localPos) {
    if (string.IsNullOrEmpty(slotName)) return null;
    Transform t = transform.Find(slotName);
    if (t == null) {
      GameObject go = new GameObject(slotName);
      go.transform.SetParent(transform, false);
      t = go.transform;
    }
    t.localPosition = localPos;
    t.localRotation = Quaternion.identity;
    return t;
  }
}
