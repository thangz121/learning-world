"""Phase 1.9.17 SpeechOcean762 phone-tier audit (annotation only, NO audio).

Aggregates the expert phone tier into a machine-readable manifest: coverage,
per-phone accuracy distribution, the observed-substitution tier, child vs adult
population, and mapping of the corpus's ARPAbet canonical phones into the frozen
LWE inventory (phone-inventory-v1.4.0).
"""
from __future__ import annotations

import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

OUT = Path(__file__).resolve().parents[1]
REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

CHILD_AGE_MAX = 15
ACC_BAD = 0.5  # corpus: score < 0.5 => "mispronounced or missed"


def bucket(a: float) -> str:
    if a <= 0.0:
        return "zero"
    if a < ACC_BAD:
        return "below_0.5"
    if a < 1.5:
        return "mid_0.5_1.5"
    if a < 2.0:
        return "high_1.5_2"
    return "perfect_2.0"


def main() -> dict:
    from datasets import load_dataset
    from Research.Speech.Phase1_4.PhoneInventory.inventory import (
        PhonemeInventoryAdapter, VERSION as INV_VERSION)

    adapter = PhonemeInventoryAdapter()
    stats = {
        "rows": 0, "words": 0, "phone_tokens": 0,
        "acc_buckets": Counter(), "acc_buckets_child": Counter(),
        "sub_tokens": 0, "sub_tokens_child": 0,
        "child_rows": 0, "adult_rows": 0,
        "age": Counter(), "child_speakers": set(), "adult_speakers": set(),
    }
    per_phone: dict = defaultdict(
        lambda: {"tokens": 0, "acc_sum": 0.0, "bad": 0, "zero": 0,
                 "canon": None})
    subs = Counter()
    base_phones = Counter()
    unmapped = Counter()
    sub_kind = Counter()

    for split in ("train", "test"):
        ds = load_dataset("mispeech/speechocean762", split=split, streaming=True)
        keep = [c for c in ds.column_names if c != "audio"]
        ds = ds.select_columns(keep)
        for ex in ds:
            stats["rows"] += 1
            is_child = ex["age"] <= CHILD_AGE_MAX
            stats["child_rows" if is_child else "adult_rows"] += 1
            stats["age"][ex["age"]] += 1
            (stats["child_speakers"] if is_child
             else stats["adult_speakers"]).add(ex["speaker"])
            for w in ex["words"]:
                stats["words"] += 1
                for p, a in zip(w["phones"], w["phones-accuracy"]):
                    stats["phone_tokens"] += 1
                    b = bucket(float(a))
                    stats["acc_buckets"][b] += 1
                    if is_child:
                        stats["acc_buckets_child"][b] += 1
                    base = adapter.strip_stress(p)
                    base_phones[base] += 1
                    canon = adapter.normalize_symbol(p, source_kind="arpa")
                    rec = per_phone[p]
                    rec["tokens"] += 1
                    rec["acc_sum"] += float(a)
                    rec["bad"] += int(float(a) < ACC_BAD)
                    rec["zero"] += int(float(a) <= 0.0)
                    rec["canon"] = canon
                    if not canon:
                        unmapped[p] += 1
                for mp in w.get("mispronunciations", []) or []:
                    stats["sub_tokens"] += 1
                    if is_child:
                        stats["sub_tokens_child"] += 1
                    pair = (mp["canonical-phone"], mp["pronounced-phone"])
                    subs[pair] += 1
                    if mp["pronounced-phone"] == "<DEL>":
                        sub_kind["deletion"] += 1
                    elif mp["pronounced-phone"] == "<unk>":
                        sub_kind["unknown"] += 1
                    else:
                        sub_kind["substitution"] += 1

    n = stats["phone_tokens"]
    child_rows = stats["child_rows"]
    child_tokens = sum(stats["acc_buckets_child"].values())
    bad_all = (stats["acc_buckets"]["zero"]
               + stats["acc_buckets"]["below_0.5"])
    bad_child = (stats["acc_buckets_child"]["zero"]
                 + stats["acc_buckets_child"]["below_0.5"])
    mapped = sum(1 for v in per_phone.values() if v["canon"])
    report = {
        "source": "mispeech/speechocean762 (mirror of OpenSLR SLR101)",
        "inventory_version": INV_VERSION,
        "rows_total": stats["rows"],
        "child_rows_le15": child_rows,
        "adult_rows_gt15": stats["adult_rows"],
        "child_speakers_le15": len(stats["child_speakers"]),
        "adult_speakers_gt15": len(stats["adult_speakers"]),
        "words": stats["words"],
        "phone_tokens": n,
        "distinct_canonical_arbabet_phones": len(per_phone),
        "distinct_base_phones": len(base_phones),
        "phone_accuracy_buckets_all": dict(stats["acc_buckets"]),
        "phone_accuracy_buckets_child": dict(stats["acc_buckets_child"]),
        "def_bad": "expert accuracy < 0.5 (= bucket 'zero' + 'below_0.5')",
        "bad_tokens_all": bad_all,
        "bad_token_rate_all": bad_all / n if n else None,
        "child_phone_tokens": child_tokens,
        "bad_tokens_child": bad_child,
        "bad_token_rate_child": bad_child / child_tokens if child_tokens else None,
        "observed_substitution_records": stats["sub_tokens"],
        "observed_substitution_records_child": stats["sub_tokens_child"],
        "distinct_substitution_pairs": len(subs),
        "substitution_kind_counts": dict(sub_kind),
        "top_substitution_pairs": [
            {"canonical": c, "pronounced": p, "n": k}
            for (c, p), k in subs.most_common(20)],
        "inventory_mapping": {
            "distinct_phones_mapped": mapped,
            "distinct_phones_unmapped": len(per_phone) - mapped,
            "unmapped_phones": sorted(unmapped),
        },
        "base_phone_histogram": dict(base_phones.most_common()),
        "phone_tier_type": (
            "MANUAL-EXPERT-SCORED canonical phones (5 experts, per-phone 0/1/2 "
            "averaged) + observed substituted phone recorded only when expert "
            "accuracy < 0.5; NOT a full observed-phone transcription"),
        "license_note": (
            "OpenSLR SLR101 states CC BY 4.0 and free commercial+non-commercial "
            "use; the HF dataset card front-matter instead declares apache-2.0. "
            "Both are permissive; the discrepancy is recorded, not resolved."),
    }
    (OUT / "so762_phone_tier_audit.json").write_text(
        json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(report, indent=2, ensure_ascii=False))
    return report


if __name__ == "__main__":
    main()
