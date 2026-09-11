#!/usr/bin/env python3
"""W1 pre-gen TTS downloader (Agent D). Stdlib only.

Reads Content/audio_manifest.json (Agent C authors it: 8 entries
{id,text,voice,lang,rate,pitch,style,format,file}). If that file does not
exist yet, falls back to the EXACT pinned W1 fetch list below.

For each entry GETs the canonical Worker (WorkerTtsContract v6.4.1):
  GET base?text=<URL-escaped>&lang=<passthrough>&rate=<normal if rate>=0.8 else slow>
NO auth header. voice/pitch/style/format are NEVER sent (contract freeze) but
stay in the manifest so the C# L2 cache keys stay unique.

Verifies every response (HTTP 200 + content-type contains audio/mpeg + size>0,
retry 5xx once), writes Assets/StreamingAssets/audio/<file>, then mirrors the
manifest to Assets/StreamingAssets/audio/manifest.json (Unity ships
StreamingAssets; Content/ does not ship in players).

This script NEVER hashes (L2 cache keys are computed ONLY in C# via
AudioCache.CacheKey). Prints per-file bytes + total; exits nonzero on failure.

Usage (run from repo root):
  python tools/pregen_w1.py
"""
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

BASE_URL = "https://round-mud-63dd.hoaithuong1995cdmna.workers.dev/"
MAX_CHARS = 200  # WorkerTtsContract: text<=200 per request
TIMEOUT_SEC = 25

ROOT = Path(__file__).resolve().parent.parent
CONTENT_MANIFEST = ROOT / "Content" / "audio_manifest.json"
OUT_DIR = ROOT / "Assets" / "StreamingAssets" / "audio"
OUT_MANIFEST = OUT_DIR / "manifest.json"

# Pinned W1 fetch list. Params must match runtime requests EXACTLY or L2 keys
# miss (priorities P1-P4 are NOT in cache keys). Used only when
# Content/audio_manifest.json does not exist yet (Agent C still authoring it).
PINNED = [
    {"id": "milo_greet", "text": "Hello! I am Milo!", "voice": "milo_v1",
     "lang": "en-US", "rate": 1.0, "pitch": 1.0, "style": "Clear",
     "format": "Mp3_44100", "file": "milo_greet.mp3"},
    {"id": "milo_instruct_find", "text": "Find the apple!", "voice": "milo_v1",
     "lang": "en-US", "rate": 1.0, "pitch": 1.0, "style": "Clear",
     "format": "Mp3_44100", "file": "milo_instruct_find.mp3"},
    {"id": "milo_instruct_bring", "text": "Bring it to Mia!", "voice": "milo_v1",
     "lang": "en-US", "rate": 1.0, "pitch": 1.0, "style": "Clear",
     "format": "Mp3_44100", "file": "milo_instruct_bring.mp3"},
    {"id": "milo_praise_found", "text": "You found it!", "voice": "milo_v1",
     "lang": "en-US", "rate": 1.0, "pitch": 1.0, "style": "Excited",
     "format": "Mp3_44100", "file": "milo_praise_found.mp3"},
    {"id": "milo_celebrate", "text": "Perfect! Good job!", "voice": "milo_v1",
     "lang": "en-US", "rate": 1.0, "pitch": 1.0, "style": "Excited",
     "format": "Mp3_44100", "file": "milo_celebrate.mp3"},
    {"id": "milo_encourage", "text": "Great! Let's try together!", "voice": "milo_v1",
     "lang": "en-US", "rate": 1.0, "pitch": 1.0, "style": "Excited",
     "format": "Mp3_44100", "file": "milo_encourage.mp3"},
    {"id": "apple_normal", "text": "apple", "voice": "learning_v1",
     "lang": "en-US", "rate": 0.85, "pitch": 0.0, "style": "Clear",
     "format": "Mp3_44100", "file": "apple_normal.mp3"},
    {"id": "apple_slow", "text": "apple", "voice": "learning_v1",
     "lang": "en-US", "rate": 0.70, "pitch": 0.0, "style": "Clear",
     "format": "Mp3_44100", "file": "apple_slow.mp3"},
]


def map_rate(rate):
    """Mirror of CloudflareTranslateTtsProvider.MapRate: >=0.8 -> normal else slow."""
    return "normal" if float(rate) >= 0.8 else "slow"


def build_url(text, lang, rate_hint):
    q = "text=%s&lang=%s&rate=%s" % (
        urllib.parse.quote(text, safe=""),
        urllib.parse.quote(lang, safe=""),
        urllib.parse.quote(rate_hint, safe=""),
    )
    return BASE_URL.rstrip("/") + "/?" + q


def fetch_audio(url):
    """GET one URL. Returns (ok, status, content_type, data_or_error). Retries 5xx once."""
    last = ("unknown", -1, "")
    for attempt in (1, 2):
        try:
            req = urllib.request.Request(
                url, headers={"User-Agent": "LWE-pregen-w1/1.0", "Accept": "audio/mpeg"})
            with urllib.request.urlopen(req, timeout=TIMEOUT_SEC) as resp:
                status = getattr(resp, "status", 200)
                ctype = resp.headers.get("Content-Type", "")
                data = resp.read()
            if status != 200:
                if 500 <= status < 600 and attempt == 1:
                    last = ("http-status-%s-then-retry" % status, status, ctype)
                    time.sleep(1)
                    continue
                return False, status, ctype, "http-status-%s" % status
            if "audio/mpeg" not in (ctype or "").lower():
                return False, status, ctype, "unexpected-content-type"
            if not data:
                return False, status, ctype, "empty-body"
            return True, status, ctype, data
        except urllib.error.HTTPError as e:
            try:
                ctype = e.headers.get("Content-Type", "") if e.headers else ""
            except Exception:
                ctype = ""
            if 500 <= e.code < 600 and attempt == 1:
                last = ("http-%s-then-retry" % e.code, e.code, ctype)
                time.sleep(1)
                continue
            return False, e.code, ctype, "http-error-%s" % e.code
        except Exception as e:  # URLError, TimeoutError, OSError (offline sandbox, DNS, ...)
            return False, -1, "", "%s: %s" % (type(e).__name__, e)
    return False, last[1], last[2], last[0]


def load_entries():
    """(entries, source_note). entries = list of dicts with required keys."""
    if CONTENT_MANIFEST.is_file():
        raw = json.loads(CONTENT_MANIFEST.read_text(encoding="utf-8"))
        if isinstance(raw, dict):
            for k in ("entries", "audio", "files"):
                if isinstance(raw.get(k), list):
                    return raw[k], "Content/audio_manifest.json ('%s')" % k
            raise SystemExit("FAIL: Content/audio_manifest.json has no entries list")
        if isinstance(raw, list):
            return raw, "Content/audio_manifest.json (top-level list)"
        raise SystemExit("FAIL: Content/audio_manifest.json is not a list/object")
    print("NOTE: Content/audio_manifest.json not found (Agent C pending) — "
          "using EXACT pinned W1 fetch list (8 entries).")
    return [dict(e) for e in PINNED], "pinned W1 fallback (Content/audio_manifest.json missing)"


def main():
    entries, source = load_entries()
    print("manifest source: %s; entries: %d" % (source, len(entries)))
    required = ("id", "text", "voice", "lang", "rate", "pitch", "style", "format", "file")
    for e in entries:
        missing = [k for k in required if k not in e]
        if missing:
            print("FAIL entry %r: missing keys %s" % (e.get("id"), missing))
            return 1
        if len(e["text"] or "") == 0 or len(e["text"]) > MAX_CHARS:
            print("FAIL entry %s: text length %d (contract: 1..%d, no split for masters)"
                  % (e["id"], len(e["text"] or ""), MAX_CHARS))
            return 1
        if Path(e["file"]).name != e["file"]:
            print("FAIL entry %s: file must be a plain filename, got %r" % (e["id"], e["file"]))
            return 1

    OUT_DIR.mkdir(parents=True, exist_ok=True)
    ok_n, total_bytes = 0, 0
    failures = []
    results = {}  # id -> bytes
    for e in entries:
        rate_hint = map_rate(e["rate"])
        url = build_url(e["text"], e["lang"], rate_hint)
        good, status, ctype, payload = fetch_audio(url)
        # Outer retry-once for any residual failure (Lead decides on persistent fails).
        if not good and status != -1:
            time.sleep(1)
            good, status, ctype, payload = fetch_audio(url)
        if good:
            dest = OUT_DIR / e["file"]
            dest.write_bytes(payload)
            ok_n += 1
            total_bytes += len(payload)
            results[e["id"]] = len(payload)
            print("OK   %-24s %7d bytes  worker_rate=%s  lang=%s" % (
                e["file"], len(payload), rate_hint, e["lang"]))
        else:
            failures.append((e["id"], e["text"], status, ctype, payload))
            print("FAIL %-24s status=%s ctype=%r err=%s" % (e["id"], status, ctype, payload))

    if not failures:
        # Mirror the manifest for the player: JsonUtility needs an object wrapper
        # (it cannot parse a top-level array), consumed by PregenSeeder.
        OUT_MANIFEST.write_text(json.dumps({"entries": entries}, indent=2) + "\n",
                                encoding="utf-8")
        print("wrote %s (mirror, %d entries)" % (OUT_MANIFEST.relative_to(ROOT), len(entries)))

    print("total: %d/%d files, %d bytes" % (ok_n, len(entries), total_bytes))
    for fid, text, status, ctype, err in failures:
        print("  FAIL [%s] %r status=%s ctype=%r err=%s" % (fid, text, status, ctype, err))
    if failures:
        print("FAILURES: %d (no files faked; Lead decides)" % len(failures))
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
