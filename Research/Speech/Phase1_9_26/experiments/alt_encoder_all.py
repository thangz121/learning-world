"""WP-1.9.26 Part 20 — alternative-encoder evidence for ALL local evaluation tokens.

Runs the second local espeak encoder (facebook/wav2vec2-lv-60-espeak-cv-ft,
already cached, apache-2.0) once per utterance over the WP-1.9.21 evaluation sets
and stores per-token alternative evidence. No training, no downloads, no
production use.
"""
from __future__ import annotations

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
import torch

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_26"
ART = OUT / "artifacts"
ART.mkdir(parents=True, exist_ok=True)
P21 = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"
ALT_ID = "facebook/wav2vec2-lv-60-espeak-cv-ft"
PRIMARY = "full"
TMPDIR = Path(os.environ.get("TEMP", r"C:\Users\ASUS\AppData\Local\Temp")) / "opencode"
TMPDIR.mkdir(parents=True, exist_ok=True)
CORPORA = ["lwe", "so762_dev", "so762_test", "so762_absent_dev"]

FIELDS = ["corpus", "token_id", "speaker_id", "word", "target_phone", "alt_max_A",
          "alt_mean_A", "alt_peak_frame", "alt_global_max", "alt_top1_at_peak",
          "alt_top1_post", "n_frames", "span_start", "span_end"]


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
    from huggingface_hub import hf_hub_download
    from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
    from Research.Speech.Phase1_4.PhoneInventory.inventory import PhonemeInventoryAdapter
    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

    t0 = time.perf_counter()
    feat = Wav2Vec2FeatureExtractor.from_pretrained(ALT_ID, local_files_only=True)
    model = Wav2Vec2ForCTC.from_pretrained(ALT_ID, local_files_only=True).eval()
    vp = hf_hub_download(ALT_ID, "vocab.json", local_files_only=True)
    vocab = json.load(open(vp, encoding="utf-8"))
    id2tok = {int(v): k for k, v in vocab.items()}
    inv = PhonemeInventoryAdapter()
    canon_ids = {}
    for i, tok in id2tok.items():
        if tok in ("|", "", "<pad>", "[PAD]", "<s>", "</s>", "<unk>"):
            continue
        c = inv.normalize_symbol(tok)
        if c:
            canon_ids.setdefault(c, []).append(i)
    print(f"alt model loaded ({time.perf_counter()-t0:.0f}s)", flush=True)

    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()
    detail = json.loads((L.SO / "resource/scores-detail.json").read_text(encoding="utf-8"))

    all_rows = []
    for corpus in CORPORA:
        rows = [r for r in csv.DictReader(
            open(P21 / f"token_evidence_{corpus}.csv", encoding="utf-8"))
            if r["window_type"] == PRIMARY]
        by_utt = defaultdict(list)
        for r in rows:
            by_utt[r["token_id"] if corpus == "lwe"
                   else r["token_id"].rsplit("_", 1)[0]].append(r)
        n_enc = 0
        for ui, (utt_id, items) in enumerate(sorted(by_utt.items())):
            first = items[0]
            sid = first["speaker_id"]
            if corpus == "lwe":
                target = L.corpus_targets_lwe(pev, tgt, first["word"])
                src = L.find_source(sid, first["word"])
                if src is None or not target:
                    continue
                x = L.load_mono16(src)
                focus = [{"token_id": first["token_id"], "j": len(target) - 1,
                          "word": first["word"]}]
            else:
                words = L.parse_words_so762(detail[utt_id])
                ref, scores, wpos, ppos, wtext = L.flatten_so762(words)
                target = pev.inv.arpa_seq_to_canon(ref)
                if len(target) != len(ref) or not target:
                    continue
                spk = int(sid)
                wav = L.SO / "WAVE" / f"SPEAKER{spk:04d}" / f"{utt_id}.WAV"
                x, sr = sf.read(str(wav))
                if x.ndim > 1:
                    x = x.mean(axis=1)
                x = x.astype(np.float32)
                focus = [{"token_id": r["token_id"], "j": int(r["target_phone_index_utt"]),
                          "word": r["word"]} for r in items]
            a, b = float(first["audio_start"]), float(first["audio_end"])
            seg = x[int(a * 16000):int(b * 16000)]
            if len(seg) < 400:
                continue
            tmp = TMPDIR / f"_p26_{os.getpid()}_{uuid.uuid4().hex[:8]}.wav"
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
            n_enc += 1
            for item in focus:
                j = item["j"]
                if j >= len(target):
                    continue
                canon = target[j]
                ids = canon_ids.get(canon, [])
                p = probs[:, ids].sum(axis=1) if ids else np.zeros(probs.shape[0])
                spans = ctc_align(probs, target, canon_ids)
                s, e = spans[j]
                pk = int(np.argmax(p[s:e + 1])) + s
                top1 = max(canon_ids, key=lambda c: probs[pk, canon_ids[c]].sum()) \
                    if canon_ids else ""
                all_rows.append({
                    "corpus": corpus, "token_id": item["token_id"], "speaker_id": sid,
                    "word": item["word"], "target_phone": canon,
                    "alt_max_A": round(float(p[s:e + 1].max()), 6),
                    "alt_mean_A": round(float(p[s:e + 1].mean()), 6),
                    "alt_peak_frame": pk, "alt_global_max": round(float(p.max()), 6),
                    "alt_top1_at_peak": top1,
                    "alt_top1_post": round(float(probs[pk, canon_ids[top1]].sum()), 6)
                    if top1 else 0.0,
                    "n_frames": int(probs.shape[0]), "span_start": int(s), "span_end": int(e),
                })
            if (ui + 1) % 40 == 0:
                print(f"  [{corpus}] utt {ui+1}/{len(by_utt)} enc={n_enc} "
                      f"rows={len(all_rows)} ({time.perf_counter()-t0:.0f}s)", flush=True)
        print(f"[{corpus}] DONE utt={len(by_utt)} enc={n_enc} rows={len(all_rows)} "
              f"({time.perf_counter()-t0:.0f}s)", flush=True)
    L.write_rows(ART / "alt_encoder_all.csv", all_rows, FIELDS)
    print("TOTAL rows", len(all_rows), "elapsed", round(time.perf_counter() - t0),
          flush=True)
    print("DONE alt_encoder_all")


if __name__ == "__main__":
    main()
