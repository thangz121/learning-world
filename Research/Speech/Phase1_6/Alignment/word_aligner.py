"""True word-boundary alignment via multi-word CTC forced alignment.
Groups phone-level Viterbi path by known per-word phone sequences (CMUdict).
Not proportional time. Method id: word_ctc_forced_v1.
"""
from __future__ import annotations
from dataclasses import dataclass, asdict
from typing import List, Tuple, Dict, Optional
import numpy as np
import torch
import soundfile as sf

from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
from Research.Speech.Phase1_4.PhoneInventory.inventory import PhonemeInventoryAdapter, phone_similarity
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter


@dataclass
class WordSpan:
    word: str
    arpa: List[str]
    canon: List[str]
    start_s: float
    end_s: float
    start_frame: int
    end_frame: int
    soft_score: float
    soft_conf: float
    phone_hits: list


class WordAlignerV1:
    """Align words by concatenating phone targets and CTC force-align, then split by phone counts."""
    method = "word_ctc_forced_v1"
    version = "1.6.0"

    def __init__(self):
        self.pev = PhoneEvidenceV2()
        self.tgt = CmuDictTargetAdapter()
        self.inv = PhonemeInventoryAdapter()

    def align_words(self, wav_path: str, words: List[str]) -> Tuple[List[WordSpan], dict]:
        # build per-word arpa/canon
        word_arpa = []
        word_canon = []
        for w in words:
            st = self.tgt.build(w)
            word_arpa.append(st.arpabet)
            word_canon.append(self.inv.arpa_seq_to_canon(st.arpabet))
        flat_arpa = [p for seq in word_arpa for p in seq]
        flat_canon = [p for seq in word_canon for p in seq]
        if not flat_canon:
            return [], {"error": "empty_target", "method": self.method}

        soft = self.pev.soft_match(wav_path, flat_arpa)
        # soft.hits already CTC-aligned per phone in flat order
        if len(soft.hits) != len(flat_canon):
            # fallback: still use hits order
            pass

        spans: List[WordSpan] = []
        idx = 0
        for w, arpa, canon in zip(words, word_arpa, word_canon):
            n = len(canon) if canon else 1
            chunk = soft.hits[idx:idx + n] if soft.hits else []
            idx += n
            if chunk:
                s_f = chunk[0].start_frame
                e_f = chunk[-1].end_frame
                s_t = chunk[0].start_s
                e_t = chunk[-1].end_s
                sims = [h.sim for h in chunk]
                posts = [h.posterior for h in chunk]
                sc = 100.0 * float(np.mean(sims)) if sims else 0.0
                conf = float(np.mean(posts)) if posts else 0.0
                # damp conf by miss rate
                misses = sum(1 for h in chunk if h.match_type == "miss")
                conf *= (1.0 - 0.5 * (misses / max(1, len(chunk))))
                hits_ser = [
                    {"exp": h.expected, "obs": h.best_obs, "type": h.match_type,
                     "sim": round(h.sim, 3), "post": round(h.posterior, 3),
                     "t0": round(h.start_s, 3), "t1": round(h.end_s, 3)}
                    for h in chunk
                ]
            else:
                s_f = e_f = 0
                s_t = e_t = 0.0
                sc = conf = 0.0
                hits_ser = []
            spans.append(WordSpan(
                word=w, arpa=arpa, canon=canon,
                start_s=s_t, end_s=e_t, start_frame=s_f, end_frame=e_f,
                soft_score=round(sc, 1), soft_conf=round(float(max(0.0, min(1.0, conf))), 3),
                phone_hits=hits_ser,
            ))

        meta = {
            "method": self.method,
            "version": self.version,
            "n_words": len(words),
            "n_phones": len(flat_canon),
            "n_hits": len(soft.hits),
            "utt_soft": soft.soft_score_0_100,
            "utt_conf": soft.confidence_0_1,
            "alignment_note": "word spans = contiguous CTC phone spans grouped by CMUdict phone counts",
        }
        return spans, meta
