// A_World/PhonicsGarden/PhonicsTypes.cs — Data Schemas for "Vườn Đánh Vần" & "Vườn Tri Thức".
// Phonics Segment Decomposition, 3-Tier Scaffolding, and Spaced Repetition Queue.
// Pure C# 9.0 only. EditMode-testable without UnityEngine scene dependencies.
using System;
using System.Collections.Generic;

[Serializable]
public class PhonicsSegment {
  public string SegmentText;       // Ký tự hiển thị (Ví dụ: "C", "A", "Á", "UÔI")
  public string AudioKey;          // Key audio phát âm phân đoạn
  public float DurationSeconds;    // Thời lượng highlight và đọc

  public PhonicsSegment(string text, string audioKey, float duration) {
    SegmentText = text;
    AudioKey = audioKey;
    DurationSeconds = duration;
  }
}

[Serializable]
public class PhonicsWordData {
  public string WordId;            // ID định danh duy nhất (ví dụ: "ca", "meo", "bo")
  public string FullWord;          // Từ hiển thị đầy đủ (ví dụ: "CÁ", "MÈO", "BÒ")
  public string TopicId;           // Nhóm chủ đề ("animals", "fruits", "vehicles", v.v.)
  public PhonicsSegment[] Segments; // Danh sách phân đoạn âm vị học khi đánh vần
  public string[] Distractors;     // 2 đáp án gây nhiễu để tạo 3 lựa chọn
  public string RealPhotoAddress;  // Key/Path ảnh thật (khi chủ dự án cấp bộ ảnh); chưa bundle — UI dùng pictogram minh họa trung thực
  public string ExplainSentenceEn; // Câu thoại tiếng Anh ngắn
  public string ExplainSentenceVi; // Câu thoại tiếng Việt ngắn (<= 6 từ)

  public PhonicsWordData(string id, string word, string topic, PhonicsSegment[] segments,
      string[] distractors, string realPhoto, string viSentence, string enSentence) {
    WordId = id;
    FullWord = word;
    TopicId = topic;
    Segments = segments;
    Distractors = distractors;
    RealPhotoAddress = realPhoto;
    ExplainSentenceVi = viSentence;
    ExplainSentenceEn = enSentence;
  }
}

public enum ScaffoldingTier {
  InitialAttempt = 0, // Lần đầu nghe đánh vần và chọn 1 trong 3 đáp án
  MistakeRepeat = 1,  // Sai lần 1: Cô giáo đọc lại + động viên
  MistakePhotoClue = 2,// Sai lần 2: Hiện mỏ neo thị giác (ảnh thật khi có; nếu không: hình minh họa)
  MistakeTeachBack = 3,// Sai lần 3: Dạy chi tiết từng chữ cái + Đưa vào Spaced Repetition
}

public enum PhonicsPlayMode {
  SpellingGarden = 0,    // Chế độ 1: Vườn Đánh Vần (Tập trung âm vị, đánh vần chi tiết)
  RealWorldDiscovery = 1 // Chế độ 2: Vườn Tri Thức Thực Tế (Đa dạng 6 chủ đề ảnh thật)
}

// Spaced Repetition Leitner Memory Queue
public class SpacedRepetitionTracker {
  readonly Queue<string> _reviewQueue = new Queue<string>();
  readonly Dictionary<string, int> _mistakeHistory = new Dictionary<string, int>();

  public int PendingReviewCount => _reviewQueue.Count;

  public void RecordMistake(string wordId) {
    if (string.IsNullOrEmpty(wordId)) return;
    if (!_mistakeHistory.ContainsKey(wordId)) _mistakeHistory[wordId] = 0;
    _mistakeHistory[wordId]++;
    if (!_reviewQueue.Contains(wordId)) {
      _reviewQueue.Enqueue(wordId);
    }
  }

  public string DequeueReviewWord() {
    if (_reviewQueue.Count > 0) return _reviewQueue.Dequeue();
    return null;
  }

  public int GetMistakeCount(string wordId) {
    if (string.IsNullOrEmpty(wordId)) return 0;
    return _mistakeHistory.TryGetValue(wordId, out int c) ? c : 0;
  }

  public void Clear() {
    _reviewQueue.Clear();
    _mistakeHistory.Clear();
  }
}

// Kho từ vựng chuẩn ngữ âm Tiếng Việt mầm non (Age 3-6)
public static class PhonicsDatabase {
  public static readonly PhonicsWordData[] Words = {
    // 1. CÁ (Animals)
    new PhonicsWordData("ca", "CÁ", "animals",
      new PhonicsSegment[] {
        new PhonicsSegment("C", "audio_c", 0.4f),
        new PhonicsSegment("A", "audio_a", 0.4f),
        new PhonicsSegment("CA", "audio_ca", 0.5f),
        new PhonicsSegment("´", "audio_sac", 0.35f),
        new PhonicsSegment("CÁ", "audio_ca_full", 0.6f)
      },
      new string[] { "GÀ", "BÒ" },
      "RealPhotos/Animals/fish_real",
      "Con cá bơi dưới nước.",
      "The fish swims gently."
    ),

    // 2. BÒ (Animals)
    new PhonicsWordData("bo", "BÒ", "animals",
      new PhonicsSegment[] {
        new PhonicsSegment("B", "audio_b", 0.4f),
        new PhonicsSegment("O", "audio_o", 0.4f),
        new PhonicsSegment("BO", "audio_bo", 0.5f),
        new PhonicsSegment("`", "audio_huyen", 0.35f),
        new PhonicsSegment("BÒ", "audio_bo_full", 0.6f)
      },
      new string[] { "DÊ", "CHÓ" },
      "RealPhotos/Animals/cow_real",
      "Chú bò ăn cỏ xanh.",
      "The cow eats grass."
    ),

    // 3. GÀ (Animals)
    new PhonicsWordData("ga", "GÀ", "animals",
      new PhonicsSegment[] {
        new PhonicsSegment("G", "audio_g", 0.4f),
        new PhonicsSegment("A", "audio_a", 0.4f),
        new PhonicsSegment("GA", "audio_ga", 0.5f),
        new PhonicsSegment("`", "audio_huyen", 0.35f),
        new PhonicsSegment("GÀ", "audio_ga_full", 0.6f)
      },
      new string[] { "VỊT", "CHIM" },
      "RealPhotos/Animals/rooster_real",
      "Chú gà trống gáy vang.",
      "The rooster crows loud."
    ),

    // 4. CAM (Fruits)
    new PhonicsWordData("cam", "CAM", "fruits",
      new PhonicsSegment[] {
        new PhonicsSegment("C", "audio_c", 0.4f),
        new PhonicsSegment("AM", "audio_am", 0.5f),
        new PhonicsSegment("CAM", "audio_cam_full", 0.6f)
      },
      new string[] { "TÁO", "NHO" },
      "RealPhotos/Fruits/orange_real",
      "Quả cam ngọt nhiều nước.",
      "The orange is sweet."
    ),

    // 5. TÁO (Fruits)
    new PhonicsWordData("tao", "TÁO", "fruits",
      new PhonicsSegment[] {
        new PhonicsSegment("T", "audio_t", 0.4f),
        new PhonicsSegment("AO", "audio_ao", 0.5f),
        new PhonicsSegment("TAO", "audio_tao", 0.5f),
        new PhonicsSegment("´", "audio_sac", 0.35f),
        new PhonicsSegment("TÁO", "audio_tao_full", 0.6f)
      },
      new string[] { "LÊ", "CHUỐI" },
      "RealPhotos/Fruits/apple_real",
      "Quả táo đỏ giòn ngon.",
      "The red apple is crisp."
    ),

    // 6. XE (Vehicles)
    new PhonicsWordData("xe", "XE", "vehicles",
      new PhonicsSegment[] {
        new PhonicsSegment("X", "audio_x", 0.4f),
        new PhonicsSegment("E", "audio_e", 0.4f),
        new PhonicsSegment("XE", "audio_xe_full", 0.6f)
      },
      new string[] { "TÀU", "THUYỀN" },
      "RealPhotos/Vehicles/car_real",
      "Chiếc xe chạy bon bon.",
      "The car rolls smoothly."
    ),

    // 7. BÀ (Family)
    new PhonicsWordData("ba", "BÀ", "family",
      new PhonicsSegment[] {
        new PhonicsSegment("B", "audio_b", 0.4f),
        new PhonicsSegment("A", "audio_a", 0.4f),
        new PhonicsSegment("BA", "audio_ba", 0.5f),
        new PhonicsSegment("`", "audio_huyen", 0.35f),
        new PhonicsSegment("BÀ", "audio_ba_full", 0.6f)
      },
      new string[] { "MẸ", "BÉ" },
      "RealPhotos/Family/grandmother_real",
      "Bà cười hiền yêu con.",
      "Grandmother smiles gently."
    ),

    // 8. MẸ (Family)
    new PhonicsWordData("me", "MẸ", "family",
      new PhonicsSegment[] {
        new PhonicsSegment("M", "audio_m", 0.4f),
        new PhonicsSegment("E", "audio_e", 0.4f),
        new PhonicsSegment("ME", "audio_me", 0.5f),
        new PhonicsSegment(".", "audio_nang", 0.35f),
        new PhonicsSegment("MẸ", "audio_me_full", 0.6f)
      },
      new string[] { "BỐ", "BÀ" },
      "RealPhotos/Family/mother_real",
      "Mẹ yêu bé rất nhiều.",
      "Mother loves baby dearly."
    ),

    // 9. TÀU (Vehicles)
    new PhonicsWordData("tau", "TÀU", "vehicles",
      new PhonicsSegment[] {
        new PhonicsSegment("T", "audio_t", 0.4f),
        new PhonicsSegment("AU", "audio_au", 0.5f),
        new PhonicsSegment("TAU", "audio_tau", 0.5f),
        new PhonicsSegment("`", "audio_huyen", 0.35f),
        new PhonicsSegment("TÀU", "audio_tau_full", 0.6f)
      },
      new string[] { "XE", "THUYỀN" },
      "RealPhotos/Vehicles/train_real",
      "Đoàn tàu hỏa dài.",
      "The train is long."
    ),

    // 10. BÚT (Tools)
    new PhonicsWordData("but", "BÚT", "tools",
      new PhonicsSegment[] {
        new PhonicsSegment("B", "audio_b", 0.4f),
        new PhonicsSegment("UT", "audio_ut", 0.5f),
        new PhonicsSegment("BUT", "audio_but", 0.5f),
        new PhonicsSegment("´", "audio_sac", 0.35f),
        new PhonicsSegment("BÚT", "audio_but_full", 0.6f)
      },
      new string[] { "THƯỚC", "SÁCH" },
      "RealPhotos/Tools/pen_real",
      "Cây bút viết chữ xinh.",
      "The pen writes words."
    ),

    // 11. HOA (Nature)
    new PhonicsWordData("hoa", "HOA", "nature",
      new PhonicsSegment[] {
        new PhonicsSegment("H", "audio_h", 0.4f),
        new PhonicsSegment("OA", "audio_oa", 0.5f),
        new PhonicsSegment("HOA", "audio_hoa_full", 0.6f)
      },
      new string[] { "LÁ", "CÂY" },
      "RealPhotos/Nature/flower_real",
      "Bông hoa nở rực rỡ.",
      "The flower blooms bright."
    ),

    // 12. TAY (Body)
    new PhonicsWordData("tay", "TAY", "body",
      new PhonicsSegment[] {
        new PhonicsSegment("T", "audio_t", 0.4f),
        new PhonicsSegment("AY", "audio_ay", 0.5f),
        new PhonicsSegment("TAY", "audio_tay_full", 0.6f)
      },
      new string[] { "CHÂN", "MẮT" },
      "RealPhotos/Body/hand_real",
      "Đôi bàn tay bé sạch.",
      "Little hands are clean."
    )
  };

  public static PhonicsWordData GetWord(string id) {
    if (string.IsNullOrEmpty(id)) return null;
    for (int i = 0; i < Words.Length; i++) {
      if (string.Equals(Words[i].WordId, id, StringComparison.OrdinalIgnoreCase))
        return Words[i];
    }
    return null;
  }

  public static PhonicsWordData[] GetWordsByTopic(string topic) {
    List<PhonicsWordData> hits = new List<PhonicsWordData>();
    for (int i = 0; i < Words.Length; i++) {
      if (string.Equals(Words[i].TopicId, topic, StringComparison.OrdinalIgnoreCase))
        hits.Add(Words[i]);
    }
    return hits.ToArray();
  }
}
