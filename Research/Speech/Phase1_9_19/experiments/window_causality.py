"""WP-1.9.19 — boundary/window causality audit (research-only, no production change).

Same frozen model, same target, same evidence code; the ONLY experimental
variable is the audio context supplied to the encoder.

Window family (pre-declared, physically meaningful at 20 ms CTC frames):
  full       [0, dur]
  raw        VAD union [segs[0].start, segs[-1].end]           (1.9.11 construction)
  pad50/100/250/500   raw +/- symmetric padding
  right100/right250   raw with right context only
  left100             raw with left context only
  shift_left100/shift_right100  same length as raw, translated +/-100 ms

Per window: frozen logits -> baseline decision (replicated soft_match for the
final phone) + deletion-aware evidence E (da_span_max), margin, free presence,
top-1 phone, absolute frame time. Human labels are attached, never changed.

Outputs: artifacts/window_rows_lwe.csv, window_rows_so762_dev.csv,
         window_rows_so762_test.csv (plus VAD cache)
"""
from __future__ import annotations

import argparse
import csv
import importlib.util
import json
import os
import re
import sys
import time
from pathlib import Path

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(REPO))
OUT = REPO / "Research/Speech/Phase1_9_19"
ART = OUT / "artifacts"
ART.mkdir(parents=True, exist_ok=True)

from Research.Speech.Phase1_9.HybridVAD.hybrid_vad import HybridVAD  # noqa: E402
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402
from Research.Speech.Phase1_4.PhoneInventory.inventory import phone_similarity  # noqa: E402

spec = importlib.util.spec_from_file_location(
    "p2del", REPO / "Research/Speech/Phase1_9_14/experiments/p2_deletion_aware.py")
p2 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p2)

P198 = REPO / "Research/Speech/Phase1_9_8/Results"
P1912 = REPO / "Research/Speech/Phase1_9_12/Results"
SO = Path(r"D:\speech-lab\data\speechocean762")
SO_MANIFEST = REPO / "Research/Speech/Phase1_9_16/artifacts/audit/so762_manifest.csv"
ZEN = REPO / "Research/Speech/ExternalData/zenodo_200495"
DER = ZEN / "derived_16k"
VOWELS = {"ɑ", "æ", "ə", "ɔ", "aʊ", "aɪ", "ɛ", "ɝ", "eɪ", "ɪ", "iː", "oʊ", "ɔɪ", "ʊ", "uː",
          "ʌ", "ɑː", "e", "o", "i", "u", "a", "ɐ", "ɜ", "ɚ", "ɞ"}

LWE_WINDOWS = ["full", "raw", "pad50", "pad100", "pad250", "pad500",
               "right100", "right250", "left100", "shift_left100", "shift_right100"]
SO_WINDOWS = ["full", "raw", "pad100", "pad250", "pad500", "right250"]


def sha256_file(p: Path) -> str:
    import hashlib
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def load_mono16(path: Path) -> np.ndarray:
    key = sha256_file(path)[:16] + "_" + re.sub(r"[^A-Za-z0-9_.-]+", "_", path.name)[:60]
    cache = DER / f"{key}.wav"
    if cache.exists():
        x, _ = sf.read(str(cache))
    else:
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


def _unique_tmp(tmpdir: Path):
    import uuid
    return tmpdir / f"_tmp_{os.getpid()}_{uuid.uuid4().hex[:10]}.wav"


def _safe_write(path: Path, x, sr, tries=4):
    for k in range(tries):
        try:
            sf.write(str(path), x, sr)
            return path
        except Exception:  # noqa: BLE001
            time.sleep(0.15 * (k + 1))
    raise RuntimeError(f"sf.write failed after {tries} tries: {path}")


def vad_bounds(x, sr, hv, tmpdir):
    tmp = _unique_tmp(tmpdir)
    try:
        _safe_write(tmp, x, sr)
        rs = hv.run(str(tmp), mode="silero", silero_thr=0.5)
        segs = rs["segments"]
        if segs:
            return float(segs[0]["start"]), float(segs[-1]["end"]), len(segs)
    except Exception as exc:  # noqa: BLE001
        print("VAD_FAIL", type(exc).__name__, flush=True)
    finally:
        try:
            tmp.unlink()
        except Exception:  # noqa: BLE001
            pass
    return 0.0, len(x) / sr, 0


def window_bounds(cond, dur, v0, v1):
    if cond == "full":
        return 0.0, dur
    if cond == "raw":
        return v0, v1
    if cond.startswith("pad"):
        p = int(cond[3:]) / 1000.0
        return max(0.0, v0 - p), min(dur, v1 + p)
    if cond.startswith("right"):
        p = int(cond[5:]) / 1000.0
        return v0, min(dur, v1 + p)
    if cond.startswith("left"):
        p = int(cond[4:]) / 1000.0
        return max(0.0, v0 - p), v1
    if cond == "shift_left100":
        return max(0.0, v0 - 0.1), max(0.0, v1 - 0.1)
    if cond == "shift_right100":
        return min(dur, v0 + 0.1), min(dur, v1 + 0.1)
    raise ValueError(cond)


def baseline_final_decision(probs, spans, target, class_ids, pev, j):
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
    elif best_sim >= 0.35:
        match = "soft"
    else:
        match = "miss"
    top1_phone, top1_post = (topk[0] if topk else ("", 0.0))
    return {"match": match, "post": post, "top1": top1_phone, "top1_post": top1_post,
            "span": (int(s), int(e))}


def analyze_window(pev, class_ids, x, sr, s0, s1, arpa, target, j, tmpdir):
    seg = x[int(s0 * sr):int(s1 * sr)]
    if len(seg) < 400:  # <25 ms -> not evaluable
        return None
    tmp = _unique_tmp(tmpdir)
    try:
        _safe_write(tmp, seg, sr)
        probs_t, dur, _ = pev.logits(str(tmp))
    finally:
        try:
            tmp.unlink()
        except Exception:  # noqa: BLE001
            pass
    probs = probs_t.numpy().astype(np.float64)
    T = probs.shape[0]
    frame_s = dur / max(1, T)
    spans = pev.ctc_align(probs_t, target)
    base = baseline_final_decision(probs, spans, target, class_ids, pev, j)
    canon = target[j]
    ids = class_ids.get(canon, [])
    p = probs[:, ids].sum(axis=1) if ids else np.zeros(T)
    emit, names, emit_blank = p2.normalized_emissions(probs, class_ids, pev._blank)
    idx = {n: k for k, n in enumerate(names)}
    emit_all = emit[:, [idx[c] for c in target]]
    dm = p2.deletion_margin(emit_all, emit_blank, len(target))
    margin = dm["margin"] / max(1, T)
    spans_da = p2.spans_from_path(dm["path_present"], len(target))
    da_span = spans_da[j] if spans_da and spans_da[j] else None
    da_max = float(p[da_span[0]:da_span[1] + 1].max()) if da_span else 0.0
    da_frame = int(da_span[0] + int(np.argmax(p[da_span[0]:da_span[1] + 1]))) if da_span else -1
    return {
        "duration_ms": round(len(seg) / sr * 1000, 1), "n_frames": T,
        "frame_s": round(frame_s, 4),
        "baseline_match": base["match"],
        "span_start": base["span"][0], "span_end": base["span"][1],
        "span_post": round(base["post"], 4),
        "target_frame": da_frame, "target_time_abs": round(s0 + max(0, da_frame) * frame_s, 4),
        "target_posterior": round(da_max, 4),
        "frame_max": round(float(p.max()), 4),
        "top1_phone": base["top1"], "top1_posterior": round(base["top1_post"], 4),
        "margin": round(margin, 5), "free_present": int(dm["present_free"]),
        "deletion_aware_evidence": round(da_max, 4),
        "research_decision": "PRESENT" if da_max >= 0.30 else "ABSENT",
    }


def run_lwe(pev, class_ids, hv, tokens=None):
    if tokens is None:
        tokens = list(csv.DictReader((P198 / "pronunciation_results.csv").open(encoding="utf-8-sig")))
    fc = {r["case_id"]: r["human_final_label"] for r in csv.DictReader(
        (P1912 / "final_consonant_human_review.csv").open(encoding="utf-8-sig"))}
    p1 = {(r["speaker_id"], r["target"]): r for r in csv.DictReader(
        (REPO / "Research/Speech/Phase1_9_14/artifacts/p1/p1_evidence_table.csv").open(encoding="utf-8"))}
    tgt = CmuDictTargetAdapter()
    rows = []
    t0 = time.perf_counter()
    for i, r in enumerate(tokens):
        sid, word = r["speaker_id"], r["target"]
        src = find_source(sid, word)
        if src is None:
            continue
        x = load_mono16(src)
        dur = len(x) / 16000.0
        st = tgt.build(word)
        arpa = [str(v) for v in st.arpabet]
        target = pev.inv.arpa_seq_to_canon(arpa)
        v0, v1, nsegs = vad_bounds(x, 16000, hv, ART)
        human = fc.get(f"fc_{sid}_{word}", "")
        hp = 1 if human.endswith("PRESENT") else (0 if human.endswith("ABSENT") else "")
        for cond in LWE_WINDOWS:
            a, b = window_bounds(cond, dur, v0, v1)
            ev = analyze_window(pev, class_ids, x, 16000, a, b, arpa, target,
                                len(target) - 1, ART)
            if ev is None:
                continue
            rows.append({
                "token_id": f"{sid}_{word}", "speaker_id": sid, "word": word,
                "target_phone": target[-1], "human_label": human, "human_present": hp,
                "human_verdict": p1.get((sid, word), {}).get("human_verdict", ""),
                "window_type": cond,
                "left_context_ms": round(max(0.0, v0 - a) * 1000, 1),
                "right_context_ms": round(max(0.0, b - v1) * 1000, 1),
                "audio_start": round(a, 4), "audio_end": round(b, 4),
                "vad_start": round(v0, 4), "vad_end": round(v1, 4), "vad_nsegs": nsegs,
                **ev})
        if (i + 1) % 10 == 0:
            print(f"  lwe {i+1}/{len(tokens)} ({time.perf_counter()-t0:.0f}s)", flush=True)
    return rows


def run_so762(pev, class_ids, hv, speakers, tag, max_utt_per_spk=8, utt_ids=None):
    detail = json.loads((SO / "resource/scores-detail.json").read_text(encoding="utf-8"))
    man = list(csv.DictReader(SO_MANIFEST.open(encoding="utf-8")))
    rows = []
    per_spk = {}
    t0 = time.perf_counter()
    n_utt = 0
    for m in man:
        if m["is_child"] != "1" or m["speaker_id"] not in speakers:
            continue
        if utt_ids is not None and m["utt_id"] not in utt_ids:
            continue
        if per_spk.get(m["speaker_id"], 0) >= max_utt_per_spk:
            continue
        per_spk[m["speaker_id"]] = per_spk.get(m["speaker_id"], 0) + 1
        spk = int(m["speaker_id"])
        wav = SO / "WAVE" / f"SPEAKER{spk:04d}" / f"{m['utt_id']}.WAV"
        x, sr = sf.read(str(wav))
        if x.ndim > 1:
            x = x.mean(axis=1)
        x = x.astype(np.float32)
        dur = len(x) / sr
        v0, v1, nsegs = vad_bounds(x, sr, hv, ART)
        words = detail[m["utt_id"]]["words"]
        ref_flat, scores, words_of, fin_flags = [], [], [], []
        for wi, w in enumerate(words):
            ref = (w.get("ref-phones") or "").split()
            exp = w.get("phones") or []
            per = [[] for _ in ref]
            for es in exp:
                j, ok = 0, True
                for t in es.split():
                    if t.startswith("[") and t.endswith("]"):
                        continue
                    if j >= len(ref):
                        ok = False
                        break
                    sc = 0 if (t.startswith("(") and t.endswith(")")) else (
                        1 if (t.startswith("{") and t.endswith("}")) else 2)
                    per[j].append(sc)
                    j += 1
            ss = [round(sum(v) / len(v), 3) if v and all(x != "?" for x in v) else "" for v in per]
            for pi, (ph, s) in enumerate(zip(ref, ss)):
                ref_flat.append(ph)
                scores.append(s)
                words_of.append(wi)
                fin_flags.append(pi == len(ref) - 1)
        if not ref_flat:
            continue
        target = pev.inv.arpa_seq_to_canon(ref_flat)
        n_utt += 1
        for j, (canon, fin, sc) in enumerate(zip(target, fin_flags, scores)):
            if not fin or canon in VOWELS or sc == "":
                continue
            for cond in SO_WINDOWS:
                a, b = window_bounds(cond, dur, v0, v1)
                ev = analyze_window(pev, class_ids, x, sr, a, b, ref_flat, target, j, ART)
                if ev is None:
                    continue
                rows.append({
                    "token_id": f"{m['utt_id']}_{j}", "speaker_id": m["speaker_id"],
                    "age": m["age"], "target_phone": canon, "human_phone_score": float(sc),
                    "human_present": 1 if float(sc) >= 0.5 else 0,
                    "split": tag, "window_type": cond,
                    "left_context_ms": round(max(0.0, v0 - a) * 1000, 1),
                    "right_context_ms": round(max(0.0, b - v1) * 1000, 1),
                    "audio_start": round(a, 4), "audio_end": round(b, 4),
                    "vad_start": round(v0, 4), "vad_end": round(v1, 4), "vad_nsegs": nsegs,
                    **ev})
        if n_utt % 20 == 0:
            print(f"  {tag} utt {n_utt} ({time.perf_counter()-t0:.0f}s)", flush=True)
    print(f"{tag}: {n_utt} utt, {len(rows)} rows ({time.perf_counter()-t0:.0f}s)", flush=True)
    return rows


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--smoke", action="store_true")
    ap.add_argument("--stage", default="all", choices=["all", "lwe", "so762"])
    args = ap.parse_args()
    pev = PhoneEvidenceV2()
    pev._ensure()
    class_ids = {c: ids for c, ids in pev._canon_ids.items() if c != "sil"}
    hv = HybridVAD()

    global LWE_WINDOWS, SO_WINDOWS
    if args.smoke:
        LWE_WINDOWS = ["full", "raw", "pad100", "right250"]
        SO_WINDOWS = ["full", "raw"]
    if args.stage in ("all", "lwe"):
        tokens = list(csv.DictReader((P198 / "pronunciation_results.csv").open(encoding="utf-8-sig")))
        if args.smoke:
            tokens = tokens[:6]
        lwe = run_lwe(pev, class_ids, hv, tokens=tokens)
        with open(ART / "window_rows_lwe.csv", "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=list(lwe[0].keys()))
            w.writeheader()
            w.writerows(lwe)

    if args.stage in ("all", "so762"):
        man = list(csv.DictReader(SO_MANIFEST.open(encoding="utf-8")))
        n_spk = 1 if args.smoke else 12
        cap = 2 if args.smoke else 8
        dev_spk = sorted({m["speaker_id"] for m in man if m["split"] == "train" and m["is_child"] == "1"})[:n_spk]
        test_spk = sorted({m["speaker_id"] for m in man if m["split"] == "test" and m["is_child"] == "1"})[:n_spk]
        for spk_list, tag in ((dev_spk, "so762_dev"), (test_spk, "so762_test")):
            rows = run_so762(pev, class_ids, hv, set(spk_list), tag, max_utt_per_spk=cap)
            with open(ART / f"window_rows_{tag}.csv", "w", newline="", encoding="utf-8") as f:
                w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
                w.writeheader()
                w.writerows(rows)
    print("DONE")


if __name__ == "__main__":
    main()
