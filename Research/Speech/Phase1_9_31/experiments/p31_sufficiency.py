"""WP-1.9.31 — label sufficiency recount under the external child standard."""
from __future__ import annotations

import csv
from collections import Counter, defaultdict

import p31_common as C


def read(path):
    return list(csv.DictReader(open(path, encoding="utf-8")))


def type_b_lookup(detail):
    cases = {
        "child_07_seven": ("lwe", None, None),
        "014180143_15": ("so762", "014180143", 15),
        "014190172_7": ("so762", "014190172", 7),
        "014350146_16": ("so762", "014350146", 16),
    }
    out = {}
    for cid, (_src, utt, k) in cases.items():
        if utt is None:
            out[cid] = {"status": "NO_EXPERT_LABELS", "reason": "zenodo 'lwe' corpus has no phone labels"}
            continue
        d = detail.get(utt)
        if d is None:
            out[cid] = {"status": "UTTERANCE_NOT_FOUND"}
            continue
        found = None
        for kk, wi, pos, ref, canon, is_final, wtext in C.iter_utt_tokens(d):
            if kk != k:
                continue
            refs = (d["words"][wi].get("ref-phones") or "").split()
            experts = (d["words"][wi].get("phones") or [])
            per_pos, ok = C.parse_word_marks(experts, len(refs))
            if ok and pos < len(per_pos):
                lab, p2, p1, a0 = C.label_for_marks(per_pos[pos])
                found = {"status": "LABELED", "word": wtext, "phone": canon,
                         "marks": per_pos[pos], "label": lab, "P2": p2, "P1": p1, "A0": a0}
        out[cid] = found or {"status": "TOKEN_NOT_FOUND"}
    return out


def set_stats(rows):
    st = {"tokens": len(rows), "labels": dict(Counter(r["label"] for r in rows)),
          "any_zero": sum(1 for r in rows if r["any_zero"] == "1"),
          "strict_correct": sum(1 for r in rows if r["strict_correct"] == "1")}
    spk = defaultdict(Counter)
    for r in rows:
        spk[r["speaker_id"]][r["label"]] += 1
    st["speakers"] = len(spk)
    return st


def main():
    detail = C.load_detail()
    res = {}

    pilot = list(csv.DictReader(open(C.ART / "labels/expert_labels_pilot.csv", encoding="utf-8")))
    res["pilot"] = set_stats(pilot)
    for sp in ("train", "dev", "test"):
        rows = [r for r in pilot if r["set"] == f"PILOT_{sp}"]
        res["pilot"][sp] = set_stats(rows)

    # /r/ under both definitions (bench, all)
    r_rows = []
    for sp in ("train", "test"):
        r_rows += [r for r in csv.DictReader(
            open(C.ART / f"labels/expert_labels_bench_{sp}.csv", encoding="utf-8"))
            if r["target_phone"] == "ɹ" and r["label"]]
    produced = Counter(r["label"] for r in r_rows)
    strict = Counter("strict_not" if r["strict_correct"] == "0" else "strict_ok" for r in r_rows)
    r_speakers = len({r["speaker_id"] for r in r_rows})
    res["r_all"] = {"tokens": len(r_rows), "produced": dict(produced), "strict": dict(strict),
                    "speakers": r_speakers,
                    "gate_produced": {"present": produced["PRESENT"], "absent": produced["ABSENT"],
                                      "speakers": r_speakers,
                                      "met": produced["PRESENT"] >= 15 and produced["ABSENT"] >= 15
                                             and r_speakers >= 10},
                    "gate_strict": {"ok": strict["strict_ok"], "not": strict["strict_not"],
                                    "met": strict["strict_ok"] >= 15 and strict["strict_not"] >= 15}}
    for sp in ("train", "test"):
        rows = [r for r in csv.DictReader(
            open(C.ART / f"labels/expert_labels_bench_{sp}.csv", encoding="utf-8"))
            if r["target_phone"] == "ɹ" and r["label"]]
        res["r_all"][sp] = {"labels": dict(Counter(r["label"] for r in rows)),
                            "strict_not": sum(1 for r in rows if r["strict_correct"] == "0")}

    res["type_b"] = type_b_lookup(detail)

    for sp in ("train", "test"):
        rows = list(csv.DictReader(
            open(C.ART / f"labels/expert_labels_bench_{sp}.csv", encoding="utf-8")))
        res[f"bench_{sp}"] = set_stats(rows)

    C.write_json(C.ART / "sufficiency/label_sufficiency_p31.json", res)
    print("pilot:", res["pilot"]["labels"], "test:", res["pilot"]["test"]["labels"])
    print("bench train:", res["bench_train"]["labels"], "bench test:", res["bench_test"]["labels"])
    print("/r/ produced:", res["r_all"]["produced"], "strict:", res["r_all"]["strict"],
          "speakers:", r_speakers, "gate_produced_met:", res["r_all"]["gate_produced"]["met"])
    for cid, v in res["type_b"].items():
        print("typeB", cid, v["status"], v.get("label", ""), v.get("marks", ""))
    print("DONE p31_sufficiency")


if __name__ == "__main__":
    main()
