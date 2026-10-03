"""Phase 1.9.18 B1 head-only adaptation (frozen encoder + trainable head).

Architecture (STEP 5):
  FROZEN wav2vec2-xlsr-53-espeak-cv-ft@2c73378  -> frame logits [T, V]
  + frozen CTC forced alignment (ctc_forced_v1) -> per-target-phone span
  + FEATURE = [expected-phone class posterior mass over span, top-k soft evidence
    (top1 sim, top2 sim, margin), span length, mean frame entropy, forward/back
    posterior gap] (fixed, encoder-only; no label leakage)
  + TRAINABLE small MLP head (B1): predicts a calibrated "expert-correct"
    probability for the phone token.

The frozen PhoneEvidenceV2 similarity `sim` is UNCHANGED and is still the
decision score; B1 re-scores a phone as accepted (sim set to its B1 probability
scaled like the baseline) ONLY via its own head. Concretely: B1 outputs a
correctness probability p; the B1 per-phone evidence is `p` in [0,1] mapped to a
0-100 style score. FRR/FAR use the same 0.5 decision boundary as the baseline.

TARGET (STEP 2, auditable):
  y = 1 if expert phone accuracy >= 1.0 (expert-correct)
  y = 0 if expert phone accuracy < 0.5  (expert-bad)
  tokens with 0.5 <= acc < 1.0 are AMBIGUOUS and excluded from the binary target
  (they are neither positive nor negative), matching the corpus's own bad
  threshold; this is documented so the exclusion cannot hide failures.

LOSS: weighted binary cross-entropy (pos_weight = n_neg/n_pos).
No pseudo-labels. `<unk>`/`<DEL>` never become phones.
"""
from __future__ import annotations

import numpy as np
import torch
import torch.nn as nn


def span_features(probs, spans, target_canon, inv, canon_ids):
    """Encoder-only features per target phone. probs: torch [T,V]."""
    T, V = probs.shape
    feats = []
    logp = torch.log(probs.clamp(min=1e-9))
    for j, ph in enumerate(target_canon):
        if j < len(spans):
            s, e = spans[j]
        else:
            s, e = 0, max(0, T - 1)
        s = max(0, min(T - 1, s))
        e = max(s, min(T - 1, e))
        seg = probs[s:e + 1].mean(dim=0)
        ids = canon_ids.get(ph, [])
        post = float(seg[ids].sum().item()) if ids else 0.0
        # top-k normalized tokens by similarity to expected
        topv, topi = torch.topk(seg, k=min(5, seg.numel()))
        sims = []
        for v, i in zip(topv.tolist(), topi.tolist()):
            tok = None
            sims.append((i, v))
        # entropy of the span-averaged distribution
        ent = float(-(seg * logp[s:e + 1].mean(dim=0)).sum().item())
        row = [
            post,
            float(topv[0].item()) if topv.numel() else 0.0,
            float(topv[1].item()) if topv.numel() > 1 else 0.0,
            float((topv[0] - topv[1]).item()) if topv.numel() > 1 else 0.0,
            float(e - s + 1) / max(1, T),
            ent,
            1.0 if ids else 0.0,
        ]
        # posterior mass that lands on the expected-canonical vs alternatives
        row.append(float(seg.max().item()))
        feats.append(row)
    return np.asarray(feats, dtype=np.float32)


class B1Head(nn.Module):
    def __init__(self, in_dim, hidden=32):
        super().__init__()
        self.net = nn.Sequential(
            nn.Linear(in_dim, hidden), nn.ReLU(),
            nn.Linear(hidden, hidden), nn.ReLU(),
            nn.Linear(hidden, 1),
        )

    def forward(self, x):
        return self.net(x).squeeze(-1)
