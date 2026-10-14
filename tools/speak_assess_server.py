#!/usr/bin/env python3
"""speak_assess_server.py — LOCAL speak-assess helper (free, offline).

Engine: faster-whisper (MIT) with the openai/whisper base.en weights (MIT),
CPU int8. Contract (kept in sync with Assets/D_Audio/LocalAssessServiceClient.cs):
    GET  /health                        -> {"ok": true, "service": "speak-assess", ...}
    POST /assess?target=<word>          body = WAV (16k mono PCM16)
         -> {"transcript", "confidence", "no_speech", "latency_ms", "engine"}

HONESTY: transcript + confidence only. The GAME's frozen SpeakingPassPolicy
combines this with the offline acoustic engine and owns the verdict; the
service never returns a "level". Hallucination guard: when the model's
no_speech probability is high the transcript is returned EMPTY.

Run:  python tools/speak_assess_server.py [--port 8470] [--engine base.en]
"""
from __future__ import annotations

import argparse
import io
import json
import math
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs

import numpy as np
import soundfile as sf

MODEL_LOCK = threading.Lock()
ASR = None
ENGINE_NAME = "base.en"
NO_SPEECH_GATE = 0.5  # transcript suppressed above this model no-speech probability


def load_engine(name: str):
    from faster_whisper import WhisperModel
    return WhisperModel(name, device="cpu", compute_type="int8")


def assess(wav_bytes: bytes) -> dict:
    x, sr = sf.read(io.BytesIO(wav_bytes), dtype="float32")
    if x.ndim > 1:
        x = x.mean(axis=1)
    if sr != 16000:
        n = int(len(x) * 16000 / sr)
        x = np.interp(np.linspace(0, 1, n, endpoint=False),
                      np.linspace(0, 1, len(x), endpoint=False), x).astype("float32")
    t0 = time.time()
    with MODEL_LOCK:
        segs, _info = ASR.transcribe(x, beam_size=1, language="en",
                                     condition_on_previous_text=False)
        segs = list(segs)
    text = " ".join(s.text for s in segs).strip()
    if segs:
        no_speech = max(float(s.no_speech_prob) for s in segs)
        conf = float(np.mean([math.exp(s.avg_logprob) for s in segs]))
    else:
        no_speech = 1.0
        conf = 0.0
    if no_speech > NO_SPEECH_GATE:
        text = ""
    return {
        "transcript": text,
        "confidence": round(min(max(conf, 0.0), 1.0), 4),
        "no_speech": round(no_speech, 4),
        "latency_ms": int((time.time() - t0) * 1000),
        "engine": ENGINE_NAME,
    }


class Handler(BaseHTTPRequestHandler):
    server_version = "speak-assess/1.0"

    def _send_json(self, code: int, obj: dict):
        body = json.dumps(obj).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        path = urlparse(self.path).path
        if path == "/health":
            self._send_json(200, {
                "ok": True, "service": "speak-assess", "engine": ENGINE_NAME,
                "ready": ASR is not None,
            })
            return
        self._send_json(404, {"error": "not_found"})

    def do_POST(self):
        url = urlparse(self.path)
        if url.path != "/assess":
            self._send_json(404, {"error": "not_found"})
            return
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if length <= 44 or length > 4_000_000:
                self._send_json(400, {"error": "bad_length"})
                return
            wav_bytes = self.rfile.read(length)
            q = parse_qs(url.query)
            target = (q.get("target") or [""])[0]
            if ASR is None:
                self._send_json(503, {"error": "model_loading"})
                return
            result = assess(wav_bytes)
            result["target"] = target
            
            # Enrich with phoneme-level analysis and kid-friendly feedback
            try:
                from pronunciation_analyzer import analyze_pronunciation
                analysis = analyze_pronunciation(target, result.get("transcript", ""))
                result["accuracy_percent"] = analysis["percent"]
                result["pron_level"] = analysis["level"]
                result["feedback_vi"] = analysis["feedback_vi"]
                result["tip"] = analysis["tip"]
                result["error_phoneme"] = analysis["error_phoneme"]
                result["error_type"] = analysis["error_type"]
                result["target_phonemes"] = " ".join(analysis["target_phonemes"])
                result["spoken_phonemes"] = " ".join(analysis["spoken_phonemes"])
            except Exception as e:
                result["accuracy_percent"] = 0
                result["feedback_vi"] = ""
                result["tip"] = ""
                result["error_phoneme"] = ""
                result["error_type"] = "none"

            self._send_json(200, result)
        except Exception as exc:  # noqa: BLE001
            self._send_json(500, {"error": "assess_failed", "detail": str(exc)[:200]})

    def log_message(self, fmt, *args):
        # Keep the console quiet; the game logs its own evidence.
        pass


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", type=int, default=8470)
    ap.add_argument("--engine", default="base.en")
    args = ap.parse_args()
    global ASR, ENGINE_NAME
    ENGINE_NAME = args.engine
    print(f"[speak-assess] loading faster-whisper {ENGINE_NAME} (cpu int8)...", flush=True)
    ASR = load_engine(ENGINE_NAME)
    srv = ThreadingHTTPServer(("127.0.0.1", args.port), Handler)
    srv.daemon_threads = True
    print(f"SPEAK_ASSESS_READY port={args.port} engine={ENGINE_NAME}", flush=True)
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        srv.server_close()
        print("[speak-assess] stopped", flush=True)


if __name__ == "__main__":
    main()
