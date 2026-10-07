"""WP-1.9.26 Parts 2/26 — research-only blind review server (no production code).

Serves the 276-candidate review pack:
  GET  /                      reviewer UI (blind: only word, target phone, audio)
  GET  /api/pack?reviewer=ID  candidate list in a per-reviewer randomized order
  GET  /api/progress?reviewer=ID  already-submitted blind_ids (resume)
  GET  /audio/<blind_id>      audio file
  POST /api/submit            append one JSONL record per label to reviews/<reviewer>.jsonl

Reviewers never see machine predictions, other reviewers' files, or the pool.
Run: python serve_review_1926.py [port]
"""
from __future__ import annotations

import csv
import hashlib
import json
import random
import sys
import urllib.parse
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

HERE = Path(__file__).resolve().parent
PHASE = HERE.parent
LAB = PHASE / "01_HUMAN_LABEL_ACQUISITION"
REVIEWS = PHASE / "artifacts" / "reviews"
REVIEWS.mkdir(parents=True, exist_ok=True)
PACK = LAB / "REVIEW_CANDIDATES.csv"

HTML = """<!doctype html><html><head><meta charset="utf-8">
<title>LWE Blind Review WP-1.9.26</title>
<style>
body{font-family:system-ui,Arial,sans-serif;max-width:760px;margin:24px auto;padding:0 16px}
.card{border:1px solid #ccc;border-radius:10px;padding:16px;margin-bottom:16px}
button{padding:10px 16px;margin:4px;border-radius:8px;border:1px solid #888;cursor:pointer}
.present{background:#e8f7e8}.absent{background:#fdeaea}.uncertain{background:#fff8e1}
select,textarea{width:100%;margin-top:8px;padding:8px}
#meta{color:#555;font-size:14px}audio{width:100%;margin-top:8px}
</style></head><body>
<h2>LWE Blind Review — WP-1.9.26</h2>
<div class="card">
  <label>Reviewer ID: <input id="reviewer" value="REV-A" onchange="start()"></label>
  <div id="meta"></div>
  <div id="progress"></div>
</div>
<div class="card" id="card" style="display:none">
  <div id="word"></div>
  <div id="phone"></div>
  <audio id="audio" controls></audio>
  <div>
    <button class="present" onclick="label('PRESENT')">PRESENT</button>
    <button class="absent" onclick="label('ABSENT')">ABSENT</button>
    <button class="uncertain" onclick="label('UNCERTAIN')">UNCERTAIN</button>
  </div>
  <select id="confidence"><option value="">-- confidence --</option>
    <option>HIGH</option><option>MEDIUM</option><option>LOW</option></select>
  <textarea id="note" rows="2" placeholder="optional note (what you heard)"></textarea>
  <div id="hint" style="color:#555;font-size:13px;margin-top:8px"></div>
</div>
<div class="card">
  <button onclick="downloadJSON()">Export my labels (JSON)</button>
  <button onclick="downloadCSV()">Export my labels (CSV)</button>
</div>
<script>
let pack=[], idx=0, reviewer="REV-A", done=new Set();
async function start(){
  reviewer=document.getElementById('reviewer').value||'REV-A';
  const r=await fetch('/api/pack?reviewer='+encodeURIComponent(reviewer));
  pack=await r.json();
  const p=await fetch('/api/progress?reviewer='+encodeURIComponent(reviewer));
  done=new Set(await p.json());
  idx=pack.findIndex(x=>!done.has(x.blind_id));
  if(idx<0) idx=pack.length;
  render();
}
function render(){
  const card=document.getElementById('card');
  if(idx>=pack.length){card.style.display='none';
    document.getElementById('progress').textContent='All candidates reviewed. Export your labels.';return;}
  const c=pack[idx]; card.style.display='block';
  document.getElementById('word').textContent='Word: '+c.word;
  document.getElementById('phone').textContent='Target final phone: '+c.target_phone;
  document.getElementById('audio').src='/audio/'+c.blind_id;
  document.getElementById('confidence').value='';
  document.getElementById('note').value='';
  document.getElementById('hint').textContent=
    'Question: is there defensible acoustic evidence consistent with this final phone?';
  document.getElementById('progress').textContent='Reviewed '+done.size+' / '+pack.length;
}
async function label(v){
  const conf=document.getElementById('confidence').value;
  if(!conf){alert('Pick confidence first');return;}
  const c=pack[idx];
  const body={reviewer_id:reviewer,blind_id:c.blind_id,label:v,confidence:conf,
              note:document.getElementById('note').value,ts:new Date().toISOString()};
  await fetch('/api/submit',{method:'POST',headers:{'Content-Type':'application/json'},
                             body:JSON.stringify(body)});
  done.add(c.blind_id); idx++;
  while(idx<pack.length && done.has(pack[idx].blind_id)) idx++;
  render();
}
function downloadJSON(){
  window.open('/api/export?reviewer='+encodeURIComponent(reviewer)+'&fmt=json');
}
function downloadCSV(){
  window.open('/api/export?reviewer='+encodeURIComponent(reviewer)+'&fmt=csv');
}
start();
</script></body></html>"""


def load_pack():
    return list(csv.DictReader(open(PACK, encoding="utf-8")))


def pack_for(reviewer):
    rows = load_pack()
    rng = random.Random(int(hashlib.sha256(reviewer.encode()).hexdigest(), 16) % (2**32))
    rng.shuffle(rows)
    return [{"blind_id": r["blind_id"], "word": r["word"],
             "target_phone": r["target_phone"]} for r in rows]


def audio_map():
    return {r["blind_id"]: r["audio_reference"] for r in load_pack()}


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *a):  # quieter
        pass

    def _json(self, obj, code=200):
        body = json.dumps(obj).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _file(self, path, ctype):
        try:
            data = Path(path).read_bytes()
        except Exception:  # noqa: BLE001
            self.send_error(404)
            return
        self.send_response(200)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):  # noqa: N802
        u = urllib.parse.urlparse(self.path)
        q = urllib.parse.parse_qs(u.query)
        if u.path == "/":
            body = HTML.encode("utf-8")
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        elif u.path == "/api/pack":
            self._json(pack_for(q.get("reviewer", ["REV-A"])[0]))
        elif u.path == "/api/progress":
            f = REVIEWS / f"{safe(q.get('reviewer', ['REV-A'])[0])}.jsonl"
            ids = []
            if f.exists():
                for line in f.read_text(encoding="utf-8").splitlines():
                    try:
                        ids.append(json.loads(line)["blind_id"])
                    except Exception:  # noqa: BLE001
                        pass
            self._json(ids)
        elif u.path.startswith("/audio/"):
            bid = u.path.split("/")[-1]
            ref = audio_map().get(bid, "")
            ctype = "audio/flac" if ref.lower().endswith(".flac") else "audio/wav"
            self._file(ref, ctype)
        elif u.path == "/api/export":
            rev = q.get("reviewer", ["REV-A"])[0]
            fmt = q.get("fmt", ["json"])[0]
            f = REVIEWS / f"{safe(rev)}.jsonl"
            rows = [json.loads(x) for x in f.read_text(encoding="utf-8").splitlines()
                    if x.strip()] if f.exists() else []
            if fmt == "csv":
                out = "blind_id,label,confidence,note,ts\n" + "\n".join(
                    f"{r['blind_id']},{r['label']},{r['confidence']},"
                    f"\"{str(r.get('note','')).replace(chr(34),'')}\",{r['ts']}" for r in rows)
                body = out.encode("utf-8")
                self.send_response(200)
                self.send_header("Content-Type", "text/csv; charset=utf-8")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                self.wfile.write(body)
            else:
                self._json(rows)
        else:
            self.send_error(404)

    def do_POST(self):  # noqa: N802
        if self.path != "/api/submit":
            self.send_error(404)
            return
        length = int(self.headers.get("Content-Length", 0))
        try:
            data = json.loads(self.rfile.read(length).decode("utf-8"))
        except Exception:  # noqa: BLE001
            self.send_error(400, "bad json")
            return
        rev = safe(str(data.get("reviewer_id") or "REV-A"))
        rec = {
            "reviewer_id": rev,
            "blind_id": str(data.get("blind_id")),
            "label": str(data.get("label")),
            "confidence": str(data.get("confidence")),
            "note": str(data.get("note", ""))[:500],
            "ts": str(data.get("ts") or datetime.now(timezone.utc).isoformat()),
        }
        if rec["label"] not in ("PRESENT", "ABSENT", "UNCERTAIN"):
            self.send_error(400, "bad label")
            return
        with open(REVIEWS / f"{rev}.jsonl", "a", encoding="utf-8") as f:
            f.write(json.dumps(rec, ensure_ascii=False) + "\n")
        self._json({"ok": True, "stored": rec["blind_id"]})


def safe(s):
    return "".join(ch for ch in s if ch.isalnum() or ch in "._-")[:64] or "REV-A"


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8791
    print(f"review pack: {PACK} ({len(load_pack())} candidates)")
    print(f"reviews dir: {REVIEWS}")
    print(f"serving on http://0.0.0.0:{port} (blind; machine fields not exposed)")
    ThreadingHTTPServer(("0.0.0.0", port), Handler).serve_forever()


if __name__ == "__main__":
    main()
