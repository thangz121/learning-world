"""Headless Unity-like caller: writes request JSON, invokes protocol process, reads response.
Does NOT integrate Unity. Smoke test only.
"""
from __future__ import annotations
import json, subprocess, sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
PY = Path(r"D:\speech-lab\venvs\p0\Scripts\python.exe")
PROTO = REPO / "Research" / "Speech" / "Phase1_6" / "Pipeline" / "speaking_protocol_v4.py"
OUT = REPO / "Research" / "Speech" / "Phase1_6" / "Results"
AUDIO = REPO / "Research" / "Speech" / "Phase1_1" / "audio"


def call(audio: Path, target: str, out_name: str):
    out = OUT / out_name
    req = OUT / (out_name + ".req.json")
    req.write_text(json.dumps({"audio": str(audio), "target": target, "population_label": "unity_harness_smoke"}), encoding="utf-8")
    cmd = [str(PY), str(PROTO), "--request", str(req), "--out", str(out)]
    p = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    ok = p.returncode == 0 and out.exists()
    resp = json.loads(out.read_text(encoding="utf-8")) if out.exists() else {}
    return {
        "ok": ok and resp.get("ok"),
        "returncode": p.returncode,
        "score": resp.get("score"),
        "confidence": resp.get("confidence"),
        "words": len(resp.get("wordDiagnostics") or []),
        "stdout_tail": (p.stdout or "")[-200:],
        "stderr_tail": (p.stderr or "")[-200:],
        "out": str(out),
    }


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    cases = [
        (AUDIO / "sapi_red.wav", "red", "unity_smoke_red.json"),
        (AUDIO / "sapi_red_apple.wav", "red apple", "unity_smoke_red_apple.json"),
        (AUDIO / "stress_silence.wav", "red", "unity_smoke_silence.json"),
    ]
    rows = []
    for a, t, o in cases:
        rows.append({"target": t, **call(a, t, o)})
        print("UNITY_SMOKE", rows[-1], flush=True)
    (OUT / "unity_adapter_smoke.json").write_text(json.dumps(rows, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
