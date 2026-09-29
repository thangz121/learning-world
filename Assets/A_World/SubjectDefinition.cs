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
  Scrolls, // Vietnamese: tablet pillars, banner slab, dot motifs
  Compass  // Exploration (KHÁM PHÁ): globe + magnifier ring, pebble pillars
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
  // Phase 3.0.x: additive subject scene (null/empty = legacy spatial district,
  // walked in continuously — Thinking/English/Vietnamese until their phases).
  public string SceneName;
}

// Single source of truth for the 5 subjects of the Subject Yard (A).
// Coordinates are in the MarketBuilder world contract (metres).
// HUB-ARC layout (FULL ARCHITECTURE RESET PHASE 2): the 5 entry gates stand
// TOGETHER in one symmetric arc hugging the yard — x = -14/-7/0/+7/+14 at
// z = -7/-5.5/-5/-5.5/-7 (outer slots deeper so their pillar-click snap never
// reaches the legacy district return discs; exact 7m neighbour spacing so
// gates + labels never occlude each other and CT-P31F's >6m contract holds).
// Left -> right from spawn: Thinking, English, KHÁM PHÁ, Vietnamese, Math.
// Districts/return triggers are legacy scenery (deleted in PHASE 5); travel
// is the skill yard now (SelectionYardArea).
// Entry labels are Vietnamese (same WorldNameLabel font that renders "Về").
public static class SubjectCatalog {
  // Hub reference: gates face the hub; walkways fan out from the yard.
  public static readonly Vector3 HubCenter = new Vector3(0f, 0f, 0.5f);
  // Arc slots, left -> right from spawn (west -> east), 7m spacing:
  // Thinking(-14,-7) English(-7,-5.5) Exploration(0,-5) Vietnamese(7,-5.5) Math(14,-7).
  public static readonly SubjectDefinition Math = new SubjectDefinition {
    Id = SubjectIds.Math,
    DisplayName = "Toán",
    Landmark = SubjectLandmarkKind.Blocks,
    GatePos = new Vector3(14f, 0f, -7f),
    PlaygroundCenter = new Vector3(12.2f, 0f, 1.8f),
    EntryPoint = new Vector3(10.84f, 0f, -2.85f), // just past the gate toward the district
    // NE corner: off the entry axis AND the follow sightline (user round:
    // gates must not occlude each other).
    ReturnPoint = new Vector3(13.6f, 0f, -0.9f),
    Primary = new Color(0.25f, 0.45f, 0.85f),
    Secondary = new Color(0.98f, 0.78f, 0.25f),
    GroundTint = new Color(0.55f, 0.68f, 0.88f),
    SceneName = "MathScene", // P3.0.1 pilot: own additive scene (others spatial until phased)
  };

  public static readonly SubjectDefinition Thinking = new SubjectDefinition {
    Id = SubjectIds.Thinking,
    DisplayName = "Tư duy",
    Landmark = SubjectLandmarkKind.Gears,
    GatePos = new Vector3(-14f, 0f, -7f),
    PlaygroundCenter = new Vector3(-12.2f, 0f, 1.8f),
    EntryPoint = new Vector3(-10.84f, 0f, -2.85f), // just past the gate toward the district
    // NE corner mirrored (same anti-occlusion reason as Math).
    ReturnPoint = new Vector3(-13.6f, 0f, -0.9f),
    Primary = new Color(0.25f, 0.62f, 0.35f),
    Secondary = new Color(0.95f, 0.55f, 0.20f),
    GroundTint = new Color(0.55f, 0.75f, 0.55f),
  };

  public static readonly SubjectDefinition English = new SubjectDefinition {
    Id = SubjectIds.English,
    DisplayName = "Tiếng Anh",
    Landmark = SubjectLandmarkKind.Books,
    GatePos = new Vector3(-7f, 0f, -5.5f),
    PlaygroundCenter = new Vector3(0f, 0f, -10.0f),
    EntryPoint = new Vector3(-2.81f, 0f, -5.98f), // just past the gate toward the district
    // West of the core, clear of the neighbouring Vietnamese gate walkway
    // (user round: the old east spot tangled the VN gate view).
    ReturnPoint = new Vector3(-2.5f, 0f, -11.5f),
    Primary = new Color(0.85f, 0.30f, 0.28f),
    Secondary = new Color(0.99f, 0.94f, 0.84f),
    GroundTint = new Color(0.90f, 0.68f, 0.62f),
  };

  public static readonly SubjectDefinition Vietnamese = new SubjectDefinition {
    Id = SubjectIds.Vietnamese,
    DisplayName = "Tiếng Việt",
    Landmark = SubjectLandmarkKind.Scrolls,
    // Hub-arc slot: together with the other 4 gates in front of the spawn
    // camera (the old x=3.5 south-gate offset is obsolete — no gate stands
    // between the spawn camera and the player anymore).
    GatePos = new Vector3(7f, 0f, -5.5f),
    PlaygroundCenter = new Vector3(3.5f, 0f, 10.0f),
    EntryPoint = new Vector3(3.5f, 0f, -3.8f), // just past the gate toward the district
    ReturnPoint = new Vector3(5.8f, 0f, 10.0f),
    Primary = new Color(0.55f, 0.35f, 0.75f),
    Secondary = new Color(0.96f, 0.60f, 0.75f),
    GroundTint = new Color(0.70f, 0.60f, 0.82f),
  };

  // KHÁM PHÁ (FULL ARCHITECTURE RESET PHASE 2): the 5th subject's gate holds
  // the arc CENTER slot. SceneName marks it as an additive (no spatial
  // district) subject like Math: its selection content is the skill yard
  // (SelectionYardScene), never a dead playground — the product rule that
  // empty subjects must not grow fake content.
  public static readonly SubjectDefinition Exploration = new SubjectDefinition {
    Id = SubjectIds.Exploration,
    DisplayName = "Khám phá",
    Landmark = SubjectLandmarkKind.Compass,
    GatePos = new Vector3(0f, 0f, -5f),
    PlaygroundCenter = new Vector3(0f, 0f, -15.4f),
    EntryPoint = new Vector3(0f, 0f, -7f), // just past the gate toward the (additive) district
    ReturnPoint = new Vector3(0f, 0f, -13.6f),
    Primary = new Color(0.16f, 0.62f, 0.60f),   // teal: distinct from every other primary
    Secondary = new Color(0.98f, 0.86f, 0.55f), // warm sand
    GroundTint = new Color(0.62f, 0.82f, 0.78f),
    SceneName = "SelectionYardScene", // additive subject: no spatial district
  };

  public static readonly SubjectDefinition[] All =
    { Math, Thinking, English, Vietnamese, Exploration };

  public static readonly Dictionary<string, SubjectDefinition> ById =
    new Dictionary<string, SubjectDefinition> {
      { SubjectIds.Math.Value, Math },
      { SubjectIds.Thinking.Value, Thinking },
      { SubjectIds.English.Value, English },
      { SubjectIds.Vietnamese.Value, Vietnamese },
      { SubjectIds.Exploration.Value, Exploration },
    };

  public static SubjectDefinition Get(SubjectId id) {
    if (id.Value == null) return null;
    SubjectDefinition def;
    return ById.TryGetValue(id.Value, out def) ? def : null;
  }
}
