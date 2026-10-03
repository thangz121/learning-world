"""Phase 1.9.14 — reproducibility manifest: hashes of outputs + frozen inputs."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
OUT = REPO / "Research/Speech/Phase1_9_14"


def sha256_file(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest().upper()


def main():
    outputs = {}
    for p in sorted(OUT.rglob("*")):
        if p.is_file() and p.name != "phase_manifest.json":
            outputs[str(p.relative_to(OUT)).replace("\\", "/")] = {
                "sha256": sha256_file(p), "bytes": p.stat().st_size}
    frozen = {}
    for rel in [
        "Research/Speech/Phase1_9_12/Results/final_consonant_human_vs_model.csv",
        "Research/Speech/Phase1_9_12/Results/final_consonant_human_review.csv",
        "Research/Speech/Phase1_9_12/Results/phase_1_9_12_master.json",
        "Research/Speech/Phase1_9_11/Results/scorer_miss_human_review.csv",
        "Research/Speech/Phase1_9_11/Results/window_human_spot_check.csv",
        "Research/Speech/Phase1_9_10/Results/human_second_pass.csv",
        "Research/Speech/Phase1_9_9/Results/human_review_results.csv",
        "Research/Speech/Phase1_9_9/Results/asr_phone_conflicts.csv",
        "Research/Speech/Phase1_9_8/Results/pronunciation_results.csv",
        "Research/Speech/Phase1_9_8/Results/vad_results.csv",
        "Research/Speech/Phase1_9_8/Results/recording_inventory.csv",
        "Research/Speech/ExternalData/SIAK/train.csv",
        "Research/Speech/ExternalData/SIAK/test.csv",
        "Research/Speech/ExternalData/SIAK/README.md",
    ]:
        p = REPO / rel
        frozen[rel] = sha256_file(p) if p.exists() else "MISSING"
    manifest = {
        "phase": "1.9.14",
        "title": "fidelity/assessability + deletion-aware pronunciation research",
        "base_head": "b4a140e (local) · phase1.9.13 commit 3ab6c01",
        "flags": {"production_vad": False, "router_locked": False, "unity_integrated": False,
                  "scorer_modified": False, "production_window_locked": False},
        "model_used": {
            "id": "facebook/wav2vec2-xlsr-53-espeak-cv-ft",
            "license": "Apache-2.0 (verified 2026-10-02)",
            "cache": "D:/speech-lab/models (gitignored)",
        },
        "dates": "2026-10-03",
        "outputs": outputs,
        "frozen_inputs": frozen,
    }
    (OUT / "manifests" / "phase_manifest.json").write_text(
        json.dumps(manifest, indent=2), encoding="utf-8")
    print(f"manifest written: {len(outputs)} outputs, {len(frozen)} frozen inputs")


if __name__ == "__main__":
    main()
