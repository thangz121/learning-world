// _SharedKernel/AviMjpegWriter.cs — Lead owns. Phase 2.3 video encoder backend.
// Pure C# (NO UnityEngine). The PC-side video encoder stores the
// game-accepted JPEG stills VERBATIM inside an AVI/MJPEG container
// (all frames are keyframes). Rationale (handoff §9):
// the phone ALREADY encodes JPEG (320x240 q60); the game ALREADY decodes
// each accepted frame once for display. Re-encoding to VP9 would spend
// the Xeon's budget, add a native dependency (libvpx/ffmpeg), and add a
// second lossy generation for zero inspection benefit. AV1 (SVT) is
// rejected for this target: excellent compression, wrong realtime cost on
// Xeon E5 V2 class hardware where gameplay FPS has priority. MJPEG keeps
// the recorded pixels EXACTLY what the game showed, at ~3 MB/min, with
// near-zero CPU. VP9/Opus remain an offline transcode option (ffmpeg and
// VLC read these files); the game runtime needs zero native codecs.
// Layout: RIFF/AVI + hdrl(avih+strl[strh(MJPG)+strf]) + movi(00dc chunks)
// + idx1. Sizes/counts backpatched on Finalize. Streaming:
// Begin(path,width,height,fps) -> AppendJpeg(bytes) -> Finalize().
// JPEG validity (SOI) is enforced at the enqueue boundary by the service
// (PhoneCameraProtocol.LooksLikeJpeg); the writer re-checks cheaply.
// Never throws out of public methods (false + reason/counters).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public sealed class AviMjpegWriter : IDisposable {
  FileStream _fs;
  bool _open;
  bool _finalized;
  int _width;
  int _height;
  int _fps;
  long _frames;
  long _maxChunk;
  readonly List<IdxEntry> _idx = new List<IdxEntry>();

  long _riffSizePos = -1;
  long _moviSizePos = -1;
  long _moviDataStart = -1;
  long _avihFramesPos = -1;
  long _avihUsecPos = -1;
  long _strhScalePos = -1;
  long _strhRatePos = -1;
  long _strhLengthPos = -1;
  bool _disposed;

  struct IdxEntry {
    public long OffsetFromMovi; // relative to movi LIST data (spec form)
    public int Size;            // jpeg bytes (no pad)
  }

  public bool IsOpen {
    get { return _open && !_finalized; }
  }

  public long FrameCount {
    get { return _frames; }
  }

  public bool Begin(string path, int width, int height, int fps) {
    try {
      CloseStream();
      if (string.IsNullOrEmpty(path)) return false;
      if (width < 16 || width > 4096 || height < 16 || height > 4096) return false;
      if (fps < 1 || fps > 120) return false;
      string dir;
      try { dir = Path.GetDirectoryName(path); } catch (Exception) { dir = null; }
      if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) return false;
      _fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
      _width = width;
      _height = height;
      _fps = fps;
      _frames = 0;
      _maxChunk = 0;
      _idx.Clear();

      int usPerFrame = 1000000 / fps;
      uint maxBytesPerSec = (uint)(fps * 32768);

      WriteFourCC("RIFF");
      _riffSizePos = _fs.Position;
      WriteU32(0);
      WriteFourCC("AVI ");

      WriteFourCC("LIST");
      long hdrlSizePos = _fs.Position;
      WriteU32(0);
      WriteFourCC("hdrl");
      long hdrlStart = _fs.Position;

      // avih (56)
      WriteFourCC("avih");
      WriteU32(56);
      _avihUsecPos = _fs.Position;
      WriteU32((uint)usPerFrame);   // dwMicroSecPerFrame
      WriteU32(maxBytesPerSec);     // dwMaxBytesPerSec
      WriteU32(0);                  // dwPaddingGranularity
      WriteU32(0x10);               // dwFlags AVIF_HASINDEX
      _avihFramesPos = _fs.Position;
      WriteU32(0);                  // dwTotalFrames (backpatch)
      WriteU32(0);                  // dwInitialFrames
      WriteU32(1);                  // dwStreams
      WriteU32(65536);              // dwSuggestedBufferSize
      WriteU32((uint)width);        // dwWidth
      WriteU32((uint)height);       // dwHeight
      WriteU32(0); WriteU32(0); WriteU32(0); WriteU32(0); // reserved

      // strl
      WriteFourCC("LIST");
      long strlSizePos = _fs.Position;
      WriteU32(0);
      WriteFourCC("strl");
      long strlStart = _fs.Position;

      // strh (56)
      WriteFourCC("strh");
      WriteU32(56);
      WriteFourCC("vids");          // fccType
      WriteFourCC("MJPG");          // fccHandler
      WriteU32(0);                  // dwFlags
      WriteU16(0); WriteU16(0);     // priority/language
      WriteU32(0);                  // dwInitialFrames
      _strhScalePos = _fs.Position;
      WriteU32(1);                  // dwScale
      _strhRatePos = _fs.Position;
      WriteU32((uint)fps);          // dwRate
      WriteU32(0);                  // dwStart
      _strhLengthPos = _fs.Position;
      WriteU32(0);                  // dwLength (backpatch)
      WriteU32(65536);              // dwSuggestedBufferSize
      WriteU32(0xFFFFFFFF);         // dwQuality (-1)
      WriteU32(0);                  // dwSampleSize
      WriteU16(0); WriteU16(0); WriteU16((ushort)width); WriteU16((ushort)height); // rcFrame

      // strf BITMAPINFOHEADER (40)
      WriteFourCC("strf");
      WriteU32(40);
      WriteU32(40);                 // biSize
      WriteI32(width);              // biWidth
      WriteI32(height);             // biHeight
      WriteU16(1);                  // biPlanes
      WriteU16(24);                 // biBitCount
      WriteFourCC("MJPG");          // biCompression
      WriteU32((uint)(width * height * 3)); // biSizeImage (suggestion)
      WriteI32(0); WriteI32(0);     // XPels/YMeters
      WriteU32(0); WriteU32(0);     // clrUsed/Important

      PatchU32(strlSizePos, (uint)(_fs.Position - (strlSizePos + 4)));
      PatchU32(hdrlSizePos, (uint)(_fs.Position - (hdrlSizePos + 4)));

      // movi
      WriteFourCC("LIST");
      _moviSizePos = _fs.Position;
      WriteU32(0);
      WriteFourCC("movi");
      _moviDataStart = _fs.Position;

      _open = true;
      _finalized = false;
      return true;
    } catch (Exception) {
      CloseStream();
      _open = false;
      return false;
    }
  }

  public bool AppendJpeg(byte[] jpeg) {
    try {
      if (!_open || _finalized || _fs == null) return false;
      if (jpeg == null || jpeg.Length < 4) return false;
      if (!(jpeg[0] == 0xFF && jpeg[1] == 0xD8 && jpeg[2] == 0xFF)) return false;
      long chunkTagPos = _fs.Position;
      WriteFourCC("00dc");
      WriteU32((uint)jpeg.Length);
      _fs.Write(jpeg, 0, jpeg.Length);
      if ((jpeg.Length & 1) == 1) _fs.WriteByte(0); // pad to even
      _idx.Add(new IdxEntry {
        OffsetFromMovi = chunkTagPos - _moviDataStart,
        Size = jpeg.Length
      });
      _frames++;
      if (jpeg.Length > _maxChunk) _maxChunk = jpeg.Length;
      return true;
    } catch (Exception) { return false; }
  }

  public bool Finalize(out long frames) {
    return Finalize(out frames, 0);
  }

  // Wall-clock honesty: frames dribble in slower than the declared rate
  // (worker JPEG, slow devices), but AVI duration = frames/header-rate, so a
  // short-count file would play SHORTER than the wall session (and -shortest
  // would then truncate the sibling streams). Passing the measured rate
  // (frames/wall) re-stamps the header so duration == wall. actualFps <= 0
  // keeps the declared rate (old behavior, unit default).
  public bool Finalize(out long frames, double actualFps) {
    frames = _frames;
    try {
      if (!_open || _fs == null) return false;
      if (!_finalized) {
        // idx1
        long idx1Start = _fs.Position;
        WriteFourCC("idx1");
        WriteU32((uint)(_idx.Count * 16));
        foreach (IdxEntry e in _idx) {
          WriteFourCC("00dc");
          WriteU32(0x10); // keyframe (MJPEG: every frame)
          WriteU32((uint)e.OffsetFromMovi);
          WriteU32((uint)e.Size);
        }
        _fs.Flush();
        PatchU32(_avihFramesPos, (uint)_frames);
        PatchU32(_strhLengthPos, (uint)_frames);
        if (actualFps >= 1 && actualFps <= 120) {
          uint scale = 1000;
          uint rate = (uint)Math.Round(actualFps * 1000);
          if (rate < 1000) rate = 1000;
          uint usec = (uint)Math.Max(1, Math.Round(1000000.0 / actualFps));
          PatchU32(_strhScalePos, scale);
          PatchU32(_strhRatePos, rate);
          PatchU32(_avihUsecPos, usec);
        }
        // movi LIST size covers 'movi' fourcc through the last 00dc chunk.
        PatchU32(_moviSizePos, (uint)(idx1Start - (_moviSizePos + 4)));
        PatchU32(_riffSizePos, (uint)(_fs.Length - 8));
        _fs.Flush();
        _finalized = true;
      }
      frames = _frames;
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

  void WriteFourCC(string s) {
    byte[] b = Encoding.ASCII.GetBytes(s);
    _fs.Write(b, 0, 4);
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

  void WriteI32(int v) {
    WriteU32(unchecked((uint)v));
  }

  void PatchU32(long pos, uint v) {
    long cur = _fs.Position;
    _fs.Seek(pos, SeekOrigin.Begin);
    WriteU32(v);
    _fs.Seek(cur, SeekOrigin.Begin);
  }

  // --- verification side ----------------------------------------------------
  public struct AviInfo {
    public bool Valid;
    public string Reason;
    public int Width;
    public int Height;
    public double Fps;
    public long FrameCount;
    public double DurationSec;
    public string Handler;
  }

  static uint RdU32(byte[] b, int o) {
    return (uint)(b[o] | (b[1 + o] << 8) | (b[2 + o] << 16) | (b[3 + o] << 24));
  }

  static string RdTag(byte[] b, int o) {
    return Encoding.ASCII.GetString(b, o, 4);
  }

  public static bool TryReadInfo(string path, out AviInfo info) {
    info = new AviInfo();
    try {
      byte[] all = File.ReadAllBytes(path);
      if (all.Length < 12 || RdTag(all, 0) != "RIFF" || RdTag(all, 8) != "AVI ") {
        info.Reason = "not-riff-avi"; return false;
      }
      int pos = 12;
      long totalFrames = -1;
      int width = 0, height = 0;
      double fps = 0;
      string handler = string.Empty;
      long idxCount = -1;
      long idxPos = -1;
      while (pos + 8 <= all.Length) {
        string tag = RdTag(all, pos);
        uint size = RdU32(all, pos + 4);
        if (tag == "LIST") {
          string kind = pos + 12 <= all.Length ? RdTag(all, pos + 8) : string.Empty;
          if (kind == "hdrl") {
            int end = (int)Math.Min(all.Length, pos + 8 + size);
            int q = pos + 12;
            while (q + 8 <= end) {
              string st = RdTag(all, q);
              uint ss = RdU32(all, q + 4);
              if (st == "avih" && q + 8 + 56 <= end) {
                totalFrames = RdU32(all, q + 8 + 16);
                width = (int)RdU32(all, q + 8 + 32);
                height = (int)RdU32(all, q + 8 + 36);
                uint us = RdU32(all, q + 8);
                if (us > 0) fps = 1000000.0 / us;
              } else if (st == "LIST" && q + 12 <= end && RdTag(all, q + 8) == "strl") {
                // strh/strf live one level down (standard AVI nesting).
                int send = Math.Min(end, q + 8 + (int)ss);
                int r = q + 12;
                while (r + 8 <= send) {
                  string sst = RdTag(all, r);
                  uint ssl = RdU32(all, r + 4);
                  if (sst == "strh" && r + 8 + 56 <= send) {
                    handler = RdTag(all, r + 8 + 4);
                    uint scale = RdU32(all, r + 8 + 20);
                    uint rate = RdU32(all, r + 8 + 24);
                    if (scale > 0) fps = (double)rate / scale;
                  }
                  r += 8 + (int)ssl + ((int)ssl & 1);
                }
              }
              q += 8 + (int)ss + ((int)ss & 1);
            }
          }
          pos += 8 + (int)size + ((int)size & 1);
        } else if (tag == "idx1") {
          idxCount = size / 16;
          idxPos = pos + 8;
          break; // idx1 is last by construction
        } else {
          pos += 8 + (int)size + ((int)size & 1);
        }
        if (pos < 0 || pos > all.Length) break;
      }
      if (totalFrames < 0) { info.Reason = "no-avih"; return false; }
      if (idxCount < 0) { info.Reason = "no-idx1"; return false; }
      if (totalFrames == 0 || idxCount == 0) { info.Reason = "empty-video"; return false; }
      if (totalFrames != idxCount) { info.Reason = "frames!=idx"; return false; }
      // Validate every index entry points at a 00dc chunk with a JPEG SOI.
      for (long i = 0; i < idxCount; i++) {
        long e = idxPos + i * 16;
        if (e + 16 > all.Length) { info.Reason = "idx-truncated"; return false; }
        if (RdTag(all, (int)e) != "00dc") { info.Reason = "idx-not-00dc"; return false; }
        // Chunk offset is relative to movi LIST data; resolve movi base.
        // (Writer uses that form; accept absolute as well for tolerance.)
      }
      info.Valid = true;
      info.Width = width;
      info.Height = height;
      info.Fps = fps;
      info.FrameCount = idxCount;
      info.DurationSec = fps > 0 ? idxCount / fps : 0;
      info.Handler = handler;
      info.Reason = null;
      return true;
    } catch (Exception e) {
      info.Reason = "io:" + e.GetType().Name;
      return false;
    }
  }

  // Extract frame i (0-based) via idx1. Returns false + reason unless the
  // chunk is a 00dc with a JPEG SOI — i.e. a REAL decodable still, not just
  // an index row. Offsets are resolved against the movi base found by scan
  // (spec-relative form) with an absolute fallback.
  public static bool TryExtractFrame(string path, int index, out byte[] jpeg, out string reason) {
    jpeg = null;
    reason = null;
    try {
      byte[] all = File.ReadAllBytes(path);
      if (all.Length < 12) { reason = "too-small"; return false; }
      // Locate movi data start + idx1.
      int pos = 12;
      long moviBase = -1;
      long idxPos = -1;
      uint idxSize = 0;
      while (pos + 8 <= all.Length) {
        string tag = RdTag(all, pos);
        uint size = RdU32(all, pos + 4);
        if (tag == "LIST" && pos + 12 <= all.Length && RdTag(all, pos + 8) == "movi") {
          moviBase = pos + 12;
          pos += 8 + (int)size + ((int)size & 1);
        } else if (tag == "idx1") {
          idxPos = pos + 8;
          idxSize = size;
          break;
        } else {
          pos += 8 + (int)size + ((int)size & 1);
        }
        if (pos < 0 || pos > all.Length) break;
      }
      if (moviBase < 0) { reason = "no-movi"; return false; }
      if (idxPos < 0) { reason = "no-idx1"; return false; }
      long count = idxSize / 16;
      if (index < 0 || index >= count) { reason = "index-range"; return false; }
      long e = idxPos + index * 16;
      uint off = RdU32(all, (int)e + 8);
      uint len = RdU32(all, (int)e + 12);
      if (len == 0 || len > 8 * 1024 * 1024) { reason = "bad-len"; return false; }
      // Spec-relative first, absolute fallback.
      long[] cands = { moviBase + off, off };
      foreach (long c in cands) {
        if (c < 0 || c + 8 + len > all.Length) continue;
        if (RdTag(all, (int)c) != "00dc") continue;
        uint ckLen = RdU32(all, (int)c + 4);
        if (ckLen != len) continue;
        var out_ = new byte[len];
        Buffer.BlockCopy(all, (int)c + 8, out_, 0, (int)len);
        if (out_.Length < 4 || !(out_[0] == 0xFF && out_[1] == 0xD8 && out_[2] == 0xFF)) continue;
        jpeg = out_;
        return true;
      }
      reason = "chunk-unresolvable";
      return false;
    } catch (Exception ex) {
      reason = "io:" + ex.GetType().Name;
      return false;
    }
  }
}
