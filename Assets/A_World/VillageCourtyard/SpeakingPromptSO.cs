// A_World/VillageCourtyard/SpeakingPromptSO.cs — Data Model for "Sân Đình Vui Vẻ" (vietnamese_speaking).
// Defines target phrases, 3-region dialect matrices, syllable counts, and local asset references.
// C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;
using LweAssets;

public enum DifficultyTier {
  Tier1_Greeting,        // Chào hỏi lễ phép: "Cháu chào bà", "Con chào mẹ"
  Tier2_RealWorldObject, // Tên đồ vật/hoa quả thực tế: "Quả bưởi", "Bông hoa sen", "Đèn lồng"
  Tier3_Affection        // Tình cảm gia đình: "Con yêu mẹ", "Bé thương bà"
}

public enum RegionType {
  North,   // Miền Bắc: s/x, ch/tr, r/d/gi
  Central, // Miền Trung: hỏi/ngã, final consonants n/ng, t/c
  South    // Miền Nam: hỏi/ngã, v/d, final consonants n/ng, t/c
}

[Serializable]
public struct DialectAcceptedVariant {
  public RegionType region;
  public List<string> acceptedSpellings; // Các cách phát âm/viết chấp nhận theo vùng

  public DialectAcceptedVariant(RegionType r, params string[] variants) {
    region = r;
    acceptedSpellings = new List<string>(variants ?? Array.Empty<string>());
  }
}

[CreateAssetMenu(fileName = "SpeakingPrompt_", menuName = "Shatterproof/SpeakingPromptData")]
public class SpeakingPromptSO : ScriptableObject {
  public string promptId;
  public string targetText;                  // Chữ hiển thị chuẩn (Ví dụ: "Quả bưởi", "Bà ơi", "Con yêu Mẹ")
  public DifficultyTier tier;
  public SpriteAssetRef realPhotoRef;    // Hình minh họa/pictogram hiện dùng; ảnh THẬT khi chủ dự án đăng ký gói ảnh
  public AssetRef<AudioClip> referenceAudioRef; // Giọng chuẩn mẫu của cô Tấm / chú Cuội
  public List<DialectAcceptedVariant> dialectMatrix = new List<DialectAcceptedVariant>();
  public int targetSyllableCount;            // Số lượng âm tiết (để check dung sai ngọng)

  // Runtime factory helper for tests and offline usage
  public static SpeakingPromptSO CreateInstance(string id, string text, DifficultyTier tier,
      int syllables, SpriteAssetRef photo = null, AssetRef<AudioClip> audio = null) {
    var prompt = ScriptableObject.CreateInstance<SpeakingPromptSO>();
    prompt.promptId = id;
    prompt.targetText = text;
    prompt.tier = tier;
    prompt.targetSyllableCount = syllables > 0 ? syllables : CountSyllables(text);
    prompt.realPhotoRef = photo ?? new SpriteAssetRef();
    prompt.referenceAudioRef = audio ?? new AssetRef<AudioClip>();
    prompt.dialectMatrix = BuildDefaultDialectMatrix(text);
    return prompt;
  }

  public static int CountSyllables(string text) {
    if (string.IsNullOrWhiteSpace(text)) return 0;
    string[] tokens = text.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
    return tokens.Length;
  }

  public static List<DialectAcceptedVariant> BuildDefaultDialectMatrix(string target) {
    var list = new List<DialectAcceptedVariant>();
    if (string.IsNullOrEmpty(target)) return list;

    // Default dialect mappings for Vietnamese
    // North: ch/tr, s/x, r/d/gi
    var northVariants = new List<string> { target };
    // Central & South: hoi/nga, n/ng, t/c
    var centralVariants = new List<string> { target };
    var southVariants = new List<string> { target };

    string lower = target.ToLowerInvariant();
    if (lower.Contains("bưởi")) {
      centralVariants.Add(target.Replace("bưởi", "bưỡi").Replace("Bưởi", "Bưỡi"));
      southVariants.Add(target.Replace("bưởi", "bưỡi").Replace("Bưởi", "Bưỡi"));
    }
    if (lower.Contains("xanh")) {
      northVariants.Add(target.Replace("xanh", "sanh").Replace("Xanh", "Sanh"));
    }
    if (lower.Contains("tròn")) {
      northVariants.Add(target.Replace("tròn", "chòn").Replace("Tròn", "Chòn"));
    }

    list.Add(new DialectAcceptedVariant(RegionType.North, northVariants.ToArray()));
    list.Add(new DialectAcceptedVariant(RegionType.Central, centralVariants.ToArray()));
    list.Add(new DialectAcceptedVariant(RegionType.South, southVariants.ToArray()));
    return list;
  }
}
