"""WP-1.9.21 shared research library (research-only; no production change).

Temporally plausible support aggregation for final-consonant evidence.
Reuses the frozen PhoneEvidenceV2 posteriors; defines position-masked
candidate regions and aggregation features. Everything here is read-only
with respect to production code, the scorer, VAD, router and Unity.
"""
from __future__ import annotations

import csv
import hashlib
import importlib.util
import json
import re
import sys
from pathlib import Path

import numpy as np

REPO = Path(__file__).resolve().parents[4]
if str(REPO) not in sys.path:
    sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402
from Research.Speech.Phase1_4.PhoneInventory.inventory import phone_similarity  # noqa: E402

P1919 = REPO / "Research/Speech/Phase1_9_19/artifacts"
P1912 = REPO / "Research/Speech/Phase1_9_12/Results"
P1918 = REPO / "Research/Speech/Phase1_9_18/artifacts"
SO = Path(r"D:\speech-lab\data\speechocean762")
SO_MANIFEST = REPO / "Research/Speech/Phase1_9_16/artifacts/audit/so762_manifest.csv"
ZEN = REPO / "Research/Speech/ExternalData/zenodo_200495"
DER = ZEN / "derived_16k"

WINDOWS = ["full", "raw", "pad100", "pad250"]
KB = 5   # region B local context around the production span (frames, 100 ms @20 ms)
KC = 5   # region C spill allowance into the following phone's span (frames)

VOWELS = {"ɑ", "æ", "ə", "ɔ", "aʊ", "aɪ", "ɛ", "ɝ", "eɪ", "ɪ", "iː", "oʊ", "ɔɪ", "ʊ", "uː",
          "ʌ", "ɑː", "e", "o", "i", "u", "a", "ɐ", "ɜ", "ɚ", "ɞ"}

PRESENT_LABELS = {"FINAL_CONSONANT_CLEARLY_PRESENT", "FINAL_CONSONANT_PROBABLY_PRESENT"}
ABSENT_LABELS = {"FINAL_CONSONANT_CLEARLY_ABSENT", "FINAL_CONSONANT_PROBABLY_ABSENT"}
HUMAN_UNCERTAIN_VERDICTS = {"AMBIGUOUS"}

# pre-declared sparse-peak widths (spec section 7)
WIDTHS = (1, 2, 3, 5)
# pre-declared top-K support values (spec section 5C)
TOPK = (1, 2, 3, 5)
# pre-declared temporal-support constants (documented purpose in the report)
TS_MARGIN_SCALE = 0.25   # target must beat the best competitor by this for full margin credit
TS_COHERENCE_WIDTH = 2   # half-max cluster width for full coherence credit
TS_HALF_FRAC = 0.5       # half-height cluster definition around the peak
TS_FLOOR = 0.05          # absolute floor for cluster membership


def sha256_file(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def load_mono16(path: Path) -> np.ndarray:
    key = sha256_file(path)[:16] + "_" + re.sub(r"[^A-Za-z0-9_.-]+", "_", path.name)[:60]
    cache = DER / f"{key}.wav"
    if cache.exists():
        import soundfile as sf
        x, _ = sf.read(str(cache))
    else:
        import soundfile as sf
        x, sr = sf.read(str(path))
        if x.ndim > 1:
            x = x.mean(axis=1)
        x = x.astype(np.float32)
        if sr != 16000:
            n = int(len(x) * 16000 / sr)
            x = np.interp(np.linspace(0, 1, n, endpoint=False),
                          np.linspace(0, 1, len(x), endpoint=False), x).astype(np.float32)
        DER.mkdir(parents=True, exist_ok=True)
        sf.write(str(cache), x, 16000)
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


def p2_module():
    spec = importlib.util.spec_from_file_location(
        "p2del21", REPO / "Research/Speech/Phase1_9_14/experiments/p2_deletion_aware.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def parse_words_so762(utt):
    """Same convention as WP-1.9.18/1.9.19/1.9.20 (expert mean scores per ref phone)."""
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
        words.append({"ref": ref, "scores": ss, "text": w.get("text", "")})
    return words


def flatten_so762(words):
    ref_flat, scores, word_idx, phone_pos, word_text = [], [], [], [], []
    for wi, w in enumerate(words):
        for pi, (ph, sc) in enumerate(zip(w["ref"], w["scores"])):
            ref_flat.append(ph)
            scores.append(sc)
            word_idx.append(wi)
            phone_pos.append(pi)
            word_text.append(w.get("text", ""))
    return ref_flat, scores, word_idx, phone_pos, word_text


def build_class_matrix(pev, probs):
    """Return (class_names, A) with A[t, c] = summed posterior of canon class c at frame t."""
    class_ids = {c: ids for c, ids in pev._canon_ids.items() if c != "sil"}
    names = list(class_ids.keys())
    V = probs.shape[1]
    ind = np.zeros((V, len(names)), dtype=np.float64)
    for ci, c in enumerate(names):
        ids = [i for i in class_ids[c] if 0 <= i < V]
        if ids:
            ind[ids, ci] = 1.0
    return names, class_ids, probs @ ind


def baseline_final_decision(probs, spans, target, class_ids, pev, j):
    """Replicate PhoneEvidenceV2.soft_match's decision for target phone j (production view)."""
    canon = target[j]
    s, e = spans[j] if j < len(spans) else (0, probs.shape[0] - 1)
    seg = probs[s:e + 1].mean(axis=0)
    top = np.argsort(seg)[-5:][::-1]
    topk = []
    for i in top:
        c = pev.inv.normalize_symbol(pev._id2tok.get(int(i), "?"))
        if c:
            topk.append((c, float(seg[i])))
    ids = class_ids.get(canon, [])
    post = float(seg[ids].sum()) if ids else 0.0
    best_obs = topk[0][0] if topk else ""
    best_sim = phone_similarity(canon, best_obs)
    for obs, pv in topk:
        sim = phone_similarity(canon, obs)
        if sim > best_sim or (sim == best_sim and pv > post):
            best_sim, best_obs = sim, obs
    if best_obs == canon or post >= 0.25:
        match = "exact" if (best_obs == canon or post >= 0.35) else "soft"
        sim = 1.0 if best_obs == canon else max(best_sim, min(1.0, post * 2))
    elif best_sim >= 0.35:
        match = "soft"
        sim = best_sim * (0.5 + 0.5 * post)
    else:
        match = "miss"
        sim = max(best_sim, post) * 0.5
    top1_phone, top1_post = (topk[0] if topk else ("", 0.0))
    return {"match": match, "post": post, "top1": top1_phone, "top1_post": top1_post,
            "best_obs": best_obs, "sim": float(sim), "span": (int(s), int(e))}


def deletion_all(probs, target, class_ids, blank_id, p2):
    """Deletion-aware Viterbi over the full target sequence (computed once per encoding).

    Same construction as WP-1.9.18/1.9.19; per-focus extraction happens in
    deletion_for_focus. Returns None when a target class has no emission column.
    """
    emit, names, emit_blank = p2.normalized_emissions(probs, class_ids, blank_id)
    idx = {n: k for k, n in enumerate(names)}
    if any(c not in idx for c in target):
        return None
    emit_all = emit[:, [idx[c] for c in target]]
    dm = p2.deletion_margin(emit_all, emit_blank, len(target))
    spans_da = p2.spans_from_path(dm["path_present"], len(target))
    return {"dm": dm, "spans": spans_da}


def deletion_for_focus(da_all, p, j):
    """Per-focus deletion-aware span/max from the shared full-sequence Viterbi."""
    if da_all is None:
        return {"da_span": None, "da_max": 0.0, "da_frame": -1,
                "free_present": "", "margin_per_frame": 0.0}
    T = len(p)
    dm = da_all["dm"]
    spans_da = da_all["spans"]
    da_span = spans_da[j] if spans_da and spans_da[j] else None
    da_max = float(p[da_span[0]:da_span[1] + 1].max()) if da_span else 0.0
    da_frame = int(da_span[0] + int(np.argmax(p[da_span[0]:da_span[1] + 1]))) if da_span else -1
    return {"da_span": da_span, "da_max": da_max, "da_frame": da_frame,
            "free_present": int(dm["present_free"]),
            "margin_per_frame": dm["margin"] / max(1, T)}


def regions_for(T, spans, target, j, A_class, class_index):
    """Position-masked candidate regions A/B/C/D for focus phone j.

    A: production span.
    B: production span +/- KB frames, minus earlier same-class spans.
    C: final tail after the preceding phone's acoustic boundary (last frame inside
       the preceding span where the preceding class posterior >= 50% of its span
       max), extending at most KC frames into the following phone's span, minus
       earlier same-class spans.
    D: conservative union B | C.
    M: earlier same-class occurrence mask (position masking, mandatory).
    """
    canon = target[j]
    A0, A1 = int(spans[j][0]), int(spans[j][1])
    prev = spans[j - 1] if j > 0 else None
    nxt = spans[j + 1] if j + 1 < len(spans) else None
    M = np.zeros(T, dtype=bool)
    earlier = []
    for k in range(j):
        if target[k] == canon and spans[k] is not None:
            s, e = int(spans[k][0]), int(spans[k][1])
            earlier.append((k, (s, e)))
            M[s:e + 1] = True

    B = np.zeros(T, dtype=bool)
    b0, b1 = max(0, A0 - KB), min(T - 1, A1 + KB)
    B[b0:b1 + 1] = True
    B &= ~M

    if j == 0 or prev is None:
        C0 = 0
    else:
        ps, pe = int(prev[0]), int(prev[1])
        pidx = class_index.get(target[j - 1])
        if pidx is not None:
            seg = A_class[ps:pe + 1, pidx]
            thr = TS_HALF_FRAC * float(seg.max()) if len(seg) else 0.0
            w = np.where(seg >= thr)[0]
            C0 = ps + int(w[-1]) + 1 if len(w) else pe + 1
        else:
            C0 = pe + 1
    C1 = min(T - 1, int(nxt[1]) + KC) if nxt is not None else T - 1
    C0 = min(max(0, C0), T - 1)
    if C1 < C0:
        C1 = C0
    C = np.zeros(T, dtype=bool)
    C[C0:C1 + 1] = True
    C &= ~M

    D = B | C
    A = np.zeros(T, dtype=bool)
    A[A0:A1 + 1] = True

    lo = min([A0 - KB, C0] + ([int(prev[0])] if prev is not None else [])
             + [s for _, (s, _) in earlier])
    hi = max([A1 + KB, C1] + ([int(nxt[1])] if nxt is not None else [])
             + [e for _, (_, e) in earlier])
    lo, hi = max(0, lo), min(T - 1, hi)
    return {"A": A, "B": B, "C": C, "D": D, "M": M,
            "A_span": (A0, A1), "prev": prev, "next": nxt, "earlier": earlier,
            "C0": C0, "C1": C1, "store_lo": lo, "store_hi": hi}


def _best_contiguous(p, mask, width):
    idx = np.where(mask)[0]
    if len(idx) == 0:
        return 0.0
    best = 0.0
    for s in range(len(idx) - width + 1):
        if idx[s + width - 1] - idx[s] == width - 1:
            v = float(p[idx[s]:idx[s + width - 1] + 1].mean())
            if v > best:
                best = v
    return best


def support_features(p, comp, blank, reg, prevp=None, nextp=None):
    """Aggregation features over the position-masked candidate region D (plus region maxima)."""
    out = {}
    A, B, C, D = reg["A"], reg["B"], reg["C"], reg["D"]
    for tag, m in (("A", A), ("B", B), ("C", C), ("D", D)):
        out[f"mean_{tag}"] = float(p[m].mean()) if m.any() else 0.0
        out[f"max_{tag}"] = float(p[m].max()) if m.any() else 0.0
        out[f"region_top3_{tag}"] = float(np.sort(p[m])[::-1][:3].mean()) if m.any() else 0.0
    vals = np.sort(p[D])[::-1] if D.any() else np.zeros(0)
    for k in TOPK:
        out[f"top{k}_D"] = float(vals[:k].mean()) if len(vals) else 0.0
    for w in WIDTHS:
        out[f"width{w}_D"] = _best_contiguous(p, D, w) if D.any() else 0.0
    out["n_frames_D"] = int(D.sum())
    if not D.any():
        out.update({"peak": 0.0, "peak_frame": -1, "cluster_width": 0,
                    "cluster_mean": 0.0, "neighbor_support": 0.0,
                    "comp_peak": 0.0, "blank_peak": 0.0, "prom_comp": 0.0,
                    "prom_blank": 0.0, "prom_neighbor": 0.0, "temporal_support": 0.0,
                    "top1_at_peak": "", "top1_post_at_peak": 0.0})
        return out
    idx = np.where(D)[0]
    tstar = int(idx[int(np.argmax(p[idx]))])
    peak = float(p[tstar])
    floor = max(TS_HALF_FRAC * peak, TS_FLOOR)
    lo = tstar
    while lo - 1 >= 0 and D[lo - 1] and p[lo - 1] >= floor:
        lo -= 1
    hi = tstar
    while hi + 1 < len(p) and D[hi + 1] and p[hi + 1] >= floor:
        hi += 1
    width = hi - lo + 1
    cluster_mean = float(p[lo:hi + 1].mean())
    if width > 1:
        neighbor_support = float((p[lo:hi + 1].sum() - peak) / (width - 1))
    else:
        neighbor_support = 0.0
    comp_peak = float(comp[tstar])
    blank_peak = float(blank[tstar])
    prom_comp = peak - comp_peak
    prom_blank = peak - blank_peak
    nb = 0.0
    if prevp is not None:
        nb = max(nb, float(prevp[tstar]))
    if nextp is not None:
        nb = max(nb, float(nextp[tstar]))
    prom_neighbor = peak - nb
    margin_credit = float(np.clip(prom_comp / TS_MARGIN_SCALE, 0.0, 1.0))
    coh_credit = float(np.clip(width / TS_COHERENCE_WIDTH, 0.0, 1.0))
    out.update({"peak": peak, "peak_frame": tstar, "cluster_width": width,
                "cluster_mean": cluster_mean, "neighbor_support": neighbor_support,
                "comp_peak": comp_peak, "blank_peak": blank_peak,
                "prom_comp": prom_comp, "prom_blank": prom_blank,
                "prom_neighbor": prom_neighbor,
                "temporal_support": peak * margin_credit * coh_credit,
                "top1_at_peak": "", "top1_post_at_peak": 0.0})
    return out


def corpus_targets_lwe(pev, tgt, word):
    st = tgt.build(word)
    arpa = [str(x) for x in st.arpabet]
    return pev.inv.arpa_seq_to_canon(arpa)


def read_rows(path):
    with open(path, encoding="utf-8") as f:
        return list(csv.DictReader(f))


def write_rows(path, rows, fields=None):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not rows and not fields:
        return
    fields = fields or list(rows[0].keys())
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(rows)


def lwe_work_items():
    """One item per LWE word token (9 speakers x number words)."""
    rows = read_rows(P1919 / "window_rows_lwe.csv")
    by_tok = {}
    for r in rows:
        by_tok.setdefault(r["token_id"], {})[r["window_type"]] = r
    items = []
    for tid, wins in sorted(by_tok.items()):
        sid, word = tid.rsplit("_", 1)
        items.append({"corpus": "lwe", "utt_id": tid, "token_id": tid, "speaker_id": sid,
                      "word": word, "focus_j": None, "windows": wins,
                      "human_label": wins.get("full", {}).get("human_label", ""),
                      "human_verdict": wins.get("full", {}).get("human_verdict", ""),
                      "human_score": ""})
    return items


def so762_work_items(csv_name, corpus):
    rows = read_rows(P1919 / csv_name)
    by_tok = {}
    for r in rows:
        by_tok.setdefault(r["token_id"], {})[r["window_type"]] = r
    items = []
    for tid, wins in sorted(by_tok.items()):
        utt_id, j = tid.rsplit("_", 1)
        ref = wins.get("full") or next(iter(wins.values()))
        items.append({"corpus": corpus, "utt_id": utt_id, "token_id": tid,
                      "speaker_id": ref["speaker_id"], "word": "",
                      "focus_j": int(j), "windows": wins,
                      "human_label": "",
                      "human_verdict": "",
                      "human_score": ref.get("human_phone_score", "")})
    return items
