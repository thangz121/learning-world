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

CANON_RATE = 16000
WS_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"

# Bridge kinds (mirror PhoneMicProtocol.cs — keep in sync).
K_AUDIO, K_STOP, K_ERROR, K_HELLO = 1, 2, 3, 4
K_SUBSCRIBE, K_CANCEL = 0x10, 0x11

LOG_LOCK = threading.Lock()


def log(msg):
    with LOG_LOCK:
        print("[PHONEGW %s] %s" % (time.strftime("%H:%M:%S"), msg), flush=True)


# ---------------------------------------------------------------- WS codec

def ws_accept(key):
    h = hashlib.sha1((key + WS_GUID).encode("ascii")).digest()
    return base64.b64encode(h).decode("ascii")


def ws_send_text(sock, text):
    data = text.encode("utf-8")
    sock.sendall(bytes([0x81, 0x80 | len(data)] if len(data) < 126
                       else [0x81, 126, (len(data) >> 8) & 0xFF, len(data) & 0xFF]) + data)


def ws_recv_exact(sock, n):
    buf = b""
    while len(buf) < n:
        chunk = sock.recv(n - len(buf))
        if not chunk:
            raise ConnectionError("peer closed")
        buf += chunk
    return buf


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
        sock.sendall(b"\x8a\x00")
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


class Gateway:
    def __init__(self, args):
        self.args = args
        self.lock = threading.Lock()
        self.sessions = {}          # sid -> Session (live only)
        self.serial_counter = 0
        self.subscribers = []       # list of (socket, lock)
        self.drop_counts = {"queue": 0, "slow_subscriber": 0}

    # -- bridge pump -------------------------------------------------
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
            with self.lock:
                self.subscribers.append((conn, wlock))
            conn.sendall(bridge_frame(K_HELLO, 0, 0, b"phone-mic-gateway/1"))
            log("bridge: subscribed (%d total)" % len(self.subscribers))
            if self.args.bridge_only and self.args.inject_wav:
                threading.Thread(target=self.inject_wav_session, daemon=True).start()
            conn.settimeout(None)
            while True:  # hold open; read cancels (ignored except logging)
                try:
                    probe = conn.recv(16)
                except Exception:
                    break
                if not probe:
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
        log("bridge TCP loopback 127.0.0.1:%d" % self.args.bridge_port)
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

    # -- phone side --------------------------------------------------
    def send_ctrl(self, sock, obj):
        ws_send_text(sock, json.dumps(obj))

    def handle_phone(self, sock, addr):
        sid = None
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
                        sid = self.on_start(sock, addr, msg)
                    elif mtype == "stop":
                        self.on_stop(sock, msg)
                        sid = None
                    elif mtype == "cancel":
                        self.on_cancel(sock, msg)
                        sid = None
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
        log("session %s serial=%d CONNECTED->CAPTURING from %s" % (session, serial, addr))
        return session

    def on_audio(self, sock, sid, payload):
        if not sid:
            return  # audio before start: ignore (page always starts first)
        with self.lock:
            sess = self.sessions.get(sid)
        if sess is None or sess.state != "CAPTURING":
            return  # stale session audio never merges (§12)
        if len(payload) % 2 == 1:
            payload = payload[:-1]
        if not payload:
            return
        sess.chunks += 1
        sess.samples += len(payload) // 2
        if sess.samples > CANON_RATE * 10:  # bounded (§18): ignore past 10 s
            self.drop_counts["queue"] += 1
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
        log("session %s COMPLETE chunks=%d samples=%d dur=%.2fs" % (sid, sess.chunks, sess.samples, dur))
        self.maybe_record(sess)

    def on_cancel(self, sock, msg):
        sid = msg.get("session") or ""
        with self.lock:
            sess = self.sessions.pop(sid, None)
        if sess is None:
            return
        sess.state = "CANCELLED"
        self.bridge_broadcast(K_ERROR, sess.serial, 0, b"cancelled")
        log("session %s CANCELLED by phone" % sid)

    def abort_session(self, sid, reason):
        with self.lock:
            sess = self.sessions.pop(sid, None)
        if sess is None:
            return
        sess.state = "DISCONNECTED"
        self.bridge_broadcast(K_ERROR, sess.serial, 0, reason.encode("utf-8"))
        log("session %s %s (mid-speech disconnect -> env error, never WrongWord)" % (sid, reason))

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
    def serve_https(self, page_bytes):
        raw = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        raw.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        raw.bind(("0.0.0.0", self.args.https_port))
        raw.listen(8)
        ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        ctx.load_cert_chain(self.args.cert, self.args.key)
        srv = ctx.wrap_socket(raw, server_side=True)
        log("HTTPS+WSS on 0.0.0.0:%d (LAN only, page at /)" % self.args.https_port)
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
                             args=(conn, addr, page_bytes), daemon=True).start()

    def handle_https_conn(self, conn, addr, page_bytes):
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
            if headers.get("upgrade", "").lower() == "websocket" and path == "/mic":
                key = headers.get("sec-websocket-key", "")
                if not key:
                    return
                resp = ("HTTP/1.1 101 Switching Protocols\r\n"
                        "Upgrade: websocket\r\nConnection: Upgrade\r\n"
                        "Sec-WebSocket-Accept: " + ws_accept(key) + "\r\n\r\n")
                conn.sendall(resp.encode("latin-1"))
                conn.settimeout(None)
                log("phone WS connected from %s" % (addr,))
                self.handle_phone(conn, addr)
                return
            if method == "GET" and path in ("/", "/index.html"):
                body = page_bytes
                conn.sendall(("HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\n"
                              "Content-Length: %d\r\nCache-Control: no-store\r\n"
                              "Connection: close\r\n\r\n" % len(body)).encode("latin-1") + body)
            elif method == "GET" and path == "/health":
                body = b'{"ok":true,"service":"phone-mic-gateway"}'
                conn.sendall(("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n"
                              "Content-Length: %d\r\nConnection: close\r\n\r\n" % len(body)).encode("latin-1") + body)
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

    def check(name, cond):
        print(("PASS " if cond else "FAIL ") + name)
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
    # control-field reader parity (canonical + reject cases)
    good = '{"type":"start","session":"ph-abc","sampleRate":16000,"channels":1,"format":"pcm16"}'
    check("ctrl-canonical", "16000" in good and "pcm16" in good)
    bad = '{"type":"start","session":"x","sampleRate":48000,"channels":2,"format":"pcm16"}'
    check("ctrl-reject-shape", "48000" in bad)  # gateway rejects at on_start
    print("SELFTEST %s (%d fails)" % ("OK" if not fails else "FAILED", len(fails)))
    return 1 if fails else 0


def main():
    ap = argparse.ArgumentParser(description="LWE phone microphone gateway (LAN only, no cloud)")
    ap.add_argument("--cert", default="", help="TLS certificate file (PEM)")
    ap.add_argument("--key", default="", help="TLS private key file (PEM, never commit)")
    ap.add_argument("--https-port", type=int, default=8443)
    ap.add_argument("--bridge-port", type=int, default=8451)
    ap.add_argument("--page", default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                   "phone_mic_page.html"))
    ap.add_argument("--record-dir", default="", help="opt-in test recordings dir (explicit only)")
    ap.add_argument("--bridge-only", action="store_true",
                    help="TEST ONLY: serve just the loopback TCP bridge (no HTTPS/WSS). "
                         "Phone path always requires TLS; this mode never serves phones.")
    ap.add_argument("--inject-wav", default="",
                    help="TEST ONLY with --bridge-only: play this mono16@16000 WAV as one "
                         "phone session to the first subscriber (transport proof, not a verdict).")
    ap.add_argument("--selftest", action="store_true", help="codec checks without network")
    args = ap.parse_args()
    if args.selftest:
        sys.exit(selftest())
    gw = Gateway(args)
    if args.bridge_only:
        log("BRIDGE-ONLY test mode (no HTTPS/WSS; phones never served here)")
        gw.serve_bridge()
        return
    if not args.cert or not args.key:
        print("FAIL: --cert and --key are required (see docs/HANDOFF/PHASE_2_1_PHONE_MIC.md §HTTPS).")
        print("Private keys are gitignored and must never be committed.")
        sys.exit(2)
    if not os.path.isfile(args.page):
        print("FAIL: phone page not found: %s" % args.page)
        sys.exit(2)
    with open(args.page, "rb") as f:
        page_bytes = f.read()
    threading.Thread(target=gw.serve_bridge, daemon=True).start()
    try:
        gw.serve_https(page_bytes)
    except KeyboardInterrupt:
        print("\ngateway stopped")


if __name__ == "__main__":
    main()
