// _SharedKernel/DialogueLang.cs — Lead owns. SYSTEM DIALOGUE LANGUAGE (user order):
//   1) Tiếng Việt mode: ALL NPC dialogue speaks Vietnamese; ONLY the English
//      subject (môn Tiếng Anh) keeps speaking English.
//   2) English mode (default): everything speaks English, exactly as before.
// One tiny state holder (no manager, no service instance): producers call
// DialogueLang.T(en, vi) for text and DialogueLang.Language for the TTS locale.
// Persisted additively in LocalSave (PlayerProgress.Language) and overridable at
// launch with "-lang vi" / "-lang en" (dev/review). The CLI override never
// rewrites the save.
// Vietnamese lines MUST stay inside the SafetyFilter caps (NPC <= 6 tokens,
// Milo <= 8) — AudioDirector silently drops longer lines, so CT-P49 pins them.
// C# 9.0 only.
using System;

public enum DialogueLanguage {
  English = 0,
  Vietnamese = 1,
}

public static class DialogueLang {
  static readonly LanguageCode En = new LanguageCode("en-US");
  static readonly LanguageCode Vi = new LanguageCode("vi-VN");

  public static DialogueLanguage Current { get; private set; } = DialogueLanguage.English;

  // Set while the active subject is the English subject (môn Tiếng Anh): that
  // world always teaches English, so its NPCs keep speaking English even when
  // the system language is Vietnamese. Forward hook: the English subject has
  // no scene yet, but every travel already publishes WorldChangedEvent.
  public static bool EnglishSubjectActive { get; set; }

  public static bool IsVietnamese {
    get { return Current == DialogueLanguage.Vietnamese && !EnglishSubjectActive; }
  }

  // TTS locale for the CURRENT lines (worker normalizes vi-VN -> vi; en-US -> en).
  public static LanguageCode Language { get { return IsVietnamese ? Vi : En; } }

  // Line picker used by every dialogue producer.
  public static string T(string english, string vietnamese) {
    return IsVietnamese ? vietnamese : english;
  }

  // Known HUD/objective pairs (the strings the HUD can be SHOWING when the
  // toggle is pressed). Relocalize flips either form to the active language so
  // the switch reads immediately instead of waiting for the next line.
  static readonly string[,] Pairs = {
    { "Choose a gate!", "Chọn một cổng nhé!" },
    { "Look around!", "Nhìn quanh nhé!" },
    { "Great job!", "Giỏi lắm!" },
    { "Talk to Milo", "Nói chuyện với Milo" },
    { "Find the apple", "Tìm quả táo" },
    { "Find the ball", "Tìm quả bóng" },
    { "Bring the apple to Mia", "Mang táo cho cô Mia" },
    { "Bring the ball to Mia", "Mang bóng cho cô Mia" },
    { "Find the one", "Tìm số một" },
    { "Bring it to Tess", "Mang cho cô Tess" },
    { "Math World", "Thế giới Toán" },
    { "Counting Garden", "Vườn Đếm" },
    { "Counting Playground", "Sân chơi đếm" },
    { "Watch!", "Xem nhé!" },
    { "Carrot patch", "Vườn cà rốt" },
    { "Strawberry patch", "Vườn dâu" },
    { "Counting stage", "Sân đếm" },
    { "Corn patch", "Vườn ngô" },
    { "Pumpkin patch", "Vườn bí" },
    { "Hear it again", "Nghe lại nhé" },
  };

  public static string Relocalize(string text) {
    if (string.IsNullOrEmpty(text)) return text;
    for (int i = 0; i < Pairs.GetLength(0); i++) {
      if (text == Pairs[i, 0] || text == Pairs[i, 1])
        return IsVietnamese ? Pairs[i, 1] : Pairs[i, 0];
    }
    return text; // dynamic string (subject names, timers): leave untouched
  }

  // Boot hook (GameInstaller): save value, then CLI override.
  public static void Init(PlayerProgress progress) {
    DialogueLanguage fromSave = DialogueLanguage.English;
    try {
      if (progress != null && Enum.IsDefined(typeof(DialogueLanguage), progress.Language))
        fromSave = progress.Language;
    } catch (Exception) { }
    Current = fromSave;
    EnglishSubjectActive = false;
    ApplyCliOverride();
  }

  static void ApplyCliOverride() {
    try {
      string[] args = Environment.GetCommandLineArgs();
      for (int i = 0; i < args.Length - 1; i++) {
        if (!string.Equals(args[i], "-lang", StringComparison.OrdinalIgnoreCase)) continue;
        string v = (args[i + 1] ?? "").Trim().ToLowerInvariant();
        if (v == "vi" || v == "vietnamese") Current = DialogueLanguage.Vietnamese;
        else if (v == "en" || v == "english") Current = DialogueLanguage.English;
      }
    } catch (Exception) { }
  }

  // Explicit setter (tests/HUD refresh/tooling); persistence is a separate call.
  public static void Set(DialogueLanguage lang) { Current = lang; }

  public static DialogueLanguage Toggle() {
    Current = Current == DialogueLanguage.Vietnamese
      ? DialogueLanguage.English : DialogueLanguage.Vietnamese;
    return Current;
  }

  // HUD toggle: flip + persist (load->modify->save, same pattern as every
  // other save writer). Persisting never throws out.
  public static void ToggleAndPersist() {
    Toggle();
    Persist();
  }

  public static void Persist() {
    try {
      var save = new LocalSave();
      PlayerProgress p = save.Load();
      p.Language = Current;
      save.Save(p);
    } catch (Exception) { }
  }
}
