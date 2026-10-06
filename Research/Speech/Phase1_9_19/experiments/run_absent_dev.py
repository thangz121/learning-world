"""WP-1.9.19 — absent-enriched so762 dev set (train children, speaker-disjoint).

Selects train-child utterances containing a word-final consonant with human
score < 0.5 (the scarce absent class) and runs the window family on them, so the
dev window-selection step has both classes. Speakers remain train children,
disjoint from the test-child evaluation set.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(REPO))
sys.path.insert(0, str(HERE))

import window_causality as wc  # noqa: E402

SO = Path(r"D:\speech-lab\data\speechocean762")
MAN = REPO / "Research/Speech/Phase1_9_16/artifacts/audit/so762_manifest.csv"


def main():
    detail = json.loads((SO / "resource/scores-detail.json").read_text(encoding="utf-8"))
    man = list(csv.DictReader(MAN.open(encoding="utf-8")))
    utt_ids, speakers = set(), set()
    for m in man:
        if m["is_child"] != "1" or m["split"] != "train":
            continue
        for w in detail[m["utt_id"]]["words"]:
            ref = (w.get("ref-phones") or "").split()
            if not ref:
                continue
            last = ref[-1]
            experts = w.get("phones") or []
            per = []
            for es in experts:
                toks = [t for t in es.split() if not (t.startswith("[") and t.endswith("]"))]
                if toks:
                    t = toks[-1]
                    per.append(0 if (t.startswith("(") and t.endswith(")")) else
                               (1 if (t.startswith("{") and t.endswith("}")) else 2))
            if per and last.upper() not in {"AA", "AE", "AH", "AO", "AW", "AY", "EH", "ER",
                                            "EY", "IH", "IY", "OW", "OY", "UH", "UW"} \
                    and sum(per) / len(per) < 0.5:
                utt_ids.add(m["utt_id"])
                speakers.add(m["speaker_id"])
    # keep speakers disjoint from the 12-spk dev/test samples
    allc = [m for m in man if m["is_child"] == "1"]
    dev12 = sorted({m["speaker_id"] for m in allc if m["split"] == "train"})[:12]
    test12 = sorted({m["speaker_id"] for m in allc if m["split"] == "test"})[:12]
    speakers -= set(dev12) | set(test12)
    utt_ids = {u for u in utt_ids if any(m["utt_id"] == u and m["speaker_id"] in speakers for m in allc)}
    print(f"enriched dev: {len(utt_ids)} utt, {len(speakers)} speakers", flush=True)

    pev = wc.PhoneEvidenceV2()
    pev._ensure()
    class_ids = {c: ids for c, ids in pev._canon_ids.items() if c != "sil"}
    hv = wc.HybridVAD()
    rows = wc.run_so762(pev, class_ids, hv, speakers, "so762_absent_dev", utt_ids=utt_ids)
    out = wc.ART / "window_rows_so762_absent_dev.csv"
    with open(out, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
    print("rows", len(rows), "->", out, flush=True)


if __name__ == "__main__":
    main()
