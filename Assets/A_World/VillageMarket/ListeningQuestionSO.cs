// Assets/A_World/VillageMarket/ListeningQuestionSO.cs — Data Models for "Chợ Quê Nhộn Nhịp" (vietnamese_listening).
// Supports 3 listening modes: Direct Noun, Attribute Riddle, and Sequential Order.
// Pure C# 9.0 only.
using System;
using System.Collections.Generic;
using UnityEngine;
using LweAssets;

// Addressables-style aliases for clean compatibility
using AssetReferenceSprite = LweAssets.SpriteAssetRef;

public enum ListeningMode {
  Level1_DirectNoun,      // Nghe tên gọi hoặc tiếng kêu -> Chọn 1 ảnh thật
  Level2_AttributeRiddle, // Nghe câu đố mô tả màu sắc/kích thước/công dụng -> Chọn 1 ảnh thật
  Level3_SequentialOrder  // Nghe dặn dò 2 món đồ -> Kéo thả đúng thứ tự vào quang gánh
}

[Serializable]
public class MarketItemOption {
  public string itemId;
  public string displayName;                      // Hiện chữ khi tổng kết hoặc ở Giàn giáo tầng 3
  public SpriteAssetRef realPhotoRef;             // Ảnh THẬT khi chủ dự án đăng ký; nếu chưa: pictogram minh họa (không giả danh ảnh chụp)
  public AssetRef<AudioClip> realSoundCue;        // Tiếng kêu thực tế / âm thanh vật lý

  public MarketItemOption() {
    itemId = "";
    displayName = "";
    realPhotoRef = new SpriteAssetRef();
    realSoundCue = new AssetRef<AudioClip>();
  }

  public MarketItemOption(string id, string name, SpriteAssetRef photo = null, AssetRef<AudioClip> sound = null) {
    itemId = id ?? "";
    displayName = name ?? "";
    realPhotoRef = photo ?? new SpriteAssetRef();
    realSoundCue = sound ?? new AssetRef<AudioClip>();
  }
}

[CreateAssetMenu(fileName = "ListenQ_", menuName = "VietnameseEdTech/ListeningQuestionSO")]
public class ListeningQuestionSO : ScriptableObject {
  public string questionId;
  public ListeningMode mode;
  public AssetRef<AudioClip> normalPromptAudio;     // Giọng Bà Ngoại tốc độ 1.0x
  public AssetRef<AudioClip> slowEmphasizedAudio;   // Giọng đọc chậm 0.8x nhấn mạnh từ khóa (Sai Lần 1)
  public AssetRef<AudioClip> teachBackAudio;        // Giọng giải thích chi tiết (Sai Lần 3)

  public List<MarketItemOption> allOptionsOnStall = new List<MarketItemOption>(); // 3 - 4 món đồ bày trên sạp
  public List<string> targetItemIdsInOrder = new List<string>();                  // 1 ID (L1, L2) hoặc 2 ID (L3)

  // Factory method for runtime generation and edit-mode test cases
  public static ListeningQuestionSO CreateInstance(string qId, ListeningMode m,
      List<MarketItemOption> options, List<string> targets,
      AssetRef<AudioClip> normalAudio = null,
      AssetRef<AudioClip> slowAudio = null,
      AssetRef<AudioClip> teachAudio = null) {
    var so = ScriptableObject.CreateInstance<ListeningQuestionSO>();
    so.questionId = qId;
    so.mode = m;
    so.allOptionsOnStall = options ?? new List<MarketItemOption>();
    so.targetItemIdsInOrder = targets ?? new List<string>();
    so.normalPromptAudio = normalAudio ?? new AssetRef<AudioClip>();
    so.slowEmphasizedAudio = slowAudio ?? new AssetRef<AudioClip>();
    so.teachBackAudio = teachAudio ?? new AssetRef<AudioClip>();
    return so;
  }
}
