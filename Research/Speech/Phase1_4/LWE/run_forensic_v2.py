"""Phase 1.4 LWE forensic re-test: hard scorer-v1 vs soft phone evidence v2."""
from __future__ import annotations
import json, sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Adapters.paths import PHASE11_AUDIO
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
from Research.Speech.Phase1_4.PhoneInventory.inventory import dump_mapping_table

OUT = REPO / "Research" / "Speech" / "Phase1_4" / "Results"
OUT.mkdir(parents=True, exist_ok=True)
INV = REPO / "Research" / "Speech" / "Phase1_4" / "PhoneInventory"

WORDS = [
    ("sapi_red.wav", "red"),
    ("sapi_blue.wav", "blue"),
    ("pregen_apple_normal.wav", "apple"),
    ("sapi_book.wav", "book"),
    ("sapi_big.wav", "big"),
    ("sapi_dog.wav", "dog"),
    ("sapi_cat.wav", "cat"),
    ("sapi_red_apple.wav", "red apple"),
    ("stress_silence.wav", "red"),
]


def main():
    dump_mapping_table(INV / "mapping_table.json")
    pipe = SpeakingPipeline(enable_openpronounce=True)
    pev = PhoneEvidenceV2()
    # load old forensics for comparison
    old_path = REPO / "Research" / "Speech" / "Phase1_3" / "Forensics" / "lwe_forensics.json"
    old = {r["target"]: r for r in json.loads(old_path.read_text(encoding="utf-8"))} if old_path.exists() else {}

    rows = []
    for fn, word in WORDS:
        r = pipe.run(str(PHASE11_AUDIO / fn), word)
        s1 = r.scores["scorer_v1"]
        op = r.scores.get("openpronounce")
        soft = pev.soft_match(str(PHASE11_AUDIO / fn), r.target.arpabet)
        old_r = old.get(word.split()[0] if word != "red apple" else "apple", old.get(word, {}))
        # for red apple compare to previous red_apple if any - use current word key
        old_score = None
        for k, v in old.items():
            if v.get("file") == fn:
                old_score = v.get("score")
                break
        row = {
            "file": fn, "target": word,
            "asr": r.asr.text,
            "target_arpa": r.target.arpabet,
            "target_canon": [h.expected for h in soft.hits],
            "old_s1_score": old_score if old_score is not None else s1.score_0_100,
            "s1_score": s1.score_0_100, "s1_conf": s1.confidence_0_1, "s1_per": s1.per,
            "soft_score": soft.soft_score_0_100, "soft_conf": soft.confidence_0_1,
            "soft_mean_sim": round(soft.mean_sim, 3),
            "soft_mean_post": round(soft.mean_posterior, 3),
            "soft_hits": [
                {"exp": h.expected, "obs": h.best_obs, "type": h.match_type,
                 "sim": round(h.sim, 3), "post": round(h.posterior, 3),
                 "t0": round(h.start_s, 3), "t1": round(h.end_s, 3),
                 "topk": h.topk[:3]}
                for h in soft.hits
            ],
            "op_score": op.score_0_100 if op else None,
            "op_warnings": op.warnings if op else None,
            "conf_reasons": soft.conf_reasons,
            "alignment_method": soft.alignment_method,
            "inventory_version": soft.inventory_version,
            # ensemble simple: if OP available and no hard fail, average with soft
            "ensemble_soft_op": None,
        }
        if op and "openpronounce_cli_not_found" not in (op.warnings or []) and "no_json" not in "".join(op.warnings or []):
            # disagreement detector
            if abs(soft.soft_score_0_100 - op.score_0_100) > 30:
                # prefer lower confidence blend
                ens = 0.6 * soft.soft_score_0_100 + 0.4 * op.score_0_100
                row["ensemble_note"] = "disagreement>30_weighted_0.6soft"
            else:
                ens = 0.5 * soft.soft_score_0_100 + 0.5 * op.score_0_100
                row["ensemble_note"] = "agree_mean"
            row["ensemble_soft_op"] = round(ens, 1)
        rows.append(row)
        print(
            f"LWE4 {word:12s} s1={s1.score_0_100:5.1f} soft={soft.soft_score_0_100:5.1f} "
            f"op={row['op_score']} sim={soft.mean_sim:.2f} post={soft.mean_posterior:.2f}",
            flush=True,
        )

    (OUT / "lwe_forensic_v2.json").write_text(json.dumps(rows, ensure_ascii=False, indent=2), encoding="utf-8")
    # summary table
    summary = []
    for r in rows:
        summary.append({
            "word": r["target"], "asr": r["asr"],
            "old_s1": r["old_s1_score"], "new_s1": r["s1_score"],
            "soft": r["soft_score"], "op": r["op_score"],
            "ensemble": r["ensemble_soft_op"],
            "evidence_quality": r["soft_mean_post"],
        })
    (OUT / "lwe_forensic_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print("WROTE", OUT / "lwe_forensic_v2.json", flush=True)


if __name__ == "__main__":
    main()
