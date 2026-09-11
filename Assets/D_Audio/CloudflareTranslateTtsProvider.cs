// D_Audio/CloudflareTranslateTtsProvider.cs — Agent D (W0-T1).
// Unity -> Cloudflare Worker source TTS (WorkerTtsContract v6.4.1, AUDIO_DESIGN §2/§8):
//   GET <BaseUrl>?text=<url-escaped, <=200 chars>&lang=<pass-through, default en-US>
//       &rate=<normal|slow from Rate>=0.8>
//   -> HTTP 200 audio/mpeg binary. NO auth header. Voice/Pitch/Style/Format are
//   NEVER sent (provider does not support them) but stay in the internal cache key
//   so a future provider swap does not collide (AUDIO_DESIGN §3: profiles are an
//   abstraction today, real voice switching comes with the future provider).
// Chunking: text >200 chars is split at sentence/space boundary; each chunk is
// fetched with its own DownloadHandlerBuffer and the byte arrays are concatenated.
// LIMITATION (documented, by design): byte-concat of mp3 frames is NOT gapless and
// may click at boundaries. Runtime-dynamic lines only; masters ship via pre-gen
// (tools/pregen_audio.py) + W1 Addressables hook. Errors throw TtsException so the
// AudioDirector can fall back upstream (pre-gen/offline intent, quest never blocks).
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public sealed class TtsException : Exception {
  public int HttpStatus { get; private set; }

  public TtsException(string message, int httpStatus = 0, Exception inner = null)
    : base(message, inner) {
    HttpStatus = httpStatus;
  }
}

public class CloudflareTranslateTtsProvider : ISpeechSynthesisProvider {
  public const string BaseUrl = "https://round-mud-63dd.hoaithuong1995cdmna.workers.dev/";
  public const int MaxCharsPerRequest = 200;
  public const string DefaultLang = "en-US";
  public const int RequestTimeoutSec = 20;

  readonly string _baseUrl;

  public CloudflareTranslateTtsProvider(string baseUrlOverride = null) {
    _baseUrl = string.IsNullOrEmpty(baseUrlOverride) ? BaseUrl : baseUrlOverride;
  }

  // Internal float rate -> worker hint (WorkerTtsContract mapping freeze).
  public static string MapRate(float rate) {
    return rate >= 0.8f ? "normal" : "slow";
  }

  public static string BuildRequestUrl(string baseUrl, string text, string lang, string rate) {
    string root = (baseUrl ?? BaseUrl).TrimEnd('/');
    return root + "/?text=" + UnityWebRequest.EscapeURL(text ?? "")
      + "&lang=" + UnityWebRequest.EscapeURL(string.IsNullOrEmpty(lang) ? DefaultLang : lang)
      + "&rate=" + UnityWebRequest.EscapeURL(rate ?? "normal");
  }

  // Split long text at sentence boundary first, space boundary second, hard cut last.
  // Never returns empty chunks; returns empty list only for blank input.
  public static List<string> SplitForWorker(string text, int maxChars = MaxCharsPerRequest) {
    var chunks = new List<string>();
    string t = (text ?? "").Trim();
    if (t.Length == 0) return chunks;
    if (t.Length <= maxChars) {
      chunks.Add(t);
      return chunks;
    }
    int i = 0;
    while (i < t.Length) {
      int end = Math.Min(i + maxChars, t.Length);
      if (end == t.Length) {
        string tail = t.Substring(i).Trim();
        if (tail.Length > 0) chunks.Add(tail);
        break;
      }
      int cut = -1;
      for (int j = end - 1; j >= i; j--) {
        char c = t[j];
        if ((c == '.' || c == '!' || c == '?' || c == ';')
            && (j + 1 >= t.Length || char.IsWhiteSpace(t[j + 1]))) {
          cut = j + 1;
          break; // scanning downward: first hit is the LAST boundary in window
        }
      }
      if (cut <= i) {
        int sp = t.LastIndexOf(' ', end - 1, end - i);
        cut = sp > i ? sp : end;
      }
      string chunk = t.Substring(i, cut - i).Trim();
      if (chunk.Length > 0) chunks.Add(chunk);
      i = cut;
      while (i < t.Length && char.IsWhiteSpace(t[i])) i++;
    }
    return chunks;
  }

  public async Task<TtsAudioResult> SynthesizeAsync(TtsRequest request, CancellationToken ct) {
    string text = request.Text ?? "";
    if (string.IsNullOrWhiteSpace(text)) {
      // Client-side 400-class: re-chunking cannot help, never retry blindly.
      throw new TtsException("empty text (client-side, no retry)", 400);
    }
    string lang = string.IsNullOrEmpty(request.Lang.Value) ? DefaultLang : request.Lang.Value;
    string rate = MapRate(request.Rate);

    List<string> chunks = SplitForWorker(text);
    if (chunks.Count == 0) throw new TtsException("empty text after chunking", 400);

    var parts = new List<byte[]>(chunks.Count);
    foreach (string chunk in chunks) {
      ct.ThrowIfCancellationRequested();
      string url = BuildRequestUrl(_baseUrl, chunk, lang, rate);
      parts.Add(await FetchChunkAsync(url, ct));
    }

    int total = 0;
    foreach (byte[] p in parts) total += p.Length;
    byte[] combined = new byte[total];
    int offset = 0;
    foreach (byte[] p in parts) {
      Buffer.BlockCopy(p, 0, combined, offset, p.Length);
      offset += p.Length;
    }

    string key = AudioCache.CacheKey(
      request.Text, request.Voice, request.Lang, request.Rate,
      request.Pitch, request.Style, request.Format);
    return new TtsAudioResult { Mp3 = combined, CacheKey = key, FromCache = false };
  }

  async Task<byte[]> FetchChunkAsync(string url, CancellationToken ct) {
    using (UnityWebRequest req = UnityWebRequest.Get(url)) {
      req.downloadHandler = new DownloadHandlerBuffer();
      req.timeout = RequestTimeoutSec;
      // Deliberately NO auth header and NO voice/pitch/style params (contract freeze).
      var op = req.SendWebRequest();
      while (!op.isDone) {
        if (ct.IsCancellationRequested) {
          try { req.Abort(); } catch (Exception) { }
          ct.ThrowIfCancellationRequested();
        }
        await Task.Yield();
      }
      ct.ThrowIfCancellationRequested();

      if (req.result != UnityWebRequest.Result.Success) {
        throw new TtsException("worker request failed: " + req.error, (int)req.responseCode);
      }
      if (req.responseCode != 200) {
        throw new TtsException("worker status " + (int)req.responseCode, (int)req.responseCode);
      }
      string contentType = req.GetResponseHeader("Content-Type") ?? "";
      if (contentType.IndexOf("audio/mpeg", StringComparison.OrdinalIgnoreCase) < 0) {
        throw new TtsException(
          "unexpected content-type '" + contentType + "' (expected audio/mpeg)",
          (int)req.responseCode);
      }
      byte[] data = req.downloadHandler != null ? req.downloadHandler.data : null;
      if (data == null || data.Length == 0) {
        throw new TtsException("empty audio body", (int)req.responseCode);
      }
      return data;
    }
  }
}
