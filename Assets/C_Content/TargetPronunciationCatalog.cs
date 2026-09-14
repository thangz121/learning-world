// C_Content/TargetPronunciationCatalog.cs — Phase 2.1-local content adapter (Agent C).
// Pure C# (no UnityEngine, no IO): turns parsed VocabEntry lists into the
// engine-facing ITargetPronunciationProvider. Content owns the data (vocab
// JSON speech.phonemes); the engine owns the matching. No word-specific
// branches: every word flows through PhonemeTable.FromIds.
using System;
using System.Collections.Generic;

public sealed class TargetPronunciationCatalog : ITargetPronunciationProvider {
  readonly Dictionary<string, TargetPronunciation> _byWord =
    new Dictionary<string, TargetPronunciation>(StringComparer.OrdinalIgnoreCase);

  public TargetPronunciationCatalog() { }

  public TargetPronunciationCatalog(IList<VocabEntry> vocabs) {
    Rebuild(vocabs);
  }

  public void Rebuild(IList<VocabEntry> vocabs) {
    _byWord.Clear();
    if (vocabs == null) return;
    for (int i = 0; i < vocabs.Count; i++) {
      VocabEntry v = vocabs[i];
      if (v == null || string.IsNullOrEmpty(v.id)) continue;
      if (v.phonemes == null || v.phonemes.Count == 0) continue;
      TargetPronunciation pron = PhonemeTable.FromIds(new WordId(v.id), v.phonemes);
      if (pron != null && pron.IsUsable()) _byWord[v.id] = pron;
    }
  }

  public bool TryGet(WordId word, out TargetPronunciation pronunciation) {
    pronunciation = null;
    if (string.IsNullOrEmpty(word.Value)) return false;
    return _byWord.TryGetValue(word.Value, out pronunciation);
  }

  public string[] KnownWords() {
    string[] keys = new string[_byWord.Count];
    _byWord.Keys.CopyTo(keys, 0);
    Array.Sort(keys, StringComparer.Ordinal);
    return keys;
  }

  public int Count() { return _byWord.Count; }
}

// Test seam: scripted provider without content files.
public sealed class ScriptedPronunciationProvider : ITargetPronunciationProvider {
  readonly Dictionary<string, TargetPronunciation> _map =
    new Dictionary<string, TargetPronunciation>(StringComparer.OrdinalIgnoreCase);

  public ScriptedPronunciationProvider Add(string word, params string[] phonemeIds) {
    TargetPronunciation p = PhonemeTable.FromIds(new WordId(word), new List<string>(phonemeIds));
    if (p != null) _map[word] = p;
    return this;
  }

  public bool TryGet(WordId word, out TargetPronunciation pronunciation) {
    pronunciation = null;
    if (string.IsNullOrEmpty(word.Value)) return false;
    return _map.TryGetValue(word.Value, out pronunciation);
  }

  public string[] KnownWords() {
    string[] keys = new string[_map.Count];
    _map.Keys.CopyTo(keys, 0);
    Array.Sort(keys, StringComparer.Ordinal);
    return keys;
  }
}
