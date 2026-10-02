"""Phone-local F1/F2 using VAD-trimmed proportional phone windows (approx).
Not forced-alignment ground truth — labeled APPROX_PROPORTIONAL.
"""
from __future__ import annotations
import json, sys
from pathlib import Path
import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Adapters.paths import PHASE11_AUDIO

OUT = REPO / "Research" / "Speech" / "Phase1_3" / "Acoustic"
OUT.mkdir(parents=True, exist_ok=True)

WORDS = [
    ("sapi_red.wav", "red"),
    ("sapi_cat.wav", "cat"),
    ("sapi_big.wav", "big"),
    ("pregen_apple_normal.wav", "apple"),
    ("sapi_blue.wav", "blue"),
    ("sapi_dog.wav", "dog"),
]


def formants_window(wav, sr, t0, t1):
    try:
        import parselmouth
    except Exception as e:
        return {"error": str(e)}
    # write temp slice
    i0, i1 = int(t0 * sr), int(t1 * sr)
    i0 = max(0, i0); i1 = min(len(wav), max(i0 + int(0.03 * sr), i1))
    sl = wav[i0:i1]
    if len(sl) < int(0.03 * sr):
        return {"error": "too_short"}
    import tempfile, os
    fd, path = tempfile.mkstemp(suffix=".wav")
    os.close(fd)
    sf.write(path, sl, sr)
    try:
        snd = parselmouth.Sound(path)
        formant = snd.to_formant_burg(time_step=0.01, max_number_of_formants=5,
                                      maximum_formant=5500, window_length=0.025)
        pitch = snd.to_pitch_ac(time_step=0.01, pitch_floor=75, pitch_ceiling=600)
        def mean_f(n):
            vals = []
            for t in formant.xs():
                v = formant.get_value_at_time(n, t)
                if v and v == v and v > 0:
                    vals.append(v)
            return float(np.mean(vals)) if vals else None
        f0s = [pitch.get_value_at_time(t) for t in pitch.xs()]
        f0s = [v for v in f0s if v and v == v and v > 0]
        return {
            "f0": float(np.mean(f0s)) if f0s else None,
            "f1": mean_f(1), "f2": mean_f(2), "f3": mean_f(3),
            "dur": (i1 - i0) / sr,
        }
    finally:
        try:
            os.remove(path)
        except Exception:
            pass


def main():
    pipe = SpeakingPipeline(enable_openpronounce=False)
    rows = []
    for fn, w in WORDS:
        r = pipe.run(str(PHASE11_AUDIO / fn), w)
        wav, sr = sf.read(str(PHASE11_AUDIO / fn))
        if wav.ndim > 1:
            wav = wav.mean(axis=1)
        # speech window from VAD or full
        if r.vad.segments:
            s0 = r.vad.segments[0].start_s
            s1 = r.vad.segments[-1].end_s
        else:
            s0, s1 = 0.0, len(wav) / sr
        phones = r.target.ipa
        n = max(1, len(phones))
        span = max(0.05, s1 - s0)
        phone_rows = []
        for i, ph in enumerate(phones):
            a = s0 + span * (i / n)
            b = s0 + span * ((i + 1) / n)
            ac = formants_window(wav, sr, a, b)
            # match diagnostic if any
            diag = next((d for d in r.scores["scorer_v1"].diagnostics if d.expected == ph), None)
            phone_rows.append({
                "phone": ph, "t0": a, "t1": b, "acoustic": ac,
                "diag_status": diag.status if diag else None,
                "diag_score": diag.score if diag else None,
            })
        rows.append({
            "file": fn, "word": w, "asr": r.asr.text,
            "score": r.primary_score_0_100, "method": "APPROX_PROPORTIONAL",
            "phones": phone_rows,
        })
        print("AC", w, "n_phones", n, flush=True)
    (OUT / "phone_local_formants.json").write_text(
        json.dumps(rows, indent=2, ensure_ascii=False), encoding="utf-8")
    print("WROTE", OUT / "phone_local_formants.json", flush=True)


if __name__ == "__main__":
    main()
