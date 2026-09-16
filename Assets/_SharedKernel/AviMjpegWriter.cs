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
  bool _raw; // true = BI_RGB raw RGBA chunks (lossless gameplay path); false = MJPEG
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
    return BeginInner(path, width, height, fps, false);
  }

  // Lossless gameplay path (forensic finding: even q90 JPEG imprinted
  // chroma speckle on flats while x264@CRF12 was transparent, so the lossy
  // intermediate had to go). Identical container, BI_RGB raw RGBA chunks.
  public bool BeginRaw(string path, int width, int height, int fps) {
    return BeginInner(path, width, height, fps, true);
  }

  bool BeginInner(string path, int width, int height, int fps, bool raw) {
    try {
      CloseStream();
      if (string.IsNullOrEmpty(path)) return false;
      if (width < 16 || width > 4096 || height < 16 || height > 4096) return false;
      if (fps < 1 || fps > 120) return false;
      string dir;
      try { dir = Path.GetDirectoryName(path); } catch (Exception) { dir = null; }
      if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) return false;
      _fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
      _raw = raw;
      _width = width;
      _height = height;
      _fps = fps;
      _frames = 0;
      _maxChunk = 0;
      _idx.Clear();

      int usPerFrame = 1000000 / fps;
      // Suggestion fields only (never parsed for decode), but keep them
      // honest: raw 1080p20 is ~165 MB/s transient, not the 640 kB/s MJPEG
      // hint. Clamped to uint range.
      uint maxBytesPerSec = (uint)(fps * 32768);
      uint suggestedBuf = 65536;
      if (raw) {
        try {
          long rawRate = (long)width * height * 4 * fps;
          if (rawRate > 0) maxBytesPerSec = rawRate > uint.MaxValue ? uint.MaxValue : (uint)rawRate;
          long rawFrame = (long)width * height * 4;
          if (rawFrame > 0) suggestedBuf = rawFrame > uint.MaxValue ? uint.MaxValue : (uint)rawFrame;
        } catch (Exception) { }
      }

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
      WriteU32(suggestedBuf);       // dwSuggestedBufferSize
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
      WriteFourCC(raw ? "DIB " : "MJPG"); // fccHandler
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
      WriteU32(suggestedBuf);       // dwSuggestedBufferSize
      WriteU32(0xFFFFFFFF);         // dwQuality (-1)
      WriteU32(0);                  // dwSampleSize
      WriteU16(0); WriteU16(0); WriteU16((ushort)width); WriteU16((ushort)height); // rcFrame

      // strf BITMAPINFOHEADER (40 bytes of data: chunk size 40 + biSize 40).
      // The double-40 is NOT a duplication: first is the chunk length, second
      // is the BITMAPINFOHEADER.biSize field. Dropping either corrupts every
      // offset downstream (ffmpeg: "Something went wrong during header
      // parsing", P2X transcode exit-22 — caught live 2026-09-16).
      WriteFourCC("strf");
      WriteU32(40);                 // chunk size
      WriteU32(40);                 // biSize
      WriteI32(width);              // biWidth
      // Raw top-down: AsyncGPUReadback RGBA32 row 0 is the TOP row (the old
      // JPEG path encoded it top-down and every shipped mp4 was upright), so
      // BI_RGB declares negative height. Positive height would flip the mp4
      // vertically. MJPEG keeps the legacy positive value (JPEG is
      // orientation-explicit; old files must keep decoding).
      WriteI32(raw ? -height : height); // biHeight
      WriteU16(1);                  // biPlanes
      WriteU16((ushort)(raw ? 32 : 24)); // biBitCount
      WriteFourCC(raw ? "\0\0\0\0" : "MJPG"); // biCompression: BI_RGB vs MJPG
      WriteU32((uint)(raw ? width * height * 4 : width * height * 3)); // biSizeImage (suggestion)
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
      if (_raw) return false; // raw track: AppendRawFrame only (never mix)
      if (jpeg == null || jpeg.Length < 4) return false;
      if (!(jpeg[0] == 0xFF && jpeg[1] == 0xD8 && jpeg[2] == 0xFF)) return false;
      return AppendChunk(jpeg);
    } catch (Exception) { return false; }
  }

  // Lossless gameplay append: EXACT-size RGBA only (any other length means
  // the capture pipeline changed shape mid-session — refuse, never adapt).
  public bool AppendRawFrame(byte[] rgba) {
    try {
      if (!_open || _finalized || _fs == null) return false;
      if (!_raw) return false; // MJPEG track: AppendJpeg only (never mix)
      if (rgba == null || rgba.Length != _width * _height * 4) return false;
      return AppendChunk(rgba);
    } catch (Exception) { return false; }
  }

  bool AppendChunk(byte[] payload) {
    try {
      long chunkTagPos = _fs.Position;
      WriteFourCC("00dc");
      WriteU32((uint)payload.Length);
      _fs.Write(payload, 0, payload.Length);
      if ((payload.Length & 1) == 1) _fs.WriteByte(0); // pad to even
      _idx.Add(new IdxEntry {
        OffsetFromMovi = chunkTagPos - _moviDataStart,
        Size = payload.Length
      });
      _frames++;
      if (payload.Length > _maxChunk) _maxChunk = payload.Length;
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
      AviScan s;
      if (!ScanAvi(path, out s)) { info.Reason = s.FailReason ?? "io"; return false; }
      if (!s.HasAvih) { info.Reason = "no-avih"; return false; }
      if (!s.SawIdx) { info.Reason = "no-idx1"; return false; }
      if (s.TotalFrames == 0 || s.Idx.Count == 0) { info.Reason = "empty-video"; return false; }
      if (s.TotalFrames != s.Idx.Count) { info.Reason = "frames!=idx"; return false; }
      // strf is what ffmpeg actually decodes from: chunk 40 + biSize 40,
      // dims matching avih (raw height signed: negative = top-down), codec
      // matching the handler. A single-40 strf shifts every downstream
      // offset and ffmpeg reports "header parsing" + exit 22 (P2X 2026-09-16).
      if (!s.SawStrf) { info.Reason = "no-strf"; return false; }
      if (s.StrfChunkSize != 40 || s.StrfBiSize != 40) { info.Reason = "bad-strf-size"; return false; }
      try {
        int aw = Math.Abs(s.StrfWidth), ah = Math.Abs(s.StrfHeight);
        if (aw != s.Width || ah != s.Height) { info.Reason = "strf-dims-mismatch"; return false; }
      } catch (Exception) { info.Reason = "strf-dims-mismatch"; return false; }
      try {
        bool isRaw = s.Handler == "DIB ";
        if (isRaw) {
          if (s.StrfCompression != "\0\0\0\0" || s.StrfBitCount != 32) { info.Reason = "strf-codec-mismatch"; return false; }
          if (s.StrfHeight >= 0) { info.Reason = "strf-raw-not-topdown"; return false; }
        } else {
          if (s.StrfCompression != "MJPG" || s.StrfBitCount != 24) { info.Reason = "strf-codec-mismatch"; return false; }
        }
      } catch (Exception) { info.Reason = "strf-codec-mismatch"; return false; }
      // Validate every index entry points at a 00dc chunk.
      for (int i = 0; i < s.Idx.Count; i++) {
        if (s.Idx[i].Tag != "00dc") { info.Reason = "idx-not-00dc"; return false; }
      }
      info.Valid = true;
      info.Width = s.Width;
      info.Height = s.Height;
      info.Fps = s.Fps;
      info.FrameCount = s.Idx.Count;
      info.DurationSec = s.Fps > 0 ? s.Idx.Count / s.Fps : 0;
      info.Handler = s.Handler;
      info.Reason = null;
      return true;
    } catch (Exception e) {
      info.Reason = "io:" + e.GetType().Name;
      return false;
    }
  }

  // --- streaming scan (never loads the file: headers are KBs, movi GBs are
  // skipped by seeking, idx rows are 16 B each). Same verdicts/reasons as
  // the old whole-blob parser, so every existing test keeps passing while
  // multi-GB raw intermediates verify fine (2 GB+ blobs OOM reliably).
  struct AviScan {
    public bool HasAvih;
    public long TotalFrames;
    public int Width;
    public int Height;
    public double Fps;
    public string Handler;
    public long MoviBase; // -1 = none
    public bool SawIdx;
    public List<ScanIdx> Idx;
    public string FailReason;
    // strf (BITMAPINFOHEADER) pins: ffmpeg parses THIS, not avih, for decode.
    // A corrupt strf passes every avih/idx check yet fails ffmpeg (P2X
    // exit-22: single-40 strf). Validate it here so VerifyGameTrack fails
    // fast with a clear reason instead of shipping a file ffmpeg rejects.
    public bool SawStrf;
    public uint StrfChunkSize;
    public uint StrfBiSize;
    public int StrfWidth;
    public int StrfHeight; // raw top-down is negative (abs compared)
    public int StrfBitCount;
    public string StrfCompression;
  }

  struct ScanIdx {
    public string Tag;
    public long OffsetFromMovi; // relative to movi LIST data (spec form)
    public int Size;            // chunk payload bytes (no pad)
  }

  static bool ReadExactly(FileStream fs, byte[] buf, int off, int count) {
    try {
      while (count > 0) {
        int n = fs.Read(buf, off, count);
        if (n <= 0) return false;
        off += n;
        count -= n;
      }
      return true;
    } catch (Exception) { return false; }
  }

  static bool TryReadU32At(FileStream fs, long pos, out uint v) {
    v = 0;
    try {
      var b = new byte[4];
      long save = fs.Position;
      try {
        fs.Seek(pos, SeekOrigin.Begin);
        if (!ReadExactly(fs, b, 0, 4)) return false;
        v = RdU32(b, 0);
        return true;
      } finally { try { fs.Seek(save, SeekOrigin.Begin); } catch (Exception) { } }
    } catch (Exception) { return false; }
  }

  static bool TryReadTagAt(FileStream fs, long pos, out string tag) {
    tag = string.Empty;
    try {
      var b = new byte[4];
      long save = fs.Position;
      try {
        fs.Seek(pos, SeekOrigin.Begin);
        if (!ReadExactly(fs, b, 0, 4)) return false;
        tag = RdTag(b, 0);
        return true;
      } finally { try { fs.Seek(save, SeekOrigin.Begin); } catch (Exception) { } }
    } catch (Exception) { return false; }
  }

  static bool ScanAvi(string path, out AviScan s) {
    s = new AviScan { MoviBase = -1, Idx = new List<ScanIdx>(), Handler = string.Empty };
    try {
      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
        long len = 0;
        try { len = fs.Length; } catch (Exception) { s.FailReason = "io"; return false; }
        string t0, t8;
        if (len < 12 || !TryReadTagAt(fs, 0, out t0) || t0 != "RIFF"
            || !TryReadTagAt(fs, 8, out t8) || t8 != "AVI ") {
          s.FailReason = "not-riff-avi";
          return false;
        }
        long pos = 12;
        while (pos + 8 <= len) {
          string tag;
          uint size;
          if (!TryReadTagAt(fs, pos, out tag) || !TryReadU32At(fs, pos + 4, out size)) break;
          if (tag == "LIST") {
            string kind;
            if (!TryReadTagAt(fs, pos + 8, out kind)) break;
            if (kind == "hdrl") {
              long end = Math.Min(len, pos + 8 + size);
              long hstart = pos + 12;
              int hlen = (int)Math.Max(0, Math.Min(end - hstart, 65536));
              var hb = new byte[hlen];
              try { fs.Seek(hstart, SeekOrigin.Begin); } catch (Exception) { break; }
              if (!ReadExactly(fs, hb, 0, hlen)) break;
              ParseHdrl(hb, ref s);
            } else if (kind == "movi") {
              s.MoviBase = pos + 12;
            }
            pos += 8 + size + (size & 1u);
          } else if (tag == "idx1") {
            // Entries are 16 B each (frame count scale, not byte scale).
            long count = size / 16;
            if (count < 0 || count > 10000000) { s.FailReason = "idx-huge"; return false; }
            int bytes = (int)(count * 16);
            var ib = new byte[bytes];
            try { fs.Seek(pos + 8, SeekOrigin.Begin); } catch (Exception) { s.FailReason = "io"; return false; }
            if (!ReadExactly(fs, ib, 0, bytes)) { s.FailReason = "idx-truncated"; return false; }
            for (long i = 0; i < count; i++) {
              int e = (int)(i * 16);
              uint off, ln;
              try {
                string ck = RdTag(ib, e);
                uint fl = RdU32(ib, e + 4);
                off = RdU32(ib, e + 8);
                ln = RdU32(ib, e + 12);
                if ((long)e + 16 > bytes) { s.FailReason = "idx-truncated"; return false; }
                s.Idx.Add(new ScanIdx { Tag = ck, OffsetFromMovi = off, Size = (int)ln });
              } catch (Exception) { s.FailReason = "idx-truncated"; return false; }
            }
            s.SawIdx = true;
            break; // idx1 is last by construction
          } else {
            pos += 8 + size + (size & 1u);
          }
          if (pos < 0 || pos > len) break;
        }
        return true;
      }
    } catch (Exception) { s.FailReason = "io"; return false; }
  }

  // hdrl sub-parse over a small in-memory buffer (same precedence as before:
  // strh rate/scale wins over avih us-per-frame). Also captures strf so
  // TryReadInfo can validate the decode header ffmpeg actually reads.
  static int RdI32(byte[] b, int o) {
    try { return unchecked((int)RdU32(b, o)); } catch (Exception) { return 0; }
  }

  static int RdU16(byte[] b, int o) {
    try { return b[o] | (b[o + 1] << 8); } catch (Exception) { return 0; }
  }

  static void ParseHdrl(byte[] hb, ref AviScan s) {
    try {
      int end = hb.Length;
      int q = 0;
      while (q + 8 <= end) {
        string st = RdTag(hb, q);
        uint ss = RdU32(hb, q + 4);
        if (st == "avih" && q + 8 + 56 <= end) {
          s.HasAvih = true;
          s.TotalFrames = RdU32(hb, q + 8 + 16);
          s.Width = (int)RdU32(hb, q + 8 + 32);
          s.Height = (int)RdU32(hb, q + 8 + 36);
          uint us = RdU32(hb, q + 8);
          if (us > 0) s.Fps = 1000000.0 / us;
        } else if (st == "LIST" && q + 12 <= end && RdTag(hb, q + 8) == "strl") {
          int send = Math.Min(end, q + 8 + (int)ss);
          int r = q + 12;
          while (r + 8 <= send) {
            string sst = RdTag(hb, r);
            uint ssl = RdU32(hb, r + 4);
            if (sst == "strh" && r + 8 + 56 <= send) {
              s.Handler = RdTag(hb, r + 8 + 4);
              uint scale = RdU32(hb, r + 8 + 20);
              uint rate = RdU32(hb, r + 8 + 24);
              if (scale > 0) s.Fps = (double)rate / scale;
            } else if (sst == "strf" && r + 8 + 40 <= send) {
              s.SawStrf = true;
              s.StrfChunkSize = ssl;
              s.StrfBiSize = RdU32(hb, r + 8);
              s.StrfWidth = RdI32(hb, r + 8 + 4);
              s.StrfHeight = RdI32(hb, r + 8 + 8);
              s.StrfBitCount = RdU16(hb, r + 8 + 14);
              try { s.StrfCompression = RdTag(hb, r + 8 + 16); }
              catch (Exception) { s.StrfCompression = string.Empty; }
            }
            r += 8 + (int)ssl + ((int)ssl & 1);
          }
        }
        q += 8 + (int)ss + ((int)ss & 1);
      }
    } catch (Exception) { }
  }

  // Extract frame i (0-based) via idx1. Returns false + reason unless the
  // chunk is a 00dc with a JPEG SOI — i.e. a REAL decodable still, not just
  // an index row. allowRaw accepts BI_RGB payloads (exact-size RGBA, SOI
  // check skipped): the lossless gameplay path. Offsets are resolved against
  // the movi base found by scan (spec-relative form) with an absolute fallback.
  public static bool TryExtractFrame(string path, int index, out byte[] jpeg, out string reason) {
    return TryExtractFrame(path, index, out jpeg, out reason, false);
  }

  public static bool TryExtractFrame(string path, int index, out byte[] data, out string reason, bool allowRaw) {
    data = null;
    reason = null;
    try {
      AviScan s;
      if (!ScanAvi(path, out s)) { reason = s.FailReason ?? "io"; return false; }
      if (s.MoviBase < 0) { reason = "no-movi"; return false; }
      long count = s.Idx.Count;
      if (index < 0 || index >= count) { reason = "index-range"; return false; }
      ScanIdx e = s.Idx[index];
      uint len = (uint)e.Size;
      uint maxLen = allowRaw ? 256u * 1024u * 1024u : 8u * 1024u * 1024u;
      if (len == 0 || len > maxLen) { reason = "bad-len"; return false; }
      // Spec-relative first, absolute fallback. Single payload read only.
      long[] cands = { s.MoviBase + e.OffsetFromMovi, e.OffsetFromMovi };
      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
        long flen = 0;
        try { flen = fs.Length; } catch (Exception) { reason = "io"; return false; }
        foreach (long c in cands) {
          if (c < 0 || c + 8 + len > flen) continue;
          string ck;
          uint ckLen;
          if (!TryReadTagAt(fs, c, out ck) || ck != "00dc") continue;
          if (!TryReadU32At(fs, c + 4, out ckLen) || ckLen != len) continue;
          var out_ = new byte[len];
          try { fs.Seek(c + 8, SeekOrigin.Begin); } catch (Exception) { continue; }
          if (!ReadExactly(fs, out_, 0, (int)len)) continue;
          if (!allowRaw && (out_.Length < 4 || !(out_[0] == 0xFF && out_[1] == 0xD8 && out_[2] == 0xFF))) continue;
          data = out_;
          return true;
        }
      }
      reason = "chunk-unresolvable";
      return false;
    } catch (Exception ex) {
      reason = "io:" + ex.GetType().Name;
      return false;
    }
  }
}
