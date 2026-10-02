"""Analyze filled human labels for Phase 1.9.6.

Authority: HUMAN_LABEL only.
Does not invent labels. Exits with PENDING if labels empty/incomplete.
"""
from __future__ import annotations
import csv
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

OUT = Path(__file__).resolve().parents[1]
RESULTS = OUT / "Results"
AUDIT = json.loads((RESULTS / "clip_audit.json").read_text(encoding="utf-8"))

VALID = {"SPEECH", "NON_SPEECH", "MIXED", "UNCERTAIN"}
DUR_BINS = [
    ("lt_0.10", 0.0, 0.10),
    ("0.10_0.20", 0.10, 0.20),
    ("0.20_0.30", 0.20, 0.30),
    ("0.30_0.50", 0.30, 0.50),
    ("ge_0.50", 0.50, 1e9),
]


def load_labels(path: Path) -> dict:
    rows = {}
    with path.open(encoding="utf-8-sig", newline="") as f:
        for row in csv.DictReader(f):
            cid = row["clip_id"].strip()
            lab = (row.get("human_label") or "").strip().upper()
            rows[cid] = lab
    return rows


def main():
    candidates = [
        RESULTS / "Human_Review_Labels_Filled.csv",
        RESULTS / "Human_Review_Labels.csv",
    ]
    path = next((p for p in candidates if p.exists()), None)
    if path is None:
        print("No label CSV found")
        sys.exit(2)

    labels = load_labels(path)
    clips = AUDIT["clips"]
    filled = []
    missing = []
    invalid = []
    for c in clips:
        lab = labels.get(c["clip_id"], "")
        if not lab:
            missing.append(c["clip_id"])
        elif lab not in VALID:
            invalid.append((c["clip_id"], lab))
        else:
            filled.append((c, lab))

    total = len(clips)
    counts = Counter(lab for _, lab in filled)
    speech = counts["SPEECH"]
    non = counts["NON_SPEECH"]
    mixed = counts["MIXED"]
    unc = counts["UNCERTAIN"]
    n_filled = len(filled)

    by_dur = defaultdict(Counter)
    for c, lab in filled:
        d = c["raw_duration_s"]
        for name, lo, hi in DUR_BINS:
            if lo <= d < hi:
                by_dur[name][lab] += 1
                break

    if n_filled < total or missing or invalid:
        status = "HUMAN_REVIEW_PENDING"
        decision = "C. HUMAN_REVIEW_INCONCLUSIVE"
        reason = (
            f"Labels incomplete: filled={n_filled}/{total}, "
            f"missing={missing}, invalid={invalid}"
        )
    else:
        status = "HUMAN_REVIEW_COMPLETE"
        amb = mixed + unc
        # Decision rules (explicit, no vanity PASS)
        if amb >= total * 0.4:
            decision = "C. HUMAN_REVIEW_INCONCLUSIVE"
            reason = f"Ambiguous (MIXED+UNCERTAIN)={amb}/{total} too large"
        elif speech >= max(1, int(0.4 * total)) and non + amb <= speech:
            decision = "A. HUMAN_VERIFIED_PROMISING"
            reason = f"SPEECH={speech}/{total} substantial; NON_SPEECH={non}; ambiguous={amb}"
        elif speech >= 1:
            decision = "B. HUMAN_VERIFIED_WEAK_OR_MIXED"
            reason = f"Some SPEECH={speech} but fragmentation/noise/mixed notable (NON={non}, amb={amb})"
        else:
            decision = "B. HUMAN_VERIFIED_WEAK_OR_MIXED"
            reason = f"SPEECH=0; hybrid candidates not human-verified speech (NON={non}, amb={amb})"

    strict = (speech / total) if total else None
    cons = (speech / n_filled) if n_filled else None
    usable = speech + non
    usable_rate = usable / total if total else None
    amb_rate = (mixed + unc) / total if total else None

    out = {
        "phase": "1.9.6",
        "status": status,
        "decision": decision,
        "reason": reason,
        "label_csv": str(path.relative_to(OUT.parent.parent.parent)).replace("\\", "/")
        if False
        else str(path),
        "total_candidates": total,
        "labels_filled": n_filled,
        "missing_clip_ids": missing,
        "invalid": invalid,
        "speech_count": speech if n_filled else None,
        "non_speech_count": non if n_filled else None,
        "mixed_count": mixed if n_filled else None,
        "uncertain_count": unc if n_filled else None,
        "speech_rate": (speech / total) if n_filled == total else None,
        "non_speech_rate": (non / total) if n_filled == total else None,
        "usable_rate": usable_rate if n_filled == total else None,
        "ambiguous_rate": amb_rate if n_filled == total else None,
        "strict_speech_recovery": strict if n_filled == total else None,
        "conservative_speech_recovery": cons if n_filled == total else None,
        "by_duration_bin": {k: dict(v) for k, v in by_dur.items()},
        "duration_stats_all_candidates": AUDIT["duration_stats"],
        "evidence_roles": AUDIT["evidence_roles"],
        "limitations": [
            "Candidate-level review only — NOT full-file VAD ground truth",
            "Cannot claim full-file recall/FN from these 28 clips alone",
            "ASR_EMPTY != NON_SPEECH",
            "Energy pseudo-ref is not human GT",
            "SINGLE_REVIEWER_REFERENCE only — not multi-rater GT",
            "Do not report hybrid accuracy% without explicit metric definition",
            "Do not feed tight crops to pronunciation scorer (use full/padded)",
        ],
        "production_vad": False,
        "answers": {
            "1_n_candidates": total,
            "2_speech": speech if n_filled == total else "PENDING_HUMAN",
            "3_non_speech": non if n_filled == total else "PENDING_HUMAN",
            "4_mixed_uncertain": (mixed + unc) if n_filled == total else "PENDING_HUMAN",
            "5_duration_bins": dict(by_dur) if n_filled == total else "PENDING_HUMAN",
            "6_hybrid_recovers_speech_when_silero_0": (
                "YES_ON_THIS_FILE_CANDIDATES" if n_filled == total and speech > 0
                else ("NO_ON_THESE_CANDIDATES" if n_filled == total and speech == 0 else "PENDING_HUMAN")
            ),
            "7_production_vad": False,
            "8_next": (
                "Phase 1.9.7 multi-recording hybrid rescue validation"
                if decision.startswith("A.")
                else (
                    "Research-only fragmentation/threshold study; no production lock"
                    if decision.startswith("B.")
                    else "Complete human listening of all 28 MODE A clips"
                )
            ),
        },
    }

    (RESULTS / "human_review_results.json").write_text(json.dumps(out, indent=2, default=str), encoding="utf-8")
    with open(RESULTS / "human_review_summary.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(["metric", "value"])
        for k in [
            "status", "decision", "reason", "total_candidates", "labels_filled",
            "speech_count", "non_speech_count", "mixed_count", "uncertain_count",
            "speech_rate", "non_speech_rate", "usable_rate", "ambiguous_rate",
            "strict_speech_recovery", "conservative_speech_recovery", "production_vad",
        ]:
            w.writerow([k, out.get(k)])

    print(json.dumps({"status": status, "decision": decision, "filled": n_filled, "total": total}, indent=2))


if __name__ == "__main__":
    main()
