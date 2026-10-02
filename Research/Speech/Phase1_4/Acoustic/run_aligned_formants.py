"""Phone-local formants using CTC forced alignment spans from phone-evidence-v2."""
from __future__ import annotations
import json, sys, tempfile, os
from pathlib import Path
import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.Adapters.paths import PHASE11_AUDIO
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

OUT = REPO / "Research" / "Speech" / "Phase1_4" / "Acoustic"
OUT.mkdir(parents=True, exist_ok=True)

WORDS = [("sapi_red.wav","red"),("sapi_cat.wav","cat"),("sapi_big.wav","big"),
         ("pregen_apple_normal.wav","apple"),("sapi_blue.wav","blue"),("sapi_dog.wav","dog")]


def formants(path, t0, t1):
    import parselmouth
    wav, sr = sf.read(path)
    if wav.ndim > 1:
        wav = wav.mean(axis=1)
    i0, i1 = int(t0*sr), int(t1*sr)
    i0 = max(0,i0); i1 = min(len(wav), max(i0+int(0.025*sr), i1))
    sl = wav[i0:i1]
    fd, tmp = tempfile.mkstemp(suffix=".wav"); os.close(fd)
    sf.write(tmp, sl, sr)
    try:
        snd = parselmouth.Sound(tmp)
        dur = snd.get_total_duration()
        if dur < 0.04:
            return {"error": "too_short", "dur": dur}
        # pitch floor must be < 1/(window); use adaptive floor for short slices
        pfloor = min(75.0, max(50.0, 0.45 / max(dur, 0.05)))
        try:
            formant = snd.to_formant_burg(time_step=0.01, max_number_of_formants=5, maximum_formant=5500, window_length=min(0.025, dur/2))
            pitch = snd.to_pitch_ac(time_step=0.01, pitch_floor=pfloor, pitch_ceiling=600)
        except Exception as e:
            return {"error": type(e).__name__, "msg": str(e)[:120], "dur": dur}
        def mf(n):
            vals=[formant.get_value_at_time(n,t) for t in formant.xs()]
            vals=[v for v in vals if v and v==v and v>0]
            return float(np.mean(vals)) if vals else None
        f0s=[pitch.get_value_at_time(t) for t in pitch.xs()]
        f0s=[v for v in f0s if v and v==v and v>0]
        return {"f0": float(np.mean(f0s)) if f0s else None, "f1": mf(1), "f2": mf(2), "dur": (i1-i0)/sr}
    finally:
        try: os.remove(tmp)
        except Exception: pass


def main():
    pev = PhoneEvidenceV2(); tgt = CmuDictTargetAdapter()
    rows=[]
    for fn,w in WORDS:
        path = str(PHASE11_AUDIO/fn)
        st = tgt.build(w)
        soft = pev.soft_match(path, st.arpabet)
        phones=[]
        for h in soft.hits:
            ac = formants(path, h.start_s, h.end_s)
            phones.append({"phone": h.expected, "obs": h.best_obs, "type": h.match_type,
                           "sim": h.sim, "post": h.posterior, "t0": h.start_s, "t1": h.end_s, "acoustic": ac})
        rows.append({"file": fn, "word": w, "method": "ctc_forced_v1", "soft_score": soft.soft_score_0_100, "phones": phones})
        print("AC", w, flush=True)
    (OUT/"phone_formants_aligned.json").write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")
    print("WROTE", OUT/"phone_formants_aligned.json", flush=True)

if __name__ == "__main__":
    main()
