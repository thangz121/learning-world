"""Build a blind page for the unrated window clips (resume support)."""
from __future__ import annotations

import csv
import json
from pathlib import Path

PHASE = Path(__file__).resolve().parents[1]
RES = PHASE / "Results"
HR = PHASE / "HumanReview"


def main():
    meta = json.loads((HR / "review_metadata.json").read_text(encoding="utf-8"))
    items = {it["token_id"]: it for it in meta["window_expanded"]["items"]}
    rows = list(csv.DictReader((RES / "window_expanded_StageA_Filled.csv").open(encoding="utf-8-sig")))
    rated = {(r["token_id"], r["blind_clip_id"]) for r in rows if r["pronunciation_clarity"]}
    missing = []
    for tid, it in items.items():
        for letter in sorted(it["clips"].keys()):
            if (tid, letter) not in rated:
                missing.append({"token_id": tid, "letter": letter, "clip": it["clips"][letter], "word": it["word"], "speaker_id": it["speaker_id"], "stratum": it["stratum"]})
    (HR / "review_metadata_remaining.json").write_text(
        json.dumps({"n": len(missing), "items": missing}, indent=2, ensure_ascii=True), encoding="utf-8"
    )
    build(missing)
    print("remaining", len(missing))


def build(missing):
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
        return f'<select name="{name}">' + "".join(f'<option value="{v}">{labels[v]}</option>' for v in values) + "</select>"

    cards = []
    for m in missing:
        key = f"{m['token_id']}_{m['letter']}"
        cards.append(
            f"""<section class="card"><h2>{m['token_id']} — Clip {m['letter']}</h2>
<p class="meta">word={m['word'].upper()} · speaker={m['speaker_id']} · stratum={m['stratum']}</p>
<audio controls preload="none" src="{m['clip']}"></audio>
<div class="q"><label>Độ rõ phát âm</label>{sel('cl_'+key, CLAR, CLARL)}</div>
<div class="q"><label>Chất lượng biên</label>{sel('bd_'+key, BND, BNDL)}</div>
<div class="q"><label>Nhận ra từ mục tiêu</label>{sel('rc_'+key, REC, RECL)}</div>
<div class="q"><label>Độ chắc</label>{sel('cf_'+key, CNF, CNFL)}</div>
</section>"""
        )
    keys = json.dumps([f"{m['token_id']}_{m['letter']}" for m in missing])
    body = f"""<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, viewport-fit=cover">
<title>1.9.12 window remaining ({len(missing)})</title>{style}</head><body>
<div class="banner"><h1>Window — phần còn lại ({len(missing)} clip)</h1>
<p>Chỉ gồm các clip chưa chấm. Chấm xong bấm nộp — hệ thống ghép với phần đã nộp trước.</p>
<p><label>Reviewer: <input id="reviewer" value="human_mobile" size="16"></label></p></div>
{''.join(cards)}
<div class="sticky"><span class="count" id="prog">0/{len(missing)}</span><button id="exp">Nộp phần còn lại</button></div>
<script>
const keys={keys};
function gv(n){{const e=document.querySelector('[name="'+n+'"]');return e?e.value:'';}}
function upd(){{let n=0;for(const k of keys)if(gv('cl_'+k))n++;document.getElementById('prog').textContent=n+'/'+keys.length;}}
document.body.addEventListener('change',upd);upd();
document.getElementById('exp').onclick=async()=>{{
 const rid=(document.getElementById('reviewer')||{{value:'human_mobile'}}).value||'human_mobile';
 let lines=['token_id,blind_clip_id,pronunciation_clarity,boundary_quality,target_recognizability,reviewer_confidence'];
 for(const k of keys){{
  const i=k.lastIndexOf('_');
  const tid=k.slice(0,i), L=k.slice(i+1);
  lines.push([tid,L,gv('cl_'+k),gv('bd_'+k),gv('rc_'+k),gv('cf_'+k)].join(','));
 }}
 const t=lines.join('\\n');
 const btn=document.getElementById('exp');
 try{{
  const r=await fetch('/submit',{{method:'POST',headers:{{'Content-Type':'application/json'}},
   body:JSON.stringify({{pack:'window_expanded_remaining',stage:'A',csv:t,reviewer_id:rid,timestamp:new Date().toISOString()}})}});
  if(r.ok){{const j=await r.json();btn.textContent='Đã nộp ✓ ('+j.rows+' dòng)';btn.style.background='#1a7f37';return;}}
  throw new Error('http '+r.status);
 }}catch(e){{
  btn.textContent='Không gửi được — tải CSV';
  const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([t],{{type:'text/csv'}}));
  a.download='WindowExpandedRemaining_StageA_Filled.csv';a.click();
 }}
}};
</script></body></html>"""
    (HR / "window_blind_remaining.html").write_text(body, encoding="utf-8")


if __name__ == "__main__":
    main()
