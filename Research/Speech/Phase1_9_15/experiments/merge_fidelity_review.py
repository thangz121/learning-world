"""Phase 1.9.15 — P1 independent validation of the v1/v2 fidelity rules.

Reads the new blind human review submission (Results/fidelity_v2_StageA_Filled.csv)
and the selection signals (HumanReview/review_metadata.json), applies the frozen
1.9.14 rules to the same signals, and reports the separation metrics required by
the spec:

  - false-gate rate on human-valid attempts
  - false-accept rate on human-not-assessable cases
  - confusion matrices, state precision/recall, uncertain rates
  - reviewer agreement if two submissions exist

Outputs:
  artifacts/fidelity/fidelity_independent.csv
  artifacts/fidelity/independent_metrics.json
"""
from __future__ import annotations

import csv
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO))
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(REPO / "Research/Speech/Phase1_9_14/experiments"))
import p1_fidelity_assessability as P1  # noqa: E402

OUT = REPO / "Research/Speech/Phase1_9_15"
RES = OUT / "Results"
HR = OUT / "HumanReview"
FID = OUT / "artifacts" / "fidelity"

REFUSAL_STATES = {"NO_SPEECH", "UNINTELLIGIBLE", "FREE_SPEAK", "INCOMPLETE"}
VALID_ATTEMPTS = {"VALID_ATTEMPT", "POSSIBLE_ATTEMPT"}


def read_csv(p: Path):
    return list(csv.DictReader(p.open(encoding="utf-8-sig")))


def find_submissions():
    out = []
    main = OUT / "Results" / "fidelity_v2_StageA_Filled.csv"
    if main.exists():
        out.append(main)
    subs = OUT / "Results" / "submissions"
    if subs.exists():
        for p in sorted(subs.glob("fidelity_v2_StageA_*.csv")):
            if p != main:
                out.append(p)
    return out


def label_maps(meta):
    return {it["case_id"]: it for it in meta["items"]}


def num(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return None


def apply_rules(item):
    row = {
        "duration_s": num(item.get("clip_duration_s")),
        "silero_n": num(item.get("silero_n")),
        "speech_ratio": num(item.get("speech_ratio")),
        "rms_mean": num(item.get("rms_mean")),
        "zcr_proxy": num(item.get("zcr_proxy")),
        "full_confidence": num(item.get("confidence")),
        "soft_full": num(item.get("soft_full")),
        "asr_status": item.get("asr_status", ""),
    }
    v1 = P1.fidelity_v1(row)
    v2 = P1.fidelity_v2(row)
    return v1, v2


def main():
    FID.mkdir(parents=True, exist_ok=True)
    meta = json.loads((HR / "review_metadata.json").read_text(encoding="utf-8"))
    items = label_maps(meta)
    subs = find_submissions()
    if not subs:
        print("NO SUBMISSION YET — run the review first. Expected:",
              OUT / "Results" / "fidelity_v2_StageA_Filled.csv")
        return
    # dedupe identical submissions (main file + timestamped history are the same)
    unique = {}
    for p in subs:
        import hashlib
        h = hashlib.sha256(p.read_bytes()).hexdigest()
        unique.setdefault(h, (p, read_csv(p)))
    sources = {p.name: rows for p, rows in unique.values()}
    primary = list(unique.values())[0][1]
    print(f"distinct submissions: {list(sources)} | primary rows: {len(primary)}")

    # agreement only across DISTINCT reviewer submissions
    agreement = None
    if len(sources) > 1:
        keys = [("content", "c"), ("attempt", "a"), ("assessability", "s"), ("confidence", "f")]
        a, b = list(sources.values())[0], list(sources.values())[1]
        amap = {r["case_id"]: r for r in a}
        bmap = {r["case_id"]: r for r in b}
        agree = {k: {"n": 0, "same": 0} for k, _ in keys}
        for cid in set(amap) & set(bmap):
            for k, _ in keys:
                va, vb = amap[cid].get(k, ""), bmap[cid].get(k, "")
                if va and vb:
                    agree[k]["n"] += 1
                    agree[k]["same"] += int(va == vb)
        agreement = {k: (v["same"] / v["n"] if v["n"] else None) for k, v in agree.items()}

    rows = []
    for r in primary:
        cid = r["case_id"]
        item = items.get(cid)
        if not item:
            continue
        v1, v2 = apply_rules(item)
        rows.append({
            "case_id": cid, "recording_id": item["recording_id"], "speaker_id": item["speaker_id"],
            "target": item["target"], "task": item["task"], "selected_stratum": item["selected_stratum"],
            "human_content": r.get("content", ""), "human_attempt": r.get("attempt", ""),
            "human_assessability": r.get("assessability", ""), "human_confidence": r.get("confidence", ""),
            "system_state_v1": v1["state"], "system_assess_v1": v1["assessability"],
            "system_state_v2": v2["state"], "system_assess_v2": v2["assessability"],
            "reasons_v2": ";".join(v2["reasons"]),
            "soft_full": item.get("soft_full"), "confidence": item.get("confidence"),
            "asr_status": item.get("asr_status", ""), "silero_n": item.get("silero_n"),
            "speech_ratio": item.get("speech_ratio"), "rms_mean": item.get("rms_mean"),
            "clip_duration_s": item.get("clip_duration_s"),
        })
    with open(FID / "fidelity_independent.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)

    # ---- metrics ----
    def is_valid_attempt(r, strict=False):
        if r["human_content"] != "SPEECH_PRESENT":
            return False
        if strict:
            return r["human_attempt"] == "VALID_ATTEMPT"
        return r["human_attempt"] in VALID_ATTEMPTS or r["human_attempt"] in ("INCOMPLETE",)

    metrics = {"n_rows": len(rows), "n_distinct_submissions": len(sources), "agreement": agreement,
               "reviewer_note": "single distinct reviewer submission; no inter-rater agreement "
                                "computable (spec: document explicitly)"}
    for version in ("v1", "v2"):
        st = f"system_state_{version}"
        valid = [r for r in rows if is_valid_attempt(r)]
        valid_strict = [r for r in rows if is_valid_attempt(r, strict=True)]
        false_gate = [r for r in valid if r[st] in REFUSAL_STATES]
        false_gate_strict = [r for r in valid_strict if r[st] in REFUSAL_STATES]
        not_assess = [r for r in rows if r["human_assessability"] == "NOT_ASSESSABLE"]
        dangerous = [r for r in not_assess if r[st] in {"POSSIBLE_ATTEMPT", "VALID_ATTEMPT", "ASSESSABLE"}]
        dangerous_strict = [r for r in not_assess if r[st] in {"VALID_ATTEMPT", "ASSESSABLE"}]
        matrix = defaultdict(Counter)
        for r in rows:
            matrix[r["human_assessability"] or "(blank)"][r[st]] += 1
        content_matrix = defaultdict(Counter)
        for r in rows:
            content_matrix[r["human_content"] or "(blank)"][r[st]] += 1
        state_pr = {}
        for state in set(r[st] for r in rows):
            predicted = [r for r in rows if r[st] == state]
            # "correct human state" mapping for the state
            human_match = {
                "NO_SPEECH": lambda x: x["human_content"] == "NO_SPEECH",
                "UNINTELLIGIBLE": lambda x: x["human_attempt"] == "UNINTELLIGIBLE",
                "POSSIBLE_ATTEMPT": lambda x: x["human_attempt"] == "POSSIBLE_ATTEMPT",
                "VALID_ATTEMPT": lambda x: x["human_attempt"] == "VALID_ATTEMPT",
                "ASSESSABLE": lambda x: x["human_assessability"] == "ASSESSABLE",
            }.get(state, lambda x: True)
            tp = sum(1 for r in predicted if human_match(r))
            precision = tp / len(predicted) if predicted else None
            all_human = [r for r in rows if human_match(r) and r["human_content"] != "UNCERTAIN"]
            recall = tp / len(all_human) if all_human else None
            state_pr[state] = {"n_system": len(predicted), "tp": tp,
                               "precision": precision, "recall": recall}
        metrics[version] = {
            "valid_attempt_n": len(valid),
            "valid_attempt_strict_n": len(valid_strict),
            "false_gate_n": len(false_gate),
            "false_gate_rate": len(false_gate) / len(valid) if valid else None,
            "false_gate_rate_strict": len(false_gate_strict) / len(valid_strict) if valid_strict else None,
            "false_gate_ids": [r["case_id"] for r in false_gate],
            "human_not_assessable_n": len(not_assess),
            "dangerous_accept_loose_n": len(dangerous),
            "dangerous_accept_loose_rate": len(dangerous) / len(not_assess) if not_assess else None,
            "dangerous_accept_loose_ids": [r["case_id"] for r in dangerous],
            "dangerous_accept_strict_n": len(dangerous_strict),
            "dangerous_accept_strict_rate": len(dangerous_strict) / len(not_assess) if not_assess else None,
            "dangerous_accept_strict_ids": [r["case_id"] for r in dangerous_strict],
            "assessability_matrix": {k: dict(v) for k, v in matrix.items()},
            "content_matrix": {k: dict(v) for k, v in content_matrix.items()},
            "state_precision_recall": state_pr,
        }
    metrics["reviewer_uncertain_rate"] = {
        "content": sum(1 for r in rows if r["human_content"] == "UNCERTAIN") / len(rows),
        "attempt": sum(1 for r in rows if r["human_attempt"] == "UNCERTAIN") / len(rows),
        "assessability": sum(1 for r in rows if r["human_assessability"] == "UNCERTAIN") / len(rows),
    }
    metrics["human_content_counts"] = dict(Counter(r["human_content"] for r in rows))
    metrics["human_attempt_counts"] = dict(Counter(r["human_attempt"] for r in rows))
    metrics["human_assessability_counts"] = dict(Counter(r["human_assessability"] for r in rows))
    metrics["flags"] = {"production_vad": False, "router_locked": False,
                        "unity_integrated": False, "scorer_modified": False,
                        "production_window_locked": False}
    (FID / "independent_metrics.json").write_text(json.dumps(metrics, indent=2), encoding="utf-8")
    print(json.dumps({v: {k: metrics[v][k] for k in
                          ("valid_attempt_n", "false_gate_n", "false_gate_rate",
                           "false_gate_rate_strict", "human_not_assessable_n",
                           "dangerous_accept_loose_rate", "dangerous_accept_strict_rate")}
                      for v in ("v1", "v2")}, indent=2))


if __name__ == "__main__":
    main()
