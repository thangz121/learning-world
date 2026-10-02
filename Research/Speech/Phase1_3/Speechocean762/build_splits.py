"""Speechocean762 schema + speaker-disjoint split (adult L2, NOT 4yo)."""
from __future__ import annotations
import json, hashlib
from pathlib import Path
from collections import defaultdict

SO = Path(r"D:\speech-lab\data\speechocean762")
OUT = Path(__file__).resolve().parents[1] / "Speechocean762"


def load_index():
    scores = json.load(open(SO / "resource" / "scores.json", encoding="utf-8"))
    detail = json.load(open(SO / "resource" / "scores-detail.json", encoding="utf-8"))
    utt_to_spk = {}
    utt_to_wav = {}
    for split in ("train", "test"):
        with open(SO / split / "wav.scp", encoding="utf-8") as f:
            for line in f:
                uid, rel = line.strip().split("\t")
                spk = rel.split("/")[1]
                utt_to_spk[uid] = spk
                utt_to_wav[uid] = str(SO / rel)
    return scores, detail, utt_to_spk, utt_to_wav


def speaker_disjoint_split(utt_to_spk, seed="phase1.3-so762-v1"):
    # deterministic bucket by speaker id hash
    spk_bucket = {}
    for spk in sorted(set(utt_to_spk.values())):
        h = int(hashlib.md5((seed + spk).encode()).hexdigest(), 16) % 100
        if h < 60:
            spk_bucket[spk] = "train"
        elif h < 80:
            spk_bucket[spk] = "valid"
        else:
            spk_bucket[spk] = "test"
    utt_split = {u: spk_bucket[s] for u, s in utt_to_spk.items()}
    return utt_split, spk_bucket


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    scores, detail, utt_to_spk, utt_to_wav = load_index()
    utt_split, spk_bucket = speaker_disjoint_split(utt_to_spk)

    # schema notes
    sample_uid = next(iter(scores))
    w0 = scores[sample_uid]["words"][0]
    schema = {
        "population": "adult-L2-non-native-English",
        "NOT_4yo": True,
        "license": "CC BY 4.0 OpenSLR 101",
        "n_utterances": len(scores),
        "n_speakers": len(set(utt_to_spk.values())),
        "utt_fields": list(scores[sample_uid].keys()),
        "word_fields": list(w0.keys()),
        "word_accuracy_scale": "0-10 (human)",
        "phones_accuracy_scale": "0-2 per phone (human)",
        "detail_has_5_annotators": True,
        "sample_utt": sample_uid,
        "sample_word": w0,
        "split_seed": "phase1.3-so762-v1",
        "split_rule": "speaker hash md5 % 100 -> train60/valid20/test20",
    }
    counts = defaultdict(int)
    spk_counts = defaultdict(set)
    for u, sp in utt_split.items():
        counts[sp] += 1
        spk_counts[sp].add(utt_to_spk[u])
    schema["split_utt_counts"] = dict(counts)
    schema["split_speaker_counts"] = {k: len(v) for k, v in spk_counts.items()}

    # verify no speaker leakage
    leakage = (
        spk_counts["train"] & spk_counts["valid"]
        | spk_counts["train"] & spk_counts["test"]
        | spk_counts["valid"] & spk_counts["test"]
    )
    schema["speaker_leakage"] = sorted(leakage)
    assert not leakage, leakage

    # word-level table index (metadata only, no scores yet)
    words = []
    for uid, meta in scores.items():
        for wi, w in enumerate(meta.get("words", [])):
            words.append({
                "uid": uid,
                "word_i": wi,
                "text": w.get("text"),
                "human_acc_0_10": w.get("accuracy"),
                "human_total_0_10": w.get("total"),
                "phones": w.get("phones"),
                "phones_accuracy": w.get("phones-accuracy"),
                "speaker": utt_to_spk.get(uid),
                "split": utt_split.get(uid),
                "wav": utt_to_wav.get(uid),
                "utt_text": meta.get("text"),
                "utt_total_0_10": meta.get("total"),
            })
    (OUT / "schema.json").write_text(json.dumps(schema, indent=2), encoding="utf-8")
    (OUT / "utt_split.json").write_text(json.dumps(utt_split, indent=2), encoding="utf-8")
    (OUT / "word_index.json").write_text(json.dumps(words), encoding="utf-8")  # compact
    print(json.dumps(schema, indent=2), flush=True)
    print("n_words", len(words), flush=True)


if __name__ == "__main__":
    main()
