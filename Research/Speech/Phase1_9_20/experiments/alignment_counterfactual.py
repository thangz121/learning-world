"""WP-1.9.20 — alignment representation audit & counterfactual realignment (research-only).

No production change, no training, no Unity. Reuses 1.9.19 evaluation sets; frame-level
logits were NOT cached in 1.9.17-19 (verified: only summary rows), so the frozen encoder is
re-run ONLY for the exact evaluation sets (LWE 80 tokens + so762 96/96/22 utterances).

Variants on the same frame posteriors:
  A CURRENT  production ctc_align (monotone, every phone >=1 frame, no blank)
  B BLANK    expanded 2L+1 Viterbi, blank states, no skips
  C SKIP     B + epsilon deletion transitions with penalty delta
  D EVID    C with emissions = normalized log-posterior + lambda * GOP(target vs competitor)
  E CONS    C + conservative final-phone support rule (s_min)

Outputs: artifacts/alignment_rows.csv, ALIGNMENT_TRACE.csv, ALIGNMENT_VARIANT_RESULTS.json
"""
from __future__ import annotations

import csv
import json
import math
import re
import sys
import time
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(REPO))
OUT = REPO / "Research/Speech/Phase1_9_20"
ART = OUT / "artifacts"
ART.mkdir(parents=True, exist_ok=True)

from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402

P198 = REPO / "Research/Speech/Phase1_9_8/Results"
P1912 = REPO / "Research/Speech/Phase1_9_12/Results"
P1919 = REPO / "Research/Speech/Phase1_9_19/artifacts"
SO = Path(r"D:\speech-lab\data\speechocean762")
SO_MANIFEST = REPO / "Research/Speech/Phase1_9_16/artifacts/audit/so762_manifest.csv"
ZEN = REPO / "Research/Speech/ExternalData/zenodo_200495"
DER = ZEN / "derived_16k"
NEG = -1e9
FLOOR = 0.30
TRACE_IDS = {"child_01_nine", "child_07_one", "child_02_one", "child_04_four",
             "child_03_six", "child_07_seven"}


def sha256_file(p: Path) -> str:
    import hashlib
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def load_mono16(path: Path):
    key = sha256_file(path)[:16] + "_" + re.sub(r"[^A-Za-z0-9_.-]+", "_", path.name)[:60]
    cache = DER / f"{key}.wav"
    x, _ = sf.read(str(cache))
    if x.ndim > 1:
        x = x.mean(axis=1)
    return x.astype(np.float32)


def find_source(speaker_id, target):
    m = re.match(r"child_(\d+)", speaker_id or "")
    if not m:
        return None
    nn = m.group(1)
    base = ZEN / "extracted/english_children/english_words_sentences"
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


def logsumexp(a):
    m = a.max(axis=1, keepdims=True)
    return (m + np.log(np.exp(a - m).sum(axis=1, keepdims=True) + 1e-300)).squeeze(1)


def viterbi(emit, blank_emit, n, skip_pen=None):
    """Expanded 2L+1 Viterbi. Returns (score, path, visited[n])."""
    T = emit.shape[0]
    S = 2 * n + 1
    dp = np.full((T, S), NEG)
    bp = np.full((T, S), -1, dtype=int)
    cons = np.zeros((T, S), dtype=bool)

    def em(t, s):
        return blank_emit[t] if s % 2 == 0 else emit[t, (s - 1) // 2]

    dp[0, 0] = blank_emit[0]
    if S > 1:
        dp[0, 1] = emit[0, 0]
    for t in range(1, T):
        for s in range(S):
            best, bs = NEG, -1
            if dp[t - 1, s] > NEG / 2:
                best, bs = dp[t - 1, s] + em(t, s), s
            if s - 1 >= 0 and dp[t - 1, s - 1] > NEG / 2:
                v = dp[t - 1, s - 1] + em(t, s)
                if v > best:
                    best, bs = v, s - 1
            if bs >= 0:
                dp[t, s] = best
                bp[t, s] = bs
                cons[t, s] = True
        if skip_pen is not None:
            for s in range(0, S - 2, 2):
                if dp[t, s] > NEG / 2 and dp[t, s] - skip_pen > dp[t, s + 2]:
                    dp[t, s + 2] = dp[t, s] - skip_pen
                    bp[t, s + 2] = s
                    cons[t, s + 2] = False
    score = float(dp[T - 1, S - 1])
    path = [0] * T
    visited = [False] * n
    s, t = S - 1, T - 1
    guard = 0
    while t >= 0 and guard < 4 * T * S:
        guard += 1
        path[t] = s
        if s % 2 == 1:
            visited[(s - 1) // 2] = True
        prev = int(bp[t, s]) if bp[t, s] >= 0 else 0
        c = bool(cons[t, s])
        if not c:
            s = prev
            continue
        if t == 0:
            break
        t -= 1
        s = prev
    return score, path, visited


def spans_from_path(path, n):
    spans = {}
    for t, s in enumerate(path):
        if s % 2 == 1:
            j = (s - 1) // 2
            a, b = spans.get(j, (t, t))
            spans[j] = (min(a, t), max(b, t))
    return [spans.get(j) for j in range(n)]


def span_stats(p, span):
    if span is None:
        return {"span": None, "max": 0.0, "mean": 0.0, "blank_frac": 0.0, "frames": 0}
    s, e = span
    seg = p[s:e + 1]
    return {"span": (int(s), int(e)), "max": float(seg.max()), "mean": float(seg.mean()),
            "frames": int(e - s + 1)}


def main():
    pev = PhoneEvidenceV2()
    pev._ensure()
    tgt = CmuDictTargetAdapter()
    class_ids = {c: ids for c, ids in pev._canon_ids.items() if c != "sil"}
    blank_id = pev._blank

    # ---- evaluation sets (identical to 1.9.19) ----
    lwe_rows = list(csv.DictReader((P1919 / "window_rows_lwe.csv").open(encoding="utf-8")))
    lwe_tokens = sorted({r["token_id"] for r in lwe_rows})
    so_sets = {}
    for tag, f in (("so762_dev", "window_rows_so762_dev.csv"),
                   ("so762_test", "window_rows_so762_test.csv"),
                   ("so762_absent_dev", "window_rows_so762_absent_dev.csv")):
        rs = list(csv.DictReader((P1919 / f).open(encoding="utf-8")))
        by = defaultdict(list)
        for r in rs:
            by[r["token_id"]].append(r)
        so_sets[tag] = by

    # so762 target sequences (use 1.9.18 parsing convention: score = mean of experts)
    detail = json.loads((SO / "resource/scores-detail.json").read_text(encoding="utf-8"))

    def parse_words(utt):
        words = []
        for w in utt.get("words", []):
            ref = (w.get("ref-phones") or "").split()
            experts = w.get("phones") or []
            per = [[] for _ in ref]
            for es in experts:
                j, ok = 0, True
                for tok in es.split():
                    if tok.startswith("[") and tok.endswith("]"):
                        continue
                    if j >= len(ref):
                        ok = False
                        break
                    sc = 0 if (tok.startswith("(") and tok.endswith(")")) else (
                        1 if (tok.startswith("{") and tok.endswith("}")) else 2)
                    per[j].append(sc)
                    j += 1
                if not ok or j != len(ref):
                    for k in range(len(ref)):
                        per[k].append("?")
            ss = [round(sum(v) / len(v), 3) if v and all(x != "?" for x in v) else "" for v in per]
            words.append({"ref": ref, "scores": ss})
        return words

    fc = {r["case_id"]: r["human_final_label"] for r in csv.DictReader(
        (P1912 / "final_consonant_human_review.csv").open(encoding="utf-8-sig"))}

    rows = []
    traces = []
    t0 = time.perf_counter()

    # ---------- LWE ----------
    for i, tid in enumerate(lwe_tokens):
        sid, word = tid.rsplit("_", 1)
        src = find_source(sid, word)
        if src is None:
            continue
        x = load_mono16(src)
        st = tgt.build(word)
        arpa = [str(v) for v in st.arpabet]
        target = pev.inv.arpa_seq_to_canon(arpa)
        probs_t, dur, _ = pev.logits(_tmp16(x))
        probs = probs_t.numpy().astype(np.float64)
        rows.extend(analyze_one(tid, sid, word, target, probs, class_ids, blank_id, pev, [],
                                fc.get(f"fc_{sid}_{word}", ""), None, traces, corpus="lwe"))
        if (i + 1) % 20 == 0:
            print(f"  lwe {i+1}/{len(lwe_tokens)} ({time.perf_counter()-t0:.0f}s)", flush=True)

    # ---------- so762 ----------
    man = {m["utt_id"]: m for m in csv.DictReader(SO_MANIFEST.open(encoding="utf-8"))}
    for tag, by in so_sets.items():
        for utt_j, winrows in by.items():
            utt_id, j = utt_j.rsplit("_", 1)
            j = int(j)
            m = man[utt_id]
            words = parse_words(detail[utt_id])
            ref_flat, scores = [], []
            for w in words:
                for ph, sc in zip(w["ref"], w["scores"]):
                    ref_flat.append(ph)
                    scores.append(sc)
            if j >= len(ref_flat) or scores[j] == "":
                continue
            toname = {c: ids for c, ids in class_ids.items()}
            target = pev.inv.arpa_seq_to_canon(ref_flat)
            spk = int(m["speaker_id"])
            wav = SO / "WAVE" / f"SPEAKER{spk:04d}" / f"{utt_id}.WAV"
            x, sr = sf.read(str(wav))
            if x.ndim > 1:
                x = x.mean(axis=1)
            x = x.astype(np.float32)
            probs_t, dur, _ = pev.logits(_tmp16(x))
            probs = probs_t.numpy().astype(np.float64)
            rows.extend(analyze_one(utt_j, m["speaker_id"], str(scores[j]), target, probs,
                                    class_ids, blank_id, pev, winrows, "",
                                    float(scores[j]), traces, corpus=tag))
        print(f"  {tag} done ({time.perf_counter()-t0:.0f}s)", flush=True)

    if (ART / "_tmp.wav").exists():
        (ART / "_tmp.wav").unlink()
    if rows:
        with open(ART / "alignment_rows.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
            w.writeheader()
            w.writerows(rows)
    if traces:
        with open(OUT / "ALIGNMENT_TRACE.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=list(traces[0].keys()))
            w.writeheader()
            w.writerows(traces)
    print("rows", len(rows), "traces", len(traces), flush=True)


_TMP = None


def _tmp16(x):
    global _TMP
    if _TMP is None:
        _TMP = ART / "_tmp.wav"
    sf.write(str(_TMP), x, 16000)
    return str(_TMP)


def analyze_one(tid, sid, word, target, probs, class_ids, blank_id, pev, winrows,
                human_label, human_score, traces, corpus="lwe"):
    """All variants for the FINAL target phone of this token/utterance."""
    n = len(target)
    j = n - 1
    T = probs.shape[0]
    canon = target[j]
    outs = []
    # class probability matrix for all classes used by targets + competitors
    ids_j = class_ids.get(canon, [])
    p = probs[:, ids_j].sum(axis=1) if ids_j else np.zeros(T)
    p_blank = probs[:, blank_id]
    all_classes = list(class_ids.keys())
    A = np.stack([probs[:, class_ids[c]].sum(axis=1) for c in all_classes], axis=1)
    top2 = np.argsort(A, axis=1)[:, -2:]
    comp = np.zeros(T)
    for t in range(T):
        a, b = top2[t]
        comp[t] = A[t, a] if all_classes[a] != canon else A[t, b]
    # target emissions
    e_raw = np.stack([np.log(probs[:, class_ids[c]].sum(axis=1) + 1e-12) for c in target], axis=1)
    norm = logsumexp(np.concatenate([A, p_blank[:, None]], axis=1))
    e_norm = e_raw - norm[:, None]
    blank_norm = np.log(p_blank + 1e-12) - norm
    gop = e_raw[:, j] - np.log(comp + 1e-12)
    variants = {}
    # A current
    spans_a = pev.ctc_align(_as_tensor(probs), target)
    variants["A_current"] = spans_a
    # B blank-aware
    _, path_b, _ = viterbi(e_raw, np.log(p_blank + 1e-12), n, skip_pen=None)
    variants["B_blank"] = spans_from_path(path_b, n)
    # C skip (delta grid) with normalized emissions
    for d in (0.0, 0.5, 1.0, 2.0, 4.0):
        _, path_c, vis = viterbi(e_norm, blank_norm, n, skip_pen=d)
        variants[f"C_skip_d{d}"] = spans_from_path(path_c, n)
    # D evidence-weighted
    for lam in (0.5, 1.0):
        e_d = e_norm + lam * np.stack([gop for _ in target], axis=1) / max(1, n)
        _, path_d, _ = viterbi(e_d, blank_norm, n, skip_pen=1.0)
        variants[f"D_evid_l{lam}"] = spans_from_path(path_d, n)
    # E conservative uses C d=1 spans (decision rule differs)
    _, path_e, _ = viterbi(e_norm, blank_norm, n, skip_pen=1.0)
    variants["E_cons"] = spans_from_path(path_e, n)

    old_span = spans_a[j] if j < len(spans_a) else None
    old = span_stats(p, old_span)
    old_match = winrows[0]["baseline_match"] if winrows else None
    mask = np.ones(T, dtype=bool)
    if old_span:
        mask[old_span[0]:old_span[1] + 1] = False
    rec = {
        "corpus": corpus,
        "token_id": tid, "speaker_id": sid, "word": word, "target_phone": canon,
        "human_label": human_label, "human_score": human_score,
        "n_frames": T,
        "old_span": f"{old_span[0]}-{old_span[1]}" if old_span else "",
        "old_span_ms": round(old["frames"] * 1000.0 / max(1, T), 1),
        "old_span_max": round(old["max"], 4),
        "evidence_outside_old": round(float(p[mask].max()) if mask.any() else 0.0, 4),
    }
    for name, spans in variants.items():
        sp = spans[j] if j < len(spans) else None
        st_ = span_stats(p, sp)
        rec[f"{name}_span"] = f"{sp[0]}-{sp[1]}" if sp else ""
        rec[f"{name}_max"] = round(st_["max"], 4)
        rec[f"{name}_frames"] = st_["frames"]
    # decisions (same floor except E)
    for name in variants:
        rec[f"{name}_decision"] = "PRESENT" if rec[f"{name}_max"] >= FLOOR else "ABSENT"
    rec["E_cons_decision"] = ("PRESENT" if rec["E_cons_max"] >= 0.5 else
                              ("UNCERTAIN" if rec["E_cons_max"] >= 0.2 else "ABSENT"))
    # displacement A->C d=1
    ca = variants["C_skip_d1.0"][j] if j < len(variants["C_skip_d1.0"]) else None
    if old_span and ca:
        rec["displacement_ms"] = round((((ca[0] + ca[1]) / 2) - ((old_span[0] + old_span[1]) / 2))
                                       * 1000.0 / max(1, T), 1)
    else:
        rec["displacement_ms"] = ""
    outs.append(rec)

    # traces for representative LWE cases
    if tid in TRACE_IDS and not winrows:
        new_span = ca
        for t in range(T):
            traces.append({
                "case_id": tid, "frame": t, "time_ms": round(t * 1000.0 / max(1, T), 1),
                "target_phone": canon,
                "target_posterior": round(float(p[t]), 5),
                "top1_phone": all_classes[int(top2[t][1])],
                "top1_posterior": round(float(A[t, top2[t][1]]), 5),
                "competitor_posterior": round(float(comp[t]), 5),
                "blank_posterior": round(float(p_blank[t]), 5),
                "old_span": f"{old_span[0]}-{old_span[1]}" if old_span else "",
                "old_span_select": int(bool(old_span and old_span[0] <= t <= old_span[1])),
                "new_span": f"{new_span[0]}-{new_span[1]}" if new_span else "",
                "new_span_select": int(bool(new_span and new_span[0] <= t <= new_span[1])),
            })
    return outs


def _as_tensor(probs):
    import torch
    return torch.tensor(probs, dtype=torch.float32)


if __name__ == "__main__":
    _TMP = ART / "_tmp.wav"
    main()
