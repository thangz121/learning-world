"""Phase 1.9.15 — unlabeled system-behavior audit of the new 115-item blind pack.

This is NOT independent validation. It reports what the frozen v1/v2 rules do on
the new selection (chosen by stratification signals, never by human labels), so
the system's behavior on unseen recordings is documented even if human labels are
unavailable. No label is invented; no accuracy is claimed.

Outputs:
  artifacts/fidelity/system_behavior_audit.csv
  artifacts/fidelity/system_behavior_audit.json
"""
from __future__ import annotations

import csv
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(REPO / "Research/Speech/Phase1_9_14/experiments"))
import merge_fidelity_review as M  # noqa: E402

OUT = REPO / "Research/Speech/Phase1_9_15"
HR = OUT / "HumanReview"
FID = OUT / "artifacts" / "fidelity"
REFUSAL = {"NO_SPEECH", "UNINTELLIGIBLE", "FREE_SPEAK", "INCOMPLETE"}


def main():
    meta = json.loads((HR / "review_metadata.json").read_text(encoding="utf-8"))
    rows = []
    for it in meta["items"]:
        v1, v2 = M.apply_rules(it)
        rows.append({
            "case_id": it["case_id"], "recording_id": it["recording_id"],
            "speaker_id": it["speaker_id"], "target": it["target"], "task": it["task"],
            "selected_stratum": it["selected_stratum"], "tags": ";".join(it.get("tags", [])),
            "soft_full": it.get("soft_full"), "confidence": it.get("confidence"),
            "asr_status": it.get("asr_status", ""), "silero_n": it.get("silero_n"),
            "speech_ratio": it.get("speech_ratio"), "rms_mean": it.get("rms_mean"),
            "clip_duration_s": it.get("clip_duration_s"),
            "system_state_v1": v1["state"], "assess_v1": v1["assessability"],
            "reasons_v1": ";".join(v1["reasons"]),
            "system_state_v2": v2["state"], "assess_v2": v2["assessability"],
            "reasons_v2": ";".join(v2["reasons"]),
        })
    with open(FID / "system_behavior_audit.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)

    def dist(version):
        return dict(Counter(r[f"system_state_{version}"] for r in rows))

    by_stratum = defaultdict(Counter)
    for r in rows:
        by_stratum[r["selected_stratum"]][r["system_state_v2"]] += 1
    low = [r for r in rows if r["assess_v2"] == "LOW"]
    refusals_v2 = [r for r in rows if r["system_state_v2"] in REFUSAL]
    vad_neg_energy = [r for r in rows if r["silero_n"] == 0]
    asr_empty_scored = [r for r in rows if r["asr_status"] == "ASR_EMPTY"
                        and r["assess_v2"] in ("MEDIUM", "HIGH")]
    free_speech_scored = [r for r in rows if r["task"].startswith("spontaneous")
                          and r["assess_v2"] in ("MEDIUM", "HIGH")]
    summary = {
        "phase": "1.9.15",
        "artifact": "unlabeled system-behavior audit (NOT independent validation)",
        "n_items": len(rows),
        "v1_state_distribution": dist("v1"),
        "v2_state_distribution": dist("v2"),
        "v2_refusal_n": len(refusals_v2),
        "v2_low_assessability_n": len(low),
        "by_stratum_v2": {k: dict(v) for k, v in sorted(by_stratum.items())},
        "flags": {
            "vad_negative_items_n": len(vad_neg_energy),
            "vad_negative_states": {r["case_id"]: r["system_state_v2"] for r in vad_neg_energy},
            "asr_empty_but_v2_medium_or_high_n": len(asr_empty_scored),
            "asr_empty_but_v2_medium_or_high_ids": [r["case_id"] for r in asr_empty_scored][:20],
            "free_speech_but_v2_medium_or_high_n": len(free_speech_scored),
            "free_speech_but_v2_medium_or_high_ids": [r["case_id"] for r in free_speech_scored][:20],
        },
        "interpretation": (
            "Behavior only. Whether these states are correct cannot be established without "
            "human labels; this file exists so the system's behavior on the new blind "
            "selection is documented and reviewable."),
    }
    (FID / "system_behavior_audit.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps({k: summary[k] for k in
                      ("n_items", "v1_state_distribution", "v2_state_distribution",
                       "v2_refusal_n", "v2_low_assessability_n", "flags")}, indent=2))


if __name__ == "__main__":
    main()
