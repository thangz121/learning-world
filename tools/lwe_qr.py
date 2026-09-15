#!/usr/bin/env python3
"""LWE QR + LAN helpers for the phone-microphone gateway (stdlib only).

This module has ONE job: let the user connect the phone WITHOUT typing an
IP address, and make every step visible in logs:

  1. detect the PC LAN IP automatically (no `ipconfig` copy-paste),
  2. check the TLS certificate actually covers that IP (else the phone
     will fail at TLS with a confusing error — we fail fast with a fix),
  3. build the page URL (https://<lan-ip>:<port>/) and render it as a
     SCANNABLE QR code (PNG file + SVG + terminal ASCII, all stdlib-only),
  4. expose uniform `[STEP n/N]` log lines used by the gateway and launcher.

QR encoding itself is done by the vendored `qrcodegen.py` (Project Nayuki,
MIT — see the file header). PNG writing uses only `struct` + `zlib`, so
this module works on a fresh machine with zero `pip install`.

Offline-first: everything here runs without internet. No URL is ever sent
anywhere; the QR payload is just the LAN page URL.
"""
import base64
import os
import socket
import struct
import sys
import zlib

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)

from qrcodegen import QrCode  # vendored Nayuki implementation (MIT)

ECL_MAP = {
    "L": QrCode.Ecc.LOW,
    "M": QrCode.Ecc.MEDIUM,
    "Q": QrCode.Ecc.QUARTILE,
    "H": QrCode.Ecc.HIGH,
}


# ---------------------------------------------------------------- QR encode

def encode_url(url, ecl="M"):
    """Encode a URL string as QR (byte mode). Returns a QrCode object."""
    if not url or len(url) > 500:
        raise ValueError("refusing to encode empty/absurd URL (len=%s)" % len(url or ""))
    try:
        level = ECL_MAP[ecl.upper()]
    except KeyError:
        raise ValueError("bad ecl %r (want one of L/M/Q/H)" % ecl)
    return QrCode.encode_binary(url.encode("utf-8"), level)


def matrix_of(qr):
    """QrCode -> list of rows of bool (True = dark)."""
    n = qr.get_size()
    return [[bool(qr.get_module(x, y)) for x in range(n)] for y in range(n)]


# ---------------------------------------------------------------- QR render

def png_bytes(matrix, scale=8, border=4):
    """Render matrix to PNG bytes (8-bit grayscale, stdlib zlib+struct).

    scale  = pixels per QR module. border = quiet-zone width in modules
    (spec minimum is 4 — scanners may fail below it, so never go below 4
    for files; ASCII art may use a smaller visual border).
    """
    if scale < 1 or border < 4:
        raise ValueError("png needs scale>=1 and border>=4 (quiet zone)")
    n = len(matrix)
    side = (n + border * 2) * scale
    white_row = b"\x00" + bytes([255]) * side
    black_run = bytes([0]) * scale
    white_run = bytes([255]) * scale
    raw = bytearray()
    raw += white_row * (border * scale)
    for row in matrix:
        line = bytearray()
        line += white_run * border
        for cell in row:
            line += black_run if cell else white_run
        line += white_run * border
        scan = b"\x00" + bytes(line)
        assert len(line) == side, "scanline width %d != %d" % (len(line), side)
        for _ in range(scale):  # one module row = `scale` pixel rows
            raw += scan
    raw += white_row * (border * scale)

    def chunk(ctype, data):
        c = struct.pack(">I", len(data)) + ctype + data
        return c + struct.pack(">I", zlib.crc32(ctype + data) & 0xFFFFFFFF)

    ihdr = struct.pack(">IIBBBBB", side, side, 8, 0, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr)
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b""))


def save_png(path, matrix, scale=8, border=4):
    data = png_bytes(matrix, scale=scale, border=border)
    with open(path, "wb") as f:
        f.write(data)
    return path, len(data)


def svg_text(matrix, scale=10, border=4, dark="#000000", light="#ffffff"):
    """Render matrix to a standalone SVG string (crisp at any size)."""
    n = len(matrix)
    side = (n + border * 2) * scale
    parts = ['<svg xmlns="http://www.w3.org/2000/svg" width="%d" height="%d" '
             'viewBox="0 0 %d %d" shape-rendering="crispEdges">' % (side, side, side, side)]
    parts.append('<rect width="100%%" height="100%%" fill="%s"/>' % light)
    runs = []
    for y, row in enumerate(matrix):
        x = 0
        while x < n:
            if row[x]:
                x0 = x
                while x < n and row[x]:
                    x += 1
                runs.append('<rect x="%d" y="%d" width="%d" height="%d" fill="%s"/>'
                            % ((x0 + border) * scale, (y + border) * scale,
                               (x - x0) * scale, scale, dark))
            else:
                x += 1
    parts.extend(runs)
    parts.append("</svg>")
    return "".join(parts)


def ascii_art(matrix, border=1):
    """Render matrix for terminals (two chars per module: '##'/spaces).

    Scannability of terminal art depends on font/zoom — it is a FALLBACK
    for reading the URL shape, NOT the primary path. The primary path is
    the PNG file + /qr.png served over HTTPS. Always log the plain URL too.
    """
    dark, light = "##", "  "
    lines = []
    w = len(matrix[0])
    for _ in range(border):
        lines.append(light * (w + border * 2))
    for row in matrix:
        lines.append(light * border + "".join(dark if c else light for c in row) + light * border)
    for _ in range(border):
        lines.append(light * (w + border * 2))
    return "\n".join(lines)


# ---------------------------------------------------------------- LAN IP

def _rank_ip(ip):
    """Lower is better: 192.168.x > 10.x > 172.16-31.x > other."""
    try:
        parts = [int(p) for p in ip.split(".")]
        if len(parts) != 4 or not all(0 <= p <= 255 for p in parts):
            return 99
    except ValueError:
        return 99
    if parts[0] == 192 and parts[1] == 168:
        return 0
    if parts[0] == 10:
        return 1
    if parts[0] == 172 and 16 <= parts[1] <= 31:
        return 2
    if parts[0] == 127 or (parts[0] == 169 and parts[1] == 254):
        return 99
    return 3


def get_lan_candidates():
    """Return deduplicated candidate LAN IPv4 addresses (best first)."""
    found = []

    def add(ip):
        if ip and ip not in found and _rank_ip(ip) < 99:
            found.append(ip)

    try:
        for fam, _, _, _, addr in socket.getaddrinfo(socket.gethostname(), None,
                                                     family=socket.AF_INET,
                                                     type=socket.SOCK_DGRAM):
            add(addr[0])
    except OSError:
        pass
    try:  # default-route probe (UDP connect sends nothing)
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        try:
            s.connect(("8.8.8.8", 80))
            add(s.getsockname()[0])
        finally:
            s.close()
    except OSError:
        pass
    found.sort(key=lambda ip: (_rank_ip(ip), ip))
    return found


def pick_lan_ip(override=""):
    """Return (ip, candidates, reason). Raises RuntimeError if none found."""
    cands = get_lan_candidates()
    if override:
        if _rank_ip(override) >= 99:
            raise RuntimeError("bad --lan-ip %r (not a usable LAN IPv4)" % override)
        return override, cands, "explicit --lan-ip override"
    if not cands:
        raise RuntimeError("no LAN IPv4 found (Wi-Fi off?); pass --lan-ip X explicitly")
    reason = "auto-pick best of %d candidate(s)" % len(cands)
    if len(cands) > 1:
        reason += " (prefer 192.168.x > 10.x > 172.16-31.x)"
    return cands[0], cands, reason


def build_url(ip, port):
    return "https://%s:%d/" % (ip, int(port))


# ---------------------------------------------------------------- cert check

def _der_blocks(cert_path):
    with open(cert_path, "rb") as f:
        pem = f.read()
    blocks = []
    for chunk in pem.split(b"-----BEGIN CERTIFICATE-----")[1:]:
        b64 = chunk.split(b"-----END CERTIFICATE-----")[0]
        try:
            blocks.append(base64.b64decode(b"".join(b64.split())))
        except Exception:
            pass
    return blocks


def cert_covers_ip(cert_path, ip):
    """Best-effort check (stdlib only) that the cert covers this IP.

    Returns (ok, detail). ok=True if the IP appears as CN text or as a
    SAN iPAddress entry (tag 0x87 len 4 + 4 octets) in any PEM block.
    Heuristic, but the failure mode is a loud FAIL log with the exact fix,
    never a silent TLS breakdown on the phone.
    """
    try:
        octets = bytes(int(p) for p in ip.split("."))
        if len(octets) != 4:
            raise ValueError("bad ip")
    except ValueError:
        return False, "invalid ip %r" % ip
    try:
        blocks = _der_blocks(cert_path)
    except OSError as e:
        return False, "cannot read cert %s (%s)" % (cert_path, e)
    if not blocks:
        return False, "no PEM certificate block found in %s" % cert_path
    san_needle = b"\x87\x04" + octets
    found_cn = any(ip.encode("ascii") in der for der in blocks)
    found_san = any(san_needle in der for der in blocks)
    if found_cn or found_san:
        where = "+".join(w for w, f in (("CN", found_cn), ("SAN", found_san)) if f)
        return True, "cert covers %s (seen in %s)" % (ip, where)
    return False, ("cert does NOT cover %s (no CN/SAN match) — phone TLS will FAIL; "
                   "re-issue the cert for this IP or start with --lan-ip of the cert IP" % ip)


# ---------------------------------------------------------------- step log

def step_line(step, total, msg):
    return "[STEP %d/%d] %s" % (step, total, msg)


def selftest():
    fails = []

    def check(name, cond, extra=""):
        print(("PASS " if cond else "FAIL ") + name + (" — " + extra if extra and not cond else ""))
        if not cond:
            fails.append(name)

    try:
        import cv2  # test-oracle only, never a runtime dependency
        have_cv2 = True
    except ImportError:
        have_cv2 = False
    try:
        from PIL import Image  # noqa
        have_pil = True
    except ImportError:
        have_pil = False

    for url in ("https://192.168.50.90:8443/", "https://10.0.0.5:8443/", "https://192.168.1.25:8443/"):
        qr = encode_url(url)
        m = matrix_of(qr)
        n = len(m)
        check("qr-square %s" % url, all(len(r) == n for r in m) and n >= 21, "n=%s" % n)
        png = png_bytes(m)
        check("qr-png-magic %s" % url, png[:8] == b"\x89PNG\r\n\x1a\n")
        if have_cv2:
            import numpy as np
            arr = np.frombuffer(png, dtype=np.uint8)
            img = cv2.imdecode(arr, cv2.IMREAD_GRAYSCALE)
            val, _, _ = cv2.QRCodeDetector().detectAndDecode(img)
            check("qr-decode %s" % url, val == url, "got %r" % val)
        else:
            print("SKIP qr-decode (no opencv)")
    if not have_cv2:
        print("NOTE: install opencv-python to enable decode proofs")
    _ = have_pil
    cands = get_lan_candidates()
    check("lan-candidates", isinstance(cands, list), repr(cands))
    print("LAN candidates now: %s" % (cands or ["(none)"]))
    svg = svg_text(matrix_of(encode_url("https://192.168.50.90:8443/")))
    check("qr-svg", svg.startswith("<svg") and svg.endswith("</svg>"))
    art = ascii_art(matrix_of(encode_url("https://192.168.50.90:8443/")))
    check("qr-ascii", "##" in art and len(art.splitlines()) > 20)
    print("SELFTEST %s (%d fails)" % ("OK" if not fails else "FAILED", len(fails)))
    return 1 if fails else 0


if __name__ == "__main__":
    sys.exit(selftest())
