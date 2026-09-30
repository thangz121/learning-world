// _SharedKernel/LearningMap.cs — FULL ARCHITECTURE RESET (2026-09-29).
// The product taxonomy: SUBJECT -> SKILL -> GAME, three distinct entities.
// Pure C# 9 (no UnityEngine): EditMode-testable without a scene, and the
// selection yards read ONLY this map for labels, counts and status.
//
// Product Owner baseline (absolute):
//   HUMAN_ACCEPTED games = EXACTLY "Cho thỏ ăn" (rabbit_feeding) and
//   "Bậc thang con số" (number_stairs). Every other game/skill/subject is
//   NOT human accepted; engineering status (build/test PASS) never implies it.
//
// Status vocabulary (fixed): Unplanned / Skeleton / Implemented /
// EngineeringVerified / HumanAccepted / Rejected. "Live" is never used to
// imply acceptance.
// C# 9.0 only.
using System;
using System.Collections.Generic;

public enum GameStatus {
  Unplanned,
  Skeleton,
  Implemented,
  EngineeringVerified,
  HumanAccepted,
  Rejected,
}

// One skill inside a subject (e.g. math -> counting).
public sealed class SkillEntry {
  public readonly string Id;
  public readonly string SubjectId;
  public readonly string DisplayName;
  public SkillEntry(string id, string subjectId, string displayName) {
    Id = id; SubjectId = subjectId; DisplayName = displayName;
  }
}

// One game inside a skill. SceneName empty = no gameplay yet (empty skill).
public sealed class GameEntry {
  public readonly string Id;
  public readonly string SkillId;
  public readonly string DisplayName;
  public readonly string SceneName;
  public readonly GameStatus Status;
  public GameEntry(string id, string skillId, string displayName, string sceneName, GameStatus status) {
    Id = id; SkillId = skillId; DisplayName = displayName; SceneName = sceneName; Status = status;
  }
  // Playable in the product sense: has a real scene AND was human accepted.
  // (EngineeringVerified without acceptance must never present as playable.)
  public bool HumanAccepted { get { return Status == GameStatus.HumanAccepted; } }
  public bool HasArena { get { return !string.IsNullOrEmpty(SceneName); } }
}

// One subject (Toán học / Tư duy / Tiếng Việt / Tiếng Anh / Khám phá).
public sealed class SubjectEntry {
  public readonly string Id;
  public readonly string DisplayName;
  public readonly SkillEntry[] Skills;
  public SubjectEntry(string id, string displayName, SkillEntry[] skills) {
    Id = id; DisplayName = displayName; Skills = skills;
  }
}

public static class LearningMap {
  // Subject ids (stable product taxonomy; raw MicroWorld ids are NOT taxonomy).
  public const string MathId = "math";
  public const string ThinkingId = "thinking";
  public const string VietnameseId = "vietnamese";
  public const string EnglishId = "english";
  public const string ExplorationId = "exploration";

  // Skill ids (globally unique; subject-prefixed on purpose).
  public const string MathCounting = "math_counting";
  public const string MathGeometry = "math_geometry";
  public const string MathComparison = "math_comparison";
  public const string MathClassification = "math_classification";
  public const string MathOrder = "math_order";
  public const string ThinkingPattern = "thinking_pattern";
  public const string ThinkingLogic = "thinking_logic";
  public const string ThinkingProblemSolving = "thinking_problem_solving";
  public const string ThinkingMemory = "thinking_memory";
  public const string VietnameseListening = "vietnamese_listening";
  public const string VietnameseSpeaking = "vietnamese_speaking";
  public const string VietnameseLetters = "vietnamese_letters";
  public const string VietnamesePreLiteracy = "vietnamese_pre_literacy";
  public const string EnglishListening = "english_listening";
  public const string EnglishSpeaking = "english_speaking";
  public const string EnglishVocabulary = "english_vocabulary";
  public const string EnglishSentences = "english_sentences";
  public const string ExplorationNature = "exploration_nature";
  public const string ExplorationAnimals = "exploration_animals";
  public const string ExplorationWorld = "exploration_world";
  public const string ExplorationDailyLife = "exploration_daily_life";

  // Game ids. HUMAN_ACCEPTED stays exactly the two counting games.
  public const string RabbitFeedingGame = "rabbit_feeding";
  public const string NumberStairsGame = "number_stairs";
  public const string ShapeBuilderGame = "shape_builder";
  public const string ComparisonMarketGame = "comparison_market";

  public static readonly SkillEntry[] MathSkills = {
    new SkillEntry(MathCounting, MathId, "Đếm"),
    new SkillEntry(MathGeometry, MathId, "Hình học"),
    new SkillEntry(MathComparison, MathId, "So sánh"),
    new SkillEntry(MathClassification, MathId, "Phân loại"),
    new SkillEntry(MathOrder, MathId, "Thứ tự"),
  };
  public static readonly SkillEntry[] ThinkingSkills = {
    new SkillEntry(ThinkingPattern, ThinkingId, "Quy luật"),
    new SkillEntry(ThinkingLogic, ThinkingId, "Logic"),
    new SkillEntry(ThinkingProblemSolving, ThinkingId, "Giải quyết vấn đề"),
    new SkillEntry(ThinkingMemory, ThinkingId, "Trí nhớ"),
  };
  public static readonly SkillEntry[] VietnameseSkills = {
    new SkillEntry(VietnameseListening, VietnameseId, "Nghe"),
    new SkillEntry(VietnameseSpeaking, VietnameseId, "Nói"),
    new SkillEntry(VietnameseLetters, VietnameseId, "Làm quen chữ"),
    new SkillEntry(VietnamesePreLiteracy, VietnameseId, "Tiền đọc - tiền viết"),
  };
  public static readonly SkillEntry[] EnglishSkills = {
    new SkillEntry(EnglishListening, EnglishId, "Listening"),
    new SkillEntry(EnglishSpeaking, EnglishId, "Speaking"),
    new SkillEntry(EnglishVocabulary, EnglishId, "Vocabulary"),
    new SkillEntry(EnglishSentences, EnglishId, "Sentences"),
  };
  public static readonly SkillEntry[] ExplorationSkills = {
    new SkillEntry(ExplorationNature, ExplorationId, "Tự nhiên"),
    new SkillEntry(ExplorationAnimals, ExplorationId, "Động vật"),
    new SkillEntry(ExplorationWorld, ExplorationId, "Thế giới xung quanh"),
    new SkillEntry(ExplorationDailyLife, ExplorationId, "Đời sống"),
  };

  // The five subjects, in product order. MATH and THINKING stay separate.
  public static readonly SubjectEntry[] Subjects = {
    new SubjectEntry(MathId, "Toán học", MathSkills),
    new SubjectEntry(ThinkingId, "Tư duy", ThinkingSkills),
    new SubjectEntry(VietnameseId, "Tiếng Việt", VietnameseSkills),
    new SubjectEntry(EnglishId, "Tiếng Anh", EnglishSkills),
    new SubjectEntry(ExplorationId, "Khám phá", ExplorationSkills),
  };

  // Scene names kept EXACTLY (approved arenas; gameplay frozen).
  public const string RabbitFeedingScene = "RabbitPlayScene";
  public const string NumberStairsScene = "StairPlayScene";
  public const string ShapeBuilderScene = "GeometryPlayScene";
  public const string ComparisonMarketScene = "ComparisonMarketPlayScene";

  // Counting games stay HUMAN_ACCEPTED. Geometry is IMPLEMENTED only.
  public static readonly GameEntry[] Games = {
    new GameEntry(RabbitFeedingGame, MathCounting, "Cho thỏ ăn",
      RabbitFeedingScene, GameStatus.HumanAccepted),
    new GameEntry(NumberStairsGame, MathCounting, "Bậc thang con số",
      NumberStairsScene, GameStatus.HumanAccepted),
    new GameEntry(ShapeBuilderGame, MathGeometry, "Lắp Hình Vui Nhộn",
      ShapeBuilderScene, GameStatus.Implemented),
    new GameEntry(ComparisonMarketGame, MathComparison, "Khu Chợ Của Bé",
      ComparisonMarketScene, GameStatus.Implemented),
  };

  // ---- lookups ------------------------------------------------------------------

  public static SubjectEntry Subject(string subjectId) {
    for (int i = 0; i < Subjects.Length; i++)
      if (Same(Subjects[i].Id, subjectId)) return Subjects[i];
    return null;
  }

  public static SkillEntry Skill(string skillId) {
    for (int i = 0; i < Subjects.Length; i++) {
      SkillEntry[] skills = Subjects[i].Skills;
      for (int s = 0; s < skills.Length; s++)
        if (Same(skills[s].Id, skillId)) return skills[s];
    }
    return null;
  }

  public static SkillEntry[] SkillsOf(string subjectId) {
    SubjectEntry subject = Subject(subjectId);
    return subject != null ? subject.Skills : new SkillEntry[0];
  }

  public static GameEntry Game(string gameId) {
    for (int i = 0; i < Games.Length; i++)
      if (Same(Games[i].Id, gameId)) return Games[i];
    return null;
  }

  public static GameEntry[] GamesOf(string skillId) {
    List<GameEntry> hits = new List<GameEntry>();
    for (int i = 0; i < Games.Length; i++)
      if (Same(Games[i].SkillId, skillId)) hits.Add(Games[i]);
    return hits.ToArray();
  }

  public static SkillEntry SkillOfGame(string gameId) {
    GameEntry game = Game(gameId);
    return game != null ? Skill(game.SkillId) : null;
  }

  public static SubjectEntry SubjectOfSkill(string skillId) {
    SkillEntry skill = Skill(skillId);
    return skill != null ? Subject(skill.SubjectId) : null;
  }

  // Display helpers for the yards (Vietnamese-first, as the product speaks).
  public static string SkillDisplay(string skillId) {
    SkillEntry skill = Skill(skillId);
    return skill != null ? skill.DisplayName : "";
  }
  public static string SubjectDisplay(string subjectId) {
    SubjectEntry subject = Subject(subjectId);
    return subject != null ? subject.DisplayName : "";
  }
  public static string GameDisplay(string gameId) {
    GameEntry game = Game(gameId);
    return game != null ? game.DisplayName : "";
  }

  // Yard titles (single source: the orientation sign in both yard levels).
  public static string SkillYardTitle(string subjectId) {
    return SubjectDisplay(subjectId) + " — Chọn kỹ năng";
  }
  public static string GameYardTitle(string skillId) {
    return SkillDisplay(skillId) + " — Chọn trò chơi";
  }
  public const string EmptyYardText = "CHƯA CÓ TRÒ CHƠI";

  // Product firewall helper (used by tests): a game may only be presented as
  // playable when it is HUMAN_ACCEPTED and has an arena scene.
  public static bool IsPlayable(string gameId) {
    GameEntry game = Game(gameId);
    return game != null && game.HumanAccepted && game.HasArena;
  }

  // IMPLEMENTED games with an arena may be entered for play (never claimed accepted).
  public static bool CanLaunch(string gameId) {
    GameEntry game = Game(gameId);
    if (game == null || !game.HasArena) return false;
    return game.Status == GameStatus.HumanAccepted
      || game.Status == GameStatus.Implemented
      || game.Status == GameStatus.EngineeringVerified;
  }

  static bool Same(string a, string b) {
    return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
  }
}
