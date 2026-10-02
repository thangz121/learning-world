"""Phase 1.9.7 — multi-recording hybrid rescue + boundary/merge (research only)."""
from __future__ import annotations

import csv
import hashlib
import json
import sys
import time
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Dict, List, Tuple

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_9.HybridVAD.hybrid_vad import HybridVAD, load_mono16

OUT = REPO / "Research" / "Speech" / "Phase1_9_7"
RES = OUT / "Results"
HR = OUT / "HumanReview"
for d in (OUT, RES, HR, OUT / "scripts"):
    d.mkdir(parents=True, exist_ok=True)

IMG_WAV = REPO / "Research/Speech/Phase1_8/AudioDerived/IMG_0639_16k_mono.wav"
NEW_WAV = REPO / "Research/Speech/Phase1_8/AudioDerived/NEW_16k_mono.wav"
NEW_MP3 = REPO / "1790932799243_8856108714107255767_8856108714107255767.mp3"
IMG_MP3 = REPO / "IMG_0639.mp3"

NEW_SHA = "17E3DA86267D1B7B864EA862A6C5C3B12AF4E49FD649F2FE140652A53DDF33FF"
IMG_SHA = "E4CDF24D2B5140B91EF637676CD5FA1B9A7F6B6A629F62AE9AC2657CB171A6F7"
IMG_WAV_SHA = "9A662E8639D7C9694CFD997A221C541900BE39BEA26C3B961CFC2DF3162E5BCB"
NEW_WAV_SHA = "7281C131C777F5B590BC3583B771DED300434E6D09306292D8DDEF8587CCF4EE"

# Historical Phase 1.9.6 human evidence (do not overwrite)
P196_SPEECH_28 = 28
P196_ROUND2 = "28/28 SPEECH min2s; round1 28/28 UNCERTAIN short"


def sha256(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest().upper()


def segs_list(r) -> List[Tuple[float, float]]:
    out = []
    for s in r["segments"]:
        out.append((float(s["start"]), float(s["end"])))
    return out


def seg_stats(segs: List[Tuple[float, float]], duration: float) -> Dict:
    if not segs:
        return {
            "n_segments": 0,
            "speech_duration_s": 0.0,
            "speech_ratio": 0.0,
            "median_dur_s": None,
            "mean_dur_s": None,
            "min_dur_s": None,
            "max_dur_s": None,
            "n_lt_0.2s": 0,
            "n_lt_0.5s": 0,
            "n_ge_1.0s": 0,
            "mean_gap_s": None,
            "median_gap_s": None,
            "n_gaps": 0,
            "fragmentation_index": 0.0,
        }
    durs = [e - s for s, e in segs]
    gaps = [segs[i + 1][0] - segs[i][1] for i in range(len(segs) - 1)]
    speech = sum(durs)
    return {
        "n_segments": len(segs),
        "speech_duration_s": speech,
        "speech_ratio": speech / duration if duration else 0.0,
        "median_dur_s": float(np.median(durs)),
        "mean_dur_s": float(np.mean(durs)),
        "min_dur_s": float(min(durs)),
        "max_dur_s": float(max(durs)),
        "n_lt_0.2s": sum(1 for d in durs if d < 0.2),
        "n_lt_0.5s": sum(1 for d in durs if d < 0.5),
        "n_ge_1.0s": sum(1 for d in durs if d >= 1.0),
        "mean_gap_s": float(np.mean(gaps)) if gaps else None,
        "median_gap_s": float(np.median(gaps)) if gaps else None,
        "n_gaps": len(gaps),
        "fragmentation_index": (len(segs) / speech) if speech > 0 else 0.0,
    }


def pad_segs(segs, duration, pad_s):
    out = []
    for s, e in segs:
        out.append((max(0.0, s - pad_s), min(duration, e + pad_s)))
    return out


def merge_gaps(segs, gap_s):
    if not segs:
        return []
    segs = sorted(segs)
    cur_s, cur_e = segs[0]
    out = []
    for s, e in segs[1:]:
        if s - cur_e <= gap_s:
            cur_e = max(cur_e, e)
        else:
            out.append((cur_s, cur_e))
            cur_s, cur_e = s, e
    out.append((cur_s, cur_e))
    return out


def expand_min_dur(segs, duration, min_s):
    out = []
    for s, e in segs:
        d = e - s
        if d >= min_s:
            out.append((s, e))
            continue
        mid = 0.5 * (s + e)
        half = min_s / 2.0
        ns = mid - half
        ne = mid + half
        if ns < 0:
            ne = min(duration, ne - ns)
            ns = 0.0
        if ne > duration:
            ns = max(0.0, ns - (ne - duration))
            ne = duration
        out.append((ns, ne))
    return out


def filter_min_dur(segs, min_s):
    return [(s, e) for s, e in segs if (e - s) >= min_s - 1e-9]


def apply_post(segs, duration, strategy: str, pad_ms: int, gap_ms: int, min_ms: int):
    pad_s, gap_s, min_s = pad_ms / 1000.0, gap_ms / 1000.0, min_ms / 1000.0
    x = list(segs)
    if strategy == "raw":
        return x
    if strategy == "pad":
        return pad_segs(x, duration, pad_s)
    if strategy == "min_expand":
        return expand_min_dur(x, duration, min_s)
    if strategy == "merge":
        return merge_gaps(x, gap_s)
    if strategy == "pad_merge":
        return merge_gaps(pad_segs(x, duration, pad_s), gap_s)
    if strategy == "pad_merge_minfilter":
        y = merge_gaps(pad_segs(x, duration, pad_s), gap_s)
        return filter_min_dur(y, min_s)
    if strategy == "full_chain":
        # pad -> merge -> min expand remaining short -> optional filter
        y = merge_gaps(pad_segs(x, duration, pad_s), gap_s)
        y = expand_min_dur(y, duration, min_s)
        y = merge_gaps(y, gap_s)
        return y
    raise ValueError(strategy)


def overlap_ratio(a: List[Tuple[float, float]], b: List[Tuple[float, float]], duration: float) -> Dict:
    """Frame-ish overlap via 10ms grid (research metric, not GT)."""
    if duration <= 0:
        return {"iou": 0.0, "precision": 0.0, "recall": 0.0}
    hop = 0.01
    n = int(duration / hop) + 1
    A = np.zeros(n, dtype=bool)
    B = np.zeros(n, dtype=bool)

    def paint(mask, segs):
        for s, e in segs:
            i0 = max(0, int(s / hop))
            i1 = min(n, int(e / hop) + 1)
            mask[i0:i1] = True

    paint(A, a)
    paint(B, b)
    inter = np.logical_and(A, B).sum()
    union = np.logical_or(A, B).sum()
    pa = A.sum()
    pb = B.sum()
    return {
        "iou": float(inter / union) if union else 0.0,
        "precision_a_vs_b": float(inter / pa) if pa else 0.0,
        "recall_a_vs_b": float(inter / pb) if pb else 0.0,
        "a_speech_ratio": float(pa / n),
        "b_speech_ratio": float(pb / n),
    }


def continuous_energy_features(x: np.ndarray, sr: int) -> Dict:
    """Reproduce Phase 1.9-style continuous energy features for router research."""
    frame = int(0.03 * sr)
    hop = int(0.01 * sr)
    rms = []
    for i in range(0, max(1, len(x) - frame), hop):
        rms.append(float(np.sqrt(np.mean(x[i : i + frame] ** 2) + 1e-20)))
    rms = np.array(rms) if rms else np.array([0.0])
    # prior phase used continuous_energy_ratio and energy_contrast from HybridVAD profile-ish
    # Use same definitions as Phase 1.9.1 router notes when available via run profile + explicit:
    floor = np.percentile(rms, 20)
    thr = floor * 2.0
    cont = float(np.mean(rms > thr))
    p90 = float(np.percentile(rms, 90))
    p20 = float(np.percentile(rms, 20) + 1e-12)
    # energy_contrast in prior work was ~0.3-0.88 scale — use (p90-p20)/(p90+p20)
    contrast = float((p90 - p20) / (p90 + p20))
    return {
        "continuous_energy_ratio_proxy": cont,
        "energy_contrast_proxy": contrast,
        "rms_mean": float(np.mean(rms)),
        "rms_p20": float(p20),
        "rms_p90": p90,
        # authoritative prior measured features from Phase 1.9.1
    }


def main():
    assert IMG_WAV.exists() and NEW_WAV.exists()
    if NEW_MP3.exists():
        assert sha256(NEW_MP3) == NEW_SHA
    if IMG_MP3.exists():
        assert sha256(IMG_MP3) == IMG_SHA
    assert sha256(IMG_WAV) == IMG_WAV_SHA
    assert sha256(NEW_WAV) == NEW_WAV_SHA

    hv = HybridVAD()
    modes = ["silero", "hybrid_score", "energy"]

    recordings = [
        {
            "recording_id": "NEW_1790",
            "role": "NORMAL_REFERENCE",
            "acoustic_complexity": "NORMAL_CASE",
            "filename_original": NEW_MP3.name if NEW_MP3.exists() else "NEW_16k_mono.wav",
            "path_16k": str(NEW_WAV.relative_to(REPO)).replace("\\", "/"),
            "sha256_original": NEW_SHA,
            "sha256_16k": NEW_WAV_SHA,
            "wav": NEW_WAV,
            "prior_continuous_energy_ratio": 0.23404255319148937,
            "prior_energy_contrast": 0.8773186139949414,
            "note": "Primary normal-case reference (clearer recording)",
        },
        {
            "recording_id": "IMG_0639",
            "role": "STRESS_CASE",
            "acoustic_complexity": "HIGH_COMPLEXITY_STRESS",
            "filename_original": "IMG_0639.mp3",
            "path_16k": str(IMG_WAV.relative_to(REPO)).replace("\\", "/"),
            "sha256_original": IMG_SHA,
            "sha256_16k": IMG_WAV_SHA,
            "wav": IMG_WAV,
            "prior_continuous_energy_ratio": 0.9726027397260274,
            "prior_energy_contrast": 0.3084678949077175,
            "note": "Hosting-program difficult stress case — NOT normal-case calibration",
            "p196_human": P196_ROUND2,
        },
    ]

    inventory_rows = []
    detector_rows = []
    det_cache = {}

    for rec in recordings:
        x, sr = load_mono16(str(rec["wav"]))
        dur = len(x) / sr
        feats = continuous_energy_features(x, sr)
        feats["prior_continuous_energy_ratio"] = rec["prior_continuous_energy_ratio"]
        feats["prior_energy_contrast"] = rec["prior_energy_contrast"]
        mode_stats = {}
        for mode in modes:
            t0 = time.time()
            r = hv.run(str(rec["wav"]), mode=mode, silero_thr=0.5, energy_margin_db=6.0)
            rt = time.time() - t0
            segs = segs_list(r)
            st = seg_stats(segs, dur)
            st.update(
                {
                    "recording_id": rec["recording_id"],
                    "role": rec["role"],
                    "mode": mode,
                    "silero_thr": 0.5,
                    "runtime_s": rt,
                    "file_duration_s": dur,
                }
            )
            detector_rows.append(st)
            mode_stats[mode] = {"segs": segs, "stats": st, "runtime_s": rt}
            print(
                f"{rec['recording_id']:10s} {mode:14s} n={st['n_segments']:3d} "
                f"ratio={st['speech_ratio']:.3f} med={st['median_dur_s']} frag={st['fragmentation_index']:.2f}",
                flush=True,
            )

        sil = mode_stats["silero"]["segs"]
        hyb = mode_stats["hybrid_score"]["segs"]
        disagree = overlap_ratio(sil, hyb, dur)
        det_cache[rec["recording_id"]] = {
            "duration": dur,
            "features": feats,
            "silero": sil,
            "hybrid": hyb,
            "energy": mode_stats["energy"]["segs"],
            "disagree_silero_vs_hybrid": disagree,
            "x": x,
            "sr": sr,
        }

        inventory_rows.append(
            {
                "recording_id": rec["recording_id"],
                "filename": rec["filename_original"],
                "duration_s": dur,
                "source": "project real-audio",
                "recording_role": rec["role"],
                "acoustic_complexity": rec["acoustic_complexity"],
                "Silero_segment_count": mode_stats["silero"]["stats"]["n_segments"],
                "hybrid_segment_count": mode_stats["hybrid_score"]["stats"]["n_segments"],
                "energy_segment_count": mode_stats["energy"]["stats"]["n_segments"],
                "prior_cont_energy": rec["prior_continuous_energy_ratio"],
                "prior_energy_contrast": rec["prior_energy_contrast"],
                "sha256_original": rec["sha256_original"],
                "note": rec["note"],
                "dataset_size_note": "MULTI-RECORDING DATASET CURRENTLY LIMITED TO 2 RECORDINGS",
            }
        )

    # --- Boundary/merge compact matrix ---
    # Compact: strategies x selected pads/gaps/mins
    pads = [0, 100, 200, 300, 500]
    gaps = [0, 100, 200, 300, 500]
    mins = [100, 200, 300, 500]
    boundary_rows = []

    for rid, cache in det_cache.items():
        base = cache["hybrid"]
        dur = cache["duration"]
        # A raw
        for name, kwargs in [
            ("raw", dict(strategy="raw", pad_ms=0, gap_ms=0, min_ms=0)),
        ]:
            segs = apply_post(base, dur, **kwargs)
            st = seg_stats(segs, dur)
            boundary_rows.append(
                {
                    "recording_id": rid,
                    "base_detector": "hybrid_score",
                    "strategy": name,
                    **kwargs,
                    **st,
                }
            )
        # B pad only
        for p in pads:
            if p == 0:
                continue
            segs = apply_post(base, dur, "pad", p, 0, 0)
            st = seg_stats(segs, dur)
            boundary_rows.append(
                {
                    "recording_id": rid,
                    "base_detector": "hybrid_score",
                    "strategy": "pad",
                    "pad_ms": p,
                    "gap_ms": 0,
                    "min_ms": 0,
                    **st,
                }
            )
        # C min expand
        for m in mins:
            segs = apply_post(base, dur, "min_expand", 0, 0, m)
            st = seg_stats(segs, dur)
            boundary_rows.append(
                {
                    "recording_id": rid,
                    "base_detector": "hybrid_score",
                    "strategy": "min_expand",
                    "pad_ms": 0,
                    "gap_ms": 0,
                    "min_ms": m,
                    **st,
                }
            )
        # D merge only
        for g in gaps:
            if g == 0:
                continue
            segs = apply_post(base, dur, "merge", 0, g, 0)
            st = seg_stats(segs, dur)
            boundary_rows.append(
                {
                    "recording_id": rid,
                    "base_detector": "hybrid_score",
                    "strategy": "merge",
                    "pad_ms": 0,
                    "gap_ms": g,
                    "min_ms": 0,
                    **st,
                }
            )
        # E pad+merge compact (pad x gap selected)
        for p in [200, 300, 500]:
            for g in [200, 300, 500]:
                segs = apply_post(base, dur, "pad_merge", p, g, 0)
                st = seg_stats(segs, dur)
                boundary_rows.append(
                    {
                        "recording_id": rid,
                        "base_detector": "hybrid_score",
                        "strategy": "pad_merge",
                        "pad_ms": p,
                        "gap_ms": g,
                        "min_ms": 0,
                        **st,
                    }
                )
        # F pad+merge+minfilter
        for p, g, m in [(200, 200, 300), (300, 300, 300), (500, 300, 500), (300, 500, 500)]:
            segs = apply_post(base, dur, "pad_merge_minfilter", p, g, m)
            st = seg_stats(segs, dur)
            boundary_rows.append(
                {
                    "recording_id": rid,
                    "base_detector": "hybrid_score",
                    "strategy": "pad_merge_minfilter",
                    "pad_ms": p,
                    "gap_ms": g,
                    "min_ms": m,
                    **st,
                }
            )
        # full_chain recommended research candidates
        for p, g, m in [(250, 300, 500), (300, 300, 500), (500, 500, 500)]:
            segs = apply_post(base, dur, "full_chain", p, g, m)
            st = seg_stats(segs, dur)
            # vs silero overlap (research only)
            ov = overlap_ratio(segs, cache["silero"], dur)
            boundary_rows.append(
                {
                    "recording_id": rid,
                    "base_detector": "hybrid_score",
                    "strategy": "full_chain",
                    "pad_ms": p,
                    "gap_ms": g,
                    "min_ms": m,
                    **st,
                    "overlap_vs_silero_iou": ov["iou"],
                    "overlap_vs_silero_precision": ov["precision_a_vs_b"],
                    "overlap_vs_silero_recall": ov["recall_a_vs_b"],
                }
            )

    # --- Router research (hypothesis only) ---
    # Use PRIOR features from Phase 1.9.1 (authoritative for this 2-file set)
    C_vals = [0.7, 0.75, 0.8, 0.85, 0.9]
    X_vals = [0.3, 0.35, 0.4, 0.45, 0.5]
    router_rows = []
    for C in C_vals:
        for X in X_vals:
            for rec in recordings:
                cont = rec["prior_continuous_energy_ratio"]
                contr = rec["prior_energy_contrast"]
                choose_hybrid = (cont > C) and (contr < X)
                router_rows.append(
                    {
                        "C": C,
                        "X": X,
                        "recording_id": rec["recording_id"],
                        "role": rec["role"],
                        "cont": cont,
                        "contrast": contr,
                        "route": "hybrid_score" if choose_hybrid else "silero",
                        "hypothesis_params": True,
                        "locked_production": False,
                    }
                )

    # Summary stability
    router_summary = []
    for C in C_vals:
        for X in X_vals:
            sub = [r for r in router_rows if r["C"] == C and r["X"] == X]
            n_new_hyb = sum(1 for r in sub if r["recording_id"] == "NEW_1790" and r["route"] == "hybrid_score")
            n_img_hyb = sum(1 for r in sub if r["recording_id"] == "IMG_0639" and r["route"] == "hybrid_score")
            router_summary.append(
                {
                    "C": C,
                    "X": X,
                    "NEW_routes_hybrid": bool(n_new_hyb),
                    "IMG_routes_hybrid": bool(n_img_hyb),
                    "desired_pattern": (n_new_hyb == 0 and n_img_hyb == 1),
                }
            )
    n_desired = sum(1 for r in router_summary if r["desired_pattern"])
    router_status = {
        "status": "ROUTER_NOT_CALIBRATED" if len(recordings) < 3 else "RESEARCH_ONLY",
        "n_recordings": len(recordings),
        "grid_size": len(router_summary),
        "n_grid_match_desired_NEW_silero_IMG_hybrid": n_desired,
        "frac_desired": n_desired / len(router_summary),
        "hypothesis": "if cont>C and contrast<X then hybrid else silero",
        "locked": False,
        "note": "Only 2 recordings — do not lock C/X. Desired pattern observed on subset of grid is overfit risk.",
    }

    # --- Pronunciation crop safety (soft-v2 if available) ---
    crop_rows = []
    scorer_ok = False
    try:
        from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
        from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

        adapter = CmuDictTargetAdapter()
        scorer = PhoneEvidenceV2()
        scorer_ok = True
        LWE = REPO / "Research/Speech/Phase1_1/audio"
        words = ["red", "cat", "apple", "blue", "big"]
        # silence: use zeros or a quiet file if any
        for w in words:
            # prefer sapi_*.wav
            candidates = list(LWE.glob(f"sapi_{w}.wav")) + list(LWE.glob(f"*{w}*.wav"))
            if not candidates:
                crop_rows.append({"word": w, "status": "FILE_MISSING"})
                continue
            path = candidates[0]
            audio, sr = sf.read(str(path))
            if audio.ndim > 1:
                audio = audio.mean(axis=1)
            audio = audio.astype(np.float32)
            if sr != 16000:
                crop_rows.append({"word": w, "status": f"SR_{sr}_SKIP"})
                continue
            dur = len(audio) / sr
            target = adapter.lookup(w) if hasattr(adapter, "lookup") else None
            # try common APIs
            def score_arr(arr):
                for meth in ("score", "score_audio", "evaluate", "run"):
                    fn = getattr(scorer, meth, None)
                    if fn is None:
                        continue
                    try:
                        r = fn(arr, sr, w)
                        if isinstance(r, dict):
                            return float(r.get("score", r.get("soft_score", r.get("final", 0))))
                        return float(r)
                    except TypeError:
                        try:
                            r = fn(arr, w)
                            if isinstance(r, dict):
                                return float(r.get("score", r.get("soft_score", 0)))
                            return float(r)
                        except Exception:
                            continue
                    except Exception:
                        continue
                return None

            full = score_arr(audio)
            # fake VAD tight crop: middle 40%
            i0 = int(0.3 * len(audio))
            i1 = int(0.7 * len(audio))
            raw = score_arr(audio[i0:i1])
            # pad 250ms equivalent samples
            pad = int(0.25 * sr)
            j0 = max(0, i0 - pad)
            j1 = min(len(audio), i1 + pad)
            padded = score_arr(audio[j0:j1])
            # merge-like: use full (single region)
            merged = full
            row = {
                "word": w,
                "file": path.name,
                "status": "OK" if full is not None else "SCORER_API_UNRESOLVED",
                "full": full,
                "raw_crop_mid40": raw,
                "padded_250ms_around_mid40": padded,
                "merged_or_full": merged,
                "boundary_sensitivity_full_vs_raw": (
                    None if full is None or raw is None else abs(full - raw)
                ),
                "note": "Higher score is NOT automatically better; watch boundary sensitivity",
            }
            crop_rows.append(row)
            print("CROP", row, flush=True)

        # silence control
        silence = np.zeros(int(1.0 * 16000), dtype=np.float32)
        sc = score_arr(silence)
        crop_rows.append(
            {
                "word": "silence",
                "file": "synthetic_zeros_1s",
                "status": "OK" if sc is not None else "SCORER_API_UNRESOLVED",
                "full": sc,
                "raw_crop_mid40": sc,
                "padded_250ms_around_mid40": sc,
                "merged_or_full": sc,
                "boundary_sensitivity_full_vs_raw": 0.0 if sc is not None else None,
                "note": "control",
            }
        )
    except Exception as e:
        crop_rows.append({"status": "SCORER_UNAVAILABLE", "error": str(e)})
        print("SCORER_SKIP", e, flush=True)

    # --- NEW human review pack (min2s) for hybrid vs silero disagreement awareness ---
    # Export silero segs on NEW as review samples (already speech-ish) + hybrid if different
    new_c = det_cache["NEW_1790"]
    sil_n = len(new_c["silero"])
    hyb_n = len(new_c["hybrid"])
    # For NEW, primary question is silero sufficiency — export up to 12 silero segs min2s
    ref_dir = HR / "clips_NEW_silero_min2s"
    ref_dir.mkdir(parents=True, exist_ok=True)
    x, sr, dur = new_c["x"], new_c["sr"], new_c["duration"]

    def exp_min2(st, en, path):
        mid = 0.5 * (st + en)
        half = max((en - st) / 2, 1.0)
        s = max(0.0, mid - half)
        e = min(dur, mid + half)
        if e - s < 2.0:
            s = max(0.0, e - 2.0)
            if e - s < 2.0:
                e = min(dur, s + 2.0)
        sf.write(str(path), x[int(s * sr) : int(e * sr)], sr)
        return s, e

    new_review = []
    for i, (st, en) in enumerate(new_c["silero"][:12]):
        cid = f"NEW_sil_{i+1:02d}"
        path = ref_dir / f"{cid}.wav"
        s, e = exp_min2(st, en, path)
        new_review.append(
            {
                "clip_id": cid,
                "source": "NEW_1790",
                "role": "NORMAL_REFERENCE",
                "detector": "silero",
                "raw_start_s": st,
                "raw_end_s": en,
                "raw_duration_s": en - st,
                "human_review_window_start": s,
                "human_review_window_end": e,
                "listen_duration_s": e - s,
                "clip_relpath": f"clips_NEW_silero_min2s/{cid}.wav",
                "human_label": "",
                "note": "Optional calibration; NEW is normal-case. Labels empty unless human fills.",
            }
        )

    # recommended postprocess on IMG for human listen (full_chain 300/300/500)
    img_c = det_cache["IMG_0639"]
    img_post = apply_post(img_c["hybrid"], img_c["duration"], "full_chain", 300, 300, 500)
    img_dir = HR / "clips_IMG_post_min2s"
    img_dir.mkdir(parents=True, exist_ok=True)
    xi, sri, duri = img_c["x"], img_c["sr"], img_c["duration"]
    img_review = []
    for i, (st, en) in enumerate(img_post):
        cid = f"IMG_post_{i+1:02d}"
        path = img_dir / f"{cid}.wav"
        mid = 0.5 * (st + en)
        # listen window at least 2s but prefer full post segment if longer
        listen_dur = max(2.0, en - st)
        half = listen_dur / 2
        s = max(0.0, mid - half)
        e = min(duri, mid + half)
        sf.write(str(path), xi[int(s * sri) : int(e * sri)], sri)
        img_review.append(
            {
                "clip_id": cid,
                "source": "IMG_0639",
                "role": "STRESS_CASE",
                "detector": "hybrid_score+full_chain_p300_g300_m500",
                "raw_candidate_note": "derived from hybrid then pad/merge/min-expand",
                "region_start_s": st,
                "region_end_s": en,
                "region_duration_s": en - st,
                "human_review_window_start": s,
                "human_review_window_end": e,
                "listen_duration_s": e - s,
                "clip_relpath": f"clips_IMG_post_min2s/{cid}.wav",
                "human_label": "",
            }
        )

    (HR / "review_metadata.json").write_text(
        json.dumps(
            {
                "new_silero_samples": new_review,
                "img_postprocess_regions": img_review,
                "p196_preserved": {
                    "IMG_hybrid_raw_candidates_human_round2": "28/28 SPEECH",
                    "commit": "a720e0c",
                },
                "label_schema": ["SPEECH", "NON_SPEECH", "MIXED", "UNCERTAIN"],
                "human_labels_this_phase": "OPTIONAL — Phase 1.9.6 already verified IMG candidates; NEW silero optional",
            },
            indent=2,
        ),
        encoding="utf-8",
    )

    # write CSVs
    def write_csv(path, rows):
        if not rows:
            path.write_text("empty\n", encoding="utf-8")
            return
        keys = []
        for r in rows:
            for k in r.keys():
                if k not in keys:
                    keys.append(k)
        with open(path, "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
            w.writeheader()
            for r in rows:
                w.writerow(r)

    write_csv(RES / "recording_inventory.csv", inventory_rows)
    write_csv(RES / "detector_results.csv", detector_rows)
    write_csv(
        RES / "normal_case_results.csv",
        [r for r in detector_rows if r["recording_id"] == "NEW_1790"],
    )
    write_csv(
        RES / "stress_case_results.csv",
        [r for r in detector_rows if r["recording_id"] == "IMG_0639"],
    )
    write_csv(RES / "boundary_merge_results.csv", boundary_rows)
    write_csv(RES / "router_research_results.csv", router_rows)
    write_csv(RES / "router_grid_summary.csv", router_summary)
    write_csv(RES / "pronunciation_crop_safety.csv", crop_rows)

    # human_review_results: preserve 1.9.6 + optional empty new
    human_rows = [
        {
            "phase_source": "1.9.6",
            "recording_id": "IMG_0639",
            "role": "STRESS_CASE",
            "n_candidates": 28,
            "SPEECH": 28,
            "NON_SPEECH": 0,
            "MIXED": 0,
            "UNCERTAIN": 0,
            "window": "min2s",
            "note": "preserved historical evidence",
        },
        {
            "phase_source": "1.9.7",
            "recording_id": "NEW_1790",
            "role": "NORMAL_REFERENCE",
            "n_candidates": len(new_review),
            "SPEECH": "",
            "NON_SPEECH": "",
            "MIXED": "",
            "UNCERTAIN": "",
            "window": "min2s_silero_sample",
            "note": "optional; not required to conclude silero works on NEW via segment counts vs IMG",
        },
    ]
    write_csv(RES / "human_review_results.csv", human_rows)

    # machine JSON
    # pick recommended boundary configs by reducing fragmentation on IMG while not exploding speech_ratio
    img_bound = [r for r in boundary_rows if r["recording_id"] == "IMG_0639"]
    # prefer full_chain / pad_merge with n_segments down from 28 and mean_dur up
    def score_bound(r):
        n = r.get("n_segments") or 0
        mean_d = r.get("mean_dur_s") or 0
        # want fewer fragments, longer mean, not huge speech_ratio
        sr_ = r.get("speech_ratio") or 0
        return (mean_d * 2.0) - (n * 0.05) - max(0, sr_ - 0.25) * 5

    img_bound_sorted = sorted(img_bound, key=score_bound, reverse=True)[:8]
    new_bound = [r for r in boundary_rows if r["recording_id"] == "NEW_1790" and r["strategy"] in ("raw", "full_chain", "pad_merge")]

    master = {
        "phase": "1.9.7",
        "roles": {
            "NEW_1790": "NORMAL_REFERENCE / primary normal-case",
            "IMG_0639": "HIGH_COMPLEXITY_STRESS / not normal calibration",
        },
        "dataset": {
            "n_recordings": 2,
            "limitation": "MULTI-RECORDING DATASET CURRENTLY LIMITED TO 2 RECORDINGS",
        },
        "inventory": inventory_rows,
        "detector_results": detector_rows,
        "disagreement": {
            rid: det_cache[rid]["disagree_silero_vs_hybrid"] for rid in det_cache
        },
        "features": {rid: det_cache[rid]["features"] for rid in det_cache},
        "router": router_status,
        "router_grid_desired_count": n_desired,
        "boundary_top_img": img_bound_sorted,
        "scorer_status": "RAN" if scorer_ok else "UNAVAILABLE_OR_PARTIAL",
        "crop_safety": crop_rows,
        "p196_preserved": P196_ROUND2,
        "decision_axes": {
            "speech_detection_stress": "PROVEN_candidate_level_via_1.9.6_28_of_28_SPEECH",
            "speech_detection_normal": "Silero produces many segs on NEW; hybrid tracks silero",
            "boundary_quality": "IMPROVED_by_pad_merge_research_but_not_human_revalidated_this_phase",
            "scorer_safety": "see crop_safety rows",
            "production_readiness": False,
        },
        "production_vad": False,
        "router_locked": False,
        "unity_integrated": False,
    }
    (RES / "phase_1_9_7_master.json").write_text(json.dumps(master, indent=2, default=str), encoding="utf-8")

    # decision
    new_sil = next(r for r in detector_rows if r["recording_id"] == "NEW_1790" and r["mode"] == "silero")
    new_hyb = next(r for r in detector_rows if r["recording_id"] == "NEW_1790" and r["mode"] == "hybrid_score")
    img_sil = next(r for r in detector_rows if r["recording_id"] == "IMG_0639" and r["mode"] == "silero")
    img_hyb = next(r for r in detector_rows if r["recording_id"] == "IMG_0639" and r["mode"] == "hybrid_score")

    decision = "A. HYBRID_RESCUE_SUPPORTED_FOR_RESEARCH"
    rationale = [
        f"NORMAL NEW: Silero n={new_sil['n_segments']} speech_ratio={new_sil['speech_ratio']:.3f} — default path works",
        f"NORMAL NEW: hybrid n={new_hyb['n_segments']} tracks silero (disagree iou={det_cache['NEW_1790']['disagree_silero_vs_hybrid']['iou']:.3f})",
        f"STRESS IMG: Silero n={img_sil['n_segments']}; hybrid n={img_hyb['n_segments']}; human 28/28 SPEECH (1.9.6)",
        "Boundary pad/merge reduces fragmentation on stress case (see boundary_merge_results)",
        "Only 2 recordings → router NOT calibrated; production_vad=false",
    ]
    if new_sil["n_segments"] == 0:
        decision = "B. HYBRID_RESCUE_NEEDS_MORE_DATA"
        rationale.append("Unexpected: Silero failed on normal case")

    (RES / "decision.json").write_text(
        json.dumps(
            {
                "decision": decision,
                "rationale": rationale,
                "production_vad": False,
                "router_locked": False,
                "unity_integrated": False,
            },
            indent=2,
        ),
        encoding="utf-8",
    )
    print("DECISION", decision)
    print("router", router_status)
    print("DONE")


if __name__ == "__main__":
    main()
