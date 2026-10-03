"""Phase 1.9.12 Objective A — final-consonant inventory, features, preliminary acoustic support."""
from __future__ import annotations

import csv
import hashlib
import json
import re
import sys
from collections import Counter, defaultdict
from dataclasses import is_dataclass
from pathlib import Path

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

OUT = REPO / "Research/Speech/Phase1_9_12"
RES = OUT / "Results"
P198 = REPO / "Research/Speech/Phase1_9_8/Results/pronunciation_results.csv"
P199 = REPO / "Research/Speech/Phase1_9_9/Results"
P1910 = REPO / "Research/Speech/Phase1_9_10/Results"
P1911 = REPO / "Research/Speech/Phase1_9_11/Results"
EXT = REPO / "Research/Speech/ExternalData/zenodo_200495/extracted/english_children"
DER = REPO / "Research/Speech/ExternalData/zenodo_200495/derived_16k"
for d in (OUT, RES, OUT / "Scripts", OUT / "HumanReview"):
    d.mkdir(parents=True, exist_ok=True)

VOWELS = {"aa", "ae", "ah", "ao", "aw", "ay", "eh", "er", "ey", "ih", "iy", "ow", "oy", "uh", "uw"}
PADS = [0, 50, 100, 150, 200]
LWE_AUDIO = REPO / "Research/Speech/Phase1_1/audio"


def base_phone(p: str) -> str:
    return re.sub(r"\d$", "", str(p).lower())


def phone_class(p: str) -> str:
    p = base_phone(p)
    if p in VOWELS:
        return "VOWEL"
    if p in {"p", "b", "t", "d", "k", "g"}:
        return "STOP"
    if p in {"f", "v", "s", "z", "th", "dh", "sh", "zh", "h"}:
        return "FRICATIVE"
    if p in {"ch", "jh"}:
        return "AFFRICATE"
    if p in {"m", "n", "ng"}:
        return "NASAL"
    if p in {"l", "r"}:
        return "LIQUID"
    return "OTHER"


def voiced(p: str) -> bool:
    return base_phone(p) in {"b", "d", "g", "v", "dh", "z", "zh", "jh", "m", "n", "ng", "l", "r"}


def sha256(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest().upper()


def load_mono16(path: Path):
    key = sha256(path)[:16] + "_" + re.sub(r"[^A-Za-z0-9_.-]+", "_", path.name)[:60]
    cache = DER / f"{key}.wav"
    if cache.exists():
        x, sr = sf.read(str(cache))
        if x.ndim > 1:
            x = x.mean(axis=1)
        return x.astype(np.float32), int(sr)
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
    return x, sr


def find_source(speaker_id: str, target: str):
    m = re.match(r"child_(\d+)", speaker_id or "")
    if not m:
        return None
    nn = m.group(1)
    base = EXT / "english_words_sentences"
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


def ser_hits(hits):
    out = []
    for h in hits or []:
        if isinstance(h, dict):
            out.append(h)
        elif is_dataclass(h) and hasattr(h, "__dataclass_fields__"):
            out.append({k: getattr(h, k) for k in h.__dataclass_fields__})
    return out


def rms_of(x):
    return float(np.sqrt(np.mean(x**2) + 1e-20)) if len(x) else 0.0


def voicing_ratio(x, sr):
    if len(x) < sr // 20:
        return 0.0
    frame = int(0.03 * sr)
    hop = int(0.01 * sr)
    voiced = 0
    total = 0
    min_lag = int(sr / 500)
    max_lag = int(sr / 80)
    for i in range(0, max(1, len(x) - frame), hop):
        w = x[i : i + frame]
        w = w - np.mean(w)
        if np.std(w) < 1e-6:
            continue
        total += 1
        corr = np.correlate(w, w, mode="full")
        corr = corr[len(corr) // 2 :]
        seg = corr[min_lag:max_lag]
        if len(seg) and corr[int(np.argmax(seg)) + min_lag] > 0.3 * corr[0]:
            voiced += 1
    return voiced / total if total else 0.0


def spectral(x, sr):
    if len(x) < 64:
        return dict(centroid=None, flatness=None, high_ratio=None)
    spec = np.abs(np.fft.rfft(x * np.hanning(len(x)))) + 1e-12
    freqs = np.fft.rfftfreq(len(x), 1 / sr)
    e = float(np.sum(spec))
    return dict(
        centroid=float(np.sum(freqs * spec) / e) if e else None,
        flatness=float(np.exp(np.mean(np.log(spec))) / np.mean(spec)),
        high_ratio=float(np.sum(spec[freqs >= 2000]) / e) if e else None,
    )


def zcr(x):
    return float(np.mean(np.abs(np.diff(np.signbit(x).astype(np.int8))))) if len(x) > 1 else 0.0


def region_features(x, sr, s0, s1, label, final_phone):
    i0, i1 = max(0, int(s0 * sr)), min(len(x), int(s1 * sr))
    seg = x[i0:i1]
    pre = x[max(0, i0 - int(0.05 * sr)) : i0]
    post = x[i1 : min(len(x), i1 + int(0.05 * sr))]
    sp = spectral(seg, sr)
    r_before = rms_of(pre)
    r_during = rms_of(seg)
    r_after = rms_of(post)
    return {
        "region": label,
        "start_s": round(s0, 4),
        "end_s": round(s1, 4),
        "duration_s": round((i1 - i0) / sr, 4),
        "rms": r_during,
        "rms_before": r_before,
        "rms_after": r_after,
        "rel_energy_drop": (r_before - r_during) / r_before if r_before > 1e-9 else None,
        "zcr": zcr(seg),
        "voicing_ratio": voicing_ratio(seg, sr),
        **sp,
        "is_voiced_class": voiced(final_phone),
    }


def write_csv(p: Path, rows, fields=None):
    if not rows:
        p.write_text("empty\n", encoding="utf-8")
        return
    keys = fields or list(rows[0].keys())
    with open(p, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)


def acoustic_indicator(final_phone, raw_feat):
    """Exploratory, phoneme-class rules (documented; NOT optimized on human labels)."""
    p = base_phone(final_phone)
    vr = raw_feat.get("voicing_ratio") or 0.0
    rd = raw_feat.get("rms") or 0.0
    rb = raw_feat.get("rms_before") or 0.0
    hi = raw_feat.get("high_ratio") or 0.0
    z = raw_feat.get("zcr") or 0.0
    ratio = rd / rb if rb > 1e-9 else 0.0
    if p in {"r", "l"}:
        present = vr >= 0.25 and ratio >= 0.10
    elif p in {"v", "dh", "z", "zh"}:
        present = (vr >= 0.20 and ratio >= 0.08) or (hi >= 0.30 and ratio >= 0.10)
    elif p in {"m", "n", "ng"}:
        present = vr >= 0.25 and ratio >= 0.08
    elif p in {"s", "sh", "f", "th"}:
        present = hi >= 0.25 and ratio >= 0.05
    elif p in {"t", "d", "k", "g", "p", "b"}:
        present = ratio >= 0.05 or z >= 0.15
    else:
        present = ratio >= 0.05
    return "PRESENT" if present else "ABSENT"


def main():
    tgt = CmuDictTargetAdapter()
    pev = PhoneEvidenceV2()

    # ---------- inventory ----------
    base = list(csv.DictReader(P198.open(encoding="utf-8-sig")))
    base = [r for r in base if r.get("soft_full") not in (None, "")]
    child_words = Counter(r["target"] for r in base)
    child_speakers = defaultdict(set)
    for r in base:
        child_speakers[r["target"]].add(r["speaker_id"])

    inv_rows = []
    for w in sorted(child_words):
        st = tgt.build(w)
        phones = [str(p) for p in st.arpabet]
        fp = phones[-1]
        inv_rows.append(
            {
                "word": w,
                "canonical_phones": " ".join(phones),
                "final_phone": fp,
                "final_phone_class": phone_class(fp),
                "n_child_examples": child_words[w],
                "n_speakers": len(child_speakers[w]),
                "target_available": phone_class(fp) != "VOWEL",
                "source": "CHILD_WORD",
            }
        )
    for f in sorted(LWE_AUDIO.glob("sapi_*.wav")):
        w = f.stem.replace("sapi_", "").replace("_", " ")
        last = w.split()[-1]
        st = tgt.build(last)
        phones = [str(p) for p in st.arpabet]
        fp = phones[-1]
        in_child = w in child_words
        inv_rows.append(
            {
                "word": w,
                "canonical_phones": " ".join(phones),
                "final_phone": fp,
                "final_phone_class": phone_class(fp),
                "n_child_examples": child_words.get(w, 0),
                "n_speakers": len(child_speakers.get(w, [])),
                "target_available": in_child,
                "source": "LWE_VOCAB",
            }
        )
    write_csv(RES / "final_consonant_inventory.csv", inv_rows)

    class_availability = Counter()
    for r in inv_rows:
        if r["source"] == "CHILD_WORD" and r["target_available"]:
            class_availability[r["final_phone_class"]] += r["n_child_examples"]
    print("class availability (child examples):", dict(class_availability))

    # ---------- features for final-consonant tokens ----------
    feat_rows = []
    token_meta = {}
    for r in base:
        w = r["target"]
        st = tgt.build(w)
        phones = [str(p) for p in st.arpabet]
        fp = phones[-1]
        if phone_class(fp) == "VOWEL":
            continue
        sid = r["speaker_id"]
        src = find_source(sid, w)
        if src is None:
            continue
        x, sr = load_mono16(src)
        tmp = RES / "_tmp.wav"
        sf.write(str(tmp), x, sr)
        sm = pev.soft_match(str(tmp), st.arpabet)
        hits = ser_hits(sm.hits)
        last = hits[-1] if hits else None
        if last is None:
            continue
        s0 = float(last.get("start_s") or 0.0)
        s1 = float(last.get("end_s") or (len(x) / sr))
        token_id = f"fc_{sid}_{w}"
        token_meta[token_id] = {
            "token_id": token_id,
            "speaker_id": sid,
            "word": w,
            "final_phone": fp,
            "final_phone_class": phone_class(fp),
            "full_score": float(r["soft_full"]),
            "phone_final_match": str(last.get("match_type")),
            "phone_final_sim": last.get("sim"),
            "phone_final_posterior": last.get("posterior"),
            "ctc_final_start": s0,
            "ctc_final_end": s1,
            "ctc_final_duration": max(0.0, s1 - s0),
        }
        for pad in PADS:
            p = pad / 1000.0
            label = "FINAL_RAW" if pad == 0 else f"FINAL_PAD_{pad}"
            f = region_features(x, sr, max(0.0, s0 - p), min(len(x) / sr, s1 + p), label, fp)
            f.update({k: token_meta[token_id][k] for k in ("token_id", "speaker_id", "word", "final_phone", "final_phone_class", "full_score")})
            feat_rows.append(f)
        raw = next(f for f in feat_rows if f["token_id"] == token_id and f["region"] == "FINAL_RAW")
        token_meta[token_id]["acoustic_indicator"] = acoustic_indicator(fp, raw)
        token_meta[token_id]["acoustic_raw_voicing"] = raw["voicing_ratio"]
        token_meta[token_id]["acoustic_raw_energy_ratio"] = (
            raw["rms"] / raw["rms_before"] if raw["rms_before"] and raw["rms_before"] > 1e-9 else None
        )
        print(token_id, fp, "phone", last.get("match_type"), "acoustic", token_meta[token_id]["acoustic_indicator"], flush=True)
        try:
            tmp.unlink()
        except Exception:
            pass
    write_csv(RES / "final_consonant_features.csv", feat_rows)

    # ---------- preliminary human labels from 1.9.11 packs ----------
    human_final = {}  # (speaker, word) -> (label, source)
    sm = list(csv.DictReader((P1911 / "scorer_miss_human_review.csv").open(encoding="utf-8-sig"))) if (P1911 / "scorer_miss_human_review.csv").exists() else []
    for r in sm:
        if r.get("human_state") == "HUMAN_TRUE_ERROR":
            human_final[(r["speaker_id"], r["target"])] = ("FINAL_CONSONANT_CLEARLY_ABSENT", "scorer_miss_human+ending_sound_rule")
    spot = list(csv.DictReader((P1911 / "window_human_spot_check.csv").open(encoding="utf-8-sig"))) if (P1911 / "window_human_spot_check.csv").exists() else []
    for r in spot:
        st = r.get("human_state")
        key = (r["speaker_id"], r["target"])
        if st == "HUMAN_CONFIRMED_ERROR" and r["target"] == "four":
            human_final.setdefault(key, ("FINAL_CONSONANT_CLEARLY_ABSENT", "window_spot_human+ending_sound_rule"))
        elif st == "HUMAN_CONFIRMED_CORRECT":
            human_final.setdefault(key, ("FINAL_CONSONANT_CLEARLY_PRESENT", "window_spot_human_inferred_from_word_correct"))

    human_vs_model = []
    value_counter = Counter()
    for token_id, m in token_meta.items():
        key = (m["speaker_id"], m["word"])
        h = human_final.get(key)
        hlabel = h[0] if h else ""
        hsrc = h[1] if h else ""
        phone_ok = m["phone_final_match"] == "exact"
        ac_ok = m["acoustic_indicator"] == "PRESENT"
        row = {
            **m,
            "human_final_label": hlabel,
            "human_label_source": hsrc,
            "phone_final_ok": phone_ok,
            "acoustic_final_ok": ac_ok,
        }
        human_vs_model.append(row)
        if hlabel:
            human_present = "PRESENT" in hlabel and "ABSENT" not in hlabel
            if phone_ok and ac_ok == human_present:
                value_counter["BOTH_RIGHT"] += 1
            elif (not phone_ok) and ac_ok == human_present:
                value_counter["PHONE_WRONG_ACOUSTIC_RIGHT"] += 1
            elif phone_ok and ac_ok != human_present:
                value_counter["PHONE_RIGHT_ACOUSTIC_WRONG"] += 1
            else:
                value_counter["BOTH_WRONG"] += 1
    write_csv(RES / "final_consonant_human_vs_model.csv", human_vs_model)

    value_rows = [
        {
            "metric": k,
            "count": v,
            "note": "preliminary: only tokens with existing human labels; full evaluation pending final-consonant pack",
        }
        for k, v in value_counter.items()
    ]
    value_rows.append(
        {
            "metric": "N_WITH_HUMAN_FINAL_LABEL",
            "count": sum(1 for r in human_vs_model if r["human_final_label"]),
            "note": "inferred labels only; not the new blind final-consonant review",
        }
    )
    value_rows.append(
        {
            "metric": "N_PENDING_HUMAN_FINAL_REVIEW",
            "count": sum(1 for r in human_vs_model if not r["human_final_label"]),
            "note": "",
        }
    )
    write_csv(RES / "acoustic_support_value.csv", value_rows)

    # ---------- final consonant x window interaction ----------
    win = {r["token_id"]: r for r in csv.DictReader((P1911 / "window_ab_80_tokens.csv").open(encoding="utf-8-sig"))}
    # map window token ids (tok_XX) to speaker/word via 1.9.8 order
    win_by_key = {}
    for i, r in enumerate(base):
        win_by_key[(r["speaker_id"], r["target"])] = win.get(f"tok_{i+1:02d}", {})
    interaction = []
    for token_id, m in token_meta.items():
        wrow = win_by_key.get((m["speaker_id"], m["word"]), {})
        pads = [float(wrow.get(f"pad{p}_score", 0) or 0) for p in (100, 200, 250, 300, 500)]
        raw_score = float(wrow.get("raw_vad_score", 0) or 0)
        restored = raw_score < 50 and max(pads) >= 50 if pads else None
        interaction.append(
            {
                "token_id": token_id,
                "speaker_id": m["speaker_id"],
                "word": m["word"],
                "final_phone": m["final_phone"],
                "final_phone_class": m["final_phone_class"],
                "full_score": wrow.get("full_score"),
                "raw_score": raw_score,
                "best_pad_score": max(pads) if pads else None,
                "final_restored_by_padding": restored,
                "score_range": wrow.get("score_range"),
            }
        )
    write_csv(RES / "final_consonant_window_interaction.csv", interaction)

    # ---------- human review sample (final consonant) ----------
    fc_tokens = [m for m in token_meta.values()]
    by_band = defaultdict(list)
    for m in fc_tokens:
        s = m["full_score"]
        band = "HIGH" if s >= 80 else "MID" if s >= 50 else "LOW" if s >= 20 else "VERY_LOW"
        by_band[band].append(m)
    sample = []
    seen = set()

    def add(pool, n):
        for m in sorted(pool, key=lambda r: r["full_score"]):
            if len(sample) >= 24:
                return
            if m["token_id"] in seen:
                continue
            seen.add(m["token_id"])
            sample.append(m)
            n -= 1
            if n <= 0:
                return

    for band in ("VERY_LOW", "LOW", "MID", "HIGH"):
        add(by_band.get(band, []), 6)
    # add four cases explicitly (final-absent candidates)
    for m in fc_tokens:
        if m["word"] == "four" and m["token_id"] not in seen and len(sample) < 28:
            seen.add(m["token_id"])
            sample.append(m)

    review_items = []
    clips_dir = OUT / "HumanReview" / "fc_clips"
    clips_dir.mkdir(parents=True, exist_ok=True)
    for m in sample:
        src = find_source(m["speaker_id"], m["word"])
        if src is None:
            continue
        x, sr = load_mono16(src)
        dur = len(x) / sr
        clip = clips_dir / f"{m['token_id']}.wav"
        sf.write(str(clip), x, sr)
        review_items.append(
            {
                "token_id": m["token_id"],
                "speaker_id": m["speaker_id"],
                "word": m["word"],
                "final_phone": m["final_phone"],
                "final_phone_class": m["final_phone_class"],
                "clip": f"fc_clips/{m['token_id']}.wav",
                "full_score": m["full_score"],
                "phone_final_match": m["phone_final_match"],
                "phone_final_sim": m["phone_final_sim"],
                "acoustic_indicator": m["acoustic_indicator"],
                "acoustic_raw_voicing": m["acoustic_raw_voicing"],
                "ctc_final_duration": m["ctc_final_duration"],
            }
        )
    write_csv(
        RES / "final_consonant_human_review.csv",
        [
            {
                "case_id": it["token_id"],
                "speaker_id": it["speaker_id"],
                "word": it["word"],
                "final_phone": it["final_phone"],
                "human_final_label": "",
                "reviewer_confidence": "",
                "notes": "",
                "reviewer_id": "",
                "review_date": "",
            }
            for it in review_items
        ],
    )
    (OUT / "HumanReview" / "review_metadata.json").write_text(
        json.dumps(
            {
                "final_consonant": {
                    "n": len(review_items),
                    "schema": [
                        "FINAL_CONSONANT_CLEARLY_PRESENT",
                        "FINAL_CONSONANT_PROBABLY_PRESENT",
                        "FINAL_CONSONANT_AMBIGUOUS",
                        "FINAL_CONSONANT_PROBABLY_ABSENT",
                        "FINAL_CONSONANT_CLEARLY_ABSENT",
                    ],
                    "items": review_items,
                }
            },
            indent=2,
            ensure_ascii=True,
        ),
        encoding="utf-8",
    )
    build_fc_html(review_items)

    master = {
        "phase": "1.9.12",
        "objective_a": {
            "final_consonant_tokens_total": len(fc_tokens),
            "class_availability_child_examples": dict(class_availability),
            "n_with_existing_human_final_label": sum(1 for r in human_vs_model if r["human_final_label"]),
            "preliminary_value_counts": dict(value_counter),
            "review_sample_n": len(review_items),
            "review_pending": True,
        },
        "decisions": {
            "acoustic_support": "C. ACOUSTIC_SUPPORT_IS_PROMISING_BUT_INSUFFICIENT",
            "window_policy": "D. EVIDENCE_INSUFFICIENT",
        },
        "production_vad": False,
        "router_locked": False,
        "unity_integrated": False,
        "scorer_modified": False,
        "production_window_locked": False,
    }
    (RES / "phase_1_9_12_master.json").write_text(json.dumps(master, indent=2, ensure_ascii=True), encoding="utf-8")
    print("DONE", json.dumps(master["objective_a"], ensure_ascii=True))


def build_fc_html(items):
    style = """<style>
*{box-sizing:border-box}
body{font-family:system-ui,-apple-system,sans-serif;margin:0 auto;max-width:760px;padding:12px 12px 96px;line-height:1.45;background:#f7f7f8}
.banner{background:#e7f1ff;border:1px solid #a9c9f5;border-radius:12px;padding:14px;margin-bottom:14px}
.card{background:#fff;border:1px solid #ddd;border-radius:12px;padding:14px;margin:0 0 16px}
.card h2{font-size:1.05rem;margin:0 0 6px}
.audio-block{margin:8px 0}.lbl{font-size:.8rem;font-weight:600;color:#333;margin-bottom:4px}
audio{width:100%}
fieldset{border:1px solid #ccc;border-radius:10px;margin:12px 0 0;padding:10px;background:#fafafa}
legend{font-weight:700;font-size:.95rem;padding:0 6px}
.opts{display:flex;flex-direction:column;gap:8px;margin-top:8px}
.opts label{display:flex;align-items:center;gap:10px;min-height:44px;padding:10px 12px;border:1px solid #ccc;border-radius:10px;background:#fff;font-size:.95rem}
.opts input{width:20px;height:20px;flex-shrink:0}
.opts label:has(input:checked){border-color:#0b5fff;background:#e8f0ff}
.notes{width:100%;min-height:44px;font-size:16px;padding:10px;border:1px solid #ccc;border-radius:10px;margin-top:8px}
.sticky{position:fixed;left:0;right:0;bottom:0;background:#111;color:#fff;padding:12px 14px calc(12px + env(safe-area-inset-bottom));display:flex;gap:10px;z-index:50}
.sticky button{flex:1;min-height:48px;font-size:1rem;font-weight:700;border:0;border-radius:10px;background:#0b5fff;color:#fff}
.sticky .count{align-self:center;font-size:.85rem;white-space:nowrap}
pre{white-space:pre-wrap;font-size:.78rem;background:#eee;padding:8px;border-radius:8px}
</style>"""
    P = [
        "FINAL_CONSONANT_CLEARLY_PRESENT",
        "FINAL_CONSONANT_PROBABLY_PRESENT",
        "FINAL_CONSONANT_AMBIGUOUS",
        "FINAL_CONSONANT_PROBABLY_ABSENT",
        "FINAL_CONSONANT_CLEARLY_ABSENT",
    ]
    PL = {
        "FINAL_CONSONANT_CLEARLY_PRESENT": "Âm cuối RÕ có",
        "FINAL_CONSONANT_PROBABLY_PRESENT": "Có lẽ có",
        "FINAL_CONSONANT_AMBIGUOUS": "Không chắc",
        "FINAL_CONSONANT_PROBABLY_ABSENT": "Có lẽ mất",
        "FINAL_CONSONANT_CLEARLY_ABSENT": "Mất rõ",
    }
    C = ["HIGH", "MEDIUM", "LOW"]
    CL = {"HIGH": "Chắc cao", "MEDIUM": "Chắc vừa", "LOW": "Chắc thấp"}

    def radios(name, values, labels):
        return '<div class="opts">' + "".join(
            f'<label><input type="radio" name="{name}" value="{v}"><span>{labels.get(v, v)}</span></label>'
            for v in values
        ) + "</div>"

    cards_a, cards_b = [], []
    for it in items:
        tid = it["token_id"]
        cards_a.append(
            f"""<section class="card"><h2>{tid}</h2>
<p class="meta">word=<b>{it['word'].upper()}</b> · final sound /{it['final_phone']}/ ({it['final_phone_class']})</p>
<div class="audio-block"><div class="lbl">FULL</div><audio controls preload="none" src="{it['clip']}"></audio></div>
<fieldset><legend>Nghe âm cuối (ẩn model)</legend>{radios('p_' + tid, P, PL)}</fieldset>
<fieldset><legend>Độ chắc</legend>{radios('c_' + tid, C, CL)}</fieldset>
<input class="notes" name="n_{tid}" placeholder="ghi chú (tuỳ chọn)">
</section>"""
        )
        cards_b.append(
            f"""<section class="card"><h2>{tid}</h2>
<audio controls preload="none" src="{it['clip']}"></audio>
<pre>word={it['word']} final=/{it['final_phone']} ({it['final_phone_class']})
phone_final={it['phone_final_match']} sim={it['phone_final_sim']}
acoustic_indicator={it['acoustic_indicator']} voicing={it['acoustic_raw_voicing']}
ctc_final_duration={it['ctc_final_duration']} full_score={it['full_score']}</pre>
</section>"""
        )
    ids = json.dumps([it["token_id"] for it in items])
    submit_js = """
async function submitCsv(t,stage,btnId){
 const rid=(document.getElementById('reviewer')||{value:'human_mobile'}).value||'human_mobile';
 const btn=document.getElementById(btnId);
 try{
  const r=await fetch('/submit',{method:'POST',headers:{'Content-Type':'application/json'},
   body:JSON.stringify({pack:'final_consonant',stage:stage,csv:t,reviewer_id:rid,timestamp:new Date().toISOString()})});
  if(r.ok){const j=await r.json();btn.textContent='Đã nộp ✓ ('+j.rows+' dòng)';btn.style.background='#1a7f37';return;}
  throw new Error('http '+r.status);
 }catch(e){
  btn.textContent='Không gửi được — tải CSV';
  const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([t],{type:'text/csv'}));
  a.download='FinalConsonant_Stage'+stage+'_Filled.csv';a.click();
 }
}
"""
    blind = f"""<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, viewport-fit=cover">
<title>1.9.12 final consonant blind</title>{style}</head><body>
<div class="banner"><h1>Final consonant — chấm mù</h1>
<p>Nghe âm cuối của từ. <b>Ẩn</b> phone/acoustic cho đến Stage B.</p>
<p><label>Reviewer: <input id="reviewer" value="human_mobile" size="16"></label></p></div>
{''.join(cards_a)}
<div class="sticky"><span class="count" id="prog">0/{len(items)}</span><button id="exp">Nộp Stage A</button></div>
<script>
const ids={ids};
function v(p,id){{const e=document.querySelector('input[name="'+p+'_'+id+'"]:checked');return e?e.value:'';}}
function upd(){{let n=0;for(const id of ids)if(v('p',id))n++;document.getElementById('prog').textContent=n+'/'+ids.length;}}
document.body.addEventListener('change',upd);upd();
{submit_js}
document.getElementById('exp').onclick=async()=>{{
 let lines=['case_id,human_final_label,reviewer_confidence,notes'];
 for(const id of ids){{
  const notes=(document.querySelector('input[name="n_'+id+'"]')||{{value:''}}).value.replace(/,/g,';');
  lines.push([id,v('p',id),v('c',id),notes].join(','));
 }}
 await submitCsv(lines.join('\\n'),'A','exp');
}};
</script></body></html>"""
    (OUT / "HumanReview" / "final_consonant_blind.html").write_text(blind, encoding="utf-8")
    reveal = f"""<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>1.9.12 final consonant reveal</title>{style}</head><body>
<h1>Final consonant — Stage B</h1>
<p>Dùng sau khi Stage A đã chấm mù.</p>
{''.join(cards_b)}</body></html>"""
    (OUT / "HumanReview" / "final_consonant_reveal.html").write_text(reveal, encoding="utf-8")


if __name__ == "__main__":
    main()
