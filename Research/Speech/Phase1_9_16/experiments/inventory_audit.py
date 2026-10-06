"""Phase 1.9.16 — phone inventory audit (spec §5).

Compares CMUdict ARPAbet, the model's eSpeak CTC vocabulary, PhoneInventory
canonical symbols (v1.4.0), speechocean762 reference phones and SIAK target
vocabulary. Produces an explicit versioned mapping table; nothing is mapped
silently.
"""
from __future__ import annotations

import csv
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2  # noqa: E402
from Research.Speech.Phase1_4.PhoneInventory.inventory import (  # noqa: E402
    ARPA_TO_CANON, ALIAS_TO_CANON, VERSION)
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter  # noqa: E402

OUT = REPO / "Research/Speech/Phase1_9_16/artifacts/inventory"
OUT.mkdir(parents=True, exist_ok=True)
CMUDICT = Path(r"D:\speech-lab\models\cmudict.dict")
SO_MANIFEST = REPO / "Research/Speech/Phase1_9_16/artifacts/audit/so762_manifest.csv"
SIAK = REPO / "Research/Speech/ExternalData/SIAK"

VOWELS = {"AA", "AE", "AH", "AO", "AW", "AY", "EH", "ER", "EY", "IH", "IY", "OW", "OY", "UH", "UW"}


def base(p):
    return re.sub(r"\d$", "", p)


def main():
    pev = PhoneEvidenceV2()
    pev._ensure()
    vocab_tokens = set(pev._id2tok.values())
    canon_to_ids = {k: v for k, v in pev._canon_ids.items()}

    cmu_phones = Counter()
    for line in CMUDICT.open(encoding="utf-8", errors="ignore"):
        m = re.match(r"^(\S+)\s+(.+)$", line.rstrip())
        if not m:
            continue
        for p in m.group(2).split():
            if re.match(r"^[A-Z]+[0-2]?$", p):  # drop cmudict special-entry noise
                cmu_phones[base(p)] += 1

    so_phones = Counter()
    if SO_MANIFEST.exists():
        for r in csv.DictReader(SO_MANIFEST.open(encoding="utf-8")):
            if r["split"] != "train" or r["is_child"] != "1":
                continue
            for p in r["ref_phones"].split():
                so_phones[base(p)] += 1

    siak_words = set()
    for split in ("train", "test"):
        for r in csv.DictReader((SIAK / f"{split}.csv").open(encoding="utf-8")):
            siak_words.add(r["utterance"])
    tgt = CmuDictTargetAdapter()
    siak_phones = Counter()
    siak_finals = Counter()
    for w in sorted(siak_words):
        try:
            st = tgt.build(w)
        except Exception:  # noqa: BLE001
            continue
        for p in st.arpabet:
            siak_phones[base(p)] += 1
        if st.arpabet:
            siak_finals[base(st.arpabet[-1])] += 1

    rows = []
    all_arpa = sorted(set(cmu_phones) | set(so_phones) | set(siak_phones) | set(ARPA_TO_CANON))
    for p in all_arpa:
        canon = ARPA_TO_CANON.get(p, "")
        ids = canon_to_ids.get(canon, [])
        rows.append({
            "arpabet_base": p,
            "type": "VOWEL" if p in VOWELS else "CONSONANT",
            "canonical": canon,
            "canon_mapped": bool(canon),
            "model_vocab_ids": ";".join(str(i) for i in ids),
            "model_vocab_n": len(ids),
            "in_cmudict": cmu_phones.get(p, 0),
            "in_so762_train_child": so_phones.get(p, 0),
            "in_siak_targets": siak_phones.get(p, 0),
            "siak_final_count": siak_finals.get(p, 0),
            "status": ("OK" if canon and ids else
                       "CANON_ONLY" if canon else "UNMAPPED"),
        })

    with open(OUT / "phone_mapping.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)

    # model vocab tokens that the inventory does not map
    inv_values = set(ARPA_TO_CANON.values()) | set(ALIAS_TO_CANON.values())
    unmapped_vocab = sorted(t for t in vocab_tokens
                            if t not in ("<pad>", "|", "", "<s>", "</s>")
                            and pev.inv.normalize_symbol(t) not in inv_values
                            and pev.inv.normalize_symbol(t) not in canon_to_ids)
    so_unmapped = sorted(set(so_phones) - set(ARPA_TO_CANON))
    summary = {
        "inventory_version": VERSION,
        "model_vocab_size": len(vocab_tokens),
        "canonical_classes": len(canon_to_ids),
        "cmudict_phones": len(cmu_phones),
        "so762_child_train_phones": len(so_phones),
        "siak_target_phones": len(siak_phones),
        "status_counts": dict(Counter(r["status"] for r in rows)),
        "so762_phones_without_canon_mapping": so_unmapped,
        "model_vocab_unmapped_tokens": unmapped_vocab,
        "final_consonants_siak": {k: v for k, v in sorted(siak_finals.items())},
        "note": "all CMUdict/espeak/final-consonant comparisons use base phones "
                "(stress stripped); mappings are from PhoneInventory v1.4.0",
    }
    (OUT / "inventory_summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
