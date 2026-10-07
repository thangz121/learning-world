"""WP-1.9.24 Part B — encoder evidence design audit (research-only).

Replays the 4 TYPE-B false accepts with a full trace and the mandatory
falsification tests A-G, builds the phone confusion profile and the
child-speech representation statistics. Reads only existing artifacts:
WP-1.9.21 frame cache, WP-1.9.22 spanmean decisions, WP-1.9.23 feature
matrix and rule decisions. No encoder rerun, no training.
"""
from __future__ import annotations

import csv
import json
import statistics as st
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_24"
ART = OUT / "artifacts"
P21 = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"
P22 = L.REPO / "Research/Speech/Phase1_9_22"
P23 = L.REPO / "Research/Speech/Phase1_9_23"
WINDOWS = L.WINDOWS
PRIMARY = "full"
TYPE_B = ["child_07_seven", "014180143_15", "014190172_7", "014350146_16"]
CLASSES = {
    "stop": {"t", "k", "p", "d", "b", "ɡ"},
    "fricative": {"s", "z", "f", "v", "θ", "ð", "ʃ", "ʒ"},
    "nasal": {"n", "m", "ŋ"},
    "liquid": {"ɹ", "l"},
    "affricate": {"tʃ", "dʒ"},
}

TRACE_FIELDS = [
    "case_id", "corpus", "speaker_id", "word", "target_phone", "human_label",
    "human_label_source", "human_confidence",
    "n_frames", "span_start", "span_end", "span_ms", "target_mean", "target_max_A",
    "target_peak", "peak_frame", "peak_time_ms", "peak_rel_pos",
    "top1_phone", "top1_post", "top5_phones", "competitor_phone", "competitor_peak",
    "peak_margin", "blank_at_peak", "blank_mean_span", "blank_ge09_frac",
    "neighbor_prev", "neighbor_next", "cluster_width_D", "longest_run_05",
    "support_count_05", "max_A_full", "max_A_raw", "max_A_pad100", "max_A_pad250",
    "strong_windows", "identity_class", "production_decision", "rule_A", "rule_B",
    "rule_C", "rule_D", "rule_E", "rule_F", "rule_G", "rule_H", "rule_final",
    "fals_A_strong", "fals_B_window_stable", "fals_C_neighbor_support",
    "fals_D_position", "fals_E_beats_competitor", "fals_F_not_blank",
    "fals_G_confident_label", "verdict", "verdict_reason",
]


def num(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def load_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def main():
    feature = {r["token_id"]: r for r in csv.DictReader(
        open(P23 / "artifacts/feature_matrix.csv", encoding="utf-8"))}
    per_token = {r["token_id"]: r for r in csv.DictReader(
        open(P23 / "ACCEPTANCE_RULE_PER_TOKEN.csv", encoding="utf-8"))}
    ev = {}
    for c in ("lwe", "so762_dev", "so762_test", "so762_absent_dev"):
        for r in csv.DictReader(open(P21 / f"token_evidence_{c}.csv", encoding="utf-8")):
            ev[(r["token_id"], r["window_type"])] = r
    frames = defaultdict(list)
    for c in ("lwe", "so762_dev", "so762_test", "so762_absent_dev"):
        for r in csv.DictReader(open(P21 / f"frames_{c}.csv", encoding="utf-8")):
            if r["window_type"] == PRIMARY:
                frames[r["token_id"]].append(r)
    span = {}
    for c in ("lwe", "so762_dev", "so762_test", "so762_absent_dev"):
        for r in csv.DictReader(open(P22 / "artifacts" / f"spanmean_decision_{c}.csv",
                                     encoding="utf-8")):
            span[r["token_id"]] = r

    human_conf = {}
    for r in csv.DictReader(open(L.REPO / "Research/Speech/Phase1_9_12/Results/"
                                 "final_consonant_human_review.csv", encoding="utf-8-sig")):
        human_conf[r["case_id"].replace("fc_", "")] = r

    trace_rows = []
    for tid in TYPE_B:
        f = feature[tid]
        p = per_token[tid]
        s = span[tid]
        fr = sorted(frames.get(tid, []), key=lambda x: int(x["frame"]))
        # in-span peak frame = argmax target posterior within the production span
        span_fr = [x for x in fr if x["in_current_span"] == "1"]
        if span_fr:
            pk = int(max(span_fr, key=lambda x: num(x["target_posterior"]))["frame"])
        else:
            pk = -1
        prev_p = next_p = 0.0
        peak_masked = 0
        if fr and pk >= 0:
            for x in fr:
                if int(x["frame"]) == pk - 1:
                    prev_p = num(x["target_posterior"])
                if int(x["frame"]) == pk + 1:
                    next_p = num(x["target_posterior"])
                if int(x["frame"]) == pk:
                    peak_masked = int(x["in_earlier_same_class"] == "1")
        h = human_conf.get(tid)
        if h:
            human_label = h["human_final_label"]
            human_src = "WP-1.9.12 blind human review"
            human_c = h["reviewer_confidence"]
        else:
            human_label = "so762 phone score 0 (incorrect or missed)"
            human_src = "so762 expert per-phone score (NOT a final-consonant listening label)"
            human_c = "N/A"
        max_by_win = {w: num(ev[(tid, w)]["max_A"]) for w in WINDOWS if (tid, w) in ev}
        strong_windows = sum(1 for v in max_by_win.values() if v >= 0.30)
        evp = ev[(tid, PRIMARY)]
        frame_s = num(evp["frame_s"])
        span_ms = round((int(evp["span_A_end"]) - int(evp["span_A_start"]) + 1)
                        * frame_s * 1000, 1)
        peak_time = round(pk * frame_s * 1000, 1) if pk >= 0 else ""
        fals = {
            "A": int(num(f["target_max_A"]) >= 0.30),
            "B": int(strong_windows >= 3),
            "C": int(int(num(f["cluster_width_D"])) >= 2 or max(prev_p, next_p) >= 0.10),
            "D": int(peak_masked == 0),
            "E": int(int(num(f["competitor_wins_peak"])) == 0),
            "F": int(num(f["blank_at_peak"]) < 0.5),
            "G": int(h is not None and h["reviewer_confidence"] in ("HIGH", "MEDIUM")),
        }
        if all(fals.values()):
            verdict = "STRONG_ENCODER_FALSE_EVIDENCE"
            reason = "all falsification tests pass"
        else:
            verdict = "UNRESOLVED"
            fails = [k for k, v in fals.items() if not v]
            reasons = {
                "A": "target evidence not strong",
                "B": f"strong evidence in only {strong_windows}/4 windows",
                "C": f"isolated peak (cluster width {f['cluster_width_D']}, "
                     f"neighbors {prev_p:.4f}/{next_p:.4f})",
                "D": "peak may be an earlier same-class occurrence",
                "E": "competitor stronger than target at peak",
                "F": "blank dominates the peak frame",
                "G": "no confident human listening label (score-0 only)",
            }
            reason = "; ".join(reasons[k] for k in fails)
        trace_rows.append({
            "case_id": tid, "corpus": f["corpus"], "speaker_id": f["speaker_id"],
            "word": f["word"], "target_phone": f["target_phone"],
            "human_label": human_label, "human_label_source": human_src,
            "human_confidence": human_c,
            "n_frames": int(num(evp["n_frames"])),
            "span_start": int(evp["span_A_start"]), "span_end": int(evp["span_A_end"]),
            "span_ms": span_ms, "target_mean": f["target_post_mean"],
            "target_max_A": f["target_max_A"], "target_peak": f["target_peak_in_span"],
            "peak_frame": pk, "peak_time_ms": peak_time,
            "peak_rel_pos": f["peak_pos_rel"],
            "top1_phone": s["top1_phone"], "top1_post": s["top1_post"],
            "top5_phones": " ".join([s["top1_phone"], s["top2_phone"], s["top3_phone"],
                                     s["top4_phone"], s["top5_phone"]]),
            "competitor_phone": s["competitor_phone"], "competitor_peak": f["competitor_peak"],
            "peak_margin": f["peak_margin"], "blank_at_peak": f["blank_at_peak"],
            "blank_mean_span": f["blank_mean_span"], "blank_ge09_frac": f["blank_ge09_frac"],
            "neighbor_prev": round(prev_p, 5), "neighbor_next": round(next_p, 5),
            "cluster_width_D": int(num(f["cluster_width_D"])),
            "longest_run_05": int(num(f["longest_run_05"])),
            "support_count_05": int(num(f["support_count_05"])),
            "max_A_full": round(max_by_win.get("full", 0.0), 5),
            "max_A_raw": round(max_by_win.get("raw", 0.0), 5),
            "max_A_pad100": round(max_by_win.get("pad100", 0.0), 5),
            "max_A_pad250": round(max_by_win.get("pad250", 0.0), 5),
            "strong_windows": strong_windows,
            "identity_class": f["identity_class"],
            "production_decision": int(num(f["production_decision"])),
            "rule_A": p["candidate_A"], "rule_B": p["candidate_B"], "rule_C": p["candidate_C"],
            "rule_D": p["candidate_D"], "rule_E": p["candidate_E"], "rule_F": p["candidate_F"],
            "rule_G": p["candidate_G"], "rule_H": p["candidate_H"],
            "rule_final": p["final_candidate_decision"],
            "fals_A_strong": fals["A"], "fals_B_window_stable": fals["B"],
            "fals_C_neighbor_support": fals["C"], "fals_D_position": fals["D"],
            "fals_E_beats_competitor": fals["E"], "fals_F_not_blank": fals["F"],
            "fals_G_confident_label": fals["G"], "verdict": verdict,
            "verdict_reason": reason,
        })
    L.write_rows(OUT / "ENCODER_TYPE_B_CASES.csv", trace_rows, TRACE_FIELDS)

    # ---- phone confusion profile ----
    labeled = [r for r in feature.values() if r["truth"] != ""]
    prof = []
    by_phone = defaultdict(list)
    for r in labeled:
        by_phone[r["target_phone"]].append(r)
    for ph, rs in sorted(by_phone.items(), key=lambda kv: -len(kv[1])):
        comps = Counter(r["competitor_phone"] for r in rs if r["competitor_phone"])
        vals = sorted(num(r["target_max_A"]) for r in rs)
        med = vals[len(vals) // 2] if vals else 0.0
        cls = next((c for c, s in CLASSES.items() if ph in s), "other")
        n_p = sum(1 for r in rs if r["truth"] == "1")
        n_a = sum(1 for r in rs if r["truth"] == "0")
        prof.append({
            "target_phone": ph, "phone_class": cls, "n": len(rs),
            "n_present": n_p, "n_absent": n_a,
            "top_competitor": comps.most_common(1)[0][0] if comps else "",
            "top_competitor_n": comps.most_common(1)[0][1] if comps else 0,
            "second_competitor": comps.most_common(2)[1][0] if len(comps) > 1 else "",
            "second_competitor_n": comps.most_common(2)[1][1] if len(comps) > 1 else 0,
            "median_target_max_A": round(med, 5),
            "median_margin": round(st.median([num(r["identity_margin_mean"]) for r in rs]), 5),
            "median_blank": round(st.median([num(r["blank_mean_span"]) for r in rs]), 5),
            "one_frame_rate": round(sum(1 for r in rs
                                       if int(num(r["longest_run_05"])) <= 1) / len(rs), 4),
            "production_recall": round(sum(1 for r in rs if r["truth"] == "1"
                                           and int(num(r["production_decision"])) == 1) / n_p, 4)
            if n_p else None,
            "production_far": round(sum(1 for r in rs if r["truth"] == "0"
                                        and int(num(r["production_decision"])) == 1) / n_a, 4)
            if n_a else None,
        })
    L.write_rows(OUT / "PHONE_CONFUSION_PROFILE.csv", prof)

    # ---- child-speech representation stats ----
    def class_of(ph):
        for c, s in CLASSES.items():
            if ph in s:
                return c
        return "other"
    stats = {"by_class": {}, "weak_present": {}, "no_evidence_present": {}}
    all_labeled = labeled
    for c in list(CLASSES) + ["other"]:
        rs = [r for r in all_labeled if class_of(r["target_phone"]) == c]
        if not rs:
            continue
        stats["by_class"][c] = {
            "n": len(rs),
            "present": sum(1 for r in rs if r["truth"] == "1"),
            "absent": sum(1 for r in rs if r["truth"] == "0"),
            "median_max_A": round(st.median([num(r["target_max_A"]) for r in rs]), 5),
            "one_frame_rate": round(sum(1 for r in rs
                                       if int(num(r["longest_run_05"])) <= 1) / len(rs), 4),
            "median_longest_run": st.median([int(num(r["longest_run_05"])) for r in rs]),
            "median_blank": round(st.median([num(r["blank_mean_span"]) for r in rs]), 5),
            "median_margin": round(st.median([num(r["identity_margin_mean"]) for r in rs]), 5),
            "window_sensitive_rate": round(sum(1 for r in rs
                                              if r["identity_class"] not in
                                              ("IDENTITY_STABLE_PRESENT",
                                               "IDENTITY_STABLE_ABSENT")) / len(rs), 4),
        }
    weak = [r for r in all_labeled if r["truth"] == "1" and num(r["target_max_A"]) < 0.15]
    stats["weak_present"] = {
        "n": len(weak),
        "production_recall": round(sum(1 for r in weak
                                       if int(num(r["production_decision"])) == 1) / len(weak), 4)
        if weak else None,
        "median_max_A": round(st.median([num(r["target_max_A"]) for r in weak]), 5) if weak else None,
        "one_frame_rate": round(sum(1 for r in weak
                                    if int(num(r["longest_run_05"])) <= 1) / len(weak), 4)
        if weak else None,
    }
    ne = [r for r in all_labeled if r["truth"] == "1" and num(r["target_max_A"]) < 0.02]
    stats["no_evidence_present"] = {
        "n": len(ne),
        "tokens": [{"token_id": r["token_id"], "word": r["word"],
                    "target_phone": r["target_phone"], "human_label": r["human_label"],
                    "max_A": num(r["target_max_A"]), "peak_D": num(r["peak_D"]),
                    "production_decision": int(num(r["production_decision"]))} for r in ne],
    }
    (ART / "child_speech_stats.json").write_text(
        json.dumps(stats, indent=2, ensure_ascii=False), encoding="utf-8")

    print("TYPE-B verdicts:")
    for r in trace_rows:
        print(f"  {r['case_id']:16s} {r['target_phone']:3s} maxA={r['target_max_A']:>8s} "
              f"strongWin={r['strong_windows']} w={r['cluster_width_D']} "
              f"nbr=({r['neighbor_prev']},{r['neighbor_next']}) "
              f"fals={' '.join(f'{k}{v}' for k, v in [('A', r['fals_A_strong']), ('B', r['fals_B_window_stable']), ('C', r['fals_C_neighbor_support']), ('D', r['fals_D_position']), ('E', r['fals_E_beats_competitor']), ('F', r['fals_F_not_blank']), ('G', r['fals_G_confident_label'])])} "
              f"-> {r['verdict']}")
    print("confusion profile rows:", len(prof))
    print("child speech classes:", list(stats["by_class"].keys()))
    print("weak present:", stats["weak_present"])
    print("no-evidence present:", stats["no_evidence_present"]["n"],
          [t["token_id"] for t in stats["no_evidence_present"]["tokens"]])
    print("DONE run_encoder_audit")


if __name__ == "__main__":
    main()
