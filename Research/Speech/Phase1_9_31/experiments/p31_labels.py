"""WP-1.9.31 — derive external expert labels (SO762 5-expert panel).

Outputs (labels only; no model metrics):
  artifacts/labels/expert_labels_pilot.csv        (frozen 546 pilot tokens)
  artifacts/labels/expert_labels_bench_train.csv  (SO762 train children, all final tokens)
  artifacts/labels/expert_labels_bench_test.csv   (SO762 test children, all final tokens)
  artifacts/labels/label_coverage.json

Frozen rule: 00_PREREGISTRATION.md section 3 (PRESENT iff P2+P1>=3; ABSENT iff A0>=3).
"""
from __future__ import annotations

import csv
import hashlib
from collections import Counter, defaultdict

import p31_common as C

FIELDS = ["token_id", "utt_id", "set", "speaker_id", "age", "word", "target_phone",
          "m1", "m2", "m3", "m4", "m5", "P2", "P1", "A0", "label", "strict_correct", "any_zero"]


def marks_for(detail, utt_id, k):
    d = detail.get(utt_id)
    if d is None:
        return None
    for kk, wi, pos, ref, canon, is_final, wtext in C.iter_utt_tokens(d):
        if kk != k:
            continue
        refs = (d["words"][wi].get("ref-phones") or "").split()
        experts = d["words"][wi].get("phones") or []
        per_pos, ok = C.parse_word_marks(experts, len(refs))
        if not ok or pos >= len(per_pos) or len(per_pos[pos]) != 5:
            return {"word": wtext, "phone": canon, "marks": None}
        return {"word": wtext, "phone": canon, "marks": per_pos[pos]}
    return None


def row(token_id, utt_id, set_name, spk, age, info):
    r = {"token_id": token_id, "utt_id": utt_id, "set": set_name, "speaker_id": spk,
         "age": age, "word": info["word"], "target_phone": info["phone"],
         "m1": "", "m2": "", "m3": "", "m4": "", "m5": "", "P2": "", "P1": "", "A0": "",
         "label": "", "strict_correct": "", "any_zero": ""}
    if info["marks"]:
        for i, m in enumerate(info["marks"]):
            r[f"m{i+1}"] = m
        lab, p2, p1, a0 = C.label_for_marks(info["marks"])
        r.update({"P2": p2, "P1": p1, "A0": a0, "label": lab,
                  "strict_correct": int(p2 >= 3), "any_zero": int(a0 >= 1)})
    return r


def main():
    detail = C.load_detail()
    sha = hashlib.sha256(C.SO_DETAIL.read_bytes()).hexdigest().upper()
    print("scores-detail sha256:", sha)

    summary = {"scores_detail_sha256": sha}

    # ---------------- pilot 546 ----------------
    feat = list(csv.DictReader(open(C.P27 / "artifacts/pilot_features.csv", encoding="utf-8")))
    order = [r["token_id"] for r in feat]
    man = {r["utt_id"]: r for r in csv.DictReader(
        open(C.P27 / "06_PILOT_DATA/PILOT_DATA_MANIFEST.csv", encoding="utf-8"))}
    out = []
    malformed = 0
    for token_id in order:
        utt_id, ks = token_id.rsplit("_", 1)
        info = marks_for(detail, utt_id, int(ks))
        if info is None:
            malformed += 1
            continue
        m = man.get(utt_id, {})
        out.append(row(token_id, utt_id, "PILOT_" + m.get("split", ""), m.get("speaker_id", ""),
                       m.get("age", ""), info))
    C.write_csv(C.ART / "labels/expert_labels_pilot.csv", out, FIELDS)
    summary["pilot"] = {"tokens": len(out), "malformed": malformed,
                        "labels": dict(Counter(r["label"] for r in out))}
    summary["pilot_r_tokens"] = sum(1 for r in out if r["target_phone"] == "ɹ")
    del out

    # ---------------- bench train/test ----------------
    for split in ("train", "test"):
        out = []
        malformed = 0
        n_utt = 0
        for spk, age, utts in C.kid_speakers(split):
            for utt in utts:
                d = detail.get(utt)
                if d is None:
                    continue
                n_utt += 1
                for k, wi, pos, ref, canon, is_final, wtext in C.iter_utt_tokens(d):
                    if not is_final or canon in C.VOWELS:
                        continue
                    info = marks_for(detail, utt, k)
                    if info is None or info["marks"] is None:
                        malformed += 1
                        info = info or {"word": wtext, "phone": canon, "marks": None}
                    token_id = f"{utt}_{k}"
                    out.append(row(token_id, utt, f"BENCH_{split}", spk, age, info))
        C.write_csv(C.ART / f"labels/expert_labels_bench_{split}.csv", out, FIELDS)
        labs = Counter(r["label"] for r in out)
        summary[f"bench_{split}"] = {"utterances": n_utt, "tokens": len(out),
                                     "malformed": malformed, "labels": dict(labs)}
        per_phone = defaultdict(Counter)
        r_strict = Counter()
        for r in out:
            if r["label"]:
                per_phone[r["target_phone"]][r["label"]] += 1
            if r["target_phone"] == "ɹ" and r["strict_correct"] != "":
                r_strict["strict_present" if r["strict_correct"] == 1 else "strict_not"] += 1
        summary[f"bench_{split}"]["per_phone"] = {
            ph: dict(c) for ph, c in sorted(per_phone.items(), key=lambda x: -sum(x[1].values()))}
        summary[f"bench_{split}"]["r_strict"] = dict(r_strict)

    # pilot /r/ already counted above
    C.write_json(C.ART / "labels/label_coverage.json", summary)
    print("pilot:", summary["pilot"]["labels"], "malformed:", summary["pilot"]["malformed"])
    for split in ("train", "test"):
        s = summary[f"bench_{split}"]
        print(f"bench_{split}: utts={s['utterances']} tokens={s['tokens']} "
              f"malformed={s['malformed']} labels={s['labels']} r_strict={s['r_strict']}")
    print("DONE p31_labels")


if __name__ == "__main__":
    main()
