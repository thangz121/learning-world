"""w2v2-xlsr-53-espeak-cv-ft phone evidence (primary from Phase 1.1)."""
from __future__ import annotations
from pathlib import Path
from typing import List, Tuple
import json

import numpy as np
import soundfile as sf
import torch

from Research.Speech.Phase1_2.Adapters.paths import HF_CACHE
from Research.Speech.Phase1_2.Schemas.speaking_schemas import (
    PhoneEvidence, PhoneToken, now)


class Wav2Vec2PhoneAdapter:
    name = "wav2vec2-xlsr-53-espeak-cv-ft"
    version = "facebook/wav2vec2-xlsr-53-espeak-cv-ft"

    def __init__(self, cache_dir: Path = None):
        self.cache_dir = str(cache_dir or HF_CACHE)
        self._model = None
        self._feat = None
        self._id2tok = None
        self._blank = 0

    def _ensure(self):
        if self._model is not None:
            return
        from transformers import Wav2Vec2FeatureExtractor, Wav2Vec2ForCTC
        from huggingface_hub import hf_hub_download
        pid = self.version
        self._feat = Wav2Vec2FeatureExtractor.from_pretrained(
            pid, cache_dir=self.cache_dir)
        self._model = Wav2Vec2ForCTC.from_pretrained(
            pid, cache_dir=self.cache_dir).eval()
        vp = hf_hub_download(pid, "vocab.json", cache_dir=self.cache_dir)
        vocab = json.load(open(vp, encoding="utf-8"))
        self._id2tok = {int(v): k for k, v in vocab.items()}
        self._blank = int(vocab.get("<pad>", 0)) if not isinstance(
            vocab.get("<pad>", 0), str) else 0

    def _ctc_decode(self, logits: torch.Tensor) -> Tuple[List[PhoneToken], float]:
        probs = torch.softmax(logits, dim=-1)
        ids = torch.argmax(probs, dim=-1).tolist()
        confs = probs.max(dim=-1).values.tolist()
        phones: List[PhoneToken] = []
        prev = None
        start = 0
        for i, (idx, c) in enumerate(zip(ids, confs)):
            if idx == self._blank:
                prev = None
                continue
            tok = self._id2tok.get(idx, f"#{idx}")
            if tok in ("|", " ", ""):
                prev = None
                continue
            if tok != prev:
                phones.append(PhoneToken(phone=tok, conf=float(c),
                                         start_frame=i, end_frame=i))
                start = i
            else:
                phones[-1].end_frame = i
                phones[-1].conf = max(phones[-1].conf, float(c))
            prev = tok
        frame_mean = float(np.mean(confs)) if confs else 0.0
        return phones, frame_mean

    def run(self, wav_path: str) -> PhoneEvidence:
        t0 = now()
        self._ensure()
        audio, sr = sf.read(str(wav_path))
        if sr != 16000:
            raise ValueError(f"expected 16kHz, got {sr}")
        if audio.ndim > 1:
            audio = audio.mean(axis=1)
        with torch.no_grad():
            inp = self._feat(audio, sampling_rate=16000, return_tensors="pt").input_values
            logits = self._model(inp).logits[0]
        phones, frame_mean = self._ctc_decode(logits)
        return PhoneEvidence(
            phones=phones,
            frame_mean_conf=frame_mean,
            processing_s=now() - t0,
            model=f"{self.name}",
            inventory="espeak-ipa",
        )
