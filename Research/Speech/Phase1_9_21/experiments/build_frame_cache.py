"""WP-1.9.21 — reusable frame evidence cache builder (research-only).

Recomputes the frozen encoder ONLY for the exact WP-1.9.19 evaluation sets
(LWE 80 tokens; so762 dev/test/absent_dev utterances) and stores, for each
final-consonant focus token and each of the four pre-declared windows
(full/raw/pad100/pad250):

  - per-frame target posterior, top-1 phone, best competitor, blank, margin
  - candidate-region membership (A production span / B span+context / C final
    tail after the preceding-phone acoustic boundary / D conservative union)
  - position masking of earlier same-class occurrences (mandatory rule)
  - aggregation features (means, top-K, sparse widths, local cluster,
    peak prominence, temporal support)

No production code is imported for execution; PhoneEvidenceV2 is used read-only.
No raw child audio is written to the cache. Outputs:
  artifacts/frame_cache/frames_<corpus>.csv
  artifacts/frame_cache/token_evidence_<corpus>.csv
  artifacts/frame_cache/FRAME_CACHE_MANIFEST.json
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
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_21"
CACHE = OUT / "artifacts/frame_cache"

FRAME_FIELDS = [
    "corpus", "token_id", "speaker_id", "word", "target_phone", "frame", "time_ms",
    "target_posterior", "top1_phone", "top1_posterior", "best_competitor_posterior",
    "blank_posterior", "target_vs_competitor_margin", "window_type", "window_start",
    "window_end", "target_word_position", "target_phone_position", "n_frames", "frame_s",
    "in_current_span", "in_region_B", "in_region_C", "in_region_D",
    "in_earlier_same_class", "in_prev_phone", "in_next_phone",
    "prev_phone_posterior", "next_phone_posterior",
]

SUMMARY_FIELDS = [
    "corpus", "token_id", "speaker_id", "age", "word", "target_phone",
    "is_final_consonant", "human_label", "human_present", "human_score",
    "human_verdict", "window_type", "n_frames", "frame_s", "dur_s", "audio_start",
    "audio_end", "vad_start", "vad_end", "target_word_position",
    "target_phone_position", "target_phone_index_utt", "span_A_start", "span_A_end",
    "prev_span", "next_span", "earlier_same_spans", "n_earlier_same",
    "region_C0", "region_C1", "n_frames_D",
    "mean_A", "max_A", "region_top3_A", "mean_B", "max_B", "region_top3_B",
    "mean_C", "max_C", "region_top3_C", "mean_D", "max_D", "region_top3_D",
    "top1_D", "top2_D", "top3_D", "top5_D", "width1_D", "width2_D", "width3_D",
    "width5_D", "peak", "peak_frame", "cluster_width", "cluster_mean",
    "neighbor_support", "comp_peak", "blank_peak", "prom_comp", "prom_blank",
    "prom_neighbor", "temporal_support", "max_global", "argmax_global",
    "max_masked_earlier", "argmax_masked_earlier", "max_later_same",
    "argmax_later_same", "top1_at_global", "top1_post_at_global", "comp_at_global",
    "blank_at_global", "baseline_match", "baseline_present", "baseline_span_post",
    "baseline_best_obs", "baseline_sim", "da_span", "da_max", "da_frame",
    "da_free_present", "da_margin_per_frame",
]


def fnum(v, nd=5):
    return "" if v is None else round(float(v), nd)


TMPDIR = Path(os.environ.get("TEMP", r"C:\Users\ASUS\AppData\Local\Temp")) / "opencode"
TMPDIR.mkdir(parents=True, exist_ok=True)


def safe_write(path: Path, x, sr, tries=6):
    """soundfile write with retries (Windows file-lock flakiness seen in WP-1.9.19)."""
    last = None
    for k in range(tries):
        try:
            sf.write(str(path), x, sr)
            return
        except Exception as exc:  # noqa: BLE001
            last = exc
            time.sleep(0.2 * (k + 1))
    raise RuntimeError(f"sf.write failed after {tries} tries: {path} ({last})")


def human_present_from_label(label):
    if label in L.PRESENT_LABELS:
        return 1
    if label in L.ABSENT_LABELS:
        return 0
    return ""


def load_so_context():
    man = {m["utt_id"]: m for m in L.read_rows(L.SO_MANIFEST)}
    detail = json.loads((L.SO / "resource/scores-detail.json").read_text(encoding="utf-8"))
    return man, detail


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--corpus", default="all",
                    choices=["all", "lwe", "so762_dev", "so762_test", "so762_absent_dev"])
    ap.add_argument("--smoke", action="store_true")
    ap.add_argument("--limit", type=int, default=0)
    args = ap.parse_args()

    CACHE.mkdir(parents=True, exist_ok=True)

    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402
    pev = PhoneEvidenceV2()
    pev._ensure()
    tgt = L.CmuDictTargetAdapter()
    p2 = L.p2_module()
    man, detail = load_so_context()

    corpora = []
    if args.corpus in ("all", "lwe"):
        corpora.append(("lwe", None))
    if args.corpus in ("all", "so762_dev"):
        corpora.append(("so762_dev", "window_rows_so762_dev.csv"))
    if args.corpus in ("all", "so762_test"):
        corpora.append(("so762_test", "window_rows_so762_test.csv"))
    if args.corpus in ("all", "so762_absent_dev"):
        corpora.append(("so762_absent_dev", "window_rows_so762_absent_dev.csv"))

    stats = {}
    t_start = time.perf_counter()
    try:
        for corpus, csv_name in corpora:
            if corpus == "lwe":
                items = L.lwe_work_items()
            else:
                items = L.so762_work_items(csv_name, corpus)
            if args.smoke:
                items = items[: args.limit or 8]

            by_utt = defaultdict(list)
            for it in items:
                by_utt[it["utt_id"]].append(it)

            frame_path = CACHE / f"frames_{corpus}.csv"
            sum_path = CACHE / f"token_evidence_{corpus}.csv"
            ff = open(frame_path, "w", newline="", encoding="utf-8")
            sfw = open(sum_path, "w", newline="", encoding="utf-8")
            fw = csv.DictWriter(ff, fieldnames=FRAME_FIELDS)
            sw = csv.DictWriter(sfw, fieldnames=SUMMARY_FIELDS)
            fw.writeheader()
            sw.writeheader()

            n_utt_done = n_token_rows = n_frame_rows = 0
            n_enc = 0
            n_skip = 0
            mism = 0
            t0 = time.perf_counter()
            utt_list = sorted(by_utt.keys())
            for ui, utt_id in enumerate(utt_list):
                fitems = by_utt[utt_id]
                first = fitems[0]
                sid = first["speaker_id"]
                age = man.get(utt_id, {}).get("age", "") if corpus != "lwe" else ""
                if corpus == "lwe":
                    word = first["word"]
                    target = L.corpus_targets_lwe(pev, tgt, word)
                    if not target:
                        continue
                    src = L.find_source(sid, word)
                    if src is None:
                        n_skip += 1
                        continue
                    x = L.load_mono16(src)
                    focus = [{"token_id": first["token_id"], "j": len(target) - 1,
                              "word": word, "wpos": 1, "ppos": len(target),
                              "human_label": first["human_label"],
                              "human_present": human_present_from_label(first["human_label"]),
                              "human_score": "", "human_verdict": first["human_verdict"],
                              "windows": first["windows"]}]
                    wins = first["windows"]
                else:
                    words = L.parse_words_so762(detail[utt_id])
                    ref_flat, scores, wpos, ppos, wtext = L.flatten_so762(words)
                    target = pev.inv.arpa_seq_to_canon(ref_flat)
                    if len(target) != len(ref_flat) or not target:
                        n_skip += 1
                        continue
                    spk = int(sid)
                    wav = L.SO / "WAVE" / f"SPEAKER{spk:04d}" / f"{utt_id}.WAV"
                    x, sr = sf.read(str(wav))
                    if x.ndim > 1:
                        x = x.mean(axis=1)
                    x = x.astype(np.float32)
                    focus = []
                    for it in fitems:
                        j = it["focus_j"]
                        if j >= len(target):
                            continue
                        hp = it["human_score"]
                        focus.append({"token_id": it["token_id"], "j": j,
                                      "word": wtext[j], "wpos": wpos[j] + 1,
                                      "ppos": ppos[j] + 1, "human_label": "",
                                      "human_present": 1 if float(hp) >= 0.5 else 0,
                                      "human_score": hp, "human_verdict": "",
                                      "windows": it["windows"]})
                    if not focus:
                        continue
                    wins = first["windows"]

                for win in L.WINDOWS:
                    row0 = wins.get(win)
                    if not row0:
                        continue
                    a = float(row0["audio_start"])
                    b = float(row0["audio_end"])
                    seg = x[int(a * 16000):int(b * 16000)]
                    if len(seg) < 400:
                        continue
                    tmp = TMPDIR / f"_p21_{os.getpid()}_{uuid.uuid4().hex[:8]}.wav"
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
                    blank = probs[:, blank_id]
                    top1_idx = A_class.argmax(axis=1)
                    top1_post = A_class.max(axis=1)
                    da_all = L.deletion_all(probs, target, class_ids, blank_id, p2)
                    n_enc += 1

                    for item in focus:
                        j = item["j"]
                        canon = target[j]
                        ci = cindex.get(canon)
                        p = A_class[:, ci] if ci is not None else np.zeros(T)
                        if ci is not None and A_class.shape[1] > 1:
                            comp = np.max(np.delete(A_class, ci, axis=1), axis=1)
                        else:
                            comp = np.zeros(T)
                        prevp = (A_class[:, cindex[target[j - 1]]]
                                 if j > 0 and target[j - 1] in cindex else None)
                        nextp = (A_class[:, cindex[target[j + 1]]]
                                 if j + 1 < len(target) and target[j + 1] in cindex else None)
                        reg = L.regions_for(T, spans, target, j, A_class, cindex)
                        feats = L.support_features(p, comp, blank, reg, prevp, nextp)
                        base = L.baseline_final_decision(probs, spans, target, class_ids, pev, j)
                        da = L.deletion_for_focus(da_all, p, j)

                        # global / earlier / later same-class evidence
                        tg = int(np.argmax(p)) if T else -1
                        max_global = float(p[tg]) if tg >= 0 else 0.0
                        me, ae = 0.0, -1
                        if reg["M"].any():
                            idx = np.where(reg["M"])[0]
                            ae = int(idx[int(np.argmax(p[idx]))])
                            me = float(p[ae])
                        ml, al = 0.0, -1
                        for k in range(j + 1, len(target)):
                            if target[k] == canon and spans[k] is not None:
                                s, e = int(spans[k][0]), int(spans[k][1])
                                v = float(p[s:e + 1].max())
                                if v > ml:
                                    ml = v
                                    al = int(s + int(np.argmax(p[s:e + 1])))
                        prev_span = ("%d-%d" % tuple(reg["prev"])) if reg["prev"] is not None else ""
                        next_span = ("%d-%d" % tuple(reg["next"])) if reg["next"] is not None else ""
                        earlier_str = ";".join("%d-%d" % sp for _, sp in reg["earlier"])
                        A0, A1 = reg["A_span"]
                        prev_mask = np.zeros(T, dtype=bool)
                        if reg["prev"] is not None:
                            prev_mask[int(reg["prev"][0]):int(reg["prev"][1]) + 1] = True
                        next_mask = np.zeros(T, dtype=bool)
                        if reg["next"] is not None:
                            next_mask[int(reg["next"][0]):int(reg["next"][1]) + 1] = True

                        for t in range(reg["store_lo"], reg["store_hi"] + 1):
                            fw.writerow({
                                "corpus": corpus, "token_id": item["token_id"],
                                "speaker_id": sid, "word": item["word"],
                                "target_phone": canon, "frame": t,
                                "time_ms": fnum(t * frame_s * 1000.0, 2),
                                "target_posterior": fnum(p[t]),
                                "top1_phone": names[int(top1_idx[t])],
                                "top1_posterior": fnum(top1_post[t]),
                                "best_competitor_posterior": fnum(comp[t]),
                                "blank_posterior": fnum(blank[t]),
                                "target_vs_competitor_margin": fnum(p[t] - comp[t]),
                                "window_type": win,
                                "window_start": row0["audio_start"],
                                "window_end": row0["audio_end"],
                                "target_word_position": item["wpos"],
                                "target_phone_position": item["ppos"],
                                "n_frames": T, "frame_s": fnum(frame_s, 4),
                                "in_current_span": int(A0 <= t <= A1),
                                "in_region_B": int(reg["B"][t]),
                                "in_region_C": int(reg["C"][t]),
                                "in_region_D": int(reg["D"][t]),
                                "in_earlier_same_class": int(reg["M"][t]),
                                "in_prev_phone": int(prev_mask[t]),
                                "in_next_phone": int(next_mask[t]),
                                "prev_phone_posterior": fnum(prevp[t]) if prevp is not None else "",
                                "next_phone_posterior": fnum(nextp[t]) if nextp is not None else "",
                            })
                            n_frame_rows += 1

                        # determinism check against this token's own 1.9.19 row
                        row_i = item.get("windows", {}).get(win, row0)
                        if (abs(da.get("da_max", 0.0) - float(row_i["deletion_aware_evidence"])) > 2e-3
                                or base["match"] != row_i["baseline_match"]
                                or int(spans[j][0]) != int(row_i["span_start"])
                                or int(spans[j][1]) != int(row_i["span_end"])):
                            mism += 1

                        sw.writerow({
                            "corpus": corpus, "token_id": item["token_id"],
                            "speaker_id": sid, "age": age, "word": item["word"],
                            "target_phone": canon,
                            "is_final_consonant": int(canon not in L.VOWELS),
                            "human_label": item["human_label"],
                            "human_present": item["human_present"],
                            "human_score": item["human_score"],
                            "human_verdict": item["human_verdict"],
                            "window_type": win, "n_frames": T,
                            "frame_s": fnum(frame_s, 4), "dur_s": fnum(dur, 3),
                            "audio_start": row0["audio_start"], "audio_end": row0["audio_end"],
                            "vad_start": row0["vad_start"], "vad_end": row0["vad_end"],
                            "target_word_position": item["wpos"],
                            "target_phone_position": item["ppos"],
                            "target_phone_index_utt": j,
                            "span_A_start": A0, "span_A_end": A1,
                            "prev_span": prev_span, "next_span": next_span,
                            "earlier_same_spans": earlier_str,
                            "n_earlier_same": len(reg["earlier"]),
                            "region_C0": reg["C0"], "region_C1": reg["C1"],
                            "n_frames_D": feats["n_frames_D"],
                            "mean_A": fnum(feats["mean_A"]), "max_A": fnum(feats["max_A"]),
                            "region_top3_A": fnum(feats["region_top3_A"]),
                            "mean_B": fnum(feats["mean_B"]), "max_B": fnum(feats["max_B"]),
                            "region_top3_B": fnum(feats["region_top3_B"]),
                            "mean_C": fnum(feats["mean_C"]), "max_C": fnum(feats["max_C"]),
                            "region_top3_C": fnum(feats["region_top3_C"]),
                            "mean_D": fnum(feats["mean_D"]), "max_D": fnum(feats["max_D"]),
                            "region_top3_D": fnum(feats["region_top3_D"]),
                            "top1_D": fnum(feats["top1_D"]), "top2_D": fnum(feats["top2_D"]),
                            "top3_D": fnum(feats["top3_D"]), "top5_D": fnum(feats["top5_D"]),
                            "width1_D": fnum(feats["width1_D"]),
                            "width2_D": fnum(feats["width2_D"]),
                            "width3_D": fnum(feats["width3_D"]),
                            "width5_D": fnum(feats["width5_D"]),
                            "peak": fnum(feats["peak"]), "peak_frame": feats["peak_frame"],
                            "cluster_width": feats["cluster_width"],
                            "cluster_mean": fnum(feats["cluster_mean"]),
                            "neighbor_support": fnum(feats["neighbor_support"]),
                            "comp_peak": fnum(feats["comp_peak"]),
                            "blank_peak": fnum(feats["blank_peak"]),
                            "prom_comp": fnum(feats["prom_comp"]),
                            "prom_blank": fnum(feats["prom_blank"]),
                            "prom_neighbor": fnum(feats["prom_neighbor"]),
                            "temporal_support": fnum(feats["temporal_support"]),
                            "max_global": fnum(max_global), "argmax_global": tg,
                            "max_masked_earlier": fnum(me), "argmax_masked_earlier": ae,
                            "max_later_same": fnum(ml), "argmax_later_same": al,
                            "top1_at_global": names[int(top1_idx[tg])] if tg >= 0 else "",
                            "top1_post_at_global": fnum(top1_post[tg]) if tg >= 0 else "",
                            "comp_at_global": fnum(comp[tg]) if tg >= 0 else "",
                            "blank_at_global": fnum(blank[tg]) if tg >= 0 else "",
                            "baseline_match": base["match"],
                            "baseline_present": int(base["match"] in ("exact", "soft")),
                            "baseline_span_post": fnum(base["post"]),
                            "baseline_best_obs": base["best_obs"],
                            "baseline_sim": fnum(base["sim"]),
                            "da_span": ("%d-%d" % da["da_span"]) if da.get("da_span") else "",
                            "da_max": fnum(da.get("da_max", 0.0)),
                            "da_frame": da.get("da_frame", -1),
                            "da_free_present": da.get("free_present", ""),
                            "da_margin_per_frame": fnum(da.get("margin_per_frame", 0.0), 6),
                        })
                        n_token_rows += 1

                n_utt_done += 1
                if (ui + 1) % 10 == 0 or args.smoke:
                    el = time.perf_counter() - t0
                    print(f"  [{corpus}] utt {ui+1}/{len(utt_list)} enc={n_enc} "
                          f"rows={n_token_rows} ({el:.0f}s)", flush=True)
            ff.close()
            sfw.close()
            stats[corpus] = {"utterances": n_utt_done, "encodings": n_enc,
                             "token_rows": n_token_rows, "frame_rows": n_frame_rows,
                             "skipped": n_skip, "determinism_mismatch_vs_1919": mism,
                             "frames_csv": frame_path.name, "summary_csv": sum_path.name}
            print(f"[{corpus}] DONE utt={n_utt_done} enc={n_enc} tokens={n_token_rows} "
                  f"frames={n_frame_rows} mism={mism} "
                  f"({time.perf_counter()-t0:.0f}s)", flush=True)
    finally:
        pass

    write_manifest(stats)
    print("TOTAL", json.dumps(stats, indent=1), flush=True)
    print(f"elapsed {time.perf_counter()-t_start:.0f}s", flush=True)


def write_manifest(stats=None):
    model_dir = Path(r"D:\speech-lab\models") / "models--facebook--wav2vec2-xlsr-53-espeak-cv-ft"
    snaps = sorted(p.name for p in (model_dir / "snapshots").iterdir()) if model_dir.exists() else []
    snap = snaps[0] if snaps else ""
    prov_inputs = {
        "window_rows_lwe": L.P1919 / "window_rows_lwe.csv",
        "window_rows_so762_dev": L.P1919 / "window_rows_so762_dev.csv",
        "window_rows_so762_test": L.P1919 / "window_rows_so762_test.csv",
        "window_rows_so762_absent_dev": L.P1919 / "window_rows_so762_absent_dev.csv",
        "final_consonant_human_review": L.P1912 / "final_consonant_human_review.csv",
        "p1_evidence_table": L.REPO / "Research/Speech/Phase1_9_14/artifacts/p1/p1_evidence_table.csv",
        "so762_manifest": L.SO_MANIFEST,
        "so762_scores_detail": L.SO / "resource/scores-detail.json",
    }
    prov = {k: {"path": str(p), "sha256": L.sha256_file(p)} if p.exists() else
            {"path": str(p), "missing": True} for k, p in prov_inputs.items()}
    snap_prov = {}
    if snap:
        for name in ("preprocessor_config.json", "vocab.json", "config.json"):
            p = model_dir / "snapshots" / snap / name
            snap_prov[name] = L.sha256_file(p) if p.exists() else None
    cache_files = {}
    for p in sorted(CACHE.glob("*.csv")):
        rows = 0
        with open(p, encoding="utf-8") as f:
            for _ in f:
                rows += 1
        cache_files[p.name] = {"sha256": L.sha256_file(p), "rows_incl_header": rows,
                               "bytes": p.stat().st_size}
    manifest = {
        "phase": "1.9.21",
        "purpose": "reusable frame-level evidence cache for final-consonant support aggregation",
        "created": time.strftime("%Y-%m-%dT%H:%M:%S"),
        "machine": "ASUS",
        "model": {
            "id": "facebook/wav2vec2-xlsr-53-espeak-cv-ft",
            "snapshot": snap,
            "snapshots_present": snaps,
            "file_hashes": snap_prov,
            "phone_evidence_v2_version": "1.4.0",
            "phone_inventory_version": "phone-inventory-v1.4.0",
        },
        "preprocessing": {
            "resample_to": 16000,
            "mono_mixdown": True,
            "feature_extractor": "Wav2Vec2FeatureExtractor (model snapshot preprocessor_config.json)",
            "write_format": "WAV via soundfile default subtype (same as WP-1.9.19/1.9.20)",
        },
        "windows": L.WINDOWS,
        "candidate_regions": {
            "A": "production ctc_align span (PhoneEvidenceV2.ctc_align, read-only)",
            "B": f"production span +/- {L.KB} frames (100 ms), minus earlier same-class spans",
            "C": "final tail after the preceding phone's acoustic boundary (last frame inside "
                 "the preceding span with preceding-class posterior >= 50% of its span max), "
                 f"extending at most {L.KC} frames into the following phone's span, minus "
                 "earlier same-class spans",
            "D": "conservative union B | C",
            "position_masking": "earlier same-class spans (same canonical phone, earlier index) "
                                "are removed from every region and reported separately as "
                                "WRONG_OCCURRENCE candidates",
        },
        "aggregation_constants": {
            "TOPK": list(L.TOPK), "WIDTHS": list(L.WIDTHS),
            "TS_MARGIN_SCALE": L.TS_MARGIN_SCALE, "TS_COHERENCE_WIDTH": L.TS_COHERENCE_WIDTH,
            "TS_HALF_FRAC": L.TS_HALF_FRAC, "TS_FLOOR": L.TS_FLOOR,
        },
        "cache_format": {
            "frames_csv": list(FRAME_FIELDS),
            "summary_csv": list(SUMMARY_FIELDS),
            "note": "frames stored from preceding-phone span start (or span-100ms) to "
                    "following-phone span end (or span+100ms) plus earlier same-class spans; "
                    "raw child audio is never stored",
        },
        "inputs": prov,
        "outputs": cache_files,
        "run_stats": stats or {},
        "flags": {"production_vad": False, "router_locked": False, "unity_integrated": False,
                  "scorer_modified": False, "production_window_locked": False},
    }
    (CACHE / "FRAME_CACHE_MANIFEST.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    print("manifest ->", CACHE / "FRAME_CACHE_MANIFEST.json", flush=True)


if __name__ == "__main__":
    main()
