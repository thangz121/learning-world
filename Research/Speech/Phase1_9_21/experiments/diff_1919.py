"""Reproduce WP-1.9.19 window evidence with the original 1.9.19 code path.

Used to attribute any frame-cache determinism mismatch (model/audio/preprocessing
drift vs logic drift). Research-only helper.
"""
from __future__ import annotations

import csv
import importlib.util
import sys
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import p21_lib as L  # noqa: E402

spec = importlib.util.spec_from_file_location(
    "wc19", L.REPO / "Research/Speech/Phase1_9_19/experiments/window_causality.py")
wc = importlib.util.module_from_spec(spec)
spec.loader.exec_module(wc)


def main():
    tokens = sys.argv[1:] or ["child_02_five:pad100", "child_02_three:pad100"]
    pev = wc.PhoneEvidenceV2()
    pev._ensure()
    class_ids = {c: ids for c, ids in pev._canon_ids.items() if c != "sil"}
    tgt = wc.CmuDictTargetAdapter()
    rows = list(csv.DictReader(open(L.P1919 / "window_rows_lwe.csv", encoding="utf-8")))
    for spec_s in tokens:
        tid, win = spec_s.split(":")
        sid, word = tid.rsplit("_", 1)
        row = [r for r in rows if r["token_id"] == tid and r["window_type"] == win][0]
        src = wc.find_source(sid, word)
        x = wc.load_mono16(src)
        st = tgt.build(word)
        arpa = [str(v) for v in st.arpabet]
        target = pev.inv.arpa_seq_to_canon(arpa)
        a, b = float(row["audio_start"]), float(row["audio_end"])
        ev = wc.analyze_window(pev, class_ids, x, 16000, a, b, arpa, target,
                               len(target) - 1, L.REPO / "Research/Speech/Phase1_9_21/artifacts")
        ev2 = wc.analyze_window(pev, class_ids, x, 16000, a, b, arpa, target,
                                len(target) - 1, L.REPO / "Research/Speech/Phase1_9_21/artifacts")
        print(f"{tid} {win} stored_ev={row['deletion_aware_evidence']} "
              f"stored_target={row['target_posterior']} stored_span={row['span_start']}-{row['span_end']}")
        print("   wc rerun 1:", {k: ev[k] for k in ("deletion_aware_evidence", "target_posterior",
                                                    "span_start", "span_end", "baseline_match")})
        print("   wc rerun 2:", {k: ev2[k] for k in ("deletion_aware_evidence", "target_posterior",
                                                    "span_start", "span_end", "baseline_match")})
        same = (ev["deletion_aware_evidence"] == ev2["deletion_aware_evidence"])
        print("   deterministic across reruns:", same)
        # our path: same segment through p21 lib
        import soundfile as sf
        tmp = L.REPO / "Research/Speech/Phase1_9_21/artifacts" / "_diff_tmp.wav"
        seg = x[int(a * 16000):int(b * 16000)]
        sf.write(str(tmp), seg, 16000)
        probs_t, dur, _ = pev.logits(str(tmp))
        probs = probs_t.numpy().astype(np.float64)
        T = probs.shape[0]
        spans = pev.ctc_align(probs_t, target)
        names, cids, A_class = L.build_class_matrix(pev, probs)
        cindex = {c: i for i, c in enumerate(names)}
        j = len(target) - 1
        canon = target[j]
        p = A_class[:, cindex[canon]]
        p2 = L.p2_module()
        da_all = L.deletion_all(probs, target, cids, pev._blank, p2)
        daf = L.deletion_for_focus(da_all, p, j)
        print("   p21 lib :", {"da_max": round(daf["da_max"], 4), "da_span": daf["da_span"],
                               "span": spans[j], "T": T,
                               "frame_max": round(float(p.max()), 4)})
        tmp.unlink(missing_ok=True)


if __name__ == "__main__":
    main()
