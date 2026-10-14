// Tests/EditMode/CT-S23_EnglishNewArenas.cs — Tests for WonderKitchen & SentenceExpress.
// Pins taxonomy, arena construction, gameplay loops, and progress flow.
// C# 9.0 only.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class CT_S23_EnglishNewArenas {
  // A. Taxonomy registration
  [Test] public void S23A_TaxonomyRegistration() {
    GameEntry kitchen = LearningMap.Game("wonder_kitchen");
    Assert.IsNotNull(kitchen, "wonder_kitchen registered");
    Assert.AreEqual("english_vocabulary", kitchen.SkillId);
    Assert.AreEqual("WonderKitchenPlayScene", kitchen.SceneName);
    Assert.AreEqual(GameStatus.Implemented, kitchen.Status);
    Assert.IsFalse(kitchen.HumanAccepted);
    Assert.IsTrue(LearningMap.CanLaunch("wonder_kitchen"));

    GameEntry sentences = LearningMap.Game("sentence_express");
    Assert.IsNotNull(sentences, "sentence_express registered");
    Assert.AreEqual("english_sentences", sentences.SkillId);
    Assert.AreEqual("SentenceExpressPlayScene", sentences.SceneName);
    Assert.AreEqual(GameStatus.Implemented, sentences.Status);
    Assert.IsFalse(sentences.HumanAccepted);
    Assert.IsTrue(LearningMap.CanLaunch("sentence_express"));
  }

  // B. Wonder Kitchen Arena & Gameplay
  [Test] public void S23B_WonderKitchenGameplay() {
    GameObject arena = new GameObject("TestKitchenArena");
    try {
      WonderKitchenBuilder builder = arena.AddComponent<WonderKitchenBuilder>();
      builder.BuildContent(arena.transform);

      WonderKitchen game = arena.AddComponent<WonderKitchen>();
      game.Build(builder, null, null, null, new ActivityLifecycle("wonder_kitchen", "test"));

      Assert.AreEqual(WonderKitchen.Phase.Wait, game.Current);
      game.BeginRound();
      Assert.AreEqual(WonderKitchen.Phase.Play, game.Current);
      Assert.AreEqual(1, game.Level);
      Assert.AreEqual(2, builder.Choices.Count, "Level 1 has 2 choices");

      WonderKitchenChoice targetChoice = null;
      WonderKitchenChoice wrongChoice = null;
      for (int i = 0; i < builder.Choices.Count; i++) {
        if (builder.Choices[i].IsTarget) targetChoice = builder.Choices[i];
        else wrongChoice = builder.Choices[i];
      }
      Assert.IsNotNull(targetChoice, "target food exists");
      Assert.IsNotNull(wrongChoice, "wrong food exists");

      // Wrong choice
      game.TrySelectChoice(wrongChoice);
      Assert.AreEqual(1, game.WrongCount);
      Assert.IsFalse(game.LastCorrect);

      // Target choice
      game.TrySelectChoice(targetChoice);
      Assert.IsTrue(game.LastCorrect);
      Assert.AreEqual(1, game.RoundIndex);
    } finally {
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  // C. Sentence Express Arena & 3-Stage Curriculum
  [Test] public void S23C_SentenceExpressCurriculum() {
    GameObject arena = new GameObject("TestSentenceArena");
    try {
      SentenceExpressBuilder builder = arena.AddComponent<SentenceExpressBuilder>();
      builder.BuildContent(arena.transform);

      SentenceExpress game = arena.AddComponent<SentenceExpress>();
      game.Build(builder, null, null, null, new ActivityLifecycle("sentence_express", "test"));

      // Level 1: Letters (C-A-R)
      game.BeginRound();
      Assert.AreEqual(1, game.Level);
      Assert.AreEqual(3, builder.Choices.Count, "CAR has 3 letter cars");
      Assert.AreEqual("C", builder.Choices[0].CardText);
      Assert.AreEqual("A", builder.Choices[1].CardText);
      Assert.AreEqual("R", builder.Choices[2].CardText);
      Assert.AreEqual(4, builder.LetterChoices.Count, "4 choices pill generated");

      // Verify missing letter
      Assert.AreEqual(1, game.MissingIndex);
      Assert.AreEqual("A", game.MissingLetter);

      // Find wrong choice and correct choice
      SentenceLetterChoice wrongChoice = builder.LetterChoices.Find(c => !c.IsCorrect);
      SentenceLetterChoice correctChoice = builder.LetterChoices.Find(c => c.IsCorrect);
      Assert.IsNotNull(wrongChoice);
      Assert.IsNotNull(correctChoice);

      // Try selecting wrong choice
      game.TrySelectLetter(wrongChoice);
      Assert.AreEqual(1, game.WrongCount);

      // Select correct choice
      game.TrySelectLetter(correctChoice);
      Assert.IsTrue(game.LastCorrect);
      Assert.AreEqual(1, game.RoundIndex);

      // Test Level 2: 2-Word Collocations
      game.SetLevelForTests(2, 0);
      Assert.AreEqual(2, game.Level);
      Assert.AreEqual(2, builder.Choices.Count, "2-word phrase has 2 cars");
      Assert.AreEqual(4, builder.LetterChoices.Count);

      // Test Level 3: 3-Word Baby Sentences
      game.SetLevelForTests(3, 0);
      Assert.AreEqual(3, game.Level);
      Assert.AreEqual(3, builder.Choices.Count, "Baby sentence has 3 cars");
      Assert.AreEqual(4, builder.LetterChoices.Count);
    } finally {
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  // D. Selection Yard Doors & Launch
  [Test] public void S23D_YardDoorsAndPreviews() {
    GameObject yard = new GameObject("TestYard");
    try {
      SelectionYardBuilder b = yard.AddComponent<SelectionYardBuilder>();
      b.Level = "game";
      b.SkillId = "english_vocabulary";
      b.BuildContent(yard.transform);
      Assert.AreEqual(1, b.GatePortals.Count);
      Assert.AreEqual("wonder_kitchen", b.GateTargetIds[0]);

      // Check sentences door
      b.ClearRoundForTest();
      b.SkillId = "english_sentences";
      b.BuildContent(yard.transform);
      Assert.AreEqual(1, b.GatePortals.Count);
      Assert.AreEqual("sentence_express", b.GateTargetIds[0]);
    } finally {
      UnityEngine.Object.DestroyImmediate(yard);
    }
  }
}

public static class TestYardHelper {
  public static void ClearRoundForTest(this SelectionYardBuilder b) {
    for (int i = b.transform.childCount - 1; i >= 0; i--) {
      UnityEngine.Object.DestroyImmediate(b.transform.GetChild(i).gameObject);
    }
  }
}
