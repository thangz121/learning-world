"""Serve Phase 1.9.12 human review packs on LAN and receive submissions directly.

Packs: final_consonant, window_expanded. GET serves HumanReview/; POST /submit saves CSV.
Run: python serve_review.py [port]
"""
from __future__ import annotations

import json
import sys
from datetime import datetime
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

HERE = Path(__file__).resolve().parent
PHASE = HERE.parent
HR = PHASE / "HumanReview"
RES = PHASE / "Results"
SUBS = RES / "submissions"
SUBS.mkdir(parents=True, exist_ok=True)

MAX_BODY = 4_000_000
ALLOWED_PACKS = {"final_consonant", "window_expanded", "window_expanded_remaining", "review"}


class Handler(SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def do_POST(self):  # noqa: N802
        if self.path.rstrip("/") != "/submit":
            self.send_error(404, "only /submit")
            return
        try:
            length = int(self.headers.get("Content-Length", 0))
        except ValueError:
            length = 0
        if length <= 0 or length > MAX_BODY:
            self.send_error(400, "bad length")
            return
        try:
            data = json.loads(self.rfile.read(length).decode("utf-8"))
        except Exception as e:
            self.send_error(400, f"bad json: {e}")
            return
        pack = str(data.get("pack") or "review")
        if pack not in ALLOWED_PACKS:
            self.send_error(400, "unknown pack")
            return
        stage = str(data.get("stage", "A")).upper()[:1] or "A"
        csv_text = str(data.get("csv", ""))
        reviewer = str(data.get("reviewer_id") or "human_mobile")[:64]
        ts = datetime.now().strftime("%Y%m%d_%H%M%S")
        rows = max(0, len([ln for ln in csv_text.strip().splitlines() if ln.strip()]) - 1)

        fname = f"{pack}_Stage{stage}_Filled.csv"
        (RES / fname).write_text(csv_text, encoding="utf-8")
        safe = "".join(ch for ch in reviewer if ch.isalnum() or ch in "._-") or "reviewer"
        hist = SUBS / f"{pack}_Stage{stage}_{ts}_{safe}.csv"
        hist.write_text(csv_text, encoding="utf-8")

        receipt = {"ok": True, "pack": pack, "stage": stage, "rows": rows, "saved": fname, "at": ts}
        body = json.dumps(receipt).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)
        print(f"SUBMISSION pack={pack} stage={stage} rows={rows} reviewer={reviewer} -> {fname}", flush=True)


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8768
    handler = partial(Handler, directory=str(HR))
    httpd = ThreadingHTTPServer(("0.0.0.0", port), handler)
    print(f"serving {HR} on http://0.0.0.0:{port} (submissions -> {RES})", flush=True)
    httpd.serve_forever()


if __name__ == "__main__":
    main()
