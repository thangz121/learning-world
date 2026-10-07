"""WP-1.9.22 Experiment 1 — exact production decision reconstruction.

The WP-1.9.21 frame cache stores the decision-relevant production outputs
(baseline_match/post/best_obs/sim) but not the span-mean top-5 raw tokens.
This targeted, primary-window-only recompute (one encoder run per utterance,
reusing the exact same audio/window construction) restores the full step-by-step
decision path: top-1/top-2 phones, target rank, competitor, blank, identity
margin. Research-only; no production change.
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

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_22"
CACHE = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"
ART = OUT / "artifacts"
ART.mkdir(parents=True, exist_ok=True)
PRIMARY = "full"
TMPDIR = Path(os.environ.get("TEMP", r"C:\Users\ASUS\AppData\Local\Temp")) / "opencode"
TMPDIR.mkdir(parents=True, exist_ok=True)

FIELDS = [
    "corpus", "token_id", "speaker_id", "word", "target_phone", "window_type",
    "n_frames", "frame_s", "span_start", "span_end",
    "target_post_mean", "target_max_A", "blank_mean_span",
    "top1_phone", "top1_post", "top2_phone", "top2_post", "top3_phone", "top3_post",
    "top4_phone", "top4_post", "top5_phone", "top5_post",
    "target_rank_in_top5", "target_in_top5", "identity_credit",
    "best_obs", "best_sim", "match_type", "production_decision",
    "competitor_phone", "competitor_post", "identity_margin_mean", "identity_ratio_mean",
    "peak_frame", "peak_post", "peak_comp_phone", "peak_competitor", "peak_margin",
    "peak_blank", "human_label", "human_present",
]


def safe_write(path, x, sr, tries=6):
    last = None
    for k in range(tries):
        try:
            sf.write(str(path), x, sr)
            return
        except Exception as exc:  # noqa: BLE001
            last = exc
            time.sleep(0.2 * (k + 1))
    raise RuntimeError(f"sf.write failed after {tries} tries: {path} ({last})")


def main():
    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402
    pev = PhoneEvidenceV2()
    pev._ensure()
    tgt = L.CmuDictTargetAdapter()
    man = {m["utt_id"]: m for m in L.read_rows(L.SO_MANIFEST)}
    detail = json.loads((L.SO / "resource/scores-detail.json").read_text(encoding="utf-8"))

    corpora = [("lwe", None), ("so762_dev", "window_rows_so762_dev.csv"),
               ("so762_test", "window_rows_so762_test.csv"),
               ("so762_absent_dev", "window_rows_so762_absent_dev.csv")]
    stats = {}
    for corpus, _ in corpora:
        rows = list(csv.DictReader(open(CACHE / f"token_evidence_{corpus}.csv", encoding="utf-8")))
        prim = [r for r in rows if r["window_type"] == PRIMARY]
        by_utt = defaultdict(list)
        for r in prim:
            if corpus == "lwe":
                by_utt[r["token_id"]].append(r)
            else:
                by_utt[r["token_id"].rsplit("_", 1)[0]].append(r)
        out_rows = []
        n_enc = 0
        t0 = time.perf_counter()
        for ui, (utt_id, items) in enumerate(sorted(by_utt.items())):
            first = items[0]
            sid = first["speaker_id"]
            if corpus == "lwe":
                word = first["word"]
                target = L.corpus_targets_lwe(pev, tgt, word)
                src = L.find_source(sid, word)
                if src is None or not target:
                    continue
                x = L.load_mono16(src)
                focus = [{"token_id": first["token_id"], "j": len(target) - 1,
                          "word": word, "human_label": first["human_label"],
                          "human_present": first["human_present"]}]
            else:
                words = L.parse_words_so762(detail[utt_id])
                ref_flat, scores, wpos, ppos, wtext = L.flatten_so762(words)
                target = pev.inv.arpa_seq_to_canon(ref_flat)
                if len(target) != len(ref_flat) or not target:
                    continue
                spk = int(sid)
                wav = L.SO / "WAVE" / f"SPEAKER{spk:04d}" / f"{utt_id}.WAV"
                x, sr = sf.read(str(wav))
                if x.ndim > 1:
                    x = x.mean(axis=1)
                x = x.astype(np.float32)
                focus = [{"token_id": r["token_id"], "j": int(r["target_phone_index_utt"]),
                          "word": r["word"], "human_label": "", "human_present": r["human_present"]}
                         for r in items]
            a = float(first["audio_start"])
            b = float(first["audio_end"])
            seg = x[int(a * 16000):int(b * 16000)]
            if len(seg) < 400:
                continue
            tmp = TMPDIR / f"_p22_{os.getpid()}_{uuid.uuid4().hex[:8]}.wav"
            safe_write(tmp, seg, 16000)
            try:
                probs_t, dur, _ = pev.logits(str(tmp))
            finally:
                try:
                    tmp.unlink()
                except OSError:
                    pass
            probs = probs_t.numpy().astype(np.float64)
            T = probs.shape[0]
            frame_s = dur / max(1, T)
            spans = pev.ctc_align(probs_t, target)
            names, class_ids, A_class = L.build_class_matrix(pev, probs)
            cindex = {c: i for i, c in enumerate(names)}
            blank_id = pev._blank
            n_enc += 1
            for item in focus:
                j = item["j"]
                canon = target[j]
                s, e = int(spans[j][0]), int(spans[j][1])
                segm = probs[s:e + 1].mean(axis=0)
                top = np.argsort(segm)[-5:][::-1]
                topk = []
                for i in top:
                    c = pev.inv.normalize_symbol(pev._id2tok.get(int(i), "?"))
                    if c:
                        topk.append((c, float(segm[i])))
                ids = class_ids.get(canon, [])
                post = float(segm[ids].sum()) if ids else 0.0
                cm = A_class[s:e + 1].mean(axis=0)
                ci = cindex.get(canon)
                if ci is not None and A_class.shape[1] > 1:
                    comp_arr = np.delete(cm, ci)
                    comp_idx = np.delete(np.arange(A_class.shape[1]), ci)
                    k = int(np.argmax(comp_arr))
                    comp_phone = names[int(comp_idx[k])]
                    comp_post = float(comp_arr[k])
                else:
                    comp_phone, comp_post = "", 0.0
                blank_mean = float(segm[blank_id])
                # production rule (exact replica)
                best_obs = topk[0][0] if topk else ""
                best_sim = L.phone_similarity(canon, best_obs)
                for obs, pv in topk:
                    sim = L.phone_similarity(canon, obs)
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
                p = A_class[:, ci] if ci is not None else np.zeros(T)
                pk = int(np.argmax(p[s:e + 1])) + s
                top5_phones = [c for c, _ in topk]
                rank = (top5_phones.index(canon) if canon in top5_phones else -1)
                out_rows.append({
                    "corpus": corpus, "token_id": item["token_id"], "speaker_id": sid,
                    "word": item["word"], "target_phone": canon, "window_type": PRIMARY,
                    "n_frames": T, "frame_s": round(frame_s, 4),
                    "span_start": s, "span_end": e,
                    "target_post_mean": round(post, 6),
                    "target_max_A": round(float(p[s:e + 1].max()), 6),
                    "blank_mean_span": round(blank_mean, 6),
                    "top1_phone": topk[0][0] if topk else "",
                    "top1_post": round(topk[0][1], 6) if topk else 0.0,
                    "top2_phone": topk[1][0] if len(topk) > 1 else "",
                    "top2_post": round(topk[1][1], 6) if len(topk) > 1 else 0.0,
                    "top3_phone": topk[2][0] if len(topk) > 2 else "",
                    "top3_post": round(topk[2][1], 6) if len(topk) > 2 else 0.0,
                    "top4_phone": topk[3][0] if len(topk) > 3 else "",
                    "top4_post": round(topk[3][1], 6) if len(topk) > 3 else 0.0,
                    "top5_phone": topk[4][0] if len(topk) > 4 else "",
                    "top5_post": round(topk[4][1], 6) if len(topk) > 4 else 0.0,
                    "target_rank_in_top5": rank, "target_in_top5": int(rank >= 0),
                    "identity_credit": int(best_obs == canon),
                    "best_obs": best_obs, "best_sim": round(float(sim), 5),
                    "match_type": match,
                    "production_decision": int(match in ("exact", "soft")),
                    "competitor_phone": comp_phone, "competitor_post": round(comp_post, 6),
                    "identity_margin_mean": round(post - comp_post, 6),
                    "identity_ratio_mean": round(post / (comp_post + 1e-9), 4),
                    "peak_frame": pk, "peak_post": round(float(p[pk]), 6),
                    "peak_comp_phone": names[int(np.delete(np.arange(A_class.shape[1]), ci)[
                        int(np.argmax(np.delete(A_class[pk], ci)))])] if ci is not None else "",
                    "peak_competitor": round(float(np.max(np.delete(A_class[pk], ci))) if ci is not None else 0.0, 6),
                    "peak_margin": round(float(p[pk] - (np.max(np.delete(A_class[pk], ci)) if ci is not None else 0.0)), 6),
                    "peak_blank": round(float(probs[pk, blank_id]), 6),
                    "human_label": item["human_label"],
                    "human_present": item["human_present"],
                })
            if (ui + 1) % 40 == 0:
                print(f"  [{corpus}] utt {ui+1}/{len(by_utt)} enc={n_enc} "
                      f"({time.perf_counter()-t0:.0f}s)", flush=True)
        # verification vs cache (primary window)
        cache_map = {(r["token_id"], r["window_type"]): r for r in rows}
        mism = 0
        details = []
        for r in out_rows:
            c = cache_map.get((r["token_id"], PRIMARY))
            if c is None:
                continue
            if (r["match_type"] != c["baseline_match"]
                    or abs(r["target_post_mean"] - float(c["baseline_span_post"])) > 2e-3
                    or int(r["span_start"]) != int(c["span_A_start"])
                    or int(r["span_end"]) != int(c["span_A_end"])
                    or r["best_obs"] != c["baseline_best_obs"]):
                mism += 1
                if len(details) < 20:
                    details.append(f"{r['token_id']}: {r['match_type']}/{r['target_post_mean']} "
                                   f"vs {c['baseline_match']}/{c['baseline_span_post']} "
                                   f"obs {r['best_obs']} vs {c['baseline_best_obs']}")
        L.write_rows(ART / f"spanmean_decision_{corpus}.csv", out_rows, FIELDS)
        stats[corpus] = {"utterances": len(by_utt), "encodings": n_enc,
                         "tokens": len(out_rows), "cache_mismatches": mism,
                         "mismatch_details": details}
        print(f"[{corpus}] tokens={len(out_rows)} enc={n_enc} "
              f"cache_mismatches={mism} ({time.perf_counter()-t0:.0f}s)", flush=True)
    (ART / "experiment1_reproduction.json").write_text(
        json.dumps(stats, indent=2), encoding="utf-8")
    print("DONE rebuild_spanmean_decision")


if __name__ == "__main__":
    main()
