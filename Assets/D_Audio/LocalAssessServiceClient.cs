// D_Audio/LocalAssessServiceClient.cs — HTTP client for the LOCAL speak-assess
// service (tools/speak_assess_server.py, faster-whisper base.en, free/offline).
// Contract (kept in sync with the python server + CT-S22):
//   GET  /health                       -> {"ok": true, ...}
//   POST /assess?target=<word>  (body = WAV 16k mono PCM16)
//        -> {"transcript","confidence","no_speech","latency_ms","engine"}
// C# 9.0 only.
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public class LocalAssessServiceClient {
  public const int DefaultPort = 8470;

  readonly string _baseUrl;

  public LocalAssessServiceClient(int port) {
    _baseUrl = "http://127.0.0.1:" + (port > 0 ? port : DefaultPort);
  }

  public LocalAssessServiceClient() : this(DefaultPort) { }

  public string BaseUrl { get { return _baseUrl; } }

  [Serializable]
  public struct Reply {
    public string transcript;
    public float confidence;
    public float no_speech;
    public int latency_ms;
    public string engine;
  }

  // ---- pure helpers (EditMode-testable, no UnityWebRequest) --------------------

  // Canonical WAV (RIFF/PCM16LE) for the captured segment.
  public static byte[] EncodeWav(float[] samples, int sampleRate, int channels) {
    if (samples == null) samples = new float[0];
    if (sampleRate <= 0) sampleRate = 16000;
    if (channels <= 0) channels = 1;
    int dataLen = samples.Length * 2;
    byte[] wav = new byte[44 + dataLen];
    int byteRate = sampleRate * channels * 2;
    WriteAscii(wav, 0, "RIFF");
    WriteInt32(wav, 4, 36 + dataLen);
    WriteAscii(wav, 8, "WAVE");
    WriteAscii(wav, 12, "fmt ");
    WriteInt32(wav, 16, 16);
    WriteInt16(wav, 20, 1);                       // PCM
    WriteInt16(wav, 22, (short)channels);
    WriteInt32(wav, 24, sampleRate);
    WriteInt32(wav, 28, byteRate);
    WriteInt16(wav, 32, (short)(channels * 2));   // block align
    WriteInt16(wav, 34, 16);                      // bits
    WriteAscii(wav, 36, "data");
    WriteInt32(wav, 40, dataLen);
    for (int i = 0; i < samples.Length; i++) {
      float v = samples[i];
      if (v > 1f) v = 1f;
      if (v < -1f) v = -1f;
      short s = (short)Mathf.RoundToInt(v * 32767f);
      wav[44 + i * 2] = (byte)(s & 0xFF);
      wav[44 + i * 2 + 1] = (byte)((s >> 8) & 0xFF);
    }
    return wav;
  }

  // Parse the /assess JSON. Returns false when the payload is not a valid reply.
  public static bool TryParseReply(string json, out Reply reply) {
    reply = default(Reply);
    if (string.IsNullOrEmpty(json)) return false;
    if (json.IndexOf("\"transcript\"", StringComparison.Ordinal) < 0) return false;
    try {
      reply = JsonUtility.FromJson<Reply>(json);
      return reply.transcript != null;
    } catch (Exception) {
      return false;
    }
  }

  static void WriteAscii(byte[] dst, int at, string s) {
    for (int i = 0; i < s.Length; i++) dst[at + i] = (byte)s[i];
  }

  static void WriteInt32(byte[] dst, int at, int v) {
    dst[at] = (byte)(v & 0xFF);
    dst[at + 1] = (byte)((v >> 8) & 0xFF);
    dst[at + 2] = (byte)((v >> 16) & 0xFF);
    dst[at + 3] = (byte)((v >> 24) & 0xFF);
  }

  static void WriteInt16(byte[] dst, int at, short v) {
    dst[at] = (byte)(v & 0xFF);
    dst[at + 1] = (byte)((v >> 8) & 0xFF);
  }

  // ---- live call ---------------------------------------------------------------

  public virtual async Task<Reply> AssessAsync(byte[] wav, string target, CancellationToken ct) {
    string url = _baseUrl + "/assess?target=" + UnityWebRequest.EscapeURL(target ?? "");
    using (var req = new UnityWebRequest(url, "POST")) {
      req.uploadHandler = new UploadHandlerRaw(wav ?? new byte[0]);
      req.downloadHandler = new DownloadHandlerBuffer();
      req.SetRequestHeader("Content-Type", "audio/wav");
      var op = req.SendWebRequest();
      while (!op.isDone) {
        ct.ThrowIfCancellationRequested();
        await Task.Yield();
      }
      if (req.result != UnityWebRequest.Result.Success) {
        throw new Exception("assess service HTTP " + req.responseCode + " " + req.error);
      }
      Reply reply;
      if (!TryParseReply(req.downloadHandler.text, out reply)) {
        throw new Exception("assess service bad payload");
      }
      return reply;
    }
  }
}
