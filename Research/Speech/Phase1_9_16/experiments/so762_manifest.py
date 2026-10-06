"""Phase 1.9.16 — speechocean762 manifest + phoneme-score parser (research only).

Parses the local speechocean762 release (SLR101) into a per-utterance manifest:
speaker, age, child flag, split, text, canonical ARPAbet phones, and per-phone
human expert scores decoded from the documented notation:
    bare = score 2 (correct), {} = score 1 (accented), () = score 0 (incorrect/missed),
    [] = insertion (not part of the reference phone sequence).

Outputs:
  artifacts/audit/so762_manifest.csv
  artifacts/audit/so762_summary.json
"""
from __future__ import annotations

import csv
import json
import re
from collections import Counter, defaultdict
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
SO = Path(r"D:\speech-lab\data\speechocean762")
OUT = REPO / "Research/Speech/Phase1_9_16/artifacts/audit"
OUT.mkdir(parents=True, exist_ok=True)


def read_lines(p: Path):
    return [ln.rstrip("\n") for ln in p.open(encoding="utf-8") if ln.strip()]


def parse_expert_string(s: str):
    """Return (ref_scores list aligned by position after dropping insertions, n_insertions)."""
    scores = []
    n_ins = 0
    for tok in s.split():
        t = tok.strip()
        if not t:
            continue
        if t.startswith("[") and t.endswith("]"):
            n_ins += 1
            continue
        core = t
        score = 2
        if t.startswith("(") and t.endswith(")"):
            score = 0
            core = t[1:-1]
        elif t.startswith("{") and t.endswith("}"):
            score = 1
            core = t[1:-1]
        scores.append(core)
    return scores, n_ins


def main():
    detail = json.loads((SO / "resource/scores-detail.json").read_text(encoding="utf-8"))
    scores = json.loads((SO / "resource/scores.json").read_text(encoding="utf-8"))

    speaker_meta = {}
    utt_split = {}
    for split in ("train", "test"):
        for ln in read_lines(SO / split / "spk2age"):
            spk, age = ln.split()
            speaker_meta[spk] = {"age": int(age), "split": split}
        for ln in read_lines(SO / split / "utt2spk"):
            utt, spk = ln.split()
            utt_split[utt] = (split, spk)

    rows = []
    parse_errors = 0
    phone_score_counter = Counter()
    for utt, item in detail.items():
        split, spk = utt_split.get(utt, ("?", "?"))
        age = speaker_meta.get(spk, {}).get("age")
        ref_all, score_all, exp_ok, ins_total, mismatch = [], [], 0, 0, 0
        for w in item.get("words", []):
            ref = (w.get("ref-phones") or "").split()
            experts = w.get("phones") or []
            per_phone = [[] for _ in ref]
            for es in experts:
                toks = es.split()
                j = 0
                ok = True
                for t in toks:
                    if t.startswith("[") and t.endswith("]"):
                        continue
                    if j >= len(ref):
                        ok = False
                        break
                    sc = 2
                    core = t
                    if t.startswith("(") and t.endswith(")"):
                        sc, core = 0, t[1:-1]
                    elif t.startswith("{") and t.endswith("}"):
                        sc, core = 1, t[1:-1]
                    per_phone[j].append(sc)
                    j += 1
                if not ok or j != len(ref):
                    mismatch += 1
            for ph, scs in zip(ref, per_phone):
                ref_all.append(ph)
                if scs:
                    avg = sum(scs) / len(scs)
                    score_all.append(round(avg, 3))
                    phone_score_counter[round(avg)] += 1
                else:
                    score_all.append("")
                    phone_score_counter["missing"] += 1
        rows.append({
            "utt_id": utt, "split": split, "speaker_id": spk, "age": age,
            "is_child": int(age < 18) if age is not None else "",
            "text": item.get("text", ""), "n_phones": len(ref_all),
            "ref_phones": " ".join(ref_all),
            "phone_scores": " ".join(str(s) for s in score_all),
            "n_expert_fields_ok": exp_ok, "n_expert_alignment_mismatch": mismatch,
            "n_insertions_annotated": ins_total,
            "sentence_accuracy": (scores.get(utt, {}) or {}).get("accuracy"),
            "sentence_total": (scores.get(utt, {}) or {}).get("total"),
        })
        if mismatch:
            parse_errors += 1

    fields = list(rows[0].keys())
    with open(OUT / "so762_manifest.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(rows)

    def block(rs):
        ages = [r["age"] for r in rs if r["age"] is not None]
        return {
            "n_utterances": len(rs), "n_speakers": len(set(r["speaker_id"] for r in rs)),
            "n_children": sum(1 for r in rs if r["is_child"] == 1),
            "n_child_speakers": len(set(r["speaker_id"] for r in rs if r["is_child"] == 1)),
            "ages": dict(sorted(Counter(ages).items())),
            "mean_n_phones": round(sum(r["n_phones"] for r in rs) / len(rs), 1),
        }

    summary = {
        "corpus": "speechocean762 (OpenSLR SLR101)",
        "path": str(SO),
        "sample_rate": 16000, "channels": 1, "format": "PCM16 WAV",
        "annotation": "5 experts; phone accuracy 0/1/2 via notation () {} bare; "
                      "insertions []; sentence/word scores also available",
        "train": block([r for r in rows if r["split"] == "train"]),
        "test": block([r for r in rows if r["split"] == "test"]),
        "train_children": block([r for r in rows if r["split"] == "train" and r["is_child"] == 1]),
        "test_children": block([r for r in rows if r["split"] == "test" and r["is_child"] == 1]),
        "utt_with_expert_alignment_mismatch": parse_errors,
        "phone_score_distribution": dict(phone_score_counter),
        "license_note": "OPENSLR/upstream documented as free for commercial and non-commercial "
                        "use; recorded license evidence to be added by audit script "
                        "(LICENSE_AND_DATA_NOTES)",
    }
    (OUT / "so762_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps({k: summary[k] for k in
                      ("train", "test", "train_children", "test_children",
                       "phone_score_distribution", "utt_with_expert_alignment_mismatch")}, indent=2))


if __name__ == "__main__":
    main()
