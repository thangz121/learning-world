// A_World/ListeningGarden/ListeningWords.cs — the listening pack registry
// (English -> Listening). Mirror of Content/listening/listen_garden.json,
// pinned by CT-S21; the audio manifest ids follow AudioId().
// C# 9.0 only.
using System;

public static class ListeningWords {
  // Voice/params frozen with Content/audio_manifest.json (voice 'listen_v1'):
  // pregen masters ship in StreamingAssets/audio so the pack plays offline.
  public const string VoiceId = "listen_v1";
  public const string Lang = "en-US";
  public const float RateNormal = 0.85f;
  public const float RateSlow = 0.70f;

  // Product order; the game pools are subsets of this list.
  public static readonly string[] All = {
    "chair", "table", "desk", "books",
    "car", "truck",
    "tree", "rock", "flower", "mushroom",
    "apple", "ball", "banana", "orange",
    "carrot", "tomato", "watermelon", "grape",
  };

  public static bool IsKnown(string id) {
    if (string.IsNullOrEmpty(id)) return false;
    for (int i = 0; i < All.Length; i++)
      if (string.Equals(All[i], id, StringComparison.OrdinalIgnoreCase)) return true;
    return false;
  }

  // The spoken English text (the manifest 'text'). "grape" is spoken as
  // "grapes" (the bunch): the isolated singular renders an unclear onset in
  // the local voice and failed the ASR self-check (WP-1.9.32 report).
  public static string TextEn(string id) {
    return string.Equals(id, "grape", StringComparison.OrdinalIgnoreCase) ? "grapes" : id;
  }

  // Vietnamese label used by the praise line (and parent-facing surfaces).
  public static string DisplayVi(string id) {
    switch ((id ?? "").ToLowerInvariant()) {
      case "chair": return "cái ghế";
      case "table": return "cái bàn";
      case "desk": return "bàn học";
      case "books": return "sách";
      case "car": return "ô tô";
      case "truck": return "xe tải";
      case "tree": return "cây";
      case "rock": return "hòn đá";
      case "flower": return "bông hoa";
      case "mushroom": return "cây nấm";
      case "apple": return "quả táo";
      case "ball": return "quả bóng";
      case "banana": return "quả chuối";
      case "orange": return "quả cam";
      case "carrot": return "củ cà rốt";
      case "tomato": return "quả cà chua";
      case "watermelon": return "quả dưa hấu";
      case "grape": return "chùm nho";
      default: return id ?? "";
    }
  }

  // PropKit/<name> for the picture pod; "ball" is built from primitives by
  // the builder (no food-kit asset), "grape" uses the 'grapes' bunch.
  public static string PropName(string id) {
    switch ((id ?? "").ToLowerInvariant()) {
      case "grape": return "grapes";
      case "car": return "sedan";
      case "tree": return "tree_default";
      case "rock": return "rock_smallA";
      case "flower": return "flower_yellowA";
      case "mushroom": return "mushroom_red";
      default: return id ?? "";
    }
  }

  // Manifest/pregen id for one word and mode ('normal' | 'slow').
  public static string AudioId(string id, bool slow) {
    return "listen_" + id + (slow ? "_slow" : "_normal");
  }
}
