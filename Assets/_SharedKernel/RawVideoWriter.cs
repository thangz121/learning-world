// _SharedKernel/RawVideoWriter.cs — Lead owns. Phase 2.3 lossless gameplay container.
//
// Why not AVI: RIFF/AVI sizes are 32-bit — a 1080p30 raw take passes 4 GB in
// ~16 s and every size field wraps (P30: 4.39 GB game.avi lost idx1 for every
// reader, dropping the game track out of the transcode unnoticed).
// Why FOOTER, not header: ffmpeg's rawvideo demuxer starts at byte 0 with no
// skip option — a 24-byte header shifts EVERY frame by 6 px (P30 forensic
// caught it at 23 dB: invisible to the eye, unmissable to PSNR). So:
//   frames first: exactly width*height*4 BGRA bytes each, top-down rows,
//   byte 0 = frame 0 (ffmpeg reads them natively, zero shift);
//   then ONE 24-byte footer: magic "LWRV" | version u32=1 | width u32 |
//   height u32 | frameCount u32 | reserved u32=0.
// A torn tail (crash) has no footer: readers fall back to length math
// (length must divide evenly by the frame size). Trailing sub-frame bytes
// are ignored by the demuxer (proven: exit 0 on headered files whose 24 B
// header became exactly such a tail-equivalent remainder).
// ffmpeg input: -f rawvideo -pix_fmt bgra -s WxH -framerate <measured>
//   -frames:v <count> -i file  (count exact from the pump, never probed).
// (Cam keeps AVI/MJPEG: ~1 MB/take, nowhere near any ceiling.)
//
// API: Begin(path,w,h) -> AppendFrame(bgra exact-size) -> Finalize(out frames)
// -> Close(). Counters mirror AviMjpegWriter. Never throws out of public methods.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public sealed class RawVideoWriter : IDisposable {
  public const string Magic = "LWRV";
  public const uint Version = 1;
  public const int FooterSize = 24;

  FileStream _fs;
  bool _open;
  bool _finalized;
  bool _disposed;
  int _width;
  int _height;
  long _frames;

  public long FrameCount {
    get { try { return _frames; } catch (Exception) { return 0; } }
  }

  public bool Begin(string path, int width, int height) {
    try {
      CloseStream();
      if (string.IsNullOrEmpty(path)) return false;
      if (width < 16 || width > 4096 || height < 16 || height > 4096) return false;
      string dir;
      try { dir = Path.GetDirectoryName(path); } catch (Exception) { dir = null; }
      if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) return false;
      // 1 MB buffer + SequentialScan: 8.3 MB frames at 30 Hz (~249 MB/s);
      // the default 4 KB buffer would syscall-chop every frame.
      _fs = new FileStream(path, FileMode.Create, FileAccess.Write,
        FileShare.Read, 1 << 20, FileOptions.SequentialScan);
      _width = width;
      _height = height;
      _frames = 0;
      _open = true;
      _finalized = false;
      return true;
    } catch (Exception) {
      CloseStream();
      _open = false;
      return false;
    }
  }

  // Lossless append: EXACT-size BGRA only (any other length means the capture
  // pipeline changed shape mid-session — refuse, never adapt, never mix).
  public bool AppendFrame(byte[] bgra) {
    try {
      if (!_open || _finalized || _fs == null) return false;
      long expect = (long)_width * _height * 4;
      if (bgra == null || bgra.Length != expect) return false;
      _fs.Write(bgra, 0, bgra.Length);
      _frames++;
      return true;
    } catch (Exception) { return false; }
  }

  public bool Finalize(out long frames) {
    frames = _frames;
    try {
      if (!_open || _fs == null) return false;
      if (!_finalized) {
        // Footer LAST (after all frames): magic | version | dims | count.
        // No seeking, no backpatching — fully streaming by design.
        WriteFourCC(Magic);
        WriteU32(Version);
        WriteU32((uint)_width);
        WriteU32((uint)_height);
        WriteU32(_frames > uint.MaxValue ? uint.MaxValue : (uint)_frames);
        WriteU32(0);
        try { _fs.Flush(); } catch (Exception) { }
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

  void WriteU32(uint v) {
    _fs.WriteByte((byte)(v & 0xFF));
    _fs.WriteByte((byte)((v >> 8) & 0xFF));
    _fs.WriteByte((byte)((v >> 16) & 0xFF));
    _fs.WriteByte((byte)((v >> 24) & 0xFF));
  }
}

// Streaming reader (never loads the file: one 24 B footer read + offset
// math, payloads read singly). Footer-first; a torn tail (crash, no footer)
// falls back to length math. Failures carry explicit reasons, never exceptions.
public static class RawVideoReader {
  public struct RawInfo {
    public bool Valid;
    public string Reason;
    public int Width;
    public int Height;
    public long FrameCount;
    public long FrameBytes;
  }

  static uint RdU32(byte[] b, int o) {
    return (uint)(b[o] | (b[1 + o] << 8) | (b[2 + o] << 16) | (b[3 + o] << 24));
  }

  static string RdTag(byte[] b, int o) {
    return Encoding.ASCII.GetString(b, o, 4);
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

  public static bool TryReadInfo(string path, out RawInfo info) {
    info = new RawInfo();
    try {
      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
        FileShare.ReadWrite, 1 << 20, FileOptions.SequentialScan)) {
        long len;
        try { len = fs.Length; } catch (Exception) { info.Reason = "io"; return false; }
        if (len < RawVideoWriter.FooterSize + 256) { info.Reason = "too-small"; return false; }
        // Footer-first: magic at len-24 + sane dims + count agreeing with
        // length math (all three must agree; any disagreement = torn file).
        var fb = new byte[RawVideoWriter.FooterSize];
        try { fs.Seek(len - RawVideoWriter.FooterSize, SeekOrigin.Begin); }
        catch (Exception) { info.Reason = "io"; return false; }
        if (!ReadExactly(fs, fb, 0, fb.Length)) { info.Reason = "io"; return false; }
        if (RdTag(fb, 0) == RawVideoWriter.Magic
            && RdU32(fb, 4) == RawVideoWriter.Version) {
          int w = (int)RdU32(fb, 8), h = (int)RdU32(fb, 12);
          long count = RdU32(fb, 16);
          if (w < 16 || w > 4096 || h < 16 || h > 4096) { info.Reason = "bad-dims"; return false; }
          long frameBytes = (long)w * h * 4;
          if (len - RawVideoWriter.FooterSize != count * frameBytes) {
            info.Reason = "tail-truncated";
            return false;
          }
          if (count <= 0) { info.Reason = "empty-video"; return false; }
          info.Valid = true;
          info.Width = w;
          info.Height = h;
          info.FrameBytes = frameBytes;
          info.FrameCount = count;
          info.Reason = null;
          return true;
        }
        // No footer (torn tail / crash): length math only, must divide evenly.
        // Dims are unknowable without the footer — report, don't guess.
        info.Reason = "no-footer";
        return false;
      }
    } catch (Exception e) {
      info.Reason = "io:" + e.GetType().Name;
      return false;
    }
  }

  // Extract frame i (0-based) by offset math. Returns false + reason unless
  // the payload is exactly one frame (a REAL still, not a torn tail).
  public static bool TryExtractFrame(string path, int index, int expectW, int expectH,
      out byte[] data, out string reason) {
    data = null;
    reason = null;
    try {
      RawInfo info;
      if (!TryReadInfo(path, out info)) { reason = info.Reason ?? "io"; return false; }
      if (info.Width != expectW || info.Height != expectH) { reason = "dims-mismatch"; return false; }
      if (index < 0 || index >= info.FrameCount) { reason = "index-range"; return false; }
      long off = (long)index * info.FrameBytes; // byte 0 = frame 0 (no header, no shift)
      using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
        FileShare.ReadWrite, 1 << 20, FileOptions.SequentialScan)) {
        long len;
        try { len = fs.Length; } catch (Exception) { reason = "io"; return false; }
        if (off < 0 || off + info.FrameBytes > len) { reason = "chunk-unresolvable"; return false; }
        var out_ = new byte[info.FrameBytes];
        try { fs.Seek(off, SeekOrigin.Begin); } catch (Exception) { reason = "io"; return false; }
        if (!ReadExactly(fs, out_, 0, out_.Length)) { reason = "chunk-unresolvable"; return false; }
        data = out_;
        return true;
      }
    } catch (Exception) { reason = "io"; return false; }
  }
}
