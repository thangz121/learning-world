"""WP-1.9.26 Parts 3/26 — large blinded review corpus (diagnostic + balanced + random control).

Selects 250-300 candidates from existing local research data (LWE + SO762 + SIAK
age-4-6 broad pool), with machine evidence for the LWE/SO762 candidates. No labels
are created; every candidate has an explicit inclusion reason. Deterministic
sampling (seed 1926). No leakage: the 28 historical LWE labels are excluded from
the new-label pools and re-included only as 10 hidden consistency controls.
"""
from __future__ import annotations

import csv
import json
import random
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_26"
LAB = OUT / "01_HUMAN_LABEL_ACQUISITION"
ART = OUT / "artifacts"
P21 = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"
P23 = L.REPO / "Research/Speech/Phase1_9_23"
P24 = L.REPO / "Research/Speech/Phase1_9_24"
SIAK = L.REPO / "Research/Speech/ExternalData/SIAK"
PRIMARY = "full"
SEED = 1926

CLASSES = {
    "stop": {"t", "k", "p", "d", "b", "ɡ"},
    "fricative": {"s", "z", "f", "v", "θ", "ð", "ʃ", "ʒ"},
    "nasal": {"n", "m", "ŋ"},
    "liquid": {"ɹ", "l"},
    "affricate": {"tʃ", "dʒ"},
    "vowel": set(L.VOWELS),
}

FIELDS = [
    "blind_id", "token_id", "corpus", "speaker_id", "word", "target_phone",
    "phone_class", "audio_reference", "purpose", "pool", "selection_reason",
    "review_order", "machine_rank", "machine_max_A", "machine_mean_A",
    "machine_peak_width", "machine_margin", "machine_blank", "production_decision",
    "alt_max_A", "historical_label", "historical_confidence", "reviewer_visible",
    "hidden_fields",
]


def num(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def class_of(ph):
    for c, s in CLASSES.items():
        if ph in s:
            return c
    return "other"


def main():
    feature = {r["token_id"]: r for r in csv.DictReader(
        open(P23 / "artifacts/feature_matrix.csv", encoding="utf-8"))}
    ev = {}
    for c in ("lwe", "so762_dev", "so762_test", "so762_absent_dev"):
        for r in csv.DictReader(open(P21 / f"token_evidence_{c}.csv", encoding="utf-8")):
            if r["window_type"] == PRIMARY:
                ev[r["token_id"]] = r
    alt = {}
    alt_path = ART / "alt_encoder_all.csv"
    if alt_path.exists():
        for r in csv.DictReader(open(alt_path, encoding="utf-8")):
            alt[r["token_id"]] = num(r["alt_max_A"])

    hist = {}
    for r in csv.DictReader(open(L.REPO / "Research/Speech/Phase1_9_12/Results/"
                                 "final_consonant_human_review.csv", encoding="utf-8-sig")):
        hist[r["case_id"].replace("fc_", "")] = r

    rng = random.Random(SEED)
    selected = {}
    manifest = []

    def add(tid, pool, purpose, reason):
        if tid in selected or tid not in feature:
            return
        f = feature[tid]
        e = ev.get(tid)
        if e is None:
            return
        h = hist.get(tid)
        selected[tid] = {
            "token_id": tid, "corpus": f["corpus"], "speaker_id": f["speaker_id"],
            "word": f["word"], "target_phone": f["target_phone"],
            "phone_class": class_of(f["target_phone"]),
            "audio_reference": audio_ref(f), "purpose": purpose, "pool": pool,
            "selection_reason": reason,
            "machine_rank": f["target_rank_in_top5"],
            "machine_max_A": f["target_max_A"], "machine_mean_A": f["target_post_mean"],
            "machine_peak_width": f["cluster_width_D"],
            "machine_margin": f["identity_margin_mean"],
            "machine_blank": f["blank_mean_span"],
            "production_decision": f["production_decision"],
            "alt_max_A": round(alt[tid], 6) if tid in alt else "",
            "historical_label": h["human_final_label"] if h else "",
            "historical_confidence": h["reviewer_confidence"] if h else "",
        }

    labeled = [r for r in feature.values() if r["truth"] != ""]
    unlabeled = [r for r in feature.values() if r["truth"] == ""
                 and r["is_final_consonant"] == "1"]

    # A-C /r/ pools
    for r in labeled + unlabeled:
        if r["target_phone"] != "ɹ":
            continue
        tid = r["token_id"]
        h = hist.get(tid)
        if h and h["human_final_label"].endswith("PRESENT"):
            add(tid, "A_r_present", "diagnostic", "labelled /r/ PRESENT (consistency control)")
        elif h and h["human_final_label"].endswith("ABSENT"):
            add(tid, "B_r_absent", "diagnostic", "labelled /r/ ABSENT (consistency control)")
        elif r["truth"] == "1":
            add(tid, "A_r_present", "diagnostic", "so762 /r/ score-present")
        elif r["truth"] == "0":
            add(tid, "B_r_absent", "diagnostic", "so762 /r/ score-absent")
        else:
            add(tid, "C_r_uncertain", "diagnostic", "unlabelled /r/ (word verdict negative)")

    # F. TYPE-B / strong false-evidence
    for tid in ("child_07_seven", "014180143_15", "014190172_7", "014350146_16",
                "014470150_3"):
        add(tid, "F_typeb_strong_false", "diagnostic",
            "TYPE-B / strong false-evidence candidate")

    # J. high-score false accepts
    for r in labeled:
        if r["truth"] == "0" and r["production_decision"] == "1":
            add(r["token_id"], "J_high_score_false_accept", "diagnostic",
                "production-accepted absent (high-score false accept)")

    # D/E. weak present/absent (stratified sample)
    def stratified_sample(rows, n, pool, reason):
        by = defaultdict(list)
        for r in rows:
            by[class_of(r["target_phone"])].append(r)
        picked, keys = [], sorted(by)
        i = 0
        while len(picked) < n and any(by.values()):
            k = keys[i % len(keys)]
            if by[k]:
                picked.append(by[k].pop(rng.randrange(len(by[k]))))
            i += 1
        for r in picked:
            add(r["token_id"], pool, "diagnostic", reason)

    weak_present = [r for r in labeled if r["truth"] == "1"
                    and num(r["target_max_A"]) < 0.15]
    weak_absent = [r for r in labeled if r["truth"] == "0"
                   and num(r["target_max_A"]) < 0.15]
    low_true = [r for r in labeled if r["truth"] == "1"
                and num(r["target_max_A"]) < 0.02]
    final_fail = [r for r in labeled if r["truth"] == "1"
                  and r["production_decision"] == "0"]
    isolated = [r for r in labeled if int(num(r["cluster_width_D"], 1)) <= 1
                and num(r["target_max_A"]) >= 0.30]
    disagree = [r for r in labeled if r["token_id"] in alt
                and abs(num(r["target_max_A"]) - alt[r["token_id"]]) >= 0.30]
    stratified_sample(weak_present, 30, "D_weak_present", "weak PRESENT (max_A<0.15)")
    stratified_sample(weak_absent, 15, "E_weak_absent", "weak ABSENT (max_A<0.15)")
    stratified_sample(low_true, 15, "L_low_score_true_present",
                      "true PRESENT with max_A<0.02")
    stratified_sample(final_fail, 15, "I_final_consonant_failure",
                      "true PRESENT production miss")
    stratified_sample(isolated, 20, "G_isolated_peak",
                      "isolated one-frame peak with max_A>=0.30")
    if disagree:
        stratified_sample(disagree, 20, "H_encoder_disagreement",
                          "primary vs alternative encoder |delta|>=0.30")

    # L. balanced review set (phone class x truth)
    bal = defaultdict(list)
    for r in labeled:
        if num(r["target_max_A"]) >= 0.15:
            bal[(class_of(r["target_phone"]), r["truth"])].append(r)
    for k in sorted(bal):
        rng.shuffle(bal[k])
    i = 0
    keys = sorted(bal)
    while len([t for t in selected if selected[t]["pool"] == "K_balanced_review"]) < 60:
        k = keys[i % len(keys)]
        if bal[k]:
            r = bal[k].pop()
            add(r["token_id"], "K_balanced_review", "balanced",
                f"balanced review ({k[0]}, truth={k[1]})")
        i += 1
        if i > 2000:
            break

    # M. random control
    pool = [r for r in labeled if r["token_id"] not in selected]
    rng.shuffle(pool)
    for r in pool[:60]:
        add(r["token_id"], "M_random_control", "random_control",
            "random control (speaker-disjoint coverage)")

    # N. consistency controls (10 historical labels)
    hist_rows = [r for r in labeled if r["token_id"] in hist]
    rng.shuffle(hist_rows)
    n_cons = 0
    for r in hist_rows:
        if n_cons >= 10:
            break
        add(r["token_id"], "N_consistency_control", "consistency_control",
            "historical listening label re-inserted blind (consistency check)")
        n_cons += 1

    # O. SIAK age 4-6 broad pool (no machine evidence)
    siak_rows = siak_candidates(30, rng)
    for s in siak_rows:
        tid = s["token_id"]
        if tid in selected:
            continue
        selected[tid] = {**s, "machine_rank": "", "machine_max_A": "",
                         "machine_mean_A": "", "machine_peak_width": "",
                         "machine_margin": "", "machine_blank": "",
                         "production_decision": "", "alt_max_A": "",
                         "historical_label": "", "historical_confidence": ""}

    # finalize order + blind ids
    rows = list(selected.values())
    rng.shuffle(rows)
    for i, r in enumerate(rows, 1):
        r["blind_id"] = f"R{i:03d}"
        r["review_order"] = i
        r["reviewer_visible"] = f"{r['blind_id']}; {r['word']}; {r['target_phone']}"
        r["hidden_fields"] = ("machine_rank; machine_max_A; machine_mean_A; "
                              "machine_peak_width; machine_margin; machine_blank; "
                              "production_decision; alt_max_A; historical_label; "
                              "historical_confidence; pool; purpose")
    rows.sort(key=lambda r: r["review_order"])
    L.write_rows(LAB / "REVIEW_CANDIDATES.csv", rows, FIELDS)
    diag = [r for r in rows if r["purpose"] == "diagnostic"]
    bal_rows = [r for r in rows if r["purpose"] == "balanced"]
    ctrl = [r for r in rows if r["purpose"] in ("random_control", "broad_pool")]
    L.write_rows(LAB / "REVIEW_DIAGNOSTIC.csv", diag, FIELDS)
    L.write_rows(LAB / "REVIEW_RANDOM_CONTROL.csv", ctrl, FIELDS)
    L.write_rows(LAB / "REVIEW_BALANCED.csv", bal_rows, FIELDS)
    manifest = [{"pool": k, "n": v} for k, v in sorted(
        Counter(r["pool"] for r in rows).items())]
    L.write_rows(LAB / "REVIEW_PACK_MANIFEST.csv", manifest,
                 ["pool", "n"])
    print("pack size:", len(rows))
    for m in manifest:
        print(" ", m["pool"], m["n"])
    print("speakers:", len({r["speaker_id"] for r in rows}),
          "| corpora:", dict(Counter(r["corpus"] for r in rows)))
    print("DONE build_review_pack")


def audio_ref(f):
    tid = f["token_id"]
    if f["corpus"] == "lwe":
        src = L.find_source(f["speaker_id"], f["word"])
        return str(src) if src else ""
    utt = tid.rsplit("_", 1)[0]
    return str(L.SO / "WAVE" / f"SPEAKER{int(f['speaker_id']):04d}" / f"{utt}.WAV")


_SIAK_INDEX = None


def siak_candidates(n, rng):
    global _SIAK_INDEX
    if _SIAK_INDEX is None:
        _SIAK_INDEX = {}
        for p in (SIAK / "flac").rglob("*.flac"):
            _SIAK_INDEX[p.name] = p
    from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
    tgt = CmuDictTargetAdapter()
    inv = L.PhoneEvidenceV2().inv
    rows = []
    for split in ("train.csv", "test.csv"):
        fp = SIAK / split
        if not fp.exists():
            continue
        with open(fp, encoding="utf-8") as f:
            for r in csv.DictReader(f):
                m = re.match(r"^(train\d+|test\d+)([a-z]+)(\d+)_\d+_t\d+_(.+)\.flac$",
                             r["file"])
                if not m:
                    continue
                spk, l1, age, utt = m.groups()
                if age not in ("04", "05", "06") or " " in utt or "-" in utt:
                    continue
                st = tgt.build(utt)
                if st.warnings:
                    continue
                target = inv.arpa_seq_to_canon([str(x) for x in st.arpabet])
                if not target or target[-1] in L.VOWELS:
                    continue
                p = _SIAK_INDEX.get(r["file"])
                if p is None:
                    continue
                rows.append({"token_id": f"siak_{spk}_{r['file'][:-5]}", "corpus": "siak",
                             "speaker_id": spk, "word": utt, "target_phone": target[-1],
                             "phone_class": class_of(target[-1]), "audio_reference": str(p),
                             "purpose": "broad_pool", "pool": "O_siak_age46_broad",
                             "selection_reason": f"SIAK age {age} single-word final "
                                                  f"consonant (no machine evidence; "
                                                  f"age-4-6 slice has only ~5 speakers)"})
    rng.shuffle(rows)
    seen, out = Counter(), []
    for r in rows:
        if seen[r["speaker_id"]] >= 8:
            continue
        seen[r["speaker_id"]] += 1
        out.append(r)
        if len(out) >= n:
            break
    return out


if __name__ == "__main__":
    main()
