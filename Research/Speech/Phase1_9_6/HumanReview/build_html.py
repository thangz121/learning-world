"""Build MODE A (bias-controlled) and MODE B human review HTML for Phase 1.9.6.

MODE A (primary): clip id, source name, time range, duration, player, labels only.
MODE B: adds technical metadata (collapsed by default).

Does NOT auto-fill human labels.
"""
from __future__ import annotations
import json
import html
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE.parent
AUDIT = json.loads((OUT / "Results" / "clip_audit.json").read_text(encoding="utf-8"))
clips = AUDIT["clips"]

LABELS = ["SPEECH", "NON_SPEECH", "MIXED", "UNCERTAIN"]


def card(c: dict, mode_b: bool) -> str:
    cid = html.escape(c["clip_id"])
    src = html.escape(c["source_file"])
    t0 = c["raw_start_s"]
    t1 = c["raw_end_s"]
    d = c["raw_duration_s"]
    audio = html.escape(c["clip_relpath"])
    radios = " ".join(
        f'<label class="lab"><input type="radio" name="{cid}" value="{lab}"> {lab}</label>'
        for lab in LABELS
    )
    tech = ""
    if mode_b:
        tech = (
            f"<details><summary>technical metadata (optional)</summary>"
            f"<pre>raw_start={t0}\nraw_end={t1}\npreview_pad=±{c['preview_pad_s']}s\n"
            f"preview={c['preview_start_s']}–{c['preview_end_s']}\n"
            f"n_samples={c['n_samples']}\n"
            f"NOTE: detector scores intentionally omitted from primary review.</pre></details>"
        )
    return (
        f'<section class="card" id="{cid}">'
        f"<h2>Clip {cid.replace('clip_', '')}</h2>"
        f"<p>Source: {src}<br>Time: {t0:.3f}–{t1:.3f}s<br>Duration: {d:.3f}s</p>"
        f'<audio controls preload="none" src="{audio}"></audio>'
        f'<div class="labels">{radios}</div>'
        f'<p><label>Notes: <input type="text" name="notes_{cid}" size="40"></label></p>'
        f"{tech}"
        f"</section>"
    )


def build(mode: str) -> str:
    mode_b = mode == "B"
    title = "Human Review MODE A (primary)" if not mode_b else "Human Review MODE B (metadata)"
    body_cards = "\n".join(card(c, mode_b) for c in clips)
    return f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{html.escape(title)} — Phase 1.9.6</title>
<style>
body{{font-family:system-ui,sans-serif;max-width:720px;margin:24px auto;padding:0 12px;line-height:1.4}}
.card{{border:1px solid #ccc;border-radius:8px;padding:12px 16px;margin:12px 0}}
.lab{{margin-right:12px;display:inline-block}}
audio{{width:100%;margin:8px 0}}
.banner{{background:#f5f5f5;padding:12px;border-radius:8px;margin-bottom:16px}}
.warn{{color:#444}}
button#export{{padding:8px 16px;font-size:14px}}
</style>
</head>
<body>
<div class="banner">
<h1>{html.escape(title)}</h1>
<p class="warn"><b>Instructions:</b> Listen to each clip. Choose exactly one label.
Do not use external detector/ASR scores. If unclear, choose UNCERTAIN or MIXED.</p>
<p>Labels: <b>SPEECH</b> | <b>NON_SPEECH</b> | <b>MIXED</b> | <b>UNCERTAIN</b></p>
<p>Total clips: {len(clips)}. Fill all 28. Then click Export CSV and save as
<code>Results/Human_Review_Labels_Filled.csv</code>.</p>
<p>Reference type after fill: <b>SINGLE_REVIEWER_REFERENCE</b> (not ground truth).</p>
</div>
{body_cards}
<p><button id="export" type="button">Export labels CSV</button></p>
<pre id="out"></pre>
<script>
document.getElementById('export').onclick = function() {{
  const ids = {[json.dumps(c["clip_id"]) for c in clips]};
  const meta = {json.dumps({c["clip_id"]: c for c in clips})};
  let lines = ['clip_id,source_file,raw_start_s,raw_end_s,raw_duration_s,human_label,notes,reviewer_id,review_date'];
  for (const id of ids) {{
    const m = meta[id];
    const sel = document.querySelector('input[name="'+id+'"]:checked');
    const lab = sel ? sel.value : '';
    const notes = (document.querySelector('input[name="notes_'+id+'"]') || {{value:''}}).value.replace(/,/g,';');
    lines.push([id, m.source_file, m.raw_start_s, m.raw_end_s, m.raw_duration_s, lab, notes, '', ''].join(','));
  }}
  const text = lines.join('\\n');
  document.getElementById('out').textContent = text;
  const blob = new Blob([text], {{type:'text/csv'}});
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = 'Human_Review_Labels_Filled.csv';
  a.click();
}};
</script>
</body>
</html>
"""


(HERE / "review.html").write_text(build("A"), encoding="utf-8")
(HERE / "review_mode_b.html").write_text(build("B"), encoding="utf-8")
print("wrote", HERE / "review.html")
print("wrote", HERE / "review_mode_b.html")
