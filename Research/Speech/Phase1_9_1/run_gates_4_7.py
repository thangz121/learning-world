"""Phase 1.9.1 gates 4-7: robustness, router sweep, padding, downstream safety."""
from __future__ import annotations
import json, sys, time
from pathlib import Path
import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_9.HybridVAD.hybrid_vad import HybridVAD, segment_iou, load_mono16
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter

OUT = REPO / "Research" / "Speech" / "Phase1_9_1" / "Results"
IMG = REPO / "Research" / "Speech" / "Phase1_8" / "AudioDerived" / "IMG_0639_16k_mono.wav"
NEW = REPO / "Research" / "Speech" / "Phase1_8" / "AudioDerived" / "NEW_16k_mono.wav"
LWE = REPO / "Research" / "Speech" / "Phase1_1" / "audio"
OUT.mkdir(parents=True, exist_ok=True)


def profile_features(path):
    x, sr = load_mono16(str(path))
    win = sr
    e = [float(np.sqrt(np.mean(x[i:i + win] ** 2))) for i in range(0, max(1, len(x) - win), win)]
    e = np.array(e) if e else np.array([0.0])
    cont = float(np.mean(e > 0.03))
    contrast = float(np.std(e) / (np.mean(e) + 1e-12))
    return {"continuous_energy_ratio": cont, "energy_contrast": contrast}


def router(cont, contrast, cont_thr, contrast_thr):
    if cont > cont_thr and contrast < contrast_thr:
        return "hybrid_score"
    return "silero"


def main():
    hv = HybridVAD()
    # --- robustness noise on NEW, WEAK_REFERENCE_FROM_CLEAN_BASELINE = silero clean segs ---
    ref = hv.run(str(NEW), mode="silero", silero_thr=0.5)
    ref_segs = ref["segments"]
    dur = ref["duration_s"]
    x, sr = load_mono16(str(NEW))
    rng = np.random.default_rng(1)
    sig_rms = float(np.sqrt(np.mean(x ** 2)) + 1e-12)
    rob = []
    tmp = OUT / "_tmp_noise.wav"
    for snr in [None, 20, 10, 5, 0, -5]:
        if snr is None:
            y = x
            cond = "clean"
        else:
            nrms = sig_rms / (10 ** (snr / 20))
            y = np.clip(x + rng.normal(0, nrms, size=len(x)).astype(np.float32), -1, 1)
            cond = f"white_snr_{snr}"
        sf.write(str(tmp), y, sr)
        for mode in ["silero", "hybrid_score", "energy", "spectral", "adaptive_then_silero"]:
            r = hv.run(str(tmp), mode=mode, silero_thr=0.5)
            m = segment_iou(ref_segs, r["segments"], dur)
            rob.append({
                "condition": cond, "snr_db": snr, "mode": mode,
                "n_segments": r["n_segments"], "speech_ratio": r["speech_ratio"],
                "precision": m["precision"], "recall": m["recall"], "f1": m["f1"],
                "fpr": m["fpr"], "fnr": m["fnr"], "dur_error": m["dur_error"],
                "runtime_s": r["processing_s"],
                "reference_type": "WEAK_REFERENCE_FROM_CLEAN_BASELINE_SILERO",
            })
            print(cond, mode, "F1", round(m["f1"], 3), "n", r["n_segments"], flush=True)
    # IMG extreme
    for mode in ["silero", "hybrid_score", "energy", "adaptive_then_silero"]:
        r = hv.run(str(IMG), mode=mode, silero_thr=0.5)
        rob.append({
            "condition": "IMG_0639_as_is", "snr_db": None, "mode": mode,
            "n_segments": r["n_segments"], "speech_ratio": r["speech_ratio"],
            "reference_type": "NO_INDEPENDENT_GT",
            "runtime_s": r["processing_s"],
        })
    (OUT / "robustness_results.json").write_text(json.dumps(rob, indent=2), encoding="utf-8")

    # --- router sweep ---
    files = {"IMG_0639": IMG, "NEW": NEW}
    feats = {k: profile_features(p) for k, p in files.items()}
    cont_thrs = [0.7, 0.75, 0.8, 0.85, 0.9]
    cont_asts = [0.3, 0.35, 0.4, 0.45, 0.5]
    router_rows = []
    for cthr in cont_thrs:
        for xthr in cont_asts:
            for name, path in files.items():
                f = feats[name]
                choice = router(f["continuous_energy_ratio"], f["energy_contrast"], cthr, xthr)
                r = hv.run(str(path), mode=choice, silero_thr=0.5)
                router_rows.append({
                    "file": name, "cont_thr": cthr, "contrast_thr": xthr,
                    "features": f, "chosen_mode": choice,
                    "n_segments": r["n_segments"], "speech_ratio": r["speech_ratio"],
                })
    # stability: for each file, fraction of thr pairs choosing hybrid
    stab = {}
    for name in files:
        rows = [r for r in router_rows if r["file"] == name]
        hyb = sum(1 for r in rows if r["chosen_mode"] == "hybrid_score")
        stab[name] = {
            "n_grid": len(rows),
            "n_choose_hybrid": hyb,
            "frac_hybrid": hyb / len(rows),
            "features": feats[name],
        }
    (OUT / "router_results.json").write_text(json.dumps({
        "hypothesis": "if cont>C and contrast<X then hybrid else silero",
        "grid": router_rows,
        "stability": stab,
        "note": "Only 2 real files — risk of overfit-to-file; not production constants",
    }, indent=2), encoding="utf-8")
    print("ROUTER_STAB", stab, flush=True)

    # --- padding / segment quality on NEW silero segs ---
    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()
    pad_rows = []
    # take first 3 NEW silero segments, export with pads, run soft on a known short word file instead
    # Downstream safety on LWE words
    words = [
        ("sapi_red.wav", "red"),
        ("pregen_apple_normal.wav", "apple"),
        ("sapi_cat.wav", "cat"),
        ("sapi_blue.wav", "blue"),
        ("sapi_big.wav", "big"),
        ("sapi_book.wav", "book"),
        ("sapi_dog.wav", "dog"),
        ("sapi_red_apple.wav", "red apple"),
        ("stress_silence.wav", "red"),
    ]
    down = []
    for fn, w in words:
        path = LWE / fn
        st = tgt.build(w)
        soft_full = pev.soft_match(str(path), st.arpabet)
        for mode, pad in [("full", None), ("silero", 0.0), ("silero", 0.1), ("silero", 0.25), ("silero", 0.5),
                          ("hybrid_score", 0.0), ("hybrid_score", 0.25), ("adaptive_then_silero", 0.25)]:
            if mode == "full":
                sc = soft_full.soft_score_0_100
                conf = soft_full.confidence_0_1
                nseg = None
            else:
                r = hv.run(str(path), mode=mode if mode != "silero" else "silero", silero_thr=0.5)
                nseg = r["n_segments"]
                if not r["segments"]:
                    sc, conf = 0.0, 0.0
                else:
                    x, sr = load_mono16(str(path))
                    s0 = max(0.0, r["segments"][0]["start"] - pad)
                    s1 = min(len(x) / sr, r["segments"][-1]["end"] + pad)
                    i0, i1 = int(s0 * sr), int(s1 * sr)
                    tmp = OUT / f"_ds_{fn}_{mode}_{pad}.wav"
                    sf.write(str(tmp), x[i0:i1], sr)
                    soft = pev.soft_match(str(tmp), st.arpabet)
                    sc, conf = soft.soft_score_0_100, soft.confidence_0_1
                    try:
                        tmp.unlink()
                    except Exception:
                        pass
            down.append({
                "word": w, "file": fn, "mode": mode, "pad_s": pad, "n_segments": nseg,
                "soft_score": sc, "soft_conf": conf,
                "delta_vs_full": round(sc - soft_full.soft_score_0_100, 2),
            })
        print("DOWN", w, "full", soft_full.soft_score_0_100, flush=True)
    (OUT / "downstream_results.json").write_text(json.dumps(down, indent=2), encoding="utf-8")

    # fragmentation stats for IMG hybrid
    hyb = HybridVAD().run(str(IMG), mode="hybrid_score", silero_thr=0.5)
    durs = [s["end"] - s["start"] for s in hyb["segments"]]
    seg_qual = {
        "IMG_hybrid_score": {
            "n": len(durs),
            "min_dur": min(durs) if durs else None,
            "max_dur": max(durs) if durs else None,
            "mean_dur": float(np.mean(durs)) if durs else None,
            "n_lt_0.2s": sum(1 for d in durs if d < 0.2),
            "n_lt_0.5s": sum(1 for d in durs if d < 0.5),
            "speech_ratio": hyb["speech_ratio"],
        }
    }
    (OUT / "segment_quality.json").write_text(json.dumps(seg_qual, indent=2), encoding="utf-8")
    print("SEGQUAL", seg_qual, flush=True)
    print("DONE", flush=True)


if __name__ == "__main__":
    main()
