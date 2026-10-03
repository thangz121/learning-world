"""Phase 1.9.9 — child pronunciation phone-level audit (research only)."""
from __future__ import annotations

import csv
import hashlib
import json
import math
import re
import sys
import time
from collections import Counter, defaultdict
from dataclasses import asdict, is_dataclass
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_9.HybridVAD.hybrid_vad import HybridVAD
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

OUT = REPO / "Research/Speech/Phase1_9_9"
RES = OUT / "Results"
HR = OUT / "HumanReview"
CLIPS = HR / "clips"
DER = REPO / "Research/Speech/ExternalData/zenodo_200495/derived_16k"
P198 = REPO / "Research/Speech/Phase1_9_8/Results/pronunciation_results.csv"
EXT = REPO / "Research/Speech/ExternalData/zenodo_200495/extracted/english_children"
for d in (OUT, RES, HR, CLIPS, DER, OUT / "Scripts"):
    d.mkdir(parents=True, exist_ok=True)

NUMBERS = {"one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten"}


def sha256(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest().upper()


def ser(o: Any):
    if is_dataclass(o) and hasattr(o, "__dataclass_fields__"):
        return {k: ser(getattr(o, k)) for k in o.__dataclass_fields__}
    if isinstance(o, (list, tuple)):
        return [ser(x) for x in o]
    if isinstance(o, dict):
        return {k: ser(v) for k, v in o.items()}
    if isinstance(o, (np.floating, np.integer)):
        return float(o) if isinstance(o, np.floating) else int(o)
    if isinstance(o, (int, float, str, bool)) or o is None:
        return o
    return str(o)


def load_mono16_from_path(path: Path) -> Tuple[np.ndarray, int, Path]:
    key = sha256(path)[:16] + "_" + re.sub(r"[^A-Za-z0-9_.-]+", "_", path.name)[:60]
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
    sf.write(str(cache), x, sr)
    return x, sr, cache


def find_source_wav(speaker_id: str, target: str, mic: str = "studio_mic") -> Optional[Path]:
    # speaker_id child_01 -> 01_...
    m = re.match(r"child_(\d+)", speaker_id or "")
    if not m:
        return None
    nn = m.group(1)
    base = EXT / "english_words_sentences"
    if not base.exists():
        return None
    for sp in base.iterdir():
        if sp.name.startswith(nn + "_"):
            cand = sp / mic / "numbers" / f"{target}.wav"
            if cand.exists():
                return cand
            # any mic
            hits = list(sp.rglob(f"numbers/{target}.wav"))
            if hits:
                return hits[0]
    return None


def f0_proxy(x: np.ndarray, sr: int) -> Dict:
    # autocorrelation rough F0 median (research proxy only)
    if len(x) < sr // 10:
        return dict(f0_median=None, f0_range=None, method="too_short")
    frame = int(0.04 * sr)
    hop = int(0.01 * sr)
    f0s = []
    min_lag = int(sr / 500)
    max_lag = int(sr / 80)
    for i in range(0, max(1, len(x) - frame), hop):
        w = x[i : i + frame]
        w = w - np.mean(w)
        if np.std(w) < 1e-6:
            continue
        corr = np.correlate(w, w, mode="full")
        corr = corr[len(corr) // 2 :]
        seg = corr[min_lag:max_lag]
        if len(seg) == 0:
            continue
        lag = int(np.argmax(seg)) + min_lag
        if corr[lag] > 0.3 * corr[0]:
            f0s.append(sr / lag)
    if not f0s:
        return dict(f0_median=None, f0_range=None, method="acorr_fail")
    return dict(
        f0_median=float(np.median(f0s)),
        f0_range=float(np.percentile(f0s, 90) - np.percentile(f0s, 10)),
        method="acorr_proxy",
    )


def spectral_feats(x: np.ndarray, sr: int) -> Dict:
    if len(x) < 64:
        return dict(rms=0.0, centroid=None, flatness=None)
    spec = np.abs(np.fft.rfft(x * np.hanning(len(x)))) + 1e-12
    freqs = np.fft.rfftfreq(len(x), 1 / sr)
    e = np.sum(spec)
    centroid = float(np.sum(freqs * spec) / e)
    geo = float(np.exp(np.mean(np.log(spec))))
    ar = float(np.mean(spec))
    flat = geo / ar
    rms = float(np.sqrt(np.mean(x**2) + 1e-20))
    return dict(rms=rms, centroid=centroid, flatness=flat)


def write_csv(path: Path, rows: List[Dict]):
    if not rows:
        path.write_text("empty\n", encoding="utf-8")
        return
    keys = []
    for r in rows:
        for k in r:
            if k not in keys:
                keys.append(k)
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)


def band(score: float) -> str:
    if score >= 80:
        return "HIGH"
    if score >= 50:
        return "MID"
    if score >= 20:
        return "LOW"
    return "VERY_LOW"


def main():
    assert P198.exists(), P198
    base_rows = list(csv.DictReader(P198.open(encoding="utf-8")))
    base_rows = [r for r in base_rows if r.get("soft_full") not in (None, "")]
    scores = [float(r["soft_full"]) for r in base_rows]
    mean_s = sum(scores) / len(scores)
    frac50 = sum(1 for s in scores if s < 50) / len(scores)
    print(f"BASELINE n={len(scores)} mean={mean_s:.4f} frac<50={frac50:.4f}", flush=True)
    repro_ok = abs(mean_s - 63.90375) < 1.0 and abs(frac50 - 0.275) < 0.02 and len(scores) == 80
    if not repro_ok:
        # still continue if close; hard stop only if wild
        if abs(mean_s - 63.9) > 5 or len(scores) < 70:
            raise SystemExit(f"BASELINE_MISMATCH mean={mean_s} n={len(scores)} frac={frac50}")

    baseline_meta = dict(
        baseline_commit="3ca915b",
        baseline_file=str(P198.relative_to(REPO)).replace("\\", "/"),
        baseline_subset="phase1.9.8_studio_numbers_cap80",
        n=len(scores),
        mean_soft_full=mean_s,
        frac_lt_50=frac50,
        reproduction_ok=bool(repro_ok),
        note="Reproduced from Phase 1.9.8 CSV (same frozen scorer outputs)",
    )
    write_csv(
        RES / "baseline_reproduction.csv",
        [dict(metric=k, value=v) for k, v in baseline_meta.items()],
    )

    # enrich inventory
    inv = []
    for r in base_rows:
        sc = float(r["soft_full"])
        conf = float(r["conf_full"]) if r.get("conf_full") not in (None, "") else None
        sil0 = float(r["soft_silero_raw"]) if r.get("soft_silero_raw") not in (None, "") else None
        sil250 = float(r["soft_silero_pad250"]) if r.get("soft_silero_pad250") not in (None, "") else None
        hyb0 = float(r["soft_hybrid_raw"]) if r.get("soft_hybrid_raw") not in (None, "") else None
        hyb250 = float(r["soft_hybrid_pad250"]) if r.get("soft_hybrid_pad250") not in (None, "") else None
        delta = None if sil0 is None else round(sil0 - sc, 2)
        inv.append(
            dict(
                speaker_id=r.get("speaker_id"),
                recording_id=r.get("recording_id"),
                target=r.get("target"),
                mic=r.get("mic"),
                full_score=sc,
                full_confidence=conf,
                score_band=band(sc),
                silero_segments=r.get("silero_n"),
                hybrid_segments=r.get("hybrid_n"),
                silero_raw_score=sil0,
                silero_pad250_score=sil250,
                hybrid_raw_score=hyb0,
                hybrid_pad250_score=hyb250,
                delta_full_vs_raw=delta,
                abs_delta_boundary=None if delta is None else abs(delta),
            )
        )
    write_csv(RES / "low_score_inventory.csv", sorted(inv, key=lambda x: x["full_score"]))

    # balanced sample
    by_band = defaultdict(list)
    for r in inv:
        by_band[r["score_band"]].append(r)
    for b in by_band:
        by_band[b] = sorted(by_band[b], key=lambda x: x["full_score"])
    sample = []
    # prefer diversity: take up to 10 each
    for b in ("VERY_LOW", "LOW", "MID", "HIGH"):
        pool = by_band.get(b, [])
        # diversify by word then child
        picked = []
        used_words = Counter()
        used_child = Counter()
        # sort VERY_LOW ascending, HIGH descending for variety
        order = pool if b != "HIGH" else list(reversed(pool))
        for r in order:
            if len(picked) >= 10:
                break
            if used_words[r["target"]] >= 3 and len(order) > 10:
                continue
            if used_child[r["speaker_id"]] >= 3 and len(order) > 10:
                continue
            picked.append(r)
            used_words[r["target"]] += 1
            used_child[r["speaker_id"]] += 1
        # fill remaining
        for r in order:
            if len(picked) >= 10:
                break
            if r not in picked:
                picked.append(r)
        sample.extend(picked)

    # also add large boundary sensitivity not already in sample
    bound = sorted(
        [r for r in inv if r.get("abs_delta_boundary") is not None],
        key=lambda x: -x["abs_delta_boundary"],
    )[:8]
    ids = {(r["speaker_id"], r["target"]) for r in sample}
    for r in bound:
        k = (r["speaker_id"], r["target"])
        if k not in ids:
            sample.append(r)
            ids.add(k)

    print("SAMPLE", len(sample), Counter(r["score_band"] for r in sample), flush=True)

    hv = HybridVAD()
    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()
    tmp = RES / "_tmp16.wav"

    # try ASR
    asr_fn = None
    try:
        from moonshine_onnx import MoonshineOnnxModel, load_tokenizer

        asr_model = MoonshineOnnxModel(model_name="moonshine/tiny")
        asr_tok = load_tokenizer()

        def asr_fn(wav16: np.ndarray):
            audio = wav16.astype(np.float32)
            tokens = asr_model.generate(audio[np.newaxis, :])
            text = asr_tok.decode_batch(tokens)[0]
            return text.strip()

    except Exception as e:
        print("ASR_UNAVAILABLE", e, flush=True)

    review_meta = []
    phone_diag = []
    boundary_diag = []
    acoustic_rows = []
    asr_conflict = []
    pad_rows = []

    pads = [0.0, 0.1, 0.2, 0.25, 0.3, 0.5]

    for i, item in enumerate(sample):
        sid, target = item["speaker_id"], item["target"]
        src = find_source_wav(sid, target)
        if src is None:
            print("MISSING", sid, target, flush=True)
            continue
        x, sr, cache = load_mono16_from_path(src)
        dur = len(x) / sr
        sf.write(str(tmp), x, sr)
        st = tgt.build(target)
        full = pev.soft_match(str(tmp), st.arpabet)
        hits = ser(full.hits)
        r_sil = hv.run(str(tmp), mode="silero", silero_thr=0.5)
        r_hyb = hv.run(str(tmp), mode="hybrid_score", silero_thr=0.5)
        sil_segs = r_sil["segments"]
        hyb_segs = r_hyb["segments"]

        def crop(segs, pad, out_path):
            if not segs:
                # full utterance as fallback window
                s0, s1 = 0.0, dur
            else:
                s0 = max(0.0, float(segs[0]["start"]) - pad)
                s1 = min(dur, float(segs[-1]["end"]) + pad)
            # ensure min 2s listen for review export separately
            sf.write(str(out_path), x[int(s0 * sr) : int(s1 * sr)], sr)
            return s0, s1

        rid = f"{sid}_{target}_{i:02d}"
        full_p = CLIPS / f"{rid}_FULL.wav"
        raw_p = CLIPS / f"{rid}_RAW.wav"
        pad_p = CLIPS / f"{rid}_PAD250.wav"
        hyb_p = CLIPS / f"{rid}_HYB.wav"
        sf.write(str(full_p), x, sr)
        rs, re_ = crop(sil_segs, 0.0, raw_p)
        ps, pe = crop(sil_segs, 0.25, pad_p)
        hs, he = crop(hyb_segs, 0.25, hyb_p)

        # listen window >=2s centered on full or pad
        mid = dur / 2
        half = max(1.0, dur / 2)
        ls = max(0.0, mid - half)
        le = min(dur, mid + half)
        if le - ls < 2.0 and dur >= 2.0:
            ls, le = 0.0, min(dur, 2.0)
        listen_p = CLIPS / f"{rid}_LISTEN.wav"
        sf.write(str(listen_p), x[int(ls * sr) : int(le * sr)], sr)

        # pad sweep scores
        pad_scores = {}
        for p in pads:
            cpath = RES / f"_pad.wav"
            if not sil_segs:
                s0, s1 = 0.0, dur
            else:
                s0 = max(0.0, float(sil_segs[0]["start"]) - p)
                s1 = min(dur, float(sil_segs[-1]["end"]) + p)
            sf.write(str(cpath), x[int(s0 * sr) : int(s1 * sr)], sr)
            sc = pev.soft_match(str(cpath), st.arpabet)
            pad_scores[f"pad_{int(p*1000)}"] = sc.soft_score_0_100
            pad_rows.append(
                dict(
                    speaker_id=sid,
                    target=target,
                    pad_ms=int(p * 1000),
                    score=sc.soft_score_0_100,
                    conf=sc.confidence_0_1,
                    full_score=full.soft_score_0_100,
                )
            )

        # ASR
        asr_text = ""
        asr_status = "ASR_UNAVAILABLE"
        if asr_fn is not None:
            try:
                asr_text = asr_fn(x) or ""
                tl = asr_text.lower().strip()
                if not tl:
                    asr_status = "ASR_EMPTY"
                elif target in tl.split() or target == tl:
                    asr_status = "ASR_CORRECT"
                else:
                    asr_status = "ASR_WRONG"
            except Exception as e:
                asr_status = f"ASR_ERROR:{e}"

        # phone summary
        n_miss = sum(1 for h in hits if h.get("match_type") == "miss")
        n_exact = sum(1 for h in hits if h.get("match_type") == "exact")
        phone_seq = " ".join(str(h.get("best_obs")) for h in hits)
        canon = " ".join(st.arpabet)

        # boundary diagnostic from phone spans + sil segs
        bdiag = "BOUNDARY_UNCLEAR"
        if not sil_segs:
            bdiag = "NO_SILERO_SEG"
        else:
            # if first phone starts near 0 and last ends near end of crop
            if hits:
                if float(hits[0].get("start_s") or 0) <= 0.02 and float(hits[-1].get("end_s") or 0) >= (
                    float(sil_segs[-1]["end"]) - float(sil_segs[0]["start"]) - 0.05
                ):
                    bdiag = "BOUNDARY_OK_PROXY"
                elif float(hits[0].get("start_s") or 0) > 0.05:
                    bdiag = "POSSIBLE_START_ISSUE"
                else:
                    bdiag = "BOUNDARY_CHECK_NEEDED"

        # pattern FULL high RAW low PAD high
        pattern = ""
        if sil0 is None:
            sil0 = item.get("silero_raw_score")
        sil250 = item.get("silero_pad250_score")
        if sil0 is not None and sil250 is not None:
            if full.soft_score_0_100 >= 70 and sil0 < 50 and sil250 >= 70:
                pattern = "FULL_HIGH_RAW_LOW_PAD_HIGH"
            elif full.soft_score_0_100 < 50 and (sil0 is None or sil0 < 50) and sil250 < 50:
                pattern = "ALL_LOW"
            elif full.soft_score_0_100 >= 70 and sil0 is not None and sil0 >= 70:
                pattern = "ALL_HIGHISH"

        ac = f0_proxy(x, sr)
        sp = spectral_feats(x, sr)
        acoustic_rows.append(
            dict(
                speaker_id=sid,
                target=target,
                full_score=full.soft_score_0_100,
                score_band=item["score_band"],
                duration_s=dur,
                **ac,
                **sp,
            )
        )

        phone_diag.append(
            dict(
                speaker_id=sid,
                target=target,
                canonical=canon,
                observed_phone_evidence=phone_seq,
                n_exact=n_exact,
                n_miss=n_miss,
                soft_score=full.soft_score_0_100,
                confidence=full.confidence_0_1,
                conf_reasons=";".join(full.conf_reasons or []),
                mean_posterior=full.mean_posterior,
                hits_json=json.dumps(hits, ensure_ascii=True),
                boundary_proxy=bdiag,
                pad_pattern=pattern,
                diagnostic="UNRESOLVED_PENDING_HUMAN",
            )
        )
        boundary_diag.append(
            dict(
                speaker_id=sid,
                target=target,
                silero_n=r_sil["n_segments"],
                hybrid_n=r_hyb["n_segments"],
                raw_start=rs,
                raw_end=re_,
                pad250_start=ps,
                pad250_end=pe,
                full_score=full.soft_score_0_100,
                **{k: pad_scores.get(k) for k in pad_scores},
                boundary_pattern=pattern,
                boundary_diagnostic=bdiag,
            )
        )
        asr_conflict.append(
            dict(
                speaker_id=sid,
                target=target,
                asr_status=asr_status,
                asr_text=asr_text,
                soft_score=full.soft_score_0_100,
                n_phone_miss=n_miss,
                human_pronunciation="",  # blind later
            )
        )

        review_meta.append(
            dict(
                review_id=rid,
                speaker_id=sid,
                recording_id=item.get("recording_id"),
                age_metadata="dataset_mean_M=4.9y; individual UNKNOWN",
                target=target,
                score_band=item["score_band"],
                # scores hidden in blind UI
                full_score=full.soft_score_0_100,
                full_confidence=full.confidence_0_1,
                silero_raw_score=item.get("silero_raw_score"),
                silero_pad250_score=item.get("silero_pad250_score"),
                hybrid_pad250_score=item.get("hybrid_pad250_score"),
                canonical=canon,
                phone_evidence=phone_seq,
                n_phone_miss=n_miss,
                asr_status=asr_status,
                asr_text=asr_text,
                boundary_pattern=pattern,
                clips=dict(
                    LISTEN=f"clips/{rid}_LISTEN.wav",
                    FULL=f"clips/{rid}_FULL.wav",
                    RAW=f"clips/{rid}_RAW.wav",
                    PAD250=f"clips/{rid}_PAD250.wav",
                    HYB=f"clips/{rid}_HYB.wav",
                ),
                # human fields empty
                human_pronunciation="",
                phoneme_error="",
                boundary_label="",
                audio_quality="",
                review_confidence="",
                stage_a_done=False,
            )
        )
        print(
            f"OK {rid} score={full.soft_score_0_100:.1f} miss={n_miss} asr={asr_status} pat={pattern}",
            flush=True,
        )

    write_csv(RES / "phone_diagnostics.csv", phone_diag)
    write_csv(RES / "boundary_diagnostics.csv", boundary_diag)
    write_csv(RES / "acoustic_analysis.csv", acoustic_rows)
    write_csv(RES / "asr_phone_conflicts.csv", asr_conflict)
    write_csv(RES / "padding_sweep.csv", pad_rows)
    (HR / "review_metadata.json").write_text(
        json.dumps(
            dict(
                n=len(review_meta),
                blind_hides=["full_score", "full_confidence", "asr_text", "phone_evidence", "canonical"],
                label_schema=dict(
                    pronunciation=["CLEAR_CORRECT", "PROBABLY_CORRECT", "AMBIGUOUS", "PROBABLY_INCORRECT", "CLEAR_INCORRECT"],
                    phoneme_error=["NONE", "SUBSTITUTION", "DELETION", "INSERTION", "DISTORTION", "MULTIPLE", "UNKNOWN"],
                    boundary=["BOUNDARY_OK", "START_CUT", "END_CUT", "BOTH_CUT", "BOUNDARY_UNCLEAR"],
                    audio_quality=["CLEAR", "NOISY", "REVERBERANT", "QUIET", "DISTORTED", "UNCERTAIN"],
                    review_confidence=["HIGH", "MEDIUM", "LOW"],
                ),
                items=review_meta,
            ),
            indent=2,
            ensure_ascii=True,
        ),
        encoding="utf-8",
    )

    # empty human results template
    human_rows = []
    for m in review_meta:
        human_rows.append(
            dict(
                review_id=m["review_id"],
                speaker_id=m["speaker_id"],
                target=m["target"],
                score_band=m["score_band"],
                full_score=m["full_score"],
                human_pronunciation="",
                phoneme_error="",
                boundary_label="",
                audio_quality="",
                review_confidence="",
                notes="",
                reviewer_id="",
                review_date="",
            )
        )
    write_csv(RES / "human_review_results.csv", human_rows)

    # word-level
    word_rows = []
    by_w = defaultdict(list)
    for r in inv:
        by_w[r["target"]].append(r)
    for w, lst in sorted(by_w.items()):
        scs = [x["full_score"] for x in lst]
        confs = [x["full_confidence"] for x in lst if x["full_confidence"] is not None]
        deltas = [x["abs_delta_boundary"] for x in lst if x["abs_delta_boundary"] is not None]
        word_rows.append(
            dict(
                target=w,
                n=len(scs),
                mean=float(np.mean(scs)),
                median=float(np.median(scs)),
                std=float(np.std(scs)),
                min=float(min(scs)),
                max=float(max(scs)),
                frac_lt_50=float(np.mean([1 if s < 50 else 0 for s in scs])),
                frac_lt_20=float(np.mean([1 if s < 20 else 0 for s in scs])),
                mean_confidence=float(np.mean(confs)) if confs else None,
                mean_abs_boundary_delta=float(np.mean(deltas)) if deltas else None,
                human_correct_rate="",
                human_incorrect_rate="",
                human_ambiguous_rate="",
            )
        )
    write_csv(RES / "word_level_results.csv", word_rows)

    # child-level
    child_rows = []
    by_c = defaultdict(list)
    for r in inv:
        by_c[r["speaker_id"]].append(r)
    for c, lst in sorted(by_c.items()):
        scs = [x["full_score"] for x in lst]
        child_rows.append(
            dict(
                speaker_id=c,
                n_tokens=len(scs),
                mean_score=float(np.mean(scs)),
                median_score=float(np.median(scs)),
                std_score=float(np.std(scs)),
                mean_confidence=float(np.mean([x["full_confidence"] for x in lst if x["full_confidence"] is not None])),
                fraction_low=float(np.mean([1 if s < 50 else 0 for s in scs])),
                fraction_very_low=float(np.mean([1 if s < 20 else 0 for s in scs])),
                human_correct_fraction="",
                human_incorrect_fraction="",
                human_ambiguous_fraction="",
            )
        )
    write_csv(RES / "child_level_results.csv", child_rows)

    # LWE vocab coverage
    lwe_audio = REPO / "Research/Speech/Phase1_1/audio"
    lwe_words = sorted({re.sub(r"^sapi_|\.wav$", "", p.name) for p in lwe_audio.glob("sapi_*.wav")})
    # also multiword
    lwe_words = [w.replace("_", " ") for w in lwe_words]
    child_targets = set(r["target"] for r in inv)
    # zenodo also has sentences - inventory from filesystem numbers only in 80-set
    all_zenodo_words = set(NUMBERS)
    # sentence words
    for p in (EXT / "english_words_sentences").rglob("sentences/*.wav"):
        for tok in p.stem.split("_"):
            if tok not in ("the", "is", "of", "in", "on", "to", "a"):
                all_zenodo_words.add(tok)
    cov = []
    for w in lwe_words:
        hit = w in all_zenodo_words or w in child_targets
        cov.append(
            dict(
                lwe_vocab_item=w,
                in_zenodo_numbers_or_sentence_tokens=hit,
                in_phase198_scored_subset=w in child_targets,
            )
        )
    n_hit = sum(1 for r in cov if r["in_zenodo_numbers_or_sentence_tokens"])
    write_csv(RES / "lwe_child_vocab_coverage.csv", cov)

    # pitch correlation proxy on acoustic rows
    hi = [r for r in acoustic_rows if r["full_score"] >= 80 and r.get("f0_median")]
    lo = [r for r in acoustic_rows if r["full_score"] < 50 and r.get("f0_median")]
    pitch_note = "PITCH_EFFECT_NOT_IDENTIFIED"
    if len(hi) >= 5 and len(lo) >= 5:
        pitch_note = (
            f"descriptive f0_med HIGH mean={np.mean([r['f0_median'] for r in hi]):.1f} "
            f"LOW mean={np.mean([r['f0_median'] for r in lo]):.1f} (proxy only; not causal)"
        )

    # human metrics pending
    human_correct_low = None
    human_incorrect_high = None
    human_ambiguous = None
    n_human = 0

    # matrix empty
    write_csv(
        RES / "human_vs_score_matrix.csv",
        [
            dict(
                note="PENDING_HUMAN_BLIND_REVIEW",
                human_correct_low_score_rate="",
                human_incorrect_high_score_rate="",
                human_ambiguous_rate="",
            )
        ],
    )
    write_csv(
        RES / "scorer_failure_cases.csv",
        [
            dict(
                status="PENDING_HUMAN",
                note="Cases where human CLEAR_CORRECT & score<50 will be listed after review",
            )
        ],
    )

    decision = "C. EVIDENCE_INSUFFICIENT"
    rationale = [
        "Automated forensics and blind-review pack complete",
        "Human pronunciation labels not yet filled — cannot compute human_correct_low_score_rate",
        "Do not choose A/B without human ear authority",
    ]

    counts = Counter(r["score_band"] for r in inv)
    master = dict(
        phase="1.9.9",
        baseline=baseline_meta,
        inventory_counts=dict(counts),
        sample_n=len(review_meta),
        human_reviewed=n_human,
        human_correct_low_score_rate=human_correct_low,
        human_incorrect_high_score_rate=human_incorrect_high,
        human_ambiguous_rate=human_ambiguous,
        lwe_vocabulary_coverage=dict(
            n_lwe_items=len(cov),
            n_overlap_zenodo_tokens=n_hit,
            coverage_frac=n_hit / len(cov) if cov else 0,
            ZENODO_TARGET_COVERAGE="LIMITED",
            REAL_CHILD_LWE_VOCABULARY_DATA="PARTIAL_NUMBERS_ONLY",
        ),
        pitch=pitch_note,
        formant="FORMANT_EFFECT_NOT_IDENTIFIED",
        decision=decision,
        production_vad=False,
        router_locked=False,
        unity_integrated=False,
        asr_is_not_pronunciation_judge=True,
        rationale=rationale,
    )
    (RES / "phase_1_9_9_master.json").write_text(json.dumps(master, indent=2), encoding="utf-8")
    (RES / "decision.json").write_text(
        json.dumps(
            dict(
                decision=decision,
                production_vad=False,
                router_locked=False,
                unity_integrated=False,
                reason="Human blind review labels required before A/B",
            ),
            indent=2,
        ),
        encoding="utf-8",
    )

    # build HTML
    build_html(review_meta)
    print("DECISION", decision)
    print("SAMPLE_CLIPS", len(review_meta))
    try:
        tmp.unlink()
        (RES / "_pad.wav").unlink()
    except Exception:
        pass


def build_html(items: List[Dict]):
    # blind
    cards = []
    for it in items:
        rid = it["review_id"]
        opts_p = "".join(
            f'<label><input type="radio" name="p_{rid}" value="{v}"> {v}</label> '
            for v in [
                "CLEAR_CORRECT",
                "PROBABLY_CORRECT",
                "AMBIGUOUS",
                "PROBABLY_INCORRECT",
                "CLEAR_INCORRECT",
            ]
        )
        opts_b = "".join(
            f'<label><input type="radio" name="b_{rid}" value="{v}"> {v}</label> '
            for v in ["BOUNDARY_OK", "START_CUT", "END_CUT", "BOTH_CUT", "BOUNDARY_UNCLEAR"]
        )
        opts_q = "".join(
            f'<label><input type="radio" name="q_{rid}" value="{v}"> {v}</label> '
            for v in ["CLEAR", "NOISY", "REVERBERANT", "QUIET", "DISTORTED", "UNCERTAIN"]
        )
        opts_c = "".join(
            f'<label><input type="radio" name="c_{rid}" value="{v}"> {v}</label> '
            for v in ["HIGH", "MEDIUM", "LOW"]
        )
        cards.append(
            f"""<section class="card" id="{rid}">
<h2>{rid}</h2>
<p><b>Target:</b> {it['target'].upper()} &nbsp; speaker={it['speaker_id']} &nbsp; age={it['age_metadata']}</p>
<p>Listen (prefer FULL / LISTEN). Judge pronunciation by ear only — scores hidden.</p>
<p>LISTEN <audio controls preload="none" src="{it['clips']['LISTEN']}"></audio></p>
<p>FULL <audio controls preload="none" src="{it['clips']['FULL']}"></audio></p>
<p>RAW crop <audio controls preload="none" src="{it['clips']['RAW']}"></audio></p>
<p>PAD250 <audio controls preload="none" src="{it['clips']['PAD250']}"></audio></p>
<p>HYB pad <audio controls preload="none" src="{it['clips']['HYB']}"></audio></p>
<p>Pronunciation: {opts_p}</p>
<p>Boundary: {opts_b}</p>
<p>Audio quality: {opts_q}</p>
<p>Review confidence: {opts_c}</p>
<p>Notes: <input name="n_{rid}" size="50"></p>
</section>"""
        )
    ids = json.dumps([it["review_id"] for it in items])
    blind = f"""<!DOCTYPE html><html><head><meta charset=utf-8><title>1.9.9 Blind Review</title>
<style>body{{font-family:system-ui,sans-serif;max-width:820px;margin:20px auto;padding:0 12px;line-height:1.4}}
.card{{border:1px solid #ccc;border-radius:8px;padding:12px;margin:12px 0}}audio{{width:100%}}
.banner{{background:#fff3cd;padding:12px;border-radius:8px}}</style></head><body>
<div class="banner"><h1>Stage A — Blind pronunciation review</h1>
<p>Scores, confidence, ASR, and phone evidence are <b>hidden</b>. Listen first.</p>
<p>Central question: ignoring the number, does the child sound acceptably close to the target English word?</p>
</div>
{''.join(cards)}
<p><button id="exp">Export Stage A CSV</button></p>
<pre id="out"></pre>
<script>
const ids={ids};
document.getElementById('exp').onclick=()=>{{
  let lines=['review_id,human_pronunciation,boundary_label,audio_quality,review_confidence,notes'];
  for (const id of ids){{
    const g=n=>{{const el=document.querySelector('input[name="'+n+'_'+id+'"]:checked'); return el?el.value:'';}};
    const notes=(document.querySelector('input[name="n_'+id+'"]')||{{value:''}}).value.replace(/,/g,';');
    lines.push([id,g('p'),g('b'),g('q'),g('c'),notes].join(','));
  }}
  const t=lines.join('\\n'); document.getElementById('out').textContent=t;
  const a=document.createElement('a'); a.href=URL.createObjectURL(new Blob([t],{{type:'text/csv'}}));
  a.download='Human_Review_StageA_Filled.csv'; a.click();
}};
</script></body></html>"""
    (HR / "review_blind.html").write_text(blind, encoding="utf-8")

    # reveal
    cards2 = []
    for it in items:
        rid = it["review_id"]
        cards2.append(
            f"""<section class="card"><h2>{rid}</h2>
<p>Target <b>{it['target']}</b> speaker={it['speaker_id']}</p>
<audio controls preload="none" src="{it['clips']['FULL']}"></audio>
<pre>score={it['full_score']} conf={it['full_confidence']}
ASR={it['asr_status']} text={it.get('asr_text')}
canonical={it['canonical']}
phone={it['phone_evidence']} miss={it['n_phone_miss']}
boundary_pattern={it['boundary_pattern']}
sil_raw={it['silero_raw_score']} sil_pad250={it['silero_pad250_score']}</pre>
</section>"""
        )
    reveal = f"""<!DOCTYPE html><html><head><meta charset=utf-8><title>1.9.9 Reveal</title>
<style>body{{font-family:system-ui,sans-serif;max-width:820px;margin:20px auto;padding:0 12px}}
.card{{border:1px solid #ccc;border-radius:8px;padding:12px;margin:12px 0}}audio{{width:100%}}</style></head>
<body><h1>Stage B — Reveal scores / evidence</h1>
<p>Use after Stage A export. Optional diagnostic notes only.</p>
{''.join(cards2)}</body></html>"""
    (HR / "review_reveal.html").write_text(reveal, encoding="utf-8")


if __name__ == "__main__":
    main()
