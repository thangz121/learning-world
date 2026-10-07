"""Tooling validation for the WP-1.9.26 review server (NOT a real review).

Starts the server, exercises pack/progress/audio/submit/export endpoints with a
reviewer id `TEST-TOOL`, verifies the blind payload contains no machine fields,
then deletes the test submission file. No human labels are created.
"""
from __future__ import annotations

import json
import subprocess
import sys
import time
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
PHASE = HERE.parent
REVIEWS = PHASE / "artifacts" / "reviews"
PY = r"D:\speech-lab\venvs\p0\Scripts\python.exe"
PORT = 8791


def get(path, binary=False):
    with urllib.request.urlopen(f"http://127.0.0.1:{PORT}{path}", timeout=30) as r:
        data = r.read()
    return data if binary else json.loads(data.decode("utf-8"))


def post(path, obj):
    req = urllib.request.Request(
        f"http://127.0.0.1:{PORT}{path}", data=json.dumps(obj).encode("utf-8"),
        headers={"Content-Type": "application/json"}, method="POST")
    with urllib.request.urlopen(req, timeout=30) as r:
        return json.loads(r.read().decode("utf-8"))


def main():
    proc = subprocess.Popen([PY, str(HERE / "serve_review_1926.py"), str(PORT)],
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        time.sleep(2.5)
        checks = {}
        with urllib.request.urlopen(f"http://127.0.0.1:{PORT}/", timeout=30) as r:
            html = r.read().decode("utf-8")
        checks["html_served"] = "LWE Blind Review" in html
        pack = get("/api/pack?reviewer=TEST-TOOL")
        checks["pack_size"] = len(pack)
        checks["blind_payload_fields"] = sorted(pack[0].keys())
        checks["no_machine_fields"] = not any(
            k in pack[0] for k in ("machine_max_A", "production_decision", "historical_label"))
        audio = get(f"/audio/{pack[0]['blind_id']}", binary=True)
        checks["audio_bytes"] = len(audio)
        post("/api/submit", {"reviewer_id": "TEST-TOOL", "blind_id": pack[0]["blind_id"],
                             "label": "UNCERTAIN", "confidence": "LOW",
                             "note": "TOOLING TEST ONLY - delete", "ts": "test"})
        checks["progress"] = get("/api/progress?reviewer=TEST-TOOL")
        checks["export_rows"] = len(get("/api/export?reviewer=TEST-TOOL"))
        # a second reviewer must not see the first reviewer's file
        checks["reviewer_isolation"] = get("/api/progress?reviewer=TEST-TOOL-B") == []
        print(json.dumps(checks, indent=1))
        ok = (checks["html_served"] and checks["pack_size"] >= 100
              and checks["no_machine_fields"] and checks["audio_bytes"] > 1000
              and checks["progress"] == [pack[0]["blind_id"]]
              and checks["export_rows"] == 1 and checks["reviewer_isolation"])
        print("PIPELINE_TEST", "PASS" if ok else "FAIL")
    finally:
        proc.terminate()
        f = REVIEWS / "TEST-TOOL.jsonl"
        if f.exists():
            f.unlink()
            print("test submission deleted (no labels fabricated)")
    print("DONE test_review_pipeline")


if __name__ == "__main__":
    main()
