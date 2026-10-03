"""Phase 1.9.16 phone inventory verification (no training).

Cross-checks: espeak-CTC vocab (pinned snapshot) <-> PhoneEvidenceV2 inventory
adapter (phone-inventory-v1.4.0) <-> CMUdict ARPAbet <-> LWE control-word targets.
Writes PHONE_INVENTORY_VERIFIED.json next to this script's output dir.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_2.Adapters.paths import HF_CACHE  # noqa: E402
from Research.Speech.Phase1_4.PhoneInventory.inventory import (  # noqa: E402
    PhonemeInventoryAdapter, VERSION as INV_VERSION)

MODEL_ID = "facebook/wav2vec2-xlsr-53-espeak-cv-ft"
CONTROL_WORDS = ["red", "cat", "apple", "blue", "big", "book", "dog"]


def load_cmudict(path: Path) -> dict:
    d: dict = {}
    with open(path, encoding="latin-1") as f:
        for line in f:
            if not line.strip() or line.startswith(";;;"):
                continue
            parts = line.strip().split()
            word = parts[0].lower().rstrip("(0123456789)")
            if word not in d:
                d[word] = [p for p in parts[1:] if p != "/"]
    return d


def main() -> dict:
    from huggingface_hub import hf_hub_download
    vp = hf_hub_download(MODEL_ID, "vocab.json", cache_dir=str(HF_CACHE))
    vocab = json.loads(Path(vp).read_text(encoding="utf-8"))
    assert len(vocab) == 392, f"vocab size drift: {len(vocab)}"

    adapter = PhonemeInventoryAdapter()
    symbols = sorted(vocab.keys())
    mapped, unknown = [], []
    for s in symbols:
        n = adapter.normalize_symbol(s)
        (mapped if n else unknown).append({"source": s, "canon": n})

    cmu = load_cmudict(Path(str(HF_CACHE)) / "cmudict.dict")
    ctrl = {}
    for w in CONTROL_WORDS:
        arpa = cmu.get(w, [])
        canon = adapter.arpa_seq_to_canon(arpa)
        ctrl[w] = {"arpa": arpa, "canon": canon, "covered": bool(arpa)}
    ctrl["red apple"] = {
        "arpa": [cmu.get("red", []), cmu.get("apple", [])],
        "canon": [adapter.arpa_seq_to_canon(cmu.get("red", [])),
                  adapter.arpa_seq_to_canon(cmu.get("apple", []))],
        "covered": bool(cmu.get("red") and cmu.get("apple")),
    }

    out = {
        "inventory_version": INV_VERSION,
        "model_vocab_size": len(vocab),
        "mapped_symbols": len(mapped),
        "unmapped_empty_symbols": len(unknown),
        "unmapped_list": unknown,
        "control_words": ctrl,
        "all_control_covered": all(v["covered"] for v in ctrl.values()),
        "status": ("VERIFIED_COMPLETE"
                   if all(v["covered"] for v in ctrl.values()) else "FAILED"),
    }
    dest = (REPO / "Research" / "Speech" / "Phase1_9_16"
            / "PHONE_INVENTORY_VERIFIED.json")
    dest.write_text(json.dumps(out, ensure_ascii=False, indent=2),
                    encoding="utf-8")
    summary = {k: v for k, v in out.items() if k != "unmapped_list"}
    summary["control_words"] = {
        w: {"covered": v["covered"], "n_phones": len(v["canon"])}
        for w, v in ctrl.items()
    }
    print(json.dumps(summary, indent=2, ensure_ascii=True))
    print("unmapped_symbols:", len(unknown))
    return out


if __name__ == "__main__":
    r = main()
    sys.exit(0 if r["status"] == "VERIFIED_COMPLETE" else 1)
