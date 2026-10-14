// Assets/Editor/TempMaxSpecCalibration.cs — Option-A calibration harness (wp-1.9.41).
// Runs the SAME WAV corpus through the production-default acoustic provider (LocalAcoustic)
// and the option-B MaxSpec provider, scoring both with the frozen SpeakingPassPolicy, and
// reports decision parity + the advisory fusion score. Parity is the B-contract proof;
// the fusion column is the data basis for any future fusion-driven promotion.
// Usage (batchmode):
//   -executeMethod TempMaxSpecCalibration.Run -calib-wav-dir <dir> [-calib-out <csv>] [-calib-max-files N] -quit
using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

public static class TempMaxSpecCalibration {
  public static void Run() {
    string dir = null, outCsv = null;
    int maxFiles = 200;
    string[] args = Environment.GetCommandLineArgs();
    for (int i = 0; i + 1 < args.Length; i++) {
      if (args[i] == "-calib-wav-dir") dir = args[i + 1];
      else if (args[i] == "-calib-out") outCsv = args[i + 1];
      else if (args[i] == "-calib-max-files") int.TryParse(args[i + 1], out maxFiles);
    }
    if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) {
      Debug.LogError("[CALIB] missing/invalid -calib-wav-dir: " + dir);
      return;
    }

    var catalog = new TargetPronunciationCatalog();
    SpeakingPack.Apply((word, phonemes) => catalog.Register(word, phonemes));
    var policy = SpeakingPassPolicy.Default();
    var local = new LocalAcousticProvider(catalog);
    var max = new MaxSpecAdaptiveSpeechProvider(catalog);

    string[] files = Directory.GetFiles(dir, "*.wav");
    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
    var sb = new StringBuilder();
    sb.AppendLine("file,word,base_decision,base_score,base_evidence,max_decision,max_score,max_evidence,fusion_score,parity");
    int total = 0, agree = 0;
    for (int i = 0; i < files.Length && i < maxFiles; i++) {
      string f = files[i];
      string word = WordFromFileName(Path.GetFileNameWithoutExtension(f));
      float[] samples;
      try {
        samples = ScriptedWavCapture.ReadWav16k(f);
      } catch (Exception e) {
        Debug.LogWarning("[CALIB] skip " + f + ": " + e.Message);
        continue;
      }
      if (samples == null || samples.Length == 0) continue;

      var cap = new CapturedSpeech {
        Samples = samples,
        SampleRate = 16000,
        Channels = 1,
        DurationSec = samples.Length / 16000f,
        MeanEnergy = VoiceActivity.MeanAbsolute(samples),
        VoicedSec = samples.Length / 16000f,
        Cancelled = false,
        Error = ""
      };
      var wid = new WordId(word);
      var rA = local.RecognizeAsync(cap, wid, CancellationToken.None).Result;
      var aA = policy.Decide(rA, wid, "calib-a-" + i);
      var rB = max.RecognizeAsync(cap, wid, CancellationToken.None).Result;
      var aB = policy.Decide(rB, wid, "calib-b-" + i);
      float fusion = rB.Pronunciation.HasFusionPayload
        ? rB.Pronunciation.FusionPayload.scores_summary.final_fusion_score
        : float.NaN;
      bool parity = aA.Decision == aB.Decision;
      total++;
      if (parity) agree++;

      sb.AppendFormat("{0},{1},{2},{3:F3},{4},{5},{6:F3},{7},{8:F1},{9}\n",
        Path.GetFileName(f), word,
        aA.Decision, aA.OverallScore, Escape(aA.Evidence),
        aB.Decision, aB.OverallScore, Escape(aB.Evidence),
        fusion, parity ? "OK" : "DIFF");
      Debug.Log("[CALIB] " + Path.GetFileName(f) + " word=" + word
        + " base=" + aA.Decision + "(" + aA.OverallScore.ToString("F3") + ")"
        + " max=" + aB.Decision + "(" + aB.OverallScore.ToString("F3") + ")"
        + " fusion=" + fusion.ToString("F1") + (parity ? " OK" : " DIFF"));
    }
    Debug.Log("[CALIB] SUMMARY files=" + total + " parity=" + agree + "/" + total);

    if (!string.IsNullOrEmpty(outCsv)) {
      try {
        File.WriteAllText(outCsv, sb.ToString());
        Debug.Log("[CALIB] csv -> " + outCsv);
      } catch (Exception e) {
        Debug.LogError("[CALIB] csv write failed: " + e.Message);
      }
    }
  }

  static string WordFromFileName(string name) {
    string n = (name ?? "").ToLowerInvariant();
    if (n.StartsWith("pregen_")) n = n.Substring(7);
    if (n.EndsWith("_normal")) n = n.Substring(0, n.Length - 7);
    else if (n.EndsWith("_slow")) n = n.Substring(0, n.Length - 5);
    return n;
  }

  static string Escape(string s) {
    return (s ?? "").Replace(",", ";");
  }
}
