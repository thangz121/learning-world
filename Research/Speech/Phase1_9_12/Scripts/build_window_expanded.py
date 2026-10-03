"""Phase 1.9.12 Objective B — stratified expanded window human review (blind A-E).

Design: 20 stratified tokens x 5 representative windows (FULL, RAW, PAD100, PAD250, PAD500)
= 100 clips; anonymous Clip A-E order randomized per token (seed documented).
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

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_9.HybridVAD.hybrid_vad import HybridVAD

OUT = REPO / "Research/Speech/Phase1_9_12"
RES = OUT / "Results"
HR = OUT / "HumanReview"
CLIPS = HR / "window_clips"
P1911 = REPO / "Research/Speech/Phase1_9_11/Results"
EXT = REPO / "Research/Speech/ExternalData/zenodo_200495/extracted/english_children"
DER = REPO / "Research/Speech/ExternalData/zenodo_200495/derived_16k"
for d in (OUT, RES, HR, CLIPS, OUT / "Scripts"):
    d.mkdir(parents=True, exist_ok=True)

WINDOWS = ["FULL", "RAW", "PAD100", "PAD250", "PAD500"]
PAD_MS = {"FULL": None, "RAW": 0, "PAD100": 100, "PAD250": 250, "PAD500": 500}
TARGET_TOKENS = 20


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


def main():
    win = list(csv.DictReader((P1911 / "window_ab_80_tokens.csv").open(encoding="utf-8-sig")))
    win = [r for r in win if "error" not in r]
    base = list(csv.DictReader((REPO / "Research/Speech/Phase1_9_8/Results/pronunciation_results.csv").open(encoding="utf-8-sig")))
    base = [r for r in base if r.get("soft_full") not in (None, "")]
    for i, r in enumerate(base):
        r["_tok"] = f"tok_{i+1:02d}"
    by_tok = {r["_tok"]: r for r in base}

    # stratify
    def band(s):
        s = float(s)
        return "HIGH" if s >= 80 else "MID" if s >= 50 else "LOW" if s >= 20 else "VERY_LOW"

    pools = defaultdict(list)
    for r in win:
        pools[band(r["full_score"])].append(r)
    selected = []
    seen = set()
    speaker_count = Counter()

    def pick(pool, n, tag):
        for r in sorted(pool, key=lambda x: float(x["score_range"]), reverse=True):
            if len(selected) >= TARGET_TOKENS or n <= 0:
                return
            if r["token_id"] in seen:
                continue
            if speaker_count[r["speaker_id"]] >= 3:
                continue
            seen.add(r["token_id"])
            speaker_count[r["speaker_id"]] += 1
            selected.append({**r, "stratum": tag})
            n -= 1

    for b in ("HIGH", "MID", "LOW", "VERY_LOW"):
        pick(pools.get(b, []), 3, f"{b}_SCORE")
    pick([r for r in win if float(r["score_range"]) >= 20], 4, "WINDOW_SENSITIVE")
    # final-consonant cases: words with consonant finals
    finals = {"one", "four", "five", "six", "seven", "eight", "nine", "ten"}
    pick([r for r in win if r["target"] in finals], 4, "FINAL_CONSONANT_CASE")
    # fill remaining up to target with stable window tokens
    pick([r for r in win if float(r["score_range"]) <= 10], TARGET_TOKENS - len(selected), "WINDOW_STABLE")

    hv = HybridVAD()
    items = []
    mapping_rows = []
    for r in selected:
        tid = r["token_id"]
        b = by_tok.get(tid)
        if b is None:
            continue
        sid, target = b["speaker_id"], b["target"]
        src = find_source(sid, target)
        if src is None:
            continue
        x, sr = load_mono16(src)
        dur = len(x) / sr
        tmp = RES / "_tmp.wav"
        sf.write(str(tmp), x, sr)
        rs = hv.run(str(tmp), mode="silero", silero_thr=0.5)
        segs = rs["segments"]

        clips = {}
        for w in WINDOWS:
            pm = PAD_MS[w]
            if w == "FULL":
                s0, s1 = 0.0, dur
            elif not segs:
                s0, s1 = 0.0, dur
            else:
                p = (pm or 0) / 1000.0
                s0 = max(0.0, float(segs[0]["start"]) - p)
                s1 = min(dur, float(segs[-1]["end"]) + p)
            path = CLIPS / f"{tid}_{w}.wav"
            sf.write(str(path), x[int(s0 * sr) : int(s1 * sr)], sr)
            clips[w] = f"window_clips/{tid}_{w}.wav"

        # randomized anonymous order (stable seed from token id)
        seed = sum(ord(c) for c in tid)
        order = WINDOWS[:]
        random.Random(seed).shuffle(order)
        letters = {chr(ord("A") + i): w for i, w in enumerate(order)}
        scores = {
            "FULL": r["full_score"],
            "RAW": r["raw_vad_score"],
            "PAD100": r["pad100_score"],
            "PAD250": r["pad250_score"],
            "PAD500": r["pad500_score"],
        }
        mapping_rows.append(
            {
                "token_id": tid,
                "speaker_id": sid,
                "word": target,
                "stratum": r["stratum"],
                "blind_order": ";".join(f"{k}={v}" for k, v in letters.items()),
                "seed": seed,
                "score_range": r["score_range"],
                "full_score": scores["FULL"],
                "raw_score": scores["RAW"],
                "pad100_score": scores["PAD100"],
                "pad250_score": scores["PAD250"],
                "pad500_score": scores["PAD500"],
            }
        )
        items.append(
            {
                "token_id": tid,
                "speaker_id": sid,
                "word": target,
                "stratum": r["stratum"],
                "clips": {k: clips[v] for k, v in letters.items()},
                "letters": letters,
                "scores": scores,
            }
        )
        print(tid, sid, target, r["stratum"], "range", r["score_range"], flush=True)
        try:
            tmp.unlink()
        except Exception:
            pass

    write_csv(RES / "window_human_expanded.csv",
              [
                  {
                      "token_id": it["token_id"],
                      "speaker_id": it["speaker_id"],
                      "word": it["word"],
                      "reviewer_id": "",
                      "blind_clip_id": "",
                      "window_policy": "",
                      "pronunciation_clarity": "",
                      "boundary_quality": "",
                      "target_recognizability": "",
                      "reviewer_confidence": "",
                      "human_final_judgment": "",
                      "score": "",
                      "confidence": "",
                  }
                  for it in items
              ])
    write_csv(RES / "window_reviewer_agreement.csv",
              [{"reviewers": 1, "metric": "N/A", "note": "single reviewer; inter-rater agreement cannot be computed"}])
    write_csv(RES / "window_consensus.csv",
              [
                  {
                      "token_id": it["token_id"],
                      "consensus_clarity": "",
                      "consensus_boundary": "",
                      "consensus_recognizability": "",
                      "note": "pending submission",
                  }
                  for it in items
              ])
    meta = json.loads((HR / "review_metadata.json").read_text(encoding="utf-8"))
    meta["window_expanded"] = {
        "n_tokens": len(items),
        "n_clips": len(items) * len(WINDOWS),
        "windows": WINDOWS,
        "blind": True,
        "note": "single reviewer; 5 representative windows (7-window numeric A/B retained in Phase 1.9.11)",
        "items": [
            {
                "token_id": it["token_id"],
                "speaker_id": it["speaker_id"],
                "word": it["word"],
                "stratum": it["stratum"],
                "clips": it["clips"],
                "letters": it["letters"],
                "scores": it["scores"],
            }
            for it in items
        ],
    }
    (HR / "review_metadata.json").write_text(json.dumps(meta, indent=2, ensure_ascii=True), encoding="utf-8")
    build_window_html(items, letters_hidden=True)
    build_window_html(items, letters_hidden=False)
    print("DONE", len(items), "tokens", len(items) * len(WINDOWS), "clips")


def build_window_html(items, letters_hidden):
    style = """<style>
*{box-sizing:border-box}
body{font-family:system-ui,-apple-system,sans-serif;margin:0 auto;max-width:780px;padding:12px 12px 96px;line-height:1.45;background:#f7f7f8}
.banner{background:#e7f1ff;border:1px solid #a9c9f5;border-radius:12px;padding:14px;margin-bottom:14px}
.card{background:#fff;border:1px solid #ddd;border-radius:12px;padding:14px;margin:0 0 16px}
.card h2{font-size:1.05rem;margin:0 0 6px}
.clip{border:1px solid #eee;border-radius:10px;padding:10px;margin:8px 0;background:#fcfcfc}
.clip .lbl{font-weight:700;margin-bottom:4px}
audio{width:100%}
.q{display:flex;flex-direction:column;gap:6px;margin:6px 0}
.q label{font-size:.82rem;color:#333;font-weight:600}
select{width:100%;min-height:40px;font-size:15px;padding:6px;border:1px solid #ccc;border-radius:8px;background:#fff}
.sticky{position:fixed;left:0;right:0;bottom:0;background:#111;color:#fff;padding:12px 14px calc(12px + env(safe-area-inset-bottom));display:flex;gap:10px;z-index:50}
.sticky button{flex:1;min-height:48px;font-size:1rem;font-weight:700;border:0;border-radius:10px;background:#0b5fff;color:#fff}
.sticky .count{align-self:center;font-size:.85rem;white-space:nowrap}
pre{white-space:pre-wrap;font-size:.78rem;background:#eee;padding:8px;border-radius:8px}
</style>"""
    CLAR = ["", "CLEAR", "MOSTLY_CLEAR", "AMBIGUOUS", "POOR"]
    CLARL = {"": "--", "CLEAR": "Rõ", "MOSTLY_CLEAR": "Phần lớn rõ", "AMBIGUOUS": "Không chắc", "POOR": "Kém"}
    BND = ["", "CLEAN", "SLIGHTLY_CUT", "STRONGLY_CUT", "EXCESS_CONTEXT", "UNCLEAR"]
    BNDL = {"": "--", "CLEAN": "Sạch", "SLIGHTLY_CUT": "Hơi cụt", "STRONGLY_CUT": "Cụt nhiều", "EXCESS_CONTEXT": "Dư ngữ cảnh", "UNCLEAR": "Không rõ"}
    REC = ["", "CLEAR", "PROBABLE", "AMBIGUOUS", "NOT_RECOGNIZABLE"]
    RECL = {"": "--", "CLEAR": "Rõ từ", "PROBABLE": "Có lẽ đúng từ", "AMBIGUOUS": "Không chắc", "NOT_RECOGNIZABLE": "Không nhận ra"}
    CNF = ["", "HIGH", "MEDIUM", "LOW"]
    CNFL = {"": "--", "HIGH": "Chắc cao", "MEDIUM": "Chắc vừa", "LOW": "Chắc thấp"}

    def sel(name, values, labels):
        opts = "".join(f'<option value="{v}">{labels[v]}</option>' for v in values)
        return f'<select name="{name}">{opts}</select>'

    cards = []
    for it in items:
        tid = it["token_id"]
        clip_blocks = []
        for letter in sorted(it["clips"].keys()):
            clip_blocks.append(
                f"""<div class="clip"><div class="lbl">Clip {letter}</div>
<audio controls preload="none" src="{it['clips'][letter]}"></audio>
<div class="q"><label>Độ rõ phát âm</label>{sel(f'cl_{tid}_{letter}', CLAR, CLARL)}</div>
<div class="q"><label>Chất lượng biên</label>{sel(f'bd_{tid}_{letter}', BND, BNDL)}</div>
<div class="q"><label>Nhận ra từ mục tiêu</label>{sel(f'rc_{tid}_{letter}', REC, RECL)}</div>
<div class="q"><label>Độ chắc</label>{sel(f'cf_{tid}_{letter}', CNF, CNFL)}</div>
</div>"""
            )
        reveal = ""
        if not letters_hidden:
            rev = "; ".join(f"{k}={v}" for k, v in sorted(it["letters"].items()))
            reveal = f"<pre>mapping: {rev}\nscores: {it['scores']}</pre>"
        cards.append(
            f"""<section class="card"><h2>{tid} — {it['word'].upper()}</h2>
<p class="meta">speaker={it['speaker_id']} · stratum={it['stratum']}</p>
{''.join(clip_blocks)}{reveal}</section>"""
        )
    ids = json.dumps([it["token_id"] for it in items])
    letters_json = json.dumps({it["token_id"]: sorted(it["clips"].keys()) for it in items})
    stage = "A" if letters_hidden else "B"
    btn = "Nộp Stage A" if letters_hidden else "Nộp Stage B"
    title = "window blind" if letters_hidden else "window reveal"
    body = f"""<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, viewport-fit=cover">
<title>1.9.12 {title}</title>{style}</head><body>
<div class="banner"><h1>Window review — {'chấm mù (A-E ẩn)' if letters_hidden else 'reveal mapping'}</h1>
<p>{'Nghe từng clip và chấm. Không hiện mapping cho đến Stage B.' if letters_hidden else 'Dùng sau khi đã nộp Stage A.'}</p>
<p><label>Reviewer: <input id="reviewer" value="human_mobile" size="16"></label></p></div>
{''.join(cards)}
<div class="sticky"><span class="count" id="prog">0/{len(items)}</span><button id="exp">{btn}</button></div>
<script>
const ids={ids};
const letters={letters_json};
function gv(n){{const e=document.querySelector('[name="'+n+'"]');return e?e.value:'';}}
function upd(){{let n=0;for(const id of ids){{if(gv('cl_'+id+'_A'))n++;}}document.getElementById('prog').textContent=n+'/'+ids.length;}}
document.body.addEventListener('change',upd);upd();
async function submitCsv(t,stage,btnId){{
 const rid=(document.getElementById('reviewer')||{{value:'human_mobile'}}).value||'human_mobile';
 const btn=document.getElementById(btnId);
 try{{
  const r=await fetch('/submit',{{method:'POST',headers:{{'Content-Type':'application/json'}},
   body:JSON.stringify({{pack:'window_expanded',stage:stage,csv:t,reviewer_id:rid,timestamp:new Date().toISOString()}})}});
  if(r.ok){{const j=await r.json();btn.textContent='Đã nộp ✓ ('+j.rows+' dòng)';btn.style.background='#1a7f37';return;}}
  throw new Error('http '+r.status);
 }}catch(e){{
  btn.textContent='Không gửi được — tải CSV';
  const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([t],{{type:'text/csv'}}));
  a.download='WindowExpanded_Stage'+stage+'_Filled.csv';a.click();
 }}
}}
document.getElementById('exp').onclick=async()=>{{
 let lines=['token_id,blind_clip_id,pronunciation_clarity,boundary_quality,target_recognizability,reviewer_confidence'];
 for(const id of ids){{
  for(const L of letters[id]){{
   lines.push([id,L,gv('cl_'+id+'_'+L),gv('bd_'+id+'_'+L),gv('rc_'+id+'_'+L),gv('cf_'+id+'_'+L)].join(','));
  }}
 }}
 await submitCsv(lines.join('\\n'),'{stage}','exp');
}};
</script></body></html>"""
    name = "window_blind.html" if letters_hidden else "window_reveal.html"
    (HR / name).write_text(body, encoding="utf-8")


if __name__ == "__main__":
    main()
