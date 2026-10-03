"""Phase 1.9.11 — forensics for the 6 SCORER_MISS cases (reverse failure).

Frozen scorer only; no formula changes. ASR is supporting evidence.
"""
from __future__ import annotations

import csv
import hashlib
import json
import re
import sys
from collections import Counter
from dataclasses import is_dataclass
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
CLIPS = HR / "clips"
P199 = REPO / "Research/Speech/Phase1_9_9/Results"
EXT = REPO / "Research/Speech/ExternalData/zenodo_200495/extracted/english_children"
DER = REPO / "Research/Speech/ExternalData/zenodo_200495/derived_16k"
for d in (OUT, RES, HR, CLIPS, OUT / "Scripts"):
    d.mkdir(parents=True, exist_ok=True)

PADS_MS = [0, 100, 200, 250, 300, 500]
VOWELS = {"aa", "ae", "ah", "ao", "aw", "ay", "eh", "er", "ey", "ih", "iy", "ow", "oy", "uh", "uw"}


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
        else:
            out.append({k: getattr(h, k) for k in ("expected", "best_obs", "match_type", "sim", "posterior", "topk", "start_s", "end_s") if hasattr(h, k)})
    return out


def acoustics(x, sr):
    try:
        import parselmouth

        snd = parselmouth.Sound(x.astype(np.float64), sampling_frequency=sr)
        pitch = snd.to_pitch(pitch_floor=100, pitch_ceiling=600)
        f0 = pitch.selected_array["frequency"]
        f0 = f0[f0 > 0]
        formant = snd.to_formant_burg(max_number_of_formants=4, maximum_formant=5500)
        f1s, f2s = [], []
        for t in formant.xs():
            v1 = formant.get_value_at_time(1, t)
            v2 = formant.get_value_at_time(2, t)
            if v1 and v1 == v1:
                f1s.append(v1)
            if v2 and v2 == v2:
                f2s.append(v2)
        return {
            "f0_median": float(np.median(f0)) if len(f0) else None,
            "f1_median": float(np.median(f1s)) if f1s else None,
            "f2_median": float(np.median(f2s)) if f2s else None,
            "duration_s": len(x) / sr,
        }
    except Exception as e:
        return {"error": str(e), "duration_s": len(x) / sr}


def write_csv(p: Path, rows, fields=None):
    if not rows:
        p.write_text("empty\n", encoding="utf-8")
        return
    keys = fields or list(rows[0].keys())
    with open(p, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)


def main():
    human = list(csv.DictReader((P199 / "human_review_results.csv").open(encoding="utf-8-sig")))
    asr_by = {
        r["review_id"]: r
        for r in csv.DictReader((P199 / "asr_phone_conflicts.csv").open(encoding="utf-8-sig"))
    }
    cases = [r for r in human if r.get("diagnostic") == "SCORER_MISS"]
    assert len(cases) == 6, len(cases)

    hv = HybridVAD()
    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()
    tmp = RES / "_tmp.wav"

    case_rows = []
    forensic_rows = []
    review_items = []

    for i, c in enumerate(cases, start=1):
        cid = f"sm_{i:02d}"
        sid, target = c["speaker_id"], c["target"]
        src = find_source(sid, target)
        if src is None:
            print("MISSING", sid, target)
            continue
        x, sr = load_mono16(src)
        dur = len(x) / sr
        st = tgt.build(target)
        canonical = [str(p) for p in st.arpabet]
        sf.write(str(tmp), x, sr)
        r_sil = hv.run(str(tmp), mode="silero", silero_thr=0.5)
        segs = r_sil["segments"]

        variants = {}
        for pm in PADS_MS:
            if not segs:
                s0, s1 = 0.0, dur
            else:
                p = pm / 1000.0
                s0 = max(0.0, float(segs[0]["start"]) - p)
                s1 = min(dur, float(segs[-1]["end"]) + p)
            sf.write(str(tmp), x[int(s0 * sr) : int(s1 * sr)], sr)
            sm = pev.soft_match(str(tmp), st.arpabet)
            hits = ser_hits(sm.hits)
            n_miss = sum(1 for h in hits if h.get("match_type") != "exact")
            sims = [h.get("sim") for h in hits if h.get("sim") is not None]
            posts = [h.get("posterior") for h in hits if h.get("posterior") is not None]
            variants[f"pad_{pm}"] = {
                "score": float(sm.soft_score_0_100),
                "conf": float(sm.confidence_0_1),
                "n_miss": n_miss,
                "n_expected": len(hits),
                "observed": [str(h.get("best_obs")) for h in hits],
                "mismatch": [
                    f"{h.get('expected')}->{h.get('best_obs')}" for h in hits if h.get("match_type") != "exact"
                ],
                "mean_sim": float(np.mean(sims)) if sims else None,
                "mean_posterior": float(np.mean(posts)) if posts else None,
                "s0": round(s0, 4),
                "s1": round(s1, 4),
            }

        # full file
        sf.write(str(tmp), x, sr)
        sm_full = pev.soft_match(str(tmp), st.arpabet)
        fh = ser_hits(sm_full.hits)
        full = {
            "score": float(sm_full.soft_score_0_100),
            "conf": float(sm_full.confidence_0_1),
            "n_miss": sum(1 for h in fh if h.get("match_type") != "exact"),
            "observed": [str(h.get("best_obs")) for h in fh],
            "mean_sim": float(np.mean([h.get("sim") for h in fh if h.get("sim") is not None])),
            "mean_posterior": float(np.mean([h.get("posterior") for h in fh if h.get("posterior") is not None])),
        }

        ac = acoustics(x, sr)
        asr = asr_by.get(c["review_id"], {})
        pad_scores = {k: v["score"] for k, v in variants.items()}
        all_scores = list(pad_scores.values()) + [full["score"]]
        score_range = max(all_scores) - min(all_scores)
        min_score = min(all_scores)
        all_high = min_score >= 50

        # mechanism rules
        mismatch_any = any(v["n_miss"] > 0 for v in variants.values()) or full["n_miss"] > 0
        if all_high and full["n_miss"] == 0 and not mismatch_any:
            mechanism = "PHONE_MODEL_FALSE_POSITIVE"
            conf = "MEDIUM"
        elif all_high and mismatch_any:
            mechanism = "SOFT_MATCH_TOO_PERMISSIVE"
            conf = "MEDIUM"
        elif not all_high and score_range >= 20:
            mechanism = "WINDOW_DEPENDENT_SCORER_MISS"
            conf = "MEDIUM"
        elif not all_high:
            mechanism = "BOUNDARY_EFFECT"
            conf = "LOW"
        else:
            mechanism = "UNRESOLVED"
            conf = "LOW"

        case_rows.append(
            {
                "case_id": cid,
                "speaker_id": sid,
                "recording_id": c.get("recording_id"),
                "target": target,
                "score_full_1_9_9": c.get("full_score"),
                "confidence": None,
                "human_stage_a": c.get("human_pronunciation"),
                "human_stage_b": "",
                "canonical_phones": " ".join(canonical),
                "observed_phones": " ".join(full["observed"]),
                "asr_status": asr.get("asr_status", ""),
                "boundary_status": c.get("boundary_label", ""),
            }
        )
        forensic_rows.append(
            {
                "case_id": cid,
                "speaker_id": sid,
                "target": target,
                "human_stage_a": c.get("human_pronunciation"),
                "full_score": full["score"],
                "raw_score": variants["pad_0"]["score"],
                "pad100_score": variants["pad_100"]["score"],
                "pad200_score": variants["pad_200"]["score"],
                "pad250_score": variants["pad_250"]["score"],
                "pad300_score": variants["pad_300"]["score"],
                "pad500_score": variants["pad_500"]["score"],
                "score_range": score_range,
                "min_score": min_score,
                "all_variants_ge_50": all_high,
                "full_n_miss": full["n_miss"],
                "raw_n_miss": variants["pad_0"]["n_miss"],
                "full_mean_sim": full["mean_sim"],
                "full_mean_posterior": full["mean_posterior"],
                "observed_full": " ".join(full["observed"]),
                "canonical": " ".join(canonical),
                "asr_status": asr.get("asr_status", ""),
                "f0_median": ac.get("f0_median"),
                "f1_median": ac.get("f1_median"),
                "f2_median": ac.get("f2_median"),
                "duration_s": ac.get("duration_s"),
                "mechanism": mechanism,
                "mechanism_confidence": conf,
            }
        )
        # review clips (full + 2s listen)
        fullp = CLIPS / f"{cid}_FULL.wav"
        sf.write(str(fullp), x, sr)
        mid = dur / 2
        half = max(1.0, dur / 2)
        s = max(0.0, mid - half)
        e = min(dur, mid + half)
        listenp = CLIPS / f"{cid}_LISTEN.wav"
        sf.write(str(listenp), x[int(s * sr) : int(e * sr)], sr)
        review_items.append(
            {
                "case_id": cid,
                "speaker_id": sid,
                "target": target,
                "clips": {"LISTEN": f"clips/{cid}_LISTEN.wav", "FULL": f"clips/{cid}_FULL.wav"},
                "canonical_phones": " ".join(canonical),
                "observed_phones": " ".join(full["observed"]),
                "mismatch": ";".join(variants["pad_0"]["mismatch"]),
                "full_score": full["score"],
                "raw_score": variants["pad_0"]["score"],
                "score_range": score_range,
                "mechanism": mechanism,
                "mechanism_confidence": conf,
                "asr_status": asr.get("asr_status", ""),
                "f0_median": ac.get("f0_median"),
            }
        )
        print(cid, sid, target, "full", full["score"], "range", round(score_range, 1), "miss", full["n_miss"], "->", mechanism, flush=True)

    write_csv(RES / "scorer_miss_cases.csv", case_rows)
    write_csv(RES / "scorer_miss_forensics.csv", forensic_rows)
    write_csv(
        RES / "scorer_miss_human_review.csv",
        [
            {
                "case_id": r["case_id"],
                "speaker_id": r["speaker_id"],
                "target": r["target"],
                "stage_a_pronunciation": "",
                "stage_b_most_responsible": "",
                "notes": "",
                "reviewer_id": "",
                "review_date": "",
            }
            for r in review_items
        ],
    )

    mech_counts = Counter(r["mechanism"] for r in forensic_rows)
    summary = {
        "n": len(forensic_rows),
        "mechanisms": dict(mech_counts),
        "all_variants_ge50": sum(1 for r in forensic_rows if r["all_variants_ge_50"]),
        "score_range_ge20": sum(1 for r in forensic_rows if r["score_range"] >= 20),
        "note": "Mechanisms are measured hypotheses; human second review pack prepared, labels empty",
    }
    (RES / "scorer_miss_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")

    # review metadata + html
    (HR / "review_metadata.json").write_text(
        json.dumps(
            {
                "section": "scorer_miss",
                "n": len(review_items),
                "stage_a_schema": ["CLEAR_INCORRECT", "PROBABLY_INCORRECT", "AMBIGUOUS", "PROBABLY_CORRECT", "CLEAR_CORRECT"],
                "stage_b_schema": ["TRUE_SCORER_MISS", "PHONE_MODEL_FALSE_POSITIVE", "SOFT_MATCH_TOO_PERMISSIVE", "WINDOW_EFFECT", "HUMAN_UNCERTAINTY", "UNCERTAIN"],
                "items": review_items,
            },
            indent=2,
            ensure_ascii=True,
        ),
        encoding="utf-8",
    )
    build_html(review_items)
    print("SUMMARY", json.dumps(summary, ensure_ascii=True))
    try:
        tmp.unlink()
    except Exception:
        pass


def build_html(items):
    style = """<style>
*{box-sizing:border-box}
body{font-family:system-ui,-apple-system,sans-serif;margin:0 auto;max-width:760px;padding:12px 12px 96px;line-height:1.45;background:#f7f7f8}
.banner{background:#e7f1ff;border:1px solid #a9c9f5;border-radius:12px;padding:14px;margin-bottom:14px}
.card{background:#fff;border:1px solid #ddd;border-radius:12px;padding:14px;margin:0 0 16px}
.card h2{font-size:1.05rem;margin:0 0 6px}
.meta{font-size:.85rem;color:#444}
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
    P = ["CLEAR_INCORRECT", "PROBABLY_INCORRECT", "AMBIGUOUS", "PROBABLY_CORRECT", "CLEAR_CORRECT"]
    PL = {
        "CLEAR_INCORRECT": "Rõ — sai",
        "PROBABLY_INCORRECT": "Có lẽ sai",
        "AMBIGUOUS": "Không chắc",
        "PROBABLY_CORRECT": "Có lẽ đúng",
        "CLEAR_CORRECT": "Rõ — đúng",
    }
    B = ["TRUE_SCORER_MISS", "PHONE_MODEL_FALSE_POSITIVE", "SOFT_MATCH_TOO_PERMISSIVE", "WINDOW_EFFECT", "HUMAN_UNCERTAINTY", "UNCERTAIN"]
    BL = {
        "TRUE_SCORER_MISS": "Scorer bỏ sót lỗi thật",
        "PHONE_MODEL_FALSE_POSITIVE": "Phone model báo sai (dương tính giả)",
        "SOFT_MATCH_TOO_PERMISSIVE": "Soft-match quá dễ tính",
        "WINDOW_EFFECT": "Do cửa sổ cắt",
        "HUMAN_UNCERTAINTY": "Người nghe không chắc",
        "UNCERTAIN": "Không rõ",
    }

    def radios(name, values, labels):
        return '<div class="opts">' + "".join(
            f'<label><input type="radio" name="{name}" value="{v}"><span>{labels.get(v, v)}</span></label>'
            for v in values
        ) + "</div>"

    cards_a, cards_b = [], []
    for it in items:
        rid = it["case_id"]
        cards_a.append(
            f"""<section class="card"><h2>{rid} — {it['target'].upper()}</h2>
<p class="meta">speaker={it['speaker_id']}</p>
<div class="audio-block"><div class="lbl">LISTEN</div><audio controls preload="none" src="{it['clips']['LISTEN']}"></audio></div>
<div class="audio-block"><div class="lbl">FULL</div><audio controls preload="none" src="{it['clips']['FULL']}"></audio></div>
<fieldset><legend>Nghe và chấm phát âm (ẩn điểm)</legend>{radios('p_' + rid, P, PL)}</fieldset>
<input class="notes" name="n_{rid}" placeholder="ghi chú (tuỳ chọn)">
</section>"""
        )
        cards_b.append(
            f"""<section class="card"><h2>{rid} — {it['target'].upper()}</h2>
<audio controls preload="none" src="{it['clips']['FULL']}"></audio>
<pre>full_score={it['full_score']}  raw_score={it['raw_score']}  range={it['score_range']}
canonical={it['canonical_phones']}
observed ={it['observed_phones']}
mismatch ={it['mismatch']}
mechanism={it['mechanism']} ({it['mechanism_confidence']})
asr={it['asr_status']}  f0_median={it['f0_median']}</pre>
<fieldset><legend>Theo bạn, vì sao hệ thống chấm cao dù trẻ nói sai?</legend>{radios('s_' + rid, B, BL)}</fieldset>
</section>"""
        )
    ids = json.dumps([it["case_id"] for it in items])
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
  a.download='ScorerMiss_Stage'+stage+'_Filled.csv';a.click();
 }
}
"""
    blind = f"""<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, viewport-fit=cover">
<title>1.9.11 SCORER_MISS blind</title>{style}</head><body>
<div class="banner"><h1>SCORER_MISS — chấm mù (6 ca)</h1>
<p>Nghe trước, chấm phát âm. <b>Ẩn</b> điểm/phone/acoustics.</p>
<p><label>Reviewer: <input id="reviewer" value="human_mobile" size="16"></label></p></div>
{''.join(cards_a)}
<div class="sticky"><span class="count" id="prog">0/6</span><button id="exp">Nộp Stage A</button></div>
<script>
const ids={ids};
function v(p,id){{const e=document.querySelector('input[name="'+p+'_'+id+'"]:checked');return e?e.value:'';}}
function upd(){{let n=0;for(const id of ids)if(v('p',id))n++;document.getElementById('prog').textContent=n+'/'+ids.length;}}
document.body.addEventListener('change',upd);upd();
{submit_js}
document.getElementById('exp').onclick=async()=>{{
 let lines=['case_id,stage_a_pronunciation,notes'];
 for(const id of ids){{
  const notes=(document.querySelector('input[name="n_'+id+'"]')||{{value:''}}).value.replace(/,/g,';');
  lines.push([id,v('p',id),notes].join(','));
 }}
 await submitCsv(lines.join('\\n'),'A','exp');
}};
</script></body></html>"""
    (HR / "scorer_miss_blind.html").write_text(blind, encoding="utf-8")

    reveal = f"""<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>1.9.11 SCORER_MISS reveal</title>{style}</head><body>
<h1>SCORER_MISS — Stage B (evidence)</h1>
<p>Dùng sau khi Stage A đã chấm mù.</p>
<p><label>Reviewer: <input id="reviewer" value="human_mobile" size="16"></label></p>
{''.join(cards_b)}
<p><button id="expb" style="min-height:48px;font-size:1rem;font-weight:700;border:0;border-radius:10px;background:#0b5fff;color:#fff;padding:0 16px">Nộp Stage B</button></p>
<script>
const ids={ids};
function v(p,id){{const e=document.querySelector('input[name="'+p+'_'+id+'"]:checked');return e?e.value:'';}}
{submit_js}
document.getElementById('expb').onclick=async()=>{{
 let lines=['case_id,stage_b_most_responsible'];
 for(const id of ids)lines.push([id,v('s',id)].join(','));
 await submitCsv(lines.join('\\n'),'B','expb');
}};
</script></body></html>"""
    (HR / "scorer_miss_reveal.html").write_text(reveal, encoding="utf-8")


if __name__ == "__main__":
    main()
