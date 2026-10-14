// CT-S22: speaking_stage (Sân Khấu Nhí) — the English -> Speaking foundation.
// Pins: taxonomy registration; the word pack mirror (Content/vocab phonemes) +
// the four-level feedback phrases; the hybrid backend merge (FRR-first guard,
// graceful service loss); the gate capture routing; the arena flow with a
// scripted recognizer (levels -> lines -> attempts -> completion, environment
// failures never fail the child); the local service client helpers; scene pin.
// C# 9.0 only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

public class CT_S22_SpeakingStage {
  // ---- fakes ------------------------------------------------------------------

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

  sealed class FakeRecognizer : ISpeechRecognizer {
    public readonly Queue<SpeakingAssessment> Script = new Queue<SpeakingAssessment>();
    public readonly List<string> Targets = new List<string>();
    public int Calls;
    public event Action<SpeechCapability> CapabilityChanged;
    public SpeechCapability Capability {
      get { return new SpeechCapability { Status = MicStatus.Ready, DeviceName = "fake", DeviceCount = 1 }; }
    }
    public Task<SpeakingAssessment> StartAttemptAsync(WordId target, CancellationToken ct) {
      Calls++;
      Targets.Add(target.Value);
      SpeakingAssessment a = Script.Count > 0 ? Script.Dequeue() : Make(SpeakingDecision.Unclear, 0.2f);
      return Task.FromResult(a);
    }
    public void Cancel() { }

    public static SpeakingAssessment Make(SpeakingDecision decision, float score) {
      var a = new SpeakingAssessment();
      a.AttemptId = "test";
      a.Decision = decision;
      a.AttemptDetected = decision != SpeakingDecision.NoSpeech && decision != SpeakingDecision.Unclear
        ? true : decision == SpeakingDecision.Unclear;
      a.SpeechDetected = decision != SpeakingDecision.NoSpeech;
      a.HasPronunciationEvidence = false;
      a.PronunciationScore = float.NaN;
      a.LexicalMatchScore = float.NaN;
      a.IntelligibilityIsProxy = true;
      a.OverallScore = score;
      a.Evidence = "fake=" + decision;
      return a;
    }

    public static SpeakingAssessment Env() {
      var a = Make(SpeakingDecision.Unclear, 0f);
      a.IsEnvironmentError = true;
      a.AttemptDetected = false;
      return a;
    }
  }

  sealed class FakeProvider : ISpeechAssessmentProvider {
    public string Id = "fake";
    public bool IsErr;
    public string ErrReason = "";
    public string Transcript = "";
    public float Confidence;
    public bool HasAcoustic;
    public float AcousticMatch;
    public bool MissingEnding;
    public bool WeakEnding;
    public int Calls;
    public string ProviderId { get { return Id; } }
    public bool ProvidesTranscript { get { return true; } }
    public bool ProvidesPhonemeEvidence { get { return false; } }
    public bool RequiresNetwork { get { return false; } }
    public Task<SpeechRecognitionResult> RecognizeAsync(CapturedSpeech audio, WordId target,
        CancellationToken ct) {
      Calls++;
      var r = new SpeechRecognitionResult();
      r.ProviderId = Id;
      r.HasSpeech = true;
      r.MeanEnergy = 0.5f;
      r.AudioDurationSec = 1.2f;
      r.SpeechDurationSec = 0.8f;
      r.Transcript = Transcript;
      r.RecognitionConfidence = Confidence;
      r.IsError = IsErr;
      r.ErrorReason = IsErr ? (string.IsNullOrEmpty(ErrReason) ? "provider_error" : ErrReason) : "";
      var ac = PronunciationEvidence.None();
      if (HasAcoustic) {
        ac.HasAcousticData = true;
        ac.AcousticMatch = AcousticMatch;
        ac.AcousticMissingEnding = MissingEnding;
        ac.AcousticWeakEnding = WeakEnding;
        ac.AcousticSyllables = 1;
      }
      r.Pronunciation = ac;
      return Task.FromResult(r);
    }
  }

  sealed class FakeServiceClient : LocalAssessServiceClient {
    public string NextTranscript = "";
    public float NextConfidence = 0.9f;
    public bool Throw;
    public int Calls;
    public override Task<LocalAssessServiceClient.Reply> AssessAsync(byte[] wav, string target,
        CancellationToken ct) {
      Calls++;
      if (Throw) throw new Exception("dead");
      var reply = new LocalAssessServiceClient.Reply();
      reply.transcript = NextTranscript;
      reply.confidence = NextConfidence;
      reply.no_speech = 0.05f;
      reply.latency_ms = 7;
      reply.engine = "base.en";
      return Task.FromResult(reply);
    }
  }

  sealed class FakeCapture : ISpeechAudioCapture {
    public int Calls;
    public string Tag = "x";
    public bool IsCapturing { get { return true; } }
    public Task<CapturedSpeech> CaptureAsync(float maxSec, float silenceSec, CancellationToken ct) {
      Calls++;
      var c = new CapturedSpeech();
      c.Samples = new float[1600];
      c.SampleRate = 16000;
      c.Channels = 1;
      c.DurationSec = 0.1f;
      c.MeanEnergy = 0.02f;
      c.PeakEnergy = 0.1f;
      c.VoicedSec = 0.05f;
      c.Error = "";
      return Task.FromResult(c);
    }
    public void Cancel() { }
  }

  sealed class FakeMic : IMicrophoneDevice {
    public event Action<MicStatus> StatusChanged;
    public MicStatus Status { get; set; }
    public string SelectedDevice { get { return Status == MicStatus.Ready ? "mic0" : null; } }
    public string[] Devices { get { return Status == MicStatus.Ready ? new[] { "mic0" } : new string[0]; } }
    public SpeechCapability Capability {
      get { return new SpeechCapability { Status = Status, DeviceCount = Devices.Length }; }
    }
    public void Refresh() { if (StatusChanged != null) { } }
    public void ReportCaptureFailure() { Status = MicStatus.Error; }
    public FakeMic(MicStatus status) { Status = status; }
  }

  // ---- helpers ----------------------------------------------------------------

  static SpeakingStageBuilder BuildArena(out GameObject arena) {
    arena = new GameObject("S22SpeakingWorld");
    SpeakingStageBuilder builder = arena.AddComponent<SpeakingStageBuilder>();
    builder.BuildContent(arena.transform);
    return builder;
  }

  static SpeakingStage BuildGame(SpeakingStageBuilder builder, GameObject arena,
      GameObject player, FakeAudio audio, ActivityLifecycle life, ISpeechRecognizer speech,
      MicSetupGate gate) {
    SpeakingStage game = arena.GetComponent<SpeakingStage>();
    if (game == null) game = arena.AddComponent<SpeakingStage>();
    game.Build(builder, player != null ? player.transform : null, null, audio, life, speech, gate);
    return game;
  }

  static GameObject PlayerAt(Vector3 local) {
    GameObject p = new GameObject("S22Player");
    p.transform.position = local;
    return p;
  }

  static void PumpTo(SpeakingStage game, SpeakingStage.Phase target, int maxTicks) {
    for (int i = 0; i < maxTicks && game.Current != target; i++) game.Tick(0.1f);
  }

  static bool SaidAny(FakeAudio audio, string a, string b) {
    for (int i = 0; i < audio.Lines.Count; i++) {
      string line = audio.Lines[i];
      if (line != null && (line.Contains(a) || line.Contains(b))) return true;
    }
    return false;
  }

  static string ContentRoot() {
    return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Content");
  }

  [Serializable] sealed class VocabJson { public string id; public SpeechJson speech; }
  [Serializable] sealed class SpeechJson { public string[] phonemes; }

  // ---- A. registration + yard --------------------------------------------------

  [Test] public void S22A_RegistrationAndYard() {
    GameEntry g = LearningMap.Game("speaking_stage");
    Assert.IsNotNull(g, "registered");
    Assert.AreEqual("english_speaking", g.SkillId, "correct skill");
    Assert.AreEqual("english", LearningMap.SubjectOfSkill(g.SkillId).Id, "correct subject");
    Assert.AreEqual("SpeakingStagePlayScene", g.SceneName, "correct scene");
    Assert.AreEqual(GameStatus.Implemented, g.Status, "IMPLEMENTED");
    Assert.IsFalse(LearningMap.IsPlayable("speaking_stage"), "acceptance firewall holds");
    Assert.IsTrue(LearningMap.CanLaunch("speaking_stage"), "may enter for play");

    GameObject r = new GameObject("S22Yard");
    try {
      SelectionYardBuilder b = r.AddComponent<SelectionYardBuilder>();
      b.Level = "game";
      b.SkillId = "english_speaking";
      b.BuildContent(r.transform);
      Assert.AreEqual(1, b.GatePortals.Count, "one speaking door");
      Assert.AreEqual("speaking_stage", b.GateTargetIds[0]);
      Assert.IsNotNull(FindDeep(r.transform, "SYPreview_speaking_stage"), "wordless mic preview");
    } finally { UnityEngine.Object.DestroyImmediate(r); }
    GameObject go = new GameObject("S22Area");
    try {
      SelectionYardArea area = go.AddComponent<SelectionYardArea>();
      area.Bind(null, null, null, null, null, Vector3.zero);
      area.SetCurrentForTests(SelectionYardArea.YardLevel.Game, "", "english_speaking");
      area.PlayGame("speaking_stage");
      Assert.AreEqual(1, area.PlayRequests, "speaking yard launches the stage");
      Assert.AreEqual("skill:english", area.BackTargetForTests(), "return is the English skill yard");
      area.PlayGame("listen_garden");
      Assert.AreEqual(1, area.PlayRequests, "the listening game is refused here");
    } finally { UnityEngine.Object.DestroyImmediate(go); }
  }

  // ---- B. pack + rounds + phrases ----------------------------------------------

  [Test] public void S22B_PackAndRounds() {
    Assert.AreEqual(8, SpeakingPack.Words.Length, "eight speaking words");
    Assert.AreEqual(8, SpeakingPack.TotalRounds(), "4 + 4 rounds in one visit");
    Assert.AreEqual("apple", SpeakingPack.WordFor(1, 0), "LV1 order");
    Assert.AreEqual("orange", SpeakingPack.WordFor(1, 3), "LV1 order end");
    Assert.AreEqual("carrot", SpeakingPack.WordFor(2, 0), "LV2 order");
    Assert.AreEqual("grape", SpeakingPack.WordFor(2, 3), "LV2 order end");
    Assert.AreEqual(3, SpeakingPack.AttemptsPerWord, "three tries per word");
    foreach (string w in SpeakingPack.Words) {
      Assert.IsNotNull(SpeakingPack.PhonemesFor(w), "phonemes for " + w);
      Assert.Greater(SpeakingPack.PhonemesFor(w).Length, 1, "usable phoneme list for " + w);
    }
    string en; string vi;
    SpeakingFeedback.PhraseFor(SpeechLevel.Perfect, out en, out vi);
    Assert.AreEqual("Xuất sắc!", vi, "perfect = xuất sắc");
    SpeakingFeedback.PhraseFor(SpeechLevel.Great, out en, out vi);
    Assert.AreEqual("Đúng rồi!", vi, "great = đúng rồi");
    SpeakingFeedback.PhraseFor(SpeechLevel.Almost, out en, out vi);
    Assert.AreEqual("Gần đúng rồi!", vi, "almost = gần đúng rồi");
    SpeakingFeedback.PhraseFor(SpeechLevel.TryTogether, out en, out vi);
    Assert.AreEqual("Thử lại nhé!", vi, "try again = thử lại");
  }

  // ---- C. content mirror (vocab phonemes == pack) -------------------------------

  [Test] public void S22C_ContentMirror() {
    string[] words = { "apple", "ball", "banana", "orange", "carrot", "tomato",
                       "watermelon", "grape", "red", "one", "please", "teddy" };
    foreach (string w in words) {
      string path = Path.Combine(ContentRoot(), "vocab", w + ".json");
      Assert.IsTrue(File.Exists(path), "vocab exists: " + path);
      VocabJson v = JsonUtility.FromJson<VocabJson>(File.ReadAllText(path));
      Assert.IsNotNull(v, w + " parses");
      Assert.IsNotNull(v.speech, w + " speech block");
      Assert.IsNotNull(v.speech.phonemes, w + " carries phonemes");
      string[] pack = SpeakingPack.PhonemesFor(w);
      Assert.IsNotNull(pack, "pack carries " + w);
      Assert.AreEqual(v.speech.phonemes.Length, pack.Length, w + " phoneme count");
      for (int i = 0; i < pack.Length; i++)
        Assert.AreEqual(v.speech.phonemes[i], pack[i], w + " phoneme " + i);
    }
    // The runtime catalog serves every mirrored word + the spoken alias.
    TargetPronunciationCatalog catalog = new TargetPronunciationCatalog();
    SpeakingPack.Apply((word, phonemes) => catalog.Register(word, phonemes));
    Assert.AreEqual(13, catalog.Count(), "catalog covers 12 words + the grapes alias");
    TargetPronunciation pron;
    Assert.IsTrue(catalog.TryGet(new WordId("watermelon"), out pron), "watermelon resolves");
    Assert.IsTrue(pron.IsUsable(), "watermelon usable");
    Assert.IsTrue(catalog.TryGet(new WordId("grapes"), out pron),
      "spoken alias 'grapes' resolves (child hears/says grapes, vocab id is grape)");
    Assert.IsNotNull(SpeakingPack.PhonemesFor("grapes"), "alias phonemes available");
  }

  // ---- D. hybrid merge ----------------------------------------------------------

  static CapturedSpeech Audio() {
    var c = new CapturedSpeech();
    c.Samples = new float[3200];
    for (int i = 0; i < c.Samples.Length; i++) c.Samples[i] = 0.2f;
    c.SampleRate = 16000;
    c.Channels = 1;
    c.DurationSec = 0.2f;
    c.MeanEnergy = 0.2f;
    c.VoicedSec = 0.2f;
    c.Error = "";
    return c;
  }

  [Test] public void S22D_HybridMerge() {
    var policy = SpeakingPassPolicy.Default();
    var acoustic = new FakeProvider { Id = "acoustic", HasAcoustic = true, AcousticMatch = 0.5f };
    var service = new FakeProvider { Id = "svc", Transcript = "ball", Confidence = 0.8f };
    var hybrid = new HybridSpeechAssessmentProvider(acoustic, service, policy);
    SpeechRecognitionResult r = hybrid.RecognizeAsync(Audio(), new WordId("ball"), default(CancellationToken)).Result;
    Assert.AreEqual("ball", r.Transcript, "transcript attached");
    Assert.IsTrue(r.Pronunciation.HasAcousticData, "acoustic evidence kept");
    Assert.IsTrue(r.ProviderId.StartsWith("hybrid"), "merged provider id");

    // Service error -> acoustic-only, never an error at the hybrid level.
    var deadService = new FakeProvider { Id = "svc", IsErr = true };
    var hybrid2 = new HybridSpeechAssessmentProvider(acoustic, deadService, policy);
    SpeechRecognitionResult r2 = hybrid2.RecognizeAsync(Audio(), new WordId("ball"), default(CancellationToken)).Result;
    Assert.IsFalse(r2.IsError, "service loss never errors");
    Assert.IsFalse(r2.HasTranscript(), "no transcript without the service");
    Assert.AreEqual("acoustic", r2.ProviderId, "acoustic provider preserved");

    // FRR-first hallucination guard: strong clean audio + unrelated transcript -> drop.
    var strong = new FakeProvider { Id = "acoustic", HasAcoustic = true, AcousticMatch = 0.9f };
    var wrongSvc = new FakeProvider { Id = "svc", Transcript = "banana", Confidence = 0.9f };
    var hybrid3 = new HybridSpeechAssessmentProvider(strong, wrongSvc, policy);
    SpeechRecognitionResult r3 = hybrid3.RecognizeAsync(Audio(), new WordId("ball"), default(CancellationToken)).Result;
    Assert.IsFalse(r3.HasTranscript(), "hallucinated transcript dropped on strong clean audio");

    // Weak acoustic + transcript -> transcript kept (lexical evidence matters).
    var weak = new FakeProvider { Id = "acoustic", HasAcoustic = true, AcousticMatch = 0.2f };
    var okSvc = new FakeProvider { Id = "svc", Transcript = "ball", Confidence = 0.9f };
    var hybrid4 = new HybridSpeechAssessmentProvider(weak, okSvc, policy);
    SpeechRecognitionResult r4 = hybrid4.RecognizeAsync(Audio(), new WordId("ball"), default(CancellationToken)).Result;
    Assert.AreEqual("ball", r4.Transcript, "weak audio keeps the transcript");

    // Acoustic error -> the service is never called.
    var errAcoustic = new FakeProvider { Id = "acoustic", IsErr = true };
    var countedSvc = new FakeProvider { Id = "svc", Transcript = "ball" };
    var hybrid5 = new HybridSpeechAssessmentProvider(errAcoustic, countedSvc, policy);
    SpeechRecognitionResult r5 = hybrid5.RecognizeAsync(Audio(), new WordId("ball"), default(CancellationToken)).Result;
    Assert.IsTrue(r5.IsError, "capture error passes through");
    Assert.AreEqual(0, countedSvc.Calls, "service not called on capture error");
  }

  // ---- E. local service provider mapping ---------------------------------------

  [Test] public void S22E_ServiceProviderMapping() {
    var client = new FakeServiceClient { NextTranscript = "apple", NextConfidence = 0.77f };
    var provider = new LocalServiceAssessmentProvider(client);
    SpeechRecognitionResult r = provider.RecognizeAsync(Audio(), new WordId("apple"), default(CancellationToken)).Result;
    Assert.IsFalse(r.IsError, "ok");
    Assert.AreEqual("apple", r.Transcript, "transcript mapped");
    Assert.AreEqual(0.77f, r.RecognitionConfidence, 0.001f, "confidence mapped");
    Assert.IsTrue(r.ProviderId.StartsWith("local-service/"), "provider id carries the engine");
    Assert.IsFalse(provider.ProvidesPhonemeEvidence, "transcript-only honesty");

    // Silence: the service is never called (policy NoSpeech path decides).
    var client2 = new FakeServiceClient();
    var provider2 = new LocalServiceAssessmentProvider(client2);
    var silent = Audio();
    silent.MeanEnergy = 0.0001f;
    silent.Samples = new float[1600];
    for (int i = 0; i < silent.Samples.Length; i++) silent.Samples[i] = 0.0001f;
    SpeechRecognitionResult r2 = provider2.RecognizeAsync(silent, new WordId("apple"), default(CancellationToken)).Result;
    Assert.IsFalse(r2.HasSpeech, "silence is not speech");
    Assert.AreEqual(0, client2.Calls, "service not called on silence");

    // Service failure -> honest provider_error (hybrid degrades to acoustic).
    var client3 = new FakeServiceClient { Throw = true };
    var provider3 = new LocalServiceAssessmentProvider(client3);
    SpeechRecognitionResult r3 = provider3.RecognizeAsync(Audio(), new WordId("apple"), default(CancellationToken)).Result;
    Assert.IsTrue(r3.IsError, "service failure is an honest error");
    Assert.AreEqual("provider_error", r3.ErrorReason, "provider_error code");
  }

  // ---- F. client helpers (WAV + JSON) ------------------------------------------

  [Test] public void S22F_ClientHelpers() {
    float[] samples = { 0f, 1f, -1f, 0.5f };
    byte[] wav = LocalAssessServiceClient.EncodeWav(samples, 16000, 1);
    Assert.AreEqual(44 + samples.Length * 2, wav.Length, "wav size");
    Assert.AreEqual((byte)'R', wav[0], "RIFF");
    Assert.AreEqual((byte)'I', wav[1]);
    Assert.AreEqual((byte)'F', wav[2]);
    Assert.AreEqual((byte)'F', wav[3]);
    Assert.AreEqual((byte)'W', wav[8], "WAVE");
    Assert.AreEqual((byte)'d', wav[36], "data");
    short max = (short)(wav[44 + 2] | (wav[44 + 3] << 8));
    Assert.AreEqual(32767, max, "1.0 -> 32767 PCM16LE");
    short min = (short)(wav[44 + 4] | (wav[44 + 5] << 8));
    Assert.AreEqual(-32767, min, "-1.0 -> -32767");

    LocalAssessServiceClient.Reply reply;
    Assert.IsTrue(LocalAssessServiceClient.TryParseReply(
      "{\"transcript\": \"apple\", \"confidence\": 0.9, \"no_speech\": 0.1, \"latency_ms\": 12, \"engine\": \"base.en\", \"accuracy_percent\": 100, \"feedback_vi\": \"Xuất sắc!\"}",
      out reply), "valid reply parses");
    Assert.AreEqual("apple", reply.transcript);
    Assert.AreEqual(100, reply.accuracy_percent);
    Assert.AreEqual("Xuất sắc!", reply.feedback_vi);
    Assert.IsFalse(LocalAssessServiceClient.TryParseReply("{\"error\": \"boom\"}", out reply),
      "error payload rejected");
    Assert.IsFalse(LocalAssessServiceClient.TryParseReply("", out reply), "empty rejected");
  }

  // ---- G. gate capture routing -------------------------------------------------

  [Test] public void S22G_GateCaptureRouting() {
    var local = new FakeCapture { Tag = "local" };
    var phone = new FakeCapture { Tag = "phone" };

    // Local ready -> local capture.
    var gateLocal = new MicSetupGate(new FakeMic(MicStatus.Ready), new FakeMic(MicStatus.NoDevice));
    gateLocal.EvaluateAtStartup();
    var cap1 = new GateSpeechCapture(local, phone, gateLocal);
    cap1.CaptureAsync(3f, 1f, default(CancellationToken)).Wait();
    Assert.AreEqual(1, local.Calls, "local capture used");
    Assert.AreEqual(0, phone.Calls, "phone not used");

    // No local mic -> phone linked -> phone capture.
    var localDead = new FakeMic(MicStatus.NoDevice);
    var phoneDev = new PhoneMicrophoneDevice();
    var gatePhone = new MicSetupGate(localDead, phoneDev);
    gatePhone.EvaluateAtStartup();
    gatePhone.AcceptPhoneOffer();
    phoneDev.ReportLinkUp("phone");
    gatePhone.OnPhoneLink(PhoneLinkState.PhoneLinked);
    Assert.AreEqual(MicSetupState.ReadyPhone, gatePhone.State, "phone ready");
    var cap2 = new GateSpeechCapture(new FakeCapture(), phone, gatePhone);
    cap2.CaptureAsync(3f, 1f, default(CancellationToken)).Wait();
    Assert.AreEqual(1, phone.Calls, "phone capture used");
  }

  // ---- H. arena flow ------------------------------------------------------------

  [Test] public void S22H_ArenaFlow() {
    GameObject arena;
    SpeakingStageBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(SpeakingStageBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      var rec = new FakeRecognizer();
      rec.Script.Enqueue(FakeRecognizer.Make(SpeakingDecision.StrongPass, 1.0f));
      rec.Script.Enqueue(FakeRecognizer.Make(SpeakingDecision.Pass, 0.82f));
      SpeakingStage game = BuildGame(builder, arena, player, audio, new ActivityLifecycle("speaking_stage", "t"), rec, null);
      PumpTo(game, SpeakingStage.Phase.Listening, 60);
      Assert.AreEqual("apple", game.TargetWord, "LV1 starts with apple");
      Assert.AreEqual(1, rec.Targets.Count, "one attempt requested");
      Assert.AreEqual("apple", rec.Targets[0], "attempt target is the spoken word");
      game.Tick(0.1f); // the scripted task completes -> assessment handled
      for (int i = 0; i < 30; i++) game.Tick(0.05f); // let the paced voice submit
      Assert.IsTrue(game.HasLastLevel, "a level was produced");
      Assert.AreEqual(SpeechLevel.Perfect, game.LastLevel, "StrongPass -> Perfect");
      Assert.AreEqual(100, game.LastPercent, "100%");
      Assert.IsTrue(SaidAny(audio, "Xuất sắc!", "Excellent!"), "perfect phrase spoken");
      PumpTo(game, SpeakingStage.Phase.Listening, 120);
      game.Tick(0.1f);
      for (int i = 0; i < 30; i++) game.Tick(0.05f);
      Assert.AreEqual(SpeechLevel.Great, game.LastLevel, "Pass -> Great");
      Assert.IsTrue(SaidAny(audio, "Đúng rồi!", "That's right!"), "great phrase spoken");
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line '" + line + "' " + why);
      }
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S22I_RetryAndCompletion() {
    GameObject arena;
    SpeakingStageBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(SpeakingStageBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      var rec = new FakeRecognizer();
      // Round 1: three weak attempts -> give up, advance without a pass.
      rec.Script.Enqueue(FakeRecognizer.Make(SpeakingDecision.Partial, 0.5f));
      rec.Script.Enqueue(FakeRecognizer.Make(SpeakingDecision.Partial, 0.5f));
      rec.Script.Enqueue(FakeRecognizer.Make(SpeakingDecision.WrongWord, 0.1f));
      // Rounds 2..8: perfect.
      for (int i = 0; i < 7; i++) rec.Script.Enqueue(FakeRecognizer.Make(SpeakingDecision.StrongPass, 1f));
      ActivityLifecycle life = new ActivityLifecycle("speaking_stage", "t");
      SpeakingStage game = BuildGame(builder, arena, player, audio, life, rec, null);
      // Drive the visit: round 1 exhausts three weak attempts, rounds 2..8 pass.
      for (int i = 0; i < 4000 && !game.Completed; i++) game.Tick(0.1f);
      Assert.IsTrue(game.Completed, "visit completes after all rounds");
      Assert.AreEqual(ActivityState.Completed, life.State, "lifecycle completed");
      Assert.IsTrue(SaidAny(audio, "Gần đúng rồi!", "Almost there!"), "partial line spoken");
      Assert.IsTrue(SaidAny(audio, "Thử lại nhé!", "Try again!"), "try-again phrase spoken");
      Assert.IsTrue(SaidAny(audio, "Giỏi lắm!", "Well done!"), "closing line spoken");
      foreach (string line in audio.Lines) {
        bool ok = SafetyFilter.ValidateLine(line, false, out string why);
        Assert.IsTrue(ok, "line '" + line + "' " + why);
      }
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S22J_EnvironmentFailuresNeverFailTheChild() {
    GameObject arena;
    SpeakingStageBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(SpeakingStageBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      var rec = new FakeRecognizer();
      rec.Script.Enqueue(FakeRecognizer.Env());
      rec.Script.Enqueue(FakeRecognizer.Env());
      ActivityLifecycle life = new ActivityLifecycle("speaking_stage", "t");
      SpeakingStage game = BuildGame(builder, arena, player, audio, life, rec, null);
      for (int i = 0; i < 400 && !(game.Current == SpeakingStage.Phase.Done && game.EnvErrors >= 2); i++)
        game.Tick(0.1f);
      for (int i = 0; i < 30; i++) game.Tick(0.05f); // let the paced voice submit
      Assert.AreEqual(2, game.EnvErrors, "two environment failures");
      Assert.IsFalse(game.Completed, "environment failure never completes the visit");
      Assert.AreNotEqual(ActivityState.Completed, life.State, "lifecycle stays retryable");
      Assert.IsTrue(SaidAny(audio, "Mình quay lại sau nhé!", "Let's come back later!"), "kind stop spoken");
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S22M_SpokenTargetAlias() {
    GameObject arena;
    SpeakingStageBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(SpeakingStageBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      var rec = new FakeRecognizer();
      rec.Script.Enqueue(FakeRecognizer.Make(SpeakingDecision.StrongPass, 1f));
      SpeakingStage game = BuildGame(builder, arena, player, audio,
        new ActivityLifecycle("speaking_stage", "t"), rec, null);
      game.SetLevelForTests(2, 3); // LV2 round 3 = "grape"
      Assert.AreEqual("grape", game.TargetWord, "vocab id is grape");
      Assert.AreEqual("grapes", game.SpokenTarget, "spoken target is grapes");
      PumpTo(game, SpeakingStage.Phase.Listening, 60);
      Assert.AreEqual(1, rec.Targets.Count, "one attempt");
      Assert.AreEqual("grapes", rec.Targets[0],
        "the attempt is graded against the SPOKEN word (else grape can never pass)");
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  [Test] public void S22K_ReentryNoDuplicate() {
    GameObject arena;
    SpeakingStageBuilder builder = BuildArena(out arena);
    GameObject player = PlayerAt(SpeakingStageBuilder.PlaySpotLocal);
    try {
      FakeAudio audio = new FakeAudio();
      var rec = new FakeRecognizer();
      ActivityLifecycle life = new ActivityLifecycle("speaking_stage", "t");
      SpeakingStage a = BuildGame(builder, arena, player, audio, life, rec, null);
      SpeakingStage b = BuildGame(builder, arena, player, audio, life, rec, null);
      Assert.AreSame(a, b, "same component");
      Assert.AreEqual(1, arena.GetComponents<SpeakingStage>().Length, "no duplicate listeners");
    } finally {
      UnityEngine.Object.DestroyImmediate(player);
      UnityEngine.Object.DestroyImmediate(arena);
    }
  }

  // ---- scene pin ----------------------------------------------------------------

  [Test] public void S22L_ScenePin() {
    string scenePath = Path.Combine(Application.dataPath,
      "A_World", "SpeakingStage", "SpeakingStagePlayScene.unity");
    Assert.IsTrue(File.Exists(scenePath), "scene file exists");
    string yaml = File.ReadAllText(scenePath);
    Assert.IsTrue(yaml.Contains("SpeakingStageWorld"), "scene root named");
    bool inBuild = false;
    foreach (UnityEditor.EditorBuildSettingsScene s in UnityEditor.EditorBuildSettings.scenes) {
      if (s.enabled && s.path.Replace('\\', '/').EndsWith("SpeakingStagePlayScene.unity")) {
        inBuild = true;
        break;
      }
    }
    Assert.IsTrue(inBuild, "scene registered in Build Settings");
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
