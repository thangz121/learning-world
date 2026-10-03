"""Compare parselmouth acoustics: 12 PHONE_MODEL_ERROR vs 17 AGREEMENT_OK child tokens."""
from __future__ import annotations

import csv
import json
import re
import sys
from pathlib import Path

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

EXT = REPO / "Research/Speech/ExternalData/zenodo_200495/extracted/english_children"
DER = REPO / "Research/Speech/ExternalData/zenodo_200495/derived_16k"
P199 = REPO / "Research/Speech/Phase1_9_9/Results"
RES = REPO / "Research/Speech/Phase1_9_10/Results"


def find_source(speaker_id: str, target: str):
    m = re.match(r"child_(\d+)", speaker_id or "")
    if not m:
        return None
    nn = m.group(1)
    base = EXT / "english_words_sentences"
    for sp in base.iterdir():
        if sp.name.startswith(nn + "_"):
            for mic in ("studio_mic", "port_mic", "nao_mic"):
                c = sp / mic / "numbers" / f"{target}.wav"
                if c.exists():
                    return c
            hits = list(sp.rglob(f"numbers/{target}.wav"))
            if hits:
                return hits[0]
    return None


def acoustics(x, sr):
    import parselmouth

    snd = parselmouth.Sound(x.astype(np.float64), sampling_frequency=sr)
    pitch = snd.to_pitch(pitch_floor=100, pitch_ceiling=600)
    f0 = pitch.selected_array["frequency"]
    f0 = f0[f0 > 0]
    formant = snd.to_formant_burg(max_number_of_formants=4, maximum_formant=5500)
    f1s, f2s = [], []
    for t in formant.xs():
        v1 = formant.get_value_at_time(1, t)
        v2 = formant.get_value_at_time(2, t)
        if v1 and v1 == v1:
            f1s.append(v1)
        if v2 and v2 == v2:
            f2s.append(v2)
    return {
        "f0_median": float(np.median(f0)) if len(f0) else None,
        "f0_range": float(np.ptp(f0)) if len(f0) else None,
        "f1_median": float(np.median(f1s)) if f1s else None,
        "f2_median": float(np.median(f2s)) if f2s else None,
        "duration_s": len(x) / sr,
    }


def main():
    human = list(csv.DictReader((P199 / "human_review_results.csv").open(encoding="utf-8-sig")))
    rows = []
    for r in human:
        diag = r.get("diagnostic")
        if diag not in ("PHONE_MODEL_ERROR", "AGREEMENT_OK"):
            continue
        src = find_source(r["speaker_id"], r["target"])
        if src is None:
            continue
        key = src.name
        # load via cache path logic (same as main script)
        import hashlib

        def sha256(p: Path):
            h = hashlib.sha256()
            with open(p, "rb") as f:
                for c in iter(lambda: f.read(1 << 20), b""):
                    h.update(c)
            return h.hexdigest().upper()

        cache = DER / (sha256(src)[:16] + "_" + re.sub(r"[^A-Za-z0-9_.-]+", "_", key)[:60] + ".wav")
        if cache.exists():
            x, sr = sf.read(str(cache))
        else:
            x, sr = sf.read(str(src))
            if x.ndim > 1:
                x = x.mean(axis=1)
            x = x.astype(np.float32)
        ac = acoustics(x, sr)
        rows.append(
            {
                "review_id": r["review_id"],
                "speaker_id": r["speaker_id"],
                "target": r["target"],
                "group": "PME" if diag == "PHONE_MODEL_ERROR" else "OK",
                "full_score": r["full_score"],
                **ac,
            }
        )
    with open(RES / "acoustic_group_comparison.csv", "w", newline="", encoding="utf-8") as f:
        keys = list(rows[0].keys())
        w = csv.DictWriter(f, fieldnames=keys)
        w.writeheader()
        w.writerows(rows)

    summary = {}
    for g in ("PME", "OK"):
        sub = [r for r in rows if r["group"] == g]
        for field in ("f0_median", "f0_range", "f1_median", "f2_median", "duration_s"):
            vals = [r[field] for r in sub if r[field] is not None]
            summary[f"{g}_{field}_mean"] = float(np.mean(vals)) if vals else None
            summary[f"{g}_{field}_n"] = len(vals)
    summary["note"] = (
        "Descriptive only. n(PME)=12, n(OK) from 1.9.9 AGREEMENT_OK; "
        "no statistical test claimed. F0/F1/F2 from parselmouth same method."
    )
    (RES / "acoustic_group_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
