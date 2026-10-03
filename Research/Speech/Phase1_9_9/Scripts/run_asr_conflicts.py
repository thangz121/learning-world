"""Phase 1.9.9 completion: ASR conflict analysis on the 42 human-reviewed child tokens.

ASR is supporting evidence only (asr_is_not_pronunciation_judge = true).
Does not modify the frozen scorer.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

OUT = REPO / "Research/Speech/Phase1_9_9"
RES = OUT / "Results"
HR = OUT / "HumanReview"


def write_csv(path: Path, rows):
    if not rows:
        path.write_text("empty\n", encoding="utf-8")
        return
    keys = []
    for r in rows:
        for k in r:
            if k not in keys:
                keys.append(k)
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)


def main():
    human = list(csv.DictReader((RES / "human_review_results.csv").open(encoding="utf-8-sig")))
    meta = json.loads((HR / "review_metadata.json").read_text(encoding="utf-8"))
    by_id = {m["review_id"]: m for m in meta["items"]}

    try:
        from Research.Speech.Phase1_2.Adapters.asr_moonshine import MoonshineAsrAdapter

        asr = MoonshineAsrAdapter()
        asr_ok = True
    except Exception as e:
        print("ASR_INIT_FAIL", e)
        asr_ok = False

    rows = []
    for h in human:
        rid = h["review_id"]
        m = by_id.get(rid, {})
        target = (m.get("target") or "").lower()
        clip = HR / "clips" / f"{rid}_LISTEN.wav"
        if not clip.exists():
            clip = HR / "clips" / f"{rid}_FULL.wav"
        text = ""
        status = "ASR_UNAVAILABLE"
        if asr_ok and clip.exists():
            try:
                text = (asr.run(str(clip)).text or "").strip()
                tl = text.lower()
                toks = tl.replace(".", "").replace(",", "").split()
                if not tl:
                    status = "ASR_EMPTY"
                elif target in toks:
                    status = "ASR_CORRECT"
                else:
                    status = "ASR_WRONG"
            except Exception as e:
                status = f"ASR_ERROR:{type(e).__name__}"

        hp = h.get("human_pronunciation", "")
        diag = h.get("diagnostic", "")
        # conflict classification
        if status == "ASR_CORRECT" and hp in ("CLEAR_INCORRECT", "PROBABLY_INCORRECT"):
            conflict = "ASR_RIGHT_HUMAN_WRONG"
        elif status == "ASR_WRONG" and hp in ("CLEAR_CORRECT", "PROBABLY_CORRECT"):
            conflict = "ASR_WRONG_HUMAN_RIGHT"
        elif status == "ASR_EMPTY" and hp in ("CLEAR_CORRECT", "PROBABLY_CORRECT"):
            conflict = "ASR_EMPTY_HUMAN_RIGHT"
        elif status == "ASR_EMPTY" and hp in ("CLEAR_INCORRECT", "PROBABLY_INCORRECT"):
            conflict = "ASR_EMPTY_HUMAN_WRONG"
        elif status.startswith("ASR_ERROR") or status == "ASR_UNAVAILABLE":
            conflict = "ASR_UNAVAILABLE"
        else:
            conflict = "NO_CONFLICT"

        rows.append(
            {
                "review_id": rid,
                "speaker_id": h.get("speaker_id"),
                "target": h.get("target"),
                "full_score": h.get("full_score"),
                "human_pronunciation": hp,
                "diagnostic": diag,
                "asr_status": status,
                "asr_text": text,
                "asr_vs_human": conflict,
                "policy": "asr_is_not_pronunciation_judge",
            }
        )
        print(rid, target, "score", h.get("full_score"), "human", hp, "asr", status, repr(text[:40]), flush=True)

    write_csv(RES / "asr_phone_conflicts.csv", rows)

    # summary
    from collections import Counter

    c = Counter(r["asr_vs_human"] for r in rows)
    summary = {
        "n": len(rows),
        "conflicts": dict(c),
        "asr_correct_n": sum(1 for r in rows if r["asr_status"] == "ASR_CORRECT"),
        "asr_wrong_n": sum(1 for r in rows if r["asr_status"] == "ASR_WRONG"),
        "asr_empty_n": sum(1 for r in rows if r["asr_status"] == "ASR_EMPTY"),
    }
    (RES / "asr_conflict_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print("SUMMARY", summary)


if __name__ == "__main__":
    main()
