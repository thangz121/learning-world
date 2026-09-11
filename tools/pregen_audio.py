#!/usr/bin/env python3
"""W0-T1 pre-gen audio downloader (Agent D). Stdlib only.

Reads Content/vocab (ACTIVE only, NORMAL rate) + Content/dialogues/manifest.json
(all lines), GETs the Cloudflare Worker (?text=&lang=en-US&rate=normal), saves:

  Content/audio/vocab/<id>_normal.mp3
  Content/audio/dialog/<lineid>.mp3

Verifies every response (HTTP 200 + content-type contains audio/mpeg + size>0),
then prints and writes a report.

Slow assets are pitch-preserving time-stretches produced later (AUDIO_DESIGN 5b) --
this script NEVER downloads worker rate=slow for masters.

Usage (run from repo root):
  python tools/pregen_audio.py --probe-only   # one text=Apple call -> tools/pregen_probe_result.txt
  python tools/pregen_audio.py --dry-run      # list the request set, no network
  python tools/pregen_audio.py                # full download (15 vocab + 36 dialog) + report
"""
import datetime
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

BASE_URL = "https://round-mud-63dd.hoaithuong1995cdmna.workers.dev/"
LANG = "en-US"
RATE = "normal"  # masters are NORMAL only; slow via time-stretch later
MAX_CHARS = 200  # WorkerTtsContract: text<=200, Unity/Python must split first
TIMEOUT_SEC = 25
RETRY_5XX_ONCE = True

ROOT = Path(__file__).resolve().parent.parent
VOCAB_DIR = ROOT / "Content" / "vocab"
MANIFEST = ROOT / "Content" / "dialogues" / "manifest.json"
AUDIO_VOCAB = ROOT / "Content" / "audio" / "vocab"
AUDIO_DIALOG = ROOT / "Content" / "audio" / "dialog"
PROBE_RESULT = Path(__file__).resolve().parent / "pregen_probe_result.txt"
REPORT_FILE = Path(__file__).resolve().parent / "pregen_report.txt"


def split_text(text, max_chars=MAX_CHARS):
    """Mirror of CloudflareTranslateTtsProvider.SplitForWorker: sentence, space, hard cut."""
    t = (text or "").strip()
    if not t:
        return []
    if len(t) <= max_chars:
        return [t]
    chunks, i = [], 0
    while i < len(t):
        end = min(i + max_chars, len(t))
        if end == len(t):
            tail = t[i:].strip()
            if tail:
                chunks.append(tail)
            break
        cut = -1
        for j in range(end - 1, i - 1, -1):
            if t[j] in ".!?;" and (j + 1 >= len(t) or t[j + 1].isspace()):
                cut = j + 1
                break
        if cut <= i:
            sp = t.rfind(" ", i, end)
            cut = sp if sp > i else end
        chunk = t[i:cut].strip()
        if chunk:
            chunks.append(chunk)
        i = cut
        while i < len(t) and t[i].isspace():
            i += 1
    return chunks


def build_url(text, lang=LANG, rate=RATE):
    q = "text=%s&lang=%s&rate=%s" % (
        urllib.parse.quote(text, safe=""),
        urllib.parse.quote(lang, safe=""),
        urllib.parse.quote(rate, safe=""),
    )
    return BASE_URL.rstrip("/") + "/?" + q


def fetch_audio(url):
    """GET one URL. Returns (ok, status, content_type, data_or_error_repr)."""
    last_err = "unknown"
    for attempt in (1, 2):
        try:
            req = urllib.request.Request(
                url, headers={"User-Agent": "LWE-pregen/1.0", "Accept": "audio/mpeg"})
            with urllib.request.urlopen(req, timeout=TIMEOUT_SEC) as resp:
                status = getattr(resp, "status", 200)
                ctype = resp.headers.get("Content-Type", "")
                data = resp.read()
            if status != 200:
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
            if e.code == 429 and attempt == 1:
                time.sleep(3)
                last_err = "http-429-then-retry"
                continue
            if 500 <= e.code < 600 and attempt == 1 and RETRY_5XX_ONCE:
                last_err = "http-%s-then-retry" % e.code
                continue
            return False, e.code, ctype, "http-error-%s" % e.code
        except Exception as e:  # URLError, TimeoutError, OSError (offline sandbox, DNS, ...)
            last_err = "%s: %s" % (type(e).__name__, e)
            if attempt == 1 and RETRY_5XX_ONCE:
                time.sleep(1)
                continue
            return False, -1, "", last_err
    return False, -1, "", last_err


def collect_requests():
    """[(kind, file_id, text)] — 15 active vocab NORMAL + every manifest line."""
    reqs = []
    for p in sorted(VOCAB_DIR.glob("*.json")):
        v = json.loads(p.read_text(encoding="utf-8"))
        if v.get("active") is True:
            text = ((v.get("display") or {}).get("en") or "").strip()
            if not text:
                raise SystemExit("FAIL: active vocab %s has no display.en" % p.name)
            for chunk in split_text(text):
                reqs.append(("vocab", v["id"], chunk))
    pack = json.loads(MANIFEST.read_text(encoding="utf-8"))
    for ln in pack.get("lines") or []:
        for chunk in split_text(ln.get("text", "")):
            reqs.append(("dialog", ln["id"], chunk))
    return reqs


def probe_only():
    url = build_url("Apple")
    ok, status, ctype, payload = fetch_audio(url)
    now = datetime.datetime.now(datetime.timezone.utc).isoformat()
    size = len(payload) if ok else 0
    outcome = "SUCCESS" if ok else "FAILURE"
    body = (
        "LWE pregen probe (Agent D, W0-T1)\n"
        "time_utc: %s\n"
        "base_url: %s\n"
        "request: GET ?text=Apple&lang=%s&rate=%s\n"
        "http_status: %s\n"
        "content_type: %s\n"
        "bytes: %s\n"
        "outcome: %s\n"
        "detail: %s\n" % (now, BASE_URL, LANG, RATE, status, ctype, size, outcome,
                          ("audio/mpeg ok" if ok else payload))
    )
    PROBE_RESULT.write_text(body, encoding="utf-8")
    print(body)
    return 0 if ok else 1


def main(argv):
    if "--probe-only" in argv:
        return probe_only()
    reqs = collect_requests()
    print("requests: %d (vocab=%d dialog=%d)" % (
        len(reqs), sum(1 for r in reqs if r[0] == "vocab"),
        sum(1 for r in reqs if r[0] == "dialog")))
    if "--dry-run" in argv:
        for kind, fid, text in reqs:
            print("  [%s] %s <- %r (%d chars)" % (kind, fid, text, len(text)))
        return 0
    AUDIO_VOCAB.mkdir(parents=True, exist_ok=True)
    AUDIO_DIALOG.mkdir(parents=True, exist_ok=True)
    ok_n, fail = 0, []
    total_bytes = 0
    for kind, fid, text in reqs:
        url = build_url(text)
        good, status, ctype, payload = fetch_audio(url)
        dest = (AUDIO_VOCAB / ("%s_normal.mp3" % fid)) if kind == "vocab" \
            else (AUDIO_DIALOG / ("%s.mp3" % fid))
        if good:
            dest.write_bytes(payload)
            ok_n += 1
            total_bytes += len(payload)
            print("OK   %-28s %7d bytes" % (dest.relative_to(ROOT), len(payload)))
        else:
            fail.append((kind, fid, text, status, ctype, payload))
            print("FAIL %-28s status=%s ctype=%r err=%s" % (fid, status, ctype, payload))
    now = datetime.datetime.now(datetime.timezone.utc).isoformat()
    report = ("LWE pregen report (Agent D, W0-T1)\ntime_utc: %s\nbase: %s\nlang=%s rate=%s\n"
              "downloaded: %d/%d files, %d bytes\nfailures: %d\n" % (
                  now, BASE_URL, LANG, RATE, ok_n, len(reqs), total_bytes, len(fail)))
    for kind, fid, text, status, ctype, err in fail:
        report += "  FAIL [%s] %s %r status=%s ctype=%r err=%s\n" % (
            kind, fid, text, status, ctype, err)
    REPORT_FILE.write_text(report, encoding="utf-8")
    print(report)
    return 0 if not fail else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
