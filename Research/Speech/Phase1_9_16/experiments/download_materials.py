"""Phase 1.9.16 material rebuild — download pinned model + SIAK dataset.

REPRODUCIBLE: pins exact revisions/hashes. Safe to re-run (idempotent).
Writes outside the repo ONLY to D:\\speech-lab\\models (ASUS-compatible HF cache
layout, gitignored by design) and to Research/Speech/ExternalData/SIAK
(gitignored; matches 1.9.15 provenance.json input paths).
"""
from __future__ import annotations

import os
from pathlib import Path

from huggingface_hub import snapshot_download

REPO = Path(__file__).resolve().parents[4]  # .../learning-world
MODELS_DIR = Path(os.environ.get("LWE_SPEECH_LAB", r"D:\speech-lab")) / "models"
SIAK_DIR = REPO / "Research" / "Speech" / "ExternalData" / "SIAK"

MODEL_ID = "facebook/wav2vec2-xlsr-53-espeak-cv-ft"
MODEL_REV = "2c733782da5604684829819a5eb744c193fe9398"  # verified 2026-10-02, Apache-2.0
SIAK_ID = "rkarhila/SIAK"  # CC-BY-ND-4.0; legal review still an open blocker for training


def main() -> None:
    MODELS_DIR.mkdir(parents=True, exist_ok=True)
    SIAK_DIR.mkdir(parents=True, exist_ok=True)

    print(f"[1/2] model {MODEL_ID}@{MODEL_REV} -> {MODELS_DIR}", flush=True)
    mp = snapshot_download(
        repo_id=MODEL_ID, revision=MODEL_REV, cache_dir=str(MODELS_DIR),
        max_workers=8,
    )
    print(f"      snapshot: {mp}", flush=True)

    print(f"[2/2] dataset {SIAK_ID} -> {SIAK_DIR}", flush=True)
    # local_dir (plain working tree: train.csv/test.csv/flac) so that the 1.9.15
    # provenance paths (Research/Speech/ExternalData/SIAK/train.csv) hold verbatim.
    sp = snapshot_download(
        repo_id=SIAK_ID, repo_type="dataset", local_dir=str(SIAK_DIR),
        max_workers=16,
    )
    print(f"      snapshot: {sp}", flush=True)
    print("DONE", flush=True)


if __name__ == "__main__":
    main()
