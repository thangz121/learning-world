// CT-S21: listen_garden (Vườn Tai Thính) — the English -> Listening foundation.
// Pins: taxonomy registration, pure round planner, yard door + launch guard,
// arena flow (wait -> hear -> tap picture, wrong -> slow replay, completion),
// the self-checked offline audio pack (voice listen_v1 keys == pregen mirror),
// the content mirror (Content/listening/listen_garden.json), the scene file +
// build settings, and re-entry adoption. C# 9.0 only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class CT_S21_ListeningGarden {
  sealed class FakeAudio : IAudioDirector {
    public readonly List<string> Lines = new List<string>();
    public readonly List<DialogueRequest> Requests = new List<DialogueRequest>();
    public Task PlayVocabularyAsync(WordId wordId, VocabularyAudioMode mode) {
      return Task.CompletedTask;
    }
    public Task SpeakAsync(DialogueRequest request) {
      Lines.Add(request.Text);
      Requests.Add(request);
      return Task.CompletedTask;
    }
    public void PlaySfx(SfxId id) { }
    public void PlayMusic(MusicId id) { }
    public void SetMusicEnabled(bool on) { }
    public void SetAudioFocus(AudioFocusMode mode) { }
  }

  [Serializable] sealed class PackJson {
    public string id; public string title; public string skill; public string voice; public string lang;
    public float audioRateNormal; public float audioRateSlow;
    public WordJson[] words; public LevelJson[] levels;
  }
  [Serializable] sealed class WordJson { public string id; public string en; public string vi; public string prop; }
  [Serializable] sealed class LevelJson { public int level; public string[] pool; public int choices; public int rounds; }

  static string ContentRoot() {
    return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Content");
  }

  static ListeningGardenBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("S21ListeningWorld");
    ListeningGardenBuilder builder = arena.AddComponent<ListeningGardenBuilder>();
    builder.BuildContent(arena.transform);
    return builder;
  }

  static ListeningGarden BuildGame(ListeningGardenBuilder builder, GameObject arena,
      GameObject player, FakeAudio audio, ActivityLifecycle life) {
    ListeningGarden game = arena.GetComponent<ListeningGarden>();
    if (game == null) game = arena.AddComponent<ListeningGarden>();
    game.Build(builder, player != null ? player.transform : null, null, audio, life);
    return game;
  }

  static GameObject PlayerAt(Vector3 local) {
    GameObject p = new GameObject("S21Player");
    p.transform.position = local;
    return p;
  }

  static void Drain(ListeningGarden game, int n) {
    for (int i = 0; i < n; i++) game.Tick(0.1f);
  }

  static ListeningGardenChoice AnswerOf(ListeningGardenBuilder b) {
    for (int i = 0; i < b.Choices.Count; i++)
      if (b.Choices[i] != null && b.Choices[i].IsAnswer) return b.Choices[i];
    return null;
  }

  static ListeningGardenChoice WrongOf(ListeningGardenBuilder b) {
    for (int i = 0; i < b.Choices.Count; i++)
      if (b.Choices[i] != null && !b.Choices[i].IsAnswer) return b.Choices[i];
    return null;
  }

  // A. Taxonomy registration.
  [Test] public void S21A_Registration() {
    GameEntry g = LearningMap.Game("listen_garden");
    Assert.IsNotNull(g, "registered");
    Assert.AreEqual("english_listening", g.SkillId, "correct skill");
    Assert.AreEqual("english", LearningMap.SubjectOfSkill(g.SkillId).Id, "correct subject");
    Assert.AreEqual("ListeningGardenPlayScene", g.SceneName, "correct scene");
    Assert.AreEqual(GameStatus.Implemented, g.Status, "IMPLEMENTED");
    Assert.IsFalse(g.HumanAccepted, "not HUMAN_ACCEPTED");
    Assert.IsFalse(LearningMap.IsPlayable("listen_garden"), "acceptance firewall holds");
    Assert.IsTrue(LearningMap.CanLaunch("listen_garden"), "may enter for play");
  }

  // B. Pure round planner: pools, rotation, distinct distractors, coverage.
  [Test] public void S21B_RoundPlanner() {
    Assert.AreEqual(1, ListeningRounds.FirstLevel, "first level");
    Assert.AreEqual(4, ListeningRounds.LastLevel, "last level");
    Assert.AreEqual(12, ListeningRounds.TotalRounds(), "2+3+3+4 rounds in one visit");
    Assert.AreEqual(2, ListeningRounds.ChoicesFor(1), "LV1 two choices");
    Assert.AreEqual(3, ListeningRounds.ChoicesFor(2), "LV2 three choices");
    Assert.AreEqual(4, ListeningRounds.ChoicesFor(3), "LV3 four choices");
    Assert.AreEqual(4, ListeningRounds.ChoicesFor(4), "LV4 four choices");
    Assert.AreEqual(2, ListeningRounds.PoolFor(1).Length, "LV1 pool");
    Assert.AreEqual(8, ListeningRounds.PoolFor(4).Length, "LV4 pool");
    var seenAnswers = new HashSet<int>();
    for (int level = 1; level <= 4; level++) {
      for (int round = 0; round < ListeningRounds.RoundsFor(level); round++) {
        string target; string[] choices; int answerIndex;
        ListeningRounds.BuildRound(level, round, out target, out choices, out answerIndex);
        Assert.AreEqual(ListeningRounds.ChoicesFor(level), choices.Length, "choice count");
        Assert.IsTrue(answerIndex >= 0 && answerIndex < choices.Length, "answer index in range");
        Assert.AreEqual(target, choices[answerIndex], "answer slot holds the target");
        Assert.IsTrue(ListeningWords.IsKnown(target), "target is a known word");
        var set = new HashSet<string>();
        foreach (string c in choices) {
          Assert.IsTrue(ListeningWords.IsKnown(c), "choice is a known word");
          Assert.IsTrue(set.Add(c), "no duplicate choice: " + c);
        }
        seenAnswers.Add(answerIndex);
        // determinism: same args, same round
        string t2; string[] c2; int a2;
        ListeningRounds.BuildRound(level, round, out t2, out c2, out a2);
        Assert.AreEqual(target, t2, "deterministic target");
        Assert.AreEqual(answerIndex, a2, "deterministic answer slot");
        Assert.AreEqual(string.Join(",", choices), string.Join(",", c2), "deterministic choices");
      }
    }
    Assert.GreaterOrEqual(seenAnswers.Count, 2, "answer position rotates (no side bias)");
    // Full coverage of LV1/LV2 pools as targets across their rounds.
    var lv1 = new HashSet<string>();
    for (int r = 0; r < ListeningRounds.RoundsFor(1); r++) {
      string t; string[] c; int a;
      ListeningRounds.BuildRound(1, r, out t, out c, out a);
      lv1.Add(t);
    }
    Assert.AreEqual(2, lv1.Count, "both LV1 words get a turn");
    // Words/audio registry consistency.
    Assert.AreEqual("listen_apple_normal", ListeningWords.AudioId("apple", false), "audio id normal");
    Assert.AreEqual("listen_apple_slow", ListeningWords.AudioId("apple", true), "audio id slow");
    Assert.AreEqual("grapes", ListeningWords.TextEn("grape"), "grape speaks as grapes");
    Assert.AreEqual("grapes", ListeningWords.PropName("grape"), "grape uses the bunch prop");
    Assert.AreEqual("quả táo", ListeningWords.DisplayVi("apple"), "vi label");
  }

  // C. Yard door + launch guard (English skill yard only).
  [Test] public void S21C_YardDoorAndLaunch() {
    GameObject r = new GameObject("S21Yard");
    try {
      SelectionYardBuilder b = r.AddComponent<SelectionYardBuilder>();
      b.Level = "game";
      b.SkillId = "english_listening";
      b.BuildContent(r.transform);
      Assert.AreEqual(1, b.GatePortals.Count, "one listening door");
      Assert.AreEqual("listen_garden", b.GateTargetIds[0]);
      Assert.IsNotNull(FindDeep(r.transform, "SYPreview_listen_garden"), "wordless bell preview");
    } finally { UnityEngine.Object.DestroyImmediate(r); }
    GameObject go = new GameObject("S21Area");
    try {
      SelectionYardArea area = go.AddComponent<SelectionYardArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Game, "", "english_listening");
      area.PlayGame("listen_garden");
      Assert.AreEqual(1, area.PlayRequests, "listening yard launches the garden");
      Assert.AreEqual("skill:english", area.BackTargetForTests(), "return is the English skill yard");
      area.PlayGame("rabbit_feeding");
      Assert.AreEqual(1, area.PlayRequests, "a math game is refused here");
    } finally { UnityEngine.Object.DestroyImmediate(go); }
  }

  // D. Arena flow: hear -> tap picture; wrong -> slow replay; correct -> next.
  [Test] public void S21D_ArenaFlow() {
    GameObject arena;
    ListeningGardenBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(ListeningGardenBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ListeningGarden game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("listen_garden", "test"));
      Drain(game, 3);
      Assert.AreEqual(ListeningGarden.Phase.Play, game.Current, "starts at play");
      Assert.AreEqual(1, game.Level, "LV1 first");
      Assert.AreEqual(2, builder.Choices.Count, "two pictures");
      Assert.IsNotNull(game.TargetWord, "a target word exists");
      ListeningGardenChoice ans = AnswerOf(builder);
      ListeningGardenChoice wrong = WrongOf(builder);
      Assert.IsNotNull(ans, "an answer exists");
      Assert.IsNotNull(wrong, "a distractor exists");
      // The bell replays the current word (explicit request wins over timers).
      int beforeBell = audio.Requests.Count;
      game.ReplayPrompt();
      Assert.Greater(audio.Requests.Count, beforeBell, "bell replays the prompt");
      game.TryTapChoice(wrong);
      Assert.AreEqual(1, game.WrongCount, "wrong tap counted");
      Assert.IsFalse(game.LastCorrect, "no false win");
      Assert.AreEqual(ListeningGarden.Phase.Play, game.Current, "still playing");
      bool sawSlowAfterWrong = false;
      for (int i = 0; i < 30; i++) {
        game.Tick(0.1f);
        for (int k = 0; k < audio.Requests.Count; k++) {
          DialogueRequest rq = audio.Requests[k];
          if (rq.Voice.Value == "listen_v1" && Math.Abs(rq.Rate - 0.70f) < 0.001f) sawSlowAfterWrong = true;
        }
        if (sawSlowAfterWrong) break;
      }
      Assert.IsTrue(sawSlowAfterWrong, "a wrong tap triggers the slow replay");
      string firstTarget = game.TargetWord;
      game.TryTapChoice(ans);
      Assert.IsTrue(game.LastCorrect, "correct tap wins the round");
      Assert.AreEqual(1, game.RoundIndex, "round advances");
      // Item prompts go through the instrumented director with the frozen voice/params.
      bool sawNormal = false;
      for (int k = 0; k < audio.Requests.Count; k++) {
        DialogueRequest rq = audio.Requests[k];
        if (rq.Voice.Value == "listen_v1" && Math.Abs(rq.Rate - 0.85f) < 0.001f
            && rq.Text == firstTarget) sawNormal = true;
      }
      Assert.IsTrue(sawNormal, "the word prompt was played (listen_v1, normal rate)");
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line '" + line + "' " + why);
      }
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  // E. Completion: one visit plays LV1..LV4 (12 rounds) then completes.
  [Test] public void S21E_CompletionAndLifecycle() {
    GameObject arena;
    ListeningGardenBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(ListeningGardenBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("listen_garden", "test");
      ListeningGarden game = BuildGame(builder, arena, player, audio, life);
      Drain(game, 3);
      int guard = 0;
      while (!game.Completed && guard++ < 40) {
        ListeningGardenChoice ans = AnswerOf(builder);
        Assert.IsNotNull(ans, "round " + game.RoundIndex + ": answer pod exists");
        game.TryTapChoice(ans);
      }
      Assert.IsTrue(game.Completed, "visit completes after all rounds");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.AreEqual(ListeningGarden.Phase.Done, game.Current, "done phase");
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  // F. Offline pack: mirror carries all 16 listen masters with exact runtime keys.
  [Test] public void S21F_OfflineAudioKeys() {
    List<PregenManifestIndex.Entry> entries = PregenManifestIndex.LoadStreamingMirror();
    Assert.Greater(entries.Count, 0, "mirror loads");
    foreach (string id in ListeningWords.All) {
      foreach (bool slow in new[] { false, true }) {
        string mid = ListeningWords.AudioId(id, slow);
        PregenManifestIndex.Entry e = entries.Find(x => x.id == mid);
        Assert.IsNotNull(e, "mirror must carry " + mid);
        Assert.IsTrue(PregenManifestIndex.HasBinary(e), mid + ": binary must exist");
        string expect = AudioCache.CacheKey(ListeningWords.TextEn(id),
          new VoiceProfileId(ListeningWords.VoiceId), new LanguageCode(ListeningWords.Lang),
          slow ? ListeningWords.RateSlow : ListeningWords.RateNormal, 0f,
          SpeechStyle.Clear, AudioFormat.Mp3_44100);
        Assert.AreEqual(expect, PregenManifestIndex.CacheKeyFor(e),
          mid + ": manifest key must equal the runtime L2 key (else silent fallback)");
      }
    }
  }

  // G. Content mirror: Content/listening/listen_garden.json == the C# registry.
  [Test] public void S21G_ContentMirror() {
    string path = Path.Combine(ContentRoot(), "listening", "listen_garden.json");
    Assert.IsTrue(File.Exists(path), "pack file exists: " + path);
    PackJson pack = JsonUtility.FromJson<PackJson>(File.ReadAllText(path));
    Assert.IsNotNull(pack, "pack parses");
    Assert.AreEqual("listen_garden", pack.id, "pack id");
    Assert.AreEqual("english_listening", pack.skill, "pack skill");
    Assert.AreEqual(ListeningWords.VoiceId, pack.voice, "pack voice frozen");
    Assert.AreEqual(ListeningWords.Lang, pack.lang, "pack lang frozen");
    Assert.AreEqual(ListeningWords.RateNormal, pack.audioRateNormal, "normal rate frozen");
    Assert.AreEqual(ListeningWords.RateSlow, pack.audioRateSlow, "slow rate frozen");
    Assert.AreEqual(ListeningWords.All.Length, pack.words.Length, "word count");
    for (int i = 0; i < pack.words.Length; i++) {
      Assert.AreEqual(ListeningWords.All[i], pack.words[i].id, "word order " + i);
      Assert.AreEqual(ListeningWords.TextEn(pack.words[i].id), pack.words[i].en, "spoken text");
      Assert.AreEqual(ListeningWords.DisplayVi(pack.words[i].id), pack.words[i].vi, "vi label");
      Assert.AreEqual(ListeningWords.PropName(pack.words[i].id), pack.words[i].prop, "prop");
    }
    Assert.AreEqual(4, pack.levels.Length, "four levels");
    for (int i = 0; i < pack.levels.Length; i++) {
      LevelJson lv = pack.levels[i];
      Assert.AreEqual(ListeningRounds.ChoicesFor(lv.level), lv.choices, "level choices");
      Assert.AreEqual(ListeningRounds.RoundsFor(lv.level), lv.rounds, "level rounds");
      string[] pool = ListeningRounds.PoolFor(lv.level);
      Assert.AreEqual(pool.Length, lv.pool.Length, "level pool size");
      for (int k = 0; k < pool.Length; k++)
        Assert.AreEqual(pool[k], lv.pool[k], "level pool order");
    }
  }

  // H. Scene file + build settings pin (mirrors the CT-P53 pattern).
  [Test] public void S21H_ScenePin() {
    string scenePath = Path.Combine(Application.dataPath,
      "A_World", "ListeningGarden", "ListeningGardenPlayScene.unity");
    Assert.IsTrue(File.Exists(scenePath), "scene file exists");
    string yaml = File.ReadAllText(scenePath);
    Assert.IsTrue(yaml.Contains("ListeningGardenWorld"), "scene root named");
    bool inBuild = false;
    foreach (UnityEditor.EditorBuildSettingsScene s in UnityEditor.EditorBuildSettings.scenes) {
      if (s.enabled && s.path.Replace('\\', '/').EndsWith("ListeningGardenPlayScene.unity")) {
        inBuild = true;
        break;
      }
    }
    Assert.IsTrue(inBuild, "scene registered in Build Settings");
  }

  // I. Re-entry: no duplicate component/listeners.
  [Test] public void S21I_ReentryNoDuplicate() {
    GameObject arena;
    ListeningGardenBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(ListeningGardenBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      ActivityLifecycle life = new ActivityLifecycle("listen_garden", "test");
      ListeningGarden a = BuildGame(builder, arena, player, audio, life);
      ListeningGarden b = BuildGame(builder, arena, player, audio, life);
      Assert.AreSame(a, b, "same component");
      Assert.AreEqual(1, arena.GetComponents<ListeningGarden>().Length, "no duplicate listeners");
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  static Transform FindDeep(Transform t, string name) {
    if (t == null) return null;
    if (t.name == name) return t;
    for (int i = 0; i < t.childCount; i++) {
      Transform f = FindDeep(t.GetChild(i), name);
      if (f != null) return f;
    }
    return null;
  }
}
