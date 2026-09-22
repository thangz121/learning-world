// A_World/MathWorld/MicroWorldGate.cs — Agent A. LW P3.0.1 Hub phase.
// ONE reusable gate contract for all 10 micro-world gates (never one script
// per gate). Skeleton stage: pure data + scene-authored anchors, NO click,
// NO travel, NO gameplay — gates are walk-to destinations with styled labels.
// Colliders are FORBIDDEN on gate geometry (P42 dressing rule); clicks fall
// through to the ground so ClickRouter walks the child to the gate mouth.
// Future Micro-World 1 wires travel/validation/feedback on top of EntryAnchor
// + GateId without touching this contract. C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class MicroWorldGate : MonoBehaviour {
  [Header("Gate identity (wired by MathWorldBuilder, never edited in scene)")]
  public string gateId = "";
  public string displayName = "";
  public string pattern = "";
  public Color accent = Color.white;

  public Transform EntryAnchor { get; private set; }
  public Transform ExitAnchor { get; private set; }
  public Transform LabelAnchor { get; private set; }

  // Called once by the builder after the gate root is posed. Creates the
  // three anchors as named children (scene-authored nodes, re-stageable).
  public void Wire(string id, string vnName, string patternId, Color accentColor) {
    gateId = id ?? "";
    displayName = vnName ?? "";
    pattern = patternId ?? "";
    accent = accentColor;
    EntryAnchor = EnsureAnchor("GateEntryAnchor", new Vector3(0f, 0f, 1.8f));
    ExitAnchor = EnsureAnchor("GateExitAnchor", new Vector3(0f, 0f, -1.8f));
    LabelAnchor = EnsureAnchor("GateLabelAnchor", new Vector3(0f, 0f, 0f));
  }

  Transform EnsureAnchor(string slotName, Vector3 localPos) {
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

// The 10 future micro-worlds (fixed order 01..10). Display names are short
// Vietnamese (LegacyRuntime diacritics proven); pattern identity drives each
// gate's visual language (§5/§6). Accents live in the shared Math palette.
public static class MicroWorldCatalog {
  public struct Entry {
    public string Id;
    public string VnName;
    public string Pattern;
    public Color Accent;
  }

  static Entry E(string id, string vn, string pattern, Color accent) {
    Entry e;
    e.Id = id;
    e.VnName = vn;
    e.Pattern = pattern;
    e.Accent = accent;
    return e;
  }

  static readonly Color Leaf = new Color(0.30f, 0.58f, 0.28f);
  static readonly Color Gold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color Berry = new Color(0.80f, 0.20f, 0.22f);
  static readonly Color Blue = new Color(0.25f, 0.45f, 0.85f);
  static readonly Color Aqua = new Color(0.24f, 0.60f, 0.72f);
  static readonly Color Wood = new Color(0.58f, 0.42f, 0.24f);
  static readonly Color Plum = new Color(0.55f, 0.30f, 0.55f);
  static readonly Color Sky = new Color(0.45f, 0.70f, 0.92f);
  static readonly Color Coral = new Color(0.92f, 0.45f, 0.35f);
  static readonly Color Mint = new Color(0.45f, 0.75f, 0.60f);

  public static readonly Entry[] All = {
    E("counting_garden", "Vườn Đếm", "COUNT / CHOOSE / COLLECT", Leaf),
    E("discovery_garden", "Vườn Khám Phá", "FIND / SEARCH / DISCOVER", Mint),
    E("fruit_orchard", "Vườn Trái Cây", "COLLECT / GATHER", Berry),
    E("match_meadow", "Đồng Ghép Cặp", "MATCH", Aqua),
    E("sorting_park", "Công Viên Phân Loại", "SORT / CATEGORIZE", Blue),
    E("puzzle_workshop", "Xưởng Xếp Hình", "DRAG / DROP / PLACE / ORDER", Wood),
    E("delivery_village", "Làng Giao Hàng", "DELIVER / GIVE / BRING", Coral),
    E("build_yard", "Sân Xây Dựng", "BUILD / CONSTRUCT", Gold),
    E("number_bridge", "Cầu Số", "PATH / SEQUENCE / ORDER", Sky),
    E("memory_grove", "Rừng Trí Nhớ", "MEMORY / RECALL", Plum),
  };
}
