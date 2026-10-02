"""MODE A review UI: NEW speech reference + IMG hybrid in >=2s listen windows."""
from __future__ import annotations
import html
import json
from pathlib import Path

HERE = Path(__file__).resolve().parent
OUT = HERE.parent
RES = OUT / "Results"
min2s = json.loads((RES / "min2s_manifest.json").read_text(encoding="utf-8"))
ref = json.loads((RES / "new_speech_reference.json").read_text(encoding="utf-8"))
clips = min2s["clips"]
refs = ref["reference_clips"]
LABELS = ["SPEECH", "NON_SPEECH", "MIXED", "UNCERTAIN"]


def label_radios(name: str) -> str:
    return " ".join(
        f'<label class="lab"><input type="radio" name="{html.escape(name)}" value="{lab}"> {lab}</label>'
        for lab in LABELS
    )


parts = [
    "<!DOCTYPE html><html lang=en><head><meta charset=utf-8>",
    "<title>Human Review min2s + NEW reference — Phase 1.9.6</title>",
    "<style>",
    "body{font-family:system-ui,sans-serif;max-width:760px;margin:24px auto;padding:0 12px;line-height:1.4}",
    ".card{border:1px solid #ccc;border-radius:8px;padding:12px 16px;margin:12px 0}",
    ".ref{border-color:#2a7}",
    ".lab{margin-right:12px;display:inline-block}",
    "audio{width:100%;margin:8px 0}",
    ".banner{background:#f5f5f5;padding:12px;border-radius:8px;margin-bottom:16px}",
    "h1,h2{margin:0.2em 0}",
    "button#export{padding:8px 16px;font-size:14px}",
    "</style></head><body>",
    '<div class="banner">',
    "<h1>Round 2 — min 2s listen windows</h1>",
    "<p><b>Round 1 result:</b> all 28 short hybrid clips labeled <b>UNCERTAIN</b> "
    "(too short to identify speech; raw mean ~0.25s, all &lt;0.5s).</p>",
    "<p><b>Speech reference standard:</b> NEW file "
    f"<code>{html.escape(ref['original_filename'])}</code> "
    "(listen section A first to calibrate what SPEECH sounds like).</p>",
    "<p>Section B: IMG hybrid candidates centered in <b>≥2.0s</b> windows. "
    "Raw detector timestamps are unchanged (shown for mapping only).</p>",
    "<p>Labels: SPEECH | NON_SPEECH | MIXED | UNCERTAIN</p>",
    "</div>",
    "<h1>A. Speech reference (NEW)</h1>",
    "<p>These are calibration clips only — not IMG hybrid scores.</p>",
]

for r in refs:
    rid = html.escape(r["ref_id"])
    parts.append(
        f'<section class="card ref" id="{rid}">'
        f"<h2>{rid}</h2>"
        f"<p>Source: NEW (reference standard)<br>"
        f"Listen window: {r['export_start']:.2f}–{r['export_end']:.2f}s "
        f"({r['export_duration']:.2f}s)</p>"
        f'<audio controls preload="none" src="{html.escape(r["clip"])}"></audio>'
        f"<p><i>No label required — reference only.</i></p>"
        f"</section>"
    )

parts.append("<h1>B. IMG hybrid candidates (≥2s context)</h1>")
for c in clips:
    cid = html.escape(c["clip_id"])
    parts.append(
        f'<section class="card" id="{cid}">'
        f'<h2>Clip {cid.replace("clip_", "")}</h2>'
        f"<p>Source: IMG_0639<br>"
        f"Detector time (raw): {c['raw_start_s']:.3f}–{c['raw_end_s']:.3f}s "
        f"(raw dur {c['raw_duration_s']:.3f}s)<br>"
        f"Listen window: {c['listen_start_s']:.3f}–{c['listen_end_s']:.3f}s "
        f"({c['listen_duration_s']:.2f}s)</p>"
        f'<audio controls preload="none" src="{html.escape(c["clip_relpath"])}"></audio>'
        f'<div class="labels">{label_radios(cid)}</div>'
        f'<p><label>Notes: <input type="text" name="notes_{cid}" size="48"></label></p>'
        f"</section>"
    )

ids_json = json.dumps([c["clip_id"] for c in clips])
meta_json = json.dumps({c["clip_id"]: c for c in clips})

parts.append(
    """
<p><button id="export" type="button">Export min2s labels CSV</button></p>
<pre id="out"></pre>
<script>
const ids = """
    + ids_json
    + """;
const meta = """
    + meta_json
    + """;
document.getElementById('export').onclick = function() {
  let lines = ['clip_id,raw_start_s,raw_end_s,raw_duration_s,listen_duration_s,human_label,notes,reviewer_id,review_date'];
  for (const id of ids) {
    const m = meta[id];
    const sel = document.querySelector('input[name="'+id+'"]:checked');
    const lab = sel ? sel.value : '';
    const notes = (document.querySelector('input[name="notes_'+id+'"]') || {value:''}).value.replace(/,/g,';');
    lines.push([id, m.raw_start_s, m.raw_end_s, m.raw_duration_s, m.listen_duration_s, lab, notes, '', ''].join(','));
  }
  const text = lines.join('\\n');
  document.getElementById('out').textContent = text;
  const blob = new Blob([text], {type:'text/csv'});
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = 'Human_Review_Labels_min2s_Filled.csv';
  a.click();
};
</script>
</body></html>
"""
)

out = HERE / "review_min2s.html"
out.write_text("".join(parts), encoding="utf-8")
print("wrote", out)
