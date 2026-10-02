"""Round-1 outcome + min-2s listen pack + NEW speech reference."""
from __future__ import annotations
import csv
import hashlib
import json
import sys
from pathlib import Path

import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))
from Research.Speech.Phase1_9.HybridVAD.hybrid_vad import HybridVAD, load_mono16

OUT = REPO / "Research/Speech/Phase1_9_6"
HR = OUT / "HumanReview"
RES = OUT / "Results"
MIN_LISTEN_S = 2.0


def export_min_window(x, sr, st, en, min_s, out_path):
    mid = 0.5 * (st + en)
    half = max((en - st) / 2.0, min_s / 2.0)
    s = mid - half
    e = mid + half
    dur = len(x) / sr
    if s < 0:
        e = min(dur, e - s)
        s = 0.0
    if e > dur:
        s = max(0.0, s - (e - dur))
        e = dur
    if e - s < min_s - 1e-6:
        s = max(0.0, e - min_s)
        if e - s < min_s - 1e-6:
            e = min(dur, s + min_s)
    i0, i1 = int(s * sr), int(e * sr)
    sf.write(str(out_path), x[i0:i1], sr)
    return s, e, (e - s)


def main():
    audit = json.loads((RES / "clip_audit.json").read_text(encoding="utf-8"))
    clips = audit["clips"]

    with open(RES / "Human_Review_Labels_Filled.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(
            [
                "clip_id",
                "source_file",
                "raw_start_s",
                "raw_end_s",
                "raw_duration_s",
                "human_label",
                "notes",
                "reviewer_id",
                "review_date",
            ]
        )
        for c in clips:
            w.writerow(
                [
                    c["clip_id"],
                    c["source_file"],
                    c["raw_start_s"],
                    c["raw_end_s"],
                    c["raw_duration_s"],
                    "UNCERTAIN",
                    "too_short_cannot_identify_speech; require_min_2s_context",
                    "human_maynode",
                    "2026-10-02",
                ]
            )

    NEW_MP3 = REPO / "1790932799243_8856108714107255767_8856108714107255767.mp3"
    NEW_WAV = REPO / "Research/Speech/Phase1_8/AudioDerived/NEW_16k_mono.wav"
    IMG_WAV = REPO / "Research/Speech/Phase1_8/AudioDerived/IMG_0639_16k_mono.wav"
    new_sha = hashlib.sha256(NEW_MP3.read_bytes()).hexdigest().upper()
    assert new_sha == "17E3DA86267D1B7B864EA862A6C5C3B12AF4E49FD649F2FE140652A53DDF33FF"

    hv = HybridVAD()
    r_new = hv.run(str(NEW_WAV), mode="silero", silero_thr=0.5)
    new_segs = [(float(s["start"]), float(s["end"])) for s in r_new["segments"]]
    x_new, sr = load_mono16(str(NEW_WAV))
    x_img, _ = load_mono16(str(IMG_WAV))
    dur_new = len(x_new) / sr

    ref_dir = HR / "reference_NEW"
    ref_dir.mkdir(parents=True, exist_ok=True)
    ref_rows = []
    for i, (st, en) in enumerate(new_segs[:10]):
        path = ref_dir / f"NEW_ref_{i+1:02d}.wav"
        s, e, d = export_min_window(x_new, sr, st, en, MIN_LISTEN_S, path)
        ref_rows.append(
            {
                "ref_id": f"NEW_ref_{i+1:02d}",
                "source": "NEW_1790",
                "role": "SPEECH_REFERENCE_STANDARD",
                "silero_raw_start": st,
                "silero_raw_end": en,
                "export_start": s,
                "export_end": e,
                "export_duration": d,
                "clip": f"reference_NEW/NEW_ref_{i+1:02d}.wav",
            }
        )

    ctx_dir = HR / "clips_hybrid_min2s"
    ctx_dir.mkdir(parents=True, exist_ok=True)
    ext_rows = []
    for c in clips:
        st, en = c["raw_start_s"], c["raw_end_s"]
        path = ctx_dir / f"{c['clip_id']}_min2s.wav"
        s, e, d = export_min_window(x_img, sr, st, en, MIN_LISTEN_S, path)
        assert d >= MIN_LISTEN_S - 0.05, (c["clip_id"], d)
        ext_rows.append(
            {
                "clip_id": c["clip_id"],
                "source_file": c["source_file"],
                "raw_start_s": st,
                "raw_end_s": en,
                "raw_duration_s": c["raw_duration_s"],
                "listen_start_s": round(s, 6),
                "listen_end_s": round(e, 6),
                "listen_duration_s": round(d, 6),
                "min_listen_s": MIN_LISTEN_S,
                "clip_relpath": f"clips_hybrid_min2s/{c['clip_id']}_min2s.wav",
                "round1_label": "UNCERTAIN",
                "round1_reason": "clip_lt_0.5s_too_short_to_identify",
            }
        )

    (RES / "new_speech_reference.json").write_text(
        json.dumps(
            {
                "designation": "SPEECH_REFERENCE_STANDARD",
                "original_filename": NEW_MP3.name,
                "sha256": new_sha,
                "path_repo_root": NEW_MP3.name,
                "derived_16k": str(NEW_WAV.relative_to(REPO)).replace("\\", "/"),
                "duration_s": dur_new,
                "silero_n_segments": len(new_segs),
                "min_listen_s": MIN_LISTEN_S,
                "reference_clips": ref_rows,
                "human_statement": (
                    "NEW mp3 is the standard clear recording for calibrating SPEECH listening."
                ),
            },
            indent=2,
        ),
        encoding="utf-8",
    )

    (RES / "min2s_manifest.json").write_text(
        json.dumps(
            {
                "purpose": "Round-2 human review: each hybrid candidate centered in >=2s window",
                "min_listen_s": MIN_LISTEN_S,
                "raw_timestamps_unchanged": True,
                "n": len(ext_rows),
                "listen_dur_min": min(r["listen_duration_s"] for r in ext_rows),
                "listen_dur_mean": sum(r["listen_duration_s"] for r in ext_rows) / len(ext_rows),
                "clips": ext_rows,
            },
            indent=2,
        ),
        encoding="utf-8",
    )

    with open(RES / "Human_Review_Labels_min2s_template.csv", "w", newline="", encoding="utf-8") as f:
        w = csv.writer(f)
        w.writerow(
            [
                "clip_id",
                "raw_start_s",
                "raw_end_s",
                "raw_duration_s",
                "listen_duration_s",
                "human_label",
                "notes",
                "reviewer_id",
                "review_date",
            ]
        )
        for r in ext_rows:
            w.writerow(
                [
                    r["clip_id"],
                    r["raw_start_s"],
                    r["raw_end_s"],
                    r["raw_duration_s"],
                    r["listen_duration_s"],
                    "",
                    "",
                    "",
                    "",
                ]
            )

    print("OK refs", len(ref_rows), "min2s", len(ext_rows))
    print(
        "listen range",
        min(r["listen_duration_s"] for r in ext_rows),
        max(r["listen_duration_s"] for r in ext_rows),
    )


if __name__ == "__main__":
    main()
