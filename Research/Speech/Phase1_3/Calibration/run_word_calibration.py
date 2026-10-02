"""Word-level Speechocean762 calibration (speaker-disjoint).
AI word score = mean scorer-v1 phone scores aligned to annotated word phones.
Human word accuracy is 0-10; we also report *10 for 0-100 comparison.
Population: adult L2. NOT_4yo.
"""
from __future__ import annotations
import json, sys, subprocess
from pathlib import Path
from collections import defaultdict
import math

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Adapters.paths import strip_stress

SO_META = Path(__file__).resolve().parents[1] / "Speechocean762"
OUT = Path(__file__).resolve().parents[1] / "Results"
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


def ensure_16k(src: Path, dst: Path) -> Path:
    if dst.exists():
        return dst
    subprocess.run(
        ["ffmpeg", "-y", "-v", "error", "-i", str(src), "-ar", "16000", "-ac", "1", str(dst)],
        check=True)
    return dst


def fit_affine(xs, ys):
    # y ~= a*x + b
    n = len(xs)
    mx, my = sum(xs) / n, sum(ys) / n
    varx = sum((x - mx) ** 2 for x in xs)
    if varx < 1e-12:
        return 0.0, my
    a = sum((x - mx) * (y - my) for x, y in zip(xs, ys)) / varx
    b = my - a * mx
    return a, b


def apply_affine(xs, a, b):
    return [max(0.0, min(100.0, a * x + b)) for x in xs]


def isotonic_predict(train_x, train_y, test_x):
    # simple pool-adjacent-violators on sorted unique x means
    pairs = sorted(zip(train_x, train_y))
    # bin by rounded raw score
    bins = defaultdict(list)
    for x, y in pairs:
        bins[round(x, 0)].append(y)
    keys = sorted(bins.keys())
    means = [sum(bins[k]) / len(bins[k]) for k in keys]
    # enforce non-decreasing
    for i in range(1, len(means)):
        if means[i] < means[i - 1]:
            means[i] = means[i - 1]
    out = []
    for x in test_x:
        # nearest key
        if not keys:
            out.append(0.0)
            continue
        k = min(keys, key=lambda kk: abs(kk - x))
        out.append(means[keys.index(k)])
    return out


def word_ai_scores_from_result(r, word_phones_list):
    """Assign scorer diagnostics phones sequentially to words by annotated phone counts."""
    diags = [d for d in r.scores["scorer_v1"].diagnostics if d.status != "ins"]
    # Use expected phones sequence from target IPA vs annotated ARPA counts
    # Simpler: split diagnostics into chunks sized by len(phones) per word
    chunks = []
    idx = 0
    # Build expected phone list lengths from annotation
    lengths = []
    for phones in word_phones_list:
        if isinstance(phones, list):
            lengths.append(len(phones))
        elif isinstance(phones, str):
            lengths.append(len(phones.split()))
        else:
            lengths.append(1)
    total_ann = sum(lengths) or 1
    # If diagnostic count differs, proportional assignment
    n_d = len(diags)
    if n_d == 0:
        return [0.0] * len(lengths), [0.0] * len(lengths)
    assigned = 0
    for i, L in enumerate(lengths):
        # number of diags for this word
        if i == len(lengths) - 1:
            take = n_d - assigned
        else:
            take = max(1, round(n_d * (L / total_ann)))
            take = min(take, n_d - assigned - (len(lengths) - i - 1))
            take = max(0, take)
        chunk = diags[assigned:assigned + take]
        assigned += take
        if chunk:
            sc = sum(d.score for d in chunk) / len(chunk)
            cf = sum(d.confidence for d in chunk) / len(chunk)
        else:
            sc, cf = 0.0, 0.0
        chunks.append((sc, cf))
    return [c[0] for c in chunks], [c[1] for c in chunks]


def main(max_utts_per_split: int = 40):
    OUT.mkdir(parents=True, exist_ok=True)
    words = json.loads((SO_META / "word_index.json").read_text(encoding="utf-8"))
    # group by utterance
    by_uid = defaultdict(list)
    for w in words:
        by_uid[w["uid"]].append(w)

    pipe = SpeakingPipeline(enable_openpronounce=False)
    rows = []
    # limit utts per split for CPU budget
    chosen = {"train": [], "valid": [], "test": []}
    for uid, ws in by_uid.items():
        sp = ws[0]["split"]
        if sp not in chosen:
            continue
        if len(chosen[sp]) >= max_utts_per_split:
            continue
        chosen[sp].append(uid)

    for sp in ("train", "valid", "test"):
        for uid in chosen[sp]:
            ws = sorted(by_uid[uid], key=lambda x: x["word_i"])
            wav = Path(ws[0]["wav"])
            if not wav.exists():
                print("MISS", uid, flush=True)
                continue
            dst = TMP / f"{uid}.wav"
            try:
                ensure_16k(wav, dst)
            except Exception as e:
                print("FF", uid, e, flush=True)
                continue
            text = ws[0]["utt_text"]
            try:
                r = pipe.run(str(dst), text, population_label="adult-L2-speechocean762")
            except Exception as e:
                print("PIPE", uid, type(e).__name__, e, flush=True)
                continue
            phones_lists = [w["phones"] for w in ws]
            ai_scores, ai_confs = word_ai_scores_from_result(r, phones_lists)
            for w, ai_s, ai_c in zip(ws, ai_scores, ai_confs):
                rows.append({
                    "uid": uid, "split": sp, "speaker": w["speaker"],
                    "word": w["text"], "word_i": w["word_i"],
                    "human_acc_0_10": w["human_acc_0_10"],
                    "human_acc_x10": float(w["human_acc_0_10"]) * 10.0,
                    "ai_raw_0_100": round(ai_s, 2),
                    "ai_conf": round(ai_c, 3),
                    "utt_s1": r.scores["scorer_v1"].score_0_100,
                    "utt_asr": r.asr.text,
                    "n_phones_ann": len(w["phones"]) if isinstance(w["phones"], list) else None,
                })
            print(f"{sp} {uid} words={len(ws)} utt_s1={r.scores['scorer_v1'].score_0_100:.1f}", flush=True)

    (OUT / "so762_word_rows.json").write_text(json.dumps(rows, indent=2), encoding="utf-8")

    def split_rows(name):
        return [r for r in rows if r["split"] == name]

    def eval_pair(xs, ys, label):
        return {
            "label": label, "n": len(xs),
            "pearson": pearson(xs, ys), "spearman": spearman(xs, ys),
            "mae": mae(xs, ys) if xs else None, "rmse": rmse(xs, ys) if xs else None,
            "mean_pred": sum(xs) / len(xs) if xs else None,
            "mean_human": sum(ys) / len(ys) if ys else None,
            "bias": (sum(xs) / len(xs) - sum(ys) / len(ys)) if xs else None,
        }

    train, valid, test = split_rows("train"), split_rows("valid"), split_rows("test")
    # filter missing human
    def clean(rs):
        return [r for r in rs if r["human_acc_0_10"] is not None]

    train, valid, test = clean(train), clean(valid), clean(test)

    results = {"population": "adult-L2-speechocean762", "NOT_4yo": True, "methods": {}}

    # A raw
    for name, rs in [("train", train), ("valid", valid), ("test", test)]:
        xs = [r["ai_raw_0_100"] for r in rs]
        ys = [r["human_acc_x10"] for r in rs]
        results["methods"].setdefault("raw", {})[name] = eval_pair(xs, ys, f"raw_{name}")

    # B affine fit on train -> apply valid/test
    if len(train) >= 10:
        a, b = fit_affine([r["ai_raw_0_100"] for r in train], [r["human_acc_x10"] for r in train])
        results["affine_params"] = {"a": a, "b": b}
        for name, rs in [("train", train), ("valid", valid), ("test", test)]:
            xs = apply_affine([r["ai_raw_0_100"] for r in rs], a, b)
            ys = [r["human_acc_x10"] for r in rs]
            results["methods"].setdefault("affine", {})[name] = eval_pair(xs, ys, f"affine_{name}")

        # isotonic-like on train
        for name, rs in [("valid", valid), ("test", test)]:
            xs = isotonic_predict(
                [r["ai_raw_0_100"] for r in train],
                [r["human_acc_x10"] for r in train],
                [r["ai_raw_0_100"] for r in rs])
            ys = [r["human_acc_x10"] for r in rs]
            results["methods"].setdefault("isotonic_bin", {})[name] = eval_pair(xs, ys, f"iso_{name}")

        # confidence-aware: weight raw by conf then affine
        # x' = raw * (0.5 + 0.5*conf)
        def conf_x(r):
            return r["ai_raw_0_100"] * (0.5 + 0.5 * r["ai_conf"])
        a2, b2 = fit_affine([conf_x(r) for r in train], [r["human_acc_x10"] for r in train])
        results["conf_affine_params"] = {"a": a2, "b": b2}
        for name, rs in [("valid", valid), ("test", test)]:
            xs = apply_affine([conf_x(r) for r in rs], a2, b2)
            ys = [r["human_acc_x10"] for r in rs]
            results["methods"].setdefault("conf_affine", {})[name] = eval_pair(xs, ys, f"confaff_{name}")

    # phone-count weighted already partially via chunk sizes; report by word length
    by_len = defaultdict(list)
    for r in test:
        n = r.get("n_phones_ann") or 0
        bucket = "1" if n <= 1 else ("2-3" if n <= 3 else "4+")
        by_len[bucket].append(r)
    results["test_by_phone_count"] = {}
    for b, rs in by_len.items():
        xs = [r["ai_raw_0_100"] for r in rs]
        ys = [r["human_acc_x10"] for r in rs]
        results["test_by_phone_count"][b] = eval_pair(xs, ys, b)

    (OUT / "so762_word_calibration.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    print(json.dumps(results, indent=2), flush=True)


if __name__ == "__main__":
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 25
    main(max_utts_per_split=n)
