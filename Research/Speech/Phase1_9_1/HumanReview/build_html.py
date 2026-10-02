import json
import html
from pathlib import Path

REPO = Path(r"D:\Vscode\little-world-english")
man = json.loads((REPO / "Research/Speech/Phase1_9_1/Results/human_review_manifest.json").read_text(encoding="utf-8"))
rows = man["hybrid_segments"]
parts = [
    "<!DOCTYPE html><html><head><meta charset=utf-8><title>IMG_0639 Hybrid VAD Human Review</title>",
    "<style>body{font-family:sans-serif;margin:16px}table{border-collapse:collapse;width:100%}"
    "td,th{border:1px solid #ccc;padding:6px;font-size:13px}audio{width:240px}</style></head><body>",
    "<h1>IMG_0639 hybrid_score segments</h1>",
    "<p><b>Not ground truth.</b> Labels: SPEECH | NON_SPEECH | MIXED_UNCERTAIN. ",
    f"Silero n={man['silero_n_segments']} hybrid n={man['hybrid_n_segments']}</p>",
    "<table><tr><th>#</th><th>start</th><th>end</th><th>dur</th><th>audio</th><th>label (use CSV)</th></tr>",
]
for r in rows:
    clip = "clips_hybrid/" + Path(r["clip_path"]).name
    parts.append(
        f"<tr><td>{r['segment_index']}</td><td>{r['start_s']}</td><td>{r['end_s']}</td>"
        f"<td>{r['duration_s']}</td><td><audio controls src='{html.escape(clip)}'></audio><br>"
        f"<code>{html.escape(clip)}</code></td><td></td></tr>"
    )
parts.append("</table><p>Fill <code>artifacts/Human_Review_Template.csv</code></p></body></html>")
out = REPO / "Research/Speech/Phase1_9_1/HumanReview/review.html"
out.write_text("".join(parts), encoding="utf-8")
print("HTML", out)
