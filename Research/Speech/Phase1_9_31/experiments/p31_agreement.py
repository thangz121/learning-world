"""WP-1.9.31 — inter-expert agreement of the SO762 five-expert panel.

Computes, per set (pilot 546, bench train, bench test):
  - 10 pairwise raw agreements + Cohen's kappa (3-way marks 2/1/0) and binary ({2,1} vs 0)
  - Fleiss' kappa over the 5 raters (3-way and binary)
Replaces the "SINGLE_REVIEWER_LIMITATION" row with a real multi-expert estimate.
"""
from __future__ import annotations

import csv
from collections import Counter
from itertools import combinations

import p31_common as C

SETS = [("pilot", C.ART / "labels/expert_labels_pilot.csv"),
        ("bench_train", C.ART / "labels/expert_labels_bench_train.csv"),
        ("bench_test", C.ART / "labels/expert_labels_bench_test.csv")]


def cohen_kappa(a, b, k=3):
    n = len(a)
    if n == 0:
        return None
    po = sum(1 for x, y in zip(a, b) if x == y) / n
    ca, cb = Counter(a), Counter(b)
    pe = sum((ca[c] / n) * (cb[c] / n) for c in set(ca) | set(cb))
    if pe >= 1.0:
        return None
    return (po - pe) / (1 - pe)


def fleiss_kappa(rows, k=3):
    """rows = list of per-subject mark count dicts {cat: count}; constant raters."""
    N = len(rows)
    if N == 0:
        return None
    n = sum(rows[0].values())
    P_i = []
    total = Counter()
    for r in rows:
        s = sum(v * v for v in r.values())
        P_i.append((s - n) / (n * (n - 1)))
        total.update(r)
    P_bar = sum(P_i) / N
    p = {c: total[c] / (N * n) for c in total}
    Pe = sum(v * v for v in p.values())
    if Pe >= 1.0:
        return None
    return (P_bar - Pe) / (1 - Pe)


def agreement_block(path):
    rows = [r for r in csv.DictReader(open(path, encoding="utf-8"))
            if r["label"] and r["m1"]]
    marks = [[r[f"m{i}"] for i in range(1, 6)] for r in rows]
    binm = [["1" if m in ("1", "2") else "0" for m in ms] for ms in marks]
    out = {"tokens": len(rows)}
    for name, mm in (("three_way", marks), ("binary", binm)):
        n_cat = 3 if name == "three_way" else 2
        pairs = list(combinations(range(5), 2))
        raw, kap = [], []
        for i, j in pairs:
            a = [m[i] for m in mm]
            b = [m[j] for m in mm]
            raw.append(sum(1 for x, y in zip(a, b) if x == y) / len(a) if a else None)
            kap.append(cohen_kappa(a, b, n_cat))
        fr = []
        for ms in mm:
            fr.append(Counter(ms))
        out[name] = {
            "raw_agreement_mean": round(sum(raw) / len(raw), 4) if raw else None,
            "raw_agreement_min": round(min(raw), 4) if raw else None,
            "cohen_kappa_mean": round(sum(k for k in kap if k is not None)
                                      / max(1, len([k for k in kap if k is not None])), 4)
                                if any(k is not None for k in kap) else None,
            "fleiss_kappa": round(fleiss_kappa(fr, n_cat), 4) if fleiss_kappa(fr, n_cat) is not None else None,
        }
    dist = Counter()
    for ms in marks:
        dist["+".join(sorted(ms))] += 1
    out["pattern_count"] = len(dist)
    out["top_patterns"] = dict(dist.most_common(10))
    out["unanimous"] = round(sum(1 for ms in marks if len(set(ms)) == 1) / len(rows), 4) if rows else None
    return out


def main():
    res = {}
    for name, path in SETS:
        res[name] = agreement_block(path)
        a = res[name]
        print(f"{name}: n={a['tokens']} unanimous={a['unanimous']} "
              f"3way raw={a['three_way']['raw_agreement_mean']} fleiss={a['three_way']['fleiss_kappa']} "
              f"bin raw={a['binary']['raw_agreement_mean']} fleiss={a['binary']['fleiss_kappa']}")
    C.write_json(C.ART / "agreement/expert_agreement.json", res)
    print("DONE p31_agreement")


if __name__ == "__main__":
    main()
