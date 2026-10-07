"""WP-1.9.25 Phase 0/4/6 — evidence map, /r/ cases, local corpus stats.

Reads existing artifacts only (WP-1.9.21/1.9.23/1.9.24 + local corpora manifests).
No downloads, no encoder runs, no training.
"""
from __future__ import annotations

import csv
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1] / "Phase1_9_21" / "experiments"))
import p21_lib as L  # noqa: E402

OUT = L.REPO / "Research/Speech/Phase1_9_25"
ART = OUT / "artifacts"
P23 = L.REPO / "Research/Speech/Phase1_9_23"
P24 = L.REPO / "Research/Speech/Phase1_9_24"
SO = L.SO
SO_MANIFEST = L.SO_MANIFEST

R_FIELDS = [
    "token_id", "corpus", "speaker_id", "age", "word", "target_phone",
    "label_source", "human_label_or_score", "human_confidence", "max_A",
    "temporal_support_D", "cluster_width_D", "longest_run_05", "identity_class",
    "production_decision", "alt_encoder_max_A", "notes",
]


def num(v, d=0.0):
    try:
        return float(v)
    except (TypeError, ValueError):
        return d


def main():
    feature = list(csv.DictReader(open(P23 / "artifacts/feature_matrix.csv", encoding="utf-8")))
    scores = {}
    for c in ("so762_dev", "so762_test", "so762_absent_dev"):
        for r in csv.DictReader(open(L.REPO / "Research/Speech/Phase1_9_21/artifacts/"
                                     f"frame_cache/token_evidence_{c}.csv", encoding="utf-8")):
            if r["window_type"] == "full":
                scores[r["token_id"]] = r.get("human_score", "")
    alt = {}
    for r in csv.DictReader(open(P24 / "artifacts/alt_encoder_counterfactual.csv",
                                 encoding="utf-8")):
        alt[r["token_id"]] = num(r["alt_max_A"])

    # ---- /r/ cases ----
    r_cases = []
    for r in feature:
        if r["target_phone"] != "ɹ":
            continue
        corpus = r["corpus"]
        if corpus == "lwe":
            src = "WP-1.9.12 blind human review"
            label = r["human_label"] or "(unlabelled)"
            conf = ""
            for h in csv.DictReader(open(L.REPO / "Research/Speech/Phase1_9_12/Results/"
                                         "final_consonant_human_review.csv",
                                         encoding="utf-8-sig")):
                if h["case_id"] == f"fc_{r['token_id']}":
                    conf = h["reviewer_confidence"]
            note = ""
        else:
            src = "so762 expert per-phone score (NOT a listening label)"
            label = f"score={scores.get(r['token_id'], '?')}"
            conf = "N/A"
            note = "age " + (r.get("age") or "?")
        r_cases.append({
            "token_id": r["token_id"], "corpus": corpus,
            "speaker_id": r["speaker_id"], "age": r.get("age", ""),
            "word": r["word"], "target_phone": "ɹ", "label_source": src,
            "human_label_or_score": label, "human_confidence": conf,
            "max_A": r["target_max_A"], "temporal_support_D": r["temporal_support_D"],
            "cluster_width_D": r["cluster_width_D"], "longest_run_05": r["longest_run_05"],
            "identity_class": r["identity_class"],
            "production_decision": r["production_decision"],
            "alt_encoder_max_A": round(alt[r["token_id"]], 5) if r["token_id"] in alt else "",
            "notes": note,
        })
    L.write_rows(OUT / "R_CASES.csv", r_cases, R_FIELDS)
    r_lwe = [r for r in r_cases if r["corpus"] == "lwe"]
    r_so = [r for r in r_cases if r["corpus"] != "lwe"]

    # ---- so762 child subset stats (local file sizes; no decoding) ----
    man = list(csv.DictReader(open(SO_MANIFEST, encoding="utf-8")))
    ch = [m for m in man if m["is_child"] == "1"]
    spk = sorted({m["speaker_id"] for m in ch})
    total_bytes = 0
    n_found = 0
    for m in ch:
        p = SO / "WAVE" / f"SPEAKER{int(m['speaker_id']):04d}" / f"{m['utt_id']}.WAV"
        if p.exists():
            total_bytes += p.stat().st_size
            n_found += 1
    hours = total_bytes / 32000 / 3600  # 16 kHz 16-bit mono
    so_stats = {
        "child_speakers": len(spk), "child_utterances_manifest": len(ch),
        "files_found": n_found, "approx_hours": round(hours, 2),
        "age_counts": dict(Counter(m["age"] for m in ch)),
        "split_counts": dict(Counter(m["split"] for m in ch)),
        "phone_level_scores": "yes (expert per-phone 0/1/2 in scores-detail.json)",
        "ages_4_6_utterances": sum(1 for m in ch if m["age"] in ("4", "5", "6")),
        "ages_4_6_speakers": len({m["speaker_id"] for m in ch if m["age"] in ("4", "5", "6")}),
    }

    # ---- SIAK local metadata ----
    siak = json.loads((L.REPO / "Research/Speech/Phase1_9_14/artifacts/siak/"
                       "siak_metadata.json").read_text(encoding="utf-8"))
    siak_stats = {
        "release": siak["release"]["source"], "license": siak["release"]["license"],
        "total": siak["counts"]["total"], "speakers_total": siak["counts"]["speakers_total"],
        "by_l1": siak["counts"]["by_l1"], "by_age": siak["counts"]["by_age"],
        "age_4_6": siak.get("age_4_6", {}).get("n"),
        "annotation": siak["annotation"],
    }

    # ---- LWE metadata ----
    lwe_meta = json.loads((L.REPO / "Research/Speech/Phase1_9_8/Results/"
                           "zenodo_200495_meta.json").read_text(encoding="utf-8"))
    lwe_stats = {
        "title": lwe_meta["metadata"]["title"],
        "license": lwe_meta["metadata"]["license"]["id"],
        "access": lwe_meta["metadata"]["access_right"],
        "creators": [c["name"] for c in lwe_meta["metadata"]["creators"]][:5],
        "local": "Research/Speech/ExternalData/zenodo_200495",
    }

    # ---- existing label inventory ----
    lwe_labels = list(csv.DictReader(open(L.REPO / "Research/Speech/Phase1_9_12/Results/"
                                          "final_consonant_human_review.csv",
                                          encoding="utf-8-sig")))
    labels = {
        "lwe_blind_labels": len(lwe_labels),
        "lwe_present": sum(1 for r in lwe_labels if "PRESENT" in r["human_final_label"]),
        "lwe_absent": sum(1 for r in lwe_labels if "ABSENT" in r["human_final_label"]),
        "lwe_confidence": dict(Counter(r["reviewer_confidence"] for r in lwe_labels)),
        "lwe_reviewer": dict(Counter(r["reviewer_id"] for r in lwe_labels)),
        "so762_expert_scores": "per-phone 0/1/2 expert scores (not listening labels)",
        "new_labels_this_wp": 0,
    }

    evidence = {
        "phase": "1.9.25",
        "r_cases": {"n_total": len(r_cases), "n_lwe": len(r_lwe), "n_so762": len(r_so),
                    "lwe_speakers": len({r["speaker_id"] for r in r_lwe}),
                    "so762_speakers": len({r["speaker_id"] for r in r_so}),
                    "lwe_labeled_present": sum(1 for r in r_lwe
                                               if "PRESENT" in r["human_label_or_score"]),
                    "lwe_labeled_absent": sum(1 for r in r_lwe
                                              if "ABSENT" in r["human_label_or_score"]),
                    "lwe_unlabelled": sum(1 for r in r_lwe
                                          if r["human_label_or_score"] == "(unlabelled)"),
                    "alt_covered": sum(1 for r in r_cases if r["alt_encoder_max_A"] != ""),
                    "isolated_peak_n": sum(1 for r in r_cases
                                           if int(num(r["cluster_width_D"], 1)) <= 1)},
        "so762_child_subset": so_stats,
        "siak": siak_stats,
        "lwe": lwe_stats,
        "existing_labels": labels,
    }
    (ART / "evidence_map.json").write_text(json.dumps(evidence, indent=2, ensure_ascii=False),
                                           encoding="utf-8")
    print("R cases:", len(r_cases), "| LWE", len(r_lwe), "| so762", len(r_so))
    print("so762 child:", so_stats["child_speakers"], "speakers",
          so_stats["child_utterances_manifest"], "utt", so_stats["approx_hours"], "h")
    print("SIAK:", siak_stats["total"], "utts,", siak_stats["speakers_total"], "speakers")
    print("DONE build_evidence_map")


if __name__ == "__main__":
    main()
