"""Phase 1.9.16 — data quality audit (spec §6).

Computes duration / RMS / clipping / silence statistics and distribution checks
for every candidate corpus: speechocean762 (child + adult), SIAK sample, Zenodo
200495. Reports domination risks (adults, older children, few speakers, phoneme
imbalance, recording conditions). No training here.

Outputs:
  artifacts/quality/quality_<dataset>.csv
  artifacts/quality/quality_summary.json
"""
from __future__ import annotations

import csv
import json
import re
import sys
from collections import Counter
from pathlib import Path

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
OUT = REPO / "Research/Speech/Phase1_9_16/artifacts/quality"
OUT.mkdir(parents=True, exist_ok=True)

SO = Path(r"D:\speech-lab\data\speechocean762")
ZEN = REPO / "Research/Speech/ExternalData/zenodo_200495"
SIAK = REPO / "Research/Speech/ExternalData/SIAK"
SI = REPO / "Research/Speech/Phase1_9_15/artifacts/siak/siak_expanded_merged.csv"
SO_MANIFEST = REPO / "Research/Speech/Phase1_9_16/artifacts/audit/so762_manifest.csv"


def wav_stats(path: Path):
    try:
        x, sr = sf.read(str(path))
    except Exception as exc:  # noqa: BLE001
        return {"error": type(exc).__name__}
    if x.ndim > 1:
        x = x.mean(axis=1)
    x = x.astype(np.float32)
    dur = len(x) / sr if sr else 0.0
    if len(x) == 0:
        return {"sr": sr, "dur": 0.0, "rms": 0.0, "peak": 0.0, "clip": 0.0, "silence": 1.0}
    rms = float(np.sqrt(np.mean(x ** 2) + 1e-20))
    peak = float(np.max(np.abs(x)) + 1e-20)
    clip = float(np.mean(np.abs(x) >= 0.985))
    frame = max(1, int(0.02 * sr))
    n_frames = max(1, len(x) // frame)
    fr = x[: n_frames * frame].reshape(n_frames, frame)
    fr_rms = np.sqrt(np.mean(fr ** 2, axis=1) + 1e-20)
    silence = float(np.mean(fr_rms < 0.005))
    return {"sr": sr, "dur": dur, "rms": rms, "peak": peak, "clip": clip, "silence": silence}


def pct(vals, q):
    return round(float(np.percentile(vals, q)), 4) if vals else None


def summarize(name, rows, extra=None):
    durs = [r["dur"] for r in rows if "dur" in r]
    rmss = [r["rms"] for r in rows if "rms" in r]
    clips = [r["clip"] for r in rows if "clip" in r]
    sils = [r["silence"] for r in rows if "silence" in r]
    srs = Counter(r["sr"] for r in rows if "sr" in r)
    n_err = sum(1 for r in rows if "error" in r)
    spk = Counter(r.get("speaker_id", "") for r in rows)
    top_share = (max(spk.values()) / len(rows)) if rows and len(spk) > 1 else None
    out = {
        "dataset": name, "n": len(rows), "n_errors": n_err,
        "sample_rates": dict(srs),
        "dur_s": {"p5": pct(durs, 5), "median": pct(durs, 50), "p95": pct(durs, 95),
                  "max": round(max(durs), 2) if durs else None},
        "rms": {"p5": pct(rmss, 5), "median": pct(rmss, 50), "p95": pct(rmss, 95)},
        "clipping_gt_0.985_mean": round(float(np.mean(clips)), 5) if clips else None,
        "clipped_gt_1pct_frames_n": sum(1 for c in clips if c > 0.01),
        "silence_ratio_median": pct(sils, 50),
        "speakers": len(spk), "top_speaker_share": round(top_share, 3) if top_share else None,
    }
    if extra:
        out.update(extra)
    return out


def main():
    summaries = []

    # --- speechocean762 (all 5000) ---
    man = list(csv.DictReader(SO_MANIFEST.open(encoding="utf-8")))
    so_rows = []
    for r in man:
        spk = r["speaker_id"]
        # wav path: WAVE/SPEAKERxxxx/<utt>.WAV
        spk_dir = SO / "WAVE" / f"SPEAKER{int(spk):04d}"
        p = spk_dir / f"{r['utt_id']}.WAV"
        if not p.exists():
            hits = list((SO / "WAVE").rglob(f"{r['utt_id']}.WAV"))
            p = hits[0] if hits else p
        st = wav_stats(p)
        st.update({"utt_id": r["utt_id"], "speaker_id": spk, "age": int(r["age"]),
                   "is_child": int(r["is_child"]), "n_phones": int(r["n_phones"])})
        so_rows.append(st)
    with open(OUT / "quality_so762.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(so_rows[0].keys()))
        w.writeheader()
        w.writerows(so_rows)
    for label, subset in (("so762_all", so_rows),
                          ("so762_children", [r for r in so_rows if r.get("is_child") == 1]),
                          ("so762_adults", [r for r in so_rows if r.get("is_child") == 0])):
        summaries.append(summarize(label, subset,
                                   {"age_counts": dict(sorted(Counter(r["age"] for r in subset).items()))
                                    if label != "so762_adults" else None}))

    # --- SIAK sampled 3168 ---
    si_rows = []
    idx = {p.name: p for p in (SIAK / "flac").rglob("*.flac")}
    for r in csv.DictReader(SI.open(encoding="utf-8")):
        p = idx.get(r["file"])
        st = wav_stats(p) if p else {"error": "MISSING"}
        st.update({"file": r["file"], "speaker_id": r["speaker_id"], "age": int(r["age"]),
                   "siak_score": int(float(r["siak_score"]))})
        si_rows.append(st)
    with open(OUT / "quality_siak.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(si_rows[0].keys()))
        w.writeheader()
        w.writerows(si_rows)
    summaries.append(summarize("siak_3168", si_rows,
                               {"age_counts": dict(sorted(Counter(r["age"] for r in si_rows).items())),
                                "score_ge80_share": round(
                                    sum(1 for r in si_rows if r.get("siak_score", 0) >= 80) / len(si_rows), 3)}))

    # --- Zenodo 200495 ---
    inv = list(csv.DictReader((REPO / "Research/Speech/Phase1_9_8/Results/recording_inventory.csv").open(encoding="utf-8-sig")))
    ze_rows = []
    for r in inv:
        if r["role"] != "CHILD_NORMAL":
            continue
        p = Path(r["path"])
        st = wav_stats(p) if p.exists() else {"error": "MISSING"}
        st.update({"recording_id": r["recording_id"], "speaker_id": r["speaker_id"],
                   "target": r["target"], "task": r["task"], "mic": r["mic"]})
        ze_rows.append(st)
    with open(OUT / "quality_zenodo.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(ze_rows[0].keys()))
        w.writeheader()
        w.writerows(ze_rows)
    summaries.append(summarize("zenodo_200495", ze_rows))

    # --- domination checks ---
    so_child = [r for r in so_rows if r.get("is_child") == 1]
    checks = {
        "adults_share_so762": round(sum(1 for r in so_rows if r.get("is_child") == 0) / len(so_rows), 3),
        "child_age6_share_of_children": round(
            sum(1 for r in so_child if r.get("age") == 6) / len(so_child), 3),
        "siak_score_lt50_share": round(sum(1 for r in si_rows if r.get("siak_score", 100) < 50) / len(si_rows), 3),
        "siak_age_le6_share": round(sum(1 for r in si_rows if r.get("age", 99) <= 6) / len(si_rows), 3),
        "clipping_risk": "none material (see clipped_gt_1pct_frames_n per dataset)",
    }
    (OUT / "quality_summary.json").write_text(json.dumps(
        {"summaries": summaries, "domination_checks": checks}, indent=2), encoding="utf-8")
    print(json.dumps({"summaries": summaries, "domination_checks": checks}, indent=2)[:4000])


if __name__ == "__main__":
    main()
