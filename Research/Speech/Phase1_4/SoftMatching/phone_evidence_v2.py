"""Phone evidence with full posteriors + CTC forced alignment to target.
Soft matching uses top-k / similarity — does NOT boost from ASR text match.
"""
from __future__ import annotations
from dataclasses import dataclass, asdict
from typing import List, Dict, Tuple, Optional
from pathlib import Path
import json
import numpy as np
import soundfile as sf
import torch

from Research.Speech.Phase1_4.PhoneInventory.inventory import (
    PhonemeInventoryAdapter, phone_similarity, VERSION as INV_VERSION)
from Research.Speech.Phase1_2.Adapters.paths import HF_CACHE


@dataclass
class SoftPhoneHit:
    expected: str
    best_obs: str
    match_type: str  # exact | soft | miss
    sim: float
    posterior: float
    topk: List[Tuple[str, float]]
    start_frame: int
    end_frame: int
    start_s: float
    end_s: float


@dataclass
class SoftMatchResult:
    hits: List[SoftPhoneHit]
    mean_sim: float
    mean_posterior: float
    soft_score_0_100: float
    confidence_0_1: float
    conf_reasons: List[str]
    processing_s: float
    model: str
    inventory_version: str
    alignment_method: str


class PhoneEvidenceV2:
    """w2v2-espeak with logits retained for soft match + CTC align."""
    name = "phone-evidence-v2"
    version = "1.4.0"

    def __init__(self, cache_dir: str = None):
        self.cache_dir = str(cache_dir or HF_CACHE)
        self._model = None
        self._feat = None
        self._id2tok = None
        self._tok2id = None
        self._blank = 0
        self.inv = PhonemeInventoryAdapter()

    def _ensure(self):
        if self._model is not None:
            return
        from transformers import Wav2Vec2FeatureExtractor, Wav2Vec2ForCTC
        from huggingface_hub import hf_hub_download
        pid = "facebook/wav2vec2-xlsr-53-espeak-cv-ft"
        self._feat = Wav2Vec2FeatureExtractor.from_pretrained(pid, cache_dir=self.cache_dir)
        self._model = Wav2Vec2ForCTC.from_pretrained(pid, cache_dir=self.cache_dir).eval()
        vp = hf_hub_download(pid, "vocab.json", cache_dir=self.cache_dir)
        vocab = json.load(open(vp, encoding="utf-8"))
        self._id2tok = {int(v): k for k, v in vocab.items()}
        self._tok2id = {k: int(v) for k, v in vocab.items()}
        self._blank = int(vocab.get("<pad>", 0)) if not isinstance(vocab.get("<pad>", 0), str) else 0
        # build canon -> list of vocab ids
        self._canon_ids: Dict[str, List[int]] = {}
        for tid, tok in self._id2tok.items():
            if tok in ("|", "", "<pad>", "[PAD]"):
                continue
            c = self.inv.normalize_symbol(tok)
            if not c:
                continue
            self._canon_ids.setdefault(c, []).append(tid)

    def logits(self, wav_path: str):
        self._ensure()
        audio, sr = sf.read(str(wav_path))
        if audio.ndim > 1:
            audio = audio.mean(axis=1)
        if sr != 16000:
            raise ValueError(f"need 16k, got {sr}")
        with torch.no_grad():
            inp = self._feat(audio, sampling_rate=16000, return_tensors="pt").input_values
            logits = self._model(inp).logits[0]  # [T,V]
        probs = torch.softmax(logits, dim=-1)
        dur = len(audio) / sr
        return probs, dur, len(audio)

    def greedy_phones(self, probs: torch.Tensor) -> List[Tuple[str, float, int, int]]:
        ids = torch.argmax(probs, dim=-1).tolist()
        confs = probs.max(dim=-1).values.tolist()
        out = []
        prev = None
        start = 0
        for i, (idx, c) in enumerate(zip(ids, confs)):
            if idx == self._blank:
                prev = None
                continue
            tok = self._id2tok.get(idx, f"#{idx}")
            if tok in ("|", ""):
                prev = None
                continue
            can = self.inv.normalize_symbol(tok)
            if not can:
                prev = None
                continue
            if can != prev:
                out.append((can, float(c), i, i))
                start = i
            else:
                out[-1] = (can, max(out[-1][1], float(c)), out[-1][2], i)
            prev = can
        return out

    def ctc_align(self, probs: torch.Tensor, target_canon: List[str]) -> List[Tuple[int, int]]:
        """Viterbi-like forced alignment of target phones to frames using max posterior of phone class.
        Returns list of (start_frame, end_frame) per target phone.
        """
        T, V = probs.shape
        n = len(target_canon)
        if n == 0 or T == 0:
            return []
        # score[t][j] = best logprob aligning first j phones ending at frame t
        # Use simple DP: emission = log sum of ids for phone
        emit = torch.full((T, n), -1e9)
        for j, ph in enumerate(target_canon):
            ids = self._canon_ids.get(ph, [])
            if not ids:
                # try unnormalized / alias already applied
                emit[:, j] = torch.log(probs.max(dim=-1).values + 1e-12) * 0.1  # weak
            else:
                # sum posteriors of class members
                s = probs[:, ids].sum(dim=-1).clamp(min=1e-12)
                emit[:, j] = torch.log(s)

        # DP
        neg = -1e9
        dp = torch.full((T, n), neg)
        bp = torch.full((T, n), -1, dtype=torch.long)
        dp[0, 0] = emit[0, 0]
        for t in range(1, T):
            # stay on phone 0
            dp[t, 0] = dp[t - 1, 0] + emit[t, 0]
            bp[t, 0] = 0
            for j in range(1, n):
                stay = dp[t - 1, j] + emit[t, j]
                adv = dp[t - 1, j - 1] + emit[t, j]
                if adv >= stay:
                    dp[t, j] = adv
                    bp[t, j] = j - 1
                else:
                    dp[t, j] = stay
                    bp[t, j] = j
        # backtrack
        path = [0] * T
        j = n - 1
        for t in range(T - 1, -1, -1):
            path[t] = j
            if t > 0:
                prevj = int(bp[t, j].item())
                if prevj == j - 1:
                    j = prevj
                elif prevj == j:
                    j = prevj
                else:
                    j = prevj
        # collapse to spans
        spans = []
        cur = path[0]
        s = 0
        for t in range(1, T):
            if path[t] != cur:
                spans.append((s, t - 1))
                s = t
                cur = path[t]
        spans.append((s, T - 1))
        # ensure n spans by merge/split if needed
        while len(spans) > n:
            # merge shortest
            lengths = [e - s + 1 for s, e in spans]
            i = int(np.argmin(lengths[:-1]))
            spans[i] = (spans[i][0], spans[i + 1][1])
            del spans[i + 1]
        while len(spans) < n and spans:
            # split longest
            lengths = [e - s + 1 for s, e in spans]
            i = int(np.argmax(lengths))
            s0, e0 = spans[i]
            mid = (s0 + e0) // 2
            spans[i] = (s0, mid)
            spans.insert(i + 1, (mid + 1, e0))
        return spans[:n]

    def soft_match(self, wav_path: str, expected_arpa: List[str],
                   top_k: int = 5) -> SoftMatchResult:
        import time
        t0 = time.perf_counter()
        probs, dur, _ = self.logits(wav_path)
        T = probs.shape[0]
        frame_s = dur / max(1, T)
        target = self.inv.arpa_seq_to_canon(expected_arpa)
        spans = self.ctc_align(probs, target) if target else []
        hits: List[SoftPhoneHit] = []
        conf_reasons = []

        for j, ph in enumerate(target):
            if j < len(spans):
                s, e = spans[j]
            else:
                s, e = 0, max(0, T - 1)
            s = max(0, min(T - 1, s))
            e = max(s, min(T - 1, e))
            seg = probs[s:e + 1].mean(dim=0)  # avg posterior over span
            # top-k raw tokens normalized
            topv, topi = torch.topk(seg, k=min(top_k, seg.numel()))
            topk = []
            for v, i in zip(topv.tolist(), topi.tolist()):
                tok = self._id2tok.get(int(i), "?")
                can = self.inv.normalize_symbol(tok)
                if can:
                    topk.append((can, float(v)))
            # aggregate posterior mass for expected phone class
            ids = self._canon_ids.get(ph, [])
            post = float(seg[ids].sum().item()) if ids else 0.0
            # best soft among topk
            best_obs = topk[0][0] if topk else ""
            best_sim = phone_similarity(ph, best_obs)
            for obs, pv in topk:
                sim = phone_similarity(ph, obs)
                if sim > best_sim or (sim == best_sim and pv > post):
                    best_sim = sim
                    best_obs = obs
            # exact if post high or top1 exact
            if best_obs == ph or post >= 0.25:
                match_type = "exact" if (best_obs == ph or post >= 0.35) else "soft"
                sim = 1.0 if best_obs == ph else max(best_sim, min(1.0, post * 2))
            elif best_sim >= 0.35:
                match_type = "soft"
                sim = best_sim * (0.5 + 0.5 * post)
            else:
                match_type = "miss"
                sim = max(best_sim, post) * 0.5
            if post < 0.15:
                conf_reasons.append("PHONE_POSTERIOR_LOW")
            hits.append(SoftPhoneHit(
                expected=ph, best_obs=best_obs, match_type=match_type,
                sim=float(sim), posterior=post, topk=topk[:5],
                start_frame=s, end_frame=e,
                start_s=s * frame_s, end_s=(e + 1) * frame_s,
            ))

        if not hits:
            return SoftMatchResult(
                hits=[], mean_sim=0.0, mean_posterior=0.0, soft_score_0_100=0.0,
                confidence_0_1=0.0, conf_reasons=["AUDIO_TOO_SHORT_OR_EMPTY_TARGET"],
                processing_s=time.perf_counter() - t0, model=self.name,
                inventory_version=INV_VERSION, alignment_method="ctc_forced_v1")

        mean_sim = float(np.mean([h.sim for h in hits]))
        mean_post = float(np.mean([h.posterior for h in hits]))
        # score from similarity (not ASR)
        soft_score = 100.0 * mean_sim
        # confidence from posterior mass + consistency of match types
        n_exact = sum(1 for h in hits if h.match_type == "exact")
        n_miss = sum(1 for h in hits if h.match_type == "miss")
        conf = mean_post * (0.4 + 0.6 * (n_exact / len(hits))) * (1.0 - 0.5 * (n_miss / len(hits)))
        conf = float(max(0.0, min(1.0, conf)))
        if n_miss / len(hits) >= 0.5:
            conf_reasons.append("PHONE_MATCH_WEAK")
        if mean_post < 0.2:
            conf_reasons.append("PHONE_POSTERIOR_LOW")
        # unique reasons
        conf_reasons = sorted(set(conf_reasons))

        return SoftMatchResult(
            hits=hits, mean_sim=mean_sim, mean_posterior=mean_post,
            soft_score_0_100=round(soft_score, 1), confidence_0_1=round(conf, 3),
            conf_reasons=conf_reasons, processing_s=time.perf_counter() - t0,
            model=f"{self.name}@{self.version}", inventory_version=INV_VERSION,
            alignment_method="ctc_forced_v1",
        )
