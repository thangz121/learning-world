// C_Content/QuestContentCatalog.cs — Phase 2A (content pipeline, ADDITIVE).
// QUEST DATA rather than QUEST CODE: one QuestContentEntry describes everything
// a quest needs (staging, vocabulary, dialogue, audio, hints, reward, next)
// so new content is configuration work, never gameplay duplication.
//
// Frozen systems are NOT modified: QuestManager keeps its IQuestContentProvider
// boundary (CatalogQuestProvider in _Bootstrap is the NEW provider, the
// BuiltIn mirror stays as fallback); QuestRewardService, ContentDatabase,
// presenters, camera, HUD all untouched. New code only READS Content/*.json
// through the existing ContentDatabase parsers.
//
// Conventions (documented, pinned by CT-P06):
//   chapterId: "w1" when the quest id starts with "w1_", else "market".
//   targetObject: first Find/Bring objective target (world prop id).
//   vocabulary: distinct objective targets (learning progression source).
//   audio: per-word normal/slow/voice/lang from the vocab entries.
//   completionEffect: mirrors rewardWorldChange (FlowerPotPresenter binds the
//     world-change id; Phase 2F may introduce distinct effects per quest).
//   nextQuest: "" = terminal for now (w1_mia_ball); the chain grows in 2F.
//   dialogueIds / wrongResponse / correctResponse: manifest line ids, supplied
//     by the caller (content authoring) and VALIDATED against the pack — the
//     catalog never guesses dialogue.
// C# 9.0 only. JsonUtility-compatible DTOs (no dicts in serialized shapes).
using System;
using System.Collections.Generic;

[Serializable]
public sealed class QuestContentObjective {
  public string id;
  public PlayerAction action;
  public string target;
}

[Serializable]
public sealed class WordAudioMap {
  public string word;
  public string normal;
  public string slow;
  public string voice;
  public string lang;
}

[Serializable]
public sealed class QuestContentEntry {
  public string questId;
  public string chapterId;
  public string npc;
  public QuestPattern pattern;
  public List<QuestContentObjective> objectives = new List<QuestContentObjective>();
  public string targetObject;
  public List<string> distractorObjects = new List<string>();
  public List<string> vocabulary = new List<string>();
  public List<string> dialogueIds = new List<string>();
  public string wrongResponse;
  public string correctResponse;
  public string completionEffect;
  public int rewardFriendship;
  public string rewardWorldChange;
  public string nextQuest;
  public List<WordAudioMap> audio = new List<WordAudioMap>();
}

public static class QuestContentCatalog {
  // Pure build: parsed Content DTOs in, staged entry out. No IO, no Unity
  // services — fully unit-testable (CT-P06 drives this with real files).
  public static QuestContentEntry BuildEntry(
      QuestEntry quest,
      List<VocabEntry> vocabs,
      DialoguePack pack,
      List<string> dialogueIds,
      string wrongResponse,
      string correctResponse,
      List<string> distractorObjects,
      string nextQuest) {
    if (quest == null) throw new ArgumentNullException("quest");
    var entry = new QuestContentEntry();
    entry.questId = quest.id;
    entry.chapterId = quest.id != null && quest.id.StartsWith("w1_") ? "w1" : "market";
    entry.npc = quest.npc;
    var actions = new List<PlayerAction>();
    var targets = new List<string>();
    if (quest.objectives != null) {
      foreach (QuestObjective o in quest.objectives) {
        if (o == null) continue;
        actions.Add(o.action);
        targets.Add(o.target);
        entry.objectives.Add(new QuestContentObjective { id = o.id, action = o.action, target = o.target });
      }
    }
    entry.pattern = QuestPatternDef.Classify(actions, targets);
    entry.targetObject = FirstFindOrBringTarget(quest);
    if (distractorObjects != null) entry.distractorObjects.AddRange(distractorObjects);
    var seen = new HashSet<string>();
    foreach (string t in targets) {
      if (string.IsNullOrEmpty(t) || !seen.Add(t)) continue;
      entry.vocabulary.Add(t);
    }
    if (dialogueIds != null) entry.dialogueIds.AddRange(dialogueIds);
    entry.wrongResponse = wrongResponse;
    entry.correctResponse = correctResponse;
    entry.rewardFriendship = quest.rewardFriendship;
    entry.rewardWorldChange = quest.rewardWorldChange;
    entry.completionEffect = quest.rewardWorldChange;
    entry.nextQuest = nextQuest ?? "";
    var vocabById = new Dictionary<string, VocabEntry>();
    if (vocabs != null) {
      foreach (VocabEntry v in vocabs) {
        if (v != null && !string.IsNullOrEmpty(v.id) && !vocabById.ContainsKey(v.id))
          vocabById[v.id] = v;
      }
    }
    foreach (string word in entry.vocabulary) {
      VocabEntry v;
      if (!vocabById.TryGetValue(word, out v) || v == null) continue;
      entry.audio.Add(new WordAudioMap {
        word = word, normal = v.audioNormal, slow = v.audioSlow,
        voice = v.audioVoice, lang = v.audioLang,
      });
    }
    return entry;
  }

  static string FirstFindOrBringTarget(QuestEntry quest) {
    if (quest.objectives == null) return "";
    foreach (QuestObjective o in quest.objectives) {
      if (o == null) continue;
      if ((o.action == PlayerAction.Find || o.action == PlayerAction.Bring)
          && !string.IsNullOrEmpty(o.target)) return o.target;
    }
    if (quest.objectives.Count > 0 && quest.objectives[0] != null)
      return quest.objectives[0].target ?? "";
    return "";
  }

  // Authoring-time validation: returns every problem found (empty = clean).
  // Mirrors the Content/ freeze rules (CT-008 + validate_content.py) plus the
  // catalog's own cross-links (dialogue/audio/vocab resolve).
  public static List<string> Validate(QuestContentEntry entry, DialoguePack pack) {
    var errors = new List<string>();
    if (entry == null) { errors.Add("entry is null"); return errors; }
    if (string.IsNullOrEmpty(entry.questId)) errors.Add("questId missing");
    if (string.IsNullOrEmpty(entry.chapterId)) errors.Add(entry.questId + ": chapterId missing");
    if (string.IsNullOrEmpty(entry.npc)) errors.Add(entry.questId + ": npc missing");
    if (entry.pattern == QuestPattern.Unknown) errors.Add(entry.questId + ": pattern Unknown");
    if (entry.objectives == null || entry.objectives.Count == 0)
      errors.Add(entry.questId + ": no objectives");
    if (string.IsNullOrEmpty(entry.targetObject))
      errors.Add(entry.questId + ": targetObject missing");
    if (string.IsNullOrEmpty(entry.rewardWorldChange))
      errors.Add(entry.questId + ": rewardWorldChange missing");
    var lineIds = new HashSet<string>();
    if (pack != null && pack.lines != null) {
      foreach (DialogueLine l in pack.lines) {
        if (l != null && !string.IsNullOrEmpty(l.id)) lineIds.Add(l.id);
      }
    }
    if (entry.dialogueIds != null) {
      foreach (string d in entry.dialogueIds) {
        if (!lineIds.Contains(d)) errors.Add(entry.questId + ": dialogue '" + d + "' not in pack");
      }
    }
    if (!string.IsNullOrEmpty(entry.wrongResponse) && !lineIds.Contains(entry.wrongResponse))
      errors.Add(entry.questId + ": wrongResponse '" + entry.wrongResponse + "' not in pack");
    if (!string.IsNullOrEmpty(entry.correctResponse) && !lineIds.Contains(entry.correctResponse))
      errors.Add(entry.questId + ": correctResponse '" + entry.correctResponse + "' not in pack");
    var audioWords = new HashSet<string>();
    if (entry.audio != null) {
      foreach (WordAudioMap m in entry.audio) {
        if (m == null) continue;
        audioWords.Add(m.word);
        if (string.IsNullOrEmpty(m.normal) || string.IsNullOrEmpty(m.slow))
          errors.Add(entry.questId + ": word '" + m.word + "' missing audio mapping");
        if (m.voice != "learning_v1" || m.lang != "en-US")
          errors.Add(entry.questId + ": word '" + m.word + "' voice/lang not frozen (learning_v1/en-US)");
      }
    }
    if (entry.vocabulary != null) {
      foreach (string w in entry.vocabulary) {
        if (!audioWords.Contains(w)) errors.Add(entry.questId + ": vocab '" + w + "' has no audio map");
      }
    }
    return errors;
  }
}
