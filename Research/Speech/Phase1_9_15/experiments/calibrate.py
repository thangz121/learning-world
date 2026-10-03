"""Phase 1.9.15 — P2 population calibration on SIAK (RESEARCH ONLY).

Trains lightweight CALIBRATION MODELS (not pronunciation-truth models) mapping
frozen soft-v2 evidence to the SIAK expert rating, with strict speaker-disjoint
train/validation/test splits built by split_audit.py.

Candidates (feature ablations):
  identity                    frozen soft-v2 score, unchanged
  affine_soft                 a*soft + b
  isotonic_soft               monotone PAVA on soft
  linear_soft_conf            soft + confidence
  linear_soft_age             soft + age
  linear_soft_conf_age        soft + confidence + age
  linear_soft_conf_evidence   soft + confidence + exact/miss ratios + duration + mean_sim
  tree3_soft                  depth-3 decision tree on soft
  tree3_full                  depth-3 tree on all features
  hgb_soft                    shallow gradient boosting on soft (+ conf)

All fits use TRAIN speakers only. Validation and test are untouched.
Ages 4-6 are never used for fitting; they are reported as a limited external set.
"""
from __future__ import annotations

import argparse
import csv
import json
import math
from pathlib import Path

import numpy as np
from sklearn.ensemble import HistGradientBoostingRegressor
from sklearn.isotonic import IsotonicRegression
from sklearn.linear_model import LinearRegression
from sklearn.tree import DecisionTreeRegressor

REPO = Path(__file__).resolve().parents[4]
OUT = REPO / "Research/Speech/Phase1_9_15"
RES = OUT / "artifacts" / "siak"
CAL = OUT / "artifacts" / "calibration"
CAL.mkdir(parents=True, exist_ok=True)

CORE = ["soft_full", "confidence_0_1"]
EVIDENCE = ["exact_ratio", "miss_ratio", "mean_sim", "duration_s", "n_hits"]
ALL_FEATURES = CORE + ["age"] + EVIDENCE


def load_rows(path: Path):
    rows = []
    for r in csv.DictReader(path.open(encoding="utf-8")):
        if r.get("error"):
            continue
        row = {}
        for k, v in r.items():
            if k in ("file", "utterance", "speaker_id", "l1", "split", "split_assignment", "population"):
                row[k] = v
            else:
                try:
                    row[k] = float(v)
                except (TypeError, ValueError):
                    row[k] = None
        n_hits = max(1.0, row.get("n_hits") or 0.0)
        row["exact_ratio"] = (row.get("n_exact") or 0.0) / n_hits
        row["miss_ratio"] = (row.get("n_miss") or 0.0) / n_hits
        row["soft_ratio"] = (row.get("n_soft") or 0.0) / n_hits
        rows.append(row)
    return rows


def pearson(xs, ys):
    n = len(xs)
    if n < 3:
        return None
    mx, my = sum(xs) / n, sum(ys) / n
    num = sum((a - mx) * (b - my) for a, b in zip(xs, ys))
    dx = math.sqrt(sum((a - mx) ** 2 for a in xs))
    dy = math.sqrt(sum((b - my) ** 2 for b in ys))
    return num / (dx * dy) if dx and dy else None


def rank(v):
    order = sorted(range(len(v)), key=lambda i: v[i])
    r = [0.0] * len(v)
    i = 0
    while i < len(order):
        j = i
        while j + 1 < len(order) and v[order[j + 1]] == v[order[i]]:
            j += 1
        avg = (i + j) / 2 + 1
        for k in range(i, j + 1):
            r[order[k]] = avg
        i = j + 1
    return r


def spearman(xs, ys):
    if len(xs) < 3:
        return None
    return pearson(rank(xs), rank(ys))


def mae(xs, ys):
    return float(np.mean(np.abs(np.asarray(xs) - np.asarray(ys)))) if xs else None


def metrics(y_true, y_pred, thresholds=(80.0,)):
    y_true = np.asarray(y_true, dtype=float)
    y_pred = np.clip(np.asarray(y_pred, dtype=float), 0.0, 100.0)
    out = {
        "n": int(len(y_true)),
        "pearson": pearson(list(y_true), list(y_pred)),
        "spearman": spearman(list(y_true), list(y_pred)),
        "mae": mae(list(y_true), list(y_pred)),
        "rmse": float(np.sqrt(np.mean((y_true - y_pred) ** 2))),
        "bias": float(np.mean(y_pred - y_true)),
        "pred_std": float(np.std(y_pred)),
        "true_std": float(np.std(y_true)),
    }
    for th in thresholds:
        good = y_true >= th
        poor = y_true < 50
        out[f"frr_proxy_lt50_on_ge{int(th)}"] = float(np.mean(y_pred[good] < 50)) if good.any() else None
        out[f"frr_proxy_lt40_on_ge{int(th)}"] = float(np.mean(y_pred[good] < 40)) if good.any() else None
        out[f"far_proxy_ge80_on_lt50"] = float(np.mean(y_pred[poor] >= 80)) if poor.any() else None
        out[f"far_proxy_ge50_on_lt50"] = float(np.mean(y_pred[poor] >= 50)) if poor.any() else None
    # calibration error (10 bins by predicted)
    order = np.argsort(y_pred)
    bins = np.array_split(order, 10)
    ece = 0.0
    for b in bins:
        if len(b):
            ece += (len(b) / len(y_pred)) * abs(np.mean(y_pred[b]) - np.mean(y_true[b]))
    out["calibration_error_10bin"] = float(ece)
    return out


def feature_matrix(rows, cols):
    X = []
    for r in rows:
        vals = []
        for c in cols:
            v = r.get(c)
            vals.append(0.0 if v is None else float(v))
        X.append(vals)
    return np.asarray(X, dtype=float)


def train_candidates(train_rows):
    y = np.asarray([r["siak_score"] for r in train_rows], dtype=float)
    models = {}
    soft = feature_matrix(train_rows, ["soft_full"])

    aff = LinearRegression().fit(soft, y)
    models["affine_soft"] = {
        "kind": "affine", "coef": float(aff.coef_[0]), "intercept": float(aff.intercept_)}

    iso = IsotonicRegression(out_of_bounds="clip").fit(soft[:, 0], y)
    models["isotonic_soft"] = {"kind": "isotonic", "x": iso.X_thresholds_.tolist(),
                               "y": iso.y_thresholds_.tolist()}

    lin_specs = {
        "linear_soft_conf": ["soft_full", "confidence_0_1"],
        "linear_soft_age": ["soft_full", "age"],
        "linear_soft_conf_age": ["soft_full", "confidence_0_1", "age"],
        "linear_soft_conf_evidence": ["soft_full", "confidence_0_1", "exact_ratio",
                                      "miss_ratio", "mean_sim", "duration_s", "n_hits"],
    }
    for name, cols in lin_specs.items():
        X = feature_matrix(train_rows, cols)
        lr = LinearRegression().fit(X, y)
        models[name] = {"kind": "linear", "cols": cols,
                        "coef": lr.coef_.tolist(), "intercept": float(lr.intercept_)}

    t3 = DecisionTreeRegressor(max_depth=3, random_state=1515).fit(soft, y)
    models["tree3_soft"] = {"kind": "tree", "model": t3, "cols": ["soft_full"]}
    Xall = feature_matrix(train_rows, ALL_FEATURES)
    models["tree3_full"] = {"kind": "tree", "model": DecisionTreeRegressor(max_depth=3, random_state=1515).fit(Xall, y),
                            "cols": ALL_FEATURES}
    no_age_cols = [c for c in ALL_FEATURES if c != "age"]
    Xnoage = feature_matrix(train_rows, no_age_cols)
    models["tree3_noage"] = {"kind": "tree",
                             "model": DecisionTreeRegressor(max_depth=3, random_state=1515).fit(Xnoage, y),
                             "cols": no_age_cols}
    hgb = HistGradientBoostingRegressor(max_depth=2, max_iter=80, learning_rate=0.07,
                                        l2_regularization=1.0, random_state=1515).fit(Xall, y)
    models["hgb_full"] = {"kind": "sklearn", "model": hgb, "cols": ALL_FEATURES}
    hgb_na = HistGradientBoostingRegressor(max_depth=2, max_iter=80, learning_rate=0.07,
                                           l2_regularization=1.0, random_state=1515).fit(Xnoage, y)
    models["hgb_noage"] = {"kind": "sklearn", "model": hgb_na, "cols": no_age_cols}
    hgb_s = HistGradientBoostingRegressor(max_depth=2, max_iter=80, learning_rate=0.07,
                                          l2_regularization=1.0, random_state=1515).fit(soft, y)
    models["hgb_soft"] = {"kind": "sklearn", "model": hgb_s, "cols": ["soft_full"]}
    return models


def apply_model(model, rows, name):
    if name == "identity":
        return np.clip([r["soft_full"] for r in rows], 0, 100)
    m = model
    if m["kind"] == "affine":
        return np.clip(m["coef"] * np.asarray([r["soft_full"] for r in rows]) + m["intercept"], 0, 100)
    if m["kind"] == "isotonic":
        return np.clip(np.interp([r["soft_full"] for r in rows], m["x"], m["y"]), 0, 100)
    X = feature_matrix(rows, m["cols"])
    return np.clip(m["model"].predict(X) if m["kind"] in ("tree", "sklearn")
                   else X @ np.asarray(m["coef"]) + m["intercept"], 0, 100)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--tags", nargs="*", default=None, help="candidate names to limit")
    args = ap.parse_args()

    train = load_rows(RES / "calibration_train.csv")
    valid = load_rows(RES / "calibration_valid.csv")
    test = load_rows(RES / "calibration_test.csv")
    ages46 = load_rows(RES / "calibration_ages46.csv")
    print(f"train {len(train)} | valid {len(valid)} | test {len(test)} | ages46 {len(ages46)}", flush=True)

    models = train_candidates(train)
    names = ["identity"] + list(models.keys())
    if args.tags:
        names = [n for n in names if n in args.tags or n == "identity"]

    results = []
    curve_rows = []
    resid_rows = []
    for name in names:
        model = models.get(name)
        for split_name, rows in (("train", train), ("validation", valid), ("test", test), ("ages46", ages46)):
            y_true = [r["siak_score"] for r in rows]
            y_pred = apply_model(model, rows, name)
            mt = metrics(y_true, y_pred)
            mt.update({"candidate": name, "split": split_name, "train_population": "SIAK ages7-12",
                       "test_population": "SIAK ages7-12" if split_name != "ages46" else "SIAK ages4-6 (external)",
                       "speaker_disjoint": split_name in ("validation", "test")})
            results.append(mt)
        # curves/residuals on test
        if model is not None:
            y_pred_test = apply_model(models.get(name), test, name)
            for r, p in zip(test, y_pred_test):
                curve_rows.append({"candidate": name, "file": r["file"], "speaker_id": r["speaker_id"],
                                   "age": r["age"], "siak_score": r["siak_score"],
                                   "predicted": float(p), "residual": float(p - r["siak_score"]),
                                   "soft_full": r["soft_full"]})
        elif name == "identity":
            for r in test:
                p = float(np.clip(r["soft_full"], 0, 100))
                curve_rows.append({"candidate": name, "file": r["file"], "speaker_id": r["speaker_id"],
                                   "age": r["age"], "siak_score": r["siak_score"],
                                   "predicted": p, "residual": p - r["siak_score"],
                                   "soft_full": r["soft_full"]})

    with open(CAL / "metrics.csv", "w", newline="", encoding="utf-8") as f:
        keys = ["candidate", "split", "n", "pearson", "spearman", "mae", "rmse", "bias",
                "pred_std", "true_std", "calibration_error_10bin",
                "frr_proxy_lt50_on_ge80", "frr_proxy_lt40_on_ge80",
                "far_proxy_ge80_on_lt50", "far_proxy_ge50_on_lt50",
                "train_population", "test_population", "speaker_disjoint"]
        w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
        w.writeheader()
        for r in results:
            w.writerow(r)
    with open(CAL / "calibration_curve_rows.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(curve_rows[0].keys()))
        w.writeheader()
        w.writerows(curve_rows)

    # age effect: within-test correlation per age for identity and best calibrated
    by_age = {}
    for row in results:
        if row["split"] == "test":
            by_age.setdefault(row["candidate"], {})
    for name in names:
        for age in sorted(set(r["age"] for r in test)):
            rs = [r for r in test if r["age"] == age]
            if len(rs) >= 10:
                pred = apply_model(models.get(name), rs, name)
                by_age.setdefault(name, {})[int(age)] = {
                    "n": len(rs), "pearson": pearson([r["siak_score"] for r in rs], list(pred)),
                    "mae": mae([r["siak_score"] for r in rs], list(pred)),
                    "mean_siak": float(np.mean([r["siak_score"] for r in rs])),
                    "mean_pred": float(np.mean(pred))}

    # age conditioning verdict: compare linear_soft_conf vs linear_soft_conf_age on test
    def find(name, split):
        return next(r for r in results if r["candidate"] == name and r["split"] == split)
    age_verdict = {
        "no_age_test": find("linear_soft_conf", "test"),
        "with_age_test": find("linear_soft_conf_age", "test"),
    }

    # SIAK score semantics summary on test
    ys = np.asarray([r["siak_score"] for r in test])
    sem = {
        "test_n": len(ys), "mean": float(np.mean(ys)), "std": float(np.std(ys)),
        "frac_ge80": float(np.mean(ys >= 80)), "frac_ge90": float(np.mean(ys >= 90)),
        "frac_eq100": float(np.mean(ys == 100)), "frac_lt50": float(np.mean(ys < 50)),
        "min": float(np.min(ys)), "max": float(np.max(ys)),
    }

    summary = {
        "phase": "1.9.15", "experiment": "SIAK population calibration (speaker-disjoint)",
        "splits": {"train": len(train), "validation": len(valid), "test": len(test),
                   "ages46_external": len(ages46)},
        "candidates": names,
        "age_conditioning_test": age_verdict,
        "per_age_test": by_age,
        "siak_test_score_semantics": sem,
        "flags": {"production_vad": False, "router_locked": False, "unity_integrated": False,
                  "scorer_modified": False, "production_window_locked": False},
    }
    (CAL / "metrics.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")

    # print compact test table
    print(f"{'candidate':28s} {'P':>6s} {'S':>6s} {'MAE':>6s} {'bias':>7s} {'FRR50':>6s} {'FAR80':>6s}")
    for r in results:
        if r["split"] == "test":
            print(f"{r['candidate']:28s} {r['pearson']:6.3f} {r['spearman']:6.3f} {r['mae']:6.2f} "
                  f"{r['bias']:7.2f} {(r['frr_proxy_lt50_on_ge80'] or -1):6.3f} "
                  f"{(r['far_proxy_ge80_on_lt50'] or -1):6.3f}")


if __name__ == "__main__":
    main()
