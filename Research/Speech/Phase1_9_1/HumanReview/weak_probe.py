import json
import sys
from pathlib import Path
import numpy as np
import soundfile as sf

REPO = Path(r"D:\Vscode\little-world-english")
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.Adapters.asr_moonshine import MoonshineAsrAdapter

man = json.loads((REPO / "Research/Speech/Phase1_9_1/Results/human_review_manifest.json").read_text(encoding="utf-8"))
asr = MoonshineAsrAdapter()
probes = []
for r in man["hybrid_segments"]:
    p = REPO / r["clip_path"]
    x, sr = sf.read(str(p))
    rms = float(np.sqrt(np.mean(x ** 2) + 1e-12))
    try:
        t = (asr.run(str(p)).text or "").strip()
    except Exception as e:
        t = f"ERR:{type(e).__name__}"
    probes.append({
        "segment_index": r["segment_index"],
        "start_s": r["start_s"],
        "end_s": r["end_s"],
        "duration_s": r["duration_s"],
        "rms": rms,
        "moonshine_asr": t,
        "weak_probe": "ASR_NONEMPTY" if t else "ASR_EMPTY",
        "reference_type": "WEAK_AUTOMATED_PROBE_NOT_HUMAN",
    })
    print(r["segment_index"], round(r["duration_s"], 2), "rms", round(rms, 4), repr(t[:40]), flush=True)

out = REPO / "Research/Speech/Phase1_9_1/Results/weak_automated_probe_hybrid.json"
out.write_text(json.dumps(probes, indent=2, ensure_ascii=False), encoding="utf-8")
print("nonempty", sum(1 for p in probes if p["moonshine_asr"]), "of", len(probes))
