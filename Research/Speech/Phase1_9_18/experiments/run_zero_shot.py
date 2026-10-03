"""Phase 1.9.18 STEP 4 — frozen PhoneEvidenceV2@1.4.0 zero-shot on SO762 child test.

Streams the SO762 child test split (speaker-disjoint, 24 speakers), reads raw
16 kHz WAVE bytes (no decode lib), and scores every utterance with the FROZEN
call path. Emits per-phone rows + FRR-first metrics. This is the number B1 must
beat. No production code is touched; PhoneEvidenceV2 is imported read-only.
"""
from __future__ import annotations

import json
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from so762_common import (BAD_THRESHOLD, CHILD_AGE_MAX, MODEL_REV,  # noqa: E402
                          SEED, build_targets, iter_rows, make_child_split,
                          read_wav_bytes)
from so762_eval import frr_far, per_phone_rows, score_baseline_utterance  # noqa

REPO = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]


def main() -> dict:
    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import (
        PhoneEvidenceV2)
    pev = PhoneEvidenceV2()
    pev._ensure()  # load once, reuse

    # rebuild the same split deterministically (annotations only) to get speakers
    meta_rows = list(iter_rows(splits=("train", "test"), with_audio=False))
    _, assigned, _ = make_child_split(meta_rows)
    test_spk = assigned["test"]

    rows = []
    per_phone = []
    t0 = time.perf_counter()
    n = 0
    for r in iter_rows(splits=("train", "test"), with_audio=True):
        if r["age"] > CHILD_AGE_MAX or r["speaker"] not in test_spk:
            continue
        n += 1
        samples = read_wav_bytes(r["wav_bytes"])
        tgt = build_targets(r["text"], r["words"])
        meta = {"speaker": r["speaker"], "age": r["age"],
                "path": r["path"], "split": r["split"]}
        try:
            sm = score_baseline_utterance(pev, samples,
                                          tgt["canonical_arpa"])
            ppr = per_phone_rows(sm, tgt["canonical_arpa"], tgt["expert_acc"],
                                 tgt["error_type"], meta)
            per_phone.extend(ppr)
            rows.append({**meta, "n_phones": len(tgt["canonical_arpa"]),
                         "soft_full": sm.soft_score_0_100,
                         "confidence": sm.confidence_0_1,
                         "mean_sim": round(sm.mean_sim, 5),
                         "mean_posterior": round(sm.mean_posterior, 6),
                         "processing_s": round(sm.processing_s, 3),
                         "error": ""})
        except Exception as exc:  # noqa: BLE001
            rows.append({**meta, "error": f"{type(exc).__name__}:{exc}"[:160]})
        if n % 50 == 0:
            print(f"  {n} ({time.perf_counter()-t0:.0f}s)", flush=True)

    ok = [r for r in rows if not r.get("error")]
    metrics = frr_far(per_phone)
    out = {
        "phase": "1.9.18",
        "step": "zero_shot_frozen_baseline_so762_child_test",
        "model": f"facebook/wav2vec2-xlsr-53-espeak-cv-ft@{MODEL_REV}",
        "pipeline": "PhoneEvidenceV2@1.4.0 (unchanged)",
        "seed": SEED,
        "test_speakers": len(test_spk),
        "n_utterances": len(rows),
        "n_scored": len(ok),
        "n_errors": len(rows) - len(ok),
        "n_phone_tokens_scored": len(per_phone),
        "mean_soft_full": round(sum(r["soft_full"] for r in ok)
                                / max(1, len(ok)), 3),
        "mean_confidence": round(sum(r["confidence"] for r in ok)
                                 / max(1, len(ok)), 4),
        "mean_processing_s": round(sum(r["processing_s"] for r in ok)
                                   / max(1, len(ok)), 3),
        "frr_first": metrics,
        "wall_s": round(time.perf_counter() - t0, 1),
        "PER": "NOT_COMPUTABLE (SO762 gives expert scores + sparse observed "
               "errors, not dense observed-phone truth)",
    }
    (OUT / "zero_shot_baseline.json").write_text(
        json.dumps(out, indent=2), encoding="utf-8")
    with open(OUT / "evidence" / "zero_shot_per_phone.csv", "w",
              encoding="utf-8", newline="") as f:
        import csv
        w = csv.DictWriter(f, fieldnames=list(per_phone[0].keys()))
        w.writeheader()
        w.writerows(per_phone)
    with open(OUT / "evidence" / "zero_shot_utterances.csv", "w",
              encoding="utf-8", newline="") as f:
        import csv
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    print(json.dumps(out, indent=2), flush=True)
    return out


if __name__ == "__main__":
    main()
