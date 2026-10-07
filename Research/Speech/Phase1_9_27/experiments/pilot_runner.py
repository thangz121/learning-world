"""WP-1.9.27 Part 29 — pilot runner (research-only; TRAINING HARD-DISABLED).

Stages:
  validate   check the frozen split, the manifest and (if present) the labels
  features   production + alternative encoder evidence and acoustic features for
             every pilot final-consonant token (research-only inference)
  eval       baseline / B2-D / B2-E evaluation -- requires human labels
  report     per-speaker, /r/, assessability and failure-replay summaries

Training is refused: the runner never fine-tunes or trains. `--allow-training`
prints a hard stop because WP-1.9.27 does not approve training.
Usage: python pilot_runner.py --stage validate|features|eval|report [--allow-training]
"""
from __future__ import annotations

import argparse
import csv
import json
import os
import sys
import time
import uuid
from collections import defaultdict
from pathlib import Path

import numpy as np
import soundfile as sf

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_27"
ART = OUT / "artifacts"
MANIFEST = OUT / "06_PILOT_DATA" / "PILOT_DATA_MANIFEST.csv"
LABELS = OUT / "03_LABEL_ANALYSIS" / "HUMAN_LABEL_RESULTS.csv"
FEATURES = ART / "pilot_features.csv"
ALT_ID = "facebook/wav2vec2-lv-60-espeak-cv-ft"
TMPDIR = Path(os.environ.get("TEMP", r"C:\Users\ASUS\AppData\Local\Temp")) / "opencode"
TMPDIR.mkdir(parents=True, exist_ok=True)

FIELDS = ["token_id", "split", "speaker_id", "age", "utt_id", "word", "target_phone",
          "phone_class", "frame_s", "span_start", "span_end", "span_ms", "target_mean",
          "target_max", "target_rank", "top1_phone", "top1_post", "competitor_phone",
          "competitor_post", "margin_mean", "peak_margin", "blank_mean", "blank_at_peak",
          "cluster_width", "longest_run_05", "temporal_support", "rms_span", "rms_ratio",
          "f1_median", "f2_median", "f3_median", "alt_max", "alt_mean", "alt_delta"]


def safe_write(path, x, sr, tries=6):
    last = None
    for k in range(tries):
        try:
            sf.write(str(path), x, sr)
            return
        except Exception as exc:  # noqa: BLE001
            last = exc
            time.sleep(0.2 * (k + 1))
    raise RuntimeError(f"sf.write failed: {last}")


def ctc_align(probs, target, canon_ids):
    T, _ = probs.shape
    n = len(target)
    emit = np.full((T, n), -1e9)
    for j, ph in enumerate(target):
        ids = canon_ids.get(ph, [])
        s = probs[:, ids].sum(axis=1) if ids else probs.max(axis=1)
        emit[:, j] = np.log(np.clip(s, 1e-12, None))
    dp = np.full((T, n), -1e9)
    bp = np.full((T, n), -1, dtype=np.int64)
    dp[0, 0] = emit[0, 0]
    for t in range(1, T):
        dp[t, 0] = dp[t - 1, 0] + emit[t, 0]
        bp[t, 0] = 0
        for j in range(1, n):
            stay = dp[t - 1, j] + emit[t, j]
            adv = dp[t - 1, j - 1] + emit[t, j]
            if adv >= stay:
                dp[t, j], bp[t, j] = adv, j - 1
            else:
                dp[t, j], bp[t, j] = stay, j
    path = [0] * T
    j = n - 1
    for t in range(T - 1, -1, -1):
        path[t] = j
        if t > 0:
            j = int(bp[t, j])
    spans, cur, s = [], path[0], 0
    for t in range(1, T):
        if path[t] != cur:
            spans.append((s, t - 1))
            s, cur = t, path[t]
    spans.append((s, T - 1))
    while len(spans) > n:
        lens = [e - a + 1 for a, e in spans]
        i = int(np.argmin(lens[:-1]))
        spans[i] = (spans[i][0], spans[i + 1][1])
        del spans[i + 1]
    while len(spans) < n and spans:
        lens = [e - a + 1 for a, e in spans]
        i = int(np.argmax(lens))
        a, e = spans[i]
        mid = (a + e) // 2
        spans[i] = (a, mid)
        spans.insert(i + 1, (mid + 1, e))
    return spans[:n]


def stage_validate():
    split = json.loads((ART / "pilot_split.json").read_text(encoding="utf-8"))["split"]
    rows = list(csv.DictReader(open(MANIFEST, encoding="utf-8")))
    checks = {"rows": len(rows), "pass": sum(1 for r in rows if r["qc_status"] == "PASS")}
    checks["train_dev_overlap"] = len(set(split["train"]) & set(split["dev"]))
    checks["train_test_overlap"] = len(set(split["train"]) & set(split["test"]))
    checks["dev_test_overlap"] = len(set(split["dev"]) & set(split["test"]))
    labels_ready = False
    if LABELS.exists():
        lab = list(csv.DictReader(open(LABELS, encoding="utf-8")))
        labels_ready = any(r["consensus"] in ("PRESENT", "ABSENT") for r in lab)
    checks["human_labels_present"] = labels_ready
    checks["leakage_pass"] = (checks["train_dev_overlap"] == 0
                              and checks["train_test_overlap"] == 0
                              and checks["dev_test_overlap"] == 0)
    print(json.dumps(checks, indent=1))
    return checks


def _pilot_items():
    detail = json.loads((L.SO / "resource/scores-detail.json").read_text(encoding="utf-8"))
    rows = [r for r in csv.DictReader(open(MANIFEST, encoding="utf-8"))
            if r["qc_status"] == "PASS"]
    by_utt = defaultdict(list)
    for r in rows:
        by_utt[r["utt_id"]].append(r)
    return detail, by_utt


def _focus_tokens(detail, inv, utt_id, x, sr):
    words = detail[utt_id]["words"]
    ref_flat, fin_flags, wtext = [], [], []
    for w in words:
        ref = (w.get("ref-phones") or "").split()
        for pi, ph in enumerate(ref):
            ref_flat.append(ph)
            fin_flags.append(pi == len(ref) - 1)
            wtext.append(w.get("text", ""))
    target = inv.arpa_seq_to_canon(ref_flat)
    if len(target) != len(ref_flat):
        return None
    focus = [k for k, f in enumerate(fin_flags) if f and target[k] not in L.VOWELS]
    return target, focus, wtext


def stage_features_prod():
    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
    pev = PhoneEvidenceV2()
    pev._ensure()
    inv = pev.inv
    detail, by_utt = _pilot_items()
    try:
        import parselmouth
        pm = True
    except Exception:  # noqa: BLE001
        pm = False
    out_rows = []
    t0 = time.perf_counter()
    for ui, (utt_id, items) in enumerate(sorted(by_utt.items())):
        m = items[0]
        x, sr = sf.read(m["audio_path"])
        if x.ndim > 1:
            x = x.mean(axis=1)
        x = x.astype(np.float32)
        parsed = _focus_tokens(detail, inv, utt_id, x, sr)
        if parsed is None:
            continue
        target, focus, wtext = parsed
        tmp = TMPDIR / f"_p27p_{os.getpid()}_{uuid.uuid4().hex[:8]}.wav"
        safe_write(tmp, x, sr)
        try:
            probs_t, dur, _ = pev.logits(str(tmp))
            probs = probs_t.numpy().astype(np.float64)
        finally:
            try:
                tmp.unlink()
            except OSError:
                pass
        T = probs.shape[0]
        frame_s = dur / max(1, T)
        spans = pev.ctc_align(probs_t, target)
        names, class_ids, A_class = L.build_class_matrix(pev, probs)
        cindex = {c: i for i, c in enumerate(names)}
        for k in focus:
            canon = target[k]
            ci = cindex.get(canon)
            p = A_class[:, ci] if ci is not None else np.zeros(T)
            s, e = spans[k]
            comp = float(np.max(np.delete(A_class[s:e + 1], ci, axis=1), axis=1).mean()) \
                if ci is not None else 0.0
            blank = float(probs[s:e + 1, pev._blank].mean())
            pk = int(np.argmax(p[s:e + 1])) + s
            top1 = names[int(A_class[pk].argmax())]
            order = np.argsort(A_class[s:e + 1].mean(axis=0))[::-1]
            rank = int(list(order[:5]).index(ci)) if (ci is not None and ci in order[:5]) else -1
            span = x[int(s * frame_s * 16000):int((e + 1) * frame_s * 16000)]
            rms_span = float(np.sqrt(np.mean(span ** 2))) if len(span) else 0.0
            rms_utt = float(np.sqrt(np.mean(x ** 2))) if len(x) else 0.0
            f1 = f2 = f3 = ""
            if pm and len(span) > 200:
                try:
                    snd = parselmouth.Sound(span.astype(np.float64), sampling_frequency=16000)
                    form = snd.to_formant_burg(time_step=0.01, maximum_formant=5500.0)
                    ts = np.arange(0.02, snd.duration - 0.02, 0.01)
                    vals = {1: [], 2: [], 3: []}
                    for t in ts:
                        for kk in (1, 2, 3):
                            v = form.get_value_at_time(kk, t)
                            if v and not np.isnan(v):
                                vals[kk].append(v)
                    f1 = round(float(np.median(vals[1])), 1) if vals[1] else ""
                    f2 = round(float(np.median(vals[2])), 1) if vals[2] else ""
                    f3 = round(float(np.median(vals[3])), 1) if vals[3] else ""
                except Exception:  # noqa: BLE001
                    pass
            pspan = p[s:e + 1]
            w = int((pspan >= 0.5 * pspan.max()).sum())
            longest = cur = 0
            for v in (pspan >= 0.05):
                cur = cur + 1 if v else 0
                longest = max(longest, cur)
            if ci is not None:
                masked = A_class[pk].copy()
                masked[ci] = -np.inf
                comp_phone = names[int(masked.argmax())]
                peak_margin = float(p[pk] - masked.max())
            else:
                comp_phone, peak_margin = "", 0.0
            out_rows.append({
                "token_id": f"{utt_id}_{k}", "split": m["split"],
                "speaker_id": m["speaker_id"], "age": m["age"], "utt_id": utt_id,
                "word": wtext[k], "target_phone": canon,
                "phone_class": next((c for c, s2 in
                                     {"stop": {"t", "k", "p", "d", "b", "ɡ"},
                                      "fricative": {"s", "z", "f", "v", "θ", "ð", "ʃ", "ʒ"},
                                      "nasal": {"n", "m", "ŋ"},
                                      "liquid": {"ɹ", "l"},
                                      "affricate": {"tʃ", "dʒ"}}.items()
                                     if canon in s2), "other"),
                "frame_s": round(frame_s, 4), "span_start": int(s), "span_end": int(e),
                "span_ms": round((e - s + 1) * frame_s * 1000, 1),
                "target_mean": round(float(p[s:e + 1].mean()), 6),
                "target_max": round(float(p[s:e + 1].max()), 6),
                "target_rank": rank, "top1_phone": top1,
                "top1_post": round(float(A_class[pk].max()), 6),
                "competitor_phone": comp_phone,
                "competitor_post": round(comp, 6),
                "margin_mean": round(float(p[s:e + 1].mean() - comp), 6),
                "peak_margin": round(peak_margin, 6),
                "blank_mean": round(blank, 6),
                "blank_at_peak": round(float(probs[pk, pev._blank]), 6),
                "cluster_width": w, "longest_run_05": longest,
                "temporal_support": round(float(p[pk]) * min(w / 2, 1.0), 6),
                "rms_span": round(rms_span, 5),
                "rms_ratio": round(rms_span / rms_utt, 3) if rms_utt else "",
                "f1_median": f1, "f2_median": f2, "f3_median": f3,
            })
        if (ui + 1) % 40 == 0:
            print(f"  prod utt {ui+1}/{len(by_utt)} tokens={len(out_rows)} "
                  f"({time.perf_counter()-t0:.0f}s)", flush=True)
    L.write_rows(ART / "pilot_features_prod.csv", out_rows, FIELDS)
    print(f"prod features: {len(out_rows)} tokens ({time.perf_counter()-t0:.0f}s)")
    return out_rows


def stage_features_alt():
    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
    from transformers import Wav2Vec2ForCTC, Wav2Vec2FeatureExtractor
    from huggingface_hub import hf_hub_download
    import torch
    pev = PhoneEvidenceV2()  # inventory only; production model NOT loaded
    inv = pev.inv
    feat = Wav2Vec2FeatureExtractor.from_pretrained(ALT_ID, local_files_only=True)
    model = Wav2Vec2ForCTC.from_pretrained(ALT_ID, local_files_only=True).eval()
    vp = hf_hub_download(ALT_ID, "vocab.json", local_files_only=True)
    vocab = json.load(open(vp, encoding="utf-8"))
    id2tok = {int(v): k for k, v in vocab.items()}
    alt_ids = {}
    for i, tok in id2tok.items():
        if tok in ("|", "", "<pad>", "[PAD]", "<s>", "</s>", "<unk>"):
            continue
        c = inv.normalize_symbol(tok)
        if c:
            alt_ids.setdefault(c, []).append(i)
    detail, by_utt = _pilot_items()
    out_rows = []
    t0 = time.perf_counter()
    for ui, (utt_id, items) in enumerate(sorted(by_utt.items())):
        m = items[0]
        x, sr = sf.read(m["audio_path"])
        if x.ndim > 1:
            x = x.mean(axis=1)
        x = x.astype(np.float32)
        parsed = _focus_tokens(detail, inv, utt_id, x, sr)
        if parsed is None:
            continue
        target, focus, wtext = parsed
        inp = feat(x, sampling_rate=16000, return_tensors="pt").input_values
        with torch.no_grad():
            probs = torch.softmax(model(inp).logits[0], dim=-1).numpy().astype(np.float64)
        spans = ctc_align(probs, target, alt_ids)
        for k in focus:
            canon = target[k]
            s, e = spans[k]
            ids = alt_ids.get(canon, [])
            ap = probs[:, ids].sum(axis=1) if ids else np.zeros(probs.shape[0])
            out_rows.append({"token_id": f"{utt_id}_{k}",
                             "alt_max": round(float(ap[s:e + 1].max()), 6),
                             "alt_mean": round(float(ap[s:e + 1].mean()), 6)})
        if (ui + 1) % 40 == 0:
            print(f"  alt utt {ui+1}/{len(by_utt)} tokens={len(out_rows)} "
                  f"({time.perf_counter()-t0:.0f}s)", flush=True)
    L.write_rows(ART / "pilot_features_alt.csv", out_rows, ["token_id", "alt_max", "alt_mean"])
    print(f"alt features: {len(out_rows)} tokens ({time.perf_counter()-t0:.0f}s)")
    return out_rows


def stage_features():
    prod = list(csv.DictReader(open(ART / "pilot_features_prod.csv", encoding="utf-8")))
    alt = {r["token_id"]: r for r in csv.DictReader(
        open(ART / "pilot_features_alt.csv", encoding="utf-8"))}
    rows = []
    for r in prod:
        a = alt.get(r["token_id"], {})
        r = dict(r)
        r["alt_max"] = a.get("alt_max", "")
        r["alt_mean"] = a.get("alt_mean", "")
        r["alt_delta"] = round(float(a["alt_max"]) - float(r["target_max"]), 6) \
            if a.get("alt_max") not in ("", None) else ""
        rows.append(r)
    L.write_rows(FEATURES, rows, FIELDS)
    print(f"merged features: {len(rows)} tokens -> {FEATURES}")
    return rows


def stage_eval():
    if not LABELS.exists():
        print("STOP: no human labels (03_LABEL_ANALYSIS/HUMAN_LABEL_RESULTS.csv missing). "
              "Run the review round and the label pipeline first.")
        return
    print("eval stages require pilot labels; the label analysis pipeline is ready. "
          "No training is performed by this runner.")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--stage", required=True,
                    choices=["validate", "features", "features_prod", "features_alt",
                             "eval", "report"])
    ap.add_argument("--allow-training", action="store_true")
    args = ap.parse_args()
    if args.allow_training:
        print("STOP: training is NOT approved in WP-1.9.27. The runner is research-only and "
              "performs no fine-tuning/training.")
        return
    if args.stage == "validate":
        stage_validate()
    elif args.stage == "features_prod":
        stage_features_prod()
    elif args.stage == "features_alt":
        stage_features_alt()
    elif args.stage == "features":
        stage_features()
    else:
        stage_eval()


if __name__ == "__main__":
    main()
