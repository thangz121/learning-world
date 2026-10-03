"""Phase 1.9.14 — P2 deletion-aware final-consonant evidence (RESEARCH ONLY).

Compares, on the frozen 65 child final-consonant tokens (28 human-labeled):
  A. current soft-v2 forced expected-phone representation (baseline, archived 1.9.12
     + reproduction check with the frozen PhoneEvidenceV2 code)
  B. deletion-aware blank-interleaved (2L+1) CTC Viterbi with explicit skip
     transitions and per-phone presence (slip-style present=false)
  C. GOP-ratio (likelihood-ratio against competing phone classes) over the
     resulting final-phone span

No production score, VAD, router or window behavior is changed. Raw child audio
is read from the gitignored ExternalData tree; only derived numbers are written.

Primary metric: FRR on human-confirmed-present child final consonants (FRR-first).

Method notes (math):
- Emissions are per-frame relative log-posteriors over (all phone classes + blank):
      E[t][c] = log p(c | frame t) - logsumexp_all_classes_and_blank
  so the blank is a genuine competing hypothesis and the deletion margin is a
  proper log-likelihood ratio between "final phone present" and "final phone
  deleted" under the same transition model.
- The expanded state layout is 0=blank, 1=p1, 2=blank, 3=p2, ..., 2n=blank.
  Deletion = epsilon transition blank_{j-1} -> blank_j (skipping phone j).
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import math
import re
import sys
import time
from collections import defaultdict
from pathlib import Path

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
from Research.Speech.Phase1_4.PhoneInventory.inventory import PhonemeInventoryAdapter

OUT = REPO / "Research/Speech/Phase1_9_14"
P1912 = REPO / "Research/Speech/Phase1_9_12"
EXT = REPO / "Research/Speech/ExternalData/zenodo_200495/extracted/english_children"
DER = REPO / "Research/Speech/ExternalData/zenodo_200495/derived_16k"

NEG = -1e9
PRESENT_LABELS = {"FINAL_CONSONANT_CLEARLY_PRESENT", "FINAL_CONSONANT_PROBABLY_PRESENT"}
ABSENT_LABELS = {"FINAL_CONSONANT_CLEARLY_ABSENT", "FINAL_CONSONANT_PROBABLY_ABSENT"}


def sha256_file(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def load_mono16(path: Path):
    key = sha256_file(path)[:16] + "_" + re.sub(r"[^A-Za-z0-9_.-]+", "_", path.name)[:60]
    cache = DER / f"{key}.wav"
    if cache.exists():
        x, sr = sf.read(str(cache))
        if x.ndim > 1:
            x = x.mean(axis=1)
        return x.astype(np.float32), int(sr), cache
    x, sr = sf.read(str(path))
    if x.ndim > 1:
        x = x.mean(axis=1)
    x = x.astype(np.float32)
    if sr != 16000:
        n = int(len(x) * 16000 / sr)
        t0 = np.linspace(0, 1, len(x), endpoint=False)
        t1 = np.linspace(0, 1, n, endpoint=False)
        x = np.interp(t1, t0, x).astype(np.float32)
        sr = 16000
    DER.mkdir(parents=True, exist_ok=True)
    sf.write(str(cache), x, sr)
    return x, sr, cache


def find_source(speaker_id: str, target: str):
    m = re.match(r"child_(\d+)", speaker_id or "")
    if not m:
        return None
    nn = m.group(1)
    base = EXT / "english_words_sentences"
    for sp in base.iterdir():
        if sp.name.startswith(nn + "_"):
            for mic in ("studio_mic", "port_mic", "nao_mic"):
                c = sp / mic / "numbers" / f"{target}.wav"
                if c.exists():
                    return c
            hits = list(sp.rglob(f"numbers/{target}.wav"))
            if hits:
                return hits[0]
    return None


def logsumexp(a: np.ndarray, axis=-1):
    m = a.max(axis=axis, keepdims=True)
    out = m + np.log(np.exp(a - m).sum(axis=axis, keepdims=True) + 1e-300)
    return np.squeeze(out, axis=axis)


def normalized_emissions(probs: np.ndarray, class_ids: dict, blank_id: int):
    """Per-frame relative log posteriors over phone classes + blank.

    Returns (emit_classes (T, n), names, emit_blank (T,)).
    """
    eps = 1e-12
    names = []
    cols = []
    for name, ids in class_ids.items():
        if not ids:
            continue
        names.append(name)
        cols.append(probs[:, ids].sum(axis=1))
    cols = np.stack(cols, axis=1)
    blank = probs[:, blank_id]
    norm = logsumexp(np.concatenate([cols, blank[:, None]], axis=1), axis=1)
    emit = np.log(cols + eps) - norm[:, None]
    emit_blank = np.log(blank + eps) - norm
    return emit, names, emit_blank


def expanded_viterbi(emit_all: np.ndarray, emit_blank: np.ndarray, n_phones: int,
                     block_final_visits: bool = False, block_final_skip: bool = False):
    """Blank-interleaved (2L+1) Viterbi over the whole token audio.

    State layout: 0=blank, 1=p1, 2=blank, 3=p2, ..., 2n=blank.
    - stay / advance transitions consume the frame.
    - deletion = epsilon blank_{j-1} -> blank_j (skip phone j); disallowed for the
      final phone when block_final_skip=True.
    - when block_final_visits=True the final phone state can never be entered, so
      the path is forced to delete it (used for the forced-absent hypothesis).
    Returns (score, per-frame state path, per-phone visited flags).
    """
    T = emit_all.shape[0]
    S = 2 * n_phones + 1
    final_state = 2 * n_phones - 1
    dp = np.full((T, S), NEG, dtype=np.float64)
    bp_state = np.full((T, S), -1, dtype=np.int64)
    bp_consumed = np.zeros((T, S), dtype=bool)

    def state_emit(t, s):
        if s % 2 == 0:
            return emit_blank[t]
        return emit_all[t, (s - 1) // 2]

    def state_ok(s):
        if block_final_visits and s == final_state:
            return False
        return True

    if state_ok(0):
        dp[0, 0] = emit_blank[0]
    if S > 1 and state_ok(1):
        dp[0, 1] = emit_all[0, 0]  # CTC init: first phone may own the first frame
    for t in range(1, T):
        for s in range(S):
            if not state_ok(s):
                continue
            best, bp, cons = NEG, -1, True
            if dp[t - 1, s] > NEG / 2:
                best, bp, cons = dp[t - 1, s] + state_emit(t, s), s, True
            if s - 1 >= 0 and state_ok(s - 1) and dp[t - 1, s - 1] > NEG / 2:
                v = dp[t - 1, s - 1] + state_emit(t, s)
                if v > best:
                    best, bp, cons = v, s - 1, True
            if bp >= 0:
                dp[t, s] = best
                bp_state[t, s] = bp
                bp_consumed[t, s] = cons
        # epsilon deletion transitions (same frame)
        for s in range(0, S - 2, 2):
            target = s + 2
            if not state_ok(target):
                continue
            if block_final_skip and (s == 2 * n_phones - 2):
                continue
            if dp[t, s] > NEG / 2 and dp[t, s] > dp[t, target]:
                dp[t, target] = dp[t, s]
                bp_state[t, target] = s
                bp_consumed[t, target] = False
    score = float(dp[T - 1, S - 1])
    # backtrack
    path = [0] * T
    visited = [False] * n_phones
    s, t = S - 1, T - 1
    guard = 0
    while t >= 0 and guard < 4 * T * S:
        guard += 1
        path[t] = s
        if s % 2 == 1:
            visited[(s - 1) // 2] = True
        prev = int(bp_state[t, s]) if bp_state[t, s] >= 0 else 0
        consumed = bool(bp_consumed[t, s])
        if not consumed:
            s = prev
            continue
        if t == 0:
            break
        t -= 1
        s = prev
    return score, path, visited


def deletion_margin(emit_all, emit_blank, n_phones):
    """LLR between final-phone-present and final-phone-deleted hypotheses."""
    score_present, path_present, present_p = expanded_viterbi(
        emit_all, emit_blank, n_phones, block_final_visits=False, block_final_skip=True)
    score_absent, _, _ = expanded_viterbi(
        emit_all, emit_blank, n_phones, block_final_visits=True, block_final_skip=False)
    score_free, path_free, present_free = expanded_viterbi(
        emit_all, emit_blank, n_phones, block_final_visits=False, block_final_skip=False)
    return {
        "score_forced_present": score_present,
        "score_forced_absent": score_absent,
        "score_free": score_free,
        "margin": score_present - score_absent,
        "present_free": bool(present_free[-1]),
        "path_present": path_present,
        "path_free": path_free,
    }


def spans_from_path(path, n_phones):
    spans = {}
    for t, s in enumerate(path):
        if s % 2 == 1:
            j = (s - 1) // 2
            a, b = spans.get(j, (t, t))
            spans[j] = (min(a, t), max(b, t))
    return [spans.get(j) for j in range(n_phones)]


def gop_for_final(probs, target_canon, spans, class_ids, blank_id):
    n = len(target_canon)
    eps = 1e-12
    if not spans or spans[n - 1] is None:
        return {"gop_ratio": None, "gop_expected": None, "gop_top_competitor": "",
                "gop_top_competitor_value": None, "gop_blank": None}
    s, e = spans[n - 1]
    mean_vec = probs[s:e + 1].mean(axis=0)
    exp_ids = class_ids.get(target_canon[n - 1], [])
    p_exp = float(mean_vec[exp_ids].sum()) if exp_ids else 0.0
    p_blank = float(mean_vec[blank_id])
    best_name, best_val = "", 0.0
    for name, ids in class_ids.items():
        if name == target_canon[n - 1] or not ids:
            continue
        v = float(mean_vec[ids].sum())
        if v > best_val:
            best_name, best_val = name, v
    p_comp = max(best_val, p_blank)
    return {"gop_ratio": math.log(p_exp + eps) - math.log(p_comp + eps),
            "gop_expected": p_exp, "gop_top_competitor": best_name,
            "gop_top_competitor_value": best_val, "gop_blank": p_blank}


def auc_roc(scores, labels):
    pos = [s for s, y in zip(scores, labels) if y == 1]
    neg = [s for s, y in zip(scores, labels) if y == 0]
    if not pos or not neg:
        return None
    wins = 0.0
    for a in pos:
        for b in neg:
            wins += 1.0 if a > b else (0.5 if a == b else 0.0)
    return wins / (len(pos) * len(neg))


def sweep(scores, labels, thresholds):
    rows = []
    for th in thresholds:
        tp = fp = tn = fn = 0
        for s, y in zip(scores, labels):
            if s is None:
                continue
            pred_absent = s < th
            if y == 1:
                fn += pred_absent
                tp += 1 - pred_absent
            else:
                tn += pred_absent
                fp += 1 - pred_absent
        n_present = sum(labels)
        n_absent = len(labels) - n_present
        rows.append({"threshold": th,
                     "frr": fn / n_present if n_present else None,
                     "far": fp / n_absent if n_absent else None,
                     "tp": tp, "fp": fp, "tn": tn, "fn": fn})
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--skip-baseline-repro", action="store_true")
    ap.add_argument("--out-prefix", default="p2")
    args = ap.parse_args()

    RES = OUT / "artifacts" / "p2"
    RES.mkdir(parents=True, exist_ok=True)

    tokens = list(csv.DictReader((P1912 / "Results/final_consonant_human_vs_model.csv").open(encoding="utf-8-sig")))
    human = {}
    for r in csv.DictReader((P1912 / "Results/final_consonant_human_review.csv").open(encoding="utf-8-sig")):
        human[r["case_id"]] = r["human_final_label"]

    tgt = CmuDictTargetAdapter()
    pev = PhoneEvidenceV2()
    pev._ensure()
    inv = PhonemeInventoryAdapter()

    class_ids = {c: ids for c, ids in pev._canon_ids.items() if c != "sil"}
    blank_id = pev._blank

    rows = []
    t0 = time.perf_counter()
    for i, r in enumerate(tokens):
        if args.limit and i >= args.limit:
            break
        sid, word, fp = r["speaker_id"], r["word"], r["final_phone"]
        src = find_source(sid, word)
        if src is None:
            print("MISSING_SOURCE", sid, word, flush=True)
            continue
        try:
            _, _, cache16 = load_mono16(src)
        except Exception as exc:  # noqa: BLE001
            print("LOAD_FAIL", sid, word, exc, flush=True)
            continue
        st = tgt.build(word)
        target = inv.arpa_seq_to_canon([str(p) for p in st.arpabet])
        probs_t, dur, _ = pev.logits(str(cache16))
        probs = probs_t.numpy().astype(np.float64)
        T = probs.shape[0]
        frame_s = dur / max(1, T)

        emit, names, emit_blank = normalized_emissions(probs, class_ids, blank_id)
        idx = {name: k for k, name in enumerate(names)}
        missing = [p for p in target if p not in idx]
        if missing:
            print("MISSING_CLASS", sid, word, missing, flush=True)
            continue
        emit_all = emit[:, [idx[p] for p in target]]

        dm = deletion_margin(emit_all, emit_blank, len(target))
        margin_per_frame = dm["margin"] / max(1, T)
        spans_p = spans_from_path(dm["path_present"], len(target))
        gop = gop_for_final(probs, target, spans_p, class_ids, blank_id)

        base_repro = {}
        if not args.skip_baseline_repro:
            sm = pev.soft_match(str(cache16), st.arpabet)
            last = sm.hits[-1] if sm.hits else None
            if last is not None:
                base_repro = {"repro_match": last.match_type, "repro_sim": last.sim,
                              "repro_posterior": last.posterior, "repro_score": sm.soft_score_0_100}

        human_label = human.get(f"fc_{sid}_{word}", "")
        row = {
            "token_id": f"fc_{sid}_{word}", "speaker_id": sid, "word": word,
            "final_phone": fp, "final_phone_class": r["final_phone_class"],
            "human_final_label": human_label,
            "human_present": (1 if human_label in PRESENT_LABELS else 0 if human_label in ABSENT_LABELS else ""),
            "archived_full_score": r["full_score"],
            "archived_phone_match": r["phone_final_match"],
            "archived_phone_sim": r["phone_final_sim"],
            "archived_phone_posterior": r["phone_final_posterior"],
            "archived_phone_present": 1 if r["phone_final_match"] in ("exact", "soft") else 0,
            "n_phones": len(target), "n_frames": T, "frame_s": frame_s, "dur_s": dur,
            "dalign_present_free": 1 if dm["present_free"] else 0,
            "dalign_margin": dm["margin"],
            "dalign_margin_per_frame": margin_per_frame,
            "final_span_start_s": (spans_p[-1][0] * frame_s) if spans_p[-1] else None,
            "final_span_end_s": (spans_p[-1][1] * frame_s) if spans_p[-1] else None,
            "final_span_frames": (spans_p[-1][1] - spans_p[-1][0] + 1) if spans_p[-1] else 0,
            "gop_ratio": gop["gop_ratio"], "gop_expected": gop["gop_expected"],
            "gop_top_competitor": gop["gop_top_competitor"],
            "gop_top_competitor_value": gop["gop_top_competitor_value"],
            "gop_blank": gop["gop_blank"],
        }
        row.update(base_repro)
        rows.append(row)
        g = row["gop_ratio"]
        print(f"[{i+1}/{len(tokens)}] {row['token_id']} {fp} human={human_label or '-'} "
              f"base={r['phone_final_match']} free={row['dalign_present_free']} "
              f"margin/f={margin_per_frame:.3f} gop={'n/a' if g is None else f'{g:.3f}'}", flush=True)

    out_csv = RES / f"{args.out_prefix}_tokens.csv"
    if rows:
        with open(out_csv, "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
            w.writeheader()
            w.writerows(rows)

    lab = [r for r in rows if r["human_present"] != ""]
    labels = [int(r["human_present"]) for r in lab]
    methods = {
        "A_softv2_phone_presence": "archived_phone_present",
        "B_dalign_free_presence": "dalign_present_free",
        "B_dalign_margin_per_frame": "dalign_margin_per_frame",
        "C_gop_ratio": "gop_ratio",
        "C_gop_expected": "gop_expected",
    }
    eval_rows = []
    for mname, field in methods.items():
        scores = [r[field] if r[field] not in ("", None) else None for r in lab]
        auc = auc_roc([s if s is not None else float("-inf") for s in scores], labels)
        eval_rows.append({"method": mname, "n": len(lab), "auc_present": auc})

    sweep_specs = {
        "B_dalign_margin_per_frame": np.arange(-4.0, 4.001, 0.25).tolist(),
        "C_gop_ratio": np.arange(-8.0, 4.001, 0.5).tolist(),
        "C_gop_expected": np.arange(0.0, 1.001, 0.05).tolist(),
    }
    sweep_rows = []
    for mname, ths in sweep_specs.items():
        scores = [r[methods[mname]] for r in lab]
        for srow in sweep(scores, labels, ths):
            srow["method"] = mname
            sweep_rows.append(srow)

    base_rows = []
    for mname, field in (("A_softv2_phone_presence", "archived_phone_present"),
                         ("B_dalign_free_presence", "dalign_present_free")):
        preds = [1 - int(r[field]) for r in lab]
        tp = fp = tn = fn = 0
        for p_abs, y in zip(preds, labels):
            fn += (p_abs and y == 1)
            tp += (not p_abs and y == 1)
            tn += (p_abs and y == 0)
            fp += (not p_abs and y == 0)
        n_present, n_absent = sum(labels), len(labels) - sum(labels)
        base_rows.append({"method": mname, "frr": fn / n_present, "far": fp / n_absent,
                          "tp": tp, "fp": fp, "tn": tn, "fn": fn})

    by_class = defaultdict(lambda: {"n": 0, "present": 0, "absent": 0, "base_correct": 0, "free_correct": 0})
    for r in lab:
        c = r["final_phone_class"]
        y = int(r["human_present"])
        by_class[c]["n"] += 1
        by_class[c]["present" if y else "absent"] += 1
        by_class[c]["base_correct"] += int(int(r["archived_phone_present"]) == y)
        by_class[c]["free_correct"] += int(int(r["dalign_present_free"]) == y)

    mism = []
    for r in lab:
        y = int(r["human_present"])
        for mname in ("A_softv2_phone_presence", "B_dalign_free_presence"):
            pred = int(r[methods[mname]])
            if pred != y:
                mism.append({"token_id": r["token_id"], "method": mname, "human": r["human_final_label"],
                             "predicted_present": pred, "final_phone": r["final_phone"],
                             "archived_phone_match": r["archived_phone_match"],
                             "gop_ratio": r["gop_ratio"], "margin_per_frame": r["dalign_margin_per_frame"]})

    summary = {
        "phase": "1.9.14",
        "experiment": "P2 deletion-aware final consonants",
        "n_tokens_scored": len(rows),
        "n_human_labeled": len(lab),
        "n_present": sum(labels),
        "n_absent": len(labels) - sum(labels),
        "baseline_operating_points": base_rows,
        "auc": eval_rows,
        "by_class": dict(by_class),
        "method_notes": {
            "A_softv2_phone_presence": "archived 1.9.12 soft-v2 final phone match exact|soft = present",
            "B_dalign_free_presence": "blank-interleaved (2L+1) Viterbi, final phone visited with zero skip penalty",
            "B_dalign_margin_per_frame": "score(forced present) - score(forced absent), per frame; higher = more present evidence",
            "C_gop_ratio": "log mean-p(expected final) - log mean-p(best competing phone class or blank) over forced-present span",
            "C_gop_expected": "mean posterior of expected final phone over forced-present span",
        },
        "runtime_s": round(time.perf_counter() - t0, 1),
        "production_vad": False, "router_locked": False, "unity_integrated": False,
        "scorer_modified": False, "production_window_locked": False,
    }
    (RES / f"{args.out_prefix}_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")

    with open(RES / f"{args.out_prefix}_threshold_sweep.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=["method", "threshold", "frr", "far", "tp", "fp", "tn", "fn"])
        w.writeheader()
        w.writerows(sweep_rows)

    if mism:
        with open(RES / f"{args.out_prefix}_mismatches.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=list(mism[0].keys()))
            w.writeheader()
            w.writerows(mism)

    prov = {
        "script": "p2_deletion_aware.py",
        "inputs": {
            "tokens": str(P1912 / "Results/final_consonant_human_vs_model.csv"),
            "tokens_sha256": sha256_file(P1912 / "Results/final_consonant_human_vs_model.csv"),
            "human_labels": str(P1912 / "Results/final_consonant_human_review.csv"),
            "human_labels_sha256": sha256_file(P1912 / "Results/final_consonant_human_review.csv"),
        },
        "model": "facebook/wav2vec2-xlsr-53-espeak-cv-ft (frozen, via PhoneEvidenceV2@1.4.0)",
        "baseline_repro_included": not args.skip_baseline_repro,
        "python": sys.version,
    }
    (OUT / "manifests" / f"{args.out_prefix}_provenance.json").write_text(json.dumps(prov, indent=2), encoding="utf-8")
    print("DONE", json.dumps({"n": len(rows), "base": base_rows, "auc": eval_rows}, indent=1))


if __name__ == "__main__":
    main()
