"""LWE Active vocab phoneme coverage from CMUdict."""
from __future__ import annotations
import json
import sys
from pathlib import Path
from collections import defaultdict

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
from Research.Speech.Phase1_2.Adapters.paths import RESULTS, strip_stress

# Active-ish LWE words from Phase 1.1 corpus + known active set
WORDS = [
    "apple", "ball", "red", "blue", "one", "please", "teddy", "cat", "dog",
    "book", "big", "small", "open", "close", "banana", "milk", "bread",
    "basket", "bag", "find", "bring", "help", "thank", "two", "three",
]


def main():
    t = CmuDictTargetAdapter()
    phone_words = defaultdict(list)
    missing = []
    rows = []
    for w in WORDS:
        st = t.build(w)
        if st.warnings:
            missing.append(w)
            continue
        bases = [strip_stress(p) for p in st.arpabet]
        rows.append({"word": w, "arpa": st.arpabet, "ipa": st.ipa})
        for i, b in enumerate(bases):
            pos = "initial" if i == 0 else ("final" if i == len(bases) - 1 else "medial")
            phone_words[b].append({"word": w, "pos": pos})
    cov = {
        "n_words": len(WORDS),
        "n_resolved": len(rows),
        "missing": missing,
        "n_unique_phones": len(phone_words),
        "phones": {k: v for k, v in sorted(phone_words.items())},
        "words": rows,
    }
    RESULTS.mkdir(parents=True, exist_ok=True)
    (RESULTS / "lwe_phone_coverage.json").write_text(json.dumps(cov, indent=2), encoding="utf-8")
    print("phones", len(phone_words), "missing", missing, flush=True)


if __name__ == "__main__":
    main()
