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
    Assert.AreEqual(4, LearningMap.SkillsOf("math").Length, "math has 4 skills (order deleted)");
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
    Assert.AreEqual(20, total, "20 skills total (order deleted by owner; invent nothing)");
    Assert.AreEqual("Đếm", LearningMap.SkillDisplay("math_counting"), "counting name");
    Assert.AreEqual("Hình học", LearningMap.SkillDisplay("math_geometry"), "geometry name");
    Assert.AreEqual("Tiền đọc - tiền viết", LearningMap.SkillDisplay("vietnamese_pre_literacy"),
      "pre-literacy name");
    Assert.AreEqual("Sentences", LearningMap.SkillDisplay("english_sentences"), "sentences name");
    Assert.AreEqual("Đời sống", LearningMap.SkillDisplay("exploration_daily_life"), "daily life name");
    Assert.IsNull(LearningMap.Skill("math_counting2"), "no invented skills");
    // Subject lookups through skills.
    Assert.IsNull(LearningMap.Skill("math_order"), "order skill deleted (owner, 2026-10-13)");
    Assert.IsNull(LearningMap.Skill("ordering_station"), "ordering game id deleted (owner)");
    Assert.AreEqual("counting", LearningMap.Skill("math_counting").Id.Replace("math_", ""),
      "counting lives under math");
  }

  // C. Game registry + ABSOLUTE acceptance baseline.
  [Test] public void S10C_AcceptedGamesOnly() {
    Assert.AreEqual(11, LearningMap.Games.Length, "two accepted + geometry + market + city + 4 english + 2 vietnamese games");
    var accepted = new List<string>();
    for (int i = 0; i < LearningMap.Games.Length; i++)
      if (LearningMap.Games[i].HumanAccepted) accepted.Add(LearningMap.Games[i].Id);
    Assert.AreEqual(2, accepted.Count, "exactly two games are HUMAN_ACCEPTED");
    Assert.IsTrue(accepted.Contains("rabbit_feeding"), "rabbit feeding accepted");
    Assert.IsTrue(accepted.Contains("number_stairs"), "number stairs accepted");
    Assert.IsFalse(accepted.Contains("shape_builder"), "geometry is not accepted");
    Assert.IsFalse(accepted.Contains("comparison_market"), "market is not accepted");
    Assert.IsFalse(accepted.Contains("classification_city"), "city is not accepted");
    Assert.IsFalse(accepted.Contains("listen_garden"), "listening is not accepted");
    Assert.IsFalse(accepted.Contains("speaking_stage"), "speaking is not accepted");
    Assert.IsFalse(accepted.Contains("wonder_kitchen"), "kitchen is not accepted");
    Assert.IsFalse(accepted.Contains("sentence_express"), "sentences is not accepted");
    Assert.IsFalse(accepted.Contains("spelling_garden"), "spelling is not accepted");
    Assert.IsFalse(accepted.Contains("discovery_garden"), "discovery is not accepted");
    Assert.IsFalse(accepted.Contains("ordering_station"), "station is not accepted");
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
    Assert.AreEqual("Vườn Tai Thính", LearningMap.GameDisplay("listen_garden"), "listening display");
    Assert.AreEqual("ListeningGardenPlayScene", LearningMap.Game("listen_garden").SceneName,
      "listening scene");
    Assert.AreEqual(GameStatus.Implemented, LearningMap.Game("listen_garden").Status,
      "listening stays IMPLEMENTED");
    Assert.AreEqual("english_listening", LearningMap.Game("listen_garden").SkillId,
      "listening lives under English -> Listening");
    Assert.AreEqual(1, LearningMap.GamesOf("english_listening").Length, "listening owns 1 game");
    Assert.AreEqual("english", LearningMap.SubjectOfSkill("english_listening").Id,
      "listening skill belongs to the English subject");
    Assert.AreEqual("Sân Khấu Nhí", LearningMap.GameDisplay("speaking_stage"), "speaking display");
    Assert.AreEqual("SpeakingStagePlayScene", LearningMap.Game("speaking_stage").SceneName,
      "speaking scene");
    Assert.AreEqual(GameStatus.Implemented, LearningMap.Game("speaking_stage").Status,
      "speaking stays IMPLEMENTED");
    Assert.AreEqual("english_speaking", LearningMap.Game("speaking_stage").SkillId,
      "speaking lives under English -> Speaking");
    Assert.AreEqual(1, LearningMap.GamesOf("english_speaking").Length, "speaking owns 1 game");
    Assert.AreEqual("Căn Bếp Thần Kỳ", LearningMap.GameDisplay("wonder_kitchen"), "kitchen display");
    Assert.AreEqual("WonderKitchenPlayScene", LearningMap.Game("wonder_kitchen").SceneName,
      "kitchen scene");
    Assert.AreEqual(GameStatus.Implemented, LearningMap.Game("wonder_kitchen").Status,
      "kitchen stays IMPLEMENTED");
    Assert.AreEqual("english_vocabulary", LearningMap.Game("wonder_kitchen").SkillId,
      "kitchen lives under English -> Vocabulary");
    Assert.AreEqual(1, LearningMap.GamesOf("english_vocabulary").Length, "vocabulary owns 1 game");
    Assert.AreEqual("Đoàn Tàu Ghép Câu", LearningMap.GameDisplay("sentence_express"), "sentences display");
    Assert.AreEqual("SentenceExpressPlayScene", LearningMap.Game("sentence_express").SceneName,
      "sentences scene");
    Assert.AreEqual(GameStatus.Implemented, LearningMap.Game("sentence_express").Status,
      "sentences stays IMPLEMENTED");
    Assert.AreEqual("english_sentences", LearningMap.Game("sentence_express").SkillId,
      "sentences lives under English -> Sentences");
    Assert.AreEqual(1, LearningMap.GamesOf("english_sentences").Length, "sentences owns 1 game");
    // Ga Thứ Tự (ordering_station) DELETED 2026-10-13 (owner order): the game
    // AND the skill are gone from the map; the door no longer exists anywhere.
    Assert.IsNull(LearningMap.Game("ordering_station"), "station deleted");
    Assert.IsNull(LearningMap.Skill("math_order"), "order skill deleted; math owns 4 skills");
    for (int i = 0; i < LearningMap.Subjects.Length; i++) {
      SkillEntry[] skills = LearningMap.Subjects[i].Skills;
      for (int s = 0; s < skills.Length; s++) {
        if (skills[s].Id == "math_counting" || skills[s].Id == "math_geometry"
            || skills[s].Id == "math_comparison" || skills[s].Id == "math_classification"
            || skills[s].Id == "english_listening" || skills[s].Id == "english_speaking"
            || skills[s].Id == "english_vocabulary" || skills[s].Id == "english_sentences"
            || skills[s].Id == "vietnamese_letters" || skills[s].Id == "vietnamese_pre_literacy") continue;
        Assert.AreEqual(0, LearningMap.GamesOf(skills[s].Id).Length,
          "no fake games under " + skills[s].Id);
      }
    }
    Assert.AreEqual(1, LearningMap.GamesOf("vietnamese_letters").Length, "letters owns 1 game");
    Assert.AreEqual(1, LearningMap.GamesOf("vietnamese_pre_literacy").Length, "pre-literacy owns 1 game");
    Assert.IsTrue(LearningMap.IsPlayable("rabbit_feeding"), "rabbit playable");
    Assert.IsTrue(LearningMap.IsPlayable("number_stairs"), "stairs playable");
    Assert.IsFalse(LearningMap.IsPlayable("shape_builder"), "implemented is not HUMAN_ACCEPTED");
    Assert.IsTrue(LearningMap.CanLaunch("shape_builder"), "implemented arena may be entered");
    Assert.IsFalse(LearningMap.IsPlayable("comparison_market"), "market is not HUMAN_ACCEPTED");
    Assert.IsTrue(LearningMap.CanLaunch("comparison_market"), "market arena may be entered");
    Assert.IsFalse(LearningMap.IsPlayable("classification_city"), "city is not HUMAN_ACCEPTED");
    Assert.IsTrue(LearningMap.CanLaunch("classification_city"), "city arena may be entered");
    Assert.IsFalse(LearningMap.IsPlayable("listen_garden"), "listening is not HUMAN_ACCEPTED");
    Assert.IsTrue(LearningMap.CanLaunch("listen_garden"), "listening arena may be entered");
    Assert.IsFalse(LearningMap.IsPlayable("speaking_stage"), "speaking is not HUMAN_ACCEPTED");
    Assert.IsTrue(LearningMap.CanLaunch("speaking_stage"), "speaking arena may be entered");
    Assert.IsFalse(LearningMap.IsPlayable("wonder_kitchen"), "kitchen is not HUMAN_ACCEPTED");
    Assert.IsTrue(LearningMap.CanLaunch("wonder_kitchen"), "kitchen arena may be entered");
    Assert.IsFalse(LearningMap.IsPlayable("sentence_express"), "sentences is not HUMAN_ACCEPTED");
    Assert.IsTrue(LearningMap.CanLaunch("sentence_express"), "sentences arena may be entered");
    Assert.IsFalse(LearningMap.IsPlayable("spelling_garden"), "spelling is not HUMAN_ACCEPTED");
    Assert.IsTrue(LearningMap.CanLaunch("spelling_garden"), "spelling arena may be entered");
    Assert.IsFalse(LearningMap.IsPlayable("discovery_garden"), "discovery is not HUMAN_ACCEPTED");
    Assert.IsTrue(LearningMap.CanLaunch("discovery_garden"), "discovery arena may be entered");
    Assert.IsFalse(LearningMap.IsPlayable("ordering_station"), "closed station is not playable");
    Assert.IsFalse(LearningMap.CanLaunch("ordering_station"), "closed station cannot be entered");
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
