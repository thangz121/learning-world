// A_World/GameAudioTap.cs — Agent A (World & Visual).
// Phase 2.5: game-audio source for recordings. Sits on the SAME GameObject as
// the AudioListener (MarketBuilder attaches it next to AddComponent<AudioListener>;
// Unity only calls OnAudioFilterRead there) and copies the final mixed output
// (Milo/Mia TTS dialogue, SFX — everything the player hears) into a bounded
// FIFO as canonical mono PCM16LE @16 kHz. The recorder pops windows from the
// FIFO and sums them with each mic chunk (RecAudioMixer), so exports carry
// voice AND gameplay — the pre-2.5 file was voice-only (player report).
//
// NEVER modifies the filter buffer (read-only copy); never touches Unity APIs
// on the audio thread (rate cached on the main thread); zero work unless the
// recorder arms CaptureEnabled (a session flag, never auto-on).
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameAudioTap : MonoBehaviour {
  public const int FifoCapBytes = 128 * 1024; // ~4 s @16k mono16 (jitter + chunk skew)

  volatile bool _captureEnabled;
  int _cachedRate;
  readonly object _mutex = new object();
  readonly Queue<byte[]> _chunks = new Queue<byte[]>();
  int _headOffset; // consumed bytes inside the head chunk (partial pops)
  int _bytes;

  public bool CaptureEnabled {
    get { return _captureEnabled; }
    set { _captureEnabled = value; }
  }

  void Awake() {
    CacheRate();
  }

  void Update() {
    CacheRate();
  }

  void CacheRate() {
    try {
      int r = AudioSettings.outputSampleRate;
      if (r >= 8000 && r <= 192000) _cachedRate = r;
      else if (_cachedRate <= 0) _cachedRate = 48000;
    } catch (Exception) {
      if (_cachedRate <= 0) _cachedRate = 48000;
    }
  }

  // Audio thread: copy only, never mutate `data`, never call Unity APIs.
  void OnAudioFilterRead(float[] data, int channels) {
    try {
      if (!_captureEnabled) return;
      if (data == null || data.Length == 0) return;
      int rate = _cachedRate > 0 ? _cachedRate : 48000;
      byte[] pcm;
      try { pcm = AudioChunkConverter.ToMono16(data, channels, rate, 16000); }
      catch (Exception) { return; }
      if (pcm == null || pcm.Length == 0) return;
      lock (_mutex) {
        _chunks.Enqueue(pcm);
        _bytes += pcm.Length;
        while (_bytes > FifoCapBytes && _chunks.Count > 0) {
          byte[] old = _chunks.Dequeue();
          _bytes -= old != null ? old.Length : 0;
        }
        if (_bytes < 0) _bytes = 0;
      }
    } catch (Exception) { }
  }

  // Pops exactly `count` bytes (zero-padded when the game was quieter than
  // the mic chunk — silence, never garbage). Returns the filled array, or an
  // all-zero array when nothing was ever captured.
  public byte[] TakeBytes(int count) {
    var out_ = new byte[Math.Max(0, count)];
    if (count <= 0) return out_;
    try {
      lock (_mutex) {
        int pos = 0;
        while (pos < count && _chunks.Count > 0) {
          byte[] head = _chunks.Peek();
          if (head == null || head.Length == 0) { _chunks.Dequeue(); _headOffset = 0; continue; }
          if (_headOffset >= head.Length) { _chunks.Dequeue(); _headOffset = 0; continue; }
          int take = Math.Min(head.Length - _headOffset, count - pos);
          Buffer.BlockCopy(head, _headOffset, out_, pos, take);
          pos += take;
          _headOffset += take;
          _bytes -= take;
          if (_headOffset >= head.Length) { _chunks.Dequeue(); _headOffset = 0; }
        }
        if (_bytes < 0) _bytes = 0;
      }
    } catch (Exception) { }
    return out_;
  }

  public void Clear() {
    try {
      lock (_mutex) {
        _chunks.Clear();
        _headOffset = 0;
        _bytes = 0;
      }
    } catch (Exception) { }
  }

  public int BufferedBytes {
    get { try { lock (_mutex) { return _bytes; } } catch (Exception) { return 0; } }
  }
}
