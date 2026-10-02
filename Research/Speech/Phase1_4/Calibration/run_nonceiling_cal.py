"""SO762 non-ceiling calibration with soft phone evidence (speaker-disjoint).
Filters: FULL / human_acc<10 / human_acc<8.
Does NOT boost from ASR text match.
"""
from __future__ import annotations
import json, sys, math, subprocess
from pathlib import Path
from collections import defaultdict

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

SO_META = REPO / "Research" / "Speech" / "Phase1_3" / "Speechocean762"
OUT = REPO / "Research" / "Speech" / "Phase1_4" / "Results"
TMP = Path(r"D:\speech-lab\data\so762_16k")
TMP.mkdir(parents=True, exist_ok=True)


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
    return math.sqrt(sum((x - m) ** 2 for x in xs) / len(xs))


def fit_affine(xs, ys):
    n = len(xs)
    mx, my = sum(xs) / n, sum(ys) / n
    varx = sum((x - mx) ** 2 for x in xs)
    if varx < 1e-12:
        return 0.0, my
    a = sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / varx
    b = my - a * mx
    return a, b


def stats(xs, ys, label):
    return {
        "label": label, "n": len(xs),
        "pearson": pearson(xs, ys), "spearman": spearman(xs, ys),
        "mae": mae(xs, ys) if xs else None, "rmse": rmse(xs, ys) if xs else None,
        "mean_pred": sum(xs) / len(xs) if xs else None,
        "mean_human": sum(ys) / len(ys) if ys else None,
        "std_pred": stdev(xs), "std_human": stdev(ys),
        "bias": (sum(xs) / len(xs) - sum(ys) / len(ys)) if xs else None,
    }


def ensure_16k(src, dst):
    if Path(dst).exists():
        return dst
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-i", str(src), "-ar", "16000", "-ac", "1", str(dst)], check=True)
    return dst


def main(max_utts=15):
    OUT.mkdir(parents=True, exist_ok=True)
    words = json.loads((SO_META / "word_index.json").read_text(encoding="utf-8"))
    by_uid = defaultdict(list)
    for w in words:
        by_uid[w["uid"]].append(w)

    # Prefer utts that contain at least one non-ceiling word for train/valid/test
    chosen = {"train": [], "valid": [], "test": []}
    for uid, ws in by_uid.items():
        sp = ws[0]["split"]
        if sp not in chosen or len(chosen[sp]) >= max_utts:
            continue
        has_err = any(float(w["human_acc_0_10"] or 10) < 10 for w in ws)
        # fill half with error-containing utts first
        if has_err or len(chosen[sp]) >= max_utts // 2:
            chosen[sp].append(uid)
    # fill remaining
    for uid, ws in by_uid.items():
        sp = ws[0]["split"]
        if sp not in chosen or len(chosen[sp]) >= max_utts:
            continue
        if uid not in chosen[sp]:
            chosen[sp].append(uid)

    pipe = SpeakingPipeline(enable_openpronounce=False)
    pev = PhoneEvidenceV2()
    rows = []
    for sp in ("train", "valid", "test"):
        for uid in chosen[sp]:
            ws = sorted(by_uid[uid], key=lambda x: x["word_i"])
            wav = Path(ws[0]["wav"])
            if not wav.exists():
                continue
            dst = TMP / f"{uid}.wav"
            try:
                ensure_16k(wav, dst)
            except Exception:
                continue
            text = ws[0]["utt_text"]
            try:
                r = pipe.run(str(dst), text, population_label="adult-L2-speechocean762")
                soft = pev.soft_match(str(dst), r.target.arpabet)
            except Exception as e:
                print("FAIL", uid, type(e).__name__, flush=True)
                continue
            # VAD gate
            soft_score = soft.soft_score_0_100 if r.vad.speech_detected else 0.0
            soft_conf = soft.confidence_0_1 if r.vad.speech_detected else 0.0
            # distribute soft score to words proportional to phone counts (approx)
            lengths = [len(w["phones"]) if isinstance(w["phones"], list) else 1 for w in ws]
            total = sum(lengths) or 1
            # use per-hit scores if counts match
            hit_scores = [100.0 * h.sim for h in soft.hits]
            idx = 0
            for w, L in zip(ws, lengths):
                if hit_scores and idx < len(hit_scores):
                    chunk = hit_scores[idx:idx + L] or hit_scores[idx:idx + 1]
                    idx += L
                    ai = sum(chunk) / len(chunk)
                else:
                    ai = soft_score
                rows.append({
                    "uid": uid, "split": sp, "speaker": w["speaker"], "word": w["text"],
                    "human_acc_0_10": w["human_acc_0_10"],
                    "human_x10": float(w["human_acc_0_10"]) * 10.0,
                    "s1": r.scores["scorer_v1"].score_0_100,
                    "soft": round(ai, 2),
                    "soft_utt": soft_score,
                    "soft_conf": soft_conf,
                    "vad": r.vad.speech_detected,
                })
            print(sp, uid, "soft", soft_score, "s1", r.scores["scorer_v1"].score_0_100, flush=True)

    (OUT / "so762_soft_rows.json").write_text(json.dumps(rows, indent=2), encoding="utf-8")

    def eval_subset(rs, pred_key, name):
        xs = [r[pred_key] for r in rs]
        ys = [r["human_x10"] for r in rs]
        return stats(xs, ys, name)

    report = {"population": "adult-L2", "NOT_4yo": True, "subsets": {}}
    for split in ("train", "valid", "test"):
        base = [r for r in rows if r["split"] == split]
        full = base
        nonceil = [r for r in base if float(r["human_acc_0_10"]) < 10]
        hard = [r for r in base if float(r["human_acc_0_10"]) < 8]
        report["subsets"][split] = {
            "full_s1": eval_subset(full, "s1", "full_s1"),
            "full_soft": eval_subset(full, "soft", "full_soft"),
            "nonceil_s1": eval_subset(nonceil, "s1", "nonceil_s1"),
            "nonceil_soft": eval_subset(nonceil, "soft", "nonceil_soft"),
            "hard_s1": eval_subset(hard, "s1", "hard_s1"),
            "hard_soft": eval_subset(hard, "soft", "hard_soft"),
            "n_full": len(full), "n_nonceil": len(nonceil), "n_hard": len(hard),
        }

    # affine on train nonceil soft -> test nonceil
    tr = [r for r in rows if r["split"] == "train" and float(r["human_acc_0_10"]) < 10]
    te = [r for r in rows if r["split"] == "test" and float(r["human_acc_0_10"]) < 10]
    te_full = [r for r in rows if r["split"] == "test"]
    if len(tr) >= 5:
        a, b = fit_affine([r["soft"] for r in tr], [r["human_x10"] for r in tr])
        report["affine_nonceil"] = {"a": a, "b": b}
        for label, rs in [("test_nonceil", te), ("test_full", te_full)]:
            xs = [max(0, min(100, a * r["soft"] + b)) for r in rs]
            ys = [r["human_x10"] for r in rs]
            st = stats(xs, ys, label)
            # flag constant-ceiling risk
            st["constant_ceiling_risk"] = bool(st["std_pred"] is not None and st["std_pred"] < 3.0)
            report[f"affine_{label}"] = st
    else:
        report["affine_nonceil"] = {"note": f"insufficient train nonceil n={len(tr)}"}

    # confidence bins on test using soft_conf vs abs err
    bins = defaultdict(list)
    for r in te_full:
        b = int(min(9, max(0, r["soft_conf"] * 10)))
        key = f"{b/10:.1f}-{(b+1)/10:.1f}"
        bins[key].append(abs(r["soft"] - r["human_x10"]))
    report["conf_bins_soft"] = {k: {"n": len(v), "mae": sum(v)/len(v)} for k, v in sorted(bins.items())}

    (OUT / "so762_nonceiling_calibration.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps({k: report[k] for k in report if k != "subsets"}, indent=2), flush=True)
    print("subsets_test", json.dumps(report["subsets"].get("test", {}), indent=2), flush=True)


if __name__ == "__main__":
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 12
    main(max_utts=n)
