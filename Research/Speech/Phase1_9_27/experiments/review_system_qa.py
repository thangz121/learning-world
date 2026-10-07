"""WP-1.9.27 Part 2 — automated QA of the review system (21 checks).

Starts the canonical server (serve_review_1927.py), exercises every endpoint with
a QA reviewer id, verifies blinding/isolation/duplicates/randomization, writes
QA artifacts and deletes all QA submissions. No human labels are created.
"""
from __future__ import annotations

import csv
import hashlib
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
LAB = PHASE.parent / "Phase1_9_26" / "01_HUMAN_LABEL_ACQUISITION"
REVIEWS = PHASE / "artifacts" / "reviews"
QA_DIR = PHASE / "01_REVIEW_SYSTEM"
PY = r"D:\speech-lab\venvs\p0\Scripts\python.exe"
PORT = 8792
QA_REVIEWER = "QA-TOOL-1927"


def sha256(p):
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest().upper()


def get(path, binary=False):
    with urllib.request.urlopen(f"http://127.0.0.1:{PORT}{path}", timeout=30) as r:
        data = r.read()
    return data if binary else json.loads(data.decode("utf-8"))


def post(path, obj, expect=200):
    req = urllib.request.Request(
        f"http://127.0.0.1:{PORT}{path}", data=json.dumps(obj).encode("utf-8"),
        headers={"Content-Type": "application/json"}, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return r.status, json.loads(r.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        try:
            return e.code, json.loads(e.read().decode("utf-8"))
        except Exception:  # noqa: BLE001
            return e.code, {}


def main():
    pack = list(csv.DictReader(open(LAB / "REVIEW_CANDIDATES.csv", encoding="utf-8")))
    checks = []

    def check(cid, name, ok, detail=""):
        checks.append({"check_id": cid, "check": name, "result": "PASS" if ok else "FAIL",
                       "detail": detail})

    check(1, "candidate_count_276", len(pack) == 276, f"n={len(pack)}")
    ids = [r["blind_id"] for r in pack]
    check(2, "unique_candidate_ids", len(set(ids)) == len(ids), f"unique={len(set(ids))}")
    auds = [r["audio_reference"] for r in pack]
    missing = [a for a in auds if not Path(a).exists()]
    check(3, "audio_references_resolve", not missing,
          f"unique_files={len(set(auds))}; missing={len(missing)} "
          f"(SO762 candidates share utterance files by design)")
    check(4, "speaker_metadata", all(r["speaker_id"] for r in pack),
          f"speakers={len({r['speaker_id'] for r in pack})}")
    check(5, "pool_metadata", all(r["pool"] for r in pack),
          f"pools={len({r['pool'] for r in pack})}")
    purp = {r["purpose"] for r in pack}
    check(6, "diagnostic_balanced_random_classification",
          {"diagnostic", "balanced", "random_control"} <= purp, str(sorted(purp)))

    proc = subprocess.Popen([PY, str(HERE / "serve_review_1927.py"), str(PORT)],
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        time.sleep(2.5)
        with urllib.request.urlopen(f"http://127.0.0.1:{PORT}/", timeout=30) as r:
            html = r.read().decode("utf-8")
        p1 = get(f"/api/pack?reviewer={QA_REVIEWER}")
        p2 = get(f"/api/pack?reviewer={QA_REVIEWER}")
        pB = get("/api/pack?reviewer=QA-OTHER-1927")
        fields = sorted(p1[0].keys())
        check(7, "blind_payload_no_model_prediction",
              not any(k in p1[0] for k in ("machine_max_A", "production_decision",
                                           "historical_label", "alt_max_A", "pool")),
              str(fields))
        check(8, "reviewer_cannot_see_another_reviewer",
              get("/api/progress?reviewer=QA-OTHER-1927") == [],
              "fresh reviewer progress empty")
        check(9, "reviewer_cannot_see_previous_labels",
              all("historical_label" not in r for r in p1), "no label field in payload")
        check(10, "labels_available", all(x in html for x in ("PRESENT", "ABSENT", "UNCERTAIN")),
              "PRESENT/ABSENT/UNCERTAIN in UI")
        check(11, "confidence_available", all(x in html for x in ("HIGH", "MEDIUM", "LOW")),
              "HIGH/MEDIUM/LOW in UI")
        check(12, "notes_available", "note" in html.lower(), "note textarea present")
        st, resp = post("/api/submit", {"reviewer_id": QA_REVIEWER, "blind_id": p1[0]["blind_id"],
                                        "label": "UNCERTAIN", "confidence": "LOW",
                                        "assessable": "ASSESSABLE", "note": "QA TEST",
                                        "ts": "qa"})
        check(13, "submit_works", st == 200 and resp.get("ok"), f"status={st}")
        check(14, "resume_works", get(f"/api/progress?reviewer={QA_REVIEWER}") == [p1[0]["blind_id"]],
              "progress returns submitted id")
        exp = get(f"/api/export?reviewer={QA_REVIEWER}")
        check(15, "export_works", len(exp) == 1 and exp[0]["blind_id"] == p1[0]["blind_id"],
              f"rows={len(exp)}")
        st2, resp2 = post("/api/submit", {"reviewer_id": QA_REVIEWER,
                                          "blind_id": p1[0]["blind_id"], "label": "PRESENT",
                                          "confidence": "HIGH", "ts": "qa"})
        check(16, "duplicate_submission_409", st2 == 409 and resp2.get("error") == "duplicate",
              f"status={st2}")
        check(17, "incomplete_review_handling",
              len(get(f"/api/pack?reviewer={QA_REVIEWER}")) == 276
              and len(get(f"/api/progress?reviewer={QA_REVIEWER}")) == 1,
              "pack returns all; progress returns submitted only")
        check(18, "reviewer_isolation", get("/api/progress?reviewer=QA-OTHER-1927") == [],
              "other reviewer sees nothing")
        check(19, "randomization_reproducible",
              [r["blind_id"] for r in p1] == [r["blind_id"] for r in p2]
              and [r["blind_id"] for r in p1] != [r["blind_id"] for r in pB],
              "same reviewer stable; different reviewer different order")
        audio = get(f"/audio/{p1[0]['blind_id']}", binary=True)
        check(21, "no_machine_leakage_in_html",
              not any(k in html for k in ("machine_max_A", "production_decision", "alt_max_A")),
              f"audio_bytes={len(audio)}")
    finally:
        proc.terminate()
        for f in REVIEWS.glob("QA-*.jsonl"):
            f.unlink()
        check(20, "qa_submissions_deleted",
              not any(REVIEWS.glob("QA-*.jsonl")), "QA files removed; no labels fabricated")

    # hashes + manifest copy
    hashes = []
    for f in (LAB / "REVIEW_CANDIDATES.csv", LAB / "REVIEW_DIAGNOSTIC.csv",
              LAB / "REVIEW_RANDOM_CONTROL.csv", LAB / "REVIEW_BALANCED.csv",
              LAB / "REVIEW_PACK_MANIFEST.csv"):
        hashes.append(f"{sha256(f)}  {f.name}  ({f.stat().st_size} bytes)")
    (QA_DIR / "REVIEW_PACK_HASHES.txt").write_text("\n".join(hashes) + "\n", encoding="utf-8")
    import shutil
    shutil.copy(LAB / "REVIEW_PACK_MANIFEST.csv", QA_DIR / "REVIEW_PACK_MANIFEST.csv")

    fields = ["check_id", "check", "result", "detail"]
    with open(QA_DIR / "REVIEW_SYSTEM_QA.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=fields)
        w.writeheader()
        w.writerows(checks)
    n_pass = sum(1 for c in checks if c["result"] == "PASS")
    lines = ["# REVIEW SYSTEM QA — WP-1.9.27", "",
             f"Automated QA of `experiments/serve_review_1927.py` + the 276-candidate pack.",
             f"Result: **{n_pass}/{len(checks)} PASS** (QA submissions deleted; no labels created).",
             "", "| # | check | result | detail |", "|---:|---|---|---|"]
    for c in checks:
        lines.append(f"| {c['check_id']} | {c['check']} | {c['result']} | {c['detail']} |")
    (QA_DIR / "REVIEW_SYSTEM_QA.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"QA {n_pass}/{len(checks)} PASS")
    for c in checks:
        if c["result"] != "PASS":
            print("FAIL", c)
    print("DONE review_system_qa")


if __name__ == "__main__":
    main()
