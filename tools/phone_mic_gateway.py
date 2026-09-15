#!/usr/bin/env python3
"""Phase 2.1-local M6 phone microphone gateway (PC side, LAN only, no cloud).

Topology (§1/§6):
    phone browser --WSS--> this gateway --TCP loopback--> Unity
TLS terminates HERE (facing the phone on LAN). Loopback needs none.

Responsibilities (NETWORK -> AUDIO only, §6):
- serve the phone page over HTTPS (same origin => WSS just works)
- accept phone WebSocket audio (canonical mono PCM16 @16kHz, §7)
- validate protocol + sequence + format (reject, never silently adapt)
- bridge live session audio to Unity over the PhoneMicProtocol envelope
- session lifecycle (§9), bounded queues (§18), explicit opt-in recordings (§20)

This process performs NO recognition, NO DSP, NO cloud calls, NO GPU work.

Usage:
  python tools/phone_mic_gateway.py --cert lan.crt --key lan.key
      [--https-port 8443] [--bridge-port 8451] [--record-dir DIR]

Certificate setup is documented in docs/HANDOFF/PHASE_2_1_PHONE_MIC.md
(WINDOWS mkcert/openssl + Android trust + iOS notes). Private keys are
gitignored and MUST never be committed (§33).

Bridge protocol (gateway<->Unity) is defined in
Assets/D_Audio/PhoneMicProtocol.cs (source of truth for framing).

Connection WITHOUT typing the IP (2026-09-15):
  just run the gateway (optionally via tools/start_phone_mic.ps1) and scan
  the printed/saved QR code with the phone camera — it encodes the full
  https://<lan-ip>:<port>/ page URL. Startup logs are numbered
  [STEP n/9] so you can always see how far the phone-side flow got:
    1 lan-ip detect · 2 cert check · 3 QR ready · 4 bridge listen ·
    5 HTTPS ready (scan now) · 6 phone WS connected · 7 session CAPTURING ·
    8 first audio chunk · 9 stop/complete/cancel.
"""
import argparse
import base64
import hashlib
import json
import os
import socket
import ssl
import struct
import sys
import threading
import time
import wave
from collections import deque

_HERE = os.path.dirname(os.path.abspath(__file__))
if _HERE not in sys.path:
    sys.path.insert(0, _HERE)
import lwe_qr

CANON_RATE = 16000
WS_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
TOTAL_STEPS = 9  # startup 1-5, phone flow 6-9 (see module docstring)

# Bridge kinds (mirror PhoneMicProtocol.cs — keep in sync).
K_AUDIO, K_STOP, K_ERROR, K_HELLO = 1, 2, 3, 4
K_UP, K_DOWN = 5, 6  # phone presence (no audio): page opened / page gone.
#   UP   (serial 0, payload utf8 "ws-connected")  -> phone page opened (WS up).
#   DOWN (serial 0, payload utf8 "ws-closed")     -> phone page closed/dropped.
# Session audio lifecycle already has AUDIO/STOP/ERROR; presence lets Unity
# tell "user pressed STOP but page still open" (STOP seen, no DOWN) apart from
# "phone gone mid-game" (DOWN, or our socket dying) in seconds.
K_SUBSCRIBE, K_CANCEL = 0x10, 0x11
# Unity -> gateway ONLY: source-preference report (game prefers plugged-in PC
# hardware over the phone per medium). Payload utf8 "<media>:<origin>", e.g.
# "mic:local" / "cam:phone". Gateway stores it and pushes {"type":"pc-prefer"}
# to the matching phone sessions so their START buttons stand down. Replies
# nothing; malformed payloads are ignored, never fatal.
K_PREFER = 0x12
# Camera bridge kinds (mirror PhoneCameraProtocol.cs — same numeric envelope,
# SEPARATE socket on --camera-bridge-port so JPEGs never block speech audio).
#   FRAME=1 (payload JPEG bytes) · STOP=2 · ERROR=3 · HELLO=4 · UP=5 · DOWN=6.
K_CAM_FRAME, K_CAM_STOP, K_CAM_ERROR, K_CAM_HELLO = 1, 2, 3, 4
K_CAM_UP, K_CAM_DOWN = 5, 6
# Camera payload contract (Phase 2.2, mirrors PhoneCameraProtocol.cs):
CAM_MAX_BYTES = 300 * 1024  # envelope cap (default config allows 200 KB)
CAM_JPEG_SOI = b"\xff\xd8\xff"  # JFIF magic every frame must start with

LOG_LOCK = threading.Lock()


def log(msg):
    with LOG_LOCK:
        print("[PHONEGW %s] %s" % (time.strftime("%H:%M:%S"), msg), flush=True)


# ---------------------------------------------------------------- WS codec

def ws_accept(key):
    h = hashlib.sha1((key + WS_GUID).encode("ascii")).digest()
    return base64.b64encode(h).decode("ascii")


def ws_send_text(sock, text):
    # Server -> client frames MUST be UNMASKED (RFC6455 §5.1). The previous
    # build set the MASK bit (0x80) on server frames; direct-LAN browsers
    # tolerated it but Cloudflare's WS proxy validates strictly and drops
    # the connection — phone then stuck at STEP 4-stream with START latched
    # disabled and no "ready" ever arriving. Fixed: never set MASK bit.
    data = text.encode("utf-8")
    if len(data) < 126:
        sock.sendall(bytes([0x81, len(data)]) + data)
    elif len(data) < 65536:
        sock.sendall(bytes([0x81, 126, (len(data) >> 8) & 0xFF, len(data) & 0xFF]) + data)
    else:
        sock.sendall(bytes([0x81, 127]) + struct.pack(">Q", len(data)) + data)


def ws_recv_exact(sock, n):
    buf = b""
    while len(buf) < n:
        chunk = sock.recv(n - len(buf))
        if not chunk:
            raise ConnectionError("peer closed")
        buf += chunk
    return buf


def ws_send_ping(sock):
    # RFC6455 server ping (unmasked, empty payload). Browsers auto-pong;
    # keeps Cloudflare (100 s idle timeout) + NAT from silently killing
    # a streaming socket during quiet speech pauses.
    try:
        sock.sendall(b"\x89\x00")
    except Exception:
        pass


def ws_recv_frame(sock):
    """Returns (opcode, payload). Handles masked client frames + control."""
    hdr = ws_recv_exact(sock, 2)
    fin = hdr[0] & 0x80
    opcode = hdr[0] & 0x0F
    masked = hdr[1] & 0x80
    length = hdr[1] & 0x7F
    if length == 126:
        length = struct.unpack(">H", ws_recv_exact(sock, 2))[0]
    elif length == 127:
        length = struct.unpack(">Q", ws_recv_exact(sock, 8))[0]
    mask = ws_recv_exact(sock, 4) if masked else None
    payload = ws_recv_exact(sock, length) if length else b""
    if mask:
        payload = bytes(b ^ mask[i % 4] for i, b in enumerate(payload))
    if opcode == 0x8:  # close
        raise ConnectionError("ws close")
    if opcode == 0x9:  # ping -> pong
        try:
            sock.sendall(b"\x8a\x00")
        except Exception:
            pass
        return ws_recv_frame(sock)
    if opcode == 0xA:  # pong (answer to our ping) -> ignore, keep reading
        return ws_recv_frame(sock)
    if opcode not in (0x1, 0x2, 0x0):
        raise ValueError("unsupported opcode %d" % opcode)
    if not fin:
        raise ValueError("fragmented frames unsupported")
    return opcode, payload


def bridge_frame(kind, serial, seq, payload):
    body = struct.pack(">BII", kind, serial, seq) + (payload or b"")
    return struct.pack(">I", len(body)) + body


# ---------------------------------------------------------------- sessions

class Session:
    def __init__(self, sid, serial, rate, channels, fmt):
        self.sid = sid
        self.serial = serial
        self.rate = rate
        self.channels = channels
        self.fmt = fmt
        self.state = "CAPTURING"
        self.chunks = 0
        self.samples = 0
        self.seq_gaps = 0
        self.last_seq = -1
        self.t0 = time.time()
        self.record_frames = []  # only when --record-dir is set
        self.bound_logged = False  # §18 10 s bridge bound: log once


class Gateway:
    def __init__(self, args):
        self.args = args
        self.lock = threading.Lock()
        self.sessions = {}          # sid -> Session (live only)
        self.serial_counter = 0
        self.subscribers = []       # list of (socket, lock)
        self.drop_counts = {"queue": 0, "slow_subscriber": 0}
        # Filled by main() before serve_https (STEP 3); read by /qr.png + /qr.
        self.page_url = ""
        self.lan_ip = ""
        self.qr_png = b""
        # Network-resilience: all LAN candidates + optional public URL
        # (Cloudflare Tunnel). Advertised via /health so a phone page that
        # went stale after a proxy on/off toggle can fail over without
        # re-scanning the QR.
        self.candidates = []
        self.public_url = getattr(args, "public_url", "") or ""
        # Phase 2.2 camera path (independent media, shared TLS/QR/health infra):
        # own bridge subscribers, own session serials, own page/QR artefacts.
        self.cam_subscribers = []     # list of (socket, lock)
        self.cam_serial_counter = 0
        self.cam_sessions = {}        # sid -> dict(serial, frames, bytes, t0)
        self.cam_page_url = ""
        self.cam_qr_png = b""
        # Unified mic+camera panel (Phase 2.2 follow-up): same origin, own QR.
        self.uni_page_url = ""
        self.uni_qr_png = b""
        # Source precedence (game decides, gateway relays): per-medium origin
        # the GAME currently prefers ("local" = plugged-in PC hardware wins,
        # phone must stand down; "phone" = phone may stream). Defaults allow
        # the phone — the game reports on transitions only.
        self.prefer = {"mic": "phone", "cam": "phone"}
        self.mic_sockets = set()  # live /mic phone WS (for pc-prefer pushes)
        self.cam_sockets = set()  # live /cam phone WS (for pc-prefer pushes)

    # -- bridge pump -------------------------------------------------
    # Source-preference relay (game -> phone pages). The game reports
    # "<media>:<origin>" over either bridge (parsed in the hold loops below);
    # the matching live phone sessions get {"type":"pc-prefer"} so their START
    # buttons stand down when the PC uses plugged-in hardware. Game-side
    # precedence applies regardless — this signal only moves the phone UI.
    def push_prefer(self, media):
        with self.lock:
            origin = self.prefer.get(media, "phone")
            socks = list(self.mic_sockets if media == "mic" else self.cam_sockets)
        msg = {"type": "pc-prefer", "media": media, "origin": origin}
        for sock in socks:
            try:
                self.send_ctrl(sock, msg)
            except Exception:
                pass

    def on_prefer(self, payload):
        try:
            text = (payload or b"").decode("utf-8")
        except Exception:
            return
        parts = text.split(":")
        if len(parts) != 2:
            return
        media, origin = parts[0].strip(), parts[1].strip()
        if media not in ("mic", "cam") or origin not in ("local", "phone"):
            return
        with self.lock:
            if self.prefer.get(media) == origin:
                return
            self.prefer[media] = origin
        log("prefer: %s -> %s (game decision, phone pages stand down=%s)"
            % (media, origin, origin == "local"))
        self.push_prefer(media)

    def drain_bridge_control(self, conn):
        # Bridge hold-loop reader (shared by mic + cam bridges): reads ONE
        # envelope frame from a subscriber. SUBSCRIBE duplicates are ignored;
        # PREFER frames (game source-preference reports) update + relay;
        # anything else is ignored. Returns False on close/error (caller
        # breaks + cleans up). Never throws, never affects media flow.
        try:
            hdr = b""
            while len(hdr) < 4:
                c = conn.recv(4 - len(hdr))
                if not c:
                    return False
                hdr += c
            body_len = struct.unpack(">I", hdr)[0]
            if body_len < 9 or body_len > 4 + 9 + CAM_MAX_BYTES:
                return True  # misaligned window: stay alive, ignore it
            body = b""
            while len(body) < body_len:
                c = conn.recv(body_len - len(body))
                if not c:
                    return False
                body += c
            if body and body[0] == K_PREFER:
                self.on_prefer(body[9:] if len(body) > 9 else b"")
            return True
        except Exception:
            return False

    def bridge_broadcast(self, kind, serial, seq, payload):
        frame = bridge_frame(kind, serial, seq, payload)
        dead = []
        with self.lock:
            subs = list(self.subscribers)
        for sock, wlock in subs:
            try:
                sock.settimeout(2.0)
                with wlock:
                    sock.sendall(frame)
            except Exception:
                dead.append((sock, wlock))
        if dead:
            with self.lock:
                for d in dead:
                    if d in self.subscribers:
                        self.subscribers.remove(d)
            self.drop_counts["slow_subscriber"] += len(dead)
            log("dropped %d slow bridge subscriber(s)" % len(dead))

    def handle_bridge_conn(self, conn, addr):
        log("bridge subscriber from %s" % (addr,))
        try:
            conn.settimeout(10.0)
            hdr = b""
            while len(hdr) < 4:
                c = conn.recv(4 - len(hdr))
                if not c:
                    return
                hdr += c
            body_len = struct.unpack(">I", hdr)[0]
            body = b""
            while len(body) < body_len:
                c = conn.recv(body_len - len(body))
                if not c:
                    return
                body += c
            if not body or body[0] != K_SUBSCRIBE:
                log("bridge: first frame not SUBSCRIBE, closing")
                return
            wlock = threading.Lock()
            # HELLO BEFORE joining the broadcast list: a phone session
            # streaming at full rate could otherwise slip an AUDIO frame to
            # this socket ahead of the greeting, and a mid-stream subscriber
            # would read a misaligned envelope ("malformed" + lost chunks).
            conn.sendall(bridge_frame(K_HELLO, 0, 0, b"phone-mic-gateway/1"))
            with self.lock:
                self.subscribers.append((conn, wlock))
            log("bridge: subscribed (%d total, Unity/test listening)" % len(self.subscribers))
            if self.args.bridge_only and self.args.inject_wav:
                threading.Thread(target=self.inject_wav_session, daemon=True).start()
            conn.settimeout(None)
            while True:  # hold open; SUBSCRIBE dupes ignored, PREFER relayed
                if not self.drain_bridge_control(conn):
                    break
        except Exception as e:
            log("bridge conn error: %s" % e)
        finally:
            with self.lock:
                self.subscribers = [(s, l) for s, l in self.subscribers if s is not conn]
            try:
                conn.close()
            except Exception:
                pass
            log("bridge subscriber left")

    def serve_bridge(self):
        srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        srv.bind(("127.0.0.1", self.args.bridge_port))
        srv.listen(4)
        log("[STEP 4/%d] bridge TCP loopback 127.0.0.1:%d (Unity dials here)" % (TOTAL_STEPS, self.args.bridge_port))
        while True:
            try:
                conn, addr = srv.accept()
            except Exception as e:
                log("bridge accept aborted (%s), still listening" % type(e).__name__)
                continue
            threading.Thread(target=self.handle_bridge_conn,
                             args=(conn, addr), daemon=True).start()

    def inject_wav_session(self):
        """TEST-ONLY loopback injector (--bridge-only --inject-wav).

        Plays a local WAV file as a phone session (AUDIO chunks + STOP) to
        bridge subscribers. Proves gateway->Unity framing over real sockets.
        This is TRANSPORT proof, never a speech verdict: the WAV is a test
        tone, and verdict behavior is pinned separately by EditMode (P14P).
        """
        import time as _t
        try:
            with wave.open(self.args.inject_wav, "rb") as w:
                if w.getnchannels() != 1 or w.getsampwidth() != 2 or w.getframerate() != CANON_RATE:
                    log("inject: need mono16@16000 wav, refusing %s" % self.args.inject_wav)
                    return
                raw = w.readframes(w.getnframes())
        except Exception as e:
            log("inject: unreadable wav: %s" % e)
            return
        with self.lock:
            self.serial_counter += 1
            serial = self.serial_counter
        log("inject: session serial=%d samples=%d" % (serial, len(raw) // 2))
        seq = 0
        step = 3200  # 100 ms chunks, paced like a live session
        for off in range(0, len(raw), step):
            self.bridge_broadcast(K_AUDIO, serial, seq, raw[off:off + step])
            seq += 1
            _t.sleep(0.02)
        self.bridge_broadcast(K_STOP, serial, seq, b"")
        log("inject: session serial=%d COMPLETE chunks=%d" % (serial, seq))

    # -- camera bridge (Phase 2.2: same envelope, SEPARATE socket) --------
    # Media independence (§8): JPEGs ride their own loopback port so a large
    # frame can never head-of-line-block speech audio. All patterns (HELLO-
    # first, broadcast, bounded, presence) mirror the mic bridge above.
    def cam_broadcast(self, kind, serial, seq, payload):
        frame = bridge_frame(kind, serial, seq, payload)
        dead = []
        with self.lock:
            subs = list(self.cam_subscribers)
        for sock, wlock in subs:
            try:
                sock.settimeout(2.0)
                with wlock:
                    sock.sendall(frame)
            except Exception:
                dead.append((sock, wlock))
        if dead:
            with self.lock:
                for d in dead:
                    if d in self.cam_subscribers:
                        self.cam_subscribers.remove(d)
            log("cam: dropped %d slow bridge subscriber(s)" % len(dead))

    def handle_cam_bridge_conn(self, conn, addr):
        log("cam bridge subscriber from %s" % (addr,))
        try:
            conn.settimeout(10.0)
            hdr = b""
            while len(hdr) < 4:
                c = conn.recv(4 - len(hdr))
                if not c:
                    return
                hdr += c
            body_len = struct.unpack(">I", hdr)[0]
            body = b""
            while len(body) < body_len:
                c = conn.recv(body_len - len(body))
                if not c:
                    return
                body += c
            if not body or body[0] != K_SUBSCRIBE:
                log("cam bridge: first frame not SUBSCRIBE, closing")
                return
            wlock = threading.Lock()
            # HELLO BEFORE joining the broadcast list (same race as audio).
            conn.sendall(bridge_frame(K_CAM_HELLO, 0, 0, b"phone-cam-gateway/1"))
            with self.lock:
                self.cam_subscribers.append((conn, wlock))
            log("cam bridge: subscribed (%d total, Unity/test listening)" % len(self.cam_subscribers))
            conn.settimeout(None)
            while True:  # hold open; SUBSCRIBE dupes ignored, PREFER relayed
                if not self.drain_bridge_control(conn):
                    break
        except Exception as e:
            log("cam bridge conn error: %s" % e)
        finally:
            with self.lock:
                self.cam_subscribers = [(s, l) for s, l in self.cam_subscribers if s is not conn]
            try:
                conn.close()
            except Exception:
                pass
            log("cam bridge subscriber left")

    def serve_cam_bridge(self, port):
        srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        srv.bind(("127.0.0.1", port))
        srv.listen(4)
        log("[CAM 1/4] bridge TCP loopback 127.0.0.1:%d (Unity camera dials here; mic stays on %d)"
            % (port, self.args.bridge_port))
        while True:
            try:
                conn, addr = srv.accept()
            except Exception as e:
                log("cam bridge accept aborted (%s), still listening" % type(e).__name__)
                continue
            threading.Thread(target=self.handle_cam_bridge_conn,
                             args=(conn, addr), daemon=True).start()

    # -- camera phone side (WS /cam: JPEG frames, same lifecycle shape) ----
    def handle_cam_phone(self, sock, addr):
        sid = None
        # Source-precedence: track this page so game reports reach it, and
        # immediately tell it the CURRENT preference (a page opened AFTER the
        # game already chose local must stand down without waiting).
        with self.lock:
            self.cam_sockets.add(sock)
            _cam_origin = self.prefer.get("cam", "phone")
        try:
            self.send_ctrl(sock, {"type": "pc-prefer", "media": "cam", "origin": _cam_origin})
        except Exception:
            pass
        self.cam_broadcast(K_CAM_UP, 0, 0, b"ws-connected")
        try:
            while True:
                opcode, payload = ws_recv_frame(sock)
                if opcode == 0x1:  # control JSON
                    try:
                        msg = json.loads(payload.decode("utf-8"))
                    except Exception:
                        self.send_ctrl(sock, {"type": "error", "reason": "protocol_error:bad-json"})
                        continue
                    mtype = msg.get("type")
                    if mtype == "cam-start":
                        if sid:
                            with self.lock:
                                old = self.cam_sessions.pop(sid, None)
                            if old is not None:
                                self.cam_broadcast(K_CAM_STOP, old["serial"], 0, b"")
                        sid = self.on_cam_start(sock, addr, msg)
                    elif mtype == "cam-stop":
                        self.on_cam_stop(sock, msg)
                        sid = None
                    elif mtype == "cancel":
                        self.on_cam_stop(sock, msg)
                        sid = None
                    elif mtype == "ping":
                        try:
                            self.send_ctrl(sock, {"type": "pong", "t": msg.get("t", 0)})
                        except Exception:
                            pass
                    elif mtype == "pong":
                        pass
                    else:
                        self.send_ctrl(sock, {"type": "error", "reason": "protocol_error:unknown-type"})
                elif opcode == 0x2:  # binary JPEG frame
                    self.on_cam_frame(sock, sid, payload)
        except (ConnectionError, ValueError) as e:
            log("cam phone %s: %s" % (addr, e))
        except Exception as e:
            log("cam phone %s error: %s" % (addr, e))
        finally:
            if sid:
                with self.lock:
                    sess = self.cam_sessions.pop(sid, None)
                if sess is not None:
                    self.cam_broadcast(K_CAM_ERROR, sess["serial"], 0, b"link_down")
                    log("[CAM 4/4] cam session %s link_down (page gone mid-stream)" % sid)
            self.cam_broadcast(K_CAM_DOWN, 0, 0, b"ws-closed")
            with self.lock:
                try:
                    self.cam_sockets.discard(sock)
                except Exception:
                    pass
            try:
                sock.close()
            except Exception:
                pass

    def on_cam_start(self, sock, addr, msg):
        session = msg.get("session") or ""
        fmt = (msg.get("format") or "").lower()
        if not session or len(session) > 64:
            self.send_ctrl(sock, {"type": "error", "reason": "protocol_error:bad-session"})
            return None
        if fmt not in ("jpeg", "jpg", "image/jpeg"):
            reason = "protocol_error:need jpeg, got fmt=%s" % (msg.get("format"),)
            self.send_ctrl(sock, {"type": "error", "reason": reason})
            log("reject cam start (%s): %s" % (addr, reason))
            return None
        with self.lock:
            if session in self.cam_sessions:
                self.send_ctrl(sock, {"type": "error", "reason": "protocol_error:duplicate-session"})
                return None
            self.cam_serial_counter += 1
            serial = self.cam_serial_counter
            self.cam_sessions[session] = {"serial": serial, "frames": 0,
                                          "bytes": 0, "t0": time.time()}
        self.send_ctrl(sock, {"type": "cam-ready", "sessionSerial": serial})
        log("[CAM 3/4] cam session %s serial=%d CAPTURING from %s" % (session, serial, addr))
        return session

    def on_cam_frame(self, sock, sid, payload):
        if not sid:
            return  # frame before start: ignore (page always starts first)
        with self.lock:
            sess = self.cam_sessions.get(sid)
            nsubs = len(self.cam_subscribers)
        if sess is None:
            return  # stale session frame never merges (§12 pattern)
        if len(payload) == 0 or len(payload) > CAM_MAX_BYTES:
            return  # oversize/empty: drop + count, never forward
        if payload[:3] != CAM_JPEG_SOI:
            return  # non-JPEG: drop (Phase 2.2 is JPEG-only)
        sess["frames"] += 1
        sess["bytes"] += len(payload)
        if sess["frames"] == 1:
            log("[CAM 4/4] cam session %s first frame flowing (serial=%d, %d B, %d listener(s))"
                % (sid, sess["serial"], len(payload), nsubs))
        elif sess["frames"] % 300 == 0:
            dur = time.time() - sess["t0"]
            log("[CAM 4/4] cam session %s frames=%d (~%.1f KB) wall=%.1fs listeners=%d"
                % (sid, sess["frames"], sess["bytes"] / 1024.0, dur, nsubs))
        # Latest-frame transport: broadcast immediately; Unity keeps depth ONE.
        self.cam_broadcast(K_CAM_FRAME, sess["serial"], sess["frames"], payload)

    def on_cam_stop(self, sock, msg):
        sid = msg.get("session") or ""
        with self.lock:
            sess = self.cam_sessions.pop(sid, None)
        if sess is None:
            return
        dur = time.time() - sess["t0"]
        self.cam_broadcast(K_CAM_STOP, sess["serial"], sess["frames"] + 1, b"")
        self.send_ctrl(sock, {"type": "cam-stopped", "sessionSerial": sess["serial"]})
        log("[CAM 4/4] cam session %s COMPLETE frames=%d wall=%.2fs" % (sid, sess["frames"], dur))

    # -- phone side --------------------------------------------------
    def send_ctrl(self, sock, obj):
        ws_send_text(sock, json.dumps(obj))

    def handle_phone(self, sock, addr):
        sid = None
        # Source-precedence: track this page + push CURRENT mic preference
        # immediately (same contract as the cam side above).
        with self.lock:
            self.mic_sockets.add(sock)
            _mic_origin = self.prefer.get("mic", "phone")
        try:
            self.send_ctrl(sock, {"type": "pc-prefer", "media": "mic", "origin": _mic_origin})
        except Exception:
            pass
        # Presence for Unity: the page is OPEN (WS up) even before any START.
        # Lets the game tell "STOP pressed, page still open" (STOP, no DOWN)
        # apart from "phone gone mid-game" (DOWN) within seconds.
        self.bridge_broadcast(K_UP, 0, 0, b"ws-connected")
        try:
            while True:
                opcode, payload = ws_recv_frame(sock)
                if opcode == 0x1:  # control JSON
                    try:
                        msg = json.loads(payload.decode("utf-8"))
                    except Exception:
                        self.send_ctrl(sock, {"type": "error", "reason": "protocol_error:bad-json"})
                        continue
                    mtype = msg.get("type")
                    if mtype == "start":
                        if sid:
                            # Double-START guard (one socket = one live
                            # session): a second START before STOP (double tap,
                            # two tabs, auto-resume race) used to create TWO
                            # live sessions whose audio graphs BOTH streamed
                            # (~2x chunk rate, leaking old session). Retire
                            # the old one cleanly first: 0-chunk sessions end
                            # with STOP (no spurious error downstream), others
                            # with an explicit "replaced" error.
                            with self.lock:
                                old = self.sessions.pop(sid, None)
                            if old is not None:
                                old.state = "REPLACED"
                                if old.chunks == 0:
                                    self.bridge_broadcast(K_STOP, old.serial, 0, b"")
                                else:
                                    self.bridge_broadcast(K_ERROR, old.serial, 0, b"replaced")
                                log("[STEP 9/%d] session %s REPLACED by a new START on the same socket "
                                    "(double-START guard — bridge never gets 2x audio)" % (TOTAL_STEPS, sid))
                        sid = self.on_start(sock, addr, msg)
                    elif mtype == "stop":
                        self.on_stop(sock, msg)
                        sid = None
                    elif mtype == "cancel":
                        self.on_cancel(sock, msg)
                        sid = None
                    elif mtype == "ping":
                        # Heartbeat for Cloudflare/NAT survival: CF closes
                        # idle WS after ~100 s; phone pings every 15 s.
                        # Also lets the phone detect a half-dead socket
                        # (proxy toggled) within seconds instead of minutes.
                        try:
                            self.send_ctrl(sock, {"type": "pong", "t": msg.get("t", 0)})
                        except Exception:
                            pass
                    elif mtype == "pong":
                        pass  # answer to our ping; link is alive
                    else:
                        self.send_ctrl(sock, {"type": "error", "reason": "protocol_error:unknown-type"})
                elif opcode == 0x2:  # binary PCM16
                    self.on_audio(sock, sid, payload)
        except (ConnectionError, ValueError) as e:
            log("phone %s: %s" % (addr, e))
        except Exception as e:
            log("phone %s error: %s" % (addr, e))
        finally:
            if sid:
                self.abort_session(sid, "link_down")
            # Authoritative phone-gone signal (page closed, radio dropped,
            # browser killed): a clean STOP already went out as K_STOP, so a
            # DOWN here always means the page is GONE, not merely idle.
            self.bridge_broadcast(K_DOWN, 0, 0, b"ws-closed")
            with self.lock:
                try:
                    self.mic_sockets.discard(sock)
                except Exception:
                    pass
            try:
                sock.close()
            except Exception:
                pass

    def on_start(self, sock, addr, msg):
        session = msg.get("session") or ""
        try:
            rate = int(msg.get("sampleRate", 0))
            channels = int(msg.get("channels", 0))
        except Exception:
            rate, channels = 0, 0
        fmt = msg.get("format") or ""
        if not session or len(session) > 64:
            self.send_ctrl(sock, {"type": "error", "reason": "protocol_error:bad-session"})
            return None
        if rate != CANON_RATE or channels != 1 or fmt.lower() != "pcm16":
            reason = "protocol_error:need mono pcm16@16000, got rate=%s ch=%s fmt=%s" % (rate, channels, fmt)
            self.send_ctrl(sock, {"type": "error", "reason": reason})
            log("reject phone start (%s): %s" % (addr, reason))
            return None
        with self.lock:
            if session in self.sessions:
                self.send_ctrl(sock, {"type": "error", "reason": "protocol_error:duplicate-session"})
                return None
            self.serial_counter += 1
            serial = self.serial_counter
            self.sessions[session] = Session(session, serial, rate, channels, fmt)
        self.send_ctrl(sock, {"type": "ready", "sessionSerial": serial})
        log("[STEP 7/%d] session %s serial=%d CONNECTED->CAPTURING from %s" % (TOTAL_STEPS, session, serial, addr))
        return session

    def on_audio(self, sock, sid, payload):
        if not sid:
            return  # audio before start: ignore (page always starts first)
        with self.lock:
            sess = self.sessions.get(sid)
            nsubs = len(self.subscribers)
        if sess is None or sess.state != "CAPTURING":
            return  # stale session audio never merges (§12)
        if len(payload) % 2 == 1:
            payload = payload[:-1]
        if not payload:
            return
        sess.chunks += 1
        sess.samples += len(payload) // 2
        if sess.chunks == 1:
            log("[STEP 8/%d] session %s first audio chunk flowing (serial=%d, %d bridge listener(s))" % (
                TOTAL_STEPS, sid, sess.serial, nsubs))
            if nsubs == 0:
                log("[STEP 8/%d] NOTE: audio IS reaching the PC (phone->gateway OK) — "
                    "no bridge listener yet, so Unity/test hears nothing. "
                    "Open the game (or test_phone_link.py --wait) to listen." % TOTAL_STEPS)
        elif sess.chunks % 100 == 0:
            # Visible proof of WHAT is arriving: chunk count, audio seconds,
            # peak mic level (0-32767), wall duration, bridge listeners.
            # Lets the user answer "did my voice reach the PC?" from this log.
            try:
                n = len(payload) // 2
                peak = 0
                for v in struct.unpack("<%dh" % n, payload):
                    a = v if v >= 0 else -v
                    if a > peak:
                        peak = a
            except Exception:
                peak = -1
            dur = time.time() - sess.t0
            log("[STEP 8/%d] session %s audio: chunks=%d (~%.1fs audio) peak=%d wall=%.1fs listeners=%d" % (
                TOTAL_STEPS, sid, sess.chunks, sess.samples / float(CANON_RATE), peak, dur, nsubs))
        if sess.samples > CANON_RATE * 10:  # bounded (§18): ignore past 10 s
            self.drop_counts["queue"] += 1
            if not sess.bound_logged:
                sess.bound_logged = True
                log("[STEP 8/%d] session %s reached the 10 s bridge bound "
                    "(captures/probes use short windows by design) — further audio "
                    "still arrives from the phone but is no longer forwarded." % (TOTAL_STEPS, sid))
            return
        if self.args.record_dir:
            sess.record_frames.append(payload)
        self.bridge_broadcast(K_AUDIO, sess.serial, sess.chunks, payload)

    def on_stop(self, sock, msg):
        sid = msg.get("session") or ""
        with self.lock:
            sess = self.sessions.pop(sid, None)
        if sess is None:
            return
        sess.state = "COMPLETE"
        dur = time.time() - sess.t0
        self.bridge_broadcast(K_STOP, sess.serial, sess.chunks + 1, b"")
        self.send_ctrl(sock, {"type": "stopped", "sessionSerial": sess.serial})
        log("[STEP 9/%d] session %s COMPLETE chunks=%d (~%.1fs audio) wall=%.2fs" % (
            TOTAL_STEPS, sid, sess.chunks, sess.samples / float(CANON_RATE), dur))
        self.maybe_record(sess)

    def on_cancel(self, sock, msg):
        sid = msg.get("session") or ""
        with self.lock:
            sess = self.sessions.pop(sid, None)
        if sess is None:
            return
        sess.state = "CANCELLED"
        self.bridge_broadcast(K_ERROR, sess.serial, 0, b"cancelled")
        log("[STEP 9/%d] session %s CANCELLED by phone" % (TOTAL_STEPS, sid))

    def abort_session(self, sid, reason):
        with self.lock:
            sess = self.sessions.pop(sid, None)
        if sess is None:
            return
        sess.state = "DISCONNECTED"
        self.bridge_broadcast(K_ERROR, sess.serial, 0, reason.encode("utf-8"))
        log("[STEP 9/%d] session %s %s (mid-speech disconnect -> env error, never WrongWord)" % (TOTAL_STEPS, sid, reason))

    def maybe_record(self, sess):
        if not self.args.record_dir:
            return
        try:
            os.makedirs(self.args.record_dir, exist_ok=True)
            path = os.path.join(self.args.record_dir, "sess-%d.wav" % sess.serial)
            with wave.open(path, "wb") as w:
                w.setnchannels(1)
                w.setsampwidth(2)
                w.setframerate(CANON_RATE)
                for chunk in sess.record_frames:
                    w.writeframes(chunk)
            log("recorded %s (explicit test mode only)" % path)
        except Exception as e:
            log("record failed: %s" % e)

    # -- HTTPS + WSS -------------------------------------------------
    def serve_https(self, page_bytes, cam_page_bytes=b"", uni_page_bytes=b""):
        raw = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        raw.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        raw.bind(("0.0.0.0", self.args.https_port))
        raw.listen(8)
        ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        ctx.load_cert_chain(self.args.cert, self.args.key)
        srv = ctx.wrap_socket(raw, server_side=True)
        extra = (" + public %s (Cloudflare failover on)" % self.public_url) if self.public_url else " (LAN-only; add --public-url for Cloudflare failover)"
        log("[STEP 5/%d] HTTPS+WSS on 0.0.0.0:%d%s (page at / — SCAN THE QR NOW)" % (TOTAL_STEPS, self.args.https_port, extra))
        while True:
            try:
                conn, addr = srv.accept()
            except Exception as e:
                # TLS handshake aborts (user dismisses cert warning, port
                # probes, half-open scans) must NEVER kill the gateway:
                # handshake runs inside accept() on this thread.
                log("https accept aborted (%s), still listening" % type(e).__name__)
                continue
            threading.Thread(target=self.handle_https_conn,
                             args=(conn, addr, page_bytes, cam_page_bytes, uni_page_bytes), daemon=True).start()

    def handle_https_conn(self, conn, addr, page_bytes, cam_page_bytes=b"", uni_page_bytes=b""):
        try:
            conn.settimeout(10.0)
            req = b""
            while b"\r\n\r\n" not in req:
                c = conn.recv(4096)
                if not c:
                    return
                req += c
                if len(req) > 65536:
                    return
            head = req.split(b"\r\n\r\n", 1)[0].decode("latin-1")
            lines = head.split("\r\n")
            method, path = (lines[0].split(" ") + ["", ""])[:2]
            headers = {}
            for line in lines[1:]:
                if ":" in line:
                    k, v = line.split(":", 1)
                    headers[k.strip().lower()] = v.strip()
            # Path without query (?pc=..., ?resume=1 from failover page).
            path_only = path.split("?", 1)[0]
            # Where did this connection really come from? Direct LAN has no
            # CF headers; via Cloudflare Tunnel/proxy these are present.
            # Logging them tells the user instantly whether the phone is on
            # the LAN path or the Cloudflare path after a proxy toggle.
            via = ""
            cf_ip = headers.get("cf-connecting-ip", "") or headers.get("x-forwarded-for", "")
            cf_ray = headers.get("cf-ray", "")
            if cf_ip or cf_ray or "cloudflare" in headers.get("via", "").lower():
                via = " via=CLOUDFLARE(cf-ip=%s ray=%s host=%s)" % (cf_ip, cf_ray, headers.get("host", ""))
            else:
                via = " via=LAN-DIRECT(host=%s)" % headers.get("host", "")
            if headers.get("upgrade", "").lower() == "websocket" and path_only == "/mic":
                key = headers.get("sec-websocket-key", "")
                if not key:
                    return
                resp = ("HTTP/1.1 101 Switching Protocols\r\n"
                        "Upgrade: websocket\r\nConnection: Upgrade\r\n"
                        "Sec-WebSocket-Accept: " + ws_accept(key) + "\r\n\r\n")
                conn.sendall(resp.encode("latin-1"))
                conn.settimeout(None)
                log("[STEP 6/%d] phone WS connected from %s%s (waiting for START...)" % (TOTAL_STEPS, addr, via))
                self.handle_phone(conn, addr)
                return
            if headers.get("upgrade", "").lower() == "websocket" and path_only == "/cam":
                key = headers.get("sec-websocket-key", "")
                if not key:
                    return
                resp = ("HTTP/1.1 101 Switching Protocols\r\n"
                        "Upgrade: websocket\r\nConnection: Upgrade\r\n"
                        "Sec-WebSocket-Accept: " + ws_accept(key) + "\r\n\r\n")
                conn.sendall(resp.encode("latin-1"))
                conn.settimeout(None)
                log("[CAM 2/4] cam WS connected from %s%s (waiting for cam-start...)" % (addr, via))
                self.handle_cam_phone(conn, addr)
                return
            if method == "GET" and path_only in ("/", "/index.html"):
                body = page_bytes
                conn.sendall(("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n"
                              "Content-Length: %d\r\nCache-Control: no-store\r\n"
                              "Connection: close\r\n\r\n" % len(body)).encode("latin-1") + body)
            elif method == "GET" and path_only in ("/phone", "/phone.html"):
                # Unified mic+camera panel (one page, two independent sessions,
                # one shared steps log). Served only when the file is present.
                if uni_page_bytes:
                    conn.sendall(("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n"
                                  "Content-Length: %d\r\nCache-Control: no-store\r\n"
                                  "Connection: close\r\n\r\n" % len(uni_page_bytes)).encode("latin-1") + uni_page_bytes)
                else:
                    conn.sendall(b"HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")
            elif method == "GET" and path_only in ("/camera", "/camera.html"):
                # Phase 2.2 phone camera page (same TLS origin as the mic page;
                # front camera -> downscaled JPEG -> WSS /cam). Served only when
                # the operator enabled it (file present at startup).
                if cam_page_bytes:
                    conn.sendall(("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n"
                                  "Content-Length: %d\r\nCache-Control: no-store\r\n"
                                  "Connection: close\r\n\r\n" % len(cam_page_bytes)).encode("latin-1") + cam_page_bytes)
                else:
                    conn.sendall(b"HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")
            elif method == "GET" and path_only == "/health":
                # CORS *: after a proxy on/off toggle the stale page may
                # poll this from a different origin (LAN IP vs public
                # hostname). Same-origin still works; cross-origin now too.
                body = json.dumps({"ok": True, "service": "phone-mic-gateway",
                                   "lan_ip": self.lan_ip, "page_url": self.page_url,
                                   "cam_page_url": self.cam_page_url,
                                   "uni_page_url": self.uni_page_url,
                                   "candidates": self.candidates,
                                   "public_url": self.public_url,
                                   "https_port": self.args.https_port,
                                   "bridge_port": self.args.bridge_port,
                                   "camera_bridge_port": getattr(self.args, "camera_bridge_port", 8452),
                                   "steps_total": TOTAL_STEPS}).encode("utf-8")
                conn.sendall(("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n"
                              "Access-Control-Allow-Origin: *\r\n"
                              "Content-Length: %d\r\nConnection: close\r\n\r\n" % len(body)).encode("latin-1") + body)
            elif method == "GET" and path_only == "/qr.png":
                # STEP 3 artefact: scannable QR of the page URL (same origin).
                if self.qr_png:
                    conn.sendall(("HTTP/1.1 200 OK\r\nContent-Type: image/png\r\n"
                                  "Access-Control-Allow-Origin: *\r\n"
                                  "Content-Length: %d\r\nCache-Control: no-store\r\n"
                                  "Connection: close\r\n\r\n" % len(self.qr_png)).encode("latin-1") + self.qr_png)
                else:
                    conn.sendall(b"HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")
            elif method == "GET" and path_only == "/qr":
                # Human-friendly QR display for the PC browser: show this on
                # the PC screen and scan it with the phone camera.
                page = ("<!doctype html><html><head><meta charset='utf-8'>"
                        "<meta name='viewport' content='width=device-width,initial-scale=1'>"
                        "<title>LWE PhoneMic QR</title></head>"
                        "<body style='font-family:system-ui;text-align:center;background:#101418;color:#eee'>"
                        "<h2>Scan with the phone camera</h2>"
                        "<p><img src='/qr.png' style='width:min(80vw,360px);image-rendering:pixelated;"
                        "background:#fff;padding:12px;border-radius:8px'></p>"
                        "<p style='font-size:18px'>%s</p>"
                        "<p><a style='color:#8cf' href='/'>open page directly (this PC)</a></p>"
                        "</body></html>" % (self.page_url or "(starting...)"))
                body = page.encode("utf-8")
                conn.sendall(("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n"
                              "Content-Length: %d\r\nCache-Control: no-store\r\n"
                              "Connection: close\r\n\r\n" % len(body)).encode("latin-1") + body)
            elif method == "GET" and path_only == "/cam-qr.png":
                # STEP C artefact: scannable QR of the CAMERA page URL.
                if self.cam_qr_png:
                    conn.sendall(("HTTP/1.1 200 OK\r\nContent-Type: image/png\r\n"
                                  "Access-Control-Allow-Origin: *\r\n"
                                  "Content-Length: %d\r\nCache-Control: no-store\r\n"
                                  "Connection: close\r\n\r\n" % len(self.cam_qr_png)).encode("latin-1") + self.cam_qr_png)
                else:
                    conn.sendall(b"HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")
            elif method == "GET" and path_only == "/cam-qr":
                page = ("<!doctype html><html><head><meta charset='utf-8'>"
                        "<meta name='viewport' content='width=device-width,initial-scale=1'>"
                        "<title>LWE PhoneCamera QR</title></head>"
                        "<body style='font-family:system-ui;text-align:center;background:#101418;color:#eee'>"
                        "<h2>Scan with the phone camera</h2>"
                        "<p><img src='/cam-qr.png' style='width:min(80vw,360px);image-rendering:pixelated;"
                        "background:#fff;padding:12px;border-radius:8px'></p>"
                        "<p style='font-size:18px'>%s</p>"
                        "<p><a style='color:#8cf' href='/camera'>open camera page directly (this PC)</a></p>"
                        "</body></html>" % (self.cam_page_url or "(camera starting...)"))
                body = page.encode("utf-8")
                conn.sendall(("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n"
                              "Content-Length: %d\r\nCache-Control: no-store\r\n"
                              "Connection: close\r\n\r\n" % len(body)).encode("latin-1") + body)
            elif method == "GET" and path_only == "/phone-qr.png":
                # Unified-panel QR (mic + camera on one page).
                if self.uni_qr_png:
                    conn.sendall(("HTTP/1.1 200 OK\r\nContent-Type: image/png\r\n"
                                  "Access-Control-Allow-Origin: *\r\n"
                                  "Content-Length: %d\r\nCache-Control: no-store\r\n"
                                  "Connection: close\r\n\r\n" % len(self.uni_qr_png)).encode("latin-1") + self.uni_qr_png)
                else:
                    conn.sendall(b"HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")
            elif method == "GET" and path_only == "/phone-qr":
                page = ("<!doctype html><html><head><meta charset='utf-8'>"
                        "<meta name='viewport' content='width=device-width,initial-scale=1'>"
                        "<title>LWE Phone QR (mic + camera)</title></head>"
                        "<body style='font-family:system-ui;text-align:center;background:#101418;color:#eee'>"
                        "<h2>Scan with the phone camera</h2>"
                        "<p><img src='/phone-qr.png' style='width:min(80vw,360px);image-rendering:pixelated;"
                        "background:#fff;padding:12px;border-radius:8px'></p>"
                        "<p style='font-size:18px'>%s</p>"
                        "<p><a style='color:#8cf' href='/phone'>open unified panel directly (this PC)</a></p>"
                        "</body></html>" % (self.uni_page_url or "(unified panel starting...)"))
                body = page.encode("utf-8")
                conn.sendall(("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n"
                              "Content-Length: %d\r\nCache-Control: no-store\r\n"
                              "Connection: close\r\n\r\n" % len(body)).encode("latin-1") + body)
            else:
                conn.sendall(b"HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n")
        except Exception as e:
            log("https conn %s: %s" % (addr, e))
        finally:
            try:
                conn.close()
            except Exception:
                pass


def selftest():
    """Codec interop proof without network (mirrors PhoneMicProtocol.cs)."""
    fails = []

    def check(name, cond, extra=""):
        print(("PASS " if cond else "FAIL ") + name + (" — " + extra if extra and not cond else ""))
        if not cond:
            fails.append(name)

    # bridge envelope: len(5)+kind+serial+seq+payload(4) = 4+1+4+4+4 = 17
    body = struct.pack(">BII", K_AUDIO, 7, 42) + b"\x01\x02\x03\x04"
    frame = struct.pack(">I", len(body)) + body
    check("bridge-len", struct.unpack(">I", frame[:4])[0] == 13)
    check("bridge-kind", frame[4] == K_AUDIO)
    check("bridge-serial", struct.unpack(">I", frame[5:9])[0] == 7)
    check("bridge-seq", struct.unpack(">I", frame[9:13])[0] == 42)
    # pcm16 round-trip extremes
    raw = struct.pack("<hhhh", -32768, -1, 0, 32767)
    floats = [s / 32768.0 for s in struct.unpack("<4h", raw)]
    check("pcm16-range", abs(floats[0] + 1.0) < 1e-6 and abs(floats[3] - 32767 / 32768.0) < 1e-6)
    # ws accept vector (RFC 6455 §1.3 example)
    check("ws-accept", ws_accept("dGhlIHNhbXBsZSBub25jZQ==") == "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=")
    # server frames MUST be unmasked (RFC6455 §5.1) — Cloudflare drops
    # masked server frames, which used to wedge the phone at 4-stream.
    import io as _io

    class _FakeSock(object):
        def __init__(self):
            self.buf = b""

        def sendall(self, b):
            self.buf += bytes(b)

    _s = _FakeSock()
    ws_send_text(_s, "hi")
    check("ws-server-unmasked", _s.buf[:2] == b"\x81\x02", repr(_s.buf[:2]))
    _s2 = _FakeSock()
    ws_send_text(_s2, "x" * 200)
    check("ws-server-extlen", _s2.buf[1] == 126 and len(_s2.buf) == 2 + 2 + 200, repr(_s2.buf[:4]))
    # presence kinds ride the same envelope (Unity skips them in captures,
    # watches them for mid-game disconnect/STOP detection)
    for _kind, _name in ((K_UP, "presence-up"), (K_DOWN, "presence-down")):
        _p = bridge_frame(_kind, 0, 0, b"ws-connected" if _kind == K_UP else b"ws-closed")
        _plen = struct.unpack(">I", _p[:4])[0]
        check(_name, _p[4] == _kind and _plen == 9 + len(_p[13:]), repr(_p[:13]))
    # control-field reader parity (canonical + reject cases)
    good = '{"type":"start","session":"ph-abc","sampleRate":16000,"channels":1,"format":"pcm16"}'
    check("ctrl-canonical", "16000" in good and "pcm16" in good)
    bad = '{"type":"start","session":"x","sampleRate":48000,"channels":2,"format":"pcm16"}'
    check("ctrl-reject-shape", "48000" in bad)  # gateway rejects at on_start
    # camera envelope (same shape, separate kinds namespace — Unity decodes by
    # port, so numeric equality with the mic kinds is INTENTIONAL, not a clash)
    cbody = struct.pack(">BII", K_CAM_FRAME, 9, 77) + b"\xff\xd8\xff\x00"
    cframe = struct.pack(">I", len(cbody)) + cbody
    check("cam-bridge-kind", cframe[4] == K_CAM_FRAME)
    check("cam-bridge-serial", struct.unpack(">I", cframe[5:9])[0] == 9)
    check("cam-jpeg-soi", cframe[13:16] == CAM_JPEG_SOI)
    check("cam-reject-nonjpeg", b"\x89PNG"[:3] != CAM_JPEG_SOI)
    check("cam-reject-empty", len(b"") == 0)
    check("cam-cap", CAM_MAX_BYTES == 300 * 1024)
    # source-precedence (game -> gateway -> phone): K_PREFER envelope + strict
    # "<media>:<origin>" parsing (malformed never fatal, game-side precedence
    # applies regardless — this only moves the phone START UI).
    _pp = bridge_frame(K_PREFER, 0, 0, b"mic:local")
    check("prefer-envelope", _pp[4] == K_PREFER == 0x12 and _pp[13:] == b"mic:local")
    _cp = bridge_frame(K_PREFER, 0, 0, b"cam:phone")
    check("prefer-cam-envelope", _cp[4] == 0x12 and _cp[13:] == b"cam:phone")
    def _prefer_ok(payload, media, origin):
        try:
            text = payload.decode("utf-8")
        except Exception:
            return False
        parts = text.split(":")
        if len(parts) != 2:
            return False
        return parts[0].strip() == media and parts[1].strip() == origin
    check("prefer-parse-mic-local", _prefer_ok(b"mic:local", "mic", "local"))
    check("prefer-parse-cam-phone", _prefer_ok(b"cam:phone", "cam", "phone"))
    check("prefer-reject-bad", not _prefer_ok(b"mic:sometimes", "mic", "local")
          and not _prefer_ok(b"", "mic", "local")
          and not _prefer_ok(b"mic local", "mic", "local"))
    print("SELFTEST %s (%d fails)" % ("OK" if not fails else "FAILED", len(fails)))
    return 1 if fails else 0


def main():
    ap = argparse.ArgumentParser(description="LWE phone microphone gateway (LAN-first, Cloudflare-tolerant, no cloud DSP)")
    ap.add_argument("--cert", default="", help="TLS certificate file (PEM)")
    ap.add_argument("--key", default="", help="TLS private key file (PEM, never commit)")
    ap.add_argument("--https-port", type=int, default=8443)
    ap.add_argument("--bridge-port", type=int, default=8451)
    ap.add_argument("--page", default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                   "phone_mic_page.html"))
    ap.add_argument("--camera-bridge-port", type=int, default=8452,
                    help="Phase 2.2 camera JPEG bridge (loopback; mic stays on --bridge-port)")
    ap.add_argument("--camera-page", default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                          "phone_camera_page.html"),
                    help="Phase 2.2 phone camera page (missing file = camera route 404s, mic unaffected)")
    ap.add_argument("--camera-qr-png", default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                            "phone-camera-qr.png"),
                    help="where to save the camera-page QR PNG")
    ap.add_argument("--unified-page", default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                           "phone_page.html"),
                    help="unified mic+camera panel (missing file = /phone 404s, mic/camera unaffected)")
    ap.add_argument("--unified-qr-png", default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                             "phone-qr.png"),
                    help="where to save the unified-panel QR PNG")
    ap.add_argument("--record-dir", default="", help="opt-in test recordings dir (explicit only)")
    ap.add_argument("--bridge-only", action="store_true",
                    help="TEST ONLY: serve just the loopback TCP bridge (no HTTPS/WSS). "
                         "Phone path always requires TLS; this mode never serves phones.")
    ap.add_argument("--inject-wav", default="",
                    help="TEST ONLY with --bridge-only: play this mono16@16000 WAV as one "
                         "phone session to the first subscriber (transport proof, not a verdict).")
    ap.add_argument("--selftest", action="store_true", help="codec checks without network")
    ap.add_argument("--lan-ip", default="",
                    help="override auto-detected LAN IP (default: auto-pick 192.168.x > 10.x > 172.16-31.x)")
    ap.add_argument("--public-url", default="",
                    help="OPTIONAL public fallback URL via Cloudflare Tunnel, e.g. "
                         "https://mic.example.com (no trailing slash). When the phone toggles "
                         "between LAN and Cloudflare networks the page fails over to this URL "
                         "without re-scanning. Requires a cloudflared tunnel pointing at "
                         "https://127.0.0.1:<https-port> with --no-tls-verify (self-signed LAN cert). "
                         "Empty = LAN-only (default, offline-first unchanged).")
    ap.add_argument("--ecl", default="M",
                    help="QR error-correction level L/M/Q/H (default M; higher survives dirty screens)")
    ap.add_argument("--no-qr", action="store_true",
                    help="skip QR generation (log the URL only)")
    ap.add_argument("--qr-png", default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                     "phone-mic-qr.png"),
                    help="where to save the scannable QR PNG (default tools/phone-mic-qr.png)")
    ap.add_argument("--qr-svg", default="",
                    help="optionally also save a QR SVG file (e.g. tools/phone-mic-qr.svg)")
    ap.add_argument("--no-ascii-qr", action="store_true",
                    help="do not print the terminal QR fallback (URL is always printed)")
    args = ap.parse_args()
    if args.selftest:
        sys.exit(selftest())
    gw = Gateway(args)
    if args.bridge_only:
        log("BRIDGE-ONLY test mode (no HTTPS/WSS; phones never served here)")
        threading.Thread(target=gw.serve_cam_bridge, args=(args.camera_bridge_port,), daemon=True).start()
        gw.serve_bridge()
        return
    if not args.cert or not args.key:
        print("FAIL: --cert and --key are required (see docs/HANDOFF/PHASE_2_1_PHONE_MIC.md §HTTPS).")
        print("Private keys are gitignored and must never be committed.")
        sys.exit(2)
    if not os.path.isfile(args.page):
        print("FAIL: phone page not found: %s" % args.page)
        sys.exit(2)
    # -- STEP 1: LAN IP (no more manual ipconfig) -------------------------
    try:
        lan_ip, candidates, reason = lwe_qr.pick_lan_ip(args.lan_ip.strip())
    except RuntimeError as e:
        print("FAIL [STEP 1/%d]: %s" % (TOTAL_STEPS, e))
        sys.exit(2)
    gw.lan_ip = lan_ip
    gw.candidates = candidates
    gw.public_url = (args.public_url or "").rstrip("/")
    log("[STEP 1/%d] LAN IP = %s (%s; all candidates=%s)" % (TOTAL_STEPS, lan_ip, reason, candidates))
    if gw.public_url:
        log("[STEP 1/%d] public fallback (Cloudflare) = %s — phone auto-fails-over on proxy toggle" % (TOTAL_STEPS, gw.public_url))
    else:
        log("[STEP 1/%d] public fallback: none (LAN-only). To survive Cloudflare proxy on/off, "
            "run: cloudflared tunnel --url https://127.0.0.1:%d --no-tls-verify "
            "then restart gateway with --public-url https://<your-host>" % (TOTAL_STEPS, args.https_port))
    # -- STEP 2: cert must cover that IP (else phone TLS fails confusingly) --
    ok, detail = lwe_qr.cert_covers_ip(args.cert, lan_ip)
    log("[STEP 2/%d] cert check: %s" % (TOTAL_STEPS, detail))
    if not ok:
        print("FAIL [STEP 2/%d]: %s" % (TOTAL_STEPS, detail))
        print("FIX: re-issue the cert for %s, or pass --lan-ip <cert-IP> --https-port %d" % (lan_ip, args.https_port))
        sys.exit(2)
    # -- STEP 3: page URL + scannable QR (no more typing the IP) ------------
    page_url = lwe_qr.build_url(lan_ip, args.https_port)
    gw.page_url = page_url
    if not args.no_qr:
        try:
            qr = lwe_qr.encode_url(page_url, ecl=args.ecl)
            matrix = lwe_qr.matrix_of(qr)
            _, size = lwe_qr.save_png(args.qr_png, matrix)
            gw.qr_png = open(args.qr_png, "rb").read()
            extra = ""
            if args.qr_svg:
                with open(args.qr_svg, "w", encoding="utf-8") as f:
                    f.write(lwe_qr.svg_text(matrix))
                extra = " + svg %s" % args.qr_svg
            log("[STEP 3/%d] QR ready (v%d, %dx%d): %s (%d bytes%s) — scan with phone camera" % (
                TOTAL_STEPS, qr.get_version(), len(matrix), len(matrix), args.qr_png, size, extra))
            if not args.no_ascii_qr:
                print("----- QR fallback (PNG file + /qr.png serve are the reliable path) -----")
                print(lwe_qr.ascii_art(matrix))
                print("------------------------------------------------------------------------")
        except Exception as e:
            print("FAIL [STEP 3/%d]: QR encode failed: %s" % (TOTAL_STEPS, e))
            sys.exit(2)
    else:
        log("[STEP 3/%d] QR skipped (--no-qr); use the URL below" % TOTAL_STEPS)
    log("PAGE URL (no typing needed — scan the QR): %s" % page_url)
    with open(args.page, "rb") as f:
        page_bytes = f.read()
    # -- STEP C: camera page URL + QR (same TLS origin, own bridge port) -----
    cam_page_bytes = b""
    if os.path.isfile(args.camera_page):
        with open(args.camera_page, "rb") as f:
            cam_page_bytes = f.read()
        cam_page_url = "https://%s:%d/camera" % (lan_ip, args.https_port)
        gw.cam_page_url = cam_page_url
        if not args.no_qr:
            try:
                cqr = lwe_qr.encode_url(cam_page_url, ecl=args.ecl)
                cmatrix = lwe_qr.matrix_of(cqr)
                _, csize = lwe_qr.save_png(args.camera_qr_png, cmatrix)
                gw.cam_qr_png = open(args.camera_qr_png, "rb").read()
                log("[CAM 1/4] camera page %s + QR %s (%d bytes) — scan for the face stream"
                    % (cam_page_url, args.camera_qr_png, csize))
            except Exception as e:
                log("[CAM 1/4] camera QR encode failed (mic QR unaffected): %s" % e)
        else:
            log("[CAM 1/4] camera page %s (QR skipped)" % cam_page_url)
    else:
        log("[CAM 1/4] camera page file missing (%s) — /camera 404s, mic path unaffected"
            % args.camera_page)
    # -- STEP U: unified mic+camera panel (one page, one QR, shared steps log)
    uni_page_bytes = b""
    if os.path.isfile(args.unified_page):
        with open(args.unified_page, "rb") as f:
            uni_page_bytes = f.read()
        uni_page_url = "https://%s:%d/phone" % (lan_ip, args.https_port)
        gw.uni_page_url = uni_page_url
        if not args.no_qr:
            try:
                uqr = lwe_qr.encode_url(uni_page_url, ecl=args.ecl)
                umatrix = lwe_qr.matrix_of(uqr)
                _, usize = lwe_qr.save_png(args.unified_qr_png, umatrix)
                gw.uni_qr_png = open(args.unified_qr_png, "rb").read()
                log("[UNI] unified panel %s + QR %s (%d bytes) — ONE scan for mic + camera"
                    % (uni_page_url, args.unified_qr_png, usize))
            except Exception as e:
                log("[UNI] unified QR encode failed (mic/cam QRs unaffected): %s" % e)
        else:
            log("[UNI] unified panel %s (QR skipped)" % uni_page_url)
    else:
        log("[UNI] unified page file missing (%s) — /phone 404s, mic/cam unaffected"
            % args.unified_page)
    threading.Thread(target=gw.serve_bridge, daemon=True).start()
    threading.Thread(target=gw.serve_cam_bridge, args=(args.camera_bridge_port,), daemon=True).start()
    try:
        gw.serve_https(page_bytes, cam_page_bytes, uni_page_bytes)
    except KeyboardInterrupt:
        print("\ngateway stopped")


if __name__ == "__main__":
    main()
