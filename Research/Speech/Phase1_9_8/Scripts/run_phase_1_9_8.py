"""Phase 1.9.8 — real ~4yo child speech validation (Zenodo 200495 primary)."""
from __future__ import annotations

import csv
import hashlib
import json
import re
import sys
import time
import wave
from collections import Counter, defaultdict
from pathlib import Path
from typing import Dict, List, Optional, Tuple

import numpy as np
import soundfile as sf

REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

from Research.Speech.Phase1_9.HybridVAD.hybrid_vad import HybridVAD

OUT = REPO / "Research/Speech/Phase1_9_8"
RES = OUT / "Results"
HR = OUT / "HumanReview"
DER = REPO / "Research/Speech/ExternalData/zenodo_200495/derived_16k"
EXT = REPO / "Research/Speech/ExternalData/zenodo_200495/extracted/english_children"
for d in (OUT, RES, HR, DER, OUT / "Scripts"):
    d.mkdir(parents=True, exist_ok=True)

IMG_WAV = REPO / "Research/Speech/Phase1_8/AudioDerived/IMG_0639_16k_mono.wav"
NEW_WAV = REPO / "Research/Speech/Phase1_8/AudioDerived/NEW_16k_mono.wav"

# Known predefined sentence targets from filenames
SENTENCE_MAP = {
    "the_dog_is_in_front_of_the_horse": "the dog is in front of the horse",
    "the_dog_is_on_top_of_the_shed": "the dog is on top of the shed",
    "the_fish_is_in_the_pond": "the fish is in the pond",
    "the_horse_is_behind_the_car": "the horse is behind the car",
    "the_horse_is_next_to_the_stable": "the horse is next to the stable",
}
NUMBERS = {"one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten"}


def sha256_file(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for c in iter(lambda: f.read(1 << 20), b""):
            h.update(c)
    return h.hexdigest().upper()


def load_any_mono16(path: Path) -> Tuple[np.ndarray, int, Path]:
    """Load audio, resample to 16k mono if needed; cache derived wav."""
    cache = DER / (sha256_file(path)[:16] + "_" + re.sub(r"[^A-Za-z0-9_.-]+", "_", path.name)[:80] + ".wav")
    if cache.exists():
        x, sr = sf.read(str(cache))
        if x.ndim > 1:
            x = x.mean(axis=1)
        return x.astype(np.float32), int(sr), cache
    x, sr = sf.read(str(path))
    if x.ndim > 1:
        x = x.mean(axis=1)
    x = x.astype(np.float32)
    if sr != 16000:
        # linear resample
        n_out = int(len(x) * 16000 / sr)
        t_old = np.linspace(0, 1, num=len(x), endpoint=False)
        t_new = np.linspace(0, 1, num=n_out, endpoint=False)
        x = np.interp(t_new, t_old, x).astype(np.float32)
        sr = 16000
    sf.write(str(cache), x, sr)
    return x, sr, cache


def write_tmp16(x: np.ndarray, sr: int, path: Path):
    sf.write(str(path), x, sr)


def segs_of(r) -> List[Tuple[float, float]]:
    return [(float(s["start"]), float(s["end"])) for s in r["segments"]]


def seg_stats(segs, duration):
    if not segs:
        return dict(
            n_segments=0,
            speech_duration_s=0.0,
            speech_ratio=0.0,
            median_segment_duration=None,
            mean_segment_duration=None,
            min_segment_duration=None,
            max_segment_duration=None,
            very_short_segment_count=0,
            fragmentation=0.0,
            mean_gap_s=None,
            n_gaps=0,
        )
    durs = [e - s for s, e in segs]
    gaps = [segs[i + 1][0] - segs[i][1] for i in range(len(segs) - 1)]
    speech = sum(durs)
    return dict(
        n_segments=len(segs),
        speech_duration_s=speech,
        speech_ratio=speech / duration if duration else 0.0,
        median_segment_duration=float(np.median(durs)),
        mean_segment_duration=float(np.mean(durs)),
        min_segment_duration=float(min(durs)),
        max_segment_duration=float(max(durs)),
        very_short_segment_count=sum(1 for d in durs if d < 0.2),
        fragmentation=(len(segs) / speech) if speech > 0 else 0.0,
        mean_gap_s=float(np.mean(gaps)) if gaps else None,
        n_gaps=len(gaps),
    )


def cont_features(x, sr):
    frame = int(0.03 * sr)
    hop = int(0.01 * sr)
    rms = []
    for i in range(0, max(1, len(x) - frame), hop):
        rms.append(float(np.sqrt(np.mean(x[i : i + frame] ** 2) + 1e-20)))
    rms = np.array(rms) if rms else np.array([0.0])
    floor = np.percentile(rms, 20)
    thr = floor * 2.0
    cont = float(np.mean(rms > thr))
    p90 = float(np.percentile(rms, 90))
    p20 = float(np.percentile(rms, 20) + 1e-12)
    contrast = float((p90 - p20) / (p90 + p20))
    # crude F0 proxy via zero-crossing rate (not true F0)
    zc = float(np.mean(np.abs(np.diff(np.signbit(x).astype(np.int8))))) if len(x) > 1 else 0.0
    return dict(
        continuous_energy_ratio_proxy=cont,
        energy_contrast_proxy=contrast,
        rms_mean=float(np.mean(rms)),
        zcr_proxy=zc,
    )


def parse_speaker(folder_name: str):
    # 01_M_native
    m = re.match(r"(\d+)_(M|F)_(native|nonNative)", folder_name)
    if not m:
        return None, None, None
    return f"child_{m.group(1)}", m.group(2), m.group(3)


def inventory_dataset() -> List[Dict]:
    rows = []
    # words/sentences: speaker / {studio_mic|port_mic|nao_mic} / {numbers|sentences} / *.wav
    ws = EXT / "english_words_sentences"
    for sp_dir in sorted(ws.iterdir()):
        if not sp_dir.is_dir():
            continue
        sid, sex, native = parse_speaker(sp_dir.name)
        for wav in sp_dir.rglob("*.wav"):
            parts = wav.relative_to(sp_dir).parts
            mic = parts[0] if parts else "unknown"
            stem = wav.stem
            if stem in NUMBERS:
                task = "number_counting"
                cat = "TARGETED_CHILD_SPEECH"
                target = stem
            elif stem in SENTENCE_MAP:
                task = "predefined_sentence"
                cat = "TARGETED_CHILD_SPEECH"
                target = SENTENCE_MAP[stem]
            elif stem.startswith("the_"):
                task = "predefined_sentence"
                cat = "TARGETED_CHILD_SPEECH"
                target = stem.replace("_", " ")
            else:
                task = "unknown_filename"
                cat = "UNKNOWN_TARGET"
                target = ""
            rows.append(
                dict(
                    dataset_id="zenodo_200495",
                    speaker_id=sid,
                    age="UNKNOWN_individual",
                    age_source="dataset_mean_M=4.9y_only",
                    age_bucket="4.x_years_dataset_mean",
                    sex_if_provided=sex,
                    language="English",
                    native_language_if_provided=native,
                    recording_id=f"{sid}_{mic}_{stem}",
                    filename=wav.name,
                    path=str(wav),
                    mic=mic,
                    task=task,
                    category=cat,
                    target=target,
                    transcript_available=bool(target),
                    target_available=bool(target),
                    timestamp_available=False,
                    license="CC-BY-4.0",
                    source_url="https://doi.org/10.5281/zenodo.200495",
                    role="CHILD_NORMAL",
                )
            )
    # free speech cut
    free_cut = EXT / "english_free_speech" / "files_cut_by_sentences"
    if free_cut.exists():
        for sp_dir in sorted(free_cut.iterdir()):
            if not sp_dir.is_dir():
                continue
            sid, sex, native = parse_speaker(sp_dir.name)
            for wav in sp_dir.glob("*.wav"):
                # transcript approx from filename
                t = wav.stem.strip()
                rows.append(
                    dict(
                        dataset_id="zenodo_200495",
                        speaker_id=sid,
                        age="UNKNOWN_individual",
                        age_source="dataset_mean_M=4.9y_only",
                        age_bucket="4.x_years_dataset_mean",
                        sex_if_provided=sex,
                        language="English",
                        native_language_if_provided=native,
                        recording_id=f"{sid}_free_{hashlib.md5(wav.name.encode()).hexdigest()[:8]}",
                        filename=wav.name,
                        path=str(wav),
                        mic="unknown_or_mixed",
                        task="spontaneous_retell_segment",
                        category="TRANSCRIBED_SPONTANEOUS_SPEECH",
                        target="",
                        transcript_available=True,
                        transcript_from_filename=t,
                        target_available=False,
                        timestamp_available=False,
                        license="CC-BY-4.0",
                        source_url="https://doi.org/10.5281/zenodo.200495",
                        role="CHILD_NORMAL",
                    )
                )
    # free full
    free_one = EXT / "english_free_speech" / "files_in_one_part"
    if free_one.exists():
        for sp_dir in sorted(free_one.iterdir()):
            if not sp_dir.is_dir():
                continue
            sid, sex, native = parse_speaker(sp_dir.name)
            for wav in sp_dir.rglob("*.wav"):
                rows.append(
                    dict(
                        dataset_id="zenodo_200495",
                        speaker_id=sid,
                        age="UNKNOWN_individual",
                        age_source="dataset_mean_M=4.9y_only",
                        age_bucket="4.x_years_dataset_mean",
                        sex_if_provided=sex,
                        language="English",
                        native_language_if_provided=native,
                        recording_id=f"{sid}_freefull_{wav.stem}_{wav.parent.name}",
                        filename=wav.name,
                        path=str(wav),
                        mic=wav.parent.name if wav.parent.name in ("studio", "port", "nao") else wav.stem,
                        task="spontaneous_retell_full",
                        category="TRANSCRIBED_SPONTANEOUS_SPEECH",
                        target="",
                        transcript_available=False,
                        target_available=False,
                        timestamp_available=False,
                        license="CC-BY-4.0",
                        source_url="https://doi.org/10.5281/zenodo.200495",
                        role="CHILD_NORMAL",
                    )
                )
    return rows


def write_csv(path: Path, rows: List[Dict]):
    if not rows:
        path.write_text("empty\n", encoding="utf-8")
        return
    keys = []
    for r in rows:
        for k in r:
            if k not in keys:
                keys.append(k)
    with open(path, "w", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=keys, extrasaction="ignore")
        w.writeheader()
        w.writerows(rows)


def main():
    assert EXT.exists(), EXT
    inv = inventory_dataset()
    # duration pass
    for r in inv:
        p = Path(r["path"])
        try:
            info = sf.info(str(p))
            r["duration"] = float(info.duration)
            r["sample_rate"] = int(info.samplerate)
            r["channels"] = int(info.channels)
            r["format"] = info.format
        except Exception as e:
            r["duration"] = None
            r["error"] = str(e)

    write_csv(RES / "recording_inventory.csv", inv)
    write_csv(RES / "dataset_inventory.csv", [
        dict(
            dataset_id="zenodo_200495",
            source_url="https://doi.org/10.5281/zenodo.200495",
            official_source="Zenodo",
            license="CC-BY-4.0",
            commercial_use_allowed=True,
            research_use_allowed=True,
            redistribution_allowed=True,
            download_allowed=True,
            attribution_required=True,
            share_alike_required=False,
            non_commercial=False,
            access_restrictions="none_open",
            consent_information_if_available="not_detailed_on_record_page",
            notes="RESEARCH_ONLY recommended for product embed until legal review; CC-BY-4.0 allows commercial with attribution",
            usage_class="POTENTIALLY_PRODUCT_COMPATIBLE_WITH_ATTRIBUTION",
            n_files=len(inv),
            n_wav_total=671,
        ),
        dict(
            dataset_id="CHILDES_Aligned",
            source_url="https://talkbank.org/childes/access/Derived/CHILDES-Aligned.html",
            license="TalkBank membership/terms — NOT auto-downloaded",
            research_use_allowed="conditional",
            download_allowed=False,
            notes="Audited only; access restrictions; not downloaded this phase",
            usage_class="RESEARCH_ONLY_IF_APPROVED",
        ),
        dict(
            dataset_id="CHILDES_Hall",
            source_url="https://talkbank.org/childes/access/Eng-NA/Hall.html",
            license="TalkBank terms — NOT auto-downloaded",
            download_allowed=False,
            notes="Audited only",
            usage_class="RESEARCH_ONLY_IF_APPROVED",
        ),
        dict(
            dataset_id="OCSC_Ohio_Child_Speech",
            source_url="investigate_public_versions",
            download_allowed=False,
            notes="No clear open bulk download confirmed this phase",
            usage_class="DATASET_ACCESS_UNCLEAR",
        ),
    ])

    speakers = {}
    for r in inv:
        sid = r["speaker_id"]
        if sid not in speakers:
            speakers[sid] = dict(
                speaker_id=sid,
                age="UNKNOWN_individual",
                age_source="dataset_mean_M=4.9y",
                age_bucket="4.x_years_dataset_mean",
                sex_if_provided=r.get("sex_if_provided"),
                native=r.get("native_language_if_provided"),
                n_recordings=0,
                total_duration_s=0.0,
            )
        speakers[sid]["n_recordings"] += 1
        speakers[sid]["total_duration_s"] += r.get("duration") or 0.0
    write_csv(RES / "speaker_inventory.csv", list(speakers.values()))

    hv = HybridVAD()
    tmp = RES / "_tmp16.wav"

    # Select evaluation set:
    # - all TARGETED studio mic if exists else port
    # - free full studio per child (1)
    # - sample free cut 2 per child
    targeted = [r for r in inv if r["category"] == "TARGETED_CHILD_SPEECH"]
    # prefer studio
    studio_t = [r for r in targeted if "studio" in str(r.get("mic", "")).lower()]
    if not studio_t:
        studio_t = targeted
    # one free full studio per speaker
    freefull = [
        r
        for r in inv
        if r["task"] == "spontaneous_retell_full"
        and "studio" in str(r.get("mic", "")).lower()
    ]
    freecut = [r for r in inv if r["task"] == "spontaneous_retell_segment"]
    # sample freecut: first 3 per speaker
    freecut_sample = []
    by_sp = defaultdict(list)
    for r in freecut:
        by_sp[r["speaker_id"]].append(r)
    for sid, lst in by_sp.items():
        freecut_sample.extend(lst[:3])

    eval_set = studio_t + freefull + freecut_sample
    # baselines NEW/IMG
    baselines = [
        dict(
            recording_id="NEW_1790",
            path=str(NEW_WAV),
            speaker_id="adult_ref",
            role="NORMAL_REFERENCE_ADULT",
            category="BASELINE",
            target="",
            age_bucket="adult",
            already_16k=True,
        ),
        dict(
            recording_id="IMG_0639",
            path=str(IMG_WAV),
            speaker_id="stress_ref",
            role="HIGH_COMPLEXITY_STRESS",
            category="BASELINE",
            target="",
            age_bucket="adult_or_unknown",
            already_16k=True,
        ),
    ]

    vad_rows = []
    acoustic_rows = []
    disagree_candidates = []

    def run_one(meta, path16: Path, x, sr):
        dur = len(x) / sr
        feats = cont_features(x, sr)
        write_tmp16(x, sr, tmp)
        out = dict(meta)
        out.update(feats)
        out["file_duration_s"] = dur
        for mode in ("silero", "hybrid_score"):
            t0 = time.time()
            r = hv.run(str(tmp), mode=mode, silero_thr=0.5, energy_margin_db=6.0)
            rt = time.time() - t0
            segs = segs_of(r)
            st = seg_stats(segs, dur)
            row = {
                **{k: out.get(k) for k in (
                    "recording_id", "speaker_id", "role", "category", "target", "age_bucket", "mic", "task"
                )},
                "mode": mode,
                "runtime_s": rt,
                **st,
                **feats,
            }
            vad_rows.append(row)
            if mode == "silero":
                sil_segs = segs
                sil_n = st["n_segments"]
            else:
                hyb_segs = segs
                hyb_n = st["n_segments"]
        acoustic_rows.append({**out, "silero_n": sil_n, "hybrid_n": hyb_n})
        # disagreement: silero empty but hybrid not, or large n diff
        if sil_n == 0 and hyb_n > 0:
            disagree_candidates.append((meta, path16, x, sr, hyb_segs, "silero0_hybrid_pos"))
        elif abs(sil_n - hyb_n) >= 3 and meta.get("category") == "TARGETED_CHILD_SPEECH":
            disagree_candidates.append((meta, path16, x, sr, hyb_segs if hyb_n > sil_n else sil_segs, "n_diff"))
        print(
            f"{meta.get('recording_id','')[:40]:40s} sil={sil_n:3d} hyb={hyb_n:3d} dur={dur:.2f}",
            flush=True,
        )

    # baselines
    for b in baselines:
        x, sr = sf.read(b["path"])
        if x.ndim > 1:
            x = x.mean(axis=1)
        x = x.astype(np.float32)
        run_one(b, Path(b["path"]), x, sr)

    # child eval
    for r in eval_set:
        try:
            x, sr, cache = load_any_mono16(Path(r["path"]))
            meta = {
                "recording_id": r["recording_id"],
                "speaker_id": r["speaker_id"],
                "role": r.get("role", "CHILD_NORMAL"),
                "category": r["category"],
                "target": r.get("target", ""),
                "age_bucket": r.get("age_bucket"),
                "mic": r.get("mic"),
                "task": r.get("task"),
            }
            run_one(meta, cache, x, sr)
        except Exception as e:
            vad_rows.append(dict(recording_id=r.get("recording_id"), error=str(e), mode="FAIL"))
            print("FAIL", r.get("recording_id"), e, flush=True)

    write_csv(RES / "vad_results.csv", vad_rows)
    write_csv(RES / "child_acoustic_results.csv", acoustic_rows)

    # age bucket summary (all child same bucket here)
    age_rows = []
    for bucket in sorted(set(r.get("age_bucket") for r in vad_rows if r.get("age_bucket"))):
        for mode in ("silero", "hybrid_score"):
            sub = [r for r in vad_rows if r.get("age_bucket") == bucket and r.get("mode") == mode and "n_segments" in r]
            if not sub:
                continue
            age_rows.append(
                dict(
                    age_bucket=bucket,
                    mode=mode,
                    n_files=len(sub),
                    mean_n_segments=float(np.mean([r["n_segments"] for r in sub])),
                    mean_speech_ratio=float(np.mean([r["speech_ratio"] for r in sub])),
                    mean_frag=float(np.mean([r["fragmentation"] for r in sub])),
                    pct_zero_seg=float(np.mean([1 if r["n_segments"] == 0 else 0 for r in sub])),
                )
            )
    write_csv(RES / "vad_child_age_results.csv", age_rows)

    # per-child summary
    child_sum = []
    for sid in sorted(speakers):
        for mode in ("silero", "hybrid_score"):
            sub = [r for r in vad_rows if r.get("speaker_id") == sid and r.get("mode") == mode and "n_segments" in r]
            if not sub:
                continue
            child_sum.append(
                dict(
                    speaker_id=sid,
                    mode=mode,
                    n_files=len(sub),
                    mean_n_segments=float(np.mean([r["n_segments"] for r in sub])),
                    mean_speech_ratio=float(np.mean([r["speech_ratio"] for r in sub])),
                    n_zero=sum(1 for r in sub if r["n_segments"] == 0),
                )
            )
    write_csv(RES / "vad_per_child_summary.csv", child_sum)

    # Human review pack from disagreements + some silero-empty cases
    review_clips = []
    rev_dir = HR / "clips_min2s"
    rev_dir.mkdir(parents=True, exist_ok=True)
    for i, (meta, path16, x, sr, segs, reason) in enumerate(disagree_candidates[:24]):
        if not segs:
            # whole file window start 2s
            s, e = 0.0, min(len(x) / sr, 2.0)
        else:
            st, en = segs[0]
            mid = 0.5 * (st + en)
            half = 1.0
            s = max(0.0, mid - half)
            e = min(len(x) / sr, mid + half)
            if e - s < 2.0:
                e = min(len(x) / sr, s + 2.0)
        cid = f"rev_{i+1:02d}"
        outp = rev_dir / f"{cid}.wav"
        sf.write(str(outp), x[int(s * sr) : int(e * sr)], sr)
        review_clips.append(
            dict(
                clip_id=cid,
                recording_id=meta.get("recording_id"),
                speaker_id=meta.get("speaker_id"),
                age=meta.get("age_bucket"),
                detector=reason,
                raw_start_s=segs[0][0] if segs else None,
                raw_end_s=segs[0][1] if segs else None,
                human_review_window_start=s,
                human_review_window_end=e,
                duration=e - s,
                clip_relpath=f"clips_min2s/{cid}.wav",
                human_label="",
                target=meta.get("target", ""),
            )
        )
    (HR / "review_metadata.json").write_text(json.dumps({"clips": review_clips, "label_schema": ["SPEECH", "NON_SPEECH", "MIXED", "UNCERTAIN"]}, indent=2), encoding="utf-8")
    write_csv(RES / "human_review_results.csv", review_clips)

    # HTML
    (HR / "review_min2s.html").write_text(
        """<!DOCTYPE html><html><head><meta charset=utf-8><title>1.9.8 child VAD review</title>
<style>body{font-family:system-ui,sans-serif;max-width:760px;margin:24px auto;padding:0 12px}
.card{border:1px solid #ccc;border-radius:8px;padding:12px;margin:10px 0}audio{width:100%}</style></head>
<body><h1>Phase 1.9.8 child VAD disagreement review (≥2s)</h1>
<p>Labels: SPEECH | NON_SPEECH | MIXED | UNCERTAIN. Child audio — anonymous IDs only.</p>
<div id=r>loading…</div>
<script>
fetch('review_metadata.json').then(r=>r.json()).then(m=>{
 let h='';
 for (const c of m.clips){
  h+=`<section class=card><h3>${c.clip_id}</h3>
  <p>rec=${c.recording_id}<br>speaker=${c.speaker_id} age=${c.age}<br>
  detector_note=${c.detector}<br>window ${Number(c.human_review_window_start).toFixed(2)}–${Number(c.human_review_window_end).toFixed(2)}s
  ${c.target?('<br>target='+c.target):''}</p>
  <audio controls preload=none src="${c.clip_relpath}"></audio></section>`;
 }
 document.getElementById('r').innerHTML=h;
});
</script></body></html>""",
        encoding="utf-8",
    )

    # Pronunciation on number targets (single words in CMUdict)
    pron_rows = []
    crop_rows = []
    try:
        from Research.Speech.Phase1_2.Adapters.target_cmudict import CmuDictTargetAdapter
        from Research.Speech.Phase1_4.SoftMatching.phone_evidence_v2 import PhoneEvidenceV2

        pev = PhoneEvidenceV2()
        tgt = CmuDictTargetAdapter()
        # studio numbers only, limit per child to keep runtime sane
        nums = [r for r in studio_t if r.get("target") in NUMBERS]
        # max 5 numbers x all children or cap 80
        nums = nums[:80]
        for r in nums:
            try:
                x, sr, cache = load_any_mono16(Path(r["path"]))
                st = tgt.build(r["target"])
                write_tmp16(x, sr, tmp)
                full = pev.soft_match(str(tmp), st.arpabet)
                # VAD crops
                rs = hv.run(str(tmp), mode="silero", silero_thr=0.5)
                rh = hv.run(str(tmp), mode="hybrid_score", silero_thr=0.5)

                def crop_score(segs, pad):
                    if not segs:
                        return None, "no_seg"
                    s0 = max(0.0, float(segs[0]["start"]) - pad)
                    s1 = min(len(x) / sr, float(segs[-1]["end"]) + pad)
                    cpath = RES / "_pcrop.wav"
                    sf.write(str(cpath), x[int(s0 * sr) : int(s1 * sr)], sr)
                    sc = pev.soft_match(str(cpath), st.arpabet)
                    return sc.soft_score_0_100, sc.confidence_0_1

                sil0, sil0c = crop_score(rs["segments"], 0.0)
                sil250, _ = crop_score(rs["segments"], 0.25)
                hyb0, _ = crop_score(rh["segments"], 0.0)
                hyb250, _ = crop_score(rh["segments"], 0.25)
                row = dict(
                    speaker_id=r["speaker_id"],
                    recording_id=r["recording_id"],
                    target=r["target"],
                    category="TARGETED_CHILD_SPEECH",
                    mic=r.get("mic"),
                    soft_full=full.soft_score_0_100,
                    conf_full=full.confidence_0_1,
                    silero_n=rs["n_segments"],
                    hybrid_n=rh["n_segments"],
                    soft_silero_raw=sil0,
                    soft_silero_pad250=sil250,
                    soft_hybrid_raw=hyb0,
                    soft_hybrid_pad250=hyb250,
                    delta_silero_raw=(None if sil0 is None else round(sil0 - full.soft_score_0_100, 2)),
                    delta_hybrid_raw=(None if hyb0 is None else round(hyb0 - full.soft_score_0_100, 2)),
                    note="child realization vs CMUdict canonical; SCORE!=CONFIDENCE; not gold pronunciation",
                )
                pron_rows.append(row)
                crop_rows.append(row)
                print("PRON", r["speaker_id"], r["target"], "full", full.soft_score_0_100, "sil", sil0, flush=True)
            except Exception as e:
                pron_rows.append(dict(recording_id=r.get("recording_id"), error=str(e)))
                print("PRON_FAIL", r.get("recording_id"), e, flush=True)
    except Exception as e:
        pron_rows.append(dict(status="SCORER_UNAVAILABLE", error=str(e)))
        print("SCORER", e)

    write_csv(RES / "pronunciation_results.csv", pron_rows)
    write_csv(RES / "pronunciation_crop_safety.csv", crop_rows)

    # simple error analysis buckets
    err = []
    for r in pron_rows:
        if r.get("soft_full") is None:
            continue
        sc = r["soft_full"]
        if sc >= 80:
            bucket = "high_score_child_vs_cmudict"
        elif sc >= 50:
            bucket = "mid_score_variation_or_partial"
        elif sc >= 20:
            bucket = "low_score_ambiguous"
        else:
            bucket = "very_low_score"
        # boundary sensitivity flag
        d = r.get("delta_silero_raw")
        flag = ""
        if d is not None and abs(d) >= 15:
            flag = "BOUNDARY_SENSITIVITY"
        err.append(dict(
            speaker_id=r.get("speaker_id"),
            target=r.get("target"),
            soft_full=sc,
            conf_full=r.get("conf_full"),
            bucket=bucket,
            boundary_flag=flag,
            delta_silero_raw=d,
        ))
    write_csv(RES / "child_error_analysis.csv", err)

    # Router research on child vs baselines
    router_rows = []
    C, X = 0.8, 0.4
    for r in acoustic_rows:
        cont = r.get("continuous_energy_ratio_proxy")
        contr = r.get("energy_contrast_proxy")
        if cont is None:
            continue
        route = "hybrid_score" if (cont > C and contr < X) else "silero"
        router_rows.append(
            dict(
                recording_id=r.get("recording_id"),
                speaker_id=r.get("speaker_id"),
                role=r.get("role"),
                category=r.get("category"),
                cont=cont,
                contrast=contr,
                C=C,
                X=X,
                route=route,
                silero_n=r.get("silero_n"),
                hybrid_n=r.get("hybrid_n"),
                locked=False,
            )
        )
    write_csv(RES / "router_research_results.csv", router_rows)

    # Decision stats
    child_vad = [r for r in vad_rows if r.get("role") == "CHILD_NORMAL" and r.get("mode") == "silero" and "n_segments" in r]
    child_hyb = [r for r in vad_rows if r.get("role") == "CHILD_NORMAL" and r.get("mode") == "hybrid_score" and "n_segments" in r]
    pct_sil_zero = float(np.mean([1 if r["n_segments"] == 0 else 0 for r in child_vad])) if child_vad else None
    pct_hyb_zero = float(np.mean([1 if r["n_segments"] == 0 else 0 for r in child_hyb])) if child_hyb else None
    mean_sil_n = float(np.mean([r["n_segments"] for r in child_vad])) if child_vad else None
    mean_hyb_n = float(np.mean([r["n_segments"] for r in child_hyb])) if child_hyb else None

    # targeted only
    t_sil = [r for r in child_vad if r.get("category") == "TARGETED_CHILD_SPEECH"]
    t_zero = float(np.mean([1 if r["n_segments"] == 0 else 0 for r in t_sil])) if t_sil else None

    n_children = len(speakers)
    n_rec = len(inv)
    n_targeted = sum(1 for r in inv if r["category"] == "TARGETED_CHILD_SPEECH")
    n_spon = sum(1 for r in inv if r["category"] == "TRANSCRIBED_SPONTANEOUS_SPEECH")
    total_min = sum((r.get("duration") or 0) for r in inv) / 60.0

    # pronunciation summary
    if any(r.get("soft_full") is not None for r in pron_rows):
        scores = [r["soft_full"] for r in pron_rows if r.get("soft_full") is not None]
        mean_score = float(np.mean(scores))
        low_frac = float(np.mean([1 if s < 50 else 0 for s in scores]))
    else:
        mean_score, low_frac = None, None

    child_vad_failure = bool(pct_sil_zero and pct_sil_zero > 0.15)
    # scorer "failure" = many very low on known targets - cautious
    child_scorer_issue = bool(low_frac is not None and low_frac > 0.5)

    if n_children >= 5 and t_sil and mean_score is not None:
        if child_vad_failure or child_scorer_issue:
            decision = "B. REAL_CHILD_VALIDATION_REQUIRES_CHILD_SPECIFIC_RESEARCH"
        else:
            decision = "A. REAL_CHILD_VALIDATION_SUPPORTS_CURRENT_PIPELINE"
    elif n_children > 0:
        decision = "C. REAL_CHILD_DATA_INSUFFICIENT" if (mean_score is None and not child_vad) else (
            "B. REAL_CHILD_VALIDATION_REQUIRES_CHILD_SPECIFIC_RESEARCH" if child_vad_failure or child_scorer_issue
            else "A. REAL_CHILD_VALIDATION_SUPPORTS_CURRENT_PIPELINE"
        )
    else:
        decision = "D. DATASET/LICENSE/BENCHMARK_BLOCKED"

    # refine: if silero works on most targeted but scores low vs adult CMUdict, that's B (child-specific scorer research)
    if t_zero is not None and t_zero < 0.1 and low_frac is not None and low_frac > 0.4:
        decision = "B. REAL_CHILD_VALIDATION_REQUIRES_CHILD_SPECIFIC_RESEARCH"

    master = dict(
        phase="1.9.8",
        decision=decision,
        production_vad=False,
        router_locked=False,
        unity_integrated=False,
        asr_is_not_pronunciation_judge=True,
        dataset=dict(
            primary="zenodo_200495",
            license="CC-BY-4.0",
            n_children=n_children,
            age_note="individual ages UNKNOWN; dataset mean M=4.9y",
            n_recordings_inventoried=n_rec,
            n_targeted=n_targeted,
            n_spontaneous=n_spon,
            total_minutes=total_min,
        ),
        vad=dict(
            child_silero_pct_zero=pct_sil_zero,
            child_hybrid_pct_zero=pct_hyb_zero,
            child_silero_mean_n=mean_sil_n,
            child_hybrid_mean_n=mean_hyb_n,
            targeted_silero_pct_zero=t_zero,
            n_disagree_review_clips=len(review_clips),
        ),
        pronunciation=dict(
            n_scored=len([r for r in pron_rows if r.get("soft_full") is not None]),
            mean_soft_full=mean_score,
            frac_soft_lt_50=low_frac,
            canonical="CMUdict",
            child_is_not_gold_pronunciation=True,
        ),
        router=dict(status="CHILD_ROUTER_NOT_CALIBRATED", hypothesis_C=0.8, hypothesis_X=0.4),
        comparison_groups=["NEW_1790", "IMG_0639", "REAL_CHILD_zenodo200495"],
    )
    (RES / "phase_1_9_8_master.json").write_text(json.dumps(master, indent=2), encoding="utf-8")
    (RES / "decision.json").write_text(
        json.dumps(
            dict(
                decision=decision,
                production_vad=False,
                router_locked=False,
                unity_integrated=False,
                child_vad_failure_found=child_vad_failure,
                child_scorer_issue_suspected=child_scorer_issue,
            ),
            indent=2,
        ),
        encoding="utf-8",
    )
    print("DECISION", decision)
    print("CHILDREN", n_children, "TARGETED", n_targeted, "MIN", round(total_min, 1))
    print("SIL_ZERO", pct_sil_zero, "T_ZERO", t_zero, "MEAN_SCORE", mean_score, "LOW_FRAC", low_frac)
    try:
        tmp.unlink()
    except Exception:
        pass
    try:
        (RES / "_pcrop.wav").unlink()
    except Exception:
        pass


if __name__ == "__main__":
    main()
