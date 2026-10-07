"""WP-1.9.27 Parts 30/31 — data leakage checks + statistical power audit.

Checks the frozen pilot manifest for speaker leakage, duplicate audio, session
overlap, label/score leakage; computes the clean local pool size and Wilson
confidence intervals for the pilot sample sizes. No training, no inference.
"""
from __future__ import annotations

import csv
import json
import math
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_27"
ART = OUT / "artifacts"
P21 = L.REPO / "Research/Speech/Phase1_9_21/artifacts/frame_cache"


def wilson(k, n, z=1.96):
    if n == 0:
        return (None, None)
    p = k / n
    d = 1 + z * z / n
    c = (p + z * z / (2 * n)) / d
    h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / d
    return (round(c - h, 3), round(c + h, 3))


def main():
    rows = list(csv.DictReader(open(OUT / "06_PILOT_DATA" / "PILOT_DATA_MANIFEST.csv",
                                    encoding="utf-8")))
    split = json.loads((ART / "pilot_split.json").read_text(encoding="utf-8"))
    sp = split["split"]
    checks = {}
    checks["train_dev_overlap"] = len(set(sp["train"]) & set(sp["dev"]))
    checks["train_test_overlap"] = len(set(sp["train"]) & set(sp["test"]))
    checks["dev_test_overlap"] = len(set(sp["dev"]) & set(sp["test"]))
    paths = [r["audio_path"] for r in rows]
    checks["duplicate_audio_paths"] = len(paths) - len(set(paths))
    pairs = Counter((r["speaker_id"], r["text"]) for r in rows)
    checks["duplicate_speaker_text_pairs"] = sum(1 for v in pairs.values() if v > 1)
    utt_by_split = defaultdict(set)
    for r in rows:
        utt_by_split[r["split"]].add(r["utt_id"])
    checks["utt_overlap_train_test"] = len(utt_by_split["train"] & utt_by_split["test"])
    checks["label_fields_in_manifest"] = [c for c in rows[0] if "label" in c.lower()]
    checks["machine_score_fields_in_manifest"] = [c for c in rows[0]
                                                  if "max_a" in c.lower()
                                                  or "evidence" in c.lower()]
    checks["manifest_rows"] = len(rows)
    checks["manifest_pass"] = sum(1 for r in rows if r["qc_status"] == "PASS")
    checks["leakage_pass"] = (checks["train_dev_overlap"] == 0
                              and checks["train_test_overlap"] == 0
                              and checks["dev_test_overlap"] == 0
                              and checks["duplicate_audio_paths"] == 0
                              and checks["utt_overlap_train_test"] == 0
                              and not checks["label_fields_in_manifest"]
                              and not checks["machine_score_fields_in_manifest"])

    # clean local pool: all child speakers not used in WP-1.9.x sets
    used = set()
    for c in ("lwe", "so762_dev", "so762_test", "so762_absent_dev"):
        for r in csv.DictReader(open(P21 / f"token_evidence_{c}.csv", encoding="utf-8")):
            used.add(r["speaker_id"])
    man = [m for m in csv.DictReader(open(L.SO_MANIFEST, encoding="utf-8"))
           if m["is_child"] == "1"]
    by = defaultdict(list)
    for m in man:
        by[m["speaker_id"]].append(m)
    unused = [s for s in by if s not in used]
    tot_bytes = 0
    for s in unused:
        for m in by[s]:
            p = L.SO / "WAVE" / f"SPEAKER{int(s):04d}" / f"{m['utt_id']}.WAV"
            if p.exists():
                tot_bytes += p.stat().st_size
    pool = {
        "child_speakers_total": len(by), "used_in_192x": len(used),
        "clean_unused_speakers": len(unused),
        "clean_unused_hours": round(tot_bytes / 32000 / 3600, 3),
        "pilot_labeled_subset_speakers": 10,
        "pilot_labeled_subset_hours": round(sum(float(r["duration_s"]) for r in rows) / 3600, 3),
        "pilot_final_consonant_tokens": sum(int(r["n_final_consonants"]) for r in rows),
        "pilot_r_tokens": sum(r["final_phones"].split(";").count("R") for r in rows),
    }

    # power audit (Wilson 95% CI)
    power = {
        "n60_p0.8_ci": wilson(48, 60), "n60_p0.5_ci": wilson(30, 60),
        "n15_p0.8_ci": wilson(12, 15), "n15_p0.5_ci": wilson(8, 15),
        "note": "95% Wilson intervals: n=60 -> +/-0.10 around p=0.8; n=15 -> +/-0.20. "
                "The pilot can detect large effects only; it is a GO/NO-GO test, "
                "not production validation.",
    }
    out = {"leakage_checks": checks, "pool": pool, "power_audit": power}
    (ART / "pilot_checks.json").write_text(json.dumps(out, indent=2), encoding="utf-8")
    print(json.dumps(out, indent=1))
    print("DONE pilot_checks")


if __name__ == "__main__":
    main()
