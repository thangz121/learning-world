"""WP-1.9.31 — bench features + production-decision replica (research-only, no training).

Sets:
  bench_train / bench_test : SO762 children (age<=15), all final-consonant tokens
  pilot_decisions          : the frozen 546 pilot tokens, decision fields only
                             (frozen pilot_features.csv is NOT regenerated)

Per utterance: production encoder (xlsr-53) evidence + decision replica
(L.baseline_final_decision, exact production logic) + alternative encoder (lv-60) evidence.
Checkpointed: <set>.part.jsonl appended per utterance; restart skips done utterances.
"""
from __future__ import annotations

import argparse
import csv
import json
import os
import sys
import time
import uuid
from pathlib import Path

import numpy as np
import soundfile as sf

import p31_common as C

sys.path.insert(0, str(C.P27 / "experiments"))
sys.path.insert(0, str(C.REPO / "Research/Speech/Phase1_9_21/experiments"))
import pandas as pd  # noqa: E402
import p21_lib as L  # noqa: E402
import pilot_runner as PR  # noqa: E402

ART = C.ART / "features"
ART.mkdir(parents=True, exist_ok=True)
TMPDIR = Path(os.environ.get("TEMP", r"C:\Users\TECHVK~1\AppData\Local\Temp")) / "opencode"
TMPDIR.mkdir(parents=True, exist_ok=True)

ALT_ID = "facebook/wav2vec2-lv-60-espeak-cv-ft"
FIELDS = PR.FIELDS + ["match_type", "decision", "best_obs", "best_sim"]


def make_wav(x, sr):
    tmp = TMPDIR / f"_p31_{os.getpid()}_{uuid.uuid4().hex[:8]}.wav"
    PR.safe_write(tmp, x, sr)
    return tmp


def load_models():
    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
    from transformers import Wav2Vec2ForCTC, Wav2Vec2FeatureExtractor
    from huggingface_hub import hf_hub_download
    import torch
    pev = PhoneEvidenceV2()
    pev._ensure()
    feat = Wav2Vec2FeatureExtractor.from_pretrained(ALT_ID, local_files_only=True)
    model = Wav2Vec2ForCTC.from_pretrained(ALT_ID, local_files_only=True).eval()
    vocab = json.load(open(hf_hub_download(ALT_ID, "vocab.json", local_files_only=True),
                          encoding="utf-8"))
    id2tok = {int(v): k for k, v in vocab.items()}
    alt_ids = {}
    for i, tok in id2tok.items():
        if tok in ("|", "", "<pad>", "[PAD]", "<s>", "</s>", "<unk>"):
            continue
        c = pev.inv.normalize_symbol(tok)
        if c:
            alt_ids.setdefault(c, []).append(i)
    return pev, feat, model, alt_ids, torch


def focus_from(detail_utt):
    ref_flat, fin_flags, wtext = [], [], []
    for w in detail_utt.get("words", []):
        ref = (w.get("ref-phones") or "").split()
        for pi, ph in enumerate(ref):
            ref_flat.append(ph)
            fin_flags.append(pi == len(ref) - 1)
            wtext.append(w.get("text", ""))
    canon = C.INV.arpa_seq_to_canon(ref_flat)
    if len(canon) != len(ref_flat) or not canon:
        return None
    focus = [k for k in range(len(canon))
             if fin_flags[k] and canon[k] not in C.VOWELS]
    return canon, focus, wtext


def utt_rows(pev, feat, model, alt_ids, torch, detail, utt, meta):
    import parselmouth
    wav = C.SO / "WAVE" / f"SPEAKER{int(meta['speaker_id']):04d}" / f"{utt}.WAV"
    if not wav.exists():
        return None, "audio_missing"
    parsed = focus_from(detail[utt])
    if parsed is None:
        return None, "canon_mismatch"
    target, focus, wtext = parsed
    if not focus:
        return None, "no_focus"
    x, sr = sf.read(str(wav))
    if x.ndim > 1:
        x = x.mean(axis=1)
    x = x.astype(np.float32)
    tmp = make_wav(x, sr)
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

    inp = feat(x, sampling_rate=16000, return_tensors="pt").input_values
    with torch.no_grad():
        aprobs = torch.softmax(model(inp).logits[0], dim=-1).numpy().astype(np.float64)
    aspans = PR.ctc_align(aprobs, target, alt_ids)

    rows = []
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
        if len(span) > 200:
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
        dec = L.baseline_final_decision(probs, spans, target, class_ids, pev, k)
        a_ids = alt_ids.get(canon, [])
        ap = aprobs[:, a_ids].sum(axis=1) if a_ids else np.zeros(aprobs.shape[0])
        as_, ae = aspans[k]
        rows.append({
            "token_id": f"{utt}_{k}", "split": meta["split"], "speaker_id": meta["speaker_id"],
            "age": meta["age"], "utt_id": utt, "word": wtext[k], "target_phone": canon,
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
            "competitor_phone": comp_phone, "competitor_post": round(comp, 6),
            "margin_mean": round(float(p[s:e + 1].mean() - comp), 6),
            "peak_margin": round(peak_margin, 6),
            "blank_mean": round(blank, 6),
            "blank_at_peak": round(float(probs[pk, pev._blank]), 6),
            "cluster_width": w, "longest_run_05": longest,
            "temporal_support": round(float(p[pk]) * min(w / 2, 1.0), 6),
            "rms_span": round(rms_span, 5),
            "rms_ratio": round(rms_span / rms_utt, 3) if rms_utt else "",
            "f1_median": f1, "f2_median": f2, "f3_median": f3,
            "alt_max": round(float(ap[as_:ae + 1].max()), 6),
            "alt_mean": round(float(ap[as_:ae + 1].mean()), 6),
            "alt_delta": round(float(ap[as_:ae + 1].max()) - float(p[s:e + 1].max()), 6),
            "match_type": dec["match"], "decision": int(dec["match"] in ("exact", "soft")),
            "best_obs": dec["best_obs"], "best_sim": round(dec["sim"], 5),
        })
    return rows, None


def build_set(name):
    detail = C.load_detail()
    items = []
    if name in ("bench_train", "bench_test"):
        split = name.split("_", 1)[1]
        for spk, age, utts in C.kid_speakers(split):
            for u in utts:
                if u in detail:
                    items.append((u, {"split": name, "speaker_id": spk, "age": age}))
    elif name == "pilot_decisions":
        rows = list(csv.DictReader(open(C.P27 / "06_PILOT_DATA/PILOT_DATA_MANIFEST.csv",
                                        encoding="utf-8")))
        seen = set()
        for r in rows:
            u = r["utt_id"]
            if u in detail and u not in seen:
                seen.add(u)
                items.append((u, {"split": "PILOT_" + r["split"],
                                  "speaker_id": r["speaker_id"], "age": r["age"]}))
    return detail, items


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--set", required=True, choices=["bench_train", "bench_test", "pilot_decisions"])
    ap.add_argument("--limit", type=int, default=0, help="stop after N utterances (smoke test)")
    args = ap.parse_args()
    name = args.set
    part = ART / f"{name}.part.jsonl"
    done = set()
    if part.exists():
        for line in part.read_text(encoding="utf-8").splitlines():
            try:
                done.add(json.loads(line)["utt"])
            except Exception:  # noqa: BLE001
                pass
    detail, items = build_set(name)
    if args.limit:
        items = [it for it in items if it[0] not in done][:args.limit]
    print(f"[{name}] utts={len(items)} done={len(done)}", flush=True)
    pev, feat, model, alt_ids, torch = load_models()
    t0 = time.perf_counter()
    n_utt = 0
    with open(part, "a", encoding="utf-8") as fh:
        for ui, (u, meta) in enumerate(items):
            if u in done:
                continue
            try:
                rows, err = utt_rows(pev, feat, model, alt_ids, torch, detail, u, meta)
            except Exception as exc:  # noqa: BLE001
                rows, err = None, f"exception: {type(exc).__name__}: {exc}"
            rec = {"utt": u, "err": err, "rows": rows or []}
            fh.write(json.dumps(rec) + "\n")
            fh.flush()
            n_utt += 1
            if n_utt % 50 == 0:
                print(f"  [{name}] {ui+1}/{len(items)} (+{n_utt}) "
                      f"{time.perf_counter()-t0:.0f}s", flush=True)
    # merge
    rows = []
    errs = 0
    for line in part.read_text(encoding="utf-8").splitlines():
        rec = json.loads(line)
        if rec["err"]:
            errs += 1
        rows.extend(rec["rows"])
    df = pd.DataFrame(rows, columns=FIELDS)
    if len(df):
        df = df.drop_duplicates(subset=["token_id"], keep="last")
    out = ART / f"{name}_features.csv"
    df.to_csv(out, index=False, encoding="utf-8")
    print(f"[{name}] rows={len(df)} errors={errs} -> {out}", flush=True)
    print("DONE p31_features", name)


if __name__ == "__main__":
    main()
