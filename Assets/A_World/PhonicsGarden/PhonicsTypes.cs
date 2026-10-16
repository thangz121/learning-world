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
        new PhonicsSegment("C", "seg_c", 1.05f),
        new PhonicsSegment("A", "seg_a", 1.05f),
        new PhonicsSegment("CA", "seg_ca", 1.10f),
        new PhonicsSegment("´", "seg_sac", 1.00f),
        new PhonicsSegment("CÁ", "word_ca", 1.15f)
      },
      new string[] { "GÀ", "BÒ" },
      "RealPhotos/Animals/fish_real",
      "Con cá bơi dưới nước.",
      "The fish swims gently."
    ),

    // 2. BÒ (Animals)
    new PhonicsWordData("bo", "BÒ", "animals",
      new PhonicsSegment[] {
        new PhonicsSegment("B", "seg_b", 1.05f),
        new PhonicsSegment("O", "seg_o", 1.05f),
        new PhonicsSegment("BO", "seg_bo", 1.10f),
        new PhonicsSegment("`", "seg_huyen", 1.00f),
        new PhonicsSegment("BÒ", "word_bo", 1.15f)
      },
      new string[] { "DÊ", "CHÓ" },
      "RealPhotos/Animals/cow_real",
      "Chú bò ăn cỏ xanh.",
      "The cow eats grass."
    ),

    // 3. GÀ (Animals)
    new PhonicsWordData("ga", "GÀ", "animals",
      new PhonicsSegment[] {
        new PhonicsSegment("G", "seg_g", 1.05f),
        new PhonicsSegment("A", "seg_a", 1.05f),
        new PhonicsSegment("GA", "seg_ga", 1.10f),
        new PhonicsSegment("`", "seg_huyen", 1.00f),
        new PhonicsSegment("GÀ", "word_ga", 1.15f)
      },
      new string[] { "VỊT", "CHIM" },
      "RealPhotos/Animals/rooster_real",
      "Chú gà trống gáy vang.",
      "The rooster crows loud."
    ),

    // 4. CAM (Fruits)
    new PhonicsWordData("cam", "CAM", "fruits",
      new PhonicsSegment[] {
        new PhonicsSegment("C", "seg_c", 1.05f),
        new PhonicsSegment("A", "seg_a", 1.05f),
        new PhonicsSegment("M", "seg_m", 1.05f),
        new PhonicsSegment("AM", "seg_am", 1.10f),
        new PhonicsSegment("CAM", "word_cam", 1.15f)
      },
      new string[] { "TÁO", "NHO" },
      "RealPhotos/Fruits/orange_real",
      "Quả cam ngọt nhiều nước.",
      "The orange is sweet."
    ),

    // 5. TÁO (Fruits)
    new PhonicsWordData("tao", "TÁO", "fruits",
      new PhonicsSegment[] {
        new PhonicsSegment("T", "seg_t", 1.05f),
        new PhonicsSegment("A", "seg_a", 1.05f),
        new PhonicsSegment("O", "seg_o", 1.05f),
        new PhonicsSegment("AO", "seg_ao", 1.10f),
        new PhonicsSegment("TAO", "seg_tao", 1.10f),
        new PhonicsSegment("´", "seg_sac", 1.00f),
        new PhonicsSegment("TÁO", "word_tao", 1.15f)
      },
      new string[] { "LÊ", "CHUỐI" },
      "RealPhotos/Fruits/apple_real",
      "Quả táo đỏ giòn ngon.",
      "The red apple is crisp."
    ),

    // 6. XE (Vehicles)
    new PhonicsWordData("xe", "XE", "vehicles",
      new PhonicsSegment[] {
        new PhonicsSegment("X", "seg_x", 1.05f),
        new PhonicsSegment("E", "seg_e", 1.05f),
        new PhonicsSegment("XE", "word_xe", 1.15f)
      },
      new string[] { "TÀU", "THUYỀN" },
      "RealPhotos/Vehicles/car_real",
      "Chiếc xe chạy bon bon.",
      "The car rolls smoothly."
    ),

    // 7. BÀ (Family)
    new PhonicsWordData("ba", "BÀ", "family",
      new PhonicsSegment[] {
        new PhonicsSegment("B", "seg_b", 1.05f),
        new PhonicsSegment("A", "seg_a", 1.05f),
        new PhonicsSegment("BA", "seg_ba", 1.10f),
        new PhonicsSegment("`", "seg_huyen", 1.00f),
        new PhonicsSegment("BÀ", "word_ba", 1.15f)
      },
      new string[] { "MẸ", "BÉ" },
      "RealPhotos/Family/grandmother_real",
      "Bà cười hiền yêu con.",
      "Grandmother smiles gently."
    ),

    // 8. MẸ (Family)
    new PhonicsWordData("me", "MẸ", "family",
      new PhonicsSegment[] {
        new PhonicsSegment("M", "seg_m", 1.05f),
        new PhonicsSegment("E", "seg_e", 1.05f),
        new PhonicsSegment("ME", "seg_me", 1.10f),
        new PhonicsSegment(".", "seg_nang", 1.00f),
        new PhonicsSegment("MẸ", "word_me", 1.15f)
      },
      new string[] { "BỐ", "BÀ" },
      "RealPhotos/Family/mother_real",
      "Mẹ yêu bé rất nhiều.",
      "Mother loves baby dearly."
    ),

    // 9. TÀU (Vehicles)
    new PhonicsWordData("tau", "TÀU", "vehicles",
      new PhonicsSegment[] {
        new PhonicsSegment("T", "seg_t", 1.05f),
        new PhonicsSegment("A", "seg_a", 1.05f),
        new PhonicsSegment("U", "seg_u", 1.05f),
        new PhonicsSegment("AU", "seg_au", 1.10f),
        new PhonicsSegment("TAU", "seg_tau", 1.10f),
        new PhonicsSegment("`", "seg_huyen", 1.00f),
        new PhonicsSegment("TÀU", "word_tau", 1.15f)
      },
      new string[] { "XE", "THUYỀN" },
      "RealPhotos/Vehicles/train_real",
      "Đoàn tàu hỏa dài.",
      "The train is long."
    ),

    // 10. BÚT (Tools)
    new PhonicsWordData("but", "BÚT", "tools",
      new PhonicsSegment[] {
        new PhonicsSegment("B", "seg_b", 1.05f),
        new PhonicsSegment("U", "seg_u", 1.05f),
        new PhonicsSegment("T", "seg_t", 1.05f),
        new PhonicsSegment("UT", "seg_ut", 1.10f),
        new PhonicsSegment("BUT", "seg_but", 1.10f),
        new PhonicsSegment("´", "seg_sac", 1.00f),
        new PhonicsSegment("BÚT", "word_but", 1.15f)
      },
      new string[] { "THƯỚC", "SÁCH" },
      "RealPhotos/Tools/pen_real",
      "Cây bút viết chữ xinh.",
      "The pen writes words."
    ),

    // 11. HOA (Nature)
    new PhonicsWordData("hoa", "HOA", "nature",
      new PhonicsSegment[] {
        new PhonicsSegment("O", "seg_o", 1.05f),
        new PhonicsSegment("A", "seg_a", 1.05f),
        new PhonicsSegment("OA", "seg_oa", 1.10f),
        new PhonicsSegment("H", "seg_h", 1.05f),
        new PhonicsSegment("OA", "seg_oa", 1.10f),
        new PhonicsSegment("HOA", "word_hoa", 1.15f)
      },
      new string[] { "LÁ", "CÂY" },
      "RealPhotos/Nature/flower_real",
      "Bông hoa nở rực rỡ.",
      "The flower blooms bright."
    ),

    // 12. TAY (Body)
    new PhonicsWordData("tay", "TAY", "body",
      new PhonicsSegment[] {
        new PhonicsSegment("T", "seg_t", 1.05f),
        new PhonicsSegment("A", "seg_a", 1.05f),
        new PhonicsSegment("Y", "seg_y", 1.05f),
        new PhonicsSegment("AY", "seg_ay", 1.10f),
        new PhonicsSegment("TAY", "word_tay", 1.15f)
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
