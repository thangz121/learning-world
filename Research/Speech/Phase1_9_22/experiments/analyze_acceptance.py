"""WP-1.9.22 — acceptance / identity decision audit (research-only).

Experiments 2-6 on the reconstructed production decisions (Experiment 1) and the
WP-1.9.21 frame cache:

  2. identity counterfactuals (FRR-first) on LWE / SO762 dev / SO762 test / absent controls
  3. false-acceptance mechanism decomposition
  4. present-side cost of removing identity credit
  5. identity margin analysis (span-mean + peak; true present / absent / uncertain)
  6. cross-window identity causality

No production change, no training, no encoder rerun (Experiment 1 already rebuilt
the span-mean decision for the primary window).
"""
from __future__ import annotations

import csv
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_22"
ART = OUT / "artifacts"
CACHE = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"
PRIMARY = "full"
WINDOWS = L.WINDOWS
TAUS = (0.02, 0.05, 0.10, 0.20, 0.30, 0.50)

CORPORA = ["lwe", "so762_dev", "so762_test", "so762_absent_dev"]


def num(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def load_spanmean():
    d = {}
    for c in CORPORA:
        for r in csv.DictReader(open(ART / f"spanmean_decision_{c}.csv", encoding="utf-8")):
            d[r["token_id"]] = r
    return d


def load_evidence():
    d = {}
    for c in CORPORA:
        for r in csv.DictReader(open(CACHE / f"token_evidence_{c}.csv", encoding="utf-8")):
            d[(r["token_id"], r["window_type"])] = r
    return d


def load_frames():
    d = defaultdict(list)
    for c in CORPORA:
        for r in csv.DictReader(open(CACHE / f"frames_{c}.csv", encoding="utf-8")):
            d[(r["token_id"], r["window_type"])].append(r)
    return d


def target_lookup():
    """token_id -> (target list, focus j, prev phone, next phone)."""
    from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
    import json as _json
    tgt = CmuDictTargetAdapter()
    inv = L.PhoneEvidenceV2().inv
    detail = _json.loads((L.SO / "resource/scores-detail.json").read_text(encoding="utf-8"))
    out = {}
    for c in CORPORA:
        for r in csv.DictReader(open(ART / f"spanmean_decision_{c}.csv", encoding="utf-8")):
            if c == "lwe":
                st = tgt.build(r["word"])
                target = inv.arpa_seq_to_canon([str(x) for x in st.arpabet])
                j = len(target) - 1
            else:
                utt = r["token_id"].rsplit("_", 1)[0]
                words = L.parse_words_so762(detail[utt])
                ref, scores, wpos, ppos, wtext = L.flatten_so762(words)
                target = inv.arpa_seq_to_canon(ref)
                j = int(r["token_id"].rsplit("_", 1)[1])
            prev = target[j - 1] if j > 0 else ""
            nxt = target[j + 1] if j + 1 < len(target) else ""
            out[r["token_id"]] = {"target": target, "j": j, "prev": prev, "next": nxt}
    return out


def raw_best_sim(r):
    canon = r["target_phone"]
    if r["best_obs"] == canon:
        return 1.0
    return L.phone_similarity(canon, r["best_obs"])


def truth_of(r):
    hp = r.get("human_present", "")
    return int(hp) if hp != "" else None


def decide(variant, params, r, ev, rbs):
    """Counterfactual decision functions (primary window)."""
    prod = int(r["production_decision"])
    if variant == "A_PRODUCTION":
        return prod
    if variant == "B_NO_IDENTITY_KEEP_SIM":
        # production rule minus the identity clause: posterior path + similarity path
        if num(r["target_post_mean"]) >= 0.25:
            return 1
        return int(r["best_obs"] != r["target_phone"] and rbs >= 0.35)
    if variant == "C_SUPPORT_ONLY":
        return int(num(r["target_max_A"]) >= params["tau"])
    if variant == "C2_PEAK_SUPPORT_ONLY":
        return int(num(ev["peak"]) >= params["tau"])
    if variant == "D_IDENTITY_AND_SUPPORT":
        return int(int(r["identity_credit"]) == 1 and num(r["target_max_A"]) >= params["tau"])
    if variant == "E_IDENTITY_OR_SIM_NO_POSTERIOR":
        return int(int(r["identity_credit"]) == 1
                   or (r["best_obs"] != r["target_phone"] and rbs >= 0.35))
    if variant == "F_SUPPORT_OR_SIM_NO_IDENTITY":
        return int(num(r["target_max_A"]) >= params["tau"]
                   or (r["best_obs"] != r["target_phone"] and rbs >= 0.35))
    if variant == "G_POSTERIOR_MEAN_ONLY":
        return int(num(r["target_post_mean"]) >= params["tau"])
    raise ValueError(variant)


def metrics(rows, variant, params, ev_map, rbs_map):
    tp = fn = fp = tn = 0
    changed = 0
    unsupported = 0
    rejects_true = 0
    accepts_false = 0
    decisions = {}
    for r in rows:
        ev = ev_map[(r["token_id"], PRIMARY)]
        rbs = rbs_map[r["token_id"]]
        d = decide(variant, params, r, ev, rbs)
        decisions[r["token_id"]] = d
        prod = int(r["production_decision"])
        changed += (d != prod)
        y = truth_of(r)
        if y is None:
            continue
        if d == 1:
            if num(r["target_max_A"]) < 0.15 and num(r["target_post_mean"]) < 0.25:
                unsupported += 1
        if y == 1:
            tp += d
            fn += (not d)
            rejects_true += int(prod == 1 and d == 0)
        else:
            fp += d
            tn += (not d)
            accepts_false += int(prod == 0 and d == 1)
    n_lab = tp + fn + fp + tn
    return {
        "n": n_lab, "present": tp + fn, "absent": fp + tn,
        "recall": round(tp / (tp + fn), 4) if (tp + fn) else None,
        "frr": round(fn / (tp + fn), 4) if (tp + fn) else None,
        "far": round(fp / (fp + tn), 4) if (fp + tn) else None,
        "absent_detection": round(tn / (fp + tn), 4) if (fp + tn) else None,
        "unsupported_accepts": unsupported,
        "false_present_on_absent": fp,
        "changed_from_production": changed,
        "rejects_true_present": rejects_true,
        "new_false_accepts": accepts_false,
        "uncertain": 0,
        "decisions": decisions,
    }


def main():
    span = load_spanmean()
    ev = load_evidence()
    frames = load_frames()
    tinfo = target_lookup()
    rbs = {tid: raw_best_sim(r) for tid, r in span.items()}

    labeled = {tid: r for tid, r in span.items() if truth_of(r) is not None}
    lwe = {tid: r for tid, r in labeled.items() if r["corpus"] == "lwe"}
    dev = {tid: r for tid, r in labeled.items()
           if r["corpus"] in ("so762_dev", "so762_absent_dev")}
    test = {tid: r for tid, r in labeled.items() if r["corpus"] == "so762_test"}
    lwe_absent = {tid: r for tid, r in lwe.items() if truth_of(r) == 0}
    dev_absent = {tid: r for tid, r in dev.items() if truth_of(r) == 0}
    datasets = [("lwe", lwe), ("so762_dev", dev), ("so762_test", test),
                ("lwe_absent", lwe_absent), ("so762_absent", dev_absent)]

    # ---------------- Experiment 2 ----------------
    variant_rows = []
    variant_specs = [("A_PRODUCTION", {})]
    variant_specs += [("B_NO_IDENTITY_KEEP_SIM", {})]
    for v in ("C_SUPPORT_ONLY", "C2_PEAK_SUPPORT_ONLY", "D_IDENTITY_AND_SUPPORT",
              "F_SUPPORT_OR_SIM_NO_IDENTITY", "G_POSTERIOR_MEAN_ONLY"):
        for tau in TAUS:
            variant_specs.append((v, {"tau": tau}))
    variant_specs.append(("E_IDENTITY_OR_SIM_NO_POSTERIOR", {}))

    decisions_all = {}
    for v, params in variant_specs:
        for ds_name, rows in datasets:
            m = metrics(list(rows.values()), v, params, ev, rbs)
            decisions_all[(v, tuple(sorted(params.items())), ds_name)] = m.pop("decisions")
            variant_rows.append({"variant": v,
                                 "params": json.dumps(params, sort_keys=True),
                                 "dataset": ds_name, **m})

    # ---------------- Experiment 3: false acceptance decomposition ----------------
    decomp_rows = []
    for ds_name, rows in (("lwe", lwe), ("so762_dev", dev)):
        for tid, r in sorted(rows.items()):
            if truth_of(r) != 0 or int(r["production_decision"]) != 1:
                continue
            evr = ev[(tid, PRIMARY)]
            win_dec = {}
            for w in WINDOWS:
                e = ev.get((tid, w))
                if e is not None:
                    win_dec[w] = int(e["baseline_present"])
            window_dependent = len(set(win_dec.values())) > 1
            ident = int(r["identity_credit"]) == 1
            support = num(r["target_max_A"])
            peak = num(evr["peak"])
            post = num(r["target_post_mean"])
            margin_mean = num(r["identity_margin_mean"])
            blank = num(r["blank_mean_span"])
            rank = int(r["target_rank_in_top5"])
            if r["best_obs"] != r["target_phone"] and post < 0.25 and rbs[tid] >= 0.35:
                mech = "SOFT_SIMILARITY"
            elif r["best_obs"] != r["target_phone"] and post >= 0.25:
                mech = "POSTERIOR_PATH"
            elif ident and support >= 0.30:
                mech = "IDENTITY_STRONG"
            elif ident:
                mech = "IDENTITY_WEAK"
            else:
                mech = "OTHER"
            decomp_rows.append({
                "corpus": r["corpus"], "token_id": tid, "speaker_id": r["speaker_id"],
                "word": r["word"], "target_phone": r["target_phone"],
                "human_label": r["human_label"], "match_type": r["match_type"],
                "identity_credit": int(ident), "target_rank_in_top5": rank,
                "target_post_mean": round(post, 6), "target_max_A": round(support, 6),
                "peak_D": round(peak, 6), "blank_mean_span": round(blank, 6),
                "competitor_phone": r["competitor_phone"],
                "competitor_post": round(num(r["competitor_post"]), 6),
                "identity_margin_mean": round(margin_mean, 6),
                "peak_margin": round(num(r["peak_margin"]), 6),
                "primary_mechanism": mech,
                "flag_identity_strong": int(mech == "IDENTITY_STRONG"),
                "flag_identity_weak": int(mech == "IDENTITY_WEAK"),
                "flag_soft_similarity": int(mech == "SOFT_SIMILARITY"),
                "flag_posterior_path": int(mech == "POSTERIOR_PATH"),
                "flag_competitor_conflict": int(margin_mean < 0),
                "flag_blank_dominated": int(blank >= 0.90 and post < 0.05),
                "flag_window_dependent": int(window_dependent),
                "windows_accept": ";".join(f"{w}:{v}" for w, v in win_dec.items()),
                "evidence": f"rank={rank} post={post:.5f} maxA={support:.5f} "
                            f"blank={blank:.4f} margin={margin_mean:.5f} peakMargin="
                            f"{num(r['peak_margin']):.5f}",
            })

    # ---------------- Experiment 4: present-side cost ----------------
    present_rows = []
    focus_cases = ["child_07_one", "child_01_seven", "child_02_ten", "child_04_four",
                   "child_01_ten", "child_01_four", "child_03_four", "child_02_four",
                   "child_06_six", "child_01_nine", "child_01_eight"]
    for tid, r in sorted(labeled.items()):
        y = truth_of(r)
        prod = int(r["production_decision"])
        if prod != 1 and tid not in focus_cases:
            continue
        evr = ev[(tid, PRIMARY)]
        d_b = decide("B_NO_IDENTITY_KEEP_SIM", {}, r, evr, rbs[tid])
        d_d1 = decide("D_IDENTITY_AND_SUPPORT", {"tau": 0.10}, r, evr, rbs[tid])
        d_d3 = decide("D_IDENTITY_AND_SUPPORT", {"tau": 0.30}, r, evr, rbs[tid])
        cost = ("not_accepted_by_production" if prod == 0
                else ("identity_removal_rejects_true_present" if y == 1 and d_b == 0
                      else ("identity_removal_rejects_false_present" if y == 0 and d_b == 0
                            else "identity_removal_changes_nothing")))
        present_rows.append({
            "token_id": tid, "corpus": r["corpus"], "word": r["word"],
            "target_phone": r["target_phone"],
            "human_label": r["human_label"], "truth": y,
            "production_decision": prod, "match_type": r["match_type"],
            "identity_credit": int(r["identity_credit"]),
            "target_rank_in_top5": int(r["target_rank_in_top5"]),
            "target_post_mean": round(num(r["target_post_mean"]), 6),
            "target_max_A": round(num(r["target_max_A"]), 6),
            "raw_best_sim": round(rbs[tid], 4),
            "B_no_identity": d_b, "D_identity_support_0.10": d_d1,
            "D_identity_support_0.30": d_d3,
            "cost": cost,
            "focus_case": int(tid in focus_cases),
        })

    # ---------------- Experiment 5: identity margin analysis ----------------
    margin_rows = []
    groups = {"true_present": [], "true_absent": [], "human_uncertain": []}
    for tid, r in sorted(labeled.items()):
        y = truth_of(r)
        g = "true_present" if y == 1 else "true_absent"
        margin_rows.append(_margin_row(r, tid, g, tinfo))
        groups[g].append(margin_rows[-1])
    for (tid, w), e in sorted(ev.items()):
        if w != PRIMARY or e["human_verdict"] != "AMBIGUOUS":
            continue
        r = span.get(tid)
        if r is None:
            continue
        mr = _margin_row(r, tid, "human_uncertain", tinfo)
        margin_rows.append(mr)
        groups["human_uncertain"].append(mr)

    def dist(rows):
        if not rows:
            return {"n": 0}
        margins = sorted(x["identity_margin_mean"] for x in rows)
        posts = sorted(x["target_post_mean"] for x in rows)
        bl = sorted(x["blank_mean_span"] for x in rows)
        def q(a, p):
            return a[min(len(a) - 1, int(p * (len(a) - 1)))]
        return {
            "n": len(rows),
            "margin_median": round(q(margins, 0.5), 6),
            "margin_q25": round(q(margins, 0.25), 6),
            "margin_q75": round(q(margins, 0.75), 6),
            "margin_negative_n": sum(1 for m in margins if m < 0),
            "post_median": round(q(posts, 0.5), 6),
            "post_lt_0.01_n": sum(1 for p in posts if p < 0.01),
            "blank_median": round(q(bl, 0.5), 6),
            "blank_ge_0.90_n": sum(1 for b in bl if b >= 0.90),
            "identity_accepted_n": sum(1 for x in rows if x["identity_credit"] == 1),
            "identity_rank0_n": sum(1 for x in rows
                                    if x["identity_credit"] == 1 and x["target_rank_in_top5"] == 0),
            "identity_rank_gt0_n": sum(1 for x in rows
                                       if x["identity_credit"] == 1 and x["target_rank_in_top5"] > 0),
            "identity_positive_margin_n": sum(1 for x in rows
                                              if x["identity_credit"] == 1
                                              and x["identity_margin_mean"] > 0),
            "peak_blank_ge_0.5_n": sum(1 for x in rows if x["peak_blank"] >= 0.5),
        }

    def auc(scores, labels):
        pos = [s for s, y in zip(scores, labels) if y == 1]
        neg = [s for s, y in zip(scores, labels) if y == 0]
        if not pos or not neg:
            return None
        wins = sum(1.0 if a > b else (0.5 if a == b else 0.0) for a in pos for b in neg)
        return round(wins / (len(pos) * len(neg)), 4)

    margin_dist = {g: dist(rows) for g, rows in groups.items()}
    margin_dist["discrimination"] = {}
    for ds_name, rows in (("lwe", lwe), ("so762_dev", dev)):
        rr = [r for r in rows.values() if truth_of(r) is not None]
        labels = [truth_of(r) for r in rr]
        margin_dist["discrimination"][ds_name] = {
            "n_present": sum(labels), "n_absent": len(labels) - sum(labels),
            "auc_identity_margin_mean": auc([num(r["identity_margin_mean"]) for r in rr], labels),
            "auc_target_post_mean": auc([num(r["target_post_mean"]) for r in rr], labels),
            "auc_target_max_A": auc([num(r["target_max_A"]) for r in rr], labels),
            "auc_target_peak_in_span": auc([num(r["peak_post"]) for r in rr], labels),
        }

    # ---------------- Experiment 6: window identity ----------------
    window_rows = []
    for tid, r in sorted(labeled.items()):
        ti = tinfo.get(tid, {})
        prev_ph, next_ph = ti.get("prev", ""), ti.get("next", "")
        id_by_win = {}
        neighbor_share = {}
        for w in WINDOWS:
            e = ev.get((tid, w))
            if e is None:
                continue
            id_by_win[w] = int(e["baseline_best_obs"] == r["target_phone"])
            fr = frames.get((tid, w), [])
            span_frames = [f for f in fr if f["in_current_span"] == "1"]
            if span_frames:
                nb = sum(1 for f in span_frames
                         if f["top1_phone"] in (prev_ph, next_ph) and f["top1_phone"] != "")
                neighbor_share[w] = round(nb / len(span_frames), 3)
        vals = list(id_by_win.values())
        if not vals:
            continue
        if all(v == vals[0] for v in vals):
            cls = "IDENTITY_STABLE_PRESENT" if vals[0] == 1 else "IDENTITY_STABLE_ABSENT"
        else:
            core = [id_by_win.get(w) for w in ("full", "raw")]
            pad = [id_by_win.get(w) for w in ("pad100", "pad250")]
            if all(v == 0 for v in core if v is not None) and any(v == 1 for v in pad if v is not None):
                cls = "IDENTITY_APPEARS_ONLY_OUTSIDE_CORE_SPAN"
            elif sum(1 for v in vals if v == 1) == 1:
                cls = "IDENTITY_UNSTABLE"
            elif any(neighbor_share.get(w, 0) > 0.5 and id_by_win.get(w) == 1
                     for w in WINDOWS):
                cls = "IDENTITY_FROM_NEIGHBOR_PHONE"
            else:
                cls = "IDENTITY_WINDOW_SENSITIVE"
        window_rows.append({
            "token_id": tid, "corpus": r["corpus"], "target_phone": r["target_phone"],
            "truth": truth_of(r), "human_label": r["human_label"],
            "production_decision": int(r["production_decision"]),
            "identity_full": id_by_win.get("full", ""),
            "identity_raw": id_by_win.get("raw", ""),
            "identity_pad100": id_by_win.get("pad100", ""),
            "identity_pad250": id_by_win.get("pad250", ""),
            "identity_windows_present": sum(1 for v in vals if v == 1),
            "classification": cls,
            "neighbor_share_full": neighbor_share.get("full", ""),
            "neighbor_share_raw": neighbor_share.get("raw", ""),
            "prev_phone": prev_ph, "next_phone": next_ph,
        })

    # ---------------- outputs ----------------
    L.write_rows(OUT / "ACCEPTANCE_VARIANT_RESULTS.csv", variant_rows)
    L.write_rows(OUT / "FALSE_ACCEPTANCE_DECOMPOSITION.csv", decomp_rows)
    L.write_rows(OUT / "IDENTITY_MARGIN_ANALYSIS.csv", margin_rows)
    L.write_rows(OUT / "WINDOW_IDENTITY_ANALYSIS.csv", window_rows)

    results = {
        "phase": "1.9.22",
        "primary_window": PRIMARY,
        "experiment1_reproduction": json.loads(
            (ART / "experiment1_reproduction.json").read_text(encoding="utf-8")),
        "experiment2_variants": variant_rows,
        "experiment3_decomposition": {
            "n_false_accepts": len(decomp_rows),
            "mechanisms": dict(Counter(r["primary_mechanism"] for r in decomp_rows)),
            "flags": {
                "competitor_conflict": sum(r["flag_competitor_conflict"] for r in decomp_rows),
                "blank_dominated": sum(r["flag_blank_dominated"] for r in decomp_rows),
                "window_dependent": sum(r["flag_window_dependent"] for r in decomp_rows),
            },
            "rows": decomp_rows,
        },
        "experiment4_present_cost": {
            "cost_counts": dict(Counter(r["cost"] for r in present_rows)),
            "rows": present_rows,
        },
        "experiment5_identity_margin": {
            "distribution": margin_dist,
            "rows": margin_rows,
        },
        "experiment6_window_identity": {
            "classification_counts": dict(Counter(r["classification"] for r in window_rows)),
            "rows": window_rows,
        },
        "flags": {"production_vad": False, "router_locked": False, "unity_integrated": False,
                  "scorer_modified": False, "production_window_locked": False},
    }
    (OUT / "EXPERIMENT_RESULTS.json").write_text(
        json.dumps(results, indent=2, ensure_ascii=False), encoding="utf-8")

    # ---------------- console ----------------
    print("== Experiment 2 (selected) ==")
    for r in variant_rows:
        if r["variant"] in ("A_PRODUCTION", "B_NO_IDENTITY_KEEP_SIM",
                            "E_IDENTITY_OR_SIM_NO_POSTERIOR"):
            if r["dataset"] in ("lwe", "so762_dev", "so762_test"):
                print(f"  {r['variant']:32s} {r['dataset']:10s} r={r['recall']} "
                      f"far={r['far']} unsup={r['unsupported_accepts']} "
                      f"changed={r['changed_from_production']} "
                      f"rejTrue={r['rejects_true_present']}")
    for tau in TAUS:
        r = [x for x in variant_rows if x["variant"] == "D_IDENTITY_AND_SUPPORT"
             and x["params"] == json.dumps({"tau": tau}, sort_keys=True)
             and x["dataset"] == "lwe"][0]
        print(f"  D identity+support tau={tau}: r={r['recall']} far={r['far']} "
              f"changed={r['changed_from_production']} rejTrue={r['rejects_true_present']}")
    print("== Experiment 3 ==", results["experiment3_decomposition"]["mechanisms"],
          results["experiment3_decomposition"]["flags"])
    print("== Experiment 4 ==", results["experiment4_present_cost"]["cost_counts"])
    print("== Experiment 5 ==")
    for g, v in margin_dist.items():
        print(f"  {g:16s} {v}")
    print("== Experiment 6 ==", results["experiment6_window_identity"]["classification_counts"])
    print("DONE analyze_acceptance")


def _margin_row(r, tid, group, tinfo):
    ti = tinfo.get(tid, {})
    return {
        "token_id": tid, "corpus": r["corpus"], "word": r["word"],
        "target_phone": r["target_phone"], "group": group,
        "human_label": r["human_label"],
        "truth": truth_of(r),
        "production_decision": int(r["production_decision"]),
        "match_type": r["match_type"],
        "identity_credit": int(r["identity_credit"]),
        "target_rank_in_top5": int(r["target_rank_in_top5"]),
        "target_post_mean": round(num(r["target_post_mean"]), 6),
        "top1_phone": r["top1_phone"], "top1_post": round(num(r["top1_post"]), 6),
        "competitor_phone": r["competitor_phone"],
        "competitor_post": round(num(r["competitor_post"]), 6),
        "identity_margin_mean": round(num(r["identity_margin_mean"]), 6),
        "identity_ratio_mean": round(num(r["identity_ratio_mean"]), 4),
        "blank_mean_span": round(num(r["blank_mean_span"]), 6),
        "target_max_A": round(num(r["target_max_A"]), 6),
        "peak_D": round(num(r["peak_post"]), 6),
        "peak_margin": round(num(r["peak_margin"]), 6),
        "peak_blank": round(num(r["peak_blank"]), 6),
        "prev_phone": ti.get("prev", ""), "next_phone": ti.get("next", ""),
    }


if __name__ == "__main__":
    main()
