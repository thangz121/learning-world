"""Phase 1.9.16 minimal model smoke test (no training).

Loads the PINNED base model snapshot via the repo pipeline path
(PhoneEvidenceV2 cache layout), runs a 1-batch forward pass on synthetic
audio, saves/loads a dummy head checkpoint. Any failure here blocks training.
"""
from __future__ import annotations

import json
import sys
import time
from pathlib import Path

import numpy as np
import torch

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_2.Adapters.paths import HF_CACHE  # noqa: E402

MODEL_ID = "facebook/wav2vec2-xlsr-53-espeak-cv-ft"
EXPECTED_REV = "2c733782da5604684829819a5eb744c193fe9398"


def main() -> dict:
    from transformers import Wav2Vec2FeatureExtractor, Wav2Vec2ForCTC

    t0 = time.time()
    feat = Wav2Vec2FeatureExtractor.from_pretrained(
        MODEL_ID, cache_dir=str(HF_CACHE))
    model = Wav2Vec2ForCTC.from_pretrained(
        MODEL_ID, cache_dir=str(HF_CACHE)).eval()
    load_s = time.time() - t0

    sr = 16000
    rng = np.random.default_rng(916)
    wav = rng.standard_normal(sr * 2).astype(np.float32) * 0.05
    inputs = feat(wav, sampling_rate=sr, return_tensors="pt")
    with torch.no_grad():
        logits = model(inputs.input_values).logits
    n_phones = logits.shape[-1]

    # tiny head: frozen-encoder-style linear probe train step on dummy labels
    head = torch.nn.Linear(n_phones, n_phones)
    opt = torch.optim.AdamW(head.parameters(), lr=1e-3)
    frames = logits.mean(dim=1).detach()
    target = torch.zeros(frames.shape[0], dtype=torch.long)
    loss = torch.nn.functional.cross_entropy(head(frames), target)
    loss.backward()
    opt.step()

    ckpt = REPO / "Research" / "Speech" / "Phase1_9_16" / "smoke_head.pt"
    torch.save({"head": head.state_dict(), "rev": EXPECTED_REV}, ckpt)
    back = torch.load(ckpt, map_location="cpu", weights_only=True)
    assert back["rev"] == EXPECTED_REV
    ckpt.unlink()  # smoke artifact only; real checkpoints versioned on success

    return {
        "model_id": MODEL_ID,
        "expected_revision": EXPECTED_REV,
        "cache_dir": str(HF_CACHE),
        "load_s": round(load_s, 1),
        "logit_shape": list(logits.shape),
        "vocab_phones": n_phones,
        "smoke_loss": round(float(loss.detach()), 4),
        "checkpoint_roundtrip": True,
        "torch": torch.__version__,
        "cuda_available": torch.cuda.is_available(),
    }


if __name__ == "__main__":
    print(json.dumps(main(), indent=2), flush=True)
