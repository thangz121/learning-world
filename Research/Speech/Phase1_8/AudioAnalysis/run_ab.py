"""Phase 1.8 A/B: identical measurements on IMG_0639 vs NEW real audio."""
from __future__ import annotations
import json, math, time
from pathlib import Path
import numpy as np
import soundfile as sf
import torch

REPO = Path(__file__).resolve().parents[4]
import sys
sys.path.insert(0, str(REPO))
from silero_vad import load_silero_vad, get_speech_timestamps
from Research.Speech.Phase1_2.Adapters.asr_moonshine import MoonshineAsrAdapter
from Research.Speech.Phase1_2.Adapters.acoustic_parselmouth import ParselmouthAcousticAdapter

OUT = REPO / "Research" / "Speech" / "Phase1_8" / "Results"
DER = REPO / "Research" / "Speech" / "Phase1_8" / "AudioDerived"
OUT.mkdir(parents=True, exist_ok=True)

FILES = {
    "IMG_0639": {
        "orig": REPO / "IMG_0639.mp3",
        "sha256": "E4CDF24D2B5140B91EF637676CD5FA1B9A7F6B6A629F62AE9AC2657CB171A6F7",
        "derived": DER / "IMG_0639_16k_mono.wav",
    },
    "NEW": {
        "orig": REPO / "1790932799243_8856108714107255767_8856108714107255767.mp3",
        "sha256": "17E3DA86267D1B7B864EA862A6C5C3B12AF4E49FD649F2FE140652A53DDF33FF",
        "derived": DER / "NEW_16k_mono.wav",
    },
}


def quality(path: Path):
    x, sr = sf.read(str(path))
    if x.ndim > 1:
        x = x.mean(axis=1)
    x = x.astype(np.float64)
    n = len(x)
    dur = n / sr
    rms = float(np.sqrt(np.mean(x ** 2) + 1e-20))
    peak = float(np.max(np.abs(x)))
    crest = float(peak / (rms + 1e-12))
    clip = float(np.mean(np.abs(x) >= 0.99))
    # noise floor proxy: 10th percentile of 50ms frame RMS
    win = max(1, int(0.05 * sr))
    frames = [np.sqrt(np.mean(x[i:i + win] ** 2)) for i in range(0, n - win, win)]
    frames = np.array(frames)
    noise_floor = float(np.percentile(frames, 10))
    speech_like = float(np.percentile(frames, 90))
    snr = 20 * math.log10((speech_like + 1e-12) / (noise_floor + 1e-12))
    sil = float(np.mean(np.abs(x) < 0.01))
    # continuous energy: fraction of 1s windows with rms > 0.03
    win1 = sr
    e1 = [float(np.sqrt(np.mean(x[i:i + win1] ** 2))) for i in range(0, max(1, n - win1), win1)]
    cont = float(np.mean([ee > 0.03 for ee in e1])) if e1 else 0.0
    # spectral
    chunk = x[: min(n, sr * 5)]
    spec = np.abs(np.fft.rfft(chunk)) + 1e-12
    freqs = np.fft.rfftfreq(len(chunk), 1 / sr)
    centroid = float(np.sum(freqs * spec) / np.sum(spec))
    # spectral flatness
    log_mean = float(np.mean(np.log(spec)))
    geom = math.exp(log_mean)
    arith = float(np.mean(spec))
    flatness = float(geom / (arith + 1e-12))
    low = float(np.sum(spec[freqs < 300]) / np.sum(spec))
    high = float(np.sum(spec[freqs > 3000]) / np.sum(spec))
    # speech/silence contrast: std of 1s energy / mean
    e1a = np.array(e1) if e1 else np.array([0.0])
    contrast = float(np.std(e1a) / (np.mean(e1a) + 1e-12))
    return {
        "duration_s": dur, "sr": sr, "rms": rms, "peak": peak, "crest_factor": crest,
        "clip_frac": clip, "noise_floor_p10_rms": noise_floor, "speech_like_p90_rms": speech_like,
        "snr_p90_over_p10_db": snr, "silence_ratio_abs_lt_0.01": sil,
        "continuous_energy_ratio_1s_gt_0.03": cont,
        "energy_1s_min": float(np.min(e1a)), "energy_1s_med": float(np.median(e1a)),
        "energy_1s_max": float(np.max(e1a)), "energy_1s_std": float(np.std(e1a)),
        "energy_contrast_std_over_mean": contrast,
        "spectral_centroid_hz": centroid, "spectral_flatness": flatness,
        "low_freq_energy_frac_lt300": low, "high_freq_energy_frac_gt3000": high,
        "energy_1s": e1,
    }


def vad_run(path: Path, model, threshold=0.5):
    x, sr = sf.read(str(path))
    assert sr == 16000
    audio = torch.tensor(x.astype(np.float32))
    t0 = time.perf_counter()
    ts = get_speech_timestamps(audio, model, return_seconds=True, threshold=threshold)
    dt = time.perf_counter() - t0
    segs = [{"start": float(t["start"]), "end": float(t["end"])} for t in ts]
    speech_dur = sum(s["end"] - s["start"] for s in segs)
    dur = len(x) / sr
    return {
        "threshold": threshold, "n_segments": len(segs), "segments": segs,
        "speech_duration_s": speech_dur, "speech_ratio": speech_dur / max(1e-9, dur),
        "first_onset": segs[0]["start"] if segs else None,
        "last_end": segs[-1]["end"] if segs else None,
        "avg_seg_dur": (speech_dur / len(segs)) if segs else 0.0,
        "min_seg_dur": min((s["end"] - s["start"]) for s in segs) if segs else None,
        "max_seg_dur": max((s["end"] - s["start"]) for s in segs) if segs else None,
        "processing_s": dt,
    }


def fixed_window_asr(path: Path, asr, win_s=8.0):
    x, sr = sf.read(str(path))
    rows = []
    for i, start in enumerate(range(0, len(x), int(sr * win_s))):
        end = min(len(x), start + int(sr * win_s))
        if end - start < sr * 1:
            break
        sl = x[start:end]
        tmp = OUT / f"_tmp_{path.stem}_{i}.wav"
        sf.write(str(tmp), sl, sr)
        r = asr.run(str(tmp))
        try:
            tmp.unlink()
        except Exception:
            pass
        txt = (r.text or "").strip()
        rows.append({"i": i, "t0": start / sr, "t1": end / sr,
                     "rms": float(np.sqrt(np.mean(sl ** 2))), "asr": txt})
    return rows


def analyze_one(key: str, meta: dict, model, asr, ac):
    t_all = time.perf_counter()
    q = quality(meta["derived"])
    # baseline VAD threshold 0.5 identical to default silero usage
    vad0 = vad_run(meta["derived"], model, threshold=0.5)
    # also measure 0.3/0.1 for sensitivity curve AFTER baseline recorded
    vad_curve = {str(th): vad_run(meta["derived"], model, threshold=th)
                 for th in [0.5, 0.3, 0.2, 0.1, 0.05]}
    t0 = time.perf_counter()
    asr_rows = fixed_window_asr(meta["derived"], asr, win_s=8.0)
    asr_t = time.perf_counter() - t0
    t1 = time.perf_counter()
    whole_ac = ac.run(str(meta["derived"]))
    ac_t = time.perf_counter() - t1

    nonempty = [r for r in asr_rows if r["asr"]]
    return {
        "id": key,
        "source_file": str(meta["orig"].name),
        "sha256": meta["sha256"],
        "original_untouched": True,
        "privacy_sensitive": True,
        "consent_status": "unknown",
        "speaker_age_status": "UNKNOWN",
        "quality": {k: v for k, v in q.items() if k != "energy_1s"},
        "energy_1s": q["energy_1s"],
        "vad_baseline_thr0.5": vad0,
        "vad_threshold_curve": {k: {kk: vv for kk, vv in v.items() if kk != "segments"}
                               for k, v in vad_curve.items()},
        "vad_segments_thr0.5": vad0["segments"],
        "asr_fixed_8s": asr_rows,
        "asr_nonempty_count": len(nonempty),
        "asr_processing_s": asr_t,
        "whole_acoustic": {
            "f0_mean": whole_ac.f0_mean, "f1_mean": whole_ac.f1_mean, "f2_mean": whole_ac.f2_mean,
            "intensity": whole_ac.intensity_mean, "voiced_frac": whole_ac.f0_voiced_frac,
            "processing_s": ac_t, "warnings": whole_ac.warnings,
        },
        "language_global": "UNKNOWN",
        "language_note": "EN-only ASR probes; no definitive LID claim",
        "target_known": False,
        "pronunciation_scored": False,
        "pronunciation_reason": "no_authorized_english_target",
        "total_s": time.perf_counter() - t_all,
    }


def main():
    print("Loading Silero...", flush=True)
    model = load_silero_vad()
    asr = MoonshineAsrAdapter()
    ac = ParselmouthAcousticAdapter()

    results = {}
    for key, meta in FILES.items():
        print("ANALYZING", key, flush=True)
        results[key] = analyze_one(key, meta, model, asr, ac)
        (OUT / f"{key}_baseline.json").write_text(
            json.dumps(results[key], ensure_ascii=False, indent=2), encoding="utf-8")
        print(key, "vad_segs", results[key]["vad_baseline_thr0.5"]["n_segments"],
              "speech_ratio", results[key]["vad_baseline_thr0.5"]["speech_ratio"],
              "rms", results[key]["quality"]["rms"],
              "cont", results[key]["quality"]["continuous_energy_ratio_1s_gt_0.03"],
              "contrast", results[key]["quality"]["energy_contrast_std_over_mean"], flush=True)

    a, b = results["IMG_0639"], results["NEW"]
    qa, qb = a["quality"], b["quality"]
    va, vb = a["vad_baseline_thr0.5"], b["vad_baseline_thr0.5"]

    def dlt(x, y):
        if x is None or y is None:
            return None
        return y - x

    rows = []
    metrics = [
        ("duration_s", qa["duration_s"], qb["duration_s"]),
        ("rms", qa["rms"], qb["rms"]),
        ("peak", qa["peak"], qb["peak"]),
        ("noise_floor_p10_rms", qa["noise_floor_p10_rms"], qb["noise_floor_p10_rms"]),
        ("snr_p90_over_p10_db", qa["snr_p90_over_p10_db"], qb["snr_p90_over_p10_db"]),
        ("silence_ratio_abs_lt_0.01", qa["silence_ratio_abs_lt_0.01"], qb["silence_ratio_abs_lt_0.01"]),
        ("continuous_energy_ratio_1s_gt_0.03", qa["continuous_energy_ratio_1s_gt_0.03"], qb["continuous_energy_ratio_1s_gt_0.03"]),
        ("energy_contrast_std_over_mean", qa["energy_contrast_std_over_mean"], qb["energy_contrast_std_over_mean"]),
        ("spectral_centroid_hz", qa["spectral_centroid_hz"], qb["spectral_centroid_hz"]),
        ("spectral_flatness", qa["spectral_flatness"], qb["spectral_flatness"]),
        ("vad_n_segments_thr0.5", va["n_segments"], vb["n_segments"]),
        ("vad_speech_ratio_thr0.5", va["speech_ratio"], vb["speech_ratio"]),
        ("vad_speech_duration_s", va["speech_duration_s"], vb["speech_duration_s"]),
        ("asr_nonempty_8s_windows", a["asr_nonempty_count"], b["asr_nonempty_count"]),
        ("total_processing_s", a["total_s"], b["total_s"]),
    ]
    for name, xa, xb in metrics:
        rows.append({
            "metric": name, "IMG_0639": xa, "NEW": xb, "delta_NEW_minus_IMG": dlt(xa, xb),
        })

    # interpretation of VAD outcome
    if va["n_segments"] == 0 and vb["n_segments"] > 0:
        outcome = "A_IMG_FAILS_NEW_WORKS"
        interp = "Material difference: same Silero thr=0.5 yields 0 segs on IMG_0639 but >0 on NEW"
    elif va["n_segments"] == 0 and vb["n_segments"] == 0:
        outcome = "B_BOTH_FAIL"
        interp = "Same Silero thr=0.5 fails both — bottleneck may be VAD config/robustness not one-file anomaly"
    elif va["n_segments"] > 0 and vb["n_segments"] > 0:
        outcome = "D_BOTH_WORK"
        interp = "Both produce segments under thr=0.5 — prior IMG failure needs recheck"
    else:
        outcome = "C_PARTIAL"
        interp = "Asymmetric/partial VAD behavior"

    ab = {
        "pipeline_version": "Phase1.7/1.6 components identical A/B",
        "silero_threshold_baseline": 0.5,
        "outcome_code": outcome,
        "outcome_interpretation": interp,
        "table": rows,
        "files": {
            "IMG_0639": {"sha256": a["sha256"], "source": a["source_file"]},
            "NEW": {"sha256": b["sha256"], "source": b["source_file"]},
        },
        "key_deltas": {
            "continuous_energy": dlt(qa["continuous_energy_ratio_1s_gt_0.03"], qb["continuous_energy_ratio_1s_gt_0.03"]),
            "energy_contrast": dlt(qa["energy_contrast_std_over_mean"], qb["energy_contrast_std_over_mean"]),
            "snr_db": dlt(qa["snr_p90_over_p10_db"], qb["snr_p90_over_p10_db"]),
            "vad_segments": dlt(va["n_segments"], vb["n_segments"]),
            "vad_speech_ratio": dlt(va["speech_ratio"], vb["speech_ratio"]),
        },
        "research_roles": {
            "IMG_0639": a.get("research_roles") if "research_roles" in a else [
                "REAL_SPEAKER_UNKNOWN_AGE", "VAD_STRESS_CASE_CONTINUOUS_ENERGY", "NOT_CANONICAL"
            ],
            "NEW": [
                "REAL_SPEAKER_UNKNOWN_AGE",
                "REAL_MIC_OR_DEVICE_SAMPLE",
                "NOT_CANONICAL",
            ],
        },
        "pronunciation": "not_scored_no_authorized_target_either_file",
        "age": "UNKNOWN_both",
        "language": "UNKNOWN_both_en_only_probes",
    }
    # refine NEW role based on VAD
    if vb["n_segments"] > 0:
        ab["research_roles"]["NEW"].append("REAL_SPEECH_CANDIDATE_VAD_POSITIVE")
        ab["research_roles"]["NEW"].append("VAD_CONTRAST_CASE_VS_IMG_0639")
    else:
        ab["research_roles"]["NEW"].append("VAD_STRESS_CASE")

    (OUT / "real_audio_ab.json").write_text(json.dumps(ab, ensure_ascii=False, indent=2), encoding="utf-8")
    # also save under requested names
    (OUT / "IMG_0639_baseline.json").write_text(json.dumps(results["IMG_0639"], ensure_ascii=False, indent=2), encoding="utf-8")
    (OUT / "new_audio_baseline.json").write_text(json.dumps(results["NEW"], ensure_ascii=False, indent=2), encoding="utf-8")
    print("OUTCOME", outcome, flush=True)
    print("TABLE", json.dumps(rows, indent=2), flush=True)


if __name__ == "__main__":
    main()
