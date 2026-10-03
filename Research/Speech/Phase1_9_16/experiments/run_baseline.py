"""Phase 1.9.16 zero-shot frozen baseline reproduction (NO TRAINING).

Scores the 482 SIAK test utterances (speaker-disjoint, seed-1515 partition)
with the FROZEN baseline (wav2vec2-xlsr-53-espeak-cv-ft @ 2c73378 +
PhoneEvidenceV2@1.4.0 + CmuDictTargetAdapter), using the exact call path of
1.9.15 siak_expand.py. Compares against archived calibration_test.csv values.

Outputs (Research/Speech/Phase1_9_16/):
  baseline_reproduction.csv   per-utterance fresh scores
  baseline_metrics.json       agreement + FRR-proxy + confidence stats
PER / confusion matrix are NOT computable: SIAK has ratings, not phone labels
(recorded explicitly in the metrics file).
"""
from __future__ import annotations

import csv
import json
import sys
import time
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_2.Adapters.target_cmudict import (  # noqa: E402
    CmuDictTargetAdapter)
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import (  # noqa: E402
    PhoneEvidenceV2)

PHASE15 = REPO / "Research" / "Speech" / "Phase1_9_15" / "artifacts" / "siak"
OUT = REPO / "Research" / "Speech" / "Phase1_9_16"


def main(limit: int = 0) -> dict:
    import soundfile as sf
    with open(PHASE15 / "calibration_test.csv", newline="",
              encoding="utf-8") as f:
        arch = {r["file"]: r for r in csv.DictReader(f)}
    idx = {}
    for p in (REPO / "Research" / "Speech" / "ExternalData"
              / "SIAK" / "flac").rglob("*.flac"):
        idx[p.name] = p

    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()
    rows = list(arch.values())
    if limit:
        rows = rows[:limit]

    fresh = []
    t0 = time.perf_counter()
    for i, r in enumerate(rows):
        path = idx.get(r["file"])
        row = {"file": r["file"], "utterance": r["utterance"],
               "speaker_id": r["speaker_id"], "siak_score": r["siak_score"]}
        try:
            dur = sf.info(str(path)).duration if path else 0.0
            sm = pev.soft_match(str(path), tgt.build(r["utterance"]).arpabet)
            mt = Counter(h.match_type for h in sm.hits)
            row.update({
                "duration_s": round(dur, 3),
                "soft_full": sm.soft_score_0_100,
                "confidence_0_1": sm.confidence_0_1,
                "n_hits": len(sm.hits), "n_exact": mt.get("exact", 0),
                "n_soft": mt.get("soft", 0), "n_miss": mt.get("miss", 0),
                "mean_sim": round(sm.mean_sim, 4),
                "mean_posterior": round(sm.mean_posterior, 6),
                "processing_s": round(sm.processing_s, 2), "error": "",
            })
        except Exception as exc:  # noqa: BLE001
            row.update({"error": type(exc).__name__ + ":" + str(exc)[:120]})
        fresh.append(row)
        if (i + 1) % 50 == 0:
            print(f"  {i+1}/{len(rows)} "
                  f"({time.perf_counter()-t0:.0f}s)", flush=True)

    with open(OUT / "baseline_reproduction.csv", "w", newline="",
              encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(fresh[0].keys()))
        w.writeheader()
        w.writerows(fresh)

    ok = [r for r in fresh if not r.get("error")]
    agree = sum(1 for r in ok
                if abs(float(r["soft_full"])
                       - float(arch[r["file"]]["soft_full"])) < 1e-6)
    frr_n = sum(1 for r in ok if int(r["siak_score"]) >= 80)
    frr = sum(1 for r in ok if int(r["siak_score"]) >= 80
              and float(r["soft_full"]) < 50) / max(1, frr_n)
    metrics = {
        "phase": "1.9.16",
        "kind": "zero-shot frozen baseline reproduction (no training)",
        "model": "facebook/wav2vec2-xlsr-53-espeak-cv-ft@2c73378",
        "pipeline": "PhoneEvidenceV2@1.4.0",
        "n_scored": len(fresh), "n_errors": len(fresh) - len(ok),
        "soft_exact_agreement_with_1_9_15": f"{agree}/{len(ok)}",
        "reproduced": agree == len(ok) and len(ok) == 482,
        "frr_proxy_soft_lt_50_on_rating_ge_80": round(frr, 4),
        "frr_denominator": frr_n,
        "mean_confidence": round(sum(float(r["confidence_0_1"])
                                     for r in ok) / max(1, len(ok)), 4),
        "mean_processing_s": round(sum(float(r["processing_s"])
                                       for r in ok) / max(1, len(ok)), 3),
        "PER": "NOT_COMPUTABLE (SIAK has expert ratings, not phone labels)",
        "confusion_matrix": "NOT_COMPUTABLE (same reason)",
        "wall_s": round(time.perf_counter() - t0, 1),
    }
    with open(OUT / "baseline_metrics.json", "w", encoding="utf-8") as f:
        json.dump(metrics, f, indent=2)
    print(json.dumps(metrics, indent=2), flush=True)
    return metrics


if __name__ == "__main__":
    lim = int(sys.argv[1]) if len(sys.argv) > 1 else 0
    main(lim)
