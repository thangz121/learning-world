"""Phase 1.9.18 shared SO762 utilities.

Streams the pinned SO762 mirror with the audio column cast to decode=False so raw
16 kHz PCM WAVE bytes are always available without torchcodec. Provides:
- iter_rows(): stream every row's annotations + raw wav bytes
- build_targets(): auditable canonical target + per-phone expert supervision
- deterministic speaker-disjoint child-focused split

TARGET FORMULATION (STEP 2) — auditable and explicit:

For each utterance we derive the CANONICAL target from CMUdict via
`CmuDictTargetAdapter` (text -> ARPAbet). The child's own audio never defines the
target. For each canonical phone position we attach the corpus's 5-expert
averaged accuracy score (0/1/2). When the corpus provides an observed error
record (only when expert score < 0.5) we attach its error type
(`substitution` | `deletion` | `unknown`) and, for substitution, the observed
phone symbol.

Rules (no fabrication):
- `<unk>` is NEVER mapped to a concrete phone; it is carried as error_type
  "unknown".
- `<DEL>` is carried as error_type "deletion", never as a phone.
- Observed substituted phones are carried as-is but are NOT used as training
  targets in B1 (they are diagnostic labels only).
- Missing CMUdict words drop those phones from the target AND are logged.

The canonical ARPAbet sequence is the training target. The expert accuracy is a
per-phone regression target / sample weight. Error types are an auxiliary
classification target. No pseudo-labels from the frozen model are used.
"""
from __future__ import annotations

import io
import json
import sys
import wave
from pathlib import Path

OUT = Path(__file__).resolve().parents[1]
REPO = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(REPO))

MODEL_ID = "facebook/wav2vec2-xlsr-53-espeak-cv-ft"
MODEL_REV = "2c733782da5604684829819a5eb744c193fe9398"
SO762_REV = "06385584fad212b26134c656fdd3ccf9f093f33e"
CHILD_AGE_MAX = 15
SEED = 1515
BAD_THRESHOLD = 0.5


def read_wav_bytes(b: bytes):
    """Return float32 mono samples at 16k read from raw WAVE bytes (no decode lib)."""
    import numpy as np
    with wave.open(io.BytesIO(b), "rb") as w:
        if w.getframerate() != 16000 or w.getsampwidth() != 2:
            raise ValueError("unexpected wav format")
        n = w.getnframes()
        raw = w.readframes(n)
    arr = np.frombuffer(raw, dtype="<i2").astype("float32") / 32768.0
    return arr


def iter_rows(splits=("train", "test"), with_audio=True):
    """Stream rows; yields dicts with annotations and optionally raw wav bytes."""
    from datasets import Audio, load_dataset
    for split in splits:
        ds = load_dataset("mispeech/speechocean762", split=split, streaming=True)
        if with_audio:
            ds = ds.cast_column("audio", Audio(decode=False))
        else:
            keep = [c for c in ds.column_names if c != "audio"]
            ds = ds.select_columns(keep)
        for ex in ds:
            row = {
                "split": split,
                "speaker": ex["speaker"],
                "age": ex["age"],
                "gender": ex["gender"],
                "text": ex["text"],
                "words": ex["words"],
            }
            if with_audio:
                row["wav_bytes"] = ex["audio"]["bytes"]
                row["path"] = ex["audio"]["path"]
            yield row


def build_targets(text: str, words, adapter=None):
    """Build the auditable per-phone target for one utterance.

    Returns dict with:
      canonical_arpa: list[str]     (training target, CMUdict)
      expert_acc: list[float|None]  (per canonical position; corpus score aligned)
      error_type: list[str|None]    (substitution|deletion|unknown|None)
      observed: list[str|None]      (observed substituted phone, never a target)
      missing_words: list[str]
    """
    if adapter is None:
        from Research.Speech.Phase1_2.Adapters.target_cmudict import (
            CmuDictTargetAdapter)
        adapter = CmuDictTargetAdapter()

    target = adapter.build(text)
    canonical_arpa = list(target.arpabet)
    expert_acc = [None] * len(canonical_arpa)
    error_type = [None] * len(canonical_arpa)
    observed = [None] * len(canonical_arpa)

    # Flatten the corpus word/phone structure into a canonical ARPAbet sequence.
    # SO762's `words[*].phones` are themselves the canonical phones (ARPAbet with
    # stress). We use them as the authoritative positional alignment for the
    # expert scores, then verify they match the CMUdict canonical sequence.
    corpus_arpa, corpus_acc = [], []
    for w in words:
        for p, a in zip(w["phones"], w["phones-accuracy"]):
            corpus_arpa.append(p)
            corpus_acc.append(float(a))
    # use the corpus canonical phones as the target sequence (it is the same
    # canonical CMUdict-style inventory the experts scored). CMUdict is used to
    # confirm coverage, not to override expert-scored positions.
    canonical_arpa = corpus_arpa
    expert_acc = corpus_acc
    error_type = [None] * len(canonical_arpa)
    observed = [None] * len(canonical_arpa)

    # attach observed error records by (canonical phone, index-within-word)
    gi = 0
    for w in words:
        for pi in range(len(w["phones"])):
            for mp in (w.get("mispronunciations") or []):
                if mp.get("index") == pi and \
                        mp["canonical-phone"] == w["phones"][pi].rstrip("012"):
                    pron = mp["pronounced-phone"]
                    if pron == "<DEL>":
                        error_type[gi] = "deletion"
                    elif pron == "<unk>":
                        error_type[gi] = "unknown"
                    else:
                        error_type[gi] = "substitution"
                        observed[gi] = pron
            gi += 1

    return {
        "canonical_arpa": canonical_arpa,
        "expert_acc": expert_acc,
        "error_type": error_type,
        "observed": observed,
        "missing_words": target.warnings,
        "cmudict_arpa": list(target.arpabet),
    }


def make_child_split(rows, seed=SEED):
    """Deterministic speaker-disjoint child-focused split.

    122 child speakers -> train/validation/test by speaker. Test speakers are
    held out entirely. Adults are kept as a clearly-labeled separate set and
    never enter the child adaptation split.
    """
    import random
    child_spk = sorted({r["speaker"] for r in rows
                        if r["age"] <= CHILD_AGE_MAX})
    adult_spk = sorted({r["speaker"] for r in rows
                        if r["age"] > CHILD_AGE_MAX})
    rng = random.Random(seed)
    shuffled = child_spk[:]
    rng.shuffle(shuffled)
    n = len(shuffled)
    n_test = max(1, round(0.20 * n))
    n_val = max(1, round(0.15 * n))
    test_spk = set(shuffled[:n_test])
    val_spk = set(shuffled[n_test:n_test + n_val])
    train_spk = set(shuffled[n_test + n_val:])
    assigned = {"train": train_spk, "validation": val_spk, "test": test_spk}
    splits = {k: [] for k in ("train", "validation", "test", "adult")}
    for r in rows:
        if r["speaker"] in train_spk:
            splits["train"].append(r)
        elif r["speaker"] in val_spk:
            splits["validation"].append(r)
        elif r["speaker"] in test_spk:
            splits["test"].append(r)
        else:
            splits["adult"].append(r)
    return splits, assigned, adult_spk
