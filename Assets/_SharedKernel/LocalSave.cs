// _SharedKernel/LocalSave.cs — Lead owns. ISaveService file implementation.
// Frozen contract: ARCHITECTURE.md §3 skeleton (`Save = new LocalSave()`) +
// §9 "Slice: LocalSave JSON (words: {id: stage+score}, questsDone, playTime)"
// + Services.md ISaveService (Save/Load) + Part E (save.npcVoices, worldSeed).
// Storage: JSON file in Application.persistentDataPath. JsonUtility cannot
// serialize Dictionary, so PlayerProgress maps to a plain DTO (words list,
// questsDone list, playTimeSec, npcVoices pairs, worldSeed).
// Load is fail-soft by design: missing/corrupt file returns a fresh
// PlayerProgress instead of throwing (voice/quest flows must never crash on
// save IO; NpcVoiceProfileSelector already null-tolerates).
// EXPLICIT W0 CHOICE (not silent): file name "lwe_save.json" is not pinned in
// contracts — pass a name via the overload if Lead freezes a different one.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class LocalSave : ISaveService {
  public const string DefaultFileName = "lwe_save.json";

  readonly string _fileName;

  public LocalSave() : this(DefaultFileName) { }

  public LocalSave(string fileName) {
    _fileName = string.IsNullOrEmpty(fileName) ? DefaultFileName : fileName;
  }

  public string FilePath => Path.Combine(Application.persistentDataPath, _fileName);

  public void Save(PlayerProgress p) {
    if (p == null) throw new ArgumentNullException(nameof(p));
    string json = JsonUtility.ToJson(ToDto(p));
    string path = FilePath;
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    File.WriteAllText(path, json);
  }

  public PlayerProgress Load() {
    try {
      string path = FilePath;
      if (!File.Exists(path)) return new PlayerProgress();
      string json = File.ReadAllText(path);
      if (string.IsNullOrEmpty(json)) return new PlayerProgress();
      LocalSaveDto dto = JsonUtility.FromJson<LocalSaveDto>(json);
      if (dto == null) return new PlayerProgress();
      return FromDto(dto);
    } catch (Exception) {
      return new PlayerProgress(); // corrupt/unreadable save never blocks gameplay
    }
  }

  static LocalSaveDto ToDto(PlayerProgress p) {
    var dto = new LocalSaveDto {
      questsDone = p.QuestsDone != null ? new List<string>(p.QuestsDone) : new List<string>(),
      playTimeSec = p.PlayTimeSec,
      worldSeed = p.WorldSeed ?? "",
      words = new List<WordEntry>(),
      npcVoices = new List<KvEntry>(),
    };
    if (p.Words != null) {
      foreach (var kv in p.Words) {
        if (kv.Value == null) continue;
        WordMastery w = kv.Value;
        dto.words.Add(new WordEntry {
          wordId = kv.Key ?? w.Id.Value ?? "",
          stage = (int)w.Stage,
          exposure = w.Exposure,
          recognitionHit = w.RecognitionHit,
          recognitionTotal = w.RecognitionTotal,
          speakingHit = w.SpeakingHit,
          speakingTotal = w.SpeakingTotal,
          contextUse = w.ContextUse,
          nextReviewTicks = w.NextReview.Ticks,
          score = w.Score,
        });
      }
    }
    if (p.NpcVoices != null) {
      foreach (var kv in p.NpcVoices) {
        dto.npcVoices.Add(new KvEntry { k = kv.Key ?? "", v = kv.Value ?? "" });
      }
    }
    return dto;
  }

  static PlayerProgress FromDto(LocalSaveDto dto) {
    var p = new PlayerProgress {
      QuestsDone = dto.questsDone ?? new List<string>(),
      PlayTimeSec = dto.playTimeSec,
      WorldSeed = dto.worldSeed ?? "",
      Words = new Dictionary<string, WordMastery>(),
      NpcVoices = new Dictionary<string, string>(),
    };
    if (dto.words != null) {
      foreach (WordEntry e in dto.words) {
        if (e == null || string.IsNullOrEmpty(e.wordId)) continue;
        var mastery = new WordMastery {
          Id = new WordId(e.wordId),
          Stage = Enum.IsDefined(typeof(MasteryStage), e.stage)
            ? (MasteryStage)e.stage : MasteryStage.Unknown,
          Exposure = e.exposure,
          RecognitionHit = e.recognitionHit,
          RecognitionTotal = e.recognitionTotal,
          SpeakingHit = e.speakingHit,
          SpeakingTotal = e.speakingTotal,
          ContextUse = e.contextUse,
          Score = e.score,
        };
        try { mastery.NextReview = new DateTime(e.nextReviewTicks, DateTimeKind.Utc); }
        catch (Exception) { mastery.NextReview = default(DateTime); }
        p.Words[e.wordId] = mastery;
      }
    }
    if (dto.npcVoices != null) {
      foreach (KvEntry kv in dto.npcVoices) {
        if (kv == null || string.IsNullOrEmpty(kv.k)) continue;
        p.NpcVoices[kv.k] = kv.v ?? "";
      }
    }
    return p;
  }

  [Serializable]
  sealed class LocalSaveDto {
    public List<WordEntry> words = new List<WordEntry>();
    public List<string> questsDone = new List<string>();
    public float playTimeSec;
    public List<KvEntry> npcVoices = new List<KvEntry>();
    public string worldSeed = "";
  }

  [Serializable]
  sealed class WordEntry {
    public string wordId = "";
    public int stage;
    public int exposure;
    public int recognitionHit;
    public int recognitionTotal;
    public int speakingHit;
    public int speakingTotal;
    public int contextUse;
    public long nextReviewTicks;
    public float score;
  }

  [Serializable]
  sealed class KvEntry {
    public string k = "";
    public string v = "";
  }
}
