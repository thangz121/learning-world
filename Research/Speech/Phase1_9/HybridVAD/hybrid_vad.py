"""Hybrid VAD research module — Phase 1.9.
Silero remains baseline thr=0.5. Adaptive gates tested; no global thr lower as default.
"""
from __future__ import annotations
from dataclasses import dataclass, asdict  # asdict used for Seg serialization
from typing import List, Dict, Tuple, Optional
import numpy as np
import soundfile as sf
import torch
import time

from silero_vad import load_silero_vad, get_speech_timestamps


@dataclass
class Seg:
    start: float
    end: float


def load_mono16(path: str) -> Tuple[np.ndarray, int]:
    x, sr = sf.read(path)
    if x.ndim > 1:
        x = x.mean(axis=1)
    x = x.astype(np.float32)
    if sr != 16000:
        raise ValueError(f"need 16k got {sr}")
    return x, sr


def frame_rms(x: np.ndarray, sr: int, frame_ms=30, hop_ms=10) -> Tuple[np.ndarray, np.ndarray]:
    frame = int(sr * frame_ms / 1000)
    hop = int(sr * hop_ms / 1000)
    n = 1 + max(0, (len(x) - frame) // hop)
    rms = np.zeros(n, dtype=np.float64)
    times = np.zeros(n, dtype=np.float64)
    for i in range(n):
        s = i * hop
        sl = x[s:s + frame]
        rms[i] = np.sqrt(np.mean(sl ** 2) + 1e-20)
        times[i] = (s + frame / 2) / sr
    return rms, times


def frame_spectral(x: np.ndarray, sr: int, frame_ms=30, hop_ms=10):
    frame = int(sr * frame_ms / 1000)
    hop = int(sr * hop_ms / 1000)
    n = 1 + max(0, (len(x) - frame) // hop)
    centroid = np.zeros(n)
    flatness = np.zeros(n)
    low_ratio = np.zeros(n)
    mid_ratio = np.zeros(n)
    high_ratio = np.zeros(n)
    flux = np.zeros(n)
    prev = None
    win = np.hanning(frame)
    for i in range(n):
        s = i * hop
        sl = x[s:s + frame] * win
        spec = np.abs(np.fft.rfft(sl)) + 1e-12
        freqs = np.fft.rfftfreq(frame, 1 / sr)
        centroid[i] = np.sum(freqs * spec) / np.sum(spec)
        logm = np.mean(np.log(spec))
        flatness[i] = np.exp(logm) / np.mean(spec)
        e = np.sum(spec)
        low_ratio[i] = np.sum(spec[freqs < 300]) / e
        mid_ratio[i] = np.sum(spec[(freqs >= 300) & (freqs < 3000)]) / e
        high_ratio[i] = np.sum(spec[freqs >= 3000]) / e
        if prev is not None:
            flux[i] = np.sum(np.abs(spec - prev)) / (np.sum(spec) + np.sum(prev))
        prev = spec
    return {
        "centroid": centroid, "flatness": flatness, "low": low_ratio,
        "mid": mid_ratio, "high": high_ratio, "flux": flux,
    }


def noise_floor_percentile(rms: np.ndarray, pct=20.0, win=100) -> np.ndarray:
    """Moving lower-percentile noise floor estimate."""
    n = len(rms)
    out = np.zeros(n)
    half = win // 2
    for i in range(n):
        a = max(0, i - half)
        b = min(n, i + half + 1)
        out[i] = np.percentile(rms[a:b], pct)
    return out


def mask_to_segments(mask: np.ndarray, times: np.ndarray, hop_s: float,
                     min_speech_s=0.15, min_silence_s=0.2) -> List[Seg]:
    """Convert boolean frame mask to segments with hangover merge."""
    segs = []
    in_sp = False
    start = 0.0
    for i, m in enumerate(mask):
        t = times[i]
        if m and not in_sp:
            in_sp = True
            start = t
        elif not m and in_sp:
            in_sp = False
            end = t
            if end - start >= min_speech_s:
                segs.append(Seg(start, end))
    if in_sp:
        end = times[-1] + hop_s
        if end - start >= min_speech_s:
            segs.append(Seg(start, end))
    # merge gaps < min_silence_s
    if not segs:
        return []
    merged = [segs[0]]
    for s in segs[1:]:
        if s.start - merged[-1].end < min_silence_s:
            merged[-1] = Seg(merged[-1].start, s.end)
        else:
            merged.append(s)
    return merged


def silero_segments(x: np.ndarray, model, thr=0.5) -> List[Seg]:
    ts = get_speech_timestamps(torch.tensor(x), model, return_seconds=True, threshold=thr)
    return [Seg(float(t["start"]), float(t["end"])) for t in ts]


def energy_gate_mask(rms, floor, margin_db=6.0) -> np.ndarray:
    # margin in dB relative to floor
    thr = floor * (10 ** (margin_db / 20.0))
    return rms > thr


def spectral_speech_mask(spec: dict) -> np.ndarray:
    """Speech-like: not too flat, mid-band energy present, some flux."""
    flat = spec["flatness"]
    mid = spec["mid"]
    flux = spec["flux"]
    # speech tends to lower flatness than noise, mid energy, some flux
    return (flat < 0.55) & (mid > 0.25) & (flux > 0.02)


class HybridVAD:
    def __init__(self):
        self.model = None

    def _ensure(self):
        if self.model is None:
            self.model = load_silero_vad()

    def run(self, path: str, mode: str = "silero", silero_thr=0.5,
            energy_margin_db=6.0, noise_pct=20.0) -> Dict:
        t0 = time.perf_counter()
        x, sr = load_mono16(path)
        hop_s = 0.01
        rms, times = frame_rms(x, sr)
        spec = frame_spectral(x, sr)
        floor = noise_floor_percentile(rms, pct=noise_pct)
        e_mask = energy_gate_mask(rms, floor, margin_db=energy_margin_db)
        s_mask = spectral_speech_mask(spec)

        self._ensure()
        # silero frame probs approximate via segments rasterized
        sil_segs = silero_segments(x, self.model, thr=silero_thr)
        sil_mask = np.zeros(len(times), dtype=bool)
        for s in sil_segs:
            sil_mask |= (times >= s.start) & (times <= s.end)

        if mode == "silero":
            segs = sil_segs
            mask = sil_mask
        elif mode == "energy":
            mask = e_mask
            segs = mask_to_segments(mask, times, hop_s)
        elif mode == "spectral":
            mask = s_mask
            segs = mask_to_segments(mask, times, hop_s)
        elif mode == "noise_floor_energy":
            mask = e_mask
            segs = mask_to_segments(mask, times, hop_s)
        elif mode == "energy_and_silero":
            mask = e_mask & sil_mask
            segs = mask_to_segments(mask, times, hop_s)
        elif mode == "energy_or_silero":
            mask = e_mask | sil_mask
            segs = mask_to_segments(mask, times, hop_s)
        elif mode == "adaptive_then_silero":
            # hierarchical: energy prefilter windows, then silero on full but keep only silero segs that overlap energy candidates
            e_segs = mask_to_segments(e_mask, times, hop_s, min_speech_s=0.1)
            segs = []
            for s in sil_segs:
                for e in e_segs:
                    # overlap
                    o0, o1 = max(s.start, e.start), min(s.end, e.end)
                    if o1 - o0 > 0.08:
                        segs.append(Seg(s.start, s.end))
                        break
            # merge
            segs = sorted(segs, key=lambda z: z.start)
            merged = []
            for s in segs:
                if not merged or s.start - merged[-1].end > 0.15:
                    merged.append(s)
                else:
                    merged[-1] = Seg(merged[-1].start, max(merged[-1].end, s.end))
            segs = merged
            mask = np.zeros(len(times), dtype=bool)
            for s in segs:
                mask |= (times >= s.start) & (times <= s.end)
        elif mode == "hybrid_score":
            # score fusion then threshold
            # normalize energy relative
            rel = (rms - floor) / (floor + 1e-8)
            rel_n = np.clip(rel / 5.0, 0, 1)
            flat_n = np.clip(1.0 - flatness_safe(spec["flatness"]), 0, 1)
            mid_n = np.clip(spec["mid"] / 0.5, 0, 1)
            flux_n = np.clip(spec["flux"] / 0.1, 0, 1)
            sil_n = sil_mask.astype(float)
            score = 0.35 * sil_n + 0.25 * rel_n + 0.20 * mid_n + 0.10 * flat_n + 0.10 * flux_n
            mask = score >= 0.45
            segs = mask_to_segments(mask, times, hop_s)
        elif mode == "spectral_and_energy_then_silero":
            pre = e_mask & s_mask
            pre_segs = mask_to_segments(pre, times, hop_s, min_speech_s=0.1)
            segs = []
            for s in sil_segs:
                for e in pre_segs:
                    o0, o1 = max(s.start, e.start), min(s.end, e.end)
                    if o1 - o0 > 0.08:
                        segs.append(Seg(s.start, s.end))
                        break
            segs = sorted(segs, key=lambda z: z.start)
            merged = []
            for s in segs:
                if not merged or s.start - merged[-1].end > 0.15:
                    merged.append(s)
                else:
                    merged[-1] = Seg(merged[-1].start, max(merged[-1].end, s.end))
            segs = merged
            mask = np.zeros(len(times), dtype=bool)
            for s in segs:
                mask |= (times >= s.start) & (times <= s.end)
        else:
            raise ValueError(mode)

        dur = len(x) / sr
        speech = sum(s.end - s.start for s in segs)
        return {
            "mode": mode,
            "silero_thr": silero_thr,
            "energy_margin_db": energy_margin_db,
            "n_segments": len(segs),
            "segments": [asdict(s) for s in segs],
            "speech_duration_s": speech,
            "speech_ratio": speech / max(1e-9, dur),
            "duration_s": dur,
            "processing_s": time.perf_counter() - t0,
            "profile": {
                "rms_mean": float(np.mean(rms)),
                "floor_mean": float(np.mean(floor)),
                "energy_gate_frac": float(np.mean(e_mask)),
                "spectral_gate_frac": float(np.mean(s_mask)),
                "silero_frac": float(np.mean(sil_mask)),
            },
        }


def flatness_safe(f):
    return np.clip(f, 0, 1)


def segment_iou(ref: List[Dict], hyp: List[Dict], duration: float, step=0.01) -> Dict:
    """Frame-level metrics from segment lists."""
    n = int(duration / step) + 1
    r = np.zeros(n, dtype=bool)
    h = np.zeros(n, dtype=bool)
    for s in ref:
        a, b = int(s["start"] / step), int(s["end"] / step)
        r[max(0, a):min(n, b + 1)] = True
    for s in hyp:
        a, b = int(s["start"] / step), int(s["end"] / step)
        h[max(0, a):min(n, b + 1)] = True
    tp = np.sum(r & h)
    fp = np.sum(~r & h)
    fn = np.sum(r & ~h)
    tn = np.sum(~r & ~h)
    prec = tp / max(1, tp + fp)
    rec = tp / max(1, tp + fn)
    f1 = 2 * prec * rec / max(1e-12, prec + rec)
    return {
        "precision": float(prec), "recall": float(rec), "f1": float(f1),
        "fpr": float(fp / max(1, fp + tn)), "fnr": float(fn / max(1, fn + tp)),
        "speech_dur_ref": float(np.sum(r) * step),
        "speech_dur_hyp": float(np.sum(h) * step),
        "dur_error": float(abs(np.sum(h) - np.sum(r)) * step),
    }
