#!/usr/bin/env python3
"""Standalone phone-link pipeline test (NO game, NO Unity).

Acts as the Unity side: dials the gateway bridge (127.0.0.1:8451),
SUBSCRIBEs, and reports what arrives. Proves each hop separately:

  Flow A — bridge path, no phone needed:
    1. gateway --bridge-only --inject-wav <tone16k.wav>   (PC window 1)
    2. python tools/test_phone_link.py --expect-session    (PC window 2)
       -> expect HELLO + AUDIO chunks + STOP (transport proof).

  Flow B — real phone end-to-end, no game:
    1. gateway --cert lan.crt --key lan.key                (PC window 1)
    2. python tools/test_phone_link.py --wait 60           (PC window 2)
    3. scan QR on phone, press START, speak, STOP
       -> expect AUDIO chunks live + STOP (phone proof).

  Helper: --gen-tone out.wav makes a 1s 440Hz mono16@16k tone for Flow A.

Exit codes: 0 = expectation met, 1 = not met (timeout / wrong frames),
2 = cannot reach the bridge at all. All stdlib, offline.
"""
import argparse
import math
import socket
import struct
import sys
import time
import wave

K_AUDIO, K_STOP, K_ERROR, K_HELLO = 1, 2, 3, 4
K_UP, K_DOWN = 5, 6  # presence (mirror phone_mic_gateway.py / PhoneMicProtocol.cs)
K_SUBSCRIBE = 0x10
HEADER = 9  # kind(1) + serial(4) + seq(4)


def log(step, total, msg):
    print("[TEST %d/%d] %s" % (step, total, msg), flush=True)


def recv_exact(sock, n, deadline):
    buf = b""
    while len(buf) < n:
        if time.time() > deadline:
            return None
        try:
            chunk = sock.recv(n - len(buf))
        except socket.timeout:
            return None
        if not chunk:
            return None
        buf += chunk
    return buf


def read_frame(sock, timeout):
    deadline = time.time() + timeout
    raw_len = recv_exact(sock, 4, deadline)
    if raw_len is None:
        return None
    (body_len,) = struct.unpack(">I", raw_len)
    if body_len < HEADER or body_len > 4 + HEADER + 65535:
        return ("MALFORMED", 0, 0, b"")
    body = recv_exact(sock, body_len, deadline)
    if body is None:
        return None
    kind, serial, seq = body[0], struct.unpack(">I", body[1:5])[0], struct.unpack(">I", body[5:9])[0]
    return (kind, serial, seq, body[HEADER:])


def gen_tone(path, secs=1.0, freq=440.0, rate=16000, amp=0.25):
    n = int(secs * rate)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        frames = b"".join(struct.pack("<h", int(math.sin(2 * math.pi * freq * i / rate) * amp * 32767))
                           for i in range(n))
        w.writeframes(frames)
    print("tone written: %s (%d samples mono16@%d)" % (path, n, rate))


def main():
    ap = argparse.ArgumentParser(description="Standalone phone-link pipeline test (no game)")
    ap.add_argument("--bridge-host", default="127.0.0.1")
    ap.add_argument("--bridge-port", type=int, default=8451)
    ap.add_argument("--wait", type=float, default=10.0,
                    help="seconds to watch for a live phone session (Flow B)")
    ap.add_argument("--expect-session", action="store_true",
                    help="REQUIRE at least one AUDIO chunk + clean STOP (Flow A/B verdict)")
    ap.add_argument("--gen-tone", default="", help="write test tone WAV to PATH and exit")
    args = ap.parse_args()

    if args.gen_tone:
        gen_tone(args.gen_tone)
        return 0

    total = 4
    # STEP 1: bridge reachable?
    log(1, total, "dialing bridge %s:%d ..." % (args.bridge_host, args.bridge_port))
    try:
        sock = socket.create_connection((args.bridge_host, args.bridge_port), timeout=3.0)
    except OSError as e:
        log(1, total, "FAIL: bridge unreachable (%s) — is the gateway running?" % e)
        return 2
    sock.settimeout(2.0)
    log(1, total, "TCP connected")

    # STEP 2: SUBSCRIBE + HELLO?
    body = struct.pack(">BII", K_SUBSCRIBE, 0, 0)
    sock.sendall(struct.pack(">I", len(body)) + body)
    frame = read_frame(sock, 5.0)
    if frame is None or frame[0] != K_HELLO:
        log(2, total, "FAIL: no HELLO greeting (got %r) — not the phone-mic gateway?" % (frame,))
        sock.close()
        return 2
    log(2, total, "subscribed, gateway says: %r" % frame[3].decode("utf-8", "replace"))

    # STEP 3: watch for a live phone session.
    log(3, total, "watching %.0fs for a live phone session (START on the phone now) ..." % args.wait)
    serials = {}
    errors = []
    deadline = time.time() + args.wait
    got_audio = got_stop = False
    while time.time() < deadline:
        frame = read_frame(sock, min(0.5, max(0.1, deadline - time.time())))
        if frame is None:
            continue
        kind, serial, seq, payload = frame
        if kind == "MALFORMED":
            log(3, total, "WARN: malformed envelope (ignored)")
            continue
        if kind == K_AUDIO:
            st = serials.setdefault(serial, {"chunks": 0, "samples": 0, "seq_gaps": 0, "last": -1})
            st["chunks"] += 1
            st["samples"] += len(payload) // 2
            if st["last"] >= 0 and seq != st["last"] + 1:
                st["seq_gaps"] += 1
            st["last"] = seq
            got_audio = True
        elif kind == K_STOP:
            serials.setdefault(serial, {"chunks": 0, "samples": 0, "seq_gaps": 0, "last": -1})
            got_stop = True
            log(3, total, "session serial=%d STOP (clean end)" % serial)
            if args.expect_session and got_audio:
                break
        elif kind == K_ERROR:
            errors.append(payload.decode("utf-8", "replace"))
            log(3, total, "session ERROR: %r" % errors[-1])
        elif kind == K_UP:
            log(3, total, "phone page OPEN (%r) — START will follow" % payload.decode("utf-8", "replace"))
        elif kind == K_DOWN:
            log(3, total, "phone page GONE (%r) — mid-game drops show here" % payload.decode("utf-8", "replace"))
        # HELLO duplicates: ignore
    sock.close()

    # STEP 4: verdict.
    total_chunks = sum(s["chunks"] for s in serials.values())
    total_samples = sum(s["samples"] for s in serials.values())
    total_gaps = sum(s["seq_gaps"] for s in serials.values())
    log(4, total, "sessions=%d chunks=%d samples=%d (%.2fs audio) seq_gaps=%d errors=%r" % (
        len(serials), total_chunks, total_samples, total_samples / 16000.0, total_gaps, errors))
    if args.expect_session:
        if got_audio and got_stop and not errors:
            log(4, total, "PASS: live session AUDIO + clean STOP, no errors")
            return 0
        log(4, total, "FAIL: need AUDIO + STOP with no errors (speak + STOP on the phone, or check --inject-wav)")
        return 1
    if got_audio:
        log(4, total, "PASS: phone audio is flowing (informational run)")
    else:
        log(4, total, "no phone audio in window (gateway OK, phone idle — press START and re-run to prove the phone hop)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
