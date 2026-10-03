"""Phase 1.9.15 — P1 build NEW blind fidelity/assessability review pack (100-110 items).

Stratified selection from the real-child corpus (Zenodo 200495), excluding
previously human-reviewed (speaker,target) pairs so the new labels are an
independent validation set. Model scores are computed for stratification and
stored in review_metadata.json but NEVER rendered in the blind HTML.

Strata: near-silence, very short, soft, loud, noisy/degraded, ASR-empty,
ASR-wrong, phone-low, ordinary high/mid, multiword sentence, free speech,
partial/unclear.

Outputs: HumanReview/fidelity_blind.html, HumanReview/review_metadata.json,
HumanReview/clips/*.wav, artifacts/fidelity/selection_table.csv
"""
from __future__ import annotations

import csv
import hashlib
import json
import random
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np
import soundfile as sf

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402

OUT = REPO / "Research/Speech/Phase1_9_15"
HR = OUT / "HumanReview"
CLIPS = HR / "clips"
P198 = REPO / "Research/Speech/Phase1_9_8/Results"
P199 = REPO / "Research/Speech/Phase1_9_9/Results"
P1910 = REPO / "Research/Speech/Phase1_9_10/Results"
P1911 = REPO / "Research/Speech/Phase1_9_11/Results"
P1912 = REPO / "Research/Speech/Phase1_9_12/Results"
ZEN = REPO / "Research/Speech/ExternalData/zenodo_200495"
EXTRACT = ZEN / "extracted/english_children"

QUOTAS = {
    "NO_SPEECH": 4, "VERY_SHORT": 8, "NEAR_SILENCE": 4, "SOFT": 8, "LOUD": 8,
    "NOISY": 12, "ASR_EMPTY": 14, "ASR_WRONG": 10, "PHONE_LOW": 8,
    "ORDINARY_HIGH": 15, "ORDINARY_MID": 6, "SENTENCE": 10, "FREE_SPEECH": 10,
}

DER = ZEN / "derived_16k"


def to16k(path: Path) -> Path:
    """Resample to a cached 16k mono WAV (shared derived_16k convention)."""
    key = hashlib.sha256(str(path).encode("utf-8")).hexdigest()[:16] + "_" + re.sub(
        r"[^A-Za-z0-9_.-]+", "_", path.name)[:50]
    cache = DER / f"{key}.wav"
    if cache.exists():
        return cache
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
    DER.mkdir(parents=True, exist_ok=True)
    sf.write(str(cache), x, sr)
    return cache


def sha256_file(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def find_source(speaker_id: str, filename: str, category: str):
    """Locate the recording by scanning the extracted tree for the filename."""
    m = re.match(r"child_(\d+)", speaker_id or "")
    if not m:
        return None
    nn = m.group(1)
    base = EXTRACT / ("english_words_sentences" if "english_words_sentences" in filename or category == "TARGETED_CHILD_SPEECH" else "english_free_speech")
    for sp in EXTRACT.iterdir():
        if sp.name.startswith(nn + "_"):
            hits = list(sp.rglob(Path(filename).name))
            if hits:
                return hits[0]
    return None


def read_csv(p: Path):
    return list(csv.DictReader(p.open(encoding="utf-8-sig")))


def reviewed_pairs():
    pairs = set()
    for r in read_csv(P199 / "human_review_results.csv"):
        pairs.add((r["speaker_id"], r["target"]))
    for r in read_csv(P1910 / "human_second_pass.csv"):
        pairs.add((r["speaker_id"], r["target"]))
    for r in read_csv(P1911 / "scorer_miss_human_review.csv"):
        pairs.add((r["speaker_id"], r["target"]))
    for r in read_csv(P1911 / "window_human_spot_check.csv"):
        pairs.add((r["speaker_id"], r["target"]))
    for r in read_csv(P1912 / "final_consonant_human_review.csv"):
        pairs.add((r["speaker_id"], r["word"]))
    return pairs


def write_clip(src: Path, dst: Path, max_s: float = 10.0) -> float:
    x, sr = sf.read(str(src))
    if x.ndim > 1:
        x = x.mean(axis=1)
    x = x.astype(np.float32)
    if sr != 16000:
        n = int(len(x) * 16000 / sr)
        t0 = np.linspace(0, 1, len(x), endpoint=False)
        t1 = np.linspace(0, 1, n, endpoint=False)
        x = np.interp(t1, t0, x).astype(np.float32)
        sr = 16000
    if len(x) > int(max_s * sr):
        x = x[: int(max_s * sr)]
    sf.write(str(dst), x, sr)
    return len(x) / sr


def main():
    HR.mkdir(parents=True, exist_ok=True)
    CLIPS.mkdir(parents=True, exist_ok=True)
    for old in CLIPS.glob("*.wav"):
        old.unlink()
    pr = reviewed_pairs()

    inv_rows = read_csv(P198 / "recording_inventory.csv")
    vad = {r["recording_id"]: r for r in read_csv(P198 / "vad_results.csv") if r["mode"] == "silero"}
    scored = {r["recording_id"]: r for r in read_csv(P198 / "pronunciation_results.csv") if r.get("soft_full")}
    pool = []
    for r in inv_rows:
        if r["category"] not in ("TARGETED_CHILD_SPEECH", "TRANSCRIBED_SPONTANEOUS_SPEECH"):
            continue
        key = (r["speaker_id"], r["target"])
        if key in pr:
            continue
        v = vad.get(r["recording_id"], {})
        pool.append({
            "recording_id": r["recording_id"], "speaker_id": r["speaker_id"],
            "target": r["target"], "task": r["task"], "mic": r["mic"],
            "category": r["category"], "duration": float(r["duration"] or 0),
            "path": r.get("path", ""),
            "silero_n": int(float(v.get("n_segments") or 0)) if v else None,
            "speech_ratio": float(v.get("speech_ratio") or 0) if v else None,
            "rms_mean": float(v.get("rms_mean") or 0) if v else None,
            "zcr_proxy": float(v.get("zcr_proxy") or 0) if v else None,
            "fragmentation": float(v.get("fragmentation") or 0) if v else None,
        })
    print(f"pool after excluding reviewed (speaker,target): {len(pool)}", flush=True)

    # score unscored number recordings + ASR on numbers/sentences
    try:
        from Research.Speech.Phase1_2.Adapters.asr_moonshine import MoonshineAsrAdapter
        asr = MoonshineAsrAdapter()
        asr_ok = True
    except Exception as exc:  # noqa: BLE001
        print("ASR unavailable:", exc, flush=True)
        asr, asr_ok = None, False
    pev = PhoneEvidenceV2()
    tgt = CmuDictTargetAdapter()

    for it in pool:
        src = Path(it["path"]) if it.get("path") and Path(it["path"]).exists() else None
        it["src"] = str(src) if src else ""
        it["soft_full"] = it["confidence"] = it["asr_status"] = ""
        it["asr_text"] = ""
        if not src:
            continue
        src16 = None
        if src:
            try:
                src16 = to16k(src)
            except Exception as exc:  # noqa: BLE001
                it["soft_full"] = f"ERR16:{type(exc).__name__}"
        if it["task"] == "number_counting" and it["recording_id"] not in scored and src16:
            try:
                sm = pev.soft_match(str(src16), tgt.build(it["target"]).arpabet)
                it["soft_full"] = sm.soft_score_0_100
                it["confidence"] = sm.confidence_0_1
            except Exception as exc:  # noqa: BLE001
                it["soft_full"] = f"ERR:{type(exc).__name__}"
        elif it["recording_id"] in scored:
            it["soft_full"] = float(scored[it["recording_id"]]["soft_full"])
            it["confidence"] = float(scored[it["recording_id"]]["conf_full"])
        if asr_ok and src16 and it["task"] in ("number_counting", "predefined_sentence") and it["duration"] < 15:
            try:
                text = (asr.run(str(src16)).text or "").strip()
                toks = text.lower().replace(".", "").replace(",", "").split()
                expected = set(it["target"].lower().split())
                if not text:
                    it["asr_status"] = "ASR_EMPTY"
                elif expected and expected & set(toks):
                    it["asr_status"] = "ASR_CORRECT"
                else:
                    it["asr_status"] = "ASR_WRONG"
                it["asr_text"] = text
            except Exception as exc:  # noqa: BLE001
                it["asr_status"] = f"ASR_ERROR:{type(exc).__name__}"
    with open(OUT / "artifacts" / "fidelity" / "candidate_pool.csv", "w", newline="", encoding="utf-8") as f:
        pk = ["recording_id", "speaker_id", "target", "task", "mic", "category", "duration",
              "silero_n", "speech_ratio", "rms_mean", "zcr_proxy", "fragmentation",
              "soft_full", "confidence", "asr_status", "asr_text", "path"]
        w = csv.DictWriter(f, fieldnames=pk, extrasaction="ignore")
        w.writeheader()
        w.writerows(pool)
    print("scoring + ASR done", flush=True)

    def fnum(v):
        try:
            return float(v)
        except (TypeError, ValueError):
            return None

    rms_vals = [it["rms_mean"] for it in pool if it["rms_mean"]]
    p25, p75 = (np.percentile(rms_vals, 25), np.percentile(rms_vals, 75)) if rms_vals else (0, 0)
    zcr_vals = [it["zcr_proxy"] for it in pool if it["zcr_proxy"]]
    z75 = np.percentile(zcr_vals, 75) if zcr_vals else 0

    def strata(it):
        tags = []
        soft = fnum(it["soft_full"])
        if it["silero_n"] == 0:
            tags.append("NO_SPEECH")
        if it["duration"] and it["duration"] < 0.5:
            tags.append("VERY_SHORT")
        if it["category"] == "TRANSCRIBED_SPONTANEOUS_SPEECH":
            tags.append("FREE_SPEECH")
        if (it["speech_ratio"] is not None and 0 < it["speech_ratio"] < 0.55
                and it["category"] == "TRANSCRIBED_SPONTANEOUS_SPEECH"):
            tags.append("NEAR_SILENCE")
        if it["task"] == "predefined_sentence":
            tags.append("SENTENCE")
        if soft is not None:
            if soft < 50:
                tags.append("PHONE_LOW")
            elif soft >= 70:
                tags.append("ORDINARY_HIGH")
            else:
                tags.append("ORDINARY_MID")
        if it["asr_status"] == "ASR_EMPTY":
            tags.append("ASR_EMPTY")
        if it["asr_status"] == "ASR_WRONG":
            tags.append("ASR_WRONG")
        if it["rms_mean"] and it["rms_mean"] < p25:
            tags.append("SOFT")
        if it["rms_mean"] and it["rms_mean"] > p75:
            tags.append("LOUD")
        degraded = ((it["zcr_proxy"] or 0) > z75 or (it["fragmentation"] or 0) > 3
                    or it["mic"] in ("port_mic", "nao_mic") and it["mic"] not in ("studio_mic",))
        if degraded and it["mic"] != "studio_mic":
            tags.append("NOISY")
        return tags

    by_stratum = defaultdict(list)
    for it in pool:
        it["tags"] = strata(it)
        for t in it["tags"]:
            by_stratum[t].append(it)
    for t, vals in sorted(by_stratum.items()):
        print(f"  {t}: {len(vals)}")

    rng = random.Random(1515)
    selected, seen = [], set()
    for stratum, quota in QUOTAS.items():
        pool_s = [it for it in by_stratum.get(stratum, []) if it["recording_id"] not in seen]
        rng.shuffle(pool_s)
        # prefer items whose (speaker,target) is not otherwise selected
        for it in pool_s:
            if len([s for s in selected if s["selected_stratum"] == stratum]) >= quota:
                break
            if it["recording_id"] in seen:
                continue
            seen.add(it["recording_id"])
            it["selected_stratum"] = stratum
            selected.append(it)
    print(f"selected {len(selected)} items", flush=True)

    items = []
    for i, it in enumerate(selected, 1):
        case_id = f"fi_{i:03d}"
        src = Path(it["src"])
        dst = CLIPS / f"{case_id}.wav"
        dur = write_clip(src, dst)
        items.append({
            "case_id": case_id, "recording_id": it["recording_id"],
            "speaker_id": it["speaker_id"], "target": it["target"], "task": it["task"],
            "mic": it["mic"], "category": it["category"], "selected_stratum": it["selected_stratum"],
            "tags": it["tags"], "clip": f"clips/{case_id}.wav", "clip_duration_s": round(dur, 3),
            "source_duration_s": it["duration"], "silero_n": it["silero_n"],
            "speech_ratio": it["speech_ratio"], "rms_mean": it["rms_mean"],
            "zcr_proxy": it["zcr_proxy"], "soft_full": it["soft_full"],
            "confidence": it["confidence"], "asr_status": it["asr_status"], "asr_text": it["asr_text"],
        })
    (HR / "review_metadata.json").write_text(json.dumps({
        "phase": "1.9.15", "pack": "fidelity_v2", "n": len(items),
        "selection_note": "excluded previously reviewed (speaker,target) pairs; "
                          "scores/ASR stored here for stratification only, never shown in blind HTML",
        "items": items}, indent=2, ensure_ascii=False), encoding="utf-8")

    with open(OUT / "artifacts" / "fidelity" / "selection_table.csv", "w", newline="", encoding="utf-8") as f:
        fields = list(items[0].keys())
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(items)

    build_html(items)
    print(f"metadata + blind HTML written; strata counts: "
          f"{dict(Counter(it['selected_stratum'] for it in items))}")


def build_html(items):
    style = """<style>
*{box-sizing:border-box}body{font-family:system-ui,-apple-system,sans-serif;margin:0 auto;max-width:780px;padding:12px 12px 110px;line-height:1.45;background:#f7f7f8}
.banner{background:#e7f1ff;border:1px solid #a9c9f5;border-radius:12px;padding:14px;margin-bottom:14px}
.card{background:#fff;border:1px solid #ddd;border-radius:12px;padding:14px;margin:0 0 16px}
.card h2{font-size:1rem;margin:0 0 4px}.meta{font-size:.85rem;color:#444;margin:4px 0 10px}
audio{width:100%}fieldset{border:1px solid #ccc;border-radius:10px;margin:10px 0 0;padding:10px;background:#fafafa}
legend{font-weight:700;font-size:.9rem;padding:0 6px}
.opts{display:flex;flex-direction:column;gap:6px;margin-top:6px}
.opts label{display:flex;align-items:center;gap:10px;min-height:40px;padding:8px 10px;border:1px solid #ccc;border-radius:10px;background:#fff;font-size:.92rem}
.opts input{width:19px;height:19px;flex-shrink:0}.opts label:has(input:checked){border-color:#0b5fff;background:#e8f0ff}
.notes{width:100%;min-height:40px;font-size:15px;padding:8px;border:1px solid #ccc;border-radius:10px;margin-top:8px}
.sticky{position:fixed;left:0;right:0;bottom:0;background:#111;color:#fff;padding:12px 14px calc(12px + env(safe-area-inset-bottom));display:flex;gap:10px;z-index:50}
.sticky button{flex:1;min-height:48px;font-size:1rem;font-weight:700;border:0;border-radius:10px;background:#0b5fff;color:#fff}
.sticky .count{align-self:center;font-size:.85rem;white-space:nowrap}
</style>"""

    def radios(name, values):
        return '<div class="opts">' + "".join(
            f'<label><input type="radio" name="{name}" value="{v[0]}"><span>{v[1]}</span></label>'
            for v in values) + "</div>"

    content_opts = [("NO_SPEECH", "Không có tiếng nói / im lặng"), ("SPEECH_PRESENT", "Có tiếng nói của trẻ"),
                    ("UNCERTAIN", "Không chắc")]
    attempt_opts = [("VALID_ATTEMPT", "Trẻ đã nói từ/câu được yêu cầu (đúng mục tiêu)"),
                    ("POSSIBLE_ATTEMPT", "Có thể là lượt nói nhưng không rõ có đúng mục tiêu không"),
                    ("INCOMPLETE", "Nói thiếu / bỏ dở"),
                    ("FREE_SPEAK_WRONG_TARGET", "Nói từ/câu khác hoặc nói tự do"),
                    ("UNINTELLIGIBLE", "Không thể hiểu được lời nói"),
                    ("UNCERTAIN", "Không chắc")]
    assess_opts = [("ASSESSABLE", "Có thể chấm phát âm từ bản ghi này"),
                   ("NOT_ASSESSABLE", "KHÔNG thể chấm phát âm từ bản ghi này"),
                   ("UNCERTAIN", "Không chắc")]
    conf_opts = [("HIGH", "Chắc cao"), ("MEDIUM", "Chắc vừa"), ("LOW", "Chắc thấp")]

    cards = []
    for it in items:
        task_line = {
            "number_counting": f"nhiệm vụ: nói số <b>{it['target']}</b>",
            "predefined_sentence": f"nhiệm vụ: nói câu <b>{it['target']}</b>",
            "spontaneous_retell_segment": "nhiệm vụ: kể lại chuyện tự do (không có từ mục tiêu cố định)",
            "spontaneous_retell_full": "nhiệm vụ: kể lại chuyện tự do (không có từ mục tiêu cố định)",
        }.get(it["task"], f"nhiệm vụ: {it['task']} · mục tiêu {it['target']}")
        cards.append(f"""<section class="card"><h2>{it['case_id']}</h2>
<p class="meta">{task_line}</p>
<audio controls preload="none" src="{it['clip']}"></audio>
<fieldset><legend>1. Trong bản ghi có tiếng nói của trẻ không?</legend>{radios('c_' + it['case_id'], content_opts)}</fieldset>
<fieldset><legend>2. Trạng thái lượt nói (nếu có tiếng nói)</legend>{radios('a_' + it['case_id'], attempt_opts)}</fieldset>
<fieldset><legend>3. Bản ghi này có đủ để CHẤM PHÁT ÂM không?</legend>{radios('s_' + it['case_id'], assess_opts)}</fieldset>
<fieldset><legend>4. Độ chắc của bạn</legend>{radios('f_' + it['case_id'], conf_opts)}</fieldset>
<input class="notes" name="n_{it['case_id']}" placeholder="ghi chú (tuỳ chọn)">
</section>""")

    ids = json.dumps([it["case_id"] for it in items])
    html = f"""<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, viewport-fit=cover">
<title>1.9.15 fidelity blind review</title>{style}</head><body>
<div class="banner"><h1>Đánh giá Fidelity / Assessability — mù</h1>
<p>Chỉ nghe bản ghi. <b>Không</b> có điểm/score của máy trong trang này.</p>
<p><label>Reviewer: <input id="reviewer" value="human_mobile" size="16"></label></p></div>
{''.join(cards)}
<div class="sticky"><span class="count" id="prog">0/{len(items)}</span><button id="exp">Nộp</button></div>
<script>
const ids={ids};
function v(p,id){{const e=document.querySelector('input[name="'+p+'_'+id+'"]:checked');return e?e.value:'';}}
function upd(){{let n=0;for(const id of ids)if(v('c',id))n++;document.getElementById('prog').textContent=n+'/'+ids.length;}}
document.body.addEventListener('change',upd);upd();
async function submitCsv(t){{
 const rid=(document.getElementById('reviewer')||{{value:'human_mobile'}}).value||'human_mobile';
 const btn=document.getElementById('exp');
 try{{
  const r=await fetch('/submit',{{method:'POST',headers:{{'Content-Type':'application/json'}},
   body:JSON.stringify({{pack:'fidelity_v2',stage:'A',csv:t,reviewer_id:rid,timestamp:new Date().toISOString()}})}});
  if(r.ok){{const j=await r.json();btn.textContent='Đã nộp ✓ ('+j.rows+' dòng)';btn.style.background='#1a7f37';return;}}
  throw new Error('http '+r.status);
 }}catch(e){{
  btn.textContent='Không gửi được — tải CSV';
  const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([t],{{type:'text/csv'}}));
  a.download='FidelityV2_StageA_Filled.csv';a.click();
 }}
}}
document.getElementById('exp').onclick=async()=>{{
 let lines=['case_id,content,attempt,assessability,confidence,notes'];
 for(const id of ids){{
  const notes=(document.querySelector('input[name="n_'+id+'"]')||{{value:''}}).value.replace(/,/g,';');
  lines.push([id,v('c',id),v('a',id),v('s',id),v('f',id),notes].join(','));
 }}
 await submitCsv(lines.join('\\n'));
}};
</script></body></html>"""
    (HR / "fidelity_blind.html").write_text(html, encoding="utf-8")


if __name__ == "__main__":
    main()
