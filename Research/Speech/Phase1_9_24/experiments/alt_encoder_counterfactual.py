"""WP-1.9.24 Part 18 — small alternative-encoder counterfactual (research-only).

Runs a SECOND, locally-available espeak-phoneme encoder
(facebook/wav2vec2-lv-60-espeak-cv-ft, already cached) on a small diagnostic
subset and asks whether the TYPE-B false-evidence and the no-evidence present
patterns persist across encoders. No training, no downloads, no production use.
"""
from __future__ import annotations

import csv
import json
import os
import sys
import time
import uuid
from pathlib import Path

import numpy as np
import soundfile as sf
import torch

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_24"
ART = OUT / "artifacts"
P21 = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"
P23 = L.REPO / "Research/Speech/Phase1_9_23"
ALT_ID = "facebook/wav2vec2-lv-60-espeak-cv-ft"
PRIMARY = "full"
TMPDIR = Path(os.environ.get("TEMP", r"C:\Users\ASUS\AppData\Local\Temp")) / "opencode"
TMPDIR.mkdir(parents=True, exist_ok=True)
TYPE_B = ["child_07_seven", "014180143_15", "014190172_7", "014350146_16"]

FIELDS = ["token_id", "group", "corpus", "target_phone", "primary_max_A", "alt_max_A",
          "primary_global_max", "alt_global_max", "alt_top1_at_span_peak",
          "alt_top1_post", "primary_decision", "delta_span", "note"]


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
        if not ids:
            emit[:, j] = np.log(probs.max(axis=1) + 1e-12) * 0.1
        else:
            s = probs[:, ids].sum(axis=1)
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
                dp[t, j] = adv
                bp[t, j] = j - 1
            else:
                dp[t, j] = stay
                bp[t, j] = j
    path = [0] * T
    j = n - 1
    for t in range(T - 1, -1, -1):
        path[t] = j
        if t > 0:
            j = int(bp[t, j])
    spans = []
    cur = path[0]
    s = 0
    for t in range(1, T):
        if path[t] != cur:
            spans.append((s, t - 1))
            s = t
            cur = path[t]
    spans.append((s, T - 1))
    while len(spans) > n:
        lengths = [e - a + 1 for a, e in spans]
        i = int(np.argmin(lengths[:-1]))
        spans[i] = (spans[i][0], spans[i + 1][1])
        del spans[i + 1]
    while len(spans) < n and spans:
        lengths = [e - a + 1 for a, e in spans]
        i = int(np.argmax(lengths))
        a, e = spans[i]
        mid = (a + e) // 2
        spans[i] = (a, mid)
        spans.insert(i + 1, (mid + 1, e))
    return spans[:n]


def main():
    from transformers import Wav2Vec2ForCTC, Wav2Vec2FeatureExtractor
    from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
    from Research.Speech.Phase1_4.PhoneInventory.inventory import PhonemeInventoryAdapter
    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

    t0 = time.perf_counter()
    feat = Wav2Vec2FeatureExtractor.from_pretrained(ALT_ID, local_files_only=True)
    model = Wav2Vec2ForCTC.from_pretrained(ALT_ID, local_files_only=True).eval()
    import json as _json
    from huggingface_hub import hf_hub_download
    vp = hf_hub_download(ALT_ID, "vocab.json", local_files_only=True)
    vocab = _json.load(open(vp, encoding="utf-8"))
    id2tok = {int(v): k for k, v in vocab.items()}
    blank = int(vocab.get("<pad>", 0))
    inv = PhonemeInventoryAdapter()
    canon_ids = {}
    for i, tok in id2tok.items():
        if tok in ("|", "", "<pad>", "[PAD]", "<s>", "</s>", "<unk>"):
            continue
        c = inv.normalize_symbol(tok)
        if c:
            canon_ids.setdefault(c, []).append(i)
    print(f"alt model loaded ({time.perf_counter()-t0:.0f}s), vocab={len(vocab)}, "
          f"classes={len(canon_ids)}, blank={blank}", flush=True)

    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()
    man = {m["utt_id"]: m for m in L.read_rows(L.SO_MANIFEST)}
    detail = _json.loads((L.SO / "resource/scores-detail.json").read_text(encoding="utf-8"))

    feature = {r["token_id"]: r for r in csv.DictReader(
        open(P23 / "artifacts/feature_matrix.csv", encoding="utf-8"))}
    ev = {}
    for c in ("lwe", "so762_dev", "so762_test", "so762_absent_dev"):
        for r in csv.DictReader(open(P21 / f"token_evidence_{c}.csv", encoding="utf-8")):
            ev[(r["token_id"], r["window_type"])] = r

    # deterministic small subset
    labeled = [r for r in feature.values() if r["truth"] != ""]
    no_ev = [r for r in labeled if r["truth"] == "1"
             and float(r["target_max_A"]) < 0.02 and r["corpus"] == "so762_dev"][:10]
    strong = [r for r in labeled if r["truth"] == "1"
              and float(r["target_max_A"]) >= 0.80 and r["corpus"] == "so762_dev"][:5]
    absent = [r for r in labeled if r["truth"] == "0"
              and r["corpus"] == "so762_absent_dev"][:5]
    lwe28 = [r for r in labeled if r["corpus"] == "lwe"]
    groups = ([(tid, "TYPE_B") for tid in TYPE_B]
              + [(r["token_id"], "lwe_labeled") for r in lwe28]
              + [(r["token_id"], "so762_no_evidence_present") for r in no_ev]
              + [(r["token_id"], "so762_strong_present") for r in strong]
              + [(r["token_id"], "so762_absent") for r in absent])

    rows = []
    for tid, group in groups:
        r = feature[tid]
        evp = ev[(tid, PRIMARY)]
        a, b = float(evp["audio_start"]), float(evp["audio_end"])
        if r["corpus"] == "lwe":
            src = L.find_source(r["speaker_id"], r["word"])
            if src is None:
                continue
            x = L.load_mono16(src)
            st = tgt.build(r["word"])
            target = pev.inv.arpa_seq_to_canon([str(v) for v in st.arpabet])
            j = len(target) - 1
        else:
            utt = tid.rsplit("_", 1)[0]
            spk = int(r["speaker_id"])
            wav = L.SO / "WAVE" / f"SPEAKER{spk:04d}" / f"{utt}.WAV"
            x, sr = sf.read(str(wav))
            if x.ndim > 1:
                x = x.mean(axis=1)
            x = x.astype(np.float32)
            words = L.parse_words_so762(detail[utt])
            ref, scores, wpos, ppos, wtext = L.flatten_so762(words)
            target = pev.inv.arpa_seq_to_canon(ref)
            j = int(tid.rsplit("_", 1)[1])
        seg = x[int(a * 16000):int(b * 16000)]
        if len(seg) < 400:
            continue
        tmp = TMPDIR / f"_p24_{os.getpid()}_{uuid.uuid4().hex[:8]}.wav"
        safe_write(tmp, seg, 16000)
        try:
            inp = feat(seg, sampling_rate=16000, return_tensors="pt").input_values
            with torch.no_grad():
                logits = model(inp).logits[0]
            probs = torch.softmax(logits, dim=-1).numpy().astype(np.float64)
        finally:
            try:
                tmp.unlink()
            except OSError:
                pass
        canon = target[j]
        ids = canon_ids.get(canon, [])
        p = probs[:, ids].sum(axis=1) if ids else np.zeros(probs.shape[0])
        spans = ctc_align(probs, target, canon_ids)
        s, e = spans[j]
        alt_max = float(p[s:e + 1].max())
        alt_global = float(p.max())
        pk = int(np.argmax(p[s:e + 1])) + s
        top1 = max(canon_ids, key=lambda c: probs[pk, canon_ids[c]].sum()) if canon_ids else ""
        rows.append({
            "token_id": tid, "group": group, "corpus": r["corpus"],
            "target_phone": canon,
            "primary_max_A": r["target_max_A"], "alt_max_A": round(alt_max, 5),
            "primary_global_max": r["max_D"], "alt_global_max": round(alt_global, 5),
            "alt_top1_at_span_peak": top1,
            "alt_top1_post": round(float(probs[pk, canon_ids[top1]].sum()), 5) if top1 else 0.0,
            "primary_decision": r["production_decision"],
            "delta_span": round(alt_max - float(r["target_max_A"]), 5),
            "note": f"span {s}-{e} of {probs.shape[0]}",
        })
        print(f"  {tid:18s} {group:26s} primary={float(r['target_max_A']):.4f} "
              f"alt={alt_max:.4f}", flush=True)

    L.write_rows(ART / "alt_encoder_counterfactual.csv", rows, FIELDS)
    summary = {}
    for g in ("TYPE_B", "lwe_labeled", "so762_no_evidence_present", "so762_strong_present",
              "so762_absent"):
        rs = [r for r in rows if r["group"] == g]
        if not rs:
            continue
        summary[g] = {
            "n": len(rs),
            "primary_max_median": round(float(np.median([r["primary_max_A"] for r in rs])), 5),
            "alt_max_median": round(float(np.median([r["alt_max_A"] for r in rs])), 5),
            "alt_finds_evidence_rate": round(sum(1 for r in rs if r["alt_max_A"] >= 0.10)
                                             / len(rs), 4),
            "primary_finds_evidence_rate": round(sum(1 for r in rs
                                                     if float(r["primary_max_A"]) >= 0.10)
                                                 / len(rs), 4),
        }
    (ART / "alt_encoder_summary.json").write_text(
        json.dumps({"model": ALT_ID, "local_files_only": True, "summary": summary,
                    "rows": len(rows)}, indent=2), encoding="utf-8")
    print("summary:", json.dumps(summary, indent=1))
    print("DONE alt_encoder_counterfactual")


if __name__ == "__main__":
    main()
