"""WP-1.9.29 Part 2 — review infrastructure health check (research-only, safe).

Re-verifies, without touching any genuine review data:
  1. every hash recorded in Phase1_9_28/01_FREEZE_CHECK/FROZEN_CONFIG_HASHES.txt
  2. Pack P blind server: served count, blind payload, submit + duplicate behavior,
     export, QA record cleanup
  3. candidate-to-feature mapping (Pack P token_id == pilot_features.csv token_id)
  4. audio availability for every Pack P candidate
  5. frozen split invariants (seed 1927, 6/2/2, 200 utts, 546 tokens)

Writes artifacts/infra_checks_1929.json. No labels are created; QA records are deleted.
"""
from __future__ import annotations

import csv
import json
import subprocess
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
PHASE = HERE.parent
REPO = PHASE.parents[2]
PY = r"D:\speech-lab\venvs\p0\Scripts\python.exe"
SERVER = REPO / "Research/Speech/Phase1_9_27/experiments/serve_review_1927.py"
HASHES = REPO / "Research/Speech/Phase1_9_28/01_FREEZE_CHECK/FROZEN_CONFIG_HASHES.txt"
PACK_P = REPO / "Research/Speech/Phase1_9_27/06_PILOT_DATA/PILOT_REVIEW_CANDIDATES.csv"
FEATURES = REPO / "Research/Speech/Phase1_9_27/artifacts/pilot_features.csv"
SPLIT = REPO / "Research/Speech/Phase1_9_27/artifacts/pilot_split.json"
MANIFEST = REPO / "Research/Speech/Phase1_9_27/06_PILOT_DATA/PILOT_DATA_MANIFEST.csv"
REVIEWS = REPO / "Research/Speech/Phase1_9_27/artifacts/reviews"
PORT = 8794
QA_REV = "QA-INFRA-1929"


def sha256(p: Path) -> str:
    import hashlib
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest().upper()


def check_hashes():
    results = []
    for line in HASHES.read_text(encoding="utf-8-sig").splitlines():
        line = line.strip()
        if not line or line.startswith("#"):
            continue
        parts = [x for x in line.split("  ") if x]
        if len(parts) < 2:
            continue
        expected, rel = parts[0], parts[1]
        p = REPO / rel
        actual = sha256(p) if p.exists() else "MISSING"
        results.append({"path": rel, "expected": expected, "actual": actual,
                        "match": expected == actual})
    return results


def server_checks():
    out = {"served": 0, "blind_fields": [], "submit_status": None,
           "duplicate_status": None, "export_rows": None, "cleanup": False}
    proc = subprocess.Popen([PY, str(SERVER), str(PORT), "--pack", str(PACK_P)],
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        time.sleep(2.5)

        def get(path):
            with urllib.request.urlopen(f"http://127.0.0.1:{PORT}{path}", timeout=30) as r:
                return json.loads(r.read().decode("utf-8"))

        pack = get(f"/api/pack?reviewer={QA_REV}")
        out["served"] = len(pack)
        out["blind_fields"] = sorted(pack[0].keys())
        bid = pack[0]["blind_id"]

        def post(obj):
            req = urllib.request.Request(
                f"http://127.0.0.1:{PORT}/api/submit", method="POST",
                data=json.dumps(obj).encode("utf-8"),
                headers={"Content-Type": "application/json"})
            try:
                with urllib.request.urlopen(req, timeout=30) as r:
                    return r.status
            except urllib.error.HTTPError as e:
                return e.code

        out["submit_status"] = post({"reviewer_id": QA_REV, "blind_id": bid,
                                     "label": "UNCERTAIN", "confidence": "LOW", "ts": "qa"})
        out["duplicate_status"] = post({"reviewer_id": QA_REV, "blind_id": bid,
                                        "label": "PRESENT", "confidence": "HIGH", "ts": "qa"})
        out["export_rows"] = len(get(f"/api/export?reviewer={QA_REV}"))
    finally:
        proc.terminate()
        qa = REVIEWS / f"{QA_REV}.jsonl"
        if qa.exists():
            qa.unlink()
        out["cleanup"] = not qa.exists()
    return out


def mapping_checks():
    pack = list(csv.DictReader(open(PACK_P, encoding="utf-8")))
    feats = {r["token_id"] for r in csv.DictReader(open(FEATURES, encoding="utf-8"))}
    pack_ids = {r["token_id"] for r in pack}
    missing_audio = [r["audio_reference"] for r in pack
                     if not Path(r["audio_reference"]).exists()]
    split = json.loads(SPLIT.read_text(encoding="utf-8"))
    man = list(csv.DictReader(open(MANIFEST, encoding="utf-8")))
    by_split = {k: 0 for k in ("train", "dev", "test")}
    for r in pack:
        by_split[r["split"]] = by_split.get(r["split"], 0) + 1
    return {
        "pack_candidates": len(pack),
        "feature_tokens": len(feats),
        "token_match": pack_ids == feats,
        "missing_audio": len(missing_audio),
        "seed": split.get("seed"),
        "frozen": split.get("frozen"),
        "splits": {k: len(v) for k, v in split["split"].items()},
        "pack_by_split": by_split,
        "manifest_rows": len(man),
        "manifest_pass": sum(1 for r in man if r["qc_status"] == "PASS"),
    }


def main():
    out = {
        "hashes": check_hashes(),
        "server_pack_p": server_checks(),
        "mapping": mapping_checks(),
    }
    out["hashes_all_match"] = all(h["match"] for h in out["hashes"])
    s = out["server_pack_p"]
    out["server_pass"] = (s["served"] == 546 and s["blind_fields"] == ["blind_id", "target_phone", "word"]
                          and s["submit_status"] == 200 and s["duplicate_status"] == 409
                          and s["export_rows"] == 1 and s["cleanup"])
    m = out["mapping"]
    out["mapping_pass"] = (m["token_match"] and m["missing_audio"] == 0
                           and m["manifest_rows"] == 200 and m["manifest_pass"] == 200
                           and m["seed"] == 1927 and m["frozen"] is True
                           and m["splits"] == {"train": 6, "dev": 2, "test": 2}
                           and m["pack_by_split"] == {"train": 337, "dev": 110, "test": 99})
    out["REVIEW_INFRASTRUCTURE"] = "PASS" if (out["hashes_all_match"] and out["server_pass"]
                                              and out["mapping_pass"]) else "FAIL"
    (PHASE / "artifacts" / "infra_checks_1929.json").write_text(
        json.dumps(out, indent=2), encoding="utf-8")
    print(json.dumps({"hashes_all_match": out["hashes_all_match"],
                      "server_pass": out["server_pass"],
                      "mapping_pass": out["mapping_pass"],
                      "REVIEW_INFRASTRUCTURE": out["REVIEW_INFRASTRUCTURE"]}, indent=1))
    print("DONE infra_health_1929")


if __name__ == "__main__":
    main()
