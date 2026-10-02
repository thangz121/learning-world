"""Forensic: correct-ASR / low-phone-score LWE words + phone inventory audit."""
from __future__ import annotations
import json, sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_2.pipeline import SpeakingPipeline
from Research.Speech.Phase1_2.Adapters.paths import PHASE11_AUDIO, ARPA2IPA, strip_stress
from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
from Research.Speech.Phase1_2.Adapters.phone_w2v2 import Wav2Vec2PhoneAdapter

OUT = REPO / "Research" / "Speech" / "Phase1_3" / "Forensics"
OUT.mkdir(parents=True, exist_ok=True)

PROBLEMS = [
    ("sapi_blue.wav", "blue"),
    ("pregen_apple_normal.wav", "apple"),
    ("sapi_book.wav", "book"),
    ("sapi_big.wav", "big"),
    ("sapi_dog.wav", "dog"),
    ("sapi_cat.wav", "cat"),
    ("sapi_red.wav", "red"),  # control high
]


def main():
    pipe = SpeakingPipeline(enable_openpronounce=False)
    phone_ad = Wav2Vec2PhoneAdapter()
    tgt_ad = CmuDictTargetAdapter()
    rows = []
    for fn, word in PROBLEMS:
        r = pipe.run(str(PHASE11_AUDIO / fn), word)
        pe = r.phone
        tgt = r.target
        s1 = r.scores["scorer_v1"]
        asr_norm = "".join(c for c in r.asr.text.lower() if c.isalpha())
        tgt_norm = "".join(c for c in word.lower() if c.isalpha())
        asr_ok = asr_norm == tgt_norm or asr_norm.startswith(tgt_norm)
        row = {
            "file": fn, "target": word,
            "asr": r.asr.text, "asr_match": asr_ok,
            "target_arpa": tgt.arpabet, "target_ipa": tgt.ipa,
            "observed_phones": [p.phone for p in pe.phones],
            "observed_confs": [round(p.conf, 3) for p in pe.phones],
            "frame_mean_conf": pe.frame_mean_conf,
            "score": s1.score_0_100, "conf": s1.confidence_0_1, "per": s1.per,
            "diagnostics": [
                {"exp": d.expected, "obs": d.observed, "status": d.status,
                 "score": d.score, "conf": d.confidence}
                for d in s1.diagnostics
            ],
            "vad_segs": [(s.start_s, s.end_s) for s in r.vad.segments],
            "f0": r.acoustic.f0_mean, "f1": r.acoustic.f1_mean, "f2": r.acoustic.f2_mean,
            "warnings": r.warnings,
        }
        # classify failure source heuristically
        sources = []
        if not asr_ok and r.asr.text.strip() == "":
            sources.append("asr_empty")
        if asr_ok and s1.per and s1.per > 0.5:
            sources.append("phone_model_or_inventory_mismatch")
        if asr_ok and s1.per == 0 and s1.score_0_100 < 70:
            sources.append("low_phone_posterior_despite_match")
        hyp_set = set(p.phone for p in pe.phones)
        exp_set = set(tgt.ipa)
        if exp_set - hyp_set:
            sources.append(f"missing_expected_phones:{sorted(exp_set - hyp_set)}")
        if not r.vad.speech_detected:
            sources.append("vad_no_speech")
        row["failure_sources"] = sources or ["none_or_true_mismatch"]
        rows.append(row)
        print("FOR", word, "asr", r.asr.text, "score", s1.score_0_100, "per", s1.per,
              "obs_n", len(pe.phones), "src", sources, flush=True)

    (OUT / "lwe_forensics.json").write_text(json.dumps(rows, indent=2, ensure_ascii=False), encoding="utf-8")

    # inventory audit
    phone_ad._ensure()
    inv = sorted(set(phone_ad._id2tok.values()) - {"|", "", "<pad>", "[PAD]"})
    arpa_ipa = sorted(set(ARPA2IPA.values()))
    unmapped_ipa_needed = []
    # check LWE words
    t = CmuDictTargetAdapter()
    for w in ["red", "blue", "apple", "book", "big", "dog", "cat", "please", "ball"]:
        st = t.build(w)
        for ipa in st.ipa:
            if ipa not in inv and ipa not in unmapped_ipa_needed:
                # check close
                unmapped_ipa_needed.append(ipa)
    audit = {
        "w2v2_inventory_size": len(inv),
        "w2v2_inventory_sample": inv[:40],
        "arpa2ipa_values": arpa_ipa,
        "lwe_ipa_not_in_w2v2_exact": unmapped_ipa_needed,
        "notes": [
            "espeak IPA may use different glyph variants (e.g. ɡ vs g, ɹ vs r)",
            "stress stripped before IPA map",
            "affricates/diphthongs may be multi-symbol in one inventory and split in another",
        ],
    }
    # exact membership check with normalized forms
    def norm(s):
        return s.replace("ɡ", "g").replace("ɹ", "r").replace("ː", "")
    inv_n = {norm(x) for x in inv}
    soft_missing = []
    for w in ["red", "blue", "apple", "book", "big", "dog", "cat", "please", "ball", "one"]:
        st = t.build(w)
        for ipa, arpa in zip(st.ipa, st.arpabet):
            if norm(ipa) not in inv_n and ipa not in inv:
                soft_missing.append({"word": w, "arpa": arpa, "ipa": ipa})
    audit["soft_missing_after_norm"] = soft_missing
    (OUT / "phone_inventory_audit.json").write_text(json.dumps(audit, indent=2, ensure_ascii=False), encoding="utf-8")
    print("AUDIT missing", soft_missing, flush=True)


if __name__ == "__main__":
    main()
