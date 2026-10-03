"""Phase 1.9.18 STEP 6/7 — evaluate B1 on the SO762 child test.

Re-scores the SAME child test utterances as the zero-shot baseline, using the
SAME frozen encoder + SAME CTC alignment, and replaces ONLY the per-phone
decision with the B1 head output (correctness probability). FRR/FAR use the
identical 0.5 boundary. Produces b1_results.json test section + phone_results.
"""
from __future__ import annotations

import csv
import json
import sys
import time
from pathlib import Path

import numpy as np
import torch

sys.path.insert(0, str(Path(__file__).resolve().parent))
from b1_head import B1Head, span_features  # noqa: E402
from run_b1_train import _logits_from_array  # noqa: E402
from so762_common import (CHILD_AGE_MAX, SEED, build_targets, iter_rows,  # noqa
                          make_child_split, read_wav_bytes)
from so762_eval import frr_far  # noqa: E402

OUT = Path(__file__).resolve().parents[1]
CKPT = OUT / "checkpoints"


def main() -> dict:
    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import (
        PhoneEvidenceV2)
    pev = PhoneEvidenceV2()
    pev._ensure()

    blob = torch.load(CKPT / "b1_head.pt", weights_only=False)
    head = B1Head(blob["in_dim"], blob["hidden"])
    head.load_state_dict(blob["state_dict"])
    head.eval()

    meta_rows = list(iter_rows(splits=("train", "test"), with_audio=False))
    _, assigned, _ = make_child_split(meta_rows)
    test_spk = assigned["test"]

    per_phone = []
    utt = []
    t0 = time.perf_counter()
    n = 0
    for r in iter_rows(splits=("train", "test"), with_audio=True):
        if r["age"] > CHILD_AGE_MAX or r["speaker"] not in test_spk:
            continue
        n += 1
        samples = read_wav_bytes(r["wav_bytes"])
        tgt = build_targets(r["text"], r["words"])
        probs, dur, _ = _logits_from_array(pev, samples)
        target = pev.inv.arpa_seq_to_canon(tgt["canonical_arpa"])
        spans = pev.ctc_align(probs, target)
        feats = span_features(probs, spans, target, pev.inv, pev._canon_ids)
        with torch.no_grad():
            prob = torch.sigmoid(head(torch.tensor(feats))).numpy()
        sims = []
        for j, (ph, acc, et) in enumerate(zip(tgt["canonical_arpa"],
                                              tgt["expert_acc"],
                                              tgt["error_type"])):
            p = float(prob[j])
            # decision score on the SAME 0-1 scale used by FRR/FAR (>=0.5 accept)
            sims.append(p)
            per_phone.append({
                "speaker": r["speaker"], "age": r["age"], "path": r["path"],
                "phone_index": j, "canonical_phone": ph, "expert_acc": acc,
                "error_type": et, "sim": p, "posterior": p,
                "match_type": "b1_accept" if p >= 0.5 else "b1_reject",
                "best_obs": "", "source": "b1_head",
            })
        utt.append({
            "speaker": r["speaker"], "age": r["age"], "path": r["path"],
            "n_phones": len(target),
            "mean_prob": round(float(np.mean(sims)) if sims else 0.0, 5),
            "error": "",
        })
        if n % 50 == 0:
            print(f"  {n} ({time.perf_counter()-t0:.0f}s)", flush=True)

    metrics = frr_far(per_phone)
    out = {
        "phase": "1.9.18",
        "step": "b1_head_only_eval_so762_child_test",
        "model": "B1 head-only (frozen encoder) + frozen CTC alignment",
        "seed": SEED,
        "test_speakers": len(test_spk),
        "n_utterances": len(utt),
        "n_phone_tokens": len(per_phone),
        "frr_first": metrics,
        "wall_s": round(time.perf_counter() - t0, 1),
    }
    (OUT / "b1_test_metrics.json").write_text(json.dumps(out, indent=2),
                                              encoding="utf-8")
    with open(OUT / "evidence" / "b1_per_phone.csv", "w", encoding="utf-8",
              newline="") as f:
        w = csv.DictWriter(f, fieldnames=list(per_phone[0].keys()))
        w.writeheader()
        w.writerows(per_phone)
    print(json.dumps(out, indent=2), flush=True)
    return out


if __name__ == "__main__":
    main()
