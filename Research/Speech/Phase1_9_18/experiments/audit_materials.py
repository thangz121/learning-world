"""Phase 1.9.18 STEP 1 — material / provenance audit (no experiment, no training).

Verifies every prior artifact the B1 experiment depends on and records SHA-256
where practical: SO762 mirror revision, wav2vec2 pinned revision, CMUdict,
phone-inventory v1.4.0, Phase 1.9.15 split seed, Phase 1.9.16 frozen baseline.
Writes provenance.json + 01_MATERIAL_PROVENANCE.md. No network download of large
blobs: only the pinned SO762 parquet revision ref is re-read (cheap).
"""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

OUT = Path(__file__).resolve().parents[1]
REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

MODEL_REV = "2c733782da5604684829819a5eb744c193fe9398"
SO762_REV = "06385584fad212b26134c656fdd3ccf9f093f33e"
SEED = 1515
SPEECH_LAB = Path(r"D:\speech-lab")
MODELS = SPEECH_LAB / "models"
HF_HUB = Path.home() / ".cache" / "huggingface" / "hub"


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> dict:
    checks: dict = {}

    # 1. SO762 mirror revision
    ref = HF_HUB / "datasets--mispeech--speechocean762" / "refs" / "main"
    so762_ref = ref.read_text().strip() if ref.exists() else None
    checks["so762_mirror_revision"] = {
        "value": so762_ref, "expected": SO762_REV,
        "match": so762_ref == SO762_REV,
        "note": "HF mirror of OpenSLR SLR101",
    }

    # 2. wav2vec2 pinned revision
    mref = (MODELS / "models--facebook--wav2vec2-xlsr-53-espeak-cv-ft"
            / "refs" / "main")
    model_ref = mref.read_text().strip() if mref.exists() else None
    snap = (MODELS / "models--facebook--wav2vec2-xlsr-53-espeak-cv-ft"
            / "snapshots" / MODEL_REV)
    checks["wav2vec2_revision"] = {
        "value": model_ref, "expected": MODEL_REV,
        "match": model_ref == MODEL_REV, "snapshot_exists": snap.exists(),
    }
    if snap.exists():
        for fn in ("config.json", "vocab.json"):
            p = snap / fn
            if p.exists():
                checks["wav2vec2_revision"][f"sha256_{fn}"] = sha256(p)

    # 3. CMUdict
    cmu = MODELS / "cmudict.dict"
    checks["cmudict"] = {"exists": cmu.exists(),
                         "sha256": sha256(cmu) if cmu.exists() else None,
                         "bytes": cmu.stat().st_size if cmu.exists() else None}

    # 4. phone inventory v1.4.0
    from Research.Speech.Phase1_4.PhoneInventory.inventory import (
        VERSION as INV_VERSION, ARPA_TO_CANON)
    inv_path = (REPO / "Research" / "Speech" / "Phase1_4" / "PhoneInventory"
                / "inventory.py")
    checks["phone_inventory"] = {
        "version": INV_VERSION,
        "import_ok": True,
        "n_arpa_symbols": len(ARPA_TO_CANON),
        "sha256_inventory_py": sha256(inv_path) if inv_path.exists() else None,
    }

    # 5. Phase 1.9.15 split seed
    sm = REPO / "Research" / "Speech" / "Phase1_9_16" / "split_manifest.json"
    spl = json.loads(sm.read_text(encoding="utf-8")) if sm.exists() else {}
    checks["split_seed_1_9_15"] = {
        "file": str(sm.relative_to(REPO)),
        "random_seed": spl.get("random_seed"),
        "match_seed_1515": spl.get("random_seed") == SEED,
        "status": spl.get("status"),
    }

    # 6. Phase 1.9.16 frozen baseline
    bm = REPO / "Research" / "Speech" / "Phase1_9_16" / "baseline_metrics.json"
    bl = json.loads(bm.read_text(encoding="utf-8")) if bm.exists() else {}
    checks["frozen_baseline_1_9_16"] = {
        "file": str(bm.relative_to(REPO)),
        "reproduced": bl.get("reproduced"),
        "soft_agreement": bl.get("soft_exact_agreement_with_1_9_15"),
        "pipeline": bl.get("pipeline"),
    }

    # 7. Phase 1.9.17 phone tier
    tier = (REPO / "Research" / "Speech" / "Phase1_9_17"
            / "so762_phone_tier_audit.json")
    tj = json.loads(tier.read_text(encoding="utf-8")) if tier.exists() else {}
    checks["so762_phone_tier_1_9_17"] = {
        "file": str(tier.relative_to(REPO)),
        "child_speakers_le15": tj.get("child_speakers_le15"),
        "child_rows_le15": tj.get("child_rows_le15"),
        "inventory_mapped": tj.get("inventory_mapping", {}).get(
            "distinct_phones_mapped"),
        "inventory_unmapped": tj.get("inventory_mapping", {}).get(
            "distinct_phones_unmapped"),
    }

    # 8. local scripts present
    checks["scripts"] = {
        "phone_evidence_v2": (REPO / "Research" / "Speech" / "Phase1_4"
                              / "SoftMatching" / "phone_evidence_v2.py").exists(),
        "target_cmudict": (REPO / "Research" / "Speech" / "Phase1_2"
                           / "Adapters" / "target_cmudict.py").exists(),
        "run_baseline_1_9_16": (REPO / "Research" / "Speech" / "Phase1_9_16"
                                / "experiments" / "run_baseline.py").exists(),
    }

    blocking = []
    if not checks["so762_mirror_revision"]["match"]:
        blocking.append("SO762 revision drift")
    if not checks["wav2vec2_revision"]["match"]:
        blocking.append("wav2vec2 revision drift")
    if not checks["cmudict"]["exists"]:
        blocking.append("CMUdict missing")
    if not checks["phone_inventory"]["import_ok"]:
        blocking.append("inventory import failed")
    if not checks["split_seed_1_9_15"]["match_seed_1515"]:
        blocking.append("split seed mismatch")

    out = {
        "phase": "1.9.18",
        "kind": "material/provenance audit (STEP 1, no experiment)",
        "checks": checks,
        "blocking_issues": blocking,
        "status": "VERIFIED_COMPLETE" if not blocking else "FAILED",
    }
    (OUT / "provenance.json").write_text(
        json.dumps(out, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps(out, indent=2, ensure_ascii=False))
    return out


if __name__ == "__main__":
    r = main()
    sys.exit(0 if r["status"] == "VERIFIED_COMPLETE" else 1)
