"""WP-1.9.31 — native-child positive reference (Zenodo 200495, CC BY 4.0, ages ~4-5).

Assumption per PO directive: native children's standard productions are correct.
Reports the production scorer's behaviour (FRR-directional) on their number words and the
five fixed sentences. /r/ excluded (UK non-rhotic cannot be assumed). No gate — reference only.
"""
from __future__ import annotations

import json
import sys
from collections import Counter, defaultdict

import numpy as np
import soundfile as sf

import p31_common as C

sys.path.insert(0, str(C.REPO))
from Research.Speech.Phase1_9_21.experiments import p21_lib as L  # noqa: E402

OUT = C.ART / "native"
NUMBERS = {"six": "s", "seven": "n", "eight": "t", "nine": "n", "ten": "n", "one": "n", "five": "v"}
NUMBERS_SKIP = {"two": "vowel-final", "three": "vowel-final", "four": "non-rhotic /r/"}
SENT_LAST = {"horse": "s", "stable": "l", "pond": "d", "shed": "d", "top": "p",
             "dog": "g", "front": "t", "fish": "ʃ", "house": "s"}
SENT_SKIP = {"car": "non-rhotic /r/"}
MICS = ("studio_mic", "port_mic", "nao_mic")


def native_speakers():
    base = C.ZEN / "extracted/english_children/english_words_sentences"
    return sorted(p for p in base.iterdir() if p.is_dir() and p.name.endswith("_native"))


def find_wav(spk_dir, kind, word):
    for mic in MICS:
        p = spk_dir / mic / kind / f"{word}.wav"
        if p.exists():
            return p, mic
    return None, None


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2
    pev = PhoneEvidenceV2()
    pev._ensure()
    tgt = L.CmuDictTargetAdapter()
    rows = []

    def analyse(spk_dir, gender, kind, text, wav, mic, token_word):
        st = tgt.build(text)
        if st.warnings:
            return {"speaker": spk_dir.name, "kind": kind, "word": token_word,
                    "mic": mic, "err": ";".join(st.warnings)}
        target_seq = pev.inv.arpa_seq_to_canon([str(x) for x in st.arpabet])
        words = text.split()
        per_word = [pev.inv.arpa_seq_to_canon([str(x) for x in tgt.build(w).arpabet])
                    for w in words]
        bounds = []
        acc = 0
        for pw in per_word:
            bounds.append((acc, acc + len(pw)))
            acc += len(pw)
        wi = [i for i, w in enumerate(words) if w == token_word]
        if not wi:
            return None
        j = bounds[wi[-1]][1] - 1
        x = L.load_mono16(wav)
        import os
        import uuid
        tmp = OUT / f"_tmp_{os.getpid()}_{uuid.uuid4().hex[:6]}.wav"
        sf.write(str(tmp), x, 16000)
        try:
            probs_t, dur, _ = pev.logits(str(tmp))
        finally:
            try:
                tmp.unlink()
            except OSError:
                pass
        probs = probs_t.numpy().astype(np.float64)
        spans = pev.ctc_align(probs_t, target_seq)
        s, e = spans[j]
        names, class_ids, A = L.build_class_matrix(pev, probs)
        ci = {c: i for i, c in enumerate(names)}.get(target_seq[j])
        p = A[:, ci] if ci is not None else np.zeros(probs.shape[0])
        dec = L.baseline_final_decision(probs, spans, target_seq, class_ids, pev, j)
        return {"speaker": spk_dir.name, "gender": gender, "kind": kind, "word": token_word,
                "mic": mic, "target_phone": target_seq[j],
                "target_max": round(float(p[s:e + 1].max()), 6),
                "target_mean": round(float(p[s:e + 1].mean()), 6),
                "decision": int(dec["match"] in ("exact", "soft")),
                "match_type": dec["match"], "err": ""}

    for spk_dir in native_speakers():
        gender = spk_dir.name.split("_")[1]
        for word, _target in NUMBERS.items():
            wav, mic = find_wav(spk_dir, "numbers", word)
            if wav is None:
                rows.append({"speaker": spk_dir.name, "kind": "numbers", "word": word,
                             "mic": "", "err": "audio_missing"})
                continue
            r = analyse(spk_dir, gender, "numbers", word, wav, mic, word)
            if r:
                rows.append(r)
        for mic in MICS:
            sent_dir = spk_dir / mic / "sentences"
            if not sent_dir.exists():
                continue
            for wav in sorted(sent_dir.glob("*.wav")):
                text = wav.stem.replace("_", " ")
                for token_word in text.split():
                    if token_word in SENT_LAST:
                        r = analyse(spk_dir, gender, "sentences", text, wav, mic, token_word)
                        if r:
                            rows.append(r)
            break  # first available mic only
    C.write_csv(OUT / "native_check.csv", rows,
                ["speaker", "gender", "kind", "word", "mic", "target_phone",
                 "target_max", "target_mean", "decision", "match_type", "err"])
    ok = [r for r in rows if not r.get("err")]
    summary = {"tokens": len(ok), "speakers": len({r["speaker"] for r in ok}),
               "accepted": sum(r["decision"] for r in ok),
               "FRR_directional": round(1 - sum(r["decision"] for r in ok) / len(ok), 4) if ok else None,
               "missing_evidence": round(sum(1 for r in ok if r["target_max"] < 0.02) / len(ok), 4) if ok else None,
               "median_target_max": round(float(np.median([r["target_max"] for r in ok])), 4) if ok else None,
               "per_phone": {}, "errors": [r for r in rows if r.get("err")]}
    for ph in sorted({r["target_phone"] for r in ok}):
        sub = [r for r in ok if r["target_phone"] == ph]
        summary["per_phone"][ph] = {
            "n": len(sub), "accepted": sum(r["decision"] for r in sub),
            "median_target_max": round(float(np.median([r["target_max"] for r in sub])), 4)}
    C.write_json(OUT / "native_check_summary.json", summary)
    print(json.dumps(summary, indent=1, ensure_ascii=False))
    print("DONE p31_native")


if __name__ == "__main__":
    main()
