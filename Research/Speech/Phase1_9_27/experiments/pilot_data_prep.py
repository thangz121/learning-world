"""WP-1.9.27 Parts 10-13 — pilot data contract: frozen split + recording QC.

Selects 10 SO762 child speakers NEVER used in WP-1.9.x evaluation sets, freezes a
speaker-disjoint split (train 6 / dev 2 / test 2), computes objective recording QC
and the target-final-consonant inventory per utterance. No audio modification, no
encoder inference, no training.
"""
from __future__ import annotations

import csv
import json
import math
import sys
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
import soundfile as sf

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_27"
ART = OUT / "artifacts"
P21 = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"
SO_MANIFEST = L.SO_MANIFEST

FIELDS = ["split", "speaker_id", "age", "utt_id", "text", "audio_path", "duration_s",
          "sample_rate", "clipping_frac", "rms", "silence_ratio", "snr_proxy",
          "voiced_fraction", "n_final_consonants", "final_phones", "qc_status",
          "exclude_reason"]


def used_speakers():
    used = set()
    for c in ("lwe", "so762_dev", "so762_test", "so762_absent_dev"):
        for r in csv.DictReader(open(P21 / f"token_evidence_{c}.csv", encoding="utf-8")):
            used.add(r["speaker_id"])
    return used


def qc(path):
    x, sr = sf.read(str(path))
    if x.ndim > 1:
        x = x.mean(axis=1)
    x = x.astype(np.float64)
    dur = len(x) / sr
    clip = float(np.mean(np.abs(x) > 0.99))
    frame = max(1, int(0.02 * sr))
    nf = len(x) // frame
    rms_f = np.array([np.sqrt(np.mean(x[i * frame:(i + 1) * frame] ** 2))
                      for i in range(nf)]) if nf else np.array([0.0])
    rms = float(np.sqrt(np.mean(x ** 2))) if len(x) else 0.0
    thr = 0.01 * (rms_f.max() if len(rms_f) else 0.0)
    silence = float(np.mean(rms_f < thr)) if len(rms_f) else 1.0
    noise = float(np.percentile(rms_f, 10)) if len(rms_f) else 0.0
    speech = float(np.percentile(rms_f, 90)) if len(rms_f) else 0.0
    snr = round(20 * math.log10((speech + 1e-9) / (noise + 1e-9)), 1) if noise else None
    voiced = None
    try:
        import parselmouth
        if dur >= 0.2:
            snd = parselmouth.Sound(x, sampling_frequency=sr)
            pitch = snd.to_pitch()
            arr = pitch.selected_array["frequency"]
            voiced = round(float(np.mean(arr > 0)), 3) if len(arr) else None
    except Exception:  # noqa: BLE001
        pass
    return {"duration_s": round(dur, 3), "sample_rate": sr, "clipping_frac": round(clip, 5),
            "rms": round(rms, 5), "silence_ratio": round(silence, 3),
            "snr_proxy": snr, "voiced_fraction": voiced}


def main():
    detail = json.loads((L.SO / "resource/scores-detail.json").read_text(encoding="utf-8"))
    man = [m for m in csv.DictReader(open(SO_MANIFEST, encoding="utf-8"))
           if m["is_child"] == "1"]
    used = used_speakers()
    by_spk = defaultdict(list)
    for m in man:
        by_spk[m["speaker_id"]].append(m)
    unused = sorted((spk for spk in by_spk if spk not in used),
                    key=lambda s: (int(by_spk[s][0]["age"]), s))
    # prefer speakers with >= 12 child utterances
    unused = [s for s in unused if len(by_spk[s]) >= 12]
    selected = unused[:10]
    split = {"train": selected[:6], "dev": selected[6:8], "test": selected[8:10]}
    ART.mkdir(parents=True, exist_ok=True)
    (ART / "pilot_split.json").write_text(json.dumps(
        {"frozen": True, "seed": 1927, "selection_rule":
         "SO762 child speakers NOT used in WP-1.9.x sets, sorted by (age, speaker_id), "
         "first 10 with >=12 utterances; train=first 6, dev=next 2, test=last 2",
         "split": split,
         "used_speakers_192x": sorted(used)}, indent=2), encoding="utf-8")

    rows = []
    for sp, members in split.items():
        for sid in members:
            for m in sorted(by_spk[sid], key=lambda x: x["utt_id"])[:20]:
                path = L.SO / "WAVE" / f"SPEAKER{int(sid):04d}" / f"{m['utt_id']}.WAV"
                if not path.exists():
                    continue
                q = qc(path)
                words = detail[m["utt_id"]]["words"]
                finals = []
                for w in words:
                    ref = (w.get("ref-phones") or "").split()
                    if ref:
                        import re as _re
                        base = _re.sub(r"\d$", "", ref[-1].upper())
                        if base not in {"AA", "AE", "AH", "AO", "AW", "AY", "EH", "ER",
                                        "EY", "IH", "IY", "OW", "OY", "UH", "UW"}:
                            finals.append(base)
                reason = ""
                status = "PASS"
                if q["duration_s"] < 0.4:
                    status, reason = "EXCLUDE", "TOO_SHORT"
                elif q["clipping_frac"] > 0.02:
                    status, reason = "EXCLUDE", "CLIPPING"
                elif q["snr_proxy"] is not None and q["snr_proxy"] < 5:
                    status, reason = "EXCLUDE", "LOW_SNR"
                rows.append({
                    "split": sp, "speaker_id": sid, "age": m["age"],
                    "utt_id": m["utt_id"], "text": m["text"], "audio_path": str(path),
                    **q, "n_final_consonants": len(finals),
                    "final_phones": ";".join(finals), "qc_status": status,
                    "exclude_reason": reason,
                })
    L.write_rows(OUT / "06_PILOT_DATA" / "PILOT_DATA_MANIFEST.csv", rows, FIELDS)
    summary = {
        "speakers": {k: v for k, v in split.items()},
        "utterances": len(rows),
        "utterances_pass": sum(1 for r in rows if r["qc_status"] == "PASS"),
        "excluded": sum(1 for r in rows if r["qc_status"] == "EXCLUDE"),
        "hours_pass": round(sum(r["duration_s"] for r in rows
                                if r["qc_status"] == "PASS") / 3600, 3),
        "final_consonant_tokens": sum(r["n_final_consonants"] for r in rows
                                      if r["qc_status"] == "PASS"),
        "ages": dict(Counter(r["age"] for r in rows if r["split"] == "train")),
        "phone_counts": dict(Counter(p for r in rows if r["qc_status"] == "PASS"
                                     for p in r["final_phones"].split(";") if p)),
    }
    (ART / "pilot_data_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=1))
    print("DONE pilot_data_prep")


if __name__ == "__main__":
    main()
