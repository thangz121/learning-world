"""Re-evaluate calibration on non-ceiling human words (human_acc < 10)."""
from __future__ import annotations
import json, math
from pathlib import Path

OUT = Path(__file__).resolve().parents[1] / "Results"
rows = json.loads((OUT / "so762_word_rows.json").read_text(encoding="utf-8"))


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
        "mae": mae(xs, ys) if xs else None,
        "mean_pred": sum(xs) / len(xs) if xs else None,
        "mean_human": sum(ys) / len(ys) if ys else None,
    }

train = [r for r in rows if r["split"] == "train" and r["human_acc_0_10"] is not None]
test = [r for r in rows if r["split"] == "test" and r["human_acc_0_10"] is not None]
# ceiling vs non-ceiling
test_all = test
test_err = [r for r in test if float(r["human_acc_0_10"]) < 10]
test_hi = [r for r in test if float(r["human_acc_0_10"]) >= 10]
train_err = [r for r in train if float(r["human_acc_0_10"]) < 10]

report = {
    "ceiling_note": "SO762 word accuracy mean ~9.4/10; many perfect 10s → ceiling effect",
    "test_all_raw": stats([r["ai_raw_0_100"] for r in test_all], [r["human_acc_x10"] for r in test_all], "test_all"),
    "test_err_raw_human_lt10": stats([r["ai_raw_0_100"] for r in test_err], [r["human_acc_x10"] for r in test_err], "test_err"),
    "test_perfect10_raw": stats([r["ai_raw_0_100"] for r in test_hi], [r["human_acc_x10"] for r in test_hi], "test_hi"),
    "n_test_err": len(test_err), "n_test_hi": len(test_hi),
}
# affine on train_err only if enough
if len(train_err) >= 8:
    a, b = fit_affine([r["ai_raw_0_100"] for r in train_err], [r["human_acc_x10"] for r in train_err])
    xs = [max(0, min(100, a * r["ai_raw_0_100"] + b)) for r in test_err]
    ys = [r["human_acc_x10"] for r in test_err]
    report["affine_on_errors_only"] = {"a": a, "b": b, **stats(xs, ys, "aff_err")}
elif len(train) >= 8:
    a, b = fit_affine([r["ai_raw_0_100"] for r in train], [r["human_acc_x10"] for r in train])
    xs = [max(0, min(100, a * r["ai_raw_0_100"] + b)) for r in test_err]
    ys = [r["human_acc_x10"] for r in test_err]
    report["affine_train_all_eval_err"] = {"a": a, "b": b, **stats(xs, ys, "aff_all_err")}

# confidence bins on test vs absolute error to human
bins = {}
for r in test_all:
    c = r["ai_conf"]
    b = int(min(9, max(0, c * 10)))
    key = f"{b/10:.1f}-{(b+1)/10:.1f}"
    bins.setdefault(key, []).append(abs(r["ai_raw_0_100"] - r["human_acc_x10"]))
report["confidence_bins_abs_err"] = {
    k: {"n": len(v), "mae": sum(v) / len(v)} for k, v in sorted(bins.items())
}

(OUT / "so762_ceiling_and_confbins.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(json.dumps(report, indent=2))
