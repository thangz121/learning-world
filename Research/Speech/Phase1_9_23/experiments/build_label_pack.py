"""WP-1.9.23 Part 7 — reviewer-ready label pack (research-only).

Builds HUMAN_LABEL_REVIEW_PACK.csv from existing local recordings only.
No audio is copied into the repo, no labels are fabricated. The pack is
READY_FOR_HUMAN_REVIEW; NEW_LABELS_COLLECTED = 0 (no reviewer in-session).
"""
from __future__ import annotations

import csv
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_23"
ART = OUT / "artifacts"
SO = L.SO

FIELDS = [
    "priority", "candidate_id", "speaker_id", "word", "target_phone", "source_corpus",
    "audio_reference", "reference_note", "machine_rank", "machine_max_A",
    "machine_blank_mean", "machine_margin", "production_decision",
    "best_rule_decision", "why_needed", "review_status",
]


def num(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def main():
    rows = list(csv.DictReader(open(ART / "feature_matrix.csv", encoding="utf-8")))
    for r in rows:
        r["maxA"] = num(r["target_max_A"])
        r["blank"] = num(r["blank_mean_span"])
        r["margin"] = num(r["identity_margin_mean"])
        r["rank"] = int(num(r["target_rank_in_top5"], -1))
        r["truth_i"] = int(r["truth"]) if r["truth"] != "" else None
        r["prod"] = int(num(r["production_decision"]))
    import json
    res = json.loads((OUT / "EXPERIMENT_RESULTS.json").read_text(encoding="utf-8"))
    best_id = res["best_rule"]["rule_id"]
    f_rule = [k for k in res["best_rule"]["params"]]  # best params (D2 -> {})
    # best rule = D2: identity_credit and blank_ge09_frac <= 1.0
    def best_dec(r):
        return int(int(num(r["identity_credit"])) == 1 and num(r["blank_ge09_frac"]) <= 1.0)

    pack = []

    def add(priority, r, why, ref_note=""):
        tid = r["token_id"]
        if r["corpus"] == "lwe":
            src = L.find_source(r["speaker_id"], r["word"])
            audio = str(src) if src else ""
            note = ref_note or f"word-level human verdict: {r.get('human_verdict', '') or '-'}"
        else:
            utt = tid.rsplit("_", 1)[0]
            spk = int(r["speaker_id"])
            audio = str(SO / "WAVE" / f"SPEAKER{spk:04d}" / f"{utt}.WAV")
            note = ref_note or "so762 per-phone human score (NOT a final-consonant label)"
        pack.append({
            "priority": priority, "candidate_id": tid, "speaker_id": r["speaker_id"],
            "word": r["word"], "target_phone": r["target_phone"],
            "source_corpus": r["corpus"], "audio_reference": audio,
            "reference_note": note, "machine_rank": r["rank"],
            "machine_max_A": round(r["maxA"], 6), "machine_blank_mean": round(r["blank"], 6),
            "machine_margin": round(r["margin"], 6), "production_decision": r["prod"],
            "best_rule_decision": best_dec(r), "why_needed": why,
            "review_status": "READY_FOR_HUMAN_REVIEW",
        })

    unlabeled = [r for r in rows if r["truth_i"] is None and r["is_final_consonant"] == "1"]
    labeled = [r for r in rows if r["truth_i"] is not None]
    print("unlabeled finals:", len(unlabeled),
          "| identity:", sum(1 for x in unlabeled if int(num(x["identity_credit"])) == 1),
          "| rank>0:", sum(1 for x in unlabeled if int(num(x["identity_credit"])) == 1
                           and x["rank"] > 0))

    # P1: /r/ PRESENT (LWE four tokens without labels)
    for r in sorted([x for x in unlabeled if x["target_phone"] == "ɹ"],
                    key=lambda x: -x["maxA"])[:6]:
        add(1, r, "confident PRESENT /r/ needed (/r/ present n=1 LOW)")
    # P2: weak-support PRESENT candidates (LWE, word verdict positive, low support)
    weak_ok = [x for x in unlabeled if x["maxA"] < 0.15
               and x.get("human_verdict", "") in ("CLEAR_CORRECT", "PROBABLY_CORRECT",
                                                  "HUMAN_CONFIRMED_CORRECT")]
    for r in sorted(weak_ok, key=lambda x: x["maxA"])[:8]:
        add(2, r, "weak-support PRESENT: separates true weak finals from weak false accepts")
    # P3: rank-2..5 identity accepts (LWE unlabeled first, then so762 with phone scores)
    rank25_lwe = [x for x in unlabeled if int(num(x["identity_credit"])) == 1 and x["rank"] > 0]
    rank25_so = [x for x in labeled if x["corpus"] != "lwe"
                 and int(num(x["identity_credit"])) == 1 and x["rank"] > 0
                 and x["truth_i"] == 1]
    for r in sorted(rank25_lwe, key=lambda x: x["rank"])[:3]:
        add(3, r, "rank-2..5 identity accept: is the similarity pick right?")
    for r in sorted(rank25_so, key=lambda x: x["rank"])[:5]:
        add(3, r, "rank-2..5 identity accept (so762): is the similarity pick right?")
    # P4: strong false-accept candidates (so762 absent with strong support)
    strong_fa = [x for x in labeled if x["truth_i"] == 0 and x["maxA"] >= 0.30]
    for r in sorted(strong_fa, key=lambda x: -x["maxA"])[:5]:
        add(4, r, "strong encoder false-accept candidate (TYPE B): confirm human absence")
    # P5: additional /r/ ABSENT
    r_abs = [x for x in labeled if x["target_phone"] == "ɹ" and x["truth_i"] == 0]
    for r in sorted(r_abs, key=lambda x: x["maxA"])[:4]:
        add(5, r, "additional /r/ ABSENT evidence (/r/ absent n=5)")
    # P6: strong /r/ PRESENT from dev/test (confident positive /r/ examples)
    r_pos = [x for x in labeled if x["target_phone"] == "ɹ" and x["truth_i"] == 1]
    for r in sorted(r_pos, key=lambda x: -x["maxA"])[:4]:
        add(6, r, "confident PRESENT /r/ reference example (dev/test strong support)")

    L.write_rows(OUT / "HUMAN_LABEL_REVIEW_PACK.csv", pack, FIELDS)
    print("pack rows:", len(pack))
    from collections import Counter
    print("by priority:", dict(Counter(p["priority"] for p in pack)))
    print("by corpus:", dict(Counter(p["source_corpus"] for p in pack)))
    print("NEW_LABELS_COLLECTED = 0")


if __name__ == "__main__":
    main()
