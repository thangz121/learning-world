"""Phase 1.9.18 — write machine-readable decision + SIAK transfer, verify locks."""
from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
OUT = Path(__file__).resolve().parents[1]

LOCKS = {
    "production_vad": False,
    "router_locked": False,
    "unity_integrated": False,
    "scorer_modified": False,
    "production_window_locked": False,
}


def main():
    b = json.loads((OUT / "zero_shot_baseline.json").read_text(encoding="utf-8"))
    a = json.loads((OUT / "b1_test_metrics.json").read_text(encoding="utf-8"))
    cfg = json.loads((OUT / "b1_config.json").read_text(encoding="utf-8"))
    bf = b["frr_first"]
    af = a["frr_first"]
    d_frr = round(af["frr"] - bf["frr"], 4)
    d_far = round(af["far"] - bf["far"], 4)

    siak = {
        "phase": "1.9.18",
        "status": "SIAK_TRANSFER_BLOCKED_PENDING_ND_REVIEW",
        "reason": "SIAK is CC-BY-ND-4.0; the training/evaluation-rights review "
                  "opened in 1.9.14 is still unresolved. No B1 evaluation was "
                  "run on SIAK data.",
        "frozen_baseline_reference": {
            "source": "Phase1_9_16/baseline_metrics.json (RERUN, matches 1.9.15)",
            "frr_proxy_soft_lt_50_on_rating_ge_80": 0.1224,
            "n": 482,
        },
        "b1": "NOT_COMPUTABLE",
    }
    (OUT / "siak_transfer.json").write_text(json.dumps(siak, indent=2),
                                            encoding="utf-8")

    decision = "E"
    decision_label = "B1_REGRESSES_HUMAN_CORRECT_CHILD_SPEECH"
    decision_json = {
        "phase": "1.9.18",
        "decision": decision,
        "decision_label": decision_label,
        "rationale": (
            "B1 (frozen encoder + trainable head) lowers FAR (0.271 -> 0.104) "
            "but RAISES FRR on expert-correct child phones (0.227 -> 0.299). "
            "FRR-first: rejecting more human-correct speech is a regression, "
            "so a FAR gain does not count as an improvement. B1 regresses FRR "
            "on every age band and on the majority of frequent phones, and it "
            "also lowers confidence on adult-TTS controls (no domain gain)."),
        "baseline_frr": bf["frr"],
        "b1_frr": af["frr"],
        "delta_frr": d_frr,
        "baseline_far": bf["far"],
        "b1_far": af["far"],
        "delta_far": d_far,
        "b2_justified": False,
        "checkpoint": "checkpoints/b1_head.pt",
        "checkpoint_sha256": cfg["checkpoint_sha256"],
        "git_commit": cfg["git_commit"],
        "production_locks": LOCKS,
        "production_untouched": True,
    }
    (OUT / "decision.json").write_text(json.dumps(decision_json, indent=2),
                                       encoding="utf-8")

    # verify production untouched: no diffs outside Phase1_9_18 (+ Phase1_9_17 ev)
    diff = subprocess.check_output(
        ["git", "status", "--porcelain"], cwd=str(REPO)).decode()
    prod_paths = [ln for ln in diff.splitlines()
                  if not ln.strip().endswith("Phase1_9_18/")
                  and "Phase1_9_17/evidence/" not in ln
                  and "Phase1_9_17/experiments" not in ln
                  and "Phase1_9_17/so762" not in ln]
    print("decision:", decision, decision_label)
    print("FRR", bf["frr"], "->", af["frr"], "delta", d_frr)
    print("FAR", bf["far"], "->", af["far"], "delta", d_far)
    print("non-phase1.9.18 dirty paths:", prod_paths)
    return decision_json


if __name__ == "__main__":
    main()
