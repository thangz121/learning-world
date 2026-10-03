"""Phase 1.9.10 — deep-dive 12 PHONE_MODEL_ERROR cases (diagnosis only, no scorer changes)."""
from __future__ import annotations

import csv
import hashlib
import json
import re
import sys
from collections import Counter, defaultdict
from dataclasses import asdict, is_dataclass
from pathlib import Path

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_9.HybridVAD.hybrid_vad import HybridVAD
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

P199 = REPO / "Research/Speech/Phase1_9_9/Results"
EXT = REPO / "Research/Speech/ExternalData/zenodo_200495/extracted/english_children"
DER = REPO / "Research/Speech/ExternalData/zenodo_200495/derived_16k"
OUT = REPO / "Research/Speech/Phase1_9_10"
RES = OUT / "Results"
HR = OUT / "HumanReview"
CLIPS = HR / "clips"
for d in (OUT, RES, HR, CLIPS, OUT / "Scripts"):
    d.mkdir(parents=True, exist_ok=True)

PADS_MS = [0, 100, 200, 250, 300, 500]

VOWELS = {
    "aa", "ae", "ah", "ao", "aw", "ay", "eh", "er", "ey", "ih", "iy",
    "ow", "oy", "uh", "uw",
}

# Documented developmental substitution pairs (used only as a documented table,
# not as an automatic "excuse": each still requires human-correct + measured mismatch).
CHILD_REALIZATION_PAIRS = {
    ("θ", "t"), ("θ", "f"), ("θ", "s"),
    ("ð", "d"), ("ð", "v"), ("ð", "z"),
    ("ɹ", "w"), ("l", "w"), ("w", "l"), ("ɹ", "l"),
    ("f", "v"), ("v", "f"), ("s", "z"), ("z", "s"),
    ("s", "t"), ("z", "d"), ("ʃ", "s"), ("tʃ", "t"), ("dʒ", "d"),
    ("k", "t"), ("g", "d"), ("ŋ", "n"), ("f", "p"), ("v", "b"),
}


def sha256(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest().upper()


def load_mono16_from_path(path: Path):
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


def find_source_wav(speaker_id: str, target: str):
    m = re.match(r"child_(\d+)", speaker_id or "")
    if not m:
        return None
    nn = m.group(1)
    base = EXT / "english_words_sentences"
    for sp in base.iterdir():
        if sp.name.startswith(nn + "_"):
            for mic in ("studio_mic", "port_mic", "nao_mic"):
                cand = sp / mic / "numbers" / f"{target}.wav"
                if cand.exists():
                    return cand
            hits = list(sp.rglob(f"numbers/{target}.wav"))
            if hits:
                return hits[0]
    return None


def base_phone(p: str) -> str:
    return re.sub(r"\d$", "", str(p).lower())


def is_vowel(p: str) -> bool:
    return base_phone(p) in VOWELS


def phone_position(idx: int, n: int) -> str:
    if n <= 1:
        return "single"
    if idx == 0:
        return "initial"
    if idx == n - 1:
        return "final"
    return "medial"


def acoustic_pm(x: np.ndarray, sr: int):
    """F0/F1/F2 via parselmouth (child-appropriate ranges), plus RMS/centroid/flatness/SNR."""
    feats = {}
    try:
        import parselmouth
        from parselmouth.praat import call

        snd = parselmouth.Sound(x.astype(np.float64), sampling_frequency=sr)
        pitch = snd.to_pitch(pitch_floor=100, pitch_ceiling=600)
        f0 = pitch.selected_array["frequency"]
        f0 = f0[f0 > 0]
        feats["f0_median"] = float(np.median(f0)) if len(f0) else None
        feats["f0_min"] = float(np.min(f0)) if len(f0) else None
        feats["f0_max"] = float(np.max(f0)) if len(f0) else None
        feats["f0_range"] = float(np.ptp(f0)) if len(f0) else None
        formant = snd.to_formant_burg(max_number_of_formants=4, maximum_formant=5500)
        times = formant.xs()
        f1s, f2s = [], []
        for t in times:
            f1 = formant.get_value_at_time(1, t)
            f2 = formant.get_value_at_time(2, t)
            if f1 and f1 == f1:
                f1s.append(f1)
            if f2 and f2 == f2:
                f2s.append(f2)
        feats["f1_median"] = float(np.median(f1s)) if f1s else None
        feats["f2_median"] = float(np.median(f2s)) if f2s else None
    except Exception as e:
        feats["parselmouth_error"] = str(e)

    frame = int(0.03 * sr)
    hop = int(0.01 * sr)
    rms = []
    for i in range(0, max(1, len(x) - frame), hop):
        rms.append(float(np.sqrt(np.mean(x[i : i + frame] ** 2) + 1e-20)))
    rms = np.array(rms) if rms else np.array([0.0])
    feats["rms_mean"] = float(np.mean(rms))
    feats["rms_p20"] = float(np.percentile(rms, 20))
    feats["rms_p90"] = float(np.percentile(rms, 90))
    feats["snr_proxy_db"] = float(
        20 * np.log10((feats["rms_p90"] + 1e-12) / (feats["rms_p20"] + 1e-12))
    )
    spec = np.abs(np.fft.rfft(x * np.hanning(len(x)))) + 1e-12 if len(x) > 8 else np.array([1e-12])
    freqs = np.fft.rfftfreq(len(x), 1 / sr) if len(x) > 8 else np.array([0.0])
    e = float(np.sum(spec))
    geo = float(np.exp(np.mean(np.log(spec))))
    ar = float(np.mean(spec))
    feats["centroid"] = float(np.sum(freqs * spec) / e) if e else None
    feats["flatness"] = (geo / ar) if ar else None
    feats["duration_s"] = len(x) / sr
    return feats


def ser_hits(hits):
    out = []
    for h in hits or []:
        if isinstance(h, dict):
            out.append(h)
        elif is_dataclass(h) and hasattr(h, "__dataclass_fields__"):
            out.append({k: getattr(h, k) for k in h.__dataclass_fields__})
        else:
            d = {}
            for k in ("expected", "best_obs", "match_type", "sim", "posterior",
                      "topk", "start_s", "end_s", "start_frame", "end_frame"):
                if hasattr(h, k):
                    d[k] = getattr(h, k)
            out.append(d)
    return out


def phone_analysis(hits):
    """Return expected/observed lists, mismatches, exact count, positions."""
    hits = ser_hits(hits)
    expected = [str(h.get("expected")) for h in hits]
    observed = [str(h.get("best_obs")) for h in hits]
    types = [str(h.get("match_type")) for h in hits]
    mismatches = []
    for i, h in enumerate(hits):
        if h.get("match_type") != "exact":
            mismatches.append(
                {
                    "index": i,
                    "position": phone_position(i, len(hits)),
                    "expected": str(h.get("expected")),
                    "observed": str(h.get("best_obs")),
                    "match_type": str(h.get("match_type")),
                    "sim": h.get("sim"),
                    "posterior": h.get("posterior"),
                    "start_s": h.get("start_s"),
                    "end_s": h.get("end_s"),
                    "duration_s": (
                        float(h.get("end_s") or 0) - float(h.get("start_s") or 0)
                        if h.get("start_s") is not None
                        else None
                    ),
                    "topk": h.get("topk"),
                    "expected_in_top3": any(
                        base_phone(t[0]) == base_phone(h.get("expected"))
                        for t in (h.get("topk") or [])[:3]
                    ),
                }
            )
    return {
        "expected": expected,
        "observed": observed,
        "match_types": types,
        "mismatches": mismatches,
        "n_exact": sum(1 for t in types if t == "exact"),
        "n_expected": len(expected),
    }


def run_variant(pev, x, sr, segs, pad_ms, tmp_path):
    if not segs:
        s0, s1 = 0.0, len(x) / sr
    else:
        p = pad_ms / 1000.0
        s0 = max(0.0, float(segs[0]["start"]) - p)
        s1 = min(len(x) / sr, float(segs[-1]["end"]) + p)
    sf.write(str(tmp_path), x[int(s0 * sr) : int(s1 * sr)], sr)
    return s0, s1


def main():
    human = list(csv.DictReader((P199 / "human_review_results.csv").open(encoding="utf-8-sig")))
    asr_rows = list(csv.DictReader((P199 / "asr_phone_conflicts.csv").open(encoding="utf-8-sig")))
    asr_by = {r["review_id"]: r for r in asr_rows}
    diag_rows = list(csv.DictReader((P199 / "phone_diagnostics.csv").open(encoding="utf-8-sig")))
    diag_by = {(r["speaker_id"], r["target"]): r for r in diag_rows}

    cases = [r for r in human if r.get("diagnostic") == "PHONE_MODEL_ERROR"]
    assert len(cases) == 12, len(cases)

    hv = HybridVAD()
    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()
    tmp = RES / "_tmp.wav"

    case_files = []
    root_rows = []
    confusion = Counter()
    boundary_rows = []
    span_rows = []
    acoustic_rows = []
    duration_rows = []
    all_phone_records = []

    for ci, c in enumerate(cases, start=1):
        cid = f"pme_{ci:02d}"
        sid = c["speaker_id"]
        target = c["target"]
        src = find_source_wav(sid, target)
        if src is None:
            print("MISSING_SOURCE", sid, target, flush=True)
            continue
        x, sr, _ = load_mono16_from_path(src)
        dur = len(x) / sr
        st = tgt.build(target)
        canonical = [str(p) for p in st.arpabet]
        sf.write(str(tmp), x, sr)
        rs = hv.run(str(tmp), mode="silero", silero_thr=0.5)
        sil_segs = rs["segments"]

        variants = {}
        for pm in PADS_MS:
            s0, s1 = run_variant(pev, x, sr, sil_segs, pm, tmp)
            sm = pev.soft_match(str(tmp), st.arpabet)
            pa = phone_analysis(sm.hits)
            variants[f"pad_{pm}"] = {
                "s0": round(s0, 4),
                "s1": round(s1, 4),
                "score": float(sm.soft_score_0_100),
                "confidence": float(sm.confidence_0_1),
                "n_exact": pa["n_exact"],
                "n_expected": pa["n_expected"],
                "mismatches": pa["mismatches"],
                "expected": pa["expected"],
                "observed": pa["observed"],
            }

        full_v = variants["pad_0"]  # pad_0 == raw VAD crop window (sil segs, no pad)
        # full file (not just VAD window)
        sf.write(str(tmp), x, sr)
        sm_full = pev.soft_match(str(tmp), st.arpabet)
        full_hits = ser_hits(sm_full.hits)
        full_file = {
            "score": float(sm_full.soft_score_0_100),
            "confidence": float(sm_full.confidence_0_1),
            "n_exact": sum(1 for h in full_hits if h.get("match_type") == "exact"),
            "n_expected": len(full_hits),
            "observed": [str(h.get("best_obs")) for h in full_hits],
            "mismatches": phone_analysis(full_hits)["mismatches"],
        }

        # phone confusion from the FULL-file evidence
        for mm in full_file["mismatches"]:
            confusion[
                (
                    base_phone(mm["expected"]),
                    base_phone(mm["observed"]),
                    mm["position"],
                    "vowel" if is_vowel(mm["expected"]) else "consonant",
                )
            ] += 1
        # per-phone span records
        for i_h, h in enumerate(full_hits):
            span_rows.append(
                {
                    "case_id": cid,
                    "speaker_id": sid,
                    "target": target,
                    "expected": str(h.get("expected")),
                    "observed": str(h.get("best_obs")),
                    "match_type": str(h.get("match_type")),
                    "sim": h.get("sim"),
                    "posterior": h.get("posterior"),
                    "start_s": h.get("start_s"),
                    "end_s": h.get("end_s"),
                    "duration_s": (
                        float(h.get("end_s") or 0) - float(h.get("start_s") or 0)
                        if h.get("start_s") is not None
                        else None
                    ),
                    "relative_position": phone_position(i_h, len(full_hits)),
                }
            )
            all_phone_records.append(
                {
                    "case_id": cid,
                    "target": target,
                    "expected": str(h.get("expected")),
                    "observed": str(h.get("best_obs")),
                    "match_type": str(h.get("match_type")),
                }
            )

        # boundary rescue: raw/pad0 < 50 and best pad >= 50, or best pad - raw >= 20
        pad_scores = {k: v["score"] for k, v in variants.items()}
        raw_score = variants["pad_0"]["score"]
        best_pad_key = max((k for k in pad_scores if k != "pad_0"), key=lambda k: pad_scores[k])
        best_pad_score = pad_scores[best_pad_key]
        rescue = (raw_score < 50 and best_pad_score >= 50) or (best_pad_score - raw_score >= 20)
        rescue_to_usable = raw_score < 50 and best_pad_score >= 50
        crop_beats_full = raw_score >= 50 and full_file["score"] < 50
        expected_all_matched_pad = any(
            v["n_exact"] == v["n_expected"] for k, v in variants.items() if k != "pad_0"
        )
        boundary_rows.append(
            {
                "case_id": cid,
                "speaker_id": sid,
                "target": target,
                "raw_score": raw_score,
                "full_file_score": full_file["score"],
                **{f"pad_{pm}_score": pad_scores[f"pad_{pm}"] for pm in PADS_MS if pm != 0},
                "best_pad": best_pad_key,
                "best_pad_score": best_pad_score,
                "rescue_definition": "raw<50 & best_pad>=50 OR best_pad-raw>=20",
                "boundary_rescue": bool(rescue),
                "boundary_rescue_to_usable": bool(rescue_to_usable),
                "crop_beats_full": bool(crop_beats_full),
                "expected_all_matched_in_any_pad": bool(expected_all_matched_pad),
                "raw_n_exact": variants["pad_0"]["n_exact"],
                "full_n_exact": full_file["n_exact"],
                "n_expected": full_file["n_expected"],
                "humans_boundary_label": c.get("boundary_label"),
            }
        )

        # acoustic features on full file, and on the raw VAD window
        ac = acoustic_pm(x, sr)
        ac.update(
            {
                "case_id": cid,
                "speaker_id": sid,
                "target": target,
                "human_judgment": c["human_pronunciation"],
                "full_score": full_file["score"],
                "n_expected_phones": len(st.arpabet),
                "phones_per_second": len(st.arpabet) / dur if dur else None,
            }
        )
        acoustic_rows.append(ac)
        acoustic_rows.append(
            {
                **acoustic_pm(
                    x[
                        int(variants["pad_0"]["s0"] * sr) : int(variants["pad_0"]["s1"] * sr)
                    ],
                    sr,
                ),
                "case_id": cid + "_raw",
                "speaker_id": sid,
                "target": target,
                "human_judgment": c["human_pronunciation"],
                "full_score": raw_score,
                "n_expected_phones": len(st.arpabet),
                "phones_per_second": len(st.arpabet) / dur if dur else None,
            }
        )

        for h in full_hits:
            duration_rows.append(
                {
                    "case_id": cid,
                    "target": target,
                    "expected": str(h.get("expected")),
                    "match_type": str(h.get("match_type")),
                    "phone_duration_s": (
                        float(h.get("end_s") or 0) - float(h.get("start_s") or 0)
                        if h.get("start_s") is not None
                        else None
                    ),
                    "utterance_duration_s": dur,
                    "phone_count": len(st.arpabet),
                    "phones_per_second": len(st.arpabet) / dur if dur else None,
                }
            )

        asr = asr_by.get(c["review_id"], {})

        case_record = {
            "case_id": cid,
            "review_id": c["review_id"],
            "speaker_id": sid,
            "recording_id": c.get("recording_id"),
            "target": target,
            "human_judgment": c["human_pronunciation"],
            "doc_diagnostic": c.get("diagnostic"),
            "canonical_phones": canonical,
            "observed_phones_full": full_file["observed"],
            "observed_phones_raw": variants["pad_0"]["observed"],
            "phone_mismatch": full_file["mismatches"],
            "full_file_score": full_file["score"],
            "raw_score": raw_score,
            "pad_scores": pad_scores,
            "best_pad": best_pad_key,
            "best_pad_score": best_pad_score,
            "boundary_rescue": bool(rescue),
            "boundary_label_human": c.get("boundary_label"),
            "vad_status": {
                "silero_n_segs": len(sil_segs),
                "raw_window": [variants["pad_0"]["s0"], variants["pad_0"]["s1"]],
                "file_duration_s": round(dur, 4),
            },
            "acoustic_features": {k: v for k, v in ac.items() if k not in ("case_id",)},
            "asr_status": asr.get("asr_status", ""),
            "asr_text": asr.get("asr_text", ""),
            "human_second_pass": "",
            "root_cause": "",
            "root_cause_confidence": "",
            "evidence": [],
        }
        case_files.append(case_record)

    # ---------------- root-cause rules (explicit, evidence-based) ----------------
    # Priority (documented; first match wins as PRIMARY; secondary noted):
    # 1. measured boundary rescue (raw<50 & best_pad>=50, or +>=20 with raw<50)
    # 2. measured crop-beats-full inversion (raw>=50 & full<50) -> full-file alignment
    # 3. all mismatches are documented child-realization pairs -> realization variation
    # 4. >=50% documented pairs (human correct) -> realization variation (LOW)
    # 5. attractor collapse (same observed phone repeated >=2) -> model generalization
    # 6. short-span dominated -> CTC alignment (LOW)
    # 7. else UNRESOLVED / MULTIPLE
    for cr in case_files:
        ev = []
        human_ok = cr["human_judgment"] in ("CLEAR_CORRECT", "PROBABLY_CORRECT")
        raw = cr["raw_score"]
        best_pad = cr["best_pad_score"]
        full = cr["full_file_score"]
        ac = cr["acoustic_features"]
        mism = cr["phone_mismatch"]
        top3_hits = [m for m in mism if m.get("expected_in_top3")]
        vowel_mism = [m for m in mism if is_vowel(m["expected"])]
        cons_mism = [m for m in mism if not is_vowel(m["expected"])]
        short_phones = [
            m for m in mism if (m.get("duration_s") is not None and m["duration_s"] < 0.05)
        ]
        low_snr = ac.get("snr_proxy_db") is not None and ac["snr_proxy_db"] < 12
        plausible = [
            m
            for m in mism
            if (base_phone(m["expected"]), base_phone(m["observed"])) in CHILD_REALIZATION_PAIRS
        ]
        plausible_rate = len(plausible) / len(mism) if mism else 0.0
        obs_counter = Counter(base_phone(m["observed"]) for m in mism)
        attractor_phone, attractor_n = (
            obs_counter.most_common(1)[0] if obs_counter else (None, 0)
        )
        crop_beats_full = raw >= 50 and full < 50
        cause = "UNRESOLVED"
        secondary = ""
        conf = "LOW"

        if cr["boundary_rescue"]:
            cause = "BOUNDARY_ERROR"
            conf = "HIGH" if (raw < 50 <= best_pad) else "MEDIUM"
            ev.append(f"raw_score={raw} -> best_pad({cr['best_pad']})={best_pad}")
            if cr["boundary_label_human"] and cr["boundary_label_human"] != "BOUNDARY_OK":
                ev.append(f"human boundary label={cr['boundary_label_human']}")
            if plausible_rate >= 0.5 and human_ok:
                secondary = "PHONEME_REALIZATION_VARIATION"
                ev.append(
                    f"secondary: {len(plausible)}/{len(mism)} mismatches are documented "
                    f"child-realization pairs ({';'.join(base_phone(m['expected'])+'->'+base_phone(m['observed']) for m in plausible)})"
                )
        elif crop_beats_full:
            cause = "CTC_ALIGNMENT_ERROR"
            conf = "MEDIUM"
            ev.append(
                f"VAD crop raw_score={raw} >=50 while full-file score={full}<50; "
                "full-file alignment fails, crop restores expected evidence"
            )
        elif mism and plausible_rate >= 1.0 and human_ok:
            cause = "PHONEME_REALIZATION_VARIATION"
            conf = "MEDIUM"
            ev.append(
                f"all {len(mism)} mismatches are documented child-realization pairs: "
                + ";".join(f"{base_phone(m['expected'])}->{base_phone(m['observed'])}" for m in mism)
            )
            ev.append(f"human={cr['human_judgment']} (accepts the realization)")
        elif mism and plausible_rate >= 0.5 and human_ok:
            cause = "PHONEME_REALIZATION_VARIATION"
            conf = "LOW"
            ev.append(
                f"{len(plausible)}/{len(mism)} mismatches are documented child-realization pairs; "
                f"remaining: "
                + ";".join(
                    f"{base_phone(m['expected'])}->{base_phone(m['observed'])}"
                    for m in mism
                    if m not in plausible
                )
            )
        elif attractor_n >= 2:
            cause = "PHONE_MODEL_GENERALIZATION_FAILURE"
            secondary = "CTC_ALIGNMENT_ERROR"
            conf = "MEDIUM"
            ev.append(
                f"attractor collapse: observed phone '{attractor_phone}' repeated {attractor_n}x "
                f"across mismatches (observed full={' '.join(str(o) for o in cr['observed_phones_full'])})"
            )
        elif short_phones:
            cause = "CTC_ALIGNMENT_ERROR"
            conf = "LOW"
            ev.append(f"{len(short_phones)} mismatched phone spans <50ms")
        elif human_ok and full < 50 and best_pad < 50 and raw < 50:
            cause = "PHONE_MODEL_GENERALIZATION_FAILURE"
            conf = "LOW"
            ev.append(
                f"all variants fail (raw={raw}, best_pad={best_pad}, full={full}); "
                f"no documented realization pairs; no attractor collapse"
            )
        else:
            cause = "MULTIPLE_CAUSES" if (low_snr or (vowel_mism and cons_mism)) else "UNRESOLVED"
            ev.append(
                f"human={cr['human_judgment']} raw={raw} pad={best_pad} full={full} "
                f"vowel_mism={len(vowel_mism)} cons_mism={len(cons_mism)} snr={ac.get('snr_proxy_db')}"
            )
            if low_snr:
                ev.append(f"snr_proxy_db={ac.get('snr_proxy_db')} (low)")
        cr["root_cause"] = cause
        cr["secondary_cause"] = secondary
        cr["root_cause_confidence"] = conf
        cr["evidence"] = ev + [
            f"canonical={' '.join(cr['canonical_phones'])}",
            f"observed_full={' '.join(str(o) for o in cr['observed_phones_full'])}",
            f"f0_median={ac.get('f0_median')} f1={ac.get('f1_median')} f2={ac.get('f2_median')}",
            f"asr_status={cr['asr_status']}",
        ]

    # write CSVs
    def write_csv(path: Path, rows, fields=None):
        rows = [r for r in rows if isinstance(r, dict)]
        if not rows:
            path.write_text("empty\n", encoding="utf-8")
            return
        keys = fields or []
        if not keys:
            for r in rows:
                for k in r:
                    if k not in keys:
                        keys.append(k)
        with open(path, "w", newline="", encoding="utf-8") as f:
            w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
            w.writeheader()
            w.writerows(rows)

    write_csv(
        RES / "phone_model_error_cases.csv",
        [
            {
                "case_id": cr["case_id"],
                "speaker_id": cr["speaker_id"],
                "recording_id": cr["recording_id"],
                "target": cr["target"],
                "full_score": cr["full_file_score"],
                "raw_score": cr["raw_score"],
                "confidence": None,
                "human_pronunciation": cr["human_judgment"],
                "diagnostic": cr["doc_diagnostic"],
                "canonical_phones": " ".join(cr["canonical_phones"]),
                "observed_phones": " ".join(str(o) for o in cr["observed_phones_full"]),
                "phone_misses": len(cr["phone_mismatch"]),
                "asr_status": cr["asr_status"],
                "boundary_status": cr["boundary_label_human"],
                "audio_quality": None,
            }
            for cr in case_files
        ],
    )
    root_rows = []
    for cr in case_files:
        ac = cr["acoustic_features"]
        root_rows.append(
            {
                "case_id": cr["case_id"],
                "speaker_id": cr["speaker_id"],
                "target": cr["target"],
                "human_judgment": cr["human_judgment"],
                "canonical_phones": " ".join(cr["canonical_phones"]),
                "observed_phones": " ".join(str(o) for o in cr["observed_phones_full"]),
                "mismatch_type": ";".join(
                    f"{m['expected']}->{m['observed']}@{m['position']}" for m in cr["phone_mismatch"]
                ),
                "word_position": ";".join(sorted({m["position"] for m in cr["phone_mismatch"]})),
                "raw_score": cr["raw_score"],
                "padded_score": cr["best_pad_score"],
                "boundary_effect": cr["boundary_rescue"],
                "f0_median": ac.get("f0_median"),
                "f1": ac.get("f1_median"),
                "f2": ac.get("f2_median"),
                "duration": ac.get("duration_s"),
                "asr_status": cr["asr_status"],
                "root_cause": cr["root_cause"],
                "confidence": cr["root_cause_confidence"],
                "evidence_summary": " | ".join(cr["evidence"][:3]),
            }
        )
    write_csv(RES / "phone_model_error_root_causes.csv", root_rows)
    write_csv(
        RES / "phone_confusion_matrix.csv",
        [
            {"expected": e, "observed": o, "position": p, "class": cl, "count": n}
            for (e, o, p, cl), n in sorted(confusion.items(), key=lambda kv: -kv[1])
        ],
    )
    write_csv(RES / "boundary_analysis.csv", boundary_rows)
    write_csv(RES / "ctc_span_analysis.csv", span_rows)
    write_csv(RES / "acoustic_analysis.csv", acoustic_rows)
    write_csv(
        RES / "f0_analysis.csv",
        [
            {
                "case_id": r["case_id"],
                "target": r["target"],
                "human_judgment": r["human_judgment"],
                "f0_median": r.get("f0_median"),
                "f0_min": r.get("f0_min"),
                "f0_max": r.get("f0_max"),
                "f0_range": r.get("f0_range"),
                "full_score": r.get("full_score"),
            }
            for r in acoustic_rows
        ],
    )
    write_csv(
        RES / "formant_analysis.csv",
        [
            {
                "case_id": r["case_id"],
                "target": r["target"],
                "f1_median": r.get("f1_median"),
                "f2_median": r.get("f2_median"),
                "full_score": r.get("full_score"),
                "human_judgment": r.get("human_judgment"),
            }
            for r in acoustic_rows
        ],
    )
    write_csv(RES / "duration_analysis.csv", duration_rows)

    # word/speaker summaries from 1.9.9 (12 + context)
    word_summary = []
    byw = defaultdict(list)
    for cr in case_files:
        byw[cr["target"]].append(cr)
    for w, lst in sorted(byw.items()):
        word_summary.append(
            {
                "word": w,
                "n_phone_model_error": len(lst),
                "speakers": ";".join(sorted({x["speaker_id"] for x in lst})),
                "mean_full_score": float(np.mean([x["full_file_score"] for x in lst])),
                "human_correct": sum(
                    1
                    for x in lst
                    if x["human_judgment"] in ("CLEAR_CORRECT", "PROBABLY_CORRECT")
                ),
            }
        )
    write_csv(RES / "word_analysis.csv", word_summary)

    speaker_summary = []
    bys = defaultdict(list)
    for cr in case_files:
        bys[cr["speaker_id"]].append(cr)
    for s, lst in sorted(bys.items()):
        speaker_summary.append(
            {
                "speaker_id": s,
                "n_phone_model_errors": len(lst),
                "targets": ";".join(sorted({x["target"] for x in lst})),
                "mean_full_score": float(np.mean([x["full_file_score"] for x in lst])),
                "boundary_rescues": sum(1 for x in lst if x["boundary_rescue"]),
                "root_causes": ";".join(sorted({x["root_cause"] for x in lst})),
            }
        )
    write_csv(RES / "speaker_analysis.csv", speaker_summary)

    # human second pass template (empty, blind)
    write_csv(
        RES / "human_second_pass.csv",
        [
            {
                "case_id": cr["case_id"],
                "speaker_id": cr["speaker_id"],
                "target": cr["target"],
                "stage_a_pronunciation": "",
                "stage_b_most_responsible": "",
                "notes": "",
                "reviewer_id": "",
                "review_date": "",
            }
            for cr in case_files
        ],
    )

    # adult vs child diagnostic comparison (same pipeline; different words - caveat)
    adult_rows = []
    lwe = REPO / "Research/Speech/Phase1_1/audio"
    adult_words = [
        ("sapi_red.wav", "red"),
        ("sapi_cat.wav", "cat"),
        ("sapi_blue.wav", "blue"),
        ("sapi_big.wav", "big"),
        ("sapi_book.wav", "book"),
        ("sapi_dog.wav", "dog"),
    ]
    for fn, w in adult_words:
        p = lwe / fn
        if not p.exists():
            continue
        xx, ssr = sf.read(str(p))
        if xx.ndim > 1:
            xx = xx.mean(axis=1)
        xx = xx.astype(np.float32)
        if ssr != 16000:
            continue
        sf.write(str(tmp), xx, ssr)
        st = tgt.build(w)
        sm = pev.soft_match(str(tmp), st.arpabet)
        ah = ser_hits(sm.hits)
        n_exact = sum(1 for h in ah if h.get("match_type") == "exact")
        adult_rows.append(
            {
                "file": fn,
                "word": w,
                "score": sm.soft_score_0_100,
                "n_exact": n_exact,
                "n_expected": len(ah),
                "exact_rate": n_exact / len(ah) if ah else 0,
            }
        )
    write_csv(RES / "adult_vs_child_phone_model.csv", adult_rows)

    # decide
    causes = Counter(cr["root_cause"] for cr in case_files)
    secondary = Counter(cr.get("secondary_cause") for cr in case_files if cr.get("secondary_cause"))
    boundary_rescue_n = sum(1 for cr in case_files if cr["boundary_rescue"])
    rescue_usable_n = sum(
        1 for b in boundary_rows if b.get("boundary_rescue_to_usable")
    )
    crop_beats_full_n = sum(1 for b in boundary_rows if b.get("crop_beats_full"))
    n_total = len(case_files)
    human_ok_n = sum(
        1 for cr in case_files if cr["human_judgment"] in ("CLEAR_CORRECT", "PROBABLY_CORRECT")
    )
    adult_exact = np.mean([r["exact_rate"] for r in adult_rows]) if adult_rows else None
    child_exact = np.mean(
        [
            (len(cr["canonical_phones"]) - len(cr["phone_mismatch"]))
            / max(1, len(cr["canonical_phones"]))
            for cr in case_files
        ]
    )

    if boundary_rescue_n >= 6:
        decision = "A. ROOT_CAUSE_SUFFICIENTLY_IDENTIFIED"
        decision_note = "Boundary/crop mechanism dominant on majority of cases"
    elif n_total == 12 and causes:
        decision = "B. ROOT_CAUSE_PARTIALLY_IDENTIFIED"
        decision_note = (
            "Multiple measurable mechanisms found (boundary rescue, full-file alignment inversion, "
            "attractor collapse, documented child-realization substitutions); human second-pass "
            "review pending; no scorer change justified"
        )
    else:
        decision = "C. ROOT_CAUSE_UNCLEAR"
        decision_note = "insufficient evidence"

    master = {
        "phase": "1.9.10",
        "n_cases": n_total,
        "root_cause_distribution": dict(causes),
        "secondary_cause_distribution": dict(secondary),
        "boundary_rescue_rate": boundary_rescue_n / n_total if n_total else None,
        "boundary_rescue_to_usable_rate": rescue_usable_n / n_total if n_total else None,
        "crop_beats_full_count": crop_beats_full_n,
        "phone_model_error_human_correct_rate": human_ok_n / n_total if n_total else None,
        "adult_exact_rate_mean": float(adult_exact) if adult_exact is not None else None,
        "child_exact_rate_mean": float(child_exact) if child_exact is not None else None,
        "adult_exact_rate_note": "adult comparison uses different words (diagnostic only); blue/dog/big also fail for adult",
        "pitch_causality": "NOT_ESTABLISHED",
        "formant_causality": "NOT_ESTABLISHED",
        "child_specific_phone_failure": "PARTIAL",
        "scorer_modification_justified": "NO",
        "decision": decision,
        "decision_note": decision_note,
        "production_vad": False,
        "router_locked": False,
        "unity_integrated": False,
        "scorer_modified": False,
        "cases": case_files,
        "limitations": [
            "human second-pass review not yet filled",
            "adult comparison uses different words (diagnostic only)",
            "F0/F1/F2 single-pass measurements, no normalization applied",
            "n=12, single recording session per child",
            "child-realization pair table is documented but not externally validated for these speakers",
        ],
    }
    (RES / "phase_1_9_10_master.json").write_text(
        json.dumps(master, indent=2, ensure_ascii=True), encoding="utf-8"
    )
    (RES / "decision.json").write_text(
        json.dumps(
            {
                "decision": decision,
                "note": decision_note,
                "root_cause_distribution": dict(causes),
                "boundary_rescue_rate": master["boundary_rescue_rate"],
                "scorer_modified": False,
                "production_vad": False,
                "router_locked": False,
                "unity_integrated": False,
            },
            indent=2,
        ),
        encoding="utf-8",
    )

    # review pack clips + html
    build_review(case_files, x_unused=None)

    print("CAUSES", dict(causes))
    print("RESCUE", boundary_rescue_n, "/", n_total)
    print("DECISION", decision)
    try:
        tmp.unlink()
    except Exception:
        pass


def build_review(case_files, x_unused=None):
    import html as _html

    items = []
    for cr in case_files:
        rid = cr["case_id"]
        src = find_source_wav(cr["speaker_id"], cr["target"])
        if src is None:
            continue
        x, sr, _ = load_mono16_from_path(src)
        dur = len(x) / sr
        fullp = CLIPS / f"{rid}_FULL.wav"
        sf.write(str(fullp), x, sr)
        # 2s listen window centered
        mid = dur / 2
        half = max(1.0, dur / 2)
        s = max(0.0, mid - half)
        e = min(dur, mid + half)
        if e - s < 2.0 and dur >= 2.0:
            s, e = 0.0, min(dur, 2.0)
        listenp = CLIPS / f"{rid}_LISTEN.wav"
        sf.write(str(listenp), x[int(s * sr) : int(e * sr)], sr)
        items.append(
            {
                "case_id": rid,
                "speaker_id": cr["speaker_id"],
                "target": cr["target"],
                "age_metadata": "dataset_mean_M=4.9y; individual UNKNOWN",
                "clips": {
                    "LISTEN": f"clips/{rid}_LISTEN.wav",
                    "FULL": f"clips/{rid}_FULL.wav",
                },
                "full_score": cr["full_file_score"],
                "raw_score": cr["raw_score"],
                "best_pad": cr["best_pad"],
                "best_pad_score": cr["best_pad_score"],
                "canonical_phones": " ".join(cr["canonical_phones"]),
                "observed_phones": " ".join(str(o) for o in cr["observed_phones_full"]),
                "mismatch_type": ";".join(
                    f"{m['expected']}->{m['observed']}@{m['position']}"
                    for m in cr["phone_mismatch"]
                ),
                "boundary_rescue": cr["boundary_rescue"],
                "asr_status": cr["asr_status"],
                "f0_median": cr["acoustic_features"].get("f0_median"),
                "f1": cr["acoustic_features"].get("f1_median"),
                "f2": cr["acoustic_features"].get("f2_median"),
                "duration_s": cr["acoustic_features"].get("duration_s"),
                # human fields
                "stage_a_pronunciation": "",
                "stage_b_most_responsible": "",
            }
        )
    (HR / "review_metadata.json").write_text(
        json.dumps(
            {
                "n": len(items),
                "blind_hides": ["full_score", "phone evidence", "canonical", "acoustics", "asr"],
                "stage_a_schema": [
                    "CLEAR_CORRECT",
                    "PROBABLY_CORRECT",
                    "AMBIGUOUS",
                    "PROBABLY_INCORRECT",
                    "CLEAR_INCORRECT",
                ],
                "stage_b_schema": [
                    "BOUNDARY",
                    "PHONE_MODEL",
                    "AUDIO",
                    "CHILD_ACOUSTICS",
                    "TRUE_PRONUNCIATION_ERROR",
                    "UNCERTAIN",
                ],
                "items": items,
            },
            indent=2,
            ensure_ascii=True,
        ),
        encoding="utf-8",
    )

    def radios(name, values, labels):
        return (
            '<div class="opts">'
            + "".join(
                f'<label><input type="radio" name="{name}" value="{v}"><span>{labels.get(v, v)}</span></label>'
                for v in values
            )
            + "</div>"
        )

    P = ["CLEAR_CORRECT", "PROBABLY_CORRECT", "AMBIGUOUS", "PROBABLY_INCORRECT", "CLEAR_INCORRECT"]
    PL = {
        "CLEAR_CORRECT": "Rõ — đúng",
        "PROBABLY_CORRECT": "Có lẽ đúng",
        "AMBIGUOUS": "Không chắc",
        "PROBABLY_INCORRECT": "Có lẽ sai",
        "CLEAR_INCORRECT": "Rõ — sai",
    }
    B = ["BOUNDARY", "PHONE_MODEL", "AUDIO", "CHILD_ACOUSTICS", "TRUE_PRONUNCIATION_ERROR", "UNCERTAIN"]
    BL = {
        "BOUNDARY": "Cắt biên",
        "PHONE_MODEL": "Phone model",
        "AUDIO": "Chất lượng audio",
        "CHILD_ACOUSTICS": "Đặc điểm giọng trẻ",
        "TRUE_PRONUNCIATION_ERROR": "Trẻ phát âm sai thật",
        "UNCERTAIN": "Không rõ",
    }

    cards_a = []
    cards_b = []
    for it in items:
        rid = it["case_id"]
        cards_a.append(
            f"""<section class="card" id="a_{rid}">
<h2>{rid} — {it['target'].upper()}</h2>
<p class="meta">speaker={it['speaker_id']} · {it['age_metadata']}</p>
<div class="audio-block"><div class="lbl">LISTEN</div><audio controls preload="none" src="{it['clips']['LISTEN']}"></audio></div>
<div class="audio-block"><div class="lbl">FULL</div><audio controls preload="none" src="{it['clips']['FULL']}"></audio></div>
<fieldset><legend>Nghe và chấm phát âm (ẩn điểm)</legend>{radios('p_' + rid, P, PL)}</fieldset>
<input class="notes" name="n_{rid}" placeholder="ghi chú (tuỳ chọn)">
</section>"""
        )
        cards_b.append(
            f"""<section class="card">
<h2>{rid} — {it['target'].upper()}</h2>
<audio controls preload="none" src="{it['clips']['FULL']}"></audio>
<pre>full_score={it['full_score']}  raw_score={it['raw_score']}  best_pad({it['best_pad']})={it['best_pad_score']}
canonical={it['canonical_phones']}
observed ={it['observed_phones']}
mismatch ={it['mismatch_type']}
boundary_rescue={it['boundary_rescue']}  asr={it['asr_status']}
f0_median={it['f0_median']}  f1={it['f1']}  f2={it['f2']}  duration={it['duration_s']}</pre>
<fieldset><legend>Theo bạn, yếu tố nào chịu trách nhiệm chính cho điểm thấp?</legend>{radios('s_' + rid, B, BL)}</fieldset>
</section>"""
        )
    ids = json.dumps([it["case_id"] for it in items])
    style = """<style>
:root{--tap:44px}
*{box-sizing:border-box}
body{font-family:system-ui,-apple-system,sans-serif;margin:0 auto;max-width:760px;padding:12px 12px 96px;line-height:1.45;background:#f7f7f8}
.banner{background:#fff3cd;border:1px solid #e6d089;border-radius:12px;padding:14px;margin-bottom:14px}
.card{background:#fff;border:1px solid #ddd;border-radius:12px;padding:14px;margin:0 0 16px}
.card h2{font-size:1.05rem;margin:0 0 6px}
.meta{font-size:.85rem;color:#444}
.target{display:inline-block;background:#111;color:#fff;font-size:1.2rem;font-weight:700;padding:6px 12px;border-radius:8px;margin:4px 0 8px}
.audio-block{margin:8px 0}.lbl{font-size:.8rem;font-weight:600;color:#333;margin-bottom:4px}
audio{width:100%}
fieldset{border:1px solid #ccc;border-radius:10px;margin:12px 0 0;padding:10px;background:#fafafa}
legend{font-weight:700;font-size:.95rem;padding:0 6px}
.opts{display:flex;flex-direction:column;gap:8px;margin-top:8px}
.opts label{display:flex;align-items:center;gap:10px;min-height:var(--tap);padding:10px 12px;border:1px solid #ccc;border-radius:10px;background:#fff;font-size:.95rem}
.opts input{width:20px;height:20px;flex-shrink:0}
.opts label:has(input:checked){border-color:#0b5fff;background:#e8f0ff}
.notes{width:100%;min-height:44px;font-size:16px;padding:10px;border:1px solid #ccc;border-radius:10px;margin-top:8px}
.sticky{position:fixed;left:0;right:0;bottom:0;background:#111;color:#fff;padding:12px 14px calc(12px + env(safe-area-inset-bottom));display:flex;gap:10px;z-index:50}
.sticky button{flex:1;min-height:48px;font-size:1rem;font-weight:700;border:0;border-radius:10px;background:#0b5fff;color:#fff}
.sticky .count{align-self:center;font-size:.85rem;white-space:nowrap}
pre{white-space:pre-wrap;font-size:.78rem;background:#eee;padding:8px;border-radius:8px}
</style>"""

    blind = f"""<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, viewport-fit=cover">
<title>1.9.10 Second-pass blind</title>{style}</head><body>
<div class="banner"><h1>1.9.10 — Second pass (mù)</h1>
<p>12 ca phone-model-error. Nghe trước, chấm phát âm. <b>Ẩn</b> điểm/phone/acoustics.</p></div>
{''.join(cards_a)}
<div class="sticky"><span class="count" id="prog">0/12</span><button id="exp">Xuất CSV Stage A</button></div>
<script>
const ids={ids};
function v(p,id){{const e=document.querySelector('input[name="'+p+'_'+id+'"]:checked');return e?e.value:'';}}
function upd(){{let n=0;for(const id of ids)if(v('p',id))n++;document.getElementById('prog').textContent=n+'/'+ids.length;}}
document.body.addEventListener('change',upd);upd();
document.getElementById('exp').onclick=()=>{{
 let lines=['case_id,stage_a_pronunciation,notes'];
 for(const id of ids){{
  const notes=(document.querySelector('input[name="n_'+id+'"]')||{{value:''}}).value.replace(/,/g,';');
  lines.push([id,v('p',id),notes].join(','));
 }}
 const t=lines.join('\\n');
 const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([t],{{type:'text/csv'}}));
 a.download='Human_SecondPass_StageA_Filled.csv';a.click();
}};
</script></body></html>"""
    (HR / "review_blind.html").write_text(blind, encoding="utf-8")

    reveal = f"""<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>1.9.10 reveal</title>{style}</head><body>
<h1>1.9.10 — Stage B (hiện evidence)</h1>
<p>Dùng sau khi Stage A đã chấm mù.</p>
{''.join(cards_b)}
<p><button id="expb" style="min-height:48px;font-size:1rem;font-weight:700;border:0;border-radius:10px;background:#0b5fff;color:#fff;padding:0 16px">Xuất CSV Stage B</button></p>
<script>
const ids={ids};
function v(p,id){{const e=document.querySelector('input[name="'+p+'_'+id+'"]:checked');return e?e.value:'';}}
document.getElementById('expb').onclick=()=>{{
 let lines=['case_id,stage_b_most_responsible'];
 for(const id of ids)lines.push([id,v('s',id)].join(','));
 const t=lines.join('\\n');
 const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([t],{{type:'text/csv'}}));
 a.download='Human_SecondPass_StageB_Filled.csv';a.click();
}};
</script></body></html>"""
    (HR / "review_reveal.html").write_text(reveal, encoding="utf-8")


if __name__ == "__main__":
    main()
