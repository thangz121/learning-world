"""Phase 1.9.16 — model/runtime + license evidence (spec §20/§21).

Measures cold model load, warm inference latency and peak working set on the
ASUS CPU-only box, and records sizes/licenses. No training.

Output: artifacts/runtime/runtime_license.json
"""
from __future__ import annotations

import ctypes
import ctypes.wintypes as wintypes
import json
import sys
import time
from pathlib import Path

import soundfile as sf
import numpy as np

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO))
OUT = REPO / "Research/Speech/Phase1_9_16/artifacts/runtime"
OUT.mkdir(parents=True, exist_ok=True)


class PROCESS_MEMORY_COUNTERS(ctypes.Structure):
    _fields_ = [("cb", wintypes.DWORD), ("PageFaultCount", wintypes.DWORD),
                ("PeakWorkingSetSize", ctypes.c_size_t), ("WorkingSetSize", ctypes.c_size_t),
                ("QuotaPeakPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPagedPoolUsage", ctypes.c_size_t),
                ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t),
                ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
                ("PagefileUsage", ctypes.c_size_t),
                ("PeakPagefileUsage", ctypes.c_size_t)]


def peak_mb():
    c = PROCESS_MEMORY_COUNTERS()
    c.cb = ctypes.sizeof(c)
    fn = ctypes.windll.kernel32.K32GetProcessMemoryInfo
    fn.argtypes = [wintypes.HANDLE, ctypes.POINTER(PROCESS_MEMORY_COUNTERS), wintypes.DWORD]
    fn.restype = wintypes.BOOL
    ok = fn(ctypes.windll.kernel32.GetCurrentProcess(), ctypes.byref(c), c.cb)
    return round(c.PeakWorkingSetSize / (1024 * 1024), 0) if ok else None


def main():
    t0 = time.perf_counter()
    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
    pev = PhoneEvidenceV2()
    pev._ensure()
    cold = time.perf_counter() - t0

    tgt = __import__("Research.Speech.Phase1_2.Adapters.target_cmudict",
                     fromlist=["CmuDictTargetAdapter"]).CmuDictTargetAdapter()
    wav = REPO / "Research/Speech/Phase1_9_12/HumanReview/fc_clips/fc_child_01_four.wav"
    x, sr = sf.read(str(wav))
    st = tgt.build("four")
    t0 = time.perf_counter()
    pev.soft_match(str(wav), st.arpabet)
    warm = time.perf_counter() - t0
    peak = peak_mb()

    snap_root = Path(r"D:\speech-lab\models\models--facebook--wav2vec2-xlsr-53-espeak-cv-ft\snapshots")
    model_bins = list(snap_root.rglob("pytorch_model.bin"))
    safets = [p for p in snap_root.rglob("model.safetensors") if p.stat().st_size > 0]
    model_bin = model_bins[0] if model_bins else snap_root / "pytorch_model.bin"
    safet = safets[0] if safets else snap_root / "model.safetensors"
    head = REPO / "Research/Speech/Phase1_9_16/artifacts/ab/head.pt"

    out = {
        "phase": "1.9.16",
        "runtime": {
            "cold_load_s": round(cold, 1),
            "warm_soft_match_s_short_word": round(warm, 3),
            "peak_working_set_mb_after_one_inference": peak,
            "cpu_only": True,
            "torch": "2.14.1+cpu",
            "note": "ASUS Windows; single process; model+torch overhead included",
        },
        "sizes": {
            "pytorch_model_bin_bytes": model_bin.stat().st_size if model_bin.exists() else None,
            "model_safetensors_bytes": safet.stat().st_size if safet.exists() else None,
            "head_state_bytes": head.stat().st_size if head.exists() else None,
            "head_params_estimate": "2xConv1d(1024->512, 512->392, k=5) ~3.6M",
        },
        "model_revision": {
            "id": "facebook/wav2vec2-xlsr-53-espeak-cv-ft",
            "local_snapshot_sha": "2c733782da5604684829819a5eb744c193fe9398",
        },
        "licenses": {
            "wav2vec2-xlsr-53-espeak-cv-ft": {
                "license": "Apache-2.0 (verified via HF model_info 2026-10-02)",
                "training_use": "allowed", "commercial": "allowed"},
            "speechocean762 (SLR101)": {
                "license": "CC BY 4.0 (openslr.org/101, fetched 2026-10-03)",
                "training_use": "allowed", "commercial": "allowed", "redistribution": "allowed with attribution"},
            "SIAK": {
                "license": "CC-BY-ND-4.0",
                "training_use": "dataset README: commercial use for building/evaluating speech "
                                 "technology models not prohibited; no unrelated derivatives",
                "commercial": "nuanced (legal review open)", "note": "used only as auxiliary "
                                                                    "acoustic data in B1 (weights from ratings)"},
            "Zenodo 200495": {
                "license": "CC-BY-4.0", "training_use": "NOT USED (test only)",
                "commercial": "allowed", "note": "LWE external evaluation only"},
            "Moonshine-tiny (ASR, supporting only)": {
                "license": "MIT (per Phase 1.1 registry)", "training_use": "n/a",
                "commercial": "allowed"},
        },
        "flags": {"production_vad": False, "router_locked": False, "unity_integrated": False,
                  "scorer_modified": False, "production_window_locked": False},
    }
    (OUT / "runtime_license.json").write_text(json.dumps(out, indent=2), encoding="utf-8")
    print(json.dumps(out, indent=2)[:3000])


if __name__ == "__main__":
    main()
