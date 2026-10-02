"""Phase 1.9 VAD evaluation: baseline freeze, hybrids, noise curve, phoneme preservation."""
from __future__ import annotations
import json, sys, time
from pathlib import Path
import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_9.HybridVAD.hybrid_vad import HybridVAD, segment_iou, load_mono16
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

OUT = REPO / "Research" / "Speech" / "Phase1_9" / "Results"
OUT.mkdir(parents=True, exist_ok=True)
IMG = REPO / "Research" / "Speech" / "Phase1_8" / "AudioDerived" / "IMG_0639_16k_mono.wav"
NEW = REPO / "Research" / "Speech" / "Phase1_8" / "AudioDerived" / "NEW_16k_mono.wav"
LWE = REPO / "Research" / "Speech" / "Phase1_1" / "audio"

MODES = [
    "silero",
    "energy",
    "spectral",
    "energy_and_silero",
    "energy_or_silero",
    "adaptive_then_silero",
    "spectral_and_energy_then_silero",
    "hybrid_score",
]


def main():
    hv = HybridVAD()
    # --- baseline freeze thr=0.5 ---
    base = {}
    for name, path in [("IMG_0639", IMG), ("NEW", NEW)]:
        r = hv.run(str(path), mode="silero", silero_thr=0.5)
        base[name] = {
            "n_segments": r["n_segments"],
            "speech_ratio": r["speech_ratio"],
            "speech_duration_s": r["speech_duration_s"],
            "processing_s": r["processing_s"],
        }
        print("BASE", name, base[name], flush=True)
    (OUT / "vad_baseline_v19.json").write_text(json.dumps({
        "silero_thr": 0.5,
        "files": base,
        "note": "frozen Phase1.8 reproduction",
    }, indent=2), encoding="utf-8")

    # NEW silero as reference for hybrid fidelity on clean-ish real speech
    new_ref = hv.run(str(NEW), mode="silero", silero_thr=0.5)
    new_ref_segs = new_ref["segments"]
    new_dur = new_ref["duration_s"]

    # IMG weak reference: energy gate with high margin as "speech candidate" pseudo-label
    # Also use hybrid_score for comparison not as GT
    img_energy = hv.run(str(IMG), mode="energy", energy_margin_db=8.0)
    # human-audible weak labels unavailable; use energy@8dB as WEAK_PSEUDO_REF labeled
    img_pseudo = img_energy["segments"]
    img_dur = img_energy["duration_s"]

    hybrid_rows = []
    for name, path, ref_segs, dur, ref_label in [
        ("NEW", NEW, new_ref_segs, new_dur, "silero_thr0.5_as_ref"),
        ("IMG_0639", IMG, img_pseudo, img_dur, "energy_margin8dB_WEAK_PSEUDO_REF"),
    ]:
        for mode in MODES:
            for thr in ([0.5] if mode != "silero" else [0.3, 0.4, 0.5, 0.6, 0.7]):
                if mode != "silero" and thr != 0.5:
                    continue
                r = hv.run(str(path), mode=mode, silero_thr=thr if mode == "silero" or "silero" in mode else 0.5,
                           energy_margin_db=6.0)
                # for silero-containing modes use thr param
                if "silero" in mode or mode == "silero":
                    r = hv.run(str(path), mode=mode, silero_thr=thr, energy_margin_db=6.0)
                m = segment_iou(ref_segs, r["segments"], dur)
                row = {
                    "file": name, "mode": mode, "silero_thr": thr,
                    "ref_label": ref_label,
                    "n_segments": r["n_segments"],
                    "speech_ratio": r["speech_ratio"],
                    "speech_duration_s": r["speech_duration_s"],
                    "runtime_s": r["processing_s"],
                    "profile": r["profile"],
                    **m,
                }
                hybrid_rows.append(row)
                print(f"{name:10s} {mode:32s} thr={thr} n={r['n_segments']:3d} "
                      f"F1={m['f1']:.3f} P={m['precision']:.3f} R={m['recall']:.3f} "
                      f"ratio={r['speech_ratio']:.3f} t={r['processing_s']:.2f}", flush=True)

    # energy margin sweep on IMG with pseudo ref
    for margin in [3, 6, 9, 12]:
        r = hv.run(str(IMG), mode="energy", energy_margin_db=margin)
        m = segment_iou(img_pseudo, r["segments"], img_dur)
        hybrid_rows.append({
            "file": "IMG_0639", "mode": f"energy_margin_{margin}dB", "silero_thr": None,
            "ref_label": "energy_margin8dB_WEAK_PSEUDO_REF",
            "n_segments": r["n_segments"], "speech_ratio": r["speech_ratio"],
            "runtime_s": r["processing_s"], **m,
        })

    (OUT / "vad_hybrid_results.json").write_text(json.dumps(hybrid_rows, indent=2), encoding="utf-8")

    # Controlled noise on NEW: silero vs adaptive_then_silero vs hybrid_score
    x, sr = load_mono16(str(NEW))
    rng = np.random.default_rng(0)
    sig_rms = float(np.sqrt(np.mean(x ** 2)) + 1e-12)
    noise_rows = []
    tmp = OUT / "_noise_tmp.wav"
    for snr in [20, 15, 10, 5, 0, -5]:
        noise_rms = sig_rms / (10 ** (snr / 20))
        y = np.clip(x + rng.normal(0, noise_rms, size=len(x)).astype(np.float32), -1, 1)
        sf.write(str(tmp), y, sr)
        for mode in ["silero", "adaptive_then_silero", "hybrid_score", "energy_and_silero"]:
            r = hv.run(str(tmp), mode=mode, silero_thr=0.5)
            m = segment_iou(new_ref_segs, r["segments"], new_dur)
            noise_rows.append({
                "snr_db": snr, "mode": mode, "n_segments": r["n_segments"],
                "speech_ratio": r["speech_ratio"], **{k: m[k] for k in
                    ["precision", "recall", "f1", "fpr", "fnr", "dur_error"]},
                "runtime_s": r["processing_s"],
            })
            print(f"NOISE snr={snr:3d} {mode:24s} F1={m['f1']:.3f} R={m['recall']:.3f} n={r['n_segments']}", flush=True)
    # IMG as extreme condition
    r = hv.run(str(IMG), mode="silero", silero_thr=0.5)
    noise_rows.append({"snr_db": None, "mode": "silero_on_IMG", "n_segments": r["n_segments"],
                       "speech_ratio": r["speech_ratio"], "note": "not_white_noise"})
    r = hv.run(str(IMG), mode="adaptive_then_silero", silero_thr=0.5)
    noise_rows.append({"snr_db": None, "mode": "adaptive_then_silero_on_IMG",
                       "n_segments": r["n_segments"], "speech_ratio": r["speech_ratio"]})
    r = hv.run(str(IMG), mode="hybrid_score", silero_thr=0.5)
    noise_rows.append({"snr_db": None, "mode": "hybrid_score_on_IMG",
                       "n_segments": r["n_segments"], "speech_ratio": r["speech_ratio"]})
    r = hv.run(str(IMG), mode="energy", energy_margin_db=6.0)
    noise_rows.append({"snr_db": None, "mode": "energy_on_IMG",
                       "n_segments": r["n_segments"], "speech_ratio": r["speech_ratio"]})
    (OUT / "vad_noise_curve.json").write_text(json.dumps(noise_rows, indent=2), encoding="utf-8")

    # Phoneme preservation: clean LWE words — segment-only should not change soft score if full file used
    # Compare soft score on full file vs on silero-cropped vs hybrid-cropped
    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()
    words = [
        ("sapi_red.wav", "red"),
        ("sapi_cat.wav", "cat"),
        ("pregen_apple_normal.wav", "apple"),
        ("sapi_blue.wav", "blue"),
        ("sapi_big.wav", "big"),
    ]
    ph_rows = []
    for fn, w in words:
        path = LWE / fn
        st = tgt.build(w)
        soft_full = pev.soft_match(str(path), st.arpabet)
        # crop with silero
        r_sil = hv.run(str(path), mode="silero", silero_thr=0.5)
        r_hyb = hv.run(str(path), mode="adaptive_then_silero", silero_thr=0.5)
        def crop_score(segs):
            if not segs:
                return 0.0, "no_seg"
            x, sr = load_mono16(str(path))
            # take union of segs with pad
            s0 = max(0.0, segs[0]["start"] - 0.05)
            s1 = min(len(x) / sr, segs[-1]["end"] + 0.05)
            i0, i1 = int(s0 * sr), int(s1 * sr)
            tmp = OUT / f"_ph_{fn}"
            sf.write(str(tmp), x[i0:i1], sr)
            sc = pev.soft_match(str(tmp), st.arpabet)
            try:
                tmp.unlink()
            except Exception:
                pass
            return sc.soft_score_0_100, sc.confidence_0_1
        sc_sil, cf_sil = crop_score(r_sil["segments"])
        sc_hyb, cf_hyb = crop_score(r_hyb["segments"])
        ph_rows.append({
            "word": w, "file": fn,
            "soft_full": soft_full.soft_score_0_100,
            "soft_silero_crop": sc_sil,
            "soft_hybrid_crop": sc_hyb,
            "delta_silero": round(sc_sil - soft_full.soft_score_0_100, 2),
            "delta_hybrid": round(sc_hyb - soft_full.soft_score_0_100, 2),
            "silero_nseg": r_sil["n_segments"], "hybrid_nseg": r_hyb["n_segments"],
        })
        print("PHONEME", w, "full", soft_full.soft_score_0_100, "sil", sc_sil, "hyb", sc_hyb, flush=True)
    (OUT / "phoneme_preservation.json").write_text(json.dumps(ph_rows, indent=2), encoding="utf-8")

    # Decision summary
    # On NEW: best hybrid should keep F1 high vs silero ref
    new_sil = [r for r in hybrid_rows if r["file"] == "NEW" and r["mode"] == "silero" and r["silero_thr"] == 0.5][0]
    candidates = [r for r in hybrid_rows if r["file"] == "NEW" and r["mode"] != "silero"]
    best_new = max(candidates, key=lambda r: r.get("f1", 0))
    img_sil = [r for r in hybrid_rows if r["file"] == "IMG_0639" and r["mode"] == "silero" and r["silero_thr"] == 0.5][0]
    img_hyb = [r for r in hybrid_rows if r["file"] == "IMG_0639" and r["mode"] == "adaptive_then_silero"]
    img_energy = [r for r in hybrid_rows if r["file"] == "IMG_0639" and r["mode"] == "energy"]
    img_score = [r for r in hybrid_rows if r["file"] == "IMG_0639" and r["mode"] == "hybrid_score"]

    decision = {
        "classification": None,
        "rationale": [],
        "recommended_default": "silero_thr_0.5",
        "recommended_difficult_real_audio": None,
    }
    # If adaptive recovers IMG segs while NEW F1 stays high
    img_adapt_n = img_hyb[0]["n_segments"] if img_hyb else 0
    img_energy_n = img_energy[0]["n_segments"] if img_energy else 0
    img_score_n = img_score[0]["n_segments"] if img_score else 0
    new_adapt = [r for r in hybrid_rows if r["file"] == "NEW" and r["mode"] == "adaptive_then_silero"]
    new_adapt_f1 = new_adapt[0]["f1"] if new_adapt else 0

    if img_sil["n_segments"] == 0 and (img_energy_n > 0 or img_score_n > 0) and new_adapt_f1 >= 0.85:
        decision["classification"] = "SILERO_PLUS_ADAPTIVE_GATE"
        decision["recommended_difficult_real_audio"] = "adaptive_then_silero OR energy_pregate_then_silero"
        decision["rationale"].append("IMG silero=0 but energy/hybrid recover candidates; NEW hybrid keeps high F1 vs silero ref")
    elif img_sil["n_segments"] == 0 and img_energy_n > 5 and new_sil["f1"] >= 0.99:
        decision["classification"] = "SILERO_PLUS_HYBRID_FRONT_END"
        decision["recommended_difficult_real_audio"] = "hybrid_score or energy gate for continuous-energy files"
        decision["rationale"].append("Need front-end beyond thr sweep; energy detects structure Silero misses on IMG")
    else:
        decision["classification"] = "SILERO_PLUS_ADAPTIVE_GATE"
        decision["rationale"].append("Default silero remains for NEW-like; special path for continuous-energy")

    # threshold sweep alone cannot fix IMG
    img_thr = [r for r in hybrid_rows if r["file"] == "IMG_0639" and r["mode"] == "silero"]
    thr_any = any(r["n_segments"] > 2 for r in img_thr)
    decision["threshold_sweep_fixes_IMG"] = thr_any
    decision["img_threshold_sweep"] = [{k: r[k] for k in ("silero_thr", "n_segments", "speech_ratio")} for r in img_thr]
    decision["new_best_hybrid"] = {k: best_new[k] for k in ("mode", "f1", "precision", "recall", "n_segments")}
    decision["phoneme_preservation_max_abs_delta"] = max(
        abs(p["delta_silero"]) for p in ph_rows) if ph_rows else None

    (OUT / "vad_decision.json").write_text(json.dumps(decision, indent=2), encoding="utf-8")
    print("DECISION", decision["classification"], decision, flush=True)


if __name__ == "__main__":
    main()
