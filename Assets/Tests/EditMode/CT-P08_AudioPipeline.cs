// CT-P08: Phase 2D audio-pipeline contracts (mapping -> binary -> key).
// Proves over the REAL shipped mirror + binaries (no mocks of the pipeline):
//   A. manifest<->binary inventory: 12 entries, files exist, MPEG sync,
//      sizes above the silence-suspicion floor (DESIGN DECISION: 2KB —
//      Worker TTS clips ship 6-17KB; anything smaller is not shippable).
//   B. key equality: every manifest entry's key == the key the runtime
//      request computes (vocab normal/slow + Milo lines + 2F dialogue
//      contract 1.0/1.0/Clear). A mismatch here = L2 miss = silent
//      fallback in production, so this is the highest-value test.
//   C. pregen collection covers the whole grown pack dynamically
//      (every active vocab normal+slow, every manifest line).
//   D. no hard-coded apple/ball: a synthetic word flows identically.
//   E. missing ids/binaries report falseObservable (never throw, never true).
//   F. no duplicate ids/files; no orphan binaries on disk.
// Pure EditMode (no audio hardware, no network). C# 9.0 only.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public class CT_P08_AudioPipeline {
  static List<PregenManifestIndex.Entry> Mirror() {
    List<PregenManifestIndex.Entry> entries = PregenManifestIndex.LoadStreamingMirror();
    Assert.Greater(entries.Count, 0, "shipped pregen mirror must load in EditMode");
    return entries;
  }

  static PregenManifestIndex.Entry ById(List<PregenManifestIndex.Entry> entries, string id) {
    return entries.Find(e => e.id == id);
  }

  static string RuntimeVocabKey(string word, float rate) {
    return AudioCache.CacheKey(word, new VoiceProfileId("learning_v1"),
      new LanguageCode("en-US"), rate, 0f, SpeechStyle.Clear, AudioFormat.Mp3_44100);
  }

  static string RuntimeDialogueKey(string text, string voice, float rate, float pitch, string style) {
    SpeechStyle st = (SpeechStyle)Enum.Parse(typeof(SpeechStyle), style);
    return AudioCache.CacheKey(text, new VoiceProfileId(voice),
      new LanguageCode("en-US"), rate, pitch, st, AudioFormat.Mp3_44100);
  }

  // A. Inventory: 12 shipped, valid, non-trivial.
  [Test] public void CT_P08A_ShippedInventoryValid() {
    List<PregenManifestIndex.Entry> entries = Mirror();
    Assert.AreEqual(12, entries.Count, "mirror ships 12 entries after 2D");
    string dir = Path.Combine(Application.streamingAssetsPath, "audio");
    foreach (PregenManifestIndex.Entry e in entries) {
      Assert.IsTrue(PregenManifestIndex.HasBinary(e), e.id + ": binary must exist and be non-empty");
      string path = Path.Combine(dir, e.file);
      byte[] head = new byte[2];
      using (FileStream fs = File.OpenRead(path)) {
        Assert.AreEqual(2, fs.Read(head, 0, 2), e.id + ": must readable");
      }
      Assert.AreEqual(0xFF, head[0], e.id + ": MPEG frame sync byte 1");
      Assert.AreEqual(0xE0, head[1] & 0xE0, e.id + ": MPEG frame sync byte 2");
      Assert.Greater(new FileInfo(path).Length, 2048,
        e.id + ": below 2KB silence-suspicion floor (Worker clips ship 6-17KB)");
    }
  }

  // B. Key equality: manifest params == runtime request params (L2 WILL hit).
  [Test] public void CT_P08B_ManifestKeysMatchRuntime() {
    List<PregenManifestIndex.Entry> entries = Mirror();
    // Vocab: PlayVocabularyAsync(word, Normal|Slow) params.
    var vocabExpect = new Dictionary<string, float> {
      { "apple_normal", 0.85f }, { "apple_slow", 0.70f },
      { "ball_normal", 0.85f }, { "ball_slow", 0.70f },
    };
    foreach (KeyValuePair<string, float> kv in vocabExpect) {
      PregenManifestIndex.Entry e = ById(entries, kv.Key);
      Assert.IsNotNull(e, "manifest must carry " + kv.Key);
      string word = kv.Key.EndsWith("_slow")
        ? kv.Key.Substring(0, kv.Key.Length - 5) : kv.Key.Substring(0, kv.Key.Length - 7);
      Assert.AreEqual(RuntimeVocabKey(word, kv.Value), PregenManifestIndex.CacheKeyFor(e),
        kv.Key + ": manifest key must equal the runtime L2 key (else silent fallback)");
    }
    // Milo dialogue: Milo.Say-equivalent params (rate/pitch 1.0, manifest style).
    string[,] milo = {
      { "milo_greet", "Hello! I am Milo!", "Clear" },
      { "milo_instruct_find", "Find the apple!", "Clear" },
      { "milo_instruct_bring", "Bring it to Mia!", "Clear" },
      { "milo_praise_found", "You found it!", "Excited" },
      { "milo_celebrate", "Perfect! Good job!", "Excited" },
      { "milo_encourage", "Great! Let's try together!", "Excited" },
    };
    for (int i = 0; i < milo.GetLength(0); i++) {
      PregenManifestIndex.Entry e = ById(entries, milo[i, 0]);
      Assert.IsNotNull(e, "manifest must carry " + milo[i, 0]);
      string expect = RuntimeDialogueKey(milo[i, 1], "milo_v1", 1.0f, 1.0f, milo[i, 2]);
      Assert.AreEqual(expect, PregenManifestIndex.CacheKeyFor(e),
        milo[i, 0] + ": manifest key must equal the Milo runtime key");
    }
    // 2F dialogue contract: SpeakAsync with rate/pitch 1.0 + Clear (documented
    // in the 2D handoff; 2F callers MUST use these or keys miss).
    string[,] mia = {
      { "inst_07", "Ball please!" }, { "ok_05", "Great! Ball!" },
    };
    for (int i = 0; i < mia.GetLength(0); i++) {
      PregenManifestIndex.Entry e = ById(entries, mia[i, 0]);
      Assert.IsNotNull(e, "manifest must carry " + mia[i, 0]);
      string expect = RuntimeDialogueKey(mia[i, 1], "mia_v1", 1.0f, 1.0f, "Clear");
      Assert.AreEqual(expect, PregenManifestIndex.CacheKeyFor(e),
        mia[i, 0] + ": manifest key must equal the documented 2F call key");
    }
  }

  // C. Dynamic collection covers the grown pack (no per-word hardcode).
  [Test] public void CT_P08C_PregenCoversGrownPack() {
    string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Content");
    var vocabs = new List<VocabEntry>();
    foreach (string path in Directory.GetFiles(Path.Combine(root, "vocab"), "*.json"))
      vocabs.Add(ContentDatabase.ParseVocab(File.ReadAllText(path)));
    DialoguePack pack = ContentDatabase.ParseDialoguePack(
      File.ReadAllText(Path.Combine(root, "dialogues", "manifest.json")));
    List<PreGenItem> pregen = ContentDatabase.BuildPreGenList(vocabs, pack.lines);
    foreach (VocabEntry v in vocabs) {
      if (!v.active) continue;
      Assert.IsNotNull(pregen.Find(p => p.id == v.id + "_normal"), v.id + "_normal collected");
      Assert.IsNotNull(pregen.Find(p => p.id == v.id + "_slow"), v.id + "_slow collected");
    }
    foreach (DialogueLine l in pack.lines)
      Assert.IsNotNull(pregen.Find(p => p.id == l.id), l.id + " collected");
    Assert.IsNotNull(pregen.Find(p => p.id == "ball_normal"), "ball flows without special-casing");
    Assert.IsNotNull(pregen.Find(p => p.id == "inst_07"), "new dialogue flows without special-casing");
  }

  // D. Synthetic word (never apple/ball) flows identically — no coupling.
  [Test] public void CT_P08D_NoHardCodedWords() {
    var zebra = new VocabEntry();
    zebra.id = "zebra"; zebra.active = true; zebra.displayEn = "zebra";
    zebra.audioVoice = "learning_v1"; zebra.audioLang = "en-US";
    List<PreGenItem> pregen = ContentDatabase.BuildPreGenList(
      new List<VocabEntry> { zebra }, new List<DialogueLine>());
    Assert.IsNotNull(pregen.Find(p => p.id == "zebra_normal"), "synthetic word normal collected");
    Assert.IsNotNull(pregen.Find(p => p.id == "zebra_slow"), "synthetic word slow collected");
    string key = RuntimeVocabKey("zebra", 0.85f);
    Assert.AreEqual(64, key.Length, "key is a full SHA256 hex (no degenerate path)");
    Assert.AreNotEqual(RuntimeVocabKey("apple", 0.85f), key, "distinct words, distinct keys");
  }

  // E. Missing ids/binaries report false, never throw, never true.
  [Test] public void CT_P08E_MissingIsObservable() {
    List<PregenManifestIndex.Entry> entries = Mirror();
    Assert.IsNull(ById(entries, "no_such_line"), "unknown id resolves null");
    Assert.IsFalse(PregenManifestIndex.HasBinary(null), "null entry is not present");
    var ghost = new PregenManifestIndex.Entry();
    ghost.id = "ghost"; ghost.file = "ghost_normal.mp3";
    Assert.IsFalse(PregenManifestIndex.HasBinary(ghost), "absent file reports false");
    var traversal = new PregenManifestIndex.Entry();
    traversal.id = "evil"; traversal.file = "../evil.mp3";
    Assert.IsFalse(PregenManifestIndex.HasBinary(traversal), "non-plain filename rejected");
    var badStyle = new PregenManifestIndex.Entry();
    badStyle.id = "bad"; badStyle.style = "NoSuchStyle"; badStyle.format = "Mp3_44100";
    Assert.AreEqual("", PregenManifestIndex.CacheKeyFor(badStyle), "unparseable style yields empty key");
    Assert.AreEqual("", PregenManifestIndex.CacheKeyFor(null), "null entry yields empty key");
  }

  // F. No duplicates, no orphans.
  [Test] public void CT_P08F_NoDuplicatesNoOrphans() {
    List<PregenManifestIndex.Entry> entries = Mirror();
    var ids = new HashSet<string>();
    var files = new HashSet<string>();
    foreach (PregenManifestIndex.Entry e in entries) {
      Assert.IsTrue(ids.Add(e.id), "duplicate manifest id '" + e.id + "'");
      Assert.IsTrue(files.Add(e.file), "duplicate manifest file '" + e.file + "'");
    }
    string dir = Path.Combine(Application.streamingAssetsPath, "audio");
    foreach (string path in Directory.GetFiles(dir, "*.mp3")) {
      string name = Path.GetFileName(path);
      Assert.IsTrue(files.Contains(name), "orphan binary '" + name + "' has no manifest entry");
    }
  }
}
