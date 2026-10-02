"""Phase 1.7 real-audio analysis for IMG_0639 — no invented targets/age."""
from __future__ import annotations
import json, math, time, wave, struct
from pathlib import Path
import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
import sys
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_2.Adapters.vad_silero import SileroVadAdapter
from Research.Speech.Phase1_2.Adapters.asr_moonshine import MoonshineAsrAdapter
from Research.Speech.Phase1_2.Adapters.acoustic_parselmouth import ParselmouthAcousticAdapter
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter

DERIVED = REPO / "Research" / "Speech" / "Phase1_7" / "AudioDerived" / "IMG_0639_16k_mono.wav"
OUT = REPO / "Research" / "Speech" / "Phase1_7" / "Results"
OUT.mkdir(parents=True, exist_ok=True)
SYN = REPO / "Research" / "Speech" / "Phase1_1" / "audio" / "sapi_red.wav"


def audio_quality(path: Path):
    x, sr = sf.read(str(path))
    if x.ndim > 1:
        x = x.mean(axis=1)
    x = x.astype(np.float64)
    n = len(x)
    dur = n / sr
    rms = float(np.sqrt(np.mean(x ** 2)))
    peak = float(np.max(np.abs(x)))
    clip_frac = float(np.mean(np.abs(x) >= 0.99))
    # silence: |x| < 0.01
    sil = float(np.mean(np.abs(x) < 0.01))
    # simple SNR proxy: speech frames vs silence frames energy
    thr = 0.02
    speech = x[np.abs(x) >= thr]
    noise = x[np.abs(x) < thr]
    if len(speech) and len(noise) and np.mean(noise ** 2) > 0:
        snr = 10 * math.log10(np.mean(speech ** 2) / np.mean(noise ** 2))
    else:
        snr = None
    # spectral centroid rough
    if n > 1024:
        spec = np.abs(np.fft.rfft(x[: min(n, sr * 5)]))
        freqs = np.fft.rfftfreq(min(n, sr * 5), 1 / sr)
        c = float(np.sum(freqs * spec) / (np.sum(spec) + 1e-12))
    else:
        c = None
    return {
        "duration_s": dur, "sample_rate": sr, "n_samples": n,
        "rms": rms, "peak": peak, "clip_frac": clip_frac,
        "silence_ratio_abs_lt_0.01": sil,
        "snr_proxy_db": snr, "spectral_centroid_hz_first5s": c,
        "dynamic_range_db": (20 * math.log10((peak + 1e-12) / (rms + 1e-12))),
    }


def extract_segment(path, t0, t1, out_path):
    x, sr = sf.read(str(path))
    if x.ndim > 1:
        x = x.mean(axis=1)
    i0, i1 = int(t0 * sr), int(t1 * sr)
    i0 = max(0, i0); i1 = min(len(x), max(i0 + 1, i1))
    sf.write(str(out_path), x[i0:i1], sr)
    return out_path


def main():
    t_all = time.perf_counter()
    q = audio_quality(DERIVED)
    print("QUALITY", {k: q[k] for k in ["duration_s", "rms", "peak", "silence_ratio_abs_lt_0.01", "snr_proxy_db"]}, flush=True)

    vad = SileroVadAdapter()
    asr = MoonshineAsrAdapter()
    ac = ParselmouthAcousticAdapter()
    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()

    t0 = time.perf_counter()
    v = vad.run(str(DERIVED))
    vad_t = time.perf_counter() - t0
    print(f"VAD segs={len(v.segments)} speech={v.speech_detected} t={vad_t:.2f}s", flush=True)

    # merge nearby segments (<0.3s gap) for ASR chunks; cap max 12s
    segs = []
    for s in v.segments:
        if not segs:
            segs.append([s.start_s, s.end_s])
            continue
        if s.start_s - segs[-1][1] < 0.35:
            segs[-1][1] = s.end_s
        else:
            segs.append([s.start_s, s.end_s])
    # split long
    final = []
    for a, b in segs:
        while b - a > 12.0:
            final.append((a, a + 12.0))
            a += 12.0
        if b - a >= 0.25:
            final.append((a, b))

    print(f"merged_segments={len(final)}", flush=True)
    seg_dir = REPO / "Research" / "Speech" / "Phase1_7" / "AudioDerived" / "segments"
    seg_dir.mkdir(parents=True, exist_ok=True)

    segment_rows = []
    for i, (a, b) in enumerate(final[:40]):  # safety cap
        sp = seg_dir / f"seg_{i:03d}_{a:.2f}_{b:.2f}.wav"
        extract_segment(DERIVED, a, b, sp)
        t1 = time.perf_counter()
        ar = asr.run(str(sp))
        asr_dt = time.perf_counter() - t1
        t2 = time.perf_counter()
        aco = ac.run(str(sp))
        ac_dt = time.perf_counter() - t2
        text = (ar.text or "").strip()
        # language hypothesis heuristics (NOT definitive)
        # moonshine is EN-only model → empty/garbage may mean non-English
        letters = sum(c.isalpha() for c in text)
        ascii_letters = sum(c.isascii() and c.isalpha() for c in text)
        lang_hyp = "uncertain"
        if not text:
            lang_hyp = "non_english_or_noise_or_unintelligible_to_en_asr"
        elif letters and ascii_letters / max(1, letters) > 0.95:
            lang_hyp = "english_hypothesis_from_en_asr_only"
        else:
            lang_hyp = "uncertain_non_ascii_or_mixed"

        row = {
            "seg_i": i, "start_s": a, "end_s": b, "dur_s": b - a,
            "asr_text": text, "asr_model": ar.model, "asr_s": asr_dt,
            "language_hypothesis": lang_hyp,
            "language_method": "moonshine-tiny-en-int8_english_only_asr_heuristic",
            "language_note": "EN-only ASR cannot confirm Vietnamese; empty/nonsensical output is NOT proof of Vietnamese",
            "f0_mean": aco.f0_mean, "f1_mean": aco.f1_mean, "f2_mean": aco.f2_mean,
            "intensity": aco.intensity_mean, "voiced_frac": aco.f0_voiced_frac,
            "acoustic_s": ac_dt,
            "pronunciation_eligible": False,
            "pronunciation_reason": "no_authorized_known_english_target_for_segment",
        }
        segment_rows.append(row)
        print(f"SEG {i:02d} {a:7.2f}-{b:7.2f} asr=[{text[:60]}] lang={lang_hyp} f0={aco.f0_mean}", flush=True)

    # whole-file acoustic
    t3 = time.perf_counter()
    whole_ac = ac.run(str(DERIVED))
    whole_ac_t = time.perf_counter() - t3

    # synthetic compare on sapi_red
    syn_q = audio_quality(SYN)
    syn_v = vad.run(str(SYN))
    syn_a = asr.run(str(SYN))
    syn_ac = ac.run(str(SYN))

    # optional: if any segment ASR is a clean single known LWE word, score it
    known = {"red", "blue", "apple", "cat", "dog", "book", "big", "ball", "small", "open", "close", "one", "two"}
    scored = []
    for row in segment_rows:
        toks = [t.lower().strip(".,!?;:\"'") for t in row["asr_text"].split()]
        # only score if EXACTLY one known word and nothing else meaningful
        content = [t for t in toks if t.isalpha()]
        if len(content) == 1 and content[0] in known:
            sp = seg_dir / f"seg_{row['seg_i']:03d}_{row['start_s']:.2f}_{row['end_s']:.2f}.wav"
            target = content[0]
            st = tgt.build(target)
            soft = pev.soft_match(str(sp), st.arpabet)
            scored.append({
                "seg_i": row["seg_i"], "target": target,
                "asr": row["asr_text"],
                "soft_score": soft.soft_score_0_100,
                "soft_conf": soft.confidence_0_1,
                "note": "scored_only_because_en_asr_emitted_single_known_lwe_word; target_is_asr_hypothesis_not_ground_truth",
                "pronunciation_ground_truth": False,
            })
            row["pronunciation_eligible"] = False  # still not GT target
            row["asr_hypothesis_score_probe"] = scored[-1]

    # research role classification
    n_text = sum(1 for r in segment_rows if r["asr_text"])
    n_empty = sum(1 for r in segment_rows if not r["asr_text"])
    roles = []
    if v.speech_detected:
        roles.append("REAL_SPEAKER_UNKNOWN_AGE")
    else:
        roles.append("NON_SPEECH_OR_VAD_FAILED")
    if n_text:
        roles.append("POSSIBLE_ENGLISH_SEGMENTS_VIA_EN_ASR")
    if n_empty:
        roles.append("SEGMENTS_UNINTELLIGIBLE_TO_EN_ASR_MAY_BE_NON_ENGLISH_OR_NOISE")
    roles.append("REAL_MICROPHONE_RECORDING_CONDITION_SAMPLE")
    roles.append("NOT_CANONICAL_REFERENCE")
    roles.append("NOT_GOLDEN_CHILD_VOICE")

    report = {
        "audio_id": "IMG_0639",
        "source_file": "IMG_0639.mp3",
        "source_path": str(REPO / "IMG_0639.mp3"),
        "sha256": "E4CDF24D2B5140B91EF637676CD5FA1B9A7F6B6A629F62AE9AC2657CB171A6F7",
        "original_untouched": True,
        "original_format": {
            "container": "mp3", "codec": "mp3", "sample_rate": 48000,
            "channels": 2, "duration_s": 219.284, "bit_rate": 192000, "size_bytes": 5264528,
        },
        "derived": {
            "path_repo": str(DERIVED),
            "path_external": r"D:\speech-lab\data\real-audio\IMG_0639_16k_mono.wav",
            "sample_rate": 16000, "channels": 1, "codec": "pcm_s16le",
        },
        "privacy_sensitive": True,
        "consent_status": "unknown",
        "speaker_age_months": None,
        "speaker_age_status": "UNKNOWN",
        "speaker_identity": "UNKNOWN",
        "language_global": "UNKNOWN_NEEDS_HUMAN_OR_MULTILINGUAL_LID",
        "language_method_limitation": "primary ASR is English-only moonshine-tiny-en",
        "audio_quality": q,
        "vad": {
            "speech_detected": v.speech_detected,
            "n_raw_segments": len(v.segments),
            "n_merged_segments": len(final),
            "model": v.model,
            "processing_s": vad_t,
            "raw_segments": [{"start": s.start_s, "end": s.end_s} for s in v.segments[:200]],
        },
        "segments": segment_rows,
        "whole_file_acoustic": {
            "f0_mean": whole_ac.f0_mean, "f0_std": whole_ac.f0_std,
            "f1_mean": whole_ac.f1_mean, "f2_mean": whole_ac.f2_mean,
            "intensity": whole_ac.intensity_mean, "voiced_frac": whole_ac.f0_voiced_frac,
            "duration_s": whole_ac.duration_s, "processing_s": whole_ac_t,
            "warnings": whole_ac.warnings,
        },
        "asr_hypothesis_score_probes": scored,
        "synthetic_compare_sapi_red": {
            "quality": syn_q,
            "vad_segs": len(syn_v.segments),
            "asr": syn_a.text,
            "f0": syn_ac.f0_mean, "f1": syn_ac.f1_mean, "f2": syn_ac.f2_mean,
        },
        "research_roles": roles,
        "pronunciation_scoring_policy": "NOT_RUN_WITHOUT_AUTHORIZED_KNOWN_TARGET; ASR-only probes marked non-GT",
        "canonical_truth": "CMUdict/G2P only; this file is NOT canonical",
        "total_processing_s": time.perf_counter() - t_all,
        "pipeline_baseline": "Phase1.6 bdc5851 / protocol 1.6.0 components",
    }
    outp = OUT / "IMG_0639_analysis.json"
    outp.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("WROTE", outp, "segs", len(segment_rows), "scored_probes", len(scored), flush=True)


if __name__ == "__main__":
    main()
