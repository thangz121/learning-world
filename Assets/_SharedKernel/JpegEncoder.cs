// _SharedKernel/JpegEncoder.cs — Lead owns. Phase 2.3 gameplay-frame encoder.
// Pure C# (NO UnityEngine): baseline DCT JPEG (4:2:0, sequential, JFIF) from
// raw RGBA pixels. Runs on the recorder's worker thread — Unity's
// ImageConversion must stay on the main thread, and gameplay frames must
// never stall it. Standard Annex-K tables (quant + Huffman) with IJG
// quality scaling; quality comes from MediaRecordingConfig (30..100).
// This is an ESTABLISHED format implementation (like WavWriter), not a new
// codec: every byte it emits is decoded by ffmpeg/PIL/viewers, proven by
// the compat run (Unity writes -> real ffmpeg decodes).
// NOTE (raw path): the GAME video path no longer uses this (q90 forensic —
// even q90 imprinted speckle while x264 stayed transparent, so game frames
// go raw BGRA straight to x264). Retained for format-compat tests (P21C/D)
// and any future still-thumbnail use; NOT on the game video path.
// API: TryEncode(rgba, width, height, quality, out jpeg). Never throws.
using System;
using System.IO;

public static class JpegEncoder {
  // Natural-order -> zigzag-position map (Annex A ordering).
  static readonly int[] Zigzag = {
    0, 1, 8,16, 9, 2, 3,10,
    17,24,32,25,18,11, 4, 5,
    12,19,26,33,40,48,41,34,
    27,20,13, 6, 7,14,21,28,
    35,42,49,56,57,50,43,36,
    29,22,15,23,30,37,44,51,
    58,59,52,45,38,31,39,46,
    53,60,61,54,47,55,62,63
  };

  static readonly int[] BaseLumaQ = {
    16,11,10,16,24,40,51,61,
    12,12,14,19,26,58,60,55,
    14,13,16,24,40,57,69,56,
    14,17,22,29,51,87,80,62,
    18,22,37,56,68,109,103,77,
    24,35,55,64,81,104,113,92,
    49,64,78,87,103,121,120,101,
    72,92,95,98,112,100,103,99
  };

  static readonly int[] BaseChromaQ = {
    17,18,24,47,99,99,99,99,
    18,21,26,66,99,99,99,99,
    24,26,56,99,99,99,99,99,
    47,66,99,99,99,99,99,99,
    99,99,99,99,99,99,99,99,
    99,99,99,99,99,99,99,99,
    99,99,99,99,99,99,99,99,
    99,99,99,99,99,99,99,99
  };

  struct HuffTable {
    public int[] Codes;  // per symbol value (0..255): code bits
    public int[] Sizes;  // per symbol value: code length
  }

  static HuffTable BuildHuffman(int[] bits, int[] vals) {
    var t = new HuffTable { Codes = new int[256], Sizes = new int[256] };
    int code = 0, k = 0;
    for (int len = 1; len <= 16; len++) {
      for (int i = 0; i < bits[len - 1]; i++) {
        int sym = vals[k++];
        t.Codes[sym] = code;
        t.Sizes[sym] = len;
        code++;
      }
      code <<= 1;
    }
    return t;
  }

  static readonly HuffTable DcLuma = BuildHuffman(
    new[] { 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 },
    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 });
  static readonly HuffTable AcLuma = BuildHuffman(
    new[] { 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7d },
    new[] {
      0x01,0x02,0x03,0x00,0x04,0x11,0x05,0x12,0x21,0x31,0x41,0x06,0x13,0x51,0x61,
      0x07,0x22,0x71,0x14,0x32,0x81,0x91,0xa1,0x08,0x23,0x42,0xb1,0xc1,0x15,0x52,
      0xd1,0xf0,0x24,0x33,0x62,0x72,0x82,0x09,0x0a,0x16,0x17,0x18,0x19,0x1a,0x25,
      0x26,0x27,0x28,0x29,0x2a,0x34,0x35,0x36,0x37,0x38,0x39,0x3a,0x43,0x44,0x45,
      0x46,0x47,0x48,0x49,0x4a,0x53,0x54,0x55,0x56,0x57,0x58,0x59,0x5a,0x63,0x64,
      0x65,0x66,0x67,0x68,0x69,0x6a,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7a,0x83,
      0x84,0x85,0x86,0x87,0x88,0x89,0x8a,0x92,0x93,0x94,0x95,0x96,0x97,0x98,0x99,
      0x9a,0xa2,0xa3,0xa4,0xa5,0xa6,0xa7,0xa8,0xa9,0xaa,0xb2,0xb3,0xb4,0xb5,0xb6,
      0xb7,0xb8,0xb9,0xba,0xc2,0xc3,0xc4,0xc5,0xc6,0xc7,0xc8,0xc9,0xca,0xd2,0xd3,
      0xd4,0xd5,0xd6,0xd7,0xd8,0xd9,0xda,0xe1,0xe2,0xe3,0xe4,0xe5,0xe6,0xe7,0xe8,
      0xe9,0xea,0xf1,0xf2,0xf3,0xf4,0xf5,0xf6,0xf7,0xf8,0xf9,0xfa });
  static readonly HuffTable DcChroma = BuildHuffman(
    new[] { 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 },
    new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 });
  static readonly HuffTable AcChroma = BuildHuffman(
    new[] { 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77 },
    new[] {
      0x00,0x01,0x02,0x03,0x11,0x04,0x05,0x21,0x31,0x06,0x12,0x41,0x51,0x07,0x61,
      0x71,0x13,0x22,0x32,0x81,0x08,0x14,0x42,0x91,0xa1,0xb1,0xc1,0x09,0x23,0x33,
      0x52,0xf0,0x15,0x62,0x72,0xd1,0x0a,0x16,0x24,0x34,0xe1,0x25,0xf1,0x17,0x18,
      0x19,0x1a,0x26,0x27,0x28,0x29,0x2a,0x35,0x36,0x37,0x38,0x39,0x3a,0x43,0x44,
      0x45,0x46,0x47,0x48,0x49,0x4a,0x53,0x54,0x55,0x56,0x57,0x58,0x59,0x5a,0x63,
      0x64,0x65,0x66,0x67,0x68,0x69,0x6a,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7a,
      0x82,0x83,0x84,0x85,0x86,0x87,0x88,0x89,0x8a,0x92,0x93,0x94,0x95,0x96,0x97,
      0x98,0x99,0x9a,0xa2,0xa3,0xa4,0xa5,0xa6,0xa7,0xa8,0xa9,0xaa,0xb2,0xb3,0xb4,
      0xb5,0xb6,0xb7,0xb8,0xb9,0xba,0xc2,0xc3,0xc4,0xc5,0xc6,0xc7,0xc8,0xc9,0xca,
      0xd2,0xd3,0xd4,0xd5,0xd6,0xd7,0xd8,0xd9,0xda,0xe2,0xe3,0xe4,0xe5,0xe6,0xe7,
      0xe8,0xe9,0xea,0xf2,0xf3,0xf4,0xf5,0xf6,0xf7,0xf8,0xf9,0xfa });

  static int[] ScaledQ(int[] baseQ, int quality) {
    int q = quality < 1 ? 1 : quality > 100 ? 100 : quality;
    int scale = q < 50 ? 5000 / q : 200 - q * 2;
    var out_ = new int[64];
    for (int i = 0; i < 64; i++) {
      int v = (baseQ[i] * scale + 50) / 100;
      out_[i] = v < 1 ? 1 : v > 255 ? 255 : v;
    }
    // Reorder to zigzag so block[i] lines up with the scan order.
    var z = new int[64];
    for (int i = 0; i < 64; i++) z[i] = out_[Zigzag[i]];
    return z;
  }

  // AAN float FDCT, 8x8 in place (input level-shifted outside).
  static void Fdct(float[] b) {
    for (int r = 0; r < 8; r++) {
      int o = r * 8;
      float d0 = b[o], d1 = b[o+1], d2 = b[o+2], d3 = b[o+3];
      float d4 = b[o+4], d5 = b[o+5], d6 = b[o+6], d7 = b[o+7];
      float t0 = d0+d7, t7 = d0-d7, t1 = d1+d6, t6 = d1-d6;
      float t2 = d2+d5, t5 = d2-d5, t3 = d3+d4, t4 = d3-d4;
      float t10 = t0+t3, t13 = t0-t3, t11 = t1+t2, t12 = t1-t2;
      b[o] = t10+t11; b[o+4] = t10-t11;
      float z1 = (t12+t13)*0.707106781f;
      b[o+2] = t13+z1; b[o+6] = t13-z1;
      t10 = t4+t5; t11 = t5+t6; t12 = t6+t7;
      float z5 = (t10-t12)*0.382683433f;
      float z2 = 0.541196100f*t10+z5;
      float z4 = 1.306562965f*t12+z5;
      float z3 = t11*0.707106781f;
      float z11 = t7+z3, z13 = t7-z3;
      b[o+5] = z13+z2; b[o+3] = z13-z2;
      b[o+1] = z11+z4; b[o+7] = z11-z4;
    }
    for (int c = 0; c < 8; c++) {
      float d0 = b[c], d1 = b[c+8], d2 = b[c+16], d3 = b[c+24];
      float d4 = b[c+32], d5 = b[c+40], d6 = b[c+48], d7 = b[c+56];
      float t0 = d0+d7, t7 = d0-d7, t1 = d1+d6, t6 = d1-d6;
      float t2 = d2+d5, t5 = d2-d5, t3 = d3+d4, t4 = d3-d4;
      float t10 = t0+t3, t13 = t0-t3, t11 = t1+t2, t12 = t1-t2;
      b[c] = (t10+t11)/8f; b[c+32] = (t10-t11)/8f;
      float z1 = (t12+t13)*0.707106781f;
      b[c+16] = (t13+z1)/8f; b[c+48] = (t13-z1)/8f;
      t10 = t4+t5; t11 = t5+t6; t12 = t6+t7;
      float z5 = (t10-t12)*0.382683433f;
      float z2 = 0.541196100f*t10+z5;
      float z4 = 1.306562965f*t12+z5;
      float z3 = t11*0.707106781f;
      float z11 = t7+z3, z13 = t7-z3;
      b[c+40] = (z13+z2)/8f; b[c+24] = (z13-z2)/8f;
      b[c+8] = (z11+z4)/8f; b[c+56] = (z11-z4)/8f;
    }
  }

  static int Category(int v) {
    int a = v < 0 ? -v : v;
    if (a == 0) return 0;
    int n = 0;
    while (a != 0) { n++; a >>= 1; }
    return n;
  }

  sealed class BitWriter {
    readonly MemoryStream _ms = new MemoryStream();
    int _acc;
    int _nbits;
    public void WriteBits(int code, int size) {
      _acc = (_acc << size) | (code & ((1 << size) - 1));
      _nbits += size;
      while (_nbits >= 8) {
        _nbits -= 8;
        int b = (_acc >> _nbits) & 0xFF;
        _ms.WriteByte((byte)b);
        if (b == 0xFF) _ms.WriteByte(0x00); // stuffing
      }
    }
    public void WriteTable(HuffTable t, int sym) {
      WriteBits(t.Codes[sym], t.Sizes[sym]);
    }
    public void Flush() {
      if (_nbits > 0) {
        int b = ((_acc << (8 - _nbits)) | ((1 << (8 - _nbits)) - 1)) & 0xFF;
        _ms.WriteByte((byte)b);
        if (b == 0xFF) _ms.WriteByte(0x00);
      }
    }
    public byte[] Raw() {
      return _ms.ToArray();
    }
  }

  static void EncodeBlock(BitWriter bw, float[] pixels, int[] q,
      HuffTable dcT, HuffTable acT, ref int prevDc) {
    var coef = new float[64];
    for (int i = 0; i < 64; i++) coef[i] = pixels[i] - 128f;
    Fdct(coef);
    // Zigzag scan: natural-order DCT output reordered to the scan order
    // that the (already zigzag-ordered) quant tables expect.
    var zz = new int[64];
    for (int i = 0; i < 64; i++) {
      int v = (int)Math.Round(coef[Zigzag[i]] / q[i]);
      zz[i] = v;
    }
    int diff = zz[0] - prevDc;
    prevDc = zz[0];
    int cat = Category(diff);
    bw.WriteTable(dcT, cat);
    if (cat != 0) {
      int bits = diff >= 0 ? diff : diff - 1;
      bw.WriteBits(bits, cat);
    }
    int zeroRun = 0;
    for (int i = 1; i < 64; i++) {
      int v = zz[i];
      if (v == 0) { zeroRun++; continue; }
      while (zeroRun >= 16) {
        bw.WriteTable(acT, 0xF0);
        zeroRun -= 16;
      }
      int c = Category(v);
      bw.WriteTable(acT, (zeroRun << 4) | c);
      int bits = v >= 0 ? v : v - 1;
      bw.WriteBits(bits, c);
      zeroRun = 0;
    }
    if (zeroRun > 0) bw.WriteTable(acT, 0x00); // EOB
  }

  static void WriteU16(MemoryStream ms, int v) {
    ms.WriteByte((byte)((v >> 8) & 0xFF));
    ms.WriteByte((byte)(v & 0xFF));
  }

  static void WriteDqt(MemoryStream ms, int id, int[] qNatural) {
    ms.WriteByte(0xFF); ms.WriteByte(0xDB);
    WriteU16(ms, 2 + 1 + 64);
    ms.WriteByte((byte)id);
    // DQT stores NATURAL order: un-zigzag the scan-ordered table.
    var nat = new int[64];
    for (int i = 0; i < 64; i++) nat[Zigzag[i]] = qNatural[i];
    for (int i = 0; i < 64; i++) ms.WriteByte((byte)nat[i]);
  }

  static void WriteDht(MemoryStream ms, int tcTh, int[] bits, int[] vals) {
    ms.WriteByte(0xFF); ms.WriteByte(0xC4);
    WriteU16(ms, (ushort)(2 + 1 + 16 + vals.Length));
    ms.WriteByte((byte)tcTh);
    for (int i = 0; i < 16; i++) ms.WriteByte((byte)bits[i]);
    for (int i = 0; i < vals.Length; i++) ms.WriteByte((byte)vals[i]);
  }

  static readonly int[] DcLumaBits = { 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 };
  static readonly int[] DcLumaVals = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
  static readonly int[] DcChromaBits = { 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 };

  public static bool TryEncode(byte[] rgba, int width, int height, int quality, out byte[] jpeg) {
    jpeg = null;
    try {
      if (rgba == null || width < 16 || height < 16 || width > 4096 || height > 4096) return false;
      if ((long)rgba.Length < (long)width * height * 4) return false;
      int[] qLuma = ScaledQ(BaseLumaQ, quality);
      int[] qChroma = ScaledQ(BaseChromaQ, quality);

      // Planar YCbCr (full-res Y, half-res Cb/Cr with edge clamp).
      int cw = (width + 1) / 2, ch = (height + 1) / 2;
      var Y = new float[width * height];
      var Cb = new float[cw * ch];
      var Cr = new float[cw * ch];
      for (int y = 0; y < height; y++) {
        for (int x = 0; x < width; x++) {
          int o = (y * width + x) * 4;
          float r = rgba[o], g = rgba[o + 1], b = rgba[o + 2];
          float yy = 0.299f * r + 0.587f * g + 0.114f * b;
          Y[y * width + x] = yy;
          if ((x & 1) == 0 && (y & 1) == 0) {
            float cb = -0.168736f * r - 0.331264f * g + 0.5f * b + 128f;
            float cr = 0.5f * r - 0.418688f * g - 0.081312f * b + 128f;
            // 2x2 average for honest 4:2:0.
            float sCb = cb, sCr = cr;
            int n = 1;
            if (x + 1 < width) {
              int o2 = o + 4;
              float r2 = rgba[o2], g2 = rgba[o2 + 1], b2 = rgba[o2 + 2];
              sCb += -0.168736f * r2 - 0.331264f * g2 + 0.5f * b2 + 128f;
              sCr += 0.5f * r2 - 0.418688f * g2 - 0.081312f * b2 + 128f;
              n++;
            }
            if (y + 1 < height) {
              int o3 = o + width * 4;
              float r3 = rgba[o3], g3 = rgba[o3 + 1], b3 = rgba[o3 + 2];
              sCb += -0.168736f * r3 - 0.331264f * g3 + 0.5f * b3 + 128f;
              sCr += 0.5f * r3 - 0.418688f * g3 - 0.081312f * b3 + 128f;
              n++;
              if (x + 1 < width) {
                int o4 = o3 + 4;
                float r4 = rgba[o4], g4 = rgba[o4 + 1], b4 = rgba[o4 + 2];
                sCb += -0.168736f * r4 - 0.331264f * g4 + 0.5f * b4 + 128f;
                sCr += 0.5f * r4 - 0.418688f * g4 - 0.081312f * b4 + 128f;
                n++;
              }
            }
            Cb[(y / 2) * cw + (x / 2)] = sCb / n;
            Cr[(y / 2) * cw + (x / 2)] = sCr / n;
          }
        }
      }

      var bw = new BitWriter();
      var blk = new float[64];
      int dcY = 0, dcCb = 0, dcCr = 0;
      int mcuW = (width + 15) / 16, mcuH = (height + 15) / 16;
      for (int my = 0; my < mcuH; my++) {
        for (int mx = 0; mx < mcuW; mx++) {
          // 4 Y blocks.
          for (int by = 0; by < 2; by++) {
            for (int bx = 0; bx < 2; bx++) {
              for (int iy = 0; iy < 8; iy++) {
                int sy = Math.Min(height - 1, my * 16 + by * 8 + iy);
                for (int ix = 0; ix < 8; ix++) {
                  int sx = Math.Min(width - 1, mx * 16 + bx * 8 + ix);
                  blk[iy * 8 + ix] = Y[sy * width + sx];
                }
              }
              EncodeBlock(bw, blk, qLuma, DcLuma, AcLuma, ref dcY);
            }
          }
          // Cb + Cr blocks (half res, edge-clamped).
          for (int c = 0; c < 2; c++) {
            float[] plane = c == 0 ? Cb : Cr;
            for (int iy = 0; iy < 8; iy++) {
              int sy = Math.Min(ch - 1, my * 8 + iy);
              for (int ix = 0; ix < 8; ix++) {
                int sx = Math.Min(cw - 1, mx * 8 + ix);
                blk[iy * 8 + ix] = plane[sy * cw + sx];
              }
            }
            if (c == 0) EncodeBlock(bw, blk, qChroma, DcChroma, AcChroma, ref dcCb);
            else EncodeBlock(bw, blk, qChroma, DcChroma, AcChroma, ref dcCr);
          }
        }
      }
      bw.Flush();
      byte[] scan = bw.Raw();

      var ms = new MemoryStream();
      ms.WriteByte(0xFF); ms.WriteByte(0xD8); // SOI
      ms.WriteByte(0xFF); ms.WriteByte(0xE0); // APP0 JFIF
      WriteU16(ms, 16);
      foreach (char chh in "JFIF\0") ms.WriteByte((byte)chh);
      ms.WriteByte(1); ms.WriteByte(1); ms.WriteByte(0);
      WriteU16(ms, 1); WriteU16(ms, 1);
      ms.WriteByte(0); ms.WriteByte(0);
      WriteDqt(ms, 0, qLuma);
      WriteDqt(ms, 1, qChroma);
      ms.WriteByte(0xFF); ms.WriteByte(0xC0); // SOF0
      WriteU16(ms, 8 + 3 * 3);
      ms.WriteByte(8); // precision
      WriteU16(ms, height); WriteU16(ms, width);
      ms.WriteByte(3);
      ms.WriteByte(1); ms.WriteByte(0x22); ms.WriteByte(0); // Y  2x2 Q0
      ms.WriteByte(2); ms.WriteByte(0x11); ms.WriteByte(1); // Cb 1x1 Q1
      ms.WriteByte(3); ms.WriteByte(0x11); ms.WriteByte(1); // Cr 1x1 Q1
      WriteDht(ms, 0x00, DcLumaBits, DcLumaVals);
      WriteDht(ms, 0x10, new[] { 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7d }, AcLumaVals());
      WriteDht(ms, 0x01, DcChromaBits, DcLumaVals);
      WriteDht(ms, 0x11, new[] { 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77 }, AcChromaVals());
      ms.WriteByte(0xFF); ms.WriteByte(0xDA); // SOS
      WriteU16(ms, 6 + 2 * 3);
      ms.WriteByte(3);
      ms.WriteByte(1); ms.WriteByte(0x00);
      ms.WriteByte(2); ms.WriteByte(0x11);
      ms.WriteByte(3); ms.WriteByte(0x11);
      ms.WriteByte(0); ms.WriteByte(0x3F); ms.WriteByte(0);
      ms.Write(scan, 0, scan.Length);
      ms.WriteByte(0xFF); ms.WriteByte(0xD9); // EOI
      jpeg = ms.ToArray();
      return true;
    } catch (Exception) {
      jpeg = null;
      return false;
    }
  }

  // Canonical AC symbol orders (kept as methods to avoid duplicating the
  // 162-entry literals used by the table builders above).
  static int[] AcLumaVals() {
    return new[] {
      0x01,0x02,0x03,0x00,0x04,0x11,0x05,0x12,0x21,0x31,0x41,0x06,0x13,0x51,0x61,
      0x07,0x22,0x71,0x14,0x32,0x81,0x91,0xa1,0x08,0x23,0x42,0xb1,0xc1,0x15,0x52,
      0xd1,0xf0,0x24,0x33,0x62,0x72,0x82,0x09,0x0a,0x16,0x17,0x18,0x19,0x1a,0x25,
      0x26,0x27,0x28,0x29,0x2a,0x34,0x35,0x36,0x37,0x38,0x39,0x3a,0x43,0x44,0x45,
      0x46,0x47,0x48,0x49,0x4a,0x53,0x54,0x55,0x56,0x57,0x58,0x59,0x5a,0x63,0x64,
      0x65,0x66,0x67,0x68,0x69,0x6a,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7a,0x83,
      0x84,0x85,0x86,0x87,0x88,0x89,0x8a,0x92,0x93,0x94,0x95,0x96,0x97,0x98,0x99,
      0x9a,0xa2,0xa3,0xa4,0xa5,0xa6,0xa7,0xa8,0xa9,0xaa,0xb2,0xb3,0xb4,0xb5,0xb6,
      0xb7,0xb8,0xb9,0xba,0xc2,0xc3,0xc4,0xc5,0xc6,0xc7,0xc8,0xc9,0xca,0xd2,0xd3,
      0xd4,0xd5,0xd6,0xd7,0xd8,0xd9,0xda,0xe1,0xe2,0xe3,0xe4,0xe5,0xe6,0xe7,0xe8,
      0xe9,0xea,0xf1,0xf2,0xf3,0xf4,0xf5,0xf6,0xf7,0xf8,0xf9,0xfa };
  }

  static int[] AcChromaVals() {
    return new[] {
      0x00,0x01,0x02,0x03,0x11,0x04,0x05,0x21,0x31,0x06,0x12,0x41,0x51,0x07,0x61,
      0x71,0x13,0x22,0x32,0x81,0x08,0x14,0x42,0x91,0xa1,0xb1,0xc1,0x09,0x23,0x33,
      0x52,0xf0,0x15,0x62,0x72,0xd1,0x0a,0x16,0x24,0x34,0xe1,0x25,0xf1,0x17,0x18,
      0x19,0x1a,0x26,0x27,0x28,0x29,0x2a,0x35,0x36,0x37,0x38,0x39,0x3a,0x43,0x44,
      0x45,0x46,0x47,0x48,0x49,0x4a,0x53,0x54,0x55,0x56,0x57,0x58,0x59,0x5a,0x63,
      0x64,0x65,0x66,0x67,0x68,0x69,0x6a,0x73,0x74,0x75,0x76,0x77,0x78,0x79,0x7a,
      0x82,0x83,0x84,0x85,0x86,0x87,0x88,0x89,0x8a,0x92,0x93,0x94,0x95,0x96,0x97,
      0x98,0x99,0x9a,0xa2,0xa3,0xa4,0xa5,0xa6,0xa7,0xa8,0xa9,0xaa,0xb2,0xb3,0xb4,
      0xb5,0xb6,0xb7,0xb8,0xb9,0xba,0xc2,0xc3,0xc4,0xc5,0xc6,0xc7,0xc8,0xc9,0xca,
      0xd2,0xd3,0xd4,0xd5,0xd6,0xd7,0xd8,0xd9,0xda,0xe2,0xe3,0xe4,0xe5,0xe6,0xe7,
      0xe8,0xe9,0xea,0xf2,0xf3,0xf4,0xf5,0xf6,0xf7,0xf8,0xf9,0xfa };
  }
}
