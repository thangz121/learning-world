"""SO762 Phase 1.6: ≥30 test speakers, word-level CTC alignment scoring,
confidence = P(|err|<=10), conflict policy comparison, variance checks.
"""
from __future__ import annotations
import json, sys, math, subprocess
from pathlib import Path
from collections import defaultdict

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.Adapters.vad_silero import SileroVadAdapter
from Research.Speech.Phase1_6.Alignment.word_aligner import WordAlignerV1

SO_META = REPO / "Research" / "Speech" / "Phase1_3" / "Speechocean762"
OUT = REPO / "Research" / "Speech" / "Phase1_6" / "Results"
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


def stdev(xs):
    if len(xs) < 2:
        return 0.0
    m = sum(xs) / len(xs)
    return math.sqrt(sum((x - m) ** 2 for x in xs) / (len(xs) - 1))


def metrics(xs, ys, label):
    ps, hs = stdev(xs), stdev(ys)
    return {
        "label": label, "n": len(xs),
        "pearson": pearson(xs, ys), "spearman": spearman(xs, ys),
        "mae": mae(xs, ys) if xs else None,
        "mean_pred": sum(xs) / len(xs) if xs else None,
        "mean_human": sum(ys) / len(ys) if ys else None,
        "std_pred": ps, "std_human": hs,
        "var_ratio": (ps / hs) if hs > 1e-9 else None,
        "VARIANCE_COLLAPSE": bool(hs > 5 and ps < 0.25 * hs),
        "p_err_le_5": sum(1 for x, y in zip(xs, ys) if abs(x - y) <= 5) / max(1, len(xs)),
        "p_err_le_10": sum(1 for x, y in zip(xs, ys) if abs(x - y) <= 10) / max(1, len(xs)),
        "p_err_le_15": sum(1 for x, y in zip(xs, ys) if abs(x - y) <= 15) / max(1, len(xs)),
    }


def ensure_16k(src, dst):
    if Path(dst).exists():
        return str(dst)
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-i", str(src), "-ar", "16000", "-ac", "1", str(dst)], check=True)
    return str(dst)


def fit_affine(xs, ys):
    n = len(xs)
    mx, my = sum(xs) / n, sum(ys) / n
    varx = sum((x - mx) ** 2 for x in xs)
    if varx < 1e-12:
        return 0.0, my
    a = sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / varx
    return a, my - a * mx


def main(min_test_speakers=30, max_utts_per_split=80):
    words = json.loads((SO_META / "word_index.json").read_text(encoding="utf-8"))
    utt_split = json.loads((SO_META / "utt_split.json").read_text(encoding="utf-8"))
    by_uid = defaultdict(list)
    for w in words:
        by_uid[w["uid"]].append(w)

    # rebuild speaker-disjoint split with MORE test speakers if needed
    # Use Phase1.3 split but expand test by taking speakers from valid if test < 30
    spk_to_split = {}
    for uid, ws in by_uid.items():
        sp = utt_split.get(uid, ws[0]["split"])
        spk = ws[0]["speaker"]
        # first assignment wins
        if spk not in spk_to_split:
            spk_to_split[spk] = sp

    # count speakers per split
    from collections import Counter
    c = Counter(spk_to_split.values())
    print("orig_spk", dict(c), flush=True)

    # If test speakers < 30, move some from train/valid to test by hash for determinism
    test_spks = [s for s, sp in spk_to_split.items() if sp == "test"]
    other = [s for s, sp in spk_to_split.items() if sp != "test"]
    other_sorted = sorted(other)
    import hashlib
    # take additional speakers into test until 30
    i = 0
    while len(test_spks) < min_test_speakers and i < len(other_sorted):
        s = other_sorted[i]
        # only move if enough remain in train
        train_left = sum(1 for sp in spk_to_split.values() if sp == "train")
        if spk_to_split[s] == "train" and train_left <= 40:
            i += 1
            continue
        spk_to_split[s] = "test"
        test_spks.append(s)
        i += 1

    # remap utt split from speaker
    new_utt_split = {}
    for uid, ws in by_uid.items():
        new_utt_split[uid] = spk_to_split[ws[0]["speaker"]]

    # verify leakage
    sets = defaultdict(set)
    for spk, sp in spk_to_split.items():
        sets[sp].add(spk)
    leak = (sets["train"] & sets["valid"]) | (sets["train"] & sets["test"]) | (sets["valid"] & sets["test"])
    assert not leak
    print("spk_counts", {k: len(v) for k, v in sets.items()}, "test_spk", len(sets["test"]), flush=True)
    (OUT / "utt_split_p16.json").write_text(json.dumps(new_utt_split), encoding="utf-8")
    (OUT / "speaker_split_p16.json").write_text(json.dumps({k: sorted(v) for k, v in sets.items()}, indent=2), encoding="utf-8")

    # select utts prioritizing error words, cap per split for CPU
    utt_rank = []
    for uid, ws in by_uid.items():
        sp = new_utt_split[uid]
        n_err = sum(1 for w in ws if float(w["human_acc_0_10"] or 10) < 10)
        utt_rank.append((n_err, sp, uid))
    utt_rank.sort(reverse=True)
    selected = {"train": [], "valid": [], "test": []}
    for n_err, sp, uid in utt_rank:
        if len(selected[sp]) >= max_utts_per_split:
            continue
        selected[sp].append(uid)

    print("selected_utts", {k: len(v) for k, v in selected.items()}, flush=True)

    aligner = WordAlignerV1()
    vad = SileroVadAdapter()
    rows = []
    processed = 0
    for sp in ("train", "valid", "test"):
        for uid in selected[sp]:
            ws = sorted(by_uid[uid], key=lambda x: x["word_i"])
            wav = Path(ws[0]["wav"])
            if not wav.exists():
                continue
            dst = TMP / f"{uid}.wav"
            try:
                path = ensure_16k(wav, dst)
            except Exception:
                continue
            word_texts = [w["text"] for w in ws]
            try:
                v = vad.run(path)
                spans, meta = aligner.align_words(path, word_texts)
            except Exception as e:
                print("FAIL", uid, type(e).__name__, str(e)[:60], flush=True)
                continue
            if not v.speech_detected:
                for w in ws:
                    rows.append({
                        "uid": uid, "split": sp, "speaker": w["speaker"],
                        "word": w["text"], "human_acc_0_10": w["human_acc_0_10"],
                        "human_x10": float(w["human_acc_0_10"]) * 10.0,
                        "soft": 0.0, "soft_conf": 0.0, "vad": False,
                        "t0": None, "t1": None, "method": meta.get("method"),
                    })
            else:
                for w, spn in zip(ws, spans):
                    rows.append({
                        "uid": uid, "split": sp, "speaker": w["speaker"],
                        "word": w["text"], "human_acc_0_10": w["human_acc_0_10"],
                        "human_x10": float(w["human_acc_0_10"]) * 10.0,
                        "soft": spn.soft_score, "soft_conf": spn.soft_conf,
                        "vad": True, "t0": spn.start_s, "t1": spn.end_s,
                        "method": meta.get("method"),
                        "n_phones": len(spn.canon),
                    })
            processed += 1
            if processed % 15 == 0:
                print(f"progress {processed} words={len(rows)}", flush=True)

    (OUT / "so762_word_aligned_rows.json").write_text(json.dumps(rows, indent=2), encoding="utf-8")
    nc = sum(1 for r in rows if float(r["human_acc_0_10"]) < 10)
    print(f"TOTAL words={len(rows)} nonceil={nc} method=word_ctc_forced_v1", flush=True)

    def sub(rs, kind):
        if kind == "full":
            return rs
        if kind == "nonceil":
            return [r for r in rs if float(r["human_acc_0_10"]) < 10]
        if kind == "hard":
            return [r for r in rs if float(r["human_acc_0_10"]) < 8]
        return rs

    report = {
        "population": "adult-L2-speechocean762", "NOT_4yo": True,
        "alignment_method": "word_ctc_forced_v1",
        "speakers": {k: len(v) for k, v in sets.items()},
        "selected_utts": {k: len(v) for k, v in selected.items()},
        "n_words": len(rows), "n_nonceil": nc,
        "speaker_leakage": [],
        "metrics": {},
    }

    train = [r for r in rows if r["split"] == "train"]
    test = [r for r in rows if r["split"] == "test"]
    valid = [r for r in rows if r["split"] == "valid"]

    for split_name, rs in [("train", train), ("valid", valid), ("test", test)]:
        report["metrics"][split_name] = {}
        for kind in ("full", "nonceil", "hard"):
            s = sub(rs, kind)
            if len(s) < 5:
                report["metrics"][split_name][kind] = {"n": len(s)}
                continue
            report["metrics"][split_name][kind] = metrics(
                [r["soft"] for r in s], [r["human_x10"] for r in s], f"{split_name}_{kind}")

    # confidence reliability: P(|err|<=10) by conf bin on TEST
    bins = defaultdict(list)
    for r in test:
        b = int(min(9, max(0, float(r["soft_conf"]) * 10)))
        key = f"{b/10:.1f}-{(b+1)/10:.1f}"
        err = abs(r["soft"] - r["human_x10"])
        bins[key].append(err)
    conf_table = {}
    for k, errs in sorted(bins.items()):
        n = len(errs)
        conf_table[k] = {
            "n": n,
            "mae": sum(errs) / n,
            "p_err_le_5": sum(1 for e in errs if e <= 5) / n,
            "p_err_le_10": sum(1 for e in errs if e <= 10) / n,
            "p_err_le_15": sum(1 for e in errs if e <= 15) / n,
        }
    report["confidence_reliability_test"] = conf_table

    # isotonic conf -> empirical p(|err|<=10)
    # train on train split: conf -> indicator
    tr_pairs = [(r["soft_conf"], 1.0 if abs(r["soft"] - r["human_x10"]) <= 10 else 0.0) for r in train]
    # bin means
    tb = defaultdict(list)
    for c, y in tr_pairs:
        tb[int(min(9, max(0, c * 10)))].append(y)
    cal_map = {b: (sum(v) / len(v) if v else 0.0) for b, v in tb.items()}
    # apply to test
    cal_preds = []
    cal_truth = []
    for r in test:
        b = int(min(9, max(0, r["soft_conf"] * 10)))
        cal_preds.append(cal_map.get(b, 0.0))
        cal_truth.append(1.0 if abs(r["soft"] - r["human_x10"]) <= 10 else 0.0)
    # Brier
    if cal_preds:
        brier = sum((p - y) ** 2 for p, y in zip(cal_preds, cal_truth)) / len(cal_preds)
        report["confidence_isotonic_p_err_le_10"] = {
            "brier": brier,
            "mean_pred_p": sum(cal_preds) / len(cal_preds),
            "mean_actual_rate": sum(cal_truth) / len(cal_truth),
            "cal_map": {str(k): v for k, v in sorted(cal_map.items())},
            "semantics": "confidence calibrated toward P(|score-human|<=10) on adult-L2 train; not child",
        }

    # affine for reference with VC check
    tr_nc = sub(train, "nonceil")
    if len(tr_nc) >= 20:
        a, b = fit_affine([r["soft"] for r in tr_nc], [r["human_x10"] for r in tr_nc])
        for kind in ("full", "nonceil", "hard"):
            te = sub(test, kind)
            if len(te) < 5:
                continue
            xs = [max(0, min(100, a * r["soft"] + b)) for r in te]
            ys = [r["human_x10"] for r in te]
            report["metrics"][f"affine_test_{kind}"] = metrics(xs, ys, f"affine_{kind}")
            report["metrics"][f"affine_test_{kind}"]["params"] = {"a": a, "b": b}

    (OUT / "so762_p16_calibration.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print("WROTE", OUT / "so762_p16_calibration.json", flush=True)
    print("test speakers", len(sets["test"]), "test metrics", report["metrics"].get("test"), flush=True)


if __name__ == "__main__":
    # default: try 30 test speakers, 50 utts/split for CPU
    main(min_test_speakers=30, max_utts_per_split=int(sys.argv[1]) if len(sys.argv) > 1 else 50)
