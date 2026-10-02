"""Speechocean762 human-correlation bench (ADULT/L2 population — NOT 4yo).
License: CC BY 4.0 (OpenSLR 101).
Human total is typically 0-10; we compare against score0-100 via *10 mapping
AND rank correlation (scale-free).
"""
from __future__ import annotations
import json
import sys
import wave
import struct
from pathlib import Path
import subprocess

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Adapters.paths import RESULTS

SO = Path(r"D:\speech-lab\data\speechocean762")
TMP = Path(r"D:\speech-lab\data\so762_16k")
TMP.mkdir(parents=True, exist_ok=True)


def ensure_16k(src: Path, dst: Path) -> Path:
    if dst.exists():
        return dst
    # ffmpeg resample
    subprocess.run(
        ["ffmpeg", "-y", "-v", "error", "-i", str(src), "-ar", "16000", "-ac", "1", str(dst)],
        check=True)
    return dst


def pearson(xs, ys):
    n = len(xs)
    if n < 3:
        return None
    mx = sum(xs) / n
    my = sum(ys) / n
    num = sum((x - mx) * (y - my) for x, y in zip(xs, ys))
    denx = sum((x - mx) ** 2 for x in xs) ** 0.5
    deny = sum((y - my) ** 2 for y in ys) ** 0.5
    if denx == 0 or deny == 0:
        return None
    return num / (denx * deny)


def spearman(xs, ys):
    # rank then pearson
    def ranks(a):
        order = sorted(range(len(a)), key=lambda i: a[i])
        r = [0] * len(a)
        for rank, i in enumerate(order):
            r[i] = rank + 1.0
        return r
    return pearson(ranks(xs), ranks(ys))


def mae(xs, ys):
    return sum(abs(x - y) for x, y in zip(xs, ys)) / max(1, len(xs))


def main(limit: int = 40, split: str = "test"):
    RESULTS.mkdir(parents=True, exist_ok=True)
    scores = json.load(open(SO / "resource" / "scores.json", encoding="utf-8"))
    scp = {}
    with open(SO / split / "wav.scp", encoding="utf-8") as f:
        for line in f:
            uid, rel = line.strip().split("\t")
            scp[uid] = SO / rel.replace("/", "\\") if "\\" not in rel else SO / rel
            # path uses WAVE/...
            scp[uid] = SO / Path(rel)

    pipe = SpeakingPipeline(enable_openpronounce=False)
    rows = []
    uids = list(scp.keys())[:limit]
    for i, uid in enumerate(uids):
        meta = scores.get(uid)
        if not meta:
            continue
        text = meta.get("text") or ""
        human_total = float(meta.get("total", 0))
        human_acc = float(meta.get("accuracy", human_total))
        wav = scp[uid]
        if not wav.exists():
            # try forward slashes resolved
            wav = SO / meta.get("path", "")
        if not Path(scp[uid]).exists():
            # rebuild from scp original
            with open(SO / split / "wav.scp", encoding="utf-8") as f:
                for line in f:
                    if line.startswith(uid):
                        rel = line.strip().split("\t")[1]
                        wav = SO / rel
                        break
        if not wav.exists():
            print("MISS", uid, flush=True)
            continue
        dst = TMP / f"{uid}.wav"
        try:
            ensure_16k(wav, dst)
        except Exception as e:
            print("FFMPEG_FAIL", uid, e, flush=True)
            continue
        try:
            r = pipe.run(str(dst), text, population_label="adult-L2-speechocean762")
        except Exception as e:
            print("PIPE_FAIL", uid, type(e).__name__, e, flush=True)
            continue
        s1 = r.scores["scorer_v1"].score_0_100
        conf = r.scores["scorer_v1"].confidence_0_1
        human100 = human_total * 10.0  # 0-10 -> 0-100 linear map
        row = {
            "uid": uid, "text": text,
            "human_total_0_10": human_total,
            "human_acc_0_10": human_acc,
            "human_total_x10": human100,
            "s1_score": s1, "s1_conf": conf, "s1_per": r.scores["scorer_v1"].per,
            "asr": r.asr.text, "abs_err_x10": abs(s1 - human100),
            "total_s": r.total_processing_s,
        }
        rows.append(row)
        print(f"SO {i+1}/{limit} {uid} H={human_total:.1f} s1={s1:.1f} conf={conf:.3f} asr={r.asr.text!r}", flush=True)

    hs = [r["human_total_x10"] for r in rows]
    ss = [r["s1_score"] for r in rows]
    summary = {
        "n": len(rows),
        "population": "adult-L2-speechocean762",
        "NOT_4yo": True,
        "human_scale": "total 0-10, compared as *10 to 0-100",
        "pearson_s1_vs_human_x10": pearson(ss, hs),
        "spearman_s1_vs_human_x10": spearman(ss, hs),
        "mae_s1_vs_human_x10": mae(ss, hs) if rows else None,
        "mean_s1": sum(ss) / len(ss) if ss else None,
        "mean_human_x10": sum(hs) / len(hs) if hs else None,
        "rows_path": str(RESULTS / "speechocean762_rows.json"),
    }
    (RESULTS / "speechocean762_rows.json").write_text(json.dumps(rows, indent=2), encoding="utf-8")
    (RESULTS / "speechocean762_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print("SUMMARY", json.dumps(summary, indent=2), flush=True)


if __name__ == "__main__":
    import json
    lim = int(sys.argv[1]) if len(sys.argv) > 1 else 40
    main(limit=lim)
