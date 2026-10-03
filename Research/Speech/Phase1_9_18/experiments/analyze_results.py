"""Phase 1.9.18 STEP 7-14 — analysis over baseline + B1 per-phone rows.

Reads evidence/zero_shot_per_phone.csv and evidence/b1_per_phone.csv (same test
utterances, same target positions) and produces:
  phone_results.json       per-phone N / baseline / B1 / delta + INSUFFICIENT_SAMPLE
  deletion_results.json    deletion + final-consonant analysis
  generalization.json      speaker / age / frequency dependence
  age_l1_matrix.json       age-band breakdown (within-domain only; LWE noted)
"""
from __future__ import annotations

import csv
import json
import sys
from collections import defaultdict
from pathlib import Path

OUT = Path(__file__).resolve().parents[1]
EV = OUT / "evidence"
GOOD = 1.0
REJECT = 0.5
MIN_N = 30  # below this many expert-correct tokens => INSUFFICIENT_SAMPLE

TARGET_PHONES = ["T", "D", "N", "S", "Z", "L", "R", "M", "NG", "TH", "SH",
                 "CH"]


def load(name):
    with open(EV / name, encoding="utf-8") as f:
        return list(csv.DictReader(f))


def key(row):
    return (row["speaker"], row["path"], row["phone_index"])


def fnum(x):
    try:
        return float(x)
    except (TypeError, ValueError):
        return None


def main():
    base = load("zero_shot_per_phone.csv")
    b1 = load("b1_per_phone.csv")
    B = {key(r): r for r in base}
    A = {key(r): r for r in b1}
    missing = [k for k in B if k not in A]
    joined = [(B[k], A[k]) for k in B if k in A]

    # per-phone
    per = defaultdict(lambda: {"n_good": 0, "n_bad": 0, "b_frr": 0, "a_frr": 0,
                               "b_far": 0, "a_far": 0})
    for r, a in joined:
        acc = fnum(r["expert_acc"])
        ph = r["canonical_phone"].rstrip("012")
        if acc is None:
            continue
        bs = fnum(r["sim"])
        as_ = fnum(a["sim"])
        if acc >= GOOD:
            per[ph]["n_good"] += 1
            if bs < REJECT:
                per[ph]["b_frr"] += 1
            if as_ < REJECT:
                per[ph]["a_frr"] += 1
        elif acc < REJECT:
            per[ph]["n_bad"] += 1
            if bs >= REJECT:
                per[ph]["b_far"] += 1
            if as_ >= REJECT:
                per[ph]["a_far"] += 1
    phone_rows = []
    for ph, d in sorted(per.items(), key=lambda kv: -kv[1]["n_good"]):
        ng = d["n_good"]
        nb = d["n_bad"]
        row = {"phone": ph, "n_good": ng, "n_bad": nb,
               "baseline_frr": round(d["b_frr"] / ng, 4) if ng else None,
               "b1_frr": round(d["a_frr"] / ng, 4) if ng else None,
               "delta_frr": round((d["a_frr"] - d["b_frr"]) / ng, 4) if ng else None,
               "baseline_far": round(d["b_far"] / nb, 4) if nb else None,
               "b1_far": round(d["a_far"] / nb, 4) if nb else None}
        if ng < MIN_N:
            row["flag"] = "INSUFFICIENT_SAMPLE"
        phone_rows.append(row)
    (OUT / "phone_results.json").write_text(json.dumps({
        "min_n": MIN_N, "target_phones": TARGET_PHONES,
        "phones": phone_rows,
        "watchlist": [r for r in phone_rows if r["phone"] in TARGET_PHONES],
    }, indent=2), encoding="utf-8")

    # deletion / final consonant
    # final consonant = canonical phone is last position of a consonant target
    # We approximate: error_type == deletion OR phone in consonant set at word
    # end is not directly available; use corpus error_type == deletion.
    del_rows = [r for r, a in joined if r["error_type"] == "deletion"]
    del_b_rej = sum(1 for r in del_rows if fnum(r["sim"]) < REJECT)
    del_a_rej = sum(1 for r, a in joined if r["error_type"] == "deletion"
                    and fnum(a["sim"]) < REJECT)
    # deletion detection: how often model rejects a true deletion (sim<0.5)
    deletion = {
        "n_deletion_tokens": len(del_rows),
        "baseline_rejected_true_deletion": del_b_rej,
        "b1_rejected_true_deletion": del_a_rej,
        "baseline_deletion_reject_rate": round(del_b_rej / max(1, len(del_rows)), 4),
        "b1_deletion_reject_rate": round(del_a_rej / max(1, len(del_rows)), 4),
        "note": "<DEL> is its own error category; never converted to a phone.",
    }
    final_phones = ["T", "D", "N", "S", "Z", "L", "R", "M", "NG", "P", "K",
                    "B", "G", "F", "V", "TH", "SH", "CH", "JH", "ZH"]
    fin = defaultdict(lambda: {"n": 0, "bg": 0, "ag": 0})
    for r, a in joined:
        ph = r["canonical_phone"].rstrip("012")
        if ph in final_phones:
            fin[ph]["n"] += 1
            if fnum(r["sim"]) < REJECT:
                fin[ph]["bg"] += 1
            if fnum(a["sim"]) < REJECT:
                fin[ph]["ag"] += 1
    deletion["final_consonant_tokens"] = {
        ph: {"n": d["n"],
             "baseline_reject_rate": round(d["bg"] / d["n"], 4),
             "b1_reject_rate": round(d["ag"] / d["n"], 4)}
        for ph, d in sorted(fin.items(), key=lambda kv: -kv[1]["n"])}
    (OUT / "deletion_results.json").write_text(json.dumps(deletion, indent=2),
                                               encoding="utf-8")

    # generalization: speaker + age
    spk = defaultdict(lambda: {"n": 0, "bg": 0, "ag": 0})
    age = defaultdict(lambda: {"n": 0, "bg": 0, "ag": 0})
    for r, a in joined:
        if fnum(r["expert_acc"]) is None or fnum(r["expert_acc"]) < GOOD:
            continue
        for d, k in ((spk, r["speaker"]), (age, str(r["age"]))):
            d[k]["n"] += 1
            if fnum(r["sim"]) < REJECT:
                d[k]["bg"] += 1
            if fnum(a["sim"]) < REJECT:
                d[k]["ag"] += 1
    gen = {
        "joined_tokens": len(joined), "missing_in_b1": len(missing),
        "per_speaker": {k: {"n": v["n"],
                            "baseline_frr": round(v["bg"] / v["n"], 4),
                            "b1_frr": round(v["ag"] / v["n"], 4)}
                        for k, v in sorted(spk.items())},
        "per_age": {k: {"n": v["n"],
                        "baseline_frr": round(v["bg"] / v["n"], 4),
                        "b1_frr": round(v["ag"] / v["n"], 4)}
                    for k, v in sorted(age.items(), key=lambda kv: int(kv[0]))},
        "interpretation": "speaker-disjoint test; B1 FRR computed on the same "
                          "held-out speakers as the baseline.",
    }
    (OUT / "generalization.json").write_text(json.dumps(gen, indent=2),
                                             encoding="utf-8")

    # age / L1 transfer matrix (within-domain + LWE reference)
    matrix = {
        "so762_child_6_15_mandarin_l1": {
            "n_good": sum(1 for r, a in joined
                          if fnum(r["expert_acc"]) and fnum(r["expert_acc"]) >= GOOD),
            "baseline_frr": _rate(joined, "b"),
            "b1_frr": _rate(joined, "a"),
        },
        "lwe_child_vietnamese_l1_age4": {
            "status": "BLOCKED_RAW_AUDIO_GITIGNORED",
            "frozen_baseline_human_correct_low_score_rate": 0.3103,
            "b1": "NOT_COMPUTABLE",
        },
        "siak": {"status": "SEE_siak_transfer.json"},
    }
    (OUT / "age_l1_matrix.json").write_text(json.dumps(matrix, indent=2),
                                            encoding="utf-8")

    print(json.dumps({"per_phone_top": phone_rows[:8],
                      "deletion": deletion,
                      "gen_summary": gen["per_age"],
                      "missing_in_b1": len(missing)}, indent=2))
    return phone_rows


def _rate(joined, which):
    n = 0
    rej = 0
    for r, a in joined:
        acc = fnum(r["expert_acc"])
        if acc is None or acc < GOOD:
            continue
        n += 1
        s = fnum(r["sim"]) if which == "b" else fnum(a["sim"])
        if s < REJECT:
            rej += 1
    return round(rej / n, 4) if n else None


if __name__ == "__main__":
    main()
