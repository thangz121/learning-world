// CT-P49: S3-P2L SYSTEM DIALOGUE LANGUAGE (user order).
// Pins: English default (old saves + old behavior), Vietnamese mode picks the
// Vietnamese line + vi-VN TTS locale for EVERY producer (Tess/Mia/Milo + the
// counting lesson), the English-subject exception keeps English, all
// Vietnamese lines pass the SafetyFilter word caps (long lines would be
// silently dropped by the AudioDirector), the save round-trip (additive field,
// old saves migrate to English), and the HUD language toggle chip.
// C# 9.0 only.
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class CT_P49_DialogueLanguage {
  sealed class FakeAudio : IAudioDirector {
    public readonly List<DialogueRequest> Lines = new List<DialogueRequest>();
    public Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode mode) { return Task.CompletedTask; }
    public Task SpeakAsync(DialogueRequest request) { Lines.Add(request); return Task.CompletedTask; }
    public void PlaySfx(SfxId id) { }
    public void PlayMusic(MusicId id) { }
    public void SetAudioFocus(AudioFocusMode mode) { }
  }

  DialogueLanguage _savedLang;
  bool _savedEnglishSubject;

  void SetUp() {
    _savedLang = DialogueLang.Current;
    _savedEnglishSubject = DialogueLang.EnglishSubjectActive;
  }

  void TearDown() {
    DialogueLang.Set(_savedLang);
    DialogueLang.EnglishSubjectActive = _savedEnglishSubject;
  }

  // A. State machine: default English, picker + locale + subject exception.
  [Test] public void P49A_LanguageStateAndPicker() {
    SetUp();
    try {
      DialogueLang.Set(DialogueLanguage.English);
      DialogueLang.EnglishSubjectActive = false;
      Assert.AreEqual("en-US", DialogueLang.Language.Value, "English mode speaks en-US");
      Assert.AreEqual("Find the one!", DialogueLang.T("Find the one!", "Tìm số một!"),
        "English mode returns the English line");
      DialogueLang.Set(DialogueLanguage.Vietnamese);
      Assert.AreEqual("vi-VN", DialogueLang.Language.Value, "Vietnamese mode speaks vi-VN");
      Assert.AreEqual("Tìm số một!", DialogueLang.T("Find the one!", "Tìm số một!"),
        "Vietnamese mode returns the Vietnamese line");
      // English subject exception: Chinese wall for the English micro-world.
      DialogueLang.EnglishSubjectActive = true;
      Assert.IsFalse(DialogueLang.IsVietnamese, "English subject forces English");
      Assert.AreEqual("en-US", DialogueLang.Language.Value, "English subject keeps the en locale");
      Assert.AreEqual("Find the one!", DialogueLang.T("Find the one!", "Tìm số một!"),
        "English subject speaks English even in Vietnamese mode");
      DialogueLang.EnglishSubjectActive = false;
      DialogueLang.Set(DialogueLanguage.English);
      Assert.AreEqual(DialogueLanguage.Vietnamese, DialogueLang.Toggle(), "toggle flips the mode");
      Assert.AreEqual(DialogueLanguage.English, DialogueLang.Toggle(), "toggle flips back");
      // Init from a save value + null progress default.
      var p = new PlayerProgress { Language = DialogueLanguage.Vietnamese };
      DialogueLang.Init(p);
      Assert.AreEqual(DialogueLanguage.Vietnamese, DialogueLang.Current, "save value restored at boot");
      DialogueLang.Init(null);
      Assert.AreEqual(DialogueLanguage.English, DialogueLang.Current, "null/old-progress boots English");
      // Relocalize: the line already on screen flips with the toggle.
      Assert.AreEqual("Choose a gate!", DialogueLang.Relocalize("Choose a gate!"),
        "English mode keeps the English HUD line");
      DialogueLang.Set(DialogueLanguage.Vietnamese);
      Assert.AreEqual("Chọn một cổng nhé!", DialogueLang.Relocalize("Choose a gate!"),
        "VI mode flips a shown English HUD line");
      Assert.AreEqual("Chọn một cổng nhé!", DialogueLang.Relocalize("Chọn một cổng nhé!"),
        "VI mode keeps the Vietnamese HUD line");
      Assert.AreEqual("Toán", DialogueLang.Relocalize("Toán"),
        "dynamic strings are left untouched");
      DialogueLang.Set(DialogueLanguage.English);
      Assert.AreEqual("Choose a gate!", DialogueLang.Relocalize("Chọn một cổng nhé!"),
        "EN mode flips a shown Vietnamese HUD line back");
    } finally { TearDown(); }
  }

  // B. Every static producer honors the mode and stays inside the safety caps.
  [Test] public void P49B_ProducersLocalize() {
    SetUp();
    try {
      FakeAudio en = new FakeAudio();
      Tess.Bind(en);
      Mia.Bind(en);
      Milo.Bind(null, null, null, null, en);
      DialogueLang.Set(DialogueLanguage.English);
      Tess.SayFind(); Tess.SayBring(); Tess.PraiseFound(); Tess.Celebrate(); Tess.SayName();
      Mia.SayName(); Mia.SayRetry();
      Milo.Encourage(); Milo.Celebrate(); Milo.PointHint(); Milo.DemoHint(); Milo.Greet();
      Milo.InstructFind(); Milo.InstructBring(); Milo.PraiseFound();
      Assert.AreEqual(15, en.Lines.Count, "all 15 lines captured in English mode");
      Assert.AreEqual("Find the one!", en.Lines[0].Text, "English line unchanged (pins hold)");
      foreach (DialogueRequest r in en.Lines) {
        Assert.AreEqual("en-US", r.Lang.Value, "English mode locale");
        bool ok = SafetyFilter.ValidateLine(r.Text, r.Voice.Value == "milo_v1", out string why);
        Assert.IsTrue(ok, "EN line passes SafetyFilter: '" + r.Text + "' (" + why + ")");
      }

      FakeAudio vi = new FakeAudio();
      Tess.Bind(vi);
      Mia.Bind(vi);
      Milo.Bind(null, null, null, null, vi);
      DialogueLang.Set(DialogueLanguage.Vietnamese);
      Tess.SayFind(); Tess.SayBring(); Tess.PraiseFound(); Tess.Celebrate(); Tess.SayName();
      Mia.SayName(); Mia.SayRetry();
      Milo.Encourage(); Milo.Celebrate(); Milo.PointHint(); Milo.DemoHint(); Milo.Greet();
      Milo.InstructFind(); Milo.InstructBring(); Milo.PraiseFound();
      Assert.AreEqual(15, vi.Lines.Count, "all 15 lines captured in Vietnamese mode");
      Assert.AreEqual("Tìm số một!", vi.Lines[0].Text, "Tess speaks Vietnamese");
      Assert.AreEqual("Cô là Mia!", vi.Lines[5].Text, "Mia speaks Vietnamese");
      Assert.AreEqual("Xin chào! Mình là Milo!", vi.Lines[11].Text, "Milo speaks Vietnamese");
      foreach (DialogueRequest r in vi.Lines) {
        Assert.AreEqual("vi-VN", r.Lang.Value, "Vietnamese mode locale");
        Assert.IsFalse(r.Text.StartsWith("Find "), "no English leaker: '" + r.Text + "'");
        bool ok = SafetyFilter.ValidateLine(r.Text, r.Voice.Value == "milo_v1", out string why);
        Assert.IsTrue(ok, "VI line must pass SafetyFilter: '" + r.Text + "' (" + why + ")");
      }
    } finally { TearDown(); }
  }

  // C. The counting lesson (live sequence) speaks the mode's language on every
  // beat, and still completes its loop.
  [Test] public void P49C_LessonLocalizes() {
    SetUp();
    GameObject garden = new GameObject("P49GardenWorld");
    try {
      CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
      builder.BuildContent(garden.transform);
      FakeAudio audio = new FakeAudio();
      CountingDemo demo = garden.AddComponent<CountingDemo>();
      DialogueLang.Set(DialogueLanguage.Vietnamese);
      demo.Build(builder, null, null, audio);
      int guard = 0;
      while (demo.LoopCount < 1 && guard < 4000) { demo.Step(0.1f); guard++; }
      Assert.GreaterOrEqual(demo.LoopCount, 1, "lesson completes in Vietnamese mode");
      Assert.GreaterOrEqual(audio.Lines.Count, 10, "lesson spoke its lines");
      foreach (DialogueRequest r in audio.Lines) {
        Assert.AreEqual("vi-VN", r.Lang.Value, "lesson locale is vi-VN");
        bool ok = SafetyFilter.ValidateLine(r.Text, false, out string why);
        Assert.IsTrue(ok, "lesson VI line passes SafetyFilter: '" + r.Text + "' (" + why + ")");
      }
    } finally {
      TearDown();
      UnityEngine.Object.DestroyImmediate(garden);
    }
  }

  // D. Save round-trip: additive language field; old saves (no field) come
  // back as English; the toggle persists.
  [Test] public void P49D_SaveRoundTripAndMigration() {
    SetUp();
    string name = "p49_lang_test.json";
    var save = new LocalSave(name);
    try {
      var p = new PlayerProgress { Language = DialogueLanguage.Vietnamese, PlayTimeSec = 3f };
      save.Save(p);
      PlayerProgress back = save.Load();
      Assert.AreEqual(DialogueLanguage.Vietnamese, back.Language, "language persists");
      Assert.AreEqual(3f, back.PlayTimeSec, "other fields untouched");
      // Old save (missing field) migrates to English.
      System.IO.File.WriteAllText(save.FilePath,
        "{\"questsDone\":[],\"playTimeSec\":1.0,\"worldSeed\":\"\",\"playerGender\":0,\"genderChosen\":0}");
      PlayerProgress old = save.Load();
      Assert.AreEqual(DialogueLanguage.English, old.Language, "old save migrates to English");
    } finally {
      try { if (System.IO.File.Exists(save.FilePath)) System.IO.File.Delete(save.FilePath); }
      catch (Exception) { }
      TearDown();
    }
  }

  // E. HUD chip: exists, shows the current language name, and reflects toggles.
  [Test] public void P49E_HudLanguageChip() {
    SetUp();
    GameObject go = new GameObject("P49Hud");
    try {
      MarketHUD hud = go.AddComponent<MarketHUD>();
      hud.BuildUiImmediate();
      Assert.IsTrue(hud.HasLanguageButton, "language chip built");
      DialogueLang.Set(DialogueLanguage.English);
      hud.RefreshLanguageLabel();
      Assert.AreEqual("English", hud.LanguageLabelText, "chip shows the current language");
      DialogueLang.Set(DialogueLanguage.Vietnamese);
      hud.RefreshLanguageLabel();
      Assert.AreEqual("Tiếng Việt", hud.LanguageLabelText, "chip follows the switch");
      Assert.IsNotNull(go.GetComponentInChildren<Button>(true), "chip is a real button");
    } finally {
      UnityEngine.Object.DestroyImmediate(go);
      TearDown();
    }
  }
}
