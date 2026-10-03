"""Phase 1.9.11 — 80-token window A/B (frozen scorer; window policy only).

Conditions: FULL (from frozen 1.9.8 CSV), RAW_VAD, PAD_100/200/250/300/500.
Policies: M1 oracle-best (upper bound), M2 median, M3 smallest stable pad,
M4 agreement pad200 vs pad300 (thresholds tested 5/10/15).
"""
from __future__ import annotations

import csv
import hashlib
import json
import re
import sys
from collections import Counter
from pathlib import Path

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_9.HybridVAD.hybrid_vad import HybridVAD
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

OUT = REPO / "Research/Speech/Phase1_9_11"
RES = OUT / "Results"
HR = OUT / "HumanReview"
CLIPS = HR / "spotcheck_clips"
P198 = REPO / "Research/Speech/Phase1_9_8/Results/pronunciation_results.csv"
P199 = REPO / "Research/Speech/Phase1_9_9/Results"
P1910 = REPO / "Research/Speech/Phase1_9_10/Results"
EXT = REPO / "Research/Speech/ExternalData/zenodo_200495/extracted/english_children"
DER = REPO / "Research/Speech/ExternalData/zenodo_200495/derived_16k"
for d in (OUT, RES, HR, CLIPS, OUT / "Scripts"):
    d.mkdir(parents=True, exist_ok=True)

PADS = [100, 200, 250, 300, 500]
RANGE_SENSITIVE = 20.0
STABLE_RANGE = 10.0
UNDER_THRESH = 10.0
OVER_THRESH = 10.0


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


def write_csv(p: Path, rows, fields=None):
    if not rows:
        p.write_text("empty\n", encoding="utf-8")
        return
    keys = fields or list(rows[0].keys())
    with open(p, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)


def build_status_map():
    reviewed = list(csv.DictReader((P199 / "human_review_results.csv").open(encoding="utf-8-sig")))
    pme_rows = [r for r in reviewed if r.get("diagnostic") == "PHONE_MODEL_ERROR"]
    pme_ids = [r["review_id"] for r in pme_rows]
    sp = list(csv.DictReader((P1910 / "human_second_pass.csv").open(encoding="utf-8-sig")))
    assert len(sp) == len(pme_ids)
    second = {pme_ids[i]: sp[i]["human_state"] for i in range(len(pme_ids))}

    status = {}
    conflict = {}
    for r in reviewed:
        rid = r["review_id"]
        hp = r.get("human_pronunciation") or ""
        key = (r["speaker_id"], r["target"])
        if rid in second:
            st = second[rid]
            if st == "HUMAN_TRUE_ERROR":
                status[key] = "HUMAN_CONFIRMED_ERROR"
            elif st == "HUMAN_CONFLICTED":
                status[key] = "HUMAN_UNCERTAIN"
                conflict[key] = True
            else:
                status[key] = "HUMAN_UNCERTAIN"
        elif hp in ("CLEAR_CORRECT", "PROBABLY_CORRECT"):
            status[key] = "HUMAN_CONFIRMED_CORRECT"
        elif hp in ("CLEAR_INCORRECT", "PROBABLY_INCORRECT"):
            status[key] = "HUMAN_CONFIRMED_ERROR"
        else:
            status[key] = "HUMAN_UNCERTAIN"
    return status, conflict


def main():
    base = list(csv.DictReader(P198.open(encoding="utf-8-sig")))
    base = [r for r in base if r.get("soft_full") not in (None, "")]
    assert len(base) == 80, len(base)
    status_map, conflict_map = build_status_map()

    hv = HybridVAD()
    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()
    tmp = RES / "_tmp.wav"

    rows = []
    for i, r in enumerate(base):
        sid, target = r["speaker_id"], r["target"]
        token_id = f"tok_{i+1:02d}"
        key = (sid, target)
        human = status_map.get(key, "NOT_HUMAN_REVIEWED")
        conflict = bool(conflict_map.get(key, False))
        src = find_source(sid, target)
        if src is None:
            rows.append({"token_id": token_id, "speaker_id": sid, "target": target, "error": "SOURCE_MISSING"})
            continue
        x, sr = load_mono16(src)
        dur = len(x) / sr
        st = tgt.build(target)
        sf.write(str(tmp), x, sr)
        rs = hv.run(str(tmp), mode="silero", silero_thr=0.5)
        segs = rs["segments"]

        scores = {"full": float(r["soft_full"])}
        confs = {"full": float(r["conf_full"]) if r.get("conf_full") not in (None, "") else None}
        for pm in [0] + PADS:
            if not segs:
                s0, s1 = 0.0, dur
            else:
                p = pm / 1000.0
                s0 = max(0.0, float(segs[0]["start"]) - p)
                s1 = min(dur, float(segs[-1]["end"]) + p)
            sf.write(str(tmp), x[int(s0 * sr) : int(s1 * sr)], sr)
            sm = pev.soft_match(str(tmp), st.arpabet)
            name = "raw" if pm == 0 else f"pad{pm}"
            scores[name] = float(sm.soft_score_0_100)
            confs[name] = float(sm.confidence_0_1)

        variant_scores = [scores["raw"]] + [scores[f"pad{p}"] for p in PADS]
        pad_scores = [scores[f"pad{p}"] for p in PADS]
        score_range = max(scores.values()) - min(scores.values())
        best_variant = max(scores, key=scores.get)
        worst_variant = min(scores, key=scores.get)
        median_pad = float(np.median(pad_scores))

        # policy M1 oracle
        m1 = max(scores.values())
        # M2 median across 6 windows (raw + 5 pads)
        m2 = float(np.median(variant_scores))
        # M3 smallest stable padded window: first pad whose score is within 5 of pad500
        m3 = None
        m3_pad = None
        for p in PADS:
            if abs(scores[f"pad{p}"] - scores["pad500"]) <= 5.0:
                m3 = scores[f"pad{p}"]
                m3_pad = p
                break
        if m3 is None:
            m3 = median_pad
            m3_pad = "median_fallback"
        # M4 agreement pad200 vs pad300
        s200, s300 = scores["pad200"], scores["pad300"]
        agree10 = abs(s200 - s300) <= 10.0
        m4 = (s200 + s300) / 2 if agree10 else min(s200, s300)

        rows.append(
            {
                "token_id": token_id,
                "speaker_id": sid,
                "target": target,
                "human_status": human,
                "human_conflict": conflict,
                "full_score": scores["full"],
                "raw_vad_score": scores["raw"],
                "pad100_score": scores["pad100"],
                "pad200_score": scores["pad200"],
                "pad250_score": scores["pad250"],
                "pad300_score": scores["pad300"],
                "pad500_score": scores["pad500"],
                "full_confidence": confs["full"],
                "raw_confidence": confs["raw"],
                "pad100_confidence": confs["pad100"],
                "pad200_confidence": confs["pad200"],
                "pad250_confidence": confs["pad250"],
                "pad300_confidence": confs["pad300"],
                "pad500_confidence": confs["pad500"],
                "best_variant": best_variant,
                "worst_variant": worst_variant,
                "score_range": score_range,
                "boundary_sensitive": score_range >= RANGE_SENSITIVE,
                "median_pad_score": median_pad,
                "full_vs_pad": (
                    "FULL_UNDERSCORES"
                    if scores["full"] < median_pad - UNDER_THRESH
                    else "FULL_OVERSCORES"
                    if scores["full"] > median_pad + OVER_THRESH
                    else "WINDOW_STABLE"
                ),
                "m1_oracle": m1,
                "m2_median": m2,
                "m3_smallest_stable": m3,
                "m3_pad_used": m3_pad,
                "m4_agreement": m4,
                "m4_agree10": agree10,
                "silero_n_segs": len(segs),
                "duration_s": dur,
            }
        )
        if (i + 1) % 10 == 0:
            print(f"progress {i+1}/80", flush=True)

    write_csv(RES / "window_ab_80_tokens.csv", rows)

    # ---------------- summaries ----------------
    def stats(vals):
        vals = [v for v in vals if v is not None]
        if not vals:
            return {}
        return {
            "n": len(vals),
            "mean": float(np.mean(vals)),
            "median": float(np.median(vals)),
            "p10": float(np.percentile(vals, 10)),
            "p90": float(np.percentile(vals, 90)),
            "frac_ge50": float(np.mean([1 if v >= 50 else 0 for v in vals])),
            "frac_lt50": float(np.mean([1 if v < 50 else 0 for v in vals])),
        }

    variant_names = ["full", "raw"] + [f"pad{p}" for p in PADS]
    summary_rows = []
    for v in variant_names:
        col = "full_score" if v == "full" else ("raw_vad_score" if v == "raw" else f"{v}_score")
        s = stats([r.get(col) for r in rows if "error" not in r])
        s["variant"] = v
        summary_rows.append(s)
    for pol in ["m1_oracle", "m2_median", "m3_smallest_stable", "m4_agreement"]:
        s = stats([r.get(pol) for r in rows if "error" not in r])
        s["variant"] = pol
        summary_rows.append(s)
    write_csv(RES / "window_summary.csv", summary_rows)

    ok = [r for r in rows if "error" not in r]
    ranges = [r["score_range"] for r in ok]
    frac_sensitive = float(np.mean([1 if x >= RANGE_SENSITIVE else 0 for x in ranges]))
    groups = Counter(r["full_vs_pad"] for r in ok)
    raw_beats_full = sum(1 for r in ok if r["raw_vad_score"] >= 50 and r["full_score"] < 50)
    full_beats_raw = sum(1 for r in ok if r["full_score"] >= 50 and r["raw_vad_score"] < 50)
    pad_beats_full = sum(
        1 for r in ok if max(r[f"pad{p}_score"] for p in PADS) >= 50 and r["full_score"] < 50
    )
    full_beats_pad = sum(
        1 for r in ok if r["full_score"] >= 50 and max(r[f"pad{p}_score"] for p in PADS) < 50
    )

    # human-conditioned metrics
    confirmed_err = [r for r in ok if r["human_status"] == "HUMAN_CONFIRMED_ERROR"]
    confirmed_ok = [r for r in ok if r["human_status"] == "HUMAN_CONFIRMED_CORRECT"]
    false_rescue = [
        r for r in confirmed_err if max(r[f"pad{p}_score"] for p in PADS) >= 50
    ]
    true_error_rescue = [
        r
        for r in confirmed_err
        if r["raw_vad_score"] < 50 and max(r[f"pad{p}_score"] for p in PADS) >= 50
    ]
    pad_rescue_correct = [
        r
        for r in confirmed_ok
        if r["full_score"] < 50 and max(r[f"pad{p}_score"] for p in PADS) >= 50
    ]

    # M4 agreement threshold exploration
    agree_rates = {}
    for th in (5, 10, 15):
        agree_rates[f"th{th}"] = float(
            np.mean([1 if abs(r["pad200_score"] - r["pad300_score"]) <= th else 0 for r in ok])
        )

    master = {
        "phase": "1.9.11",
        "n_tokens": len(ok),
        "pads_ms": PADS,
        "thresholds": {
            "range_sensitive": RANGE_SENSITIVE,
            "stable_range": STABLE_RANGE,
            "full_underscore": UNDER_THRESH,
            "full_overscore": OVER_THRESH,
        },
        "window_sensitive_rate": frac_sensitive,
        "median_score_range": float(np.median(ranges)),
        "p90_score_range": float(np.percentile(ranges, 90)),
        "groups": dict(groups),
        "raw_beats_full_count": raw_beats_full,
        "full_beats_raw_count": full_beats_raw,
        "pad_beats_full_count": pad_beats_full,
        "full_beats_pad_count": full_beats_pad,
        "human_status_counts": dict(Counter(r["human_status"] for r in ok)),
        "confirmed_error_n": len(confirmed_err),
        "false_rescue_n": len(false_rescue),
        "true_error_rescue_n": len(true_error_rescue),
        "pad_rescue_confirmed_correct_n": len(pad_rescue_correct),
        "m4_agreement_rates": agree_rates,
        "scorer_formula_changed": False,
        "production_vad": False,
        "router_locked": False,
        "unity_integrated": False,
        "scorer_modified": False,
    }

    # decision
    if frac_sensitive >= 0.5 and len(confirmed_err) >= 5 and len(false_rescue) >= 3:
        decision = "B. WINDOW_EFFECT_REAL_BUT_NOT_SAFE"
        note = "Window effects large and frequent; padding rescues confirmed errors too often"
    elif frac_sensitive >= 0.3 and len(confirmed_err) >= 5:
        decision = "B. WINDOW_EFFECT_REAL_BUT_NOT_SAFE"
        note = "Window effects real; safety cannot be established from current labels"
    elif frac_sensitive < 0.15:
        decision = "C. WINDOW_POLICY_NOT_SUPPORTED"
        note = "Window effects too small to justify a policy"
    else:
        decision = "D. EVIDENCE_INSUFFICIENT"
        note = "Insufficient confirmed human labels to choose a policy"
    master["decision"] = decision
    master["decision_note"] = note

    (RES / "phase_1_9_11_master.json").write_text(json.dumps(master, indent=2, ensure_ascii=True), encoding="utf-8")
    (RES / "decision.json").write_text(
        json.dumps(
            {
                "decision": decision,
                "note": note,
                "window_sensitive_rate": frac_sensitive,
                "false_rescue_n": len(false_rescue),
                "true_error_rescue_n": len(true_error_rescue),
                "scorer_formula_change": "NOT_JUSTIFIED",
                "production_vad": False,
                "router_locked": False,
                "unity_integrated": False,
                "scorer_modified": False,
            },
            indent=2,
        ),
        encoding="utf-8",
    )

    # multi-variant results CSV
    write_csv(
        RES / "multi_variant_results.csv",
        [
            {
                "token_id": r["token_id"],
                "speaker_id": r["speaker_id"],
                "target": r["target"],
                "human_status": r["human_status"],
                "m1_oracle": r["m1_oracle"],
                "m2_median": r["m2_median"],
                "m3_smallest_stable": r["m3_smallest_stable"],
                "m3_pad_used": r["m3_pad_used"],
                "m4_agreement": r["m4_agreement"],
                "m4_agree10": r["m4_agree10"],
                "score_range": r["score_range"],
            }
            for r in ok
        ],
    )

    # ---------------- spot check selection ----------------
    under = sorted([r for r in ok if r["full_vs_pad"] == "FULL_UNDERSCORES"], key=lambda r: r["full_score"])[:5]
    over = sorted([r for r in ok if r["full_vs_pad"] == "FULL_OVERSCORES"], key=lambda r: -r["full_score"])[:5]
    stable = [r for r in ok if r["full_vs_pad"] == "WINDOW_STABLE"]
    stable = stable[:5]
    big_range = sorted(ok, key=lambda r: -r["score_range"])[:5]
    picks = []
    seen = set()
    for group, lst in (("FULL_UNDERSCORES", under), ("FULL_OVERSCORES", over), ("WINDOW_STABLE", stable), ("LARGEST_RANGE", big_range)):
        for r in lst:
            if r["token_id"] in seen:
                continue
            seen.add(r["token_id"])
            picks.append({**r, "spot_group": group})
    spot_rows = [
        {
            "token_id": r["token_id"],
            "speaker_id": r["speaker_id"],
            "target": r["target"],
            "spot_group": r["spot_group"],
            "full_score": r["full_score"],
            "raw_vad_score": r["raw_vad_score"],
            "pad250_score": r["pad250_score"],
            "score_range": r["score_range"],
            "stage_a_pronunciation": "",
            "stage_b_most_responsible": "",
            "reviewer_id": "",
            "review_date": "",
        }
        for r in picks
    ]
    write_csv(RES / "window_human_spot_check.csv", spot_rows)

    # clips for spot check
    spot_items = []
    for r in picks:
        src = find_source(r["speaker_id"], r["target"])
        if src is None:
            continue
        x, sr = load_mono16(src)
        dur = len(x) / sr
        fullp = CLIPS / f"{r['token_id']}_FULL.wav"
        sf.write(str(fullp), x, sr)
        mid = dur / 2
        half = max(1.0, dur / 2)
        s = max(0.0, mid - half)
        e = min(dur, mid + half)
        listenp = CLIPS / f"{r['token_id']}_LISTEN.wav"
        sf.write(str(listenp), x[int(s * sr) : int(e * sr)], sr)
        spot_items.append(
            {
                "token_id": r["token_id"],
                "speaker_id": r["speaker_id"],
                "target": r["target"],
                "spot_group": r["spot_group"],
                "clips": {"LISTEN": f"spotcheck_clips/{r['token_id']}_LISTEN.wav", "FULL": f"spotcheck_clips/{r['token_id']}_FULL.wav"},
                "full_score": r["full_score"],
                "raw_vad_score": r["raw_vad_score"],
                "pad250_score": r["pad250_score"],
                "score_range": r["score_range"],
            }
        )
    # merge spot check into review metadata (append if exists)
    meta_path = HR / "review_metadata.json"
    meta = json.loads(meta_path.read_text(encoding="utf-8")) if meta_path.exists() else {}
    meta["window_spot_check"] = {
        "n": len(spot_items),
        "note": "balanced: FULL_UNDERSCORES/FULL_OVERSCORES/WINDOW_STABLE/LARGEST_RANGE; blind first",
        "items": spot_items,
    }
    meta_path.write_text(json.dumps(meta, indent=2, ensure_ascii=True), encoding="utf-8")
    build_spotcheck_html(spot_items)

    print("WINDOW_SENSITIVE_RATE", round(frac_sensitive, 3))
    print("GROUPS", dict(groups))
    print("raw_beats_full", raw_beats_full, "full_beats_raw", full_beats_raw, "pad_beats_full", pad_beats_full, "full_beats_pad", full_beats_pad)
    print("HUMAN", dict(Counter(r["human_status"] for r in ok)))
    print("confirmed_err", len(confirmed_err), "false_rescue", len(false_rescue), "true_error_rescue", len(true_error_rescue))
    print("DECISION", decision)
    try:
        tmp.unlink()
    except Exception:
        pass


def build_spotcheck_html(items):
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
pre{white-space:pre-wrap;font-size:.78rem;background:#eee;padding:8px;border-radius:8px}
</style>"""
    P = ["CLEAR_CORRECT", "PROBABLY_CORRECT", "AMBIGUOUS", "PROBABLY_INCORRECT", "CLEAR_INCORRECT"]
    PL = {"CLEAR_CORRECT": "Rõ — đúng", "PROBABLY_CORRECT": "Có lẽ đúng", "AMBIGUOUS": "Không chắc", "PROBABLY_INCORRECT": "Có lẽ sai", "CLEAR_INCORRECT": "Rõ — sai"}
    B = ["WINDOW_EFFECT", "TRUE_PRONUNCIATION_ERROR", "AUDIO_QUALITY", "UNCERTAIN"]
    BL = {"WINDOW_EFFECT": "Do cửa sổ cắt", "TRUE_PRONUNCIATION_ERROR": "Trẻ phát âm sai thật", "AUDIO_QUALITY": "Chất lượng audio", "UNCERTAIN": "Không rõ"}

    def radios(name, values, labels):
        return '<div class="opts">' + "".join(
            f'<label><input type="radio" name="{name}" value="{v}"><span>{labels.get(v, v)}</span></label>'
            for v in values
        ) + "</div>"

    cards_a, cards_b = [], []
    for it in items:
        tid = it["token_id"]
        cards_a.append(
            f"""<section class="card"><h2>{tid} — {it['target'].upper()}</h2>
<p class="meta">speaker={it['speaker_id']} · group hidden in blind mode</p>
<div class="audio-block"><div class="lbl">LISTEN</div><audio controls preload="none" src="{it['clips']['LISTEN']}"></audio></div>
<div class="audio-block"><div class="lbl">FULL</div><audio controls preload="none" src="{it['clips']['FULL']}"></audio></div>
<fieldset><legend>Nghe và chấm phát âm (ẩn điểm)</legend>{radios('p_' + tid, P, PL)}</fieldset>
<input class="notes" name="n_{tid}" placeholder="ghi chú (tuỳ chọn)">
</section>"""
        )
        cards_b.append(
            f"""<section class="card"><h2>{tid} — {it['target'].upper()}</h2>
<p class="meta">speaker={it['speaker_id']} · group={it['spot_group']}</p>
<audio controls preload="none" src="{it['clips']['FULL']}"></audio>
<pre>full={it['full_score']}  raw={it['raw_vad_score']}  pad250={it['pad250_score']}  range={it['score_range']}</pre>
<fieldset><legend>Điểm thay đổi theo cửa sổ — theo bạn vì sao?</legend>{radios('s_' + tid, B, BL)}</fieldset>
</section>"""
        )
    ids = json.dumps([it["token_id"] for it in items])
    submit_js = """
async function submitCsv(t,stage,btnId){
 const rid=(document.getElementById('reviewer')||{value:'human_mobile'}).value||'human_mobile';
 const btn=document.getElementById(btnId);
 try{
  const r=await fetch('/submit',{method:'POST',headers:{'Content-Type':'application/json'},
   body:JSON.stringify({stage:stage,csv:t,reviewer_id:rid,timestamp:new Date().toISOString()})});
  if(r.ok){const j=await r.json();btn.textContent='Đã nộp ✓ ('+j.rows+' dòng)';btn.style.background='#1a7f37';return;}
  throw new Error('http '+r.status);
 }catch(e){
  btn.textContent='Không gửi được — tải CSV';
  const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([t],{type:'text/csv'}));
  a.download='WindowSpot_Stage'+stage+'_Filled.csv';a.click();
 }
}
"""
    blind = f"""<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, viewport-fit=cover">
<title>1.9.11 window spot-check blind</title>{style}</head><body>
<div class="banner"><h1>Window spot-check — chấm mù</h1>
<p>Nghe trước, chấm phát âm. Điểm số ẩn cho đến Stage B.</p>
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
 let lines=['token_id,stage_a_pronunciation,notes'];
 for(const id of ids){{
  const notes=(document.querySelector('input[name="n_'+id+'"]')||{{value:''}}).value.replace(/,/g,';');
  lines.push([id,v('p',id),notes].join(','));
 }}
 await submitCsv(lines.join('\\n'),'A','exp');
}};
</script></body></html>"""
    (HR / "window_spotcheck_blind.html").write_text(blind, encoding="utf-8")

    reveal = f"""<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>1.9.11 window spot-check reveal</title>{style}</head><body>
<h1>Window spot-check — Stage B</h1>
<p>Dùng sau khi Stage A đã chấm mù.</p>
<p><label>Reviewer: <input id="reviewer" value="human_mobile" size="16"></label></p>
{''.join(cards_b)}
<p><button id="expb" style="min-height:48px;font-size:1rem;font-weight:700;border:0;border-radius:10px;background:#0b5fff;color:#fff;padding:0 16px">Nộp Stage B</button></p>
<script>
const ids={ids};
function v(p,id){{const e=document.querySelector('input[name="'+p+'_'+id+'"]:checked');return e?e.value:'';}}
{submit_js}
document.getElementById('expb').onclick=async()=>{{
 let lines=['token_id,stage_b_most_responsible'];
 for(const id of ids)lines.push([id,v('s',id)].join(','));
 await submitCsv(lines.join('\\n'),'B','expb');
}};
</script></body></html>"""
    (HR / "window_spotcheck_reveal.html").write_text(reveal, encoding="utf-8")


if __name__ == "__main__":
    main()
