// A_World/SubjectDefinition.cs — Phase 3.0 WORLD FOUNDATION (Agent A).
// Minimal data-driven subject configuration. WORLD data only:
// identity, gate position, playground center, entry/return points, palette,
// landmark kind. Deliberately ABSENT (phases 3.1+ own them):
// Lesson, Topic, Question, QuestionType, Activity, Curriculum —
// adding any of those fields here is a roadmap violation, reject the PR.
// Pure data (no Unity lifecycle), fully EditMode-testable. C# 9.0 only.
using System.Collections.Generic;
using UnityEngine;

// Landmark shape language per subject. These are VISUAL identities only —
// they imply no curriculum, no gameplay, no question content.
public enum SubjectLandmarkKind {
  Blocks, // Math: geometric solids, counting cubes, abacus beads
  Gears,  // Thinking: gear wheels, interlocking puzzle slab, maze ring
  Books,  // English: open-book slabs, letter blocks, speech-bubble sign
  Scrolls // Vietnamese: tablet pillars, banner slab, dot motifs
}

public class SubjectDefinition {
  public SubjectId Id;
  public string DisplayName;
  public SubjectLandmarkKind Landmark;
  public Vector3 GatePos;           // entry gate arch (on the hedge line)
  public Vector3 PlaygroundCenter;  // district middle
  public Vector3 EntryPoint;        // just inside the gate (camera aim + future spawn)
  public Vector3 ReturnPoint;       // return arch position inside the playground
  public Color Primary;
  public Color Secondary;
  public Color GroundTint;          // playground medallion tint
}

// Single source of truth for the 4 Phase 3.0 subjects. Coordinates are in the
// MarketBuilder world contract (metres): Main World X[-8,8] Z[-6,6] untouched;
// districts sit outside it, roads cross the hedge through 2.4m gaps.
public static class SubjectCatalog {
  // East road runs at z=1.8 (clear of BallCrate z2.6..3.8 and Pedestal z0.15..1.05).
  public static readonly SubjectDefinition Math = new SubjectDefinition {
    Id = SubjectIds.Math,
    DisplayName = "Math",
    Landmark = SubjectLandmarkKind.Blocks,
    GatePos = new Vector3(8.8f, 0f, 1.8f),
    PlaygroundCenter = new Vector3(12.2f, 0f, 1.8f),
    EntryPoint = new Vector3(9.9f, 0f, 1.8f),
    // NE corner: off the entry axis AND the follow sightline (user round:
    // gates must not occlude each other).
    ReturnPoint = new Vector3(13.6f, 0f, -0.9f),
    Primary = new Color(0.25f, 0.45f, 0.85f),
    Secondary = new Color(0.98f, 0.78f, 0.25f),
    GroundTint = new Color(0.55f, 0.68f, 0.88f),
  };

  public static readonly SubjectDefinition Thinking = new SubjectDefinition {
    Id = SubjectIds.Thinking,
    DisplayName = "Thinking",
    Landmark = SubjectLandmarkKind.Gears,
    GatePos = new Vector3(-8.8f, 0f, 1.8f),
    PlaygroundCenter = new Vector3(-12.2f, 0f, 1.8f),
    EntryPoint = new Vector3(-9.9f, 0f, 1.8f),
    // NE corner mirrored (same anti-occlusion reason as Math).
    ReturnPoint = new Vector3(-13.6f, 0f, -0.9f),
    Primary = new Color(0.25f, 0.62f, 0.35f),
    Secondary = new Color(0.95f, 0.55f, 0.20f),
    GroundTint = new Color(0.55f, 0.75f, 0.55f),
  };

  public static readonly SubjectDefinition English = new SubjectDefinition {
    Id = SubjectIds.English,
    DisplayName = "English",
    Landmark = SubjectLandmarkKind.Books,
    GatePos = new Vector3(0f, 0f, -6.8f),
    PlaygroundCenter = new Vector3(0f, 0f, -10.0f),
    EntryPoint = new Vector3(0f, 0f, -7.9f),
    // Further east + toward the gate (user round: no mutual occlusion).
    ReturnPoint = new Vector3(3.0f, 0f, -9.2f),
    Primary = new Color(0.85f, 0.30f, 0.28f),
    Secondary = new Color(0.99f, 0.94f, 0.84f),
    GroundTint = new Color(0.90f, 0.68f, 0.62f),
  };

  public static readonly SubjectDefinition Vietnamese = new SubjectDefinition {
    Id = SubjectIds.Vietnamese,
    DisplayName = "Tiếng Việt",
    Landmark = SubjectLandmarkKind.Scrolls,
    // East of the spawn axis: the south spawn camera (z=9.1) looks straight
    // down x=0, so a road on x=0 would put the gate between camera and player
    // on frame one. x=3.5 keeps first impression (path + Milo) intact.
    GatePos = new Vector3(3.5f, 0f, 6.8f),
    PlaygroundCenter = new Vector3(3.5f, 0f, 10.0f),
    EntryPoint = new Vector3(3.5f, 0f, 7.9f),
    ReturnPoint = new Vector3(5.8f, 0f, 10.0f),
    Primary = new Color(0.55f, 0.35f, 0.75f),
    Secondary = new Color(0.96f, 0.60f, 0.75f),
    GroundTint = new Color(0.70f, 0.60f, 0.82f),
  };

  public static readonly SubjectDefinition[] All = { Math, Thinking, English, Vietnamese };

  public static readonly Dictionary<string, SubjectDefinition> ById =
    new Dictionary<string, SubjectDefinition> {
      { SubjectIds.Math.Value, Math },
      { SubjectIds.Thinking.Value, Thinking },
      { SubjectIds.English.Value, English },
      { SubjectIds.Vietnamese.Value, Vietnamese },
    };

  public static SubjectDefinition Get(SubjectId id) {
    if (id.Value == null) return null;
    SubjectDefinition def;
    return ById.TryGetValue(id.Value, out def) ? def : null;
  }
}
