"""Phase 1.9.18 shared evaluation for the frozen baseline / B1.

A single scoring function scores an in-memory waveform through the frozen
PhoneEvidenceV2 call path (soft_match on a temp wav) OR through the B1 head.
Both produce a per-utterance, per-phone record so the SAME evaluation protocol
(FRR-first on expert-correct phones, FAR on expert-bad phones) applies.

FRR-first definition (per phone token, not utterance):
  expert-correct token = expert phone accuracy >= 1.0
  rejected = per-phone sim < 0.5  (the frozen scorer's decision boundary,
             mirrored from the utterance-level soft<50 threshold)
  FRR = fraction of expert-correct tokens with sim < 0.5
  FAR = fraction of expert-bad (<0.5) tokens with sim >= 0.5
"""
from __future__ import annotations

import io
import sys
import tempfile
import wave
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

GOOD_THRESHOLD = 1.0      # expert score >= 1.0 => expert-correct phone
REJECT_SIM = 0.5          # per-phone sim < 0.5 => rejected


def write_temp_wav(samples, sr=16000) -> str:
    import numpy as np
    pcm = (np.clip(samples, -1.0, 1.0) * 32767.0).astype("<i2")
    fd, path = tempfile.mkstemp(suffix=".wav")
    import os
    os.close(fd)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(pcm.tobytes())
    return path


def score_baseline_utterance(pev, samples, canonical_arpa):
    """Score one waveform with the frozen PhoneEvidenceV2 pipeline."""
    path = write_temp_wav(samples)
    try:
        sm = pev.soft_match(path, canonical_arpa)
    finally:
        Path(path).unlink(missing_ok=True)
    return sm


def per_phone_rows(sm, canonical_arpa, expert_acc, error_type, meta):
    """Align a SoftMatchResult to per-phone supervision rows."""
    rows = []
    for j, (ph, acc, et) in enumerate(zip(canonical_arpa, expert_acc,
                                          error_type)):
        hit = sm.hits[j] if j < len(sm.hits) else None
        rows.append({
            **meta,
            "phone_index": j,
            "canonical_phone": ph,
            "expert_acc": acc,
            "error_type": et,
            "sim": hit.sim if hit else 0.0,
            "posterior": hit.posterior if hit else 0.0,
            "match_type": hit.match_type if hit else "miss",
            "best_obs": hit.best_obs if hit else "",
            "source": "frozen_baseline",
        })
    return rows


def frr_far(rows):
    """Compute FRR-first metrics from per-phone rows."""
    good = [r for r in rows if r["expert_acc"] is not None
            and r["expert_acc"] >= GOOD_THRESHOLD]
    bad = [r for r in rows if r["expert_acc"] is not None
           and r["expert_acc"] < 0.5]
    n_good = len(good)
    n_bad = len(bad)
    rejected = sum(1 for r in good if r["sim"] < REJECT_SIM)
    accepted_bad = sum(1 for r in bad if r["sim"] >= REJECT_SIM)
    return {
        "n_good": n_good,
        "n_bad": n_bad,
        "frr": (rejected / n_good) if n_good else None,
        "far": (accepted_bad / n_bad) if n_bad else None,
        "rejected_good": rejected,
        "accepted_bad": accepted_bad,
    }
