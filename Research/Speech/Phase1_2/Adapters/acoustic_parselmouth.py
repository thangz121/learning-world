"""Parselmouth / Praat acoustic features (evidence, not absolute truth)."""
from __future__ import annotations
import soundfile as sf

from Research.Speech.Phase1_2.Schemas.speaking_schemas import AcousticFeatures, now


class ParselmouthAcousticAdapter:
    name = "parselmouth"
    version = "0.4.7"

    def run(self, wav_path: str) -> AcousticFeatures:
        t0 = now()
        warnings = []
        try:
            import parselmouth
            from parselmouth.praat import call
        except Exception as e:
            audio, sr = sf.read(str(wav_path))
            return AcousticFeatures(
                f0_mean=None, f0_std=None, f0_voiced_frac=None,
                f1_mean=None, f2_mean=None, f3_mean=None,
                intensity_mean=None, duration_s=len(audio) / sr,
                processing_s=now() - t0, model=self.name,
                warnings=[f"parselmouth_unavailable:{type(e).__name__}"])

        snd = parselmouth.Sound(str(wav_path))
        dur = float(snd.get_total_duration())
        pitch = snd.to_pitch_ac(time_step=0.01, pitch_floor=75, pitch_ceiling=600)
        f0_vals = [pitch.get_value_at_time(t) for t in
                   [pitch.xs()[i] for i in range(pitch.n_frames)]]
        f0_voiced = [v for v in f0_vals if v is not None and v == v and v > 0]
        f0_mean = float(sum(f0_voiced) / len(f0_voiced)) if f0_voiced else None
        f0_std = None
        if f0_voiced and len(f0_voiced) > 1:
            m = f0_mean
            f0_std = float((sum((x - m) ** 2 for x in f0_voiced) / len(f0_voiced)) ** 0.5)
        voiced_frac = len(f0_voiced) / max(1, len(f0_vals))

        formant = snd.to_formant_burg(time_step=0.01, max_number_of_formants=5,
                                      maximum_formant=5500, window_length=0.025)
        def mean_formant(n):
            vals = []
            for t in formant.xs():
                v = formant.get_value_at_time(n, t)
                if v is not None and v == v and v > 0:
                    vals.append(v)
            return float(sum(vals) / len(vals)) if vals else None

        try:
            intensity = call(snd, "To Intensity", 75, 0.0, "yes")
            inten_mean = float(call(intensity, "Get mean", 0, 0, "energy"))
        except Exception:
            inten_mean = None
            warnings.append("intensity_failed")

        if not f0_voiced:
            warnings.append("no_voiced_frames")

        return AcousticFeatures(
            f0_mean=f0_mean, f0_std=f0_std, f0_voiced_frac=voiced_frac,
            f1_mean=mean_formant(1), f2_mean=mean_formant(2), f3_mean=mean_formant(3),
            intensity_mean=inten_mean, duration_s=dur,
            processing_s=now() - t0, model=f"{self.name}@{self.version}",
            warnings=warnings,
        )
