"""Scale SO762 calibration: ≥200 non-ceiling words, speaker-disjoint, variance-collapse checks.
No ASR score boost. Population: adult L2. NOT_4yo.
"""
from __future__ import annotations
import json, sys, math, hashlib, subprocess
from pathlib import Path
from collections import defaultdict, Counter

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
from Research.Speech.Phase1_2.Adapters.vad_silero import SileroVadAdapter
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

SO_META = REPO / "Research" / "Speech" / "Phase1_3" / "Speechocean762"
SO = Path(r"D:\speech-lab\data\speechocean762")
OUT = REPO / "Research" / "Speech" / "Phase1_5" / "Results"
TMP = Path(r"D:\speech-lab\data\so762_16k")
TMP.mkdir(parents=True, exist_ok=True)
OUT.mkdir(parents=True, exist_ok=True)


def pearson(xs, ys):
    n = len(xs)
    if n < 3:
        return None
    mx, my = sum(xs) / n, sum(ys) / n
    num = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    dx = math.sqrt(sum((x - mx) ** 2 for x in xs))
    dy = math.sqrt(sum((y - my) ** 2 for y in ys))
    if dx == 0 or dy == 0:
        return None
    return num / (dx * dy)


def spearman(xs, ys):
    def ranks(a):
        order = sorted(range(len(a)), key=lambda i: a[i])
        r = [0.0] * len(a)
        for rank, i in enumerate(order):
            r[i] = float(rank + 1)
        return r
    return pearson(ranks(xs), ranks(ys))


def mae(xs, ys):
    return sum(abs(x - y) for x, y in zip(xs, ys)) / max(1, len(xs))


def rmse(xs, ys):
    return math.sqrt(sum((x - y) ** 2 for x, y in zip(xs, ys)) / max(1, len(xs)))


def stdev(xs):
    if len(xs) < 2:
        return 0.0
    m = sum(xs) / len(xs)
    return math.sqrt(sum((x - m) ** 2 for x in xs) / (len(xs) - 1))


def fit_affine(xs, ys):
    n = len(xs)
    mx, my = sum(xs) / n, sum(ys) / n
    varx = sum((x - mx) ** 2 for x in xs)
    if varx < 1e-12:
        return 0.0, my
    a = sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / varx
    return a, my - a * mx


def apply_affine(xs, a, b):
    return [max(0.0, min(100.0, a * x + b)) for x in xs]


def isotonic(train_x, train_y, test_x):
    bins = defaultdict(list)
    for x, y in zip(train_x, train_y):
        bins[int(round(x))].append(y)
    keys = sorted(bins)
    means = [sum(bins[k]) / len(bins[k]) for k in keys]
    for i in range(1, len(means)):
        if means[i] < means[i - 1]:
            means[i] = means[i - 1]
    out = []
    for x in test_x:
        if not keys:
            out.append(0.0)
            continue
        k = min(keys, key=lambda kk: abs(kk - x))
        out.append(means[keys.index(k)])
    return out


def metrics(xs, ys, label):
    ps, hs = stdev(xs), stdev(ys)
    return {
        "label": label, "n": len(xs),
        "pearson": pearson(xs, ys), "spearman": spearman(xs, ys),
        "mae": mae(xs, ys) if xs else None, "rmse": rmse(xs, ys) if xs else None,
        "mean_pred": sum(xs) / len(xs) if xs else None,
        "mean_human": sum(ys) / len(ys) if ys else None,
        "std_pred": ps, "std_human": hs,
        "var_ratio": (ps / hs) if hs > 1e-9 else None,
        "pred_range": [min(xs), max(xs)] if xs else None,
        "human_range": [min(ys), max(ys)] if ys else None,
        "bias": (sum(xs) / len(xs) - sum(ys) / len(ys)) if xs else None,
        "VARIANCE_COLLAPSE": bool(hs > 5 and ps < 0.25 * hs),
    }


def ensure_16k(src, dst):
    if Path(dst).exists():
        return str(dst)
    subprocess.run(
        ["ffmpeg", "-y", "-v", "error", "-i", str(src), "-ar", "16000", "-ac", "1", str(dst)],
        check=True)
    return str(dst)


def main(target_nonceil=200, max_utts=120):
    words = json.loads((SO_META / "word_index.json").read_text(encoding="utf-8"))
    utt_split = json.loads((SO_META / "utt_split.json").read_text(encoding="utf-8"))

    # distribution analysis
    all_acc = [float(w["human_acc_0_10"]) for w in words if w["human_acc_0_10"] is not None]
    nonceil = [a for a in all_acc if a < 10]
    hard = [a for a in all_acc if a < 8]
    severe = [a for a in all_acc if a < 7]
    dist = {
        "n_words": len(all_acc),
        "mean": sum(all_acc) / len(all_acc),
        "std": stdev(all_acc),
        "median": sorted(all_acc)[len(all_acc) // 2],
        "min": min(all_acc), "max": max(all_acc),
        "n_nonceil_lt10": len(nonceil),
        "n_hard_lt8": len(hard),
        "n_severe_lt7": len(severe),
        "pct_perfect10": sum(1 for a in all_acc if a >= 10) / len(all_acc),
        "NOT_4yo": True, "population": "adult-L2-speechocean762",
    }
    (OUT / "so762_human_distribution.json").write_text(json.dumps(dist, indent=2), encoding="utf-8")
    print("DIST", dist, flush=True)

    # group words by uid
    by_uid = defaultdict(list)
    for w in words:
        by_uid[w["uid"]].append(w)

    # select utts prioritizing non-ceiling words, stratified by split
    selected = {"train": [], "valid": [], "test": []}
    nonceil_count = {"train": 0, "valid": 0, "test": 0}
    # sort utts by number of error words desc
    utt_rank = []
    for uid, ws in by_uid.items():
        sp = utt_split.get(uid) or ws[0].get("split")
        if sp not in selected:
            continue
        n_err = sum(1 for w in ws if float(w["human_acc_0_10"] or 10) < 10)
        utt_rank.append((n_err, sp, uid))
    utt_rank.sort(reverse=True)

    # aim ~target_nonceil error words total; cap utts per split for CPU budget
    per_split_cap = max(15, max_utts // 3)
    for n_err, sp, uid in utt_rank:
        if len(selected[sp]) >= per_split_cap:
            continue
        if n_err <= 0 and nonceil_count[sp] >= target_nonceil // 3:
            continue
        selected[sp].append(uid)
        nonceil_count[sp] += max(0, n_err)
        if sum(nonceil_count.values()) >= int(target_nonceil * 1.3) and all(
                len(selected[s]) >= 12 for s in selected):
            break

    # verify speaker leakage
    spk_sets = {s: set() for s in selected}
    for sp, uids in selected.items():
        for uid in uids:
            spk_sets[sp].add(by_uid[uid][0]["speaker"])
    leak = (spk_sets["train"] & spk_sets["valid"]) | (spk_sets["train"] & spk_sets["test"]) | (spk_sets["valid"] & spk_sets["test"])
    assert not leak, f"SPEAKER_LEAKAGE {leak}"
    print("SELECTED utts", {k: len(v) for k, v in selected.items()}, "nonceil_est", nonceil_count, "spk", {k: len(v) for k, v in spk_sets.items()}, flush=True)

    tgt_ad = CmuDictTargetAdapter()
    vad_ad = SileroVadAdapter()
    pev = PhoneEvidenceV2()

    rows = []
    processed = 0
    for sp in ("train", "valid", "test"):
        for uid in selected[sp]:
            ws = sorted(by_uid[uid], key=lambda x: x["word_i"])
            wav = Path(ws[0]["wav"])
            if not wav.exists():
                continue
            try:
                path = ensure_16k(wav, TMP / f"{uid}.wav")
            except Exception as e:
                print("FF", uid, e, flush=True)
                continue
            text = ws[0]["utt_text"]
            try:
                st = tgt_ad.build(text)
                vad = vad_ad.run(path)
                soft = pev.soft_match(path, st.arpabet)
            except Exception as e:
                print("PIPE", uid, type(e).__name__, str(e)[:80], flush=True)
                continue
            soft_utt = soft.soft_score_0_100 if vad.speech_detected else 0.0
            soft_conf = soft.confidence_0_1 if vad.speech_detected else 0.0
            hit_sims = [100.0 * h.sim for h in soft.hits]
            lengths = [len(w["phones"]) if isinstance(w["phones"], list) else 1 for w in ws]
            idx = 0
            for w, L in zip(ws, lengths):
                if hit_sims and idx < len(hit_sims):
                    chunk = hit_sims[idx:idx + max(1, L)]
                    idx += max(1, L)
                    ai = sum(chunk) / len(chunk) if chunk else soft_utt
                else:
                    ai = soft_utt
                # phone-level human if present
                ph_acc = w.get("phones_accuracy")
                rows.append({
                    "uid": uid, "split": sp, "speaker": w["speaker"],
                    "word": w["text"], "word_i": w["word_i"],
                    "human_acc_0_10": w["human_acc_0_10"],
                    "human_x10": float(w["human_acc_0_10"]) * 10.0,
                    "soft": round(float(ai), 2),
                    "soft_utt": soft_utt,
                    "soft_conf": soft_conf,
                    "vad": vad.speech_detected,
                    "phones_accuracy": ph_acc,
                    "n_phones": L,
                })
            processed += 1
            if processed % 10 == 0:
                nc = sum(1 for r in rows if float(r["human_acc_0_10"]) < 10)
                print(f"progress utts={processed} words={len(rows)} nonceil={nc}", flush=True)

    (OUT / "so762_scale_rows.json").write_text(json.dumps(rows, indent=2), encoding="utf-8")
    nc_total = sum(1 for r in rows if float(r["human_acc_0_10"]) < 10)
    print(f"TOTAL words={len(rows)} nonceil={nc_total}", flush=True)

    def subset(rs, kind):
        if kind == "full":
            return rs
        if kind == "nonceil":
            return [r for r in rs if float(r["human_acc_0_10"]) < 10]
        if kind == "moderate":
            return [r for r in rs if float(r["human_acc_0_10"]) < 9]
        if kind == "hard":
            return [r for r in rs if float(r["human_acc_0_10"]) < 8]
        if kind == "severe":
            return [r for r in rs if float(r["human_acc_0_10"]) < 7]
        return rs

    report = {
        "population": "adult-L2-speechocean762", "NOT_4yo": True,
        "human_dist": dist,
        "n_words_scored": len(rows),
        "n_nonceil_scored": nc_total,
        "selected_utts": {k: len(v) for k, v in selected.items()},
        "speakers": {k: len(v) for k, v in spk_sets.items()},
        "speaker_leakage": [],
        "methods": {},
    }

    train = [r for r in rows if r["split"] == "train"]
    test = [r for r in rows if r["split"] == "test"]
    valid = [r for r in rows if r["split"] == "valid"]

    # raw soft on all subsets
    for split_name, rs in [("train", train), ("valid", valid), ("test", test)]:
        report["methods"][f"raw_soft_{split_name}"] = {}
        for kind in ("full", "nonceil", "moderate", "hard", "severe"):
            sub = subset(rs, kind)
            if len(sub) < 3:
                report["methods"][f"raw_soft_{split_name}"][kind] = {"n": len(sub), "note": "too_small"}
                continue
            report["methods"][f"raw_soft_{split_name}"][kind] = metrics(
                [r["soft"] for r in sub], [r["human_x10"] for r in sub], f"raw_{split_name}_{kind}")

    # calibrators fit on train nonceil
    tr_nc = subset(train, "nonceil")
    if len(tr_nc) >= 20:
        a, b = fit_affine([r["soft"] for r in tr_nc], [r["human_x10"] for r in tr_nc])
        report["affine_params_nonceil"] = {"a": a, "b": b}
        for kind in ("full", "nonceil", "hard", "severe"):
            te = subset(test, kind)
            if len(te) < 3:
                continue
            xs = apply_affine([r["soft"] for r in te], a, b)
            ys = [r["human_x10"] for r in te]
            m = metrics(xs, ys, f"affine_test_{kind}")
            report["methods"][f"affine_test_{kind}"] = m

        # conf-weighted soft: soft * (0.5+0.5*conf)
        def cx(r):
            return r["soft"] * (0.5 + 0.5 * r["soft_conf"])
        a2, b2 = fit_affine([cx(r) for r in tr_nc], [r["human_x10"] for r in tr_nc])
        report["conf_affine_params"] = {"a": a2, "b": b2}
        for kind in ("full", "nonceil", "hard"):
            te = subset(test, kind)
            if len(te) < 3:
                continue
            xs = apply_affine([cx(r) for r in te], a2, b2)
            ys = [r["human_x10"] for r in te]
            report["methods"][f"conf_affine_test_{kind}"] = metrics(xs, ys, f"confaff_{kind}")

        # isotonic on nonceil train -> test nonceil/hard
        for kind in ("nonceil", "hard", "full"):
            te = subset(test, kind)
            if len(te) < 3:
                continue
            xs = isotonic([r["soft"] for r in tr_nc], [r["human_x10"] for r in tr_nc], [r["soft"] for r in te])
            ys = [r["human_x10"] for r in te]
            report["methods"][f"isotonic_test_{kind}"] = metrics(xs, ys, f"iso_{kind}")
    else:
        report["affine_params_nonceil"] = {"note": f"train_nonceil too small n={len(tr_nc)}"}

    # confidence bins on test full
    bins = defaultdict(list)
    within = defaultdict(lambda: {"n": 0, "w5": 0, "w10": 0, "w15": 0})
    for r in test:
        b = int(min(9, max(0, r["soft_conf"] * 10)))
        key = f"{b/10:.1f}-{(b+1)/10:.1f}"
        err = abs(r["soft"] - r["human_x10"])
        bins[key].append(err)
        within[key]["n"] += 1
        within[key]["w5"] += int(err <= 5)
        within[key]["w10"] += int(err <= 10)
        within[key]["w15"] += int(err <= 15)
    report["confidence_bins"] = {
        k: {
            "n": within[k]["n"],
            "mae": sum(bins[k]) / len(bins[k]),
            "within_5": within[k]["w5"] / within[k]["n"],
            "within_10": within[k]["w10"] / within[k]["n"],
            "within_15": within[k]["w15"] / within[k]["n"],
        }
        for k in sorted(bins)
    }

    # phoneme-level human phones_accuracy vs soft (utterance-level proxy limited)
    # per-word mean phones_accuracy * 50 -> 0-100 if scale 0-2
    ph_rows = []
    for r in rows:
        pa = r.get("phones_accuracy")
        if not pa or not isinstance(pa, list):
            continue
        try:
            mean_pa = sum(float(x) for x in pa) / len(pa)  # 0-2
        except Exception:
            continue
        ph_rows.append({"soft": r["soft"], "human_phone_x50": mean_pa * 50.0, "split": r["split"]})
    if len(ph_rows) >= 10:
        te = [r for r in ph_rows if r["split"] == "test"]
        if len(te) >= 5:
            report["phoneme_proxy_test"] = metrics(
                [r["soft"] for r in te], [r["human_phone_x50"] for r in te], "soft_vs_mean_phones_acc")

    (OUT / "so762_scale_calibration.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("WROTE calibration report", flush=True)
    # print key test metrics
    for k, v in report["methods"].items():
        if "test" in k and isinstance(v, dict):
            if "n" in v:
                print(k, "n=", v.get("n"), "P=", v.get("pearson"), "S=", v.get("spearman"),
                      "MAE=", v.get("mae"), "std_p=", v.get("std_pred"), "VC=", v.get("VARIANCE_COLLAPSE"), flush=True)
            else:
                for kk, vv in v.items():
                    if isinstance(vv, dict) and "n" in vv:
                        print(k, kk, "n=", vv.get("n"), "P=", vv.get("pearson"), "MAE=", vv.get("mae"),
                              "std_p=", vv.get("std_pred"), "VC=", vv.get("VARIANCE_COLLAPSE"), flush=True)


if __name__ == "__main__":
    t = int(sys.argv[1]) if len(sys.argv) > 1 else 200
    main(target_nonceil=t)
