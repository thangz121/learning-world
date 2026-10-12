"""WP-1.9.31 shared helpers — external child standard (research-only).

Nothing here trains or touches production code. Label rule is frozen in
Research/Speech/Phase1_9_31/00_PREREGISTRATION.md section 3.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
SO = Path(r"D:\speech-lab\data\speechocean762")
ZEN = REPO / "Research/Speech/ExternalData/zenodo_200495"
P31 = REPO / "Research/Speech/Phase1_9_31"
ART = P31 / "artifacts"
P27 = REPO / "Research/Speech/Phase1_9_27"
SO_DETAIL = SO / "resource" / "scores-detail.json"

sys.path.insert(0, str(REPO))
sys.path.insert(0, str(REPO / "Research/Speech/Phase1_9_21/experiments"))

from Research.Speech.Phase1_4.PhoneInventory.inventory import PhonemeInventoryAdapter  # noqa: E402

INV = PhonemeInventoryAdapter()
VOWELS = {"ɑ", "æ", "ə", "ɔ", "aʊ", "aɪ", "ɛ", "ɝ", "eɪ", "ɪ", "iː", "oʊ", "ɔɪ", "ʊ", "uː"}


def load_detail():
    return json.loads(SO_DETAIL.read_text(encoding="utf-8"))


def load_kv2(path: Path):
    out = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        parts = line.split()
        if len(parts) >= 2:
            out[parts[0]] = parts[1]
    return out


def load_spk2utt(path: Path):
    out = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        parts = line.split()
        if parts:
            out[parts[0]] = parts[1:]
    return out


def kid_speakers(split: str, max_age: int = 15):
    ages = load_kv2(SO / split / "spk2age")
    utts = load_spk2utt(SO / split / "spk2utt")
    out = []
    for spk, a in ages.items():
        try:
            ai = int(float(a))
        except ValueError:
            continue
        if ai <= max_age:
            out.append((spk, ai, utts.get(spk, [])))
    return sorted(out)


def parse_word_marks(expert_strings, n_ref: int):
    """Per reference-position 3-way marks for ONE word from 5 expert strings.

    Drops [insertions]; (X)->'0', {X}->'1', X->'2'. Returns (per_pos, ok) where
    per_pos[i] is the list of marks across experts at reference position i.
    ok=False when any expert string does not align to n_ref (token row then
    carries empty marks and is excluded from labelled sets).
    """
    per_pos = [[] for _ in range(n_ref)]
    ok = True
    for es in expert_strings:
        j = 0
        for tok in str(es).split():
            if tok.startswith("[") and tok.endswith("]"):
                continue
            if j >= n_ref:
                ok = False
                break
            if tok.startswith("{") and tok.endswith("}"):
                per_pos[j].append("1")
            elif tok.startswith("(") and tok.endswith(")"):
                per_pos[j].append("0")
            else:
                per_pos[j].append("2")
            j += 1
        if not ok or j != n_ref:
            ok = False
            break
    return per_pos, ok


def label_for_marks(marks):
    """Frozen mapping (prereg section 3): PRESENT iff P2+P1>=3, ABSENT iff A0>=3."""
    if len(marks) != 5:
        return "", -1, -1, -1
    p2 = marks.count("2")
    p1 = marks.count("1")
    a0 = marks.count("0")
    if p2 + p1 >= 3:
        lab = "PRESENT"
    elif a0 >= 3:
        lab = "ABSENT"
    else:  # unreachable with 5 marks
        lab = ""
    return lab, p2, p1, a0


def iter_utt_tokens(detail_utt):
    """Yield (k, word_index, pos_in_word, ref_phone, canon, is_final, word_text).

    k = flattened reference index (same convention as the frozen pilot token ids).
    """
    ref_flat, fin_flags, wtext, wpos = [], [], [], []
    word_refs = []
    for wi, w in enumerate(detail_utt.get("words", [])):
        ref = (w.get("ref-phones") or "").split()
        word_refs.append(len(ref))
        for pi, ph in enumerate(ref):
            ref_flat.append(ph)
            fin_flags.append(pi == len(ref) - 1)
            wtext.append(w.get("text", ""))
            wpos.append(wi)
    canon = INV.arpa_seq_to_canon(ref_flat)
    if len(canon) != len(ref_flat):
        return
    bounds = []
    acc = 0
    for n in word_refs:
        bounds.append((acc, acc + n))
        acc += n
    for k in range(len(canon)):
        wi = wpos[k]
        pos = k - bounds[wi][0]
        yield k, wi, pos, ref_flat[k], canon[k], fin_flags[k], wtext[k]


def write_json(path: Path, obj):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, indent=2, ensure_ascii=False), encoding="utf-8")


def write_csv(path: Path, rows, fields):
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", newline="", encoding="utf-8") as fh:
        w = csv.DictWriter(fh, fieldnames=fields)
        w.writeheader()
        for r in rows:
            w.writerow(r)
