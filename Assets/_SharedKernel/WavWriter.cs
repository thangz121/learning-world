// _SharedKernel/WavWriter.cs — Lead owns. Phase 2.3 audio encoder backend.
// Pure C# (NO UnityEngine). The PC-side audio encoder is deliberately the
// simplest established container that preserves the game-accepted canonical
// format bit-exact: WAV / PCM16 mono @ 16 kHz. Rationale (handoff §6):
// the phone ALREADY delivers canonical PCM16 (gateway rejects anything
// else); re-encoding to Opus would spend CPU on the Xeon, add a native
// dependency, and lose nothing audible for inspection. Opus/Vorbis remain
// an offline transcode option (ffmpeg reads these files); the game runtime
// needs zero native codecs and zero extra threads beyond the one file pump.
// Layout: RIFF/WAVE + fmt(16 PCM) + data, sizes backpatched on Finalize.
// Streaming: Begin(path) -> AppendPcm16(bytes[, offset,count]) -> Finalize()
// -> VerifyBasics(). Never throws out of public methods (false + reason).
using System;
using System.IO;

public sealed class WavWriter : IDisposable {
  public const int SampleRate = 16000;
  public const int Channels = 1;
  public const int BitsPerSample = 16;
  public const int BytesPerSecond = SampleRate * Channels * BitsPerSample / 8;

  FileStream _fs;
  bool _open;
  bool _finalized;
  long _dataBytes;
  long _dataSizePos = -1;
  long _riffSizePos = -1;
  bool _disposed;

  public bool IsOpen {
    get { return _open && !_finalized; }
  }

  public long DataBytes {
    get { return _dataBytes; }
  }

  public bool Begin(string path) {
    try {
      CloseStream();
      if (string.IsNullOrEmpty(path)) return false;
      string dir;
      try { dir = Path.GetDirectoryName(path); } catch (Exception) { dir = null; }
      if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) return false;
      _fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
      WriteAscii("RIFF");
      _riffSizePos = _fs.Position;
      WriteU32(0); // backpatched
      WriteAscii("WAVE");
      WriteAscii("fmt ");
      WriteU32(16);
      WriteU16(1); // PCM
      WriteU16((ushort)Channels);
      WriteU32((uint)SampleRate);
      WriteU32((uint)BytesPerSecond);
      WriteU16((ushort)(Channels * BitsPerSample / 8));
      WriteU16((ushort)BitsPerSample);
      WriteAscii("data");
      _dataSizePos = _fs.Position;
      WriteU32(0); // backpatched
      _dataBytes = 0;
      _open = true;
      _finalized = false;
      return true;
    } catch (Exception) {
      CloseStream();
      _open = false;
      return false;
    }
  }

  // PCM16LE bytes (even length; a trailing odd byte is carried as silence
  // padding by the caller contract — here we truncate it and count it).
  public bool AppendPcm16(byte[] bytes, int offset, int count, out int appended) {
    appended = 0;
    try {
      if (!_open || _finalized || _fs == null) return false;
      if (bytes == null || count <= 0) return true;
      int avail = Math.Min(count, bytes.Length - offset);
      if (avail <= 0) return true;
      avail &= ~1; // even
      if (avail <= 0) return true;
      _fs.Write(bytes, offset, avail);
      _dataBytes += avail;
      appended = avail;
      return true;
    } catch (Exception) { return false; }
  }

  public bool AppendPcm16(byte[] bytes) {
    int n;
    return AppendPcm16(bytes, 0, bytes != null ? bytes.Length : 0, out n);
  }

  public bool Finalize(out long dataBytes) {
    dataBytes = _dataBytes;
    try {
      if (!_open || _fs == null) return false;
      if (!_finalized) {
        _fs.Flush();
        PatchU32(_riffSizePos, (uint)(36 + _dataBytes));
        PatchU32(_dataSizePos, (uint)_dataBytes);
        _fs.Flush();
        _finalized = true;
      }
      dataBytes = _dataBytes;
      return true;
    } catch (Exception) { return false; }
  }

  public void Close() {
    try {
      if (_open && !_finalized) {
        long n;
        Finalize(out n);
      }
    } catch (Exception) { }
    CloseStream();
  }

  public void Dispose() {
    if (_disposed) return;
    _disposed = true;
    Close();
  }

  void CloseStream() {
    try { if (_fs != null) _fs.Dispose(); } catch (Exception) { }
    _fs = null;
    _open = false;
  }

  void WriteAscii(string s) {
    byte[] b = System.Text.Encoding.ASCII.GetBytes(s);
    _fs.Write(b, 0, b.Length);
  }

  void WriteU16(ushort v) {
    _fs.WriteByte((byte)(v & 0xFF));
    _fs.WriteByte((byte)((v >> 8) & 0xFF));
  }

  void WriteU32(uint v) {
    _fs.WriteByte((byte)(v & 0xFF));
    _fs.WriteByte((byte)((v >> 8) & 0xFF));
    _fs.WriteByte((byte)((v >> 16) & 0xFF));
    _fs.WriteByte((byte)((v >> 24) & 0xFF));
  }

  void PatchU32(long pos, uint v) {
    long cur = _fs.Position;
    _fs.Seek(pos, SeekOrigin.Begin);
    WriteU32(v);
    _fs.Seek(cur, SeekOrigin.Begin);
  }

  // --- verification side (game-side basics + python verifier parity) --------
  public struct WavInfo {
    public bool Valid;
    public string Reason;
    public int SampleRate;
    public int Channels;
    public int BitsPerSample;
    public long DataBytes;
    public long SampleCount;
    public double DurationSec;
  }

  // Opens + parses the header only (no full decode here; the python
  // verifier + EditMode round-trip decode the samples for real).
  public static bool TryReadInfo(string path, out WavInfo info) {
    info = new WavInfo();
    try {
      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
        if (fs.Length < 44) { info.Reason = "too-small"; return false; }
        var h = new byte[44];
        int got = 0;
        while (got < 44) {
          int n = fs.Read(h, got, 44 - got);
          if (n <= 0) break;
          got += n;
        }
        if (got < 44) { info.Reason = "short-header"; return false; }
        if (h[0] != 'R' || h[1] != 'I' || h[2] != 'F' || h[3] != 'F') { info.Reason = "not-riff"; return false; }
        if (h[8] != 'W' || h[9] != 'A' || h[10] != 'V' || h[11] != 'E') { info.Reason = "not-wave"; return false; }
        if (h[12] != 'f' || h[13] != 'm' || h[14] != 't' || h[15] != ' ') { info.Reason = "no-fmt"; return false; }
        int audioFormat = h[20] | (h[21] << 8);
        int channels = h[22] | (h[23] << 8);
        int rate = h[24] | (h[25] << 8) | (h[26] << 16) | (h[27] << 24);
        int bits = h[34] | (h[35] << 8);
        if (audioFormat != 1) { info.Reason = "not-pcm"; return false; }
        // data chunk is expected at 36 for our writer; scan forward if needed.
        long dataPos = 36;
        long dataLen = -1;
        var tag = new byte[8];
        fs.Seek(dataPos, SeekOrigin.Begin);
        for (int i = 0; i < 8; i++) {
          int r = 0;
          while (r < 8) {
            int n = fs.Read(tag, r, 8 - r);
            if (n <= 0) break;
            r += n;
          }
          if (r < 8) break;
          uint ckLen = (uint)(tag[4] | (tag[5] << 8) | (tag[6] << 16) | (tag[7] << 24));
          if (tag[0] == 'd' && tag[1] == 'a' && tag[2] == 't' && tag[3] == 'a') {
            dataLen = ckLen;
            break;
          }
          fs.Seek(ckLen + (ckLen & 1), SeekOrigin.Current);
        }
        if (dataLen < 0) { info.Reason = "no-data"; return false; }
        if (dataLen == 0) { info.Reason = "empty-data"; return false; }
        info.Valid = true;
        info.SampleRate = rate;
        info.Channels = channels;
        info.BitsPerSample = bits;
        info.DataBytes = dataLen;
        info.SampleCount = bits > 0 && channels > 0 ? dataLen * 8L / bits / channels : 0;
        info.DurationSec = rate > 0 ? (double)info.SampleCount / rate : 0;
        info.Reason = null;
        return true;
      }
    } catch (Exception e) {
      info.Reason = "io:" + e.GetType().Name;
      return false;
    }
  }
}
