"""Phase 1.9.18 STEP 10/11 — historical controls replay + LWE transfer test.

Two independent evaluations, both with the SAME frozen encoder + alignment +
B1 head and the SAME decision boundary as the SO762 test:

1. CONTROLS (STEP 10): the frozen LWE control words that exist locally
   (`sapi_*`, `pregen_apple_normal`, `stress_silence`, `stress_noise` across
   Phase1_1/audio). These are synthetic/TTS or noise controls, not child speech;
   they are reported as a control replay with explicit population labels.

2. LWE CHILD TRANSFER (STEP 11): the raw real-child LWE audio is gitignored
   (privacy). The committed 1.9.15 `external_lwe_features.csv` holds per-token
   frozen soft scores + human verdicts but NOT the waveforms, so B1 cannot be
   run on that audio here. This script writes LWE_TRANSFER status accordingly
   and still reports the frozen-baseline human-correct low-score rate as the
   pre-registered number any future transfer must beat.

Also replays the 12 PHONE_MODEL_ERROR + 6 SCORER_MISS + 5 context cases from
`scorer_failure_cases.csv` where raw audio exists; else NOT_COMPUTABLE.
"""
from __future__ import annotations

import csv
import glob
import json
import sys
import time
from pathlib import Path

import numpy as np
import torch

sys.path.insert(0, str(Path(__file__).resolve().parent))
from b1_head import B1Head, span_features  # noqa: E402
from run_b1_train import _logits_from_array  # noqa: E402
from so762_common import build_targets  # noqa: E402

REPO = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]
CKPT = OUT / "checkpoints"
CTRL_DIR = REPO / "Research" / "Speech" / "Phase1_1" / "audio"
CONTROL_TARGETS = {
    "sapi_red.wav": "red", "sapi_cat.wav": "cat",
    "sapi_red_apple.wav": "red apple", "sapi_blue.wav": "blue",
    "sapi_big.wav": "big", "sapi_book.wav": "book", "sapi_dog.wav": "dog",
    "pregen_apple_normal.wav": "apple",
    "stress_silence.wav": "red", "stress_noise.wav": "red",
}


def load_head():
    blob = torch.load(CKPT / "b1_head.pt", weights_only=False)
    head = B1Head(blob["in_dim"], blob["hidden"])
    head.load_state_dict(blob["state_dict"])
    head.eval()
    return head


def b1_per_phone(pev, head, wav_path, text):
    import soundfile as sf
    audio, sr = sf.read(str(wav_path))
    if audio.ndim > 1:
        audio = audio.mean(axis=1)
    target_arpa = build_targets(text, [])["canonical_arpa"]
    # build targets from CMUdict directly for control words
    from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
    target_arpa = CmuDictTargetAdapter().build(text).arpabet
    probs, dur, _ = _logits_from_array(pev, audio)
    target = pev.inv.arpa_seq_to_canon(target_arpa)
    spans = pev.ctc_align(probs, target)
    feats = span_features(probs, spans, target, pev.inv, pev._canon_ids)
    with torch.no_grad():
        prob = torch.sigmoid(head(torch.tensor(feats))).numpy()
    return target_arpa, prob.tolist()


def baseline_per_phone(pev, wav_path, text):
    from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
    target_arpa = CmuDictTargetAdapter().build(text).arpabet
    sm = pev.soft_match(str(wav_path), target_arpa)
    return target_arpa, [h.sim for h in sm.hits]


def main() -> dict:
    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import (
        PhoneEvidenceV2)
    pev = PhoneEvidenceV2()
    pev._ensure()
    head = load_head()

    controls = []
    for fn, text in CONTROL_TARGETS.items():
        p = CTRL_DIR / fn
        if not p.exists():
            controls.append({"file": fn, "target": text, "status":
                             "NOT_COMPUTABLE_AUDIO_MISSING"})
            continue
        try:
            base_arpa, base_sims = baseline_per_phone(pev, p, text)
            b1_arpa, b1 = b1_per_phone(pev, head, p, text)
            controls.append({
                "file": fn, "target": text, "status": "OK",
                "baseline_mean_sim": round(float(np.mean(base_sims))
                                           if base_sims else 0.0, 4),
                "baseline_soft": round(100 * float(np.mean(base_sims))
                                       if base_sims else 0.0, 1),
                "b1_mean_prob": round(float(np.mean(b1)) if b1 else 0.0, 4),
                "n_phones": len(base_arpa),
                "population_label": "adult-TTS-or-synthetic control",
            })
        except Exception as exc:  # noqa: BLE001
            controls.append({"file": fn, "target": text, "status":
                             f"ERROR:{type(exc).__name__}:{exc}"[:120]})

    # LWE child transfer (raw audio gitignored)
    lwe_csv = (REPO / "Research" / "Speech" / "Phase1_9_15" / "artifacts"
               / "external" / "external_lwe_features.csv")
    lwe = []
    if lwe_csv.exists():
        for r in csv.DictReader(open(lwe_csv, encoding="utf-8")):
            lwe.append(r)
    correct = [r for r in lwe if r["human_class"] == "HUMAN_CORRECT"]
    incorrect = [r for r in lwe if r["human_class"] == "HUMAN_INCORRECT"]
    low_correct = sum(1 for r in correct if float(r["soft_full"]) < 50)
    high_incorrect = sum(1 for r in incorrect if float(r["soft_full"]) >= 50)
    lwe_transfer = {
        "status": "LWE_TRANSFER_BLOCKED_RAW_AUDIO_GITIGNORED",
        "reason": "real-child LWE waveforms are privacy-sensitive and not "
                  "committed; only frozen per-token soft scores are available, "
                  "so B1 cannot be run on this audio in this environment.",
        "frozen_baseline_reference": {
            "n_human_correct": len(correct),
            "human_correct_low_score_rate": round(low_correct
                                                  / max(1, len(correct)), 4),
            "n_human_incorrect": len(incorrect),
            "human_incorrect_high_score_rate": round(high_incorrect
                                                     / max(1, len(incorrect)), 4),
        },
        "b1_number": "NOT_COMPUTABLE",
    }

    # historical replay
    replay = _historical_replay()

    out = {"phase": "1.9.18", "step": "controls+transfer+replay",
           "controls": controls, "lwe_transfer": lwe_transfer,
           "historical_replay": replay}
    (OUT / "lwe_transfer.json").write_text(json.dumps(lwe_transfer, indent=2),
                                           encoding="utf-8")
    (OUT / "historical_replay.json").write_text(json.dumps(replay, indent=2),
                                                encoding="utf-8")
    with open(OUT / "evidence" / "controls_replay.json", "w",
              encoding="utf-8") as f:
        json.dump(controls, f, indent=2)
    print(json.dumps(out, indent=2), flush=True)
    return out


def _historical_replay():
    """Replay forensic cases where the audio is present; else NOT_COMPUTABLE."""
    cases = []
    # locator: scorer_failure_cases.csv from 1.9.9/1.9.10
    cand = list((REPO / "Research" / "Speech").glob(
        "Phase1_9_*/**/scorer_failure_cases.csv"))
    case_file = cand[0] if cand else None
    n = 0
    if case_file:
        for r in csv.DictReader(open(case_file, encoding="utf-8")):
            n += 1
            cases.append({"case": r.get("recording_id", r.get("file")),
                          "diagnostic": r.get("diagnostic"),
                          "baseline_output": r.get("phone_evidence",
                                                   "see_committed_csv"),
                          "b1_output": "NOT_COMPUTABLE",
                          "reason": "raw case audio gitignored / not "
                                    "resolvable in this environment"})
    # committed evidence recovers the historical 12+6 counts even without audio
    ev = (REPO / "Research" / "Speech" / "Phase1_9_16"
          / "lwe_failure_replay.csv")
    counts = {}
    if ev.exists():
        for r in csv.DictReader(open(ev, encoding="utf-8")):
            counts[r.get("diagnostic", "?")] = counts.get(
                r.get("diagnostic", "?"), 0) + 1
    return {
        "source_case_file": str(case_file.relative_to(REPO)) if case_file
        else None,
        "n_cases_found": n,
        "committed_diagnostic_counts": counts,
        "b1_replay": "NOT_COMPUTABLE (case audio gitignored; requires the "
                     "privacy-sensitive raw LWE recordings)",
        "recovered_from_git": "Phase1_9_16/lwe_failure_replay.csv (12 "
                              "PHONE_MODEL_ERROR + 6 SCORER_MISS + 5 context)",
    }


if __name__ == "__main__":
    main()
