// CT-S10: FULL ARCHITECTURE RESET — the product taxonomy (Subject -> Skill ->
// Game). Pins the five subjects (Math and Thinking SEPARATE), the approved
// skill trees, the empty-skill rules, and the ABSOLUTE acceptance baseline:
// exactly two games exist and both are HUMAN_ACCEPTED.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;

public class CT_S10_LearningMap {
  // A. Five subjects, exact names, Math and Thinking separate.
  [Test] public void S10A_FiveSubjects() {
    Assert.AreEqual(5, LearningMap.Subjects.Length, "exactly five subjects");
    string[] ids = { "math", "thinking", "vietnamese", "english", "exploration" };
    string[] names = { "Toán học", "Tư duy", "Tiếng Việt", "Tiếng Anh", "Khám phá" };
    var seen = new HashSet<string>();
    for (int i = 0; i < 5; i++) {
      Assert.AreEqual(ids[i], LearningMap.Subjects[i].Id, "subject order " + i);
      Assert.AreEqual(names[i], LearningMap.Subjects[i].DisplayName, "subject name " + i);
      Assert.IsTrue(seen.Add(LearningMap.Subjects[i].Id), "subject ids unique");
    }
    Assert.AreNotEqual(LearningMap.MathId, LearningMap.ThinkingId,
      "Math and Thinking are two separate subjects");
    Assert.IsNotNull(LearningMap.Subject("math"), "math lookup");
    Assert.IsNotNull(LearningMap.Subject("exploration"), "exploration lookup");
    Assert.IsNull(LearningMap.Subject("nope"), "unknown subject is null");
  }

  // B. Approved skill trees (5/4/4/4/4), exact display names, unique ids.
  [Test] public void S10B_SkillTrees() {
    Assert.AreEqual(5, LearningMap.SkillsOf("math").Length, "math has 5 skills");
    Assert.AreEqual(4, LearningMap.SkillsOf("thinking").Length, "thinking has 4 skills");
    Assert.AreEqual(4, LearningMap.SkillsOf("vietnamese").Length, "vietnamese has 4 skills");
    Assert.AreEqual(4, LearningMap.SkillsOf("english").Length, "english has 4 skills");
    Assert.AreEqual(4, LearningMap.SkillsOf("exploration").Length, "exploration has 4 skills");
    int total = 0;
    var ids = new HashSet<string>();
    for (int i = 0; i < LearningMap.Subjects.Length; i++) {
      SkillEntry[] skills = LearningMap.Subjects[i].Skills;
      total += skills.Length;
      for (int s = 0; s < skills.Length; s++) {
        Assert.AreEqual(LearningMap.Subjects[i].Id, skills[s].SubjectId,
          "skill " + skills[s].Id + " belongs to its subject");
        Assert.IsTrue(ids.Add(skills[s].Id), "skill ids globally unique: " + skills[s].Id);
        Assert.IsFalse(string.IsNullOrEmpty(skills[s].DisplayName), "skill has a name");
      }
    }
    Assert.AreEqual(21, total, "21 skills total (invent nothing)");
    Assert.AreEqual("Đếm", LearningMap.SkillDisplay("math_counting"), "counting name");
    Assert.AreEqual("Hình học", LearningMap.SkillDisplay("math_geometry"), "geometry name");
    Assert.AreEqual("Tiền đọc - tiền viết", LearningMap.SkillDisplay("vietnamese_pre_literacy"),
      "pre-literacy name");
    Assert.AreEqual("Sentences", LearningMap.SkillDisplay("english_sentences"), "sentences name");
    Assert.AreEqual("Đời sống", LearningMap.SkillDisplay("exploration_daily_life"), "daily life name");
    Assert.IsNull(LearningMap.Skill("math_counting2"), "no invented skills");
    // Subject lookups through skills.
    Assert.AreEqual("math", LearningMap.SubjectOfSkill("math_order").Id, "skill->subject");
    Assert.AreEqual("counting", LearningMap.Skill("math_counting").Id.Replace("math_", ""),
      "counting lives under math");
  }

  // C. Game registry + ABSOLUTE acceptance baseline.
  [Test] public void S10C_AcceptedGamesOnly() {
    Assert.AreEqual(5, LearningMap.Games.Length, "two accepted + geometry + market + city");
    var accepted = new List<string>();
    for (int i = 0; i < LearningMap.Games.Length; i++)
      if (LearningMap.Games[i].HumanAccepted) accepted.Add(LearningMap.Games[i].Id);
    Assert.AreEqual(2, accepted.Count, "exactly two games are HUMAN_ACCEPTED");
    Assert.IsTrue(accepted.Contains("rabbit_feeding"), "rabbit feeding accepted");
    Assert.IsTrue(accepted.Contains("number_stairs"), "number stairs accepted");
    Assert.IsFalse(accepted.Contains("shape_builder"), "geometry is not accepted");
    Assert.IsFalse(accepted.Contains("comparison_market"), "market is not accepted");
    Assert.IsFalse(accepted.Contains("classification_city"), "city is not accepted");
    Assert.AreEqual("Cho thỏ ăn", LearningMap.GameDisplay("rabbit_feeding"), "rabbit display");
    Assert.AreEqual("Bậc thang con số", LearningMap.GameDisplay("number_stairs"), "stairs display");
    Assert.AreEqual("Lắp Hình Vui Nhộn", LearningMap.GameDisplay("shape_builder"), "geometry display");
    Assert.AreEqual("RabbitPlayScene", LearningMap.Game("rabbit_feeding").SceneName,
      "rabbit scene frozen");
    Assert.AreEqual("StairPlayScene", LearningMap.Game("number_stairs").SceneName,
      "stairs scene frozen");
    Assert.AreEqual("GeometryPlayScene", LearningMap.Game("shape_builder").SceneName,
      "geometry scene");
    Assert.AreEqual(GameStatus.Implemented, LearningMap.Game("shape_builder").Status,
      "geometry stays IMPLEMENTED");
    Assert.AreEqual("math_geometry", LearningMap.Game("shape_builder").SkillId,
      "geometry lives under Math -> Hình học");
    Assert.AreEqual(2, LearningMap.GamesOf("math_counting").Length, "counting owns 2 games");
    Assert.AreEqual(1, LearningMap.GamesOf("math_geometry").Length, "geometry owns 1 game");
    Assert.AreEqual("Khu Chợ Của Bé", LearningMap.GameDisplay("comparison_market"), "market display");
    Assert.AreEqual("ComparisonMarketPlayScene", LearningMap.Game("comparison_market").SceneName,
      "market scene");
    Assert.AreEqual(GameStatus.Implemented, LearningMap.Game("comparison_market").Status,
      "market stays IMPLEMENTED");
    Assert.AreEqual("math_comparison", LearningMap.Game("comparison_market").SkillId,
      "market lives under Math -> So sánh");
    Assert.AreEqual(1, LearningMap.GamesOf("math_comparison").Length, "comparison owns 1 game");
    Assert.AreEqual("Thành Phố Phân Loại", LearningMap.GameDisplay("classification_city"), "city display");
    Assert.AreEqual("ClassificationCityPlayScene", LearningMap.Game("classification_city").SceneName,
      "city scene");
    Assert.AreEqual(GameStatus.Implemented, LearningMap.Game("classification_city").Status,
      "city stays IMPLEMENTED");
    Assert.AreEqual("math_classification", LearningMap.Game("classification_city").SkillId,
      "city lives under Math -> Phân loại");
    Assert.AreEqual(1, LearningMap.GamesOf("math_classification").Length, "classification owns 1 game");
    for (int i = 0; i < LearningMap.Subjects.Length; i++) {
      SkillEntry[] skills = LearningMap.Subjects[i].Skills;
      for (int s = 0; s < skills.Length; s++) {
        if (skills[s].Id == "math_counting" || skills[s].Id == "math_geometry"
            || skills[s].Id == "math_comparison" || skills[s].Id == "math_classification") continue;
        Assert.AreEqual(0, LearningMap.GamesOf(skills[s].Id).Length,
          "no fake games under " + skills[s].Id);
      }
    }
    Assert.IsTrue(LearningMap.IsPlayable("rabbit_feeding"), "rabbit playable");
    Assert.IsTrue(LearningMap.IsPlayable("number_stairs"), "stairs playable");
    Assert.IsFalse(LearningMap.IsPlayable("shape_builder"), "implemented is not HUMAN_ACCEPTED");
    Assert.IsTrue(LearningMap.CanLaunch("shape_builder"), "implemented arena may be entered");
    Assert.IsFalse(LearningMap.IsPlayable("comparison_market"), "market is not HUMAN_ACCEPTED");
    Assert.IsTrue(LearningMap.CanLaunch("comparison_market"), "market arena may be entered");
    Assert.IsFalse(LearningMap.IsPlayable("classification_city"), "city is not HUMAN_ACCEPTED");
    Assert.IsTrue(LearningMap.CanLaunch("classification_city"), "city arena may be entered");
    Assert.IsFalse(LearningMap.IsPlayable("anything_else"), "unknown is never playable");
  }

  // D. Yard titles + empty-yard text (single source for the orientation signs).
  [Test] public void S10D_YardTitles() {
    Assert.AreEqual("Toán học — Chọn kỹ năng", LearningMap.SkillYardTitle("math"), "math skill title");
    Assert.AreEqual("Khám phá — Chọn kỹ năng", LearningMap.SkillYardTitle("exploration"),
      "exploration skill title");
    Assert.AreEqual("Đếm — Chọn trò chơi", LearningMap.GameYardTitle("math_counting"), "counting game title");
    Assert.AreEqual("Hình học — Chọn trò chơi", LearningMap.GameYardTitle("math_geometry"),
      "empty skill still has a game yard title");
    Assert.AreEqual("CHƯA CÓ TRÒ CHƠI", LearningMap.EmptyYardText, "empty-yard text pinned");
  }
}
