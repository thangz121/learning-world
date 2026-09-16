#!/usr/bin/env python3
"""verify_recording.py — Phase 2.3 file verification (stdlib only).

Decodes the game-produced recording files and reports FIVE SEPARATE
verdicts per medium (§39 — never confuse them):

  CREATED  file exists, non-zero
  VALID    container parses (WAV fmt / AVI hdrl+idx1), params as expected
  DECODED  a real decoder opens the payload (stdlib `wave` for audio;
           JPEG SOI extraction + optional PIL pixel decode for video)
  CONTENT  audible energy present (audio) / non-frozen distinct frames (video)
  CORRECT  human inspection hook (face/phrase) — the tool reports the
           evidence (hashes, sizes, energies); the HUMAN confirms content
           in the E2E runbook (handoff §22). Never auto-claimed.

Usage:
  python tools/verify_recording.py --audio rec-xxx_audio.wav --video rec-xxx_video.avi
  python tools/verify_recording.py --audio f.wav --expect-audio-samples 16000
  python tools/verify_recording.py --video c.avi --expect-video-frames 10 --expect-width 320 --expect-height 240
  python tools/verify_recording.py --selftest   # no Unity, no hardware needed
  python tools/verify_recording.py --audio f.wav --json   # machine output

If ffmpeg/ffprobe exist on PATH they are used as a SECOND decoder opinion
(optional, never required). The game runtime never depends on them (§38).

Exit codes: 0 = all requested media PASS (content included);
1 = any FAIL; 2 = usage/file-missing error.
"""
import argparse
import hashlib
import json
import os
import shutil
import struct
import sys
import tempfile
import wave

MIN_AUDIO_DURATION_SEC = 0.2
MIN_AUDIO_PEAK = 0.02  # tone 0.25 / speech ~0.05+ pass; digital silence fails
MIN_VIDEO_FRAMES = 1
MIN_MP4_DURATION_SEC = 0.2
MIN_MP3_DURATION_SEC = 0.2


def verdict(name):
    return {"check": name, "result": "PENDING", "detail": ""}


def mark(v, ok, detail=""):
    v["result"] = "PASS" if ok else "FAIL"
    v["detail"] = detail
    return ok


# ---------------- audio (WAV PCM16 mono 16k) ----------------

def verify_audio(path, expect_samples=None, min_duration=MIN_AUDIO_DURATION_SEC,
                 min_peak=MIN_AUDIO_PEAK):
    out = {"medium": "audio", "path": path,
           "created": verdict("created"), "valid": verdict("valid"),
           "decoded": verdict("decoded"), "content": verdict("content"),
           "correct": verdict("correct"), "info": {}}
    ok_all = True
    if not path or not os.path.isfile(path):
        mark(out["created"], False, "missing file")
        for k in ("valid", "decoded", "content"):
            mark(out[k], False, "no file")
        mark(out["correct"], False, "needs human listen (E2E runbook)")
        out["overall"] = "FAIL"
        return out
    size = os.path.getsize(path)
    if not mark(out["created"], size > 44, "bytes=%d" % size):
        ok_all = False
    # VALID + DECODED via the stdlib wave decoder (a REAL decoder, not a
    # header peek): params must be the canonical game format.
    try:
        with wave.open(path, "rb") as w:
            ch = w.getnchannels()
            sw = w.getsampwidth()
            fr = w.getframerate()
            n = w.getnframes()
            out["info"] = {"channels": ch, "sampwidth": sw, "framerate": fr,
                           "frames": n, "duration_sec": round(n / fr, 3) if fr else 0}
            params_ok = (ch == 1 and sw == 2 and fr == 16000)
            dur_ok = (fr and (n / fr) >= min_duration)
            if expect_samples is not None:
                params_ok = params_ok and (n == expect_samples)
            if not mark(out["valid"], params_ok and dur_ok,
                        "mono=%s 16bit=%s 16k=%s dur=%.2fs%s" % (
                            ch == 1, sw == 2, fr == 16000,
                            (n / fr) if fr else 0,
                            (" samples=%d" % n) if expect_samples is not None else "")):
                ok_all = False
            raw = w.readframes(n)
            got = len(raw) // 2
            if not mark(out["decoded"], got == n and got > 0,
                        "decoded_samples=%d/%d" % (got, n)):
                ok_all = False
                raw = b""
            else:
                # CONTENT: audible energy present (not silence, not empty).
                peak = 0.0
                total = 0.0
                cnt = len(raw) // 2
                for i in range(cnt):
                    s = struct.unpack_from("<h", raw, i * 2)[0] / 32768.0
                    a = abs(s)
                    if a > peak:
                        peak = a
                    total += a
                mean = total / cnt if cnt else 0.0
                out["info"]["peak"] = round(peak, 4)
                out["info"]["mean_abs"] = round(mean, 4)
                if not mark(out["content"], peak >= min_peak,
                            "peak=%.4f mean=%.4f (min_peak=%.3f)" % (peak, mean, min_peak)):
                    ok_all = False
    except Exception as e:  # wave.Error, EOFError, OSError ...
        if out["valid"]["result"] == "PENDING":
            mark(out["valid"], False, "decoder refused: %s" % type(e).__name__)
        if out["decoded"]["result"] == "PENDING":
            mark(out["decoded"], False, "decoder refused: %s" % type(e).__name__)
        mark(out["content"], False, "no decode")
        ok_all = False
    mark(out["correct"], False, "needs human listen: known phrase audible? (E2E runbook)")
    out["overall"] = "PASS" if ok_all else "FAIL"
    return out


# ---------------- video (AVI/MJPEG) ----------------

def _rdtag(b, o):
    return bytes(b[o:o + 4]).decode("ascii", "replace")


def _rdu32(b, o):
    return struct.unpack_from("<I", b, o)[0]


def _rdi32(b, o):
    return struct.unpack_from("<i", b, o)[0]


def _rdu16(b, o):
    return struct.unpack_from("<H", b, o)[0]


def _parse_avi(blob):
    """Returns dict or raises ValueError. Independent implementation of the
    AVI 1.0 + MJPEG layout the game writer produces (spec-derived, not a
    byte-copy of the C# writer). Also parses strf (BITMAPINFOHEADER): ffmpeg
    decodes from strf, not avih, so a corrupt strf must fail VALID even when
    avih/idx look fine (P2X exit-22: single-40 strf passed avih checks)."""
    if len(blob) < 12 or _rdtag(blob, 0) != "RIFF" or _rdtag(blob, 8) != "AVI ":
        raise ValueError("not-riff-avi")
    pos = 12
    avih = {}
    strh = {}
    strf = {}
    idx = []
    idx_pos = -1
    movi_base = -1
    size = len(blob)
    while pos + 8 <= size:
        tag = _rdtag(blob, pos)
        ln = _rdu32(blob, pos + 4)
        if tag == "LIST":
            kind = _rdtag(blob, pos + 8) if pos + 12 <= size else ""
            if kind == "hdrl":
                end = min(size, pos + 8 + ln)
                q = pos + 12
                while q + 8 <= end:
                    st = _rdtag(blob, q)
                    sl = _rdu32(blob, q + 4)
                    if st == "avih" and q + 8 + 56 <= end:
                        us = _rdu32(blob, q + 8)
                        avih = {"total": _rdu32(blob, q + 8 + 16),
                                "width": _rdu32(blob, q + 8 + 32),
                                "height": _rdu32(blob, q + 8 + 36),
                                "us_per_frame": us}
                    elif st == "LIST" and q + 12 <= end and _rdtag(blob, q + 8) == "strl":
                        # strh/strf live one level down (standard AVI nesting)
                        send = min(end, q + 8 + sl)
                        r = q + 12
                        while r + 8 <= send:
                            sst = _rdtag(blob, r)
                            ssl = _rdu32(blob, r + 4)
                            if sst == "strh" and r + 8 + 56 <= send:
                                scale = _rdu32(blob, r + 8 + 20)
                                rate = _rdu32(blob, r + 8 + 24)
                                strh = {"handler": _rdtag(blob, r + 8 + 4),
                                        "scale": scale, "rate": rate}
                            elif sst == "strf" and r + 8 + 40 <= send:
                                strf = {"chunk": ssl,
                                        "biSize": _rdu32(blob, r + 8),
                                        "biWidth": _rdi32(blob, r + 8 + 4),
                                        "biHeight": _rdi32(blob, r + 8 + 8),
                                        "biPlanes": _rdu16(blob, r + 8 + 12),
                                        "biBitCount": _rdu16(blob, r + 8 + 14),
                                        "biCompression": _rdtag(blob, r + 8 + 16)}
                            r += 8 + ssl + (ssl & 1)
                    q += 8 + sl + (sl & 1)
            elif kind == "movi":
                movi_base = pos + 12
            pos += 8 + ln + (ln & 1)
        elif tag == "idx1":
            idx_pos = pos + 8
            n = ln // 16
            for i in range(n):
                e = idx_pos + i * 16
                idx.append({"ckid": _rdtag(blob, e),
                            "flags": _rdu32(blob, e + 4),
                            "offset": _rdu32(blob, e + 8),
                            "length": _rdu32(blob, e + 12)})
            break
        else:
            pos += 8 + ln + (ln & 1)
        if pos < 0 or pos > size:
            break
    if not avih:
        raise ValueError("no-avih")
    if idx_pos < 0:
        raise ValueError("no-idx1")
    if movi_base < 0:
        raise ValueError("no-movi")
    if not strf:
        raise ValueError("no-strf")
    # strf is the decode header: chunk 40 + biSize 40, dims matching avih
    # (raw height signed: negative = top-down), codec matching handler.
    if strf.get("chunk") != 40 or strf.get("biSize") != 40:
        raise ValueError("bad-strf-size")
    if abs(strf.get("biWidth") or 0) != avih.get("width") or \
       abs(strf.get("biHeight") or 0) != avih.get("height"):
        raise ValueError("strf-dims-mismatch")
    return {"avih": avih, "strh": strh, "strf": strf,
            "idx": idx, "movi_base": movi_base}


def _extract_frame(blob, parsed, i, raw_ok=False):
    e = parsed["idx"][i]
    cap = 256 * 1024 * 1024 if raw_ok else 8 * 1024 * 1024
    if e["ckid"] != "00dc" or e["length"] == 0 or e["length"] > cap:
        raise ValueError("bad-idx-entry")
    for base in (parsed["movi_base"] + e["offset"], e["offset"]):
        c = base
        if c < 0 or c + 8 + e["length"] > len(blob):
            continue
        if _rdtag(blob, c) != "00dc":
            continue
        if _rdu32(blob, c + 4) != e["length"]:
            continue
        payload = bytes(blob[c + 8:c + 8 + e["length"]])
        if raw_ok and parsed.get("strh", {}).get("handler") == "DIB ":
            w = parsed.get("avih", {}).get("width") or 0
            h = parsed.get("avih", {}).get("height") or 0
            if len(payload) == w * h * 4 and w >= 16 and h >= 16:
                return payload
            continue
        if len(payload) >= 4 and payload[0] == 0xFF and payload[1] == 0xD8 and payload[2] == 0xFF:
            return payload
    raise ValueError("chunk-unresolvable")


def _raw_is_dark(payload, floor=4):
    """Black-path tripwire mirror (see CountDarkFrame in service): raw RGBA
    means near-zero bytes when the capture rendered black."""
    try:
        if not payload:
            return True
        s = 0
        n = 0
        for i in range(0, len(payload), 1024):
            s += payload[i]
            n += 1
        return n > 0 and (s / n) < floor
    except Exception:
        return True


def _try_pil_decode(payload, raw_shape=None):
    """Optional second decoder opinion (PIL only if installed). raw_shape =
    (w, h) interprets the payload as BGRA bytes (AVI BI_RGB wire order),
    converting to RGBA for correct colors."""
    try:
        from PIL import Image  # type: ignore
    except Exception:
        return None
    try:
        import io as _io
        if raw_shape is not None:
            try:
                # BGRA wire -> RGBA image (correct colors, not just shape).
                im = Image.frombytes("RGBA", raw_shape, payload,
                                     "raw", "BGRA")
            except Exception:
                im = Image.frombytes("RGBA", raw_shape, payload)
            im.load()
            return {"pil_size": list(im.size), "pil_mode": im.mode}
        im = Image.open(_io.BytesIO(payload))
        im.load()
        return {"pil_size": list(im.size), "pil_mode": im.mode}
    except Exception as e:
        return {"pil_error": "%s" % type(e).__name__}


def verify_video(path, expect_frames=None, expect_width=None, expect_height=None):
    out = {"medium": "video", "path": path,
           "created": verdict("created"), "valid": verdict("valid"),
           "decoded": verdict("decoded"), "content": verdict("content"),
           "correct": verdict("correct"), "info": {}}
    ok_all = True
    if not path or not os.path.isfile(path):
        mark(out["created"], False, "missing file")
        for k in ("valid", "decoded", "content"):
            mark(out[k], False, "no file")
        mark(out["correct"], False, "needs human view (E2E runbook)")
        out["overall"] = "FAIL"
        return out
    size = os.path.getsize(path)
    if not mark(out["created"], size > 64, "bytes=%d" % size):
        ok_all = False
        for k in ("valid", "decoded", "content"):
            mark(out[k], False, "no file")
        mark(out["correct"], False, "needs human view (E2E runbook)")
        out["overall"] = "FAIL"
        return out
    try:
        with open(path, "rb") as f:
            blob = f.read()
        parsed = _parse_avi(blob)
        avih = parsed["avih"]
        strh = parsed["strh"]
        strf = parsed.get("strf", {})
        n = len(parsed["idx"])
        fps = (strh["rate"] / strh["scale"]) if strh.get("scale") else (
            1000000.0 / avih["us_per_frame"] if avih.get("us_per_frame") else 0)
        out["info"] = {"width": avih.get("width"), "height": avih.get("height"),
                       "fps": round(fps, 3) if fps else 0,
                       "frames": n,
                       "duration_sec": round(n / fps, 3) if fps else 0,
                       "handler": strh.get("handler"),
                       "strf": {"chunk": strf.get("chunk"),
                                "biSize": strf.get("biSize"),
                                "biWidth": strf.get("biWidth"),
                                "biHeight": strf.get("biHeight"),
                                "biBitCount": strf.get("biBitCount"),
                                "biCompression": strf.get("biCompression")}}
        shape_ok = (strh.get("handler") in ("MJPG", "DIB ") and n >= MIN_VIDEO_FRAMES
                    and avih.get("total") == n)
        # strf codec pins (decode header ffmpeg reads): DIB = BI_RGB 32-bit
        # top-down; MJPG = MJPG 24-bit. _parse_avi already enforces chunk 40
        # + biSize 40 + dims; here enforce the codec/top-down contract.
        try:
            if strh.get("handler") == "DIB ":
                shape_ok = shape_ok and strf.get("biBitCount") == 32 \
                    and (strf.get("biCompression") or "").replace("\x00", "") == "" \
                    and (strf.get("biHeight") or 0) < 0
            elif strh.get("handler") == "MJPG":
                shape_ok = shape_ok and strf.get("biBitCount") == 24 \
                    and strf.get("biCompression") == "MJPG"
            else:
                shape_ok = False
        except Exception:
            shape_ok = False
        dim_ok = True
        if expect_width is not None:
            dim_ok = dim_ok and (avih.get("width") == expect_width)
        if expect_height is not None:
            dim_ok = dim_ok and (avih.get("height") == expect_height)
        if expect_frames is not None:
            dim_ok = dim_ok and (n == expect_frames)
        if not mark(out["valid"], shape_ok and dim_ok,
                    "handler=%s frames=%d %dx%d fps=%.1f dur=%.2fs" % (
                        strh.get("handler"), n, avih.get("width"), avih.get("height"),
                        fps or 0, (n / fps) if fps else 0)):
            ok_all = False
        # DECODED: every index entry must resolve to a real payload; spot
        # pixel-decode first/mid/last when PIL is available. Raw (DIB)
        # payloads are exact-size RGBA, not JPEG.
        is_raw = (strh.get("handler") == "DIB ")
        raw_shape = None
        if is_raw:
            raw_shape = (avih.get("width"), avih.get("height"))
        bad = 0
        first = mid = last = None
        # Anti-fake-fps scan (raw only): stride-hash EVERY frame while it is
        # in hand; adjacent-identical payloads = the pipeline re-emitted one
        # capture twice. Cheap (2K samples/frame) next to the extract itself.
        dup_pairs = 0
        max_run = 1
        run = 1
        prev_h = None
        for i in range(n):
            try:
                p = _extract_frame(blob, parsed, i, raw_ok=is_raw)
                if i == 0:
                    first = p
                if i == n // 2:
                    mid = p
                if i == n - 1:
                    last = p
                if is_raw and p:
                    h = hashlib.sha256(p[::4096]).hexdigest()[:16]
                    if prev_h is not None:
                        if h == prev_h:
                            dup_pairs += 1
                            run += 1
                            max_run = max(max_run, run)
                        else:
                            run = 1
                    prev_h = h
            except ValueError:
                bad += 1
                run = 1
                prev_h = None
        if not mark(out["decoded"], bad == 0 and first is not None,
                    "extracted=%d/%d bad=%d" % (n - bad, n, bad)):
            ok_all = False
        else:
            pil = _try_pil_decode(first, raw_shape if is_raw else None)
            if pil:
                out["info"]["pil_first"] = pil
            # CONTENT: not blank/frozen/corrupt. MJPEG: the three probes must
            # be non-trivial JPEGs and must DIFFER (a frozen single frame
            # repeated N times hashes identically). RAW lossless: identical
            # bytes are EXPECTED when nothing moves, so frozen-detection
            # instead rejects near-black payloads (black-path tripwire).
            hashes = {}
            for label, p in (("first", first), ("mid", mid), ("last", last)):
                h = hashlib.sha256(p).hexdigest()[:16]
                hashes[label] = h
                out["info"]["len_%s" % label] = len(p)
            out["info"]["sha_first12"] = hashes
            sizes_ok = all(len(p) > 256 for p in (first, mid, last) if p)
            if is_raw:
                dark = any(_raw_is_dark(p) for p in (first, mid, last) if p)
                out["info"]["dup_pairs"] = dup_pairs
                out["info"]["dup_max_run"] = max_run
                out["info"]["dup_rate"] = round(dup_pairs / n, 4) if n else 0
                frozen = (n > 1 and max_run >= n)
                if not mark(out["content"], sizes_ok and not dark and not frozen,
                            "raw-lens=%s alldark=%s dup_pairs=%d max_run=%d" % (
                                [len(p) for p in (first, mid, last) if p],
                                dark, dup_pairs, max_run)):
                    ok_all = False
            else:
                distinct = len(set(hashes.values())) > 1 or n == 1
                if not mark(out["content"], sizes_ok and distinct,
                            "lens=%s distinct=%s" % (
                                [len(p) for p in (first, mid, last) if p], distinct)):
                    ok_all = False
    except ValueError as e:
        if out["valid"]["result"] == "PENDING":
            mark(out["valid"], False, "container: %s" % e)
        if out["decoded"]["result"] == "PENDING":
            mark(out["decoded"], False, "no decode")
        mark(out["content"], False, "no decode")
        ok_all = False
    except Exception as e:
        mark(out["valid"], False, "io: %s" % type(e).__name__)
        mark(out["decoded"], False, "io: %s" % type(e).__name__)
        mark(out["content"], False, "io: %s" % type(e).__name__)
        ok_all = False
    mark(out["correct"], False, "needs human view: face/session content? (E2E runbook)")
    out["overall"] = "PASS" if ok_all else "FAIL"
    return out


# ---------------- rawvid stream (lossless game intermediate) ----------------

RAWVID_MAGIC = b"LWRV"
RAWVID_VERSION = 1
RAWVID_FOOTER = 24


def _parse_rawvid_footer(tail24, total_len):
    """24-byte footer (byte 0 = frame 0, dims+count at the END so ffmpeg's
    headerless rawvideo demuxer reads frames with zero shift) or raise
    ValueError. Independent of the C# writer."""
    if len(tail24) < RAWVID_FOOTER:
        raise ValueError("too-small")
    if bytes(tail24[0:4]) != RAWVID_MAGIC:
        raise ValueError("no-footer")
    ver = struct.unpack_from("<I", tail24, 4)[0]
    if ver != RAWVID_VERSION:
        raise ValueError("bad-version")
    w = struct.unpack_from("<I", tail24, 8)[0]
    h = struct.unpack_from("<I", tail24, 12)[0]
    count = struct.unpack_from("<I", tail24, 16)[0]
    if w < 16 or w > 4096 or h < 16 or h > 4096:
        raise ValueError("bad-dims")
    fb = w * h * 4
    if total_len - RAWVID_FOOTER != count * fb:
        raise ValueError("tail-truncated")
    if count <= 0:
        raise ValueError("empty-video")
    return {"width": w, "height": h, "frame_bytes": fb, "frames": count}


def verify_rawvid(path, expect_frames=None, expect_width=None, expect_height=None):
    out = {"medium": "rawvid", "path": path,
           "created": verdict("created"), "valid": verdict("valid"),
           "decoded": verdict("decoded"), "content": verdict("content"),
           "correct": verdict("correct"), "info": {}}
    ok_all = True
    if not path or not os.path.isfile(path):
        mark(out["created"], False, "missing file")
        for k in ("valid", "decoded", "content"):
            mark(out[k], False, "no file")
        mark(out["correct"], False, "needs human view (E2E runbook)")
        out["overall"] = "FAIL"
        return out
    size = os.path.getsize(path)
    if not mark(out["created"], size > RAWVID_FOOTER + 256, "bytes=%d" % size):
        ok_all = False
        for k in ("valid", "decoded", "content"):
            mark(out[k], False, "no file")
        mark(out["correct"], False, "needs human view (E2E runbook)")
        out["overall"] = "FAIL"
        return out
    try:
        with open(path, "rb") as f:
            f.seek(max(0, size - RAWVID_FOOTER))
            tail = f.read(RAWVID_FOOTER)
            try:
                hdr = _parse_rawvid_footer(tail, size)
            except ValueError as e:
                mark(out["valid"], False, "footer: %s" % e)
                mark(out["decoded"], False, "no decode")
                mark(out["content"], False, "no decode")
                mark(out["correct"], False, "needs human view (E2E runbook)")
                out["overall"] = "FAIL"
                return out
            w, h, fb = hdr["width"], hdr["height"], hdr["frame_bytes"]
            n = hdr["frames"]
            out["info"] = {"width": w, "height": h, "frames": n,
                           "frame_bytes": fb, "torn_tail": False}
            shape_ok = (n >= MIN_VIDEO_FRAMES)
            dim_ok = True
            if expect_width is not None:
                dim_ok = dim_ok and (w == expect_width)
            if expect_height is not None:
                dim_ok = dim_ok and (h == expect_height)
            if expect_frames is not None:
                dim_ok = dim_ok and (n == expect_frames)
            if not mark(out["valid"], shape_ok and dim_ok,
                        "rawvid %dx%d frames=%d" % (w, h, n)):
                ok_all = False
            # Single sequential pass from byte 0 (frame 0 lives at offset 0):
            # retain first/mid/last, stride-hash all.
            f.seek(0)
            first = mid = last = None
            bad = 0
            dup_pairs = 0
            max_run = 1
            run = 1
            prev_h = None
            mid_idx = n // 2
            for i in range(n):
                chunk = f.read(fb)
                if len(chunk) != fb:
                    bad += 1
                    run = 1
                    prev_h = None
                    break
                if i == 0:
                    first = chunk
                if i == mid_idx:
                    mid = bytes(chunk)
                if i == n - 1:
                    last = chunk
                hh = hashlib.sha256(chunk[::4096]).hexdigest()[:16]
                if prev_h is not None:
                    if hh == prev_h:
                        dup_pairs += 1
                        run += 1
                        max_run = max(max_run, run)
                    else:
                        run = 1
                prev_h = hh
            # Keep only small probe copies (first/mid/last already bytes).
            if not mark(out["decoded"], bad == 0 and first is not None,
                        "scanned=%d/%d bad=%d" % (n - bad, n, bad)):
                ok_all = False
            else:
                pil = _try_pil_decode(first, (w, h))
                if pil:
                    out["info"]["pil_first"] = pil
                hashes = {}
                for label, p in (("first", first), ("mid", mid), ("last", last)):
                    hh = hashlib.sha256(p).hexdigest()[:16]
                    hashes[label] = hh
                    out["info"]["len_%s" % label] = len(p)
                out["info"]["sha_first12"] = hashes
                out["info"]["dup_pairs"] = dup_pairs
                out["info"]["dup_max_run"] = max_run
                out["info"]["dup_rate"] = round(dup_pairs / n, 4) if n else 0
                dark = any(_raw_is_dark(p) for p in (first, mid, last) if p)
                frozen = (n > 1 and max_run >= n)
                if not mark(out["content"],
                            all(len(p) > 256 for p in (first, mid, last) if p)
                            and not dark and not frozen,
                            "alldark=%s dup_pairs=%d max_run=%d" % (
                                dark, dup_pairs, max_run)):
                    ok_all = False
    except Exception as e:
        mark(out["valid"], False, "io: %s" % type(e).__name__)
        mark(out["decoded"], False, "io: %s" % type(e).__name__)
        mark(out["content"], False, "io: %s" % type(e).__name__)
        ok_all = False
    mark(out["correct"], False, "needs human view: face/session content? (E2E runbook)")
    out["overall"] = "PASS" if ok_all else "FAIL"
    return out


# ---------------- ffmpeg second opinion (optional) ----------------

def _run_proc(cmd, timeout=30):
    try:
        import subprocess
        p = subprocess.run(cmd, capture_output=True, timeout=timeout)
        return p.returncode, (p.stdout or b"")[:4096], (p.stderr or b"")[-2048:]
    except Exception as e:
        return None, b"", str(e).encode()[:200]


def ffmpeg_opinion(path, kind):
    ffprobe = shutil.which("ffprobe")
    if not ffprobe or not path or not os.path.isfile(path):
        return None
    try:
        import subprocess
        p = subprocess.run(
            [ffprobe, "-v", "error", "-show_entries",
             "stream=codec_name,width,height,sample_rate,channels" if kind == "video"
             else "stream=codec_name,sample_rate,channels",
             "-of", "json", path],
            capture_output=True, text=True, timeout=30)
        if p.returncode != 0:
            return {"ffprobe": "refused", "stderr": p.stderr.strip()[:200]}
        return {"ffprobe": json.loads(p.stdout or "{}")}
    except Exception as e:
        return {"ffprobe": "error", "detail": type(e).__name__}


# ---------------- MP4 deliverable (H.264 + MP3, PiP session) ----------------

def _mp4_read_boxes(f, start, end):
    """Yield (type, content_start, content_len) for direct children."""
    pos = start
    while pos + 8 <= end:
        f.seek(pos)
        hdr = f.read(8)
        if len(hdr) < 8:
            break
        size = struct.unpack(">I", hdr[:4])[0]
        typ = hdr[4:].decode("ascii", "replace")
        if size == 1:
            big = f.read(8)
            if len(big) < 8:
                break
            size = struct.unpack(">Q", big)[0]
            hlen = 16
        elif size == 0:
            size = end - pos
            hlen = 8
        else:
            hlen = 8
        if size < hlen or pos + size > end + 1:
            break
        yield typ, pos + hlen, size - hlen
        pos += size


def _mp4_find(f, boxes, *path):
    """Descend box path (e.g. moov/trak/mdia); returns (start, len) or None."""
    cur = [(t, s, ln) for (t, s, ln) in boxes]
    for name in path:
        nxt = None
        for (t, s, ln) in cur:
            if t == name:
                nxt = (s, ln)
                break
        if nxt is None:
            return None
        if name == path[-1]:
            return nxt
        s, ln = nxt
        cur = list(_mp4_read_boxes(f, s, s + ln))
    return None


def _parse_mp4(path):
    """Structural parse (seek-based, never loads mdat). Returns dict or
    raises ValueError. Independent of the transcode that produced it."""
    size = os.path.getsize(path)
    info = {"tracks": []}
    with open(path, "rb") as f:
        top = list(_mp4_read_boxes(f, 0, size))
        ftyp = [b for b in top if b[0] == "ftyp"]
        if not ftyp:
            raise ValueError("no-ftyp")
        f.seek(ftyp[0][1])
        major = f.read(4).decode("ascii", "replace")
        info["brand"] = major
        moov = _mp4_find(f, top, "moov")
        if moov is None:
            raise ValueError("no-moov (unfinalized?)")
        ms, ml = moov
        mvhd = _mp4_find(f, list(_mp4_read_boxes(f, ms, ms + ml)), "mvhd")
        if mvhd is None:
            raise ValueError("no-mvhd")
        f.seek(mvhd[0])
        ver = ord(f.read(1))
        if ver == 1:
            # v1: flags(3) + ctime(8) + mtime(8) -> timescale@+20, duration u64@+24
            f.seek(mvhd[0] + 20)
            ts = struct.unpack(">I", f.read(4))[0]
            du = struct.unpack(">Q", f.read(8))[0]
        else:
            # v0: flags(3) + ctime(4) + mtime(4) -> timescale@+12, duration@+16
            f.seek(mvhd[0] + 12)
            ts = struct.unpack(">I", f.read(4))[0]
            du = struct.unpack(">I", f.read(4))[0]
        info["timescale"] = ts
        info["duration_sec"] = (du / ts) if ts else 0
        # Tracks.
        for (t, s, ln) in _mp4_read_boxes(f, ms, ms + ml):
            if t != "trak":
                continue
            tr = {"width": 0, "height": 0, "codec": "?", "handler": "?",
                  "samples": 0, "duration_sec": 0}
            tkhd = _mp4_find(f, [(t, s, ln)], "trak", "tkhd")
            if tkhd:
                f.seek(tkhd[0])
                v = ord(f.read(1))
                # width/height are 16.16 fixed point after flags+times+id+
                # duration+layer/volume/matrix: v0@+76, v1@+88 from content start
                base = tkhd[0] + (88 if v == 1 else 76)
                f.seek(base)
                tr["width"] = struct.unpack(">I", f.read(4))[0] >> 16
                tr["height"] = struct.unpack(">I", f.read(4))[0] >> 16
            mdia = _mp4_find(f, [(t, s, ln)], "trak", "mdia")
            if mdia is None:
                continue
            md = list(_mp4_read_boxes(f, mdia[0], mdia[0] + mdia[1]))
            hdlr = _mp4_find(f, md, "hdlr")
            if hdlr:
                f.seek(hdlr[0] + 8)
                tr["handler"] = f.read(4).decode("ascii", "replace")
            minf = _mp4_find(f, md, "minf")
            if minf is None:
                continue
            stbl = _mp4_find(f, list(_mp4_read_boxes(f, minf[0], minf[0] + minf[1])), "stbl")
            if stbl is None:
                continue
            st = list(_mp4_read_boxes(f, stbl[0], stbl[0] + stbl[1]))
            stsd = _mp4_find(f, st, "stsd")
            if stsd:
                # content: version/flags(4) + entry_count(4) + entry(size(4) + format(4))
                f.seek(stsd[0] + 12)
                tr["codec"] = f.read(4).decode("ascii", "replace")
            # Sample table: count + resolve first/mid/last inside file bounds.
            stsz = _mp4_find(f, st, "stsz")
            stsc = _mp4_find(f, st, "stsc")
            stco = _mp4_find(f, st, "stco")
            co64 = _mp4_find(f, st, "co64")
            sizes = []
            if stsz:
                f.seek(stsz[0])
                f.read(4)
                fixed_sz = struct.unpack(">I", f.read(4))[0]
                count = struct.unpack(">I", f.read(4))[0]
                if fixed_sz:
                    sizes = [fixed_sz] * count
                else:
                    raw = f.read(count * 4)
                    sizes = [struct.unpack_from(">I", raw, i * 4)[0] for i in range(count)]
            chunks = []
            cobox = co64 or stco
            if cobox:
                f.seek(cobox[0])
                f.read(4)
                cc = struct.unpack(">I", f.read(4))[0]
                if co64:
                    chunks = [struct.unpack(">Q", f.read(8))[0] for _ in range(cc)]
                else:
                    chunks = [struct.unpack(">I", f.read(4))[0] for _ in range(cc)]
            runs = []
            if stsc:
                f.seek(stsc[0])
                f.read(4)
                rc = struct.unpack(">I", f.read(4))[0]
                for _ in range(rc):
                    first = struct.unpack(">I", f.read(4))[0]
                    per = struct.unpack(">I", f.read(4))[0]
                    f.read(4)
                    runs.append((first, per))
            tr["samples"] = len(sizes)
            # Resolve first/mid/last sample offsets (container-level decode,
            # same rigor as the AVI idx walk).
            bad = 0
            probed = 0
            if sizes and chunks and runs:
                def chunk_of(sample_idx):
                    # sample_idx 0-based -> (chunk_idx 0-based, first_sample_in_chunk)
                    acc = 0
                    for ci in range(len(chunks)):
                        c1 = ci + 1
                        per = runs[-1][1]
                        for (first, p) in runs:
                            if first <= c1:
                                per = p
                        if sample_idx < acc + per:
                            return ci, acc
                        acc += per
                    return None, None
                for si in {0, len(sizes) // 2, len(sizes) - 1}:
                    ci, acc = chunk_of(si)
                    if ci is None or ci >= len(chunks):
                        bad += 1
                        continue
                    off = chunks[ci]
                    for k in range(acc, si):
                        off += sizes[k]
                    if off < 0 or off + sizes[si] > size:
                        bad += 1
                        continue
                    if sizes[si] == 0:
                        bad += 1
                        continue
                    probed += 1
            tr["sample_probes_ok"] = probed
            tr["sample_probes_bad"] = bad
            info["tracks"].append(tr)
    if not info["tracks"]:
        raise ValueError("no-tracks")
    return info


def _ffmpeg_pixel_probe(ffmpeg, path):
    """TRUE pixel decode: extract one JPEG through H.264, check SOI."""
    rc, out, err = _run_proc(
        [ffmpeg, "-hide_banner", "-v", "error", "-i", path,
         "-frames:v", "1", "-f", "image2pipe", "-vcodec", "mjpeg", "-"],
        timeout=60)
    if rc is None:
        return {"pixel": "SKIP", "detail": "no-ffmpeg-process"}
    if rc != 0:
        return {"pixel": "FAIL", "detail": (err.decode("utf8", "replace"))[:200]}
    ok = len(out) > 256 and out[0] == 0xFF and out[1] == 0xD8 and out[2] == 0xFF
    return {"pixel": "PASS" if ok else "FAIL", "detail": "extracted=%dB" % len(out)}


def verify_mp4(path, expect_width=None, expect_height=None, expect_audio=None,
               min_duration=MIN_MP4_DURATION_SEC):
    out = {"medium": "mp4", "path": path,
           "created": verdict("created"), "valid": verdict("valid"),
           "decoded": verdict("decoded"), "content": verdict("content"),
           "correct": verdict("correct"), "info": {}}
    ok_all = True
    if not path or not os.path.isfile(path):
        mark(out["created"], False, "missing file")
        for k in ("valid", "decoded", "content"):
            mark(out[k], False, "no file")
        mark(out["correct"], False, "needs human view (E2E runbook)")
        out["overall"] = "FAIL"
        return out
    size = os.path.getsize(path)
    if not mark(out["created"], size > 1024, "bytes=%d" % size):
        ok_all = False
        for k in ("valid", "decoded", "content"):
            mark(out[k], False, "no file")
        mark(out["correct"], False, "needs human view (E2E runbook)")
        out["overall"] = "FAIL"
        return out
    try:
        info = _parse_mp4(path)
        out["info"] = {"brand": info.get("brand"),
                       "duration_sec": round(info.get("duration_sec", 0), 3),
                       "tracks": [
                           {"handler": t["handler"], "codec": t["codec"],
                            "width": t["width"], "height": t["height"],
                            "samples": t["samples"],
                            "probes_ok": t.get("sample_probes_ok", 0),
                            "probes_bad": t.get("sample_probes_bad", 0)}
                           for t in info["tracks"]]}
        vids = [t for t in info["tracks"] if t["handler"] == "vide"]
        auds = [t for t in info["tracks"] if t["handler"] == "soun"]
        shape_ok = (len(vids) >= 1 and vids[0]["codec"] == "avc1"
                    and info.get("duration_sec", 0) >= min_duration)
        dim_ok = True
        if expect_width is not None:
            dim_ok = dim_ok and any(t["width"] == expect_width for t in vids)
        if expect_height is not None:
            dim_ok = dim_ok and any(t["height"] == expect_height for t in vids)
        if expect_audio is not None:
            dim_ok = dim_ok and ((len(auds) > 0) == expect_audio)
        if not mark(out["valid"], shape_ok and dim_ok,
                    "brand=%s dur=%.2fs vtracks=%d atracks=%d" % (
                        info.get("brand"), info.get("duration_sec", 0),
                        len(vids), len(auds))):
            ok_all = False
        # DECODED: container-level sample resolution for every video track
        # (same rigor as the AVI idx walk) + pixel proof when ffmpeg exists.
        bad = sum(t.get("sample_probes_bad", 1) for t in vids)
        okn = sum(t.get("sample_probes_ok", 0) for t in vids)
        ffmpeg = shutil.which("ffmpeg")
        pix = _ffmpeg_pixel_probe(ffmpeg, path) if ffmpeg else {"pixel": "SKIP", "detail": "no-ffmpeg"}
        out["info"]["pixel_probe"] = pix
        struct_ok = (okn >= 3 and bad == 0)
        if pix["pixel"] == "FAIL":
            struct_ok = False
        if not mark(out["decoded"], struct_ok,
                    "sample_probes_ok=%d bad=%d pixel=%s" % (okn, bad, pix["pixel"])):
            ok_all = False
        # CONTENT: real duration + real samples + non-trivial size.
        if not mark(out["content"],
                    info.get("duration_sec", 0) >= min_duration
                    and sum(t["samples"] for t in vids) >= 5
                    and size > 4096,
                    "dur=%.2fs vsamples=%d bytes=%d" % (
                        info.get("duration_sec", 0),
                        sum(t["samples"] for t in vids), size)):
            ok_all = False
    except ValueError as e:
        if out["valid"]["result"] == "PENDING":
            mark(out["valid"], False, "container: %s" % e)
        if out["decoded"]["result"] == "PENDING":
            mark(out["decoded"], False, "no decode")
        mark(out["content"], False, "no decode")
        ok_all = False
    except Exception as e:
        mark(out["valid"], False, "io: %s" % type(e).__name__)
        mark(out["decoded"], False, "io: %s" % type(e).__name__)
        mark(out["content"], False, "io: %s" % type(e).__name__)
        ok_all = False
    mark(out["correct"], False, "needs human view: gameplay + PiP face? (E2E runbook)")
    out["overall"] = "PASS" if ok_all else "FAIL"
    return out


# ---------------- MP3 deliverable (LAME VBR voice) ----------------

_MP3_BITRATES = {
    # (version, layer) -> table; version: 1=MPEG1, 2=MPEG2/2.5
    (1, 1): [0, 32, 64, 96, 128, 160, 192, 224, 256, 288, 320, 352, 384, 416, 448],
    (1, 2): [0, 32, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 384],
    (1, 3): [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320],
    (2, 1): [0, 32, 48, 56, 64, 80, 96, 112, 128, 144, 160, 176, 192, 224, 256],
    (2, 2): [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160],
    (2, 3): [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160],
}
_MP3_RATES = {
    1: [44100, 48000, 32000],
    2: [22050, 24000, 16000],
    0: [11025, 12000, 8000],  # MPEG 2.5
}


def _mp3_scan(path):
    """Returns dict(frames, bad, samplerate, version, layer, avg_kbps,
    duration_sec, xing_frames) or raises ValueError."""
    with open(path, "rb") as f:
        blob = f.read()
    pos = 0
    if len(blob) > 10 and blob[:3] == b"ID3":
        sync = ((blob[6] & 0x7F) << 21) | ((blob[7] & 0x7F) << 14) | \
               ((blob[8] & 0x7F) << 7) | (blob[9] & 0x7F)
        pos = 10 + sync
    n = len(blob)
    frames = 0
    bad = 0
    total_bits = 0
    rate = 0
    ver = 0
    layer = 0
    xing_frames = 0
    i = pos
    first = True
    while i + 4 <= n:
        if blob[i] != 0xFF or (blob[i + 1] & 0xE0) != 0xE0:
            # resync: next sync byte (ID3 junk / padding tolerated in gaps)
            j = blob.find(b"\xff", i + 1)
            if j < 0:
                break
            bad += (j - i) if frames else 0
            i = j
            continue
        b1, b2, b3 = blob[i + 1], blob[i + 2], blob[i + 3]
        vm = (b1 >> 3) & 0x03
        lm = (b1 >> 1) & 0x03
        if vm == 1 or lm == 0:
            bad += 1
            i += 2
            continue
        lay = 4 - lm  # 1=I, 2=II, 3=III
        vv = 1 if vm == 3 else (2 if vm == 2 else 0)
        bri = (b2 >> 4) & 0x0F
        sri = (b2 >> 2) & 0x03
        if bri == 0 or bri == 15 or sri == 3:
            bad += 1
            i += 2
            continue
        key = (1 if vv == 1 else 2, lay)
        table = _MP3_BITRATES.get(key)
        rates = _MP3_RATES.get(vv if vv else 0)
        if not table or not rates:
            bad += 1
            i += 2
            continue
        br = table[bri] * 1000
        sr = rates[sri]
        pad = (b2 >> 1) & 0x01
        if lay == 3:
            flen = (144 * br // sr + pad) if vv == 1 else (72 * br // sr + pad)
        elif lay == 2:
            flen = 144 * br // sr + pad
        else:
            flen = (12 * br // sr + pad) * 4
        if flen < 21 or i + flen > n + 1:
            # last partial frame at EOF is truncation, not corruption
            if i + 21 <= n:
                bad += 1
            break
        if first:
            first = False
            ver, layer, rate = vv, lay, sr
            # Xing/Info header (VBR truth when present).
            try:
                ch = (blob[i + 3] >> 6) & 0x03
                side = (17 if ch == 3 else 32) if vv == 1 else (9 if ch == 3 else 17)
                at = i + 4 + side
                if blob[at:at + 4] in (b"Xing", b"Info"):
                    flags = struct.unpack(">I", blob[at + 4:at + 8])[0]
                    if flags & 1:
                        xing_frames = struct.unpack(">I", blob[at + 8:at + 12])[0]
            except Exception:
                pass
        # next frame must sync (unless EOF); a trailing ID3v1 TAG is data, not corruption
        if i + flen + 2 <= n and not (
                blob[i + flen] == 0xFF and (blob[i + flen + 1] & 0xE0) == 0xE0
                and blob[i + flen:i + flen + 4] != b"TAG"):
            # trailing ID3v1 TAG at EOF is data, not corruption
            if blob[i + flen:i + flen + 3] == b"TAG":
                frames += 1
                total_bits += flen * 8
                break
            bad += 1
            i += 1
            continue
        frames += 1
        total_bits += flen * 8
        i += flen
    if frames < 2:
        raise ValueError("too-few-frames=%d" % frames)
    spf = 1152 if (ver == 1 or layer != 3) else 576
    if xing_frames and rate:
        dur = xing_frames * spf / rate
    else:
        avg_br = total_bits / frames * rate / spf if frames and spf else 0
        dur = total_bits / avg_br if avg_br else 0
    return {"frames": frames, "bad_bytes": bad, "samplerate": rate,
            "version": ver, "layer": layer,
            "avg_kbps": round(total_bits / frames * rate / spf / 1000, 1) if frames and spf and rate else 0,
            "duration_sec": round(dur, 3), "xing_frames": xing_frames}


def verify_mp3(path, min_duration=MIN_MP3_DURATION_SEC, min_avg_kbps=8):
    out = {"medium": "mp3", "path": path,
           "created": verdict("created"), "valid": verdict("valid"),
           "decoded": verdict("decoded"), "content": verdict("content"),
           "correct": verdict("correct"), "info": {}}
    ok_all = True
    if not path or not os.path.isfile(path):
        mark(out["created"], False, "missing file")
        for k in ("valid", "decoded", "content"):
            mark(out[k], False, "no file")
        mark(out["correct"], False, "needs human listen (E2E runbook)")
        out["overall"] = "FAIL"
        return out
    size = os.path.getsize(path)
    if not mark(out["created"], size > 512, "bytes=%d" % size):
        ok_all = False
        for k in ("valid", "decoded", "content"):
            mark(out[k], False, "no file")
        mark(out["correct"], False, "needs human listen (E2E runbook)")
        out["overall"] = "FAIL"
        return out
    try:
        st = _mp3_scan(path)
        out["info"] = st
        if not mark(out["valid"], st["layer"] == 3 and st["samplerate"] in (
                16000, 22050, 24000, 32000, 44100, 48000),
                    "mpeg%d layer%d %dHz avg=%.1fkbps" % (
                        1 if st["version"] == 1 else 2, st["layer"],
                        st["samplerate"], st["avg_kbps"])):
            ok_all = False
        bad_ratio = st["bad_bytes"] / max(size, 1)
        if not mark(out["decoded"], st["frames"] >= 10 and bad_ratio < 0.02,
                    "frames=%d bad=%.3f" % (st["frames"], bad_ratio)):
            ok_all = False
        if not mark(out["content"],
                    st["duration_sec"] >= min_duration
                    and st["avg_kbps"] >= min_avg_kbps,
                    "dur=%.2fs avg=%.1fkbps xing=%d" % (
                        st["duration_sec"], st["avg_kbps"], st["xing_frames"])):
            ok_all = False
    except ValueError as e:
        if out["valid"]["result"] == "PENDING":
            mark(out["valid"], False, "frames: %s" % e)
        if out["decoded"]["result"] == "PENDING":
            mark(out["decoded"], False, "no decode")
        mark(out["content"], False, "no decode")
        ok_all = False
    except Exception as e:
        mark(out["valid"], False, "io: %s" % type(e).__name__)
        mark(out["decoded"], False, "io: %s" % type(e).__name__)
        mark(out["content"], False, "io: %s" % type(e).__name__)
        ok_all = False
    mark(out["correct"], False, "needs human listen: known phrase audible? (E2E runbook)")
    out["overall"] = "PASS" if ok_all else "FAIL"
    return out




def _selftest_wav(path):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(16000)
        frames = b"".join(struct.pack("<h", int(0.25 * 32767)) for _ in range(16000))
        w.writeframes(frames)


def _selftest_avi(path, nframes=10, width=320, height=240, fps=10, frame_step=50):
    """Minimal AVI/MJPEG writer (spec-derived, independent of the C#
    writer): RIFF/AVI + hdrl(avih+strl[strh+strf]) + movi(00dc) + idx1."""
    import io
    frames = []
    for i in range(nframes):
        body = bytes([0x30 + (i % 10)]) * (4000 + i * frame_step)
        frames.append(b"\xff\xd8\xff\xe0" + body)
    out = io.BytesIO()

    def tag(s):
        out.write(s.encode("ascii"))

    def u32(v):
        out.write(struct.pack("<I", v))

    def u16(v):
        out.write(struct.pack("<H", v))

    tag("RIFF")
    riff_at = out.tell()
    u32(0)
    tag("AVI ")
    tag("LIST")
    hdrl_at = out.tell()
    u32(0)
    tag("hdrl")
    hdrl_data = out.tell()
    tag("avih")
    u32(56)
    u32(1000000 // fps)
    u32(fps * 32768)
    u32(0)
    u32(0x10)
    avih_frames = out.tell()
    u32(0)
    u32(0)
    u32(1)
    u32(65536)
    u32(width)
    u32(height)
    u32(0)
    u32(0)
    u32(0)
    u32(0)
    tag("LIST")
    strl_at = out.tell()
    u32(0)
    tag("strl")
    strl_data = out.tell()
    tag("strh")
    u32(56)
    tag("vids")
    tag("MJPG")
    u32(0)
    u16(0)
    u16(0)
    u32(0)
    u32(1)
    u32(fps)
    u32(0)
    strh_len = out.tell()
    u32(0)
    u32(65536)
    u32(0xFFFFFFFF)
    u32(0)
    u16(0)
    u16(0)
    u16(width)
    u16(height)
    tag("strf")
    u32(40)
    u32(40)
    out.write(struct.pack("<i", width))
    out.write(struct.pack("<i", height))
    u16(1)
    u16(24)
    tag("MJPG")
    u32(width * height * 3)
    out.write(struct.pack("<i", 0))
    out.write(struct.pack("<i", 0))
    u32(0)
    u32(0)

    def patch(at, val):
        cur = out.tell()
        out.seek(at)
        out.write(struct.pack("<I", val))
        out.seek(cur)

    patch(strl_at, out.tell() - (strl_at + 4))
    patch(hdrl_at, out.tell() - (hdrl_at + 4))
    tag("LIST")
    movi_at = out.tell()
    u32(0)
    tag("movi")
    movi_data = out.tell()
    idx = []
    for f in frames:
        cpos = out.tell()
        tag("00dc")
        u32(len(f))
        out.write(f)
        if len(f) & 1:
            out.write(b"\x00")
        idx.append((cpos - movi_data, len(f)))
    idx_start = out.tell()
    tag("idx1")
    u32(len(idx) * 16)
    for off, ln in idx:
        tag("00dc")
        u32(0x10)
        u32(off)
        u32(ln)
    patch(movi_at, idx_start - (movi_at + 4))
    patch(avih_frames, len(frames))
    patch(strh_len, len(frames))
    patch(riff_at, out.tell() - 8)
    with open(path, "wb") as fh:
        fh.write(out.getvalue())


def _selftest_rawvid(path, nframes=6, width=64, height=48, step=37):
    """Minimal rawvid writer (spec-derived, independent of the C# writer):
    back-to-back BGRA frames + 24-byte footer. step=0 writes identical
    frames (frozen negative)."""
    with open(path, "wb") as f:
        for i in range(nframes):
            # Bright on every stride the tripwire/dup scans use (j*13 spreads
            # values so no 1024-stride aliases to zero).
            body = bytes([(i * step + j * 13 + 64) & 0xFF for j in range(width * height * 4)])
            if step == 0:
                body = bytes([0x40 + (j & 0x3F) for j in range(width * height * 4)])
            f.write(body)
        f.write(RAWVID_MAGIC)
        f.write(struct.pack("<I", RAWVID_VERSION))
        f.write(struct.pack("<I", width))
        f.write(struct.pack("<I", height))
        f.write(struct.pack("<I", nframes))
        f.write(struct.pack("<I", 0))


def run_selftest():
    tmp = tempfile.mkdtemp(prefix="rec-verify-")
    fails = []

    def check(name, cond, detail=""):
        print(("PASS " if cond else "FAIL ") + name + (" — " + detail if detail else ""))
        if not cond:
            fails.append(name)

    try:
        good_wav = os.path.join(tmp, "tone.wav")
        good_avi = os.path.join(tmp, "clip.avi")
        _selftest_wav(good_wav)
        _selftest_avi(good_avi)
        ra = verify_audio(good_wav, expect_samples=16000)
        check("selftest wav overall", ra["overall"] == "PASS", json.dumps(ra["info"]))
        check("selftest wav content audible", ra["content"]["result"] == "PASS")
        rv = verify_video(good_avi, expect_frames=10, expect_width=320, expect_height=240)
        check("selftest avi overall", rv["overall"] == "PASS", json.dumps(rv["info"]))
        check("selftest avi content distinct", rv["content"]["result"] == "PASS")
        # Negatives must FAIL loudly (never a false PASS).
        empty = os.path.join(tmp, "empty.wav")
        open(empty, "wb").close()
        check("empty wav fails", verify_audio(empty)["overall"] == "FAIL")
        trunc = os.path.join(tmp, "trunc.avi")
        with open(good_avi, "rb") as f:
            blob = f.read()
        with open(trunc, "wb") as f:
            f.write(blob[:len(blob) // 2])
        check("truncated avi fails", verify_video(trunc)["overall"] == "FAIL")
        silence = os.path.join(tmp, "silence.wav")
        with wave.open(silence, "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(16000)
            w.writeframes(b"\x00" * 32000)
        rs = verify_audio(silence)
        check("silence created+valid+decoded", all(
            rs[k]["result"] == "PASS" for k in ("created", "valid", "decoded")))
        check("silence content fails (honest, not audible)", rs["content"]["result"] == "FAIL")
        frozen = os.path.join(tmp, "frozen.avi")
        _selftest_avi(frozen, nframes=5, width=320, height=240, fps=10, frame_step=0)
        # Make every payload identical to frame 0 -> frozen detector must fire
        # while decode still passes (index rows stay valid).
        with open(frozen, "rb") as f:
            fb = bytearray(f.read())
        p = _parse_avi(bytes(fb))
        f0 = _extract_frame(bytes(fb), p, 0)
        for i in range(1, len(p["idx"])):
            e = p["idx"][i]
            base = None
            for cand in (p["movi_base"] + e["offset"], e["offset"]):
                if (cand >= 0 and bytes(fb[cand:cand + 4]) == b"00dc"
                        and _rdu32(fb, cand + 4) == e["length"]):
                    base = cand
                    break
            assert base is not None
            fb[base + 8:base + 8 + len(f0)] = f0[:e["length"]]
        with open(frozen, "wb") as f:
            f.write(bytes(fb))
        rf = verify_video(frozen)
        check("frozen video decoded-but-content-fails",
              rf["decoded"]["result"] == "PASS" and rf["content"]["result"] == "FAIL",
              json.dumps(rf["info"].get("sha_first12")))
        # rawvid stream: good clip passes all machine verdicts with dup stats;
        # torn tails and fully-frozen clips fail loudly.
        good_raw = os.path.join(tmp, "game.rawvid")
        _selftest_rawvid(good_raw, nframes=6, width=64, height=48, step=37)
        rr = verify_rawvid(good_raw, expect_frames=6, expect_width=64, expect_height=48)
        check("selftest rawvid overall", rr["overall"] == "PASS", json.dumps(rr["info"]))
        check("selftest rawvid dup clean", rr["info"].get("dup_pairs") == 0)
        torn_raw = os.path.join(tmp, "torn.rawvid")
        with open(good_raw, "rb") as f:
            rb = f.read()
        with open(torn_raw, "wb") as f:
            f.write(rb + b"\x00" * 100)
        check("torn rawvid fails", verify_rawvid(torn_raw)["overall"] == "FAIL")
        frozen_raw = os.path.join(tmp, "frozen.rawvid")
        _selftest_rawvid(frozen_raw, nframes=4, width=64, height=48, step=0)
        rz = verify_rawvid(frozen_raw)
        check("frozen rawvid decoded-but-content-fails",
              rz["decoded"]["result"] == "PASS" and rz["content"]["result"] == "FAIL",
              json.dumps({k: rz["info"].get(k) for k in ("dup_pairs", "dup_max_run")}))
        # MP4/MP3 deliverables: needs a real ffmpeg (SKIP without it — the
        # parsers still get negative coverage on mutated copies below).
        _selftest_deliverables(tmp, check)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)
    print("SELFTEST %s (%d failures)" % ("PASS" if not fails else "FAIL", len(fails)))
    return 0 if not fails else 1


def _selftest_deliverables(tmp, check):
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        print("SKIP mp4/mp3 selftest (no ffmpeg on PATH)")
        return
    good_mp4 = os.path.join(tmp, "s.mp4")
    good_mp3 = os.path.join(tmp, "s.mp3")
    rc, _, err = _run_proc(
        [ffmpeg, "-hide_banner", "-v", "error", "-y",
         "-f", "lavfi", "-i", "testsrc=duration=1:size=320x240:rate=10",
         "-f", "lavfi", "-i", "sine=frequency=440:duration=1:sample_rate=16000",
         "-c:v", "libx264", "-preset", "veryfast", "-crf", "24", "-pix_fmt", "yuv420p",
         "-c:a", "libmp3lame", "-q:a", "4", "-ar", "16000", "-ac", "1",
         "-shortest", good_mp4,
         "-map", "1:a", "-c:a", "libmp3lame", "-q:a", "4", good_mp3],
        timeout=120)
    if rc != 0 or not os.path.isfile(good_mp4):
        print("SKIP mp4/mp3 selftest (lavfi/encoders unavailable: %s)" %
              err.decode("utf8", "replace")[:160])
        return
    rm = verify_mp4(good_mp4, expect_width=320, expect_height=240, expect_audio=True)
    check("selftest mp4 overall", rm["overall"] == "PASS", json.dumps(rm["info"]))
    ra3 = verify_mp3(good_mp3)
    check("selftest mp3 overall", ra3["overall"] == "PASS", json.dumps(ra3["info"]))
    trunc = os.path.join(tmp, "trunc.mp4")
    with open(good_mp4, "rb") as f:
        blob = f.read()
    with open(trunc, "wb") as f:
        f.write(blob[:len(blob) // 2])
    check("truncated mp4 fails", verify_mp4(trunc)["overall"] == "FAIL")
    empty = os.path.join(tmp, "empty.mp3")
    open(empty, "wb").close()
    check("empty mp3 fails", verify_mp3(empty)["overall"] == "FAIL")


# ---------------- cli ----------------

def main(argv=None):
    ap = argparse.ArgumentParser(description="Phase 2.3 recording file verifier (stdlib only)")
    ap.add_argument("--audio", default=None)
    ap.add_argument("--video", default=None)
    ap.add_argument("--mp4", default=None)
    ap.add_argument("--mp3", default=None)
    ap.add_argument("--expect-audio-samples", type=int, default=None)
    ap.add_argument("--expect-video-frames", type=int, default=None)
    ap.add_argument("--expect-width", type=int, default=None)
    ap.add_argument("--expect-height", type=int, default=None)
    ap.add_argument("--expect-mp4-audio", action="store_true",
                    help="require an audio track inside the mp4")
    ap.add_argument("--min-duration", type=float, default=MIN_AUDIO_DURATION_SEC)
    ap.add_argument("--min-peak", type=float, default=MIN_AUDIO_PEAK)
    ap.add_argument("--json", action="store_true")
    ap.add_argument("--selftest", action="store_true")
    args = ap.parse_args(argv)
    if args.selftest:
        return run_selftest()
    if not args.audio and not args.video and not args.mp4 and not args.mp3:
        ap.print_usage(sys.stderr)
        return 2
    results = []
    if args.audio:
        ra = verify_audio(args.audio, args.expect_audio_samples,
                          args.min_duration, args.min_peak)
        ra["ffmpeg"] = ffmpeg_opinion(args.audio, "audio")
        results.append(ra)
    if args.video:
        if args.video.lower().endswith(".rawvid"):
            rv = verify_rawvid(args.video, args.expect_video_frames,
                               args.expect_width, args.expect_height)
        else:
            rv = verify_video(args.video, args.expect_video_frames,
                              args.expect_width, args.expect_height)
        rv["ffmpeg"] = ffmpeg_opinion(args.video, "video")
        results.append(rv)
    if args.mp4:
        rm = verify_mp4(args.mp4, args.expect_width, args.expect_height,
                        True if args.expect_mp4_audio else None)
        rm["ffmpeg"] = ffmpeg_opinion(args.mp4, "video")
        results.append(rm)
    if args.mp3:
        r3 = verify_mp3(args.mp3)
        r3["ffmpeg"] = ffmpeg_opinion(args.mp3, "audio")
        results.append(r3)
    overall = "PASS" if all(r["overall"] == "PASS" for r in results) else "FAIL"
    if args.json:
        print(json.dumps({"overall": overall, "results": results}, indent=1))
    else:
        for r in results:
            print("== %s: %s (%s)" % (r["medium"], r["overall"], r["path"]))
            for k in ("created", "valid", "decoded", "content", "correct"):
                v = r[k]
                print("   %-8s %-4s %s" % (k, v["result"], v["detail"]))
            if r["info"]:
                print("   info: %s" % json.dumps(r["info"]))
            if r.get("ffmpeg"):
                print("   ffmpeg: %s" % json.dumps(r["ffmpeg"]))
        print("OVERALL " + overall)
    return 0 if overall == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
