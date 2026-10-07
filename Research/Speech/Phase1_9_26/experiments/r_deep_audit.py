"""WP-1.9.26 Part 7 — /r/ deep audit with acoustic features (research-only).

Reads the WP-1.9.25 R_CASES.csv and computes, for every /r/ case: context category,
duration, RMS energy, voiced fraction and F1/F2/F3 (parselmouth/Praat) over the
production span. No encoder run, no training.
"""
from __future__ import annotations

import csv
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
import soundfile as sf

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_26"
R_DIR = OUT / "03_R"
P21 = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"
P25 = L.REPO / "Research/Speech/Phase1_9_25"
PRIMARY = "full"

FIELDS = [
    "token_id", "corpus", "speaker_id", "age", "word", "target_phone",
    "context_position", "prev_phone", "next_phone", "following_silence",
    "label_source", "human_label_or_score", "human_confidence",
    "production_max_A", "alt_max_A", "span_frames", "span_ms", "rms_span",
    "rms_utterance", "rms_ratio", "voiced_fraction", "f1_median", "f2_median",
    "f3_median", "f3_f2_ratio", "isolated_peak", "window_class", "notes",
]


def num(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def main():
    cases = list(csv.DictReader(open(P25 / "R_CASES.csv", encoding="utf-8")))
    ev = {}
    for c in ("lwe", "so762_dev", "so762_test", "so762_absent_dev"):
        for r in csv.DictReader(open(P21 / f"token_evidence_{c}.csv", encoding="utf-8")):
            if r["window_type"] == PRIMARY:
                ev[r["token_id"]] = r
    # context lookup (prev/next phone, word position)
    from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
    tgt = CmuDictTargetAdapter()
    inv = L.PhoneEvidenceV2().inv
    import json as _json
    detail = _json.loads((L.SO / "resource/scores-detail.json").read_text(encoding="utf-8"))
    ctx = {}
    for c in ("lwe", "so762_dev", "so762_test", "so762_absent_dev"):
        for r in csv.DictReader(open(L.REPO / "Research/Speech/Phase1_9_22/artifacts/"
                                     f"spanmean_decision_{c}.csv", encoding="utf-8")):
            tid = r["token_id"]
            if c == "lwe":
                target = inv.arpa_seq_to_canon([str(x) for x in tgt.build(r["word"]).arpabet])
                j = len(target) - 1
                word_final = 1
            else:
                utt = tid.rsplit("_", 1)[0]
                words = L.parse_words_so762(detail[utt])
                ref, sc, wpos_l, ppos_l, wtext = L.flatten_so762(words)
                target = inv.arpa_seq_to_canon(ref)
                j = int(tid.rsplit("_", 1)[1])
                word_final = 1  # the 1.9.19 evaluation sets are word-final tokens
            prev = target[j - 1] if j > 0 else ""
            nxt = target[j + 1] if j + 1 < len(target) else ""
            ctx[tid] = {"prev": prev, "next": nxt, "word_final": word_final,
                        "utterance_final": int(j == len(target) - 1)}

    try:
        import parselmouth
        has_pm = True
    except Exception:  # noqa: BLE001
        has_pm = False

    rows = []
    for c in cases:
        tid = c["token_id"]
        e = ev.get(tid)
        if e is None:
            continue
        a, b = float(e["audio_start"]), float(e["audio_end"])
        if c["corpus"] == "lwe":
            src = L.find_source(c["speaker_id"], c["word"])
            if src is None:
                continue
            x = L.load_mono16(src)
        else:
            utt = tid.rsplit("_", 1)[0]
            wav = L.SO / "WAVE" / f"SPEAKER{int(c['speaker_id']):04d}" / f"{utt}.WAV"
            x, sr = sf.read(str(wav))
            if x.ndim > 1:
                x = x.mean(axis=1)
            x = x.astype(np.float32)
        s0 = int(e["span_A_start"])
        s1 = int(e["span_A_end"])
        fs = num(e["frame_s"], 0.02)
        seg = x[int(a * 16000):int(b * 16000)]
        span = seg[int(s0 * fs * 16000):int((s1 + 1) * fs * 16000)]
        if len(span) < 80:
            span = seg
        rms_span = float(np.sqrt(np.mean(span ** 2))) if len(span) else 0.0
        rms_utt = float(np.sqrt(np.mean(seg ** 2))) if len(seg) else 0.0
        f1 = f2 = f3 = voiced = ""
        if has_pm and len(span) > 200:
            try:
                snd = parselmouth.Sound(span.astype(np.float64), sampling_frequency=16000)
                form = snd.to_formant_burg(time_step=0.01, max_number_of_formants=5,
                                           maximum_formant=5500.0)
                times = np.arange(0.02, snd.duration - 0.02, 0.01)
                vals = {1: [], 2: [], 3: []}
                for t in times:
                    for k in (1, 2, 3):
                        v = form.get_value_at_time(k, t)
                        if v and not np.isnan(v):
                            vals[k].append(v)
                f1 = round(float(np.median(vals[1])), 1) if vals[1] else ""
                f2 = round(float(np.median(vals[2])), 1) if vals[2] else ""
                f3 = round(float(np.median(vals[3])), 1) if vals[3] else ""
                pitch = snd.to_pitch()
                n_voiced = sum(1 for v in pitch.selected_array["frequency"] if v > 0)
                voiced = round(n_voiced / max(1, len(pitch.selected_array["frequency"])), 3)
            except Exception:  # noqa: BLE001
                pass
        cx = ctx.get(tid, {})
        notes = []
        if isinstance(f3, float) and isinstance(f2, float) and f3 > 0:
            notes.append(f"F3-F2={round(f3 - f2, 1)}")
        rows.append({
            "token_id": tid, "corpus": c["corpus"], "speaker_id": c["speaker_id"],
            "age": c["age"], "word": c["word"], "target_phone": "ɹ",
            "context_position": ("word-final+utterance-final"
                                 if cx.get("word_final") and cx.get("utterance_final")
                                 else "word-final" if cx.get("word_final")
                                 else "medial"),
            "prev_phone": cx.get("prev", ""), "next_phone": cx.get("next", ""),
            "following_silence": int(cx.get("next", "") == ""),
            "label_source": c["label_source"],
            "human_label_or_score": c["human_label_or_score"],
            "human_confidence": c["human_confidence"],
            "production_max_A": c["max_A"], "alt_max_A": c["alt_encoder_max_A"],
            "span_frames": int(s1 - s0 + 1), "span_ms": round((s1 - s0 + 1) * fs * 1000, 1),
            "rms_span": round(rms_span, 5), "rms_utterance": round(rms_utt, 5),
            "rms_ratio": round(rms_span / rms_utt, 3) if rms_utt else "",
            "voiced_fraction": voiced, "f1_median": f1, "f2_median": f2, "f3_median": f3,
            "f3_f2_ratio": round(f3 / f2, 3) if isinstance(f3, float)
            and isinstance(f2, float) and f2 else "",
            "isolated_peak": int(int(num(c["cluster_width_D"], 1)) <= 1),
            "window_class": c["identity_class"], "notes": "; ".join(notes),
        })
    L.write_rows(R_DIR / "R_CASES.csv", rows, FIELDS)

    summary = {
        "n": len(rows), "has_parselmouth": has_pm,
        "by_corpus": dict(Counter(r["corpus"] for r in rows)),
        "span_ms_median": float(np.median([r["span_ms"] for r in rows])) if rows else None,
        "rms_ratio_median": float(np.median([r["rms_ratio"] for r in rows
                                             if r["rms_ratio"] != ""])) if rows else None,
        "f3_median_median": float(np.median([r["f3_median"] for r in rows
                                             if r["f3_median"] != ""]))
        if any(r["f3_median"] != "" for r in rows) else None,
        "f3_f2_ratio_median": float(np.median([r["f3_f2_ratio"] for r in rows
                                               if r["f3_f2_ratio"] != ""]))
        if any(r["f3_f2_ratio"] != "" for r in rows) else None,
        "isolated_peak_n": sum(r["isolated_peak"] for r in rows),
        "label_status": dict(Counter(r["human_label_or_score"] for r in rows)),
    }
    (OUT / "artifacts" / "r_acoustic_summary.json").write_text(
        json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=1))
    print("DONE r_deep_audit")


if __name__ == "__main__":
    main()
